import { describe, expect, test } from "bun:test";

import {
  buildFieldCheckpoints,
  buildSaveMetadataShapeBlockers,
  formatTimberbornSaveTimestamp,
  isTimberbornSaveTimestamp,
  mutateWorldEntities,
  updateMetadata,
} from "../scripts/generate-wildfire-scenario-save.ts";

const prototypeEntity = (template: string, x: number, y: number, z: number) => ({
  Components: {
    BlockObject: {
      Coordinates: { X: x, Y: y, Z: z },
    },
  },
  Id: `${template}-prototype`,
  Template: template,
});

describe("generate-wildfire-scenario-save metadata", () => {
  test("formats generated timestamps in Timberborn save metadata shape", () => {
    const timestamp = formatTimberbornSaveTimestamp(new Date(2026, 4, 3, 14, 7, 50));

    expect(timestamp).toBe("05/03/2026 14:07:50");
    expect(isTimberbornSaveTimestamp(timestamp)).toBe(true);
    expect(isTimberbornSaveTimestamp("2026-05-03T14:07:50.000Z")).toBe(false);
  });

  test("updates save_metadata without mutating the template object", () => {
    const templateMetadata = {
      Cycle: 1,
      Day: 1,
      Mods: [{ Id: "Harmony", Name: "Harmony", Version: "2.4.1" }],
      Timestamp: "05/01/2026 06:43:46",
    };

    const updated = updateMetadata(templateMetadata, new Date(2026, 4, 3, 14, 7, 50));

    expect(templateMetadata.Timestamp).toBe("05/01/2026 06:43:46");
    expect(updated).toEqual({
      Cycle: 1,
      Day: 1,
      Mods: [{ Id: "Harmony", Name: "Harmony", Version: "2.4.1" }],
      Timestamp: "05/03/2026 14:07:50",
    });
    expect(buildSaveMetadataShapeBlockers(updated)).toEqual([]);
  });

  test("flags the ISO timestamp shape Timberborn rejected", () => {
    const blockers = buildSaveMetadataShapeBlockers({
      Cycle: 1,
      Day: 1,
      Mods: [],
      Timestamp: "2026-05-03T05:54:46.231Z",
    });

    expect(blockers).toContain("save_metadata.json Timestamp must use Timberborn save format MM/dd/yyyy HH:mm:ss");
  });
});

describe("generate-wildfire-scenario-save field checkpoints", () => {
  test("emits deterministic checkpoints with material bands and companion expectations", () => {
    const checkpoints = buildFieldCheckpoints([
      { category: "tree-pad", coordinate: { x: 3, y: 45, z: 2 }, template: "Pine" },
      { category: "crop-pad", coordinate: { x: 3, y: 5, z: 2 }, template: "Carrot" },
      { category: "wood-heavy-structure-pad", coordinate: { x: 21, y: 25, z: 2 }, template: "MediumWarehouse.Folktails" },
      { category: "mixed-material-structure-pad", coordinate: { x: 26, y: 25, z: 2 }, template: "LargePile.Folktails" },
      { category: "central-camera-lane", coordinate: { x: 24, y: 20, z: 2 }, template: "Path.Folktails" },
      { category: "water-channel-source", coordinate: { x: 31, y: 48, z: 2 }, template: "WaterSource" },
      { category: "badwater-channel-source", coordinate: { x: 12, y: 48, z: 2 }, template: "BadwaterSource" },
    ]);

    expect(checkpoints.map((checkpoint) => checkpoint.category)).toEqual([
      "terrain-control",
      "empty-control",
      "tree-pad",
      "crop-pad",
      "wood-heavy-structure-pad",
      "mixed-material-structure-pad",
      "central-camera-lane",
      "water-channel-source",
      "badwater-channel-source",
    ]);
    expect(checkpoints.find((checkpoint) => checkpoint.category === "tree-pad")).toMatchObject({
      companionField: { consequenceTargetKind: "tree" },
      expectedCellMaterialClass: "tree",
      expectedPackedCellBand: { fuel: 12, flammability: 2 },
      expectedSourceMaterialClass: "tree",
      id: "tree-pad-1",
      template: "Pine",
    });
    expect(checkpoints.find((checkpoint) => checkpoint.category === "mixed-material-structure-pad")).toMatchObject({
      companionField: { consequenceTargetKind: "structure" },
      expectedCellMaterialClass: "building",
      expectedSourceMaterialClass: "storage",
      id: "mixed-material-structure-pad-1",
    });
    expect(checkpoints.find((checkpoint) => checkpoint.category === "water-channel-source")).toMatchObject({
      companionField: { consequenceTargetKind: "water" },
      expectedPackedCellBand: { water: 3 },
    });
  });
});

describe("generate-wildfire-scenario-save placement validity", () => {
  test("emits survivor-expected checkpoints at existing template-supported coordinates", () => {
    const prototypes = [
      ...Array.from({ length: 4 }, (_, index) => prototypeEntity("BadwaterSource", 80 + index, 80, 2)),
      ...Array.from({ length: 4 }, (_, index) => prototypeEntity("WaterSource", 90 + index, 80, 2)),
      ...["Pine", "Birch", "Oak"].flatMap((template, templateIndex) =>
        Array.from({ length: 4 }, (_, index) => prototypeEntity(template, 80 + index, 81 + templateIndex, 2)),
      ),
      ...Array.from({ length: 4 }, (_, index) => prototypeEntity("Carrot", 80 + index, 85, 2)),
      prototypeEntity("MediumWarehouse.Folktails", 80, 86, 2),
      prototypeEntity("LargePile.Folktails", 81, 86, 2),
      prototypeEntity("SmallTank.Folktails", 82, 86, 2),
      ...Array.from({ length: 3 }, (_, index) => prototypeEntity("Path", 80 + index, 87, 2)),
    ];
    const world = {
      Entities: prototypes,
      Singletons: {
        MapSize: {
          Size: { X: 128, Y: 128 },
        },
        TerrainMap: {
          Voxels: {
            Array: "0 0 0 0",
          },
        },
      },
    };

    const mutation = mutateWorldEntities(world);

    expect(mutation.nextWorld).toEqual(world);
    expect(mutation.blockers).toEqual([]);
    expect(mutation.generated).toHaveLength(30);
    expect(mutation.generated.filter((checkpoint) => checkpoint.template === "WaterSource")).toEqual([
      { category: "water-channel-source", coordinate: { x: 90, y: 80, z: 2 }, template: "WaterSource" },
      { category: "water-channel-source", coordinate: { x: 91, y: 80, z: 2 }, template: "WaterSource" },
      { category: "water-channel-source", coordinate: { x: 92, y: 80, z: 2 }, template: "WaterSource" },
      { category: "water-channel-source", coordinate: { x: 93, y: 80, z: 2 }, template: "WaterSource" },
    ]);
    expect(mutation.generated.map((checkpoint) => `${checkpoint.coordinate.x},${checkpoint.coordinate.y},${checkpoint.coordinate.z}`)).not.toContain("31,48,2");
  });

  test("keeps unsupported placement blockers explicit when the template has too few supported coordinates", () => {
    const world = {
      Entities: [prototypeEntity("WaterSource", 81, 80, 2)],
      Singletons: {
        MapSize: {
          Size: { X: 128, Y: 128 },
        },
        TerrainMap: {
          Voxels: {
            Array: "0 0 0 0",
          },
        },
      },
    };

    const mutation = mutateWorldEntities(world);

    expect(mutation.generated).toEqual([{ category: "water-channel-source", coordinate: { x: 81, y: 80, z: 2 }, template: "WaterSource" }]);
    expect(mutation.blockers).toContainEqual({
      blockedReason: "template entity Carrot or Potato or Wheat or Sunflower was not found in world.json Entities with valid BlockObject coordinates",
      category: "crop-pad",
      coordinate: { x: 3, y: 5, z: 2 },
      template: "Carrot",
    });
    expect(mutation.blockers.filter((blocker) => blocker.template === "WaterSource")).toHaveLength(3);
    expect(mutation.blockers.filter((blocker) => blocker.template === "WaterSource").every((blocker) => blocker.blockedReason?.includes("only 1 existing template-supported WaterSource"))).toBe(true);
  });

  test("uses modern Folktails path and harvestable crop fallbacks", () => {
    const world = {
      Entities: [
        ...Array.from({ length: 4 }, (_, index) => prototypeEntity("WaterSource", 90 + index, 80, 2)),
        ...Array.from({ length: 4 }, (_, index) => prototypeEntity("BadwaterSource", 80 + index, 80, 2)),
        ...["Pine", "Birch", "Oak"].flatMap((template, templateIndex) =>
          Array.from({ length: 4 }, (_, index) => prototypeEntity(template, 80 + index, 81 + templateIndex, 2)),
        ),
        ...Array.from({ length: 4 }, (_, index) => prototypeEntity("Potato", 80 + index, 85, 2)),
        prototypeEntity("MediumWarehouse.Folktails", 80, 86, 2),
        prototypeEntity("LargePile.Folktails", 81, 86, 2),
        prototypeEntity("SmallTank.Folktails", 82, 86, 2),
        ...Array.from({ length: 3 }, (_, index) => prototypeEntity("Path.Folktails", 80 + index, 87, 2)),
      ],
      Singletons: {
        MapSize: {
          Size: { X: 128, Y: 128 },
        },
        TerrainMap: {
          Voxels: {
            Array: "0 0 0 0",
          },
        },
      },
    };

    const mutation = mutateWorldEntities(world);

    expect(mutation.blockers).toEqual([]);
    expect(mutation.generated.filter((checkpoint) => checkpoint.category === "crop-pad").map((checkpoint) => checkpoint.template)).toEqual([
      "Potato",
      "Potato",
      "Potato",
      "Potato",
    ]);
    expect(mutation.generated.filter((checkpoint) => checkpoint.category === "central-camera-lane").map((checkpoint) => checkpoint.template)).toEqual([
      "Path.Folktails",
      "Path.Folktails",
      "Path.Folktails",
    ]);
  });
});
