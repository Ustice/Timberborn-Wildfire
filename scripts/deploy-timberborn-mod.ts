#!/usr/bin/env bun

import {
  copyFileSync,
  existsSync,
  cpSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  rmSync,
  statSync,
  writeFileSync,
} from "fs";
import { dirname, join, resolve } from "path";

type DeployOptions = {
  allowOpenGame: boolean;
  clean: boolean;
  configuration: string;
  dryRun: boolean;
  forceLock: boolean;
  help: boolean;
  lockTimeoutSeconds: number;
  modsDir: string;
  remove: boolean;
  skipAssetBundle: boolean;
  skipBuild: boolean;
  unityExecutable: string;
  targetFramework: string;
};

type CopyPlan = {
  from: string;
  to: string;
  required: boolean;
};

type AssetBundleArtifact = {
  builderMethod: string;
  logName: string;
  manifestName: string;
  name: string;
  requiredAsset: string;
  // When set, replaces the default "--shader <computeShaderPath>" args passed to the builder.
  extraCliArgs?: string[];
};

const repoRoot = resolve(import.meta.dir, "..");
const home = process.env.HOME ?? "";
const defaultModsDir = join(home, "Documents", "Timberborn", "Mods");
const lockDir = join(home, "Library", "Application Support", "Timberborn", "WildfireQA", "locks", "build-deploy.lock");
const lockInfoPath = join(lockDir, "lock.json");
const modFolderName = "Wildfire";
const unityProjectPath = join(repoRoot, "src", "Wildfire.Unity", "UnityBatchmodeProject");
const computeShaderPath = join(repoRoot, "src", "Wildfire.Unity", "FireSim.compute");
const timberbornDataPath = join(repoRoot, "src", "Wildfire.Timberborn", "Data");
const flameShaderPath   = join(repoRoot, "src", "Wildfire.Unity", "WildfireFlame.shader");
const cloudShaderPath   = join(repoRoot, "src", "Wildfire.Unity", "WildfireCloud.shader");
const smoothShaderPath  = join(repoRoot, "src", "Wildfire.Unity", "WildfireSmoothing.compute");
const ashOverlayShaderPath = join(repoRoot, "src", "Wildfire.Unity", "AshOverlay.shader");
const defaultUnityExecutable =
  process.env.WILDFIRE_UNITY_EXECUTABLE ??
  "/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity";
const assetBundleTarget = "StandaloneOSX";
const privateComputeShaderFolderName = "ComputeShaders";
const assetBundleArtifacts: AssetBundleArtifact[] = [
  {
    builderMethod: "Wildfire.UnityBatchmode.FireSimAssetBundleBuilder.Build",
    logName: "assetbundle-build.log",
    manifestName: "wildfire_compute_mac.manifest",
    name: "wildfire_compute_mac",
    requiredAsset: "Assets/WildfireGenerated/FireSim.compute",
  },
  {
    builderMethod: "Wildfire.UnityBatchmode.DiagnosticTextAssetBundleBuilder.Build",
    logName: "diagnostic-assetbundle-build.log",
    manifestName: "wildfire_diagnostic_mac.manifest",
    name: "wildfire_diagnostic_mac",
    requiredAsset: "Assets/WildfireGenerated/Diagnostic.txt",
  },
  {
    builderMethod: "Wildfire.UnityBatchmode.WildfireEffectsAssetBundleBuilder.Build",
    logName: "effects-assetbundle-build.log",
    manifestName: "wildfire_effects_mac.manifest",
    name: "wildfire_effects_mac",
    requiredAsset: "Assets/WildfireGenerated/WildfireFlame.shader",
    extraCliArgs: ["--flame", flameShaderPath, "--cloud", cloudShaderPath, "--smooth", smoothShaderPath],
  },
  {
    builderMethod: "Wildfire.UnityBatchmode.AshOverlayAssetBundleBuilder.Build",
    logName: "ash-overlay-assetbundle-build.log",
    manifestName: "wildfire_visual_mac.manifest",
    name: "wildfire_visual_mac",
    requiredAsset: "Assets/WildfireGenerated/AshOverlay.shader",
    extraCliArgs: ["--shader", ashOverlayShaderPath],
  },
];
const manifest = {
  Name: "Wildfire",
  Version: "0.1.0.0",
  Id: "JasonKleinberg.Wildfire",
  MinimumGameVersion: "1.0.0.0",
  Description: "Wildfire cellular-automata fire simulation adapter scaffold.",
  RequiredMods: [],
  OptionalMods: [],
};
const requiredAssemblies = ["Wildfire.Timberborn.dll", "Wildfire.Core.dll"];
const optionalAssemblies = ["Wildfire.Timberborn.pdb", "Wildfire.Core.pdb"];

const log = (message: string): void => {
  console.log(`[wildfire-deploy] ${message}`);
};

const fail = (message: string): never => {
  throw new Error(`[wildfire-deploy] ${message}`);
};

const usage = `Usage:
  bun scripts/deploy-timberborn-mod.ts [options]

Options:
  --dry-run                 Print the build/deploy plan without writing the mod folder. Default.
  --apply                   Write the deployed mod folder. Refuses while Timberborn is open unless --allow-open-game is set.
  --skip-build              Reuse existing build output instead of running dotnet build.
  --skip-asset-bundle       Reuse existing FireSim and diagnostic AssetBundles instead of running Unity batchmode.
  --unity-executable <path> Unity Editor executable. Default: WILDFIRE_UNITY_EXECUTABLE or Unity Hub 6000.3.6f1.
  --configuration <name>    Build configuration. Default: Debug.
  --target-framework <tfm>  Timberborn adapter target framework. Default: netstandard2.1.
  --mods-dir <path>         Timberborn Mods directory. Default: ~/Documents/Timberborn/Mods.
  --clean                   Remove the target mod folder before copying files.
  --remove                  Remove the deployed Wildfire mod folder and exit.
  --allow-open-game         Allow real deploy/remove while Timberborn appears to be running.
  --lock-timeout <seconds>  Seconds to wait for the deploy lock. Default: 0.
  --force-lock              Remove an existing deploy lock before acquiring it.
  --help                    Show this help.

Examples:
  bun scripts/deploy-timberborn-mod.ts
  bun scripts/deploy-timberborn-mod.ts --apply
  bun scripts/deploy-timberborn-mod.ts --dry-run --remove
`;

const parseArgs = (args: string[]): DeployOptions => {
  const options: DeployOptions = {
    allowOpenGame: false,
    clean: false,
    configuration: "Debug",
    dryRun: true,
    forceLock: false,
    help: false,
    lockTimeoutSeconds: 0,
    modsDir: defaultModsDir,
    remove: false,
    skipAssetBundle: false,
    skipBuild: false,
    unityExecutable: defaultUnityExecutable,
    targetFramework: "netstandard2.1",
  };

  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index] ?? "";

    if (arg === "--allow-open-game") {
      options.allowOpenGame = true;
    } else if (arg === "--apply") {
      options.dryRun = false;
    } else if (arg === "--clean") {
      options.clean = true;
    } else if (arg === "--configuration") {
      options.configuration = requireValue(args, ++index, arg);
    } else if (arg.startsWith("--configuration=")) {
      options.configuration = arg.slice("--configuration=".length);
    } else if (arg === "--dry-run") {
      options.dryRun = true;
    } else if (arg === "--force-lock") {
      options.forceLock = true;
    } else if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--lock-timeout") {
      options.lockTimeoutSeconds = parseTimeout(requireValue(args, ++index, arg));
    } else if (arg.startsWith("--lock-timeout=")) {
      options.lockTimeoutSeconds = parseTimeout(arg.slice("--lock-timeout=".length));
    } else if (arg === "--target-framework") {
      options.targetFramework = requireValue(args, ++index, arg);
    } else if (arg.startsWith("--target-framework=")) {
      options.targetFramework = arg.slice("--target-framework=".length);
    } else if (arg === "--mods-dir") {
      options.modsDir = resolve(requireValue(args, ++index, arg));
    } else if (arg.startsWith("--mods-dir=")) {
      options.modsDir = resolve(arg.slice("--mods-dir=".length));
    } else if (arg === "--remove") {
      options.remove = true;
    } else if (arg === "--skip-asset-bundle") {
      options.skipAssetBundle = true;
    } else if (arg === "--skip-build") {
      options.skipBuild = true;
    } else if (arg === "--unity-executable") {
      options.unityExecutable = resolve(requireValue(args, ++index, arg));
    } else if (arg.startsWith("--unity-executable=")) {
      options.unityExecutable = resolve(arg.slice("--unity-executable=".length));
    } else {
      fail(`Unknown argument: ${arg}`);
    }
  }

  if (!options.configuration.trim()) {
    fail("Build configuration must not be empty.");
  }

  if (!options.targetFramework.trim()) {
    fail("Target framework must not be empty.");
  }

  return options;
};

const requireValue = (args: string[], index: number, flag: string): string => {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    fail(`${flag} requires a value.`);
  }

  return value;
};

const parseTimeout = (value: string): number => {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    fail(`Invalid lock timeout: ${value}`);
  }

  return parsed;
};

const run = (command: string, args: string[]): void => {
  log(`run ${command} ${args.join(" ")}`);
  const result = Bun.spawnSync([command, ...args], {
    cwd: repoRoot,
    stdout: "inherit",
    stderr: "inherit",
  });

  if (result.exitCode !== 0) {
    fail(`${command} exited with code ${result.exitCode}.`);
  }
};

const isTimberbornRunning = (): boolean => {
  const result = Bun.spawnSync(["pgrep", "-x", "Timberborn"], {
    stdout: "pipe",
    stderr: "pipe",
  });

  return result.exitCode === 0;
};

const readLock = (): string => {
  try {
    return readFileSync(lockInfoPath, "utf8");
  } catch {
    return "(lock metadata unavailable)";
  }
};

const acquireLock = (options: DeployOptions): (() => void) => {
  mkdirSync(dirname(lockDir), { recursive: true });

  if (options.forceLock && existsSync(lockDir)) {
    log(`force removing existing lock ${lockDir}`);
    rmSync(lockDir, { recursive: true, force: true });
  }

  const startedAt = Date.now();
  while (true) {
    try {
      mkdirSync(lockDir);
      writeFileSync(
        lockInfoPath,
        `${JSON.stringify(
          {
            branch: getGitBranch(),
            cwd: repoRoot,
            mode: options.dryRun ? "dry-run" : options.remove ? "remove" : "deploy",
            pid: process.pid,
            startedAt: new Date().toISOString(),
          },
          null,
          2,
        )}\n`,
      );
      log(`acquired lock ${lockDir}`);
      return () => {
        rmSync(lockDir, { recursive: true, force: true });
        log(`released lock ${lockDir}`);
      };
    } catch (error) {
      if (!existsSync(lockDir)) {
        throw error;
      }

      const elapsedSeconds = (Date.now() - startedAt) / 1000;
      if (elapsedSeconds >= options.lockTimeoutSeconds) {
        fail(`deploy lock is held at ${lockDir}: ${readLock()}`);
      }

      Bun.sleepSync(1000);
    }
  }
};

const getGitBranch = (): string => {
  const result = Bun.spawnSync(["git", "branch", "--show-current"], {
    cwd: repoRoot,
    stdout: "pipe",
    stderr: "pipe",
  });

  return result.exitCode === 0 ? result.stdout.toString().trim() : "unknown";
};

const getBuildOutputDir = (configuration: string, targetFramework: string): string =>
  join(repoRoot, "src", "Wildfire.Timberborn", "bin", configuration, targetFramework);

const getAssetBundleOutputDir = (): string =>
  join(unityProjectPath, "Build", "AssetBundles", assetBundleTarget);

const createCopyPlan = (options: DeployOptions): CopyPlan[] => {
  const outputDir = getBuildOutputDir(options.configuration, options.targetFramework);
  const assetBundleOutputDir = getAssetBundleOutputDir();
  const targetDir = join(options.modsDir, modFolderName);
  const scriptsDir = join(targetDir, "Scripts");
  const computeShadersDir = join(targetDir, privateComputeShaderFolderName);
  const assetBundlePlan = assetBundleArtifacts.flatMap((artifact) => [
    {
      from: join(assetBundleOutputDir, artifact.name),
      to: join(computeShadersDir, artifact.name),
      required: true,
    },
    {
      from: join(assetBundleOutputDir, artifact.manifestName),
      to: join(computeShadersDir, artifact.manifestName),
      required: false,
    },
  ]);

  return [
    ...requiredAssemblies.map((name) => ({
      from: join(outputDir, name),
      to: join(scriptsDir, name),
      required: true,
    })),
    ...optionalAssemblies.map((name) => ({
      from: join(outputDir, name),
      to: join(scriptsDir, name),
      required: false,
    })),
    ...assetBundlePlan,
  ];
};

const copyModData = (targetDir: string, dryRun: boolean): void => {
  if (!existsSync(timberbornDataPath)) {
    return;
  }

  if (dryRun) {
    readdirSync(timberbornDataPath).forEach((entry) => {
      log(`dry-run copy ${join(timberbornDataPath, entry)} -> ${join(targetDir, entry)}`);
    });
    return;
  }

  mkdirSync(targetDir, { recursive: true });
  readdirSync(timberbornDataPath).forEach((entry) => {
    cpSync(join(timberbornDataPath, entry), join(targetDir, entry), { recursive: true });
    log(`copied ${join(timberbornDataPath, entry)} -> ${join(targetDir, entry)}`);
  });
};

const runUnityAssetBundleBuild = (options: DeployOptions): void => {
  if (options.skipAssetBundle) {
    log("skip_asset_bundle=true");
    return;
  }

  if (!existsSync(options.unityExecutable)) {
    fail(`Unity executable is missing: ${options.unityExecutable}`);
  }

  const outputDir = getAssetBundleOutputDir();
  mkdirSync(outputDir, { recursive: true });
  assetBundleArtifacts.forEach((artifact) => runUnityAssetBundleBuilder(options, artifact, outputDir));
};

const runUnityAssetBundleBuilder = (
  options: DeployOptions,
  artifact: AssetBundleArtifact,
  outputDir: string,
): void => {
  const logPath = join(outputDir, artifact.logName);
  rmSync(join(outputDir, artifact.name), { force: true });
  rmSync(join(outputDir, artifact.manifestName), { force: true });
  log(`run ${options.unityExecutable} -batchmode -quit -projectPath ${unityProjectPath} -executeMethod ${artifact.builderMethod}`);
  const result = Bun.spawnSync(
    [
      options.unityExecutable,
      "-batchmode",
      "-quit",
      "-projectPath",
      unityProjectPath,
      "-executeMethod",
      artifact.builderMethod,
      "-logFile",
      logPath,
      "--",
      ...(artifact.extraCliArgs ?? ["--shader", computeShaderPath]),
      "--output",
      outputDir,
      "--bundle",
      artifact.name,
      "--target",
      assetBundleTarget,
    ],
    {
      cwd: repoRoot,
      stdout: "inherit",
      stderr: "inherit",
    },
  );

  if (result.exitCode !== 0) {
    fail(`Unity AssetBundle build exited with code ${result.exitCode}. Log: ${logPath}\n${readLogTail(logPath)}`);
  }
};

const readLogTail = (path: string): string => {
  if (!existsSync(path)) {
    return "(Unity log file was not created.)";
  }

  return readFileSync(path, "utf8").split(/\r?\n/u).slice(-80).join("\n");
};

const validateSources = (plan: CopyPlan[]): void => {
  plan
    .filter((entry) => entry.required)
    .filter((entry) => !existsSync(entry.from))
    .forEach((entry) => fail(`Required build artifact is missing: ${entry.from}`));
};

const validateAssetBundleOutputs = (): void => {
  const outputDir = getAssetBundleOutputDir();
  assetBundleArtifacts.forEach((artifact) => validateAssetBundleOutput(outputDir, artifact));
};

const validateAssetBundleOutput = (outputDir: string, artifact: AssetBundleArtifact): void => {
  const bundlePath = join(outputDir, artifact.name);
  const manifestPath = join(outputDir, artifact.manifestName);

  if (!existsSync(bundlePath) || !statSync(bundlePath).isFile()) {
    fail(`Required AssetBundle is missing: ${bundlePath}`);
  }

  if (!existsSync(manifestPath) || !statSync(manifestPath).isFile()) {
    fail(`Required AssetBundle manifest is missing: ${manifestPath}`);
  }

  const manifestText = readFileSync(manifestPath, "utf8");
  if (!manifestText.includes(artifact.requiredAsset)) {
    fail(
      `AssetBundle manifest does not contain ${artifact.requiredAsset}: ${manifestPath}`,
    );
  }
};

const printPlan = (options: DeployOptions, plan: CopyPlan[]): void => {
  const targetDir = join(options.modsDir, modFolderName);
  log(`mod_id=${manifest.Id}`);
  log(`mod_name=${manifest.Name}`);
  log(`mod_version=${manifest.Version}`);
  log(`minimum_game_version=${manifest.MinimumGameVersion}`);
  log(`configuration=${options.configuration}`);
  log(`target_framework=${options.targetFramework}`);
  log(`unity_project=${unityProjectPath}`);
  log(`unity_executable=${options.unityExecutable}`);
  log(`asset_bundle_target=${assetBundleTarget}`);
  log(`asset_bundles=${assetBundleArtifacts.map((artifact) => artifact.name).join(",")}`);
  log(`asset_bundle_output_dir=${getAssetBundleOutputDir()}`);
  log(`mod_data=${timberbornDataPath}`);
  log(`skip_asset_bundle=${options.skipAssetBundle}`);
  log(`mods_dir=${options.modsDir}`);
  log(`target_dir=${targetDir}`);
  log(`manifest=${join(targetDir, "manifest.json")}`);
  plan.forEach((entry) => {
    const status = existsSync(entry.from) ? "present" : entry.required ? "missing-required" : "missing-optional";
    log(`artifact ${status} ${entry.from} -> ${entry.to}`);
  });
};

const writeManifest = (targetDir: string, dryRun: boolean): void => {
  const manifestPath = join(targetDir, "manifest.json");
  if (dryRun) {
    log(`dry-run write ${manifestPath}`);
    return;
  }

  mkdirSync(targetDir, { recursive: true });
  writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
  log(`wrote ${manifestPath}`);
};

const copyArtifacts = (plan: CopyPlan[], dryRun: boolean): void => {
  plan
    .filter((entry) => entry.required || existsSync(entry.from))
    .forEach((entry) => {
      if (dryRun) {
        log(`dry-run copy ${entry.from} -> ${entry.to}`);
        return;
      }

      mkdirSync(dirname(entry.to), { recursive: true });
      copyFileSync(entry.from, entry.to);
      log(`copied ${entry.from} -> ${entry.to}`);
    });
};

const removeTarget = (targetDir: string, dryRun: boolean): void => {
  if (!existsSync(targetDir)) {
    log(`target not present ${targetDir}`);
    return;
  }

  if (dryRun) {
    log(`dry-run remove ${targetDir}`);
    return;
  }

  rmSync(targetDir, { recursive: true, force: true });
  log(`removed ${targetDir}`);
};

const assertTargetShape = (targetDir: string): void => {
  const expectedPaths = [
    join(targetDir, "manifest.json"),
    ...requiredAssemblies.map((name) => join(targetDir, "Scripts", name)),
    ...assetBundleArtifacts.map((artifact) => join(targetDir, privateComputeShaderFolderName, artifact.name)),
  ];

  expectedPaths
    .filter((path) => !existsSync(path) || !statSync(path).isFile())
    .forEach((path) => fail(`Expected deployed file is missing: ${path}`));
};

const main = (): void => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  const targetDir = join(options.modsDir, modFolderName);
  const releaseLock = acquireLock(options);

  try {
    const gameRunning = isTimberbornRunning();
    log(`timberborn_running=${gameRunning}`);
    if (gameRunning && !options.dryRun && !options.allowOpenGame) {
      fail("Timberborn appears to be running. Close the game or pass --allow-open-game for an explicit unsafe deploy/remove.");
    }

    if (!options.skipBuild && !options.remove) {
      run("dotnet", ["build", "Wildfire.slnx", "--configuration", options.configuration]);
    }

    if (!options.remove) {
      runUnityAssetBundleBuild(options);
      validateAssetBundleOutputs();
    }

    const plan = createCopyPlan(options);
    printPlan(options, plan);

    if (options.remove) {
      removeTarget(targetDir, options.dryRun);
      return;
    }

    validateSources(plan);

    if (options.clean) {
      removeTarget(targetDir, options.dryRun);
    }

    writeManifest(targetDir, options.dryRun);
    copyModData(targetDir, options.dryRun);
    copyArtifacts(plan, options.dryRun);

    if (!options.dryRun) {
      assertTargetShape(targetDir);
      log(`deploy_complete target_dir=${targetDir}`);
    } else {
      log("dry_run_complete");
    }
  } finally {
    releaseLock();
  }
};

try {
  main();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
}
