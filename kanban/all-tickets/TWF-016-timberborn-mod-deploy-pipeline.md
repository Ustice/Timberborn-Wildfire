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
