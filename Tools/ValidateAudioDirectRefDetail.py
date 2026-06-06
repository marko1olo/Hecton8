#!/usr/bin/env python3
"""Validate the player-prefab audio direct-reference detail table."""

from __future__ import annotations

import csv
from collections import Counter
from pathlib import Path

from ValidateAudioSceneStaticRoute import count_categories, scan_player_direct_audio_refs


ROOT = Path(__file__).resolve().parents[1]
CSV_PATH = ROOT / "Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.csv"
MD_PATH = ROOT / "Docs/AssetAudit/AUDIO_DIRECT_REF_DETAIL_20260605.md"
PLAYER_PREFAB_PATH = ROOT / "Assets/_Project/Prefabs/Player.prefab"
SIDECAR_CAVEAT_PATHS = (
    ROOT / "Docs/AssetAudit/ASSET_GUID_REFERENCE_MATRIX_20260605.md",
    ROOT / "Docs/AssetAudit/ASSET_GUID_ACTIVE_ROUTE_TRIAGE_20260605.md",
    ROOT / "Docs/AssetAudit/AUDIO_PROFILE_USAGE_REVIEW_20260605.md",
    ROOT / "Docs/Audio/README.md",
    ROOT / "Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md",
    ROOT / "Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md",
    ROOT / "Docs/Reports/AssetSystem_20260605/ASSET_FRONT_CONTROLLER_SYNTHESIS_20260605.md",
    ROOT / "Docs/Reports/AssetSystem_20260605/ASSET_WORKER_BOARD_20260605.md",
)

EXPECTED_COLUMNS = (
    "Priority",
    "SourceAsset",
    "SourceLine",
    "CuePath",
    "CueId",
    "CueClass",
    "DurationSec",
    "LoadType",
    "Compression",
    "Quality",
    "DirectRefContext",
    "RemediationCategory",
    "CurrentRoute",
    "RequiredOwner",
    "RequiredAction",
    "ProofRequired",
    "EvidenceClass",
    "Disposition",
)
EXPECTED_COUNTS = {
    ("P1", "P1_FOOTSTEP_DIRECT_REF_OWNER_BLOCKED"): 20,
    ("P1", "P1_UI_DIRECT_REF_AUDIBILITY_BLOCKED"): 4,
}
EXPECTED_TOTAL = 24


def display_path(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def load_rows(path: Path = CSV_PATH) -> list[dict[str, str]]:
    if not path.exists():
        raise SystemExit(f"FAIL: missing audio direct-ref detail CSV: {display_path(path)}")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        headers = tuple(reader.fieldnames or ())
        missing = [column for column in EXPECTED_COLUMNS if column not in headers]
        if missing:
            raise SystemExit(f"FAIL: audio direct-ref CSV missing column(s): {', '.join(missing)}")
        rows = [{column: (row.get(column) or "").strip() for column in EXPECTED_COLUMNS} for row in reader]

    if len(rows) != EXPECTED_TOTAL:
        raise SystemExit(f"FAIL: expected {EXPECTED_TOTAL} audio direct-ref rows, got {len(rows)}")
    return rows


def load_text(path: Path = MD_PATH) -> str:
    if not path.exists():
        raise SystemExit(f"FAIL: missing audio direct-ref markdown: {display_path(path)}")
    return path.read_text(encoding="utf-8")


def require_float(row: dict[str, str], key: str, minimum: float, row_id: str) -> float:
    try:
        value = float(row[key])
    except ValueError as exc:
        raise SystemExit(f"FAIL: {row_id} {key} must be float: {row[key]}") from exc
    if value < minimum:
        raise SystemExit(f"FAIL: {row_id} {key} below {minimum}: {value}")
    return value


def require_int(row: dict[str, str], key: str, row_id: str) -> int:
    try:
        value = int(row[key])
    except ValueError as exc:
        raise SystemExit(f"FAIL: {row_id} {key} must be int: {row[key]}") from exc
    if value <= 0:
        raise SystemExit(f"FAIL: {row_id} {key} must be positive: {value}")
    return value


def validate_rows(rows: list[dict[str, str]], root: Path = ROOT) -> Counter[tuple[str, str]]:
    counts: Counter[tuple[str, str]] = Counter()
    cue_counts: Counter[str] = Counter()
    seen_contexts: set[str] = set()

    for row in rows:
        row_id = f"{row['SourceLine']}:{row['CueId']}"
        for column in EXPECTED_COLUMNS:
            if not row[column]:
                raise SystemExit(f"FAIL: {row_id} empty {column}")

        if row["SourceAsset"] != "Assets/_Project/Prefabs/Player.prefab":
            raise SystemExit(f"FAIL: {row_id} unexpected source asset: {row['SourceAsset']}")
        if row["CueClass"] != "DirectPrefabAudioRef":
            raise SystemExit(f"FAIL: {row_id} cue class drift: {row['CueClass']}")
        if row["EvidenceClass"] != "STATIC_SOURCE":
            raise SystemExit(f"FAIL: {row_id} evidence class drift: {row['EvidenceClass']}")
        if row["DirectRefContext"] in seen_contexts:
            raise SystemExit(f"FAIL: duplicate direct-ref context: {row['DirectRefContext']}")
        seen_contexts.add(row["DirectRefContext"])

        source_line = require_int(row, "SourceLine", row_id)
        duration = require_float(row, "DurationSec", 0.001, row_id)
        require_float(row, "Quality", 0.0, row_id)

        cue_path = root / row["CuePath"]
        if not cue_path.exists():
            raise SystemExit(f"FAIL: {row_id} missing cue path: {row['CuePath']}")

        proof_required = row["ProofRequired"]
        if "Prefab readback" not in proof_required or "0 B/frame proof" not in proof_required:
            raise SystemExit(f"FAIL: {row_id} proof requirement lost readback/runtime GC boundary")
        if row["RequiredOwner"] != "Player/audio lifecycle owner":
            raise SystemExit(f"FAIL: {row_id} required owner drift")
        if "Addressables" not in row["RequiredAction"]:
            raise SystemExit(f"FAIL: {row_id} required action must keep Addressables/exception ownership")

        counts[(row["Priority"], row["Disposition"])] += 1
        cue_counts[row["CueId"]] += 1

        if row["CuePath"].endswith("Underwater Ambient.wav"):
            raise SystemExit(f"FAIL: {row_id} stale underwater ambient direct-ref row; current Player.prefab static scan has no ambient direct refs")
        elif row["CuePath"].endswith("dive_splash.wav"):
            raise SystemExit(f"FAIL: {row_id} stale dive splash direct-ref row; current Player.prefab static scan has no splash direct refs")
        elif "Footsteps/" in row["CuePath"]:
            if row["Priority"] != "P1" or row["Disposition"] != "P1_FOOTSTEP_DIRECT_REF_OWNER_BLOCKED":
                raise SystemExit(f"FAIL: {row_id} footstep disposition drift")
            if row["RemediationCategory"] != "short_sfx" or row["Compression"] != "ADPCM":
                raise SystemExit(f"FAIL: {row_id} footstep classification drift")
            if duration > 1.1:
                raise SystemExit(f"FAIL: {row_id} footstep duration outside expected short SFX range")
        elif "/Audio/UI/" in row["CuePath"]:
            if row["Priority"] != "P1" or row["Disposition"] != "P1_UI_DIRECT_REF_AUDIBILITY_BLOCKED":
                raise SystemExit(f"FAIL: {row_id} UI disposition drift")
            if row["RemediationCategory"] != "ui_feedback":
                raise SystemExit(f"FAIL: {row_id} UI remediation category drift")
        else:
            raise SystemExit(f"FAIL: {row_id} unexpected direct-ref cue path: {row['CuePath']}")

    if counts != Counter(EXPECTED_COUNTS):
        raise SystemExit(f"FAIL: audio direct-ref disposition counts drift: {dict(counts)}")
    if cue_counts["UNDERWATER_AMBIENT"] != 0 or cue_counts["DIVE_SPLASH"] != 0:
        raise SystemExit("FAIL: P0 direct-ref duplicate counts drift")
    return counts


def validate_prefab_alignment(rows: list[dict[str, str]], root: Path = ROOT) -> dict[str, int]:
    blockers, _notes, direct_refs = scan_player_direct_audio_refs(PLAYER_PREFAB_PATH, root)
    unresolved = [blocker for blocker in blockers if "unresolved-guid" in blocker]
    if unresolved:
        raise SystemExit(f"FAIL: current Player.prefab direct audio scan has unresolved GUID(s): {'; '.join(unresolved)}")

    row_keys = Counter((int(row["SourceLine"]), row["CuePath"]) for row in rows)
    ref_keys = Counter((ref.line, ref.asset_path) for ref in direct_refs)
    if row_keys != ref_keys:
        missing = sorted((row_key, count) for row_key, count in (ref_keys - row_keys).items())
        stale = sorted((row_key, count) for row_key, count in (row_keys - ref_keys).items())
        raise SystemExit(f"FAIL: audio direct-ref detail no longer matches Player.prefab scan: missing={missing} stale={stale}")

    category_counts = count_categories(direct_refs)
    if category_counts["underwater_ambient"] != 0:
        raise SystemExit("FAIL: current Player.prefab scan still has underwater ambient direct refs")
    if category_counts["dive_splash"] != 0:
        raise SystemExit("FAIL: current Player.prefab scan still has dive splash direct refs")
    if len(direct_refs) != EXPECTED_TOTAL:
        raise SystemExit(f"FAIL: expected {EXPECTED_TOTAL} current Player.prefab direct refs, got {len(direct_refs)}")
    return category_counts


def validate_companion_doc(text: str | None = None) -> None:
    text = load_text() if text is None else text
    required_terms = (
        "This file proves serialized direct refs only.",
        "`P1_FOOTSTEP_DIRECT_REF_OWNER_BLOCKED` | 20",
        "`P1_UI_DIRECT_REF_AUDIBILITY_BLOCKED` | 4",
        "Current static prefab scan reports `0` direct `Underwater Ambient.wav` refs.",
        "Current static prefab scan reports `0` direct `dive_splash.wav` refs.",
        "Do not treat direct prefab serialization as Addressables ownership.",
        "Final status: `PENDING VERIFICATION`.",
    )
    for term in required_terms:
        if term not in text:
            raise SystemExit(f"FAIL: audio direct-ref markdown missing term: {term}")


def validate_sidecar_caveats(paths: tuple[Path, ...] = SIDECAR_CAVEAT_PATHS) -> None:
    required_terms = (
        "stale",
        "Underwater Ambient.wav",
        "dive_splash.wav",
        "AUDIO_DIRECT_REF_DETAIL",
    )
    for path in paths:
        if not path.exists():
            raise SystemExit(f"FAIL: missing audio direct-ref stale-sidecar caveat file: {display_path(path)}")
        text = path.read_text(encoding="utf-8")
        lowered = text.lower()
        for term in required_terms:
            if term.lower() not in lowered:
                raise SystemExit(f"FAIL: {display_path(path)} missing stale-sidecar caveat term: {term}")


def validate_audio_direct_ref_detail() -> Counter[tuple[str, str]]:
    rows = load_rows()
    counts = validate_rows(rows)
    validate_prefab_alignment(rows)
    validate_companion_doc()
    validate_sidecar_caveats()
    return counts


def main() -> None:
    counts = validate_audio_direct_ref_detail()
    print(
        "AUDIO_DIRECT_REF_DETAIL_OK "
        f"rows={EXPECTED_TOTAL} p0=0 "
        f"footsteps={counts[('P1', 'P1_FOOTSTEP_DIRECT_REF_OWNER_BLOCKED')]} ui={counts[('P1', 'P1_UI_DIRECT_REF_AUDIBILITY_BLOCKED')]}"
    )


if __name__ == "__main__":
    main()
