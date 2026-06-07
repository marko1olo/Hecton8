#!/usr/bin/env python3
"""Audit current ItemData icon gaps and rank the next inventory icon sheet targets."""

from __future__ import annotations

import argparse
import hashlib
from dataclasses import dataclass
import json
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
ITEM_ROOT = ROOT / "Assets/_Project/Data/Items"

FIELD_PATTERN = re.compile(r"^\s*(?P<key>[A-Za-z_][A-Za-z0-9_]*):\s*(?P<value>.*?)\s*$")
FALLBACK_PATTERN = re.compile(r"^\s*fallbackText:\s*(?P<value>.*?)\s*$")

CATEGORY_NAMES = {
    0: "Miscellaneous",
    1: "Material",
    2: "Tool",
    3: "Equipment",
    4: "Consumable",
    5: "Component",
    6: "Organic",
}

TIER_NAMES = {
    0: "None",
    1: "Tier0",
    2: "Tier1",
    3: "Tier2",
    4: "Tier3",
}

HIGH_VALUE_TERMS = (
    "Emergency",
    "O2",
    "Oxygen",
    "Battery",
    "Cell",
    "Copper",
    "Titanium",
    "Repair",
    "Scanner",
    "Builder",
    "Laser",
    "Pressure",
    "Cooling",
    "Beacon",
    "Salvage",
    "Electrolyte",
    "Hydraulic",
    "Sensor",
    "Seal",
)

VISUAL_HINTS = {
    "SalvageSampler": "dedicated salvage sampler tool with short cutting jaw, sample corer tip, bio-growth scraping edge, sealed grip, pressure-rated hinge",
    "ElectrolyteAmpoule": "single rugged electrolyte ampoule held alone in a protective pressure sleeve, translucent fluid window, capped injection neck",
    "SeafloorDrill": "compact seafloor drill with short hardened bit, pressure-sealed motor housing, mineral dust scuffs, two-hand industrial grip",
    "BeaconDeployer": "compact beacon deployment tool with folded handle, antenna socket, pressure-rated clasp",
    "Builder": "rugged fabrication builder tool with industrial grip, modular nozzle, material feed port",
    "LaserCutter": "laser cutter tool with ceramic heat shield, lens shroud, gasket seams, scorched nozzle edge",
    "Scanner": "hydroacoustic scanner wand with sensor face, hydrophone ribs, blank sealed glass",
    "CopperWire": "bundled copper wire spool with real copper strands, black insulating ties, pressure-safe spool core",
    "PressureSeal": "pressure seal component with thick gasket ring, clamp segments, worn rubber and titanium bite marks",
    "SealantPack": "sealant pack with rugged squeeze cartridge, capped nozzle, plain industrial polymer pouch",
    "SensorPackage": "sealed sensor module cluster with glass sensor eye, ceramic sockets, cable ports",
    "FieldMedGel": "field med gel injector pack with translucent gel chamber, sealed cap, emergency-use silhouette",
    "HighCapacityCell": "advanced high-capacity power cell with layered casing, heat vents, restrained cyan charge accent",
    "HydraulicActuator": "short hydraulic piston assembly with oil-stained seals, brushed metal rod, compact mounting lugs",
    "EnvAnalyzer": "environmental analyzer tool with sample intake, probe fork, rugged handheld body, blank protected lens",
    "TitaniumScrap": "irregular titanium salvage plate fragment with torn pressure-hull edge, bolt holes, scraped oxide, cut brace rib, clearly scrap not a clean ingot",
}


@dataclass(frozen=True)
class ItemIconGap:
    path: Path
    stable_id: str
    display_name: str
    category: int
    progression_tier: int
    width: int
    height: int
    icon_empty: bool
    planned: bool
    priority: int


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def parse_scalar(value: str) -> str:
    value = value.strip()
    if value.startswith('"') and value.endswith('"') and len(value) >= 2:
        return value[1:-1]
    return value


def parse_int(fields: dict[str, str], key: str, default: int) -> int:
    try:
        return int(fields.get(key, str(default)))
    except ValueError:
        return default


def load_planned_ids(binding_map: Path | None) -> set[str]:
    if binding_map is None:
        return set()

    payload = json.loads(binding_map.read_text(encoding="utf-8-sig"))
    planned: set[str] = set()
    for binding in payload.get("bindings", []) or []:
        if not binding.get("enabled", False):
            continue
        persistent_id = str(binding.get("persistentId", "")).strip()
        if persistent_id:
            planned.add(persistent_id)
    return planned


def read_item(path: Path, planned_ids: set[str]) -> ItemIconGap:
    text = path.read_text(encoding="utf-8-sig")
    fields: dict[str, str] = {}
    first_fallback = ""
    for line in text.splitlines():
        match = FIELD_PATTERN.match(line)
        if match:
            key = match.group("key")
            value = parse_scalar(match.group("value"))
            fields.setdefault(key, value)
            continue

        fallback_match = FALLBACK_PATTERN.match(line)
        if fallback_match and not first_fallback:
            first_fallback = parse_scalar(fallback_match.group("value"))

    stable_id = fields.get("stableId", "").strip() or path.stem
    legacy_name = fields.get("legacyItemName", "").strip()
    old_item_name = fields.get("itemName", "").strip()
    display_name = legacy_name or old_item_name or first_fallback or path.stem
    category = parse_int(fields, "category", 0)
    progression_tier = parse_int(fields, "progressionTier", 0)
    width = max(1, parse_int(fields, "width", 1))
    height = max(1, parse_int(fields, "height", 1))
    icon_value = fields.get("icon", "")
    icon_empty = "fileID: 0" in icon_value or icon_value == ""
    planned = stable_id in planned_ids

    return ItemIconGap(
        path=path,
        stable_id=stable_id,
        display_name=display_name,
        category=category,
        progression_tier=progression_tier,
        width=width,
        height=height,
        icon_empty=icon_empty,
        planned=planned,
        priority=score_item(stable_id, display_name, category, progression_tier, width, height, planned),
    )


def score_item(
    stable_id: str,
    display_name: str,
    category: int,
    progression_tier: int,
    width: int,
    height: int,
    planned: bool,
) -> int:
    score = 0
    if category == 2:
        score += 100
    elif category == 4:
        score += 95
    elif category == 5:
        score += 80
    elif category == 1:
        score += 55
    elif category == 3:
        score += 50
    else:
        score += 30

    if progression_tier in (1, 2):
        score += 20
    elif progression_tier == 3:
        score += 10

    haystack = f"{stable_id} {display_name}"
    for term in HIGH_VALUE_TERMS:
        if term.lower() in haystack.lower():
            score += 18
            break

    if width * height > 1:
        score += 5

    if planned:
        score -= 45

    return score


def collect_items(planned_ids: set[str]) -> list[ItemIconGap]:
    return [read_item(path, planned_ids) for path in sorted(ITEM_ROOT.rglob("*.asset"))]


def build_text(items: list[ItemIconGap], limit: int, include_planned: bool, force_ids: tuple[str, ...] = ()) -> str:
    lines: list[str] = []
    lines.append("INVENTORY_ICON_GAP_AUDIT")
    lines.append(f"items={len(items)}")
    missing = select_missing(items, limit, include_planned, force_ids, trim=False)
    lines.append(f"missing_unplanned={len(missing)}")
    lines.append("rank | score | category | tier | grid | state | persistentId | displayName | asset")
    for rank, item in enumerate(missing[:limit], start=1):
        state = "planned" if item.planned else "empty"
        category = CATEGORY_NAMES.get(item.category, str(item.category))
        tier = TIER_NAMES.get(item.progression_tier, str(item.progression_tier))
        lines.append(
            f"{rank:02d} | {item.priority:03d} | {category} | {tier} | "
            f"{item.width}x{item.height} | {state} | {item.stable_id} | "
            f"{item.display_name} | {display_path(item.path)}"
        )
    return "\n".join(lines) + "\n"


def print_text(items: list[ItemIconGap], limit: int, include_planned: bool, force_ids: tuple[str, ...] = ()) -> None:
    print(build_text(items, limit, include_planned, force_ids), end="")


def build_json(items: list[ItemIconGap], limit: int, include_planned: bool, force_ids: tuple[str, ...] = ()) -> str:
    missing = select_missing(items, limit, include_planned, force_ids)
    payload = [
        {
            "priority": item.priority,
            "persistentId": item.stable_id,
            "displayName": item.display_name,
            "category": CATEGORY_NAMES.get(item.category, str(item.category)),
            "progressionTier": TIER_NAMES.get(item.progression_tier, str(item.progression_tier)),
            "grid": [item.width, item.height],
            "state": "planned" if item.planned else "empty",
            "asset": display_path(item.path),
        }
        for item in missing
    ]
    return json.dumps(payload, indent=2) + "\n"


def print_json(items: list[ItemIconGap], limit: int, include_planned: bool, force_ids: tuple[str, ...] = ()) -> None:
    print(build_json(items, limit, include_planned, force_ids), end="")


def select_missing(
    items: list[ItemIconGap],
    limit: int,
    include_planned: bool,
    force_ids: tuple[str, ...] = (),
    *,
    trim: bool = True,
) -> list[ItemIconGap]:
    if limit <= 0:
        raise RuntimeError(f"Limit must be positive. actual={limit}")

    by_id = {item.stable_id: item for item in items}
    selected: list[ItemIconGap] = []
    selected_ids: set[str] = set()
    for stable_id in force_ids:
        item = by_id.get(stable_id)
        if item is None:
            raise RuntimeError(f"Forced persistentId is not an ItemData asset: {stable_id}")
        if not item.icon_empty:
            raise RuntimeError(f"Forced persistentId already has an icon: {stable_id}")
        if item.planned and not include_planned:
            raise RuntimeError(f"Forced persistentId is already planned by the binding map: {stable_id}")
        if stable_id in selected_ids:
            continue

        selected.append(item)
        selected_ids.add(stable_id)

    if len(selected) > limit:
        raise RuntimeError(f"Forced persistentId count exceeds limit. forced={len(selected)} limit={limit}")

    missing = [
        item
        for item in items
        if item.icon_empty and (include_planned or not item.planned) and item.stable_id not in selected_ids
    ]
    missing.sort(key=lambda item: (-item.priority, item.stable_id))
    selected.extend(missing)
    return selected[:limit] if trim else selected


def safe_name(item: ItemIconGap) -> str:
    raw = item.stable_id
    for prefix in ("Item_Tool_", "Comp_", "Data_"):
        if raw.startswith(prefix):
            raw = raw[len(prefix) :]
            break

    safe = re.sub(r"[^A-Za-z0-9]+", "", raw)
    if safe:
        return safe

    digest = hashlib.sha1(item.stable_id.encode("utf-8")).hexdigest()[:8]
    return f"Item{digest}"


def visual_hint(item: ItemIconGap) -> str:
    for key, hint in VISUAL_HINTS.items():
        if key in item.stable_id:
            return hint

    return item.display_name.lower().replace("-", " ") + ", believable pressure-rated AA survival-game inventory prop"


def build_names(items: list[ItemIconGap], limit: int, include_planned: bool, force_ids: tuple[str, ...] = ()) -> str:
    return ",".join(safe_name(item) for item in select_missing(items, limit, include_planned, force_ids)) + "\n"


def print_names(items: list[ItemIconGap], limit: int, include_planned: bool, force_ids: tuple[str, ...] = ()) -> None:
    print(build_names(items, limit, include_planned, force_ids), end="")


def build_spec(items: list[ItemIconGap], limit: int, include_planned: bool, force_ids: tuple[str, ...] = ()) -> str:
    selected = select_missing(items, limit, include_planned, force_ids)
    payload = {
        "schema": "hecton8.inventory_icon_gap_batch_spec.v1",
        "source": "Tools/InventoryIconGapAudit.py",
        "count": len(selected),
        "bakerNamesArgument": ",".join(safe_name(item) for item in selected),
        "items": [
            {
                "index": index,
                "priority": item.priority,
                "persistentId": item.stable_id,
                "displayName": item.display_name,
                "safeName": safe_name(item),
                "asset": display_path(item.path),
                "category": CATEGORY_NAMES.get(item.category, str(item.category)),
                "progressionTier": TIER_NAMES.get(item.progression_tier, str(item.progression_tier)),
                "grid": [item.width, item.height],
                "promptPhrase": visual_hint(item),
            }
            for index, item in enumerate(selected, start=1)
        ],
    }
    return json.dumps(payload, indent=2) + "\n"


def print_spec(items: list[ItemIconGap], limit: int, include_planned: bool, force_ids: tuple[str, ...] = ()) -> None:
    print(build_spec(items, limit, include_planned, force_ids), end="")


def parse_force_ids(values: list[str] | None) -> tuple[str, ...]:
    if not values:
        return ()

    force_ids: list[str] = []
    seen: set[str] = set()
    for raw in values:
        for part in raw.split(","):
            stable_id = part.strip()
            if not stable_id or stable_id in seen:
                continue

            force_ids.append(stable_id)
            seen.add(stable_id)

    return tuple(force_ids)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--binding-map", type=Path)
    parser.add_argument("--include-planned", action="store_true")
    parser.add_argument("--limit", type=int, default=24)
    parser.add_argument(
        "--force-persistent-id",
        action="append",
        help="PersistentId to place first in the result if it is currently missing an icon. May be repeated or comma-separated.",
    )
    parser.add_argument("--format", choices=("text", "json", "spec", "names"), default="text")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    planned = load_planned_ids(args.binding_map.resolve() if args.binding_map else None)
    items = collect_items(planned)
    force_ids = parse_force_ids(args.force_persistent_id)
    try:
        if args.format == "json":
            content = build_json(items, args.limit, args.include_planned, force_ids)
        elif args.format == "spec":
            content = build_spec(items, args.limit, args.include_planned, force_ids)
        elif args.format == "names":
            content = build_names(items, args.limit, args.include_planned, force_ids)
        else:
            content = build_text(items, args.limit, args.include_planned, force_ids)
    except RuntimeError as exception:
        print(f"INVENTORY_ICON_GAP_AUDIT_STATUS: FAIL {exception}")
        return 1

    if args.output:
        output = args.output.resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(content, encoding="utf-8")
    else:
        print(content, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
