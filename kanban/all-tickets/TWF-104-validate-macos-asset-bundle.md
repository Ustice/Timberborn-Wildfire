---
ticket: TWF-104
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
  - kanban/all-tickets/TWF-104-validate-macos-asset-bundle.md
---

# TWF-104: Validate macOS Asset Bundle

## Goal

Validate the release macOS asset bundle from the packaged artifact.

## Why

Current live proof is macOS-focused, but release validation should still prove the packaged bundle, not only the development deploy path.

## Requirements

- Build or locate the macOS bundle in the release package.
- Confirm package paths, names, and manifests match Timberborn expectations.
- Install from the packaged artifact or closest release-candidate path.
- Capture live load evidence and relevant `Player.log` tokens.
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
- Preserve bundle build logs, package contents, and live load evidence.

## Notes

- Windows validation belongs to `TWF-105`.
