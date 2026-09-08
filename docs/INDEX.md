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
- [Corrected Iron Teeth startup](qa/warden-corrected-startup-live.md), [green placement preview](qa/warden-pivot-placement-live.md), [doorstep parent fix](qa/warden-doorstep-parent.md), and [completed placement](qa/warden-doorstep-placement-live.md): placement, district connection and native Warden assignment passed; subsequent ordinary hauling exposed a missing public-input validator.
- [Warden public input](qa/warden-public-input-native-contract.md): native validator and emptying behavior regressions; [ordinary native hauling](qa/warden-native-supply-first-response-live.md) subsequently supplied both stations with 20 Water.
- [Warden helmet presentation](qa/warden-helmet-presentation.md): lazy attachment, ownership and lifecycle proof; the [authored-origin correction](qa/warden-helmet-authored-origin.md) reverses a measured preview-export offset, while native fit remains unverified.
- [Warden coverage display](qa/warden-native-range-display.md): native placement-preview and selection highlighting of potential response surfaces; rendering and preview cleanup passed in the subsequent live run.
- [Native executor Transform](qa/native-executor-transform.md): the first live Warden response exposed a custom-component lookup error; four executor initialization regressions and the subsequent live path-start retry pass.
- [Warden charge and return](qa/warden-native-charge-return-live.md): actual Water pickup, unsafe-route withdrawal, retained-charge saves and return to ordinary activity; suppression and active-phase reload remain open.
- [Warden behavior save owner](qa/warden-behavior-save-owner.md): the worker owns new sortie behaviors so station deletion cannot orphan their saved executor; native reference/load tests pass, with live demolition/reload still pending.
- [Shared fire walking](qa/shared-owned-fire-walk.md): three jobs share native path monitoring while preserving their phase, resource and save decisions; the extraction awaits live acceptance.
- [Ash job classification](qa/ash-native-job-marker.md): actual native work-status and nonworking-worker selection regressions preserve ash collection as the gatherer's assigned job.
- [Warden district lifecycle](qa/warden-district-lifecycle.md): native death and deletion continue after cleanup failures while the shared resource guard prevents inconsistent saves.
- [Incomplete dispatch safety](qa/legacy-dispatch-fail-stop.md): authoritative step outcomes prevent saves and further dispatch after escaping failures interrupt committed world effects; [normal dispatch save exclusion](qa/dispatch-save-exclusion.md) also blocks reentrant saves, while [verified native followup gaps](qa/legacy-native-followup-gaps.md) remain.
- [Soil authority correction](qa/tainted-ash-soil-authority.md): the old rendering-only hook is removed after a [native engine counterexample](qa/original-soil-hook-native-counterexample.md); native soil poisoning reports unavailable until a verified native source integration exists.
- [Native smoke delivery](qa/smoke-native-delivery.md): partial native status/speed changes reject saves until reload; completed actor history remains valid, and callback-driven speed changes are preserved.
- [Elapsed-time ash growth](qa/ash-native-elapsed-growth.md): restore and observation no longer advance plants; the guarded native rate prototype remains inactive pending soil mapping and readiness proof.
- [Clean ash application](qa/clean-ash-application-receipt.md): exact conditional receipts, guarded consumption privilege and a full 46-test GPU pass; fertilizer worker adoption remains open.
- [Inactive fertilizer satchel](qa/native-fertilizer-satchel.md): one native unit, exact reservations, persistence and teardown admission; [shared personal inventory registration](qa/personal-inventory-district-registration.md) centralizes the Warden/satchel lifecycle while preserving their admission rules.
- [Native satchel return](qa/fertilizer-satchel-native-return.md): actual headless Unity one-unit transfer and capacity release, plus disabled-destination refusal.
- [Inactive fertilizer recovery](qa/fertilizer-satchel-recovery-prototype.md): inventory-derived return, verified native root priority, bounded cancellation and unblocked destination access; [native component persistence](qa/fertilizer-recovery-native-persistence.md) now passes in Unity; production templates, full world reload and movement remain activation gates.
- [Inactive finite fertilizer job](qa/finite-fertilizer-job.md): native job ownership, guarded acceptance/rejection, cancel-on-load intent and explicit pending-offer cancellation; Claude review completed, with concrete Runtime and tree travel acceptance still open.
- [Full native domain engine proof](qa/owned-full-domain-engine-proof.md): bounded OWNED4 formation/restore with native objects and the 33-layer world domain; production still uses WF1.
- [Two-inventory engine proof](qa/two-inventory-session-engine-proof.md): exact restore, actual native consumption receipts and reserved-stock preservation under the shared guard, using explicit synthetic damage inputs.
- [Declared storage effects](qa/declared-storage-effects.md): exact inventory identity, reserved-stock preservation, one owner budget and verified withdrawal receipts.
- [Snapshot validation allocation](qa/snapshot-enum-allocation.md): measured optimization and limits of its small-grid comparison.
- [Remaining snapshot copies](qa/planner-snapshot-copy-allocation.md): full-domain allocation attribution, callback isolation regression, and why removing a copy needs an explicit ownership boundary.

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
