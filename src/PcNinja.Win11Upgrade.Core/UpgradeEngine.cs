using Microsoft.Win32;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PcNinja.Win11Upgrade.Core;

public sealed class UpgradeEngine
{
    public const string DownloadEndpoint = "https://11oem.pcninja.pro";
    public const string MicrosoftDownloadPage = "https://www.microsoft.com/software-download/windows11";
    public const long MinimumIsoBytes = 3L * 1024 * 1024 * 1024;
    public const long MinimumSystemDriveFreeBytes = 20L * 1024 * 1024 * 1024;
    private const long UnknownDownloadReserveBytes = 12L * 1024 * 1024 * 1024;
    private const long DownloadSafetyMarginBytes = 1L * 1024 * 1024 * 1024;
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
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PcNinja-Win11-Upgrade-Assistant/1.1.0-rc.2");
    }

    public string GetOperatingSystemDescription()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var name = key?.GetValue("ProductName")?.ToString() ?? "Windows";
        var display = key?.GetValue("DisplayVersion")?.ToString();
        var editionId = key?.GetValue("EditionID")?.ToString();
        var buildText = key?.GetValue("CurrentBuildNumber")?.ToString();
        var build = int.TryParse(buildText, out var parsedBuild) ? parsedBuild : Environment.OSVersion.Version.Build;
        return FormatOperatingSystemDescription(name, display, build, Environment.Is64BitOperatingSystem, editionId);
    }

    public static string FormatOperatingSystemDescription(string productName, string? displayVersion,
        int build, bool is64Bit, string? editionId = null)
    {
        var family = build >= 22000 ? "Windows 11" : "Windows 10";
        var edition = ExtractEdition(productName);
        if (string.IsNullOrWhiteSpace(edition)) edition = FormatEditionId(editionId);
        var version = string.IsNullOrWhiteSpace(displayVersion) ? "" : $" {displayVersion.Trim()}";
        var architecture = is64Bit ? "x64" : "x86";
        return $"{family}{(string.IsNullOrWhiteSpace(edition) ? "" : $" {edition}")}{version} (build {build}, {architecture})";
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
        if (productName.Contains("LTSC", StringComparison.OrdinalIgnoreCase) ||
            productName.Contains("LTSB", StringComparison.OrdinalIgnoreCase))
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

    public IReadOnlyList<StorageLocation> FindDownloadLocations()
    {
        var locations = new List<StorageLocation>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                locations.Add(new StorageLocation(drive.RootDirectory.FullName, drive.VolumeLabel,
                    drive.DriveType, drive.DriveFormat, drive.AvailableFreeSpace));
            }
            catch (Exception ex)
            {
                AppLog.Error($"Storage scan skipped {drive.Name}: {ex.Message}");
            }
        }

        return locations.OrderBy(location => location.DriveType == DriveType.Fixed ? 0 : 1)
            .ThenBy(location => location.RootPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public StorageCheck CheckSystemDriveSpace()
    {
        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))
            ?? throw new InvalidOperationException("The Windows system drive could not be identified.");
        var drive = new DriveInfo(systemRoot);
        var valid = drive.IsReady && drive.AvailableFreeSpace >= MinimumSystemDriveFreeBytes;
        var message = valid
            ? $"{FormatBytes(drive.AvailableFreeSpace)} free on the Windows drive."
            : $"At least {FormatBytes(MinimumSystemDriveFreeBytes)} must be free on the Windows drive. " +
              $"Only {FormatBytes(drive.AvailableFreeSpace)} is currently available.";
        return new StorageCheck(valid, drive.RootDirectory.FullName, drive.DriveFormat,
            drive.AvailableFreeSpace, MinimumSystemDriveFreeBytes, message);
    }

    public StorageCheck CheckDownloadDestination(string destinationDirectory, long? expectedBytes = null)
    {
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new InvalidOperationException("Choose a download destination.");

        var fullDirectory = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(fullDirectory);
        var root = Path.GetPathRoot(fullDirectory)
            ?? throw new InvalidOperationException("The selected download drive could not be identified.");
        var drive = new DriveInfo(root);
        if (!drive.IsReady) throw new InvalidOperationException($"Drive {root} is not ready.");
        if (drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("FAT32 cannot store this Windows ISO. Choose an NTFS or exFAT drive.");

        var required = checked((expectedBytes ?? UnknownDownloadReserveBytes) + DownloadSafetyMarginBytes);
        var valid = drive.AvailableFreeSpace >= required;
        var message = valid
            ? $"{FormatBytes(drive.AvailableFreeSpace)} free on {root}."
            : $"The selected drive needs at least {FormatBytes(required)} free. " +
              $"Only {FormatBytes(drive.AvailableFreeSpace)} is available.";
        if (valid)
        {
            var probe = Path.Combine(fullDirectory, $".pcninja-write-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(probe, "write test");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"The selected download folder is not writable: {fullDirectory}", ex);
            }
            finally
            {
                try { if (File.Exists(probe)) File.Delete(probe); } catch { }
            }
        }
        return new StorageCheck(valid, root, drive.DriveFormat, drive.AvailableFreeSpace, required, message);
    }

    public async Task<DownloadResult> DownloadIsoAsync(string destinationDirectory,
        IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        AppLog.Info("Starting automatic ISO download from the configured PcNinja endpoint.");
        using var response = await _http.GetAsync(DownloadEndpoint,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The download service returned a web page instead of an ISO file.");

        var total = response.Content.Headers.ContentLength;
        if (total is > 0 and < MinimumIsoBytes)
            throw new InvalidOperationException($"The server reported an unexpectedly small file ({FormatBytes(total.Value)}). ");

        var storage = CheckDownloadDestination(destinationDirectory, total);
        if (!storage.IsValid) throw new InvalidOperationException(storage.Message);

        var remoteFileName = GetRemoteFileName(response);
        var destination = GetAvailableDestination(Path.GetFullPath(destinationDirectory), remoteFileName);
        var partial = destination + ".partial";
        AppLog.Info($"Download target selected: {destination}. Partial file: {partial}.");
        progress?.Report(new DownloadProgress(destination, 0, total, total.HasValue ? 0 : null));

        long received = 0;
        try
        {
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[1024 * 1024];
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    received += read;
                    int? percentage = total is > 0
                        ? (int)Math.Clamp(received * 100L / total.Value, 0, 100)
                        : null;
                    progress?.Report(new DownloadProgress(destination, received, total, percentage));
                }
                await output.FlushAsync(cancellationToken);
            }

            if (total is > 0 && received != total.Value)
                throw new InvalidOperationException(
                    $"The ISO download is incomplete. Received {FormatBytes(received)} of {FormatBytes(total.Value)}.");
            if (received < MinimumIsoBytes)
                throw new InvalidOperationException($"The downloaded file is too small to be a Windows ISO ({FormatBytes(received)}). ");

            File.Move(partial, destination);
            ValidateIsoFile(destination);
            progress?.Report(new DownloadProgress(destination, received, total, 100));
            AppLog.Info($"ISO download completed: {destination} ({FormatBytes(received)}). ");
            return new DownloadResult(destination, received, total);
        }
        catch
        {
            AppLog.Error($"ISO download failed after {FormatBytes(received)}. Partial file retained at {partial}.");
            throw;
        }
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
        return await ValidateMountedSourceAsync(root, cancellationToken);
    }

    public MountedSource ValidateMountedSource(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("No mounted installation source was selected.");
        root = System.IO.Path.GetPathRoot(root) ?? root;
        if (root.EndsWith(':')) root += Path.DirectorySeparatorChar;
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

    public async Task<MountedSource> ValidateMountedSourceAsync(string root, CancellationToken cancellationToken)
    {
        var source = ValidateMountedSource(root);
        var sourcesDirectory = Path.Combine(source.Root, "sources");
        var installImage = new[] { "install.wim", "install.esd", "install.swm" }
            .Select(name => Path.Combine(sourcesDirectory, name))
            .FirstOrDefault(File.Exists);
        if (installImage is null)
            throw new InvalidOperationException("The mounted ISO does not contain sources\\install.wim or install.esd.");

        AppLog.Info($"Validating Windows image metadata with DISM: {installImage}.");
        var dism = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
        var result = await RunProcessAsync(dism, $"/English /Get-WimInfo /WimFile:\"{installImage}\"",
            cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Windows could not validate the installation image (DISM exit code {result.ExitCode}). " +
                "The ISO may be incomplete or damaged.");
        AppLog.Info("DISM successfully read the Windows installation image metadata.");
        return source;
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

    public static void OpenPcNinjaDownloadPage() => Process.Start(new ProcessStartInfo
    {
        FileName = DownloadEndpoint,
        UseShellExecute = true
    });

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

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(
        string fileName, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {Path.GetFileName(fileName)}.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static string GetRemoteFileName(HttpResponseMessage response)
    {
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName;
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            fileName = fileName.Trim().Trim('"');
            var encodingSeparator = fileName.IndexOf("''", StringComparison.Ordinal);
            if (encodingSeparator >= 0) fileName = fileName[(encodingSeparator + 2)..];
            fileName = Uri.UnescapeDataString(fileName);
        }
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = Uri.UnescapeDataString(Path.GetFileName(response.RequestMessage?.RequestUri?.LocalPath) ?? "");
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Equals("download", StringComparison.OrdinalIgnoreCase))
            fileName = "Windows11.iso";

        fileName = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(invalid, '_');
        if (!fileName.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) fileName += ".iso";
        return fileName;
    }

    public static string GetAvailableDestination(string directory, string originalFileName)
    {
        Directory.CreateDirectory(directory);
        var stem = Path.GetFileNameWithoutExtension(originalFileName);
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".iso";
        for (var copy = 0; copy < 10_000; copy++)
        {
            var name = copy == 0 ? $"{stem}{extension}" : $"{stem} (Copy {copy}){extension}";
            var path = Path.Combine(directory, name);
            if (!File.Exists(path) && !File.Exists(path + ".partial")) return path;
        }

        throw new IOException("No available ISO filename could be created in the selected folder.");
    }

    private static string ExtractEdition(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName)) return "";
        var value = productName.Trim();
        if (value.StartsWith("Microsoft ", StringComparison.OrdinalIgnoreCase)) value = value[10..].Trim();
        foreach (var prefix in new[] { "Windows 11", "Windows 10", "Windows" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return value[prefix.Length..].Trim();
        }
        return value;
    }

    private static string FormatEditionId(string? editionId) => editionId?.ToLowerInvariant() switch
    {
        "professional" => "Pro",
        "professionaln" => "Pro N",
        "core" => "Home",
        "coren" => "Home N",
        "coresinglelanguage" => "Home Single Language",
        "education" => "Education",
        "educationn" => "Education N",
        "professionalworkstation" => "Pro for Workstations",
        "professionalworkstationn" => "Pro N for Workstations",
        _ => editionId ?? ""
    };

    public static string FormatBytes(long bytes) => $"{bytes / 1024d / 1024d / 1024d:N2} GB";
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
