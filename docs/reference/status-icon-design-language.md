# Timberborn Status Icon Design Language

This reference describes the palette, sub-icon, and symbol grammar visible in the exported Timberborn status icons.

## Color Patterns

### Red Ring, Tan Inner Background, Dark Foreground

Meaning: urgent active problem. The entity or site has a serious condition that needs attention while the affected subject remains visually readable.

Use for active hazards, shortages, conflicts, countdown hazards, and serious warnings where the entity is still represented as the main dark symbol.

Examples: `BuildingNeedsWater`, `FloodedBuilding`, `NotEnoughWater`, `NoUnemployed`, `OutOfHaulersRange`, `GenericError`, `UnstableCoreCountdown`.

### Red Ring, Dark Inner Background, Tan Foreground

Meaning: cannot function. The subject is blocked, disconnected, unpowered, incompatible, or otherwise unable to operate. The dark interior makes the entity feel shut down.

Use for incapacitated beavers, inoperable buildings, or any state where the entity cannot perform its normal role until resolved.

Examples: `EntranceBlocked`, `NoPower`, `UnconnectedBuilding`, `NonCompatibleVersion`, `LackOfNutrients`.

### Gold Ring, Tan Inner Background, Dark Foreground

Meaning: warning or impairment. The condition is concerning but less severe than red. The subject can usually still function.

Use for beavers or systems that are impaired but not fully disabled. This is a good fit for coughing and burned-but-mobile states.

Examples: `Injury`, `ChippedTeeth`, `OutOfEnergy`, `OutOfFuel`, `NothingToDo`, `Stranded`, `UnspecifiedGood`.

### Gold Ring, Dark Inner Background, Tan Foreground

Meaning: basic creature need. These are direct biological or need-meter statuses, usually personal creature conditions rather than building or system problems.

Use sparingly for fundamental beaver needs such as hunger, thirst, or exhaustion.

Examples: `Hunger`, `Thirst`, `Exhaustion`.

### Brown Ring, Dark Inner Background, Tan Foreground

Meaning: neutral mode or system state. These are usually descriptive or player-driven, not emergencies.

Use for pause, emptying, demolish, stopped, or mode-like behavior.

Examples: `ApiStopped`, `Demolish`, `Empty`, `Pause`.

### Black-Brown Ring, Tan Inner Background, Black Foreground

Meaning: terminal creature state.

Use for death-like final outcomes. Avoid this pattern for temporary incapacitation.

Example: `Death`.

### Green Ring, Yellow-Green Inner Background, Black Foreground

Meaning: positive result.

Use for unlocks, high scores, success, achievements, and beneficial statuses.

Examples: `NewFactionUnlocked`, `WellbeingHighscore`.

### Teal Ring, Tan Inner Background, Dark Foreground

Meaning: automation or special operational mode.

Use for automation states or unusual operating modes.

Example: `PausedByAutomation`.

## Sub-Icons

### Exclamation Bubble

Upper-right cream circle with a dark exclamation mark. Means active attention: warning, problem, blocker, or urgent condition.

### Question Bubble

Upper-right question mark. Means no target, confusion, stranded, or unresolved/unknown action.

### Pause Badge

Small pause bars. Means process or lifecycle is halted rather than simply missing something.

### Countdown Clock

Small clock. Means timed hazard or countdown modifier.

### Contamination Mini-Symbol

Small badwater or contamination mark. Adds contamination as the cause or modifier to the base problem.

### Bottom-Right Item Or Gear

Lower-right small mark. Means missing selected good, recipe, or production configuration.

### No Sub-Icon

Read the main symbol directly. Used for basic needs, modes, positives, and terminal states.

## Common Main Symbols

### Droplet

Liquids: water, badwater, contamination, thirst, or fluid problem.

### Pentagonal House Or Building

Building, structure, facility, or built object.

### Gear

Power, machinery, automation, mechanical dependency, or production system.

### Beaver Silhouette

Creature or beaver state. Native beaver icons use a simple dark shape, usually side-profile or hunched. Avoid detailed face, fur, or realistic anatomy.

### Zzz

Exhaustion, sleep, incapacitated tiredness, or low energy.

### Skull

Death or terminal state only. Avoid for temporary choking unless the state is lethal.

### Pause Bars

Paused, stopped, or inactive mode.

### Flame Or Heat Mark

Fire, burning, scorch, or heat damage. Pair with a building or beaver subject to clarify what is affected.

## Wildfire Status Mapping

### Coughing

Use `WildfireCoughingStatus`. It uses a gold ring, tan inner background, dark foreground, with optional exclamation. This means impaired beaver, still able to act.

### Choking

Use `WildfireChokingStatus`. It uses a red ring, dark inner background, tan foreground, with exclamation. This means the beaver cannot function or self-rescue.

### Burned

Use `WildfireBurnedStatus` for beaver fire or heat injury. It uses a gold ring, tan inner background, dark foreground, with optional exclamation. This means the beaver is burned but may still function unless a later safe injury API proves work prevention.

### Burning

Use `WildfireBurningStatus` for structures, not beavers. It uses a red ring, dark inner background, tan foreground, with exclamation. This means the building is actively on fire and inoperable.

### Dead

Use Timberborn's native death status language unless `TWF-171` proves a Wildfire-specific death surface is required. Death is a terminal beaver state and should not reuse choking, burned, or burning icons.
