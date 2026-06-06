#!/usr/bin/env python3
"""Gate mass-deletion dirty sets before cleanup or handoff.

Evidence class: STATIC_GIT_STATUS only. This script does not delete, restore,
move, stage, import, build, or prove Unity runtime state.
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

DEFAULT_DISPOSITION_PATHS = (
    Path("Docs/AssetAudit/MASS_DELETION_DIRTY_SET_DISPOSITION.md"),
    Path("Docs/MASS_DELETION_DIRTY_SET_DISPOSITION.md"),
)

RESOLVED_SENTINELS = (
    "MASS_DELETION_DIRTY_SET_RESOLVED=TRUE",
    "MASS_DELETION_DIRTY_SET_DISPOSITION=RESOLVED",
    "MASS_DELETION_DIRTY_SET: RESOLVED",
)

REJECTING_DISPOSITION_TERMS = (
    "PENDING_VERIFICATION",
    "PENDING VERIFICATION",
    "REJECT",
    "REJECTED",
    "BLOCKED",
)

TOOL_SOURCE_SUFFIXES = (
    ".py",
    ".ps1",
    ".cs",
    ".shader",
    ".uxml",
    ".uss",
    ".json",
    ".md",
    ".txt",
)


@dataclass(frozen=True)
class StatusEntry:
    index_status: str
    worktree_status: str
    path: str

    @property
    def status(self) -> str:
        return f"{self.index_status}{self.worktree_status}"

    @property
    def is_untracked(self) -> bool:
        return self.status == "??"

    @property
    def is_staged(self) -> bool:
        return not self.is_untracked and self.index_status not in (" ", "!")

    @property
    def is_deleted(self) -> bool:
        return not self.is_untracked and "D" in self.status

    @property
    def is_modified(self) -> bool:
        return not self.is_untracked and "M" in self.status


@dataclass(frozen=True)
class StatusCounts:
    total_rows: int
    tracked_deletions: int
    tracked_modifications: int
    untracked_rows: int
    staged_rows: int


@dataclass(frozen=True)
class DeletionCounts:
    assets: int
    assets_project: int
    tools_source_outside_bin_obj: int
    docs_reports: int
    docs_screenshots: int
    docs_agentlogs: int
    docs_tasks: int
    polish_deleted: bool
    deleted_meta: int
    deleted_cs: int
    deleted_shader: int
    deleted_asset: int
    deleted_unity: int


@dataclass(frozen=True)
class MetaPairingSummary:
    deleted_asset_payloads: int
    missing_meta_for_deleted_payloads: tuple[str, ...]
    deleted_meta_without_payload: int

    @property
    def is_clean(self) -> bool:
        return not self.missing_meta_for_deleted_payloads


@dataclass(frozen=True)
class OwnerDisposition:
    resolved: bool
    matched_path: str | None
    checked_paths: tuple[str, ...]
    present_without_resolution: tuple[str, ...]


@dataclass(frozen=True)
class MassDeletionReport:
    status_counts: StatusCounts
    deletion_counts: DeletionCounts
    meta_pairing: MetaPairingSummary
    owner_disposition: OwnerDisposition
    blockers: tuple[str, ...]
    notes: tuple[str, ...]
    tool_source_samples: tuple[str, ...]

    @property
    def has_high_risk_deletions(self) -> bool:
        counts = self.deletion_counts
        return any(
            (
                counts.assets,
                counts.assets_project,
                counts.tools_source_outside_bin_obj,
                counts.docs_reports,
                counts.docs_screenshots,
                counts.docs_agentlogs,
                counts.docs_tasks,
                counts.polish_deleted,
                counts.deleted_cs,
                counts.deleted_shader,
                counts.deleted_asset,
                counts.deleted_unity,
            )
        )

    @property
    def is_rejected(self) -> bool:
        return bool(self.blockers)

    @property
    def label(self) -> str:
        if self.is_rejected:
            return "MASS_DELETION_DIRTY_SET_REJECTED"
        if self.has_high_risk_deletions and self.owner_disposition.resolved:
            return "MASS_DELETION_DIRTY_SET_RESOLVED_BY_OWNER_DISPOSITION"
        return "MASS_DELETION_DIRTY_SET_OK"


def normalize_path(path: str) -> str:
    value = path.strip()
    if len(value) >= 2 and value[0] == value[-1] == '"':
        value = value[1:-1]
    return value.replace("\\", "/")


def display_path(path: Path, root: Path = ROOT) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def parse_status_text(text: str) -> list[StatusEntry]:
    if "\0" in text:
        return parse_status_records([part for part in text.split("\0") if part], z_mode=True)
    return parse_status_records([line for line in text.splitlines() if line], z_mode=False)


def parse_status_records(records: list[str], *, z_mode: bool) -> list[StatusEntry]:
    entries: list[StatusEntry] = []
    index = 0
    while index < len(records):
        record = records[index]
        if len(record) < 3:
            index += 1
            continue

        index_status = record[0]
        worktree_status = record[1]
        raw_path = record[3:] if len(record) > 3 and record[2] == " " else record[2:].lstrip()
        path = normalize_path(raw_path)
        if " -> " in path:
            path = normalize_path(path.split(" -> ", 1)[1])

        entries.append(StatusEntry(index_status=index_status, worktree_status=worktree_status, path=path))

        # Porcelain -z encodes rename/copy as one status record plus a second
        # path record. The deletion gate only needs one row for the status.
        if z_mode and (index_status in ("R", "C") or worktree_status in ("R", "C")):
            index += 2
        else:
            index += 1
    return entries


def read_status_from_git(root: Path) -> str:
    completed = subprocess.run(
        ("git", "status", "--porcelain=v1", "-z", "--untracked-files=all"),
        cwd=root,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if completed.returncode != 0:
        stderr = completed.stderr.strip() or "git status failed"
        raise RuntimeError(stderr)
    return completed.stdout


def load_status_entries(root: Path, status_file: Path | None) -> list[StatusEntry]:
    if status_file is not None:
        text = status_file.read_text(encoding="utf-8-sig", errors="replace")
        return parse_status_text(text)
    return parse_status_text(read_status_from_git(root))


def is_inside_bin_or_obj(path: str) -> bool:
    parts = [part.lower() for part in path.split("/")]
    return "bin" in parts or "obj" in parts


def is_tool_source_outside_bin_obj(path: str) -> bool:
    lowered = path.lower()
    return (
        lowered.startswith("tools/")
        and not is_inside_bin_or_obj(path)
        and lowered.endswith(TOOL_SOURCE_SUFFIXES)
    )


def is_asset_payload(path: str) -> bool:
    lowered = path.lower()
    return lowered.startswith("assets/") and not lowered.endswith(".meta")


def build_status_counts(entries: list[StatusEntry]) -> StatusCounts:
    return StatusCounts(
        total_rows=len(entries),
        tracked_deletions=sum(1 for entry in entries if entry.is_deleted),
        tracked_modifications=sum(1 for entry in entries if entry.is_modified),
        untracked_rows=sum(1 for entry in entries if entry.is_untracked),
        staged_rows=sum(1 for entry in entries if entry.is_staged),
    )


def build_deletion_counts(deleted_paths: list[str]) -> DeletionCounts:
    lowered_paths = [path.lower() for path in deleted_paths]
    return DeletionCounts(
        assets=sum(1 for path in lowered_paths if path.startswith("assets/")),
        assets_project=sum(1 for path in lowered_paths if path.startswith("assets/_project/")),
        tools_source_outside_bin_obj=sum(1 for path in deleted_paths if is_tool_source_outside_bin_obj(path)),
        docs_reports=sum(1 for path in lowered_paths if path.startswith("docs/reports/")),
        docs_screenshots=sum(1 for path in lowered_paths if path.startswith("docs/screenshots/")),
        docs_agentlogs=sum(1 for path in lowered_paths if path.startswith("docs/agentlogs/")),
        docs_tasks=sum(1 for path in lowered_paths if path.startswith("docs/tasks/")),
        polish_deleted="docs/tasks/polish.txt" in lowered_paths,
        deleted_meta=sum(1 for path in lowered_paths if path.endswith(".meta")),
        deleted_cs=sum(1 for path in lowered_paths if path.endswith(".cs")),
        deleted_shader=sum(1 for path in lowered_paths if path.endswith(".shader")),
        deleted_asset=sum(1 for path in lowered_paths if path.endswith(".asset")),
        deleted_unity=sum(1 for path in lowered_paths if path.endswith(".unity")),
    )


def build_meta_pairing_summary(deleted_paths: list[str]) -> MetaPairingSummary:
    deleted = {path.lower() for path in deleted_paths}
    asset_payloads = [path for path in deleted_paths if is_asset_payload(path)]
    missing_meta = tuple(
        sorted(path for path in asset_payloads if f"{path.lower()}.meta" not in deleted)
    )
    deleted_meta_without_payload = sum(
        1
        for path in deleted
        if path.startswith("assets/")
        and path.endswith(".meta")
        and path[:-5] not in deleted
    )
    return MetaPairingSummary(
        deleted_asset_payloads=len(asset_payloads),
        missing_meta_for_deleted_payloads=missing_meta,
        deleted_meta_without_payload=deleted_meta_without_payload,
    )


def normalize_disposition_text(text: str) -> str:
    return "\n".join(line.strip().upper() for line in text.splitlines() if line.strip())


def disposition_text_resolves(text: str) -> bool:
    normalized = normalize_disposition_text(text)
    if any(term in normalized for term in REJECTING_DISPOSITION_TERMS):
        return False
    return any(sentinel in normalized for sentinel in RESOLVED_SENTINELS)


def resolve_disposition_path(path: Path, root: Path) -> Path:
    return path if path.is_absolute() else root / path


def inspect_owner_disposition(root: Path, explicit_paths: tuple[Path, ...]) -> OwnerDisposition:
    candidates = explicit_paths or DEFAULT_DISPOSITION_PATHS
    checked: list[str] = []
    present_without_resolution: list[str] = []

    for candidate in candidates:
        path = resolve_disposition_path(candidate, root)
        checked.append(display_path(path, root))
        if not path.exists():
            continue
        text = path.read_text(encoding="utf-8-sig", errors="replace")
        if disposition_text_resolves(text):
            return OwnerDisposition(
                resolved=True,
                matched_path=display_path(path, root),
                checked_paths=tuple(checked),
                present_without_resolution=tuple(present_without_resolution),
            )
        present_without_resolution.append(display_path(path, root))

    return OwnerDisposition(
        resolved=False,
        matched_path=None,
        checked_paths=tuple(checked),
        present_without_resolution=tuple(present_without_resolution),
    )


def build_blockers(
    deletion_counts: DeletionCounts,
    meta_pairing: MetaPairingSummary,
    owner_disposition: OwnerDisposition,
    tool_source_samples: tuple[str, ...],
) -> list[str]:
    blockers: list[str] = []

    if deletion_counts.assets:
        blockers.append(f"assets-deletions: count={deletion_counts.assets}")
    if deletion_counts.assets_project:
        blockers.append(f"assets-project-deletions: count={deletion_counts.assets_project}")
    if deletion_counts.tools_source_outside_bin_obj:
        sample_text = ",".join(tool_source_samples) if tool_source_samples else "none"
        blockers.append(
            "tools-source-deletions-outside-bin-obj: "
            f"count={deletion_counts.tools_source_outside_bin_obj} samples={sample_text}"
        )
    if deletion_counts.docs_reports:
        blockers.append(f"docs-reports-deletions: count={deletion_counts.docs_reports}")
    if deletion_counts.docs_screenshots:
        blockers.append(f"docs-screenshots-deletions: count={deletion_counts.docs_screenshots}")
    if deletion_counts.docs_agentlogs:
        blockers.append(f"docs-agentlogs-deletions: count={deletion_counts.docs_agentlogs}")
    if deletion_counts.docs_tasks:
        blockers.append(f"docs-tasks-deletions: count={deletion_counts.docs_tasks}")
    if deletion_counts.polish_deleted:
        blockers.append("polish-task-deleted: Docs/Tasks/POLISH.txt")
    if deletion_counts.deleted_cs:
        blockers.append(f"deleted-csharp-files: count={deletion_counts.deleted_cs}")
    if deletion_counts.deleted_shader:
        blockers.append(f"deleted-shader-files: count={deletion_counts.deleted_shader}")
    if deletion_counts.deleted_asset:
        blockers.append(f"deleted-asset-files: count={deletion_counts.deleted_asset}")
    if deletion_counts.deleted_unity:
        blockers.append(f"deleted-scene-files: count={deletion_counts.deleted_unity}")
    if not meta_pairing.is_clean:
        samples = ",".join(meta_pairing.missing_meta_for_deleted_payloads[:5])
        blockers.append(
            "asset-meta-pairing-missing: "
            f"count={len(meta_pairing.missing_meta_for_deleted_payloads)} samples={samples}"
        )

    if owner_disposition.resolved:
        return []
    return blockers


def analyze_entries(
    entries: list[StatusEntry],
    root: Path = ROOT,
    disposition_paths: tuple[Path, ...] = (),
) -> MassDeletionReport:
    status_counts = build_status_counts(entries)
    deleted_paths = [entry.path for entry in entries if entry.is_deleted]
    deletion_counts = build_deletion_counts(deleted_paths)
    meta_pairing = build_meta_pairing_summary(deleted_paths)
    owner_disposition = inspect_owner_disposition(root, disposition_paths)
    tool_source_samples = tuple(path for path in deleted_paths if is_tool_source_outside_bin_obj(path))[:8]
    blockers = build_blockers(deletion_counts, meta_pairing, owner_disposition, tool_source_samples)
    meta_pairing_status = (
        "PAIRING_CLEAN_BUT_NOT_DELETION_APPROVAL"
        if meta_pairing.is_clean
        else "PAIRING_BROKEN"
    )

    notes = [
        "evidence-class: STATIC_GIT_STATUS / PENDING UNITY PROOF",
        "side-effects: no delete/restore/move/stage operations performed",
        "meta-pairing: "
        f"asset_payloads={meta_pairing.deleted_asset_payloads} "
        f"missing_meta={len(meta_pairing.missing_meta_for_deleted_payloads)} "
        f"deleted_meta_without_payload={meta_pairing.deleted_meta_without_payload} "
        f"status={meta_pairing_status}",
    ]
    if owner_disposition.resolved and owner_disposition.matched_path:
        notes.append(f"owner-disposition: resolved_by={owner_disposition.matched_path}")
    elif owner_disposition.present_without_resolution:
        notes.append(
            "owner-disposition: present_without_resolution="
            + ",".join(owner_disposition.present_without_resolution)
        )
    else:
        notes.append("owner-disposition: unresolved checked=" + ",".join(owner_disposition.checked_paths))

    return MassDeletionReport(
        status_counts=status_counts,
        deletion_counts=deletion_counts,
        meta_pairing=meta_pairing,
        owner_disposition=owner_disposition,
        blockers=tuple(blockers),
        notes=tuple(notes),
        tool_source_samples=tool_source_samples,
    )


def print_report(report: MassDeletionReport) -> None:
    print(
        f"{report.label} blockers={len(report.blockers)} "
        f"high_risk_deletions={str(report.has_high_risk_deletions).lower()} "
        f"owner_disposition={str(report.owner_disposition.resolved).lower()}"
    )
    print(
        "status-rows: "
        f"total={report.status_counts.total_rows} "
        f"tracked_deletions={report.status_counts.tracked_deletions} "
        f"tracked_modifications={report.status_counts.tracked_modifications} "
        f"untracked={report.status_counts.untracked_rows} "
        f"staged={report.status_counts.staged_rows}"
    )
    print(
        "deletions: "
        f"assets={report.deletion_counts.assets} "
        f"assets_project={report.deletion_counts.assets_project} "
        f"tools_source_outside_bin_obj={report.deletion_counts.tools_source_outside_bin_obj} "
        f"docs_reports={report.deletion_counts.docs_reports} "
        f"docs_screenshots={report.deletion_counts.docs_screenshots} "
        f"docs_agentlogs={report.deletion_counts.docs_agentlogs} "
        f"docs_tasks={report.deletion_counts.docs_tasks} "
        f"polish_deleted={str(report.deletion_counts.polish_deleted).lower()}"
    )
    print(
        "deleted-extensions: "
        f"meta={report.deletion_counts.deleted_meta} "
        f"cs={report.deletion_counts.deleted_cs} "
        f"shader={report.deletion_counts.deleted_shader} "
        f"asset={report.deletion_counts.deleted_asset} "
        f"unity={report.deletion_counts.deleted_unity}"
    )
    for blocker in report.blockers:
        print(f"- {blocker}")
    for note in report.notes:
        print(f"+ {note}")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT, help="Repository root for live git status.")
    parser.add_argument("--status-file", type=Path, help="Synthetic porcelain status fixture for tests.")
    parser.add_argument(
        "--disposition",
        type=Path,
        action="append",
        default=[],
        help=(
            "Owner disposition file. To resolve, it must contain "
            "MASS_DELETION_DIRTY_SET_RESOLVED=TRUE or an equivalent sentinel."
        ),
    )
    parser.add_argument("--no-fail", action="store_true", help="Print reject report but return exit code 0.")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(sys.argv[1:] if argv is None else argv)
    root = args.root.resolve()
    try:
        entries = load_status_entries(root, args.status_file)
    except (OSError, RuntimeError) as exc:
        print(f"MASS_DELETION_DIRTY_SET_STATUS_UNAVAILABLE: {exc}")
        return 0 if args.no_fail else 2

    report = analyze_entries(entries, root=root, disposition_paths=tuple(args.disposition))
    print_report(report)
    if not report.is_rejected or args.no_fail:
        return 0
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
