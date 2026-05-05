---
ticket: TWF-155
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-147
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-155-clarify-gpu-renderer-region-telemetry.md
---

# TWF-155: Clarify GPU Renderer Region Telemetry

## Goal

Make GPU field renderer region counters describe what actually happened during a dispatch.

## Why

`TWF-147` live QA proved the GPU field renderer can render a real imported field region, but the same evidence showed `dropped_regions` increasing when sampled regions fell below the visible-intensity threshold. That makes the counter sound like capacity loss or presenter failure when it may only be a normal below-threshold cull.

Release diagnostics need this telemetry to be trustworthy, especially when QA is comparing visible renderer behavior against field intensity.

## Requirements

- Split below-threshold culls from true capacity or binding drops.
- Preserve the existing `gpu_field_renderer_dropped_regions` field only if its meaning is narrowed and documented.
- Add a separate status field for below-threshold or invisible regions if QA still needs that count.
- Update deterministic tests to cover visible, below-threshold, binding-missing, and max-region-limit cases.
- Update `docs/TEST_PLAN.md` with the accepted meaning of the region counters.

## Implementation Notes

- Start in `TimberbornGpuFieldRendererSink.CompleteVisualEffectDispatch`, where below-threshold regions are currently counted through the same dropped-region path as true drops.
- Keep the renderer behavior unchanged unless the counter split exposes an actual rendering defect.
- Prefer explicit names such as `below_threshold_regions` or `invisible_regions` over overloading `dropped_regions`.

## Verification

- Run `git diff --check`.
- Run focused `TimberbornGpuFieldRendererTests`.
- Run `dotnet test Wildfire.slnx`.

## Notes

- 2026-05-05 coordinator: created during `TWF-147` live QA. Evidence root `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-147-live-20260505T030230Z/` shows valid renderer proof at tick `260`, but also shows `dropped_regions` on follow-up ticks where the field intensity had already fallen below the visible threshold.
- 2026-05-05 worker: split below-threshold renderer regions into `invisible_regions` / `gpu_field_renderer_invisible_regions`. `dropped_regions` now remains reserved for missing surface binding or capacity drops, while fadeout/threshold culls report separately. The same pass also aligns the status `gpu_field_renderer_updated_regions` counter with the log's rendered `updated_regions` value instead of reporting every sampled candidate region.
- 2026-05-05 verification passed: `git diff --check`, focused `dotnet test --filter FullyQualifiedName~TimberbornGpuFieldRendererTests`, and full `dotnet test Wildfire.slnx` with 275 tests.
