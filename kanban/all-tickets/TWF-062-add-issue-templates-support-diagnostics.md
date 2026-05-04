---
ticket: TWF-062
agent_level: Low
role: worker
requires_qa: false
doc_only: true
dependencies:
  - TWF-056
  - TWF-060
write_scope:
  - .github/**
  - README.md
  - docs/**
  - kanban/all-tickets/TWF-062-add-issue-templates-support-diagnostics.md
---

# TWF-062: Add Issue Templates Support Diagnostics

## Goal

Add GitHub issue templates and support instructions that collect the diagnostics needed to debug release problems.

## Why

Public releases produce vague bug reports unless the project asks for the right evidence. Wildfire needs Timberborn version, OS, mod version, log snippets, save/load state, settings, reproduction steps, and screenshots when visual behavior is involved.

## Requirements

- Add GitHub issue templates for bug reports and compatibility reports.
- Ask for Timberborn version, OS, Wildfire version, distribution channel, settings, reproduction steps, expected behavior, actual behavior, and `Player.log`.
- Include instructions for finding `Player.log`.
- Include guidance for attaching screenshots or release package version/checksum when relevant.
- Link support instructions from README or release docs.

## Dependencies

- `TWF-056` creates player-facing docs.
- `TWF-060` defines release-safe diagnostics and command surfaces.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Review issue templates for clear player-facing wording.

## Notes

- Keep templates concise enough that players will actually complete them.
