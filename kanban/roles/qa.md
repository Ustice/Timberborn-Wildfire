# QA

Validate assigned behavior using [AGENTS.md](../../AGENTS.md), relevant sections of [TEST_PLAN.md](../../docs/TEST_PLAN.md), and [qa-tooling.md](../../docs/qa-tooling.md). Start from the build, fixture, acceptance criterion, and prior evidence that the assignment identifies.

## Execution

Choose the smallest reliable test that answers the question. Use deterministic checks before live validation where possible. Improve a broken tool or fixture within agreed scope instead of repeatedly rerunning an unreliable path; coordinate product changes with their owner.

For live work, one controller owns deployment, launch/restart, the shared QA lock, and input. Reuse a suitable session and confirm process/readiness state before deciding to launch again. Inspect lock ownership before recovery, and keep long captures awake when needed. Follow the player-save and publication boundaries in AGENTS.md.

Fresh observations can establish UI targets. Validate screen and display assumptions before scripted input; if uncertain, obtain better evidence. Inspect the whole scene in screenshots. Logs and counters support runtime claims but cannot substitute for visible evidence when appearance is the acceptance criterion.

## Results

Distinguish tool, environment, product, and test-design failures using the taxonomy in `docs/qa-tooling.md`. Report observed symptoms separately from inferred causes. For a startup failure, identify whether the failure was launch, process lifetime, app activation, bridge response, or loaded-save readiness; use current logs/process evidence rather than assuming duplicate launches.

Record fixture/build identity, commands or actions, pass/fail per criterion, and relevant artifact paths. Log meaningful automation runs when maintaining durable reliability history. Use the [evidence template](../evidence-manifest-template.md) for larger artifact sets.

Rerun a failed required gate against its fix before recommending acceptance. For incomplete validation, name the remaining observable and the smallest next step. Send results to the assigned reporting owner; do not change issue status concurrently with another owner.
