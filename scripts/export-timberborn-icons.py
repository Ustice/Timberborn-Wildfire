#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
import os
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

import UnityPy
from PIL import Image, ImageDraw, ImageFont


GAME_DATA_PATH = Path(
    "~/Library/Application Support/Steam/steamapps/common/Timberborn/"
    "Timberborn.app/Contents/Resources/Data"
).expanduser()
OUTPUT_ROOT = Path("docs/reference/assets")

MENU_BLUEPRINT_FOLDERS = (
    "Buildings",
    "MapEditor",
    "MapEditorCategories",
    "NaturalResources",
    "Terrain",
    "ToolGroups",
)

GOODS_BLUEPRINT_FOLDER = "Goods"

EXPLICIT_MENU_ICON_NAMES = (
    "CancelToolIcon",
    "DeleteGroupIcon",
    "DeleteObjectIcon",
    "DeleteRecoveredGoodStackToolIcon",
    "DemolishResourcesTool",
    "FieldsPlantingToolGroupIcon",
    "ForestryPlantingToolGroupIcon",
    "NaturalResourcesIcon",
    "PriorityToolGroupIcon",
    "RelativeTerrainHeightBrushIcon",
    "SculptingTerrainBrushIcon",
    "TreeToolGroupIcon",
)

STATUS_ICON_NAMES = (
    "ApiStopped",
    "AutomationLoop",
    "BadwaterContamination",
    "BuildingBlockedByContamination",
    "BuildingNeedsWater",
    "ChippedTeeth",
    "ContaminatedNaturalResource",
    "CultivationHalted",
    "Death",
    "Demolish",
    "DemolitionBlocked",
    "DirectionalBlocking",
    "DryingNaturalResource",
    "Empty",
    "EntranceBlocked",
    "Exhaustion",
    "FloodedBuilding",
    "GateConflict",
    "GenericError",
    "Hunger",
    "Incubation",
    "Injury",
    "LackOfNutrients",
    "LackOfResources",
    "NewFactionUnlocked",
    "NoControlSignal",
    "NoPower",
    "NoStartingLocation",
    "NoStorage",
    "NoUnemployed",
    "NonCompatibleVersion",
    "NotEnoughScience",
    "NotEnoughWater",
    "NothingToDo",
    "OutOfEnergy",
    "OutOfFuel",
    "OutOfHaulersRange",
    "Pause",
    "PausedByAutomation",
    "Stranded",
    "Thirst",
    "TooMuchWater",
    "UnconnectedBuilding",
    "UnreachableObject",
    "UnspecifiedGood",
    "UnspecifiedRecipe",
    "UnstableCoreCountdown",
    "WaterSourceCountdown",
    "WellbeingHighscore",
)


@dataclass(frozen=True)
class IconEntry:
    key: str
    sprite_name: str
    source: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Export Timberborn menu, goods, and status sprites plus contact sheets."
    )
    parser.add_argument("--game-data", type=Path, default=GAME_DATA_PATH)
    parser.add_argument("--out-root", type=Path, default=OUTPUT_ROOT)
    return parser.parse_args()


def sanitize_filename(value: str) -> str:
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", value).strip("._") or "icon"


def collect_icon_paths(value: Any) -> list[str]:
    paths: list[str] = []
    if isinstance(value, dict):
        for key, nested in value.items():
            if key == "Icon" and isinstance(nested, str):
                paths.append(nested)
            paths.extend(collect_icon_paths(nested))
    elif isinstance(value, list):
        for item in value:
            paths.extend(collect_icon_paths(item))
    return paths


def sprite_name_from_icon_path(icon_path: str) -> str:
    return icon_path.replace("\\", "/").rstrip("/").split("/")[-1]


def collect_blueprint_entries(blueprints_root: Path, folders: Iterable[str]) -> list[IconEntry]:
    entries: dict[str, IconEntry] = {}
    for folder in folders:
        root = blueprints_root / folder
        if not root.exists():
            continue

        for blueprint in sorted(root.rglob("*.blueprint.json")):
            try:
                data = json.loads(blueprint.read_text(encoding="utf-8-sig"))
            except json.JSONDecodeError as exc:
                print(f"Skipping invalid blueprint JSON {blueprint}: {exc}")
                continue

            for icon_path in collect_icon_paths(data):
                sprite_name = sprite_name_from_icon_path(icon_path)
                source = str(blueprint.relative_to(blueprints_root))
                key = f"{source}:{icon_path}"
                entries[key] = IconEntry(key=key, sprite_name=sprite_name, source=source)

    return sorted(entries.values(), key=lambda entry: (entry.sprite_name.lower(), entry.source))


def collect_sprites(resources_file: Path) -> dict[str, Image.Image]:
    sprites: dict[str, Image.Image] = {}
    env = UnityPy.load(str(resources_file))
    for obj in env.objects:
        if obj.type.name != "Sprite":
            continue

        data = obj.read()
        sprite_name = getattr(data, "m_Name", "")
        if not sprite_name:
            continue

        try:
            sprites[sprite_name] = data.image.convert("RGBA")
        except Exception as exc:  # UnityPy can fail on malformed or non-readable sprites.
            print(f"Skipping unreadable sprite {sprite_name}: {exc}")

    return sprites


def clean_pngs(folder: Path) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    for path in folder.glob("*.png"):
        path.unlink()


def export_icons(entries: list[IconEntry], sprites: dict[str, Image.Image], out_dir: Path) -> list[dict[str, str]]:
    clean_pngs(out_dir)
    exported: list[dict[str, str]] = []
    missing = sorted({entry.sprite_name for entry in entries if entry.sprite_name not in sprites})
    if missing:
        print(f"Missing {len(missing)} referenced sprites: {', '.join(missing[:20])}")

    seen: set[str] = set()
    for entry in entries:
        if entry.sprite_name in seen:
            continue

        image = sprites.get(entry.sprite_name)
        if image is None:
            continue

        filename = f"{sanitize_filename(entry.sprite_name)}.png"
        image.save(out_dir / filename)
        exported.append(
            {
                "spriteName": entry.sprite_name,
                "source": entry.source,
                "path": filename,
                "width": str(image.width),
                "height": str(image.height),
            }
        )
        seen.add(entry.sprite_name)

    return sorted(exported, key=lambda item: item["spriteName"].lower())


def write_index(exported: list[dict[str, str]], out_dir: Path) -> None:
    index = [
        {
            "spriteName": item["spriteName"],
            "source": item["source"],
            "path": item["path"],
            "width": int(item["width"]),
            "height": int(item["height"]),
        }
        for item in exported
    ]
    (out_dir / "index.json").write_text(json.dumps(index, indent=2) + "\n")


def create_composite(exported: list[dict[str, str]], out_dir: Path, filename: str) -> None:
    if not exported:
        return

    images = [(item, Image.open(out_dir / item["path"]).convert("RGBA")) for item in exported]
    max_dimension = max(max(image.width, image.height) for _, image in images)
    tile_size = max(96, max_dimension + 24)
    columns = min(12, max(1, math.ceil(math.sqrt(len(images) * 1.5))))
    rows = math.ceil(len(images) / columns)
    composite = Image.new("RGBA", (columns * tile_size, rows * tile_size), (0, 0, 0, 0))

    for index, (_, image) in enumerate(images):
        x = (index % columns) * tile_size + (tile_size - image.width) // 2
        y = (index // columns) * tile_size + (tile_size - image.height) // 2
        composite.alpha_composite(image, (x, y))

    composite.save(out_dir / filename)


def create_labeled_contact_sheet(exported: list[dict[str, str]], out_dir: Path, filename: str) -> None:
    if not exported:
        return

    images = [(item, Image.open(out_dir / item["path"]).convert("RGBA")) for item in exported]
    tile_size = 168
    icon_size = 96
    label_height = 46
    padding = 14
    columns = min(7, max(1, math.ceil(math.sqrt(len(images) * 1.35))))
    rows = math.ceil(len(images) / columns)
    sheet = Image.new("RGBA", (columns * tile_size, rows * (tile_size + label_height)), (26, 33, 36, 255))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    for index, (item, image) in enumerate(images):
        column = index % columns
        row = index // columns
        x0 = column * tile_size
        y0 = row * (tile_size + label_height)
        thumbnail = image.copy()
        thumbnail.thumbnail((icon_size, icon_size), Image.Resampling.LANCZOS)
        x = x0 + (tile_size - thumbnail.width) // 2
        y = y0 + padding + (icon_size - thumbnail.height) // 2
        sheet.alpha_composite(thumbnail, (x, y))

        label = item["spriteName"]
        words = re.findall(r"[A-Z]?[a-z]+|[A-Z]+(?=[A-Z]|$)|\d+", label)
        label_lines = [" ".join(words[:2]), " ".join(words[2:])] if len(words) > 2 else [" ".join(words)]
        for line_index, line in enumerate(label_lines):
            if not line:
                continue

            text_bbox = draw.textbbox((0, 0), line, font=font)
            text_width = text_bbox[2] - text_bbox[0]
            text_x = x0 + (tile_size - text_width) // 2
            text_y = y0 + icon_size + padding + 6 + line_index * 15
            draw.text((text_x, text_y), line, fill=(230, 222, 198, 255), font=font)

    sheet.save(out_dir / filename)


def with_explicit_menu_entries(entries: list[IconEntry]) -> list[IconEntry]:
    explicit = [
        IconEntry(
            key=f"explicit:{sprite_name}",
            sprite_name=sprite_name,
            source="explicit-bottom-bar-tool",
        )
        for sprite_name in EXPLICIT_MENU_ICON_NAMES
    ]
    return sorted(entries + explicit, key=lambda entry: (entry.sprite_name.lower(), entry.source))


def collect_status_entries() -> list[IconEntry]:
    return [
        IconEntry(
            key=f"status:{sprite_name}",
            sprite_name=sprite_name,
            source="timberborn-status-sprite",
        )
        for sprite_name in STATUS_ICON_NAMES
    ]


def main() -> None:
    args = parse_args()
    game_data = args.game_data.expanduser()
    resources_file = game_data / "resources.assets"
    blueprints_root = game_data / "StreamingAssets" / "Modding" / "Blueprints"
    if not resources_file.exists():
        raise SystemExit(f"Could not find resources.assets at {resources_file}")
    if not blueprints_root.exists():
        raise SystemExit(f"Could not find blueprint root at {blueprints_root}")

    sprites = collect_sprites(resources_file)
    out_root = args.out_root
    menu_out = out_root / "menu-icons"
    goods_out = out_root / "goods-icons"
    status_out = out_root / "status-icons"

    menu_entries = with_explicit_menu_entries(collect_blueprint_entries(blueprints_root, MENU_BLUEPRINT_FOLDERS))
    goods_entries = collect_blueprint_entries(blueprints_root, (GOODS_BLUEPRINT_FOLDER,))
    status_entries = collect_status_entries()

    menu_exported = export_icons(menu_entries, sprites, menu_out)
    goods_exported = export_icons(goods_entries, sprites, goods_out)
    status_exported = export_icons(status_entries, sprites, status_out)

    write_index(menu_exported, menu_out)
    write_index(goods_exported, goods_out)
    write_index(status_exported, status_out)
    create_composite(menu_exported, menu_out, "composite.png")
    create_composite(goods_exported, goods_out, "composite.png")
    create_composite(status_exported, status_out, "composite.png")
    create_labeled_contact_sheet(status_exported, status_out, "contact-sheet.png")

    print(f"Exported {len(menu_exported)} menu icons to {menu_out}")
    print(f"Exported {len(goods_exported)} goods icons to {goods_out}")
    print(f"Exported {len(status_exported)} status icons to {status_out}")
    print(f"Wrote {menu_out / 'composite.png'}")
    print(f"Wrote {goods_out / 'composite.png'}")
    print(f"Wrote {status_out / 'composite.png'}")
    print(f"Wrote {status_out / 'contact-sheet.png'}")


if __name__ == "__main__":
    main()
