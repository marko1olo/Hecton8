import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAudioCriticalCueSourceCoverage as validator  # noqa: E402


class ValidateAudioCriticalCueSourceCoverageTests(unittest.TestCase):
    def test_current_project_has_explicit_placeholder_boundary(self) -> None:
        result = validator.validate_source_coverage()

        self.assertEqual(12, result.rows)
        self.assertEqual(28, result.candidate_paths)
        self.assertEqual(28, result.ledger_matches)
        self.assertEqual(2, result.missing_source_rows)
        self.assertEqual(1, result.placeholder_rows)
        self.assertEqual(0, result.blockers)
        self.assertEqual((), result.issues)

    def test_no_fail_returns_success_for_current_rejection(self) -> None:
        self.assertEqual(0, validator.main(["--no-fail"]))

    def test_strict_main_accepts_current_source_coverage(self) -> None:
        self.assertEqual(0, validator.main([]))

    def test_missing_source_sentinel_requires_missing_status(self) -> None:
        row = _cue_row(candidate_sources=validator.NO_SOURCE_SENTINEL, status="PENDING_RUNTIME_MIX_PROOF")

        result = validator.validate_rows([row], {}, {})

        self.assertEqual(1, result.blockers)
        self.assertEqual("missing_source_status", result.issues[0].category)

    def test_unknown_candidate_path_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            path = "Assets/_Project/Audio/UI/click.wav"
            (root / path).parent.mkdir(parents=True, exist_ok=True)
            (root / path).write_bytes(b"RIFF")
            row = _cue_row(candidate_sources=path)

            result = validator.validate_rows([row], {}, {}, root=root)

        self.assertEqual(1, result.blockers)
        self.assertEqual("missing_from_audio_ledger", result.issues[0].category)

    def test_placeholder_status_requires_placeholder_source(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            path = "Assets/_Project/Audio/VO/Stubs/stub.wav"
            (root / path).parent.mkdir(parents=True, exist_ok=True)
            (root / path).write_bytes(b"RIFF")
            row = _cue_row(
                candidate_sources=path,
                status="PLACEHOLDER_BLOCKED",
                missing_or_blocked="Placeholder route pending",
            )
            ledger = {path: {"placeholder_flag": "false"}}
            technical = {path: {"path": path}}

            result = validator.validate_rows([row], ledger, technical, root=root)

        self.assertEqual(1, result.blockers)
        self.assertEqual("placeholder_status_without_placeholder_source", result.issues[0].category)


def _cue_row(
    candidate_sources: str,
    status: str = "PENDING_RUNTIME_MIX_PROOF",
    missing_or_blocked: str = "No dedicated final cue source coverage",
) -> dict[str, str]:
    return {
        "CoverageId": "AUDCUE-X",
        "CandidateSources": candidate_sources,
        "Status": status,
        "MissingOrBlocked": missing_or_blocked,
    }


if __name__ == "__main__":
    unittest.main()
