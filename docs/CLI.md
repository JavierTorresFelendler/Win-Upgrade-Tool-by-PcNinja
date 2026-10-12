# Command-line guide

The graphical interface opens when the EXE is started without arguments. The
same standalone EXE becomes a command-line tool when a command is supplied.

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe --help
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe help upgrade
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe help all
```

## Permissions

`status`, `scan`, `--help`, `--version`, and `log-path` can run normally.
Commands that mount or validate installation media, download and mount an ISO,
or start Windows Setup must run from an elevated Command Prompt or PowerShell.

## Built-in help and PowerShell completion

The EXE contains its own documentation and does not require GitHub access:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe help
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe help validate
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe upgrade --help
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe help all
```

Enable Tab completion for the current PowerShell session:

```powershell
& .\PcNinja-Win11-Upgrade-Assistant-RC3.exe completion powershell |
    Out-String | Invoke-Expression
```

Completion covers commands, options relevant to the selected command, mounted
drives containing `setup.exe`, ISO paths, and destination directories. The EXE
does not modify `$PROFILE`; run `help completion` before adding the generated
script to a persistent profile.

## Inspect the computer and available sources

Show the Windows version, system-drive storage, administrator state, and log
path:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe status
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe status --json
```

Rescan ISO files found beside the application, mounted Windows media, and
available download drives:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe scan
```

A mounted Windows ISO appears under `Mounted Windows sources`. The drive must
contain `setup.exe` at its root. If the ISO is mounted as `E:\`, verify it with:

```powershell
Test-Path E:\setup.exe
```

## Use an already mounted ISO

Validate the mounted media without starting Windows Setup:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe validate --mounted E:\
```

Validate it and start the upgrade:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe upgrade --mounted E:\
```

Prevent Windows Setup from restarting automatically:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe upgrade --mounted E:\ --no-restart
```

The drive is intentionally explicit. RC3 never guesses which mounted source to
use when more than one installation disc is present.

## Use a local ISO file

Mount and validate an exact ISO path without starting Setup:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe validate --iso "D:\ISO\Windows11.iso"
```

Start an upgrade from that ISO:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe upgrade --iso "D:\ISO\Windows11.iso"
```

## Download an ISO automatically

Download to the default suitable fixed drive, validate, and start the upgrade:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe upgrade --download
```

Select a download folder or removable NTFS/exFAT drive:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe upgrade --download --destination "E:\PcNinja Downloads"
```

## Validation, confirmation, and automation

`validate` never starts Windows Setup. `--dry-run` provides the same safety when
using the `upgrade` command:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe upgrade --iso "D:\ISO\Windows11.iso" --dry-run
```

An interactive upgrade requires typing `YES`. Automation must deliberately add
`--yes` to bypass that prompt:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC3.exe upgrade --mounted E:\ --no-restart --yes
```

Add `--json` when the final result must be machine-readable. Progress and
diagnostic messages may still be written to the error stream so JSON output can
remain parseable.

## Exit codes

| Code | Meaning |
| ---: | --- |
| 0 | Success |
| 1 | Unexpected error |
| 2 | Invalid arguments |
| 3 | Validation, unsupported system, or storage failure |
| 4 | Automatic download failure |
| 5 | Administrator permission required |
| 6 | Confirmation cancelled |
| 7 | Windows Setup returned a failure code |

The assistant never calls `shutdown.exe`. `--no-restart` adds `/noreboot` to
Windows Setup, and a failed Setup process does not trigger a forced restart.
