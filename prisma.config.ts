import { defineConfig } from "prisma/config";
import { resolve } from "path";

export default defineConfig({
  datasource: {
    url: process.env.DATABASE_URL ?? `file:${resolve("qa/tool-runs.sqlite")}`,
  },
  schema: "prisma/schema.prisma",
});
