#!/usr/bin/env bun

import { execFileSync } from "child_process";
import { existsSync, lstatSync, readFileSync, readdirSync, realpathSync } from "fs";
import { basename, join, relative, resolve } from "path";

type Ticket = {
  dependencies: string[];
  docOnly: boolean;
  file: string;
  id: string;
  requiresQa: boolean;
  role: string;
  text: string;
  title: string;
  writeScope: string[];
};

type StatusLink = {
  file: string;
  id: string;
  linkPath: string;
  status: string;
};

type Audit = {
  blockedTodo: string[];
  brokenLinks: string[];
  duplicateStatuses: string[];
  missingDependencyFiles: string[];
  missingImplementationNotes: string[];
  missingStatus: string[];
  missingVerification: string[];
  readyCandidates: string[];
  worktreeKanbanChanges: string[];
  writeScopeOverlaps: string[];
};

const repoRoot = resolve(import.meta.dir, "..");
const allTicketsDir = join(repoRoot, "kanban", "all-tickets");
const statusDir = join(repoRoot, "kanban", "by-status");
const strict = Bun.argv.includes("--strict");
const includeDone = Bun.argv.includes("--include-done");

const statusOrder = [
  "01-todo",
  "02-ready",
  "03-in-progress",
  "04-verify",
  "05-integration",
  "06-done",
  "07-blocked",
  "08-deferred",
  "09-awaiting-review",
];

const readMarkdownFiles = (dir: string): string[] =>
  readdirSync(dir)
    .filter((file) => /^TWF-\d+.*\.md$/u.test(file))
    .sort();

const frontmatterValue = (text: string, key: string): string | undefined =>
  text.match(new RegExp(`^${key}:\\s*(.+)$`, "mu"))?.[1]?.trim();

const frontmatterList = (text: string, key: string): string[] => {
  const lines = text.match(/^---\n([\s\S]*?)\n---/u)?.[1]?.split(/\r?\n/u) ?? [];
  const start = lines.findIndex((line) => line === `${key}:`);
  if (start < 0) {
    return [];
  }

  const tail = lines.slice(start + 1);
  const end = tail.findIndex((line) => /^[a-zA-Z_]+:/u.test(line));
  return tail
    .slice(0, end < 0 ? undefined : end)
    .map((line) => line.match(/^\s+-\s+(.+)$/u)?.[1]?.trim())
    .filter((value): value is string => value !== undefined && value.length > 0);
};

const parseTicket = (file: string): Ticket => {
  const text = readFileSync(join(allTicketsDir, file), "utf8");
  const id = frontmatterValue(text, "ticket") ?? file.match(/^TWF-\d+/u)?.[0] ?? file;
  const title = text.match(/^#\s+(TWF-\d+:\s+.+)$/mu)?.[1] ?? `${id}: ${file}`;
  return {
    dependencies: frontmatterList(text, "dependencies").flatMap((dependency) => dependency.match(/TWF-\d+/gu) ?? []),
    docOnly: frontmatterValue(text, "doc_only") === "true",
    file,
    id,
    requiresQa: frontmatterValue(text, "requires_qa") === "true",
    role: frontmatterValue(text, "role") ?? "unknown",
    text,
    title,
    writeScope: frontmatterList(text, "write_scope"),
  };
};

const ticketFiles = readMarkdownFiles(allTicketsDir);
const tickets = ticketFiles.map(parseTicket);
const ticketsById = new Map(tickets.map((ticket) => [ticket.id, ticket]));

const statusLinks: StatusLink[] = statusOrder.flatMap((status) => {
  const dir = join(statusDir, status);
  return readdirSync(dir)
    .filter((file) => file.endsWith(".md"))
    .map((file) => ({
      file,
      id: file.match(/^TWF-\d+/u)?.[0] ?? file,
      linkPath: join(dir, file),
      status,
    }));
});

const statusByTicket = statusLinks.reduce<Map<string, string[]>>((map, link) => {
  map.set(link.id, [...(map.get(link.id) ?? []), link.status]);
  return map;
}, new Map());

const done = new Set(statusLinks.filter((link) => link.status === "06-done").map((link) => link.id));
const deferred = new Set(statusLinks.filter((link) => link.status === "08-deferred").map((link) => link.id));

const formatTicket = (id: string): string => {
  const ticket = ticketsById.get(id);
  return ticket === undefined ? id : `${id} ${ticket.title.replace(/^TWF-\d+:\s*/u, "")}`;
};

const brokenLinks = statusLinks
  .filter((link) => lstatSync(link.linkPath).isSymbolicLink() && !existsSync(link.linkPath))
  .map((link) => relative(repoRoot, link.linkPath));

const duplicateStatuses = Array.from(statusByTicket.entries())
  .filter(([, statuses]) => statuses.length > 1)
  .map(([id, statuses]) => `${formatTicket(id)} -> ${statuses.join(", ")}`);

const missingStatus = tickets
  .filter((ticket) => !statusByTicket.has(ticket.id))
  .map((ticket) => formatTicket(ticket.id));

const missingDependencyFiles = tickets
  .flatMap((ticket) => ticket.dependencies.filter((dependency) => !ticketsById.has(dependency)).map((dependency) => `${formatTicket(ticket.id)} -> ${dependency}`));

const dependencyStatus = (dependency: string): string => statusByTicket.get(dependency)?.join(",") ?? "missing-status";

const todoTickets = tickets.filter((ticket) => statusByTicket.get(ticket.id)?.includes("01-todo"));
const readyCandidates = todoTickets
  .filter((ticket) => ticket.dependencies.every((dependency) => done.has(dependency)))
  .map((ticket) => formatTicket(ticket.id));

const blockedTodo = todoTickets
  .filter((ticket) => ticket.dependencies.some((dependency) => !done.has(dependency)))
  .map((ticket) => {
    const blockers = ticket.dependencies.filter((dependency) => !done.has(dependency)).map((dependency) => `${dependency}:${dependencyStatus(dependency)}`);
    return `${formatTicket(ticket.id)} blocked_by=${blockers.join(",")}`;
  });

const activeTickets = tickets.filter((ticket) => {
  const statuses = statusByTicket.get(ticket.id) ?? [];
  return includeDone || (!statuses.includes("06-done") && !statuses.includes("08-deferred"));
});

const missingVerification = activeTickets
  .filter((ticket) => !/^## Verification\b/mu.test(ticket.text))
  .map((ticket) => formatTicket(ticket.id));

const missingImplementationNotes = activeTickets
  .filter((ticket) => !ticket.docOnly && !/^## Implementation Notes\b/mu.test(ticket.text))
  .map((ticket) => formatTicket(ticket.id));

const normalizeScope = (scope: string): string =>
  scope
    .replace(/`/gu, "")
    .replace(/\/\*\*$/u, "/")
    .replace(/\*.*$/u, "")
    .trim();

const scopesMayOverlap = (left: string, right: string): boolean => {
  const normalizedLeft = normalizeScope(left);
  const normalizedRight = normalizeScope(right);
  if (normalizedLeft === "" || normalizedRight === "") {
    return false;
  }

  return normalizedLeft === normalizedRight || normalizedLeft.startsWith(normalizedRight) || normalizedRight.startsWith(normalizedLeft);
};

const overlapRelevantScope = (scope: string): boolean =>
  ![
    "docs/TEST_PLAN.md",
    "docs/HANDOFF.md",
    "kanban/all-tickets/**",
    "kanban/by-status/**",
  ].includes(scope);

const readyTickets = todoTickets.filter(
  (ticket) =>
    ticket.role === "worker" &&
    !ticket.docOnly &&
    ticket.dependencies.every((dependency) => done.has(dependency)),
);
const writeScopeOverlaps = readyTickets.flatMap((left, leftIndex) =>
  readyTickets.slice(leftIndex + 1).flatMap((right) =>
    left.writeScope.filter(overlapRelevantScope).flatMap((leftScope) =>
      right.writeScope
        .filter(overlapRelevantScope)
        .filter((rightScope) => scopesMayOverlap(leftScope, rightScope))
        .map((rightScope) => `${left.id} <-> ${right.id}: ${leftScope} ~= ${rightScope}`),
    ),
  ),
);

const git = (args: string[], cwd = repoRoot): string =>
  execFileSync("git", args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();

const worktreePaths = (): string[] => {
  const output = git(["worktree", "list", "--porcelain"]);
  const rootRealPath = realpathSync(repoRoot);
  return output
    .split(/\r?\n/u)
    .filter((line) => line.startsWith("worktree "))
    .map((line) => line.replace(/^worktree\s+/u, ""))
    .filter((worktreePath) => existsSync(worktreePath))
    .filter((worktreePath) => realpathSync(worktreePath) !== rootRealPath);
};

const worktreeKanbanChanges = worktreePaths().flatMap((worktreePath) => {
  const status = git(["status", "--short", "--", "kanban"], worktreePath);
  if (status === "") {
    return [];
  }

  return status.split(/\r?\n/u).map((line) => `${basename(worktreePath)} ${line}`);
});

const audit: Audit = {
  blockedTodo,
  brokenLinks,
  duplicateStatuses,
  missingDependencyFiles,
  missingImplementationNotes,
  missingStatus,
  missingVerification,
  readyCandidates,
  worktreeKanbanChanges,
  writeScopeOverlaps,
};

const printSection = (title: string, lines: string[]): void => {
  console.log(`\n## ${title}`);
  if (lines.length === 0) {
    console.log("- none");
    return;
  }

  lines.map((line) => `- ${line}`).forEach((line) => console.log(line));
};

const criticalCount =
  audit.brokenLinks.length +
  audit.duplicateStatuses.length +
  audit.missingDependencyFiles.length +
  audit.missingStatus.length +
  audit.worktreeKanbanChanges.length;

console.log("# Wildfire Kanban Audit");
console.log(`repo=${repoRoot}`);
console.log(`tickets=${tickets.length}`);
console.log(`status_links=${statusLinks.length}`);
console.log(`critical_findings=${criticalCount}`);

printSection("Ready Candidates", audit.readyCandidates);
printSection("Todo Blocked By Dependencies", audit.blockedTodo);
printSection("Potential Ready Write-Scope Overlaps", audit.writeScopeOverlaps);
printSection("Missing Implementation Notes", audit.missingImplementationNotes);
printSection("Missing Verification Sections", audit.missingVerification);
printSection("Broken Status Symlinks", audit.brokenLinks);
printSection("Duplicate Ticket Statuses", audit.duplicateStatuses);
printSection("Missing Ticket Status", audit.missingStatus);
printSection("Missing Dependency Files", audit.missingDependencyFiles);
printSection("Worktree Kanban Changes", audit.worktreeKanbanChanges);

if (strict && criticalCount > 0) {
  process.exit(1);
}
