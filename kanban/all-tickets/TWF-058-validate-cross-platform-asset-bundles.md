---
ticket: TWF-058
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
   - TWF-050
   - TWF-053
write_scope:
   - scripts/**
   - docs/TEST_PLAN.md
   - docs/HANDOFF.md
   - kanban/all-tickets/TWF-058-validate-cross-platform-asset-bundles.md
---

# TWF-058: Validate Cross Platform Asset Bundles

## Goal

Validate the release asset bundles for the platforms Wildfire intends to support at launch.

## Why

Current live proof is macOS-focused. Steam Workshop users may be on Windows or other supported Timberborn platforms. Release needs either cross-platform bundle validation or an explicit macOS-only limitation.

## Requirements

- Decide the platform support target for the initial Steam Workshop release.
- Build and validate macOS and Windows bundles if both are supported.
- Confirm the release package includes the correct platform bundle names and manifests.
- Capture live or closest-available load evidence for each supported platform.
- Document unsupported platforms clearly if validation is not available.
- Update packaging scripts if platform-specific artifacts need different layout.

## Dependencies

- `TWF-050` hardens asset failure modes.
- `TWF-053` creates the package layout.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if scripts change.
- Preserve bundle build logs, package contents, and live load evidence where available.

## Notes

- If Windows validation requires a separate machine, move the ticket to blocked with the exact environment need.
