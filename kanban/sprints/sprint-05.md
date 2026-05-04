# Sprint 5: Safety Rails And Test Surfaces

## Goal

Make the next gameplay work reproducible and safe before broader world-consequence implementation begins.

## Included Tickets

- `TWF-126`: tighten ticket implementation notes before assignment.
- `TWF-093`: validate enabled save/reload.
- `TWF-094`: validate disabled-mod save recovery.
- `TWF-095`: validate re-enable runtime rebuild after `TWF-093` and `TWF-094`.
- `TWF-047`: integrate the save/reload, disable, and re-enable lifecycle after child validation tickets.
- `TWF-049`: add compatibility probes.
- `TWF-050`: harden GPU asset failure modes after `TWF-049`.
- `TWF-064`: investigate building burnout pause consequence mismatch.
- `TWF-065`: add screen recording QA tooling.
- `TWF-114`: define resource fuel and flammability catalog.
- `TWF-118`: add generated world-consequence scenario save tool.
- `TWF-119`: validate the generated world-consequence scenario save after `TWF-118`.
- `TWF-096`: add release settings framework if capacity allows.
- `TWF-097`: add enable/disable safety setting if `TWF-096` lands early enough.

## Out Of Scope

- `TWF-066` through `TWF-070`: visual and behavior tuning wait for recording tooling.
- `TWF-075` through `TWF-081`: world consequence implementation waits for resource catalog, scenario generation, and save/reload proof.
- `TWF-085` through `TWF-087`: beaver behavior waits for beaver field contract and exposure telemetry.
- `TWF-052` through `TWF-063`: release packaging waits until gameplay consequences and evidence are more stable.
- `TWF-082`, `TWF-083`, and `TWF-120` through `TWF-125`: deferred future gameplay systems.

## Dependency Order

1. Run `bun run kanban:audit` and reconcile the ready candidates with this charter.
2. Dispatch `TWF-126`, `TWF-049`, `TWF-064`, `TWF-065`, `TWF-093`, `TWF-094`, `TWF-096`, `TWF-114`, and `TWF-118` where write scopes allow.
3. After `TWF-093` and `TWF-094`, dispatch `TWF-095`, then parent `TWF-047`.
4. After `TWF-049`, dispatch `TWF-050`.
5. After `TWF-118`, dispatch `TWF-119`.
6. After `TWF-096`, dispatch `TWF-097` if capacity remains.

## Assignment Packets

- Use [../assignment-packet-template.md](../assignment-packet-template.md) for each sub-agent dispatch.
- Include the main-checkout canonical ticket path in every assignment.
- Tell sub-agents to report ticket notes back to the coordinator unless board edits are explicitly in scope.

## QA Gates

- Save/reload validation must preserve logs, commands, screenshots where useful, and pass/fail evidence for enabled, disabled, and re-enabled states.
- Live Timberborn deploy, launch, restart, and shared-lock work belongs to QA.
- Any failed required QA gate must pass in a later run before the ticket can move to `05-integration/`.
- `TWF-064` must produce either a fixed building-burnout consequence path or the smallest concrete blocker for the next sprint.
- `TWF-119` must prove the generated scenario loads or document the exact schema/runtime blocker.

## Live QA Risks

- Save/reload tests can consume time if Timberborn starts in a stale or paused state. Use existing load/unpause and `qa-readiness` utilities before deeper validation.
- The generated scenario tool may uncover save schema assumptions. Treat schema discovery as useful output, not failure, if the blocker is precise.
- `TWF-065` and `TWF-118` both touch `scripts/**`; do not dispatch them concurrently unless their write scopes are narrowed first.
- `TWF-049`, `TWF-064`, `TWF-096`, and `TWF-114` may overlap in `src/Wildfire.Timberborn/**`; serialize or narrow assignments.

## Evidence Manifest

- Use [../evidence-manifest-template.md](../evidence-manifest-template.md) for runtime artifacts, screenshots, logs, generated saves, and scenario manifests.

## Close Criteria

- All included tickets are in `06-done`, `07-blocked`, `08-deferred`, or `09-awaiting-review` with concrete notes.
- No ticket with failed required QA is in `05-integration/` or `06-done` unless the failed gate later passed with evidence.
- `bun run kanban:audit -- --strict` passes or any critical finding is explicitly explained.
- `git diff --check` passes.
- `bun run typecheck` passes if any TypeScript script changed.
- Required tests and QA evidence are linked from tickets.
- `docs/HANDOFF.md` is updated only if durable project status changed.
- The first Sprint 6 dependency-ready ticket is identified.

## Notes

- This sprint is intentionally infrastructure-heavy. It buys down coordination risk for the later world-consequence and beaver behavior sprints.
- The first dependency-ready wave is already in `02-ready/`: `TWF-049`, `TWF-064`, `TWF-065`, `TWF-093`, `TWF-094`, `TWF-096`, `TWF-114`, `TWF-118`, and `TWF-126`.

## Closeout

- Closed on 2026-05-03 with no critical `kanban:audit` findings.
- Done: `TWF-047`, `TWF-049`, `TWF-065`, `TWF-093`, `TWF-094`, `TWF-095`, `TWF-096`, `TWF-114`, `TWF-118`, and `TWF-126`.
- Blocked on live Timberborn launch environment: `TWF-050`, `TWF-064`, `TWF-097`, and `TWF-119`.
- `TWF-050` deploy validation is fixed and reviewed, but healthy GPU runtime and missing-compute proof still need fresh live QA after Steam clears its launch-args prompt.
- `TWF-097` deterministic enable/disable implementation passed review after a typed-settings fix, but live disabled-state proof still needs Timberborn launch to work.
- `TWF-064` and `TWF-119` are live-QA tickets and should resume after `bun scripts/load-latest-save-and-unpause.ts --launch` can reach a command-responsive loaded save again.
- First Sprint 6 dependency-ready tickets identified: `TWF-066`, `TWF-088`, `TWF-089`, `TWF-090`, `TWF-091`, and `TWF-092`.
