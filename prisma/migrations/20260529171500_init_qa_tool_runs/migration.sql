CREATE TABLE "tools" (
  "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "name" TEXT NOT NULL,
  "category" TEXT NOT NULL DEFAULT 'other',
  "owner" TEXT NOT NULL DEFAULT 'qa',
  "created_at" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "retired_at" DATETIME
);

CREATE TABLE "tool_runs" (
  "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "tool_id" INTEGER NOT NULL,
  "started_at" DATETIME NOT NULL,
  "finished_at" DATETIME NOT NULL,
  "command" TEXT NOT NULL,
  "git_sha" TEXT,
  "timberborn_version" TEXT,
  "display_resolution" TEXT,
  "ui_scale" TEXT,
  "result" TEXT NOT NULL,
  CONSTRAINT "tool_runs_tool_id_fkey" FOREIGN KEY ("tool_id") REFERENCES "tools" ("id") ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT "tool_runs_result_check" CHECK ("result" IN ('pass', 'fail', 'blocked', 'unknown'))
);

CREATE TABLE "failures" (
  "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "run_id" INTEGER NOT NULL,
  "class" TEXT NOT NULL,
  "reason" TEXT NOT NULL,
  "detail" TEXT NOT NULL DEFAULT '',
  CONSTRAINT "failures_run_id_fkey" FOREIGN KEY ("run_id") REFERENCES "tool_runs" ("id") ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT "failures_class_check" CHECK ("class" IN ('tool_failure', 'environment_failure', 'product_failure', 'test_design_failure', 'unknown'))
);

CREATE TABLE "artifacts" (
  "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
  "run_id" INTEGER NOT NULL,
  "type" TEXT NOT NULL,
  "path" TEXT NOT NULL,
  "sha256" TEXT,
  CONSTRAINT "artifacts_run_id_fkey" FOREIGN KEY ("run_id") REFERENCES "tool_runs" ("id") ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE UNIQUE INDEX "tools_name_key" ON "tools"("name");

CREATE INDEX "idx_tool_runs_tool_id_started_at" ON "tool_runs"("tool_id", "started_at");

CREATE INDEX "idx_failures_run_id_class" ON "failures"("run_id", "class");

CREATE INDEX "idx_artifacts_run_id" ON "artifacts"("run_id");
