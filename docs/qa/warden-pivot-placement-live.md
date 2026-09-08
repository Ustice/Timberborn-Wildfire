# Warden green preview and doorstep boundary — 2026-09-07

Actual player follow-up on immutable `fd13bef8af9797822763f26894476d598270b306`, adding the reviewed standard nested CustomPivot declaration. The supported Release deployment rebuilt all four bundles in a new detached checkout. All 39 copied files matched source/build bytes; the generated manifest completed the 40-file installation. Native SHA-256: `69e742515c8493fb6784361e7ccf3d95024d3e237c2e168d5d20e6f54330a210`; Core: `06e089641943602f59ecdb0326c102ecb01cfa816704ebf5ba82f492914028a8`. Previous dbe2b5f installation and the clean healthy save were backed up.

A single raw Steam launch requested process-only `--wildfire-enable-qa-mutations`. Steam displayed its custom-arguments confirmation; the startup waiter timed out while this dialog remained pending. No second launch was issued. CUA could not attach to the existing Steam process (`AXError.illegalArgument`); installed macOS `screencapture` successfully showed the exact argument and Continue button. One observed CLI click accepted that existing request, and Steam created PID 17364 with the exact argument. Saved Steam launch options remained unchanged. This was a pending confirmation, not a failed game startup or authorization denial.

Camera Bookmarks, Hats, Wildfire and Wildfire Asset Preview remained selected. The named disposable `Healthy start dbe2b5f` save loaded in **8,318 ms**, with actual runtime `loaded_game_ready=true simulator_integrated=true`, WF1 tick **25** restored and fresh `command_access=development`. The world remained paused; exposure sampling was not yet available after this load, so zero samples are not a population result. The UI still showed nine adults and four kits. Native physical Shift/Option/Z enabled development tools, using the same guarded controller input helper as the prior run.

The Warden placement cursor now displayed a **green station preview over clear grass**, without the former `ComposeCoordinates` exception. A single commit-placement click then reached a distinct native failure:

```text
NullReferenceException
BuildingDoorstepSpawner.SpawnDoorstep(BuildingModel)
BuildingDoorstepSpawner.OnEntityInitialized(EntityInitializedEvent)
EventBus.Post
EntityComponent.Initialize
EntityService.Instantiate
```

This establishes preview recovery and the next entity-initialization boundary, not successful construction. No worker assignment, refill, fire stimulus or suppression occurred. Native exception saving ran; the controller preserved the primary stack and error report, clicked the error-screen Exit button, and verified PID 17364 absent. No unchanged retry followed. The healthy named baseline was not overwritten, and no OWNED4 runtime publication occurred.

Evidence: `/tmp/wildfire-warden-pivot-live/`, including exact deployment plan/log and installed hashes, previous-mod and save backups, `steam-args-confirmation.png`, launch log/Steam process evidence, `loaded-readiness.log`, `Player-doorstep-failure.log`, `doorstep-primary-stack.txt`, and `placement-doorstep-exception.jpg`. The green preview was observed in the controller screenshot before the placement click; that intermediate screenshot was not separately saved to disk. Native `error-report-2026-09-07-22h51m24s.zip` is 240,035 bytes, SHA-256 `f464c7b929a6fc28f06469595b02319a7c50710b6df72de8f4534c00b53beb2c`. Existing input geometry and native-key helper are recorded beside the evidence. Parent owns the next doorway source diagnosis; no further game deployment is authorized until its reviewed pin.
