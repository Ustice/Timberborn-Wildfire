# GPU material handoff prototype

Isolated vertical prototype: typed whole-batch validation, shared coordinator authority, both simulator backends, dedicated GPU buffers, ordered shader apply and actual licensed shader fixtures. No native lifecycle caller is wired. Synthetic records in Core tests are protocol evidence only; engine fixtures read actual pre-simulation GPU receipts.

## Required behavior

One active material slot per cell. A slot is `(TargetId, SlotId)`: an entity ID plus a stable local-footprint slot ID. Two cells of one entity may retain different fuel/history. IDs are session tokens; future native registry persistence must map them to EntityId Guid/local coordinate. Do not use enumeration position or world cell as lasting slot identity.

Fresh activation requires a never-activated `(TargetId, SlotId)` pair. A different local slot of a known target may initialize from its current validated native definition. Every previously activated pair requires an available GPU archive or captured source cell; there is no fallback to original fuel. Native admission derives the pair from the current registry projection and checks complete simulator authority; saved bindings alone do not establish history. See [first-slot admission](first-material-slot-admission.md). Hidden material pauses with its GPU-authored remaining fuel/raw history. Current shader does not advance BurnHistory; the archive preserves those bits without inventing history from entity damage.

Handoff resets burning level but retains destination heat/water, all atmospheric fields, and companion deposited ash/soil contamination. Incoming material restores only fuel/static profile/history. Refresh preserves the entire live cell/companion, with profile matching and zero supplied fuel. No administrative material edit emits a burn-damage delta or ash. Only the dedicated receipts describe those edits, so existing delta capacity (grid cells plus ordinary command capacity) remains sufficient even for a full-grid handoff.

## Typed authority boundary

`FireSimMaterialHandoffBatch` sorts unique cells, binds expected/incoming target+slot, and rejects repeated active slots, repeated incoming slots, invalid/non-relinquished captured sources, duplicate source consumption and duplicate archive consumption. Admission checks whole-footprint capacity, grid bounds, known slots/owners and explicit available archives before any mutation.

`FireSimMaterialArchive` has no public raw-history constructor. Accepted validated GPU receipt data creates it. Refresh/still-active moved slots cannot also yield inactive archives. Outgoing archive access returns the same immutable object, not independent clones. Archive identity includes owner and local slot; capture token/cell identify its authoritative version.

The pure validation API accepts known slots and available archives to test the contract. Production admission goes through `FireSimStepCoordinator`, which owns those collections and the active-cell map, seeded from explicit initial ownership. Accepted transactions consume incoming archives, archive outgoing inactive slots and update active/known identities atomically. Rejected and not-admitted transactions, and failures before authority publication, do not publish those changes. A host callback failure after the accepted authority commit retains that published authority but poisons the session; it does not roll back or authorize replay. Attempt tokens are monotonically increasing and consumed immediately before upload, including an upload failure; a proven NotApplied failure may retry only with a new token. Missing/tampered receipts or later indeterminate failures block further ticks until a future reconciliation/reload boundary exists. True native deletion/retirement remains outside this prototype and must not be inferred from a material removal alone.

## Explicit buffer layouts

Ordinary commands stay 16 bytes. Delta rows now use 20 bytes to retain both originating target and local-slot ids; see [delta provenance](delta-slot-provenance.md). The material batch marker is four words: request offset0, opcode `0x80000000`, request count, transaction token. The tag is interpreted before ordinary cell-index/mask fields. This supersedes the unused isolated per-cell encoding experiment 836b088.

A request is 10 uints/40 bytes:

| Index | Meaning |
| --- | --- |
| 0 | Destination CellIndex |
| 1–2 | Expected TargetId, SlotId |
| 3–4 | Incoming TargetId, SlotId |
| 5 | Mode: Refresh0, Fresh1, Archived2, CapturedSource3, Baseline4 |
| 6 | Incoming material bits: fuel/flammability/terrain only |
| 7 | Incoming owner companion bits: class/capacity/history/ash-quality/contamination behavior |
| 8 | Pre-write source receipt index, or uint.MaxValue |
| 9 | Reserved zero |

A receipt is 10 uints/40 bytes: CellIndex, prior TargetId, prior SlotId, prior raw cell, prior raw companion, applied TargetId, applied SlotId, applied raw cell, applied raw companion, validation status. Header is 4 uints: token, Accepted 1/Rejected 2, count, first rejected index (uint.MaxValue on acceptance). Status 1 marks a valid row; 2 identity mismatch, 3 profile/refresh mismatch, 4 invalid material shape, 5 invalid captured source, 6 repeated source consumption, 7 destination bounds/order, 8 unknown mode. Pending/missing status 0 is never a receipt.

A read-only request buffer, writable receipt rows, and a per-cell slot-ID buffer are required, alongside the target-ID buffer. Receipt row zero reserves 40 bytes for the four logical header words (attempt token, accepted/rejected status, request count, rejected row or `uint.MaxValue`) followed by six zero words. Per-cell 40-byte receipts begin at row one. Usable request capacity excludes that header row: `N` requests allocate `N + 1` receipt rows after checked capacity/word-count validation. Each admission clears row zero before dispatch; an unexecuted or stale header cannot satisfy the current token validation. Native readback keeps the established flat-uint shape from offset zero and then separates the header and cell rows.

The acknowledged material marker remains the exclusive final command and is never rewritten. Ordinary and ash-command bytes and receipt writes are unchanged. Read-only requests and the removed separate header UAV keep `ApplyExternalChanges` at eight writable buffers. An earlier experiment storing the header in `ExternalChanges` caused Unity 6000.3's generated Metal to omit ash receipt stores; that design was rejected after the unchanged ash fixtures failed. The reserved row avoids that shared-buffer interaction. Request storage starts at one row, receipt storage at two, and both grow with the admitted batch size, bounded by grid capacity; no permanent full-grid 80-byte table is allocated just to run ordinary ticks. A material batch cannot be split into partial entity updates because a buffer is full.

## Ordered execution

1. Admit one marker plus complete material table after earlier queued ordinary inputs. Keep existing NotApplied/Indeterminate/Committed outcome semantics.
2. At the marker, capture all affected cells/owners/slots first. Validate every request and captured-source reference before any material write. Sorted unique destination cells and source consumption checks prevent double application.
3. Any validation rejection leaves every material cell unchanged; earlier ordinary inputs and later normal simulation can still run. If the rejected receipt exposes actual prior ownership different from the host expectation, the session is indeterminate and blocks subsequent ticks; ordinary rejection remains safe only while GPU and host identities agree. A GPU failure during writes is indeterminate and stops/reconciles instead of replaying.
4. On acceptance apply the complete table from pre-write captures/known archives, then acknowledge. Capture prior/applied state before normal simulation; read receipts alongside deltas and swap before host commit.
5. Validate all header/row identities, field masks and environmental preservation before a single host ownership/archive commit. New live material may have simulated once; do not write the handoff receipt back as a CPU state mirror.
6. Append the originating TargetId and SlotId to each GPU delta and retain both in readbacks. Queued old-slot fuel transitions and incoming-slot simulation transitions remain distinguishable, including when the native target is unchanged. The owned consumer must resolve retained origins and normalize complete per-slot transition chains before native mutation. Production legacy dispatch still needs replacement; readback identity alone does not activate the owned route.

## Engine fixtures

Use production encoded request words, and read prior/applied receipts before simulation. Preserve existing fixture JSON compatibility with optional material inputs/owner-slot fields. Actual tests must inspect both receipts and final simulator fields; they must not fabricate successful receipts.

- **Fresh owner, two slots:** old owner 1 slots 11/12 with fuel 3/0, raw history 5/9, nonzero heat/water/deposited ash/soil ; replace with owner 2 slots 21/22. Verify complete prior capture, full identities, preserved destination environment, burning reset and fresh input only for the genuinely new owner. The fixture uses one Tree profile over two slots; it does not prove structure/storage profile composition. Native registry composition is a separate boundary.
- **Hide then restore from actual receipt:** decode outgoing archives from the first real GPU capture, encode a second restore batch, preserve fuel 3/0 and history 5/9. Repeat a cycle; exhausted material remains zero. Consumed archive replay is rejected before admission.
- **Same-owner two-slot swap/rotation:** source receipts captured before writes transfer distinct fuel/history to opposite destinations while preserving each destination's environment. Neither moved slot appears in the inactive archive.
- **One stale slot in a multi-cell transition:** valid rejected header; all material cells unchanged, including the first otherwise-valid request. Reject malformed headers/partial or tampered receipts in the host without publishing state.
- **Unchanged refresh:** exhausted fuel/burning/history unchanged; incompatible profile rejected, no refill, no inactive archive minted.
- **Queued old-owner delta plus new-owner simulation:** earlier ordinary SetFuel/AddHeat and then handoff; verify each delta's originating ID through actual shader output and both readback paths. Administrative removal adds no damage delta. The managed listener fixture retains those IDs; native consequence routing remains explicitly unproven.
- **Removal and invalid mode:** original terrain/air cases clear ownership and archive prior state; unknown mode rejects the whole table before material writes. The later [typed baseline extension](typed-material-baseline.md) adds open soil/Water/Badwater and unowned updates with unchanged masks and authority.
- **Normal input compatibility:** previous water and ash receipt fixtures remain byte-identical and pass; full shader suite follows focused proof because shared apply/delta layouts change.
- **Failure boundaries:** upload/preflight/readback/commit/listener failures preserve admission outcomes; full capacity returns not-admitted, no partial transition; active/archive ownership transfers once only.

## Intentionally unresolved

No native event registry, placement mapping, save/legacy migration, dynamic resource/profile changes, or exact splitting of existing cross-owner mixed storage fuel. Archives require explicit known ownership. Wrong/missing retained state is rejected, never reconstructed from full initial fuel or aggregate entity damage. No game/deployment actions are needed for this prototype.

The shader trusts the typed host admission for global identity/alias/known-archive authority, while independently checking live expected identities, static profiles, sorted bounds and captured-source references. Raw fixture words intentionally bypass selected host checks to test GPU rejection; they are not a public native material-input API. Current native constructor fallback slot IDs are `cellIndex + 1` only to seed the unactivated session prototype. Existing importer TargetIds collide across providers: the durable native Guid/local-slot registry, coherent material projection and original-owner route must be published together before live activation. Complete snapshot capture and new-simulator restoration are now implemented and independently tested; see [snapshot evidence](material-snapshot-prototype.md). These do not by themselves validate full native world publication.

## Verified checkpoint

On 2026-09-07, isolated runtime/source `a1e64ff` passed all 34 licensed Unity shader cases (zero skips, 39 captures; 26 existing cases and 8 handoff cases), 151 Core/compute-contract tests, and 686 native .NET tests against the installed Timberborn assemblies. Native adapter build had zero warnings/errors. This includes a two-row raw uint readback using the same 40-byte structured-buffer count/stride shape as the native helper. Evidence: `/tmp/wildfire-material-handoff/REPORT.md` and its `gpu-full/capture-index.json` (local artifacts, not packaged assets).

Unity/game controller returned idle with no engine process; build lock released. No game launch, deployment, loaded-world, persistence, or native lifecycle activation is claimed.

The checkpoint capture label `material-fresh-composite` was subsequently renamed `material-fresh-owner`: it exercises one Tree profile across two slots, not structure/storage composite construction. This naming correction changes no fixture values, shader, or runtime behavior.

## Writable-buffer limit regression

At `d497c47`, the complete licensed suite passes 34/34 cases with zero skips and 39 captures, including all unchanged ash-collection cases. Every captured Metal log is free of UAV-limit and shader-loop warnings. The actual production native factory also passes its serialized hide/restore/re-exposure lifecycle and independently burns fuel 3 to zero before saving; re-exposure remains exhausted. Core passes 166 tests and the native suite passes 780 tests on this revision's integration base.

The device reports `supportedRandomWriteTargetCount=8` and `maxComputeBufferInputsCompute=31` (Apple M2 Pro, Metal, Unity 6000.3.6f1). These are separate limits. The shader now uses eight writable resources; the request buffer is read-only. No warning suppression or platform-specific alternate fire rules were added. This is GPU and native-simulator evidence, not live world activation.
