using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace PcNinja.Win11Upgrade.Core;

public sealed class DownloadTransferException(string message, Exception? inner = null) : IOException(message, inner);

internal sealed record PartialDownload(string Endpoint, string FileName, string ETag, long TotalBytes);

internal static class IsoTransfer
{
    internal static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(90);

    internal static async Task<DownloadResult> DownloadAsync(HttpClient http, Uri endpoint, string directory,
        Action<long?> checkStorage, IProgress<DownloadProgress>? progress, CancellationToken token,
        string? expectedSha256, long minimumBytes = UpgradeEngine.MinimumIsoBytes)
    {
        using var initial = await SendAsync(http, new HttpRequestMessage(HttpMethod.Get, endpoint), token);
        var mediaType = initial.Content.Headers.ContentType?.MediaType ?? "";
        if (mediaType.Contains("text/", StringComparison.OrdinalIgnoreCase))
            throw new DownloadTransferException("The download service returned a web page. Try downloading in your browser.");
        var total = initial.Content.Headers.ContentLength;
        if (total is > 0 && total < minimumBytes)
            throw new DownloadTransferException("The download service did not return a complete Windows ISO.");

        var name = UpgradeEngine.GetRemoteFileName(initial);
        var etag = initial.Headers.ETag;
        PartialDownload? metadata = total is > 0 && etag is { IsWeak: false }
            ? new(endpoint.AbsoluteUri, name, etag.ToString(), total.Value) : null;
        Directory.CreateDirectory(directory);
        string? partial = null;
        if (metadata is not null)
        {
            foreach (var metadataPath in Directory.EnumerateFiles(directory, "*.iso.partial.json"))
            {
                try
                {
                    var candidate = metadataPath[..^5];
                    if (JsonSerializer.Deserialize<PartialDownload>(await File.ReadAllTextAsync(metadataPath, token)) == metadata &&
                        Path.GetFileName(candidate).StartsWith(Path.GetFileNameWithoutExtension(name), StringComparison.OrdinalIgnoreCase) &&
                        File.Exists(candidate) && !File.Exists(candidate[..^8]) &&
                        new FileInfo(candidate).Length is > 0 && new FileInfo(candidate).Length < total)
                    {
                        partial = candidate;
                        break;
                    }
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
                {
                    AppLog.Info($"Ignoring unavailable partial-download metadata: {ex.Message}");
                }
            }
        }

        var destination = partial is null ? UpgradeEngine.GetAvailableDestination(directory, name) : partial[..^8];
        partial ??= destination + ".partial";
        var offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        // Reserve enough free space for the remaining ISO bytes AND Windows workspace.
        checkStorage(total.HasValue ? total.Value - offset : null);
        HttpResponseMessage? resumedResponse = null;
        try
        {
            var response = initial;
            if (offset > 0)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Range = new RangeHeaderValue(offset, null);
                request.Headers.IfRange = new RangeConditionHeaderValue(etag!);
                resumedResponse = await SendAsync(http, request, token);
                var range = resumedResponse.Content.Headers.ContentRange;
                if (resumedResponse.StatusCode != HttpStatusCode.PartialContent || range?.From != offset ||
                    range.Length != total || resumedResponse.Headers.ETag?.ToString() != metadata!.ETag)
                {
                    // Never append bytes from an unverified response to the retained ISO.
                    resumedResponse.Dispose();
                    resumedResponse = null;
                    destination = UpgradeEngine.GetAvailableDestination(directory, name);
                    partial = destination + ".partial";
                    offset = 0;
                    checkStorage(total);
                }
                else response = resumedResponse;
            }

            AppLog.Info($"Download target: {destination}. Partial: {partial}. Resume offset: {offset}.");
            if (metadata is not null)
                await File.WriteAllTextAsync(partial + ".json", JsonSerializer.Serialize(metadata), token);
            var received = offset;
            progress?.Report(new(destination, received, total, total is > 0 ? (int)(received * 100 / total.Value) : null));
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(partial, offset > 0 ? FileMode.Open : FileMode.CreateNew,
                             FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous))
            {
                output.Position = offset;
                var buffer = new byte[1024 * 1024];
                var lastReport = Environment.TickCount64;
                while (true)
                {
                    int count;
                    using (var stall = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        stall.CancelAfter(StallTimeout);
                        try { count = await input.ReadAsync(buffer, stall.Token).ConfigureAwait(false); }
                        catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
                        { throw new DownloadTransferException("The download stopped responding. Try again to resume it.", ex); }
                        catch (IOException ex)
                        { throw new DownloadTransferException("The connection was interrupted. Try again to resume the download.", ex); }
                    }
                    if (count == 0) break;
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    received += count;
                    if (Environment.TickCount64 - lastReport >= 200)
                    {
                        progress?.Report(new(destination, received, total,
                            total is > 0 ? (int)Math.Clamp(received * 100 / total.Value, 0, 100) : null));
                        lastReport = Environment.TickCount64;
                    }
                }
                await output.FlushAsync(token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            if ((total.HasValue && received != total) || received < minimumBytes)
                throw new DownloadTransferException("The ISO download is incomplete. Try again to resume it.");
            progress?.Report(new(destination, received, total, null, "Checking the ISO checksum..."));
            var hash = await UpgradeEngine.VerifyIsoHashAsync(partial, expectedSha256, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(partial, destination);
            try { File.Delete(partial + ".json"); } catch (IOException) { }
            progress?.Report(new(destination, received, total, 100));
            AppLog.Info($"Download complete: {destination}. SHA256: {hash}. Expected hash checked: {expectedSha256 is not null}.");
            return new(destination, received, total, hash, offset > 0);
        }
        catch (OperationCanceledException)
        {
            AppLog.Info($"Download cancelled. Partial retained: {partial}");
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Download stopped. Exact partial path: {partial}. {ex}");
            throw;
        }
        finally { resumedResponse?.Dispose(); }
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient http, HttpRequestMessage request, CancellationToken token)
    {
        using (request)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var code = response.StatusCode;
                    response.Dispose();
                    throw new DownloadTransferException($"Download service returned HTTP {(int)code}. Try again or use your browser.");
                }
                return response;
            }
            catch (HttpRequestException ex)
            { throw new DownloadTransferException("Could not connect to the download service. Try again or use your browser.", ex); }
            catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
            { throw new DownloadTransferException("The download service did not respond in time. Try again or use your browser.", ex); }
        }
    }
}
