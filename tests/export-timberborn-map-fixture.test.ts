import { describe, expect, test } from "bun:test";

import { buildFixtureFromWorld } from "../scripts/export-timberborn-map-fixture.ts";

const buildMixedFixture = () =>
  buildFixtureFromWorld(
    {
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
    },
    "timberborn-map-state",
    null,
    {},
  );

const nonZeroTargetIds = (targetIds: number[]): [number, number][] =>
  targetIds.flatMap((targetId, index) => (targetId === 0 ? [] : [[index, targetId] as [number, number]]));

describe("export-timberborn-map-fixture", () => {
  test("maps Timberborn terrain and entities into semantic fixture behavior", () => {
    const { fixture, summary } = buildMixedFixture();

    expect(fixture.grid).toEqual({ depth: 2, height: 2, width: 2 });
    expect(fixture.selectedLayer).toEqual({ cellCount: 4, index: 0, offset: 0 });
    expect(nonZeroTargetIds(fixture.companionFieldValues.targetIds)).toEqual([
      [2, 2],
      [5, 1],
    ]);
    expect(fixture.parityCounts.sourceCountsByMaterialClass).toMatchObject({
      infrastructure: 1,
      terrain: 1,
      tree: 1,
      water: 0,
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
      waterSources: 0,
    });
  });

  test("emits the v1 packed field layout for shader fixture serialization", () => {
    const { fixture } = buildMixedFixture();

    expect(fixture.packedCellValues).toMatchObject({
      indexOrder: "x + y * width + z * width * height",
      valueType: "uint16",
    });
    expect(fixture.companionFieldValues).toMatchObject({
      indexOrder: "x + y * width + z * width * height",
      statePacking: "material:0-7,burnCapacity:8-11,burnHistory:12-15,ashStrength:16-19,ashQuality:20-21,contamination:22-24",
      valueType: "uint32",
    });
    expect(fixture.packedCellValues.values).toEqual([
      0x1000,
      0x0000,
      0x1000,
      0x0000,
      0x0000,
      0x1e0c,
      0x0000,
      0x0000,
    ]);
    expect(fixture.companionFieldValues.packedStateValues).toEqual([
      0x000001,
      0x000000,
      0x500007,
      0x000000,
      0x000000,
      0x500c04,
      0x000000,
      0x000000,
    ]);
  });

  test("counts terrain sources as exposed surface cells while preserving solid voxel diagnostics", () => {
    const { fixture, summary } = buildFixtureFromWorld(
      {
        Entities: [],
        Singletons: {
          MapSize: { Size: { X: 2, Y: 1 } },
          TerrainMap: {
            Voxels: {
              Array: "1 1 1 0 0 1",
            },
          },
        },
      },
      "timberborn-map-state",
      null,
      {},
    );

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

  test("expands saved entity coordinates through blueprint footprints", () => {
    const { fixture, summary } = buildFixtureFromWorld(
      {
        Entities: [
          {
            Components: {
              BlockObject: { Coordinates: { X: 0, Y: 0, Z: 0 } },
            },
            Template: "Pine",
          },
        ],
        Singletons: {
          MapSize: { Size: { X: 1, Y: 1 } },
          TerrainMap: {
            Voxels: {
              Array: "0 0 0",
            },
          },
        },
      },
      "timberborn-map-state",
      null,
      {
        Pine: { occupyAllBelow: false, sizeX: 1, sizeY: 1, sizeZ: 3 },
      },
    );

    expect(fixture.parityCounts.sourceCountsByMaterialClass).toMatchObject({
      tree: 3,
    });
    expect(fixture.parityCounts.resolvedCellCountsByMaterialClass).toMatchObject({
      tree: 3,
    });
    expect(fixture.parityCounts).toMatchObject({
      entityObjectCount: 1,
      entitySourceCount: 3,
    });
    expect(summary).toMatchObject({
      entityObjects: 1,
      entitySources: 3,
      treeSources: 3,
    });
  });

  test("expands occupy-all-below water sources as entity source cells", () => {
    const { fixture, summary } = buildFixtureFromWorld(
      {
        Entities: [
          {
            Components: {
              BlockObject: { Coordinates: { X: 0, Y: 0, Z: 3 } },
            },
            Template: "WaterSource",
          },
        ],
        Singletons: {
          MapSize: { Size: { X: 1, Y: 1 } },
          TerrainMap: {
            Voxels: {
              Array: "0 0 0 0",
            },
          },
        },
      },
      "timberborn-map-state",
      null,
      {
        WaterSource: { occupyAllBelow: true, sizeX: 1, sizeY: 1, sizeZ: 1 },
      },
    );

    expect(fixture.parityCounts.sourceCountsByMaterialClass).toMatchObject({
      water: 4,
    });
    expect(fixture.parityCounts.resolvedCellCountsByMaterialClass).toMatchObject({
      water: 4,
    });
    expect(fixture.parityCounts).toMatchObject({
      entityObjectCount: 1,
      entitySourceCount: 4,
      waterColumnSourceCount: 0,
    });
    expect(summary).toMatchObject({
      entityObjects: 1,
      entitySources: 4,
      waterSources: 4,
    });
  });

  test("uses occupied blueprint blocks instead of full bounding boxes", () => {
    const { fixture, summary } = buildFixtureFromWorld(
      {
        Entities: [
          {
            Components: {
              BlockObject: { Coordinates: { X: 0, Y: 0, Z: 0 } },
            },
            Template: "Building",
          },
        ],
        Singletons: {
          MapSize: { Size: { X: 1, Y: 1 } },
          TerrainMap: {
            Voxels: {
              Array: "0 0",
            },
          },
        },
      },
      "timberborn-map-state",
      null,
      {
        Building: {
          blocks: [
            { occupyAllBelow: false, x: 0, y: 0, z: 0 },
          ],
          occupyAllBelow: false,
          sizeX: 1,
          sizeY: 1,
          sizeZ: 2,
        },
      },
    );

    expect(fixture.parityCounts.sourceCountsByMaterialClass).toMatchObject({
      building: 1,
    });
    expect(fixture.parityCounts.resolvedCellCountsByMaterialClass).toMatchObject({
      building: 1,
      empty: 1,
    });
    expect(summary).toMatchObject({
      buildingSources: 1,
      entitySources: 1,
    });
  });
});
