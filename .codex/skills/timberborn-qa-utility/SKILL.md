---
name: timberborn-qa-utility
description: Build Wildfire Timberborn QA utilities with Bun, TypeScript, and guarded cliclick-style UI automation.
---

# Timberborn QA Utility

Use this skill when creating or changing a Wildfire QA utility that launches Timberborn, inspects Timberborn state, stages or runs a QA script, or automates Timberborn UI interactions from the desktop.

Do not use this skill for simulation-core rules, shader behavior, or deterministic CLI scenarios that do not touch Timberborn. Keep those host-agnostic and covered by normal deterministic tests.

## Required Inputs

Read these before designing the utility:

- `AGENTS.md`.
- `docs/INDEX.md`.
- `docs/TEST_PLAN.md`.
- `docs/timberborn-menu-coordinate-guide.md`.
- The assigned ticket and role instructions.

For UI automation, `docs/timberborn-menu-coordinate-guide.md` is the only approved source for Timberborn menu coordinates. Do not invent coordinates from memory, screenshots in chat, or approximate layout guesses. If the guide does not contain the target, stop and document the missing coordinate as a blocker or make coordinate capture the assigned task.

## Tooling Rules

- Write QA utilities in TypeScript and run them with `bun`.
- Use `bun` commands instead of `npm`, `node`, or package-lock workflows.
- Keep utility scripts local to the assigned ticket scope. If the ticket does not allow script changes, outline the utility in notes instead of adding files.
- Prefer deterministic CLI or log checks before live UI automation.
- Treat Timberborn as an adapter target. Do not move fire rules into Timberborn-facing code to make QA easier.

## UI Automation Rules

Use `cliclick`-style automation only when the expected Timberborn screen is known and the target coordinate is documented in `docs/timberborn-menu-coordinate-guide.md`.

Before any click or keypress, the utility must verify:

- The target app is running or can be launched by the assigned workflow.
- Timberborn can be activated by bundle id `com.mechanistry.timberborn`.
- The display resolution and UI scaling match the coordinate guide, or the utility is explicitly running in a documented capture-refresh mode.
- The expected screen is visible.
- The target coordinate exists in the guide and is safe for the assigned task.

The utility must fail loudly with a non-zero exit and a clear error when:

- Timberborn is not running and the utility is not allowed to launch it.
- The app activation command fails.
- Screenshot capture fails.
- The expected screen cannot be confirmed.
- A coordinate is missing from the guide.
- The coordinate guide's display assumptions do not match the current environment.
- The target is destructive, opens an external app, exits the game, loads a save, deletes data, or changes persistent state without explicit ticket approval.

Capture evidence around automation:

- Record the command that was run.
- Save or reference screenshots when the ticket asks for visual proof.
- Log the before-screen, click target, after-screen, and pass/fail result.
- Include searchable tokens in output when possible, such as `wildfire_qa_utility`, the utility name, and the target id from the coordinate guide.

## Screen Checks

Screen checks should be conservative. Prefer a combination of:

- Process and bundle-id checks.
- `osascript` activation success.
- Screenshot existence and dimensions.
- Known text, pixel, or image-match assertions from the documented screen.
- Player.log or Fire.log evidence when UI state has a corresponding log signal.

If a screen cannot be confirmed, stop before clicking. A missed click is better evidence than a blind click that changes a running save.

## Implementation Checklist

Before writing the utility:

- Confirm the ticket allows the files you plan to edit.
- Identify the exact Timberborn state the utility expects.
- Identify every coordinate target by id from `docs/timberborn-menu-coordinate-guide.md`.
- Mark destructive or state-changing targets as out of scope unless the ticket explicitly allows them.
- Decide whether a deterministic CLI or log assertion can cover part of the task before UI automation.

While writing the utility:

- Use TypeScript and `bun`.
- Parse arguments explicitly and print help for unsupported options.
- Validate the app, screen, display assumptions, and coordinate ids before acting.
- Print clear stderr errors and exit non-zero on every missing precondition.
- Keep reusable helpers small and ticket-scoped unless a shared helper is explicitly assigned.

When testing the utility:

- Run dry-run or help mode first with `bun`.
- Confirm failure behavior by testing at least one missing or mismatched precondition when practical.
- Run `git diff --check`.
- Run any additional ticket-required tests.

When documenting the utility:

- Update the assigned ticket with commands, output summary, evidence paths, and unresolved blockers.
- Link any new coordinate evidence back to `docs/timberborn-menu-coordinate-guide.md`.
- Update `docs/TEST_PLAN.md` or a more specific doc when the utility changes durable QA workflow.
- Recommend the next GitHub issue status-label change, but do not change labels unless explicitly assigned.
