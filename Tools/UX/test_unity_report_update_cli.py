#!/usr/bin/env python3
"""CLI tests for the UX Unity verification report updater."""

from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE_REPORT = ROOT / "Docs/AgentLogs/UnityVerification_UX_ENGINEER.json"


class UnityReportUpdateCliTests(unittest.TestCase):
    def _copy_report(self, directory: Path) -> Path:
        target = directory / "UnityVerification_UX_ENGINEER.json"
        target.write_text(SOURCE_REPORT.read_text(encoding="utf-8"), encoding="utf-8")
        return target

    def test_cli_updates_pass_with_existing_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root:
            temp_dir = Path(temp_root)
            report_path = self._copy_report(temp_dir)
            evidence_path = temp_dir / "unity_import_audit.json"
            evidence_path.write_text('{"status":"PASS"}\n', encoding="utf-8")

            completed = subprocess.run(
                (
                    sys.executable,
                    "Tools/UX/update_unity_verification_report.py",
                    "--report",
                    str(report_path),
                    "--check",
                    "UNITY_IMPORT",
                    "--status",
                    "PASS",
                    "--evidence",
                    str(evidence_path),
                    "--actual",
                    "Synthetic import audit passed.",
                ),
                cwd=ROOT,
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
            )

            self.assertEqual(0, completed.returncode, completed.stderr)
            report = json.loads(report_path.read_text(encoding="utf-8"))
            unity_import = next(check for check in report["checks"] if check["id"] == "UNITY_IMPORT")
            self.assertEqual("PASS", unity_import["status"])
            self.assertEqual(str(evidence_path), unity_import["evidencePath"])
            self.assertEqual("PENDING_UNITY_VERIFICATION", report["status"])

    def test_cli_rejects_pass_without_existing_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root:
            report_path = self._copy_report(Path(temp_root))

            completed = subprocess.run(
                (
                    sys.executable,
                    "Tools/UX/update_unity_verification_report.py",
                    "--report",
                    str(report_path),
                    "--check",
                    "UNITY_IMPORT",
                    "--status",
                    "PASS",
                    "--evidence",
                    str(Path(temp_root) / "missing.json"),
                ),
                cwd=ROOT,
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
            )

            self.assertNotEqual(0, completed.returncode)
            self.assertIn("PASS evidence path does not exist", completed.stderr + completed.stdout)

    def test_cli_rejects_top_level_pass_until_all_checks_complete(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root:
            temp_dir = Path(temp_root)
            report_path = self._copy_report(temp_dir)
            evidence_path = temp_dir / "unity_import_audit.json"
            evidence_path.write_text('{"status":"PASS"}\n', encoding="utf-8")

            completed = subprocess.run(
                (
                    sys.executable,
                    "Tools/UX/update_unity_verification_report.py",
                    "--report",
                    str(report_path),
                    "--check",
                    "UNITY_IMPORT",
                    "--status",
                    "PASS",
                    "--evidence",
                    str(evidence_path),
                    "--top-status",
                    "PASS",
                ),
                cwd=ROOT,
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
            )

            self.assertNotEqual(0, completed.returncode)
            self.assertIn("top-level PASS rejected", completed.stderr + completed.stdout)

    def test_cli_resolves_relative_evidence_from_repo_root_when_cwd_differs(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root:
            temp_dir = Path(temp_root)
            report_path = self._copy_report(temp_dir)
            evidence_relative = "Docs/AgentLogs/UI_HardwareAdaptiveValidation_UX_ENGINEER.json"
            self.assertTrue((ROOT / evidence_relative).exists())

            completed = subprocess.run(
                (
                    sys.executable,
                    str(ROOT / "Tools/UX/update_unity_verification_report.py"),
                    "--report",
                    str(report_path),
                    "--check",
                    "UNITY_IMPORT",
                    "--status",
                    "PASS",
                    "--evidence",
                    evidence_relative,
                    "--actual",
                    "Synthetic relative evidence path passed.",
                ),
                cwd=temp_dir,
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
            )

            self.assertEqual(0, completed.returncode, completed.stderr)
            report = json.loads(report_path.read_text(encoding="utf-8"))
            unity_import = next(check for check in report["checks"] if check["id"] == "UNITY_IMPORT")
            self.assertEqual(evidence_relative, unity_import["evidencePath"])


if __name__ == "__main__":
    unittest.main()
