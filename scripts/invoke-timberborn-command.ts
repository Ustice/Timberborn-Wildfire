#!/usr/bin/env bun

import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from "fs";
import { dirname, join, resolve } from "path";

type InvokeOptions = {
  command: string;
  commandDir: string;
  help: boolean;
  requireAdvancedTick: boolean;
  requireNonzeroDelta: boolean;
  requireSustainedHeatCycles: number | null;
  requireWaterChanged: boolean;
  waitSeconds: number;
};

const home = process.env.HOME ?? "";
const defaultCommandDir = join(home, "Library", "Application Support", "Mechanistry", "Timberborn", "WildfireQA");
const inboxFileName = "command-inbox.txt";
const outboxFileName = "command-outbox.txt";
const knownCommands = [
  "help",
  "qa-ash-cell",
  "qa-ash-water-stimulus",
  "qa-building-burnout-stimulus",
  "qa-burn-duration-stimulus",
  "qa-delta-stimulus",
  "qa-fire-preset",
  "qa-readiness",
  "qa-soil-moisture-range",
  "qa-water-suppression-stimulus",
  "status",
];

const usage = `Usage:
  bun scripts/invoke-timberborn-command.ts [command] [options]

Commands:
  status                    Read-only Wildfire runtime status. Default.
  qa-readiness              Read-only loaded-game readiness summary.
  qa-ash-cell <cell-index>  Read-only simulator transport/read-model ash state for one cell.
  qa-ash-water-stimulus <clean|tainted>
                            Queue simulator-owned ash plus water contact on one imported burnable field target.
  qa-delta-stimulus [burnable|tree|contaminated-tree|beaver-exposure|toxic-beaver-exposure|vegetation|crop|storage|building|lodge|district-center]
                            Queue heat on an imported burnable field target or allowlisted burn-damage target.
  qa-building-burnout-stimulus
                            Queue heat and fuel depletion on a real pausable building target.
  qa-burn-duration-stimulus <low|medium|high>
                            Queue heat on an imported burnable target in the selected fuel band.
  qa-fire-preset <preset>   Select an allowlisted fire-simulation tuning preset.
  qa-soil-moisture-range    Read-only scan of Timberborn SoilMoisture values over terrain.
  qa-water-suppression-stimulus [burnable|tree|vegetation|crop|storage|building]
                            Queue water on an imported burnable field target.
  help                      Read-only command list.

Options:
  --command-dir <path>      Command bridge directory. Default: ~/Library/Application Support/Mechanistry/Timberborn/WildfireQA.
  --require-advanced-tick   Fail unless the result reports numeric tick_count greater than 0. Unpause a loaded save before using this.
  --require-nonzero-delta   Fail unless the result reports numeric last_delta_count greater than 0. Use after qa-delta-stimulus.
  --require-sustained-heat-cycles <count>
                            Fail unless qa_delta_stimulus_sustained_heat_completed_cycles is at least count. Use after qa-delta-stimulus and enough unpaused dispatches.
  --require-water-changed   Fail unless the result reports durable last_positive_water_changed_count greater than 0. Use after qa-water-suppression-stimulus or qa-ash-water-stimulus.
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

const parsePositiveInteger = (value: string, flag: string): number => {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    fail(`${flag} requires a positive integer.`);
  }

  return parsed;
};

const parseArgs = (args: string[]): InvokeOptions => {
  const options: InvokeOptions = {
    command: "status",
    commandDir: defaultCommandDir,
    help: false,
    requireAdvancedTick: false,
    requireNonzeroDelta: false,
    requireSustainedHeatCycles: null,
    requireWaterChanged: false,
    waitSeconds: 5,
  };
  let skipNext = false;
  const commandParts: string[] = [];

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
    } else if (arg === "--require-nonzero-delta") {
      options.requireNonzeroDelta = true;
    } else if (arg === "--require-sustained-heat-cycles") {
      options.requireSustainedHeatCycles = parsePositiveInteger(requireValue(args, index + 1, arg), arg);
      skipNext = true;
    } else if (arg.startsWith("--require-sustained-heat-cycles=")) {
      options.requireSustainedHeatCycles = parsePositiveInteger(
        arg.slice("--require-sustained-heat-cycles=".length),
        "--require-sustained-heat-cycles",
      );
    } else if (arg === "--require-water-changed") {
      options.requireWaterChanged = true;
    } else if (arg === "--wait") {
      options.waitSeconds = parseWait(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--wait=")) {
      options.waitSeconds = parseWait(arg.slice("--wait=".length));
    } else if (arg.startsWith("--")) {
      fail(`Unknown argument: ${arg}`);
    } else {
      commandParts.push(arg);
    }

    return undefined;
  }, undefined);

  options.command = commandParts.length > 0 ? commandParts.join(" ") : options.command;

  return options;
};

const commandName = (command: string): string => command.trim().split(/\s+/u)[0]?.toLowerCase() ?? "";

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

const requireNonzeroDelta = (result: string): void => {
  const deltaMatch =
    result.match(/\blast_delta_count=(\d+)\b/u) ??
    fail("Result did not include numeric last_delta_count. Is the command reporting simulator state?");
  const deltaValue = deltaMatch[1];
  const deltaCount = Number(deltaValue);
  if (deltaCount <= 0) {
    fail(`Result reported last_delta_count=${deltaCount}. Run qa-delta-stimulus against an unpaused loaded save, wait for a simulator tick, then rerun status or qa-readiness.`);
  }
};

const requireSustainedHeatCycles = (result: string, requiredCycles: number): void => {
  const completedMatch =
    result.match(/\bqa_delta_stimulus_sustained_heat_completed_cycles=(\d+)\b/u) ??
    fail("Result did not include numeric qa_delta_stimulus_sustained_heat_completed_cycles. Did this build include the sustained crop stimulus proof fields?");
  const requestedMatch =
    result.match(/\bqa_delta_stimulus_sustained_heat_requested_cycles=(\d+)\b/u) ??
    fail("Result did not include numeric qa_delta_stimulus_sustained_heat_requested_cycles. Did this build include the sustained crop stimulus proof fields?");
  const completedCycles = Number(completedMatch[1]);
  const requestedCycles = Number(requestedMatch[1]);
  if (requestedCycles < requiredCycles || completedCycles < requiredCycles) {
    fail(`Result reported sustained heat completed_cycles=${completedCycles} requested_cycles=${requestedCycles}. Run qa-delta-stimulus against an unpaused loaded save, wait for ${requiredCycles} simulator dispatches, then rerun status or qa-readiness.`);
  }
};

const requireWaterChanged = (result: string): void => {
  const waterMatch =
    result.match(/\blast_positive_water_changed_count=(\d+)\b/u) ??
    fail("Result did not include numeric last_positive_water_changed_count. Is the command reporting persistent water-change state?");
  const waterValue = waterMatch[1];
  const waterChangedCount = Number(waterValue);
  if (waterChangedCount <= 0) {
    fail(`Result reported last_positive_water_changed_count=${waterChangedCount}. Run qa-water-suppression-stimulus against an unpaused loaded save, wait for a simulator tick, then rerun status or qa-readiness.`);
  }
};

const main = async (): Promise<void> => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  const requestedCommandName = commandName(options.command);
  if (!knownCommands.includes(requestedCommandName)) {
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

  if (options.requireNonzeroDelta) {
    requireNonzeroDelta(result);
  }

  if (options.requireSustainedHeatCycles !== null) {
    requireSustainedHeatCycles(result, options.requireSustainedHeatCycles);
  }

  if (options.requireWaterChanged) {
    requireWaterChanged(result);
  }
};

try {
  await main();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
}
