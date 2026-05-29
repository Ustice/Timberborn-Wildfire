#!/usr/bin/env bun

import { PrismaLibSql } from "@prisma/adapter-libsql";
import { createHash } from "crypto";
import { existsSync, readFileSync, statSync } from "fs";
import { resolve } from "path";
import { PrismaClient } from "../prisma/generated/client/client";

type FailureClass = "environment_failure" | "product_failure" | "test_design_failure" | "tool_failure" | "unknown";
type RunResult = "blocked" | "fail" | "pass" | "unknown";

type ArtifactInput = {
  path: string;
  type: string;
};

type LogOptions = {
  artifacts: ArtifactInput[];
  category: string;
  command: string;
  dbPath: string;
  detail: string;
  displayResolution: string | null;
  failureClass: FailureClass | null;
  finishedAt: Date;
  gitSha: string | null;
  help: boolean;
  reason: string;
  result: RunResult | null;
  startedAt: Date;
  timberbornVersion: string | null;
  tool: string;
  uiScale: string | null;
};

const defaultDbPath = "qa/tool-runs.sqlite";
const runResults = new Set<RunResult>(["blocked", "fail", "pass", "unknown"]);
const failureClasses = new Set<FailureClass>([
  "environment_failure",
  "product_failure",
  "test_design_failure",
  "tool_failure",
  "unknown",
]);

const usage = `Usage:
  bun scripts/qa-log-tool-run.ts --tool <name> --result <pass|fail|blocked|unknown> [options]

Options:
  --db <path>                    SQLite DB path. Default: qa/tool-runs.sqlite.
  --tool <name>                  Stable QA tool name.
  --category <name>              Tool category. Default: other.
  --command <text>               Command or action sequence. Default: current argv.
  --result <value>               pass, fail, blocked, or unknown.
  --failure-class <class>        tool_failure, environment_failure, product_failure, test_design_failure, or unknown.
  --reason <text>                Short failure reason.
  --detail <text>                Longer failure detail.
  --artifact <type:path>         Artifact path. Can be repeated.
  --git-sha <sha>                Git SHA. Default: current HEAD when available.
  --timberborn-version <text>    Timberborn version or build label.
  --display-resolution <text>    Display resolution used by the run, such as 1920x1080.
  --ui-scale <text>              Timberborn UI scale or display scale.
  --started-at <iso>             Start timestamp. Default: now.
  --finished-at <iso>            Finish timestamp. Default: now.
  --help                         Show this help.

Examples:
  bun scripts/qa-log-tool-run.ts --tool invoke-timberborn-command --category command --result pass --command "bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6"
  bun scripts/qa-log-tool-run.ts --tool load-latest-save-and-unpause --result fail --failure-class tool_failure --reason "screen classifier missed loaded save" --artifact screenshot:/tmp/wildfire-qa/latest-save-startup/failure.png
`;

const fail = (message: string): never => {
  throw new Error(`[qa-log-tool-run] ${message}`);
};

const sqliteUrlForPath = (path: string): string => `file:${resolve(path)}`;

const ensureMigrated = (dbPath: string): void => {
  const result = Bun.spawnSync(["bunx", "prisma", "migrate", "deploy"], {
    env: {
      ...process.env,
      DATABASE_URL: sqliteUrlForPath(dbPath),
    },
    stderr: "pipe",
    stdout: "pipe",
  });

  if (result.exitCode !== 0) {
    const stderr = new TextDecoder().decode(result.stderr).trim();
    const stdout = new TextDecoder().decode(result.stdout).trim();
    fail(`Prisma migration failed. ${stderr || stdout}`);
  }
};

const prismaFor = (dbPath: string): PrismaClient => {
  const adapter = new PrismaLibSql({
    url: sqliteUrlForPath(dbPath),
  });

  return new PrismaClient({ adapter });
};

const requireValue = (args: string[], index: number, flag: string): string => {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    fail(`${flag} requires a value.`);
  }

  return value;
};

const readGitSha = (): string | null => {
  const result = Bun.spawnSync(["git", "rev-parse", "--short=12", "HEAD"], {
    stderr: "pipe",
    stdout: "pipe",
  });
  if (result.exitCode !== 0) {
    return null;
  }

  return new TextDecoder().decode(result.stdout).trim() || null;
};

const parseDate = (value: string, flag: string): Date => {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    fail(`${flag} requires an ISO-like timestamp: ${value}`);
  }

  return date;
};

const parseFailureClass = (value: string): FailureClass => {
  if (!failureClasses.has(value as FailureClass)) {
    fail(`Invalid failure class: ${value}`);
  }

  return value as FailureClass;
};

const parseResult = (value: string): RunResult => {
  if (!runResults.has(value as RunResult)) {
    fail(`Invalid result: ${value}`);
  }

  return value as RunResult;
};

const parseArtifact = (value: string): ArtifactInput => {
  const separator = value.indexOf(":");
  if (separator <= 0 || separator === value.length - 1) {
    fail(`Artifact must use type:path format: ${value}`);
  }

  return {
    path: value.slice(separator + 1),
    type: value.slice(0, separator),
  };
};

const parseArgs = (args: string[]): LogOptions => {
  const now = new Date();
  const options: LogOptions = {
    artifacts: [],
    category: "other",
    command: Bun.argv.slice(2).join(" "),
    dbPath: defaultDbPath,
    detail: "",
    displayResolution: null,
    failureClass: null,
    finishedAt: now,
    gitSha: readGitSha(),
    help: false,
    reason: "",
    result: null,
    startedAt: now,
    timberbornVersion: null,
    tool: "",
    uiScale: null,
  };
  let skipNext = false;

  args.reduce((_, arg, index) => {
    if (skipNext) {
      skipNext = false;
      return undefined;
    }

    if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--db") {
      options.dbPath = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--db=")) {
      options.dbPath = arg.slice("--db=".length);
    } else if (arg === "--tool") {
      options.tool = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--tool=")) {
      options.tool = arg.slice("--tool=".length);
    } else if (arg === "--category") {
      options.category = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--category=")) {
      options.category = arg.slice("--category=".length);
    } else if (arg === "--command") {
      options.command = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--command=")) {
      options.command = arg.slice("--command=".length);
    } else if (arg === "--result") {
      options.result = parseResult(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--result=")) {
      options.result = parseResult(arg.slice("--result=".length));
    } else if (arg === "--failure-class") {
      options.failureClass = parseFailureClass(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--failure-class=")) {
      options.failureClass = parseFailureClass(arg.slice("--failure-class=".length));
    } else if (arg === "--reason") {
      options.reason = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--reason=")) {
      options.reason = arg.slice("--reason=".length);
    } else if (arg === "--detail") {
      options.detail = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--detail=")) {
      options.detail = arg.slice("--detail=".length);
    } else if (arg === "--artifact") {
      options.artifacts.push(parseArtifact(requireValue(args, index + 1, arg)));
      skipNext = true;
    } else if (arg.startsWith("--artifact=")) {
      options.artifacts.push(parseArtifact(arg.slice("--artifact=".length)));
    } else if (arg === "--git-sha") {
      options.gitSha = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--git-sha=")) {
      options.gitSha = arg.slice("--git-sha=".length);
    } else if (arg === "--timberborn-version") {
      options.timberbornVersion = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--timberborn-version=")) {
      options.timberbornVersion = arg.slice("--timberborn-version=".length);
    } else if (arg === "--display-resolution") {
      options.displayResolution = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--display-resolution=")) {
      options.displayResolution = arg.slice("--display-resolution=".length);
    } else if (arg === "--ui-scale") {
      options.uiScale = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--ui-scale=")) {
      options.uiScale = arg.slice("--ui-scale=".length);
    } else if (arg === "--started-at") {
      options.startedAt = parseDate(requireValue(args, index + 1, arg), arg);
      skipNext = true;
    } else if (arg.startsWith("--started-at=")) {
      options.startedAt = parseDate(arg.slice("--started-at=".length), "--started-at");
    } else if (arg === "--finished-at") {
      options.finishedAt = parseDate(requireValue(args, index + 1, arg), arg);
      skipNext = true;
    } else if (arg.startsWith("--finished-at=")) {
      options.finishedAt = parseDate(arg.slice("--finished-at=".length), "--finished-at");
    } else {
      fail(`Unknown argument: ${arg}`);
    }

    return undefined;
  }, undefined);

  return options;
};

const assertOptions = (options: LogOptions): void => {
  if (options.help) {
    return;
  }

  if (!options.tool.trim()) {
    fail("--tool is required.");
  }

  if (!options.result) {
    fail("--result is required.");
  }

  if (options.result !== "pass" && !options.failureClass) {
    fail("--failure-class is required when result is not pass.");
  }

  if (options.failureClass && !options.reason.trim()) {
    fail("--reason is required when --failure-class is present.");
  }
};

const sha256File = (path: string): string | null => {
  const resolvedPath = resolve(path);
  if (!existsSync(resolvedPath) || !statSync(resolvedPath).isFile()) {
    return null;
  }

  return createHash("sha256").update(readFileSync(resolvedPath)).digest("hex");
};

const insertRun = async (prisma: PrismaClient, options: LogOptions): Promise<number> =>
  prisma.$transaction(async (tx) => {
    const tool = await tx.tool.upsert({
      create: {
        category: options.category,
        name: options.tool,
      },
      update: {
        category: options.category,
      },
      where: {
        name: options.tool,
      },
    });

    const run = await tx.toolRun.create({
      data: {
        command: options.command,
        displayResolution: options.displayResolution,
        finishedAt: options.finishedAt,
        gitSha: options.gitSha,
        result: options.result ?? "unknown",
        startedAt: options.startedAt,
        timberbornVersion: options.timberbornVersion,
        toolId: tool.id,
        uiScale: options.uiScale,
      },
    });

    if (options.failureClass) {
      await tx.failure.create({
        data: {
          class: options.failureClass,
          detail: options.detail,
          reason: options.reason,
          runId: run.id,
        },
      });
    }

    await Promise.all(
      options.artifacts.map((artifact) =>
        tx.artifact.create({
          data: {
            path: artifact.path,
            runId: run.id,
            sha256: sha256File(artifact.path),
            type: artifact.type,
          },
        }),
      ),
    );

    return run.id;
  });

const main = async (): Promise<void> => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  assertOptions(options);
  ensureMigrated(options.dbPath);
  const prisma = prismaFor(options.dbPath);

  try {
    const runId = await insertRun(prisma, options);
    console.log(
      [
        "wildfire_qa_tool_run_logged",
        `run_id=${runId}`,
        `tool=${JSON.stringify(options.tool)}`,
        `result=${options.result}`,
        `failure_class=${options.failureClass ?? "none"}`,
        `db=${JSON.stringify(resolve(options.dbPath))}`,
      ].join(" "),
    );
  } finally {
    await prisma.$disconnect();
  }
};

main().catch((error: unknown) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
});
