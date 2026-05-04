---
ticket: TWF-056
agent_level: Medium
role: worker
requires_qa: false
doc_only: true
dependencies:
  - TWF-048
  - TWF-055
write_scope:
  - README.md
  - docs/**
  - release/**
  - kanban/all-tickets/TWF-056-write-player-facing-readme-install-docs.md
---

# TWF-056: Write Player Facing README Install Docs

## Goal

Write the player-facing README and install/uninstall/support documentation for release.

## Why

Release docs should explain what Wildfire does, how to install it, how to disable or uninstall it, what settings exist, known limitations, and what evidence is useful when reporting issues.

## Requirements

- Add or update a player-facing `README.md`.
- Document install and uninstall steps for the official Steam Workshop channel.
- Document settings and safe disable behavior.
- Document known limitations and compatibility expectations.
- Document how to find and attach `Player.log`.
- Keep internal sprint, agent, and kanban details out of player docs.
- Link to issue templates once `TWF-062` exists, or leave a placeholder note for that ticket.

## Dependencies

- `TWF-048` defines release settings.
- `TWF-055` provides release metadata and screenshots/copy.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Review the docs for player-facing clarity and absence of internal process noise.

## Notes

- This is release documentation, not development handoff documentation.
