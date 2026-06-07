#!/usr/bin/env python3
"""Validate generated inventory icon binding maps before Unity assignment."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
STABLE_ID_PATTERN = re.compile(r"^\s*stableId:\s*(.*?)\s*$", re.MULTILINE)


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def load_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def project_path(raw_path: str) -> Path:
    path = Path(raw_path)
    if path.is_absolute():
        return path
    return ROOT / path


def normalize_asset_path(raw_path: str) -> str:
    return raw_path.replace("\\", "/").strip()


def is_under_path(path: str, root: str) -> bool:
    normalized = normalize_asset_path(path)
    normalized_root = normalize_asset_path(root).rstrip("/")
    return normalized.startswith(normalized_root + "/")


def positive_unit_float(raw: str) -> float:
    try:
        value = float(raw)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("must be a number") from exc
    if value < 0.0 or value > 1.0:
        raise argparse.ArgumentTypeError("must be between 0.0 and 1.0")
    return value


def positive_int(raw: str) -> int:
    try:
        value = int(raw)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("must be an integer") from exc
    if value <= 0:
        raise argparse.ArgumentTypeError("must be positive")
    return value


def read_item_stable_id(path: Path) -> str:
    return read_item_stable_id_from_text(load_text(path), path)


def read_item_stable_id_from_text(text: str, path: Path) -> str:
    match = STABLE_ID_PATTERN.search(text)
    if not match:
        return path.stem

    value = match.group(1).strip().strip("\"'")
    return value or path.stem


def validate_sprite(path: Path, min_coverage: float, edge_margin_px: int) -> list[str]:
    errors: list[str] = []
    if not path.exists():
        return [f"missing sprite: {display_path(path)}"]

    with Image.open(path) as source_image:
        image = source_image.convert("RGBA")

    alpha = image.getchannel("A")
    solid = alpha.point(lambda value: 255 if value > 8 else 0)
    coverage = sum(1 for value in solid.getdata() if value > 0) / float(image.width * image.height)
    edge = max(1, min(edge_margin_px, image.width // 2, image.height // 2))
    touches_edge = bool(
        any(solid.crop((0, 0, image.width, edge)).getdata())
        or any(solid.crop((0, image.height - edge, image.width, image.height)).getdata())
        or any(solid.crop((0, 0, edge, image.height)).getdata())
        or any(solid.crop((image.width - edge, 0, image.width, image.height)).getdata())
    )
    if coverage < min_coverage:
        errors.append(f"low alpha coverage {coverage:.4f}: {display_path(path)}")
    if touches_edge:
        errors.append(f"sprite alpha touches edge: {display_path(path)}")
    return errors


def infer_manifest_path(map_path: Path) -> Path | None:
    atlas_dir = map_path.parent / "Atlas"
    if not atlas_dir.exists():
        return None

    candidates = sorted(atlas_dir.glob("*_Manifest.json"))
    if len(candidates) != 1:
        return None

    return candidates[0]


def validate_manifest(
    manifest_path: Path | None,
    min_coverage: float,
    edge_margin_px: int,
    allow_cell_edge_touch: bool,
) -> tuple[set[str], str, list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []
    manifest_sources: set[str] = set()
    source_root = ""

    if manifest_path is None:
        warnings.append("no atlas manifest supplied or inferred")
        return manifest_sources, source_root, errors, warnings

    if not manifest_path.exists():
        errors.append(f"missing manifest: {display_path(manifest_path)}")
        return manifest_sources, source_root, errors, warnings

    payload = json.loads(load_text(manifest_path))
    source_root = normalize_asset_path(payload.get("source", ""))
    atlas_path = normalize_asset_path(payload.get("atlas", ""))
    entries = payload.get("entries", [])
    cell_size = int(payload.get("cellSizePx", 0) or 0)
    columns = int(payload.get("columns", 0) or 0)
    rows = int(payload.get("rows", 0) or 0)

    if not source_root:
        errors.append(f"manifest missing source root: {display_path(manifest_path)}")
    elif not project_path(source_root).exists():
        errors.append(f"manifest source root missing: {source_root}")

    if not atlas_path:
        errors.append(f"manifest missing atlas path: {display_path(manifest_path)}")
    elif not project_path(atlas_path).exists():
        errors.append(f"manifest atlas missing: {atlas_path}")

    if cell_size <= 0 or columns <= 0 or rows <= 0:
        errors.append(f"manifest grid must be positive: cellSizePx={cell_size} columns={columns} rows={rows}")

    if not isinstance(entries, list) or not entries:
        errors.append(f"manifest has no entries: {display_path(manifest_path)}")
        return manifest_sources, source_root, errors, warnings

    if source_root:
        source_pngs = sorted(project_path(source_root).glob("*.png")) if project_path(source_root).exists() else []
        if len(source_pngs) != len(entries):
            errors.append(f"manifest entry count does not match source PNG count: entries={len(entries)} pngs={len(source_pngs)}")

    atlas_size: tuple[int, int] | None = None
    if atlas_path and project_path(atlas_path).exists():
        with Image.open(project_path(atlas_path)) as image:
            atlas_size = image.size
        if cell_size > 0 and columns > 0 and rows > 0:
            expected_size = (columns * cell_size, rows * cell_size)
            if atlas_size != expected_size:
                errors.append(f"atlas size mismatch: expected={expected_size} actual={atlas_size} path={atlas_path}")

    seen_names: set[str] = set()
    for index, entry in enumerate(entries):
        name = str(entry.get("name", "")).strip()
        source = normalize_asset_path(str(entry.get("source", "")))
        if not name:
            errors.append(f"manifest entry[{index}] has empty name")
        elif name in seen_names:
            errors.append(f"manifest duplicate entry name: {name}")
        else:
            seen_names.add(name)

        if not source:
            errors.append(f"manifest entry[{index}] has empty source")
        else:
            manifest_sources.add(source)
            if source_root and not is_under_path(source, source_root):
                errors.append(f"manifest entry[{index}] source outside source root: {source}")
            if not project_path(source).exists():
                errors.append(f"manifest entry[{index}] source missing: {source}")
            elif name and project_path(source).stem != name:
                errors.append(f"manifest entry[{index}] source/name mismatch: name={name} source={source}")
            else:
                errors.extend(
                    f"manifest entry[{index}] {error}"
                    for error in validate_sprite(project_path(source), min_coverage, edge_margin_px)
                )

        rect = entry.get("atlas_rect_px", [])
        if not isinstance(rect, list) or len(rect) != 4:
            errors.append(f"manifest entry[{index}] invalid atlas_rect_px")
        else:
            x, y, width, height = [int(value) for value in rect]
            if x < 0 or y < 0 or width <= 0 or height <= 0:
                errors.append(f"manifest entry[{index}] rect must be positive/non-negative: {rect}")
            if cell_size > 0 and (width != cell_size or height != cell_size):
                errors.append(f"manifest entry[{index}] rect size must match cellSizePx={cell_size}: {rect}")
            if atlas_size is not None and (x + width > atlas_size[0] or y + height > atlas_size[1]):
                errors.append(f"manifest entry[{index}] rect outside atlas bounds: {rect} atlas={atlas_size}")

        if entry.get("touches_cell_edge", False):
            message = f"manifest entry[{index}] touches cell edge: {name or source}"
            if allow_cell_edge_touch:
                warnings.append(message)
            else:
                errors.append(message)

    for scaled_atlas in payload.get("scaledAtlases", []) or []:
        scaled_path = normalize_asset_path(str(scaled_atlas))
        if not scaled_path:
            warnings.append("manifest contains empty scaled atlas path")
        elif not project_path(scaled_path).exists():
            errors.append(f"manifest scaled atlas missing: {scaled_path}")

    return manifest_sources, source_root, errors, warnings


def validate_bake_manifest(
    bake_manifest_path: Path | None,
    errors: list[str],
    warnings: list[str],
    allow_bake_review: bool,
) -> None:
    if bake_manifest_path is None:
        return

    if not bake_manifest_path.exists():
        errors.append(f"missing source bake manifest: {display_path(bake_manifest_path)}")
        return

    payload = json.loads(load_text(bake_manifest_path))
    source = normalize_asset_path(str(payload.get("source", "")).strip())
    if source and not project_path(source).exists():
        errors.append(f"source bake manifest source missing: {source}")

    preview = normalize_asset_path(str(payload.get("sourceGridMarginPreview", "")).strip())
    if not preview:
        errors.append(f"source bake manifest missing sourceGridMarginPreview: {display_path(bake_manifest_path)}")
    elif not project_path(preview).exists():
        errors.append(f"source bake manifest sourceGridMarginPreview missing: {preview}")

    review_count = int(payload.get("reviewCount", 0) or 0)
    items = list(payload.get("items", []) or [])
    failed_items = [
        f"{item.get('index', '?')}:{item.get('name', '<unnamed>')}:{item.get('status', '<missing>')}"
        for item in items
        if str(item.get("status", "")).strip() != "OK"
    ]

    if review_count > 0 or failed_items:
        message = (
            f"source bake manifest has review items: manifest={display_path(bake_manifest_path)} "
            f"reviewCount={review_count} items={', '.join(failed_items[:8])}"
        )
        if allow_bake_review:
            warnings.append(message)
        else:
            errors.append(message)


def read_source_bake_manifest_path(manifest_path: Path | None) -> Path | None:
    if manifest_path is None or not manifest_path.exists():
        return None

    payload = json.loads(load_text(manifest_path))
    raw_path = normalize_asset_path(str(payload.get("sourceBakeManifest", "")).strip())
    if not raw_path:
        return None

    return project_path(raw_path)


def validate_spec_order(spec_path: Path | None, bindings: list[dict], errors: list[str]) -> None:
    if spec_path is None:
        return

    if not spec_path.exists():
        errors.append(f"missing spec json: {display_path(spec_path)}")
        return

    payload = json.loads(load_text(spec_path))
    items = list(payload.get("items", []) or [])
    enabled = [binding for binding in bindings if binding.get("enabled", False)]
    if len(enabled) != len(items):
        errors.append(f"spec/enabled binding count mismatch: spec={len(items)} enabled={len(enabled)}")

    for index, item in enumerate(items):
        if index >= len(enabled):
            return

        binding = enabled[index]
        expected_pid = str(item.get("persistentId", "")).strip()
        expected_asset = normalize_asset_path(str(item.get("asset", "")).strip())
        expected_safe_name = str(item.get("safeName", "")).strip()
        expected_index = int(item.get("index", index + 1))
        binding_pid = str(binding.get("persistentId", "")).strip()
        binding_asset = normalize_asset_path(str(binding.get("itemAsset", "")).strip())
        sprite_asset = normalize_asset_path(str(binding.get("spriteAsset", "")).strip())

        if expected_pid and binding_pid != expected_pid:
            errors.append(
                f"binding/spec persistentId mismatch at enabled[{index}]: expected={expected_pid} actual={binding_pid}"
            )

        if expected_asset and binding_asset != expected_asset:
            errors.append(
                f"binding/spec itemAsset mismatch at enabled[{index}]: expected={expected_asset} actual={binding_asset}"
            )

        if expected_safe_name:
            expected_token = f"_{expected_index:02d}_{expected_safe_name}_Alpha512.png"
            if expected_token not in sprite_asset:
                errors.append(
                    f"binding/spec sprite name mismatch at enabled[{index}]: expected token {expected_token} in {sprite_asset}"
                )


def binding_has_review_metadata(binding: dict) -> bool:
    return (
        bool(str(binding.get("reviewedBy", "")).strip())
        and bool(str(binding.get("reviewedAt", "")).strip())
        and bool(str(binding.get("reviewNote", "")).strip())
    )


def binding_is_approved(binding: dict) -> bool:
    approved = bool(binding.get("approved", False)) or str(binding.get("reviewStatus", "")).strip().upper() == "APPROVED"
    return approved and binding_has_review_metadata(binding)


def validate(args: argparse.Namespace) -> int:
    map_path = Path(args.map).resolve()
    payload = json.loads(load_text(map_path))
    bindings = payload.get("bindings", [])
    errors: list[str] = []
    warnings: list[str] = []
    enabled_count = 0
    skipped_count = 0
    manifest_path = Path(args.manifest).resolve() if args.manifest else infer_manifest_path(map_path)
    manifest_sources, manifest_source_root, manifest_errors, manifest_warnings = validate_manifest(
        manifest_path,
        args.min_coverage,
        args.edge_margin_px,
        args.allow_cell_edge_touch,
    )
    errors.extend(manifest_errors)
    warnings.extend(manifest_warnings)
    source_bake_manifest_path = read_source_bake_manifest_path(manifest_path)
    if args.require_source_bake_manifest and source_bake_manifest_path is None:
        errors.append(f"atlas manifest missing sourceBakeManifest: {display_path(manifest_path) if manifest_path else '<none>'}")
    validate_bake_manifest(
        source_bake_manifest_path,
        errors,
        warnings,
        args.allow_bake_review,
    )
    spec_path = Path(args.spec_json).resolve() if args.spec_json else None
    validate_spec_order(spec_path, bindings, errors)
    seen_persistent_ids: dict[str, int] = {}
    seen_item_assets: dict[str, int] = {}
    seen_sprite_assets: dict[str, int] = {}

    for index, binding in enumerate(bindings):
        sprite_asset = normalize_asset_path(binding.get("spriteAsset", ""))
        sprite_path = project_path(sprite_asset)
        if sprite_asset:
            if not sprite_path.exists():
                errors.append(f"binding[{index}] missing sprite: {sprite_asset}")
            elif manifest_sources and is_under_path(sprite_asset, manifest_source_root):
                if sprite_asset not in manifest_sources:
                    errors.append(f"binding[{index}] sprite not present in manifest sources: {sprite_asset}")

        if not binding.get("enabled", False):
            skipped_count += 1
            if sprite_asset:
                errors.extend(
                    f"binding[{index}] {error}"
                    for error in validate_sprite(sprite_path, args.min_coverage, args.edge_margin_px)
            )
            continue

        enabled_count += 1
        if args.require_approved_bindings and not binding_is_approved(binding):
            errors.append(
                f"binding[{index}] enabled binding is not visually approved: "
                f"persistentId={binding.get('persistentId', '')} sprite={sprite_asset}"
            )

        item_asset = normalize_asset_path(binding.get("itemAsset", ""))
        item_path = project_path(item_asset)
        persistent_id = str(binding.get("persistentId", "")).strip()
        if not persistent_id:
            errors.append(f"binding[{index}] enabled binding has no persistentId")
        elif persistent_id in seen_persistent_ids:
            errors.append(f"binding[{index}] duplicate persistentId with binding[{seen_persistent_ids[persistent_id]}]: {persistent_id}")
        else:
            seen_persistent_ids[persistent_id] = index

        if not item_asset:
            errors.append(f"binding[{index}] enabled binding has no itemAsset")
        elif item_asset in seen_item_assets:
            errors.append(f"binding[{index}] duplicate itemAsset with binding[{seen_item_assets[item_asset]}]: {item_asset}")
        else:
            seen_item_assets[item_asset] = index

        if not sprite_asset:
            errors.append(f"binding[{index}] enabled binding has no spriteAsset")
        elif sprite_asset in seen_sprite_assets:
            errors.append(f"binding[{index}] duplicate spriteAsset with binding[{seen_sprite_assets[sprite_asset]}]: {sprite_asset}")
        else:
            seen_sprite_assets[sprite_asset] = index

        if not item_path.exists():
            errors.append(f"binding[{index}] missing item asset: {display_path(item_path)}")
        else:
            item_text = load_text(item_path)
            stable_id = read_item_stable_id_from_text(item_text, item_path)
            if persistent_id and stable_id != persistent_id:
                errors.append(f"binding[{index}] stableId mismatch: {display_path(item_path)} expected {persistent_id} actual {stable_id}")
            if args.require_empty_icon and "icon: {fileID: 0}" not in item_text:
                errors.append(f"binding[{index}] item icon is not empty: {display_path(item_path)}")

        if sprite_asset:
            errors.extend(
                f"binding[{index}] {error}"
                for error in validate_sprite(sprite_path, args.min_coverage, args.edge_margin_px)
            )

    print("INVENTORY_ICON_BINDING_MAP_VALIDATOR")
    print(f"map={display_path(map_path)}")
    print(f"manifest={display_path(manifest_path) if manifest_path else '<none>'}")
    print(f"enabled={enabled_count}")
    print(f"skipped={skipped_count}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map", required=True)
    parser.add_argument("--manifest", help="Optional atlas manifest path. Defaults to the only *_Manifest.json under the map sibling Atlas folder.")
    parser.add_argument("--spec-json", help="Optional frozen gap spec JSON. Validates enabled binding order and target identity.")
    parser.add_argument("--min-coverage", type=positive_unit_float, default=0.03)
    parser.add_argument("--edge-margin-px", type=positive_int, default=12)
    parser.add_argument("--allow-cell-edge-touch", action="store_true")
    parser.add_argument("--allow-bake-review", action="store_true")
    parser.add_argument("--require-source-bake-manifest", action="store_true")
    parser.add_argument("--require-approved-bindings", action="store_true")
    parser.add_argument("--require-empty-icon", action="store_true")
    return validate(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
