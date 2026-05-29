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

type Mode = "attach" | "launch";
type ScreenshotMode = "always" | "failure" | "never";

type StartupOptions = {
  artifactsDir: string;
  commandDir: string;
  dryRun: boolean;
  expectedResolution: string;
  forceLock: boolean;
  help: boolean;
  lockTimeoutSeconds: number;
  mode: Mode;
  playerLogPath: string;
  requireCommandStatus: boolean;
  requiredTokens: string[];
  screenshotMode: ScreenshotMode;
  skipResolutionCheck: boolean;
  waitSeconds: number;
};

type EvidenceResult = {
  failureLines: string[];
  missingTokens: string[];
  successLines: string[];
};

type LogBaseline = {
  exists: boolean;
  mtimeMs: number;
  size: number;
};

type CommandStatusResult = {
  missingParts: string[];
  text: string;
};

const home = process.env.HOME ?? "";
const bundleId = "com.mechanistry.timberborn";
const processName = "Timberborn";
const qaRoot = join(home, "Library", "Application Support", "Mechanistry", "Timberborn", "WildfireQA");
const qaTempRoot = join("/tmp", "wildfire-qa");
const lockDir = join(home, "Library", "Application Support", "Timberborn", "WildfireQA", "locks", "build-deploy.lock");
const lockInfoPath = join(lockDir, "lock.json");
const playerLogDefault = join(home, "Library", "Logs", "Mechanistry", "Timberborn", "Player.log");
const commandOutboxFileName = "command-outbox.txt";
const commandInboxFileName = "command-inbox.txt";
const defaultRequiredTokens = [
  "wildfire_command_bridge_ready",
  "wildfire_timberborn_runtime_ready",
  "wildfire_timberborn_diagnostic_asset_loaded",
  "wildfire_timberborn_compute_asset_loaded",
  "wildfire_timberborn_gpu_factory_created",
  "wildfire_timberborn_runtime_simulator_initialized",
];
const failureTokens = [
  "wildfire_timberborn_runtime_initialize_failed",
  "wildfire_timberborn_compute_asset_load_failed",
  "wildfire_timberborn_diagnostic_asset_load_failed",
  "wildfire_timberborn_gpu_dispatch_failed",
  "Mod JasonKleinberg.Wildfire failed",
  "Wildfire.Timberborn",
  "Exception",
];

const usage = `Usage:
  bun scripts/check-timberborn-startup.ts [options]

Modes:
  --attach                  Require Timberborn to already be running. Default.
  --launch                  Launch Timberborn by bundle id, then wait for startup evidence.

Options:
  --wait <seconds>          Seconds to wait for required Player.log evidence. Default: 90.
  --player-log <path>       Player.log path. Default: ~/Library/Logs/Mechanistry/Timberborn/Player.log.
  --command-dir <path>      Wildfire command bridge directory. Default: ~/Library/Application Support/Mechanistry/Timberborn/WildfireQA.
  --require-token <token>   Require an additional searchable Player.log token. Can be repeated.
  --require-command-status  Write read-only status to the command bridge and require a successful simulator-integrated result.
  --artifacts-dir <path>    Evidence root. Default: /tmp/wildfire-qa/startup-harness.
  --screenshot <mode>       Screenshot capture mode: failure, always, never. Default: failure.
  --expected-resolution <WxH> Display resolution required by the coordinate guide. Default: 1920x1080.
  --skip-resolution-check   Skip display validation. Use only when no UI automation or screenshot claim is needed.
  --lock-timeout <seconds>  Seconds to wait for the shared deploy/QA lock. Default: 0.
  --force-lock              Remove an existing shared lock before acquiring it.
  --dry-run                 Print the plan and validate local preconditions without launching or waiting.
  --help                    Show this help.

Examples:
  bun scripts/check-timberborn-startup.ts --attach --wait=30
  bun scripts/check-timberborn-startup.ts --launch --wait=120
  bun scripts/check-timberborn-startup.ts --attach --require-command-status --wait=10
`;

const log = (message: string): void => {
  console.log(`[wildfire-startup] ${message}`);
};

const fail = (message: string): never => {
  throw new Error(`[wildfire-startup] ${message}`);
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
  if (!Number.isFinite(parsed) || parsed < 0) {
    fail(`Invalid ${flag} value: ${value}`);
  }

  return parsed;
};

const parseMode = (value: string): Mode => {
  if (value === "attach" || value === "launch") {
    return value;
  }

  return fail(`Invalid mode: ${value}. Expected attach or launch.`);
};

const parseScreenshotMode = (value: string): ScreenshotMode => {
  if (value === "always" || value === "failure" || value === "never") {
    return value;
  }

  return fail(`Invalid screenshot mode: ${value}. Expected failure, always, or never.`);
};

const parseArgs = (args: string[]): StartupOptions => {
  const options: StartupOptions = {
    artifactsDir: join(qaTempRoot, "startup-harness"),
    commandDir: qaRoot,
    dryRun: false,
    expectedResolution: "1920x1080",
    forceLock: false,
    help: false,
    lockTimeoutSeconds: 0,
    mode: "attach",
    playerLogPath: playerLogDefault,
    requireCommandStatus: false,
    requiredTokens: [...defaultRequiredTokens],
    screenshotMode: "failure",
    skipResolutionCheck: false,
    waitSeconds: 90,
  };
  let skipNext = false;

  args.reduce((_, arg, index) => {
    if (skipNext) {
      skipNext = false;
      return undefined;
    }

    if (arg === "--attach") {
      options.mode = "attach";
    } else if (arg === "--launch") {
      options.mode = "launch";
    } else if (arg === "--mode") {
      options.mode = parseMode(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--mode=")) {
      options.mode = parseMode(arg.slice("--mode=".length));
    } else if (arg === "--artifacts-dir") {
      options.artifactsDir = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--artifacts-dir=")) {
      options.artifactsDir = resolve(arg.slice("--artifacts-dir=".length));
    } else if (arg === "--command-dir") {
      options.commandDir = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--command-dir=")) {
      options.commandDir = resolve(arg.slice("--command-dir=".length));
    } else if (arg === "--dry-run") {
      options.dryRun = true;
    } else if (arg === "--expected-resolution") {
      options.expectedResolution = normalizeResolution(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--expected-resolution=")) {
      options.expectedResolution = normalizeResolution(arg.slice("--expected-resolution=".length));
    } else if (arg === "--force-lock") {
      options.forceLock = true;
    } else if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--lock-timeout") {
      options.lockTimeoutSeconds = parseSeconds(requireValue(args, index + 1, arg), arg);
      skipNext = true;
    } else if (arg.startsWith("--lock-timeout=")) {
      options.lockTimeoutSeconds = parseSeconds(arg.slice("--lock-timeout=".length), "--lock-timeout");
    } else if (arg === "--player-log") {
      options.playerLogPath = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--player-log=")) {
      options.playerLogPath = resolve(arg.slice("--player-log=".length));
    } else if (arg === "--require-command-status") {
      options.requireCommandStatus = true;
    } else if (arg === "--require-token") {
      options.requiredTokens.push(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--require-token=")) {
      options.requiredTokens.push(arg.slice("--require-token=".length));
    } else if (arg === "--screenshot") {
      options.screenshotMode = parseScreenshotMode(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--screenshot=")) {
      options.screenshotMode = parseScreenshotMode(arg.slice("--screenshot=".length));
    } else if (arg === "--skip-resolution-check") {
      options.skipResolutionCheck = true;
    } else if (arg === "--wait") {
      options.waitSeconds = parseSeconds(requireValue(args, index + 1, arg), arg);
      skipNext = true;
    } else if (arg.startsWith("--wait=")) {
      options.waitSeconds = parseSeconds(arg.slice("--wait=".length), "--wait");
    } else {
      fail(`Unknown argument: ${arg}`);
    }

    return undefined;
  }, undefined);

  return options;
};

const normalizeResolution = (value: string): string => {
  const match = value.match(/^(\d+)\s*x\s*(\d+)$/iu);
  if (!match) {
    return fail(`Invalid resolution: ${value}. Expected WxH, for example 1920x1080.`);
  }

  return `${match[1]}x${match[2]}`;
};

const run = (command: string, args: string[]): { exitCode: number; stderr: string; stdout: string } => {
  const result = Bun.spawnSync([command, ...args], {
    stdout: "pipe",
    stderr: "pipe",
  });

  return {
    exitCode: result.exitCode,
    stderr: result.stderr.toString(),
    stdout: result.stdout.toString(),
  };
};

const isTimberbornRunning = (): boolean => run("pgrep", ["-x", processName]).exitCode === 0;

const readLock = (): string => {
  try {
    return readFileSync(lockInfoPath, "utf8");
  } catch {
    return "(lock metadata unavailable)";
  }
};

const acquireLock = (options: StartupOptions): (() => void) => {
  mkdirSync(dirname(lockDir), { recursive: true });

  if (options.forceLock && existsSync(lockDir)) {
    log(`force_removing_lock path=${lockDir}`);
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
            command: "check-timberborn-startup",
            mode: options.mode,
            pid: process.pid,
            startedAt: new Date().toISOString(),
          },
          null,
          2,
        )}\n`,
      );
      log(`acquired_lock path=${lockDir}`);
      return () => {
        rmSync(lockDir, { recursive: true, force: true });
        log(`released_lock path=${lockDir}`);
      };
    } catch (error) {
      if (!existsSync(lockDir)) {
        throw error;
      }

      const elapsedSeconds = (Date.now() - startedAt) / 1000;
      if (elapsedSeconds >= options.lockTimeoutSeconds) {
        fail(`QA lock is held at ${lockDir}: ${readLock()}`);
      }

      Bun.sleepSync(1000);
    }
  }
};

const assertResolution = (expectedResolution: string): void => {
  const result = run("system_profiler", ["SPDisplaysDataType"]);
  if (result.exitCode !== 0) {
    fail(`Could not inspect display resolution: ${result.stderr.trim() || result.stdout.trim()}`);
  }

  const normalizedOutput = result.stdout.replaceAll(/\s+/gu, " ");
  const expectedForSystemProfiler = expectedResolution.replace("x", " x ");
  if (!normalizedOutput.includes(`Resolution: ${expectedForSystemProfiler}`)) {
    fail(`Expected display resolution ${expectedResolution}, but system_profiler did not report it.`);
  }

  log(`resolution_ok expected=${expectedResolution}`);
};

const launchTimberborn = async (waitSeconds: number): Promise<void> => {
  const openResult = run("open", ["-b", bundleId]);
  if (openResult.exitCode !== 0) {
    fail(`Could not launch Timberborn bundle ${bundleId}: ${openResult.stderr.trim() || openResult.stdout.trim()}`);
  }

  const startedAt = Date.now();
  while (Date.now() - startedAt <= waitSeconds * 1000) {
    if (isTimberbornRunning()) {
      log("timberborn_running=true");
      return;
    }

    await Bun.sleep(500);
  }

  fail(`Timed out waiting for ${processName} to start.`);
};

const activateTimberborn = (): void => {
  const result = run("osascript", ["-e", `tell application id "${bundleId}" to activate`]);
  if (result.exitCode !== 0) {
    fail(`Could not activate Timberborn bundle ${bundleId}: ${result.stderr.trim() || result.stdout.trim()}`);
  }
};

const matchingLines = (text: string, tokens: string[]): string[] =>
  text
    .split(/\r?\n/u)
    .filter((line) => tokens.some((token) => line.includes(token)));

const captureLogBaseline = (playerLogPath: string): LogBaseline => {
  if (!existsSync(playerLogPath)) {
    return {
      exists: false,
      mtimeMs: 0,
      size: 0,
    };
  }

  const stats = statSync(playerLogPath);
  return {
    exists: true,
    mtimeMs: stats.mtimeMs,
    size: stats.size,
  };
};

const readCurrentLogWindow = (playerLogPath: string, baseline: LogBaseline): string => {
  if (!existsSync(playerLogPath)) {
    return "";
  }

  const playerLogText = readFileSync(playerLogPath, "utf8");
  const stats = statSync(playerLogPath);
  const canUseBaselineOffset = baseline.exists && stats.size >= baseline.size && stats.mtimeMs >= baseline.mtimeMs;

  return canUseBaselineOffset ? playerLogText.slice(baseline.size) : playerLogText;
};

const getEvidence = (
  playerLogPath: string,
  baseline: LogBaseline,
  requiredTokens: string[],
): EvidenceResult => {
  const currentLogText = readCurrentLogWindow(playerLogPath, baseline);
  const missingTokens = requiredTokens.filter((token) => !currentLogText.includes(token));
  return {
    failureLines: matchingLines(currentLogText, failureTokens),
    missingTokens,
    successLines: matchingLines(currentLogText, requiredTokens),
  };
};

const waitForEvidence = async (options: StartupOptions, baseline: LogBaseline): Promise<EvidenceResult> => {
  const startedAt = Date.now();
  let latestEvidence = getEvidence(options.playerLogPath, baseline, options.requiredTokens);

  while (
    latestEvidence.failureLines.length === 0 &&
    latestEvidence.missingTokens.length > 0 &&
    Date.now() - startedAt <= options.waitSeconds * 1000
  ) {
    await Bun.sleep(500);
    latestEvidence = getEvidence(options.playerLogPath, baseline, options.requiredTokens);
  }

  return latestEvidence;
};

const requireCommandStatus = async (options: StartupOptions): Promise<CommandStatusResult> => {
  const inboxPath = join(options.commandDir, commandInboxFileName);
  const outboxPath = join(options.commandDir, commandOutboxFileName);
  const previousModified = existsSync(outboxPath) ? statSync(outboxPath).mtimeMs : 0;

  mkdirSync(dirname(inboxPath), { recursive: true });
  writeFileSync(inboxPath, "status\n");
  log(`command_status_requested inbox=${inboxPath}`);

  const startedAt = Date.now();
  while (Date.now() - startedAt <= options.waitSeconds * 1000) {
    if (existsSync(outboxPath) && statSync(outboxPath).mtimeMs > previousModified) {
      const result = readFileSync(outboxPath, "utf8");
      const requiredStatusParts = [
        "wildfire_command_result",
        "command=status",
        "success=true",
        "simulator_integrated=true",
      ];
      const missingParts = requiredStatusParts.filter((part) => !result.includes(part));
      return { missingParts, text: result };
    }

    await Bun.sleep(100);
  }

  return {
    missingParts: ["command-outbox-update"],
    text: `Timed out waiting for command status outbox: ${outboxPath}`,
  };
};

const createArtifactDir = (options: StartupOptions): string => {
  const stamp = new Date().toISOString().replaceAll(/[:.]/gu, "-");
  const artifactDir = join(options.artifactsDir, stamp);
  mkdirSync(artifactDir, { recursive: true });
  return artifactDir;
};

const maybeCaptureScreenshot = (artifactDir: string, shouldCapture: boolean): string | null => {
  if (!shouldCapture) {
    return null;
  }

  const screenshotPath = join(artifactDir, "timberborn-startup.png");
  const result = run("screencapture", ["-x", screenshotPath]);
  if (result.exitCode !== 0 || !existsSync(screenshotPath)) {
    writeFileSync(
      join(artifactDir, "screenshot-error.txt"),
      `${result.stderr.trim() || result.stdout.trim() || "screencapture did not create an image."}\n`,
    );
    return null;
  }

  return screenshotPath;
};

const writeArtifacts = (
  options: StartupOptions,
  baseline: LogBaseline,
  evidence: EvidenceResult,
  commandStatus: CommandStatusResult | null,
  passed: boolean,
): string => {
  const artifactDir = createArtifactDir(options);
  const playerLogCopy = join(artifactDir, "Player.log");
  const evidencePath = join(artifactDir, "startup-evidence.txt");
  const commandOutboxPath = join(options.commandDir, commandOutboxFileName);
  const shouldCaptureScreenshot =
    options.screenshotMode === "always" || (options.screenshotMode === "failure" && !passed);

  if (existsSync(options.playerLogPath)) {
    copyFileSync(options.playerLogPath, playerLogCopy);
  } else {
    writeFileSync(playerLogCopy, `Player.log was missing: ${options.playerLogPath}\n`);
  }
  if (commandStatus !== null && existsSync(commandOutboxPath)) {
    copyFileSync(commandOutboxPath, join(artifactDir, commandOutboxFileName));
  }

  const screenshotPath = maybeCaptureScreenshot(artifactDir, shouldCaptureScreenshot);
  const summary = [
    `wildfire_startup_harness_result=${passed ? "pass" : "fail"}`,
    `mode=${options.mode}`,
    `player_log_baseline_exists=${baseline.exists}`,
    `player_log_baseline_size=${baseline.size}`,
    `player_log_baseline_mtime_ms=${baseline.mtimeMs}`,
    `player_log=${playerLogCopy}`,
    `missing_tokens=${evidence.missingTokens.join(",") || "none"}`,
    `missing_command_status=${commandStatus === null ? "not_requested" : commandStatus.missingParts.join(",") || "none"}`,
    `screenshot=${screenshotPath ?? "not_captured"}`,
    "",
    "[success evidence]",
    ...evidence.successLines,
    "",
    "[failure evidence]",
    ...evidence.failureLines,
    "",
    "[command status]",
    commandStatus?.text.trimEnd() ?? "not_requested_or_unavailable",
    "",
  ].join("\n");

  writeFileSync(evidencePath, summary);
  log(`artifacts_dir=${artifactDir}`);
  log(`evidence_file=${evidencePath}`);
  return artifactDir;
};

const printPlan = (options: StartupOptions): void => {
  log(`mode=${options.mode}`);
  log(`player_log=${options.playerLogPath}`);
  log(`command_dir=${options.commandDir}`);
  log(`artifacts_dir=${options.artifactsDir}`);
  log(`wait_seconds=${options.waitSeconds}`);
  log(`expected_resolution=${options.skipResolutionCheck ? "skipped" : options.expectedResolution}`);
  log(`required_tokens=${options.requiredTokens.join(",")}`);
  log(`require_command_status=${options.requireCommandStatus}`);
  log(`screenshot=${options.screenshotMode}`);
  log(`lock=${lockDir}`);
};

const main = async (): Promise<void> => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  printPlan(options);

  if (!options.skipResolutionCheck) {
    assertResolution(options.expectedResolution);
  }

  if (options.dryRun) {
    log("dry_run_complete");
    return;
  }

  const releaseLock = acquireLock(options);
  try {
    const baseline = captureLogBaseline(options.playerLogPath);
    log(`player_log_baseline exists=${baseline.exists} size=${baseline.size} mtime_ms=${baseline.mtimeMs}`);

    if (options.mode === "launch") {
      await launchTimberborn(options.waitSeconds);
    } else if (!isTimberbornRunning()) {
      fail("Timberborn is not running. Start it first or pass --launch.");
    }

    activateTimberborn();

    const initialEvidence = await waitForEvidence(options, baseline);
    const hasInitialRequiredEvidence = initialEvidence.missingTokens.length === 0;
    const commandStatus =
      hasInitialRequiredEvidence && initialEvidence.failureLines.length === 0 && options.requireCommandStatus
        ? await requireCommandStatus(options)
        : null;
    const evidence = getEvidence(options.playerLogPath, baseline, options.requiredTokens);
    const hasRequiredEvidence = evidence.missingTokens.length === 0;
    const hasFailureEvidence = evidence.failureLines.length > 0;
    const hasCommandStatus = !options.requireCommandStatus || commandStatus?.missingParts.length === 0;
    const passed = hasRequiredEvidence && !hasFailureEvidence && hasCommandStatus;
    const artifactsDir = writeArtifacts(options, baseline, evidence, commandStatus, passed);

    if (!passed) {
      const missingLogText =
        evidence.missingTokens.length > 0 ? `missing Player.log evidence: ${evidence.missingTokens.join(", ")}` : null;
      const failureLogText =
        evidence.failureLines.length > 0 ? `failure Player.log evidence: ${evidence.failureLines.join(" | ")}` : null;
      const missingCommandText =
        commandStatus && commandStatus.missingParts.length > 0
          ? `missing command status evidence: ${commandStatus.missingParts.join(", ")}`
          : null;
      fail(`${[missingLogText, failureLogText, missingCommandText].filter(Boolean).join("; ")}. Artifacts: ${artifactsDir}`);
    }

    log("startup_evidence_complete");
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  } finally {
    releaseLock();
  }
};

await main();
