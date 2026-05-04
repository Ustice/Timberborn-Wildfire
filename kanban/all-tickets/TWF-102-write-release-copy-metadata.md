---
ticket: TWF-102
agent_level: Medium
role: worker
requires_qa: false
doc_only: true
dependencies:
  - TWF-100
  - TWF-101
write_scope:
  - release/**
  - docs/**
  - kanban/all-tickets/TWF-102-write-release-copy-metadata.md
---

# TWF-102: Write Release Copy Metadata

## Goal

Write the short description, long description, feature bullets, compatibility notes, and known limitations for release.

## Why

Release copy should be honest, player-facing, and aligned with the actual screenshot and gameplay evidence.

## Requirements

- Write short description, long description, feature bullets, compatibility notes, and known limitations.
- Keep claims aligned with accepted tickets and live evidence.
- Store copy and metadata in `release/` or a documented durable path.
- Note screenshot and thumbnail sources.
- Keep internal kanban and agent details out of release copy.

## Dependencies

- `TWF-100` provides the thumbnail/key graphic.
- `TWF-101` provides release screenshots.

## Parent Reference

- Parent gate: `TWF-055`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Review copy against current accepted behavior and known limitations.

## Notes

- Steam Workshop item-specific metadata is finalized in `TWF-112`.
