---
ticket: TWF-150
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-141
  - TWF-144
  - TWF-145
  - TWF-148
  - TWF-149
write_scope:
  - src/**
  - scripts/**
  - tests/**
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-150-remove-scaffold-only-paths.md
---

# TWF-150: Remove Scaffold Only Paths

## Goal

Remove or demote code paths that exist only to fake field input, fixed-cell proof, or prefab-only visibility after real replacements pass their gates.

## Requirements

- Delete fixed-cell QA behavior that has a real imported-field replacement.
- Delete duplicated fuel/material constants that are superseded by the shared schema.
- Remove tests that overfit scaffold implementation details, and replace them with contract tests where needed.
- Keep small debug helpers only when they call the real importer, real parameter buffer, and real renderer/consequence paths.
- Update docs so future agents do not treat old scaffolds as production architecture.
- Run a second scan for `scaffold`, `fixed`, `placeholder`, `qa_delta_stimulus`, hardcoded fuel bands, and prefab-only assumptions.

## Dependencies

- `TWF-141`, `TWF-144`, `TWF-145`, `TWF-148`, and `TWF-149` must pass or document explicit replacements before removal.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Do not remove the QA command bridge itself. Remove fake behavior behind it.
- Do not remove evidence docs; annotate them as historical proof where useful.
- If a scaffold has no replacement gate, leave it and create a specific follow-up blocker instead of deleting blindly.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `bun test`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run `bun run kanban:audit`.

## Notes

- This ticket is intentionally last. Cleanup before replacement would hide risk; cleanup after gates removes confusion.
- 2026-05-06 coordinator: blocked by failed replacement gates. `TWF-144` and `TWF-148` failed required live QA because normal gameplay visuals were not visible and `gpu_field_renderer_enabled=false` / `gpu_field_renderer_material_ready=false`; `TWF-149` is also blocked on those gates plus consequence ownership review failures. Do not remove scaffold-only paths until replacement gates pass or each remaining scaffold has an explicit accepted replacement/blocker.
