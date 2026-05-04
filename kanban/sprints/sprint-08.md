# Sprint 8: Live QA Recovery

## Goal

Recover command-responsive live Timberborn QA and convert reviewed blocked work into accepted evidence or precise new blockers.

## Included Tickets

- `TWF-050`: first live gate because it proves the healthy GPU runtime and intentional missing-compute failure path.
- `TWF-097`: release enable/disable setting needs live disabled-state proof after `TWF-050` confirms the runtime is reachable.
- `TWF-064`: building-burnout pause investigation needs loaded-save command evidence before structure rollback can proceed.
- `TWF-119`: generated world-consequence scenario validation needs a command-responsive loaded Timberborn session and stable screenshots/logs.
- `TWF-066`: first visual blocked ticket after the shared live gate is healthy.
- `TWF-088`: first behavior-tuning blocked ticket after the shared live gate is healthy.

## Out Of Scope

- `TWF-089` through `TWF-092`: wait for `TWF-088` live acceptance before continuing the stacked behavior-tuning chain.
- `TWF-076` and `TWF-084`: keep crop/tree consequence live QA blocked until the common live gate is healthy and the QA scenario surface is clear.
- `TWF-115` and `TWF-127` through `TWF-130`: new implementation/research lanes wait until older reviewed blocked work has been triaged.
- `TWF-078` through `TWF-081`: ash, contamination, and persistence wait for crop/tree/structure consequence evidence.
- Release packaging tickets: wait until gameplay evidence and media are stable.

## Dependency Order

1. Prove normal-launch or attach-based Timberborn readiness without the Steam launch-args prompt.
2. Rerun `TWF-050` healthy runtime and missing-compute failure QA from a fresh evidence root.
3. Rerun `TWF-097`, `TWF-064`, and `TWF-119` live QA if the shared gate is healthy.
4. Rerun `TWF-066` visual recording and `TWF-088` spread recording only after the command bridge and loaded-save gate are stable.
5. Move passing tickets toward integration in dependency order; keep failed gates blocked with exact evidence and smallest next action.

## Assignment Packets

- Use [../assignment-packet-template.md](../assignment-packet-template.md) for each sub-agent dispatch.
- Include the main-checkout canonical ticket path in every assignment.
- Tell sub-agents to report ticket notes back to the coordinator unless board edits are explicitly in scope.
- QA owns all live Timberborn deploy, launch, attach, restart, screenshot, recording, and shared lock work.

## QA Gates

- Shared live gate: loaded, unpaused Timberborn save with `status` or `qa-readiness --require-advanced-tick` returning command output.
- `TWF-050`: healthy GPU runtime plus intentional missing-compute failure proof, both with copied `Player.log`.
- `TWF-097`: disabled state proof with `wildfire_enabled=false`, rejected simulator changes, skipped dispatch log token, and command/status visibility.
- `TWF-064`: building-burnout stimulus proves applied pause consequence or explicit safe no-op reason.
- `TWF-119`: generated scenario loads and matches manifest checkpoints.
- `TWF-066`: high-resolution recording plus screenshots and status/readiness evidence for active pooled fire effects.
- `TWF-088`: low-resolution spread recording plus command/status/log evidence.
- Any failed required QA gate must pass in a later run before the ticket can move to `05-integration/`.
- Any failed required review must return to `03-in-progress/`, move back through `04-verify/`, and pass a fresh review before the ticket can move to `05-integration/`.

## Live QA Risks

- Risk: macOS display sleep or screensaver can turn otherwise healthy Timberborn launches into black-screen or foreground/screenshot failures.
- Mitigation: run `caffeinate -disu` for the duration of Sprint 8 live QA, and record the active caffeinate state in QA evidence.
- Risk: launching Timberborn with command-line parameters triggers a Steam prompt.
- Mitigation: prefer attach to an already normally launched Timberborn session, or have QA launch normally before using attach/readiness commands.
- Risk: broad live QA tickets compete for the same deployed mod and shared lock.
- Mitigation: serialize QA runs and preserve separate evidence roots per ticket.
- Risk: a ticket's reviewed branch is stale against main-checkout accumulated changes.
- Mitigation: Tech-Lead reviews integration order before any merge or board move to `05-integration/`.

## Evidence Manifest

- Use [../evidence-manifest-template.md](../evidence-manifest-template.md) for runtime artifacts, screenshots, logs, generated clips, and command outputs.
- Preserve the exact launch or attach path in every QA report so parameterized-launch failures are not confused with product failures.

## Blocked Ticket Rehydration Plan

When `TWF-050` restores command-responsive loaded-save QA, move currently live-gate-blocked tickets out of `07-blocked` according to the state they were in before the shared gate failed.

- `TWF-064`: move to `02-ready` for worker/QA investigation of the building-burnout pause consequence.
- `TWF-097`: move to `04-verify` for disabled-state live QA; deterministic implementation and fresh review already passed.
- `TWF-119`: move to `02-ready` for QA-only generated-scenario validation.
- `TWF-066`: move to `04-verify` for high-resolution fire-effect recording QA; code review already passed.
- `TWF-088`: move to `04-verify` for low-resolution spread recording QA; deterministic implementation and Tech-Lead review already passed.
- `TWF-076`: move to `04-verify` for crop/harvestable live QA after the shared gate is healthy; review passed and required live evidence is still missing.
- `TWF-084`: move to `04-verify` for tree/cuttable live QA after the shared gate is healthy; fresh review passed and required live evidence is still missing.
- `TWF-077`: keep blocked until `TWF-064` resolves the building-burnout pause consequence, then reassess for ready or verify based on its latest worker/review state.
- `TWF-089`: keep blocked until `TWF-088` live QA passes, then move to `04-verify` for burn-duration live recording.
- `TWF-090`: keep blocked until `TWF-088` and `TWF-089` live gates pass, then move to `04-verify` for suppression live recording.
- `TWF-091`: keep blocked until `TWF-088` through `TWF-090` live gates pass, then move to `04-verify` for vertical or multi-cell structure live recording.
- `TWF-092`: keep blocked until `TWF-088` through `TWF-091` live gates pass, then move to `04-verify` for burnout/cooling live recording.

Do not move downstream tickets only because Timberborn launches. The proof gate is command responsiveness: `status` or `qa-readiness --require-advanced-tick` must consume `command-inbox.txt` and write `command-outbox.txt` from a loaded save.

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

- Jason clarified on 2026-05-03 that the Steam blocker was caused by launching Timberborn with parameters. Normal launch should avoid the prompt.
- This sprint intentionally interrupts the older ash/persistence numbering plan because reviewed blocked work should be converted into evidence before more overlapping implementation begins.
- 2026-05-03 kickoff result: `TWF-050` deploy passed and normal launch avoided the Steam args prompt, but the loaded session remained command-unresponsive. Evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-050-qa-20260503T125647Z` includes timed-out `qa-readiness`, a stalled attach helper, cleaned QA lock state, and restored compute bundle checksum `290c097c12ec885263eb472e20a4741d9cfbb9a24f65a396baed57390982ee79`.
- 2026-05-03 closeout: Sprint 8 is paused with `TWF-050` back in `07-blocked`. Downstream live QA remains blocked until Timberborn is closed/restarted normally and the healthy `qa-readiness --require-advanced-tick` gate passes.
