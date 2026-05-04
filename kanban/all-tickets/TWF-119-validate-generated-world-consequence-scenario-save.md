---
ticket: TWF-119
agent_level: Medium
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-118
  - TWF-132
write_scope:
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-119-validate-generated-world-consequence-scenario-save.md
---

# TWF-119: Validate Generated World Consequence Scenario Save

## Goal

Prove the generated world-consequence scenario loads in Timberborn and contains the expected test layout.

## Why

A generator is only useful if Timberborn accepts the output and the map actually contains the water, badwater, trees, crops, structures, storage, and camera-readable test lanes that downstream QA needs.

## Requirements

- Run the generator from `TWF-118` against a copied template.
- Load the generated save/map in Timberborn.
- Confirm the 50 by 50 layout or record the exact accepted fallback dimensions.
- Confirm water and badwater channels flow north to south or record the precise blocker.
- Confirm tree, crop, structure, storage, and contamination test areas exist where the manifest says they should.
- Capture screenshots or logs that future agents can use as evidence.
- Update `docs/TEST_PLAN.md` with the accepted command and evidence path.

## Dependencies

- `TWF-118` creates the generator.
- `TWF-132` fixes generated placement validity after Timberborn loading issues deleted the generated checkpoints.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Implementation Notes

- Start from the generator command, output folder, and manifest produced by `TWF-118`; do not hand-edit the generated save before validation.
- Load the generated save or map through the normal Timberborn path and preserve screenshots, copied `Player.log`, generated manifest, command transcript, and any load dialogs.
- Validate against the manifest rather than memory: dimensions, north-to-south water and badwater channels, land bands, tree/crop/structure/storage pads, contamination areas, and any declared fallback dimensions.
- If Timberborn rejects the generated output, block with the exact archive member or schema field from `TWF-118`, the load error text, and whether the generator or the template assumption owns the fix.
- Passing evidence should leave downstream world-consequence tickets with a stable artifact path and named visual checkpoints they can reuse.

## Verification

- Run `git diff --check`.
- Capture live Timberborn evidence for the generated scenario load.
- Attach or reference the generated manifest, screenshots, and logs in this ticket's notes.

## Notes

- Parent design reference: [world-consequence-first-pass.md](../../docs/world-consequence-first-pass.md), "Scenario Save Generator".
- Blocked on the live launch environment as of the `TWF-050` QA retry: Timberborn could not be launched or activated through Steam, with `frontmost_bundle_id=com.valvesoftware.steam` and Steam waiting on a launch-args prompt. This ticket is QA-only and cannot validate the generated save until Timberborn can load into a command-responsive game again.
- Unblock by clearing the Steam/Timberborn launch prompt and proving `bun scripts/load-latest-save-and-unpause.ts --launch` can reach a loaded save, then rerun this ticket from the `TWF-118` generated scenario manifest.
- 2026-05-03 coordinator update: the shared blocker has moved past the Steam launch-args prompt. Normal-launched Timberborn can start, but the live command bridge still fails the `TWF-050` gate by leaving `command-inbox.txt` without producing `command-outbox.txt`; once `TWF-050` restores command-responsive loaded-save QA, this QA-only ticket should be ready to return from blocked for generated-scenario validation.
- 2026-05-03 live QA failed after `TWF-050` was restored. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-119-qa-20260503T140750Z`. The generated save appeared in Timberborn's Load Game UI, but loading it reached the crash screen. `Player.log` reports `Failed to read save entry save_metadata.json: System.ArgumentOutOfRangeException`, followed by the already-known `WildfireReleaseSettings` constructor failure from unintegrated `TWF-097`. Archive inspection matched all 26 generated manifest entities, but live validation could not confirm the 50 by 50 or fallback dimensions, water/badwater flow, crop pads, storage contents, or contamination checkpoints. Coordinator moved this ticket back to `07-blocked`; smallest unblock is a generator fix for the `save_metadata.json` serialization shape, then rerun with the `TWF-097` constructor fix included.
- 2026-05-03 unblock: `TWF-097` and `TWF-131` are now in `06-done`, so the deployed build should include the Bindito constructor fix and the generated scenario artifact should include the Timberborn-accepted `save_metadata.json.Timestamp` shape. Coordinator moved this ticket back to `02-ready` for live validation rerun using the `TWF-131` generated artifact.
- 2026-05-03 rerun failed before save selection. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-119-qa-20260503T144034Z`. The fixed `TWF-131` artifact installed and matched the QA save slot checksum; static archive/entity inspection matched all 26 generated entities; deploy of current main passed. Both normal launch routes created a Timberborn process, but the screen stayed black and `Player.log` stopped after `Successfully connected to the Steam client`, so Timberborn never reached startup dialogs, main menu, or Load Game UI. Live validation of dimensions, water/badwater flow, crop pads, storage, and contamination remains unproven. Coordinator moved this ticket back to `07-blocked`; next rerun should start from the same `TWF-131` artifact unless a newer fixed artifact exists, but first prove normal-launched Timberborn reaches startup dialogs/main menu or a command-responsive loaded state.
- 2026-05-03 coordinator update: Jason reported the screensaver started during the sprint, which likely explains the black-screen launch evidence. Coordinator started `caffeinate -disu`, documented caffeinate as required for live Timberborn sprint runs, and moved this ticket back to `02-ready` for a rerun under caffeinate. Use the same `TWF-131` artifact unless a newer fixed artifact exists.
- 2026-05-03 coordinator: moved to `03-in-progress` for a QA rerun under active `caffeinate -disu`. QA should report the caffeinate PID/state in the evidence notes, then validate the fixed `TWF-131` generated artifact against the manifest.
- 2026-05-03 QA rerun under active `caffeinate -disu` failed the scenario-content gate. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-119-qa-20260503T152225Z`. `00-initial-state.txt` shows `caffeinate -disu` active as PID `94422`; the fixed TWF-131 artifact checksum matched the installed exact save at `~/Documents/Timberborn/ExperimentalSaves/Wildfire generated QA/Wildfire world consequence scenario TWF-119.timber`; static inspection of that exact installed save matched all 26 manifest generated entities. The first `Continue` startup pass loaded latest autosave `2026-05-03 11h10m, Day 1-2.autosave.timber`, which did not contain the manifest coordinates and should be treated only as environment/startup proof. QA then used Load Game UI to select the exact `Wildfire world consequence scenario TWF-119` save. `Player.log` confirms `Opening file: .../Wildfire world consequence scenario TWF-119.timber`; Timberborn reached a Loading issues dialog, preserved at `07-load-exact-generated-save/09-after-exact-save-load-wait.png`, reporting invalid locations and deletion of `Badwater Source (4)`, `Water Source (4)`, `Birch (4)`, `Oak (4)`, `Pine (4)`, `Path (3)`, `Small Tank`, `Large Pile`, and `Medium Warehouse`. After Continue playing and unpause, `13-qa-readiness-exact-save-after-unpause.txt` passed with `loaded_game_ready=true`, `simulator_integrated=true`, `width=128`, `height=128`, `depth=23`, and `tick_count=4`; `14-status-exact-save-after-unpause.txt` also passed. Live dimensions are therefore the fallback template dimensions `128x128x23`, not a 50x50 map. Water/badwater flow, tree/crop/structure/storage/contamination checkpoints fail: generated water/badwater sources and tree/structure/storage/path objects were deleted as invalid locations; crop pads and storage contents were already manifest schema blockers. Smallest likely owner is the generator/template-placement lane from `TWF-118`/follow-up generator fix: generated entity coordinates or required terrain/support locations are not Timberborn-valid even though the archive loads.
- 2026-05-03 coordinator: moved back to `07-blocked` on new unblocker `TWF-132`. The launch and command-responsive gates are healthy under caffeinate; do not diagnose this as a Steam, screensaver, or command bridge issue unless a later rerun regresses those gates.
- 2026-05-03 unblock: `TWF-132` passed review with a narrower valid-placement strategy. Rerun this ticket against artifact `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-132-template-supported-checkpoints-20260503T154213Z/wildfire-world-consequence-scenario.timber`. Acceptance should be scoped to Timberborn load survival and manifest-declared checkpoint presence at existing template-supported coordinates; water/badwater flow layout, crop pads, and storage inventory remain explicit blockers unless a later artifact fixes them.
- 2026-05-03 worker handoff from `TWF-132`: new artifact `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-132-template-supported-checkpoints-20260503T154213Z` no longer injects invalid planned clones. Instead, its manifest relocates survivor-expected checkpoints onto existing template-supported BlockObject coordinates. Manifest summary: `generatedEntities=24`, `blockedPlacements=6`, no metadata blockers, generated `world.json` entity count `2246` matching the template. Generated checkpoints cover two badwater sources, four water sources, 12 trees, one warehouse, one pile, one tank, and three path tiles. TWF-119 should rerun live QA against this artifact to confirm Timberborn loads without deleting those existing-coordinate checkpoints; crop pads and two additional badwater-source planned slots remain explicit manifest blockers.
- 2026-05-03 narrowed QA rerun passed the TWF-132 load-survival gate. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-119-qa-20260503T154926Z`. `00-initial-state.txt` and `16-final-process-lock-state.txt` show active `caffeinate -disu` PID `94422`; Timberborn PID `21888` was preserved and no build/deploy lock was created. The reviewed artifact checksum matched the installed exact save at `~/Documents/Timberborn/ExperimentalSaves/Wildfire generated QA/Wildfire world consequence scenario TWF-119.timber` in `02-installed-exact-save-checksums.txt` and `13-final-installed-save-checksums.txt`. `02-installed-exact-save-manifest-match.json` matched all 24 manifest-generated survivor checkpoints with `missing=[]`; manifest blockers remained six planned-but-unsupported placements. `05-load-dialog-opened.png` proves the Load Game selection for `Wildfire world consequence scenario TWF-119`, and `12-Player-after-unpause.log` confirms `Opening file: .../Wildfire world consequence scenario TWF-119.timber`. Timberborn loaded without a Loading issues dialog; `15-no-loading-issues-deletion-scan.txt` is empty for the exact-load segment, so there is no evidence of Timberborn deleting manifest-declared checkpoint objects. After unpause, `10-qa-readiness-after-unpause.txt` passed with `loaded_game_ready=true`, `simulator_integrated=true`, `width=128`, `height=128`, `depth=23`, and `tick_count=5`; `11-status-after-unpause.txt` also passed. This does not prove the original full TWF-119 scope: the 50 by 50 layout, crop pads, full badwater source count, water/badwater flow layout, and storage inventory remain explicitly blocked by the manifest and should not be treated as accepted by this rerun.
- 2026-05-03 coordinator closeout: the original full generated-scenario ambition is split to `TWF-133`. This ticket is accepted for the narrower load-survival and manifest checkpoint-presence proof created by `TWF-132`; do not use it as evidence that full channels, crop pads, storage inventory, or a 50 by 50 generated layout exist.
- 2026-05-04 follow-up pointer: `TWF-133` generated fuller static coverage at `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-133-full-layout-20260504T174048`, covering crops/harvestables, storage goods, and Folktails paths. It is not a replacement acceptance artifact for this closed `TWF-119` gate because one planned badwater source and live flow/load proof remain unresolved there.
