---
ticket: TWF-131
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-118
write_scope:
  - scripts/generate-wildfire-scenario-save.ts
  - tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-131-fix-generated-scenario-save-metadata.md
---

# TWF-131: Fix Generated Scenario Save Metadata

## Goal

Make the `TWF-118` generated world-consequence save loadable by Timberborn by fixing the generated `save_metadata.json` shape without broadening the scenario generator.

## Why

`TWF-119` proved the generated save appears in Timberborn's Load Game UI, but loading it crashes before live validation. The first blocker in `Player.log` is `Failed to read save entry save_metadata.json: System.ArgumentOutOfRangeException`, so the QA ticket cannot validate dimensions, water, badwater, crop, structure, storage, or contamination checkpoints until the metadata shape is accepted.

## Requirements

- Inspect the generated archive and the template archive with structured zip and JSON APIs.
- Identify the exact `save_metadata.json` field or shape that Timberborn rejects.
- Fix the generator to preserve or emit a Timberborn-accepted metadata shape.
- Keep output-root, overwrite, and symlink safety from `TWF-118` intact.
- Do not hand-edit the generated save as the accepted fix.
- Add deterministic validation that would catch the rejected metadata shape.
- Regenerate a scenario artifact and manifest for `TWF-119` to rerun.
- Leave crop prototype gaps, storage inventory mutation, terrain/channel carving, and contamination proof to follow-up validation unless the metadata fix directly exposes a small structured correction.

## Dependencies

- `TWF-118` owns the generator and prior safety fixes.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run the generator in dry-run mode.
- Run the generator against a copied template and inspect the generated `save_metadata.json`.
- Run any targeted tests added for metadata shape validation.

## Notes

- Blocking QA evidence: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-119-qa-20260503T140750Z`.
- `TWF-119` should rerun after this fix and after the `TWF-097` Bindito constructor fix is included in the deployed Wildfire build.
- 2026-05-03 worker fix in `~/repos/wildfire-TWF-131`: root cause was the generator replacing Timberborn's accepted `save_metadata.json.Timestamp` shape (`MM/dd/yyyy HH:mm:ss`) with an ISO timestamp. The generator now preserves Timberborn's slash-date timestamp shape while keeping manifest `generatedAt` as ISO, and deterministic metadata validation catches ISO timestamps before QA receives the artifact. Worker checks passed: `git diff --check`, `bun run typecheck`, `bun test tests/generate-wildfire-scenario-save.test.ts` with 3 tests, `dotnet test` with 132 tests, generator dry-run, generator real run, and structured ZIP/JSON inspection with metadata blockers `[]`.
- 2026-05-03 generated artifact for `TWF-119` rerun: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-131-generated-metadata-fix-20260503T1423Z/wildfire-world-consequence-scenario.timber`; manifest: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-131-generated-metadata-fix-20260503T1423Z/wildfire-scenario-manifest.json`.
- 2026-05-03 coordinator: moved to `04-verify` for review.
- 2026-05-03 review passed in `~/repos/wildfire-TWF-131`. Reviewer confirmed the fix is narrow, uses structured archive/JSON output rather than hand-editing saves, preserves `TWF-118` output-root/overwrite/symlink/final-confinement safety paths, and includes a deterministic test that catches ISO `save_metadata.json.Timestamp`. Reviewer reran `git diff --check`, `bun run typecheck`, `bun test tests/generate-wildfire-scenario-save.test.ts` with 3 tests, `dotnet test` with 132 tests, generator dry-run, generator real run, and structured ZIP/JSON inspection with metadata blockers `[]`. Coordinator moved this ticket to `05-integration`; integration must include the reviewed untracked test file.
- 2026-05-03 integration complete in main checkout: `scripts/generate-wildfire-scenario-save.ts` and `tests/generate-wildfire-scenario-save.test.ts` now match the accepted source worktree copies. Integration checks passed: `git diff --check -- scripts/generate-wildfire-scenario-save.ts`, `git diff --cached --check -- scripts/generate-wildfire-scenario-save.ts`, `bun run typecheck`, and `bun test tests/generate-wildfire-scenario-save.test.ts` with 3 tests. Coordinator moved this ticket to `06-done`.
