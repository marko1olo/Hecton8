import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAudioWaveformProofArtifacts as validator  # noqa: E402


class ValidateAudioWaveformProofArtifactsTests(unittest.TestCase):
    def test_current_project_waveform_artifacts_match_static_contract(self) -> None:
        stats = validator.validate_audio_waveform_proof_artifacts()

        self.assertEqual(11, len(stats))

    def test_missing_preview_is_rejected(self) -> None:
        stats = validator.parse_stats(validator.load_csv(validator.STATS_PATH, validator.REQUIRED_STATS_COLUMNS))
        first = stats[0]
        edited = validator.WaveformStat(
            path=first.path,
            preview_png="Docs/AssetAudit/AudioVisual/DOES_NOT_EXIST.png",
            peak_dbfs=first.peak_dbfs,
            rms_dbfs=first.rms_dbfs,
            preview_samples=first.preview_samples,
        )

        with self.assertRaises(SystemExit):
            validator.validate_stats([edited] + stats[1:])

    def test_vo_stub_must_remain_placeholder_blocked(self) -> None:
        stats = validator.parse_stats(validator.load_csv(validator.STATS_PATH, validator.REQUIRED_STATS_COLUMNS))
        queue_rows = validator.load_csv(validator.LISTENING_QUEUE_PATH, validator.REQUIRED_QUEUE_COLUMNS)
        for row in queue_rows:
            if row["asset_or_config"].strip().endswith("VOStub_Chen_Log01_EN.wav"):
                row["status"] = "PENDING_VERIFICATION"
                break

        with self.assertRaises(SystemExit):
            validator.validate_listening_links(stats, queue_rows)

    def test_missing_proof_index_preview_is_rejected(self) -> None:
        stats = validator.parse_stats(validator.load_csv(validator.STATS_PATH, validator.REQUIRED_STATS_COLUMNS))
        proof_rows = validator.load_csv(validator.PROOF_INDEX_PATH, validator.REQUIRED_PROOF_COLUMNS)
        preview_to_remove = stats[0].preview_png
        proof_rows = [row for row in proof_rows if row["ArtifactPath"].strip().replace("\\", "/") != preview_to_remove]

        with self.assertRaises(SystemExit):
            validator.validate_proof_index_links(stats, proof_rows)

    def test_nonnumeric_waveform_metric_is_rejected(self) -> None:
        rows = validator.load_csv(validator.STATS_PATH, validator.REQUIRED_STATS_COLUMNS)
        rows[0]["peak_dbfs"] = "not-a-number"

        with self.assertRaises(SystemExit):
            validator.parse_stats(rows)


if __name__ == "__main__":
    unittest.main()
