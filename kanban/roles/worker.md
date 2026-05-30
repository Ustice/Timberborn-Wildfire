# Worker Role Instructions

Use these instructions for every Wildfire issue worker unless the issue says otherwise.

## Worktree

- Work in your coordinator-assigned ticket worktree and branch only.
- Verify the assigned worktree path and branch with `git status --short --branch` before editing.
- Read the assigned `CONTEXT.md` when it exists. Update it only when durable task state changes, and keep it compressed rather than append-only.
- If the worktree or branch is missing, wrong, detached unexpectedly, or dirty with unrelated changes, stop and report it to the coordinator instead of creating, renaming, or moving the allocation yourself.
- Do not touch historical ticket board status files unless the coordinator explicitly assigns migration cleanup.
- Do not treat historical ticket notes or status symlinks inside your worktree as authoritative.
- Read the assigned GitHub issue, then report notes and evidence back to the coordinator unless direct issue updates are explicitly assigned.

## Inputs

- Read `AGENTS.md`.
- Read `docs/INDEX.md`.
- Read `kanban/github-issue-workflow.md`.
- Read `kanban/roles/worker.md`.
- Read your assigned GitHub issue.
- Read source docs only as needed for the assigned ticket.
- Read the assignment packet from the coordinator if one is provided.

## Scope

- Stay inside the issue's explicit write scope.
- Do not overlap another worker's write scope unless the coordinator approves it.
- Do not update milestone/status docs unless the issue explicitly includes those files in `write_scope`.
- Report worker notes, evidence, blockers, and completion details to the coordinator for GitHub issue updates.
- Keep `Wildfire.Core` host-agnostic.
- Do not introduce Timberborn or Unity dependencies into core code.
- Preserve deterministic simulation behavior.

## Verification

- Run `git diff --check`.
- Run `dotnet test` for code, content, script, or behavior changes.
- Skip runtime verification for documentation-only issues marked `doc_only: true` when the diff only changes documentation.

## Final Report

Report:

- Changed files.
- Commit SHA if committed.
- Tests and checks run, with outcomes.
- Unresolved unknowns or blockers.
- Short behavior or architecture summary.
- Issue notes the coordinator should add.
- Process Feedback:
  - Friction or issues encountered.
  - Reusable lessons from retries or pivots, including what you would repeat or change next time.
  - Suggested process or tooling improvements.
