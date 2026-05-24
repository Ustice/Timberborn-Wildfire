# Wildfire Versioning And Changelog Discipline

This page owns the lightweight release version rules for Wildfire. Keep it boring: one manifest version, one package version, one tag, and one changelog entry must agree before a release package is handed off.

## Version Sources

- `scripts/deploy-timberborn-mod.ts` owns the generated Timberborn `manifest.json` version.
- `CHANGELOG.md` owns the human-readable release notes for that same version.
- `bun run release:package` owns the packaged artifact name and ZIP validation.
- Release tags should be `v<manifest-version>`, for example `v0.1.0.0`.

## Version Format

Use four numeric components:

```text
major.minor.patch.build
```

The first three components follow normal release meaning. The fourth component keeps the Timberborn manifest format explicit and should usually be `0` for public releases.

## Bump Rules

- Prerelease or internal validation builds: increment the fourth component, for example `0.1.0.1`, when the package needs a distinct installable identity before the public release.
- Patch releases: increment the third component and reset the fourth component to `0`, for example `0.1.1.0`, for compatible fixes or release-note corrections.
- Minor releases: increment the second component and reset patch/build to `0`, for example `0.2.0.0`, for new player-facing behavior, new settings, or meaningful compatibility changes.
- Release-candidate builds: use the intended release version in the manifest and changelog, then tag the candidate outside the shipped manifest name only if the release coordinator explicitly asks for it. Do not invent `rc` text inside `manifest.json`; Timberborn expects four numeric components.

## Release Checklist

1. Update the version in `scripts/deploy-timberborn-mod.ts`.

2. Add or promote the matching `CHANGELOG.md` entry.

   - Keep the heading exactly as `## [x.y.z.w] - YYYY-MM-DD` for a final release.
   - `Unreleased` is acceptable while preparing a package that has not been tagged yet.

3. Run the version validation before release handoff.

```bash
bun scripts/package-release.ts --no-zip --version 0.1.0.0 --deploy-arg --skip-build --deploy-arg --skip-asset-bundle
```

4. For a tagged release candidate or final release, validate the tag too.

```bash
bun run release:package -- --tag v0.1.0.0
```

5. Confirm the package output reports the same version everywhere.

Expected release package output includes:

- `manifest_version=x.y.z.w`.
- `release_version=x.y.z.w`.
- `release_tag=vx.y.z.w` when `--tag` or a release-tag environment variable is supplied.
- `changelog_entry=CHANGELOG.md#[x.y.z.w]`.
- `release_package_zip=.../Wildfire-x.y.z.w.zip` when ZIP output is enabled.

## Validation Contract

`scripts/package-release.ts` fails when:

- The generated manifest version is not four numeric components.
- `--version` does not match `manifest.json`.
- `--tag`, `WILDFIRE_RELEASE_TAG`, or a GitHub tag ref does not normalize to the manifest version.
- `CHANGELOG.md` does not contain a `## [manifest-version] - ...` entry.
- The package ZIP name does not include the manifest version.
