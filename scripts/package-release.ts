#!/usr/bin/env bun

import { cpSync, mkdirSync, readFileSync, rmSync } from "fs";
import { dirname, join, resolve } from "path";
import {
  modFolderName,
  requireDirectory,
  requireFile,
  validateManifest,
  validateTimberbornModArtifact,
  type ArtifactSummary,
  type ValidatedReleaseManifest,
} from "./release-package-validation.ts";

type Options = {
  artifactName: string;
  clean: boolean;
  configuration: string;
  deployArgs: string[];
  expectedTag?: string;
  expectedVersion?: string;
  outputDir: string;
  stagingModsDir: string;
  zip: boolean;
};

type ReleaseIdentitySummary = {
  changelogEntry: string;
  releaseTag?: string;
  releaseVersion: string;
};

type ReleaseArtifactSummary = ArtifactSummary & ReleaseIdentitySummary;

const repoRoot = resolve(import.meta.dir, "..");

const usage = `Usage: bun scripts/package-release.ts [options]

Options:
  --configuration <name>  Build configuration forwarded to deploy-timberborn-mod.ts. Default: Release.
  --output <path>         Release output directory. Default: release/package.
  --staging-mods <path>   Temporary staging Mods directory. Default: release/package/staging-mods.
  --artifact-name <name>  Artifact directory name. Default: Wildfire.
  --version <version>     Expected release version. Must match manifest.json Version.
  --tag <tag>             Expected release tag. Accepts v-prefixed tags such as v0.1.0.0.
  --no-clean              Keep existing staging and output directories.
  --no-zip                Skip ZIP creation and produce only the artifact directory.
  --deploy-arg <arg>      Extra argument forwarded to deploy-timberborn-mod.ts.
  --help                  Show this help.

Examples:
  bun run release:package
  bun scripts/package-release.ts --no-zip
  bun scripts/package-release.ts --version 0.1.0.0 --tag v0.1.0.0
  bun scripts/package-release.ts --artifact-name WildfireSmokeTest
  bun scripts/package-release.ts --deploy-arg --skip-asset-bundle`;

const log = (message: string): void => {
  console.log(`[wildfire-release-package] ${message}`);
};

const fail = (message: string): never => {
  throw new Error(`[wildfire-release-package] ${message}`);
};

const requireValue = (args: string[], index: number, flag: string, allowFlagValue = false): string => {
  const value = args[index];
  if (!value || (!allowFlagValue && value.startsWith("--"))) {
    fail(`${flag} requires a value.`);
  }

  return value;
};

const parseArgs = (args: string[]): Options => {
  const consumedIndexes = new Set<number>();

  return args.reduce<Options>(
    (options, arg, index) => {
      if (consumedIndexes.has(index)) {
        return options;
      }

      if (arg === "--configuration") {
        consumedIndexes.add(index + 1);
        return { ...options, configuration: requireValue(args, index + 1, arg) };
      }

      if (arg.startsWith("--configuration=")) {
        return { ...options, configuration: arg.slice("--configuration=".length) };
      }

      if (arg === "--output") {
        consumedIndexes.add(index + 1);
        return { ...options, outputDir: requireValue(args, index + 1, arg) };
      }

      if (arg.startsWith("--output=")) {
        return { ...options, outputDir: arg.slice("--output=".length) };
      }

      if (arg === "--staging-mods") {
        consumedIndexes.add(index + 1);
        return { ...options, stagingModsDir: requireValue(args, index + 1, arg) };
      }

      if (arg.startsWith("--staging-mods=")) {
        return { ...options, stagingModsDir: arg.slice("--staging-mods=".length) };
      }

      if (arg === "--artifact-name") {
        consumedIndexes.add(index + 1);
        return { ...options, artifactName: requireValue(args, index + 1, arg) };
      }

      if (arg.startsWith("--artifact-name=")) {
        return { ...options, artifactName: arg.slice("--artifact-name=".length) };
      }

      if (arg === "--deploy-arg") {
        consumedIndexes.add(index + 1);
        return { ...options, deployArgs: [...options.deployArgs, requireValue(args, index + 1, arg, true)] };
      }

      if (arg.startsWith("--deploy-arg=")) {
        return { ...options, deployArgs: [...options.deployArgs, arg.slice("--deploy-arg=".length)] };
      }

      if (arg === "--tag") {
        consumedIndexes.add(index + 1);
        return { ...options, expectedTag: requireValue(args, index + 1, arg) };
      }

      if (arg.startsWith("--tag=")) {
        return { ...options, expectedTag: arg.slice("--tag=".length) };
      }

      if (arg === "--version") {
        consumedIndexes.add(index + 1);
        return { ...options, expectedVersion: requireValue(args, index + 1, arg) };
      }

      if (arg.startsWith("--version=")) {
        return { ...options, expectedVersion: arg.slice("--version=".length) };
      }

      if (arg === "--no-clean") {
        return { ...options, clean: false };
      }

      if (arg === "--no-zip") {
        return { ...options, zip: false };
      }

      return fail(`Unknown argument: ${arg}`);
    },
    {
      artifactName: modFolderName,
      clean: true,
      configuration: "Release",
      deployArgs: [],
      outputDir: "release/package",
      stagingModsDir: "release/package/staging-mods",
      zip: true,
    },
  );
};

const validateOptions = (options: Options): void => {
  if (!options.artifactName.trim()) {
    fail("--artifact-name must not be empty.");
  }

  if (options.artifactName === "." || options.artifactName === ".." || /[\\/]/u.test(options.artifactName)) {
    fail("--artifact-name must be a directory name, not a path.");
  }
};

const versionPattern = /^\d+\.\d+\.\d+\.\d+$/u;

const normalizeReleaseTag = (tag: string): string => {
  const normalized = tag.replace(/^refs\/tags\//u, "").replace(/^v/u, "");
  if (!versionPattern.test(normalized)) {
    fail(`Release tag must be v-prefixed or bare four-part version: ${tag}`);
  }

  return normalized;
};

const readExpectedTag = (options: Options): string | undefined => {
  const githubTag =
    process.env.GITHUB_REF_TYPE === "tag"
      ? process.env.GITHUB_REF_NAME
      : process.env.GITHUB_REF?.startsWith("refs/tags/")
        ? process.env.GITHUB_REF
        : undefined;

  return options.expectedTag ?? process.env.WILDFIRE_RELEASE_TAG ?? githubTag;
};

const run = (command: string, args: string[], cwd = repoRoot): void => {
  log(`run ${command} ${args.map((arg) => (arg.includes(" ") ? `"${arg}"` : arg)).join(" ")}`);
  const result = Bun.spawnSync([command, ...args], {
    cwd,
    stdout: "inherit",
    stderr: "inherit",
  });

  if (result.exitCode !== 0) {
    fail(`${command} exited with code ${result.exitCode}.`);
  }
};

const validateReleaseIdentity = (
  manifest: ValidatedReleaseManifest,
  options: Options,
  zipPath?: string,
): ReleaseIdentitySummary => {
  const releaseVersion = manifest.Version;
  if (options.expectedVersion && options.expectedVersion !== releaseVersion) {
    fail(`--version ${options.expectedVersion} does not match manifest.json Version ${releaseVersion}.`);
  }

  const expectedTag = readExpectedTag(options);
  const normalizedTagVersion = expectedTag ? normalizeReleaseTag(expectedTag) : undefined;
  if (normalizedTagVersion && normalizedTagVersion !== releaseVersion) {
    fail(`Release tag ${expectedTag} does not match manifest.json Version ${releaseVersion}.`);
  }

  if (zipPath && !zipPath.endsWith(`-${releaseVersion}.zip`)) {
    fail(`Release zip name must include manifest version ${releaseVersion}: ${zipPath}`);
  }

  const changelogPath = join(repoRoot, "CHANGELOG.md");
  requireFile(changelogPath);
  const changelog = readFileSync(changelogPath, "utf8");
  const changelogHeading = `## [${releaseVersion}] - `;
  if (!changelog.includes(changelogHeading)) {
    fail(`CHANGELOG.md must contain a release heading for ${releaseVersion}.`);
  }

  return {
    changelogEntry: `CHANGELOG.md#[${releaseVersion}]`,
    releaseTag: expectedTag,
    releaseVersion,
  };
};

const createZip = (artifactDir: string, zipPath: string, artifactName: string): void => {
  rmSync(zipPath, { force: true });
  mkdirSync(dirname(zipPath), { recursive: true });

  run("zip", ["-qry", zipPath, artifactName], dirname(artifactDir));
};

const validateReleaseArtifact = (
  artifactDir: string,
  artifactName: string,
  options: Options,
  zipPath?: string,
): ReleaseArtifactSummary => {
  const artifactSummary = validateTimberbornModArtifact(artifactDir, artifactName, zipPath);
  const releaseIdentity = validateReleaseIdentity(artifactSummary.manifest, options, zipPath);

  return {
    ...artifactSummary,
    ...releaseIdentity,
  };
};

const main = (): void => {
  const args = Bun.argv.slice(2);
  if (args.includes("--help") || args.includes("-h")) {
    console.log(usage);
    return;
  }

  const options = parseArgs(args);
  validateOptions(options);

  const outputDir = resolve(repoRoot, options.outputDir);
  const stagingModsDir = resolve(repoRoot, options.stagingModsDir);
  const stagingModDir = join(stagingModsDir, modFolderName);
  const artifactDir = join(outputDir, options.artifactName);

  if (options.clean) {
    rmSync(stagingModsDir, { recursive: true, force: true });
    rmSync(artifactDir, { recursive: true, force: true });
  }

  run("bun", [
    "scripts/deploy-timberborn-mod.ts",
    "--apply",
    "--clean",
    "--configuration",
    options.configuration,
    "--mods-dir",
    stagingModsDir,
    "--lock-timeout",
    "60",
    "--allow-open-game",
    ...options.deployArgs,
  ]);

  requireDirectory(stagingModDir);
  mkdirSync(dirname(artifactDir), { recursive: true });
  cpSync(stagingModDir, artifactDir, { recursive: true });

  const manifest = validateManifest(artifactDir);
  const zipPath = options.zip ? join(outputDir, `${options.artifactName}-${manifest.Version}.zip`) : undefined;
  if (zipPath) {
    createZip(artifactDir, zipPath, options.artifactName);
  }

  const summary = validateReleaseArtifact(artifactDir, options.artifactName, options, zipPath);
  log(`release_package_ready directory=${summary.directory}`);
  if (summary.zipPath) {
    log(`release_package_zip=${summary.zipPath}`);
  }
  log(`manifest_id=${summary.manifest.Id}`);
  log(`manifest_version=${summary.manifest.Version}`);
  log(`release_version=${summary.releaseVersion}`);
  if (summary.releaseTag) {
    log(`release_tag=${summary.releaseTag}`);
  }
  log(`changelog_entry=${summary.changelogEntry}`);
  log(`file_count=${summary.fileCount}`);
  summary.files.forEach((file) => log(`artifact_file ${file}`));
};

main();
