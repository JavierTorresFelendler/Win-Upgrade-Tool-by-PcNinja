# V1 final candidate checks

This is release closeout, not a new feature cycle. A PR artifact is a test build;
the official GitHub Release/tag/hash identifies the published product.

## Automated gates

- Core, headless CLI, Windows-client GUI and offline publication tests pass.
- Exact stable filename mappings: 1.0.0 -> v.1, 1.1.0 -> v.1.1, 1.0.1 -> v.1.0.1.
  C# updater and PowerShell publisher agree; old Beta/RC names stay unchanged.
- Fake GitHub discovery/manifest checks prove stable 1.0.0 -> 1.1.0 selects the
  exact short-name asset and rejects old-name/other-release substitutions. No
  channel/downgrade bypass or mutable latest EXE URL.
- Actual candidate EXE properties/version/help/status/completion and no-consent
  app-update behavior pass read-only checks, no Windows Setup or real app update.
- SHA256SUMS, BUILD-INFO, source commit, size below 100,000,000 bytes and current
  unsigned status are recorded. No logs/ISOs/personal data are committed.

## Owner VM final check

Copy the candidate to an ordinary writable local NTFS folder and open it manually.

1. Filename `Win-Upgrade-Tool-by-PcNinja-v.1.exe`; version 1.0.0 and PcNinja logo/site
   links are correct. Website is visibly larger (11pt) and still opens
   https://www.pcninja.pro. No installer/Program Files move or unexpected console.
2. Existing-source browsing, Refresh and image-inspection guidance remain familiar.
   No Windows upgrade needs to be started for this packaging/name check.
3. Check app updates: until the first stable is published, No stable release is
   expected; afterwards the same version should report current. Beta 5 must not
   be offered as a production update. No EXE download without consent.
4. Confirm the new name and documentation are acceptable; report problems with
   the candidate build identifier before the main merge/official publication.

The owner already reported Windows 10/11 client upgrade tests in multiple
languages, a real customer-machine test, Beta 3 -> 4 update, and Beta 4 -> 5
startup/OLD rename/current check. These observations do not prove every possible
hardware/media/Windows path or a not-yet-published stable-to-stable update.

After the accepted candidate, require successful PR checks, reviewed main ancestry,
an immutable v1.0.0 tag and draft/upload/verify-first publication. Independently
download all final assets and verify identity/hashes. Never replace older assets.
