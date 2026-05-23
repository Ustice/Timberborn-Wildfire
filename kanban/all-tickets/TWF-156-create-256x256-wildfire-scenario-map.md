---
ticket: TWF-156
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-149
write_scope:
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - docs/world-consequence-first-pass.md
  - kanban/all-tickets/TWF-156-create-256x256-wildfire-scenario-map.md
---

# TWF-156: Create 256x256 Wildfire Scenario Map

## Goal

Create a reusable `256x256` Timberborn map or save for Sprint 10 visual-consequence and aftermath QA.

## Why

The large-map question is not whether a single map-spanning `256x256` fire can run forever. The useful release question is whether a normal local forest fire inside a `256x256` world stays playable and does not make the map chug.

We should not block on generated-scenario technicalities if the Timberborn map editor and dev tools can create a clearer custom scenario. A hand-authored map is acceptable if it loads reliably, exercises the right fields, and preserves repeatable ignition/camera checkpoints.

## Requirements

- Create or select a `256x256` map or save.
- Include at least one local forest-fire test area that is large enough to show spread but not intended to burn the whole map.
- Include connected fuel, sparse fuel, water suppression, badwater or contaminated lane, clean ash lane, tainted ash lane, crop or harvestable lane, tree lane, dead bush/dead tree drought-ignition lane, structure pad, fire-using building pad, non-fire building control pad, storage pad, and camera path/checkpoints where practical.
- Keep firebreaks or spacing so one local test does not immediately invalidate every other test area.
- Use Timberborn map editor and dev tools where they are safer than save mutation.
- Preserve the exact map/save path, copied artifact or checksum, setup notes, ignition target, and camera checkpoint notes.
- Confirm the selected map reports `256x256` dimensions from archive metadata or live status.
- Confirm the map loads in Timberborn with Wildfire enabled and can reach command-responsive `status` or `qa-readiness`.
- Record the intended Sprint 10 consumers: scorch, burned textures, ash overlay, crop/tree/structure consequences, fertile ash collection, tainted ash washout, beaver exposure, rare ignition, aggregated feedback, and big-map performance smoke checks.

## Dependencies

- `TWF-149` reframes the real-field consequence gate as the Sprint 10 charter.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- This ticket can be completed with a hand-authored `.timber` map/save and notes; it does not require extending the TypeScript scenario generator.
- Prefer a stable map-editor/dev-tools workflow over direct JSON mutation unless the edit is small, structured, and easy to verify.
- The acceptance fire should be local: a small forest or cluster inside a `256x256` world. Do not require a whole-map burn.
- Include one clear release loop where fire produces clean ash, Gatherer Posts collect `FertileAsh`, the ash visually recedes, and status proves inventory mutation.
- Include one clear release loop where contaminated ash is visually distinct, fades or washes away, and any water-taint path is proven or safely reported unavailable.
- Include one rare-ignition proof lane with a forced high-test-rate setting, plus default/release-rate notes proving spontaneous ignition remains rare.
- If an existing `256x256` save already fits, document why it is suitable instead of creating a new map.
- `50x50` saves such as Diorama and Fuel are useful smoke tests but do not satisfy this ticket.

## Verification

- Run `git diff --check`.
- Inspect the `.timber` archive metadata or `world.json` to prove `256x256` dimensions.
- Live QA must load the map/save, unpause if needed, and capture `status` or `qa-readiness` showing command responsiveness.
- Preserve screenshots or recordings showing the local forest-fire test area and camera/checkpoint framing.
- Record any exact blocker if map editor/dev tools cannot create a required area safely.

## Notes

- This belongs in Sprint 10, not as the first required ticket and not as a Sprint 9 blocker.
- The first performance question is local-fire cost on a max-size map, not worst-case map-wide burn cost.
- 2026-05-23 coordinator: moved to `07-blocked` during GitHub issue migration because dependency `TWF-149` remains blocked. Migrated to GitHub as <https://github.com/Ustice/Timberborn-Wildfire/issues/34>.
