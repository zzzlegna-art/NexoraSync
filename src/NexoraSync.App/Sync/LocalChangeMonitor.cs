using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NexoraSync.Sync;

public enum PendingChangeKind
{
    Created,
    Modified,
    Deleted
}

public sealed record PendingFileChange(
    Guid Id,
    string RelativePath,
    PendingChangeKind Kind,
    DateTimeOffset DetectedAt,
    long? Length,
    bool IsDirectory = false);

/// <summary>
/// Observes file and directory changes in one local shared folder and persists
/// a coalesced journal. It does not transfer or synchronize data remotely.
/// </summary>
public sealed class LocalChangeMonitor : IDisposable
{
    private readonly string _rootPath;
    private readonly string _journalPath;
    private readonly FileSystemWatcher _watcher;
    private readonly object _sync = new();
    private readonly List<PendingFileChange> _pending;
    private readonly HashSet<string> _knownDirectories = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounce = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public event Action<IReadOnlyList<PendingFileChange>>? QueueChanged;
    public event Action<string>? Error;

    public LocalChangeMonitor(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_rootPath);

        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexoraSync");
        Directory.CreateDirectory(settingsDirectory);

        var folderKey = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(_rootPath.ToUpperInvariant())))[..16].ToLowerInvariant();
        _journalPath = Path.Combine(settingsDirectory, $"pending-{folderKey}.json");
        _pending = LoadJournal();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(
                         _rootPath, "*", SearchOption.AllDirectories))
                _knownDirectories.Add(Path.GetFullPath(directory));
        }
        catch (Exception ex)
        {
            Error?.Invoke("Some existing subfolders could not be indexed: " + ex.Message);
        }

        // Migrate old journal entries for directories that still exist.
        for (var i = 0; i < _pending.Count; i++)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_rootPath, _pending[i].RelativePath));
            if (Directory.Exists(fullPath))
            {
                _knownDirectories.Add(fullPath);
                _pending[i] = _pending[i] with { IsDirectory = true, Length = null };
            }
        }
        PersistJournal();

        _watcher = new FileSystemWatcher(_rootPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 64 * 1024,
            EnableRaisingEvents = false
        };
        _watcher.Created += (_, e) =>
        {
            var isDirectory = Directory.Exists(e.FullPath);
            if (isDirectory) lock (_sync) _knownDirectories.Add(Path.GetFullPath(e.FullPath));
            Schedule(e.FullPath, isDirectory);
        };
        _watcher.Changed += (_, e) => Schedule(e.FullPath, Directory.Exists(e.FullPath));
        _watcher.Deleted += (_, e) =>
        {
            bool wasDirectory;
            lock (_sync)
            {
                wasDirectory = _knownDirectories.Contains(Path.GetFullPath(e.FullPath));
                if (wasDirectory) RemoveKnownDirectoryTree(e.FullPath);
            }
            Schedule(e.FullPath, wasDirectory);
        };
        _watcher.Renamed += (_, e) =>
        {
            bool wasDirectory;
            var oldPath = Path.GetFullPath(e.OldFullPath);
            var newPath = Path.GetFullPath(e.FullPath);
            lock (_sync)
            {
                wasDirectory = _knownDirectories.Contains(oldPath);
                if (wasDirectory) RemoveKnownDirectoryTree(oldPath);
                if (Directory.Exists(newPath)) _knownDirectories.Add(newPath);
            }
            Schedule(oldPath, wasDirectory);
            Schedule(newPath, Directory.Exists(newPath));
        };
        _watcher.Error += (_, e) => Error?.Invoke(
            "Windows reported a file-watcher overflow. A full folder rescan will be required before remote sync.");
        _watcher.EnableRaisingEvents = true;
    }

    public IReadOnlyList<PendingFileChange> PendingChanges
    {
        get { lock (_sync) return _pending.ToArray(); }
    }

    public void Acknowledge(Guid changeId)
    {
        lock (_sync)
        {
            _pending.RemoveAll(change => change.Id == changeId);
            PersistJournal();
        }
        PublishQueue();
    }

    private List<PendingFileChange> LoadJournal()
    {
        if (!File.Exists(_journalPath)) return new List<PendingFileChange>();
        try
        {
            return JsonSerializer.Deserialize<List<PendingFileChange>>(File.ReadAllText(_journalPath))
                   ?? new List<PendingFileChange>();
        }
        catch (Exception ex)
        {
            Error?.Invoke("The pending-change journal could not be read: " + ex.Message);
            return new List<PendingFileChange>();
        }
    }

    private void Schedule(string fullPath, bool isDirectoryHint)
    {
        if (_disposed) return;

        string relativePath;
        try
        {
            relativePath = Path.GetRelativePath(_rootPath, Path.GetFullPath(fullPath));
            if (Path.IsPathRooted(relativePath) ||
                relativePath.Equals("..", StringComparison.Ordinal) ||
                relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return;
        }
        catch { return; }

        var replacement = new CancellationTokenSource();
        var active = _debounce.AddOrUpdate(relativePath, replacement, (_, previous) =>
        {
            previous.Cancel();
            previous.Dispose();
            return replacement;
        });
        _ = ProcessAfterQuietPeriodAsync(relativePath, isDirectoryHint, active);
    }

    private async Task ProcessAfterQuietPeriodAsync(
        string relativePath, bool isDirectoryHint, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(700, cancellation.Token);
            if (!_debounce.TryGetValue(relativePath, out var current) ||
                !ReferenceEquals(current, cancellation))
                return;

            _debounce.TryRemove(relativePath, out _);
            ApplyChange(relativePath, isDirectoryHint);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (cancellation.IsCancellationRequested || !_debounce.ContainsKey(relativePath))
                cancellation.Dispose();
        }
    }

    private void ApplyChange(string relativePath, bool isDirectoryHint)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var existsAsFile = File.Exists(fullPath);
        var existsAsDirectory = Directory.Exists(fullPath);
        var isDirectory = existsAsDirectory || isDirectoryHint;

        lock (_sync)
        {
            if (!isDirectory && !existsAsFile && HasDeletedDirectoryAncestor(relativePath))
                return;

            if (isDirectory)
            {
                ApplyDirectoryChange(relativePath, fullPath, existsAsDirectory);
            }
            else
            {
                ApplyFileChange(relativePath, fullPath, existsAsFile);
            }

            try { PersistJournal(); }
            catch (Exception ex) { Error?.Invoke("Could not persist the pending-change queue: " + ex.Message); }
        }

        PublishQueue();
    }

    private void ApplyDirectoryChange(string relativePath, string fullPath, bool exists)
    {
        var index = FindPendingIndex(relativePath);
        var previous = index >= 0 ? _pending[index] : null;

        if (exists)
        {
            _knownDirectories.Add(fullPath);
            var kind = previous?.Kind switch
            {
                PendingChangeKind.Created => PendingChangeKind.Created,
                PendingChangeKind.Deleted => PendingChangeKind.Modified,
                PendingChangeKind.Modified => PendingChangeKind.Modified,
                _ => PendingChangeKind.Created
            };
            var change = new PendingFileChange(
                previous?.Id ?? Guid.NewGuid(), relativePath, kind, DateTimeOffset.UtcNow, null, true);
            if (index >= 0) _pending[index] = change;
            else _pending.Add(change);
            return;
        }

        RemoveKnownDirectoryTree(fullPath);
        RemovePendingDescendants(relativePath);
        index = FindPendingIndex(relativePath);
        previous = index >= 0 ? _pending[index] : null;

        if (previous?.Kind == PendingChangeKind.Created)
        {
            _pending.RemoveAt(index);
        }
        else if (previous is not null)
        {
            _pending[index] = previous with
            {
                Kind = PendingChangeKind.Deleted,
                DetectedAt = DateTimeOffset.UtcNow,
                Length = null,
                IsDirectory = true
            };
        }
        else
        {
            _pending.Add(new PendingFileChange(
                Guid.NewGuid(), relativePath, PendingChangeKind.Deleted, DateTimeOffset.UtcNow, null, true));
        }
    }

    private void ApplyFileChange(string relativePath, string fullPath, bool exists)
    {
        var index = FindPendingIndex(relativePath);
        var previous = index >= 0 ? _pending[index] : null;

        if (exists)
        {
            var kind = previous?.Kind switch
            {
                PendingChangeKind.Created => PendingChangeKind.Created,
                PendingChangeKind.Deleted => PendingChangeKind.Modified,
                PendingChangeKind.Modified => PendingChangeKind.Modified,
                _ => PendingChangeKind.Created
            };
            var length = new FileInfo(fullPath).Length;
            var change = new PendingFileChange(
                previous?.Id ?? Guid.NewGuid(), relativePath, kind, DateTimeOffset.UtcNow, length, false);
            if (index >= 0) _pending[index] = change;
            else _pending.Add(change);
        }
        else if (previous?.Kind == PendingChangeKind.Created)
        {
            _pending.RemoveAt(index);
        }
        else if (previous is not null)
        {
            _pending[index] = previous with
            {
                Kind = PendingChangeKind.Deleted,
                DetectedAt = DateTimeOffset.UtcNow,
                Length = null,
                IsDirectory = false
            };
        }
        else
        {
            _pending.Add(new PendingFileChange(
                Guid.NewGuid(), relativePath, PendingChangeKind.Deleted, DateTimeOffset.UtcNow, null, false));
        }
    }

    private int FindPendingIndex(string relativePath) =>
        _pending.FindIndex(change =>
            change.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));

    private bool HasDeletedDirectoryAncestor(string relativePath) =>
        _pending.Any(change =>
            change.IsDirectory &&
            change.Kind == PendingChangeKind.Deleted &&
            IsSameOrDescendant(relativePath, change.RelativePath) &&
            !relativePath.Equals(change.RelativePath, StringComparison.OrdinalIgnoreCase));

    private void RemovePendingDescendants(string relativePath) =>
        _pending.RemoveAll(change =>
            !change.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase) &&
            IsSameOrDescendant(change.RelativePath, relativePath));

    private void RemoveKnownDirectoryTree(string fullPath)
    {
        var normalized = Path.GetFullPath(fullPath);
        _knownDirectories.RemoveWhere(path =>
            path.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(normalized.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSameOrDescendant(string candidate, string parent)
    {
        if (candidate.Equals(parent, StringComparison.OrdinalIgnoreCase)) return true;
        var prefix = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private void PersistJournal()
    {
        var temporaryPath = _journalPath + ".tmp";
        var json = JsonSerializer.Serialize(_pending, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _journalPath, true);
    }

    private void PublishQueue() => QueueChanged?.Invoke(PendingChanges);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        foreach (var cancellation in _debounce.Values)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
        _debounce.Clear();
    }
}
