namespace PcNinja.Win11UpgradeAssistant;

internal static class CliHelp
{
    internal const string Version = "1.1.0-rc.4";

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
PcNinja Windows 11 Upgrade Assistant 1.1.0-rc.4

The graphical interface opens when the EXE is started without arguments.
Supplying a command starts CLI mode.

Usage:
  PcNinja-Win11-Upgrade-Assistant-RC4.exe <command> [options]
  PcNinja-Win11-Upgrade-Assistant-RC4.exe help <command>
  PcNinja-Win11-Upgrade-Assistant-RC4.exe <command> --help

Commands:
  status       Show Windows, storage, administrator, and log information.
  scan         Find local ISO files, mounted Windows media, and download drives.
  validate     Validate one exact ISO file or mounted Windows source.
  upgrade      Validate a source and start Windows Setup after confirmation.
  log-path     Print the application log path.
  completion   Generate shell completion code. RC4 supports PowerShell.
  help         Show general help or detailed help for one command.

Global options:
  --json          Emit machine-readable final output where supported.
  --help, -h      Show help. It can follow a command, for example upgrade --help.
  --version       Show the application version.

Learn more:
  PcNinja-Win11-Upgrade-Assistant-RC4.exe help validate
  PcNinja-Win11-Upgrade-Assistant-RC4.exe help upgrade
  PcNinja-Win11-Upgrade-Assistant-RC4.exe help completion
  PcNinja-Win11-Upgrade-Assistant-RC4.exe help all

Exit codes:
  0  Success
  1  Unexpected error
  2  Invalid command or arguments
  3  Validation, unsupported system, or storage failure
  4  Automatic download failure
  5  Administrator permission required
  6  Confirmation cancelled
  7  Windows Setup returned a failure code

Mounting, validation, and upgrade commands must be run as Administrator.
The assistant never calls shutdown.exe and never forces a restart after failure.
""";

    private const string Status = """
COMMAND: status

Show the detected Windows version, whether upgrades are supported, available
space on the Windows drive, administrator state, and log path.

Usage:
  PcNinja-Win11-Upgrade-Assistant-RC4.exe status
  PcNinja-Win11-Upgrade-Assistant-RC4.exe status --json

This command does not require Administrator permission and can report an
unsupported Windows Server or LTSC host without attempting an upgrade.
""";

    private const string Scan = """
COMMAND: scan

Rescan sources without changing the computer. Aliases: refresh, rescan.

Usage:
  PcNinja-Win11-Upgrade-Assistant-RC4.exe scan
  PcNinja-Win11-Upgrade-Assistant-RC4.exe scan --json

The output contains:
  Local ISO files          Suitable .iso files found beside the application.
  Mounted Windows sources Drives containing setup.exe at their root.
  Download locations      Ready fixed or removable drives and free space.

Example workflow for a mounted ISO:
  PcNinja-Win11-Upgrade-Assistant-RC4.exe scan
  PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --mounted E:\
  PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --mounted E:\ --no-restart

This command does not require Administrator permission.
""";

    private const string Validate = """
COMMAND: validate

Validate exactly one installation source without starting Windows Setup.

Usage:
  PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --iso <exact-iso-path> [--sha256 <hash>] [--json]
  PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --mounted <drive-or-folder> [--json]

Examples:
  PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --iso "D:\ISO\Windows11.iso"
  PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --mounted E:\
  PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --mounted "C:\Win11 Extracted"
  PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --mounted E:\ --json

--iso mounts the exact file, then validates the mounted media.
--mounted accepts either an ISO that Windows already mounted or an extracted
Windows Setup folder containing setup.exe and the sources directory.

Validation checks the system drive, ISO size, setup.exe location, Microsoft
signature, absence of Autounattend.xml, and DISM readability of install.wim,
install.esd, or install.swm. Administrator permission is required.
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
  --no-restart             Add /noreboot; restart manually when ready.
  --dry-run                Perform full validation but do not start Setup.
  --yes                    Explicitly skip the interactive YES confirmation.
  --json                   Emit a machine-readable final result.

Examples:
  PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --mounted E:\
  PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --mounted "C:\Win11 Extracted"
  PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --mounted E:\ --no-restart
  PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --iso "D:\ISO\Windows11.iso" --dry-run
  PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --iso "D:\ISO\Windows11.iso" --no-restart
  PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --download --destination "F:\PcNinja Downloads"
  PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --mounted E:\ --no-restart --yes --json

Without --yes, type YES at the final safety prompt. Non-interactive execution
without --yes is cancelled. Administrator permission is required.

Windows Setup arguments:
  /product server /auto upgrade /eula accept /dynamicupdate disable
  /compat ignorewarning

--no-restart additionally supplies /noreboot. PcNinja never calls shutdown.exe.

Ctrl+C cancels download, checksum and preparation work (exit code 6). It does
not interrupt Windows Setup after launch. Network failures may open the normal
browser fallback in interactive mode; disk errors never do.
Downloads pause after 90 seconds without incoming data. Retry resumes a partial
only when the server confirms the same strong ETag, size and byte range.
SHA-256 is calculated for ISO files. Supply --sha256 or PCNINJA_ISO_SHA256 to
compare with a trusted value; a calculated hash alone is not proof of integrity.
""";

    private const string LogPath = """
COMMAND: log-path

Print the full path of the PcNinja upgrade log.

Usage:
  PcNinja-Win11-Upgrade-Assistant-RC4.exe log-path

This command does not require Administrator permission.
""";

    private const string Completion = """
COMMAND: completion

Generate a PowerShell native argument completer for the current EXE name.

Enable completion for the current PowerShell session:
  & .\PcNinja-Win11-Upgrade-Assistant-RC4.exe completion powershell |
    Out-String | Invoke-Expression

After registration, press Tab to complete commands, relevant options, mounted
drives containing setup.exe, ISO paths, and destination directories.

The command prints a script only. It does not modify $PROFILE automatically.
To make it persistent, review the generated script and add it to your profile.
PowerShell completion is supported in RC4; CMD receives the embedded help only.
""";

    private const string PowerShellCompletion = """
# PcNinja Windows 11 Upgrade Assistant native completion (RC4)
Register-ArgumentCompleter -Native -CommandName '__EXE_NAME__', 'PcNinja-Win11-Upgrade-Assistant.exe' -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)

    $elements = @($commandAst.CommandElements | ForEach-Object { $_.Extent.Text.Trim("'").Trim('"') })
    $command = if ($elements.Count -gt 1) { $elements[1].ToLowerInvariant() } else { '' }
    $previousIndex = if ([string]::IsNullOrWhiteSpace($wordToComplete)) { $elements.Count - 1 } else { $elements.Count - 2 }
    $previous = if ($previousIndex -ge 1) { $elements[$previousIndex].ToLowerInvariant() } else { '' }

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
        'validate'   { @('--iso', '--mounted', '--sha256', '--json', '--help') }
        'upgrade'    { @('--iso', '--mounted', '--download', '--destination', '--sha256', '--no-restart', '--dry-run', '--yes', '--json', '--help') }
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
