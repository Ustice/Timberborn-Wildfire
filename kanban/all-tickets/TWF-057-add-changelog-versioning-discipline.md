---
ticket: TWF-057
agent_level: Low
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-053
write_scope:
   - CHANGELOG.md
   - scripts/**
   - docs/**
   - kanban/all-tickets/TWF-057-add-changelog-versioning-discipline.md
---

# TWF-057: Add Changelog Versioning Discipline

## Goal

Define the release versioning rules and add a changelog process that keeps package, manifest, and release notes aligned.

## Why

Steam Workshop and GitHub releases need consistent version identity. The manifest version, package filename, changelog entry, and tag should not drift.

## Requirements

- Add `CHANGELOG.md`.
- Define version bump rules for prerelease, patch, minor, and release-candidate builds.
- Ensure the release packaging workflow validates manifest version against the requested package version or tag.
- Document the release checklist for updating version, changelog, and release notes.
- Prefer a simple script-owned validation over manual memory.

## Dependencies

- `TWF-053` creates the release packaging workflow that should enforce version consistency.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if scripts change.
- Run version validation or package dry-run checks.

## Notes

- Keep the process lightweight enough to actually use.
