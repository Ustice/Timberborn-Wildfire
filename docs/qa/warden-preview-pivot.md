# Warden placement preview pivot

The sole live controller reproduced a placement-preview crash on deployed `dbe2b5f`: selecting the Iron Teeth Warden Station development tool and moving over clear grass threw `NullReferenceException` in native `BlockObjectPreviewPicker.ComposeCoordinates`, before placement. The earlier duplicate-Accessible startup fix had already allowed the new world to load.

The blueprint omitted `PlaceableBlockObjectSpec.CustomPivot`. Native `CenteredPreviewCoordinates` reads that property and passes it to `ComposeCoordinates`, whose first instruction dereferences `HasCustomPivot` without a null check. This is a nested value object, not another root blueprint component. Installed Builders Hut and Hauling Post templates provide `HasCustomPivot:false` with zero coordinates; the native Deep Water Pump deliberately supplies a different, enabled pivot.

The fix adds the ordinary native false/zero pivot object. It leaves the station's footprint, entrance, access roles, tool permissions and model unchanged. With `HasCustomPivot:false`, native placement calculates its center from the actual block dimensions; the zero coordinates do not impose a custom position.

## Executed regression

`NativeWardenPreviewCoordinatesTests` uses the installed native Newtonsoft deserializer to construct the real native placeable/block specs, then calls the exact native `ComposeCoordinates` method with a concrete terrain-intersection candidate. It does not stub the picker or assert only that a JSON field exists.

Before the blueprint edit, the station case failed with the same native method and `NullReferenceException`; two installed faction Builders Hut controls passed. Each control also removes the nested object through real deserialization and reproduces the exception. After the edit, all three cases pass; the full native suite passes **1,307 tests with zero failures or skips**. The station's 3×3 footprint maps the supplied intersection `(12.25,15.75,2)` to centered block origin `(11,14,2)`.

Evidence: `/tmp/wildfire-warden-preview-pivot/{red.log,green.log,full-native.log,picker-il.txt}` and the copied installed blueprint controls. Original live evidence: `/tmp/wildfire-warden-fixed-live/{placement-primary-stack.txt,Player-placement-failure.log}`.

This is actual native managed coordinate computation, not a running-game placement or worker proof. The controller must still verify preview movement/rotation, placement, path connection and the first staffed sortie on the corrected build. No Unity, desktop, game or deployment action was performed by the source-fix agent.
