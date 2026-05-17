# Persistent Ash Scar Rendering Implementation Plan

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

**Goal:** Make the full burned ash scar remain visible on huge Timberborn maps even after accumulated ash exceeds the current per-dispatch render cap.

**Architecture:** Keep fire simulation host-agnostic and leave burn/ash rules in the core visual field. Change only the Timberborn ash presentation path so persistent old ash is represented more efficiently than one live rendered region/quad per ash cell. Keep the current high-detail, newly updated ash overlay for the active front, and add a lower-detail persistent scar layer for old accumulated ash.

**Tech Stack:** C#/.NET, xUnit, Timberborn Unity adapter, Unity mesh/material presenter, Bun deployment script.

---

## Current context

The current normal ash overlay path is in `src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs`.

Important existing behavior:

- `TimberbornGpuFieldRendererOptions.MaxUpdatedRegionsPerDispatch` defaults to `2048`.
- `_visibleRegions` stores accumulated ash region state across dispatches.
- `SelectRenderedRegions(...)` now prioritizes `updatedRegions` first, then fills with older persistent regions.
- This keeps the ash front moving, but if the scar grows far beyond `2048` visible regions, some old ash drops from the presenter’s rendered region list.
- Debug overlay intentionally remains intensity-sorted and should not be changed by this feature.

The likely test file is `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`.

Existing deploy command is `bun scripts/deploy-timberborn-mod.ts --apply --clean` after a successful dry run.

---

## Product decision

Use a two-layer ash overlay:

1. **Fresh ash detail layer**
   - Existing per-region/quads path.
   - Prioritize newly updated ash and active front detail.
   - Still bounded by `MaxUpdatedRegionsPerDispatch`.

2. **Persistent scar summary layer**
   - Lower-detail representation for older accumulated ash.
   - Downsample many old ash cells into coarser scar tiles.
   - Render a bounded number of coarse tiles so the whole scar remains visible without unbounded quads.

This avoids simply raising the 2048 cap, which risks frame-rate and mesh-update cost on large maps.

---

## Task 1: Add explicit renderer options for scar summarization

**Objective:** Make the new behavior configurable and offloadable without changing core fire rules.

**Files:**

- Modify: `src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs`
- Test: `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`

**Step 1: Add a failing options validation test**

Add tests near existing option validation tests, or create them if none exist:

```csharp
[Fact]
public void RejectsInvalidPersistentAshScarOptions()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => new TimberbornGpuFieldRendererOptions(
        PersistentAshScarCellSize: 0));
    Assert.Throws<ArgumentOutOfRangeException>(() => new TimberbornGpuFieldRendererOptions(
        MaxPersistentAshScarRegions: 0));
}
```

**Step 2: Run the targeted test and verify failure**

Run:

```bash
dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --configuration Debug --filter RejectsInvalidPersistentAshScarOptions
```

Expected: compile failure because the option parameters do not exist yet.

**Step 3: Add options to `TimberbornGpuFieldRendererOptions`**

Add constructor parameters after `AshBlendCellRadius`:

```csharp
int PersistentAshScarCellSize = 8,
int MaxPersistentAshScarRegions = 4096,
```

Add validation:

```csharp
if (PersistentAshScarCellSize <= 0)
{
    throw new ArgumentOutOfRangeException(
        nameof(PersistentAshScarCellSize),
        PersistentAshScarCellSize,
        "Persistent ash scar cell size must be positive.");
}

if (MaxPersistentAshScarRegions <= 0)
{
    throw new ArgumentOutOfRangeException(
        nameof(MaxPersistentAshScarRegions),
        MaxPersistentAshScarRegions,
        "Persistent ash scar region limit must be positive.");
}
```

Add properties:

```csharp
public int PersistentAshScarCellSize { get; }

public int MaxPersistentAshScarRegions { get; }
```

Assign them in the constructor.

**Step 4: Run the targeted test and verify pass**

Run:

```bash
dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --configuration Debug --filter RejectsInvalidPersistentAshScarOptions
```

Expected: pass.

**Step 5: Commit**

```bash
git add src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs
git commit -m "feat: configure persistent ash scar rendering"
```

---

## Task 2: Introduce a presenter contract that can render detail and scar layers separately

**Objective:** Let the sink pass high-detail active ash and low-detail persistent scar data without overloading the current one-list `RenderRegions` call.

**Files:**

- Modify: `src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs`
- Modify: Unity presenter class in the same file or adjacent class if it is split later
- Test: `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`

**Step 1: Add a scar region record**

In `TimberbornGpuFieldRenderer.cs`, near `TimberbornGpuFieldRendererRegionState`, add:

```csharp
public readonly record struct TimberbornGpuFieldRendererScarRegionState(
    int RegionId,
    int MinX,
    int MinY,
    int MinZ,
    int MaxX,
    int MaxY,
    int MaxZ,
    int SourceRegionCount,
    float Ash,
    float Visibility,
    float Intensity);
```

**Step 2: Add a presentation payload record**

```csharp
public readonly record struct TimberbornGpuFieldRendererPresentation(
    IReadOnlyList<TimberbornGpuFieldRendererRegionState> DetailRegions,
    IReadOnlyList<TimberbornGpuFieldRendererScarRegionState> ScarRegions);
```

**Step 3: Extend the presenter interface conservatively**

Prefer adding a new method while keeping the old method for compatibility during migration:

```csharp
TimberbornGpuFieldRendererPresentationResult RenderPresentation(
    TimberbornGpuFieldRendererPresentation presentation);
```

Default presenter implementations can initially route `DetailRegions` to existing rendering and ignore `ScarRegions`.

**Step 4: Update test presenters**

In `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`, update `RecordingGpuFieldRendererPresenter` to record both lists:

```csharp
public List<TimberbornGpuFieldRendererRegionState> RenderedRegions { get; } = new();

public List<TimberbornGpuFieldRendererScarRegionState> RenderedScarRegions { get; } = new();
```

`RenderPresentation` should clear and append both lists.

**Step 5: Keep old tests passing**

Update `TimberbornGpuFieldRendererSink.CompleteVisualEffectDispatch(...)` so it builds:

```csharp
TimberbornGpuFieldRendererPresentation presentation = new(
    DetailRegions: renderedRegions,
    ScarRegions: Array.Empty<TimberbornGpuFieldRendererScarRegionState>());
```

Then call `RenderPresentation(presentation)`.

**Step 6: Run existing renderer tests**

Run:

```bash
dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --configuration Debug --filter TimberbornGpuFieldRendererTests
```

Expected: all existing renderer tests pass.

**Step 7: Commit**

```bash
git add src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs
git commit -m "feat: add ash renderer presentation payload"
```

---

## Task 3: Build deterministic persistent scar downsampling in the sink

**Objective:** Convert accumulated old ash regions that do not fit the detail cap into coarse scar tiles.

**Files:**

- Modify: `src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs`
- Test: `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`

**Step 1: Add a failing test for full-scar preservation under a tiny detail cap**

Add a test like:

```csharp
[Fact]
public void AshOverlaySummarizesOldAshIntoPersistentScarWhenDetailLimitIsExceeded()
{
    RecordingFireLogSink logSink = new();
    RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
    {
        [0] = Sample(0, fire: 0f, smoke: 0f, ash: 0.6f, visibility: 1f),
        [1] = Sample(1, fire: 0f, smoke: 0f, ash: 0.6f, visibility: 1f),
        [2] = Sample(2, fire: 0f, smoke: 0f, ash: 0.6f, visibility: 1f),
        [3] = Sample(3, fire: 0f, smoke: 0f, ash: 0.6f, visibility: 1f),
    });
    TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader, width: 4, height: 1, depth: 1);
    RecordingGpuFieldRendererPresenter presenter = new();
    TimberbornGpuFieldRendererSink sink = new(
        surface,
        logSink,
        new TimberbornGpuFieldRendererOptions(
            RegionSize: 1,
            MaxUpdatedRegionsPerDispatch: 2,
            AshBlendCellRadius: 0,
            PersistentAshScarCellSize: 2,
            MaxPersistentAshScarRegions: 8),
        presenter);

    sink.BeginVisualEffectDispatch(1);
    sink.UpdateVisualEffect(EffectEvent(0, 1));
    sink.UpdateVisualEffect(EffectEvent(1, 1));
    sink.CompleteVisualEffectDispatch(1);

    sink.BeginVisualEffectDispatch(2);
    sink.UpdateVisualEffect(EffectEvent(2, 2));
    sink.UpdateVisualEffect(EffectEvent(3, 2));
    sink.CompleteVisualEffectDispatch(2);

    Assert.Equal([2, 3], presenter.RenderedRegions.Select(static region => region.RegionId).OrderBy(static id => id).ToArray());
    Assert.Contains(presenter.RenderedScarRegions, static region => region.SourceRegionCount >= 2);
    Assert.True(presenter.RenderedScarRegions.Sum(static region => region.SourceRegionCount) >= 2);
}
```

Adjust `CreateBoundSurface(...)` if its signature differs. If it does not accept dimensions today, add a small helper overload in the test file only.

**Step 2: Run and verify failure**

Run:

```bash
dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --configuration Debug --filter AshOverlaySummarizesOldAshIntoPersistentScarWhenDetailLimitIsExceeded
```

Expected: fail because no scar regions are produced.

**Step 3: Implement `SelectScarRegions(...)`**

Add a private method in `TimberbornGpuFieldRendererSink`:

```csharp
private TimberbornGpuFieldRendererScarRegionState[] SelectScarRegions(
    IReadOnlyCollection<TimberbornGpuFieldRendererRegionState> detailRegions)
```

Behavior:

- Return empty array when `Options.DebugOverlayEnabled` is true.
- Build a `HashSet<int>` of detail region IDs.
- Use `_visibleRegions.Values` excluding detail regions as candidates.
- Group candidates into coarse tiles based on `PersistentAshScarCellSize` and world x/y/z bounds.
- For each tile:
   - `SourceRegionCount` is number of source detail regions represented.
   - `Ash` is the max or weighted average ash. Prefer max for visibility at first.
   - `Visibility` is max visibility.
   - `Intensity` is `Ash * Math.Max(Visibility, 0.001f)`.
   - Bounds are min/max of source bounds.
- Sort by deterministic tile ID or intensity then tile ID.
- Take `Options.MaxPersistentAshScarRegions`.

**Step 4: Call `SelectScarRegions(...)` from dispatch completion**

After `renderedRegions = SelectRenderedRegions(updatedRegions)`, add:

```csharp
TimberbornGpuFieldRendererScarRegionState[] scarRegions = SelectScarRegions(renderedRegions);
```

Pass both to `RenderPresentation`.

**Step 5: Run the targeted test and verify pass**

Run:

```bash
dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --configuration Debug --filter AshOverlaySummarizesOldAshIntoPersistentScarWhenDetailLimitIsExceeded
```

Expected: pass.

**Step 6: Run all renderer tests**

Run:

```bash
dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --configuration Debug --filter TimberbornGpuFieldRendererTests
```

Expected: pass.

**Step 7: Commit**

```bash
git add src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs
git commit -m "feat: summarize old ash into persistent scar tiles"
```

---

## Task 4: Add tests that fresh ash still wins over old scar detail

**Objective:** Prevent regression to the earlier bug where old ash monopolized visible output.

**Files:**

- Modify: `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`

**Step 1: Add a failing/focused prioritization test**

Add a test that creates many old ash regions, then emits new ash regions with a tiny `MaxUpdatedRegionsPerDispatch`.

Assertions:

- `RenderedRegions` contains the newly updated region IDs.
- `RenderedScarRegions` contains old source regions not present in detail.
- No updated region is moved only into scar while stale regions occupy the detail layer.

Suggested assertion pattern:

```csharp
int[] detailIds = presenter.RenderedRegions.Select(static region => region.RegionId).ToArray();
Assert.Contains(newUpdatedRegionId, detailIds);
Assert.DoesNotContain(oldRegionIdThatShouldBeScarOnly, detailIds);
Assert.Contains(presenter.RenderedScarRegions, region => region.SourceRegionCount > 0);
```

**Step 2: Run the new test**

Run:

```bash
dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --configuration Debug --filter AshOverlayKeepsFreshAshInDetailLayerWhenOldAshIsSummarized
```

Expected: pass once Task 3 behavior exists. If it fails, adjust `SelectRenderedRegions(...)` before proceeding.

**Step 3: Commit**

```bash
git add tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs
git commit -m "test: cover fresh ash priority with persistent scar layer"
```

---

## Task 5: Implement Unity presenter rendering for the scar layer

**Objective:** Render coarse scar tiles in-game instead of only recording them in tests.

**Files:**

- Modify: `src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs` if `TimberbornUnityGpuFieldRendererPresenter` is in this file
- Otherwise modify the presenter file containing `TimberbornUnityGpuFieldRendererPresenter`
- Possibly modify: `src/Wildfire.Unity/AshOverlay.shader`
- Possibly modify: Unity asset bundle build inputs if shader/material parameters change

**Step 1: Inspect presenter internals**

Locate `TimberbornUnityGpuFieldRendererPresenter` in `TimberbornGpuFieldRenderer.cs` or adjacent files.

Identify:

- Where mesh vertices/quads are built from `TimberbornGpuFieldRendererRegionState`.
- Whether a second mesh object is easier than mixing detail and scar vertices in one mesh.
- Current material color/intensity encoding.

**Step 2: Prefer one mesh with two sets of quads unless shader changes are needed**

Implementation guideline:

- Detail regions render exactly as today.
- Scar regions render as larger quads using their min/max x/y bounds.
- Scar alpha/intensity should be lower than fresh ash so active front remains readable.
- Use deterministic vertex ordering.
- Keep z projection/surface behavior from accumulated source states; do not re-query the simulation core.

**Step 3: Add or update unit-level presenter tests if practical**

If current presenter mesh construction is not testable because it depends heavily on Unity runtime types, extract a pure helper that converts presentation state into mesh instructions:

```csharp
internal static IReadOnlyList<AshOverlayQuad> BuildAshOverlayQuads(
    TimberbornGpuFieldRendererPresentation presentation)
```

Then test:

- Detail quads are one cell/region sized.
- Scar quads cover coarse bounds.
- Scar alpha is lower than detail alpha.

**Step 4: Run targeted tests**

Run:

```bash
dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --configuration Debug --filter TimberbornGpuFieldRendererTests
```

Expected: pass.

**Step 5: Commit**

```bash
git add src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs src/Wildfire.Unity/AshOverlay.shader tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs
git commit -m "feat: render persistent ash scar layer"
```

Only include `src/Wildfire.Unity/AshOverlay.shader` if it actually changes.

---

## Task 6: Improve logging/counters for diagnosing scar coverage

**Objective:** Make live Timberborn logs show whether the detail/scar split is healthy.

**Files:**

- Modify: `src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs`
- Test: `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`

**Step 1: Extend counters**

Add to `TimberbornGpuFieldRendererCounters`:

```csharp
int RenderedDetailRegionCount,
int RenderedScarRegionCount,
int PersistentAshScarSourceRegionCount,
```

Track these from the latest completed dispatch.

**Step 2: Extend log output**

Add fields to `wildfire_timberborn_gpu_field_renderer_updated`:

```text
rendered_detail_regions=...
rendered_scar_regions=...
persistent_ash_scar_source_regions=...
```

**Step 3: Add a test assertion**

In the scar summarization test, assert counters and log fields, e.g.:

```csharp
Assert.True(sink.Counters.RenderedScarRegionCount > 0);
Assert.Contains(logSink.InfoMessages, message => message.Contains("rendered_scar_regions=", StringComparison.Ordinal));
```

**Step 4: Run tests**

Run:

```bash
dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --configuration Debug --filter TimberbornGpuFieldRendererTests
```

Expected: pass.

**Step 5: Commit**

```bash
git add src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs
git commit -m "chore: log persistent ash scar coverage"
```

---

## Task 7: Full validation and deploy

**Objective:** Verify the feature without relying only on live-game observation.

**Files:**

- No source changes expected unless failures are found.

**Step 1: Run full test suite**

Run:

```bash
dotnet test Wildfire.slnx --configuration Debug
```

Expected: all tests pass.

**Step 2: Run dry-run deploy with clean output**

Run:

```bash
bun scripts/deploy-timberborn-mod.ts --dry-run --clean
```

Expected:

- Build succeeds.
- Unity asset bundle rebuild succeeds if required by shader/material changes.
- Copy plan includes `Wildfire.Timberborn.dll`, `Wildfire.Core.dll`, and visual asset bundle artifacts.

**Step 3: Apply deploy only after dry-run passes**

Run:

```bash
bun scripts/deploy-timberborn-mod.ts --apply --clean
```

Expected:

- Refuses if Timberborn is open, unless explicitly overridden.
- Writes to `~/Documents/Timberborn/Mods/Wildfire`.

**Step 4: Live Timberborn validation**

Use a large map where the burned scar exceeds the old `2048` detail cap.

Watch Player.log for:

- `rendered_detail_regions` near the active front cap.
- `rendered_scar_regions` greater than zero after old ash accumulates.
- `persistent_ash_scar_source_regions` growing with burned area.
- No sustained `material_failures`.

Visual acceptance:

- Active ash front keeps progressing.
- Older burned area remains visibly scarred at lower detail.
- Old scar may be softer/coarser than fresh ash, but it should not disappear wholesale.
- Fire/smoke particles remain unaffected.

**Step 5: Commit final validation notes if the repo tracks them**

If there is a suitable QA note or kanban ticket, update it. Otherwise do not add noisy documentation.

---

## Risks and tradeoffs

- **Visual fidelity:** Coarse scar tiles will be less precise than one quad per ash cell. This is intentional for performance.
- **Overdraw:** Very large scar quads could increase overdraw. Keep tiles moderately sized, e.g. 8x8 cells, and alpha subdued.
- **Height variation:** Timberborn terrain is uneven. A coarse tile spanning different surface Z values may look floaty or clipped. If this appears in-game, group scar tiles by surface Z as well as x/y tile.
- **Memory growth:** `_visibleRegions` still grows with ash coverage. This plan solves render capacity, not internal tracking. If memory becomes an issue later, add a true compressed scar store and evict old detail regions into it.
- **Shader changes:** If the existing shader cannot distinguish detail/scar alpha cleanly from vertex colors, asset bundle rebuild is required.

---

## Open questions

- Should old scar persist forever, or slowly fade after N in-game days?
- Is coarse scar acceptable visually on steep cliffs and stacked terrain, or should the scar layer split by surface Z from the start?
- Should `PersistentAshScarCellSize` be exposed in a mod config file, or remain hard-coded options for now?
- Should `_visibleRegions` eventually be compressed too, so old ash does not stay as one dictionary entry per cell forever?

---

## Recommended first implementation path

Implement Tasks 1 through 4 first and run all renderer tests. That will prove the data model and prioritization are correct without touching Unity rendering complexity.

Then implement Task 5 in the presenter and validate live. If the Unity presenter turns out to make a separate scar layer mesh easier, prefer that over a clever combined mesh.
