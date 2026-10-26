# Security reporting

Please report suspected vulnerabilities privately to **javier@pcninja.pro**.
Do not post exploit details, credentials, customer data, or unredacted customer
logs in a public issue. For ordinary non-sensitive bugs, use this repository's
[issue tracker](https://github.com/JavierTorresFelendler/Win-Upgrade-Tool-by-PcNinja/issues).

## What to include

- Application version and source/build identifier from Help, properties or logs.
- Windows edition/build, architecture and relevant language.
- A short reproduction, expected/observed behavior, and likely security impact.
- Relevant redacted log excerpts or screenshots. Remove account names, private
  network paths, personal files, credentials and download/session tokens.
- The official download URL and SHA-256 if file integrity is involved.

There is no guaranteed response deadline or paid-support commitment. Do not
assume that a report has been reviewed until you receive a reply. Coordinate
publication of sensitive findings privately with PcNinja.

## Release and update policy

The newest stable release is the recommended production baseline once V1 is
published. Beta builds are a separate testing channel; historical builds remain
available for traceability, not a promise of security maintenance for each build.
Download official builds and verify the exact release's SHA256SUMS. A checksum
does not independently authenticate a publisher; current builds are unsigned.

Application-update discovery reads GitHub metadata. Downloading/running a new
EXE requires user consent. A critical-update notice recommends action but does
not authorize silent installation, Windows Setup or an OS restart. Pending OS
restart flags do not prevent updating this tool.

## Upgrade safety

Back up important data before using Windows Setup. Unsupported-hardware
workarounds are experimental and are not Microsoft support guarantees. Stop if
Setup unexpectedly offers disk/partition selection or a clean installation.
Never bypass interfering unattended-media checks or delete Windows restart
flags as a workaround for a report.
