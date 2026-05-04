# Worker Role Instructions

Use these instructions for every Wildfire ticket worker unless the ticket says otherwise.

## Worktree

- Work in your assigned ticket worktree only.
- Use a worktree name that includes the ticket number.
- Do not touch ticket board status files unless the coordinator explicitly assigns that as part of your scope.
- Do not treat ticket notes or status symlinks inside your worktree as authoritative.
- Read the canonical ticket from the coordinator's main checkout path when assigned, then report notes and evidence back to the coordinator unless ticket editing is explicitly in your write scope.

## Inputs

- Read `AGENTS.md`.
- Read `docs/INDEX.md`.
- Read `kanban/README.md`.
- Read `kanban/roles/worker.md`.
- Read your assigned ticket file.
- Read source docs only as needed for the assigned ticket.
- Read the assignment packet from the coordinator if one is provided.

## Scope

- Stay inside the ticket's explicit write scope.
- Do not overlap another worker's write scope unless the coordinator approves it.
- Do not update milestone/status docs unless the ticket explicitly includes those files in `write_scope`.
- Report worker notes, evidence, blockers, and completion details to the coordinator for canonical ticket updates.
- Keep `Wildfire.Core` host-agnostic.
- Do not introduce Timberborn or Unity dependencies into core code.
- Preserve deterministic simulation behavior.

## Verification

- Run `git diff --check`.
- Run `dotnet test` for code, content, script, or behavior changes.
- Skip runtime verification for documentation-only tickets marked `doc_only: true` when the diff only changes documentation.

## Final Report

Report:

- Changed files.
- Commit SHA if committed.
- Tests and checks run, with outcomes.
- Unresolved unknowns or blockers.
- Short behavior or architecture summary.
- Ticket notes the coordinator should add to the canonical ticket.
