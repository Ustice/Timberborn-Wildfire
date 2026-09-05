# Steam Simulation Model

Steam is a clean, transient simulator transport field produced when heat meets water or wetness. The simulator owns its production, movement, and decay. Renderers read that field instead of inventing steam from water-change notifications.

## Decisions

- Steam has a 0–7 transport lane and no contamination lane.
- Badwater can suppress fire as a water-like input without becoming clean water or creating a separate toxic-steam field.
- Steam and smoke share transport infrastructure but can have different movement, decay, and presentation tuning.
- Visual smoothing, puffs, opacity, and lifetime affect presentation; they do not author simulation state.

## Implementation anchors

[WildfireTransportFieldState](../src/Wildfire.Core/WildfireTransportFieldState.cs) defines the packed lane. [FireSim.compute](../src/Wildfire.Unity/FireSim.compute) contains `SteamSourceFromMoistureAndHeat` and transport logic. [Shader execution tests](../tests/Wildfire.Shader.Tests/UnityShaderExecutionHarnessTests.cs) include wet/hot steam generation and converging transport cases; see [validation](TEST_PLAN.md) to establish whether they actually execute.

The [earlier steam record](history/2026-09-04/steam-simulation-model.md) preserves the May 2026 rationale and proposed acceptance criteria. It does not establish present visual quality or issue status.
