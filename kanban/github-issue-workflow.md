# GitHub Issue Workflow

[GitHub Issues](https://github.com/Ustice/Timberborn-Wildfire/issues) is Wildfire's durable backlog. Follow [AGENTS.md](../AGENTS.md) for collaboration, ownership, and verification. This workflow supports the task; it does not require creating an issue for every directly authorized change.

## Work And Ownership

1. Read the issue and relevant current evidence. Confirm the outcome, dependencies, owned files, and required checks; resolve material ambiguity before dependent work.
2. Agree on one owner for issue updates and integration. Sub-agents report to that owner unless direct updates are assigned. Serialize status changes.
3. Implement in coherent commits. Parallelize independent work with explicit write ownership; a preferred integration order is a coordination constraint, not automatically a blocked dependency.
4. Record the resulting behavior, verification, and remaining work. Keep long logs in linked evidence artifacts. Close only when required checks and integration are complete; rerun failed gates against the fix before claiming acceptance.

Use the GitHub issue number for new backlog items, with a descriptive title. Historical identifiers are lookup references, not a naming scheme for new work. Create follow-up issues for durable work outside the current scope, rather than expanding the task silently.

## Status Labels

Use a status that describes the next action. Preserve the existing label vocabulary when updating issues; this document does not migrate labels.

| Label | Meaning and evidence to record |
| --- | --- |
| `status:todo` | Scoped work not yet selected. |
| `status:ready` | Useful work can proceed; note real dependencies and coordination constraints. |
| `status:rework` | A failed gate needs a change; name the finding, evidence, fix, and rerun. |
| `status:qa-needed` | Implementation is ready for a specific validation pass; name target, fixture, observable, and pass/fail criterion. |
| `status:blocked-by-environment` | The environment prevents a fair run; name the condition and recovery signal. |
| `status:waiting-for-dependency` | Another result is required before progress; link it and state what it unlocks. |
| `status:needs-fixture` | Evidence requires missing tooling or scenario data; state the smallest unblock work. |
| `status:deferred` | Valid work intentionally outside the current milestone. |

Continue runnable work within the agreed task. When blocked, explain the cause and next action; a status transition alone is not completion. Change acceptance criteria only when the original gate is invalid or the user agrees to a changed outcome, never merely to make a failure pass.

## Commands

```bash
gh issue list --repo Ustice/Timberborn-Wildfire --state open
gh issue view <number> --repo Ustice/Timberborn-Wildfire --comments
gh issue comment <number> --repo Ustice/Timberborn-Wildfire --body-file <reviewed-comment-file>
```

Use structured tool arguments or a body file for multiline updates. Keep progress in the task/context file until a durable result or decision merits an issue update.
