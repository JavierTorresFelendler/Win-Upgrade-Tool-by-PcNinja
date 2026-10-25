# V1 — WinUpgradeToolByPcNinja

First stable release candidate prepared from the field-tested Beta 5 code.
The GitHub Release page is authoritative for actual publication status.

- Public portable EXE: `WinUpgradeToolByPcNinja.v.1.exe`, technical version
  `1.0.0`. Future V1.1 maps to `1.1.0` / `WinUpgradeToolByPcNinja.v.1.1.exe`.
- Same tested Windows 10/11 x64 client upgrade flow, own matching ISO priority,
  manual ISO/mounted/folder sources, actual image inspection, language/edition
  gates, unattended-media checks, storage checks and explicit restart/quiet policy.
- Consent-based GitHub application updates, independent of Windows restart flags
  and external Setup. New verified version stays beside the running copy.
- After shown/painted GUI readiness authenticated to the exact new process, mark
  only the verified predecessor OLD-, then OLD.V1-/V2- on name collisions. No
  overwrite, deletion or broad file search. CLI without GUI relaunch retains it.
- Thank you to the Massgrave contributors for the alternative ISO catalogs;
  [full credits](https://github.com/JavierTorresFelendler/Windows-Upgrade-Assistant-by-PcNinja/blob/v1.0.0/docs/CREDITS.md). No activation scripts or licensing bypass integrated.
- Freeware use, not an open-source permission grant: see [LICENSE](https://github.com/JavierTorresFelendler/Windows-Upgrade-Assistant-by-PcNinja/blob/v1.0.0/LICENSE).
  Report security issues privately to **javier@pcninja.pro**; see [SECURITY](https://github.com/JavierTorresFelendler/Windows-Upgrade-Assistant-by-PcNinja/blob/v1.0.0/SECURITY.md).

## Move from a Beta to V1

Beta and stable channels stay separate. Download/open V1 manually once; do not
try to force `1.1.0-beta.5` to auto-downgrade or change channel to `1.0.0`. Future
stable versions compare their technical versions and use the short stable filenames.
Old Beta/RC GitHub assets are unchanged. There is no Program Files installation.

## Limits and acceptance

English GUI/CLI; matching manually supplied media can use other system languages.
Windows Server remains inspection/download/manual-command guidance only, not
app-driven Setup. Current EXEs are unsigned; backups and valid Windows licensing
remain the user's responsibility. See [V1 checks](https://github.com/JavierTorresFelendler/Windows-Upgrade-Assistant-by-PcNinja/blob/v1.0.0/docs/V1-TESTING.md).
Stable-to-stable update fixtures do not substitute for a later live stable release
update test. No Windows upgrade is required for the final packaging/name check.
