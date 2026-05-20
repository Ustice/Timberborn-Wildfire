# Sprint 11: Visual, Beaver, And Ash Verification

## Goal

Turn the current ready and verify work into accepted evidence: smoke readability, the beaver behavior harness, simulator-backed ash live QA, and fertile-ash collection/application live QA.

## Included Tickets

- `TWF-067`: tune visible smoke effect now that the procedural Fire baseline is accepted.
- `TWF-073`: add the beaver field behavior harness from accepted beaver exposure telemetry.
- `TWF-078`: verify the off-sprint simulator-backed ash read model with live Timberborn evidence.
- `TWF-082`: verify the off-sprint fertile ash collection/application implementation with live Timberborn evidence after `TWF-078`.

## Out Of Scope

- `TWF-068` and `TWF-070`: ash and steam visual tuning wait until the smoke baseline is accepted.
- `TWF-079` through `TWF-081`: contamination, aggregate feedback, and persistence validation should start after `TWF-078` and `TWF-082` live evidence is understood.
- `TWF-085` through `TWF-087`: smoke, toxic smoke, and fire/heat behavior variants wait until `TWF-073` accepts the shared behavior harness.
- `TWF-052` through `TWF-063`: release packaging waits until gameplay evidence, screenshots, and release-facing behavior are stable.
- `TWF-083` and `TWF-120` through `TWF-125`: overgrowth and faction fire-response systems remain deferred.

## Dependency Order

1. Start from board reconciliation: `TWF-067` and `TWF-073` are in `02-ready`; `TWF-078` and `TWF-082` are in `04-verify`.
2. Send `TWF-078` to QA first because it is the foundation for ash quality, growth, tainted ash, persistence, and collection status.
3. Send `TWF-082` to QA after or alongside `TWF-078` only if QA can reuse the same loaded save without mixing evidence roots.
4. Dispatch `TWF-067` as the first implementation lane for visual polish.
5. Dispatch `TWF-073` as a separate implementation lane only if its `src/Wildfire.Timberborn/**` and `tests/Wildfire.Core.Tests/**` writes can be kept separate from `TWF-067`; otherwise serialize it after the smoke patch reaches verify.
6. Use the results to unblock the next wave: `TWF-068`/`TWF-070` after smoke, `TWF-085` through `TWF-087` after beaver harness, and `TWF-079`/`TWF-081` after ash verification.

## Assignment Packets

- Use [../assignment-packet-template.md](../assignment-packet-template.md) for each sub-agent dispatch.
- Include the main-checkout canonical ticket path in every assignment.
- Tell sub-agents to report ticket notes back to the coordinator unless board edits are explicitly in scope.
- Give QA explicit evidence roots per ticket when `TWF-078` and `TWF-082` share a live Timberborn session.

## QA Gates

- Smoke readability gate: high-resolution recording and screenshot evidence must show smoke as distinct from active flame in normal gameplay, with status/log proof and no presentation failures.
- Persistent ash gate: live evidence must show clean ash creation, growth telemetry or safe-unavailable growth telemetry, save/reload survival, and `status` or `qa-readiness` counters. Tainted ash is split to `TWF-166`.
- Fertile ash gate: live evidence must show Gatherer Post collection, `FertileAsh` inventory mutation, designation application, save/reload of designations, and toolbar usability or exact API blockers. Tainted-cell blocking is split to `TWF-166`.
- Beaver harness gate: live evidence must show the harness observes accepted exposure telemetry and applies or skips bounded no-op behavior without critical exceptions.
- Any failed required QA gate must pass in a later run before the ticket can move to `05-integration/`.
- Any failed required review must return to `03-in-progress/`, move back through `04-verify/`, and pass a fresh review before the ticket can move to `05-integration/`.

## Live QA Risks

- Risk: Ash verification and fertile-ash verification may contaminate each other's evidence if they use the same save state without clear before/after commands.
- Mitigation: QA should preserve separate evidence roots, copied `Player.log` windows, and command transcripts for `TWF-078` and `TWF-082`.
- Risk: Smoke tuning and beaver harness overlap broad Timberborn runtime files.
- Mitigation: serialize if file ownership cannot be narrowed before dispatch.
- Risk: Timberborn command responsiveness may regress.
- Mitigation: verify `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick` from a loaded, unpaused save before live QA.

## Evidence Manifest

- Use [../evidence-manifest-template.md](../evidence-manifest-template.md) for runtime artifacts, screenshots, logs, recordings, command transcripts, and save/reload proof.
- Every QA report should include the exact deploy/launch/attach path and final lock-state cleanup.

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

- Created after commit `49f339c2`, which reconciled the board with off-sprint ash and fertile-ash implementation work.
- This sprint intentionally mixes two implementation lanes with two QA verification lanes because Jason asked to include all four next-lane choices. Keep write scopes serialized where needed.
- Closed on 2026-05-19 as clean-scope done after Jason confirmed `TWF-078` and `TWF-082`. `TWF-067` passed review, live smoke readability QA, integration, `git diff --check`, targeted renderer tests, and full `dotnet test`; it moved to `06-done`. `TWF-073` passed review, live beaver-harness QA, integration, focused tests, and full `dotnet test`; it moved to `06-done`. `TWF-078` and `TWF-082` moved to `06-done` on Jason's manual confirmation of clean ash persistence and fertile-ash collection/application.
- Tainted ash was not tested during Sprint 11 and is split to `TWF-166`: prove contaminated-source tainted ash, no `FertileAsh` collection from tainted ash, no clean growth bonus, and distinguishable status or presentation evidence.
