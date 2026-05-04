---
ticket: TWF-109
agent_level: Medium
role: reviewer
requires_qa: false
doc_only: false
dependencies:
  - TWF-048
  - TWF-050
write_scope:
  - scripts/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-109-review-release-scripts-dev-surfaces.md
---

# TWF-109: Review Release Scripts Dev Surfaces

## Goal

Review scripts, debug visibility, and developer-only surfaces for release safety.

## Why

Release packaging should not expose development helpers, unsafe automation, or debug panels as player-facing behavior.

## Requirements

- Inventory release-relevant scripts and development surfaces.
- Confirm development-only scripts are not packaged or are clearly separate from player release artifacts.
- Confirm debug visibility is gated by accepted settings or build mode.
- Add script checks where reasonable.
- Document remaining release-visible development surfaces and why they are safe.

## Dependencies

- `TWF-048` defines release settings.
- `TWF-050` defines safe failure behavior.

## Parent Reference

- Parent gate: `TWF-060`.

## Role

- Reviewer.
- Follow [tech-lead.md](../roles/tech-lead.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if scripts change.

## Notes

- QA command bridge gating belongs to `TWF-107`.
