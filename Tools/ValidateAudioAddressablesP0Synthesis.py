#!/usr/bin/env python3
"""Validate the Audio/Addressables P0 synthesis against current static route truth."""

from __future__ import annotations

import sys
from pathlib import Path

from ValidateAudioSceneStaticRoute import AudioSceneStaticReport, count_categories, validate_audio_scene_static_route


ROOT = Path(__file__).resolve().parents[1]
SYNTHESIS_PATH = ROOT / "Docs/Orchestration/AUDIO_ADDRESSABLES_P0_SYNTHESIS_20260605.md"

EXPECTED_DIRECT_REFS = 24
EXPECTED_FOOTSTEPS = 20
EXPECTED_UI = 4
EXPECTED_ROUTE_BLOCKERS = 1
EXPECTED_ADDRESSABLE_BLOCKERS = 1

REQUIRED_TERMS = (
    "Status: `STATIC_SYNTHESIS / PENDING UNITY PROOF`",
    "Audio and Addressables are not production-ready.",
    "`AUDIO_SCENE_STATIC_ROUTE_REJECTED blockers=1`: Addressables route absent; scene anchor is statically present but still pending Unity/runtime proof.",
    "Current direct `Player.prefab` `AudioClip` refs are `24` P1 refs: `20` footstep rows and `4` UI rows.",
    "Static scan reports `0` direct `Underwater Ambient.wav` refs and `0` direct `dive_splash.wav` refs.",
    "`Assets/AddressableAssetsData` has no active settings/groups/entries in this checkout.",
    "`Tools/ValidateAudioAddressablesP0Synthesis.py` now guards this synthesis against stale blocker counts and stale direct-ref P0 claims.",
    "`AUDIO_ADDRESSABLES_P0_SYNTHESIS_OK blockers=1 direct_refs=24 p0=0 footsteps=20 ui=4 fallback_required=1`",
    "`Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv` reports 24 direct `Player.prefab` clip refs: `0` P0, `20` footstep P1, and `4` UI P1.",
    "Runtime listening, memory, GC, mixer output, import, and Addressables readiness remain `PENDING VERIFICATION` until fresh proof exists.",
    "Final status: `ADDRESSABLES_P0_BLOCKED / PLAYER_DIRECT_P0_STATIC_CLEARED / STATIC_ONLY / PENDING UNITY PROOF`.",
)

FORBIDDEN_STALE_TERMS = (
    "Current direct `Player.prefab` `AudioClip` refs are `28`",
    "`Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv` reports 28",
    "P0 direct Player audio refs",
    "player-p0-direct-audio-ref",
    "`Underwater Ambient.wav` refs and `1` direct `dive_splash.wav` refs",
)


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_text(path: Path = SYNTHESIS_PATH) -> str:
    if not path.exists():
        raise SystemExit(f"FAIL: missing audio Addressables synthesis doc: {display_path(path)}")
    return path.read_text(encoding="utf-8")


def validate_current_route(report: AudioSceneStaticReport) -> dict[str, int]:
    blockers = list(report.blockers)
    addressable_blockers = [blocker for blocker in blockers if "addressables-absent" in blocker]
    non_addressable_blockers = [blocker for blocker in blockers if "addressables-absent" not in blocker]

    if report.is_ok:
        raise SystemExit("FAIL: current audio route unexpectedly reports OK; synthesis must be updated with Unity proof first")
    if len(blockers) != EXPECTED_ROUTE_BLOCKERS:
        raise SystemExit(f"FAIL: expected {EXPECTED_ROUTE_BLOCKERS} current route blockers, got {len(blockers)}")
    if len(addressable_blockers) != EXPECTED_ADDRESSABLE_BLOCKERS:
        raise SystemExit(f"FAIL: expected one Addressables hard blocker, got {len(addressable_blockers)}")
    if non_addressable_blockers:
        raise SystemExit(f"FAIL: unexpected non-Addressables hard blocker(s): {'; '.join(non_addressable_blockers)}")

    if report.addressable_settings != 0 or report.addressable_groups != 0 or report.addressable_entries != 0:
        raise SystemExit(
            "FAIL: Addressables settings/groups/entries are no longer absent; synthesis requires Unity readback update"
        )

    counts = count_categories(report.direct_refs)
    if len(report.direct_refs) != EXPECTED_DIRECT_REFS:
        raise SystemExit(f"FAIL: expected {EXPECTED_DIRECT_REFS} current Player direct refs, got {len(report.direct_refs)}")
    if counts["underwater_ambient"] != 0 or counts["dive_splash"] != 0:
        raise SystemExit(
            "FAIL: current Player.prefab static scan has P0 ambient/splash direct refs; synthesis stale"
        )
    if counts["footstep"] != EXPECTED_FOOTSTEPS or counts["ui"] != EXPECTED_UI:
        raise SystemExit(
            f"FAIL: current Player direct-ref category drift: footstep={counts['footstep']} ui={counts['ui']}"
        )
    if not any("config-mixer-ref: _musicMixerGroup is statically non-null" in item for item in report.notes):
        raise SystemExit("FAIL: MusicDirector config _musicMixerGroup static non-null note missing from current route")
    if not any("config-mixer-ref: _stingerMixerGroup is statically non-null" in item for item in report.notes):
        raise SystemExit("FAIL: MusicDirector config _stingerMixerGroup static non-null note missing from current route")
    if not any("OutputAudioMixerGroup" in item for item in report.fallback_required):
        raise SystemExit("FAIL: MusicDirector prefab mixer fallback note missing from current static route")

    return counts


def validate_document_text(text: str, report: AudioSceneStaticReport) -> None:
    for term in REQUIRED_TERMS:
        if term not in text:
            raise SystemExit(f"FAIL: audio Addressables synthesis missing required term: {term}")

    lowered = text.lower()
    for term in FORBIDDEN_STALE_TERMS:
        if term.lower() in lowered:
            raise SystemExit(f"FAIL: audio Addressables synthesis contains stale term: {term}")

    if "No Unity, dotnet, import, Play Mode, profiler, screenshots, or asset mutation." not in text:
        raise SystemExit("FAIL: audio Addressables synthesis lost static-only evidence boundary")
    if "not accepted" not in lowered:
        raise SystemExit("FAIL: audio Addressables synthesis must explicitly reject static route acceptance")

    blocker_phrase = f"AUDIO_SCENE_STATIC_ROUTE_REJECTED blockers={len(report.blockers)}"
    if blocker_phrase not in text:
        raise SystemExit(f"FAIL: synthesis blocker count does not match current static route: {blocker_phrase}")


def validate_audio_addressables_p0_synthesis(root: Path = ROOT, text: str | None = None) -> AudioSceneStaticReport:
    report = validate_audio_scene_static_route(root)
    validate_current_route(report)
    validate_document_text(load_text() if text is None else text, report)
    return report


def main() -> int:
    report = validate_audio_addressables_p0_synthesis()
    counts = count_categories(report.direct_refs)
    print(
        "AUDIO_ADDRESSABLES_P0_SYNTHESIS_OK "
        f"blockers={len(report.blockers)} direct_refs={len(report.direct_refs)} p0=0 "
        f"footsteps={counts['footstep']} ui={counts['ui']} fallback_required={len(report.fallback_required)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
