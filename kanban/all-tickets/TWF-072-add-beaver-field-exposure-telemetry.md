---
ticket: TWF-072
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-071
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-072-add-beaver-field-exposure-telemetry.md
---

# TWF-072: Add Beaver Field Exposure Telemetry

## Goal

Detect and report when beavers are in or near accepted wildfire fields without changing beaver behavior yet.

## Why

Before changing pathing, work, injury, or panic behavior, Wildfire needs a safe way to prove it can identify beaver exposure to fire, smoke, steam, ash, heat, and suppression fields. Telemetry gives QA a low-risk bridge between field simulation and beaver behavior.

## Requirements

- Implement the narrowest safe Timberborn adapter surface that can identify beaver positions or beaver-adjacent cells.
- Sample accepted wildfire fields from the existing visual-field or packed-cell surfaces without mutating the simulation grid.
- Classify exposure separately for respiratory danger, burn danger, contaminated smoke, clean steam, and tainted aftermath where field data can support it.
- Report bounded telemetry for exposed beavers or candidate cells through status, `qa-readiness`, logs, or a dedicated QA command.
- Avoid per-beaver spam; aggregate counts and only include bounded sample details.
- Add deterministic tests for exposure classification where possible.
- Document QA commands and expected tokens in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-071` defines which field exposures matter.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start with the safest read-only beaver position surface available in the Timberborn adapter.
- Exposure can be sampled over multiple game ticks; it does not need to process every beaver in the same tick if batching avoids frame spikes.
- Keep detail samples bounded, such as a few beaver IDs, cells, and classifications, with aggregate counts for the rest.
- Expected counters include sampled beavers, exposed beavers, respiratory exposure cells, heat exposure cells, toxic smoke cells, clean steam cells, tainted aftermath cells, batching skips, and unavailable API skips.
- Safe no-op behavior should report that beaver position sampling is unavailable without changing simulation or beaver state.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture live loaded-save evidence showing exposure telemetry either detects a controlled beaver/field case or reports an explicit safe unavailable state.

## Notes

- This ticket should not alter beaver behavior. It is the instrumentation layer for later behavior changes.
- Relevant design reference: `docs/DESIGN.md` section 20, "Beaver Field Effects" and "Contamination Interaction".
- 2026-05-19 design correction: steam is clean suppression vapor only; toxic exposure belongs to contaminated smoke or tainted aftermath.
- 2026-05-05 worker: added a telemetry-only beaver exposure sampler that reads Timberborn beaver entity positions through `EntityRegistry`, samples the existing GPU visual-field surface, classifies respiratory, burn, toxic, clean steam, contaminated smoke, and tainted-afterburn exposure counters, and exposes aggregate status/`qa-readiness` fields. Focused deterministic coverage passed for classification, aggregate field sampling, and safe-unavailable position API reporting.
- 2026-05-06 review failed after follow-up exposure fixes: the sampler caps inspected distinct candidate cells at 256 but reports every beaver as sampled with no skipped/batching counter, so larger settlements could underreport exposure silently. Moved back to `03-in-progress`; add bounded-sampling skip telemetry or batching before live QA.
- 2026-05-06 worker bounded-sampling fix: sampled beaver count now includes only beavers whose full candidate-cell set was inspected under the 256-cell visual-surface cap, and skipped partial beavers are exposed through aggregate log/status token `beaver_field_exposure_skipped_bounded_sampling`. Added deterministic cap coverage and status token coverage; verification: `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --filter "FullyQualifiedName~TimberbornBeaverFieldExposureTelemetryTests|FullyQualifiedName~TimberbornQaCommandBridgeTests"` and `git diff --check` passed.
- 2026-05-06 fresh review passed the bounded-sampling fix with no blocking findings. Reviewer confirmed sampled beaver count now only includes fully inspected candidate-cell sets under the 256-cell cap, and skipped partial beavers surface through snapshot, log, runtime status, and command bridge tokens. Verification passed: `git diff --check` and focused beaver telemetry plus QA command bridge tests with `90/90`. Keep in `04-verify`; live loaded-save exposure telemetry QA remains required before integration.
- 2026-05-06 live QA failed under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-072-qa-20260506T201200Z` using `/Users/jasonkleinberg/Documents/Timberborn/ExperimentalSaves/QA Tunnels and Booms/2026-05-06 16h04m, Day 2-5.autosave.timber`. Telemetry was available and safe, but the controlled fields did not overlap sampled beaver candidate cells.
- 2026-05-06 live QA failure detail: baseline and final tokens reported `beaver_field_exposure_available=true`, `sampled_beavers=27`, `exposed_beavers=0`, all respiratory/burn/toxic/steam/ash counters `0`, `skipped_no_position_api=0`, `skipped_bounded_sampling=0`, and `unavailable_reason=none`. Crop stimulus completed `12` sustained heat cycles at `x=25 y=18 z=4`, and center-tree stimulus queued `25` heat changes at `x=25 y=24 z=6` with `last_delta_count=42`, `visible_regions=16`, and `active_pooled_fire_effects=85`, but beaver exposure stayed zero. This is not an accepted safe-unavailable fallback because the beaver position API is available. Move back to `03-in-progress`; add a bounded beaver-proximate exposure selector or an accepted fixture/save path where a controlled field overlaps sampled beaver candidate cells.
- 2026-05-06 worker beaver-proximate selector fix: added `qa-delta-stimulus beaver-exposure`, which uses the same beaver position sampler and bounded candidate-cell logic as exposure telemetry, chooses a sampled beaver candidate cell, and queues sustained QA-only simulator heat/fuel there. The selector remains telemetry-only and does not change beaver pathing, work, health, contamination, behavior, or native state.
- 2026-05-06 worker selector verification passed: focused beaver telemetry and QA command bridge tests with `97/97`, `dotnet build Wildfire.slnx --no-restore` with `0` warnings and `0` errors, and `git diff --check`. Command/status proof now includes `target_selector=beaver-exposure`, `target_source=beaver_candidate_cell`, `beaver_exposure_target_beaver_*`, candidate-cell count, sampled-beaver count, skip counters, and sustained heat tokens; if beaver position sampling is unavailable, it fails safely without queueing simulator changes. Move back to `04-verify` for fresh review before rerunning live QA.
- 2026-05-06 fresh review passed the beaver-proximate selector fix with no blocking findings. Reviewer confirmed `qa-delta-stimulus beaver-exposure` selects a bounded sampled beaver candidate cell through the telemetry sampler path, queues only QA simulator heat/fuel, and reports target/source/beaver/sample/skip tokens. Verification passed: `git diff --check` and focused beaver telemetry plus QA command bridge tests with `97/97`. Keep in `04-verify`; rerun the failed live QA gate with the new selector before integration.
- 2026-05-06 live QA passed under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-072-qa-20260506T205748Z` using `/Users/jasonkleinberg/Documents/Timberborn/ExperimentalSaves/QA Tunnels and Booms/2026-05-06 16h40m, Day 2-6.autosave.timber`. QA ran `qa-delta-stimulus beaver-exposure --wait=30 --require-advanced-tick`, then follow-up `status` and `qa-readiness` with `--require-sustained-heat-cycles=12`.
- 2026-05-06 live QA proof: selector tokens reported `target_selector=beaver-exposure`, `target_source=beaver_candidate_cell`, target beaver id `0636d5b7-f6b4-4b28-9b4e-254f35a270cd`, target cell `x=16 y=4 z=13`, `candidate_cells=9`, and `sampled_beavers=27`. Sustained heat completed `12/12` cycles. Follow-up telemetry reported `beaver_field_exposure_available=true`, `sampled_beavers=27`, `exposed_beavers=2`, `respiratory_cells=8`, `skipped_no_position_api=0`, `skipped_bounded_sampling=0`, and `unavailable_reason=none`. Behavior mutation scan found no beaver pathing, work, health, contamination, panic, injury, incapacitation, death, or native state mutation tokens. Move to `05-integration`.
- 2026-05-06 integration complete. Integration review passed with `git diff --check` and focused beaver telemetry plus QA command bridge tests `97/97`. Accepted scope is telemetry only: the beaver-proximate QA selector produced nonzero exposure telemetry, and the negative scan found no beaver behavior/native mutation tokens.
