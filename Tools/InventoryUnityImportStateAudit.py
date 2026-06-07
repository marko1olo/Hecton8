#!/usr/bin/env python3
"""Read-only audit for generated inventory icon Unity import/binding state."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
FIELD_PATTERN = re.compile(r"^\s*(?P<key>[A-Za-z_][A-Za-z0-9_]*):\s*(?P<value>.*?)\s*$")


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw_path: str) -> Path:
    path = Path(raw_path)
    return path if path.is_absolute() else ROOT / path


def read_fields(path: Path) -> dict[str, str]:
    fields: dict[str, str] = {}
    if not path.exists():
        return fields

    for line in path.read_text(encoding="utf-8-sig").splitlines():
        match = FIELD_PATTERN.match(line)
        if match:
            fields.setdefault(match.group("key"), match.group("value").strip())
    return fields


def read_meta_guid(texture_path: Path) -> str:
    meta = Path(str(texture_path) + ".meta")
    if not meta.exists():
        return ""

    fields = read_fields(meta)
    return fields.get("guid", "").strip()


def load_bindings(binding_map: Path) -> list[dict]:
    payload = json.loads(binding_map.read_text(encoding="utf-8-sig"))
    return list(payload.get("bindings", []) or [])


def audit_texture_meta(
    path: Path,
    expected_max_size: int,
    errors: list[str],
    warnings: list[str],
    require_import_settings: bool,
) -> None:
    if not path.exists():
        errors.append(f"missing texture: {display_path(path)}")
        return

    meta = Path(str(path) + ".meta")
    if not meta.exists():
        errors.append(f"missing texture meta: {display_path(meta)}")
        return

    fields = read_fields(meta)
    checks = {
        "textureType": "8",
        "spriteMode": "1",
        "alphaIsTransparency": "1",
    }
    for key, expected in checks.items():
        actual = fields.get(key, "")
        if actual != expected:
            errors.append(f"{display_path(meta)} {key} expected {expected} actual {actual or '<missing>'}")

    if "enableMipMap: 0" not in meta.read_text(encoding="utf-8-sig"):
        errors.append(f"{display_path(meta)} mipmaps are not disabled")

    actual_max = fields.get("maxTextureSize", "")
    if actual_max != str(expected_max_size):
        message = (
            f"{display_path(meta)} maxTextureSize expected {expected_max_size} after importer, "
            f"actual {actual_max or '<missing>'}"
        )
        if require_import_settings:
            errors.append(message)
        else:
            warnings.append(message)


def infer_atlas_manifest_path(binding_map: Path) -> Path | None:
    atlas_dir = binding_map.parent / "Atlas"
    if not atlas_dir.exists():
        return None

    candidates = sorted(atlas_dir.glob("*_Manifest.json"))
    if len(candidates) != 1:
        return None

    return candidates[0]


def expected_atlas_max_size(path: Path, cell_size: int) -> int:
    name = path.name
    if "_256xCells" in name:
        return 2048

    if "_512xCells" in name:
        return 4096

    return max(512, min(4096, int(cell_size) * 8))


def audit_atlas_manifest(
    binding_map: Path,
    atlas_manifest: Path | None,
    errors: list[str],
    warnings: list[str],
    require_import_settings: bool,
) -> None:
    manifest_path = atlas_manifest or infer_atlas_manifest_path(binding_map)
    if manifest_path is None:
        errors.append(f"atlas manifest not found next to binding map: {display_path(binding_map.parent / 'Atlas')}")
        return

    if not manifest_path.exists():
        errors.append(f"missing atlas manifest: {display_path(manifest_path)}")
        return

    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    cell_size = int(payload.get("cellSizePx", 0) or 0)
    atlas_path = str(payload.get("atlas", "")).strip()
    scaled_atlases = list(payload.get("scaledAtlases", []) or [])
    if not atlas_path:
        errors.append(f"atlas manifest missing atlas path: {display_path(manifest_path)}")
    else:
        atlas = project_path(atlas_path)
        audit_texture_meta(atlas, expected_atlas_max_size(atlas, cell_size), errors, warnings, require_import_settings)

    for raw_scaled in scaled_atlases:
        scaled = project_path(str(raw_scaled).strip())
        audit_texture_meta(scaled, expected_atlas_max_size(scaled, cell_size), errors, warnings, require_import_settings)


def audit_binding(binding: dict, index: int, errors: list[str], warnings: list[str], require_bound: bool) -> None:
    if not binding.get("enabled", False):
        return

    item_asset = str(binding.get("itemAsset", "")).strip()
    sprite_asset = str(binding.get("spriteAsset", "")).strip()
    persistent_id = str(binding.get("persistentId", "")).strip()
    if not item_asset or not sprite_asset:
        errors.append(f"binding[{index}] missing itemAsset or spriteAsset")
        return

    item_path = project_path(item_asset)
    sprite_path = project_path(sprite_asset)
    if not item_path.exists():
        errors.append(f"binding[{index}] missing item asset: {item_asset}")
        return

    if not sprite_path.exists():
        errors.append(f"binding[{index}] missing sprite asset: {sprite_asset}")
        return

    item_text = item_path.read_text(encoding="utf-8-sig")
    if persistent_id and f"stableId: {persistent_id}" not in item_text:
        errors.append(f"binding[{index}] persistentId guard not found in {item_asset}: {persistent_id}")

    sprite_guid = read_meta_guid(sprite_path)
    if not sprite_guid:
        errors.append(f"binding[{index}] sprite guid missing: {sprite_asset}")
        return

    expected_guid_token = f"guid: {sprite_guid}"
    if expected_guid_token in item_text:
        return

    message = f"binding[{index}] item icon not bound to generated sprite yet: item={item_asset} sprite={sprite_asset}"
    if require_bound:
        errors.append(message)
    else:
        warnings.append(message)


def audit(args: argparse.Namespace) -> int:
    binding_map = args.binding_map.resolve()
    bindings = load_bindings(binding_map)
    errors: list[str] = []
    warnings: list[str] = []

    for binding in bindings:
        if not binding.get("enabled", False):
            continue

        sprite_asset = str(binding.get("spriteAsset", "")).strip()
        if sprite_asset:
            audit_texture_meta(project_path(sprite_asset), 512, errors, warnings, args.require_import_settings)

    if not args.skip_atlas_checks:
        audit_atlas_manifest(
            binding_map,
            args.atlas_manifest.resolve() if args.atlas_manifest else None,
            errors,
            warnings,
            args.require_import_settings,
        )

    for index, binding in enumerate(bindings):
        audit_binding(binding, index, errors, warnings, args.require_bound)

    print("INVENTORY_UNITY_IMPORT_STATE_AUDIT")
    print(f"bindingMap={display_path(binding_map)}")
    print(f"bindings={len(bindings)}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--binding-map", required=True, type=Path)
    parser.add_argument("--require-bound", action="store_true")
    parser.add_argument("--require-import-settings", action="store_true")
    parser.add_argument("--atlas-manifest", type=Path)
    parser.add_argument("--skip-atlas-checks", action="store_true")
    return audit(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
