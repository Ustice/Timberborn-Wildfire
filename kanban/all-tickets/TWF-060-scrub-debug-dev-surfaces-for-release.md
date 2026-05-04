---
ticket: TWF-060
agent_level: Medium
role: reviewer
requires_qa: true
doc_only: false
dependencies:
  - TWF-048
  - TWF-050
  - TWF-107
  - TWF-108
  - TWF-109
write_scope:
  - src/Wildfire.Timberborn/**
  - scripts/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-060-scrub-debug-dev-surfaces-for-release.md
---

# TWF-060: Scrub Debug Dev Surfaces For Release

## Goal

Integrate command-bridge gating, release log policy, and script/developer-surface review so release builds expose only safe player-facing behavior.

## Why

The project intentionally uses command bridges and QA utilities. Release needs those surfaces allowlisted, gated, quiet, and non-destructive so they do not become accidental player tools or security holes.

## Requirements

- Confirm `TWF-107` gates QA command bridge commands and file inbox/outbox behavior.
- Confirm `TWF-108` defines release log/noise policy.
- Confirm `TWF-109` reviews scripts, debug visibility, and developer surfaces.
- Add tests for command rejection or release gating where possible.
- Document what remains available in release and why.

## Dependencies

- `TWF-048` defines release settings.
- `TWF-050` defines safe failure behavior.

## Role

- Reviewer.
- Follow [tech-lead.md](../roles/tech-lead.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if scripts change.
- Run `dotnet test`.
- QA must capture at least one release-mode command rejection or gated behavior proof.

## Notes

- This ticket may produce small code changes if review finds unsafe release surfaces.
- Child tickets: `TWF-107`, `TWF-108`, and `TWF-109`.
