#!/usr/bin/env python3
"""Report AppliedLore CSV-vs-h8bin row deltas with packet/locale detail."""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Protocol, Sequence

TOOL_DIR = Path(__file__).resolve().parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

import AppliedLoreRuntimeAudit as runtime_audit  # noqa: E402


CSV_RELATIVE_PATH = Path("Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv")
ROUTE_CSV_RELATIVE_PATH = Path("Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv")
BLOB_RELATIVE_PATH = Path("Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin")
DATA_MONOLITH_BAKE_METHOD = "Hecton8.EditorValidation.H8DataMonolithCompiler.BakeFromCommandLine"


class RowLike(Protocol):
    packet_id: str
    locale: str
    release_set_id: str
    line_number: int

    @property
    def key(self) -> tuple[int, int]:
        ...


class RecordLike(Protocol):
    index: int

    @property
    def key(self) -> tuple[int, int]:
        ...


class RouteSourceLike(Protocol):
    route_card_id: str
    route_card_hash: int
    line_number: int


class RouteRecordLike(Protocol):
    route_card_hash: int
    index: int


@dataclass(frozen=True)
class MissingPacketGroup:
    packet_id: str
    release_set_id: str
    locales: tuple[str, ...]
    first_line: int


@dataclass(frozen=True)
class ArtifactState:
    csv_path: str
    route_csv_path: str
    blob_path: str
    csv_exists: bool
    route_csv_exists: bool
    blob_exists: bool
    csv_mtime_ns: int | None
    route_csv_mtime_ns: int | None
    blob_mtime_ns: int | None

    @property
    def blob_older_than_csv(self) -> bool:
        if self.csv_mtime_ns is None or self.blob_mtime_ns is None:
            return False
        return self.blob_mtime_ns < self.csv_mtime_ns

    @property
    def blob_older_than_route_csv(self) -> bool:
        if self.route_csv_mtime_ns is None or self.blob_mtime_ns is None:
            return False
        return self.blob_mtime_ns < self.route_csv_mtime_ns

    @property
    def is_current(self) -> bool:
        return (
            self.csv_exists
            and self.route_csv_exists
            and self.blob_exists
            and not self.blob_older_than_csv
            and not self.blob_older_than_route_csv
        )

    @property
    def stale_reasons(self) -> tuple[str, ...]:
        reasons: list[str] = []
        if not self.csv_exists:
            reasons.append("packet_csv_missing")
        if not self.route_csv_exists:
            reasons.append("route_csv_missing")
        if not self.blob_exists:
            reasons.append("blob_missing")
        if self.blob_older_than_csv:
            reasons.append("blob_older_than_packet_csv")
        if self.blob_older_than_route_csv:
            reasons.append("blob_older_than_route_csv")
        return tuple(reasons)


@dataclass(frozen=True)
class BlobDeltaStats:
    csv_rows: int
    blob_rows: int
    missing_rows: int
    extra_rows: int
    duplicate_blob_rows: int
    missing_packets: tuple[MissingPacketGroup, ...]
    extra_keys: tuple[tuple[int, int], ...]
    duplicate_keys: tuple[tuple[int, int], ...]

    @property
    def is_clean(self) -> bool:
        return self.missing_rows == 0 and self.extra_rows == 0 and self.duplicate_blob_rows == 0


@dataclass(frozen=True)
class MissingRouteGroup:
    route_card_id: str
    route_card_hash: int
    first_line: int


@dataclass(frozen=True)
class RouteDeltaStats:
    source_rows: int
    blob_rows: int
    missing_rows: int
    extra_rows: int
    duplicate_blob_rows: int
    missing_routes: tuple[MissingRouteGroup, ...]
    extra_hashes: tuple[int, ...]
    duplicate_hashes: tuple[int, ...]

    @property
    def is_clean(self) -> bool:
        return self.missing_rows == 0 and self.extra_rows == 0 and self.duplicate_blob_rows == 0


def load_csv_rows(root: Path) -> list[runtime_audit.CsvPacketRow]:
    return runtime_audit.load_csv(root / CSV_RELATIVE_PATH)


def load_blob_records(root: Path) -> list[runtime_audit.AppliedLoreRecord]:
    data, _entries, _localization, applied, _routes = runtime_audit.parse_blob(root / BLOB_RELATIVE_PATH)
    return runtime_audit.parse_applied_records(data, applied)


def load_route_source_records(root: Path) -> list[runtime_audit.RouteSourceRecord]:
    return runtime_audit.load_route_source_records(root)


def load_blob_route_records(root: Path) -> list[runtime_audit.AppliedLoreRouteRecord]:
    data, _entries, _localization, _applied, routes = runtime_audit.parse_blob(root / BLOB_RELATIVE_PATH)
    return runtime_audit.parse_applied_route_records(data, routes)


def file_mtime_ns(path: Path) -> int | None:
    if not path.exists():
        return None
    return path.stat().st_mtime_ns


def collect_artifact_state(root: Path) -> ArtifactState:
    csv_path = root / CSV_RELATIVE_PATH
    route_csv_path = root / ROUTE_CSV_RELATIVE_PATH
    blob_path = root / BLOB_RELATIVE_PATH
    return ArtifactState(
        csv_path=CSV_RELATIVE_PATH.as_posix(),
        route_csv_path=ROUTE_CSV_RELATIVE_PATH.as_posix(),
        blob_path=BLOB_RELATIVE_PATH.as_posix(),
        csv_exists=csv_path.exists(),
        route_csv_exists=route_csv_path.exists(),
        blob_exists=blob_path.exists(),
        csv_mtime_ns=file_mtime_ns(csv_path),
        route_csv_mtime_ns=file_mtime_ns(route_csv_path),
        blob_mtime_ns=file_mtime_ns(blob_path),
    )


def compute_blob_delta(rows: Sequence[RowLike], records: Sequence[RecordLike]) -> BlobDeltaStats:
    expected_by_key = {row.key: row for row in rows}
    actual_seen: set[tuple[int, int]] = set()
    duplicate_keys: list[tuple[int, int]] = []
    for record in records:
        key = record.key
        if key in actual_seen:
            duplicate_keys.append(key)
            continue
        actual_seen.add(key)

    missing_keys = sorted(set(expected_by_key).difference(actual_seen))
    extra_keys = sorted(actual_seen.difference(expected_by_key))

    missing_by_packet: dict[str, list[RowLike]] = {}
    for key in missing_keys:
        row = expected_by_key[key]
        missing_by_packet.setdefault(row.packet_id, []).append(row)

    missing_packets: list[MissingPacketGroup] = []
    for packet_id, packet_rows in missing_by_packet.items():
        ordered_rows = sorted(packet_rows, key=lambda item: item.locale)
        missing_packets.append(
            MissingPacketGroup(
                packet_id=packet_id,
                release_set_id=ordered_rows[0].release_set_id,
                locales=tuple(row.locale for row in ordered_rows),
                first_line=min(row.line_number for row in ordered_rows),
            )
        )
    missing_packets.sort(key=lambda group: (group.first_line, group.packet_id))

    return BlobDeltaStats(
        csv_rows=len(rows),
        blob_rows=len(records),
        missing_rows=len(missing_keys),
        extra_rows=len(extra_keys),
        duplicate_blob_rows=len(duplicate_keys),
        missing_packets=tuple(missing_packets),
        extra_keys=tuple(extra_keys),
        duplicate_keys=tuple(sorted(set(duplicate_keys))),
    )


def compute_route_delta(
    source_records: Sequence[RouteSourceLike],
    blob_records: Sequence[RouteRecordLike],
) -> RouteDeltaStats:
    expected_by_hash = {record.route_card_hash: record for record in source_records}
    actual_seen: set[int] = set()
    duplicate_hashes: list[int] = []
    for record in blob_records:
        route_hash = record.route_card_hash
        if route_hash in actual_seen:
            duplicate_hashes.append(route_hash)
            continue
        actual_seen.add(route_hash)

    missing_hashes = sorted(set(expected_by_hash).difference(actual_seen))
    extra_hashes = sorted(actual_seen.difference(expected_by_hash))
    missing_routes = tuple(
        sorted(
            (
                MissingRouteGroup(
                    route_card_id=expected_by_hash[route_hash].route_card_id,
                    route_card_hash=route_hash,
                    first_line=expected_by_hash[route_hash].line_number,
                )
                for route_hash in missing_hashes
            ),
            key=lambda item: (item.first_line, item.route_card_id),
        )
    )

    return RouteDeltaStats(
        source_rows=len(source_records),
        blob_rows=len(blob_records),
        missing_rows=len(missing_hashes),
        extra_rows=len(extra_hashes),
        duplicate_blob_rows=len(duplicate_hashes),
        missing_routes=missing_routes,
        extra_hashes=tuple(extra_hashes),
        duplicate_hashes=tuple(sorted(set(duplicate_hashes))),
    )


def format_hash_pair(key: tuple[int, int]) -> str:
    return f"packet=0x{key[0]:08X}/locale=0x{key[1]:08X}"


def format_hash(value: int) -> str:
    return f"0x{value:08X}"


def format_sample(values: Sequence[str], *, limit: int) -> str:
    samples = ",".join(values[:limit])
    more = "" if len(values) <= limit else f"; +{len(values) - limit} more"
    return f"count={len(values)} samples={samples}{more}"


def format_mtime(mtime_ns: int | None) -> str:
    if mtime_ns is None:
        return "missing"
    return datetime.fromtimestamp(mtime_ns / 1_000_000_000, tz=timezone.utc).isoformat().replace("+00:00", "Z")


def format_artifact_state(state: ArtifactState) -> str:
    return (
        f"- artifact csv={state.csv_path} csv_exists={str(state.csv_exists).lower()} "
        f"csv_mtime={format_mtime(state.csv_mtime_ns)} "
        f"route_csv={state.route_csv_path} route_csv_exists={str(state.route_csv_exists).lower()} "
        f"route_csv_mtime={format_mtime(state.route_csv_mtime_ns)} "
        f"blob={state.blob_path} blob_exists={str(state.blob_exists).lower()} "
        f"blob_mtime={format_mtime(state.blob_mtime_ns)} "
        f"blob_older_than_csv={str(state.blob_older_than_csv).lower()} "
        f"blob_older_than_route_csv={str(state.blob_older_than_route_csv).lower()} "
        f"artifact_current={str(state.is_current).lower()} "
        f"rebake_method={DATA_MONOLITH_BAKE_METHOD}"
    )


def render_artifact_reasons(state: ArtifactState) -> list[str]:
    reasons = state.stale_reasons
    if not reasons:
        return []
    return [f"- artifact stale_reason={reason}" for reason in reasons]


def render_delta(
    stats: BlobDeltaStats,
    *,
    max_packets: int = 40,
    max_locales: int = 8,
    artifact_state: ArtifactState | None = None,
) -> str:
    artifact_current = artifact_state is None or artifact_state.is_current
    if stats.is_clean and artifact_current:
        lines = [
            "AppliedLore blob delta OK: "
            f"csv_rows={stats.csv_rows} blob_rows={stats.blob_rows}"
        ]
        if artifact_state is not None:
            lines.append(format_artifact_state(artifact_state))
        return "\n".join(lines)

    lines = [
        "AppliedLore blob delta failed: "
        f"csv_rows={stats.csv_rows} blob_rows={stats.blob_rows} "
        f"missing_rows={stats.missing_rows} extra_rows={stats.extra_rows} "
        f"duplicate_blob_rows={stats.duplicate_blob_rows}"
    ]
    if artifact_state is not None:
        lines.append(format_artifact_state(artifact_state))
        lines.extend(render_artifact_reasons(artifact_state))
    for group in stats.missing_packets[:max_packets]:
        lines.append(
            f"- missing packet={group.packet_id} release_set={group.release_set_id} "
            f"first_csv_line={group.first_line} locales "
            f"{format_sample(group.locales, limit=max_locales)}"
        )
    if len(stats.missing_packets) > max_packets:
        lines.append(f"- ... {len(stats.missing_packets) - max_packets} more missing packets")

    if stats.extra_keys:
        samples = ", ".join(format_hash_pair(key) for key in stats.extra_keys[:max_packets])
        more = "" if len(stats.extra_keys) <= max_packets else f"; +{len(stats.extra_keys) - max_packets} more"
        lines.append(f"- extra blob keys count={len(stats.extra_keys)} samples={samples}{more}")
    if stats.duplicate_keys:
        samples = ", ".join(format_hash_pair(key) for key in stats.duplicate_keys[:max_packets])
        more = "" if len(stats.duplicate_keys) <= max_packets else f"; +{len(stats.duplicate_keys) - max_packets} more"
        lines.append(f"- duplicate blob keys count={len(stats.duplicate_keys)} samples={samples}{more}")
    return "\n".join(lines)


def render_route_delta(stats: RouteDeltaStats, *, max_routes: int = 40) -> str:
    if stats.is_clean:
        return (
            "AppliedLore route blob delta OK: "
            f"source_rows={stats.source_rows} blob_rows={stats.blob_rows}"
        )

    lines = [
        "AppliedLore route blob delta failed: "
        f"source_rows={stats.source_rows} blob_rows={stats.blob_rows} "
        f"missing_rows={stats.missing_rows} extra_rows={stats.extra_rows} "
        f"duplicate_blob_rows={stats.duplicate_blob_rows}"
    ]
    for group in stats.missing_routes[:max_routes]:
        lines.append(
            f"- missing route={group.route_card_id} hash={format_hash(group.route_card_hash)} "
            f"first_csv_line={group.first_line}"
        )
    if len(stats.missing_routes) > max_routes:
        lines.append(f"- ... {len(stats.missing_routes) - max_routes} more missing routes")

    if stats.extra_hashes:
        samples = ", ".join(format_hash(route_hash) for route_hash in stats.extra_hashes[:max_routes])
        more = "" if len(stats.extra_hashes) <= max_routes else f"; +{len(stats.extra_hashes) - max_routes} more"
        lines.append(f"- extra route hashes count={len(stats.extra_hashes)} samples={samples}{more}")
    if stats.duplicate_hashes:
        samples = ", ".join(format_hash(route_hash) for route_hash in stats.duplicate_hashes[:max_routes])
        more = "" if len(stats.duplicate_hashes) <= max_routes else f"; +{len(stats.duplicate_hashes) - max_routes} more"
        lines.append(f"- duplicate route hashes count={len(stats.duplicate_hashes)} samples={samples}{more}")
    return "\n".join(lines)


def artifact_state_to_payload(state: ArtifactState) -> dict[str, object]:
    return {
        "csv_path": state.csv_path,
        "route_csv_path": state.route_csv_path,
        "blob_path": state.blob_path,
        "csv_exists": state.csv_exists,
        "route_csv_exists": state.route_csv_exists,
        "blob_exists": state.blob_exists,
        "csv_mtime": format_mtime(state.csv_mtime_ns),
        "route_csv_mtime": format_mtime(state.route_csv_mtime_ns),
        "blob_mtime": format_mtime(state.blob_mtime_ns),
        "blob_older_than_csv": state.blob_older_than_csv,
        "blob_older_than_route_csv": state.blob_older_than_route_csv,
        "artifact_current": state.is_current,
        "stale_reasons": list(state.stale_reasons),
        "rebake_method": DATA_MONOLITH_BAKE_METHOD,
    }


def blob_delta_to_payload(
    stats: BlobDeltaStats,
    *,
    max_packets: int = 40,
    max_locales: int = 8,
) -> dict[str, object]:
    max_packets = max(max_packets, 0)
    max_locales = max(max_locales, 0)
    missing_packets = stats.missing_packets[:max_packets]
    return {
        "csv_rows": stats.csv_rows,
        "blob_rows": stats.blob_rows,
        "missing_rows": stats.missing_rows,
        "extra_rows": stats.extra_rows,
        "duplicate_blob_rows": stats.duplicate_blob_rows,
        "clean": stats.is_clean,
        "truncated_missing_packets": max(len(stats.missing_packets) - len(missing_packets), 0),
        "missing_packets": [
            {
                "packet": group.packet_id,
                "release_set": group.release_set_id,
                "first_csv_line": group.first_line,
                "locales": list(group.locales[:max_locales]),
                "locale_count": len(group.locales),
                "truncated_locales": max(len(group.locales) - max_locales, 0),
            }
            for group in missing_packets
        ],
        "extra_keys": [format_hash_pair(key) for key in stats.extra_keys[:max_packets]],
        "duplicate_keys": [format_hash_pair(key) for key in stats.duplicate_keys[:max_packets]],
    }


def route_delta_to_payload(stats: RouteDeltaStats, *, max_routes: int = 40) -> dict[str, object]:
    max_routes = max(max_routes, 0)
    missing_routes = stats.missing_routes[:max_routes]
    return {
        "source_rows": stats.source_rows,
        "blob_rows": stats.blob_rows,
        "missing_rows": stats.missing_rows,
        "extra_rows": stats.extra_rows,
        "duplicate_blob_rows": stats.duplicate_blob_rows,
        "clean": stats.is_clean,
        "truncated_missing_routes": max(len(stats.missing_routes) - len(missing_routes), 0),
        "missing_routes": [
            {
                "route": group.route_card_id,
                "hash": format_hash(group.route_card_hash),
                "first_csv_line": group.first_line,
            }
            for group in missing_routes
        ],
        "extra_hashes": [format_hash(route_hash) for route_hash in stats.extra_hashes[:max_routes]],
        "duplicate_hashes": [format_hash(route_hash) for route_hash in stats.duplicate_hashes[:max_routes]],
    }


def combined_delta_to_json_payload(
    stats: BlobDeltaStats,
    route_stats: RouteDeltaStats,
    artifact_state: ArtifactState,
    *,
    max_packets: int = 40,
    max_locales: int = 8,
) -> dict[str, object]:
    return {
        "clean": stats.is_clean and route_stats.is_clean and artifact_state.is_current,
        "artifact": artifact_state_to_payload(artifact_state),
        "packets": blob_delta_to_payload(stats, max_packets=max_packets, max_locales=max_locales),
        "routes": route_delta_to_payload(route_stats, max_routes=max_packets),
    }


def run(root: Path) -> BlobDeltaStats:
    rows = load_csv_rows(root)
    records = load_blob_records(root)
    return compute_blob_delta(rows, records)


def run_route(root: Path) -> RouteDeltaStats:
    source_records = load_route_source_records(root)
    blob_records = load_blob_route_records(root)
    return compute_route_delta(source_records, blob_records)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root")
    parser.add_argument("--max-packets", type=int, default=40)
    parser.add_argument("--max-locales", type=int, default=8)
    parser.add_argument("--json", action="store_true", help="Write a machine-readable delta payload to stdout")
    args = parser.parse_args(argv)

    try:
        stats = run(Path(args.root).resolve())
        route_stats = run_route(Path(args.root).resolve())
    except runtime_audit.AuditFailure as exc:
        print(f"AppliedLore blob delta failed:\n- {exc}", file=sys.stderr)
        return 1

    artifact_state = collect_artifact_state(Path(args.root).resolve())
    if args.json:
        print(
            json.dumps(
                combined_delta_to_json_payload(
                    stats,
                    route_stats,
                    artifact_state,
                    max_packets=args.max_packets,
                    max_locales=args.max_locales,
                ),
                ensure_ascii=False,
                indent=2,
            )
        )
        return 0 if stats.is_clean and route_stats.is_clean and artifact_state.is_current else 1

    output = render_delta(
        stats,
        max_packets=args.max_packets,
        max_locales=args.max_locales,
        artifact_state=artifact_state,
    )
    route_output = render_route_delta(route_stats, max_routes=args.max_packets)
    combined_output = output + "\n" + route_output
    if stats.is_clean and route_stats.is_clean and artifact_state.is_current:
        print(combined_output)
        return 0
    print(combined_output, file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
