# Wildfire Documentation

Use the entry point that matches the work. Source and fresh validation establish implemented behavior; design decisions explain intent; dated records preserve earlier evidence without defining current machine or backlog state.

## Current guides

- [Design](DESIGN.md): simulation model, packed formats, and gameplay boundaries.
- [Architecture](ARCHITECTURE.md): project ownership, actual execution paths, and compatibility seams.
- [Source map](source-map.md): concepts mapped to code and tests.
- [Validation](TEST_PLAN.md): portable checks, shader execution, native game QA, and evidence.
- [GitHub Issues](https://github.com/Ustice/Timberborn-Wildfire/issues): active backlog and acceptance state.

## Release validation evidence

- [Current-build live baseline](qa/live-current-build-baseline.md): verified deployment, ordinary GPU ticks, and tick-33 save/reload at `8cbeb19`.
- [Fresh faction startup](qa/live-faction-startup.md): populated Folktails success and the Iron Teeth Warden preview failure on that build.
- [Corrected Iron Teeth startup](qa/warden-corrected-startup-live.md), [green placement preview](qa/warden-pivot-placement-live.md), and [doorstep parent fix](qa/warden-doorstep-parent.md): successive live building checks and their native regressions; completed placement and worker operation remain separate checks.
- [Full native domain engine proof](qa/owned-full-domain-engine-proof.md): bounded OWNED4 formation/restore with native objects and the 33-layer world domain; production still uses WF1.
- [Two-inventory engine proof](qa/two-inventory-session-engine-proof.md): exact restore, actual native consumption receipts and reserved-stock preservation under the shared guard, using explicit synthetic damage inputs.
- [Declared storage effects](qa/declared-storage-effects.md): exact inventory identity, reserved-stock preservation, one owner budget and verified withdrawal receipts.
- [Snapshot validation allocation](qa/snapshot-enum-allocation.md): measured optimization and limits of its small-grid comparison.

## Focused references

- [Ash decisions](ash-simulation-model.md) and [steam decisions](steam-simulation-model.md).
- [Conditional ash receipts](reference/ash-collection-receipts.md): simulator-owned removal and host commit evidence.
- [Native owned-walk stopping](reference/native-owned-walker-stop.md): path callbacks, scheduler timing and restored executor ownership.
- [Civilian carrying emergency prototype](reference/carry-emergency-prototype.md): development opt-in, native ownership and remaining needs/recovery limitations.
- [Persistent burn-damage identity](reference/burn-damage-identity.md): native entity keys, legacy saves and restoration evidence.
- [QA tooling](qa-tooling.md) and [Timberborn deploy pipeline](reference/timberborn-deploy-pipeline.md).
- [Versioning](release/versioning.md) and [Workshop packaging/publication](release/workshop.md).
- [Native API reference](reference/timberborn-native-api-reference.md), [API support requests](reference/native-api-support-requests.md), and [blueprint reference](reference/blueprint-reference.md).
- [Timberborn UI](reference/timberborn-ui.md), [status icon design](reference/status-icon-design-language.md), and [debug panels](timberborn-debug-panels.md).
- [Menu reference](timberborn-menu-coordinate-guide.md) and [bottom-menu reference](timberborn-bottom-menu-guide.md). Captured coordinates describe their recorded scene, not a current window.

## Design records and history

The [design records](history/2026-09-04/README.md) preserve earlier gameplay rationale, implementation descriptions, and dated technical findings. They describe their original code and environment, not necessarily the current implementation.

Deleted process documents remain in Git history. Earlier file-kanban tickets and evidence manifests are on branch `archive/file-kanban-2026-05-23`.
