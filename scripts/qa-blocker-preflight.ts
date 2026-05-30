#!/usr/bin/env bun

import { existsSync, readFileSync } from "fs";

export type BlockerStatus = "blocked" | "ready" | "unknown";

export type BlockerReport = {
  blockers: string[];
  issue: number;
  next: string[];
  status: BlockerStatus;
  summary: string;
  title: string;
};

type Options = {
  commandDir: string | null;
  format: "json" | "text";
  fromFile: string | null;
  help: boolean;
  waitSeconds: number;
};

type StatusTokens = Record<string, string>;

const usage = `Usage:
  bun scripts/qa-blocker-preflight.ts [options]

Options:
  --from-file <path>        Read a captured status/qa-readiness transcript instead of calling Timberborn.
  --command-dir <path>      Command bridge directory passed to invoke-timberborn-command.
  --wait <seconds>          Wait for live status. Default: 10.
  --format <text|json>      Output format. Default: text.
  --help                    Show this help.

Examples:
  bun scripts/qa-blocker-preflight.ts
  bun scripts/qa-blocker-preflight.ts --from-file qa-evidence/run/status.txt
  bun scripts/qa-blocker-preflight.ts --format=json
`;

const fail = (message: string): never => {
  throw new Error(`[qa-blocker-preflight] ${message}`);
};

const requireValue = (args: string[], index: number, flag: string): string => {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    fail(`${flag} requires a value.`);
  }

  return value;
};

const parsePositiveNumber = (value: string, flag: string): number => {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    fail(`${flag} requires a positive number.`);
  }

  return parsed;
};

const parseFormat = (value: string): "json" | "text" => {
  if (value !== "json" && value !== "text") {
    fail(`Invalid format: ${value}.`);
  }

  return value === "json" ? "json" : "text";
};

const parseArgs = (args: string[]): Options => {
  const initial: Options & { skipNext: boolean } = {
    commandDir: null,
    format: "text",
    fromFile: null,
    help: false,
    skipNext: false,
    waitSeconds: 10,
  };

  const parsed = args.reduce((options, arg, index) => {
    if (options.skipNext) {
      return { ...options, skipNext: false };
    }

    if (arg === "--help" || arg === "-h") {
      return { ...options, help: true };
    }

    if (arg === "--from-file") {
      return { ...options, fromFile: requireValue(args, index + 1, arg), skipNext: true };
    }

    if (arg.startsWith("--from-file=")) {
      return { ...options, fromFile: arg.slice("--from-file=".length) };
    }

    if (arg === "--command-dir") {
      return { ...options, commandDir: requireValue(args, index + 1, arg), skipNext: true };
    }

    if (arg.startsWith("--command-dir=")) {
      return { ...options, commandDir: arg.slice("--command-dir=".length) };
    }

    if (arg === "--wait") {
      return { ...options, skipNext: true, waitSeconds: parsePositiveNumber(requireValue(args, index + 1, arg), arg) };
    }

    if (arg.startsWith("--wait=")) {
      return { ...options, waitSeconds: parsePositiveNumber(arg.slice("--wait=".length), "--wait") };
    }

    if (arg === "--format") {
      return { ...options, format: parseFormat(requireValue(args, index + 1, arg)), skipNext: true };
    }

    if (arg.startsWith("--format=")) {
      return { ...options, format: parseFormat(arg.slice("--format=".length)) };
    }

    return fail(`Unknown argument: ${arg}`);
  }, initial);

  const { skipNext: _skipNext, ...options } = parsed;
  return options;
};

export const parseStatusTokens = (input: string): StatusTokens =>
  input
    .split(/\s+/u)
    .map((part) => part.match(/^([A-Za-z0-9_]+)=(.+)$/u))
    .filter((match): match is RegExpMatchArray => match !== null)
    .reduce<StatusTokens>(
      (tokens, match) => ({
        ...tokens,
        [match[1] ?? ""]: match[2] ?? "",
      }),
      {},
    );

const boolToken = (tokens: StatusTokens, key: string): boolean => tokens[key] === "true";

const numberToken = (tokens: StatusTokens, key: string): number => {
  const value = tokens[key];
  const parsed = value === undefined || value === "placeholder" ? 0 : Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
};

const hasLoadedRuntime = (tokens: StatusTokens): boolean =>
  boolToken(tokens, "runtime_loaded") && boolToken(tokens, "loaded_game_ready") && boolToken(tokens, "simulator_integrated");

const rendererCountersAreZero = (tokens: StatusTokens): boolean =>
  [
    "gpu_field_renderer_visible_regions",
    "gpu_field_renderer_updated_regions",
    "gpu_field_renderer_last_nonzero_updated_regions",
    "gpu_field_renderer_max_updated_regions",
  ].every((key) => numberToken(tokens, key) === 0);

const burnDurationStayedQueued = (tokens: StatusTokens): boolean =>
  tokens.burn_duration_proof_status === "queued" &&
  boolToken(tokens, "burn_duration_proof_sustained_heat_complete") &&
  tokens.burn_duration_proof_burn_start_tick === "placeholder" &&
  tokens.burn_duration_proof_depletion_tick === "placeholder";

const activeDeltas = (tokens: StatusTokens): boolean =>
  numberToken(tokens, "last_delta_count") > 0 || numberToken(tokens, "last_delta_consumer_visual_effect_events") > 0;

export const evaluateBlockers = (tokens: StatusTokens): BlockerReport[] => {
  const runtimeReady = hasLoadedRuntime(tokens);
  const rendererReady =
    boolToken(tokens, "gpu_field_renderer_enabled") &&
    boolToken(tokens, "gpu_field_renderer_material_ready") &&
    boolToken(tokens, "gpu_field_renderer_surface_bound");
  const burnDurationQueued = burnDurationStayedQueued(tokens);
  const slowPresetActive = tokens.fire_sim_preset === "slow-reactable";
  const storedGoodsProven = numberToken(tokens, "last_delta_consumer_stored_good_burn_destroyed_items") > 0;
  const explosiveStoredGoodsProven =
    numberToken(tokens, "last_delta_consumer_stored_good_burn_hazardous_goods") > 0 ||
    numberToken(tokens, "last_delta_consumer_explosive_infrastructure_triggered_targets") > 0 ||
    numberToken(tokens, "last_delta_consumer_explosive_infrastructure_native_triggered_targets") > 0;
  const contaminatedStoredGoodsProven =
    numberToken(tokens, "contamination_fire_contaminated_burn_sources") > 0 ||
    numberToken(tokens, "ash_field_contaminated_burn_sources") > 0 ||
    numberToken(tokens, "smoke_height_contaminated_smoke_cells") > 0;
  const fertileAshCollectionProven =
    numberToken(tokens, "fertile_ash_collected_goods") > 0 && numberToken(tokens, "fertile_ash_collection_depleted_cells") > 0;
  const taintedWashoutProven =
    numberToken(tokens, "ash_water_washout_tainted_ash_washed") > 0 ||
    numberToken(tokens, "ash_water_washout_water_taint_successes") > 0;
  const cropCountersProven =
    numberToken(tokens, "last_delta_consumer_crop_burn_killed_crops") > 0 ||
    numberToken(tokens, "last_delta_consumer_crop_burn_yield_lost") > 0;

  return [
    {
      blockers:
        rendererReady && activeDeltas(tokens) && rendererCountersAreZero(tokens)
          ? ["Renderer/material/surface are ready, but all GPU renderer visible/updated counters are still zero after active deltas."]
          : [],
      issue: 45,
      next:
        rendererReady && activeDeltas(tokens) && rendererCountersAreZero(tokens)
          ? ["Fix renderer counter instrumentation or the renderer update path, then rerun a normal-camera recording plus final status."]
          : ["Run a paired stimulus + recording + status pass to create active visual deltas before judging renderer counters."],
      status: rendererReady && activeDeltas(tokens) && rendererCountersAreZero(tokens) ? "blocked" : runtimeReady ? "ready" : "unknown",
      summary: "GPU field renderer gate.",
      title: "TWF-148: Gate Visual Field Renderer",
    },
    {
      blockers: burnDurationQueued
        ? ["Burn-duration proof completed sustained heat, but never observed burn_start or depletion."]
        : [],
      issue: 44,
      next: burnDurationQueued
        ? ["Create a cleaner slow-reactable burn target or fix why sustained heat does not transition to burn/depletion."]
        : ["Run slow-reactable preset, tree stimulus, water suppression, burn-duration proof, recording, and cooldown status."],
      status: burnDurationQueued ? "blocked" : runtimeReady ? "ready" : "unknown",
      summary: slowPresetActive ? "Slow-reactable behavior gate; preset is active in this status." : "Slow-reactable behavior gate.",
      title: "TWF-144: Gate Slow Reactable Wildfire Tuning",
    },
    {
      blockers: burnDurationQueued
        ? ["Burnout/cooling proof is not proving burn start or fuel depletion."]
        : [],
      issue: 43,
      next: burnDurationQueued
        ? ["Add or select a reliable burn-duration fixture target, then rerun the medium burn proof."]
        : ["Run qa-burn-duration-stimulus medium and inspect burn_start/depletion/cooling fields."],
      status: burnDurationQueued ? "blocked" : runtimeReady ? "ready" : "unknown",
      summary: "Burnout/cooling proof gate.",
      title: "TWF-092: Tune Burnout Cooling Behavior",
    },
    {
      blockers: [
        ...(storedGoodsProven ? [] : ["No current stored-good destruction proof in this status."]),
        ...(explosiveStoredGoodsProven ? [] : ["No explosive stored-good or native blast proof in this status."]),
        ...(contaminatedStoredGoodsProven ? [] : ["No contaminated stored-good smoke/ash proof in this status."]),
      ],
      issue: 60,
      next:
        explosiveStoredGoodsProven && contaminatedStoredGoodsProven
          ? ["Run focused Player.log scan and final status, then close the remaining stored-material QA gate."]
          : [
              "Use or create a save with burnable explosive stored goods and contaminated stored goods.",
              "Add a targeted stored-material fixture probe if the command bridge cannot identify those inventories from status alone.",
            ],
      status: explosiveStoredGoodsProven && contaminatedStoredGoodsProven ? "ready" : "blocked",
      summary: "Stored-material explosive/contaminated fixture gate.",
      title: "TWF-176: Make stored materials fuel blueprint-driven",
    },
    {
      blockers: [
        ...(storedGoodsProven ? [] : ["Stored-goods loss persistence still lacks a proven burnable stored-good target in this status."]),
        ...(fertileAshCollectionProven ? [] : ["Fertile ash collection/depletion is not proven."]),
        ...(cropCountersProven ? [] : ["Crop-specific burn counters are not proven."]),
        ...(taintedWashoutProven ? [] : ["Tainted ash washout/no-resurrection is not proven."]),
      ],
      issue: 17,
      next:
        storedGoodsProven && fertileAshCollectionProven && cropCountersProven && taintedWashoutProven
          ? ["Run the controlled save/quit/reload persistence matrix and compare before/after status/log tokens."]
          : [
              "Use or create a persistence fixture with burnable stored goods, collectible fertile ash, crop targets, and tainted ash plus water contact.",
              "Add focused fixture-readiness commands for fertile ash collection and tainted washout if current status cannot prove targetability.",
            ],
      status:
        storedGoodsProven && fertileAshCollectionProven && cropCountersProven && taintedWashoutProven
          ? "ready"
          : "blocked",
      summary: "World consequence persistence fixture gate.",
      title: "TWF-081: Validate World Consequence Persistence",
    },
  ];
};

const invokeStatus = (options: Options): string => {
  const args = ["scripts/invoke-timberborn-command.ts", "status", "--wait", String(options.waitSeconds)];
  const commandDirArgs = options.commandDir === null ? [] : ["--command-dir", options.commandDir];
  const result = Bun.spawnSync(["bun", ...args, ...commandDirArgs], {
    stderr: "pipe",
    stdout: "pipe",
  });
  const stdout = new TextDecoder().decode(result.stdout);
  const stderr = new TextDecoder().decode(result.stderr);

  if (result.exitCode !== 0) {
    fail((stderr || stdout).trim() || "status command failed.");
  }

  return stdout;
};

const loadInput = (options: Options): string => {
  if (options.fromFile === null) {
    return invokeStatus(options);
  }

  if (!existsSync(options.fromFile)) {
    fail(`Status file does not exist: ${options.fromFile}`);
  }

  return readFileSync(options.fromFile, "utf8");
};

const formatText = (reports: BlockerReport[]): string =>
  [
    "Wildfire QA blocker preflight",
    "",
    ...reports.flatMap((report) => [
      `#${report.issue} ${report.title}`,
      `status: ${report.status}`,
      `summary: ${report.summary}`,
      ...(report.blockers.length === 0 ? ["blockers: none detected from current status"] : ["blockers:", ...report.blockers.map((blocker) => `- ${blocker}`)]),
      "next:",
      ...report.next.map((next) => `- ${next}`),
      "",
    ]),
  ].join("\n");

const main = (): void => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  const input = loadInput(options);
  const reports = evaluateBlockers(parseStatusTokens(input));
  if (options.format === "json") {
    console.log(JSON.stringify({ reports }, null, 2));
    return;
  }

  console.log(formatText(reports));
};

if (import.meta.main) {
  try {
    main();
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  }
}
