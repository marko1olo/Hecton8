import copy
import sys
import unittest
from pathlib import Path
from unittest import mock


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateBatch31LocalPbrImportIntentArtifacts as validator  # noqa: E402


class ValidateBatch31LocalPbrImportIntentArtifactsTests(unittest.TestCase):
    def test_current_project_batch31_import_intent_matches_static_contract(self) -> None:
        rows, empty_cells, runtime_rows, blocked_rows = validator.validate_batch31_local_pbr_import_intent_artifacts()

        self.assertEqual(21, rows)
        self.assertEqual(36, empty_cells)
        self.assertEqual(6, runtime_rows)
        self.assertEqual(3, blocked_rows)

    def test_mrao_runtime_import_rejected(self) -> None:
        _headers, rows, _empty_cells = validator.load_csv_rows()
        edited = [dict(row) for row in rows]
        for row in edited:
            if row["role_key"] == "mrao":
                row["runtime_import"] = "1"
                break

        with self.assertRaises(SystemExit):
            validator.validate_csv_rows(edited)

    def test_pass_static_warning_drift_rejected(self) -> None:
        _headers, rows, _empty_cells = validator.load_csv_rows()
        edited = [dict(row) for row in rows]
        for row in edited:
            if row["verdict"] == "PASS_STATIC":
                row["warnings"] = "unexpected_warning"
                break

        with self.assertRaises(SystemExit):
            validator.validate_csv_rows(edited)

    def test_json_no_visual_boundary_rejected(self) -> None:
        _headers, rows, _empty_cells = validator.load_csv_rows()
        data = validator.load_json()
        edited = copy.deepcopy(data)
        edited["notVisualAcceptance"] = False

        with self.assertRaises(SystemExit):
            validator.validate_json(edited, rows)

    def test_missing_maskmap_warning_in_markdown_rejected(self) -> None:
        original_load_text = validator.load_text

        def fake_load_text(path: Path) -> str:
            text = original_load_text(path)
            if path == validator.MD_PATH:
                return text.replace("Do not import Batch31 `MRAOSource` as `_MaskMap` by name alone.", "")
            return text

        with mock.patch.object(validator, "load_text", fake_load_text):
            with self.assertRaises(SystemExit):
                validator.validate_docs()


if __name__ == "__main__":
    unittest.main()
