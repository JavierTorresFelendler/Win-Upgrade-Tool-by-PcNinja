# PcNinja Windows 11 Upgrade Assistant

Standalone Windows application for guiding advanced home users through an
in-place Windows 11 upgrade on unsupported x64 hardware.

> [!WARNING]
> RC1 is a private proof of concept. It uses the undocumented `/product server`
> compatibility behavior with Windows client setup. Back up important files
> before using it. Microsoft can change this behavior in future releases.

## Download

Download the latest private release from the repository's **Releases** page.
The release contains one portable, self-contained x64 EXE:

```text
PcNinja-Win11-Upgrade-Assistant-RC1.exe
```

No PowerShell or CMD files are required in the release package. Windows asks
for Administrator permission when the EXE starts.

## One-screen workflow

The application shows the current Windows version and all source choices on
one screen:

1. Use a suitable ISO found next to the EXE.
2. Download the configured PcNinja ISO automatically.
3. Select an ISO that was downloaded elsewhere.
4. Use a Windows ISO that is already mounted.

If automatic download is unavailable, the application offers the official
[Microsoft Windows 11 download page](https://www.microsoft.com/software-download/windows11)
and retains the local ISO picker.

Before starting Setup, the application validates:

- Windows 10 or Windows 11 client on x64 hardware
- ISO file existence and plausible size
- `setup.exe` at the root of the mounted source
- a valid Microsoft Authenticode signature on `setup.exe`
- absence of `Autounattend.xml`

## Upgrade and restart behavior

The upgrade command is:

```text
setup.exe /product server /auto upgrade /eula accept /dynamicupdate disable /compat ignorewarning
```

Users can clear **Allow Windows Setup to restart the computer automatically**.
That adds `/noreboot`. The application never calls `shutdown.exe` and never
forces a restart after a failed Setup process.

Logs are written to:

```text
C:\ProgramData\PcNinja\Win11UpgradeAssistant\Upgrade-Assistant.log
```

## Architecture

- `src/PcNinja.Win11Upgrade.Core` — reusable ISO, validation, mounting,
  download, and Windows Setup engine.
- `src/PcNinja.Win11UpgradeAssistant` — standalone WinForms user interface.

This separation is intentional: the standalone product can remain independent
while the Core project is later integrated into another PcNinja update tool.

## Build

Requirements: .NET 8 SDK or later on Windows.

```powershell
dotnet publish src\PcNinja.Win11UpgradeAssistant\PcNinja.Win11UpgradeAssistant.csproj -c Release -r win-x64 --self-contained true
```

The GitHub release workflow builds the same single-file EXE and publishes its
SHA-256 checksum when a `v*` tag is pushed.

## Current scope

- Windows 10 and Windows 11 client editions
- x64 only
- Consumer Windows 11 x64 ISO media
- In-place upgrade only
- No Windows Server or LTSC support
- English user interface in RC1

The repository remains private during RC testing.
