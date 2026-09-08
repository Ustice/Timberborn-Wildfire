# Warden corrected startup and placement boundary — 2026-09-07

Pinned source `dbe2b5f` includes the native BuildingAccessible role fix (`555dc89`, reviewed source `3f03277`). The supported Release/deployment command rebuilt all four bundles in a detached checkout, backed up the previous installed mod, and installed 40 matching files. Native DLL SHA-256: `848dc078c259684b09de6767d70d518187ed12a45b549a19eaa96d0b1cca445d`; Core: `89a5e851b975251d24c1c1a05ab82e58e3defd3858a3ae88da688bf0329c0e0f`. Actual player remained Timberborn 1.1.2.4-52e959e-xsm, Unity 6000.5.5f1, Apple M2 Pro Metal. No source or public release was changed by deployment.

One guarded Steam launch used the process-only argument `--wildfire-enable-qa-mutations`; the Steam log and fresh bridge `command_access=development` agree. Saved Steam options were unchanged. Camera Bookmarks, Hats, Wildfire, and Wildfire Asset Preview remained selected. The existing three-atlas warning remains visible with that mod set; its cause was not isolated in this run.

| Live operation | Result |
| --- | --- |
| Fresh Iron Teeth / official Cliffside 100×50 / Easy | Loaded in 8,517 ms; settlement naming and normal world UI reached; former duplicate-Accessible eager-preview exception absent |
| Runtime readiness | GPU simulator integrated, 100×50×23 legacy runtime; tick 9 sampled all 13 beavers with no exposure; ordinary dispatch later reached tick 25 |
| Save clean disposable baseline | `Wildfire Iron Teeth QA 2026-09-07 / Healthy start dbe2b5f.timber`, 0.20 s, 139,630 bytes; backup SHA-256 `fec30ceac28ef8aad07e3e32795e0ddabc5aae11e01c47d456514d15e33cf8cd` |
| Native developer shortcut | Actual physical Shift/Option/Z events enabled development mode; log and expanded Water toolbar agree; tooltip identifies “Warden station (prototype)” |
| Select Warden tool, move preview over grass | New primary `NullReferenceException` in native `BlockObjectPreviewPicker.ComposeCoordinates`, before any placement |

The new primary stack continues through `CenteredPreviewCoordinates → AreaPicker.GetBlocks → AreaSelectionController.ProcessInput → BlockObjectTool.ProcessInput`. It is distinct from the corrected Warden `Awake` failure. The game produced an exception save and native error ZIP. No station placement, worker assignment, refill, forced heat, or suppression occurred. The controller captured the failure and clicked the error-screen Exit button; PID 11896 was verified absent. No unchanged retry followed. The clean healthy save remains available for the next reviewed correction. This proof does not activate OWNED4 or establish a complete station workflow.

## Desktop input evidence

CUA screenshots and native AX menu/keyboard actions remained available, but coordinate clicks returned `windowNotFoundAtPosition` even for correctly mapped initial menu points. Installed `cliclick` 5.1 provided the working mouse route, with coordinates derived from observed screenshots and read-only CoreGraphics window/display bounds. This was a routing failure, not an authorization denial.

Initially the app screenshot mapped 1366×768 to the physical 1920×1080 window. Later native auxiliary windows extended the captured application union from Y=-58 through 1080; the 768-high composite therefore scaled by `768/1138`, with main content about 1296 pixels wide and unused right canvas. Mapping screenshot `x/scale`, `y/scale-58` correctly selected individual tools. These dimensions are evidence for this session, not reusable fixed coordinates.

CUA modifier combinations delivered plain Z camera rotation. A controller-owned one-shot CoreGraphics helper verified the foreground bundle and PID, delivered actual Carbon Shift/Option/Z key events to that PID, and released modifiers. The native log recorded `Dev mode enabled`; no key binding or saved launch option was altered. Helpers and exact geometry records are retained with the artifacts.

Evidence root: `/tmp/wildfire-warden-fixed-live/`. It contains all build/deploy logs, installed SHA-256 manifest, previous-mod backup, exact launch record, `ironteeth-new-readiness.log`, `ironteeth-healthy-start.png`, clean save backup, `Player-placement-failure.log`, `placement-primary-stack.txt`, and `warden-placement-primary-exception.jpg`. Native report `error-report-2026-09-07-22h30m48s.zip` is 107,250 bytes, SHA-256 `15499b08c16ba3d7bbf9a0abd0eedc78df6197a7a8bf81981203aed239c42586`. Input helpers are `window-geometry.swift`, its JSON captures, and `toggle-dev-mode.swift`; `CUA-ROUTING.md` preserves failed and successful paths.
