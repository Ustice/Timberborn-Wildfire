---
ticket: TWF-137
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-114
  - TWF-117
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Timberborn/**
  - scripts/**
  - tests/**
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-137-define-shared-material-field-schema.md
---

# TWF-137: Define Shared Material Field Schema

## Goal

Define one shared schema for converting Timberborn world objects into Wildfire simulation input and companion field data. This schema must be usable by live C# import and TypeScript `.timber` snapshot export.

## Requirements

- Define material classes for terrain, vegetation, crop, tree, building, storage, infrastructure, water, badwater, empty, and unknown.
- Define field values for each class: packed-cell fuel, flammability, heat loss, terrain bit, water band, burn capacity, consequence target kind, ash quality, contamination behavior, and unresolved-resource behavior.
- Reuse the `TWF-114` resource catalog for resource IDs and the `TWF-117` infrastructure classification decisions.
- Keep the schema host-agnostic where possible. Timberborn-specific API lookup stays in the adapter.
- Add test fixtures that a C# test and a Bun test can both consume.
- Document how unknown resources fail closed without inventing fake fuel.

## Dependencies

- `TWF-114` provides resource fuel and flammability decisions.
- `TWF-117` provides infrastructure classification decisions.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Prefer a small JSON fixture or generated table that both C# tests and TypeScript tests can read.
- Do not implement live import in this ticket.
- Do not change shader behavior in this ticket.
- Keep the first schema version explicit so later save or fixture formats can migrate.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `bun test`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- This ticket replaces duplicated hardcoded bands like vegetation fuel `10` in TypeScript and C# with one source of truth.
