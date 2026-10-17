# Automatic-flow development test build

This is a PR test, not a new RC or stable release. Published RC5 is unchanged.
Use the exact EXE, SHA256SUMS.txt and BUILD-INFO.txt from the same successful PR
validation artifact. The window/product version still says RC5; BUILD-INFO's
source and tested-merge commits identify this test, not the old release label.
Download artifacts from the PR's **Checks > PR validation > Summary > Artifacts**
while signed into GitHub. Artifacts expire after 30 days; keep the build-info
and checksum with a saved test EXE. Never substitute a design-only PR artifact.

## Automated checks (no Windows Setup)

- Existing Core transfer, ownership/reuse/resume, safety, images and history tests.
- CLIXML genuine error vs progress, malformed XML/DTD rejection and decoded text;
  synthetic Windows-drive space/current/required/shortfall guidance.
- Windows client GUI: one automatic consent for either restart policy; manual,
  changed-source, ambiguous and changed-policy confirmation; declined real Start
  returns before transfer; existing selection/refresh/cancel/language checks.
- GUI persistent failure text through Refresh. Published-style CLI read-only
  checks, warnings-as-errors builds and EXE cap of 100,000,000 bytes.
- CI compiles but does not execute interactive GUI checks on its Server runner.

## Owner VM acceptance before merge

Use disposable snapshots. Put a test file and an installed app in the VM to
verify preservation after upgrading. Do not perform these tests on production.

1. On an en-US Windows 10/11 VM with enough space, choose automatic mode and
   automatic restart. Start, accept the single **Download and upgrade** prompt,
   and leave it running. It should download/reuse, validate, then start Setup
   without a second assistant Start question. Verify files/apps and final OS.
2. Repeat with automatic restart off: `/noreboot` remains in the logged Setup
   command, with no assistant-forced reboot. After Setup exit 0, an OK-only
   **Restart required** information dialog and persistent readiness instruction
   explain how to continue. OK never reboots. Restart manually when appropriate.
   CLI `--no-restart` prints the same instruction; successful JSON includes
   `manualRestartRequired`. No restart notice is shown for failure/transition.
3. Decline the initial question: no transfer and no Setup. Start again, accept,
   cancel during preparation: no Setup, Start returns. A later Start asks again.
4. Choose known-good manual ISO/folder/mounted media: review image and receive
   the normal postvalidation confirmation. CLI `--download` / `--iso` /
   `--mounted` retain the existing `--yes` and image-index contracts; CLI has
   no new GUI consent policy. Never run actual CLI upgrade on the developer host.
5. A browser fallback (if encountered) stops, asks for exact downloaded ISO
   selection and requires the normal manual Start confirmation. No unattended
   continuation from an old automatic attempt.
6. Less than 20 GB free on Windows drive: no Setup; persistent drive/current/
   required/missing space message. Refresh retains the explanation. A separate
   spacious ISO drive must not bypass the Windows workspace gate.
7. Unsafe selected/secondary answer-file media, incompatible/no matching or
   ambiguous images: no guessed index, no safety bypass or clean-install fallback.
   Remove blockers/choose a valid image and explicitly retry.
8. If drive discovery times out, verify readable guidance and exact ISO/volume
   diagnostics rather than `#< CLIXML`. This PR does not claim to fix eject locks.

Report source commit, source mode, restart policy, original/target build/edition/
UI language, whether assistant prompts matched, and whether apps/files survived.
Send logs privately; do not commit raw VM logs or screenshots into the public repo.

## Deferred, not acceptance claims for this build

General manual Inspect/Start caching; app-created mount ownership/eject cleanup;
safe-media copy/detach from design PR #9; Windows 10 target upgrades/equal-version
repair; Internet application updates and first stable publication. The owner
must participate before the first stable/self-update version-pair experiment.
