# RC5 verification and field tests

## Automated/local verification

- Core smoke tests: exact-name resume and ignored-Range restart, completed-file
  reuse with media revalidation, corrupt-owned-file replacement, preservation of
  unowned files, environmental errors and cancellation, and secure route fallback.
- Safety fixtures: selected writable answer file removal, secondary-media block,
  locked selected file block, late file detection, and unrelated file preservation.
  These tests do not claim to emulate an actual optical disk or physical USB.
- Image fixtures: DISM tool version versus image version, indexes, default language,
  matching N/edition/language/build rules, ambiguous and invalid indexes, source
  binding, first split-image path, and upgrade-only command arguments.
- History fixtures: process status alone is not success; target OS observation
  requires the changed target build/edition and no active Setup.
- Windows 11 GUI tests: two radio choices, unique highlight after switching,
  real Start/Cancel click handlers, Setup-running lockout, refresh retention of
  source/image/restart/download folder, and image reset on source change.
- Published-style portable CLI: version, complete offline help, JSON status/scan,
  invalid/repeated options, image-index parsing, PowerShell completion syntax and
  actual Tab completion, Windows GUI PE subsystem, and <=100,000,000-byte size.
- Build and PR/release checks must pass on the exact commit being published.

No test invokes Windows Setup or upgrades the development computer. A local
download dry run without elevation correctly returned exit 5. Hosted Server CI
builds the GUI checks but cannot replace Windows 10/11 client interaction tests.

## Live service probe

Bounded header requests forced TLS 1.2 with certificate checks enabled. The
short-link handshake failed; the direct provider redirected and returned HTTP
403. The secure fallback is covered synthetically, but current provider blocks
prevent claiming a successful real-provider transfer. Browser fallback remains
available without forcing direct-download mode. No provider link is published
in this test document.

## Owner field-test checklist

Use disposable VMs with snapshots first. Record the exact RC5 SHA-256, source
path/type, source image/index/language, current Windows edition/build and result.
Do not reuse RC4 results as proof that RC5 completed an upgrade.

- [ ] Windows 10 Pro to Windows 11 Pro, keeping applications and personal files.
- [ ] Windows 11 to a newer Windows 11 build, keeping applications and files.
- [ ] Automatic provider download on Windows 10 and Windows 11; browser fallback
      if blocked, then exact-file selection and successful upgrade.
- [ ] Retry a completed download: original path reused, no second ISO or Copy N.
- [ ] Interrupt/resume the real provider; safely restart if identity/range changes.
- [ ] Corrupt a recorded app download: exact file removed and downloaded again.
- [ ] Invalid unrelated/unowned ISO: preserved with a clear corrective message.
- [ ] Mounted secondary ISO with Autounattend.xml: blocked; detach, Refresh, retry.
- [ ] Read-only or locked selected source: blocked without bypass or forced detach.
- [ ] Writable extracted source: only its exact recognized answer file removed.
- [ ] Insert interfering USB after inspection: final gate blocks Setup.
- [ ] ISO, extracted folder, mapped/UNC source and mounted drive work through the
      same existing-media selector; refresh and replacement reset as documented.
- [ ] Matching and duplicate eligible images; invalid/incompatible CLI index;
      no automatic N/Single Language/edition/language downgrade or change.
- [ ] Green Start, red Cancel, cancellation outcome retention and no console flash.
- [ ] Low system space versus low download-drive space, FAT32 exclusion, connecting
      and removing USB, Refresh without restarting, restart policy retained.
- [ ] Setup failure: no forced restart. Transition result: unconfirmed until the
      next status check; observed target OS still requires checking files/apps.
- [ ] Verify 100%, 125% and 150% scaling on the test hardware.

If Setup asks for a target partition or clean install, cancel and preserve logs.
Never continue merely to see the outcome on a computer containing valuable data.

## Reproduce safe checks

```powershell
dotnet run --project tests/PcNinja.Win11Upgrade.SmokeTests -c Release
dotnet run --project tests/PcNinja.Win11Upgrade.GuiChecks -c Release -- .artifacts/rc5-qa
dotnet publish src/PcNinja.Win11UpgradeAssistant -c Release -r win-x64 --self-contained true -o .artifacts/rc5-publish
./tests/Verify-Cli.ps1 -ExePath .artifacts/rc5-publish/PcNinja-Win11-Upgrade-Assistant.exe -ExpectedVersion 1.1.0-rc.5
```

## Reference behavior

- Matching media is a prerequisite, not proof of a supported migration path.
  [Windows Setup command-line options](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/windows-setup-command-line-options)
  describes `/Auto Upgrade`, `/ImageIndex` and `/InstallFrom`.
- [Windows Setup automation overview](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/windows-setup-automation-overview)
  documents implicit answer-file search locations and Windows-owned caches.
