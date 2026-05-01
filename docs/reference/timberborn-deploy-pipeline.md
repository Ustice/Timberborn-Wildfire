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
