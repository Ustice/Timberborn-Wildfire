# AGENTS.md

Use bun.sh instead of node.js.

Use `bun` instead of `npm`.

Use Typescript for all repository scripts, skills, and automations.

For Typescript:

- Prefer using `.map`, `.reduce`, `.flatMap`, and similar collection helpers over for-loops.

For Markdown:

- Add one blank line before and after each heading.
- Add blank lines before and after all lists.
- Use 3 spaces for sub-items under single-digit numbers.

For Wildfire:

- Keep the simulation core host-agnostic.
- Timberborn is an adapter and must not own fire rules or mutate the grid directly.
- Prefer deterministic tests and CLI scenarios before live Timberborn validation.
- Use `kanban/` as the durable sprint source of truth.
