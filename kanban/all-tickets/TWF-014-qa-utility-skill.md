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
