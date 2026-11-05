# Product identity and portable download contract

- Display/GUI title: Windows Upgrade Tool by PcNinja, without a release suffix.
- Canonical GitHub repository: `JavierTorresFelendler/Win-Upgrade-Tool-by-PcNinja`.
- Header website: `www.pcninja.pro`, 11-point Segoe UI Semibold, linking to
  `https://www.pcninja.pro`. Keep the original single PcNinja logo and ISO links.
- Product version, not filename, drives update comparison. Full version/source
  commit remain in properties, startup logs and CLI version/status.
- Internal build output: Windows-Upgrade-Assistant-By-PcNinja.exe.
- Historical stable assets retain their immutable versioned names: V1
  (`1.0.0`) is `Win-Upgrade-Tool-by-PcNinja-v.1.exe`; V1.1 (`1.1.0`) is
  `Win-Upgrade-Tool-by-PcNinja-v.1.1.exe`; V1.2 (`1.2.0`) is
  `Win-Upgrade-Tool-by-PcNinja-v.1.2.exe`.
- New stable/Beta releases use the fixed public asset
  `Win-Upgrade-Tool-by-PcNinja.exe`, while RC release assets retain their
  historical/versioned mapping. Never rename or replace an already published
  GitHub asset.
- Previously published RC8/older asset names and bytes remain unchanged. Do not
  rename or overwrite historical GitHub assets.
- Each release pins its exact asset/size/SHA-256. Do not use an unbound latest
  EXE URL; resolve the verified manifest's exact fixed-name asset.
- The fixed-name active EXE is replaced only after the parent exits, its exact
  bytes are snapshotted and verified, and the same-directory replacement is
  atomic. After PID-authenticated GUI readiness, archive the predecessor using
  its verified four-part Windows FileVersion. If that name belongs to different
  bytes, add the predecessor SHA-256; identical archives are reused. Unknown or
  changed files, links and uncertain transactions are retained. A confirmed
  failed launch restores the exact predecessor; a possibly live successor is
  never stopped to force rollback.
- Published old clients keep exact versioned asset checks and old assets remain
  immutable. They cannot consume new fixed-name manifests; migrate by manually
  downloading/opening the fixed-name EXE once. CLI handoff does not relaunch a
  GUI or claim readiness.
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
