#!/usr/bin/env bun

import {
  cpSync,
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  rmSync,
  statSync,
} from "fs";
import { dirname, join, relative, resolve } from "path";

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

type ReleaseManifest = {
  Id?: unknown;
  MinimumGameVersion?: unknown;
  Name?: unknown;
  Version?: unknown;
};

type ValidatedReleaseManifest = {
  Id: string;
  MinimumGameVersion: string;
  Name: string;
  Version: string;
};

type ArtifactSummary = {
  changelogEntry: string;
  directory: string;
  fileCount: number;
  files: string[];
  manifest: ValidatedReleaseManifest;
  releaseTag?: string;
  releaseVersion: string;
  zipPath?: string;
};

const repoRoot = resolve(import.meta.dir, "..");
const modFolderName = "Wildfire";
const privateComputeShaderFolderName = "ComputeShaders";
const requiredAssemblies = ["Wildfire.Timberborn.dll", "Wildfire.Core.dll"];
const requiredBundles = [
  "wildfire_compute_mac",
  "wildfire_diagnostic_mac",
  "wildfire_effects_mac",
  "wildfire_visual_mac",
];
const requiredBundleManifests = requiredBundles.map((name) => `${name}.manifest`);
const blockedRootEntries = new Set([
  ".git",
  "artifacts",
  "docs",
  "kanban",
  "release",
  "src",
  "tests",
  "TestResults",
  "WildfireQA",
]);
const blockedFileExtensions = new Set([
  ".cs",
  ".csproj",
  ".sln",
  ".slnx",
  ".ts",
  ".tsx",
]);

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

const readJson = <T>(path: string): T => {
  try {
    return JSON.parse(readFileSync(path, "utf8")) as T;
  } catch (error) {
    return fail(`Could not parse JSON at ${path}: ${error instanceof Error ? error.message : String(error)}`);
  }
};

const requireFile = (path: string): void => {
  if (!existsSync(path) || !statSync(path).isFile()) {
    fail(`Required release file is missing: ${path}`);
  }
};

const requireDirectory = (path: string): void => {
  if (!existsSync(path) || !statSync(path).isDirectory()) {
    fail(`Required release directory is missing: ${path}`);
  }
};

const walkFiles = (root: string): string[] =>
  readdirSync(root, { withFileTypes: true }).flatMap((entry) => {
    const path = join(root, entry.name);
    if (entry.isDirectory()) {
      return walkFiles(path);
    }

    return entry.isFile() ? [path] : [];
  });

const validateManifest = (artifactDir: string): ArtifactSummary["manifest"] => {
  const manifestPath = join(artifactDir, "manifest.json");
  requireFile(manifestPath);
  const manifest = readJson<ReleaseManifest>(manifestPath);
  const requiredKeys = ["Id", "MinimumGameVersion", "Name", "Version"] as const;

  requiredKeys
    .filter((key) => typeof manifest[key] !== "string" || !manifest[key].trim())
    .forEach((key) => fail(`manifest.json must contain a non-empty string ${key}.`));

  if (!versionPattern.test(manifest.Version as string)) {
    fail(`manifest.json Version must use four numeric components: ${String(manifest.Version)}`);
  }

  return {
    Id: manifest.Id as string,
    MinimumGameVersion: manifest.MinimumGameVersion as string,
    Name: manifest.Name as string,
    Version: manifest.Version as string,
  };
};

const validateReleaseIdentity = (
  manifest: ArtifactSummary["manifest"],
  options: Options,
  zipPath?: string,
): Pick<ArtifactSummary, "changelogEntry" | "releaseTag" | "releaseVersion"> => {
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

const validateRequiredFiles = (artifactDir: string): void => {
  requireDirectory(join(artifactDir, "Scripts"));
  requireDirectory(join(artifactDir, privateComputeShaderFolderName));

  [
    ...requiredAssemblies.map((name) => join(artifactDir, "Scripts", name)),
    ...requiredBundles.map((name) => join(artifactDir, privateComputeShaderFolderName, name)),
    ...requiredBundleManifests.map((name) => join(artifactDir, privateComputeShaderFolderName, name)),
  ].forEach(requireFile);
};

const validateModData = (artifactDir: string): void => {
  const sourceDataDir = join(repoRoot, "src", "Wildfire.Timberborn", "Data");
  if (!existsSync(sourceDataDir)) {
    return;
  }

  readdirSync(sourceDataDir, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() || entry.isFile())
    .map((entry) => join(artifactDir, entry.name))
    .forEach((path) => {
      if (!existsSync(path)) {
        fail(`Timberborn data entry was not packaged: ${path}`);
      }
    });
};

const validateExclusions = (artifactDir: string, files: string[]): void => {
  const blockedEntries = files
    .map((path) => relative(artifactDir, path))
    .filter((path) => {
      const segments = path.split(/[\\/]/u);
      const extension = path.slice(path.lastIndexOf("."));

      return segments.some((segment) => blockedRootEntries.has(segment)) || blockedFileExtensions.has(extension);
    });

  if (blockedEntries.length > 0) {
    fail(`Release artifact contains blocked files: ${blockedEntries.join(", ")}`);
  }
};

const validateBundleManifestText = (artifactDir: string): void => {
  requiredBundleManifests
    .map((name) => join(artifactDir, privateComputeShaderFolderName, name))
    .forEach((path) => {
      const text = readFileSync(path, "utf8");
      if (!text.includes("Assets/WildfireGenerated/")) {
        fail(`AssetBundle manifest does not describe generated Wildfire assets: ${path}`);
      }
    });
};

const createZip = (artifactDir: string, zipPath: string, artifactName: string): void => {
  rmSync(zipPath, { force: true });
  mkdirSync(dirname(zipPath), { recursive: true });

  run("zip", ["-qry", zipPath, artifactName], dirname(artifactDir));
};

const validateZip = (zipPath: string, artifactName: string): void => {
  requireFile(zipPath);
  const result = Bun.spawnSync(["unzip", "-Z1", zipPath], {
    cwd: repoRoot,
    stdout: "pipe",
    stderr: "pipe",
  });

  if (result.exitCode !== 0) {
    fail(`Could not inspect release zip ${zipPath}: ${result.stderr.toString()}`);
  }

  const entries = result.stdout.toString().split(/\r?\n/u).filter(Boolean);
  const blockedEntries = entries.filter((entry) => entry.startsWith("__MACOSX/") || entry.includes("/._"));
  const requiredZipEntries = [
    `${artifactName}/manifest.json`,
    `${artifactName}/Scripts/Wildfire.Timberborn.dll`,
    `${artifactName}/${privateComputeShaderFolderName}/wildfire_compute_mac`,
  ];

  if (blockedEntries.length > 0) {
    fail(`Release zip contains macOS resource-fork entries: ${blockedEntries.join(", ")}`);
  }

  requiredZipEntries
    .filter((entry) => !entries.includes(entry))
    .forEach((entry) => fail(`Release zip is missing ${entry}`));
};

const validateArtifact = (
  artifactDir: string,
  artifactName: string,
  options: Options,
  zipPath?: string,
): ArtifactSummary => {
  requireDirectory(artifactDir);
  const manifest = validateManifest(artifactDir);
  const releaseIdentity = validateReleaseIdentity(manifest, options, zipPath);
  validateRequiredFiles(artifactDir);
  validateModData(artifactDir);
  validateBundleManifestText(artifactDir);

  const files = walkFiles(artifactDir).map((path) => relative(artifactDir, path)).sort();
  validateExclusions(artifactDir, files.map((path) => join(artifactDir, path)));

  if (zipPath) {
    validateZip(zipPath, artifactName);
  }

  return {
    directory: artifactDir,
    fileCount: files.length,
    files,
    manifest,
    ...releaseIdentity,
    zipPath,
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

  const summary = validateArtifact(artifactDir, options.artifactName, options, zipPath);
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
