# RC4 user-message catalog

The interface is English. These strings are the RC4 baseline for owner review;
they can be revised independently of the underlying workflow in a later RC.
Routine failures use the main status area and an optional **View details** link.
Technical exceptions and paths stay in details/logs, not the primary popup.

| Situation | Primary wording | Presentation |
| --- | --- | --- |
| Administrator prompt declined | Run the app as administrator to continue. | Information popup |
| Final upgrade confirmation | Save your work before continuing. / Your computer may restart automatically. / Start the Windows upgrade? | Yes/No, default No |
| Final confirmation with manual restart | Save your work before continuing. / You will restart the computer manually. / Start the Windows upgrade? | Yes/No, default No |
| Setup stage finished, automatic restart | Setup finished this stage. Windows will handle the restart. | Information popup |
| Setup stage finished, manual restart | Setup finished this stage. Restart when you are ready. | Information popup |
| Close attempted during preparation | Use CANCEL to stop the current operation first. | Inline |
| Close attempted during Setup | Windows Setup is running. Please wait. | Inline |
| Cancel requested | Cancelling the current operation... | Inline; CANCELLING... button |
| Cancel completed | Operation cancelled. | Inline; Start restored |
| Read/write failure | Could not read or save the file. Check the drive and available space. | Inline + details |
| Insufficient Windows space | At least 20.00 GB must be free on the Windows drive. Only [free] is currently available. | Inline + details |
| No mounted source/folder | No mounted media or extracted setup folder was selected. Click Refresh or Browse folder. | Inline + details |
| Wrong trusted checksum | The ISO checksum does not match. Download a fresh copy before upgrading. | Inline + details |
| Signature failure | setup.exe does not have a valid Microsoft digital signature. | Inline + details |
| Network fallback | Download interrupted. Choose automatic download to retry, or select the browser download. | Inline + details; normal browser fallback |
| Unexpected application error | Something went wrong. Open View log for details and try again. | Error popup |

Other validation failures (unavailable drive, unsupported filesystem, missing
image, unreadable image metadata, unsupported OS) use the concise Core message
in the same inline area. **View details** contains the underlying diagnostic.
Windows-owned UAC and Setup screens are outside the app's message catalog.
