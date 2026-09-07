# Persistent burn-damage identity

Native structure, stockpile, tree, crop and infrastructure targets use `family:entity:<EntityId>` keys. `EntityComponent.EntityId` is Timberborn's persistent Guid; the family distinguishes independently registered target kinds. Enumeration order, component object identity and footprints do not determine saved damage identity. An entity without an assigned Guid is rejected before target registration.

The consequence snapshot still records the target key, accumulated damage and last-damaged tick. Its format is unchanged. Restoration matches the complete key: a replacement entity at the same coordinates must not inherit the old entity's damage.

Older development saves used runtime object hashes. Those keys cannot identify reconstructed native objects, and the snapshot contains no reliable identity mapping to migrate them. Unmatched legacy damage remains unapplied; it is never guessed onto an entity by position, enumeration or a coincidental hash. Loading nonzero unmatched legacy damage emits `wildfire_legacy_damage_identity_unrestorable` with the target count and policy. Other readable simulator and native world state can load normally. A subsequent save captures currently matched state, so retain an original copied save when assessing older development worlds.

The regression test creates separate instances of the installed native EntityComponent, assigns the same Guid using its actual managed setter, changes registration order/footprints, and passes damage through capture, encode, decode and restore. It verifies retained damage/tick and no transfer to a different Guid. Separate checks cover family separation and legacy reporting without reassignment. No Unity entity or live game world is constructed by that fixture.

This fixes identity generation, not the complete lifecycle. Incremental registration must still preserve damage when footprints or eligibility change; the existing full registration method resets state. Live entity/material updates and copied-save gameplay checks for partial tree, structure and storage damage remain required by [issue 84](https://github.com/Ustice/Timberborn-Wildfire/issues/84).
