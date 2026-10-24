# Manual Windows 10 upgrade/repair POC

## Scope and decision

Owner-approved experiment: reuse the existing ISO/folder/mounted/network source
and image-index selector. Detect Windows 10 client x64 from the selected image's
metadata and switch the Setup command. Do **not compare older/equal/newer build
order within Windows 10** in this POC. This replaces the earlier stricter plan;
it is not a claim that Windows can downgrade while preserving apps/files.

- Windows 10 -> Windows 10: keep edition, exact system UI language, x64, signature,
  DISM, storage, answer-file and final source/index checks. Setup decides whether
  `/auto upgrade` migration can proceed; no clean/data-only fallback or /quiet.
- Windows 11 -> Windows 10 stays blocked. Windows 11 target build checks and
  experimental `/product server` profile stay unchanged.
- Automatic download remains Windows 11 only. Supply Windows 10 media manually.
- No new wizard, extractor, registry-hive reader, package or CLI flag is added.

Windows 10 command (do not execute on the developer computer):

```text
setup.exe /auto upgrade /dynamicupdate disable /compat ignorewarning /imageindex <actual-index> /installfrom "<exact-image-path>"
```

Append `/noreboot` for manual restart. Omit `/product server` and `/eula accept`.
The Windows 11 command is unchanged. The original filename, exact chosen source,
WIM/ESD/first-SWM binding, cancellation and manual-restart completion notice remain.

Windows 10 shared-base files can report image metadata that does not identify
the exact feature release. This POC deliberately does not resolve it. Do not use
ISO/folder names as release proof; history's raw TargetBuild does not automatically
verify a completed Windows 10 migration. Check winver and apps/files manually.
Windows 10 standard Home/Pro support has ended; ESU is separate. This is an
advanced experiment, not a supported-lifecycle recommendation.

## Plan and safe verification

Complicated, bounded change: shared eligibility/profile -> GUI/CLI wording ->
fixture/regression tests -> Draft PR/CI EXE -> owner VM acceptance. The uncertain
step is real Setup migration eligibility, deliberately delegated by the owner;
synthetic acceptance is not a real downgrade or upgrade result.

Automated checks run no Setup: older/equal/newer Windows 10 fixtures, Windows 11
to 10 rejection, malformed/server/x86/language/edition/index rejection, exact
WIM/ESD/SWM arguments with restart on/off, automatic-source block (including mixed
media), unchanged Windows 11 command/build checks, honest history and UI warning.

## Disposable-VM acceptance before merge

1. Snapshot a Windows 10 VM; keep a recognizable file and installed app. Supply
   matching Windows 10 ISO or extracted folder. Inspect images and select its
   actual index (never assume a universal Home/Pro index).
2. Validate/dry-run first. CLI uses existing `validate --iso <path>` or
   `validate --mounted <folder> --image-index <index> --json`. Inspect
   `setupProfile: Windows10`, `buildOrderChecked: false` and `policyNotice`.
3. Start manually. Check the POC warning and logged standard command: no product
   server/EULA/quiet, correct InstallFrom/ImageIndex. Test restart off first;
   exit 0 should request a manual restart, not claim final upgrade success.
4. After restarting, check winver, edition/language, test file and installed app.
   Repeat for equal-version repair from a fresh snapshot. Older-image attempts
   are optional snapshot-only tests; record Setup's refusal/outcome, never assume
   that PcNinja accepting an index means a downgrade is supported.
5. If Setup offers disk/partition selection, clean install or cannot keep apps,
   **stop** and preserve logs; do not proceed through another installation type.
6. Regression: Windows 10 -> 11, Windows 11 older-target blocking, ambiguous image
   selection, language mismatch, unsafe answer-file media and low Windows space.

Report exact test-build commit, actual image index/metadata, installed OS before/
after, restart policy, Setup exit code, UI behavior and apps/files preservation.
Before another failed-attempt retry, privately preserve the matching app log,
Last-Upgrade and current Panther logs. Do not upload raw VM data to the public PR.

Rollback: restore the VM snapshot and use the prior known-good EXE. Published
RC5/test artifacts are not overwritten. Keep this PR Draft until owner feedback;
first stable publication and Internet app-update testing remain separate decisions.

## Primary references

- [Windows Setup options](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/windows-setup-command-line-options): /Auto Upgrade preserves apps/data when eligible; /EULA is new with Windows 11.
- [Windows 10 shared-core releases](https://support.microsoft.com/en-us/servicing/os/windows-10/2022/06/kb5015684-featured-update-to-windows-10-version-22h2-by-using-an-enablement-package).
- [Windows 10 release/lifecycle information](https://learn.microsoft.com/en-us/windows/release-health/release-information).
