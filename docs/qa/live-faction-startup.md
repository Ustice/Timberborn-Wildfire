# Fresh faction startup — 2026-09-07

Actual player follow-up to the [current-build baseline](live-current-build-baseline.md), on the same verified source `8cbeb19` and installed DLL/bundles. No redeploy or extra process launch occurred between faction tests. Both used official Cliffside (100×50), Easy mode, with Camera Bookmarks, Hats, Wildfire, and Wildfire Asset Preview enabled. New worlds were disposable.

Folktails succeeded: native world loading took 6,692 ms, the GPU runtime became ready, and the fresh colony visibly contained nine adults and four kits with its starting food/water. Ordinary dispatch reached tick 33 without a runtime exception. `Wildfire Folktails QA 2026-09-07 / Healthy start 8cbeb19.timber` was saved and backed up before leaving. This is a healthy starting fixture, not a sustained colony or worker/fire workflow proof. At both tick 9 and tick 33, diagnostics report `beaver_field_exposure_available=true sampled_beavers=13 exposed_beavers=0 skipped_no_position_api=0 skipped_bounded_sampling=0 unavailable_reason=none`. The behavior dispatcher reports zero tracked states/decisions because no beavers had exposure; its state count is not the colony population. These samples independently verify all 13 visible beavers were enumerated.

Iron Teeth failed during initial native toolbar construction, before the settlement naming screen or simulator readiness:

```text
InvalidOperationException: More than one component of type Timberborn.Navigation.Accessible found in WardenStation.IronTeeth(Clone)
ComponentCache.GetCachedComponent<T>()
Wildfire.Timberborn.FireResponse.WardenStation.Awake()
ComponentCache.SetActive()
BaseComponentUnityAdapter.OnEnable()
TemplateInstantiator.Instantiate(...)
PreviewFactory.Create(...)
PreviewPlacerFactory.EagerlyCreateFirstPreview(...)
BottomBarPanel.Load()
```

The native exception UI showed that same primary stack. Follow-on preview initialization and water-renderer teardown null references occurred after the initial failure. Wildfire stopped with `game_update_id=0 simulator_integrated=false`; persistence correctly logged `save_skipped initialization_state=Unloaded`. A native error report was created. No Warden Station was placed and no forced heat was used. This is a confirmed actual native template/preview startup defect; supplied-component engine fixtures did not exercise this full decorator lifecycle.

Evidence: `/tmp/wildfire-healthy-factions-live/`, including `folktails-new-colony.png`, `ironteeth-startup-error.png`, per-faction Player.log copies, diagnostic readiness, healthy-save backups, SHA-256 manifest, and `error-report-2026-09-07-21h46m19s.zip`. The controller closed the failed game through its error-screen Exit button after capture; PID 8033 exited. There was no unchanged retry. Source diagnosis and a reviewed correction are required before repeating Iron Teeth startup.
