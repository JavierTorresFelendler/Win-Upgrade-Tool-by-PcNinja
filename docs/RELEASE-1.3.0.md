# Windows Upgrade Tool by PcNinja — 1.3

Opening the tool again restores/activates the running GUI instead of leaving
another persistent window. Startup/shutdown activation is retried, modal dialogs
are handled and a normal launch can reach the elevated instance before UAC.
If Windows refuses foreground focus, the taskbar flashes. Unavailable guard
objects fail open with diagnostics; Core operation serialization is separate.

Portable application updates now use the fixed current filename:

```text
Win-Upgrade-Tool-by-PcNinja.exe
```

The verified predecessor is archived as, for example,
`Win-Upgrade-Tool-by-PcNinja-v1.2.0.0.exe`. Identical backups are reused;
different-content collisions get a full SHA256 suffix. Distinct historical
backups survive. Same-name updates retain a private recovery snapshot and use
atomic replacement. GUI readiness precedes final archival/cleanup; uncertain
transactions retain recovery bytes. A bounded independent worker cleans proven
completed staging after the helper exits, including after GUI close. Legacy
staging without completion proof is retained. Last update status is in ProgramData.

The update check now reports up to date when the published release is equal to
or older than this tool, even if that historical release uses its old filename.
Every newer update still requires exact version/channel/product/repository,
asset URL/name/size/hash and applicable signer validation, plus explicit consent.

**Migration:** Published V1/1.2 and older clients that pin versioned asset names
cannot automatically consume the new fixed-name release. Download/open 1.3
manually once. New fixed-name clients, including the PR31 1.2 test candidate,
can discover a newer stable fixed-name release. RC builds using the current
product identity and stable channel can discover a newer official stable release;
historical Beta channel/identity restrictions still apply. Old releases are unchanged.

Technical product version: `1.3.0`; Windows FileVersion: `1.3.0.0`. The EXE is
portable x64 and unsigned. CLI, Windows media compatibility checks and Windows
Setup behavior retain their existing safety boundaries. Experimental unsupported
hardware and Windows 10 paths keep their documented limits. An app update never
authorizes a Windows upgrade or computer restart.
