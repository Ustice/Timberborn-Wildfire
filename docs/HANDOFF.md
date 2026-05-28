# Wildfire Handoff

## Current State

- Wildfire lives in `~/repos/wildfire`.
- GitHub Issues own the active backlog: <https://github.com/Ustice/Timberborn-Wildfire/issues>.
- The file-kanban archive lives on branch `archive/file-kanban-2026-05-23`; use `kanban/github-issue-migration.md` to map historical `TWF-*` ids to issue numbers.
- The simulation has one authoritative GPU path. `Wildfire.Core` owns packed data and host-facing contracts, `Wildfire.Unity` owns compute dispatch and shader-side field state, and `Wildfire.Timberborn` owns adapter import, QA commands, visuals, alerts, persistence, and gameplay consequences.
- The old alternate C# fire-spread execution path is gone. Future simulation behavior should target `FireSim.compute` and the Unity shader harness.
- Timberborn QA has a file-backed command bridge with `status`, `qa-readiness`, stimulus commands, deploy tooling, coordinate references, and live evidence conventions.
- The initial public release platform target is macOS only, contingent on `TWF-104` validating the packaged release artifact in a real macOS Timberborn run. Windows is unvalidated and unsupported unless `TWF-105` and `TWF-106` complete before release; Linux, SteamOS, Steam Deck, Proton, and other platforms are unsupported for the first public release.
- Steam Workshop packaging uses `release/workshop/content/` as the SteamCMD `contentfolder` and `release/workshop/content/version-1.0/` as the Timberborn mod payload. The generic `release/package/Wildfire-*.zip` is not the Workshop upload body. First-release Workshop publishing is intentionally manual at the Steam credential, Steam Guard, metadata review, and visibility boundary; use `bun run workshop:publish` only as the guarded SteamCMD wrapper documented in [release/workshop.md](release/workshop.md).

## Durable References

- Design: [DESIGN.md](DESIGN.md), [ARCHITECTURE.md](ARCHITECTURE.md), and [RELEASE_DESIGN.md](RELEASE_DESIGN.md).
- Validation: [TEST_PLAN.md](TEST_PLAN.md) and [reference/timberborn-deploy-pipeline.md](reference/timberborn-deploy-pipeline.md).
- Current model notes: [ash-simulation-model.md](ash-simulation-model.md), [steam-simulation-model.md](steam-simulation-model.md), [fire-sim-field-model-plan.md](fire-sim-field-model-plan.md), and [world-consequence-first-pass.md](world-consequence-first-pass.md).
- Timberborn UI references: [timberborn-menu-coordinate-guide.md](timberborn-menu-coordinate-guide.md), [timberborn-bottom-menu-guide.md](timberborn-bottom-menu-guide.md), and [timberborn-debug-panels.md](timberborn-debug-panels.md).
- GitHub issue workflow: [../kanban/github-issue-workflow.md](../kanban/github-issue-workflow.md), [../kanban/github-issue-migration.md](../kanban/github-issue-migration.md), and [../kanban/README.md](../kanban/README.md).

## Active Backlog

Do not mirror GitHub issue status in this file. Start from GitHub every time:

```bash
gh issue list --repo Ustice/Timberborn-Wildfire --state open --label status:ready
gh issue list --repo Ustice/Timberborn-Wildfire --state open --label status:qa-needed
gh issue list --repo Ustice/Timberborn-Wildfire --state open --label status:blocked
gh issue list --repo Ustice/Timberborn-Wildfire --state open --label source:kanban --limit 100
```

As of the 2026-05-23 migration, the first ready candidates were:

- `TWF-087` [#40](https://github.com/Ustice/Timberborn-Wildfire/issues/40): Implement Beaver Fire Heat Exposure.
- `TWF-167` [#36](https://github.com/Ustice/Timberborn-Wildfire/issues/36): Define Ash Water Interaction.
- `TWF-174` [#39](https://github.com/Ustice/Timberborn-Wildfire/issues/39): Stop Stumps From Counting As Fuel Sources.

Treat that list as a migration snapshot only. Refresh from GitHub before assigning work.

## Current Cautions

- Keep live-QA issues blocked until Timberborn is closed or restarted normally and `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick` returns from a loaded save.
- Do not imply progress on behavior or real-field blockers until their GitHub issue has fresh command/status/log or visual evidence.
- If shader behavior changes, use the opt-in Unity harness command documented in [TEST_PLAN.md](TEST_PLAN.md) before accepting the change.
- If a ticket fails required QA or review, keep the GitHub issue open and update labels/comments with the exact blocker.
- Use `bun run typecheck` before accepting TypeScript script changes.
- Before any real Steam Workshop upload or update, run `bun run workshop:package` and `bun run workshop:publish -- --dry-run --skip-preview --user <steam-account>`, then confirm Steam account permission, Steam Guard access, `steamcmd`, preview size, VDF metadata, and the final `publishedfileid`. The current planned Workshop item id is `3730392791`, but it remains a confirmation blocker until a real authenticated SteamCMD result or Workshop URL proves it.
- Do not claim platform support from package contents alone. Platform support needs packaged-artifact evidence plus live `Player.log` and `status` or `qa-readiness` proof on that platform; Windows is blocked on a Windows Timberborn validation environment and Windows AssetBundle packaging, while Linux/SteamOS/Deck need an explicit support decision and live target environment.
- As of 2026-05-24 16:03 EDT, Timberborn was redeployed and relaunched after the fail-loud consequence cleanup. The previous live crash produced `/Users/jasonkleinberg/Documents/Timberborn/Error reports/error-report-2026-05-24-15h53m09s.zip` and ended with `InvalidOperationException: Field of LackOfResourcesStatus named _activePredicate isn't null` while `BuildExecutor` finished construction. The lead-in showed repeated `wildfire_timberborn_structure_burned_visual_applied` lines for lodges, foresters, bakeries, and district center cells. Treat saves touched by that run as potentially polluted by native construction-state rollback.
- `TimberbornStructureBurnDamageRollbackTargetApi` should attempt rollback work instead of pre-declaring structures unsupported: enter Timberborn `Unfinished` state when the native state transition works, burn construction materials, reset construction progress when a construction site is present, close/interrupt workplaces, and apply burned textures or tint materials. Real exceptions from those attempts should still crash loudly through `TimberbornFireRuntime` after logging `wildfire_timberborn_runtime_dispatch_failed`.
- The fresh post-deploy `Player.log` only proved Timberborn reached the menu with Wildfire loaded; it did not yet prove a loaded save initializes the runtime. Before resuming live QA, load a clean save if possible and rerun `qa-readiness` from the loaded world.

## Useful Commands

```bash
bun run typecheck
dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj
bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout 60
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick
bun run workshop:package
bun run workshop:publish -- --dry-run --skip-preview --user <steam-account>
```

Use this suppression proof when a ticket needs water-change evidence:

```bash
bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=6 --require-advanced-tick
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-water-changed
```

Use this profile summary when revisiting full-grid dispatch or active-frontier work:

```bash
bun scripts/summarize-dispatch-profile.ts "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-031-live-20260502T143543Z"
```

## Known Gaps

- `FireSim.compute` has local Unity batchmode proof, but CI does not run it unless Unity Editor licensing and compute-shader capable graphics are available.
- The broader Timberborn UI is not exhaustively mapped. Add settings, new-game, map-editor, credits, deeper Mods flows, or destructive load/delete/exit targets only through explicitly assigned QA passes with fresh screenshot evidence.
- Timberborn-native damage, effect, alert, contamination, and beaver-state interactions should stay guarded by deterministic tests plus live evidence because those APIs are version-sensitive.
