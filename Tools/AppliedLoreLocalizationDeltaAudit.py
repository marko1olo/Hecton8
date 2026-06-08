#!/usr/bin/env python3
"""Report AppliedLore native-localization blockers by locale, release set, and surface."""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol, Sequence

TOOL_DIR = Path(__file__).resolve().parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

import AppliedLoreRuntimeAudit as runtime_audit  # noqa: E402


CSV_RELATIVE_PATH = Path("Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv")


class RowLike(Protocol):
    packet_id: str
    locale: str
    release_set_id: str
    article_id: str
    surface_mask: int
    fields: dict[str, str]
    flags: int
    line_number: int


@dataclass(frozen=True)
class LocalizationIssue:
    packet_id: str
    locale: str
    release_set_id: str
    article_id: str
    surfaces: tuple[str, ...]
    line_number: int
    reason: str
    matching_fields: tuple[str, ...] = ()


@dataclass(frozen=True)
class CountGroup:
    key: str
    count: int
    packets: int
    locales: tuple[str, ...]


@dataclass(frozen=True)
class LocalizationDeltaStats:
    total_rows: int
    packet_count: int
    locale_count: int
    draft_rows: tuple[LocalizationIssue, ...]
    english_clone_rows: tuple[LocalizationIssue, ...]
    ready_non_english_rows: int
    draft_text_differs_rows: int
    draft_english_clone_rows: int
    draft_partial_english_clone_rows: int
    draft_by_locale: tuple[CountGroup, ...]
    draft_by_release_set: tuple[CountGroup, ...]
    draft_by_surface: tuple[CountGroup, ...]
    draft_by_reason: tuple[CountGroup, ...]
    ready_by_locale: tuple[CountGroup, ...]
    ready_by_release_set: tuple[CountGroup, ...]

    @property
    def is_clean(self) -> bool:
        return not self.draft_rows and not self.english_clone_rows


def load_csv_rows(root: Path) -> list[runtime_audit.CsvPacketRow]:
    return runtime_audit.load_csv(root / CSV_RELATIVE_PATH)


def surface_names(surface_mask: int) -> tuple[str, ...]:
    names = tuple(name for name, bit, _offset, _hash_offset in runtime_audit.SURFACES if surface_mask & bit)
    return names if names else ("none",)


def normalize_visible_text(value: str) -> str:
    return runtime_audit.normalize_visible_text_for_localization_identity(value)


def visible_source_fields(row: RowLike) -> tuple[str, ...]:
    return tuple(
        field
        for field in runtime_audit.PLAYER_VISIBLE_TEXT_FIELDS
        if normalize_visible_text(row.fields.get(field, "")) != ""
    )


def matching_english_fields(source: RowLike, candidate: RowLike) -> tuple[str, ...]:
    fields = visible_source_fields(source)
    return tuple(
        field
        for field in fields
        if normalize_visible_text(candidate.fields.get(field, "")) == normalize_visible_text(source.fields.get(field, ""))
    )


def row_issue(row: RowLike, reason: str, matching_fields: tuple[str, ...] = ()) -> LocalizationIssue:
    return LocalizationIssue(
        packet_id=row.packet_id,
        locale=row.locale,
        release_set_id=row.release_set_id,
        article_id=row.article_id,
        surfaces=surface_names(row.surface_mask),
        line_number=row.line_number,
        reason=reason,
        matching_fields=matching_fields,
    )


def make_count_groups(
    rows: Sequence[LocalizationIssue],
    key_for_issue,
) -> tuple[CountGroup, ...]:
    issues_by_key: dict[str, list[LocalizationIssue]] = defaultdict(list)
    for issue in rows:
        keys = key_for_issue(issue)
        if isinstance(keys, str):
            keys = (keys,)
        for key in keys:
            issues_by_key[key].append(issue)

    groups: list[CountGroup] = []
    for key, issues in issues_by_key.items():
        groups.append(
            CountGroup(
                key=key,
                count=len(issues),
                packets=len({issue.packet_id for issue in issues}),
                locales=tuple(sorted({issue.locale for issue in issues})),
            )
        )
    groups.sort(key=lambda group: (-group.count, group.key))
    return tuple(groups)


def make_row_count_groups(
    rows: Sequence[RowLike],
    key_for_row,
) -> tuple[CountGroup, ...]:
    rows_by_key: dict[str, list[RowLike]] = defaultdict(list)
    for row in rows:
        keys = key_for_row(row)
        if isinstance(keys, str):
            keys = (keys,)
        for key in keys:
            rows_by_key[key].append(row)

    groups: list[CountGroup] = []
    for key, grouped_rows in rows_by_key.items():
        groups.append(
            CountGroup(
                key=key,
                count=len(grouped_rows),
                packets=len({row.packet_id for row in grouped_rows}),
                locales=tuple(sorted({row.locale for row in grouped_rows})),
            )
        )
    groups.sort(key=lambda group: (-group.count, group.key))
    return tuple(groups)


def compute_localization_delta(rows: Sequence[RowLike]) -> LocalizationDeltaStats:
    english_by_packet = {row.packet_id: row for row in rows if row.locale == "en_US"}
    draft_rows: list[LocalizationIssue] = []
    english_clone_rows: list[LocalizationIssue] = []
    ready_non_english: list[RowLike] = []
    ready_non_english_rows = 0

    for row in rows:
        is_draft = (row.flags & runtime_audit.ROW_FLAG_DRAFT_LOCALIZATION) != 0
        if is_draft:
            if row.locale == "en_US":
                draft_rows.append(row_issue(row, "draft_source_locale"))
                continue

            source = english_by_packet.get(row.packet_id)
            if source is None:
                draft_rows.append(row_issue(row, "draft_missing_english_source"))
                continue

            source_fields = visible_source_fields(source)
            if not source_fields:
                draft_rows.append(row_issue(row, "draft_no_visible_english_fields"))
                continue

            matched = matching_english_fields(source, row)
            if len(matched) == len(source_fields):
                draft_rows.append(row_issue(row, "draft_english_clone", matched))
            elif matched:
                draft_rows.append(row_issue(row, "draft_partial_english_clone", matched))
            else:
                draft_rows.append(row_issue(row, "draft_text_differs_from_english"))
            continue

        if row.locale == "en_US":
            continue

        ready_non_english_rows += 1
        ready_non_english.append(row)
        source = english_by_packet.get(row.packet_id)
        if source is None:
            continue
        source_fields = visible_source_fields(source)
        if not source_fields:
            continue
        matched = matching_english_fields(source, row)
        if len(matched) == len(source_fields):
            english_clone_rows.append(row_issue(row, "non_draft_english_clone", matched))

    draft_tuple = tuple(draft_rows)
    draft_reason_counts = Counter(issue.reason for issue in draft_tuple)
    return LocalizationDeltaStats(
        total_rows=len(rows),
        packet_count=len({row.packet_id for row in rows}),
        locale_count=len({row.locale for row in rows}),
        draft_rows=draft_tuple,
        english_clone_rows=tuple(english_clone_rows),
        ready_non_english_rows=ready_non_english_rows,
        draft_text_differs_rows=draft_reason_counts.get("draft_text_differs_from_english", 0),
        draft_english_clone_rows=draft_reason_counts.get("draft_english_clone", 0),
        draft_partial_english_clone_rows=draft_reason_counts.get("draft_partial_english_clone", 0),
        draft_by_locale=make_count_groups(draft_tuple, lambda issue: issue.locale),
        draft_by_release_set=make_count_groups(draft_tuple, lambda issue: issue.release_set_id),
        draft_by_surface=make_count_groups(draft_tuple, lambda issue: issue.surfaces),
        draft_by_reason=make_count_groups(draft_tuple, lambda issue: issue.reason),
        ready_by_locale=make_row_count_groups(ready_non_english, lambda row: row.locale),
        ready_by_release_set=make_row_count_groups(ready_non_english, lambda row: row.release_set_id),
    )


def truncate_items(items: Sequence, limit: int) -> tuple[Sequence, int]:
    if limit < 0:
        limit = 0
    return items[:limit], max(len(items) - limit, 0)


def issue_to_json(issue: LocalizationIssue) -> dict[str, object]:
    payload: dict[str, object] = {
        "packet": issue.packet_id,
        "locale": issue.locale,
        "release_set": issue.release_set_id,
        "article": issue.article_id,
        "surfaces": list(issue.surfaces),
        "line": issue.line_number,
        "reason": issue.reason,
    }
    if issue.matching_fields:
        payload["matching_fields"] = list(issue.matching_fields)
    return payload


def group_to_json(group: CountGroup, max_locales: int) -> dict[str, object]:
    locales, truncated_locales = truncate_items(group.locales, max_locales)
    return {
        "key": group.key,
        "count": group.count,
        "packets": group.packets,
        "locales": list(locales),
        "locale_count": len(group.locales),
        "truncated_locales": truncated_locales,
    }


def issues_grouped_by_reason(issues: Sequence[LocalizationIssue]) -> dict[str, list[LocalizationIssue]]:
    grouped: dict[str, list[LocalizationIssue]] = defaultdict(list)
    for issue in issues:
        grouped[issue.reason].append(issue)
    return dict(sorted(grouped.items()))


def samples_by_reason_to_json(issues: Sequence[LocalizationIssue], max_rows: int) -> dict[str, object]:
    payload: dict[str, object] = {}
    for reason, reason_issues in issues_grouped_by_reason(issues).items():
        samples, truncated_samples = truncate_items(reason_issues, max_rows)
        payload[reason] = {
            "count": len(reason_issues),
            "samples": [issue_to_json(issue) for issue in samples],
            "truncated_samples": truncated_samples,
        }
    return payload


def localization_delta_to_json_payload(
    stats: LocalizationDeltaStats,
    *,
    max_rows: int = 40,
    max_groups: int = 40,
    max_locales: int = 15,
) -> dict[str, object]:
    draft_samples, truncated_draft_samples = truncate_items(stats.draft_rows, max_rows)
    clone_samples, truncated_clone_samples = truncate_items(stats.english_clone_rows, max_rows)
    locale_groups, truncated_locale_groups = truncate_items(stats.draft_by_locale, max_groups)
    release_groups, truncated_release_groups = truncate_items(stats.draft_by_release_set, max_groups)
    surface_groups, truncated_surface_groups = truncate_items(stats.draft_by_surface, max_groups)
    reason_groups, truncated_reason_groups = truncate_items(stats.draft_by_reason, max_groups)
    ready_locale_groups, truncated_ready_locale_groups = truncate_items(stats.ready_by_locale, max_groups)
    ready_release_groups, truncated_ready_release_groups = truncate_items(stats.ready_by_release_set, max_groups)
    return {
        "clean": stats.is_clean,
        "rows": {
            "total": stats.total_rows,
            "packets": stats.packet_count,
            "locales": stats.locale_count,
            "draft": len(stats.draft_rows),
            "draft_text_differs_from_english": stats.draft_text_differs_rows,
            "draft_english_clone": stats.draft_english_clone_rows,
            "draft_partial_english_clone": stats.draft_partial_english_clone_rows,
            "ready_non_english": stats.ready_non_english_rows,
            "non_draft_english_clone": len(stats.english_clone_rows),
        },
        "draft_by_reason": [group_to_json(group, max_locales) for group in reason_groups],
        "truncated_draft_by_reason": truncated_reason_groups,
        "draft_by_locale": [group_to_json(group, max_locales) for group in locale_groups],
        "truncated_draft_by_locale": truncated_locale_groups,
        "draft_by_release_set": [group_to_json(group, max_locales) for group in release_groups],
        "truncated_draft_by_release_set": truncated_release_groups,
        "draft_by_surface": [group_to_json(group, max_locales) for group in surface_groups],
        "truncated_draft_by_surface": truncated_surface_groups,
        "ready_by_locale": [group_to_json(group, max_locales) for group in ready_locale_groups],
        "truncated_ready_by_locale": truncated_ready_locale_groups,
        "ready_by_release_set": [group_to_json(group, max_locales) for group in ready_release_groups],
        "truncated_ready_by_release_set": truncated_ready_release_groups,
        "draft_samples": [issue_to_json(issue) for issue in draft_samples],
        "truncated_draft_samples": truncated_draft_samples,
        "draft_samples_by_reason": samples_by_reason_to_json(stats.draft_rows, max_rows),
        "english_clone_samples": [issue_to_json(issue) for issue in clone_samples],
        "truncated_english_clone_samples": truncated_clone_samples,
    }


def render_group_line(prefix: str, group: CountGroup) -> str:
    return (
        f"{prefix} {group.key}: rows={group.count} packets={group.packets} "
        f"locales={','.join(group.locales)}"
    )


def render_issue(prefix: str, issue: LocalizationIssue) -> str:
    fields = ""
    if issue.matching_fields:
        fields = f" matching_fields={','.join(issue.matching_fields)}"
    return (
        f"{prefix} {issue.packet_id}/{issue.locale} release_set={issue.release_set_id} "
        f"line={issue.line_number} surfaces={','.join(issue.surfaces)} reason={issue.reason}{fields}"
    )


def render_delta(stats: LocalizationDeltaStats, *, max_rows: int = 20, max_groups: int = 12) -> str:
    if stats.is_clean:
        return (
            "AppliedLore native localization delta OK: "
            f"rows={stats.total_rows} packets={stats.packet_count} locales={stats.locale_count} "
            f"ready_non_english_rows={stats.ready_non_english_rows}"
        )

    lines = [
        "AppliedLore native localization delta blocked: "
        f"rows={stats.total_rows} packets={stats.packet_count} locales={stats.locale_count} "
        f"draft_rows={len(stats.draft_rows)} "
        f"draft_text_differs_from_english_rows={stats.draft_text_differs_rows} "
        f"draft_english_clone_rows={stats.draft_english_clone_rows} "
        f"draft_partial_english_clone_rows={stats.draft_partial_english_clone_rows} "
        f"non_draft_english_clone_rows={len(stats.english_clone_rows)} "
        f"ready_non_english_rows={stats.ready_non_english_rows}"
    ]
    for group in stats.draft_by_reason[:max_groups]:
        lines.append(render_group_line("draft reason", group))
    for group in stats.draft_by_locale[:max_groups]:
        lines.append(render_group_line("draft locale", group))
    for group in stats.draft_by_release_set[:max_groups]:
        lines.append(render_group_line("draft release_set", group))
    for group in stats.draft_by_surface[:max_groups]:
        lines.append(render_group_line("draft surface", group))
    for group in stats.ready_by_locale[:max_groups]:
        lines.append(render_group_line("ready locale", group))
    for group in stats.ready_by_release_set[:max_groups]:
        lines.append(render_group_line("ready release_set", group))
    for issue in stats.draft_rows[:max_rows]:
        lines.append(render_issue("draft sample", issue))
    for issue in stats.english_clone_rows[:max_rows]:
        lines.append(render_issue("english-clone sample", issue))
    return "\n".join(lines)


def run(root: Path) -> LocalizationDeltaStats:
    return compute_localization_delta(load_csv_rows(root))


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="Repository root.")
    parser.add_argument("--json", action="store_true", help="Write a machine-readable result payload to stdout.")
    parser.add_argument("--max-rows", type=int, default=40, help="Maximum sample rows to include.")
    parser.add_argument("--max-groups", type=int, default=40, help="Maximum grouped counters to include.")
    parser.add_argument("--max-locales", type=int, default=15, help="Maximum locales shown per group in JSON.")
    args = parser.parse_args(argv)

    try:
        stats = run(Path(args.root).resolve())
    except runtime_audit.AuditFailure as exc:
        if args.json:
            print(json.dumps({"clean": False, "message": str(exc)}, ensure_ascii=False, indent=2))
        else:
            print(f"AppliedLore native localization delta FAILED: {exc}")
        return 1

    if args.json:
        print(
            json.dumps(
                localization_delta_to_json_payload(
                    stats,
                    max_rows=args.max_rows,
                    max_groups=args.max_groups,
                    max_locales=args.max_locales,
                ),
                ensure_ascii=False,
                indent=2,
            )
        )
    else:
        print(render_delta(stats, max_rows=args.max_rows, max_groups=args.max_groups))
    return 0 if stats.is_clean else 1


if __name__ == "__main__":
    raise SystemExit(main())
