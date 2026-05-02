---
ticket: TWF-059
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
   - TWF-053
   - TWF-055
   - TWF-058
write_scope:
   - docs/TEST_PLAN.md
   - docs/HANDOFF.md
   - release/**
   - kanban/all-tickets/TWF-059-run-release-candidate-clean-install-qa.md
---

# TWF-059: Run Release Candidate Clean Install QA

## Goal

Validate the packaged release candidate from a clean install path instead of the development deploy folder.

## Why

Release readiness must be proven from the artifact players will install. Development deploy success is not enough.

## Requirements

- Start from a clean Timberborn Mods folder state or a documented clean test profile.
- Install the packaged release candidate.
- Launch Timberborn and confirm mod discovery.
- Load a save and run the coherent gameplay loop.
- Confirm visuals, alert/status, settings, save/reload behavior, and no critical exceptions.
- Preserve `Player.log`, screenshots, package checksum, install path, commands, and artifact paths.
- Update `docs/TEST_PLAN.md` with release-candidate evidence.
- Update `docs/HANDOFF.md` with blockers or release readiness.

## Dependencies

- `TWF-053` creates the package.
- `TWF-055` provides release metadata.
- `TWF-058` validates platform bundle support.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Passing evidence requires a clean install from the release artifact and no new critical `Player.log` failures.

## Notes

- Do not satisfy this with `scripts/deploy-timberborn-mod.ts --apply`.
