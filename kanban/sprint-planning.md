# Wildfire Sprint Planning

Use this document to choose sprint boundaries before starting a coordination run. `kanban/by-status` remains the live board, and individual ticket files remain the assignment contract. This document is only the planning map.

## Current Planning Principle

Split sprints by dependency gates, not ticket number. A sprint should have one integration theme, one evidence ladder, and a small number of shared live-QA surfaces.

The biggest risk in the current board is mixing release packaging with unfinished gameplay consequence work. Release tickets should wait until fire visuals, world consequences, beaver danger, persistence, and player feedback are proven enough to record and describe honestly.

Required QA is a hard integration gate. If a ticket fails QA, it must pass that same required QA gate in a later run before it can move to `05-integration/`.

Required review is also a hard integration gate. If a ticket fails review, it must return to `03-in-progress/` for fixes, move back through `04-verify/`, and pass a fresh review before it can move to `05-integration/`.

## Shared Board Directory Protocol

All agents should work from the same canonical board directory:

- Main checkout board: `~/repos/wildfire/kanban/by-status`.
- Canonical tickets: `~/repos/wildfire/kanban/all-tickets`.
- Sprint plan: `~/repos/wildfire/kanban/sprint-planning.md`.

Workers may use ticket-specific worktrees for implementation, but those worktrees are implementation sandboxes, not board authorities. The coordinator owns board status moves and canonical ticket-note updates in the main checkout unless a ticket explicitly assigns board-maintenance work.

Sub-agents should read the canonical ticket from the main checkout at assignment time, then report notes, evidence, blockers, and recommended moves back to the coordinator. The coordinator transcribes accepted notes into the canonical ticket and moves status symlinks in the main checkout.

This avoids three common failure modes:

- A worker updates ticket notes in a worktree that never integrates.
- A status symlink in a worktree disagrees with the main board.
- Coordinators spend time reconciling ticket prose instead of integrating code and evidence.

If a sub-agent must edit a ticket file directly, the coordinator should say so explicitly and include `kanban/all-tickets/...` in the write scope. Those edits should happen in the main checkout whenever possible.

## Recommended Sprint Split

### Sprint 5: Safety Rails And Test Surfaces

Goal: make the next gameplay work reproducible and safe.

Include:

- `TWF-126`: ticket implementation-notes hygiene pass.
- `TWF-093`, `TWF-094`, `TWF-095`, then parent `TWF-047`: save/reload, disabled-mod recovery, and re-enable lifecycle.
- `TWF-049`, `TWF-050`: compatibility probes and GPU asset failure hardening.
- `TWF-064`: building burnout pause consequence investigation.
- `TWF-065`: screen recording QA tool.
- `TWF-114`: resource fuel and flammability catalog.
- `TWF-118`, `TWF-119`: generated world-consequence scenario save and validation.
- `TWF-096`, `TWF-097` if capacity allows: settings framework and enable/disable safety.

Why: this sprint creates the shared evidence surfaces needed by later sprints: save/reload proof, failure-mode hardening, recording, a generated scenario, and the resource catalog.

### Sprint 6: Fire Feel And Field Presentation

Goal: make fire, smoke, ash, and steam readable, then tune core fire behavior with recording evidence.

Include:

- `TWF-066`, `TWF-067`, `TWF-068`, `TWF-070`: fire, smoke, ash, and steam visual tuning.
- `TWF-088`, `TWF-089`, `TWF-090`, `TWF-091`, `TWF-092`, then parent `TWF-069`: behavior tuning slices and parent recording pass.
- `TWF-099`: visual and debug settings.
- `TWF-098`: behavior tuning settings after `TWF-069`.
- `TWF-051`: active-frontier release-scope decision after tuning evidence.

Why: presentation and behavior tuning should be judged from the same recordings. This also prevents world-consequence and beaver tickets from tuning against unclear fire behavior.

### Sprint 7: World Consequence Core

Goal: make fire damage the world in durable, resource-accounted ways.

Include:

- `TWF-075`: burn damage state foundation.
- `TWF-076`: crop burn consequences.
- `TWF-084`: tree burn consequences.
- `TWF-077`: structure burn damage rollback.
- `TWF-116`: explosive storage fire behavior.
- `TWF-115`: stored goods burn consequences.
- `TWF-117`: infrastructure burn classification.

Why: this is the core gameplay consequence sprint. Foundation and catalog work must be accepted before crops, trees, structures, stored goods, explosives, or infrastructure can be implemented safely.

### Sprint 8: Ash, Contamination, And Persistence

Goal: make aftermath durable and meaningful.

Include:

- `TWF-078`: persistent ash field service.
- `TWF-079`: contamination-aware fire consequences.
- `TWF-081`: world consequence persistence validation.

Why: ash depends on crop, tree, and structure burn outcomes. Contamination depends on ash and exposure classification. Persistence should validate the integrated world-consequence state, not isolated stubs.

### Sprint 9: Real Field Replacement

Goal: replace fake field inputs, fixed-cell stimuli, shader-constant tuning, and prefab-only large-area visuals with real imported fields, runtime parameters, generated-scenario evidence, GPU field rendering, and consequence gates.

Include:

- `TWF-137` through `TWF-150`: shared field schema, companion fields, live and snapshot importers, importer parity, runtime parameters, internal presets, tuning gates, real-field QA controls, generated scenario checkpoints, GPU field renderer, consequence gate, and scaffold cleanup.
- `TWF-133`: generated scenario layout dependency.
- Existing consequence tickets only as downstream gates when their implementation is ready.

Why: the project has proven the GPU loop and QA bridge. Further gameplay tuning from fixed cells would be misleading because it cannot answer whether real Timberborn trees, crops, storage, water, structures, and aftermath behave correctly.

### Sprint 10: Beavers And Player Feedback

Goal: make beavers respond to danger and make the result understandable to players.

Include:

- `TWF-071`: beaver field-effects design contract.
- `TWF-072`: beaver exposure telemetry.
- `TWF-073`: shared beaver behavior harness.
- `TWF-085`: normal smoke exposure.
- `TWF-087`: fire and heat exposure.
- `TWF-086`: toxic smoke exposure after `TWF-079`.
- `TWF-074`: beaver behavior recording validation.
- `TWF-080`: aggregated world consequence feedback.
- `TWF-101` if release-quality recordings are desired from this result.

Why: this work is API-risky and player-visible. Keep the ladder explicit: telemetry, harness, lower-risk states, severe states, recordings, then aggregated feedback.

### Sprint 11: Release Packaging

Goal: turn the proven gameplay into a distributable mod.

Include:

- `TWF-052`, `TWF-053`, `TWF-054`: CI, packaging, tagged release flow.
- `TWF-055`, `TWF-056`, `TWF-057`: release media, README/install docs, changelog/versioning.
- `TWF-058`, `TWF-059`: cross-platform bundle validation and clean-install QA.
- `TWF-060`, `TWF-061`, `TWF-062`: release debug-surface scrub, license/attribution, support diagnostics.
- `TWF-063`: Steam Workshop distribution parent.
- `TWF-100`, `TWF-102`, `TWF-103` through `TWF-113`: workshop graphic, copy, platform support, package shape, metadata, and private install validation.

Why: release packaging should describe and capture the actual product. Do not spend the sprint budget polishing public release materials while the gameplay loop is still changing underneath them.

## Deferred Work

Keep these deferred until the release path is stable or Jason explicitly pulls them forward:

- `TWF-082`: fertile ash collection and application.
- `TWF-083`: overgrowth and surface fuel load.
- `TWF-120` through `TWF-125`: faction fire response, Fire Wardens, Fire Bell bucket brigades, Emberpelt stamping, fans, and fire berms.

## Coordinator Use

At sprint start, the coordinator should:

- Read this file after `kanban/README.md` and before moving tickets to `02-ready`.
- Reconcile this plan against the current `kanban/by-status` board.
- Choose the smallest sprint slice that can be integrated and verified in dependency order.
- Treat this document as guidance, not an override of live board state or Jason's current instruction.
- Keep all board moves and canonical ticket updates in the main checkout.
