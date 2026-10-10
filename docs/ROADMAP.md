# Roadmap

## POC validation

- Test the download portal and ISO selection on Windows 10 and Windows 11.
- Test successful upgrades on supported and unsupported x64 hardware.
- Record Windows Setup exit codes and rollback behavior.
- Improve user-facing error messages from real test results.

## EXE application

- Replace the PowerShell interface with a C#/.NET desktop application.
- Add visible download and preparation progress.
- Add a clear restart policy and upgrade status screen.
- Store structured logs that can be exported for support.
- Package the application for simple installation or portable use.

## Private release candidates

- Add automated build checks.
- Create versioned private GitHub releases.
- Test upgrade and rollback paths on virtual machines.
- Add code signing before public distribution.

## Public release

- Complete security and privacy review.
- Publish documentation and limitations.
- Select an appropriate open-source or freeware license.
- Publish signed binaries and checksums.

