import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image

TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateBatch34PaddedAtlasSources as validator  # noqa: E402


class ValidateBatch34PaddedAtlasSourcesTests(unittest.TestCase):
    def setUp(self) -> None:
        self.original_root = validator.ROOT
        self.original_manifest = validator.MANIFEST
        self.original_curation_manifest = validator.CURATION_MANIFEST
        self.temp_dir = tempfile.mkdtemp()
        self.temp_root = Path(self.temp_dir)

        validator.ROOT = self.temp_root
        validator.MANIFEST = self.temp_root / "Manifest.json"
        validator.CURATION_MANIFEST = self.temp_root / "CurationManifest.json"

    def tearDown(self) -> None:
        validator.ROOT = self.original_root
        validator.MANIFEST = self.original_manifest
        validator.CURATION_MANIFEST = self.original_curation_manifest
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def write_json(self, path: Path, data: dict) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data), encoding="utf-8-sig")

    def create_valid_environment(self) -> None:
        # Create curation manifest
        curation_entries = []
        for expected_id in validator.EXPECTED_IDS:
            curation_entries.append({
                "id": expected_id,
                "title": f"Title {expected_id}",
                "sourceType": "SEAMLESS_TILE",
                "family": "Family",
                "curationStatus": "PAD_OR_SPLIT_BEFORE_IMPORT",
                "baseColorCandidatePath": f"Raw/{expected_id}.png"
            })

        curation_data = {
            "schema": "hecton8.batch34.texture_expansion_curation.v1",
            "entries": curation_entries
        }
        self.write_json(validator.CURATION_MANIFEST, curation_data)

        # Create padded manifest
        padded_entries = []
        for expected_id in validator.EXPECTED_IDS:
            rel_path = f"Padded/{expected_id}.png"

            # Create valid image
            img_path = self.temp_root / rel_path
            img_path.parent.mkdir(parents=True, exist_ok=True)

            # Create a 1536x1536 image with solid alpha in the center, transparent at the edges
            # to keep edge pct 0 and alpha pct > 0
            img = Image.new("RGBA", (validator.EXPECTED_SIZE, validator.EXPECTED_SIZE), (0, 0, 0, 0))
            # Just fill a center box with solid alpha
            import PIL.ImageDraw as ImageDraw
            draw = ImageDraw.Draw(img)
            # Make sure it's away from the 16-pixel edge
            draw.rectangle([20, 20, 1516, 1516], fill=(255, 255, 255, 255))
            img.save(img_path)

            # Calculate what the script would calculate
            alpha = np.array(img.convert("RGBA").getchannel("A"))
            nonzero_pct = float(np.count_nonzero(alpha)) * 100.0 / max(1, alpha.size)

            edge = np.concatenate([alpha[:16, :].ravel(), alpha[-16:, :].ravel(), alpha[:, :16].ravel(), alpha[:, -16:].ravel()])
            edge_pct = float(np.count_nonzero(edge)) * 100.0 / max(1, edge.size)

            padded_entries.append({
                "id": expected_id,
                "title": f"Title {expected_id}",
                "sourceType": "SEAMLESS_TILE",
                "family": "Family",
                "sourceCurationStatus": "PAD_OR_SPLIT_BEFORE_IMPORT",
                "source": f"Raw/{expected_id}.png",
                "paddedAtlas": rel_path,
                "alphaNonZeroPct": nonzero_pct,
                "edgeAlphaNonZeroPct": edge_pct,
                "productionBindingStatus": "PADDED_SOURCE_ATLAS_PENDING_UV_BINDING"
            })

        # Create a preview file
        preview_path = self.temp_root / "Preview.png"
        preview_path.write_bytes(b"fake_image_data")

        padded_data = {
            "schema": "hecton8.batch34.padded_atlas_sources.v1",
            "canvasSize": validator.EXPECTED_SIZE,
            "productionBindingStatus": "PADDED_SOURCE_ATLAS_PENDING_UV_BINDING",
            "sourceCurationManifest": validator.display(validator.CURATION_MANIFEST),
            "preview": "Preview.png",
            "entries": padded_entries
        }
        self.write_json(validator.MANIFEST, padded_data)

    def test_valid_environment_passes(self) -> None:
        self.create_valid_environment()
        exit_code = validator.main()
        self.assertEqual(0, exit_code)


    def test_missing_manifest_fails(self) -> None:
        exit_code = validator.main()
        self.assertEqual(1, exit_code)

    def test_missing_curation_manifest_fails(self) -> None:
        self.create_valid_environment()
        validator.CURATION_MANIFEST.unlink()
        exit_code = validator.main()
        self.assertEqual(1, exit_code)

    def test_curation_mismatch_fails(self) -> None:
        self.create_valid_environment()
        curation_data = json.loads(validator.CURATION_MANIFEST.read_text(encoding="utf-8-sig"))
        curation_data["entries"][0]["curationStatus"] = "REJECTED"
        self.write_json(validator.CURATION_MANIFEST, curation_data)
        exit_code = validator.main()
        self.assertEqual(1, exit_code)

    def test_missing_expected_id_fails(self) -> None:
        self.create_valid_environment()
        padded_data = json.loads(validator.MANIFEST.read_text(encoding="utf-8-sig"))
        padded_data["entries"] = padded_data["entries"][1:]
        self.write_json(validator.MANIFEST, padded_data)
        exit_code = validator.main()
        self.assertEqual(1, exit_code)

    def test_unexpected_id_fails(self) -> None:
        self.create_valid_environment()
        padded_data = json.loads(validator.MANIFEST.read_text(encoding="utf-8-sig"))
        padded_data["entries"].append(padded_data["entries"][0].copy())
        padded_data["entries"][-1]["id"] = "B34-9999"
        self.write_json(validator.MANIFEST, padded_data)
        exit_code = validator.main()
        self.assertEqual(1, exit_code)

    def test_image_wrong_size_fails(self) -> None:
        self.create_valid_environment()
        img_path = self.temp_root / f"Padded/{list(validator.EXPECTED_IDS)[0]}.png"
        img = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
        img.save(img_path)
        exit_code = validator.main()
        self.assertEqual(1, exit_code)

    def test_image_wrong_mode_fails(self) -> None:
        self.create_valid_environment()
        img_path = self.temp_root / f"Padded/{list(validator.EXPECTED_IDS)[0]}.png"
        img = Image.new("RGB", (validator.EXPECTED_SIZE, validator.EXPECTED_SIZE), (0, 0, 0))
        img.save(img_path)
        exit_code = validator.main()
        self.assertEqual(1, exit_code)

    def test_alpha_pct_mismatch_fails(self) -> None:
        self.create_valid_environment()
        padded_data = json.loads(validator.MANIFEST.read_text(encoding="utf-8-sig"))
        padded_data["entries"][0]["alphaNonZeroPct"] += 5.0
        self.write_json(validator.MANIFEST, padded_data)
        exit_code = validator.main()
        self.assertEqual(1, exit_code)

    def test_edge_alpha_mismatch_fails(self) -> None:
        self.create_valid_environment()
        padded_data = json.loads(validator.MANIFEST.read_text(encoding="utf-8-sig"))
        padded_data["entries"][0]["edgeAlphaNonZeroPct"] += 5.0
        self.write_json(validator.MANIFEST, padded_data)
        exit_code = validator.main()
        self.assertEqual(1, exit_code)

    def test_sparse_alpha_fails(self) -> None:
        self.create_valid_environment()
        img_path = self.temp_root / f"Padded/{list(validator.EXPECTED_IDS)[0]}.png"
        # Create an almost empty image to trigger "alpha too sparse"
        img = Image.new("RGBA", (validator.EXPECTED_SIZE, validator.EXPECTED_SIZE), (0, 0, 0, 0))
        import PIL.ImageDraw as ImageDraw
        draw = ImageDraw.Draw(img)
        draw.rectangle([700, 700, 701, 701], fill=(255, 255, 255, 255))
        img.save(img_path)

        # update manifest to reflect new alpha so we don't get the mismatch error
        alpha = np.array(img.convert("RGBA").getchannel("A"))
        nonzero_pct = float(np.count_nonzero(alpha)) * 100.0 / max(1, alpha.size)
        edge = np.concatenate([alpha[:16, :].ravel(), alpha[-16:, :].ravel(), alpha[:, :16].ravel(), alpha[:, -16:].ravel()])
        edge_pct = float(np.count_nonzero(edge)) * 100.0 / max(1, edge.size)

        padded_data = json.loads(validator.MANIFEST.read_text(encoding="utf-8-sig"))
        for entry in padded_data["entries"]:
            if entry["id"] == list(validator.EXPECTED_IDS)[0]:
                entry["alphaNonZeroPct"] = nonzero_pct
                entry["edgeAlphaNonZeroPct"] = edge_pct
        self.write_json(validator.MANIFEST, padded_data)

        exit_code = validator.main()
        self.assertEqual(1, exit_code)

    def test_non_transparent_edge_fails(self) -> None:
        self.create_valid_environment()
        img_path = self.temp_root / f"Padded/{list(validator.EXPECTED_IDS)[0]}.png"

        # create valid image but with non-transparent edge
        img = Image.new("RGBA", (validator.EXPECTED_SIZE, validator.EXPECTED_SIZE), (0, 0, 0, 0))
        import PIL.ImageDraw as ImageDraw
        draw = ImageDraw.Draw(img)
        draw.rectangle([20, 20, 1516, 1516], fill=(255, 255, 255, 255))
        draw.rectangle([0, 0, 5, 5], fill=(255, 255, 255, 255)) # non-transparent edge
        img.save(img_path)

        # update manifest to reflect new alpha
        alpha = np.array(img.convert("RGBA").getchannel("A"))
        nonzero_pct = float(np.count_nonzero(alpha)) * 100.0 / max(1, alpha.size)
        edge = np.concatenate([alpha[:16, :].ravel(), alpha[-16:, :].ravel(), alpha[:, :16].ravel(), alpha[:, -16:].ravel()])
        edge_pct = float(np.count_nonzero(edge)) * 100.0 / max(1, edge.size)

        padded_data = json.loads(validator.MANIFEST.read_text(encoding="utf-8-sig"))
        for entry in padded_data["entries"]:
            if entry["id"] == list(validator.EXPECTED_IDS)[0]:
                entry["alphaNonZeroPct"] = nonzero_pct
                entry["edgeAlphaNonZeroPct"] = edge_pct
        self.write_json(validator.MANIFEST, padded_data)

        exit_code = validator.main()
        self.assertEqual(1, exit_code)

if __name__ == "__main__":
    unittest.main()
