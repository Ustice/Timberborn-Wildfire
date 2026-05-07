---
ticket: TWF-153
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-130
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-153-implement-detonator-fire-safety-behavior.md
---

# TWF-153: Implement Detonator Fire Safety Behavior

## Goal

Define and implement conservative fire behavior for placed `Detonator` targets without corrupting automation state.

## Why

`TWF-130` found that detonators are trigger devices constrained to sit on dynamite, with native `Arm()`, `Disarm()`, and `Evaluate()` methods. They are not ordinary fuel, and premature arming is riskier than disabling or marking the device unsafe.

## Requirements

- Resolve placed detonator targets from compact fire deltas.
- Treat detonators as trigger devices, not fuel.
- Add a setting gate for detonator fire behavior.
- Start with disable or unsafe-mark behavior unless a safe native arming path is proven.
- Preserve recoverability of automation state after fire ends.
- Suppress duplicate cells for the same detonator target in one dispatch.
- Expose telemetry for considered, disabled, armed, skipped setting disabled, skipped no-safe-api, duplicate suppressed, and recoverability state.
- Add deterministic tests for settings gates, duplicate suppression, disabled behavior, safe unavailable APIs, and no direct terrain mutation.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Implementation Notes

- Start with a wrapper around `Detonator` state changes and a deterministic no-op path.
- Prefer disabled or unsafe-mark behavior before any premature arming behavior.
- Keep automation state recoverability visible in telemetry and QA notes.
- Do not trigger adjacent dynamite from this ticket unless a later accepted proof says that path is safe.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for one detonator being disabled or explicitly skipped without corrupting automation state.

## Notes

- Do not use detonator behavior to trigger dynamite until a separate accepted proof says that path is safe.
- Keep native dynamite triggering in `TWF-152`.
- Keep tunnel destruction in `TWF-154`.
- 2026-05-05 worker: implemented a conservative detonator fire-safety consequence lane. Compact fire deltas now resolve placed detonator targets through an adapter API, suppress duplicate cells by stable target id, call only a wrapped disable/disarm behavior, and report `detonator_fire_safety_*` summary plus QA status fields. The lane never arms, evaluates, triggers adjacent dynamite, emits heat pulses, or mutates terrain.
- 2026-05-05 worker learning: Timberborn's `Detonator` runtime type is present in `Timberborn.AutomationBuildings.dll`, but it is not publicly accessible to the mod assembly. The live adapter therefore uses reflection to resolve `GetObjectsWithComponentAt<Detonator>` and call wrapped `Disarm()`, while the deterministic sink remains typed behind `ITimberbornDetonatorFireSafetyTargetApi`.
- 2026-05-06 review failed: reflected detonator lookup and `Disarm()` invocation can throw or silently skip instead of reporting safe-unavailable/recoverability telemetry. Moved back to `03-in-progress`; require containment tests and fresh review before live QA.
- 2026-05-06 worker safety fix: contained detonator resolver and `Disarm()` failures as `skipped_no_safe_api` plus recoverability-unknown telemetry, exposed unavailable reflection lookup as a safe-unavailable target, and added resolver/disable failure regression tests.
- 2026-05-06 live QA failed after deploy/load/readiness succeeded under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-152-153-154-live-20260506T143435Z/`. The loaded save and ExperimentalSaves scan did not find a placed `Detonator` target, so the required disable or safe-skip recoverability gate was not reached. Moved back to `03-in-progress`; add a stable detonator fixture or safe targetable QA surface before rerunning.
- 2026-05-06 review failed the direct targeting unblock: `qa-delta-stimulus detonator` can treat a synthetic `detonator-unavailable:<cell>` safe-unavailable target as a real placed direct target and queue simulator heat/fuel. Keep in `03-in-progress`; direct QA targeting must fail cleanly without queueing when only unavailable pseudo-targets exist.
- 2026-05-06 QA-targeting unblock worker: added `qa-delta-stimulus detonator`, which scans placed infrastructure through the existing detonator fire-safety target API and queues only conservative Wildfire heat/fuel changes at the resolved target cell. Command queueing does not arm, evaluate, disarm, or trigger adjacent dynamite; the existing consequence sink owns the later default-safe disable/safe-unavailable telemetry if a placed detonator exists in the loaded save.
- 2026-05-06 worker direct-selector fix: `qa-delta-stimulus detonator` now rejects `detonator-unavailable:<cell>` pseudo-targets as no placed target and exits without queueing simulator changes. Regression coverage: `QueueQaDeltaStimulusDirectConsequenceSelectorsRejectUnavailablePseudoTargets`.
- 2026-05-06 live QA on `QA Tunnels and Booms` failed under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-153-154-qa-20260506T155606Z/`: the exact save scan found tunnels and dynamite but no detonator strings/templates, and runtime `qa-delta-stimulus detonator` failed cleanly with `queued_changes=0` and `No_placed_Timberborn_target_was_found_for_QA_selector_'detonator'`. Moved to `07-blocked`; unblock requires a save/fixture with a real placed `Detonator`.
- 2026-05-06 Jason correction: `QA Tunnels and Booms` was intended to include detonators. A fresh static archive pass over all current `QA Tunnels and Booms` `.timber` files found 7 each of `Dynamite.Folktails`, `DoubleDynamite.Folktails`, and `TripleDynamite.Folktails`, plus 10 `Tunnel.Folktails`, but still no literal `Detonator` template/component string. Treat the blocker as a target-resolution/design mismatch, not simply missing save content: Timberborn may represent detonator behavior as a component on dynamite, through an automation object not named `Detonator`, or via a runtime-only type that the current save scan and `qa-delta-stimulus detonator` selector miss. Move back to `03-in-progress` for a narrow live/API reconciliation pass before the next QA run.
- 2026-05-06 worker reconciliation: confirmed Timberborn has an internal `Timberborn.AutomationBuildings.Detonator` runtime/blueprint surface, but the current `QA Tunnels and Booms` saves still serialize only `Dynamite.Folktails`, `DoubleDynamite.Folktails`, `TripleDynamite.Folktails`, and `Tunnel.Folktails` with no `Detonator.*` template string. Native inspection also showed `Detonator.Disarm()` is non-public, so the wrapper now searches public and non-public zero-argument `Disarm()` only and falls back to safe-unavailable telemetry when absent or failing. Tests prove non-public `Disarm()` works and `Arm()`/`Evaluate()` are not called. Verification passed: focused detonator plus QA bridge tests with `90` tests, `dotnet build Wildfire.slnx`, and `git diff --check`.
- 2026-05-06 review passed the detonator reconciliation/native-wrapper fix with no blocking findings. Reviewer confirmed the wrapper filters to zero-argument `Disarm()`, does not call `Arm()` or `Evaluate()`, and the direct QA selector still rejects `detonator-unavailable:*` pseudo-targets before queueing heat/fuel. Keep in `04-verify`; do not move to `05-integration` until live QA has either a real placed `Detonator.*` save or a runtime diagnostic proving the internal detonator component is resolvable without a saved template string.
- 2026-05-06 worker reconciliation: native assembly inspection confirmed `Timberborn.AutomationBuildings.Detonator` is an internal runtime type and native `Disarm()` is non-public, while `Evaluate()` is public and `Arm()` is non-public. Fixed the native wrapper to invoke non-public zero-argument `Disarm()` only and added regression coverage proving it does not call `Arm()` or `Evaluate()`. Static scans of current `QA Tunnels and Booms` saves still show only `Dynamite.Folktails`, `DoubleDynamite.Folktails`, and `TripleDynamite.Folktails` templates, so live QA still needs either a save with a real `Detonator.*` object or a live runtime diagnostic proving a detonator component exists without a saved template string.
- 2026-05-06 live QA failed under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-153-detonator-fire-safety-20260506T193615Z`. QA loaded the exact save `/Users/jasonkleinberg/Documents/Timberborn/ExperimentalSaves/QA Tunnels and Booms/2026-05-06 15h09m, Day 2-2.autosave.timber`; `Player.log` confirmed it loaded at `2026-05-06 15:37:14Z`, and the command bridge stayed healthy.
- 2026-05-06 live QA failure detail: `qa-delta-stimulus detonator --wait=30 --require-advanced-tick` returned `success=false`, `queued_changes=0`, and `No_placed_Timberborn_target_was_found_for_QA_selector_'detonator'`. Follow-up `status` and `qa-readiness` passed, but all `last_delta_consumer_detonator_fire_safety_*` counters stayed `0`; wiring was present with `detonator_fire_safety_enabled=true` and `wildfire_timberborn_delta_consequence_sink_bound lane=detonator_fire_safety`. Move back to `03-in-progress`; fix the runtime target-resolution/design mismatch so the intended save can either resolve a real detonator target or expose a precise runtime diagnostic proving where Timberborn represents the control.
- 2026-05-06 worker fallback fix: after a real `Detonator` lookup is available but finds no `Detonator` target, the adapter now resolves same-cell `Dynamite` through `GetObjectsWithComponentAt<Dynamite>()`, stores it as `detonator-dynamite-control:*`, and sends it through the existing `Disarm()`-only wrapper. This preserves the conservative boundary: no `Trigger()`, `TriggerDelayed()`, `Arm()`, or `Evaluate()` calls were added, and `detonator-unavailable:*` pseudo-targets still fail direct QA selection without queueing.
- 2026-05-06 worker fallback verification passed: focused detonator fire-safety and QA command bridge tests with `99/99`, `dotnet build Wildfire.slnx --no-restore` with `0` warnings and `0` errors, and `git diff --check`. Added deterministic coverage for fallback stable ids, disarm-only behavior, QA direct selector acceptance, and the assertion API mismatch uncovered during integration checks. Move to `04-verify` for fresh review before rerunning live QA.
- 2026-05-06 fresh review passed the detonator fallback fix with no blocking findings. Reviewer confirmed same-cell dynamite fallback uses distinct `detonator-dynamite-control:*` stable ids and flows through the existing zero-argument `Disarm()`-only wrapper; no production `Trigger()`, `TriggerDelayed()`, `Arm()`, or `Evaluate()` calls were added. Direct QA still rejects `detonator-unavailable:*` before queueing heat/fuel. Verification passed: `git diff --check` and focused detonator plus QA command bridge tests with `99/99`. Keep in `04-verify` for live QA.
- 2026-05-06 live QA passed under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-153-detonator-fire-safety-20260506T200049Z` using `~/Documents/Timberborn/ExperimentalSaves/QA Tunnels and Booms/2026-05-06 16h00m, Day 2-4.autosave.timber`. QA deployed cleanly, loaded the exact `50x50x23` save, and ran `qa-delta-stimulus detonator --wait=30 --require-advanced-tick`.
- 2026-05-06 live QA proof: the direct selector resolved fallback target `direct_target_stable_id=detonator-dynamite-control:-1760284024` at `x=38 y=3 z=7 index=17688`, queued `1` change, and follow-up status/readiness reported `last_delta_count=19` and detonator lane counters `considered=15`, `matched=3`, `duplicate_suppressed=1`, `disabled=2`, `armed=0`, `skipped_no_safe_api=0`, `recoverability_preserved=2`, and `recoverability_unknown=0`. Copied `Player.log` forbidden-token scan found no `Trigger()`, `TriggerDelayed()`, `Arm()`, or `Evaluate()` method-token strings. Move to `05-integration`.
- 2026-05-06 integration complete. Integration review passed with `git diff --check` and focused detonator plus QA command bridge tests `99/99`. Accepted scope is conservative detonator fire safety only: same-cell `detonator-dynamite-control:*` fallback, `Disarm()`-only wrapper, `armed=0`, `disabled=2`, `recoverability_preserved=2`, and no forbidden runtime method-token evidence. This does not accept native dynamite triggering; if native dynamite triggering is later enabled, same-cell TWF-152/TWF-153 sequencing needs fresh review.
