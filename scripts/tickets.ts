#!/usr/bin/env bun

import { basename, join } from "node:path";
import { existsSync, lstatSync, mkdirSync, symlinkSync, unlinkSync } from "node:fs";

const ticketRoot = "kanban";
const allTickets = "all-tickets";
const statusRoot = "by-status";
const allDir = join(ticketRoot, allTickets);
const statusDir = join(ticketRoot, statusRoot);
const statuses = [
  "01-todo",
  "02-ready",
  "03-in-progress",
  "04-verify",
  "05-integration",
  "06-done",
  "07-blocked",
  "08-deferred",
  "09-awaiting-review"
];
const statusAliases = new Map([
  ["todo", "01-todo"],
  ["ready", "02-ready"],
  ["in-progress", "03-in-progress"],
  ["verify", "04-verify"],
  ["integration", "05-integration"],
  ["done", "06-done"],
  ["blocked", "07-blocked"],
  ["deferred", "08-deferred"],
  ["awaiting-review", "09-awaiting-review"],
  ...statuses.map(status => [status, status] as const)
]);

const [command, ticketRef, status] = Bun.argv.slice(2);

const usage = () => {
  console.log("Usage: ./tickets move <TWF-id-or-ticket-path> <status>");
  console.log("Examples:");
  console.log("  ./tickets move TWF-001 ready");
  console.log("  ./tickets move kanban/all-tickets/TWF-001-core-rules-and-frontier-hardening.md 04-verify");
  console.log("Statuses: " + statuses.join(", "));
  console.log("Aliases: " + [...statusAliases.keys()].filter(key => !key.match(/^\\d\\d-/)).join(", "));
};

const resolvedStatus = status ? statusAliases.get(status) : undefined;
if (command !== "move" || !ticketRef || !resolvedStatus) {
  usage();
  process.exit(command ? 1 : 0);
}

mkdirSync(join(statusDir, resolvedStatus), { recursive: true });

const ticketName = ticketRef.endsWith(".md")
  ? basename(ticketRef)
  : Array.from(new Bun.Glob(`${ticketRef}*.md`).scanSync({ cwd: allDir }))[0];

if (!ticketName) {
  console.error(`Could not find canonical ticket for ${ticketRef}`);
  process.exit(1);
}

const canonicalPath = join(allDir, ticketName);
if (!existsSync(canonicalPath)) {
  console.error(`Canonical ticket does not exist: ${canonicalPath}`);
  process.exit(1);
}

statuses
  .map(existingStatus => join(statusDir, existingStatus, ticketName))
  .filter(statusPath => existsSync(statusPath))
  .forEach(statusPath => {
    if (!lstatSync(statusPath).isSymbolicLink()) {
      console.error(`Refusing to remove non-symlink status entry: ${statusPath}`);
      process.exit(1);
    }

    unlinkSync(statusPath);
  });

const target = join(statusDir, resolvedStatus, ticketName);
if (existsSync(target)) {
  if (!lstatSync(target).isSymbolicLink()) {
    console.error(`Refusing to replace non-symlink status entry: ${target}`);
    process.exit(1);
  }

  unlinkSync(target);
}

symlinkSync(join("..", "..", allTickets, ticketName), target);
console.log(`${ticketName} -> ${resolvedStatus}`);
