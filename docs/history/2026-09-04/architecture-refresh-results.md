# Architecture Refresh Results

This dated record describes the implementation following the [review baseline](review-baseline.md). Current design and commands live in [Architecture](../../ARCHITECTURE.md) and [Test Plan](../../TEST_PLAN.md).

## Changes

- Simplified project guidance, skills, assignment templates, and role documents. Removed mandatory role sequences, separate process reviews, and repeated procedural gates. Retained explicit ownership, coherent commits, proportionate verification, and shared-session/player-state protections.
- Replaced stale current documentation with concise source-backed descriptions. Preserved superseded plans and dated evidence in this directory rather than treating them as ongoing requirements.
- Separated routine ash transport observations from save snapshots. Synchronization reads one transport buffer per successfully observed tick; it no longer reads and converts packed cells for this purpose.
- Consolidated GPU command encoding, bounded queue handling, and tick orchestration in Core. Live and portable backends use the same contract, including smoke overrides. Delta buffers accommodate external changes and simulation transitions in the same dispatch.
- Separated portable, native adapter, and opt-in shader suites. Hosted CI runs the checked-in portable and Bun suites rather than a generated four-test substitute. Disabled shader cases report skipped explicitly.
- Centralized native assembly configuration and inventory/construction compatibility operations for the installed Timberborn 1.1.2.4 API. Package identity now has one shared definition. Construction requests are prepared before deletion; this does not make native rebuilding transactional.
- Extracted QA targeting and proof accounting from the fire system. Player sustained ignition has its own scheduler, with explicit 12/96-dispatch preset durations replacing an unused simulation-cadence parameter.
- Made initialization readiness, rejection, and failure explicit. Unsupported worlds are rejected before import and do not repeat initialization each update. Runtime composition and persistence changes are described in the current architecture documentation.

## Verification Boundaries

Integrated code revision `1cba73d` passed the full Release .NET solution: 573 native adapter tests and 83 portable tests, with zero build warnings/errors. All 16 disabled shader cases were reported as skipped. The final portable CI command independently passed the same 83 tests. Bun passed 43 tests; TypeScript checking and all 375 generated blueprints passed. Markdown review found no missing local targets across 45 files and verified 58 source-map paths.

Local logs are `/tmp/wildfire-final-full-dotnet.log`, `/tmp/wildfire-final-portable.log`, `/tmp/wildfire-final-bun.log`, `/tmp/wildfire-final-typecheck.log`, and `/tmp/wildfire-final-blueprints.log`. These are local evidence pointers, not committed artifacts or claims about another checkout.

Portable tests exercise the shared protocol, queue, coordinator, and backend contracts. Native tests compile against installed game assemblies and exercise adapter behavior with fixtures and doubles. Neither establishes rendered appearance or full game integration.

The two new external-change shader regressions were enabled and attempted. Unity exited before shader compilation because no valid Editor license was available. Their actual GPU results remain unverified. Resume with the external-change test command in the Test Plan after license activation.

No game deployment, game launch, player-save mutation, release publication, or Git push was performed for this refactor. Targeted validation on a disposable save remains necessary for native inventory accounting, construction reconstruction, rendering, and save/reload behavior. Native reconstruction can still fail after deletion; preparation is not rollback.

## Follow-up After License Activation

The user subsequently authorized publication to `main`, activated Unity, and authorized fixes for failures exposed by shader execution. GitHub CI passed for `f87de2d`. Unity CLI `1.0.0-beta.6` is installed; the shader harness continues to invoke Unity Editor `6000.3.6f1` directly.

The two external-change regressions passed immediately after activation. The full suite then exposed ten older failures. Replaying five representative scenarios with the pre-refactor runner and unchanged shader at `625251f` produced identical packed cells, transport fields, material fields, and per-tick delta counts. The refactor had not introduced those reproduced failures.

Corrections made during this follow-up:

- Fixtures now serialize Core parameters rather than using independently hard-coded runner tuning (notably ignition 11 versus production 5). Explicit complete overrides remain supported.
- Transport fixtures now provide open air, sufficient vertical space and time, and actual emission sources. Ash assertions read authoritative transport rather than deprecated material ash fields. Wind assertions measure directional heat bias without assuming adjacent four-bit samples cannot tie.
- Fixed a real shader bug: ash generated from toxic smoke lost the smoke's contamination. Both local deposition and newly generated falling ash now retain it under the existing maximum-contamination rule. Two isolated GPU regressions failed before the fix and passed afterward.

Final follow-up validation: **19 shader tests executed and passed**, **88 portable tests passed**, and **573 native tests passed**, with zero build warnings/errors. The shader execution log is `/tmp/wildfire-shader-corrected-full.log`; the .NET integration log is `/tmp/wildfire-shader-followup-dotnet.log`. Pre-fix provenance evidence is `/tmp/wildfire-smoke-ash-red.log`, and baseline comparison evidence is `/tmp/wildfire-shader-baseline-comparison/results.json`.

The Unity licensing blocker is resolved. Native gameplay, rendering, and save/reload still require targeted validation in a disposable game save; this follow-up did not launch or deploy Timberborn.
