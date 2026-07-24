import sys
import unittest
from pathlib import Path
from unittest import mock


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAssetFrontFileMap as validator  # noqa: E402


class ValidateAssetFrontFileMapTests(unittest.TestCase):
    def test_current_project_file_map_matches_static_contract(self) -> None:
        rows = validator.validate_asset_front_file_map()

        self.assertEqual(196, len(rows))

    def test_duplicate_path_rejected(self) -> None:
        rows = validator.load_rows()
        rows[1] = _row_from(rows[1], file_path=rows[0].file_path)

        with self.assertRaises(SystemExit):
            validator.validate_rows(rows)

    def test_missing_path_rejected(self) -> None:
        rows = validator.load_rows()
        rows[0] = _row_from(rows[0], file_path="Docs/AssetAudit/DOES_NOT_EXIST.md")

        with self.assertRaises(SystemExit):
            validator.validate_rows(rows)

    def test_stale_csv_row_count_rejected(self) -> None:
        rows = validator.load_rows()
        for index, row in enumerate(rows):
            if row.file_path == "Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.csv":
                rows[index] = _row_from(row, rows_or_scope="34_rows")
                break

        with self.assertRaises(SystemExit):
            validator.validate_rows(rows)

    def test_missing_required_validator_path_rejected(self) -> None:
        rows = [row for row in validator.load_rows() if row.file_path != "Tools/ValidateAssetFrontFileMap.py"]

        with self.assertRaises(SystemExit):
            validator.validate_rows(rows)

    def test_runner_validator_path_must_be_mapped(self) -> None:
        rows = validator.load_rows()
        fake_command = validator.RunAssetStaticValidators.ValidatorCommand(
            "missing_from_file_map",
            ("Tools/ValidateMissingFromFileMap.py",),
        )

        with mock.patch.object(
            validator.RunAssetStaticValidators,
            "VALIDATOR_COMMANDS",
            (*validator.RunAssetStaticValidators.VALIDATOR_COMMANDS, fake_command),
        ):
            with self.assertRaises(SystemExit):
                validator.validate_rows(rows)


def _row_from(row: validator.FileMapRow, **overrides: str) -> validator.FileMapRow:
    values = {
        "file_path": row.file_path,
        "domain": row.domain,
        "primary_use": row.primary_use,
        "evidence_class": row.evidence_class,
        "rows_or_scope": row.rows_or_scope,
        "read_when": row.read_when,
        "not_proof_of": row.not_proof_of,
    }
    values.update(overrides)
    return validator.FileMapRow(**values)


if __name__ == "__main__":
    unittest.main()
