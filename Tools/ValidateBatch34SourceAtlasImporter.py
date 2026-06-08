#!/usr/bin/env python3
"""Validate Batch34 source-atlas Unity importer wiring and manifest coverage."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
IMPORTER_PATH = ROOT / "Assets/_Project/Scripts/Editor/Batch34SourceAtlasImporter.cs"
IMPORTER_META_PATH = IMPORTER_PATH.with_suffix(IMPORTER_PATH.suffix + ".meta")
APPLY_ALL_PATH = ROOT / "Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs"
MANIFEST_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/GeminiBatch34SourceAtlases_Manifest.json"
ALPHA_MANIFEST_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json"
PADDED_MANIFEST_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34PaddedAtlasSources_20260608/GeminiBatch34PaddedAtlasSources_Manifest.json"
SPLIT_MANIFEST_PATH = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/GeminiBatch34SplitAtlasCandidates_Manifest.json"


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def validate_post_apply_meta(source_path: Path, entry_id: str, alpha_is_transparency: bool, errors: list[str]) -> None:
    meta_path = source_path.with_suffix(source_path.suffix + ".meta")
    if not meta_path.exists():
        errors.append(f"{entry_id}: missing Unity .meta after import: {display_path(meta_path)}")
        return

    text = meta_path.read_text(encoding="utf-8-sig")
    required_tokens = (
        "TextureImporter:",
        "sRGBTexture: 1",
        "mipmapEnabled: 1",
        "textureCompression: 1",
        "wrapU: 1",
        "wrapV: 1",
        "wrapW: 1",
        "filterMode: 2",
    )
    for token in required_tokens:
        if token not in text:
            errors.append(f"{entry_id}: imported texture meta missing token {token}")
    expected_alpha = "alphaIsTransparency: 1" if alpha_is_transparency else "alphaIsTransparency: 0"
    if expected_alpha not in text:
        errors.append(f"{entry_id}: imported texture meta missing token {expected_alpha}")
    if "wrapU: 0" in text or "wrapV: 0" in text:
        errors.append(f"{entry_id}: source atlas imported with Repeat wrap; expected Clamp")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--post-apply", action="store_true", help="also require Unity-created .meta import settings")
    args = parser.parse_args()

    errors: list[str] = []
    warnings: list[str] = []

    if not IMPORTER_PATH.exists():
        errors.append(f"missing importer source: {display_path(IMPORTER_PATH)}")
    else:
        text = IMPORTER_PATH.read_text(encoding="utf-8-sig")
        required_tokens = (
            "GeminiBatch34SourceAtlases_20260608/GeminiBatch34SourceAtlases_Manifest.json",
            "GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json",
            "GeminiBatch34PaddedAtlasSources_20260608/GeminiBatch34PaddedAtlasSources_Manifest.json",
            "GeminiBatch34SplitAtlasCandidates_20260608/GeminiBatch34SplitAtlasCandidates_Manifest.json",
            "ImportAlphaCandidates(ref imported);",
            "ImportPaddedAtlases(ref imported);",
            "ImportSplitAtlasCandidates(ref imported);",
            "LoadRequiredManifest<SourceAtlasManifest>(SourceAtlasManifestPath, \"source atlas\")",
            "LoadRequiredManifest<AlphaCandidateManifest>(AlphaCandidateManifestPath, \"alpha candidate\")",
            "LoadRequiredManifest<PaddedAtlasManifest>(PaddedAtlasManifestPath, \"padded atlas\")",
            "LoadRequiredManifest<SplitAtlasManifest>(SplitAtlasManifestPath, \"split atlas candidate\")",
            "ResolveProjectFilePath(normalizedManifestPath)",
            "File.ReadAllText(projectFilePath)",
            "!IsProjectAssetPath(sourcePath)",
            "File.Exists(ResolveProjectFilePath(sourcePath))",
            "Path.GetFullPath(Path.Combine(Application.dataPath, \"..\"))",
            "throw new InvalidOperationException(\"[Batch34SourceAtlasImporter] Missing or empty source atlas manifest entries",
            "throw new InvalidOperationException(\"[Batch34SourceAtlasImporter] Missing or empty alpha candidate manifest entries",
            "throw new InvalidOperationException(\"[Batch34SourceAtlasImporter] Missing or empty padded atlas manifest entries",
            "throw new InvalidOperationException(\"[Batch34SourceAtlasImporter] Missing or empty split atlas candidate manifest entries",
            "Source atlas entry missing id at index",
            "Alpha candidate entry missing id at index",
            "Padded atlas entry missing id at index",
            "Split atlas entry missing id at index",
            "Split atlas entry missing islands at index",
            "Split atlas island count mismatch",
            "Split atlas island entry missing path",
            "Split atlas island index drift",
            "throw new InvalidOperationException($\"[Batch34SourceAtlasImporter] Missing source atlas texture for {id}: {source}\")",
            "throw new InvalidOperationException($\"[Batch34SourceAtlasImporter] Missing TextureImporter for {id}: {sourcePath}\")",
            "importer.textureType = TextureImporterType.Default",
            "importer.sRGBTexture = true",
            "importer.mipmapEnabled = true",
            "importer.wrapMode = TextureWrapMode.Clamp",
            "importer.filterMode = FilterMode.Trilinear",
            "TextureImporterCompression.CompressedHQ",
            "TextureImporterFormat.BC7",
            "TextureImporterFormat.ASTC_6x6",
            "importer.alphaSource = TextureImporterAlphaSource.FromInput",
            "importer.alphaIsTransparency = alphaIsTransparency",
            "importer.SaveAndReimport()",
        )
        for token in required_tokens:
            if token not in text:
                errors.append(f"importer missing required token: {token}")
        forbidden_tokens = (
            "new Material(",
            "AssetDatabase.CreateAsset(material",
            "TextureWrapMode.Repeat",
            "Debug.LogWarning",
            "Debug.LogWarning($\"[Batch34SourceAtlasImporter] Missing or empty source atlas manifest",
            "ref int skipped",
            "skipped++",
            "Skipped=",
            "return false;",
            "return null;",
            "File.Exists(manifestPath)",
            "File.ReadAllText(manifestPath)",
            "File.Exists(sourcePath)",
        )
        for token in forbidden_tokens:
            if token in text:
                errors.append(f"importer must not contain token: {token}")
    if not IMPORTER_META_PATH.exists():
        errors.append(f"importer Unity meta is missing: {display_path(IMPORTER_META_PATH)}")
    elif "guid:" not in IMPORTER_META_PATH.read_text(encoding="utf-8-sig"):
        errors.append(f"importer Unity meta has no guid: {display_path(IMPORTER_META_PATH)}")

    if not APPLY_ALL_PATH.exists():
        errors.append(f"missing apply-all source: {display_path(APPLY_ALL_PATH)}")
    else:
        apply_text = APPLY_ALL_PATH.read_text(encoding="utf-8-sig")
        required_apply_tokens = (
            "Batch34SourceAtlasImporter.ImportBatch34SourceAtlases();",
        )
        for token in required_apply_tokens:
            if token not in apply_text:
                errors.append(f"apply-all source atlas route missing token: {token}")
        forbidden_apply_tokens = (
            "TryInvokeStaticEditorTool(Batch34SourceAtlasImporterTypeName, Batch34SourceAtlasImporterMethodName);",
            'Batch34SourceAtlasImporterTypeName = "Hecton8.EditorTools.Batch34SourceAtlasImporter"',
            'Batch34SourceAtlasImporterMethodName = "ImportBatch34SourceAtlases"',
            "method.Invoke(null, null);",
        )
        for token in forbidden_apply_tokens:
            if token in apply_text:
                errors.append(f"apply-all source atlas route must not use reflection token: {token}")

    manifest_count = 0
    alpha_count = 0
    padded_count = 0
    split_count = 0
    split_island_count = 0
    if not MANIFEST_PATH.exists():
        errors.append(f"missing source atlas manifest: {display_path(MANIFEST_PATH)}")
    else:
        payload = json.loads(MANIFEST_PATH.read_text(encoding="utf-8-sig"))
        for entry in payload.get("entries", []) or []:
            manifest_count += 1
            entry_id = str(entry.get("id", "")).strip() or "<missing-id>"
            source = str(entry.get("source", "")).strip()
            if not source:
                errors.append(f"{entry_id}: missing source path")
                continue
            resolved_source = project_path(source)
            if not resolved_source.exists():
                errors.append(f"{entry_id}: source atlas file missing: {source}")
            elif args.post_apply:
                validate_post_apply_meta(resolved_source, entry_id, False, errors)
            if str(entry.get("productionBindingStatus", "")).strip() != "PENDING SPLIT_OR_ALPHA_EXTRACTION":
                warnings.append(f"{entry_id}: unexpected productionBindingStatus={entry.get('productionBindingStatus', '')}")

    if ALPHA_MANIFEST_PATH.exists():
        payload = json.loads(ALPHA_MANIFEST_PATH.read_text(encoding="utf-8-sig"))
        for entry in payload.get("entries", []) or []:
            alpha_count += 1
            entry_id = str(entry.get("id", "")).strip() or "<missing-id>"
            alpha_candidate = str(entry.get("alphaCandidate", "")).strip()
            if not alpha_candidate:
                errors.append(f"{entry_id}: missing alphaCandidate path")
                continue
            resolved_alpha = project_path(alpha_candidate)
            if not resolved_alpha.exists():
                errors.append(f"{entry_id}: alpha candidate file missing: {alpha_candidate}")
            elif args.post_apply:
                validate_post_apply_meta(resolved_alpha, entry_id, True, errors)

    if PADDED_MANIFEST_PATH.exists():
        payload = json.loads(PADDED_MANIFEST_PATH.read_text(encoding="utf-8-sig"))
        if payload.get("schema") != "hecton8.batch34.padded_atlas_sources.v1":
            errors.append(f"unexpected padded atlas manifest schema: {payload.get('schema')}")
        for entry in payload.get("entries", []) or []:
            padded_count += 1
            entry_id = str(entry.get("id", "")).strip() or "<missing-id>"
            padded_atlas = str(entry.get("paddedAtlas", "")).strip()
            if not padded_atlas:
                errors.append(f"{entry_id}: missing paddedAtlas path")
                continue
            resolved_padded = project_path(padded_atlas)
            if not resolved_padded.exists():
                errors.append(f"{entry_id}: padded atlas file missing: {padded_atlas}")
            elif args.post_apply:
                validate_post_apply_meta(resolved_padded, entry_id, True, errors)
    else:
        errors.append(f"missing padded atlas manifest: {display_path(PADDED_MANIFEST_PATH)}")

    if SPLIT_MANIFEST_PATH.exists():
        payload = json.loads(SPLIT_MANIFEST_PATH.read_text(encoding="utf-8-sig"))
        if payload.get("schema") != "hecton8.batch34.split_atlas_candidates.v1":
            errors.append(f"unexpected split atlas manifest schema: {payload.get('schema')}")
        if payload.get("productionBindingStatus") != "SPLIT_ISLAND_CANDIDATE_PENDING_UV_BINDING":
            errors.append(f"unexpected split atlas productionBindingStatus: {payload.get('productionBindingStatus')}")
        for entry_index, entry in enumerate(payload.get("entries", []) or []):
            split_count += 1
            entry_id = str(entry.get("id", "")).strip() or "<missing-id>"
            islands = entry.get("islands", []) or []
            if not islands:
                errors.append(f"{entry_id}: split entry has no islands")
                continue
            declared_count = int(entry.get("islandCount", 0) or 0)
            if declared_count != len(islands):
                errors.append(f"{entry_id}: split islandCount={declared_count} but manifest has {len(islands)} islands")
            for expected_index, island in enumerate(islands):
                split_island_count += 1
                island_path = str(island.get("path", "")).strip()
                if not island_path:
                    errors.append(f"{entry_id}: split island missing path at index {expected_index}")
                    continue
                raw_index = island.get("index", -1)
                try:
                    actual_index = int(raw_index)
                except (TypeError, ValueError):
                    actual_index = -1
                if actual_index != expected_index:
                    errors.append(
                        f"{entry_id}: split island index drift at entry {entry_index}: "
                        f"expected={expected_index} actual={actual_index}"
                    )
                resolved_island = project_path(island_path)
                if not resolved_island.exists():
                    errors.append(f"{entry_id}: split island file missing: {island_path}")
                elif args.post_apply:
                    validate_post_apply_meta(resolved_island, f"{entry_id}_island_{expected_index:02d}", True, errors)
    else:
        errors.append(f"missing split atlas manifest: {display_path(SPLIT_MANIFEST_PATH)}")

    print("BATCH34_SOURCE_ATLAS_IMPORTER_VALIDATOR")
    print(f"importer={display_path(IMPORTER_PATH)}")
    print(f"manifest={display_path(MANIFEST_PATH)}")
    print(f"entries={manifest_count}")
    print(f"alphaEntries={alpha_count}")
    print(f"paddedEntries={padded_count}")
    print(f"splitEntries={split_count}")
    print(f"splitIslands={split_island_count}")
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
