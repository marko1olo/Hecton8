#!/usr/bin/env python3
"""Regression tests for the HARDWARE_ADAPTIVE_UI_BAKER artifacts."""

from __future__ import annotations

import json
import re
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image


SCRIPT_PATH = Path(__file__).resolve()
ROOT = SCRIPT_PATH.parents[2]
TOOLS = ROOT / "Tools"
UX_TOOLS = TOOLS / "UX"
sys.path.insert(0, str(TOOLS))
sys.path.insert(0, str(UX_TOOLS))

import IconBaker  # noqa: E402
import ui_readability_test as readability  # noqa: E402
import ui_shader_sample_audit as sample_audit  # noqa: E402


SPEC_PATH = ROOT / "Docs" / "Design" / "HardwareAdaptiveUIScaler.json"
SHARPNESS_CONTROLLER = ROOT / "Assets" / "_Project" / "Scripts" / "UI" / "WorldSpaceTMPSharpnessController.cs"


class HardwareAdaptiveUiTests(unittest.TestCase):
    def test_spec_declares_required_profiles(self) -> None:
        spec = json.loads(SPEC_PATH.read_text(encoding="utf-8"))
        self.assertEqual("UI SCALED", spec["status"])
        self.assertEqual("HARDWARE_ADAPTIVE_UI_BAKER", spec["promptId"])
        self.assertEqual("O2 LOW", spec["sampleText"])
        self.assertEqual(5, len(spec["sdfProfiles"]))
        self.assertEqual(
            ["TOASTER_800P", "LOW_900P", "STANDARD_1080P", "HIGH_1440P", "GOD_MODE_4K"],
            [profile["id"] for profile in spec["sdfProfiles"]],
        )

    def test_csharp_sdf_matrix_matches_json(self) -> None:
        spec = json.loads(SPEC_PATH.read_text(encoding="utf-8"))
        source = SHARPNESS_CONTROLLER.read_text(encoding="utf-8")
        resolved = extract_csharp_sdf_profiles(source)
        self.assertEqual(5, len(resolved))

        for profile in spec["sdfProfiles"]:
            bucket = int(profile["shortSideMax"])
            self.assertIn(bucket, resolved)
            csharp = resolved[bucket]
            self.assertAlmostEqual(float(profile["tmpWeightNormal"]), csharp["weightNormal"], places=4)
            self.assertAlmostEqual(float(profile["tmpWeightBold"]), csharp["weightBold"], places=4)
            self.assertAlmostEqual(float(profile["faceDilateOffset"]), csharp["dilateOffset"], places=4)
            self.assertAlmostEqual(float(profile["outlineSoftnessOffset"]), csharp["softnessOffset"], places=4)

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

    def test_written_reports_are_current(self) -> None:
        readability_report_path = ROOT / "Docs" / "AgentLogs" / "UI_Readability_UX_ENGINEER.json"
        shader_report_path = ROOT / "Docs" / "AgentLogs" / "UI_ShaderSampleAudit_UX_ENGINEER.json"
        self.assertTrue(readability_report_path.exists())
        self.assertTrue(shader_report_path.exists())

        readability_report = json.loads(readability_report_path.read_text(encoding="utf-8"))
        shader_report = json.loads(shader_report_path.read_text(encoding="utf-8"))
        self.assertEqual(readability.build_report(SPEC_PATH), readability_report)
        self.assertEqual(sample_audit.build_report(SPEC_PATH), shader_report)

    def test_icon_baker_outputs_three_snap_sizes(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
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


def extract_csharp_sdf_profiles(source: str) -> dict[int, dict[str, float]]:
    body_match = re.search(
        r"ResolveHardwareSdfProfile\([^{]+{(?P<body>.*?)\n\s*}\n\n\s*private void RegisterToTickManager",
        source,
        re.DOTALL,
    )
    if body_match is None:
        raise AssertionError("ResolveHardwareSdfProfile body not found")

    body = body_match.group("body")
    profiles: dict[int, dict[str, float]] = {}
    for block_match in re.finditer(r"if \(shortSide <= (?P<limit>\d+)\)\s*{(?P<block>.*?)return;\s*}", body, re.DOTALL):
        limit = int(block_match.group("limit"))
        profiles[limit] = extract_assignments(block_match.group("block"))

    fallback_match = re.search(
        r"weightNormal\s*=\s*(?P<weightNormal>[-0-9.]+)f;\s*"
        r"weightBold\s*=\s*(?P<weightBold>[-0-9.]+)f;\s*"
        r"dilateOffset\s*=\s*(?P<dilateOffset>[-0-9.]+)f;\s*"
        r"softnessOffset\s*=\s*(?P<softnessOffset>[-0-9.]+)f;",
        body.rsplit("return;", 1)[-1],
        re.DOTALL,
    )
    if fallback_match is None:
        raise AssertionError("4K fallback SDF profile not found")

    profiles[2160] = {key: float(value) for key, value in fallback_match.groupdict().items()}
    return profiles


def extract_assignments(block: str) -> dict[str, float]:
    values: dict[str, float] = {}
    for key in ("weightNormal", "weightBold", "dilateOffset", "softnessOffset"):
        match = re.search(rf"{key}\s*=\s*(?P<value>[-0-9.]+)f;", block)
        if match is None:
            raise AssertionError(f"{key} assignment missing")
        values[key] = float(match.group("value"))
    return values


if __name__ == "__main__":
    unittest.main()
