#!/usr/bin/env bun

const command = ["dotnet", "test", "Wildfire.slnx"];
const proc = Bun.spawn(command, {
  stdout: "inherit",
  stderr: "inherit"
});

const exitCode = await proc.exited;
process.exit(exitCode);
