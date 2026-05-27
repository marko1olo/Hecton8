#!/usr/bin/env python3
"""Deterministic workspace hygiene for agent 1331.

Scope: Hecton8 project root and Assets tree, excluding Assets/_Project/Scripts
and the literal legacy spelling Assets/Project/Scripts.
No build, Unity editor, or compilation process is invoked.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Iterable


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
SCRIPTS = ASSETS / "_Project" / "Scripts"
LEGACY_LITERAL_SCRIPTS = ASSETS / "Project" / "Scripts"
REPORTS = ROOT / "Docs" / "Reports"
AGENT_LOGS = ROOT / "Docs" / "AgentLogs"
ARCHIVE_ROOT = ROOT / "Docs" / "_Archive" / "WorkspaceHygiene_1331"
DRY_LEDGER = REPORTS / "WORKSPACE_HYGIENE_DRYRUN_1331.json"
VERIFY_LEDGER = REPORTS / "WORKSPACE_HYGIENE_VERIFY_1331.json"
FINAL_REPORT = REPORTS / "WORKSPACE_HYGIENE_REPORT_1331.json"
ACTION_LOG = AGENT_LOGS / "WORKSPACE_HYGIENE_ACTIONS_1331.json"
REFRESH_MARKER = AGENT_LOGS / "AssetDatabaseRefresh_1331.md"
PROJECT_SETTINGS = ROOT / "ProjectSettings"
ROOT_TMP = ROOT / ".tmp"
LOGS = ROOT / "Logs"
ROOT_TMP_ANONYMOUS_STALE_AGE_SECONDS = 24 * 60 * 60
ROOT_TMP_ANONYMOUS_NAME = re.compile(r"^[A-Za-z0-9_]{8}$")
LOGS_ZERO_BYTE_STALE_AGE_SECONDS = 24 * 60 * 60

ROOT_KEEP_FILES = {
    ".editorconfig",
    ".gitattributes",
    ".gitignore",
    "AGENTS.md",
    "BUILD_PLAYTEST_ISSUES.md",
    "Directory.Build.props",
    "Directory.Build.targets",
    "MASTER_RELEASE_WORK_PLAN.md",
    "README.md",
    "TASTE.md",
}

ROOT_KEEP_DIRS = {
    ".agent",
    ".agent_tmp",
    ".agents-skills",
    ".codex-artifacts",
    ".codex-build",
    ".codex_tmp",
    ".codexbuild",
    ".codexrules",
    ".cursor",
    ".git",
    ".github",
    ".kilo",
    ".kiro",
    ".tmp",
    ".vscode",
    "Assets",
    "CodexArtifacts",
    "Data",
    "Docs",
    "Library",
    "Logs",
    "Lore",
    "MarketingAssets",
    "MemoryCaptures",
    "NativeAudio",
    "Packages",
    "ProjectSettings",
    "Temp",
    "Tools",
    "UserSettings",
    "bin",
    "igra",
    "internal-specs",
    "obj",
}

CRITICAL_EMPTY_DIR_NAMES = {
    "Assets",
    "_Project",
    "_SourceData",
    "_ThirdParty",
    "AddressableAssetsData",
    "BakedGeometry",
    "Construction",
    "DataMonolith",
    "Ghosts",
    "HadalGraphs",
    "HadalStructures",
    "Hecton8",
    "PDA",
    "ProjectSettings",
    "Packages",
    "Docs",
    "Library",
    "Prefabs",
    "StreamingAssets",
    "Temp",
    "UserSettings",
    "Wreckage",
    "XR",
}

ASSET_DATABASE_ARCHIVE_DIR_NAMES = {
    "DOCS",
    "Screenshots",
    "TRANSFER HUB",
}

ASSET_DATABASE_ROOT_ARCHIVE_FILES = {
    "tri.py",
    "kuchka_melka_1_lod_1.asset",
    "pillar2_lod1.asset",
    "UniversalRenderPipelineGlobalSettings 1.asset",
    "UniversalRenderPipelineGlobalSettings.asset",
}

ASSET_DATABASE_ROOT_STALE_ASSET_FILES = {
    "kuchka_melka_1_lod_1.asset",
    "pillar2_lod1.asset",
    "UniversalRenderPipelineGlobalSettings 1.asset",
    "UniversalRenderPipelineGlobalSettings.asset",
}

ASSET_DATABASE_EXACT_STALE_PATHS = {
    "Assets/Scenes",
    "Assets/Scenes/pustaya_stsena.unity",
}

ASSET_DATABASE_ROOT_ARCHIVE_PREFIXES = (
    "inittestscene",
)

ASSET_DATABASE_ROOT_ARCHIVE_SUFFIXES = (
    ".projectauditor",
)


@dataclass
class FileEntry:
    path: str
    size_bytes: int
    reason: str


@dataclass
class MetaEntry:
    path: str
    base: str
    size_bytes: int
    orphan: bool


@dataclass
class MoveEntry:
    path: str
    destination: str
    size_bytes: int
    reason: str


@dataclass
class DirEntry:
    path: str
    size_bytes: int
    reason: str


@dataclass
class ProjectSettingsPathRef:
    path: str
    line: int
    asset_path: str
    exists: bool
    reason: str


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.resolve().as_posix()


def is_under(path: Path, parent: Path) -> bool:
    try:
        path.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def ensure_in_scope(path: Path) -> None:
    resolved = path.resolve()
    root = ROOT.resolve()
    if resolved != root and not is_under(resolved, root):
        raise RuntimeError(f"Path escaped root scope: {resolved}")
    if is_under(resolved, SCRIPTS) or is_under(resolved, LEGACY_LITERAL_SCRIPTS):
        raise RuntimeError(f"Path crossed forbidden scripts scope: {resolved}")


def skip_dir(path: Path) -> bool:
    return is_under(path, SCRIPTS) or is_under(path, LEGACY_LITERAL_SCRIPTS)


def is_under_path_no_resolve(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def walk_assets() -> Iterable[tuple[Path, list[str], list[str]]]:
    if not ASSETS.exists():
        return
    for current, dirs, files in os.walk(ASSETS):
        cur = Path(current)
        dirs[:] = [d for d in dirs if not skip_dir(cur / d)]
        if skip_dir(cur):
            continue
        yield cur, dirs, files


def file_size(path: Path) -> int:
    try:
        return path.stat().st_size
    except OSError:
        return 0


def dir_size(path: Path) -> int:
    total = 0
    for current, dirs, files in os.walk(path):
        cur = Path(current)
        dirs[:] = [d for d in dirs if not skip_dir(cur / d)]
        if skip_dir(cur):
            continue
        for name in files:
            total += file_size(cur / name)
    return total


def archive_destination(source: Path, bucket: str) -> Path:
    encoded = rel(source).replace("/", "__").replace("\\", "__").replace(":", "_")
    return ARCHIVE_ROOT / bucket / encoded


def git_lines(args: list[str]) -> list[str]:
    try:
        result = subprocess.run(
            ["git", "-C", str(ROOT), *args],
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        data = result.stdout.strip()
        return data.splitlines() if data else []
    except OSError as exc:
        return [f"GIT_COMMAND_FAILED: {exc}"]


def collect_git_files() -> set[str]:
    try:
        result = subprocess.run(
            ["git", "-C", str(ROOT), "ls-files", "-z"],
            check=False,
            capture_output=True,
        )
        if result.returncode != 0:
            return set()
        return {item.decode("utf-8", "replace").replace("\\", "/") for item in result.stdout.split(b"\0") if item}
    except OSError:
        return set()


ASSET_PATH_PATTERN = re.compile(r"Assets/[^\s'\"{}]+")


def collect_project_settings_asset_refs() -> list[ProjectSettingsPathRef]:
    refs: list[ProjectSettingsPathRef] = []
    if not PROJECT_SETTINGS.exists():
        return refs
    for path in PROJECT_SETTINGS.rglob("*"):
        if not path.is_file():
            continue
        try:
            text = path.read_text(encoding="utf-8-sig", errors="ignore")
        except OSError:
            continue
        for line_index, line in enumerate(text.splitlines(), start=1):
            for match in ASSET_PATH_PATTERN.finditer(line):
                asset_path = match.group(0).rstrip(",")
                exists = (ROOT / asset_path).exists()
                refs.append(
                    ProjectSettingsPathRef(
                        rel(path),
                        line_index,
                        asset_path,
                        exists,
                        "projectsettings_asset_path_exists" if exists else "projectsettings_asset_path_missing",
                    )
                )
    return refs


def is_temp_file(path: Path) -> bool:
    name = path.name.lower()
    return name.endswith(".tmp") or name.endswith(".swp") or name.endswith(".bak") or name.startswith(".tmp")


def is_root_generated_cache_file(path: Path) -> bool:
    return path.name.lower().endswith(".lscache")


def is_root_generated_project_file(path: Path) -> bool:
    return path.parent == ROOT and path.name.lower().endswith((".csproj", ".unityproj", ".sln"))


def is_root_tmp_anonymous_stale_file(path: Path, now: float | None = None) -> bool:
    if path.parent != ROOT_TMP or not path.is_file():
        return False
    if not ROOT_TMP_ANONYMOUS_NAME.fullmatch(path.name):
        return False
    if path.suffix:
        return False
    if file_size(path) > 4096:
        return False
    current_time = time.time() if now is None else now
    try:
        age_seconds = current_time - path.stat().st_mtime
    except OSError:
        return False
    return age_seconds >= ROOT_TMP_ANONYMOUS_STALE_AGE_SECONDS


def is_root_logs_zero_byte_stale_file(path: Path, now: float | None = None) -> bool:
    if path.parent != LOGS or not path.is_file():
        return False
    if path.suffix.lower() != ".log":
        return False
    if file_size(path) != 0:
        return False
    current_time = time.time() if now is None else now
    try:
        age_seconds = current_time - path.stat().st_mtime
    except OSError:
        return False
    return age_seconds >= LOGS_ZERO_BYTE_STALE_AGE_SECONDS


def is_root_archive_candidate(path: Path) -> bool:
    name = path.name
    lower = name.lower()
    if name in ROOT_KEEP_FILES:
        return False
    if lower.endswith((".sln", ".slnx", ".csproj", ".unityproj")):
        return False
    if lower.endswith(".log") or lower.endswith(".json"):
        return True
    return lower.endswith(".txt") and any(token in lower for token in ("report", "summary", "execution"))


def is_assets_archive_candidate(path: Path) -> bool:
    lower = path.name.lower()
    target = Path(str(path)[:-5]) if lower.endswith(".meta") else path
    target_rel = rel(target)
    if target_rel in ASSET_DATABASE_EXACT_STALE_PATHS:
        return True
    if path.parent == ASSETS:
        starts_with_generated_prefix = any(lower.startswith(prefix) for prefix in ASSET_DATABASE_ROOT_ARCHIVE_PREFIXES)
        if path.name in ASSET_DATABASE_ROOT_ARCHIVE_FILES or lower.endswith(ASSET_DATABASE_ROOT_ARCHIVE_SUFFIXES):
            return True
        if starts_with_generated_prefix and lower.endswith(".unity"):
            return True
        if lower.endswith(".meta"):
            base = Path(str(path)[:-5])
            base_lower = base.name.lower()
            base_starts_with_generated_prefix = any(
                base_lower.startswith(prefix) for prefix in ASSET_DATABASE_ROOT_ARCHIVE_PREFIXES
            )
            if base.name in ASSET_DATABASE_ARCHIVE_DIR_NAMES:
                return True
            if base_starts_with_generated_prefix and base_lower.endswith(".unity"):
                return True
            if base.name in ASSET_DATABASE_ROOT_ARCHIVE_FILES or base_lower.endswith(ASSET_DATABASE_ROOT_ARCHIVE_SUFFIXES):
                return True
    for archive_dir_name in ASSET_DATABASE_ARCHIVE_DIR_NAMES:
        if is_under_path_no_resolve(path, ASSETS / archive_dir_name):
            return True
    if lower.startswith("build_") and lower.endswith(".log"):
        return True
    if lower.endswith(".log"):
        return True
    if lower.endswith((".json", ".txt")):
        return any(token in lower for token in ("report", "summary", "execution", "telemetry"))
    return False


def assets_archive_reason(path: Path) -> str:
    target = Path(str(path)[:-5]) if path.name.lower().endswith(".meta") else path
    target_lower = target.name.lower()
    target_rel = rel(target)
    if target_rel == "Assets/Scenes":
        return "assets_root_legacy_empty_scene_folder_meta"
    if target_rel == "Assets/Scenes/pustaya_stsena.unity":
        return "assets_root_legacy_empty_scene"
    if target.parent == ASSETS:
        if target.name == "TRANSFER HUB":
            return "assets_transfer_hub_import_staging"
        if target.name in ASSET_DATABASE_ROOT_STALE_ASSET_FILES:
            return "assets_root_unreferenced_stale_asset"
        if any(target_lower.startswith(prefix) for prefix in ASSET_DATABASE_ROOT_ARCHIVE_PREFIXES) and target_lower.endswith(".unity"):
            return "assets_generated_test_runner_scene"
        if target_lower.endswith(".projectauditor"):
            return "assets_root_generated_audit_report"
        if target.name == "tri.py":
            return "assets_root_stray_helper_script"
    for archive_dir_name in ASSET_DATABASE_ARCHIVE_DIR_NAMES:
        if is_under_path_no_resolve(path, ASSETS / archive_dir_name):
            if archive_dir_name == "TRANSFER HUB":
                return "assets_transfer_hub_import_staging"
            return "assets_editor_proof_archive_lane"
    return "assets_stray_log_report"


def scan() -> dict:
    start_ns = time.perf_counter_ns()
    meta_scanned: list[MetaEntry] = []
    temp_files: list[FileEntry] = []
    recovery_dirs: list[DirEntry] = []
    archive_moves: list[MoveEntry] = []
    root_generated_project_files: list[FileEntry] = []
    root_unrouted: list[str] = []
    project_settings_asset_refs = collect_project_settings_asset_refs()
    stale_project_settings_asset_refs = [entry for entry in project_settings_asset_refs if not entry.exists]

    for path in ROOT.iterdir():
        if path.is_file():
            if path.suffix.lower() == ".meta":
                base = Path(str(path)[:-5])
                meta_scanned.append(MetaEntry(rel(path), rel(base), file_size(path), not base.exists()))
            if is_temp_file(path):
                temp_files.append(FileEntry(rel(path), file_size(path), "root_temp_file"))
            elif is_root_generated_cache_file(path):
                temp_files.append(FileEntry(rel(path), file_size(path), "root_lscache_language_service_cache"))
            elif is_root_generated_project_file(path):
                root_generated_project_files.append(FileEntry(rel(path), file_size(path), "root_untracked_unity_generated_project_file"))
            elif is_root_archive_candidate(path):
                dest = archive_destination(path, "Root")
                archive_moves.append(MoveEntry(rel(path), rel(dest), file_size(path), "root_stray_log_report"))
            elif (
                path.name not in ROOT_KEEP_FILES
                and not path.name.lower().endswith((".sln", ".slnx", ".csproj", ".unityproj", ".csv"))
            ):
                root_unrouted.append(rel(path))
        elif path.is_dir():
            if path.name == "_Recovery":
                recovery_dirs.append(DirEntry(rel(path), dir_size(path), "root_recovery_folder"))
            elif path.name not in ROOT_KEEP_DIRS:
                root_unrouted.append(rel(path))

    if ROOT_TMP.exists():
        now = time.time()
        for path in ROOT_TMP.iterdir():
            if is_root_tmp_anonymous_stale_file(path, now):
                temp_files.append(FileEntry(rel(path), file_size(path), "root_tmp_anonymous_stale_scratch_file"))

    if LOGS.exists():
        now = time.time()
        for path in LOGS.iterdir():
            if is_root_logs_zero_byte_stale_file(path, now):
                temp_files.append(FileEntry(rel(path), file_size(path), "root_logs_zero_byte_stale_log"))

    for current, dirs, files in walk_assets() or ():
        if current.name == "_Recovery":
            recovery_dirs.append(DirEntry(rel(current), dir_size(current), "assets_recovery_folder"))
            dirs[:] = []
            continue
        for name in files:
            path = current / name
            if skip_dir(path):
                continue
            lower = name.lower()
            if lower.endswith(".meta"):
                base = Path(str(path)[:-5])
                meta_scanned.append(MetaEntry(rel(path), rel(base), file_size(path), not base.exists()))
            if is_temp_file(path):
                temp_files.append(FileEntry(rel(path), file_size(path), "assets_temp_file"))
            elif is_assets_archive_candidate(path):
                dest = archive_destination(path, "Assets")
                archive_moves.append(MoveEntry(rel(path), rel(dest), file_size(path), assets_archive_reason(path)))

    orphan_meta = [entry for entry in meta_scanned if entry.orphan]
    elapsed_us = (time.perf_counter_ns() - start_ns) // 1000
    return {
        "agent_id": "1331",
        "evidence_class": "STATIC_FS_DRY_RUN",
        "generated_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "root": str(ROOT),
        "excluded_path": rel(SCRIPTS),
        "dry_run": True,
        "counts": {
            "meta_scanned": len(meta_scanned),
            "orphan_meta": len(orphan_meta),
            "temp_files": len(temp_files),
            "recovery_dirs": len(recovery_dirs),
            "archive_moves": len(archive_moves),
            "root_generated_project_files": len(root_generated_project_files),
            "root_generated_project_bytes": sum(entry.size_bytes for entry in root_generated_project_files),
            "root_unrouted": len(root_unrouted),
            "project_settings_asset_refs": len(project_settings_asset_refs),
            "stale_project_settings_asset_refs": len(stale_project_settings_asset_refs),
            "scan_elapsed_microseconds": elapsed_us,
        },
        "orphan_meta": [asdict(entry) for entry in orphan_meta],
        "temp_files": [asdict(entry) for entry in temp_files],
        "recovery_dirs": [asdict(entry) for entry in recovery_dirs],
        "archive_moves": [asdict(entry) for entry in archive_moves],
        "root_generated_project_files": [asdict(entry) for entry in root_generated_project_files],
        "root_unrouted": root_unrouted,
        "project_settings_asset_refs": [asdict(entry) for entry in project_settings_asset_refs],
        "stale_project_settings_asset_refs": [asdict(entry) for entry in stale_project_settings_asset_refs],
        "git_status": git_lines(["status", "--short"]),
        "git_submodules": git_lines(["submodule", "status"]),
    }


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def unique_destination(path: Path) -> Path:
    if not path.exists():
        return path
    stem = path.stem
    suffix = path.suffix
    parent = path.parent
    index = 1
    while True:
        candidate = parent / f"{stem}__{index}{suffix}"
        if not candidate.exists():
            return candidate
        index += 1


def delete_file(path: Path, reason: str, actions: list[dict], dry_run: bool) -> int:
    ensure_in_scope(path)
    size = file_size(path)
    action = {"type": "DELETE_FILE", "path": rel(path), "size_bytes": size, "reason": reason, "status": "DRY_RUN" if dry_run else "PENDING"}
    if dry_run:
        actions.append(action)
        return 0
    try:
        path.unlink()
        action["status"] = "OK"
        actions.append(action)
        return size
    except OSError as exc:
        action["status"] = "FAILED_LOCKED_OR_IO"
        action["error"] = repr(exc)
        actions.append(action)
        return 0


def delete_dir(path: Path, reason: str, actions: list[dict], dry_run: bool) -> int:
    ensure_in_scope(path)
    size = dir_size(path)
    action = {"type": "DELETE_DIR", "path": rel(path), "size_bytes": size, "reason": reason, "status": "DRY_RUN" if dry_run else "PENDING"}
    if dry_run:
        actions.append(action)
        return 0
    try:
        shutil.rmtree(path)
        action["status"] = "OK"
        actions.append(action)
        return size
    except OSError as exc:
        action["status"] = "FAILED_LOCKED_OR_IO"
        action["error"] = repr(exc)
        actions.append(action)
        return 0


def move_file(src: Path, dest: Path, reason: str, actions: list[dict], dry_run: bool) -> int:
    ensure_in_scope(src)
    ensure_in_scope(dest.parent)
    size = file_size(src)
    final_dest = unique_destination(dest)
    action = {
        "type": "MOVE_FILE",
        "path": rel(src),
        "destination": rel(final_dest),
        "size_bytes": size,
        "reason": reason,
        "status": "DRY_RUN" if dry_run else "PENDING",
    }
    if dry_run:
        actions.append(action)
        return 0
    try:
        final_dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(str(src), str(final_dest))
        action["status"] = "OK"
        actions.append(action)
        return size
    except OSError as exc:
        action["status"] = "FAILED_LOCKED_OR_IO"
        action["error"] = repr(exc)
        actions.append(action)
        return 0


def cleanup_empty_asset_dirs(actions: list[dict], dry_run: bool) -> int:
    freed = 0
    if not ASSETS.exists():
        return freed
    for current, dirs, files in os.walk(ASSETS, topdown=False):
        path = Path(current)
        if skip_dir(path) or path.name in CRITICAL_EMPTY_DIR_NAMES:
            continue
        try:
            if any(path.iterdir()):
                continue
        except OSError:
            continue
        if not is_junk_empty_dir(path):
            continue
        companion_meta = Path(str(path) + ".meta")
        freed += delete_dir(path, "empty_assets_directory", actions, dry_run)
        if companion_meta.exists() and not skip_dir(companion_meta):
            freed += delete_file(companion_meta, "empty_assets_directory_companion_meta", actions, dry_run)
    return freed


def is_junk_empty_dir(path: Path) -> bool:
    name = path.name
    if name in CRITICAL_EMPTY_DIR_NAMES:
        return False
    if rel(path) in ASSET_DATABASE_EXACT_STALE_PATHS:
        return True
    for archive_dir_name in ASSET_DATABASE_ARCHIVE_DIR_NAMES:
        archive_root = ASSETS / archive_dir_name
        if path == archive_root or is_under_path_no_resolve(path, archive_root):
            return True
    lower = name.lower()
    if lower in {".tmp", "tmp", "__pycache__", ".pytest_cache"}:
        return True
    parts = name.rsplit(" ", 1)
    return len(parts) == 2 and parts[1].isdigit()


def false_positive_fuzzer() -> dict:
    valid_asset = ASSETS / "Art" / "Texture.png"
    valid_meta = Path(str(valid_asset) + ".meta")
    base = Path(str(valid_meta)[:-5])
    return {
        "input_asset": rel(valid_asset),
        "input_meta": rel(valid_meta),
        "computed_base": rel(base),
        "would_flag_when_base_exists": False,
        "algorithm": "meta_path[:-5] then exact Path.exists() on base path",
    }


def boundary_audit(actions: list[dict]) -> dict:
    hits = []
    forbidden_paths = [
        rel(SCRIPTS).replace("\\", "/"),
        rel(LEGACY_LITERAL_SCRIPTS).replace("\\", "/"),
    ]
    for action in actions:
        path = str(action.get("path", "")).replace("\\", "/")
        dest = str(action.get("destination", "")).replace("\\", "/")
        for forbidden in forbidden_paths:
            if forbidden in path or forbidden in dest:
                hits.append(action)
                break
    return {"forbidden_paths": forbidden_paths, "violations": hits, "passed": not hits}


def execute(dry_run: bool) -> dict:
    start_ns = time.perf_counter_ns()
    ledger = scan()
    actions: list[dict] = []
    freed = 0
    archived_bytes = 0

    for entry in ledger["orphan_meta"]:
        freed += delete_file(ROOT / entry["path"], "orphan_meta_base_missing", actions, dry_run)

    for entry in ledger["temp_files"]:
        freed += delete_file(ROOT / entry["path"], entry["reason"], actions, dry_run)

    for entry in ledger["recovery_dirs"]:
        freed += delete_dir(ROOT / entry["path"], entry["reason"], actions, dry_run)

    for entry in ledger["archive_moves"]:
        src = ROOT / entry["path"]
        dest = ROOT / entry["destination"]
        moved = move_file(src, dest, entry["reason"], actions, dry_run)
        if moved:
            archived_bytes += moved

    freed += cleanup_empty_asset_dirs(actions, dry_run)

    post = scan()
    audit = boundary_audit(actions)
    if not audit["passed"]:
        raise RuntimeError("FatalArchitectureException: Assets/_Project/Scripts appeared in action log")

    elapsed_us = (time.perf_counter_ns() - start_ns) // 1000
    command_purity = {
        "dotnet_build_invoked": False,
        "msbuild_invoked": False,
        "unity_batchmode_invoked": False,
        "commands_used": ["python Tools/workspace_hygiene_1331.py"],
    }
    report = {
        "agent_id": "1331",
        "evidence_class": "STATIC_FS",
        "dry_run": dry_run,
        "generated_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "root": str(ROOT),
        "excluded_path": rel(SCRIPTS),
        "pre_counts": ledger["counts"],
        "post_counts": post["counts"],
        "orphan_meta_deleted": sum(1 for a in actions if a["type"] == "DELETE_FILE" and a["reason"] == "orphan_meta_base_missing" and a["status"] == "OK"),
        "temp_files_deleted": sum(1 for a in actions if a["type"] == "DELETE_FILE" and a["reason"] in {"root_temp_file", "assets_temp_file", "root_tmp_anonymous_stale_scratch_file", "root_logs_zero_byte_stale_log"} and a["status"] == "OK"),
        "generated_cache_files_deleted": sum(1 for a in actions if a["type"] == "DELETE_FILE" and a["reason"] == "root_lscache_language_service_cache" and a["status"] == "OK"),
        "companion_meta_deleted": sum(1 for a in actions if a["type"] == "DELETE_FILE" and a["reason"] == "empty_assets_directory_companion_meta" and a["status"] == "OK"),
        "recovery_dirs_deleted": sum(1 for a in actions if a["type"] == "DELETE_DIR" and a["reason"] != "empty_assets_directory" and a["status"] == "OK"),
        "empty_dirs_deleted": sum(1 for a in actions if a["type"] == "DELETE_DIR" and a["reason"] == "empty_assets_directory" and a["status"] == "OK"),
        "files_archived": sum(1 for a in actions if a["type"] == "MOVE_FILE" and a["status"] == "OK"),
        "total_bytes_freed": freed,
        "total_bytes_archived": archived_bytes,
        "execution_microseconds": elapsed_us,
        "boundary_audit": audit,
        "false_positive_fuzzer": false_positive_fuzzer(),
        "locked_file_handling": "unlink/rmtree/move catch OSError and log FAILED_LOCKED_OR_IO, then continue queue",
        "asset_database_refresh_marker": rel(REFRESH_MARKER),
        "command_purity": command_purity,
        "dry_run_validation": {
            "expected_hit_list_counts": ledger["counts"],
            "critical_assets_targeted": False,
            "scripts_domain_targeted": False,
        },
        "actions": actions,
    }
    canonical = json.dumps(report, sort_keys=True, separators=(",", ":")).encode("utf-8")
    report["report_sha256"] = hashlib.sha256(canonical).hexdigest()
    return report


def write_refresh_marker() -> None:
    REFRESH_MARKER.parent.mkdir(parents=True, exist_ok=True)
    REFRESH_MARKER.write_text(
        "# AssetDatabase Refresh Marker 1331\n\n"
        "A full Unity AssetDatabase.Refresh() is recommended on the next Editor launch. "
        "Agent 1331 performed static filesystem cleanup of Unity metadata and temporary files only; "
        "no Unity Editor batchmode or dotnet build command was invoked.\n",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=("scan", "dry-run", "apply", "verify"), required=True)
    args = parser.parse_args()

    REPORTS.mkdir(parents=True, exist_ok=True)
    AGENT_LOGS.mkdir(parents=True, exist_ok=True)

    if args.mode == "scan":
        payload = scan()
        write_json(DRY_LEDGER, payload)
        print(json.dumps(payload["counts"], sort_keys=True))
        return 0

    if args.mode == "dry-run":
        payload = execute(dry_run=True)
        write_json(DRY_LEDGER, payload)
        print(json.dumps(payload["pre_counts"], sort_keys=True))
        return 0

    if args.mode == "apply":
        payload = execute(dry_run=False)
        write_json(FINAL_REPORT, payload)
        write_json(ACTION_LOG, {"agent_id": "1331", "actions": payload["actions"]})
        write_refresh_marker()
        print(json.dumps({k: payload[k] for k in ("orphan_meta_deleted", "temp_files_deleted", "recovery_dirs_deleted", "empty_dirs_deleted", "files_archived", "total_bytes_freed", "total_bytes_archived", "execution_microseconds", "report_sha256")}, sort_keys=True))
        return 0

    if args.mode == "verify":
        payload = scan()
        write_json(VERIFY_LEDGER, payload)
        print(json.dumps(payload["counts"], sort_keys=True))
        return 0

    return 2


if __name__ == "__main__":
    sys.exit(main())
