#!/usr/bin/env python3
"""Tests for the static audio scene route validator."""

from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import ValidateAudioSceneStaticRoute as validator  # noqa: E402


class ValidateAudioSceneStaticRouteTests(unittest.TestCase):
    def test_synthetic_route_passes_with_mixer_fallback_and_reported_p1_refs(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_audio_scene_ok_") as temp_dir:
            root = Path(temp_dir)
            _write_static_fixture(root, include_addressables=True, include_p0_direct_refs=False)

            report = validator.validate_audio_scene_static_route(root)

        self.assertTrue(report.is_ok, "\n".join(report.blockers))
        self.assertGreaterEqual(len(report.fallback_required), 2)
        counts = validator.count_categories(report.direct_refs)
        self.assertEqual(1, counts["footstep"])
        self.assertEqual(1, counts["ui"])
        self.assertEqual(0, counts["underwater_ambient"])
        self.assertEqual(0, counts["dive_splash"])

    def test_synthetic_route_rejects_missing_addressables_and_p0_direct_refs(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_audio_scene_reject_") as temp_dir:
            root = Path(temp_dir)
            _write_static_fixture(root, include_addressables=False, include_p0_direct_refs=True)

            report = validator.validate_audio_scene_static_route(root)

        self.assertFalse(report.is_ok)
        joined = "\n".join(report.blockers)
        self.assertIn("addressables-absent", joined)
        self.assertIn("player-p0-direct-audio-ref", joined)
        counts = validator.count_categories(report.direct_refs)
        self.assertEqual(1, counts["underwater_ambient"])
        self.assertEqual(1, counts["dive_splash"])

    def test_scene_requires_single_active_anchor(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_audio_scene_dupe_") as temp_dir:
            root = Path(temp_dir)
            _write_static_fixture(root, include_addressables=True, include_p0_direct_refs=False, duplicate_anchor=True)

            report = validator.validate_audio_scene_static_route(root)

        self.assertFalse(report.is_ok)
        self.assertIn("scene-anchor-count", "\n".join(report.blockers))

    def test_current_repo_static_route_reports_known_blockers_when_available(self) -> None:
        required = (
            validator.DEFAULT_SCENE,
            validator.DEFAULT_CONFIG,
            validator.DEFAULT_MUSIC_PREFAB,
            validator.DEFAULT_PLAYER_PREFAB,
        )
        if any(not path.exists() for path in required):
            self.skipTest("Current project audio route files are not present")

        report = validator.validate_audio_scene_static_route(REPO_ROOT)

        self.assertFalse(report.is_ok)
        joined = "\n".join(report.blockers)
        self.assertIn("addressables-absent", joined)
        self.assertNotIn("player-p0-direct-audio-ref", joined)
        counts = validator.count_categories(report.direct_refs)
        self.assertEqual(0, counts["underwater_ambient"])
        self.assertEqual(0, counts["dive_splash"])
        self.assertGreaterEqual(counts["footstep"], 1)
        self.assertGreaterEqual(counts["ui"], 1)

    def test_cli_reject_output_and_no_fail_exit(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_audio_scene_cli_") as temp_dir:
            root = Path(temp_dir)
            _write_static_fixture(root, include_addressables=False, include_p0_direct_refs=True)
            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT_DIR / "ValidateAudioSceneStaticRoute.py"),
                    "--root",
                    str(root),
                    "--no-fail",
                ],
                check=False,
                capture_output=True,
                text=True,
            )

        self.assertEqual(0, result.returncode)
        self.assertIn("AUDIO_SCENE_STATIC_ROUTE_REJECTED", result.stdout)
        self.assertIn("addressables-absent", result.stdout)
        self.assertIn("direct-audio-ref-details", result.stdout)


def _write_static_fixture(
    root: Path,
    *,
    include_addressables: bool,
    include_p0_direct_refs: bool,
    duplicate_anchor: bool = False,
) -> None:
    scene_path = root / "Assets" / "_Project" / "Scenes" / "02_HECTON_WORLD.unity"
    config_path = root / "Assets" / "_Project" / "Data" / "Audio" / "Music" / "Configs" / "MusicDirectorConfig_Global.asset"
    music_prefab_path = root / "Assets" / "_Project" / "Prefabs" / "Audio" / "PFB_HectonMusicDirectorRoot.prefab"
    player_prefab_path = root / "Assets" / "_Project" / "Prefabs" / "Player.prefab"
    addressables_path = root / "Assets" / "AddressableAssetsData"

    scene_path.parent.mkdir(parents=True, exist_ok=True)
    config_path.parent.mkdir(parents=True, exist_ok=True)
    music_prefab_path.parent.mkdir(parents=True, exist_ok=True)
    player_prefab_path.parent.mkdir(parents=True, exist_ok=True)
    addressables_path.mkdir(parents=True, exist_ok=True)

    scene_text = _scene_yaml("100", "200", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
    if duplicate_anchor:
        scene_text += "\n" + _scene_yaml("101", "201", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")
    scene_path.write_text(scene_text, encoding="utf-8")

    config_path.write_text(
        "\n".join(
            [
                "%YAML 1.1",
                "--- !u!114 &11400000",
                "MonoBehaviour:",
                "  m_Name: MusicDirectorConfig_Global",
                "  m_EditorClassIdentifier: Hecton8.Core::Hecton8.Audio.HectonMusicDirectorConfig",
                "  _musicMixerGroup: {fileID: 0}",
                "  _stingerMixerGroup: {fileID: 0}",
                "  _runtimeDirectorPrefab: {fileID: 4511111111111111111, guid: cccccccccccccccccccccccccccccccc, type: 3}",
            ]
        ),
        encoding="utf-8",
    )

    music_prefab_path.write_text(
        "\n".join(
            [
                "%YAML 1.1",
                "--- !u!1 &10",
                "GameObject:",
                "  m_Name: MusicVoice_0",
                "--- !u!82 &20",
                "AudioSource:",
                "  OutputAudioMixerGroup: {fileID: 0}",
                "--- !u!1 &11",
                "GameObject:",
                "  m_Name: MusicVoice_1",
                "--- !u!82 &21",
                "AudioSource:",
                "  OutputAudioMixerGroup: {fileID: 0}",
                "--- !u!1 &12",
                "GameObject:",
                "  m_Name: MusicStinger",
                "--- !u!82 &22",
                "AudioSource:",
                "  OutputAudioMixerGroup: {fileID: 0}",
                "--- !u!114 &30",
                "MonoBehaviour:",
                "  m_EditorClassIdentifier: Hecton8.Core::Hecton8.Audio.HectonMusicDirector",
                "  _voicePool: {fileID: 31}",
                "--- !u!114 &31",
                "MonoBehaviour:",
                "  m_EditorClassIdentifier: Hecton8.Core::Hecton8.Audio.MusicVoicePool",
                "  _musicVoices:",
                "  - {fileID: 20}",
                "  - {fileID: 21}",
                "  _stingerSource: {fileID: 22}",
            ]
        ),
        encoding="utf-8",
    )

    player_lines = [
        "%YAML 1.1",
        "--- !u!114 &1",
        "MonoBehaviour:",
        "  defaultFootstepClips:",
        "  - {fileID: 8300000, guid: dddddddddddddddddddddddddddddddd, type: 3}",
        "  openSound: {fileID: 8300000, guid: eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee, type: 3}",
    ]
    if include_p0_direct_refs:
        player_lines.extend(
            [
                "  m_Resource: {fileID: 8300000, guid: ffffffffffffffffffffffffffffffff, type: 3}",
                "  waterEntrySplashClip: {fileID: 8300000, guid: 11111111111111111111111111111111, type: 3}",
            ]
        )
    player_prefab_path.write_text("\n".join(player_lines), encoding="utf-8")

    _write_meta(root / "Assets" / "_Project" / "Audio" / "Footsteps" / "step.wav.meta", "dddddddddddddddddddddddddddddddd")
    _write_meta(root / "Assets" / "_Project" / "Audio" / "UI" / "click.wav.meta", "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee")
    if include_p0_direct_refs:
        _write_meta(root / "Assets" / "_Project" / "Audio" / "Underwater Ambient.wav.meta", "ffffffffffffffffffffffffffffffff")
        _write_meta(root / "Assets" / "_Project" / "Audio" / "Movement" / "dive_splash.wav.meta", "11111111111111111111111111111111")

    if include_addressables:
        settings_path = addressables_path / "AddressableAssetSettings.asset"
        group_path = addressables_path / "AssetGroups" / "Core.asset"
        group_path.parent.mkdir(parents=True, exist_ok=True)
        settings_path.write_text("AddressableAssetSettings:\n  m_Name: AddressableAssetSettings\n", encoding="utf-8")
        group_path.write_text(
            "\n".join(
                [
                    "AddressableAssetGroup:",
                    "  m_Address: Assets/_Project/Audio/UI/click.wav",
                    "  m_GUID: eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
                ]
            ),
            encoding="utf-8",
        )


def _scene_yaml(game_object_id: str, component_id: str, config_guid: str) -> str:
    return "\n".join(
        [
            "%YAML 1.1",
            f"--- !u!1 &{game_object_id}",
            "GameObject:",
            "  m_Component:",
            f"  - component: {{fileID: {component_id}}}",
            "  m_Name: '[MUSIC_SYSTEM]'",
            "  m_IsActive: 1",
            f"--- !u!114 &{component_id}",
            "MonoBehaviour:",
            f"  m_GameObject: {{fileID: {game_object_id}}}",
            "  m_Enabled: 1",
            "  m_EditorClassIdentifier: Hecton8.Core::Hecton8.Audio.HectonMusicDirectorAnchor",
            f"  _config: {{fileID: 11400000, guid: {config_guid}, type: 2}}",
        ]
    )


def _write_meta(path: Path, guid: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(f"fileFormatVersion: 2\nguid: {guid}\n", encoding="utf-8")


if __name__ == "__main__":
    unittest.main()
