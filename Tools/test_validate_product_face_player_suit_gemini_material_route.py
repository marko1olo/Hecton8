import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateProductFacePlayerSuitGeminiMaterialRoute as validator  # noqa: E402


class ProductFacePlayerSuitGeminiMaterialRouteTests(unittest.TestCase):
    def setUp(self) -> None:
        self.original_specs = validator.EXPECTED_SPECS
        self.original_material_root = validator.MATERIAL_ROOT
        self.original_output_root = validator.OUTPUT_ROOT
        self.original_iter_material_assets = validator.iter_material_assets
        self.original_root = validator.ROOT

    def tearDown(self) -> None:
        validator.EXPECTED_SPECS = self.original_specs
        validator.MATERIAL_ROOT = self.original_material_root
        validator.OUTPUT_ROOT = self.original_output_root
        validator.iter_material_assets = self.original_iter_material_assets
        validator.ROOT = self.original_root

    def configure_fixture(self, root: Path, include_guid_in_output: bool) -> None:
        validator.ROOT = root
        validator.MATERIAL_ROOT = root / "GeneratedMaterials"
        validator.OUTPUT_ROOT = root / "SuitOutput"
        validator.EXPECTED_SPECS = [
            {
                "slot": slot,
                "slotName": f"Slot{slot}",
                "output": f"MAT_GEN_PlayerSuit_Slot{slot}",
                "provider": "GeminiTest",
                "id": "gemini_test_suit_fabric",
            }
            for slot in range(4)
        ]

        texture_dir = root / "Textures"
        texture_dir.mkdir(parents=True)
        maps = {}
        for index, key in enumerate(validator.REQUIRED_MAPS):
            path = texture_dir / f"{key}.png"
            path.write_bytes(b"png")
            guid = f"{index + 1:032x}"
            path.with_suffix(path.suffix + ".meta").write_text(f"guid: {guid}\n", encoding="utf-8")
            maps[key] = path.as_posix()

        source_material = validator.MATERIAL_ROOT / "GeminiTest" / "MAT_EXT_GeminiTest_gemini_test_suit_fabric.mat"
        source_material.parent.mkdir(parents=True)
        source_material.write_text("%YAML source material\n", encoding="utf-8")

        validator.OUTPUT_ROOT.mkdir(parents=True)
        for spec in validator.EXPECTED_SPECS:
            output_material = validator.OUTPUT_ROOT / f"{spec['output']}.mat"
            output_text = "\n".join(maps[key].split("/")[-1] for key in validator.REQUIRED_MAPS)
            if include_guid_in_output:
                output_text += "\n" + "\n".join(
                    (texture_dir / f"{key}.png.meta").read_text(encoding="utf-8").replace("guid: ", "").strip()
                    for key in validator.REQUIRED_MAPS
                )
            output_material.write_text(output_text, encoding="utf-8")

        validator.iter_material_assets = lambda: {
            ("GeminiTest", "gemini_test_suit_fabric"): {"maps": maps}
        }

    def test_post_apply_accepts_output_material_with_manifest_texture_guids(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            self.configure_fixture(Path(temp), include_guid_in_output=True)
            errors: list[str] = []

            validator.validate_post_apply(errors)

            self.assertEqual([], errors)

    def test_post_apply_rejects_output_material_without_manifest_texture_guids(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            self.configure_fixture(Path(temp), include_guid_in_output=False)
            errors: list[str] = []

            validator.validate_post_apply(errors)

            self.assertTrue(
                any("missing BaseColor texture guid" in error for error in errors),
                errors,
            )


if __name__ == "__main__":
    unittest.main()
