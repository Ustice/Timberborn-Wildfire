#!/usr/bin/env bun

import { PrismaLibSql } from "@prisma/adapter-libsql";
import { existsSync } from "fs";
import { resolve } from "path";
import { PrismaClient } from "../prisma/generated/client/client";

type ReportOptions = {
  dbPath: string;
  days: number;
  format: "json" | "text";
  help: boolean;
  tool: string | null;
};

type ReportRow = {
  blocked_runs: number;
  environment_failures: number;
  failed_runs: number;
  last_run_at: string | null;
  passed_runs: number;
  product_failures: number;
  runs: number;
  test_design_failures: number;
  tool: string;
  tool_failures: number;
  unknown_failures: number;
  unknown_runs: number;
};

const defaultDbPath = "qa/tool-runs.sqlite";

const usage = `Usage:
  bun scripts/qa-tool-report.ts [options]

Options:
  --db <path>               SQLite DB path. Default: qa/tool-runs.sqlite.
  --days <count>            Include runs started within this many days. Default: 30.
  --tool <name>             Report only one tool.
  --format <text|json>      Output format. Default: text.
  --help                    Show this help.

Examples:
  bun scripts/qa-tool-report.ts
  bun scripts/qa-tool-report.ts --days=7 --format=json
`;

const fail = (message: string): never => {
  throw new Error(`[qa-tool-report] ${message}`);
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

const parsePositiveInteger = (value: string, flag: string): number => {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    fail(`${flag} requires a positive integer.`);
  }

  return parsed;
};

const parseFormat = (value: string): "json" | "text" => {
  if (value !== "json" && value !== "text") {
    fail(`Invalid format: ${value}`);
  }

  return value === "json" ? "json" : "text";
};

const parseArgs = (args: string[]): ReportOptions => {
  const options: ReportOptions = {
    days: 30,
    dbPath: defaultDbPath,
    format: "text",
    help: false,
    tool: null,
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
    } else if (arg === "--days") {
      options.days = parsePositiveInteger(requireValue(args, index + 1, arg), arg);
      skipNext = true;
    } else if (arg.startsWith("--days=")) {
      options.days = parsePositiveInteger(arg.slice("--days=".length), "--days");
    } else if (arg === "--tool") {
      options.tool = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--tool=")) {
      options.tool = arg.slice("--tool=".length);
    } else if (arg === "--format") {
      options.format = parseFormat(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--format=")) {
      options.format = parseFormat(arg.slice("--format=".length));
    } else {
      fail(`Unknown argument: ${arg}`);
    }

    return undefined;
  }, undefined);

  return options;
};

const rate = (part: number, total: number): string => `${(total === 0 ? 0 : (part / total) * 100).toFixed(1)}%`;

const loadReport = async (prisma: PrismaClient, options: ReportOptions): Promise<ReportRow[]> => {
  const since = new Date(Date.now() - options.days * 24 * 60 * 60 * 1000);
  const runs = await prisma.toolRun.findMany({
    include: {
      failures: true,
      tool: true,
    },
    where: {
      startedAt: {
        gte: since,
      },
      tool: options.tool
        ? {
            name: options.tool,
          }
        : undefined,
    },
  });

  const rowsByTool = runs.reduce<Record<string, ReportRow>>((rows, run) => {
    const row = rows[run.tool.name] ?? {
      blocked_runs: 0,
      environment_failures: 0,
      failed_runs: 0,
      last_run_at: null,
      passed_runs: 0,
      product_failures: 0,
      runs: 0,
      test_design_failures: 0,
      tool: run.tool.name,
      tool_failures: 0,
      unknown_failures: 0,
      unknown_runs: 0,
    };
    const failureClasses = new Set(run.failures.map((failure) => failure.class));
    const startedAt = run.startedAt.toISOString();

    return {
      ...rows,
      [run.tool.name]: {
        ...row,
        blocked_runs: row.blocked_runs + (run.result === "blocked" ? 1 : 0),
        environment_failures: row.environment_failures + (failureClasses.has("environment_failure") ? 1 : 0),
        failed_runs: row.failed_runs + (run.result === "fail" ? 1 : 0),
        last_run_at: row.last_run_at && row.last_run_at > startedAt ? row.last_run_at : startedAt,
        passed_runs: row.passed_runs + (run.result === "pass" ? 1 : 0),
        product_failures: row.product_failures + (failureClasses.has("product_failure") ? 1 : 0),
        runs: row.runs + 1,
        test_design_failures: row.test_design_failures + (failureClasses.has("test_design_failure") ? 1 : 0),
        tool_failures: row.tool_failures + (failureClasses.has("tool_failure") ? 1 : 0),
        unknown_failures: row.unknown_failures + (failureClasses.has("unknown") ? 1 : 0),
        unknown_runs: row.unknown_runs + (run.result === "unknown" ? 1 : 0),
      },
    };
  }, {});

  return Object.values(rowsByTool).sort(
    (left, right) =>
      right.tool_failures - left.tool_failures ||
      right.failed_runs - left.failed_runs ||
      right.runs - left.runs ||
      left.tool.localeCompare(right.tool),
  );
};

const formatText = (rows: ReportRow[], options: ReportOptions): string => {
  if (rows.length === 0) {
    return [
      "wildfire_qa_tool_report",
      `db=${JSON.stringify(resolve(options.dbPath))}`,
      `days=${options.days}`,
      "runs=0",
    ].join(" ");
  }

  const header = [
    "Tool",
    "Runs",
    "Pass",
    "Tool Fail",
    "Env Fail",
    "Product Fail",
    "Test Design",
    "Unknown",
    "Last Run",
  ].join(" | ");
  const divider = [
    "---",
    "---:",
    "---:",
    "---:",
    "---:",
    "---:",
    "---:",
    "---:",
    "---",
  ].join(" | ");
  const body = rows
    .map((row) =>
      [
        row.tool,
        row.runs,
        rate(row.passed_runs, row.runs),
        rate(row.tool_failures, row.runs),
        rate(row.environment_failures, row.runs),
        rate(row.product_failures, row.runs),
        rate(row.test_design_failures, row.runs),
        rate(row.unknown_failures + row.unknown_runs, row.runs),
        row.last_run_at ?? "never",
      ].join(" | "),
    )
    .join("\n");

  return `${header}\n${divider}\n${body}`;
};

const main = async (): Promise<void> => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  if (!existsSync(options.dbPath)) {
    ensureMigrated(options.dbPath);
  }

  const prisma = prismaFor(options.dbPath);
  try {
    const rows = await loadReport(prisma, options);
    if (options.format === "json") {
      console.log(JSON.stringify({ days: options.days, rows }, null, 2));
      return;
    }

    console.log(formatText(rows, options));
  } finally {
    await prisma.$disconnect();
  }
};

main().catch((error: unknown) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
});
