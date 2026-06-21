import argparse
import io
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateBatch34SourceAtlasImporter as validator  # noqa: E402


class ValidateBatch34SourceAtlasImporterTests(unittest.TestCase):
    def setUp(self) -> None:
        self.original_root = validator.ROOT
        self.original_importer_path = validator.IMPORTER_PATH
        self.original_importer_meta_path = validator.IMPORTER_META_PATH
        self.original_apply_all_path = validator.APPLY_ALL_PATH
        self.original_unity_apply_runner_path = validator.UNITY_APPLY_RUNNER_PATH
        self.original_static_preflight_path = validator.STATIC_PREFLIGHT_PATH
        self.original_manifest_path = validator.MANIFEST_PATH
        self.original_alpha_manifest_path = validator.ALPHA_MANIFEST_PATH
        self.original_padded_manifest_path = validator.PADDED_MANIFEST_PATH
        self.original_split_manifest_path = validator.SPLIT_MANIFEST_PATH

        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)

    def tearDown(self) -> None:
        self.temp_dir.cleanup()
        validator.ROOT = self.original_root
        validator.IMPORTER_PATH = self.original_importer_path
        validator.IMPORTER_META_PATH = self.original_importer_meta_path
        validator.APPLY_ALL_PATH = self.original_apply_all_path
        validator.UNITY_APPLY_RUNNER_PATH = self.original_unity_apply_runner_path
        validator.STATIC_PREFLIGHT_PATH = self.original_static_preflight_path
        validator.MANIFEST_PATH = self.original_manifest_path
        validator.ALPHA_MANIFEST_PATH = self.original_alpha_manifest_path
        validator.PADDED_MANIFEST_PATH = self.original_padded_manifest_path
        validator.SPLIT_MANIFEST_PATH = self.original_split_manifest_path

    def _setup_paths(self) -> None:
        validator.ROOT = self.root
        validator.IMPORTER_PATH = self.root / "Batch34SourceAtlasImporter.cs"
        validator.IMPORTER_META_PATH = validator.IMPORTER_PATH.with_suffix(validator.IMPORTER_PATH.suffix + ".meta")
        validator.APPLY_ALL_PATH = self.root / "GeminiMaterialIntegrationApplier.cs"
        validator.UNITY_APPLY_RUNNER_PATH = self.root / "RunGeminiMaterialUnityApplyAll.ps1"
        validator.STATIC_PREFLIGHT_PATH = self.root / "RunGeminiMaterialStaticPreflight.ps1"
        validator.MANIFEST_PATH = self.root / "GeminiBatch34SourceAtlases_Manifest.json"
        validator.ALPHA_MANIFEST_PATH = self.root / "GeminiBatch34AlphaCandidates_Manifest.json"
        validator.PADDED_MANIFEST_PATH = self.root / "GeminiBatch34PaddedAtlasSources_Manifest.json"
        validator.SPLIT_MANIFEST_PATH = self.root / "GeminiBatch34SplitAtlasCandidates_Manifest.json"

    def test_current_project_batch34_source_atlas_importer_matches_static_contract(self) -> None:
        # Check current state using the original paths, exactly as run from CI.
        # DO NOT modify paths here
        with patch("sys.argv", ["ValidateBatch34SourceAtlasImporter.py"]):
            with patch("sys.stdout", new_callable=io.StringIO) as mock_stdout:
                result = validator.main()

        self.assertEqual(0, result, mock_stdout.getvalue())
        output = mock_stdout.getvalue()
        self.assertIn("BATCH34_SOURCE_ATLAS_IMPORTER_VALIDATOR", output)
        self.assertIn("errors=0", output)

    def test_post_apply_meta_validation_missing_meta(self) -> None:
        errors: list[str] = []
        source_path = self.root / "fake_image.png"
        source_path.parent.mkdir(parents=True, exist_ok=True)
        source_path.write_text("dummy")

        validator.validate_post_apply_meta(source_path, "test_entry", False, errors)

        self.assertEqual(1, len(errors))
        self.assertIn("missing Unity .meta after import", errors[0])

    def test_post_apply_meta_validation_missing_token(self) -> None:
        errors: list[str] = []
        source_path = self.root / "fake_image.png"
        meta_path = source_path.with_suffix(source_path.suffix + ".meta")
        source_path.write_text("dummy")
        # Missing textureCompression
        meta_path.write_text("TextureImporter:\nsRGBTexture: 1\nenableMipMap: 1", encoding="utf-8-sig")

        validator.validate_post_apply_meta(source_path, "test_entry", False, errors)

        self.assertTrue(len(errors) > 0)
        self.assertIn("test_entry: imported texture meta missing token textureCompression: 1", errors)

    def test_missing_importer_source_rejected(self) -> None:
        self._setup_paths()
        with patch("sys.argv", ["ValidateBatch34SourceAtlasImporter.py"]):
            with patch("sys.stdout", new_callable=io.StringIO) as mock_stdout:
                result = validator.main()

        self.assertEqual(1, result)
        output = mock_stdout.getvalue()
        self.assertIn("ERROR missing importer source:", output)

    def test_missing_manifest_rejected(self) -> None:
        self._setup_paths()
        validator.IMPORTER_PATH.write_text("\n".join((
            "GeminiBatch34SourceAtlases_20260608/GeminiBatch34SourceAtlases_Manifest.json",
            "GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json",
            "GeminiBatch34PaddedAtlasSources_20260608/GeminiBatch34PaddedAtlasSources_Manifest.json",
            "GeminiBatch34SplitAtlasCandidates_20260608/GeminiBatch34SplitAtlasCandidates_Manifest.json",
            "ImportAlphaCandidates(ref imported);",
            "ImportPaddedAtlases(ref imported);",
            "ImportSplitAtlasCandidates(ref imported);",
            "LoadRequiredManifest<SourceAtlasManifest>(SourceAtlasManifestPath, \"source atlas\")",
            "LoadRequiredManifest<AlphaCandidateManifest>(AlphaCandidateManifestPath, \"alpha candidate\")",
            "LoadRequiredManifest<PaddedAtlasManifest>(PaddedAtlasManifestPath, \"padded atlas\")",
            "LoadRequiredManifest<SplitAtlasManifest>(SplitAtlasManifestPath, \"split atlas candidate\")",
            "ResolveProjectFilePath(normalizedManifestPath)",
            "File.ReadAllText(projectFilePath)",
            "!IsProjectAssetPath(sourcePath)",
            "File.Exists(ResolveProjectFilePath(sourcePath))",
            "Path.GetFullPath(Path.Combine(Application.dataPath, \"..\"))",
            "throw new InvalidOperationException(\"[Batch34SourceAtlasImporter] Missing or empty source atlas manifest entries",
            "throw new InvalidOperationException(\"[Batch34SourceAtlasImporter] Missing or empty alpha candidate manifest entries",
            "throw new InvalidOperationException(\"[Batch34SourceAtlasImporter] Missing or empty padded atlas manifest entries",
            "throw new InvalidOperationException(\"[Batch34SourceAtlasImporter] Missing or empty split atlas candidate manifest entries",
            "Source atlas entry missing id at index",
            "Alpha candidate entry missing id at index",
            "Padded atlas entry missing id at index",
            "Split atlas entry missing id at index",
            "Split atlas entry missing islands at index",
            "Split atlas island count mismatch",
            "Split atlas island entry missing path",
            "Split atlas island index drift",
            "throw new InvalidOperationException($\"[Batch34SourceAtlasImporter] Missing source atlas texture for {id}: {source}\")",
            "throw new InvalidOperationException($\"[Batch34SourceAtlasImporter] Missing TextureImporter for {id}: {sourcePath}\")",
            "importer.textureType = TextureImporterType.Default",
            "importer.sRGBTexture = true",
            "importer.mipmapEnabled = true",
            "importer.wrapMode = TextureWrapMode.Clamp",
            "importer.filterMode = FilterMode.Trilinear",
            "TextureImporterCompression.CompressedHQ",
            "TextureImporterFormat.BC7",
            "TextureImporterFormat.ASTC_6x6",
            "importer.alphaSource = TextureImporterAlphaSource.FromInput",
            "importer.alphaIsTransparency = alphaIsTransparency",
            "importer.SaveAndReimport()",
        )), encoding="utf-8-sig")
        validator.IMPORTER_META_PATH.write_text("guid: fakeguid", encoding="utf-8-sig")
        validator.APPLY_ALL_PATH.write_text("Batch34SourceAtlasImporter.ImportBatch34SourceAtlases();", encoding="utf-8-sig")
        validator.UNITY_APPLY_RUNNER_PATH.write_text("Invoke-PythonValidator -ValidatorPath $batch34SourceAtlasImporterValidator -Arguments @(\"--post-apply\")", encoding="utf-8-sig")
        validator.STATIC_PREFLIGHT_PATH.write_text("ValidateBatch34SourceAtlasImporter.py", encoding="utf-8-sig")

        # Do not create MANIFEST_PATH so it reports missing
        with patch("sys.argv", ["ValidateBatch34SourceAtlasImporter.py"]):
            with patch("sys.stdout", new_callable=io.StringIO) as mock_stdout:
                result = validator.main()

        self.assertEqual(1, result)
        output = mock_stdout.getvalue()
        self.assertIn("ERROR missing source atlas manifest:", output)



    def test_post_apply_mode_runs_successfully(self) -> None:
        # Check current state using the original paths, exactly as run from CI.
        # DO NOT modify paths here
        with patch("sys.argv", ["ValidateBatch34SourceAtlasImporter.py", "--post-apply"]):
            with patch("sys.stdout", new_callable=io.StringIO) as mock_stdout:
                result = validator.main()

        self.assertEqual(0, result)
        output = mock_stdout.getvalue()
        self.assertIn("postApply=True", output)
        self.assertIn("errors=0", output)


if __name__ == "__main__":
    unittest.main()
