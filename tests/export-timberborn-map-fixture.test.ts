import { describe, expect, test } from "bun:test";

import { buildFixtureFromWorld } from "../scripts/export-timberborn-map-fixture.ts";

const packCell = (fuel: number, heat: number, flammability: number, water: number, terrain: number, heatLoss: number): number =>
  ((fuel & 0b1111) << 0) |
  ((heat & 0b1111) << 4) |
  ((flammability & 0b11) << 8) |
  ((water & 0b11) << 10) |
  ((terrain & 0b1) << 12) |
  ((heatLoss & 0b111) << 13);

const packCompanion = (material: number, burnCapacity: number, ashQuality: number, contaminationBehavior: number): number =>
  (material & 0xff) |
  ((burnCapacity & 0xf) << 8) |
  ((ashQuality & 0b11) << 20) |
  ((contaminationBehavior & 0b111) << 22);

describe("export-timberborn-map-fixture", () => {
  test("maps Timberborn terrain and entities into shader fixture packed cells", () => {
    const { fixture, summary } = buildFixtureFromWorld({
      Entities: [
        {
          Components: {
            BlockObject: { Coordinates: { X: 1, Y: 0, Z: 1 } },
            LivingNaturalResource: {},
          },
          Template: "Pine",
        },
        {
          Components: {
            BlockObject: { Coordinates: { X: 0, Y: 1, Z: 0 } },
          },
          Template: "Path",
        },
      ],
      Singletons: {
        MapSize: { Size: { X: 2, Y: 2 } },
        TerrainMap: {
          Voxels: {
            Array: "1 0 0 0 0 0 0 0",
          },
        },
        WaterMapNew: {
          WaterColumns: {
            Array: "0 1.0:1.0:0:1 0 0",
          },
        },
      },
    });

    expect(fixture.grid).toEqual({ depth: 2, height: 2, width: 2 });
    expect(fixture.selectedLayer).toEqual({ cellCount: 4, index: 0, offset: 0 });
    expect(fixture.packedCellValues.values).toEqual([
      packCell(0, 0, 0, 0, 1, 6),
      0xe000,
      packCell(0, 0, 0, 0, 1, 5),
      0xe000,
      0xe000,
      packCell(12, 0, 2, 3, 1, 1),
      0xe000,
      0xe000,
    ]);
    expect(fixture.companionFieldValues.targetIds).toEqual([0, 0, 2, 0, 0, 1, 0, 0]);
    expect(fixture.companionFieldValues.packedStateValues).toEqual([
      packCompanion(1, 0, 0, 0),
      packCompanion(0, 0, 0, 0),
      packCompanion(7, 0, 1, 1),
      packCompanion(0, 0, 0, 0),
      packCompanion(0, 0, 0, 0),
      packCompanion(4, 12, 1, 1),
      packCompanion(0, 0, 0, 0),
      packCompanion(0, 0, 0, 0),
    ]);
    expect(fixture.parityCounts.sourceCountsByMaterialClass).toMatchObject({
      infrastructure: 1,
      terrain: 1,
      tree: 1,
      water: 1,
    });
    expect(fixture.parityCounts.resolvedCellCountsByMaterialClass).toMatchObject({
      empty: 5,
      infrastructure: 1,
      terrain: 1,
      tree: 1,
    });
    expect(fixture.parityCounts).toMatchObject({
      entitySourceCount: 2,
      terrainSurfaceSourceCount: 1,
      terrainSolidVoxelCount: 1,
      waterColumnSourceCount: 1,
    });
    expect(summary).toMatchObject({
      cellCount: 8,
      infrastructureSources: 1,
      solidTerrainSources: 1,
      terrainSurfaceSources: 1,
      treeSources: 1,
      waterSources: 1,
    });
  });

  test("counts terrain sources as exposed surface cells while preserving solid voxel diagnostics", () => {
    const { fixture, summary } = buildFixtureFromWorld({
      Entities: [],
      Singletons: {
        MapSize: { Size: { X: 2, Y: 1 } },
        TerrainMap: {
          Voxels: {
            Array: "1 1 1 0 0 1",
          },
        },
      },
    });

    expect(fixture.parityCounts.sourceCountsByMaterialClass).toMatchObject({
      terrain: 3,
    });
    expect(fixture.parityCounts.resolvedCellCountsByMaterialClass).toMatchObject({
      empty: 3,
      terrain: 3,
    });
    expect(fixture.parityCounts).toMatchObject({
      terrainSurfaceSourceCount: 3,
      terrainSolidVoxelCount: 4,
    });
    expect(summary).toMatchObject({
      solidTerrainSources: 4,
      terrainSurfaceSources: 3,
    });
  });
});
