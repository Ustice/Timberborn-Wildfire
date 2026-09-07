# Native shoreline withdrawal and bucket conversion

This is a test-only resource feasibility slice. It does not select bell placement, donor policy, a designated intake, source safety/contamination policy or production suppression. It does not add a WaterInput, firefighter job or runtime API.

## Tested native boundary

The installed WaterGoodToWaterAmountConverter converts one Water good to **0.2 native water-volume units**. That is unrelated to a FireSim water band.

One shared native WaterInput is a viable authoritative buffer. DemandCleanWaterAmount may enqueue a future withdrawal and returns only the buffer already available. RemoveCleanWater synchronously debits that native float. WaterInput.Save/Load persists clean and contaminated buffered amounts. It has no per-beaver reservation API and its public debit does not clamp overdraw; the caller must validate finite positive volume and recheck sufficient native buffer immediately before debit.

The proposed bucket conversion is a short synchronous transaction:

1. Validate physical arrival/source eligibility and exact source ownership; validate an empty private native bucket inventory with capacity for Water1.
2. Demand the native volume, wait if the already credited buffer is insufficient. A queued demand is not cargo or a completed withdrawal.
3. Under the same NativeResourceCoordinator guard used by runtime Save, recheck source/buffer/capacity, call RemoveCleanWater(0.2), then private Inventory.GiveProduced(Water1), then commit the loaded worker phase.
4. Native inventory remains the authoritative loaded amount. Subsequent application uses the existing suppression commit protocol; this test does not queue suppression.

Two firefighters share the same input and perform this transaction sequentially; they do not retain separate promises against the buffer. No additional volume ledger or persistent per-worker river-water reservation is needed. A cancellation before conversion creates no product; already withdrawn water may remain in the shared native buffer. Ordinary death/deletion is not refunded.

## Coordinate ownership remains an implementation prerequisite

Native WaterChangeService.GetWaterChangeUnsafe reads a coordinate dictionary without consuming it. Native WaterInputService.RegisterWaterInput simply adds to a List; its Tick copies the full coordinate receipt into every registered input at that coordinate. Two WaterInput instances are therefore unsafe at one coordinate, even if every individual debit is correct. Never attach independent inputs to moving firefighters or share a manufactory's input without a proven resource-ownership contract.

Checking the native input list only when filling is insufficient. The tests reproduce one0.2 credit being copied into two inputs, a competing consumer using one copy and leaving, and the remaining input passing a later uniqueness check to create a second Water good. The test intentionally demonstrates a flawed admission-only approach; it is not production code.

The minimum source contract is **one stable, exclusive input owner for the entire request/credit/buffer lifetime**. Native public APIs provide no coordinate lease. A future implementation must either establish exclusion through proven native placement/occupancy semantics or track all relevant native input registration/coordinate lifecycle changes and reject a source whose credit history became ambiguous. Such tracking is ownership state, not a second water-volume ledger. A transient collision cannot simply be forgotten when its competitor disappears or the save reloads. The subsequent [native credit-boundary fixtures](native-water-credit-boundary.md) establish a test-only scheduler observation seam. Production source ownership, marker persistence and player-facing intake policy remain unimplemented.

Avoid an arbitrary per-frame list scan being presented as that proof. Native fixed input coordinates are derived from BlockObject.TransformCoordinates; pipe inputs can change coordinates. Competing native producers must be considered as well as other firefighters.

## Pending native credit and save

Native UpdateWaterChangesTask clears the previous receipt dictionary and writes actual completed removals for the new water job. WaterInputService credits these during a later singleton tick. TickableSingletonService.TickAll finishes the previous parallel work, ticks singleton consumers, then starts new parallel work. Normal save FinishFullTick completes entity buckets and force-finishes parallel work; it does not run another WaterInputService credit tick. The removal dictionary is not persistent; WaterSimulator.Save stores water columns/outflows, while WaterInput.Save stores only its credited buffer.

Therefore a completed removal may be saved after leaving the river but before entering an input buffer. On reload that uncredited withdrawal can be abandoned. **This is possible water loss, not phantom firefighter cargo.** A waiting worker reloads still waiting, creates no Water good and receives no invented refund or replayed withdrawal credit. This follows the native pump pipeline rather than changing global water simulation/save behavior. Exact river loss in a real save was not measured here.

The gameplay cost is at most the uncredited native withdrawals actually completed at that boundary; it can scale with outstanding demands, so a later shared source should avoid every waiting worker repeatedly issuing redundant prefetch requests. No persistent claim should promise those pending amounts. This is a rate/admission concern, not a reason to duplicate native buffered stock.

During buffer→inventory conversion, a native inventory event or later phase update can throw after stock changes. The shared guard must enclose both native operations and the phase change. Tests prove in-flight save rejection, indeterminate state after such failures and refusal of a speculative retry. No refund is attempted.

## Evidence

Ten focused tests use actual installed WaterInput, WaterInputService, WaterChangeService, fixed-coordinate objects, WaterGoodToWaterAmountConverter, native Inventory/GoodRegistry and native EntitySaver/EntityLoader:

- One completed0.2 coordinate credit permits exactly one of two sequential empty buckets; an already loaded bucket cannot fill again.
- Duplicate native inputs copy the same receipt; duplicate registration/unregistered source fails the local admission check.
- A removed competing input demonstrates why the local uniqueness check alone is insufficient.
- Native buffered credit and completed debit survive native in-memory Save/Load.
- Partial clean credit or contaminated-only credit creates no clean bucket.
- Pending uncredited removal is absent from native input Save/Load, with no cargo/refund invented.
- Native stock-event and post-give phase failures retain the actual mutation, poison save safety and prevent replay.

The fixture supplies a completed native receipt dictionary value; it does **not** execute the fluid simulator to measure a river withdrawal. It constructs actual native managed resource objects, not beavers/placed buildings or Unity GameObjects. It does not prove source physical reachability, live input registration, full game save/reload, persistent bucket component integration or final Folktails jobs.

Local audit evidence is in /tmp/wildfire-shoreline-proof: water-change.il.txt, update-water.il.txt, converter.il.txt, fixed-coordinates.il.txt, water-save.il.txt, singleton-tick.il.txt, bucket-service.il.txt and native caller scans. The existing save capture audit remains /tmp/wildfire-responder-feasibility/save-capture-review.md.
