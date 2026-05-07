#!/usr/bin/env bun

import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { inflateRawSync, inflateSync } from "node:zlib";
import { fileURLToPath } from "url";

type Mode = "attach" | "launch";
type ScreenKind = "experimental-mode" | "loaded-save" | "main-menu" | "startup-mods" | "unknown";
type BlockingOverlay = "mac-system-alert" | null;

type Options = {
  artifactsDir: string;
  classifyScreenshotPath: string | null;
  commandDir: string;
  dryRun: boolean;
  expectedResolution: string;
  forceLock: boolean;
  help: boolean;
  lockTimeoutSeconds: number;
  mode: Mode;
  playerLogPath: string;
  postStatus: boolean;
  skipLatestSavePreflight: boolean;
  skipResolutionCheck: boolean;
  waitSeconds: number;
};

type CommandResult = {
  text: string;
  tickCount: number | null;
};

type ShellResult = {
  exitCode: number;
  stderr: string;
  stdout: string;
};

type SavePreflight = {
  cellCount: number | null;
  height: number | null;
  path: string;
  width: number | null;
};

type CoordinateTarget = {
  id: string;
  x: number;
  y: number;
};

type PngImage = {
  bytesPerPixel: number;
  data: Uint8Array;
  height: number;
  width: number;
};

type Pixel = {
  b: number;
  g: number;
  r: number;
};

type Screenshot = {
  blockingOverlay: BlockingOverlay;
  image: PngImage;
  path: string;
  screen: ScreenKind;
};

type FastFrameSample = {
  blockingOverlay: BlockingOverlay | "capture-error";
  name: string;
  path: string;
  screen: ScreenKind | "capture-error";
};

type FastFrameSampler = {
  samples: FastFrameSample[];
  stop: () => Promise<FastFrameSample[]>;
};

const home = process.env.HOME ?? "";
const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const bundleId = "com.mechanistry.timberborn";
const processName = "Timberborn";
const qaRoot = join(home, "Library", "Application Support", "Mechanistry", "Timberborn", "WildfireQA");
const lockDir = join(home, "Library", "Application Support", "Timberborn", "WildfireQA", "locks", "build-deploy.lock");
const lockInfoPath = join(lockDir, "lock.json");
const playerLogDefault = join(home, "Library", "Logs", "Mechanistry", "Timberborn", "Player.log");
const coordinateGuidePath = join(repoRoot, "docs", "timberborn-menu-coordinate-guide.md");
const inboxFileName = "command-inbox.txt";
const outboxFileName = "command-outbox.txt";
const fastFrameIntervalMs = 1500;
const activationRetryIntervalMs = 500;
const activationRetryTimeoutMs = 20000;

const targets = {
  experimentalStart: { id: "experimental_mode.start", x: 960, y: 716 },
  hudSpeed1: { id: "hud.speed1", x: 1734, y: 20 },
  mainContinue: { id: "main.continue", x: 960, y: 324 },
  startupModsOk: { id: "startup_mods.ok", x: 960, y: 830 },
} satisfies Record<string, CoordinateTarget>;

const usage = `Usage:
  bun scripts/load-latest-save-and-unpause.ts [options]

Modes:
  --launch                  Launch Timberborn by bundle id if needed. Default.
  --attach                  Require Timberborn to already be running.

Options:
  --wait <seconds>          Seconds to wait for each expected UI transition. Default: 180.
  --player-log <path>       Player.log path. Default: ~/Library/Logs/Mechanistry/Timberborn/Player.log.
  --command-dir <path>      Wildfire command bridge directory. Default: ~/Library/Application Support/Mechanistry/Timberborn/WildfireQA.
  --artifacts-dir <path>    Evidence root. Default: ~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup.
  --classify-screenshot <path>
                            Classify an existing screenshot, then exit without locking, launching, or clicking.
  --expected-resolution <WxH> Display resolution required by the coordinate guide. Default: 1920x1080.
  --skip-resolution-check   Skip display validation. Use only for local script checks, not live UI automation.
  --skip-post-status        Do not require post-unpause read-only status evidence.
  --skip-latest-save-preflight
                            Do not inspect the newest .timber save before clicking Continue.
  --lock-timeout <seconds>  Seconds to wait for the shared deploy/QA lock. Default: 0.
  --force-lock              Remove an existing shared lock before acquiring it.
  --dry-run                 Print the plan and validate local preconditions without launching or clicking.
  --help                    Show this help.

Examples:
  bun scripts/load-latest-save-and-unpause.ts --dry-run
  bun scripts/load-latest-save-and-unpause.ts --classify-screenshot ./screen.png
  bun scripts/load-latest-save-and-unpause.ts --launch --wait=240
  bun scripts/load-latest-save-and-unpause.ts --attach --skip-post-status

Default startup behavior:
  --launch uses the signal-driven cold-start path: sample frames, press Enter when startup confirmation gates are positively identified, click only the documented main.continue coordinate, then verify loaded-save HUD/status/log proof.
  --attach uses the conservative classifier path for already-running Timberborn sessions.
`;

const log = (message: string): void => {
  console.log(`[wildfire-latest-save] ${message}`);
};

const fail = (message: string): never => {
  throw new Error(`[wildfire-latest-save] ${message}`);
};

const requireValue = (args: string[], index: number, flag: string): string => {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    fail(`${flag} requires a value.`);
  }

  return value;
};

const normalizeResolution = (value: string): string => {
  const match = value.match(/^(\d+)\s*x\s*(\d+)$/iu);
  if (!match) {
    return fail(`Invalid resolution: ${value}. Expected WxH, for example 1920x1080.`);
  }

  return `${match[1]}x${match[2]}`;
};

const parseSeconds = (value: string, flag: string): number => {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    fail(`Invalid ${flag} value: ${value}`);
  }

  return parsed;
};

const parseArgs = (args: string[]): Options => {
  const options: Options = {
    artifactsDir: join(qaRoot, "latest-save-startup"),
    classifyScreenshotPath: null,
    commandDir: qaRoot,
    dryRun: false,
    expectedResolution: "1920x1080",
    forceLock: false,
    help: false,
    lockTimeoutSeconds: 0,
    mode: "launch",
    playerLogPath: playerLogDefault,
    postStatus: true,
    skipLatestSavePreflight: false,
    skipResolutionCheck: false,
    waitSeconds: 180,
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
    } else if (arg === "--artifacts-dir") {
      options.artifactsDir = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--artifacts-dir=")) {
      options.artifactsDir = resolve(arg.slice("--artifacts-dir=".length));
    } else if (arg === "--classify-screenshot") {
      options.classifyScreenshotPath = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--classify-screenshot=")) {
      options.classifyScreenshotPath = resolve(arg.slice("--classify-screenshot=".length));
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
    } else if (arg === "--skip-post-status") {
      options.postStatus = false;
    } else if (arg === "--skip-latest-save-preflight") {
      options.skipLatestSavePreflight = true;
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

const isTimberbornRunning = (): boolean => run("pgrep", ["-x", processName]).exitCode === 0;

const commandFailureText = (result: ShellResult): string => result.stderr.trim() || result.stdout.trim() || "(no stderr/stdout)";

const formatError = (error: unknown): string => error instanceof Error ? error.message : String(error);

const compactLogToken = (value: string): string => value.replaceAll(/\s+/gu, "_").replaceAll('"', "'");

const defaultTimberbornGridDepth = 23;
const maxContinueCellCount = 500_000;
const timberbornExperimentalSavesDir = join(home, "Documents", "Timberborn", "ExperimentalSaves");

const viewOf = (buffer: Buffer): DataView => new DataView(buffer.buffer, buffer.byteOffset, buffer.byteLength);

const readUtf8 = (buffer: Buffer, offset: number, length: number): string => buffer.subarray(offset, offset + length).toString("utf8");

const findZipEndOfCentralDirectory = (bytes: Buffer): number | null => {
  let offset = bytes.length - 22;
  while (offset >= 0) {
    if (viewOf(bytes).getUint32(offset, true) === 0x06054b50) {
      return offset;
    }
    offset -= 1;
  }

  return null;
};

const readZipEntry = (archivePath: string, entryName: string): Buffer | null => {
  const bytes = readFileSync(archivePath);
  const view = viewOf(bytes);
  const eocdOffset = findZipEndOfCentralDirectory(bytes);
  if (eocdOffset === null) {
    return null;
  }

  const entryCount = view.getUint16(eocdOffset + 10, true);
  const centralDirectoryOffset = view.getUint32(eocdOffset + 16, true);
  const entries = Array.from({ length: entryCount }).reduce<{ data: Buffer | null; offset: number }>(
    (state) => {
      if (state.data !== null) {
        return state;
      }
      if (view.getUint32(state.offset, true) !== 0x02014b50) {
        return state;
      }

      const compressionMethod = view.getUint16(state.offset + 10, true);
      const compressedSize = view.getUint32(state.offset + 20, true);
      const fileNameLength = view.getUint16(state.offset + 28, true);
      const extraLength = view.getUint16(state.offset + 30, true);
      const commentLength = view.getUint16(state.offset + 32, true);
      const localHeaderOffset = view.getUint32(state.offset + 42, true);
      const name = readUtf8(bytes, state.offset + 46, fileNameLength);
      const nextOffset = state.offset + 46 + fileNameLength + extraLength + commentLength;
      if (name !== entryName || view.getUint32(localHeaderOffset, true) !== 0x04034b50) {
        return { data: null, offset: nextOffset };
      }

      const localNameLength = view.getUint16(localHeaderOffset + 26, true);
      const localExtraLength = view.getUint16(localHeaderOffset + 28, true);
      const dataOffset = localHeaderOffset + 30 + localNameLength + localExtraLength;
      const compressedData = bytes.subarray(dataOffset, dataOffset + compressedSize);
      const data =
        compressionMethod === 0
          ? Buffer.from(compressedData)
          : compressionMethod === 8
            ? inflateRawSync(compressedData)
            : null;

      return { data, offset: nextOffset };
    },
    { data: null, offset: centralDirectoryOffset },
  );

  return entries.data;
};

const asRecord = (value: unknown): Record<string, unknown> | null =>
  value !== null && typeof value === "object" && !Array.isArray(value) ? value as Record<string, unknown> : null;

const readNestedNumber = (value: unknown, path: string[]): number | null => {
  const found = path.reduce<unknown>((current, key) => asRecord(current)?.[key], value);
  return typeof found === "number" ? found : null;
};

const findTimberbornSaves = (root: string, depth = 0): string[] => {
  if (!existsSync(root)) {
    return [];
  }

  return readdirSync(root, { withFileTypes: true }).flatMap((entry) => {
    const path = join(root, entry.name);
    if (entry.isDirectory()) {
      return depth < 1 ? findTimberbornSaves(path, depth + 1) : [];
    }

    return entry.isFile() && entry.name.endsWith(".timber") ? [path] : [];
  });
};

const latestTimberbornSavePath = (): string | null =>
  findTimberbornSaves(timberbornExperimentalSavesDir)
    .map((path) => ({ modified: statSync(path).mtimeMs, path }))
    .sort((left, right) => right.modified - left.modified)
    .at(0)?.path ?? null;

const inspectSaveForPreflight = (path: string): SavePreflight => {
  const worldEntry = readZipEntry(path, "world.json");
  if (worldEntry === null) {
    return { cellCount: null, height: null, path, width: null };
  }

  const world = JSON.parse(worldEntry.toString("utf8")) as unknown;
  const width = readNestedNumber(world, ["Singletons", "MapSize", "Size", "X"]);
  const height = readNestedNumber(world, ["Singletons", "MapSize", "Size", "Y"]);
  const cellCount = width !== null && height !== null ? width * height * defaultTimberbornGridDepth : null;
  return { cellCount, height, path, width };
};

const assertLatestSaveCanBeLoadedByContinue = (options: Options): void => {
  if (options.skipLatestSavePreflight) {
    log("latest_save_preflight=skipped");
    return;
  }

  const path = latestTimberbornSavePath();
  if (path === null) {
    log(`latest_save_preflight=not_found root=${timberbornExperimentalSavesDir}`);
    return;
  }

  const preflight = inspectSaveForPreflight(path);
  log(
    `latest_save_preflight path=${preflight.path} width=${preflight.width ?? "unknown"} height=${preflight.height ?? "unknown"} depth=${defaultTimberbornGridDepth} cell_count=${preflight.cellCount ?? "unknown"} limit=${maxContinueCellCount}`,
  );
  if (preflight.cellCount !== null && preflight.cellCount > maxContinueCellCount) {
    fail(
      `Refusing to click Continue because the newest save is too large for the live QA harness: ${preflight.path} has ${preflight.cellCount} cells at ${preflight.width}x${preflight.height}x${defaultTimberbornGridDepth}. Move that save out of the newest slot or pass --skip-latest-save-preflight for an intentional manual stress run.`,
    );
  }
};

const getFrontmostBundleId = (): string | null => {
  const result = run("osascript", [
    "-e",
    'tell application "System Events" to get bundle identifier of first application process whose frontmost is true',
  ]);

  return result.exitCode === 0 ? result.stdout.trim() || null : null;
};

const assertTimberbornForeground = (label: string): void => {
  const frontmostBundleId = getFrontmostBundleId() ?? "unknown";
  if (frontmostBundleId !== bundleId) {
    fail(`Expected Timberborn foreground for ${label}, got frontmost_bundle_id=${frontmostBundleId}.`);
  }
};

const isTransientForegroundFailure = (message: string): boolean =>
  message.includes("frontmost_bundle_id=") ||
  message.includes(`Could not activate Timberborn bundle ${bundleId}`);

const assertToolExists = (tool: string): void => {
  const result = run("/usr/bin/which", [tool]);
  if (result.exitCode !== 0) {
    fail(`Required tool is missing: ${tool}`);
  }
};

const escapeRegExp = (value: string): string => value.replaceAll(/[.*+?^${}()|[\]\\]/gu, "\\$&");

const assertCoordinateGuideTargets = (): void => {
  const guide = readFileSync(coordinateGuidePath, "utf8");
  const missingTargets = Object.values(targets)
    .map((target) => {
      const pattern = new RegExp(
        `\\|\\s+\`${escapeRegExp(target.id)}\`\\s+\\|\\s+${target.x}\\s+\\|\\s+${target.y}\\s+\\|`,
        "u",
      );
      return pattern.test(guide) ? null : `${target.id}@${target.x},${target.y}`;
    })
    .filter((target): target is string => target !== null);

  if (missingTargets.length > 0) {
    fail(`Coordinate guide is missing expected targets: ${missingTargets.join(", ")}. Checked ${coordinateGuidePath}`);
  }

  log(`coordinate_guide_ok path=${coordinateGuidePath}`);
};

const assertResolution = (expectedResolution: string): void => {
  const result = run("system_profiler", ["SPDisplaysDataType"]);
  if (result.exitCode !== 0) {
    fail(`Could not inspect display resolution: ${commandFailureText(result)}`);
  }

  const normalizedOutput = result.stdout.replaceAll(/\s+/gu, " ");
  const expectedForSystemProfiler = expectedResolution.replace("x", " x ");
  if (!normalizedOutput.includes(`Resolution: ${expectedForSystemProfiler}`)) {
    fail(`Expected display resolution ${expectedResolution}, but system_profiler did not report it.`);
  }

  log(`resolution_ok expected=${expectedResolution}`);
};

const readLock = (): string => {
  try {
    return readFileSync(lockInfoPath, "utf8");
  } catch {
    return "(lock metadata unavailable)";
  }
};

const acquireLock = (options: Options): (() => void) => {
  mkdirSync(dirname(lockDir), { recursive: true });

  if (options.forceLock && existsSync(lockDir)) {
    log(`force_removing_lock path=${lockDir}`);
    rmSync(lockDir, { force: true, recursive: true });
  }

  const startedAt = Date.now();
  while (true) {
    try {
      mkdirSync(lockDir);
      writeFileSync(
        lockInfoPath,
        `${JSON.stringify(
          {
            command: "load-latest-save-and-unpause",
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
        rmSync(lockDir, { force: true, recursive: true });
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

const activateTimberborn = (label = "activate", timeoutMs = activationRetryTimeoutMs): void => {
  const startedAt = Date.now();
  let attempts = 0;
  let lastFailure = "(activation was not attempted)";
  let lastFrontmostBundleId = getFrontmostBundleId() ?? "unknown";

  while (Date.now() - startedAt <= timeoutMs) {
    attempts += 1;
    const result = run("osascript", ["-e", `tell application id "${bundleId}" to activate`]);
    if (result.exitCode === 0) {
      lastFrontmostBundleId = getFrontmostBundleId() ?? "unknown";
      if (lastFrontmostBundleId === bundleId) {
        if (attempts > 1) {
          log(`activation_ok label=${label} attempts=${attempts}`);
        }
        return;
      }

      lastFailure = `frontmost_bundle_id=${lastFrontmostBundleId}`;
    } else {
      lastFailure = commandFailureText(result);
    }

    Bun.sleepSync(activationRetryIntervalMs);
  }

  fail(
    `Could not activate Timberborn bundle ${bundleId} for ${label} after ${attempts} attempts over ${Date.now() - startedAt}ms: ${lastFailure}`,
  );
};

const tryActivateTimberborn = (label = "activate", timeoutMs = activationRetryTimeoutMs): boolean => {
  try {
    activateTimberborn(label, timeoutMs);
    return true;
  } catch (error) {
    log(`activation_pending label=${label} error=${compactLogToken(formatError(error))}`);
    return false;
  }
};

const launchOrAttach = async (options: Options): Promise<void> => {
  if (options.mode === "attach") {
    if (!isTimberbornRunning()) {
      fail("Timberborn is not running. Start it first or pass --launch.");
    }
    activateTimberborn("attach");
    return;
  }

  if (!isTimberbornRunning()) {
    const openResult = run("open", ["-b", bundleId]);
    if (openResult.exitCode !== 0) {
      fail(`Could not launch Timberborn bundle ${bundleId}: ${commandFailureText(openResult)}`);
    }
  }

  const startedAt = Date.now();
  while (Date.now() - startedAt <= options.waitSeconds * 1000) {
    if (isTimberbornRunning()) {
      if (!tryActivateTimberborn("launch")) {
        log("launch_activation_deferred=true reason=timberborn_running_but_not_foreground");
      }
      log("timberborn_running=true");
      return;
    }

    await Bun.sleep(500);
  }

  fail(`Timed out waiting for ${processName} to start.`);
};

const createArtifactDir = (options: Options): string => {
  const stamp = new Date().toISOString().replaceAll(/[:.]/gu, "-");
  const artifactDir = join(options.artifactsDir, stamp);
  mkdirSync(artifactDir, { recursive: true });
  return artifactDir;
};

const concatUint8Arrays = (chunks: Uint8Array[]): Uint8Array => {
  const totalLength = chunks.reduce((sum, chunk) => sum + chunk.length, 0);
  const joined = new Uint8Array(totalLength);
  chunks.reduce((offset, chunk) => {
    joined.set(chunk, offset);
    return offset + chunk.length;
  }, 0);
  return joined;
};

const paeth = (left: number, up: number, upLeft: number): number => {
  const estimate = left + up - upLeft;
  const leftDistance = Math.abs(estimate - left);
  const upDistance = Math.abs(estimate - up);
  const upLeftDistance = Math.abs(estimate - upLeft);

  if (leftDistance <= upDistance && leftDistance <= upLeftDistance) {
    return left;
  }

  return upDistance <= upLeftDistance ? up : upLeft;
};

const parsePng = (path: string): PngImage => {
  const bytes = readFileSync(path);
  const expectedSignature = "89504e470d0a1a0a";
  if (bytes.subarray(0, 8).toString("hex") !== expectedSignature) {
    fail(`Screenshot is not a PNG: ${path}`);
  }

  let cursor = 8;
  let width = 0;
  let height = 0;
  let bitDepth = 0;
  let colorType = 0;
  let interlace = 0;
  const idatChunks: Uint8Array[] = [];

  while (cursor < bytes.length) {
    const length = bytes.readUInt32BE(cursor);
    const type = bytes.subarray(cursor + 4, cursor + 8).toString("ascii");
    const data = bytes.subarray(cursor + 8, cursor + 8 + length);
    cursor += 12 + length;

    if (type === "IHDR") {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8] ?? 0;
      colorType = data[9] ?? 0;
      interlace = data[12] ?? 0;
    } else if (type === "IDAT") {
      idatChunks.push(data);
    } else if (type === "IEND") {
      break;
    }
  }

  const bytesPerPixel = colorType === 6 ? 4 : colorType === 2 ? 3 : 0;
  if (width <= 0 || height <= 0 || bitDepth !== 8 || bytesPerPixel === 0 || interlace !== 0) {
    fail(`Unsupported PNG format for ${path}: width=${width} height=${height} bit_depth=${bitDepth} color_type=${colorType} interlace=${interlace}`);
  }

  const inflated = inflateSync(concatUint8Arrays(idatChunks));
  const stride = width * bytesPerPixel;
  const pixels = new Uint8Array(height * stride);
  let sourceOffset = 0;
  let row = 0;

  while (row < height) {
    const filter = inflated[sourceOffset] ?? 0;
    sourceOffset += 1;
    let column = 0;

    while (column < stride) {
      const raw = inflated[sourceOffset + column] ?? 0;
      const left = column >= bytesPerPixel ? pixels[row * stride + column - bytesPerPixel] ?? 0 : 0;
      const up = row > 0 ? pixels[(row - 1) * stride + column] ?? 0 : 0;
      const upLeft = row > 0 && column >= bytesPerPixel ? pixels[(row - 1) * stride + column - bytesPerPixel] ?? 0 : 0;
      const filtered =
        filter === 0
          ? raw
          : filter === 1
            ? raw + left
            : filter === 2
              ? raw + up
              : filter === 3
                ? raw + Math.floor((left + up) / 2)
                : filter === 4
                  ? raw + paeth(left, up, upLeft)
                  : fail(`Unsupported PNG filter ${filter} in ${path}`);
      pixels[row * stride + column] = filtered & 0xff;
      column += 1;
    }

    sourceOffset += stride;
    row += 1;
  }

  return {
    bytesPerPixel,
    data: pixels,
    height,
    width,
  };
};

const pixelAt = (image: PngImage, x: number, y: number): Pixel => {
  if (x < 0 || x >= image.width || y < 0 || y >= image.height) {
    fail(`Pixel outside screenshot bounds: ${x},${y} in ${image.width}x${image.height}`);
  }

  const offset = (y * image.width + x) * image.bytesPerPixel;
  return {
    b: image.data[offset + 2] ?? 0,
    g: image.data[offset + 1] ?? 0,
    r: image.data[offset] ?? 0,
  };
};

const near = (pixel: Pixel, expected: Pixel, tolerance: number): boolean =>
  Math.abs(pixel.r - expected.r) <= tolerance &&
  Math.abs(pixel.g - expected.g) <= tolerance &&
  Math.abs(pixel.b - expected.b) <= tolerance;

const isTealButton = (pixel: Pixel): boolean => pixel.r <= 45 && pixel.g >= 80 && pixel.g <= 150 && pixel.b >= 70 && pixel.b <= 125;
const isCreamButton = (pixel: Pixel): boolean =>
  pixel.r >= 160 && pixel.r <= 230 && pixel.g >= 155 && pixel.g <= 220 && pixel.b >= 120 && pixel.b <= 190;
const isMenuButtonFill = (pixel: Pixel): boolean => isTealButton(pixel) || isCreamButton(pixel);

const isStartupModsDialog = (sample: (x: number, y: number) => Pixel): boolean =>
  isTealButton(sample(960, 830)) &&
  near(sample(960, 184), { b: 215, g: 230, r: 234 }, 45) &&
  near(sample(960, 500), { b: 31, g: 35, r: 19 }, 45) &&
  near(sample(960, 650), { b: 31, g: 35, r: 19 }, 45) &&
  near(sample(960, 760), { b: 31, g: 35, r: 19 }, 45);

const isMacSystemAlertOverlay = (sample: (x: number, y: number) => Pixel): boolean =>
  near(sample(882, 250), { b: 70, g: 211, r: 249 }, 25) &&
  near(sample(870, 270), { b: 50, g: 179, r: 235 }, 35) &&
  near(sample(895, 270), { b: 50, g: 179, r: 235 }, 35) &&
  near(sample(882, 265), { b: 240, g: 241, r: 242 }, 25) &&
  near(sample(850, 214), { b: 44, g: 49, r: 39 }, 25) &&
  near(sample(853, 399), { b: 57, g: 61, r: 51 }, 35) &&
  near(sample(1070, 399), { b: 57, g: 60, r: 49 }, 35);

const hasCenterModal = (sample: (x: number, y: number) => Pixel): boolean =>
  near(sample(960, 500), { b: 31, g: 35, r: 19 }, 35) &&
  near(sample(960, 650), { b: 31, g: 35, r: 19 }, 35);

const isInGameModsPanel = (sample: (x: number, y: number) => Pixel): boolean =>
  hasCenterModal(sample) &&
  near(sample(960, 877), { b: 130, g: 147, r: 78 }, 45) &&
  near(sample(1292, 158), { b: 91, g: 130, r: 147 }, 55);

const isMainMenu = (sample: (x: number, y: number) => Pixel): boolean => {
  const documentedButtonRows = [324, 376, 428, 480, 532, 585, 637, 689, 741, 793];
  const matchingRows = documentedButtonRows
    .map((y) => isMenuButtonFill(sample(850, y)) && isMenuButtonFill(sample(1070, y)))
    .filter(Boolean).length;

  return (
    matchingRows >= 9 &&
    near(sample(960, 184), { b: 77, g: 116, r: 135 }, 70) &&
    near(sample(960, 850), { b: 30, g: 50, r: 20 }, 45) &&
    near(sample(1412, 817), { b: 33, g: 33, r: 9 }, 45)
  );
};

const classifyScreen = (image: PngImage): ScreenKind => {
  const sample = (x: number, y: number): Pixel => pixelAt(image, x, y);
  const startupMods = isStartupModsDialog(sample);

  const experimentalMode =
    isTealButton(sample(850, 716)) &&
    isTealButton(sample(1070, 716)) &&
    near(sample(960, 716), { b: 255, g: 255, r: 255 }, 35) &&
    near(sample(650, 360), { b: 36, g: 48, r: 24 }, 45) &&
    near(sample(1270, 360), { b: 35, g: 37, r: 19 }, 45);
  if (experimentalMode) {
    return "experimental-mode";
  }

  if (startupMods) {
    return "startup-mods";
  }

  if (isMainMenu(sample)) {
    return "main-menu";
  }

  if (isInGameModsPanel(sample)) {
    return "unknown";
  }

  const loadedSave =
    near(sample(278, 1044), { b: 125, g: 157, r: 167 }, 50) &&
    near(sample(1298, 20), { b: 41, g: 45, r: 24 }, 35) &&
    near(sample(1730, 20), { b: 41, g: 45, r: 24 }, 35);
  if (loadedSave) {
    return "loaded-save";
  }

  return "unknown";
};

const detectBlockingOverlay = (image: PngImage): BlockingOverlay => {
  const sample = (x: number, y: number): Pixel => pixelAt(image, x, y);
  return isMacSystemAlertOverlay(sample) ? "mac-system-alert" : null;
};

const classifyExistingScreenshot = (options: Options): void => {
  const path = options.classifyScreenshotPath ?? fail("Missing screenshot path.");
  const image = parsePng(path);
  if (!options.skipResolutionCheck) {
    assertScreenshotSize(path, image, options.expectedResolution);
  }

  const screen = classifyScreen(image);
  const blockingOverlay = detectBlockingOverlay(image);
  log(
    `classify_screenshot path=${path} resolution=${image.width}x${image.height} screen=${screen} blocking_overlay=${blockingOverlay ?? "none"}`,
  );
};

const assertScreenshotSize = (path: string, image: PngImage, expectedResolution: string): void => {
  const actualResolution = `${image.width}x${image.height}`;
  if (actualResolution !== expectedResolution) {
    fail(`Expected screenshot ${path} to be ${expectedResolution}, got ${actualResolution}.`);
  }
};

const captureScreenshot = (artifactDir: string, name: string, expectedResolution: string): Screenshot => {
  activateTimberborn(`screenshot:${name}`);
  assertTimberbornForeground(`screenshot:${name}`);
  const path = join(artifactDir, `${name}.png`);
  const result = run("screencapture", ["-x", path]);
  if (result.exitCode !== 0 || !existsSync(path)) {
    fail(`screencapture failed for ${name}: ${result.stderr.trim() || result.stdout.trim() || "no PNG created"}`);
  }

  const image = parsePng(path);
  assertScreenshotSize(path, image, expectedResolution);
  const screen = classifyScreen(image);
  const blockingOverlay = detectBlockingOverlay(image);
  log(`screenshot name=${name} path=${path} screen=${screen} blocking_overlay=${blockingOverlay ?? "none"}`);
  return { blockingOverlay, image, path, screen };
};

const captureScreenshotForWait = (
  artifactDir: string,
  name: string,
  expectedResolution: string,
): Screenshot | null => {
  try {
    return captureScreenshot(artifactDir, name, expectedResolution);
  } catch (error) {
    const message = formatError(error);
    if (isTransientForegroundFailure(message)) {
      log(`screenshot_wait_deferred name=${name} reason=${compactLogToken(message)}`);
      return null;
    }

    throw error;
  }
};

const failOnBlockingOverlay = (screenshot: Screenshot): void => {
  if (screenshot.blockingOverlay === "mac-system-alert") {
    fail(
      `A macOS system alert is covering Timberborn in ${screenshot.path}. Clear the alert manually before running the latest-save startup utility.`,
    );
  }
};

const captureFrameSample = (artifactDir: string, name: string, expectedResolution: string): FastFrameSample => {
  try {
    const screenshot = captureScreenshot(artifactDir, name, expectedResolution);
    return {
      blockingOverlay: screenshot.blockingOverlay,
      name,
      path: screenshot.path,
      screen: screenshot.screen,
    };
  } catch (error) {
    const path = join(artifactDir, `${name}.png`);
    const message = error instanceof Error ? error.message : String(error);
    log(`frame_sample_failed name=${name} error=${message.replaceAll(/\s+/gu, " ")}`);
    return {
      blockingOverlay: "capture-error",
      name,
      path,
      screen: "capture-error",
    };
  }
};

const startFastFrameSampler = (artifactDir: string, options: Options): FastFrameSampler => {
  const samples: FastFrameSample[] = [];
  let stopped = false;
  let sampleIndex = 0;

  const sample = (): void => {
    if (stopped) {
      return;
    }

    sampleIndex += 1;
    samples.push(
      captureFrameSample(artifactDir, `fast-frame-${String(sampleIndex).padStart(2, "0")}`, options.expectedResolution),
    );
  };

  sample();
  const interval = setInterval(sample, fastFrameIntervalMs);

  return {
    samples,
    stop: async () => {
      stopped = true;
      clearInterval(interval);
      sampleIndex += 1;
      samples.push(
        captureFrameSample(artifactDir, `fast-frame-${String(sampleIndex).padStart(2, "0")}`, options.expectedResolution),
      );
      return samples;
    },
  };
};

const writeFastFrameManifest = (artifactDir: string, samples: FastFrameSample[]): void => {
  const rows = [
    "name,path,screen,blocking_overlay",
    ...samples.map((sample) =>
      [sample.name, sample.path, sample.screen, sample.blockingOverlay ?? "none"]
        .map((value) => `"${value.replaceAll('"', '""')}"`)
        .join(","),
    ),
    "",
  ];
  writeFileSync(join(artifactDir, "fast-frame-samples.csv"), rows.join("\n"));
  log(`fast_frame_samples=${samples.length} manifest=${join(artifactDir, "fast-frame-samples.csv")}`);
};

const uniqueScreenKinds = (screens: ScreenKind[]): ScreenKind[] =>
  screens.reduce<ScreenKind[]>((uniqueScreens, screen) => {
    if (!uniqueScreens.includes(screen)) {
      uniqueScreens.push(screen);
    }

    return uniqueScreens;
  }, []);

const clickTarget = (target: CoordinateTarget): void => {
  activateTimberborn(`click:${target.id}`);
  log(`click target=${target.id} x=${target.x} y=${target.y}`);
  const result = run("cliclick", [`c:${target.x},${target.y}`]);
  if (result.exitCode !== 0) {
    fail(`cliclick failed for ${target.id}: ${commandFailureText(result)}`);
  }
};

const pressEnter = (actionName: string): void => {
  activateTimberborn(`press:${actionName}`);
  log(`keypress action=${actionName} key=return`);
  const result = run("cliclick", ["kp:return"]);
  if (result.exitCode !== 0) {
    fail(`cliclick failed for ${actionName}: ${commandFailureText(result)}`);
  }
};

const pressSpeedOne = (actionName: string): void => {
  activateTimberborn(`press:${actionName}`);
  log(`keypress action=${actionName} key=1`);
  const result = run("cliclick", ["kp:num-1"]);
  if (result.exitCode !== 0) {
    fail(`cliclick failed for ${actionName}: ${commandFailureText(result)}`);
  }
};

const waitForKnownScreen = async (artifactDir: string, options: Options, prefix: string): Promise<Screenshot> => {
  const startedAt = Date.now();
  let attempts = 0;

  while (Date.now() - startedAt <= options.waitSeconds * 1000) {
    attempts += 1;
    const screenshot = captureScreenshotForWait(
      artifactDir,
      `${prefix}-${String(attempts).padStart(2, "0")}`,
      options.expectedResolution,
    );
    if (screenshot === null) {
      await Bun.sleep(1000);
      continue;
    }

    if (screenshot.screen !== "unknown" || screenshot.blockingOverlay !== null) {
      return screenshot;
    }

    await Bun.sleep(1000);
  }

  return fail(`Timed out waiting for a known Timberborn screen. Last screenshots are in ${artifactDir}.`);
};

const waitForKnownScreenChange = async (
  artifactDir: string,
  options: Options,
  prefix: string,
  previousScreen: ScreenKind,
  onStillSame: (attempts: number) => void = () => undefined,
): Promise<Screenshot> => {
  const startedAt = Date.now();
  let attempts = 0;

  while (Date.now() - startedAt <= options.waitSeconds * 1000) {
    attempts += 1;
    const screenshot = captureScreenshotForWait(
      artifactDir,
      `${prefix}-${String(attempts).padStart(2, "0")}`,
      options.expectedResolution,
    );
    if (screenshot === null) {
      await Bun.sleep(1000);
      continue;
    }

    failOnBlockingOverlay(screenshot);
    if (screenshot.screen !== "unknown" && screenshot.screen !== previousScreen) {
      return screenshot;
    }

    if (screenshot.screen === previousScreen) {
      onStillSame(attempts);
    }

    await Bun.sleep(1000);
  }

  return fail(`Timed out waiting for ${previousScreen} to advance. Last screenshots are in ${artifactDir}.`);
};

const waitForLoadedSaveAfterContinue = async (
  artifactDir: string,
  options: Options,
  prefix: string,
): Promise<Screenshot> => {
  const startedAt = Date.now();
  let attempts = 0;

  while (Date.now() - startedAt <= options.waitSeconds * 1000) {
    attempts += 1;
    const screenshot = captureScreenshotForWait(
      artifactDir,
      `${prefix}-${String(attempts).padStart(2, "0")}`,
      options.expectedResolution,
    );
    if (screenshot === null) {
      await Bun.sleep(1000);
      continue;
    }

    failOnBlockingOverlay(screenshot);
    if (screenshot.screen === "loaded-save") {
      return screenshot;
    }

    if (screenshot.screen === "main-menu" && attempts % 5 === 0) {
      clickTarget(targets.mainContinue);
    } else if (screenshot.screen !== "main-menu" && (attempts === 1 || attempts % 5 === 0)) {
      pressSpeedOne("loaded_save_speed_probe");
    }

    await Bun.sleep(1000);
  }

  return fail(`Timed out waiting for loaded-save after Continue. Last screenshots are in ${artifactDir}.`);
};

const waitForScreen = async (
  artifactDir: string,
  options: Options,
  expectedScreen: ScreenKind,
  prefix: string,
): Promise<Screenshot> => {
  const startedAt = Date.now();
  let attempts = 0;

  while (Date.now() - startedAt <= options.waitSeconds * 1000) {
    attempts += 1;
    const screenshot = captureScreenshotForWait(
      artifactDir,
      `${prefix}-${String(attempts).padStart(2, "0")}`,
      options.expectedResolution,
    );
    if (screenshot === null) {
      await Bun.sleep(1000);
      continue;
    }

    failOnBlockingOverlay(screenshot);
    if (screenshot.screen === expectedScreen) {
      return screenshot;
    }

    await Bun.sleep(1000);
  }

  return fail(`Timed out waiting for ${expectedScreen}. Last screenshots are in ${artifactDir}.`);
};

const readStatus = async (commandDir: string, waitSeconds: number): Promise<CommandResult | null> => {
  const inboxPath = join(commandDir, inboxFileName);
  const outboxPath = join(commandDir, outboxFileName);
  const previousModified = existsSync(outboxPath) ? statSync(outboxPath).mtimeMs : 0;

  mkdirSync(dirname(inboxPath), { recursive: true });
  writeFileSync(inboxPath, "status\n");

  const startedAt = Date.now();
  while (Date.now() - startedAt <= waitSeconds * 1000) {
    if (existsSync(outboxPath) && statSync(outboxPath).mtimeMs > previousModified) {
      const text = readFileSync(outboxPath, "utf8");
      const tickMatch = text.match(/\btick_count=(\d+)\b/u);
      return {
        text,
        tickCount: tickMatch ? Number(tickMatch[1]) : null,
      };
    }

    await Bun.sleep(100);
  }

  return null;
};

const ensureUnpaused = async (artifactDir: string, options: Options): Promise<CommandResult | null> => {
  const beforeUnpause = captureScreenshot(artifactDir, "04-loaded-save-before-unpause", options.expectedResolution);
  if (beforeUnpause.screen !== "loaded-save") {
    fail(`Expected loaded-save HUD before unpause, got ${beforeUnpause.screen}.`);
  }

  const firstStatus = await readStatus(options.commandDir, 6);
  if (firstStatus !== null && firstStatus.tickCount !== null && firstStatus.tickCount > 0) {
    await Bun.sleep(1500);
    const secondStatus = await readStatus(options.commandDir, 6);
    if (secondStatus !== null && secondStatus.tickCount !== null && secondStatus.tickCount > firstStatus.tickCount) {
      captureScreenshot(artifactDir, "05-loaded-save-after-unpause", options.expectedResolution);
      writeFileSync(join(artifactDir, "command-status-after-unpause.txt"), secondStatus.text);
      log(`already_unpaused tick_count_before=${firstStatus.tickCount} tick_count_after=${secondStatus.tickCount}`);
      return secondStatus;
    }
  }

  clickTarget(targets.hudSpeed1);
  await Bun.sleep(3000);
  captureScreenshot(artifactDir, "05-loaded-save-after-unpause", options.expectedResolution);

  if (!options.postStatus) {
    return firstStatus;
  }

  const postStatus =
    (await readStatus(options.commandDir, 6)) ??
    fail("Timed out waiting for post-unpause status. The loaded save may not have the Wildfire command bridge active.");

  const requiredParts = [
    "wildfire_command_result",
    "command=status",
    "success=true",
    "status=success",
    "simulator_integrated=true",
  ];
  const missingParts = requiredParts.filter((part) => !postStatus.text.includes(part));
  if (missingParts.length > 0) {
    fail(`Post-unpause status was missing: ${missingParts.join(", ")}. Result: ${postStatus.text.trimEnd()}`);
  }

  if (postStatus.tickCount === null || postStatus.tickCount <= 0) {
    fail(`Post-unpause status did not report an advanced tick_count. Result: ${postStatus.text.trimEnd()}`);
  }

  writeFileSync(join(artifactDir, "command-status-after-unpause.txt"), postStatus.text);
  log(`post_status_ok tick_count=${postStatus.tickCount}`);
  return postStatus;
};

const copyPlayerLog = (artifactDir: string, playerLogPath: string): string => {
  const copyPath = join(artifactDir, "Player.log");
  if (existsSync(playerLogPath)) {
    copyFileSync(playerLogPath, copyPath);
  } else {
    writeFileSync(copyPath, `Player.log was missing: ${playerLogPath}\n`);
  }

  return copyPath;
};

const writeSummary = (
  artifactDir: string,
  options: Options,
  observedScreens: ScreenKind[],
  postStatus: CommandResult | null,
  startupPath: "classifier" | "fast-recorded",
  fastFrameSamples: FastFrameSample[] = [],
): void => {
  const playerLogCopy = copyPlayerLog(artifactDir, options.playerLogPath);
  const logWindow = existsSync(options.playerLogPath) ? readFileSync(options.playerLogPath, "utf8") : "";
  const dispatchLines = logWindow
    .split(/\r?\n/u)
    .filter((line) => line.includes("wildfire_timberborn_dispatch_completed"))
    .slice(-5);
  const summary = [
    "wildfire_latest_save_startup_result=pass",
    `mode=${options.mode}`,
    `startup_path=${startupPath}`,
    `observed_screens=${observedScreens.join(",") || "none"}`,
    `artifacts_dir=${artifactDir}`,
    `player_log=${playerLogCopy}`,
    `frame_sampling=${fastFrameSamples.length > 0 ? "screenshot-sampling" : "not_used"}`,
    `frame_sample_manifest=${fastFrameSamples.length > 0 ? join(artifactDir, "fast-frame-samples.csv") : "not_used"}`,
    `post_status=${postStatus?.text.trim().replaceAll(/\s+/gu, " ") ?? "not_requested_or_unavailable"}`,
    "",
    "[recent dispatch evidence]",
    ...(dispatchLines.length > 0 ? dispatchLines : ["not_found_in_copied_log"]),
    "",
  ].join("\n");

  writeFileSync(join(artifactDir, "latest-save-startup-summary.txt"), summary);
  log(`artifacts_dir=${artifactDir}`);
  log(`summary=${join(artifactDir, "latest-save-startup-summary.txt")}`);
};

const printPlan = (options: Options): void => {
  log(`mode=${options.mode}`);
  log(`startup_path=${options.mode === "launch" ? "fast-recorded" : "classifier"}`);
  if (options.classifyScreenshotPath !== null) {
    log(`classify_screenshot=${options.classifyScreenshotPath}`);
  }
  log(`player_log=${options.playerLogPath}`);
  log(`command_dir=${options.commandDir}`);
  log(`artifacts_dir=${options.artifactsDir}`);
  log(`wait_seconds=${options.waitSeconds}`);
  log(`expected_resolution=${options.skipResolutionCheck ? "skipped" : options.expectedResolution}`);
  log(`post_status=${options.postStatus}`);
  log(`latest_save_preflight=${options.skipLatestSavePreflight ? "skipped" : "enabled"}`);
  if (options.mode === "launch") {
    log(`fast_startup_timing_ms=frame_interval:${fastFrameIntervalMs}`);
  }
  log(`targets=${Object.values(targets).map((target) => `${target.id}@${target.x},${target.y}`).join(",")}`);
  log(`lock=${lockDir}`);
};

const validateDryRunPreconditions = (options: Options): void => {
  assertCoordinateGuideTargets();
  assertToolExists("cliclick");
  assertToolExists("screencapture");
  assertToolExists("osascript");
  if (!options.skipResolutionCheck) {
    assertResolution(options.expectedResolution);
  }
  assertLatestSaveCanBeLoadedByContinue(options);
  log(options.dryRun ? "dry_run_complete" : "preconditions_ok");
};

const runClassifierStartupPath = async (
  artifactDir: string,
  options: Options,
): Promise<{ observedScreens: ScreenKind[]; postStatus: CommandResult | null }> => {
  const observedScreens: ScreenKind[] = [];
  let current = await waitForKnownScreen(artifactDir, options, "00-current-screen");
  observedScreens.push(current.screen);
  failOnBlockingOverlay(current);

  if (current.screen === "startup-mods") {
    clickTarget(targets.startupModsOk);
    await Bun.sleep(1000);
    current = await waitForKnownScreen(artifactDir, options, "01-after-startup-mods");
    observedScreens.push(current.screen);
    failOnBlockingOverlay(current);
  }

  if (current.screen === "experimental-mode") {
    clickTarget(targets.experimentalStart);
    await Bun.sleep(1000);
    current = await waitForKnownScreen(artifactDir, options, "02-after-experimental-mode");
    observedScreens.push(current.screen);
  }

  if (current.screen === "main-menu") {
    assertLatestSaveCanBeLoadedByContinue(options);
    clickTarget(targets.mainContinue);
    await waitForLoadedSaveAfterContinue(artifactDir, options, "03-after-main-continue");
    observedScreens.push("loaded-save");
  } else if (current.screen !== "loaded-save") {
    fail(`Expected main-menu or loaded-save after startup gates, got ${current.screen}.`);
  }

  const postStatus = await ensureUnpaused(artifactDir, options);
  return { observedScreens, postStatus };
};

const runFastRecordedStartupPath = async (
  artifactDir: string,
  options: Options,
): Promise<{ fastFrameSamples: FastFrameSample[]; observedScreens: ScreenKind[]; postStatus: CommandResult | null }> => {
  const observedScreens: ScreenKind[] = [];
  const sampler = startFastFrameSampler(artifactDir, options);
  let fastFrameSamples: FastFrameSample[] = [];

  try {
    log("fast_startup_sequence_started recording=screenshot-sampling");
    let current = await waitForKnownScreen(artifactDir, options, "00-current-screen");
    observedScreens.push(current.screen);
    failOnBlockingOverlay(current);

    if (current.screen === "startup-mods") {
      pressEnter("startup_mods.confirm");
      current = await waitForKnownScreenChange(artifactDir, options, "01-after-startup-mods", "startup-mods", (attempts) => {
        if (attempts % 15 === 0) {
          clickTarget(targets.startupModsOk);
        } else if (attempts % 5 === 0) {
          pressEnter("startup_mods.confirm_retry");
        }
      });
      observedScreens.push(current.screen);
      failOnBlockingOverlay(current);
    }

    if (current.screen === "experimental-mode") {
      pressEnter("experimental_mode.start");
      current = await waitForKnownScreenChange(artifactDir, options, "02-after-experimental-mode", "experimental-mode", (attempts) => {
        if (attempts % 5 === 0) {
          pressEnter("experimental_mode.start_retry");
        }
      });
      observedScreens.push(current.screen);
    }

    if (current.screen === "main-menu") {
      assertLatestSaveCanBeLoadedByContinue(options);
      clickTarget(targets.mainContinue);
      await waitForLoadedSaveAfterContinue(artifactDir, options, "03-after-main-continue");
      observedScreens.push("loaded-save");
    } else if (current.screen !== "loaded-save") {
      fail(`Expected main-menu or loaded-save after startup gates, got ${current.screen}.`);
    }

    const postStatus = await ensureUnpaused(artifactDir, options);
    fastFrameSamples = await sampler.stop();
    writeFastFrameManifest(artifactDir, fastFrameSamples);
    return {
      fastFrameSamples,
      observedScreens: uniqueScreenKinds([
        ...fastFrameSamples
          .map((sample) => sample.screen)
          .filter((screen): screen is ScreenKind => screen !== "capture-error"),
        ...observedScreens,
      ]),
      postStatus,
    };
  } catch (error) {
    fastFrameSamples = await sampler.stop();
    writeFastFrameManifest(artifactDir, fastFrameSamples);
    throw error;
  }
};

const main = async (): Promise<void> => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  if (options.classifyScreenshotPath !== null) {
    classifyExistingScreenshot(options);
    return;
  }

  printPlan(options);
  validateDryRunPreconditions(options);
  if (options.dryRun) {
    return;
  }

  const releaseLock = acquireLock(options);
  const artifactDir = createArtifactDir(options);
  const observedScreens: ScreenKind[] = [];

  try {
    const wasRunningBeforeLaunch = isTimberbornRunning();
    await launchOrAttach(options);

    if (options.mode === "launch" && !wasRunningBeforeLaunch) {
      const result = await runFastRecordedStartupPath(artifactDir, options);
      writeSummary(artifactDir, options, result.observedScreens, result.postStatus, "fast-recorded", result.fastFrameSamples);
    } else {
      if (options.mode === "launch" && wasRunningBeforeLaunch) {
        log("launch_requested_but_timberborn_already_running=true startup_path=classifier");
      }
      const result = await runClassifierStartupPath(artifactDir, options);
      observedScreens.push(...result.observedScreens);
      writeSummary(artifactDir, options, observedScreens, result.postStatus, "classifier");
    }
    log("latest_save_startup_complete");
  } finally {
    releaseLock();
  }
};

try {
  await main();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
}
