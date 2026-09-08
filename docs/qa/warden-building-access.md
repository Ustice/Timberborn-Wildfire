# Warden station preview access correction

The deployed `8cbeb19` Ironteeth world failed while eagerly constructing the Warden toolbar preview, before simulation initialization. The actual game log `/tmp/wildfire-healthy-factions-live/Player-ironteeth-failure.log` reports `InvalidOperationException: More than one component of type Timberborn.Navigation.Accessible found in WardenStation.IronTeeth(Clone)` from `WardenStation.Awake → ComponentCache.GetCachedComponent` during `PreviewPlacerFactory.EagerlyCreateFirstPreview`.

## Cause and correction

Native building access is a named role, not a singleton component type. Installed modules produce:

- `Buildings`: BuildingAccessibleSpec → BuildingAccessible.
- `BuildingsNavigation`: BuildingSpec → ConstructionSiteAccessible.
- `AccessibleNavigation`: each IAccessibleNeeder → its own dedicated Accessible.

AccessibleInitializer first initializes the name and calls the originating role's SetAccessible. The names are `Building` and `ConstructionSite`. TemplateInstantiator.Instantiate invokes its cached dedicated initializers after inactive component/cache construction and **before** GameObject.SetActive invokes Awake. BuildingAccessible.Awake does not create or reassign that binding. EnterableSpec is not required to produce the two access roles; it remains necessary for native workplace entry and is preserved.

WardenStation now obtains `GetComponent<BuildingAccessible>().Accessible`. It does not take the first Accessible, delete native construction access, change blueprint decorators, or depend on which access instance occurs first. The station's existing finished-building entrance/access remains authoritative for refill and return.

## Regression evidence

`NativeWardenAccessTests` uses installed native modules and TemplateInstantiator.GetInstanceComponents to prove that one BuildingAccessible and one ConstructionSiteAccessible create exactly two Accessible components on the first query; a second query remains two. This distinguishes normal native composition from accumulating preview or duplicate module registration.

A separate call-site regression builds the actual native TypeIndexMap cache over two role components and their Accessible instances, invokes the installed AccessibleInitializer for both, then invokes production WardenStation.Awake. Both orders of the Accessible instances select the exact `Building` instance after the fix. Before the fix both reach ComponentCache's duplicate branch. Outside Unity its error formatter calls GameObject.name on an engine-less object and throws there, so the managed red run does **not** independently reproduce the final native exception string. The original game trace supplies that string. No fabricated GameObject liveness is used.

Focused result: 3 tests passed, zero skipped. Red and green logs: `/tmp/wildfire-warden-access-native-red.log` and `/tmp/wildfire-warden-access-green.log`. Installed IL and exact assembly hashes: `/tmp/wildfire-warden-building-access/`.

## Controller verification still required

The sole controller should exercise actual corrected Ironteeth eager preview startup, placement and one connected station. Confirm advancing simulation without the original Accessible exception, one assigned worker, and actual native refill/return access. A real-GameObject cache probe can additionally assert both named roles coexist and role selection survives reversed ordering. Managed tests do not execute GameObject.SetActive, the full preview lifecycle, navigation or refill. Keep the healthy pre-test save untouched and attach exact deployed DLL/source identity to the live result.

What would have prevented the bug: using the existing native role object at the adapter boundary and testing a normal constructed building's complete component topology. No additional registry, reflection compatibility seam or access-selection framework is needed.
