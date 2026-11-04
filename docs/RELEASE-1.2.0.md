# Windows Upgrade Tool by PcNinja — 1.2

- Hide and collapse automatic download controls when the existing eligibility
  policy does not permit them. Keep manual ISO, mounted drive and folder selection.
- Place **Find matching ISO** first among manual download options and highlight
  it when automatic download is unavailable.
- On Windows Server, use **Copy upgrade command** in the main header after
  validating media and explicitly selecting a compatible image. The compact
  read-only preview appears after copying; the assistant does not execute it.
- Preserve source/image/restart choices, mismatched-media explanations, existing
  cancellation behavior and all Core/CLI download and Setup safety checks.

Technical product version: `1.2.0`; Windows file version: `1.2.0.0`.
All new downloads use `Win-Upgrade-Tool-by-PcNinja-v.1.2.exe`, preserving the
filename contract in the updater already installed with V1.
The product name and branding remain Windows Upgrade Tool by PcNinja.

Existing stable V1 users receive an ordinary application-update offer. Installation
requires their consent. The updater retains the verified predecessor until the
new GUI reports readiness; it does not perform a Windows upgrade or restart.
Older Beta channels retain their existing channel policy and require manual
stable installation. Historical release assets remain unchanged.

This EXE remains unsigned. Windows Server is inspection/download/manual-command
guidance only. Verify the supported Server upgrade path and keep backups; the
assistant never starts Server Setup.
