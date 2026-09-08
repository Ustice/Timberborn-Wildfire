# First activation of a native local slot

Fresh admission checks exact `(TargetId, SlotId)` membership. Having another active or archived slot with the same TargetId does not make a never-activated local slot retained material. Known pairs remain known for the complete saved timeline; zero fuel, removal, hiding and later re-exposure never authorize Fresh.

`IFireSimMaterialHandoffSimulator.IsSlotKnown` reads coordinator authority. Legacy history, an uncertain GPU step, capture and an in-progress step/commit throw instead of answering false. A new unpublished simulator restores known pairs together with active buffers, archives and attempted tokens. Attempt tokens are monotonic within that restored timeline, not globally across discarded unsaved histories.

`TimberbornNativeMaterialRegistry.CreateFirstActivationRequest(cellIndex, expectedGpuOwner, simulator)` accepts no incoming identity or material definition. It resolves the current winning native projection, uses its durable Guid/local-coordinate binding and current validated material, and rejects a known pair. It neither allocates bindings nor publishes GPU authority. This registry projection is a trusted captured native boundary, not a fresh EntityRegistry liveness check at request construction; the eventual lifecycle planner must validate and stage native changes before handoff. The coordinator still verifies `expectedGpuOwner`, uniqueness, capacity and whole-batch admission before upload. Only accepted GPU receipts add known pairs.

An initially hidden native slot can be bound and saved before first activation. WF2 permits these extra bindings; after restore it still requires a current native projection. First activation uses the definition at that moment, without a separate frozen birth profile or pending identity ledger. Native transforms preserve local-coordinate identity and cannot create replacement slots for known coordinates. New Guids receive distinct targets while old origin bindings and archives remain available.

## Proof boundaries

Managed tests cover a partially hidden two-slot owner, current definition at first activation, WF2 save between binding and activation, malformed/missing authority, zero-fuel archived rejection, new Guid origin separation, and actual native Blocks transforms for all four rotations and both flips. Core tests cover two new slots of a known target in one batch, duplicate/known pair rejection, and unavailable queries during capture, commit and uncertain steps.

The licensed shader fixture replaces two same-target slots with distinct new local slots while capturing separate prior fuel/history. The native snapshot probe additionally exercises production coordinator admission, publication, serialization/new simulator, known-pair Fresh rejection and archived restoration, while preserving an exhausted sibling. These are offline engine proofs; current world lifecycle activation and saved-game integration remain gated.

Known-slot profile changes, tree regrowth, dynamic storage accounting, terrain policy and legacy missing history are not addressed here. Current native projection authority must come from the actual initialized entity and local footprint adapter, not invented caller identities. No CPU fuel ledger or synthetic retained archives are introduced.

## Recorded validation

At `7e0f410`: Core 167/167 and native 825/825 passed. Licensed Unity 6000.3.6f1 on Metal Apple M2 Pro passed all 35 shader cases with zero skips (40 captures), plus the actual native simulator first-slot/save/restore and independent GPU burn-to-zero probes. No shader or UAV warning appeared; device limits remain eight writable targets and 31 compute-buffer inputs. Detailed local evidence is `/tmp/wildfire-first-material-slot/REPORT.md`. No game/deployment or live native lifecycle result is implied.
