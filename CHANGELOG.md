# Changelog

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
