# Changelog

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
