---
ticket: TWF-161
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-157
  - TWF-158
  - TWF-159
  - TWF-160
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-161-rename-ash-field-concepts-after-behavior-migration.md
---

# TWF-161: Rename Ash Field Concepts After Behavior Migration

## Goal

Rename the ash-related field concepts after the simulator-authoritative behavior is working, so code names match the model without mixing semantic and behavioral changes.

## Why

`docs/ash-simulation-model.md` recommends `TransportFields`, `MaterialFields`, and ash presentation language. Those names are useful, but doing a broad rename before behavior changes would make review harder and hide real risks.

## Requirements

- Rename the `AtmosphericFields` concept to `TransportFields` where the code boundary can tolerate it.
- Rename the `CompanionFields` concept to `MaterialFields` where the field is used as material/source metadata.
- Rename renderer-facing `visual ash` wording to `ash presentation`, `ash overlay`, or `ash render input`.
- Preserve serialized compatibility or add explicit migration for any saved field names.
- Keep shader property compatibility unless the asset bundle and harness updates are included in the same patch.
- Update tests, docs, and status labels that rely on old names.

## Dependencies

- `TWF-157`, `TWF-158`, `TWF-159`, and `TWF-160` complete the behavior migration first.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Treat this as a cleanup and clarity ticket, not a behavior ticket.
- Keep diffs mechanical and reviewable.
- Do not rename public save fields without a compatibility check.

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck`.
- Run the Unity shader harness if shader property or fixture names change.

## Notes

- This ticket exists because the doc says the current names hide intent, but behavior should move first.
