# Session-scoped ISO validation reuse

## Scope and acceptance

Problem: Validate an exact local ISO, switch to Automatic with that same file in
the download destination, and preparation repeats SHA-256 and DISM enumeration.
The previous mount helper already checked attachment before mounting; its
"Mounting/locating" log did not distinguish reuse from a new attachment.

The shared Core preparation now retains **one successful source per engine
session**, not across application restarts. GUI manual ISO preparation and
Automatic's existing-file hook use it; CLI uses the same Core path. Included in
RC8 without additional CLI switches, popups, source choices or dependencies.

## Safe fast path

Reuse requires all of the following, checked again on each operation:

1. Exact normalized ISO path, file ID, volume serial, length, NTFS journal ID and
   latest file USN match the successful snapshot. Size/time/name alone never pass.
2. The journal still retains the exact matching V2 file/USN **CLOSE** record, with
   valid bounds before and after the read. Same-kind writes can coalesce before
   final close; a last-USN comparison without this gate is insufficient.
3. The exact ISO is still attached to the same device, volume ID and drive root.
   A drive letter alone is not an identity. Check this binding before and after
   fresh signature and selected/secondary-media answer-file safety checks.
4. Any supplied expected SHA-256 still matches the previously calculated actual
   digest. Cached image metadata is a read-only copy, not a mutable caller list.

File guards are read-only/share-read and **operation-scoped**. Snapshots keep no
handles while idle. Preparation is serialized within an engine; existing GUI
Start/Cancel and transfer exact-file locks remain. Cancellation or failure clears
the snapshot. A different source, invalidation, changed/absent proof, ejected or
rebound mount performs full preparation instead. No automatic detach is added.

The optimization is deliberately limited to canonical, non-reparse, local fixed
NTFS files with available journal access/evidence. Network, removable, exFAT,
unsupported formats, missing/aged-out/reset journals and non-admin environments
remain usable with full validation. This is normal filesystem change detection,
not a security boundary against privileged raw-volume/offline/kernel tampering.
A calculated digest without a trusted independent expected digest is an
identifier, not proof of every byte's authenticity.

## What remains full validation

- Signature and all existing answer-file safety checks still run on a cache hit.
- `StartUpgradeAsync` still re-reads image metadata, validates source/index,
  current compatibility, storage and attached-media safety immediately before
  Setup. No cached success or automatic consent can bypass these gates.
- Extracted folders, network folders and drive-only selection have no cache:
  their contents can change independently of an ISO file identity.
- Fresh downloads retain the existing single hash-before-promotion plus media
  validation and exact same-attempt handoff. That earlier hash does **not** seed
  the general session snapshot. A later first preparation can need full checks.
- The transfer hook only deletes a recorded app-owned file on proven checksum/
  length corruption. Cancellation, signature/safety/read/mount errors retain it.
  An already mounted corrupt file can still require manual detach before deletion.

Progress identifies checksum percentage and image X of Y; the cache hit says
"Reusing the unchanged ISO; rechecking signature and media safety...".
Mount logs now distinguish "Mounted exact ISO" from "Using already mounted exact
ISO". Late GUI progress from cancelled/replaced operations cannot overwrite status.

## Verification and remaining field tests

Safe fixtures use tiny files and fake media/HTTP only, never real Setup/mounts:

- `IsoValidationSessionChecks`: hits, changed identities/mounts/paths, expected
  digest, absent/unstable proof, immutable snapshots, cancellation, invalidation,
  environmental failure and concurrent requests with operation handle release.
- `IsoFileProofChecks`: synthetic native record/journal bounds and conservative
  failure cases; an optional real tiny-file native probe verifies unchanged
  lifetimes, restored-time same-size edits, replacement, coalescing and locks.
  It explicitly reports **SKIP**, not proof of success, if journal access is absent.
- `IsoMountCommandChecks`: child PowerShell with shadowed fake cmdlets verifies
  attached versus attach-if-missing behavior and exact Unicode/apostrophe JSON.
- Transfer fixtures exercise both offline recorded-pointer reuse and exact-name
  existing media, callback counts, actual digests/sidecars and deletion ownership.
- Client GUI and headless CLI suites retain consent, quiet/restart, image-cue,
  safety/result/branding/link coverage; portable CLI checks never start Setup.

Owner VM acceptance: Validate a local fixed-NTFS ISO that already exists at the
exact Automatic destination; switch modes and start. Confirm the session-reuse
log, no duplicate hash/image preparation and no new mount. Final prelaunch checks
are expected. Test Cancel/retry, late unsafe secondary media, source changes,
manual eject while idle and normal fallback on USB/network. A green fixture is
not evidence of a real ISO mount, successful migration or arbitrary eject locks.

## Native references

- [Change journal records and coalescing](https://learn.microsoft.com/en-us/windows/win32/fileio/change-journal-records)
- [Last file USN and invalid Reason fields](https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ni-winioctl-fsctl_read_file_usn_data)
- [Bounded journal reads](https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ns-winioctl-read_usn_journal_data_v1)

The original #20 test artifact intentionally excluded the separate #19 routing
work. RC8 consolidates merged #20 and #21; #21 supersedes #19's routing behavior.
RC7 and all older test/release binaries remain unchanged. This session mechanism
does not add automatic detach or weaken final source/answer-file checks.
