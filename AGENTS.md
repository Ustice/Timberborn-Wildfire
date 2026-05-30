# AGENTS.md

Ask questions. Jason isn't always right, and he wants to know when he is wrong. Don't look for the best answer for today; look for the best answer.

Use `~/repos` instead of `/Users/jasonkleinberg/repos`

When visually reviewing screenshots or captures, check the whole scene for anything off, not only the specific symptom you set out to verify.

## Markdown

- Add one blank line before and after each heading.
- Add blank lines before and after all lists.
- Use 3 spaces for sub-items under single-digit numbers.
- For scripts that read or rewrite Markdown by section, prefer a TypeScript Markdown parser/AST, such as `remark`/`mdast` with front-matter support, over line-oriented `sed`/`grep`. Use `rg` for quick heading discovery and read-only checks.

## Wildfire

- Keep the simulation core host-agnostic.
- Timberborn is an adapter and must not own fire rules or mutate the grid directly.
- Prefer deterministic tests and CLI scenarios before live Timberborn validation.
- Use GitHub Issues as the durable backlog source of truth.
- Treat branch `archive/file-kanban-2026-05-23` as the migrated historical board unless a task explicitly asks for file-board archaeology.
- Do not hide Timberborn consequence behavior behind no-op, skip, or fallback stopgaps. If a requested live consequence is missing, implement a concrete attempt and let real errors crash loudly.

## Task Context

- For multi-turn or multi-agent worktree tasks, use the git-ignored `CONTEXT.md` in the worktree as the short-lived task state file.
- Use front matter for machine-readable routing metadata such as task, role, branch, worktree, base branch, cleanup owner, status, and updated timestamp.
- Use Markdown headings for human-readable state: current state, decisions, constraints, evidence pointers, next action, and process notes.
- Update `CONTEXT.md` by compression instead of accumulation. Replace stale state, keep evidence as links or paths, and avoid copying chat history, logs, or diffs.
- At closeout, move durable information into the GitHub issue, PR, docs, or final report. Do not commit `CONTEXT.md`.
