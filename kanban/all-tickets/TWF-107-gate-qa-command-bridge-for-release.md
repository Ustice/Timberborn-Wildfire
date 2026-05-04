---
ticket: TWF-107
agent_level: Medium
role: reviewer
requires_qa: true
doc_only: false
dependencies:
  - TWF-048
  - TWF-050
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-107-gate-qa-command-bridge-for-release.md
---

# TWF-107: Gate QA Command Bridge For Release

## Goal

Review and gate QA command bridge commands, file inbox/outbox behavior, and destructive mutation surfaces for release builds.

## Why

The command bridge is useful for QA but should not become an accidental player tool, support liability, or unsafe mutation surface.

## Requirements

- Inventory QA command bridge commands and file inbox/outbox behavior.
- Ensure destructive or broad mutation commands are absent or disabled in release.
- Keep read-only diagnostics available only where safe and intentional.
- Add tests for command rejection or release gating where possible.
- Document what remains available in release and why.

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
- Run `dotnet test`.
- QA must capture at least one release-mode command rejection or gated behavior proof.

## Notes

- Log policy belongs to `TWF-108`; scripts/dev surfaces belong to `TWF-109`.
