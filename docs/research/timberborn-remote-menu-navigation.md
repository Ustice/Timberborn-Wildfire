# Timberborn Remote Menu Navigation Research

Ticket: `TWF-028`

Date: 2026-05-01

## Conclusion

QA cannot navigate Timberborn menus remotely without screen interaction using the current Wildfire repo surfaces.

The current command bridge is remote and safe, but it only supports `status` and `help`. Timberborn has keybindings for a few UI actions and debug toggles, and its shipped UXML exposes named menu buttons, but the menu buttons inspected here are not keyboard-focusable and no repo-local or public modding reference showed a ready-made remote "click this menu item" API.

The best smallest path is not a generic remote menu navigator. It is an allowlisted in-game command bridge extension for one concrete QA need at a time, starting with a non-destructive loaded-game readiness command or a separately researched `load-latest-save` command that calls Timberborn-owned save/load services directly.

Confidence: medium-high for "not possible with current surfaces"; medium for the proposed future direction because the exact Timberborn save/load service API still needs targeted reflection/source discovery.

## Evidence

### Wildfire Command Bridge

- `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs` defines only `status` and `help`.
- `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs` rejects unknown commands and returns a logged failure instead of dynamic execution.
- `src/Wildfire.Timberborn/TimberbornQaCommandFileBridge.cs` polls `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-inbox.txt`, forwards the text to the bridge, and writes `command-outbox.txt`.
- `scripts/invoke-timberborn-command.ts` also allowlists only `status` and `help` before writing the inbox.
- Historical ticket `TWF-019` on branch `archive/file-kanban-2026-05-23` records live proof that `status` returned through the file bridge from Timberborn.

This proves remote QA command execution exists, but not menu navigation.

### Existing Menu Automation

- `docs/timberborn-menu-coordinate-guide.md` documents in-game Escape, load, mods, startup mods, exit confirmation, standalone main menu, and main-menu load coordinates.
- The guide explicitly states coordinates are valid only for the documented display state and records screen-click verification with `cliclick`.
- `docs/TEST_PLAN.md` requires UI automation to take coordinate targets from the guide, verify the target app and screen, and fail loudly rather than click unknown states.
- Historical ticket `TWF-015` on branch `archive/file-kanban-2026-05-23` is still framed as a coordinate-driven Bun utility, not a remote command bridge or in-game save/load command.

This is the current supported path for menu traversal: guarded screen interaction.

### Timberborn Keybindings

Local Timberborn modding assets expose useful keybindings:

- `KeyBinding.Cancel.blueprint.json` binds `Cancel` to `/Keyboard/escape`.
- `KeyBinding.Confirm.blueprint.json` binds `Confirm` to `/Keyboard/enter` and `/Keyboard/numpadEnter`.
- `KeyBinding.NextRootButton.blueprint.json` and `KeyBinding.PreviousRootButton.blueprint.json` bind root-button cycling to Ctrl + mouse wheel.
- `KeyBinding.NextTool.blueprint.json` and `KeyBinding.PreviousTool.blueprint.json` bind tool cycling to Shift + mouse wheel.
- `KeyBinding.ToggleConsole.blueprint.json` binds console toggle to Alt + backquote.
- `KeyBinding.ToggleDevMode.blueprint.json` binds developer mode to Alt + Shift + Z.
- `KeyBinding.ToggleDebugMode.blueprint.json` binds debug mode to Alt + Shift + X.

These support limited keyboard or pointer-wheel automation, but they do not provide named menu selection, load-save selection, or modal list traversal without screen input.

### Timberborn UXML

Local Timberborn UI assets expose named menu elements:

- `Views/MainMenu/MainMenuPanel.uxml` includes `ContinueButton`, `LoadGameButton`, `ModManagerButton`, `SettingsButton`, and other main-menu buttons.
- `Views/Options/LoadGameBox.uxml` includes `Settlements`, `Saves`, `DeleteSettlementButton`, `DeleteSaveButton`, `LoadButton`, and `BrowseDirectoryButton`.
- `Views/Options/SaveBox.uxml` includes `SaveName`, `SaveButton`, `ItemList`, and `BrowseDirectoryButton`.
- `Views/Common/BottomBar/BottomBarPanel.uxml` exposes bottom-bar slots: `SubSection`, `LeftSection`, `MiddleSection`, and `RightSection`.

The key menu buttons inspected in `MainMenuPanel.uxml`, `LoadGameBox.uxml`, and `SaveBox.uxml` use `focusable="false"`. That makes keyboard focus/Tab navigation an unlikely immediate route for menu automation.

### Debug And Developer Surfaces

- `docs/timberborn-bottom-menu-guide.md` records live evidence that `Shift-Alt-Z` toggled developer mode and `Shift-Alt-X` opened debug panels.
- The official Timberborn wiki developer-mode page also lists Alt + Shift + Z for developer mode and Alt + Shift + X for debug mode: <https://timberborn.wiki.gg/wiki/Developer_Mode>.
- Historical ticket `TWF-027` on branch `archive/file-kanban-2026-05-23` records visible debug panels such as Automation, Performance, Cursor, Mesh metrics, Sound system, Mechanical system, Navigation, Terrain columns, Parallel singletons, Time scale, Clock, Water rendering, Water columns, and Weather.

These surfaces are useful for QA observation and some dev-only mutations. They are not evidence of a remote menu-navigation API.

### Modding References

- `docs/reference/modding-guide.md` identifies UI extension points such as `IBottomBarElementsProvider`, `IEntityPanelFragment`, `VisualElementLoader`, `UILayout`, and game-owned UI helpers such as `DialogBoxShower` and `QuickNotificationService`.
- The same guide identifies save/load persistence interfaces such as `ISaveableSingleton`, `ISingletonSaver`, and `ISingletonLoader`, but those are mod-state persistence hooks, not "load this Timberborn save from the main menu" commands.
- `docs/reference/timberborn-ui.md` focuses on authoring Timberborn-native UI, not driving existing Timberborn menus remotely.

These references support building native Wildfire UI or mod persistence. They do not by themselves expose safe menu automation.

## Inference

The presence of named UXML elements means an in-game mod could probably locate some UI elements after they are loaded, but invoking those elements generically would be brittle and version-sensitive. It would also blur the safety boundary because it can accidentally click destructive or external-opening controls such as delete buttons, feedback links, browse-folder buttons, or exit buttons.

The safer architecture is command-oriented, not menu-oriented:

- Keep external QA as a file-command client.
- Keep the in-game command bridge allowlisted.
- Add one named command per QA outcome.
- Implement each command with Timberborn-owned services where possible.
- Log before and after state with searchable tokens.
- Reject unknown commands and avoid arbitrary UI queries or arbitrary VisualElement invocation.

## Safety Boundary

Safe for this sprint:

- Read-only bridge commands such as `status` and `help`.
- Commands that report current state, loaded-save metadata, mod readiness, or available known commands.
- A future command that performs one explicitly named, repeatable, non-destructive action after state checks.

Unsafe as a generic primitive:

- "Click menu element by name."
- "Invoke VisualElement by path."
- "Select list row by index" without proving the settlement/save identity.
- Any command that can delete saves, overwrite saves, open external folders/sites, exit to desktop, or mutate a non-disposable save without explicit ticket scope.

## Risks

- Timberborn internal UI controller and save/load service names may change between versions.
- UI Toolkit element names are useful evidence, but not a stable automation API.
- Main-menu UI may not exist in the loaded-game context where the current Wildfire game-context bridge is bound.
- Loading a save from inside a loaded save is state-changing and needs a disposable-save boundary.
- Keyboard automation still interrupts the active desktop and does not solve the original "remote without screen clicks" goal.

## Smallest Follow-Up Ticket

Create a worker ticket for one allowlisted bridge command, not a generic navigator:

Title: `Add Read-Only Timberborn QA Readiness Command`

Goal: Extend the existing file command bridge with a `qa-readiness` command that reports loaded-game readiness, current Timberborn version text if available from logs or game state, simulator integration, and whether known Wildfire command files are writable. It must not open menus or mutate saves.

Verification:

- `git diff --check`
- `dotnet test`
- `dotnet build Wildfire.slnx`
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6`
- `Player.log` evidence with `wildfire_command_request command=qa-readiness` and `wildfire_command_result command=qa-readiness success=true`

After that lands, research or implement a separate `load-latest-save` bridge command only if a Timberborn-owned save/load service can be identified and guarded by disposable-save checks. If no such service is found, keep `TWF-015` coordinate-driven.

## Checked Sources

- `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs`
- `src/Wildfire.Timberborn/TimberbornQaCommandFileBridge.cs`
- `src/Wildfire.Timberborn/WildfireConfigurator.cs`
- `src/Wildfire.Timberborn/TimberbornFireRuntime.cs`
- `scripts/invoke-timberborn-command.ts`
- `docs/timberborn-menu-coordinate-guide.md`
- `docs/timberborn-bottom-menu-guide.md`
- `docs/TEST_PLAN.md`
- `docs/reference/timberborn-ui.md`
- `docs/reference/modding-guide.md`
- `docs/reference/blueprint-reference.md`
- Historical `TWF-012`, `TWF-015`, `TWF-017`, `TWF-019`, and `TWF-027` tickets on branch `archive/file-kanban-2026-05-23`
- Timberborn local modding assets under `~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/`
- Official Timberborn wiki developer-mode page: <https://timberborn.wiki.gg/wiki/Developer_Mode>
