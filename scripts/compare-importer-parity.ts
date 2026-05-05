#!/usr/bin/env bun

import { mkdirSync, readFileSync, writeFileSync } from "fs";
import { dirname } from "path";

import type { MaterialClass } from "./material-field-schema.ts";

type Options = {
  failOnMismatch: boolean;
  fixturePath: string | null;
  help: boolean;
  liveOutputPath: string | null;
  outputPath: string | null;
};

type Grid = {
  depth: number;
  height: number;
  width: number;
};

type Fixture = {
  grid: Grid;
  parityCounts: {
    resolvedCellCountsByMaterialClass: Partial<Record<MaterialClass, number>>;
    sourceCountsByMaterialClass: Partial<Record<MaterialClass, number>>;
  };
};

type ComparisonRow = {
  className: MaterialClass;
  delta: number;
  live: number;
  snapshot: number;
};

type ParityComparison = {
  dimensions: ComparisonRow[];
  matching: boolean;
  resolvedCells: ComparisonRow[];
  sourceCounts: ComparisonRow[];
};

const sourceClasses: MaterialClass[] = ["terrain", "vegetation", "crop", "tree", "building", "storage", "infrastructure", "water", "badwater"];
const resolvedClasses: MaterialClass[] = ["empty", "terrain", "vegetation", "crop", "tree", "building", "storage", "infrastructure", "water", "badwater"];

const usage = `Usage:
  bun scripts/compare-importer-parity.ts --fixture <fixture.json> --live-output <qa-readiness.txt> [--output <report.md>] [--fail-on-mismatch]

Compares snapshot fixture parityCounts with live Timberborn world_import tokens using explicit source and resolved-cell units.`;

const fail = (message: string): never => {
  throw new Error(message);
};

const requireValue = (args: string[], index: number, flag: string): string => args[index] ?? fail(`${flag} requires a value.`);

const parseArgs = (args: string[]): Options =>
  args.reduce<Options>(
    (options, arg, index) => {
      if (arg === "--help" || arg === "-h") {
        return { ...options, help: true };
      }
      if (arg === "--fail-on-mismatch") {
        return { ...options, failOnMismatch: true };
      }
      if (arg === "--fixture") {
        return { ...options, fixturePath: requireValue(args, index + 1, arg) };
      }
      if (arg.startsWith("--fixture=")) {
        return { ...options, fixturePath: arg.slice("--fixture=".length) };
      }
      if (arg === "--live-output") {
        return { ...options, liveOutputPath: requireValue(args, index + 1, arg) };
      }
      if (arg.startsWith("--live-output=")) {
        return { ...options, liveOutputPath: arg.slice("--live-output=".length) };
      }
      if (arg === "--output") {
        return { ...options, outputPath: requireValue(args, index + 1, arg) };
      }
      if (arg.startsWith("--output=")) {
        return { ...options, outputPath: arg.slice("--output=".length) };
      }
      if (arg.startsWith("-")) {
        fail(`Unknown option: ${arg}`);
      }
      return options;
    },
    { failOnMismatch: false, fixturePath: null, help: false, liveOutputPath: null, outputPath: null },
  );

const asObject = (value: unknown, context: string): Record<string, unknown> =>
  value !== null && typeof value === "object" && !Array.isArray(value) ? (value as Record<string, unknown>) : fail(`${context} is not an object.`);

const asNumber = (value: unknown, context: string): number => (typeof value === "number" ? value : fail(`${context} is not a number.`));

const parseFixture = (text: string): Fixture => {
  const root = asObject(JSON.parse(text) as unknown, "fixture root");
  const grid = asObject(root.grid, "fixture.grid");
  const parityCounts = asObject(root.parityCounts, "fixture.parityCounts");

  return {
    grid: {
      depth: asNumber(grid.depth, "fixture.grid.depth"),
      height: asNumber(grid.height, "fixture.grid.height"),
      width: asNumber(grid.width, "fixture.grid.width"),
    },
    parityCounts: {
      resolvedCellCountsByMaterialClass: asObject(
        parityCounts.resolvedCellCountsByMaterialClass,
        "fixture.parityCounts.resolvedCellCountsByMaterialClass",
      ) as Partial<Record<MaterialClass, number>>,
      sourceCountsByMaterialClass: asObject(
        parityCounts.sourceCountsByMaterialClass,
        "fixture.parityCounts.sourceCountsByMaterialClass",
      ) as Partial<Record<MaterialClass, number>>,
    },
  };
};

const parseLiveTokens = (text: string): Record<string, string> => {
  const line = text
    .split(/\r?\n/u)
    .find((candidate) => candidate.includes("wildfire_command_result")) ?? fail("Live output does not contain wildfire_command_result.");

  return line
    .trim()
    .split(/\s+/u)
    .slice(1)
    .map((token) => token.match(/^([^=]+)=(.*)$/u))
    .filter((match): match is RegExpMatchArray => match !== null)
    .reduce<Record<string, string>>((tokens, match) => ({ ...tokens, [match[1]]: match[2] }), {});
};

const tokenNumber = (tokens: Record<string, string>, key: string): number => {
  const value = tokens[key] ?? "0";
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fail(`Live token ${key} is not numeric: ${value}`);
};

const compareRows = (
  classes: MaterialClass[],
  snapshotCounts: Partial<Record<MaterialClass, number>>,
  liveCount: (className: MaterialClass) => number,
): ComparisonRow[] =>
  classes.map((className) => {
    const snapshot = snapshotCounts[className] ?? 0;
    const live = liveCount(className);
    return { className, delta: live - snapshot, live, snapshot };
  });

export const compareImporterParity = (fixtureText: string, liveOutputText: string): ParityComparison => {
  const fixture = parseFixture(fixtureText);
  const liveTokens = parseLiveTokens(liveOutputText);
  const dimensions: ComparisonRow[] = (["width", "height", "depth"] as const).map((dimension) => {
    const snapshot = fixture.grid[dimension];
    const live = tokenNumber(liveTokens, dimension);
    return { className: dimension as MaterialClass, delta: live - snapshot, live, snapshot };
  });
  const sourceCounts = compareRows(
    sourceClasses,
    fixture.parityCounts.sourceCountsByMaterialClass,
    (className) => tokenNumber(liveTokens, `world_import_${className}_sources`),
  );
  const resolvedCells = compareRows(
    resolvedClasses,
    fixture.parityCounts.resolvedCellCountsByMaterialClass,
    (className) => tokenNumber(liveTokens, `world_import_resolved_${className}_cells`),
  );
  const allRows = [...dimensions, ...sourceCounts, ...resolvedCells];

  return {
    dimensions,
    matching: allRows.every((row) => row.delta === 0),
    resolvedCells,
    sourceCounts,
  };
};

const renderTable = (rows: ComparisonRow[]): string =>
  [
    "| Unit | Snapshot | Live | Delta |",
    "| --- | ---: | ---: | ---: |",
    ...rows.map((row) => `| ${row.className} | ${row.snapshot} | ${row.live} | ${row.delta} |`),
  ].join("\n");

export const renderImporterParityReport = (comparison: ParityComparison): string =>
  [
    "# Importer Parity Comparison",
    "",
    `Status: ${comparison.matching ? "pass" : "mismatch"}`,
    "",
    "## Dimensions",
    "",
    renderTable(comparison.dimensions),
    "",
    "## Source Counts",
    "",
    renderTable(comparison.sourceCounts),
    "",
    "## Resolved Cell Counts",
    "",
    renderTable(comparison.resolvedCells),
    "",
  ].join("\n");

const main = (): void => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  const fixturePath = options.fixturePath ?? fail("--fixture is required.");
  const liveOutputPath = options.liveOutputPath ?? fail("--live-output is required.");
  const comparison = compareImporterParity(readFileSync(fixturePath, "utf8"), readFileSync(liveOutputPath, "utf8"));
  const report = renderImporterParityReport(comparison);

  if (options.outputPath) {
    mkdirSync(dirname(options.outputPath), { recursive: true });
    writeFileSync(options.outputPath, report);
  } else {
    console.log(report);
  }

  if (options.failOnMismatch && !comparison.matching) {
    fail("Importer parity mismatch.");
  }
};

if (import.meta.main) {
  main();
}
