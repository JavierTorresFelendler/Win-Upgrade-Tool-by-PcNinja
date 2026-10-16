# RC5 work register

Owner: **JavierTorresFelendler**. Milestone: [RC5](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/milestone/1).

This is a planning/workflow PR, not the RC5 implementation. The existing RC4
runtime and published EXE are unchanged. The field-test findings below remain
open; do not treat a successful baseline build as a fix for them.

## Ordered implementation work

| Order | Task | Status and acceptance |
| --- | --- | --- |
| 1 | [#1 — Unattended-media safety](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/issues/1) | Pending. Block interfering selected/secondary ISO or USB; verify narrowly permitted deletion; rescan immediately before Setup; no clean-install fallback. |
| 2 | [#2 — Download transport and exact-file reuse](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/issues/2) | Pending. Windows 10 TLS-compatible automatic download; reuse validated exact filename; delete only a proven-corrupt owned download; remove obsolete Copy N logic. |
| 3 | [#3 — Two source choices](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/issues/3) | Pending. Automatic / existing media; ISO, folder and mount in one path; preserve refresh and cancellation; no new UI framework. |
| 4 | [#4 — Installation-image selection](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/issues/4) | Pending. Enumerate actual media; bind file/index; GUI choice and CLI help/validation; never assume a fixed Pro index. |
| 5 | [#5 — Mount readiness and Setup diagnostics](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/issues/5) | Pending. Bounded drive-letter wait and evidence-based outcome reporting; no unconditional success-code reinterpretation. |

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
- **C4 — Merge decision:** Javier explicitly requests merge after reviewing the
  current candidate. If any code changes after a test, rerun the relevant tests
  against the new commit. No automatic merge or required third-party review.
- **C5 — Release decision:** separate owner request, correct version and notes,
  tag and release workflow, download and checksum verification. The existing tag
  workflow is still RC4-specific; update its naming/notes and carry the 100 MB
  gate into release packaging before an RC5 tag is created. Do not push a future
  tag against the unchanged RC4 workflow.

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
