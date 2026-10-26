# Product identity and portable download contract

- Display/GUI title: Windows Upgrade Tool by PcNinja, without a release suffix.
- Canonical GitHub repository: `JavierTorresFelendler/Win-Upgrade-Tool-by-PcNinja`.
- Header website: `www.pcninja.pro`, 11-point Segoe UI Semibold, linking to
  `https://www.pcninja.pro`. Keep the original single PcNinja logo and ISO links.
- Product version, not filename, drives update comparison. Full version/source
  commit remain in properties, startup logs and CLI version/status.
- Internal build output: Windows-Upgrade-Assistant-By-PcNinja.exe.
- Official stable download naming is separate from the unchanged internal build
  basename: V1 (`1.0.0`) is `Win-Upgrade-Tool-by-PcNinja-v.1.exe`; V1.1 (`1.1.0`) is
  `Win-Upgrade-Tool-by-PcNinja-v.1.1.exe`. A nonzero patch remains unambiguous, for
  example `1.0.1` uses `Win-Upgrade-Tool-by-PcNinja-v.1.0.1.exe`.
- Beta/RC assets retain their legacy naming, for example
  `Windows-Upgrade-Assistant-By-PcNinja-V1.1-Beta5.exe`; the short stable names do
  not authorize renaming or replacing a historical GitHub asset.
- Previously published RC8/older asset names and bytes remain unchanged. Do not
  rename or overwrite historical GitHub assets.
- Each release pins its own asset/size/SHA-256. Do not use a mutable latest EXE URL;
  resolve the verified manifest's exact versioned asset instead.
- New versions stay beside the running EXE. No Program Files relocation or
  original replacement. After PID-authenticated new GUI readiness, the exact
  predecessor is best-effort marked `OLD-`, then `OLD.V1-`, `OLD.V2-`, etc. on
  collisions. Existing archives and file contents are never overwritten/deleted.
  CLI without GUI relaunch keeps the old filename unchanged.
- Stable/RC builds discover stable only; Beta builds discover numbered Beta only.
  Beta publication never changes latest stable.
- Existing `1.1.0-beta.5` cannot switch itself to the lower-numbered stable
  `1.0.0` or to another channel. Download/open the first official V1 manually once;
  subsequent stable-to-stable updates use the normal verified updater. Do not add
  a downgrade/channel bypass for this one-time transition.
- Earlier test candidates used the former product/repository identity. Download
  and open the final-branded first stable manually; do not relax manifest product,
  repository or asset checks to migrate a candidate. Historical notes retain the
  names and evidence of the builds they describe; current usage follows this file.
- The product is free to use, not offered under an open-source license. Public
  source visibility is not a grant to modify/redistribute it; see [LICENSE](../LICENSE).

See [application updates](APPLICATION-UPDATES.md) for consent, notices, trust,
CLI and live acceptance. GitHub source ZIP/tar files are not executable assets.
