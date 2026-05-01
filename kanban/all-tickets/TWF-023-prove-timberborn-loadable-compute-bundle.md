---
ticket: TWF-023
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-022
write_scope:
   - src/Wildfire.Unity/**
   - src/Wildfire.Timberborn/**
   - scripts/deploy-timberborn-mod.ts
   - docs/TEST_PLAN.md
---

# TWF-023: Prove Timberborn-Loadable Compute Bundle

## Goal

Produce a `wildfire_compute_mac` bundle that Timberborn can load as a real `ComputeShader`, or prove the specific Unity/Timborn constraint that prevents compute-shader AssetBundles from this pipeline.

## Requirements

- Start from the clean-deployed TWF-022 evidence, where the stale duplicate bundle is gone but the real FireSim bundle still fails Unity compatibility.
- Compare the Wildfire batchmode builder against `~/repos/timberborn-modding/Assets/Tools/Editor/Scripts/ModBuilding/AssetBundleBuilder.cs`.
- Investigate the Unity build-log warning: `'AssetBundle' is not supported because the module AssetBundle is disabled in the build.`
- Test the smallest diagnostic bundle needed to separate "all Wildfire bundles fail" from "ComputeShader bundles fail."
- Keep the production compute bundle outside Timberborn's `AssetBundles/` auto-load folder unless evidence proves auto-load is required and safe.
- Do not replace the compute shader with a no-op, CPU simulator, or dispatch-only success path.

## Dependencies

- `TWF-022` deploy validation must prevent stale non-FireSim bundles from being copied as `wildfire_compute_mac`.
- Live Timberborn QA must be available on macOS with Unity `6000.3.6f1`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run the Unity shader harness.
- Run `bun scripts/deploy-timberborn-mod.ts --apply --clean`.
- Restart Timberborn, load or continue the Wildfire save, then run `bun scripts/invoke-timberborn-command.ts status --wait=6`.
- Capture `Player.log` evidence for bundle load success/failure and `wildfire_command_result`.

## Notes

- TWF-022 clean deploy evidence: source and deployed bundle SHA matched `cb824a5bdfed742f014790f9d6c7b9a2250be8c7d0f01eaea5f1764f63a4809f`, and the manifest contained `Assets/WildfireGenerated/FireSim.compute`.
- Live blocker after that clean deploy: `The AssetBundle 'wildfire_compute_mac' could not be loaded because it is not compatible with this newer version of the Unity runtime. Rebuild the AssetBundle to fix this error.`
- Unity build-log warning to chase: `'AssetBundle' is not supported because the module AssetBundle is disabled in the build.`
- Research scaffold committed for this ticket: `src/Wildfire.Unity/UnityBatchmodeProject/Assets/Editor/DiagnosticTextAssetBundleBuilder.cs` builds a minimal `Diagnostic.txt` bundle through the same argument parser and official-style AssetBundle overload so the next worker can test whether a non-compute bundle loads in Timberborn.
