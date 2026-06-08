#!/usr/bin/env python3
"""Validate Batch34 texture expansion intake manifest and source-file coverage."""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/QA/Batch34_TextureExpansion_IntakeManifest.json"
EXPECTED_IDS = {f"B34-{index}" for index in range(3401, 3451)}
PBR_SOURCE_TYPES = {"SEAMLESS_TILE", "TRIM_SHEET"}
REQUIRED_MAPS = ("NormalGL", "Height", "MRAO_Provisional_RGBA_Metal_Rough_AO_Emission")


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str | Path) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def normalized_path_key(path: Path) -> str:
    return str(path.resolve()).casefold()


def require_existing_project_path(raw: object, label: str, entry_id: str, errors: list[str]) -> None:
    value = str(raw or "").strip()
    if not value:
        errors.append(f"{entry_id}: missing {label}")
        return
    resolved = project_path(value)
    if not resolved.exists():
        errors.append(f"{entry_id}: {label} file missing: {display_path(resolved)}")


def validate() -> int:
    errors: list[str] = []
    warnings: list[str] = []

    if not MANIFEST_PATH.exists():
        print("BATCH34_TEXTURE_EXPANSION_INTAKE_MANIFEST_VALIDATOR")
        print(f"manifest={display_path(MANIFEST_PATH)}")
        print("errors=1")
        print("warnings=0")
        print("ERROR missing intake manifest")
        return 1

    payload = json.loads(MANIFEST_PATH.read_text(encoding="utf-8-sig"))
    entries = list(payload.get("entries", []) or [])
    source_audit = payload.get("sourceAudit", {}) or {}

    if payload.get("schema") != "hecton8.batch34.texture_expansion_intake.v1":
        errors.append(f"unexpected schema: {payload.get('schema')}")

    seen_ids: set[str] = set()
    selected_sources: list[str] = []
    for index, entry in enumerate(entries):
        entry_id = str(entry.get("id", "")).strip()
        if not entry_id:
            errors.append(f"entry[{index}] missing id")
            continue
        if entry_id in seen_ids:
            errors.append(f"entry[{index}] duplicate id: {entry_id}")
        seen_ids.add(entry_id)

        if str(entry.get("verdict", "")).startswith("REJECT"):
            errors.append(f"{entry_id}: rejected source in intake manifest: {entry.get('verdict')}")

        source_glob = str(entry.get("sourceGlob", "")).strip()
        if not source_glob:
            errors.append(f"{entry_id}: missing sourceGlob")

        matched_sources = list(entry.get("matchedDownloadSources", []) or [])
        if not matched_sources:
            errors.append(f"{entry_id}: matchedDownloadSources is empty")

        selected = str(entry.get("downloadSource", "")).strip()
        if not selected:
            errors.append(f"{entry_id}: missing selected downloadSource")
        else:
            selected_sources.append(selected)
            if matched_sources and selected != str(matched_sources[-1]):
                errors.append(f"{entry_id}: selected source is not the latest matched source")

        require_existing_project_path(entry.get("originalPath"), "originalPath", entry_id, errors)
        require_existing_project_path(entry.get("cleanedPath"), "cleanedPath", entry_id, errors)
        require_existing_project_path(entry.get("baseColorCandidatePath"), "baseColorCandidatePath", entry_id, errors)

        if entry.get("sourceType") in PBR_SOURCE_TYPES:
            maps = entry.get("maps", {}) or {}
            for map_key in REQUIRED_MAPS:
                require_existing_project_path(maps.get(map_key), f"maps.{map_key}", entry_id, errors)

    missing = sorted(EXPECTED_IDS - seen_ids)
    extra = sorted(seen_ids - EXPECTED_IDS)
    if missing:
        errors.append(f"missing Batch34 ids: {', '.join(missing)}")
    if extra:
        errors.append(f"unexpected Batch34 ids: {', '.join(extra)}")
    if len(entries) != 50:
        errors.append(f"expected 50 entries, found {len(entries)}")

    selected_keys = [normalized_path_key(Path(raw)) for raw in selected_sources]
    if len(selected_keys) != len(set(selected_keys)):
        errors.append("duplicate selected downloadSource values")

    if not isinstance(source_audit, dict) or not source_audit:
        errors.append("missing sourceAudit block")
    else:
        expected_count = int(source_audit.get("expectedJobCount", -1))
        selected_count = int(source_audit.get("selectedDownloadSourceCount", -1))
        unique_selected_count = int(source_audit.get("uniqueSelectedDownloadSourceCount", -1))
        missing_jobs = list(source_audit.get("missingJobIds", []) or [])
        duplicate_sources = list(source_audit.get("duplicateSelectedDownloadSources", []) or [])
        unmatched = list(source_audit.get("unmatchedDownloadCandidates", []) or [])
        ignored_candidates = list(source_audit.get("ignoredDownloadCandidates", []) or [])

        if expected_count != 50:
            errors.append(f"sourceAudit expectedJobCount must be 50, found {expected_count}")
        if selected_count != 50:
            errors.append(f"sourceAudit selectedDownloadSourceCount must be 50, found {selected_count}")
        if unique_selected_count != 50:
            errors.append(f"sourceAudit uniqueSelectedDownloadSourceCount must be 50, found {unique_selected_count}")
        if missing_jobs:
            errors.append(f"sourceAudit missingJobIds is not empty: {', '.join(map(str, missing_jobs))}")
        if duplicate_sources:
            errors.append(f"sourceAudit duplicateSelectedDownloadSources is not empty: {duplicate_sources}")
        if unmatched:
            warnings.append(f"unmatched download candidates recorded={len(unmatched)}")
        for index, candidate in enumerate(ignored_candidates):
            if not isinstance(candidate, dict):
                errors.append(f"sourceAudit ignoredDownloadCandidates[{index}] must be an object")
                continue
            reason = str(candidate.get("reason", "")).strip()
            path = str(candidate.get("path", "")).strip()
            selected_path = str(candidate.get("selectedPath", "")).strip()
            sha256 = str(candidate.get("sha256", "")).strip()
            if reason != "byte_identical_duplicate_selected_source":
                errors.append(f"sourceAudit ignoredDownloadCandidates[{index}] has unsupported reason: {reason}")
            if not path:
                errors.append(f"sourceAudit ignoredDownloadCandidates[{index}] missing path")
            if not selected_path:
                errors.append(f"sourceAudit ignoredDownloadCandidates[{index}] missing selectedPath")
            if len(sha256) != 64:
                errors.append(f"sourceAudit ignoredDownloadCandidates[{index}] missing sha256")

    print("BATCH34_TEXTURE_EXPANSION_INTAKE_MANIFEST_VALIDATOR")
    print(f"manifest={display_path(MANIFEST_PATH)}")
    print(f"entries={len(entries)}")
    print(f"selectedDownloadSources={len(selected_sources)}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(validate())
