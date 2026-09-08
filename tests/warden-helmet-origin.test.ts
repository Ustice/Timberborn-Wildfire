import { expect, test } from "bun:test";
import { readFileSync } from "node:fs";
import { inflateSync } from "node:zlib";

const root = new URL("../", import.meta.url);
const read = (path: string) => readFileSync(new URL(path, root));
type Point = [number, number, number];

// Read the small protobuf wire subset used by the checked-in timbermesh model.
// Geometry remains real artifact data; no Blender or installed native runtime is needed.
function fields(bytes: Buffer, wanted: number): Buffer[] {
  let offset = 0;
  const result: Buffer[] = [];
  const varint = () => {
    let value = 0;
    let shift = 0;
    let byte: number;
    do {
      byte = bytes[offset++]!;
      value += (byte & 127) * 2 ** shift;
      shift += 7;
    } while (byte & 128);
    return value;
  };
  while (offset < bytes.length) {
    const tag = varint();
    const wire = tag & 7;
    if (wire === 0) { varint(); continue; }
    const length = wire === 2 ? varint() : wire === 5 ? 4 : wire === 1 ? 8 : -1;
    if (length < 0 || offset + length > bytes.length) throw new Error("Invalid timbermesh field");
    if (tag >>> 3 === wanted) result.push(bytes.subarray(offset, offset + length));
    offset += length;
  }
  return result;
}

interface Glb {
  scene: number;
  scenes: { nodes: number[] }[];
  nodes: { name: string; mesh?: number; children?: number[]; translation?: Point;
    scale?: Point; rotation?: number[]; matrix?: number[] }[];
  meshes: { primitives: { attributes: { POSITION: number } }[] }[];
  accessors: { bufferView: number; byteOffset?: number; componentType: number; type: string; count: number }[];
  bufferViews: { byteOffset?: number; byteStride?: number }[];
}

function authoredPoints(): Point[] {
  const bytes = read("art/fire-response/WardenHelmet.glb");
  expect(bytes.readUInt32LE(0)).toBe(0x46546c67);
  const jsonLength = bytes.readUInt32LE(12);
  const glb: Glb = JSON.parse(bytes.subarray(20, 20 + jsonLength).toString());
  const binary = bytes.subarray(28 + jsonLength);
  const rootNode = glb.nodes[glb.scenes[glb.scene]!.nodes[0]!]!;
  expect(rootNode.name).toBe("WardenHelmet");
  expect(rootNode.translation).toBeUndefined();
  expect(rootNode.scale).toBeUndefined();
  expect(rootNode.rotation).toBeUndefined();
  expect(rootNode.matrix).toBeUndefined();
  const result: Point[] = [];
  for (const index of rootNode.children!) {
    const node = glb.nodes[index]!;
    expect(node.children).toBeUndefined();
    expect(node.rotation).toBeUndefined();
    expect(node.matrix).toBeUndefined();
    const scale = node.scale ?? [1, 1, 1];
    const shift = node.translation ?? [0, 0, 0];
    for (const primitive of glb.meshes[node.mesh!]!.primitives) {
      const accessor = glb.accessors[primitive.attributes.POSITION]!;
      const view = glb.bufferViews[accessor.bufferView]!;
      expect([accessor.componentType, accessor.type]).toEqual([5126, "VEC3"]);
      const start = (view.byteOffset ?? 0) + (accessor.byteOffset ?? 0);
      for (let i = 0; i < accessor.count; i++) {
        const point = (axis: number) => binary.readFloatLE(start + i * (view.byteStride ?? 12) + axis * 4)
          * scale[axis]! + shift[axis]!;
        // GLB (Blender x,z,-y) -> official native export (-x,z,-y).
        result.push([-point(0), point(1), point(2)]);
      }
    }
  }
  return result;
}

test("native attachment pose restores every exported helmet vertex to its authored local origin", () => {
  const authored = authoredPoints();
  const model = inflateSync(read("src/Wildfire.Timberborn/Data/Equipment/FireResponse/WardenHelmet.IronTeeth.timbermesh"));
  const nodes = fields(model, 3);
  expect(nodes).toHaveLength(1);
  const node = nodes[0]!;
  // Node transform is identity; the preview displacement is baked into vertices.
  expect(fields(node, 3)[0]!.length).toBe(0);
  expect(fields(fields(node, 4)[0]!, 4)[0]!.readFloatLE()).toBe(1);
  for (const axis of [1, 2, 3]) expect(fields(fields(node, 5)[0]!, axis)[0]!.readFloatLE()).toBe(1);
  const positions = fields(node, 7).filter(property => fields(property, 1)[0]!.toString() === "position");
  expect(positions).toHaveLength(1);
  const vertices = fields(positions[0]!, 4)[0]!;
  const blueprint = JSON.parse(read("src/Wildfire.Timberborn/Data/Characters/Beaver/BeaverAdult.blueprint.json").toString());
  const attachment = blueprint.TemplateAttachmentsSpec["Attachments#append"][0];
  expect(attachment.Parent).toBe("#Head");
  expect(attachment.Rotation).toEqual({ X: 0, Y: 0, Z: 0 });
  expect(attachment.Scale).toEqual({ X: 1, Y: 1, Z: 1 });
  const pose: Point = [attachment.Position.X, attachment.Position.Y, attachment.Position.Z];
  expect(vertices.length / 12).toBeGreaterThan(4000);
  let maximumError = 0;
  for (let offset = 0; offset < vertices.length; offset += 12) {
    const point: Point = [vertices.readFloatLE(offset) + pose[0], vertices.readFloatLE(offset + 4) + pose[1],
      vertices.readFloatLE(offset + 8) + pose[2]];
    let nearest = Infinity;
    for (const other of authored) nearest = Math.min(nearest,
      Math.max(Math.abs(point[0] - other[0]), Math.abs(point[1] - other[1]), Math.abs(point[2] - other[2])));
    maximumError = Math.max(maximumError, nearest);
  }
  expect(maximumError).toBeLessThan(1e-6);
});
