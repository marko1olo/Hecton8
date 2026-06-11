#!/usr/bin/env python3
"""Audit direct native collection constructors against DataVault sovereignty."""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from functools import lru_cache
import functools
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
NATIVE_COLLECTION_TYPE_PATTERN = r"NativeArray|NativeList|Native(?:Parallel)?HashMap"
CSHARP_INLINE_GENERIC_ARGUMENTS_PATTERN = r"(?:<[^>\r\n;{}()]+>)?"
NATIVE_COLLECTION_CONSTRUCTOR_RE = re.compile(
    r"\bnew\s+(?P<collection>" + NATIVE_COLLECTION_TYPE_PATTERN + r")\s*<[^;\r\n=]+>\s*\("
)
NATIVE_ARRAY_CONSTRUCTOR_RE = NATIVE_COLLECTION_CONSTRUCTOR_RE
NATIVE_ARRAY_ALLOCATOR_RE = re.compile(r"\bAllocator\s*\.\s*(?P<allocator>Persistent|TempJob|Temp)\b")
NATIVE_COLLECTION_CONSTRUCTOR_ASSIGNMENT_RE = re.compile(
    r"(?:\b(?:" + NATIVE_COLLECTION_TYPE_PATTERN + r")\s*<[^>\r\n;=(){}]+>\s+)?"
    r"(?P<name>[A-Za-z_]\w*)\s*=\s*new\s+"
    r"(?P<collection>" + NATIVE_COLLECTION_TYPE_PATTERN + r")\s*<[^;\r\n=]+>\s*\("
)
NATIVE_ARRAY_CONSTRUCTOR_ASSIGNMENT_RE = NATIVE_COLLECTION_CONSTRUCTOR_ASSIGNMENT_RE
NATIVE_COLLECTION_CONSTRUCTOR_ASSIGNMENT_BLOCK_RE = re.compile(
    r"(?:\b(?:" + NATIVE_COLLECTION_TYPE_PATTERN + r")\s*<[^>\r\n;=(){}]+>\s+)?"
    r"(?P<name>[A-Za-z_]\w*)\s*=\s*"
    r"(?:\r?\n\s*)?new\s+(?P<collection>" + NATIVE_COLLECTION_TYPE_PATTERN + r")\s*<[^;\r\n=]+>\s*\(",
    re.MULTILINE,
)
NATIVE_ARRAY_CONSTRUCTOR_ASSIGNMENT_BLOCK_RE = NATIVE_COLLECTION_CONSTRUCTOR_ASSIGNMENT_BLOCK_RE
LATEST_CREATED_FALLBACK_RE = re.compile(r"\bGlobalDataVault\s*\.\s*TryGetLatestCreated\s*\(")
NON_NEWLINE_RE = re.compile(r"[^\r\n]")
REF_OUT_RE = re.compile(r"\b(ref|in|out)\s+")
IDENTIFIER_RE = re.compile(r"[A-Za-z_]\w*")
NATIVE_COLLECTION_DECLARATION_RE = re.compile(
    r"^\s*(?:\[[^\]]+\]\s*)*"
    r"(?:(?:public|private|protected|internal|static|readonly|volatile|unsafe|new)\s+)+"
    r"(?P<collection>NativeArray|NativeList|Native(?:Parallel)?HashMap)\s*<[^>;]+>\s+"
    r"(?P<name>[A-Za-z_]\w*)\s*(?:;|,|=\s*(?!>))"
)
NATIVE_COLLECTION_DECLARATION_SCAN_RE = re.compile(
    NATIVE_COLLECTION_DECLARATION_RE.pattern,
    re.MULTILINE,
)
NATIVE_ARRAY_DECLARATION_RE = NATIVE_COLLECTION_DECLARATION_RE
TYPE_DECLARATION_RE = re.compile(
    r"\b(?P<kind>ref\s+(?:partial\s+)?struct|record\s+struct|record\s+class|class|struct|interface|record)\s+"
    r"(?P<name>[A-Za-z_]\w*)"
    r"(?:\s*<[^>{}]+>)?"
    r"(?:\s*:\s*(?P<bases>[^{]+))?"
)
JOB_INTERFACE_TOKENS = (
    "IJob",
    "IJobFor",
    "IJobParallelFor",
    "IJobParallelForTransform",
    "IJobEntity",
    "IJobChunk",
    "IAnimationJob",
)
NATIVE_COLLECTION_TOKENS = (
    "NativeArray",
    "NativeList",
    "NativeHashMap",
    "NativeParallelHashMap",
)
DIRECTORY_NATIVE_LIFETIME_HELPER_FILE_TOKENS = (
    "bridge",
    "helper",
    "helpers",
    "lifetime",
    "memory",
    "native",
    "util",
    "utilities",
    "utility",
)
DIRECTORY_NATIVE_LIFETIME_HELPER_TYPE_RE = re.compile(
    r"\b(?:static\s+)?(?:class|struct)\s+"
    r"[A-Za-z_]\w*(?:Bridge|Helper|Helpers|Lifetime|Memory|Native|Util|Utilities|Utility)[A-Za-z_]\w*"
)
SENTINEL_REGISTER_METHODS_BY_COLLECTION = {
    "NativeArray": ("RegisterNativeArray", "RegisterNativeArrayInstance"),
    "NativeList": ("RegisterNativeList", "RegisterNativeListInstance"),
    "NativeHashMap": ("RegisterNativeHashMap", "RegisterNativeHashMapInstance"),
    "NativeParallelHashMap": (
        "RegisterNativeParallelHashMap",
        "RegisterNativeParallelHashMapInstance",
    ),
}
NATIVE_VIEW_STRUCT_SUFFIXES = (
    "Buffer",
    "BufferSet",
    "Buffers",
    "BufferView",
    "BufferViews",
    "Command",
    "Data",
    "Lease",
    "NativeScratch",
    "NativeState",
    "PendingJob",
    "View",
    "Views",
    "Payload",
    "Writer",
    "Snapshot",
    "Kernel",
)
REGISTERED_NATIVE_OWNER_STRUCT_NAMES = frozenset({
    "RegisteredTransientNativeArray",
})
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


@dataclass(frozen=True)
class CSharpSourceFile:
    path: Path
    relative_scan_path: Path
    source: str
    sanitized_source: str
    sanitized_lines: tuple[str, ...]
    original_lines: tuple[str, ...]


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


def source_needs_sanitized_snapshot(source: str) -> bool:
    if (
        "H8Memory" in source
        or "NativeMemorySentinel" in source
        or "NativeMemoryTrackingBridge" in source
        or "TryGetLatestCreated" in source
    ):
        return True
    if not contains_native_collection_token(source):
        return False
    return (
        NATIVE_COLLECTION_CONSTRUCTOR_RE.search(source) is not None
        or NATIVE_COLLECTION_DECLARATION_SCAN_RE.search(source) is not None
    )


@functools.lru_cache(maxsize=32)
def read_csharp_source_files(source_root: Path) -> list[CSharpSourceFile]:
    if not source_root.exists():
        raise FileNotFoundError(f"source root not found: {source_root}")

    source_files: list[CSharpSourceFile] = []
    for path in sorted(source_root.rglob("*.cs")):
        relative_scan_path = path.relative_to(source_root)
        if should_skip(relative_scan_path):
            continue

        try:
            source = path.read_text(encoding="utf-8", errors="ignore")
        except FileNotFoundError:
            continue
        except OSError as exc:
            raise OSError(f"failed to read {path}") from exc

        sanitized_source = sanitize_csharp_source(source) if source_needs_sanitized_snapshot(source) else ""
        source_files.append(
            CSharpSourceFile(
                path,
                relative_scan_path,
                source,
                sanitized_source,
                tuple(sanitized_source.splitlines()) if sanitized_source else (),
                tuple(source.splitlines()) if sanitized_source else (),
            )
        )

    return source_files


def contains_native_collection_token(source: str) -> bool:
    return any(token in source for token in NATIVE_COLLECTION_TOKENS)


def contains_native_collection_declaration_candidate(lines: Sequence[str]) -> bool:
    return any(NATIVE_COLLECTION_DECLARATION_RE.search(line) for line in lines)


def contains_latest_created_fallback_token(source: str) -> bool:
    return "TryGetLatestCreated" in source


def source_file_may_provide_directory_native_lifetime_helpers(
    source_file: CSharpSourceFile,
    source: str,
) -> bool:
    stem = source_file.path.stem.lower()
    if any(token in stem for token in DIRECTORY_NATIVE_LIFETIME_HELPER_FILE_TOKENS):
        return True

    if any(
        token in source
        for token in (
            "AllocateTrackedNative",
            "DisposeTrackedNative",
            "RegisterTrackedNative",
            "UnregisterTrackedNative",
        )
    ):
        return True

    return DIRECTORY_NATIVE_LIFETIME_HELPER_TYPE_RE.search(source) is not None


def scan_source_tree(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_PATH_SUFFIXES,
) -> list[FileFinding]:
    findings: list[FileFinding] = []
    for source_file in read_csharp_source_files(source_root):
        path = source_file.path
        source = source_file.source
        line_numbers: list[int] = []
        allocator_kinds: list[str] = []
        if not contains_native_collection_token(source):
            continue
        if NATIVE_COLLECTION_CONSTRUCTOR_RE.search(source) is None:
            continue

        sanitized_lines = source_file.sanitized_lines
        if not sanitized_lines:
            sanitized_lines = tuple(sanitize_csharp_source(source).splitlines())
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


@functools.lru_cache(maxsize=128)
def sanitize_csharp_source(source: str) -> str:
    result: list[str] = []
    index = 0
    length = len(source)

    def append_masked(segment: str) -> None:
        if segment:
            result.append(NON_NEWLINE_RE.sub(" ", segment))

    def find_standard_literal_end(start: int, quote: str) -> int:
        cursor = start + 1
        escaped = False
        while cursor < length:
            char = source[cursor]
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == quote:
                return cursor + 1
            cursor += 1

        return length

    def find_verbatim_string_end(start: int) -> int:
        cursor = start + 2
        while cursor < length:
            char = source[cursor]
            nxt = source[cursor + 1] if cursor + 1 < length else ""
            if char == '"' and nxt == '"':
                cursor += 2
                continue
            if char == '"':
                return cursor + 1
            cursor += 1

        return length

    while index < length:
        next_index = length
        for token in ("/", "\"", "'", "@"):
            token_index = source.find(token, index)
            if token_index != -1 and token_index < next_index:
                next_index = token_index

        if next_index > index:
            result.append(source[index:next_index])
            index = next_index
            if index >= length:
                break

        char = source[index]
        nxt = source[index + 1] if index + 1 < length else ""

        if char == "/" and nxt == "/":
            end = source.find("\n", index + 2)
            if end == -1:
                append_masked(source[index:])
                break
            append_masked(source[index:end])
            index = end
            continue
        if char == "/" and nxt == "*":
            end = source.find("*/", index + 2)
            end = length if end == -1 else end + 2
            append_masked(source[index:end])
            index = end
            continue
        if char == "@" and nxt == '"':
            end = find_verbatim_string_end(index)
            append_masked(source[index:end])
            index = end
            continue
        if char == '"':
            end = find_standard_literal_end(index, '"')
            append_masked(source[index:end])
            index = end
            continue
        if char == "'":
            end = find_standard_literal_end(index, "'")
            append_masked(source[index:end])
            index = end
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


def is_ref_struct_scope(scope: TypeScope | None) -> bool:
    if scope is None:
        return False
    return scope.kind.startswith("ref ") and "struct" in scope.kind


def is_registered_native_owner_scope(scope: TypeScope | None) -> bool:
    if scope is None:
        return False
    if "struct" not in scope.kind:
        return False
    return scope.name in REGISTERED_NATIVE_OWNER_STRUCT_NAMES


def classify_constructor_allocator(line: str) -> str:
    match = NATIVE_ARRAY_ALLOCATOR_RE.search(line)
    return match.group("allocator") if match else "Unknown"


def constructor_window(lines: Sequence[str], line_index: int, max_lines: int = 8) -> str:
    return "\n".join(lines[line_index : min(len(lines), line_index + max_lines)])


def native_array_constructor_assignment_name(lines: Sequence[str], line_index: int) -> str:
    line = lines[line_index]
    matches = list(NATIVE_ARRAY_CONSTRUCTOR_ASSIGNMENT_RE.finditer(line))
    if not matches:
        window = "\n".join(lines[max(0, line_index - 3) : line_index + 1])
        matches = list(NATIVE_ARRAY_CONSTRUCTOR_ASSIGNMENT_BLOCK_RE.finditer(window))
    if not matches:
        return ""

    name = matches[-1].group("name")
    return "" if name == "_" else name


def native_collection_constructor_type(lines: Sequence[str], line_index: int) -> str:
    window = constructor_window(lines, line_index, 4)
    match = NATIVE_COLLECTION_CONSTRUCTOR_RE.search(window)
    return match.group("collection") if match else "NativeArray"


def sentinel_register_methods_for_collection(collection_type: str) -> tuple[str, ...]:
    return SENTINEL_REGISTER_METHODS_BY_COLLECTION.get(collection_type, ())


def native_tracking_register_owner_pattern() -> str:
    return (
        r"(?:NativeMemorySentinel|"
        r"(?:Hecton8\s*\.\s*Core\s*\.\s*Contracts\s*\.\s*)?NativeMemoryTrackingBridge)"
    )


def h8memory_owner_pattern() -> str:
    return r"(?:Hecton8\s*\.\s*Core\s*\.\s*Memory\s*\.)?H8Memory"


def helper_name_fragments_for_collection(collection_type: str) -> tuple[str, ...]:
    if collection_type == "NativeArray":
        return ("NativeArray", "Array")
    if collection_type == "NativeList":
        return ("NativeList", "List")
    if collection_type == "NativeHashMap":
        return ("NativeHashMap", "HashMap")
    if collection_type == "NativeParallelHashMap":
        return ("NativeParallelHashMap", "ParallelHashMap", "HashMap")

    return (collection_type,)


def csharp_blocks_containing_line(lines: Sequence[str], line_index: int) -> list[tuple[int, int]]:
    stack: list[int] = []
    blocks: list[tuple[int, int]] = []
    for index, line in enumerate(lines):
        for char in line:
            if char == "{":
                stack.append(index)
                continue
            if char == "}":
                if not stack:
                    continue
                start = stack.pop()
                if start <= line_index <= index:
                    blocks.append((start, index))

    return blocks


def find_matching_brace(text: str, open_index: int) -> int:
    if open_index < 0 or open_index >= len(text) or text[open_index] != "{":
        return -1

    depth = 0
    for index in range(open_index, len(text)):
        char = text[index]
        if char == "{":
            depth += 1
            continue
        if char == "}":
            depth -= 1
            if depth == 0:
                return index

    return -1


def csharp_block_header(lines: Sequence[str], start_index: int, max_lines: int = 6) -> str:
    return "\n".join(lines[max(0, start_index - max_lines) : start_index + 1])


def is_method_like_block_header(header: str) -> bool:
    control_prefixes = (
        "if",
        "else",
        "for",
        "foreach",
        "while",
        "switch",
        "try",
        "catch",
        "finally",
        "using",
        "lock",
        "fixed",
    )
    raw_lines = [line.strip() for line in header.splitlines() if line.strip()]
    if not raw_lines:
        return False

    last_line = raw_lines[-1]
    signature_lines: list[str] = []
    if "{" in last_line:
        before_brace = last_line.rsplit("{", 1)[0].strip()
        if before_brace:
            signature_lines.append(before_brace)

    if not signature_lines:
        for line in reversed(raw_lines[:-1]):
            signature_lines.insert(0, line)
            first_token_match = re.match(r"^(?P<token>[A-Za-z_]\w*)\b", line)
            if first_token_match and first_token_match.group("token") in control_prefixes:
                break
            if "(" in line:
                break

    compact = " ".join(" ".join(signature_lines).strip().split())
    if "(" not in compact or ")" not in compact:
        return False

    first_token_match = re.match(r"^(?P<token>[A-Za-z_]\w*)\b", compact)
    if first_token_match and first_token_match.group("token") in control_prefixes:
        return False

    return True


def csharp_method_bounds_for_line(lines: Sequence[str], line_index: int) -> tuple[int, int]:
    candidates: list[tuple[int, int]] = []
    for start, end in csharp_blocks_containing_line(lines, line_index):
        if is_method_like_block_header(csharp_block_header(lines, start)):
            candidates.append((start, end))

    if candidates:
        return max(candidates, key=lambda item: item[0])

    return max(0, line_index - 8), min(len(lines) - 1, line_index + 16)


def csharp_method_signature_header_for_line(lines: Sequence[str], line_index: int) -> str:
    start, _ = csharp_method_bounds_for_line(lines, line_index)
    header = csharp_block_header(lines, start)
    if is_method_like_block_header(header) and TYPE_DECLARATION_RE.search(header) is None:
        return header

    search_start = max(0, line_index - 64)
    for brace_line in range(line_index, search_start - 1, -1):
        if "{" not in lines[brace_line]:
            continue

        signature_lines: list[str] = []
        for header_line in range(brace_line, search_start - 1, -1):
            stripped = lines[header_line].strip()
            if not stripped and not signature_lines:
                continue
            if signature_lines and stripped in {"}", "};"}:
                break

            signature_lines.insert(0, lines[header_line])
            candidate = "\n".join(signature_lines)
            if is_method_like_block_header(candidate):
                return candidate

            if len(signature_lines) > 32:
                break

    return header


def register_native_array_helper_names(method_text: str, variable_name: str) -> list[str]:
    return register_native_collection_helper_names(method_text, variable_name, "NativeArray")


def helper_name_pattern_for_collection(collection_type: str) -> str:
    fragments = list(helper_name_fragments_for_collection(collection_type))
    fragments.extend(["NativeMemory", "Sentinel", "Tracked", "NativeCollection", "Collection"])
    if collection_type == "NativeArray":
        fragments.extend(["Buffer", "Buffers"])

    helper_fragment_pattern = "|".join(re.escape(fragment) for fragment in sorted(set(fragments)))
    return (
        r"\b(?P<helper>Register[A-Za-z0-9_]*(?:"
        + helper_fragment_pattern
        + r")[A-Za-z0-9_]*|"
        r"[A-Za-z_]\w*Register[A-Za-z0-9_]*(?:"
        + helper_fragment_pattern
        + r")[A-Za-z0-9_]*)"
        r"\s*"
        + CSHARP_INLINE_GENERIC_ARGUMENTS_PATTERN
        + r"\s*\("
    )


def find_matching_parenthesis(text: str, open_index: int) -> int:
    if open_index < 0 or open_index >= len(text) or text[open_index] != "(":
        return -1

    depth = 0
    for index in range(open_index, len(text)):
        char = text[index]
        if char == "(":
            depth += 1
            continue
        if char == ")":
            depth -= 1
            if depth == 0:
                return index

    return -1


def split_csharp_arguments(argument_text: str) -> list[str]:
    arguments: list[str] = []
    start = 0
    depth = 0
    for index, char in enumerate(argument_text):
        if char in "([{<":
            depth += 1
            continue
        if char in ")]}>":
            depth = max(0, depth - 1)
            continue
        if char == "," and depth == 0:
            arguments.append(argument_text[start:index].strip())
            start = index + 1

    tail = argument_text[start:].strip()
    if tail:
        arguments.append(tail)
    return arguments


@functools.lru_cache(maxsize=1024)
def csharp_argument_identifier(argument: str) -> str:
    cleaned = REF_OUT_RE.sub("", argument.strip())
    identifiers = IDENTIFIER_RE.findall(cleaned)
    return identifiers[-1] if identifiers else ""


@functools.lru_cache(maxsize=1024)
def csharp_reference_pattern(identifier: str) -> str:
    parts = [part for part in IDENTIFIER_RE.findall(identifier) if part]
    if not parts:
        return re.escape(identifier)

    if len(parts) > 1:
        return r"\s*\.\s*".join(re.escape(part) for part in parts) + r"\b"

    return r"(?:[A-Za-z_]\w*\s*\.\s*)*" + re.escape(parts[0]) + r"\b"


def csharp_helper_call_pattern() -> str:
    return (
        r"\b(?:[A-Za-z_]\w*\s*\.\s*)*"
        r"(?P<helper>[A-Za-z_]\w*)\s*"
        + CSHARP_INLINE_GENERIC_ARGUMENTS_PATTERN
        + r"\s*\("
    )


def csharp_parameter_name(parameter: str) -> str:
    cleaned = parameter.split("=", 1)[0].strip()
    identifiers = re.findall(r"[A-Za-z_]\w*", cleaned)
    return identifiers[-1] if identifiers else ""


@lru_cache(maxsize=8192)
def register_native_collection_helper_calls(
    method_text: str,
    variable_name: str,
    collection_type: str,
) -> tuple[tuple[str, int], ...]:
    helper_pattern = re.compile(helper_name_pattern_for_collection(collection_type))
    calls: list[tuple[str, int]] = []
    for match in helper_pattern.finditer(method_text):
        open_index = match.end() - 1
        close_index = find_matching_parenthesis(method_text, open_index)
        if close_index < 0:
            continue

        arguments = split_csharp_arguments(method_text[open_index + 1 : close_index])
        for argument_index, argument in enumerate(arguments):
            if csharp_argument_identifier(argument) == variable_name:
                calls.append((match.group("helper"), argument_index))
                break

    return tuple(calls)


def register_native_collection_helper_names(
    method_text: str,
    variable_name: str,
    collection_type: str,
) -> tuple[str, ...]:
    return tuple(
        helper_name
        for helper_name, _ in register_native_collection_helper_calls(
            method_text,
            variable_name,
            collection_type,
        )
    )


def source_has_sentinel_register_helper(
    sanitized_source: str,
    helper_name: str,
    collection_type: str = "NativeArray",
) -> bool:
    direct_register_methods = sentinel_register_methods_for_collection(collection_type)
    if not direct_register_methods:
        return False

    direct_methods_pattern = "|".join(re.escape(method) for method in direct_register_methods)
    direct_register_pattern = re.compile(
        r"\b" + native_tracking_register_owner_pattern() + r"\s*\.\s*(?:"
        + direct_methods_pattern + r")\s*\("
    )
    for helper_text in source_method_bodies_named(sanitized_source, helper_name):
        if direct_register_pattern.search(helper_text):
            return True

    helper_pattern = re.compile(
        r"\b" + re.escape(helper_name) + r"\s*"
        + CSHARP_INLINE_GENERIC_ARGUMENTS_PATTERN
        + r"\s*\("
    )
    helper_lines = sanitized_source.splitlines()
    for helper_match in helper_pattern.finditer(sanitized_source):
        helper_line_index = sanitized_source[: helper_match.start()].count("\n")
        start, end = csharp_method_bounds_for_line(helper_lines, helper_line_index)
        helper_text = "\n".join(helper_lines[start : end + 1])
        if direct_register_pattern.search(helper_text):
            return True

    return False


def source_has_reflective_sentinel_register_for_parameter(
    method_text: str,
    parameter_name: str,
    collection_type: str,
) -> bool:
    if "NativeMemorySentinel" not in method_text or "GetMethod" not in method_text or "Invoke" not in method_text:
        return False

    direct_register_methods = sentinel_register_methods_for_collection(collection_type)
    if not direct_register_methods:
        return False

    if not any(method_name in method_text for method_name in direct_register_methods):
        return False

    for object_array_match in re.finditer(r"\bnew\s+object\s*\[\]\s*\{(?P<arguments>[^}]*)\}", method_text, re.DOTALL):
        arguments = split_csharp_arguments(object_array_match.group("arguments"))
        if arguments and csharp_argument_identifier(arguments[0]) == parameter_name:
            return True

    return False


def source_has_reflective_sentinel_unregister_for_parameter(
    method_text: str,
    parameter_name: str,
    collection_type: str,
) -> bool:
    if "NativeMemorySentinel" not in method_text or "GetMethod" not in method_text or "Invoke" not in method_text:
        return False

    unregister_methods = sentinel_unregister_methods_for_collection(collection_type)
    if not unregister_methods:
        return False

    if not any(method_name in method_text for method_name in unregister_methods):
        return False

    for object_array_match in re.finditer(r"\bnew\s+object\s*\[\]\s*\{(?P<arguments>[^}]*)\}", method_text, re.DOTALL):
        arguments = split_csharp_arguments(object_array_match.group("arguments"))
        if arguments and csharp_argument_identifier(arguments[0]) == parameter_name:
            return True

    return False


def source_has_reflective_sentinel_lifetime_tokens(source: str) -> bool:
    return (
        "NativeMemorySentinel" in source
        and "GetMethod" in source
        and "Invoke" in source
        and "RegisterNative" in source
        and "UnregisterNative" in source
    )


def source_has_h8memory_lifetime_tokens(source: str) -> bool:
    return "H8Memory" in source and "Allocate" in source and "Release" in source


def source_has_sentinel_registration_tokens(source: str) -> bool:
    return "NativeMemorySentinel" in source and "Register" in source


def source_has_sentinel_lifetime_tokens(source: str) -> bool:
    return source_has_sentinel_registration_tokens(source) and "Unregister" in source


def source_has_native_tracking_bridge_register_bytes_tokens(source: str) -> bool:
    return "NativeMemoryTrackingBridge" in source and "RegisterBytes" in source


def source_has_native_tracking_bridge_lifetime_tokens(source: str) -> bool:
    return source_has_native_tracking_bridge_register_bytes_tokens(source) and "Unregister" in source


@lru_cache(maxsize=4096)
def source_method_definitions_named(
    sanitized_source: str,
    method_name: str,
) -> list[tuple[str, list[str]]]:
    method_pattern = re.compile(
        r"\b(?:public|private|internal|protected|static|readonly|unsafe|sealed|"
        r"virtual|override|async|extern|partial|\s)+"
        r"[A-Za-z_][A-Za-z0-9_<>,\s\[\]\.?]*\s+"
        + re.escape(method_name)
        + r"\s*"
        + CSHARP_INLINE_GENERIC_ARGUMENTS_PATTERN
        + r"\s*\("
    )
    definitions: list[tuple[str, list[str]]] = []
    for method_match in method_pattern.finditer(sanitized_source):
        open_paren = sanitized_source.find("(", method_match.end() - 1)
        close_paren = find_matching_parenthesis(sanitized_source, open_paren)
        next_brace = sanitized_source.find("{", close_paren if close_paren >= 0 else method_match.end())
        next_semicolon = sanitized_source.find(";", close_paren if close_paren >= 0 else method_match.end())
        if open_paren < 0 or close_paren < 0 or next_brace < 0:
            continue
        if next_semicolon >= 0 and next_semicolon < next_brace:
            continue

        close_brace = find_matching_brace(sanitized_source, next_brace)
        if close_brace < 0:
            continue

        parameter_names = [
            csharp_parameter_name(parameter)
            for parameter in split_csharp_arguments(sanitized_source[open_paren + 1 : close_paren])
        ]
        definitions.append((sanitized_source[method_match.start() : close_brace + 1], parameter_names))

    return definitions


def source_method_bodies_named(sanitized_source: str, method_name: str) -> list[str]:
    return [body for body, _ in source_method_definitions_named(sanitized_source, method_name)]


def source_has_sentinel_register_helper_argument(
    sanitized_source: str,
    helper_name: str,
    argument_index: int,
    collection_type: str = "NativeArray",
    raw_source: str | None = None,
    visited_helper_keys: set[tuple[str, int]] | None = None,
) -> bool:
    if visited_helper_keys is None:
        visited_helper_keys = set()
    helper_key = (helper_name, argument_index)
    if helper_key in visited_helper_keys:
        return False
    visited_helper_keys.add(helper_key)

    direct_register_methods = sentinel_register_methods_for_collection(collection_type)
    if not direct_register_methods:
        return False

    direct_methods_pattern = "|".join(re.escape(method) for method in direct_register_methods)
    for helper_text, parameter_names in source_method_definitions_named(sanitized_source, helper_name):
        if argument_index >= len(parameter_names):
            continue

        parameter_name = parameter_names[argument_index]
        if not parameter_name:
            continue

        direct_register_pattern = re.compile(
            r"\b"
            + native_tracking_register_owner_pattern()
            + r"\s*\.\s*(?:"
            + direct_methods_pattern
            + r")\s*\(\s*"
            + csharp_reference_pattern(parameter_name)
        )
        if direct_register_pattern.search(helper_text):
            return True
        if source_has_native_tracking_register_bytes_for_parameter(helper_text, parameter_name):
            return True

        if raw_source is not None:
            for raw_helper_text, raw_parameter_names in source_method_definitions_named(raw_source, helper_name):
                if argument_index >= len(raw_parameter_names):
                    continue

                raw_parameter_name = raw_parameter_names[argument_index]
                if not raw_parameter_name:
                    continue

                if source_has_reflective_sentinel_register_for_parameter(
                    raw_helper_text,
                    raw_parameter_name,
                    collection_type,
                ):
                    return True
                if source_has_native_tracking_register_bytes_for_parameter(raw_helper_text, raw_parameter_name):
                    return True

        for nested_helper_name, nested_argument_index in register_native_collection_helper_calls(
            helper_text,
            parameter_name,
            collection_type,
        ):
            if source_has_sentinel_register_helper_argument(
                sanitized_source,
                nested_helper_name,
                nested_argument_index,
                collection_type,
                raw_source,
                visited_helper_keys,
            ):
                return True

    return False



@lru_cache(maxsize=8192)
def bulk_sentinel_registration_method_names(method_text: str) -> tuple[str, ...]:
    method_pattern = re.compile(
        r"\b(?:[A-Za-z_]\w*\s*\.\s*)?"
        r"(?P<method>Register[A-Za-z0-9_]*)\s*\("
    )
    return tuple(match.group("method") for match in method_pattern.finditer(method_text))


def source_has_bulk_sentinel_registration_for_field(
    sanitized_source: str,
    bulk_method_name: str,
    field_name: str,
    collection_type: str = "NativeArray",
    raw_source: str | None = None,
    visited_method_names: set[str] | None = None,
) -> bool:
    if visited_method_names is None:
        visited_method_names = set()
    if bulk_method_name in visited_method_names:
        return False
    visited_method_names.add(bulk_method_name)

    direct_register_methods = sentinel_register_methods_for_collection(collection_type)
    direct_register_pattern: re.Pattern[str] | None = None
    if direct_register_methods:
        direct_methods_pattern = "|".join(re.escape(method) for method in direct_register_methods)
        direct_register_pattern = re.compile(
            r"\b"
            + native_tracking_register_owner_pattern()
            + r"\s*\.\s*(?:"
            + direct_methods_pattern
            + r")\s*\(\s*"
            + csharp_reference_pattern(field_name)
        )

    for bulk_method_body in source_method_bodies_named(sanitized_source, bulk_method_name):
        if direct_register_pattern is not None and direct_register_pattern.search(bulk_method_body):
            return True

        for helper_name, argument_index in register_native_collection_helper_calls(
            bulk_method_body,
            field_name,
            collection_type,
        ):
            if source_has_sentinel_register_helper_argument(
                sanitized_source,
                helper_name,
                argument_index,
                collection_type,
                raw_source,
            ):
                return True

        for nested_method_name in bulk_sentinel_registration_method_names(bulk_method_body):
            if source_has_bulk_sentinel_registration_for_field(
                sanitized_source,
                nested_method_name,
                field_name,
                collection_type,
                raw_source,
                visited_method_names,
            ):
                return True

    return False


@lru_cache(maxsize=2048)
def native_lifetime_helper_call_entries(method_text: str) -> tuple[tuple[str, int, str], ...]:
    if not any(token in method_text for token in ("Dispose", "Release", "Unregister")):
        return ()

    helper_pattern = re.compile(csharp_helper_call_pattern())
    calls: list[tuple[str, int, str]] = []
    for match in helper_pattern.finditer(method_text):
        helper_name = match.group("helper")
        if not any(token in helper_name for token in ("Dispose", "Release", "Unregister")):
            continue

        open_index = match.end() - 1
        close_index = find_matching_parenthesis(method_text, open_index)
        if close_index < 0:
            continue

        arguments = split_csharp_arguments(method_text[open_index + 1 : close_index])
        for argument_index, argument in enumerate(arguments):
            argument_name = csharp_argument_identifier(argument)
            if argument_name:
                calls.append((helper_name, argument_index, argument_name))

    return tuple(calls)


@lru_cache(maxsize=8192)
def native_lifetime_helper_calls(method_text: str, variable_name: str) -> tuple[tuple[str, int], ...]:
    if variable_name not in method_text:
        return ()

    return tuple(
        (helper_name, argument_index)
        for helper_name, argument_index, argument_name in native_lifetime_helper_call_entries(method_text)
        if argument_name == variable_name
    )


@lru_cache(maxsize=8192)
def native_lifetime_helper_label_calls(method_text: str, field_name: str) -> tuple[tuple[str, int, int], ...]:
    helper_pattern = re.compile(csharp_helper_call_pattern())
    calls: list[tuple[str, int, int]] = []
    label_argument_pattern = re.compile(r"\bnameof\s*\(\s*" + re.escape(field_name) + r"\s*\)")
    for match in helper_pattern.finditer(method_text):
        helper_name = match.group("helper")
        if not any(token in helper_name for token in ("Dispose", "Release", "Unregister")):
            continue

        open_index = match.end() - 1
        close_index = find_matching_parenthesis(method_text, open_index)
        if close_index < 0:
            continue

        arguments = split_csharp_arguments(method_text[open_index + 1 : close_index])
        field_argument_index = -1
        label_argument_index = -1
        for argument_index, argument in enumerate(arguments):
            if field_argument_index < 0 and csharp_argument_identifier(argument) == field_name:
                field_argument_index = argument_index
            if label_argument_index < 0 and label_argument_pattern.search(argument):
                label_argument_index = argument_index

        if field_argument_index >= 0 and label_argument_index >= 0:
            calls.append((helper_name, field_argument_index, label_argument_index))

    return tuple(calls)


@lru_cache(maxsize=2048)
def native_allocation_helper_call_entries(method_text: str) -> tuple[tuple[str, int, str], ...]:
    if not any(token in method_text for token in ("Acquire", "Allocate", "Claim", "Create", "Ensure")):
        return ()

    helper_pattern = re.compile(csharp_helper_call_pattern())
    calls: list[tuple[str, int, str]] = []
    for match in helper_pattern.finditer(method_text):
        helper_name = match.group("helper")
        if not any(token in helper_name for token in ("Acquire", "Allocate", "Claim", "Create", "Ensure")):
            continue

        open_index = match.end() - 1
        close_index = find_matching_parenthesis(method_text, open_index)
        if close_index < 0:
            continue

        arguments = split_csharp_arguments(method_text[open_index + 1 : close_index])
        for argument_index, argument in enumerate(arguments):
            argument_name = csharp_argument_identifier(argument)
            if argument_name:
                calls.append((helper_name, argument_index, argument_name))

    return tuple(calls)


@lru_cache(maxsize=8192)
def native_allocation_helper_calls(method_text: str, variable_name: str) -> tuple[tuple[str, int], ...]:
    if variable_name not in method_text:
        return ()

    return tuple(
        (helper_name, argument_index)
        for helper_name, argument_index, argument_name in native_allocation_helper_call_entries(method_text)
        if argument_name == variable_name
    )


def source_has_h8memory_allocate_helper_argument(
    sanitized_source: str,
    helper_name: str,
    argument_index: int,
    visited_helper_keys: set[tuple[str, int]] | None = None,
) -> bool:
    if visited_helper_keys is None:
        visited_helper_keys = set()
    helper_key = (helper_name, argument_index)
    if helper_key in visited_helper_keys:
        return False
    visited_helper_keys.add(helper_key)

    for helper_text, parameter_names in source_method_definitions_named(sanitized_source, helper_name):
        if argument_index >= len(parameter_names):
            continue

        parameter_name = parameter_names[argument_index]
        if not parameter_name:
            continue

        direct_allocate_pattern = re.compile(
            csharp_reference_pattern(parameter_name)
            + r"\s*=\s*"
            + h8memory_owner_pattern()
            + r"\s*\.\s*Allocate\s*<"
        )
        if direct_allocate_pattern.search(helper_text):
            return True

        for nested_helper_name, nested_argument_index in native_allocation_helper_calls(helper_text, parameter_name):
            if source_has_h8memory_allocate_helper_argument(
                sanitized_source,
                nested_helper_name,
                nested_argument_index,
                visited_helper_keys,
            ):
                return True

    return False


def source_has_h8memory_release_helper_argument(
    sanitized_source: str,
    helper_name: str,
    argument_index: int,
    visited_helper_keys: set[tuple[str, int]] | None = None,
) -> bool:
    if visited_helper_keys is None:
        visited_helper_keys = set()
    helper_key = (helper_name, argument_index)
    if helper_key in visited_helper_keys:
        return False
    visited_helper_keys.add(helper_key)

    for helper_text, parameter_names in source_method_definitions_named(sanitized_source, helper_name):
        if argument_index >= len(parameter_names):
            continue

        parameter_name = parameter_names[argument_index]
        if not parameter_name:
            continue

        direct_release_pattern = re.compile(
            r"\b"
            + h8memory_owner_pattern()
            + r"\s*\.\s*Release\s*\(\s*ref\s+"
            + csharp_reference_pattern(parameter_name)
        )
        if direct_release_pattern.search(helper_text):
            return True

        for nested_helper_name, nested_argument_index in native_lifetime_helper_calls(helper_text, parameter_name):
            if source_has_h8memory_release_helper_argument(
                sanitized_source,
                nested_helper_name,
                nested_argument_index,
                visited_helper_keys,
            ):
                return True

    return False


def source_has_h8memory_allocate_factory_helper(sanitized_source: str, helper_name: str) -> bool:
    for helper_text in source_method_bodies_named(sanitized_source, helper_name):
        direct_allocate_pattern = re.compile(
            r"\b"
            + h8memory_owner_pattern()
            + r"\s*\.\s*Allocate\s*<"
        )
        if direct_allocate_pattern.search(helper_text):
            return True

    return False


@lru_cache(maxsize=1024)
def h8memory_direct_lifetime_name_sets(sanitized_source: str) -> tuple[frozenset[str], frozenset[str]]:
    owner = h8memory_owner_pattern()
    allocated: set[str] = set()
    released: set[str] = set()
    allocate_call_pattern = re.compile(r"\b" + owner + r"\s*\.\s*Allocate\s*<")
    release_call_pattern = re.compile(r"\b" + owner + r"\s*\.\s*Release\s*\(")
    assignment_lhs_pattern = re.compile(
        r"(?P<name>(?:[A-Za-z_]\w*\s*\.\s*)*[A-Za-z_]\w*)\s*=\s*$"
    )

    for match in allocate_call_pattern.finditer(sanitized_source):
        statement_start = max(
            sanitized_source.rfind(";", 0, match.start()),
            sanitized_source.rfind("{", 0, match.start()),
            sanitized_source.rfind("}", 0, match.start()),
        )
        prefix = sanitized_source[statement_start + 1 : match.start()]
        assignment_match = assignment_lhs_pattern.search(prefix)
        if assignment_match:
            allocated.add(csharp_argument_identifier(assignment_match.group("name")))

    for match in release_call_pattern.finditer(sanitized_source):
        open_index = match.end() - 1
        close_index = find_matching_parenthesis(sanitized_source, open_index)
        if close_index < 0:
            continue

        arguments = split_csharp_arguments(sanitized_source[open_index + 1 : close_index])
        if arguments:
            released.add(csharp_argument_identifier(arguments[0]))

    return frozenset(allocated), frozenset(released)


def source_has_h8memory_tracked_field_lifetime(
    sanitized_source: str,
    field_name: str,
) -> bool:
    if not source_has_h8memory_lifetime_tokens(sanitized_source):
        return False

    direct_allocated_names, direct_released_names = h8memory_direct_lifetime_name_sets(sanitized_source)
    if field_name in direct_allocated_names and field_name in direct_released_names:
        return True

    field_pattern = csharp_reference_pattern(field_name)
    has_allocate = field_name in direct_allocated_names
    if not has_allocate:
        factory_assignment_pattern = re.compile(
            field_pattern
            + r"\s*=\s*(?:[A-Za-z_]\w*\s*\.\s*)*(?P<helper>[A-Za-z_]\w*)\s*"
            + CSHARP_INLINE_GENERIC_ARGUMENTS_PATTERN
            + r"\s*\("
        )
        for match in factory_assignment_pattern.finditer(sanitized_source):
            if source_has_h8memory_allocate_factory_helper(
                sanitized_source,
                match.group("helper"),
            ):
                has_allocate = True
                break

    if not has_allocate:
        for helper_name, argument_index in native_allocation_helper_calls(sanitized_source, field_name):
            if source_has_h8memory_allocate_helper_argument(
                sanitized_source,
                helper_name,
                argument_index,
            ):
                has_allocate = True
                break

    if not has_allocate:
        if source_has_h8memory_constructor_alias_field_lifetime(
            sanitized_source,
            field_name,
        ):
            return True

    if not has_allocate:
        return False

    if field_name in direct_released_names:
        return True

    for helper_name, argument_index in native_lifetime_helper_calls(sanitized_source, field_name):
        if source_has_h8memory_release_helper_argument(
            sanitized_source,
            helper_name,
            argument_index,
        ):
            return True

    return False


def source_has_h8memory_constructor_alias_field_lifetime(
    sanitized_source: str,
    field_name: str,
) -> bool:
    if field_name not in sanitized_source or "=" not in sanitized_source:
        return False

    assignment_pattern = re.compile(
        r"\b" + re.escape(field_name) + r"\s*=\s*(?P<local>[A-Za-z_]\w*)\s*;"
    )
    for assignment_match in assignment_pattern.finditer(sanitized_source):
        local_name = assignment_match.group("local")
        if not source_local_is_h8memory_allocated(sanitized_source, local_name):
            continue
        if source_local_is_h8memory_released(sanitized_source, local_name):
            return True
        if source_field_alias_is_h8memory_released(sanitized_source, field_name, local_name):
            return True

    return False


@lru_cache(maxsize=4096)
def source_local_is_h8memory_allocated(sanitized_source: str, local_name: str) -> bool:
    if local_name not in sanitized_source:
        return False

    allocate_pattern = re.compile(
        csharp_reference_pattern(local_name)
        + r"\s*=\s*"
        + h8memory_owner_pattern()
        + r"\s*\.\s*Allocate\s*<"
    )
    return allocate_pattern.search(sanitized_source) is not None


@lru_cache(maxsize=4096)
def source_local_is_h8memory_released(sanitized_source: str, local_name: str) -> bool:
    release_pattern = re.compile(
        r"\b"
        + h8memory_owner_pattern()
        + r"\s*\.\s*Release\s*\(\s*ref\s+"
        + csharp_reference_pattern(local_name)
    )
    return release_pattern.search(sanitized_source) is not None


def source_field_alias_is_h8memory_released(
    sanitized_source: str,
    field_name: str,
    preferred_local_name: str,
) -> bool:
    if field_name not in sanitized_source:
        return False

    alias_pattern = re.compile(
        r"\bNativeArray\s*<[^>\r\n;=(){}]+>\s+(?P<local>[A-Za-z_]\w*)\s*=\s*"
        + csharp_reference_pattern(field_name)
        + r"\s*;"
    )
    for alias_match in alias_pattern.finditer(sanitized_source):
        local_name = alias_match.group("local")
        if local_name != preferred_local_name and local_name.lower() != preferred_local_name.lower():
            continue
        if source_local_is_h8memory_released(sanitized_source, local_name):
            return True

    return False


def sentinel_unregister_methods_for_collection(collection_type: str) -> tuple[str, ...]:
    if collection_type == "NativeArray":
        return ("UnregisterNativeArray",)
    if collection_type == "NativeList":
        return ("UnregisterNativeList",)
    if collection_type == "NativeHashMap":
        return ("UnregisterNativeHashMap",)
    if collection_type == "NativeParallelHashMap":
        return ("UnregisterNativeParallelHashMap",)

    return ()


def source_has_sentinel_registered_field_lifetime(
    sanitized_source: str,
    field_name: str,
    collection_type: str,
    raw_source: str | None = None,
) -> bool:
    raw_lookup_source = raw_source or sanitized_source
    if (
        not source_has_sentinel_registration_tokens(sanitized_source)
        and not source_has_native_tracking_bridge_register_bytes_tokens(sanitized_source)
        and not (
            "NativeMemorySentinel" in raw_lookup_source
            and "GetMethod" in raw_lookup_source
            and "Invoke" in raw_lookup_source
            and any(method_name in raw_lookup_source for method_name in sentinel_register_methods_for_collection(collection_type))
        )
    ):
        return False

    field_pattern = csharp_reference_pattern(field_name)
    allocate_pattern = re.compile(
        field_pattern
        + r"\s*=\s*new\s+"
        + re.escape(collection_type)
        + r"\s*<"
    )
    has_registered_allocation = allocate_pattern.search(sanitized_source) is not None
    factory_registered_assignment = False
    alias_registered_assignment = False
    if not has_registered_allocation:
        factory_registered_assignment = source_has_sentinel_registered_factory_assignment_for_field(
            sanitized_source,
            field_name,
            collection_type,
            raw_source,
        )
        has_registered_allocation = factory_registered_assignment

    if not has_registered_allocation:
        alias_registered_assignment = source_has_sentinel_registered_alias_assignment_for_field(
            sanitized_source,
            field_name,
            collection_type,
        )
        has_registered_allocation = alias_registered_assignment

    if not has_registered_allocation:
        return False

    if not source_has_sentinel_registration_for_field(sanitized_source, field_name, collection_type, raw_source):
        if not factory_registered_assignment and not alias_registered_assignment:
            return False

    return source_has_sentinel_unregistration_for_field(sanitized_source, field_name, collection_type, raw_source)


def source_has_sentinel_registered_alias_assignment_for_field(
    sanitized_source: str,
    field_name: str,
    collection_type: str,
) -> bool:
    if field_name not in sanitized_source:
        return False

    assignment_pattern = re.compile(
        csharp_reference_pattern(field_name)
        + r"\s*=\s*(?P<local>[A-Za-z_]\w*)\s*;"
    )
    for assignment_match in assignment_pattern.finditer(sanitized_source):
        local_name = assignment_match.group("local")
        if source_local_is_sentinel_registered(sanitized_source, local_name, collection_type):
            return True

    return False


def source_has_native_tracking_register_bytes_for_parameter(method_text: str, parameter_name: str) -> bool:
    if not source_has_native_tracking_bridge_register_bytes_tokens(method_text):
        return False

    register_bytes_pattern = re.compile(
        r"\b(?:Hecton8\s*\.\s*Core\s*\.\s*Contracts\s*\.)?"
        r"NativeMemoryTrackingBridge\s*\.\s*RegisterBytes(?:Instance)?\s*\(",
    )
    return (
        register_bytes_pattern.search(method_text) is not None
        and re.search(csharp_reference_pattern(parameter_name), method_text) is not None
    )


def source_has_native_tracking_bridge_sidecar_field_lifetime(
    sanitized_source: str,
    field_name: str,
    collection_type: str,
) -> bool:
    if (
        not source_has_native_tracking_bridge_lifetime_tokens(sanitized_source)
        or field_name not in sanitized_source
    ):
        return False

    if not source_has_native_collection_assignment_for_field(
        sanitized_source,
        field_name,
        collection_type,
    ):
        return False

    if not source_has_native_tracking_bridge_sidecar_registration_for_field(
        sanitized_source,
        field_name,
        collection_type,
    ):
        return False

    return source_has_native_tracking_bridge_sidecar_unregistration_for_field(
        sanitized_source,
        field_name,
    )


def source_has_native_collection_assignment_for_field(
    sanitized_source: str,
    field_name: str,
    collection_type: str,
) -> bool:
    field_pattern = csharp_reference_pattern(field_name)
    direct_assignment_pattern = re.compile(
        field_pattern
        + r"\s*=\s*new\s+"
        + re.escape(collection_type)
        + r"\s*<"
    )
    if direct_assignment_pattern.search(sanitized_source):
        return True

    return source_has_sentinel_registered_factory_assignment_for_field(
        sanitized_source,
        field_name,
        collection_type,
    )


def source_has_native_tracking_bridge_sidecar_registration_for_field(
    sanitized_source: str,
    field_name: str,
    collection_type: str,
) -> bool:
    sentinel_field_name = field_name + "SentinelId"
    if sentinel_field_name not in sanitized_source:
        return False

    has_registration_call = False
    for helper_name, argument_index in register_native_collection_helper_calls(
        sanitized_source,
        field_name,
        collection_type,
    ):
        if source_has_sentinel_register_helper_argument(
            sanitized_source,
            helper_name,
            argument_index,
            collection_type,
        ):
            has_registration_call = True
            break

    if not has_registration_call:
        for bulk_method_name in bulk_sentinel_registration_method_names(sanitized_source):
            if source_has_bulk_sentinel_registration_for_field(
                sanitized_source,
                bulk_method_name,
                field_name,
                collection_type,
            ):
                has_registration_call = True
                break

    if not has_registration_call:
        return False

    assignment_pattern = re.compile(
        csharp_reference_pattern(sentinel_field_name)
        + r"\s*=\s*(?P<body>.*?);",
        re.DOTALL,
    )
    for match in assignment_pattern.finditer(sanitized_source):
        body = match.group("body")
        if "Register" in body and re.search(csharp_reference_pattern(field_name), body):
            return True

    return False


def source_has_native_tracking_bridge_sidecar_unregistration_for_field(
    sanitized_source: str,
    field_name: str,
) -> bool:
    sentinel_field_name = field_name + "SentinelId"
    if sentinel_field_name not in sanitized_source:
        return False

    direct_unregister_pattern = re.compile(
        r"\b(?:Hecton8\s*\.\s*Core\s*\.\s*Contracts\s*\.)?"
        r"NativeMemoryTrackingBridge\s*\.\s*Unregister\s*\(\s*"
        + csharp_reference_pattern(sentinel_field_name)
    )
    if direct_unregister_pattern.search(sanitized_source):
        return True

    for helper_name, argument_index in native_lifetime_helper_calls(sanitized_source, sentinel_field_name):
        if source_has_native_tracking_bridge_unregister_helper_argument(
            sanitized_source,
            helper_name,
            argument_index,
        ):
            return True

    return False


def source_has_native_tracking_bridge_unregister_helper_argument(
    sanitized_source: str,
    helper_name: str,
    argument_index: int,
    visited_helper_keys: set[tuple[str, int]] | None = None,
) -> bool:
    if visited_helper_keys is None:
        visited_helper_keys = set()
    helper_key = (helper_name, argument_index)
    if helper_key in visited_helper_keys:
        return False
    visited_helper_keys.add(helper_key)

    for helper_text, parameter_names in source_method_definitions_named(sanitized_source, helper_name):
        if argument_index >= len(parameter_names):
            continue

        parameter_name = parameter_names[argument_index]
        if not parameter_name:
            continue

        direct_unregister_pattern = re.compile(
            r"\b(?:Hecton8\s*\.\s*Core\s*\.\s*Contracts\s*\.)?"
            r"NativeMemoryTrackingBridge\s*\.\s*Unregister\s*\(\s*"
            + csharp_reference_pattern(parameter_name)
        )
        if direct_unregister_pattern.search(helper_text):
            return True

        for nested_helper_name, nested_argument_index in native_lifetime_helper_calls(helper_text, parameter_name):
            if source_has_native_tracking_bridge_unregister_helper_argument(
                sanitized_source,
                nested_helper_name,
                nested_argument_index,
                visited_helper_keys,
            ):
                return True

    return False


def source_local_is_sentinel_registered(
    sanitized_source: str,
    local_name: str,
    collection_type: str,
) -> bool:
    if local_name not in sanitized_source:
        return False

    register_methods = sentinel_register_methods_for_collection(collection_type)
    if not register_methods:
        return False

    direct_methods_pattern = "|".join(re.escape(method) for method in register_methods)
    direct_register_pattern = re.compile(
        r"\bNativeMemorySentinel\s*\.\s*(?:"
        + direct_methods_pattern
        + r")\s*\([^;\n]*"
        + csharp_reference_pattern(local_name)
    )
    if direct_register_pattern.search(sanitized_source):
        return True

    for helper_name, argument_index in register_native_collection_helper_calls(
        sanitized_source,
        local_name,
        collection_type,
    ):
        if source_has_sentinel_register_helper_argument(
            sanitized_source,
            helper_name,
            argument_index,
            collection_type,
        ):
            return True

    return False


def source_has_sentinel_registered_factory_assignment_for_field(
    sanitized_source: str,
    field_name: str,
    collection_type: str,
    raw_source: str | None = None,
) -> bool:
    if field_name not in sanitized_source:
        return False

    field_pattern = csharp_reference_pattern(field_name)
    factory_assignment_pattern = re.compile(
        field_pattern
        + r"\s*=\s*(?:[A-Za-z_]\w*\s*\.\s*)*(?P<helper>[A-Za-z_]\w*)\s*"
        + CSHARP_INLINE_GENERIC_ARGUMENTS_PATTERN
        + r"\s*\("
    )
    for match in factory_assignment_pattern.finditer(sanitized_source):
        helper_name = match.group("helper")
        if source_has_sentinel_registered_factory_helper(
            sanitized_source,
            helper_name,
            collection_type,
            raw_source,
        ):
            return True

    return False


def source_has_sentinel_registered_factory_helper(
    sanitized_source: str,
    helper_name: str,
    collection_type: str,
    raw_source: str | None = None,
) -> bool:
    collection_constructor_pattern = re.compile(
        r"\bnew\s+" + re.escape(collection_type) + r"\s*<"
    )
    constructed_local_pattern = re.compile(
        r"(?:\b"
        + re.escape(collection_type)
        + r"\s*<[^>\r\n;=(){}]+>\s+)?(?P<local>[A-Za-z_]\w*)\s*=\s*new\s+"
        + re.escape(collection_type)
        + r"\s*<"
    )
    for helper_text in source_method_bodies_named(sanitized_source, helper_name):
        if not collection_constructor_pattern.search(helper_text):
            continue
        if source_has_sentinel_register_helper(sanitized_source, helper_name, collection_type):
            return True
        for local_match in constructed_local_pattern.finditer(helper_text):
            local_name = local_match.group("local")
            for nested_helper_name, argument_index in register_native_collection_helper_calls(
                helper_text,
                local_name,
                collection_type,
            ):
                if source_has_sentinel_register_helper_argument(
                    sanitized_source,
                    nested_helper_name,
                    argument_index,
                    collection_type,
                    raw_source,
                ):
                    return True

    return False


def source_has_sentinel_registration_for_field(
    sanitized_source: str,
    field_name: str,
    collection_type: str,
    raw_source: str | None = None,
) -> bool:
    escaped_field = re.escape(field_name)
    register_methods = sentinel_register_methods_for_collection(collection_type)
    if not register_methods:
        return False

    direct_methods_pattern = "|".join(re.escape(method) for method in register_methods)
    direct_register_pattern = re.compile(
        r"\bNativeMemorySentinel\s*\.\s*(?:"
        + direct_methods_pattern
        + r")\s*\(\s*"
        + escaped_field
        + r"\b"
    )
    if direct_register_pattern.search(sanitized_source):
        return True

    for helper_name, argument_index in register_native_collection_helper_calls(
        sanitized_source,
        field_name,
        collection_type,
    ):
        if source_has_sentinel_register_helper(sanitized_source, helper_name, collection_type):
            return True
        if source_has_sentinel_register_helper_argument(
            sanitized_source,
            helper_name,
            argument_index,
            collection_type,
            raw_source,
        ):
            return True

    for bulk_method_name in bulk_sentinel_registration_method_names(sanitized_source):
        if source_has_bulk_sentinel_registration_for_field(
            sanitized_source,
            bulk_method_name,
            field_name,
            collection_type,
        ):
            return True

    return False


def source_has_sentinel_unregistration_for_field(
    sanitized_source: str,
    field_name: str,
    collection_type: str,
    raw_source: str | None = None,
) -> bool:
    unregister_methods = sentinel_unregister_methods_for_collection(collection_type)
    if not unregister_methods:
        return False

    method_pattern = "|".join(re.escape(method) for method in unregister_methods)
    field_argument_pattern = csharp_reference_pattern(field_name)
    label_argument_pattern = r"nameof\s*\(\s*" + re.escape(field_name) + r"\s*\)"
    direct_unregister_pattern = re.compile(
        r"\bNativeMemorySentinel\s*\.\s*(?:"
        + method_pattern
        + r")\s*\([^;\n]*(?:"
        + field_argument_pattern
        + r"|"
        + label_argument_pattern
        + r")"
    )
    if direct_unregister_pattern.search(sanitized_source):
        return True

    if source_has_sentinel_unregistration_alias_for_field(
        sanitized_source,
        field_name,
        collection_type,
        raw_source,
    ):
        return True

    for helper_name, argument_index in native_lifetime_helper_calls(sanitized_source, field_name):
        if source_has_sentinel_unregister_helper_argument(
            sanitized_source,
            helper_name,
            argument_index,
            collection_type,
            raw_source,
        ):
            return True

    label_reference_pattern = re.compile(r"\bnameof\s*\(\s*" + re.escape(field_name) + r"\s*\)")
    if label_reference_pattern.search(sanitized_source):
        for helper_name, field_argument_index, label_argument_index in native_lifetime_helper_label_calls(
            sanitized_source,
            field_name,
        ):
            if source_has_sentinel_label_unregister_helper_argument(
                sanitized_source,
                helper_name,
                field_argument_index,
                label_argument_index,
                collection_type,
            ):
                return True

    return False


def source_has_sentinel_unregistration_alias_for_field(
    sanitized_source: str,
    field_name: str,
    collection_type: str,
    raw_source: str | None = None,
) -> bool:
    for local_alias in csharp_local_aliases_for_parameter(sanitized_source, field_name):
        unregister_methods = sentinel_unregister_methods_for_collection(collection_type)
        if unregister_methods:
            method_pattern = "|".join(re.escape(method) for method in unregister_methods)
            direct_alias_unregister_pattern = re.compile(
                r"\bNativeMemorySentinel\s*\.\s*(?:"
                + method_pattern
                + r")\s*\([^;\n]*"
                + csharp_reference_pattern(local_alias)
            )
            if direct_alias_unregister_pattern.search(sanitized_source):
                return True

        for helper_name, argument_index in native_lifetime_helper_calls(sanitized_source, local_alias):
            if source_has_sentinel_unregister_helper_argument(
                sanitized_source,
                helper_name,
                argument_index,
                collection_type,
                raw_source,
            ):
                return True

    return False


def source_has_sentinel_id_backed_struct_field_lifetime(
    sanitized_source: str,
    field_name: str,
    collection_type: str,
) -> bool:
    sentinel_field_name = field_name + "SentinelId"
    if sentinel_field_name not in sanitized_source:
        return False

    register_methods = sentinel_register_methods_for_collection(collection_type)
    if not register_methods:
        return False

    register_method_pattern = "|".join(re.escape(method) for method in register_methods)
    registration_assignment_pattern = re.compile(
        r"\b(?P<registration>[A-Za-z_]\w*)\s*=\s*NativeMemorySentinel\s*\.\s*(?:"
        + register_method_pattern
        + r")\s*\("
    )
    registration_names = {
        match.group("registration")
        for match in registration_assignment_pattern.finditer(sanitized_source)
    }
    if not registration_names:
        registration_names = set()

    sentinel_field_assignment_found = sentinel_field_name in registration_names
    for registration_name in registration_names:
        assignment_pattern = re.compile(
            r"\b"
            + re.escape(sentinel_field_name)
            + r"\s*=\s*"
            + re.escape(registration_name)
            + r"\b"
        )
        if assignment_pattern.search(sanitized_source):
            sentinel_field_assignment_found = True
            break

    if not sentinel_field_assignment_found:
        sentinel_field_assignment_found = source_has_sentinel_id_register_helper_call(
            sanitized_source,
            field_name,
            sentinel_field_name,
            collection_type,
        )
    if not sentinel_field_assignment_found:
        sentinel_field_assignment_found = source_has_sentinel_id_assignment_from_registered_factory(
            sanitized_source,
            field_name,
            sentinel_field_name,
            collection_type,
        )
    if not sentinel_field_assignment_found:
        return False

    direct_unregister_pattern = re.compile(
        r"\bNativeMemorySentinel\s*\.\s*Unregister\s*\(\s*"
        + csharp_reference_pattern(sentinel_field_name)
    )
    if direct_unregister_pattern.search(sanitized_source):
        return True

    ref_dispose_pattern = re.compile(
        r"\b[A-Za-z_]\w*\s*"
        + CSHARP_INLINE_GENERIC_ARGUMENTS_PATTERN
        + r"\s*\([^;{}]*\bref\s+"
        + csharp_reference_pattern(field_name)
        + r"[^;{}]*\bref\s+"
        + csharp_reference_pattern(sentinel_field_name),
        re.DOTALL,
    )
    helper_unregisters_id = re.search(
        r"\bNativeMemorySentinel\s*\.\s*Unregister\s*\(\s*[A-Za-z_]\w*\s*\)",
        sanitized_source,
    ) is not None
    return helper_unregisters_id and ref_dispose_pattern.search(sanitized_source) is not None


def source_has_sentinel_id_register_helper_call(
    sanitized_source: str,
    field_name: str,
    sentinel_field_name: str,
    collection_type: str,
) -> bool:
    helper_name_fragments = helper_name_fragments_for_collection(collection_type)
    helper_call_pattern = re.compile(csharp_helper_call_pattern())
    label_argument_pattern = re.compile(r"\bnameof\s*\(\s*" + re.escape(field_name) + r"\s*\)")
    for match in helper_call_pattern.finditer(sanitized_source):
        helper_name = match.group("helper")
        if "Register" not in helper_name or not any(fragment in helper_name for fragment in helper_name_fragments):
            continue

        open_index = match.end() - 1
        close_index = find_matching_parenthesis(sanitized_source, open_index)
        if close_index < 0:
            continue

        arguments = split_csharp_arguments(sanitized_source[open_index + 1 : close_index])
        has_field_argument = False
        has_sentinel_out_argument = False
        for argument in arguments:
            argument_identifier = csharp_argument_identifier(argument)
            if argument_identifier == field_name:
                has_field_argument = True
            if "out" in argument and argument_identifier == sentinel_field_name:
                has_sentinel_out_argument = True
            if label_argument_pattern.search(argument):
                has_field_argument = True

        if (
            has_field_argument
            and has_sentinel_out_argument
            and source_has_sentinel_register_helper(sanitized_source, helper_name, collection_type)
        ):
            return True

    return False


def source_has_sentinel_id_assignment_from_registered_factory(
    sanitized_source: str,
    field_name: str,
    sentinel_field_name: str,
    collection_type: str,
) -> bool:
    if field_name not in sanitized_source or sentinel_field_name not in sanitized_source:
        return False

    field_pattern = csharp_reference_pattern(field_name)
    sentinel_field_pattern = csharp_reference_pattern(sentinel_field_name)
    array_local_names = {
        match.group("local")
        for match in re.finditer(
            field_pattern + r"\s*=\s*(?P<local>[A-Za-z_]\w*)\b",
            sanitized_source,
        )
    }
    sentinel_local_names = {
        match.group("local")
        for match in re.finditer(
            sentinel_field_pattern + r"\s*=\s*(?P<local>[A-Za-z_]\w*)\b",
            sanitized_source,
        )
    }
    if not array_local_names or not sentinel_local_names:
        return False

    factory_assignment_pattern = re.compile(
        r"\b(?P<array>[A-Za-z_]\w*)\s*=\s*(?:[A-Za-z_]\w*\s*\.\s*)*(?P<helper>[A-Za-z_]\w*)\s*"
        + CSHARP_INLINE_GENERIC_ARGUMENTS_PATTERN
        + r"\s*\("
    )
    for match in factory_assignment_pattern.finditer(sanitized_source):
        array_name = match.group("array")
        if array_name not in array_local_names:
            continue

        close_index = find_matching_parenthesis(sanitized_source, match.end() - 1)
        if close_index < 0:
            continue

        arguments = split_csharp_arguments(sanitized_source[match.end() : close_index])
        has_sentinel_out = any(
            "out" in argument and csharp_argument_identifier(argument) in sentinel_local_names
            for argument in arguments
        )
        if not has_sentinel_out:
            continue

        if source_has_sentinel_registered_factory_helper(
            sanitized_source,
            match.group("helper"),
            collection_type,
        ):
            return True

    return False


def source_has_sentinel_unregister_helper_argument(
    sanitized_source: str,
    helper_name: str,
    argument_index: int,
    collection_type: str,
    raw_source: str | None = None,
    visited_helper_keys: set[tuple[str, int, str]] | None = None,
) -> bool:
    if visited_helper_keys is None:
        visited_helper_keys = set()
    helper_key = (helper_name, argument_index, collection_type)
    if helper_key in visited_helper_keys:
        return False
    visited_helper_keys.add(helper_key)

    unregister_methods = sentinel_unregister_methods_for_collection(collection_type)
    if not unregister_methods:
        return False

    method_pattern = "|".join(re.escape(method) for method in unregister_methods)
    for helper_text, parameter_names in source_method_definitions_named(sanitized_source, helper_name):
        if argument_index >= len(parameter_names):
            continue

        parameter_name = parameter_names[argument_index]
        if not parameter_name:
            continue

        direct_unregister_pattern = re.compile(
            r"\bNativeMemorySentinel\s*\.\s*(?:"
            + method_pattern
            + r")\s*\([^;\n]*"
            + csharp_reference_pattern(parameter_name)
        )
        if direct_unregister_pattern.search(helper_text):
            return True

        if source_has_sentinel_unregister_alias_for_parameter(
            helper_text,
            parameter_name,
            collection_type,
        ):
            return True

        if source_has_sentinel_owner_label_unregister_helper_for_parameter(
            helper_text,
            parameter_name,
            collection_type,
        ):
            return True

        if source_has_sentinel_id_unregister_helper_for_parameter(
            helper_text,
            parameter_names,
            argument_index,
        ):
            return True

        if raw_source is not None:
            for raw_helper_text, raw_parameter_names in source_method_definitions_named(raw_source, helper_name):
                if argument_index >= len(raw_parameter_names):
                    continue

                raw_parameter_name = raw_parameter_names[argument_index]
                if not raw_parameter_name:
                    continue

                if source_has_reflective_sentinel_unregister_for_parameter(
                    raw_helper_text,
                    raw_parameter_name,
                    collection_type,
                ):
                    return True

        for local_alias in csharp_local_aliases_for_parameter(helper_text, parameter_name):
            for nested_helper_name, nested_argument_index in native_lifetime_helper_calls(helper_text, local_alias):
                if source_has_sentinel_unregister_helper_argument(
                    sanitized_source,
                    nested_helper_name,
                    nested_argument_index,
                    collection_type,
                    raw_source,
                    visited_helper_keys,
                ):
                    return True

        for nested_helper_name, nested_argument_index in native_lifetime_helper_calls(helper_text, parameter_name):
            if source_has_sentinel_unregister_helper_argument(
                sanitized_source,
                nested_helper_name,
                nested_argument_index,
                collection_type,
                raw_source,
                visited_helper_keys,
            ):
                return True

    return False


def source_has_sentinel_id_unregister_helper_for_parameter(
    helper_text: str,
    parameter_names: Sequence[str],
    collection_parameter_index: int,
) -> bool:
    if (
        collection_parameter_index >= len(parameter_names)
        or "NativeMemorySentinel" not in helper_text
        or "Unregister" not in helper_text
    ):
        return False

    collection_parameter_name = parameter_names[collection_parameter_index]
    if not collection_parameter_name:
        return False

    collection_parameter_pattern = csharp_reference_pattern(collection_parameter_name)
    dispose_pattern = re.compile(collection_parameter_pattern + r"\s*\.\s*Dispose\s*\(")
    default_pattern = re.compile(collection_parameter_pattern + r"\s*=\s*default\b")
    if dispose_pattern.search(helper_text) is None and default_pattern.search(helper_text) is None:
        return False

    for parameter_index, parameter_name in enumerate(parameter_names):
        if parameter_index == collection_parameter_index or not parameter_name:
            continue

        unregister_pattern = re.compile(
            r"\bNativeMemorySentinel\s*\.\s*Unregister\s*\(\s*"
            + csharp_reference_pattern(parameter_name)
        )
        if unregister_pattern.search(helper_text):
            return True

    return False


def source_has_sentinel_owner_label_unregister_helper_for_parameter(
    helper_text: str,
    parameter_name: str,
    collection_type: str,
) -> bool:
    unregister_methods = sentinel_unregister_methods_for_collection(collection_type)
    if not unregister_methods:
        return False

    parameter_pattern = csharp_reference_pattern(parameter_name)
    dispose_pattern = re.compile(parameter_pattern + r"\s*\.\s*Dispose\s*\(")
    if dispose_pattern.search(helper_text) is None:
        return False

    method_pattern = "|".join(re.escape(method) for method in unregister_methods)
    unregister_pattern = re.compile(
        r"\bNativeMemorySentinel\s*\.\s*(?:"
        + method_pattern
        + r")\s*\("
    )
    return unregister_pattern.search(helper_text) is not None


def source_has_sentinel_unregister_alias_for_parameter(
    helper_text: str,
    parameter_name: str,
    collection_type: str,
) -> bool:
    unregister_methods = sentinel_unregister_methods_for_collection(collection_type)
    if not unregister_methods:
        return False

    method_pattern = "|".join(re.escape(method) for method in unregister_methods)
    for local_name in csharp_local_aliases_for_parameter(helper_text, parameter_name):
        alias_unregister_pattern = re.compile(
            r"\bNativeMemorySentinel\s*\.\s*(?:"
            + method_pattern
            + r")\s*\([^;\n]*"
            + csharp_reference_pattern(local_name)
        )
        if alias_unregister_pattern.search(helper_text):
            return True

    return False


@lru_cache(maxsize=8192)
def csharp_local_aliases_for_parameter(helper_text: str, parameter_name: str) -> tuple[str, ...]:
    parameter_pattern = csharp_reference_pattern(parameter_name)
    alias_assignment_pattern = re.compile(
        r"\b(?:[A-Za-z_][A-Za-z0-9_<>,\s\[\]\.?]*\s+)?"
        r"(?P<local>[A-Za-z_]\w*)\s*=\s*"
        + parameter_pattern
        + r"\s*;"
    )
    aliases: list[str] = []
    for alias_match in alias_assignment_pattern.finditer(helper_text):
        local_name = alias_match.group("local")
        if local_name == parameter_name:
            continue
        aliases.append(local_name)

    return tuple(aliases)


def source_has_sentinel_label_unregister_helper_argument(
    sanitized_source: str,
    helper_name: str,
    field_argument_index: int,
    label_argument_index: int,
    collection_type: str,
) -> bool:
    unregister_methods = sentinel_unregister_methods_for_collection(collection_type)
    if not unregister_methods:
        return False

    method_pattern = "|".join(re.escape(method) for method in unregister_methods)
    for helper_text, parameter_names in source_method_definitions_named(sanitized_source, helper_name):
        if field_argument_index >= len(parameter_names) or label_argument_index >= len(parameter_names):
            continue

        collection_parameter_name = parameter_names[field_argument_index]
        label_parameter_name = parameter_names[label_argument_index]
        if not collection_parameter_name or not label_parameter_name:
            continue

        label_unregister_pattern = re.compile(
            r"\bNativeMemorySentinel\s*\.\s*(?:"
            + method_pattern
            + r")\s*\([^;\n]*"
            + csharp_reference_pattern(label_parameter_name)
        )
        dispose_pattern = re.compile(
            csharp_reference_pattern(collection_parameter_name)
            + r"\s*\.\s*Dispose\s*\("
        )
        if label_unregister_pattern.search(helper_text) and dispose_pattern.search(helper_text):
            return True

    return False


@lru_cache(maxsize=8192)
def data_vault_alias_helper_calls(method_text: str, field_name: str) -> tuple[tuple[str, int], ...]:
    helper_pattern = re.compile(csharp_helper_call_pattern())
    calls: list[tuple[str, int]] = []
    for match in helper_pattern.finditer(method_text):
        helper_name = match.group("helper")
        if not any(token in helper_name for token in ("Acquire", "Open", "Read", "Resolve", "Vault", "View", "Buffer")):
            continue

        open_index = match.end() - 1
        close_index = find_matching_parenthesis(method_text, open_index)
        if close_index < 0:
            continue

        arguments = split_csharp_arguments(method_text[open_index + 1 : close_index])
        for argument_index, argument in enumerate(arguments):
            if not re.search(r"\b(out|ref)\b", argument):
                continue
            if csharp_argument_identifier(argument) == field_name:
                calls.append((helper_name, argument_index))
                break

    return tuple(calls)


def source_has_datavault_alias_helper_argument(
    sanitized_source: str,
    helper_name: str,
    argument_index: int,
    visited_helper_keys: set[tuple[str, int]] | None = None,
) -> bool:
    if visited_helper_keys is None:
        visited_helper_keys = set()
    helper_key = (helper_name, argument_index)
    if helper_key in visited_helper_keys:
        return False
    visited_helper_keys.add(helper_key)

    for helper_text, parameter_names in source_method_definitions_named(sanitized_source, helper_name):
        if argument_index >= len(parameter_names):
            continue

        parameter_name = parameter_names[argument_index]
        if not parameter_name:
            continue

        datavault_signal = (
            "EnsureGenerationHandle" in helper_text
            or "TryResolveHandle" in helper_text
            or "TryAcquireWriteLock" in helper_text
            or "TryReadHandle" in helper_text
            or "TryReadOnlyHandle" in helper_text
        )
        parameter_receives_alias = (
            re.search(r"\bout\s+" + csharp_reference_pattern(parameter_name), helper_text) is not None
            or re.search(csharp_reference_pattern(parameter_name) + r"\s*=", helper_text) is not None
        )
        if datavault_signal and parameter_receives_alias:
            return True

        for nested_helper_name, nested_argument_index in data_vault_alias_helper_calls(helper_text, parameter_name):
            if source_has_datavault_alias_helper_argument(
                sanitized_source,
                nested_helper_name,
                nested_argument_index,
                visited_helper_keys,
            ):
                return True

    return False


def source_has_datavault_alias_field_lifetime(
    sanitized_source: str,
    field_name: str,
    owner_name: str = "",
) -> bool:
    if (
        "EnsureGenerationHandle" not in sanitized_source
        and "TryResolveHandle" not in sanitized_source
        and "TryAcquireWriteLock" not in sanitized_source
        and "TryReadHandle" not in sanitized_source
        and "TryReadOnlyHandle" not in sanitized_source
    ):
        return False

    direct_alias_pattern = re.compile(
        r"\b(?:TryResolveHandle|TryAcquireWriteLock|TryReadHandle|TryReadOnlyHandle)\s*\([^;{}]*\bout\s+"
        + csharp_reference_pattern(field_name),
        re.DOTALL,
    )
    if direct_alias_pattern.search(sanitized_source):
        return True

    if source_has_datavault_context_field_alias_assignment(sanitized_source, field_name):
        return True

    if owner_name and source_has_datavault_parameter_field_alias_assignment(sanitized_source, field_name, owner_name):
        return True

    for helper_name, argument_index in data_vault_alias_helper_calls(sanitized_source, field_name):
        if source_has_datavault_alias_helper_argument(
            sanitized_source,
            helper_name,
            argument_index,
        ):
            return True

    return False


def source_has_datavault_context_field_alias_assignment(
    sanitized_source: str,
    field_name: str,
) -> bool:
    if field_name not in sanitized_source:
        return False

    assignment_pattern = re.compile(
        r"\b[A-Za-z_]\w*\s*\.\s*"
        + re.escape(field_name)
        + r"\s*=\s*(?P<local>[A-Za-z_]\w*)\s*;"
    )
    for assignment_match in assignment_pattern.finditer(sanitized_source):
        local_name = assignment_match.group("local")
        if source_has_datavault_resolved_local_alias(sanitized_source, local_name):
            return True

    return False


def source_has_datavault_parameter_field_alias_assignment(
    sanitized_source: str,
    field_name: str,
    owner_name: str,
) -> bool:
    for callable_name, argument_index in source_field_parameter_assignment_call_targets(
        sanitized_source,
        field_name,
        owner_name,
    ):
        if source_call_argument_is_datavault_alias(
            sanitized_source,
            callable_name,
            argument_index,
            constructor=callable_name == owner_name,
        ):
            return True

    return False


@lru_cache(maxsize=4096)
def source_field_parameter_assignment_call_targets(
    sanitized_source: str,
    field_name: str,
    owner_name: str,
) -> tuple[tuple[str, int], ...]:
    assignment_pattern = re.compile(
        r"(?:\bthis\s*\.\s*)?"
        + csharp_reference_pattern(field_name)
        + r"\s*=\s*(?P<parameter>[A-Za-z_]\w*)\s*;"
    )
    lines = sanitized_source.splitlines()
    targets: list[tuple[str, int]] = []
    for assignment_match in assignment_pattern.finditer(sanitized_source):
        parameter_name = assignment_match.group("parameter")
        line_index = sanitized_source[: assignment_match.start()].count("\n")
        header = csharp_method_signature_header_for_line(lines, line_index)
        if not header:
            continue
        open_index = header.rfind("(")
        close_index = header.rfind(")")
        if open_index < 0 or close_index < open_index:
            continue

        method_name_candidates = re.findall(r"[A-Za-z_]\w*", header[:open_index])
        if not method_name_candidates:
            continue

        callable_name = method_name_candidates[-1]
        if callable_name != owner_name and callable_name in {"if", "for", "foreach", "while", "switch", "using", "lock"}:
            continue

        parameter_names = [
            csharp_parameter_name(parameter)
            for parameter in split_csharp_arguments(header[open_index + 1 : close_index])
        ]
        for argument_index, candidate_name in enumerate(parameter_names):
            if candidate_name == parameter_name:
                targets.append((callable_name, argument_index))
                break

    return tuple(targets)


@lru_cache(maxsize=4096)
def source_call_argument_is_datavault_alias(
    sanitized_source: str,
    callable_name: str,
    argument_index: int,
    constructor: bool = False,
) -> bool:
    if constructor:
        call_pattern = re.compile(
            r"\bnew\s+"
            + re.escape(callable_name)
            + CSHARP_INLINE_GENERIC_ARGUMENTS_PATTERN
            + r"\s*\("
        )
    else:
        call_pattern = re.compile(
            r"\b(?:[A-Za-z_]\w*\s*\.\s*)?"
            + re.escape(callable_name)
            + CSHARP_INLINE_GENERIC_ARGUMENTS_PATTERN
            + r"\s*\("
        )

    for call_match in call_pattern.finditer(sanitized_source):
        open_index = call_match.end() - 1
        close_index = find_matching_parenthesis(sanitized_source, open_index)
        if close_index < 0:
            continue

        arguments = split_csharp_arguments(sanitized_source[open_index + 1 : close_index])
        if argument_index >= len(arguments):
            continue

        argument_name = csharp_argument_identifier(arguments[argument_index])
        if not argument_name:
            continue

        if source_has_datavault_resolved_local_alias(sanitized_source, argument_name):
            return True

    return False


@lru_cache(maxsize=8192)
def source_has_datavault_resolved_local_alias(sanitized_source: str, local_name: str) -> bool:
    if local_name not in sanitized_source:
        return False

    local_pattern = csharp_reference_pattern(local_name)
    resolve_pattern = re.compile(
        r"\b(?:TryResolve|TryResolveHandle|TryAcquireWriteLock|TryReadHandle|TryReadOnlyHandle)\s*\([^;{}]*\bout\s+"
        + r"(?:[A-Za-z_][A-Za-z0-9_<>,\s\[\]\.?]*\s+)?"
        + local_pattern,
        re.DOTALL,
    )
    if resolve_pattern.search(sanitized_source):
        return True

    for helper_name, argument_index in data_vault_alias_helper_calls(sanitized_source, local_name):
        if source_has_datavault_alias_helper_argument(
            sanitized_source,
            helper_name,
            argument_index,
        ):
            return True

    return False


def is_sentinel_tracked_constructor_wrapper(source: str, lines: Sequence[str], line_index: int) -> bool:
    sanitized_source = "\n".join(lines)
    source_mentions_native_tracking = (
        source_has_sentinel_registration_tokens(sanitized_source)
        or ("NativeMemoryTrackingBridge" in sanitized_source and "Register" in sanitized_source)
        or (
            "NativeMemorySentinel" in source
            and any(
                method_name in source
                for methods in SENTINEL_REGISTER_METHODS_BY_COLLECTION.values()
                for method_name in methods
            )
        )
    )
    if not source_mentions_native_tracking:
        return False

    variable_name = native_array_constructor_assignment_name(lines, line_index)
    if not variable_name:
        return False

    collection_type = native_collection_constructor_type(lines, line_index)
    direct_register_methods = sentinel_register_methods_for_collection(collection_type)
    if not direct_register_methods:
        return False

    start, end = csharp_method_bounds_for_line(lines, line_index)
    method_text = "\n".join(lines[start : end + 1])
    direct_methods_pattern = "|".join(re.escape(method) for method in direct_register_methods)
    direct_register_pattern = re.compile(
        r"\b"
        + native_tracking_register_owner_pattern()
        + r"\s*\.\s*(?:"
        + direct_methods_pattern
        + r")\s*\(\s*"
        + csharp_reference_pattern(variable_name)
    )
    if direct_register_pattern.search(method_text):
        return True

    for helper_name, argument_index in register_native_collection_helper_calls(method_text, variable_name, collection_type):
        if source_has_sentinel_register_helper_argument(
            sanitized_source,
            helper_name,
            argument_index,
            collection_type,
            source,
        ):
            return True

    for bulk_method_name in bulk_sentinel_registration_method_names(method_text):
        if source_has_bulk_sentinel_registration_for_field(
                sanitized_source,
                bulk_method_name,
                variable_name,
                collection_type,
                source):
            return True

    return False


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
    if allocator == "SentinelTracked":
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
    transient_editor_scratch_lines: list[int] = []
    transient_editor_scratch_allocators: list[str] = []
    transient_editor_scratch_surfaces: list[str] = []
    sentinel_tracked_lines: list[int] = []
    sentinel_tracked_surfaces: list[str] = []

    for index, line_number in enumerate(line_numbers):
        allocator = allocator_kinds[index] if index < len(allocator_kinds) else "Unknown"
        line_surface = line_execution_surfaces[index]
        if finding.allowed:
            continue
        if constructor_is_gate_relevant(line_surface, allocator):
            forbidden_lines.append(line_number)
            forbidden_allocators.append(allocator)
            forbidden_surfaces.append(line_surface)
        elif allocator == "SentinelTracked":
            sentinel_tracked_lines.append(line_number)
            sentinel_tracked_surfaces.append(line_surface)
        else:
            transient_editor_scratch_lines.append(line_number)
            transient_editor_scratch_allocators.append(allocator)
            transient_editor_scratch_surfaces.append(line_surface)

    forbidden_count = len(forbidden_lines)
    non_gate_relevant_count = len(transient_editor_scratch_lines) + len(sentinel_tracked_lines)
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
        "nonGateRelevantConstructorCount": non_gate_relevant_count,
        "transientEditorScratchCount": len(transient_editor_scratch_lines),
        "transientEditorScratchLines": transient_editor_scratch_lines,
        "transientEditorScratchExecutionSurfaces": transient_editor_scratch_surfaces,
        "transientEditorScratchExecutionSurfaceCounts": count_values(transient_editor_scratch_surfaces),
        "transientEditorScratchAllocatorCounts": count_values(transient_editor_scratch_allocators),
        "sentinelTrackedCount": len(sentinel_tracked_lines),
        "sentinelTrackedLines": sentinel_tracked_lines,
        "sentinelTrackedExecutionSurfaces": sentinel_tracked_surfaces,
        "sentinelTrackedExecutionSurfaceCounts": count_values(sentinel_tracked_surfaces),
    }


def is_editor_offline_surface(relative_path: str) -> bool:
    return extract_execution_surface(relative_path) in EDITOR_OFFLINE_SURFACES


def classify_declaration_scope(scope: TypeScope | None, relative_path: str = "") -> str:
    if scope is None:
        return "unknownOwnerField"
    if is_job_scope(scope):
        return "burstJobInput" if scope.burst else "jobInputStruct"
    if is_registered_native_owner_scope(scope):
        return "registeredNativeOwnerStruct"
    if is_ref_struct_scope(scope):
        return "nativeViewStruct"
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


def declaration_allowed_path_accepts_classification(
    relative_path: str,
    classification: str,
    path_is_allowed: bool,
) -> bool:
    if not path_is_allowed:
        return False
    if not declaration_is_gate_relevant(classification):
        return True

    normalized = relative_path.replace("\\", "/")
    return normalized.endswith("Assets/_Project/Scripts/Core/Memory/H8Memory.cs")


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


def partial_type_names_in_source(sanitized_lines: Sequence[str]) -> set[str]:
    names: set[str] = set()
    for line in sanitized_lines:
        type_match = TYPE_DECLARATION_RE.search(line)
        if not type_match:
            continue

        kind = type_match.group("kind")
        modifier_prefix = line[: type_match.start()]
        if "partial" in kind.split() or re.search(r"\bpartial\b", modifier_prefix):
            names.add(type_match.group("name"))

    return names


def build_h8memory_lifetime_sources_by_owner(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    source_files: Sequence[CSharpSourceFile] | None = None,
) -> dict[str, str]:
    owner_parts: dict[str, list[str]] = {}
    resolved_source_files = source_files if source_files is not None else read_csharp_source_files(source_root)
    for source_file in resolved_source_files:
        source = source_file.source
        if "H8Memory" not in source:
            continue

        sanitized_source = source_file.sanitized_source or sanitize_csharp_source(source)
        sanitized_lines = sanitized_source.splitlines()
        for owner_name in partial_type_names_in_source(sanitized_lines):
            owner_parts.setdefault(owner_name, []).append(sanitized_source)

    return {
        owner_name: "\n".join(parts)
        for owner_name, parts in owner_parts.items()
        if parts
    }


def build_native_lifetime_helper_sources_by_directory(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    source_files: Sequence[CSharpSourceFile] | None = None,
) -> dict[str, str]:
    directory_parts: dict[str, list[str]] = {}
    resolved_source_files = source_files if source_files is not None else read_csharp_source_files(source_root)
    for source_file in resolved_source_files:
        source = source_file.source
        if (
            "NativeMemorySentinel" not in source
            and "NativeMemoryTrackingBridge" not in source
            and "H8Memory" not in source
        ):
            continue
        if not source_file_may_provide_directory_native_lifetime_helpers(source_file, source):
            continue

        directory = normalize_path(source_file.path.parent, repo_root)
        directory_parts.setdefault(directory, []).append(source_file.sanitized_source or sanitize_csharp_source(source))

    return {
        directory: "\n".join(parts)
        for directory, parts in directory_parts.items()
        if parts
    }


def build_native_lifetime_helper_raw_sources_by_directory(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    source_files: Sequence[CSharpSourceFile] | None = None,
) -> dict[str, str]:
    directory_parts: dict[str, list[str]] = {}
    resolved_source_files = source_files if source_files is not None else read_csharp_source_files(source_root)
    for source_file in resolved_source_files:
        source = source_file.source
        if (
            "NativeMemorySentinel" not in source
            and "NativeMemoryTrackingBridge" not in source
            and "H8Memory" not in source
        ):
            continue
        if not source_file_may_provide_directory_native_lifetime_helpers(source_file, source):
            continue

        directory = normalize_path(source_file.path.parent, repo_root)
        directory_parts.setdefault(directory, []).append(source)

    return {
        directory: "\n".join(parts)
        for directory, parts in directory_parts.items()
        if parts
    }


def scan_native_collection_declarations_in_source(
    source: str,
    relative_path: str,
    allowed: bool = False,
    sanitized_lines: Sequence[str] | None = None,
    original_lines: Sequence[str] | None = None,
    h8memory_lifetime_sources_by_owner: dict[str, str] | None = None,
    native_lifetime_helper_sources_by_directory: dict[str, str] | None = None,
    native_lifetime_helper_raw_sources_by_directory: dict[str, str] | None = None,
) -> list[DeclarationFinding]:
    if sanitized_lines is None:
        sanitized_lines = sanitize_csharp_source(source).splitlines()
    if original_lines is None:
        original_lines = source.splitlines()

    tracked_editor_preview = (
        "H8MEMORY_TRACKED_EDITOR_PREVIEW" in source
        and source_has_h8memory_lifetime_tokens(source)
    )
    sanitized_source = "\n".join(sanitized_lines)
    relative_directory = Path(relative_path.replace("\\", "/")).parent.as_posix()
    directory_lifetime_source = ""
    directory_lifetime_raw_source = ""
    source_has_local_lifetime_source = (
        source_has_sentinel_lifetime_tokens(sanitized_source)
        or source_has_reflective_sentinel_lifetime_tokens(source)
        or source_has_native_tracking_bridge_lifetime_tokens(sanitized_source)
        or source_has_h8memory_lifetime_tokens(sanitized_source)
        or (
            "EnsureGenerationHandle" in sanitized_source
            and (
                "ReleaseBuffer" in sanitized_source
                or "TryAcquireWriteLock" in sanitized_source
                or "ReleaseWriteLock" in sanitized_source
            )
        )
    )
    if (
        not source_has_local_lifetime_source
        and
        native_lifetime_helper_sources_by_directory
        and relative_directory in native_lifetime_helper_sources_by_directory
    ):
        directory_lifetime_source = native_lifetime_helper_sources_by_directory[relative_directory]
        if (
            native_lifetime_helper_raw_sources_by_directory
            and relative_directory in native_lifetime_helper_raw_sources_by_directory
        ):
            directory_lifetime_raw_source = native_lifetime_helper_raw_sources_by_directory[relative_directory]
    h8memory_lifetime_cache: dict[str, bool] = {}
    sentinel_lifetime_cache: dict[tuple[str, str], bool] = {}
    sentinel_id_lifetime_cache: dict[tuple[str, str], bool] = {}
    native_tracking_bridge_lifetime_cache: dict[tuple[str, str], bool] = {}
    datavault_alias_lifetime_cache: dict[str, bool] = {}
    h8memory_owner_lifetime_sources: dict[str, str] = {}
    combined_directory_lifetime_source = (
        sanitized_source + "\n" + directory_lifetime_source
        if directory_lifetime_source
        else sanitized_source
    )
    combined_directory_raw_lifetime_source = (
        source + "\n" + directory_lifetime_raw_source
        if directory_lifetime_raw_source
        else source
    )
    local_has_sentinel_lifetime_source = (
        source_has_sentinel_lifetime_tokens(sanitized_source)
        or source_has_reflective_sentinel_lifetime_tokens(source)
        or source_has_native_tracking_bridge_lifetime_tokens(sanitized_source)
    )
    directory_has_sentinel_lifetime_source = (
        source_has_sentinel_lifetime_tokens(directory_lifetime_source)
        or source_has_reflective_sentinel_lifetime_tokens(directory_lifetime_raw_source)
        or source_has_native_tracking_bridge_lifetime_tokens(directory_lifetime_source)
    )
    has_sentinel_lifetime_source = local_has_sentinel_lifetime_source or directory_has_sentinel_lifetime_source

    def h8memory_owner_lifetime_source(owner_name: str) -> str:
        if (
            not owner_name
            or not h8memory_lifetime_sources_by_owner
            or owner_name not in h8memory_lifetime_sources_by_owner
        ):
            return ""
        if owner_name not in h8memory_owner_lifetime_sources:
            h8memory_owner_lifetime_sources[owner_name] = (
                sanitized_source + "\n" + h8memory_lifetime_sources_by_owner[owner_name]
            )
        return h8memory_owner_lifetime_sources[owner_name]

    def has_h8memory_tracked_field_lifetime(field_name: str, owner_name: str = "") -> bool:
        cache_key = owner_name + "::" + field_name
        if cache_key not in h8memory_lifetime_cache:
            tracked = source_has_h8memory_tracked_field_lifetime(
                sanitized_source,
                field_name,
            )
            owner_lifetime_source = h8memory_owner_lifetime_source(owner_name)
            if not tracked and owner_lifetime_source:
                tracked = source_has_h8memory_tracked_field_lifetime(
                    owner_lifetime_source,
                    field_name,
                )
            if not tracked and directory_lifetime_source:
                tracked = source_has_h8memory_tracked_field_lifetime(
                    combined_directory_lifetime_source,
                    field_name,
                )
            h8memory_lifetime_cache[cache_key] = tracked
        return h8memory_lifetime_cache[cache_key]

    def has_sentinel_registered_field_lifetime(field_name: str, collection: str) -> bool:
        key = (field_name, collection)
        if key not in sentinel_lifetime_cache:
            tracked = False
            if local_has_sentinel_lifetime_source:
                tracked = source_has_sentinel_registered_field_lifetime(
                    sanitized_source,
                    field_name,
                    collection,
                    source,
                )
            if not tracked and directory_has_sentinel_lifetime_source:
                tracked = source_has_sentinel_registered_field_lifetime(
                    combined_directory_lifetime_source,
                    field_name,
                    collection,
                    combined_directory_raw_lifetime_source,
                )
            sentinel_lifetime_cache[key] = tracked
        return sentinel_lifetime_cache[key]

    def has_sentinel_id_backed_struct_field_lifetime(field_name: str, collection: str) -> bool:
        key = (field_name, collection)
        if key not in sentinel_id_lifetime_cache:
            tracked = False
            if local_has_sentinel_lifetime_source:
                tracked = source_has_sentinel_id_backed_struct_field_lifetime(
                    sanitized_source,
                    field_name,
                    collection,
                )
            if not tracked and directory_has_sentinel_lifetime_source:
                tracked = source_has_sentinel_id_backed_struct_field_lifetime(
                    combined_directory_lifetime_source,
                    field_name,
                    collection,
                )
            sentinel_id_lifetime_cache[key] = tracked
        return sentinel_id_lifetime_cache[key]

    def has_native_tracking_bridge_sidecar_field_lifetime(field_name: str, collection: str) -> bool:
        key = (field_name, collection)
        if key not in native_tracking_bridge_lifetime_cache:
            tracked = source_has_native_tracking_bridge_sidecar_field_lifetime(
                sanitized_source,
                field_name,
                collection,
            )
            if not tracked and directory_lifetime_source:
                tracked = source_has_native_tracking_bridge_sidecar_field_lifetime(
                    combined_directory_lifetime_source,
                    field_name,
                    collection,
                )
            native_tracking_bridge_lifetime_cache[key] = tracked
        return native_tracking_bridge_lifetime_cache[key]

    def has_datavault_alias_field_lifetime(field_name: str, owner_name: str = "") -> bool:
        cache_key = owner_name + "::" + field_name
        if cache_key not in datavault_alias_lifetime_cache:
            datavault_alias_lifetime_cache[cache_key] = source_has_datavault_alias_field_lifetime(
                sanitized_source,
                field_name,
                owner_name,
            )
        return datavault_alias_lifetime_cache[cache_key]

    scopes: list[TypeScope] = []
    pending_scope: TypeScope | None = None
    depth = 0
    findings: list[DeclarationFinding] = []
    declaration_path_is_h8memory_owner = relative_path.replace("\\", "/").endswith(
        "Assets/_Project/Scripts/Core/Memory/H8Memory.cs"
    )

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
            field_name = declaration_match.group("name")
            collection = declaration_match.group("collection")
            lifetime_checked_classification = classification in (
                "persistentOwnerField",
                "unknownStructField",
            )
            if declaration_path_is_h8memory_owner and lifetime_checked_classification:
                classification = "h8MemoryTrackedOwnerField"
            elif (
                lifetime_checked_classification
                and has_h8memory_tracked_field_lifetime(field_name, owner.name if owner else "")
            ):
                classification = "h8MemoryTrackedOwnerField"
            elif (
                lifetime_checked_classification
                and has_sentinel_lifetime_source
                and (
                    has_sentinel_registered_field_lifetime(field_name, collection)
                    or has_sentinel_id_backed_struct_field_lifetime(field_name, collection)
                    or has_native_tracking_bridge_sidecar_field_lifetime(field_name, collection)
                )
            ):
                classification = "sentinelTrackedOwnerField"
            elif (
                lifetime_checked_classification
                and has_datavault_alias_field_lifetime(field_name, owner.name if owner else "")
            ):
                classification = "dataVaultAliasOwnerField"
            finding_allowed = (
                declaration_allowed_path_accepts_classification(
                    relative_path,
                    classification,
                    allowed,
                )
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
                    names=(field_name,),
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
    findings: list[DeclarationFinding] = []
    source_files = read_csharp_source_files(source_root)
    h8memory_lifetime_sources_by_owner = build_h8memory_lifetime_sources_by_owner(source_root, repo_root, source_files)
    native_lifetime_helper_sources_by_directory = build_native_lifetime_helper_sources_by_directory(source_root, repo_root, source_files)
    native_lifetime_helper_raw_sources_by_directory = build_native_lifetime_helper_raw_sources_by_directory(source_root, repo_root, source_files)
    for source_file in source_files:
        path = source_file.path
        source = source_file.source
        if not contains_native_collection_token(source):
            continue
        if NATIVE_COLLECTION_DECLARATION_SCAN_RE.search(source) is None:
            continue

        relative_path = normalize_path(path, repo_root)
        sanitized_lines = source_file.sanitized_lines
        if not sanitized_lines:
            sanitized_lines = tuple(sanitize_csharp_source(source).splitlines())
        if not contains_native_collection_declaration_candidate(sanitized_lines):
            continue

        findings.extend(
            scan_native_collection_declarations_in_source(
                source,
                relative_path,
                is_allowed_path(relative_path, allowed_suffixes),
                sanitized_lines=sanitized_lines,
                original_lines=source_file.original_lines or source.splitlines(),
                h8memory_lifetime_sources_by_owner=h8memory_lifetime_sources_by_owner,
                native_lifetime_helper_sources_by_directory=native_lifetime_helper_sources_by_directory,
                native_lifetime_helper_raw_sources_by_directory=native_lifetime_helper_raw_sources_by_directory,
            )
        )

    return group_declaration_findings(findings)


def scan_latest_created_fallback_tree(
    source_root: Path,
    repo_root: Path = REPO_ROOT,
    allowed_suffixes: Sequence[str] = DEFAULT_ALLOWED_LATEST_CREATED_PATH_SUFFIXES,
    source_files: Sequence[CSharpSourceFile] | None = None,
) -> list[LatestCreatedFallbackFinding]:
    findings: list[LatestCreatedFallbackFinding] = []
    resolved_source_files = source_files if source_files is not None else read_csharp_source_files(source_root)
    for source_file in resolved_source_files:
        path = source_file.path
        source = source_file.source
        if not contains_latest_created_fallback_token(source):
            continue

        relative_path = normalize_path(path, repo_root)
        sanitized_lines = source_file.sanitized_lines
        if not sanitized_lines:
            sanitized_lines = tuple(sanitize_csharp_source(source).splitlines())
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
    source_files: Sequence[CSharpSourceFile] | None = None,
) -> tuple[list[FileFinding], list[DeclarationFinding]]:
    constructor_findings: list[FileFinding] = []
    declaration_findings: list[DeclarationFinding] = []
    resolved_source_files = source_files if source_files is not None else read_csharp_source_files(source_root)
    h8memory_lifetime_sources_by_owner = build_h8memory_lifetime_sources_by_owner(source_root, repo_root, resolved_source_files)
    native_lifetime_helper_sources_by_directory = build_native_lifetime_helper_sources_by_directory(source_root, repo_root, resolved_source_files)
    native_lifetime_helper_raw_sources_by_directory = build_native_lifetime_helper_raw_sources_by_directory(source_root, repo_root, resolved_source_files)
    for source_file in resolved_source_files:
        path = source_file.path
        source = source_file.source
        constructor_lines: list[int] = []
        constructor_allocator_kinds: list[str] = []

        if not contains_native_collection_token(source):
            continue
        raw_has_constructor = NATIVE_COLLECTION_CONSTRUCTOR_RE.search(source) is not None
        raw_has_declaration_candidate = NATIVE_COLLECTION_DECLARATION_SCAN_RE.search(source) is not None
        if not raw_has_constructor and not raw_has_declaration_candidate:
            continue

        sanitized_lines = source_file.sanitized_lines
        if not sanitized_lines:
            sanitized_lines = tuple(sanitize_csharp_source(source).splitlines())
        original_lines = source_file.original_lines or tuple(source.splitlines())
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
                h8memory_lifetime_sources_by_owner=h8memory_lifetime_sources_by_owner,
                native_lifetime_helper_sources_by_directory=native_lifetime_helper_sources_by_directory,
                native_lifetime_helper_raw_sources_by_directory=native_lifetime_helper_raw_sources_by_directory,
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
    sentinel_tracked_direct = sum(
        int(finding["sentinelTrackedCount"])
        for finding in constructor_findings
    )
    runtime_sentinel_tracked_direct = aggregate_constructor_count_for_surfaces(
        constructor_findings,
        "sentinelTrackedExecutionSurfaceCounts",
        {RUNTIME_SURFACE},
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
    h8memory_tracked_owner_declarations = [
        finding for finding in declaration_findings if finding.classification == "h8MemoryTrackedOwnerField"
    ]
    sentinel_tracked_owner_declarations = [
        finding for finding in declaration_findings if finding.classification == "sentinelTrackedOwnerField"
    ]
    datavault_alias_owner_declarations = [
        finding for finding in declaration_findings if finding.classification == "dataVaultAliasOwnerField"
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
        "sentinelTrackedDirectConstructors": sentinel_tracked_direct,
        "runtimeSentinelTrackedDirectConstructors": runtime_sentinel_tracked_direct,
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
        "h8MemoryTrackedNativeCollectionDeclarations": sum(
            finding.count for finding in h8memory_tracked_owner_declarations
        ),
        "sentinelTrackedNativeCollectionDeclarations": sum(
            finding.count for finding in sentinel_tracked_owner_declarations
        ),
        "dataVaultAliasNativeCollectionDeclarations": sum(
            finding.count for finding in datavault_alias_owner_declarations
        ),
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
        "sentinelTrackedDirectConstructors": payload.get("sentinelTrackedDirectConstructors", 0),
        "runtimeSentinelTrackedDirectConstructors": payload.get(
            "runtimeSentinelTrackedDirectConstructors",
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
            "Forbidden direct native collection constructors increased "
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
                "Forbidden native collection field declarations increased "
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
                    f"{path}: forbidden native collection field declarations increased from {baseline_count} to {count}."
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
        f"| Total direct native collection constructors | {payload['totalDirectConstructors']} |",
        f"| Allowed allocator-internal constructors | {payload['allowedDirectConstructors']} |",
        f"| Forbidden system constructors | {payload['forbiddenDirectConstructors']} |",
        f"| Runtime forbidden constructors | {payload.get('runtimeForbiddenDirectConstructors', 0)} |",
        f"| Editor/offline forbidden constructors | {payload.get('editorOfflineForbiddenDirectConstructors', 0)} |",
        f"| Editor/offline transient scratch constructors | {payload.get('editorOfflineTransientScratchDirectConstructors', 0)} |",
        f"| Sentinel-tracked owner wrapper constructors | {payload.get('sentinelTrackedDirectConstructors', 0)} |",
        f"| Runtime sentinel-tracked owner wrapper constructors | {payload.get('runtimeSentinelTrackedDirectConstructors', 0)} |",
        f"| Files with forbidden constructors | {payload['forbiddenFileCount']} |",
        f"| Editor/offline session scratch declarations | {payload.get('editorOfflineSessionScratchNativeCollectionDeclarations', 0)} |",
        f"| Editor/offline persistent preview declarations | {payload.get('editorOfflinePersistentPreviewNativeCollectionDeclarations', 0)} |",
        f"| Total field-like native collection declarations | {payload.get('totalNativeArrayDeclarations', 0)} |",
        f"| Allowed DataVault/H8Memory declarations | {payload.get('allowedNativeArrayDeclarations', 0)} |",
        f"| Forbidden system declarations | {payload.get('forbiddenNativeArrayDeclarations', 0)} |",
        f"| Persistent owner native collection declarations | {payload.get('persistentNativeCollectionDeclarations', 0)} |",
        f"| H8Memory-tracked owner field declarations | {payload.get('h8MemoryTrackedNativeCollectionDeclarations', 0)} |",
        f"| DataVault alias owner field declarations | {payload.get('dataVaultAliasNativeCollectionDeclarations', 0)} |",
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
    source_files = read_csharp_source_files(args.root)
    findings, declaration_findings = scan_source_tree_with_declarations(args.root, source_files=source_files)
    latest_created_fallback_findings = scan_latest_created_fallback_tree(args.root, source_files=source_files)
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
            f"{payload['forbiddenDirectConstructors']} forbidden direct native collection constructors remain."
        )
    if args.fail_on_any and int(payload.get("forbiddenNativeArrayDeclarations", 0)) > 0:
        failure_reasons.append(
            f"{payload['forbiddenNativeArrayDeclarations']} forbidden native collection field declarations remain."
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
        f"sentinelTracked={payload.get('sentinelTrackedDirectConstructors', 0)}, "
        f"runtimeSentinelTracked={payload.get('runtimeSentinelTrackedDirectConstructors', 0)}, "
        f"editorOfflineAllocatorSplit={payload.get('editorOfflineForbiddenDirectConstructorsByAllocator', {})}, "
        f"files={payload['forbiddenFileCount']}, "
        f"declarations={payload.get('totalNativeArrayDeclarations', 0)}, "
        f"forbiddenDeclarations={payload.get('forbiddenNativeArrayDeclarations', 0)}, "
        f"persistentDeclarations={payload.get('persistentNativeCollectionDeclarations', 0)}, "
        f"h8MemoryTrackedDeclarations={payload.get('h8MemoryTrackedNativeCollectionDeclarations', 0)}, "
        f"dataVaultAliasDeclarations={payload.get('dataVaultAliasNativeCollectionDeclarations', 0)}, "
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
