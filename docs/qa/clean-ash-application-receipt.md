# Conditional clean ash application prerequisite

This is an inactive simulator/native transaction capability. No worker, designation tool,
maintenance policy, intake, or legacy application path is enabled by this change.

## Contract

`TryApplyCleanAsh` offers exactly one unit with an explicit limit of 1..3. A full ordinary
upload batch returns null without stepping or retaining the offered command. Otherwise the
GPU evaluates the application after earlier queued inputs and before ordinary simulation.
The paired result carries the step plus its application receipt, including valid rejection.
Only an exact `Applied / Added=1` receipt invokes the host callback. Rejection order is:
invalid ash landing surface, ash contamination, then quantity at/above the supplied limit.
The landing predicate is the simulator's existing predicate; class Terrain open soil is
valid even when the packed solid-terrain bit is zero. Actual ash remains capped at 3.

At the application stage, a rejected command leaves all fields unchanged. Acceptance changes
only ash quantity; it does not clear ash taint, soil, smoke, steam, wetness, or source flags.
Subsequent simulation can independently move, contaminate, absorb, or decay ash. Neither the
final field nor a `CellDelta` is an application receipt.

The exclusive 16-byte command uses SetMask bit12. AddFields bits25–26 are the explicit limit,
27–28 actual added quantity, 29 GPU validity, and 30–31 outcome (Applied0, Full1, Tainted2,
InvalidSurface3). Uploads contain only the limit; any uploaded receipt bit invalidates the
request. Collection retains its existing exclusive bit11 and unchanged decoding. Both use
its existing command readback backend: Unity's abstraction reads **one structured element**
and obtains four words; native `ComputeBuffer.GetData` reads one 16-byte element. No new
buffer or ninth UAV is introduced.

Generic RegisterChange and unconditional step input reject application commands. Complete
snapshot validation also rejects them from pending ordinary inputs. OWNED codecs still
serialize only validated ordinary fields: no pending application, new payload field, or
receipt may be silently persisted or replayed as an ordinary command.

Missing validity, altered identity/limit/command fields, and inconsistent outcome/quantity
are indeterminate after dispatch. Readback or native callback failure likewise fail-stop;
no zero receipt, refund, or retry is invented. A listener failure after successful callback
is `Committed`: the paired return value may be unavailable, but that application must not
be replayed. A valid rejection can also end in `Committed` listener failure with no stock
consumption. No-capacity rejection and pre-dispatch failure remain distinct.

## Native ownership

`NativeResourceCoordinator`'s internal entry uses only the attached world simulator. It
forwards to the existing transaction used for all resource operations and save exclusion.
`RequireAshApplicationCommit()` succeeds only while the validated positive receipt callback
is executing, and its privilege is cleared in finally. Read capture, transfer, water/ash
collection, and the rest of the GPU step cannot authorize fertilizer consumption. Existing
indeterminate state/reset-on-actual-world-load semantics are unchanged. Worker scheduling,
field revision advancement, consequence dispatch and satchel integration remain adoption
work; this entry is not a parallel production tick loop.

## Verification

Protocol negative draft: 12 failed / 5 passed against deliberately unconditional fabricated
success; strict decoder: 17 passed. Focused application/step/snapshot suite: 36 passed.
Full portable suite: 227 passed. Full native suite: 1,314 passed, zero failures or skips.
Managed tests exercise phase rejection, exact accepted callback, native coordinator
forwarding through its installed-assembly context, failed callback poison, and listener
failure completion. They do not execute native stock changes or GPU code. A Unity adapter test verifies the
appended structured command index and outcome bits30–31 through the existing four-word
readback abstraction. Both positive and rejected receipts retain committed failure semantics
when a later listener throws.

Seven `AshApplicationShaderTests` compile without warnings. They cover competing requests,
earlier removal/taint, later removal, OpenSoil, rejection field preservation against an
identical control simulation, falling ash after application, and malformed/stale command
words. Shader execution is pending the sole controller; a compiled fixture is not GPU proof.

Controller request: pin the final source/fixture commit, run the seven application tests
first, then existing collection/external-change tests and full shader suite because all
share ApplyExternalChanges. Use the repository's licensed Unity harness and exclusive QA
lock; preserve captures and exact command receipt words. No deployment or game launch is
part of this request.
