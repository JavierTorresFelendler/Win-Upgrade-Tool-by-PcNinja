# AI maintenance playbook

This is repository-owned, reusable project know-how for any AI assistant or human
maintainer. It does not require Codex, proprietary tools or access to past chats.
Read `../AGENTS.md` first and use [Development status](DEVELOPMENT-STATUS.md) for
current work, test evidence and release holds. Verify these notes against source;
when behavior changes, update the relevant lesson and its tests together.

## Product intent

Help advanced home users perform an **in-place upgrade preserving apps and
personal files**, including an explicitly experimental unsupported-hardware path.
Never turn an unavailable upgrade into a clean or data-only installation.
This remains a standalone application. A future Windows Update tool integration
should reuse Core operations, not copy the standalone UI into another product.

Keep two main source choices on one screen: automatic download, or existing
ISO/mounted/extracted/network media. Preserve Refresh, exact source selection,
restart policy, visible warnings and Start/Cancel behavior. Prefer concise,
actionable user wording; retain technical detail in diagnostics. Repository/UI
text is English; discussion with the owner can be Hebrew.

## Code ownership map

All paths below are relative to the repository root.

| Area | Read first | Responsibility |
| --- | --- | --- |
| GUI | `src/PcNinja.Win11UpgradeAssistant/MainForm.cs` | Selection state, refresh, preparation/cancel, visible explanations |
| CLI | `src/PcNinja.Win11UpgradeAssistant/CliRunner.cs`, `CliHelp.cs` | Arguments, JSON/exit contracts, offline help/completion |
| Shared orchestration | `src/PcNinja.Win11Upgrade.Core/UpgradeEngine.cs` | OS/storage checks, mounting/validation, exact Setup launch |
| Transfer | `src/PcNinja.Win11Upgrade.Core/IsoTransfer.cs` | Exact-file ownership, partial/resume, integrity and reuse |
| Installation safety | `src/PcNinja.Win11Upgrade.Core/InstallationSafety.cs` | Selected/secondary answer files and final prelaunch gate |
| Image/language policy | `src/PcNinja.Win11Upgrade.Core/InstallationImage.cs`, `AutomaticDownloadPolicy.cs` | Actual image metadata, compatible selection and automatic-language gate |
| Outcome evidence | `src/PcNinja.Win11Upgrade.Core/UpgradeHistory.cs` | Persisted attempt and post-restart observations |
| Tests and delivery | `tests/`, `.github/workflows/` | Safe fixtures, client GUI checks, CLI checks and separate PR/release builds |

## Validated engineering lessons

### 1. Identify the exact source, not a likely filename

- Keep the original safe server filename, exact final path, selected source and
  source-specific image index. Never guess using `Windows*`, newest ISO, or a
  universal index for Pro/Home.
- Reuse recorded/exact-name downloads only after the existing integrity/media
  checks. Do not reintroduce numbered Copy N files as an alternative to reuse.
- Only proven-corrupt, recorded app-owned downloads may be removed for retry.
  Cancellation, unavailable media, unsafe answer files and signature-chain/network
  failures do not prove a user's ISO is corrupt.
- A trusted signature on root `setup.exe` does not validate every image byte.
  DISM metadata readability is not a full integrity scan; a calculated SHA-256
  without an independently trusted expected value is only an identifier.

### 2. Treat unattended media as a data-loss hazard

- A different attached ISO/USB can contain an interfering answer file even if the
  selected source is correct. Preserve attached-media checks, not just a check of
  the chosen setup folder.
- Remove only narrowly scoped writable answer files on the selected valid source
  under the existing safety rules. Block on other/read-only/locked unsafe media;
  explain the exact blocker and ask the user to detach it and Refresh.
- Do not delete Windows-owned cached files broadly, auto-unmount arbitrary media,
  add a bypass, select a disk/partition, or fall back to `/auto clean`.
- Repeat safety and source/index validation immediately before launch. A passed
  earlier scan does not cover media added or modified during preparation.

### 3. Separate compatibility from media readability

- Successful image enumeration does not mean an upgrade is allowed. Validate the
  actual edition (including N/Single Language), architecture, system-default UI
  language and target build. Never trust the ISO filename for these properties.
- DISM output contains a tool version and an image version. Parse the image detail
  block, not the first `Version` line. Read every actual index's metadata.
- English variants such as en-US/en-GB are not interchangeable under the current
  strict language policy. Keyboard language is not the system-default UI language.
- Automatic download availability follows the configured image language in shared
  Core logic, not only a disabled GUI control. Manual matching media has no
  English-only restriction. A higher build alone does not prove a supported path.
- Current target eligibility is Windows 11 client x64, equal-or-newer build and
  matching edition/language. Future OS catalog expansion needs separate design
  and field tests; discussion is not an implemented capability.

### 4. Distinguish storage and transport failures

- ISO-destination space and Windows-drive upgrade workspace are separate gates.
  Moving the ISO to USB does not fix inadequate Windows-drive space. Derive the
  Windows volume from Windows' actual location; do not assume C: unconditionally.
- For TLS/network issues, keep the inner error and route details for diagnostics,
  retain normal certificate validation and use the existing secure provider
  fallback. Never disable TLS validation to make a download work.
- Public browser/manual selection and the internal automatic transport are separate.
  Do not replace the direct-provider transport with the manual catalog URL.
- Wait for a mounted ISO's drive letter with a bounded timeout; mount success is
  not proof that the volume is ready for immediate validation.

### 5. Keep UI, CLI and outcome evidence consistent

- Put operational safety in Core and call it from both interfaces. Add tests for
  both presentation modes when availability, selection or exit behavior changes.
- Refresh must preserve the exact source, index and restart policy when valid.
  Changing source invalidates the old index. Failure/cancel messages must survive
  the final refresh and explain what to do, not disappear as a brief flash.
- Cancellation is for download/preparation. After Setup actually launches, do not
  pretend the assistant can safely cancel it. Do not force a reboot after failure.
- Root Setup process exit 0 means its current stage completed, not that all files
  and apps survived a completed migration. Record uncertain outcomes honestly and
  use post-restart build/edition observations plus owner verification.
- Build >= 22000 identifies Windows 11 in this client's display normalization;
  registry ProductName alone can still say Windows 10. Keep Server detection separate.

## Safe verification recipe

Run from the repository root on a Windows client with the .NET 8 SDK. Read the
test sources first; these fixtures must never invoke Windows Setup. Do not run a
real `upgrade` command on the development machine to test a theory.

```powershell
dotnet build tests/PcNinja.Win11Upgrade.SmokeTests/PcNinja.Win11Upgrade.SmokeTests.csproj -c Debug -warnaserror
dotnet run --project tests/PcNinja.Win11Upgrade.SmokeTests/PcNinja.Win11Upgrade.SmokeTests.csproj -c Debug --no-build
dotnet build tests/PcNinja.Win11Upgrade.GuiChecks/PcNinja.Win11Upgrade.GuiChecks.csproj -c Debug -warnaserror
dotnet run --project tests/PcNinja.Win11Upgrade.GuiChecks/PcNinja.Win11Upgrade.GuiChecks.csproj -c Debug --no-build
& ./tests/Verify-Cli.ps1 -ExePath ./tests/PcNinja.Win11Upgrade.GuiChecks/bin/Debug/net8.0-windows/win-x64/PcNinja-Win11-Upgrade-Assistant.exe -ExpectedVersion '1.1.0-rc.5'
git diff --check
```

Use the current project version for `ExpectedVersion` after a version change.
Check each command's exit code; a later successful command does not cancel an
earlier failure. Put `-warnaserror` on `dotnet build`, not `dotnet run`, where it
can be forwarded as an application argument. WinForms checks need an interactive
Windows client; a hosted Server CI runner is not equivalent to a Windows 10/11 VM.

For GUI-subsystem EXE output in PowerShell, pipe output (for example `| Out-String`)
and capture `$LASTEXITCODE`; a bare interactive launch may not wait as expected.
See [CLI guide](CLI.md) for supported commands, JSON and exit codes.

Real upgrades require a disposable VM/snapshot and owner authorization. Test an
app-downloaded ISO separately from a manually supplied known-good ISO. Record
actual source/index, build/edition/language, failure stage and preservation of
files/apps. Sanitize evidence; never commit raw customer logs or screenshots.

## Maintain the knowledge, not just the patch

For each meaningful change, record:

1. The symptom and evidence, separating observation from hypothesis.
2. Why the chosen fix is safe and which alternatives were rejected.
3. A regression test or a reproducible safe verification procedure.
4. Remaining limits and owner field tests, with the tested commit.
5. A committed/pushed checkpoint in the same Draft PR; verify remote SHA.

Put durable lessons here, changing status in `DEVELOPMENT-STATUS.md`, user-facing
behavior in README/help/message docs, and release history in CHANGELOG/release
notes. Do not copy whole chats or depend on personal local skill installations.
Do not store temporary branch names and test dates here as permanent policy.
Never treat a green check or a recovery checkpoint as authorization to merge or
publish. Follow [GitHub workflow](GITHUB-WORKFLOW.md) and the owner's release hold.
