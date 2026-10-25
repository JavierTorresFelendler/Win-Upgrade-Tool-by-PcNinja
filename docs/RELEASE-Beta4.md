# V1.1 Beta 4 — independent application update trial

Test prerelease, not stable or RC9. The owner has the fixed Beta 3 PR test build
open in a VM and approved publishing its successor for a real update test.

- Runtime source is identical to the fixed Beta 3. Only version and release notice
  change. No new Windows-upgrade behavior or feature is introduced in this trial.
- Both parent and successor helper are independent of Windows restart flags and
  external OS Setup. No registry flag clearing, Windows update reset or reboot is
  required to update this application. Own active tasks still serialize safely.
- In the fixed Beta 3 choose **Check app updates**. The offer should show Beta 4,
  **REGULAR UPDATE**, a concise explanation and this exact GitHub notes page.
- Opening notes or choosing **Not now** must not download an EXE. Recheck and
  explicitly choose **Download and run the new version** for the update test.
- Verify that `Windows-Upgrade-Assistant-By-PcNinja-V1.1-Beta4.exe` appears beside
  Beta 3, the old window closes, Beta 4 opens without replayed arguments and the
  old EXE remains unchanged. Rechecking in Beta 4 should find no newer version.
- Windows restart/Setup flags must not cause the former updater refusal. Do not
  start a Windows upgrade to test this feature. Use an ordinary local writable
  NTFS folder and a disposable VM snapshot. Keep permissions and byte validation.

Beta 1/2 retain their old overly strict gate: a blocked old binary needs manual
bootstrap to the fixed Beta 3 test build or this Beta 4. Published old assets are
never replaced. Beta 3 was a PR test artifact, not a published release tag.

This is a regular demonstration, not a claim of a discovered security flaw.
EXEs remain unsigned. SHA-256 verifies release bytes, not an independently
authenticated publisher. CI/injected launch fixtures do not prove real VM
download/helper/relaunch success; that acceptance is still being tested.
