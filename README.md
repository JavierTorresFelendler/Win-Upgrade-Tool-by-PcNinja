# PcNinja Windows 11 Upgrade Assistant

Private proof of concept for guiding advanced home users through an in-place
Windows 11 upgrade on unsupported x64 hardware.

> [!WARNING]
> This repository is an internal POC. It is not ready for public distribution.
> The `/product server` behavior used for client upgrades is undocumented and
> may change in future Windows releases.

## Current workflow

1. The launcher requests Administrator permission.
2. The user can select an existing Windows 11 x64 ISO or ask the assistant to
   open [11oem.pcninja.pro](https://11oem.pcninja.pro) to download one.
3. The user selects the ISO file from the local computer.
4. The assistant mounts the selected ISO and validates Microsoft's signature on
   `setup.exe`.
5. The CMD helper starts the in-place upgrade.
6. Windows Setup controls the normal restart process.

## Running the POC

1. Save your work and back up important files.
2. Double-click `Start-PcNinja-Win11-Upgrade.cmd`.
3. Approve the Administrator prompt.
4. Choose an existing ISO or open the optional PcNinja download page.
5. Select the ISO and follow the final confirmation prompt.

Runtime logs are written to:

```text
C:\PcNinja\Win11UpgradeAssistant\Upgrade-Assistant.log
```

## Upgrade command

```cmd
setup.exe /product server /auto upgrade /eula accept /dynamicupdate disable /compat ignorewarning
```

The helper deliberately does not use `/noreboot` and does not issue a
`shutdown` command. Windows Setup owns the natural restart sequence.

## Current scope

- Windows 10 and Windows 11 client systems
- x64 only
- Existing local ISO or an ISO downloaded through the optional PcNinja portal
- In-place upgrade only
- No Windows Server or LTSC support

The upgrade workflow does not depend on the PcNinja portal. If the portal is
unavailable, the user can provide an ISO obtained from another trusted source.

## Repository status

The repository remains private while the workflow is validated. The intended
next major milestone is a signed Windows EXE with a graphical interface.
