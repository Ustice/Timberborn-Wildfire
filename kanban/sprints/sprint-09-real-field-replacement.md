# Sprint 9: Real Field Replacement

## Goal

Replace the remaining fire-simulation scaffolding with real field-driven implementations: shared map/material import, companion field state, runtime tunables, real-field QA acceptance, GPU field rendering, and consequence gates that depend on actual simulation output instead of proof-only stimuli.

## Why This Sprint Exists

The current system proves that the GPU loop, packed cells, deltas, visual surface, alerts, and QA bridge can work in Timberborn. That was the right scaffold for proving feasibility, but it is no longer enough for gameplay tuning.

More scaffolding would make tuning misleading. A fixed center-cell stimulus can show that a shader dispatch runs, but it cannot tell us whether a pine forest has enough fuel, whether a warehouse burns because of stored logs, whether water on the map suppresses the right cells, or whether ash persists after a real tree burns. The next decisions depend on real map truth.

The implementation must therefore replace fake inputs and proof-only controls with a pipeline where Timberborn world state and `.timber` snapshots classify into the same field data, the shader consumes explicit parameters, visuals render from GPU field outputs, and consequences apply from real deltas. A purpose-built generated scenario is useful, but it should not block the sprint if a better stable real save proves the same real-field behavior. The `256x256` map acceptance work belongs to Sprint 10 through `TWF-156`.

## Included Tickets

- `TWF-137`: define the shared material and field schema that all importers and consequences use.
- `TWF-138`: add companion field storage for material, owner, burn capacity, burn history, ash, and contamination.
- `TWF-139`: implement the live Timberborn world importer beyond terrain-only cells.
- `TWF-140`: align the `.timber` snapshot exporter with the shared catalog and field schema.
- `TWF-141`: gate live/offline importer parity before downstream tuning.
- `TWF-142`: replace shader `#define` tuning with a runtime `FireSimParameters` upload.
- `TWF-143`: add internal QA/debug tuning presets and telemetry.
- `TWF-144`: gate slow-reactable wildfire tuning from stable real-field evidence.
- `TWF-145`: replace fixed-cell QA stimuli with commands that drive real imported fields.
- `TWF-133`: validate the large-scenario acceptance surface, using either the generated QA scenario or a better stable real save.
- `TWF-146`: add real-field checkpoints and manifests to the generated QA scenario.
- `TWF-147`: implement the GPU field renderer for fire, smoke, ash, steam, visibility, and heat haze.
- `TWF-148`: gate visual field renderer acceptance from real-field recordings and status evidence.
- `TWF-149`: define the world-consequence real-field gate and hand it off as the Sprint 10 consequence charter rather than treating it as one oversized Sprint 9 QA ticket.
- `TWF-150`: remove scaffold-only code paths after their replacement gates pass.

## Existing Tickets Used As Downstream Work

- `TWF-076`, `TWF-084`, `TWF-077`, `TWF-115`, `TWF-127`, `TWF-128`, and `TWF-129` remain the scoped implementation tickets for crops, trees, structures, stored goods, and infrastructure behavior.
- `TWF-078`, `TWF-079`, and `TWF-081` remain the scoped tickets for simulator-backed ash, contamination-aware aftermath, and persistence validation.
- `docs/world-consequence-first-pass.md` now carries the junior-ready work packets for Sprint 10 visual consequences and aftermath.
- `TWF-156` creates the reusable `256x256` Sprint 10 map used to prove local-fire behavior on a max-size world.
- `TWF-089` through `TWF-092` remain the behavior tuning slices, but they should be revalidated through the new parameter buffer and generated scenario evidence.

## Out Of Scope

- Beaver behavior tickets stay out of this sprint until real smoke, heat, ash, and contamination fields are accepted.
- Player-facing tuning settings stay out of this sprint. Use internal QA/debug overrides only.
- Active-frontier optimization stays deferred until correctness and profiling evidence justify it.
- Steam Workshop release packaging stays out until visuals and consequences are honest enough to record.

## Dependency Order

1. Define the shared field contract: `TWF-137`, then `TWF-138`.
2. Replace inputs: `TWF-139`, `TWF-140`, `TWF-133`, and `TWF-146`.
3. Gate importer parity with real-field evidence: `TWF-141`.
4. Replace tuning scaffolding: `TWF-142`, then `TWF-143`.
5. Gate slow-reactable wildfire behavior on the best stable real-field surface: `TWF-144`.
6. Replace fixed QA controls: `TWF-145`.
7. Replace presentation: `TWF-147`, then `TWF-148` on the same or better real-field surface.
8. Use `TWF-149` to define the Sprint 10 world-consequence gate and split it into implementable consequence tickets.
9. Remove scaffold-only paths: `TWF-150`.

## Assignment Packets

- Use [../assignment-packet-template.md](../assignment-packet-template.md) for each sub-agent dispatch.
- Include the main-checkout canonical ticket path in every assignment.
- Tell sub-agents to report notes, evidence, blockers, and recommended board moves back to the coordinator.
- Junior workers should receive exactly one ticket and its direct dependencies. Do not assign a gate ticket and implementation ticket to the same worker unless explicitly approved.

## QA Gates

- Importer parity gate: live Timberborn import and `.timber` snapshot export agree on representative real-field checkpoints.
- Parameter gate: shader snapshots prove identical deterministic behavior for default parameters and expected behavior changes for at least one preset.
- Slow-reactable wildfire gate: real-field recording shows connected fuel spreads, trees sustain flame, water suppresses, and players have response time before large-area loss.
- Visual renderer gate: normal gameplay recordings show field-rendered fire, smoke, ash, steam, visibility, and heat haze without relying on alert text or tiny pooled prefabs.
- Consequence gate: Sprint 9 only needs to prove real-field readiness and name the Sprint 10 split. Sprint 10 owns nonzero consequence counters or precise safe-unavailable telemetry for crops, trees, structures, storage, infrastructure, ash, contamination, and persistence.
- Scaffold removal gate: fixed-cell proof paths are gone or clearly demoted to debug helpers that call real field/tuning paths.

## Live QA Risks

- Risk: Timberborn API discovery may show no safe live access for some inventories, contamination, visuals, or construction rollback.
- Mitigation: implement safe no-op telemetry and split a narrow follow-up only for the missing API, not another fake gameplay path.
- Risk: adding companion fields can increase GPU memory and CPU readback pressure.
- Mitigation: keep full-grid dispatch for correctness, measure before optimizing, and avoid readback of companion fields unless a consumer requires it.
- Risk: TypeScript and C# catalogs drift.
- Mitigation: generate or compare shared catalog fixtures in tests before accepting importer parity.
- Risk: the generated scenario becomes a technicality that blocks real gameplay proof.
- Mitigation: use the best stable real-field scenario available. Keep generated-scenario work as reusable QA tooling, not the only acceptance surface. `Fuel` is confirmed `50x50`, so it can help smoke-test workflows but cannot satisfy Sprint 10's `256x256` gate.

## Evidence Manifest

- Use [../evidence-manifest-template.md](../evidence-manifest-template.md) for generated scenario artifacts, command transcripts, recordings, screenshots, logs, shader captures, and parity reports.
- Every gate ticket must link evidence roots and exact commands.

## Close Criteria

- All included new tickets are in `06-done`, `07-blocked`, `08-deferred`, or `09-awaiting-review` with concrete notes.
- No downstream consequence ticket is accepted using fixed-cell scaffolding as proof.
- Sprint 10 has a clear charter candidate for visual consequences and aftermath based on `docs/world-consequence-first-pass.md`.
- `bun run kanban:audit` has been reviewed.
- `git diff --check`, `bun run typecheck`, `bun test`, `dotnet test`, and `dotnet build Wildfire.slnx` pass for relevant changes.
- Shader behavior changes have Unity batchmode snapshot evidence.
- Live Timberborn QA evidence exists for every ticket that requires it, or the ticket is blocked with the smallest concrete unblock action.

## Notes

- This sprint intentionally pulls real field infrastructure ahead of beaver behavior and release packaging. The existing board had enough proof that scaffolding works; the next risk is making gameplay decisions from fake inputs.
- 2026-05-06 direction update: do not get stuck on generated-scenario technicalities if the newer real-field system works. The `256x256` question belongs to Sprint 10 through `TWF-156`: create a max-size map and prove that a normal local forest fire inside that world does not make the map chug. Treat `TWF-149` as the bridge into the Sprint 10 visual-consequence/aftermath charter rather than a giant all-in-one QA gate.
- 2026-05-06 closeout: Sprint 9 closed as mixed done/blocked. `TWF-133`, `TWF-137` through `TWF-143`, and `TWF-145` through `TWF-147` are done. `TWF-144` and `TWF-148` are blocked on normal gameplay visuals: live `Fuel` QA proved real-field behavior telemetry, but recordings did not visibly show fire/smoke/ash/steam/heat haze and the renderer reported disabled/material-not-ready. `TWF-149` is blocked until those visual gates pass and downstream consequence ownership fixes pass fresh review. `TWF-150` is blocked until the replacement gates pass or each remaining scaffold has an accepted replacement/blocker.
