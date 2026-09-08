# Shared personal inventory district registration

Source `1c2bdfa`, based on root `100a6e4`, extracts the already-corrected Warden/satchel district lifecycle. It adds no worker, equipment policy, template, scheduler, cargo or runtime activation. No engine/game/deploy action was taken.

## Implementation and preserved interface

`PersonalInventoryDistrictRegistration` owns one inventory's native Citizen/Character event subscriptions, district registry and counter membership, private IGoodProcessor proxy, once-only Exited state, and guarded Restore/Exit implementation. Both components construct it during Awake using their native private inventory and the existing runtime resource coordinator. There is one required extra-admission predicate: Warden returns true (the helper independently requires Character.Alive); satchel additionally requires !Mortal.Dead && !Mortal.ShouldDie. This predicate is evaluated before registration and after native registry callbacks.

Native inventory initialization, capacities, goods, stock/reservation operations, loaded state, worker phases and public Live admission remain in their components. Satchel reads helper.Exited first in its existing Live predicate. There is no new save field, cargo ledger, equipment base class, role enum, options builder or transfer abstraction. Public component constructors remain the sole existing coordinator constructors; actual native Bindito constructor-selection regressions still pass.

The shared implementation retains one outer resource exclusion guard, the registry callback postchecks, poison-before-warning ordering and once-only exit. Neither diagnostics nor failed cleanup can abort later native death/delete. The existing private string sink and original diagnostic prefixes remain at each component; a single exception delegate performs their formatting inside the helper's best-effort catch. The callback-drift exception now names personal inventory rather than one component; its type/failure semantics are unchanged.

## Source order and managed evidence

Installed TemplateInstantiator.Instantiate performs dedicated decorator initializers before GameObject.SetActive. BaseInstantiator initializes the common ComponentCache first. BaseComponentUnityAdapter.OnEnable calls ComponentCache.SetActive, which invokes IAwakableComponent.Awake. Thus the private Inventory is bound and shares the owner's native cache when helper construction resolves Citizen/Character. Actual fixture setup follows this order. Character's native constructor establishes Alive=true before component Awake/Initialize, as the existing tests verify.

The existing native load audit remains applicable: SingletonLifecycleService.LoadAll runs singleton Load before non-singleton entity loading/initialization; post-load entity callbacks follow. The runtime world-transition guard reset therefore still precedes these entity-registration operations. No load, reset or subscription timing was moved by this extraction.

Both actual native lifecycle suites survive the extraction: Citizen-first Character.KillCharacter, native EntityComponent.Delete and later events, enable-time death, district unassignment, busy capture/write, existing poison, one-time unsubscribe, no retry, throwing/reentrant diagnostic, exception-formatting failure and satchel exit admission. Earlier satchel tests that directly called its removed OnDied method now call actual Character.KillCharacter.

Six additional cases verify actual Awake-wired admission with native Mortal.DieSilentlyAsSoonAsPossible (pending death excludes satchel but does not add a Warden policy), and registry callbacks that exit the owner or schedule pending satchel death before counter publication. Both successful controls preserve native stock. For registry mutation tests, the private RegisterDistrict(DistrictCenter) segment executes within the same guard using actual native registry/counter methods and supplied current district references. It begins after HasAssignedDistrict admission because that native getter evaluates Unity GameObject truth. No positive Unity liveness is fabricated.

Focused lifecycle/satchel run: **75 passed**. Counterfactual removal of only post-Registry.Add revalidation: **2 failed, 2 passed**; the failures observe forbidden counter publication after exit or newly pending satchel death. Restored source full native suite: **1,442 passed, zero failures or skips**. Commands:

```sh
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj --filter 'FullyQualifiedName~NativeWardenDistrictLifecycleTests|FullyQualifiedName~NativeFertilizer'
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj
```

Logs and exact native IL: `/tmp/wildfire-personal-inventory-registration/` (`focused-complete.log`, `counterfactual-red.log`, `full-native.log`, `template-il.txt`, `base-instantiator-il.txt`, `cache-il.txt`, `unity-adapter-il.txt`).

## Size and limits

Production changed from 416 lines across WardenEquipment/Satchel to 341 total: helper134, Warden74, satchel133 (**net -75 lines**). The two previous private counter types became one helper and one shared private counter (**net zero types**); no interface type was added. Caller lifecycle construction/delegation is 8 Warden lines and 11 satchel lines, including spacing. Across source and tests the commit has 256 additions/237 deletions; this is not a claim that tests became smaller.

Managed fixtures prove actual native methods against supplied caches and dependencies, not full live district admission, Unity liveness or a complete actor world lifecycle. The helper does not fix or change on-duty Warden selection, sortie/suppression policy, fertilizer worker dispatch, save-resume jobs or application runtime followups. Controller acceptance should exercise existing Warden district assignment, charge/return, save/load and native teardown on a disposable world. Satchel remains unbound/inactive. No engine proof is claimed for this extraction.
