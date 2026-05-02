---
ticket: TWF-063
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-053
   - TWF-055
   - TWF-056
   - TWF-057
   - TWF-059
   - TWF-061
write_scope:
   - release/**
   - scripts/**
   - README.md
   - docs/TEST_PLAN.md
   - docs/HANDOFF.md
   - kanban/all-tickets/TWF-063-prepare-steam-workshop-distribution.md
---

# TWF-063: Prepare Steam Workshop Distribution

## Goal

Prepare Steam Workshop as the official initial Wildfire distribution channel.

## Why

Steam Workshop is the primary place Timberborn players expect to install mods. Other channels can be considered later, but release prep should first make the Workshop package, metadata, preview assets, and validation path solid.

## Requirements

- Confirm the release package shape expected by Steam Workshop for Timberborn.
- Prepare Workshop title, short description, long description, tags, thumbnail, screenshots, compatibility notes, and changelog.
- Ensure packaged files match the artifact validated by release-candidate QA.
- Document the manual or scripted upload/update process.
- Record whether Workshop upload automation is feasible or intentionally manual for the first release.
- Preserve the Steam Workshop item id or planned placeholder once created.
- Run a private or limited visibility Workshop install test if Steam supports it for this workflow.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with Workshop release evidence and blockers.

## Dependencies

- `TWF-053` creates the release package.
- `TWF-055` creates graphics and metadata.
- `TWF-056` creates player-facing docs.
- `TWF-057` defines versioning and changelog rules.
- `TWF-059` validates the release candidate.
- `TWF-061` completes license and attribution review.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if scripts change.
- QA must install from the Workshop path or record the exact blocker preventing that test.

## Notes

- GitHub artifacts remain useful for reproducibility, but Steam Workshop is the official first public distribution channel.
