import { describe, expect, test } from "bun:test";
import { launchOrAttachTimberborn, type ShellResult } from "../scripts/lib/timberborn-startup.ts";

type Call = {
  args: string[];
  command: string;
};

type ShellHandler = (call: Call) => ShellResult;

const success = (stdout = ""): ShellResult => ({ exitCode: 0, stderr: "", stdout });
const failure = (stderr = ""): ShellResult => ({ exitCode: 1, stderr, stdout: "" });

const createRunner = (handler: ShellHandler) => {
  const calls: Call[] = [];
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
    expect(commandCalls(runner.calls, "open")).toEqual([{ args: ["-b", "com.mechanistry.timberborn"], command: "open" }]);
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
