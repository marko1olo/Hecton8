#!/usr/bin/env python3
"""Validate world-tool pickup prefab external PBR assignment rules."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
APPLIER = ROOT / "Assets/_Project/Scripts/Editor/WorldToolExternalPbrMaterialApplier.cs"
ITEM_DATA_ROOT = ROOT / "Assets/_Project/Data/Items/Tools"
STAGING_SPAWNER = ROOT / "Assets/_Project/Scripts/ToolStagingSpawner.cs"
MATERIAL_ROOT = ROOT / "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607"
GEMINI_ATLAS_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases"
STATIC_MANIFESTS = {
    "PolyHaven": ROOT / "Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/PolyHavenExternalPBR_Manifest.json",
    "GeminiSingles_20260607": ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json",
}
RULE_PATTERN = re.compile(
    r'new\("(?P<prefab>Assets/[^"]+\.prefab)",\s*"(?P<renderer>[^"]+)",\s*(?P<provider>[^,]+),\s*"(?P<material>[^"]+)"\)'
)
CONST_PATTERN = re.compile(r'private\s+const\s+string\s+(?P<name>[A-Za-z0-9_]+)\s*=\s*"(?P<value>[^"]+)";')
UNITY_OBJECT_PATTERN = re.compile(r"^--- !u!(?P<class_id>\d+) &(?P<file_id>-?\d+)\n", re.MULTILINE)
GAME_OBJECT_NAME_PATTERN = re.compile(r"^\s*m_Name:\s*(?P<name>.+?)\s*$", re.MULTILINE)
OBJECT_GAME_OBJECT_PATTERN = re.compile(r"^\s*m_GameObject:\s*\{fileID:\s*(?P<file_id>-?\d+)\}", re.MULTILINE)


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def read_guid(asset_path: Path) -> str:
    meta_path = asset_path.with_suffix(asset_path.suffix + ".meta")
    if not meta_path.exists():
        return ""

    match = re.search(
        r"^guid:\s*([0-9a-fA-F]+)\s*$",
        meta_path.read_text(encoding="utf-8-sig"),
        re.MULTILINE,
    )
    return match.group(1) if match else ""


def material_asset_path(provider: str, material_id: str) -> Path:
    return MATERIAL_ROOT / provider / f"MAT_EXT_{provider}_{material_id}.mat"


def sanitize_provider_name(value: str) -> str:
    if not value.strip():
        return "Atlas"
    return "".join(char if char.isalnum() or char in "_-" else "_" for char in value)


def collect_manifest_paths() -> dict[str, Path]:
    manifests = dict(STATIC_MANIFESTS)
    if GEMINI_ATLAS_ROOT.exists():
        for manifest_path in sorted(GEMINI_ATLAS_ROOT.rglob("GeminiMaterialAtlas_Manifest.json")):
            provider = f"Gemini_{sanitize_provider_name(manifest_path.parent.name)}"
            manifests[provider] = manifest_path
    return manifests


def item_asset_path_for_prefab(prefab: str) -> Path:
    name = Path(prefab).name
    if not name.endswith("_World.prefab"):
        return ITEM_DATA_ROOT / name.replace(".prefab", ".asset")
    return ITEM_DATA_ROOT / f"{name[:-len('_World.prefab')]}.asset"


def item_prefab_guid(item_text: str) -> str:
    match = re.search(
        r"^\s*worldPrefab:\s*\{fileID:\s*\d+,\s*guid:\s*(?P<guid>[0-9a-fA-F]+),\s*type:\s*3\}",
        item_text,
        re.MULTILINE,
    )
    return match.group("guid") if match else ""


def staged_prefab_paths() -> set[str]:
    if not STAGING_SPAWNER.exists():
        return set()

    text = STAGING_SPAWNER.read_text(encoding="utf-8-sig")
    return {
        match.group("path")
        for match in re.finditer(r'"(?P<path>Assets/_Project/Prefabs/Items/Tools/Item_Tool_[^"]+_World\.prefab)"', text)
    }


def parse_unity_objects(text: str) -> dict[str, tuple[str, str]]:
    matches = list(UNITY_OBJECT_PATTERN.finditer(text))
    objects: dict[str, tuple[str, str]] = {}
    for index, match in enumerate(matches):
        start = match.start()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        objects[match.group("file_id")] = (match.group("class_id"), text[start:end])
    return objects


def renderer_blocks_for_token(prefab_text: str, renderer_token: str) -> list[str]:
    objects = parse_unity_objects(prefab_text)
    matching_game_objects: set[str] = set()
    token_lower = renderer_token.lower()

    for file_id, (class_id, block) in objects.items():
        if class_id != "1":
            continue
        name_match = GAME_OBJECT_NAME_PATTERN.search(block)
        if name_match and token_lower in name_match.group("name").lower():
            matching_game_objects.add(file_id)

    renderer_blocks: list[str] = []
    for class_id, block in objects.values():
        if class_id not in {"23", "137"}:
            continue
        go_match = OBJECT_GAME_OBJECT_PATTERN.search(block)
        if go_match and go_match.group("file_id") in matching_game_objects:
            renderer_blocks.append(block)
    return renderer_blocks


def load_material_assets(manifest_path: Path) -> dict[str, dict]:
    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    return {
        str(asset.get("id", "")).strip(): asset
        for asset in payload.get("assets", []) or []
        if asset.get("id")
    }


def load_constants(text: str) -> dict[str, str]:
    return {match.group("name"): match.group("value") for match in CONST_PATTERN.finditer(text)}


def resolve_provider(raw: str, constants: dict[str, str]) -> str:
    value = raw.strip()
    if value.startswith('"') and value.endswith('"'):
        return value.strip('"')
    return constants.get(value, value)


def validate(args: argparse.Namespace) -> int:
    applier_path = project_path(args.applier).resolve()
    text = applier_path.read_text(encoding="utf-8-sig")
    constants = load_constants(text)
    manifest_paths = collect_manifest_paths()
    manifests = {provider: load_material_assets(path) for provider, path in manifest_paths.items()}
    rules = list(RULE_PATTERN.finditer(text))
    errors: list[str] = []
    warnings: list[str] = []
    touched_prefabs: set[str] = set()
    rule_prefabs: set[str] = set()
    staged_paths = staged_prefab_paths()

    if not rules:
        errors.append(f"no assignment rules found in {display_path(applier_path)}")

    if "ValidateRules();" not in text:
        errors.append("world-tool applier must validate all prefabs/materials/renderers before mutating prefab assets")
    if "RequirePrefab(rule)" not in text:
        errors.append("world-tool applier must require prefab existence in validation and apply stages")
    if "RequireMaterial(rule)" not in text:
        errors.append("world-tool applier must require generated material existence in validation and apply stages")
    if "No renderer matched" not in text:
        errors.append("world-tool applier must throw with renderer token and prefab path when binding target is missing")
    if "File.Exists(rule.prefabPath)" in text:
        errors.append("world-tool applier must use Unity AssetDatabase prefab resolution, not cwd-relative File.Exists")
    if "Debug.LogWarning($" in text:
        errors.append("world-tool applier must not downgrade missing prefab/material/renderer failures to warnings")
    if "RecordFailure(" in text or "failures=" in text:
        errors.append("world-tool applier must not aggregate required binding failures after validation")

    for index, match in enumerate(rules):
        prefab = match.group("prefab")
        renderer_token = match.group("renderer")
        provider = resolve_provider(match.group("provider"), constants)
        material = match.group("material")
        prefab_path = project_path(prefab)
        touched_prefabs.add(prefab)
        if prefab in rule_prefabs:
            errors.append(f"rule[{index}] duplicate world tool prefab assignment: {prefab}")
        rule_prefabs.add(prefab)

        provider_assets = manifests.get(provider)
        if provider_assets is None:
            errors.append(f"rule[{index}] unknown material provider: {provider}")
        elif material not in provider_assets:
            errors.append(f"rule[{index}] material id not in provider manifest: provider={provider} material={material}")
        else:
            asset = provider_assets[material]
            if not bool(asset.get("heldToolAllowed", False)):
                errors.append(f"rule[{index}] material is not allowed for tool prefabs: provider={provider} material={material}")
            if bool(asset.get("watermarkRisk", False)):
                errors.append(f"rule[{index}] material has watermark risk: provider={provider} material={material}")

        if not prefab.startswith("Assets/_Project/Prefabs/Items/Tools/Item_Tool_"):
            errors.append(f"rule[{index}] world tool prefab path outside Items/Tools route: {prefab}")

        if not prefab_path.exists():
            errors.append(f"rule[{index}] missing prefab: {prefab}")
            continue

        item_path = item_asset_path_for_prefab(prefab)
        if not item_path.exists():
            errors.append(f"rule[{index}] missing ItemData asset for world prefab: prefab={prefab} expected={display_path(item_path)}")
        else:
            prefab_guid = read_guid(prefab_path)
            item_guid = item_prefab_guid(item_path.read_text(encoding="utf-8-sig"))
            if not prefab_guid:
                errors.append(f"rule[{index}] world prefab has no .meta guid: {prefab}")
            elif item_guid != prefab_guid:
                errors.append(
                    f"rule[{index}] ItemData.worldPrefab guid mismatch: item={display_path(item_path)} prefab={prefab} itemGuid={item_guid or '<missing>'} prefabGuid={prefab_guid}"
                )

        if prefab not in staged_paths:
            errors.append(f"rule[{index}] ToolStagingSpawner does not include world prefab: {prefab}")

        prefab_text = prefab_path.read_text(encoding="utf-8-sig")
        renderer_blocks = renderer_blocks_for_token(prefab_text, renderer_token)
        if not renderer_blocks:
            errors.append(f"rule[{index}] renderer token not found in prefab: token={renderer_token} prefab={prefab}")

        if args.post_apply:
            material_path = material_asset_path(provider, material)
            if not material_path.exists():
                errors.append(f"rule[{index}] post-apply missing material asset: {display_path(material_path)}")
                continue

            material_guid = read_guid(material_path)
            if not material_guid:
                errors.append(f"rule[{index}] post-apply missing material asset guid: {display_path(material_path)}")
            else:
                for block_index, renderer_block in enumerate(renderer_blocks):
                    if material_guid not in renderer_block:
                        errors.append(
                            f"rule[{index}] post-apply renderer[{block_index}] does not reference expected material guid: prefab={prefab} token={renderer_token} provider={provider} material={material}"
                        )

    item_world_prefabs: set[str] = set()
    for item_path in sorted(ITEM_DATA_ROOT.glob("Item_Tool_*.asset")):
        item_guid = item_prefab_guid(item_path.read_text(encoding="utf-8-sig"))
        if not item_guid:
            errors.append(f"tool ItemData has missing worldPrefab guid: {display_path(item_path)}")
            continue

        for prefab_path in sorted((ROOT / "Assets/_Project/Prefabs/Items/Tools").glob("Item_Tool_*_World.prefab")):
            if read_guid(prefab_path).lower() == item_guid.lower():
                item_world_prefabs.add(display_path(prefab_path))
                break
        else:
            errors.append(f"tool ItemData worldPrefab guid does not resolve to Items/Tools prefab: {display_path(item_path)} guid={item_guid}")

    missing_rule_prefabs = sorted(item_world_prefabs - rule_prefabs)
    for prefab in missing_rule_prefabs:
        errors.append(f"tool ItemData worldPrefab has no generated material assignment rule: {prefab}")

    extra_staged_prefabs = sorted(staged_paths - rule_prefabs)
    for prefab in extra_staged_prefabs:
        warnings.append(f"ToolStagingSpawner includes prefab without generated material rule: {prefab}")

    print("WORLD_TOOL_EXTERNAL_PBR_RULE_VALIDATOR")
    print(f"applier={display_path(applier_path)}")
    print(f"itemDataRoot={display_path(ITEM_DATA_ROOT)}")
    print(f"stagingSpawner={display_path(STAGING_SPAWNER)}")
    for provider, path in manifest_paths.items():
        print(f"manifest[{provider}]={display_path(path)}")
    print(f"rules={len(rules)}")
    print(f"prefabs={len(touched_prefabs)}")
    print(f"itemWorldPrefabs={len(item_world_prefabs)}")
    print(f"stagedPrefabs={len(staged_paths)}")
    print(f"postApply={args.post_apply}")
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
    parser.add_argument(
        "--post-apply",
        action="store_true",
        help="also verify expected generated material GUIDs are referenced by target prefabs after Unity apply",
    )
    return validate(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
