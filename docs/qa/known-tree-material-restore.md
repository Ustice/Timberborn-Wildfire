# Known tree material and first-activation eligibility

Source: mandatory planner input `9eda183`, native composition observation `7775e5f`, and restoration/reveal regressions `d4daf25`. This is an unpublished restore and planning boundary, not ordinary runtime activation.

## Separate authority

The simulator's exact `(TargetId, SlotId)` known set determines whether material has existing history. Same active material is untouched; known inactive material uses its exact archive or captured active source. Restoring an exhausted slot never substitutes its profile's initial fuel. A registered but never-activated slot has no invented archive.

The desired planner now requires an explicit `freshEligibleOwners` argument. It copies and validates that input as a unique subset of canonical retained owners before backend callbacks. Only the never-known incoming-pair branch requires membership. A hidden, unverified slot can remain represented with no pending Fresh request. If its reveal requires Fresh, the entire plan rejects before any material upload or native effect; another known move in the same plan cannot commit separately.

This set is current native admission evidence, not saved history or a new owner registry. `TimberbornOwnedRestoreObservation.CaptureFreshEligibleOwners` derives a new read-only set after validating the same current observation. No session stores it, no persistence schema changes, and no all-allowed overload exists. Future execution must obtain/revalidate native facts and use this input within its shared resource-guard scope; a previously captured set is not a reusable startup permission.

## Bounded native-tree evidence

Complete restore now accepts an ordinary supported Tree with dead or disabled yield state only when current native composition proves the retained-wood route:

- Actual `TreeComponent` and `Cuttable`, with `RemoveOnCut=false`.
- The exact body-owned named `Cuttable.Yielder`, matching configured spec and captured declared role/good/amount, without duplicate named yielders.
- Neither the native `DeadCuttableYieldRemoverSpec` nor its runtime component occurs in the complete `AllComponents` list. Both exact types are resolved from the installed Cutting assembly. An absent runtime decorator alone cannot hide a configured removal policy.

This is not a species-name policy. Installed Pine supplies the positive test definition (Cuttable Log2). Native `LivingNaturalResource.Die` sets death and raises callbacks; it does not itself remove wood. The installed death-yield remover is attached only through its specific spec: the inspected Birch, Chestnut, Mangrove, Maple, Oak and Pine definitions lack it, while unsupported Succulent has it. A disabled Yielder's public amount can be zero while its raw native amount survives; the observation does not enable or rewrite it.

These exceptional retained trees are excluded from current Fresh eligibility. Dead/disabled Crop and Vegetation states, harvested leftovers, unfinished objects, unregistered exclusions and unsupported roles remain rejected. There is no automatic suspension, retirement, generation reset, partial-harvest policy or body-fuel conversion.

## Executed proof

Eight focused native tests use actual native ComponentCache lookup, an installed Pine Cuttable definition, actual Yielder initialization/getters, and the native specs/components. They prove the positive ordinary-tree composition, public-zero/raw-two preservation with zero yield callbacks, and rejection of spec-only removal, component-only removal, RemoveOnCut, missing TreeComponent, duplicate name, foreign yielder and changed declared amount. These are managed native composition fixtures; they do not execute the full template/GameObject death event chain.

Complete restore fixtures preserve paired encoding, saved body capacity, active fields and archived fuel0/3 for dead/disabled trees with supplied supported native evidence. No tree or inventory consequence is replayed. A changed evidence callback disposes the unpublished backend without poisoning the read guard. Generalized planner fixtures reexpose archives0/3 without Fresh eligibility and reject mixed known-move/unknown-Fresh batches before upload. The known-slot tests do not establish a new material-to-accounting scale.

Full suite after `d4daf25` and the independent explicit world-domain dependency `10a49bd`: **1,259 native tests passed**, zero failures/skips. Logs: `/tmp/wildfire-native-retained-tree-evidence.log`, `/tmp/wildfire-known-tree-focused.log`, `/tmp/wildfire-known-tree-full.log`. The earlier planner-only checkpoint passed25 focused/1,232 native tests in `/tmp/wildfire-known-material-planner.log` and `-full.log`.

## Remaining policy and runtime limits

At this checkpoint Fresh eligibility conservatively excluded a body with any disabled named yield. The later [living-tree correction](living-tree-first-activation.md) uses verified enabled positive Cuttable wood independently of its first-unripe or regrowing Gatherable. Native evidence identifies growth timers, not a calendar-season rule. Dead-tree Fresh and initial material policy remain separate.

Initial formation is also distinct: its existing provider excludes tree leftovers but does not capture death as an initial admission fact, and its compiler uses the selected native profile for fresh material independently of the accounting choice. Known-material restoration does not justify that initial deadwood/Fresh behavior. Explicit initial lifecycle evidence and material policy remain required before production activation; this slice adds no blanket tree gate and adopts no pending fuel-budget choice.

The current complete restore report is [OWNED4 staging](complete-owned-restore-staging.md). Generalized planner shader proof and earlier actual OWNED4 engine preparation remain valid at their recorded source scopes. This change adds no new licensed-engine execution, ordinary-step runtime binding, copied-save gameplay or full-colony claim.
