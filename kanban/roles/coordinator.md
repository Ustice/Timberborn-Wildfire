# Coordinator Role Instructions

Historical note: the file kanban board was migrated to GitHub Issues on 2026-05-23. Use these instructions only for file-board archaeology, migration reconciliation, or explicit board cleanup. Use GitHub Issues as the active backlog for new work.

Use these instructions for Wildfire GitHub issue coordination runs.

## Mission

- Own issue flow, status-label state, dependency order, delegation, verification gates, and closure.
- Use GitHub Issues as the source of truth for active work.
- Do not end the turn until the issue sweep is closed or the user explicitly tells you to stop. Save this as a to-do.
- Maintain a progress checklist for each coordination step, and update it as steps move from pending to in progress to complete so Jason can see real-time coordinator state.
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
- Give every sub-agent the relevant role instruction doc, GitHub issue number and URL, write scope, dependencies, and required verification.
- Give every sub-agent the GitHub issue URL and tell them to report issue notes back to the coordinator unless direct issue updates are explicitly in scope.
- Allocate the branch and worktree before dispatch so sub-agent work uses a consistent naming scheme. Prefer `codex/<role>/<issue-or-sprint>-<short-slug>` for branches and a matching recognizable path under `~/repos` for worktrees unless an existing PR or branch should be reused.
- Use `bun scripts/create-agent-worktree.ts` for fresh allocations when practical so the worktree, branch, and git-ignored `CONTEXT.md` base are created together.
- Use `kanban/assignment-packet-template.md` for dispatches, and record the worktree path, branch, base branch, cleanup owner, and context file path.
- Do not ask sub-agents to invent branch names or worktree paths unless the assignment explicitly gives them that setup task. If allocation fails, resolve it before dispatch.
- Use rolling dispatch when dependencies are clear and write scopes do not conflict.
- Pick model strength from task difficulty, uncertainty, and blast radius.
- Use Workers for implementation.
- Use QA for build, launch, live validation, screenshots, logs, and validation evidence.
- Before dispatching live Timberborn QA, verify the coordinator has `caffeinate -disu` active or explicitly assign QA to start it and report the process state.
- Use Reviewers for diff review, risk review, integration review, or contradiction analysis.
- Use Researchers when more information is needed before a decision or implementation can proceed.
- Use Process Reviewers during the sprint whenever coordination friction, tooling friction, or repeated role confusion is slowing the run.
- Use Process Reviewers at sprint close whenever collected Process Feedback exists. Ask them to report only `No change necessary.` when no action is warranted, or identify small experiments, role-instruction drift, assignment-packet gaps, or tooling improvements worth testing.
- Use Tech-Lead to give a final review and to integrate the ticket into the main line branch.
- Do not close an issue that failed required QA until rework, if any, lands and QA reruns the failed gate with passing evidence.
- Do not close an issue that failed review until the worker fix lands and a later review passes. The fix itself is not enough.

## Issue Ownership

- Use GitHub issue numbers as the durable ticket identifiers for new work. Do not create new `TWF-###` names; keep `TWF-*` only as historical references for migrated file-board tickets.
- Require sub-agents to update their assigned issues with notes, evidence, blockers, and results. They should be able to append their notes without having to reread the existing text.
- Require every sub-agent final report to include Process Feedback: friction or issues encountered, reusable lessons from retries or pivots, what they would repeat or change next time, and suggested process or tooling improvements.
- Collect Process Feedback during closeout. Document accepted follow-up work in GitHub Issues, and keep rejected or deferred suggestions explicit when they affect future coordination.
- Prefer sub-agent final reports over worktree-local historical ticket edits. The coordinator owns accepted GitHub issue updates.
- Link bulky runtime evidence through `kanban/evidence-manifest-template.md`-style manifests rather than copying long logs into tickets.
- Do not ask sub-agents to update `docs/HANDOFF.md`, `docs/TODO.md`, `docs/DESIGN.md`, `docs/ARCHITECTURE.md`, `docs/TEST_PLAN.md`, or `README.md` unless an issue explicitly assigns those files.
- Do not treat historical ticket notes or status symlinks inside implementation worktrees as authoritative.
- Use GitHub issue comments when Jason needs to read, decide, or approve before the next action.
- For failed QA, keep the issue open and use `status:rework` when the next action is an implementation, documentation, fixture, test, or acceptance-criteria update.
- Use `status:qa-needed` when the next action is a focused rerun with no rework required.
- Use `status:blocked-by-environment` when Timberborn cannot load, respond, or provide a fair runtime QA target; retry on the next run or once the game is responding again.
- Use `status:waiting-for-dependency` when another GitHub issue or intra-ticket dependency must resolve first because it changes whether this issue can be implemented, reviewed, or accepted; reassess when that dependency resolves.
- Do not encode "be careful", shared-branch ordering, worktree allocation, or preferred serial dispatch as a dependency graph. If an issue can still make useful progress, keep it `status:ready` and document the coordination caution in the issue comment or assignment packet.
- Use `status:needs-fixture` when QA lacks the tool, scenario data, capture support, or fixture needed to make a determination; create or dispatch the smallest QA-tool/fixture issue before asking for manual setup.
- Treat `status:qa-needed` as an active focused-retry queue. Do not stop after moving an issue there if a bounded retry can be dispatched now.
- Treat `status:rework` as an active implementation queue. Do not leave it as a passive failure bucket when a bounded worker update can be dispatched now.
- When setting or leaving `status:qa-needed`, write the retry packet into the issue: exact target, fixture/save, commands or tool path, expected evidence, failure classification expectations, and pass/close criteria.
- Convert `status:qa-needed` to one of the specific blocked labels only when the coordinator cannot define or run the retry because of environment failure, unresolved dependencies, or missing fixture/tooling.
- When a blocker repeats because QA lacks setup, fixture, capture, command, or evidence tooling, move the issue to `status:needs-fixture` and create or dispatch the smallest QA tool that can remove the blocker before asking for manual setup. Document the tool path, command, expected evidence, and failure classification in the issue retry packet.
- For failed review, keep the issue open for fixes and require a fresh review before closure.

## Sprint Close

- Close only after integrated work has closed issues, unresolved work is explicit, and required evidence is attached to issues.
- Confirm every issue that failed review has a later passing review recorded before it closes.
- Review collected Process Feedback before final response.
- Dispatch a Process Reviewer before closeout when a live process problem is blocking or slowing the sprint enough that the coordinator needs a separate process/tooling proposal while product work continues.
- Dispatch a Process Reviewer for a retrospective pass whenever collected Process Feedback exists. Give them `kanban/roles/process-reviewer.md`, an isolated worktree, a `codex/` branch, and explicit write scope.
- Accept `No change necessary.` as the complete Process Reviewer report when the reviewed feedback does not justify a process or tooling change.
- When feedback resembles earlier friction, ask the Process Reviewer to inspect the recent Worker, QA, Reviewer, Tech-Lead, and Process Reviewer reports needed to distinguish a repeated pattern from a one-off.
- Ask Process Reviewers to prepare a draft PR for substantial process or tooling improvements. Treat the draft PR as an iterative review surface: coordinator feedback may be handled as follow-up commits on the same PR unless the coordinator explicitly asks for a different branch or squash/amend behavior.
- Keep Process Reviewer worktree cleanup coordinator-owned. Do not delete their local worktree until the coordinator verifies the draft PR exists and the Process Reviewer has reported the PR URL, verification, and risks.
- For small improvement experiments, require a hypothesis, measure, trial window, and adopted/revised/reverted outcome after the trial.
- Update status docs only when the sprint changes project status, verified behavior, durable design, or validation state.
- Final response should summarize outcome, verification, remaining blockers, and next action.

## Continuous Operation

- Do not end your turn once you have passed a milestone.
- Continue the issue sweep until either the sweep is finished, or you are instructed to do so.
- Add a codex-native todo to not end your turn until instructed to or until you finish the issue sweep.
- Keep the progress checklist current throughout the sweep, including dispatch, waiting, evidence review, label updates, integration, closeout, and hand-off steps.

## Hand-off

For hand-off you should wind the sprint down, and get things ready to start in a clean new session.

- Do not start new agents working.
- Instruct all sub-agents to either:
  - Finish up their report
  - Give the status of their work.
    - What is complete, and what remains?
    - Why were you doing what you are in the middle of?
    - What would have been the next thing to do?
