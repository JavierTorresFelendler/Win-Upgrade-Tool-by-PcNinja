using System.Text.Json;
using PcNinja.Win11Upgrade.Core;

namespace PcNinja.Win11UpgradeAssistant;

internal static class CliRunner
{
    internal const int Success = 0;
    internal const int UnexpectedError = 1;
    internal const int InvalidArguments = 2;
    internal const int ValidationFailed = 3;
    internal const int DownloadFailed = 4;
    internal const int AdministratorRequired = 5;
    internal const int Cancelled = 6;
    internal const int SetupFailed = 7;
    internal const int OutcomeUnconfirmed = 8;
    private const string Version = CliHelp.Version;
    private static volatile bool _setupRunning;

    public static async Task<int> RunAsync(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            if (!_setupRunning) cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            if (CliHelp.TryGetRequestedTopic(args, out var helpTopic))
            {
                if (args.Length > 2)
                    return Fail(InvalidArguments, "Help accepts one topic. Use 'help all' for the complete manual.", false);
                if (!CliHelp.TryPrint(helpTopic))
                    return Fail(InvalidArguments,
                        $"Unknown help topic: {helpTopic}. Use 'help' to list available topics.", false);
                return Success;
            }

            if (args[0].Equals("completion", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 2 || !args[1].Equals("powershell", StringComparison.OrdinalIgnoreCase))
                    return Fail(InvalidArguments,
                        "Usage: completion powershell. Run 'help completion' for setup instructions.", false);
                var executableName = Path.GetFileName(Environment.ProcessPath)
                    ?? "PcNinja-Win11-Upgrade-Assistant-RC5.exe";
                Console.WriteLine(CliHelp.GetPowerShellCompletion(executableName));
                return Success;
            }

            var options = CliOptions.Parse(args);
            if (options.Command == "version") { Console.WriteLine(Version); return Success; }
            if (options.Command == "log-path") { Console.WriteLine(AppLog.Path); return Success; }

            var engine = new UpgradeEngine();
            return options.Command switch
            {
                "status" => ShowStatus(engine, options.Json),
                "scan" or "refresh" or "rescan" => ShowScan(engine, options.Json),
                "validate" => await ValidateOrUpgradeAsync(engine, options, false, cancellation.Token),
                "upgrade" => await ValidateOrUpgradeAsync(engine, options, true, cancellation.Token),
                _ => Fail(InvalidArguments,
                    $"Unknown command: {options.Command}. Use 'help' to list commands.", options.Json)
            };
        }
        catch (OperationCanceledException)
        {
            return Fail(Cancelled, "Operation cancelled. Windows Setup was not started.", args.Contains("--json", StringComparer.OrdinalIgnoreCase));
        }
        catch (CliArgumentException ex)
        {
            return Fail(InvalidArguments, ex.Message, args.Contains("--json", StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            AppLog.Error($"CLI failure: {ex}");
            return Fail(UnexpectedError, ex.Message, args.Contains("--json", StringComparer.OrdinalIgnoreCase));
        }
        finally { Console.CancelKeyPress -= cancelHandler; _setupRunning = false; }
    }

    private static int ShowStatus(UpgradeEngine engine, bool json)
    {
        var storage = engine.CheckSystemDriveSpace();
        var language = CurrentWindows.Read().Language;
        string? supportError = null;
        try { engine.ValidateSupportedComputer(); }
        catch (Exception ex) { supportError = ex.Message; }
        var value = new
        {
            version = Version,
            operatingSystem = engine.GetOperatingSystemDescription(),
            upgradeSupported = supportError is null,
            supportError,
            systemUiLanguage = language,
            automaticDownloadAvailable = AutomaticDownloadPolicy.IsAvailable(language),
            automaticDownloadUnavailableReason = AutomaticDownloadPolicy.GetUnavailableReason(language),
            isAdministrator = Program.IsAdministrator(),
            systemDrive = storage.RootPath,
            systemDriveFreeBytes = storage.AvailableBytes,
            systemDriveReady = storage.IsValid,
            logPath = AppLog.Path,
            previousUpgrade = UpgradeHistory.ReadStatus()
        };
        Write(value, json,
            $"PcNinja Windows 11 Upgrade Assistant {Version}\n" +
            $"OS: {value.operatingSystem}\nUpgrade supported: {value.upgradeSupported}" +
            (supportError is null ? "" : $" ({supportError})") +
            $"\nSystem UI language: {language}\nAutomatic download available: {value.automaticDownloadAvailable}" +
            (value.automaticDownloadUnavailableReason is null ? "" : $"\n{value.automaticDownloadUnavailableReason}") +
            $"\nAdministrator: {value.isAdministrator}\n" +
            $"System storage: {storage.Message}\nLog: {AppLog.Path}" +
            (value.previousUpgrade is null ? "" : $"\nPrevious upgrade: {value.previousUpgrade.State}. {value.previousUpgrade.Message}"));
        return Success;
    }

    private static int ShowScan(UpgradeEngine engine, bool json)
    {
        var local = engine.FindLocalIsos();
        var mounted = engine.FindMountedSources();
        var drives = engine.FindDownloadLocations();
        if (json)
        {
            Write(new { localIsos = local, mountedSources = mounted, downloadLocations = drives }, true, "");
        }
        else
        {
            Console.WriteLine("Local ISO files:");
            WriteList(local);
            Console.WriteLine("Mounted Windows sources:");
            WriteList(mounted.Select(source => source.Root));
            Console.WriteLine("Download locations:");
            WriteList(drives.Select(drive => drive.ToString()));
        }
        return Success;
    }

    private static async Task<int> ValidateOrUpgradeAsync(UpgradeEngine engine, CliOptions options, bool startSetup, CancellationToken token)
    {
        try { engine.ValidateSupportedComputer(); }
        catch (Exception ex) { return Fail(ValidationFailed, ex.Message, options.Json); }

        if (!Program.IsAdministrator())
            return Fail(AdministratorRequired,
                "Run this command from an elevated Command Prompt or PowerShell window.", options.Json);

        var sourceCount = (options.IsoPath is null ? 0 : 1) + (options.MountedRoot is null ? 0 : 1) + (options.Download ? 1 : 0);
        if (sourceCount != 1)
            return Fail(InvalidArguments,
                "Choose exactly one source: --iso <path>, --mounted <drive>, or --download.", options.Json);

        if (options.Download && AutomaticDownloadPolicy.GetUnavailableReason(CurrentWindows.Read().Language) is string reason)
            return Fail(ValidationFailed, reason, options.Json);

        var storage = engine.CheckSystemDriveSpace();
        if (!storage.IsValid) return Fail(ValidationFailed, storage.Message, options.Json);

        MountedSource source;
        string? exactIsoPath = null;
        try
        {
            if (options.Download)
            {
                var destination = options.Destination ?? GetDefaultDestination(engine);
                var progress = options.Json ? null : new Progress<DownloadProgress>(item =>
                {
                    var progressText = item.Stage ?? (item.Percentage.HasValue ? $"{item.Percentage}%" : UpgradeEngine.FormatBytes(item.BytesReceived));
                    Console.Error.Write($"\rDownloading {Path.GetFileName(item.DestinationPath)}: {progressText}     ");
                });
                DownloadResult download;
                try
                {
                    download = await engine.DownloadIsoAsync(destination, progress, token, options.Sha256);
                    if (!options.Json) Console.Error.WriteLine();
                }
                catch (DownloadTransferException ex)
                {
                    if (!options.Json) Console.Error.WriteLine();
                    var browserOpened = false;
                    if (!options.Json && !Console.IsInputRedirected && !Console.IsOutputRedirected)
                    {
                        try
                        {
                            UpgradeEngine.OpenPcNinjaDownloadPage();
                            browserOpened = true;
                        }
                        catch (Exception browserError)
                        {
                            AppLog.Error($"CLI browser fallback failed: {browserError}");
                        }
                    }
                    return Fail(DownloadFailed,
                        browserOpened
                            ? $"{ex.Message} The PcNinja browser fallback was opened. " +
                              "After the ISO downloads, rerun with --iso <path>."
                            : $"{ex.Message} Automatic download did not complete; provide the ISO with --iso <path>.",
                        options.Json);
                }
                exactIsoPath = download.IsoPath;
                source = download.ValidatedSource
                    ?? throw new InvalidOperationException("The downloaded ISO did not complete media validation.");
            }
            else if (options.IsoPath is not null)
            {
                exactIsoPath = Path.GetFullPath(options.IsoPath.Trim('"'));
                engine.ValidateIsoFile(exactIsoPath);
                await UpgradeEngine.VerifyIsoHashAsync(exactIsoPath, options.Sha256 ?? UpgradeEngine.ConfiguredIsoSha256, token);
                source = await engine.MountAndValidateAsync(exactIsoPath, token);
            }
            else
            {
                source = await engine.ValidateMountedSourceAsync(options.MountedRoot!, token);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Fail(ValidationFailed, ex.Message, options.Json);
        }

        InstallationImage? selectedImage = null;
        try
        {
            if (options.ImageIndex.HasValue || (startSetup && !options.DryRun))
                selectedImage = InstallationImages.Select(source, options.ImageIndex, CurrentWindows.Read());
        }
        catch (InvalidOperationException ex) { return Fail(ValidationFailed, ex.Message, options.Json); }
        var matchingImages = InstallationImages.Compatible(source, CurrentWindows.Read());
        var previewImage = selectedImage ?? (matchingImages.Count == 1 ? matchingImages[0] : null);
        var previewProfile = previewImage is null ? null : InstallationImages.GetSetupProfile(previewImage);
        if (!startSetup || options.DryRun)
        {
            Write(new { valid = true, isoPath = exactIsoPath, mountedRoot = source.Root, setupPath = source.SetupPath,
                    imagePath = source.ImagePath, images = source.Images, compatibleImageIndexes = matchingImages.Select(image => image.Index),
                    selectedImageIndex = previewImage?.Index, setupProfile = previewProfile?.ToString(),
                    buildOrderChecked = previewProfile is null ? (bool?)null : previewProfile != WindowsSetupProfile.Windows10,
                    policyNotice = previewProfile == WindowsSetupProfile.Windows10 ? InstallationImages.Windows10Notice : null },
                options.Json, $"Validation passed.\nSource: {source.Root}\nImage file: {source.ImagePath}\n" +
                string.Join("\n", (source.Images ?? []).Select(image => image.ToString())) +
                $"\nCompatible indexes: {string.Join(", ", matchingImages.Select(image => image.Index))}" +
                (previewProfile == WindowsSetupProfile.Windows10 ? $"\n{InstallationImages.Windows10Notice}" : ""));
            return Success;
        }

        if (!options.Yes)
        {
            if (Console.IsInputRedirected)
                return Fail(Cancelled, "Confirmation is required. Re-run interactively or add --yes.", options.Json);
            Console.Error.WriteLine(InstallationImages.GetSetupProfile(selectedImage!) == WindowsSetupProfile.Windows10
                ? $"WARNING: {InstallationImages.Windows10Notice}"
                : "WARNING: This uses an experimental unsupported-hardware upgrade method.");
            Console.Error.WriteLine($"Image {selectedImage!.Index}: {selectedImage.Name}");
            Console.Error.WriteLine(options.NoRestart
                ? "Automatic restart is disabled."
                : "Windows Setup may restart this computer automatically.");
            Console.Error.Write("Type YES to start Windows Setup: ");
            if (!string.Equals(Console.ReadLine(), "YES", StringComparison.Ordinal))
                return Fail(Cancelled, "Upgrade cancelled. Windows Setup was not started.", options.Json);
        }

        token.ThrowIfCancellationRequested();
        UpgradeResult result;
        try { result = await engine.StartUpgradeAsync(source, selectedImage!.Index, !options.NoRestart, token,
            () => _setupRunning = true); }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex) { return Fail(ValidationFailed, ex.Message, options.Json); }
        if (result.ExitCode == 0x40010004)
            return Fail(OutcomeUnconfirmed, "Setup exited during the transition. The upgrade outcome is not confirmed. After restarting, run status --json and verify your files/apps.", options.Json);
        if (result.ExitCode != 0)
            return Fail(SetupFailed,
                $"Windows Setup stopped with exit code {result.ExitCode}. No forced restart was issued.", options.Json);
        Write(new { started = true, setupExitCode = result.ExitCode, automaticRestart = !options.NoRestart,
                manualRestartRequired = result.NeedsManualRestartNotice(!options.NoRestart),
                setupProfile = InstallationImages.GetSetupProfile(selectedImage)?.ToString(),
                buildOrderChecked = InstallationImages.GetSetupProfile(selectedImage) != WindowsSetupProfile.Windows10,
                isoPath = exactIsoPath, mountedRoot = source.Root, imageIndex = selectedImage.Index,
                outcome = "PreparationCompleted" },
            options.Json, result.NeedsManualRestartNotice(!options.NoRestart)
                ? UpgradeResult.ManualRestartMessage
                : "Windows Setup completed its current stage. Verify the final outcome after restarting.");
        return Success;
    }

    private static string GetDefaultDestination(UpgradeEngine engine)
    {
        var location = engine.FindDownloadLocations().FirstOrDefault(item => item.DriveType == DriveType.Fixed)
            ?? throw new InvalidOperationException("No suitable download drive was found. Use --destination <folder>.");
        return Path.Combine(location.RootPath, "PcNinja Downloads");
    }

    private static int Fail(int code, string message, bool json)
    {
        if (json) Console.WriteLine(JsonSerializer.Serialize(new { success = false, exitCode = code, error = message }));
        else Console.Error.WriteLine($"ERROR: {message}");
        return code;
    }

    private static void Write(object value, bool json, string text)
    {
        Console.WriteLine(json ? JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) : text);
    }

    private static void WriteList(IEnumerable<string> items)
    {
        var values = items.ToArray();
        if (values.Length == 0) Console.WriteLine("  (none)");
        else foreach (var value in values) Console.WriteLine($"  {value}");
    }

}

internal sealed record CliOptions(
    string Command,
    string? IsoPath,
    string? MountedRoot,
    bool Download,
    string? Destination,
    bool NoRestart,
    bool Yes,
    bool DryRun,
    bool Json,
    string? Sha256 = null,
    int? ImageIndex = null)
{
    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0) throw new CliArgumentException("No command was supplied.");
        if (args[0].Equals("--version", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 1) throw new CliArgumentException("--version does not accept additional options.");
            return Empty("version");
        }

        var command = args[0].ToLowerInvariant();
        string? iso = null;
        string? mounted = null;
        string? destination = null;
        string? sha256 = null;
        int? imageIndex = null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var download = false;
        var noRestart = false;
        var yes = false;
        var dryRun = false;
        var json = false;

        for (var index = 1; index < args.Length; index++)
        {
            if (!seen.Add(args[index])) throw new CliArgumentException($"Repeated option: {args[index]}.");
            switch (args[index].ToLowerInvariant())
            {
                case "--iso": iso = RequireValue(args, ref index, "--iso"); break;
                case "--mounted": mounted = RequireValue(args, ref index, "--mounted"); break;
                case "--download": download = true; break;
                case "--destination": destination = RequireValue(args, ref index, "--destination"); break;
                case "--no-restart": noRestart = true; break;
                case "--yes": yes = true; break;
                case "--dry-run": dryRun = true; break;
                case "--json": json = true; break;
                case "--image-index":
                    if (!int.TryParse(RequireValue(args, ref index, "--image-index"), out var value) || value < 1)
                        throw new CliArgumentException("--image-index must be a positive integer.");
                    imageIndex = value;
                    break;
                case "--sha256":
                    try { sha256 = UpgradeEngine.NormalizeSha256(RequireValue(args, ref index, "--sha256")); }
                    catch (ArgumentException ex) { throw new CliArgumentException(ex.Message); }
                    break;
                default: throw new CliArgumentException(
                    $"Unknown option for {command}: {args[index]}. Run '{command} --help' for valid options.");
            }
        }

        var allowed = command switch
        {
            "status" or "scan" or "refresh" or "rescan" => new[] { "--json" },
            "validate" => new[] { "--iso", "--mounted", "--sha256", "--image-index", "--json" },
            "upgrade" => new[] { "--iso", "--mounted", "--download", "--destination", "--sha256", "--image-index", "--no-restart", "--yes", "--dry-run", "--json" },
            _ => Array.Empty<string>()
        };
        foreach (var option in seen)
            if (!allowed.Contains(option, StringComparer.OrdinalIgnoreCase))
                throw new CliArgumentException($"{option} is not valid for {command}. See '{command} --help'.");
        if (destination is not null && !download) throw new CliArgumentException("--destination requires --download.");
        if (sha256 is not null && mounted is not null) throw new CliArgumentException("--sha256 requires --iso or --download.");
        return new CliOptions(command, iso, mounted, download, destination, noRestart, yes, dryRun, json, sha256, imageIndex);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new CliArgumentException($"{option} requires a value.");
        return args[index];
    }

    private static CliOptions Empty(string command) => new(command, null, null, false, null, false, false, false, false);
}

internal sealed class CliArgumentException(string message) : Exception(message);
