# RC5 execution plan

Owner request: complete implementation, verification, merge and prerelease publication
for further owner testing. This supersedes the previous planning-only hold. No host
OS upgrade is authorized or required during development.

Baseline: RC4 tag `v1.1.0-rc.4`; process PR #6 merged separately before runtime work.

## Scope and checkpoints

1. Safety gate and image metadata (Complicated): shared Core scan before preparation
   and immediately before launch; narrowly delete writable selected-source answer
   files, block other/read-only answer files; bind actual image file/index to source.
2. Download reuse/transport (Complex): exact original name, no Copy N allocator;
   validate existing completed files, delete only demonstrably corrupt owned files;
   retained partials with exact metadata, secure alternate provider route for TLS
   redirect failure. Probe with synthetic HTTP faults and bounded live header/range
   requests, never disable certificate validation.
3. Two-choice GUI and CLI parity (Complicated): automatic download / existing media;
   one source selector, ISO/folder browse, refresh retention, image selection in the
   same screen, green Start/red Cancel, embedded help and completion.
4. Diagnostics (Complicated): bounded mount readiness, persisted attempt state and
   post-restart reconciliation; unknown process status is not upgrade success.
5. Packaging and QA (Complicated): smoke/GUI/CLI checks, screenshots, workflow review,
   RC5 version/assets/notes, <=100,000,000-byte portable EXE, SHA-256 verification.
6. Protected PR merge and prerelease (Clear): wait for required checks, merge without
   bypass, tag exact reviewed main commit, verify downloaded release artifact.

## Acceptance and boundaries

- Cover issues #1-#5 and update roadmap, test matrix and message catalog.
- Keep Core reusable and standalone separate; no new UI/runtime dependencies.
- No clean-install fallback, partition selection, forced reboot, broad ISO matching,
  deletion on cancellation/environmental errors, or certificate-validation bypass.
- No deleting cached Windows Panther files or automatically detaching user media.
- Keep earlier tags/releases intact; use corrective commits and revert PRs.
- CI does not prove real Windows migration. Explicitly retain Windows 10/11 field
  upgrades, physical USB and actual-provider interrupted/resumed downloads as owner
  validation items where unavailable locally.
