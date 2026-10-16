using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using PcNinja.Win11Upgrade.Core;

internal static class TransferChecks
{
    public static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PcNinja-Transfer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var data = new byte[8192];
            Random.Shared.NextBytes(data);
            var hash = Convert.ToHexString(SHA256.HashData(data));
            var handler = new FakeDownloadHandler(data);
            using var http = new HttpClient(handler);
            var endpoint = new Uri("https://download.invalid/iso");
            var storageCalls = new List<long?>();
            try
            {
                await IsoTransfer.DownloadAsync(http, endpoint, directory, storageCalls.Add, null, CancellationToken.None, hash, 1);
                throw new Exception("Interrupted transfer unexpectedly completed.");
            }
            catch (DownloadTransferException) { }
            var exact = Path.Combine(directory, "Original.iso");
            if (!File.Exists(exact + ".partial")) throw new Exception("Interrupted partial missing.");
            var result = await IsoTransfer.DownloadAsync(http, endpoint, directory, storageCalls.Add, null, CancellationToken.None, hash, 1);
            if (!result.Resumed || !handler.SawRange || result.Sha256 != hash ||
                !File.ReadAllBytes(exact).SequenceEqual(data) || !storageCalls.Contains(4096L))
                throw new Exception("Exact resume failed byte identity, checksum or remaining-space checks.");
            handler.Interrupt = false;
            var validations = 0;
            var reused = await IsoTransfer.DownloadAsync(http, endpoint, directory, _ => throw new Exception("Reuse reserved download space."),
                null, CancellationToken.None, hash, 1, (_, _) => { validations++; return Task.CompletedTask; });
            if (!reused.Reused || validations != 1 || reused.IsoPath != exact)
                throw new Exception("The completed exact file was not reused and revalidated.");
            using var offlineHttp = new HttpClient(new OfflineHandler());
            var offlineReuse = await IsoTransfer.DownloadAsync(offlineHttp, endpoint, directory,
                _ => throw new Exception("Offline reuse reserved download space."), null, CancellationToken.None, hash, 1);
            if (!offlineReuse.Reused || offlineReuse.IsoPath != exact)
                throw new Exception("Recorded exact ISO could not be reused while the provider was unavailable.");
            var damaged = data.ToArray();
            damaged[0] ^= 0xFF;
            File.WriteAllBytes(exact, damaged);
            var repaired = await IsoTransfer.DownloadAsync(http, endpoint, directory, _ => { }, null, CancellationToken.None, hash, 1);
            if (repaired.Reused || repaired.IsoPath != exact || !File.ReadAllBytes(exact).SequenceEqual(data))
                throw new Exception("A proven-corrupt owned download was not replaced at the original name.");

            try
            {
                await IsoTransfer.DownloadAsync(http, endpoint, directory, _ => { }, null, CancellationToken.None, hash, 1,
                    (_, _) => throw new UnsafeInstallationMediaException("interfering secondary media"));
                throw new Exception("Unsafe media validation unexpectedly passed.");
            }
            catch (UnsafeInstallationMediaException) { }
            if (!File.ReadAllBytes(exact).SequenceEqual(data)) throw new Exception("Environmental media failure deleted a valid ISO.");
            try
            {
                await IsoTransfer.DownloadAsync(http, endpoint, directory, _ => { }, null, CancellationToken.None, hash, 1,
                    (_, _) => throw new IOException("simulated unavailable drive"));
                throw new Exception("I/O validation unexpectedly passed.");
            }
            catch (IOException) { }
            if (!File.Exists(exact)) throw new Exception("An I/O error deleted a completed ISO.");

            var unownedDirectory = Path.Combine(directory, "unowned");
            Directory.CreateDirectory(unownedDirectory);
            var unowned = Path.Combine(unownedDirectory, "Original.iso");
            File.WriteAllBytes(unowned, damaged);
            try
            {
                await IsoTransfer.DownloadAsync(http, endpoint, unownedDirectory, _ => { }, null, CancellationToken.None, hash, 1);
                throw new Exception("An unowned corrupt file was overwritten.");
            }
            catch (InvalidOperationException) { }
            if (!File.ReadAllBytes(unowned).SequenceEqual(damaged)) throw new Exception("An unowned ISO was deleted.");

            var wrongHashDirectory = Path.Combine(directory, "wrong-hash");
            try
            {
                await IsoTransfer.DownloadAsync(http, endpoint, wrongHashDirectory, _ => { }, null, CancellationToken.None, new string('0', 64), 1);
                throw new Exception("An incorrect trusted checksum was accepted.");
            }
            catch (CorruptDownloadException) { }
            if (File.Exists(Path.Combine(wrongHashDirectory, "Original.iso")) ||
                File.Exists(Path.Combine(wrongHashDirectory, "Original.iso.partial")))
                throw new Exception("Corrupt new bytes were kept/promoted.");

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try
            {
                await IsoTransfer.DownloadAsync(http, endpoint, directory, _ => { }, null, cancelled.Token, hash, 1);
                throw new Exception("Cancellation was ignored.");
            }
            catch (OperationCanceledException) { }
            if (!File.Exists(exact)) throw new Exception("Cancellation removed a valid ISO.");

            var restartDirectory = Path.Combine(directory, "range-ignored");
            using var restartHttp = new HttpClient(new FakeDownloadHandler(data));
            try { await IsoTransfer.DownloadAsync(restartHttp, endpoint, restartDirectory, _ => { }, null, CancellationToken.None, hash, 1); }
            catch (DownloadTransferException) { }
            using var fullHttp = new HttpClient(new FakeDownloadHandler(data) { Interrupt = false, IgnoreRange = true });
            var restarted = await IsoTransfer.DownloadAsync(fullHttp, endpoint, restartDirectory, _ => { }, null, CancellationToken.None, hash, 1);
            if (restarted.Resumed || restarted.IsoPath != Path.Combine(restartDirectory, "Original.iso") ||
                !File.ReadAllBytes(restarted.IsoPath).SequenceEqual(data) || File.Exists(restarted.IsoPath + ".partial"))
                throw new Exception("Ignored Range did not safely restart at the original name.");

            using var midCancel = new CancellationTokenSource();
            using var cancelHttp = new HttpClient(new FakeDownloadHandler(data) { CancelOnSecondRead = midCancel });
            var cancelDirectory = Path.Combine(directory, "cancel-active");
            try
            {
                await IsoTransfer.DownloadAsync(cancelHttp, endpoint, cancelDirectory, _ => { }, null, midCancel.Token, hash, 1);
                throw new Exception("Mid-transfer cancellation was ignored.");
            }
            catch (OperationCanceledException) { }
            if (new FileInfo(Path.Combine(cancelDirectory, "Original.iso.partial")).Length != 4096)
                throw new Exception("Cancelled transfer lost its written bytes.");
            File.WriteAllBytes(Path.Combine(cancelDirectory, "Original.iso.partial"), []);
            var zeroRetry = await IsoTransfer.DownloadAsync(http, endpoint, cancelDirectory, _ => { }, null, CancellationToken.None, hash, 1);
            if (!File.ReadAllBytes(zeroRetry.IsoPath).SequenceEqual(data)) throw new Exception("Zero-byte owned partial could not restart.");

            var routes = new List<Uri>();
            await UpgradeEngine.DownloadWithTransportFallbackAsync(uri =>
            {
                routes.Add(uri);
                return routes.Count == 1
                    ? Task.FromException<DownloadResult>(new DownloadTransferException("TLS failure", new HttpRequestException("ProtocolVersion")))
                    : Task.FromResult(result);
            });
            if (routes.Count != 2 || routes[0].Host == routes[1].Host || routes[1].Scheme != "https")
                throw new Exception("Secure direct-provider fallback was not attempted.");
            if (Directory.EnumerateFiles(directory, "*Copy*", SearchOption.AllDirectories).Any())
                throw new Exception("Obsolete numbered-copy logic remains.");
            Console.WriteLine("PASS: Original-name resume/restart, reuse/media revalidation, owned corruption replacement, unowned/environmental/cancel preservation, secure transport fallback; no Copy N allocation.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class FakeDownloadHandler(byte[] data) : HttpMessageHandler
    {
        public bool Interrupt { get; set; } = true;
        public bool IgnoreRange { get; set; }
        public CancellationTokenSource? CancelOnSecondRead { get; set; }
        public bool SawRange { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var offset = request.Headers.Range?.Ranges.First().From;
            SawRange |= offset.HasValue;
            if (IgnoreRange) offset = null;
            HttpContent content = offset.HasValue
                ? new ByteArrayContent(data[(int)offset.Value..])
                : Interrupt ? new StreamContent(new InterruptedStream(data, CancelOnSecondRead)) : new ByteArrayContent(data);
            content.Headers.ContentLength = data.Length - (offset ?? 0);
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "Original.iso" };
            if (offset.HasValue)
                content.Headers.ContentRange = new ContentRangeHeaderValue(offset.Value, data.Length - 1, data.Length);
            var response = new HttpResponseMessage(offset.HasValue ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
            {
                Content = content, RequestMessage = request
            };
            response.Headers.ETag = new EntityTagHeaderValue("\"test-v1\"");
            return Task.FromResult(response);
        }
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("A valid recorded cache must not contact the unavailable provider.");
    }

    private sealed class InterruptedStream(byte[] data, CancellationTokenSource? cancellation = null) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position >= 4096)
            {
                cancellation?.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                throw new IOException("Simulated connection drop.");
            }
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, 4096)], cancellationToken);
        }
    }
}
