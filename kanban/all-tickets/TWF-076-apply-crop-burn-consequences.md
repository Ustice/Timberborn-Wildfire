---
ticket: TWF-076
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-075
  - TWF-114
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-076-apply-crop-burn-consequences.md
---

# TWF-076: Apply Crop Burn Consequences

## Goal

Use burn damage to reduce crop and harvestable yield as fuel burns, then kill and visually mark the crop when its yield is consumed.

## Why

The design says fire consumes burnable world value. Crops are the farming-facing version of that rule: as fuel burns away, harvest yield should fall, and a fully burned crop should die rather than remaining harvestable.

## Requirements

- Consume compact fire deltas through the Timberborn consequence path.
- Reduce crop and harvestable yields according to accepted burn damage.
- Kill fully burned crops when their remaining yield is consumed.
- Switch burned crops to an accepted dead, burned, or charred presentation when Timberborn exposes a safe visual path.
- Preserve host boundaries; do not move fire-spread rules into Timberborn code.
- Add deterministic tests for crop yield loss, full-burn death, duplicate-cell suppression, and no-op behavior for non-burnable or unmapped targets.
- Expose bounded QA/status telemetry for considered crop targets, yield lost, killed targets, and visual-state updates.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-075` provides the burn damage state foundation.
- `TWF-114` provides resource fuel and flammability classification for yield-derived fuel value.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start with harvestable/crop spec lookup and the burn damage target resolution from `TWF-075`.
- Damage capacity should be based on remaining crop or harvestable yield, adjusted by the `TWF-114` resource catalog when the harvested good is known.
- Safe no-op cases must include unmapped cells, unknown harvest resources, non-burnable yields, and unavailable plant-death or visual APIs.
- Do not emit one player alert per crop; publish aggregate counters for `TWF-080`.
- Expected counters include considered crops, burnable crops, yield lost, killed crops, visual updates, skipped unknown resources, and skipped unsafe APIs.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for at least one burned crop consequence, plus copied `Player.log` and status counters.

## Notes

- Tree burn consequences belong to `TWF-084`.
- Structure burn consequences belong to `TWF-077`.
- Avoid one alert per crop. Player-facing aggregation belongs to `TWF-080`.
- Relevant design reference: `docs/DESIGN.md` section 20, "Burn Damage State".
- 2026-05-03 coordinator: moved to `03-in-progress` for Sprint 7 in `~/repos/wildfire-TWF-076` on branch `codex/TWF-076-crop-burn-consequences`, based on reviewed and integrated `TWF-075` commit `00eac0e7ac12b1f7084be3e5bfaba2dbff1033e3`.
- 2026-05-03 coordinator: serialize this broad `src/Wildfire.Timberborn/**` and `tests/Wildfire.Core.Tests/**` implementation lane ahead of `TWF-084` tree consequences unless scopes are narrowed. Required review is a hard gate; if review fails, return through `03-in-progress` for fixes, then back to `04-verify` for fresh review before integration.
- 2026-05-03 worker result: deterministic implementation committed in `~/repos/wildfire-TWF-076` at `1b5083e719e1fd7d4220cda47fce0ea745d2a2b9`. The worker added an adapter-local crop/harvestable consequence sink downstream of `TimberbornBurnDamageService`, runtime aggregate crop-burn counters, and safe default unavailable API behavior.
- 2026-05-03 worker evidence: deterministic tests cover crop yield loss, full-burn death/visual requests through an injected safe API, duplicate-cell suppression, non-burnable no-op, unmapped targets, unknown harvest resources, skipped unsafe API behavior, and QA/status telemetry. Runtime defaults to an unavailable safe API and does not claim live yield loss, death, or visual updates without a real safe binding.
- 2026-05-03 worker verification: `git diff --check` passed, full `dotnet test` passed with `166` tests, and `dotnet build Wildfire.slnx` passed with `0` warnings and `0` errors.
- 2026-05-03 worker blocker: live Timberborn launch/load remains blocked, so no live evidence was captured or claimed. Before integration, QA still needs `Player.log`, command output, nonzero crop-burn counters, safe crop/yield/death or precise skipped-unsafe-API evidence, and proof there is no per-crop alert spam.
- 2026-05-03 coordinator: moved to `04-verify` for review. Even if review passes, do not move to `05-integration` until required live QA evidence passes.
- 2026-05-03 review: passed deterministic review on commit `1b5083e719e1fd7d4220cda47fce0ea745d2a2b9` with no blocking findings. Review confirmed `Wildfire.Core` remains unchanged, the implementation stays crop/harvestable-only, unavailable live Timberborn APIs fail safely through skipped telemetry, and tree, structure, storage, explosive, ash, beaver, release-setting, and per-crop alert behavior remain out of scope.
- 2026-05-03 review verification: `git diff --check 00eac0e7...1b5083e` passed, targeted `dotnet test --filter FullyQualifiedName~TimberbornCropBurnConsequenceTests` passed with `7/7`, full `dotnet test` passed with `166/166`, and `dotnet build Wildfire.slnx` passed with `0` warnings and `0` errors.
- 2026-05-03 coordinator: moved to `07-blocked`, not integration. Required live gate: deploy after Timberborn launch/load is unblocked, register at least one real crop or harvestable with known yield, capture compact-delta burn sequence, preserve `Player.log`, command output, and `qa-readiness`/`status` tokens showing nonzero crop-burn considered targets, show real yield loss/death/visual update through a safe API or precise `skipped_unsafe_apis` evidence, and prove no per-crop alert spam.
