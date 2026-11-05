# Application update implementation contract

The current updater uses consent-based portable EXEs and a fixed active filename
for new stable/Beta releases. The authoritative
[user and acceptance guide](../APPLICATION-UPDATES.md) records behavior and limits.
Historical RC/Beta and stable assets remain immutable; V1/V1.1/V1.2 retain their
versioned names. Internal assembly/build names stay unchanged, while RC
publication retains its historical versioned filename formatter.

- Core/AppUpdates/ApplicationUpdateClient.cs: pinned repository, isolated
  stable/Beta discovery, strict same-release manifest, bounded JSON/redirects/
  bytes/SHA-256. Normal/critical notice is bounded plain text; exact GitHub notes
  URL is allowlisted. Metadata discovery never fetches an EXE.
- AppUpdateFiles.cs: source-directory anchoring, private same-folder staging,
  exact product/PE/hash/signature validation, protected snapshots, atomic
  same-name replacement, deterministic four-part FileVersion archives with
  hash-qualified collisions, transaction verification and held launch guards.
- AppOperationGate.cs: secure non-thread-affine serialization of this tool's own
  work and helper ownership transfer. No OS restart flags or external Setup probes.
  Windows-upgrade validation remains separate; app updates never require reboot.
- AppUpdateInstaller.cs: internal protocol-v2 successor helper. Private fresh
  request binds exact parent process/start/path/hash/FileVersion and release.
  READY/GO, exact-parent exit wait, guarded replacement/rollback and no-argument
  GUI relaunch; no legacy command replay.
  Despite the historical class name, this is not a software installer.
- AppUpdateGuiReadiness.cs / Program.cs: a shown/painted GUI acknowledges over
  a bounded same-user random pipe. The helper authenticates the native client PID
  to the retained launched process. Only then can the exact request-bound
  predecessor be archived. A separate bounded cleanup worker removes only
  transaction-proven files; uncertain recovery and unknown legacy operations are
  retained.
- MainForm.AppUpdates.cs: nonblocking check, explicit custom offer with Not now
  default, regular/critical badge/summary/version/GitHub notes, cancel/progress,
  preservation of independent Windows/media diagnostics.
- CliAppUpdates.cs: check/status/run, explicit elevated --yes, stable result codes
  and no GUI/browser/restart authorization. Handoff is not run completion.
- Directory.Build.props and .github/scripts: authoritative version, deterministic
  fixed-name stable/Beta asset, validated notice configuration, draft-first
  immutable asset publication. Beta is prerelease/non-latest and may use a matching exact
  CI-passed PR commit; stable/RC still require main ancestry.

Manifest schema 1 fields: schemaVersion, product, repository, channel, version,
tag, assetName, assetUrl, sizeBytes, sha256, releaseNotesUrl, importance, message.
channel is stable or beta. The exact fixed asset URL is derived from the validated
manifest; no broad wildcard/newest-file guess is allowed.

Beta-to-stable bootstrap is manual once: old `1.1.0-beta.5` is isolated from stable
`1.0.0` by channel and newer-version checks. Do not add a downgrade/channel bypass.
Future stable-to-stable discovery keeps the strict generated asset-name/URL policy.

The owner log confirms historical Beta 4 -> Beta 5 startup, GUI ACK, predecessor
OLD rename and UpToDate; it does not establish fixed-name lifecycle acceptance.
The one-time migration from old clients is manual because their strict
versioned-asset checks are unchanged. Unsigned publisher trust, fixed local NTFS
restriction, and conservative legacy staging retention are explicit limits.
Owner-approved release gates remain separate from a green fixture build. See
[LICENSE](../../LICENSE) for free-use-only terms and
[SECURITY.md](../../SECURITY.md) for private reporting; source visibility is not
an open-source license grant.
