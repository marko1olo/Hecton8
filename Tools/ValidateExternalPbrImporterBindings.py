#!/usr/bin/env python3
"""Validate External PBR Unity material binding semantics."""

from __future__ import annotations

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CHECKS = (
    (
        "external_pbr_importer",
        ROOT / "Assets/_Project/Scripts/Editor/ExternalPbrTexturePackImporter.cs",
        "material",
    ),
    (
        "world_proxy_gemini_biome_applier",
        ROOT / "Assets/_Project/Scripts/Editor/WorldProxyGeminiBiomeMaterialApplier.cs",
        "target",
    ),
    (
        "construction_gemini_material_applier",
        ROOT / "Assets/_Project/Scripts/Editor/ConstructionGeminiMaterialApplier.cs",
        "target",
    ),
)


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def contains_call(text: str, receiver: str, property_name: str, argument: str) -> bool:
    pattern = re.compile(
        rf"\bSetTextureIfPresent\(\s*{re.escape(receiver)}\s*,\s*\"{re.escape(property_name)}\"\s*,\s*{re.escape(argument)}\s*\)"
    )
    return bool(pattern.search(text))


def contains_keyword_call(text: str, receiver: str, keyword: str, expression: str) -> bool:
    pattern = re.compile(
        rf"\bSetKeyword\(\s*{re.escape(receiver)}\s*,\s*\"{re.escape(keyword)}\"\s*,\s*{re.escape(expression)}\s*\)"
    )
    return bool(pattern.search(text))


def main() -> int:
    errors: list[str] = []

    for label, path, receiver in CHECKS:
        if not path.exists():
            errors.append(f"{label}: missing source {display_path(path)}")
            continue

        text = path.read_text(encoding="utf-8-sig")
        if not contains_call(text, receiver, "_MetallicGlossMap", "maskMap"):
            errors.append(f"{label}: _MetallicGlossMap must bind MaskMap_UnityURP/maskMap")
        if not contains_call(text, receiver, "_OcclusionMap", "maskMap"):
            errors.append(f"{label}: _OcclusionMap must bind MaskMap_UnityURP/maskMap because AO is in G")
        if contains_call(text, receiver, "_OcclusionMap", "arm"):
            errors.append(f"{label}: _OcclusionMap incorrectly binds ARM source; ARM.G is roughness, not AO")
        if not contains_keyword_call(text, receiver, "_OCCLUSIONMAP", "maskMap != null"):
            errors.append(f"{label}: _OCCLUSIONMAP keyword must be driven by maskMap")
        if contains_keyword_call(text, receiver, "_OCCLUSIONMAP", "arm != null"):
            errors.append(f"{label}: _OCCLUSIONMAP keyword incorrectly driven by ARM source")
        if label == "external_pbr_importer":
            for required in (
                "TextureImporterFormat.BC5",
                "TextureImporterFormat.BC7",
                "TextureImporterFormat.ASTC_6x6",
                "SetPlatformTextureSettings",
                "ImportRequiredTexture(asset.maps.BaseColor",
                "ImportRequiredTexture(asset.maps.NormalGL",
                "ImportRequiredTexture(asset.maps.MaskMap_UnityURP",
                "ImportRequiredTexture(asset.maps.Height",
                "ImportRequiredTexture(asset.maps.ARM_AO_Rough_Metal",
                "ImportOptionalTexture(asset.maps.Roughness",
                "RequireTexture(asset.maps.BaseColor",
                "Missing required texture map",
                "Missing texture map {mapKey}",
                "ResolveProjectFilePath",
                "Missing Gemini atlas root",
                "Missing manifest",
                "Missing texture",
                "Missing TextureImporter",
                "Invalid manifest payload",
                "No supported Lit shader found",
            ):
                if required not in text:
                    errors.append(f"{label}: missing texture import compression guard {required}")
            if "Debug.LogWarning($" in text:
                errors.append(f"{label}: importer must not downgrade missing manifest/texture/material failures to warnings")
            if "File.Exists(manifestPath)" in text or "File.ReadAllText(manifestPath)" in text or "Directory.Exists(GeminiAtlasRoot)" in text:
                errors.append(f"{label}: importer must resolve project-root paths before file IO")

    print("EXTERNAL_PBR_IMPORTER_BINDING_VALIDATOR")
    for label, path, _ in CHECKS:
        print(f"source[{label}]={display_path(path)}")
    print(f"errors={len(errors)}")
    for error in errors:
        print(f"ERROR {error}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
