import { describe, expect, test } from "bun:test";

import { loadMaterialFieldSchema, lookupMaterialProfile } from "../scripts/material-field-schema.ts";

describe("material field schema", () => {
  test("loads every v1 material class exactly once", () => {
    const schema = loadMaterialFieldSchema();

    expect(schema.profiles.map((profile) => profile.materialClass)).toEqual([
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
    ]);
  });

  test("keeps unknown material fail-closed", () => {
    expect(lookupMaterialProfile("MissingTemplate")).toMatchObject({
      burnCapacity: 0,
      consequenceTargetKind: "none",
      contaminationBehavior: "fail-closed",
      fuel: 0,
      materialClass: "unknown",
      resourcePolicy: "fail-closed",
    });
  });

  test("marks water and badwater as suppressing without decontaminating", () => {
    expect(lookupMaterialProfile("water")).toMatchObject({
      contaminationBehavior: "suppresses-without-cleaning",
      water: 3,
    });
    expect(lookupMaterialProfile("badwater")).toMatchObject({
      ashQuality: "tainted",
      contaminationBehavior: "tainted-source",
      water: 3,
    });
  });
});
