#!/usr/bin/env bun

import { existsSync, lstatSync, readdirSync, rmSync, statSync } from "fs";
import { extname, join, resolve } from "path";

type CleanupOptions = {
  ageHours: number;
  allFiles: boolean;
  dryRun: boolean;
  help: boolean;
  roots: string[];
  verbose: boolean;
};

type Candidate = {
  modifiedAtMs: number;
  path: string;
  size: number;
};

const home = process.env.HOME ?? "";
const defaultQaRoot = join(home, "Library", "Application Support", "Mechanistry", "Timberborn", "WildfireQA");
const defaultTempQaRoot = join("/tmp", "wildfire-qa");
const mediaExtensions = new Set([".jpeg", ".jpg", ".mov", ".mp4", ".png"]);

const usage = `Usage:
  bun scripts/cleanup-wildfire-qa-media.ts [options]

Options:
  --root <path>             Root folder to scan. Can be repeated. Default: /tmp/wildfire-qa plus Timberborn WildfireQA.
  --age-hours <hours>       Delete files older than this many hours. Default: 24.
  --all-files               Delete every old file under the scan roots, not just image/video media.
  --dry-run                 Print what would be deleted without deleting files.
  --verbose                 Print each deleted or matched path.
  --help                    Show this help.

Examples:
  bun scripts/cleanup-wildfire-qa-media.ts --dry-run
  bun scripts/cleanup-wildfire-qa-media.ts --age-hours 24
`;

const fail = (message: string): never => {
  throw new Error(`[wildfire-qa-cleanup] ${message}`);
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

const parseArgs = (args: string[]): CleanupOptions => {
  const options: CleanupOptions = {
    ageHours: 24,
    allFiles: false,
    dryRun: false,
    help: false,
    roots: [],
    verbose: false,
  };
  let skipNext = false;

  args.reduce((_, arg, index) => {
    if (skipNext) {
      skipNext = false;
      return undefined;
    }

    if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--root") {
      options.roots.push(resolve(requireValue(args, index + 1, arg)));
      skipNext = true;
    } else if (arg.startsWith("--root=")) {
      options.roots.push(resolve(arg.slice("--root=".length)));
    } else if (arg === "--age-hours") {
      options.ageHours = parsePositiveNumber(requireValue(args, index + 1, arg), arg);
      skipNext = true;
    } else if (arg.startsWith("--age-hours=")) {
      options.ageHours = parsePositiveNumber(arg.slice("--age-hours=".length), "--age-hours");
    } else if (arg === "--all-files") {
      options.allFiles = true;
    } else if (arg === "--dry-run") {
      options.dryRun = true;
    } else if (arg === "--verbose") {
      options.verbose = true;
    } else {
      fail(`Unknown option: ${arg}`);
    }

    return undefined;
  }, undefined);

  return {
    ...options,
    roots: options.roots.length === 0 ? [defaultTempQaRoot, defaultQaRoot] : options.roots,
  };
};

const isMediaFile = (path: string): boolean => mediaExtensions.has(extname(path).toLowerCase());

const collectCandidates = (root: string, options: CleanupOptions, cutoffMs: number): Candidate[] => {
  if (!existsSync(root)) {
    return [];
  }

  const entry = lstatSync(root);
  if (entry.isSymbolicLink()) {
    return [];
  }

  if (entry.isFile()) {
    return entry.mtimeMs < cutoffMs && (options.allFiles || isMediaFile(root))
      ? [{ modifiedAtMs: entry.mtimeMs, path: root, size: entry.size }]
      : [];
  }

  if (!entry.isDirectory()) {
    return [];
  }

  return readdirSync(root)
    .map((name) => join(root, name))
    .flatMap((path) => collectCandidates(path, options, cutoffMs));
};

const formatBytes = (bytes: number): string => {
  const units = ["B", "KB", "MB", "GB", "TB"];
  const index = Math.min(Math.floor(Math.log(Math.max(bytes, 1)) / Math.log(1024)), units.length - 1);
  const value = bytes / 1024 ** index;
  return `${value.toFixed(index === 0 ? 0 : 1)} ${units[index]}`;
};

const main = (): void => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  const cutoffMs = Date.now() - options.ageHours * 60 * 60 * 1000;
  const roots = options.roots.map((root) => resolve(root));
  const candidates = roots.flatMap((root) => collectCandidates(root, options, cutoffMs));
  const totalBytes = candidates.reduce((sum, candidate) => sum + candidate.size, 0);

  candidates
    .sort((left, right) => left.path.localeCompare(right.path))
    .map((candidate) => {
      if (options.verbose || options.dryRun) {
        console.log(
          `${options.dryRun ? "would_delete" : "delete"} path=${JSON.stringify(candidate.path)} size=${candidate.size}`,
        );
      }

      if (!options.dryRun) {
        rmSync(candidate.path, { force: true });
      }

      return candidate.path;
    });

  console.log(
    [
      `roots=${roots.map((root) => JSON.stringify(root)).join(",")}`,
      `age_hours=${options.ageHours}`,
      `mode=${options.allFiles ? "all-files" : "media-only"}`,
      `deleted_files=${options.dryRun ? 0 : candidates.length}`,
      `matched_files=${candidates.length}`,
      `reclaimable_bytes=${totalBytes}`,
      `reclaimable=${JSON.stringify(formatBytes(totalBytes))}`,
      `dry_run=${options.dryRun}`,
    ].join(" "),
  );
};

main();
