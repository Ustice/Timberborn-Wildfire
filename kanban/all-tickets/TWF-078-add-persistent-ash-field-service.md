---
ticket: TWF-078
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-076
  - TWF-077
  - TWF-084
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-078-add-persistent-ash-field-service.md
---

# TWF-078: Add Persistent Ash Field Service

## Goal

Keep the current Timberborn ash-field implementation verifiable while migrating the authoritative ash model toward simulator-owned transport state.

## Why

This ticket implemented the first persistent gameplay-ash proof, but [docs/ash-simulation-model.md](../../docs/ash-simulation-model.md) now supersedes the older split-authority design. The existing service should be treated as a transitional adapter/read model while follow-up tickets move ash amount, contamination, persistence, status, and mutation back under simulator authority.

## Requirements

- Keep `PackedCell` unchanged.
- Preserve the current ash field service until simulator-owned ash state can replace it safely.
- Do not expand this ticket into a second permanent ash authority.
- Keep existing deterministic and live QA gates for current fertile/tainted ash behavior.
- Create follow-up migration work against simulator transport state instead of adding more independent service state.
- Apply a bounded plant growth-speed bonus for plants that opt into ash fertility.
- Prevent tainted or contaminated ash from granting a growth bonus.
- Persist ash fields across save/load.
- Expose bounded QA/status telemetry for ash cells, quality counts, growth-bonus applications, and decay.
- Add deterministic tests for ash creation, quality classification, growth-speed application, decay, and persistence serialization where possible.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-076` proves crop burn aftermath.
- `TWF-077` proves structure burn aftermath.
- `TWF-084` proves tree burn aftermath.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Treat this ticket as the historical implementation and verification gate for the transitional service.
- `TWF-157` through `TWF-160` own the simulator-authoritative migration.
- Uncontaminated simulator ash should eventually replace `fertile` service state; contaminated simulator ash should eventually replace `tainted` service state.
- Plant-growth bonuses should be bounded and opt-in by plant category; tainted ash must never grant the bonus.
- Expected counters include ash cells by quality, new ash cells, decayed cells, growth bonus applications, tainted bonus skips, persistence saves, and persistence loads.
- Safe no-op cases must include missing plant growth APIs and unresolved contamination data.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for fertile ash and tainted ash creation, or explicit safe unavailable states, plus status/log proof of field state.

## Notes

- 2026-05-17 coordinator/code reconciliation: implementation exists on `main` ahead of `origin/main` in `3712f4d4` plus follow-up fixes. Code surfaces include `TimberbornAshFieldService`, `TimberbornAshWorldEffects`, `TimberbornFireRuntime` persistence/status wiring, `FertileAsh` blueprints/localization, and QA/status telemetry for ash quality, growth, tainted soil poisoning, and fertile ash collection. Deterministic verification passed with `git diff --check origin/main..HEAD`, `bun run typecheck`, and `dotnet test Wildfire.slnx --no-restore` (`436` tests).
- Moved to `04-verify` because the code is implemented and locally tested, but required live QA still needs accepted evidence for fertile/tainted ash creation, save/reload, growth or safe-unavailable growth telemetry, and collection behavior.
- Collection/application work overlapped and landed with this off-sprint implementation; see `TWF-082` for the player-facing fertile-ash loop verification gate.
- 2026-05-19 ash-model update: `docs/ash-simulation-model.md` supersedes the older split between visual ash and gameplay ash. `TWF-157` through `TWF-160` now own the migration from this transitional service to simulator-owned ash state.
- Relevant design references: `docs/DESIGN.md` section 20, "Ash And Fertility", `docs/ARCHITECTURE.md` "Ash Adapter Services", and `docs/ash-simulation-model.md`.
