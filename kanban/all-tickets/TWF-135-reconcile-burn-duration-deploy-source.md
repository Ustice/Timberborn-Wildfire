---
ticket: TWF-135
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-089
  - TWF-134
write_scope:
  - src/Wildfire.Unity/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-135-reconcile-burn-duration-deploy-source.md
  - kanban/all-tickets/TWF-089-tune-fuel-burn-down-duration.md
---

# TWF-135: Reconcile Burn Duration Deploy Source

## Goal

Produce one verified deployable source tree that contains both the accepted `TWF-089` burn-duration shader tuning and the accepted `TWF-134` live proof command.

## Why

`TWF-089` live QA cannot run low, medium, and high burn-duration proof while the deployable pieces are split. `main` has `qa-burn-duration-stimulus` and durable `burn_duration_proof_*` fields from `TWF-134`, but lacks the accepted `FireSim.compute` burn-duration tuning. `~/repos/wildfire-TWF-089` has the accepted shader tuning and snapshots, but lacks the `TWF-134` command bridge. Running either tree alone would produce invalid evidence.

## Requirements

- Reconcile the accepted `TWF-089` production shader tuning into the same checkout that contains `TWF-134`.
- Preserve the `TWF-134` command bridge behavior and tests.
- Preserve `TWF-089` deterministic low, medium, and high burn-down shader evidence.
- Do not change spread pace, suppression, structure behavior, cooling, visuals, or Timberborn consequence accounting.
- Do not broaden the QA bridge into arbitrary coordinate or packed-cell mutation.
- Keep generated evidence paths and ticket notes pointing to the source tree used for deploy.

## Dependencies

- `TWF-089` supplies the reviewed shader tuning and shader evidence.
- `TWF-134` supplies the reviewed live proof command.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Source of accepted burn-duration tuning: `~/repos/wildfire-TWF-089` at commit `082077d2b99819c4b448b0ba9fe758ed81f4f412`.
- The TWF-089 diff includes `src/Wildfire.Unity/FireSim.compute`, `tests/Wildfire.Core.Tests/UnityShaderExecutionHarnessTests.cs`, release shader snapshots, and `tests/Wildfire.Core.Tests/ShaderSnapshots/twf-089/**`.
- Main already contains the accepted `TWF-134` bridge command and durable proof fields.
- The live preflight blocker evidence is `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-089-live-retry-preflight-20260503T163733Z/`.

## Verification

- Run `git diff --check`.
- Run `dotnet test --filter TimberbornQaCommandBridgeTests`.
- Run relevant shader snapshot/harness tests for `TWF-089`.
- Run full `dotnet test` if feasible.
- Live QA must rerun `TWF-089` after this ticket passes review using `qa-burn-duration-stimulus low`, `medium`, and `high`.

## Notes

- 2026-05-03 coordinator: created after `TWF-089` live QA correctly stopped at preflight because no deploy source contained both the reviewed shader tuning and the reviewed proof command.
- 2026-05-03 worker result: reconciled the reviewed `TWF-089` source artifacts from `~/repos/wildfire-TWF-089` commit `082077d2b99819c4b448b0ba9fe758ed81f4f412` into this checkout while preserving the already-present `TWF-134` command bridge. Imported `src/Wildfire.Unity/FireSim.compute`, release shader captures, low/medium/high `tests/Wildfire.Core.Tests/ShaderSnapshots/twf-089/` fixtures and captures, and the deterministic harness assertions. No Timberborn bridge files were edited for this reconciliation.
- 2026-05-03 worker scope note: the imported shader source carries the accepted `TWF-088` spread baseline that `TWF-089` was reviewed against, plus the `TWF-089` fuel burn-down roll. This worker did not tune suppression, structure behavior, cooling, visuals, or Timberborn consequence accounting.
- 2026-05-03 worker verification: `git diff --check` passed, `dotnet test --filter TimberbornQaCommandBridgeTests` passed with 42 tests, `dotnet test --filter FullyQualifiedName~UnityShaderExecutionHarnessTests` passed with 6 tests, full `dotnet test` passed with 197 tests, and the opt-in Unity batchmode shader harness passed with `WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled`.
- 2026-05-03 review: passed with no blocking findings. `src/Wildfire.Unity/FireSim.compute`, `tests/Wildfire.Core.Tests/UnityShaderExecutionHarnessTests.cs`, and release shader captures match reviewed `TWF-089` commit `082077d2b99819c4b448b0ba9fe758ed81f4f412`; the imported `tests/Wildfire.Core.Tests/ShaderSnapshots/twf-089/` directory is byte-for-byte identical to that commit. Review confirmed the already-present `TWF-134` bridge still exposes `qa-burn-duration-stimulus low|medium|high` and durable `burn_duration_proof_*` fields without accepting arbitrary coordinates or packed-cell values.
- 2026-05-03 review verification: `git diff --check` passed; `dotnet test --filter TimberbornQaCommandBridgeTests` passed with 42 tests; `dotnet test --filter FullyQualifiedName~UnityShaderExecutionHarnessTests` passed with 6 tests; full `dotnet test` passed with 197 tests. Targeted board check found `TWF-135` in `04-verify`, `TWF-134` in `06-done`, and `TWF-089` still in `07-blocked` pending the live low/medium/high burn-duration proof retry, which remains appropriate until QA reruns from this reconciled deploy source.
