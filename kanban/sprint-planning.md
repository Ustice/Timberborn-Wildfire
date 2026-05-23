# Wildfire Sprint Planning

Historical note: the file kanban board was migrated to GitHub Issues on 2026-05-23. Use GitHub Issues as the active backlog for new work; keep this document as the historical sprint-boundary map unless explicitly reconciling old file-board state.

Use this document to understand historical sprint boundaries. `kanban/by-status` remains the historical board, and individual ticket files remain the archived assignment contracts. This document is only the planning map.

## Current Planning Principle

Split sprints by dependency gates, not ticket number. A sprint should have one integration theme, one evidence ladder, and a small number of shared live-QA surfaces.

The biggest risk in the current board is mixing release packaging with unfinished gameplay consequence work. Release tickets should wait until fire visuals, world consequences, beaver danger, persistence, and player feedback are proven enough to record and describe honestly.

Required QA is a hard integration gate. If a ticket fails QA, it must pass that same required QA gate in a later run before it can move to `05-integration/`.

Required review is also a hard integration gate. If a ticket fails review, it must return to `03-in-progress/` for fixes, move back through `04-verify/`, and pass a fresh review before it can move to `05-integration/`.

## Historical Board Directory Protocol

Before the migration, agents worked from the same canonical board directory:

- Main checkout board: `~/repos/wildfire/kanban/by-status`.
- Canonical tickets: `~/repos/wildfire/kanban/all-tickets`.
- Sprint plan: `~/repos/wildfire/kanban/sprint-planning.md`.

Workers used ticket-specific worktrees for implementation, but those worktrees were implementation sandboxes, not board authorities. GitHub Issues now own active status and notes.

Sub-agents should now read the assigned GitHub issue, then report notes, evidence, blockers, and recommended status-label changes back to the coordinator.

This avoids three common failure modes:

- A worker updates ticket notes in a worktree that never integrates.
- A status symlink in a worktree disagrees with the main board.
- Coordinators spend time reconciling ticket prose instead of integrating code and evidence.

If a sub-agent must edit a historical ticket file directly, the coordinator should say so explicitly and include `kanban/all-tickets/...` in the write scope. Those edits should happen in the main checkout whenever possible.

## Recommended Sprint Split

### Current Next Sprint: Visual, Beaver, And Ash Verification

The next active sprint charter is [sprint-11-visual-beaver-ash-verification.md](sprints/sprint-11-visual-beaver-ash-verification.md).

Include:

- `TWF-067`: smoke readability implementation.
- `TWF-073`: beaver field behavior harness implementation.
- `TWF-078`: simulator-backed ash read-model live verification.
- `TWF-082`: fertile ash collection/application live verification.

Why: the board was reconciled after off-sprint ash/fertility implementation. `TWF-078` and `TWF-082` were later accepted for their clean ash scope, with tainted ash split to `TWF-166`. Release packaging remains later work.

### Follow-On Sprint: Release Gameplay Readiness

The follow-on release gameplay charter is [sprint-12-release-gameplay-readiness.md](sprints/sprint-12-release-gameplay-readiness.md).

Include:

- `TWF-166`, `TWF-079`, and `TWF-167`: narrow tainted ash verification, contamination-aware aftermath, and ash/water interaction.
- `TWF-163` and `TWF-168`: release icons for alert classes, fertile ash, tainted ash, fertilize crop/tree tools, and the structure-on-fire alert.
- `TWF-085` through `TWF-087`, then `TWF-074`: beaver consequence variants and recording validation.
- `TWF-080` and `TWF-081`: aggregated player feedback and persistence validation.
- `TWF-169` and `TWF-170`: fix burned-tree stump visuals and paused-load ash visibility.
- `TWF-165`: rare deterministic drought-aware ignition from eligible sources only.
- `TWF-156`: `256x256` release QA scenario/map.

Why: release packaging should wait until the gameplay loop is legible and validated. This sprint turns ash, contamination, beaver consequences, structure alerts, rare ignition, icons, feedback, and save/reload proof into the release candidate behavior surface while closing two visible aftermath bugs.

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

- `TWF-070`: steam visual tuning, now that fire, smoke, and ash visual baselines are accepted.
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

- `TWF-078`: simulator-backed ash read model.
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

### Later Sprint: Release Packaging

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

- `TWF-083`: overgrowth and surface fuel load.
- `TWF-120` through `TWF-125`: faction fire response, Fire Wardens, Fire Bell bucket brigades, Emberpelt stamping, fans, and fire berms.

## Coordinator Use

At sprint start, the coordinator should:

- Read this file after `kanban/README.md` and before moving tickets to `02-ready`.
- Reconcile this plan against GitHub Issues when using it for planning.
- Choose the smallest sprint slice that can be integrated and verified in dependency order.
- Treat this document as guidance, not an override of GitHub Issues or Jason's current instruction.
- Keep all status changes in GitHub issue labels.
