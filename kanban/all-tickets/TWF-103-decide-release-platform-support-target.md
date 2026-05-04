---
ticket: TWF-103
agent_level: Medium
role: researcher
requires_qa: false
doc_only: true
dependencies:
  - TWF-053
write_scope:
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-103-decide-release-platform-support-target.md
---

# TWF-103: Decide Release Platform Support Target

## Goal

Decide which Timberborn platforms Wildfire intends to support for the initial Steam Workshop release.

## Why

Cross-platform validation cannot be meaningful until the target is explicit. Unsupported platforms should be documented honestly instead of implied by package contents.

## Requirements

- Identify the initial release platform target.
- Document whether macOS, Windows, Linux, or other Timberborn platforms are supported, unsupported, or unvalidated.
- Define the evidence required for each supported platform.
- Record any environment blockers for validation.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with the decision.

## Dependencies

- `TWF-053` defines the release package workflow that platform support affects.

## Parent Reference

- Parent gate: `TWF-058`.

## Role

- Researcher.
- Follow [researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check`.
- No runtime validation is required for this design ticket.

## Notes

- Do not broaden launch targets without a matching validation path.
