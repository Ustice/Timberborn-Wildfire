import { spawnSync } from "bun";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";

type IconSlot = {
  readonly spriteName: string;
  readonly note?: string;
};

type Palette = {
  readonly outline: string;
  readonly outerRing: string;
  readonly innerBg: string;
  readonly foreground: string;
};

type Section = {
  readonly title: string;
  readonly meaning: string;
  readonly use: string;
  readonly palette: Palette;
  readonly groups: readonly {
    readonly title: string;
    readonly description: string;
    readonly icons: readonly IconSlot[];
  }[];
};

const repoRoot = path.resolve(import.meta.dir, "..");
const assetDir = path.join(repoRoot, "docs/reference/assets/status-icons");
const outputPath = path.join(assetDir, "contact-sheet-design-language-all-icons.png");
const svgPath = path.join(assetDir, "contact-sheet-design-language-all-icons.svg");

const colors = {
  page: "#182124",
  panel: "#202b2d",
  panelStroke: "#67552d",
  text: "#e6dec6",
  muted: "#b9ad91",
  gold: "#d2a42a",
  red: "#a01812",
  tan: "#bf8946",
  brown: "#8a4f2e",
  blackBrown: "#1d120d",
  green: "#78b642",
  yellowGreen: "#b6bd43",
  teal: "#669e97",
  placeholderStroke: "#7a704f",
  placeholderFill: "#263235",
};

const sections: readonly Section[] = [
  {
    title: "Red ring / tan inner background / dark foreground",
    meaning:
      "Urgent active problem. Serious condition that needs attention while the affected subject remains visually readable.",
    use: "Active hazards, shortages, conflicts, countdown hazards, and serious warnings.",
    palette: {
      outline: colors.red,
      outerRing: colors.gold,
      innerBg: colors.tan,
      foreground: colors.blackBrown,
    },
    groups: [
      {
        title: "Contamination sub-icon",
        description: "Badwater or contamination marker on top of the base problem.",
        icons: [{ spriteName: "BuildingBlockedByContamination" }],
      },
      {
        title: "Exclamation sub-icon",
        description: "Urgent attention marker for active problems.",
        icons: [
          { spriteName: "AutomationLoop" },
          { spriteName: "BuildingNeedsWater" },
          { spriteName: "ContaminatedNaturalResource" },
          { spriteName: "CultivationHalted" },
          { spriteName: "DemolitionBlocked" },
          { spriteName: "DirectionalBlocking" },
          { spriteName: "DryingNaturalResource" },
          { spriteName: "FloodedBuilding" },
          { spriteName: "GateConflict" },
          { spriteName: "LackOfResources" },
          { spriteName: "NoStartingLocation" },
          { spriteName: "NoStorage" },
          { spriteName: "NoUnemployed" },
          { spriteName: "NotEnoughScience" },
          { spriteName: "NotEnoughWater" },
          { spriteName: "OutOfHaulersRange" },
          { spriteName: "TooMuchWater" },
          { spriteName: "UnreachableObject" },
        ],
      },
      {
        title: "Countdown clock sub-icon",
        description: "The next hazard is scheduled to happen.",
        icons: [{ spriteName: "UnstableCoreCountdown" }, { spriteName: "WaterSourceCountdown" }],
      },
      {
        title: "No corner sub-icon / central warning mark",
        description: "The warning symbol itself is the message.",
        icons: [{ spriteName: "GenericError" }],
      },
    ],
  },
  {
    title: "Red ring / dark inner background / tan foreground",
    meaning:
      "Cannot function. The subject is blocked, disconnected, unpowered, incompatible, or otherwise unable to operate.",
    use: "Incapacitated beavers, inoperable buildings, or systems where the entity cannot perform its normal role.",
    palette: {
      outline: colors.red,
      outerRing: colors.red,
      innerBg: colors.blackBrown,
      foreground: colors.tan,
    },
    groups: [
      {
        title: "Exclamation sub-icon",
        description: "Hard blocker that prevents function.",
        icons: [
          { spriteName: "EntranceBlocked" },
          { spriteName: "NoPower" },
          { spriteName: "NonCompatibleVersion" },
          { spriteName: "UnconnectedBuilding" },
        ],
      },
      {
        title: "Pause sub-icon",
        description: "A failed recipe/process rather than a missing resource.",
        icons: [{ spriteName: "LackOfNutrients" }],
      },
    ],
  },
  {
    title: "Gold ring / tan inner background / dark foreground",
    meaning: "Warning or impairment. Concerning but less severe than red; the subject can usually still function.",
    use: "Beaver or system states that are impaired but not fully disabled.",
    palette: {
      outline: colors.gold,
      outerRing: colors.gold,
      innerBg: colors.tan,
      foreground: colors.blackBrown,
    },
    groups: [
      {
        title: "Exclamation sub-icon",
        description: "Warning condition that needs attention.",
        icons: [
          { spriteName: "BadwaterContamination" },
          { spriteName: "ChippedTeeth" },
          { spriteName: "Incubation" },
          { spriteName: "Injury" },
          { spriteName: "NoControlSignal" },
          { spriteName: "OutOfEnergy" },
          { spriteName: "OutOfFuel" },
        ],
      },
      {
        title: "Question sub-icon",
        description: "Not lost, confused, stranded, or unresolved assignment.",
        icons: [{ spriteName: "NothingToDo" }, { spriteName: "Stranded" }],
      },
      {
        title: "Bottom-right missing-item sub-icon",
        description: "Missing configured good/item.",
        icons: [{ spriteName: "UnspecifiedGood" }],
      },
      {
        title: "Bottom-right gear sub-icon",
        description: "Missing configured recipe/production setting.",
        icons: [{ spriteName: "UnspecifiedRecipe" }],
      },
    ],
  },
  {
    title: "Gold ring / dark inner background / tan foreground",
    meaning: "Basic creature need. Direct biological or need-meter status.",
    use: "Reserved for fundamental beaver needs such as hunger, thirst, or exhaustion.",
    palette: {
      outline: colors.gold,
      outerRing: colors.gold,
      innerBg: colors.blackBrown,
      foreground: colors.tan,
    },
    groups: [
      {
        title: "Question sub-icon",
        description: "Nonvolition with uncertain or unresolved action.",
        icons: [{ spriteName: "Exhaustion" }],
      },
      {
        title: "No sub-icon",
        description: "Direct need icon that the main symbol alone communicates.",
        icons: [{ spriteName: "Hunger" }, { spriteName: "Thirst" }],
      },
    ],
  },
  {
    title: "Brown ring / dark inner background / tan foreground",
    meaning: "Neutral mode or system status. Usually descriptive or player-driven, not an emergency.",
    use: "Pause, emptying, demolish, stopped, or mode-like behavior.",
    palette: {
      outline: colors.brown,
      outerRing: colors.brown,
      innerBg: colors.blackBrown,
      foreground: colors.tan,
    },
    groups: [
      {
        title: "Exclamation sub-icon",
        description: "Stopped state that still asks for attention.",
        icons: [{ spriteName: "ApiStopped" }],
      },
      {
        title: "No sub-icon",
        description: "Neutral command or state.",
        icons: [{ spriteName: "Demolish" }, { spriteName: "Empty" }, { spriteName: "Pause" }],
      },
    ],
  },
  {
    title: "Black-brown ring / tan inner background / black foreground",
    meaning: "Terminal creature state. Death-like final outcome.",
    use: "Reserved for death; avoid for temporary incapacitation.",
    palette: {
      outline: colors.blackBrown,
      outerRing: colors.blackBrown,
      innerBg: colors.tan,
      foreground: "#070707",
    },
    groups: [
      {
        title: "No sub-icon",
        description: "Final state.",
        icons: [{ spriteName: "Death" }],
      },
    ],
  },
  {
    title: "Green ring / yellow-green inner background / black foreground",
    meaning: "Positive result. Unlock, high score, success, or beneficial status.",
    use: "Achievement and positive outcomes, not hazards.",
    palette: {
      outline: colors.green,
      outerRing: colors.green,
      innerBg: colors.yellowGreen,
      foreground: "#070707",
    },
    groups: [
      {
        title: "No sub-icon",
        description: "Positive state/result.",
        icons: [{ spriteName: "NewFactionUnlocked" }, { spriteName: "WellbeingHighscore" }],
      },
    ],
  },
  {
    title: "Teal ring / tan inner background / dark foreground",
    meaning: "Automation or special operational mode.",
    use: "Automation states or unusual operating modes.",
    palette: {
      outline: colors.teal,
      outerRing: colors.teal,
      innerBg: colors.tan,
      foreground: colors.blackBrown,
    },
    groups: [
      {
        title: "No sub-icon",
        description: "Special mode.",
        icons: [{ spriteName: "PausedByAutomation" }],
      },
    ],
  },
];

const missingIcons = sections
  .flatMap((section) => section.groups)
  .flatMap((group) => group.icons)
  .map((icon) => path.join(assetDir, `${icon.spriteName}.png`))
  .filter((iconPath) => !existsSync(iconPath));

if (missingIcons.length > 0) {
  throw new Error(`Missing status icon PNGs:\n${missingIcons.join("\n")}`);
}

const escapeXml = (value: string): string =>
  value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");

const splitLabel = (value: string): string =>
  value
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");

const wrapText = (value: string, maxChars: number): readonly string[] =>
  value.split(" ").reduce<string[]>((lines, word) => {
    const previous = lines.at(-1);
    const next = previous ? `${previous} ${word}` : word;
    return previous && next.length > maxChars
      ? [...lines.slice(0, -1), previous, word]
      : [...lines.slice(0, -1), next];
  }, []);

const imageHref = (spriteName: string): string => {
  const data = readFileSync(path.join(assetDir, `${spriteName}.png`)).toString("base64");
  return `data:image/png;base64,${data}`;
};

const canvas = {
  width: 2160,
  height: 3840,
  margin: 42,
  panelPadding: 20,
  gap: 24,
  sectionColumns: 3,
  iconColumns: 2,
  cardWidth: 680,
  slotHeight: 122,
  iconSize: 76,
};

const panelInnerWidth = canvas.cardWidth - canvas.panelPadding * 2;
const slotGap = 12;
const slotWidth = (panelInnerWidth - slotGap * (canvas.iconColumns - 1)) / canvas.iconColumns;
const groupHeaderBaseHeight = 38;
const futureSlotCount = canvas.iconColumns * 2;

type RenderedSection = {
  readonly section: Section;
  readonly height: number;
};

type PlacedSection = RenderedSection & {
  readonly x: number;
  readonly y: number;
};

const sectionHeaderHeight = (section: Section): number => {
  const titleLines = wrapText(section.title, 38);
  const meaningLines = wrapText(section.meaning, 82);
  const useLines = wrapText(section.use, 82);
  return (
    canvas.panelPadding +
    titleLines.length * 24 +
    52 +
    meaningLines.length * 17 +
    useLines.length * 17 +
    18
  );
};

const groupHeaderHeight = (description: string): number =>
  groupHeaderBaseHeight + wrapText(description, 74).length * 17;

const candidatePanelHeaderHeight = 92;

const sectionRows = (section: Section): number =>
  section.groups
    .map((group) => groupHeaderHeight(group.description) + Math.ceil(group.icons.length / canvas.iconColumns) * canvas.slotHeight)
    .reduce((height, groupHeight) => height + groupHeight, sectionHeaderHeight(section)) +
  groupHeaderHeight("Two reserved rows for extending this grammar section.") +
  Math.ceil(futureSlotCount / canvas.iconColumns) * canvas.slotHeight +
  canvas.panelPadding +
  28;

const renderedSections: readonly RenderedSection[] = sections.map((section) => ({
  section,
  height: sectionRows(section),
}));

const initialColumnHeights = Array.from({ length: canvas.sectionColumns }, () => 0);

const placedSections = renderedSections.reduce<{
  readonly columnHeights: readonly number[];
  readonly sections: readonly PlacedSection[];
}>(
  (state, section) => {
    const minHeight = Math.min(...state.columnHeights);
    const columnIndex = state.columnHeights.indexOf(minHeight);
    const columnHeight = state.columnHeights[columnIndex] ?? 0;
    const yOffset = columnHeight === 0 ? 0 : columnHeight + canvas.gap;
    const x = canvas.margin + columnIndex * (canvas.cardWidth + canvas.gap);
    const y = canvas.margin + 72 + yOffset;
    const nextColumnHeight = yOffset + section.height;
    return {
      columnHeights: state.columnHeights.toSpliced(columnIndex, 1, nextColumnHeight),
      sections: [...state.sections, { ...section, x, y }],
    };
  },
  { columnHeights: initialColumnHeights, sections: [] },
);

const rightColumnHeight = placedSections.columnHeights.at(-1) ?? 0;
const candidatePanelY = canvas.margin + 72 + rightColumnHeight + canvas.gap;
const candidatePanelHeight = canvas.height - canvas.margin - candidatePanelY;
const candidateSlotRows = Math.max(
  0,
  Math.floor((candidatePanelHeight - canvas.panelPadding * 2 - candidatePanelHeaderHeight) / canvas.slotHeight),
);
const candidateSlotCount = candidateSlotRows * canvas.iconColumns;
const contentHeight = Math.max(
  canvas.margin * 2 + 72 + Math.max(...placedSections.columnHeights),
  candidatePanelY + candidatePanelHeight + canvas.margin,
);

if (contentHeight > canvas.height) {
  throw new Error(`Contact sheet content height ${contentHeight}px exceeds target canvas height ${canvas.height}px`);
}

const textLines = (
  lines: readonly string[],
  x: number,
  y: number,
  className: string,
  lineHeight: number,
  prefix = "",
): string =>
  lines
    .map(
      (line, index) =>
        `<text x="${x}" y="${y + index * lineHeight}" class="${className}">${index === 0 ? prefix : ""}${escapeXml(line)}</text>`,
    )
    .join("");

const swatch = (x: number, y: number, label: string, fill: string): string => `
  <rect x="${x}" y="${y}" width="24" height="16" rx="3" fill="${fill}" />
  <text x="${x + 34}" y="${y + 13}" class="tiny">${escapeXml(label)}</text>`;

const paletteLegend = (palette: Palette, x: number, y: number): string =>
  [
    swatch(x, y, "outline", palette.outline),
    swatch(x + 152, y, "outer ring", palette.outerRing),
    swatch(x, y + 24, "inner bg", palette.innerBg),
    swatch(x + 152, y + 24, "foreground", palette.foreground),
  ].join("");

const iconSlot = (icon: IconSlot, index: number, x: number, y: number): string => {
  const column = index % canvas.iconColumns;
  const row = Math.floor(index / canvas.iconColumns);
  const slotX = x + column * (slotWidth + slotGap);
  const slotY = y + row * canvas.slotHeight;
  const imageX = slotX;
  const imageY = slotY + 16;
  const labelLines = wrapText(splitLabel(icon.spriteName), 15).slice(0, 3);
  const textSvg = labelLines
    .map((line, lineIndex) => `<tspan x="${imageX + canvas.iconSize + 14}" dy="${lineIndex === 0 ? 0 : 18}">${escapeXml(line)}</tspan>`)
    .join("");

  return `
    <g>
      <image href="${imageHref(icon.spriteName)}" x="${imageX}" y="${imageY}" width="${canvas.iconSize}" height="${canvas.iconSize}" />
      <text class="icon-label" y="${slotY + 43}">${textSvg}</text>
    </g>`;
};

const placeholderSlot = (index: number, x: number, y: number, label: string, note: string): string => {
  const column = index % canvas.iconColumns;
  const row = Math.floor(index / canvas.iconColumns);
  const slotX = x + column * (slotWidth + slotGap);
  const slotY = y + row * canvas.slotHeight;
  const radius = canvas.iconSize / 2;
  return `
    <g>
      <rect x="${slotX}" y="${slotY + 16}" width="${canvas.iconSize}" height="${canvas.iconSize}" rx="${radius}" class="future-circle" />
      <line x1="${slotX + 24}" y1="${slotY + 48}" x2="${slotX + 40}" y2="${slotY + 48}" class="future-mark" />
      <line x1="${slotX + 32}" y1="${slotY + 40}" x2="${slotX + 32}" y2="${slotY + 56}" class="future-mark" />
      <text x="${slotX + canvas.iconSize + 14}" y="${slotY + 43}" class="future-label">${escapeXml(label)}</text>
      <text x="${slotX + canvas.iconSize + 14}" y="${slotY + 64}" class="future-note">${escapeXml(note)}</text>
    </g>`;
};

const futureSlot = (index: number, x: number, y: number): string =>
  placeholderSlot(index, x, y, "Future icon", "Reserved slot");

const candidateSlot = (index: number, x: number, y: number): string =>
  placeholderSlot(index, x, y, "Candidate icon", "Imagegen slot");

const groupSvg = (
  group: Section["groups"][number],
  x: number,
  y: number,
): { readonly svg: string; readonly nextY: number } => {
  const noteLines = wrapText(group.description, 74);
  const headerHeight = groupHeaderHeight(group.description);
  const iconsY = y + headerHeight;
  const iconRows = Math.ceil(group.icons.length / canvas.iconColumns);
  const groupTitle = `
    <text x="${x}" y="${y + 20}" class="group-title">${escapeXml(group.title)}</text>
    ${textLines(noteLines, x, y + 44, "group-note", 17)}`;
  const icons = group.icons.map((icon, index) => iconSlot(icon, index, x, iconsY)).join("");
  return {
    svg: `${groupTitle}${icons}`,
    nextY: iconsY + iconRows * canvas.slotHeight,
  };
};

const sectionSvg = (
  renderedSection: RenderedSection,
  x: number,
  y: number,
): { readonly svg: string; readonly nextY: number } => {
  const section = renderedSection.section;
  const panelHeight = renderedSection.height;
  const textX = x + canvas.panelPadding;
  const titleLines = wrapText(section.title, 38);
  const meaningLines = wrapText(section.meaning, 82);
  const useLines = wrapText(section.use, 82);
  const paletteY = y + canvas.panelPadding + titleLines.length * 24 + 12;
  const meaningY = paletteY + 68;
  const useY = meaningY + meaningLines.length * 17 + 22;
  const groupStartY = useY + useLines.length * 17 + 20;
  const header = `
    <rect x="${x}" y="${y}" width="${canvas.cardWidth}" height="${panelHeight}" rx="8" class="panel" />
    ${textLines(titleLines, textX, y + 34, "section-title", 24)}
    ${paletteLegend(section.palette, textX, paletteY)}
    ${textLines(meaningLines, textX, meaningY, "body", 17, `<tspan class="strong">Meaning:</tspan> `)}
    ${textLines(useLines, textX, useY, "body", 17, `<tspan class="strong">Use:</tspan> `)}`;

  const grouped = section.groups.reduce(
    (state, group) => {
      const rendered = groupSvg(group, textX, state.y);
      return { y: rendered.nextY, svg: `${state.svg}${rendered.svg}` };
    },
    { y: groupStartY, svg: "" },
  );

  const futureY = grouped.y + 4;
  const futureDescription = "Two reserved rows for extending this grammar section.";
  const futureDescriptionLines = wrapText(futureDescription, 74);
  const futureHeader = `
    <text x="${textX}" y="${futureY + 20}" class="group-title">Future icon slots</text>
    ${textLines(futureDescriptionLines, textX, futureY + 44, "group-note", 17)}`;
  const futures = Array.from({ length: futureSlotCount }, (_, index) =>
    futureSlot(index, textX, futureY + groupHeaderHeight(futureDescription)),
  ).join("");

  return {
    svg: `${header}${grouped.svg}${futureHeader}${futures}`,
    nextY: y + panelHeight + canvas.gap,
  };
};

const candidatePanelSvg = (): string => {
  if (candidateSlotCount === 0) {
    return "";
  }

  const x = canvas.margin + (canvas.sectionColumns - 1) * (canvas.cardWidth + canvas.gap);
  const textX = x + canvas.panelPadding;
  const slotsY = candidatePanelY + canvas.panelPadding + candidatePanelHeaderHeight;
  const slots = Array.from({ length: candidateSlotCount }, (_, index) => candidateSlot(index, textX, slotsY)).join("");

  return `
    <rect x="${x}" y="${candidatePanelY}" width="${canvas.cardWidth}" height="${candidatePanelHeight}" rx="8" class="panel" />
    <text x="${textX}" y="${candidatePanelY + 34}" class="section-title">Candidate icons</text>
    <text x="${textX}" y="${candidatePanelY + 64}" class="body">Scratch slots for imagegen concepts before promoting them into a grammar section.</text>
    ${slots}`;
};

const body = `${placedSections.sections.map((section) => sectionSvg(section, section.x, section.y).svg).join("")}${candidatePanelSvg()}`;

const svg = `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="${canvas.width}" height="${canvas.height}" viewBox="0 0 ${canvas.width} ${canvas.height}">
  <style>
    .page { fill: ${colors.page}; }
    .panel { fill: ${colors.panel}; stroke: ${colors.panelStroke}; stroke-width: 1.5; }
    .title { fill: ${colors.text}; font: 700 28px Arial, Helvetica, sans-serif; }
    .subtitle { fill: ${colors.muted}; font: 400 14px Arial, Helvetica, sans-serif; }
    .section-title { fill: ${colors.text}; font: 700 22px Arial, Helvetica, sans-serif; }
    .group-title { fill: ${colors.gold}; font: 700 15px Arial, Helvetica, sans-serif; }
    .group-note, .body { fill: ${colors.muted}; font: 400 13px Arial, Helvetica, sans-serif; }
    .tiny { fill: ${colors.muted}; font: 400 11px Arial, Helvetica, sans-serif; }
    .icon-label { fill: ${colors.text}; font: 700 18px Arial, Helvetica, sans-serif; }
    .future-label { fill: ${colors.text}; font: 700 18px Arial, Helvetica, sans-serif; }
    .future-note { fill: ${colors.muted}; font: 400 13px Arial, Helvetica, sans-serif; }
    .future-circle { fill: ${colors.placeholderFill}; stroke: ${colors.placeholderStroke}; stroke-width: 2; stroke-dasharray: 8 7; }
    .future-mark { stroke: ${colors.placeholderStroke}; stroke-width: 4; stroke-linecap: round; }
    .strong { fill: ${colors.text}; font-weight: 700; }
  </style>
  <rect class="page" x="0" y="0" width="${canvas.width}" height="${canvas.height}" />
  <text x="${canvas.margin}" y="${canvas.margin}" class="title">Timberborn Status Icon Design Language</text>
  <text x="${canvas.margin}" y="${canvas.margin + 28}" class="subtitle">Three section columns using the original Timberborn status icon PNGs. Each section reserves two future rows.</text>
  ${body}
</svg>`;

mkdirSync(assetDir, { recursive: true });
writeFileSync(svgPath, svg);

const result = spawnSync({
  cmd: ["magick", svgPath, outputPath],
  stdout: "pipe",
  stderr: "pipe",
});

if (result.exitCode !== 0) {
  throw new Error(`magick failed:\n${result.stderr.toString()}`);
}

console.log(`Wrote ${outputPath}`);
console.log(`Wrote ${svgPath}`);
