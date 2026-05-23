# Wildfire Documentation Index

Use this page as the startup map for humans and agents.

## Active Source Of Truth

- [GitHub Issues](https://github.com/Ustice/Timberborn-Wildfire/issues) owns the active backlog after the 2026-05-23 file-board migration.
- [DESIGN.md](DESIGN.md) owns the simulation and gameplay design.
- [RELEASE_DESIGN.md](RELEASE_DESIGN.md) summarizes the release-facing behavior compared with `main`.
- [ARCHITECTURE.md](ARCHITECTURE.md) owns durable code boundaries and data flow.
- [ash-simulation-model.md](ash-simulation-model.md) owns the current ash authority, naming, and buffer-responsibility direction.
- [steam-simulation-model.md](steam-simulation-model.md) owns the current steam authority, clean-field semantics, and smoke-like transport direction.
- [fire-sim-field-model-plan.md](fire-sim-field-model-plan.md) owns the one-session worktree plan for the anisotropic heat, atmosphere, and contamination shader model.
- [fire-sim-field-model-live-validation.md](fire-sim-field-model-live-validation.md) records live validation attempts and exact blockers for the field-model branch.
- [world-consequence-first-pass.md](world-consequence-first-pass.md) owns the current first-pass plan for stored-item consequences, generated scenario saves, and faction fire-response ideas.
- [HANDOFF.md](HANDOFF.md) owns current status, blockers, and next exact action.
- [TEST_PLAN.md](TEST_PLAN.md) owns validation strategy and evidence expectations.
- [reference/timberborn-ui.md](reference/timberborn-ui.md) owns Timberborn UI design-system notes for adapter-facing UI.
- [timberborn-debug-panels.md](timberborn-debug-panels.md) owns the focused Timberborn debug/developer panel reference for QA.
- [TODO.md](TODO.md) owns milestone-level status, not per-agent scratch work.
- [../kanban/github-issue-migration.md](../kanban/github-issue-migration.md) maps migrated file-board tickets to GitHub issues.
- [../kanban/process.md](../kanban/process.md) owns the historical file-board sprint startup, delegation, verification, and integration flow.
- [../kanban/README.md](../kanban/README.md) owns the historical ticket-board state machine.

## Role Instructions

- [../kanban/roles/coordinator.md](../kanban/roles/coordinator.md)
- [../kanban/roles/worker.md](../kanban/roles/worker.md)
- [../kanban/roles/qa.md](../kanban/roles/qa.md)
- [../kanban/roles/tech-lead.md](../kanban/roles/tech-lead.md)
- [../kanban/roles/researcher.md](../kanban/roles/researcher.md)

## Local Codex Skills

- [../.codex/skills/kanban/SKILL.md](../.codex/skills/kanban/SKILL.md) starts or resumes the file-board sprint.
- [../.codex/skills/delegate/SKILL.md](../.codex/skills/delegate/SKILL.md) guides assigned ticket agents.
- [../.codex/skills/timberborn-qa-utility/SKILL.md](../.codex/skills/timberborn-qa-utility/SKILL.md) guides Timberborn QA utility scripts and guarded UI automation.

## Historical Material

Closed sprint notes and obsolete planning should move to [archive/](archive/) instead of staying in active docs.
