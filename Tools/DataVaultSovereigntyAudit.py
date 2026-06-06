#!/usr/bin/env python3
"""Audit direct NativeArray constructors against DataVault sovereignty."""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Container, Iterable, Sequence


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_BASELINE_PATH = (
    REPO_ROOT
    / "Docs"
    / "AgentLogs"
    / "DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json"
)
DEFAULT_REPORT_PATH = (
    REPO_ROOT
    / "Docs"
    / "AgentLogs"
    / "DataVaultSovereigntyAudit_VAULT_SOVEREIGNTY_ENFORCER.md"
)
AUDIT_SCHEMA = "hecton8.datavault_sovereignty_audit.v4"
BASELINE_SCHEMA = "hecton8.datavault_sovereignty_baseline.v4"
REPORT_SCHEMA = "hecton8.datavault_sovereignty_audit_report.v3"
NATIVE_ARRAY_CONSTRUCTOR_RE = re.compile(r"\bnew\s+NativeArray\s*<")
NATIVE_ARRAY_ALLOCATOR_RE = re.compile(r"\bAllocator\s*\.\s*(?P<allocator>Persistent|TempJob|Temp)\b")
LATEST_CREATED_FALLBACK_RE = re.compile(r"\bGlobalDataVault\s*\.\s*TryGetLatestCreated\s*\(")
NATIVE_COLLECTION_DECLARATION_RE = re.compile(
    r"^\s*(?:\[[^\]]+\]\s*)*"
    r"(?:(?:public|private|protected|internal|static|readonly|volatile|unsafe|new)\s+)+"
    r"(?P<collection>NativeArray|NativeList|Native(?:Parallel)?HashMap)\s*<[^>;]+>\s+"
    r"(?P<name>[A-Za-z_]\w*)\s*(?:;|,|=\s*(?!>))"
)
NATIVE_ARRAY_DECLARATION_RE = NATIVE_COLLECTION_DECLARATION_RE
TYPE_DECLARATION_RE = re.compile(
    r"\b(?P<kind>class|struct|interface|record\s+struct|record\s+class|record)\s+"
    r"(?P<name>[A-Za-z_]\w*)"
    r"(?:\s*:\s*(?P<bases>[^{]+))?"
)
JOB_INTERFACE_TOKENS = (
    "IJob",
    "IJobFor",
    "IJobParallelFor",
    "IJobParallelForTransform",
    "IJobEntity",
    "IJobChunk",
)
NATIVE_COLLECTION_TOKENS = (
    "NativeArray",
    "NativeList",
    "NativeHashMap",
    "NativeParallelHashMap",
)
NATIVE_VIEW_STRUCT_SUFFIXES = (
    "Buffer",
    "Buffers",
    "BufferView",
    "BufferViews",
    "View",
    "Views",
    "Payload",
    "Snapshot",
    "Kernel",
)
DEFAULT_ALLOWED_PATH_SUFFIXES = (
    "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
)
DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES = (
    "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
    "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs",
)
DEFAULT_ALLOWED_LATEST_CREATED_PATH_SUFFIXES = (
    "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
    "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs",
)
LATEST_CREATED_ALLOWED_PATH_TOKENS = (
    "/bootstrap/",
    "/diagnostics/",
    "/gizmo",
    "/xray",
    "/validator",
    "/smoketester",
    "/fuzzer",
    "/crash",
    "/fault",
    "/dump",
    "/watchdog",
)
SKIP_DIR_NAMES = {
    ".git",
    ".vs",
    "__pycache__",
    "bin",
    "obj",
    "Library",
    "Temp",
}
RUNTIME_SURFACE = "Runtime"
EDITOR_OFFLINE_SURFACES = frozenset({"Editor", "Test", "Dev", "OfflineBake"})
OFFLINE_BAKE_PATH_TOKENS = (
    "offline",
    "baker",
    "bakepipeline",
    "forge",
    "geographysanity",
    "topographysanity",
    "topographyforge",
    "seambinder",
    "importer",
    "exporter",
)


@dataclass(frozen=True)
class FileFinding:
    path: str
    count: int
    lines: tuple[int, ...]
    allowed: bool
    allocator_counts: tuple[tuple[str, int], ...] = ()
    allocator_kinds: tuple[str, ...] = ()
    execution_surface: str = ""
    execution_surfaces: tuple[str, ...] = ()


@dataclass(frozen=True)
class DeclarationFinding:
    path: str
    count: int
    lines: tuple[int, ...]
    allowed: bool
    classification: str
    collection_type: str
    names: tuple[str, ...]
    owner_type: str
    owner_kind: str
    owner_line: int
    burst_owner: bool


@dataclass(frozen=True)
class LatestCreatedFallbackFinding:
    path: str
    count: int
    lines: tuple[int, ...]
    allowed: bool
    execution_surface: str
    execution_surfaces: tuple[str, ...]


@dataclass(frozen=True)
class TypeScope:
    name: str
    kind: str
    bases: str
    body_depth: int
    line: int
    burst: bool


def normalize_path(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        relative = path.resolve().relative_to(repo_root.resolve())
    except ValueError:
        relative = path

    return relative.as_posix()


def is_allowed_path(relative_path: str, allowed_suffixes: Sequence[str]) -> bool:
    normalized = relative_path.replace("\\", "/")
    for suffix in allowed_suffixes:
        if normalized.endswith(suffix.replace("\\", "/")):
            return True

    return False


def count_values(values: Iterable[str]) -> dict[str, int]:
    result: dict[str, int] = {}
    for value in values:
        result[value] = result.get(value, 0) + 1

    return result


def count_allocator_values_by_surface(
    execution_surfaces: Sequence[str],
    allocator_kinds: Sequence[str],
) -> dict[str, dict[str, int]]:
    result: dict[str, dict[str, int]] = {}
    count = min(len(execution_surfaces), len(allocator_kinds))
    for index in range(count):
        surface = execution_surfaces[index]
        allocator = allocator_kinds[index]
        surface_counts = result.setdefault(surface, {})
        surface_counts[allocator] = surface_counts.get(allocator, 0) + 1

    return {surface: dict(sorted(counts.items())) for surface, counts in sorted(result.items())}


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.parts)


def contains_native_collection_token(source: str) -> bool:
    return any(token in source for token in NATIVE_COLLECTION_TOKENS)


def contains_native_collection_declaration_candidate(lines: Sequence[str]) -> bool:
    return any(NATIVE_COLLECTION_DECLARATION_RE.search(line) for line in lines)


def contains_latest_created_fallback_token(source: str) -> bool:
    return "TryGetLatestCreated" in source


def scan_source_tree(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_PATH_SUFFIXES,
) -> list[FileFinding]:
    if not source_root.exists():
        raise FileNotFoundError(f"source root not found: {source_root}")

    findings: list[FileFinding] = []
    for path in sorted(source_root.rglob("*.cs")):
        relative_scan_path = path.relative_to(source_root)
        if should_skip(relative_scan_path):
            continue

        line_numbers: list[int] = []
        allocator_kinds: list[str] = []
        try:
            source = path.read_text(encoding="utf-8", errors="ignore")
        except OSError as exc:
            raise OSError(f"failed to read {path}") from exc
        if "NativeArray" not in source:
            continue

        sanitized_lines = sanitize_csharp_source(source).splitlines()
        relative_path = normalize_path(path, repo_root)

        for line_index, line in enumerate(sanitized_lines):
            if NATIVE_ARRAY_CONSTRUCTOR_RE.search(line):
                line_number = line_index + 1
                line_numbers.append(line_number)
                allocator_kinds.append(
                    classify_constructor_allocator_at_line(sanitized_lines, line_index, source)
                )

        if not line_numbers:
            continue

        line_execution_surfaces = extract_line_execution_surfaces_for_source(relative_path, source)
        constructor_surfaces = tuple(
            resolve_line_execution_surface(line_execution_surfaces, line_number)
            for line_number in line_numbers
        )
        execution_surface = summarize_execution_surfaces(constructor_surfaces)
        findings.append(
            FileFinding(
                path=relative_path,
                count=len(line_numbers),
                lines=tuple(line_numbers),
                allowed=is_allowed_path(relative_path, allowed_suffixes),
                allocator_counts=tuple(sorted(count_values(allocator_kinds).items())),
                allocator_kinds=tuple(allocator_kinds),
                execution_surface=execution_surface,
                execution_surfaces=constructor_surfaces,
            )
        )

    return findings


def strip_line_comment(line: str) -> str:
    return line.split("//", 1)[0]


def sanitize_csharp_source(source: str) -> str:
    result: list[str] = []
    index = 0
    in_block_comment = False
    in_string = False
    in_char = False
    in_verbatim = False
    while index < len(source):
        char = source[index]
        nxt = source[index + 1] if index + 1 < len(source) else ""

        if in_block_comment:
            if char == "*" and nxt == "/":
                result.extend((" ", " "))
                index += 2
                in_block_comment = False
                continue
            result.append("\n" if char == "\n" else " ")
            index += 1
            continue

        if in_string:
            if in_verbatim:
                if char == '"' and nxt == '"':
                    result.extend((" ", " "))
                    index += 2
                    continue
                if char == '"':
                    result.append(" ")
                    index += 1
                    in_string = False
                    in_verbatim = False
                    continue
                result.append("\n" if char == "\n" else " ")
                index += 1
                continue
            if char == "\\" and nxt:
                result.extend((" ", " "))
                index += 2
                continue
            if char == '"':
                result.append(" ")
                index += 1
                in_string = False
                continue
            result.append("\n" if char == "\n" else " ")
            index += 1
            continue

        if in_char:
            if char == "\\" and nxt:
                result.extend((" ", " "))
                index += 2
                continue
            if char == "'":
                result.append(" ")
                index += 1
                in_char = False
                continue
            result.append("\n" if char == "\n" else " ")
            index += 1
            continue

        if char == "/" and nxt == "/":
            result.extend((" ", " "))
            index += 2
            while index < len(source) and source[index] != "\n":
                result.append(" ")
                index += 1
            continue
        if char == "/" and nxt == "*":
            result.extend((" ", " "))
            index += 2
            in_block_comment = True
            continue
        if char == "@" and nxt == '"':
            result.extend((" ", " "))
            index += 2
            in_string = True
            in_verbatim = True
            continue
        if char == '"':
            result.append(" ")
            index += 1
            in_string = True
            in_verbatim = False
            continue
        if char == "'":
            result.append(" ")
            index += 1
            in_char = True
            continue

        result.append(char)
        index += 1

    return "".join(result)


def source_is_file_scoped_unity_editor_guard(source: str) -> bool:
    meaningful_lines = [
        line.strip().lstrip("\ufeff")
        for line in source.splitlines()
        if line.strip()
    ]
    return (
        len(meaningful_lines) >= 2
        and meaningful_lines[0] == "#if UNITY_EDITOR"
        and meaningful_lines[-1] == "#endif"
    )


def extract_execution_surface_for_source(relative_path: str, source: str) -> str:
    if source_is_file_scoped_unity_editor_guard(source):
        return "Editor"

    return extract_execution_surface(relative_path)


def preprocessor_condition_surface(condition: str, parent_surface: str, default_surface: str) -> str:
    normalized = condition.replace("(", " ").replace(")", " ").strip()
    if "!UNITY_EDITOR" in normalized:
        return default_surface
    if "DEVELOPMENT_BUILD" in normalized:
        return "Dev"
    if "UNITY_EDITOR" in normalized:
        return "Editor"

    return parent_surface


def extract_line_execution_surfaces_for_source(relative_path: str, source: str) -> list[str]:
    default_surface = extract_execution_surface_for_source(relative_path, source)
    raw_lines = source.splitlines()
    surfaces: list[str] = []
    stack: list[tuple[str, str, str, str]] = []
    active_surface = default_surface

    for raw_line in raw_lines:
        stripped = raw_line.strip().lstrip("\ufeff")
        if stripped.startswith("#if "):
            parent_surface = active_surface
            condition = stripped[4:].strip()
            if_surface = preprocessor_condition_surface(condition, parent_surface, default_surface)
            else_surface = default_surface if if_surface != parent_surface else parent_surface
            stack.append((parent_surface, if_surface, else_surface, if_surface))
            active_surface = if_surface
            surfaces.append(active_surface)
            continue
        if stripped.startswith("#elif "):
            if stack:
                parent_surface, _, else_surface, _ = stack[-1]
                condition = stripped[6:].strip()
                active_surface = preprocessor_condition_surface(condition, parent_surface, default_surface)
                stack[-1] = (parent_surface, active_surface, else_surface, active_surface)
            surfaces.append(active_surface)
            continue
        if stripped == "#else":
            if stack:
                parent_surface, if_surface, else_surface, _ = stack[-1]
                active_surface = else_surface
                stack[-1] = (parent_surface, if_surface, else_surface, active_surface)
            surfaces.append(active_surface)
            continue
        if stripped == "#endif":
            if stack:
                parent_surface, _, _, _ = stack.pop()
                active_surface = stack[-1][3] if stack else parent_surface
            surfaces.append(active_surface)
            continue

        surfaces.append(active_surface)

    return surfaces


def resolve_line_execution_surface(line_execution_surfaces: Sequence[str], line_number: int) -> str:
    index = line_number - 1
    if index < 0 or index >= len(line_execution_surfaces):
        return RUNTIME_SURFACE

    return line_execution_surfaces[index]


def summarize_execution_surfaces(execution_surfaces: Sequence[str]) -> str:
    if not execution_surfaces:
        return RUNTIME_SURFACE

    first_surface = execution_surfaces[0]
    for surface in execution_surfaces:
        if surface != first_surface:
            return "Mixed"

    return first_surface


def is_job_scope(scope: TypeScope | None) -> bool:
    if scope is None:
        return False
    if "struct" not in scope.kind:
        return False
    return any(token in scope.bases for token in JOB_INTERFACE_TOKENS)


def is_native_view_scope(scope: TypeScope | None) -> bool:
    if scope is None:
        return False
    if "struct" not in scope.kind:
        return False
    return scope.name.endswith(NATIVE_VIEW_STRUCT_SUFFIXES)


def classify_constructor_allocator(line: str) -> str:
    match = NATIVE_ARRAY_ALLOCATOR_RE.search(line)
    return match.group("allocator") if match else "Unknown"


def constructor_window(lines: Sequence[str], line_index: int, max_lines: int = 8) -> str:
    return "\n".join(lines[line_index : min(len(lines), line_index + max_lines)])


def is_sentinel_tracked_constructor_wrapper(source: str, lines: Sequence[str], line_index: int) -> bool:
    if "NativeMemorySentinel.RegisterNativeArray" not in source:
        return False
    if "NativeMemorySentinel.UnregisterNativeArray" not in source:
        return False

    lookback = "\n".join(lines[max(0, line_index - 12) : line_index + 1])
    lookahead = "\n".join(lines[line_index : min(len(lines), line_index + 8)])
    return "NewTrackedArray" in lookback and "RegisterNativeArray" in lookahead


def classify_constructor_allocator_at_line(
    lines: Sequence[str],
    line_index: int,
    source: str,
) -> str:
    if is_sentinel_tracked_constructor_wrapper(source, lines, line_index):
        return "SentinelTracked"

    return classify_constructor_allocator(constructor_window(lines, line_index))


def constructor_is_gate_relevant(execution_surface: str, allocator: str) -> bool:
    if execution_surface in EDITOR_OFFLINE_SURFACES and allocator in {"Temp", "TempJob"}:
        return False
    if execution_surface in EDITOR_OFFLINE_SURFACES and allocator == "SentinelTracked":
        return False

    return True


def latest_created_fallback_is_allowed(
    relative_path: str,
    execution_surface: str,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_LATEST_CREATED_PATH_SUFFIXES,
) -> bool:
    if is_allowed_path(relative_path, allowed_suffixes):
        return True
    if execution_surface in EDITOR_OFFLINE_SURFACES:
        return True

    normalized = "/" + relative_path.replace("\\", "/").lower()
    return any(token in normalized for token in LATEST_CREATED_ALLOWED_PATH_TOKENS)


def scan_latest_created_fallbacks_in_source(
    source: str,
    relative_path: str,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_LATEST_CREATED_PATH_SUFFIXES,
    sanitized_lines: Sequence[str] | None = None,
) -> list[LatestCreatedFallbackFinding]:
    if sanitized_lines is None:
        sanitized_lines = sanitize_csharp_source(source).splitlines()

    lines: list[int] = []
    for line_index, line in enumerate(sanitized_lines):
        if LATEST_CREATED_FALLBACK_RE.search(line):
            lines.append(line_index + 1)

    if not lines:
        return []

    line_execution_surfaces = extract_line_execution_surfaces_for_source(relative_path, source)
    surfaces = tuple(
        resolve_line_execution_surface(line_execution_surfaces, line_number)
        for line_number in lines
    )
    allowed = all(
        latest_created_fallback_is_allowed(relative_path, surface, allowed_suffixes)
        for surface in surfaces
    )
    return [
        LatestCreatedFallbackFinding(
            path=relative_path,
            count=len(lines),
            lines=tuple(lines),
            allowed=allowed,
            execution_surface=summarize_execution_surfaces(surfaces),
            execution_surfaces=surfaces,
        )
    ]


def latest_created_fallback_finding_to_dict(
    finding: LatestCreatedFallbackFinding,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_LATEST_CREATED_PATH_SUFFIXES,
) -> dict[str, Any]:
    forbidden_lines: list[int] = []
    forbidden_surfaces: list[str] = []
    allowed_lines: list[int] = []
    allowed_surfaces: list[str] = []

    for index, line_number in enumerate(finding.lines):
        surface = finding.execution_surfaces[index] if index < len(finding.execution_surfaces) else RUNTIME_SURFACE
        if latest_created_fallback_is_allowed(finding.path, surface, allowed_suffixes):
            allowed_lines.append(line_number)
            allowed_surfaces.append(surface)
        else:
            forbidden_lines.append(line_number)
            forbidden_surfaces.append(surface)

    return {
        "path": finding.path,
        "count": finding.count,
        "lines": list(finding.lines),
        "allowed": len(forbidden_lines) == 0,
        "executionSurface": finding.execution_surface,
        "lineExecutionSurfaces": list(finding.execution_surfaces),
        "lineExecutionSurfaceCounts": count_values(finding.execution_surfaces),
        "domain": extract_domain(finding.path),
        "forbiddenCount": len(forbidden_lines),
        "forbiddenLines": forbidden_lines,
        "forbiddenLineExecutionSurfaces": forbidden_surfaces,
        "forbiddenLineExecutionSurfaceCounts": count_values(forbidden_surfaces),
        "allowedCount": len(allowed_lines),
        "allowedLines": allowed_lines,
        "allowedLineExecutionSurfaces": allowed_surfaces,
        "allowedLineExecutionSurfaceCounts": count_values(allowed_surfaces),
    }


def constructor_finding_to_dict(finding: FileFinding) -> dict[str, Any]:
    line_numbers = list(finding.lines)
    allocator_kinds = list(finding.allocator_kinds)
    line_execution_surfaces = list(finding.execution_surfaces)
    if len(line_execution_surfaces) != len(line_numbers):
        fallback_surface = finding.execution_surface or extract_execution_surface(finding.path)
        line_execution_surfaces = [fallback_surface for _ in line_numbers]
    execution_surface = finding.execution_surface or summarize_execution_surfaces(line_execution_surfaces)
    forbidden_lines: list[int] = []
    forbidden_allocators: list[str] = []
    forbidden_surfaces: list[str] = []
    transient_lines: list[int] = []
    transient_allocators: list[str] = []
    transient_surfaces: list[str] = []

    for index, line_number in enumerate(line_numbers):
        allocator = allocator_kinds[index] if index < len(allocator_kinds) else "Unknown"
        line_surface = line_execution_surfaces[index]
        if finding.allowed:
            continue
        if constructor_is_gate_relevant(line_surface, allocator):
            forbidden_lines.append(line_number)
            forbidden_allocators.append(allocator)
            forbidden_surfaces.append(line_surface)
        else:
            transient_lines.append(line_number)
            transient_allocators.append(allocator)
            transient_surfaces.append(line_surface)

    forbidden_count = len(forbidden_lines)
    return {
        "path": finding.path,
        "count": finding.count,
        "lines": line_numbers,
        "allowed": finding.allowed or forbidden_count == 0,
        "pathAllowed": finding.allowed,
        "executionSurface": execution_surface,
        "lineExecutionSurfaces": line_execution_surfaces,
        "lineExecutionSurfaceCounts": count_values(line_execution_surfaces),
        "domain": extract_domain(finding.path),
        "allocatorCounts": dict(finding.allocator_counts),
        "allocatorKinds": allocator_kinds,
        "forbiddenCount": forbidden_count,
        "forbiddenLines": forbidden_lines,
        "forbiddenLineExecutionSurfaces": forbidden_surfaces,
        "forbiddenLineExecutionSurfaceCounts": count_values(forbidden_surfaces),
        "forbiddenAllocatorCounts": count_values(forbidden_allocators),
        "forbiddenAllocatorCountsByExecutionSurface": count_allocator_values_by_surface(
            forbidden_surfaces,
            forbidden_allocators,
        ),
        "transientEditorScratchCount": len(transient_lines),
        "transientEditorScratchLines": transient_lines,
        "transientEditorScratchExecutionSurfaces": transient_surfaces,
        "transientEditorScratchExecutionSurfaceCounts": count_values(transient_surfaces),
        "transientEditorScratchAllocatorCounts": count_values(transient_allocators),
    }


def is_editor_offline_surface(relative_path: str) -> bool:
    return extract_execution_surface(relative_path) in EDITOR_OFFLINE_SURFACES


def classify_declaration_scope(scope: TypeScope | None, relative_path: str = "") -> str:
    if scope is None:
        return "unknownOwnerField"
    if is_job_scope(scope):
        return "burstJobInput" if scope.burst else "jobInputStruct"
    if is_native_view_scope(scope):
        return "nativeViewStruct"
    if is_editor_offline_surface(relative_path) and (scope.kind == "class" or scope.kind == "record class"):
        lowered_name = scope.name.lower()
        if "bakesession" in lowered_name or lowered_name.endswith("session"):
            return "editorOfflineSessionScratchField"
        if "previewstore" in lowered_name or "previewcache" in lowered_name:
            return "editorOfflinePersistentPreviewField"
    if scope.kind == "class" or scope.kind == "record class":
        return "persistentOwnerField"
    if "struct" in scope.kind:
        return "unknownStructField"
    return "unknownOwnerField"


def declaration_is_gate_relevant(classification: str) -> bool:
    return classification in {
        "persistentOwnerField",
        "editorOfflinePersistentPreviewField",
        "unknownStructField",
        "unknownOwnerField",
    }


def declaration_finding_to_dict(finding: DeclarationFinding) -> dict[str, Any]:
    return {
        "path": finding.path,
        "count": finding.count,
        "lines": list(finding.lines),
        "allowed": finding.allowed,
        "classification": finding.classification,
        "collectionType": finding.collection_type,
        "names": list(finding.names),
        "ownerType": finding.owner_type,
        "ownerKind": finding.owner_kind,
        "ownerLine": finding.owner_line,
        "burstOwner": finding.burst_owner,
    }


def scan_native_collection_declarations_in_source(
    source: str,
    relative_path: str,
    allowed: bool = False,
    sanitized_lines: Sequence[str] | None = None,
    original_lines: Sequence[str] | None = None,
) -> list[DeclarationFinding]:
    if sanitized_lines is None:
        sanitized_lines = sanitize_csharp_source(source).splitlines()
    if original_lines is None:
        original_lines = source.splitlines()

    tracked_editor_preview = (
        "H8MEMORY_TRACKED_EDITOR_PREVIEW" in source
        and "H8Memory.Allocate" in source
        and "H8Memory.Release" in source
    )
    scopes: list[TypeScope] = []
    pending_scope: TypeScope | None = None
    depth = 0
    findings: list[DeclarationFinding] = []

    for line_number, line in enumerate(sanitized_lines, 1):
        if pending_scope is not None and "{" in line:
            pending_scope = TypeScope(
                pending_scope.name,
                pending_scope.kind,
                pending_scope.bases,
                depth + 1,
                pending_scope.line,
                pending_scope.burst,
            )
            scopes.append(pending_scope)
            pending_scope = None

        type_match = TYPE_DECLARATION_RE.search(line)
        if type_match:
            lookback_start = max(0, line_number - 5)
            burst = any("BurstCompile" in item for item in original_lines[lookback_start:line_number])
            kind = " ".join(type_match.group("kind").split())
            new_scope = TypeScope(
                type_match.group("name"),
                kind,
                type_match.group("bases") or "",
                depth + 1,
                line_number,
                burst,
            )
            if "{" in line[type_match.end() :]:
                scopes.append(new_scope)
            else:
                pending_scope = new_scope

        declaration_match = NATIVE_COLLECTION_DECLARATION_RE.search(line)
        if declaration_match:
            owner = scopes[-1] if scopes else None
            classification = classify_declaration_scope(owner, relative_path)
            collection = declaration_match.group("collection")
            finding_allowed = (
                allowed
                or not declaration_is_gate_relevant(classification)
                or (
                    classification == "editorOfflinePersistentPreviewField"
                    and tracked_editor_preview
                )
            )
            findings.append(
                DeclarationFinding(
                    path=relative_path,
                    count=1,
                    lines=(line_number,),
                    allowed=finding_allowed,
                    classification=classification,
                    collection_type=collection,
                    names=(declaration_match.group("name"),),
                    owner_type=owner.name if owner else "",
                    owner_kind=owner.kind if owner else "",
                    owner_line=owner.line if owner else 0,
                    burst_owner=bool(owner and owner.burst),
                )
            )

        depth += line.count("{") - line.count("}")
        while scopes and depth < scopes[-1].body_depth:
            scopes.pop()

    return findings


def group_declaration_findings(findings: Iterable[DeclarationFinding]) -> list[DeclarationFinding]:
    grouped: dict[tuple[str, bool, str, str, str, int, bool], dict[str, Any]] = {}
    for finding in findings:
        key = (
            finding.path,
            finding.allowed,
            finding.classification,
            finding.collection_type,
            finding.owner_type,
            finding.owner_kind,
            finding.owner_line,
            finding.burst_owner,
        )
        entry = grouped.setdefault(
            key,
            {
                "lines": [],
                "names": [],
                "sample": finding,
            },
        )
        entry["lines"].extend(finding.lines)
        entry["names"].extend(finding.names)

    result: list[DeclarationFinding] = []
    for entry in grouped.values():
        sample = entry["sample"]
        lines = tuple(sorted(int(line) for line in entry["lines"]))
        names = tuple(str(name) for name in entry["names"])
        result.append(
            DeclarationFinding(
                path=sample.path,
                count=len(lines),
                lines=lines,
                allowed=sample.allowed,
                classification=sample.classification,
                collection_type=sample.collection_type,
                names=names,
                owner_type=sample.owner_type,
                owner_kind=sample.owner_kind,
                owner_line=sample.owner_line,
                burst_owner=sample.burst_owner,
            )
        )

    return sorted(result, key=lambda item: (item.path, item.lines[0], item.classification))


def scan_native_array_declaration_tree(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES,
) -> list[DeclarationFinding]:
    if not source_root.exists():
        raise FileNotFoundError(f"source root not found: {source_root}")

    findings: list[DeclarationFinding] = []
    for path in sorted(source_root.rglob("*.cs")):
        relative_scan_path = path.relative_to(source_root)
        if should_skip(relative_scan_path):
            continue

        try:
            source = path.read_text(encoding="utf-8", errors="ignore")
        except OSError as exc:
            raise OSError(f"failed to read {path}") from exc

        if not contains_native_collection_token(source):
            continue

        relative_path = normalize_path(path, repo_root)
        sanitized_lines = sanitize_csharp_source(source).splitlines()
        if not contains_native_collection_declaration_candidate(sanitized_lines):
            continue

        findings.extend(
            scan_native_collection_declarations_in_source(
                source,
                relative_path,
                is_allowed_path(relative_path, allowed_suffixes),
                sanitized_lines=sanitized_lines,
                original_lines=source.splitlines(),
            )
        )

    return group_declaration_findings(findings)


def scan_latest_created_fallback_tree(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_LATEST_CREATED_PATH_SUFFIXES,
) -> list[LatestCreatedFallbackFinding]:
    if not source_root.exists():
        raise FileNotFoundError(f"source root not found: {source_root}")

    findings: list[LatestCreatedFallbackFinding] = []
    for path in sorted(source_root.rglob("*.cs")):
        relative_scan_path = path.relative_to(source_root)
        if should_skip(relative_scan_path):
            continue

        try:
            source = path.read_text(encoding="utf-8", errors="ignore")
        except OSError as exc:
            raise OSError(f"failed to read {path}") from exc

        if not contains_latest_created_fallback_token(source):
            continue

        relative_path = normalize_path(path, repo_root)
        sanitized_lines = sanitize_csharp_source(source).splitlines()
        findings.extend(
            scan_latest_created_fallbacks_in_source(
                source,
                relative_path,
                allowed_suffixes=allowed_suffixes,
                sanitized_lines=sanitized_lines,
            )
        )

    return findings


def scan_source_tree_with_declarations(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    constructor_allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_PATH_SUFFIXES,
    declaration_allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES,
) -> tuple[list[FileFinding], list[DeclarationFinding]]:
    if not source_root.exists():
        raise FileNotFoundError(f"source root not found: {source_root}")

    constructor_findings: list[FileFinding] = []
    declaration_findings: list[DeclarationFinding] = []
    for path in sorted(source_root.rglob("*.cs")):
        relative_scan_path = path.relative_to(source_root)
        if should_skip(relative_scan_path):
            continue

        constructor_lines: list[int] = []
        constructor_allocator_kinds: list[str] = []
        try:
            source = path.read_text(encoding="utf-8", errors="ignore")
        except OSError as exc:
            raise OSError(f"failed to read {path}") from exc

        if not contains_native_collection_token(source):
            continue

        sanitized_lines = sanitize_csharp_source(source).splitlines()
        original_lines = source.splitlines()
        relative_path = normalize_path(path, repo_root)
        has_declaration_candidate = False

        for line_index, line in enumerate(sanitized_lines):
            if NATIVE_ARRAY_CONSTRUCTOR_RE.search(line):
                line_number = line_index + 1
                constructor_lines.append(line_number)
                constructor_allocator_kinds.append(
                    classify_constructor_allocator_at_line(sanitized_lines, line_index, source)
                )
            if not has_declaration_candidate and NATIVE_COLLECTION_DECLARATION_RE.search(line):
                has_declaration_candidate = True

        file_declaration_findings: list[DeclarationFinding] = []
        if has_declaration_candidate:
            file_declaration_findings = scan_native_collection_declarations_in_source(
                source,
                relative_path,
                is_allowed_path(relative_path, declaration_allowed_suffixes),
                sanitized_lines=sanitized_lines,
                original_lines=original_lines,
            )

        if not constructor_lines and not file_declaration_findings:
            continue

        if constructor_lines:
            line_execution_surfaces = extract_line_execution_surfaces_for_source(relative_path, source)
            constructor_surfaces = tuple(
                resolve_line_execution_surface(line_execution_surfaces, line_number)
                for line_number in constructor_lines
            )
            execution_surface = summarize_execution_surfaces(constructor_surfaces)
            constructor_findings.append(
                FileFinding(
                    path=relative_path,
                    count=len(constructor_lines),
                    lines=tuple(constructor_lines),
                    allowed=is_allowed_path(relative_path, constructor_allowed_suffixes),
                    allocator_counts=tuple(sorted(count_values(constructor_allocator_kinds).items())),
                    allocator_kinds=tuple(constructor_allocator_kinds),
                    execution_surface=execution_surface,
                    execution_surfaces=constructor_surfaces,
                )
            )
        declaration_findings.extend(file_declaration_findings)

    return constructor_findings, group_declaration_findings(declaration_findings)


def build_audit_payload(
    findings: Sequence[FileFinding],
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_PATH_SUFFIXES,
    declaration_findings: Sequence[DeclarationFinding] | None = None,
    declaration_allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES,
    latest_created_fallback_findings: Sequence[LatestCreatedFallbackFinding] | None = None,
    latest_created_allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_LATEST_CREATED_PATH_SUFFIXES,
) -> dict[str, Any]:
    total_direct = sum(finding.count for finding in findings)
    constructor_findings = [constructor_finding_to_dict(finding) for finding in findings]
    forbidden = [finding for finding in constructor_findings if int(finding["forbiddenCount"]) > 0]
    forbidden_direct = sum(int(finding["forbiddenCount"]) for finding in forbidden)
    allowed_direct = total_direct - forbidden_direct
    editor_offline_transient_scratch_direct = sum(
        int(finding["transientEditorScratchCount"])
        for finding in constructor_findings
    )
    forbidden_runtime_constructor_count = aggregate_constructor_count_for_surfaces(
        constructor_findings,
        "forbiddenLineExecutionSurfaceCounts",
        {RUNTIME_SURFACE},
    )
    forbidden_editor_offline_constructor_count = aggregate_constructor_count_for_surfaces(
        constructor_findings,
        "forbiddenLineExecutionSurfaceCounts",
        EDITOR_OFFLINE_SURFACES,
    )
    forbidden_constructor_allocator_counts = aggregate_constructor_allocators(
        [finding for finding in constructor_findings if int(finding["forbiddenCount"]) > 0]
    )
    editor_offline_forbidden_constructor_allocator_counts = aggregate_constructor_allocators_for_surfaces(
        [finding for finding in constructor_findings if int(finding["forbiddenCount"]) > 0],
        EDITOR_OFFLINE_SURFACES,
    )
    declaration_findings = tuple(declaration_findings or ())
    total_declarations = sum(finding.count for finding in declaration_findings)
    allowed_declarations = sum(finding.count for finding in declaration_findings if finding.allowed)
    forbidden_declarations = [finding for finding in declaration_findings if not finding.allowed]
    forbidden_declaration_count = sum(finding.count for finding in forbidden_declarations)
    persistent_declarations = [
        finding for finding in declaration_findings if finding.classification == "persistentOwnerField"
    ]
    job_input_declarations = [
        finding for finding in declaration_findings if finding.classification in {"jobInputStruct", "burstJobInput"}
    ]
    burst_job_input_declarations = [
        finding for finding in declaration_findings if finding.classification == "burstJobInput"
    ]
    unknown_struct_declarations = [
        finding for finding in declaration_findings if finding.classification == "unknownStructField"
    ]
    unknown_owner_declarations = [
        finding for finding in declaration_findings if finding.classification == "unknownOwnerField"
    ]
    native_view_declarations = [
        finding for finding in declaration_findings if finding.classification == "nativeViewStruct"
    ]
    editor_offline_session_scratch_declarations = [
        finding
        for finding in declaration_findings
        if finding.classification == "editorOfflineSessionScratchField"
    ]
    editor_offline_preview_declarations = [
        finding
        for finding in declaration_findings
        if finding.classification == "editorOfflinePersistentPreviewField"
    ]
    latest_created_fallback_findings = tuple(latest_created_fallback_findings or ())
    latest_created_findings = [
        latest_created_fallback_finding_to_dict(finding, latest_created_allowed_suffixes)
        for finding in latest_created_fallback_findings
    ]
    forbidden_latest_created_findings = [
        finding for finding in latest_created_findings if int(finding["forbiddenCount"]) > 0
    ]
    total_latest_created_fallbacks = sum(finding.count for finding in latest_created_fallback_findings)
    forbidden_latest_created_fallbacks = sum(
        int(finding["forbiddenCount"])
        for finding in forbidden_latest_created_findings
    )
    runtime_forbidden_latest_created_fallbacks = aggregate_constructor_count_for_surfaces(
        latest_created_findings,
        "forbiddenLineExecutionSurfaceCounts",
        {RUNTIME_SURFACE},
    )

    return {
        "schema": AUDIT_SCHEMA,
        "sourceRoot": normalize_path(source_root, repo_root),
        "pattern": NATIVE_ARRAY_CONSTRUCTOR_RE.pattern,
        "declarationPattern": NATIVE_ARRAY_DECLARATION_RE.pattern,
        "latestCreatedFallbackPattern": LATEST_CREATED_FALLBACK_RE.pattern,
        "allowedPathSuffixes": list(allowed_suffixes),
        "declarationAllowedPathSuffixes": list(declaration_allowed_suffixes),
        "latestCreatedAllowedPathSuffixes": list(latest_created_allowed_suffixes),
        "totalDirectConstructors": total_direct,
        "allowedDirectConstructors": allowed_direct,
        "forbiddenDirectConstructors": forbidden_direct,
        "directConstructorsByExecutionSurface": aggregate_constructor_surface_counts(
            constructor_findings,
            "lineExecutionSurfaceCounts",
        ),
        "forbiddenDirectConstructorsByExecutionSurface": aggregate_constructor_surface_counts(
            [finding for finding in constructor_findings if int(finding["forbiddenCount"]) > 0],
            "forbiddenLineExecutionSurfaceCounts",
        ),
        "forbiddenDirectConstructorsByAllocator": forbidden_constructor_allocator_counts,
        "editorOfflineForbiddenDirectConstructorsByAllocator": editor_offline_forbidden_constructor_allocator_counts,
        "editorOfflineTransientScratchDirectConstructors": editor_offline_transient_scratch_direct,
        "runtimeForbiddenDirectConstructors": forbidden_runtime_constructor_count,
        "editorOfflineForbiddenDirectConstructors": forbidden_editor_offline_constructor_count,
        "forbiddenFileCount": len(forbidden),
        "totalNativeArrayDeclarations": total_declarations,
        "totalNativeCollectionDeclarations": total_declarations,
        "allowedNativeArrayDeclarations": allowed_declarations,
        "allowedNativeCollectionDeclarations": allowed_declarations,
        "forbiddenNativeArrayDeclarations": forbidden_declaration_count,
        "forbiddenNativeCollectionDeclarations": forbidden_declaration_count,
        "persistentNativeCollectionDeclarations": sum(finding.count for finding in persistent_declarations),
        "jobInputNativeCollectionDeclarations": sum(finding.count for finding in job_input_declarations),
        "burstJobInputNativeCollectionDeclarations": sum(finding.count for finding in burst_job_input_declarations),
        "transientJobDataNativeCollectionDeclarations": 0,
        "nativeViewNativeCollectionDeclarations": sum(finding.count for finding in native_view_declarations),
        "editorOfflineSessionScratchNativeCollectionDeclarations": sum(
            finding.count for finding in editor_offline_session_scratch_declarations
        ),
        "editorOfflinePersistentPreviewNativeCollectionDeclarations": sum(
            finding.count for finding in editor_offline_preview_declarations
        ),
        "unknownStructNativeCollectionDeclarations": sum(finding.count for finding in unknown_struct_declarations),
        "unknownOwnerNativeCollectionDeclarations": sum(finding.count for finding in unknown_owner_declarations),
        "declarationFileCount": len(forbidden_declarations),
        "totalLatestCreatedFallbacks": total_latest_created_fallbacks,
        "allowedLatestCreatedFallbacks": total_latest_created_fallbacks - forbidden_latest_created_fallbacks,
        "forbiddenLatestCreatedFallbacks": forbidden_latest_created_fallbacks,
        "runtimeForbiddenLatestCreatedFallbacks": runtime_forbidden_latest_created_fallbacks,
        "forbiddenLatestCreatedFallbacksByExecutionSurface": aggregate_constructor_surface_counts(
            forbidden_latest_created_findings,
            "forbiddenLineExecutionSurfaceCounts",
        ),
        "latestCreatedFallbackFileCount": len(latest_created_findings),
        "forbiddenLatestCreatedFallbackFileCount": len(forbidden_latest_created_findings),
        "findingCount": len(findings),
        "findings": constructor_findings,
        "declarationFindings": [
            declaration_finding_to_dict(finding)
            for finding in declaration_findings
        ],
        "persistentDeclarationFindings": [
            declaration_finding_to_dict(finding)
            for finding in declaration_findings
            if finding.classification == "persistentOwnerField"
        ],
        "jobInputDeclarationFindings": [
            declaration_finding_to_dict(finding)
            for finding in declaration_findings
            if finding.classification in {"jobInputStruct", "burstJobInput"}
        ],
        "editorOfflineSessionScratchDeclarationFindings": [
            declaration_finding_to_dict(finding)
            for finding in declaration_findings
            if finding.classification == "editorOfflineSessionScratchField"
        ],
        "editorOfflinePersistentPreviewDeclarationFindings": [
            declaration_finding_to_dict(finding)
            for finding in declaration_findings
            if finding.classification == "editorOfflinePersistentPreviewField"
        ],
        "latestCreatedFallbackFindings": latest_created_findings,
    }


def build_baseline(payload: dict[str, Any]) -> dict[str, Any]:
    forbidden_by_file: dict[str, int] = {}
    allowed_by_file: dict[str, int] = {}
    for finding in payload["findings"]:
        forbidden_count = int(finding.get("forbiddenCount", 0 if finding["allowed"] else finding["count"]))
        allowed_count = int(finding["count"]) - forbidden_count
        if forbidden_count > 0:
            forbidden_by_file[finding["path"]] = forbidden_count
        if allowed_count > 0:
            allowed_by_file[finding["path"]] = allowed_count

    forbidden_declarations_by_file: dict[str, int] = {}
    allowed_declarations_by_file: dict[str, int] = {}
    for finding in payload.get("declarationFindings", []):
        target = allowed_declarations_by_file if finding["allowed"] else forbidden_declarations_by_file
        target[finding["path"]] = int(finding["count"])

    forbidden_latest_created_by_file: dict[str, int] = {}
    runtime_forbidden_latest_created_by_file: dict[str, int] = {}
    allowed_latest_created_by_file: dict[str, int] = {}
    for finding in payload.get("latestCreatedFallbackFindings", []):
        forbidden_count = int(finding.get("forbiddenCount", 0))
        runtime_forbidden_count = int(
            finding.get("forbiddenLineExecutionSurfaceCounts", {}).get(RUNTIME_SURFACE, 0)
        )
        allowed_count = int(finding.get("allowedCount", int(finding["count"]) - forbidden_count))
        if forbidden_count > 0:
            forbidden_latest_created_by_file[finding["path"]] = forbidden_count
        if runtime_forbidden_count > 0:
            runtime_forbidden_latest_created_by_file[finding["path"]] = runtime_forbidden_count
        if allowed_count > 0:
            allowed_latest_created_by_file[finding["path"]] = allowed_count

    return {
        "schema": BASELINE_SCHEMA,
        "sourceRoot": payload["sourceRoot"],
        "pattern": payload["pattern"],
        "declarationPattern": payload.get("declarationPattern", NATIVE_ARRAY_DECLARATION_RE.pattern),
        "latestCreatedFallbackPattern": payload.get("latestCreatedFallbackPattern", LATEST_CREATED_FALLBACK_RE.pattern),
        "totalDirectConstructors": payload["totalDirectConstructors"],
        "allowedDirectConstructors": payload["allowedDirectConstructors"],
        "forbiddenDirectConstructors": payload["forbiddenDirectConstructors"],
        "forbiddenDirectConstructorsByExecutionSurface": payload.get(
            "forbiddenDirectConstructorsByExecutionSurface",
            {},
        ),
        "editorOfflineTransientScratchDirectConstructors": payload.get(
            "editorOfflineTransientScratchDirectConstructors",
            0,
        ),
        "runtimeForbiddenDirectConstructors": payload.get("runtimeForbiddenDirectConstructors", 0),
        "editorOfflineForbiddenDirectConstructors": payload.get("editorOfflineForbiddenDirectConstructors", 0),
        "forbiddenFileCount": payload["forbiddenFileCount"],
        "totalNativeArrayDeclarations": payload.get("totalNativeArrayDeclarations", 0),
        "allowedNativeArrayDeclarations": payload.get("allowedNativeArrayDeclarations", 0),
        "forbiddenNativeArrayDeclarations": payload.get("forbiddenNativeArrayDeclarations", 0),
        "declarationFileCount": payload.get("declarationFileCount", 0),
        "totalLatestCreatedFallbacks": payload.get("totalLatestCreatedFallbacks", 0),
        "allowedLatestCreatedFallbacks": payload.get("allowedLatestCreatedFallbacks", 0),
        "forbiddenLatestCreatedFallbacks": payload.get("forbiddenLatestCreatedFallbacks", 0),
        "runtimeForbiddenLatestCreatedFallbacks": payload.get("runtimeForbiddenLatestCreatedFallbacks", 0),
        "forbiddenLatestCreatedFallbackFileCount": payload.get("forbiddenLatestCreatedFallbackFileCount", 0),
        "allowedPathSuffixes": payload["allowedPathSuffixes"],
        "declarationAllowedPathSuffixes": payload.get(
            "declarationAllowedPathSuffixes",
            list(DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES),
        ),
        "latestCreatedAllowedPathSuffixes": payload.get(
            "latestCreatedAllowedPathSuffixes",
            list(DEFAULT_ALLOWED_LATEST_CREATED_PATH_SUFFIXES),
        ),
        "forbiddenByFile": dict(sorted(forbidden_by_file.items())),
        "allowedByFile": dict(sorted(allowed_by_file.items())),
        "forbiddenDeclarationsByFile": dict(sorted(forbidden_declarations_by_file.items())),
        "allowedDeclarationsByFile": dict(sorted(allowed_declarations_by_file.items())),
        "forbiddenLatestCreatedFallbacksByFile": dict(sorted(forbidden_latest_created_by_file.items())),
        "runtimeForbiddenLatestCreatedFallbacksByFile": dict(sorted(runtime_forbidden_latest_created_by_file.items())),
        "allowedLatestCreatedFallbacksByFile": dict(sorted(allowed_latest_created_by_file.items())),
    }


def load_json(path: Path) -> dict[str, Any] | None:
    if not path.exists():
        return None

    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)

    if not isinstance(data, dict):
        raise ValueError(f"baseline is not a JSON object: {path}")

    return data


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def forbidden_by_file(payload: dict[str, Any]) -> dict[str, int]:
    result: dict[str, int] = {}
    for finding in payload["findings"]:
        forbidden_count = int(finding.get("forbiddenCount", 0 if finding["allowed"] else finding["count"]))
        if forbidden_count <= 0:
            continue

        result[finding["path"]] = forbidden_count

    return result


def forbidden_declarations_by_file(payload: dict[str, Any]) -> dict[str, int]:
    result: dict[str, int] = {}
    for finding in payload.get("declarationFindings", []):
        if finding["allowed"]:
            continue

        result[finding["path"]] = int(finding["count"])

    return result


def forbidden_latest_created_fallbacks_by_file(
    payload: dict[str, Any],
    runtime_only: bool = False,
) -> dict[str, int]:
    result: dict[str, int] = {}
    for finding in payload.get("latestCreatedFallbackFindings", []):
        if runtime_only:
            count = int(
                finding.get("forbiddenLineExecutionSurfaceCounts", {}).get(RUNTIME_SURFACE, 0)
            )
        else:
            count = int(finding.get("forbiddenCount", 0))
        if count <= 0:
            continue

        result[finding["path"]] = count

    return result


def extract_domain(relative_path: str) -> str:
    normalized = relative_path.replace("\\", "/")
    prefix = "Assets/_Project/Scripts/"
    if not normalized.startswith(prefix):
        return "External"

    remainder = normalized[len(prefix) :]
    if "/" not in remainder:
        return "Root"

    return remainder.split("/", 1)[0] or "Root"


def extract_execution_surface(relative_path: str) -> str:
    normalized = relative_path.replace("\\", "/")
    lowered = normalized.lower()
    parts = [part.lower() for part in normalized.split("/")]

    if "editor" in parts:
        return "Editor"
    if "tests" in parts or "test" in parts:
        return "Test"
    if "dev" in parts or lowered.endswith("smoketester.cs") or "testharness" in lowered:
        return "Dev"
    filename = parts[-1] if parts else lowered
    if any(token in filename for token in OFFLINE_BAKE_PATH_TOKENS):
        return "OfflineBake"
    if "/plugins/" in lowered:
        return "Plugin"
    if not normalized.startswith("Assets/_Project/Scripts/"):
        return "External"

    return RUNTIME_SURFACE


def aggregate_current_findings_by_surface(
    findings: Sequence[dict[str, Any]],
    count_key: str = "count",
) -> dict[str, int]:
    result: dict[str, int] = {}
    for finding in findings:
        surface = str(finding.get("executionSurface") or extract_execution_surface(str(finding["path"])))
        result[surface] = result.get(surface, 0) + int(finding.get(count_key, 0))

    return dict(sorted(result.items()))


def aggregate_constructor_surface_counts(
    findings: Sequence[dict[str, Any]],
    counts_key: str,
) -> dict[str, int]:
    result: dict[str, int] = {}
    for finding in findings:
        surface_counts = finding.get(counts_key, {})
        if not isinstance(surface_counts, dict):
            continue
        for surface, count in surface_counts.items():
            key = str(surface)
            result[key] = result.get(key, 0) + int(count)

    return dict(sorted(result.items()))


def aggregate_constructor_count_for_surfaces(
    findings: Sequence[dict[str, Any]],
    counts_key: str,
    surfaces: Container[str],
) -> int:
    total = 0
    for finding in findings:
        surface_counts = finding.get(counts_key, {})
        if not isinstance(surface_counts, dict):
            continue
        for surface, count in surface_counts.items():
            if str(surface) in surfaces:
                total += int(count)

    return total


def aggregate_constructor_allocators(findings: Sequence[dict[str, Any]]) -> dict[str, int]:
    result: dict[str, int] = {}
    for finding in findings:
        allocator_counts = finding.get("forbiddenAllocatorCounts", finding.get("allocatorCounts", {}))
        if not isinstance(allocator_counts, dict):
            continue
        for allocator, count in allocator_counts.items():
            key = str(allocator)
            result[key] = result.get(key, 0) + int(count)

    return dict(sorted(result.items()))


def aggregate_constructor_allocators_for_surfaces(
    findings: Sequence[dict[str, Any]],
    surfaces: Container[str],
) -> dict[str, int]:
    result: dict[str, int] = {}
    for finding in findings:
        surface_allocator_counts = finding.get("forbiddenAllocatorCountsByExecutionSurface", {})
        if not isinstance(surface_allocator_counts, dict):
            continue
        for surface, allocator_counts in surface_allocator_counts.items():
            if str(surface) not in surfaces or not isinstance(allocator_counts, dict):
                continue
            for allocator, count in allocator_counts.items():
                key = str(allocator)
                result[key] = result.get(key, 0) + int(count)

    return dict(sorted(result.items()))


def collect_regression_details(
    payload: dict[str, Any],
    baseline: dict[str, Any] | None,
) -> tuple[list[str], list[dict[str, Any]]]:
    if baseline is None:
        return ["Baseline missing; no-regression gate fails closed."], []

    errors: list[str] = []
    details: list[dict[str, Any]] = []
    if baseline.get("schema") != BASELINE_SCHEMA:
        errors.append(f"Baseline schema mismatch: {baseline.get('schema')!r}.")

    current_total = int(payload["forbiddenDirectConstructors"])
    baseline_total = int(baseline.get("forbiddenDirectConstructors", -1))
    if current_total > baseline_total:
        errors.append(
            "Forbidden direct NativeArray constructors increased "
            f"from {baseline_total} to {current_total}."
        )

    baseline_by_file = baseline.get("forbiddenByFile", {})
    if not isinstance(baseline_by_file, dict):
        errors.append("Baseline forbiddenByFile is missing or invalid.")
        baseline_by_file = {}

    for path, count in sorted(forbidden_by_file(payload).items()):
        baseline_count = int(baseline_by_file.get(path, 0))
        if count > baseline_count:
            delta = count - baseline_count
            errors.append(
                f"{path}: forbidden direct constructors increased from {baseline_count} to {count}."
            )
            details.append(
                {
                    "kind": "directConstructor",
                    "domain": extract_domain(path),
                    "executionSurface": extract_execution_surface(path),
                    "path": path,
                    "baseline": baseline_count,
                    "current": count,
                    "delta": delta,
                }
            )

    if "forbiddenNativeArrayDeclarations" in payload or "declarationFindings" in payload:
        current_declaration_total = int(payload.get("forbiddenNativeArrayDeclarations", 0))
        baseline_declaration_total = int(baseline.get("forbiddenNativeArrayDeclarations", -1))
        if current_declaration_total > baseline_declaration_total:
            errors.append(
                "Forbidden NativeArray field declarations increased "
                f"from {baseline_declaration_total} to {current_declaration_total}."
            )

        baseline_declarations_by_file = baseline.get("forbiddenDeclarationsByFile", {})
        if not isinstance(baseline_declarations_by_file, dict):
            errors.append("Baseline forbiddenDeclarationsByFile is missing or invalid.")
            baseline_declarations_by_file = {}

        for path, count in sorted(forbidden_declarations_by_file(payload).items()):
            baseline_count = int(baseline_declarations_by_file.get(path, 0))
            if count > baseline_count:
                delta = count - baseline_count
                errors.append(
                    f"{path}: forbidden NativeArray field declarations increased from {baseline_count} to {count}."
                )
                details.append(
                    {
                        "kind": "fieldDeclaration",
                        "domain": extract_domain(path),
                        "executionSurface": extract_execution_surface(path),
                        "path": path,
                        "baseline": baseline_count,
                        "current": count,
                        "delta": delta,
                    }
                )

    if "forbiddenLatestCreatedFallbacks" in payload or "latestCreatedFallbackFindings" in payload:
        current_latest_total = int(payload.get("forbiddenLatestCreatedFallbacks", 0))
        baseline_latest_total = int(baseline.get("forbiddenLatestCreatedFallbacks", -1))
        if current_latest_total > baseline_latest_total:
            errors.append(
                "Forbidden GlobalDataVault.TryGetLatestCreated fallbacks increased "
                f"from {baseline_latest_total} to {current_latest_total}."
            )

        baseline_latest_by_file = baseline.get("forbiddenLatestCreatedFallbacksByFile", {})
        if not isinstance(baseline_latest_by_file, dict):
            errors.append("Baseline forbiddenLatestCreatedFallbacksByFile is missing or invalid.")
            baseline_latest_by_file = {}

        baseline_runtime_latest_by_file = baseline.get("runtimeForbiddenLatestCreatedFallbacksByFile", {})
        if not isinstance(baseline_runtime_latest_by_file, dict):
            errors.append("Baseline runtimeForbiddenLatestCreatedFallbacksByFile is missing or invalid.")
            baseline_runtime_latest_by_file = {}

        current_runtime_latest_by_file = forbidden_latest_created_fallbacks_by_file(payload, runtime_only=True)
        for path, count in sorted(forbidden_latest_created_fallbacks_by_file(payload).items()):
            baseline_count = int(baseline_latest_by_file.get(path, 0))
            if count > baseline_count:
                errors.append(
                    f"{path}: forbidden TryGetLatestCreated fallbacks increased from {baseline_count} to {count}."
                )
                current_runtime_count = int(current_runtime_latest_by_file.get(path, 0))
                baseline_runtime_count = int(baseline_runtime_latest_by_file.get(path, 0))
                current_non_runtime_count = max(0, count - current_runtime_count)
                baseline_non_runtime_count = max(0, baseline_count - baseline_runtime_count)
                if current_non_runtime_count > baseline_non_runtime_count:
                    delta = current_non_runtime_count - baseline_non_runtime_count
                    details.append(
                        {
                            "kind": "latestCreatedFallback",
                            "domain": extract_domain(path),
                            "executionSurface": extract_execution_surface(path),
                            "path": path,
                            "baseline": baseline_non_runtime_count,
                            "current": current_non_runtime_count,
                            "delta": delta,
                        }
                    )

        current_runtime_latest_total = int(payload.get("runtimeForbiddenLatestCreatedFallbacks", 0))
        baseline_runtime_latest_total = int(baseline.get("runtimeForbiddenLatestCreatedFallbacks", -1))
        if current_runtime_latest_total > baseline_runtime_latest_total:
            errors.append(
                "Runtime forbidden GlobalDataVault.TryGetLatestCreated fallbacks increased "
                f"from {baseline_runtime_latest_total} to {current_runtime_latest_total}."
            )

        for path, count in sorted(current_runtime_latest_by_file.items()):
            baseline_count = int(baseline_runtime_latest_by_file.get(path, 0))
            if count > baseline_count:
                delta = count - baseline_count
                errors.append(
                    f"{path}: runtime TryGetLatestCreated fallbacks increased from {baseline_count} to {count}."
                )
                details.append(
                    {
                        "kind": "latestCreatedRuntimeFallback",
                        "domain": extract_domain(path),
                        "executionSurface": RUNTIME_SURFACE,
                        "path": path,
                        "baseline": baseline_count,
                        "current": count,
                        "delta": delta,
                    }
                )

    return errors, details


def detect_regressions(payload: dict[str, Any], baseline: dict[str, Any] | None) -> list[str]:
    errors, _ = collect_regression_details(payload, baseline)
    return errors


def collect_runtime_regression_details(
    payload: dict[str, Any],
    baseline: dict[str, Any] | None,
) -> tuple[list[str], list[dict[str, Any]]]:
    if baseline is None:
        return ["Baseline missing; runtime no-regression gate fails closed."], []

    errors: list[str] = []
    if baseline.get("schema") != BASELINE_SCHEMA:
        errors.append(f"Baseline schema mismatch: {baseline.get('schema')!r}.")

    _, details = collect_regression_details(payload, baseline)
    runtime_details = [
        detail for detail in details if detail.get("executionSurface") == RUNTIME_SURFACE
    ]
    if runtime_details:
        errors.append(
            "Runtime DataVault native ownership regressions detected: "
            f"{len(runtime_details)} file deltas."
        )
        for detail in runtime_details:
            errors.append(
                f"{detail['path']}: runtime {detail['kind']} increased "
                f"from {detail['baseline']} to {detail['current']}."
            )

    return errors, runtime_details


def aggregate_regression_details(details: Sequence[dict[str, Any]]) -> list[dict[str, Any]]:
    aggregate: dict[str, dict[str, Any]] = {}
    for detail in details:
        domain = str(detail.get("domain", "Unknown"))
        entry = aggregate.setdefault(
            domain,
            {
                "domain": domain,
                "delta": 0,
                "directConstructorDelta": 0,
                "fieldDeclarationDelta": 0,
                "latestCreatedFallbackDelta": 0,
                "fileCount": 0,
                "files": set(),
            },
        )
        delta = int(detail.get("delta", 0))
        entry["delta"] += delta
        if detail.get("kind") == "directConstructor":
            entry["directConstructorDelta"] += delta
        elif detail.get("kind") == "fieldDeclaration":
            entry["fieldDeclarationDelta"] += delta
        elif detail.get("kind") in {"latestCreatedFallback", "latestCreatedRuntimeFallback"}:
            entry["latestCreatedFallbackDelta"] += delta
        path = str(detail.get("path", ""))
        if path:
            entry["files"].add(path)

    rows: list[dict[str, Any]] = []
    for entry in aggregate.values():
        files = sorted(entry["files"])
        rows.append(
            {
                "domain": entry["domain"],
                "delta": entry["delta"],
                "directConstructorDelta": entry["directConstructorDelta"],
                "fieldDeclarationDelta": entry["fieldDeclarationDelta"],
                "latestCreatedFallbackDelta": entry["latestCreatedFallbackDelta"],
                "fileCount": len(files),
                "files": files,
            }
        )

    return sorted(rows, key=lambda item: (-int(item["delta"]), str(item["domain"])))


def aggregate_regression_details_by_surface(details: Sequence[dict[str, Any]]) -> list[dict[str, Any]]:
    aggregate: dict[str, dict[str, Any]] = {}
    for detail in details:
        surface = str(detail.get("executionSurface", "Unknown"))
        entry = aggregate.setdefault(
            surface,
            {
                "executionSurface": surface,
                "delta": 0,
                "directConstructorDelta": 0,
                "fieldDeclarationDelta": 0,
                "latestCreatedFallbackDelta": 0,
                "fileCount": 0,
                "files": set(),
            },
        )
        delta = int(detail.get("delta", 0))
        entry["delta"] += delta
        if detail.get("kind") == "directConstructor":
            entry["directConstructorDelta"] += delta
        elif detail.get("kind") == "fieldDeclaration":
            entry["fieldDeclarationDelta"] += delta
        elif detail.get("kind") in {"latestCreatedFallback", "latestCreatedRuntimeFallback"}:
            entry["latestCreatedFallbackDelta"] += delta
        path = str(detail.get("path", ""))
        if path:
            entry["files"].add(path)

    rows: list[dict[str, Any]] = []
    for entry in aggregate.values():
        files = sorted(entry["files"])
        rows.append(
            {
                "executionSurface": entry["executionSurface"],
                "delta": entry["delta"],
                "directConstructorDelta": entry["directConstructorDelta"],
                "fieldDeclarationDelta": entry["fieldDeclarationDelta"],
                "latestCreatedFallbackDelta": entry["latestCreatedFallbackDelta"],
                "fileCount": len(files),
                "files": files,
            }
        )

    return sorted(rows, key=lambda item: (-int(item["delta"]), str(item["executionSurface"])))


def build_report_payload(
    payload: dict[str, Any],
    baseline_path: Path,
    baseline: dict[str, Any] | None,
    regression_errors: Sequence[str],
    regression_details: Sequence[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "schema": REPORT_SCHEMA,
        "audit": payload,
        "baselinePath": normalize_path(baseline_path),
        "baselineSchema": None if baseline is None else baseline.get("schema"),
        "regressionErrors": list(regression_errors),
        "regressionDetails": list(regression_details),
        "regressionByDomain": aggregate_regression_details(regression_details),
        "regressionByExecutionSurface": aggregate_regression_details_by_surface(regression_details),
    }


def top_findings(payload: dict[str, Any], allowed: bool, limit: int) -> list[dict[str, Any]]:
    findings = [
        finding
        for finding in payload["findings"]
        if bool(finding["allowed"]) == allowed
    ]
    count_key = "count" if allowed else "forbiddenCount"
    return sorted(findings, key=lambda item: (-int(item.get(count_key, item["count"])), str(item["path"])))[:limit]


def format_line_samples(lines: Iterable[int], limit: int = 8) -> str:
    values = list(lines)
    sample = ", ".join(str(line) for line in values[:limit])
    if len(values) > limit:
        sample += ", ..."

    return sample


def write_markdown_report(
    path: Path,
    payload: dict[str, Any],
    baseline_path: Path,
    baseline: dict[str, Any] | None,
    regression_errors: Sequence[str],
    regression_details: Sequence[dict[str, Any]],
    top_limit: int,
) -> None:
    status = "PASS_NO_REGRESSION_WITH_LEGACY_DEBT"
    if baseline is None:
        status = "BLOCKED_BASELINE_MISSING"
    elif regression_errors:
        status = "FAIL_REGRESSION"
    elif (
        int(payload["forbiddenDirectConstructors"]) == 0
        and int(payload.get("forbiddenNativeArrayDeclarations", 0)) == 0
        and int(payload.get("forbiddenLatestCreatedFallbacks", 0)) == 0
    ):
        status = "PASS_ZERO_FORBIDDEN_DATAVAULT_DEBT"

    lines: list[str] = [
        "# DataVault Sovereignty Audit - VAULT_SOVEREIGNTY_ENFORCER",
        "",
        f"Schema: `{AUDIT_SCHEMA}`",
        f"Status: `{status}`",
        f"Source root: `{payload['sourceRoot']}`",
        f"Pattern: `{payload['pattern']}`",
        f"Baseline: `{normalize_path(baseline_path)}`",
        "",
        "## Summary",
        "",
        "| Metric | Count |",
        "|---|---:|",
        f"| Total direct `new NativeArray<T>` constructors | {payload['totalDirectConstructors']} |",
        f"| Allowed allocator-internal constructors | {payload['allowedDirectConstructors']} |",
        f"| Forbidden system constructors | {payload['forbiddenDirectConstructors']} |",
        f"| Runtime forbidden constructors | {payload.get('runtimeForbiddenDirectConstructors', 0)} |",
        f"| Editor/offline forbidden constructors | {payload.get('editorOfflineForbiddenDirectConstructors', 0)} |",
        f"| Editor/offline transient scratch constructors | {payload.get('editorOfflineTransientScratchDirectConstructors', 0)} |",
        f"| Files with forbidden constructors | {payload['forbiddenFileCount']} |",
        f"| Editor/offline session scratch declarations | {payload.get('editorOfflineSessionScratchNativeCollectionDeclarations', 0)} |",
        f"| Editor/offline persistent preview declarations | {payload.get('editorOfflinePersistentPreviewNativeCollectionDeclarations', 0)} |",
        f"| Total field-like `NativeArray<T>` declarations | {payload.get('totalNativeArrayDeclarations', 0)} |",
        f"| Allowed DataVault/H8Memory declarations | {payload.get('allowedNativeArrayDeclarations', 0)} |",
        f"| Forbidden system declarations | {payload.get('forbiddenNativeArrayDeclarations', 0)} |",
        f"| Persistent owner native collection declarations | {payload.get('persistentNativeCollectionDeclarations', 0)} |",
        f"| Job input native collection declarations | {payload.get('jobInputNativeCollectionDeclarations', 0)} |",
        f"| Burst job input native collection declarations | {payload.get('burstJobInputNativeCollectionDeclarations', 0)} |",
        f"| Native view/payload/kernel struct declarations | {payload.get('nativeViewNativeCollectionDeclarations', 0)} |",
        f"| Unknown struct native collection declarations | {payload.get('unknownStructNativeCollectionDeclarations', 0)} |",
        f"| Files with forbidden declarations | {payload.get('declarationFileCount', 0)} |",
        f"| Total `GlobalDataVault.TryGetLatestCreated` fallbacks | {payload.get('totalLatestCreatedFallbacks', 0)} |",
        f"| Allowed bootstrap/editor/diagnostic fallbacks | {payload.get('allowedLatestCreatedFallbacks', 0)} |",
        f"| Forbidden domain runtime fallbacks | {payload.get('forbiddenLatestCreatedFallbacks', 0)} |",
        f"| Runtime forbidden domain fallbacks | {payload.get('runtimeForbiddenLatestCreatedFallbacks', 0)} |",
        f"| Files with forbidden domain fallbacks | {payload.get('forbiddenLatestCreatedFallbackFileCount', 0)} |",
        "",
    ]

    if regression_errors:
        lines.extend(["## Regression Findings", ""])
        for error in regression_errors:
            lines.append(f"- {error}")
        lines.append("")

    constructor_surface_totals = payload.get("forbiddenDirectConstructorsByExecutionSurface", {})
    if constructor_surface_totals:
        lines.extend(
            [
                "## Current Forbidden Constructors By Execution Surface",
                "",
                "| Surface | Count |",
                "|---|---:|",
            ]
        )
        for surface, count in sorted(constructor_surface_totals.items()):
            lines.append(f"| `{surface}` | {count} |")
        lines.append("")

    allocator_totals = payload.get("forbiddenDirectConstructorsByAllocator", {})
    if allocator_totals:
        lines.extend(
            [
                "## Current Forbidden Constructors By Allocator",
                "",
                "| Allocator | Count |",
                "|---|---:|",
            ]
        )
        for allocator, count in sorted(allocator_totals.items()):
            lines.append(f"| `{allocator}` | {count} |")
        lines.append("")

    editor_allocator_totals = payload.get("editorOfflineForbiddenDirectConstructorsByAllocator", {})
    if editor_allocator_totals:
        lines.extend(
            [
                "## Editor/Offline Forbidden Constructors By Allocator",
                "",
                "| Allocator | Count |",
                "|---|---:|",
            ]
        )
        for allocator, count in sorted(editor_allocator_totals.items()):
            lines.append(f"| `{allocator}` | {count} |")
        lines.append("")

    latest_created_surface_totals = payload.get("forbiddenLatestCreatedFallbacksByExecutionSurface", {})
    if latest_created_surface_totals:
        lines.extend(
            [
                "## Current Forbidden TryGetLatestCreated Fallbacks By Execution Surface",
                "",
                "| Surface | Count |",
                "|---|---:|",
            ]
        )
        for surface, count in sorted(latest_created_surface_totals.items()):
            lines.append(f"| `{surface}` | {count} |")
        lines.append("")

    domain_regressions = aggregate_regression_details(regression_details)
    surface_regressions = aggregate_regression_details_by_surface(regression_details)
    if surface_regressions:
        lines.extend(
            [
                "## Regression Delta By Execution Surface",
                "",
                "| Surface | Delta | Direct constructor delta | Field declaration delta | TryGetLatestCreated delta | Files |",
                "|---|---:|---:|---:|---:|---:|",
            ]
        )
        for item in surface_regressions:
            lines.append(
                f"| `{item['executionSurface']}` | {item['delta']} | "
                f"{item['directConstructorDelta']} | {item['fieldDeclarationDelta']} | "
                f"{item['latestCreatedFallbackDelta']} | "
                f"{item['fileCount']} |"
            )
        lines.append("")

    if domain_regressions:
        lines.extend(
            [
                "## Regression Delta By Domain",
                "",
                "| Domain | Delta | Direct constructor delta | Field declaration delta | TryGetLatestCreated delta | Files |",
                "|---|---:|---:|---:|---:|---:|",
            ]
        )
        for item in domain_regressions:
            lines.append(
                f"| `{item['domain']}` | {item['delta']} | "
                f"{item['directConstructorDelta']} | {item['fieldDeclarationDelta']} | "
                f"{item['latestCreatedFallbackDelta']} | "
                f"{item['fileCount']} |"
            )
        lines.append("")

        lines.extend(
            [
                "## Regression Delta Details",
                "",
                "| Kind | Surface | Domain | Baseline | Current | Delta | Path |",
                "|---|---|---|---:|---:|---:|---|",
            ]
        )
        for item in sorted(
            regression_details,
            key=lambda detail: (
                -int(detail.get("delta", 0)),
                str(detail.get("domain", "")),
                str(detail.get("path", "")),
            ),
        ):
            lines.append(
                f"| `{item['kind']}` | `{item.get('executionSurface', 'Unknown')}` | "
                f"`{item['domain']}` | "
                f"{item['baseline']} | {item['current']} | {item['delta']} | "
                f"`{item['path']}` |"
            )
        lines.append("")

    lines.extend(
        [
            f"## Top {top_limit} Forbidden Files",
            "",
            "| Count | Path | Lines |",
            "|---:|---|---|",
        ]
    )
    for finding in top_findings(payload, allowed=False, limit=top_limit):
        count = int(finding.get("forbiddenCount", finding["count"]))
        lines_sample = finding.get("forbiddenLines", finding["lines"])
        lines.append(
            f"| {count} | `{finding['path']}` | {format_line_samples(lines_sample)} |"
        )

    lines.extend(
        [
            "",
            f"## Top {top_limit} Forbidden Declaration Files",
            "",
            "| Count | Path | Lines |",
            "|---:|---|---|",
        ]
    )
    for finding in top_findings(
        {"findings": payload.get("declarationFindings", [])},
        allowed=False,
        limit=top_limit,
    ):
        lines.append(
            f"| {finding['count']} | `{finding['path']}` | {format_line_samples(finding['lines'])} |"
        )

    lines.extend(
        [
            "",
            f"## Top {top_limit} Forbidden TryGetLatestCreated Fallback Files",
            "",
            "| Count | Path | Lines |",
            "|---:|---|---|",
        ]
    )
    forbidden_latest = top_findings(
        {"findings": payload.get("latestCreatedFallbackFindings", [])},
        allowed=False,
        limit=top_limit,
    )
    if forbidden_latest:
        for finding in forbidden_latest:
            count = int(finding.get("forbiddenCount", finding["count"]))
            lines_sample = finding.get("forbiddenLines", finding["lines"])
            lines.append(
                f"| {count} | `{finding['path']}` | {format_line_samples(lines_sample)} |"
            )
    else:
        lines.append("| 0 | none | |")

    lines.extend(
        [
            "",
            "## Allowed Allocator-Internal Sites",
            "",
            "| Count | Path | Lines |",
            "|---:|---|---|",
        ]
    )
    allowed_findings = top_findings(payload, allowed=True, limit=top_limit)
    if allowed_findings:
        for finding in allowed_findings:
            lines.append(
                f"| {finding['count']} | `{finding['path']}` | {format_line_samples(finding['lines'])} |"
            )
    else:
        lines.append("| 0 | none | |")

    lines.extend(
        [
            "",
            "## Allowed DataVault/H8Memory Declaration Sites",
            "",
            "| Count | Path | Lines |",
            "|---:|---|---|",
        ]
    )
    allowed_declaration_findings = top_findings(
        {"findings": payload.get("declarationFindings", [])},
        allowed=True,
        limit=top_limit,
    )
    if allowed_declaration_findings:
        for finding in allowed_declaration_findings:
            lines.append(
                f"| {finding['count']} | `{finding['path']}` | {format_line_samples(finding['lines'])} |"
            )
    else:
        lines.append("| 0 | none | |")

    lines.extend(
        [
            "",
            "## Gate Commands",
            "",
            "```powershell",
            "python Tools\\DataVaultSovereigntyAudit.py --fail-on-regression",
            "python Tools\\DataVaultSovereigntyAudit.py --fail-on-any",
            "```",
            "",
            "`--fail-on-regression` blocks any new or increased forbidden constructor, field-declaration, or domain `TryGetLatestCreated` fallback count against the baseline.",
            "`--fail-on-any` is the final zero-debt gate and currently fails until all legacy DataVault debt is migrated.",
            "",
        ]
    )

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=DEFAULT_SOURCE_ROOT, help="Source tree to scan.")
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE_PATH, help="No-regression baseline JSON.")
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT_PATH, help="Markdown report path.")
    parser.add_argument("--audit-json", type=Path, default=None, help="Optional JSON report path.")
    parser.add_argument("--write-baseline", action="store_true", help="Overwrite the baseline with the current audit.")
    parser.add_argument("--fail-on-regression", action="store_true", help="Exit nonzero if forbidden constructor debt increases.")
    parser.add_argument(
        "--fail-on-runtime-regression",
        action="store_true",
        help="Exit nonzero only for runtime constructor or field declaration regressions.",
    )
    parser.add_argument("--fail-on-any", action="store_true", help="Exit nonzero if any forbidden DataVault debt remains.")
    parser.add_argument("--no-report", action="store_true", help="Do not write the Markdown report.")
    parser.add_argument("--top", type=int, default=40, help="Number of findings to include in the report.")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(sys.argv[1:] if argv is None else argv)
    findings, declaration_findings = scan_source_tree_with_declarations(args.root)
    latest_created_fallback_findings = scan_latest_created_fallback_tree(args.root)
    payload = build_audit_payload(
        findings,
        args.root,
        declaration_findings=declaration_findings,
        latest_created_fallback_findings=latest_created_fallback_findings,
    )
    baseline = load_json(args.baseline)

    if args.write_baseline:
        baseline = build_baseline(payload)
        write_json(args.baseline, baseline)

    if args.fail_on_regression:
        regression_errors, regression_details = collect_regression_details(payload, baseline)
    elif args.fail_on_runtime_regression:
        regression_errors, regression_details = collect_runtime_regression_details(payload, baseline)
    else:
        regression_errors, regression_details = [], []
    if not args.no_report:
        write_markdown_report(
            args.report,
            payload,
            args.baseline,
            baseline,
            regression_errors,
            regression_details,
            max(args.top, 1),
        )
    if args.audit_json is not None:
        write_json(
            args.audit_json,
            build_report_payload(payload, args.baseline, baseline, regression_errors, regression_details),
        )

    failure_reasons = list(regression_errors)
    if args.fail_on_any and int(payload["forbiddenDirectConstructors"]) > 0:
        failure_reasons.append(
            f"{payload['forbiddenDirectConstructors']} forbidden direct NativeArray constructors remain."
        )
    if args.fail_on_any and int(payload.get("forbiddenNativeArrayDeclarations", 0)) > 0:
        failure_reasons.append(
            f"{payload['forbiddenNativeArrayDeclarations']} forbidden NativeArray field declarations remain."
        )
    if args.fail_on_any and int(payload.get("forbiddenLatestCreatedFallbacks", 0)) > 0:
        failure_reasons.append(
            f"{payload['forbiddenLatestCreatedFallbacks']} forbidden GlobalDataVault.TryGetLatestCreated fallbacks remain."
        )

    status = "PASS"
    if failure_reasons:
        status = "FAIL"

    print(
        "DataVault sovereignty audit: "
        f"status={status}, "
        f"direct={payload['totalDirectConstructors']}, "
        f"allowed={payload['allowedDirectConstructors']}, "
        f"forbidden={payload['forbiddenDirectConstructors']}, "
        f"runtimeForbidden={payload.get('runtimeForbiddenDirectConstructors', 0)}, "
        f"editorOfflineForbidden={payload.get('editorOfflineForbiddenDirectConstructors', 0)}, "
        f"editorOfflineTransientScratch={payload.get('editorOfflineTransientScratchDirectConstructors', 0)}, "
        f"editorOfflineAllocatorSplit={payload.get('editorOfflineForbiddenDirectConstructorsByAllocator', {})}, "
        f"files={payload['forbiddenFileCount']}, "
        f"declarations={payload.get('totalNativeArrayDeclarations', 0)}, "
        f"forbiddenDeclarations={payload.get('forbiddenNativeArrayDeclarations', 0)}, "
        f"persistentDeclarations={payload.get('persistentNativeCollectionDeclarations', 0)}, "
        f"editorSessionScratchDeclarations={payload.get('editorOfflineSessionScratchNativeCollectionDeclarations', 0)}, "
        f"editorPreviewPersistentDeclarations={payload.get('editorOfflinePersistentPreviewNativeCollectionDeclarations', 0)}, "
        f"jobInputDeclarations={payload.get('jobInputNativeCollectionDeclarations', 0)}, "
        f"declarationFiles={payload.get('declarationFileCount', 0)}, "
        f"latestCreatedFallbacks={payload.get('totalLatestCreatedFallbacks', 0)}, "
        f"forbiddenLatestCreatedFallbacks={payload.get('forbiddenLatestCreatedFallbacks', 0)}, "
        f"runtimeForbiddenLatestCreatedFallbacks={payload.get('runtimeForbiddenLatestCreatedFallbacks', 0)}"
    )
    for reason in failure_reasons:
        print(f"ERROR: {reason}", file=sys.stderr)

    return 1 if failure_reasons else 0


if __name__ == "__main__":
    raise SystemExit(main())
