namespace PcNinja.Win11UpgradeAssistant;

internal static class CliHelp
{
    internal const string Version = "1.1.0-rc.5";

    private static readonly string[] Topics =
        ["status", "scan", "validate", "upgrade", "log-path", "completion"];

    public static bool TryGetRequestedTopic(string[] args, out string? topic)
    {
        topic = null;
        if (args.Length == 0) return false;

        var first = args[0].ToLowerInvariant();
        if (first is "help" or "manual")
        {
            topic = args.Length > 1 ? args[1].ToLowerInvariant() : first == "manual" ? "all" : null;
            return true;
        }

        if (first is "--help" or "-h") return true;
        if (args.Length == 2 && args[1] is "--help" or "-h")
        {
            topic = first;
            return true;
        }

        return false;
    }

    public static bool TryPrint(string? topic)
    {
        topic = topic?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(topic))
        {
            Console.WriteLine(General);
            return true;
        }

        if (topic is "all" or "manual")
        {
            Console.WriteLine(General);
            foreach (var knownTopic in Topics)
            {
                Console.WriteLine();
                Console.WriteLine(GetTopic(knownTopic));
            }
            return true;
        }

        var text = GetTopic(topic);
        if (text is null) return false;
        Console.WriteLine(text);
        return true;
    }

    public static string GetPowerShellCompletion(string executableName)
    {
        var safeName = executableName.Replace("'", "''");
        return PowerShellCompletion.Replace("__EXE_NAME__", safeName, StringComparison.Ordinal);
    }

    private static string? GetTopic(string topic) => topic switch
    {
        "status" => Status,
        "scan" or "refresh" or "rescan" => Scan,
        "validate" => Validate,
        "upgrade" => Upgrade,
        "log-path" => LogPath,
        "completion" => Completion,
        _ => null
    };

    private const string General = """
PcNinja Windows 11 Upgrade Assistant 1.1.0-rc.5

The graphical interface opens when the EXE is started without arguments.
Supplying a command starts CLI mode.

Usage:
  PcNinja-Win11-Upgrade-Assistant-RC5.exe <command> [options]
  PcNinja-Win11-Upgrade-Assistant-RC5.exe help <command>
  PcNinja-Win11-Upgrade-Assistant-RC5.exe <command> --help

Commands:
  status       Show Windows, storage, administrator, and log information.
  scan         Find local ISO files, mounted Windows media, and download drives.
  validate     Validate one exact ISO file or mounted Windows source.
  upgrade      Validate a source and start Windows Setup after confirmation.
  log-path     Print the application log path.
  completion   Generate shell completion code. RC5 supports PowerShell.
  help         Show general help or detailed help for one command.

Global options:
  --json          Emit machine-readable final output where supported.
  --help, -h      Show help. It can follow a command, for example upgrade --help.
  --version       Show the application version.

Learn more:
  PcNinja-Win11-Upgrade-Assistant-RC5.exe help validate
  PcNinja-Win11-Upgrade-Assistant-RC5.exe help upgrade
  PcNinja-Win11-Upgrade-Assistant-RC5.exe help completion
  PcNinja-Win11-Upgrade-Assistant-RC5.exe help all

Exit codes:
  0  Success
  1  Unexpected error
  2  Invalid command or arguments
  3  Validation, unsupported system, or storage failure
  4  Automatic download failure
  5  Administrator permission required
  6  Confirmation cancelled
  7  Windows Setup returned a failure code
  8  Setup transition outcome is unconfirmed; verify after restart

Mounting, validation, and upgrade commands must be run as Administrator.
The assistant never calls shutdown.exe and never forces a restart after failure.
""";

    private const string Status = """
COMMAND: status

Show the detected Windows version, whether upgrades are supported, available
space on the Windows drive, administrator state, log path, and the previous
upgrade attempt. TargetWindowsObserved confirms only the running build/edition,
not preservation of personal files or applications.
Also reports the system UI language and automatic-download availability/reason.

Usage:
  PcNinja-Win11-Upgrade-Assistant-RC5.exe status
  PcNinja-Win11-Upgrade-Assistant-RC5.exe status --json

This command does not require Administrator permission and can report an
unsupported Windows Server or LTSC host without attempting an upgrade.
""";

    private const string Scan = """
COMMAND: scan

Rescan sources without changing the computer. Aliases: refresh, rescan.

Usage:
  PcNinja-Win11-Upgrade-Assistant-RC5.exe scan
  PcNinja-Win11-Upgrade-Assistant-RC5.exe scan --json

The output contains:
  Local ISO files          Suitable .iso files found beside the application.
  Mounted Windows sources Drives containing setup.exe at their root.
  Download locations      Ready fixed or removable drives and free space.

Example workflow for a mounted ISO:
  PcNinja-Win11-Upgrade-Assistant-RC5.exe scan
  PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --mounted E:\
  PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --mounted E:\ --no-restart

This command does not require Administrator permission.
""";

    private const string Validate = """
COMMAND: validate

Validate exactly one installation source without starting Windows Setup.

Usage:
  PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --iso <exact-iso-path> [--sha256 <hash>] [--image-index <index>] [--json]
  PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --mounted <drive-or-folder> [--image-index <index>] [--json]

Examples:
  PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --iso "D:\ISO\Windows11.iso"
  PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --mounted E:\
  PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --mounted "C:\Win11 Extracted"
  PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --mounted E:\ --json

--iso mounts the exact file, then validates the mounted media.
--mounted accepts either an ISO that Windows already mounted or an extracted
Windows Setup folder containing setup.exe and the sources directory.

Validation checks the system drive, ISO size, setup.exe location, Microsoft
signature, interfering answer files on selected and attached media, and image
metadata in install.wim, install.esd or the first install.swm. A narrowly scoped
writable answer file in the selected source is removed; otherwise Setup is
blocked. Other connected media and Windows-owned files are never auto-deleted.
Validation may mount the ISO and clean the selected source; it never runs Setup.
Administrator permission is required.

Image indexes, names, editions, architecture, build and default language are
listed, including compatible indexes. --image-index validates an explicit index.
Index numbers belong to this exact image file, not to a universal edition number.
ISO SHA-256 is calculated; --sha256 or PCNINJA_ISO_SHA256 supplies a trusted
expected hash for comparison. --sha256 cannot be used with --mounted.
""";

    private const string Upgrade = """
COMMAND: upgrade

Validate exactly one source and start the known-good Windows Setup command.

Source modes - choose exactly one:
  --iso <path>             Use and mount one exact local ISO file.
  --mounted <path>         Use mounted media or an extracted Setup folder.
  --download               Download an ISO automatically, then mount it.

Additional options:
  --destination <folder>   Download folder for --download. NTFS/exFAT required.
  --sha256 <hash>          Compare an ISO with a trusted 64-character SHA-256.
  --image-index <index>    Select an image in the validated source; positive integer.
  --no-restart             Add /noreboot; restart manually when ready.
  --dry-run                Perform full validation but do not start Setup.
  --yes                    Explicitly skip the interactive YES confirmation.
  --json                   Emit a machine-readable final result.

Examples:
  PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --mounted E:\
  PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --mounted "C:\Win11 Extracted"
  PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --mounted E:\ --no-restart
  PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --iso "D:\ISO\Windows11.iso" --dry-run
  PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --iso "D:\ISO\Windows11.iso" --no-restart
  PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --download --destination "F:\PcNinja Downloads"
  PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --mounted E:\ --no-restart --yes --json

The same-edition, x64, system-UI-language image is selected automatically only
when exactly one match exists. Windows 11 targets must be equal-or-newer builds.
For Windows 10 -> Windows 10, this POC does NOT compare build order: Setup must
decide whether an in-place upgrade/repair keeping apps and files is available.
Windows 11 -> Windows 10 remains blocked. Windows 10 targets require manually
supplied --iso or --mounted media; --download remains Windows 11 only.
Validation/dry-run JSON reports setupProfile and buildOrderChecked (null if no
unique/explicit image was selected). These are policy details, not proof of
migration support. Otherwise specify
--image-index after inspecting validate output. An explicit incompatible image
is rejected; there is no clean-install or arbitrary edition-change fallback.

Without --yes, type YES at the final safety prompt. Non-interactive execution
without --yes is cancelled. Administrator permission is required.

Windows 11 target arguments (existing experimental profile):
  /product server /auto upgrade /eula accept /dynamicupdate disable
  /compat ignorewarning /imageindex <selected-index> /installfrom "<image-file>"

Windows 10 target arguments:
  /auto upgrade /dynamicupdate disable /compat ignorewarning
  /imageindex <selected-index> /installfrom "<image-file>"
No /product server or /eula accept for Windows 10. No /quiet, clean or data-only
fallback is added. A refused migration must stop, not switch installation type.

--no-restart additionally supplies /noreboot. PcNinja never calls shutdown.exe.
After Setup exit 0 with --no-restart, restart manually to continue the upgrade.
Successful upgrade JSON includes manualRestartRequired. /noreboot suppresses
the first post-preparation reboot, not later Setup reboots after manual restart.
PreparationCompleted does not confirm a finished OS migration.

Ctrl+C cancels download, checksum and preparation work (exit code 6). It does
not interrupt Windows Setup after launch. Network failures may open the normal
browser fallback in interactive mode; disk errors never do.
Downloads stop after 90 seconds without incoming data. Retry resumes only the
exact original-name partial with matching endpoint, strong ETag, size and range.
An ignored Range restarts that owned partial; Copy N files are no longer created.
An existing exact-name completed ISO is reused after integrity/media validation.
Only a proven-corrupt, recorded PcNinja download is deleted automatically;
cancellation, media safety and environmental errors preserve the existing ISO.
The original filename is required; files are never guessed by a wildcard.
A short-link TLS connection failure triggers a secure direct-provider attempt;
provider blocks may still require the normal browser fallback.
Automatic download currently provides en-US media and requires system UI
language en-US. Other languages (including en-GB) must use matching manual
media with --iso or --mounted; --download is rejected with exit code 3.
Manual downloads: https://iso.pcninja.pro or
https://www.microsoft.com/software-download/windows11
For standard Microsoft Windows media, prefer Microsoft's download page.
SHA-256 is calculated for ISO files. Supply --sha256 or PCNINJA_ISO_SHA256 to
compare with a trusted value; a calculated hash alone is not proof of integrity.
""";

    private const string LogPath = """
COMMAND: log-path

Print the full path of the PcNinja upgrade log.

Usage:
  PcNinja-Win11-Upgrade-Assistant-RC5.exe log-path

This command does not require Administrator permission.
""";

    private const string Completion = """
COMMAND: completion

Generate a PowerShell native argument completer for the current EXE name.

Enable completion for the current PowerShell session:
  & .\PcNinja-Win11-Upgrade-Assistant-RC5.exe completion powershell |
    Out-String | Invoke-Expression

After registration, press Tab to complete commands, relevant options, mounted
drives containing setup.exe, ISO paths, and destination directories.

The command prints a script only. It does not modify $PROFILE automatically.
To make it persistent, review the generated script and add it to your profile.
PowerShell completion is supported in RC5; CMD receives the embedded help only.
""";

    private const string PowerShellCompletion = """
# PcNinja Windows 11 Upgrade Assistant native completion (RC5)
Register-ArgumentCompleter -Native -CommandName '__EXE_NAME__', 'PcNinja-Win11-Upgrade-Assistant.exe' -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)

    $elements = @($commandAst.CommandElements | ForEach-Object { $_.Extent.Text.Trim("'").Trim('"') })
    $command = if ($elements.Count -gt 1) { $elements[1].ToLowerInvariant() } else { '' }
    $previousIndex = if ([string]::IsNullOrWhiteSpace($wordToComplete)) { $elements.Count - 1 } else { $elements.Count - 2 }
    $previous = if ($previousIndex -ge 1) { $elements[$previousIndex].ToLowerInvariant() } else { '' }

    if ($previous -eq '--image-index') { return } # Validate lists the actual indexes; do not guess.

    if ($previous -eq '--mounted') {
        Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.Root 'setup.exe') } |
            ForEach-Object {
                [System.Management.Automation.CompletionResult]::new($_.Root, $_.Root, 'ParameterValue', 'Mounted Windows source')
            }
        $filter = if ([string]::IsNullOrWhiteSpace($wordToComplete)) { '*' } else { "$wordToComplete*" }
        Get-ChildItem -Path $filter -Directory -Force -ErrorAction SilentlyContinue |
            ForEach-Object {
                $value = $_.FullName
                $completion = if ($value.Contains(' ')) { "'" + $value.Replace("'", "''") + "'" } else { $value }
                [System.Management.Automation.CompletionResult]::new($completion, $_.Name, 'ParameterValue', $value)
            }
        return
    }

    if ($previous -in @('--iso', '--destination')) {
        $filter = if ([string]::IsNullOrWhiteSpace($wordToComplete)) { '*' } else { "$wordToComplete*" }
        Get-ChildItem -Path $filter -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.PSIsContainer -or ($previous -eq '--iso' -and $_.Extension -eq '.iso') } |
            ForEach-Object {
                $value = $_.FullName
                $completion = if ($value.Contains(' ')) { "'" + $value.Replace("'", "''") + "'" } else { $value }
                [System.Management.Automation.CompletionResult]::new($completion, $_.Name, 'ParameterValue', $value)
            }
        return
    }

    $commands = @('status', 'scan', 'validate', 'upgrade', 'log-path', 'completion', 'help', '--help', '--version')
    $options = switch ($command) {
        'status'     { @('--json', '--help') }
        'scan'       { @('--json', '--help') }
        'refresh'    { @('--json', '--help') }
        'rescan'     { @('--json', '--help') }
        'validate'   { @('--iso', '--mounted', '--sha256', '--image-index', '--json', '--help') }
        'upgrade'    { @('--iso', '--mounted', '--download', '--destination', '--sha256', '--image-index', '--no-restart', '--dry-run', '--yes', '--json', '--help') }
        'completion' { @('powershell', '--help') }
        'help'       { @('status', 'scan', 'validate', 'upgrade', 'log-path', 'completion', 'all') }
        default      { $commands }
    }

    $options |
        Where-Object { $_ -like "$wordToComplete*" } |
        Sort-Object -Unique |
        ForEach-Object {
            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterName', $_)
        }
}
""";
}
