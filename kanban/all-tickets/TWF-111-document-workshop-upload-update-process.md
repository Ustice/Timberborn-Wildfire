---
ticket: TWF-111
agent_level: Medium
role: worker
requires_qa: false
doc_only: true
dependencies:
  - TWF-110
write_scope:
  - release/**
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-111-document-workshop-upload-update-process.md
---

# TWF-111: Document Workshop Upload Update Process

## Goal

Document the manual or scripted process for creating and updating the Steam Workshop item.

## Why

The first release may not automate Workshop publishing. The process still needs to be repeatable and clear enough that release evidence can be reproduced.

## Requirements

- Document whether Workshop upload automation is feasible or intentionally manual for the first release.
- Document each manual or scripted step for creating and updating the Workshop item.
- Record required accounts, tools, package inputs, and expected outputs.
- Preserve the Workshop item id or planned placeholder once available.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with the accepted process or blockers.

## Dependencies

- `TWF-110` confirms the package shape.

## Parent Reference

- Parent gate: `TWF-063`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Review the process for repeatability and missing credentials/tooling blockers.

## Notes

- This ticket documents the path; `TWF-113` validates install from the Workshop path.
