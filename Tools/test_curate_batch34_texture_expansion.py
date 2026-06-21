import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import CurateBatch34TextureExpansion as curate  # noqa: E402

class CurateBatch34TextureExpansionTests(unittest.TestCase):
    def test_project_rel(self):
        fake_root = Path("/fake/root").resolve()
        with mock.patch("CurateBatch34TextureExpansion.ROOT", fake_root):
            path = fake_root / "Docs" / "test.txt"
            self.assertEqual(curate.project_rel(path), "Docs/test.txt")

            # test value error
            outside_path = Path("/outside/path/test.txt").resolve()
            self.assertEqual(curate.project_rel(outside_path), str(outside_path))

    def test_curation_bucket(self):
        self.assertEqual(curate.curation_bucket("CURATED_READY_STATIC"), "ReadyStatic")
        self.assertEqual(curate.curation_bucket("CURATED_READY_ALPHA_SOURCE"), "ReadyStatic")
        self.assertEqual(curate.curation_bucket("LOCAL_ONLY_STATIC"), "LocalOnly")
        self.assertEqual(curate.curation_bucket("LOCAL_ONLY_OR_REGEN_SEAMLESS"), "LocalOnly")
        self.assertEqual(curate.curation_bucket("REGEN_RECOMMENDED"), "NeedsWork")
        self.assertEqual(curate.curation_bucket("PAD_OR_SPLIT_BEFORE_IMPORT"), "NeedsWork")

    def test_default_curation(self):
        entry_ready = {"verdict": "INTAKE_READY_STATIC", "use": "Wall"}
        res = curate.default_curation(entry_ready)
        self.assertEqual(res["curationStatus"], "CURATED_READY_STATIC")
        self.assertEqual(res["targetRole"], "Wall")

        entry_not_ready = {"verdict": "INTAKE_WARNING_EDGE_ISLANDS", "use": "Decal"}
        res = curate.default_curation(entry_not_ready)
        self.assertEqual(res["curationStatus"], "REVIEW_REQUIRED_STATIC")
        self.assertEqual(res["targetRole"], "Decal")

    @mock.patch("CurateBatch34TextureExpansion.load_regen_overrides")
    def test_apply_curation(self, mock_load_regen):
        mock_load_regen.return_value = {
            "B34-9999": {"curationStatus": "REGEN_RECOMMENDED"}
        }

        entries = [
            {"id": "B34-3401", "verdict": "INTAKE_WARNING", "use": "Old role"}, # Overridden by CURATION_OVERRIDES
            {"id": "B34-9999", "verdict": "INTAKE_READY_STATIC", "use": "New role"}, # Overridden by load_regen_overrides
            {"id": "B34-1111", "verdict": "INTAKE_READY_STATIC", "use": "Default ready"}, # Default
        ]

        curated = curate.apply_curation(entries)
        self.assertEqual(len(curated), 3)

        self.assertEqual(curated[0]["id"], "B34-3401")
        self.assertEqual(curated[0]["curationStatus"], "CURATED_READY_STATIC")
        self.assertIn("detail/blend layer first", curated[0]["integrationNote"])

        self.assertEqual(curated[1]["id"], "B34-9999")
        self.assertEqual(curated[1]["curationStatus"], "REGEN_RECOMMENDED") # from regen override

        self.assertEqual(curated[2]["id"], "B34-1111")
        self.assertEqual(curated[2]["curationStatus"], "CURATED_READY_STATIC")
        self.assertEqual(curated[2]["targetRole"], "Default ready")

    def test_main_execution(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_root = Path(temp_dir).resolve()

            with mock.patch("CurateBatch34TextureExpansion.ROOT", temp_root), \
                 mock.patch("CurateBatch34TextureExpansion.OUTPUT_ROOT", temp_root / "Outputs"), \
                 mock.patch("CurateBatch34TextureExpansion.QA_DIR", temp_root / "Outputs/QA"), \
                 mock.patch("CurateBatch34TextureExpansion.CONTACT_DIR", temp_root / "Outputs/ContactSheets"), \
                 mock.patch("CurateBatch34TextureExpansion.CURATED_DIR", temp_root / "Outputs/Curated"), \
                 mock.patch("CurateBatch34TextureExpansion.MANIFEST_PATH", temp_root / "Outputs/QA/Batch34_TextureExpansion_IntakeManifest.json"), \
                 mock.patch("CurateBatch34TextureExpansion.REGEN_TARGETS_MANIFEST", temp_root / "RegenTargets/QA/Batch34_RegenTargets_IntakeManifest.json"):

                # Setup files
                curate.MANIFEST_PATH.parent.mkdir(parents=True)
                curate.REGEN_TARGETS_MANIFEST.parent.mkdir(parents=True)

                intake_data = {
                    "entries": [
                        {"id": "B34-0001", "verdict": "INTAKE_READY_STATIC", "use": "Floor", "baseColorCandidatePath": "source/1.png", "sourceType": "SEAMLESS_TILE"},
                        {"id": "B34-0002", "verdict": "INTAKE_WARNING", "use": "Decal", "baseColorCandidatePath": "source/2.png", "sourceType": "DECAL_ATLAS"}
                    ]
                }
                curate.MANIFEST_PATH.write_text(json.dumps(intake_data))

                regen_data = {
                    "entries": [
                        {"selected": True, "sourceId": "B34-0002", "finalCandidatePath": "source/2.png", "sourceType": "DECAL_ATLAS"}
                    ]
                }
                curate.REGEN_TARGETS_MANIFEST.write_text(json.dumps(regen_data))

                # Create dummy images
                source_dir = temp_root / "source"
                source_dir.mkdir()

                # Instead of writing text, write a valid small image
                from PIL import Image
                img = Image.new('RGB', (10, 10), color = 'red')
                img.save(source_dir / "1.png")
                img.save(source_dir / "2.png")

                exit_code = curate.main()
                self.assertEqual(exit_code, 0)

                # Check that outputs are created
                self.assertTrue((temp_root / "Outputs/Curated/ReadyStatic/1.png").exists())
                self.assertTrue((temp_root / "Outputs/Curated/ReadyStatic/2.png").exists())

                self.assertTrue((temp_root / "Outputs/QA/Batch34_TextureExpansion_CurationManifest.json").exists())
                self.assertTrue((temp_root / "Outputs/QA/Batch34_TextureExpansion_UnityImportQueue.csv").exists())
                self.assertTrue((temp_root / "Outputs/QA/Batch34_TextureExpansion_Curation.md").exists())
                self.assertTrue((temp_root / "Outputs/ContactSheets/Batch34_CuratedReady_Contact.png").exists())


if __name__ == "__main__":
    unittest.main()
