#!/usr/bin/env python3
"""Run the AppliedLore authoring/blob/placement integration gates together."""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass
from pathlib import Path

TOOL_DIR = Path(__file__).resolve().parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

import AppliedLoreBlobDeltaAudit as blob_delta  # noqa: E402
import AppliedLoreImporter as importer  # noqa: E402
import AppliedLorePacketCoverageAudit as packet_coverage  # noqa: E402
import AppliedLorePageExporter as page_exporter  # noqa: E402
import AppliedLoreRuntimeAudit as runtime_audit  # noqa: E402
import AppliedLoreRouteCardExporter as route_exporter  # noqa: E402
import AppliedLoreScenePlacementDeltaAudit as scene_delta  # noqa: E402
import AppliedLoreSignalRouteAudit as signal_route  # noqa: E402
import ValidateAppliedLoreAuthoringBridge as authoring_bridge  # noqa: E402


@dataclass(frozen=True)
class GateResult:
    name: str
    clean: bool
    summary: str
    payload: dict[str, object]


@dataclass(frozen=True)
class PreflightResult:
    gates: tuple[GateResult, ...]

    @property
    def clean(self) -> bool:
        return all(gate.clean for gate in self.gates)


def run_authoring_bridge_gate(
    root: Path,
    *,
    max_issues: int,
    include_all_packet_json: bool,
    strict_localized_text: bool,
) -> GateResult:
    try:
        stats = authoring_bridge.validate_authoring_bridge(
            root,
            max_issues=max_issues,
            include_all_packet_json=include_all_packet_json,
            strict_localized_text=strict_localized_text,
        )
    except authoring_bridge.AppliedLoreAuthoringBridgeError as exc:
        return GateResult(
            name="authoring_bridge",
            clean=False,
            summary=str(exc),
            payload={"clean": False, "error": str(exc), "issues": [str(exc)]},
        )

    clean = not stats.issues
    scope = "all_packet_json" if include_all_packet_json else "canonical_manifests"
    strict = "strict_text" if strict_localized_text else "shape_only"
    summary = (
        f"scope={scope} text={strict} packets={stats.packets} rows={stats.localized_rows} "
        f"runtime_fields={stats.runtime_fields} publication_article_fields={stats.publication_article_fields} "
        f"issues={len(stats.issues)}"
    )
    return GateResult(
        name="authoring_bridge",
        clean=clean,
        summary=summary,
        payload={
            "clean": clean,
            "scope": scope,
            "strict_localized_text": strict_localized_text,
            "packets": stats.packets,
            "localized_rows": stats.localized_rows,
            "runtime_fields": stats.runtime_fields,
            "publication_article_fields": stats.publication_article_fields,
            "issues": list(stats.issues),
        },
    )


def run_blob_delta_gate(root: Path, *, max_packets: int, max_locales: int) -> GateResult:
    try:
        packet_stats = blob_delta.run(root)
        route_stats = blob_delta.run_route(root)
    except runtime_audit.AuditFailure as exc:
        return GateResult(
            name="blob_delta",
            clean=False,
            summary=str(exc),
            payload={"clean": False, "error": str(exc)},
        )

    artifact_state = blob_delta.collect_artifact_state(root)
    clean = packet_stats.is_clean and route_stats.is_clean and artifact_state.is_current
    summary = (
        f"packet_missing_rows={packet_stats.missing_rows} packet_extra_rows={packet_stats.extra_rows} "
        f"packet_duplicate_rows={packet_stats.duplicate_blob_rows} route_missing_rows={route_stats.missing_rows} "
        f"route_extra_rows={route_stats.extra_rows} route_duplicate_rows={route_stats.duplicate_blob_rows} "
        f"blob_older_than_csv={str(artifact_state.blob_older_than_csv).lower()} "
        f"blob_older_than_route_csv={str(artifact_state.blob_older_than_route_csv).lower()} "
        f"artifact_current={str(artifact_state.is_current).lower()}"
    )
    return GateResult(
        name="blob_delta",
        clean=clean,
        summary=summary,
        payload=blob_delta.combined_delta_to_json_payload(
            packet_stats,
            route_stats,
            artifact_state,
            max_packets=max_packets,
            max_locales=max_locales,
        ),
    )


def run_generated_artifacts_gate(root: Path) -> GateResult:
    try:
        import_stats = importer.check_import_outputs(root)
        publication_stats = page_exporter.check_publication_freshness(root, packet_glob="P*")
        route_count, route_current = route_exporter.route_card_export_current(root)
    except (OSError, ValueError) as exc:
        return GateResult(
            name="generated_artifacts",
            clean=False,
            summary=str(exc),
            payload={"clean": False, "error": str(exc)},
        )

    clean = (
        import_stats.stale_files == 0
        and import_stats.missing_files == 0
        and publication_stats.stale_files == 0
        and publication_stats.missing_files == 0
        and publication_stats.disabled_generated_pages == 0
        and route_current
    )
    summary = (
        f"import_checked={import_stats.checked_files} import_stale={import_stats.stale_files} "
        f"import_missing={import_stats.missing_files} publication_checked={publication_stats.checked_files} "
        f"publication_stale={publication_stats.stale_files} publication_missing={publication_stats.missing_files} "
        f"publication_disabled={publication_stats.disabled_generated_pages} "
        f"route_cards={route_count} route_export_current={str(route_current).lower()}"
    )
    return GateResult(
        name="generated_artifacts",
        clean=clean,
        summary=summary,
        payload={
            "clean": clean,
            "import": {
                "checked_files": import_stats.checked_files,
                "stale_files": import_stats.stale_files,
                "missing_files": import_stats.missing_files,
                "sample_issues": list(import_stats.sample_issues),
            },
            "publication": {
                "checked_files": publication_stats.checked_files,
                "stale_files": publication_stats.stale_files,
                "missing_files": publication_stats.missing_files,
                "disabled_generated_pages": publication_stats.disabled_generated_pages,
                "sample_issues": list(publication_stats.sample_issues),
            },
            "route_cards": {
                "count": route_count,
                "export_current": route_current,
            },
        },
    )


def run_packet_inventory_gate(root: Path, *, sample_limit: int) -> GateResult:
    try:
        stats = packet_coverage.inventory_sources(root, sample_limit=sample_limit)
    except packet_coverage.AppliedLoreCoverageError as exc:
        return GateResult(
            name="packet_inventory",
            clean=False,
            summary=str(exc),
            payload={"clean": False, "error": str(exc)},
        )

    clean = stats.baked_missing_source_packets == 0 and stats.canonical_ready_unbaked_packets == 0
    summary = (
        f"source_packets={stats.source_packets} baked_packets={stats.baked_packets} "
        f"unbaked_packets={stats.unbaked_packets} "
        f"baked_missing_source_packets={stats.baked_missing_source_packets} "
        f"canonical_ready_unbaked_packets={stats.canonical_ready_unbaked_packets} "
        f"canonical_not_ready_unbaked_packets={stats.canonical_not_ready_unbaked_packets}"
    )
    return GateResult(
        name="packet_inventory",
        clean=clean,
        summary=summary,
        payload={
            "clean": clean,
            "source_packets": stats.source_packets,
            "baked_packets": stats.baked_packets,
            "unbaked_packets": stats.unbaked_packets,
            "baked_missing_source_packets": stats.baked_missing_source_packets,
            "canonical_ready_unbaked_packets": stats.canonical_ready_unbaked_packets,
            "canonical_not_ready_unbaked_packets": stats.canonical_not_ready_unbaked_packets,
            "sample_unbaked": list(stats.sample_unbaked),
            "sample_canonical_ready_unbaked": list(stats.sample_canonical_ready_unbaked),
        },
    )


def run_scene_placement_gate(root: Path, *, max_rows: int) -> GateResult:
    try:
        delta = scene_delta.run(root)
    except runtime_audit.AuditFailure as exc:
        return GateResult(
            name="scene_placement",
            clean=False,
            summary=str(exc),
            payload={"clean": False, "error": str(exc)},
        )

    clean = not delta.issues
    summary = f"planned={delta.total_rows} covered={delta.covered_rows} missing={len(delta.issues)}"
    payload = scene_delta.delta_to_json_payload(delta, max_rows=max_rows)
    payload["clean"] = clean
    return GateResult(name="scene_placement", clean=clean, summary=summary, payload=payload)


def run_signal_route_gate(root: Path, *, max_issues: int) -> GateResult:
    stats = signal_route.validate_signal_route(root, max_issues=max_issues)
    summary = f"checked_files={stats.checked_files} checked_methods={stats.checked_methods} issues={len(stats.issues)}"
    return GateResult(
        name="signal_route",
        clean=stats.clean,
        summary=summary,
        payload=signal_route.stats_to_payload(stats),
    )


def run_preflight(
    root: Path,
    *,
    max_issues: int = 80,
    max_packets: int = 40,
    max_locales: int = 8,
    max_scene_rows: int = 40,
    include_all_packet_json: bool = False,
    strict_localized_text: bool = False,
) -> PreflightResult:
    root = root.resolve()
    return PreflightResult(
        gates=(
            run_authoring_bridge_gate(
                root,
                max_issues=max_issues,
                include_all_packet_json=include_all_packet_json,
                strict_localized_text=strict_localized_text,
            ),
            run_generated_artifacts_gate(root),
            run_packet_inventory_gate(root, sample_limit=max_packets),
            run_signal_route_gate(root, max_issues=max_issues),
            run_blob_delta_gate(root, max_packets=max_packets, max_locales=max_locales),
            run_scene_placement_gate(root, max_rows=max_scene_rows),
        )
    )


def result_to_json_payload(result: PreflightResult) -> dict[str, object]:
    return {
        "clean": result.clean,
        "gates": {
            gate.name: {
                "clean": gate.clean,
                "summary": gate.summary,
                "payload": gate.payload,
            }
            for gate in result.gates
        },
    }


def render_result(result: PreflightResult) -> str:
    status = "OK" if result.clean else "failed"
    lines = [f"AppliedLore integration preflight {status}:"]
    for gate in result.gates:
        gate_status = "OK" if gate.clean else "FAIL"
        lines.append(f"- {gate.name} {gate_status}: {gate.summary}")
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Hecton8 repository root")
    parser.add_argument("--max-issues", type=int, default=80)
    parser.add_argument("--max-packets", type=int, default=40)
    parser.add_argument("--max-locales", type=int, default=8)
    parser.add_argument("--max-scene-rows", type=int, default=40)
    parser.add_argument(
        "--include-all-packet-json",
        action="store_true",
        help="Scan every packet JSON, including authoring backlog not selected by canonical manifests.",
    )
    parser.add_argument(
        "--strict-localized-text",
        action="store_true",
        help="Fail on LOC HOLD, placeholders, and mojibake in player-facing localized fields.",
    )
    parser.add_argument("--json", action="store_true", help="Write a machine-readable payload to stdout")
    args = parser.parse_args(argv)

    result = run_preflight(
        Path(args.root),
        max_issues=max(args.max_issues, 0),
        max_packets=max(args.max_packets, 0),
        max_locales=max(args.max_locales, 0),
        max_scene_rows=max(args.max_scene_rows, 0),
        include_all_packet_json=args.include_all_packet_json,
        strict_localized_text=args.strict_localized_text,
    )

    if args.json:
        print(json.dumps(result_to_json_payload(result), ensure_ascii=False, indent=2))
    else:
        output = render_result(result)
        if result.clean:
            print(output)
        else:
            print(output, file=sys.stderr)
    return 0 if result.clean else 1


if __name__ == "__main__":
    raise SystemExit(main())
