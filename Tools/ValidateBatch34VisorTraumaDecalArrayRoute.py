#!/usr/bin/env python3
"""Validate Batch34 visor trauma Texture2DArray bake/bind route."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
INTEGRATOR_PATH = ROOT / "Assets/_Project/Scripts/Editor/Batch34VisorTraumaDecalArrayIntegrator.cs"
APPLY_ALL_PATH = ROOT / "Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs"
ALPHA_MANIFEST_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json"
OUTPUT_ARRAY_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/TextureArrays/TX_B34_VisorTrauma_DecalArray.asset"
SLICE_CONTRACT_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/TextureArrays/TX_B34_VisorTrauma_DecalArray_SliceContract.json"
RENDERER_PATHS = (
    ROOT / "Assets/_Project/Data/PC_Renderer.asset",
    ROOT / "Assets/_Project/Data/PC_High_Renderer.asset",
)
REQUIRED_SLICE_IDS = (
    "B34-3429",
    "B34-3432",
    "B34-3431",
    "B34-3423",
    "B34-3427",
    "B34-3428",
    "B34-3425",
    "B34-3426",
    "B34-3430",
    "B34-3433",
    "B34-3436",
    "B34-3439",
    "B34-3445",
    "B34-3446",
    "B34-3448",
    "B34-3450",
)
REQUIRED_RUNTIME_TYPES = {
    "Scorch",
    "Blood",
    "Acid",
    "HullDent",
    "GlassCrack",
    "Burn",
    "SaltCrust",
}
MAX_VISOR_TRAUMA_ARRAY_STATIC_MB = 96.0
RGBA32_BYTES_PER_PIXEL = 4
MIP_CHAIN_MULTIPLIER = 4.0 / 3.0


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--post-apply", action="store_true", help="also require Unity-created array asset and renderer binding")
    args = parser.parse_args()

    errors: list[str] = []
    warnings: list[str] = []
    estimated_array_mb = 0.0

    if not INTEGRATOR_PATH.exists():
        errors.append(f"missing integrator source: {display_path(INTEGRATOR_PATH)}")
    else:
        text = INTEGRATOR_PATH.read_text(encoding="utf-8-sig")
        required_tokens = (
            "Texture2DArray",
            "TextureFormat.RGBA32",
            "TX_B34_VisorTrauma_DecalArray.asset",
            "GeminiBatch34AlphaCandidates_Manifest.json",
            "PC_Renderer.asset",
            "PC_High_Renderer.asset",
            "TextureWrapMode.Clamp",
            "FilterMode.Trilinear",
            "SerializedObject",
            'FindPropertyRelative("decalAtlas")',
            'FindPropertyRelative("atlasSlices")',
            "AssetDatabase.LoadAllAssetsAtPath",
            "ResolveProjectFilePath(AlphaCandidateManifestPath)",
            "File.ReadAllText(manifestFilePath)",
            "Invalid Batch34 visor trauma alpha manifest entry",
            "Duplicate Batch34 visor trauma alpha source id",
            "!IsProjectAssetPath(entry.alphaCandidate)",
            "File.Exists(ResolveProjectFilePath(entry.alphaCandidate))",
            "!IsProjectAssetPath(normalized)",
            "Path.GetFullPath(Path.Combine(Application.dataPath, \"..\"))",
            "GetPixels32(0)",
            "out bool restoreReadableOnExit",
            "if (restoreReadableOnExit)",
            "RestoreReadable(entry.alphaCandidate);",
            "No alpha candidates found:",
            "Missing Batch34 visor trauma alpha source",
            "No DeferredDecalPass renderer features were bound",
            "expected {AtlasSize}x{AtlasSize}",
            "Texture2DArray bake returned null",
            "Texture2DArray save failed",
            "bool madeReadable = false;",
            "if (texture == null && madeReadable)",
            "restoreReadableOnExit = true;",
            "RestoreReadable(normalized);",
            "decalArray.Apply(true, true);",
        )
        for token in required_tokens:
            if token not in text:
                errors.append(f"integrator missing token: {token}")
        for source_id in REQUIRED_SLICE_IDS:
            if source_id not in text:
                errors.append(f"integrator missing required slice source id: {source_id}")
        if "TextureWrapMode.Repeat" in text:
            errors.append("integrator must not bake array with Repeat wrap")
        if "SetPixels32(clearPixels" in text:
            errors.append("integrator must fail missing/invalid slices instead of baking transparent fallback slices")
        if "decalArray.Apply(true, false)" in text:
            errors.append("integrator must release readable CPU copy after baking the Batch34 Texture2DArray")
        if "finally\n                {\n                    RestoreReadable(entry.alphaCandidate);" in text:
            errors.append("integrator must only restore readable import state when it changed the source importer")
        size_match = re.search(r"private\s+const\s+int\s+AtlasSize\s*=\s*(?P<size>\d+)\s*;", text)
        slice_match = re.search(r"private\s+const\s+int\s+AtlasSliceCount\s*=\s*(?P<count>\d+)\s*;", text)
        if size_match is None or slice_match is None:
            errors.append("integrator must declare AtlasSize and AtlasSliceCount constants for static VRAM budgeting")
        else:
            atlas_size = int(size_match.group("size"))
            slice_count = int(slice_match.group("count"))
            estimated_array_mb = (
                atlas_size * atlas_size * slice_count * RGBA32_BYTES_PER_PIXEL * MIP_CHAIN_MULTIPLIER
            ) / (1024.0 * 1024.0)
            if estimated_array_mb > MAX_VISOR_TRAUMA_ARRAY_STATIC_MB:
                errors.append(
                    "visor trauma Texture2DArray static VRAM estimate exceeds compact-route guard: "
                    f"{estimated_array_mb:.2f} MB > {MAX_VISOR_TRAUMA_ARRAY_STATIC_MB:.2f} MB"
                )
        forbidden_tokens = (
            "File.Exists(AlphaCandidateManifestPath)",
            "File.ReadAllText(AlphaCandidateManifestPath)",
            "if (entry == null || string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.alphaCandidate))\n                    continue;",
        )
        for token in forbidden_tokens:
            if token in text:
                errors.append(f"integrator must not contain token: {token}")

    if not APPLY_ALL_PATH.exists():
        errors.append(f"missing apply-all source: {display_path(APPLY_ALL_PATH)}")
    else:
        apply_text = APPLY_ALL_PATH.read_text(encoding="utf-8-sig")
        required_apply_tokens = (
            "Batch34VisorTraumaDecalArrayIntegrator.BakeAndBindVisorTraumaArray();",
        )
        for token in required_apply_tokens:
            if token not in apply_text:
                errors.append(f"apply-all missing visor trauma array route token: {token}")
        forbidden_apply_tokens = (
            'Batch34VisorTraumaDecalArrayIntegratorTypeName = "Hecton8.EditorTools.Batch34VisorTraumaDecalArrayIntegrator"',
            'Batch34VisorTraumaDecalArrayIntegratorMethodName = "BakeAndBindVisorTraumaArray"',
            "TryInvokeStaticEditorTool(Batch34VisorTraumaDecalArrayIntegratorTypeName, Batch34VisorTraumaDecalArrayIntegratorMethodName);",
        )
        for token in forbidden_apply_tokens:
            if token in apply_text:
                errors.append(f"apply-all visor trauma route must not use reflection token: {token}")

    promoted_ids: set[str] = set()
    if not ALPHA_MANIFEST_PATH.exists():
        errors.append(f"missing alpha candidate manifest: {display_path(ALPHA_MANIFEST_PATH)}")
    else:
        payload = json.loads(ALPHA_MANIFEST_PATH.read_text(encoding="utf-8-sig"))
        for entry in payload.get("entries", []) or []:
            promoted_ids.add(str(entry.get("id", "")).strip())
        for source_id in REQUIRED_SLICE_IDS:
            if source_id not in promoted_ids:
                errors.append(f"required slice source id is not promoted in alpha manifest: {source_id}")

    if not SLICE_CONTRACT_PATH.exists():
        errors.append(f"missing slice contract: {display_path(SLICE_CONTRACT_PATH)}")
    else:
        contract = json.loads(SLICE_CONTRACT_PATH.read_text(encoding="utf-8-sig"))
        if contract.get("schema") != "hecton8.batch34.visor_trauma_decal_array_slice_contract.v1":
            errors.append(f"slice contract has unexpected schema: {contract.get('schema')}")
        if contract.get("arrayAsset") != display_path(OUTPUT_ARRAY_PATH):
            errors.append("slice contract arrayAsset does not match expected output array path")
        if contract.get("alphaCandidateManifest") != display_path(ALPHA_MANIFEST_PATH):
            errors.append("slice contract alphaCandidateManifest does not match promoted alpha manifest path")

        slices = contract.get("slices", []) or []
        if len(slices) != len(REQUIRED_SLICE_IDS):
            errors.append(f"slice contract entry count mismatch: expected={len(REQUIRED_SLICE_IDS)} actual={len(slices)}")
        seen_indices: set[int] = set()
        seen_ids: list[str] = []
        for index, entry in enumerate(slices):
            slice_index = entry.get("slice")
            source_id = str(entry.get("sourceId", "")).strip()
            runtime_type = str(entry.get("runtimeDecalType", "")).strip()
            if slice_index != index:
                errors.append(f"slice contract index mismatch at ordinal {index}: slice={slice_index}")
            if isinstance(slice_index, int):
                if slice_index in seen_indices:
                    errors.append(f"slice contract duplicate slice index: {slice_index}")
                seen_indices.add(slice_index)
            if source_id:
                seen_ids.append(source_id)
                if source_id not in promoted_ids:
                    errors.append(f"slice contract source id is not promoted: {source_id}")
            else:
                errors.append(f"slice contract missing sourceId at ordinal {index}")
            if runtime_type not in REQUIRED_RUNTIME_TYPES:
                errors.append(f"slice contract has unknown runtimeDecalType at ordinal {index}: {runtime_type}")

        if tuple(seen_ids) != REQUIRED_SLICE_IDS:
            errors.append("slice contract sourceId order does not match required C# bake order")

    for renderer_path in RENDERER_PATHS:
        if not renderer_path.exists():
            errors.append(f"missing renderer asset: {display_path(renderer_path)}")
            continue
        renderer_text = renderer_path.read_text(encoding="utf-8-sig")
        if "Hecton8.Visor.DeferredDecalPass" not in renderer_text:
            errors.append(f"{display_path(renderer_path)}: missing DeferredDecalPass feature")
        if args.post_apply and "decalAtlas: {fileID: 0}" in renderer_text:
            errors.append(f"{display_path(renderer_path)}: decalAtlas still unbound after apply")

    if args.post_apply:
        if not OUTPUT_ARRAY_PATH.exists():
            errors.append(f"missing baked Texture2DArray asset after apply: {display_path(OUTPUT_ARRAY_PATH)}")
        if not OUTPUT_ARRAY_PATH.with_suffix(OUTPUT_ARRAY_PATH.suffix + ".meta").exists():
            errors.append(f"missing baked Texture2DArray .meta after apply: {display_path(OUTPUT_ARRAY_PATH)}.meta")

    print("BATCH34_VISOR_TRAUMA_DECAL_ARRAY_ROUTE_VALIDATOR")
    print(f"integrator={display_path(INTEGRATOR_PATH)}")
    print(f"alphaManifest={display_path(ALPHA_MANIFEST_PATH)}")
    print(f"outputArray={display_path(OUTPUT_ARRAY_PATH)}")
    print(f"sliceContract={display_path(SLICE_CONTRACT_PATH)}")
    print(f"sliceBindings={len(REQUIRED_SLICE_IDS)}")
    print(f"estimatedArrayMb={estimated_array_mb:.2f}")
    print(f"postApply={args.post_apply}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
