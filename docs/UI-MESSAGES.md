# RC5 user-message catalog

## Pending next release: manual Windows 10 POC

- Reuse the existing two source modes and image selector. The automatic label
  explicitly says Windows 11; manual browse accepts Windows 10/11 x64 media.
- Windows 10 image/confirmation warning: "Experimental Windows 10 path: build
  order is not checked. Windows Setup must allow keeping apps and files;
  otherwise stop. No clean-install fallback."
- Windows 11 -> 10: "The experimental Windows 10 target requires a Windows 10
  computer. Windows 11 to Windows 10 is blocked."
- Language/no-matching-media wording is target-neutral. All existing file safety,
  signature, space, image-index, restart, failure and Cancel messages remain.

## Pending next release: automatic flow and diagnostics

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

## Pending next release: language-aware download choice

These source changes are not yet included in the published RC5 executable.

- Automatic download is available only when the system-default UI language
  matches the automatic ISO (**en-US**); the disabled option explains both values.
- Other languages use the existing ISO/drive/folder choice. Both manual-download
  buttons stay available; PcNinja opens **https://iso.pcninja.pro**.
- Guidance: "For standard Microsoft Windows media, prefer Microsoft. For PcNinja
  media, use PcNinja ISO downloads. Select a matching language, then choose ISO...
  or Folder... above."
- Image mismatch: "Language mismatch: this Windows installation uses [system],
  but the image uses [image]. Choose Windows 11 media with the same system UI
  language; changing the keyboard language is not enough."
- Selected-image incompatibility is shown in warning color beside the image
  selector, not only in the bottom status line. Diagnostic details remain available.

## Published RC5 behavior

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
| Source changed | Source changed. Inspect images or Start to validate this media. | Inline |
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
