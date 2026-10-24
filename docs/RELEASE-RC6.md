# Windows Upgrade Assistant By PcNinja - RC6

Portable Windows x64 prerelease **1.1.0-rc.6**, published under owner approval.
Download **Windows-Upgrade-Assistant-By-PcNinja.exe**. The filename stays constant;
release/commit identity is in file properties, diagnostic logs, `version`,
`status --json` and BUILD-INFO.txt. SHA256SUMS.txt identifies the exact EXE.
No scripts or separate .NET installation are needed. Size cap: 100,000,000 bytes.

## Changes since RC5

- **Windows 10 manual targets:** reuse ISO/mounted/extracted/network media and
  actual image selection. Windows 10 uses `/auto upgrade` without `/product server`
  or `/eula accept`; Windows 11 retains its existing experimental profile.
  Intra-Windows-10 build order is delegated to Windows Setup in this POC, not
  treated as a promise that downgrade or every same-version repair is supported.
- **One upfront automatic consent:** download/reuse, validate and start a uniquely
  compatible upgrade without a second assistant confirmation. Manual media still
  confirms; browser fallback, ambiguity and changed intent stop continuation.
- **Less repeated automatic validation:** GUI/CLI reuse the exact validated
  source returned by that download. Final safety/image/storage checks remain.
- **Language-aware downloads:** automatic ISO is en-US Windows 11 only. Other
  system UI languages keep matching manual media, Microsoft and PcNinja ISO
  download choices. Mismatch guidance identifies both languages.
- **Readable persistent diagnostics:** decode PowerShell CLIXML, explain mount
  readiness timeouts, preserve failures through Refresh, and show actual required/
  available/missing Windows-drive workspace.
- **Manual restart instruction:** successful preparation with automatic restart
  off shows an OK-only information dialog and persistent guidance; CLI reports
  the same next step. The assistant never forces a restart after failure.
- **Rollback outcome evidence:** correlated completed Panther/Rollback evidence
  reports actual result/extended codes and phase via existing GUI warning/details
  and CLI status. Process exit and final Setup outcome remain distinct.
- **New product identity:** fixed executable name and version-neutral GUI title,
  with build/commit metadata retained for support and future update tooling.

## Verification and limitations

Owner reports stable staged Windows 10/11 upgrades, including **Windows 10 Pro
21H1 (19043) -> 22H2 (19045)** with working apps and personal files. Core fixtures,
Windows-client GUI checks, CLI checks and PR/release CI are separate evidence.
No actual OS upgrade is run on the development machine or CI.

This is an **unsigned experimental prerelease**, not a stable production release.
Back up important files and use a disposable VM/snapshot for risky tests.
Windows 11's undocumented `/product server` behavior is not a support guarantee.
Edition/system UI language/client x64 checks and Windows 11 -> 10 blocking remain.

Never continue if Setup unexpectedly asks for a partition or offers a clean
installation. The assistant does not format/select partitions, fall back to
clean/data-only installation, or auto-clean Windows Recovery files. Unsafe
answer-file media still blocks Setup until removed and rechecked.

Signature and DISM metadata checks do not prove every ISO/image byte is intact.
Missing/stale/incomplete rollback evidence stays unknown. Windows 10 shared-base
image build 19041 is not an independently verified target feature release.
Last-Upgrade.json preserves process-stage evidence; CLI status computes later
observations. Verify files/apps and the running OS after reboot.

Manual Inspect/Start validation reuse, mount/eject-lock investigation, popup/
wording cleanup and software self-update remain future work. ISO copying/cleaning
belongs to another product variation. No updater/feed or stable release is added.

Previous versions remain available under their original tags and filenames.
