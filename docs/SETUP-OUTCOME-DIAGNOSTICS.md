# Read-only Windows Setup outcome diagnostics

Process exit 0 is preparation evidence; `0x40010004` alone is not a successful
upgrade or a root-cause diagnosis. The recorded process outcome is preserved.

At GUI startup / CLI `status`, Core can return **RollbackObserved** with a
`SetupFailure` object containing the actual result/extended codes, phase,
summary and Windows log path. This is computed on read; Last-Upgrade.json stays
the recorded process-stage result. The existing GUI warning/details area shows
the explanation without a new popup. No user content preservation is inferred.

Evidence requirements:

- Setup is not marked in progress, and the original Windows build is running.
- The Panther session starts within -5 seconds/+2 minutes of the recorded launch
  and its header binds the exact media root, quoted image path and image index.
- Panther reports a terminal failed execution phase, followed by matching
  rollback result/extended codes and an explicit completed rollback, within
  bounded timestamp windows. Visible later sessions invalidate the association.
- Read at most 16 KiB of the Panther header and 512 KiB of each log tail. Accept
  UTF-8 or BOM-marked UTF-16. Missing, locked, overwritten, ambiguous, unsupported
  or truncated evidence stays unknown; this is not a universal SetupDiag parser.
- A Recovery-file-collision explanation is used only when both 0x800700B7 and
  the exact ReAgentOld.xml move collision are present. Other errors stay generic.
- Logs use machine-local timestamps. Time-zone changes or missing session headers
  can prevent correlation; do not guess. Windows 10 shared-base image version
  19041 does not prove an actual post-restart 22H2 feature build.

This feature never edits Windows logs, Recovery, the source image, boot settings
or Setup arguments; it does not dismount media, clean files, retry or reboot.
The helper has no arbitrary external command or log-import CLI. Old history JSON
remains readable. Core behavior is shared by GUI and CLI.

Verification uses synthetic correlated/stale/wrong-source/wrong-index/incomplete
log fixtures, bounded UTF-8/UTF-16 filesystem fixtures, active-Setup guards and
client GUI/read-only CLI checks. Real field reproduction remains owner testing.
