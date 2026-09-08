# Desired-material planner: actual GPU proof

Executed 2026-09-07 on exact source `5878283`. Native Release build: zero warnings/errors.
Licensed Unity 6000.3.6f1, Metal on Apple M2 Pro: **four cases passed, Editor exit 0**.
Actual device reports eight UAV slots and 31 compute inputs; no shader/UAV warnings appeared.
Evidence and runnable probe: `/tmp/wildfire-desired-material-planner-engine`.

The probe loads the unchanged production native DLL outside Assets, constructs the actual native
material registry and explicit canonical retentions, and invokes the production planner through
reflection. Each plan runs through the real native `CreateFromSnapshot` factory and
`TryHandoffMaterial`, including HLSL, receipt decoding, authority commit and subsequent simulation.
These are supplied registry projections and snapshots, not a captured colony or native entity lifecycle.

| Case | Actual result |
| --- | --- |
| Surviving same-owner swap | Both local slots captured before replacement; final slots 2/1, fuel 1/0; no duplicate archive. |
| Overlapping two-cell move | Source closure covers all three cells; final slots 0/1/2 and fuel 0/0/1. |
| Hidden exhausted archive reveal | Planner selects Archived, preserving fuel 0 and raw history 11; previous owner has exact GPU outgoing archive; incoming archive consumed. |
| Unowned Water→Badwater | Typed baseline preserves receipt-time heat/water and ambient companion fields; no owner, known slot or archive invented. |

Both movement cases start with four pending ordinary inputs, exceeding the simulator's fixed ordinary
budget. All four precede the marker and retain exact old source provenance: target 1, slots 1/2/1/2,
with fuel after each input 4/2/0/1. Marker prior receipts see final queued fuel 0/1. Destination ambient
fields remain local while source remaining fuel and raw burn history move by slot. The applied batch
therefore does not duplicate a partially burned contributor or refill the exhausted one.

The baseline case also initializes a separate actual native simulator from the GPU-applied receipt
and original transport, then runs one tick. Its cells, companion and transport exactly equal the
handoff simulator after simulation. This distinguishes preserved ambient state from a receipt-only
assertion that could miss a later reset. Replanning returns null after every accepted case.

Each case records before snapshot, plan, actual receipt, raw deltas and after snapshot. No shader or
layout change was made and no full shared shader-suite rerun was needed for this host planner proof.
No game, deployment, Steam launch, native consequence application or world activation occurred.
The fixture does not test concurrent native changes or resolve unsupported retained-state policy.

- Native DLL SHA256: `c7da6c1f204dbec838af557c663fdec17c59e15157b9c63b5ff039560d837b38`.
- Core DLL SHA256: `fabb27d4eaef84436adcb12df044b3d1d68ec6040e59580761eca92e34c18838`.
- Probe SHA256: `f877be9727acec3f0bcc5eae56d1bfa0d18ec7c9e6df7e37b53c96be1aa52e8f`.

Installed game assemblies target Unity 6000.5.5; the isolated licensed Editor is 6000.3.6f1. Results
prove the exercised native planner/factory/protocol on this Metal backend, not whole-mod Editor
compatibility. Controller ended idle: no Unity/game/Blender process and no build/deploy lock.
