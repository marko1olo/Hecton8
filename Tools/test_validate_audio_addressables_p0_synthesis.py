import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import ValidateAudioAddressablesP0Synthesis as validator  # noqa: E402
from ValidateAudioSceneStaticRoute import AudioSceneStaticReport  # noqa: E402


class ValidateAudioAddressablesP0SynthesisTests(unittest.TestCase):
    def test_current_project_audio_addressables_synthesis_matches_static_route(self) -> None:
        report = validator.validate_audio_addressables_p0_synthesis()

        self.assertEqual(1, len(report.blockers))
        self.assertEqual(0, report.addressable_settings)
        self.assertEqual(0, report.addressable_groups)
        self.assertEqual(0, report.addressable_entries)

    def test_stale_blocker_count_is_rejected(self) -> None:
        report = validator.validate_audio_scene_static_route(validator.ROOT)
        text = validator.load_text().replace("AUDIO_SCENE_STATIC_ROUTE_REJECTED blockers=1", "AUDIO_SCENE_STATIC_ROUTE_REJECTED blockers=2")

        with self.assertRaises(SystemExit):
            validator.validate_document_text(text, report)

    def test_stale_player_direct_ref_count_is_rejected(self) -> None:
        report = validator.validate_audio_scene_static_route(validator.ROOT)
        text = validator.load_text().replace(
            "Current direct `Player.prefab` `AudioClip` refs are `24`",
            "Current direct `Player.prefab` `AudioClip` refs are `28`",
        )

        with self.assertRaises(SystemExit):
            validator.validate_document_text(text, report)

    def test_static_only_boundary_is_required(self) -> None:
        report = validator.validate_audio_scene_static_route(validator.ROOT)
        text = validator.load_text().replace(
            "No Unity, dotnet, import, Play Mode, profiler, screenshots, or asset mutation.",
            "",
        )

        with self.assertRaises(SystemExit):
            validator.validate_document_text(text, report)

    def test_current_route_rejects_unexpected_ok_state_without_doc_update(self) -> None:
        report = AudioSceneStaticReport(
            blockers=(),
            notes=("evidence-class: STATIC_ASSET_YAML / PENDING UNITY PROOF",),
            fallback_required=(),
            direct_refs=(),
            addressable_settings=1,
            addressable_groups=1,
            addressable_entries=1,
        )

        with self.assertRaises(SystemExit):
            validator.validate_current_route(report)


if __name__ == "__main__":
    unittest.main()
