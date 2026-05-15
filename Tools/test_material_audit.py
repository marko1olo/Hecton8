#!/usr/bin/env python3
"""Regression tests for the HECTON-8 material audit tool."""

from __future__ import annotations

import sys
import subprocess
import tempfile
import unittest
from pathlib import Path

from PIL import Image


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import MaterialAudit as audit  # noqa: E402


def write_meta(path: Path, *, srgb: int, mip: int, texture_type: int, compression: int = 1) -> None:
    path.with_name(path.name + ".meta").write_text(
        "\n".join(
            [
                "fileFormatVersion: 2",
                "guid: 11111111111111111111111111111111",
                "TextureImporter:",
                "  mipmaps:",
                f"    enableMipMap: {mip}",
                f"    sRGBTexture: {srgb}",
                "  isReadable: 0",
                f"  textureType: {texture_type}",
                "  platformSettings:",
                "  - serializedVersion: 4",
                "    buildTarget: Standalone",
                "    maxTextureSize: 512",
                "    textureFormat: 25",
                f"    textureCompression: {compression}",
            ]
        ),
        encoding="utf-8",
    )


class MaterialAuditTests(unittest.TestCase):
    def test_texture_memory_estimate_uses_mip_factor(self) -> None:
        self.assertAlmostEqual(1.333, audit.estimate_texture_mib(1024, 1024, 8, True), places=3)
        self.assertAlmostEqual(1.0, audit.estimate_texture_mib(1024, 1024, 8, False), places=3)

    def test_classifier_ignores_non_surface_skybox_and_ui(self) -> None:
        skybox = Path("Assets/_Project/Art/Skyboxes/panorama_den.png")
        ui = Path("Assets/_Project/Art/Sprites/ui/Panel_Color.png")
        surface = Path("Assets/_Project/Art/TEXTURES/Terrain/basalt/Rock031_1K-JPG_Color.jpg")
        orm = Path("Assets/_Project/Art/TEXTURES/Terrain/basalt/Rock031_1K-JPG_ORM.png")

        self.assertFalse(audit.classify_texture(skybox)["is_albedo_candidate"])
        self.assertFalse(audit.classify_texture(ui)["is_albedo_candidate"])
        self.assertTrue(audit.classify_texture(surface)["is_albedo_candidate"])
        self.assertTrue(audit.classify_texture(orm)["is_orm_candidate"])

    def test_albedo_energy_flags_only_overbright_surface_albedo(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            bright = root / "Panel_Albedo.png"
            dark = root / "DarkPanel_Albedo.png"
            Image.new("RGB", (8, 8), (255, 255, 255)).save(bright)
            Image.new("RGB", (8, 8), (64, 64, 64)).save(dark)
            write_meta(bright, srgb=1, mip=1, texture_type=0)
            write_meta(dark, srgb=1, mip=1, texture_type=0)

            bright_record = audit.inspect_image(bright, root, 8)
            dark_record = audit.inspect_image(dark, root, 8)

            self.assertEqual("FAIL", bright_record["energy_status"])
            self.assertEqual("PASS", dark_record["energy_status"])

    def test_import_issues_detect_data_srgb_and_normal_import_debt(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            data = root / "Hull_ORM.png"
            normal = root / "Hull_Normal.png"
            write_meta(data, srgb=1, mip=1, texture_type=0)
            write_meta(normal, srgb=1, mip=1, texture_type=0)

            data_record = audit.inspect_image(data, root, 16)
            normal_record = audit.inspect_image(normal, root, 16)

            self.assertIn("DATA_TEXTURE_SRGB_ON", data_record["import_issues"])
            self.assertIn("NORMAL_SRGB_ON", normal_record["import_issues"])
            self.assertIn("NORMAL_NOT_TEXTURETYPE_NORMAL", normal_record["import_issues"])

    def test_material_slot_issues_detect_missing_orm_and_detail(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            material = root / "MAT_Test.mat"
            material.write_text(
                "\n".join(
                    [
                        "%YAML 1.1",
                        "m_SavedProperties:",
                        "  m_TexEnvs:",
                        "  - _BaseMap:",
                        "      m_Texture: {fileID: 2800000, guid: 22222222222222222222222222222222, type: 3}",
                        "  - _OcclusionMap:",
                        "      m_Texture: {fileID: 2800000, guid: 33333333333333333333333333333333, type: 3}",
                        "  - _MetallicGlossMap:",
                        "      m_Texture: {fileID: 2800000, guid: 44444444444444444444444444444444, type: 3}",
                    ]
                ),
                encoding="utf-8",
            )

            resolved = audit.resolve_material(audit.parse_material(material, root), {})

            self.assertIn("UNRESOLVED_TEXTURE_GUID", resolved["issues"])
            self.assertIn("NO_PROMPT_ORM_SLOT", resolved["issues"])
            self.assertIn("LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW", resolved["issues"])
            self.assertIn("SEPARATE_OCCLUSION_AND_METALLIC_MAPS", resolved["issues"])
            self.assertIn("NO_DETAIL_MAP_SLOT", resolved["issues"])
            self.assertEqual(3, len(resolved["unresolved_texture_refs"]))
            self.assertEqual("HIGH", resolved["channel_packing_candidate"]["priority"])

    def test_scoped_material_audit_resolves_guids_from_wider_root(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            textures = root / "Textures"
            materials = root / "Materials"
            textures.mkdir()
            materials.mkdir()

            albedo = textures / "Panel_Albedo.png"
            albedo.write_bytes(b"")
            write_meta(albedo, srgb=1, mip=1, texture_type=0)

            material = materials / "MAT_Scoped.mat"
            material.write_text(
                "\n".join(
                    [
                        "%YAML 1.1",
                        "m_SavedProperties:",
                        "  m_TexEnvs:",
                        "  - _BaseMap:",
                        "      m_Texture: {fileID: 2800000, guid: 11111111111111111111111111111111, type: 3}",
                    ]
                ),
                encoding="utf-8",
            )

            report = audit.run_audit(materials, 16, False, root)

            self.assertEqual(str(root.as_posix()), report["resolve_root"])
            self.assertEqual(0, report["material_summary"]["materials_with_unresolved_texture_refs"])
            self.assertEqual(0, report["material_summary"]["unresolved_texture_ref_count"])

    def test_cli_fail_flags_return_expected_exit_codes(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            texture = root / "Hull_ORM.png"
            Image.new("RGB", (8, 8), (64, 64, 64)).save(texture)
            write_meta(texture, srgb=1, mip=1, texture_type=0)

            import_gate = subprocess.run(
                [
                    sys.executable,
                    str(TOOLS_ROOT / "MaterialAudit.py"),
                    "--root",
                    str(root),
                    "--sample-size",
                    "16",
                    "--fail-on-import-issues",
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(2, import_gate.returncode, import_gate.stdout + import_gate.stderr)
            self.assertIn("import_issue_textures=1", import_gate.stdout)

        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            material = root / "MAT_CliGate.mat"
            material.write_text(
                "\n".join(
                    [
                        "%YAML 1.1",
                        "m_SavedProperties:",
                        "  m_TexEnvs:",
                        "  - _BaseMap:",
                        "      m_Texture: {fileID: 2800000, guid: 22222222222222222222222222222222, type: 3}",
                    ]
                ),
                encoding="utf-8",
            )

            material_gate = subprocess.run(
                [
                    sys.executable,
                    str(TOOLS_ROOT / "MaterialAudit.py"),
                    "--root",
                    str(root),
                    "--sample-size",
                    "16",
                    "--fail-on-material-issues",
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(3, material_gate.returncode, material_gate.stdout + material_gate.stderr)
            self.assertIn("materials_with_issues=1", material_gate.stdout)

            unresolved_gate = subprocess.run(
                [
                    sys.executable,
                    str(TOOLS_ROOT / "MaterialAudit.py"),
                    "--root",
                    str(root),
                    "--sample-size",
                    "16",
                    "--fail-on-unresolved-refs",
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(4, unresolved_gate.returncode, unresolved_gate.stdout + unresolved_gate.stderr)
            self.assertIn("unresolved_texture_refs=1", unresolved_gate.stdout)

    def test_markdown_and_csv_exports_include_recommendations(self) -> None:
        report = {
            "root": "Temp",
            "resolve_root": "Temp",
            "sample_size": 16,
            "include_third_party": False,
            "gate_exit_codes": {
                "energy_failures": 1,
                "import_issues": 2,
                "material_issues": 3,
                "unresolved_texture_refs": 4,
            },
            "texture_summary": {
                "texture_count": 1,
                "albedo_candidate_count": 0,
                "normal_candidate_count": 0,
                "orm_candidate_count": 1,
                "detail_candidate_count": 0,
                "energy_fail_count": 0,
                "energy_warn_count": 0,
                "import_issue_count": 1,
                "import_issue_counts": {"DATA_TEXTURE_SRGB_ON": 1},
                "detail_suggestions": [],
                "energy_failures": [],
                "energy_warnings": [],
                "import_issue_textures": [
                    {"path": "Hull_ORM.png", "import_issues": ["DATA_TEXTURE_SRGB_ON"]},
                ],
                "estimated_texture_mib": 1.333,
                "largest_estimated_textures": [
                    {
                        "path": "Hull_ORM.png",
                        "estimated_resident_mib": 1.333,
                        "memory_role": "BC7_ORM_LINEAR_8BPP",
                        "width": 1024,
                        "height": 1024,
                    },
                ],
            },
            "material_summary": {
                "material_count": 1,
                "materials_with_prompt_orm": 0,
                "materials_with_legacy_mask": 0,
                "materials_with_packed_mask": 0,
                "materials_with_detail": 0,
                "materials_with_issues": 1,
                "channel_packing_candidate_count": 1,
                "channel_packing_priority_counts": {"LOW": 1},
                "channel_packing_candidates": [
                    {
                        "path": "MAT_Test.mat",
                        "priority": "LOW",
                        "reason": "Base material has no prompt ORM slot.",
                        "base_maps": ["_BaseMap:Panel_Albedo.png"],
                        "normal_maps": [],
                        "occlusion_sources": [],
                        "roughness_sources": [],
                        "metallic_sources": [],
                        "legacy_mask_sources": [],
                        "detail_sources": [],
                        "has_detail": False,
                    },
                ],
                "vram_model": {
                    "standard_mib_per_material": 6.65,
                    "optimized_mib_per_material": 2.99,
                    "candidate_standard_mib": 6.65,
                    "candidate_optimized_mib": 2.99,
                    "candidate_saved_mib": 3.66,
                    "candidate_reduction_percent": 55.0,
                },
                "issue_counts": {"NO_DETAIL_MAP_SLOT": 1},
                "issue_materials": [
                    {"path": "MAT_Test.mat", "issues": ["NO_DETAIL_MAP_SLOT"]},
                ],
            },
            "god_mode_texture_overrides": [
                {
                    "asset_class": "Hero cockpit albedo",
                    "toaster_max": 1024,
                    "deck_max": 2048,
                    "pro_max": 2048,
                    "god_mode_max": 4096,
                    "format": "BC7 sRGB",
                    "fallback": "Demote one mip tier when VRAM used/total > 0.90.",
                },
            ],
            "global_detail_overlay_plan": [
                {
                    "overlay_role": "fine_cockpit_scratches",
                    "source_status": "MISSING_AUTHORING",
                    "target_surfaces": "Cockpit glass",
                    "toaster_rule": "Disabled except inspection props.",
                    "god_mode_rule": "BC4/BC5 1024 overlay at 8x-16x tiling.",
                    "expected_detail_gain_percent": 20,
                },
            ],
        }

        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            markdown = root / "report.md"
            csv_prefix = root / "report"
            audit.write_markdown_report(report, markdown)
            audit.write_csv_reports(report, csv_prefix)

            markdown_text = markdown.read_text(encoding="utf-8")
            texture_csv = Path(f"{csv_prefix}_texture_import_issues.csv").read_text(encoding="utf-8")
            material_csv = Path(f"{csv_prefix}_material_issues.csv").read_text(encoding="utf-8")
            channel_csv = Path(f"{csv_prefix}_channel_packing_candidates.csv").read_text(encoding="utf-8")
            memory_csv = Path(f"{csv_prefix}_texture_memory_hotspots.csv").read_text(encoding="utf-8")
            overrides_csv = Path(f"{csv_prefix}_god_mode_texture_overrides.csv").read_text(encoding="utf-8")
            detail_plan_csv = Path(f"{csv_prefix}_global_detail_overlay_plan.csv").read_text(encoding="utf-8")

            self.assertIn("Disable sRGB", markdown_text)
            self.assertIn("Channel Packing Candidates", markdown_text)
            self.assertIn("Candidate saved MiB", markdown_text)
            self.assertIn("Texture Memory Hotspots", markdown_text)
            self.assertIn("GOD_MODE Texture Overrides", markdown_text)
            self.assertIn("Global Detail Overlay Plan", markdown_text)
            self.assertIn("Gate Exit Codes", markdown_text)
            self.assertIn("unresolved_texture_refs", markdown_text)
            self.assertIn("Hull_ORM.png", texture_csv)
            self.assertIn("NO_DETAIL_MAP_SLOT", material_csv)
            self.assertIn("MAT_Test.mat", channel_csv)
            self.assertIn("BC7_ORM_LINEAR_8BPP", memory_csv)
            self.assertIn("Hero cockpit albedo", overrides_csv)
            self.assertIn("fine_cockpit_scratches", detail_plan_csv)


if __name__ == "__main__":
    unittest.main()
