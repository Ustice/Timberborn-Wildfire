import { cpSync, rmSync } from "node:fs";
import { mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { spawnSync } from "node:child_process";
import {
  modFolderName,
  validateTimberbornModArtifact,
  workshopVersionFolderName,
} from "./release-package-validation.ts";

type Options = {
  clean: boolean;
  configuration: string;
  contentPath: string;
  deployArgs: string[];
  stagingModsPath: string;
};

const root = resolve(import.meta.dir, "..");

const usage = `Usage: bun scripts/package-steam-workshop.ts [options]

Options:
  --configuration <name>  Build configuration. Default: Release.
  --staging-mods <path>   Deploy staging Mods dir. Default: release/workshop/staging-mods.
  --content <path>        Workshop content version dir. Default: release/workshop/content/version-1.0.
  --no-clean              Keep existing staging/content directories.
  --deploy-arg <arg>      Extra argument forwarded to deploy-timberborn-mod.ts.
  --help                  Show this help.`;

function parseArgs(args: string[]): Options {
  const options: Options = {
    clean: true,
    configuration: "Release",
    contentPath: "release/workshop/content/version-1.0",
    deployArgs: [],
    stagingModsPath: "release/workshop/staging-mods",
  };

  args.reduce<boolean>((skipNext, currentArg, index) => {
    if (skipNext) {
      return false;
    }

    const arg = currentArg ?? "";
    const value = (): string => {
      const next = args[index + 1];
      if (!next || next.startsWith("--")) {
        throw new Error(`${arg} requires a value.`);
      }
      return next;
    };

    if (arg === "--help") {
      console.log(usage);
      process.exit(0);
    } else if (arg === "--configuration") {
      options.configuration = value();
    } else if (arg.startsWith("--configuration=")) {
      options.configuration = arg.slice("--configuration=".length);
    } else if (arg === "--staging-mods") {
      options.stagingModsPath = value();
    } else if (arg.startsWith("--staging-mods=")) {
      options.stagingModsPath = arg.slice("--staging-mods=".length);
    } else if (arg === "--content") {
      options.contentPath = value();
    } else if (arg.startsWith("--content=")) {
      options.contentPath = arg.slice("--content=".length);
    } else if (arg === "--no-clean") {
      options.clean = false;
    } else if (arg === "--deploy-arg") {
      options.deployArgs.push(value());
    } else if (arg.startsWith("--deploy-arg=")) {
      options.deployArgs.push(arg.slice("--deploy-arg=".length));
    } else {
      throw new Error(`Unknown argument: ${arg}`);
    }
    return arg === "--configuration" || arg === "--staging-mods" || arg === "--content" || arg === "--deploy-arg";
  }, false);

  return options;
}

function run(command: string, args: string[]): void {
  console.log([command, ...args.map((arg) => (arg.includes(" ") ? `"${arg}"` : arg))].join(" "));
  const result = spawnSync(command, args, {
    cwd: root,
    stdio: "inherit",
    env: process.env,
  });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    throw new Error(`${command} exited with status ${result.status ?? "unknown"}.`);
  }
}

async function main(): Promise<void> {
  const options = parseArgs(process.argv.slice(2));
  const stagingModsPath = resolve(root, options.stagingModsPath);
  const stagingWildfirePath = resolve(stagingModsPath, modFolderName);
  const contentPath = resolve(root, options.contentPath);

  if (options.clean) {
    rmSync(stagingWildfirePath, { recursive: true, force: true });
    rmSync(contentPath, { recursive: true, force: true });
  }

  run("bun", [
    "scripts/deploy-timberborn-mod.ts",
    "--apply",
    "--clean",
    "--configuration",
    options.configuration,
    "--mods-dir",
    stagingModsPath,
    "--lock-timeout",
    "60",
    "--allow-open-game",
    ...options.deployArgs,
  ]);

  validateTimberbornModArtifact(stagingWildfirePath, modFolderName);

  await mkdir(dirname(contentPath), { recursive: true });
  cpSync(stagingWildfirePath, contentPath, { recursive: true });

  const summary = validateTimberbornModArtifact(contentPath, workshopVersionFolderName);

  console.log(`workshop_package_ready content=${contentPath}`);
  console.log(`manifest_id=${summary.manifest.Id}`);
  console.log(`manifest_version=${summary.manifest.Version}`);
  console.log(`platform_support=${summary.platformSupport}`);
  console.log(`required_bundles=${summary.requiredBundles.join(",")}`);
  console.log(`file_count=${summary.fileCount}`);
  summary.files.forEach((file) => console.log(`workshop_artifact_file ${file}`));
}

main().catch((error: unknown) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});
