---
name: timberborn-qa-utility
description: Build and verify Wildfire QA utilities using Bun and TypeScript, with observed UI state and explicit shared-session ownership.
---

# Timberborn QA Utilities

Use when creating or changing tools that deploy, launch, inspect, or automate Timberborn. For simulator changes that do not touch the game, use deterministic tests and scenarios first.

Read `AGENTS.md` and the assignment. Consult the relevant sections of `docs/TEST_PLAN.md` and `docs/qa-tooling.md` for validation procedures and evidence classification. Use UI reference docs when the workflow touches those screens.

## Design

- Use TypeScript with Bun and existing helpers where they fit. Keep process, file, and UI effects at explicit boundaries so decisions can be tested without the game.
- Prefer command-bridge, log, and deterministic assertions when they provide the required observable. Use visual evidence for appearance and UI behavior.
- Keep argument parsing, failure reporting, and artifact capture clear. Improve shared helpers when duplication or repeated failure warrants it, within agreed write ownership.
- Make setup failures explicit and distinguish them from product failures. Preserve enough context to reproduce the problem.

## Live Operation

One controller owns the shared game session, launch/restart decisions, deployment, and input. Check the QA lock and current process state before acting; do not assume a transport timeout means the game stopped.

Before input, confirm the target app and current screen. Match resolution/scaling assumptions for scripted coordinates. Fresh screenshots or accessibility observations may establish a target; reference coordinates must be revalidated against the live screen. If a target is ambiguous, obtain a better observation before acting. For unattended repeatable workflows, encode screen/precondition assertions and stop with diagnostic evidence when they fail.

Use disposable/copied QA saves and follow existing authorization for loading or changing state. Obtain a user decision for an unapproved destructive action, upload, credential use, or publication. Keep launch and state changes within the assigned live session; other agents submit requests to its controller.

## Verification And Evidence

Run help/dry-run and relevant automated tests before live operation when supported. Exercise failure preconditions when they protect the shared session or player data. Run TypeScript checks for TypeScript changes, plus focused behavior tests; documentation edits need link/content review rather than a game run.

Record commands, fixture/build identity, observations, result, and artifact paths. Inspect the whole scene in visual captures. Log consequential automation runs with `scripts/qa-log-tool-run.ts` when maintaining durable tool reliability evidence; use `docs/qa-tooling.md` for classification and reports.

Update affected usage/reference documentation and report verification limits. A successful tool exit, startup log, or readiness counter proves only its stated gate, not gameplay or rendered appearance.
