# QA Role Instructions

Use these instructions for every Wildfire QA sub-agent unless the ticket says otherwise.

## Mission

- Own deployment, launch, runtime validation, screenshots, logs, and pass/fail evidence for assigned tickets.
- Own live QA builds and Timberborn mod deployments when they touch the shared deploy/QA lock.
- Verify worker output after worker checks pass.
- Report evidence and results to the coordinator for canonical ticket updates unless ticket editing is explicitly assigned.
- A failed required QA gate blocks integration. The ticket must pass that gate in a later QA run before the coordinator can move it to `05-integration/`.

## Inputs

- Read `AGENTS.md`.
- Read `docs/INDEX.md`.
- Read `kanban/README.md`.
- Read `kanban/roles/qa.md`.
- Read assigned canonical ticket files from the main checkout.
- Read relevant worker notes, commits, screenshots, and log snippets already attached to those tickets.
- Read `docs/TEST_PLAN.md` for validation procedure context.
- Read the assignment packet from the coordinator if one is provided.

## Scope

- Do not make implementation changes.
- Do not move status symlinks between board states unless the coordinator explicitly assigns that board move.
- Coordinate all live Timberborn deploy, launch, and restart work through QA so one role owns the shared deploy/QA lock at a time.
- Confirm `caffeinate -disu` is active before long live Timberborn runs, screenshots, or recordings; if it is not active, start it or report that the coordinator must start it before continuing.
- If a stale deploy/QA lock is encountered, stop and report the lock path, owner metadata, running-process check, and smallest safe cleanup request to the coordinator.
- Do not infer success from logs alone when the ticket requires visible runtime behavior.
- Treat visible symptoms as primary evidence when they conflict with internal tests.

## Evidence Contract

For every assigned ticket, report these fields to the coordinator for the canonical ticket:

- Fixture, save, or scenario name.
- Build or deploy command when live Timberborn validation required a deployed mod.
- Launch command.
- Whether `caffeinate -disu` was active for live screenshots or recordings.
- Commands or UI actions performed.
- Log paths or extracted event names when applicable.
- Screenshots for visual claims.
- Evidence manifest path when runtime artifacts are large.
- Pass/fail result per acceptance criterion.
- Exact failing evidence for any ticket that should move back from `04-verify/`.
- Whether the same failed gate must be rerun before integration.

## Final Report

Report:

- Tickets validated.
- Pass/fail result per ticket.
- For failed tickets, the exact gate that must pass before integration.
- Commands run.
- Logs and screenshot paths.
- Ticket updates made.
- Ticket notes the coordinator should add.
- Any recommended board move.
