# PcNinja Windows 11 Upgrade Assistant RC4

Portable Windows x64 prerelease, version **1.1.0-rc.4**.

Download **PcNinja-Win11-Upgrade-Assistant-RC4.exe** below. It includes the GUI,
the CLI and the offline manual. **SHA256SUMS.txt** identifies this release's EXE.
Windows 10/11 client only; Windows Server, LTSC and ARM64 are outside this RC.

## What changed

- Green Start beside Refresh; red Cancel during download/preparation; Setup
  cannot be interrupted through the assistant once it has started.
- No black console window at GUI startup; PowerShell/CLI output still works.
- Clickable `www.pcninja.pro` branding, one logo, CPU/RAM/free-space information,
  clear exclusive source highlighting and shorter user-facing messages.
- Automatic download remains the primary source, alongside exact local ISO,
  mounted media and extracted Setup folders. Custom download folders survive
  refresh, and cancellation results remain visible.
- Better NTFS/exFAT USB/drive filtering, detected reserved-partition exclusion,
  combined download/Windows workspace reservation and a final space recheck.
- Validated partial-download resume, stalled-transfer timeouts, SHA-256
  calculation and optional trusted-checksum comparison (`--sha256`).
- Browser fallback for network/service problems only; local disk and checksum
  errors stay in the app. No browser fallback on cancellation.
- CLI Ctrl+C preparation cancellation, stricter arguments, updated completion,
  and non-fatal logging errors.

The Setup command and restart policy are preserved. The assistant never calls
`shutdown.exe`; clearing automatic restart or passing `--no-restart` adds
`/noreboot`.

## Documentation

- [Overview and GUI](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/blob/v1.1.0-rc.4/README.md)
- [Complete changes](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/blob/v1.1.0-rc.4/CHANGELOG.md)
- [CLI guide](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/blob/v1.1.0-rc.4/docs/CLI.md)
- [User-message catalog](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/blob/v1.1.0-rc.4/docs/UI-MESSAGES.md)
- [Verification and field-test checklist](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/blob/v1.1.0-rc.4/docs/RC4-TESTING.md)

Offline help: `PcNinja-Win11-Upgrade-Assistant-RC4.exe help all`.

## Verification and limits

Local Core, transfer fault, GUI action and published-EXE CLI checks passed.
Transfer checks cover resume with exact bytes, ignored-range safe restart,
checksum mismatch rejection, filename collisions and mid-transfer cancellation.
GUI tests exercise actual Start/Cancel clicks without starting Windows Setup.

A new end-to-end Windows upgrade was **not** executed during RC4 verification.
Please test app-downloaded media on Windows 10 and Windows 11 VMs. DISM metadata
and a signed `setup.exe` do not prove full ISO integrity: provide an independently
trusted SHA-256 for that comparison. Resume depends on server ETag/Range support.
The EXE is unsigned, and the `/product server` compatibility behavior remains
experimental.
