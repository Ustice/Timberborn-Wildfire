#!/usr/bin/env bun

import { existsSync, mkdirSync, writeFileSync } from "fs";
import { homedir } from "os";
import { dirname, resolve } from "path";

type Options = {
  baseBranch: string;
  branch: string | null;
  cleanupOwner: string;
  dryRun: boolean;
  help: boolean;
  issue: string | null;
  role: string | null;
  slug: string | null;
  status: string;
  worktree: string | null;
};

type Allocation = {
  baseBranch: string;
  branch: string;
  cleanupOwner: string;
  issue: string | null;
  role: string;
  slug: string;
  status: string;
  worktree: string;
};

const usage = `Usage:
  bun scripts/create-agent-worktree.ts --role <role> --slug <slug> [options]

Options:
  --issue <id>              Issue, PR, or sprint id to include in names.
  --role <role>             Sub-agent role, such as worker, qa, reviewer, process.
  --slug <slug>             Short human-readable task slug.
  --base <branch>           Base branch or ref. Default: main.
  --branch <name>           Branch name. Default: codex/<role>/<issue-slug>.
  --worktree <path>         Worktree path. Default: ~/repos/wildfire-worktrees/<role>-<issue-slug>.
  --cleanup-owner <owner>   Cleanup owner written to CONTEXT.md. Default: coordinator.
  --status <status>         Context status. Default: active.
  --dry-run                 Print planned actions without creating anything.
  --help                    Show this help.

Examples:
  bun scripts/create-agent-worktree.ts --role worker --issue TWF-115 --slug storage-fire
  bun scripts/create-agent-worktree.ts --role process --slug sprint-retro-kaizen --dry-run
`;

const fail = (message: string): never => {
  throw new Error(`[create-agent-worktree] ${message}`);
};

const requireValue = (args: string[], index: number, flag: string): string => {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    fail(`${flag} requires a value.`);
  }

  return value;
};

const parseArgs = (args: string[]): Options => {
  const options: Options = {
    baseBranch: "main",
    branch: null,
    cleanupOwner: "coordinator",
    dryRun: false,
    help: false,
    issue: null,
    role: null,
    slug: null,
    status: "active",
    worktree: null,
  };
  let skipNext = false;

  args.reduce((_, arg, index) => {
    if (skipNext) {
      skipNext = false;
      return undefined;
    }

    if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--dry-run") {
      options.dryRun = true;
    } else if (arg === "--base") {
      options.baseBranch = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--base=")) {
      options.baseBranch = arg.slice("--base=".length);
    } else if (arg === "--branch") {
      options.branch = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--branch=")) {
      options.branch = arg.slice("--branch=".length);
    } else if (arg === "--cleanup-owner") {
      options.cleanupOwner = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--cleanup-owner=")) {
      options.cleanupOwner = arg.slice("--cleanup-owner=".length);
    } else if (arg === "--issue") {
      options.issue = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--issue=")) {
      options.issue = arg.slice("--issue=".length);
    } else if (arg === "--role") {
      options.role = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--role=")) {
      options.role = arg.slice("--role=".length);
    } else if (arg === "--slug") {
      options.slug = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--slug=")) {
      options.slug = arg.slice("--slug=".length);
    } else if (arg === "--status") {
      options.status = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--status=")) {
      options.status = arg.slice("--status=".length);
    } else if (arg === "--worktree") {
      options.worktree = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--worktree=")) {
      options.worktree = arg.slice("--worktree=".length);
    } else {
      fail(`Unknown argument: ${arg}`);
    }

    return undefined;
  }, undefined);

  return options;
};

const sanitizeSegment = (value: string): string => {
  const sanitized = value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9._-]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .replace(/-{2,}/g, "-");

  if (!sanitized) {
    fail(`Could not derive a safe name from "${value}".`);
  }

  return sanitized;
};

const expandHome = (path: string): string => {
  if (path === "~") {
    return homedir();
  }

  return path.startsWith("~/") ? `${homedir()}${path.slice(1)}` : path;
};

const shellQuote = (value: string): string => (value.match(/^[a-zA-Z0-9_./:@%+=,-]+$/) ? value : JSON.stringify(value));

const run = (command: string[], dryRun: boolean): void => {
  console.log(`$ ${command.map(shellQuote).join(" ")}`);

  if (dryRun) {
    return;
  }

  const result = Bun.spawnSync(command, {
    stderr: "pipe",
    stdout: "pipe",
  });

  if (result.exitCode !== 0) {
    const stderr = new TextDecoder().decode(result.stderr).trim();
    const stdout = new TextDecoder().decode(result.stdout).trim();
    fail(stderr || stdout || `${command[0]} exited with ${result.exitCode}`);
  }
};

const ensureParentDirectory = (path: string, dryRun: boolean): void => {
  const parentPath = dirname(path);
  console.log(`$ mkdir -p ${shellQuote(parentPath)}`);

  if (!dryRun) {
    mkdirSync(parentPath, { recursive: true });
  }
};

const buildAllocation = (options: Options): Allocation => {
  const roleInput = options.role ?? fail("--role is required.");

  if (!options.slug && !options.issue) {
    fail("--slug or --issue is required.");
  }

  const role = sanitizeSegment(roleInput);
  const slug = sanitizeSegment([options.issue, options.slug].filter((value): value is string => Boolean(value)).join("-"));
  const branch = options.branch ?? `codex/${role}/${slug}`;
  const worktree = resolve(expandHome(options.worktree ?? `~/repos/wildfire-worktrees/${role}-${slug}`));

  return {
    baseBranch: options.baseBranch,
    branch,
    cleanupOwner: options.cleanupOwner,
    issue: options.issue,
    role,
    slug,
    status: options.status,
    worktree,
  };
};

const yamlValue = (value: string | null): string => (value === null ? "null" : JSON.stringify(value));

const contextFor = (allocation: Allocation): string => `---
task: ${yamlValue(allocation.issue ?? allocation.slug)}
role: ${yamlValue(allocation.role)}
branch: ${yamlValue(allocation.branch)}
worktree: ${yamlValue(allocation.worktree)}
base_branch: ${yamlValue(allocation.baseBranch)}
cleanup_owner: ${yamlValue(allocation.cleanupOwner)}
status: ${yamlValue(allocation.status)}
updated: ${yamlValue(new Date().toISOString())}
---

# Task Context

## Current State

- Coordinator created this worktree context. Replace this bullet with the current task state.

## Decisions

- None yet.

## Constraints

- Work only in the assigned worktree and branch.
- Keep this file short and update by compression, not accumulation.

## Evidence

- None yet.

## Next Action

- Read the assignment packet and role instructions.

## Process Notes

- None yet.
`;

const createAllocation = (allocation: Allocation, dryRun: boolean): void => {
  if (existsSync(allocation.worktree)) {
    fail(`Worktree path already exists: ${allocation.worktree}`);
  }

  ensureParentDirectory(allocation.worktree, dryRun);
  run(["git", "worktree", "add", "-b", allocation.branch, allocation.worktree, allocation.baseBranch], dryRun);

  const contextPath = `${allocation.worktree}/CONTEXT.md`;
  console.log(`write ${shellQuote(contextPath)}`);

  if (!dryRun) {
    writeFileSync(contextPath, contextFor(allocation), { encoding: "utf8", flag: "wx" });
  }
};

const main = (): void => {
  const options = parseArgs(Bun.argv.slice(2));

  if (options.help) {
    console.log(usage);
    return;
  }

  const allocation = buildAllocation(options);
  createAllocation(allocation, options.dryRun);
};

main();
