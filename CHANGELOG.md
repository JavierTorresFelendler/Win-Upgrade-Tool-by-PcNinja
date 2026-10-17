# Changelog

## Unreleased

- Add an owner-scoped manual Windows 10 -> Windows 10 POC using existing image
  selection. Detect the target family from image metadata; use `/auto upgrade`
  without `/product server` or `/eula accept`. Do not compare Windows 10 build
  order in this experiment; let Setup decide migration eligibility, with no
  clean/data-only fallback. Windows 11 target behavior and all other gates remain.
- Keep automatic download Windows 11-only; expose selected Setup profile and
  build-order policy in CLI diagnostics. Windows 10 outcome verification remains
  manual because shared-base metadata is not an exact feature-release identity.
- Show an OK-only manual-restart information dialog and persistent guidance after
  successful Setup preparation with automatic restart off. CLI prints the same
  action and adds `manualRestartRequired` to successful upgrade JSON; no forced
  reboot or false final-upgrade success is introduced.
- Ask once before automatic download/reuse plus upgrade; retain manual-media
  confirmation and stop unattended continuation on fallback/ambiguous selection.
- Reuse this automatic attempt's validated mounted source in GUI and CLI;
  retain final prelaunch safety/source/image/storage validation. No general
  manual Inspect/Start cache or automatic dismount cleanup is introduced.
- Decode bounded PowerShell CLIXML errors without showing serialized progress;
  retain raw diagnostics and explain mount discovery timeouts actionably.
- Keep preparation errors visible through Refresh; show the Windows drive's
  available/required/missing upgrade space. Log exact ISO-to-volume resolution.

- Disable automatic download unless the system-default UI language matches its
  en-US ISO; enforce the same policy in GUI, CLI and Core before any transfer.
- Keep matching manually supplied media available regardless of language, with
  explicit current/image language mismatch details beside the image selector.
- Use https://iso.pcninja.pro for manual PcNinja ISO selection; preserve the
  internal automatic transport and Microsoft's official manual alternative.
- Recommend Microsoft for standard Microsoft media; document language behavior
  and CLI availability diagnostics. Published RC5 assets remain unchanged.

## 1.1.0-rc.5

- Reduced the GUI to automatic download or existing installation media, sharing
  one ISO/folder/mounted/network selector without a new UI dependency.
- Added shared selected/secondary answer-file gates, narrowly scoped writable
  source cleanup, read-only/locked media blocking and final pre-launch rescan.
- Enumerated actual installation images with edition, language, x64 and build
  eligibility; bound `/ImageIndex` and `/InstallFrom` to the validated source.
- Added GUI image inspection/selection and CLI `--image-index`, validation image
  output, embedded documentation and completion-option parity.
- Reused exact-name validated completed downloads; replaced only corrupt recorded
  downloads, preserved unowned files and environmental/cancellation failures.
- Removed numbered-copy allocation and broad partial-file rediscovery. Resume or
  safe restart now uses the exact original-name partial and identity metadata.
- Added secure direct-provider retry for short-link transport/TLS failure, keeping
  normal certificate validation and the existing browser/Microsoft fallbacks.
- Waited up to 30 seconds for ISO drive assignment; retained hidden helpers.
- Persisted last-attempt diagnostics with post-restart build/edition observation;
  added CLI exit 8 for unconfirmed transition status instead of false success.
- Retained Start/Cancel, one-logo website branding, CPU/RAM/storage details,
  refresh, restart policy, shared Core, and independent standalone packaging.
- Added safety/image/transfer/GUI/CLI regression checks, protected-PR workflow,
  release tag/version/main-commit checks and a 100,000,000-byte portable EXE cap.
- Documented untested field cases and provider HTTP 403 observed during probing;
  no new real OS upgrade or successful provider transfer is claimed.

## 1.1.0-rc.4

- Restyled the single-screen GUI with one mascot, CPU/RAM/free-space details,
  a green Start button beside Refresh, and exclusive selected-source highlighting.
- Replaced the standalone header label with a clickable `www.pcninja.pro` link.
- Added extracted Setup folder selection alongside already mounted media in
  both GUI and CLI (`--mounted <drive-or-folder>`).
- Built the app as a Windows GUI executable to prevent black startup windows;
  CLI mode attaches to the caller's terminal and preserves redirected streams.
- Added red Cancel during preparation, duplicate-click protection, safe helper
  process cancellation, Setup-running lockout, and CLI Ctrl+C cancellation.
- Preserved cancellation/error outcomes across the final refresh and custom
  download folders across source rescans.
- Reserved ISO download space and Windows upgrade workspace together when they
  share a drive; rechecked Windows workspace immediately before Setup launch.
- Filtered unsupported filesystems, undersized drives and detected reserved
  partitions; applied the same validation to manually chosen download folders.
- Added 60-second header and 90-second stalled-read timeouts.
- Added guarded partial-download resume using strong ETag, Content-Range and
  length checks; retained original names, exact paths and numbered collisions.
- Added ISO SHA-256 calculation and optional trusted-hash comparison through
  CLI `--sha256` or GUI/CLI environment setting `PCNINJA_ISO_SHA256`.
- Limited browser fallback to network/service failures; local storage and
  checksum errors stay in the app. Cancellation never opens a browser.
- Shortened confirmation/success text and moved routine failures inline with
  optional details. Added an editable message catalog for future wording review.
- Made log write failures non-fatal; tolerated individual drives disappearing
  during source scans; rejected ARM64 for the x64-only release.
- Rejected repeated and command-inapplicable CLI options instead of ignoring them.
- Added transfer fault tests, GUI action tests and documented RC4 limitations.

## 1.1.0-rc.3

- Added a complete offline CLI manual embedded inside the standalone EXE.
- Added `help`, `help <command>`, `<command> --help`, and `help all`.
- Documented all source modes, validation checks, restart behavior, automation
  confirmation, JSON output, Setup arguments, and exit-code behavior in-product.
- Added `completion powershell` to generate native PowerShell Tab completion.
- Added completion for commands, applicable options, mounted Windows media,
  local ISO paths, and download destination directories.
- Kept PowerShell profile changes under user control; the application prints the
  completer but never silently edits `$PROFILE`.
- Added release-pipeline checks for embedded mounted-media help and completion
  script syntax.

## 1.1.0-rc.2

- Added a dark PcNinja-branded interface, mascot, window icon, and EXE icon.
- Added CLI support to the same standalone EXE with text and JSON output.
- Added `status`, `scan`, `validate`, `upgrade`, and `log-path` CLI commands.
- Added explicit CLI confirmation, `--yes`, `--dry-run`, and `--no-restart`.
- Added stable CLI exit codes for arguments, validation, download, elevation,
  cancellation, and Setup failures.
- Fixed Windows 11 systems being reported as Windows 10 by normalizing build
  22000 and newer as Windows 11.
- Fixed automatic downloads failing during final rename while the partial file
  was still open.
- Preserved the ISO filename supplied by the server or redirected URL.
- Added non-destructive `(Copy 1)`, `(Copy 2)`, and later collision naming.
- Kept the exact downloaded path throughout validation, mounting, and Setup.
- Added Content-Length completion checks and retained `.partial` files on error.
- Added DISM validation of `install.wim`, `install.esd`, or `install.swm`.
- Added a 20 GB Windows system-drive check.
- Added selectable download storage, USB refresh, free-space, write-access, and
  FAT32 checks.
- Added Refresh for newly downloaded, mounted, or connected media without
  restarting the application.
- Changed automatic-download failure fallback to the normal PcNinja browser
  page while retaining the Microsoft page as a separate option.

## 1.0.0-rc.1

- Replaced the PowerShell/CMD POC with a portable self-contained Windows EXE.
- Added one-screen WinForms interface with current Windows version details.
- Added automatic discovery of an ISO next to the application.
- Added automatic download through the configured private PcNinja endpoint.
- Added graceful fallback to Microsoft's official download page.
- Added selection of an existing local ISO.
- Added discovery and selection of an already mounted Windows ISO.
- Added visible download and preparation status.
- Added Microsoft Authenticode verification for `setup.exe`.
- Added optional `/noreboot` behavior controlled by the user.
- Preserved the known-good unsupported-hardware Setup arguments.
- Removed forced restart behavior and legacy script launchers.
- Split the reusable upgrade engine from the standalone user interface.
- Added portable EXE release automation and SHA-256 checksum generation.

## 0.1.0-poc

- Added the original PowerShell interface and CMD Setup helper.
- Added ISO selection, mounting, logging, and Setup launch validation.
