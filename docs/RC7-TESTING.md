# RC7 verification

## Automated and developer gates

- GUI/Core metadata: 1.1.0-rc.7; EXE file version 1.1.0.7, fixed product/asset
  name, no GUI release label. Size must stay below 100,000,000 bytes.
- Run Core smoke fixtures and client GUI fixtures with warnings-as-errors builds.
  The added resize fixture must fail against the old plain Panel implementation.
- Both actual dashboard cards must enable ResizeRedraw/OptimizedDoubleBuffer.
  Repeated live grow/shrink must invalidate the complete client rectangle and
  paint both cards, without manual Refresh/Invalidate/DrawToBitmap masking it.
  Exercise maximize, minimize and restore, then retain existing GUI safety checks.
- Verify approved source captions after switching both ways, one original
  embedded mascot, original EXE icon/source asset hashes, and unchanged real
  website/manual-download handlers/targets.
- Inspect normal/minimum-window previews. Screen capture is supplementary;
  DrawToBitmap alone cannot demonstrate that old on-screen borders are erased.
- Run read-only CLI checks against the actual portable binary, including expected
  version/build, help, JSON, invalid arguments, completion, GUI subsystem and size.
- PR CI must pass before merge. Release only the reviewed merged main commit.
  Download the uploaded GitHub Release EXE; compare SHA256SUMS, BUILD-INFO,
  file version/product/source commit and run the same read-only CLI suite.
- Preserve RC6 and earlier tags/assets. Do not run Windows Setup or reboot the
  development machine as part of any gate.

## Owner checks on the affected machine

1. Verify title, shorter headings, one original logo and all three public links.
2. Repeatedly stretch/shrink the window and alternate maximized/restored sizes.
   Check bottom/side borders after each operation and after minimize/restore.
3. Repeat with the DPI/display/remote-session configuration that showed trails.
4. Check both source choices, Validate, Refresh, restart selection and Cancel.

The rendering regression is UI-only; actual Windows upgrade acceptance remains
the distinct owner field evidence recorded for RC6. Published RC7 must not be
presented as a new real-upgrade validation or universal graphics-driver fix.
