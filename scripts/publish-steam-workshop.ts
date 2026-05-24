import { existsSync, statSync } from "node:fs";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, isAbsolute, resolve } from "node:path";
import { spawnSync } from "node:child_process";

type Options = {
  dryRun: boolean;
  skipPreview: boolean;
  publishedFileId: string;
  user?: string;
  vdfPath: string;
  sourceImagePath: string;
  previewImagePath: string;
  contentFolderPath: string;
};

const root = resolve(import.meta.dir, "..");

const usage = `Usage: bun scripts/publish-steam-workshop.ts [options]

Options:
  --user <account>       Steam account name. Defaults to STEAM_USER.
  --published-file-id <id>
                         Workshop item id. Default: STEAM_WORKSHOP_PUBLISHED_FILE_ID or 3730392791.
  --vdf <path>           Workshop item VDF. Default: release/workshop/wildfire-workshop-item.vdf
  --content-folder <path>
                         Workshop content folder. Default: release/workshop/content.
  --source <path>        Tracked thumbnail source image. Default: release/workshop/wildfire-workshop-thumbnail.png
  --preview <path>       Generated preview image. Default: release/workshop/wildfire-workshop-thumbnail.jpg
  --skip-preview         Do not regenerate the preview image.
  --dry-run              Validate and print the SteamCMD command without publishing.
  --help                 Show this help.

STEAM_PASSWORD is optional. If omitted, SteamCMD prompts for the password and Steam Guard code.`;

function parseArgs(argv: string[]): Options {
  const options: Options = {
    dryRun: false,
    skipPreview: false,
    publishedFileId: process.env.STEAM_WORKSHOP_PUBLISHED_FILE_ID ?? "3730392791",
    user: process.env.STEAM_USER,
    vdfPath: "release/workshop/wildfire-workshop-item.vdf",
    contentFolderPath: "release/workshop/content",
    sourceImagePath: "release/workshop/wildfire-workshop-thumbnail.png",
    previewImagePath: "release/workshop/wildfire-workshop-thumbnail.jpg",
  };

  argv.reduce((skipNext, arg, index) => {
    if (skipNext) {
      return false;
    }

    const readValue = (name: string): string => {
      const value = argv[index + 1];
      if (!value || value.startsWith("--")) {
        throw new Error(`${name} requires a value.`);
      }
      return value;
    };

    switch (arg) {
      case "--help":
        console.log(usage);
        process.exit(0);
      case "--dry-run":
        options.dryRun = true;
        return false;
      case "--skip-preview":
        options.skipPreview = true;
        return false;
      case "--user":
        options.user = readValue(arg);
        return true;
      case "--published-file-id":
        options.publishedFileId = readValue(arg);
        return true;
      case "--vdf":
        options.vdfPath = readValue(arg);
        return true;
      case "--content-folder":
        options.contentFolderPath = readValue(arg);
        return true;
      case "--source":
        options.sourceImagePath = readValue(arg);
        return true;
      case "--preview":
        options.previewImagePath = readValue(arg);
        return true;
      default:
        throw new Error(`Unknown argument: ${arg}`);
    }
  }, false);

  return options;
}

function absolutePath(path: string): string {
  return isAbsolute(path) ? path : resolve(root, path);
}

function requireFile(path: string, label: string): void {
  if (!existsSync(path) || !statSync(path).isFile()) {
    throw new Error(`${label} does not exist or is not a file: ${path}`);
  }
}

function requireDirectory(path: string, label: string): void {
  if (!existsSync(path) || !statSync(path).isDirectory()) {
    throw new Error(`${label} does not exist or is not a directory: ${path}`);
  }
}

function runCommand(command: string, args: string[], dryRun: boolean): void {
  console.log([command, ...args.map((arg) => (arg.includes(" ") ? `"${arg}"` : arg))].join(" "));

  if (dryRun) {
    return;
  }

  const result = spawnSync(command, args, {
    cwd: root,
    stdio: "inherit",
    env: process.env,
  });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    throw new Error(`${command} exited with status ${result.status ?? "unknown"}.`);
  }
}

function parseVdfValue(vdf: string, key: string): string | undefined {
  return new RegExp(`"${key}"\\s+"([^"]*)"`).exec(vdf)?.[1];
}

async function main(): Promise<void> {
  const options = parseArgs(process.argv.slice(2));
  const vdfPath = absolutePath(options.vdfPath);
  const contentFolderPath = absolutePath(options.contentFolderPath);
  const sourceImagePath = absolutePath(options.sourceImagePath);
  const previewImagePath = absolutePath(options.previewImagePath);
  const steamUser = options.user;

  requireFile(sourceImagePath, "Cover source image");

  if (!options.skipPreview) {
    await mkdir(dirname(previewImagePath), { recursive: true });
    runCommand(
      "magick",
      [
        sourceImagePath,
        "-resize",
        "1920x1080^",
        "-gravity",
        "center",
        "-extent",
        "1920x1080",
        "-strip",
        "-quality",
        "82",
        previewImagePath,
      ],
      options.dryRun,
    );
  }

  requireFile(previewImagePath, "Workshop preview image");
  const previewSizeLimitBytes = 1_000_000;
  const previewSizeBytes = statSync(previewImagePath).size;

  if (previewSizeBytes >= previewSizeLimitBytes) {
    throw new Error(
      `Workshop preview image is ${previewSizeBytes} bytes; Steam requires less than ${previewSizeLimitBytes} bytes.`,
    );
  }

  const vdf = existsSync(vdfPath)
    ? await readFile(vdfPath, "utf8")
    : createDefaultVdf(options.publishedFileId, contentFolderPath, previewImagePath);
  const contentFolder = parseVdfValue(vdf, "contentfolder");
  const publishedFileId = parseVdfValue(vdf, "publishedfileid");

  if (!contentFolder || !publishedFileId) {
    throw new Error("Workshop VDF must include contentfolder, previewfile, and publishedfileid.");
  }

  requireDirectory(contentFolder, "VDF contentfolder");
  requireFile(resolve(contentFolder, "version-1.0", "manifest.json"), "Workshop mod manifest");

  if (!steamUser) {
    throw new Error("Missing Steam account. Pass --user <account> or set STEAM_USER.");
  }

  const generatedVdfPath = resolve(root, "release/workshop/wildfire-workshop-item.generated.vdf");
  const generatedVdf = vdf.replace(/"previewfile"\s+"[^"]*"/, `"previewfile"\t\t"${previewImagePath}"`);
  await writeFile(generatedVdfPath, generatedVdf);

  const steamCmdArgs = [
    "+login",
    steamUser,
    ...(process.env.STEAM_PASSWORD ? [process.env.STEAM_PASSWORD] : []),
    "+workshop_build_item",
    generatedVdfPath,
    "+quit",
  ];

  await writeFile(
    resolve(root, "release/workshop/last-publish-command.txt"),
    [`steamcmd ${steamCmdArgs.map((arg) => (arg === process.env.STEAM_PASSWORD ? "<STEAM_PASSWORD>" : arg)).join(" ")}`, ""].join("\n"),
  );

  console.log(`Publishing Steam Workshop item ${publishedFileId} from ${contentFolder}`);
  runCommand("steamcmd", steamCmdArgs, options.dryRun);
}

main().catch((error: unknown) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});

function createDefaultVdf(publishedFileId: string, contentFolder: string, previewFile: string): string {
  return `"workshopitem"
{
\t"appid"\t\t"1062090"
\t"publishedfileid"\t\t"${publishedFileId}"
\t"contentfolder"\t\t"${contentFolder}"
\t"previewfile"\t\t"${previewFile}"
\t"visibility"\t\t"0"
\t"title"\t\t"[ModContest1] Wildfire"
\t"description"\t\t"Wildfire brings fire to Timberborn as a living world system, not just a visual effect. Fires can spread through forests and settlements, leave burned terrain behind, generate smoke and steam, and begin tying into consequences for the colony around them."
\t"changenote"\t\t"Automated Workshop update from the steam branch."
}
`;
}
