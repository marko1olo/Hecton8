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

    def test_unity_texture_container_dimensions_are_read_without_external_dependencies(self) -> None:
        samples = [
            ("Assets/ScifiFacility/Textures/Lights_emissive.tga", (2048, 2048, "RGB")),
            ("Assets/ScifiFacility/Textures/Text_01.psd", (1024, 1024, "PSD_4CH")),
            ("Assets/Bakery/ftUnitySpotTexture.bmp", (128, 128, "RGB")),
            ("Assets/MapMagic/Tools/GUI/Editor/Resources/DPUI/PolyLineTex.tif", (1, 8, "TIFF")),
            ("Packages/com.waveharmonic.crest/Shared/Textures/Skybox.hdr", (2048, 1024, "HDR")),
            ("Assets/_Project/Scenes/02_HECTON_WORLD/ReflectionProbe-0.exr", (768, 128, "EXR")),
            ("Data/Visuals/Biolum_Waveforms.gif", (960, 720, "GIF")),
        ]

        for relative_path, expected in samples:
            with self.subTest(relative_path=relative_path):
                self.assertEqual(budget.read_image_size(PROJECT_ROOT / relative_path), expected)

    def test_obj_triangle_count_triangulates_ngons(self) -> None:
        obj = PROJECT_ROOT / "Assets" / "Dynamic Decals" / "Resources" / "Decal.obj"

        self.assertGreater(budget.count_obj_triangles(obj) or 0, 0)

    def test_fbx_polygon_index_math_counts_faces(self) -> None:
        values = [0, 1, -3, 4, 5, 6, -8]
        self.assertEqual(budget.count_triangles_from_indices(values), 3)

    def test_gltf_and_glb_triangle_counts_are_included_as_mesh_sources(self) -> None:
        document = {
            "accessors": [{"count": 6}, {"count": 4}, {"count": 5}],
            "meshes": [
                {
                    "primitives": [
                        {"mode": 4, "indices": 0},
                        {"mode": 5, "attributes": {"POSITION": 1}},
                        {"mode": 6, "attributes": {"POSITION": 2}},
                        {"mode": 1, "attributes": {"POSITION": 0}},
                    ]
                }
            ],
        }
        glb = PROJECT_ROOT / "Assets" / "_Project" / "Art" / "Models" / "Rocks" / "nordic_beach_rock_vbumba2fa_mid.glb"

        self.assertIn(".glb", budget.MESH_EXTS)
        self.assertIn(".gltf", budget.MESH_EXTS)
        self.assertEqual(budget.count_gltf_document_triangles(document), 7)
        self.assertGreater(budget.count_gltf_triangles(glb) or 0, 0)

    def test_render_texture_asset_estimate_is_reported_from_yaml(self) -> None:
        rt_path = PROJECT_ROOT / "Assets" / "_Project" / "Art" / "TEXTURES" / "RT_HUD_Display.renderTexture"

        records = budget.audit_render_textures([rt_path])

        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].width, 1280)
        self.assertEqual(records[0].height, 720)
        self.assertEqual(records[0].color_format, "8")
        self.assertEqual(records[0].depth_stencil_format, "94")
        self.assertGreater(records[0].estimated_bytes, 0)
        self.assertIn("RENDER_TEXTURE_DEPTH_STENCIL_PRESENT_STATIC_SUSPECT", records[0].flags)

    def test_render_texture_source_hotspots_find_runtime_allocations(self) -> None:
        hits = budget.find_render_texture_source_hotspots(PROJECT_ROOT)

        self.assertTrue(any(hit.pattern == "new RenderTexture" and not hit.editor_only for hit in hits))
        self.assertTrue(any("RenderTextureDescriptor" == hit.pattern and not hit.editor_only for hit in hits))

    def test_static_geometry_estimate_is_conservative_and_deterministic(self) -> None:
        self.assertEqual(
            budget.estimate_geometry_bytes(10),
            10 * 3 * (budget.STATIC_GEOMETRY_VERTEX_STRIDE_BYTES + budget.STATIC_GEOMETRY_INDEX_BYTES),
        )
        self.assertEqual(budget.estimate_geometry_bytes(None), 0)

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

    def test_texture_container_risk_flags_source_formats(self) -> None:
        texture = PROJECT_ROOT / "Assets" / "ScifiFacility" / "Textures" / "sky_hdr.hdr"
        flags, recommendation = budget.classify_texture(texture, 2048, 2048, "HDR", "2048", "1", "-1", "0", "0")

        self.assertIn("HDR_TEXTURE_CONTAINER_STATIC_SUSPECT", flags)
        self.assertIn("HDR import", recommendation)

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
            estimated_geometry_bytes=budget.estimate_geometry_bytes(90000),
            lod_detected=False,
            meta_is_readable="1",
            flags=["MESH_GT_80K_ABSOLUTE_STATIC", "MESH_READ_WRITE_ENABLED_STATIC_SUSPECT"],
        )
        rt_record = budget.RenderTextureRecord(
            path=PROJECT_ROOT / "Assets" / "_Project" / "Art" / "TEXTURES" / "RT_Test.renderTexture",
            width=1280,
            height=720,
            color_format="8",
            depth_stencil_format="94",
            estimated_bytes=1280 * 720 * 8,
            flags=["RENDER_TEXTURE_DEPTH_STENCIL_PRESENT_STATIC_SUSPECT"],
        )
        rt_hit = budget.RenderTextureSourceHit(
            path=PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "DemoRt.cs",
            line=12,
            pattern="new RenderTexture",
            snippet="RenderTexture texture = new RenderTexture(width, height, 0);",
            editor_only=False,
        )

        payload = budget.build_summary_payload(PROJECT_ROOT, [texture_record], [mesh_record], [rt_record], [], "LINK_XML_MISSING", [], [rt_hit])

        self.assertEqual(payload["texture_count"], 1)
        self.assertEqual(payload["render_texture_count"], 1)
        self.assertEqual(payload["schema_version"], 1)
        self.assertIn("TEXTURE_VRAM_CRIMES", payload["gate_reasons"])
        self.assertIn("MESH_REDLINE_OR_RISK", payload["gate_reasons"])
        self.assertIn("RENDER_TEXTURE_REDLINE_OR_RISK", payload["gate_reasons"])
        self.assertIn("texture_extension_summary", payload)
        self.assertIn("mesh_extension_summary", payload)
        self.assertIn("render_textures", payload)
        self.assertIn("render_texture_source_hotspots", payload)
        self.assertEqual(payload["runtime_render_texture_source_hotspot_rows"], 1)
        self.assertIn(".codex-build", payload["skipped_directory_names"])
        self.assertEqual(payload["mesh_redline_rows"], 1)
        self.assertGreater(payload["mesh_geometry_static_estimate_mib"], 0)
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
