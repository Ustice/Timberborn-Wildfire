---
ticket: TWF-153
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-130
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-153-implement-detonator-fire-safety-behavior.md
---

# TWF-153: Implement Detonator Fire Safety Behavior

## Goal

Define and implement conservative fire behavior for placed `Detonator` targets without corrupting automation state.

## Why

`TWF-130` found that detonators are trigger devices constrained to sit on dynamite, with native `Arm()`, `Disarm()`, and `Evaluate()` methods. They are not ordinary fuel, and premature arming is riskier than disabling or marking the device unsafe.

## Requirements

- Resolve placed detonator targets from compact fire deltas.
- Treat detonators as trigger devices, not fuel.
- Add a setting gate for detonator fire behavior.
- Start with disable or unsafe-mark behavior unless a safe native arming path is proven.
- Preserve recoverability of automation state after fire ends.
- Suppress duplicate cells for the same detonator target in one dispatch.
- Expose telemetry for considered, disabled, armed, skipped setting disabled, skipped no-safe-api, duplicate suppressed, and recoverability state.
- Add deterministic tests for settings gates, duplicate suppression, disabled behavior, safe unavailable APIs, and no direct terrain mutation.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Implementation Notes

- Start with a wrapper around `Detonator` state changes and a deterministic no-op path.
- Prefer disabled or unsafe-mark behavior before any premature arming behavior.
- Keep automation state recoverability visible in telemetry and QA notes.
- Do not trigger adjacent dynamite from this ticket unless a later accepted proof says that path is safe.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for one detonator being disabled or explicitly skipped without corrupting automation state.

## Notes

- Do not use detonator behavior to trigger dynamite until a separate accepted proof says that path is safe.
- Keep native dynamite triggering in `TWF-152`.
- Keep tunnel destruction in `TWF-154`.
