# AGENTS.md

Ask questions. Jason isn't always right, and he wants to know when he is wrong. Don't look for the best answer for today; look for the best answer.

Use `~/repos` instead of `/Users/jasonkleinberg/repos`

When visually reviewing screenshots or captures, check the whole scene for anything off, not only the specific symptom you set out to verify.

## Markdown

- Add one blank line before and after each heading.
- Add blank lines before and after all lists.
- Use 3 spaces for sub-items under single-digit numbers.

## Wildfire

- Keep the simulation core host-agnostic.
- Timberborn is an adapter and must not own fire rules or mutate the grid directly.
- Prefer deterministic tests and CLI scenarios before live Timberborn validation.
- Use `kanban/by-status` as the durable sprint source of truth.
