# Command-line guide

The graphical interface opens when the EXE is started without arguments. The
same standalone EXE becomes a command-line tool when a command is supplied.

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe --help
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe help upgrade
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe help all
```

## Permissions

`status`, `scan`, `--help`, `--version`, and `log-path` can run normally.
Commands that mount or validate installation media, download and mount an ISO,
or start Windows Setup must run from an elevated Command Prompt or PowerShell.

## Built-in help and PowerShell completion

The EXE contains its own documentation and does not require GitHub access:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe help
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe help validate
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --help
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe help all
```

Enable Tab completion for the current PowerShell session:

```powershell
& .\PcNinja-Win11-Upgrade-Assistant-RC4.exe completion powershell |
    Out-String | Invoke-Expression
```

Completion covers commands, options relevant to the selected command, mounted
drives, extracted folders, ISO paths, and destination directories. The EXE
does not modify `$PROFILE`; run `help completion` before adding the generated
script to a persistent profile.

## Inspect the computer and available sources

Show the Windows version, system-drive storage, administrator state, and log
path:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe status
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe status --json
```

Rescan ISO files found beside the application, mounted Windows media, and
available download drives:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe scan
```

A mounted Windows ISO appears under `Mounted Windows sources`. The drive must
contain `setup.exe` at its root. If the ISO is mounted as `E:\`, verify it with:

```powershell
Test-Path E:\setup.exe
```

## Use mounted media or an extracted Setup folder

Validate the mounted media without starting Windows Setup:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --mounted E:\
```

Validate it and start the upgrade:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --mounted E:\
```

Prevent Windows Setup from restarting automatically:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --mounted E:\ --no-restart
```

The drive is intentionally explicit. RC4 never guesses which mounted source to
use when more than one installation disc is present.

An extracted folder containing `setup.exe` and the `sources` directory can use
the same option:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --mounted "C:\Win11 Extracted"
```

## Use a local ISO file

Mount and validate an exact ISO path without starting Setup:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --iso "D:\ISO\Windows11.iso"
```

Start an upgrade from that ISO:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --iso "D:\ISO\Windows11.iso"
```

## Download an ISO automatically

Download to the default suitable fixed drive, validate, and start the upgrade:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --download
```

Select a download folder or removable NTFS/exFAT drive:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --download --destination "E:\PcNinja Downloads"
```

## Validation, confirmation, and automation

`validate` never starts Windows Setup. `--dry-run` provides the same safety when
using the `upgrade` command:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --iso "D:\ISO\Windows11.iso" --dry-run
```

An interactive upgrade requires typing `YES`. Automation must deliberately add
`--yes` to bypass that prompt:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --mounted E:\ --no-restart --yes
```

Add `--json` when the final result must be machine-readable. Progress and
diagnostic messages may still be written to the error stream so JSON output can
remain parseable.

## RC4 cancellation, downloads and checksums

Press **Ctrl+C** during download, checksum calculation or source preparation
to request cancellation (exit code 6). Once Windows Setup has started, the
assistant does not interrupt it. `--no-restart` still controls Setup's restart
behavior independently.

Retrying `upgrade --download` resumes an incomplete file only when the server
returns a matching strong ETag, total size and valid byte range. Otherwise the
retained partial is left alone and a new `(Copy N)` download starts. Header
requests have a 60-second timeout; 90 seconds without data stops a transfer.
Local storage/checksum errors do not open the download website.

SHA-256 is calculated for ISO files. For independent verification, obtain an
expected hash from a trusted source and pass it explicitly:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --iso "D:\Windows11.iso" --sha256 <64-hex-character-hash>
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --download --sha256 <64-hex-character-hash> --no-restart
```

Alternatively set `PCNINJA_ISO_SHA256` before launching the app. That setting
applies to downloaded/local ISO files in GUI and CLI. An explicit `--sha256`
takes precedence in CLI. Whole-file hashing does not apply to `--mounted` media
or folders; `--sha256 --mounted` is rejected. A locally calculated hash without
an independently trusted expected value is not proof of ISO integrity.

Repeated options, options belonging to another command, and `--destination`
without `--download` are rejected with exit code 2.

The EXE uses the Windows GUI subsystem so double-clicking has no console flash.
PowerShell invocation and redirected output remain supported. For reliable
waiting and `%ERRORLEVEL%` handling in interactive CMD, use:

```bat
start "" /wait PcNinja-Win11-Upgrade-Assistant-RC4.exe status --json
```

## Exit-code reference

| Code | Meaning |
| ---: | --- |
| 0 | Success |
| 1 | Unexpected error |
| 2 | Invalid arguments |
| 3 | Validation, unsupported system, or storage failure |
| 4 | Automatic download failure |
| 5 | Administrator permission required |
| 6 | Confirmation or preparation cancelled |
| 7 | Windows Setup returned a failure code |

The assistant never calls `shutdown.exe`. `--no-restart` adds `/noreboot` to
Windows Setup, and a failed Setup process does not trigger a forced restart.
