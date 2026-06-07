#!/usr/bin/env python3
"""Regression tests for the HARDWARE_ADAPTIVE_UI_BAKER artifacts."""

from __future__ import annotations

import json
import subprocess
import sys
import unittest
from pathlib import Path

from PIL import Image


SCRIPT_PATH = Path(__file__).resolve()
ROOT = SCRIPT_PATH.parents[2]
TOOLS = ROOT / "Tools"
UX_TOOLS = TOOLS / "UX"
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(TOOLS))
sys.path.insert(0, str(UX_TOOLS))

import IconBaker  # noqa: E402
import ui_readability_test as readability  # noqa: E402
import ui_shader_sample_audit as sample_audit  # noqa: E402
from Tools.test_local_temp import project_local_tempdir_factory  # noqa: E402


SPEC_PATH = ROOT / "Docs" / "Design" / "HardwareAdaptiveUIScaler.json"
SHARPNESS_CONTROLLER = ROOT / "Assets" / "_Project" / "Scripts" / "UI" / "WorldSpaceTMPSharpnessController.cs"
TEMP_DIR = project_local_tempdir_factory("ux_hardware_adaptive_ui")


class HardwareAdaptiveUiTests(unittest.TestCase):
    def test_spec_declares_required_profiles(self) -> None:
        spec = json.loads(SPEC_PATH.read_text(encoding="utf-8-sig"))
        self.assertEqual("STATIC_PROFILE_AUTHORED_PENDING_ARTIFACT_RECHECK", spec["status"])
        self.assertEqual("PY_READABILITY_PENDING_RERUN_UNITY_PROFILER_PENDING", spec["verificationStatus"])
        self.assertEqual("HARDWARE_ADAPTIVE_UI_BAKER", spec["promptId"])
        self.assertEqual("O2 LOW", spec["sampleText"])
        self.assertEqual(5, len(spec["sdfProfiles"]))
        self.assertEqual(
            ["TOASTER_800P", "LOW_900P", "STANDARD_1080P", "HIGH_1440P", "GOD_MODE_4K"],
            [profile["id"] for profile in spec["sdfProfiles"]],
        )

    def test_csharp_runtime_keeps_sdf_materials_static(self) -> None:
        source = SHARPNESS_CONTROLLER.read_text(encoding="utf-8")
        self.assertIn("Runtime SDF sharpness must come from offline-baked atlases", source)
        self.assertIn("private void BindStaticSharedMaterial()", source)
        self.assertIn("_target.fontSharedMaterial = staticMaterial;", source)
        self.assertIn("SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI)", source)
        self.assertNotIn("new Material", source)
        self.assertNotIn(".fontMaterial", source)
        self.assertNotIn(".SetFloat(", source)

    def test_readability_report_passes_all_buckets(self) -> None:
        report = readability.build_report(SPEC_PATH)
        self.assertEqual("PASS", report["status"])
        self.assertEqual([], report["errors"])
        self.assertEqual(5, len(report["results"]))
        for result in report["results"]:
            self.assertEqual("PASS", result["status"])

    def test_shader_sample_audit_passes_budget(self) -> None:
        report = sample_audit.build_report(SPEC_PATH)
        self.assertEqual("PASS", report["status"])
        self.assertEqual([], report["errors"])
        self.assertGreaterEqual(report["shaderCount"], 10)
        for record in report["records"]:
            self.assertLessEqual(record["totalTextureSamples"], report["maxSamplesPerUiElement"])

    def test_cli_written_reports_match_current_builders(self) -> None:
        with TEMP_DIR() as temp_root:
            temp_dir = Path(temp_root)
            readability_report_path = temp_dir / "UI_Readability_UX_ENGINEER.json"
            shader_report_path = temp_dir / "UI_ShaderSampleAudit_UX_ENGINEER.json"

            readability_completed = subprocess.run(
                (
                    sys.executable,
                    str(ROOT / "Tools/UX/ui_readability_test.py"),
                    "--spec",
                    str(SPEC_PATH),
                    "--write-report",
                    str(readability_report_path),
                ),
                cwd=ROOT,
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
            )
            shader_completed = subprocess.run(
                (
                    sys.executable,
                    str(ROOT / "Tools/UX/ui_shader_sample_audit.py"),
                    "--spec",
                    str(SPEC_PATH),
                    "--write-report",
                    str(shader_report_path),
                ),
                cwd=ROOT,
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
            )

            self.assertEqual(0, readability_completed.returncode, readability_completed.stderr)
            self.assertEqual(0, shader_completed.returncode, shader_completed.stderr)
            readability_report = json.loads(readability_report_path.read_text(encoding="utf-8-sig"))
            shader_report = json.loads(shader_report_path.read_text(encoding="utf-8-sig"))

        self.assertEqual(readability.build_report(SPEC_PATH), readability_report)
        self.assertEqual(sample_audit.build_report(SPEC_PATH), shader_report)

    def test_icon_baker_outputs_three_snap_sizes(self) -> None:
        with TEMP_DIR() as temp_dir:
            source = Path(temp_dir) / "source_icon.png"
            output = Path(temp_dir) / "out"
            IconBaker.create_self_test_icon(source)
            result = IconBaker.bake_icon(source, output, (32, 128, 512), 0.08, 8)
            self.assertEqual(3, len(result.outputs))

            for size in (32, 128, 512):
                baked = output / f"source_icon_{size}.png"
                self.assertTrue(baked.exists())
                with Image.open(baked) as image:
                    self.assertEqual((size, size), image.size)
                    self.assertEqual("RGBA", image.mode)

            with Image.open(output / "source_icon_32.png") as image32:
                alpha_values = set(image32.getchannel("A").getdata())
                self.assertTrue(alpha_values.issubset({0, 255}))

if __name__ == "__main__":
    unittest.main()
