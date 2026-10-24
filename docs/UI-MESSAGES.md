# User-message catalog

## Development PR: matching ISO and Server guidance

- Keep the same two source choices, logo and www.pcninja.pro branding. Add
  **Find matching ISO** beside the existing manual download buttons.
- Automatic availability explains the full-profile match, not just the language.
  All eleven en-US/x64 OEM editions remain eligible on supported client families.
- While searching: use the existing cancellable operation and status area.
  On browser launch, clear old source/image/consent state and persist the chosen
  filename plus instructions to finish downloading, select **ISO...**, then
  **Validate**. Explicitly say the assistant will not continue automatically.
- No exact match/timeout/changed HTML uses manual catalog guidance; do not claim
  a specific ISO was found or substitute English. Existing errors/details remain.
- Server shows **MANUAL GUIDANCE ONLY**, disables Start/quiet/restart and presents
  the manual-only advisory. Inspection asks for an explicit matching image, then
  enables **Copy upgrade command**. Copy rechecks media, previews the CMD command
  and says it was copied, not executed. Cancellation says nothing was copied.
- Same-feature media is called a repair candidate; older/equal revision is not a
  newer OS. 26H1 is separately explained. Exact behavior/limits are documented in
  [matched ISO downloads](MATCHED-ISO-DOWNLOADS.md). Published RC7 is unchanged.

## Development PR: ISO preparation progress

- Checksum stage: `Checking the ISO checksum... N%`.
- Image metadata: `Reading Windows image X of Y...`.
- Proven same-session reuse: `Reusing the unchanged ISO; rechecking signature and media safety...`.
- These update the existing status area, not a new popup. Cancelled/previous
  operation progress cannot replace the outcome. Final prelaunch revalidation,
  independent error details and existing image-selector cues remain unchanged.
- Unsupported or missing unchanged-file proof silently uses full checks; this is
  not an incompatible-media or corrupt-download error. See `ISO-VALIDATION-REUSE.md`.

## Post-RC7 follow-up: installation-image guidance

| Image-selector state | Empty-row cue |
| --- | --- |
| Source has not been validated / source changed | Validate a source first |
| Successfully read media has matching images but none is selected | Choose a Windows image… |
| Successfully read media has no matching image | No compatible Windows image |
| An actual image is selected | Original image name/index/language/architecture/version |

These are visual cues, not extra dropdown items. Existing unique-match automatic
selection and same-source selection retention remain unchanged. Inspect reports
the selected image when valid, asks for a choice when needed, or shows the actual
Core incompatibility reasons in the existing warning area. Keep both languages in
language-mismatch warnings and retain the experimental Windows 10 notice.
Image cues do not change independent error/details messages. A failed read/mount
is not evidence that enumerated images are incompatible. No new popup is added.
This is development PR #18 scope, not a change to the published RC7 binary.

## Post-RC7 feature PR: quiet Setup

- Add **Run Windows Setup silently** above automatic restart. Description:
  "Hide Windows Setup windows and allow automatic restart. Errors still appear
  in this assistant."
- When checked, select/disable automatic restart; when unchecked, restore the
  previous restart preference. Disable both choices during an operation.
- Initial automatic/manual consent adds "Windows Setup will run silently."
  Changed quiet intent invalidates the automatic-consent shortcut.
- Keep current GUI success/failure notices, source/image/storage/unsafe-media
  gates, branding and links. CLI never shows these GUI dialogs; see
  `CLI-AUTOMATION.md` for result identifiers and raw Setup error evidence.
- Published RC7 is unchanged. This is a separately identified PR test build.

## RC7: approved main-window copy

The owner approved the simplified main-window preview and its inclusion in RC7.
Published RC6 remains unchanged. This pass changes headings, descriptions and related control captions,
not popup flow, download providers, validation, Setup commands or CLI contracts.

| Area | Approved wording |
| --- | --- |
| Header subtitle | Upgrade Windows 10 or Windows 11 while keeping your apps and files. |
| Source heading | Choose your installation source |
| Source description | Download automatically, or select an ISO, drive or folder. |
| Automatic choice | Download Windows 11 automatically |
| Automatic description (available) | English (en-US). The ISO is checked before the upgrade starts. |
| Existing-media choice | Use an ISO, drive or folder |
| Existing-media description | Windows 10 or 11 media, on this computer or your network. |
| Image heading / action | Windows installation image / Validate |
| Image guidance | A matching image is selected automatically when possible. |
| Manual downloads | Use Microsoft for standard Windows, or PcNinja for PcNinja media. Choose the same language as your installed Windows. |
| Computer heading | Your computer & upgrade settings |
| Restart checkbox | Restart automatically when needed |
| Restart guidance | Turn off to restart manually after upgrade preparation. |

Remove the redundant INSTALLATION SOURCE and THIS COMPUTER kickers. Refresh,
PcNinja downloads and Microsoft downloads are shorter button captions. Preserve
START UPGRADE/CANCEL, ISO..., Folder..., View log and View details. The manual
restart summary says Automatic or Manual restart, not the internal /noreboot switch.

Keep the original embedded mascot, application icon, product name and clickable
www.pcninja.pro header. Public destinations remain https://www.pcninja.pro,
https://iso.pcninja.pro and https://www.microsoft.com/software-download/windows11.
The internal automatic provider and browser-fallback behavior are unchanged.

Unlike the illustrative preview, the application does not display invented
hardware values or a static Not checked yet result. Its validation row remains
the policy Required; actual outcomes remain in existing notices. Short summary
values avoid clipping at the supported minimum window width.
The following RC6 sections describe the published baseline where wording differs.

## Branding and outcome evidence

- GUI/product name: **Windows Upgrade Assistant By PcNinja**; footer **View log**.
  Version/build identity remains metadata and explicit diagnostics, not visible
  release branding. No additional popup is added by outcome reconciliation.
- Correlated completed rollback shows its summary, actual result/extended codes
  and phase in the existing persistent warning. **View details** includes the
  Windows log path. Do not label a process exit code as its root cause.
- Exact wording review follows owner-approved popup/flow cleanup. Existing
  upgrade consent and unsafe-media safety remain unchanged in this change.

## RC6: manual Windows 10 POC

- Reuse the existing two source modes and image selector. The automatic label
  explicitly says Windows 11; manual browse accepts Windows 10/11 x64 media.
- Windows 10 image/confirmation warning: "Experimental Windows 10 path: build
  order is not checked. Windows Setup must allow keeping apps and files;
  otherwise stop. No clean-install fallback."
- Windows 11 -> 10: "The experimental Windows 10 target requires a Windows 10
  computer. Windows 11 to Windows 10 is blocked."
- Language/no-matching-media wording is target-neutral. All existing file safety,
  signature, space, image-index, restart, failure and Cancel messages remain.

## RC6: automatic flow and diagnostics

- Successful Setup preparation with automatic restart off: information dialog
  **Restart required**, OK only: "Upgrade preparation is complete. Restart your
  computer to continue the upgrade." Keep this instruction in the readiness panel
  through Refresh and a waiting-for-manual-restart status. Dismissing it does not
  reboot. No such popup on failure, cancel, transition or automatic restart.
- Automatic confirmation, default No: destination; download/reuse, validate and
  start the compatible in-place upgrade; restart policy; experimental method,
  backup and save-work reminder. A unique matching result does not ask twice.
- Manual confirmation remains the selected image/index plus restart policy.
  Browser fallback or a later manual Start never inherits automatic consent.
- Mount timeout: "Could not detect the ISO drive within 30 seconds. Use Refresh,
  or select the mounted drive with Folder... if it is visible in Explorer.
  Otherwise remount the ISO manually. No upgrade was started."
- PowerShell failures show a short decoded error, never raw CLIXML/progress;
  details/log retain the original stderr and exit code.
- Low Windows-drive space shows actual drive, available/required/missing space,
  Refresh action and explains that moving only the ISO does not solve it.
- Errors/no matching image stay in a multiline readiness warning through Refresh,
  with optional diagnostic details. This is not a new wizard page.

## RC6: language-aware download choice

These changes are included in RC6; historical RC5 assets remain unchanged.

- Automatic download is available only when the system-default UI language
  matches the automatic ISO (**en-US**); the disabled option explains both values.
- Other languages use the existing ISO/drive/folder choice. Both manual-download
  buttons stay available; PcNinja opens **https://iso.pcninja.pro**.
- Guidance: "For standard Microsoft Windows media, prefer Microsoft. For PcNinja
  media, use PcNinja ISO downloads. Select a matching language, then choose ISO...
  or Folder... above."
- Image mismatch: "Language mismatch: this Windows installation uses [system],
  but the image uses [image]. Choose Windows media with the same system UI
  language; changing the keyboard language is not enough."
- Selected-image incompatibility is shown in warning color beside the image
  selector, not only in the bottom status line. Diagnostic details remain available.

## Retained baseline behavior

The interface and repository documentation are English. Routine failures use the
main status area with **View details**. Destructive-installation risk is a
blocking warning, not a Yes/No bypass. UAC and Windows Setup own their screens.

| Situation | Wording / behavior | Presentation |
| --- | --- | --- |
| Administrator declined | Run the app as administrator to continue. | Information popup |
| Final confirmation | Selected image name/index, restart policy, Save your work. Start the upgrade? | Yes/No, default No |
| Preparation finished | Setup finished this stage. Check the upgrade result after restarting. | Inline |
| Transition exit | Setup exited during the transition. Outcome not confirmed; reopen the assistant after restarting. | Inline |
| Unsafe answer-file media | Upgrade blocked: [exact path]. Customized installation media can trigger a clean install and data loss. Remove the interfering ISO/USB or answer file, then Refresh and try again. Windows Setup was not started. | Warning, OK only; no bypass |
| Unsafe-media status | Upgrade blocked. Remove the interfering installation media, then Refresh. See View details. | Inline + details |
| Close during preparation | Use CANCEL to stop the current operation first. | Inline |
| Close during Setup | Windows Setup is running. Please wait. | Inline |
| Cancel requested | Cancelling the current operation... | Inline; CANCELLING... |
| Cancel complete | Operation cancelled. | Inline; Start restored |
| No source | No existing installation media selected. Choose an ISO, drive or folder. | Inline + details |
| Source changed | Source changed. Click Validate or START UPGRADE to check this media. | Inline |
| Multiple eligible images | Choose an installation image above, then click START UPGRADE again. | Inline; same screen |
| No matching image | No matching image for [edition, language]. Choose matching Windows 11 media. | Inline |
| Existing download | Validating the existing ISO... | Inline; Cancel available |
| Wrong trusted hash | The ISO checksum does not match. Download a fresh copy before upgrading. | Inline + details |
| Unknown corrupt file | The existing ISO failed integrity checks but is not a recorded PcNinja download. Move or remove that exact file, then retry. | Inline; file preserved |
| Browser fallback | Browser download opened. When it finishes, use ISO... to select the downloaded file, then Start. | Inline + browser |
| Browser unavailable | Automatic download unavailable. Download manually, then use ISO... to select the file. | Inline + details |
| Read/write failure | Could not read or save the file. Check the drive and available space. | Inline + details |
| Signature failure | setup.exe does not have a valid Microsoft digital signature. | Inline + details |
| Previous attempt | Previous upgrade: [state]. [evidence-based message] | Inline after opening |
| Unexpected error | Something went wrong. Open View log for details and try again. | Error popup |

Storage, missing images, unsupported OS, image mismatch, DISM and drive-letter
timeouts use the Core message. Technical exceptions and paths remain in details
and logs. Owner wording review is still welcome.
