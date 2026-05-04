import { describe, expect, test } from "bun:test";

import { cellsToUInt16LittleEndian, readBinarySnapshot } from "../scripts/convert-wildfire-snapshot-binary.ts";

describe("convert-wildfire-snapshot-binary", () => {
  test("converts fixture packed cells to uint16 little-endian bytes", () => {
    const snapshot = readBinarySnapshot({
      grid: { depth: 1, height: 2, width: 2 },
      packedCellValues: {
        indexOrder: "x + y * width + z * width * height",
        valueType: "uint16",
        values: [0, 1, 0x1234, 0xffff],
      },
      scenario: "hand-authored",
      seed: 17,
    });

    expect(snapshot).toMatchObject({
      cellCount: 4,
      grid: { depth: 1, height: 2, width: 2 },
      scenario: "hand-authored",
      seed: 17,
      source: "fixture",
      tickCount: null,
    });
    expect([...cellsToUInt16LittleEndian(snapshot.cells)]).toEqual([0, 0, 1, 0, 0x34, 0x12, 0xff, 0xff]);
  });

  test("can read capture finalPackedCells explicitly", () => {
    const snapshot = readBinarySnapshot(
      {
        finalPackedCells: [7],
        grid: { depth: 1, height: 1, width: 1 },
        scenario: "after-two-ticks",
        seed: 21,
        tickCount: 2,
      },
      "capture",
    );

    expect(snapshot).toMatchObject({
      cellCount: 1,
      source: "capture",
      tickCount: 2,
    });
    expect([...cellsToUInt16LittleEndian(snapshot.cells)]).toEqual([7, 0]);
  });

  test("rejects snapshots whose cell count does not match the grid", () => {
    expect(() =>
      readBinarySnapshot({
        grid: { depth: 1, height: 2, width: 2 },
        packedCellValues: {
          values: [1, 2],
        },
      }),
    ).toThrow("expected 4");
  });
});
