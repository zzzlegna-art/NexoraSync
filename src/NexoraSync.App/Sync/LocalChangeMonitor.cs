using System.Collections.Concurrent;
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
    long? Length);

/// <summary>
/// Observes changes in one local shared folder and persists a durable queue.
/// It deliberately does not claim to transfer or synchronize files remotely.
/// </summary>
public sealed class LocalChangeMonitor : IDisposable
{
    private readonly string _rootPath;
    private readonly string _journalPath;
    private readonly FileSystemWatcher _watcher;
    private readonly object _sync = new();
    private readonly List<PendingFileChange> _pending;
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

        _watcher = new FileSystemWatcher(_rootPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 64 * 1024,
            EnableRaisingEvents = false
        };
        _watcher.Created += (_, e) => Schedule(e.FullPath);
        _watcher.Changed += (_, e) => Schedule(e.FullPath);
        _watcher.Deleted += (_, e) => Schedule(e.FullPath);
        _watcher.Renamed += (_, e) =>
        {
            Schedule(e.OldFullPath);
            Schedule(e.FullPath);
        };
        _watcher.Error += (_, e) => Error?.Invoke(
            "Windows reported a file-watcher overflow. A full folder rescan will be required before remote sync.");
        _watcher.EnableRaisingEvents = true;
    }

    public IReadOnlyList<PendingFileChange> PendingChanges
    {
        get
        {
            lock (_sync) return _pending.ToArray();
        }
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

    private void Schedule(string fullPath)
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
        _ = ProcessAfterQuietPeriodAsync(relativePath, active);
    }

    private async Task ProcessAfterQuietPeriodAsync(string relativePath, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(700, cancellation.Token);
            if (!_debounce.TryGetValue(relativePath, out var current) || !ReferenceEquals(current, cancellation))
                return;

            _debounce.TryRemove(relativePath, out _);
            ApplyChange(relativePath);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (cancellation.IsCancellationRequested || !_debounce.ContainsKey(relativePath))
                cancellation.Dispose();
        }
    }

    private void ApplyChange(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var existsAsFile = File.Exists(fullPath);
        var existsAsDirectory = Directory.Exists(fullPath);
        if (existsAsDirectory) return;

        lock (_sync)
        {
            var existingIndex = _pending.FindIndex(change =>
                change.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
            var previous = existingIndex >= 0 ? _pending[existingIndex] : null;

            if (!existsAsFile)
            {
                if (previous?.Kind == PendingChangeKind.Created)
                    _pending.RemoveAt(existingIndex);
                else if (previous is not null)
                    _pending[existingIndex] = previous with
                    {
                        Kind = PendingChangeKind.Deleted,
                        DetectedAt = DateTimeOffset.UtcNow,
                        Length = null
                    };
                else
                    _pending.Add(new PendingFileChange(Guid.NewGuid(), relativePath,
                        PendingChangeKind.Deleted, DateTimeOffset.UtcNow, null));
            }
            else
            {
                var kind = previous?.Kind is PendingChangeKind.Created or PendingChangeKind.Deleted
                    ? PendingChangeKind.Created
                    : PendingChangeKind.Modified;
                var length = new FileInfo(fullPath).Length;
                if (previous is not null)
                    _pending[existingIndex] = previous with
                    {
                        Kind = kind,
                        DetectedAt = DateTimeOffset.UtcNow,
                        Length = length
                    };
                else
                    _pending.Add(new PendingFileChange(Guid.NewGuid(), relativePath,
                        kind, DateTimeOffset.UtcNow, length));
            }

            try { PersistJournal(); }
            catch (Exception ex) { Error?.Invoke("Could not persist the pending-change queue: " + ex.Message); }
        }

        PublishQueue();
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
