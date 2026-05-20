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

# TWF-078: Verify Simulator-Backed Ash Field Read Model

## Goal

Keep the current Timberborn ash-field implementation verifiable for clean ash while enforcing simulator-owned transport state as the authoritative ash lifecycle.

## Why

This ticket implemented the first persistent gameplay-ash proof, but [docs/ash-simulation-model.md](../../docs/ash-simulation-model.md) now supersedes the older split-authority design. `TimberbornAshFieldService` must be treated as a derived adapter/read model over simulator ash readback, not as ash storage.

## Requirements

- Keep `PackedCell` unchanged.
- Preserve only the Timberborn ash read model needed by native adapters.
- Do not expand this ticket into a second permanent ash authority.
- Keep existing deterministic and live QA gates for current clean ash and fertile ash behavior.
- Keep all ash amount, contamination, decay, persistence, and collection mutations anchored in simulator transport state.
- Apply a bounded plant growth-speed bonus for plants that opt into ash fertility.
- Leave tainted ash live verification to `TWF-166`; this ticket should not stay blocked on untested contaminated-ash behavior.
- Persist simulator ash fields across save/load.
- Expose bounded QA/status telemetry for ash cells, quality counts, growth-bonus applications, and decay.
- Add deterministic tests for simulator ash readback, quality classification, growth-speed application, decay requests, and persistence serialization where possible.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-076` proves crop burn aftermath.
- `TWF-077` proves structure burn aftermath.
- `TWF-084` proves tree burn aftermath.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Treat this ticket as the historical implementation and verification gate for the Timberborn ash read model.
- `TWF-157` through `TWF-160` own the simulator-authoritative migration and follow-up cleanup.
- Uncontaminated simulator ash is the source of fertile ash behavior. `TWF-166` owns the narrower tainted-ash live proof.
- Do not seed `TimberbornAshFieldService` from burn source events. Burn consequences may report ash-source telemetry, but the read model must come from simulator `AtmosphericFields`/`TransportFields`.
- Plant-growth bonuses should be bounded and opt-in by plant category. Tainted ash bonus exclusion remains a `TWF-166` proof gate.
- Expected counters include ash cells by quality, new ash cells, decayed cells, growth bonus applications, tainted bonus skips, persistence saves, and persistence loads.
- Safe no-op cases must include missing plant growth APIs and unresolved contamination data.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for clean ash creation from simulator readback, persistence, growth behavior or safe-unavailable growth telemetry, and status/log proof of field state. Tainted ash creation and no-fertility behavior are split to `TWF-166`.

## Notes

- 2026-05-17 coordinator/code reconciliation: implementation exists on `main` ahead of `origin/main` in `3712f4d4` plus follow-up fixes. Code surfaces include `TimberbornAshFieldService`, `TimberbornAshWorldEffects`, `TimberbornFireRuntime` persistence/status wiring, `FertileAsh` blueprints/localization, and QA/status telemetry for ash quality, growth, tainted soil poisoning, and fertile ash collection. Deterministic verification passed with `git diff --check origin/main..HEAD`, `bun run typecheck`, and `dotnet test Wildfire.slnx --no-restore` (`436` tests).
- Moved to `04-verify` because the code is implemented and locally tested, but required live QA still needs accepted evidence for fertile/tainted ash creation, save/reload, growth or safe-unavailable growth telemetry, and collection behavior.
- Collection/application work overlapped and landed with this off-sprint implementation; see `TWF-082` for the player-facing fertile-ash loop verification gate.
- 2026-05-19 ash-model update: `docs/ash-simulation-model.md` supersedes the older split between visual ash and gameplay ash. The ash lifecycle is now simulator-owned: renderers and Timberborn services read transport fields and queue bounded mutations.
- Relevant design references: `docs/DESIGN.md` section 20, "Ash And Fertility", `docs/ARCHITECTURE.md` "Ash Adapter Services", and `docs/ash-simulation-model.md`.
- 2026-05-19 coordinator reconciliation: moved back to `03-in-progress` because `~/repos/wildfire-twf-078-ash-crash-fix` contains an unintegrated repair that stops burned-leftover crop handling from destroying the native block object and instead applies burned textures. Next action is reviewer/tech-lead validation of the repair against the ash live-QA failure path before returning this ticket to `04-verify`.
- 2026-05-19 reviewer result: passed for live QA, not integration. The no-delete crop burned-leftover repair avoids destructive crop leftover deletion/GoodStack clearing and applies burned textures with `deleted=false` telemetry. Live QA still must prove the ash/fertile-ash blocker is gone and must inspect rendered/log evidence, not counters alone.
- 2026-05-19 QA result in `~/repos/wildfire-twf-078-ash-crash-fix`: partial pass, do not advance to integration. Evidence root `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-078-ash-crash-fix-qa-20260519T083029Z` shows build/deploy passed, `Fuel` loaded command-responsive, repeated tree and contaminated-tree stimuli produced no `NullReferenceException`, no destroyed-object tick crash, no `(destroyed)` crash token, fertile and tainted ash counters appeared, growth candidates were nonzero, tainted cells skipped growth, tainted soil poisoning reported safe-unavailable telemetry, and burned-leftover handling logged `deleted=false` with separate burned texture logs showing `materials=10`. Failed/incomplete gate: nonzero-ash save/reload was not proven because the only completed save logged `ash_entries=0` after ash decayed before save completion. Next QA needs a controlled nonzero-ash save/reload rerun, probably by pausing/saving immediately after ash creation or adding QA utility support to hold ash long enough for the persistence gate.
- 2026-05-19 sprint closeout: moved to `07-blocked` because required live QA still lacks nonzero-ash save/reload proof. Smallest unblock: add or use a QA-controlled immediate pause/save path after ash creation, or otherwise hold ash long enough to save with `ash_entries>0`, reload, and prove restored ash counters before moving back to `04-verify`.
- 2026-05-20 Jason confirmation: accepted `TWF-078` clean-ash persistence/read-model proof. Tainted ash has not been tested and is split into `TWF-166` as a narrower live-verification ticket, so this ticket is no longer blocked by tainted-ash evidence.
