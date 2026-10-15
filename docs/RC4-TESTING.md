# RC4 verification and remaining field checks

## Automated checks

- Core smoke checks: Windows naming, Microsoft signature acceptance, unsigned
  rejection, collision filenames, browser fallback, extracted-folder paths.
- Transfer fault checks: interrupted stream, exact partial retention, validated
  ETag/Range resume, byte-for-byte result, trusted-checksum mismatch rejection,
  pre-cancelled request, completed-file collision protection.
- Storage calculations: ISO bytes plus Windows workspace; FAT32 and undersized
  volume exclusion; NTFS/exFAT eligibility.
- GUI checks: actual Start/Cancel event dispatch, restored Start after failure,
  cancellation token, duplicate-click prevention, Setup lockout, exclusive source
  choices, retained result status, custom-folder persistence during Refresh.
- Published EXE: version/help/all topics/completion parsing, status/scan JSON,
  invalid and repeated arguments, stdout/stderr redirection, exit-code propagation,
  and Windows GUI PE subsystem (no console allocation at GUI startup).

Run:

```powershell
dotnet run --project tests/PcNinja.Win11Upgrade.SmokeTests -c Release
dotnet run --project tests/PcNinja.Win11Upgrade.GuiChecks -c Release -- .artifacts/rc4-qa
```

The GUI checks deliberately choose an empty mounted-source selection to exercise
Start without ever launching Setup. Transfer tests use a synthetic in-memory
HTTP handler and small files, not the production ISO endpoint.
GUI checks run on a supported Windows client. GitHub-hosted Windows Server
runners skip the client-only GUI checks; Core and published-EXE CLI checks still
run in the release workflow.

## Field testing still required

- Complete upgrade on a Windows 10 and a Windows 11 test VM using an ISO entirely
  downloaded by RC4, separately from the previously successful manual ISO tests.
- Cancel/retry a real multi-GB download; verify the provider's actual ETag/Range
  behavior. If the provider cannot prove file identity, RC4 starts a new copy.
- Test UAC launch and no transient black windows on Windows 10 and Windows 11;
  verify interactive Ctrl+C from real PowerShell and CMD sessions.
- Test physical USB insertion/removal, recovery partitions, write protection,
  low disk space, and browser fallback under the real provider's throttling.

## Explicit limitations

- No full Windows upgrade or restart is executed during automated verification.
- DISM metadata readability does not prove every image data block is intact.
  A trusted expected SHA-256 is needed for independent whole-ISO verification.
- Complete-but-unverified or changed-server partials are kept; resume is used
  only for incomplete files with matching strong server validators. Old partials
  are never silently deleted. They can be removed manually when no longer needed.
- Authenticode verification itself is a native synchronous operation. It runs
  off the UI thread; cancellation takes effect when that check returns.
- Hidden/reserved partition classification is best effort if Windows rejects
  the read-only partition metadata query.
- The EXE is unsigned prerelease software, and the compatibility command remains
  experimental. Windows Setup remains responsible for the actual upgrade.
