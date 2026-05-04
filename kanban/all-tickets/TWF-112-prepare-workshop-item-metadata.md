---
ticket: TWF-112
agent_level: Medium
role: worker
requires_qa: false
doc_only: true
dependencies:
  - TWF-055
  - TWF-057
  - TWF-061
write_scope:
  - release/**
  - docs/**
  - kanban/all-tickets/TWF-112-prepare-workshop-item-metadata.md
---

# TWF-112: Prepare Workshop Item Metadata

## Goal

Prepare the Workshop-specific title, descriptions, tags, thumbnail, screenshots, compatibility notes, and changelog.

## Why

Generic release copy still needs to be shaped into the fields Steam Workshop expects. This ticket keeps metadata assembly separate from package shape and install testing.

## Requirements

- Assemble Workshop title, short description, long description, tags, thumbnail, screenshots, compatibility notes, and changelog.
- Use outputs from `TWF-055`, `TWF-057`, and `TWF-061`.
- Keep metadata honest about current behavior, supported platforms, and known limitations.
- Store metadata in `release/` or a documented durable path.
- Preserve image and attribution references.

## Dependencies

- `TWF-055` creates graphics and metadata.
- `TWF-057` defines versioning and changelog rules.
- `TWF-061` completes license and attribution review.

## Parent Reference

- Parent gate: `TWF-063`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Review metadata against current release evidence and limitations.

## Notes

- Upload/update process belongs to `TWF-111`.
