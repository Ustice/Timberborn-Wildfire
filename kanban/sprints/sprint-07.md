# Sprint 7: World Consequence Core

## Goal

Make fire damage the world in durable, resource-accounted ways without moving simulation rules into Timberborn.

## Included Tickets

- `TWF-075`: add the burn damage state foundation that downstream crop, tree, structure, storage, and infrastructure tickets consume.
- `TWF-076`: apply crop burn consequences after the foundation is accepted.
- `TWF-084`: apply tree burn consequences after the foundation is accepted.
- `TWF-077`: apply structure burn damage rollback after the foundation and `TWF-064` are accepted.
- `TWF-116`: define conservative explosive storage fire behavior from the resource catalog.
- `TWF-115`: apply stored goods burn consequences after the foundation and explosive behavior are accepted.
- `TWF-117`: classify infrastructure burn behavior after the foundation and resource catalog are accepted.

## Out Of Scope

- `TWF-078` through `TWF-081`: ash, contamination, aggregation, and persistence belong to Sprint 8 after core consequences exist.
- `TWF-071` through `TWF-074` and `TWF-085` through `TWF-087`: beaver danger belongs to Sprint 9.
- `TWF-052` through `TWF-063` and `TWF-100` through `TWF-113`: release packaging waits for stable gameplay evidence.
- `TWF-082`, `TWF-083`, and `TWF-120` through `TWF-125`: deferred future gameplay.

## Dependency Order

1. Start `TWF-075` first because it defines burn damage identity, capacity, duplicate suppression, and ownership.
2. Run `TWF-116` as a research/design lane when it will not overlap implementation writes from `TWF-075`.
3. After `TWF-075` passes review, start crop and tree consequence slices before broader storage or infrastructure work.
4. Keep `TWF-077` blocked until `TWF-064` live investigation is unblocked or the ticket scope is explicitly narrowed.
5. Start `TWF-115` only after `TWF-116` has accepted hazardous-good behavior.
6. Close Sprint 7 only when included tickets are done, blocked, deferred, or awaiting review with exact next actions.

## Assignment Packets

- Use [../assignment-packet-template.md](../assignment-packet-template.md) for each sub-agent dispatch.
- Include the main-checkout canonical ticket path in every assignment.
- Tell sub-agents to report ticket notes back to the coordinator unless board edits are explicitly in scope.
- Avoid overlapping writes to `src/Wildfire.Timberborn/**`, `tests/Wildfire.Core.Tests/**`, and shared docs unless the coordinator narrows the assignment to disjoint files.

## QA Gates

- Deterministic gate: `git diff --check`, `dotnet test`, and `dotnet build Wildfire.slnx` for behavior or adapter code.
- Runtime gate: live Timberborn evidence only when the ticket requires it, including command/status output, copied `Player.log`, artifact paths, and final QA lock state.
- Any failed required QA gate must pass in a later run before the ticket can move to `05-integration/`.
- Any failed required review must return to `03-in-progress/`, move back through `04-verify/`, and pass a fresh review before `05-integration/`.

## Live QA Risks

- Risk: Steam or Timberborn launch prompt blocks a command-responsive loaded save.
- Mitigation: keep live-dependent consequence tickets out of `05-integration/` until `bun scripts/load-latest-save-and-unpause.ts --launch` reaches a loaded save and `qa-readiness` can respond.
- Risk: burn-damage foundation grows into crop, tree, storage, structure, or ash behavior.
- Mitigation: keep `TWF-075` to shared identity, capacity, mapping, state, and telemetry foundations only.

## Evidence Manifest

- Use [../evidence-manifest-template.md](../evidence-manifest-template.md) for runtime artifacts, screenshots, logs, generated clips, and command outputs.

## Close Criteria

- All included tickets are in `06-done`, `07-blocked`, `08-deferred`, or `09-awaiting-review` with concrete notes.
- No ticket with failed required QA is in `05-integration/` or `06-done` unless the failed gate later passed with evidence.
- No ticket with failed required review is in `05-integration/` or `06-done` unless a fresh review later passed from `04-verify/`.
- `bun run kanban:audit` has been reviewed.
- `git diff --check` passes.
- Required tests and QA evidence are linked from tickets.
- `docs/HANDOFF.md` is updated only if durable project status changed.
- Sprint 8's first dependency-ready ticket is identified.

## Notes

- Sprint 7 starts while Sprint 6 live QA remains blocked. Do not use deterministic Sprint 6 acceptance as a substitute for live recordings where tickets require them.
- `TWF-075` is done and integrated after two failed-review loops. Main-checkout verification passed `git diff --check`, targeted `TimberbornBurnDamageStateTests`, full `dotnet test`, and `dotnet build Wildfire.slnx`.
- `TWF-116` is accepted as the explosive stored-good contract after a research-only pass. Implementation should wait for `TWF-115` storage accounting and a safe inventory API; if no safe inventory API is found, split that path instead of faking item destruction.
- `TWF-076` has a reviewed deterministic crop consequence implementation on `codex/TWF-076-crop-burn-consequences` at commit `1b5083e719e1fd7d4220cda47fce0ea745d2a2b9`, but it is blocked on required live Timberborn crop-burn evidence.
- `TWF-084` has a reviewed deterministic tree consequence implementation on `codex/TWF-084-tree-burn-consequences` at commit `5788353736103ee249c24103b195f9909fe259d1`, but it is blocked on required live Timberborn tree-burn evidence.
- `TWF-117` is accepted as the infrastructure classification contract after a research-only pass. Implementation should start with descriptors, capacity, counters, and safe no-op behavior before any path, power, or water-obstacle mutation.
- `TWF-077` is blocked on `TWF-064` live building-burnout investigation.
- `TWF-115` is unblocked from the `TWF-116` decision but must still find a safe storage/inventory accounting surface before claiming stored-goods destruction.
- `TWF-127` through `TWF-130` capture the accepted follow-up lanes for path, power, water, and tunnel/dynamite infrastructure behavior.
- Sprint 7 is closed as mixed done/blocked, not fully integrated. Live-required consequence tickets must stay out of `05-integration` until the Steam/Timberborn launch/load issue is cleared and their specific QA gates pass.
