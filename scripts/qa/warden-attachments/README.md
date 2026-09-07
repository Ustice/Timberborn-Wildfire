# Warden native attachment probe

From the repository root, with the shared game/Unity controller idle:

```sh
python3 scripts/qa/warden-attachments/run-probe.py
```

This acquires the existing build/deploy lock, creates a temporary copy of the
minimal Unity project, compiles the actual production provider/configurator
source, and loads the installed native assemblies into licensed Unity. It never
launches Timberborn or deploys a mod. Temporary assemblies and probe materials
are test inputs only. Paths can be overridden with `--managed` and `--unity`.
The script retains its temporary project and logs for diagnosis.

The Play Mode probe exercises native `AssetLoader` normalization and all four
explicit prefab names, repeated cached lookup, wrong-type and unrelated-name
rejection, no broad-scan side effects, and Reset followed by fresh creation. It
waits for Unity's deferred destruction and verifies old wrappers are destroyed
while imported clones survive. This uses Unity's actual GameObject/Destroy and
Timberborn's actual `TimbermeshDescription`, not substitute native types.

For the existing helmet and three split native meshes, the actual native
`TimbermeshImporter` creates 6,020 triangles. Its isolated material repository
allows only the four expected native material names, reusing one temporary test
material per name. This does **not** test native material collection loading,
atlasing, optimized-prefab caching, character attachment fit, or worn movement.
Those require a copied-save game session and remain open.

`IAssetLoader` and the native file providers are Bootstrapper singletons in the
installed game. The separate Bootstrapper configurator therefore contributes the
provider before the loader captures its provider list. Loader calls and Reset
are synchronous; actual construction/destruction must remain on Unity's main
thread, as for native providers. The provider has no tasks, worker threads,
static cache, saved visual state or independently allocated materials. It owns
only the inert source descriptors and clears them on native asset-loader Reset.
The native optimizer owns imported clones separately.

No character blueprint currently asks for these prefabs. This is a staged
provisioning prototype, not an equipped-visual feature. The four reserved IDs are
`Wildfire/Characters/Attachments/Warden{Helmet,Tank,Wand,Hose}.IronTeeth`.
The merged preview meshes and coat are unchanged.
