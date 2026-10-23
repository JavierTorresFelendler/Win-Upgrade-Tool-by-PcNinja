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

- Keep the standalone application separate from future WinUpdate Tool integration.
- GUI and CLI must use the same Core validation, download and upgrade operations.
- Never replace an in-place upgrade with a clean/data-only installation fallback.
- Do not launch Windows Setup or upgrade the development machine during tests.
- Keep raw automatic-provider details internal. The owner-approved public OEM
  route is `https://11oem.pcninja.pro/` for matching x64/en-US Home/Pro/Education
  editions on Windows 10/11, including their supported N/SL/Workstation variants.
  Use shared Core routing for browser fallback and the PcNinja download button;
  keep `https://iso.pcninja.pro` for other systems/languages and Microsoft's
  official alternative. Do not disable language or actual image validation.
- Keep versioned release artifacts unchanged while the owner tests them. CI test
  artifacts are not product releases or permission to upgrade a machine.

See `docs/GITHUB-WORKFLOW.md` for workflow/recovery details and
`docs/DEVELOPMENT-STATUS.md` for the current handoff.
