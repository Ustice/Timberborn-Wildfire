---
ticket: TWF-118
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-030
write_scope:
  - scripts/**
  - docs/world-consequence-first-pass.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-118-add-world-consequence-scenario-save-generator.md
---

# TWF-118: Add World Consequence Scenario Save Generator

## Goal

Add a Bun TypeScript tool that generates or mutates a Timberborn save/map fixture for repeatable fire-consequence testing.

## Why

World consequences need specific layouts for trees, crops, structures, storage, water, badwater, contamination, and future suppression behavior. Hand-built saves are hard to reproduce and easy to accidentally invalidate.

## Requirements

- Use Bun and TypeScript.
- Inspect a known-good `.timber` save/map archive before assuming the schema.
- Parse and write JSON through structured APIs, not string manipulation.
- Generate from a template save/map copy rather than overwriting user saves.
- Support a dry-run mode that reports planned output, entity counts, and unresolved schema fields.
- Refuse to overwrite generated outputs unless an explicit flag is passed.
- Target the first-pass 50 by 50 layout from `docs/world-consequence-first-pass.md`.
- Include water and badwater north-to-south channels, land bands, trees, crops, representative structures, storage contents, and isolated test pads where the schema allows.
- Write a manifest next to the generated save describing the template source, layout assumptions, and expected validation points.
- Document usage and validation expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-030` provides the existing load-latest-save utility context.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Build the generator as a Bun TypeScript script under `scripts/**`; use `scripts/load-latest-save-and-unpause.ts` for artifact-root, argument parsing, and manifest style inspiration, and `TWF-030` for save discovery context.
- Inspect `.timber` archives with structured zip and JSON APIs before writing anything. Avoid string replacement inside save payloads, and always write to a copied template or generated output folder.
- The dry-run should report template path, output path, detected schema files, planned dimensions, entity counts by category, unresolved schema fields, and whether overwrite protection would block the run.
- The manifest should be the handoff contract for `TWF-119`: template source, generator version or command, layout assumptions, water/badwater channels, tree/crop/structure/storage pads, contamination expectations, and known fallbacks.
- Safe blockers are useful here. If Timberborn save schema fields cannot be resolved, stop with the exact archive member, JSON path, and expected value instead of producing a speculative save.
- Do not launch Timberborn or claim live-load acceptance in this ticket; that validation belongs to `TWF-119`.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if the script is included in project TypeScript checks.
- Run the generator in dry-run mode.
- Run the generator against a copied template and verify it writes the expected output folder and manifest.

## Notes

- Parent design reference: [world-consequence-first-pass.md](../../docs/world-consequence-first-pass.md), "Scenario Save Generator".
- Live Timberborn loading validation belongs to `TWF-119`.
- Do not write directly into a user's existing save unless the user explicitly requests it.
- Worker fix `cd46818b3cead9ed6e7520e9bc043532668bf44b` addresses the second review's symlink-parent confinement finding by refusing symlinked generated roots, output ancestors, output folders, and manifest paths, then rechecking real output/archive/manifest containment after create.
- Fresh generated worker evidence: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-118-re-review-generated-20260503T055438Z`.
- Returned to `04-verify` after the worker fix; requires a fresh passing review before integration because this ticket failed review earlier.
- Fresh re-review passed at `cd46818b3cead9ed6e7520e9bc043532668bf44b`: `git diff --check main...HEAD`, `bun run typecheck`, `dotnet test`, dry-run, copied-template generation, overwrite refusal, marker-backed overwrite, output-root refusal, path-traversal refusal, symlinked output-folder refusal, symlinked output-ancestor refusal, and symlinked manifest-path refusal all passed.
- Worker evidence before the review fixes generated `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-118-worker/generated/wildfire-world-consequence-scenario.timber`; archive inspection showed the expected save members and `world.json` entity count increased from `2246` to `2272`.
- Remaining blockers intentionally handed to `TWF-119`: terrain channel carving, storage inventory contents, crop prototype gaps, occupied coordinates, and live Timberborn load validation.
