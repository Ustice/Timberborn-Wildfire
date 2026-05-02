---
ticket: TWF-043
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-036
   - TWF-038
   - TWF-041
write_scope:
   - src/Wildfire.Unity/**
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/DESIGN.md
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-043-tune-fire-game-feel-constants.md
---

# TWF-043: Tune Fire Game Feel Constants

## Goal

Tune spread speed, burn duration, heat loss, material flammability, and water suppression so the first fire loop feels coherent in live Timberborn.

## Why

After Sprint 3, fire should be visible and player-facing. The next design step is making that loop feel playable rather than merely proving the pipeline. The tuning must stay in the GPU simulation and adapter-owned material inputs, not in a parallel C# fire path.

## Requirements

- Tune shader constants or material bands for spread speed, burn duration, heat loss, flammability, and suppression.
- Keep fire-spread rules in `FireSim.compute`.
- Keep Timberborn as an adapter that supplies material and water inputs.
- Preserve deterministic stochastic behavior from seed, tick, and cell index.
- Use at least two representative scenarios and one live Timberborn run as tuning evidence.
- Record accepted constants, commands, artifact paths, screenshots, and interpretation in `docs/TEST_PLAN.md`.
- Update `docs/DESIGN.md` only for durable rule-shape or tuning-contract decisions.

## Dependencies

- `TWF-036` provides building consequences.
- `TWF-038` provides water suppression input.
- `TWF-041` provides visual tuning evidence and live visual validation.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness when shader behavior changes.
- QA must capture live screenshots, command/status evidence, copied `Player.log`, and a short note explaining whether the tuned loop is playable.

## Notes

- Do not broaden this into new mechanics such as wind or diagonal spread unless `TWF-044` has already accepted that decision.

## Worker Notes

Worker pass on 2026-05-02 in worktree `~/repos/wildfire-TWF-043`.

Changed tuning:

- `src/Wildfire.Unity/FireSim.compute` now names the game-feel constants for ignition, neighbor spread, water suppression, burn pressure, and burn heat. The rule still uses only seed, tick, and cell index for stochastic burn rolls, and fire spread remains in the compute shader.
- Neighbor heating now gives burning neighbors enough direct heat to spread out of broad grass scenarios instead of being lost to integer averaging.
- Water now applies stronger heat suppression, raises ignition threshold by two heat bands per water band, and applies a larger burn-pressure penalty before fuel consumption.
- Timberborn adapter material bands now make vegetation and stockpile resources catch more readily while wood-like buildings burn longer and less explosively:
   - Wood-like building: fuel `15`, flammability `1`, heat loss `3`.
   - Stockpile resource: fuel `8`, flammability `2`, heat loss `3`.
   - Vegetation: fuel `10`, flammability `3`, heat loss `1`.
- QA building-burnout stimulus now uses the tuned wood-like building constants for both the primed and spent cells.

Representative shader evidence:

- Artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/`.
- `single-ignition`, seed `21`, `5x5x1`, tick `2`: visual checksum `visual-fnv1a32:50C4978E`, per-tick deltas `[5, 5]`, final hot cells `5`.
- `line-of-fuel`, seed `42`, `12x5x1`, tick `4`: visual checksum `visual-fnv1a32:120F70AE`, per-tick deltas `[5, 5, 5, 2]`, final hot cells `5`.
- `water-barrier`, seed `42`, `12x5x1`, tick `4`: visual checksum `visual-fnv1a32:40818F57`, per-tick deltas `[5, 5, 5, 5]`, final hot cells `1`.

Commands run:

- `dotnet test`
- `WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled`
- `dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --seed=21 --width=5 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/single-ignition-seed21-5x5x1.fixture.json"`
- `dotnet run --project src/Wildfire.Cli -- --scenario=line-of-fuel --seed=42 --width=12 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/line-of-fuel-seed42-12x5x1.fixture.json"`
- `dotnet run --project src/Wildfire.Cli -- --scenario=water-barrier --seed=42 --width=12 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/water-barrier-seed42-12x5x1.fixture.json"`
- Unity batchmode captures were run for the three fixtures above with `Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture`; logs and capture JSON are in the artifact directory.

Proposed doc updates for the TWF-044/doc owner:

- `docs/DESIGN.md`: no update in this worker cleanup; the branch preserves the TWF-044 release decisions from `1315a4d`.
- `docs/TEST_PLAN.md`: completed in the review cleanup as an additive TWF-043 section compatible with the TWF-044 conservative release-validation expectations.

Blockers and follow-up:

- Live Timberborn gameplay evidence was not captured in this worker pass. This ticket still needs QA for live screenshots, command/status evidence, copied `Player.log`, and a short playable-loop interpretation.

Review cleanup on 2026-05-02:

- Rebasing/updating onto `main` at `1315a4d Resolve release simulation decisions` preserved the TWF-044 docs and board state; this diff does not touch `docs/DESIGN.md` or `kanban/by-status`.
- `docs/TEST_PLAN.md` now records accepted TWF-043 constants, artifact paths, commands, interpretation, semantic snapshot outcomes, and the live screenshot/`Player.log` QA requirement.
- `UnityShaderExecutionHarnessTests` now asserts per-tick delta counts and final hot-cell counts for `single-ignition`, `line-of-fuel`, and `water-barrier` in addition to visual checksums.
- Re-run evidence: `git diff --check`, `dotnet test`, and the opt-in Unity shader harness all passed after cleanup.
