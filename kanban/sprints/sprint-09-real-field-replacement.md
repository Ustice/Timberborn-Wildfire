# Sprint 9: Real Field Replacement

## Goal

Replace the remaining fire-simulation scaffolding with real field-driven implementations: shared map/material import, companion field state, runtime tunables, generated QA acceptance, GPU field rendering, and consequence gates that depend on actual simulation output instead of proof-only stimuli.

## Why This Sprint Exists

The current system proves that the GPU loop, packed cells, deltas, visual surface, alerts, and QA bridge can work in Timberborn. That was the right scaffold for proving feasibility, but it is no longer enough for gameplay tuning.

More scaffolding would make tuning misleading. A fixed center-cell stimulus can show that a shader dispatch runs, but it cannot tell us whether a pine forest has enough fuel, whether a warehouse burns because of stored logs, whether water on the map suppresses the right cells, or whether ash persists after a real tree burns. The next decisions depend on real map truth.

The implementation must therefore replace fake inputs and proof-only controls with a pipeline where Timberborn world state and `.timber` snapshots classify into the same field data, the shader consumes explicit parameters, visuals render from GPU field outputs, and consequences apply from real deltas.

## Included Tickets

- `TWF-137`: define the shared material and field schema that all importers and consequences use.
- `TWF-138`: add companion field storage for material, owner, burn capacity, burn history, ash, and contamination.
- `TWF-139`: implement the live Timberborn world importer beyond terrain-only cells.
- `TWF-140`: align the `.timber` snapshot exporter with the shared catalog and field schema.
- `TWF-141`: gate live/offline importer parity before downstream tuning.
- `TWF-142`: replace shader `#define` tuning with a runtime `FireSimParameters` upload.
- `TWF-143`: add internal QA/debug tuning presets and telemetry.
- `TWF-144`: gate slow-reactable wildfire tuning from generated scenario evidence.
- `TWF-145`: replace fixed-cell QA stimuli with commands that drive real imported fields.
- `TWF-133`: finish the generated QA scenario layout so the importer has stable acceptance data.
- `TWF-146`: add real-field checkpoints and manifests to the generated QA scenario.
- `TWF-147`: implement the GPU field renderer for fire, smoke, ash, steam, visibility, and heat haze.
- `TWF-148`: gate visual field renderer acceptance from recordings and status evidence.
- `TWF-149`: gate world consequences over real field data and unblock existing consequence tickets.
- `TWF-150`: remove scaffold-only code paths after their replacement gates pass.

## Existing Tickets Used As Downstream Work

- `TWF-076`, `TWF-084`, `TWF-077`, `TWF-115`, `TWF-127`, `TWF-128`, and `TWF-129` remain the scoped implementation tickets for crops, trees, structures, stored goods, and infrastructure behavior.
- `TWF-078`, `TWF-079`, and `TWF-081` remain the scoped tickets for persistent ash, contamination-aware aftermath, and persistence validation.
- `TWF-089` through `TWF-092` remain the behavior tuning slices, but they should be revalidated through the new parameter buffer and generated scenario evidence.

## Out Of Scope

- Beaver behavior tickets stay out of this sprint until real smoke, heat, ash, and contamination fields are accepted.
- Player-facing tuning settings stay out of this sprint. Use internal QA/debug overrides only.
- Active-frontier optimization stays deferred until correctness and profiling evidence justify it.
- Steam Workshop release packaging stays out until visuals and consequences are honest enough to record.

## Dependency Order

1. Define the shared field contract: `TWF-137`, then `TWF-138`.
2. Replace inputs: `TWF-139`, `TWF-140`, `TWF-133`, and `TWF-146`.
3. Gate importer parity with generated scenario evidence: `TWF-141`.
4. Replace tuning scaffolding: `TWF-142`, then `TWF-143`.
5. Gate slow-reactable wildfire behavior: `TWF-144`.
6. Replace fixed QA controls: `TWF-145`.
7. Replace presentation: `TWF-147`, then `TWF-148`.
8. Gate world consequences over real field data: `TWF-149`.
9. Remove scaffold-only paths: `TWF-150`.

## Assignment Packets

- Use [../assignment-packet-template.md](../assignment-packet-template.md) for each sub-agent dispatch.
- Include the main-checkout canonical ticket path in every assignment.
- Tell sub-agents to report notes, evidence, blockers, and recommended board moves back to the coordinator.
- Junior workers should receive exactly one ticket and its direct dependencies. Do not assign a gate ticket and implementation ticket to the same worker unless explicitly approved.

## QA Gates

- Importer parity gate: live Timberborn import and `.timber` snapshot export agree on representative generated scenario checkpoints.
- Parameter gate: shader snapshots prove identical deterministic behavior for default parameters and expected behavior changes for at least one preset.
- Slow-reactable wildfire gate: generated scenario recording shows connected fuel spreads, trees sustain flame, water suppresses, and players have response time before large-area loss.
- Visual renderer gate: normal gameplay recordings show field-rendered fire, smoke, ash, steam, visibility, and heat haze without relying on alert text or tiny pooled prefabs.
- Consequence gate: real imported targets produce nonzero consequence counters or precise safe-unavailable telemetry for crops, trees, structures, storage, infrastructure, ash, contamination, and persistence.
- Scaffold removal gate: fixed-cell proof paths are gone or clearly demoted to debug helpers that call real field/tuning paths.

## Live QA Risks

- Risk: Timberborn API discovery may show no safe live access for some inventories, contamination, visuals, or construction rollback.
- Mitigation: implement safe no-op telemetry and split a narrow follow-up only for the missing API, not another fake gameplay path.
- Risk: adding companion fields can increase GPU memory and CPU readback pressure.
- Mitigation: keep full-grid dispatch for correctness, measure before optimizing, and avoid readback of companion fields unless a consumer requires it.
- Risk: TypeScript and C# catalogs drift.
- Mitigation: generate or compare shared catalog fixtures in tests before accepting importer parity.

## Evidence Manifest

- Use [../evidence-manifest-template.md](../evidence-manifest-template.md) for generated scenario artifacts, command transcripts, recordings, screenshots, logs, shader captures, and parity reports.
- Every gate ticket must link evidence roots and exact commands.

## Close Criteria

- All included new tickets are in `06-done`, `07-blocked`, `08-deferred`, or `09-awaiting-review` with concrete notes.
- No downstream consequence ticket is accepted using fixed-cell scaffolding as proof.
- `bun run kanban:audit` has been reviewed.
- `git diff --check`, `bun run typecheck`, `bun test`, `dotnet test`, and `dotnet build Wildfire.slnx` pass for relevant changes.
- Shader behavior changes have Unity batchmode snapshot evidence.
- Live Timberborn QA evidence exists for every ticket that requires it, or the ticket is blocked with the smallest concrete unblock action.

## Notes

- This sprint intentionally pulls real field infrastructure ahead of beaver behavior and release packaging. The existing board had enough proof that scaffolding works; the next risk is making gameplay decisions from fake inputs.
