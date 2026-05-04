#!/usr/bin/env bun

import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { inflateRawSync } from "node:zlib";

type JsonValue = null | boolean | number | string | JsonValue[] | { [key: string]: JsonValue };
type JsonObject = { [key: string]: JsonValue };

type Options = {
  help: boolean;
  latestSave: boolean;
  outputPath: string | null;
  savePath: string | null;
  scenario: string | null;
  selectedLayer: number | null;
};

type ZipEntry = {
  compressedSize: number;
  compressionMethod: number;
  data: Buffer;
  name: string;
  uncompressedSize: number;
};

type Coordinate = {
  x: number;
  y: number;
  z: number;
};

type Grid = {
  depth: number;
  height: number;
  width: number;
};

type PackedFixture = {
  formatVersion: 1;
  grid: Grid;
  packedCellValues: {
    indexOrder: "x + y * width + z * width * height";
    valueType: "uint16";
    values: number[];
  };
  scenario: string;
  seed: number;
  selectedLayer: {
    cellCount: number;
    index: number;
    offset: number;
  };
};

type ExportSummary = {
  buildingSources: number;
  cellCount: number;
  depth: number;
  entitySources: number;
  solidTerrainSources: number;
  vegetationSources: number;
  waterSources: number;
  width: number;
  height: number;
};

const emptyCell = 0xe000;
const solidTerrainHeatLoss = 6;
const vegetationFuel = 10;
const vegetationFlammability = 3;
const vegetationHeatLoss = 1;
const woodLikeFuel = 15;
const woodLikeFlammability = 1;
const woodLikeHeatLoss = 3;
const nonBurnableHeatLoss = 7;
const stockpileFuel = 8;
const stockpileFlammability = 2;
const stockpileHeatLoss = 3;

const home = process.env.HOME ?? "";
const timberbornRoot = join(home, "Library", "Application Support", "Mechanistry", "Timberborn");
const defaultOutputPath = join(timberbornRoot, "WildfireQA", "fixtures", "timberborn-map.fixture.json");
const knownVegetationTemplates = new Set([
  "Birch",
  "BlueberryBush",
  "Carrot",
  "Cattail",
  "ChestnutTree",
  "Dandelion",
  "Mangrove",
  "Oak",
  "Pine",
  "Potato",
  "Spadderdock",
  "Sunflower",
  "Wheat",
]);
const knownInertTemplates = new Set([
  "BadwaterSource",
  "BadtideDrain",
  "Blockage",
  "Path",
  "Slope",
  "UndergroundRuins",
  "WaterSource",
]);

const usage = `Usage:
  bun scripts/export-timberborn-map-fixture.ts --save <path.timber> [options]
  bun scripts/export-timberborn-map-fixture.ts --latest-save [options]

Options:
  --save <path>             Timberborn .timber archive to read.
  --latest-save             Read the newest .timber file under Timberborn's local data folder.
  --output <path>           JSON fixture path. Default: Timberborn/WildfireQA/fixtures/timberborn-map.fixture.json.
  --scenario <name>         Fixture scenario name. Default: timberborn-map-state.
  --selected-layer <index>  Selected Z layer in fixture metadata. Default: first layer with vegetation, else 0.
  --help                    Show this help.
`;

const fail = (message: string): never => {
  throw new Error(`[timberborn-map-fixture] ${message}`);
};

const requireValue = (args: string[], index: number, flag: string): string => {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    fail(`${flag} requires a value.`);
  }

  return value;
};

const parseInteger = (value: string, flag: string): number => {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 0) {
    fail(`${flag} must be a non-negative integer.`);
  }

  return parsed;
};

const parseArgs = (args: string[]): Options => {
  const options: Options = {
    help: false,
    latestSave: false,
    outputPath: null,
    savePath: null,
    scenario: null,
    selectedLayer: null,
  };
  let skipNext = false;

  args.reduce((_, arg, index) => {
    if (skipNext) {
      skipNext = false;
      return undefined;
    }

    if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--latest-save") {
      options.latestSave = true;
    } else if (arg === "--output") {
      options.outputPath = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--output=")) {
      options.outputPath = resolve(arg.slice("--output=".length));
    } else if (arg === "--save") {
      options.savePath = resolve(requireValue(args, index + 1, arg));
      skipNext = true;
    } else if (arg.startsWith("--save=")) {
      options.savePath = resolve(arg.slice("--save=".length));
    } else if (arg === "--scenario") {
      options.scenario = requireValue(args, index + 1, arg);
      skipNext = true;
    } else if (arg.startsWith("--scenario=")) {
      options.scenario = arg.slice("--scenario=".length);
    } else if (arg === "--selected-layer") {
      options.selectedLayer = parseInteger(requireValue(args, index + 1, arg), arg);
      skipNext = true;
    } else if (arg.startsWith("--selected-layer=")) {
      options.selectedLayer = parseInteger(arg.slice("--selected-layer=".length), "--selected-layer");
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

const getPath = (value: JsonValue, path: string[]): JsonValue | undefined =>
  path.reduce<JsonValue | undefined>((current, key) => asObject(current)?.[key], value);

const viewOf = (buffer: Buffer): DataView => new DataView(buffer.buffer, buffer.byteOffset, buffer.byteLength);

const readUtf8 = (buffer: Buffer, offset: number, length: number): string => buffer.subarray(offset, offset + length).toString("utf8");

const findEndOfCentralDirectory = (bytes: Buffer): number =>
  Array.from({ length: Math.max(0, bytes.length - 21) })
    .map((_, index) => bytes.length - 22 - index)
    .find((offset) => viewOf(bytes).getUint32(offset, true) === 0x06054b50) ?? fail("Could not find ZIP end-of-central-directory record.");

const readZip = (path: string): ZipEntry[] => {
  const bytes = readFileSync(path);
  const view = viewOf(bytes);
  const eocdOffset = findEndOfCentralDirectory(bytes);
  const entryCount = view.getUint16(eocdOffset + 10, true);
  const centralDirectoryOffset = view.getUint32(eocdOffset + 16, true);

  return Array.from({ length: entryCount }).reduce<{ entries: ZipEntry[]; offset: number }>(
    (state) => {
      if (view.getUint32(state.offset, true) !== 0x02014b50) {
        fail(`Invalid ZIP central directory signature at offset ${state.offset}.`);
      }

      const compressionMethod = view.getUint16(state.offset + 10, true);
      const compressedSize = view.getUint32(state.offset + 20, true);
      const uncompressedSize = view.getUint32(state.offset + 24, true);
      const fileNameLength = view.getUint16(state.offset + 28, true);
      const extraLength = view.getUint16(state.offset + 30, true);
      const commentLength = view.getUint16(state.offset + 32, true);
      const localHeaderOffset = view.getUint32(state.offset + 42, true);
      const name = readUtf8(bytes, state.offset + 46, fileNameLength);

      if (view.getUint32(localHeaderOffset, true) !== 0x04034b50) {
        fail(`Invalid ZIP local header signature for ${name}.`);
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
            : fail(`Unsupported ZIP compression method ${compressionMethod} for ${name}.`);

      if (data.length !== uncompressedSize) {
        fail(`Uncompressed size mismatch for ${name}: expected ${uncompressedSize}, got ${data.length}.`);
      }

      return {
        entries: [...state.entries, { compressedSize, compressionMethod, data, name, uncompressedSize }],
        offset: state.offset + 46 + fileNameLength + extraLength + commentLength,
      };
    },
    { entries: [], offset: centralDirectoryOffset },
  ).entries;
};

const readWorldJson = (savePath: string): JsonObject => {
  const worldEntry = readZip(savePath).find((entry) => entry.name === "world.json") ?? fail(`${savePath} does not contain world.json.`);
  return asObject(JSON.parse(worldEntry.data.toString("utf8")) as JsonValue) ?? fail("world.json root is not an object.");
};

const packCell = (fuel: number, heat: number, flammability: number, water: number, terrain: number, heatLoss: number): number =>
  ((fuel & 0b1111) << 0) |
  ((heat & 0b1111) << 4) |
  ((flammability & 0b11) << 8) |
  ((water & 0b11) << 10) |
  ((terrain & 0b1) << 12) |
  ((heatLoss & 0b111) << 13);

const terrainCell = (): number => packCell(0, 0, 0, 0, 1, solidTerrainHeatLoss);

const vegetationCell = (water: number): number => packCell(vegetationFuel, 0, vegetationFlammability, water, 1, vegetationHeatLoss);

const woodLikeCell = (water: number): number => packCell(woodLikeFuel, 0, woodLikeFlammability, water, 1, woodLikeHeatLoss);

const nonBurnableCell = (water: number): number => packCell(0, 0, 0, water, 1, nonBurnableHeatLoss);

const stockpileCell = (water: number): number => packCell(stockpileFuel, 0, stockpileFlammability, water, 1, stockpileHeatLoss);

const waterOf = (cell: number): number => (cell >> 10) & 0b11;

const withWater = (cell: number, water: number): number => (cell & ~0b0000_1100_0000_0000) | ((Math.max(waterOf(cell), water) & 0b11) << 10);

const indexOf = (grid: Grid, coordinate: Coordinate): number => coordinate.x + coordinate.y * grid.width + coordinate.z * grid.width * grid.height;

const gridOf = (world: JsonObject, terrainValues: number[]): Grid => {
  const width = asNumber(getPath(world, ["Singletons", "MapSize", "Size", "X"])) ?? fail("world.json is missing Singletons.MapSize.Size.X.");
  const height = asNumber(getPath(world, ["Singletons", "MapSize", "Size", "Y"])) ?? fail("world.json is missing Singletons.MapSize.Size.Y.");
  const layerSize = width * height;
  if (terrainValues.length % layerSize !== 0) {
    fail(`TerrainMap.Voxels.Array has ${terrainValues.length} values, not a multiple of width * height (${layerSize}).`);
  }

  return { depth: terrainValues.length / layerSize, height, width };
};

const terrainValuesOf = (world: JsonObject): number[] => {
  const terrainArray = asString(getPath(world, ["Singletons", "TerrainMap", "Voxels", "Array"])) ?? fail("world.json is missing TerrainMap.Voxels.Array.");
  return terrainArray.trim().split(/\s+/u).filter(Boolean).map(Number);
};

const coordinateOf = (entity: JsonValue): Coordinate | null => {
  const coordinates = asObject(getPath(entity, ["Components", "BlockObject", "Coordinates"]));
  const x = asNumber(coordinates?.X);
  const y = asNumber(coordinates?.Y);
  const z = asNumber(coordinates?.Z);

  return x === null || y === null || z === null ? null : { x, y, z };
};

const componentsOf = (entity: JsonObject): string[] => Object.keys(asObject(entity.Components) ?? {});

const isVegetation = (template: string, components: string[]): boolean =>
  knownVegetationTemplates.has(template) ||
  components.includes("LivingNaturalResource") ||
  components.includes("Growable") ||
  components.some((component) => component.startsWith("Yielder:"));

const isStockpile = (template: string, components: string[]): boolean =>
  template.includes("Pile") || template.includes("Warehouse") || components.includes("Inventory");

const isInert = (template: string): boolean =>
  knownInertTemplates.has(template) || template.includes("Source") || template.includes("Drain") || template.includes("Ruins");

const entityCell = (template: string, components: string[], existingWater: number): number => {
  if (isVegetation(template, components)) {
    return vegetationCell(existingWater);
  }

  if (isStockpile(template, components)) {
    return stockpileCell(existingWater);
  }

  return isInert(template) ? nonBurnableCell(existingWater) : woodLikeCell(existingWater);
};

const validCoordinate = (grid: Grid, coordinate: Coordinate): boolean =>
  coordinate.x >= 0 && coordinate.x < grid.width && coordinate.y >= 0 && coordinate.y < grid.height && coordinate.z >= 0 && coordinate.z < grid.depth;

const waterColumnsOf = (world: JsonObject): string[] => {
  const waterArray = asString(getPath(world, ["Singletons", "WaterMapNew", "WaterColumns", "Array"]));
  return waterArray?.trim().split(/\s+/u).filter(Boolean) ?? [];
};

const waterCoordinateOf = (grid: Grid, token: string, flatIndex: number): Coordinate | null => {
  if (token === "0") {
    return null;
  }

  const z = Number(token.split(":").at(-1));
  if (!Number.isFinite(z)) {
    return null;
  }

  return { x: flatIndex % grid.width, y: Math.floor(flatIndex / grid.width), z: Math.min(grid.depth - 1, Math.max(0, Math.floor(z))) };
};

const applyWaterColumns = (world: JsonObject, grid: Grid, cells: number[]): number =>
  waterColumnsOf(world).reduce((count, token, flatIndex) => {
    const coordinate = waterCoordinateOf(grid, token, flatIndex);
    if (!coordinate || !validCoordinate(grid, coordinate)) {
      return count;
    }

    const index = indexOf(grid, coordinate);
    cells[index] = withWater(cells[index] ?? emptyCell, 3);
    return count + 1;
  }, 0);

export const buildFixtureFromWorld = (
  world: JsonObject,
  scenario = "timberborn-map-state",
  selectedLayerOverride: number | null = null,
): { fixture: PackedFixture; summary: ExportSummary } => {
  const terrainValues = terrainValuesOf(world);
  const grid = gridOf(world, terrainValues);
  const cells = terrainValues.map((value) => (value > 0 ? terrainCell() : emptyCell));
  const waterSources = applyWaterColumns(world, grid, cells);
  const entities = asArray(world.Entities) ?? [];
  const entitySources = entities
    .map((entity) => ({ entity: asObject(entity), coordinate: coordinateOf(entity) }))
    .filter((source): source is { entity: JsonObject; coordinate: Coordinate } => source.entity !== null && source.coordinate !== null)
    .filter((source) => validCoordinate(grid, source.coordinate));
  const entityStats = entitySources.reduce(
    (stats, source) => {
      const template = asString(source.entity.Template) ?? "";
      const components = componentsOf(source.entity);
      const index = indexOf(grid, source.coordinate);
      const water = waterOf(cells[index] ?? emptyCell);
      cells[index] = entityCell(template, components, water);

      return {
        buildingSources: stats.buildingSources + (!isVegetation(template, components) ? 1 : 0),
        vegetationSources: stats.vegetationSources + (isVegetation(template, components) ? 1 : 0),
      };
    },
    { buildingSources: 0, vegetationSources: 0 },
  );
  const selectedLayer =
    selectedLayerOverride ??
    entitySources.map((source) => source.coordinate.z).sort((left, right) => left - right)[0] ??
    0;
  if (selectedLayer < 0 || selectedLayer >= grid.depth) {
    fail(`Selected layer ${selectedLayer} is outside grid depth ${grid.depth}.`);
  }

  return {
    fixture: {
      formatVersion: 1,
      grid,
      packedCellValues: {
        indexOrder: "x + y * width + z * width * height",
        valueType: "uint16",
        values: cells,
      },
      scenario,
      seed: 0,
      selectedLayer: {
        cellCount: grid.width * grid.height,
        index: selectedLayer,
        offset: selectedLayer * grid.width * grid.height,
      },
    },
    summary: {
      buildingSources: entityStats.buildingSources,
      cellCount: cells.length,
      depth: grid.depth,
      entitySources: entitySources.length,
      height: grid.height,
      solidTerrainSources: terrainValues.filter((value) => value > 0).length,
      vegetationSources: entityStats.vegetationSources,
      waterSources,
      width: grid.width,
    },
  };
};

const findTimberFiles = (root: string): string[] => {
  const glob = new Bun.Glob("**/*.timber");
  return Array.from(glob.scanSync({ cwd: root, absolute: true }));
};

const latestSavePath = (): string =>
  findTimberFiles(timberbornRoot)
    .map((path) => ({ modified: statSync(path).mtimeMs, path }))
    .sort((left, right) => right.modified - left.modified)[0]?.path ?? fail(`No .timber files found under ${timberbornRoot}.`);

const writeFixture = (path: string, fixture: PackedFixture): void => {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, `${JSON.stringify(fixture, null, 2)}\n`);
};

const main = (): void => {
  const options = parseArgs(Bun.argv.slice(2));
  if (options.help) {
    console.log(usage);
    return;
  }

  const savePath: string = options.latestSave
    ? latestSavePath()
    : options.savePath === null
      ? fail("Pass --save <path.timber> or --latest-save.")
      : options.savePath;
  if (!existsSync(savePath)) {
    fail(`Save archive does not exist: ${savePath}`);
  }

  const outputPath = options.outputPath ?? defaultOutputPath;
  const { fixture, summary } = buildFixtureFromWorld(readWorldJson(savePath), options.scenario ?? "timberborn-map-state", options.selectedLayer);
  writeFixture(outputPath, fixture);

  console.log(`[timberborn-map-fixture] read ${savePath}`);
  console.log(`[timberborn-map-fixture] wrote ${outputPath}`);
  console.log(
    `[timberborn-map-fixture] grid=${summary.width}x${summary.height}x${summary.depth} cells=${summary.cellCount} terrain=${summary.solidTerrainSources} vegetation=${summary.vegetationSources} buildings=${summary.buildingSources} water=${summary.waterSources}`,
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
