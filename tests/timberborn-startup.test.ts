import { describe, expect, test } from "bun:test";
import { launchOrAttachTimberborn, type ShellResult } from "../scripts/lib/timberborn-startup.ts";

type Call = {
  args: string[];
  command: string;
};

const success = (stdout = ""): ShellResult => ({ exitCode: 0, stderr: "", stdout });
const failure = (stderr = ""): ShellResult => ({ exitCode: 1, stderr, stdout: "" });

const createRunner = (results: ShellResult[]) => {
  const calls: Call[] = [];
  let index = 0;
  return {
    calls,
    run: (command: string, args: string[]): ShellResult => {
      calls.push({ args, command });
      const result = results[index] ?? failure(`unexpected command: ${command} ${args.join(" ")}`);
      index += 1;
      return result;
    },
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
    const runner = createRunner([success("123\n"), success("123\n"), success()]);

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
    expect(runner.calls.map((call) => call.command)).toEqual(["pgrep", "pgrep", "osascript"]);
  });

  test("launch mode opens the app exactly once when Timberborn is absent", async () => {
    const runner = createRunner([failure(), success(), success("123\n"), success()]);

    const result = await launchOrAttachTimberborn({
      ...startupOptions(runner.run),
      activationPolicy: "required",
      mode: "launch",
    });

    expect(result.launched).toBe(true);
    expect(result.wasRunningBeforeLaunch).toBe(false);
    expect(runner.calls.map((call) => call.command)).toEqual(["pgrep", "open", "pgrep", "osascript"]);
    expect(runner.calls.filter((call) => call.command === "open")).toHaveLength(1);
  });

  test("activation retries never call open when the process is already running", async () => {
    const runner = createRunner([success("123\n"), success("123\n"), failure("not frontmost"), failure("not frontmost")]);

    const result = await launchOrAttachTimberborn({
      ...startupOptions(runner.run),
      activationPolicy: "best-effort",
      mode: "launch",
    });

    expect(result.activationStatus).toBe("failed");
    expect(result.launched).toBe(false);
    expect(runner.calls.map((call) => call.command)).toEqual(["pgrep", "pgrep", "osascript", "osascript"]);
    expect(runner.calls.some((call) => call.command === "open")).toBe(false);
  });

  test("attach mode fails clearly when Timberborn is absent", async () => {
    const runner = createRunner([failure()]);

    await expect(
      launchOrAttachTimberborn({
        ...startupOptions(runner.run),
        activationPolicy: "required",
        mode: "attach",
      }),
    ).rejects.toThrow("Timberborn is not running. Start it first or pass --launch.");
    expect(runner.calls.map((call) => call.command)).toEqual(["pgrep"]);
  });
});
