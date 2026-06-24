import sys
import unittest
from pathlib import Path
from unittest import mock


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAssetFrontFileMap as validator  # noqa: E402


class ValidateAssetFrontFileMapTests(unittest.TestCase):
    def test_display_path_fallback_to_absolute(self) -> None:
        import tempfile
        out_of_repo_path = Path(tempfile.gettempdir()) / "some_random_file.txt"
        self.assertEqual(str(out_of_repo_path), validator.display_path(out_of_repo_path))

    def test_load_text_missing_file_rejected(self) -> None:
        with self.assertRaises(SystemExit) as cm:
            validator.load_text(Path("Docs/AssetAudit/DOES_NOT_EXIST.md"))
        self.assertIn("FAIL: missing file: Docs/AssetAudit/DOES_NOT_EXIST.md", str(cm.exception))

    def test_load_rows_missing_file_rejected(self) -> None:
        with self.assertRaises(SystemExit) as cm:
            validator.load_rows(Path("Docs/AssetAudit/DOES_NOT_EXIST.csv"))
        self.assertIn("FAIL: missing file map: Docs/AssetAudit/DOES_NOT_EXIST.csv", str(cm.exception))

    def test_load_rows_missing_column_rejected(self) -> None:
        import tempfile
        with tempfile.NamedTemporaryFile(mode="w+", delete=False, suffix=".csv", newline="") as f:
            f.write("FilePath,Domain,PrimaryUse,EvidenceClass,RowsOrScope,ReadWhen\n") # Missing NotProofOf
            f.write("A,B,C,D,E,F\n")
            temp_path = Path(f.name)

        try:
            with self.assertRaises(SystemExit) as cm:
                validator.load_rows(temp_path)
            self.assertIn("FAIL: asset front file map missing column(s): NotProofOf", str(cm.exception))
        finally:
            temp_path.unlink()

    def test_load_rows_empty_value_rejected(self) -> None:
        import tempfile
        with tempfile.NamedTemporaryFile(mode="w+", delete=False, suffix=".csv", newline="") as f:
            f.write("FilePath,Domain,PrimaryUse,EvidenceClass,RowsOrScope,ReadWhen,NotProofOf\n")
            f.write("A,B,,D,E,F,G\n") # Empty PrimaryUse
            temp_path = Path(f.name)

        try:
            with self.assertRaises(SystemExit) as cm:
                validator.load_rows(temp_path)
            self.assertIn("FAIL: asset front file map empty PrimaryUse at row 2", str(cm.exception))
        finally:
            temp_path.unlink()

    def test_count_csv_rows_missing_header_rejected(self) -> None:
        import tempfile
        with tempfile.NamedTemporaryFile(mode="w+", delete=False, suffix=".csv", newline="") as f:
            temp_path = Path(f.name)

        try:
            with self.assertRaises(SystemExit) as cm:
                validator.count_csv_rows(temp_path)
            self.assertIn("FAIL: mapped CSV has no header", str(cm.exception))
        finally:
            temp_path.unlink()

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

    def test_weak_not_proof_of_boundary_rejected(self) -> None:
        rows = validator.load_rows()
        rows[0] = _row_from(rows[0], not_proof_of="weak term only")

        with self.assertRaises(SystemExit) as cm:
            validator.validate_rows(rows)
        self.assertIn("FAIL: file-map row has weak NotProofOf boundary", str(cm.exception))

    def test_missing_required_file_paths_rejected(self) -> None:
        rows = validator.load_rows()
        # Remove the first required file path from our rows
        target_path = validator.REQUIRED_FILE_PATHS[0]
        filtered_rows = [r for r in rows if r.file_path != target_path]

        # We also need to add a dummy row so the expected count check passes
        # Note: Need an existing file to bypass the missing file check and duplicate check
        # 'Tools/test_run_asset_static_validators.py' is already in REQUIRED_FILE_PATHS
        # so let's use something else that is not already in the map
        import tempfile
        with tempfile.NamedTemporaryFile(mode="w+", delete=False, dir=validator.ROOT / "Tools", suffix=".py") as f:
            dummy_file_path = f.name

        try:
            rel_dummy_path = Path(dummy_file_path).relative_to(validator.ROOT).as_posix()
            dummy_row = _row_from(rows[0], file_path=rel_dummy_path)
            filtered_rows.append(dummy_row)

            with mock.patch.object(validator, 'runner_validator_paths', return_value=set()):
                with self.assertRaises(SystemExit) as cm:
                    validator.validate_rows(filtered_rows)
                self.assertIn(f"FAIL: file map missing required path: {target_path}", str(cm.exception))
        finally:
            Path(dummy_file_path).unlink()

    def test_validate_companion_doc_missing_term_rejected(self) -> None:
        import tempfile
        with tempfile.NamedTemporaryFile(mode="w+", delete=False, suffix=".md", newline="") as f:
            f.write("Some dummy content without required terms.")
            temp_path = Path(f.name)

        try:
            with self.assertRaises(SystemExit) as cm:
                validator.validate_companion_doc(temp_path)
            self.assertIn("FAIL: asset front file-map companion doc missing term:", str(cm.exception))
        finally:
            temp_path.unlink()

    @mock.patch("sys.stdout", new_callable=__import__("io").StringIO)
    def test_main_success_output(self, mock_stdout: __import__("io").StringIO) -> None:
        validator.main()
        self.assertIn("ASSET_FRONT_FILE_MAP_OK", mock_stdout.getvalue())


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
