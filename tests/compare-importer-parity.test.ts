import { describe, expect, test } from "bun:test";

import { compareImporterParity, renderImporterParityReport } from "../scripts/compare-importer-parity.ts";

const fixture = JSON.stringify({
  grid: { depth: 3, height: 2, width: 4 },
  parityCounts: {
    resolvedCellCountsByMaterialClass: {
      badwater: 1,
      building: 2,
      crop: 0,
      empty: 10,
      infrastructure: 0,
      storage: 0,
      terrain: 7,
      tree: 3,
      vegetation: 1,
      water: 0,
    },
    sourceCountsByMaterialClass: {
      badwater: 1,
      building: 2,
      crop: 0,
      infrastructure: 0,
      storage: 1,
      terrain: 8,
      tree: 3,
      vegetation: 1,
      water: 4,
    },
  },
});

describe("compare-importer-parity", () => {
  test("compares snapshot and live importer counts by explicit unit", () => {
    const liveOutput = [
      "wildfire_command_result command=qa-readiness success=true width=4 height=2 depth=3",
      "world_import_terrain_sources=8 world_import_vegetation_sources=0 world_import_crop_sources=0",
      "world_import_tree_sources=3 world_import_building_sources=2 world_import_storage_sources=1",
      "world_import_infrastructure_sources=0 world_import_water_sources=4 world_import_badwater_sources=1",
      "world_import_resolved_empty_cells=10 world_import_resolved_terrain_cells=7",
      "world_import_resolved_vegetation_cells=0 world_import_resolved_crop_cells=0",
      "world_import_resolved_tree_cells=3 world_import_resolved_building_cells=2",
      "world_import_resolved_storage_cells=0 world_import_resolved_infrastructure_cells=0",
      "world_import_resolved_water_cells=0 world_import_resolved_badwater_cells=1",
    ].join(" ");

    const comparison = compareImporterParity(fixture, liveOutput);

    expect(comparison.matching).toBe(false);
    expect(comparison.sourceCounts.find((row) => row.className === "vegetation")).toMatchObject({
      delta: -1,
      live: 0,
      snapshot: 1,
    });
    expect(comparison.resolvedCells.find((row) => row.className === "storage")).toMatchObject({
      delta: 0,
      live: 0,
      snapshot: 0,
    });
  });

  test("renders a markdown report with separate source and resolved tables", () => {
    const liveOutput = [
      "wildfire_command_result command=qa-readiness success=true width=4 height=2 depth=3",
      "world_import_terrain_sources=8 world_import_vegetation_sources=1 world_import_crop_sources=0",
      "world_import_tree_sources=3 world_import_building_sources=2 world_import_storage_sources=1",
      "world_import_infrastructure_sources=0 world_import_water_sources=4 world_import_badwater_sources=1",
      "world_import_resolved_empty_cells=10 world_import_resolved_terrain_cells=7",
      "world_import_resolved_vegetation_cells=1 world_import_resolved_crop_cells=0",
      "world_import_resolved_tree_cells=3 world_import_resolved_building_cells=2",
      "world_import_resolved_storage_cells=0 world_import_resolved_infrastructure_cells=0",
      "world_import_resolved_water_cells=0 world_import_resolved_badwater_cells=1",
    ].join(" ");

    const report = renderImporterParityReport(compareImporterParity(fixture, liveOutput));

    expect(report).toContain("Status: pass");
    expect(report).toContain("Source status: pass");
    expect(report).toContain("Resolved-cell diagnostic status: pass");
    expect(report).toContain("## Source Counts");
    expect(report).toContain("| storage | 1 | 1 | 0 |");
    expect(report).toContain("## Resolved Cell Counts");
    expect(report).toContain("| storage | 0 | 0 | 0 |");
  });

  test("does not fail source parity when only resolved diagnostic counts differ", () => {
    const liveOutput = [
      "wildfire_command_result command=qa-readiness success=true width=4 height=2 depth=3",
      "world_import_terrain_sources=8 world_import_vegetation_sources=1 world_import_crop_sources=0",
      "world_import_tree_sources=3 world_import_building_sources=2 world_import_storage_sources=1",
      "world_import_infrastructure_sources=0 world_import_water_sources=4 world_import_badwater_sources=1",
      "world_import_resolved_empty_cells=11 world_import_resolved_terrain_cells=6",
      "world_import_resolved_vegetation_cells=1 world_import_resolved_crop_cells=0",
      "world_import_resolved_tree_cells=3 world_import_resolved_building_cells=2",
      "world_import_resolved_storage_cells=0 world_import_resolved_infrastructure_cells=0",
      "world_import_resolved_water_cells=0 world_import_resolved_badwater_cells=1",
    ].join(" ");

    const comparison = compareImporterParity(fixture, liveOutput);

    expect(comparison.matching).toBe(true);
    expect(comparison.sourceMatching).toBe(true);
    expect(comparison.resolvedMatching).toBe(false);
  });
});
