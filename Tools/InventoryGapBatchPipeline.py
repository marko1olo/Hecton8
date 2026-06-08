#!/usr/bin/env python3
"""Run the offline inventory gap batch pipeline from a generated source sheet.

This does not launch Unity, edit .meta files, or bind ItemData icons. It prepares
project-local PNGs, atlas files, and a binding map that the Unity editor runner
can import and bind later.
"""

from __future__ import annotations

import argparse
from datetime import datetime
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]
BATCH_LABEL_PATTERN = re.compile(r"^[A-Za-z0-9_-]+$")


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def run(command: list[str]) -> None:
    print("+ " + " ".join(command), flush=True)
    subprocess.run(command, cwd=ROOT, check=True)


def copy_tree_files(source: Path, destination: Path, pattern: str) -> int:
    destination.mkdir(parents=True, exist_ok=True)
    count = 0
    for path in sorted(source.glob(pattern)):
        if path.name.endswith(".meta"):
            continue

        target = destination / path.name
        shutil.copy2(path, target)
        count += 1
    return count


def reset_generated_dir(path: Path) -> None:
    if path.exists():
        metas = list(path.rglob("*.meta"))
        if metas:
            preview = ", ".join(display_path(meta) for meta in metas[:3])
            raise RuntimeError(
                f"Refusing to clear imported Unity asset directory with .meta files: "
                f"{display_path(path)}. Use a new batch name. metas={preview}"
            )
        shutil.rmtree(path)
    path.mkdir(parents=True, exist_ok=True)


def has_non_meta_content(path: Path) -> bool:
    if not path.exists():
        return False

    for child in path.rglob("*"):
        if child.name.endswith(".meta"):
            continue
        return True
    return False


def require_overwrite_allowed(path: Path, label: str, allow_overwrite: bool) -> None:
    if allow_overwrite or not has_non_meta_content(path):
        return

    raise RuntimeError(
        f"Refusing to overwrite existing {label}: {display_path(path)}. "
        "Use a new batch name or pass --allow-overwrite after visual/review state is preserved."
    )


def is_under_assets(path: Path) -> bool:
    try:
        resolved = path.resolve()
        assets = (ROOT / "Assets").resolve()
        return resolved == assets or assets in resolved.parents
    except OSError:
        return False


def backup_existing_file(path: Path) -> Path | None:
    if not path.exists():
        return None

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup = path.with_name(f"{path.name}.bak_{timestamp}")
    shutil.copy2(path, backup)
    return backup


def count_csv_names(raw: str) -> int:
    return len([part for part in raw.split(",") if part.strip()])


def load_names_from_spec(spec_json: Path) -> str:
    payload = json.loads(spec_json.read_text(encoding="utf-8-sig"))
    names: list[str] = []
    for item in payload.get("items", []) or []:
        safe_name = str(item.get("safeName", "")).strip()
        if not safe_name:
            raise RuntimeError(f"Spec item is missing safeName: {spec_json}")
        names.append(safe_name)

    if not names:
        raise RuntimeError(f"Spec contains no items: {spec_json}")

    return ",".join(names)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path, help="Generated source sheet image.")
    parser.add_argument("--batch", required=True, help="Batch label, e.g. Batch32.")
    parser.add_argument("--previous-binding-map", type=Path)
    parser.add_argument("--spec-json", type=Path, help="Frozen gap spec JSON used to generate the source sheet.")
    parser.add_argument("--limit", type=int, default=12)
    parser.add_argument("--grid-rows", type=int, default=3)
    parser.add_argument("--grid-columns", type=int, default=4)
    parser.add_argument("--stem-prefix", default="", help="Defaults to DRAFT_TX_<batch>_InventoryGap.")
    parser.add_argument("--working-output", type=Path, default=Path("Docs/GeneratedAssets/Gemini/Outputs"))
    parser.add_argument("--asset-root", type=Path, default=Path("Assets/_Project/Art/Sprites/ui/InventoryGenerated"))
    parser.add_argument("--allow-non-asset-root", action="store_true")
    parser.add_argument("--grabcut-iterations", type=int, default=4)
    parser.add_argument("--segmentation-max-side", type=int, default=512)
    parser.add_argument("--edge-margin-px", type=int, default=32)
    parser.add_argument("--allow-overwrite", action="store_true")
    parser.add_argument(
        "--source-edge-margin-px",
        type=int,
        default=32,
        help="Reserved raw source-cell margin. Foreground entering this band fails/reviews before atlas packing.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    source = args.source.resolve()
    if not source.exists():
        raise FileNotFoundError(source)

    batch = args.batch.strip()
    if not BATCH_LABEL_PATTERN.fullmatch(batch):
        raise RuntimeError(f"Invalid batch label: {args.batch!r}. Use only letters, numbers, underscore, and dash.")

    stem_prefix = args.stem_prefix.strip() or f"DRAFT_TX_{batch}_InventoryGap"
    previous_binding_map = args.previous_binding_map.resolve() if args.previous_binding_map else None
    spec_json = args.spec_json.resolve() if args.spec_json else None
    if spec_json is None and previous_binding_map is None:
        raise RuntimeError("Either --spec-json or --previous-binding-map is required.")
    working_root = (ROOT / args.working_output / batch / "InventoryGapObjects").resolve()
    asset_batch_root = (ROOT / args.asset_root / batch).resolve()
    asset_alpha_root = asset_batch_root / "Alpha512"
    asset_atlas_root = asset_batch_root / "Atlas"
    binding_map = asset_batch_root / "InventoryIconCandidateBindingMap.json"

    if spec_json:
        names = load_names_from_spec(spec_json)
    else:
        names_result = subprocess.run(
            [
                sys.executable,
                "-B",
                str(ROOT / "Tools/InventoryIconGapAudit.py"),
                "--binding-map",
                str(previous_binding_map),
                "--limit",
                str(args.limit),
                "--format",
                "names",
            ],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        )
        names = names_result.stdout.strip()

    if not names:
        raise RuntimeError("Gap audit returned no names.")

    expected_count = count_csv_names(names)
    if expected_count != args.limit:
        raise RuntimeError(f"Target list returned {expected_count} names, expected limit={args.limit}.")
    if args.grid_rows * args.grid_columns != expected_count:
        raise RuntimeError(
            f"Grid does not match target count. grid={args.grid_columns}x{args.grid_rows}, targets={expected_count}."
        )

    if not args.allow_non_asset_root and not is_under_assets(asset_batch_root):
        raise RuntimeError(
            f"Refusing non-Assets asset output root: {display_path(asset_batch_root)}. "
            "Pass --allow-non-asset-root only for temp smoke/test runs."
        )

    require_overwrite_allowed(working_root, "working inventory-icon output", args.allow_overwrite)
    require_overwrite_allowed(asset_alpha_root, "asset Alpha512 output", args.allow_overwrite)
    require_overwrite_allowed(asset_atlas_root, "asset atlas output", args.allow_overwrite)
    if binding_map.exists() and not args.allow_overwrite:
        raise RuntimeError(
            f"Refusing to overwrite existing binding map: {display_path(binding_map)}. "
            "Use a new batch name or pass --allow-overwrite after review state is no longer needed."
        )

    reset_generated_dir(working_root)
    reset_generated_dir(asset_alpha_root)
    reset_generated_dir(asset_atlas_root)
    if binding_map.exists():
        backup = backup_existing_file(binding_map)
        if backup:
            print(f"Backed up existing binding map: {display_path(backup)}")
        binding_map.unlink()
    bake_manifest = working_root / "InventoryIsolatedObjectBakeManifest.json"

    run(
        [
            sys.executable,
            "-B",
            "Tools/InventoryIsolatedObjectBaker.py",
            "--source",
            str(source),
            "--output",
            str(working_root),
            "--grid-rows",
            str(args.grid_rows),
            "--grid-columns",
            str(args.grid_columns),
            "--names",
            names,
            "--stem-prefix",
            stem_prefix,
            "--grabcut-iterations",
            str(args.grabcut_iterations),
            "--segmentation-max-side",
            str(args.segmentation_max_side),
            "--source-edge-margin-px",
            str(args.source_edge_margin_px),
            "--contact-columns",
            str(args.grid_columns),
        ]
    )

    copied = copy_tree_files(working_root / "Alpha512", asset_alpha_root, "*.png")
    if copied != args.limit:
        raise RuntimeError(f"Expected to copy {args.limit} Alpha512 PNGs, copied {copied}.")

    run(
        [
            sys.executable,
            "-B",
            "Tools/InventoryAtlasBaker.py",
            "--source",
            str(asset_alpha_root),
            "--output",
            str(asset_atlas_root),
            "--name",
            f"TX_{batch}_InventoryGenerated_CandidateAtlas",
            "--cell-size",
            "512",
            "--columns",
            str(args.grid_columns),
            "--scaled-cell-sizes",
            "256",
            "--edge-margin-px",
            str(args.edge_margin_px),
            "--source-bake-manifest",
            str(bake_manifest),
        ]
    )

    binding_command = [
        sys.executable,
        "-B",
        "Tools/InventoryIconBindingMapFromGapAudit.py",
        "--alpha-root",
        str(asset_alpha_root),
        "--output",
        str(binding_map),
        "--stem-prefix",
        stem_prefix,
        "--limit",
        str(args.limit),
        "--require-sprites",
    ]
    if spec_json:
        binding_command.extend(["--spec-json", str(spec_json)])
    if previous_binding_map:
        binding_command.extend(["--previous-binding-map", str(previous_binding_map)])
    run(binding_command)

    manifest = next(asset_atlas_root.glob("*_Manifest.json"))
    run(
        [
            sys.executable,
            "-B",
            "Tools/InventoryIconBindingMapValidator.py",
            "--map",
            str(binding_map),
            "--manifest",
            str(manifest),
            "--require-empty-icon",
            "--require-source-bake-manifest",
            "--edge-margin-px",
            str(args.edge_margin_px),
        ]
        + (["--spec-json", str(spec_json)] if spec_json else [])
    )

    preview_path = asset_atlas_root / f"PREVIEW_{batch}_InventoryGenerated_DiegeticSlots.png"
    readability_preview_path = asset_atlas_root / f"PREVIEW_{batch}_InventoryGenerated_Readability.png"
    run(
        [
            sys.executable,
            "-B",
            "Tools/InventoryBindingMapPreview.py",
            "--map",
            str(binding_map),
            "--output",
            str(preview_path),
            "--slot-size",
            "128",
            "--gap",
            "16",
            "--columns",
            str(args.grid_columns),
        ]
    )
    run(
        [
            sys.executable,
            "-B",
            "Tools/InventoryIconReadabilityPreview.py",
            "--map",
            str(binding_map),
            "--output",
            str(readability_preview_path),
            "--columns",
            "2",
            "--pending-outline",
            "c28a2cff",
        ]
    )

    print("INVENTORY_GAP_BATCH_PIPELINE_STATUS: PASS")
    print(f"source={display_path(source)}")
    print(f"workingOutput={display_path(working_root)}")
    print(f"assetAlphaRoot={display_path(asset_alpha_root)}")
    print(f"assetAtlasRoot={display_path(asset_atlas_root)}")
    print(f"bindingMap={display_path(binding_map)}")
    print(f"preview={display_path(preview_path)}")
    print(f"readabilityPreview={display_path(readability_preview_path)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
