from __future__ import annotations

import hashlib
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
import sys

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import Batch31LocalPbrImportIntent as tool


def write_png(path: Path, color: tuple[int, int, int, int]) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.new("RGBA", (4, 4), color).save(path)
    return hashlib.sha256(path.read_bytes()).hexdigest()


class Batch31LocalPbrImportIntentTests(unittest.TestCase):
    def test_build_report_detects_static_review_and_contract_block(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            package_root = root / "Docs/GeneratedAssets/Batch31_LocalPBR/TX_Test"
            albedo = package_root / "TX_Test_AlbedoSource.png"
            normal = package_root / "TX_Test_NormalSource.png"
            mrao = package_root / "TX_Test_MRAOSource.png"
            height = package_root / "TX_Test_HeightSource.png"
            hashes = {
                "albedo": write_png(albedo, (80, 90, 100, 255)),
                "normal": write_png(normal, (128, 128, 255, 255)),
                "mrao": write_png(mrao, (0, 160, 80, 255)),
                "height": write_png(height, (90, 90, 90, 255)),
            }
            index_path = root / tool.DEFAULT_INDEX
            index_path.parent.mkdir(parents=True, exist_ok=True)
            index_path.write_text(
                json.dumps(
                    {
                        "packages": [
                            {
                                "id": "TX_Test",
                                "source": "source.png",
                                "not_unity_imported": True,
                                "not_visual_acceptance": True,
                                "outputs": {
                                    "albedo": tool.rel(albedo, root),
                                    "normal": tool.rel(normal, root),
                                    "mrao": tool.rel(mrao, root),
                                    "height": tool.rel(height, root),
                                },
                                "sha256": hashes,
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )

            report = tool.build_report(root, index_path)

        self.assertEqual("STATIC_SOURCE", report["evidenceClass"])
        self.assertEqual("STATIC_IMAGE_IMPORT_INTENT", report["evidenceScope"])
        self.assertEqual(1, report["summary"]["packages"])
        self.assertEqual(4, report["summary"]["rows"])
        self.assertEqual(1, report["summary"]["blockedRows"])
        self.assertEqual(1, report["summary"]["channelContractBlockedPackages"])
        self.assertIn("_MasterShadowParams.w proof", report["batch31PromotionRequirement"])
        self.assertTrue(any("HectonMasterMaterialMigrator1615.cs" in item for item in report["shaderContractEvidence"]))
        self.assertTrue(any("HectonMaskChannelPacker.cs" in item for item in report["shaderContractEvidence"]))
        self.assertTrue(any("Hecton_Master_Lit.shader" in item and "layout 3" in item for item in report["shaderContractEvidence"]))
        self.assertTrue(any("Hecton8_UberNoir.hlsl" in item for item in report["shaderContractEvidence"]))
        self.assertTrue(any("Hecton_MraoAtlasLit.shader" in item for item in report["shaderContractEvidence"]))
        self.assertIn("blocked_channel_semantics_mrao_vs_arm", report["packages"][0]["warnings"])
        self.assertIn("requires_shader_target_layout_decision", report["packages"][0]["warnings"])
        self.assertTrue(any(row["role_key"] == "mrao" and row["verdict"] == "BLOCKED" and row["runtime_import"] == 0 for row in report["rows"]))

    def test_build_report_flags_hash_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            package_root = root / "Docs/GeneratedAssets/Batch31_LocalPBR/TX_Test"
            albedo = package_root / "TX_Test_AlbedoSource.png"
            write_png(albedo, (80, 90, 100, 255))
            index_path = root / tool.DEFAULT_INDEX
            index_path.parent.mkdir(parents=True, exist_ok=True)
            index_path.write_text(
                json.dumps(
                    {
                        "packages": [
                            {
                                "id": "TX_Test",
                                "outputs": {"albedo": tool.rel(albedo, root)},
                                "sha256": {"albedo": "0" * 64},
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )

            report = tool.build_report(root, index_path)

        self.assertEqual(1, report["summary"]["errorRows"])
        self.assertIn("sha256_mismatch", report["rows"][0]["issues"])

    def test_unknown_output_role_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            texture = root / "Docs/GeneratedAssets/Batch31_LocalPBR/TX_Test/TX_Test_Roughness.png"
            hash_value = write_png(texture, (80, 90, 100, 255))
            index_path = root / tool.DEFAULT_INDEX
            index_path.parent.mkdir(parents=True, exist_ok=True)
            index_path.write_text(
                json.dumps(
                    {
                        "packages": [
                            {
                                "id": "TX_Test",
                                "outputs": {"roughness": tool.rel(texture, root)},
                                "sha256": {"roughness": hash_value},
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaises(tool.ImportIntentError):
                tool.build_report(root, index_path)

    def test_output_path_must_stay_under_batch31_root(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            outside = root / "Docs/GeneratedAssets/Other/TX_Test_Albedo.png"
            hash_value = write_png(outside, (80, 90, 100, 255))
            index_path = root / tool.DEFAULT_INDEX
            index_path.parent.mkdir(parents=True, exist_ok=True)
            index_path.write_text(
                json.dumps(
                    {
                        "packages": [
                            {
                                "id": "TX_Test",
                                "outputs": {"albedo": tool.rel(outside, root)},
                                "sha256": {"albedo": hash_value},
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaises(tool.ImportIntentError):
                tool.build_report(root, index_path)

    def test_absolute_output_path_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            texture = root / "Docs/GeneratedAssets/Batch31_LocalPBR/TX_Test/TX_Test_Albedo.png"
            hash_value = write_png(texture, (80, 90, 100, 255))
            index_path = root / tool.DEFAULT_INDEX
            index_path.parent.mkdir(parents=True, exist_ok=True)
            index_path.write_text(
                json.dumps(
                    {
                        "packages": [
                            {
                                "id": "TX_Test",
                                "outputs": {"albedo": str(texture.resolve())},
                                "sha256": {"albedo": hash_value},
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaises(tool.ImportIntentError):
                tool.build_report(root, index_path)

    def test_cli_fail_on_error_fails_on_blocked_channel_semantics(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            package_root = root / "Docs/GeneratedAssets/Batch31_LocalPBR/TX_Test"
            albedo = package_root / "TX_Test_AlbedoSource.png"
            normal = package_root / "TX_Test_NormalSource.png"
            mrao = package_root / "TX_Test_MRAOSource.png"
            hashes = {
                "albedo": write_png(albedo, (80, 90, 100, 255)),
                "normal": write_png(normal, (128, 128, 255, 255)),
                "mrao": write_png(mrao, (0, 160, 80, 255)),
            }
            index_path = root / tool.DEFAULT_INDEX
            index_path.parent.mkdir(parents=True, exist_ok=True)
            index_path.write_text(
                json.dumps(
                    {
                        "packages": [
                            {
                                "id": "TX_Test",
                                "outputs": {
                                    "albedo": tool.rel(albedo, root),
                                    "normal": tool.rel(normal, root),
                                    "mrao": tool.rel(mrao, root),
                                },
                                "sha256": hashes,
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )

            result = subprocess.run(
                [
                    sys.executable,
                    str(Path(tool.__file__).resolve()),
                    "--project-root",
                    str(root),
                    "--fail-on-error",
                ],
                text=True,
                capture_output=True,
                check=False,
            )

        self.assertEqual(2, result.returncode)
        self.assertIn("blocked=1", result.stdout)


if __name__ == "__main__":
    unittest.main()
