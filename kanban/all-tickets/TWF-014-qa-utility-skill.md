---
ticket: TWF-014
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-013
write_scope:
   - .codex/skills/**
   - docs/**
   - kanban/all-tickets/TWF-014-qa-utility-skill.md
---

# TWF-014: Create QA Utility Skill

## Goal

Create a project skill for building Timberborn QA utilities, including scripts that can drive the UI with tools such as `cliclick`.

## Why

QA utility scripts need consistent guardrails: use Bun and TypeScript, avoid unsafe clicks, document resolution assumptions, and prefer reusable helpers. A skill captures those rules so future utility work starts correctly instead of being re-explained each time.

## Requirements

- Add a local Codex skill under `.codex/skills/`.
- Explain when to use the skill.
- Require Bun and TypeScript for scripts.
- Include guidance for `cliclick`-style UI automation.
- Require coordinate references to come from the Timberborn menu coordinate guide.
- Require scripts to fail loudly when the target app or expected screen is missing.
- Include a checklist for creating, testing, and documenting a QA utility.
- Link the skill from any relevant docs if needed.

## Dependencies

- `TWF-013` Timberborn menu coordinate guide.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Review the skill by using it to outline a simple QA utility task.
- If the ticket adds TypeScript examples or helper scripts, run them with `bun`.

## Notes

- This ticket creates the reusable instructions. The first concrete utility is `TWF-015`.

## Worker Notes

- Created local skill `.codex/skills/timberborn-qa-utility/SKILL.md`.
- Linked the skill from `docs/INDEX.md` and `docs/TEST_PLAN.md`.
- Kept this pass to Markdown/docs only. No TypeScript helper scripts were added.

### Skill Review Outline

Simple utility task: outline a future non-destructive script that opens the in-game Escape menu, opens the Mods dialog, verifies a known mod row, and closes the dialog.

Using the new skill, the utility should:

- Run as a TypeScript script with `bun`.
- Read target ids from `docs/timberborn-menu-coordinate-guide.md`: `pause.mods`, `mods.prometheus_row`, and `mods.ok`.
- Activate `com.mechanistry.timberborn` before input.
- Verify the display assumptions match the coordinate guide.
- Verify the in-game Escape menu is visible before clicking `pause.mods`.
- Stop with a non-zero error if Timberborn is not running, activation fails, screenshot capture fails, the Escape menu is not visible, or any coordinate id is missing.
- Avoid `mods.browse_local_mods`, `mods.create_new_mod`, save loading, delete buttons, and exit targets because those can change state or leave Timberborn.
- Record command output, before/after screen evidence, coordinate ids, and pass/fail notes in the assigned ticket.

### Evidence

- `git diff --check`: passed.
- `dotnet test`: passed, 68 tests.
- Skill review outline: complete in this ticket note.
- Bun execution: not applicable; no TypeScript examples or helper scripts were added.
