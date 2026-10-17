# Development handoff

Updated: 2026-10-17. This is sanitized project context, not a backup of Codex chats.

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

Still required: PR CI for the pushed commit and owner field tests with matching
non-English media. Synthetic metadata acceptance is not a real upgrade result.

## Recent owner evidence and pending follow-up

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

1. Read `AGENTS.md`, this document, the current branch and its Draft PR.
2. Inspect current git status and CI; do not assume a previous test applies to a
   different commit or that a conversation summary is release authorization.
3. Keep owner-reported field evidence separate from automated/synthetic checks.
4. Implement only authorized work, then commit/push the checkpoint and update
   the same Draft PR. Merge and release are separate owner decisions.
