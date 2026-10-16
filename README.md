# PcNinja Windows 11 Upgrade Assistant

Standalone Windows application for guiding advanced home users through an
in-place Windows 11 upgrade on unsupported x64 hardware.

> [!WARNING]
> RC4 is prerelease software. It uses the undocumented `/product server`
> compatibility behavior with Windows client Setup. Back up important files
> before using it. Microsoft can change this behavior in future releases.

## Download

Download the latest prerelease from the repository's **Releases** page:

```text
PcNinja-Win11-Upgrade-Assistant-RC4.exe
```

The portable x64 EXE contains both the graphical application and the CLI. No
PowerShell or CMD files are required.

## One-screen graphical workflow

The application shows the current Windows version, storage state, and source
choices on one screen:

1. Download the configured PcNinja ISO automatically.
2. Select an ISO already downloaded.
3. Use a Windows ISO that is already mounted or an extracted Setup folder.
4. Refresh after downloading an ISO, mounting media, or connecting a USB drive.

The automatic downloader preserves the server-provided filename. Existing files
are never overwritten; collisions use `(Copy 1)`, `(Copy 2)`, and so on. A
failed or cancelled transfer remains as the exact `.partial` file recorded in the
log. Retrying automatic download resumes it only when the server supplies the
same strong ETag, total length and a valid byte-range response. Otherwise a new
numbered copy is downloaded. Header requests time out after 60 seconds and a
transfer stops after 90 seconds without incoming data.

The green **START UPGRADE** button becomes red **CANCEL** during preparation.
After cancellation, the result remains visible. Once Windows Setup is running,
the button is disabled as **SETUP RUNNING**. Closing the assistant is blocked
while it is busy. The application launches without a console window.

If the direct automatic transfer is temporarily unavailable, the application
opens the configured PcNinja browser fallback without forcing direct-download
mode and switches to exact local-ISO selection. After the browser download
finishes, select that file with **Browse**. The official
[Microsoft Windows 11 download page](https://www.microsoft.com/software-download/windows11)
is always available as an additional fallback.

Before starting Setup, the application validates:

- Windows 10 or Windows 11 client on x64 hardware
- at least 20 GB free on the Windows system drive
- enough free space and a writable NTFS/exFAT download destination
- completed download size against the server's `Content-Length`, when supplied
- SHA-256 calculation, with comparison when a trusted expected hash is supplied
- ISO file existence and plausible size
- `setup.exe` at the root of the mounted source
- a valid Microsoft Authenticode signature on `setup.exe`
- a readable `install.wim`, `install.esd`, or `install.swm` through DISM
- absence of `Autounattend.xml`

Downloading onto the Windows drive reserves space for both the remaining ISO
bytes and the 20 GB upgrade workspace, plus a 1 GB download margin. Windows
workspace is checked again immediately before Setup starts. FAT32, small drives
and partitions identified as hidden, recovery or system partitions are excluded
from download choices. NTFS/exFAT USB drives remain supported. Partition checks
are best effort when Windows does not expose partition metadata.

**Integrity limit:** reading image metadata with DISM and checking `setup.exe`'s
signature does not verify every byte of the Windows image. A calculated SHA-256
is an identifier, not independent proof. Obtain a trusted hash separately and
set `PCNINJA_ISO_SHA256` before opening the GUI, or supply CLI `--sha256`.
Mounted media/extracted folders do not support whole-ISO checksum comparison.

Routine errors appear in the main window; **View details** shows diagnostics.
See the [message catalog](docs/UI-MESSAGES.md) for the short English wording
used in RC4 and the [RC4 test notes](docs/RC4-TESTING.md) for coverage and limits.

## CLI

The same EXE contains global help, per-command help, and a complete offline
manual. It can also generate PowerShell Tab completion. Read-only commands do
not require elevation:

See the [complete CLI guide](docs/CLI.md) for mounted ISO drives, automatic
downloads, validation-only runs, JSON output, confirmations, and exit codes.

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe help
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe help all
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe help upgrade
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe status --json
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe scan
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --iso "D:\Windows11.iso"
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe validate --mounted E:\
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --iso "D:\Windows11.iso" --no-restart
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --mounted E:\ --no-restart
.\PcNinja-Win11-Upgrade-Assistant-RC4.exe upgrade --download --destination "E:\PcNinja Downloads"
```

Enable PowerShell completion for the current session:

```powershell
& .\PcNinja-Win11-Upgrade-Assistant-RC4.exe completion powershell | Out-String | Invoke-Expression
```

Mount, validation, download-and-mount, and upgrade operations must be launched
from an elevated terminal. An upgrade asks for explicit `YES` confirmation;
automation must deliberately add `--yes`. Use `--dry-run` for validation only
and `--json` for machine-readable output.

Stable CLI exit codes:

| Code | Meaning |
| ---: | --- |
| 0 | Success |
| 1 | Unexpected error |
| 2 | Invalid arguments |
| 3 | Validation or storage failure |
| 4 | Download failure |
| 5 | Administrator permission required |
| 6 | User confirmation or preparation cancelled |
| 7 | Windows Setup returned failure |

## Upgrade and restart behavior

The upgrade command is:

```text
setup.exe /product server /auto upgrade /eula accept /dynamicupdate disable /compat ignorewarning
```

Clearing **Allow Windows Setup to restart the computer automatically**, or using
CLI option `--no-restart`, adds `/noreboot`. The application never calls
`shutdown.exe` and never forces a restart after a failed Setup process.

Logs are written to:

```text
C:\ProgramData\PcNinja\Win11UpgradeAssistant\Upgrade-Assistant.log
```

## Architecture

- `src/PcNinja.Win11Upgrade.Core` - reusable download, storage, validation,
  mounting, and Windows Setup engine.
- `src/PcNinja.Win11UpgradeAssistant` - standalone branded WinForms and CLI host.

This boundary is intentional. The standalone product remains independent while
the Core project can later be integrated into PcNinja WinUpdate Tool without
copying the standalone interface.

## Development workflow

Work is tracked through issues, feature branches and Draft PRs under the owner's
account. No other person's approval is required. Merge and release publication
remain separate owner decisions. See the [GitHub workflow guide](docs/GITHUB-WORKFLOW.md)
for checkpoints, test artifacts, recovery and the proposed main protection.
PR test builds do not replace the published RC4 release.

## Build

Requirements: .NET 8 SDK or later on Windows.

```powershell
dotnet run --project tests\PcNinja.Win11Upgrade.SmokeTests\PcNinja.Win11Upgrade.SmokeTests.csproj -c Release
dotnet publish src\PcNinja.Win11UpgradeAssistant\PcNinja.Win11UpgradeAssistant.csproj -c Release -r win-x64 --self-contained true
```

The GitHub release workflow produces the single-file EXE and SHA-256 checksum
when a `v*` tag is pushed.

## Current scope

- Windows 10 and Windows 11 client editions
- x64 only
- Consumer Windows 11 x64 ISO media
- In-place upgrade only
- No Windows Server or LTSC support
- English interface and CLI
- Unsigned prerelease binary

RC releases remain prereleases. Repository visibility is managed separately.
