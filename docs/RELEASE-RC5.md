# PcNinja Windows 11 Upgrade Assistant RC5

Portable Windows x64 prerelease, version **1.1.0-rc.5**.
Download **PcNinja-Win11-Upgrade-Assistant-RC5.exe** below. No scripts or separate
.NET installation are needed. SHA256SUMS.txt and BUILD-INFO.txt identify the file
and exact source commit. The portable EXE is limited to 100,000,000 bytes.

## Changes

- **Two primary source choices:** automatic download, or existing installation
  media. The existing-media selector accepts an ISO, mounted drive, extracted
  Setup folder or network folder, with ISO/folder browse buttons.
- **Installation image selection in the same screen:** Inspect images or Start
  reads actual indexes and metadata. A unique matching edition/language/x64 image
  is recommended. Select explicitly if multiple matching images exist. Source
  replacement clears old image selection; CLI adds `--image-index`.
- **Unattended-media safety:** checks selected and secondary attached media,
  configured answer files and dedicated Windows unattended paths. Narrowly
  identified writable selected-source answer files are removed and verified;
  other/read-only/locked answer files block launch. The final gate repeats before
  Setup. Detach the interfering ISO/USB, Refresh and retry; no bypass is offered.
- **Exact-file download reuse:** validate an existing original-name ISO rather
  than downloading a numbered copy. Only proven-corrupt recorded PcNinja downloads
  are replaced. A recorded completed ISO can be revalidated without contacting an
  unavailable provider. Unowned files and environmental/cancellation failures
  are preserved. Original-name resumable partials replace Copy N logic.
- **Secure transport fallback:** a short-link connection failure automatically
  tries the provider route, without disabling certificate validation. Provider
  access/rate limits may still require the normal browser download; select that
  exact file afterward. Microsoft remains a separate manual fallback.
- **Bounded mount readiness:** wait up to 30 seconds for the mounted drive letter.
- **Outcome diagnostics:** persist the last attempt and reconcile the running
  build/edition on the next GUI launch or CLI `status`. `0x40010004` is unconfirmed,
  not success (CLI exit 8). Stage completion is not final upgrade success.
- Retain the top green Start/red Cancel button, exclusive selection highlight,
  one logo, website branding, CPU/RAM/storage details, refresh, and restart policy.
- PR and release checks cover regression tests, CLI help/completion and the size
  cap. Release tags must match the product version and point to reviewed main code.

## Important limitations and safety

This is **an unsigned experimental prerelease for further testing**, not a stable
production release. The undocumented `/product server` behavior is not a Microsoft
support guarantee. Back up files and create a VM snapshot before testing.

Never continue if Windows Setup unexpectedly offers a clean installation or asks
for a target partition. The assistant does not choose, format or erase partitions,
does not fall back to `/auto clean`, and never calls `shutdown.exe`.

Windows-owned cached Panther answer files are not deleted. These checks are not
a guarantee against every customized Windows image; customized image contents
and scripts are outside the whole-ISO metadata check. Do not attach new
installation media after the final check. Use a separately trusted SHA-256 for
independent image verification; a self-calculated hash is not publisher proof.

Automated tests use synthetic transfers and media fixtures. Windows 11 GUI action
checks and portable CLI checks passed locally. A fresh Windows 10/11 OS upgrade,
physical read-only USB/DVD media, and a complete real-provider download/resume
remain field-test items. Live TLS 1.2 probing confirmed the short-link problem;
the provider returned HTTP 403 during the bounded probe. Provider availability
and Windows 10 end-to-end download success are **not** claimed by this release.

## Documentation

- [Overview and GUI](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/blob/v1.1.0-rc.5/README.md)
- [CLI and all exit codes](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/blob/v1.1.0-rc.5/docs/CLI.md)
- [Change log](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/blob/v1.1.0-rc.5/CHANGELOG.md)
- [Verification and field-test checklist](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/blob/v1.1.0-rc.5/docs/RC5-TESTING.md)
- [User-message catalog](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/blob/v1.1.0-rc.5/docs/UI-MESSAGES.md)

Offline help: `PcNinja-Win11-Upgrade-Assistant-RC5.exe help all`.
