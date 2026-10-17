# Development handoff

Updated: 2026-10-17. This is sanitized project context, not a backup of Codex chats.

Reusable architecture, diagnosis and verification know-how lives in
[AI maintenance playbook](AI-MAINTENANCE-PLAYBOOK.md), independently of any
assistant's personal memory or skills installation.

## Published baseline and release hold

- Published baseline: `v1.1.0-rc.5`, source commit
  `408628f67469e263be69825faccd046994148046`.
- The owner authorized staged merging/development on 2026-10-17 until real VM
  tests require participation or work reaches Internet application self-update.
  PR #8 was squash-merged as `d2f671c263d90458eaace871b55209337ab62a24` after
  passing PR validation. Published RC5 EXE/tags remain unchanged.
- After owner-reported Windows 10 to early Windows 11 success and renewed merge
  authorization, PR #10 was squash-merged as
  `3ebd17e463bd090ae16b2a74797c7ddd047ef35f`. Its CI source checkpoint was
  `ec7830c084222cd89b91a83b55fb0c38ca3545ee` (run `39051844550`).
- Stop for owner testing of runtime changes before promoting unverified flows.
  Do not publish the first stable release or implement/activate self-update yet.
  The owner wants a first official version, then a distinct higher-version
  release to test Internet updating (it may contain no functional changes).
- Approved source changes must still be committed/pushed to a development branch
  with a Draft PR. Local-only work is not an acceptable completed checkpoint.
- Product versions remain RC5 until a separate release decision. CI artifacts
  from a development commit are test builds, not a replacement RC5 release.

## Current work: owner-scoped manual Windows 10 POC

Branch: `feature/windows10-target-upgrades`, based on merged PR #11
(`e356cca0d5cb71cddca550a807e1ff8815b62d82`). Published RC5 and prior test EXEs
are unchanged. PR #11 CI was `39058458738`, source `f4443b7`.

- The owner explicitly narrowed the experiment: reuse image selection, recognize
  Windows 10 and select the appropriate command, without determining older/equal/
  newer Windows 10 versions. This supersedes the stricter target-build plan;
  it does not authorize automatic media extraction, downgrade promises or clean
  installation. No offline-hive extraction/version resolver was added.
- Core recognizes client Windows 10 image metadata (10.0 builds 10240-19045),
  reuses the exact source/index and standard `/auto upgrade` profile without
  `/product server` or `/eula accept`. Windows 11 keeps the old profile and build
  comparison. Windows 11 -> 10 and unsupported families remain blocked.
- Windows 10 trials require manual ISO/mounted/extracted/network media. Shared
  download validation refuses Windows 10 images, including mixed media; the
  existing automatic source is Windows 11 only in both GUI and CLI.
- Preserve edition/UI-language/x64, signature/DISM, storage, answer-file and final
  prelaunch gates. Setup must approve keeping apps/files or refuse; no quiet,
  clean, data-only, disk selection or forced-reboot fallback is introduced.
- GUI shows the experimental policy beside the image and in manual confirmation.
  CLI help/validation/dry-run expose `setupProfile`, `buildOrderChecked` and a
  policy notice, with profile/order also in successful upgrade JSON.
- Windows 10 raw image build is not a verified feature-release target. Existing
  history fields remain, but Windows 10 attempts cannot automatically become
  TargetWindowsObserved. Exit 0 still means preparation only; verify manually.
- Local Core/GUI/CLI tests cover accepted older/equal/newer Windows 10 fixtures,
  cross-family and metadata rejection, exact WIM/ESD/SWM arguments/restart policy,
  automatic-source blocking, Windows 11 regression and confirmation warnings.
  No Windows Setup was run on the developer. CI and disposable owner VM tests
  precede promotion of this changed upgrade flow; keep its PR Draft for testing.
- Acceptance and rollback: [Windows 10 POC checklist](WINDOWS10-POC.md). Preserve
  an old known-good EXE and VM snapshot; report code/log evidence without posting
  raw customer data. Stable/self-update holds and deferred PR #9 are unchanged.

## Merged source: manual-restart guidance (PR #11)

- When Setup returns 0 with automatic restart disabled, show an information
  dialog titled **Restart required** with an OK button: "Upgrade preparation is
  complete. Restart your computer to continue the upgrade."
- Keep the same explanation in the readiness panel through Refresh, plus a
  waiting-for-manual-restart status. OK only dismisses the dialog; it never
  restarts the machine. Do not display this popup for failure, cancellation,
  unconfirmed transition or automatic-restart mode.
- CLI prints the same instruction for successful `--no-restart`; successful
  upgrade JSON adds `manualRestartRequired`. Existing exit codes and
  PreparationCompleted semantics remain: exit 0 is not final migration proof.
- Core policy tests cover both restart settings and success/failure/transition
  codes. Client GUI tests exercise the actual completion presenter with an
  injected information-dialog sink; no Setup or reboot is invoked. Existing
  Core/GUI/CLI regression suites and warnings-as-errors builds still apply.
- This is a presentation-only follow-up to the owner-tested flow; it does not
  change Setup arguments, launch, storage or answer-file gates. Keep the prior
  test EXE and published RC5 immutable; CI produces a separately identified EXE.

## Remaining scope and explicit deferrals

| Work | Current disposition |
| --- | --- |
| Automatic consent, download handoff and readable diagnostics | Merged in #10; broader source/language/automatic-download field matrix remains distinct from one folder upgrade |
| Manual-restart explanation | Merged #11; Core/GUI/CLI tests and CI passed |
| Windows 10 target upgrades and equal-version repair | Experimental manual POC on the current branch; no Windows 10 build-order comparison by explicit owner request; CI and VM acceptance pending, not yet in published RC5 |
| Safe ISO copy/clean/detach | Owner explicitly deferred; keep design-only PR #9 Draft for the future EXE shipped inside customized ISOs. Do not merge/implement it as part of this pass |
| General manual Inspect/Start validation reuse | Deferred pending reliable identity/invalidation design; no size/timestamp-only cache |
| Mount ownership/eject-lock cleanup | Investigation open; no proven locking process or general safe-detach fix |
| Internet application self-update | Separate owner checkpoint before first stable publication, then two increasing official versions for the live update test; no active updater/feed yet |

### Latest owner field evidence

- The owner confirms Windows 10 to early Windows 11 worked after a manual
  restart. The corresponding app record shows original build 19043, target
  22621 Professional, exact folder/SWM image index 1, `/noreboot`, and Setup exit
  0 after about 14 minutes. The initial impression of a crash was not supported
  by that attempt's app logs; root Setup preparation completed without auto-reboot.
- This is owner-reported completion, not independent confirmation of every app
  or file. It does not close the distinct earlier morning Appraiser crash.
  Do not conflate old Panther logs with this later attempt.
- An earlier Windows 10 target test first hit en-US/en-GB mismatch and unsafe
  secondary-media gates; later it reached the hardcoded Windows 11 target gate.
  A valid matching Windows 10 target still cannot run in the current product.
- The log also confirms successful selected-folder Autounattend.xml deletion.
  No raw VM logs, JSON or screenshots are committed to this public repository.

## Merged source: automatic consent and actionable diagnostics

Merged PR #10: `fix/automatic-consent-and-diagnostics`.

- Automatic mode asks once before transfer/reuse, covering validation, compatible
  in-place upgrade and the selected restart policy. A unique matching image then
  continues without a second assistant question. Cancel defaults to No.
- Consent is local to that attempt and exact returned source object; manual
  sources, a later Start, ambiguous images or changed restart policy require the
  normal confirmation. Browser fallback stops the attempt rather than carrying
  unattended consent into a later manual selection.
- Shared Core returns the exact successful download's validated mounted source.
  GUI and CLI consume it instead of immediately hashing/mounting/enumerating the
  download again. Final source/image/signature/answer-file/storage checks remain.
  Manual Inspect then Start caching is NOT implemented: timestamps/size alone are
  not sufficient identity evidence. Mount ownership/detach cleanup is NOT fixed.
- Decode bounded, DTD-disabled PowerShell CLIXML error records, suppress progress,
  retain raw stderr and exit code in details/log. A 30-second drive-discovery
  timeout explains Refresh/manual mounted-folder selection, not presumed ISO
  corruption. Log exact requested ISO and resolved volume. Helper working directory
  is Windows System32; this does not prove the unresolved eject problem is fixed.
- Failure and no-compatible-image explanations remain in a multiline warning
  panel through Refresh. Low-space guidance names the actual Windows volume,
  available/required/missing space and action, independently of ISO destination.
- Automated verification: Core smoke tests, bounded CLIXML/DTD/space fixtures,
  Windows client GUI consent/invalidation/real rejected Start/persistent-warning
  checks, existing language/selection/cancel tests and read-only CLI checks.
  Builds use warnings-as-errors. No Windows Setup was launched locally.
- Delivery/owner acceptance: see [automatic-flow test checklist](AUTOMATIC-FLOW-TESTING.md).
  A green CI compiles GUI checks but cannot replace their Windows client run or
  a disposable-VM upgrade. Owner feedback and merge authorization were received;
  untested matrix cases remain release-validation work, not implied passes.

## Merged source: language-aware automatic download

Merged PR #8: `fix/language-download-guidance`. The older notes below preserve
the investigation history; current implementation status above supersedes any
old statement that all future work remains unauthorized.

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

### New successful round and proposed delivery order

- The owner reports a completed further round from Windows 10 through Windows 11
  26H2 with successful results and is satisfied with the last test executable.
  Exact artifact identity and per-source coverage were not restated; verify before
  marking a particular PR accepted. Earlier failure investigations remain open.
- Requested next: one upfront consent for automatic download plus upgrade (manual
  source confirmation retained), Windows 10 target upgrades and equal-version
  repair using target-aware Setup arguments, and application self-update.
- [Next iteration plan](NEXT-ITERATION-PLAN.md) records source-backed behavior,
  acceptance cases and proposed PR order. The owner subsequently approved staged
  execution; PR #8 is now merged. Design-only PR #9 remains separate and does
  not implement its proposed runtime feature. Each checkpoint needs its own CI.

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
