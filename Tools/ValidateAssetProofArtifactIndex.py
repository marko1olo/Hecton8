#!/usr/bin/env python3
"""Validate proof-adjacent asset artifact indexing and visual reference continuity."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
INDEX_PATH = ROOT / "Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.csv"
COMPANION_PATH = ROOT / "Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.md"
REFERENCE_PATHS_PATH = ROOT / "Docs/AssetAudit/VISUAL_REFERENCE_PATH_CONTINUITY_20260605.csv"
BATCH31_INDEX_PATH = (
    ROOT / "Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/Batch31_PromotionPrep_INDEX.md"
)

REQUIRED_COLUMNS = (
    "ArtifactType",
    "ArtifactPath",
    "RelatedAssetFamily",
    "EvidenceClass",
    "Disposition",
    "UseFor",
    "RejectAs",
    "NextOwner",
)
EXPECTED_ROW_COUNT = 35
EXPECTED_REFERENCE_COUNT = 15
REQUIRED_ARTIFACTS = {
    "Docs/AssetAudit/ContactSheets/mandatory_visual_references_current_20260605.png": (
        "reference_contact_sheet",
        "mandatory_visual_references",
        "STATIC_IMAGE_QA",
        "REFERENCE_REQUIRED",
    ),
    "Docs/AssetAudit/VISUAL_REFERENCE_PATH_CONTINUITY_20260605.csv": (
        "reference_manifest",
        "mandatory_visual_references",
        "STATIC_DOC",
        "PENDING_VISUAL_REVIEW",
    ),
    "Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.png": (
        "diagnostic_screenshot",
        "surface_crest_1914",
        "STATIC_SCREENSHOT_REVIEW",
        "DIAGNOSTIC_REJECTED",
    ),
    "Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/Batch31_PromotionPrep_contact_sheet.png": (
        "generated_pbr_contact_sheet",
        "batch31_local_pbr_promotion_prep",
        "STATIC_IMAGE_PREP_ONLY",
        "SOURCE_ONLY_USEFUL",
    ),
    "Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/Batch31_PromotionPrep_INDEX.md": (
        "generated_manifest",
        "batch31_local_pbr_promotion_prep",
        "STATIC_DOC",
        "BLOCKED_CHANNEL_SEMANTICS",
    ),
}
COMPANION_TERMS = (
    "This index is a map of existing proof-adjacent artifacts. It is not product acceptance.",
    "mandatory_visual_references_current_20260605.png",
    "h8_1914_surface_crest_recovery_probe.png",
    "Batch31_PromotionPrep_contact_sheet.png",
    "Final status: `PENDING VERIFICATION`.",
)
BATCH31_BLOCKER_TERMS = (
    "Unity Imported**: `false`",
    "Visual Acceptance**: `false`",
    "Semantic Status**: `BLOCKED_CHANNEL_SEMANTICS`",
    "Promotion Ready**: `false`",
)


@dataclass(frozen=True)
class ProofArtifactRow:
    artifact_type: str
    artifact_path: str
    related_asset_family: str
    evidence_class: str
    disposition: str
    use_for: str
    reject_as: str
    next_owner: str


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_text(path: Path) -> str:
    if not path.exists():
        raise SystemExit(f"FAIL: missing file: {display_path(path)}")
    return path.read_text(encoding="utf-8")


def load_rows(path: Path = INDEX_PATH) -> list[ProofArtifactRow]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing proof artifact index: {display_path(path)}")

    rows: list[ProofArtifactRow] = []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = tuple(reader.fieldnames or ())
        missing = [column for column in REQUIRED_COLUMNS if column not in fieldnames]
        if missing:
            raise SystemExit(f"FAIL: proof artifact index missing column(s): {', '.join(missing)}")

        for row_number, row in enumerate(reader, start=2):
            values: dict[str, str] = {}
            for column in REQUIRED_COLUMNS:
                value = (row.get(column) or "").strip()
                if not value:
                    raise SystemExit(f"FAIL: proof artifact index empty {column} at row {row_number}")
                values[column] = value
            rows.append(
                ProofArtifactRow(
                    artifact_type=values["ArtifactType"],
                    artifact_path=values["ArtifactPath"],
                    related_asset_family=values["RelatedAssetFamily"],
                    evidence_class=values["EvidenceClass"],
                    disposition=values["Disposition"],
                    use_for=values["UseFor"],
                    reject_as=values["RejectAs"],
                    next_owner=values["NextOwner"],
                )
            )
    return rows


def validate_artifact_paths(rows: list[ProofArtifactRow], root: Path = ROOT) -> None:
    for row in rows:
        if not row.artifact_path.startswith("Docs/"):
            continue

        path = root / row.artifact_path
        if not path.exists():
            raise SystemExit(f"FAIL: proof artifact index missing artifact path: {row.artifact_path}")


def validate_required_artifacts(rows: list[ProofArtifactRow]) -> None:
    by_path = {row.artifact_path: row for row in rows}
    for artifact_path, expected in REQUIRED_ARTIFACTS.items():
        row = by_path.get(artifact_path)
        if row is None:
            raise SystemExit(f"FAIL: proof artifact index missing required artifact: {artifact_path}")

        artifact_type, family, evidence_class, disposition = expected
        actual = (row.artifact_type, row.related_asset_family, row.evidence_class, row.disposition)
        if actual != expected:
            raise SystemExit(f"FAIL: proof artifact index bad row contract for {artifact_path}: {actual}")

    for row in rows:
        if "h8_1914" in row.artifact_path and row.disposition != "DIAGNOSTIC_REJECTED":
            raise SystemExit(f"FAIL: h8_1914 artifact must remain DIAGNOSTIC_REJECTED: {row.artifact_path}")
        if row.disposition in {"SOURCE_ONLY", "SOURCE_ONLY_USEFUL", "BLOCKED_CHANNEL_SEMANTICS"}:
            if "acceptance" not in row.reject_as.lower() and "import" not in row.reject_as.lower():
                raise SystemExit(f"FAIL: source-only artifact must reject promotion boundary: {row.artifact_path}")


def validate_reference_continuity(path: Path = REFERENCE_PATHS_PATH, root: Path = ROOT) -> None:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        rows = list(reader)

    if len(rows) != EXPECTED_REFERENCE_COUNT:
        raise SystemExit(f"FAIL: expected {EXPECTED_REFERENCE_COUNT} mandatory references, got {len(rows)}")

    for row in rows:
        reference_id = (row.get("ReferenceId") or "").strip()
        current_path = (row.get("CurrentPath") or "").strip()
        status = (row.get("Status") or "").strip()
        size_text = (row.get("SizeBytes") or "").strip()
        if not reference_id or not current_path or not status or not size_text:
            raise SystemExit("FAIL: visual reference continuity row has empty required field")
        if status != "PENDING_VISUAL_REVIEW":
            raise SystemExit(f"FAIL: {reference_id} status must remain PENDING_VISUAL_REVIEW")

        artifact_path = root / current_path
        if not artifact_path.exists():
            raise SystemExit(f"FAIL: {reference_id} missing mandatory reference path: {current_path}")
        expected_size = int(size_text)
        actual_size = artifact_path.stat().st_size
        if actual_size != expected_size:
            raise SystemExit(f"FAIL: {reference_id} size expected {expected_size}, actual {actual_size}")


def validate_batch31_index_text(text: str) -> None:
    for term in BATCH31_BLOCKER_TERMS:
        if term not in text:
            raise SystemExit(f"FAIL: Batch31 promotion-prep index missing blocker term: {term}")


def validate_companion_doc(path: Path = COMPANION_PATH) -> None:
    text = load_text(path)
    for term in COMPANION_TERMS:
        if term not in text:
            raise SystemExit(f"FAIL: proof artifact companion doc missing term: {term}")


def validate_rows(rows: list[ProofArtifactRow]) -> None:
    if len(rows) != EXPECTED_ROW_COUNT:
        raise SystemExit(f"FAIL: expected {EXPECTED_ROW_COUNT} proof artifact rows, got {len(rows)}")
    validate_artifact_paths(rows)
    validate_required_artifacts(rows)


def validate_asset_proof_artifact_index() -> list[ProofArtifactRow]:
    rows = load_rows()
    validate_rows(rows)
    validate_reference_continuity()
    validate_batch31_index_text(load_text(BATCH31_INDEX_PATH))
    validate_companion_doc()
    return rows


def main() -> None:
    rows = validate_asset_proof_artifact_index()
    diagnostic_rejected = sum(1 for row in rows if row.disposition == "DIAGNOSTIC_REJECTED")
    print(
        "ASSET_PROOF_ARTIFACT_INDEX_OK "
        f"rows={len(rows)} mandatory_refs={EXPECTED_REFERENCE_COUNT} diagnostic_rejected={diagnostic_rejected}"
    )


if __name__ == "__main__":
    main()
