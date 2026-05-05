#!/usr/bin/env bun

import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { inflateRawSync } from "node:zlib";

import { lookupMaterialProfile, type MaterialClass, type MaterialFieldProfile } from "./material-field-schema.ts";

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
  companionFieldValues: {
    indexOrder: "x + y * width + z * width * height";
    packedStateValues: number[];
    statePacking: "material:0-7,burnCapacity:8-11,burnHistory:12-15,ashStrength:16-19,ashQuality:20-21,contamination:22-24";
    targetIds: number[];
    valueType: "uint32";
  };
  formatVersion: 1;
  grid: Grid;
  parityCounts: {
    entityObjectCount: number;
    entitySourceCount: number;
    resolvedCellCountsByMaterialClass: Record<MaterialClass, number>;
    sourceCountsByMaterialClass: Record<MaterialClass, number>;
    terrainSurfaceSourceCount: number;
    terrainSolidVoxelCount: number;
    waterColumnSourceCount: number;
  };
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
  badwaterSources: number;
  buildingSources: number;
  cellCount: number;
  cropSources: number;
  depth: number;
  entitySources: number;
  entityObjects: number;
  infrastructureSources: number;
  storageSources: number;
  terrainSurfaceSources: number;
  solidTerrainSources: number;
  treeSources: number;
  unresolvedTemplateSources: number;
  vegetationSources: number;
  waterSources: number;
  width: number;
  height: number;
};

type BlueprintFootprint = {
  blocks?: BlueprintFootprintBlock[];
  occupyAllBelow: boolean;
  sizeX: number;
  sizeY: number;
  sizeZ: number;
};

type BlueprintFootprintBlock = {
  occupyAllBelow: boolean;
  x: number;
  y: number;
  z: number;
};

const emptyCell = 0xe000;
const ashQualityValues = new Map([
  ["none", 0],
  ["fertile", 1],
  ["spent", 2],
  ["tainted", 3],
]);
const contaminationBehaviorValues = new Map([
  ["none", 0],
  ["taint-if-source-contaminated", 1],
  ["tainted-source", 2],
  ["suppresses-without-cleaning", 3],
  ["fail-closed", 4],
]);
const materialClassValues = new Map<MaterialClass, number>([
  ["empty", 0],
  ["terrain", 1],
  ["vegetation", 2],
  ["crop", 3],
  ["tree", 4],
  ["building", 5],
  ["storage", 6],
  ["infrastructure", 7],
  ["water", 8],
  ["badwater", 9],
  ["unknown", 10],
]);
const materialClasses: MaterialClass[] = [
  "empty",
  "terrain",
  "vegetation",
  "crop",
  "tree",
  "building",
  "storage",
  "infrastructure",
  "water",
  "badwater",
  "unknown",
];
const materialClassByValue = new Map<number, MaterialClass>(Array.from(materialClassValues.entries()).map(([key, value]) => [value, key]));

const home = process.env.HOME ?? "";
const timberbornRoot = join(home, "Library", "Application Support", "Mechanistry", "Timberborn");
const defaultOutputPath = join(timberbornRoot, "WildfireQA", "fixtures", "timberborn-map.fixture.json");
const defaultBlueprintRoot = join(
  home,
  "Library",
  "Application Support",
  "Steam",
  "steamapps",
  "common",
  "Timberborn",
  "Timberborn.app",
  "Contents",
  "Resources",
  "Data",
  "StreamingAssets",
  "Modding",
  "Blueprints",
);
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
const knownTreeTemplates = new Set([
  "Birch",
  "ChestnutTree",
  "Mangrove",
  "Maple",
  "Oak",
  "Pine",
]);
const knownCropTemplates = new Set([
  "Canola",
  "Carrot",
  "Cassava",
  "Cattail",
  "CoffeeBush",
  "Corn",
  "Dandelion",
  "Eggplant",
  "Kohlrabi",
  "Potato",
  "Soybean",
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
const knownInfrastructureTemplates = new Set([
  "Path",
  "Slope",
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

const cellFromProfile = (profile: MaterialFieldProfile, water: number): number =>
  packCell(profile.fuel, 0, profile.flammability, Math.max(profile.water, water), profile.terrain, profile.heatLoss);

const terrainCell = (): number => cellFromProfile(lookupMaterialProfile("terrain"), 0);

const companionStateFromProfile = (profile: MaterialFieldProfile): number =>
  (materialClassValues.get(profile.materialClass) ?? materialClassValues.get("unknown")!) |
  ((Math.min(15, Math.max(0, profile.burnCapacity)) & 0xf) << 8) |
  ((ashQualityValues.get(profile.ashQuality) ?? 0) << 20) |
  ((contaminationBehaviorValues.get(profile.contaminationBehavior) ?? 0) << 22);

const companionStateOf = (materialClass: MaterialClass): number => companionStateFromProfile(lookupMaterialProfile(materialClass));

const emptyMaterialCounts = (): Record<MaterialClass, number> =>
  materialClasses.reduce<Record<MaterialClass, number>>(
    (counts, materialClass) => ({ ...counts, [materialClass]: 0 }),
    {} as Record<MaterialClass, number>,
  );

const incrementMaterialCount = (
  counts: Record<MaterialClass, number>,
  materialClass: MaterialClass,
  amount = 1,
): Record<MaterialClass, number> => ({
  ...counts,
  [materialClass]: counts[materialClass] + amount,
});

const materialClassFromCompanionState = (state: number): MaterialClass => materialClassByValue.get(state & 0xff) ?? "unknown";

const resolvedCellCountsByMaterialClass = (companionStates: number[]): Record<MaterialClass, number> =>
  companionStates.reduce<Record<MaterialClass, number>>(
    (counts, state) => incrementMaterialCount(counts, materialClassFromCompanionState(state)),
    emptyMaterialCounts(),
  );

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

const orientationOf = (entity: JsonObject): string => asString(getPath(entity, ["Components", "BlockObject", "Orientation"])) ?? "";

const isVegetation = (template: string, components: string[]): boolean =>
  knownVegetationTemplates.has(template) ||
  components.includes("LivingNaturalResource") ||
  components.includes("Growable") ||
  components.some((component) => component.startsWith("Yielder:"));

const isStockpile = (template: string, components: string[]): boolean =>
  template.includes("Pile") ||
  template.includes("Tank") ||
  template.includes("Warehouse") ||
  components.some((component) => component.startsWith("Inventory:Stockpile"));

const isBadwater = (template: string): boolean => template.includes("Badwater");

const isWaterSource = (template: string): boolean => template.includes("WaterSource") && !isBadwater(template);

const isInfrastructure = (template: string): boolean =>
  knownInfrastructureTemplates.has(template) ||
  template.includes("Path") ||
  template.includes("Platform") ||
  template.includes("Bridge") ||
  template.includes("Stair") ||
  template.includes("Fence");

const isInert = (template: string): boolean =>
  knownInertTemplates.has(template) || template.includes("Source") || template.includes("Drain") || template.includes("Ruin");

const materialClassOfEntity = (template: string, components: string[]): MaterialClass => {
  if (isBadwater(template)) {
    return "badwater";
  }

  if (isWaterSource(template)) {
    return "water";
  }

  if (knownTreeTemplates.has(template)) {
    return "tree";
  }

  if (knownCropTemplates.has(template)) {
    return "crop";
  }

  if (isInfrastructure(template)) {
    return "infrastructure";
  }

  if (isInert(template)) {
    return "terrain";
  }

  if (isVegetation(template, components)) {
    return "vegetation";
  }

  if (isStockpile(template, components)) {
    return "storage";
  }

  return "building";
};

const entityCell = (template: string, components: string[], existingWater: number): number =>
  cellFromProfile(lookupMaterialProfile(materialClassOfEntity(template, components)), existingWater);

const validCoordinate = (grid: Grid, coordinate: Coordinate): boolean =>
  coordinate.x >= 0 && coordinate.x < grid.width && coordinate.y >= 0 && coordinate.y < grid.height && coordinate.z >= 0 && coordinate.z < grid.depth;

const terrainSurfaceCoordinatesOf = (grid: Grid, terrainValues: number[]): Coordinate[] =>
  Array.from({ length: grid.width }, (_, x) => x).flatMap((x) =>
    Array.from({ length: grid.height }, (_, y) => y).flatMap((y) =>
      Array.from({ length: grid.depth }, (_, z) => ({ x, y, z })).filter((coordinate) => {
        const index = indexOf(grid, coordinate);
        const aboveIndex = coordinate.z + 1 >= grid.depth ? -1 : indexOf(grid, { ...coordinate, z: coordinate.z + 1 });
        return terrainValues[index] > 0 && (aboveIndex < 0 || terrainValues[aboveIndex] <= 0);
      }),
    ),
  );

const footprintCoordinatesOf = (
  origin: Coordinate,
  footprint: BlueprintFootprint | undefined,
  orientation: string,
): Coordinate[] => {
  const size = footprint ?? { occupyAllBelow: false, sizeX: 1, sizeY: 1, sizeZ: 1 };
  const offsets = size.blocks ?? Array.from({ length: size.sizeX }, (_, x) => x).flatMap((x) =>
    Array.from({ length: size.sizeY }, (_, y) => y).flatMap((y) =>
      Array.from({ length: size.sizeZ }, (_, z) => ({ occupyAllBelow: size.occupyAllBelow, x, y, z })),
    ),
  );

  return offsets.flatMap((offset) => {
    const coordinate =
      orientation === "Cw90" || orientation === "Cw270"
        ? { x: origin.x + offset.y, y: origin.y + offset.x, z: origin.z + offset.z }
        : { x: origin.x + offset.x, y: origin.y + offset.y, z: origin.z + offset.z };

    if (!offset.occupyAllBelow) {
      return [coordinate];
    }

    return Array.from({ length: coordinate.z + 1 }, (_, z) => ({ ...coordinate, z }));
  });
};

const blueprintOccupiesAllBelow = (blueprint: JsonObject): boolean =>
  asArray(getPath(blueprint, ["BlockObjectSpec", "Blocks"]))?.some((block) => asObject(block)?.OccupyAllBelow === true) ?? false;

const blueprintOccupiedBlocksOf = (blueprint: JsonObject, sizeX: number, sizeY: number): BlueprintFootprintBlock[] | null => {
  const blocks = asArray(getPath(blueprint, ["BlockObjectSpec", "Blocks"]));
  if (!blocks) {
    return null;
  }

  return blocks
    .map(asObject)
    .flatMap((block, index) => {
      if (!block || asString(block.Occupations) === "None") {
        return [];
      }

      return [{
        occupyAllBelow: block.OccupyAllBelow === true,
        x: index % sizeX,
        y: Math.floor(index / sizeX) % sizeY,
        z: Math.floor(index / (sizeX * sizeY)),
      }];
    });
};

const blueprintFootprintOf = (blueprint: JsonObject): BlueprintFootprint | null => {
  const size = asObject(getPath(blueprint, ["BlockObjectSpec", "Size"]));
  const sizeX = asNumber(size?.X);
  const sizeY = asNumber(size?.Y);
  const sizeZ = asNumber(size?.Z);

  return sizeX === null || sizeY === null || sizeZ === null
    ? null
    : {
        blocks: blueprintOccupiedBlocksOf(blueprint, sizeX, sizeY) ?? undefined,
        occupyAllBelow: blueprintOccupiesAllBelow(blueprint),
        sizeX,
        sizeY,
        sizeZ,
      };
};

const blueprintNamesOf = (path: string, blueprint: JsonObject): string[] => {
  const templateName = asString(getPath(blueprint, ["TemplateSpec", "TemplateName"]));
  const backwardCompatibleNames = asArray(getPath(blueprint, ["TemplateSpec", "BackwardCompatibleTemplateNames"]))
    ?.map(asString)
    .filter((name): name is string => name !== null) ?? [];
  const fallbackName = path.split("/").at(-1)?.replace(/\.blueprint\.json$/u, "") ?? "";

  return [templateName, fallbackName, ...backwardCompatibleNames].filter((name): name is string => Boolean(name));
};

const loadBlueprintFootprints = (root = defaultBlueprintRoot): Record<string, BlueprintFootprint> => {
  if (!existsSync(root)) {
    return {};
  }

  const glob = new Bun.Glob("**/*.blueprint.json");
  return Array.from(glob.scanSync({ cwd: root, absolute: true })).reduce<Record<string, BlueprintFootprint>>((footprints, path) => {
    const blueprint = asObject(JSON.parse(readFileSync(path, "utf8").replace(/^\uFEFF/u, "")) as JsonValue);
    const footprint = blueprint ? blueprintFootprintOf(blueprint) : null;
    if (!blueprint || !footprint) {
      return footprints;
    }

    return blueprintNamesOf(path, blueprint).reduce<Record<string, BlueprintFootprint>>(
      (nextFootprints, name) => ({ ...nextFootprints, [name]: footprint }),
      footprints,
    );
  }, {});
};

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
  blueprintFootprintsByTemplate: Record<string, BlueprintFootprint> = loadBlueprintFootprints(),
): { fixture: PackedFixture; summary: ExportSummary } => {
  const terrainValues = terrainValuesOf(world);
  const grid = gridOf(world, terrainValues);
  const terrainSurfaceCoordinates = terrainSurfaceCoordinatesOf(grid, terrainValues);
  const terrainSurfaceIndices = new Set(terrainSurfaceCoordinates.map((coordinate) => indexOf(grid, coordinate)));
  const cells = terrainValues.map((_, index) => (terrainSurfaceIndices.has(index) ? terrainCell() : emptyCell));
  const targetIds = Array.from({ length: cells.length }, () => 0);
  const companionStates = terrainValues.map((_, index) => companionStateOf(terrainSurfaceIndices.has(index) ? "terrain" : "empty"));
  const waterColumnSources = applyWaterColumns(world, grid, cells);
  waterColumnsOf(world).forEach((token, flatIndex) => {
    const coordinate = waterCoordinateOf(grid, token, flatIndex);
    if (!coordinate || !validCoordinate(grid, coordinate)) {
      return;
    }

    const index = indexOf(grid, coordinate);
    if (cells[index] === emptyCell) {
      companionStates[index] = companionStateOf("water");
    }
  });
  const entities = asArray(world.Entities) ?? [];
  const entitySources = entities
    .map((entity, entityIndex) => ({ entity: asObject(entity), entityIndex, coordinate: coordinateOf(entity) }))
    .filter(
      (source): source is { entity: JsonObject; entityIndex: number; coordinate: Coordinate } =>
        source.entity !== null && source.coordinate !== null,
    )
    .map((source) => {
      const template = asString(source.entity.Template) ?? "";
      const components = componentsOf(source.entity);
      const materialClass = materialClassOfEntity(template, components);
      const footprint = footprintCoordinatesOf(
        source.coordinate,
        blueprintFootprintsByTemplate[template],
        orientationOf(source.entity),
      ).filter((coordinate) => validCoordinate(grid, coordinate));

      return { ...source, components, footprint, materialClass, template };
    })
    .filter((source) => source.materialClass !== "terrain" && source.footprint.length > 0);
  const entityStats = entitySources.reduce(
    (stats, source) => {
      source.footprint.forEach((coordinate) => {
        const index = indexOf(grid, coordinate);
        const water = waterOf(cells[index] ?? emptyCell);
        cells[index] = entityCell(source.template, source.components, water);
        targetIds[index] = source.entityIndex + 1;
        companionStates[index] = companionStateOf(source.materialClass);
      });

      return {
        badwaterSources: stats.badwaterSources + (source.materialClass === "badwater" ? source.footprint.length : 0),
        buildingSources: stats.buildingSources + (source.materialClass === "building" ? source.footprint.length : 0),
        cropSources: stats.cropSources + (source.materialClass === "crop" ? source.footprint.length : 0),
        infrastructureSources: stats.infrastructureSources + (source.materialClass === "infrastructure" ? source.footprint.length : 0),
        storageSources: stats.storageSources + (source.materialClass === "storage" ? source.footprint.length : 0),
        treeSources: stats.treeSources + (source.materialClass === "tree" ? source.footprint.length : 0),
        unresolvedTemplateSources: stats.unresolvedTemplateSources + (source.materialClass === "building" && source.template.length === 0 ? 1 : 0),
        vegetationSources: stats.vegetationSources + (source.materialClass === "vegetation" ? source.footprint.length : 0),
        waterSources: stats.waterSources + (source.materialClass === "water" ? source.footprint.length : 0),
      };
    },
    {
      badwaterSources: 0,
      buildingSources: 0,
      cropSources: 0,
      infrastructureSources: 0,
      storageSources: 0,
      treeSources: 0,
      unresolvedTemplateSources: 0,
      vegetationSources: 0,
      waterSources: 0,
    },
  );
  const selectedLayer =
    selectedLayerOverride ??
    entitySources.map((source) => source.coordinate.z).sort((left, right) => left - right)[0] ??
    0;
  if (selectedLayer < 0 || selectedLayer >= grid.depth) {
    fail(`Selected layer ${selectedLayer} is outside grid depth ${grid.depth}.`);
  }
  const solidTerrainSources = terrainValues.filter((value) => value > 0).length;
  const terrainSurfaceSources = terrainSurfaceCoordinates.length;
  const sourceCountsByMaterialClass: Record<MaterialClass, number> = {
    ...emptyMaterialCounts(),
    badwater: entityStats.badwaterSources,
    building: entityStats.buildingSources,
    crop: entityStats.cropSources,
    infrastructure: entityStats.infrastructureSources,
    storage: entityStats.storageSources,
    terrain: terrainSurfaceSources,
    tree: entityStats.treeSources,
    vegetation: entityStats.vegetationSources,
    water: entityStats.waterSources,
  };

  return {
    fixture: {
      companionFieldValues: {
        indexOrder: "x + y * width + z * width * height",
        packedStateValues: companionStates,
        statePacking: "material:0-7,burnCapacity:8-11,burnHistory:12-15,ashStrength:16-19,ashQuality:20-21,contamination:22-24",
        targetIds,
        valueType: "uint32",
      },
      formatVersion: 1,
      grid,
      parityCounts: {
        entityObjectCount: entitySources.length,
        entitySourceCount: entitySources.reduce((count, source) => count + source.footprint.length, 0),
        resolvedCellCountsByMaterialClass: resolvedCellCountsByMaterialClass(companionStates),
        sourceCountsByMaterialClass,
        terrainSurfaceSourceCount: terrainSurfaceSources,
        terrainSolidVoxelCount: solidTerrainSources,
        waterColumnSourceCount: waterColumnSources,
      },
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
      badwaterSources: entityStats.badwaterSources,
      buildingSources: entityStats.buildingSources,
      cellCount: cells.length,
      cropSources: entityStats.cropSources,
      depth: grid.depth,
      entityObjects: entitySources.length,
      entitySources: entitySources.reduce((count, source) => count + source.footprint.length, 0),
      height: grid.height,
      infrastructureSources: entityStats.infrastructureSources,
      solidTerrainSources,
      terrainSurfaceSources,
      storageSources: entityStats.storageSources,
      treeSources: entityStats.treeSources,
      unresolvedTemplateSources: entityStats.unresolvedTemplateSources,
      vegetationSources: entityStats.vegetationSources,
      waterSources: entityStats.waterSources,
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
    `[timberborn-map-fixture] grid=${summary.width}x${summary.height}x${summary.depth} cells=${summary.cellCount} terrain=${summary.terrainSurfaceSources} terrain_solid_voxels=${summary.solidTerrainSources} trees=${summary.treeSources} crops=${summary.cropSources} vegetation=${summary.vegetationSources} buildings=${summary.buildingSources} storage=${summary.storageSources} infrastructure=${summary.infrastructureSources} water=${summary.waterSources} badwater=${summary.badwaterSources} unresolved_templates=${summary.unresolvedTemplateSources}`,
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
