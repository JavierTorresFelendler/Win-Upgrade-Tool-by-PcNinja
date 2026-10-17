# Development handoff

Updated: 2026-10-17. This is sanitized project context, not a backup of Codex chats.

Reusable architecture, diagnosis and verification know-how lives in
[AI maintenance playbook](AI-MAINTENANCE-PLAYBOOK.md), independently of any
assistant's personal memory or skills installation.

## Published baseline and release hold

- Published baseline: `v1.1.0-rc.5`, source commit
  `408628f67469e263be69825faccd046994148046`.
- The owner is still testing RC5. Do not replace its EXE, merge pending work,
  change release tags or publish another release without explicit authorization.
- Approved source changes must still be committed/pushed to a development branch
  with a Draft PR. Local-only work is not an acceptable completed checkpoint.
- Product versions remain RC5 until a separate release decision. CI artifacts
  from a development commit are test builds, not a replacement RC5 release.

## Current work: language-aware automatic download

Branch: `fix/language-download-guidance`.

Implemented in source, not the published RC5 EXE:

- The configured automatic ISO language is `en-US`. Disable automatic download
  for a different system-default UI language, including `en-GB`, with a persistent
  explanation showing system and automatic-image languages.
- Keep matching manual ISO/mounted/folder/network media available without an
  English-only restriction. Existing edition/x64/Windows 11/build/safety gates
  remain; this does not imply every language has been field-tested.
- Apply the same gate in Core and CLI before a download; CLI validation failure
  uses exit code 3. Read-only status reports language, availability and reason.
- Manual PcNinja downloads use `https://iso.pcninja.pro`. Retain Microsoft's
  official manual option and recommend it for standard Microsoft Windows media.
- Keep the internal automatic transport separate and unchanged.
- Show both detected languages in a selected-image mismatch warning next to the
  image selector. Keep technical diagnostics available separately.

## Verification for this source change

Passed locally on Windows 11, without launching Windows Setup:

- Core smoke tests, including simulated transfers and existing media safety gates.
- Synthetic language-policy tests: en-US accepted case-insensitively; he-IL,
  en-GB, ru-RU, ar-SA, ja-JP and de-DE denied automatic download but matching
  manually supplied image metadata accepted. Unknown language denied.
- GUI checks: source exclusivity, Start/Cancel, mismatch text, manual alternatives
  remain available, and busy-state transitions do not re-enable a denied download.
- CLI version/help/status JSON/argument rejection/completion/subsystem checks,
  including language-policy status fields and manual-download guidance.
- Debug builds of GUI and Core test projects with warnings treated as errors:
  zero warnings/errors. No distributable release was produced.

GitHub PR validation passed for source checkpoint
`2d471e39e01c3fc16449fccd8032ceadf8a91205` in Draft PR #8, run
`39003768728`. A later documentation checkpoint has its own CI run; verify the
current head rather than treating this result as approval for another commit.
Owner field tests with matching non-English media are still required. Synthetic
metadata acceptance is not a real upgrade result.

## Recent owner evidence and pending follow-up

### Validation repetition and mount diagnostics (investigation only)

- The latest supplied test log records ISO hashing at 09:18:46, then failure at
  09:19:23 because the mount helper did not discover a volume drive letter within
  its 30-second poll. It does not record completed DISM image validation or a
  Windows Setup launch. This does not establish corruption or prove that Windows
  itself had no mounted volume; the helper may have failed to discover one.
- After Start at 09:19:53, the same SHA-256 was recorded again at 09:23:09. The
  supplied log ends there; the second attempt's final result is unknown. A hash
  without an independent trusted expected value is not full media validation.
- The screenshot shows `#< CLIXML` in the status line. Source review confirms
  that the helper throws raw PowerShell stderr on nonzero exit, then the GUI
  displays that raw exception message. The serialized output includes 96 progress
  records and the real drive-letter timeout error. This is not merely harmless
  progress misclassified as failure: an actual error is present.
- Source review confirms that Inspect and Start both hash a selected ISO and
  enumerate its images; StartUpgradeAsync repeats source/image validation before
  launch. Prepared state currently preserves selection, not a reusable validation
  result that avoids this work.
- Pending: separate a concise, actionable mount error from full diagnostic detail;
  suppress/parse serialized progress without hiding actual errors; improve logging
  of attached-image/volume discovery and the exact failed stage.
- Pending: design safe reuse of successful expensive validation within a session.
  Define file identity, mount/source binding and invalidation rules; file size and
  timestamps alone do not prove unchanged content. Never cache a failed attempt
  as success or remove final attached-answer-file/storage/source/index safeguards.
- Suggested acceptance cases: Validate then Start on unchanged media avoids
  redundant full hashing; changed/replaced/ejected media invalidates the result;
  a newly attached unsafe answer file still blocks; a drive-letter timeout gives
  a readable message rather than XML; both GUI and CLI follow the same rules.
- No application fix or replacement executable was produced during this report.
  The owner remains in field testing. Findings are recorded for a later authorized
  change; do not silently replace the test artifact being evaluated.

### Earlier findings

- Future safe-media preparation is recorded separately in
  [Draft PR #9](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/pull/9),
  branch `design/safe-media-preparation`, file
  `docs/design/SAFE-MEDIA-PREPARATION.md`. This is a design-only PR based on main;
  that file is not part of this branch yet. The proposed flow chooses a suitable
  local destination or lets the user choose manually, copies/validates into owned
  staging, removes known answer files from the copy only, relocates an assistant
  running from the ISO, then detaches the exact consented unsafe Windows ISO mounts.
  Unremovable USB/hypervisor/locked media remains a manual blocker. Final attached
  safety checks and explicit upgrade confirmation remain. No capability was
  implemented/tested by that design; do not replace the owner's PR #8 test EXE.

- The owner reported successful automatic ISO download on Windows 10 with RC5.
  Download success alone does not confirm the final upgrade from that file.
- A mounted original ISO passed signature/image enumeration, but Start was
  blocked by about 11 GB free on the Windows drive versus the tool's 20 GB gate.
  Pending UI improvement: show current/required/missing space prominently with a
  Refresh instruction; this storage-message improvement is not implemented here.
- A folder containing Windows 10 images was read successfully on Windows 11
  24H2. The first recorded rejection was a language mismatch. The old-target
  guard would also reject it. Pending improvement: aggregate rejection reasons
  instead of reporting only the first one.
- Multiple downloadable target OS versions were discussed, not implemented.
  Windows 10 targets, downgrade support, UI translations and catalog-driven
  multi-language automatic downloads are outside this change.
- Answer files on attached installation media remain a data-loss risk. Preserve
  the RC5 removal/blocking and final prelaunch checks; never add a bypass.

## Resume procedure

1. Read `AGENTS.md`, the AI maintenance playbook, this document, the current branch
   and its Draft PR.
2. Inspect current git status and CI; do not assume a previous test applies to a
   different commit or that a conversation summary is release authorization.
3. Keep owner-reported field evidence separate from automated/synthetic checks.
4. Implement only authorized work, then commit/push the checkpoint and update
   the same Draft PR. Merge and release are separate owner decisions.
