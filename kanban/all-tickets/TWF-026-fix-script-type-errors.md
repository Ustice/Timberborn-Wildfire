---
ticket: TWF-026
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-017
write_scope:
   - scripts/**
   - tsconfig.json
   - package.json
   - bun.lock
---

# TWF-026: Fix Script Type Errors

## Goal

Make the repository's TypeScript scripts type-check cleanly with Bun-oriented tooling.

## Why

The repo now relies on TypeScript scripts for deploy, command invocation, Timberborn guide generation, and QA automation. Type errors in those scripts make the automation harder to trust and slow down future sprint work.

## Requirements

- Identify the current TypeScript type-check command or add the smallest Bun-based one if none exists.
- Use Bun tooling, not `npm` or Node-centered workflow assumptions.
- Fix type errors in existing scripts without changing their intended runtime behavior.
- Prefer clear typed helpers and data shapes over suppressions.
- Avoid broad rewrites unrelated to type correctness.
- Preserve existing script command-line behavior.
- If a config file is needed, keep it minimal and document why it exists in the ticket notes.
- Do not overlap or undo the active `TWF-017` startup-log harness work.

## Dependencies

- `TWF-017` owns active changes in `scripts/**`; this ticket should wait until that work is integrated or explicitly coordinate with it.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run the chosen Bun-based TypeScript type-check command.
- Run relevant script help or dry-run commands with `bun` to confirm runtime behavior still works.
- Run `dotnet test` if any script changes affect build, deploy, or generated source behavior.

## Notes

- The current repo has TypeScript scripts under `scripts/` but may not yet have a committed `tsconfig.json` or package manifest.
- If the type checker reports errors caused by missing ambient Bun or Node types, solve that as tooling setup rather than by weakening script types.
