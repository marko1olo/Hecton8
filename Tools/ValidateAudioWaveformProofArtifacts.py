#!/usr/bin/env python3
"""Validate static audio waveform proof-adjacent artifacts."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
STATS_PATH = ROOT / "Docs/AssetAudit/AudioVisual/audio_preview_waveform_stats_20260605.csv"
LISTENING_QUEUE_PATH = ROOT / "Docs/AssetAudit/AUDIO_LISTENING_PASS_QUEUE_20260605.csv"
PROOF_INDEX_PATH = ROOT / "Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.csv"
CONTACT_SHEET_PATH = ROOT / "Docs/AssetAudit/AudioVisual/audio_waveform_contact_sheet_20260605.png"

EXPECTED_STATS_COUNT = 11
REQUIRED_STATS_COLUMNS = ("path", "preview_png", "peak_dbfs", "rms_dbfs", "preview_samples")
REQUIRED_QUEUE_COLUMNS = (
    "queue_order",
    "priority",
    "target",
    "asset_or_config",
    "route_context",
    "why_first",
    "required_runtime_proof",
    "reject_condition",
    "status",
)
REQUIRED_PROOF_COLUMNS = (
    "ArtifactType",
    "ArtifactPath",
    "RelatedAssetFamily",
    "EvidenceClass",
    "Disposition",
    "UseFor",
    "RejectAs",
    "NextOwner",
)
REQUIRED_LINKS = {
    "Assets/_Project/Audio/Breathing/breathing breath in and out 1.mp3": ("P0", "Player breath loop"),
    "Assets/_Project/Audio/Breathing/inside suit sounds (too loud).wav": ("P0", "Suit interior loop"),
    "Assets/_Project/Audio/Music for Game/shelf_1_Abandoned Depths.ogg": ("P1", "Shelf loud long bed"),
    "Assets/_Project/Audio/Music for Game/abyss_3_Deep Trench Drone.ogg": ("P1", "Abyss long drone"),
    "Assets/_Project/Audio/Music for Game/stinger_dangerous_1_Iron_Teeth.ogg": ("P1", "Danger stinger"),
    "Assets/_Project/Audio/UI/click sound.wav": ("P2", "UI click"),
    "Assets/_Project/Audio/VO/Stubs/VOStub_Chen_Log01_EN.wav": ("P2", "VO stub sanity"),
}


@dataclass(frozen=True)
class WaveformStat:
    path: str
    preview_png: str
    peak_dbfs: float
    rms_dbfs: float
    preview_samples: int


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_csv(path: Path, required_columns: tuple[str, ...]) -> list[dict[str, str]]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing CSV: {display_path(path)}")

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        fieldnames = tuple(reader.fieldnames or ())
        missing = [column for column in required_columns if column not in fieldnames]
        if missing:
            raise SystemExit(f"FAIL: {display_path(path)} missing column(s): {', '.join(missing)}")
        rows = list(reader)

    for row_number, row in enumerate(rows, start=2):
        for column in required_columns:
            if not (row.get(column) or "").strip():
                raise SystemExit(f"FAIL: {display_path(path)} empty {column} at row {row_number}")
    return rows


def parse_stats(rows: list[dict[str, str]]) -> list[WaveformStat]:
    stats: list[WaveformStat] = []
    for row_number, row in enumerate(rows, start=2):
        try:
            peak = float(row["peak_dbfs"])
            rms = float(row["rms_dbfs"])
            samples = int(row["preview_samples"])
        except ValueError as exc:
            raise SystemExit(f"FAIL: waveform stats row {row_number} has nonnumeric metric") from exc

        stats.append(
            WaveformStat(
                path=row["path"].strip(),
                preview_png=row["preview_png"].strip().replace("\\", "/"),
                peak_dbfs=peak,
                rms_dbfs=rms,
                preview_samples=samples,
            )
        )
    return stats


def validate_stats(stats: list[WaveformStat], root: Path = ROOT) -> None:
    if len(stats) != EXPECTED_STATS_COUNT:
        raise SystemExit(f"FAIL: expected {EXPECTED_STATS_COUNT} waveform stat rows, got {len(stats)}")
    if not CONTACT_SHEET_PATH.exists():
        raise SystemExit(f"FAIL: missing waveform contact sheet: {display_path(CONTACT_SHEET_PATH)}")

    for stat in stats:
        source_path = root / stat.path
        preview_path = root / stat.preview_png
        if not source_path.exists():
            raise SystemExit(f"FAIL: waveform source missing: {stat.path}")
        if not preview_path.exists():
            raise SystemExit(f"FAIL: waveform preview missing: {stat.preview_png}")
        if stat.preview_samples <= 0:
            raise SystemExit(f"FAIL: waveform preview samples must be positive: {stat.path}")
        if stat.peak_dbfs > 0.0 or stat.rms_dbfs > 0.0:
            raise SystemExit(f"FAIL: waveform dBFS values must not be positive: {stat.path}")

    by_path = {stat.path: stat for stat in stats}
    breath = by_path["Assets/_Project/Audio/Breathing/breathing breath in and out 1.mp3"]
    if breath.peak_dbfs > -1.0 and breath.rms_dbfs > -15.0:
        pass
    else:
        raise SystemExit("FAIL: breath loop hot-peak risk row no longer matches expected waveform profile")

    vo_stub = by_path["Assets/_Project/Audio/VO/Stubs/VOStub_Chen_Log01_EN.wav"]
    if vo_stub.peak_dbfs > -30.0 or vo_stub.rms_dbfs > -60.0:
        raise SystemExit("FAIL: VO stub waveform is no longer placeholder-quiet")


def validate_listening_links(stats: list[WaveformStat], queue_rows: list[dict[str, str]]) -> None:
    by_asset = {row["asset_or_config"].strip(): row for row in queue_rows}
    for asset_path, expected in REQUIRED_LINKS.items():
        row = by_asset.get(asset_path)
        if row is None:
            raise SystemExit(f"FAIL: listening queue missing waveform-linked asset: {asset_path}")
        priority, target = expected
        if row["priority"].strip() != priority or row["target"].strip() != target:
            raise SystemExit(f"FAIL: listening queue bad link for {asset_path}")
        if asset_path.endswith("VOStub_Chen_Log01_EN.wav"):
            if row["status"].strip() != "PLACEHOLDER_BLOCKED":
                raise SystemExit("FAIL: VO stub must remain PLACEHOLDER_BLOCKED")
        elif row["status"].strip() != "PENDING_VERIFICATION":
            raise SystemExit(f"FAIL: listening queue waveform-linked row must remain pending: {asset_path}")

    stat_paths = {stat.path for stat in stats}
    for asset_path in REQUIRED_LINKS:
        if asset_path not in stat_paths:
            raise SystemExit(f"FAIL: waveform stats missing linked listening asset: {asset_path}")


def validate_proof_index_links(stats: list[WaveformStat], proof_rows: list[dict[str, str]]) -> None:
    by_artifact = {row["ArtifactPath"].strip().replace("\\", "/"): row for row in proof_rows}
    stats_artifact = "Docs/AssetAudit/AudioVisual/audio_preview_waveform_stats_20260605.csv"
    if stats_artifact not in by_artifact:
        raise SystemExit("FAIL: proof artifact index missing waveform stats CSV")

    for stat in stats:
        row = by_artifact.get(stat.preview_png)
        if row is None:
            raise SystemExit(f"FAIL: proof artifact index missing waveform preview: {stat.preview_png}")
        if row["EvidenceClass"].strip() != "AUDIO_WAVEFORM_QA":
            raise SystemExit(f"FAIL: waveform preview must remain AUDIO_WAVEFORM_QA: {stat.preview_png}")
        reject_as = row["RejectAs"].strip().lower()
        if "acceptance" not in reject_as:
            raise SystemExit(f"FAIL: waveform preview must reject acceptance boundary: {stat.preview_png}")


def validate_audio_waveform_proof_artifacts() -> list[WaveformStat]:
    stats = parse_stats(load_csv(STATS_PATH, REQUIRED_STATS_COLUMNS))
    queue_rows = load_csv(LISTENING_QUEUE_PATH, REQUIRED_QUEUE_COLUMNS)
    proof_rows = load_csv(PROOF_INDEX_PATH, REQUIRED_PROOF_COLUMNS)
    validate_stats(stats)
    validate_listening_links(stats, queue_rows)
    validate_proof_index_links(stats, proof_rows)
    return stats


def main() -> None:
    stats = validate_audio_waveform_proof_artifacts()
    linked_count = len(REQUIRED_LINKS)
    print(f"AUDIO_WAVEFORM_PROOF_ARTIFACTS_OK stats={len(stats)} linked={linked_count} placeholder_blocked=1")


if __name__ == "__main__":
    main()
