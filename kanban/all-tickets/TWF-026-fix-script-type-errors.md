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

## Worker Notes

- Worktree: `~/repos/wildfire-TWF-026` on branch `codex/TWF-026-fix-script-type-errors`.
- Added the smallest Bun-managed type-check surface: `package.json` with `bun run typecheck`, `bun.lock`, and a minimal `tsconfig.json` limited to `scripts/**/*.ts`.
- `tsconfig.json` exists so `tsc --noEmit` has Bun ambient types, strict checking, bundler module resolution, and no output path.
- Chosen type-check command: `bun run typecheck`.
- Fixed strict script errors without changing runtime behavior by returning existing `fail(...)` calls from impossible branches in `check-timberborn-startup.ts` and `invoke-timberborn-command.ts`.
- Did not run `generate-timberborn-modding-guide.ts` because it writes generated docs outside this ticket's write scope.

## Evidence

- `bun run typecheck` passed.
- `git diff --check` passed.
- `bun scripts/check-timberborn-startup.ts --help` passed.
- `bun scripts/invoke-timberborn-command.ts --help` passed.
- `bun scripts/deploy-timberborn-mod.ts --help` passed.
- `bun scripts/check-timberborn-startup.ts --dry-run --skip-resolution-check --wait=0 --screenshot=never` passed.
- `dotnet test` passed: 73 tests.

## Blockers

- `bun scripts/deploy-timberborn-mod.ts --dry-run --remove --mods-dir /tmp/wildfire-twf-026-deploy-check --lock-timeout=1` was blocked by the shared deploy/QA lock held by an active `check-timberborn-startup` launch session, so I did not force-remove the lock.

## Result

- TypeScript scripts now type-check through Bun-oriented tooling with `bun run typecheck`.
- No unresolved implementation blockers.
