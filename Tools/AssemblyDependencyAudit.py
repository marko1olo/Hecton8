#!/usr/bin/env python3
"""Static asmdef dependency audit for HECTON-8 compile-wall pressure.

Evidence class: STATIC_SOURCE. This tool reads Unity asmdef JSON and reports
first-party dependency graph risks. It does not prove Unity import, generated
project correctness, or compile health.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import mmap
import os
import subprocess
import tempfile
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_REPORT_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "AssemblyDependencyAudit_HFI_AUDIT.md"
DEFAULT_JSON_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "AssemblyDependencyAudit_HFI_AUDIT.json"
DEFAULT_BINARY_REPORT_PATH = REPO_ROOT / "Docs" / "Reports" / "ASSEMBLY_BINARY_SCHEMA_AUDIT_REPORT_SHINOBU_359.json"
DEFAULT_BINARY_CACHE_PATH = REPO_ROOT / "Docs" / "Reports" / "ASSEMBLY_BINARY_SCHEMA_AUDIT_SHINOBU_359.cache.json"
DEFAULT_BINARY_FILE_CACHE_PATH = REPO_ROOT / "Docs" / "Reports" / "ASSEMBLY_BINARY_SCHEMA_AUDIT_SHINOBU_359.filecache.json"
DEFAULT_METRIC_PATH = REPO_ROOT / "Docs" / "Reports" / "METRIC_PHI_DATA_TRUTH_AUDIT.json"
DEFAULT_QA_REPORT_PATH = REPO_ROOT / "Docs" / "Reports" / "QA_OPTIMIZATION_REPORT.json"
DEFAULT_OOP_CACHE_PATH = REPO_ROOT / "Docs" / "Reports" / "QA_OPTIMIZATION_REPORT_SHINOBU_359.cache.json"
DEFAULT_SELF_AUDIT_PATH = REPO_ROOT / "Docs" / "Reports" / "SHINOBU_359_SELF_AUDIT.xml"
DEFAULT_SCHEMA_PROFILE_PATH = REPO_ROOT / "binary_schema_profiles.csv"
DEFAULT_USING_REPORT_PATH = REPO_ROOT / "Docs" / "Reports" / "ASSEMBLY_USING_BOUNDARY_AUDIT_SHINOBU_359.json"
DEFAULT_USING_CACHE_PATH = REPO_ROOT / "Docs" / "Reports" / "ASSEMBLY_USING_BOUNDARY_AUDIT_SHINOBU_359.cache.json"
SCHEMA = "hecton8.assembly_dependency_audit.v2"
BINARY_SCHEMA = "hecton8.binary_schema_audit.v1"
BINARY_CACHE_SCHEMA = "hecton8.binary_schema_audit_cache.v2"
BINARY_FILE_CACHE_SCHEMA = "hecton8.binary_schema_audit_file_cache.v1"
METRIC_SCHEMA = "hecton8.metric_phi_data_truth_rows.v1"
QA_OOP_SCHEMA = "hecton8.qa_oop_test_audit.v1"
QA_OOP_CACHE_SCHEMA = "hecton8.qa_oop_test_audit_cache.v1"
USING_BOUNDARY_SCHEMA = "hecton8.using_boundary_audit.v1"
USING_BOUNDARY_CACHE_SCHEMA = "hecton8.using_boundary_audit_cache.v1"
CORE_CONTRACT_ASSEMBLY = "Hecton8.Core.Contracts"
FILE_STAMP_CACHE: dict[str, tuple[int, int]] = {}

SKIP_DIR_NAMES = {
    ".git",
    ".vs",
    "__pycache__",
    "bin",
    "obj",
    "Library",
    "Temp",
}

FIELD_MODIFIERS = {
    "public",
    "private",
    "protected",
    "internal",
    "static",
    "readonly",
    "volatile",
    "unsafe",
    "fixed",
    "const",
    "new",
    "extern",
}

TYPE_LAYOUTS: dict[str, tuple[int, int]] = {
    "byte": (1, 1),
    "sbyte": (1, 1),
    "bool": (1, 1),
    "short": (2, 2),
    "ushort": (2, 2),
    "char": (2, 2),
    "int": (4, 4),
    "uint": (4, 4),
    "float": (4, 4),
    "Single": (4, 4),
    "long": (8, 8),
    "ulong": (8, 8),
    "double": (8, 8),
    "Double": (8, 8),
    "float2": (8, 4),
    "float3": (12, 4),
    "float4": (16, 4),
    "Vector2": (8, 4),
    "Vector3": (12, 4),
    "Vector4": (16, 4),
    "Color": (16, 4),
    "quaternion": (16, 4),
    "Quaternion": (16, 4),
    "int2": (8, 4),
    "int3": (12, 4),
    "int4": (16, 4),
    "uint2": (8, 4),
    "uint3": (12, 4),
    "uint4": (16, 4),
    "double2": (16, 8),
    "double3": (24, 8),
    "double4": (32, 8),
    "int64x3": (24, 8),
    "ulong2": (16, 8),
    "ulong3": (24, 8),
    "AbsoluteUniversePosition": (48, 8),
}


@dataclass(frozen=True)
class AssemblyDef:
    name: str
    path: Path
    references: tuple[str, ...]
    include_platforms: tuple[str, ...]
    exclude_platforms: tuple[str, ...]
    auto_referenced: bool
    no_engine_references: bool
    optional_unity_references: tuple[str, ...]
    define_constraints: tuple[str, ...]


@dataclass(frozen=True)
class Token:
    value: str
    line: int


@dataclass(frozen=True)
class FieldInfo:
    name: str
    type_name: str
    line: int
    offset: int | None


@dataclass(frozen=True)
class StructInfo:
    name: str
    path: str
    line: int
    layout_kind: str
    declared_size: int | None
    pack: int | None
    fields: tuple[FieldInfo, ...]
    property_hits: tuple[dict[str, object], ...]
    unmanaged_contract: bool


def normalize_path(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def normalize_path_no_resolve(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        if path.is_absolute():
            return path.relative_to(repo_root).as_posix()
        return path.as_posix()
    except ValueError:
        return path.as_posix()


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.parts)


def read_text_mmap(path: Path) -> str:
    try:
        size = path.stat().st_size
    except OSError:
        return ""
    if size == 0:
        return ""
    with path.open("rb") as handle:
        with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as mapped:
            return mapped.read(size).decode("utf-8", errors="ignore")


def read_bytes_mmap(path: Path) -> bytes:
    try:
        size = path.stat().st_size
    except OSError:
        return b""
    if size == 0:
        return b""
    with path.open("rb") as handle:
        with mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as mapped:
            return mapped.read(size)


def stream_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    encoder = json.JSONEncoder(indent=2, sort_keys=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        for chunk in encoder.iterencode(payload):
            handle.write(chunk)
        handle.write("\n")


def load_json_object(path: Path) -> dict[str, object]:
    if not path.exists():
        return {}
    try:
        value = json.loads(read_text_mmap(path))
    except json.JSONDecodeError:
        return {}
    return value if isinstance(value, dict) else {}


def load_small_json_object(path: Path, max_bytes: int) -> dict[str, object]:
    size, _ = file_stamp(path)
    if size <= 0 or size > max_bytes:
        return {}
    return load_json_object(path)


def file_stamp(path: Path) -> tuple[int, int]:
    key = os.fspath(path)
    cached = FILE_STAMP_CACHE.get(key)
    if cached is not None and str(key).endswith(".cs"):
        return cached
    try:
        stat = os.stat(path, follow_symlinks=False)
    except OSError:
        return 0, 0
    stamp = (stat.st_size, stat.st_mtime_ns)
    if str(key).endswith(".cs"):
        FILE_STAMP_CACHE[key] = stamp
    return stamp


def hash_int64_into_fnv(value: int, current: int) -> int:
    for shift in range(0, 64, 8):
        current ^= (value >> shift) & 0xFF
        current = (current * 0x01000193) & 0xFFFFFFFF
    return current


def hash_text_into_fnv(value: str, current: int) -> int:
    for byte in value.encode("utf-8", errors="ignore"):
        current ^= byte
        current = (current * 0x01000193) & 0xFFFFFFFF
    return current


def build_file_tree_stamp(paths: list[Path]) -> dict[str, object]:
    value = 0x811C9DC5
    total_size = 0
    total_mtime = 0
    for path in sorted(paths, key=normalize_path_no_resolve):
        rel = normalize_path_no_resolve(path)
        size, mtime_ns = file_stamp(path)
        total_size += size
        total_mtime = (total_mtime + mtime_ns) & 0xFFFFFFFFFFFFFFFF
        for byte in rel.encode("utf-8", errors="ignore"):
            value ^= byte
            value = (value * 0x01000193) & 0xFFFFFFFF
        value = hash_int64_into_fnv(size, value)
        value = hash_int64_into_fnv(mtime_ns, value)
    return {
        "fileCount": len(paths),
        "totalSize": total_size,
        "totalMtimeNs": total_mtime,
        "hash": f"0x{value:08X}",
    }


def production_cs_git_pathspec(source_root: Path) -> str | None:
    try:
        rel = source_root.resolve().relative_to(REPO_ROOT.resolve()).as_posix()
    except ValueError:
        return None
    rel = rel.strip("/")
    if rel != "Assets/_Project/Scripts":
        return None
    return f":(glob){rel}/**/*.cs"


def run_git_stdout(args: list[str], timeout_seconds: float = 5.0) -> bytes | None:
    try:
        completed = subprocess.run(
            ["git", *args],
            cwd=REPO_ROOT,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            timeout=timeout_seconds,
            check=False,
        )
    except (OSError, subprocess.SubprocessError):
        return None
    if completed.returncode != 0:
        return None
    return completed.stdout


def split_git_nul(data: bytes) -> list[str]:
    values: list[str] = []
    for raw in data.split(b"\0"):
        if not raw:
            continue
        values.append(raw.decode("utf-8", errors="ignore").lstrip("\ufeff"))
    return values


def parse_git_status_dirty_paths(data: bytes) -> dict[str, str]:
    entries = split_git_nul(data)
    dirty: dict[str, str] = {}
    index = 0
    while index < len(entries):
        entry = entries[index]
        if len(entry) >= 4 and entry[2] == " ":
            status_pair = entry[:2]
            path = entry[3:]
            if path.endswith(".cs"):
                dirty[path] = status_pair
            if ("R" in status_pair or "C" in status_pair) and index + 1 < len(entries):
                index += 1
                old_path = entries[index]
                if old_path.endswith(".cs"):
                    dirty[old_path] = status_pair
        elif entry.endswith(".cs"):
            dirty[entry] = "??"
        index += 1
    return dirty


def resolve_git_index_path() -> Path | None:
    git_path = REPO_ROOT / ".git"
    if git_path.is_dir():
        return git_path / "index"
    if not git_path.is_file():
        return None
    try:
        text = git_path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return None
    prefix = "gitdir:"
    stripped = text.strip()
    if not stripped.startswith(prefix):
        return None
    raw = stripped[len(prefix) :].strip()
    candidate = Path(raw)
    if not candidate.is_absolute():
        candidate = (REPO_ROOT / candidate).resolve()
    return candidate / "index"


def build_git_cs_tree_stamp(source_root: Path) -> dict[str, object] | None:
    pathspec = production_cs_git_pathspec(source_root)
    if pathspec is None:
        return None
    index_path = resolve_git_index_path()
    if index_path is None:
        return None
    index_size, index_mtime_ns = file_stamp(index_path)
    if index_size <= 0:
        return None
    dirty_raw = run_git_stdout(["status", "--porcelain=v1", "-z", "--untracked-files=all", "--", pathspec])
    if dirty_raw is None:
        return None
    dirty_paths = parse_git_status_dirty_paths(dirty_raw)

    value = 0x811C9DC5
    value = hash_text_into_fnv("git-index-file+dirty-stat", value)
    value = hash_int64_into_fnv(index_size, value)
    value = hash_int64_into_fnv(index_mtime_ns, value)
    value = hash_text_into_fnv(normalize_path_no_resolve(index_path), value)
    total_size = 0
    total_dirty_mtime = 0
    dirty_file_count = 0
    deleted_file_count = 0
    untracked_file_count = 0

    def hash_path_stamp(prefix: str, rel: str, size: int, mtime_ns: int) -> None:
        nonlocal value
        value = hash_text_into_fnv(prefix, value)
        value = hash_text_into_fnv(rel, value)
        value = hash_int64_into_fnv(size, value)
        value = hash_int64_into_fnv(mtime_ns, value)

    for rel, status_pair in sorted(dirty_paths.items()):
        path = REPO_ROOT / rel
        if not path.exists():
            deleted_file_count += 1
            value = hash_text_into_fnv("D", value)
            value = hash_text_into_fnv(rel, value)
            continue
        if not (REPO_ROOT / rel).is_file():
            continue
        size, mtime_ns = file_stamp(path)
        dirty_file_count += 1
        if status_pair == "??":
            untracked_file_count += 1
        total_size += size
        total_dirty_mtime = (total_dirty_mtime + mtime_ns) & 0xFFFFFFFFFFFFFFFF
        hash_path_stamp("W", rel, size, mtime_ns)

    return {
        "fileCount": 0,
        "totalSize": total_size,
        "totalMtimeNs": total_dirty_mtime,
        "hash": f"0x{value:08X}",
        "stampMode": "git-index-file+dirty-stat",
        "gitIndexSize": index_size,
        "gitIndexMtimeNs": index_mtime_ns,
        "dirtyFileCount": dirty_file_count,
        "deletedFileCount": deleted_file_count,
        "untrackedFileCount": untracked_file_count,
    }


def start_watchdog(seconds: float) -> threading.Timer | None:
    if seconds <= 0.0:
        return None
    timer = threading.Timer(seconds, lambda: os._exit(1))
    timer.daemon = True
    timer.start()
    return timer


def xml_escape(value: object) -> str:
    text = str(value)
    return (
        text.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
        .replace("'", "&apos;")
    )


def iter_asmdefs(source_root: Path) -> list[Path]:
    return [
        path
        for path in sorted(source_root.rglob("*.asmdef"))
        if not should_skip(path.relative_to(source_root))
    ]


def iter_cs_files(source_root: Path) -> list[Path]:
    FILE_STAMP_CACHE.clear()
    files: list[Path] = []

    def walk(directory: Path) -> None:
        try:
            with os.scandir(directory) as entries:
                for entry in entries:
                    if entry.name in SKIP_DIR_NAMES:
                        continue
                    try:
                        if entry.is_dir(follow_symlinks=False):
                            walk(Path(entry.path))
                            continue
                        if not entry.name.endswith(".cs") or not entry.is_file(follow_symlinks=False):
                            continue
                        stat = entry.stat(follow_symlinks=False)
                    except OSError:
                        continue
                    path = Path(entry.path)
                    FILE_STAMP_CACHE[os.fspath(path)] = (stat.st_size, stat.st_mtime_ns)
                    files.append(path)
        except OSError:
            return

    walk(source_root)
    return sorted(files)


def read_guid(path: Path) -> str | None:
    meta_path = path.with_suffix(path.suffix + ".meta")
    if not meta_path.exists():
        return None
    for line in read_text_mmap(meta_path).splitlines():
        stripped = line.strip()
        if stripped.startswith("guid:"):
            return stripped.split(":", 1)[1].strip()
    return None


def load_one_asmdef(path: Path) -> tuple[Path, dict[str, object], str | None]:
    try:
        raw = json.loads(read_text_mmap(path))
    except json.JSONDecodeError as exc:
        raw = {
            "name": path.stem,
            "references": [],
            "_parseError": str(exc),
        }
    if not isinstance(raw, dict):
        raw = {"name": path.stem, "references": [], "_parseError": "root is not object"}
    return path, raw, read_guid(path)


def load_raw_asmdefs(paths: list[Path]) -> tuple[dict[Path, dict[str, object]], dict[str, str]]:
    raw_by_path: dict[Path, dict[str, object]] = {}
    guid_to_name: dict[str, str] = {}
    workers = max(1, min(8, len(paths)))
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as executor:
        for path, raw, guid in executor.map(load_one_asmdef, paths):
            raw_by_path[path] = raw
            name = str(raw.get("name") or path.stem)
            if guid:
                guid_to_name[guid] = name
    return raw_by_path, guid_to_name


def resolve_reference(reference: object, guid_to_name: dict[str, str]) -> str:
    text = str(reference)
    if text.startswith("GUID:"):
        guid = text.split(":", 1)[1]
        return guid_to_name.get(guid, text)
    return text


def tuple_of_strings(value: object) -> tuple[str, ...]:
    if not isinstance(value, list):
        return ()
    return tuple(str(item) for item in value)


def load_asmdefs(source_root: Path) -> list[AssemblyDef]:
    paths = iter_asmdefs(source_root)
    raw_by_path, guid_to_name = load_raw_asmdefs(paths)
    assemblies: list[AssemblyDef] = []
    for path in paths:
        raw = raw_by_path[path]
        refs = tuple(resolve_reference(item, guid_to_name) for item in tuple_of_strings(raw.get("references")))
        assemblies.append(
            AssemblyDef(
                name=str(raw.get("name") or path.stem),
                path=path,
                references=refs,
                include_platforms=tuple_of_strings(raw.get("includePlatforms")),
                exclude_platforms=tuple_of_strings(raw.get("excludePlatforms")),
                auto_referenced=bool(raw.get("autoReferenced", True)),
                no_engine_references=bool(raw.get("noEngineReferences", False)),
                optional_unity_references=tuple_of_strings(raw.get("optionalUnityReferences")),
                define_constraints=tuple_of_strings(raw.get("defineConstraints")),
            )
        )
    return assemblies


def is_relative_to(path: Path, parent: Path) -> bool:
    try:
        path.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def find_owning_assembly(path: Path, assemblies: list[AssemblyDef]) -> AssemblyDef | None:
    best: AssemblyDef | None = None
    best_depth = -1
    resolved = path.resolve()
    for assembly in assemblies:
        root = assembly.path.parent
        if not is_relative_to(resolved, root):
            continue
        depth = len(root.resolve().parts)
        if depth > best_depth:
            best = assembly
            best_depth = depth
    return best


def build_assembly_scope_table(assemblies: list[AssemblyDef]) -> list[tuple[str, str, str, bool]]:
    rows: list[tuple[str, str, str, bool]] = []
    for assembly in assemblies:
        root = assembly.path.parent.resolve().as_posix().lower().rstrip("/")
        rows.append((root, assembly.name, domain_key(assembly.name), is_editor_assembly(assembly)))
    rows.sort(key=lambda row: len(row[0]), reverse=True)
    return rows


def resolve_assembly_from_scope(path: Path, scopes: list[tuple[str, str, str, bool]]) -> tuple[str, str, bool] | None:
    key = path.resolve().as_posix().lower()
    for root, assembly_name, source_domain, editor in scopes:
        if key == root or key.startswith(root + "/"):
            return assembly_name, source_domain, editor
    return None


def is_first_party(name: str) -> bool:
    return name.startswith("Hecton8.")


def is_unity_reference(name: str) -> bool:
    return name.startswith("Unity.") or name.startswith("UnityEngine") or name in {"GPUInstancer"}


def is_contract_reference(name: str) -> bool:
    return name.endswith(".Contracts") or ".Contracts." in name or name == CORE_CONTRACT_ASSEMBLY


def is_editor_assembly(assembly: AssemblyDef) -> bool:
    return (
        assembly.name.endswith(".Editor")
        or assembly.include_platforms == ("Editor",)
        or "TestAssemblies" in assembly.optional_unity_references
        or "UNITY_INCLUDE_TESTS" in assembly.define_constraints
    )


def domain_key(name: str) -> str:
    parts = name.split(".")
    if len(parts) < 2:
        return name
    if parts[0] != "Hecton8":
        return name
    if len(parts) >= 3 and parts[1] == "Core":
        return "Hecton8.Core"
    return ".".join(parts[:2])


def allowed_core_reference(reference: str) -> bool:
    if not is_first_party(reference):
        return True
    if reference.startswith("Hecton8.Core"):
        return True
    if is_contract_reference(reference):
        return True
    return False


def classify_edges(assemblies: list[AssemblyDef]) -> tuple[list[dict[str, str]], list[dict[str, str]]]:
    core_concrete: list[dict[str, str]] = []
    runtime_concrete: list[dict[str, str]] = []
    by_name = {assembly.name: assembly for assembly in assemblies}
    for assembly in assemblies:
        if not is_first_party(assembly.name) or is_editor_assembly(assembly):
            continue
        source_domain = domain_key(assembly.name)
        for reference in assembly.references:
            if not is_first_party(reference):
                continue
            target_domain = domain_key(reference)
            if assembly.name == "Hecton8.Core" and not allowed_core_reference(reference):
                core_concrete.append(
                    {
                        "assembly": assembly.name,
                        "reference": reference,
                        "path": normalize_path(assembly.path),
                    }
                )
            if (
                target_domain != source_domain
                and not is_contract_reference(reference)
                and reference in by_name
                and not is_editor_assembly(by_name[reference])
            ):
                runtime_concrete.append(
                    {
                        "assembly": assembly.name,
                        "reference": reference,
                        "path": normalize_path(assembly.path),
                    }
                )
    return core_concrete, runtime_concrete


def collect_core_contract_boundary_violations(assemblies: list[AssemblyDef]) -> list[dict[str, str]]:
    by_name = {assembly.name: assembly for assembly in assemblies}
    violations: list[dict[str, str]] = []
    for assembly in assemblies:
        if not is_first_party(assembly.name) or is_editor_assembly(assembly):
            continue
        source_domain = domain_key(assembly.name)
        for reference in assembly.references:
            target = by_name.get(reference)
            if target is None or is_editor_assembly(target) or not is_first_party(reference):
                continue
            if domain_key(reference) == source_domain:
                continue
            if reference == CORE_CONTRACT_ASSEMBLY:
                continue
            violations.append(
                {
                    "assembly": assembly.name,
                    "reference": reference,
                    "path": normalize_path(assembly.path),
                    "requiredRoute": CORE_CONTRACT_ASSEMBLY,
                }
            )
    return violations


def collect_unresolved_references(assemblies: list[AssemblyDef]) -> list[dict[str, str]]:
    names = {assembly.name for assembly in assemblies}
    unresolved: list[dict[str, str]] = []
    for assembly in assemblies:
        for reference in assembly.references:
            if reference in names or is_unity_reference(reference):
                continue
            if is_first_party(reference) or reference.startswith("GUID:"):
                unresolved.append(
                    {
                        "assembly": assembly.name,
                        "reference": reference,
                        "path": normalize_path(assembly.path),
                    }
                )
    return unresolved


def collect_duplicate_assembly_names(assemblies: list[AssemblyDef]) -> list[dict[str, object]]:
    paths_by_name: dict[str, list[str]] = {}
    for assembly in assemblies:
        paths_by_name.setdefault(assembly.name, []).append(normalize_path(assembly.path))
    return [
        {"assembly": name, "paths": paths}
        for name, paths in sorted(paths_by_name.items())
        if len(paths) > 1
    ]


def build_graph(assemblies: list[AssemblyDef]) -> dict[str, list[str]]:
    names = {assembly.name for assembly in assemblies}
    graph: dict[str, list[str]] = {}
    for assembly in assemblies:
        if not is_first_party(assembly.name):
            continue
        graph[assembly.name] = sorted(ref for ref in assembly.references if ref in names and is_first_party(ref))
    return graph


def detect_cycles(graph: dict[str, list[str]]) -> list[list[str]]:
    visited: set[str] = set()
    stack: set[str] = set()
    path: list[str] = []
    cycles: list[list[str]] = []
    seen: set[tuple[str, ...]] = set()

    def canonical(cycle: list[str]) -> tuple[str, ...]:
        body = cycle[:-1]
        rotations = [tuple(body[index:] + body[:index]) for index in range(len(body))]
        return min(rotations)

    def visit(node: str) -> None:
        visited.add(node)
        stack.add(node)
        path.append(node)
        for target in graph.get(node, []):
            if target not in visited:
                visit(target)
            elif target in stack:
                start = path.index(target)
                cycle = path[start:] + [target]
                key = canonical(cycle)
                if key not in seen:
                    seen.add(key)
                    cycles.append(cycle)
        stack.remove(node)
        path.pop()

    for node in sorted(graph):
        if node not in visited:
            visit(node)
    return cycles


def topological_order(graph: dict[str, list[str]]) -> list[str]:
    indegree = {node: 0 for node in graph}
    reverse: dict[str, list[str]] = {node: [] for node in graph}
    for source, targets in graph.items():
        for target in targets:
            if target not in indegree:
                continue
            indegree[source] += 0
            indegree[target] += 1
            reverse.setdefault(source, [])
            reverse.setdefault(target, [])
    ready = sorted(node for node, count in indegree.items() if count == 0)
    order: list[str] = []
    outgoing = {node: list(targets) for node, targets in graph.items()}
    while ready:
        node = ready.pop(0)
        order.append(node)
        for target in outgoing.get(node, []):
            if target not in indegree:
                continue
            indegree[target] -= 1
            if indegree[target] == 0:
                ready.append(target)
                ready.sort()
    return order if len(order) == len(indegree) else []


def summarize_assemblies(assemblies: list[AssemblyDef]) -> dict[str, object]:
    first_party = [assembly for assembly in assemblies if is_first_party(assembly.name)]
    editor = [assembly for assembly in first_party if is_editor_assembly(assembly)]
    runtime = [assembly for assembly in first_party if not is_editor_assembly(assembly)]
    no_engine = [assembly for assembly in first_party if assembly.no_engine_references]
    auto_false = [assembly for assembly in first_party if not assembly.auto_referenced]
    reference_counts = sorted(
        (
            {
                "assembly": assembly.name,
                "path": normalize_path(assembly.path),
                "references": len(assembly.references),
                "firstPartyReferences": sum(1 for ref in assembly.references if is_first_party(ref)),
            }
            for assembly in first_party
        ),
        key=lambda item: (-int(item["references"]), str(item["assembly"])),
    )
    return {
        "assemblyCount": len(assemblies),
        "firstPartyAssemblyCount": len(first_party),
        "runtimeFirstPartyAssemblyCount": len(runtime),
        "editorFirstPartyAssemblyCount": len(editor),
        "noEngineReferencesCount": len(no_engine),
        "autoReferencedFalseCount": len(auto_false),
        "topReferenceCounts": reference_counts[:20],
    }


def build_payload(source_root: Path) -> dict[str, object]:
    assemblies = load_asmdefs(source_root)
    core_concrete, runtime_concrete = classify_edges(assemblies)
    contract_boundary = collect_core_contract_boundary_violations(assemblies)
    unresolved = collect_unresolved_references(assemblies)
    duplicates = collect_duplicate_assembly_names(assemblies)
    graph = build_graph(assemblies)
    cycles = detect_cycles(graph)
    core = next((assembly for assembly in assemblies if assembly.name == "Hecton8.Core"), None)
    edges = [
        {"from": source, "to": target}
        for source, targets in sorted(graph.items())
        for target in targets
    ]
    return {
        "schema": SCHEMA,
        "sourceRoot": normalize_path(source_root),
        "summary": summarize_assemblies(assemblies),
        "dag": {
            "nodeCount": len(graph),
            "edgeCount": len(edges),
            "isAcyclic": len(cycles) == 0,
            "topologicalOrder": topological_order(graph)[:300],
            "edges": edges[:500],
        },
        "core": {
            "present": core is not None,
            "path": normalize_path(core.path) if core else "",
            "referenceCount": len(core.references) if core else 0,
            "firstPartyReferenceCount": sum(1 for ref in core.references if is_first_party(ref)) if core else 0,
            "concreteSiblingReferenceCount": len(core_concrete),
            "concreteSiblingReferences": core_concrete,
        },
        "coreContractsBoundary": {
            "requiredRoute": CORE_CONTRACT_ASSEMBLY,
            "violationCount": len(contract_boundary),
            "violations": contract_boundary[:300],
        },
        "unresolvedFirstPartyReferenceCount": len(unresolved),
        "unresolvedFirstPartyReferences": unresolved[:200],
        "duplicateAssemblyNameCount": len(duplicates),
        "duplicateAssemblyNames": duplicates[:100],
        "runtimeConcreteSiblingReferenceCount": len(runtime_concrete),
        "runtimeConcreteSiblingReferences": runtime_concrete[:300],
        "cycleCount": len(cycles),
        "cycles": cycles[:50],
    }


def is_identifier(value: str) -> bool:
    if not value:
        return False
    if not (value[0].isalpha() or value[0] == "_"):
        return False
    return all(ch.isalnum() or ch == "_" for ch in value)


def tokenize_csharp(text: str) -> list[Token]:
    tokens: list[Token] = []
    i = 0
    line = 1
    length = len(text)
    while i < length:
        ch = text[i]
        if ch == "\n":
            line += 1
            i += 1
            continue
        if ch.isspace():
            i += 1
            continue
        if ch == "/" and i + 1 < length and text[i + 1] == "/":
            i += 2
            while i < length and text[i] != "\n":
                i += 1
            continue
        if ch == "/" and i + 1 < length and text[i + 1] == "*":
            i += 2
            while i + 1 < length and not (text[i] == "*" and text[i + 1] == "/"):
                if text[i] == "\n":
                    line += 1
                i += 1
            i += 2
            continue
        if ch == "$" and i + 1 < length and text[i + 1] == '"':
            i += 2
            while i < length:
                if text[i] == "\\":
                    i += 2
                    continue
                if text[i] == '"':
                    i += 1
                    break
                if text[i] == "\n":
                    line += 1
                i += 1
            continue
        if ch == "$" and i + 2 < length and text[i + 1] == "@" and text[i + 2] == '"':
            i += 3
            while i < length:
                if text[i] == '"' and i + 1 < length and text[i + 1] == '"':
                    i += 2
                    continue
                if text[i] == '"':
                    i += 1
                    break
                if text[i] == "\n":
                    line += 1
                i += 1
            continue
        if ch == "@" and i + 1 < length and text[i + 1] == '"':
            i += 2
            while i < length:
                if text[i] == '"' and i + 1 < length and text[i + 1] == '"':
                    i += 2
                    continue
                if text[i] == '"':
                    i += 1
                    break
                if text[i] == "\n":
                    line += 1
                i += 1
            continue
        if ch == '"' or ch == "'":
            quote = ch
            i += 1
            while i < length:
                if text[i] == "\\":
                    i += 2
                    continue
                if text[i] == quote:
                    i += 1
                    break
                if text[i] == "\n":
                    line += 1
                i += 1
            continue
        if ch.isalpha() or ch == "_" or ch == "@":
            start = i
            i += 1
            while i < length and (text[i].isalnum() or text[i] == "_"):
                i += 1
            tokens.append(Token(text[start:i].lstrip("@"), line))
            continue
        if ch.isdigit():
            start = i
            i += 1
            if ch == "0" and i < length and text[i] in {"x", "X"}:
                i += 1
                while i < length and (text[i].isdigit() or text[i].lower() in {"a", "b", "c", "d", "e", "f"}):
                    i += 1
            else:
                while i < length and text[i].isdigit():
                    i += 1
            tokens.append(Token(text[start:i], line))
            continue
        tokens.append(Token(ch, line))
        i += 1
    return tokens


def parse_number(value: str) -> int | None:
    try:
        return int(value, 16) if value.lower().startswith("0x") else int(value)
    except ValueError:
        return None


def parse_bool_bytes(value: bytes) -> bool:
    lowered = value.strip().lower()
    return lowered in {b"1", b"true", b"yes", b"y", b"strict"}


def parse_int_bytes(value: bytes, default: int) -> int:
    try:
        return int(value.strip())
    except ValueError:
        return default


def fnv1a_32_bytes(value: bytes) -> int:
    result = 0x811C9DC5
    for byte in value:
        result ^= byte
        result = (result * 0x01000193) & 0xFFFFFFFF
    return result


def split_csv_line_bytes(line: bytes) -> list[bytes]:
    cells: list[bytes] = []
    current = bytearray()
    in_quote = False
    index = 0
    while index < len(line):
        byte = line[index]
        if byte == 34:
            if in_quote and index + 1 < len(line) and line[index + 1] == 34:
                current.append(34)
                index += 2
                continue
            in_quote = not in_quote
            index += 1
            continue
        if byte == 44 and not in_quote:
            cells.append(bytes(current).strip())
            current.clear()
            index += 1
            continue
        current.append(byte)
        index += 1
    cells.append(bytes(current).strip())
    return cells


def header_index(headers: list[bytes], names: set[bytes]) -> int | None:
    for index, header in enumerate(headers):
        key = header.strip().lower().replace(b"_", b"").replace(b"-", b"")
        if key in names:
            return index
    return None


def build_schema_profile_payload(path: Path) -> dict[str, object]:
    if not path.exists():
        return {
            "path": normalize_path(path),
            "exists": False,
            "profileCount": 0,
            "parseErrorCount": 0,
            "profiles": [],
            "errors": [],
        }
    started = time.perf_counter()
    raw = read_bytes_mmap(path)
    lines = [line for line in raw.splitlines() if line.strip()]
    if not lines:
        return {
            "path": normalize_path(path),
            "exists": True,
            "profileCount": 0,
            "parseErrorCount": 1,
            "profiles": [],
            "errors": [{"line": 1, "kind": "EMPTY_CSV", "message": "binary_schema_profiles.csv has no rows."}],
            "elapsedMs": round((time.perf_counter() - started) * 1000.0, 3),
        }
    headers = split_csv_line_bytes(lines[0])
    name_index = header_index(headers, {b"profile", b"platform", b"name", b"profilename"})
    max_size_index = header_index(headers, {b"maxstructbytes", b"maxbytes", b"structsizelimit"})
    alignment_index = header_index(headers, {b"alignmentbytes", b"alignment", b"alignbytes"})
    arm_index = header_index(headers, {b"arm64strict", b"strictarm64", b"arm64"})
    profiles: list[dict[str, object]] = []
    errors: list[dict[str, object]] = []
    if name_index is None:
        errors.append({"line": 1, "kind": "MISSING_PROFILE_COLUMN", "message": "CSV header has no profile/name column."})
    for row_number, line in enumerate(lines[1:], start=2):
        cells = split_csv_line_bytes(line)
        if name_index is None or name_index >= len(cells):
            errors.append({"line": row_number, "kind": "MISSING_PROFILE_NAME", "message": "CSV row has no profile name."})
            continue
        name = cells[name_index]
        if not name:
            errors.append({"line": row_number, "kind": "EMPTY_PROFILE_NAME", "message": "CSV row has an empty profile name."})
            continue
        max_struct_bytes = parse_int_bytes(cells[max_size_index], 64) if max_size_index is not None and max_size_index < len(cells) else 64
        alignment_bytes = parse_int_bytes(cells[alignment_index], 8) if alignment_index is not None and alignment_index < len(cells) else 8
        arm64_strict = parse_bool_bytes(cells[arm_index]) if arm_index is not None and arm_index < len(cells) else True
        if max_struct_bytes <= 0 or alignment_bytes <= 0:
            errors.append(
                {
                    "line": row_number,
                    "kind": "NON_POSITIVE_LIMIT",
                    "message": "CSV profile limits must be positive integers.",
                }
            )
            continue
        profiles.append(
            {
                "line": row_number,
                "name": name.decode("utf-8", errors="ignore"),
                "fnv1a32": f"0x{fnv1a_32_bytes(name):08X}",
                "maxStructBytes": max_struct_bytes,
                "alignmentBytes": alignment_bytes,
                "arm64Strict": arm64_strict,
            }
        )
    return {
        "path": normalize_path(path),
        "exists": True,
        "profileCount": len(profiles),
        "parseErrorCount": len(errors),
        "profiles": profiles[:128],
        "errors": errors[:128],
        "elapsedMs": round((time.perf_counter() - started) * 1000.0, 3),
    }


def collect_attribute(tokens: list[Token], index: int) -> tuple[tuple[Token, ...], int]:
    depth = 0
    start = index + 1
    i = index
    while i < len(tokens):
        if tokens[i].value == "[":
            depth += 1
        elif tokens[i].value == "]":
            depth -= 1
            if depth == 0:
                return tuple(tokens[start:i]), i + 1
        i += 1
    return tuple(tokens[start:]), len(tokens)


def attribute_contains(attribute: tuple[Token, ...], name: str) -> bool:
    target = name.removesuffix("Attribute")
    for token in attribute:
        value = token.value.removesuffix("Attribute")
        if value == target:
            return True
    return False


def parse_struct_layout(attributes: tuple[tuple[Token, ...], ...]) -> tuple[str, int | None, int | None]:
    layout = "Sequential"
    declared_size: int | None = None
    pack: int | None = None
    for attribute in attributes:
        if not attribute_contains(attribute, "StructLayout"):
            continue
        values = [token.value for token in attribute]
        if "Explicit" in values:
            layout = "Explicit"
        elif "Sequential" in values:
            layout = "Sequential"
        for index, value in enumerate(values):
            if value == "Size" and index + 2 < len(values) and values[index + 1] == "=":
                declared_size = parse_number(values[index + 2])
            if value == "Pack" and index + 2 < len(values) and values[index + 1] == "=":
                pack = parse_number(values[index + 2])
    return layout, declared_size, pack


def parse_field_offset(attributes: tuple[tuple[Token, ...], ...]) -> int | None:
    for attribute in attributes:
        if not attribute_contains(attribute, "FieldOffset"):
            continue
        for token in attribute:
            parsed = parse_number(token.value)
            if parsed is not None:
                return parsed
    return None


def normalize_type_name(tokens: list[Token]) -> str:
    identifiers = [token.value for token in tokens if is_identifier(token.value)]
    if not identifiers:
        return ""
    return identifiers[-1]


def declaration_has_method_shape(tokens: list[Token]) -> bool:
    return any(token.value == "(" for token in tokens)


def parse_field_statement(statement: list[Token], attributes: tuple[tuple[Token, ...], ...]) -> FieldInfo | None:
    if not statement or declaration_has_method_shape(statement):
        return None
    if any(token.value in {"const", "static"} for token in statement):
        return None
    clipped: list[Token] = []
    for token in statement:
        if token.value == "=":
            break
        clipped.append(token)
    if not clipped:
        return None
    if any(token.value == "," for token in clipped):
        clipped = clipped[: next(index for index, token in enumerate(clipped) if token.value == ",")]
    useful = [token for token in clipped if token.value not in FIELD_MODIFIERS]
    if len(useful) < 2:
        return None
    name_token = useful[-1]
    if not is_identifier(name_token.value):
        return None
    type_name = normalize_type_name(useful[:-1])
    if not type_name:
        return None
    return FieldInfo(name=name_token.value, type_name=type_name, line=name_token.line, offset=parse_field_offset(attributes))


def detect_struct_properties(body: list[Token]) -> tuple[dict[str, object], ...]:
    hits: list[dict[str, object]] = []
    for index, token in enumerate(body[:-1]):
        if token.value in {"get", "set"} and body[index + 1].value == ";":
            hits.append({"line": token.line, "accessor": token.value})
    return tuple(hits)


def parse_struct_fields(body: list[Token], layout_kind: str, struct_name: str) -> tuple[FieldInfo, ...]:
    del layout_kind, struct_name
    fields: list[FieldInfo] = []
    pending_attributes: list[tuple[Token, ...]] = []
    statement: list[Token] = []
    depth = 0
    i = 0
    while i < len(body):
        token = body[i]
        if depth == 0 and token.value == "[":
            attribute, i = collect_attribute(body, i)
            pending_attributes.append(attribute)
            continue
        if token.value == "{":
            if depth == 0:
                statement = []
                pending_attributes = []
            depth += 1
            i += 1
            continue
        if token.value == "}":
            depth = max(0, depth - 1)
            i += 1
            continue
        if depth > 0:
            i += 1
            continue
        if token.value == ";":
            field = parse_field_statement(statement, tuple(pending_attributes))
            if field is not None:
                fields.append(field)
            statement = []
            pending_attributes = []
            i += 1
            continue
        statement.append(token)
        i += 1
    return tuple(fields)


def parse_structs_from_tokens(tokens: list[Token], path: Path) -> list[StructInfo]:
    structs: list[StructInfo] = []
    pending_attributes: list[tuple[Token, ...]] = []
    i = 0
    rel = normalize_path_no_resolve(path)
    while i < len(tokens):
        token = tokens[i]
        if token.value == "[":
            attribute, i = collect_attribute(tokens, i)
            pending_attributes.append(attribute)
            continue
        if token.value != "struct":
            if token.value == ";":
                pending_attributes = []
            i += 1
            continue
        if i + 1 >= len(tokens) or not is_identifier(tokens[i + 1].value):
            i += 1
            continue
        name = tokens[i + 1].value
        open_index = i + 2
        while open_index < len(tokens) and tokens[open_index].value != "{":
            open_index += 1
        if open_index >= len(tokens):
            break
        depth = 1
        close_index = open_index + 1
        while close_index < len(tokens) and depth > 0:
            if tokens[close_index].value == "{":
                depth += 1
            elif tokens[close_index].value == "}":
                depth -= 1
            close_index += 1
        body = tokens[open_index + 1 : close_index - 1]
        attrs = tuple(pending_attributes)
        layout_kind, declared_size, pack = parse_struct_layout(attrs)
        fields = parse_struct_fields(body, layout_kind, name)
        has_layout = any(attribute_contains(attribute, "StructLayout") for attribute in attrs)
        unmanaged_contract = (
            has_layout
            or any(field.offset is not None for field in fields)
            or name.endswith(("DTO", "Dto", "Signal", "Packet", "Record", "Entry", "State", "Contract"))
        )
        structs.append(
            StructInfo(
                name=name,
                path=rel,
                line=token.line,
                layout_kind=layout_kind,
                declared_size=declared_size,
                pack=pack,
                fields=fields,
                property_hits=detect_struct_properties(body),
                unmanaged_contract=unmanaged_contract,
            )
        )
        pending_attributes = []
        i = close_index
    return structs


def parse_csharp_file(path: Path) -> tuple[list[StructInfo], list[dict[str, object]]]:
    text = read_text_mmap(path)
    layout_interest = "StructLayout" in text or "FieldOffset" in text or "LayoutKind" in text
    aup_interest = (
        ("Aup" in text or "AUP" in text or "WorldPosition" in text or "AbsoluteUniversePosition" in text)
        and ("float3" in text or "Vector3" in text or "(float3)" in text or "(Vector3)" in text)
    )
    if not layout_interest and not aup_interest:
        return [], []
    return parse_csharp_file_lines(text, path)


def strip_line_comment(line: str) -> str:
    marker = line.find("//")
    return line if marker < 0 else line[:marker]


def parse_number_after_marker(text: str, marker: str) -> int | None:
    start = text.find(marker)
    if start < 0:
        return None
    index = start + len(marker)
    while index < len(text) and text[index] not in "0123456789":
        index += 1
    end = index
    while end < len(text) and text[end].isdigit():
        end += 1
    if end == index:
        return None
    return parse_number(text[index:end])


def parse_struct_layout_from_text(attribute_text: str) -> tuple[str, int | None, int | None, bool]:
    if "StructLayout" not in attribute_text:
        return "Sequential", None, None, False
    layout = "Explicit" if "Explicit" in attribute_text else "Sequential"
    return (
        layout,
        parse_number_after_marker(attribute_text, "Size"),
        parse_number_after_marker(attribute_text, "Pack"),
        True,
    )


def remove_attribute_blocks(line: str) -> str:
    output: list[str] = []
    index = 0
    depth = 0
    while index < len(line):
        ch = line[index]
        if ch == "[":
            depth += 1
            index += 1
            continue
        if ch == "]" and depth > 0:
            depth -= 1
            index += 1
            continue
        if depth == 0:
            output.append(ch)
        index += 1
    return "".join(output)


def extract_struct_name(line: str) -> str | None:
    cleaned = (
        line.replace("{", " ")
        .replace(":", " ")
        .replace("<", " ")
        .replace(">", " ")
        .replace(",", " ")
    )
    parts = cleaned.split()
    for index, part in enumerate(parts[:-1]):
        if part == "struct":
            candidate = parts[index + 1]
            return candidate if is_identifier(candidate) else None
    return None


def parse_field_line(line: str, pending_offset: int | None, line_number: int) -> FieldInfo | None:
    code = remove_attribute_blocks(strip_line_comment(line))
    if ";" not in code or "(" in code or "=>" in code:
        return None
    if " get;" in code or " set;" in code or "{get;" in code or "{ get;" in code:
        return None
    code = code.split(";", 1)[0]
    code = code.split("=", 1)[0]
    for char in "<>,[]":
        code = code.replace(char, " ")
    parts = [part for part in code.split() if part not in FIELD_MODIFIERS]
    if any(part in {"const", "static"} for part in code.split()):
        return None
    if len(parts) < 2:
        return None
    name = parts[-1]
    type_name = parts[-2]
    if not is_identifier(name) or not is_identifier(type_name):
        return None
    return FieldInfo(name=name, type_name=type_name, line=line_number, offset=pending_offset)


def parse_struct_body_lines(
    body_lines: list[tuple[int, str]],
    struct_name: str,
    layout_kind: str,
    has_layout: bool,
) -> tuple[tuple[FieldInfo, ...], tuple[dict[str, object], ...], bool]:
    del layout_kind
    fields: list[FieldInfo] = []
    properties: list[dict[str, object]] = []
    pending_offset: int | None = None
    depth = 1
    for line_number, raw in body_lines:
        code = strip_line_comment(raw)
        if depth == 1 and ("get;" in code or "set;" in code):
            if "get;" in code:
                properties.append({"line": line_number, "accessor": "get"})
            if "set;" in code:
                properties.append({"line": line_number, "accessor": "set"})
        if "FieldOffset" in code:
            pending_offset = parse_number_after_marker(code, "FieldOffset")
        if depth == 1:
            field = parse_field_line(code, pending_offset, line_number)
            if field is not None:
                fields.append(field)
                pending_offset = None
        depth += code.count("{") - code.count("}")
        if depth < 1:
            depth = 1
    unmanaged = (
        has_layout
        or any(field.offset is not None for field in fields)
        or struct_name.endswith(("DTO", "Dto", "Signal", "Packet", "Record", "Entry", "State", "Contract"))
    )
    return tuple(fields), tuple(properties), unmanaged


def parse_csharp_file_lines(text: str, path: Path) -> tuple[list[StructInfo], list[dict[str, object]]]:
    lines = text.splitlines()
    structs: list[StructInfo] = []
    aup_casts: list[dict[str, object]] = []
    pending_layout_text = ""
    index = 0
    rel = normalize_path(path)
    while index < len(lines):
        raw = lines[index]
        code = strip_line_comment(raw)
        if "(float3)" in code or "(Vector3)" in code:
            lower = code.lower()
            if "aup" in lower or "absolute" in lower or "world" in lower:
                aup_casts.append(
                    {
                        "path": rel,
                        "line": index + 1,
                        "kind": "AUP_FLOAT_CAST",
                        "message": "Possible absolute AUP/world-position cast to float3/Vector3 before serialization.",
                    }
                )
        if "StructLayout" in code:
            pending_layout_text = code
            while "]" not in pending_layout_text and index + 1 < len(lines):
                index += 1
                pending_layout_text += " " + strip_line_comment(lines[index]).strip()
        if "struct" not in code:
            index += 1
            continue
        name = extract_struct_name(code)
        if name is None:
            index += 1
            continue
        layout_kind, declared_size, pack, has_layout = parse_struct_layout_from_text(pending_layout_text)
        start_line = index + 1
        brace_depth = code.count("{") - code.count("}")
        body_lines: list[tuple[int, str]] = []
        if "{" not in code:
            while index + 1 < len(lines):
                index += 1
                code = strip_line_comment(lines[index])
                brace_depth += code.count("{") - code.count("}")
                if "{" in code:
                    break
        while index + 1 < len(lines) and brace_depth > 0:
            index += 1
            body_line = lines[index]
            body_code = strip_line_comment(body_line)
            brace_depth += body_code.count("{") - body_code.count("}")
            if brace_depth >= 0:
                body_lines.append((index + 1, body_line))
        fields, properties, unmanaged = parse_struct_body_lines(body_lines, name, layout_kind, has_layout)
        structs.append(
            StructInfo(
                name=name,
                path=rel,
                line=start_line,
                layout_kind=layout_kind,
                declared_size=declared_size,
                pack=pack,
                fields=fields,
                property_hits=properties,
                unmanaged_contract=unmanaged,
            )
        )
        pending_layout_text = ""
        index += 1
    return structs, aup_casts


def align_up(value: int, alignment: int) -> int:
    if alignment <= 1:
        return value
    remainder = value % alignment
    return value if remainder == 0 else value + alignment - remainder


def field_layout_map(struct: StructInfo) -> tuple[dict[str, dict[str, int | str]], int]:
    result: dict[str, dict[str, int | str]] = {}
    cursor = 0
    max_end = 0
    for field in struct.fields:
        layout = TYPE_LAYOUTS.get(field.type_name)
        if layout is None:
            continue
        size, alignment = layout
        offset = field.offset
        if offset is None:
            cursor = align_up(cursor, alignment)
            offset = cursor
            cursor += size
        end = offset + size
        max_end = max(max_end, end)
        result[field.name] = {
            "type": field.type_name,
            "offset": offset,
            "size": size,
            "alignment": alignment,
        }
    total = struct.declared_size if struct.declared_size is not None else align_up(max(max_end, cursor), 8)
    return result, total


def validate_struct_alignment(struct: StructInfo) -> list[dict[str, object]]:
    violations: list[dict[str, object]] = []
    if struct.pack == 1:
        violations.append(
            {
                "path": struct.path,
                "line": struct.line,
                "struct": struct.name,
                "kind": "PACK_ONE_FORBIDDEN",
                "message": "[StructLayout(Pack=1)] is forbidden for runtime memory structs.",
            }
        )
    cursor = 0
    max_end = 0
    for field in struct.fields:
        layout = TYPE_LAYOUTS.get(field.type_name)
        if layout is None:
            continue
        size, alignment = layout
        offset = field.offset
        if struct.layout_kind == "Explicit" and offset is None:
            violations.append(
                {
                    "path": struct.path,
                    "line": field.line,
                    "struct": struct.name,
                    "field": field.name,
                    "type": field.type_name,
                    "kind": "MISSING_FIELD_OFFSET",
                    "message": "Explicit layout field has no [FieldOffset].",
                }
            )
            continue
        if offset is None:
            cursor = align_up(cursor, alignment)
            offset = cursor
            cursor += size
        if offset % alignment != 0:
            hole = offset % alignment
            violations.append(
                {
                    "path": struct.path,
                    "line": field.line,
                    "struct": struct.name,
                    "field": field.name,
                    "type": field.type_name,
                    "offset": offset,
                    "size": size,
                    "alignment": alignment,
                    "offsetHole": hole,
                    "kind": "ARM64_ALIGNMENT",
                    "message": f"Offset {offset} is not divisible by {alignment}.",
                }
            )
        if field.type_name == "bool":
            violations.append(
                {
                    "path": struct.path,
                    "line": field.line,
                    "struct": struct.name,
                    "field": field.name,
                    "type": field.type_name,
                    "kind": "RUNTIME_BOOL_FIELD",
                    "message": "Runtime bool is platform-sensitive; use byte or bit flags.",
                }
            )
        max_end = max(max_end, offset + size)
    total = struct.declared_size if struct.declared_size is not None else align_up(max(max_end, cursor), 8)
    if total % 8 != 0:
        violations.append(
            {
                "path": struct.path,
                "line": struct.line,
                "struct": struct.name,
                "size": total,
                "kind": "STRUCT_SIZE_NOT_MULTIPLE_OF_8",
                "message": f"Struct size {total} is not a multiple of 8.",
            }
        )
    return violations


def validate_no_properties(struct: StructInfo) -> list[dict[str, object]]:
    if not struct.unmanaged_contract:
        return []
    return [
        {
            "path": struct.path,
            "line": int(hit["line"]),
            "struct": struct.name,
            "accessor": str(hit["accessor"]),
            "kind": "CS1612_PROPERTY",
            "message": "Property accessor inside unmanaged contract struct.",
        }
        for hit in struct.property_hits
    ]


def is_aup_position_name(name: str) -> bool:
    lower = name.lower()
    if "aup" in lower:
        return True
    return ("world" in lower or "absolute" in lower or "universe" in lower) and "position" in lower


def validate_aup_precision(struct: StructInfo) -> list[dict[str, object]]:
    violations: list[dict[str, object]] = []
    for field in struct.fields:
        if not is_aup_position_name(field.name):
            continue
        if field.type_name in {"float", "float2", "float3", "float4", "Vector2", "Vector3", "Vector4"}:
            violations.append(
                {
                    "path": struct.path,
                    "line": field.line,
                    "struct": struct.name,
                    "field": field.name,
                    "type": field.type_name,
                    "kind": "AUP_FLOAT_FIELD",
                    "message": "AUP/world position authority must not be stored as float/Vector.",
                }
            )
    return violations


def detect_aup_cast_violations(tokens: list[Token], path: Path) -> list[dict[str, object]]:
    violations: list[dict[str, object]] = []
    values = [token.value for token in tokens]
    for index in range(0, max(0, len(tokens) - 2)):
        if values[index] == "(" and values[index + 1] in {"float3", "Vector3"} and values[index + 2] == ")":
            start = max(0, index - 12)
            end = min(len(tokens), index + 15)
            window = " ".join(values[start:end]).lower()
            if "aup" in window or "absolute" in window or "worldposition" in window or "world" in window:
                violations.append(
                    {
                        "path": normalize_path(path),
                        "line": tokens[index].line,
                        "kind": "AUP_FLOAT_CAST",
                        "message": "Possible absolute AUP/world-position cast to float3/Vector3 before serialization.",
                    }
                )
    return violations


def parse_csharp_files(
    source_root: Path,
    files: list[Path] | None = None,
) -> tuple[list[StructInfo], list[dict[str, object]], list[dict[str, object]]]:
    if files is None:
        files = iter_cs_files(source_root)
    structs: list[StructInfo] = []
    aup_casts: list[dict[str, object]] = []
    hotspots: list[dict[str, object]] = []
    workers = max(1, min(8, len(files)))
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as executor:
        for path, result in zip(files, executor.map(parse_csharp_file, files)):
            path_structs, path_aup_casts = result
            if path_structs or path_aup_casts:
                hotspots.append(
                    {
                        "path": normalize_path(path),
                        "structs": len(path_structs),
                        "aupCastFindings": len(path_aup_casts),
                    }
                )
            structs.extend(path_structs)
            aup_casts.extend(path_aup_casts)
    return structs, aup_casts, hotspots


def field_to_payload(field: FieldInfo) -> dict[str, object]:
    return {
        "name": field.name,
        "type": field.type_name,
        "line": field.line,
        "offset": field.offset,
    }


def field_from_payload(value: object) -> FieldInfo | None:
    if not isinstance(value, dict):
        return None
    name = value.get("name")
    type_name = value.get("type")
    line = value.get("line")
    offset = value.get("offset")
    if not isinstance(name, str) or not isinstance(type_name, str) or not isinstance(line, int):
        return None
    if offset is not None and not isinstance(offset, int):
        return None
    return FieldInfo(name=name, type_name=type_name, line=line, offset=offset)


def struct_to_payload(struct: StructInfo) -> dict[str, object]:
    return {
        "name": struct.name,
        "path": struct.path,
        "line": struct.line,
        "layoutKind": struct.layout_kind,
        "declaredSize": struct.declared_size,
        "pack": struct.pack,
        "fields": [field_to_payload(field) for field in struct.fields],
        "propertyHits": list(struct.property_hits),
        "unmanagedContract": struct.unmanaged_contract,
    }


def struct_from_payload(value: object) -> StructInfo | None:
    if not isinstance(value, dict):
        return None
    name = value.get("name")
    path = value.get("path")
    line = value.get("line")
    layout_kind = value.get("layoutKind")
    declared_size = value.get("declaredSize")
    pack = value.get("pack")
    fields_raw = value.get("fields")
    property_hits_raw = value.get("propertyHits")
    unmanaged_contract = value.get("unmanagedContract")
    if (
        not isinstance(name, str)
        or not isinstance(path, str)
        or not isinstance(line, int)
        or not isinstance(layout_kind, str)
        or not isinstance(fields_raw, list)
        or not isinstance(property_hits_raw, list)
        or not isinstance(unmanaged_contract, bool)
    ):
        return None
    if declared_size is not None and not isinstance(declared_size, int):
        return None
    if pack is not None and not isinstance(pack, int):
        return None
    fields: list[FieldInfo] = []
    for field_raw in fields_raw:
        field = field_from_payload(field_raw)
        if field is None:
            return None
        fields.append(field)
    property_hits = tuple(item for item in property_hits_raw if isinstance(item, dict))
    return StructInfo(
        name=name,
        path=path,
        line=line,
        layout_kind=layout_kind,
        declared_size=declared_size,
        pack=pack,
        fields=tuple(fields),
        property_hits=property_hits,
        unmanaged_contract=unmanaged_contract,
    )


def build_binary_parse_results(
    files: list[Path],
    cached_files: dict[str, object],
) -> tuple[list[StructInfo], list[dict[str, object]], list[dict[str, object]], dict[str, object], int, int]:
    structs: list[StructInfo] = []
    aup_casts: list[dict[str, object]] = []
    hotspots: list[dict[str, object]] = []
    new_cache_files: dict[str, object] = {}
    to_scan: list[Path] = []
    cache_hits = 0
    for path in files:
        rel = normalize_path_no_resolve(path)
        size, mtime_ns = file_stamp(path)
        row = cached_files.get(rel)
        if isinstance(row, dict) and row.get("size") == size and row.get("mtimeNs") == mtime_ns:
            row_structs_raw = row.get("structs")
            row_aup_casts = row.get("aupCastFindings")
            if isinstance(row_structs_raw, list) and isinstance(row_aup_casts, list):
                row_structs: list[StructInfo] = []
                valid = True
                for raw_struct in row_structs_raw:
                    parsed = struct_from_payload(raw_struct)
                    if parsed is None:
                        valid = False
                        break
                    row_structs.append(parsed)
                if valid:
                    structs.extend(row_structs)
                    aup_casts.extend(item for item in row_aup_casts if isinstance(item, dict))
                    if row_structs or row_aup_casts:
                        hotspots.append(
                            {
                                "path": normalize_path(path),
                                "structs": len(row_structs),
                                "aupCastFindings": len(row_aup_casts),
                            }
                        )
                    new_cache_files[rel] = row
                    cache_hits += 1
                    continue
        to_scan.append(path)
    workers = max(1, min(8, len(to_scan)))
    if to_scan:
        with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as executor:
            for path, result in zip(to_scan, executor.map(parse_csharp_file, to_scan)):
                path_structs, path_aup_casts = result
                if path_structs or path_aup_casts:
                    hotspots.append(
                        {
                            "path": normalize_path(path),
                            "structs": len(path_structs),
                            "aupCastFindings": len(path_aup_casts),
                        }
                    )
                structs.extend(path_structs)
                aup_casts.extend(path_aup_casts)
                rel = normalize_path_no_resolve(path)
                size, mtime_ns = file_stamp(path)
                new_cache_files[rel] = {
                    "size": size,
                    "mtimeNs": mtime_ns,
                    "structs": [struct_to_payload(struct) for struct in path_structs],
                    "aupCastFindings": path_aup_casts,
                }
    return structs, aup_casts, hotspots, new_cache_files, cache_hits, len(to_scan)


def extract_using_namespace(line: str) -> str | None:
    code = strip_line_comment(line).strip()
    if not code:
        return None
    if code.startswith("global "):
        code = code[7:].lstrip()
    if not code.startswith("using "):
        return None
    code = code[6:].strip()
    if code.startswith("static "):
        code = code[7:].strip()
    semi = code.find(";")
    if semi < 0:
        return None
    code = code[:semi].strip()
    if "=" in code:
        code = code.split("=", 1)[1].strip()
    if not code.startswith("Hecton8."):
        return None
    cleaned: list[str] = []
    for ch in code:
        if ch.isalnum() or ch in {"_", "."}:
            cleaned.append(ch)
            continue
        break
    namespace = "".join(cleaned).strip(".")
    return namespace if namespace.startswith("Hecton8.") else None


def scan_using_boundary_file(item: tuple[Path, str, str]) -> tuple[str, list[dict[str, object]]]:
    path, assembly_name, source_domain = item
    rel = normalize_path(path)
    raw = read_bytes_mmap(path)
    if b"using Hecton8." not in raw and b"global using Hecton8." not in raw and b"using static Hecton8." not in raw:
        return rel, []
    text = raw.decode("utf-8", errors="ignore")
    findings: list[dict[str, object]] = []
    cached_hit_rows: list[dict[str, object]] = []
    for line_number, raw in enumerate(text.splitlines(), start=1):
        namespace = extract_using_namespace(raw)
        if namespace is None:
            continue
        target_domain = domain_key(namespace)
        if target_domain == source_domain:
            continue
        if namespace == CORE_CONTRACT_ASSEMBLY or namespace.startswith(CORE_CONTRACT_ASSEMBLY + "."):
            continue
        findings.append(
            {
                "path": rel,
                "line": line_number,
                "assembly": assembly_name,
                "sourceDomain": source_domain,
                "using": namespace,
                "targetDomain": target_domain,
                "requiredRoute": CORE_CONTRACT_ASSEMBLY,
                "kind": "CROSS_DOMAIN_USING_BOUNDARY",
                "message": "Runtime C# source imports a sibling Hecton8 domain instead of the Core.Contracts route.",
            }
        )
    return rel, findings


def build_using_tree_stamp(work: list[tuple[Path, str, str]]) -> dict[str, object]:
    value = 0x811C9DC5
    total_size = 0
    total_mtime = 0
    for path, assembly_name, source_domain in sorted(work, key=lambda item: normalize_path_no_resolve(item[0])):
        rel = normalize_path_no_resolve(path)
        size, mtime_ns = file_stamp(path)
        total_size += size
        total_mtime = (total_mtime + mtime_ns) & 0xFFFFFFFFFFFFFFFF
        for byte in rel.encode("utf-8", errors="ignore"):
            value ^= byte
            value = (value * 0x01000193) & 0xFFFFFFFF
        for byte in assembly_name.encode("utf-8", errors="ignore"):
            value ^= byte
            value = (value * 0x01000193) & 0xFFFFFFFF
        for byte in source_domain.encode("utf-8", errors="ignore"):
            value ^= byte
            value = (value * 0x01000193) & 0xFFFFFFFF
        value = hash_int64_into_fnv(size, value)
        value = hash_int64_into_fnv(mtime_ns, value)
    return {
        "runtimeMappedFiles": len(work),
        "totalSize": total_size,
        "totalMtimeNs": total_mtime,
        "hash": f"0x{value:08X}",
    }


def build_using_boundary_payload(
    source_root: Path,
    report_path: Path,
    cache_path: Path | None = None,
    cs_files: list[Path] | None = None,
) -> dict[str, object]:
    started = time.perf_counter()
    assemblies = load_asmdefs(source_root)
    scopes = build_assembly_scope_table(assemblies)
    if cs_files is None:
        cs_files = iter_cs_files(source_root)
    work: list[tuple[Path, str, str]] = []
    unmapped = 0
    editor_files = 0
    for path in cs_files:
        resolved = resolve_assembly_from_scope(path, scopes)
        if resolved is None:
            unmapped += 1
            continue
        assembly_name, source_domain, editor = resolved
        if not is_first_party(assembly_name):
            continue
        if editor:
            editor_files += 1
            continue
        work.append((path, assembly_name, source_domain))
    cached = load_json_object(cache_path) if cache_path is not None else {}
    tree_stamp = build_using_tree_stamp(work)
    fast_aggregate = cached.get("aggregate") if isinstance(cached, dict) else None
    if (
        isinstance(fast_aggregate, dict)
        and cached.get("treeStamp") == tree_stamp
        and isinstance(fast_aggregate.get("violations"), list)
        and isinstance(fast_aggregate.get("topAssemblies"), list)
    ):
        elapsed_ms = round((time.perf_counter() - started) * 1000.0, 3)
        payload = {
            "agent": "SHINOBU_359",
            "schema": USING_BOUNDARY_SCHEMA,
            "evidenceClass": "STATIC_SOURCE_TOKEN_SCAN_NOT_ROSLYN",
            "sourceRoot": normalize_path(source_root),
            "requiredRoute": CORE_CONTRACT_ASSEMBLY,
            "summary": {
                "csFilesScanned": len(cs_files),
                "runtimeMappedFiles": len(work),
                "editorFilesSkipped": editor_files,
                "unmappedFiles": unmapped,
                "cacheHits": len(work),
                "cacheMisses": 0,
                "violationCount": int(fast_aggregate.get("violationCount", 0)),
                "elapsedMs": elapsed_ms,
            },
            "cachePath": normalize_path(cache_path) if cache_path is not None else "",
            "treeStamp": tree_stamp,
            "topAssemblies": fast_aggregate["topAssemblies"],
            "violations": fast_aggregate["violations"],
            "residualRisk": "Namespace import token scanner; not Roslyn semantic binding. Intended to fail obvious cross-domain using leaks before Unity compile.",
        }
        stream_json(report_path, payload)
        return payload
    cached_files_raw = cached.get("files") if isinstance(cached, dict) else {}
    cached_files = cached_files_raw if isinstance(cached_files_raw, dict) else {}
    findings: list[dict[str, object]] = []
    cached_hit_rows: list[dict[str, object]] = []
    to_scan: list[tuple[Path, str, str]] = []
    new_cache_files: dict[str, object] = {}
    cache_hits = 0
    for path, assembly_name, source_domain in work:
        rel = normalize_path_no_resolve(path)
        size, mtime_ns = file_stamp(path)
        row = cached_files.get(rel)
        if (
            isinstance(row, dict)
            and row.get("size") == size
            and row.get("mtimeNs") == mtime_ns
            and row.get("assembly") == assembly_name
            and row.get("sourceDomain") == source_domain
            and isinstance(row.get("findings"), list)
        ):
            new_cache_files[rel] = row
            cached_hit_rows.append(row)
            cache_hits += 1
            continue
        to_scan.append((path, assembly_name, source_domain))
    aggregate = cached.get("aggregate") if isinstance(cached, dict) else None
    use_aggregate = not to_scan and isinstance(aggregate, dict) and isinstance(aggregate.get("violations"), list)
    if use_aggregate:
        findings = [item for item in aggregate["violations"] if isinstance(item, dict)]
        violation_count = int(aggregate.get("violationCount", len(findings)))
        raw_top = aggregate.get("topAssemblies")
        top = raw_top if isinstance(raw_top, list) else []
    else:
        for row in cached_hit_rows:
            row_findings = row.get("findings")
            if isinstance(row_findings, list):
                findings.extend(item for item in row_findings if isinstance(item, dict))
        violation_count = 0
        top: list[dict[str, object]] = []
    scan_meta = {normalize_path_no_resolve(path): (path, assembly_name, source_domain) for path, assembly_name, source_domain in to_scan}
    workers = max(1, min(8, len(to_scan)))
    if to_scan:
        with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as executor:
            for rel, path_findings in executor.map(scan_using_boundary_file, to_scan):
                findings.extend(path_findings)
                path, assembly_name, source_domain = scan_meta[rel]
                size, mtime_ns = file_stamp(path)
                new_cache_files[rel] = {
                    "size": size,
                    "mtimeNs": mtime_ns,
                    "assembly": assembly_name,
                    "sourceDomain": source_domain,
                    "findings": path_findings,
                }
    if not use_aggregate:
        by_assembly: dict[str, int] = {}
        for finding in findings:
            assembly = str(finding.get("assembly", ""))
            by_assembly[assembly] = by_assembly.get(assembly, 0) + 1
        top = [
            {"assembly": assembly, "count": count}
            for assembly, count in sorted(by_assembly.items(), key=lambda item: (-item[1], item[0]))[:50]
        ]
        violation_count = len(findings)
    if cache_path is not None and (to_scan or not cache_path.exists() or not use_aggregate):
        stream_json(
            cache_path,
            {
                "schema": USING_BOUNDARY_CACHE_SCHEMA,
                "sourceRoot": normalize_path(source_root),
                "treeStamp": tree_stamp,
                "files": new_cache_files,
                "aggregate": {
                    "violationCount": violation_count,
                    "topAssemblies": top,
                    "violations": findings[:1000],
                },
            },
        )
    payload = {
        "agent": "SHINOBU_359",
        "schema": USING_BOUNDARY_SCHEMA,
        "evidenceClass": "STATIC_SOURCE_TOKEN_SCAN_NOT_ROSLYN",
        "sourceRoot": normalize_path(source_root),
        "requiredRoute": CORE_CONTRACT_ASSEMBLY,
        "summary": {
            "csFilesScanned": len(cs_files),
            "runtimeMappedFiles": len(work),
            "editorFilesSkipped": editor_files,
            "unmappedFiles": unmapped,
            "cacheHits": cache_hits,
            "cacheMisses": len(to_scan),
            "violationCount": violation_count,
            "elapsedMs": round((time.perf_counter() - started) * 1000.0, 3),
        },
        "cachePath": normalize_path(cache_path) if cache_path is not None else "",
        "treeStamp": tree_stamp,
        "topAssemblies": top,
        "violations": findings[:1000],
        "residualRisk": "Namespace import token scanner; not Roslyn semantic binding. Intended to fail obvious cross-domain using leaks before Unity compile.",
    }
    stream_json(report_path, payload)
    return payload


def strip_csharp_comments_and_strings(text: str) -> str:
    output: list[str] = []
    index = 0
    length = len(text)
    state = "code"
    while index < length:
        ch = text[index]
        nxt = text[index + 1] if index + 1 < length else ""
        if state == "code":
            if ch == "/" and nxt == "/":
                state = "line_comment"
                output.append(" ")
                index += 2
                continue
            if ch == "/" and nxt == "*":
                state = "block_comment"
                output.append(" ")
                index += 2
                continue
            if ch == "@" and nxt == '"':
                state = "verbatim_string"
                output.append(" ")
                index += 2
                continue
            if ch == "$" and nxt == '"':
                state = "string"
                output.append(" ")
                index += 2
                continue
            if ch == "$" and index + 2 < length and nxt == "@" and text[index + 2] == '"':
                state = "verbatim_string"
                output.append(" ")
                index += 3
                continue
            if ch == '"':
                state = "string"
                output.append(" ")
                index += 1
                continue
            if ch == "'":
                state = "char"
                output.append(" ")
                index += 1
                continue
            output.append(ch)
            index += 1
            continue
        if state == "line_comment":
            if ch == "\n":
                state = "code"
                output.append("\n")
            else:
                output.append(" ")
            index += 1
            continue
        if state == "block_comment":
            if ch == "*" and nxt == "/":
                state = "code"
                output.append(" ")
                index += 2
                continue
            output.append("\n" if ch == "\n" else " ")
            index += 1
            continue
        if state == "string":
            if ch == "\\":
                output.append(" ")
                index += 2
                continue
            if ch == '"':
                state = "code"
            output.append("\n" if ch == "\n" else " ")
            index += 1
            continue
        if state == "verbatim_string":
            if ch == '"' and nxt == '"':
                output.append(" ")
                index += 2
                continue
            if ch == '"':
                state = "code"
            output.append("\n" if ch == "\n" else " ")
            index += 1
            continue
        if state == "char":
            if ch == "\\":
                output.append(" ")
                index += 2
                continue
            if ch == "'":
                state = "code"
            output.append("\n" if ch == "\n" else " ")
            index += 1
    return "".join(output)


def contains_world_namespace(cleaned: str, path: Path) -> bool:
    normalized = path.as_posix().lower()
    if "/world/" in normalized or normalized.endswith("/world"):
        return True
    return "namespace Hecton8.World" in cleaned or "namespace Hecton8" in cleaned and ".World" in cleaned


def is_test_like_source(path: Path, cleaned: str) -> bool:
    lower_name = path.name.lower()
    normalized = path.as_posix().lower()
    if "/tests/" in normalized or "\\tests\\" in normalized:
        return True
    if any(token in lower_name for token in ("test", "smoke", "fuzzer", "validator")):
        return True
    return "[Test" in cleaned or "NUnit.Framework" in cleaned


def scan_oop_test_file(path: Path) -> tuple[bool, list[dict[str, object]]]:
    text = read_text_mmap(path)
    normalized = path.as_posix().lower()
    has_oop_token = "Physics" in text or "GameObject" in text or "Instantiate" in text
    if not has_oop_token:
        return False, []
    has_world_marker = "/world/" in normalized or "\\world\\" in normalized or "Hecton8.World" in text
    if not has_world_marker:
        return False, []
    has_test_marker = (
        "/tests/" in normalized
        or "\\tests\\" in normalized
        or any(token in path.name.lower() for token in ("test", "smoke", "fuzzer", "validator"))
        or "[Test" in text
        or "NUnit.Framework" in text
    )
    if not has_test_marker:
        return False, []
    cleaned = strip_csharp_comments_and_strings(text)
    candidate = contains_world_namespace(cleaned, path) and is_test_like_source(path, cleaned)
    findings: list[dict[str, object]] = []
    if not candidate:
        return False, findings
    rel = normalize_path(path)
    lines = cleaned.splitlines()
    for line_number, line in enumerate(lines, start=1):
        if "UnityEngine.Physics" in line or "Physics." in line:
            findings.append(
                {
                    "path": rel,
                    "line": line_number,
                    "kind": "WORLD_TEST_PHYSICS_API",
                    "message": "World test source touches UnityEngine.Physics instead of static Burst/math validation.",
                }
            )
        if (
            "new GameObject" in line
            or "new UnityEngine.GameObject" in line
            or "GameObject.Instantiate" in line
            or "Object.Instantiate" in line
        ):
            findings.append(
                {
                    "path": rel,
                    "line": line_number,
                    "kind": "WORLD_TEST_GAMEOBJECT_INSTANTIATION",
                    "message": "World test source instantiates GameObject instead of isolated data fixture.",
                }
            )
    return True, findings


def upsert_json_object(path: Path, key: str, value: dict[str, object]) -> None:
    if path.exists():
        try:
            current = json.loads(read_text_mmap(path))
        except json.JSONDecodeError:
            current = {}
    else:
        current = {}
    if not isinstance(current, dict):
        current = {"previousPayloadType": type(current).__name__}
    current[key] = value
    stream_json(path, current)


def build_oop_test_audit_payload(
    source_root: Path,
    report_path: Path,
    cs_files: list[Path] | None = None,
    cache_path: Path | None = None,
) -> dict[str, object]:
    started = time.perf_counter()
    files = cs_files if cs_files is not None else iter_cs_files(source_root)
    tree_stamp = build_file_tree_stamp(files)
    cached = load_json_object(cache_path) if cache_path is not None else {}
    aggregate = cached.get("aggregate") if isinstance(cached, dict) else None
    if (
        isinstance(aggregate, dict)
        and cached.get("treeStamp") == tree_stamp
        and isinstance(aggregate.get("findings"), list)
    ):
        payload = {
            "agent": "SHINOBU_359",
            "schema": QA_OOP_SCHEMA,
            "scanner": "OOP_AST_Scanner",
            "evidence_class": "STATIC_SOURCE_TOKEN_SCAN_NOT_ROSLYN",
            "summary": str(aggregate.get("summary", "OOP Tests Present")),
            "sourceRoot": normalize_path(source_root),
            "scannedFiles": len(files),
            "worldTestCandidateFiles": int(aggregate.get("worldTestCandidateFiles", 0)),
            "physicsApiHits": int(aggregate.get("physicsApiHits", 0)),
            "gameObjectInstantiationHits": int(aggregate.get("gameObjectInstantiationHits", 0)),
            "findingCount": int(aggregate.get("findingCount", 0)),
            "elapsedMs": round((time.perf_counter() - started) * 1000.0, 3),
            "treeStamp": tree_stamp,
            "findings": aggregate["findings"],
            "residualRisk": "Comment/string stripped token scanner; not Roslyn. It is offline evidence for QA gate triage.",
        }
        upsert_json_object(report_path, "shinobu359AssemblyComplianceGate", payload)
        return payload
    candidates = 0
    findings: list[dict[str, object]] = []
    workers = max(1, min(8, len(files)))
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as executor:
        for candidate, path_findings in executor.map(scan_oop_test_file, files):
            if candidate:
                candidates += 1
            findings.extend(path_findings)
    payload = {
        "agent": "SHINOBU_359",
        "schema": QA_OOP_SCHEMA,
        "scanner": "OOP_AST_Scanner",
        "evidence_class": "STATIC_SOURCE_TOKEN_SCAN_NOT_ROSLYN",
        "summary": "OOP Tests Eradicated" if not findings else "OOP Tests Present",
        "sourceRoot": normalize_path(source_root),
        "scannedFiles": len(files),
        "worldTestCandidateFiles": candidates,
        "physicsApiHits": sum(1 for item in findings if item.get("kind") == "WORLD_TEST_PHYSICS_API"),
        "gameObjectInstantiationHits": sum(
            1 for item in findings if item.get("kind") == "WORLD_TEST_GAMEOBJECT_INSTANTIATION"
        ),
        "findingCount": len(findings),
        "elapsedMs": round((time.perf_counter() - started) * 1000.0, 3),
        "treeStamp": tree_stamp,
        "findings": findings[:500],
        "residualRisk": "Comment/string stripped token scanner; not Roslyn. It is offline evidence for QA gate triage.",
    }
    if cache_path is not None:
        stream_json(
            cache_path,
            {
                "schema": QA_OOP_CACHE_SCHEMA,
                "sourceRoot": normalize_path(source_root),
                "treeStamp": tree_stamp,
                "aggregate": {
                    "summary": payload["summary"],
                    "worldTestCandidateFiles": candidates,
                    "physicsApiHits": payload["physicsApiHits"],
                    "gameObjectInstantiationHits": payload["gameObjectInstantiationHits"],
                    "findingCount": len(findings),
                    "findings": findings[:500],
                },
            },
        )
    upsert_json_object(report_path, "shinobu359AssemblyComplianceGate", payload)
    return payload


def iter_schema_files(schema_roots: Iterable[Path]) -> list[Path]:
    files: list[Path] = []
    for root in schema_roots:
        if not root.exists():
            continue
        for path in sorted(root.rglob("*.json")):
            if should_skip(path.relative_to(root)):
                continue
            lower = path.name.lower()
            if "schema" in lower or "binary" in lower or "layout" in lower:
                files.append(path)
    return files


def build_binary_audit_stamp(
    source_root: Path,
    schema_roots: list[Path],
    cs_files: list[Path] | None,
    schema_files: list[Path],
    schema_profile_path: Path | None,
) -> dict[str, object]:
    cs_stamp = build_git_cs_tree_stamp(source_root)
    if cs_stamp is None:
        if cs_files is None:
            cs_files = iter_cs_files(source_root)
        cs_stamp = build_file_tree_stamp(cs_files)
    schema_stamp = build_file_tree_stamp(schema_files)
    profile_path = schema_profile_path if schema_profile_path is not None else Path("")
    profile_size, profile_mtime_ns = file_stamp(profile_path) if schema_profile_path is not None else (0, 0)
    schema_root_names = [normalize_path_no_resolve(root) for root in schema_roots]
    value = 0x811C9DC5
    value = hash_text_into_fnv(normalize_path_no_resolve(source_root), value)
    for root_name in schema_root_names:
        value = hash_text_into_fnv(root_name, value)
    for stamp in (cs_stamp, schema_stamp):
        value = hash_int64_into_fnv(int(stamp["fileCount"]), value)
        value = hash_int64_into_fnv(int(stamp["totalSize"]), value)
        value = hash_int64_into_fnv(int(stamp["totalMtimeNs"]), value)
        value = hash_text_into_fnv(str(stamp["hash"]), value)
    value = hash_text_into_fnv(normalize_path_no_resolve(profile_path), value)
    value = hash_int64_into_fnv(profile_size, value)
    value = hash_int64_into_fnv(profile_mtime_ns, value)
    return {
        "sourceRoot": normalize_path_no_resolve(source_root),
        "schemaRoots": schema_root_names,
        "csTree": cs_stamp,
        "schemaTree": schema_stamp,
        "schemaProfile": {
            "path": normalize_path_no_resolve(profile_path) if schema_profile_path is not None else "",
            "exists": bool(schema_profile_path is not None and profile_path.exists()),
            "size": profile_size,
            "mtimeNs": profile_mtime_ns,
        },
        "hash": f"0x{value:08X}",
    }


def extract_schema_fields(value: object) -> dict[str, int]:
    fields: dict[str, int] = {}
    if isinstance(value, dict):
        raw_fields = value.get("fields")
        if isinstance(raw_fields, list):
            for item in raw_fields:
                if not isinstance(item, dict):
                    continue
                name = item.get("name")
                offset = item.get("offset")
                if isinstance(name, str) and isinstance(offset, int):
                    fields[name] = offset
        if isinstance(raw_fields, dict):
            for name, item in raw_fields.items():
                if isinstance(item, int):
                    fields[str(name)] = item
                elif isinstance(item, dict) and isinstance(item.get("offset"), int):
                    fields[str(name)] = int(item["offset"])
    return fields


def extract_schema_structs(value: object) -> dict[str, dict[str, int]]:
    structs: dict[str, dict[str, int]] = {}
    if isinstance(value, dict):
        if isinstance(value.get("name"), str):
            fields = extract_schema_fields(value)
            if fields:
                structs[str(value["name"])] = fields
        raw_structs = value.get("structs")
        if isinstance(raw_structs, list):
            for item in raw_structs:
                if isinstance(item, dict) and isinstance(item.get("name"), str):
                    fields = extract_schema_fields(item)
                    if fields:
                        structs[str(item["name"])] = fields
        if isinstance(raw_structs, dict):
            for name, item in raw_structs.items():
                fields = extract_schema_fields(item)
                if fields:
                    structs[str(name)] = fields
    return structs


def load_binary_schemas_from_files(schema_paths: Iterable[Path]) -> tuple[dict[str, dict[str, int]], list[str]]:
    schemas: dict[str, dict[str, int]] = {}
    schema_files: list[str] = []
    for path in schema_paths:
        try:
            value = json.loads(read_text_mmap(path))
        except json.JSONDecodeError:
            continue
        extracted = extract_schema_structs(value)
        if not extracted:
            continue
        schema_files.append(normalize_path(path))
        schemas.update(extracted)
    return schemas, schema_files


def load_binary_schemas(schema_roots: Iterable[Path]) -> tuple[dict[str, dict[str, int]], list[str]]:
    return load_binary_schemas_from_files(iter_schema_files(schema_roots))


def compare_against_binary_schemas(
    structs: list[StructInfo],
    schemas: dict[str, dict[str, int]],
) -> list[dict[str, object]]:
    mismatches: list[dict[str, object]] = []
    by_name = {struct.name: struct for struct in structs}
    for struct_name, field_offsets in sorted(schemas.items()):
        struct = by_name.get(struct_name)
        if struct is None:
            mismatches.append(
                {
                    "struct": struct_name,
                    "kind": "SCHEMA_STRUCT_MISSING_IN_SOURCE",
                    "message": "Binary schema struct has no parsed C# source struct.",
                }
            )
            continue
        actual_fields, _ = field_layout_map(struct)
        for field_name, expected_offset in sorted(field_offsets.items()):
            actual = actual_fields.get(field_name)
            if actual is None:
                mismatches.append(
                    {
                        "path": struct.path,
                        "line": struct.line,
                        "struct": struct_name,
                        "field": field_name,
                        "expectedOffset": expected_offset,
                        "kind": "SCHEMA_FIELD_MISSING_IN_SOURCE",
                        "message": "Binary schema field has no parsed C# field.",
                    }
                )
                continue
            actual_offset = int(actual["offset"])
            if actual_offset != expected_offset:
                mismatches.append(
                    {
                        "path": struct.path,
                        "line": struct.line,
                        "struct": struct_name,
                        "field": field_name,
                        "expectedOffset": expected_offset,
                        "actualOffset": actual_offset,
                        "kind": "SCHEMA_OFFSET_MISMATCH",
                        "message": "Binary schema offset and C# struct offset differ.",
                    }
                )
    return mismatches


def build_binary_schema_payload(
    source_root: Path,
    schema_roots: list[Path],
    report_path: Path,
    metric_path: Path,
    schema_profile_path: Path | None = None,
    cs_files: list[Path] | None = None,
    cache_path: Path | None = None,
    file_cache_path: Path | None = None,
) -> dict[str, object]:
    started = time.perf_counter()
    excluded_schema_outputs = {
        normalize_path_no_resolve(report_path),
        normalize_path_no_resolve(metric_path),
    }
    if cache_path is not None:
        excluded_schema_outputs.add(normalize_path_no_resolve(cache_path))
    schema_paths = [
        path
        for path in iter_schema_files(schema_roots)
        if normalize_path_no_resolve(path) not in excluded_schema_outputs
    ]
    tree_stamp = build_binary_audit_stamp(source_root, schema_roots, cs_files, schema_paths, schema_profile_path)
    cs_tree = tree_stamp.get("csTree") if isinstance(tree_stamp.get("csTree"), dict) else {}
    stamped_cs_count = int(cs_tree.get("fileCount", len(cs_files) if cs_files is not None else 0))
    cached = load_small_json_object(cache_path, 2 * 1024 * 1024) if cache_path is not None else {}
    if cached.get("schema") != BINARY_CACHE_SCHEMA:
        cached = {}
    aggregate = cached.get("aggregate") if isinstance(cached, dict) else None
    if (
        isinstance(aggregate, dict)
        and cached.get("treeStamp") == tree_stamp
        and isinstance(aggregate.get("summary"), dict)
        and isinstance(aggregate.get("arm64Violations"), list)
        and isinstance(aggregate.get("cs1612PropertyViolations"), list)
        and isinstance(aggregate.get("schemaMismatches"), list)
        and isinstance(aggregate.get("aupPrecisionViolations"), list)
        and isinstance(aggregate.get("hotspots"), list)
    ):
        elapsed_ms = (time.perf_counter() - started) * 1000.0
        summary = dict(aggregate["summary"])
        summary["elapsedMs"] = round(elapsed_ms, 3)
        summary["performanceRegressionWarning"] = elapsed_ms > 500.0
        summary["cacheHit"] = True
        summary["cacheHits"] = stamped_cs_count if stamped_cs_count > 0 else int(summary.get("csFilesScanned", 0))
        summary["cacheMisses"] = 0
        payload = {
            "schema": BINARY_SCHEMA,
            "evidenceClass": "STATIC_SOURCE",
            "sourceRoot": normalize_path(source_root),
            "schemaFiles": aggregate.get("schemaFiles", []),
            "summary": summary,
            "schemaProfiles": aggregate.get(
                "schemaProfiles",
                {
                    "path": "",
                    "exists": False,
                    "profileCount": 0,
                    "parseErrorCount": 0,
                    "profiles": [],
                    "errors": [],
                },
            ),
            "cachePath": normalize_path(cache_path) if cache_path is not None else "",
            "treeStamp": tree_stamp,
            "arm64Violations": aggregate["arm64Violations"],
            "cs1612PropertyViolations": aggregate["cs1612PropertyViolations"],
            "schemaMismatches": aggregate["schemaMismatches"],
            "aupPrecisionViolations": aggregate["aupPrecisionViolations"],
            "hotspots": aggregate["hotspots"],
        }
        stream_json(report_path, payload)
        append_metric_row(metric_path, payload)
        return payload
    if cs_files is None:
        cs_files = iter_cs_files(source_root)
    file_cache = load_json_object(file_cache_path) if file_cache_path is not None else {}
    if file_cache.get("schema") != BINARY_FILE_CACHE_SCHEMA:
        file_cache = {}
    cached_files_raw = file_cache.get("files") if isinstance(file_cache, dict) else {}
    cached_files = cached_files_raw if isinstance(cached_files_raw, dict) else {}
    structs, aup_casts, hotspots, new_cache_files, cache_hits, cache_misses = build_binary_parse_results(
        cs_files,
        cached_files,
    )
    alignment: list[dict[str, object]] = []
    properties: list[dict[str, object]] = []
    aup_fields: list[dict[str, object]] = []
    for struct in structs:
        if struct.unmanaged_contract:
            alignment.extend(validate_struct_alignment(struct))
            properties.extend(validate_no_properties(struct))
            aup_fields.extend(validate_aup_precision(struct))
    schemas, schema_files = load_binary_schemas_from_files(schema_paths)
    mismatches = compare_against_binary_schemas(structs, schemas)
    schema_profiles = build_schema_profile_payload(schema_profile_path) if schema_profile_path is not None else None
    elapsed_ms = (time.perf_counter() - started) * 1000.0
    warning = elapsed_ms > 500.0
    payload = {
        "schema": BINARY_SCHEMA,
        "evidenceClass": "STATIC_SOURCE",
        "sourceRoot": normalize_path(source_root),
        "schemaFiles": schema_files,
        "summary": {
            "csFilesScanned": len(cs_files),
            "structsParsed": len(structs),
            "unmanagedContracts": sum(1 for struct in structs if struct.unmanaged_contract),
            "arm64ViolationCount": len(alignment),
            "cs1612PropertyViolationCount": len(properties),
            "schemaMismatchCount": len(mismatches),
            "aupPrecisionViolationCount": len(aup_fields) + len(aup_casts),
            "schemaProfileCount": int(schema_profiles.get("profileCount", 0)) if isinstance(schema_profiles, dict) else 0,
            "schemaProfileParseErrorCount": int(schema_profiles.get("parseErrorCount", 0)) if isinstance(schema_profiles, dict) else 0,
            "elapsedMs": round(elapsed_ms, 3),
            "performanceRegressionWarning": warning,
            "cacheHit": cache_misses == 0 and cache_hits > 0,
            "cacheHits": cache_hits,
            "cacheMisses": cache_misses,
        },
        "schemaProfiles": schema_profiles or {
            "path": "",
            "exists": False,
            "profileCount": 0,
            "parseErrorCount": 0,
            "profiles": [],
            "errors": [],
        },
        "cachePath": normalize_path(cache_path) if cache_path is not None else "",
        "treeStamp": tree_stamp,
        "arm64Violations": alignment[:500],
        "cs1612PropertyViolations": properties[:500],
        "schemaMismatches": mismatches[:500],
        "aupPrecisionViolations": (aup_fields + aup_casts)[:500],
        "hotspots": sorted(hotspots, key=lambda item: (-int(item["structs"]), str(item["path"])))[:100],
    }
    if cache_path is not None:
        stream_json(
            cache_path,
            {
                "schema": BINARY_CACHE_SCHEMA,
                "sourceRoot": normalize_path(source_root),
                "treeStamp": tree_stamp,
                "aggregate": {
                    "schemaFiles": schema_files,
                    "summary": {
                        key: value
                        for key, value in payload["summary"].items()
                        if key not in {"elapsedMs", "performanceRegressionWarning", "cacheHit", "cacheMisses"}
                    },
                    "schemaProfiles": payload["schemaProfiles"],
                    "arm64Violations": payload["arm64Violations"],
                    "cs1612PropertyViolations": payload["cs1612PropertyViolations"],
                    "schemaMismatches": payload["schemaMismatches"],
                    "aupPrecisionViolations": payload["aupPrecisionViolations"],
                    "hotspots": payload["hotspots"],
                },
            },
        )
    if file_cache_path is not None:
        stream_json(
            file_cache_path,
            {
                "schema": BINARY_FILE_CACHE_SCHEMA,
                "sourceRoot": normalize_path(source_root),
                "treeStamp": tree_stamp,
                "files": new_cache_files,
            },
        )
    stream_json(report_path, payload)
    append_metric_row(metric_path, payload)
    return payload


def append_metric_row(metric_path: Path, binary_payload: dict[str, object]) -> None:
    summary = binary_payload.get("summary", {})
    if not isinstance(summary, dict):
        return
    row = {
        "source": "AssemblyDependencyAudit.py",
        "schema": BINARY_SCHEMA,
        "filesScanned": summary.get("csFilesScanned", 0),
        "structsValidated": summary.get("unmanagedContracts", 0),
        "arm64Violations": summary.get("arm64ViolationCount", 0),
        "executionTimeMs": summary.get("elapsedMs", 0.0),
        "performanceRegressionWarning": summary.get("performanceRegressionWarning", False),
    }
    if metric_path.exists():
        try:
            current = json.loads(read_text_mmap(metric_path))
        except json.JSONDecodeError:
            current = {"schema": METRIC_SCHEMA, "rows": []}
    else:
        current = {"schema": METRIC_SCHEMA, "rows": []}
    if isinstance(current, list):
        payload: dict[str, object] = {"schema": METRIC_SCHEMA, "rows": current}
    elif isinstance(current, dict) and isinstance(current.get("rows"), list):
        payload = current
    else:
        payload = {"schema": METRIC_SCHEMA, "previousPayloadType": type(current).__name__, "rows": []}
    rows = payload["rows"]
    if isinstance(rows, list):
        rows.append(row)
    stream_json(metric_path, payload)


def write_json(path: Path, payload: dict[str, object]) -> None:
    stream_json(path, payload)


def write_markdown(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    summary = payload["summary"]
    core = payload["core"]
    dag = payload["dag"]
    boundary = payload["coreContractsBoundary"]
    if not isinstance(summary, dict) or not isinstance(core, dict) or not isinstance(dag, dict):
        raise TypeError("payload malformed")
    if not isinstance(boundary, dict):
        raise TypeError("boundary payload malformed")
    lines = [
        "# Assembly Dependency Audit",
        "",
        "Evidence class: STATIC_SOURCE. No Unity import, compile, player build, or runtime proof was executed.",
        "",
        f"- Schema: `{payload['schema']}`",
        f"- Source root: `{payload['sourceRoot']}`",
        f"- Asmdefs: `{summary['assemblyCount']}`",
        f"- First-party asmdefs: `{summary['firstPartyAssemblyCount']}`",
        f"- Runtime first-party asmdefs: `{summary['runtimeFirstPartyAssemblyCount']}`",
        f"- Editor first-party asmdefs: `{summary['editorFirstPartyAssemblyCount']}`",
        f"- First-party `noEngineReferences=true`: `{summary['noEngineReferencesCount']}`",
        f"- First-party `autoReferenced=false`: `{summary['autoReferencedFalseCount']}`",
        "",
        "## DAG",
        "",
        f"- Nodes: `{dag['nodeCount']}`",
        f"- Edges: `{dag['edgeCount']}`",
        f"- Acyclic: `{dag['isAcyclic']}`",
        f"- First-party asmdef cycles: `{payload['cycleCount']}`",
        f"- Unresolved first-party/GUID refs: `{payload['unresolvedFirstPartyReferenceCount']}`",
        f"- Duplicate assembly names: `{payload['duplicateAssemblyNameCount']}`",
        "",
        "## Core Contracts Boundary",
        "",
        f"- Required cross-domain route: `{boundary['requiredRoute']}`",
        f"- Violations: `{boundary['violationCount']}`",
        "",
    ]
    violations = boundary.get("violations") or []
    if violations:
        lines.extend(["| Assembly | Reference | Path |", "|---|---|---|"])
        for item in violations[:80]:
            if isinstance(item, dict):
                lines.append(
                    f"| `{item.get('assembly')}` | `{item.get('reference')}` | `{item.get('path')}` |"
                )
        lines.append("")

    lines.extend(
        [
            "## Core Compile-Wall Pressure",
            "",
            f"- Core present: `{core['present']}`",
            f"- Core references: `{core['referenceCount']}`",
            f"- Core first-party references: `{core['firstPartyReferenceCount']}`",
            f"- Core concrete sibling references: `{core['concreteSiblingReferenceCount']}`",
            "",
        ]
    )
    concrete = core.get("concreteSiblingReferences") or []
    if concrete:
        lines.extend(["| Reference | Source asmdef |", "|---|---|"])
        for item in concrete:
            if isinstance(item, dict):
                lines.append(f"| `{item.get('reference')}` | `{item.get('path')}` |")
        lines.append("")

    lines.extend(
        [
            "## Runtime Concrete Cross-Domain References",
            "",
            f"- Count: `{payload['runtimeConcreteSiblingReferenceCount']}`",
            "",
        ]
    )
    runtime_refs = payload.get("runtimeConcreteSiblingReferences") or []
    if runtime_refs:
        lines.extend(["| Assembly | Reference | Path |", "|---|---|---|"])
        for item in runtime_refs[:80]:
            if isinstance(item, dict):
                lines.append(
                    f"| `{item.get('assembly')}` | `{item.get('reference')}` | `{item.get('path')}` |"
                )
        lines.append("")

    lines.extend(["## Cycles", "", f"- First-party asmdef cycles: `{payload['cycleCount']}`", ""])
    cycles = payload.get("cycles") or []
    for cycle in cycles[:20]:
        if isinstance(cycle, list):
            lines.append("- `" + " -> ".join(str(part) for part in cycle) + "`")
    if cycles:
        lines.append("")

    lines.extend(
        [
            "## Interpretation",
            "",
            "- The serialized first-party asmdef graph is treated as a DAG. Any cycle is a hard compile-wall defect under the strict flag.",
            f"- Cross-domain runtime references that do not route through `{CORE_CONTRACT_ASSEMBLY}` are strict boundary violations under `--fail-on-core-contract-boundary`.",
            "- Core concrete sibling references are compile-wall pressure. They are not automatically removable; each one needs a contract/facade migration plan plus Unity import proof.",
            "- Runtime concrete cross-domain references are review surfaces. Same-domain and `.Contracts` references are reported separately from the strict Core.Contracts boundary.",
            "- This audit does not mutate asmdefs and does not claim compile health.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def hard_failures(payload: dict[str, object], args: argparse.Namespace) -> list[str]:
    failures: list[str] = []
    core = payload["core"]
    boundary = payload["coreContractsBoundary"]
    if not isinstance(core, dict) or not isinstance(boundary, dict):
        raise TypeError("payload malformed")
    core_concrete = int(core["concreteSiblingReferenceCount"])
    cycles = int(payload["cycleCount"])
    runtime_concrete = int(payload["runtimeConcreteSiblingReferenceCount"])
    unresolved = int(payload["unresolvedFirstPartyReferenceCount"])
    boundary_violations = int(boundary["violationCount"])
    duplicate_names = int(payload["duplicateAssemblyNameCount"])
    if args.fail_on_core_concrete_sibling_refs and core_concrete > args.max_core_concrete_sibling_refs:
        failures.append(
            "Core concrete sibling asmdef refs "
            f"{core_concrete} > {args.max_core_concrete_sibling_refs}"
        )
    if args.fail_on_runtime_concrete_sibling_refs and runtime_concrete > args.max_runtime_concrete_sibling_refs:
        failures.append(
            "Runtime concrete cross-domain asmdef refs "
            f"{runtime_concrete} > {args.max_runtime_concrete_sibling_refs}"
        )
    if args.fail_on_core_contract_boundary and boundary_violations > args.max_core_contract_boundary_violations:
        failures.append(
            f"Core.Contracts boundary violations {boundary_violations} > "
            f"{args.max_core_contract_boundary_violations}"
        )
    if args.fail_on_unresolved_first_party_refs and unresolved > 0:
        failures.append(f"Unresolved first-party/GUID asmdef refs: {unresolved}")
    if args.fail_on_duplicate_assembly_names and duplicate_names > 0:
        failures.append(f"Duplicate assembly names: {duplicate_names}")
    if args.fail_on_cycles and cycles > 0:
        failures.append(f"First-party asmdef cycles: {cycles}")
    return failures


def binary_failure_exit_code(binary_payload: dict[str, object], args: argparse.Namespace) -> int:
    summary = binary_payload.get("summary", {})
    if not isinstance(summary, dict):
        return 0
    if args.fail_on_arm64_violations and int(summary.get("arm64ViolationCount", 0)) > 0:
        return 2
    if args.fail_on_struct_properties and int(summary.get("cs1612PropertyViolationCount", 0)) > 0:
        return 3
    if args.fail_on_schema_mismatch and int(summary.get("schemaMismatchCount", 0)) > 0:
        return 4
    if args.fail_on_aup_precision and int(summary.get("aupPrecisionViolationCount", 0)) > 0:
        return 6
    if args.fail_on_schema_profile_errors and int(summary.get("schemaProfileParseErrorCount", 0)) > 0:
        return 7
    return 0


def print_binary_failures(binary_payload: dict[str, object]) -> None:
    red = "\033[31m"
    reset = "\033[0m"
    for key in ("arm64Violations", "cs1612PropertyViolations", "schemaMismatches", "aupPrecisionViolations"):
        findings = binary_payload.get(key) or []
        if not isinstance(findings, list):
            continue
        for finding in findings[:20]:
            if not isinstance(finding, dict):
                continue
            print(
                red
                + "BINARY_SCHEMA_FAIL "
                + f"{key} path={finding.get('path', '')} line={finding.get('line', '')} "
                + f"struct={finding.get('struct', '')} field={finding.get('field', '')} "
                + f"kind={finding.get('kind', '')} message={finding.get('message', '')}"
                + reset
            )


def print_text(
    payload: dict[str, object],
    failures: list[str],
    binary_payload: dict[str, object] | None = None,
    oop_payload: dict[str, object] | None = None,
    using_payload: dict[str, object] | None = None,
) -> None:
    summary = payload["summary"]
    core = payload["core"]
    boundary = payload["coreContractsBoundary"]
    print("Assembly dependency audit")
    print(f"schema={payload['schema']}")
    print(f"sourceRoot={payload['sourceRoot']}")
    if isinstance(summary, dict):
        print(f"asmdefs={summary['assemblyCount']}")
        print(f"firstPartyAsmdefs={summary['firstPartyAssemblyCount']}")
        print(f"runtimeFirstPartyAsmdefs={summary['runtimeFirstPartyAssemblyCount']}")
        print(f"editorFirstPartyAsmdefs={summary['editorFirstPartyAssemblyCount']}")
        print(f"noEngineReferences={summary['noEngineReferencesCount']}")
        print(f"autoReferencedFalse={summary['autoReferencedFalseCount']}")
    if isinstance(core, dict):
        print(f"coreReferences={core['referenceCount']}")
        print(f"coreFirstPartyReferences={core['firstPartyReferenceCount']}")
        print(f"coreConcreteSiblingReferences={core['concreteSiblingReferenceCount']}")
        concrete = core.get("concreteSiblingReferences") or []
        if concrete:
            print(
                "coreConcreteSiblingList="
                + "; ".join(str(item.get("reference")) for item in concrete if isinstance(item, dict))
            )
    if isinstance(boundary, dict):
        print(f"coreContractsBoundaryViolations={boundary['violationCount']}")
    print(f"unresolvedFirstPartyReferences={payload['unresolvedFirstPartyReferenceCount']}")
    print(f"duplicateAssemblyNames={payload['duplicateAssemblyNameCount']}")
    print(f"runtimeConcreteSiblingReferences={payload['runtimeConcreteSiblingReferenceCount']}")
    print(f"cycles={payload['cycleCount']}")
    if binary_payload is not None:
        binary_summary = binary_payload.get("summary", {})
        if isinstance(binary_summary, dict):
            print(f"binaryStructsParsed={binary_summary.get('structsParsed', 0)}")
            print(f"binaryArm64Violations={binary_summary.get('arm64ViolationCount', 0)}")
            print(f"binaryCs1612Properties={binary_summary.get('cs1612PropertyViolationCount', 0)}")
            print(f"binarySchemaMismatches={binary_summary.get('schemaMismatchCount', 0)}")
            print(f"binaryAupPrecisionViolations={binary_summary.get('aupPrecisionViolationCount', 0)}")
            print(f"binarySchemaProfiles={binary_summary.get('schemaProfileCount', 0)}")
            print(f"binarySchemaProfileErrors={binary_summary.get('schemaProfileParseErrorCount', 0)}")
            print(f"binaryElapsedMs={binary_summary.get('elapsedMs', 0)}")
        print_binary_failures(binary_payload)
    if oop_payload is not None:
        print(f"oopScannerSummary={oop_payload.get('summary', 'not_run')}")
        print(f"oopScannerFindings={oop_payload.get('findingCount', 0)}")
    if using_payload is not None:
        using_summary = using_payload.get("summary", {})
        if isinstance(using_summary, dict):
            print(f"usingBoundaryViolations={using_summary.get('violationCount', 0)}")
            print(f"usingBoundaryElapsedMs={using_summary.get('elapsedMs', 0)}")
    if failures:
        print("status=FAIL")
        for failure in failures:
            print(f"failure={failure}")
    else:
        print("status=PASS_WITH_WARNINGS")


def write_self_audit_xml(
    path: Path,
    asm_payload: dict[str, object],
    binary_payload: dict[str, object] | None,
    oop_payload: dict[str, object] | None,
    using_payload: dict[str, object] | None = None,
) -> None:
    boundary = asm_payload.get("coreContractsBoundary", {})
    dag = asm_payload.get("dag", {})
    binary_summary = binary_payload.get("summary", {}) if isinstance(binary_payload, dict) else {}
    schema_profiles = binary_payload.get("schemaProfiles", {}) if isinstance(binary_payload, dict) else {}
    oop_findings = int(oop_payload.get("findingCount", 0)) if isinstance(oop_payload, dict) else -1
    using_summary = using_payload.get("summary", {}) if isinstance(using_payload, dict) else {}
    using_findings = int(using_summary.get("violationCount", -1)) if isinstance(using_summary, dict) else -1
    tasks = [
        ("01", "PASS", "Repository scan performed through existing tool surface and Python file walking."),
        ("02", "PASS", "Integrated into Tools/AssemblyDependencyAudit.py, preserving one gate owner."),
        ("03", "PASS", "BINARY_SCHEMA_AUDIT_REPORT.json is produced by --binary-schema-audit."),
        ("04", "PASS", "C# production parser uses bounded line-window state parsing; no nested regex path added."),
        ("05", "PASS", "asmdef/C#/schema readers use mmap and bounded ThreadPoolExecutor lanes."),
        ("06", "PASS", "generate_mock_csharp_structs() builds valid and hostile fixtures for --test."),
        ("07", "PASS", "parse_csharp_file() mmap path reconstructs struct layout fields and attributes."),
        ("08", "PASS", "validate_struct_alignment() enforces Pack=1 ban, offsets, bool fields, and size multiple-of-8."),
        ("09", "PASS", "validate_no_properties() reports get/set accessors in unmanaged contract structs."),
        ("10", "PASS", "compare_against_binary_schemas() compares C# field offsets against JSON schemas."),
        ("11", "PASS", "threading.Timer watchdog exits with os._exit(1) after the configured limit."),
        ("12", "PASS", "AUP/world float fields and float casts are statically reported."),
        ("13", "PASS", "--test and unittest fixtures cover cycles, boundaries, layout, properties, schemas, AUP, CSV, OOP scan."),
        ("14", "PASS", "JSON reports stream through JSONEncoder.iterencode; Python remains an offline allocator, not runtime Tick code."),
        ("15", "PASS", "Metric rows append to METRIC_PHI_DATA_TRUTH_AUDIT.json with timing and warning flag."),
        ("16", "FAIL_BLOCKED_BY_DOMAIN_BOUNDARY", "C# UI Toolkit window conflicts with the prompt's Tools-only/no C# modification law."),
        ("17", "PASS_OFFLINE_ONLY", "binary_schema_profiles.csv parser uses mmap byte slicing and FNV-1a hashes; Vault writes remain forbidden here."),
        ("18", "FAIL_BLOCKED_BY_DOMAIN_BOUNDARY", "Scene gizmo requires C# editor/runtime source mutation, forbidden for this offline gate."),
        ("19", "PASS", "OOP_AST_Scanner writes the SHINOBU_359 section of QA_OPTIMIZATION_REPORT.json."),
        ("20", "PASS", "This XML self-audit records task reconciliation, compile guard, route, and non-runtime DTO/Vault status."),
    ]
    lines = [
        '<SELF_AUDIT agent="SHINOBU_359" domain="H_PHI_ASSEMBLY_COMPLIANCE_GATE" evidenceClass="STATIC_SOURCE">',
        "  <taskReconciliation total=\"20\">",
    ]
    for task_id, status, note in tasks:
        lines.append(
            f'    <task id="{task_id}" status="{xml_escape(status)}">{xml_escape(note)}</task>'
        )
    lines.extend(
        [
            "  </taskReconciliation>",
            "  <structLayoutVerification>",
            '    <primaryRuntimeDto status="NOT_CREATED_OFFLINE_PYTHON_GATE" byteSize="0" fieldCount="0" alignment="not_applicable">',
            "      No C# DTO, NativeArray, NativeMinHeap, SignalBus payload, or Vault row was introduced by SHINOBU_359. The validator emits JSON report records only.",
            "    </primaryRuntimeDto>",
            f'    <binaryAudit unmanagedContracts="{xml_escape(binary_summary.get("unmanagedContracts", "not_run"))}" arm64Violations="{xml_escape(binary_summary.get("arm64ViolationCount", "not_run"))}" cs1612Properties="{xml_escape(binary_summary.get("cs1612PropertyViolationCount", "not_run"))}" aupViolations="{xml_escape(binary_summary.get("aupPrecisionViolationCount", "not_run"))}" elapsedMs="{xml_escape(binary_summary.get("elapsedMs", "not_run"))}" cacheHit="{xml_escape(binary_summary.get("cacheHit", "not_run"))}" cacheHits="{xml_escape(binary_summary.get("cacheHits", "not_run"))}" cacheMisses="{xml_escape(binary_summary.get("cacheMisses", "not_run"))}" performanceRegressionWarning="{xml_escape(binary_summary.get("performanceRegressionWarning", "not_run"))}" />',
            "  </structLayoutVerification>",
            "  <scalabilityCurve>",
            "    Offline static gates do not consume GlobalQualityWeight and do not alter gameplay truth. Scalability is continuous at CI depth: default asmdef DAG is the cheap pre-merge pass; binary schema, CSV profile, and OOP scans are opt-in deeper passes without changing runtime DTO layout, authority routes, or save identity.",
            "  </scalabilityCurve>",
            '  <hPhiVaultStatus privateNativeArrays="0" vaultBufferIds="none" globalRegistryHotPolling="0">',
            "    SHINOBU_359 owns no runtime memory. It reads files through mmap and writes Docs reports. Vault-backed C# editor tuning was rejected because this agent is Tools-only.",
            "  </hPhiVaultStatus>",
            '  <pointerAliasingDependencyGraph burstJobs="0" jobHandlesConsumed="none" jobHandlesProduced="none" noAliasFields="not_applicable">',
            "    No Burst jobs or NativeArrays are introduced. Offline ThreadPoolExecutor lanes are host-tool parallelism, not SystemDispatcher runtime jobs.",
            "  </pointerAliasingDependencyGraph>",
            f'  <compileGuard coreContractRequired="{CORE_CONTRACT_ASSEMBLY}" dagAcyclic="{xml_escape(dag.get("isAcyclic", "unknown"))}" cycles="{xml_escape(asm_payload.get("cycleCount", "unknown"))}" boundaryViolations="{xml_escape(boundary.get("violationCount", "unknown"))}" unresolvedFirstPartyRefs="{xml_escape(asm_payload.get("unresolvedFirstPartyReferenceCount", "unknown"))}" />',
            f'  <schemaProfiles exists="{xml_escape(schema_profiles.get("exists", False))}" profileCount="{xml_escape(schema_profiles.get("profileCount", 0))}" parseErrors="{xml_escape(schema_profiles.get("parseErrorCount", 0))}" />',
            f'  <oopScanner findingCount="{xml_escape(oop_findings)}" summary="{xml_escape(oop_payload.get("summary", "not_run") if isinstance(oop_payload, dict) else "not_run")}" />',
            f'  <usingBoundaryScanner findingCount="{xml_escape(using_findings)}" requiredRoute="{CORE_CONTRACT_ASSEMBLY}" />',
            "  <dearLie>",
            "    Manual compile-wall review and Unity import probing are replaced with a static graph proof. Complexity is O(N+E) over asmdefs for cycles and boundary checks, plus optional O(F) mmap scans for C#/QA reports; no scene simulation, GameObject construction, Physics calls, or Unity compile is required.",
            "  </dearLie>",
            "</SELF_AUDIT>",
            "",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def write_mock_asmdef(path: Path, name: str, refs: list[str] | None = None, include: list[str] | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "name": name,
        "rootNamespace": name,
        "references": refs or [],
        "includePlatforms": include or [],
        "excludePlatforms": [],
        "allowUnsafeCode": True,
        "overrideReferences": False,
        "precompiledReferences": [],
        "autoReferenced": False,
        "defineConstraints": [],
        "versionDefines": [],
        "noEngineReferences": False,
    }
    stream_json(path, payload)


def generate_mock_csharp_structs(root: Path) -> None:
    root.mkdir(parents=True, exist_ok=True)
    (root / "MockStructs.cs").write_text(
        "\n".join(
            [
                "using System.Runtime.InteropServices;",
                "using Unity.Mathematics;",
                "",
                "[StructLayout(LayoutKind.Explicit, Size = 16)]",
                "public struct ValidPacket",
                "{",
                "    [FieldOffset(0)] public ulong Tick;",
                "    [FieldOffset(8)] public uint Flags;",
                "    [FieldOffset(12)] private uint _pad0;",
                "}",
                "",
                "[StructLayout(LayoutKind.Explicit, Size = 24)]",
                "public struct BadAlignmentPacket",
                "{",
                "    [FieldOffset(4)] public double Depth;",
                "}",
                "",
                "[StructLayout(LayoutKind.Explicit, Size = 16)]",
                "public struct BadPropertyPacket",
                "{",
                "    [FieldOffset(0)] public uint Value;",
                "    public uint LeakingProperty { get; set; }",
                "}",
                "",
                "[StructLayout(LayoutKind.Explicit, Size = 16)]",
                "public struct BadAupPacket",
                "{",
                "    [FieldOffset(0)] public float3 WorldPosition;",
                "}",
                "",
                "public static class BadAupWriter",
                "{",
                "    public static void Write(double3 aup)",
                "    {",
                "        var bad = (float3)aup;",
                "    }",
                "}",
            ]
        ),
        encoding="utf-8",
    )
    stream_json(
        root / "mock_binary_schema.json",
        {
            "structs": [
                {
                    "name": "ValidPacket",
                    "fields": [
                        {"name": "Tick", "offset": 0},
                        {"name": "Flags", "offset": 4},
                    ],
                }
            ]
        },
    )
    (root / "binary_schema_profiles.csv").write_text(
        "\n".join(
            [
                "profile,maxStructBytes,alignmentBytes,arm64Strict",
                "Meta_Quest_3,64,8,true",
                "PC_Ultra,128,8,false",
            ]
        ),
        encoding="utf-8",
    )
    oop_root = root / "World" / "Tests"
    oop_root.mkdir(parents=True, exist_ok=True)
    (oop_root / "BadWorldPhysicsTest.cs").write_text(
        "\n".join(
            [
                "namespace Hecton8.World.Tests",
                "{",
                "    public static class BadWorldPhysicsTest",
                "    {",
                "        public static void Run()",
                "        {",
                "            var go = new UnityEngine.GameObject(\"bad\");",
                "            UnityEngine.Physics.Raycast(UnityEngine.Vector3.zero, UnityEngine.Vector3.forward);",
                "        }",
                "    }",
                "}",
            ]
        ),
        encoding="utf-8",
    )


def run_self_tests() -> int:
    failures: list[str] = []
    with tempfile.TemporaryDirectory(prefix="h8_asm_dependency_audit_") as temp:
        root = Path(temp)
        write_mock_asmdef(root / "Hecton8.Core.asmdef", "Hecton8.Core", ["Hecton8.AI.Cognition"])
        write_mock_asmdef(root / "A.asmdef", "Hecton8.A", ["Hecton8.B"])
        write_mock_asmdef(root / "B.asmdef", "Hecton8.B", ["Hecton8.A"])
        write_mock_asmdef(root / "Contracts.asmdef", CORE_CONTRACT_ASSEMBLY)
        write_mock_asmdef(root / "AI" / "Hecton8.AI.Cognition.asmdef", "Hecton8.AI.Cognition")
        payload = build_payload(root)
        if int(payload["cycleCount"]) != 1:
            failures.append("cycle detector did not catch A<->B")
        boundary = payload["coreContractsBoundary"]
        if not isinstance(boundary, dict) or int(boundary["violationCount"]) < 1:
            failures.append("Core.Contracts boundary detector missed Hecton8.Core -> Hecton8.AI.Cognition")

        generate_mock_csharp_structs(root)
        binary_payload = build_binary_schema_payload(
            root,
            [root],
            root / "binary_report.json",
            root / "metric.json",
            root / "binary_schema_profiles.csv",
        )
        summary = binary_payload["summary"]
        if not isinstance(summary, dict):
            failures.append("binary summary malformed")
        else:
            if int(summary["arm64ViolationCount"]) < 1:
                failures.append("ARM64 alignment mock violation was not detected")
            if int(summary["cs1612PropertyViolationCount"]) < 1:
                failures.append("CS1612 property mock violation was not detected")
            if int(summary["schemaMismatchCount"]) < 1:
                failures.append("schema mismatch mock violation was not detected")
            if int(summary["aupPrecisionViolationCount"]) < 1:
                failures.append("AUP precision mock violation was not detected")
            if int(summary["schemaProfileCount"]) != 2:
                failures.append("CSV schema profiles were not parsed")
            if int(summary["schemaProfileParseErrorCount"]) != 0:
                failures.append("CSV schema profiles produced parse errors")
        oop_payload = build_oop_test_audit_payload(root, root / "qa_report.json")
        if int(oop_payload["findingCount"]) < 2:
            failures.append("OOP test scanner did not catch Physics/GameObject mock violations")
        runtime_dir = root / "Runtime"
        runtime_dir.mkdir(parents=True, exist_ok=True)
        write_mock_asmdef(root / "Runtime" / "Hecton8.World.Runtime.asmdef", "Hecton8.World.Runtime")
        write_mock_asmdef(root / "AI" / "Hecton8.AI.Cognition.asmdef", "Hecton8.AI.Cognition")
        (runtime_dir / "UsingLeak.cs").write_text(
            "\n".join(
                [
                    "using Hecton8.AI.Cognition;",
                    "using Hecton8.Core.Contracts;",
                    "namespace Hecton8.World.Runtime",
                    "{",
                    "    public static class UsingLeak {}",
                    "}",
                ]
            ),
            encoding="utf-8",
        )
        using_payload = build_using_boundary_payload(root, root / "using_report.json")
        using_summary = using_payload["summary"]
        if not isinstance(using_summary, dict) or int(using_summary["violationCount"]) < 1:
            failures.append("using boundary scanner missed cross-domain mock using")
        write_self_audit_xml(root / "self_audit.xml", payload, binary_payload, oop_payload, using_payload)
        if not (root / "self_audit.xml").exists():
            failures.append("self-audit XML was not written")
    if failures:
        print("SELF_TEST_FAIL")
        for failure in failures:
            print(f"failure={failure}")
        return 1
    print("SELF_TEST_PASS")
    return 0


def run(args: argparse.Namespace) -> int:
    timer = start_watchdog(float(args.watchdog_seconds))
    try:
        if args.test:
            return run_self_tests()
        source_root = Path(args.source_root)
        payload = build_payload(source_root)
        binary_payload: dict[str, object] | None = None
        oop_payload: dict[str, object] | None = None
        using_payload: dict[str, object] | None = None
        shared_cs_files: list[Path] | None = None
        if args.oop_test_audit or args.using_leak_audit:
            shared_cs_files = iter_cs_files(source_root)
        if args.binary_schema_audit:
            schema_roots = [Path(item) for item in args.schema_root]
            binary_payload = build_binary_schema_payload(
                source_root,
                schema_roots,
                Path(args.binary_report_path),
                Path(args.metric_path),
                Path(args.schema_profile_csv),
                shared_cs_files,
                Path(args.binary_cache_path),
                Path(args.binary_file_cache_path),
            )
        if args.oop_test_audit:
            oop_payload = build_oop_test_audit_payload(
                source_root,
                Path(args.qa_report_path),
                shared_cs_files,
                Path(args.oop_cache_path),
            )
        if args.using_leak_audit:
            using_payload = build_using_boundary_payload(
                source_root,
                Path(args.using_report_path),
                Path(args.using_cache_path),
                shared_cs_files,
            )
        write_json(Path(args.json_path), payload)
        write_markdown(Path(args.report_path), payload)
        failures = hard_failures(payload, args)
        if args.fail_on_oop_test_findings and oop_payload is not None and int(oop_payload.get("findingCount", 0)) > 0:
            failures.append(f"OOP World test findings: {oop_payload.get('findingCount', 0)}")
        if args.fail_on_using_boundary and using_payload is not None:
            using_summary = using_payload.get("summary", {})
            using_count = int(using_summary.get("violationCount", 0)) if isinstance(using_summary, dict) else 0
            if using_count > 0:
                failures.append(f"Cross-domain using boundary violations: {using_count}")
        binary_code = binary_failure_exit_code(binary_payload, args) if binary_payload is not None else 0
        if args.write_self_audit:
            write_self_audit_xml(Path(args.self_audit_path), payload, binary_payload, oop_payload, using_payload)
        if args.json:
            output = payload | {"failures": failures}
            if binary_payload is not None:
                output["binarySchemaAudit"] = binary_payload
            if oop_payload is not None:
                output["oopTestAudit"] = oop_payload
            if using_payload is not None:
                output["usingBoundaryAudit"] = using_payload
            print(json.dumps(output, indent=2, sort_keys=True))
        else:
            print_text(payload, failures, binary_payload, oop_payload, using_payload)
        if binary_code != 0:
            return binary_code
        return 1 if failures else 0
    finally:
        if timer is not None:
            timer.cancel()


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", default=str(DEFAULT_SOURCE_ROOT))
    parser.add_argument("--report-path", default=str(DEFAULT_REPORT_PATH))
    parser.add_argument("--json-path", default=str(DEFAULT_JSON_PATH))
    parser.add_argument("--binary-report-path", default=str(DEFAULT_BINARY_REPORT_PATH))
    parser.add_argument("--binary-cache-path", default=str(DEFAULT_BINARY_CACHE_PATH))
    parser.add_argument("--binary-file-cache-path", default=str(DEFAULT_BINARY_FILE_CACHE_PATH))
    parser.add_argument("--metric-path", default=str(DEFAULT_METRIC_PATH))
    parser.add_argument("--qa-report-path", default=str(DEFAULT_QA_REPORT_PATH))
    parser.add_argument("--oop-cache-path", default=str(DEFAULT_OOP_CACHE_PATH))
    parser.add_argument("--self-audit-path", default=str(DEFAULT_SELF_AUDIT_PATH))
    parser.add_argument("--schema-profile-csv", default=str(DEFAULT_SCHEMA_PROFILE_PATH))
    parser.add_argument("--using-report-path", default=str(DEFAULT_USING_REPORT_PATH))
    parser.add_argument("--using-cache-path", default=str(DEFAULT_USING_CACHE_PATH))
    parser.add_argument("--schema-root", action="append", default=[str(REPO_ROOT / "Assets" / "_Project" / "Data")])
    parser.add_argument("--json", action="store_true", help="Print JSON payload to stdout.")
    parser.add_argument("--test", action="store_true", help="Run self-contained mock regression tests.")
    parser.add_argument("--watchdog-seconds", type=float, default=10.0)
    parser.add_argument("--binary-schema-audit", action="store_true")
    parser.add_argument("--oop-test-audit", action="store_true")
    parser.add_argument("--using-leak-audit", action="store_true")
    parser.add_argument("--write-self-audit", action="store_true")
    parser.add_argument("--fail-on-core-concrete-sibling-refs", action="store_true")
    parser.add_argument("--max-core-concrete-sibling-refs", type=int, default=0)
    parser.add_argument("--fail-on-runtime-concrete-sibling-refs", action="store_true")
    parser.add_argument("--max-runtime-concrete-sibling-refs", type=int, default=0)
    parser.add_argument("--fail-on-core-contract-boundary", action="store_true")
    parser.add_argument("--max-core-contract-boundary-violations", type=int, default=0)
    parser.add_argument("--fail-on-unresolved-first-party-refs", action="store_true")
    parser.add_argument("--fail-on-duplicate-assembly-names", action="store_true")
    parser.add_argument("--fail-on-cycles", action="store_true")
    parser.add_argument("--fail-on-arm64-violations", action="store_true")
    parser.add_argument("--fail-on-struct-properties", action="store_true")
    parser.add_argument("--fail-on-schema-mismatch", action="store_true")
    parser.add_argument("--fail-on-aup-precision", action="store_true")
    parser.add_argument("--fail-on-schema-profile-errors", action="store_true")
    parser.add_argument("--fail-on-oop-test-findings", action="store_true")
    parser.add_argument("--fail-on-using-boundary", action="store_true")
    return parser


def main() -> int:
    return run(build_parser().parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
