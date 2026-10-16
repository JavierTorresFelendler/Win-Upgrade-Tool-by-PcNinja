# GitHub Development Workflow

This project is managed under the `JavierTorresFelendler` account. No additional
reviewer is required: you decide when a change is ready and when to merge it.
A pull request (PR) is the record of the work and its testing, not a requirement
to obtain someone else's approval.

## Standard workflow

`Issue → Branch → Commits → Draft PR → Testing → Merge → Release`

1. **Issue - define the work:** Describe the bug or requirement, acceptance
   criteria, and exclusions. Group tasks for the same version under a milestone.
   A task appearing in the backlog does not mean it is already implemented.
2. **Branch - isolate the work:** Create a branch from `main`, such as
   `fix/answer-file-safety`. Changes stay on that branch and do not change the
   published release.
3. **Commit - save a checkpoint:** Save a small, focused change with a clear
   description and its test result. You can inspect exactly what changed between
   checkpoints without rewriting history.
4. **Draft PR - collect the work and evidence:** Open a PR targeting `main`, link
   the related issues, describe completed and pending work, and push additional
   commits to the same branch. Do not open a new PR for every small correction.
5. **Testing:** GitHub runs checks and builds a test EXE with a SHA-256 checksum
   and source-commit information. Open the run through **Checks** or **Actions**,
   then download its **Artifacts** at the bottom of the run page. This is a test
   build, not a release. A different commit requires retesting the new artifact.
6. **Merge - your decision:** After testing, select **Ready for review**, inspect
   **Files changed** and the checklist, then select **Squash and merge**. You do
   not need to approve your own PR or obtain another user's review. Do not enable
   auto-merge as part of this workflow.
7. **Release - a separate publication step:** Only after an explicit decision,
   prepare the version number, release notes, and tag. In this repository, pushing
   a `v*` tag starts the existing release workflow. Do not create or move release
   tags as routine checkpoints. Merging a PR alone does not publish an EXE under
   **Releases**.

## Automated checks and manual testing

The `PR validation` check runs Core smoke tests, builds the portable EXE, checks
CLI commands that do not start installation, and enforces a maximum EXE size of
100,000,000 bytes. The workflow has no permission to write repository code or
publish releases. Test artifacts are retained for 30 days; commits, tags, and
documentation provide the permanent recovery checkpoints.

The GitHub runner is not a Windows 10/11 client test machine. Dedicated GUI
checks, real upgrades, preservation of files and applications, unsupported
hardware, USB behavior, and media containing answer files still require testing
on the test machines. Passing CI does not prove that an upgrade is safe. Never
launch Windows Setup from CI.

In the initial workflow-preparation PR, application code and the product version
remain RC4. Its EXE only verifies the baseline build; it does not include the RC5
requirements tracked in the issues.

## Recovery checkpoints

| Checkpoint | What is preserved | Recovery without losing history |
| --- | --- | --- |
| Before the work cycle | Tag `v1.1.0-rc.4`, commit `2e65c1e83f729a03909f393666b6abc368b6ea94` | Use the RC4 EXE from Releases or create a new branch from the tag. |
| During development | Branch commits and the PR discussion | Add a corrective commit or revert a specific commit. |
| Before merging | Tested commit, CI results, SHA-256, and test report | Do not merge a failed candidate; continue fixing it in the same PR. |
| After merging | Merged PR and its squash commit | Open a revert PR, test it, and merge it; do not force-push to `main`. |
| After publication | Version tag, EXE, and checksum | Stop recommending the faulty release, direct users to the previous artifact, and publish a fix under a new tag. |

Do not replace an old release asset or move its tag to make the original release
appear correct. Temporary artifacts are not a substitute for an approved release.
Keep sanitized test evidence; do not commit ISOs or raw customer logs to the
public repository.

Important: reverting application code does not restore a Windows installation
that has already been upgraded or erased. Recovery requires a backup or test-VM
snapshot created before installation.

## Protecting main without making it read-only

The recommendation is to protect `main`, not enable **Lock branch**:

- Require changes through a PR with **0 required approvals**, no required Code
  Owner approval, and no approval requirement for the most recent push. This
  avoids dependence on another person.
- Require `PR validation` to pass and require the branch to be up to date with
  `main` before merging.
- Block force pushes and deletion of `main`, and apply the rules to repository
  administrators as well.
- Keep development branches available for normal work. If `main` advances,
  update the working branch, resolve conflicts, and rerun checks before merging.

Once these protections are enabled, direct pushes to `main` are rejected. You
continue editing and committing on a working branch, open a PR, and merge it
yourself when the checks pass. Development is not blocked, and no second GitHub
account is needed. A failing CI check can block merging until it is fixed. The
repository owner can change the policy in settings, but exceptions should be
rare and documented.

Protection cannot prevent someone using your account from merging on your
behalf. The working instruction therefore remains: **Do not merge or publish a
release without your explicit request.** No branch-protection settings were
changed by the workflow-preparation PR; the owner manages those separately.

References: [GitHub flow](https://docs.github.com/en/get-started/using-github/github-flow),
[Protected branches](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches).

## Requesting the next work cycle

> Work on issue X on a separate branch and open a Draft PR under my account.
> Include documentation, tests, and a test EXE from the PR. Keep commits focused
> and preserve a recoverable baseline. Do not merge or publish a release until
> I have tested the result and explicitly requested it.
