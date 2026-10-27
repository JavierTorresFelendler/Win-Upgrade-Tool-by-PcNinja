# Credits and external ISO sources

## Thank you to the Massgrave contributors

PcNinja gratefully acknowledges the work of the
[Massgrave project](https://massgrave.dev/) and its contributors in maintaining
public Windows ISO download catalogs. Their catalog coverage is an important part
of this tool's usefulness for users whose installed Windows language or edition
is not covered by PcNinja's configured automatic-download source.

The ability to find appropriately matched media in additional languages and
editions makes a substantial difference to the guided upgrade experience.
We appreciate the effort involved in keeping these references available and
want that contribution to be clearly visible rather than hidden in implementation
details.

Project and source links:

- [Massgrave website](https://massgrave.dev/)
- [Windows 11 ISO catalog](https://massgrave.dev/windows_11_links)
- [Windows Server ISO catalog](https://massgrave.dev/windows-server-links)
- [Website/catalog source on GitHub](https://github.com/massgravel/massgrave.dev)
- [Massgrave's main project on GitHub](https://github.com/massgravel/Microsoft-Activation-Scripts)
- [Website repository's upstream license](https://github.com/massgravel/massgrave.dev/blob/main/LICENSE)

## What PcNinja uses

On explicit request for a matching download, the assistant first checks whether
a configured PcNinja ISO source covers the installed system's complete profile.
A matching PcNinja source stays preferred. For other supported profiles, the
assistant can read the public catalog and recommend an exact-language/edition/
architecture media landing page. The user completes that download in a browser
and then selects the exact ISO in the tool.

This catalog adapter does not automate file-host challenges, extract host session
tokens, or download the external ISO on the user's behalf. If catalog markup
changes, a match is ambiguous or the page is unavailable, it falls back to manual
guidance instead of substituting a different edition, architecture or language.
Microsoft's official download alternative remains available.

See [matching download guidance](MATCHED-ISO-DOWNLOADS.md) for the precise matching,
network bounds and safety policy. A download recommendation is not a validated
installation source: actual ISO/media validation, edition/language/image checks,
answer-file safety, storage checks and the in-place-upgrade-only policy still apply.
Server recommendations do not enable Server upgrade execution in this product.

## Boundaries of this acknowledgement

This integration references ISO catalogs only. It does **not** include or execute
Microsoft Activation Scripts, change activation, supply licenses or change the
licensing of Windows. Linking to the contributors' main project is attribution,
not a product feature or an instruction to use its activation functionality.

PcNinja is an independent project. These credits do not claim formal partnership,
endorsement, certification or support by Massgrave or Microsoft. External projects,
downloaded installation media, names and trademarks remain subject to their own
terms and rights. The PcNinja [license](../LICENSE) does not replace upstream terms.

## PcNinja and the Windows platform

The standalone interface, upgrade orchestration, validation, diagnostics and
consent-based portable updater are maintained by PcNinja. Product branding and
the PcNinja website links remain intact.

Windows, Windows Setup and the .NET/Windows Forms platform are Microsoft
technologies; their availability and behavior do not constitute Microsoft's
endorsement of this tool or the experimental compatibility method.

The self-contained Windows build uses the .NET 8 runtime and Windows Forms.
Their official source, licenses and third-party notices are maintained upstream:

- [.NET runtime](https://github.com/dotnet/runtime),
  [license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT),
  [third-party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT)
- [Windows Forms](https://github.com/dotnet/winforms),
  [license](https://github.com/dotnet/winforms/blob/main/LICENSE.TXT),
  [third-party notices](https://github.com/dotnet/winforms/blob/main/THIRD-PARTY-NOTICES.TXT)

Those component rights and redistribution notices remain separate from PcNinja's
free-use-only application license. PcNinja does not claim ownership of the runtime
or replace its upstream terms. Follow the notices accompanying the distributed
components; the links here are attribution and reference, not a substitute for
their applicable notices.

For private security reports use **javier@pcninja.pro** and
[SECURITY.md](../SECURITY.md). Do not send sensitive reports to an external ISO
catalog project merely because its catalog was used by this assistant.
