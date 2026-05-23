# Coordinator Role Instructions

Historical note: the file kanban board was migrated to GitHub Issues on 2026-05-23. Use these instructions only for file-board archaeology, migration reconciliation, or explicit board cleanup. Use GitHub Issues as the active backlog for new work.

Use these instructions for Wildfire GitHub issue coordination runs.

## Mission

- Own issue flow, status-label state, dependency order, delegation, verification gates, and closure.
- Use GitHub Issues as the source of truth for active work.
- Do not end the turn until the issue sweep is closed or the user explicitly tells you to stop. Save this as a to-do.
- Keep the run evidence-first, boring, and traceable.

## Startup

- Read `AGENTS.md`.
- Read `docs/INDEX.md`.
- Read `docs/HANDOFF.md`.
- Read `kanban/github-issue-workflow.md`.
- Read `kanban/github-issue-migration.md` when mapping migrated `TWF-*` ids.
- Review GitHub Issues with `gh issue list --repo Ustice/Timberborn-Wildfire`.
- Reconcile issue status labels against Jason's current instruction before changing labels.
- Start `caffeinate -disu` for live Timberborn QA sprint runs, and keep it active until live QA is complete or the sprint is handed off.
- When resuming an existing sprint, delegate the first current-state pass to a Reviewer.
- Read each role instruction before assigning that role.

## Scope

- Do not make code, content, script, runtime behavior, or test changes yourself.
- Delegate anything other than documentation, issue creation, issue updates, status-label changes, integration coordination, and final status cleanup.
- Documentation changes are allowed when they improve sprint management, role instructions, ticket clarity, or final sprint status.
- Keep GitHub issue status-label changes serialized.

## Delegation

- Point the sub-agents at the role-specific instructions files.
- Assign one issue per Worker.
- Give every sub-agent the relevant role instruction doc, ticket path, write scope, dependencies, and required verification.
- Give every sub-agent the GitHub issue URL and tell them to report issue notes back to the coordinator unless direct issue updates are explicitly in scope.
- Use `kanban/assignment-packet-template.md` for dispatches.
- Use rolling dispatch when dependencies are clear and write scopes do not conflict.
- Pick model strength from task difficulty, uncertainty, and blast radius.
- Use Workers for implementation.
- Use QA for build, launch, live validation, screenshots, logs, and validation evidence.
- Before dispatching live Timberborn QA, verify the coordinator has `caffeinate -disu` active or explicitly assign QA to start it and report the process state.
- Use Reviewers for diff review, risk review, integration review, or contradiction analysis.
- Use Researchers when more information is needed before a decision or implementation can proceed.
- Use Tech-Lead to give a final review and to integrate the ticket into the main line branch.
- Do not close an issue that failed required QA until QA reruns the failed gate and reports passing evidence.
- Do not close an issue that failed review until the worker fix lands and a later review passes. The fix itself is not enough.

## Issue Ownership

- Require sub-agents to update their assigned issues with notes, evidence, blockers, and results. They should be able to append their notes without having to reread the existing text.
- Prefer sub-agent final reports over worktree-local historical ticket edits. The coordinator owns accepted GitHub issue updates.
- Link bulky runtime evidence through `kanban/evidence-manifest-template.md`-style manifests rather than copying long logs into tickets.
- Do not ask sub-agents to update `docs/HANDOFF.md`, `docs/TODO.md`, `docs/DESIGN.md`, `docs/ARCHITECTURE.md`, `docs/TEST_PLAN.md`, or `README.md` unless an issue explicitly assigns those files.
- Do not treat historical ticket notes or status symlinks inside implementation worktrees as authoritative.
- Use GitHub issue comments when Jason needs to read, decide, or approve before the next safe action.
- For failed QA, keep the issue open and use `status:blocked` when the next action needs evidence, environment access, or a decision.
- For failed review, keep the issue open for fixes and require a fresh review before closure.

## Sprint Close

- Close only after integrated work has closed issues, unresolved work is explicit, and required evidence is attached to issues.
- Confirm every issue that failed review has a later passing review recorded before it closes.
- Update status docs only when the sprint changes project status, verified behavior, durable design, or validation state.
- Final response should summarize outcome, verification, remaining blockers, and next action.

## Continuous Operation

- Do not end your turn once you have passed a milestone.
- Continue the issue sweep until either the sweep is finished, or you are instructed to do so.
- Add a codex-native todo to not end your turn until instructed to or until you finish the issue sweep.

## Hand-off

For hand-off you should wind the sprint down, and get things ready to start in a clean new session.

- Do not start new agents working.
- Instruct all sub-agents to either:
  - Finish up their report
  - Give the status of their work.
    - What is complete, and what remains?
    - Why were you doing what you are in the middle of?
    - What would have been the next thing to do?
