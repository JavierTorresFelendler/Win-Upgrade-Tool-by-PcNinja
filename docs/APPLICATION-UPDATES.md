# Portable application updates

V1 includes the consent-based portable updater developed and tested through the
Beta line. Published RC/Beta assets stay unchanged. The owner confirmed a real
Beta 4 -> Beta 5 update, new GUI readiness, exact predecessor OLD rename and an
UpToDate check. That evidence does not establish every failure-path test or a
stable-to-stable lifecycle. Release authorization and remaining gates are recorded
in [Development status](DEVELOPMENT-STATUS.md).

## User flow

1. Check this project's GitHub release metadata at startup or via Check app updates.
   This check downloads metadata only, never an EXE.
2. A newer version opens an offer with target version, **REGULAR UPDATE** or
   **CRITICAL UPDATE**, a short publisher message and **View release notes on GitHub**.
3. **Not now**, Escape or closing the offer keeps the current tool. Opening notes
   opens the exact GitHub release page, not a download and not update consent.
4. Only **Download and run the new version** starts transfer/verification.
5. Save the version-named EXE beside the running tool, not the shell's working
   directory. A private hidden `.pcninja-updates/<operation-id>` staging directory
   is below that same folder. There is no Program Files destination or fallback.
6. Verify bytes/hash/size/product/version/publisher continuity. A verified helper
   waits for the exact old process to exit, then GUI opens the new EXE with NO
   arguments. After the initialized new window is shown/painted and acknowledges
   startup, the helper marks only this exact predecessor OLD-; bytes stay intact.

No installer, original replacement, numbered Copy N, service,
scheduled task, Windows Setup or forced restart is involved. Cancellation/failure
before handoff keeps the old window open. A helper/launch failure does not remove
the old EXE; open it again. A correctly verified existing exact-version file is
reused; a mismatch is not overwritten/deleted and causes an actionable refusal.
Readonly/unsupported locations fail, never relocate silently.

### Mark the predecessor OLD

After confirmed GUI startup, the helper best-effort renames only the exact
predecessor bound in the private update request to `OLD-<original filename>`.
No directory scan or deletion occurs. An existing OLD destination is never
overwritten: try OLD.V1-, OLD.V2-, etc., retaining the exact original basename.
There are at most 1000 deterministic name attempts; native no-overwrite rename
also protects against a slot being occupied between the existence check and move.
Locked/changed/unsafe/already-OLD files are retained and logged.
This cosmetic failure does not fail an otherwise successful update. Original
hash verification and no-overwrite rename share one source handle under anchored
ancestors. Never release a path lock, hash a filename and then rename by path.

GUI startup uses a bounded same-user random pipe; the helper verifies the native
client PID against the retained exact new process. The environment carries only
an opaque ACK channel, not a target path/command, and the GUI clears it. No command
line arguments or OS actions are replayed. Missing/late/wrong-process readiness
means no archive; old bytes remain available. CLI does not relaunch a GUI and
therefore leaves the predecessor name unchanged. Local status adds archiveStatus
and archivedPath. Published Beta 1/2/4 binaries are not retroactively modified.

## Version names and channels

Binary/product identity is authoritative, not a guessed filename. Internal build
output remains Windows-Upgrade-Assistant-By-PcNinja.exe; published assets are:

| Product version | Published filename | Update discovery |
| --- | --- | --- |
| 1.1.0-beta.1 | Windows-Upgrade-Assistant-By-PcNinja-V1.1-Beta1.exe | Beta only |
| 1.1.0-beta.2 | Windows-Upgrade-Assistant-By-PcNinja-V1.1-Beta2.exe | Beta only |
| 1.0.0 (V1) | Win-Upgrade-Tool-by-PcNinja-v.1.exe | Stable only |
| 1.0.1 | Win-Upgrade-Tool-by-PcNinja-v.1.0.1.exe | Stable only |
| 1.1.0 (V1.1) | Win-Upgrade-Tool-by-PcNinja-v.1.1.exe | Stable only |

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
launch. Existing files are never overwritten. Private handoff files are retained
for diagnosis; do not broadly clean the staging root or replay helper requests.

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
After helper completion, correlate `operationId` in local
`.pcninja-updates/Last-AppUpdate.json` and run the new EXE's --version. Status is
bounded/read-only and may be null/unavailable without folder access.
Every updater result has setupStarted=false and readyForRestart=false.

## Live acceptance and historical Beta notes

The owner-provided Beta 5 log confirms the Beta 4 handoff, new GUI startup,
`GUI ready=True`, `predecessor archive=renamed`, and a subsequent UpToDate check.
The OLD.V collision sequence also has native disposable-file test coverage;
do not describe every collision/cancel/offline/signing scenario as field-tested.

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
