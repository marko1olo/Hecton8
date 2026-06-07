import csv
import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
ROOT = TOOLS_ROOT.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAssetStaticSummary as validator  # noqa: E402
from Tools.test_local_temp import project_local_tempdir_factory  # noqa: E402


TEMP_DIR = project_local_tempdir_factory("asset_static_summary")


class ValidateAssetStaticSummaryTests(unittest.TestCase):
    def test_parse_current_rows_ignores_excluded_sidecar_table(self) -> None:
        summary = "\n".join(
            (
                "## Current Static Parse Set",
                "",
                "| File | Rows | Columns | Empty cells |",
                "|---|---:|---:|---:|",
                "| `Docs/AssetAudit/current.csv` | 2 | 3 | 0 |",
                "",
                "Total current rows: `2`.",
                "",
                "## Excluded Older/Sidecar CSV Boundary",
                "| File | Rows | Empty cells | Boundary |",
                "| `Docs/AssetAudit/sidecar.csv` | 99 | 9 | Sparse. |",
            )
        )

        rows = validator.parse_current_rows(summary)

        self.assertEqual(1, len(rows))
        self.assertEqual("Docs/AssetAudit/current.csv", rows[0].file_path)

    def test_validate_summary_accepts_matching_csv_stats(self) -> None:
        with TEMP_DIR() as temp_dir:
            root = Path(temp_dir)
            csv_path = root / "Docs/AssetAudit/current.csv"
            summary_path = root / "summary.md"
            self._write_csv(csv_path, ["a", "b"], [["1", "2"], ["3", "4"]])
            summary_path.write_text(
                "\n".join(
                    (
                        "## Current Static Parse Set",
                        "| File | Rows | Columns | Empty cells |",
                        "|---|---:|---:|---:|",
                        "| `Docs/AssetAudit/current.csv` | 2 | 2 | 0 |",
                        "Total current rows: `2`.",
                        "## Excluded Older/Sidecar CSV Boundary",
                    )
                ),
                encoding="utf-8",
            )

            rows = validator.validate_summary(summary_path=summary_path, root=root)

        self.assertEqual(1, len(rows))

    def test_validate_summary_rejects_stale_row_count(self) -> None:
        with TEMP_DIR() as temp_dir:
            root = Path(temp_dir)
            csv_path = root / "Docs/AssetAudit/current.csv"
            summary_path = root / "summary.md"
            self._write_csv(csv_path, ["a"], [["1"], ["2"]])
            summary_path.write_text(
                "\n".join(
                    (
                        "## Current Static Parse Set",
                        "| File | Rows | Columns | Empty cells |",
                        "|---|---:|---:|---:|",
                        "| `Docs/AssetAudit/current.csv` | 1 | 1 | 0 |",
                        "Total current rows: `1`.",
                        "## Excluded Older/Sidecar CSV Boundary",
                    )
                ),
                encoding="utf-8",
            )

            with self.assertRaises(SystemExit):
                validator.validate_summary(summary_path=summary_path, root=root)

    def test_current_project_summary_matches_current_csvs(self) -> None:
        rows = validator.validate_summary()

        self.assertEqual(62, len(rows))
        self.assertEqual(14701, sum(row.rows for row in rows))

    @staticmethod
    def _write_csv(path: Path, headers: list[str], rows: list[list[str]]) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open("w", encoding="utf-8", newline="") as handle:
            writer = csv.writer(handle)
            writer.writerow(headers)
            writer.writerows(rows)


if __name__ == "__main__":
    unittest.main()
