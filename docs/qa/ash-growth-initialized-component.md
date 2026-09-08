# Initialized native ash-growth component proof

On source `448a1becd39128396c4a844c36276f0cb060ccc6`, the controller's CPU-only licensed Unity probe passed the positive prepared-growth path using actual GameObjects and installed native methods. This closes the supplied component initialization/liveness gate. **The ticker remains unbound: authored clean requests do not prove production readiness, GPU acceptance, full prefab lifecycle, or live fertility gameplay.**

## Executed boundary

The fixture creates three independent plant graphs. Each uses actual ComponentCache.InjectDependencies/Initialize with its complete supplied component list, EntityComponent.Awake and **EntityComponent.Initialize**, Growable.Awake through native TimeTriggerFactory, actual living/dying components and NativeResourceLifecycleModel creation, and native BlockService.InitializeArrays/SetObject. No EntityState assignment or filtered-out initialization interface forces the positive gate.

Clock/spec values, body placement, terrain-column arrays and a minimal real model hierarchy are explicit fixture inputs. Native BlockObject.UpdateTransformedBlocks derives actual PositionedBlocks/entrance/CoordinatesAtBaseZ from that placement. The ordinary placement is z=0, BaseZ=1, and the tall footprint also occupies z=2; native base is therefore z=1. The supplied padded native column storage has XY index4, lower floor0/ceiling1 at index4 and a separate floor3/ceiling4 at index13. Actual CellToIndex/TryGetIndexAtCeiling methods perform lookup.

Native Growable initialization finds actual `FullModel/Mature|Seedling/#Alive|#Dying|#Dead` GameObjects. They establish native hierarchy/lifecycle-model lookup, not imported art rendering. Reproducible's optional specification is absent, so its actual Awake disables reproduction; native growth still executes BlockReproduction/UnblockReproduction and completion without an unsupplied world reproducer. Full template instantiation, Entity.PreInitialize/PostInitialize, navigation and world reproduction are outside this graph.

The unbound production growth ticker receives an **explicit authored observation callback**, configured native clock and actual adapter/coordinator. Preparation uses CaptureAtRest and application uses TransferInventory; no production current-world-ready callback is substituted or claimed.

## Results

- All three real Entity.Initialize calls returned Initialized, nondeleted and positive native BaseComponent/GameObject truth. Each native BlockService.SetObject completed. A lookup at occupied z=2 returns the real Growable, establishing that its subsequent rejection is the soil filter, not absent placement.
- Requests at occupied upper z=2 and plain placement origin z=0 produce zero candidates/progress. The exact native base/ceiling z=1 applies once.
- With native duration2 days, multiplier1.1 and clock interval0.24 hours, extra progress is **0.000500023365** (expected0.0005 within native float precision). No completion occurs during that partial interval.
- For bounded completion proof, the fixture then calls native IncreaseGrowthProgress(.999) to prepare near-completion and supplies a one-day interval. The ticker reaches native progress1 and HasGrown once; another interval does not repeat completion. This is explicit timer preparation, not proof of a full natural growing season or GPU-created ash.
- A separate prepared candidate whose native lower ceiling changes1→2 before application produces Applied0 and no progress. The candidate's native object remains live; the exact soil contact has become invalid.
- A fresh near-complete plant's actual HasGrown subscriber throws after native progress reaches1. The original exception survives, LastApplication stays absent, the existing coordinator becomes indeterminate, and save plus another Tick are rejected. Completion count stays1.
- Native Growable.DeleteEntity pauses timers, BlockService.UnsetObject removes fixture placements, registry Unregister removes completed registrations, and DestroyImmediate removes all three root hierarchies before Editor.Exit. Cleanup: **3/3 graphs, 3/3 root GameObjects, 0 errors**. This root count includes destruction of their model children; it is not a claim that only three GameObjects were created.

## Failed attempt and exact correction

First attempt terminal64113 exited1 before GameObject allocation or Entity.Initialize. The supplied BlockObjectSpec omitted its required nested Entrance; actual PositionedEntrance.From dereferenced it at IL0000 during UpdateTransformedBlocks. The installed Pine blueprint explicitly declares `Entrance { HasEntrance:false, Coordinates:(0,0,0) }`.

The corrected fixture adds only that native default no-entrance specification. Both production DLL hashes remain identical; no native assembly, production code, component interface or initialization state was changed. First-attempt logs/source remain preserved. Its cleanup completed1/1 graph,0/0 root GameObjects,0 errors before exit.

Corrected attempt terminal87543 exited0. Unity6000.3.6f1 reported graphicsNull and batchTrue; installed game engine is6000.5.5f1. This proves the selected component methods under that Editor runtime, not whole-mod Editor compatibility or game behavior. No Persistent allocation notice appeared. An unrelated Unity cloud Curl42 shutdown cancellation is retained in the full log.

## Reproduction and fingerprints

- Detached build: `/Users/jasonkleinberg/repos/wildfire-worktrees/ash-growth-engine-448a1be`, exact448a1be before the documentation commit. Fresh Release build succeeded,0 errors and2 existing CS8600 smoke-adapter warnings.
- Original failed run: `/tmp/wildfire-ash-growth-component-engine-run/`.
- Corrected proof: `/tmp/wildfire-ash-growth-component-engine-corrected/` (`unity.log`, `positive.json`, `subscriber-failure.json`, `outcome.json`, command and before/final/native hashes, `fixture.diff`).
- Corrected source: `UnityProject/Assets/Editor/AshGrowthComponentProbe.cs`; entry AshGrowthComponentProbe.Run; environment WILDFIRE_ASH_GROWTH_COMPONENT_PROBE_OUTPUT. Licensed invocation uses `-batchmode -nographics -quit`. Unmodified native mod DLL stays outside Assets and matching Core is in Plugins.
- Native mod SHA256: `622c4343b3a736ef4fde74e8b9471077b18fb651cbef07b304afa8c51b1bdad3`.
- Core SHA256: `a7b42ecf55c6fa16a68872e1f444a38882049b7b4f1e7ba16041aaff77d36936`.
- Corrected probe SHA256: `2b0040d22f1df9d44248340d0adc5c453a2107faed18131edca8c1f654197ca3`.

No game, Steam, desktop, deployment or compute run occurred. No Editor/game process remained after cleanup; the controller lock returned to waiting for manual desktop unlock. Installed14ef76e returning-save/helmet acceptance remains the first game task.

## Remaining activation gates

Production observation/readiness is still missing. Future growth must honor **global IsWildfireEnabled** and current-world initialization, settled native services and valid current transport provenance. IsAutoDispatchEnabled's global-toggle coupling must not be mistaken for a Warden-only preference; the earlier read-only feasibility inference was corrected.

Actual accepted GPU clean ash→same native soil contact, production callback/binding, full world lifecycle and live save/reload remain separate evidence. The engine probe does not repeat the managed stacked-upper-plant, column-compaction or movement cases, and does not execute a complete game tick scheduler. It establishes the positive native component and callback-failure boundary without widening those claims.
