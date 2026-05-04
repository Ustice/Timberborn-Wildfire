# Sprint N: Name

## Goal

State the one integration outcome this sprint should produce.

## Included Tickets

- `TWF-000`: reason this ticket is in the sprint.

## Out Of Scope

- `TWF-000`: reason this ticket should not be pulled into the sprint.

## Dependency Order

1. First dependency gate.
2. Second dependency gate.
3. Final integration gate.

## Assignment Packets

- Use [../assignment-packet-template.md](../assignment-packet-template.md) for each sub-agent dispatch.
- Include the main-checkout canonical ticket path in every assignment.
- Tell sub-agents to report ticket notes back to the coordinator unless board edits are explicitly in scope.

## QA Gates

- Gate name: evidence required before dependent tickets can move forward.
- Any failed required QA gate must pass in a later run before the ticket can move to `05-integration/`.
- Any failed required review must return to `03-in-progress/`, move back through `04-verify/`, and pass a fresh review before the ticket can move to `05-integration/`.

## Live QA Risks

- Risk: mitigation or owner.

## Evidence Manifest

- Use [../evidence-manifest-template.md](../evidence-manifest-template.md) for runtime artifacts, screenshots, logs, and generated scenario outputs.

## Close Criteria

- All included tickets are in `06-done`, `07-blocked`, `08-deferred`, or `09-awaiting-review` with concrete notes.
- No ticket with failed required QA is in `05-integration/` or `06-done` unless the failed gate later passed with evidence.
- No ticket with failed required review is in `05-integration/` or `06-done` unless a fresh review later passed from `04-verify/`.
- `bun run kanban:audit` has been reviewed.
- `git diff --check` passes.
- Required tests and QA evidence are linked from tickets.
- `docs/HANDOFF.md` is updated only if durable project status changed.
- The next sprint's first dependency-ready ticket is identified.

## Notes

- Capture sprint-specific decisions here. Keep per-ticket scratch detail in ticket notes or evidence manifests.
