# Application updates (development only)

Implemented in development Draft PR #13, not published. The unchanged RC8
release and older binaries do not have this feature. Development builds retaining
the RC8 version string are not replacement RC8 releases. No stable version/tag
or live update acceptance is authorized by this guide.

This updates **Windows Upgrade Assistant By PcNinja**, not Windows. It never
downloads an ISO, runs Windows Setup, upgrades the OS or restarts the computer.
There is no background service, scheduled updater or silent installation.

## Check and consent

The development GUI makes one bounded stable-release check after opening and
offers **Check app updates** for retry. Offline/rate-limit/verification failures
do not prevent normal tool use. An available link shows the version; clicking
it asks before download/handoff and displays the destination. Choose No to keep
using the current app. RCs, drafts and equal/older versions are not offered.
Only this project's pinned GitHub repository is checked; there is no alternate
feed/preview channel. A repository containing RCs only normally reports no stable
release yet.

CLI checking needs no administrator permission; installation requires an
elevated terminal and explicit `--yes`. See the [CLI guide](CLI.md#application-updates-development-only)
for commands, result identifiers and exit codes. CLI installation does not open
a GUI, prompt for hidden input or relaunch a successor process.

## Protected portable copy

The updated standalone EXE is saved at the fixed Windows Program Files location:

```text
%ProgramFiles%\PcNinja\WindowsUpgradeAssistant\Windows-Upgrade-Assistant-By-PcNinja.exe
```

If the current portable EXE is elsewhere, **that original stays untouched**.
Use the new protected copy afterward; an old desktop shortcut to the original
still opens the old version. If the current app already occupies the protected
destination, it is replaced only after its exact process exits. GUI handoff
closes the old window and reopens the protected copy without arguments. The CLI
returns a handoff result and exits; it does not reopen a GUI or repeat commands.

Installation requires ordinary fixed local NTFS paths, including the running
EXE. When running from an ISO, network/removable drive, non-NTFS volume or
reparse-point path, copy the app to an ordinary fixed local NTFS folder first.
The updater does not alter that source or offer an arbitrary target override.
Unsafe existing folder permissions, changed/locked files or insufficient staging
space cause refusal rather than permission takeover or forced replacement.

App staging space is separate from Windows upgrade workspace. Each attempt uses
a private `.updates\<operation-id>` folder beneath the protected product folder
for the candidate, verified helper and request. Administrators/SYSTEM control
that staging; ordinary users can run/read the final protected EXE, not modify it.
The helper revalidates identity and bytes immediately before applying the update.

## Busy state, trust and recovery

A machine-wide gate excludes concurrent PcNinja preparation/Setup and app-update
operations across instances. External Windows Setup, recognized pending-restart
conditions or unreadable safety state also block installation, with fresh checks
before handoff/apply. Resolve the Windows condition separately and retry; the
updater does not force-close processes, bypass the gate or force a restart.
Historical `PreparationCompleted` alone is not a permanent update blocker.

The download must match the exact stable release's pinned URL, size, SHA-256 and
AMD64 GUI/product/version identity. These are integrity/identity checks, not a
digital signature. Current released binaries are unsigned: trust rests on this
project's GitHub account, release and manifest. An attacker controlling publication
could change both the EXE and hash. A trusted signed running/installed app instead
requires the same signing certificate in its successor; signing/key rotation is
a separate release-policy decision.

Cancellation before committed handoff leaves the current app unchanged. The
helper waits for the exact parent process; a locked or changed target fails
closed. Replacing an existing protected copy retains `previous.exe` in that
operation's private folder, with verified restoration on handled apply/relaunch
failure. This is not a guarantee against every power-loss/storage failure.
Handoff/recovery files are deliberately retained rather than broadly deleted;
do not recursively clean `.updates` or manually replay helper requests.

`app-update install` success means **handoff started, not installation proved**.
After the helper finishes, run `app-update status --json` to inspect the bounded
local `Last-AppUpdate.json` result and run the protected EXE's `--version` to
confirm the actual version. Match its `operationId` to the returned handoff.
A missing/stale result is not success. Keep the
original portable copy available; if a helper fails, inspect the app log and
retained recovery state before retrying. Every updater CLI result has
`applicationUpdated=false`, `setupStarted=false` and `readyForRestart=false`:
the already-returned parent cannot certify later replacement or authorize reboot.

## Acceptance still pending

Offline/synthetic checks do not establish end-to-end Internet updating. Final
acceptance requires a disposable VM with two separately approved stable versions:
the first already contains the updater; the second has a higher exact binary,
tag and manifest version. Verify discovery/consent, verified bytes, parent/helper
handoff, actual protected-copy version and GUI reopen, no repeated offer, original
outside copy preserved and failure/recovery cases. Confirm no Setup, OS upgrade,
reboot or argument replay. Neither stable publication nor this final VM acceptance
is part of the current development PR. See the [implementation contract](design/APPLICATION-UPDATES.md).
