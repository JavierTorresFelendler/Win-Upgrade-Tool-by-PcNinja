# RC5 work register

Owner: **JavierTorresFelendler**. Milestone: [RC5](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/milestone/1).

The owner authorized RC5 implementation, protected-PR merge and prerelease
publication for further testing. The requirements below are implemented and
covered by local/automated tests. Field-test issues remain open until real
Windows 10/11, provider and physical-media evidence is supplied. See
[RC5 verification and field tests](RC5-TESTING.md); implementation is not stable
production approval. No development-machine Windows upgrade is performed.

## Ordered implementation work

| Order | Task | Status and acceptance |
| --- | --- | --- |
| 1 | [#1 — Unattended-media safety](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/issues/1) | Implemented; fixtures pass. Selected/secondary/locked/late answer-file gates, narrow cleanup and final rescan. Real ISO/USB field checks pending. |
| 2 | [#2 — Download transport and exact-file reuse](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/issues/2) | Implemented; transfer fault tests pass. Secure alternate route, exact reuse, corrupt-owned replacement and no Copy N. Provider returned 403 during live probing; actual Windows 10 transfer pending. |
| 3 | [#3 — Two source choices](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/issues/3) | Implemented; Windows 11 GUI action checks pass. Two exclusive choices, one path, browse/refresh/cancel retention, image reset. User scaling/USB/network tests pending. |
| 4 | [#4 — Installation-image selection](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/issues/4) | Implemented; metadata/eligibility/command fixtures and CLI help/completion checks pass. Actual multi-edition media upgrades pending. |
| 5 | [#5 — Mount readiness and Setup diagnostics](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/issues/5) | Implemented; bounded drive wait, persisted attempt and evidence-based reconciliation. Real delayed mount/Setup/post-restart field evidence pending. |

Detailed requirements are in the issues and [roadmap](ROADMAP.md). Safety comes
before convenience. Split implementation PRs by coherent task; do not conceal
unfinished work by closing an issue when only its plan has been committed.

## Checkpoints and decision gates

- **C0 — Frozen reference:** tag `v1.1.0-rc.4`, commit
  `2e65c1e83f729a03909f393666b6abc368b6ea94`. Preserve its release assets and tags.
- **C1 — Process preparation:** this branch adds the work register, workflow
  guide, PR template and PR checks. No application-code/version changes.
  Owner decides whether to merge process preparation separately.
- **C2 — Per-task implementation:** new branch/PR, focused commits, tests and
  explicit scope. Link the issue with `Refs #N` until acceptance is satisfied.
- **C3 — Candidate testing:** record exact source commit, Actions run, EXE hash,
  VM snapshot, source type and outcome. Keep a checklist of GUI + CLI, Windows
  10 + 11, automatic + existing ISO + folder + mount, cancellation, refresh,
  missing/low storage, corrupt download, interfering read-only and writable
  answer files, image-index mismatch, and failed Setup without forced restart.
  Never continue if Setup asks for a target partition or proposes a clean install.
- **C4 — Merge decision:** Javier authorized completing merge/publication for an
  RC to test next. If any code changes after a test, rerun the relevant tests
  against the new commit. No automatic merge or required third-party review.
- **C5 — Release decision:** owner authorization covers this RC publication.
  Version-aware naming/notes, reviewed-main ancestry and tag/version checks,
  100 MB cap, SHA-256/build provenance and downloaded-asset verification are
  required. Earlier tags/assets are preserved; no bypass of main protection.

## Evidence and limits

Owner-reported Windows 11 24H2 to 26H2 and Windows 10 Pro to Windows 11 Pro tests
passed after detaching the original VM installer ISO containing an answer file.
Known-good manually provided media does **not** prove automatic-download success.
CI verifies synthetic transfers, validation, packaging and read-only CLI entry
points, not an actual OS upgrade or preservation of personal files/apps.

Keep the portable artifact at or below **100,000,000 bytes**. No runtime download,
architecture rewrite, ISO extraction launcher or integration into WinUpdate Tool
is included in this process PR. Standalone and later integration stay separate.

For recovery, main-protection choices and the owner's step-by-step actions, see
the [GitHub workflow guide](GITHUB-WORKFLOW.md).
