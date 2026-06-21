import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAssetFrontFileMap as validator  # noqa: E402


class ValidateAssetFrontFileMapTests(unittest.TestCase):
    def test_missing_file_rejected(self) -> None:
        with self.assertRaises(SystemExit) as context:
            validator.load_text(Path("/does/not/exist/foo.md"))
        self.assertIn("FAIL: missing file:", str(context.exception))

        with self.assertRaises(SystemExit) as context:
            validator.load_rows(Path("/does/not/exist/foo.csv"))
        self.assertIn("FAIL: missing file map:", str(context.exception))

    def test_missing_required_column_rejected(self) -> None:
        with tempfile.TemporaryDirectory(dir="/tmp") as tmpdir:
            path = Path(tmpdir) / "bad.csv"
            with path.open("w", encoding="utf-8", newline="") as handle:
                handle.write("FilePath,Domain\n")
                handle.write("A,B\n")

            with self.assertRaises(SystemExit) as context:
                validator.load_rows(path)
            self.assertIn("missing column(s):", str(context.exception))

    def test_empty_column_value_rejected(self) -> None:
        with tempfile.TemporaryDirectory(dir="/tmp") as tmpdir:
            path = Path(tmpdir) / "bad.csv"
            with path.open("w", encoding="utf-8", newline="") as handle:
                header = ",".join(validator.REQUIRED_COLUMNS)
                handle.write(f"{header}\n")
                # Second item empty (Domain)
                handle.write("some_path.txt,,PrimaryUse,EvidenceClass,RowsOrScope,ReadWhen,NotProofOf\n")

            with self.assertRaises(SystemExit) as context:
                validator.load_rows(path)
            self.assertIn("empty Domain at row 2", str(context.exception))

    def test_duplicate_file_path_rejected(self) -> None:
        rows = [
            _create_row(file_path="Docs/some_doc.md"),
            _create_row(file_path="Docs/some_doc.md"),
        ]
        # We also need to pad rows up to EXPECTED_ROW_COUNT so it doesn't fail on count mismatch first.
        # But wait, EXPECTED_ROW_COUNT is validated first. We can override it in the module or mock the length.
        # Let's just create enough rows. Or monkeypatch EXPECTED_ROW_COUNT.
        with tempfile.TemporaryDirectory(dir="/tmp") as tmpdir:
            root = Path(tmpdir)
            (root / "Docs/some_doc.md").parent.mkdir(parents=True, exist_ok=True)
            (root / "Docs/some_doc.md").touch()

            # Monkeypatch EXPECTED_ROW_COUNT to match our test rows
            original_count = validator.EXPECTED_ROW_COUNT
            validator.EXPECTED_ROW_COUNT = 2
            try:
                with self.assertRaises(SystemExit) as context:
                    validator.validate_rows(rows, root)
                self.assertIn("FAIL: duplicate file-map path", str(context.exception))
            finally:
                validator.EXPECTED_ROW_COUNT = original_count

    def test_mapped_file_missing_rejected(self) -> None:
        rows = [_create_row(file_path="Docs/missing_doc.md")]
        with tempfile.TemporaryDirectory(dir="/tmp") as tmpdir:
            root = Path(tmpdir)
            original_count = validator.EXPECTED_ROW_COUNT
            validator.EXPECTED_ROW_COUNT = 1
            try:
                with self.assertRaises(SystemExit) as context:
                    validator.validate_rows(rows, root)
                self.assertIn("FAIL: mapped file missing:", str(context.exception))
            finally:
                validator.EXPECTED_ROW_COUNT = original_count

    def test_weak_not_proof_boundary_rejected(self) -> None:
        rows = [_create_row(file_path="Docs/doc.md", not_proof_of="strong security")]
        with tempfile.TemporaryDirectory(dir="/tmp") as tmpdir:
            root = Path(tmpdir)
            (root / "Docs/doc.md").parent.mkdir(parents=True, exist_ok=True)
            (root / "Docs/doc.md").touch()
            original_count = validator.EXPECTED_ROW_COUNT
            validator.EXPECTED_ROW_COUNT = 1
            try:
                with self.assertRaises(SystemExit) as context:
                    validator.validate_rows(rows, root)
                self.assertIn("FAIL: file-map row has weak NotProofOf boundary", str(context.exception))
            finally:
                validator.EXPECTED_ROW_COUNT = original_count

    def test_csv_row_count_mismatch_rejected(self) -> None:
        rows = [_create_row(file_path="Docs/some_doc.csv", rows_or_scope="10_rows")]
        with tempfile.TemporaryDirectory(dir="/tmp") as tmpdir:
            root = Path(tmpdir)
            path = root / "Docs/some_doc.csv"
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open("w", encoding="utf-8", newline="") as handle:
                handle.write("Header1,Header2\nValue1,Value2\n") # 1 row

            original_count = validator.EXPECTED_ROW_COUNT
            original_required = validator.REQUIRED_FILE_PATHS
            validator.EXPECTED_ROW_COUNT = 1
            validator.REQUIRED_FILE_PATHS = ("Docs/some_doc.csv",)

            try:
                with self.assertRaises(SystemExit) as context:
                    validator.validate_rows(rows, root)
                self.assertIn("RowsOrScope expected 10, actual 1", str(context.exception))
            finally:
                validator.EXPECTED_ROW_COUNT = original_count
                validator.REQUIRED_FILE_PATHS = original_required

    def test_missing_required_path_rejected(self) -> None:
        rows = [_create_row(file_path="Docs/AssetAudit/some_file.csv")]
        with tempfile.TemporaryDirectory(dir="/tmp") as tmpdir:
            root = Path(tmpdir)
            (root / "Docs/AssetAudit/some_file.csv").parent.mkdir(parents=True, exist_ok=True)
            (root / "Docs/AssetAudit/some_file.csv").touch()

            original_count = validator.EXPECTED_ROW_COUNT
            validator.EXPECTED_ROW_COUNT = 1
            # REQUIRED_FILE_PATHS expects many files, none of which we provided in `rows`
            try:
                with self.assertRaises(SystemExit) as context:
                    validator.validate_rows(rows, root)
                self.assertIn("FAIL: file map missing required path:", str(context.exception))
            finally:
                validator.EXPECTED_ROW_COUNT = original_count

    def test_companion_doc_missing_term_rejected(self) -> None:
        with tempfile.TemporaryDirectory(dir="/tmp") as tmpdir:
            path = Path(tmpdir) / "companion.md"
            path.write_text("Missing terms here.", encoding="utf-8")
            with self.assertRaises(SystemExit) as context:
                validator.validate_companion_doc(path)
            self.assertIn("FAIL: asset front file-map companion doc missing term:", str(context.exception))

    def test_current_project_file_map_matches_static_contract(self) -> None:
        # End to end validation over the real project data.
        rows = validator.validate_asset_front_file_map()
        self.assertEqual(validator.EXPECTED_ROW_COUNT, len(rows))


def _create_row(**overrides: str) -> validator.FileMapRow:
    values = {
        "file_path": "Docs/AssetAudit/some_file.csv",
        "domain": "Asset",
        "primary_use": "Validation",
        "evidence_class": "Static",
        "rows_or_scope": "Entire file",
        "read_when": "Anytime",
        "not_proof_of": "visual",
    }
    values.update(overrides)
    return validator.FileMapRow(**values)


if __name__ == "__main__":
    unittest.main()
