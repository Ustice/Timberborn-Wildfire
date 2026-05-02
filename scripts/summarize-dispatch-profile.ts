#!/usr/bin/env bun

import { existsSync, readFileSync } from "fs";
import { join, resolve } from "path";

type DispatchSample = {
  tick: number;
  deltaCount: number;
  elapsedMs: number;
};

type KernelSample = {
  elapsedMs: number;
  kernel: string;
  tick: number;
};

type ReadbackSample = {
  deltaCount: number;
  tick: number;
};

type ConsumerSample = {
  alerts: number;
  changedCells: number;
  gameplayConsequences: number;
  tick: number;
  visualEffectEvents: number;
};

type CommandSample = {
  command: string;
  fields: Record<string, string>;
};

type Profile = {
  commands: CommandSample[];
  consumers: ConsumerSample[];
  dispatches: DispatchSample[];
  kernels: KernelSample[];
  readbacks: ReadbackSample[];
};

const home = process.env.HOME ?? "";
const defaultArtifactDir = join(
  home,
  "Library",
  "Application Support",
  "Mechanistry",
  "Timberborn",
  "WildfireQA",
  "twf-031-live-20260502T143543Z",
);

const usage = `Usage:
  bun scripts/summarize-dispatch-profile.ts [artifact-dir]

Reads Player.log from a WildfireQA artifact directory and summarizes live GPU dispatch timing,
readback/delta counts, command output fields, and delta-consumer counters.
`;

const fail = (message: string): never => {
  throw new Error(`[wildfire-dispatch-profile] ${message}`);
};

const parseNumber = (value: string | undefined, field: string): number => {
  if (value === undefined) {
    fail(`Missing numeric field: ${field}`);
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    fail(`Invalid numeric ${field}: ${value}`);
  }

  return parsed;
};

const fieldsFromLine = (line: string): Record<string, string> =>
  Array.from(line.matchAll(/\b([a-zA-Z_]+)=([^\s]+)/gu)).reduce<Record<string, string>>((fields, match) => {
    const [, key, value] = match;
    if (key !== undefined && value !== undefined) {
      fields[key] = value;
    }

    return fields;
  }, {});

const parseProfile = (text: string): Profile =>
  text.split(/\r?\n/u).reduce<Profile>(
    (profile, line) => {
      if (line.includes("wildfire_timberborn_dispatch_completed")) {
        const fields = fieldsFromLine(line);
        profile.dispatches.push({
          deltaCount: parseNumber(fields.delta_count, "delta_count"),
          elapsedMs: parseNumber(fields.elapsed_ms, "elapsed_ms"),
          tick: parseNumber(fields.tick, "tick"),
        });
      }

      if (line.includes("wildfire_timberborn_gpu_dispatch_kernel_completed")) {
        const fields = fieldsFromLine(line);
        profile.kernels.push({
          elapsedMs: parseNumber(fields.elapsed_ms, "elapsed_ms"),
          kernel: fields.kernel ?? "unknown",
          tick: parseNumber(fields.tick, "tick"),
        });
      }

      if (line.includes("wildfire_timberborn_gpu_readback_completed")) {
        const fields = fieldsFromLine(line);
        profile.readbacks.push({
          deltaCount: parseNumber(fields.delta_count, "delta_count"),
          tick: parseNumber(fields.tick, "tick"),
        });
      }

      if (line.includes("wildfire_timberborn_delta_consumer_completed")) {
        const fields = fieldsFromLine(line);
        profile.consumers.push({
          alerts: parseNumber(fields.alerts, "alerts"),
          changedCells: parseNumber(fields.changed_cells, "changed_cells"),
          gameplayConsequences: parseNumber(fields.gameplay_consequences, "gameplay_consequences"),
          tick: parseNumber(fields.tick, "tick"),
          visualEffectEvents: parseNumber(fields.visual_effect_events, "visual_effect_events"),
        });
      }

      if (line.includes("wildfire_command_result")) {
        const fields = fieldsFromLine(line);
        profile.commands.push({
          command: fields.command ?? "unknown",
          fields,
        });
      }

      return profile;
    },
    { commands: [], consumers: [], dispatches: [], kernels: [], readbacks: [] },
  );

const round = (value: number): string => value.toFixed(3).replace(/\.?0+$/u, "");

const percentile = (values: number[], percentileValue: number): number => {
  if (values.length === 0) {
    return 0;
  }

  const sorted = [...values].sort((left, right) => left - right);
  const index = Math.min(sorted.length - 1, Math.ceil((percentileValue / 100) * sorted.length) - 1);
  return sorted[index] ?? 0;
};

const sum = (values: number[]): number => values.reduce((total, value) => total + value, 0);

const summarizeValues = (values: number[]): string => {
  if (values.length === 0) {
    return "count=0";
  }

  return [
    `count=${values.length}`,
    `min=${round(Math.min(...values))}`,
    `median=${round(percentile(values, 50))}`,
    `p95=${round(percentile(values, 95))}`,
    `max=${round(Math.max(...values))}`,
    `avg=${round(sum(values) / values.length)}`,
  ].join(" ");
};

const summarizeByKernel = (kernels: KernelSample[]): string[] =>
  [...new Set(kernels.map((sample) => sample.kernel))].map((kernel) => {
    const values = kernels.filter((sample) => sample.kernel === kernel).map((sample) => sample.elapsedMs);
    return `kernel ${kernel} elapsed_ms ${summarizeValues(values)}`;
  });

const main = (): void => {
  const args = Bun.argv.slice(2);
  if (args.includes("--help") || args.includes("-h")) {
    console.log(usage);
    return;
  }

  const artifactDir = resolve(args[0] ?? defaultArtifactDir);
  const logPath = join(artifactDir, "Player.log");
  if (!existsSync(logPath)) {
    fail(`Player.log not found: ${logPath}`);
  }

  const profile = parseProfile(readFileSync(logPath, "utf8"));
  const dispatchElapsed = profile.dispatches.map((sample) => sample.elapsedMs);
  const nonzeroDispatches = profile.dispatches.filter((sample) => sample.deltaCount > 0);
  const nonzeroReadbacks = profile.readbacks.filter((sample) => sample.deltaCount > 0);
  const nonzeroConsumers = profile.consumers.filter((sample) => sample.changedCells > 0);
  const firstCommand = profile.commands[0]?.fields ?? {};
  const latestCommand = profile.commands.at(-1)?.fields ?? {};

  [
    `artifact_dir=${artifactDir}`,
    `player_log=${logPath}`,
    `map=${latestCommand.width ?? firstCommand.width ?? "unknown"}x${latestCommand.height ?? firstCommand.height ?? "unknown"}x${latestCommand.depth ?? firstCommand.depth ?? "unknown"}`,
    `ticks_observed=${profile.dispatches[0]?.tick ?? "unknown"}..${profile.dispatches.at(-1)?.tick ?? "unknown"}`,
    `dispatch_elapsed_ms ${summarizeValues(dispatchElapsed)}`,
    ...summarizeByKernel(profile.kernels),
    `readbacks count=${profile.readbacks.length} nonzero=${nonzeroReadbacks.length} max_delta_count=${Math.max(0, ...profile.readbacks.map((sample) => sample.deltaCount))}`,
    `dispatches count=${profile.dispatches.length} nonzero=${nonzeroDispatches.length} max_delta_count=${Math.max(0, ...profile.dispatches.map((sample) => sample.deltaCount))}`,
    `consumers count=${profile.consumers.length} nonzero_changed_cells=${nonzeroConsumers.length} max_changed_cells=${Math.max(0, ...profile.consumers.map((sample) => sample.changedCells))} max_visual_effect_events=${Math.max(0, ...profile.consumers.map((sample) => sample.visualEffectEvents))} max_gameplay_consequences=${Math.max(0, ...profile.consumers.map((sample) => sample.gameplayConsequences))} max_alerts=${Math.max(0, ...profile.consumers.map((sample) => sample.alerts))}`,
    `commands_seen=${profile.commands.map((sample) => sample.command).join(",")}`,
    `latest_command_tick=${latestCommand.tick_count ?? "unknown"} queued_changes=${latestCommand.queued_changes ?? "unknown"} last_delta_count=${latestCommand.last_delta_count ?? "unknown"} last_delta_consumer_changed_cells=${latestCommand.last_delta_consumer_changed_cells ?? "unknown"}`,
    `nonzero_dispatch_ticks=${nonzeroDispatches.map((sample) => `${sample.tick}:${sample.deltaCount}@${round(sample.elapsedMs)}ms`).join(",") || "none"}`,
  ].forEach((line) => console.log(line));
};

try {
  main();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
}
