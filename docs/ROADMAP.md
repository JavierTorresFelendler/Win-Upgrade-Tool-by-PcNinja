# Roadmap

## RC3 - self-documenting CLI

- Field-test global help, per-command help, the offline manual, and PowerShell
  completion from a clean machine.
- Verify mounted-drive and ISO-path completion when multiple sources exist.
- Decide whether a reviewed opt-in `$PROFILE` installer is useful after RC3.

## RC2 - standalone GUI and CLI

- Field-test automatic download through the current PcNinja endpoint.
- Prove that an automatically downloaded ISO passes validation and completes an
  in-place upgrade on clean Windows 10 and Windows 11 test machines.
- Test local ISO, mounted ISO, USB destination, low-space, removed-drive,
  interrupted-download, failed Setup, and `/noreboot` paths.
- Verify GUI layout at 100%, 125%, and 150% display scaling.
- Record Setup exit codes and rollback behavior from real machines.

## Standalone product

- Add resumable download support.
- Add an expected SHA-256 allowlist when a stable trusted source publishes one.
- Add structured support-log export.
- Add code signing before public distribution.
- Add localization after the English workflow is stable.

## Integration into PcNinja WinUpdate Tool

- Reference or package `PcNinja.Win11Upgrade.Core` as a reusable component.
- Keep the standalone application and integrated update-tool experience as
  separate products and release artifacts.
- Let WinUpdate Tool own navigation and presentation while Core owns source
  validation, ISO mounting, and Setup execution.
- Reuse PcNinja visual assets without copying the standalone WinForms screen.

## Public release

- Complete security, privacy, and licensing review.
- Review ISO distribution and endpoint terms before public promotion.
- Publish signed binaries, checksums, documentation, and limitations.
