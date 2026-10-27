# Repository and publication security

These controls protect development and future releases. They do not modify an
already-downloaded EXE or add cryptographic publisher authentication to V1.

## Repository controls

- Secret scanning and push protection detect supported credential patterns;
  they are not proof that all secrets or vulnerabilities have been found.
- External Actions must use full commit SHAs. Current workflows pin the actions
  they consume; dependency changes still require review.
- The `Protect release tags` ruleset prohibits updating/deleting `v*` tags,
  with no bypass actors. Creating a new version tag remains the release path.
- Repository release immutability is enabled for **future publications only**.
  Draft assets are uploaded/verified before publishing, then GitHub locks assets
  and the tag. V1's previously published assets do not become immutable retroactively.
- Account authentication is managed by the owner. This hardening does not change
  the owner's existing two-factor authentication, account recovery or credentials.

## Separated build and publication

1. `build` has `contents: read` and `actions: read`, never repository write.
   It checks out the exact triggering SHA without persisting credentials, tests
   and prepares the final bytes, then uploads one immutable Actions artifact.
2. Tag builds verify the pre-created `release` environment. It requires exactly
   the owner reviewer, permits deliberate owner self-review, disallows admin
   bypass, and permits only tag pattern `v*`, not branches. Missing/unprotected
   environments fail closed instead of silently creating a usable gate.
3. The tag-only `publish` job waits for successful build and human owner approval
   in that environment. Only this job receives `contents: write`.
4. After approval, it rechecks environment policy and binds the exact artifact ID
   to this run, source SHA, repository/head repository and upload SHA-256 digest.
   The pinned downloader treats digest mismatch as an error, never a warning.
5. It downloads that artifact, rechecks stable/main or Beta/exact-PR-CI ancestry,
   and invokes the existing draft-first publisher. It does not rebuild, regenerate
   manifests/checksums or execute the candidate in the privileged job.

Only scoped built-in job tokens are used; no long-lived publishing PAT is added.
Do not weaken the gate to recover a failed build. Inspect failed checks and any
existing draft rather than moving tags, replacing assets or using `--clobber`.

## Owner action on the next release

After the tagged build completes, open its Actions run, select **Review
deployments**, inspect the source/version and approve the `release` deployment.
The owner can approve their own release; another person is not required. Neither
the workflow nor an assistant should automatically approve or bypass this step.

An ordinary branch `workflow_dispatch` can test the read-only build; its publisher
is skipped. It is not a release and does not test an actual approved tag publication.

## Limits and next step

These are GitHub-side protections, not an independent publisher trust root.
Control of the owner's account may permit changing repository configuration or
authoring a new malicious release. A matching hash hosted beside the EXE does
not distinguish that attacker from the publisher.

Independent signed update metadata with a private key protected separately from
GitHub is a later product change, not implemented here. A Windows-trusted EXE
certificate is also separate. Do not claim that V1 now requires signed updates.

Offline checks: `tests/Verify-ReleaseWorkflow.ps1`,
`tests/Verify-PublicationPolicy.ps1` and `tests/Verify-AppUpdatePackaging.ps1`.
They use synthetic failures and never approve, publish, install or run Setup.

References: [GitHub secret protection](https://docs.github.com/en/code-security/getting-started/github-security-features),
[deployment protection](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments),
[immutable releases](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/establish-provenance-and-integrity/prevent-release-changes).
