# Matched ISO download guidance

Included in RC8 and refined for V1. Previous test/release binaries remain
unchanged. Matching recommendations
are separate from actual-media validation and permission to run Windows Setup.

## Approved scope and flow

1. Read the installed edition, system-default UI language, native architecture,
   installation type, build/revision and LTSC status in shared Core. A keyboard
   layout or the UI thread culture is not the installation language.
2. Prefer a matching configured PcNinja source. All eleven existing OEM editions
   are retained: Home, Home N, Home Single Language, Education, Education N, Pro,
   Pro N, Pro Education, Pro Education N, Pro for Workstations and its N edition.
   The configured source is en-US/x64 PcNinja Windows OEM, at
   https://winoemiso.pcninja.pro/. Own-source eligibility uses client type,
   architecture, edition, language and LTSC status, not a pinned target build or
   per-release host-build list. This permits a download attempt, not a promise of
   newer/repair media. TargetVersion remains unknown until actual inspection.
   External catalog feature/build rules stay separate and unchanged. Automatic Start
   still uses the real download/validation/upgrade flow, not browser-only guidance.
3. Otherwise **Find matching ISO** reads the appropriate public catalog on demand.
   It matches the exact culture (for example es-ES is not es-MX), architecture,
   Consumer/Business edition package and a known released feature/build family.
   Prefer the latest eligible feature and catalog revision, never an older feature.
4. Open the selected public file-host landing page in the browser. Any human
   verification and download are the user's responsibility. Keep instructions
   on the same application screen: finish the browser download, click **ISO...**,
   select that exact file, then **Validate**. No directory polling, newest-file
   guessing, automatic media selection or unattended continuation is added.
5. Actual signature, image/edition/language, storage and answer-file validation
   remain mandatory. Link matching is not proof of ISO contents or upgrade support.

The PcNinja manual button uses the same full-profile policy: matching users get
the OEM browser page; other users retain the broader PcNinja catalog. Microsoft's
official alternative stays available. Automatic transfer failures retain the
existing explicit browser fallback and invalidate unattended consent.

## Catalog adapter and conservative limits

### Automatic-source preflight and manual guidance follow-up

The owner's automatic PcNinja route/secure provider fallback remains unchanged.
Eligibility is checked from the current edition, system UI language, architecture,
client/LTSC/Server type **before** destination creation or
an ISO HTTP request, in shared Core. The GUI also rechecks before asking for
automatic-flow consent. All eleven configured owner editions remain covered;
the existing own-source download/validation/upgrade flow has no added manual step.

Tests call the real Core download entry with synthetic unsupported profiles and
a fail-if-called HTTP handler, asserting zero requests and no destination created.
The internal constructor seam is test-only, not a public CLI eligibility override.
Normal HTTP redirect/cookie/TLS/user-agent/provider behavior and transfer callbacks
are unchanged. Existing policy fixtures retain all eleven allowed edition IDs.
Version-independent own-source fixtures include Pro N/newer and preview build
numbers without enabling Enterprise/LTSC/Server/other-language routes. They also
prove that actual older/mismatched Windows 11 image metadata remains incompatible.

This preflight trusts the owner's configured ISO coverage; it cannot prove that
a mutable remote link still contains that media. Post-download actual-image,
signature, integrity, storage and answer-file checks must still run. Wrong bytes,
provider changes or Windows Setup's later compatibility checks can still fail;
never replace those checks with a guessed filename or a readiness highlight.

Manual source selection now uses a Downloads-first picker and visual next-action
cues. These are presentation hints only, not consent or validation. Quiet/JSON
CLI download errors include the matching public fallback page without opening a
browser or exposing internal provider details. See [GUI messages](UI-MESSAGES.md).

Credits: the alternative ISO catalogs are maintained by the
[Massgrave project](https://massgrave.dev/). See their
[catalog/website source](https://github.com/massgravel/massgrave.dev) and
[main GitHub project](https://github.com/massgravel/Microsoft-Activation-Scripts).
We appreciate their work making the catalog available. This integration uses
catalog references only, not their activation code or licensing tools.

- Client catalog: <https://massgrave.dev/windows_11_links>.
- Server catalog: <https://massgrave.dev/windows-server-links>.
- Read HTML only, on explicit request, with a 20-second deadline and 2 MiB limit.
  Use a dedicated no-cookie/no-auto-redirect HTTP client, separate from the ISO
  transport. No file-host API, challenge bypass, session-token extraction or ISO
  download is performed by this adapter.
- Recognize each table's own nearest heading/build, not navigation-tab titles.
  Current minified markup permits omitted row/cell closing tags. Cross-check
  language label, exact ISO filename, package, feature, architecture and build.
- Only configured HTTPS public landing patterns on zerofs.link and
  buzzheavier.com are accepted. Reject API paths, queries, credentials, custom
  ports, fragments, misleading hosts and ambiguous different matches.
- Changed markup, HTTP failure, redirects, unknown edition/release/language,
  LTSC, ARM64/x86 or no exact match return catalog/manual guidance with
  `ExactMatch=false`. Never silently substitute en-US, a different edition or x64.
- Same-feature media can be a repair candidate; older/equal monthly revision is
  explicitly not called a newer OS. Windows Setup still decides what is allowed.
- Windows 11 26H1/build 28000 is device-specific, not an ordinary numerically
  newer upgrade. Recommend it only as same-family repair for an existing 26H1
  system. Do not offer it instead of general-release 26H2/build 26300.
- These source-selection rules do not remove the existing owner-approved manual
  Windows 10 POC behavior or alter the final Core client-upgrade gates.

## Manual LTSC correction

Manual x64 Windows 10/11 **client** LTSC media is now eligible under the shared
`ManualUpgradePolicy`; automatic transfer is still unavailable for LTSC. The
external catalog adapter still falls back for LTSC rather than inventing an exact
LTSC ISO match. The user supplies the exact appropriate file/drive/folder.

Actual image edition (including EnterpriseS/N/IoT variants), system UI language,
architecture, client type and existing target-profile/build rules remain strict.
No LTSC/General Availability edition conversion or product-key injection is added.
The existing Windows 10 POC still delegates intra-Windows-10 build order to Setup;
Windows 11 targets retain equal-or-newer checks and Windows 11 -> 10 stays blocked.
The Windows 11 image retains the experimental `/product server` command profile;
this is not a claim that Microsoft supports every possible LTSC upgrade.

GUI Start/inspection messages, CLI host preflight/status and final Core launch use
the same host gate. Readable images on an unsupported host show the blocker, not
an invitation to click disabled Start. Keep independent image mismatch/error
details. Host eligibility is not proof of an allowed migration or data preservation.
Storage, signature, answer-file, exact selected image, consent and final-media
checks are unchanged. Require Setup to preserve apps/files or stop.

Regression tests first reproduced the disabled LTSC Start on the old code, then
exercise Hebrew Enterprise LTSC, full edition/language rejection, blocked hosts,
automatic exclusion and unchanged command/restart/quiet behavior with fake media.
The owner's manual Setup screenshot showed Keep apps/files, but final acceptance
was pending when this correction was implemented. Do not call it a completed
successful upgrade. See [Microsoft upgrade-path constraints](https://learn.microsoft.com/en-us/windows/deployment/upgrade/windows-upgrade-paths)
before making supported-path claims.

## Server: guidance and user-click command copy only

Server never enables automatic transfer, client Start, quiet mode or restart
controls. It can find a public ISO page and inspect supplied media. No Server
image is implicitly chosen. Only an explicitly selected, matching image enables
**Copy upgrade command**. That click freshly revalidates the exact source and
metadata, checks secondary/selected answer files, then copies and previews a
command; it does not execute it or write upgrade history.

The conservative command builder accepts known 2016/2019/2022/2025 x64 Standard
or Datacenter non-evaluation media with the same edition, system UI language and
Core/Desktop installation type, and no older target feature. Reject other
editions/types and unsafe CMD path characters. This is not a supported-path,
role, cluster, license or backup assessment. The user must check those separately.

Example shape for an elevated **Command Prompt**, not PowerShell:

```bat
"E:\setup.exe" /auto upgrade /noreboot /imageindex 2 /installfrom "E:\sources\install.wim"
```

There is deliberately no `/product server` on the real Server command. The
existing Windows 11 client workaround is a separate concern. `/noreboot` prevents
the first reboot only; later restarts are possible after manual continuation.
Stop if Setup cannot keep applications, settings and files. The Microsoft Server
alternative opens Evaluation Center; check licensing and do not accidentally
convert to evaluation media. Evaluation images are rejected by this command builder.

## CLI and extensibility

`downloads [--json]` calls the same Core recommendation policy without elevation.
It never opens a browser, copies a command, downloads/mounts media or starts Setup.
`status` stays offline. `--download` requires a matching configured own-source
profile; it does not silently download an English ISO for other users. See
[CLI guide](CLI.md) for the existing result/exit contract.

Add future owner sources to the ordered read-only `DownloadSourcePolicy.Sources`
records with edition coverage, language, architecture, build and browser page.
The first exact eligible match wins; the external Consumer mapping is independent
of list order. A new host/transport, release family or package still requires
separate tests and review; a browser page is not automatically a direct-download
API. Never remove certificate validation or reuse the OEM provider fallback for
an unrelated host. Keep provider credentials/session details out of documentation.

## Verification and owner VM gate

Safe fixtures cover all 11 owner editions, 38 exact cultures, Enterprise/N,
Consumer/Business separation, Server/Core, unknown/LTSC/ARM profiles, 26H1 traps,
same-feature repair, malformed/ambiguous/non-ISO HTML, unsafe URLs, bounded HTTP,
redirects and cancellation. GUI effects use fakes for browser/clipboard/preparation;
check persistent manual instructions, stale-source cleanup, cancel/retry and
Server Start lockout. Existing quiet/restart/image/consent/reuse suites still run.

The safe-to-fail external probe requests only Client/Server catalog HTML and
prints recommendation metadata for synthetic profiles. It does not follow
landing pages or download media. Green fixtures/CI do not prove a real upgrade.

Owner acceptance remains: en-US Home/Pro/N and Education automatic transfers;
Spanish Spain/Mexico browser download then exact ISO validation; cancellation,
network failure and recovery; unchanged mounted-ISO reuse; unsafe answer-file
blocks; Server GUI inspection/copy on a disposable VM with **no command execution**.
Use a VM snapshot for any separately authorized actual client upgrade.

## Reference constraints

- [Windows 11 26H1](https://learn.microsoft.com/en-us/windows/whats-new/windows-11-version-26h1):
  device-specific release, not a general in-place update.
- [Server upgrade/migration constraints](https://learn.microsoft.com/en-us/windows-server/get-started/install-upgrade-migrate)
  and [in-place upgrade](https://learn.microsoft.com/en-us/windows-server/get-started/upgrade-in-place):
  supported paths, language/type, roles and licensing require separate checks.
- [Windows Setup switches](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/windows-setup-command-line-options?view=windows-11):
  `/auto upgrade` and the first-reboot scope of `/noreboot`.
