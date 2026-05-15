import sys
import unittest
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


if __name__ == "__main__":
    unittest.main()
