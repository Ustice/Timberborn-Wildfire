# Initial owned session preparation

Source: `33c6446`, with scoped capture/formation `baac452`, explicit body compiler `b6cac6f`, typed baseline `63aeaed`, and fixed environment projection `070fc17`. No runtime publication or gameplay activation is wired.

## One unpublished session

Internal PrepareInitial accepts a grid, same-scope native capture producer, explicit accounting-plan producer, new complete-snapshot backend factory, exact native effects and the existing shared resource guard. The plan contains copied body accounting selections, parameters and seed only. It cannot supply material IDs, coordinates, quantities, profiles, initial ambient arrays or historical state.

One CaptureAtRest scope covers the entire operation. The initializer captures native facts, compiles supported body selections, invokes the fixed environment projector on that same capture, creates a fresh registry with all contributors and body accounting, and constructs a witnessed consumer. Public capture/consumer constructors preserve their existing guarded behavior; narrow internal methods permit this single scope without nested guards.

Every cell resolves through the registry once. Packed material comes from resolved.PackedDefinition, while the matching resolved profile supplies companion material. This preserves physical packed terrain semantics: an OpenSoil profile's terrain default cannot turn it into solid ground, and the existing Tree definition is preserved exactly. The fixed environmental overlay writes initial wetness/soil once. Initial target/slot arrays come only from the new registry. Known slots equal the active set; hidden owners retain bindings and body witnesses without invented archives. Tick, token, burn history, transport and pending changes start at zero/empty.

The initial snapshot and OWNED3/body association validate before backend allocation. The factory receives its own cloned arrays. After allocation, the backend must expose CompleteMaterialHistory and return exactly the expected initial cells, companion fields, transport, identities, known slots, parameters and seed. A factory cannot rewrite its mutable input and have that become the initializer's expected state.

Callback-capable backend/history reads finish before the final full native capture. The initializer compares body readings, membership, exclusions, water-source facts and environment, rather than merely testing that the same Guids are live. Any discrepancy disposes the unpublished backend. A preparation failure does not mutate native quantities or poison the shared resource guard. A factory that throws before returning a backend owns cleanup of its partial allocation, consistent with the existing restore boundary.

## Executed tests

Twelve initial-session cases plus eight scope/equality cases: **20 passed**. Full native suite at this checkpoint: **1,104 passed, zero failures/skips**. Logs: `/tmp/wildfire-owned-initial-session-focused.txt` and `/tmp/wildfire-owned-initial-session-full.txt`.

The session cases execute the real compiler, fixed projector, registry, damage calculator, witnessed consumer, complete snapshot validation and OWNED3 capture/new restore against a supplied in-memory snapshot backend. They verify actual3 versus declared5 accounting under the same native spec, no native yield/stock effects, hidden binding-only contributors, exact initial fields/authority, producer reentry exclusion, late native quantity/placement/membership/environment/exclusion changes, late liveness-callback mutation, invalid selections before allocation, backend-input mutation and cleanup/retry after factory failure.

This is not GPU or real game initialization proof. The backend simply retains the supplied snapshot; native bodies/effects in these tests are supplied facts and callback fakes. The sole controller has been asked for the next bounded fixture: supplied actual native GameObjects/components and capture, fixed environment, explicit selections, actual complete-snapshot factory, OWNED3 capture/new restore and unchanged native quantities. That fixture must remain distinct from full-colony provider/load proof.

## Remaining gates

Current supported compilation accepts Tree, Crop, Structure and Stockpile only. Infrastructure, Vegetation, standalone GoodStack, unknown profiles and multiple native inventory roles reject the entire capture. The native coverage audit has already identified physical-building classification and additional inventory roles that need concrete treatment; merely allowing more enum values would not provide correct material or consequence ownership.

Explicit proof accounting inputs are not the adopted first-release plant/material policy. Full-colony activation, richer desired-baseline restore composition, supplied-engine proof and actual copied-save gameplay remain outstanding. Restore must never recover failure by calling PrepareInitial. An initial returned bundle is unpublished; runtime must publish it synchronously with no intervening native mutation or revalidate. Initial ambient fields are never a later material-reveal or restore-update source.
