---
ticket: TWF-113
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-059
  - TWF-110
  - TWF-111
  - TWF-112
write_scope:
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - release/**
  - kanban/all-tickets/TWF-113-run-private-workshop-install-test.md
---

# TWF-113: Run Private Workshop Install Test

## Goal

Install and validate Wildfire from the Steam Workshop path, using private or limited visibility if available.

## Why

Clean install from a local release artifact is not the same as Workshop distribution. The official first channel needs its own install proof or a concrete blocker.

## Requirements

- Use the Workshop package shape and upload/update process from the child tickets.
- Run a private or limited visibility Workshop install test if Steam supports the workflow.
- Confirm Timberborn discovers the mod from the Workshop path.
- Load a save and run the coherent gameplay loop or release-candidate smoke path.
- Preserve `Player.log`, screenshots, Workshop item id or placeholder, commands, package checksum, and artifact paths.
- If the test cannot run, record the exact blocker and required account/tooling/state.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with evidence or blockers.

## Dependencies

- `TWF-059` validates the release candidate from a clean install path.
- `TWF-110` confirms Workshop package shape.
- `TWF-111` documents upload/update process.
- `TWF-112` prepares Workshop metadata.

## Parent Reference

- Parent gate: `TWF-063`.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Passing evidence requires Workshop-path install proof or a precise blocker.

## Notes

- Do not satisfy this with development deploy or local clean-install artifact evidence alone.
