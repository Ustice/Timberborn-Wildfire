# Wildfire Issue Workflow

Wildfire uses [GitHub Issues](https://github.com/Ustice/Timberborn-Wildfire/issues) as the active backlog.

The old file-kanban archive was moved off `main` to branch `archive/file-kanban-2026-05-23`. Search that branch when you need old ticket bodies, sprint charters, final status symlinks, or historical evidence manifests.

## Active Files

- [github-issue-workflow.md](github-issue-workflow.md) describes the active issue-backed workflow.
- [github-issue-migration.md](github-issue-migration.md) maps migrated `TWF-*` ids to GitHub issue numbers.
- [assignment-packet-template.md](assignment-packet-template.md) is the current sub-agent dispatch template.
- [evidence-manifest-template.md](evidence-manifest-template.md) is the current compact QA/runtime evidence template.
- [roles/](roles/) contains active Coordinator, Worker, QA, Reviewer, Tech-Lead, and Researcher instructions.

## Archive Lookup

Use git when you need historical file-board material:

```bash
git fetch origin archive/file-kanban-2026-05-23
git grep "TWF-172" archive/file-kanban-2026-05-23 -- kanban/all-tickets
git show archive/file-kanban-2026-05-23:kanban/sprints/sprint-12-release-gameplay-readiness.md
```

On GitHub, switch the branch selector to `archive/file-kanban-2026-05-23` and search within that branch.
