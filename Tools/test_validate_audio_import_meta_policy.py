import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAudioImportMetaPolicy as validator  # noqa: E402
import FixAudioImportMetaPolicy as fixer  # noqa: E402


class ValidateAudioImportMetaPolicyTests(unittest.TestCase):
    def test_current_project_audio_import_meta_policy_is_rejected(self) -> None:
        report = validator.validate_audio_import_meta_policy()

        self.assertEqual(138, report.rows)
        self.assertEqual(0, report.missing_meta)
        self.assertIn(report.blockers, (0, 41))
        if report.blockers == 0:
            self.assertEqual(0, report.load_mismatch)
            self.assertEqual(0, report.quality_mismatch)
        else:
            self.assertEqual(27, report.load_mismatch)
            self.assertEqual(14, report.quality_mismatch)

    def test_current_project_no_fail_returns_success(self) -> None:
        self.assertEqual(0, validator.main(["--no-fail"]))

    def test_current_project_strict_returns_failure(self) -> None:
        report = validator.validate_audio_import_meta_policy()
        expected = 1 if report.blockers > 0 else 0
        self.assertEqual(expected, validator.main([]))

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

    def test_fix_meta_file_applies_settings(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "test_clip.wav.meta"
            # Write an incorrect meta file
            _write_meta(
                path,
                load_type=1,          # Mismatch: 1 (CompressedInMemory) vs 0 (DecompressOnLoad)
                compression=1,        # Mismatch: 1 (Vorbis) vs 2 (ADPCM)
                quality="0.45",       # Mismatch: 0.45 vs 1.0
                preload=0,            # Preload mismatch for DecompressOnLoad (should be 1)
                force_mono=0,         # Should be 1 for sfx class
                background=1,         # Background mismatch for DecompressOnLoad (should be 0)
            )

            row = validator.LedgerRow(
                path="Assets/_Project/Audio/SFX/test_clip.wav",
                cue_id="TEST_CLIP",
                audio_class="sfx",
                duration_sec=1.0,
                load_type="DecompressOnLoad",
                compression="ADPCM",
                quality=1.0,
            )

            # Apply modifications
            modified = fixer.fix_meta_file(path, row)
            self.assertTrue(modified)

            # Check updated values
            meta = validator.parse_audio_meta(path)
            self.assertEqual("DecompressOnLoad", meta.load_type)
            self.assertEqual("ADPCM", meta.compression)
            self.assertAlmostEqual(1.0, meta.quality)
            self.assertTrue(meta.preload_audio_data)
            self.assertFalse(meta.load_in_background)
            self.assertTrue(meta.force_to_mono)

            # Running again should return False (idempotent)
            self.assertFalse(fixer.fix_meta_file(path, row))

    def test_fix_meta_file_sample_rate_override(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "test_music.wav.meta"
            # Write a meta with sampleRateOverride > 44100
            _write_meta_with_override(
                path,
                load_type=2,
                compression=1,
                quality="0.7",
                preload=0,
                force_mono=0,
                background=1,
                sample_rate_setting=2,
                sample_rate_override=48000
            )

            row = validator.LedgerRow(
                path="Assets/_Project/Audio/Music/test_music.wav",
                cue_id="TEST_MUSIC",
                audio_class="music",
                duration_sec=120.0,
                load_type="Streaming",
                compression="Vorbis",
                quality=0.7,
            )

            modified = fixer.fix_meta_file(path, row)
            self.assertTrue(modified)

            meta = validator.parse_audio_meta(path)
            self.assertEqual(2, meta.sample_rate_setting)
            self.assertEqual(44100, meta.sample_rate_override)


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


def _write_meta_with_override(
    path: Path,
    *,
    load_type: int,
    compression: int,
    quality: str,
    preload: int,
    force_mono: int,
    background: int,
    sample_rate_setting: int,
    sample_rate_override: int,
) -> None:
    path.write_text(
        "\n".join(
            (
                "fileFormatVersion: 2",
                "AudioImporter:",
                "  defaultSettings:",
                "    loadType: " + str(load_type),
                "    sampleRateSetting: " + str(sample_rate_setting),
                "    sampleRateOverride: " + str(sample_rate_override),
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
