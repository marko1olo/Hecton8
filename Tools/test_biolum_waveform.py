import json
import sys
import unittest
from pathlib import Path

from PIL import Image


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import BiolumWaveform as biolum  # noqa: E402


PROJECT_ROOT = TOOLS_ROOT.parent
VISUALS_ROOT = PROJECT_ROOT / "Data" / "Visuals"


class BiolumWaveformTests(unittest.TestCase):
    def test_source_profiles_match_contract_counts(self) -> None:
        profiles = biolum.build_profiles()
        palettes = biolum.build_palettes()

        self.assertEqual(len(profiles), 20)
        self.assertEqual(len(palettes), 8)
        self.assertTrue(all(4 <= len(profile.harmonics) <= biolum.MAX_HARMONICS for profile in profiles))
        self.assertTrue(all(len(palette.toaster) == biolum.TOASTER_COLOR_COUNT for palette in palettes))
        self.assertTrue(all(len(palette.god_mode) == biolum.GOD_COLOR_COUNT for palette in palettes))

    def test_generated_json_matches_source_profile_names_and_safety(self) -> None:
        profiles_json = json.loads((VISUALS_ROOT / "Biolum_Profiles.json").read_text(encoding="utf-8"))
        verification_json = json.loads((VISUALS_ROOT / "Biolum_Verification.json").read_text(encoding="utf-8"))
        source_names = [profile.name for profile in biolum.build_profiles()]
        json_names = [profile["name"] for profile in profiles_json["profiles"]]
        clamped_names = [
            profile["name"]
            for profile in profiles_json["profiles"]
            if profile["safety"]["safetyClampActive"]
        ]

        self.assertEqual(profiles_json["status"], "RHYTHMS COMPOSED")
        self.assertEqual(verification_json["status"], "RHYTHMS COMPOSED")
        self.assertEqual(json_names, source_names)
        self.assertEqual(clamped_names, ["Thermal Vent Alarm", "Emergency Beacon"])
        self.assertEqual(verification_json["summary"]["safetyClampProfiles"], 2)
        self.assertLessEqual(
            verification_json["summary"]["maxDcDrift01"],
            verification_json["driftLimit01"],
        )
        self.assertLessEqual(
            verification_json["summary"]["maxOrganicJerk95"],
            verification_json["organicJerkLimit"],
        )

    def test_binary_readback_and_manifest_values(self) -> None:
        readback = biolum.readback_binary(VISUALS_ROOT / "Biolum_Profiles.bin")
        binary_records = biolum.verify_binary_records(VISUALS_ROOT / "Biolum_Profiles.bin")
        manifest = json.loads((VISUALS_ROOT / "Biolum_Manifest.json").read_text(encoding="utf-8"))
        schema = json.loads((VISUALS_ROOT / "Biolum_BinarySchema.json").read_text(encoding="utf-8"))
        manifest_info = biolum.verify_manifest(VISUALS_ROOT / "Biolum_Manifest.json")
        package_info = biolum.verify_generated_package(VISUALS_ROOT)

        self.assertEqual(readback["profileCount"], 20)
        self.assertEqual(readback["paletteCount"], 8)
        self.assertEqual(readback["maxHarmonics"], biolum.MAX_HARMONICS)
        self.assertEqual(readback["curveSamples"], biolum.CURVE_SAMPLES)
        self.assertEqual(readback["godColorCount"], biolum.GOD_COLOR_COUNT)
        self.assertEqual(readback["toasterColorCount"], biolum.TOASTER_COLOR_COUNT)
        self.assertEqual(readback["bytes"], 25936)
        self.assertEqual(readback["payloadCrc32"], "0x0D545E74")
        self.assertEqual(manifest["status"], "RHYTHMS COMPOSED")
        self.assertEqual(manifest["payloadCrc32"], readback["payloadCrc32"])
        self.assertEqual(manifest["profileCount"], 20)
        self.assertEqual(len(manifest["artifacts"]), 6)
        self.assertTrue(all(len(artifact["sha256"]) == 64 for artifact in manifest["artifacts"]))
        self.assertEqual(manifest_info["artifactCount"], 6)
        self.assertEqual(manifest_info["payloadCrc32"], "0x0D545E74")
        self.assertEqual(binary_records["profileCount"], 20)
        self.assertEqual(binary_records["paletteCount"], 8)
        self.assertEqual(binary_records["safetyClampedRecords"], 2)
        self.assertEqual(binary_records["payloadCrc32"], "0x0D545E74")
        self.assertEqual(package_info["artifactCount"], 6)
        self.assertEqual(package_info["profileCount"], 20)
        self.assertEqual(package_info["paletteCount"], 8)
        self.assertEqual(package_info["safetyClampedRecords"], 2)
        self.assertEqual(package_info["payloadCrc32"], "0x0D545E74")
        self.assertEqual(schema["schema"], "H8_BIOLUM_BINARY_SCHEMA")
        self.assertEqual(schema["constants"]["profileStride"], 1232)
        self.assertEqual(schema["profileRecord"]["curveSamples"]["offset"], 208)
        self.assertEqual(schema["paletteRecord"]["colors"]["floatCount"], 36)

    def test_waveform_images_are_present_and_sized(self) -> None:
        with Image.open(VISUALS_ROOT / "Biolum_Waveforms.png") as png:
            self.assertEqual(png.size, (1800, 1200))

        with Image.open(VISUALS_ROOT / "Biolum_Waveforms.gif") as gif:
            self.assertEqual(gif.size, (960, 720))
            self.assertEqual(gif.n_frames, 48)


if __name__ == "__main__":
    unittest.main()
