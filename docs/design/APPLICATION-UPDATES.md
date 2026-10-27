# Application update implementation contract

The owner replaced the earlier fixed Program Files/in-place replacement design
with consent-based side-by-side versioned portable EXEs. The authoritative
[user and acceptance guide](../APPLICATION-UPDATES.md) records behavior and limits.
PR #13 retains implementation/review history; historical RC/Beta assets are immutable.
Official V1 maps technical `1.0.0` to `Win-Upgrade-Tool-by-PcNinja-v.1.exe`, with
`1.1.0` -> `-v.1.1.exe` and `1.0.1` -> `-v.1.0.1.exe`. Internal assembly/build
names stay unchanged, and Beta/RC publication keeps its legacy filename formatter.

- Core/AppUpdates/ApplicationUpdateClient.cs: pinned repository, isolated
  stable/Beta discovery, strict same-release manifest, bounded JSON/redirects/
  bytes/SHA-256. Normal/critical notice is bounded plain text; exact GitHub notes
  URL is allowlisted. Metadata discovery never fetches an EXE.
- AppUpdateFiles.cs: source-directory anchoring, private same-folder staging,
  exact product/PE/hash/signature validation, safe existing-file reuse, private
  copy/helper ACLs, no-overwrite publication and held launch guard.
- AppOperationGate.cs: secure non-thread-affine serialization of this tool's own
  work and helper ownership transfer. No OS restart flags or external Setup probes.
  Windows-upgrade validation remains separate; app updates never require reboot.
- AppUpdateInstaller.cs: internal protocol-v2 successor helper. Private fresh
  request binds exact parent process/start/path/hash and release. READY/GO,
  exact-parent exit wait and no-argument GUI relaunch; no legacy command replay.
  Despite the historical class name, this is not a software installer.
- AppUpdateGuiReadiness.cs / Program.cs: a shown/painted GUI acknowledges over
  a bounded same-user random pipe. The helper authenticates the native client PID
  to the retained launched process. Only then can the exact request-bound
  predecessor be hash-verified/renamed through one handle to OLD-/OLD.Vn-.
  No readiness/CLI relaunch means no archive; locked/mismatched names are retained.
- MainForm.AppUpdates.cs: nonblocking check, explicit custom offer with Not now
  default, regular/critical badge/summary/version/GitHub notes, cancel/progress,
  preservation of independent Windows/media diagnostics.
- CliAppUpdates.cs: check/status/run, explicit elevated --yes, stable result codes
  and no GUI/browser/restart authorization. Handoff is not run completion.
- Directory.Build.props and .github/scripts: authoritative version, deterministic
  versioned asset name, validated notice configuration, draft-first immutable
  asset publication. Beta is prerelease/non-latest and may use a matching exact
  CI-passed PR commit; stable/RC still require main ancestry.

Manifest schema 1 fields: schemaVersion, product, repository, channel, version,
tag, assetName, assetUrl, sizeBytes, sha256, releaseNotesUrl, importance, message.
channel is stable or beta. Filenames and pinned URLs are derived strictly from
the validated release version, never broad wildcard/newest-file guesses.

Beta-to-stable bootstrap is manual once: old `1.1.0-beta.5` is isolated from stable
`1.0.0` by channel and newer-version checks. Do not add a downgrade/channel bypass.
Future stable-to-stable discovery keeps the strict generated asset-name/URL policy.

The owner log confirms real Beta 4 -> Beta 5 startup, GUI ACK, predecessor OLD
rename and UpToDate. That does not establish every fixture or stable lifecycle.
Unsigned publisher trust, fixed local NTFS restriction and retained staging
are explicit limits. Owner-approved release gates remain separate from a green
fixture build. See [LICENSE](../../LICENSE) for free-use-only terms and
[SECURITY.md](../../SECURITY.md) for private reporting; source visibility is not
an open-source license grant.
