#!/usr/bin/env python3
"""Validate Batch34 Gemini terrain-layer builder wiring."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BUILDER = ROOT / "Assets/_Project/Scripts/Editor/Batch34TerrainLayerAssetBuilder.cs"
APPLY_ALL = ROOT / "Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs"
UNITY_APPLY_RUNNER = ROOT / "Tools/RunGeminiMaterialUnityApplyAll.ps1"
MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/GeminiMaterialAtlas_Manifest.json"
OUTPUT_ROOT = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers"
REQUIRED_MAPS = ("BaseColor", "NormalGL", "MaskMap_UnityURP")
SPEC_PATTERN = re.compile(
    r'new\s+TerrainLayerSpec\(\s*"(?P<id>[^"]+)"\s*,\s*"(?P<output>[^"]+\.terrainlayer)"\s*,'
    r"\s*(?P<min_r>[0-9.]+)f\s*,\s*(?P<min_g>[0-9.]+)f\s*,\s*(?P<min_b>[0-9.]+)f\s*,"
    r"\s*(?P<max_r>[0-9.]+)f\s*,\s*(?P<max_g>[0-9.]+)f\s*,\s*(?P<max_b>[0-9.]+)f\s*\)",
    re.MULTILINE,
)
FLOAT_PATTERN = r"[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?"


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


def clamp(value: object, minimum: float, maximum: float) -> float:
    try:
        number = float(value)
    except (TypeError, ValueError):
        number = 0.0
    return max(minimum, min(maximum, number))


def parse_scalar(text: str, field: str) -> float | None:
    match = re.search(rf"^\s*{re.escape(field)}:\s*({FLOAT_PATTERN})\s*$", text, re.MULTILINE)
    return float(match.group(1)) if match else None


def parse_vector2(text: str, field: str) -> tuple[float, float] | None:
    match = re.search(
        rf"^\s*{re.escape(field)}:\s*\{{x:\s*({FLOAT_PATTERN}),\s*y:\s*({FLOAT_PATTERN})\}}\s*$",
        text,
        re.MULTILINE,
    )
    return (float(match.group(1)), float(match.group(2))) if match else None


def parse_vector4(text: str, field: str) -> tuple[float, float, float, float] | None:
    match = re.search(
        rf"^\s*{re.escape(field)}:\s*\{{x:\s*({FLOAT_PATTERN}),\s*y:\s*({FLOAT_PATTERN}),\s*z:\s*({FLOAT_PATTERN}),\s*w:\s*({FLOAT_PATTERN})\}}\s*$",
        text,
        re.MULTILINE,
    )
    return (
        float(match.group(1)),
        float(match.group(2)),
        float(match.group(3)),
        float(match.group(4)),
    ) if match else None


def nearly_equal(actual: float, expected: float, tolerance: float = 0.0001) -> bool:
    return abs(actual - expected) <= tolerance


def require_scalar(
    errors: list[str],
    text: str,
    output_path: str,
    field: str,
    expected: float,
) -> None:
    actual = parse_scalar(text, field)
    if actual is None:
        errors.append(f"post-apply terrain layer missing scalar {field}: {output_path}")
        return
    if not nearly_equal(actual, expected):
        errors.append(f"post-apply terrain layer {field} mismatch: {output_path} expected={expected} actual={actual}")


def require_vector2(
    errors: list[str],
    text: str,
    output_path: str,
    field: str,
    expected: tuple[float, float],
) -> None:
    actual = parse_vector2(text, field)
    if actual is None:
        errors.append(f"post-apply terrain layer missing vector2 {field}: {output_path}")
        return
    for index in range(2):
        if not nearly_equal(actual[index], expected[index]):
            errors.append(f"post-apply terrain layer {field} mismatch: {output_path} expected={expected} actual={actual}")
            return


def require_vector4(
    errors: list[str],
    text: str,
    output_path: str,
    field: str,
    expected: tuple[float, float, float, float],
) -> None:
    actual = parse_vector4(text, field)
    if actual is None:
        errors.append(f"post-apply terrain layer missing vector4 {field}: {output_path}")
        return
    for index in range(4):
        if not nearly_equal(actual[index], expected[index]):
            errors.append(f"post-apply terrain layer {field} mismatch: {output_path} expected={expected} actual={actual}")
            return


def parse_specs(builder_text: str) -> list[dict[str, str | tuple[float, float, float] | tuple[float, float, float]]]:
    specs = []
    for match in SPEC_PATTERN.finditer(builder_text):
        specs.append(
            {
                "id": match.group("id"),
                "output": match.group("output"),
                "min": (
                    float(match.group("min_r")),
                    float(match.group("min_g")),
                    float(match.group("min_b")),
                ),
                "max": (
                    float(match.group("max_r")),
                    float(match.group("max_g")),
                    float(match.group("max_b")),
                ),
            }
        )
    return specs


def validate(args: argparse.Namespace) -> int:
    errors: list[str] = []
    warnings: list[str] = []

    if not BUILDER.exists():
        errors.append(f"missing builder: {display_path(BUILDER)}")
        builder_text = ""
    else:
        builder_text = BUILDER.read_text(encoding="utf-8-sig")

    apply_all_text = APPLY_ALL.read_text(encoding="utf-8-sig") if APPLY_ALL.exists() else ""
    unity_apply_runner_text = UNITY_APPLY_RUNNER.read_text(encoding="utf-8-sig") if UNITY_APPLY_RUNNER.exists() else ""
    if "Batch34TerrainLayerAssetBuilder.BuildTerrainLayers(false)" not in apply_all_text:
        errors.append("Gemini apply-all must invoke Batch34TerrainLayerAssetBuilder.BuildTerrainLayers(false)")
    if "ValidateBatch34TerrainLayerBuilder.py" not in unity_apply_runner_text or "--post-apply" not in unity_apply_runner_text:
        errors.append("Unity apply-all runner must post-apply validate Batch34 terrain layers")

    if not MANIFEST.exists():
        errors.append(f"missing manifest: {display_path(MANIFEST)}")
        manifest_assets: dict[str, dict] = {}
    else:
        payload = json.loads(MANIFEST.read_text(encoding="utf-8-sig"))
        manifest_assets = {
            str(asset.get("id", "")).strip(): asset
            for asset in payload.get("assets", []) or []
            if asset.get("id")
        }

    specs = parse_specs(builder_text)
    if len(specs) != 7:
        errors.append(f"expected exactly 7 terrain layer specs, got {len(specs)}")

    required_tokens = (
        "TerrainLayer",
        "diffuseTexture",
        "normalMap",
        "maskMap",
        "tileSize",
        "normalScale",
        "metallic",
        "smoothness",
        "diffuseRemapMin",
        "diffuseRemapMax",
        "layer.tileOffset = Vector2.zero;",
        "layer.maskMapRemapMin = Vector4.zero;",
        "layer.maskMapRemapMax = Vector4.one;",
        "Mathf.Clamp(asset.tilingScale, 0.5f, 8f)",
        "Mathf.Clamp(asset.normalScale, 0f, 2f)",
        "Mathf.Clamp01(asset.metallic)",
        "Mathf.Clamp01(asset.smoothness)",
        "ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks",
        "AssetDatabase.CreateAsset",
        "ResolveProjectFilePath(ManifestPath)",
        "File.ReadAllText(manifestFilePath)",
        "ValidateTerrainInputs(manifest)",
        "BuildLayer(spec, asset);",
        "RequireTexture(asset.maps.BaseColor, spec.MaterialId, \"BaseColor\")",
        "RequireTexture(asset.maps.NormalGL, spec.MaterialId, \"NormalGL\")",
        "RequireTexture(asset.maps.MaskMap_UnityURP, spec.MaterialId, \"MaskMap_UnityURP\")",
        "Missing terrain-ready asset",
        "Missing BaseColor map",
        "Missing NormalGL map",
        "Missing MaskMap_UnityURP map",
        "Missing required terrain map",
        "!IsProjectAssetPath(assetPath)",
        "Path.GetFullPath(Path.Combine(Application.dataPath, \"..\"))",
        OUTPUT_ROOT,
    )
    for token in required_tokens:
        if token not in builder_text:
            errors.append(f"builder missing required token: {token}")

    forbidden_tokens = (
        "Debug.LogWarning($\"[Batch34TerrainLayerAssetBuilder] Missing manifest:",
        "Debug.LogWarning(\"[Batch34TerrainLayerAssetBuilder] Empty Batch34 manifest.",
        "Debug.LogWarning",
        "Skipped=",
        "return false;",
        "Terrain layer build failed",
        "File.Exists(ManifestPath)",
        "File.ReadAllText(ManifestPath)",
    )
    for token in forbidden_tokens:
        if token in builder_text:
            errors.append(f"builder must not contain token: {token}")

    seen_ids: set[str] = set()
    seen_outputs: set[str] = set()
    for index, spec in enumerate(specs):
        material_id = str(spec["id"])
        output = str(spec["output"])
        output_path = f"{OUTPUT_ROOT}/{output}"

        if material_id in seen_ids:
            errors.append(f"duplicate terrain material id: {material_id}")
        seen_ids.add(material_id)

        if output_path in seen_outputs:
            errors.append(f"duplicate terrain output path: {output_path}")
        seen_outputs.add(output_path)

        if not output.startswith("L_B34_") or not output.endswith(".terrainlayer"):
            errors.append(f"spec[{index}] invalid terrain layer output name: {output}")

        asset = manifest_assets.get(material_id)
        if asset is None:
            errors.append(f"spec[{index}] material id missing from manifest: {material_id}")
            continue

        if not bool(asset.get("geologyAllowed", False)):
            errors.append(f"spec[{index}] material is not geologyAllowed: {material_id}")

        if asset.get("sourceType") != "SEAMLESS_TILE":
            errors.append(f"spec[{index}] material is not SEAMLESS_TILE: {material_id}")

        if not str(asset.get("sourceFamily", "")).startswith("terrain_"):
            errors.append(f"spec[{index}] material is not a terrain family: {material_id} family={asset.get('sourceFamily')}")

        for map_key in REQUIRED_MAPS:
            map_path = str((asset.get("maps") or {}).get(map_key, "")).strip()
            if not map_path:
                errors.append(f"spec[{index}] missing map key {map_key}: {material_id}")
                continue
            if not project_path(map_path).exists():
                errors.append(f"spec[{index}] missing map file {map_key}: {material_id}: {map_path}")

        for label, values in (("min", spec["min"]), ("max", spec["max"])):
            assert isinstance(values, tuple)
            if any(value < 0.0 or value > 1.0 for value in values):
                errors.append(f"spec[{index}] remap {label} outside 0..1: {material_id} {values}")

        min_values = spec["min"]
        max_values = spec["max"]
        assert isinstance(min_values, tuple)
        assert isinstance(max_values, tuple)
        if any(min_values[i] > max_values[i] for i in range(3)):
            errors.append(f"spec[{index}] remap min greater than max: {material_id}")

        if args.post_apply:
            terrain_layer_path = project_path(output_path)
            if not terrain_layer_path.exists():
                errors.append(f"post-apply terrain layer missing: {output_path}")
            else:
                text = terrain_layer_path.read_text(encoding="utf-8-sig")
                maps = asset.get("maps") or {}
                for map_key in REQUIRED_MAPS:
                    guid = read_guid(project_path(str(maps.get(map_key, ""))))
                    if not guid:
                        errors.append(f"post-apply missing texture guid {map_key}: {material_id}")
                    elif guid not in text:
                        errors.append(f"post-apply terrain layer does not reference {map_key}: {output_path}")

                tile_size = clamp(asset.get("tilingScale"), 0.5, 8.0)
                normal_scale = clamp(asset.get("normalScale"), 0.0, 2.0)
                metallic = clamp(asset.get("metallic"), 0.0, 1.0)
                smoothness = clamp(asset.get("smoothness"), 0.0, 1.0)
                assert isinstance(min_values, tuple)
                assert isinstance(max_values, tuple)
                require_vector2(errors, text, output_path, "m_TileSize", (tile_size, tile_size))
                require_vector2(errors, text, output_path, "m_TileOffset", (0.0, 0.0))
                require_scalar(errors, text, output_path, "m_NormalScale", normal_scale)
                require_scalar(errors, text, output_path, "m_Metallic", metallic)
                require_scalar(errors, text, output_path, "m_Smoothness", smoothness)
                require_vector4(errors, text, output_path, "m_DiffuseRemapMin", (*min_values, 1.0))
                require_vector4(errors, text, output_path, "m_DiffuseRemapMax", (*max_values, 1.0))
                require_vector4(errors, text, output_path, "m_MaskMapRemapMin", (0.0, 0.0, 0.0, 0.0))
                require_vector4(errors, text, output_path, "m_MaskMapRemapMax", (1.0, 1.0, 1.0, 1.0))

    print("BATCH34_TERRAIN_LAYER_BUILDER_VALIDATOR")
    print(f"builder={display_path(BUILDER)}")
    print(f"applyAll={display_path(APPLY_ALL)}")
    print(f"unityApplyRunner={display_path(UNITY_APPLY_RUNNER)}")
    print(f"manifest={display_path(MANIFEST)}")
    print(f"outputRoot={OUTPUT_ROOT}")
    print(f"specs={len(specs)}")
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
    parser.add_argument(
        "--post-apply",
        action="store_true",
        help="also verify generated TerrainLayer assets exist and reference expected texture GUIDs",
    )
    return validate(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
