# Wildfire Collaboration

Challenge assumptions with evidence and recommend better approaches. Read the relevant code and documentation before applying guidance; the user's current goal and constraints take precedence over workflow defaults. Ask when a missing decision materially changes scope or risk, and use judgment for routine reversible work.

## Architecture And Verification

- Keep `Wildfire.Core` host-agnostic. Simulation rules belong to the simulator; Timberborn translates world inputs and applies simulator outputs through adapter contracts.
- Start with deterministic tests and scenarios. Use shader execution and live Timberborn evidence when the change depends on those environments.
- Choose checks for the changed behavior and its boundaries. Report what ran, what failed, and what remains unverified; a build or telemetry counter alone does not prove visible gameplay.
- Do not mask missing consequence behavior as success. Handle errors at explicit boundaries, preserve diagnostic context, and make failures observable. Choose recovery or termination based on state integrity instead of a blanket crash policy.
- When reviewing captures, inspect the whole scene for unexpected effects as well as the requested symptom.

## Working Together

- Save coherent progress in incremental commits, stage only owned changes, and preserve unrelated work.
- Delegate substantial independent work when parallel progress helps. Agree on objective, write ownership, branch/worktree, and verification before agents edit concurrently. Use `kanban/assignment-packet-template.md` when helpful.
- The coordinator may investigate, implement, review, and integrate within an unclaimed scope. Serialize integration and shared-file edits; use separate worktrees for concurrent implementation when practical.
- Reuse an appropriate existing checkout or create a `codex/` branch/worktree. Use `~/repos` in shell examples. Resolve conflicting ownership before changing shared work.
- GitHub Issues hold the durable backlog. A directly authorized task can proceed without a new issue; record unfinished follow-up work there when useful. See `kanban/github-issue-workflow.md` for issue operations.

## Shared Machine And Player State

- Assign one live controller for Timberborn deployment, launch/restart, and UI input. Other agents route live requests through that owner; honor the shared QA lock and check its owner/process before recovery.
- Use disposable or copied QA saves for experiments. Do not overwrite player saves or perform destructive operations without authorization. Treat credentials, uploads, release publication, and visibility changes as explicit user decisions; reuse authorization already provided.
- Inspect current application state before UI input. Documented coordinates are reference evidence, and must be checked against the current screen and display assumptions.

## Task Context

Use git-ignored `CONTEXT.md` for multi-turn or multi-agent worktree state. Keep routing metadata in front matter and a short current state, decisions, constraints, evidence pointers, and next action. Compress stale entries instead of accumulating logs or chat. Move durable results into an issue, PR, documentation, or final report at closeout; do not commit `CONTEXT.md`.

## Markdown

Use blank lines around headings and lists, with 3 spaces for nested items under single-digit numbered lists. For structural Markdown transformations, prefer a parser/AST; use `rg` for discovery and read-only checks.
