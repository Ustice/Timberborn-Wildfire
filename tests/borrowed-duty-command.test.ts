import { expect, test } from "bun:test";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

for (const payload of [
  ["qa-borrowed-duty-arm", "01234567-89ab-cdef-0123-456789abcdef", "1.25", "2", "3.5"],
  ["qa-borrowed-duty-cancel"],
  ["qa-borrowed-duty-status"],
]) {
  test(`controller script transports ${payload[0]} through an isolated QA inbox`, async () => {
    const directory = await mkdtemp(join(tmpdir(), "wildfire-borrowed-duty-command-"));
    const inbox = join(directory, "command-inbox.txt");
    const child = Bun.spawn([Bun.which("bun")!, resolve("scripts/invoke-timberborn-command.ts"), ...payload,
      `--command-dir=${directory}`, "--wait=2"], { stdout: "pipe", stderr: "pipe" });
    try {
      for (let tries = 0; !existsSync(inbox) && tries < 100; tries++) await Bun.sleep(10);
      expect(await readFile(inbox, "utf8")).toBe(payload.join(" ") + "\n");
      // A fake outbox proves CLI transport only; no game/native command is executed.
      await writeFile(join(directory, "command-outbox.txt"), `wildfire_command_result command=${payload[0]} success=true\n`);
      expect(await child.exited).toBe(0);
    } finally {
      child.kill();
      await rm(directory, { recursive: true, force: true });
    }
  });
}
