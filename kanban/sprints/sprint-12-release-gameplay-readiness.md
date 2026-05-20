# Sprint 12: Release Gameplay Readiness

## Goal

Turn the release gameplay loop into an integrated, explainable, and QA-proven experience: fires can start from believable rare sources, burn consequences affect buildings, plants, beavers, ash, and contamination, players can understand what happened, and the aftermath persists through save/reload.

## Accepted Prerequisites

- `TWF-067`: smoke readability is accepted.
- `TWF-073`: the shared beaver behavior harness is accepted.
- `TWF-078` and `TWF-082`: clean ash and fertile ash collection/application are accepted.
- `TWF-157` through `TWF-160`: simulator-owned ash authority, queued ash mutations, ash presentation, and ash persistence/status/harvest sync are accepted.
- `TWF-164`: tainted ash decay is accepted; ash/water interaction remains split to `TWF-167`.

## Included Tickets

- `TWF-166`: validate tainted ash live behavior after clean ash and fertile ash are accepted.
- `TWF-079`: complete contamination-aware fire consequences, including tainted ash, toxic smoke classification, badwater/contaminated-water suppression semantics, and no-decontamination proof.
- `TWF-167`: define ash/water interaction, including washout and water-taint safe-unavailable telemetry if needed.
- `TWF-161`: rename ash field concepts after the simulator-owned behavior migration is proven.
- `TWF-162`: align clean steam transport with the field model.
- `TWF-070`: tune visible steam effects after steam transport authority is settled.
- `TWF-163`: create release-quality icons for consequence alerts, fertile ash, tainted ash, and fertilize crop/tree tools using the collected Timberborn asset sheets.
- `TWF-168`: add a structure-on-fire alert class with Timberborn-style icon references from the combined asset sheets.
- `TWF-085`: implement normal smoke beaver behavior through the shared harness.
- `TWF-086`: implement toxic smoke behavior after contamination classifications are available.
- `TWF-087`: implement fire and heat beaver behavior through the shared harness.
- `TWF-169`: fix fully burned trees not showing as stumps or burned-leftover visuals.
- `TWF-074`: validate beaver field behavior recordings after the behavior variants have evidence.
- `TWF-080`: aggregate player feedback for fire, building damage, plant/resource loss, beaver danger, fertile ash, and tainted ash.
- `TWF-081`: validate save/reload persistence for burn damage, ash, fertile ash, tainted ash washout, inventory, designations, and beaver state.
- `TWF-170`: fix ash fields not showing before the game unpauses.
- `TWF-165`: add rare deterministic drought-aware ignition from fire-using buildings and dry dead vegetation, while keeping ordinary non-fire buildings ineligible.
- `TWF-156`: create the `256x256` release QA scenario/map proving local fires, clean ash, tainted ash, beaver exposure, rare ignition, and big-map responsiveness.

## Out Of Scope

- `TWF-052` through `TWF-063`: release packaging, CI/CD, README, changelog, licensing, support templates, and Steam Workshop distribution wait until this gameplay-readiness sprint produces stable behavior and screenshots.
- `TWF-100` through `TWF-113`: workshop media, platform validation, package shape, and private workshop install testing wait until the gameplay loop and UI assets are stable.
- `TWF-083`: broad overgrowth and surface-fuel systems remain deferred. This sprint only promotes the narrow rare-ignition release slice in `TWF-165`.
- `TWF-120` through `TWF-125`: faction fire-response systems, smoke fans, and constructible berms remain future design unless Jason explicitly promotes them.
- Native beaver death, forced incapacitation, and native badwater contamination coupling remain out of scope unless the relevant beaver tickets prove safe APIs and recoverability during this sprint.
- Whole-map burn performance is out of scope. `TWF-156` should prove normal local-fire behavior inside a `256x256` world.

## Dependency Order

1. Treat the accepted prerequisites above as closed and do not redispatch them.
2. Validate the split tainted-ash surface through `TWF-166` before dependent contaminated-ash claims integrate.
3. Complete contamination and tainted aftermath: `TWF-079` before `TWF-086`; `TWF-167` before final persistence validation if ash/water state changes.
4. Clean up ash/steam naming and authority: `TWF-161`, `TWF-162`, then `TWF-070`.
5. Fix release-readiness bugs close to their owning lanes: `TWF-170` can run after the simulator ash presentation work; `TWF-169` can run after tree consequences.
6. Implement beaver behavior in ladder order: `TWF-085`, `TWF-087`, then `TWF-086` after `TWF-079`; validate all variants through `TWF-074`.
7. Add player explanation after consequence sources exist: `TWF-080` should consume final counters and icon assets rather than inventing temporary categories. `TWF-168` is the specific structure-on-fire alert class inside that feedback lane.
8. Tune and bind presentation: `TWF-163` after the fertile ash UI/good surfaces are stable and after `TWF-080`/`TWF-168` have settled alert classes enough for icon binding.
9. Add rare ignition after tuning context exists: `TWF-165` should use conservative defaults, deterministic tests, and a forced high-rate QA preset.
10. Build the release proof surface: `TWF-156` should provide a `256x256` scenario once the required lanes are stable enough to prove there.
11. Run final persistence validation through `TWF-081` only after ash, contamination, collection, beaver state, and rare ignition have their current implementation shape.

## Assignment Packets

- Use [../assignment-packet-template.md](../assignment-packet-template.md) for each sub-agent dispatch.
- Include the main-checkout canonical ticket path in every assignment.
- Tell workers they are not alone in the codebase and must preserve concurrent board notes and evidence.
- Keep ash authority, beaver behavior, UI icon binding, rare ignition, and QA scenario writes serialized unless the coordinator can prove write scopes are disjoint.
- For image generation assignments, include the reference sheets:
  - `docs/reference/assets/menu-icons/composite.png`
  - `docs/reference/assets/goods-icons/composite.png`
  - `docs/reference/assets/goods-icons/DirtIcon.png`
  - `docs/reference/assets/menu-icons/FieldsPlantingToolGroupIcon.png`
  - `docs/reference/assets/menu-icons/ForestryPlantingToolGroupIcon.png`
  - `docs/reference/assets/menu-icons/ContaminationBarrierIcon.png`
  - `docs/reference/assets/menu-icons/ContaminationSensorIcon.png`

## QA Gates

- Prerequisite gate: smoke readability, beaver harness, simulator-backed clean ash, simulator-owned ash authority, tainted ash decay, and clean fertile-ash collection/application are accepted; do not reopen them unless new regressions are filed as separate bugs.
- Ash authority gate: simulator-owned ash state must support creation, mutation, harvest, persistence, status, and presentation without a second authoritative ash store.
- Fertile ash collection gate: Gatherer Posts collect only uncontaminated ash, create `FertileAsh`, remove one ash unit per good, and visibly recede the local ash level.
- Tainted ash gate: contaminated sources produce tainted ash, tainted ash is visually distinct or status-distinct, tainted ash does not produce `FertileAsh`, and tainted ash does not grant the clean growth bonus. Decay is accepted in `TWF-164`; water interaction remains `TWF-167`.
- Beaver consequence gate: smoke, toxic smoke, and fire/heat behavior variants must show reversible lower-risk behavior or precise safe-unavailable API evidence before severe states are accepted.
- Rare ignition gate: forced high-rate QA proves the path, default release settings remain rare, only eligible fire-using buildings and dry dead vegetation ignite, and ordinary non-fire buildings do not.
- Feedback gate: alerts/status aggregate consequences without per-cell spam and use the release icon set where the native UI surface permits.
- Structure alert gate: burning structures produce a bounded, distinguishable alert or precise native-surface blocker evidence, with no per-cell alert spam.
- Tree aftermath gate: trees that burn all fuel show a stump or accepted burned-leftover visual state, or report precise safe-unavailable telemetry without claiming success.
- Paused ash gate: existing ash is visible from loaded-save readiness before unpause and remains stable after unpause without duplicate ash state.
- `256x256` gate: a local fire inside a `256x256` world remains command-responsive, visually inspectable, and does not require map-wide burn acceptance.
- Persistence gate: save/reload preserves or safely degrades burn damage, ash state, ash depletion, tainted ash washout, `FertileAsh` inventory, fertilize designations, and any beaver behavior state.
- Any failed required QA gate must pass in a later run before the ticket can move to `05-integration/`.
- Any failed required review must return to `03-in-progress/`, move back through `04-verify/`, and pass a fresh review before the ticket can move to `05-integration/`.

## Live QA Risks

- Risk: this sprint is large enough to hide failures behind apparent progress.
- Mitigation: coordinate as waves. Do not start rare ignition, final feedback, or persistence validation until their source lanes are implemented or explicitly blocked.
- Risk: ash/water behavior could accidentally imply decontamination.
- Mitigation: keep water-contact behavior isolated in `TWF-167` and preserve the no-decontamination rule from `TWF-079`.
- Risk: generated or edited icons may look off-style even if technically bound.
- Mitigation: compare against the full collected asset sheets and inspect screenshots in their real UI context, not isolated PNGs.
- Risk: rare ignition can feel unfair if it appears causeless.
- Mitigation: restrict eligibility to fire-using buildings and visibly dry/dead vegetation, keep default probability rare, and route explanations through `TWF-080`.
- Risk: `256x256` QA can become a performance stress rabbit hole.
- Mitigation: accept a normal local-fire proof in a max-size world; keep whole-map burn stress out of scope.
- Risk: Timberborn command responsiveness may regress during long live QA.
- Mitigation: start `caffeinate -disu` for live runs and verify `qa-readiness --require-advanced-tick` before collecting evidence.

## Evidence Manifest

- Use [../evidence-manifest-template.md](../evidence-manifest-template.md) for runtime artifacts, screenshots, logs, recordings, command transcripts, generated icons, generated scenario outputs, and save/reload proof.
- Every live QA report should preserve the exact save/map path, deploy source, command transcript, copied `Player.log`, screenshots or recordings, and final lock-state cleanup.
- Icon evidence should include the source prompts or editing notes, reference images used, final PNG paths, binding paths, and in-game screenshots.

## Close Criteria

- All included tickets are in `06-done`, `07-blocked`, `08-deferred`, or `09-awaiting-review` with concrete notes.
- No ticket with failed required QA is in `05-integration/` or `06-done` unless the failed gate later passed with evidence.
- No ticket with failed required review is in `05-integration/` or `06-done` unless a fresh review later passed from `04-verify/`.
- `bun run kanban:audit` has been reviewed.
- `git diff --check` passes.
- Required tests and QA evidence are linked from tickets.
- `docs/HANDOFF.md` is updated only if durable project status changed.
- The first release-packaging ticket is identified as the next dependency-ready follow-up.

## Notes

- Created on 2026-05-19 after Jason promoted release readiness around beaver consequences, contaminated ash, ash visuals, fertile ash collection, rare ignition, drought-dry vegetation, and `256x256` QA.
- The main missing item called out during chartering is suppression/player response. It remains out of scope for this sprint unless Jason promotes a faction response or manual suppression slice; current release scope should still make fire consequences understandable and recoverable without shipping a full firefighting system.
- Packaging should not start until this sprint has stable player-facing behavior, screenshots, and clear release gates.
