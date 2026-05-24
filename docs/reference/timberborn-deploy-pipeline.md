# Timberborn Deploy Pipeline

This page owns the local Wildfire deploy path for Timberborn validation. It is intentionally narrower than the full modding guide: build the adapter, stage only shippable files, and keep live QA serialized.

## Deployed Folder Shape

The direct deploy script writes this local mod folder:

```text
~/Documents/Timberborn/Mods/Wildfire/
  manifest.json
  Scripts/
    Wildfire.Timberborn.dll
    Wildfire.Core.dll
    Wildfire.Timberborn.pdb
    Wildfire.Core.pdb
```

The manifest identity is:

```json
{
  "Name": "Wildfire",
  "Version": "0.1.0.0",
  "Id": "JasonKleinberg.Wildfire",
  "MinimumGameVersion": "1.0.0.0"
}
```

Internal repository files are not copied. In particular, `docs/`, `kanban/`, source files, test files, and agent notes stay out of the deployed mod tree.

## Prerequisites

- Timberborn is installed for the current user.
- .NET can build `Wildfire.slnx`.
- Bun is available for TypeScript scripts.
- Timberborn should be closed for real deploy or cleanup unless the operator explicitly accepts `--allow-open-game`.

## Commands

Preview the exact build/deploy plan without writing the mod folder:

```bash
bun scripts/deploy-timberborn-mod.ts
```

Deploy for real when Timberborn is closed:

```bash
bun scripts/deploy-timberborn-mod.ts --apply --clean
```

Reuse an existing build output during script development:

```bash
bun scripts/deploy-timberborn-mod.ts --dry-run --skip-build
```

Remove the deployed Wildfire folder:

```bash
bun scripts/deploy-timberborn-mod.ts --apply --remove
```

Print usage:

```bash
bun scripts/deploy-timberborn-mod.ts --help
```

## Release Packaging

The release package command builds the Release adapter, stages the mod through the same deploy pipeline, validates required shippable files, and writes reviewable artifacts under `release/package/`:

```bash
bun run release:package
```

The package command stages into a repo-local Mods directory, not `~/Documents/Timberborn/Mods`, so it can build release artifacts while Timberborn is open without replacing another QA session's live install.

The default output is:

```text
release/package/
  Wildfire/
    manifest.json
    Scripts/
      Wildfire.Timberborn.dll
      Wildfire.Core.dll
      Wildfire.Timberborn.pdb
      Wildfire.Core.pdb
    ComputeShaders/
      wildfire_compute_mac
      wildfire_compute_mac.manifest
      wildfire_diagnostic_mac
      wildfire_diagnostic_mac.manifest
      wildfire_effects_mac
      wildfire_effects_mac.manifest
      wildfire_visual_mac
      wildfire_visual_mac.manifest
    GoodCollections/
    Goods/
    Localizations/
    NaturalResources/
    Sprites/
    TemplateCollections/
  Wildfire-0.1.0.0.zip
```

Use an artifact directory only when a ZIP is not needed:

```bash
bun scripts/package-release.ts --no-zip
```

Use a custom artifact root when a smoke test or downstream release step needs an alternate package name:

```bash
bun scripts/package-release.ts --artifact-name WildfireSmokeTest
```

During script development, already-built AssetBundles can be reused explicitly:

```bash
bun scripts/package-release.ts --deploy-arg --skip-asset-bundle
```

The package command fails unless it can validate:

- `manifest.json` contains non-empty `Id`, `Name`, `Version`, and `MinimumGameVersion`, and `Version` uses four numeric components.
- Required Release assemblies exist in `Scripts/`.
- Required compute, diagnostic, effects, and visual AssetBundles and bundle manifests exist in `ComputeShaders/`.
- Timberborn `Data` entries from `src/Wildfire.Timberborn/Data` are present at the mod root.
- The artifact does not contain `docs/`, `kanban/`, `.git/`, `src/`, `tests/`, `artifacts/`, `release/`, `WildfireQA/`, C# project files, solution files, or TypeScript source.
- The ZIP, when enabled, contains the selected artifact root folder and required files.

The command logs every packaged file as `artifact_file ...` so agents can paste or inspect a concise artifact inventory without launching Timberborn.

## Locking

The script serializes build/deploy/remove through:

```text
~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock
```

If another worker holds the lock, wait or use `--lock-timeout <seconds>`. Use `--force-lock` only after confirming the previous process is gone and the lock is stale.

## Expected Output

Successful dry-run output includes:

- `mod_id=JasonKleinberg.Wildfire`.
- `mod_version=0.1.0.0`.
- `mods_dir=.../Documents/Timberborn/Mods`.
- `target_dir=.../Documents/Timberborn/Mods/Wildfire`.
- One manifest write line.
- Copy lines for `Wildfire.Timberborn.dll` and `Wildfire.Core.dll`.
- `dry_run_complete`.

Successful real deploy output ends with `deploy_complete target_dir=.../Wildfire`.

## Live Evidence

After a real deploy, launch Timberborn and inspect:

```text
~/Library/Logs/Mechanistry/Timberborn/Player.log
```

The current deploy pipeline can place the folder and assemblies, but live load evidence still depends on Timberborn discovering this direct-deploy shape and on a future in-game hook that invokes the TWF-012 command bridge. If Timberborn is already running, do not replace the folder during someone else's QA session; capture that as the blocker and retry when the game is closed.
