#!/usr/bin/env python3
"""Static validator for the HECTON-8 audio scene route.

Evidence class: STATIC_ASSET_YAML only. This script does not prove Unity import,
runtime playback, mixer output, Addressables load/release, GC, or profiler health.
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ROOT.parent

DEFAULT_SCENE = REPO_ROOT / "Assets" / "_Project" / "Scenes" / "02_HECTON_WORLD.unity"
DEFAULT_CONFIG = REPO_ROOT / "Assets" / "_Project" / "Data" / "Audio" / "Music" / "Configs" / "MusicDirectorConfig_Global.asset"
DEFAULT_MUSIC_PREFAB = REPO_ROOT / "Assets" / "_Project" / "Prefabs" / "Audio" / "PFB_HectonMusicDirectorRoot.prefab"
DEFAULT_ADDRESSABLES = REPO_ROOT / "Assets" / "AddressableAssetsData"
DEFAULT_PLAYER_PREFAB = REPO_ROOT / "Assets" / "_Project" / "Prefabs" / "Player.prefab"

AUDIO_EXTENSIONS = {".wav", ".ogg", ".mp3", ".flac", ".aif", ".aiff"}
ZERO_GUID = "0" * 32


@dataclass(frozen=True)
class ObjectRef:
    file_id: str
    guid: str | None
    ref_type: str | None

    @property
    def is_non_null(self) -> bool:
        if self.file_id == "0":
            return False
        if self.guid is not None and self.guid.lower() == ZERO_GUID:
            return False
        return True


@dataclass(frozen=True)
class UnityDoc:
    type_id: str
    file_id: str
    body: str


@dataclass(frozen=True)
class DirectAudioRef:
    line: int
    field: str
    guid: str
    asset_path: str

    @property
    def category(self) -> str:
        lowered_path = self.asset_path.lower().replace("\\", "/")
        lowered_field = self.field.lower()
        if "underwater ambient" in lowered_path:
            return "underwater_ambient"
        if "dive_splash" in lowered_path or "splash" in lowered_field:
            return "dive_splash"
        if "/footsteps/" in lowered_path or "footstep" in lowered_field:
            return "footstep"
        if "/ui/" in lowered_path or lowered_field in {"opensound", "closesound", "tabswitchsound", "lowbatterysound"}:
            return "ui"
        if self.asset_path == "UNKNOWN_GUID":
            return "unknown"
        return "other"


@dataclass(frozen=True)
class AudioSceneStaticReport:
    blockers: tuple[str, ...]
    notes: tuple[str, ...]
    fallback_required: tuple[str, ...]
    direct_refs: tuple[DirectAudioRef, ...]
    addressable_settings: int
    addressable_groups: int
    addressable_entries: int

    @property
    def is_ok(self) -> bool:
        return not self.blockers


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def display_path(path: Path, root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def parse_object_ref(line: str) -> ObjectRef | None:
    match = re.search(
        r"\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-fA-F]{32}))?(?:,\s*type:\s*(\d+))?\}",
        line,
    )
    if not match:
        return None
    return ObjectRef(
        file_id=match.group(1),
        guid=match.group(2).lower() if match.group(2) else None,
        ref_type=match.group(3),
    )


def split_unity_docs(text: str) -> list[UnityDoc]:
    docs: list[UnityDoc] = []
    current_header: re.Match[str] | None = None
    current_lines: list[str] = []

    for line in text.splitlines():
        header = re.match(r"--- !u!(\d+)\s+&(-?\d+)", line)
        if header:
            if current_header is not None:
                docs.append(
                    UnityDoc(
                        type_id=current_header.group(1),
                        file_id=current_header.group(2),
                        body="\n".join(current_lines),
                    )
                )
            current_header = header
            current_lines = []
        else:
            current_lines.append(line)

    if current_header is not None:
        docs.append(
            UnityDoc(
                type_id=current_header.group(1),
                file_id=current_header.group(2),
                body="\n".join(current_lines),
            )
        )

    return docs


def scalar_value(body: str, key: str) -> str | None:
    match = re.search(rf"^\s*{re.escape(key)}:\s*(.*)$", body, re.MULTILINE)
    return match.group(1).strip() if match else None


def ref_value(body: str, key: str) -> ObjectRef | None:
    value = scalar_value(body, key)
    return parse_object_ref(value) if value is not None else None


def validate_scene(scene_path: Path, root: Path) -> tuple[list[str], list[str]]:
    blockers: list[str] = []
    notes: list[str] = []

    if not scene_path.exists():
        return [f"scene-missing: {display_path(scene_path, root)}"], notes

    docs = split_unity_docs(read_text(scene_path))
    game_objects = {
        doc.file_id: doc
        for doc in docs
        if doc.type_id == "1"
    }
    anchors: list[tuple[UnityDoc, UnityDoc, ObjectRef | None]] = []

    for doc in docs:
        if doc.type_id != "114":
            continue
        if "Hecton8.Audio.HectonMusicDirectorAnchor" not in doc.body:
            continue
        game_object_ref = ref_value(doc.body, "m_GameObject")
        game_object = game_objects.get(game_object_ref.file_id if game_object_ref else "")
        config_ref = ref_value(doc.body, "_config")
        if game_object is not None:
            anchors.append((doc, game_object, config_ref))

    active_matching = []
    for anchor, game_object, config_ref in anchors:
        name = scalar_value(game_object.body, "m_Name")
        is_active = scalar_value(game_object.body, "m_IsActive") == "1"
        is_enabled = scalar_value(anchor.body, "m_Enabled") == "1"
        if name == "'[MUSIC_SYSTEM]'" and is_active and is_enabled:
            active_matching.append((anchor, game_object, config_ref))

    if len(active_matching) != 1:
        blockers.append(
            "scene-anchor-count: expected exactly one active [MUSIC_SYSTEM] / "
            f"HectonMusicDirectorAnchor, found {len(active_matching)}"
        )
    else:
        config_ref = active_matching[0][2]
        if config_ref is None or not config_ref.is_non_null or config_ref.guid is None:
            blockers.append("scene-anchor-config-null: HectonMusicDirectorAnchor _config has null/non-project ref")
        else:
            notes.append(f"scene-anchor: active [MUSIC_SYSTEM] config_guid={config_ref.guid}")

    return blockers, notes


def validate_config(config_path: Path, root: Path) -> tuple[list[str], list[str], list[str]]:
    blockers: list[str] = []
    notes: list[str] = []
    fallback_required: list[str] = []

    if not config_path.exists():
        return [f"config-missing: {display_path(config_path, root)}"], notes, fallback_required

    text = read_text(config_path)
    runtime_ref = ref_value(text, "_runtimeDirectorPrefab")
    if runtime_ref is None or not runtime_ref.is_non_null or runtime_ref.guid is None:
        blockers.append("config-runtime-prefab-null: MusicDirectorConfig_Global _runtimeDirectorPrefab is null")
    else:
        notes.append(f"config-runtime-prefab: guid={runtime_ref.guid}")

    for field_name in ("_musicMixerGroup", "_stingerMixerGroup"):
        mixer_ref = ref_value(text, field_name)
        if mixer_ref is None or not mixer_ref.is_non_null:
            fallback_required.append(
                f"config-mixer-fallback-required: {field_name} is null; STATIC_ASSET_YAML is not runtime mixer proof"
            )
        else:
            notes.append(f"config-mixer-ref: {field_name} is statically non-null")

    return blockers, notes, fallback_required


def validate_music_prefab(prefab_path: Path, root: Path) -> tuple[list[str], list[str], list[str]]:
    blockers: list[str] = []
    notes: list[str] = []
    fallback_required: list[str] = []

    if not prefab_path.exists():
        return [f"music-prefab-missing: {display_path(prefab_path, root)}"], notes, fallback_required

    text = read_text(prefab_path)

    if "Hecton8.Audio.HectonMusicDirector" not in text:
        blockers.append("music-prefab-director-missing: HectonMusicDirector class identifier absent")
    else:
        notes.append("music-prefab-director: HectonMusicDirector identifier present")

    if "Hecton8.Audio.MusicVoicePool" not in text:
        blockers.append("music-prefab-voice-pool-missing: MusicVoicePool class identifier absent")
    else:
        notes.append("music-prefab-voice-pool: MusicVoicePool identifier present")

    voice_names = sorted(set(re.findall(r"^\s*m_Name:\s*(MusicVoice[^\r\n]*)$", text, re.MULTILINE)))
    if len(voice_names) < 2:
        blockers.append(f"music-prefab-voice-children: expected at least two MusicVoice-like child names, found {len(voice_names)}")
    else:
        notes.append(f"music-prefab-voice-children: count={len(voice_names)} names={','.join(voice_names[:4])}")

    if re.search(r"^\s*m_Name:\s*MusicStinger\s*$", text, re.MULTILINE) is None:
        blockers.append("music-prefab-stinger-missing: MusicStinger child name not statically detectable")
    else:
        notes.append("music-prefab-stinger: MusicStinger child name present")

    music_voice_refs = re.findall(r"^\s*-\s*\{fileID:\s*([1-9]\d*)\}\s*$", block_for_key(text, "_musicVoices"), re.MULTILINE)
    stinger_ref = ref_value(text, "_stingerSource")
    if len(music_voice_refs) < 2:
        blockers.append(f"music-prefab-voice-pool-refs: expected at least two _musicVoices refs, found {len(music_voice_refs)}")
    else:
        notes.append(f"music-prefab-voice-pool-refs: count={len(music_voice_refs)}")
    if stinger_ref is None or not stinger_ref.is_non_null:
        blockers.append("music-prefab-stinger-ref: _stingerSource is null or absent")
    else:
        notes.append("music-prefab-stinger-ref: _stingerSource non-null")

    null_output_count = len(re.findall(r"^\s*OutputAudioMixerGroup:\s*\{fileID:\s*0\}\s*$", text, re.MULTILINE))
    if null_output_count:
        fallback_required.append(
            f"music-prefab-mixer-fallback-required: {null_output_count} AudioSource OutputAudioMixerGroup refs are null; STATIC_ASSET_YAML is not runtime mixer proof"
        )
    else:
        notes.append("music-prefab-mixer-refs: all statically detected OutputAudioMixerGroup refs are non-null")

    return blockers, notes, fallback_required


def block_for_key(text: str, key: str) -> str:
    lines = text.splitlines()
    for index, line in enumerate(lines):
        if re.match(rf"^\s*{re.escape(key)}:\s*$", line):
            start_indent = len(line) - len(line.lstrip())
            collected: list[str] = []
            for follow in lines[index + 1 :]:
                if follow.strip() == "":
                    collected.append(follow)
                    continue
                indent = len(follow) - len(follow.lstrip())
                if indent <= start_indent and not follow.lstrip().startswith("- "):
                    break
                collected.append(follow)
            return "\n".join(collected)
    return ""


def inspect_addressables(addressables_path: Path, root: Path) -> tuple[list[str], list[str], int, int, int]:
    blockers: list[str] = []
    notes: list[str] = []

    if not addressables_path.exists():
        return [f"addressables-absent: {display_path(addressables_path, root)} missing"], notes, 0, 0, 0

    files = [path for path in addressables_path.rglob("*") if path.is_file()]
    settings = [path for path in files if "AddressableAssetSettings" in path.name and path.suffix.lower() == ".asset"]
    groups = [
        path
        for path in files
        if path.suffix.lower() == ".asset" and ("AssetGroups" in path.parts or "Groups" in path.parts)
    ]

    entry_count = 0
    for path in groups:
        text = read_text(path)
        entry_count += len(re.findall(r"^\s*m_GUID:\s*[0-9a-fA-F]{32}\s*$", text, re.MULTILINE))
        entry_count += len(re.findall(r"^\s*m_Address:\s*.+$", text, re.MULTILINE))

    if not files or (not settings and not groups and entry_count == 0):
        blockers.append(
            f"addressables-absent: {display_path(addressables_path, root)} has settings=0 groups=0 entries=0"
        )
    else:
        notes.append(f"addressables-static: files={len(files)} settings={len(settings)} groups={len(groups)} entries={entry_count}")

    return blockers, notes, len(settings), len(groups), entry_count


def build_guid_map(root: Path, wanted_guids: set[str]) -> dict[str, str]:
    if not wanted_guids:
        return {}

    guid_map: dict[str, str] = {}
    assets_root = root / "Assets"
    if not assets_root.exists():
        return guid_map

    for meta_path in assets_root.rglob("*.meta"):
        try:
            with meta_path.open("r", encoding="utf-8-sig", errors="replace") as handle:
                prefix = handle.read(512)
        except OSError:
            continue
        match = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", prefix, re.MULTILINE)
        if not match:
            continue
        guid = match.group(1).lower()
        if guid not in wanted_guids:
            continue
        asset_path = meta_path.with_suffix("")
        guid_map[guid] = display_path(asset_path, root)
        if len(guid_map) == len(wanted_guids):
            break

    return guid_map


def scan_player_direct_audio_refs(player_prefab_path: Path, root: Path) -> tuple[list[str], list[str], list[DirectAudioRef]]:
    blockers: list[str] = []
    notes: list[str] = []
    refs: list[DirectAudioRef] = []

    if not player_prefab_path.exists():
        return [f"player-prefab-missing: {display_path(player_prefab_path, root)}"], notes, refs

    lines = read_text(player_prefab_path).splitlines()
    ref_line_pattern = re.compile(r"\{fileID:\s*8300000,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*3\}")
    found_guids = {
        match.group(1).lower()
        for line in lines
        for match in ref_line_pattern.finditer(line)
    }
    guid_map = build_guid_map(root, found_guids)

    context_key = ""
    for line_number, line in enumerate(lines, start=1):
        stripped = line.strip()
        key_match = re.match(r"^([A-Za-z_][A-Za-z0-9_]*):(?:\s|$)", stripped)
        if key_match:
            context_key = key_match.group(1)

        for match in ref_line_pattern.finditer(line):
            guid = match.group(1).lower()
            if guid == ZERO_GUID:
                continue
            asset_path = guid_map.get(guid, "UNKNOWN_GUID")
            field = key_match.group(1) if key_match else context_key or "<list-item>"
            if asset_path != "UNKNOWN_GUID":
                suffix = Path(asset_path).suffix.lower()
                if suffix and suffix not in AUDIO_EXTENSIONS:
                    continue
            refs.append(DirectAudioRef(line=line_number, field=field, guid=guid, asset_path=asset_path))

    category_counts = count_categories(refs)
    notes.append(
        "player-direct-audio-refs: "
        f"total={len(refs)} underwater_ambient={category_counts['underwater_ambient']} "
        f"dive_splash={category_counts['dive_splash']} footstep={category_counts['footstep']} "
        f"ui={category_counts['ui']} other={category_counts['other']} unknown={category_counts['unknown']}"
    )

    p0_count = category_counts["underwater_ambient"] + category_counts["dive_splash"]
    if p0_count:
        blockers.append(
            "player-p0-direct-audio-ref: "
            f"count={p0_count} underwater_ambient={category_counts['underwater_ambient']} "
            f"dive_splash={category_counts['dive_splash']}; direct prefab refs require owner/load/release/runtime proof"
        )

    if category_counts["unknown"]:
        blockers.append(f"player-direct-audio-ref-unresolved-guid: count={category_counts['unknown']}")

    if not refs:
        notes.append("player-direct-audio-refs: none detected by fileID 8300000 static scan")

    return blockers, notes, refs


def count_categories(refs: list[DirectAudioRef] | tuple[DirectAudioRef, ...]) -> dict[str, int]:
    counts = {
        "underwater_ambient": 0,
        "dive_splash": 0,
        "footstep": 0,
        "ui": 0,
        "other": 0,
        "unknown": 0,
    }
    for ref in refs:
        counts[ref.category] = counts.get(ref.category, 0) + 1
    return counts


def validate_audio_scene_static_route(
    root: Path,
    scene_path: Path | None = None,
    config_path: Path | None = None,
    music_prefab_path: Path | None = None,
    addressables_path: Path | None = None,
    player_prefab_path: Path | None = None,
) -> AudioSceneStaticReport:
    scene_path = scene_path or root / "Assets" / "_Project" / "Scenes" / "02_HECTON_WORLD.unity"
    config_path = config_path or root / "Assets" / "_Project" / "Data" / "Audio" / "Music" / "Configs" / "MusicDirectorConfig_Global.asset"
    music_prefab_path = music_prefab_path or root / "Assets" / "_Project" / "Prefabs" / "Audio" / "PFB_HectonMusicDirectorRoot.prefab"
    addressables_path = addressables_path or root / "Assets" / "AddressableAssetsData"
    player_prefab_path = player_prefab_path or root / "Assets" / "_Project" / "Prefabs" / "Player.prefab"

    blockers: list[str] = []
    notes: list[str] = ["evidence-class: STATIC_ASSET_YAML / PENDING UNITY PROOF"]
    fallback_required: list[str] = []

    scene_blockers, scene_notes = validate_scene(scene_path, root)
    blockers.extend(scene_blockers)
    notes.extend(scene_notes)

    config_blockers, config_notes, config_fallback = validate_config(config_path, root)
    blockers.extend(config_blockers)
    notes.extend(config_notes)
    fallback_required.extend(config_fallback)

    prefab_blockers, prefab_notes, prefab_fallback = validate_music_prefab(music_prefab_path, root)
    blockers.extend(prefab_blockers)
    notes.extend(prefab_notes)
    fallback_required.extend(prefab_fallback)

    addressable_blockers, addressable_notes, settings_count, group_count, entry_count = inspect_addressables(addressables_path, root)
    blockers.extend(addressable_blockers)
    notes.extend(addressable_notes)

    ref_blockers, ref_notes, direct_refs = scan_player_direct_audio_refs(player_prefab_path, root)
    blockers.extend(ref_blockers)
    notes.extend(ref_notes)

    return AudioSceneStaticReport(
        blockers=tuple(blockers),
        notes=tuple(notes),
        fallback_required=tuple(fallback_required),
        direct_refs=tuple(direct_refs),
        addressable_settings=settings_count,
        addressable_groups=group_count,
        addressable_entries=entry_count,
    )


def print_report(report: AudioSceneStaticReport) -> None:
    if report.is_ok:
        print(f"OK AUDIO_SCENE_STATIC_ROUTE_OK blockers=0")
    else:
        print(f"AUDIO_SCENE_STATIC_ROUTE_REJECTED blockers={len(report.blockers)}")

    for blocker in report.blockers:
        print(f"- {blocker}")
    for fallback in report.fallback_required:
        print(f"! {fallback}")
    for note in report.notes:
        print(f"+ {note}")

    if report.direct_refs:
        print("direct-audio-ref-details:")
        for ref in report.direct_refs:
            print(f"* line={ref.line} field={ref.field} category={ref.category} asset={ref.asset_path} guid={ref.guid}")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=REPO_ROOT)
    parser.add_argument("--scene", type=Path)
    parser.add_argument("--config", type=Path)
    parser.add_argument("--music-prefab", type=Path)
    parser.add_argument("--addressables", type=Path)
    parser.add_argument("--player-prefab", type=Path)
    parser.add_argument("--no-fail", action="store_true", help="Print reject report but return exit code 0.")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    root = args.root.resolve()
    report = validate_audio_scene_static_route(
        root=root,
        scene_path=args.scene,
        config_path=args.config,
        music_prefab_path=args.music_prefab,
        addressables_path=args.addressables,
        player_prefab_path=args.player_prefab,
    )
    print_report(report)
    if report.is_ok or args.no_fail:
        return 0
    return 2


if __name__ == "__main__":
    sys.exit(main())
