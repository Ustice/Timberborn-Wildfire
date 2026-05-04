---
ticket: TWF-106
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-053
  - TWF-103
write_scope:
  - scripts/**
  - release/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-106-verify-platform-package-layout.md
---

# TWF-106: Verify Platform Package Layout

## Goal

Verify that platform-specific bundles, names, manifests, and package layout match the accepted support target.

## Why

Even if bundles build, the release archive can still fail because names or paths do not match Timberborn or Workshop expectations.

## Requirements

- Inspect the package layout created by `TWF-053`.
- Confirm each supported platform bundle is present with the expected name.
- Confirm unsupported or unvalidated platform artifacts are not misleadingly included.
- Add script checks where reasonable.
- Update packaging scripts if platform-specific layout needs correction.
- Document package-layout evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-053` creates the package layout.
- `TWF-103` defines the support target.

## Parent Reference

- Parent gate: `TWF-058`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if scripts change.
- Preserve package listing or checksum evidence.

## Notes

- Live platform load evidence belongs to `TWF-104` and `TWF-105`.
