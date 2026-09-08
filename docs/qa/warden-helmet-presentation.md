# Warden helmet presentation draft

Helmet-only source integration; **pose and full native rendering remain unverified**. Uses the existing WardenHelmet.IronTeeth mesh. No tank, wand, hose, coat, material replacement, animation override, cargo change or new visual save state.

## Source behavior

The common BeaverAdult blueprint appends one unique `Wildfire.WardenHelmet.IronTeeth` attachment, using the existing provider prefab, `#Head` parent, CreateInstantly=false and provisional zero position/rotation, unit scale. Existing attachment, outfit, carrying and animation declarations remain intact. The asset itself has not been recentered or resized to pretend a fit.

One AdultSpec `WardenHelmetPresentation` component lazily requests the native attachment only after settled initialization, when BehaviorManager currently owns WardenExecutor, its phase is Fetching/Approaching/Applying/AwaitingApplication/Returning, and the actor is alive. It observes each rendered frame, including paused loaded worlds, rather than relying on an interrupted executor to call Finish. Death/delete hide immediately. Idle, stale phase with foreign executor, or dead actors show no helmet. The helmet remains through an empty return. Native outfit and GoodCarrier toggles are never modified.

The component verifies one exact #Head under CharacterModel.Model before native creation, checks the returned native parent and requires imported renderers. It acquires one native visibility toggle and reuses it. Native attachment caching owns the instance; entity destruction owns its eventual disposal. Failures are caught only in presentation, logged once as `wildfire_warden_helmet status=unavailable` with beaver Guid, attachment ID, parent and exception, and disable further creation attempts. No arbitrary children are destroyed and no fallback mesh is substituted. Gameplay continues independently.

Only the helmet provider descriptor now starts inactive. Actual installed IL establishes this order: optimizer/import → native instantiation → pose/scale → EntityMaterials.AddMaterials → MaterialLightingRenderers.CollectRenderers → TemplateAttachment construction. Constructor stores references only. Native optimizer, material and lighting scans include inactive children. Bindito InstantiateInactive records clone activeSelf, injects under an inactive object, then Instantiate restores that recorded state. A visibility toggle ultimately calls GameObject.SetActive with the AND of all votes, so Show can activate the inactive clone. This source evidence does not substitute for observing the real engine pipeline; a failure after a native partial import can leave an inactive cached/child artifact until native ownership teardown, and no retry is attempted.

## Executed tests

Six focused tests and **1,316 full native tests passed**, zero skips.

* Actual installed JsonMerger merges the shipping adult blueprint with our #append patch; every original attachment and unrelated declaration remains exactly equal, and the added helmet is unique and lazy.
* Actual native TemplateModule/TemplateInstantiator discovers one helmet presentation decorator beside one Warden executor.
* Actual native visibility toggle methods/events verify lazy one-time creation, all owned sortie phases, ownership loss without Finish, returning without water quantity dependence, repeated cleanup and reacquisition with the same toggle.
* Actual component PostInitialize creates no gear; death-handler and Delete calls hide existing presentation idempotently.
* Failed creation reports once; a native toggle subscriber that throws after Show changed IsVisible is followed by Hide, with no repeated writes/imports.

Fixtures do not create a real adult GameObject or execute live BehaviorManager/paused Update scheduling. The lifecycle predicates are supplied to the transient state helper, whose output uses actual native toggles. The full component's native liveness/parent/material path and rendered fit are acceptance work below. No engine run occurred for this source change.

The existing isolated provider probe now additionally asserts that only the helmet descriptor is inactive and clones preserve activeSelf. Its previous 6,020-triangle/import/reset result used stand-in materials; these new assertions are not yet executed and do not establish full game materials.

## Controller acceptance using the full native game pipeline

Use the existing sole live-QA controller and a copied/disposable populated Iron Teeth save. Deploy the reviewed source through the normal build/deploy route. No new debug injection, stand-in material repository, replacement character asset or controller path is required.

1. Start with an ordinary worker/hauler and an idle Warden. Neither should wear our helmet; native carried goods and outfit remain visible. Confirm startup has no asset/ref/import errors.
2. Reuse the established Warden water/refill scenario in [Warden native QA](warden-native-prototype.md). With QA development access already enabled, existing `qa-readiness --wait=6 --require-advanced-tick` and `qa-delta-stimulus tree --wait=6 --require-advanced-tick` commands can provide the same fire stimulus. Keep the station within the returned target's range; do not assume the nearest tree was selected.
3. When `wildfire_warden` reports an owned response, inspect the actual actor. This exercises the real asset loader, native material collection/atlas, OptimizedPrefabInstantiator, TemplateAttachments and renderer/highlight registration. Require visible helmet with native materials, no pink/missing mesh, and no helmet-unavailable warning. An error means this draft failed presentation acceptance even if the worker still moves.
4. Record front/side/rear/above views at idle/preparation, walking/running and stairs. Check ears, eyes, crown, jaw and neck clearance and that it follows the head. Zero-offset/unit-scale is an explicit provisional pose: measure and revise the attachment transform if the real fit fails. Do not approve merely because 1,812 source triangles imported.
5. Follow application and the empty return; helmet remains until sortie completion. Interrupt via ordinary station pause/unemployment/needs behavior; once manager ownership ends, it must disappear even if the saved Warden phase remains non-Idle. Resume ordinary hauling and confirm original goods/outfit/animation unchanged.
6. Save/reload an owned phase and Idle with native cargo; pause immediately after load. Visibility must match restored native ownership without advancing fire time, with no duplicate helmets or visual persistence component. Confirm original cargo and Warden equipment quantities remain unchanged by rendering.
7. On disposable actors, validate native hiding/inside/level visibility and death/delete: no floating helmet or retained visible orphan. Follow normal actor selection highlighting. Failures in these cases block completed wearable claims.

The existing source-only isolated probe may separately verify inactive-wrapper assertions when the controller is idle, but it is not a substitute for steps 2–7 with real native materials. Full sprayer fit/animation and coat direction remain separate work.
