#!/usr/bin/env python3
"""Unit tests for UX Unity evidence gate validators."""

from __future__ import annotations

import copy
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from Tools.UX.validate_unity_verification_report import REQUIRED_CHECKS, _check_failures
from Tools.UX.validate_unity_verification_template import _failures
from Tools.UX.unity_compile_log_audit import _read_log_text, audit_log_text
from Tools.UX.update_unity_verification_report import _find_check
from Tools.test_local_temp import project_local_tempdir_factory


TEMPLATE_PATH = ROOT / "Docs/Design/HardwareAdaptiveUIScaler_UnityVerificationTemplate.json"
TEMP_DIR = project_local_tempdir_factory("ux_unity_verification_gates")


def _load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def _pending_report_fixture() -> dict:
    return {
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": "PENDING_UNITY_VERIFICATION",
        "checks": [
            {
                "id": check_id,
                "required": True,
                "status": "PENDING",
                "evidencePath": "",
            }
            for check_id in sorted(REQUIRED_CHECKS)
        ],
    }


class UnityVerificationGateTests(unittest.TestCase):
    def test_template_accepts_locked_pending_form(self) -> None:
        template = _load_json(TEMPLATE_PATH)
        self.assertEqual([], _failures(template))

    def test_template_rejects_fake_pass_and_evidence_paths(self) -> None:
        template = _load_json(TEMPLATE_PATH)
        template["status"] = "PASS"
        template["checks"][0]["status"] = "PASS"
        template["checks"][0]["evidencePath"] = "Docs/AgentLogs/fake.png"

        failures = _failures(template)

        self.assertTrue(any("PENDING_UNITY_VERIFICATION" in failure for failure in failures))
        self.assertTrue(any("template status must be PENDING" in failure for failure in failures))
        self.assertTrue(any("evidencePath must be empty" in failure for failure in failures))

    def test_pending_report_is_valid_but_not_runtime_pass(self) -> None:
        report = _pending_report_fixture()

        self.assertEqual("PENDING_UNITY_VERIFICATION", report["status"])
        self.assertEqual([], _check_failures(report))

    def test_report_rejects_top_level_pass_without_evidence(self) -> None:
        report = _pending_report_fixture()
        report["status"] = "PASS"

        failures = _check_failures(report)

        self.assertTrue(any("top-level PASS requires check status PASS" in failure for failure in failures))
        self.assertTrue(any("top-level PASS requires evidencePath" in failure for failure in failures))
        self.assertTrue(any("runtime PASS rejected" in failure for failure in failures))

    def test_report_accepts_complete_synthetic_pass(self) -> None:
        report = copy.deepcopy(_pending_report_fixture())
        report["status"] = "PASS"
        for check in report["checks"]:
            check["status"] = "PASS"
            check["evidencePath"] = "Docs/AgentLogs/evidence_placeholder.txt"

        self.assertEqual([], _check_failures(report))

    def test_unity_log_audit_accepts_clean_compile_text(self) -> None:
        audit = audit_log_text("Refresh completed in 1.2 seconds\nScripts have compiler warnings only: 0\n")

        self.assertEqual("PASS", audit["status"])
        self.assertEqual([], audit["failures"])

    def test_unity_log_audit_rejects_csharp_errors(self) -> None:
        audit = audit_log_text("Assets/_Project/Scripts/UI/Broken.cs(7,2): error CS1002: ; expected\n")

        self.assertEqual("FAIL", audit["status"])
        self.assertIn("error CS1002", audit["failures"][0])

    def test_unity_log_audit_rejects_shader_errors(self) -> None:
        audit = audit_log_text("Shader error in 'Hecton/HUD': undeclared identifier at line 44\n")

        self.assertEqual("FAIL", audit["status"])
        self.assertIn("Shader error", audit["failures"][0])

    def test_unity_log_reader_accepts_bom_encoded_logs(self) -> None:
        with TEMP_DIR() as temp_root:
            log_path = Path(temp_root) / "UnityImport.log"
            log_path.write_bytes("Shader error in 'Hecton/HUD': failed\n".encode("utf-16"))

            audit = audit_log_text(_read_log_text(log_path))

        self.assertEqual("FAIL", audit["status"])
        self.assertIn("Shader error", audit["failures"][0])

    def test_update_helper_finds_required_check(self) -> None:
        report = _pending_report_fixture()
        check = _find_check(report, "UNITY_IMPORT")

        self.assertEqual("UNITY_IMPORT", check["id"])

    def test_update_helper_rejects_unknown_check(self) -> None:
        report = _pending_report_fixture()

        with self.assertRaises(ValueError):
            _find_check(report, "NOT_A_REAL_CHECK")


if __name__ == "__main__":
    unittest.main()
