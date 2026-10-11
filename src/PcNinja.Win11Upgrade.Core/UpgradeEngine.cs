using Microsoft.Win32;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PcNinja.Win11Upgrade.Core;

public sealed record MountedSource(string Root, string SetupPath);
public sealed record UpgradeResult(int ExitCode, string SetupPath);

public sealed class UpgradeEngine
{
    public const string DownloadEndpoint = "https://11oem.pcninja.pro";
    public const string MicrosoftDownloadPage = "https://www.microsoft.com/software-download/windows11";
    private const long MinimumIsoBytes = 3L * 1024 * 1024 * 1024;
    private readonly HttpClient _http;

    public UpgradeEngine()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };
        _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PcNinja-Win11-Upgrade-Assistant/1.0.0-rc.1");
    }

    public string GetOperatingSystemDescription()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var name = key?.GetValue("ProductName")?.ToString() ?? "Windows";
        var display = key?.GetValue("DisplayVersion")?.ToString();
        var build = key?.GetValue("CurrentBuildNumber")?.ToString() ?? Environment.OSVersion.Version.Build.ToString();
        return $"{name}{(string.IsNullOrWhiteSpace(display) ? "" : $" {display}")} (build {build}, x64)";
    }

    public void ValidateSupportedComputer()
    {
        if (!Environment.Is64BitOperatingSystem)
            throw new InvalidOperationException("This release supports x64 computers only.");

        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var installationType = key?.GetValue("InstallationType")?.ToString() ?? "";
        var productName = key?.GetValue("ProductName")?.ToString() ?? "";
        if (installationType.Contains("Server", StringComparison.OrdinalIgnoreCase) ||
            productName.Contains("Server", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Windows Server and LTSC are not supported by this release.");
    }

    public IReadOnlyList<string> FindLocalIsos()
    {
        try
        {
            return Directory.EnumerateFiles(AppContext.BaseDirectory, "*.iso", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .Where(file => file.Length >= MinimumIsoBytes)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => file.FullName)
                .ToArray();
        }
        catch (Exception ex)
        {
            AppLog.Error($"ISO scan failed: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<MountedSource> FindMountedSources() => DriveInfo.GetDrives()
        .Where(drive => drive.IsReady)
        .Select(drive => new MountedSource(drive.RootDirectory.FullName,
            System.IO.Path.Combine(drive.RootDirectory.FullName, "setup.exe")))
        .Where(source => File.Exists(source.SetupPath))
        .ToArray();

    public async Task<string> DownloadIsoAsync(IProgress<int> progress, CancellationToken cancellationToken)
    {
        var destination = GetDownloadDestination();
        var partial = destination + ".partial";
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        if (File.Exists(partial)) File.Delete(partial);

        AppLog.Info("Starting automatic ISO download from the configured PcNinja endpoint.");
        using var response = await _http.GetAsync(DownloadEndpoint,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The download service returned a web page instead of an ISO file.");

        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[1024 * 1024];
        long received = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            if (total > 0) progress.Report((int)Math.Clamp(received * 100L / total.Value, 0, 100));
        }
        await output.FlushAsync(cancellationToken);

        if (received < MinimumIsoBytes)
            throw new InvalidOperationException($"The downloaded file is too small to be a Windows ISO ({FormatBytes(received)}).");

        File.Move(partial, destination, true);
        progress.Report(100);
        AppLog.Info($"ISO download completed: {destination} ({FormatBytes(received)}). ");
        return destination;
    }

    public void ValidateIsoFile(string isoPath)
    {
        if (string.IsNullOrWhiteSpace(isoPath) || !File.Exists(isoPath))
            throw new FileNotFoundException("The selected ISO file was not found.", isoPath);
        if (!string.Equals(System.IO.Path.GetExtension(isoPath), ".iso", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected file is not an ISO image.");
        var size = new FileInfo(isoPath).Length;
        if (size < MinimumIsoBytes)
            throw new InvalidOperationException($"The selected ISO is unexpectedly small ({FormatBytes(size)}).");
    }

    public async Task<MountedSource> MountAndValidateAsync(string isoPath, CancellationToken cancellationToken)
    {
        ValidateIsoFile(isoPath);
        var escaped = isoPath.Replace("'", "''");
        var command = "$p='" + escaped + "';" +
            "$d=Get-DiskImage -ImagePath $p -ErrorAction SilentlyContinue;" +
            "if(-not $d -or -not $d.Attached){Mount-DiskImage -ImagePath $p -ErrorAction Stop|Out-Null};" +
            "$v=Get-DiskImage -ImagePath $p|Get-Volume|Where-Object DriveLetter|Select-Object -First 1;" +
            "if(-not $v){throw 'Windows did not assign a drive letter to the ISO.'};" +
            "[Console]::Out.Write($v.DriveLetter+':\\')";
        var root = (await RunPowerShellAsync(command, cancellationToken)).Trim();
        return ValidateMountedSource(root);
    }

    public MountedSource ValidateMountedSource(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("No mounted installation source was selected.");
        root = System.IO.Path.GetPathRoot(root) ?? root;
        var setup = System.IO.Path.Combine(root, "setup.exe");
        if (!File.Exists(setup))
            throw new InvalidOperationException($"setup.exe was not found in {root}.");
        if (File.Exists(System.IO.Path.Combine(root, "Autounattend.xml")))
            throw new InvalidOperationException("The installation source contains Autounattend.xml and cannot be used safely.");
        if (!AuthenticodeVerifier.IsTrustedMicrosoftFile(setup, out var signer))
            throw new InvalidOperationException("setup.exe does not have a valid Microsoft digital signature.");
        AppLog.Info($"Validated installation source {root}; signer: {signer}.");
        return new MountedSource(root, setup);
    }

    public async Task<UpgradeResult> StartUpgradeAsync(string setupPath, bool allowAutomaticRestart,
        CancellationToken cancellationToken)
    {
        var arguments = "/product server /auto upgrade /eula accept /dynamicupdate disable /compat ignorewarning";
        if (!allowAutomaticRestart) arguments += " /noreboot";

        AppLog.Info($"Starting Windows Setup. Automatic restart: {allowAutomaticRestart}. Command: setup.exe {arguments}");
        var startInfo = new ProcessStartInfo
        {
            FileName = setupPath,
            Arguments = arguments,
            UseShellExecute = true,
            WorkingDirectory = System.IO.Path.GetDirectoryName(setupPath)!
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Windows Setup could not be started.");
        await process.WaitForExitAsync(cancellationToken);
        AppLog.Info($"Windows Setup exit code: {process.ExitCode}.");
        return new UpgradeResult(process.ExitCode, setupPath);
    }

    public static void OpenMicrosoftDownloadPage() => Process.Start(new ProcessStartInfo
    {
        FileName = MicrosoftDownloadPage,
        UseShellExecute = true
    });

    private static string GetDownloadDestination()
    {
        try
        {
            var probe = System.IO.Path.Combine(AppContext.BaseDirectory, $".pcninja-write-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "test");
            File.Delete(probe);
            return System.IO.Path.Combine(AppContext.BaseDirectory, "Windows11.iso");
        }
        catch
        {
            return System.IO.Path.Combine(AppLog.Folder, "Downloads", "Windows11.iso");
        }
    }

    private static async Task<string> RunPowerShellAsync(string command, CancellationToken cancellationToken)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Windows could not mount the ISO.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Windows could not mount the ISO." : error.Trim());
        return output;
    }

    private static string FormatBytes(long bytes) => $"{bytes / 1024d / 1024d / 1024d:N2} GB";
}

public static class AuthenticodeVerifier
{
    private static readonly Guid ActionGenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    public static bool IsTrustedMicrosoftFile(string filePath, out string signer)
    {
        signer = "Unknown";
        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
            signer = certificate.Subject;
            if (!certificate.Subject.Contains("Microsoft Corporation", StringComparison.OrdinalIgnoreCase)) return false;
        }
        catch { return false; }

        var fileInfo = new WinTrustFileInfo
        {
            StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
            FilePath = filePath,
            FileHandle = IntPtr.Zero,
            KnownSubject = IntPtr.Zero
        };
        var fileInfoPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustFileInfo>());
        var dataPointer = IntPtr.Zero;
        var actionPointer = IntPtr.Zero;
        var fileInfoWritten = false;
        try
        {
            Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
            fileInfoWritten = true;
            var data = new WinTrustData
            {
                StructSize = (uint)Marshal.SizeOf<WinTrustData>(),
                PolicyCallbackData = IntPtr.Zero,
                SipClientData = IntPtr.Zero,
                UiChoice = 2,
                RevocationChecks = 0,
                UnionChoice = 1,
                FileInfoPointer = fileInfoPointer,
                StateAction = 0,
                StateData = IntPtr.Zero,
                UrlReference = IntPtr.Zero,
                ProviderFlags = 0,
                UiContext = 0
            };
            dataPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustData>());
            Marshal.StructureToPtr(data, dataPointer, false);
            actionPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<Guid>());
            Marshal.StructureToPtr(ActionGenericVerifyV2, actionPointer, false);
            return WinVerifyTrust(IntPtr.Zero, actionPointer, dataPointer) == 0;
        }
        finally
        {
            if (actionPointer != IntPtr.Zero) Marshal.FreeCoTaskMem(actionPointer);
            if (dataPointer != IntPtr.Zero) Marshal.FreeCoTaskMem(dataPointer);
            if (fileInfoWritten) Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPointer);
            Marshal.FreeCoTaskMem(fileInfoPointer);
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern uint WinVerifyTrust(IntPtr hwnd, IntPtr actionId, IntPtr trustData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint StructSize;
        [MarshalAs(UnmanagedType.LPWStr)] public string FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfoPointer;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
    }
}
