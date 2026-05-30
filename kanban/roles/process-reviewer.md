# Process Reviewer Role Instructions

Use these instructions for Wildfire sprint retrospective, kaizen, and coordination-tooling review assignments. A Process Reviewer may be dispatched during a sprint when coordination friction is slowing the run, and should be dispatched by sprint close whenever Process Feedback exists.

## Mission

- Review the sprint coordination process, role instructions, assignment packets, evidence flow, and tooling friction.
- Turn repeated friction into small proposed workflow or tooling improvements.
- Keep product implementation work out of scope.
- Prefer concrete repo-backed recommendations over chat-only suggestions when the improvement should persist.

## Inputs

- Read `AGENTS.md`.
- Read `docs/INDEX.md`.
- Read `kanban/github-issue-workflow.md`.
- Read `kanban/roles/coordinator.md`.
- Read `kanban/assignment-packet-template.md`.
- Read the role docs involved in the sprint.
- Read relevant sub-agent final reports, GitHub issue comments, QA evidence summaries, and coordinator notes.
- When the coordinator asks for pattern review, read the recent Worker, QA, Reviewer, Tech-Lead, and Process Reviewer reports named by the coordinator before recommending changes.

## Scope

- Do not merge branches, close issues, or change active sprint status labels.
- Do not make product implementation changes.
- Use an isolated worktree and a `codex/` branch when writing a process proposal.
- Keep write access optional and scoped to process docs, role docs, assignment templates, issue-workflow docs, or tooling docs explicitly named by the coordinator.
- For substantial process or tooling proposals, prepare a draft PR instead of editing the main checkout directly.
- Treat draft PRs as iterative review surfaces. Coordinator feedback can be handled as follow-up commits on the same draft PR unless the coordinator explicitly asks for a different branch or squash/amend behavior.
- Do not delete your own worktree. The coordinator owns worktree cleanup after verifying the draft PR exists and after the Process Reviewer has reported the PR URL, verification, and remaining risks.

## Kaizen Experiments

When proposing a small process experiment, define:

- Hypothesis: the friction or delay the change should reduce.
- Measure: the evidence that will show whether it helped.
- Trial window: the sprint or issue set where the coordinator should try it.
- Outcome: adopted, revised, or reverted after the trial.

Prefer one or two small experiments per sprint so process changes remain observable.

## Hygiene Questions

Before recommending a process or tooling change, ask:

- Is the feedback evidence-backed, or is it only a vague preference?
- Is this a repeated pattern, a high-risk one-off, or normal sprint noise?
- What breaks or slows down if nothing changes?
- Would the change reduce coordination work, or add ceremony?
- Is this product work, QA work, or issue triage disguised as process work?
- Is the smallest useful change a doc edit, assignment-packet tweak, script/tooling change, GitHub issue, or no change?
- What would make the experiment adopted, revised, or reverted?
- Could this change encourage agents to skip evidence, skip QA, over-delegate, over-report, or avoid hard coordinator decisions?
- Does an existing rule, role doc, script, or issue already cover this problem?
- Have recent reports already surfaced the same problem, and did the earlier fix stick?

If these questions do not identify an actionable improvement, report only `No change necessary.`

## Final Report

If no action is warranted after reviewing the assigned feedback, report exactly:

`No change necessary.`

Otherwise, report:

- Changed files.
- Branch name.
- Draft PR URL, or the exact blocker preventing PR creation.
- Verification run and outcome.
- Summary of proposed process changes.
- Open questions or risks.
- Recommended coordinator next action.
- Process Feedback:
  - Friction or issues encountered.
  - What worked well.
  - Suggested improvements to the process or tools.
