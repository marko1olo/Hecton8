#!/usr/bin/env python3
"""Validate generated Gemini material handoff for ProductFace player suit sources."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
APPLIER = ROOT / "Assets/_Project/Scripts/Editor/ProductFacePlayerSuitGeminiMaterialApplier.cs"
AUTHORING = ROOT / "Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs"
APPLY_ALL = ROOT / "Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs"
MATERIAL_ROOT = ROOT / "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607"
OUTPUT_ROOT = ROOT / "Assets/_Project/Art/Generated/ProductFace/PlayerSuit/Materials"
GEMINI_SINGLE_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json"
GEMINI_BIOME_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607/GeminiBiomeMaterials_Manifest.json"
GEMINI_ATLAS_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases"


EXPECTED_SPECS = [
    {
        "slot": 0,
        "slotName": "GraphiteFabric",
        "output": "MAT_GEN_PlayerSuit_Slot0_GraphiteFabric",
        "provider": "GeminiBiome_20260607",
        "id": "gemini_biome_20260607_pressure_suit_fabric_composite",
    },
    {
        "slot": 1,
        "slotName": "WetHardShell",
        "output": "MAT_GEN_PlayerSuit_Slot1_WetHardShell",
        "provider": "Gemini_Batch20260607_MicroPanel",
        "id": "gemini_Batch20260607_MicroPanel_blue_painted_metal",
    },
    {
        "slot": 2,
        "slotName": "PatchTrim",
        "output": "MAT_GEN_PlayerSuit_Slot2_PatchTrim",
        "provider": "Gemini_Batch20260608_TextureExpansion",
        "id": "gemini_Batch20260608_TextureExpansion_b34_3422_pressure_suit_patch_trim_sheet",
    },
    {
        "slot": 3,
        "slotName": "ViewportGlass",
        "output": "MAT_GEN_PlayerSuit_Slot3_ViewportGlass",
        "provider": "Gemini_Batch20260607_MicroPanel",
        "id": "gemini_Batch20260607_MicroPanel_smoky_acrylic_glass",
    },
    {
        "slot": -1,
        "slotName": "GasketSealAux",
        "output": "MAT_GEN_PlayerSuit_Aux_GasketSeal",
        "provider": "Gemini_Batch20260608_TextureExpansion",
        "id": "gemini_Batch20260608_TextureExpansion_b34_3414_rubber_gasket_ring_trim_sheet",
    },
    {
        "slot": -1,
        "slotName": "RibbedHoseAux",
        "output": "MAT_GEN_PlayerSuit_Aux_RibbedHose",
        "provider": "Gemini_Batch20260608_TextureExpansion",
        "id": "gemini_Batch20260608_TextureExpansion_b34_3416_ribbed_flexible_hose_material",
    },
    {
        "slot": -1,
        "slotName": "SafetyLatchAux",
        "output": "MAT_GEN_PlayerSuit_Aux_SafetyLatch",
        "provider": "Gemini_Batch20260607_MicroPanel",
        "id": "gemini_Batch20260607_MicroPanel_orange_safety_composite",
    },
]


def display(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sanitize_provider_name(value: str) -> str:
    if not value.strip():
        return "Atlas"
    return "".join(c if c.isalnum() or c in "_-" else "_" for c in value)


def iter_material_assets() -> dict[tuple[str, str], dict]:
    manifests: list[tuple[str, Path]] = []
    if GEMINI_BIOME_MANIFEST.exists():
        manifests.append(("GeminiBiome_20260607", GEMINI_BIOME_MANIFEST))
    if GEMINI_SINGLE_MANIFEST.exists():
        manifests.append(("GeminiSingles_20260607", GEMINI_SINGLE_MANIFEST))
    if GEMINI_ATLAS_ROOT.exists():
        for path in sorted(GEMINI_ATLAS_ROOT.rglob("GeminiMaterialAtlas_Manifest.json")):
            provider = "Gemini_" + sanitize_provider_name(path.parent.name)
            manifests.append((provider, path))

    records: dict[tuple[str, str], dict] = {}
    for provider, path in manifests:
        payload = load_json(path)
        for asset in payload.get("assets", []) or []:
            asset_id = str(asset.get("id", "")).strip()
            if asset_id:
                records[(provider, asset_id)] = asset
    return records


def extract_new_specs(applier_text: str) -> list[tuple[int, str, str, str, str]]:
    pattern = re.compile(
        r"new PlayerSuitGeminiMaterialSpec\(\s*"
        r"(?P<slot>-?\d+),\s*"
        r'"(?P<slotName>[^"]+)",\s*'
        r'"(?P<output>[^"]+)",\s*'
        r'"(?P<provider>[^"]+)",\s*'
        r'"(?P<id>[^"]+)"',
        re.MULTILINE,
    )
    return [
        (
            int(match.group("slot")),
            match.group("slotName"),
            match.group("output"),
            match.group("provider"),
            match.group("id"),
        )
        for match in pattern.finditer(applier_text)
    ]


def validate_static(errors: list[str]) -> None:
    for path in (APPLIER, AUTHORING, APPLY_ALL):
        if not path.exists():
            errors.append(f"missing required source: {display(path)}")

    if errors:
        return

    applier_text = APPLIER.read_text(encoding="utf-8-sig")
    authoring_text = AUTHORING.read_text(encoding="utf-8-sig")
    apply_all_text = APPLY_ALL.read_text(encoding="utf-8-sig")

    specs = extract_new_specs(applier_text)
    expected_specs = [
        (spec["slot"], spec["slotName"], spec["output"], spec["provider"], spec["id"])
        for spec in EXPECTED_SPECS
    ]
    if specs != expected_specs:
        errors.append("ProductFace player suit Gemini material spec contract changed or reordered unexpectedly")

    if "ValidateSourceMaterials();" not in applier_text:
        errors.append("player suit material applier must validate all generated source materials before writing target assets")
    if "Missing generated source materials" not in applier_text:
        errors.append("player suit material applier must fail fast with missing source material count and first missing path")
    if "ApplyMaterial(spec);" not in applier_text:
        errors.append("player suit material applier must apply each spec directly after source validation")
    if "Missing generated source material for player suit slot " not in applier_text:
        errors.append("player suit material applier must throw with slot name and source path for stale source handles")
    if "Player suit palette has failures=" in applier_text:
        errors.append("player suit material applier must not downgrade apply failures to warnings")
    if "TryApplyMaterial(" in applier_text or "out string failure" in applier_text or "report.Failures" in applier_text:
        errors.append("player suit material applier must not use bool/out-string failure masking")
    if "Debug.LogWarning" in applier_text:
        errors.append("player suit material applier must not downgrade apply failures to warnings")

    if "ProductFacePlayerSuitGeminiMaterialApplier.GetRequiredMaterialPathsForStaticAudit()" not in authoring_text:
        errors.append("player suit mesh source authoring must require Gemini player-suit material palette paths")

    forbidden = "RuntimeVisualProof/MAT_RuntimeVisualProof_PlayerSuit"
    if forbidden in authoring_text:
        errors.append("player suit mesh source authoring still depends on old RuntimeVisualProof suit materials")

    importer_index = apply_all_text.find("ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks()")
    suit_index = apply_all_text.find("ProductFace.ProductFacePlayerSuitGeminiMaterialApplier.Apply(false)")
    held_index = apply_all_text.find("HeldToolExternalPbrMaterialApplier.ApplyExternalPbrToHeldTools(false)")
    if importer_index < 0 or suit_index < 0 or held_index < 0:
        errors.append("Gemini apply-all is missing importer, player-suit material applier, or held-tool applier call")
    elif not (importer_index < suit_index < held_index):
        errors.append("player-suit material applier must run after ExternalPBR import and before downstream consumer appliers")

    material_assets = iter_material_assets()
    for spec in EXPECTED_SPECS:
        key = (spec["provider"], spec["id"])
        if key not in material_assets:
            errors.append(f"missing generated material source in manifests: {spec['provider']}/{spec['id']}")
            continue
        asset = material_assets[key]
        maps = asset.get("maps", {}) or {}
        for map_key in ("BaseColor", "NormalGL", "MaskMap_UnityURP"):
            raw_path = str(maps.get(map_key, "")).strip()
            if not raw_path:
                errors.append(f"{spec['id']}: missing source map key {map_key}")
                continue
            resolved = ROOT / raw_path if not Path(raw_path).is_absolute() else Path(raw_path)
            if not resolved.exists():
                errors.append(f"{spec['id']}: source map file missing for {map_key}: {raw_path}")


def validate_post_apply(errors: list[str]) -> None:
    for spec in EXPECTED_SPECS:
        source_material = MATERIAL_ROOT / spec["provider"] / f"MAT_EXT_{spec['provider']}_{spec['id']}.mat"
        output_material = OUTPUT_ROOT / f"{spec['output']}.mat"
        if not source_material.exists():
            errors.append(f"post-apply missing generated source material: {display(source_material)}")
        if not output_material.exists():
            errors.append(f"post-apply missing player suit output material: {display(output_material)}")

    primary_slots = [spec for spec in EXPECTED_SPECS if spec["slot"] >= 0]
    if len(primary_slots) != 4 or sorted(spec["slot"] for spec in primary_slots) != [0, 1, 2, 3]:
        errors.append("post-apply primary player suit material slot contract must be exactly slots 0..3")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--post-apply", action="store_true")
    args = parser.parse_args()

    errors: list[str] = []
    validate_static(errors)
    if args.post_apply:
        validate_post_apply(errors)

    print("PRODUCT_FACE_PLAYER_SUIT_GEMINI_MATERIAL_ROUTE_VALIDATOR")
    print(f"applier={display(APPLIER)}")
    print(f"authoring={display(AUTHORING)}")
    print(f"expectedSpecs={len(EXPECTED_SPECS)}")
    print(f"errors={len(errors)}")
    for error in errors:
        print(f"ERROR {error}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
