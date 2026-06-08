#!/usr/bin/env python3
"""Explain AppliedLore scene placement coverage gaps.

This is a narrow companion to AppliedLoreRuntimeAudit. The runtime audit owns the
contract and the pass/fail gate; this tool turns the same plan into a complete
row-level delta so a stale scene placement bake is actionable.
"""

from __future__ import annotations

import argparse
import csv
import json
import sys
from dataclasses import dataclass
from pathlib import Path

import AppliedLoreRuntimeAudit as runtime_audit


PLAN_RELATIVE_PATH = Path("Docs/Lore/AppliedContent/binding_maps/RS001_RS010_scene_placement_plan.csv")


@dataclass(frozen=True)
class PlacementIssue:
    line_number: int
    packet_id: str
    component: str
    reason: str
    scene_path: str
    object_name: str
    source_prefab: str
    discovery_id: str
    depth_band: str
    zone_tag: str
    local_position: str


@dataclass(frozen=True)
class PlacementDelta:
    total_rows: int
    covered_rows: int
    issues: tuple[PlacementIssue, ...]


def read_text_or_none(path: Path) -> str | None:
    if not path.exists():
        return None
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return None


def count_yaml_scalar(scene_text: str, field_name: str, value: str) -> int:
    needle = f"{field_name}: {value}"
    return sum(1 for line in scene_text.splitlines() if line.strip() == needle)


def make_issue_from_row(row: dict[str, str], line_number: int, reason: str) -> PlacementIssue:
    return PlacementIssue(
        line_number=line_number,
        packet_id=runtime_audit.require_cell(row, "packet_id", line_number),
        component=runtime_audit.require_cell(row, "authoring_component", line_number),
        reason=reason,
        scene_path=runtime_audit.require_cell(row, "scene_path", line_number),
        object_name=runtime_audit.require_cell(row, "object_name", line_number),
        source_prefab=runtime_audit.require_cell(row, "source_prefab", line_number),
        discovery_id=row.get("discovery_id", ""),
        depth_band=row.get("depth_band", ""),
        zone_tag=row.get("zone_tag", ""),
        local_position=row.get("local_position", ""),
    )


def classify_scene_placement_row(
    root: Path,
    row: dict[str, str],
    line_number: int,
    scene_cache: dict[str, str | None],
    prefab_cache: dict[str, str | None],
) -> PlacementIssue | None:
    packet_id = runtime_audit.require_cell(row, "packet_id", line_number)
    component = runtime_audit.require_cell(row, "authoring_component", line_number)
    scene_path = runtime_audit.require_cell(row, "scene_path", line_number)
    object_name = runtime_audit.require_cell(row, "object_name", line_number)
    source_prefab = runtime_audit.require_cell(row, "source_prefab", line_number)
    discovery_id = row.get("discovery_id", "")
    depth_band = row.get("depth_band", "")
    zone_tag = row.get("zone_tag", "")
    local_position = row.get("local_position", "")

    def issue(reason: str) -> PlacementIssue:
        return make_issue_from_row(row, line_number, reason)

    scene_text = scene_cache.get(scene_path)
    if scene_path not in scene_cache:
        scene_text = read_text_or_none(root / scene_path)
        scene_cache[scene_path] = scene_text
    if scene_text is None:
        return issue("scene_missing_or_unreadable")

    scene_object_name_count = count_yaml_scalar(scene_text, "m_Name", object_name)
    if scene_object_name_count > 1:
        return issue("duplicate_object_name_in_scene")

    if object_name not in scene_text:
        return issue("object_missing_in_scene")

    serialized_field = runtime_audit.require_cell(row, "serialized_field", line_number)
    packet_hash = runtime_audit.parse_int(
        runtime_audit.require_cell(row, "packet_hash_decimal", line_number),
        "packet_hash_decimal",
        line_number,
    )
    hash_fragment = f"{serialized_field}: {packet_hash}"
    scene_has_hash = hash_fragment in scene_text

    prefab_text = prefab_cache.get(source_prefab)
    if source_prefab not in prefab_cache:
        prefab_text = read_text_or_none(root / source_prefab)
        prefab_cache[source_prefab] = prefab_text
    prefab_has_hash = prefab_text is not None and hash_fragment in prefab_text
    if not scene_has_hash and not prefab_has_hash:
        reason = "binding_hash_missing_in_scene_and_prefab"
        if prefab_text is None:
            reason = "binding_hash_missing_and_prefab_missing_or_unreadable"
        return issue(reason)

    if component == "NarrativeDiscovery":
        discovery_id = runtime_audit.require_cell(row, "discovery_id", line_number)
        discovery_id_count = count_yaml_scalar(scene_text, "discoveryId", discovery_id)
        if discovery_id_count > 1:
            return issue("duplicate_discovery_id_in_scene")
        if f"discoveryId: {discovery_id}" not in scene_text:
            return issue("discovery_id_missing_in_scene")

    return None


def compute_scene_placement_delta(root: Path) -> PlacementDelta:
    path = root / PLAN_RELATIVE_PATH
    if not path.exists():
        raise runtime_audit.AuditFailure(f"Missing AppliedLore scene placement plan: {path}")

    scene_cache: dict[str, str | None] = {}
    prefab_cache: dict[str, str | None] = {}
    plan_rows: list[tuple[int, dict[str, str]]] = []
    issues: list[PlacementIssue] = []
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != runtime_audit.SCENE_PLACEMENT_PLAN_HEADERS:
            raise runtime_audit.AuditFailure(f"Scene placement plan header mismatch in {path}")

        for line_number, row in enumerate(reader, start=2):
            plan_rows.append((line_number, row))

    object_name_counts: dict[str, int] = {}
    discovery_id_counts: dict[str, int] = {}
    for _line_number, row in plan_rows:
        object_name = row.get("object_name", "").strip()
        if object_name:
            object_name_counts[object_name] = object_name_counts.get(object_name, 0) + 1
        discovery_id = row.get("discovery_id", "").strip()
        if row.get("authoring_component", "").strip() == "NarrativeDiscovery" and discovery_id:
            discovery_id_counts[discovery_id] = discovery_id_counts.get(discovery_id, 0) + 1

    for line_number, row in plan_rows:
        object_name = row.get("object_name", "").strip()
        if object_name and object_name_counts.get(object_name, 0) > 1:
            issues.append(make_issue_from_row(row, line_number, "duplicate_object_name_in_plan"))
            continue

        discovery_id = row.get("discovery_id", "").strip()
        if row.get("authoring_component", "").strip() == "NarrativeDiscovery" and discovery_id_counts.get(discovery_id, 0) > 1:
            issues.append(make_issue_from_row(row, line_number, "duplicate_discovery_id_in_plan"))
            continue

        issue = classify_scene_placement_row(root, row, line_number, scene_cache, prefab_cache)
        if issue is not None:
            issues.append(issue)

    return PlacementDelta(total_rows=len(plan_rows), covered_rows=len(plan_rows) - len(issues), issues=tuple(issues))


def render_delta(delta: PlacementDelta, *, max_rows: int = 40) -> str:
    missing_rows = len(delta.issues)
    lines = [
        "AppliedLore scene placement delta: "
        f"planned={delta.total_rows} covered={delta.covered_rows} missing={missing_rows}",
    ]
    if not missing_rows:
        return "\n".join(lines)

    reason_counts: dict[tuple[str, str], int] = {}
    for issue in delta.issues:
        key = (issue.component, issue.reason)
        reason_counts[key] = reason_counts.get(key, 0) + 1

    for (component, reason), count in sorted(reason_counts.items(), key=lambda item: (-item[1], item[0])):
        lines.append(f"- reason component={component} reason={reason} count={count}")

    scene_counts: dict[str, int] = {}
    prefab_counts: dict[str, int] = {}
    depth_counts: dict[str, int] = {}
    zone_counts: dict[str, int] = {}
    for issue in delta.issues:
        scene_counts[issue.scene_path] = scene_counts.get(issue.scene_path, 0) + 1
        prefab_counts[issue.source_prefab] = prefab_counts.get(issue.source_prefab, 0) + 1
        depth_counts[issue.depth_band] = depth_counts.get(issue.depth_band, 0) + 1
        zone_counts[issue.zone_tag] = zone_counts.get(issue.zone_tag, 0) + 1

    for scene_path, count in sorted(scene_counts.items(), key=lambda item: (-item[1], item[0]))[:8]:
        lines.append(f"- scene_missing_work scene={scene_path} count={count}")

    for source_prefab, count in sorted(prefab_counts.items(), key=lambda item: (-item[1], item[0]))[:8]:
        lines.append(f"- prefab_source prefab={source_prefab} count={count}")

    for depth_band, count in sorted(depth_counts.items(), key=lambda item: (-item[1], item[0]))[:8]:
        lines.append(f"- depth_band depth={depth_band} count={count}")

    for zone_tag, count in sorted(zone_counts.items(), key=lambda item: (-item[1], item[0]))[:12]:
        lines.append(f"- zone_tag zone={zone_tag} count={count}")

    shown = 0
    for issue in delta.issues:
        if shown >= max_rows:
            remaining = missing_rows - shown
            if remaining > 0:
                lines.append(f"- ... {remaining} more missing placement rows")
            break
        lines.append(
            f"- missing line={issue.line_number} packet={issue.packet_id} "
            f"component={issue.component} reason={issue.reason} "
            f"scene={issue.scene_path} object={issue.object_name} prefab={issue.source_prefab} "
            f"depth={issue.depth_band} zone={issue.zone_tag} local_position={issue.local_position}"
        )
        shown += 1

    return "\n".join(lines)


def count_issues_by_reason(delta: PlacementDelta) -> list[dict[str, int | str]]:
    counts: dict[tuple[str, str], int] = {}
    for issue in delta.issues:
        key = (issue.component, issue.reason)
        counts[key] = counts.get(key, 0) + 1

    return [
        {"component": component, "reason": reason, "count": count}
        for (component, reason), count in sorted(counts.items(), key=lambda item: (-item[1], item[0]))
    ]


def count_issues_by_field(delta: PlacementDelta, field_name: str, output_name: str) -> list[dict[str, int | str]]:
    counts: dict[str, int] = {}
    for issue in delta.issues:
        key = getattr(issue, field_name)
        counts[key] = counts.get(key, 0) + 1

    return [
        {output_name: key, "count": count}
        for key, count in sorted(counts.items(), key=lambda item: (-item[1], item[0]))
    ]


def delta_to_json_payload(delta: PlacementDelta, *, max_rows: int = 40) -> dict[str, object]:
    issues = delta.issues[:max(max_rows, 0)]
    return {
        "planned": delta.total_rows,
        "covered": delta.covered_rows,
        "missing": len(delta.issues),
        "truncated_issues": max(len(delta.issues) - len(issues), 0),
        "reason_counts": count_issues_by_reason(delta),
        "scene_missing_work": count_issues_by_field(delta, "scene_path", "scene"),
        "prefab_sources": count_issues_by_field(delta, "source_prefab", "prefab"),
        "depth_bands": count_issues_by_field(delta, "depth_band", "depth"),
        "zone_tags": count_issues_by_field(delta, "zone_tag", "zone"),
        "issues": [
            {
                "line": issue.line_number,
                "packet": issue.packet_id,
                "component": issue.component,
                "reason": issue.reason,
                "scene": issue.scene_path,
                "object": issue.object_name,
                "prefab": issue.source_prefab,
                "discovery_id": issue.discovery_id,
                "depth_band": issue.depth_band,
                "zone_tag": issue.zone_tag,
                "local_position": issue.local_position,
            }
            for issue in issues
        ],
    }


def run(root: Path) -> PlacementDelta:
    return compute_scene_placement_delta(root)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Hecton8 repository root")
    parser.add_argument("--max-rows", type=int, default=40, help="Maximum row-level issues to print")
    parser.add_argument("--json", action="store_true", help="Write a machine-readable delta payload to stdout")
    args = parser.parse_args(argv)

    root = Path(args.root).resolve()
    try:
        delta = run(root)
    except runtime_audit.AuditFailure as exc:
        print(f"AppliedLore scene placement delta failed: {exc}", file=sys.stderr)
        return 1

    if args.json:
        print(json.dumps(delta_to_json_payload(delta, max_rows=args.max_rows), ensure_ascii=False, indent=2))
        return 1 if delta.issues else 0

    output = render_delta(delta, max_rows=max(args.max_rows, 0))
    if delta.issues:
        print(output, file=sys.stderr)
        return 1
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
