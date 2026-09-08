# Native colony body classification

Source: `c2e9ffc`, based on `1f1c9d1`. Read-only colony audit and managed native proof, 2026-09-07. Production owned-world activation remains off.

## Implemented boundary

Initial and retained capture classify an actual `Stockpile` first, then an actual `Building`, before functional infrastructure names. Thus a pump, shaft, or GearWorkshop is a physical constructed body with its existing exact construction cost and footprint. This does not classify every infrastructure-shaped object as constructed: native NaturalDam and Slope have no Building and remain explicitly unsupported Infrastructure.

`TimberbornMaterialPart.Vegetation(specId)` requires a known bush profile and preserves its exact fuel/flammability with material class Vegetation. The initial compiler accepts Shape.Vegetation while keeping physical Resource and canonical Crop consequence family. BlueberryBush and Dandelion retain fuel1 with flammability1 and2 respectively; they do not receive the generic vegetation source's fuel10/flammability3 or the Crop material class. Missing/wrong-category profiles reject. The generic legacy vegetation source is unchanged.

This is new captured-definition formation, not a migration. Existing Infrastructure-family prototype definitions remain rejected by the complete compiler/witness path. No saved family keys, body capacities, native quantities or material history are rewritten. Native-resource accounting selection and all partial-yield/generation gates are unchanged.

## Proof

- Focused capture/classification/compiler tests: **45 passed**, no skips.
- Full native suite: **1,135 passed**, no skips.
- `NativeBodyClassificationTests` seeds real native component-cache lookups from these installed root templates: GearWorkshop in both factions; WaterPump.Folktails and DeepWaterPump.IronTeeth; PowerShaft in both factions; SmallWarehouse.Folktails; common Path; NaturalDam; Slope; BlueberryBush; Dandelion. The Ironteeth basic pump is not named WaterPump.IronTeeth.
- `InitialConstructionCaptureTests` reads actual installed costs through native BuildingSpec getters, including GearWorkshop (Log15/Plank25), WaterPump (Log12), and both shafts (Log1), and proves copied costs are not changed by later spec mutation.
- `InitialVegetationCompilationTests` initializes actual native named Yielders using installed declarations, reduces only fixture setup quantities (Blueberry3→2, Dandelion1→0), then exercises read-only capture, compiler, registry, owned formation and static witness capture. Native raw/initial quantities remain unchanged and no YieldAdded/YieldDecreased callbacks fire. Physical Resource/Crop-family identity, per-slot placement, exact Vegetation profile and declared witness quantity remain distinct.
- Existing transformed sparse-footprint tests continue to cover native rotation/flip/local-slot identity; classification does not change that projection API.

These are managed native API/component-cache fixtures. They do not instantiate the listed complete native templates or run GameObject liveness, full-world capture, game save/reload, gameplay effects, or a new shader run. Independent [typed baseline GPU proof](typed-baseline-gpu-proof.md) does not imply colony formation coverage.

Commands:

```sh
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj --filter 'FullyQualifiedName~NativeBodyClassificationTests|FullyQualifiedName~InitialVegetationCompilationTests|FullyQualifiedName~InitialConstructionCaptureTests|FullyQualifiedName~InitialNativeMaterialCaptureTests|FullyQualifiedName~InitialBodyCompilationTests'
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj --no-restore
```

## Installed-template audit and remaining release gaps

The audited installed `Modding/Blueprints.zip` SHA-256 is `ac4e47860ddd8a133c35ac2610dbdb769abace24355089e54988142ce90d9521`. The source archive is under the installed Timberborn app's `Contents/Resources/Data/StreamingAssets`. Enumeration reads root TemplateSpec+BlockObjectSpec documents, not child models.

It contains 371 block templates, 319 with BuildingSpec. The two faction building collections reference 316 distinct templates: 314 have DevModeTool=false, of which 29 lack an exact WildfireBurnableSpec profile; two are dev tools. These counts are archive scope, not instantiated save membership or proof all feature-gated templates are enabled. Of 81 names matched by the previous infrastructure heuristic, 79 have BuildingSpec; GearWorkshop is a concrete misleading `Gear` match. NaturalDam and Slope are the two nonbuilding matches.

| Priority | Remaining concrete gap | Required boundary |
|---|---|---|
| Basic constructed colony | Both DistrictCenters, Gatherer/Lumberjack/Scavenger flags, common Path lack exact profiles; centers/flags/Path have explicitly empty construction cost | Native BuildingSpec determines physical construction; material/accounting fallback is a separate deliberate policy. Do not manufacture zero fuel or generic wood from free construction |
| Damage units | SmallWarehouse occupies one cell and costs Log3; cost-derived capacity36 cannot be reached by curated fuel2 or even packed maximum15 | Resolve normalized simulated/body units versus explicit conversion before cost fallback; do not clamp capacity or invent a second fuel ledger |
| Exact stock roles | All 46 Manufactory templates expose a dedicated native Inventory, not SimpleOutputInventory; capture currently omits it | Capture exact same-owner role once; prove unreserved input/output consumption and native resource-counter semantics before effects |
| Recovered goods | RecoveredGoodSystem.RecoveredGoodStack implements IGoodStackInventory but is not GoodStackSystem.GoodStack | Separate standalone inventory-backed owner/role, exact Guid liveness, native stock authority and settled retirement/detach; harvest stacks remain on their plant owner |
| Natural definitions | Common natural collection includes Succulent (native TreeComponentSpec, backward alias Cactus), with no current profile | Explicit native natural definition; no unknown-profile omission |
| Map objects | NaturalDam, Slope, ruins, Thorns and map stockpiles lack complete owned material routes | Explicit nonbuilding physical/material representation and all local contributors; editor-only placement does not make existing objects absent from gameplay |
| Environmental category | Aquifer, WaterSeep and BadwaterSeep have native WaterSourceSpec but escape current name-only source classification | Component-verified environmental source classification, retaining independent physical contributions where applicable |

Constructed bodies should eventually use exact native static facts for a reviewed material/accounting fallback, with curated exceptions for intentional gameplay choices. A power/water/path function must not create a second body account for the same Guid. This slice deliberately makes no such fallback or normalized damage policy.

Native cost weighting currently values Log at12, while packed fuel is at most15 per cell; whole-body damage sums accepted fuel-loss rows directly. The one-cell warehouse counterexample is therefore an accounting-units issue, not fixed by additional profile names. One candidate is a single immutable normalized simulated/body budget, keeping exact native costs as static/reconstruction facts. Preserving cost units instead requires an explicit immutable conversion and cumulative rounding semantics. The same-owner structure+stock scalar composite cannot losslessly distinguish structure from inventory fuel; that limitation must be addressed by the chosen policy. Neither alternative is adopted here.

Manufactory's installed initializer creates a dedicated named Inventory from recipe goods, adds public output, and assigns it through InitializeInventory. ProcessedGoods, FuelRemaining and production progress are not additional physical stock. Existing storage consumption reads UnreservedTakeableStock, so adding the capture role alone would not prove input-goods combustion. Native TakeConsumed subtracts stock before emitting callbacks; use the same resource mutation guard and actual removed-quantity receipts, with no refund/replay after an uncertain callback.

Recovered piles also have native expiry, movement/merge and deletion. Native merging uses a produced-goods deposit, which must not be adopted as a conserved cargo-transfer primitive. Exact inventory burning is separate from creating piles or changing cargo policy. Preserve reservations, retire only after exact Guid absence, and do not infer ownership/history transfer from a nearby replacement pile. Multiple genuine inventories on one owner require one shared deterministic damage/credit budget; cloning the budget per inventory would double-spend it. The current compiler keeps that case explicitly unsupported.

Full audit extraction and exact native IL were recorded under `/tmp/wildfire-colony-family-audit`; this document preserves the material findings independently of those temporary artifacts. No new Claude call was made for this source-fact audit.
