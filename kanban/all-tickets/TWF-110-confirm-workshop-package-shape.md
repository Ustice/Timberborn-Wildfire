---
ticket: TWF-110
agent_level: Medium
role: researcher
requires_qa: false
doc_only: false
dependencies:
  - TWF-053
write_scope:
  - release/**
  - scripts/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-110-confirm-workshop-package-shape.md
---

# TWF-110: Confirm Workshop Package Shape

## Goal

Confirm and document the package shape expected by Steam Workshop for Timberborn.

## Why

The Workshop package shape is a separate concern from generic GitHub release artifacts. The release archive must match the path, manifest, and file expectations players will actually install.

## Requirements

- Confirm the package shape expected by Steam Workshop for Timberborn mods.
- Compare that shape to the artifact created by `TWF-053`.
- Update packaging scripts if the Workshop shape differs from the generic release package.
- Preserve package listing or evidence in `docs/TEST_PLAN.md`.
- Record any uncertainty or manual validation still needed.

## Dependencies

- `TWF-053` creates the release package.

## Parent Reference

- Parent gate: `TWF-063`.

## Role

- Researcher.
- Follow [researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if scripts change.

## Notes

- Upload/update process belongs to `TWF-111`.
