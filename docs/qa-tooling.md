# QA Tooling Reliability

This page defines the durable Wildfire QA tool suite and the local reliability log that helps QA improve Timberborn interaction tools over time.

## Goal

QA should reduce repeated manual validation work by building small, reliable tools for Timberborn launch, readiness checks, save loading, command bridge calls, UI navigation, screenshots, recordings, and evidence capture.

The tools should improve over time. A failing QA run should distinguish product behavior from automation reliability so the next agent knows whether to fix Wildfire, repair the environment, tighten the test design, or improve the QA tool.

## Local Database

The live tool-run database is repo-local and ignored by git:

```bash
qa/tool-runs.sqlite
```

SQLite sidecar files are also ignored:

```bash
qa/tool-runs.sqlite-*
```

The Prisma schema is checked in at [../prisma/schema.prisma](../prisma/schema.prisma), the Prisma CLI config lives at [../prisma.config.ts](../prisma.config.ts), and migrations live under [../prisma/migrations/](../prisma/migrations/). Scripts apply migrations automatically when they create or open the local database.

Do not commit the live database. When a result matters for durable project history, export the relevant summary into the GitHub issue, `docs/TEST_PLAN.md`, or a small evidence manifest.

## Failure Classes

Every failed or blocked tool run should use one of these classes:

- `tool_failure`: the automation made a bad assumption, clicked the wrong target, timed out incorrectly, misread state, or produced unreliable evidence.
- `environment_failure`: Timberborn, Steam, the display, the shared QA lock, permissions, or local machine state prevented a fair tool run.
- `product_failure`: the tool worked, but Wildfire or the Timberborn adapter failed the assigned acceptance criterion.
- `test_design_failure`: the gate was ambiguous, too broad, missing a stable observable, or depended on an unsafe/manual step.
- `unknown`: a temporary classification only. QA should reduce this to one of the other classes before integration when practical.

## Recording Runs

Generate Prisma Client after dependency installs or schema changes:

```bash
bun run qa:db:generate
```

Apply checked-in migrations to the local ignored database:

```bash
bun run qa:db:migrate
```

Record a tool run with:

```bash
bun scripts/qa-log-tool-run.ts --tool <tool-name> --result <pass|fail|blocked|unknown> --command "<command or action sequence>"
```

For a tool failure:

```bash
bun scripts/qa-log-tool-run.ts \
  --tool load-latest-save-and-unpause \
  --category navigation \
  --result fail \
  --failure-class tool_failure \
  --reason "screen classifier missed the loaded save state" \
  --artifact screenshot:/tmp/wildfire-qa/latest-save-startup/failure.png
```

For a product failure:

```bash
bun scripts/qa-log-tool-run.ts \
  --tool invoke-timberborn-command \
  --category command \
  --result fail \
  --failure-class product_failure \
  --reason "qa-readiness reported loaded_game_ready=false" \
  --command "bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick"
```

## Reporting Reliability

Summarize the latest local runs with:

```bash
bun scripts/qa-tool-report.ts
```

Limit the report window or emit JSON:

```bash
bun scripts/qa-tool-report.ts --days=7
bun scripts/qa-tool-report.ts --format=json
```

Use the report to decide whether a repeated validation problem should become a QA-tooling issue instead of staying attached to a product ticket.

## Blocker Preflight

Before dispatching another live QA run against the current loaded save, run the blocker preflight:

```bash
bun scripts/qa-blocker-preflight.ts
```

The preflight is read-only. It calls `status`, maps the returned telemetry to the current blocked gates, and reports whether each gate is runnable from the loaded save or still blocked by missing fixture/data evidence.

To evaluate captured evidence without touching Timberborn:

```bash
bun scripts/qa-blocker-preflight.ts --from-file qa-evidence/<run>/status.txt
```

Use this before retrying #17, #43, #44, #45, or #60. If the preflight says the loaded save lacks fixture proof, create or load a targeted scenario instead of running another generic QA pass.

## Generated Scenario Profiles

Use the scenario-save generator to create blocker-focused scenario shapes from a known-good template archive:

```bash
bun scripts/generate-wildfire-scenario-save.ts \
  --template <path-to-template.timber> \
  --profile stored-materials
```

Available profiles:

- `world-consequence`: general trees, crops, storage-like structures, water, badwater, and camera lanes.
- `stored-materials`: adds explicit explosive-storage, contaminated-storage, blast-witness, and contamination-witness pads for #60.
- `persistence-matrix`: adds stored-material pads plus fertile-ash, tainted-washout, crop-counter, and clean burn-duration targets for #17.

The generator writes under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/` and refuses direct writes into user save roots. Inspect `wildfire-scenario-manifest.json` before using the archive as accepted evidence. The current generator can reposition existing template-supported entities and describe required pads, but it still does not stock inventories or carve terrain/water channels directly. Use the live inventory command below for storage/fertile-ash setup; if the manifest reports terrain blockers, QA needs a better template or a dedicated terrain mutator before the scenario can close fixture-heavy gates.

After loading a generated storage scenario, use the live QA command bridge to stock inventories instead of waiting for manual setup:

```bash
bun scripts/invoke-timberborn-command.ts qa-adjust-inventory stored-materials --wait=6 --require-advanced-tick
```

Supported profiles are `stored-materials`, `persistence-matrix`, and `all-consequences`. The command uses Timberborn inventory APIs and reports `targets_scanned`, `targets_adjusted`, and per-good added counts for `Explosives`, `Badwater`, `FertileAsh`, and `Log`. A zero target count is a tool/setup failure for the scenario template, not a product consequence failure.

## Ownership

QA owns this reliability log and should update it during assigned validation work. Workers still own product implementation unless the assignment explicitly gives QA implementation scope.

When a repeated `tool_failure` blocks validation, QA should create or recommend a GitHub issue for the smallest tool improvement that would make the gate reliable.
