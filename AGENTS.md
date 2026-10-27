# Repository working agreement

## Start here (any AI coding assistant)

Read this file, `docs/AI-MAINTENANCE-PLAYBOOK.md`, and
`docs/DEVELOPMENT-STATUS.md` before making changes. These repository files carry
the project's reusable know-how across assistants and machines. Do not assume
access to prior chats, local Codex skills, personal memories, or machine paths.

## Remote checkpoints

- Keep approved development on a dedicated branch, never directly on `main`.
- Commit and push each bounded, meaningful change with its documentation and
  test evidence. Do not leave completed work only on a local disk.
- Create a Draft PR for a new work branch and keep pushing follow-up checkpoints
  to the same PR. A draft is a backup/review record, not release approval.
- Verify the remote branch commit after pushing. If network/authentication fails,
  explicitly report which work is still local; never claim it has been backed up.
- A request to hold builds/releases does not prevent backing up authorized source
  changes. A request not to edit code must still be respected: document findings
  without implementing them until authorized.
- Do not merge, mark a draft ready, tag, publish a release, or replace existing
  release assets without the owner's explicit authorization. Never auto-merge.

## Recoverable project context

- Keep repository documentation in English. The conversation may be in Hebrew.
- Update `docs/DEVELOPMENT-STATUS.md` with implemented scope, verification,
  remaining work and release holds when these materially change.
- When an investigation produces a validated, reusable lesson, update
  `docs/AI-MAINTENANCE-PLAYBOOK.md` in the same PR. Preserve the reason, safe
  procedure, verification and limitations, not just a summary that a fix worked.
- Put temporary test results and pending hypotheses in the development handoff,
  not in the permanent playbook as established facts. Do not silently overwrite
  a decision; record why its replacement is needed and what evidence supports it.
- Preserve decisions and sanitized acceptance evidence, not full conversations,
  personal Codex memories, credentials, ISOs, or raw customer logs/screenshots.
  This repository is public; inspect the staged diff before each push.
- GitHub backs up tracked, pushed repository files only. Codex chat history and
  local application state are not automatically included.

## Product and safety boundaries

- The application is free to use under the repository's freeware LICENSE, not an
  open-source permission grant. Do not replace it with MIT/GPL or imply source
  reuse/rebranding rights without an explicit owner decision. Preserve upstream
  third-party terms; report security issues privately to javier@pcninja.pro.
- Stable V1 uses technical 1.0.0 and Win-Upgrade-Tool-by-PcNinja-v.1.exe; 1.1.0 uses
  -v.1.1.exe. Display Windows Upgrade Tool by PcNinja; canonical repository is
  JavierTorresFelendler/Win-Upgrade-Tool-by-PcNinja. Keep C# and publisher filename mappings identical and preserve all
  historical Beta/RC names. Beta-to-first-stable is a manual bootstrap, not an
  excuse to bypass newer-version or channel checks.
- Keep the standalone application separate from future WinUpdate Tool integration.
- GUI and CLI must use the same Core validation, download and upgrade operations.
- Manual x64 Windows 10/11 client LTSC upgrades use `ManualUpgradePolicy` and
  exact image edition/language checks. Do not infer manual eligibility from
  automatic-download availability, reintroduce a blanket LTSC host ban, or permit
  edition/channel conversion. Server execution remains blocked.
- Never replace an in-place upgrade with a clean/data-only installation fallback.
- Do not launch Windows Setup or upgrade the development machine during tests.
- Keep automatic-provider transport/session details internal. Prefer the full-profile
  matching owner source, including all eleven configured OEM editions. Its browser
  fallback uses `https://winoemiso.pcninja.pro/`; unmatched users keep the broader
  `https://iso.pcninja.pro` and Microsoft's official alternative.
- Own OEM download coverage is edition/language/x64/client/LTSC based, not a
  pinned target build or released-build allowlist. Keep external catalog feature
  rules separate. Unknown target version stays unknown until actual media
  validation; never remove final host/image/build/storage/answer-file checks.
- External catalog guidance resolves public landing pages only, on explicit request.
  Never substitute a language/edition/architecture or automate host challenges.
  Actual media must still pass Core validation. See `docs/MATCHED-ISO-DOWNLOADS.md`.
- Server is guidance/explicit manual-command-copy only. Never enable or execute
  Setup on Server; preserve the Core guard. Do not confuse the Windows 11 client
  `/product server` workaround with a generic required real-Server switch.
- Keep versioned release artifacts unchanged while the owner tests them. CI test
  artifacts are not product releases or permission to upgrade a machine.

See `docs/GITHUB-WORKFLOW.md` for workflow/recovery details and
`docs/DEVELOPMENT-STATUS.md` for the current handoff.
