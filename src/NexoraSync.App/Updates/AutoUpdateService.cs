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
        string stage = "consultar la última versión";
        string? pathInUse = LatestReleaseApi;
        string? temporaryPath = null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.UserAgent.ParseAdd("NexoraSync-AutoUpdater");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new(false, null, null, "No published release is available.");
            response.EnsureSuccessStatusCode();

            stage = "leer los datos de la versión publicada";
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

            stage = "crear la carpeta de actualizaciones";
            Directory.CreateDirectory(updateDirectory);

            // Every attempt gets a fresh destination. Never reuse or overwrite an installer
            // that may still be open by the app, Explorer, Defender, or another process.
            var uniqueId = Guid.NewGuid().ToString("N");
            var finalPath = Path.Combine(updateDirectory, $"NexoraSync-Setup-{tag}-{uniqueId}.exe");
            temporaryPath = finalPath + ".download";
            pathInUse = temporaryPath;

            stage = "descargar el instalador";
            using (var downloadRequest = new HttpRequestMessage(HttpMethod.Get, url))
            {
                downloadRequest.Headers.UserAgent.ParseAdd("NexoraSync-AutoUpdater");
                using var downloadResponse = await Client.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                downloadResponse.EnsureSuccessStatusCode();
                await using var input = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await input.CopyToAsync(output, 81920, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            stage = "verificar la integridad SHA-256 del instalador";
            pathInUse = temporaryPath;
            await using (var downloaded = new FileStream(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            {
                var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(downloaded, cancellationToken)).ToLowerInvariant();
                if (!actualHash.Equals(digest["sha256:".Length..], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded installer failed its SHA-256 integrity check.");
            }

            stage = "preparar el instalador descargado";
            pathInUse = finalPath;
            Exception? lastMoveError = null;
            for (var attempt = 0; attempt < 6; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Move(temporaryPath, finalPath);
                    temporaryPath = null;
                    return new(true, tag, finalPath, "Update downloaded and verified.");
                }
                catch (IOException ex) when (attempt < 5)
                {
                    lastMoveError = ex;
                    await Task.Delay(TimeSpan.FromMilliseconds(400 * (attempt + 1)), cancellationToken);
                }
            }

            throw new IOException("Windows kept the downloaded installer file locked after several attempts.", lastMoveError);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            throw new IOException($"No se pudo completar la actualización durante la etapa: {stage}. Archivo: {pathInUse ?? "(sin ruta)"}. Detalle: {ex.Message}", ex);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryPath))
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
            }
        }
    }
}
