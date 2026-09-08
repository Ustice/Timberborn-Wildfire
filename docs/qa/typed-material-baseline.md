# Typed unowned material baseline

The material handoff can now reveal five exact zero-fuel definitions. This closes the previous bool-only
terrain/air limitation without changing fire, transport, material masks, command strides or receipts.
The native registry retains this desired material underneath every contributor. No production initializer
or environmental synchronization is activated by this change.

| Definition | Packed material | Companion material | Meaning |
|---|---:|---:|---|
| Empty | `0` | `0` | Open unowned air |
| SolidTerrain | `0x1000` | `1` | Physical terrain voxel |
| OpenSoil | `0` | `1` | Open native soil surface, Terrain material class |
| Water | `0` | `0x00c00008` | Unowned Water profile, suppresses-without-cleaning behavior |
| Badwater | `0` | `0x00b00009` | Unowned Badwater profile, tainted quality/source behavior |

`FireSimBaselineDefinition` exposes these immutable values and validates conversion from an existing
`FireSimMaterialDefinition`. A default value is Empty. Capacity, fuel, flammability, burning/history and
noncanonical profile variants cannot enter through this type. Baseline definitions carry no heat,
wetness, ash or soil contamination.

`SetBaseline(cell, expectedOwner, definition)` uses existing mode4 with incoming identity0/0. Expected
identity may be an exact owned pair or0/0 for an already-unowned cell. Existing `Remove(bool)` retains
its owned-only precondition and delegates to Empty/SolidTerrain. Core admission still verifies expected
host ownership, unique cell/source closure and full capacity. HLSL validates the same exact baseline
allowlist after its ordinary identity/mask validation, rejecting the entire batch before material writes.
An unowned update creates no known slot or archive. An outgoing owned slot retains its exact GPU archive
under the unchanged authority rules.

Material application retains destination heat and wetness through mask `0x0cf0`, companion ash-strength
and soil-contamination values through the unchanged complement of `0x01f0ffff`, and atmospheric state.
Revealing Water does **not** assign water band3. The initial projector may initialize wetness once;
later native environmental changes need explicit ordered input. Reveal never replays initial moisture.

## Native registry boundary

`TimberbornMaterialBaseline(grid, cells)` copies sparse per-cell material definitions with Empty fallback.
The new registry constructor takes this baseline; the existing solid-index constructor preserves its
old Empty/SolidTerrain behavior. No initial/current wetness, native inventory, burn state or liquid-contact
ledger lives in the baseline. Resolver output pairs the exact packed definition with its canonical profile.

`CreateBaselineRequest` only succeeds when no current native contributor wins. A hidden tree below a
removed building must reveal before unowned material can replace it. All original Guid/local-slot
bindings remain retained. Malformed baseline or rejected cross-owner overlap cannot partially mutate
registry state. Initial/native water projection and live baseline replacement remain separate work.
Existing callers deriving `Remove(bool)` from only the terrain bit must migrate to this typed request
before they can use richer baselines; otherwise OpenSoil/Water/Badwater would lose their profile.

## Shader meaning and limits

Packed Terrain1 permits burning and rejects smoke/steam movement targets. Companion Terrain class1
separately rejects that cell as a heat donor and permits ash landing. OpenSoil therefore allows incoming
smoke while retaining ash at the native walkable soil coordinate. These are current shader rules, not a
new physical model. Radius2 heat exchange and long-range atmospheric moves do not test intervening
voxels; water-class Terrain0 cells remain atmosphere-open. Wetness is a finite simulation band, not
native liquid volume or underwater fire immunity.

Actual native water-column contact remains separate from packed wetness. The legacy ash classifier's
Water>0 shortcut includes soil moisture/suppression and cannot establish real-liquid washout for the
owned path. No correction to that legacy native-effect path is included here.

## Validation and handoff

At source `63aeaed`, isolated suites pass **189 Core** and **1045 native** tests, zero failures/skips.
Ten focused Core tests prove canonical definitions, rejected profiles, ambient receipt preservation,
stale expectation rejection, rejected corrupt accepted receipts, and exact known/archive behavior.
Three native tests prove typed fallback, immutable inputs, hidden contributor ordering and failed
reconciliation. The managed backend checks the protocol/authority seam, not shader execution.

`BaselineShaderTests` builds with zero warnings. The controller selection is:

```sh
dotnet test tests/Wildfire.Shader.Tests/Wildfire.Shader.Tests.csproj --filter FullyQualifiedName~BaselineShaderTests
```

Three facts produce13 captures: all five owned reveals and an equivalent post-marker control; all five
unowned updates; ten invalid raw-field/identity cases which must reject atomically. The control compares
final atmospheric/cell/companion fields to detect a handoff transport reset. Invalid cases include fuel,
flammability, burning, capacity, burn history, ash/soil injection, bare Water without its profile policy,
invalid terrain/class pairing and a nonzero incoming target. Existing MaterialHandoffShaderTests remain
the regression group. **GPU execution was pending at this source handoff**; only the single controller
may run it. Full ordinary-backlog admission uses the separately validated capacity dependency.

Local logs: `/tmp/wildfire-typed-baseline-core-full.log`, `...-native-full.log`, `...-shader-build.log`.

Actual tools-disabled Claude geometry review completed terminal54932, exit0, is_error=false,
234643ms. Its report confirmed the baseline restriction and current geometry/transport limitations.
Source verification resolved its mask question and rejected blanket building insulation as an unchosen
policy. Exact prompt/response/process evidence and disposition are preserved in
`/tmp/wildfire-environment-baseline-review/`. This was a new geometry topic, not a retry of the refused
saved-body accounting review. No game, Unity, deployment or production activation was performed by
the implementation agent.
