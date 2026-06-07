#!/usr/bin/env python3
"""Generate a strict Gemini inventory-object sheet prompt from gap-audit spec JSON."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_REFERENCE = (
    "Docs/GeneratedAssets/Gemini/Outputs/Batch30/InventoryIsolatedObjects_20260607/"
    "TX_B30_InventoryIsolatedObjects_Source_20260607_Gemini.png"
)
NUMBER_WORDS = {
    1: "one",
    2: "two",
    3: "three",
    4: "four",
    5: "five",
    6: "six",
    7: "seven",
    8: "eight",
    9: "nine",
    10: "ten",
    11: "eleven",
    12: "twelve",
    13: "thirteen",
    14: "fourteen",
    15: "fifteen",
    16: "sixteen",
    17: "seventeen",
    18: "eighteen",
    19: "nineteen",
    20: "twenty",
}
ROW_NAMES = ("top", "middle", "bottom")
COL_NAMES = ("left", "second", "third", "right")
NEGATIVE_PHRASE_FRAGMENTS = (
    " with no text",
    ", no text",
    " with no screen text",
    ", no screen text",
    " with no label",
    ", no label",
    " with no cross symbol",
    ", no cross symbol",
)
PROMPT_BANNED_PATTERNS = (
    ("visible cell number", "Cell "),
    ("internal persistent id", "Item_"),
    ("internal persistent id", "Comp_"),
    ("internal persistent id", "Data_"),
    ("numeric grid spelling", " x "),
    ("legacy sprite filename", "OXYGEN.png"),
    ("legacy sprite filename", "BATTERY.png"),
    ("legacy sprite filename", "COPPER.png"),
    ("legacy sprite filename", "CUTTER.png"),
    ("legacy sprite filename", "MICRO.png"),
    ("legacy sprite filename", "TITANIUM.png"),
)


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def run_gap_audit(binding_map: Path, limit: int, force_ids: tuple[str, ...]) -> dict:
    command = [
        sys.executable,
        "-B",
        str(ROOT / "Tools/InventoryIconGapAudit.py"),
        "--binding-map",
        str(binding_map),
        "--limit",
        str(limit),
        "--format",
        "spec",
    ]
    for stable_id in force_ids:
        command.extend(["--force-persistent-id", stable_id])

    result = subprocess.run(command, cwd=ROOT, check=True, capture_output=True, text=True)
    return json.loads(result.stdout)


def number_word(value: int) -> str:
    return NUMBER_WORDS.get(value, str(value))


def position_label(index: int, columns: int, rows: int) -> str:
    row = (index - 1) // columns
    column = (index - 1) % columns
    if rows == 3 and columns == 4 and row < len(ROW_NAMES) and column < len(COL_NAMES):
        return f"{ROW_NAMES[row]}-{COL_NAMES[column]} position"

    return f"reading-order position {number_word(index)}"


def sanitize_prompt_phrase(phrase: str) -> str:
    cleaned = " " + phrase.strip()
    for fragment in NEGATIVE_PHRASE_FRAGMENTS:
        cleaned = cleaned.replace(fragment, "")

    return " ".join(cleaned.split())


def parse_force_ids(values: list[str]) -> tuple[str, ...]:
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


def render_prompt(spec: dict, reference: str) -> str:
    items = spec.get("items", [])
    columns = 4 if len(items) > 6 else max(1, min(3, len(items)))
    rows = max(1, math.ceil(len(items) / float(columns)))
    lines: list[str] = []
    lines.append("# HECTON-8 Gemini Prompt - Inventory Gap Sheet From Live ItemData")
    lines.append("")
    lines.append("Positive reference image:")
    lines.append(f"`{reference}`")
    lines.append("")
    lines.append(
        "Use the positive reference only for physical 3D prop readability, separated object-sheet "
        "composition, hard-surface material richness, and three-quarter inventory presentation. "
        "Do not copy the exact objects."
    )
    lines.append(
        "Reference caveat: if the reference contains cropped props, generator watermarks, or text-like "
        "surface marks, treat those as defects to avoid, not as style targets."
    )
    lines.append(
        "All new objects must have unmarked physical surfaces: scratches, seams, bevels, dirt, chips, and "
        "abstract wear are allowed, but any deliberate glyph-like stroke, label plate, serial mark, icon, "
        "printed symbol, or UI marking is a failure."
    )
    lines.append("")
    lines.append("Do not use old project UI sprites as references. They are legacy and must not influence this image.")
    lines.append("")
    lines.append("## Prompt")
    lines.append("")
    lines.append("Create one improved HECTON-8 inventory object source sheet.")
    lines.append("")
    lines.append(
        f"Generate {number_word(len(items))} distinct AA-quality physical objects in a clean invisible "
        f"{number_word(columns)}-column by {number_word(rows)}-row layout. Use the exact reading order below; "
        "the companion spec JSON maps these positions to project PersistentIds. Do not render any position "
        "markers, names, captions, numbers, letters, arrows, symbols, or grid."
    )
    lines.append("")
    for item in items:
        label = position_label(int(item["index"]), columns, rows)
        phrase = sanitize_prompt_phrase(str(item["promptPhrase"]))
        lines.append(f"{label}: {phrase}")
    lines.append("")
    lines.append(
        "Each object must be a believable AA/AAA survival-game inventory prop, not a flat icon. "
        "Aim above Subnautica item thumbnail quality: stronger material breakup, clearer silhouette, "
        "better industrial logic, less toy-like, less mobile-game."
    )
    lines.append("")
    lines.append("Layout constraints:")
    lines.append("- one object per invisible cell")
    lines.append("- large empty spacing between objects")
    lines.append("- every object fully inside its cell with at least one quarter of the cell kept as empty padding")
    lines.append("- keep a clear safety moat around every object: at least fifteen percent of cell width and height empty on all sides")
    lines.append("- no handle, drill bit, wire strand, nozzle, ring, or ingot corner may enter the outer cell-border band")
    lines.append("- each object centered as a complete physical product render, never a close-up crop")
    lines.append("- if a tool or resource feels too large for its cell, make it smaller rather than cropping it")
    lines.append("- no object touches the image border")
    lines.append("- no object is cropped")
    lines.append("- no overlap")
    lines.append("- no visible grid lines")
    lines.append("- neutral dark gray matte background, flat and removable")
    lines.append("- no floor horizon")
    lines.append("- no cast shadows that connect objects")
    lines.append("")
    lines.append("Hard negative constraints:")
    for negative in (
        "no text",
        "no labels",
        "no letters",
        "no numbers",
        "no alphanumeric glyphs",
        "no fake alien glyphs",
        "no label plates",
        "no text-like decal noise",
        "no readable markings printed on object surfaces",
        "no printed surface marks of any kind",
        "no screen UI text",
        "blank screens, lenses, and glass only",
        "no serial numbers",
        "no warning stickers",
        "no diagrams or pictograms that resemble labels",
        "no logos",
        "no UI frames",
        "no circular badges",
        "no square icon cards",
        "no inventory slot backgrounds",
        "no captions",
        "no sticker-sheet look",
        "no mobile-game icon style",
        "no flat vector art",
        "no cartoon toy look",
        "no object touching any edge",
        "no decorative sparkle on the objects",
    ):
        lines.append(f"- {negative}")
    lines.append("")
    lines.append("Rendering target:")
    lines.append(
        "three-quarter view, crisp edges, real thickness, bevels, bolts, seams, gaskets, "
        "scratches, chipped paint, grime, worn polymer, ceramic, glass, titanium, copper, rubber, "
        "restrained cyan instrument accents, strong readable silhouette at small inventory size, "
        "natural object-camera distance with no badge rim, halo, glow card, or app-store icon pose."
    )
    lines.append("")
    lines.append("Identity must come from silhouette, mechanical construction, material, color accents, and proportions only, never text.")
    return "\n".join(lines) + "\n"


def lint_prompt(prompt: str) -> None:
    for reason, token in PROMPT_BANNED_PATTERNS:
        if token in prompt:
            raise RuntimeError(f"Prompt lint failed: {reason}: {token}")

    for line in prompt.splitlines():
        if "position:" in line and " no " in line:
            raise RuntimeError(f"Prompt lint failed: negative phrase inside item line: {line}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--binding-map", type=Path)
    parser.add_argument("--spec-input", type=Path, help="Frozen spec JSON to render directly without running gap audit.")
    parser.add_argument("--limit", type=int, default=12)
    parser.add_argument("--reference", default=DEFAULT_REFERENCE)
    parser.add_argument(
        "--previous-source",
        dest="reference",
        help="Alias for --reference; use when the previous generated sheet is the positive reference.",
    )
    parser.add_argument("--output", type=Path)
    parser.add_argument("--spec-output", type=Path)
    parser.add_argument(
        "--force-persistent-id",
        action="append",
        default=[],
        help="PersistentId to place first in the generated spec if missing an icon. May be repeated or comma-separated.",
    )
    args = parser.parse_args()

    if args.spec_input:
        spec = json.loads(args.spec_input.resolve().read_text(encoding="utf-8-sig"))
    else:
        if args.binding_map is None:
            parser.error("--binding-map is required unless --spec-input is provided")
        force_ids = parse_force_ids(args.force_persistent_id)
        spec = run_gap_audit(args.binding_map.resolve(), args.limit, force_ids)
    prompt = render_prompt(spec, args.reference)
    lint_prompt(prompt)
    if args.spec_output:
        spec_output = args.spec_output.resolve()
        spec_output.parent.mkdir(parents=True, exist_ok=True)
        spec_output.write_text(json.dumps(spec, indent=2) + "\n", encoding="utf-8")
        print(f"INVENTORY_GEMINI_PROMPT_SPEC_STATUS: PASS output={display_path(spec_output)}")

    if args.output:
        output = args.output.resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(prompt, encoding="utf-8")
        print(f"INVENTORY_GEMINI_PROMPT_STATUS: PASS output={display_path(output)}")
    else:
        print(prompt, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
