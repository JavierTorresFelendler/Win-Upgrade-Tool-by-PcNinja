# PR 31 VM acceptance

This is an unpublished test candidate for issue #30 and the fixed-name updater.
Use the portable EXE, SHA256SUMS.txt and BUILD-INFO.txt from the same successful
PR Actions artifact. Record the source commit, EXE hash, VM Windows build and
edition, account/elevation and display scaling with results.

## Single-instance GUI

Copy `Win-Upgrade-Tool-by-PcNinja.exe` into a fresh local folder in a disposable
Windows 10/11 x64 VM. Open it normally and accept the first elevation prompt.
Do not select installation media or start a Windows upgrade for these tests.

1. Open the EXE again while its window is normal, minimized, maximized and behind
   another application. Expect one persistent main window; a second launcher may
   exist briefly, then exit. Minimized restores; maximized stays maximized.
2. Repeat from a standard Explorer launch while the first window is elevated.
   Expect activation without another UAC prompt. Also repeat from an elevated
   shell. Windows may refuse foreground focus and flash the taskbar instead.
3. Open a modal file-selection dialog, then launch again. Expect the modal dialog
   to come forward. Cancel the dialog.
4. Launch several times rapidly, including while the first copy is starting.
   Expect one persistent GUI. Close it and immediately reopen; repeat after
   terminating only that VM test GUI in Task Manager. The next launch must work.
5. With the GUI open, run the following in PowerShell. It must complete and print
   JSON without opening another GUI or running Windows Setup:

   ```powershell
   & .\Win-Upgrade-Tool-by-PcNinja.exe status --json | Out-String
   ```

Report any duplicate persistent window, unexpected UAC prompt, lost activation,
hang, or failed relaunch. The guard deliberately fails open with a log entry if
its named objects are inaccessible; operation serialization remains independent.

## Updater evidence and remaining VM work

The candidate still reports product version 1.2.0. The public release feed cannot
offer it a newer fixed-name test build. Opening Check for updates is therefore
not an end-to-end test of this change. Do not change the feed, use a downgrade,
or bypass pinned version/channel/hash/signature checks to force it.

Elevated CI fixtures exercise actual filesystem snapshot/publication/rollback,
two repeated fixed-name replacements, archive reuse/collisions, readiness-state
cleanup and legacy duplicate preservation using harmless product-host bytes and
an injected GUI-readiness result. They do not launch the actual helper/successor.

A controlled newer-version lifecycle test remains needed before release: both
historical-name to fixed-name migration and fixed-name to fixed-name replacement,
actual helper READY/GO, native successor GUI readiness, failed/uncertain launch,
immediate GUI close, and cleanup after the helper exits. Expected fresh-folder
result is the current fixed-name EXE plus one predecessor named from its verified
four-part FileVersion; distinct historical backups must survive. Uncertain
transactions retain recovery data. Legacy staging without transaction proof is
also retained. Diagnostics are under `%ProgramData%\PcNinja\Win11UpgradeAssistant`.

Older published clients pin versioned release filenames and require a one-time
manual migration when the future fixed-name release is published. This test
candidate does not publish that release or alter existing assets.
