# Windows Upgrade Assistant By PcNinja - RC8

Portable Windows x64 prerelease **1.1.0-rc.8**. Download
**Windows-Upgrade-Assistant-By-PcNinja.exe**. No scripts or separate .NET install
are needed. The filename and branding stay fixed; file properties, logs, CLI and
BUILD-INFO identify the version/commit. SHA256SUMS identifies the exact binary.

## Changes since RC7

- Preserve real automatic PcNinja download/validation/upgrade. Check the installed
  edition, system UI language, architecture, client/LTSC/Server type and known
  build family against configured media coverage before transfer. Retain all
  eleven covered Home/Pro/Education-family editions and secure provider fallback.
- Offer matching public browser ISO guidance for other supported profiles and
  languages. Finish browser downloads manually, then select the exact ISO in the
  app. Add catalog credits to Massgrave; no activation/licensing code is included.
- Permit matching manually supplied x64 client LTSC media without automatic LTSC
  transfer or edition/channel conversion. Server stays guidance/inspection and
  explicit manual-command-copy only; the app never starts Setup on Server.
- Reuse successful preparation of the exact unchanged ISO within a session only
  with conservative file/journal/CLOSE and exact mount-identity proof. Otherwise
  do full validation. Keep fresh signatures, attached-media safety and final gates.
- Start ISO browsing in registered Downloads. Guide manual selection with
  exclusive Browse/Validate/image-choice/Start highlights; clear stale readiness
  on rechecks. Preserve mismatch explanations, real Cancel and automatic Start.
- Add optional silent Windows Setup (`/quiet`) and documented CLI automation
  results with explicit restart readiness and native Setup codes. Quiet CLI needs
  `--yes`; CLI `--no-restart` remains independent. GUI silent mode locks automatic
  restart while selected and restores the prior preference when cleared.
- Explain empty installation-image selection without inventing a selectable image
  or hiding the existing no-compatible-image/error explanations.

## Safety and limits

This is an **unsigned experimental prerelease**, not a stable production release.
Back up important files. Windows 11 uses the undocumented `/product server`
method. Never continue if Windows Setup asks for a partition or offers a clean
installation. No clean/data-only fallback or forced reboot after failure is added.

Automatic eligibility trusts configured media coverage, not the contents of a
mutable remote URL. Actual downloaded-media validation is still mandatory.
Signature/DISM readability and a calculated hash without a trusted expected hash
are not full image-integrity proof. Browser guidance is not validation or consent.

The existing manual Windows 10 target POC remains: choose actual matching media;
Setup decides migration eligibility without an intra-Windows-10 build-order check.
Windows 11-to-10 is blocked. This is not a guarantee of downgrade or repair support.

No application self-updater, ISO extraction/copy/sanitization preparation or
automatic arbitrary-media detach is included. Those are separate future work.
All previous RC tags/assets and per-build test executables remain unchanged.

## Verification

Core, headless CLI and Windows client GUI fixtures cover preflight, transfer,
session reuse, media/image/answer-file rules, consent, cancellation, next-action
cues, quiet/restart outcomes and original branding/links. Hosted Server CI builds
GUI fixtures; interactive client execution is checked separately. These fixtures
do not run Windows Setup or establish a real OS migration result.

The owner reports the current test application works well and authorized this RC
checkpoint. Remaining field cases and artifact-verification steps are recorded in
[RC8 verification](RC8-TESTING.md). Only reviewed source merged to main is tagged;
the published EXE is checked again after downloading it from GitHub Releases.
