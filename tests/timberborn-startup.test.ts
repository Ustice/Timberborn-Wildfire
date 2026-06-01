import { describe, expect, test } from "bun:test";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, utimesSync, writeFileSync } from "fs";
import { tmpdir } from "os";
import { join } from "path";
import {
  errorReportInventory,
  writeStartupExitDiagnosticsBundle,
} from "../scripts/lib/timberborn-startup-diagnostics.ts";
import {
  launchOrAttachTimberborn,
  recordLaunchIntentFailure,
  type ShellResult,
  type TimberbornStartupOptions,
} from "../scripts/lib/timberborn-startup.ts";

type Call = {
  args: string[];
  command: string;
};

type ShellHandler = (call: Call) => ShellResult;
type ShellStub = ShellHandler | ShellResult[];

const success = (stdout = ""): ShellResult => ({ exitCode: 0, stderr: "", stdout });
const failure = (stderr = ""): ShellResult => ({ exitCode: 1, stderr, stdout: "" });

const createRunner = (stub: ShellStub) => {
  const calls: Call[] = [];
  const handler: ShellHandler = Array.isArray(stub)
    ? () => stub[Math.min(calls.length - 1, stub.length - 1)] ?? success()
    : stub;

  return {
    calls,
    run: (command: string, args: string[]): ShellResult => {
      const call = { args, command };
      calls.push(call);
      return handler(call);
    },
  };
};

const commandCalls = (calls: Call[], command: string): Call[] => calls.filter((call) => call.command === command);

const runningTimberbornRunner = (activationResult: ShellResult = success()): ShellHandler => (call) => {
  if (call.command === "pgrep") {
    return success("123\n");
  }

  if (call.command === "osascript") {
    return activationResult;
  }

  return success();
};

const coldLaunchRunner = (): ShellHandler => {
  let running = false;

  return (call) => {
    if (call.command === "pgrep") {
      return running ? success("123\n") : failure();
    }

    if (call.command === "open") {
      running = true;
      return success();
    }

    if (call.command === "osascript") {
      return success();
    }

    return success();
  };
};

const startupOptions = (run: (command: string, args: string[]) => ShellResult) => ({
  activationRetryIntervalMs: 1,
  activationRetryTimeoutMs: 1,
  bundleId: "com.mechanistry.timberborn",
  log: () => undefined,
  processName: "Timberborn",
  run,
  sleepMs: async () => undefined,
  sleepSyncMs: () => undefined,
  waitSeconds: 1,
});

const guardedStartupOptions = (
  run: (command: string, args: string[]) => ShellResult,
  guardDir: string,
  nowMs: () => number,
): TimberbornStartupOptions => ({
  ...startupOptions(run),
  activationPolicy: "best-effort",
  launchIntentGuard: {
    dir: guardDir,
    failureCooldownMs: 120_000,
    nowMs,
    ttlMs: 60_000,
  },
  mode: "launch",
});

const withTempDir = async <T>(callback: (dir: string) => Promise<T> | T): Promise<T> => {
  const dir = mkdtempSync(join(tmpdir(), "wildfire-startup-"));
  try {
    return await callback(dir);
  } finally {
    rmSync(dir, { force: true, recursive: true });
  }
};

describe("timberborn startup contract", () => {
  test("launch mode attaches to an already-running process without calling open", async () => {
    const runner = createRunner(runningTimberbornRunner());

    const result = await launchOrAttachTimberborn({
      ...startupOptions(runner.run),
      activationPolicy: "required",
      mode: "launch",
    });

    expect(result).toEqual({
      activationStatus: "activated",
      launched: false,
      wasRunningBeforeLaunch: true,
    });
    expect(commandCalls(runner.calls, "open")).toHaveLength(0);
  });

  test("launch mode opens the app exactly once when Timberborn is absent", async () => {
    const runner = createRunner(coldLaunchRunner());

    const result = await launchOrAttachTimberborn({
      ...startupOptions(runner.run),
      activationPolicy: "required",
      mode: "launch",
    });

    expect(result.launched).toBe(true);
    expect(result.wasRunningBeforeLaunch).toBe(false);
    expect(commandCalls(runner.calls, "open")).toEqual([{ args: ["-a", "Timberborn"], command: "open" }]);
    expect(commandCalls(runner.calls, "pgrep").length).toBeGreaterThanOrEqual(2);
  });

  test("attach mode never launches when Timberborn is already running", async () => {
    const runner = createRunner(runningTimberbornRunner());

    const result = await launchOrAttachTimberborn({
      ...startupOptions(runner.run),
      activationPolicy: "required",
      mode: "attach",
    });

    expect(result).toEqual({
      activationStatus: "activated",
      launched: false,
      wasRunningBeforeLaunch: true,
    });
    expect(commandCalls(runner.calls, "open")).toHaveLength(0);
  });

  test("launch mode records launch intent before opening Timberborn", async () => {
    await withTempDir(async (dir) => {
      const runner = createRunner([failure(), success(), success("123\n"), success()]);
      const logs: string[] = [];

      const result = await launchOrAttachTimberborn({
        ...startupOptions(runner.run),
        activationPolicy: "required",
        launchIntentGuard: {
          dir: join(dir, "timberborn-launch-intent"),
          nowMs: () => 1_000,
          ttlMs: 60_000,
        },
        log: (message) => logs.push(message),
        mode: "launch",
      });

      expect(result.launched).toBe(true);
      expect(runner.calls.map((call) => call.command)).toEqual(["pgrep", "open", "pgrep", "osascript"]);
      expect(logs).toContain(
        `launch_intent_recorded action=open_app path=${join(dir, "timberborn-launch-intent")} ttl_ms=60000`,
      );
      expect(logs).toContain("startup_intent mode=launch timberborn_running=false action=open_app");
    });
  });

  test("launch mode refuses a recent duplicate launch intent before calling open", async () => {
    await withTempDir(async (dir) => {
      const guardDir = join(dir, "timberborn-launch-intent");
      const runner = createRunner([failure()]);
      const logs: string[] = [];

      mkdirSync(guardDir);
      writeFileSync(
        join(guardDir, "intent.json"),
        `${JSON.stringify({
          bundleId: "com.mechanistry.timberborn",
          createdAt: new Date(1_000).toISOString(),
          pid: 123,
          processName: "Timberborn",
          timestampMs: 1_000,
          ttlMs: 60_000,
        })}\n`,
      );

      await expect(
        launchOrAttachTimberborn({
          ...startupOptions(runner.run),
          activationPolicy: "required",
          launchIntentGuard: {
            dir: guardDir,
            nowMs: () => 2_000,
            ttlMs: 60_000,
          },
          log: (message) => logs.push(message),
          mode: "launch",
        }),
      ).rejects.toThrow("Refusing duplicate Timberborn launch intent");

      expect(runner.calls.map((call) => call.command)).toEqual(["pgrep"]);
      expect(runner.calls.some((call) => call.command === "open")).toBe(false);
      expect(logs).toContain(
        `launch_intent_duplicate_refused action=open_app path=${guardDir} age_ms=1000 ttl_ms=60000 existing_pid=123`,
      );
    });
  });

  test("launch mode preserves a longer recorded launch intent TTL", async () => {
    await withTempDir(async (dir) => {
      const guardDir = join(dir, "timberborn-launch-intent");
      const runner = createRunner([failure()]);
      const logs: string[] = [];

      mkdirSync(guardDir);
      writeFileSync(
        join(guardDir, "intent.json"),
        `${JSON.stringify({
          bundleId: "com.mechanistry.timberborn",
          createdAt: new Date(1_000).toISOString(),
          pid: 123,
          processName: "Timberborn",
          timestampMs: 1_000,
          ttlMs: 240_000,
        })}\n`,
      );

      await expect(
        launchOrAttachTimberborn({
          ...startupOptions(runner.run),
          activationPolicy: "required",
          launchIntentGuard: {
            dir: guardDir,
            nowMs: () => 92_000,
            ttlMs: 90_000,
          },
          log: (message) => logs.push(message),
          mode: "launch",
        }),
      ).rejects.toThrow("Refusing duplicate Timberborn launch intent");

      expect(runner.calls.map((call) => call.command)).toEqual(["pgrep"]);
      expect(runner.calls.some((call) => call.command === "open")).toBe(false);
      expect(logs).toContain(
        `launch_intent_duplicate_refused action=open_app path=${guardDir} age_ms=91000 ttl_ms=240000 existing_pid=123`,
      );
    });
  });

  test("launch mode treats missing guard metadata as an in-progress launch intent", async () => {
    await withTempDir(async (dir) => {
      const guardDir = join(dir, "timberborn-launch-intent");
      const runner = createRunner([failure()]);

      mkdirSync(guardDir);

      await expect(
        launchOrAttachTimberborn({
          ...startupOptions(runner.run),
          activationPolicy: "required",
          launchIntentGuard: {
            dir: guardDir,
            nowMs: () => Date.now(),
            ttlMs: 60_000,
          },
          mode: "launch",
        }),
      ).rejects.toThrow("Refusing duplicate Timberborn launch intent");

      expect(runner.calls.map((call) => call.command)).toEqual(["pgrep"]);
      expect(runner.calls.some((call) => call.command === "open")).toBe(false);
    });
  });

  test("launch mode replaces stale launch intent before opening Timberborn", async () => {
    await withTempDir(async (dir) => {
      const guardDir = join(dir, "timberborn-launch-intent");
      const runner = createRunner([failure(), success(), success("123\n"), success()]);
      const logs: string[] = [];

      mkdirSync(guardDir);
      writeFileSync(
        join(guardDir, "intent.json"),
        `${JSON.stringify({
          bundleId: "com.mechanistry.timberborn",
          createdAt: new Date(1_000).toISOString(),
          pid: 123,
          processName: "Timberborn",
          timestampMs: 1_000,
          ttlMs: 60_000,
        })}\n`,
      );

      const result = await launchOrAttachTimberborn({
        ...startupOptions(runner.run),
        activationPolicy: "required",
        launchIntentGuard: {
          dir: guardDir,
          nowMs: () => 62_000,
          ttlMs: 60_000,
        },
        log: (message) => logs.push(message),
        mode: "launch",
      });

      expect(result.launched).toBe(true);
      expect(runner.calls.map((call) => call.command)).toEqual(["pgrep", "open", "pgrep", "osascript"]);
      expect(logs).toContain(
        `launch_intent_recorded action=open_app path=${guardDir} ttl_ms=60000 replaced_stale=true previous_age_ms=61000`,
      );
    });
  });

  test("activation retries never call open when the process is already running", async () => {
    const runner = createRunner(runningTimberbornRunner(failure("not frontmost")));

    const result = await launchOrAttachTimberborn({
      ...startupOptions(runner.run),
      activationPolicy: "best-effort",
      mode: "launch",
    });

    expect(result.activationStatus).toBe("failed");
    expect(result.launched).toBe(false);
    expect(commandCalls(runner.calls, "osascript").length).toBeGreaterThan(1);
    expect(commandCalls(runner.calls, "open")).toHaveLength(0);
  });

  test("Steam-frontmost activation failure records a launch cooldown", async () => {
    await withTempDir(async (dir) => {
      const guardDir = join(dir, "timberborn-launch-intent");
      let nowMs = 1_000;
      const runner = createRunner([failure(), success(), success("123\n"), success(), success()]);
      const logs: string[] = [];

      const result = await launchOrAttachTimberborn({
        ...guardedStartupOptions(runner.run, guardDir, () => nowMs),
        getFrontmostBundleId: () => "com.valvesoftware.steam",
        log: (message) => logs.push(message),
      });

      expect(result.activationStatus).toBe("failed");
      expect(logs).toContain(
        `launch_intent_failure_recorded failure_kind=steam_frontmost path=${guardDir} cooldown_ms=120000`,
      );

      nowMs = 31_000;
      const retry = createRunner([failure()]);
      await expect(
        launchOrAttachTimberborn({
          ...guardedStartupOptions(retry.run, guardDir, () => nowMs),
          log: (message) => logs.push(message),
        }),
      ).rejects.toThrow("Refusing duplicate Timberborn launch intent");

      expect(retry.calls.map((call) => call.command)).toEqual(["pgrep"]);
      expect(logs).toContain(
        `launch_intent_duplicate_refused action=open_app path=${guardDir} age_ms=30000 ttl_ms=120000 existing_pid=${process.pid} existing_failure_kind=steam_frontmost`,
      );
    });
  });

  test("process-exit failure extends retry cadence from the failure time", async () => {
    await withTempDir(async (dir) => {
      const guardDir = join(dir, "timberborn-launch-intent");
      let nowMs = 62_000;
      const logs: string[] = [];
      const runner = createRunner([failure()]);
      const options = {
        ...guardedStartupOptions(runner.run, guardDir, () => nowMs),
        log: (message: string) => logs.push(message),
      };

      mkdirSync(guardDir);
      writeFileSync(
        join(guardDir, "intent.json"),
        `${JSON.stringify({
          bundleId: "com.mechanistry.timberborn",
          createdAt: new Date(1_000).toISOString(),
          pid: 123,
          processName: "Timberborn",
          status: "open_app",
          timestampMs: 1_000,
          ttlMs: 60_000,
        })}\n`,
      );

      recordLaunchIntentFailure(options, "timberborn_process_exit", "process exited before startup completed");
      expect(logs).toContain(
        `launch_intent_failure_recorded failure_kind=timberborn_process_exit path=${guardDir} cooldown_ms=120000`,
      );

      nowMs = 121_000;
      await expect(launchOrAttachTimberborn(options)).rejects.toThrow("Refusing duplicate Timberborn launch intent");

      expect(runner.calls.map((call) => call.command)).toEqual(["pgrep"]);
      expect(logs).toContain(
        `launch_intent_duplicate_refused action=open_app path=${guardDir} age_ms=59000 ttl_ms=120000 existing_pid=${process.pid} existing_failure_kind=timberborn_process_exit`,
      );
    });
  });

  test("launch timeout records a distinct timeout failure", async () => {
    await withTempDir(async (dir) => {
      const guardDir = join(dir, "timberborn-launch-intent");
      const runner = createRunner([failure(), success(), failure()]);
      const logs: string[] = [];

      await expect(
        launchOrAttachTimberborn({
          ...guardedStartupOptions(runner.run, guardDir, () => 1_000),
          log: (message) => logs.push(message),
          waitSeconds: 0,
        }),
      ).rejects.toThrow("Timed out waiting for Timberborn to start");

      expect(logs).toContain(
        "startup_failure failure_kind=timeout process_running=false launched=true was_running_before_launch=false",
      );
      expect(logs).toContain(
        `launch_intent_failure_recorded failure_kind=timeout path=${guardDir} cooldown_ms=120000`,
      );
    });
  });

  test("attach mode fails clearly when Timberborn is absent", async () => {
    const runner = createRunner((call) => (call.command === "pgrep" ? failure() : success()));

    await expect(
      launchOrAttachTimberborn({
        ...startupOptions(runner.run),
        activationPolicy: "required",
        mode: "attach",
      }),
    ).rejects.toThrow("Timberborn is not running. Start it first or pass --launch.");
    expect(commandCalls(runner.calls, "open")).toHaveLength(0);
    expect(commandCalls(runner.calls, "osascript")).toHaveLength(0);
  });
});

describe("timberborn startup process-exit diagnostics", () => {
  test("writes a concise process-exit bundle with changed error reports and bounded Player.log evidence", async () => {
    await withTempDir(async (dir) => {
      const artifactDir = join(dir, "artifacts");
      const errorReportDir = join(dir, "Error reports");
      const guardDir = join(dir, "timberborn-launch-intent");
      const playerLogPath = join(dir, "Player.log");
      const beforeReport = join(errorReportDir, "error-report-2026-05-31-01h00m00s.zip");
      const afterReport = join(errorReportDir, "error-report-2026-05-31-01h05m00s.zip");
      const runner = createRunner((call) => {
        if (call.command === "/bin/ps") {
          return success(
            [
              "  11 rg Timberborn",
              "  12 zsh /bin/ps -axo pid=,comm=,args= | rg Timberborn",
              " 123 Timberborn /Applications/Timberborn.app/Contents/MacOS/Timberborn",
            ].join("\n"),
          );
        }

        return success();
      });

      mkdirSync(errorReportDir, { recursive: true });
      mkdirSync(guardDir, { recursive: true });
      writeFileSync(beforeReport, "old report");
      utimesSync(beforeReport, new Date("2026-05-31T01:00:00Z"), new Date("2026-05-31T01:00:00Z"));
      const beforeErrorReports = errorReportInventory(errorReportDir);
      writeFileSync(afterReport, "new report");
      utimesSync(afterReport, new Date("2026-05-31T01:05:00Z"), new Date("2026-05-31T01:05:00Z"));
      writeFileSync(
        join(guardDir, "intent.json"),
        `${JSON.stringify({
          bundleId: "com.mechanistry.timberborn",
          createdAt: "2026-05-31T01:04:00.000Z",
          pid: 999,
          processName: "Timberborn",
          status: "open_app",
          timestampMs: 1_000,
          ttlMs: 240_000,
        })}\n`,
      );
      writeFileSync(
        playerLogPath,
        [
          "ordinary startup line",
          "Starting game version 1.0.13.1-b769e88-xsm",
          "Input System module state changed to: ShutdownInProgress",
          "Thread 9 may have been prematurely finalized",
          "Input System module state changed to: Shutdown",
        ].join("\n"),
      );

      const bundle = writeStartupExitDiagnosticsBundle({
        afterErrorReports: errorReportInventory(errorReportDir),
        artifactDir,
        beforeErrorReports,
        errorReportDir,
        failureKind: "timberborn_process_exit",
        frontmostBundleId: "com.valvesoftware.steam",
        launchIntentGuardDir: guardDir,
        message: "Expected Timberborn foreground, got frontmost_bundle_id=com.valvesoftware.steam.",
        playerLogPath,
        processName: "Timberborn",
        processRunning: false,
        run: runner.run,
      });

      expect(bundle).not.toBeNull();
      expect(bundle?.copiedErrorReports).toEqual([
        join(artifactDir, "startup-process-exit-diagnostics", "error-reports", "error-report-2026-05-31-01h05m00s.zip"),
      ]);
      expect(existsSync(bundle?.summaryPath ?? "")).toBe(true);
      const summary = readFileSync(bundle?.summaryPath ?? "", "utf8");
      expect(summary).toContain("wildfire_startup_exit_diagnostics=present");
      expect(summary).toContain("failure_kind=timberborn_process_exit");
      expect(summary).toContain("frontmost_bundle_id=com.valvesoftware.steam");
      expect(summary).toContain("new_or_changed_error_reports=1");
      expect(summary).toContain(`player_log=${bundle?.playerLogCopyPath}`);
      const launchIntent = JSON.parse(readFileSync(bundle?.launchIntentCopyPath ?? "", "utf8")) as {
        failureKind?: string;
        status?: string;
      };
      expect(launchIntent.status).toBe("open_app");
      expect(launchIntent.failureKind).toBeUndefined();

      const processSnapshot = readFileSync(bundle?.processSnapshotPath ?? "", "utf8");
      expect(processSnapshot).toContain("match_strategy=exact_comm_or_executable_basename");
      expect(processSnapshot).toContain("123 Timberborn /Applications/Timberborn.app/Contents/MacOS/Timberborn");
      expect(processSnapshot).not.toContain("rg Timberborn");
      expect(runner.calls).toEqual([{ args: ["-axo", "pid=,comm=,args="], command: "/bin/ps" }]);

      const playerLogScan = readFileSync(bundle?.playerLogScanPath ?? "", "utf8");
      expect(playerLogScan).toContain("Starting game version 1.0.13.1-b769e88-xsm");
      expect(playerLogScan).toContain("ShutdownInProgress");
      expect(playerLogScan).toContain("prematurely finalized");
    });
  });

  test("skips the forensic bundle for non process-exit failures", async () => {
    await withTempDir(async (dir) => {
      const bundle = writeStartupExitDiagnosticsBundle({
        afterErrorReports: [],
        artifactDir: join(dir, "artifacts"),
        beforeErrorReports: [],
        errorReportDir: join(dir, "Error reports"),
        failureKind: "steam_frontmost",
        frontmostBundleId: "com.valvesoftware.steam",
        launchIntentGuardDir: join(dir, "timberborn-launch-intent"),
        message: "frontmost_bundle_id=com.valvesoftware.steam",
        playerLogPath: join(dir, "Player.log"),
        processName: "Timberborn",
        processRunning: true,
        run: () => success(),
      });

      expect(bundle).toBeNull();
      expect(existsSync(join(dir, "artifacts"))).toBe(false);
    });
  });
});
