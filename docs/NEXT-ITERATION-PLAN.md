# Proposed next iteration and PR sequence

Status: updated 2026-10-18. PRs #8, #10 and #11 are merged. Windows 10 runtime
POC is in Draft #12; its first owner trial reportedly passed and another image is
under test. This branch adds app-update design only. Stop for owner VM
tests when needed. Stable publication and Internet self-update remain a separate
owner checkpoint; no published RC5 asset replacement is authorized.

## Latest owner evidence

The owner reports completing a further round of upgrades from Windows 10 through
Windows 11 26H2 successfully and is satisfied with the last test executable.
The exact artifact/commit and per-scenario source types were not restated; confirm
the tested checkpoint before treating this as acceptance of a particular PR.
Do not erase the earlier Appraiser crash or mount/eject findings: successful
later upgrades do not establish their original root causes or resolve every case.

## Approved staged delivery sequence

| Order | PR/work item | Acceptance boundary |
| --- | --- | --- |
| 1 | PR #8 merged as the next source baseline | Completed; squash d2f671c; published RC5 unchanged |
| 2 | Automatic-flow PR #10 and manual-restart PR #11 | Both merged after CI; retain the separate recovery checkpoints |
| 3 | Windows 10 POC in Draft PR #12 | Runtime source bdaee5e; first owner trial reportedly passed, another image under test. No intra-Windows-10 build-order comparison by owner request; no clean fallback |
| Deferred | Design PR #9 safe-media preparation | Owner explicitly postponed this for the future in-ISO EXE; keep Draft, no runtime change or merge in the current pass |
| 5 | Application self-update design | Proposed in design/APPLICATION-UPDATES.md; no runtime/feed/release change. Implementation and live version-pair test require their own owner checkpoint |

Do not wait for every future feature to merge a useful tested improvement. Each
runtime PR gets CI and a commit-identified test EXE; changed upgrade flows need
owner VM tests before promotion. A small presentation-only follow-up can be
checked with local client GUI tests without rerunning Setup on the developer.
Release tags/RC publication remain separate explicit decisions. PR #9 currently
contains design only, so its green CI does not test the proposed runtime feature.
If validation caching or mount cleanup needs a larger safety redesign, split it
from the small confirmation/diagnostics PR rather than hiding untested behavior
inside a usability patch. Self-update design may proceed while field tests run,
but must not change the executable currently being evaluated.

## Automatic mode: one upfront assistant confirmation

- Capture an immutable operation intent at Start, before any download. Show that
  consent covers downloading, validating and starting the compatible in-place
  upgrade, plus the selected automatic/manual restart policy. Default Cancel.
- After successful download/validation and a unique compatible image selection,
  continue without a second assistant Start question. Preserve cancellation until
  actual Setup launch. Do not add `/quiet` merely to hide this assistant's dialog.
- Manual ISO/folder/mounted-media mode keeps its current post-validation review
  and final confirmation. No global confirmation bypass and no third source mode.
- The current GUI switches `_existingMedia.Checked` after a download: that display
  state must not erase the original automatic intent or grant automatic intent to
  a later manual source. A boolean tied only to the current radio is insufficient.
- Browser fallback, changed source/target/restart policy, ambiguous or incompatible
  indexes, unsafe media, insufficient space or failure cancels unattended
  continuation. Require the relevant corrective action/new consent, not a guessed
  image, warning bypass, clean install or forced reboot.
- Unsafe-ISO detach consent remains scoped to its device list; upfront upgrade
  consent does not permit arbitrary dismounts. Windows Setup may still need user
  intervention for errors; this proposal removes redundant assistant prompts,
  not every possible OS-owned dialog.
- Tests: consent before transfer, Cancel causes no transfer/Setup, unique valid
  automatic image starts once with no second prompt, manual path still confirms,
  fallback/change invalidates consent, no double-click launches, safety still blocks.

## Windows 10 targets and same-version repair

**Historical stricter proposal below, superseded for the current POC.** The owner
subsequently requested only family detection and command selection, delegating
older/equal/newer Windows 10 migration eligibility to Setup. That scoped runtime
is in Draft PR #12, not this documentation branch. Do not implement the old
lower-build rejection/exact-version resolver from the following planning text
as if it were still the current requirement. Windows 11 -> 10 remains blocked.

There is no Windows 10 22H1 release. The proposed first Windows 10 target is
22H2 (build 19045), using matching client x64 edition/system-UI-language media.
Windows 10 Home/Pro normal support ended in October 2025; ESU eligibility is
separate. Offer this as an explicit advanced upgrade/repair choice with lifecycle
information, not as the default supported destination for every user.

The target media determines the Setup profile, not just the installed OS. Both
Windows 10 and 11 report NT version 10.0; major-version strings alone cannot
select the profile. Validate actual client image build plus Setup version.

Conceptual Windows 10 command, shown for review only (do not run on the developer):

```cmd
setup.exe /auto upgrade /dynamicupdate disable /compat ignorewarning
```

Do not add `/product server` for Windows 10 targets. `/EULA accept` is documented
as new with Windows 11 and should not be copied blindly into older Setup commands.
Bind `/imageindex` and `/installfrom` to the actual validated file/index as today;
append `/noreboot` only for the user's manual-restart policy. Preserve the existing
experimental Windows 11 profile until separately changed and tested. Current
Core rejects image builds below 22000, so a command-only edit cannot enable this.

Initial matrix to validate: 19043/19044 to 19045, 19045 to equal-feature 19045
repair, existing Windows 10 to 11 paths and equal-feature Windows 11 repair.
Reject Windows 11 to 10, lower feature builds, mismatched edition/language/x64
and unsupported Server/LTSC paths. Same-version repair is a deliberate valid
operation, not an available-update indicator; Setup must still offer apps/data
preservation. Capture feature build and servicing revision without assuming that
equal release labels prove compatibility or that an unchanged build proves repair
success. Let Setup compatibility establish migration availability; never fall back
to `/auto clean` or DataOnly. Do not promise every older-to-newer path works.

Update eligibility, command building, history/reporting, GUI wording, CLI/help and
fixtures together in Core-driven operations. Automatic download remains the
configured Windows 11 en-US source; adding a Windows 10 catalog is separate work.

## Application self-update (separate from OS upgrade)

The owner requested planning, not activation while PR #12 is under test. See
[the scoped design and two-stable-release acceptance plan](design/APPLICATION-UPDATES.md).
The first official version must already include the future updater; only then
can it be tested against a higher official version. Version numbers in the design
are examples, not approved tags. No live manifest or release workflow is changed.

- Reuse the existing PcNinja release-manifest approach, adapted for this .NET
  portable product; do not copy MSI replacement behavior into a portable EXE.
- Check asynchronously, allow failure/offline use, compare complete release
  versions including prerelease ordering, and download only a newer explicitly
  selected release asset over HTTPS. Verify SHA-256 and signing policy honestly;
  a checksum does not turn an unsigned EXE into a signed one.
- Keep stable and opt-in preview separate. GitHub's latest-release endpoint
  excludes prereleases; RC detection needs explicit preview policy. Temporary
  authenticated/expiring PR artifacts are not a customer update feed.
- Start with an explicit user-approved verified portable handoff, keep the old
  file, and block app replacement during active copy/download/preparation/Setup.
  Never expose a developer GitHub token or run `git pull` on a customer machine.
- Test no update, newer/older/equal/RC versions, wrong hash, offline/rate limits,
  interrupted transfer, file locks, permission failures and preservation of the
  old executable. Publish a manifest only for an authorized actual release.

References: [Windows Setup options](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/windows-setup-command-line-options),
[Windows 10 release/lifecycle information](https://learn.microsoft.com/en-us/windows/release-health/release-information),
[upgrade paths](https://learn.microsoft.com/en-us/windows/deployment/upgrade/windows-upgrade-paths),
[GitHub latest release](https://docs.github.com/en/rest/releases/releases#get-the-latest-release).
