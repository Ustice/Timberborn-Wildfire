#!/usr/bin/env bun

import { existsSync, mkdirSync, readFileSync, writeFileSync } from "fs";
import { dirname, resolve } from "path";

type JsonValue = null | boolean | number | string | JsonValue[] | { [key: string]: JsonValue };
type JsonObject = { [key: string]: JsonValue };

type Options = {
  help: boolean;
  inputPath: string | null;
  metadataPath: string | null;
  outputPath: string | null;
  source: "auto" | "fixture" | "capture";
};

type Grid = {
  depth: number;
  height: number;
  width: number;
};

type BinarySnapshot = {
  cellCount: number;
  cells: number[];
  grid: Grid;
  scenario: string;
  seed: number;
  source: "fixture" | "capture";
  tickCount: number | null;
};

const usage = `Usage:
  bun scripts/convert-wildfire-snapshot-binary.ts --input <snapshot.json> --output <cells.bin> [options]

Options:
  --input <path>       JSON fixture or shader capture snapshot.
  --output <path>      Raw packed-cell binary output. Cells are uint16 little-endian.
  --source <kind>      auto, fixture, or capture. Default: auto.
  --metadata <path>    Optional JSON metadata sidecar with grid/scenario/cell-count.
  --help               Show this help.
`;

const fail = (message: string): never => {
  throw new Error(`[wildfire-snapshot-binary] ${message}`);
};

const requireValue = (args: string[], index: number, flag: string): string => {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    fail(`${flag} requires a value.`);
  }

  return value;
};

const parseSource = (value: string): Options["source"] => {
  if (value === "auto" || value === "fixture" || value === "capture") {
    return value;
  }

  return fail(`Invalid --source ${value}. Expected auto, fixture, or capture.`);
};

const parseArgs = (args: string[]): Options => {
  const options: Options = {
    help: false,
    inputPath: null,
    metadataPath: null,
    outputPath: null,
    source: "auto",
  };
  let skipNext = false;

  args.reduce((_, arg, index) => {
    if (skipNext) {
      skipNext = false;
      return undefined;
    }

    if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--input") {
      options.inputPath = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--input=")) {
      options.inputPath = resolve(arg.slice("--input=".length));
    } else if (arg === "--metadata") {
      options.metadataPath = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--metadata=")) {
      options.metadataPath = resolve(arg.slice("--metadata=".length));
    } else if (arg === "--output") {
      options.outputPath = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--output=")) {
      options.outputPath = resolve(arg.slice("--output=".length));
    } else if (arg === "--source") {
      options.source = parseSource(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--source=")) {
      options.source = parseSource(arg.slice("--source=".length));
    } else {
      fail(`Unknown argument: ${arg}`);
    }

    return undefined;
  }, undefined);

  return options;
};

const asObject = (value: JsonValue | undefined): JsonObject | null =>
  value !== null && typeof value === "object" && !Array.isArray(value) ? value : null;

const asArray = (value: JsonValue | undefined): JsonValue[] | null => (Array.isArray(value) ? value : null);

const asNumber = (value: JsonValue | undefined): number | null => (typeof value === "number" ? value : null);

const asString = (value: JsonValue | undefined): string | null => (typeof value === "string" ? value : null);

const readJson = (path: string): JsonObject =>
  asObject(JSON.parse(readFileSync(path, "utf8")) as JsonValue) ?? fail(`${path} root is not a JSON object.`);

const readGrid = (root: JsonObject): Grid => {
  const grid = asObject(root.grid) ?? fail("Snapshot is missing grid.");
  const width = asNumber(grid.width) ?? fail("Snapshot grid is missing numeric width.");
  const height = asNumber(grid.height) ?? fail("Snapshot grid is missing numeric height.");
  const depth = asNumber(grid.depth) ?? fail("Snapshot grid is missing numeric depth.");
  const dimensions = { depth, height, width };
  if (!Object.values(dimensions).every((value) => Number.isInteger(value) && value > 0)) {
    fail(`Snapshot grid must contain positive integer dimensions, got ${JSON.stringify(dimensions)}.`);
  }

  return dimensions;
};

const readCellValues = (root: JsonObject, source: Options["source"]): { cells: number[]; source: "fixture" | "capture" } => {
  const fixtureValues = asArray(asObject(root.packedCellValues)?.values);
  const captureValues = asArray(root.finalPackedCells);
  const selectedSource =
    source === "fixture" || (source === "auto" && fixtureValues !== null)
      ? "fixture"
      : source === "capture" || (source === "auto" && captureValues !== null)
        ? "capture"
        : fail("Snapshot does not contain packedCellValues.values or finalPackedCells.");
  const cellValues: JsonValue[] =
    selectedSource === "fixture"
      ? fixtureValues ?? fail("Snapshot does not contain packedCellValues.values.")
      : captureValues ?? fail("Snapshot does not contain finalPackedCells.");
  return {
    cells: cellValues.map((value, index) => {
      const cell = asNumber(value) ?? fail(`Packed cell at index ${index} must be a uint16 integer.`);
      if (!Number.isInteger(cell) || cell < 0 || cell > 0xffff) {
        fail(`Packed cell at index ${index} must be a uint16 integer.`);
      }

      return cell;
    }),
    source: selectedSource,
  };
};

export const readBinarySnapshot = (root: JsonObject, source: Options["source"] = "auto"): BinarySnapshot => {
  const grid = readGrid(root);
  const { cells, source: selectedSource } = readCellValues(root, source);
  const expectedCellCount = grid.width * grid.height * grid.depth;
  if (cells.length !== expectedCellCount) {
    fail(`Snapshot has ${cells.length} packed cells, expected ${expectedCellCount} from grid ${grid.width}x${grid.height}x${grid.depth}.`);
  }

  return {
    cellCount: cells.length,
    cells,
    grid,
    scenario: asString(root.scenario) ?? "wildfire-snapshot",
    seed: asNumber(root.seed) ?? 0,
    source: selectedSource,
    tickCount: selectedSource === "capture" ? asNumber(root.tickCount) : null,
  };
};

export const cellsToUInt16LittleEndian = (cells: number[]): Buffer =>
  cells.reduce((buffer, cell, index) => {
    buffer.writeUInt16LE(cell, index * 2);
    return buffer;
  }, Buffer.alloc(cells.length * 2));

const writeBinary = (path: string, snapshot: BinarySnapshot): void => {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, cellsToUInt16LittleEndian(snapshot.cells));
};

const writeMetadata = (path: string, snapshot: BinarySnapshot, binaryPath: string): void => {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(
    path,
    `${JSON.stringify(
      {
        binaryPath,
        byteLength: snapshot.cellCount * 2,
        cellCount: snapshot.cellCount,
        cellType: "uint16-le",
        grid: snapshot.grid,
        indexOrder: "x + y * width + z * width * height",
        scenario: snapshot.scenario,
        seed: snapshot.seed,
        source: snapshot.source,
        tickCount: snapshot.tickCount,
      },
      null,
      2,
    )}\n`,
  );
};

const main = (): void => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  const inputPath = options.inputPath ?? fail("Pass --input <snapshot.json>.");
  const outputPath = options.outputPath ?? fail("Pass --output <cells.bin>.");
  if (!existsSync(inputPath)) {
    fail(`Input JSON does not exist: ${inputPath}`);
  }

  const snapshot = readBinarySnapshot(readJson(inputPath), options.source);
  writeBinary(outputPath, snapshot);
  if (options.metadataPath) {
    writeMetadata(options.metadataPath, snapshot, outputPath);
  }

  console.log(`[wildfire-snapshot-binary] read ${inputPath}`);
  console.log(`[wildfire-snapshot-binary] wrote ${outputPath}`);
  console.log(
    `[wildfire-snapshot-binary] source=${snapshot.source} grid=${snapshot.grid.width}x${snapshot.grid.height}x${snapshot.grid.depth} cells=${snapshot.cellCount} bytes=${snapshot.cellCount * 2}`,
  );
};

if (import.meta.main) {
  try {
    main();
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  }
}
