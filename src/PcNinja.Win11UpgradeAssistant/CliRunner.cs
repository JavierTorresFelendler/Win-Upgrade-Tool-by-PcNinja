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
    private const string Version = "1.1.0-rc.2";

    public static async Task<int> RunAsync(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        try
        {
            var options = CliOptions.Parse(args);
            if (options.Command == "help") { PrintHelp(); return Success; }
            if (options.Command == "version") { Console.WriteLine(Version); return Success; }
            if (options.Command == "log-path") { Console.WriteLine(AppLog.Path); return Success; }

            var engine = new UpgradeEngine();
            engine.ValidateSupportedComputer();
            return options.Command switch
            {
                "status" => ShowStatus(engine, options.Json),
                "scan" or "refresh" or "rescan" => ShowScan(engine, options.Json),
                "validate" => await ValidateOrUpgradeAsync(engine, options, false),
                "upgrade" => await ValidateOrUpgradeAsync(engine, options, true),
                _ => Fail(InvalidArguments, $"Unknown command: {options.Command}. Use --help for syntax.", options.Json)
            };
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
    }

    private static int ShowStatus(UpgradeEngine engine, bool json)
    {
        var storage = engine.CheckSystemDriveSpace();
        var value = new
        {
            version = Version,
            operatingSystem = engine.GetOperatingSystemDescription(),
            isAdministrator = Program.IsAdministrator(),
            systemDrive = storage.RootPath,
            systemDriveFreeBytes = storage.AvailableBytes,
            systemDriveReady = storage.IsValid,
            logPath = AppLog.Path
        };
        Write(value, json,
            $"PcNinja Windows 11 Upgrade Assistant {Version}\n" +
            $"OS: {value.operatingSystem}\nAdministrator: {value.isAdministrator}\n" +
            $"System storage: {storage.Message}\nLog: {AppLog.Path}");
        return storage.IsValid ? Success : ValidationFailed;
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

    private static async Task<int> ValidateOrUpgradeAsync(UpgradeEngine engine, CliOptions options, bool startSetup)
    {
        if (!Program.IsAdministrator())
            return Fail(AdministratorRequired,
                "Run this command from an elevated Command Prompt or PowerShell window.", options.Json);

        var sourceCount = (options.IsoPath is null ? 0 : 1) + (options.MountedRoot is null ? 0 : 1) + (options.Download ? 1 : 0);
        if (sourceCount != 1)
            return Fail(InvalidArguments,
                "Choose exactly one source: --iso <path>, --mounted <drive>, or --download.", options.Json);

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
                    var progressText = item.Percentage.HasValue ? $"{item.Percentage}%" : UpgradeEngine.FormatBytes(item.BytesReceived);
                    Console.Error.Write($"\rDownloading {Path.GetFileName(item.DestinationPath)}: {progressText}     ");
                });
                DownloadResult download;
                try
                {
                    download = await engine.DownloadIsoAsync(destination, progress, CancellationToken.None);
                    if (!options.Json) Console.Error.WriteLine();
                }
                catch (Exception ex)
                {
                    if (!options.Json) Console.Error.WriteLine();
                    return Fail(DownloadFailed,
                        $"{ex.Message} Browser fallback: {UpgradeEngine.DownloadEndpoint}", options.Json);
                }
                exactIsoPath = download.IsoPath;
                source = await engine.MountAndValidateAsync(exactIsoPath, CancellationToken.None);
            }
            else if (options.IsoPath is not null)
            {
                exactIsoPath = Path.GetFullPath(options.IsoPath.Trim('"'));
                source = await engine.MountAndValidateAsync(exactIsoPath, CancellationToken.None);
            }
            else
            {
                source = await engine.ValidateMountedSourceAsync(options.MountedRoot!, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            return Fail(ValidationFailed, ex.Message, options.Json);
        }

        if (!startSetup || options.DryRun)
        {
            Write(new { valid = true, isoPath = exactIsoPath, mountedRoot = source.Root, setupPath = source.SetupPath },
                options.Json, $"Validation passed.\nMounted source: {source.Root}\nSetup: {source.SetupPath}");
            return Success;
        }

        if (!options.Yes)
        {
            if (Console.IsInputRedirected)
                return Fail(Cancelled, "Confirmation is required. Re-run interactively or add --yes.", options.Json);
            Console.Error.WriteLine("WARNING: This uses an experimental unsupported-hardware upgrade method.");
            Console.Error.WriteLine(options.NoRestart
                ? "Automatic restart is disabled."
                : "Windows Setup may restart this computer automatically.");
            Console.Error.Write("Type YES to start Windows Setup: ");
            if (!string.Equals(Console.ReadLine(), "YES", StringComparison.Ordinal))
                return Fail(Cancelled, "Upgrade cancelled. Windows Setup was not started.", options.Json);
        }

        var result = await engine.StartUpgradeAsync(source.SetupPath, !options.NoRestart, CancellationToken.None);
        if (result.ExitCode != 0)
            return Fail(SetupFailed,
                $"Windows Setup stopped with exit code {result.ExitCode}. No forced restart was issued.", options.Json);
        Write(new { started = true, setupExitCode = result.ExitCode, automaticRestart = !options.NoRestart,
                isoPath = exactIsoPath, mountedRoot = source.Root },
            options.Json, "Windows Setup completed its current stage successfully.");
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

    private static void PrintHelp()
    {
        Console.WriteLine("""
PcNinja Windows 11 Upgrade Assistant 1.1.0-rc.2

Usage:
  PcNinja-Win11-Upgrade-Assistant.exe status [--json]
  PcNinja-Win11-Upgrade-Assistant.exe scan [--json]
  PcNinja-Win11-Upgrade-Assistant.exe validate --iso <path> [--json]
  PcNinja-Win11-Upgrade-Assistant.exe validate --mounted <drive> [--json]
  PcNinja-Win11-Upgrade-Assistant.exe upgrade --iso <path> [--no-restart] [--yes] [--json]
  PcNinja-Win11-Upgrade-Assistant.exe upgrade --mounted <drive> [--no-restart] [--yes] [--json]
  PcNinja-Win11-Upgrade-Assistant.exe upgrade --download [--destination <folder>] [--no-restart] [--yes] [--json]

Commands:
  status       Show Windows, storage, administrator, and log information.
  scan         Rescan local ISO files, mounted media, and download drives.
  validate     Mount and fully validate media without starting Windows Setup.
  upgrade      Validate media and start the known-good Windows Setup command.
  log-path     Print the log path.

Options:
  --dry-run       Validate only, even when the upgrade command is used.
  --no-restart    Add /noreboot. PcNinja never calls shutdown.exe.
  --yes           Explicitly skip the interactive final confirmation.
  --json          Emit machine-readable final output.
  --help, -h      Show this help.
  --version       Show the version.

Mounting and upgrade commands must be run as Administrator.
""");
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
    bool Json)
{
    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0) throw new CliArgumentException("No command was supplied.");
        if (args.Any(arg => arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase)))
            return Empty("help");
        if (args[0].Equals("--version", StringComparison.OrdinalIgnoreCase)) return Empty("version");

        var command = args[0].ToLowerInvariant();
        string? iso = null;
        string? mounted = null;
        string? destination = null;
        var download = false;
        var noRestart = false;
        var yes = false;
        var dryRun = false;
        var json = false;

        for (var index = 1; index < args.Length; index++)
        {
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
                default: throw new CliArgumentException($"Unknown option: {args[index]}");
            }
        }

        return new CliOptions(command, iso, mounted, download, destination, noRestart, yes, dryRun, json);
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
