import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAssetProofArtifactIndex as validator  # noqa: E402


class ValidateAssetProofArtifactIndexTests(unittest.TestCase):
    def test_current_project_index_matches_static_contract(self) -> None:
        rows = validator.validate_asset_proof_artifact_index()

        self.assertEqual(35, len(rows))
        self.assertEqual(
            4,
            sum(1 for row in rows if row.disposition == "DIAGNOSTIC_REJECTED"),
        )

    def test_missing_required_artifact_rejected(self) -> None:
        rows = [
            row
            for row in validator.load_rows()
            if row.artifact_path != "Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.png"
        ]

        with self.assertRaises(SystemExit):
            validator.validate_required_artifacts(rows)

    def test_h8_1914_must_remain_diagnostic_rejected(self) -> None:
        rows = validator.load_rows()
        row = next(row for row in rows if row.artifact_path.endswith("h8_1914_surface_crest_recovery_probe.png"))
        edited = _row_from(row, disposition="PENDING_VERIFICATION")
        replaced = [edited if item is row else item for item in rows]

        with self.assertRaises(SystemExit):
            validator.validate_required_artifacts(replaced)

    def test_missing_artifact_path_rejected(self) -> None:
        rows = [_row(artifact_path="Docs/AssetAudit/DOES_NOT_EXIST.png")]

        with self.assertRaises(SystemExit):
            validator.validate_artifact_paths(rows)

    def test_batch31_blocker_terms_required(self) -> None:
        with self.assertRaises(SystemExit):
            validator.validate_batch31_index_text("Unity Imported**: `false`")


def _row_from(row: validator.ProofArtifactRow, **overrides: str) -> validator.ProofArtifactRow:
    values = {
        "artifact_type": row.artifact_type,
        "artifact_path": row.artifact_path,
        "related_asset_family": row.related_asset_family,
        "evidence_class": row.evidence_class,
        "disposition": row.disposition,
        "use_for": row.use_for,
        "reject_as": row.reject_as,
        "next_owner": row.next_owner,
    }
    values.update(overrides)
    return validator.ProofArtifactRow(**values)


def _row(**overrides: str) -> validator.ProofArtifactRow:
    values = {
        "artifact_type": "contact_sheet",
        "artifact_path": "Docs/AssetAudit/ContactSheets/water_foam_caustic_contact_sheet.png",
        "related_asset_family": "water_foam_caustic",
        "evidence_class": "STATIC_IMAGE_QA",
        "disposition": "PENDING_VERIFICATION",
        "use_for": "Static image triage",
        "reject_as": "In-game visual acceptance",
        "next_owner": "Unity material readback owner",
    }
    values.update(overrides)
    return validator.ProofArtifactRow(**values)


if __name__ == "__main__":
    unittest.main()
