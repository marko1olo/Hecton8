#!/usr/bin/env python3
"""Validate held-tool external PBR assignment rules before Unity API application."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
APPLIER = ROOT / "Assets/_Project/Scripts/Editor/HeldToolExternalPbrMaterialApplier.cs"
MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/PolyHavenExternalPBR_Manifest.json"
RULE_PATTERN = re.compile(r'new\("(?P<prefab>Assets/[^"]+\.prefab)",\s*"(?P<renderer>[^"]+)",\s*"(?P<material>[^"]+)"\)')


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def load_material_assets(manifest_path: Path) -> dict[str, dict]:
    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    return {
        str(asset.get("id", "")).strip(): asset
        for asset in payload.get("assets", []) or []
        if asset.get("id")
    }


def validate(args: argparse.Namespace) -> int:
    applier_path = project_path(args.applier).resolve()
    manifest_path = project_path(args.manifest).resolve()
    material_assets = load_material_assets(manifest_path)
    text = applier_path.read_text(encoding="utf-8-sig")
    rules = list(RULE_PATTERN.finditer(text))
    errors: list[str] = []
    warnings: list[str] = []
    touched_prefabs: set[str] = set()

    if not rules:
        errors.append(f"no assignment rules found in {display_path(applier_path)}")

    for index, match in enumerate(rules):
        prefab = match.group("prefab")
        renderer_token = match.group("renderer")
        material = match.group("material")
        prefab_path = project_path(prefab)
        touched_prefabs.add(prefab)

        if material not in material_assets:
            errors.append(f"rule[{index}] material id not in manifest: {material}")
        elif not bool(material_assets[material].get("heldToolAllowed", False)):
            errors.append(f"rule[{index}] material is not allowed for held tools: {material}")

        if not prefab_path.exists():
            errors.append(f"rule[{index}] missing prefab: {prefab}")
            continue

        prefab_text = prefab_path.read_text(encoding="utf-8-sig")
        if f"m_Name: {renderer_token}" not in prefab_text and renderer_token not in prefab_text:
            errors.append(f"rule[{index}] renderer token not found in prefab: token={renderer_token} prefab={prefab}")

    print("HELD_TOOL_EXTERNAL_PBR_RULE_VALIDATOR")
    print(f"applier={display_path(applier_path)}")
    print(f"manifest={display_path(manifest_path)}")
    print(f"rules={len(rules)}")
    print(f"prefabs={len(touched_prefabs)}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--applier", default=str(APPLIER.relative_to(ROOT)))
    parser.add_argument("--manifest", default=str(MANIFEST.relative_to(ROOT)))
    return validate(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
