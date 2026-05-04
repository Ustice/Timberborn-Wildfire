---
ticket: TWF-016
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-012
write_scope:
  - scripts/**
  - docs/TEST_PLAN.md
  - docs/reference/**
  - src/Wildfire.Timberborn/**
---

# TWF-016: Add Timberborn Mod Deploy Pipeline

## Goal

Create a repeatable path that builds and deploys the Wildfire Timberborn adapter so the running game can load it.

## Why

The current repo can build .NET projects and inspect Timberborn UI, but it does not yet have a concrete route from `src/Wildfire.Timberborn` into `~/Documents/Timberborn/Mods/`. Without that, `TWF-012`, `TWF-008`, and later live validation can only prove scaffolding outside the game.

## Requirements

- Define the deployed Wildfire mod folder shape, including manifest and managed assemblies.
- Build or stage the Timberborn adapter assembly and any required Wildfire assemblies.
- Copy or link shippable files into the local Timberborn mods directory.
- Keep internal docs and kanban files out of deployed mod content.
- Use a serialized build/deploy lock so concurrent agents do not redeploy during live QA.
- Log the deployed paths and versions.
- Document prerequisites, command usage, expected output, and cleanup.
- Preserve `Wildfire.Core` as host-agnostic and keep Timberborn as an adapter.

## Dependencies

- `TWF-012` should define the first code path worth proving from inside the game.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run the deploy command in dry-run mode if available.
- Run the deploy command for real when Timberborn is closed or the workflow is otherwise safe.
- Launch Timberborn and capture `Player.log` evidence that the Wildfire mod folder or assembly is discovered, or document the smallest missing hook if the current adapter cannot load yet.

## Notes

- Use the Timberborn modding guide under `docs/reference/` as the starting source.
- Do not broaden this into gameplay validation. This ticket only proves build, staging, deploy, and game-load evidence.
- Worker update 2026-05-01:
  - Added `scripts/deploy-timberborn-mod.ts`, a Bun/TypeScript direct-deploy script for the Wildfire Timberborn adapter.
  - The deployed shape is `~/Documents/Timberborn/Mods/Wildfire/manifest.json` plus `Scripts/Wildfire.Timberborn.dll`, `Scripts/Wildfire.Core.dll`, and optional PDBs.
  - `Scripts/` is the deployed managed-assembly directory, matching the official Timberborn mod builder convention.
  - The script builds `Wildfire.slnx` by default, dry-runs by default, and supports `--apply`, `--dry-run`, `--skip-build`, `--clean`, `--remove`, `--mods-dir`, `--configuration`, `--lock-timeout`, `--force-lock`, and `--help`.
  - Deploy/remove is serialized with `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`.
  - Internal docs, kanban files, source files, and tests are not copied; only generated manifest and managed assemblies are staged.
  - Dry-run evidence: `bun scripts/deploy-timberborn-mod.ts` passed, built the solution, detected `timberborn_running=true`, and printed target paths/artifact plan without writing to `~/Documents/Timberborn/Mods`.
  - Real-deploy safety evidence: `bun scripts/deploy-timberborn-mod.ts --apply --skip-build` refused to write because Timberborn is running and reported `Timberborn appears to be running`.
  - Verification passed: `git diff --check`; `dotnet test` passed 68 tests; `dotnet build Wildfire.slnx` succeeded with 0 warnings and 0 errors; `bun scripts/deploy-timberborn-mod.ts` dry-run succeeded.
  - Player.log evidence is blocked until a real deploy can run: current `Player.log` and `Player-prev.log` contain only the save name `Wildfire testing`, not a Wildfire mod folder or `Wildfire.Timberborn.dll` load line.
  - Live deploy was not run because Timberborn is open; real deploy should wait until Timberborn is closed or be run only with explicit `--allow-open-game`.
  - Smallest live unblock: close Timberborn, run `bun scripts/deploy-timberborn-mod.ts --apply`, launch Timberborn, then inspect `~/Library/Logs/Mechanistry/Timberborn/Player.log` for Wildfire mod folder or assembly discovery.

## QA Notes - 2026-05-01 Follow-Up

- First real deploy proved the mod folder was discovered, but Timberborn crashed on startup because the deployed assemblies targeted `.NET 10` and tried to load `System.Linq, Version=10.0.0.0`.
- Retargeted `Wildfire.Core` and `Wildfire.Timberborn` to `netstandard2.1`, added record compatibility shims, and removed newer BCL calls from the Timberborn-facing path.
- Redeployed with `bun scripts/deploy-timberborn-mod.ts --apply --allow-open-game --skip-build`; deploy copied `netstandard2.1` `Wildfire.Timberborn.dll` and `Wildfire.Core.dll`.
- `Player.log` evidence after restart:
  - `Modded: true, official`
  - `- Wildfire (v0.1.0.0)`
  - `Loading saved game Wildfire testing - 2026-05-01 07h49m, Day 1-2.autosave at 2026-05-01 07:54:28Z`
- Screenshot evidence:
  - `docs/reference/screenshots/timberborn-menu-coordinate-guide/07-startup-mods-wildfire.png`
  - `docs/reference/screenshots/timberborn-menu-coordinate-guide/08-post-startup-loaded-save.png`
- Result: deploy pipeline and game-load proof passed after compatibility fix.
