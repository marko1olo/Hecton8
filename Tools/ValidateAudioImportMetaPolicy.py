#!/usr/bin/env python3
"""Validate static AudioImporter .meta policy against the audio ledger."""

from __future__ import annotations

import argparse
import csv
import re
import sys
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
LEDGER_PATH = ROOT / "Docs/Audio/audio_asset_ledger.csv"
TECHNICAL_PATH = ROOT / "Docs/AssetAudit/AUDIO_FILE_TECHNICAL_PROPERTIES_20260605.csv"

LOAD_TYPE_MAP = {
    "0": "DecompressOnLoad",
    "1": "CompressedInMemory",
    "2": "Streaming",
}
COMPRESSION_FORMAT_MAP = {
    "0": "PCM",
    "1": "Vorbis",
    "2": "ADPCM",
}
META_FIELD_PATTERN = re.compile(r"^\s*(?P<key>[A-Za-z0-9_]+):\s*(?P<value>[^\n]+)", re.MULTILINE)
FORCE_MONO_CLASSES = {"sfx", "player_loop"}
SHORT_NON_STREAMING_CLASSES = {"sfx", "ui"}
MUSIC_AMBIENT_CLASSES = {"music", "ambient"}
QUALITY_EPSILON = 0.02


@dataclass(frozen=True)
class LedgerRow:
    path: str
    cue_id: str
    audio_class: str
    duration_sec: float
    load_type: str
    compression: str
    quality: float


@dataclass(frozen=True)
class AudioMeta:
    load_type: str
    sample_rate_setting: int
    sample_rate_override: int
    compression: str
    quality: float
    preload_audio_data: bool
    force_to_mono: bool
    load_in_background: bool
    ambisonic: bool


@dataclass(frozen=True)
class AudioImportMetaPolicyReport:
    rows: int
    missing_meta: int
    load_mismatch: int
    compression_mismatch: int
    quality_mismatch: int
    force_mono_policy: int
    sample_rate_policy: int
    preload_background_policy: int
    short_streaming_policy: int
    blockers: int


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_csv(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing CSV: {display_path(path)}")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None:
            raise SystemExit(f"FAIL: CSV has no header: {display_path(path)}")
        return [{key: (value or "").strip() for key, value in row.items()} for row in reader]


def load_ledger_rows(path: Path = LEDGER_PATH) -> list[LedgerRow]:
    rows: list[LedgerRow] = []
    for row_index, row in enumerate(load_csv(path), start=2):
        for column in ("path", "cue_id", "class", "duration_sec", "load_type", "compression", "quality"):
            if not row.get(column):
                raise SystemExit(f"FAIL: audio ledger empty {column} at row {row_index}")
        rows.append(
            LedgerRow(
                path=row["path"],
                cue_id=row["cue_id"],
                audio_class=row["class"],
                duration_sec=float(row["duration_sec"]),
                load_type=row["load_type"],
                compression=row["compression"],
                quality=float(row["quality"]),
            )
        )
    return rows


def load_technical_paths(path: Path = TECHNICAL_PATH) -> set[str]:
    paths = {row.get("path", "") for row in load_csv(path)}
    paths.discard("")
    if not paths:
        raise SystemExit(f"FAIL: no paths in technical audio table: {display_path(path)}")
    return paths


def parse_bool01(value: str, field_name: str, path: Path) -> bool:
    if value == "0":
        return False
    if value == "1":
        return True
    raise SystemExit(f"FAIL: invalid bool field {field_name}={value!r} in {display_path(path)}")


def parse_audio_meta(path: Path) -> AudioMeta:
    if not path.exists():
        raise FileNotFoundError(display_path(path))
    text = path.read_text(encoding="utf-8", errors="ignore")
    if "AudioImporter:" not in text:
        raise SystemExit(f"FAIL: meta file is not AudioImporter: {display_path(path)}")

    fields = {match.group("key"): match.group("value").strip() for match in META_FIELD_PATTERN.finditer(text)}
    required = (
        "loadType",
        "sampleRateSetting",
        "sampleRateOverride",
        "compressionFormat",
        "quality",
        "preloadAudioData",
        "forceToMono",
        "loadInBackground",
        "ambisonic",
    )
    missing = [field for field in required if field not in fields]
    if missing:
        raise SystemExit(f"FAIL: AudioImporter meta missing field(s) {', '.join(missing)}: {display_path(path)}")

    return AudioMeta(
        load_type=LOAD_TYPE_MAP.get(fields["loadType"], fields["loadType"]),
        sample_rate_setting=int(fields["sampleRateSetting"]),
        sample_rate_override=int(fields["sampleRateOverride"]),
        compression=COMPRESSION_FORMAT_MAP.get(fields["compressionFormat"], fields["compressionFormat"]),
        quality=float(fields["quality"]),
        preload_audio_data=parse_bool01(fields["preloadAudioData"], "preloadAudioData", path),
        force_to_mono=parse_bool01(fields["forceToMono"], "forceToMono", path),
        load_in_background=parse_bool01(fields["loadInBackground"], "loadInBackground", path),
        ambisonic=parse_bool01(fields["ambisonic"], "ambisonic", path),
    )


def preload_policy_mismatch(meta: AudioMeta) -> bool:
    if meta.load_type == "DecompressOnLoad":
        return not meta.preload_audio_data or meta.load_in_background
    if meta.load_type in {"CompressedInMemory", "Streaming"}:
        return meta.preload_audio_data or not meta.load_in_background
    return True


def sample_rate_policy_mismatch(row: LedgerRow, meta: AudioMeta) -> bool:
    if meta.sample_rate_setting != 2:
        return False
    if row.audio_class in MUSIC_AMBIENT_CLASSES:
        return meta.sample_rate_override > 44100
    if row.audio_class in FORCE_MONO_CLASSES or row.audio_class == "ui":
        return meta.sample_rate_override > 22050
    return False


def validate_audio_import_meta_policy(root: Path = ROOT) -> AudioImportMetaPolicyReport:
    ledger = load_ledger_rows()
    technical_paths = load_technical_paths()
    ledger_paths = {row.path for row in ledger}
    if ledger_paths != technical_paths:
        missing = sorted(ledger_paths - technical_paths)[:5]
        extra = sorted(technical_paths - ledger_paths)[:5]
        raise SystemExit(f"FAIL: audio ledger and technical table path drift: missing={missing} extra={extra}")

    missing_meta = 0
    load_mismatch = 0
    compression_mismatch = 0
    quality_mismatch = 0
    force_mono_policy = 0
    sample_rate_policy = 0
    preload_background_policy = 0
    short_streaming_policy = 0

    for row in ledger:
        meta_path = root / f"{row.path}.meta"
        try:
            meta = parse_audio_meta(meta_path)
        except FileNotFoundError:
            missing_meta += 1
            continue

        if meta.load_type != row.load_type:
            load_mismatch += 1
        if meta.compression != row.compression:
            compression_mismatch += 1
        if abs(meta.quality - row.quality) > QUALITY_EPSILON:
            quality_mismatch += 1
        if row.audio_class in FORCE_MONO_CLASSES and not meta.force_to_mono:
            force_mono_policy += 1
        if sample_rate_policy_mismatch(row, meta):
            sample_rate_policy += 1
        if preload_policy_mismatch(meta):
            preload_background_policy += 1
        if row.audio_class in SHORT_NON_STREAMING_CLASSES and meta.load_type == "Streaming":
            short_streaming_policy += 1

    blockers = (
        missing_meta
        + load_mismatch
        + compression_mismatch
        + quality_mismatch
        + force_mono_policy
        + sample_rate_policy
        + preload_background_policy
        + short_streaming_policy
    )
    return AudioImportMetaPolicyReport(
        rows=len(ledger),
        missing_meta=missing_meta,
        load_mismatch=load_mismatch,
        compression_mismatch=compression_mismatch,
        quality_mismatch=quality_mismatch,
        force_mono_policy=force_mono_policy,
        sample_rate_policy=sample_rate_policy,
        preload_background_policy=preload_background_policy,
        short_streaming_policy=short_streaming_policy,
        blockers=blockers,
    )


def print_report(report: AudioImportMetaPolicyReport) -> None:
    status = "AUDIO_IMPORT_META_POLICY_OK" if report.blockers == 0 else "AUDIO_IMPORT_META_POLICY_REJECTED"
    print(
        f"{status} blockers={report.blockers} rows={report.rows} "
        f"missing_meta={report.missing_meta} load_mismatch={report.load_mismatch} "
        f"compression_mismatch={report.compression_mismatch} quality_mismatch={report.quality_mismatch} "
        f"force_mono_policy={report.force_mono_policy} sample_rate_policy={report.sample_rate_policy} "
        f"preload_background_policy={report.preload_background_policy} "
        f"short_streaming_policy={report.short_streaming_policy}"
    )
    if report.blockers:
        print("+ evidence-class: STATIC_AUDIO_IMPORTER_META / PENDING UNITY IMPORT READBACK")
        print("+ side-effects: no import/reimport/meta write/Addressables/build/Unity action performed")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--no-fail", action="store_true", help="Return success while printing rejection counters.")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(sys.argv[1:] if argv is None else argv)
    report = validate_audio_import_meta_policy()
    print_report(report)
    if report.blockers and not args.no_fail:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
