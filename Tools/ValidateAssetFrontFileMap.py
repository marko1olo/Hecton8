#!/usr/bin/env python3
"""Validate the asset-front file map."""

from __future__ import annotations

import csv
import re
from dataclasses import dataclass
from pathlib import Path

import RunAssetStaticValidators


ROOT = Path(__file__).resolve().parents[1]
MAP_PATH = ROOT / "Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.csv"
COMPANION_PATH = ROOT / "Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.md"

REQUIRED_COLUMNS = (
    "FilePath",
    "Domain",
    "PrimaryUse",
    "EvidenceClass",
    "RowsOrScope",
    "ReadWhen",
    "NotProofOf",
)
EXPECTED_ROW_COUNT = 196
CSV_ROW_PATTERN = re.compile(r"^(?P<count>\d+)_rows$")
REQUIRED_FILE_PATHS = (
    "Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.csv",
    "Tools/ValidateAssetProofArtifactIndex.py",
    "Tools/test_validate_asset_proof_artifact_index.py",
    "Tools/ValidateAudioWaveformProofArtifacts.py",
    "Tools/test_validate_audio_waveform_proof_artifacts.py",
    "Tools/ValidateAudioSceneStaticRoute.py",
    "Tools/test_validate_audio_scene_static_route.py",
    "Tools/ValidateAudioAddressablesP0Synthesis.py",
    "Tools/test_validate_audio_addressables_p0_synthesis.py",
    "Tools/ValidateAudioImportMetaPolicy.py",
    "Tools/test_validate_audio_import_meta_policy.py",
    "Tools/ValidateAudioCriticalCueSourceCoverage.py",
    "Tools/test_validate_audio_critical_cue_source_coverage.py",
    "Tools/ValidatePlayerRouteStaticEvidence.py",
    "Tools/test_validate_player_route_static_evidence.py",
    "Tools/ValidateTextureRoleTechnicalLedger.py",
    "Tools/test_validate_texture_role_technical_ledger.py",
    "Tools/ValidateVisualReferenceOwnerMatrix.py",
    "Tools/test_validate_visual_reference_owner_matrix.py",
    "Tools/ValidateVisualReferenceCurrentRejectionMatrix.py",
    "Tools/test_validate_visual_reference_current_rejection_matrix.py",
    "Tools/ValidateMassDeletionDirtySet.py",
    "Tools/test_validate_mass_deletion_dirty_set.py",
    "Tools/ValidateAssetFrontFileMap.py",
    "Tools/test_validate_asset_front_file_map.py",
    "Tools/RunAssetStaticValidators.py",
    "Tools/test_run_asset_static_validators.py",
    "Tools/BuildAegirGasGiantProofContactSheet.py",
    "Tools/ValidateAegirGasGiantSourceContract.py",
    "Tools/test_validate_aegir_gas_giant_source_contract.py",
    "Tools/ValidateBatch31LocalPbrImportIntentArtifacts.py",
    "Tools/test_validate_batch31_local_pbr_import_intent_artifacts.py",
    "Tools/ValidateBatch31PromotionPrepArtifacts.py",
    "Tools/test_validate_batch31_promotion_prep_artifacts.py",
    "Tools/ValidateMapMagicErosionSourceRoute.py",
    "Tools/test_validate_mapmagic_erosion_source_route.py",
)
NOT_PROOF_TERMS = (
    "acceptance",
    "readiness",
    "proof",
    "runtime",
    "readback",
    "visual",
    "audio",
    "import",
    "memory",
    "build",
    "behavior",
    "authority",
)
COMPANION_TERMS = (
    "Read the entry file, then only the domain row that matches the assigned work.",
    "asset front file-map validator",
    "Tools/ValidateAssetFrontFileMap.py",
    "Final status: `PENDING VERIFICATION`.",
)


@dataclass(frozen=True)
class FileMapRow:
    file_path: str
    domain: str
    primary_use: str
    evidence_class: str
    rows_or_scope: str
    read_when: str
    not_proof_of: str


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_text(path: Path) -> str:
    if not path.exists():
        raise SystemExit(f"FAIL: missing file: {display_path(path)}")
    return path.read_text(encoding="utf-8")


def load_rows(path: Path = MAP_PATH) -> list[FileMapRow]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing file map: {display_path(path)}")

    rows: list[FileMapRow] = []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = tuple(reader.fieldnames or ())
        missing = [column for column in REQUIRED_COLUMNS if column not in fieldnames]
        if missing:
            raise SystemExit(f"FAIL: asset front file map missing column(s): {', '.join(missing)}")

        for row_number, row in enumerate(reader, start=2):
            values: dict[str, str] = {}
            for column in REQUIRED_COLUMNS:
                value = (row.get(column) or "").strip()
                if not value:
                    raise SystemExit(f"FAIL: asset front file map empty {column} at row {row_number}")
                values[column] = value
            rows.append(
                FileMapRow(
                    file_path=values["FilePath"],
                    domain=values["Domain"],
                    primary_use=values["PrimaryUse"],
                    evidence_class=values["EvidenceClass"],
                    rows_or_scope=values["RowsOrScope"],
                    read_when=values["ReadWhen"],
                    not_proof_of=values["NotProofOf"],
                )
            )
    return rows


def count_csv_rows(path: Path) -> int:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None:
            raise SystemExit(f"FAIL: mapped CSV has no header: {display_path(path)}")
        return sum(1 for _ in reader)


def runner_validator_paths() -> set[str]:
    paths: set[str] = set()
    for command in RunAssetStaticValidators.VALIDATOR_COMMANDS:
        for arg in command.args:
            if arg.endswith(".py"):
                paths.add(arg.replace("\\", "/"))
    return paths


def validate_rows(rows: list[FileMapRow], root: Path = ROOT) -> None:
    if len(rows) != EXPECTED_ROW_COUNT:
        raise SystemExit(f"FAIL: expected {EXPECTED_ROW_COUNT} file-map rows, got {len(rows)}")

    seen: set[str] = set()
    by_path: dict[str, FileMapRow] = {}
    for row in rows:
        if row.file_path in seen:
            raise SystemExit(f"FAIL: duplicate file-map path: {row.file_path}")
        seen.add(row.file_path)
        by_path[row.file_path] = row

        path = root / row.file_path
        if not path.exists():
            raise SystemExit(f"FAIL: mapped file missing: {row.file_path}")

        lowered_not_proof = row.not_proof_of.lower()
        if not any(term in lowered_not_proof for term in NOT_PROOF_TERMS):
            raise SystemExit(f"FAIL: file-map row has weak NotProofOf boundary: {row.file_path}")

        match = CSV_ROW_PATTERN.match(row.rows_or_scope)
        if row.file_path.endswith(".csv") and match is not None:
            expected = int(match.group("count"))
            actual = count_csv_rows(path)
            if expected != actual:
                raise SystemExit(f"FAIL: {row.file_path} RowsOrScope expected {expected}, actual {actual}")

    for file_path in REQUIRED_FILE_PATHS:
        if file_path not in by_path:
            raise SystemExit(f"FAIL: file map missing required path: {file_path}")

    for file_path in sorted(runner_validator_paths()):
        if file_path not in by_path:
            raise SystemExit(f"FAIL: file map missing runner validator path: {file_path}")


def validate_companion_doc(path: Path = COMPANION_PATH) -> None:
    text = load_text(path)
    for term in COMPANION_TERMS:
        if term not in text:
            raise SystemExit(f"FAIL: asset front file-map companion doc missing term: {term}")


def validate_asset_front_file_map() -> list[FileMapRow]:
    rows = load_rows()
    validate_rows(rows)
    validate_companion_doc()
    return rows


def main() -> None:
    rows = validate_asset_front_file_map()
    csv_rows = sum(1 for row in rows if row.file_path.endswith(".csv"))
    print(f"ASSET_FRONT_FILE_MAP_OK rows={len(rows)} csv_rows={csv_rows}")


if __name__ == "__main__":
    main()
