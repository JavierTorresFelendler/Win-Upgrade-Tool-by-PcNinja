# Roadmap

## RC1 — standalone EXE

- Validate the one-screen workflow on clean Windows 10 and Windows 11 systems.
- Test automatic ISO download from a network without an existing Microsoft login.
- Test local, mounted, successful upgrade, failed Setup, and `/noreboot` paths.
- Record Setup exit codes and rollback behavior from real machines.

## Standalone product

- Add PcNinja branding and application icon.
- Add code signing before public distribution.
- Add resumable download and stronger ISO identity/hash validation.
- Add structured support-log export.
- Add localization after the English workflow is stable.

## Integration into PcNinja update tool

- Reference or extract `PcNinja.Win11Upgrade.Core` as a reusable component.
- Keep the standalone application and integrated update-tool experience as
  separate products and release artifacts.
- Let the host update tool own navigation and presentation while Core owns
  source validation, ISO mounting, and Setup execution.

## Public release

- Complete security, privacy, and licensing review.
- Replace the private ISO endpoint if public redistribution terms require it.
- Publish signed binaries, checksums, documentation, and limitations.
