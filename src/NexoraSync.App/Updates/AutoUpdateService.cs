using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace NexoraSync.Updates;

internal sealed record AutoUpdateResult(bool UpdateAvailable, string? Version, string? InstallerPath, string Message);

internal static class AutoUpdateService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(20) };
    private const string LatestReleaseApi = "https://api.github.com/repos/zzzlegna-art/NexoraSync/releases/latest";

    public static async Task<AutoUpdateResult> CheckAndStageAsync(string currentVersion, string updateDirectory, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.UserAgent.ParseAdd("NexoraSync-AutoUpdater");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new(false, null, null, "No published release is available.");
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v');
        if (!Version.TryParse(tag, out var latest) || !Version.TryParse(currentVersion, out var current))
            throw new InvalidDataException("The release version is invalid.");
        if (latest <= current)
            return new(false, tag, null, "Already up to date.");

        if (!root.TryGetProperty("assets", out var assets))
            throw new InvalidDataException("The release has no installer assets.");
        var expectedName = $"NexoraSync-Setup-{tag}.exe";
        var asset = assets.EnumerateArray().FirstOrDefault(item =>
            item.TryGetProperty("name", out var name) &&
            name.GetString()?.Equals(expectedName, StringComparison.OrdinalIgnoreCase) == true);
        if (asset.ValueKind == JsonValueKind.Undefined)
            throw new InvalidDataException("The installer is not attached to the release yet.");

        var url = asset.GetProperty("browser_download_url").GetString();
        var digest = asset.TryGetProperty("digest", out var digestElement) ? digestElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(digest) ||
            !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The release does not provide a SHA-256 digest. The update was not downloaded.");

        Directory.CreateDirectory(updateDirectory);
        var finalPath = Path.Combine(updateDirectory, expectedName);
        if (File.Exists(finalPath))
        {
            try
            {
                await using var existing = new FileStream(finalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var existingHash = Convert.ToHexString(await SHA256.HashDataAsync(existing, cancellationToken)).ToLowerInvariant();
                if (existingHash.Equals(digest["sha256:".Length..], StringComparison.OrdinalIgnoreCase))
                    return new(true, tag, finalPath, "Update already downloaded and verified.");

                // A stale installer may be locked by Windows or antivirus software.
                // Never let that prevent downloading a fresh, uniquely named copy.
                try { File.Delete(finalPath); }
                catch (IOException) { finalPath = Path.Combine(updateDirectory, $"NexoraSync-Setup-{tag}-{Guid.NewGuid():N}.exe"); }
                catch (UnauthorizedAccessException) { finalPath = Path.Combine(updateDirectory, $"NexoraSync-Setup-{tag}-{Guid.NewGuid():N}.exe"); }
            }
            catch (IOException)
            {
                finalPath = Path.Combine(updateDirectory, $"NexoraSync-Setup-{tag}-{Guid.NewGuid():N}.exe");
            }
            catch (UnauthorizedAccessException)
            {
                finalPath = Path.Combine(updateDirectory, $"NexoraSync-Setup-{tag}-{Guid.NewGuid():N}.exe");
            }
        }

        var temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, url);
            downloadRequest.Headers.UserAgent.ParseAdd("NexoraSync-AutoUpdater");
            using var downloadResponse = await Client.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            downloadResponse.EnsureSuccessStatusCode();
            await using (var input = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await input.CopyToAsync(output, 81920, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            await using var downloaded = File.OpenRead(temporaryPath);
            var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(downloaded, cancellationToken)).ToLowerInvariant();
            if (!actualHash.Equals(digest["sha256:".Length..], StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded installer failed its SHA-256 integrity check.");

            File.Move(temporaryPath, finalPath, overwrite: true);
            return new(true, tag, finalPath, "Update downloaded and verified.");
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }
    }
}
