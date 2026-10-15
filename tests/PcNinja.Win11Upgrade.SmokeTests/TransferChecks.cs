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
                await IsoTransfer.DownloadAsync(http, endpoint, directory, storageCalls.Add, null,
                    CancellationToken.None, hash, 1);
                throw new Exception("Interrupted transfer unexpectedly completed.");
            }
            catch (DownloadTransferException) { }
            if (!File.Exists(Path.Combine(directory, "Original.iso.partial")))
                throw new Exception("Interrupted transfer did not preserve its exact partial file.");
            var result = await IsoTransfer.DownloadAsync(http, endpoint, directory, storageCalls.Add, null,
                CancellationToken.None, hash, 1);
            if (!result.Resumed || !handler.SawRange || result.Sha256 != hash ||
                !File.ReadAllBytes(result.IsoPath).SequenceEqual(data) || !storageCalls.Contains(4096L))
                throw new Exception("Resumed download failed range, checksum, byte identity or remaining-space checks.");

            handler.Interrupt = false;
            try
            {
                await IsoTransfer.DownloadAsync(http, endpoint, directory, _ => { }, null,
                    CancellationToken.None, new string('0', 64), 1);
                throw new Exception("An incorrect trusted checksum was accepted.");
            }
            catch (InvalidDataException) { }
            if (File.Exists(Path.Combine(directory, "Original (Copy 1).iso")))
                throw new Exception("Checksum failure was promoted to a completed ISO.");
            if (!File.ReadAllBytes(result.IsoPath).SequenceEqual(data))
                throw new Exception("A collision overwrote the previous completed ISO.");

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try
            {
                await IsoTransfer.DownloadAsync(http, endpoint, directory, _ => { }, null,
                    cancelled.Token, hash, 1);
                throw new Exception("Cancelled transfer unexpectedly completed.");
            }
            catch (OperationCanceledException) { }

            var restartDirectory = Path.Combine(directory, "range-ignored");
            using var restartHttp = new HttpClient(new FakeDownloadHandler(data));
            try { await IsoTransfer.DownloadAsync(restartHttp, endpoint, restartDirectory, _ => { }, null, CancellationToken.None, hash, 1); }
            catch (DownloadTransferException) { }
            var restartHandler = new FakeDownloadHandler(data) { Interrupt = false, IgnoreRange = true };
            using var fullHttp = new HttpClient(restartHandler);
            var restarted = await IsoTransfer.DownloadAsync(fullHttp, endpoint, restartDirectory, _ => { }, null, CancellationToken.None, hash, 1);
            if (restarted.Resumed || !restarted.IsoPath.EndsWith("Original (Copy 1).iso") ||
                new FileInfo(Path.Combine(restartDirectory, "Original.iso.partial")).Length != 4096)
                throw new Exception("Ignored Range did not safely restart in a separate copy.");

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
                throw new Exception("Mid-transfer cancellation did not retain written bytes.");
            Console.WriteLine("PASS: Exact ETag/Range resume, ignored-range safe restart, checksum rejection, collision protection and mid-transfer cancellation.");
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
