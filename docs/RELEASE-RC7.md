# Windows Upgrade Assistant By PcNinja - RC7

Portable Windows x64 prerelease **1.1.0-rc.7**. Download
**Windows-Upgrade-Assistant-By-PcNinja.exe**. No scripts or separate .NET install
are needed. The filename stays fixed; file properties, logs, CLI and BUILD-INFO
identify the version/commit. SHA256SUMS identifies the exact binary.

## Changes since RC6

- **Simpler main-window wording (PR #16):** shorter source descriptions and
  buttons, no redundant source/computer kickers, clearer automatic Windows 11
  versus existing Windows 10/11 media choice, and simpler restart wording.
- **Resize rendering:** both bordered dashboard cards redraw completely when
  resized, using double buffering. This addresses stale border lines appearing
  at the bottom and edges after growing/shrinking or maximizing the window.
- Preserve the original PcNinja header logo, EXE icon, clickable www.pcninja.pro,
  PcNinja manual downloads at iso.pcninja.pro and Microsoft's download link.

## Unchanged upgrade behavior

No Setup switches, ISO provider, download/reuse behavior, safety checks, image
selection, consent dialogs, restart policy or CLI contracts are changed. All
RC6 Windows 10/11 features and limits remain. No app self-updater, ISO-copy/
clean/detach feature or popup-flow rewrite is added. PRs #9/#13 remain design only.

This is an **unsigned experimental prerelease**, not a stable production release.
Back up important files. Windows 11 uses the undocumented `/product server`
method. Never continue if Windows Setup asks for a partition or offers a clean
installation. No clean/data-only fallback or forced reboot on failure is added.

## Verification and owner follow-up

Core and read-only CLI regression suites remain required. Client GUI checks
cover approved wording, original embedded-logo identity, source selection,
consent/cancellation and full card invalidation during repeated live resize.
DrawToBitmap/forced Refresh is not used to prove resize invalidation. Hosted
Server CI compiles GUI checks; client execution is verified locally separately.

No real Windows Setup or OS upgrade is run on the development machine/CI.
Owner follow-up: repeat the originally reported drag-resize/maximize case on the
affected machine, including its DPI/display settings. Local regression coverage
does not prove every graphics driver, remote session or scaling configuration.

Previous RC releases and their assets remain unchanged. See [RC7 verification](RC7-TESTING.md).
