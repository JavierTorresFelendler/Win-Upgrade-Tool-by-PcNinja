# Command-line guide

The graphical interface opens when the EXE is started without arguments. The
same standalone EXE becomes a command-line tool when a command is supplied.

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe --help
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe help upgrade
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe help all
```

## Permissions

`status`, `scan`, `--help`, `--version`, and `log-path` can run normally.
Commands that mount or validate installation media, download and mount an ISO,
or start Windows Setup must run from an elevated Command Prompt or PowerShell.

## Built-in help and PowerShell completion

The EXE contains its own documentation and does not require GitHub access:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe help
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe help validate
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --help
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe help all
```

Enable Tab completion for the current PowerShell session:

```powershell
& .\PcNinja-Win11-Upgrade-Assistant-RC5.exe completion powershell |
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
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe status
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe status --json
```

Rescan ISO files found beside the application, mounted Windows media, and
available download drives:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe scan
```

A mounted Windows ISO appears under `Mounted Windows sources`. The drive must
contain `setup.exe` at its root. If the ISO is mounted as `E:\`, verify it with:

```powershell
Test-Path E:\setup.exe
```

## Use mounted media or an extracted Setup folder

Validate the mounted media without starting Windows Setup:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --mounted E:\
```

Validate it and start the upgrade:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --mounted E:\
```

Prevent Windows Setup from restarting automatically:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --mounted E:\ --no-restart
```

The drive is intentionally explicit. RC5 never guesses which mounted source to
use when more than one installation disc is present.

An extracted folder containing `setup.exe` and the `sources` directory can use
the same option:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --mounted "C:\Win11 Extracted"
```

## Use a local ISO file

Mount and validate an exact ISO path without starting Setup:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --iso "D:\ISO\Windows11.iso"
```

Start an upgrade from that ISO:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --iso "D:\ISO\Windows11.iso"
```

## Download an ISO automatically

**Pending next release (not in the published RC5 EXE):** Automatic download
requires system-default UI language **en-US**, matching the configured ISO.
Other languages, including en-GB, return validation exit code **3** before a
transfer. `status --json` reports `systemUiLanguage`,
`automaticDownloadAvailable`, and `automaticDownloadUnavailableReason`.
Matching manually supplied media remains available through `--iso`/`--mounted`.

Use [PcNinja ISO downloads](https://iso.pcninja.pro) for PcNinja media, or prefer
[Microsoft](https://www.microsoft.com/software-download/windows11) for standard
Microsoft media. Select the same system UI language, edition and x64 architecture.

Download to the default suitable fixed drive, validate, and start the upgrade:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --download
```

Select a download folder or removable NTFS/exFAT drive:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --download --destination "E:\PcNinja Downloads"
```

## Validation, confirmation, and automation

`validate` never starts Windows Setup. `--dry-run` provides the same safety when
using the `upgrade` command:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --iso "D:\ISO\Windows11.iso" --dry-run
```

An interactive upgrade requires typing `YES`. Automation must deliberately add
`--yes` to bypass that prompt:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --mounted E:\ --no-restart --yes
```

Add `--json` when the final result must be machine-readable. Progress and
diagnostic messages may still be written to the error stream so JSON output can
remain parseable.

## Installation image selection

`validate` lists the actual image file, all images and compatible indexes without
starting Setup. In JSON, inspect `imagePath`, `images`,
`compatibleImageIndexes` and `selectedImageIndex`.

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --mounted E:\ --json
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --mounted E:\ --image-index 6 --no-restart
```

Index 6 is only an example: never assume a fixed index for Pro. Without an explicit
index, exactly one matching edition/language/x64 image must exist. Windows 11
targets must be equal-or-newer builds. The manual Windows 10 -> Windows 10 POC
does not compare build order; Windows Setup decides whether apps/files can be
preserved. Windows 11 -> 10 remains blocked. Duplicate eligible images require
`--image-index`; nonexistent or
incompatible indexes are rejected. `--image-index` is accepted by `validate`,
`upgrade` and dry runs, and is listed in embedded help and option completion.
The command binds `/ImageIndex` and `/InstallFrom` to the validated file; split
images use the first `install.swm`. Image indexes are never guessed by completion.

Windows 10 uses the existing `--iso` or `--mounted` options, including WIM/ESD/SWM
media; no new flag or automatic Windows 10 downloader is introduced. Its command
is `/auto upgrade /dynamicupdate disable /compat ignorewarning`, with the exact
`/imageindex`, `/installfrom` and optional `/noreboot`. Windows 11 retains its
existing experimental command. Validation/dry-run JSON adds `setupProfile`,
`buildOrderChecked` and `policyNotice` for the unique/explicit selected image;
profile/policy are null when selection is ambiguous. Successful upgrade JSON also
reports profile/build-order policy. Eligibility is not proof that Setup will
approve migration. See [POC scope and VM checklist](WINDOWS10-POC.md).

## Answer-file safety

Selected-source and attached-media roots are checked for interfering answer files.
Only narrowly identified writable answer files within the selected installation
source can be deleted automatically. Secondary media, locked files or read-only
ISOs block the operation: detach the interfering ISO/USB or remove the file, then
retry. There is no bypass. Validation can mount an ISO and clean a selected source,
but never launches Setup. Windows-owned cached Panther files are not deleted.
Checks repeat immediately before Setup, so late-attached media can block launch.

## RC5 cancellation, downloads and checksums

Press **Ctrl+C** during download, checksum calculation or source preparation
to request cancellation (exit code 6). Once Windows Setup has started, the
assistant does not interrupt it. `--no-restart` still controls Setup's restart
behavior independently.

Retrying `upgrade --download` resumes an incomplete file only when the server
returns a matching endpoint, strong ETag, total size and valid byte range. An
ignored Range or changed validator restarts the exact owned partial. Completed
exact-name ISOs are revalidated and reused; only proven-corrupt recorded PcNinja
downloads are deleted for replacement. Unowned user files are never overwritten
or deleted, and no numbered copies are created. Environmental failures and
cancellation preserve completed ISOs.
The last completed download is recorded by its exact name in the chosen folder,
allowing revalidation/reuse even when the provider cannot be contacted. Header
requests have a 60-second timeout; 90 seconds without data stops a transfer.
Local storage/checksum errors do not open the download website.

SHA-256 is calculated for ISO files. For independent verification, obtain an
expected hash from a trusted source and pass it explicitly:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --iso "D:\Windows11.iso" --sha256 <64-hex-character-hash>
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --download --sha256 <64-hex-character-hash> --no-restart
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
start "" /wait PcNinja-Win11-Upgrade-Assistant-RC5.exe status --json
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
| 8 | Setup transition outcome unconfirmed; verify after restart |

The assistant never calls `shutdown.exe`. `--no-restart` adds `/noreboot` to
Windows Setup, and a failed Setup process does not trigger a forced restart.
After exit 0 with `--no-restart`, the CLI explicitly says to restart manually to
continue the upgrade. Successful upgrade JSON includes `manualRestartRequired`
(`true` with `--no-restart`, otherwise `false`); existing exit codes are unchanged.
`/noreboot` suppresses the first post-preparation reboot, not later Setup reboots
after the user manually restarts. PreparationCompleted is not final OS success.

## Post-restart outcome

`status --json` includes `previousUpgrade` from the persisted last-attempt record.
A zero process exit means only that Setup completed its current stage. Transition
exit `0x40010004` returns CLI code 8, not success; other nonzero Setup exits return 7.
`TargetWindowsObserved` requires the target build/edition to be running after an
observed build change with no active Setup. It does not prove files/apps were kept.
Same-build upgrades remain unconfirmed without additional evidence. No scheduled
task, startup entry, background service or forced restart is added.
Windows 10 POC attempts also require manual verification: the recorded image
build can be a shared base, not the exact installed feature release, so it never
triggers `TargetWindowsObserved` automatically.

For reliable waiting in PowerShell scripts with this GUI-subsystem EXE, capture
output through a pipeline, for example `$result = & .\PcNinja-Win11-Upgrade-Assistant-RC5.exe status --json | Out-String`, then read `$LASTEXITCODE`.
