import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import MemoryBudgetCheck as budget  # noqa: E402


PROJECT_ROOT = TOOLS_ROOT.parent


class MemoryBudgetCheckTests(unittest.TestCase):
    def test_png_and_jpeg_dimensions_are_read_without_external_dependencies(self) -> None:
        png = PROJECT_ROOT / "Data" / "Textures" / "BlueNoise_RGBA.png"
        jpg = PROJECT_ROOT / "Assets" / "_Project" / "Art" / "Models" / "Rocks" / "Rock 7" / "Materials" / "2.jpg"

        self.assertEqual(budget.read_png_size(png), (256, 256, "RGBA"))
        self.assertEqual(budget.read_jpeg_size(jpg), (4000, 4000, "RGB"))

    def test_obj_triangle_count_triangulates_ngons(self) -> None:
        obj = PROJECT_ROOT / "Assets" / "Dynamic Decals" / "Resources" / "Decal.obj"

        self.assertGreater(budget.count_obj_triangles(obj) or 0, 0)

    def test_fbx_polygon_index_math_counts_faces(self) -> None:
        values = [0, 1, -3, 4, 5, 6, -8]
        self.assertEqual(budget.count_triangles_from_indices(values), 3)

    def test_mesh_meta_fields_are_exposed_for_importer_risk(self) -> None:
        mesh = PROJECT_ROOT / "Assets" / "ScifiFacility" / "Models" / "decals" / "decal_01.fbx"

        fields = budget.parse_mesh_meta_fields(mesh)
        self.assertEqual(fields[0], "1")
        self.assertEqual(fields[1], "2")
        self.assertEqual(fields[3], "1")
        self.assertEqual(fields[5], "1")

    def test_mesh_import_flags_cover_readable_blendshape_and_compression_risk(self) -> None:
        mesh = PROJECT_ROOT / "Assets" / "Demo" / "RiskMesh.fbx"
        record = budget.MeshRecord(
            path=mesh,
            meta_is_readable="1",
            meta_mesh_compression="0",
            meta_import_blend_shapes="1",
            meta_add_colliders="1",
            meta_keep_quads="1",
        )

        budget.append_mesh_import_flags(record)

        self.assertIn("MESH_READ_WRITE_ENABLED_STATIC_SUSPECT", record.flags)
        self.assertIn("MESH_COMPRESSION_OFF_STATIC_SUSPECT", record.flags)
        self.assertIn("MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT", record.flags)
        self.assertIn("MESH_IMPORT_COLLIDERS_ENABLED_STATIC_SUSPECT", record.flags)
        self.assertIn("MESH_KEEP_QUADS_ENABLED_STATIC_SUSPECT", record.flags)

    def test_texture_redline_flags_large_rgba_png(self) -> None:
        texture = PROJECT_ROOT / "Assets" / "_Project" / "Art" / "TEXTURES" / "Aegir_storms.png"
        flags, _recommendation = budget.classify_texture(texture, 4096, 2048, "RGBA", "4096", "1", "12", "0", "0")

        self.assertIn("VRAM CRIME: TEXTURE_GT_2048", flags)
        self.assertIn("VRAM CRIME: IMPORT_MAX_GT_2048", flags)
        self.assertIn("STREAMING_MIPMAPS_OFF_LARGE", flags)
        self.assertTrue(budget.is_first_party_production_candidate(texture, PROJECT_ROOT))

    def test_summary_payload_exposes_gate_keys_without_filesystem_writes(self) -> None:
        texture = PROJECT_ROOT / "Assets" / "_Project" / "Art" / "TEXTURES" / "TX_split.png"
        texture_record = budget.TextureRecord(
            path=texture,
            width=4096,
            height=4096,
            mode="RGBA",
            bc7_bytes=4096 * 4096,
            flags=["VRAM CRIME: TEXTURE_GT_2048"],
        )
        mesh = PROJECT_ROOT / "Assets" / "Demo" / "BigMesh.fbx"
        mesh_record = budget.MeshRecord(
            path=mesh,
            file_bytes=1024,
            triangles=90000,
            lod_detected=False,
            meta_is_readable="1",
            flags=["MESH_GT_80K_ABSOLUTE_STATIC", "MESH_READ_WRITE_ENABLED_STATIC_SUSPECT"],
        )

        payload = budget.build_summary_payload(PROJECT_ROOT, [texture_record], [mesh_record], [], "LINK_XML_MISSING", [])

        self.assertEqual(payload["texture_count"], 1)
        self.assertEqual(payload["schema_version"], 1)
        self.assertIn("TEXTURE_VRAM_CRIMES", payload["gate_reasons"])
        self.assertIn("MESH_REDLINE_OR_RISK", payload["gate_reasons"])
        self.assertIn(".codex-build", payload["skipped_directory_names"])
        self.assertEqual(payload["mesh_redline_rows"], 1)
        self.assertEqual(payload["mesh_import_risk_rows"], 1)
        self.assertEqual(payload["mesh_read_write_enabled_rows"], 1)
        self.assertEqual(payload["first_party_mesh_import_risk_rows"], 0)
        self.assertFalse(payload["critical_vram_overflow"])
        self.assertEqual(payload["ci_expected_exit_code"], 2)

    def test_iter_assets_uses_case_insensitive_generated_tree_exclusion(self) -> None:
        self.assertIn(".codex-build", budget.SKIP_DIRS)
        self.assertIn(".codex-artifacts", budget.SKIP_DIRS)
        self.assertIn(".codex-build", budget.SKIP_DIR_NAMES_LOWER)
        self.assertIn("library", budget.SKIP_DIR_NAMES_LOWER)


if __name__ == "__main__":
    unittest.main()
