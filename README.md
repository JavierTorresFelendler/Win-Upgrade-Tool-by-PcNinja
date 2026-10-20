# PcNinja Windows 11 Upgrade Assistant

Standalone Windows application for guiding advanced home users through an
in-place Windows 11 upgrade on unsupported x64 hardware.

> [!WARNING]
> RC5 is prerelease software. It uses the undocumented `/product server`
> compatibility behavior with Windows client Setup. Back up important files
> before using it. Microsoft can change this behavior in future releases.

## AI-assisted maintenance

For AI-assisted maintenance, start with [AGENTS.md](AGENTS.md), the
[AI maintenance playbook](docs/AI-MAINTENANCE-PLAYBOOK.md), and the
[current development handoff](docs/DEVELOPMENT-STATUS.md). These files preserve
project-specific decisions and working procedures across assistants and machines.

## Download

Download the latest prerelease from the repository's **Releases** page:

```text
PcNinja-Win11-Upgrade-Assistant-RC5.exe
```

The portable x64 EXE contains both the graphical application and the CLI. No
PowerShell or CMD files are required.

## One-screen graphical workflow

> **Pending next release:** The language policy, automatic consent, diagnostics and Windows 10 POC below
> are source changes, not a replacement for the published RC5 executable.

The application shows the current Windows version, storage state, and source
choices on one screen:

1. **Download the ISO automatically.** Choose a download drive/folder.
2. **Use existing installation media.** Select an ISO, mounted drive, extracted
   Setup folder or network folder in one path field, using **ISO...** or **Folder...**.

Development test builds also accept a manually supplied **Windows 10 client x64**
image on Windows 10, using the same image selector and `--image-index`. By owner
request, this POC does **not compare build order inside Windows 10**: older,
same-build and newer images may be offered to Windows Setup, which must allow
keeping apps/files or stop. This is not a promise of downgrade/repair support.
Windows 11 to 10 remains blocked; Windows 11 target build checks are unchanged.
Automatic download remains Windows 11 only. See [Windows 10 POC tests](docs/WINDOWS10-POC.md).

Automatic Start asks once **before** downloading/reusing media, explaining that
validation and the compatible in-place upgrade follow with the chosen restart
policy. A unique compatible image continues without a second assistant prompt.
Manual media still asks after validation. Browser fallback, failures or ambiguous
images stop unattended continuation; Windows Setup can still show its own errors.
The assistant does not issue a forced reboot after failure.
With automatic restart off, successful Setup preparation shows **Restart required**
and keeps the manual-restart instruction visible. OK only dismisses the message;
restart your computer yourself to continue. This is not yet a completed migration.

The automatic operation hands its successfully validated exact source to the GUI
or CLI without immediate duplicate hashing/mounting. Final prelaunch validation
is retained. Manual Inspect then Start reuse and automatic mount cleanup remain
separate future work. Errors stay visible through Refresh; full details/log remain
available. See [development test checklist](docs/AUTOMATIC-FLOW-TESTING.md).

Automatic download currently supplies **English (en-US)** media. It is disabled
when Windows' system-default UI language does not match, including en-GB; the
screen explains the mismatch and keeps manual media selection available.
Matching manually supplied media is not restricted to English. Edition, x64,
Windows 11 build and all other safety checks still apply; this is not a claim
that every language/media combination has been field-tested.

For PcNinja media, use [PcNinja ISO downloads](https://iso.pcninja.pro). For standard
Microsoft Windows media, prefer the official Microsoft download page. Choose
the same system UI language, download the ISO, then select it with **ISO...**.
Changing the keyboard language does not change upgrade-language compatibility.

Refresh after downloading, mounting media or connecting a USB drive. The exact
source, image selection and restart policy are retained when the source is unchanged.
A single ISO beside the EXE is offered automatically; multiple files are never
resolved by newest-file guessing.

Use **Inspect images**, or **Start**, to enumerate the actual installation images.
Names, indexes, edition, x64 architecture, build and default language come from
that source. A unique same-edition/language match is selected; otherwise select
an image in the same screen. Incompatible images are rejected. Replacing the
source resets the old image index.

The downloader requires the original ISO filename supplied by the server.
An existing exact-name completed ISO is reused after size, hash and media checks.
The last completed app download is recorded by its exact name in that folder,
so a valid recorded ISO can also be revalidated when the provider is unavailable.
Only a proven-corrupt **recorded PcNinja download** is deleted before a fresh
transfer. Unknown user files, cancellation, signature-chain failures, unavailable
drives and interfering answer files never trigger automatic ISO deletion.
There is no numbered-copy allocator or broad wildcard selection.

Partial files retain their exact path and ownership metadata. Resume requires
the same endpoint, strong ETag, total size and valid range. If Range is ignored,
the owned partial restarts under its original name. Header requests time out
after 60 seconds; 90 seconds without incoming data stops a transfer.
A TLS connection failure at the short-link host triggers a secure direct-provider
attempt; certificate validation remains enabled. Provider rate/access blocks may
still require browser download.

The green **START UPGRADE** button becomes red **CANCEL** during preparation.
After cancellation, the result remains visible. Once Windows Setup is running,
the button is disabled as **SETUP RUNNING**. Closing the assistant is blocked
while it is busy. The application launches without a console window.

If the direct automatic transfer is temporarily unavailable, the application
opens [PcNinja ISO downloads](https://iso.pcninja.pro) for manual selection
and switches to exact local-ISO selection. After the browser download
finishes, select that file with **ISO...**. The official
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
- answer-file safety on the selected source and other attached media, repeated
  immediately before Setup; narrowly scoped writable selected-source answer
  files are removed, while secondary/read-only media blocks the upgrade
- actual image index, matching edition (including N/Single Language), x64 and
  system-default UI language; equal-or-newer build for Windows 11 targets only

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
used in RC5 and the [RC5 test notes](docs/RC5-TESTING.md) for coverage and limits.

## CLI

The same EXE contains global help, per-command help, and a complete offline
manual. It can also generate PowerShell Tab completion. Read-only commands do
not require elevation:

See the [complete CLI guide](docs/CLI.md) for mounted ISO drives, automatic
downloads, validation-only runs, JSON output, confirmations, and exit codes.

```powershell
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe help
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe help all
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe help upgrade
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe status --json
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe scan
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --iso "D:\Windows11.iso"
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe validate --mounted E:\
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --iso "D:\Windows11.iso" --no-restart
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --mounted E:\ --no-restart
.\PcNinja-Win11-Upgrade-Assistant-RC5.exe upgrade --download --destination "E:\PcNinja Downloads"
```

Enable PowerShell completion for the current session:

```powershell
& .\PcNinja-Win11-Upgrade-Assistant-RC5.exe completion powershell | Out-String | Invoke-Expression
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
| 8 | Setup transition outcome unconfirmed; verify after restart |

## Upgrade and restart behavior

The upgrade command is:

```text
setup.exe /product server /auto upgrade /eula accept /dynamicupdate disable /compat ignorewarning /imageindex <selected-index> /installfrom "<validated-image-file>"
```

Clearing **Allow Windows Setup to restart the computer automatically**, or using
CLI option `--no-restart`, adds `/noreboot`. The application never calls
`shutdown.exe` and never forces a restart after a failed Setup process.

Preparation completion is not a final upgrade result. A Setup transition exit
such as `0x40010004` is reported as unconfirmed, not automatically successful.
After restarting, reopen the GUI or run `status --json` to compare the running
build/edition with the recorded attempt. Confirm that your files and apps remain.
The last-attempt record is `Last-Upgrade.json` beside the log; no startup task or
background service is installed.

If an interfering ISO/USB is detected, detach it and Refresh. There is no warning
bypass or clean-install fallback. Windows-owned Panther cache files are never
automatically modified. Do not attach installation media after the final check.
Never continue if Windows Setup unexpectedly asks where to install Windows.

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
PR artifacts are test builds, not replacements for an explicitly published release.

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
