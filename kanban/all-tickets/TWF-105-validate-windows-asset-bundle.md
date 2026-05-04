---
ticket: TWF-105
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-050
  - TWF-053
  - TWF-103
write_scope:
  - scripts/**
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-105-validate-windows-asset-bundle.md
---

# TWF-105: Validate Windows Asset Bundle

## Goal

Validate the release Windows asset bundle, or record the exact environment blocker.

## Why

Steam Workshop users may be on Windows. If Windows is supported, the bundle needs evidence; if it is not validated, release copy must say so clearly.

## Requirements

- Build or locate the Windows bundle in the release package.
- Confirm package paths, names, and manifests match Timberborn expectations.
- Capture live load evidence on Windows if the environment is available.
- If the environment is unavailable, move or mark the ticket with the exact blocker and required machine/tooling.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with evidence or blockers.

## Dependencies

- `TWF-050` hardens asset failure modes.
- `TWF-053` creates the package layout.
- `TWF-103` defines the support target.

## Parent Reference

- Parent gate: `TWF-058`.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if scripts change.
- Preserve bundle build logs, package contents, and live load evidence or blocker details.

## Notes

- Do not let unavailable Windows hardware silently pass the parent ticket.
