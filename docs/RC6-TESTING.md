# RC6 verification

## Required automated gates

- Release metadata: GUI/Core version 1.1.0-rc.6, file version 1.1.0.6, GUI product
  Windows Upgrade Assistant By PcNinja, fixed EXE filename, no GUI release label.
- Core smoke tests: transfer reuse/resume/ownership, selected/secondary answer
  files, source/index binding, edition/language/x64/family policy, Windows 10/11
  command profiles and restart behavior. No Windows Setup invocation.
- Outcome fixtures: bounded UTF-8/UTF-16 reading; session/time/source/index
  correlation; stale/incomplete/missing evidence rejection; running Setup guard;
  separate process/Setup codes; no false Windows 10 shared-base confirmation.
- Local Windows-client GUI checks: two exclusive sources, Start/Cancel/Setup
  lockout, automatic consent/invalidation, language guidance, persistent rollback
  notice/details and manual-restart guidance. Hosted Server CI compiles these;
  it does not substitute for client interaction tests.
- Read-only CLI: expected version and full build identity, help/JSON/argument
  rejection, native completion, product metadata, Windows GUI subsystem and the
  100,000,000-byte cap on the actual portable artifact.
- Release provenance: reviewed main commit/tag/version agreement; uploaded EXE
  SHA-256 matches SHA256SUMS; BUILD-INFO commit matches the tag. Run read-only CLI
  verification against the downloaded GitHub Release EXE, not just a local build.

## Owner field evidence and remaining limits

- Owner explicitly confirmed 19043 (21H1) -> 19045 (22H2) Windows 10 Pro with
  working apps/files after reboot, then continued staged Windows 11 trials.
- On 2026-10-20 the owner reported stability and authorized RC6 merging/release.
  This is not independent evidence for every source/index/language/hardware path.
- A separate trial rolled back due to a ReAgentOld.xml collision, 0x800700B7 -
  0x2000A. The source of that duplicate is unproven; no automatic cleanup added.
- Preserve the old EXE and VM snapshots. Keep automatic app-downloaded media
  coverage distinct from known-good manually supplied media. Check actual OS
  build/edition and files/apps after reboot; preparation exit 0 is not completion.

Release-preparation PR checks and the tag workflow retain exact run/commit and
artifact provenance. No raw VM logs, ISOs or customer screenshots are published.
