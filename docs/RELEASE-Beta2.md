# V1.1 Beta 2 — live application update trial

Test prerelease, not a stable release and not RC9. The owner has opened Beta 1
on a virtual machine and approved the next step of the demonstration.

- Only the application version and release notice change from Beta 1 runtime.
  There are no new Windows-upgrade features or changes to ISO/Setup behavior.
- In Beta 1 choose **Check app updates**. The offer should show Beta 2, a
  **REGULAR UPDATE** badge, a short explanation and this exact GitHub notes page.
- Opening release notes or choosing **Not now** must not download an executable.
  Recheck, then choose **Download and run the new version** to test the real flow.
- Beta 2 is saved as `Windows-Upgrade-Assistant-By-PcNinja-V1.1-Beta2.exe` beside
  Beta 1. The old EXE stays unchanged. No Program Files relocation, replacement,
  numbered collision copy, Windows Setup or OS restart is part of this update.
- After verification, the old window closes and the new version opens without
  replaying arguments. In Beta 2, checking again should report no newer version.
- Test in an ordinary writable local NTFS folder in a disposable VM. Unsupported
  or unsafe paths fail rather than silently moving the tool elsewhere.

Beta-only channel: RC8/main/stable remain unchanged. No security vulnerability is
claimed by this regular demonstration. Builds remain unsigned; SHA-256 verifies
release bytes, not an independently authenticated publisher. Live VM handoff and
relaunch acceptance is the purpose of this test, not a result already claimed.
