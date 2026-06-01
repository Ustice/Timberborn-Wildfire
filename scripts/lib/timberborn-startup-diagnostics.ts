import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from "fs";
import { basename, join } from "path";
import type { ShellRunner, TimberbornStartupFailureKind } from "./timberborn-startup.ts";

export type ErrorReportInventoryEntry = {
  modifiedAt: string;
  modifiedMs: number;
  path: string;
  sizeBytes: number;
};

export type StartupExitDiagnosticsOptions = {
  afterErrorReports: ErrorReportInventoryEntry[];
  artifactDir: string;
  beforeErrorReports: ErrorReportInventoryEntry[];
  errorReportDir: string;
  failureKind: TimberbornStartupFailureKind;
  frontmostBundleId: string;
  launchIntentGuardDir: string;
  message: string;
  playerLogPath: string;
  processName: string;
  processRunning: boolean;
  run: ShellRunner;
};

export type StartupExitDiagnosticsBundle = {
  copiedErrorReports: string[];
  launchIntentCopyPath: string;
  playerLogCopyPath: string;
  playerLogScanPath: string;
  processSnapshotPath: string;
  summaryPath: string;
};

type PlayerLogScan = {
  matchedLines: string[];
  tailLines: string[];
};

const maxErrorReports = 12;
const playerLogTailLineCount = 80;
const playerLogScanLineCount = 80;
const processSnapshotCommand = "/bin/ps";
const processSnapshotArgs = ["-axo", "pid=,comm=,args="];

const compactLogToken = (value: string): string => value.replaceAll(/\s+/gu, "_").replaceAll('"', "'");

export const errorReportInventory = (errorReportDir: string): ErrorReportInventoryEntry[] => {
  if (!existsSync(errorReportDir)) {
    return [];
  }

  return readdirSync(errorReportDir, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.startsWith("error-report-") && entry.name.endsWith(".zip"))
    .map((entry) => {
      const path = join(errorReportDir, entry.name);
      const stat = statSync(path);
      return {
        modifiedAt: stat.mtime.toISOString(),
        modifiedMs: stat.mtimeMs,
        path,
        sizeBytes: stat.size,
      };
    })
    .sort((left, right) => right.modifiedMs - left.modifiedMs)
    .slice(0, maxErrorReports);
};

export const diffErrorReportInventory = (
  before: ErrorReportInventoryEntry[],
  after: ErrorReportInventoryEntry[],
): ErrorReportInventoryEntry[] => {
  const beforeByPath = new Map(before.map((entry) => [entry.path, entry]));
  return after.filter((entry) => {
    const previous = beforeByPath.get(entry.path);
    return previous === undefined || previous.modifiedMs !== entry.modifiedMs || previous.sizeBytes !== entry.sizeBytes;
  });
};

const readLaunchIntent = (launchIntentGuardDir: string): string => {
  const intentPath = join(launchIntentGuardDir, "intent.json");
  if (!existsSync(intentPath)) {
    return `launch intent metadata was missing: ${intentPath}\n`;
  }

  return readFileSync(intentPath, "utf8");
};

const captureProcessSnapshot = (run: ShellRunner, processName: string): string => {
  const result = run(processSnapshotCommand, processSnapshotArgs);
  const output = result.exitCode === 0 ? result.stdout : `${result.stdout}\n${result.stderr}`.trim();
  const lines = output
    .split(/\r?\n/u)
    .map((line) => line.trimEnd())
    .filter((line) => {
      const parts = line.trimStart().split(/\s+/u);
      return parts[1] === processName || parts[2]?.endsWith(`/${processName}`) === true;
    });

  return [
    `command=${processSnapshotCommand} ${processSnapshotArgs.join(" ")}`,
    `exit_code=${result.exitCode}`,
    `match_strategy=exact_comm_or_executable_basename`,
    `process_name=${processName}`,
    "",
    ...(lines.length > 0 ? lines : ["no_exact_process_match"]),
    "",
  ].join("\n");
};

const scanPlayerLog = (playerLogPath: string): PlayerLogScan => {
  if (!existsSync(playerLogPath)) {
    return {
      matchedLines: [`Player.log was missing: ${playerLogPath}`],
      tailLines: [],
    };
  }

  const lines = readFileSync(playerLogPath, "utf8").split(/\r?\n/u);
  const matchedLines = lines
    .filter((line) =>
      [
        "Starting game version",
        "ShutdownInProgress",
        "Input System module state changed to: Shutdown",
        "prematurely finalized",
        "Exception",
        "Fatal",
        "Crash",
        "wildfire_",
        "command bridge",
      ].some((token) => line.includes(token)),
    )
    .slice(-playerLogScanLineCount);

  return {
    matchedLines,
    tailLines: lines.slice(-playerLogTailLineCount),
  };
};

const writeInventory = (label: string, entries: ErrorReportInventoryEntry[]): string[] => [
  `[${label}]`,
  ...(entries.length > 0
    ? entries.map((entry) => `${entry.modifiedAt} size_bytes=${entry.sizeBytes} path=${entry.path}`)
    : ["none"]),
  "",
];

const copyErrorReports = (artifactDir: string, entries: ErrorReportInventoryEntry[]): string[] => {
  const targetDir = join(artifactDir, "error-reports");
  mkdirSync(targetDir, { recursive: true });
  return entries
    .filter((entry) => existsSync(entry.path))
    .map((entry) => {
      const targetPath = join(targetDir, basename(entry.path));
      copyFileSync(entry.path, targetPath);
      return targetPath;
    });
};

export const writeStartupExitDiagnosticsBundle = (
  options: StartupExitDiagnosticsOptions,
): StartupExitDiagnosticsBundle | null => {
  if (options.failureKind !== "timberborn_process_exit") {
    return null;
  }

  const bundleDir = join(options.artifactDir, "startup-process-exit-diagnostics");
  mkdirSync(bundleDir, { recursive: true });

  const changedErrorReports = diffErrorReportInventory(options.beforeErrorReports, options.afterErrorReports);
  const copiedErrorReports = copyErrorReports(bundleDir, changedErrorReports);
  const launchIntentCopyPath = join(bundleDir, "launch-intent.json");
  const processSnapshotPath = join(bundleDir, "process-snapshot.txt");
  const playerLogCopyPath = join(bundleDir, "Player.log");
  const playerLogScanPath = join(bundleDir, "Player-log-scan.txt");
  const summaryPath = join(bundleDir, "startup-process-exit-summary.txt");
  const playerLog = scanPlayerLog(options.playerLogPath);

  writeFileSync(launchIntentCopyPath, readLaunchIntent(options.launchIntentGuardDir));
  writeFileSync(processSnapshotPath, captureProcessSnapshot(options.run, options.processName));
  if (existsSync(options.playerLogPath)) {
    copyFileSync(options.playerLogPath, playerLogCopyPath);
  } else {
    writeFileSync(playerLogCopyPath, `Player.log was missing: ${options.playerLogPath}\n`);
  }
  writeFileSync(
    playerLogScanPath,
    [
      "[matched lines]",
      ...(playerLog.matchedLines.length > 0 ? playerLog.matchedLines : ["none"]),
      "",
      "[tail]",
      ...(playerLog.tailLines.length > 0 ? playerLog.tailLines : ["none"]),
      "",
    ].join("\n"),
  );

  const summary = [
    "wildfire_startup_exit_diagnostics=present",
    `failure_kind=${options.failureKind}`,
    `process_running=${options.processRunning}`,
    `frontmost_bundle_id=${options.frontmostBundleId}`,
    `error=${compactLogToken(options.message)}`,
    `artifacts_dir=${bundleDir}`,
    `player_log=${playerLogCopyPath}`,
    `player_log_scan=${playerLogScanPath}`,
    `process_snapshot=${processSnapshotPath}`,
    `launch_intent=${launchIntentCopyPath}`,
    `error_report_dir=${options.errorReportDir}`,
    `new_or_changed_error_reports=${changedErrorReports.length}`,
    `copied_error_reports=${copiedErrorReports.length > 0 ? copiedErrorReports.join(",") : "none"}`,
    "",
    ...writeInventory("error reports before", options.beforeErrorReports),
    ...writeInventory("error reports after", options.afterErrorReports),
  ].join("\n");
  writeFileSync(summaryPath, `${summary}\n`);

  return {
    copiedErrorReports,
    launchIntentCopyPath,
    playerLogCopyPath,
    playerLogScanPath,
    processSnapshotPath,
    summaryPath,
  };
};
