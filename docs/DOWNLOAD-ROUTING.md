# PcNinja download routing

Development follow-up, not part of the published RC7 EXE. This supersedes the
earlier catalog-only browser fallback for matching OEM clients.

| System | PcNinja button / interactive browser fallback |
| --- | --- |
| Windows 10/11, x64, system UI en-US and an edition below | https://11oem.pcninja.pro/ |
| Other language, architecture, edition or unrecognized client build | https://iso.pcninja.pro |

Recognized EditionIDs: Core, CoreN, CoreSingleLanguage, Education, EducationN,
Professional, ProfessionalN, ProfessionalEducation, ProfessionalEducationN,
ProfessionalWorkstation and ProfessionalWorkstationN. This is the configured
multi-edition ISO coverage, not a live query of the provider's current contents.
Registry EditionID/system UI language are reused from the image-selection policy;
ProductName and the ISO filename are not compatibility evidence.

Automatic transfer still uses the OEM short link and its existing secure internal
provider fallback for Windows 10 transport failures. Raw provider details stay
internal. Automatic language eligibility and final signature/image/storage/answer-
file checks are unchanged; a routing match is not approval to launch Setup.
Windows 10 as the source system still downloads Windows 11 media. Manual Windows
10 target upgrades use an existing ISO/folder as before.

GUI fallback stops automatic continuation; after the browser download, select the
exact ISO. Quiet, JSON or redirected CLI never opens a browser; status reports
pcNinjaDownloadPage and download-failure guidance includes the resolved public URL.
Microsoft's official manual option stays unchanged.

The prior-build cancellation test showed exact partial-file resume, then a network
response timeout and browser fallback, followed by /quiet preparation exit 0.
This change fixes the browser destination only, not timeout/retry/cancellation
responsiveness. No real provider download or Windows Setup is needed for fixtures.
