using Microsoft.Win32;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Security.Cryptography;

namespace PcNinja.Win11Upgrade.Core;

public sealed class UpgradeEngine
{
    public const string DownloadEndpoint = "https://11oem.pcninja.pro";
    public const string PcNinjaBrowserFallbackPage = "https://iso.pcninja.pro";
    private const string AutomaticDownloadProviderPage =
        "https://technionmail-my.sharepoint.com/:u:/g/personal/techsup_technion_ac_il/IQB4hCE2DRrGRqQuQYDafLdfAYjrr8R8MAqD4efXPsI-epA?e=aqZwxY";
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
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };
        _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PcNinja-Win11-Upgrade-Assistant/1.1.0-rc.5");
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
        if (productName.Contains("Server", StringComparison.OrdinalIgnoreCase))
        {
            var serverName = productName.StartsWith("Microsoft ", StringComparison.OrdinalIgnoreCase)
                ? productName[10..].Trim()
                : productName.Trim();
            var serverVersion = string.IsNullOrWhiteSpace(displayVersion) ? "" : $" {displayVersion.Trim()}";
            return $"{serverName}{serverVersion} (build {build}, {(is64Bit ? "x64" : "x86")})";
        }
        var family = build >= 22000 ? "Windows 11" : "Windows 10";
        var edition = ExtractEdition(productName);
        if (string.IsNullOrWhiteSpace(edition)) edition = FormatEditionId(editionId);
        var version = string.IsNullOrWhiteSpace(displayVersion) ? "" : $" {displayVersion.Trim()}";
        var architecture = is64Bit ? "x64" : "x86";
        return $"{family}{(string.IsNullOrWhiteSpace(edition) ? "" : $" {edition}")}{version} (build {build}, {architecture})";
    }

    public void ValidateSupportedComputer()
    {
        if (RuntimeInformation.OSArchitecture != Architecture.X64)
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
                .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .Select(file => file.FullName)
                .ToArray();
        }
        catch (Exception ex)
        {
            AppLog.Error($"ISO scan failed: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<MountedSource> FindMountedSources()
    {
        var sources = new List<MountedSource>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                var setup = Path.Combine(drive.RootDirectory.FullName, "setup.exe");
                if (File.Exists(setup)) sources.Add(new(drive.RootDirectory.FullName, setup));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { AppLog.Info($"Source scan skipped {drive.Name}: {ex.Message}"); }
        }
        return sources;
    }

    public IReadOnlyList<StorageLocation> FindDownloadLocations()
    {
        var locations = new List<StorageLocation>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                if (!IsEligibleDownloadLocation(drive.DriveFormat, drive.AvailableFreeSpace)) continue;
                if (DownloadVolume.IsReserved(drive.RootDirectory.FullName)) continue;
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
        var message = SystemDriveSpaceMessage(systemRoot, drive.AvailableFreeSpace);
        return new StorageCheck(valid, drive.RootDirectory.FullName, drive.DriveFormat,
            drive.AvailableFreeSpace, MinimumSystemDriveFreeBytes, message);
    }

    internal static string SystemDriveSpaceMessage(string root, long availableBytes) =>
        availableBytes >= MinimumSystemDriveFreeBytes
            ? $"{FormatBytes(availableBytes)} free on the Windows drive ({root})."
            : $"Not enough space on the Windows drive ({root}). " +
              $"Available: {FormatBytes(availableBytes)}. Required: {FormatBytes(MinimumSystemDriveFreeBytes)}. " +
              $"Free another {FormatBytes(MinimumSystemDriveFreeBytes - availableBytes)}, then Refresh. " +
              "Moving the ISO to another drive does not free Windows upgrade workspace.";

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
        if (DownloadVolume.IsReserved(root))
            throw new InvalidOperationException("Choose a data drive instead of a hidden, recovery or system partition.");
        if (!IsSupportedDownloadFileSystem(drive.DriveFormat))
            throw new InvalidOperationException("Choose an NTFS or exFAT drive for the ISO download.");

        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;
        var required = RequiredDownloadBytes(expectedBytes, string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase));
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

    public static bool IsSupportedDownloadFileSystem(string format) =>
        format.Equals("NTFS", StringComparison.OrdinalIgnoreCase) || format.Equals("exFAT", StringComparison.OrdinalIgnoreCase);

    public static bool IsEligibleDownloadLocation(string format, long availableBytes) =>
        IsSupportedDownloadFileSystem(format) && availableBytes >= MinimumIsoBytes + DownloadSafetyMarginBytes;

    public static long RequiredDownloadBytes(long? remainingBytes, bool windowsDrive) =>
        checked((remainingBytes ?? UnknownDownloadReserveBytes) + DownloadSafetyMarginBytes +
            (windowsDrive ? MinimumSystemDriveFreeBytes : 0));

    public static string? ConfiguredIsoSha256 => Environment.GetEnvironmentVariable("PCNINJA_ISO_SHA256") is { Length: > 0 } hash
        ? NormalizeSha256(hash) : null;

    public static string NormalizeSha256(string hash)
    {
        hash = hash.Trim();
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
            throw new ArgumentException("SHA-256 must contain exactly 64 hexadecimal characters.");
        return hash.ToUpperInvariant();
    }

    public static async Task<string> VerifyIsoHashAsync(string path, string? expected, CancellationToken token)
    {
        expected = expected is null ? null : NormalizeSha256(expected);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
        if (expected is not null && !actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The ISO checksum does not match. Download a fresh copy before upgrading.");
        AppLog.Info($"ISO SHA256: {actual}. Trusted expected hash supplied: {expected is not null}.");
        return actual;
    }

    public async Task<DownloadResult> DownloadIsoAsync(string destinationDirectory,
        IProgress<DownloadProgress>? progress, CancellationToken cancellationToken, string? expectedSha256 = null)
    {
        AutomaticDownloadPolicy.EnsureAvailable(CurrentWindows.Read().Language);
        InstallationSafety.EnsureSafe(AppContext.BaseDirectory, removeSelectedAnswerFiles: false);
        MountedSource? validatedSource = null;
        var result = await DownloadWithTransportFallbackAsync(endpoint => IsoTransfer.DownloadAsync(_http, endpoint, Path.GetFullPath(destinationDirectory),
            remaining =>
            {
                var storage = CheckDownloadDestination(destinationDirectory, remaining);
                if (!storage.IsValid) throw new InvalidOperationException(storage.Message);
            }, progress, cancellationToken, expectedSha256 ?? ConfiguredIsoSha256,
            validateMedia: async (path, token) =>
            {
                var source = await MountAndValidateAsync(path, token);
                InstallationImages.EnsureAutomaticWindows11Source(source);
                validatedSource = source;
            }));
        // Reuse only this operation's exact completed validation, never a path/time-based cache.
        return result with { ValidatedSource = validatedSource
            ?? throw new InvalidOperationException("The downloaded ISO did not complete media validation.") };
    }

    internal static async Task<DownloadResult> DownloadWithTransportFallbackAsync(Func<Uri, Task<DownloadResult>> transfer)
    {
        try { return await transfer(new Uri(DownloadEndpoint)); }
        catch (DownloadTransferException ex) when (ex.InnerException is HttpRequestException)
        {
            // The short-link host may require TLS 1.3. Windows 10 can securely use the
            // provider route directly with OS-default TLS and normal certificate checks.
            AppLog.Info($"Short-link transport unavailable; trying the provider directly. {ex.InnerException.Message}");
            return await transfer(new Uri(AutomaticDownloadProviderPage + "&download=1"));
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
        isoPath = Path.GetFullPath(isoPath);
        ValidateIsoFile(isoPath);
        AppLog.Info($"Mounting/locating exact ISO: {Path.GetFullPath(isoPath)}.");
        var escaped = isoPath.Replace("'", "''");
        var command = "$ProgressPreference='SilentlyContinue';$p='" + escaped + "';" +
            "$d=Get-DiskImage -ImagePath $p -ErrorAction SilentlyContinue;" +
            "if(-not $d -or -not $d.Attached){Mount-DiskImage -ImagePath $p -ErrorAction Stop|Out-Null};" +
            "$until=[DateTime]::UtcNow.AddSeconds(30);$v=$null;" +
            "do{$v=Get-DiskImage -ImagePath $p|Get-Volume -ErrorAction SilentlyContinue|Where-Object DriveLetter|Select-Object -First 1;" +
            "if(-not $v){Start-Sleep -Milliseconds 500}}while(-not $v -and [DateTime]::UtcNow -lt $until);" +
            "if(-not $v){throw 'PCNINJA_ISO_VOLUME_TIMEOUT'};" +
            "[Console]::Out.Write($v.DriveLetter+':\\')";
        var root = (await RunPowerShellAsync(command, cancellationToken)).Trim();
        AppLog.Info($"Resolved exact ISO {Path.GetFullPath(isoPath)} to volume {root}.");
        return await ValidateMountedSourceAsync(root, cancellationToken);
    }

    public MountedSource ValidateMountedSource(string root, bool removeSelectedAnswerFiles = true)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("No mounted installation source was selected.");
        root = Path.GetFullPath(root.Trim().Trim('"'));
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"The installation source folder was not found: {root}");
        var setup = System.IO.Path.Combine(root, "setup.exe");
        if (!File.Exists(setup))
            throw new InvalidOperationException($"setup.exe was not found in {root}.");
        if (!AuthenticodeVerifier.IsTrustedMicrosoftFile(setup, out var signer))
            throw new InvalidOperationException("setup.exe does not have a valid Microsoft digital signature.");
        InstallationSafety.EnsureSafe(root, removeSelectedAnswerFiles);
        AppLog.Info($"Validated installation source {root}; signer: {signer}.");
        return new MountedSource(root, setup);
    }

    public async Task<MountedSource> ValidateMountedSourceAsync(string root, CancellationToken cancellationToken,
        bool removeSelectedAnswerFiles = true)
    {
        var source = await Task.Run(() => ValidateMountedSource(root, removeSelectedAnswerFiles), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var sourcesDirectory = Path.Combine(source.Root, "sources");
        var installImage = new[] { "install.wim", "install.esd", "install.swm" }
            .Select(name => Path.Combine(sourcesDirectory, name))
            .FirstOrDefault(File.Exists);
        if (installImage is null)
            throw new InvalidOperationException(
                "The installation source does not contain sources\\install.wim, install.esd, or install.swm.");

        AppLog.Info($"Validating Windows image metadata with DISM: {installImage}.");
        var dism = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
        var result = await RunProcessAsync(dism, $"/English /Get-WimInfo /WimFile:\"{installImage}\"",
            cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Windows could not validate the installation image (DISM exit code {result.ExitCode}). " +
                "The ISO may be incomplete or damaged.");
        var images = new List<InstallationImage>();
        foreach (var index in InstallationImages.ParseIndexes(result.Output))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var details = await RunProcessAsync(dism,
                $"/English /Get-WimInfo /WimFile:\"{installImage}\" /Index:{index}", cancellationToken);
            if (details.ExitCode != 0)
                throw new InvalidOperationException($"Windows could not read image {index} (DISM exit code {details.ExitCode}).");
            images.Add(InstallationImages.ParseDetail(details.Output, index));
        }
        if (images.Count == 0) throw new InvalidOperationException("No installation images were found in the selected source.");
        AppLog.Info($"DISM read {images.Count} installation images from {installImage}.");
        return source with { ImagePath = installImage, Images = images };
    }

    public async Task<UpgradeResult> StartUpgradeAsync(MountedSource selectedSource, int imageIndex,
        bool allowAutomaticRestart, CancellationToken cancellationToken, Action? setupStarting = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(AppLog.Folder);
        await using var setupLock = new FileStream(Path.Combine(AppLog.Folder, "Setup-Launch.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        ValidateSupportedComputer();
        var storage = CheckSystemDriveSpace();
        if (!storage.IsValid) throw new InvalidOperationException(storage.Message);
        var source = await ValidateMountedSourceAsync(selectedSource.Root, cancellationToken, removeSelectedAnswerFiles: false);
        if (!string.Equals(source.ImagePath, selectedSource.ImagePath, StringComparison.OrdinalIgnoreCase) ||
            !(source.Images ?? []).SequenceEqual(selectedSource.Images ?? []))
            throw new InvalidOperationException("Installation media changed. Validate and select the image again.");
        var current = CurrentWindows.Read();
        var image = InstallationImages.Select(source, imageIndex, current);
        AppLog.Info($"Selected Setup profile: {InstallationImages.GetSetupProfile(image)}; " +
            $"image {image.Index}; image metadata version {image.Version}; current build {current.Build}.");
        var arguments = BuildSetupArguments(source, image, allowAutomaticRestart);
        var setupPath = source.SetupPath;
        InstallationSafety.EnsureSafe(source.Root, removeSelectedAnswerFiles: false);
        var attempt = UpgradeHistory.Begin(source, image);
        cancellationToken.ThrowIfCancellationRequested();
        setupStarting?.Invoke();

        AppLog.Info($"Starting Windows Setup. Automatic restart: {allowAutomaticRestart}. Command: setup.exe {arguments}");
        var startInfo = new ProcessStartInfo
        {
            FileName = setupPath,
            Arguments = arguments,
            UseShellExecute = true,
            WorkingDirectory = System.IO.Path.GetDirectoryName(setupPath)!
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Windows Setup could not be started.");
        // Once Setup starts, let Windows finish; cancelling the assistant cannot stop an upgrade safely.
        await process.WaitForExitAsync(CancellationToken.None);
        AppLog.Info($"Windows Setup exit code: {process.ExitCode}.");
        UpgradeHistory.RecordExit(attempt, process.ExitCode);
        return new UpgradeResult(process.ExitCode, setupPath);
    }

    internal static string BuildSetupArguments(MountedSource source, InstallationImage image, bool automaticRestart)
    {
        if (source.ImagePath is null || !(source.Images ?? []).Contains(image))
            throw new InvalidOperationException("The selected image is not bound to the validated source.");
        var command = InstallationImages.GetSetupProfile(image) switch
        {
            WindowsSetupProfile.Windows10 => "/auto upgrade /dynamicupdate disable /compat ignorewarning",
            WindowsSetupProfile.Windows11 => "/product server /auto upgrade /eula accept /dynamicupdate disable /compat ignorewarning",
            _ => throw new InvalidOperationException("No supported Setup command profile was found for the selected image.")
        };
        return command +
            $" /imageindex {image.Index} /installfrom \"{source.ImagePath}\"" + (automaticRestart ? "" : " /noreboot");
    }

    public static void OpenMicrosoftDownloadPage() => Process.Start(new ProcessStartInfo
    {
        FileName = MicrosoftDownloadPage,
        UseShellExecute = true
    });

    public static void OpenPcNinjaDownloadPage() => Process.Start(new ProcessStartInfo
    {
        // Manual language/version selection is separate from the internal automatic route.
        FileName = PcNinjaBrowserFallbackPage,
        UseShellExecute = true
    });

    private static async Task<string> RunPowerShellAsync(string command, CancellationToken cancellationToken)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Windows could not mount the ISO.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryTerminateProcess(process, "PowerShell ISO operation");
            throw;
        }
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            var failure = new InvalidOperationException(PowerShellDiagnostics.MountFailureMessage(error),
                new InvalidOperationException($"PowerShell exit code {process.ExitCode}. Raw stderr:\n{error}"));
            AppLog.Error($"ISO mount helper failed: {failure}");
            throw failure;
        }
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
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {Path.GetFileName(fileName)}.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryTerminateProcess(process, Path.GetFileName(fileName));
            throw;
        }
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static void TryTerminateProcess(Process process, string description)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Could not stop {description} after cancellation: {ex.Message}");
        }
    }

    internal static string GetRemoteFileName(HttpResponseMessage response)
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
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) ||
            fileName != Path.GetFileName(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new DownloadTransferException("The service did not provide a safe original ISO filename. Download in your browser and select the exact file.");
        return fileName;
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
