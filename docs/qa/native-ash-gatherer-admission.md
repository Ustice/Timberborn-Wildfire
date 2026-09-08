# Native ash gatherer admission proof

Source audited: `dbe2b5f`. This is a test-only admission/UI-model proof; no production change was needed. Both installed faction Gatherer Flags already have a coherent route to the packaged ash priority.

## Proven native boundary

`NativeAshGathererAdmissionTests` reads the installed Folktails and Iron Teeth Gatherer Flag blueprints, their `Gatherable` resource group and output capacity (20), the common Berries good collection, and the packaged faction FertileAsh append plus common FertileAshField template append. It reconstructs the relevant native blueprint specs using those actual values. The template and good services contain a controlled two-option subset (ash and blueberries).

The tests execute the installed native `YieldRemovingBuilding` filter, `GathererFlag.InitializeEntity`, `GatherablePrioritizerDropdownProvider.InitializeEntity/SetValue/GetValue`, native saved-name lookup, and `SimpleOutputInventoryInitializer.AllowGoodsAsTakeable`. They prove:

- Both faction flags admit the ash template when its good is available and its resource group matches.
- The dropdown model lists **Fertile ash**, selects the exact native blueprint spec, and emits one priority-change event. This label comes from the good's plural display name, using the packaged English localization; the internal template name is `FertileAshField`.
- The native output policy declares FertileAsh takeable, not givable, with capacity 20.
- Removing the faction good or changing the ash resource group excludes it from gathering and saved-name resolution.

Four focused cases and the full native suite passed: **1,304 tests, zero failures or skips**. Command: `dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj --no-restore`.

This does not execute the game's blueprint patch/collection loader, the full output inventory initializer, a rendered UI Toolkit dropdown, real prefab lifecycle, worker motion, or save/reload. The fixture supplies native managed specs, service fields, a localization stub for the Nothing option, and a null icon wrapper; it does not pretend those are a running world. Actual SimpleOutput declaration policy is proven; actual inventory creation and worker delivery remain live checks.

## Controller checks

In each healthy disposable faction world, place/finish and staff the native Gatherer Flag with ordinary path/district access. Select **Fertile ash** in its native priority dropdown. Confirm that selection persists after saving/reloading. The flag must be enabled and its SimpleOutput inventory enabled with one unreserved FertileAsh capacity; an ordinary employed adult must have empty hands and no unrelated reservation.

Use an observed cool, clean ash field reachable from the flag. Do not infer collectible clean ash from a designation or a candidate count: require simulator ash, zero ash contamination, and the existing fertile-ash read model. `qa-ash-cell <index>` inspects the actual cell. The existing water/ash stimulus also adds water and can wash the test field out; it is not a dry clean-ash seeding command.

Observe the selected beaver's ash status and physical outbound/return journey, then one actual FertileAsh unit in the flag output. The resource-step receipt (`fertile_ash_collected_goods`) is per step, not cumulative; a later zero does not disprove an earlier collection. Verify finite collection, full-output waiting, and save/reload while carrying using copied saves. Shared resource recovery/holding and broad critical-needs recovery remain separate release limitations.

Native registration is common to both factions: the mod decorates GathererFlag with its ash workplace behavior and AdultSpec with the persistent ash executor/behavior. This supports ash gathering in both factions; it does not imply Folktails firefighting equipment or borrowed-duty suppression is complete.

## Separate fertilizer release gap

The coordinator independently verified that the current fertilizer designation singleton consumes nearby native ash before queuing `AddAsh` plus `SetAshContamination=0`, catches failures, and retries. Its tooltip describes worker application while the current service applies instantly and merges crop/forestry designations. That path has neither the harvest receipt nor the shared delivery commit boundary proven here. It is a separate worker/transaction design task, not evidence against gatherer admission and not fixed by this test-only change.
