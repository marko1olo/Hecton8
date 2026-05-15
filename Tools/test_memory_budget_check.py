import contextlib
import csv
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import MemoryBudgetCheck as budget  # noqa: E402


PROJECT_ROOT = TOOLS_ROOT.parent


class MemoryBudgetCheckTests(unittest.TestCase):
    def write_csv_rows(self, path: Path, fieldnames, rows) -> None:
        with path.open("w", newline="", encoding="utf-8") as handle:
            writer = csv.DictWriter(handle, fieldnames=fieldnames)
            writer.writeheader()
            for row in rows:
                writer.writerow(row)

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

    def test_parallel_audits_match_serial_records_for_fixture_subset(self) -> None:
        textures = [
            PROJECT_ROOT / "Data" / "Textures" / "BlueNoise_RGBA.png",
            PROJECT_ROOT / "Assets" / "_Project" / "Art" / "Models" / "Rocks" / "Rock 7" / "Materials" / "2.jpg",
        ]
        meshes = [
            PROJECT_ROOT / "Assets" / "Dynamic Decals" / "Resources" / "Decal.obj",
            PROJECT_ROOT / "Assets" / "_Project" / "Art" / "Models" / "Rocks" / "nordic_beach_rock_vbumba2fa_mid.glb",
        ]
        render_textures = [PROJECT_ROOT / "Assets" / "_Project" / "Art" / "TEXTURES" / "RT_HUD_Display.renderTexture"]

        serial_textures = budget.audit_textures(textures, PROJECT_ROOT, workers=1)
        parallel_textures = budget.audit_textures(textures, PROJECT_ROOT, workers=2)
        serial_meshes = budget.audit_meshes(meshes, workers=1)
        parallel_meshes = budget.audit_meshes(meshes, workers=2)
        serial_rts = budget.audit_render_textures(render_textures, workers=1)
        parallel_rts = budget.audit_render_textures(render_textures, workers=2)

        self.assertEqual(
            [(item.path, item.width, item.height, item.mode, item.bc7_bytes, item.flags) for item in serial_textures],
            [(item.path, item.width, item.height, item.mode, item.bc7_bytes, item.flags) for item in parallel_textures],
        )
        self.assertEqual(
            [(item.path, item.triangles, item.estimated_geometry_bytes, item.flags) for item in serial_meshes],
            [(item.path, item.triangles, item.estimated_geometry_bytes, item.flags) for item in parallel_meshes],
        )
        self.assertEqual(
            [(item.path, item.width, item.height, item.estimated_bytes, item.flags) for item in serial_rts],
            [(item.path, item.width, item.height, item.estimated_bytes, item.flags) for item in parallel_rts],
        )
        self.assertEqual(budget.normalize_worker_count(0), budget.DEFAULT_AUDIT_WORKERS)
        self.assertEqual(budget.normalize_worker_count(9999), budget.MAX_AUDIT_WORKERS)

    def test_render_texture_source_hotspots_find_runtime_allocations(self) -> None:
        source = PROJECT_ROOT / "Assets" / "_Project" / "Scripts" / "World" / "Biolum" / "HectonBiolumDiffusionVolume.cs"

        hits = budget.find_render_texture_source_hotspots_in_paths(PROJECT_ROOT, [source])

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
        self.assertIn("texture_redlines", payload)
        self.assertIn("render_textures", payload)
        self.assertIn("render_texture_source_hotspots", payload)
        self.assertEqual(payload["runtime_render_texture_source_hotspot_rows"], 1)
        self.assertEqual(payload["texture_flagged_rows"], 1)
        self.assertEqual(payload["texture_redlines"][0]["path"], "Assets/_Project/Art/TEXTURES/TX_split.png")
        self.assertIn(".codex-build", payload["skipped_directory_names"])
        self.assertEqual(payload["mesh_redline_rows"], 1)
        self.assertGreater(payload["mesh_geometry_static_estimate_mib"], 0)
        self.assertEqual(payload["mesh_import_risk_rows"], 1)
        self.assertEqual(payload["mesh_read_write_enabled_rows"], 1)
        self.assertEqual(payload["first_party_mesh_import_risk_rows"], 0)
        self.assertFalse(payload["critical_vram_overflow"])
        self.assertEqual(payload["ci_expected_exit_code"], 2)

    def test_generated_reports_match_import_root_scope_and_counts(self) -> None:
        json_path = PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.json"
        csv_path = PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.csv"

        payload = json.loads(json_path.read_text(encoding="utf-8"))
        with csv_path.open(newline="", encoding="utf-8") as handle:
            rows = list(csv.DictReader(handle))

        texture_rows = [row for row in rows if row["asset_type"] == "texture"]
        mesh_rows = [row for row in rows if row["asset_type"] == "mesh"]
        render_texture_rows = [row for row in rows if row["asset_type"] == "render_texture"]
        allowed_prefixes = ("Assets/", "Packages/", "Data/")

        self.assertEqual(payload["texture_count"], len(texture_rows))
        self.assertEqual(payload["mesh_count"], len(mesh_rows))
        self.assertEqual(payload["render_texture_count"], len(render_texture_rows))
        self.assertEqual(payload["resolved_scan_roots"], ["Assets", "Packages", "Data"])
        self.assertTrue(all(row["path"].startswith(allowed_prefixes) for row in texture_rows))
        self.assertTrue(all(row["path"].startswith(allowed_prefixes) for row in mesh_rows))
        self.assertTrue(all(row["path"].startswith(allowed_prefixes) for row in render_texture_rows))
        self.assertFalse(any(row["path"].startswith("Docs/") for row in texture_rows))
        self.assertFalse(any("_agent_screen_capture" in row["path"] for row in texture_rows))

    def test_validate_reports_cli_path_accepts_current_generated_reports(self) -> None:
        ok, messages = budget.validate_generated_reports(
            PROJECT_ROOT,
            PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.csv",
            PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.json",
            PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Texture_Redlines.csv",
            PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Mesh_Redlines.csv",
            PROJECT_ROOT / "Docs" / "Reports" / "VRAM_RenderTexture_Redlines.csv",
            PROJECT_ROOT / "Docs" / "Reports" / "VRAM_RenderTexture_SourceHotspots.csv",
            PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit_Summary.md",
            PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Remediation_Plan.md",
        )

        self.assertTrue(ok, messages)
        self.assertIn("reports valid", messages[0])
        payload = json.loads((PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.json").read_text(encoding="utf-8"))
        self.assertIn(f"rt_hotspots={payload['render_texture_source_hotspot_rows']}", messages[0])
        buffer = io.StringIO()
        with contextlib.redirect_stdout(buffer):
            exit_code = budget.main(["--root", str(PROJECT_ROOT), "--validate-reports"])
        self.assertEqual(
            exit_code,
            0,
        )
        self.assertIn("reports valid", buffer.getvalue())

    def test_validate_reports_rejects_json_split_payload_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            (root / "Assets").mkdir()
            broad_csv = root / "audit.csv"
            json_path = root / "audit.json"
            mesh_redlines = root / "mesh_redlines.csv"
            rt_redlines = root / "rt_redlines.csv"

            broad_mesh_row = {column: "" for column in budget.BROAD_REPORT_COLUMNS}
            broad_mesh_row.update(
                {
                    "asset_type": "mesh",
                    "path": "Assets/BigMesh.fbx",
                    "extension": ".fbx",
                    "redline_flags": "MESH_GT_80K_ABSOLUTE_STATIC",
                    "evidence_class": "STATIC_SOURCE",
                }
            )
            broad_rt_row = {column: "" for column in budget.BROAD_REPORT_COLUMNS}
            broad_rt_row.update(
                {
                    "asset_type": "render_texture",
                    "path": "Assets/RT_Test.renderTexture",
                    "extension": ".rendertexture",
                    "width": "1280",
                    "height": "720",
                    "rt_estimate_mib": "7.031",
                    "redline_flags": "RENDER_TEXTURE_DEPTH_STENCIL_PRESENT_STATIC_SUSPECT",
                    "evidence_class": "STATIC_SOURCE",
                }
            )
            self.write_csv_rows(broad_csv, budget.BROAD_REPORT_COLUMNS, [broad_mesh_row, broad_rt_row])
            self.write_csv_rows(
                mesh_redlines,
                budget.MESH_REDLINE_COLUMNS,
                [
                    {
                        "path": "Assets/BigMesh.fbx",
                        "file_mib": "",
                        "triangles": "",
                        "geometry_estimate_mib": "",
                        "lod_detected": "",
                        "meta_is_readable": "",
                        "meta_mesh_compression": "",
                        "meta_optimize_mesh": "",
                        "meta_import_blend_shapes": "",
                        "meta_add_colliders": "",
                        "meta_generate_secondary_uv": "",
                        "meta_keep_quads": "",
                        "flags": "MESH_GT_80K_ABSOLUTE_STATIC",
                        "recommendation": "",
                    }
                ],
            )
            self.write_csv_rows(
                rt_redlines,
                budget.RENDER_TEXTURE_REDLINE_COLUMNS,
                [
                    {
                        "path": "Assets/RT_Test.renderTexture",
                        "width": "1280",
                        "height": "720",
                        "estimate_mib": "7.031",
                        "color_format": "",
                        "depth_stencil_format": "",
                        "anti_aliasing": "",
                        "mipmap": "",
                        "random_write": "",
                        "flags": "RENDER_TEXTURE_DEPTH_STENCIL_PRESENT_STATIC_SUSPECT",
                        "recommendation": "",
                    }
                ],
            )
            payload = {
                "schema_version": 1,
                "evidence_class": "STATIC_SOURCE/FILESYSTEM/PY_UNIT_TEST",
                "scan_root_names": list(budget.DEFAULT_SCAN_ROOT_NAMES),
                "ci_expected_exit_code": 0,
                "texture_count": 0,
                "mesh_count": 1,
                "render_texture_count": 1,
                "resolved_scan_roots": ["Assets"],
                "texture_flagged_rows": 0,
                "mesh_redline_rows": 1,
                "render_texture_redline_rows": 1,
                "critical_vram_overflow": False,
                "gate_reasons": [],
                "mesh_redlines": [
                    {
                        "path": "Assets/BigMesh.fbx",
                        "flags": ["MESH_GT_80K_ABSOLUTE_STATIC"],
                    },
                ],
                "render_textures": [
                    {
                        "path": "Assets/RT_Test.renderTexture",
                        "width": 1280,
                        "height": 720,
                        "estimate_mib": 7.031,
                        "flags": ["RENDER_TEXTURE_DEPTH_STENCIL_PRESENT_STATIC_SUSPECT"],
                    },
                ],
            }
            json_path.write_text(json.dumps(payload), encoding="utf-8")

            ok, messages = budget.validate_generated_reports(
                root,
                broad_csv,
                json_path,
                None,
                mesh_redlines,
                rt_redlines,
            )
            self.assertTrue(ok, messages)

            payload["mesh_redlines"][0]["flags"] = ["STALE_FLAG"]
            json_path.write_text(json.dumps(payload), encoding="utf-8")
            ok, messages = budget.validate_generated_reports(
                root,
                broad_csv,
                json_path,
                None,
                mesh_redlines,
                rt_redlines,
            )
            self.assertFalse(ok)
            self.assertIn("mesh redline flags mismatch JSON", messages)

            payload["mesh_redlines"][0]["flags"] = ["MESH_GT_80K_ABSOLUTE_STATIC"]
            payload["render_textures"][0]["estimate_mib"] = 9.0
            json_path.write_text(json.dumps(payload), encoding="utf-8")
            ok, messages = budget.validate_generated_reports(
                root,
                broad_csv,
                json_path,
                None,
                mesh_redlines,
                rt_redlines,
            )
            self.assertFalse(ok)
            self.assertIn("RenderTexture dimensions/estimate mismatch JSON", messages)

    def test_validate_reports_rejects_texture_json_split_payload_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            (root / "Assets" / "_Project").mkdir(parents=True)
            broad_csv = root / "audit.csv"
            json_path = root / "audit.json"
            texture_redlines = root / "texture_redlines.csv"

            broad_texture_row = {column: "" for column in budget.BROAD_REPORT_COLUMNS}
            broad_texture_row.update(
                {
                    "asset_type": "texture",
                    "path": "Assets/_Project/TX_Test.png",
                    "extension": ".png",
                    "width": "4096",
                    "height": "2048",
                    "redline_flags": "VRAM CRIME: TEXTURE_GT_2048",
                    "evidence_class": "STATIC_SOURCE",
                }
            )
            self.write_csv_rows(broad_csv, budget.BROAD_REPORT_COLUMNS, [broad_texture_row])
            self.write_csv_rows(
                texture_redlines,
                budget.TEXTURE_REDLINE_COLUMNS,
                [
                    {
                        "path": "Assets/_Project/TX_Test.png",
                        "width": "4096",
                        "height": "2048",
                        "bc7_full_mip_mib": "42.667",
                        "first_party_production": "true",
                        "flags": "VRAM CRIME: TEXTURE_GT_2048",
                        "recommendation": "Clamp import cap.",
                    }
                ],
            )
            payload = {
                "schema_version": 1,
                "evidence_class": "STATIC_SOURCE/FILESYSTEM/PY_UNIT_TEST",
                "scan_root_names": list(budget.DEFAULT_SCAN_ROOT_NAMES),
                "ci_expected_exit_code": 0,
                "texture_count": 1,
                "mesh_count": 0,
                "render_texture_count": 0,
                "resolved_scan_roots": ["Assets"],
                "texture_flagged_rows": 1,
                "mesh_redline_rows": 0,
                "render_texture_redline_rows": 0,
                "render_texture_source_hotspot_rows": 0,
                "runtime_render_texture_source_hotspot_rows": 0,
                "critical_vram_overflow": False,
                "gate_reasons": [],
                "texture_redlines": [
                    {
                        "path": "Assets/_Project/TX_Test.png",
                        "width": 4096,
                        "height": 2048,
                        "bc7_full_mip_mib": 42.667,
                        "first_party_production": True,
                        "flags": ["VRAM CRIME: TEXTURE_GT_2048"],
                    }
                ],
                "mesh_redlines": [],
                "render_textures": [],
                "render_texture_source_hotspots": [],
            }
            json_path.write_text(json.dumps(payload), encoding="utf-8")

            ok, messages = budget.validate_generated_reports(
                root,
                broad_csv,
                json_path,
                texture_redlines,
                None,
                None,
            )
            self.assertTrue(ok, messages)

            payload["texture_redlines"][0]["flags"] = ["STALE_FLAG"]
            json_path.write_text(json.dumps(payload), encoding="utf-8")
            ok, messages = budget.validate_generated_reports(
                root,
                broad_csv,
                json_path,
                texture_redlines,
                None,
                None,
            )
            self.assertFalse(ok)
            self.assertIn("texture redline flags mismatch JSON", messages)

            payload["texture_redlines"][0]["flags"] = ["VRAM CRIME: TEXTURE_GT_2048"]
            payload["texture_redlines"][0]["bc7_full_mip_mib"] = 40.0
            json_path.write_text(json.dumps(payload), encoding="utf-8")
            ok, messages = budget.validate_generated_reports(
                root,
                broad_csv,
                json_path,
                texture_redlines,
                None,
                None,
            )
            self.assertFalse(ok)
            self.assertIn("texture redline dimensions/estimate mismatch JSON", messages)

    def test_validate_reports_rejects_broad_csv_schema_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            drifted_csv = Path(temp_dir) / "VRAM_Budget_Audit_schema_drift.csv"
            source_csv = PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.csv"
            lines = source_csv.read_text(encoding="utf-8").splitlines()
            lines[0] = lines[0].replace(",evidence_class", "")
            drifted_csv.write_text("\n".join(lines) + "\n", encoding="utf-8")

            ok, messages = budget.validate_generated_reports(
                PROJECT_ROOT,
                drifted_csv,
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.json",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Texture_Redlines.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Mesh_Redlines.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_RenderTexture_Redlines.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_RenderTexture_SourceHotspots.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit_Summary.md",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Remediation_Plan.md",
            )

            self.assertFalse(ok)
            self.assertIn("CSV report schema drift", messages)

    def test_validate_reports_rejects_evidence_class_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            drifted_csv = Path(temp_dir) / "VRAM_Budget_Audit_evidence_drift.csv"
            source_csv = PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.csv"
            with source_csv.open(newline="", encoding="utf-8") as handle:
                reader = csv.DictReader(handle)
                rows = list(reader)
                fieldnames = reader.fieldnames or []
            rows[0]["evidence_class"] = "RUNTIME_CLAIM_WITHOUT_PROFILER"
            self.write_csv_rows(drifted_csv, fieldnames, rows)

            ok, messages = budget.validate_generated_reports(
                PROJECT_ROOT,
                drifted_csv,
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.json",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Texture_Redlines.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Mesh_Redlines.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_RenderTexture_Redlines.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_RenderTexture_SourceHotspots.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit_Summary.md",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Remediation_Plan.md",
            )

            self.assertFalse(ok)
            self.assertIn("CSV report evidence_class drift", messages)

    def test_validate_reports_rejects_json_authority_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            drifted_json = Path(temp_dir) / "VRAM_Budget_Audit_authority_drift.json"
            payload = json.loads((PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.json").read_text(encoding="utf-8"))
            payload["evidence_class"] = "RUNTIME_MEMORY_PROFILER_CLAIM_WITHOUT_CAPTURE"
            payload["ci_expected_exit_code"] = 0
            drifted_json.write_text(json.dumps(payload), encoding="utf-8")

            ok, messages = budget.validate_generated_reports(
                PROJECT_ROOT,
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.csv",
                drifted_json,
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Texture_Redlines.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Mesh_Redlines.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_RenderTexture_Redlines.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_RenderTexture_SourceHotspots.csv",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit_Summary.md",
                PROJECT_ROOT / "Docs" / "Reports" / "VRAM_Remediation_Plan.md",
            )

            self.assertFalse(ok)
            self.assertIn("JSON evidence_class drift", messages)
            self.assertIn("JSON ci_expected_exit_code drift", messages)

    def test_iter_assets_uses_case_insensitive_generated_tree_exclusion(self) -> None:
        self.assertIn(".codex-build", budget.SKIP_DIRS)
        self.assertIn(".codex-artifacts", budget.SKIP_DIRS)
        self.assertIn(".codex-build", budget.SKIP_DIR_NAMES_LOWER)
        self.assertIn("library", budget.SKIP_DIR_NAMES_LOWER)
        textures, meshes, render_textures, link_xml_paths = budget.iter_asset_and_link_paths(PROJECT_ROOT)
        self.assertGreater(len(textures), 0)
        self.assertGreater(len(meshes), 0)
        self.assertGreater(len(render_textures), 0)
        self.assertTrue(all(path.name.lower() == "link.xml" for path in link_xml_paths))
        self.assertGreater(len(link_xml_paths), 0)


if __name__ == "__main__":
    unittest.main()
