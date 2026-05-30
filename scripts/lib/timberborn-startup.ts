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

const maybeActivateTimberborn = (
  options: TimberbornStartupOptions,
  label: string,
): TimberbornStartupResult["activationStatus"] => {
  if (options.activationPolicy === "skip") {
    return "skipped";
  }

  try {
    activateTimberbornBundle({
      bundleId: options.bundleId,
      getFrontmostBundleId: options.getFrontmostBundleId,
      label,
      retryIntervalMs: options.activationRetryIntervalMs,
      run: options.run,
      sleepSyncMs: options.sleepSyncMs,
      timeoutMs: options.activationRetryTimeoutMs,
    });
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

    return {
      activationStatus: maybeActivateTimberborn(options, "attach"),
      launched: false,
      wasRunningBeforeLaunch,
    };
  }

  let launched = false;
  if (!wasRunningBeforeLaunch) {
    const openResult = options.run("open", ["-b", options.bundleId]);
    if (openResult.exitCode !== 0) {
      throw new Error(`Could not launch Timberborn bundle ${options.bundleId}: ${commandFailureText(openResult)}`);
    }
    launched = true;
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

  throw new Error(`Timed out waiting for ${options.processName} to start.`);
};
