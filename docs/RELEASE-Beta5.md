# V1.1 Beta 5 — mark the previous version OLD after confirmed startup

Test prerelease, not stable or RC9. The owner confirmed the Beta 3 -> Beta 4
VM update succeeded and approved this successor through the Beta channel.

- In running Beta 4 use **Check app updates**, then **Download and run the new version**.
- Beta 5 is downloaded and verified beside the current EXE, as before. The old
  process exits and Beta 5 opens without replaying any command-line arguments.
- After the initialized window is shown/painted, a short-lived same-user random
  pipe sends an acknowledgement. The helper checks the actual native client PID
  against the exact launched process; process creation alone is not readiness.
- Only after that acknowledgement, the exact predecessor recorded in this
  update is renamed to `OLD-<original filename>`. For the normal Beta 4 test,
  expect `OLD-Windows-Upgrade-Assistant-By-PcNinja-V1.1-Beta4.exe` beside Beta 5.
- Original bytes remain intact. No deletion, broad EXE search, older-version
  sweep, overwrite or Copy N. Hash and rename use the same pinned file handle.
- An occupied OLD name advances to `OLD.V1-<original filename>`, then OLD.V2-,
  etc. Existing archives are never overwritten. Attempts are bounded to 1000 names.
- Locked/changed predecessor, permissions or other rename
  refusal leave the old filename unchanged and are logged without failing a
  successful application update. No ready acknowledgement means no rename.
- CLI handoff does not launch a GUI, so it leaves the original filename unchanged.
  Status adds `archiveStatus` and `archivedPath` to the local last-update record.
- Windows pending-restart flags and external Setup still do not gate app updates.
  No Windows upgrade, flag clearing, service reset or OS restart is part of this test.

Test on the VM from an ordinary writable local NTFS folder. Check the new window,
new version, exact OLD filename, original bytes and a subsequent no-newer-version
check. CI/pipe/temporary-byte fixtures are not the owner's live lifecycle result.
EXEs are unsigned; SHA-256 binds bytes but is not independent publisher signing.
Main, RC8 and all existing published assets stay unchanged.
