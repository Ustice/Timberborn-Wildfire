---
ticket: TWF-088
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-065
  - TWF-043
  - TWF-046
write_scope:
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-088-tune-fire-spread-pace.md
---

# TWF-088: Tune Fire Spread Pace

## Goal

Tune how quickly fire spreads between eligible cells in representative live and shader scenarios.

## Why

Spread pace is the first thing players feel. It should be dangerous enough to matter without making a small ignition instantly unrecoverable.

## Requirements

- Keep fire-spread rules in `FireSim.compute`.
- Compare spread in single ignition, line of fuel, sparse forest, and building cluster scenarios.
- Preserve the 6-neighbor release decision unless a new design decision explicitly changes it.
- Tune ignition or spread constants only with deterministic shader evidence and live recording evidence.
- Keep visual scale, prefab choice, and player alerts out of this ticket.
- Document accepted evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-065` provides recording tooling.
- `TWF-043` provides the current game-feel baseline.
- `TWF-046` proves the coherent live loop.

## Parent Reference

- Parent gate: `TWF-069`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness when shader behavior changes.
- QA must capture low-resolution spread recordings plus command/status evidence.

## Implementation Notes

- Start from the current `FireSim.compute` spread constants and add deterministic scenario evidence before accepting a new pace.
- Treat live recording as a required QA gate for integration, but keep the ticket blocked if the current Steam or Timberborn launch prompt prevents a command-responsive loaded save.

## Notes

- Keep this to spread pace. Burn duration, suppression, structure behavior, and cooling have separate child tickets.
- 2026-05-03 coordinator: moved to `03-in-progress` for Sprint 6 worker implementation in `~/repos/wildfire-TWF-088` on branch `codex/TWF-088-fire-spread-pace`, based on accepted `TWF-065` recording tooling.
- 2026-05-03 worker result: deterministic implementation complete in commit `8eff5cf6adf85cf8729ab19c1abdb592a7f549e3`. Changed only spread pace by tuning `FIRE_BURNING_NEIGHBOR_HEAT_BONUS` from `3` to `5`, preserving the six-neighbor release decision. Regenerated release shader snapshots and documented deterministic spread evidence in `docs/TEST_PLAN.md`.
- 2026-05-03 worker verification: `git diff --check` passed, `dotnet test` passed with 133 tests, and the opt-in Unity shader harness passed across release captures. Shader capture evidence was generated under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-088-spread-pace/`.
- 2026-05-03 coordinator: moved to `04-verify` for review and QA triage. Live Timberborn recording remains required before integration; if the launch prompt still blocks a command-responsive loaded save, this ticket must move to `07-blocked` rather than `05-integration`.
- 2026-05-03 review: passed Tech-Lead review on commit `8eff5cf6adf85cf8729ab19c1abdb592a7f549e3` with no blocking architecture or correctness findings. Review confirmed the change stays scoped to spread pace, keeps fire rules in `FireSim.compute`, preserves six-neighbor reads, and does not creep into visuals, suppression, fuel duration, structure behavior, cooling, alerts, prefab selection, or Timberborn-owned rule logic.
- 2026-05-03 QA: live recording gate blocked. Deploy passed from `~/repos/wildfire-TWF-088`, but `load-latest-save-and-unpause` timed out after 161 unknown frames, `qa-readiness --require-advanced-tick` timed out waiting for `command-outbox.txt`, final state showed `steam_osx` frontmost, no Timberborn process, and no stale QA lock. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-088-live-20260503T073516Z`; worktree evidence manifest: `~/repos/wildfire-TWF-088/kanban/evidence-manifests/TWF-088-qa-20260503T073516Z.md`.
- 2026-05-03 coordinator: moved to `07-blocked`. Before integration, rerun the required live gate: launch/load/unpause into a command-responsive Timberborn save, prove it with `qa-readiness` or `status`, run a visible spread stimulus, capture the low-resolution spread recording with `scripts/record-timberborn-qa.ts`, and preserve command output plus copied `Player.log`.
- 2026-05-03 coordinator update: keep this blocked behind `TWF-050` command responsiveness, not the older Steam prompt diagnosis. Once `TWF-050` proves loaded-save command output again, this reviewed deterministic ticket should return to `04-verify` for its required live spread recording.
- 2026-05-03 QA rerun: live recording gate passed from `~/repos/wildfire-TWF-088` on branch `codex/TWF-088-fire-spread-pace` at commit `8eff5cf6adf85cf8729ab19c1abdb592a7f549e3`. `caffeinate -disu` PID `94422` was active before and after the run. Timberborn PID `44014` was quit cleanly before deploy; deploy, launch, load, and unpause passed, with post-unpause `status` at `tick_count=3` and explicit `qa-readiness --require-advanced-tick` at `tick_count=13`.
- 2026-05-03 QA evidence: root `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-088-live-20260503T161336Z/`. Low-resolution recording `screen-recordings/2026-05-03T16-15-07-544Z-low/recording.mov` captured the spread-stimulus window; metadata is beside it in `recording-metadata.json`. `qa-delta-stimulus` queued center cell `188480` at `x=64 y=64 z=11` with `set_cell=13311`; follow-up `qa-readiness --require-advanced-tick --require-nonzero-delta` passed at `tick_count=33` with `last_delta_count=1`, `last_delta_consumer_changed_cells=1`, `last_delta_consumer_gameplay_consequences=1`, `updated_visual_regions=1`, `player_fire_alert_notification_sent=true`, and `pooled_fire_effects_native_prefab=CampfireFire`.
- 2026-05-03 QA log/process evidence: copied `Player.log`, `player-log-spread-tokens.txt`, `player-log-stimulus-focused-tokens.txt`, and `dispatch-profile-summary.txt` are in the evidence root. Log tokens show `wildfire_timberborn_changes_registered source=qa_delta_stimulus`, `wildfire_timberborn_qa_delta_stimulus_queued`, nonzero GPU readbacks and dispatches at ticks `31` through `36`, native prefab resolution to `CampfireFire`, and player alert delivery. Final `status` at `tick_count=59` stayed command-responsive; final process state kept Timberborn running as PID `50103`, kept `caffeinate -disu` PID `94422`, and found no shared QA lock file in either lock root.
- 2026-05-03 coordinator: moved to `06-done` after deterministic implementation, review, and the required low-resolution live recording QA all passed. This also unblocks `TWF-089` for its burn-duration live recording gate.
