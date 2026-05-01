import { mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from "fs";
import { dirname, join, relative } from "path";

type JsonValue = null | boolean | number | string | JsonValue[] | { [key: string]: JsonValue };

type BlueprintRecord = {
  path: string;
  category: string;
  specs: string[];
};

type SpecRecord = {
  name: string;
  count: number;
  categories: string[];
  fields: Record<string, string[]>;
  examples: string[];
};

type CategorySpecRecord = {
  category: string;
  name: string;
  count: number;
  fields: Record<string, string[]>;
  examples: string[];
};

const blueprintRoot =
  `${process.env.HOME}/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/Blueprints`;

const guideDir = "doc/modding-guide";
const mainGuidePath = `${guideDir}/README.md`;
const blueprintReferencePath = "docs/reference/blueprint-reference.md";
const folktailsIconPath = relative(dirname(blueprintReferencePath), "docs/reference/assets/faction-folktails.png");
const ironTeethIconPath = relative(dirname(blueprintReferencePath), "docs/reference/assets/faction-ironteeth.png");

const readJson = (path: string): Record<string, JsonValue> =>
  JSON.parse(readFileSync(path, "utf8").replace(/^\uFEFF/, ""));

const walk = (dir: string): string[] =>
  readdirSync(dir)
    .map((name) => join(dir, name))
    .flatMap((path) => statSync(path).isDirectory() ? walk(path) : [path]);

const uniq = <T>(values: T[]): T[] => [...new Set(values)];

const sorted = (values: string[]): string[] => [...values].sort((a, b) => a.localeCompare(b));

const slug = (value: string): string =>
  value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");

const referenceSlug = (value: string): string =>
  slug(value.replace(/([a-z0-9])([A-Z])/g, "$1-$2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1-$2"));

const blueprintAnchor = (path: string): string => `blueprint-${slug(path)}`;

const specAnchor = (category: string, spec: string): string => `spec-${slug(category)}-${slug(spec)}`;

const blueprintName = (path: string): string => {
  const fileName = path.split("/").at(-1) ?? path;
  const withoutSuffix = fileName.replace(/\.blueprint\.json$/, "");
  const parts = withoutSuffix.split(".");
  const typePrefixes = new Set([
    "BlockObjectToolGroup",
    "BonusType",
    "CustomCursor",
    "Decal",
    "Faction",
    "Firework",
    "Good",
    "GoodCollection",
    "GoodGroup",
    "IlluminationColor",
    "KeyBinding",
    "KeyBindingGroup",
    "MaterialCollection",
    "Need",
    "NeedCollection",
    "NeedGroup",
    "NewGameMode",
    "Recipe",
    "RemoveYieldStrategy",
    "TemplateCollection",
    "ToolGroup",
    "Tutorial",
    "WellbeingTier",
    "WorkerOutfit",
    "WorkerType",
  ]);
  const withoutTypePrefix = typePrefixes.has(parts[0] ?? "") ? parts.slice(1) : parts;
  const factionSuffixes = new Set(["Folktails", "IronTeeth"]);
  const withoutFaction =
    withoutTypePrefix.length > 1 && factionSuffixes.has(withoutTypePrefix.at(-1) ?? "")
      ? withoutTypePrefix.slice(0, -1)
      : withoutTypePrefix;
  return withoutFaction.join(".") || withoutSuffix;
};

const blueprintStem = (path: string): string => {
  const fileName = path.split("/").at(-1) ?? path;
  return fileName.replace(/\.blueprint\.json$/i, "");
};

const faction = (path: string): "folktails" | "ironteeth" | undefined => {
  if (path.includes(".Folktails.")) {
    return "folktails";
  }
  if (path.includes(".IronTeeth.")) {
    return "ironteeth";
  }
  return undefined;
};

const factionIcon = (path: string): string => {
  const blueprintFaction = faction(path);
  if (blueprintFaction === "folktails") {
    return `![Folktails](${folktailsIconPath})`;
  }
  if (blueprintFaction === "ironteeth") {
    return `![Iron Teeth](${ironTeethIconPath})`;
  }
  return "";
};

const factionRank = (path: string): number => {
  const blueprintFaction = faction(path);
  if (blueprintFaction === "folktails") {
    return 0;
  }
  if (blueprintFaction === "ironteeth") {
    return 1;
  }
  return 2;
};

const compareBlueprintPaths = (a: string, b: string): number =>
  blueprintName(a).localeCompare(blueprintName(b)) || factionRank(a) - factionRank(b) || a.localeCompare(b);

const sortBlueprintPaths = (paths: string[]): string[] => [...paths].sort(compareBlueprintPaths);

const referenceLink = (label: string, referenceId: string): string => `[${label}][${referenceId}]`;

const codeReferenceLink = (label: string, referenceId: string): string => referenceLink(`\`${label}\``, referenceId);

const isChildrenSpec = (spec: string): boolean => spec === "Children";

const formatChildrenSpecDescription = (sourceLabel: string): string[] => [
  "Child map:",
  "",
  "- Keys are entity child names from individual blueprints, not reusable Spec properties.",
  [
    "- Values are child entity fragments that can contain Specs such as",
    "`TimbermeshSpec`, `TransformSpec`, `CollidersSpec`, nested `Children`, or `BlueprintPath` references.",
  ].join(" "),
  `- Treat the ${sourceLabel} as the source for exact child names and shapes.`,
];

const normalizeTypes = (types: string[]): string[] => {
  const uniqueTypes = sorted(uniq(types));
  const hasSpecificArray = uniqueTypes.some((type) => type.endsWith("[]"));
  const hasSpecificObject = uniqueTypes.some((type) => type.startsWith("{ ") && type !== "{}");
  return uniqueTypes.filter(
    (type) => !(type === "array" && hasSpecificArray) && !(type === "{}" && hasSpecificObject),
  );
};

const mergeTypeEntries = (entries: Array<Record<string, string[]>>): Record<string, string[]> =>
  entries.reduce<Record<string, string[]>>(
    (acc, entry) =>
      Object.entries(entry).reduce(
        (innerAcc, [field, types]) => ({
          ...innerAcc,
          [field]: normalizeTypes([...(innerAcc[field] ?? []), ...types]),
        }),
        acc,
      ),
    {},
  );

const objectShape = (value: Record<string, JsonValue>): string => {
  const entries = Object.entries(value).sort(([a], [b]) => a.localeCompare(b));
  return entries.length === 0
    ? "{}"
    : `{ ${entries.map(([field, fieldValue]) => `${field}: ${valueType(fieldValue)}`).join("; ")} }`;
};

const arrayObjectShape = (values: Array<Record<string, JsonValue>>): string => {
  const fieldTypes = mergeTypeEntries(
    values.map((value) =>
      Object.entries(value).reduce<Record<string, string[]>>(
        (acc, [field, fieldValue]) => ({ ...acc, [field]: [valueType(fieldValue)] }),
        {},
      ),
    ),
  );
  const entries = Object.entries(fieldTypes).sort(([a], [b]) => a.localeCompare(b));
  return entries.length === 0
    ? "{}"
    : `{ ${entries
        .map(([field, types]) => {
          const optionalMarker = values.some((value) => !(field in value)) ? "?" : "";
          return `${field}${optionalMarker}: ${types.join(" | ")}`;
        })
        .join("; ")} }`;
};

const arrayType = (type: string): string => {
  const needsGrouping = type.includes(" | ");
  return `${needsGrouping ? `(${type})` : type}[]`;
};

const valueType = (value: JsonValue): string => {
  if (value === null) {
    return "null";
  }
  if (Array.isArray(value)) {
    const objectValues = value.filter(
      (item): item is Record<string, JsonValue> =>
        item !== null && typeof item === "object" && !Array.isArray(item),
    );
    if (objectValues.length === value.length && objectValues.length > 0) {
      return arrayType(arrayObjectShape(objectValues));
    }
    const itemTypes = sorted(uniq(value.map(valueType)));
    return itemTypes.length === 0 ? "array" : arrayType(itemTypes.join(" | "));
  }
  if (typeof value === "object") {
    return objectShape(value);
  }
  return typeof value;
};

const mergeFieldTypes = (
  existing: Record<string, string[]>,
  fields: Record<string, string[]>,
): Record<string, string[]> =>
  mergeTypeEntries([existing, fields]);

const specFields = (value: JsonValue): Record<string, string[]> =>
  value && typeof value === "object" && !Array.isArray(value)
    ? Object.entries(value).reduce<Record<string, string[]>>(
        (acc, [field, fieldValue]) => ({ ...acc, [field]: [valueType(fieldValue)] }),
        {},
      )
    : {};

const formatFields = (fields: Record<string, string[]>): string[] => {
  const entries = Object.entries(fields).sort(([a], [b]) => a.localeCompare(b));
  return entries.length === 0
    ? []
    : [
        "| Property | Type |",
        "| --- | --- |",
        ...entries.map(
          ([field, types]) => `| \`${field}\` | \`${types.join(" | ").replaceAll("|", "\\|")}\` |`,
        ),
      ];
};

const formatPropertiesSection = (fields: Record<string, string[]>): string[] => {
  const formattedFields = formatFields(fields);
  return formattedFields.length === 0 ? [] : ["Properties:", "", ...formattedFields, ""];
};

const jsonFiles = walk(blueprintRoot).filter((path) => path.endsWith(".json"));

const blueprints: BlueprintRecord[] = jsonFiles
  .map((filePath) => {
    const path = relative(blueprintRoot, filePath);
    const json = readJson(filePath);
    return {
      path,
      category: path.split("/")[0],
      specs: Object.keys(json).sort((a, b) => a.localeCompare(b)),
    };
  })
  .sort((a, b) => a.path.localeCompare(b.path));

const blueprintReferencePrimarySeed = (path: string): string => {
  const blueprintFaction = faction(path);
  return blueprintFaction ? `${referenceSlug(blueprintName(path))}-${blueprintFaction}` : referenceSlug(blueprintName(path));
};

const blueprintReferenceSecondarySeed = (path: string): string => {
  const blueprintFaction = faction(path);
  return blueprintFaction ? blueprintReferencePrimarySeed(path) : referenceSlug(blueprintStem(path));
};

const groupPathsBySeed = (seedForPath: (path: string) => string): Record<string, string[]> =>
  blueprints.reduce<Record<string, string[]>>(
    (acc, blueprint) => ({
      ...acc,
      [seedForPath(blueprint.path)]: [...(acc[seedForPath(blueprint.path)] ?? []), blueprint.path],
    }),
    {},
  );

const primaryReferenceEntries = Object.entries(groupPathsBySeed(blueprintReferencePrimarySeed)).flatMap(
  ([seed, paths]) => (paths.length === 1 ? [[paths[0] ?? "", seed] as const] : []),
);

const secondaryReferenceEntries = Object.values(groupPathsBySeed(blueprintReferencePrimarySeed))
  .filter((paths) => paths.length > 1)
  .flatMap((paths) => paths)
  .map((path) => [path, blueprintReferenceSecondarySeed(path)] as const);

const duplicatedSecondarySeeds = new Set(
  Object.values(
    secondaryReferenceEntries.reduce<Record<string, string[]>>(
      (acc, [path, seed]) => ({ ...acc, [seed]: [...(acc[seed] ?? []), path] }),
      {},
    ),
  )
    .filter((paths) => paths.length > 1)
    .flatMap((paths) => paths),
);

const blueprintReferenceIds = new Map([
  ...primaryReferenceEntries,
  ...secondaryReferenceEntries.map(
    ([path, seed]) =>
      [
        path,
        duplicatedSecondarySeeds.has(path) ? `${seed}-${referenceSlug(path.split("/").slice(0, -1).join("-"))}` : seed,
      ] as const,
  ),
]);

const blueprintReferenceId = (path: string): string => blueprintReferenceIds.get(path) ?? blueprintReferencePrimarySeed(path);

const specReferenceId = (category: string, spec: string): string => specAnchor(category, spec);

const specLink = (category: string, spec: string): string => codeReferenceLink(spec, specReferenceId(category, spec));

const blueprintIconLink = (path: string): string => referenceLink(factionIcon(path), blueprintReferenceId(path));

const blueprintLink = (path: string): string => {
  const blueprintFaction = faction(path);
  return blueprintFaction
    ? [`\`${blueprintName(path)}\``, blueprintIconLink(path)].join(" ")
    : codeReferenceLink(blueprintName(path), blueprintReferenceId(path));
};

const blueprintGroupKey = (blueprint: BlueprintRecord): string =>
  faction(blueprint.path) ? `faction:${blueprint.category}:${blueprintName(blueprint.path)}` : `path:${blueprint.path}`;

const groupBlueprints = (entries: BlueprintRecord[]): BlueprintRecord[][] =>
  Object.values(
    entries.reduce<Record<string, BlueprintRecord[]>>(
      (acc, blueprint) => ({
        ...acc,
        [blueprintGroupKey(blueprint)]: [...(acc[blueprintGroupKey(blueprint)] ?? []), blueprint],
      }),
      {},
    ),
  ).map((group) => group.sort((a, b) => compareBlueprintPaths(a.path, b.path)));

const formatBlueprintGroup = (group: BlueprintRecord[]): string => {
  const sortedGroup = group.sort((a, b) => compareBlueprintPaths(a.path, b.path));
  const first = sortedGroup[0];
  if (!first) {
    return "";
  }
  const factionBlueprints = sortedGroup.filter((blueprint) => faction(blueprint.path));
  return factionBlueprints.length > 0
    ? [`\`${blueprintName(first.path)}\``, ...factionBlueprints.map((blueprint) => blueprintIconLink(blueprint.path))].join(" ")
    : blueprintLink(first.path);
};

const formatBlueprintList = (paths: string[]): string =>
  groupBlueprints(
    sortBlueprintPaths(paths)
      .map((path) => blueprints.find((blueprint) => blueprint.path === path))
      .filter((blueprint): blueprint is BlueprintRecord => Boolean(blueprint)),
  )
    .sort((a, b) => compareBlueprintPaths(a[0]?.path ?? "", b[0]?.path ?? ""))
    .map(formatBlueprintGroup)
    .join(", ");

const specRecords: SpecRecord[] = Object.values(
  blueprints.reduce<Record<string, SpecRecord>>((acc, blueprint) => {
    const json = readJson(join(blueprintRoot, blueprint.path));
    Object.entries(json).forEach(([specName, value]) => {
      const existing = acc[specName] ?? {
        name: specName,
        count: 0,
        categories: [],
        fields: {},
        examples: [],
      };
      acc[specName] = {
        name: specName,
        count: existing.count + 1,
        categories: sorted(uniq([...existing.categories, blueprint.category])),
        fields: mergeFieldTypes(existing.fields, specFields(value)),
        examples:
          existing.examples.length >= 5
            ? existing.examples
            : [...existing.examples, blueprint.path],
      };
    });
    return acc;
  }, {}),
).sort((a, b) => a.name.localeCompare(b.name));

const categorySpecRecords: CategorySpecRecord[] = Object.values(
  blueprints.reduce<Record<string, CategorySpecRecord>>((acc, blueprint) => {
    const json = readJson(join(blueprintRoot, blueprint.path));
    return Object.entries(json).reduce((innerAcc, [specName, value]) => {
      const key = `${blueprint.category}/${specName}`;
      const existing = innerAcc[key] ?? {
        category: blueprint.category,
        name: specName,
        count: 0,
        fields: {},
        examples: [],
      };
      return {
        ...innerAcc,
        [key]: {
          category: blueprint.category,
          name: specName,
          count: existing.count + 1,
          fields: mergeFieldTypes(existing.fields, specFields(value)),
          examples: [...existing.examples, blueprint.path],
        },
      };
    }, acc);
  }, {}),
).sort((a, b) => a.category.localeCompare(b.category) || a.name.localeCompare(b.name));

const categories = Object.entries(
  blueprints.reduce<Record<string, number>>(
    (acc, blueprint) => ({ ...acc, [blueprint.category]: (acc[blueprint.category] ?? 0) + 1 }),
    {},
  ),
).sort(([a], [b]) => a.localeCompare(b));

const categoryDetails = categories
  .map(([category]) => {
    const entries = [...blueprints]
      .filter((blueprint) => blueprint.category === category)
      .sort((a, b) => compareBlueprintPaths(a.path, b.path));
    const groupedEntries = groupBlueprints(entries).sort((a, b) =>
      compareBlueprintPaths(a[0]?.path ?? "", b[0]?.path ?? ""),
    );
    const specs = sorted(uniq(entries.flatMap((blueprint) => blueprint.specs)));
    return [
      `### ${category}`,
      "",
      `Blueprints: ${entries.length}`,
      "",
      `Specs seen: ${specs.map((spec) => specLink(category, spec)).join(", ") || "none"}`,
      "",
      ...groupedEntries.map(
        (group) =>
          [
            `- ${group.map((blueprint) => `<a id="${blueprintAnchor(blueprint.path)}"></a>`).join("")}${formatBlueprintGroup(group)}`,
            sorted(uniq(group.flatMap((blueprint) => blueprint.specs)))
              .map((spec) => specLink(category, spec))
              .join(", ") || "none",
          ].join("—"),
      ),
      "",
    ].join("\n");
  })
  .join("\n");

const specReference = categories
  .map(([category]) => {
    const specs = categorySpecRecords.filter((spec) => spec.category === category);
    return [
      `### ${category}`,
      "",
      ...specs.flatMap((spec) => [
        `<a id="${specAnchor(category, spec.name)}"></a>`,
        "",
        `#### ${spec.name}`,
        "",
        ...(isChildrenSpec(spec.name)
          ? formatChildrenSpecDescription("linked blueprints")
          : formatPropertiesSection(spec.fields)),
        `Blueprints (${spec.count}):`,
        "",
        formatBlueprintList(spec.examples),
        "",
      ]),
    ].join("\n");
  })
  .join("\n");

const globalSpecIndex = specRecords
  .map((spec) => [
    `### ${spec.name}`,
    "",
    `Seen in ${spec.count} blueprint${spec.count === 1 ? "" : "s"}.`,
    "",
    `Categories: ${spec.categories.map((category) => codeReferenceLink(category, specReferenceId(category, spec.name))).join(", ")}`,
    "",
    ...(isChildrenSpec(spec.name)
      ? formatChildrenSpecDescription("examples")
      : formatPropertiesSection(spec.fields)),
    "Examples:",
    "",
    formatBlueprintList(spec.examples),
    "",
  ].join("\n"))
  .join("\n");

const referenceDefinitions = [
  ...blueprints.map(
    (blueprint) => `[${blueprintReferenceId(blueprint.path)}]: #${blueprintAnchor(blueprint.path)}`,
  ),
  ...categorySpecRecords.map(
    (spec) => `[${specReferenceId(spec.category, spec.name)}]: #${specAnchor(spec.category, spec.name)}`,
  ),
]
  .sort((a, b) => a.localeCompare(b))
  .join("\n");

const blueprintReference = `# Timberborn 1.0+ Blueprint Reference

This file is generated from the installed Timberborn 1.0+ blueprint corpus at:

\`${blueprintRoot}\`

It contains every Spec and every Blueprint discovered locally on 2026-05-01. Property types are inferred from the installed JSON values, including nested object shapes. Empty arrays are shown as \`array\` because the local value does not expose an item shape.

## Blueprint Category Summary

${categories.map(([category, count]) => `- \`${category}\`: ${count}`).join("\n")}

## Specs By Category

${specReference}

## Global Spec Index

${globalSpecIndex}

## Every Blueprint

${categoryDetails}

${referenceDefinitions}
`;

const guide = `# Timberborn 1.0+ Modding Guide

This guide is intentionally scoped to Timberborn 1.0 and newer. It focuses on the current official Blueprint, asset, Unity tooling, and code-extension standards.

Generated on 2026-05-01 from the installed Timberborn blueprint corpus at:

\`${blueprintRoot}\`

The corpus contains ${blueprints.length} blueprint files, ${categories.length} top-level blueprint categories, and ${specRecords.length} unique Spec keys.

The complete generated inventory lives in [blueprint-reference.md](blueprint-reference.md).

## Guide Decisions

- This is both a general Timberborn modding handbook and the upstream Wildfire Timberborn adapter guide.
- Generated reference material is split under \`docs/reference/\` so the main guide stays readable.
- Code examples should be C# unless a section is explicitly about local automation.
- Prometheus and Wildfire policies are included as quotes and suggestions, not as universal Timberborn rules.
- Discord research is approved, but this environment needs exported threads, screenshots, or access to relevant modding channels before it can cite that material.

## Wildfire And Prometheus Guidance

These are project-tested suggestions. Treat them as defaults for Wildfire and as strong prompts for other large Timberborn mods.

> Keep the simulation core host-agnostic.

> Timberborn is an adapter and should translate game state into the domain model rather than own the domain rules.

> Prefer deterministic tests and CLI scenarios before live Timberborn validation.

> A mod is not live-validated until Timberborn loads it, the target save opens, the relevant UI or command path is exercised, and logs/screenshots prove the expected behavior.

> Anything inside the shippable mod tree can end up in the user's deployed mod. Keep internal docs, agent notes, and planning artifacts outside deployed content.

## Source Baseline

- Official 1.0+ modding path: the official \`mechanistry/timberborn-modding\` Unity project and wiki.
- Installed game assets: Blueprints, localizations, shaders, UI, and editor DLLs under \`StreamingAssets/Modding\`.
- Official examples: \`HelloWorld\`, \`ShantySpeaker\`, \`OverwritesExample\`, and \`YearOfTheSnakeBeaverTails\` in \`~/repos/timberborn-modding/Assets/Mods\`.
- Community examples considered only when they are plausibly 1.0+ compatible: current DatVM/Luke TimberbornMods documentation and source, eMkaQQ's Mod Settings and 1.0 mod sources, public BobingAbout GitHub/Workshop evidence, plus recent Reddit posts about 1.0 building blueprints and modding pain points.
- Discord was not directly accessible from this Codex environment. The public Reddit signal is that users are being directed to a Timberborn modding Discord for specialized blueprint/model help, but this guide does not quote or infer private Discord content.

## What 1.0 Changed

Timberborn 1.0 makes JSON Blueprints the center of modding. The game now defines virtually every in-game object through \`.blueprint.json\` files, and modders get the same data surface for additions and overrides. The official tools import Blueprints, localizations, UI styles/views, shaders, and editor tooling from the game build.

The practical consequence: start with data before code. If the target can be expressed as goods, needs, recipes, buildings, tool groups, templates, worker outfits, keybindings, decals, or configuration changes, make the smallest Blueprint first. Add C# only when you need runtime behavior, UI logic, world sampling, custom simulation, a new component, or a service binding.

## Mod Folder Model

A built mod has this conceptual shape:

\`\`\`text
MyMod/
manifest.json
AssetBundles/
Blueprint category folders...
Localizations/
Sprites/
Materials/
Scripts/
\`\`\`

In the official Unity tools, the source shape is:

\`\`\`text
Assets/Mods/MyMod/
manifest.json
Data/
Root/
Scripts/
AssetBundles/
\`\`\`

The Mod Builder copies \`Data/\` into the deployed mod version folder, copies \`Root/\` into the mod root, copies \`manifest.json\`, optionally copies built DLL/PDB files, and optionally builds Windows/macOS AssetBundles. That means a Unity-tool source file at:

\`\`\`text
Assets/Mods/MyMod/Data/Goods/Good.MyGood.blueprint.json
\`\`\`

deploys as:

\`\`\`text
~/Documents/Timberborn/Mods/MyMod/Goods/Good.MyGood.blueprint.json
\`\`\`

Prometheus used a direct deploy script instead of the Unity Mod Builder. That works too, but the same rule applies: shippable data belongs in the deployed mod tree; internal docs and agent notes do not.

## Manifest

Every mod needs \`manifest.json\`. Treat it as the stable identity and compatibility contract.

Common fields:

- \`Name\`: user-facing mod name in the mod manager.
- \`Version\`: mod version.
- \`Id\`: stable unique mod id.
- \`MinimumGameVersion\`: lowest supported Timberborn version.
- \`RequiredMods\`: hard dependencies.
- \`OptionalMods\`: soft compatibility dependencies.

Pattern:

- Keep \`Id\` stable even if \`Name\` changes.
- Prefer a narrow \`MinimumGameVersion\` for 1.0+ mods instead of claiming broad compatibility.
- Put per-game-version payloads under \`version-<game-version>\` only when you intentionally ship multiple compatibility layers.
- Put reusable libraries in their own mod with a stable \`Id\`, then list them under \`RequiredMods\` from consumer mods. Current 1.0 examples include eMka's \`Mod Settings\` and Luke's \`Moddable Timberborn\`, \`TimberUi\`, and related helper-library pattern.

### Mod Settings Pattern

The current eMkaQQ Mod Settings source is a strong 1.0+ reference for configurable mods:

- Create a \`ModSettingsOwner\` for your mod.
- Declare public \`ModSetting<T>\` properties for values that should appear in the settings UI.
- Override \`ModId\` with the same stable mod id used in \`manifest.json\`.
- Bind the owner in the \`MainMenu\` context, and in \`Game\` too if settings need in-game changes.
- Use \`ChangeableOn\`, ranges, dropdown factories, enable predicates, and custom setting elements for richer controls.
- Store only simple user-facing settings in this layer. Game-state persistence still belongs in the game's save/load services.

Settings are a compatibility contract. Give them stable keys, choose conservative defaults, and make disabling the mod leave saves in a recoverable state whenever possible.

## Blueprint Merge Rules

Blueprints are path-based:

- Same relative path as a game Blueprint: modify the existing Blueprint.
- New relative path: add a new Blueprint.
- \`.optional.blueprint.json\`: apply only if the target already exists, useful for compatibility with another mod.

Field behavior:

- A regular field replaces the original value.
- An omitted field leaves the original value alone.
- \`#append\` adds list items and, in 1.0, ignores duplicates.
- \`#remove\` removes list items.
- \`#delete\` deletes an existing property or Spec.

Use \`#delete\` deliberately. It is powerful enough to remove built-in Specs from a Blueprint, which is exactly what you want for some overrides and exactly how you can create hard-to-debug missing behavior if used casually.

## Choosing Blueprint, Code, Or AssetBundle

Use Blueprint-only mods for:

- Tuning goods, recipes, needs, wellbeing effects, science costs, building costs, capacities, tool groups, keybindings, decals, natural resources, and template collection membership.
- Adding simple goods, needs, recipes, and many data-defined buildings.
- Overriding existing values through minimal merge files.

Use C# code for:

- Runtime systems, simulation, custom UI logic, component behavior, save/load coordination, command bridges, custom services, and integration with non-public or version-sensitive game behavior.
- New component Specs, by defining a \`ComponentSpec\` record and registering a decorator/component pair through a \`TemplateModule\`.

Use AssetBundles for:

- UXML and USS UI assets.
- Materials and sounds.
- Unity assets that cannot be loaded directly from disk.
- Cross-platform bundles, with \`_win\` and \`_mac\` suffixes when needed.

Use direct files for:

- \`.png\` and \`.jpg\` images.
- \`.timbermesh\` models.
- \`.blueprint.json\` data.
- Localization CSVs.

## Common 1.0 Patterns

### Add A Good

Create \`Goods/Good.MyGood.blueprint.json\` with \`GoodSpec\`. Add the good to one or more \`GoodCollections\` with \`GoodCollectionSpec.Blueprints#append\`. Add localization keys for display and plural display names. Add an icon under \`Sprites/\` or reuse an existing icon path.

### Add A Recipe

Create \`Recipes/Recipe.MyRecipe.blueprint.json\` with \`RecipeSpec\`. Add the recipe id to the consuming building's \`ManufactorySpec.ProductionRecipeIds\` with \`#append\`, or add a new building that owns the recipe list.

### Add A Need

Create \`Needs/Need.Beaver.MyNeed.blueprint.json\` with \`NeedSpec\`. Add it to \`NeedCollections/NeedCollection.<Faction>.blueprint.json\` with \`NeedCollectionSpec.Needs#append\`. Add a producer building, attraction, or consumption effect so the need is satisfiable.

Prometheus lesson: adding an orphan need is easy; making it explainable and testable is the actual work. Always validate collection membership, localization, UI ordering, and gameplay path.

### Add A Building

Start from the closest existing building Blueprint and reduce it to intentional changes. A typical placeable building combines:

- \`TemplateSpec\`
- \`BlockObjectSpec\`
- \`PlaceableBlockObjectSpec\`
- \`BuildingSpec\`
- \`BuildingModelSpec\`
- \`LabeledEntitySpec\`
- Access, navmesh, transput, workplace, power, storage, ranged-effect, or manufactory Specs as needed.

Then add the building Blueprint path to the correct \`TemplateCollectionSpec.Blueprints#append\` so the toolbar can discover it. Keep faction-specific variants explicit.

Prometheus lesson: do not infer runtime category from a display name. Bind to the actual Timberborn components and Blueprint Specs that own behavior.

### Add A Toolbar Group Or Custom Tool

For data-defined placement, use \`ToolGroupSpec\`, \`CustomBottomBarElementSpec\`, \`CustomRootElementSpec\`, and \`PlaceableBlockObjectSpec.ToolGroupId\`.

For runtime tool behavior, bind an \`ITool\`, create a button/element provider, and multi-bind the bottom-bar module. The official \`HelloWorld\` example shows \`IBottomBarElementsProvider\` plus a \`BottomBarModule\` provider.

### Add Entity Panel UI

Implement an \`IEntityPanelFragment\` and multi-bind an \`EntityPanelModule\` provider. Use localization keys for visible text. Only show the fragment for entities that actually have the components it controls.

Prometheus lesson: make debug/admin UI prove the clicked target and action result in logs. A button that reports success before the game state changed is worse than no button.

### Add Free-Standing UI

Put UXML/USS in an AssetBundle. Load the visual tree with \`VisualElementLoader\` and attach it to \`UILayout\` or a panel service. The official docs call out that UXML/USS cannot be loaded directly from disk.

Luke's TimberUi library shows a useful community pattern: wrap common Timberborn UI controls in small helpers, but prefer existing game UI elements over inventing a parallel design system. When a UI library is only a developer dependency, keep the end-user behavior in the consuming mod and list the library as a dependency.

### Add Top-Bar Or Panel Extensions

Top-bar and panel extensions are runtime work, not just Blueprint work. The common pattern is:

- Bind a provider or updater in the \`Game\` context.
- Read game state through Timberborn services rather than searching the scene every frame.
- Keep formatting and tooltips in small describer/factory classes.
- Let settings decide visibility, ordering, grouping, and thresholds.
- Rebuild UI on relevant state/settings changes instead of doing expensive polling.

Luke's Configurable Top Bar and eMka's Good Statistics are useful 1.0+ examples of this shape: the feature is user-facing, but the code is split into settings, sampling/registry, display providers, and UI descriptions.

### Add A Custom Runtime Component

Define a Spec:

\`\`\`csharp
using Timberborn.BlueprintSystem;

namespace Mods.MyMod.Scripts {
  internal record MyRuntimeSpec : ComponentSpec {
    [Serialize]
    public string SomeValue { get; init; }
  }
}
\`\`\`

Define a component and register the decorator:

\`\`\`csharp
using Bindito.Core;
using Timberborn.TemplateInstantiation;

namespace Mods.MyMod.Scripts {
  [Context("Game")]
  public class MyConfigurator : Configurator {
    protected override void Configure() {
      Bind<MyRuntimeComponent>().AsTransient();
      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    }

    private static TemplateModule ProvideTemplateModule() {
      var builder = new TemplateModule.Builder();
      builder.AddDecorator<MyRuntimeSpec, MyRuntimeComponent>();
      return builder.Build();
    }
  }
}
\`\`\`

Then add \`MyRuntimeSpec\` to the target Blueprint.

Prometheus lesson: keep custom Spec names descriptive and searchable. Timberborn-facing type names are documentation.

### Add Content Extension Specs

For new systems, define small custom \`ComponentSpec\` records and decorate only the templates that need them. Good examples in current community sources include raft/dock specs, statistics specs, recipe/unlock helper specs, tool specs, and compatibility marker specs.

The pattern is:

- Put static configuration in the Blueprint Spec.
- Put runtime state in a component or saveable singleton.
- Register the Spec-to-component decorator through \`TemplateModule\`.
- Add the custom Spec to collections or templates through normal Blueprint merge rules.
- Keep the Spec schema stable once saves or third-party mods might reference it.

### Add Images

Place \`.png\` or \`.jpg\` files in the mod directory. Use a sibling \`.meta.json\` file to control import options such as sprite mode, mipmaps, filter mode, wrap mode, texture format, and dimensions. Same-path images override built-in images.

### Add Timbermesh Models

Use Timbermesh for models. Reference them through \`TimbermeshSpec\`, \`BuildingModelSpec\`, \`BlockObjectModelSpec\`, or child model Specs depending on the Blueprint.

Model naming and structure matter. Use existing in-game models as references. For buildings, expect slots, colliders, block objects, construction bases, and material names to be just as important as the visible mesh.

### Add Materials Or Sounds

Use AssetBundles. The official asset docs call out materials and sounds as notable asset types that require AssetBundles.

## Runtime Coding Patterns

### Entry Points

Use \`IModStarter\` for mod startup in the mod manager environment. Use the current interface shape from the official examples.

Use Bindito \`Configurator\` classes with \`[Context("Game")]\` for game services, components, UI modules, and template decorators.

### Notable C# Interfaces And Classes

These are the first 1.0+ code surfaces to know. Namespaces can move between game builds, so confirm imports in the current official modding project or IDE before copying code.

Entry and dependency injection:

- \`IModStarter\`: earliest mod startup hook used by the official \`HelloWorld\` logger example.
- \`Configurator\`: Bindito module class for registering services, components, providers, and decorators.
- \`[Context("Game")]\`, \`[Context("MainMenu")]\`, \`[Context("MapEditor")]\`: context gates for when a configurator is active.
- \`Bind<T>()\` and \`MultiBind<T>()\`: register one service or contribute to a multi-provider extension point.
- \`IProvider<T>\`: build objects such as \`BottomBarModule\`, \`EntityPanelModule\`, or \`TemplateModule\` only after dependencies are injected.

Lifecycle and components:

- \`ILoadableSingleton\`: singleton with a \`Load()\` method; useful for startup wiring after dependencies exist.
- \`IUnloadableSingleton\`: singleton cleanup hook; use for unregistering events, UI, caches, and external state.
- \`IUpdatableSingleton\`: per-tick singleton work; keep it thin and settings-gated.
- \`BaseComponent\`: base type for entity-attached runtime behavior.
- \`IAwakableComponent\`: component initialization hook used after entity dependencies are available.
- \`IFinishedStateListener\`: building lifecycle listener used by the official \`ShantySpeaker\` example to start and stop sounds when a building becomes finished.

Blueprint-backed runtime behavior:

- \`ComponentSpec\`: base record for custom Blueprint Specs.
- \`[Serialize]\`: marks Spec properties read from Blueprint JSON.
- \`TemplateModule\` and \`TemplateModule.Builder\`: register Blueprint Spec to component decorators with \`AddDecorator<TSpec, TComponent>()\`.
- \`GetComponent<T>()\`: read sibling Specs/components from a \`BaseComponent\`.
- \`[Inject]\`: method/property dependency injection for entity components that Bindito creates.

Tools and bottom bar UI:

- \`ITool\`: runtime tool with \`Enter()\` and \`Exit()\`.
- \`IBottomBarElementsProvider\`: provides bottom-bar elements.
- \`BottomBarElement\`: wrapper for a bottom-bar UI element.
- \`BottomBarModule\` and \`BottomBarModule.Builder\`: multi-bound module used to place elements in bottom-bar sections.
- \`ToolButtonFactory\`: creates game-styled tool buttons.
- \`QuickNotificationService\`: sends quick user-facing notifications.

Entity panels and general UI:

- \`IEntityPanelFragment\`: entity selection panel extension with \`InitializeFragment()\`, \`ShowFragment()\`, \`ClearFragment()\`, and \`UpdateFragment()\`.
- \`EntityPanelModule\` and \`EntityPanelModule.Builder\`: multi-bound module used to add fragments to entity panels.
- \`VisualElementLoader\`: loads UXML from AssetBundles.
- \`UILayout\`: root UI layout target used by the official \`HelloWorld\` initializer.
- \`DialogBoxShower\`: creates Timberborn dialog boxes.
- \`NineSliceVisualElement\` and \`NineSliceButton\`: common Timberborn-styled UI Toolkit controls.
- \`ToggleDisplayStyle()\`: helper used by examples to show or hide UI elements.

Save, load, and settings:

- \`ISaveableSingleton\`: save/load participant for singleton state.
- \`ISingletonSaver\` and \`ISingletonLoader\`: write and read singleton state.
- \`IValueSerializer<T>\`: serializer for custom structured values in save data.
- \`ISettings\`: key/value settings backend used by current Mod Settings implementations.
- \`ModSettingsOwner\`: eMka Mod Settings base class that registers public \`ModSetting<T>\` properties.
- \`ModSetting<T>\`: typed user setting with default value, current value, reset, and change events.
- \`NonPersistentSetting\`: settings UI element that should not be stored as a persistent preference.
- \`IModSettingElementFactory\`: extension point for custom Mod Settings UI controls.

Game services and helpers worth searching for before writing your own:

- \`ISoundSystem\` and \`MixerNames\`: play/stop sounds and route them through game mixer groups.
- \`NaturalResource\`: entity component used by the official panel example to decide whether a selected entity is relevant.
- \`DialogBoxShower\`, \`QuickNotificationService\`, and other game-owned UI services should be preferred over custom popups when possible.

### Lifecycles

Base components no longer inherit \`MonoBehaviour\`. If your component needs Unity-like lifecycle behavior, implement the appropriate Timberborn lifecycle interface, such as an awakable/loadable/updatable pattern used by the game API.

Prometheus lesson: lifecycle timing is the fastest way to crash a mod. Delay world/entity scans until the game has loaded far enough, centralize readiness checks, and make every runtime singleton no-op until its required world state exists.

### Save And Load State

Use Timberborn save/load services for world or profile state that must survive reloads. A durable pattern from current 1.0 mods is:

- Use \`ISaveableSingleton\` for global mod state.
- Use typed value serializers for structured data.
- Keep persisted data versioned and resilient to missing fields.
- Keep player preferences in settings, not in save data.
- Keep computed caches rebuildable after load.

For user-editable exported data, prefer plain JSON with explicit version fields and import/export tools. Building Blueprints-style mods show why: users want files they can share, copy, inspect, and recover.

### Public API First, Reflection Last

Prefer game-owned services and public components. Use reflection only when there is no stable public surface, and isolate reflection behind a compatibility/probe layer with explicit logs.

Prometheus lesson: a compatibility probe should say which type/member was found, which fallback was used, and whether the feature is degraded. Do not scatter reflection lookups across gameplay code.

### Adapter Boundary

Keep domain logic host-agnostic. Timberborn adapters should translate coordinates, component state, and events into your core model; they should not own the core rules.

Prometheus lesson for Wildfire: the simulation core should stay testable through deterministic CLI scenarios before any live Timberborn validation. Timberborn is an adapter, not the source of fire truth.

### Logs And QA

Use \`Player.log\` for game/mod load errors and general Unity exceptions. Use a dedicated mod log for high-volume domain telemetry. Prometheus used \`Fire.log\` to keep fire-system evidence separate from general game noise.

Useful macOS paths:

\`\`\`text
~/Library/Logs/Mechanistry/Timberborn/Player.log
~/Library/Logs/Mechanistry/Timberborn/Player-prev.log
~/Library/Logs/Mechanistry/Timberborn/Fire.log
~/Documents/Timberborn/Mods/
\`\`\`

Prometheus lesson: live QA is not "the build passed". A mod is not validated until the game loads, the target save opens, the UI or command path is exercised, and logs/screenshots prove the expected behavior.

## Build And Pipeline Patterns

### Official Unity Pipeline

1. Open \`~/repos/timberborn-modding\` in the Unity version expected by the project.

2. Use the tooling to import Timberborn DLLs and game assets from the installed game.

3. Put source mod files under \`Assets/Mods/<ModName>\`.

4. Put shippable data under \`Data/\`.

5. Put workshop/root metadata such as thumbnails under \`Root/\`.

6. Put C# under \`Scripts/\` and use asmdef files.

7. Open \`Timberborn -> Show Mod Builder\`.

8. For dev builds, choose whether to build code and Windows/macOS AssetBundles.

9. For release builds, use clean build and optional ZIP archive.

10. Test the built mod from \`~/Documents/Timberborn/Mods/<ModName>\`.

### Direct Deploy Pipeline

Prometheus showed that a script-owned direct deploy can be faster for agent workflows:

1. Compile C# against the official modding project or imported Timberborn assemblies.

2. Link or copy shippable non-code content into \`~/Documents/Timberborn/Mods/<ModName>\`.

3. Place the built DLL/PDB under \`Scripts/\`.

4. Clear logs before launch.

5. Launch Timberborn.

6. Wait for the game process and mod startup log evidence.

7. Run focused live QA.

8. Preserve logs and screenshots as evidence.

Prometheus-specific hardening worth copying:

- One command for test/build/deploy/launch.
- A shared build/QA lock across worktrees so agents do not clear logs or redeploy during another live QA run.
- A \`--qa\` mode that leaves the lock held while live evidence is collected.
- A command bridge for repeatable in-game actions.
- A handoff doc that records exact save names, commands, logs, screenshots, and blockers.

## What We Learned From Prometheus

- Data-first works, but only when collection membership is explicit. Goods, needs, recipes, and buildings often need companion collection edits before they are visible or usable.
- Keep internal docs out of deployed mod content. Anything inside the shippable mod tree can end up in \`~/Documents/Timberborn/Mods/<ModName>\`.
- Prefer Timberborn-native assets and visual patterns before inventing approximations.
- Use official component/services where possible; when a public API is missing, create a narrow adapter and log compatibility probes.
- Treat loaded-scene object scanning as dangerous until the world is ready.
- Query terrain and soil services only for cells that are valid for that service. Prometheus hit soil-moisture warnings by asking Timberborn for invalid/non-terrain cells.
- Debug/admin tools must be as safe as production code. Prometheus replaced unsafe destructive shortcuts with game-owned actions or guarded state transitions.
- A result named \`success\` must mean the requested game-state change actually happened. Otherwise return \`rejected\`, \`no_target\`, or a specific reason.
- Deterministic tests catch rule regressions cheaply. Live Timberborn QA catches lifecycle, UI, asset, save/load, and component integration failures that tests cannot.
- Sibling worktrees are useful for risky cleanup and multi-agent work, but build/deploy/QA must be serialized.
- Names matter. Descriptive Timberborn-facing type names make search, debugging, and review much easier.
- Do not keep superseded systems in parallel unless there is a deliberate migration period. Replacement work should remove retired authority paths once the new path is proven.

## Community Signals From 1.0+

- Recent Reddit questions show that blueprint structure, block ordering, and Timbermesh load failures are common pain points. The guide should therefore include actual Blueprint inventory and point people to existing files as examples.
- Building Blueprints-style mods show demand for shareable JSON user data, settings copy, flipping, Steam Workshop support, and unlock handling. For mod authors, that suggests designing data formats to be inspectable and portable.
- Luke Vo's current source is \`datvm/TimberbornMods\`; his 1.0 Workshop collection links there directly. It is useful for no-Unity workflows, UI helpers, reusable APIs, configurable QoL mods, top-bar work, tools, recipes, weather, and save/export style patterns.
- eMkaQQ's current source is useful for Mod Settings, minimap/UI work, statistics sampling, custom setting elements, dependency manifests, and small reusable framework mods.
- BobingAbout's public GitHub was checked. I did not find a public Timberborn source repository there, so use public Workshop behavior only as inspiration unless source appears later. The current Script Pack Workshop page is still useful for its documented custom Specification-file pattern around character textures, avatars, bot textures, grow-up texture maps, and name-triggered customization.
- Current reusable-system mods show a durable product pattern: expose high-level templates for normal users, keep advanced formats editable for power users, and make extension points explicit.

## 1.0 Compatibility Standards

- Use \`.blueprint.json\` files and the current Blueprint merge system.
- Use the official modding tools repository for Unity-based builds and asset import.
- Use current \`BaseComponent\` lifecycle interfaces for runtime behavior.
- Verify helper-library dependencies against Timberborn 1.0 before documenting them as requirements.
- Treat compile-only checks as insufficient for live compatibility.

## Local Asset Map

Installed 1.0+ modding assets available on this machine:

\`\`\`text
~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/Blueprints
~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/Blueprints.zip
~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/EditorDll
~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/EditorUI.zip
~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/Localizations.zip
~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/Shaders
~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/UI.zip
\`\`\`

## Reference Sources

- Official modding tools repository: https://github.com/mechanistry/timberborn-modding
- Official modding wiki entry point: https://github.com/mechanistry/timberborn-modding/wiki
- Official wiki "Creating Mods": https://timberborn.wiki.gg/wiki/Creating_Mods
- Official asset documentation mirror: https://github-wiki-see.page/m/mechanistry/timberborn-modding/wiki/Assets
- Official UI documentation mirror: https://github-wiki-see.page/m/mechanistry/timberborn-modding/wiki/User-interface
- SteamDB mirror of 1.0 modding pipeline notes: https://steamdb.info/patchnotes/20576139/
- Luke Vo GitHub profile: https://github.com/datvm
- DatVM/Luke TimberbornMods source: https://github.com/datvm/TimberbornMods
- DatVM/Luke TimberbornMods guide: https://datvm.github.io/TimberbornMods/
- eMkaQQ Timberborn modding source: https://github.com/eMkaQQ/timberborn-modding
- BobingAbout GitHub profile checked: https://github.com/BobingAbout
- BobingAbout Script Pack public Workshop listing: https://steamcommunity.com/workshop/filedetails/?id=3416879061
- Reddit blueprint documentation question: https://www.reddit.com/r/Timberborn/comments/1s9r6qg/building_blueprint_documentationtimbermesh_error/
- Reddit Building Blueprints launch/update threads: https://www.reddit.com/r/Timberborn/comments/1r6dwl9/new_mod_building_blueprints/ and https://www.reddit.com/r/Timberborn/comments/1ra6o0e/building_blueprints_v1020_blueprint_flipping/

## Open Research: Discord

Discord research is approved, but not complete. To incorporate it without guessing, collect one of these:

- Exported threads from the official Timberborn modding channels.
- Screenshots or copied messages with dates, author context, and channel names.
- A local text dump of relevant discussions about Blueprint merge rules, Timbermesh failures, official 1.0 API changes, or recommended mod packaging.

When Discord material is added, keep it in a separate sourced section and mark whether it is official Mechanistry guidance, community convention, or one author's implementation advice.
`;

mkdirSync(guideDir, { recursive: true });
writeFileSync(mainGuidePath, guide);
writeFileSync(blueprintReferencePath, blueprintReference);
