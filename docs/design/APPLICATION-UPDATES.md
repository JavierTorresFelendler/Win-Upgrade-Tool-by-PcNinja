# Standalone application updates: implementation contract

Status: implemented in development Draft PR #13, not a published capability.
The published `v1.1.0-rc.8` and older assets remain unchanged and contain no app
updater. This document supersedes the planning-only proposal; it authorizes no
merge, version bump, stable tag, release or replacement of a tested executable.
See the [user guide](../APPLICATION-UPDATES.md) and [CLI contract](../CLI.md#application-updates-development-only).

## Architecture and identity

- `Core/AppUpdates/ApplicationUpdateClient.cs` owns product-pinned stable
  discovery, strict manifest validation and bounded verified bytes. It creates
  no files and never invokes Windows Setup.
- `AppUpdateFiles.cs` owns fixed local NTFS staging, executable identity,
  Authenticode policy, anchored paths and access-control checks.
- `AppOperationGate.cs` shares machine-wide exclusion with Core Windows
  preparation/Setup and transfers that exclusion to the verified helper.
- The standalone host owns GUI consent and `app-update` CLI results.
  `AppUpdateInstaller.cs` dispatches the internal helper before ordinary
  startup/elevation, with no public arbitrary-copy or command-execution option.
- `Directory.Build.props` is the literal release-version source for both
  assemblies. Runtime informational metadata preserves `+commit`; semantic
  comparison ignores valid build metadata, not prerelease identity. A stable
  release can be newer than its same-base RC. Equal/older releases are not offers.

The pinned repository is
`JavierTorresFelendler/Windows-Upgrade-Assistant-by-PcNinja`; the fixed asset is
`Windows-Upgrade-Assistant-By-PcNinja.exe`. A future WinUpdate integration must
keep its own host/product updater, not replace that host through this service.

## Discovery and publication contract

Discovery uses only the pinned GitHub latest-release API and that same release's
`update-manifest.json` asset. Drafts/RCs are not candidates. HTTPS certificate
validation stays enabled; cookies/automatic redirects are disabled. Manual
redirect handling allows at most five release-asset redirects to the two exact
GitHub asset CDN hosts. Metadata is bounded to 128 KiB/depth 16 and a 15-second
check; transfer has a 100,000,000-byte cap, ten-minute total deadline and bounded
reads. No feed override, mutable latest-EXE URL or CI-artifact URL is accepted.

Manifest schema 1 has exactly these camelCase fields:

```text
schemaVersion, product, repository, channel, version, tag, assetName,
assetUrl, sizeBytes, sha256, releaseNotesUrl
```

The manifest pins `channel=stable`, exact product/repository/asset, stable version,
matching `v{version}` tag, exact version-pinned GitHub EXE URL and release-notes
page. Release metadata and candidate bytes must agree on size/SHA-256; the actual
candidate must also be the expected AMD64 GUI EXE/product/company/version.
See [build and publication](../../README.md#build) for the workflow entry points.

The release workflow supports future numeric `x.y.z` stable versions with
`docs/RELEASE-{version}.md`; RC notes remain `docs/RELEASE-RC{N}.md`. It checks
tag/main/version agreement and the final EXE's exact `version+GITHUB_SHA` after
read-only CLI verification. Stable publication creates a draft, uploads EXE,
SHA256SUMS, BUILD-INFO and manifest, checks all four asset IDs/sizes/SHA digests
and rehashes local bytes, then publishes/latest last. It refuses existing
published releases, nonempty drafts, asset replacement and backwards latest
movement. RCs retain three assets/prerelease status and no manifest. There is
no generated main-branch feed commit or permission to publish from this design.

## Replacement safety

Explicit GUI consent or elevated CLI `install --yes` is required. Installation
targets the fixed protected Program Files copy, never the arbitrary running
portable source. Private per-operation staging is Administrators/SYSTEM-only;
the final app is readable/executable by users. Existing unsafe ownership/ACLs,
reparse/hardlink/changed paths and non-fixed/non-NTFS locations fail closed, not
silently hardened or redirected. A source running on ISO/network/removable media
must first be copied to an ordinary fixed local NTFS folder by the user.

The verified candidate also supplies the helper. A private, bounded, fresh
request binds the exact parent process/start time/path/hash, candidate, target
state and signing requirement. READY/GO handoff preserves the machine gate while
the helper waits for the exact parent to exit. It rechecks Windows Setup/restart
state, request, paths and bytes before recoverable replacement. It never kills
processes, schedules work, forces a reboot or replays upgrade arguments. GUI
relaunch has no arguments; CLI never launches a successor GUI.

Unsigned releases explicitly rely on repository/release/manifest trust, not a
cryptographic publisher guarantee. A compromised publishing account can replace
both hash and executable. If the running or protected installed app is signed,
the successor must retain its exact trusted signing certificate; invalid,
unsigned or different-certificate successors are refused. Certificate rotation
needs a separate policy. This is not the ISO Microsoft-only signature verifier.

## Verification and remaining acceptance

Safe fixtures cover release/version/HTTP rejection, exact bytes, CLI consent and
handoff semantics, GUI state, local ACL/path/request policy, operation exclusion
and the offline draft-first publication lifecycle. Privileged native probes may
report SKIP when unavailable; synthetic fixtures are not a live replacement test.
See [development handoff](../DEVELOPMENT-STATUS.md) for current run evidence.

The final disposable-VM test still needs two separately owner-approved stable
versions: first stable already containing this updater, then a higher stable
with correctly increased binary/tag/manifest identity. Verify actual GitHub
discovery, consent, exact bytes, helper/parent exit, protected-copy version and
relaunch, original outside copy unchanged, no repeated offer, failure recovery
and no Windows Setup/reboot/argument replay. Existing assets remain immutable.
Neither those stable publications nor final VM acceptance occurred in this PR.
