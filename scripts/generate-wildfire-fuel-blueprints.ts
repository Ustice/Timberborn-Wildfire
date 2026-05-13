#!/usr/bin/env bun

import {
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "fs";
import { dirname, join, resolve } from "path";

type CatalogType = "good" | "tree" | "crop" | "bush" | "building";

type CatalogRow = {
  id: string;
  type: CatalogType;
  path: string;
  fuel: number;
  destructionThreshold: number;
  flammability: number;
  explosive: boolean;
  contaminated: boolean;
};

type GeneratedBlueprint = {
  relativePath: string;
  content: string;
};

type Options = {
  catalogPath: string;
  check: boolean;
  outputRoot: string;
};

const repoRoot = resolve(import.meta.dir, "..");
const defaultCatalogPath = join(repoRoot, "wildfire-fuel-catalog.csv");
const defaultOutputRoot = join(repoRoot, "src", "Wildfire.Timberborn", "Blueprints");
const requiredHeaders = [
  "Id",
  "Type",
  "Path",
  "Fuel",
  "Destruction Threshold",
  "Flammability",
  "Explosive",
  "Contaminated",
];
const specNameByType: Record<CatalogType, string> = {
  good: "WildfireFuelSpec",
  tree: "WildfireBurnableSpec",
  crop: "WildfireBurnableSpec",
  bush: "WildfireBurnableSpec",
  building: "WildfireBurnableSpec",
};

const parseArgs = (args: string[]): Options => {
  const options: Options = {
    catalogPath: defaultCatalogPath,
    check: false,
    outputRoot: defaultOutputRoot,
  };

  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index] ?? "";

    if (arg === "--catalog") {
      options.catalogPath = resolve(requireValue(args, ++index, arg));
    } else if (arg.startsWith("--catalog=")) {
      options.catalogPath = resolve(arg.slice("--catalog=".length));
    } else if (arg === "--check") {
      options.check = true;
    } else if (arg === "--output") {
      options.outputRoot = resolve(requireValue(args, ++index, arg));
    } else if (arg.startsWith("--output=")) {
      options.outputRoot = resolve(arg.slice("--output=".length));
    } else {
      throw new Error(`Unknown argument: ${arg}`);
    }
  }

  return options;
};

const requireValue = (args: string[], index: number, flag: string): string => {
  const value = args[index];
  if (!value || value.startsWith("--")) {
    throw new Error(`${flag} requires a value.`);
  }

  return value;
};

const parseCsv = (text: string): string[][] => {
  const rows: string[][] = [];
  let row: string[] = [];
  let field = "";
  let quoted = false;

  Array.from(text.replace(/^\uFEFF/u, "")).forEach((character, index, characters) => {
    if (quoted) {
      if (character === "\"" && characters[index + 1] === "\"") {
        field += "\"";
        characters[index + 1] = "";
      } else if (character === "\"") {
        quoted = false;
      } else if (character !== "") {
        field += character;
      }

      return;
    }

    if (character === "\"") {
      quoted = true;
    } else if (character === ",") {
      row.push(field);
      field = "";
    } else if (character === "\n") {
      row.push(field.replace(/\r$/u, ""));
      field = "";
      if (row.some((value) => value.length > 0)) {
        rows.push(row);
      }
      row = [];
    } else {
      field += character;
    }
  });

  if (field.length > 0 || row.length > 0) {
    row.push(field.replace(/\r$/u, ""));
    rows.push(row);
  }

  return rows;
};

const readCatalog = (catalogPath: string): CatalogRow[] => {
  if (!existsSync(catalogPath)) {
    throw new Error(`Fuel catalog CSV is missing: ${catalogPath}`);
  }

  const rows = parseCsv(readFileSync(catalogPath, "utf8"));
  const headers = rows[0] ?? [];
  const missingHeaders = requiredHeaders.filter((header) => !headers.includes(header));
  if (missingHeaders.length > 0) {
    throw new Error(`Fuel catalog is missing required headers: ${missingHeaders.join(", ")}`);
  }

  return rows
    .slice(1)
    .map((row, rowIndex) => parseCatalogRow(headers, row, rowIndex + 2))
    .sort((left, right) => left.path.localeCompare(right.path));
};

const parseCatalogRow = (headers: string[], row: string[], rowNumber: number): CatalogRow => {
  const value = (header: string): string => row[headers.indexOf(header)]?.trim() ?? "";
  const type = value("Type").toLowerCase();
  if (!isCatalogType(type)) {
    throw new Error(`Invalid Type at row ${rowNumber}: ${value("Type")}`);
  }

  const path = value("Path");
  if (!path.endsWith(".blueprint.json")) {
    throw new Error(`Invalid Path at row ${rowNumber}: ${path}`);
  }

  return {
    id: requireCell(value("Id"), "Id", rowNumber),
    type,
    path,
    fuel: parseNumberCell(value("Fuel"), "Fuel", rowNumber),
    destructionThreshold: parseNumberCell(value("Destruction Threshold"), "Destruction Threshold", rowNumber),
    flammability: parseNumberCell(value("Flammability"), "Flammability", rowNumber),
    explosive: parseBooleanCell(value("Explosive"), "Explosive", rowNumber),
    contaminated: parseBooleanCell(value("Contaminated"), "Contaminated", rowNumber),
  };
};

const isCatalogType = (value: string): value is CatalogType =>
  value === "good" || value === "tree" || value === "crop" || value === "bush" || value === "building";

const requireCell = (value: string, header: string, rowNumber: number): string => {
  if (value.length === 0) {
    throw new Error(`Missing ${header} at row ${rowNumber}.`);
  }

  return value;
};

const parseNumberCell = (value: string, header: string, rowNumber: number): number => {
  if (value.length === 0) {
    return 0;
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    throw new Error(`Invalid ${header} at row ${rowNumber}: ${value}`);
  }

  return parsed;
};

const parseBooleanCell = (value: string, header: string, rowNumber: number): boolean => {
  if (/^true$/iu.test(value)) {
    return true;
  }

  if (/^false$/iu.test(value) || value.length === 0) {
    return false;
  }

  throw new Error(`Invalid ${header} at row ${rowNumber}: ${value}`);
};

const generateBlueprints = (rows: CatalogRow[]): GeneratedBlueprint[] =>
  rows.map((row) => ({
    relativePath: row.path,
    content: `${JSON.stringify(
      {
        [specNameByType[row.type]]: {
          Type: row.type,
          Fuel: row.fuel,
          "Destruction Threshold": row.destructionThreshold,
          Flammability: row.flammability,
          Explosive: row.explosive,
          Contaminated: row.contaminated,
        },
      },
      null,
      2,
    )}\n`,
  }));

const writeBlueprints = (outputRoot: string, blueprints: GeneratedBlueprint[], check: boolean): void => {
  if (check) {
    const mismatches = blueprints
      .map((blueprint) => [join(outputRoot, blueprint.relativePath), blueprint.content] as const)
      .filter(([path, content]) => !existsSync(path) || readFileSync(path, "utf8") !== content)
      .map(([path]) => path);

    if (mismatches.length > 0) {
      throw new Error(`Generated blueprints are out of date:\n${mismatches.join("\n")}`);
    }

    return;
  }

  if (existsSync(outputRoot)) {
    rmSync(outputRoot, { recursive: true, force: true });
  }

  blueprints.forEach((blueprint) => {
    const path = join(outputRoot, blueprint.relativePath);
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, blueprint.content);
  });
};

const main = (): void => {
  const options = parseArgs(Bun.argv.slice(2));
  const rows = readCatalog(options.catalogPath);
  const blueprints = generateBlueprints(rows);
  writeBlueprints(options.outputRoot, blueprints, options.check);

  const counts = rows.reduce<Record<string, number>>(
    (accumulator, row) => ({
      ...accumulator,
      [row.type]: (accumulator[row.type] ?? 0) + 1,
    }),
    {},
  );

  console.log(
    `[wildfire-blueprints] ${options.check ? "checked" : "generated"} ` +
      `${blueprints.length} blueprints ` +
      Object.entries(counts)
        .map(([type, count]) => `${type}=${count}`)
        .join(" "),
  );
};

main();
