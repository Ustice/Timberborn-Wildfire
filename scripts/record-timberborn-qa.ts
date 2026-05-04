#!/usr/bin/env bun

import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from "fs";
import { dirname, join, resolve } from "path";

type RecordingMode = "high" | "low";
type SourceKind = "display" | "rect" | "window";

type Rect = {
  height: number;
  width: number;
  x: number;
  y: number;
};

type ShellResult = {
  exitCode: number;
  stderr: string;
  stdout: string;
};

type DisplayInfo = {
  displayIndex: number;
  isMain: boolean;
  name: string;
  resolution: string;
};

type CompanionEvidencePlan = {
  boundedPlayerLogPath: string;
  commandBridgeDir: string;
  commandInboxPath: string;
  commandOutboxCopyPath: string;
  commandOutboxPath: string;
  finalQaLockStatePath: string;
  lockPaths: string[];
  playerLogCopyPath: string;
  playerLogSourcePath: string;
};

type Options = {
  activate: boolean;
  artifactsDir: string;
  audio: boolean;
  commands: string[];
  commandsFile: string | null;
  display: number;
  dryRun: boolean;
  durationSeconds: number;
  help: boolean;
  includeCursor: boolean;
  lowResolution: string;
  mode: RecordingMode;
  note: string | null;
  rect: Rect | null;
  saveName: string | null;
  scenarioName: string | null;
  showClicks: boolean;
  source: SourceKind | null;
};

type CapturePlan = {
  artifactDir: string;
  commandSequence: string[];
  companionEvidence: CompanionEvidencePlan;
  captureResolution: string;
  displayGeometryNote: string;
  displays: DisplayInfo[];
  metadataPath: string;
  outputPath: string;
  planPath: string;
  rect: Rect | null;
  screencaptureArgs: string[] | null;
  selectedDisplay: DisplayInfo | null;
  source: SourceKind;
  timberbornPid: string | null;
  windowBoundsStatus: string;
  windowBounds: Rect | null;
};

const home = process.env.HOME ?? "";
const bundleId = "com.mechanistry.timberborn";
const processName = "Timberborn";
const qaRoot = join(home, "Library", "Application Support", "Mechanistry", "Timberborn", "WildfireQA");
const commandInboxFileName = "command-inbox.txt";
const commandOutboxFileName = "command-outbox.txt";
const playerLogDefault = join(home, "Library", "Logs", "Mechanistry", "Timberborn", "Player.log");
const legacyLockRoot = join(home, "Library", "Application Support", "Timberborn", "WildfireQA", "locks");
const mechanistryLockRoot = join(qaRoot, "locks");

const usage = `Usage:
  bun scripts/record-timberborn-qa.ts [options]

Modes:
  --mode high               Full-display recording for visual-effect readability. Default.
  --mode low                Smaller region recording for spread, suppression, and burnout comparison.

Options:
  --duration <seconds>      Recording length. Defaults: high=12, low=8.
  --display <number>        macOS screencapture display number. Default: 1.
  --source <kind>           Capture source: display, rect, window. Defaults: high=display, low=rect.
  --rect <x,y,w,h>          Explicit capture rectangle. Implies --source rect.
  --low-resolution <WxH>    Low-mode centered capture size when --rect is omitted. Default: 1280x720.
  --save-name <name>        Save name to write into metadata.
  --scenario-name <name>    Scenario name to write into metadata.
  --command <text>          QA command or action to write into metadata. Can be repeated.
  --commands-file <path>    Newline-delimited command sequence to write into metadata.
  --note <text>             Freeform review note to write into metadata.
  --artifacts-dir <path>    Evidence root. Default: ~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/screen-recordings.
  --no-activate             Do not activate Timberborn before recording.
  --include-cursor          Include cursor in the recording.
  --show-clicks             Show clicks in the recording.
  --audio                   Capture default input audio.
  --dry-run                 Print the plan and write no files.
  --help                    Show this help.

Examples:
  bun scripts/record-timberborn-qa.ts --dry-run --mode high --duration=6 --save-name release-loop
  bun scripts/record-timberborn-qa.ts --mode high --duration=10 --command "qa-delta-stimulus then qa-readiness"
  bun scripts/record-timberborn-qa.ts --mode low --duration=20 --scenario-name water-barrier
  bun scripts/record-timberborn-qa.ts --dry-run --no-activate --source window
`;

const log = (message: string): void => {
  console.log(`[wildfire-recording] ${message}`);
};

const fail = (message: string): never => {
  throw new Error(`[wildfire-recording] ${message}`);
};

const requireValue = (args: string[], index: number, flag: string): string => {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    fail(`${flag} requires a value.`);
  }

  return value;
};

const parseSeconds = (value: string, flag: string): number => {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    fail(`Invalid ${flag} value: ${value}`);
  }

  return parsed;
};

const parsePositiveInteger = (value: string, flag: string): number => {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    fail(`Invalid ${flag} value: ${value}`);
  }

  return parsed;
};

const parseMode = (value: string): RecordingMode => {
  if (value === "high" || value === "low") {
    return value;
  }

  return fail(`Invalid mode: ${value}. Expected high or low.`);
};

const parseSource = (value: string): SourceKind => {
  if (value === "display" || value === "rect" || value === "window") {
    return value;
  }

  return fail(`Invalid source: ${value}. Expected display, rect, or window.`);
};

const parseResolution = (value: string): string => {
  const match = value.match(/^(\d+)x(\d+)$/u) ?? fail(`Invalid resolution: ${value}. Expected WxH, for example 1280x720.`);

  const width = parsePositiveInteger(match[1], "resolution width");
  const height = parsePositiveInteger(match[2], "resolution height");
  return `${width}x${height}`;
};

const parseRect = (value: string): Rect => {
  const parts = value.split(",").map((part) => Number(part.trim()));
  if (parts.length !== 4 || parts.some((part) => !Number.isInteger(part))) {
    fail(`Invalid rect: ${value}. Expected x,y,w,h.`);
  }

  const [x, y, width, height] = parts;
  if (width <= 0 || height <= 0) {
    fail(`Invalid rect: ${value}. Width and height must be positive.`);
  }

  return { height, width, x, y };
};

const parseArgs = (args: string[]): Options => {
  const options: Options = {
    activate: true,
    artifactsDir: join(qaRoot, "screen-recordings"),
    audio: false,
    commands: [],
    commandsFile: null,
    display: 1,
    dryRun: false,
    durationSeconds: 0,
    help: false,
    includeCursor: false,
    lowResolution: "1280x720",
    mode: "high",
    note: null,
    rect: null,
    saveName: null,
    scenarioName: null,
    showClicks: false,
    source: null,
  };
  let skipNext = false;

  args.reduce((_, arg, index) => {
    if (skipNext) {
      skipNext = false;
      return undefined;
    }

    if (arg === "--artifacts-dir") {
      options.artifactsDir = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--artifacts-dir=")) {
      options.artifactsDir = resolve(arg.slice("--artifacts-dir=".length));
    } else if (arg === "--audio") {
      options.audio = true;
    } else if (arg === "--command") {
      options.commands.push(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--command=")) {
      options.commands.push(arg.slice("--command=".length));
    } else if (arg === "--commands-file") {
      options.commandsFile = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--commands-file=")) {
      options.commandsFile = resolve(arg.slice("--commands-file=".length));
    } else if (arg === "--display") {
      options.display = parsePositiveInteger(requireValue(args, index + 1, arg), arg);
      skipNext = true;
    } else if (arg.startsWith("--display=")) {
      options.display = parsePositiveInteger(arg.slice("--display=".length), "--display");
    } else if (arg === "--dry-run") {
      options.dryRun = true;
    } else if (arg === "--duration") {
      options.durationSeconds = parseSeconds(requireValue(args, index + 1, arg), arg);
      skipNext = true;
    } else if (arg.startsWith("--duration=")) {
      options.durationSeconds = parseSeconds(arg.slice("--duration=".length), "--duration");
    } else if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--include-cursor") {
      options.includeCursor = true;
    } else if (arg === "--low-resolution") {
      options.lowResolution = parseResolution(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--low-resolution=")) {
      options.lowResolution = parseResolution(arg.slice("--low-resolution=".length));
    } else if (arg === "--mode") {
      options.mode = parseMode(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--mode=")) {
      options.mode = parseMode(arg.slice("--mode=".length));
    } else if (arg === "--no-activate") {
      options.activate = false;
    } else if (arg === "--note") {
      options.note = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--note=")) {
      options.note = arg.slice("--note=".length);
    } else if (arg === "--rect") {
      options.rect = parseRect(requireValue(args, index + 1, arg));
      options.source = "rect";
      skipNext = true;
    } else if (arg.startsWith("--rect=")) {
      options.rect = parseRect(arg.slice("--rect=".length));
      options.source = "rect";
    } else if (arg === "--save-name") {
      options.saveName = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--save-name=")) {
      options.saveName = arg.slice("--save-name=".length);
    } else if (arg === "--scenario-name") {
      options.scenarioName = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--scenario-name=")) {
      options.scenarioName = arg.slice("--scenario-name=".length);
    } else if (arg === "--show-clicks") {
      options.showClicks = true;
    } else if (arg === "--source") {
      options.source = parseSource(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--source=")) {
      options.source = parseSource(arg.slice("--source=".length));
    } else {
      fail(`Unknown argument: ${arg}`);
    }

    return undefined;
  }, undefined);

  return {
    ...options,
    durationSeconds: options.durationSeconds || (options.mode === "high" ? 12 : 8),
  };
};

const run = (command: string, args: string[]): ShellResult => {
  const result = Bun.spawnSync([command, ...args], {
    stderr: "pipe",
    stdout: "pipe",
  });

  return {
    exitCode: result.exitCode,
    stderr: result.stderr.toString(),
    stdout: result.stdout.toString(),
  };
};

const commandFailureText = (result: ShellResult): string => result.stderr.trim() || result.stdout.trim() || "(no stderr/stdout)";

const assertToolExists = (tool: string): void => {
  const result = run("/usr/bin/which", [tool]);
  if (result.exitCode !== 0) {
    fail(`Required tool is missing: ${tool}`);
  }
};

const getTimberbornPid = (): string | null => {
  const result = run("pgrep", ["-x", processName]);
  if (result.exitCode !== 0) {
    return null;
  }

  return result.stdout
    .split(/\s+/u)
    .map((pid) => pid.trim())
    .filter(Boolean)[0] ?? null;
};

const activateTimberborn = (): void => {
  const result = run("osascript", ["-e", `tell application id "${bundleId}" to activate`]);
  if (result.exitCode !== 0) {
    fail(`Could not activate Timberborn: ${commandFailureText(result)}`);
  }
};

const displayName = (display: Record<string, string>, fallbackIndex: number): string =>
  display._name ?? display.sppci_model ?? `display-${fallbackIndex}`;

const parseDimensions = (value: string): { height: number; width: number } | null => {
  const match = value.match(/(\d+)\s*x\s*(\d+)/u);
  if (!match) {
    return null;
  }

  return {
    height: Number(match[2]),
    width: Number(match[1]),
  };
};

const getDisplays = (): DisplayInfo[] => {
  const result = run("system_profiler", ["SPDisplaysDataType", "-json"]);
  if (result.exitCode !== 0) {
    return [];
  }

  try {
    const parsed = JSON.parse(result.stdout) as {
      SPDisplaysDataType?: Array<{
        spdisplays_ndrvs?: Array<Record<string, string>>;
      }>;
    };
    const displays = parsed.SPDisplaysDataType?.flatMap((gpu) => gpu.spdisplays_ndrvs ?? []) ?? [];
    return displays
      .map((display, index) => {
        const dimensions = parseDimensions(display._spdisplays_pixels ?? display.spdisplays_resolution ?? "");
        return dimensions === null
          ? null
          : {
              displayIndex: index + 1,
              isMain: display.spdisplays_main === "spdisplays_yes",
              name: displayName(display, index + 1),
              resolution: `${dimensions.width}x${dimensions.height}`,
            };
      })
      .filter((display): display is DisplayInfo => display !== null);
  } catch {
    return [];
  }
};

const getMainDisplayDimensions = (displays: DisplayInfo[]): { height: number; width: number } => {
  const fallback = { height: 1080, width: 1920 };
  const mainDisplay = displays.find((display) => display.isMain) ?? displays[0];
  const dimensions = mainDisplay ? parseDimensions(mainDisplay.resolution) : null;
  return dimensions ?? fallback;
};

const centeredRect = (resolution: string, displays: DisplayInfo[]): Rect => {
  const requested = parseDimensions(resolution) ?? fail(`Invalid low-resolution value: ${resolution}`);
  const display = getMainDisplayDimensions(displays);
  const width = Math.min(requested.width, display.width);
  const height = Math.min(requested.height, display.height);

  return {
    height,
    width,
    x: Math.max(0, Math.floor((display.width - width) / 2)),
    y: Math.max(0, Math.floor((display.height - height) / 2)),
  };
};

const getWindowBounds = (allowActivation: boolean): Rect | null => {
  const script = [
    ...(allowActivation ? [`tell application id "${bundleId}" to activate`] : []),
    `tell application "System Events" to tell process "${processName}"`,
    "if exists window 1 then",
    "set windowPosition to position of window 1",
    "set windowSize to size of window 1",
    "return (item 1 of windowPosition as text) & \",\" & (item 2 of windowPosition as text) & \",\" & (item 1 of windowSize as text) & \",\" & (item 2 of windowSize as text)",
    "end if",
    "end tell",
  ];
  const result = run("osascript", script.flatMap((line) => ["-e", line]));
  if (result.exitCode !== 0) {
    return null;
  }

  const text = result.stdout.trim();
  return text ? parseRect(text) : null;
};

const readCommandSequence = (options: Options): string[] => {
  const fileCommands =
    options.commandsFile === null
      ? []
      : readFileSync(options.commandsFile, "utf8")
          .split(/\r?\n/u)
          .map((line) => line.trim())
          .filter(Boolean);

  return [...options.commands, ...fileCommands];
};

const createArtifactDir = (options: Options): string => {
  const stamp = new Date().toISOString().replaceAll(/[:.]/gu, "-");
  return join(options.artifactsDir, `${stamp}-${options.mode}`);
};

const createCompanionEvidencePlan = (artifactDir: string): CompanionEvidencePlan => ({
  boundedPlayerLogPath: join(artifactDir, "Player-run-window-tail.log"),
  commandBridgeDir: qaRoot,
  commandInboxPath: join(qaRoot, commandInboxFileName),
  commandOutboxCopyPath: join(artifactDir, commandOutboxFileName),
  commandOutboxPath: join(qaRoot, commandOutboxFileName),
  finalQaLockStatePath: join(artifactDir, "final-qa-lock-state.txt"),
  lockPaths: [legacyLockRoot, mechanistryLockRoot],
  playerLogCopyPath: join(artifactDir, "Player.log"),
  playerLogSourcePath: playerLogDefault,
});

const buildScreencaptureArgs = (
  options: Options,
  outputPath: string,
  source: SourceKind,
  rect: Rect | null,
): string[] | null => {
  const sourceArgs =
    source === "display"
      ? [`-D${options.display}`]
      : rect === null
        ? null
        : [`-R${rect.x},${rect.y},${rect.width},${rect.height}`];
  if (sourceArgs === null) {
    return null;
  }

  return [
    "-x",
    "-v",
    `-V${options.durationSeconds}`,
    ...(options.includeCursor ? ["-C"] : []),
    ...(options.showClicks ? ["-k"] : []),
    ...(options.audio ? ["-g"] : []),
    ...sourceArgs,
    outputPath,
  ];
};

const createPlan = (options: Options): CapturePlan => {
  const source = options.source ?? (options.mode === "high" ? "display" : "rect");
  const displays = getDisplays();
  const selectedDisplay = displays.find((display) => display.displayIndex === options.display) ?? null;
  const allowWindowActivation = source === "window" && options.activate && !options.dryRun;
  const windowBounds = source === "window" ? getWindowBounds(allowWindowActivation) : null;
  const rect = source === "window" ? windowBounds : source === "rect" ? options.rect ?? centeredRect(options.lowResolution, displays) : null;
  const artifactDir = createArtifactDir(options);
  const outputPath = join(artifactDir, "recording.mov");

  return {
    artifactDir,
    commandSequence: readCommandSequence(options),
    companionEvidence: createCompanionEvidencePlan(artifactDir),
    captureResolution:
      rect === null
        ? (selectedDisplay?.resolution ?? "unknown")
        : `${rect.width}x${rect.height}`,
    displayGeometryNote:
      "system_profiler provides display order and resolution, but not global display origins; explicit --rect remains the source of truth for multi-display crops.",
    displays,
    metadataPath: join(artifactDir, "recording-metadata.json"),
    outputPath,
    planPath: join(artifactDir, "recording-plan.txt"),
    rect,
    screencaptureArgs: buildScreencaptureArgs(options, outputPath, source, rect),
    selectedDisplay,
    source,
    timberbornPid: getTimberbornPid(),
    windowBoundsStatus:
      source !== "window"
        ? "not_requested"
        : windowBounds === null
          ? allowWindowActivation
            ? "unresolved_after_activation"
            : "unresolved_without_activation"
          : allowWindowActivation
            ? "resolved_with_activation"
            : "resolved_without_activation",
    windowBounds,
  };
};

const metadataForPlan = (options: Options, plan: CapturePlan, status: "planned" | "recorded") => ({
  artifactDir: plan.artifactDir,
  captureTool: "macos_screencapture",
  commandSequence: plan.commandSequence,
  companionEvidence: {
    collectionStatus:
      "recording_tool_copies_player_log_tail_command_outbox_and_lock_state_when_available; qa_must_collect_command_outputs_and_any missing companion files before review",
    commandBridgeDir: plan.companionEvidence.commandBridgeDir,
    commandInboxPath: plan.companionEvidence.commandInboxPath,
    commandOutboxCopyPath: plan.companionEvidence.commandOutboxCopyPath,
    commandOutboxPath: plan.companionEvidence.commandOutboxPath,
    finalQaLockStatePath: plan.companionEvidence.finalQaLockStatePath,
    lockPaths: plan.companionEvidence.lockPaths,
    playerLogCopyPath: plan.companionEvidence.playerLogCopyPath,
    playerLogSourcePath: plan.companionEvidence.playerLogSourcePath,
    boundedPlayerLogPath: plan.companionEvidence.boundedPlayerLogPath,
    reviewerRequirement:
      "attach command output, copied Player.log or bounded run-window excerpt, and final QA lock state with each accepted recording",
  },
  createdAt: new Date().toISOString(),
  captureResolution: plan.captureResolution,
  display: plan.source === "display" ? plan.screencaptureArgs?.find((arg) => arg.startsWith("-D"))?.slice(2) : null,
  displayGeometryNote: plan.displayGeometryNote,
  displays: plan.displays,
  durationSeconds: options.durationSeconds,
  frameRate: "macos_screencapture_default_unreported",
  mode: options.mode,
  note: options.note,
  outputPath: plan.outputPath,
  outputSizeBytes: existsSync(plan.outputPath) ? statSync(plan.outputPath).size : null,
  rect: plan.rect,
  requestedLowResolution: options.mode === "low" ? options.lowResolution : null,
  saveName: options.saveName,
  scenarioName: options.scenarioName,
  screencaptureArgs: plan.screencaptureArgs,
  selectedDisplay: plan.selectedDisplay,
  source: plan.source,
  status,
  timberbornPid: plan.timberbornPid,
  windowBoundsStatus: plan.windowBoundsStatus,
  windowBounds: plan.windowBounds,
});

const planText = (options: Options, plan: CapturePlan): string =>
  [
    `mode=${options.mode}`,
    `source=${plan.source}`,
    `duration_seconds=${options.durationSeconds}`,
    `capture_resolution=${plan.captureResolution}`,
    `timberborn_pid=${plan.timberbornPid ?? "not_running"}`,
    `save_name=${options.saveName ?? "not_provided"}`,
    `scenario_name=${options.scenarioName ?? "not_provided"}`,
    `artifact_dir=${plan.artifactDir}`,
    `output=${plan.outputPath}`,
    `metadata=${plan.metadataPath}`,
    `player_log_copy=${plan.companionEvidence.playerLogCopyPath}`,
    `player_log_tail=${plan.companionEvidence.boundedPlayerLogPath}`,
    `command_inbox=${plan.companionEvidence.commandInboxPath}`,
    `command_outbox_copy=${plan.companionEvidence.commandOutboxCopyPath}`,
    `final_qa_lock_state=${plan.companionEvidence.finalQaLockStatePath}`,
    `rect=${plan.rect === null ? "not_used" : `${plan.rect.x},${plan.rect.y},${plan.rect.width},${plan.rect.height}`}`,
    `display=${plan.source === "display" ? options.display : "not_used"}`,
    `display_geometry_note=${plan.displayGeometryNote}`,
    `window_bounds=${
      plan.windowBounds === null
        ? "not_used"
        : `${plan.windowBounds.x},${plan.windowBounds.y},${plan.windowBounds.width},${plan.windowBounds.height}`
    }`,
    `window_bounds_status=${plan.windowBoundsStatus}`,
    `command_sequence=${plan.commandSequence.length === 0 ? "not_provided" : plan.commandSequence.join(" | ")}`,
    `screencapture_command=${
      plan.screencaptureArgs === null
        ? "unavailable_until_window_bounds_resolve_without_activation_or_with_live_activation"
        : `screencapture ${plan.screencaptureArgs.map((arg) => JSON.stringify(arg)).join(" ")}`
    }`,
    "",
  ].join("\n");

const writePlanArtifacts = (options: Options, plan: CapturePlan, status: "planned" | "recorded"): void => {
  mkdirSync(plan.artifactDir, { recursive: true });
  writeFileSync(plan.planPath, planText(options, plan));
  writeFileSync(plan.metadataPath, `${JSON.stringify(metadataForPlan(options, plan, status), null, 2)}\n`);
  writeFileSync(join(plan.artifactDir, "command-sequence.txt"), `${plan.commandSequence.join("\n")}\n`);
};

const copyIfPresent = (sourcePath: string, targetPath: string, missingMessage: string): void => {
  if (existsSync(sourcePath)) {
    copyFileSync(sourcePath, targetPath);
  } else {
    writeFileSync(targetPath, `${missingMessage}\n`);
  }
};

const writeBoundedPlayerLog = (sourcePath: string, targetPath: string): void => {
  if (!existsSync(sourcePath)) {
    writeFileSync(targetPath, `Player.log was missing: ${sourcePath}\n`);
    return;
  }

  const tail = readFileSync(sourcePath, "utf8").split(/\r?\n/u).slice(-400).join("\n");
  writeFileSync(targetPath, `${tail.trimEnd()}\n`);
};

const describeLockPath = (lockPath: string): string => {
  if (!existsSync(lockPath)) {
    return `${lockPath}: missing`;
  }

  const stats = statSync(lockPath);
  if (!stats.isDirectory()) {
    return `${lockPath}: present file size=${stats.size}`;
  }

  const entries = readdirSync(lockPath);
  return `${lockPath}: present entries=${entries.length === 0 ? "none" : entries.join(",")}`;
};

const writeCompanionEvidence = (plan: CapturePlan): void => {
  mkdirSync(plan.artifactDir, { recursive: true });
  copyIfPresent(
    plan.companionEvidence.playerLogSourcePath,
    plan.companionEvidence.playerLogCopyPath,
    `Player.log was missing: ${plan.companionEvidence.playerLogSourcePath}`,
  );
  writeBoundedPlayerLog(plan.companionEvidence.playerLogSourcePath, plan.companionEvidence.boundedPlayerLogPath);
  copyIfPresent(
    plan.companionEvidence.commandOutboxPath,
    plan.companionEvidence.commandOutboxCopyPath,
    `command-outbox.txt was missing: ${plan.companionEvidence.commandOutboxPath}`,
  );
  writeFileSync(
    plan.companionEvidence.finalQaLockStatePath,
    [
      "Final QA lock state captured by record-timberborn-qa.",
      "QA should verify no stale lock remains before accepting live recording evidence.",
      ...plan.companionEvidence.lockPaths.map(describeLockPath),
      "",
    ].join("\n"),
  );
};

const printPlan = (options: Options, plan: CapturePlan): void => {
  planText(options, plan)
    .trimEnd()
    .split("\n")
    .map((line) => log(line));
};

const main = (): void => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  assertToolExists("screencapture");
  assertToolExists("osascript");
  const plan = createPlan(options);
  printPlan(options, plan);

  if (options.dryRun) {
    log("dry_run_complete");
    return;
  }

  const screencaptureArgs =
    plan.screencaptureArgs ??
    fail(`Capture source ${plan.source} requires resolved bounds. window_bounds_status=${plan.windowBoundsStatus}.`);

  mkdirSync(dirname(plan.outputPath), { recursive: true });
  writePlanArtifacts(options, plan, "planned");
  if (options.activate) {
    activateTimberborn();
  }

  const result = run("screencapture", screencaptureArgs);
  if (result.exitCode !== 0 || !existsSync(plan.outputPath)) {
    writeFileSync(join(plan.artifactDir, "recording-error.txt"), `${commandFailureText(result)}\n`);
    fail(`screencapture failed. See ${join(plan.artifactDir, "recording-error.txt")}`);
  }

  writeCompanionEvidence(plan);
  writePlanArtifacts(options, plan, "recorded");
  log(`recording=${plan.outputPath}`);
  log(`metadata=${plan.metadataPath}`);
};

try {
  main();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
}
