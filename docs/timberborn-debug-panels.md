# Timberborn Debug Panels

This reference records the Timberborn debug and developer panels visible in the loaded-save state captured for `TWF-027`. Use it as a QA navigation aid, not as an automation script or permission to mutate save state during routine validation.

## Screenshot Evidence

| Screenshot                                                                                                             | Size          | Evidence                                                                     |
| ---------------------------------------------------------------------------------------------------------------------- | ------------- | ---------------------------------------------------------------------------- |
| [`01-loaded-save-baseline.png`](reference/screenshots/timberborn-debug-panels/01-loaded-save-baseline.png)             | `1920 x 1080` | Loaded save with the Development controls panel open over the colony.        |
| [`02-developer-mode-alt-shift-z.png`](reference/screenshots/timberborn-debug-panels/02-developer-mode-alt-shift-z.png) | `1920 x 1080` | Developer mode enabled with the loaded save visible and FPS overlay present. |
| [`03-debug-panel-alt-shift-x.png`](reference/screenshots/timberborn-debug-panels/03-debug-panel-alt-shift-x.png)       | `1920 x 1080` | Debug mode panel and Object debugger opened over the loaded save.            |

## Entrypoints

`Alt-Shift-Z` toggles Timberborn developer mode. In the captured loaded save this exposes developer affordances, including the FPS overlay and the Development controls panel.

`Alt-Shift-X` opens the debug panels. In the captured loaded save this creates two floating windows:

- `Debug mode panel`, centered over the map.
- `Object debugger`, docked on the right side of the screen.

The debug windows are draggable and have collapse/minimize controls. The current screenshots show the default loaded-save position after opening the panels.

## Development Controls

The Development controls panel is visible in [`01-loaded-save-baseline.png`](reference/screenshots/timberborn-debug-panels/01-loaded-save-baseline.png). It is a scrollable list of developer actions with a filter field at the bottom.

Visible favorite actions:

- `Add 1000 Science`
- `Fill input inventories`
- `Highlight resource reproduction spots`
- `Jump to next daytime [7]`
- `Jump to next season [8]`

Visible general actions:

- `Auto move levels down`
- `Auto move levels up`
- `Automation: Log partitions`
- `Camera state: Copy [Cmd + Shift + M]`
- `Camera state: Paste [Shift + M]`
- `Camera state: Restore [M]`
- `Camera state: Save [Cmd + M]`
- `Camera: FOV -`
- `Camera: FOV +`
- `Camera: Free mode`
- `Camera: Move clip plane farther`
- `Camera: Move clip plane nearer`
- `Camera: Move target down`
- `Camera: Move target up`
- `Camera: Reset [Shift + Z]`
- `Dump mesh metrics`
- `Force water on`
- `Kill 30% of characters`
- `Kill all characters except selected`
- `Kill all characters instantly`
- `Kill selected character [Delete]`
- `Multithreading: Snapshot (1 tick)`
- `Multithreading: Snapshot (2 ticks)`
- `Multithreading: Snapshot (3 ticks)`
- `Reset debugging panels position`
- `Save 20x to memory`
- `Sky: Toggle fog`
- `Speed: x0.25 [Shift + 1]`

Useful for Wildfire QA:

- Camera actions are useful for recreating comparable fire, smoke, and overlay screenshots.
- `Dump mesh metrics`, `Automation: Log partitions`, and multithreading snapshots may help diagnose performance or rendering symptoms without directly testing fire rules.
- `Reset debugging panels position` is useful when panels drift off the visible screen during repeated captures.

Inventory, science, season, water, speed, and character-kill actions mutate the save or simulation timing.

## Debug Mode Panel

The Debug mode panel is visible in [`03-debug-panel-alt-shift-x.png`](reference/screenshots/timberborn-debug-panels/03-debug-panel-alt-shift-x.png). It shows collapsible diagnostic sections. In the captured state, `Performance` is expanded and the other sections are collapsed.

Visible sections:

- `Automation`
- `Performance`
- `Cursor`
- `Mesh metrics`
- `Sound system`
- `Mechanical system`
- `Navigation`
- `Terrain columns`
- `Parallel singletons`
- `Time scale`
- `Clock`
- `Water rendering data`
- `Water rendering times`
- `Water columns`
- `Weather`

Visible `Performance` metrics:

- `CPU (Total)`
- `CPU (Main)`
- `CPU (Render)`
- `CPU (Wait)`
- `GPU`

Useful for Wildfire QA:

- `Performance` is the first stop for spotting obvious CPU, render, wait, or GPU changes while fire visuals are active.
- `Cursor` can help when QA needs to confirm map coordinates or hovered-cell behavior.
- `Terrain columns` and `Water columns` are likely useful for validating terrain-adjacent fire behavior, drought context, and future water/fire interactions.
- `Weather` is relevant when fire behavior depends on hazardous weather, drought, or season state.
- `Mesh metrics`, `Water rendering data`, and `Water rendering times` are rendering diagnostics and should be treated as supporting evidence rather than fire-rule proof.

Panels with caution:

- `Time scale` and `Clock` can affect simulation timing or time inspection. Record their values if used during validation.
- `Automation` may run or report automation systems outside Wildfire's own test harness. Prefer deterministic CLI scenarios for rule validation before leaning on live automation state.

## Object Debugger

The Object debugger is visible in [`03-debug-panel-alt-shift-x.png`](reference/screenshots/timberborn-debug-panels/03-debug-panel-alt-shift-x.png). The captured context is `Singletons`, with a filter field and a scrollable list of game services.

Visible singleton entries:

- `AccessibilitySettings`
- `EventBus`
- `GameSceneSerializedWorldSupplier`
- `TreePlantingCounter`
- `SpriteResizer`
- `SpriteFlipper`
- `SpecService`
- `FactionSpecService`
- `FactionService`
- `BadtideStreakAchievement`
- `MechanicalGraphRegistry`
- `BatteryChargeStorageAchievement`
- `TickService`
- `TickProgressService`
- `DayNightCycle`
- `DroughtWeather`
- `GameModeSpecService`
- `BadtideWeather`
- `HazardousWeatherHistory`

Useful for Wildfire QA:

- The filter is the safest way to inspect whether relevant Timberborn services are present without changing the save.
- Weather and time-related singletons can help explain live validation context, especially drought and hazardous-weather conditions.
- `EventBus`, `TickService`, and `TickProgressService` are useful orientation points when diagnosing whether a live mod adapter is receiving game-tick context.

The captures verify the singleton list; deeper object-property controls were not exercised.
