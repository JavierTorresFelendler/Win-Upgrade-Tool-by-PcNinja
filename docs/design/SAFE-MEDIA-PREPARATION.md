# Safe preparation of unattended installation media

Status: **Design only; not implemented and not approval to upgrade a machine.**
The owner is still field-testing the existing product. This proposal is separate
from [PR #8](https://github.com/JavierTorresFelendler/pcninja-win11-upgrade-assistant/pull/8).
No application version, release tag, executable or active upgrade is changed here.

## Goal and boundaries

When the selected installation ISO contains an answer file, offer to prepare a
writable copy, remove the applicable answer files from that copy, detach identified
interfering ISO mounts with explicit consent, and use the prepared folder for an
in-place upgrade. The original ISO/USB remains unchanged. Removing an answer file
does not turn customized Windows images into verified stock Microsoft media or
guarantee a supported migration; all normal compatibility checks still apply.

The owner intends to leave a small launcher inside PcNinja ISOs. Implement the
preparation operations in reusable Core code so a future ISO launcher can call
them without duplicating safety behavior or becoming a second upgrade engine.
That separate launcher/product is not built by this design PR.

## One-screen automatic/manual flow

Keep the existing two source modes; do not add a third primary radio option or
a multi-page wizard. Show an inline remediation panel when unsafe media is found:

1. Identify the exact selected source and all currently discoverable blocking
   answer files. Separate read-only discovery from destructive cleanup/launch.
2. Explain: "This media contains automatic-install settings. Prepare a safe copy
   before upgrading to keep your files and apps. The original media is unchanged."
   This is the intended purpose, not a guarantee that a customized image is safe.
3. Recommend a suitable local destination automatically. Show exact source,
   destination, required/free space and **Prepare automatically**, **Choose
   folder...**, **Refresh** and **Cancel**. Both routes use the same checks.
4. Confirm preparation and show the exact interfering ISO mounts proposed for
   detachment after successful copying. This consent is distinct from starting
   Windows Setup. Selecting a source is not consent to detach unrelated devices.
5. Copy into a new app-owned staging folder with byte progress and cancellation.
   Never overwrite/merge into an existing user folder or launch source scripts.
6. Remove only known selected-source `Autounattend.xml` / `Unattend.xml` candidates
   in the copied root and `sources`, using the existing narrow cleanup protections.
   Verify absence; do not search/delete arbitrary personal or Windows XML files.
7. Validate the copy, make the assistant independent of removable media, then
   detach each approved interfering Windows-mounted ISO by its exact identity.
8. Rescan all attached media. If any answer-file blocker remains, stop with exact
   instructions. USB, virtual DVD or unknown/in-use mounts may need manual removal
   and Refresh. Never offer a warning-only bypass.
9. Bind selection to the completed prepared folder and its actual image/index.
   Run the final Core safety/storage/compatibility gate, then show the normal
   explicit upgrade confirmation. Preparing files must not start Setup by itself.

Manual mode changes the folder or performs device removal manually; it does not
weaken validation. If the selected writable folder already permits safe narrow
cleanup, retain that existing path instead of forcing an unnecessary full copy.

## Destination selection and storage accounting

- Prefer eligible local fixed data volumes, ordered by usable free space after
  copy and required reserves, not by drive letter. Show the recommended choice
  and allow an explicit alternative. Exclude the source medium, optical volumes,
  reserved/hidden/recovery partitions, unavailable or unwritable destinations.
- Count the actual files to copy, including all SWM parts and a local launcher
  if required; the ISO file size alone is not the extracted payload requirement.
  Include a safety/allocation margin and recheck space before/during copying.
- Use supported NTFS/exFAT destinations initially; no silent FAT32 splitting,
  formatting, repartitioning or automatic deletion to create space. Removable
  destination support must be explicit and tested; recommend local storage first.
- Check the Windows volume's upgrade workspace separately. If staging there,
  require payload + copy margin + the current Core Windows-workspace reserve.
  Another destination cannot solve inadequate Windows-volume workspace.
- If no destination fits, show required/current/missing space, allow another
  drive/USB and Refresh, and wait. Do not silently fall back to a network share.
- Resolve canonical identities before copying. Reject source=destination,
  destinations nested in the source, path traversal, reparse-point traversal,
  reserved OS locations, and any plan that would copy its own staging tree.

## Copy, integrity and lifecycle

- Give each operation a unique private staging directory and ownership marker.
  Record source identity and relative file inventory, never answer-file contents.
- Treat `.partial`/incomplete folders as unusable. Mark completion only after
  copying, bounded cleanup and validation succeed; retain a manifest of the exact
  completed path so later steps do not rediscover it using newest-file guessing.
- Verify copied payload identity against the source, including all split parts.
  Plan efficient streaming hashes and destination verification; timestamps/length
  alone are not proof. Retain Microsoft setup.exe signature and DISM image checks.
- If media is changed, removed, copy fails or Cancel is pressed, do not detach
  sources or launch Setup. Offer retry or removal of only this exact app-owned
  incomplete staging directory after user consent. Never delete an original ISO.
- Keep completed prepared files by default; show their path. Do not auto-clean
  them during Setup/restart or while another process may still need them.
- Running from the ISO is a separate lifecycle hazard: relocate the assistant to
  a validated local app-owned path and relaunch it before detaching its volume.
  Verify the copied assistant's identity and pass a bounded preparation state;
  do not execute arbitrary paths/commands supplied in state. Changing current
  working directory alone does not release a running executable on that ISO.
- If relaunch/handoff cannot be confirmed, keep the source attached and stop.
  No force-killing the assistant or unrelated applications to release a drive.

## Exact detachment, not "unmount everything"

Discover every currently visible answer-file blocker, not only the chosen source.
For each Windows-mounted ISO, bind the root/device to the exact backing image
and answer-file location. Detach **all confirmed unsafe ISO mounts in the approved
list**, including an unsafe selected ISO, but leave safe/unrelated ISOs alone.
Never operate on VHD/VHDX, system/data volumes or a device whose identity is
ambiguous. Revalidate identity and consent immediately before each detach.

Do not modify another mounted ISO's contents. A new interfering source found
after consent requires a new decision or manual removal. A USB, hypervisor-mounted
DVD, locked image or unsupported eject operation remains a hard blocker until
the user removes it; do not control a VM host or disconnect arbitrary hardware.
The final attached-media scan remains mandatory even after every detach succeeds.

## Implementation seams (future authorized work)

- `InstallationSafety.cs`: expose structured read-only blocker discovery without
  weakening `EnsureSafe`; do not parse paths from exception message strings.
- `UpgradeEngine.cs` and storage helpers: separate copy-space planning from ISO
  download-space checks, preserve actual Windows-volume reserve and source binding.
- New small Core service: preparation plan, owned copy, progress/cancel, validated
  local-source result, exact detach capability adapter and relaunch handoff.
- `MainForm.cs`: inline remediation panel using existing styling; no new UI/runtime
  dependency. Retain the portable EXE size limit of 100,000,000 bytes.
- `CliRunner.cs` / `CliHelp.cs`: expose preparation/planning and structured results
  through the same Core service. Dry-run performs no copy/delete/detach/Setup.
  Noninteractive detach requires explicit authorization for the exact planned
  unsafe ISO set; a generic upgrade `--yes` must not grant blanket device removal.
  Final command names/options are intentionally not promised before implementation.
- Future small ISO launcher: reusable bootstrap/handoff contract, not a copied UI
  or a second product release hidden inside this feature.

## Preflight and safe-to-fail probes

The copy operation is complicated; exact mount identity, live media changes and
running-from-ISO handoff are complex and block implementation readiness until
tested. A design PR is not evidence that these capabilities already work.

| Probe | Method and evidence | Pass / partial / fail rule |
| --- | --- | --- |
| Destination/copy | Fixture file trees and simulated capacities; compare source/destination hashes, cancellation and operation log | Pass only with correct byte identity, reserve accounting and no writes outside owned staging; otherwise stop before detach |
| Exact ISO identity | Disposable VM, selected plus secondary unsafe ISO and a safe ISO; record backing-image/device mappings before/after | Pass only when approved unsafe images detach and the safe image remains; unsupported/ambiguous mapping becomes manual blocker |
| Running from ISO | Disposable VM launches assistant from test ISO; verify local process identity and completed copy before detach | Pass only when local handoff survives detach and retains source/index/restart intent; failure keeps original attached |
| Race/failure | Introduce late answer file, locked image, removed destination, changed source, insufficient space and denied consent | Every case must stop without Setup and preserve original media; no partial preparation may be considered complete |

Rollback for these probes is reverting the disposable VM snapshot. Never test
detachment against the developer's real media. Mock/unit tests must not execute
DiskImage detach or Windows Setup. Do not automatically reattach unsafe media
as a failure rollback; present the state and exact next action to the user.

## Acceptance and release gate

- Cover selected/secondary ISO, USB/virtual-DVD manual removal, writable-folder
  cleanup, WIM/ESD/all SWM parts, Unicode/spaces, destination collisions, filesystem
  rules, cancellation, live removal, low space and permission failures.
- Verify GUI/CLI parity and repeat final attached-media safety gates, including a
  late unsafe device. A copied folder must never bypass those gates.
- Test the prepared source's real in-place upgrade in disposable Windows 10/11
  VMs and verify apps/files after reboot, separately from successful copying.
- Do not mark complete on hashes, DISM readability, a root Setup exit code or CI
  alone. No merge, new EXE release, tag or publishing until explicitly authorized.

References: [Dismount-DiskImage](https://learn.microsoft.com/en-us/powershell/module/storage/dismount-diskimage)
requires an exact image/device identity;
[Get-DiskImage](https://learn.microsoft.com/en-us/powershell/module/storage/get-diskimage)
reports image attachment. Host-specific VM eject behavior still needs the probes
above; these APIs do not justify a universal removable-media detach promise.
