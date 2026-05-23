#!/usr/bin/env bun

import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

const repoRoot = resolve(import.meta.dir, "..");
const coreProjectPath = join(repoRoot, "src", "Wildfire.Core", "Wildfire.Core.csproj");

const escapeXml = (value: string): string =>
  value
    .replaceAll("&", "&amp;")
    .replaceAll("\"", "&quot;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;");

const run = (command: string, args: string[], cwd: string): void => {
  console.log([command, ...args].join(" "));
  const result = Bun.spawnSync([command, ...args], {
    cwd,
    stdout: "inherit",
    stderr: "inherit",
  });

  if (result.exitCode !== 0) {
    throw new Error(`${command} exited with code ${result.exitCode}.`);
  }
};

const testProject = (projectReference: string): string => `<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="${escapeXml(projectReference)}" />
  </ItemGroup>

</Project>
`;

const testSource = `using Wildfire.Core;

namespace Wildfire.Hosted.Tests;

public sealed class HostedCoreSmokeTests
{
    [Fact]
    public void PackedCellRoundTripsFields()
    {
        ushort cell = PackedCell.Pack(
            fuel: 13,
            heat: 11,
            flammability: 3,
            water: 2,
            terrain: 1,
            burningLevel: 6);

        Assert.Equal(13, PackedCell.Fuel(cell));
        Assert.Equal(11, PackedCell.Heat(cell));
        Assert.Equal(3, PackedCell.Flammability(cell));
        Assert.Equal(2, PackedCell.Water(cell));
        Assert.Equal(1, PackedCell.Terrain(cell));
        Assert.Equal(6, PackedCell.BurningLevel(cell));
    }

    [Fact]
    public void FireGridConvertsBetweenCoordinatesAndIndices()
    {
        FireGrid grid = new(4, 3, 2);

        int index = grid.ToIndex(2, 1, 1);

        Assert.Equal(18, index);
        Assert.Equal((2, 1, 1), grid.FromIndex(index));
    }

    [Fact]
    public void FireRandomHashIsDeterministicAndInputSensitive()
    {
        uint baseline = FireRandom.Hash(cellIndex: 7, tick: 3, seed: 19);

        Assert.Equal(baseline, FireRandom.Hash(cellIndex: 7, tick: 3, seed: 19));
        Assert.NotEqual(baseline, FireRandom.Hash(cellIndex: 8, tick: 3, seed: 19));
    }

    [Fact]
    public void WindNormalizesDirectionAndClampsStrength()
    {
        FireSimWind wind = new(3f, 4f, 2f);

        FireSimWind normalized = wind.Normalized();

        Assert.Equal(0.6f, normalized.DirectionX, precision: 4);
        Assert.Equal(0.8f, normalized.DirectionY, precision: 4);
        Assert.Equal(1f, normalized.Strength);
    }
}
`;

const main = async (): Promise<void> => {
  const workspace = await mkdtemp(join(tmpdir(), "wildfire-hosted-dotnet-tests-"));

  try {
    await writeFile(join(workspace, "Wildfire.Hosted.Tests.csproj"), testProject(coreProjectPath));
    await writeFile(join(workspace, "HostedCoreSmokeTests.cs"), testSource);
    run("dotnet", ["test", "Wildfire.Hosted.Tests.csproj", "--configuration", "Release"], workspace);
  } finally {
    await rm(workspace, { force: true, recursive: true });
  }
};

main().catch((error: unknown) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
});
