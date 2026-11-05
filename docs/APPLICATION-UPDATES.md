# Portable application updates

The updater is EXE-only. Historical assets remain immutable. New stable/Beta
releases use a fixed unversioned EXE name; predecessor archives use the verified
four-part Windows FileVersion and SHA-256 only when different bytes already
occupy the ordinary archive name. The owner-confirmed Beta 4 -> Beta 5 live
update is historical evidence for the old versioned updater, not acceptance of
this fixed-name transition. Release status and outstanding gates are recorded in
[Development status](DEVELOPMENT-STATUS.md).

## User flow

1. Check this project's GitHub release metadata at startup or via Check app updates.
   This check downloads metadata only, never an EXE.
2. A newer version opens an offer with target version, **REGULAR UPDATE** or
   **CRITICAL UPDATE**, a short publisher message and **View release notes on GitHub**.
3. **Not now**, Escape or closing the offer keeps the current tool. Opening notes
   opens the exact GitHub release page, not a download and not update consent.
4. Only **Download and run the new version** starts transfer/verification.
5. Save `Win-Upgrade-Tool-by-PcNinja.exe` beside the running tool, not the shell's
   working directory. A private hidden `.pcninja-updates/<operation-id>` staging
   directory is below that same folder while the update runs. There is no Program
   Files destination or fallback.
6. Verify bytes/hash/size/product/version/publisher continuity. The helper waits
   for the exact old process to exit, then verifies and snapshots its bytes. If
   the active path is already the fixed filename, replace it atomically; for a
   one-time versioned-to-fixed transition, install the fixed EXE beside the old
   one. GUI opens the successor with NO arguments. After the initialized new
   window is shown/painted and acknowledges startup, archive the predecessor
   under `Win-Upgrade-Tool-by-PcNinja-v<FileVersion>.exe`; a different file at
   that name uses a SHA-256-qualified name. Identical archives are reused.
7. Once the new GUI has been painted, it confirms a pending CLI update's
   transaction against its exact fixed-name EXE. An independent bounded cleanup
   worker then deletes only readiness-proven completed staging and verified
   recovery copies. Unknown, changed, reparse, hard-linked, locked, or
   transaction-uncertain content is retained and logged; legacy operation
   folders without sufficient completion evidence are retained.

No installer, original replacement, numbered Copy N, service,
scheduled task, Windows Setup or forced restart is involved. Cancellation/failure
before handoff keeps the old window open. A helper/launch failure does not remove
the old EXE; open it again. A correctly verified existing exact-version file is
reused; a mismatch is not overwritten/deleted and causes an actionable refusal.
Readonly/unsupported locations fail, never relocate silently.

### Same-name replacement and recovery

The helper records a private transaction before publication. It holds anchored
NTFS directory handles, rejects reparse points and hard links, verifies exact
hashes/ACLs, and uses an atomic same-volume replace while retaining displaced
bytes. The predecessor snapshot's Windows FileVersion is read from the exact
verified executable, not inferred from the release version. A normal archive is
`Win-Upgrade-Tool-by-PcNinja-v<four-part FileVersion>.exe`. If that path contains
different bytes, use `-<predecessor SHA-256>.exe`; if identical bytes already
exist, reuse them. Retire only exact legacy OLD duplicates after the versioned
survivor is re-verified. Unknown or modified legacy archives are never removed.

The GUI readiness result distinguishes confirmed startup, a definitely exited
failed successor, and uncertain/live successor state. Roll back only when the
successor is known gone; otherwise keep the target, snapshot and displaced
bytes. No process is force-closed. Archive failure after successful GUI startup
is reported and retried by the next cleanup worker, without undoing a working
update.

GUI startup uses a bounded same-user random pipe; the helper verifies the native
client PID against the retained exact new process. The environment carries only
an opaque ACK channel, not a target path/command, and the GUI clears it. No command
line arguments or OS actions are replayed. Missing or wrong-process readiness
never authorizes deleting a potentially live successor. CLI does not relaunch a
GUI; it leaves transaction recovery pending until a GUI startup is confirmed.
Local status includes archiveStatus, archivedPath and a deferred reason when
archival cannot yet be completed. A transaction still marked `publishing` or
`awaiting-gui` cannot authorize archival or cleanup; the normal GUI marks an
awaiting CLI transaction ready only after its fixed-name executable and private
predecessor snapshot are re-verified. Published Beta 1/2/4 binaries are immutable.

## Version names and channels

Binary/product identity is authoritative, not a guessed filename. Internal build
output remains Windows-Upgrade-Assistant-By-PcNinja.exe. Historical published
assets remain:

| Product version | Published filename | Update discovery |
| --- | --- | --- |
| 1.1.0-beta.1 | Windows-Upgrade-Assistant-By-PcNinja-V1.1-Beta1.exe | Beta only |
| 1.1.0-beta.2 | Windows-Upgrade-Assistant-By-PcNinja-V1.1-Beta2.exe | Beta only |
| 1.0.0 (V1) | Win-Upgrade-Tool-by-PcNinja-v.1.exe | Stable only |
| 1.0.1 | Win-Upgrade-Tool-by-PcNinja-v.1.0.1.exe | Stable only |
| 1.1.0 (V1.1) | Win-Upgrade-Tool-by-PcNinja-v.1.1.exe | Stable only |
| 1.2.0 (1.2) | Win-Upgrade-Tool-by-PcNinja-v.1.2.exe | Stable only |

Future stable/Beta manifests and publisher assets use
`Win-Upgrade-Tool-by-PcNinja.exe`; numbered RC assets retain their historical
versioned mapping. Do not change any old binary or relax its manifest/asset
checks. Since old binaries require the prior versioned asset filename, download
and open a fixed-name release manually once to migrate; then the new updater can
follow fixed-name releases. Never replace the immutable `v1.2.0` release asset.

Beta discovery checks up to three pages of five recent release records, bounded
by metadata/time limits, and selects the highest numbered published Beta. It
does not accept drafts, RCs or stable releases. Stable/RC binaries use only latest
stable; they never request the Beta list. Missing Beta/stable releases are normal
results. An offline/rate-limited/invalid check does not block normal tool use.

### One-time move from the Beta trial to official V1

Download the first stable V1 from the official GitHub Release and open it manually
once. The Beta trial used `1.1.0-beta.5`; the first official line starts at `1.0.0`.
The old Beta updater intentionally rejects both a channel switch and a lower
version. It cannot automatically bootstrap V1, and old binaries/assets are not
patched to make that happen. Manual bootstrap is not a downgrade bypass in the
updater. After bootstrap, stable V1 follows stable releases such as V1.1 normally.
Keep the old copy for recovery until the official build has been checked.

## Publisher-controlled regular/critical notices

Each published stable/Beta manifest includes `importance` (`normal` or `critical`)
and `message` (1-1200 plain-text characters). Source configuration is
`docs/updates/{version}.json`. The release workflow validates the fields and binds
them, version, exact EXE URL, size and SHA-256 to that release's manifest.

Critical gets a prominent colored badge and recommendation, not automatic or
mandatory download. The default button remains Not now. Do not invent a security
claim: explain the actual fix accurately; label critical-style trial notices as
demonstrations if used in Beta. Full details belong in that version's release
notes. The GUI renders plain text, not HTML/scripts.

Published manifests/assets are immutable. To change the notice or binary, publish
a new version; never replace an old asset. Stable/latest is never advanced by Beta.

## Safety, status and trust

Only ordinary fixed local NTFS paths are currently accepted. Copy off network,
ISO/removable/reparse/unsupported folders first. Administrator elevation does
not make readonly media writable. Staging directories are created atomically
with Administrators/SYSTEM ACLs; unsafe existing staging is refused, not taken over.
Reused files are copied into private staging and their ACLs revalidated/hardened;
CopyFile's source permissions must not leak into the privileged helper.
Exact candidate/ancestor read/no-write/no-delete handles remain held through
launch. Only the exact canonical current EXE may be replaced, atomically, after
the predecessor snapshot is hash-verified; other existing files are never
overwritten. Private handoff files are retained unless a verified transaction
proves cleanup safe; do not broadly clean the staging root or replay requests.

The machine-wide gate serializes this tool's own active operations across instances;
it never reads Windows restart flags or enumerates external Setup processes.
Pending Windows Update/CBS/file-renames, drivers, antivirus and external OS Setup
do not gate downloading/running a new version of this application. No Windows
restart, update reset or flag deletion is needed. The updater never force-closes
Windows work, replays upgrade arguments or reboots. An own active operation must
finish/cancel normally before closing its window for application handoff.
SHA-256 and fixed GitHub URLs bind bytes to the manifest, not an independent
publisher identity. EXEs are currently unsigned; a compromised publishing account
could change both manifest and binary. A signed current app requires the same
trusted signing certificate in its successor. Consent/critical styling does not
replace code signing or prevent all supply-chain compromise.

CLI: `app-update check --json`, `app-update status --json`,
`app-update run --yes --json`. The unreleased install verb is an alias, not an
installer. run needs explicit elevated --yes; CLI never opens a GUI, browser or
hidden prompt. APP_UPDATE_HANDOFF_STARTED is not proof the new process ran.
After helper completion, correlate `operationId` in
`%ProgramData%\PcNinja\Win11UpgradeAssistant\Last-AppUpdate.json` (beside the log;
older builds kept it in `.pcninja-updates`, still read as a fallback) and run the
new EXE's --version. Status is bounded/read-only and may be null/unavailable.
Every updater result has setupStarted=false and readyForRestart=false.

## Live acceptance and historical Beta notes

The owner-provided Beta 5 log confirms the historical Beta 4 handoff, new GUI
startup, `GUI ready=True`, predecessor OLD rename, and a subsequent UpToDate
check. That is not live acceptance of the fixed-name transition. Native
disposable-file tests cover four-part version archives, hash collisions, exact
identical reuse and legacy duplicate retirement; do not describe the new update
lifecycle as field-tested until owner VM acceptance is complete.

Use a disposable Windows 10/11 VM/snapshot for release-candidate checks: notes
and Not now must not transfer an EXE; consent must download/verify the exact
release and open its new GUI without replayed arguments. Check sibling paths,
predecessor naming/bytes, no-overwrite collision behavior and new-version status.
A stable-to-stable test needs a genuinely newer separately approved stable
release; opening V1 or checking metadata alone does not prove that lifecycle.
No Windows upgrade or OS restart is required to test application updates.

Historical Beta 1/2 contain the old overly strict OS-state gate; Beta 3 was an
unreleased PR bootstrap, and Beta 4/5 remove that dependency. Keep those published
assets immutable, rather than resetting Windows flags or rewriting history.
See [product identity](PRODUCT-IDENTITY.md), [LICENSE](../LICENSE),
[credits](CREDITS.md) and [security reporting](../SECURITY.md).
