import { readFileSync } from "fs";
import { join } from "path";

export type MaterialClass =
  | "empty"
  | "terrain"
  | "vegetation"
  | "crop"
  | "tree"
  | "building"
  | "storage"
  | "infrastructure"
  | "water"
  | "badwater"
  | "unknown";

export type ConsequenceTargetKind =
  | "none"
  | "crop"
  | "tree"
  | "structure"
  | "storage"
  | "infrastructure"
  | "water";

export type AshQuality = "none" | "fertile" | "spent" | "tainted";

export type ContaminationBehavior =
  | "none"
  | "taint-if-source-contaminated"
  | "tainted-source"
  | "suppresses-without-cleaning"
  | "fail-closed";

export type ResourcePolicy = "fixed" | "use-resource-catalog" | "fail-closed";

export type MaterialFieldProfile = {
  ashQuality: AshQuality;
  burnCapacity: number;
  consequenceTargetKind: ConsequenceTargetKind;
  contaminationBehavior: ContaminationBehavior;
  flammability: number;
  fuel: number;
  heatLoss: number;
  materialClass: MaterialClass;
  resourcePolicy: ResourcePolicy;
  terrain: number;
  water: number;
};

export type MaterialFieldSchema = {
  formatVersion: 1;
  indexOrder: "x + y * width + z * width * height";
  profiles: MaterialFieldProfile[];
};

export const materialFieldSchemaPath = join(import.meta.dir, "..", "src", "Wildfire.Core", "MaterialFieldSchema.v1.json");

const assertBand = (value: number, max: number, field: string, materialClass: string): void => {
  if (!Number.isInteger(value) || value < 0 || value > max) {
    throw new Error(`${materialClass}.${field} must be an integer in 0..${max}.`);
  }
};

export const validateMaterialFieldSchema = (schema: MaterialFieldSchema): MaterialFieldSchema => {
  if (schema.formatVersion !== 1) {
    throw new Error(`Unsupported material field schema version ${schema.formatVersion}.`);
  }

  const seen = new Set<string>();
  schema.profiles.forEach((profile) => {
    if (seen.has(profile.materialClass)) {
      throw new Error(`Duplicate material class ${profile.materialClass}.`);
    }

    seen.add(profile.materialClass);
    assertBand(profile.fuel, 15, "fuel", profile.materialClass);
    assertBand(profile.flammability, 3, "flammability", profile.materialClass);
    assertBand(profile.heatLoss, 7, "heatLoss", profile.materialClass);
    assertBand(profile.terrain, 1, "terrain", profile.materialClass);
    assertBand(profile.water, 3, "water", profile.materialClass);
    assertBand(profile.burnCapacity, 15, "burnCapacity", profile.materialClass);
  });

  if (!seen.has("unknown")) {
    throw new Error("Schema must include an unknown material profile.");
  }

  return schema;
};

export const loadMaterialFieldSchema = (path: string = materialFieldSchemaPath): MaterialFieldSchema =>
  validateMaterialFieldSchema(JSON.parse(readFileSync(path, "utf8")) as MaterialFieldSchema);

export const lookupMaterialProfile = (
  materialClass: string,
  schema: MaterialFieldSchema = loadMaterialFieldSchema(),
): MaterialFieldProfile =>
  schema.profiles.find((profile) => profile.materialClass === materialClass) ??
  schema.profiles.find((profile) => profile.materialClass === "unknown")!;
