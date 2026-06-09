import sys
import os
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAudioImportMetaPolicy as validator  # noqa: E402
import FixAudioImportMetaPolicy

class ValidateAudioImportMetaPolicyTests(unittest.TestCase):
    def test_current_project_audio_import_meta_policy_is_rejected(self) -> None:
        report = validator.validate_audio_import_meta_policy()

        self.assertEqual(138, report.rows)
        self.assertEqual(0, report.missing_meta)
        self.assertEqual(27, report.load_mismatch)
        self.assertEqual(14, report.quality_mismatch)
        self.assertEqual(41, report.blockers)

    def test_current_project_no_fail_returns_success(self) -> None:
        self.assertEqual(0, validator.main(["--no-fail"]))

    def test_current_project_strict_returns_failure(self) -> None:
        self.assertEqual(1, validator.main([]))

    def test_parse_audio_meta_maps_unity_enums(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "clip.wav.meta"
            _write_meta(path, load_type=0, compression=2, quality="1", preload=1, force_mono=1, background=0)

            meta = validator.parse_audio_meta(path)

        self.assertEqual("DecompressOnLoad", meta.load_type)
        self.assertEqual("ADPCM", meta.compression)
        self.assertTrue(meta.preload_audio_data)
        self.assertFalse(meta.load_in_background)

    def test_preload_policy_rejects_decompress_background_load(self) -> None:
        meta = validator.AudioMeta(
            load_type="DecompressOnLoad",
            sample_rate_setting=2,
            sample_rate_override=22050,
            compression="ADPCM",
            quality=1.0,
            preload_audio_data=False,
            force_to_mono=True,
            load_in_background=True,
            ambisonic=False,
        )

        self.assertTrue(validator.preload_policy_mismatch(meta))

    def test_sample_rate_policy_rejects_sfx_above_22050(self) -> None:
        row = validator.LedgerRow(
            path="Assets/_Project/Audio/SFX/test.wav",
            cue_id="TEST",
            audio_class="sfx",
            duration_sec=1.0,
            load_type="DecompressOnLoad",
            compression="ADPCM",
            quality=1.0,
        )
        meta = validator.AudioMeta(
            load_type="DecompressOnLoad",
            sample_rate_setting=2,
            sample_rate_override=48000,
            compression="ADPCM",
            quality=1.0,
            preload_audio_data=True,
            force_to_mono=True,
            load_in_background=False,
            ambisonic=False,
        )

        self.assertTrue(validator.sample_rate_policy_mismatch(row, meta))

    def test_fix_audio_meta_vorbis_q70(self) -> None:
        with tempfile.NamedTemporaryFile(mode='w', suffix='.meta', delete=False) as f:
            f.write("""fileFormatVersion: 2
guid: 123456789
AudioImporter:
  forceToMono: 0
  defaultSettings:
    compressionFormat: 0
    quality: 1
""")
            filepath = Path(f.name)

        audio_info = {
            filepath.name[:-5]: {"duration_sec": "3.0", "audio_class": "music"}
        }

        fixed = FixAudioImportMetaPolicy.parse_and_fix_meta(filepath, audio_info)
        self.assertTrue(fixed)

        content = filepath.read_text()
        self.assertIn("compressionFormat: 1", content)
        self.assertIn("quality: 0.7", content)

        os.unlink(filepath)

    def test_fix_audio_meta_adpcm_short_sfx(self) -> None:
        with tempfile.NamedTemporaryFile(mode='w', suffix='.meta', delete=False) as f:
            f.write("""fileFormatVersion: 2
guid: 123456789
AudioImporter:
  forceToMono: 0
  defaultSettings:
    compressionFormat: 0
    quality: 1
""")
            filepath = Path(f.name)

        audio_info = {
            filepath.name[:-5]: {"duration_sec": "1.0", "audio_class": "sfx"}
        }

        fixed = FixAudioImportMetaPolicy.parse_and_fix_meta(filepath, audio_info)
        self.assertTrue(fixed)

        content = filepath.read_text()
        self.assertIn("forceToMono: 1", content)
        self.assertIn("compressionFormat: 2", content)

        os.unlink(filepath)

def _write_meta(
    path: Path,
    *,
    load_type: int,
    compression: int,
    quality: str,
    preload: int,
    force_mono: int,
    background: int,
) -> None:
    path.write_text(
        "\n".join(
            (
                "fileFormatVersion: 2",
                "AudioImporter:",
                "  defaultSettings:",
                "    loadType: " + str(load_type),
                "    sampleRateSetting: 2",
                "    sampleRateOverride: 22050",
                "    compressionFormat: " + str(compression),
                "    quality: " + quality,
                "    preloadAudioData: " + str(preload),
                "  forceToMono: " + str(force_mono),
                "  loadInBackground: " + str(background),
                "  ambisonic: 0",
            )
        ),
        encoding="utf-8",
    )


if __name__ == "__main__":
    unittest.main()
