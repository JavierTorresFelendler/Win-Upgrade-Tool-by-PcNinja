# Standalone application updates: proposed design

Status: design only, 2026-10-18. The owner requested planning while testing PR #12.
No updater, live feed, stable release, version bump or replacement test EXE is
implemented/authorized by this document. Do not merge the Windows 10 POC merely
because one initial trial passed. Keep safe-media PR #9 separately deferred.

## Small first scope

- Check for a newer stable **application** version asynchronously when the GUI
  opens, with a bounded timeout/cache and a manual Check for updates action.
  Offline, missing-feed and rate-limit failures must not block normal tool use.
- Show available version/release notes in the existing one-screen interface.
  User chooses Update or Later. "Automatic" means discovery/download/handoff,
  not silent executable replacement or an OS upgrade started by an updater.
- Download the exact release EXE, verify SHA-256/size/version/product identity,
  then perform a user-approved portable replacement/relaunch. Preserve an old
  executable for recovery. Never add MSI installation, a service, scheduled task,
  driver, telemetry or developer GitHub credentials to this portable product.
- Add CLI check/install/help/JSON through the same service; proposed syntax and
  exit codes need review with implementation, not undocumented ad-hoc flags.
- Stable only initially. PR artifacts, draft releases and RCs are not a stable
  customer update feed. A future preview channel must be explicitly opt-in.

## GitHub publication contract

Reuse the manifest pattern already present in PcNinja WinUpdate V2 and Smart
Office Installer, adapted for this standalone .NET portable EXE, not copied
from another host's installation/replacement policy. A future `public-release/update-manifest.json` should
contain schema version, stable channel, full release version/tag, exact asset
name/URL, byte count, SHA-256 and release-notes URL. It does not exist here yet.

Use a **version-pinned** GitHub Release asset URL, not a mutable latest-download
URL that could change between manifest check and download. Publish a manifest
only after the corresponding release assets/checksums are uploaded and verified.
No feed read from a PR branch and no expired/authenticated CI artifact URL.
GitHub's latest-release API excludes draft/prerelease releases; do not equate
latest with the highest acceptable version. Always compare against the installed
version, reject older/equal candidates, and verify tag/manifest/binary agreement.

The release workflow currently accepts RC version syntax only, names RC assets
and always publishes a prerelease. Stable publication support must be developed
and tested separately before the owner authorizes a stable tag. Keep the merged-
main/tag/version/notes/checksum checks and 100,000,000-byte EXE cap.

## Implementation boundaries and safety

- One authoritative release-version source for GUI, CLI, assembly metadata,
  updater, user agent and packaging. Current RC strings are repeated across files.
  Compare release versions correctly; ignore `+commit` only as build metadata,
  never silently strip prerelease status or interpret a Git SHA as a version.
- Keep a separate small app-update service. Do not reuse the ISO transfer flow:
  its ISO filename/size, provider fallback and deletion rules are inappropriate
  for a portable EXE. Reuse safe HTTP/hash primitives where sensible, not policy.
- Pin the intended repository/product, require HTTPS and bounded responses,
  retain certificate validation and verify exact asset bytes before use.
  Hashes are integrity checks, not digital signatures. The current EXE is
  unsigned; signing/disclosure is an owner decision before first stable release.
  A signed version must not silently accept a wrong-publisher/unsigned successor.
  The ISO setup.exe Microsoft-only signature verifier is NOT the app verifier:
  PcNinja executables are a different publisher/product.
- Reject replacement during ISO transfer/hash/mount/DISM/preparation/Windows
  Setup, including concurrent CLI/GUI instances, using a shared operation gate.
  Recheck before handoff. Handle a pending OS restart, but do not permanently
  block solely on Last-Upgrade PreparationCompleted: Windows 10 POC history
  intentionally requires manual outcome verification even after reboot.
- Windows cannot replace the running EXE directly. A hidden, narrowly scoped
  helper must wait for the exact original process to exit, reverify its staged
  candidate and target, replace recoverably and relaunch the GUI **without any
  OS-upgrade arguments**. Add internal helper dispatch before public CLI/GUI
  startup/elevation handling; no public arbitrary-file-copy/command execution.
- Treat handoff files and paths as untrusted input. Elevated staging/requests
  must have restrictive ownership/ACLs, fixed product/target scope, freshness and
  operation identity; no symlink/path traversal, broad process kills, deletion
  or shell-built replacement commands. Verify again at replacement time.
- On read-only ISO, unwritable or network execution paths, do not overwrite the
  original source. Offer a user-approved local destination for the new app.
  Check staging/helper/candidate/backup space separately from OS workspace.
  Cancellation/failure leaves the old EXE intact. Clean only known owned files.
- Future WinUpdate integration keeps its own host/product updater. Loading OS
  upgrade Core in another tool must not cause this updater to replace that host.

## Delivery and probes

Complex boundary: process/file replacement under Windows. Before live releases,
probe it with harmless old/new fixture EXEs in a temporary directory, not Setup.
Acceptance: expected process exit, exact new version, no OS-upgrade launch,
old-file recovery and no unrelated path/process changes. On partial/failure,
stop the update and keep/recover the previous executable; do not force reboot.

1. Finish owner PR #12 image tests; merge only after feedback/approval.
2. Review this design; implement updater/version identity and stable packaging
   in scoped PR(s), with local fixtures and Windows client GUI/CLI checks.
3. Test no update, newer/equal/older/RC versions, malformed manifest, wrong hash/
   size/product/version, missing asset, offline/rate limit, cancelled/interrupted
   download, unwritable/ISO/renamed paths, locked files and active upgrade states.
4. Owner approves first stable publication. That first stable must **already
   contain the updater**; publishing a version without one cannot test self-update.
   Example labels only: `1.1.0` then `1.1.1` (not release authorization).
5. Keep a copy of the first stable on a disposable VM. Publish the higher stable
   with unchanged functional code if desired, but with correctly increased binary/
   tag/manifest versions and its own checksum. Observe detection, consent,
   download/verification, replacement/relaunch, new version and no repeated offer.
6. Verify the previous EXE/recovery remains usable and test an update failure.
   Existing published versions/assets remain immutable. Do not describe this
   successful app-update test as a Windows OS migration test.

## References inspected for planning

- [WinUpdate V2 manifest](https://github.com/JavierTorresFelendler/PcNinja-WinUpdateTool-V2/blob/main/public-release/update-manifest.json) and its WinUpdateTool.ps1 manifest/download/hash/signature flow.
- [Smart Office manifest](https://github.com/JavierTorresFelendler/pcninja-smart-office-installer/blob/main/public-release/update-manifest.json) and its separate InstallerForm update UI.
- [GitHub latest release](https://docs.github.com/en/rest/releases/releases#get-the-latest-release).
- [GitHub release-asset metadata](https://docs.github.com/en/rest/releases/assets#get-a-release-asset).
