#!/usr/bin/env bun

import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from "fs";
import { dirname, join, resolve } from "path";

type InvokeOptions = {
  command: string;
  commandDir: string;
  help: boolean;
  requireAdvancedTick: boolean;
  waitSeconds: number;
};

const home = process.env.HOME ?? "";
const defaultCommandDir = join(home, "Library", "Application Support", "Mechanistry", "Timberborn", "WildfireQA");
const inboxFileName = "command-inbox.txt";
const outboxFileName = "command-outbox.txt";
const knownCommands = ["help", "qa-readiness", "status"];

const usage = `Usage:
  bun scripts/invoke-timberborn-command.ts [command] [options]

Commands:
  status                    Read-only Wildfire runtime status. Default.
  qa-readiness              Read-only loaded-game readiness summary.
  help                      Read-only command list.

Options:
  --command-dir <path>      Command bridge directory. Default: ~/Library/Application Support/Mechanistry/Timberborn/WildfireQA.
  --require-advanced-tick   Fail unless the result reports numeric tick_count greater than 0. Unpause a loaded save before using this.
  --wait <seconds>          Wait for command-outbox.txt to update. Default: 5.
  --help                    Show this help.
`;

const fail = (message: string): never => {
  throw new Error(`[wildfire-command] ${message}`);
};

const requireValue = (args: string[], index: number, flag: string): string => {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    fail(`${flag} requires a value.`);
  }

  return value;
};

const parseWait = (value: string): number => {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    fail(`Invalid wait value: ${value}`);
  }

  return parsed;
};

const parseArgs = (args: string[]): InvokeOptions => {
  const options: InvokeOptions = {
    command: "status",
    commandDir: defaultCommandDir,
    help: false,
    requireAdvancedTick: false,
    waitSeconds: 5,
  };
  let skipNext = false;

  args.reduce((_, arg, index) => {
    if (skipNext) {
      skipNext = false;
      return undefined;
    }

    if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--command-dir") {
      options.commandDir = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--command-dir=")) {
      options.commandDir = resolve(arg.slice("--command-dir=".length));
    } else if (arg === "--require-advanced-tick") {
      options.requireAdvancedTick = true;
    } else if (arg === "--wait") {
      options.waitSeconds = parseWait(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--wait=")) {
      options.waitSeconds = parseWait(arg.slice("--wait=".length));
    } else if (arg.startsWith("--")) {
      fail(`Unknown argument: ${arg}`);
    } else {
      options.command = arg;
    }

    return undefined;
  }, undefined);

  return options;
};

const waitForOutbox = async (outboxPath: string, previousModified: number, waitSeconds: number): Promise<string | null> => {
  const startedAt = Date.now();
  const timeoutMs = waitSeconds * 1000;

  while (Date.now() - startedAt <= timeoutMs) {
    if (existsSync(outboxPath)) {
      const modified = statSync(outboxPath).mtimeMs;
      if (modified > previousModified) {
        return readFileSync(outboxPath, "utf8");
      }
    }

    await Bun.sleep(100);
  }

  return null;
};

const requireAdvancedTick = (result: string): void => {
  const tickMatch =
    result.match(/\btick_count=(\d+)\b/u) ??
    fail("Result did not include numeric tick_count. Is the command reporting simulator state?");
  const tickValue = tickMatch[1];
  const tickCount = Number(tickValue);
  if (tickCount <= 0) {
    fail(`Result reported tick_count=${tickCount}. Unpause the loaded save and rerun the command.`);
  }
};

const main = async (): Promise<void> => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  if (!knownCommands.includes(options.command.toLowerCase())) {
    fail(`Unknown command: ${options.command}. Known commands: ${knownCommands.join(", ")}.`);
  }

  const inboxPath = join(options.commandDir, inboxFileName);
  const outboxPath = join(options.commandDir, outboxFileName);
  const previousModified = existsSync(outboxPath) ? statSync(outboxPath).mtimeMs : 0;

  mkdirSync(dirname(inboxPath), { recursive: true });
  writeFileSync(inboxPath, `${options.command.trim()}\n`);
  console.log(`[wildfire-command] wrote ${inboxPath}`);

  const result = await waitForOutbox(outboxPath, previousModified, options.waitSeconds);
  if (result === null) {
    return fail(`Timed out waiting for ${outboxPath}. Is Timberborn running with a loaded game?`);
  }

  console.log(result.trimEnd());
  if (options.requireAdvancedTick) {
    requireAdvancedTick(result);
  }
};

try {
  await main();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
}
