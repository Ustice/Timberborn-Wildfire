---
ticket: TWF-082
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-078
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/DESIGN.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-082-add-fertile-ash-collection-and-application.md
---

# TWF-082: Add Fertile Ash Collection And Application

## Goal

Allow beavers to collect fertile ash and place it in fields as a later gameplay loop.

## Why

The design treats automatic ash fertility as the first gameplay pass, but the longer-term goal is for controlled burns to create a resource beavers can collect and deliberately apply to fields. That turns aftermath into a player-managed farming strategy.

## Requirements

- Build on the ash field service rather than the temporary visual ash channel.
- Define how fertile ash becomes collectable without duplicating or losing field state.
- Add a good, job, hauling, storage, building, or field-application path only through safe Timberborn APIs.
- Preserve ash quality; tainted ash should not be usable as fertilizer unless a later decontamination mechanic exists.
- Add deterministic tests for collection eligibility, resource accounting, application, and decay or depletion.
- Capture live QA evidence for collection and field application, or document the exact Timberborn API blocker.
- Update `docs/DESIGN.md` only if the accepted gameplay loop changes.

## Dependencies

- `TWF-078` provides persistent ash field state and fertile ash quality.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for collection and application or an explicit safe unavailable state.

## Notes

- This is deferred future work, not required for the first ash field pass.
- Relevant design reference: `docs/DESIGN.md` section 20, "Ash And Fertility".
