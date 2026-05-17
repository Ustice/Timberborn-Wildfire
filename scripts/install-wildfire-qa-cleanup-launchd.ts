#!/usr/bin/env bun

import { execFileSync } from "child_process";
import { existsSync, mkdirSync, rmSync, writeFileSync } from "fs";
import { dirname, join, resolve } from "path";

type InstallOptions = {
  dryRun: boolean;
  help: boolean;
  hour: number;
  minute: number;
  unload: boolean;
};

const home = process.env.HOME ?? "";
const repoRoot = resolve(import.meta.dir, "..");
const label = "com.jasonkleinberg.wildfire.qa-media-cleanup";
const launchAgentsDir = join(home, "Library", "LaunchAgents");
const plistPath = join(launchAgentsDir, `${label}.plist`);
const logPath = join(home, "Library", "Logs", "wildfire-qa-media-cleanup.log");
const errorLogPath = join(home, "Library", "Logs", "wildfire-qa-media-cleanup.err.log");

const usage = `Usage:
  bun scripts/install-wildfire-qa-cleanup-launchd.ts [options]

Options:
  --hour <0-23>             Daily run hour. Default: 3.
  --minute <0-59>           Daily run minute. Default: 15.
  --unload                  Unload and remove the LaunchAgent instead of installing it.
  --dry-run                 Print the plan without writing or loading the LaunchAgent.
  --help                    Show this help.

Examples:
  bun scripts/install-wildfire-qa-cleanup-launchd.ts
  bun scripts/install-wildfire-qa-cleanup-launchd.ts --hour 2 --minute 30
`;

const fail = (message: string): never => {
  throw new Error(`[wildfire-qa-cleanup-install] ${message}`);
};

const requireValue = (args: string[], index: number, flag: string): string => {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    fail(`${flag} requires a value.`);
  }

  return value;
};

const parseIntegerRange = (value: string, flag: string, min: number, max: number): number => {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < min || parsed > max) {
    fail(`${flag} requires an integer from ${min} to ${max}.`);
  }

  return parsed;
};

const parseArgs = (args: string[]): InstallOptions => {
  const options: InstallOptions = {
    dryRun: false,
    help: false,
    hour: 3,
    minute: 15,
    unload: false,
  };
  let skipNext = false;

  args.reduce((_, arg, index) => {
    if (skipNext) {
      skipNext = false;
      return undefined;
    }

    if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--hour") {
      options.hour = parseIntegerRange(requireValue(args, index + 1, arg), arg, 0, 23);
      skipNext = true;
    } else if (arg.startsWith("--hour=")) {
      options.hour = parseIntegerRange(arg.slice("--hour=".length), "--hour", 0, 23);
    } else if (arg === "--minute") {
      options.minute = parseIntegerRange(requireValue(args, index + 1, arg), arg, 0, 59);
      skipNext = true;
    } else if (arg.startsWith("--minute=")) {
      options.minute = parseIntegerRange(arg.slice("--minute=".length), "--minute", 0, 59);
    } else if (arg === "--unload") {
      options.unload = true;
    } else if (arg === "--dry-run") {
      options.dryRun = true;
    } else {
      fail(`Unknown option: ${arg}`);
    }

    return undefined;
  }, undefined);

  return options;
};

const shellQuote = (value: string): string => `'${value.replace(/'/g, "'\\''")}'`;

const runLaunchctl = (args: string[], dryRun: boolean): void => {
  if (dryRun) {
    console.log(`launchctl ${args.map(shellQuote).join(" ")}`);
    return;
  }

  execFileSync("launchctl", args, { stdio: "inherit" });
};

const optionalLaunchctl = (args: string[], dryRun: boolean): void => {
  try {
    runLaunchctl(args, dryRun);
  } catch {
    // launchctl returns non-zero when a job is not currently loaded; that is fine for idempotent installs.
  }
};

const resolveBunExecutable = (): string => {
  try {
    return execFileSync("which", ["bun"], { encoding: "utf8" }).trim();
  } catch {
    return process.execPath;
  }
};

const plistXml = (options: InstallOptions, bunExecutable: string): string => `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>${label}</string>
  <key>ProgramArguments</key>
  <array>
    <string>${bunExecutable}</string>
    <string>${join(repoRoot, "scripts", "cleanup-wildfire-qa-media.ts")}</string>
    <string>--age-hours</string>
    <string>24</string>
  </array>
  <key>StartCalendarInterval</key>
  <dict>
    <key>Hour</key>
    <integer>${options.hour}</integer>
    <key>Minute</key>
    <integer>${options.minute}</integer>
  </dict>
  <key>StandardOutPath</key>
  <string>${logPath}</string>
  <key>StandardErrorPath</key>
  <string>${errorLogPath}</string>
  <key>WorkingDirectory</key>
  <string>${repoRoot}</string>
</dict>
</plist>
`;

const main = (): void => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  const userId = process.getuid?.();
  if (userId === undefined) {
    fail("Could not determine the current user id for launchctl.");
  }

  const guiDomain = `gui/${userId}`;
  const bunExecutable = resolveBunExecutable();

  if (options.unload) {
    optionalLaunchctl(["bootout", guiDomain, plistPath], options.dryRun);
    if (!options.dryRun && existsSync(plistPath)) {
      rmSync(plistPath, { force: true });
    }
    console.log(`removed=${JSON.stringify(plistPath)} label=${label}`);
    return;
  }

  const plist = plistXml(options, bunExecutable);
  if (options.dryRun) {
    console.log(plist);
    optionalLaunchctl(["bootout", guiDomain, plistPath], true);
    runLaunchctl(["bootstrap", guiDomain, plistPath], true);
    return;
  }

  mkdirSync(dirname(plistPath), { recursive: true });
  writeFileSync(plistPath, plist);
  optionalLaunchctl(["bootout", guiDomain, plistPath], false);
  runLaunchctl(["bootstrap", guiDomain, plistPath], false);

  console.log(`installed=${JSON.stringify(plistPath)} label=${label} schedule=${options.hour}:${String(options.minute).padStart(2, "0")}`);
  console.log(`logs=${JSON.stringify(logPath)} errors=${JSON.stringify(errorLogPath)}`);
};

main();
