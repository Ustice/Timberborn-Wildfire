#!/usr/bin/env bun

import {
  copyFileSync,
  existsSync,
  mkdirSync,
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
  skipBuild: boolean;
};

type CopyPlan = {
  from: string;
  to: string;
  required: boolean;
};

const repoRoot = resolve(import.meta.dir, "..");
const home = process.env.HOME ?? "";
const defaultModsDir = join(home, "Documents", "Timberborn", "Mods");
const lockDir = join(home, "Library", "Application Support", "Timberborn", "WildfireQA", "locks", "build-deploy.lock");
const lockInfoPath = join(lockDir, "lock.json");
const modFolderName = "Wildfire";
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
  --configuration <name>    Build configuration. Default: Debug.
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
    skipBuild: false,
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
    } else if (arg === "--mods-dir") {
      options.modsDir = resolve(requireValue(args, ++index, arg));
    } else if (arg.startsWith("--mods-dir=")) {
      options.modsDir = resolve(arg.slice("--mods-dir=".length));
    } else if (arg === "--remove") {
      options.remove = true;
    } else if (arg === "--skip-build") {
      options.skipBuild = true;
    } else {
      fail(`Unknown argument: ${arg}`);
    }
  }

  if (!options.configuration.trim()) {
    fail("Build configuration must not be empty.");
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

const getBuildOutputDir = (configuration: string): string =>
  join(repoRoot, "src", "Wildfire.Timberborn", "bin", configuration, "net10.0");

const createCopyPlan = (options: DeployOptions): CopyPlan[] => {
  const outputDir = getBuildOutputDir(options.configuration);
  const targetDir = join(options.modsDir, modFolderName);
  const scriptsDir = join(targetDir, "Scripts");

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
  ];
};

const validateSources = (plan: CopyPlan[]): void => {
  plan
    .filter((entry) => entry.required)
    .filter((entry) => !existsSync(entry.from))
    .forEach((entry) => fail(`Required build artifact is missing: ${entry.from}`));
};

const printPlan = (options: DeployOptions, plan: CopyPlan[]): void => {
  const targetDir = join(options.modsDir, modFolderName);
  log(`mod_id=${manifest.Id}`);
  log(`mod_name=${manifest.Name}`);
  log(`mod_version=${manifest.Version}`);
  log(`minimum_game_version=${manifest.MinimumGameVersion}`);
  log(`configuration=${options.configuration}`);
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
