#!/usr/bin/env python3
"""Unit tests for the mass-deletion dirty-set gate."""

from __future__ import annotations

import contextlib
import io
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import ValidateMassDeletionDirtySet as validator  # noqa: E402


class ValidateMassDeletionDirtySetTests(unittest.TestCase):
    def test_synthetic_mass_deletion_rejects_and_counts_required_categories(self) -> None:
        entries = validator.parse_status_text(
            "\n".join(
                [
                    " D Assets/_Project/Scripts/Foo.cs",
                    " D Assets/_Project/Scripts/Foo.cs.meta",
                    " D Assets/_Project/Data/Test.asset",
                    " D Assets/_Project/Data/Test.asset.meta",
                    " D Assets/_Project/Scenes/Test.unity",
                    " D Assets/_Project/Scenes/Test.unity.meta",
                    " D Tools/workspace_hygiene_1331.py",
                    " D Tools/bin/generated.py",
                    " D Docs/Reports/report.md",
                    " D Docs/Screenshots/capture.png",
                    " D Docs/AgentLogs/LOG_X.md",
                    " D Docs/Tasks/POLISH.txt",
                    " M Assets/_Project/Scripts/Bar.cs",
                    "?? Temp/untracked.txt",
                ]
            )
        )

        report = validator.analyze_entries(entries, root=Path(tempfile.gettempdir()))

        self.assertEqual("MASS_DELETION_DIRTY_SET_REJECTED", report.label)
        self.assertEqual(14, report.status_counts.total_rows)
        self.assertEqual(12, report.status_counts.tracked_deletions)
        self.assertEqual(1, report.status_counts.tracked_modifications)
        self.assertEqual(1, report.status_counts.untracked_rows)
        self.assertEqual(0, report.status_counts.staged_rows)
        self.assertEqual(6, report.deletion_counts.assets_project)
        self.assertEqual(1, report.deletion_counts.tools_source_outside_bin_obj)
        self.assertEqual(1, report.deletion_counts.docs_reports)
        self.assertEqual(1, report.deletion_counts.docs_screenshots)
        self.assertEqual(1, report.deletion_counts.docs_agentlogs)
        self.assertEqual(1, report.deletion_counts.docs_tasks)
        self.assertTrue(report.deletion_counts.polish_deleted)
        self.assertEqual(1, report.deletion_counts.deleted_cs)
        self.assertEqual(1, report.deletion_counts.deleted_asset)
        self.assertEqual(1, report.deletion_counts.deleted_unity)
        self.assertTrue(report.meta_pairing.is_clean)

        joined = "\n".join(report.blockers)
        self.assertIn("assets-project-deletions", joined)
        self.assertIn("tools-source-deletions-outside-bin-obj", joined)
        self.assertIn("Docs/Tasks/POLISH.txt", joined)
        self.assertIn("deleted-csharp-files", joined)
        self.assertIn("deleted-asset-files", joined)

    def test_missing_asset_meta_is_reported(self) -> None:
        entries = validator.parse_status_text(" D Assets/_Project/Data/MissingMeta.asset\n")

        report = validator.analyze_entries(entries, root=Path(tempfile.gettempdir()))

        self.assertFalse(report.meta_pairing.is_clean)
        self.assertEqual(("Assets/_Project/Data/MissingMeta.asset",), report.meta_pairing.missing_meta_for_deleted_payloads)
        self.assertIn("asset-meta-pairing-missing", "\n".join(report.blockers))

    def test_owner_disposition_sentinel_clears_high_risk_reject(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mass_delete_disposition_") as temp_dir:
            root = Path(temp_dir)
            disposition = root / "owner.md"
            disposition.write_text(
                "\n".join(
                    [
                        "owner: integrator",
                        "MASS_DELETION_DIRTY_SET_RESOLVED=TRUE",
                    ]
                ),
                encoding="utf-8",
            )
            entries = validator.parse_status_text(" D Docs/Tasks/POLISH.txt\n")

            report = validator.analyze_entries(entries, root=root, disposition_paths=(disposition,))

        self.assertEqual("MASS_DELETION_DIRTY_SET_RESOLVED_BY_OWNER_DISPOSITION", report.label)
        self.assertFalse(report.is_rejected)
        self.assertTrue(report.owner_disposition.resolved)

    def test_disposition_pending_does_not_clear_reject(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mass_delete_pending_") as temp_dir:
            root = Path(temp_dir)
            disposition = root / "owner.md"
            disposition.write_text(
                "MASS_DELETION_DIRTY_SET_RESOLVED=TRUE\nstatus: PENDING_VERIFICATION\n",
                encoding="utf-8",
            )
            entries = validator.parse_status_text(" D Docs/Tasks/POLISH.txt\n")

            report = validator.analyze_entries(entries, root=root, disposition_paths=(disposition,))

        self.assertEqual("MASS_DELETION_DIRTY_SET_REJECTED", report.label)
        self.assertFalse(report.owner_disposition.resolved)

    def test_nul_delimited_fixture_parses_without_live_git(self) -> None:
        entries = validator.parse_status_text(
            " D Assets/_Project/Scripts/Foo.cs\0 D Assets/_Project/Scripts/Foo.cs.meta\0?? Scratch/file.txt\0"
        )

        self.assertEqual(3, len(entries))
        self.assertEqual("Assets/_Project/Scripts/Foo.cs", entries[0].path)
        self.assertEqual("Scratch/file.txt", entries[2].path)

    def test_cli_no_fail_surfaces_reject_but_exits_zero(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mass_delete_cli_") as temp_dir:
            root = Path(temp_dir)
            status_file = root / "status.txt"
            status_file.write_text(" D Docs/Tasks/POLISH.txt\n", encoding="utf-8")
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateMassDeletionDirtySet.py"),
                    "--root",
                    str(root),
                    "--status-file",
                    str(status_file),
                    "--no-fail",
                ],
                check=False,
                capture_output=True,
                text=True,
            )

        self.assertEqual(0, result.returncode)
        self.assertIn("MASS_DELETION_DIRTY_SET_REJECTED", result.stdout)
        self.assertIn("polish-task-deleted", result.stdout)

    def test_cli_default_reject_exits_two(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_mass_delete_cli_reject_") as temp_dir:
            root = Path(temp_dir)
            status_file = root / "status.txt"
            status_file.write_text(" D Docs/Tasks/POLISH.txt\n", encoding="utf-8")

            with contextlib.redirect_stdout(io.StringIO()) as output:
                exit_code = validator.main(["--root", str(root), "--status-file", str(status_file)])

        self.assertEqual(2, exit_code)
        self.assertIn("MASS_DELETION_DIRTY_SET_REJECTED", output.getvalue())


if __name__ == "__main__":
    unittest.main()
