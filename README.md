# PcNinja Windows 11 Upgrade Assistant

Standalone Windows application for guiding advanced home users through an
in-place Windows 11 upgrade on unsupported x64 hardware.

> [!WARNING]
> RC2 is prerelease software. It uses the undocumented `/product server`
> compatibility behavior with Windows client Setup. Back up important files
> before using it. Microsoft can change this behavior in future releases.

## Download

Download the latest private prerelease from the repository's **Releases** page:

```text
PcNinja-Win11-Upgrade-Assistant-RC2.exe
```

The portable x64 EXE contains both the graphical application and the CLI. No
PowerShell or CMD files are required.

## One-screen graphical workflow

The application shows the current Windows version, storage state, and source
choices on one screen:

1. Download the configured PcNinja ISO automatically.
2. Select an ISO already downloaded.
3. Use a Windows ISO that is already mounted.
4. Refresh after downloading an ISO, mounting media, or connecting a USB drive.

The automatic downloader preserves the server-provided filename. Existing files
are never overwritten; collisions use `(Copy 1)`, `(Copy 2)`, and so on. A
failed transfer remains as the exact `.partial` file recorded in the log.

If the automatic service is unavailable, the application offers the normal
[PcNinja download page](https://11oem.pcninja.pro). The official
[Microsoft Windows 11 download page](https://www.microsoft.com/software-download/windows11)
is always available as an additional fallback.

Before starting Setup, the application validates:

- Windows 10 or Windows 11 client on x64 hardware
- at least 20 GB free on the Windows system drive
- enough free space and a writable NTFS/exFAT download destination
- completed download size against the server's `Content-Length`, when supplied
- ISO file existence and plausible size
- `setup.exe` at the root of the mounted source
- a valid Microsoft Authenticode signature on `setup.exe`
- a readable `install.wim`, `install.esd`, or `install.swm` through DISM
- absence of `Autounattend.xml`

## CLI

Run the same EXE with `--help` for full syntax. Read-only commands do not require
elevation:

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC2.exe status --json
.\PcNinja-Win11-Upgrade-Assistant-RC2.exe scan
.\PcNinja-Win11-Upgrade-Assistant-RC2.exe validate --iso "D:\Windows11.iso"
.\PcNinja-Win11-Upgrade-Assistant-RC2.exe upgrade --iso "D:\Windows11.iso" --no-restart
.\PcNinja-Win11-Upgrade-Assistant-RC2.exe upgrade --download --destination "E:\PcNinja Downloads"
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
| 6 | User confirmation cancelled |
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

The repository remains private during RC testing.
