import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";

export type ReleaseManifest = {
  Id?: unknown;
  MinimumGameVersion?: unknown;
  Name?: unknown;
  Version?: unknown;
};

export type ValidatedReleaseManifest = {
  Id: string;
  MinimumGameVersion: string;
  Name: string;
  Version: string;
};

export type ArtifactSummary = {
  directory: string;
  fileCount: number;
  files: string[];
  manifest: ValidatedReleaseManifest;
  zipPath?: string;
};

export const modFolderName = "Wildfire";
export const workshopVersionFolderName = "version-1.0";

const repoRoot = resolve(import.meta.dir, "..");
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

const fail = (message: string): never => {
  throw new Error(`[wildfire-release-validation] ${message}`);
};

const readJson = <T>(path: string): T => {
  try {
    return JSON.parse(readFileSync(path, "utf8")) as T;
  } catch (error) {
    return fail(`Could not parse JSON at ${path}: ${error instanceof Error ? error.message : String(error)}`);
  }
};

export const requireFile = (path: string): void => {
  if (!existsSync(path) || !statSync(path).isFile()) {
    fail(`Required release file is missing: ${path}`);
  }
};

export const requireDirectory = (path: string): void => {
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

export const validateManifest = (artifactDir: string): ValidatedReleaseManifest => {
  const manifestPath = join(artifactDir, "manifest.json");
  requireFile(manifestPath);
  const manifest = readJson<ReleaseManifest>(manifestPath);
  const requiredKeys = ["Id", "MinimumGameVersion", "Name", "Version"] as const;

  requiredKeys
    .filter((key) => typeof manifest[key] !== "string" || !manifest[key].trim())
    .forEach((key) => fail(`manifest.json must contain a non-empty string ${key}.`));

  if (!/^\d+\.\d+\.\d+\.\d+$/u.test(manifest.Version as string)) {
    fail(`manifest.json Version must use four numeric components: ${String(manifest.Version)}`);
  }

  return {
    Id: manifest.Id as string,
    MinimumGameVersion: manifest.MinimumGameVersion as string,
    Name: manifest.Name as string,
    Version: manifest.Version as string,
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

export const validateTimberbornModArtifact = (
  artifactDir: string,
  artifactName: string,
  zipPath?: string,
): ArtifactSummary => {
  requireDirectory(artifactDir);
  const manifest = validateManifest(artifactDir);
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
    zipPath,
  };
};
