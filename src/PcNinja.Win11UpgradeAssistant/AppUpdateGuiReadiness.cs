using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using PcNinja.Win11Upgrade.Core;

namespace PcNinja.Win11UpgradeAssistant;

internal static class AppUpdateGuiReadiness
{
    internal const string EnvironmentName = "PCNINJA_APP_UPDATE_READY_PIPE";
    internal const string Prefix = "PcNinja.AppReady.";
    private const string Ready = "PCNINJA_GUI_READY_V1";

    internal static bool IsValidName(string? name) => name is not null &&
        name.StartsWith(Prefix, StringComparison.Ordinal) &&
        name.Length == Prefix.Length + 32 && name[Prefix.Length..].All(char.IsAsciiHexDigit);

    internal static NamedPipeServerStream CreateServer(string name)
    {
        if (!IsValidName(name)) throw new InvalidDataException("Invalid readiness pipe name.");
        return new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }

    internal static bool LaunchAndWait(string target)
    {
        var name = Prefix + Guid.NewGuid().ToString("N");
        using var server = CreateServer(name);
        var start = new ProcessStartInfo
        {
            FileName = target, WorkingDirectory = Path.GetDirectoryName(target)!, UseShellExecute = false
            // No arguments. Environment conveys only an opaque ACK channel, never file paths or commands.
        };
        start.Environment[EnvironmentName] = name;
        using var launched = Process.Start(start) ?? throw new IOException("The updated app could not be reopened.");
        _ = launched.SafeHandle; // Retain this exact process, not a reusable PID.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return WaitAsync(server, launched.Id, timeout.Token).GetAwaiter().GetResult() && !launched.HasExited;
    }

    internal static async Task<bool> WaitAsync(NamedPipeServerStream server, int expectedProcessId, CancellationToken token)
    {
        try
        {
            await server.WaitForConnectionAsync(token).ConfigureAwait(false);
            if (!GetNamedPipeClientProcessId(server.SafePipeHandle, out var actual) || actual != (uint)expectedProcessId)
                return false;
            using var reader = new StreamReader(server, Encoding.UTF8, false, 128, leaveOpen: true);
            if (await AppUpdateInstaller.ReadHandshakeAsync(reader, token).ConfigureAwait(false) != Ready) return false;
            using var writer = new StreamWriter(server, new UTF8Encoding(false), 128, leaveOpen: true);
            await writer.WriteLineAsync("ACK".AsMemory(), token).ConfigureAwait(false);
            await writer.FlushAsync(token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException) { return false; }
    }

    internal static async Task SignalAsync(string? name)
    {
        if (!IsValidName(name)) return;
        try
        {
            using var client = new NamedPipeClientStream(".", name!, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
            using var writer = new StreamWriter(client, new UTF8Encoding(false), 128, leaveOpen: true);
            await writer.WriteLineAsync(Ready.AsMemory(), timeout.Token).ConfigureAwait(false);
            await writer.FlushAsync(timeout.Token).ConfigureAwait(false);
            // Keep the client alive until the helper has authenticated its PID.
            // Closing immediately after Flush can race native pipe-client identity.
            using var reader = new StreamReader(client, Encoding.UTF8, false, 128, leaveOpen: true);
            if (await AppUpdateInstaller.ReadHandshakeAsync(reader, timeout.Token).ConfigureAwait(false) != "ACK")
                AppLog.Info("GUI readiness was not acknowledged by the helper; the previous filename may be retained.");
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException)
        { AppLog.Info("GUI readiness acknowledgement unavailable; the previous filename will be retained."); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);
}
