# Windows Upgrade Tool by PcNinja

Free-to-use standalone Windows application for guiding advanced home users through an
in-place Windows upgrade on x64 hardware, including an experimental Windows 11
compatibility method and an experimental manual Windows 10 target path.

> [!WARNING]
> The Windows 11 path uses the undocumented
> `/product server` compatibility behavior. Back up important files
> before using it. Microsoft can change this behavior in future releases.

## AI-assisted maintenance

For AI-assisted maintenance, start with [AGENTS.md](AGENTS.md), the
[AI maintenance playbook](docs/AI-MAINTENANCE-PLAYBOOK.md), and the
[current development handoff](docs/DEVELOPMENT-STATUS.md). These files preserve
project-specific decisions and working procedures across assistants and machines.

## Download

V1 is being prepared for official publication. Download a published build only
from this repository's [Releases](https://github.com/JavierTorresFelendler/Win-Upgrade-Tool-by-PcNinja/releases)
page; a PR artifact is a test build, not an official release. The first stable
download uses this short name:

```text
Win-Upgrade-Tool-by-PcNinja-v.1.exe
```

The portable x64 EXE contains both the graphical application and the CLI. No
PowerShell or CMD files are required.

Public V1 is technical version `1.0.0`; V1.1 is `1.1.0` and uses
`Win-Upgrade-Tool-by-PcNinja-v.1.1.exe`. Full version/commit metadata remains in
file properties, diagnostic logs, `version` and `status --json`. The GUI title
stays **Windows Upgrade Tool by PcNinja**. Historical RC/Beta assets keep
their original names and bytes. See [product identity](docs/PRODUCT-IDENTITY.md).

V1 includes [portable application updates](docs/APPLICATION-UPDATES.md).
New version-named EXEs stay beside the running tool; nothing moves to Program
Files or overwrites the old EXE. After the new GUI confirms startup, only the
exact predecessor is marked `OLD-`, or `OLD.V1-`, `OLD.V2-`, etc. if occupied.
A regular/critical offer shows the release summary and exact GitHub notes;
only **Download and run the new version** authorizes EXE transfer.
Stable discovers stable only; Beta discovers numbered Betas only. **Beta users
must download and open the first stable V1 manually once.** The old
`1.1.0-beta.5` cannot switch channels or downgrade itself to stable `1.0.0`.
Future stable-to-stable updates use the normal consent-based updater. Windows
restart flags and external Setup do not gate updates to this application.

The tool is free to use, but it is **not offered under an open-source license**.
See [LICENSE](LICENSE) for the actual permissions and restrictions, and
[SECURITY.md](SECURITY.md) for private vulnerability reporting.

Completed, correlated Windows Setup rollbacks are reported in the existing GUI
warning/details area and CLI status. See [outcome diagnostics](docs/SETUP-OUTCOME-DIAGNOSTICS.md).

## One-screen graphical workflow

[Matched ISO download guidance](docs/MATCHED-ISO-DOWNLOADS.md) prefers a configured
PcNinja source only when the installed profile matches its coverage. Otherwise,
request a matching public browser download, then select the exact ISO manually.
Matching manual x64 client LTSC media is supported without enabling automatic
LTSC download, edition/channel conversion or Server execution.

> **Included in V1:** Full-profile automatic-download preflight, matching manual
> download guidance, safe exact-ISO session reuse, Downloads-first browsing and
> visual next-action cues. Includes optional quiet Setup and machine-readable CLI
> results. Original branding, safety gates and two-source screen remain.

The application shows the current Windows version, storage state, and source
choices on one screen:

1. **Download Windows 11 automatically.** Choose a download drive/folder.
2. **Use an ISO, drive or folder.** Select an ISO, mounted drive, extracted
   Setup folder or network folder in one path field, using **ISO...** or **Folder...**.

The tool also accepts a manually supplied **Windows 10 client x64**
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
is retained. Manual Validate then Start, or switching to Automatic for the exact
same ISO, can reuse successful session preparation only with unchanged-file and
exact mount-identity proof; unavailable proof falls back to full validation.
Automatic mount cleanup is not included. Errors stay visible through Refresh;
full details/log remain available. See [session reuse](docs/ISO-VALIDATION-REUSE.md).

Automatic download currently supplies **English (en-US), x64 Windows 11** media
for eleven configured Home/Pro/Education-family editions. The installed edition,
language, architecture, client/LTSC/Server type and known build family are checked
before an ISO transfer or destination creation. An unsupported profile, including
en-GB, keeps manual media selection available with an explanation. Eligibility
uses configured media coverage; the actual downloaded bytes must still validate.
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

ISO browsing starts in the user's registered Downloads folder. Visual cues guide
manual selection through **ISO...**, **Validate**, image choice and **Start**;
the cues do not replace validation, consent or mismatch/error explanations.
Automatic mode retains its direct Start/download/validation/upgrade flow.

Use **Validate**, or **Start**, to enumerate the actual installation images.
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
opens the [matched PcNinja browser route](docs/MATCHED-ISO-DOWNLOADS.md) and
switches to exact local-ISO selection. Other profiles retain the broader
[PcNinja ISO downloads](https://iso.pcninja.pro) catalog and can request a matching
alternative. After the browser download finishes, select that file with **ISO...**.
Browser fallback does not resume unattended upgrade consent. The official
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
and the [historical RC8 test matrix](docs/RC8-TESTING.md) for field scenarios and
limits. Do not treat every matrix row as independently field-tested.

## CLI

V1 includes [quiet Setup and automation results](docs/CLI-AUTOMATION.md)
for remote scripts/management agents, including explicit restart readiness.

The same EXE contains global help, per-command help, and a complete offline
manual. It can also generate PowerShell Tab completion. Read-only commands do
not require elevation:

See the [complete CLI guide](docs/CLI.md) for mounted ISO drives, automatic
downloads, validation-only runs, JSON output, confirmations, and exit codes.

```powershell
.\Win-Upgrade-Tool-by-PcNinja-v.1.exe help
.\Win-Upgrade-Tool-by-PcNinja-v.1.exe help all
.\Win-Upgrade-Tool-by-PcNinja-v.1.exe help upgrade
.\Win-Upgrade-Tool-by-PcNinja-v.1.exe status --json
.\Win-Upgrade-Tool-by-PcNinja-v.1.exe scan
.\Win-Upgrade-Tool-by-PcNinja-v.1.exe downloads --json
.\Win-Upgrade-Tool-by-PcNinja-v.1.exe validate --iso "D:\Windows11.iso"
.\Win-Upgrade-Tool-by-PcNinja-v.1.exe validate --mounted E:\
.\Win-Upgrade-Tool-by-PcNinja-v.1.exe upgrade --iso "D:\Windows11.iso" --no-restart
.\Win-Upgrade-Tool-by-PcNinja-v.1.exe upgrade --mounted E:\ --no-restart
.\Win-Upgrade-Tool-by-PcNinja-v.1.exe upgrade --download --destination "E:\PcNinja Downloads"
```

Enable PowerShell completion for the current session:

```powershell
& .\Win-Upgrade-Tool-by-PcNinja-v.1.exe completion powershell | Out-String | Invoke-Expression
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
| 3 | Validation/storage failure or app-update blocker |
| 4 | Download or app-update check/verification/helper failure |
| 5 | Administrator permission required |
| 6 | User confirmation or preparation cancelled |
| 7 | Windows Setup returned failure |
| 8 | Setup transition outcome unconfirmed; verify after restart |

## Upgrade and restart behavior

The Windows 11 target upgrade command is:

```text
setup.exe /product server /auto upgrade /eula accept /dynamicupdate disable /compat ignorewarning /imageindex <selected-index> /installfrom "<validated-image-file>"
```

Windows 10 targets use the same upgrade-only profile without `/product server`
or `/eula accept`. Optional quiet mode adds `/quiet`. CLI `--quiet` requires
explicit `--yes`; CLI restart control remains independent of quiet mode.

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

## ISO sources and acknowledgements

Thank you to the [Massgrave](https://massgrave.dev/) contributors for maintaining
the public Windows ISO catalogs used by our [matching download fallback](docs/MATCHED-ISO-DOWNLOADS.md).
PcNinja sources remain the preferred configured option; the external catalog
helps users find matching media in other languages and editions. This fallback
provides manual browser guidance, not automatic third-party transfer.

- [Massgrave website and ISO catalogs](https://massgrave.dev/)
- [Catalog and website source on GitHub](https://github.com/massgravel/massgrave.dev)
- [Massgrave's main project on GitHub](https://github.com/massgravel/Microsoft-Activation-Scripts)

Our integration references ISO download catalogs only; it does not include or run
activation scripts or modify licensing. Microsoft's official download alternative
remains available. Source recommendations do not replace PcNinja's actual-media
compatibility, signature and safety checks.

See the dedicated [credits and external-source notes](docs/CREDITS.md) for the
role of their catalogs, source links, upstream terms and integration boundaries.
This acknowledgement does not claim formal partnership or endorsement.

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

`Directory.Build.props` contains the authoritative literal `PcNinjaReleaseVersion`
for Core/GUI metadata and packaging. The first official line maps V1 to `1.0.0`;
the release status is recorded in [Development status](docs/DEVELOPMENT-STATUS.md).
Published RC/Beta assets remain immutable.

The tag workflow requires reviewed main ancestry, matching tag/version/notes,
read-only CLI verification and the final EXE's exact `version+commit` metadata
under the 100,000,000-byte cap; the explicitly authorized Beta branch test
requires exact passed PR CI instead of main ancestry. RC notes use `docs/RELEASE-RC{N}.md`; RC releases
keep EXE, SHA256SUMS and BUILD-INFO as three prerelease assets, with no updater
manifest. Beta releases additionally include a channel-bound manifest and validated
`docs/updates/{version}.json` regular/critical message. They are non-latest
prereleases and may use an owner-approved exact PR-CI-passed unmerged commit.
Numeric `x.y.z` stable releases require
`docs/RELEASE-{version}.md` and add `update-manifest.json` as a fourth asset.
Both use draft-first upload/metadata verification; stable publication updates
latest only after verified assets and refuses backwards movement. Existing
published releases/assets are never overwritten. Owner approval, completed checks
and a matching release identity remain separate from ordinary development builds.

The offline contract fixtures make no network or GitHub changes:

```powershell
& ./tests/Verify-AppUpdatePackaging.ps1
```

See the [updater implementation contract](docs/design/APPLICATION-UPDATES.md)
for manifest, staging, trust and the distinction between fixture and live VM checks.

## Current scope

- Windows 10 and Windows 11 client editions
- x64 only
- Matching Windows 10/11 x64 client ISO or extracted/mounted media
- In-place upgrade only
- Matching manual client LTSC media; no automatic LTSC download/channel conversion
- Windows Server download/inspection/explicit manual-command-copy guidance only;
  the assistant never starts Setup on Server
- English interface and CLI
- Consent-based portable application updates through GitHub
- Currently unsigned binaries; published SHA-256 is not independent publisher signing
- No automatic media extraction/copy/detach preparation

Historical RC/Beta releases remain prereleases. Repository visibility is managed
separately and does not turn this free-to-use product into open-source software.

## License, support and security

The owner provides the tool free of charge for use under [LICENSE](LICENSE).
Do not assume that public source visibility permits modifying or redistributing
the code or rebranding the tool. Check the license for the actual permission terms.
Third-party software, installation media and trademarks retain their own terms.

Report ordinary reproducible defects through the repository's
[issue tracker](https://github.com/JavierTorresFelendler/Win-Upgrade-Tool-by-PcNinja/issues),
with sensitive information removed. Send security vulnerabilities privately to
**javier@pcninja.pro**, following [SECURITY.md](SECURITY.md); do not publish exploit
details or customer logs in an issue.
