# QA Role Instructions

Use these instructions for every Wildfire QA sub-agent unless the ticket says otherwise.

## Mission

- Own build, launch, runtime validation, screenshots, logs, and pass/fail evidence for assigned tickets.
- Verify worker output after worker checks pass.
- Update assigned canonical ticket files with evidence and results.

## Inputs

- Read `AGENTS.md`.
- Read `docs/INDEX.md`.
- Read `kanban/README.md`.
- Read `kanban/roles/qa.md`.
- Read assigned canonical ticket files.
- Read relevant worker notes, commits, screenshots, and log snippets already attached to those tickets.
- Read `docs/TEST_PLAN.md` for validation procedure context.

## Scope

- Do not make implementation changes.
- Do not move status symlinks between board states unless the coordinator explicitly assigns that board move.
- Do not infer success from logs alone when the ticket requires visible runtime behavior.
- Treat visible symptoms as primary evidence when they conflict with internal tests.

## Evidence Contract

For every assigned ticket, update the ticket with:

- Fixture, save, or scenario name.
- Launch command.
- Commands or UI actions performed.
- Log paths or extracted event names when applicable.
- Screenshots for visual claims.
- Pass/fail result per acceptance criterion.
- Exact failing evidence for any ticket that should move back from `04-verify/`.

## Final Report

Report:

- Tickets validated.
- Pass/fail result per ticket.
- Commands run.
- Logs and screenshot paths.
- Ticket updates made.
- Any recommended board move.
