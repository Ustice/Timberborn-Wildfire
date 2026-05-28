# Native API Support Requests

This catalog lists Wildfire calls into Timberborn native APIs that are currently non-public, reflection-only, or type-name-based. It is intended as a Mechanistry-facing support inventory: each entry describes what Wildfire is trying to do, the current unsupported call, and the public support that would remove the brittle dependency.

## Summary

- Highest-risk area: structure burn rollback, because it changes finished buildings back into construction-like states, preserves runtime settings, and interacts with district/build/construction internals.
- Gameplay consequence area: explosions, detonators, tunnels, and soil contamination need public effect APIs so Wildfire can reuse native consequences without reflection.
- Work-behavior area: fertile ash collection needs public scheduling/priority and animation hooks.
- Visual/read-model area: burned natural-resource visuals need public state/model refresh hooks instead of probing private fields or invoking hidden refresh methods.
- Beaver behavior area: smoke exposure needs a supported way to temporarily adjust worker speed.

## Structure Burn Rollback

### Enter Finished Buildings Into Unfinished State

- Code: `src/Wildfire.Timberborn/Consequences/TimberbornStructureBurnDamageRollback.cs:773`
- Current call: reads `BlockObject._blockObjectState`, finds `EnterState(...)`, parses the private state enum value `Unfinished`, and invokes it.
- Why Wildfire uses it: severe structure fire damage should roll a finished building back into an unfinished/repairable state rather than only applying a cosmetic scorch.
- Requested support: a public operation on `BlockObject`, construction, or building lifecycle services that can transition an existing finished building into a construction/repair state with native bookkeeping.

### Rebuild District Centers Without Stale District State

- Code: `src/Wildfire.Timberborn/Consequences/TimberbornStructureBurnDamageRollback.cs:976`
- Current call: finds `Timberborn.GameDistricts.DistrictCenter` by full type name, reads `_districtService`, reads `_districtUpdater`, and invokes `ProcessRegularChanges(null)` and `ProcessInstantChanges(null)`.
- Why Wildfire uses it: burning or rebuilding a district center needs native district removal/recreation state to flush immediately enough that the save does not keep stale references.
- Requested support: a public district lifecycle API for rebuilding/removing district centers, or a public "flush pending district topology changes" operation with a documented argument contract.

### Reset Construction Progress After Fire Damage

- Code: `src/Wildfire.Timberborn/Consequences/TimberbornStructureBurnDamageRollback.cs:1101`
- Current call: reads `ConstructionSite._constructionSiteBuildTimeCalculator`, invokes `GetConstructionTimeInHours(ConstructionSite)`, then invokes `ConstructionSite.SetBuildTimeProgress(float)`.
- Why Wildfire uses it: a burned structure should retain some construction progress when returned to a repairable unfinished state, instead of becoming fully new or fully complete.
- Requested support: a public construction-site progress setter that accepts normalized progress or build-time progress, plus a public construction-time query.

### Disable Recoverable Goods On Burned Structures

- Code: `src/Wildfire.Timberborn/Consequences/TimberbornStructureBurnDamageRollback.cs:1397`
- Current call: finds `Timberborn.RecoverableGoodSystem.RecoverableGoodProvider` by full type name and invokes `DisableGoodRecovery()`.
- Why Wildfire uses it: when fire consumes structure inventory/materials, native demolition/recovery should not later duplicate those goods.
- Requested support: a public recoverable-good provider API for disabling or reconciling recoverable resources after external damage.

### Preserve Manufactory Runtime Settings

- Code: `src/Wildfire.Timberborn/Consequences/TimberbornStructureBurnDamageRollback.cs:1407`
- Current call: finds `Timberborn.Workshops.Manufactory` by full type name, reads `CurrentRecipe`, `FuelRemaining`, and `ProductionProgress`, invokes `SetRecipe(...)`, and writes those runtime properties back after rebuild.
- Why Wildfire uses it: fire damage should interrupt and damage buildings without silently wiping player-selected production configuration.
- Requested support: a public snapshot/restore or rebuild-preserve contract for workshop runtime settings.

### Preserve Inventory Policy Across Rebuild

- Code: `src/Wildfire.Timberborn/Consequences/TimberbornStructureBurnDamageRollback.cs:1536`
- Current call: reads private `Inventory._ignorableCapacity` and `Inventory._goodDisallower`, then invokes `Inventory.Initialize(...)` to restore allowed goods, IO flags, capacity policy, and disallowers.
- Why Wildfire uses it: when a structure is rebuilt as unfinished, Wildfire needs to preserve native inventory policy while separately applying fire-driven stock loss.
- Requested support: public inventory policy snapshot/restore APIs, or public setters for capacity/allowed-goods/IO/disallower configuration.

## Explosions, Detonators, And Tunnels

### Trigger Native Blast Radius Consequences

- Code: `src/Wildfire.Timberborn/Consequences/TimberbornExplosiveInfrastructureConsequences.cs:154`
- Current call: invokes public `ExplosionOutcomeGatherer.GetAffectedTilesPerRadius(Vector3, float)` by reflection, then invokes non-public `ExplosionService.ProcessAffectedTiles(...)`.
- Why Wildfire uses it: explosives or explosive goods touched by fire should use native blast effects instead of approximating terrain/building damage independently.
- Requested support: a public explosion service method that accepts a center/radius and applies the same native affected-tile processing.

### Find And Disarm Detonators

- Code: `src/Wildfire.Timberborn/Consequences/TimberbornDetonatorFireSafetyConsequences.cs:230`
- Current call: resolves `Timberborn.AutomationBuildings.Detonator` with `Type.GetType(...)`, uses generic block-service lookup via reflection, then invokes `Disarm()` with public-or-non-public binding flags.
- Why Wildfire uses it: fire near detonator controls should disarm them as a native safety consequence.
- Requested support: a public detonator/control component contract or service for finding and disarming detonator-like controls at coordinates.

### Find And Explode Tunnels

- Code: `src/Wildfire.Timberborn/Consequences/TimberbornTunnelFireConsequences.cs:269`
- Current call: resolves `Timberborn.Explosions.Tunnel` with `Type.GetType(...)`, uses generic block-service lookup via reflection, invokes `Explode()`, and reads `BottomLevel`.
- Why Wildfire uses it: tunnel fire consequences should reuse native tunnel collapse/explosion behavior.
- Requested support: a public tunnel component contract or service for looking up tunnel objects at coordinates, reading their level metadata, and triggering native explosion/collapse.

## Ash And Contamination

### Update Soil Contamination From Tainted Ash

- Code: `src/Wildfire.Timberborn/Ash/TimberbornAshWorldEffects.cs:106`
- Current call: probes `ISoilContaminationService.UpdateContamination(Vector3Int, float)` with public-or-non-public binding flags and invokes it when available.
- Why Wildfire uses it: tainted ash should poison soil through Timberborn's native contamination field instead of maintaining a detached Wildfire-only state.
- Requested support: a public soil contamination mutation API with documented coordinate, range, clamping, and update-notification behavior.

### Prioritize Fertile Ash Work Behavior

- Code: `src/Wildfire.Timberborn/Ash/TimberbornAshWorldEffects.cs:923`
- Current call: reads private `Workplace._workplaceBehaviors` and moves Wildfire's behavior to the front of the list.
- Why Wildfire uses it: fertile ash collection should participate in native workplace behavior selection but needs to run before generic gathering behavior steals the same worker opportunity.
- Requested support: public workplace behavior priority/ordering registration, or a supported way for mods to declare behavior precedence.

### Drive Native Working Animation During Custom Harvest

- Code: `src/Wildfire.Timberborn/Ash/TimberbornAshWorldEffects.cs:1000`
- Current call: discovers `Timberborn.TimbermeshAnimations.IAnimatorController` by interface full name, then invokes `HasParameter(string)` and `SetBool(string, bool)`.
- Why Wildfire uses it: fertile ash harvesting should visually look like a native working task while using Wildfire's custom harvest executor.
- Requested support: a public animation facade for behavior executors, or a public component/interface reference for setting standard work-state parameters.

## Natural-Resource Burn Visuals

### Detect Leftover/Stump State

- Code: `src/Wildfire.Timberborn/Visuals/TimberbornRuntimeBurnedTextures.cs:300`
- Current call: reads `Cuttable._leftoverModel` to detect whether a natural resource is in leftover/stump state.
- Why Wildfire uses it: burned texture application should avoid treating leftover models as active burnable fuel visuals.
- Requested support: a public `Cuttable`/natural-resource property that reports leftover, stump, cut, or regrowth visual state.

### Drive Native Natural-Resource Lifecycle And Model Refresh

- Code: `src/Wildfire.Timberborn/Visuals/TimberbornRuntimeBurnedTextures.cs:94`
- Current call: invokes `WateredNaturalResource.StartDryingOut()` with public-or-non-public binding flags for trees and crops.
- Code: `src/Wildfire.Timberborn/Visuals/TimberbornRuntimeBurnedTextures.cs:216`
- Current call: invokes `GoodStack.DisableGoodStack()` with public-or-non-public binding flags after taking stack inventory.
- Code: `src/Wildfire.Timberborn/Visuals/TimberbornRuntimeBurnedTextures.cs:320`
- Current call: finds `Timberborn.NaturalResourcesModelSystem.NaturalResourceModel` by full type name and invokes `ShowCurrentModel()` with public-or-non-public binding flags.
- Code: `src/Wildfire.Timberborn/Visuals/TimberbornRuntimeBurnedTextures.cs:849`
- Current call: invokes `GatherableYieldGrower.RemoveYield()` with public-or-non-public binding flags.
- Why Wildfire uses it: when fire kills, dries, scorches, or leaves behind crops/trees, native visual models must refresh so the player sees the right state.
- Requested support: public lifecycle and refresh methods for drying, disabling stacks, removing yield, and showing the current natural-resource/crop model after external lifecycle changes.

## Beaver Smoke Exposure

### Temporarily Adjust Worker Speed

- Code: `src/Wildfire.Timberborn/Beavers/TimberbornBeaverFieldBehavior.cs:365`
- Current call: gets `Worker.WorkingSpeedMultiplier` with public-or-non-public binding flags and writes it during smoke/choking effects.
- Why Wildfire uses it: beavers exposed to smoke should cough/choke and work more slowly while the exposure persists, then restore their prior speed.
- Requested support: a public modifier stack for worker speed/status effects, or a documented property/method for temporary speed multipliers with restoration semantics.

## Public Reflection Worth Replacing

These calls are not strictly non-public, but they are still brittle because they depend on type-name strings or reflection to access public generic APIs.

- `src/Wildfire.Timberborn/Consequences/TimberbornDetonatorFireSafetyConsequences.cs:298`: generic `IBlockService.GetObjectsWithComponentAt<T>` invocation where `T` is a type resolved by name.
- `src/Wildfire.Timberborn/Consequences/TimberbornTunnelFireConsequences.cs:337`: same pattern for tunnel lookup.
- `src/Wildfire.Timberborn/Consequences/TimberbornTunnelFireConsequences.cs:371`: public `BottomLevel` read by reflection because the tunnel type is not referenced directly.

## Mechanistry Contact Framing

Wildfire is trying to stay adapter-only: the fire simulation remains host-agnostic, while Timberborn-specific consequences reuse Timberborn-native lifecycle, construction, district, work, explosion, contamination, and visual systems. The current non-public calls are not attempts to bypass gameplay rules; they are places where public modding APIs do not yet expose the native operation that best matches the player-facing consequence.

Useful umbrella ask: "Can Mechanistry expose supported modding APIs for external world consequences: damage or rollback a finished building into repair/construction state, apply native explosion/tunnel/detonator consequences, mutate soil contamination, schedule custom workplace behaviors with priority, refresh natural-resource visuals, and apply temporary worker-speed/status modifiers?"
