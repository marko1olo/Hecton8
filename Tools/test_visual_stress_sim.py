import io
import sys
import unittest
from contextlib import redirect_stdout
from copy import deepcopy
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import VisualStressSim as visual_stress  # noqa: E402


PROJECT_ROOT = TOOLS_ROOT.parent
MATRIX_PATH = PROJECT_ROOT / "Data" / "System" / "Visual_Scalability_Matrix.json"


class VisualStressSimTests(unittest.TestCase):
    def test_visual_scalability_matrix_passes_hard_self_audits(self) -> None:
        data = visual_stress.load_matrix(MATRIX_PATH)
        report = visual_stress.build_report(data, MATRIX_PATH, seed=8808, frame_count=120)

        self.assertEqual(report["selfAudit"]["status"], "PASS")
        self.assertLessEqual(report["tiers"]["TOASTER"]["estimatedVramMb"], 1600.0)
        self.assertGreaterEqual(report["selfAudit"]["godModeDensityRatioVsPro"], 5.0)
        self.assertEqual(report["tiers"]["TOASTER"]["visualDensityScore"], 14.8)
        self.assertEqual(report["tiers"]["GOD_MODE"]["visualDensityScore"], 3078.4)

    def test_all_tiers_fit_declared_vram_guards(self) -> None:
        data = visual_stress.load_matrix(MATRIX_PATH)
        report = visual_stress.build_report(data, MATRIX_PATH, seed=8808, frame_count=120)
        tiers = visual_stress.tiers_by_name(data)

        for tier_name in visual_stress.REQUIRED_TIERS:
            with self.subTest(tier=tier_name):
                self.assertLessEqual(
                    report["tiers"][tier_name]["estimatedVramMb"],
                    float(tiers[tier_name]["vramGuardMb"]),
                )

    def test_god_mode_keeps_expensive_feature_fallbacks_in_same_json(self) -> None:
        data = visual_stress.load_matrix(MATRIX_PATH)
        fallback_map = data["godModeFallbacks"]
        tier = visual_stress.tiers_by_name(data)["GOD_MODE"]

        for key in visual_stress.REQUIRED_GOD_FALLBACK_KEYS:
            self.assertIn(key, fallback_map)
            self.assertTrue(fallback_map[key])

        self.assertEqual(tier["shaderFeatures"]["pom"]["fallback"], "godModeFallbacks.pom")
        self.assertEqual(tier["shaderFeatures"]["ssr"]["fallback"], "godModeFallbacks.ssr")
        self.assertEqual(tier["particles"]["fallback"], "godModeFallbacks.particleBudget")
        self.assertEqual(tier["textures"]["fallback"], "godModeFallbacks.textureOverrides")

        for path in visual_stress.REQUIRED_GOD_FALLBACK_REFS:
            with self.subTest(path=path):
                fallback_ref = visual_stress.get_nested(tier, path)
                self.assertIsInstance(fallback_ref, str)
                self.assertTrue(fallback_ref.startswith("godModeFallbacks."))
                self.assertIn(fallback_ref.split(".", 1)[1], fallback_map)

    def test_missing_required_tier_reports_failure_without_throwing(self) -> None:
        data = deepcopy(visual_stress.load_matrix(MATRIX_PATH))
        data["tiers"] = [tier for tier in data["tiers"] if tier["tier"] != "GOD_MODE"]

        report = visual_stress.build_report(data, MATRIX_PATH, seed=8808, frame_count=16)

        self.assertEqual(report["selfAudit"]["status"], "FAIL")
        self.assertIn("missing tier GOD_MODE", report["selfAudit"]["failures"])
        self.assertNotIn("GOD_MODE", report["tiers"])

    def test_missing_required_nested_field_reports_failure_without_throwing(self) -> None:
        data = deepcopy(visual_stress.load_matrix(MATRIX_PATH))
        tiers = visual_stress.tiers_by_name(data)
        del tiers["GOD_MODE"]["shaderFeatures"]["pom"]["tapCount"]

        report = visual_stress.build_report(data, MATRIX_PATH, seed=8808, frame_count=16)

        self.assertEqual(report["selfAudit"]["status"], "FAIL")
        self.assertIn("missing tier field GOD_MODE.shaderFeatures.pom.tapCount", report["selfAudit"]["failures"])
        self.assertEqual(report["tiers"], {})

    def test_summary_prints_partial_failure_report_without_throwing(self) -> None:
        data = deepcopy(visual_stress.load_matrix(MATRIX_PATH))
        data["tiers"] = [tier for tier in data["tiers"] if tier["tier"] != "GOD_MODE"]
        report = visual_stress.build_report(data, MATRIX_PATH, seed=8808, frame_count=16)
        output = io.StringIO()

        with redirect_stdout(output):
            visual_stress.print_summary(report)

        summary = output.getvalue()
        self.assertIn("GOD_MODE: MISSING", summary)
        self.assertIn("STATUS=FAIL", summary)

    def test_broken_god_mode_fallback_reference_reports_failure(self) -> None:
        data = deepcopy(visual_stress.load_matrix(MATRIX_PATH))
        tiers = visual_stress.tiers_by_name(data)
        tiers["GOD_MODE"]["shaderFeatures"]["pom"]["fallback"] = "godModeFallbacks.notDefined"

        report = visual_stress.build_report(data, MATRIX_PATH, seed=8808, frame_count=16)

        self.assertEqual(report["selfAudit"]["status"], "FAIL")
        self.assertIn(
            "GOD_MODE fallback ref shaderFeatures.pom.fallback points to missing key notDefined",
            report["selfAudit"]["failures"],
        )


if __name__ == "__main__":
    unittest.main()
