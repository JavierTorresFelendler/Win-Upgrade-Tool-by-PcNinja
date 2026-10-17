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
  first snapshot ended there. The extended log now confirms the second attempt
  failed with the same drive-letter timeout at 09:23:44. A hash without an
  independent trusted expected value is not full media validation.
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

### Reported ISO eject failure requiring VM restart (unresolved)

- The owner reports the ISO remained mounted and could not be ejected until a VM
  restart. The extended log shows the two mount-discovery timeouts above, followed
  by selected-source access denied for an answer file on volume F at 09:24:47 and
  another answer-file block at 09:24:51. No Setup launch is recorded in that early
  segment; the later folder-source launch below is a separate attempt.
- The log does not map the failed helper's backing ISO path to volume F, record
  an eject attempt/result, identify a locking process, or show whether the app
  was closed or running from that volume. Do not claim an application handle leak
  or Windows Storage service fault as the proven cause.
- Access denied while deleting an answer file on read-only ISO media is not
  evidence that another process locked the volume. A mount remaining attached is
  likewise different from a demonstrated eject-blocking open handle.
- Source review finds no dismount/ownership cleanup after mounting/validation
  fails. The PowerShell helper is awaited and disposed on normal nonzero exit;
  that code path alone does not prove a helper process was left running.
- Pending: track exact ISO/device/volume identity, pre-existing versus app-created
  mounts, active consumers and operation stage; log bounded mount/eject results.
  Design safe cleanup/release of only app-created mounts after failure/cancel when
  unused. Never detach a user-preexisting mount on failure, interrupt Setup or
  force-close unrelated processes. Keep approved unsafe-media remediation scoped
  separately as proposed in PR #9.
- Regression cases: mount then failed validation, cancel after mount, repeated
  validation, pre-existing mount preserved, app launched from ISO versus local,
  and a real in-use mount. Distinguish clean release from a readable manual-action
  blocker; do not reboot the machine automatically to work around a locked image.
- The owner subsequently confirmed the ISO file was local and eject still failed
  after closing the assistant, requiring a VM restart. This does not establish
  where the EXE itself ran or identify a locking process. Still useful: executable
  location, exact eject error and live process/handle evidence before restarting.
  This remains an investigation, not a product-code fix.

### Windows Setup crash from a folder source (investigation only)

- A later attempt is corroborated by the assistant log and Last-Upgrade state:
  original build **19043** (Windows 10 21H1), target **22621** (Windows 11 22H2),
  target edition Professional, image index 1. The owner's recollection of a 21H2
  target is not what these recorded build numbers identify.
- Source was an extracted folder using `sources/install.swm`, not a mounted ISO
  for this launch. Signature and enumeration of three images passed. Enumeration
  repeated at 09:32:10, 09:32:21 and 09:32:23; Setup launched at 09:32:24 and
  returned at 09:33:09 (UTC+03:00). The repeated-enumeration finding is therefore
  visible independently of the earlier mount failure.
- The launched command used the existing upgrade-only switches plus image index 1
  and InstallFrom bound to the first SWM. The process exit was **-1073741819**, or
  **0xC0000005 (STATUS_ACCESS_VIOLATION)**. This indicates an invalid memory access
  termination, not a filesystem Access Denied result. The faulting component is
  not identified by the assistant log; do not blame a particular module or ISO.
- Stored state OutcomeUnconfirmed is not proof of success or completed migration.
  No forced reboot was issued. Do not infer Windows Setup never started from the
  earlier mount errors: this later attempt did start its process.
- SWM is supported by the documented InstallFrom option; the extension alone is
  not a reason to declare this command invalid. Still verify the complete split
  set and media consistency; metadata enumeration does not validate every part.
- Needed before a diagnostic retry: capture the test machine's
  `$WINDOWS.~BT/Sources/Panther/setupact.log` and `setuperr.log` from its Windows
  volume, plus Application Error / Windows Error Reporting details around
  09:33 (faulting application/module, exception code and offset). If staging logs
  are absent, inspect Windows/Panther and the crash event; absence is not success.
- Also request the SWM part names/sizes and Setup file version, without publishing
  raw logs or customer data. No argument change, ISO deletion, clean-install
  fallback or actual Setup execution is authorized by this investigation.
- References: [Windows 10 build mapping](https://learn.microsoft.com/en-us/windows/release-health/release-information),
  [Windows 11 build mapping](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information),
  [Access violation](https://learn.microsoft.com/en-us/shows/inside/c0000005),
  [InstallFrom](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/windows-setup-command-line-options#installfrom).

### Panther follow-up: crash narrowed to compatibility appraisal

- The owner supplied Setup's setupact/setuperr logs from the same failed attempt;
  they end at 09:33:08, immediately before the assistant's recorded process exit.
  The test VM is already undergoing another upgrade; do not request interruption
  or run diagnostic repair/cleanup while that upgrade is active.
- SetupHost identifies media Setup build 10.0.22621.1, host Professional x64 build
  19043, host/default media UI language en-US, the selected folder/SWM source and
  the requested automatic-upgrade command. This is not a logged language mismatch
  or a failure to launch Setup.
- At 09:33:08, CONX/Appraiser records ShimExceptionHandler exception 0xc0000005,
  repeated eight times with the same exception address. Immediately beforehand,
  application inventory reports completion (98 apps / 324 files), then the
  appraisal path crashes. Repeated lines are not proof of eight upgrade attempts.
  The reported module name is empty, so the precise faulting binary/stack remains
  unknown. This narrows the failing subsystem, not the final root cause.
- Earlier SdbpGetMergeSdbsDisabled [c0000034] messages are investigation clues,
  not independently demonstrated causes: the initial requirements appraisal
  subsequently completes with NoIssue. Similarly, the last driver-coverage line
  before an exception does not establish that driver coverage caused the crash.
- The supplied logs do not show a CRC/checksum failure, image decompression error,
  or a confirmed corrupt SWM part. They do not prove that media is intact either.
  File-not-found inventory messages for installed apps and early optional-footprint
  warnings must not be presented as proof of a damaged ISO without correlation.
- The existing signature/DISM/hash checks are distinct from Windows' later
  application/driver compatibility appraisal. Preserve both safety layers; do not
  remove Appraiser files, reinterpret the crash as success, or use clean-install
  fallback to get past it. No product code/arguments were changed.
- Future controlled comparison: reuse a VM snapshot with the exact same source
  system and a verified complete media set, changing one factor at a time.
  Capture compatibility XML and a crash event/dump if it recurs. Success with a
  different target Setup build alone cannot prove corruption of the old ISO.
- Dynamic Update was disabled in the recorded command. Microsoft documents that
  Dynamic Update can refresh Setup/compatibility data, but this does not prove
  that disabling it caused this crash. Any such experiment needs separate owner
  authorization and unsupported-hardware regression testing; do not flip the
  current default or silently modify the owner's ISO.
- Reference: [Compatibility scan logs](https://learn.microsoft.com/en-us/troubleshoot/windows-client/setup-upgrade-and-drivers/use-windows-setup-compatibility-scan-logs-to-identify-blocking-issues),
  [Setup Dynamic Update](https://learn.microsoft.com/en-us/windows/deployment/update/media-dynamic-update).

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
