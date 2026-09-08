import { afterEach, expect, test } from "bun:test";
import { cpSync, mkdtempSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { releaseManifestIdentity } from "../scripts/release-manifest.ts";
import { validateTimberbornModArtifact } from "../scripts/release-package-validation.ts";

const temporaryDirectories: string[] = [];
const stationModel = "Buildings/FireResponse/WardenStation.IronTeeth.timbermesh";

afterEach(() => {
  for (const directory of temporaryDirectories.splice(0)) rmSync(directory, { recursive: true, force: true });
});

function stagePackage() {
  const root = mkdtempSync(join(tmpdir(), "wildfire-release-validation-"));
  temporaryDirectories.push(root);
  const artifact = join(root, "Wildfire");
  cpSync(resolve(import.meta.dir, "../src/Wildfire.Timberborn/Data"), artifact, { recursive: true });
  writeFileSync(join(artifact, "manifest.json"), JSON.stringify(releaseManifestIdentity));
  mkdirSync(join(artifact, "Scripts"));
  for (const assembly of ["Wildfire.Timberborn.dll", "Wildfire.Core.dll"]) {
    writeFileSync(join(artifact, "Scripts", assembly), "fixture");
  }
  mkdirSync(join(artifact, "ComputeShaders"));
  for (const kind of ["compute", "diagnostic", "effects", "visual"]) {
    const bundle = join(artifact, "ComputeShaders", `wildfire_${kind}_mac`);
    writeFileSync(bundle, "fixture");
    writeFileSync(`${bundle}.manifest`, "Assets/WildfireGenerated/fixture");
  }
  return { root, artifact };
}

test("a complete staged payload includes the source station model", () => {
  const { artifact } = stagePackage();
  expect(validateTimberbornModArtifact(artifact, "Wildfire").files).toContain(stationModel);
});

test("a parent data folder cannot hide a missing nested model", () => {
  const { artifact } = stagePackage();
  rmSync(join(artifact, stationModel));
  expect(() => validateTimberbornModArtifact(artifact, "Wildfire")).toThrow(stationModel);
});

test("a stale or truncated source asset cannot pass package validation", () => {
  const { artifact } = stagePackage();
  writeFileSync(join(artifact, stationModel), "truncated");
  expect(() => validateTimberbornModArtifact(artifact, "Wildfire")).toThrow("differs from source");
});

test("the ZIP must retain every validated data file", () => {
  const { root, artifact } = stagePackage();
  const zipPath = join(root, "incomplete.zip");
  const result = Bun.spawnSync(["zip", "-qr", zipPath, "Wildfire", "-x", `Wildfire/${stationModel}`], {
    cwd: root,
    stdout: "pipe",
    stderr: "pipe",
  });
  expect(result.exitCode).toBe(0);
  expect(() => validateTimberbornModArtifact(artifact, "Wildfire", zipPath)).toThrow(`missing Wildfire/${stationModel}`);
});
