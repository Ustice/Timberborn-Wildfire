---
ticket: TWF-061
agent_level: Low
role: researcher
requires_qa: false
doc_only: true
dependencies:
   - TWF-055
write_scope:
   - LICENSE
   - NOTICE.md
   - README.md
   - release/**
   - docs/**
   - kanban/all-tickets/TWF-061-add-license-attribution-pass.md
---

# TWF-061: Add License Attribution Pass

## Goal

Add or verify the project license, third-party attributions, and release asset attribution notes.

## Why

Release graphics, screenshots, generated assets, third-party references, and bundled code or tooling may carry attribution or license obligations. The release should be clean before Steam Workshop publication.

## Requirements

- Add or confirm the repository `LICENSE`.
- Add `NOTICE.md` or an equivalent attribution file if needed.
- Review release graphics, generated art, screenshots, Timberborn assets, scripts, dependencies, and copied examples for attribution needs.
- Preserve source, prompt, or capture details for release images.
- Ensure player-facing docs point to license/attribution information where appropriate.

## Dependencies

- `TWF-055` creates release graphics and metadata.

## Role

- Researcher.
- Follow [researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check`.
- No runtime validation is required for documentation-only attribution work.

## Notes

- Raise a blocker if any asset cannot be safely released under the intended license.
