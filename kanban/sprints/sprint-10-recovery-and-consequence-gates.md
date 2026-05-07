# Sprint 10: Recovery And Consequence Gates

## Goal

Finish the current recovery slice by turning reviewed implementation into accepted evidence, and by returning failed live gates to the smallest worker fixes.

## Included Tickets

- `TWF-066`: current visible-fire presentation must pass a fresh review/QA gate after the 2026-05-05 procedural-particle direction changes.
- `TWF-072`: beaver field exposure telemetry is implemented on `main` and needs review, then live telemetry QA.
- `TWF-076`: crop burn consequences passed deterministic review and need live crop/harvestable evidence.
- `TWF-084`: tree burn consequences passed deterministic review and need live tree/cuttable evidence.
- `TWF-089`: burn-duration tuning failed live low/medium/high depletion proof and needs a worker fix plus fresh review and QA.
- `TWF-090`, `TWF-091`, and `TWF-092`: behavior tuning slices stay blocked until upstream `TWF-089` live acceptance is restored.
- `TWF-144` and `TWF-148`: real-field behavior and renderer gates stay blocked until normal gameplay visuals are plainly visible.
- `TWF-149` and `TWF-150`: downstream real-field consequence charter and scaffold cleanup stay blocked until replacement gates pass.
- `TWF-152`, `TWF-153`, and `TWF-154`: explosive infrastructure, detonator safety, and tunnel fire lanes are in verify and need review plus live QA before integration.

## Out Of Scope

- `TWF-052` through `TWF-063`: release packaging waits until gameplay, consequence, and visual evidence are stable.
- `TWF-067`, `TWF-068`, and `TWF-070`: smoke, ash, and steam tuning wait until `TWF-066` has an accepted fire presentation baseline.
- `TWF-073` through `TWF-087`: beaver behavior implementation waits until `TWF-072` telemetry is accepted.
- `TWF-156`: the `256x256` map/save proof is useful setup, but this recovery sprint should not start it before the active verify/blocker pile is closed.

## Dependency Order

1. Review current verify tickets and decide which can proceed to live QA.
2. Run live QA for already-reviewed consequence tickets while worker fixes repair known failed gates.
3. Return failed review or failed QA tickets to `03-in-progress` with the smallest concrete fix.
4. Move accepted tickets to `05-integration` only after required review and required QA both pass.
5. Integrate accepted work in dependency order and move unresolved work to `07-blocked` with exact next actions.

## Assignment Packets

- Use [../assignment-packet-template.md](../assignment-packet-template.md) for each sub-agent dispatch.
- Include the main-checkout canonical ticket path in every assignment.
- Tell sub-agents to report ticket notes back to the coordinator unless board edits are explicitly in scope.

## QA Gates

- Visible-fire gate: high-resolution normal-gameplay recording or screenshot evidence must show the fire effect itself plainly legible, with current procedural/field presentation status evidence and zero presentation failures.
- Crop/tree consequence gate: live loaded-save evidence must show nonzero target consideration and either safe mutation/effect counters or precise safe-unavailable telemetry.
- Burn-duration gate: `qa-burn-duration-stimulus low`, `medium`, and `high` must reach durable depleted proof, not `no_depletion_timeout`.
- Explosive/tunnel gate: live evidence must prove default-disabled native mutation paths, safe wrappers, telemetry, and recoverability expectations before any native destructive path is accepted.
- Any failed required QA gate must pass in a later run before the ticket can move to `05-integration/`.
- Any failed required review must return to `03-in-progress/`, move back through `04-verify/`, and pass a fresh review before the ticket can move to `05-integration/`.

## Live QA Risks

- Live Timberborn command responsiveness: QA owns deploy, launch, restart, command transcripts, and final lock state.
- Visual readability: status counters are supporting evidence only; visible normal-gameplay output is primary.
- Native explosive/tunnel APIs: default to disabled, deferred, or safe-unavailable telemetry unless live proof accepts a wrapper.
- Shared work scopes: keep board moves serialized in the main checkout, and avoid overlapping runtime writes except through explicit worker ownership.

## Evidence Manifest

- Use [../evidence-manifest-template.md](../evidence-manifest-template.md) for runtime artifacts, screenshots, logs, recordings, and generated scenario outputs.

## Close Criteria

- All included tickets are in `06-done`, `07-blocked`, `08-deferred`, or `09-awaiting-review` with concrete notes.
- No ticket with failed required QA is in `05-integration/` or `06-done` unless the failed gate later passed with evidence.
- No ticket with failed required review is in `05-integration/` or `06-done` unless a fresh review later passed from `04-verify/`.
- `bun run kanban:audit` has been reviewed.
- `git diff --check` passes.
- Required tests and QA evidence are linked from tickets.
- `docs/HANDOFF.md` is updated only if durable project status changed.
- The next sprint's first dependency-ready ticket is identified.

## Notes

- Created on 2026-05-06 during Sprint 10 resume after blockers were partially cleared.
- The live board remains the authority; this charter records the recovery shape observed at resume.
