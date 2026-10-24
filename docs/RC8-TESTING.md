# RC8 verification

## Release identity and automated gates

- GUI/Core version `1.1.0-rc.8`, EXE file version `1.1.0.8`, unchanged product/icon,
  one original mascot/site link and fixed `Windows-Upgrade-Assistant-By-PcNinja.exe`.
  Portable size must not exceed 100,000,000 bytes.
- Build Core, headless CLI and client GUI fixtures with warnings as errors; run
  each suite without real ISO transfer/attach/detach, browser/clipboard effects,
  Windows Setup or reboot. All external effects in fixtures must remain fake.
- Real-entry automatic preflight fixtures must reject unsupported profiles before
  any HTTP request or destination creation; keep positive eleven-edition coverage.
- Retain transfer/resume/corrupt-owned-file rules, exact-source session/journal/
  mount proof and safe fallback. Native tiny-file journal proof may explicitly SKIP
  without privileges; synthetic proof does not establish a real ISO cache hit.
- Exercise manual next-action transitions, failed/cancelled revalidation, actual
  red Cancel, incompatible-image details, LTSC/manual and Server execution blocks.
  Preserve consent, quiet/restart, results, refresh, resize and logo/link fixtures.
- Required PR validation passes before each approved merge. Tag `v1.1.0-rc.8`
  only on the reviewed main release commit. Keep prior tags/releases untouched.
- Download all three GitHub Release assets into a new directory. Verify EXE hash
  against SHA256SUMS, BUILD-INFO commit against the tag, file/product versions and
  byte count. Run `tests/Verify-Cli.ps1` on that exact EXE with ExpectedVersion
  `1.1.0-rc.8`; these commands do not start an upgrade.

## Owner field checks (disposable VM/snapshot)

1. Eligible en-US/x64 covered client: Start still downloads/reuses the owner's
   exact ISO, validates and continues using upfront consent, not browser-only mode.
2. Unsupported automatic profile: no ISO transfer; show the reason and leave exact
   manual media and matching download guidance available. Browser fallback ends
   automatic consent; no guessed/newest downloaded-file selection.
3. Spanish/other language or manual client LTSC: select matching actual media from
   Downloads. Validate becomes prominent, image choice is clear, then Start is
   highlighted only for compatible prepared media. Bad media keeps its explanation.
4. Validate an existing ISO in the exact automatic destination, switch modes and
   start. Check exact identity reuse/no duplicate mount when proof is available;
   network/removable/missing proof falls back to full validation safely.
5. Cancel download/preparation and retry; change source/image/restart/quiet state.
   No stale readiness, stuck highlight, unexpected browser or implicit restart.
6. Selected/secondary answer-file media remains blocking when unsafe/read-only;
   attach unsafe media late and verify final checks still stop Setup. Refresh after
   manual detach. Do not continue if Setup offers disk selection/clean installation.
7. For quiet/remote automation, use explicit consent and check resultCode, assistant
   exit, native Setup code and readyForRestart together. Failure/uncertain/status/
   validation never authorize reboot. A real agent integration is a separate test.
8. After actual upgrade/restart, confirm OS version, edition, files and installed
   apps. Record exact source/index and app build; exit 0 alone is preparation only.

The owner reports stable operation and successful initial next-action visibility
checks. Do not mark every matrix row above passed without exact field evidence.
Server remains inspection/explicit command-copy guidance only, never an app-driven
upgrade. Application self-update and media preparation are not in this RC.
