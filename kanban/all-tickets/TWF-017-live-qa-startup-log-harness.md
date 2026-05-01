---
ticket: TWF-017
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-013
   - TWF-016
write_scope:
   - scripts/**
   - docs/TEST_PLAN.md
---

# TWF-017: Add Live QA Startup Log Harness

## Goal

Create a repeatable command that launches or attaches to Timberborn QA, waits for startup evidence, and captures the logs/screenshots needed to prove Wildfire loaded.

## Why

Manual UI access is useful, but the sprint needs a durable smoke test that shows whether the deployed mod is actually running. This should make later live tickets faster and less ambiguous.

## Requirements

- Use Bun and TypeScript for any new script.
- Reuse documented coordinates from `TWF-013` when UI automation is needed.
- Serialize launch, deploy, and log capture with a local QA lock.
- Capture `Player.log` and any Wildfire-specific log paths named by the deploy or command bridge tickets.
- Wait for searchable success or failure tokens instead of relying only on elapsed time.
- Save screenshots only when they support a visible claim.
- Fail loudly when Timberborn is not running, the resolution does not match, or expected log evidence is missing.
- Document the command, prerequisites, output paths, and known limitations.

## Dependencies

- `TWF-013` supplies reliable UI coordinates.
- `TWF-016` supplies a deployed mod worth detecting.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run any script help or dry-run mode with `bun`.
- Run live Timberborn QA and capture evidence when the local game state is available.

## Notes

- This ticket should not load saves or trigger gameplay. `TWF-015` owns loading the latest save.
- Keep the first version focused on startup and mod-load evidence.
