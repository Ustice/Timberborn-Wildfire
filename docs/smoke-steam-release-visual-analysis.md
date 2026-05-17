# Smoke And Steam Release Visual Analysis

## Context

This analysis was made in `~/repos/wildfire-smoke-steam-release-analysis` on branch `codex/smoke-steam-release-analysis`, with the current uncommitted renderer/shader diff from `~/repos/wildfire` copied into the worktree as unstaged changes.

Jason's current read is accurate: the new smoke and steam path looks like a grid of translucent balls. The code does that by design right now, not by accident.

## Current Rendering Path

The current GPU indirect renderer draws smoke and steam from the simulator buffers through `TimberbornGpuIndirectFireRenderer`:

- `src/Wildfire.Timberborn/TimberbornGpuIndirectFireRenderer.cs` draws one smoke billboard instance per grid cell and three steam billboard instances per grid cell.
- `src/Wildfire.Unity/WildfireSmoothing.compute` temporally smooths fire, smoke, smoke contamination, and steam into `_SmoothedFields`.
- `src/Wildfire.Unity/WildfireCloud.shader` renders each smoke or steam instance as a camera-facing circular billboard with a sphere-like falloff.

That path is GPU-efficient and host-agnostic, but it is still visually cell-owned. It exposes the simulation lattice directly to the player.

## Why It Looks Like Translucent Balls

The strongest causes are:

- `WildfireCloud.shader` describes and implements smoke as "1 billboard per cell" with "sphere-shaded circular falloff." The fragment shader uses a radial distance field, a circular mask, and center-brightening. That produces readable individual disks, not a cloud volume.
- Smoke uses exactly one puff per cell. Adjacent hot cells therefore become a repeated grid of identical circles.
- Steam uses three puffs per cell, but the puffs are still anchored to the same cell centers. The staggered lifecycle adds vertical movement, but it does not break the tile lattice.
- Cell positions are deterministic cell centers with no horizontal jitter or region aggregation.
- The renderer draws across the full grid and relies on vertex discard for low intensity, so the visible shape is still "one eligible cell, one cloud object."
- The older ash-overlay puff shader already has a better visual vocabulary: elliptical masks, noise breakup, lower and upper fades, and softer intensity scaling. The new cloud shader regressed from that language back to geometric circles.

## Release Bar

For release, smoke and steam should read as field phenomena:

- Smoke should appear as clustered, drifting, uneven volume above burning or recently burned areas.
- Toxic smoke should be distinguishable by warmer dirty/burgundy tint without looking like a separate cartoon overlay.
- Steam should read as short-lived vapor from water meeting heat, lighter and more vertical than smoke.
- Neither effect should reveal the exact simulation grid unless a debug overlay is explicitly enabled.
- The player should understand cause and danger from normal gameplay camera distance, not from debug counters.

## Recommended Path

The best next step is not a full VFX Graph rewrite. The current indirect renderer is a good performance foundation. The release problem is the cloud presentation contract.

Implementation status in this worktree: the concrete shader and renderer changes below are applied. Region aggregation remains a follow-up only if live visual review still shows the simulation grid after this puff pass.

1. Replace the spherical cloud shader with a noisy, vertically-biased puff shader.

   Port the useful parts of the ash-overlay puff branch into `WildfireCloud.shader`: elliptical radius, Perlin breakup, lower fade, upper fade, asymmetric steam/smoke vertical profiles, and color variation. Remove sphere center shading.

2. Add deterministic per-instance horizontal jitter and scale variation in the vertex shader.

   Use the existing hash function with `cellIndex` and `puffSlot` to offset each puff within roughly one-third of a cell and vary width/height. Keep the jitter deterministic so recordings and tests remain stable.

3. Draw multiple smoke puffs per active smoke cell.

   Smoke needs at least two or three staggered puffs per cell if it remains cell-instanced. Without that, a dense field will always show one repeated mark per tile. This is a small renderer change: make smoke's instance count `cellCount * SmokePuffsPerCell`, mirror the steam instance addressing, and pass `_PuffsPerCell` for smoke too.

4. Make smoke wider and less opaque; make steam taller, lighter, and shorter-lived.

   Current smoke opacity `0.56` is high for overlapping transparent disks. Lower per-puff alpha after adding more puffs, make smoke radius larger than the cell, and make steam rise/fade faster with a smaller base opacity.

5. Then consider region aggregation if the grid still reads.

   The tickets and design already say smoke/steam should cluster or aggregate neighboring intensity into larger regions. If shader jitter and multi-puff breakup are still visibly tiled, the next production step is a compact "cloud region" buffer: group smoke/steam cells into coarse regions and draw region-owned puffs rather than cell-owned puffs. That is the bigger architectural step and should be taken only if the shader pass fails live review.

## Acceptance Evidence

The release gate should require:

- A high-resolution recording on the `Fuel` save or another stable real-field save.
- A normal gameplay camera angle where smoke and steam are visible without debug overlays.
- A screenshot or recording frame reviewed across the whole scene, confirming no grid of repeated balls, no UI obstruction, no sky-colored vapor lost against the background, and no fire/smoke/steam confusion.
- Command/status/log proof that the indirect renderer initialized, smoke/steam sources were nonzero, and material failures stayed at zero.

## Ticket Impact

This should primarily update `TWF-067` and `TWF-070`.

Recommended `TWF-067` implementation boundary:

- Own `WildfireCloud.shader`, `TimberbornGpuIndirectFireRenderer.cs`, and focused renderer/source tests.
- Do not change simulation smoke rules.
- Do not revive per-cell Timberborn GameObjects.
- Accept only after recording evidence shows smoke as a field, not tile markers.

Recommended `TWF-070` implementation boundary:

- Stack on the smoke shader improvements.
- Tune steam-specific height, lifetime, opacity, and color.
- Do not ship steam until water-suppression footage shows it distinct from smoke.
