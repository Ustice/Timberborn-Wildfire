---
ticket: TWF-081
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-047
  - TWF-076
  - TWF-077
  - TWF-078
  - TWF-079
  - TWF-084
  - TWF-115
  - TWF-073
  - TWF-082
  - TWF-164
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-081-validate-world-consequence-persistence.md
---

# TWF-081: Validate World Consequence Persistence

## Goal

Prove burn damage, burned visuals, closed buildings, ash fields, and contamination-aware aftermath survive save/reload or degrade safely.

## Why

The world consequence design introduces durable state beyond transient simulator fields. Players must be able to save and reload after fire damage without losing important aftermath state, duplicating resources, or corrupting the save.

## Requirements

- Validate save/reload after crop burn consequences.
- Validate save/reload after tree burn consequences.
- Validate save/reload after structure burn damage and construction rollback.
- Validate save/reload after simulator-backed ash fields exist.
- Validate save/reload after contamination-aware tainted aftermath exists, if available.
- Validate save/reload after fertile ash collection depletes simulator ash and mutates `FertileAsh` inventory.
- Validate save/reload after tainted ash decay or water washout, including no resurrection of washed ash.
- Validate save/reload after stored-goods loss exists, including no duplicate or resurrected inventory.
- Validate save/reload of beaver field behavior harness state if any non-transient beaver status is implemented.
- Confirm disabled or removed mod behavior remains recoverable or document a clear limitation.
- Add save/load code or deterministic tests only if validation exposes a real gap.
- Preserve saves, commands, logs, screenshots or recordings, status outputs, and artifact paths.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with accepted evidence or blockers.

## Dependencies

- `TWF-047` proves the baseline save/reload, disable, and re-enable lifecycle.
- `TWF-076`, `TWF-077`, `TWF-078`, `TWF-079`, `TWF-084`, and `TWF-115` create the durable world consequence states this ticket validates.
- `TWF-073` provides beaver behavior persistence hooks when variant tickets create non-transient state.
- `TWF-082` adds fertile ash collection/application state.
- `TWF-164` adds tainted ash decay and water washout state.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test` if production code changes are made.
- Passing evidence requires no new critical Unity or Wildfire failure tokens in the save/reload window.

## Notes

- 2026-05-17 coordinator/code reconciliation: ash-field persistence and fertile-designation persistence code exists, and deterministic persistence coverage passed in the local test suite. This ticket remains open as a QA gate because live save/reload evidence still needs to prove ash entries, burn damage state, burned visuals, fertile designations, and any contamination-aware aftermath survive in an actual Timberborn save without duplication or stale UI/runtime state.
- Relevant design reference: `docs/DESIGN.md` section 20, especially burn damage, ash, contamination, and persistence expectations.
- Prefer the generated scenario from `TWF-119` when it exists; otherwise capture the exact save and setup used.
