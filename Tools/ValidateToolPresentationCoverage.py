#!/usr/bin/env python3
"""Validate tool world/material/icon presentation coverage across live authoring routes."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
TOOL_ITEM_ROOT = ROOT / "Assets/_Project/Data/Items/Tools"
WORLD_PREFAB_ROOT = ROOT / "Assets/_Project/Prefabs/Items/Tools"
WORLD_MATERIAL_APPLIER = ROOT / "Assets/_Project/Scripts/Editor/WorldToolExternalPbrMaterialApplier.cs"
STAGING_SPAWNER = ROOT / "Assets/_Project/Scripts/ToolStagingSpawner.cs"
RUN_INVENTORY_ICON_GEMINI_SHEET_INTAKE = ROOT / "Tools/RunInventoryIconGeminiSheetIntake.ps1"
RUN_TOOL_INVENTORY_BATCH33_SHEET_INTAKE = ROOT / "Tools/RunToolInventoryBatch33SheetIntake.ps1"
RUN_TOOL_PRESENTATION_UNITY_APPLY = ROOT / "Tools/RunToolPresentationUnityApply.ps1"
APPROVED_ICON_MAPS = (
    ROOT / "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch30/InventoryIconCandidateBindingMap.json",
    ROOT / "Assets/_Project/Art/Sprites/ui/InventoryGenerated/Batch33/InventoryIconCandidateBindingMap.json",
)
PENDING_ICON_SPECS = (
    ROOT / "Docs/GeneratedAssets/Gemini/Prompts/Batch33/3301_TOOL_INVENTORY_SHEET_FROM_WORLD_PREFABS_20260607.spec.json",
)
ICON_MAP_SPECS = {
    APPROVED_ICON_MAPS[1]: PENDING_ICON_SPECS[0],
}
REQUIRED_ICON_CONSUMERS = (
    (
        ROOT / "Assets/_Project/Scripts/PDAInventoryTab.cs",
        "tool.ToolData.icon",
        "PDA inventory tool strip",
    ),
    (
        ROOT / "Assets/_Project/Scripts/HUDQuickBar.cs",
        "tool.ToolData.icon",
        "HUD quickbar current tool slot",
    ),
    (
        ROOT / "Assets/_Project/Scripts/UI/PDALoadoutTab.cs",
        "item.icon",
        "PDA loadout slot",
    ),
    (
        ROOT / "Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs",
        "item.icon",
        "suit HUD item overlay",
    ),
    (
        ROOT / "Assets/_Project/Scripts/LocalizedInlineIconResolver.cs",
        "TryResolveItemChip(ItemData item",
        "localized inline item chip fallback",
    ),
)
ICON_STATE_APPROVED = "APPROVED"
ICON_STATE_REJECTED = "REJECTED"
ICON_STATE_PENDING_REVIEW = "PENDING_VISUAL_REVIEW"
MISSING_ICON_APPROVED_PENDING_UNITY = "approved_pending_unity"
MISSING_ICON_PENDING_REVIEW = "pending_visual_review"
MISSING_ICON_REJECTED_NEEDS_REGENERATION = "rejected_needs_regeneration"
MISSING_ICON_PENDING_GENERATION = "pending_generation"
MISSING_ICON_REJECTED_WITHOUT_SPEC = "rejected_without_spec"
MISSING_ICON_NO_ROUTE = "missing_route"


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def normalize_asset_path(raw_path: str) -> str:
    return raw_path.replace("\\", "/").strip()


def read_guid(asset_path: Path) -> str:
    meta_path = asset_path.with_suffix(asset_path.suffix + ".meta")
    if not meta_path.exists():
        return ""

    match = re.search(
        r"^guid:\s*([0-9a-fA-F]+)\s*$",
        meta_path.read_text(encoding="utf-8-sig"),
        re.MULTILINE,
    )
    return match.group(1).lower() if match else ""


def project_path(raw_path: str) -> Path:
    path = Path(raw_path)
    return path if path.is_absolute() else ROOT / path


def yaml_scalar(text: str, key: str) -> str:
    match = re.search(rf"^\s*{re.escape(key)}:\s*(.*?)\s*$", text, re.MULTILINE)
    return match.group(1).strip().strip("\"'") if match else ""


def world_prefab_guid(text: str) -> str:
    match = re.search(
        r"^\s*worldPrefab:\s*\{fileID:\s*\d+,\s*guid:\s*(?P<guid>[0-9a-fA-F]+),\s*type:\s*3\}",
        text,
        re.MULTILINE,
    )
    return match.group("guid").lower() if match else ""


def icon_is_assigned(text: str) -> bool:
    value = yaml_scalar(text, "icon")
    if not value:
        return False
    return "fileID: 0" not in value


def item_paths() -> list[Path]:
    return sorted(TOOL_ITEM_ROOT.glob("Item_Tool_*.asset"))


def prefab_guid_map() -> dict[str, str]:
    result: dict[str, str] = {}
    for prefab in sorted(WORLD_PREFAB_ROOT.glob("Item_Tool_*_World.prefab")):
        guid = read_guid(prefab)
        if guid:
            result[guid] = display_path(prefab)
    return result


def world_material_rule_paths() -> set[str]:
    if not WORLD_MATERIAL_APPLIER.exists():
        return set()

    text = WORLD_MATERIAL_APPLIER.read_text(encoding="utf-8-sig")
    return {
        normalize_asset_path(match.group("prefab"))
        for match in re.finditer(r'new\("(?P<prefab>Assets/[^"]+_World\.prefab)",\s*"[^"]+",\s*[^,]+,\s*"[^"]+"\)', text)
    }


def staged_prefab_paths() -> set[str]:
    if not STAGING_SPAWNER.exists():
        return set()

    text = STAGING_SPAWNER.read_text(encoding="utf-8-sig")
    return {
        normalize_asset_path(match.group("path"))
        for match in re.finditer(r'"(?P<path>Assets/_Project/Prefabs/Items/Tools/Item_Tool_[^"]+_World\.prefab)"', text)
    }


def binding_is_approved(binding: dict) -> bool:
    if not binding.get("enabled", False):
        return False
    approved = bool(binding.get("approved", False)) or str(binding.get("reviewStatus", "")).strip().upper() == "APPROVED"
    return approved and bool(str(binding.get("reviewedBy", "")).strip()) and bool(str(binding.get("reviewedAt", "")).strip())


def binding_review_state(binding: dict) -> str:
    if binding_is_approved(binding):
        return ICON_STATE_APPROVED

    if str(binding.get("reviewStatus", "")).strip().upper() == ICON_STATE_REJECTED:
        return ICON_STATE_REJECTED

    if not binding.get("enabled", False):
        return ""

    return ICON_STATE_PENDING_REVIEW


def spec_items_for_map(map_path: Path) -> list[dict]:
    spec_path = ICON_MAP_SPECS.get(map_path)
    if spec_path is None or not spec_path.exists():
        return []

    payload = json.loads(spec_path.read_text(encoding="utf-8-sig"))
    items = payload.get("items", []) if isinstance(payload, dict) else []
    return items if isinstance(items, list) else []


def persistent_id_for_binding_state(binding: dict, index: int, spec_items: list[dict]) -> str:
    persistent_id = str(binding.get("persistentId", "")).strip()
    if persistent_id:
        return persistent_id

    rejected_persistent_id = str(binding.get("rejectedPersistentId", "")).strip()
    if rejected_persistent_id:
        return rejected_persistent_id

    if binding_review_state(binding) == ICON_STATE_REJECTED and index < len(spec_items):
        return str(spec_items[index].get("persistentId", "")).strip()

    return ""


def icon_binding_review_states(errors: list[str], warnings: list[str]) -> dict[str, set[str]]:
    states: dict[str, set[str]] = {}
    approved_sources: dict[str, str] = {}
    for map_path in APPROVED_ICON_MAPS:
        if not map_path.exists():
            continue

        payload = json.loads(map_path.read_text(encoding="utf-8-sig"))
        spec_items = spec_items_for_map(map_path)
        for index, binding in enumerate(payload.get("bindings", []) or []):
            state = binding_review_state(binding)
            if not state:
                continue

            persistent_id = persistent_id_for_binding_state(binding, index, spec_items)
            if not persistent_id:
                errors.append(f"{state.lower()} binding[{index}] has empty persistentId and no spec fallback: map={display_path(map_path)}")
                continue

            item_asset = normalize_asset_path(str(binding.get("itemAsset", "")).strip())
            sprite_asset = normalize_asset_path(str(binding.get("spriteAsset", "")).strip())
            if state != ICON_STATE_REJECTED and not project_path(item_asset).exists():
                errors.append(f"{state.lower()} binding[{index}] item asset missing: map={display_path(map_path)} item={item_asset}")
            if not project_path(sprite_asset).exists():
                message = f"{state.lower()} binding[{index}] sprite asset missing: map={display_path(map_path)} sprite={sprite_asset}"
                if state == ICON_STATE_REJECTED:
                    warnings.append(message)
                else:
                    errors.append(message)

            if state == ICON_STATE_APPROVED:
                source = f"{display_path(map_path)} binding[{index}]"
                previous = approved_sources.get(persistent_id)
                if previous:
                    errors.append(f"duplicate approved icon binding for {persistent_id}: {previous} and {source}")
                else:
                    approved_sources[persistent_id] = source

            states.setdefault(persistent_id, set()).add(state)
    return states


def classify_missing_icon(persistent_id: str, binding_states: dict[str, set[str]], pending_specs: set[str]) -> str:
    states = binding_states.get(persistent_id, set())
    if ICON_STATE_APPROVED in states:
        return MISSING_ICON_APPROVED_PENDING_UNITY
    if ICON_STATE_PENDING_REVIEW in states:
        return MISSING_ICON_PENDING_REVIEW
    if ICON_STATE_REJECTED in states:
        return MISSING_ICON_REJECTED_NEEDS_REGENERATION if persistent_id in pending_specs else MISSING_ICON_REJECTED_WITHOUT_SPEC
    if persistent_id in pending_specs:
        return MISSING_ICON_PENDING_GENERATION
    return MISSING_ICON_NO_ROUTE


def pending_icon_specs(errors: list[str]) -> set[str]:
    pending: set[str] = set()
    seen: dict[str, str] = {}
    for spec_path in PENDING_ICON_SPECS:
        if not spec_path.exists():
            continue

        payload = json.loads(spec_path.read_text(encoding="utf-8-sig"))
        for index, item in enumerate(payload.get("items", []) or []):
            persistent_id = str(item.get("persistentId", "")).strip()
            item_asset = normalize_asset_path(str(item.get("asset", "")).strip())
            safe_name = str(item.get("safeName", "")).strip()
            if not persistent_id:
                errors.append(f"pending icon spec[{index}] has empty persistentId: {display_path(spec_path)}")
                continue
            if persistent_id in seen:
                errors.append(f"pending icon spec duplicate persistentId: {persistent_id} specs={seen[persistent_id]} and {display_path(spec_path)}")
            seen[persistent_id] = display_path(spec_path)
            if not item_asset or not project_path(item_asset).exists():
                errors.append(f"pending icon spec[{index}] item asset missing: spec={display_path(spec_path)} item={item_asset}")
            if not safe_name:
                errors.append(f"pending icon spec[{index}] missing safeName: spec={display_path(spec_path)} persistentId={persistent_id}")
            pending.add(persistent_id)
    return pending


def validate_icon_consumers(errors: list[str]) -> int:
    routes = 0
    for source_path, required_text, label in REQUIRED_ICON_CONSUMERS:
        if not source_path.exists():
            errors.append(f"icon consumer source missing: {display_path(source_path)} route={label}")
            continue

        text = source_path.read_text(encoding="utf-8-sig")
        if required_text not in text:
            errors.append(
                f"item presentation consumer route is missing expected code path: {display_path(source_path)} route={label}"
            )
            continue

        routes += 1

        if source_path.name == "LocalizedInlineIconResolver.cs":
            validate_localized_inline_item_fallback(text, errors)
    return routes


def source_block(text: str, start_marker: str, end_marker: str) -> str:
    start = text.find(start_marker)
    end = text.find(end_marker, start + len(start_marker)) if start >= 0 else -1
    return text[start:end] if start >= 0 and end > start else ""


def validate_localized_inline_item_fallback(text: str, errors: list[str]) -> None:
    string_block = source_block(
        text,
        "public static bool TryResolveItemChip(ItemData item",
        "public static bool TryResolveItemChipSpan(ItemData item",
    )
    span_block = source_block(
        text,
        "public static bool TryResolveItemChipSpan(ItemData item",
        "/// <summary>\n        /// Build a combined inline chip",
    )

    if not string_block or not span_block:
        errors.append("LocalizedInlineIconResolver ItemData chip overloads could not be parsed")
        return

    if "item.icon != null" in string_block or "item.icon != null" in span_block:
        errors.append("LocalizedInlineIconResolver ItemData chip fallback still depends on item.icon; pending icon imports would suppress text chips")
    if "markup = GenericItemChip;" not in string_block:
        errors.append("LocalizedInlineIconResolver string ItemData chip overload does not assign GenericItemChip fallback")
    if "markup = GenericItemChip.AsSpan();" not in span_block:
        errors.append("LocalizedInlineIconResolver span ItemData chip overload does not assign GenericItemChip fallback")


def validate_powershell_wrapper_splatting(errors: list[str], script_texts: dict[Path, str] | None = None) -> None:
    required_hash_splats = (
        (RUN_INVENTORY_ICON_GEMINI_SHEET_INTAKE, "$runnerArgs = @{"),
        (RUN_TOOL_INVENTORY_BATCH33_SHEET_INTAKE, "$intakeArgs = @{"),
        (RUN_TOOL_PRESENTATION_UNITY_APPLY, "$materialArgs = @{"),
        (RUN_TOOL_PRESENTATION_UNITY_APPLY, "$iconArgs = @{"),
    )
    forbidden_array_splats = (
        (RUN_INVENTORY_ICON_GEMINI_SHEET_INTAKE, "$runnerArgs = @("),
        (RUN_TOOL_INVENTORY_BATCH33_SHEET_INTAKE, "$intakeArgs = @("),
        (RUN_TOOL_PRESENTATION_UNITY_APPLY, "$materialArgs = @("),
        (RUN_TOOL_PRESENTATION_UNITY_APPLY, "$iconArgs = @("),
        (RUN_TOOL_PRESENTATION_UNITY_APPLY, "$mapIconArgs = @($iconArgs)"),
    )

    def read_script(path: Path) -> str | None:
        if script_texts is not None:
            return script_texts.get(path, "")
        if not path.exists():
            errors.append(f"tool presentation wrapper missing: {display_path(path)}")
            return None

        return path.read_text(encoding="utf-8-sig")

    for path, needle in required_hash_splats:
        text = read_script(path)
        if text is None:
            continue
        if needle not in text:
            errors.append(f"PowerShell wrapper must use hashtable splatting for nested script args: {display_path(path)} missing '{needle}'")

    for path, needle in forbidden_array_splats:
        text = read_script(path)
        if text is None:
            continue
        if needle in text:
            errors.append(f"PowerShell wrapper still uses array splatting that can shift named script parameters: {display_path(path)} contains '{needle}'")


def validate(args: argparse.Namespace) -> int:
    errors: list[str] = []
    warnings: list[str] = []
    paths = item_paths()
    prefabs_by_guid = prefab_guid_map()
    material_rule_paths = world_material_rule_paths()
    staged_paths = staged_prefab_paths()
    binding_states = icon_binding_review_states(errors, warnings)
    pending_specs = pending_icon_specs(errors)
    icon_consumer_routes = validate_icon_consumers(errors)
    validate_powershell_wrapper_splatting(errors)
    assigned_icons = 0
    approved_pending_bind = 0
    pending_generation = 0
    pending_visual_review = 0
    rejected_needs_regeneration = 0

    if not paths:
        errors.append(f"no tool ItemData assets found: {display_path(TOOL_ITEM_ROOT)}")

    for item_path in paths:
        text = item_path.read_text(encoding="utf-8-sig")
        persistent_id = yaml_scalar(text, "stableId") or item_path.stem
        guid = world_prefab_guid(text)
        prefab = prefabs_by_guid.get(guid, "")

        if not guid:
            errors.append(f"{display_path(item_path)}: worldPrefab guid missing")
        elif not prefab:
            errors.append(f"{display_path(item_path)}: worldPrefab guid does not resolve under Items/Tools: {guid}")
        else:
            if prefab not in material_rule_paths:
                errors.append(f"{display_path(item_path)}: world prefab has no generated-material assignment rule: {prefab}")
            if prefab not in staged_paths:
                errors.append(f"{display_path(item_path)}: world prefab is not included in ToolStagingSpawner: {prefab}")

        if icon_is_assigned(text):
            assigned_icons += 1
            if persistent_id in pending_specs:
                warnings.append(f"{display_path(item_path)}: icon already assigned but still present in pending icon spec")
            continue

        missing_icon_state = classify_missing_icon(persistent_id, binding_states, pending_specs)
        if missing_icon_state == MISSING_ICON_APPROVED_PENDING_UNITY:
            approved_pending_bind += 1
            continue

        if missing_icon_state == MISSING_ICON_PENDING_REVIEW:
            pending_visual_review += 1
            continue

        if missing_icon_state == MISSING_ICON_REJECTED_NEEDS_REGENERATION:
            rejected_needs_regeneration += 1
            continue

        if missing_icon_state == MISSING_ICON_PENDING_GENERATION:
            pending_generation += 1
            continue

        if missing_icon_state == MISSING_ICON_REJECTED_WITHOUT_SPEC:
            errors.append(f"{display_path(item_path)}: icon rejected with no pending Gemini regeneration spec")
        else:
            errors.append(f"{display_path(item_path)}: icon empty with no approved binding and no pending Gemini spec")

    extra_material_prefabs = sorted(material_rule_paths - set(prefabs_by_guid.values()))
    for prefab in extra_material_prefabs:
        warnings.append(f"generated-material rule targets prefab not referenced by tool ItemData: {prefab}")

    extra_staged_prefabs = sorted(staged_paths - set(prefabs_by_guid.values()))
    for prefab in extra_staged_prefabs:
        warnings.append(f"ToolStagingSpawner includes prefab not referenced by tool ItemData: {prefab}")

    status = "STATIC_READY" if assigned_icons == len(paths) and not errors else "STATIC_PLANNED_PENDING_ASSET_OR_UNITY"
    print("TOOL_PRESENTATION_COVERAGE_VALIDATOR")
    print(f"status={status}")
    print(f"toolItems={len(paths)}")
    print(f"worldPrefabs={len(prefabs_by_guid)}")
    print(f"worldMaterialRules={len(material_rule_paths)}")
    print(f"stagedPrefabs={len(staged_paths)}")
    print(f"iconsAssigned={assigned_icons}")
    print(f"approvedIconBindingsPendingUnity={approved_pending_bind}")
    print(f"pendingIconVisualReview={pending_visual_review}")
    print(f"rejectedIconBindingsNeedRegeneration={rejected_needs_regeneration}")
    print(f"pendingGeminiIconSpec={pending_generation}")
    print(f"iconConsumerRoutes={icon_consumer_routes}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    if args.fail_on_pending and status != "STATIC_READY":
        return 1
    return 1 if errors else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--fail-on-pending",
        action="store_true",
        help="treat approved/pending icon plans as failure; use only for final content lock, not active production",
    )
    return validate(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
