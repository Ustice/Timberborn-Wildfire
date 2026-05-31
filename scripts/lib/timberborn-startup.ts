import { existsSync, mkdirSync, readFileSync, rmSync, statSync, writeFileSync } from "fs";
import { dirname, join } from "path";

export type TimberbornStartupMode = "attach" | "launch";

export type ShellResult = {
  exitCode: number;
  stderr: string;
  stdout: string;
};

export type ShellRunner = (command: string, args: string[]) => ShellResult;

export type TimberbornStartupResult = {
  activationStatus: "activated" | "failed" | "skipped";
  launched: boolean;
  wasRunningBeforeLaunch: boolean;
};

export type TimberbornStartupOptions = {
  activationPolicy: "best-effort" | "required" | "skip";
  activationRetryIntervalMs?: number;
  activationRetryTimeoutMs?: number;
  bundleId: string;
  getFrontmostBundleId?: () => string | null;
  launchIntentGuard?: {
    dir: string;
    nowMs?: () => number;
    ttlMs: number;
  };
  log?: (message: string) => void;
  mode: TimberbornStartupMode;
  processName: string;
  run: ShellRunner;
  sleepMs?: (milliseconds: number) => Promise<void>;
  sleepSyncMs?: (milliseconds: number) => void;
  waitSeconds: number;
};

const defaultPollIntervalMs = 500;
const defaultActivationRetryIntervalMs = 500;
const defaultActivationRetryTimeoutMs = 20_000;

type LaunchIntentMetadata = {
  bundleId: string;
  createdAt: string;
  pid: number;
  processName: string;
  timestampMs: number;
  ttlMs: number;
};

export const commandFailureText = (result: ShellResult): string =>
  result.stderr.trim() || result.stdout.trim() || "(no stderr/stdout)";

export const isTimberbornRunning = (run: ShellRunner, processName: string): boolean =>
  run("pgrep", ["-x", processName]).exitCode === 0;

const failActivation = (
  bundleId: string,
  label: string,
  attempts: number,
  timeoutMs: number,
  lastFailure: string,
): never => {
  throw new Error(
    `Could not activate Timberborn bundle ${bundleId} for ${label} after ${attempts} attempts over ${timeoutMs}ms: ${lastFailure}`,
  );
};

export const activateTimberbornBundle = (options: {
  bundleId: string;
  getFrontmostBundleId?: () => string | null;
  label: string;
  retryIntervalMs?: number;
  run: ShellRunner;
  sleepSyncMs?: (milliseconds: number) => void;
  timeoutMs?: number;
}): void => {
  const retryIntervalMs = options.retryIntervalMs ?? defaultActivationRetryIntervalMs;
  const timeoutMs = options.timeoutMs ?? defaultActivationRetryTimeoutMs;
  const maxAttempts = Math.max(1, Math.floor(timeoutMs / retryIntervalMs) + 1);
  let attempts = 0;
  let lastFailure = "(activation was not attempted)";

  const activated = Array.from({ length: maxAttempts }).some((_, index) => {
    attempts = index + 1;
    const result = options.run("osascript", ["-e", `tell application id "${options.bundleId}" to activate`]);
    if (result.exitCode === 0) {
      const frontmostBundleId = options.getFrontmostBundleId?.();
      if (frontmostBundleId === undefined || frontmostBundleId === options.bundleId) {
        return true;
      }

      lastFailure = `frontmost_bundle_id=${frontmostBundleId ?? "unknown"}`;
    } else {
      lastFailure = commandFailureText(result);
    }

    if (index < maxAttempts - 1) {
      options.sleepSyncMs?.(retryIntervalMs);
    }

    return false;
  });

  if (!activated) {
    failActivation(options.bundleId, options.label, attempts, timeoutMs, lastFailure);
  }
};

const compactLogToken = (value: string): string => value.replaceAll(/\s+/gu, "_").replaceAll('"', "'");

const launchIntentMetadataPath = (dir: string): string => join(dir, "intent.json");

const readLaunchIntentMetadata = (dir: string): LaunchIntentMetadata | null => {
  try {
    const parsed = JSON.parse(readFileSync(launchIntentMetadataPath(dir), "utf8")) as Partial<LaunchIntentMetadata>;
    return typeof parsed.timestampMs === "number" &&
      typeof parsed.ttlMs === "number" &&
      typeof parsed.createdAt === "string"
      ? {
          bundleId: parsed.bundleId ?? "unknown",
          createdAt: parsed.createdAt,
          pid: parsed.pid ?? 0,
          processName: parsed.processName ?? "unknown",
          timestampMs: parsed.timestampMs,
          ttlMs: parsed.ttlMs,
        }
      : null;
  } catch {
    return null;
  }
};

const writeLaunchIntentMetadata = (
  options: TimberbornStartupOptions,
  dir: string,
  nowMs: number,
  ttlMs: number,
): void => {
  writeFileSync(
    launchIntentMetadataPath(dir),
    `${JSON.stringify(
      {
        bundleId: options.bundleId,
        createdAt: new Date(nowMs).toISOString(),
        pid: process.pid,
        processName: options.processName,
        timestampMs: nowMs,
        ttlMs,
      } satisfies LaunchIntentMetadata,
      null,
      2,
    )}\n`,
  );
};

const launchIntentAgeMs = (dir: string, nowMs: number, metadata: LaunchIntentMetadata | null): number | null => {
  if (metadata !== null) {
    return Math.max(0, nowMs - metadata.timestampMs);
  }

  try {
    return Math.max(0, nowMs - statSync(dir).mtimeMs);
  } catch {
    return null;
  }
};

const acquireLaunchIntentGuard = (options: TimberbornStartupOptions): void => {
  const guard = options.launchIntentGuard;
  if (guard === undefined) {
    return;
  }

  const nowMs = guard.nowMs?.() ?? Date.now();
  mkdirSync(dirname(guard.dir), { recursive: true });

  try {
    mkdirSync(guard.dir);
    writeLaunchIntentMetadata(options, guard.dir, nowMs, guard.ttlMs);
    options.log?.(`launch_intent_recorded action=open_bundle path=${compactLogToken(guard.dir)} ttl_ms=${guard.ttlMs}`);
    return;
  } catch (error) {
    if (!existsSync(guard.dir)) {
      throw error;
    }
  }

  const metadata = readLaunchIntentMetadata(guard.dir);
  const ageMs = launchIntentAgeMs(guard.dir, nowMs, metadata);
  const activeTtlMs = Math.max(metadata?.ttlMs ?? guard.ttlMs, guard.ttlMs);
  if (ageMs === null || ageMs <= activeTtlMs) {
    options.log?.(
      `launch_intent_duplicate_refused action=open_bundle path=${compactLogToken(guard.dir)} age_ms=${ageMs ?? "unknown"} ttl_ms=${activeTtlMs} existing_pid=${metadata?.pid ?? "unknown"}`,
    );
    throw new Error(
      `Refusing duplicate Timberborn launch intent: a recent open -b ${options.bundleId} was recorded ${ageMs ?? "unknown"}ms ago at ${guard.dir}. Wait for that startup attempt to settle or use --attach once Timberborn is running.`,
    );
  }

  rmSync(guard.dir, { force: true, recursive: true });
  mkdirSync(guard.dir);
  writeLaunchIntentMetadata(options, guard.dir, nowMs, guard.ttlMs);
  options.log?.(
    `launch_intent_recorded action=open_bundle path=${compactLogToken(guard.dir)} ttl_ms=${guard.ttlMs} replaced_stale=true previous_age_ms=${ageMs ?? "unknown"}`,
  );
};

const maybeActivateTimberborn = (
  options: TimberbornStartupOptions,
  label: string,
): TimberbornStartupResult["activationStatus"] => {
  if (options.activationPolicy === "skip") {
    return "skipped";
  }

  try {
    options.log?.(`activation_intent label=${label} action=activate_bundle`);
    activateTimberbornBundle({
      bundleId: options.bundleId,
      getFrontmostBundleId: options.getFrontmostBundleId,
      label,
      retryIntervalMs: options.activationRetryIntervalMs,
      run: options.run,
      sleepSyncMs: options.sleepSyncMs,
      timeoutMs: options.activationRetryTimeoutMs,
    });
    options.log?.(`activation_result label=${label} status=activated`);
    return "activated";
  } catch (error) {
    if (options.activationPolicy === "required") {
      throw error;
    }

    const message = error instanceof Error ? error.message : String(error);
    options.log?.(`activation_pending label=${label} error=${compactLogToken(message)}`);
    return "failed";
  }
};

export const launchOrAttachTimberborn = async (
  options: TimberbornStartupOptions,
): Promise<TimberbornStartupResult> => {
  const sleepMs = options.sleepMs ?? ((milliseconds) => Bun.sleep(milliseconds));
  const waitMs = options.waitSeconds * 1000;
  const pollAttempts = Math.max(1, Math.floor(waitMs / defaultPollIntervalMs) + 1);
  const wasRunningBeforeLaunch = isTimberbornRunning(options.run, options.processName);

  if (options.mode === "attach") {
    if (!wasRunningBeforeLaunch) {
      throw new Error("Timberborn is not running. Start it first or pass --launch.");
    }

    options.log?.("startup_intent mode=attach timberborn_running=true action=activate");
    return {
      activationStatus: maybeActivateTimberborn(options, "attach"),
      launched: false,
      wasRunningBeforeLaunch,
    };
  }

  let launched = false;
  if (!wasRunningBeforeLaunch) {
    acquireLaunchIntentGuard(options);
    options.log?.("startup_intent mode=launch timberborn_running=false action=open_bundle");
    const openResult = options.run("open", ["-b", options.bundleId]);
    if (openResult.exitCode !== 0) {
      throw new Error(`Could not launch Timberborn bundle ${options.bundleId}: ${commandFailureText(openResult)}`);
    }
    launched = true;
  } else {
    options.log?.("startup_intent mode=launch timberborn_running=true action=attach");
  }

  for (const index of Array.from({ length: pollAttempts }).map((_, attempt) => attempt)) {
    if (isTimberbornRunning(options.run, options.processName)) {
      const activationStatus = maybeActivateTimberborn(options, "launch");
      options.log?.("timberborn_running=true");
      return {
        activationStatus,
        launched,
        wasRunningBeforeLaunch,
      };
    }

    if (index < pollAttempts - 1) {
      await sleepMs(defaultPollIntervalMs);
    }
  }

  options.log?.(
    `timberborn_startup_timeout process_running=false launched=${launched} was_running_before_launch=${wasRunningBeforeLaunch}`,
  );
  throw new Error(`Timed out waiting for ${options.processName} to start.`);
};
