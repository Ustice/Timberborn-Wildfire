# Native structure closure proof

Test-only investigation of a same-entity alternative to immediate native reconstruction. This does not activate a new damage threshold, closure component, repair behavior or material lifecycle route.

The installed `BlockableObject.Block(object)`/`Unblock(object)` APIs track independent owner tokens. They mutate the token set before invoking ObjectBlocked/ObjectUnblocked, emitting events only for the first blocker and final release. A wildfire-owned token can therefore coexist with native player pause without writing the player's persisted Paused field.

Six actual managed native test cases prove:

- Repeated Block/Unblock is idempotent; one owner cannot remove another owner's closure.
- Actual PausableBuilding.Pause/Resume methods coexist with a separate fire token in both release orders.
- Block and Unblock callback exceptions occur after token mutation; the existing NativeResourceTransaction becomes indeterminate and prevents replay/save.
- The actual Workplace.OnObjectBlocked → UnassignAllWorkers → Worker.Unemploy chain clears native employment and emits unemployment. Closure is not employment preservation.
- ConstructionSite.IsOn remains false until all blockers release.

Full native suite at this proof: **827 passed**, zero failed/skipped. These fixtures use actual installed managed methods and callbacks; they do not create GameObjects, fake Unity liveness, or prove native world registration, live movement, positive scene resolution or actual builders.

Closure is separate from repair. Reopening cannot heal structural damage or mint construction materials. The existing rollback selects Unfinished for every positive damage; its 10% helper is unused. No alternative threshold is adopted here. Moving reconstruction later can reduce disruptive lifecycle work while preserving the need for material-funded repair; a subthreshold scorch policy would still need a conserved minor-repair path.

The current reconstruction deletes A and creates a distinct unfinished B. Normal ConstructionSite completion marks that same B finished; special DeleteOnFinishConstructionSite blueprints require separate handling. Exact repair ownership must track B directly, preserve A's retained origin tombstone, and never forward delayed A events or cell-selected repair effects to B/C.

Replacement also depends on GPU material birth semantics. Archived/CapturedSource adoption requires the same identity, and Fresh starts history at zero. A new native Guid/TargetId cannot silently inherit A by changing the origin binding or refill its material through a fresh default. B needs an explicitly conserved repair-material birth or a separately designed GPU-authorized history-preserving transition. This proof changes neither protocol nor policy.
