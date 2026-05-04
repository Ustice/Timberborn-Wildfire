# Sprint 6: Fire Feel And Field Presentation

## Goal

Make fire, smoke, ash, and steam readable in live Timberborn recordings, then tune core fire behavior from the same evidence surface.

## Included Tickets

- `TWF-066`: tune visible fire effect readability using the pooled native presentation path and `TWF-065` recording tooling.
- `TWF-067`: tune visible smoke effect after fire readability has a stable baseline.
- `TWF-068`: tune visible ash effect after fire and smoke presentation are legible.
- `TWF-070`: tune visible steam effect after water-suppression behavior is clear enough to record.
- `TWF-088`: tune fire spread pace with deterministic shader evidence and live recording evidence.
- `TWF-089`: tune fuel burn-down duration after spread pace has an accepted baseline.
- `TWF-090`: tune water-suppression behavior after spread and burn-down timing are stable.
- `TWF-091`: tune structure vertical fire behavior after spread and suppression baselines are accepted.
- `TWF-092`: tune burnout cooling behavior after spread, burn-down, suppression, and structure behavior baselines are accepted.
- `TWF-069`: parent recording pass for fire behavior tuning after its child slices are accepted.
- `TWF-099`: add visual and debug settings once the tuned presentation controls are known.
- `TWF-098`: add behavior tuning settings after the accepted behavior constants are known.
- `TWF-051`: decide active-frontier release scope from the accepted tuning evidence.

## Out Of Scope

- `TWF-075` through `TWF-081`: world-consequence implementation belongs to Sprint 7 after fire behavior and presentation are understandable.
- `TWF-071` through `TWF-074`: beaver danger work waits until fire, smoke, ash, and steam fields are readable.
- `TWF-052` through `TWF-063`: release packaging waits until gameplay evidence and media are stable.
- `TWF-082`, `TWF-083`, and `TWF-120` through `TWF-125`: deferred future gameplay work.

## Dependency Order

1. Ready the first non-overlapping wave: `TWF-066`, `TWF-088`, `TWF-089`, `TWF-090`, `TWF-091`, and `TWF-092`.
2. Complete fire-effect readability before smoke, ash, and steam presentation tickets depend on the same visual baseline.
3. Complete behavior slices in dependency order: spread pace, fuel duration, water suppression, structure vertical behavior, then burnout cooling.
4. Run the parent `TWF-069` recording pass only after child behavior slices have review and QA evidence.
5. Add settings tickets only after the tuned controls are known.
6. Use `TWF-051` to decide release scope from accepted Sprint 6 evidence, not speculation.

## Assignment Packets

- Use [../assignment-packet-template.md](../assignment-packet-template.md) for each sub-agent dispatch.
- Include the main-checkout canonical ticket path in every assignment.
- Tell sub-agents to report ticket notes back to the coordinator unless board edits are explicitly in scope.
- Avoid overlapping writes to `src/Wildfire.Timberborn/**`, `src/Wildfire.Unity/**`, and `tests/Wildfire.Core.Tests/**` unless the coordinator narrows the assignment to disjoint files.

## QA Gates

- Deterministic gate: `git diff --check`, `dotnet test`, and shader harness evidence when shader behavior changes.
- Recording gate: live low-resolution or high-resolution recording evidence as required by each ticket.
- Runtime gate: command/status evidence from a loaded save, copied `Player.log`, artifact paths, and final QA lock state.
- Any failed required QA gate must pass in a later run before the ticket can move to `05-integration/`.
- Any failed required review must return to `03-in-progress/`, move back through `04-verify/`, and pass a fresh review before `05-integration/`.

## Live QA Risks

- Risk: Steam or Timberborn launch prompt blocks a command-responsive loaded save.
- Mitigation: keep live-dependent tickets out of `05-integration/` until `bun scripts/load-latest-save-and-unpause.ts --launch` reaches a loaded save and `qa-readiness` can respond.
- Risk: presentation and behavior tickets overlap the same Timberborn runtime surfaces.
- Mitigation: serialize broad write scopes and use narrow review packets.

## Evidence Manifest

- Use [../evidence-manifest-template.md](../evidence-manifest-template.md) for runtime artifacts, screenshots, logs, generated clips, and command outputs.
- Preserve recording metadata from `scripts/record-timberborn-qa.ts` for every accepted visual or behavior tuning clip.

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

- Live Timberborn QA is currently blocked by a launch prompt. Deterministic implementation and review can continue, but live-required tickets must stop at `07-blocked` if the environment remains unavailable.
- `TWF-066` has a reviewed deterministic implementation on `codex/TWF-066-visible-fire-effect` through commit `199047d8b7ac854d102c708854506a1bc1b6e62e`; it is blocked on high-resolution live recording, screenshots, `qa-readiness` or `status`, copied `Player.log`, artifact paths, and final QA lock state.
- `TWF-088` has a reviewed deterministic implementation on `codex/TWF-088-fire-spread-pace` at commit `8eff5cf6adf85cf8729ab19c1abdb592a7f549e3`; it is blocked on live low-resolution spread recording and command-responsive loaded-save evidence. QA evidence manifest: [../evidence-manifests/TWF-088-qa-20260503T073516Z.md](../evidence-manifests/TWF-088-qa-20260503T073516Z.md).
- `TWF-089` has a reviewed deterministic implementation on `codex/TWF-089-fuel-burn-duration` at commit `082077d2b99819c4b448b0ba9fe758ed81f4f412`; it is stacked on reviewed `TWF-088` and blocked on both `TWF-088` live acceptance and its own live burn-duration recording.
- `TWF-090` has a reviewed deterministic evidence pass on `codex/TWF-090-water-suppression` at commit `79aa895778271819312f58e1159a10158aa289ad`; it is stacked on reviewed `TWF-089`, accepts current water constants unchanged, and is blocked on upstream live gates plus its own suppression recording and command/status proof.
- `TWF-091` has a reviewed deterministic evidence pass on `codex/TWF-091-structure-vertical-fire` at commit `83af04b10a05ef192bd9461ca0b90ae35fff5abd`; it is stacked on reviewed `TWF-090`, accepts current structure/vertical behavior unchanged, and is blocked on upstream live gates plus one vertical or multi-cell structure recording and command/status proof.
- `TWF-092` has a reviewed deterministic evidence pass on `codex/TWF-092-burnout-cooling` at commit `36e27d87e01ce786507db925debf954452204198`; it is stacked on reviewed `TWF-091`, accepts current burnout/cooling behavior unchanged, and is blocked on upstream live gates plus one burnout/cooling recording and command/status proof.
- Sprint 6 deterministic lanes are closed as blocked, not done. No Sprint 6 ticket should move to `05-integration/` until its required live recording and runtime evidence exists.
- Keep per-ticket scratch detail in ticket notes or evidence manifests.
