#!/usr/bin/env python3
"""HECTON-8 static H-Phi architecture scanner.

Evidence class: STATIC_SOURCE / FILESYSTEM.

This is a focused C# structural parser for CI/editor enforcement. It strips
comments and literals, builds type/method scopes from balanced braces, evaluates
rule sites in syntax scopes, emits a deterministic flat JSON report, and
regenerates atlas graph sections from asmdef files.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SOURCE_ROOT = ROOT / "Assets" / "_Project"
SCRIPT_ROOT = ROOT / "Assets" / "_Project" / "Scripts"
REPORT_PATH = ROOT / "Docs" / "Reports" / "HECTON_PHI_SCORE_FINAL.json"
HPHI_ATLAS_PATH = ROOT / "Docs" / "Generated" / "PROJECT_ATLAS_HPHI.md"
DEPENDENCY_GRAPH_PATH = ROOT / "Docs" / "Generated" / "DEPENDENCY_GRAPH.md"
DEPENDENCY_GRAPH_JSON_PATH = ROOT / "Docs" / "Generated" / "DEPENDENCY_GRAPH.json"
DEFAULT_EXCLUSION_PATHS = (
    ROOT / "scanner_exclusion_rules.csv",
    ROOT / "Tools" / "scanner_exclusion_rules.csv",
)
HOOK_PATH = ROOT / ".git" / "hooks" / "pre-commit"

SKIP_DIR_NAMES = {
    ".git",
    ".vs",
    ".idea",
    "__pycache__",
    "bin",
    "obj",
    "Library",
    "Logs",
    "Temp",
    "UserSettings",
}

HOT_METHOD_NAMES = {
    "Update",
    "FixedUpdate",
    "LateUpdate",
    "Tick",
    "FixedTick",
    "LateFrameTick",
    "SlowTick",
    "ColdTick",
    "FrostTick",
    "Execute",
}

SYNC_ALLOWED_METHOD_TOKENS = (
    "POST_SIMULATION",
    "PostSimulation",
    "VISUAL_SYNC",
    "VisualSync",
    "Bootstrap",
    "Boot",
    "Initialize",
    "OnDisable",
    "OnDestroy",
    "Dispose",
    "Shutdown",
    "Teardown",
    "Cleanup",
    "Drain",
    "Flush",
)

VALUE_NEW_TYPES = {
    "Vector2",
    "Vector3",
    "Vector4",
    "Quaternion",
    "Color",
    "Color32",
    "Bounds",
    "Rect",
    "Ray",
    "float2",
    "float3",
    "float4",
    "double2",
    "double3",
    "double4",
    "int2",
    "int3",
    "int4",
    "uint2",
    "uint3",
    "uint4",
    "FixedString32Bytes",
    "FixedString64Bytes",
    "FixedString128Bytes",
}

EIGHT_BYTE_TYPES = {
    "double",
    "long",
    "ulong",
    "Int64",
    "UInt64",
    "double2",
    "double3",
    "double4",
}

LINQ_CALL_RE = re.compile(
    r"\.(Select|Where|Any|All|First|FirstOrDefault|Last|LastOrDefault|ToList|ToArray|OrderBy|OrderByDescending|GroupBy|Aggregate|Sum|Min|Max|Count)\s*\("
)
NEW_RE = re.compile(r"\bnew\s+(?P<type>[A-Za-z_][A-Za-z0-9_\.]*)(?:\s*<[^;\n\(\[]+>)?\s*(?P<kind>\[|\()")
NATIVE_NEW_RE = re.compile(
    r"\bnew\s+(?P<type>NativeArray|NativeList|NativeHashMap|NativeParallelHashMap|NativeQueue|UnsafeList)\s*<"
)
NATIVE_DECL_RE = re.compile(
    r"\b(?P<type>NativeArray|NativeList|NativeHashMap|NativeParallelHashMap|NativeQueue|UnsafeList)\s*<[^;]+>\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)"
)
TYPE_DECL_RE = re.compile(
    r"(?P<attrs>(?:\s*\[[^\]]+\]\s*)*)"
    r"(?P<mods>(?:(?:public|private|protected|internal|static|readonly|unsafe|partial|sealed|abstract|new)\s+)*)"
    r"(?P<kind>record\s+struct|record\s+class|class|struct|interface)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)"
    r"(?P<bases>\s*:\s*[^\{;]+)?\s*(?P<brace>\{)"
)
METHOD_RE = re.compile(
    r"(?P<attrs>(?:\s*\[[^\]]+\]\s*)*)"
    r"(?P<mods>(?:(?:public|private|protected|internal|static|virtual|override|sealed|async|unsafe|extern|partial|new|readonly)\s+)*)"
    r"(?:(?P<ret>[A-Za-z_][A-Za-z0-9_<>,\.\?\[\]:]*\s+)+)?"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*"
    r"\((?P<args>[^\)]*)\)\s*"
    r"(?:where\s+[^\{;]+)?(?P<brace>\{)"
)
CONTROL_NAMES = {"if", "for", "foreach", "while", "switch", "catch", "using", "lock", "fixed"}
LOOP_RE = re.compile(r"\b(for|foreach|while)\s*\(")
GLOBAL_GET_RE = re.compile(r"\bGlobalRegistry\s*\.\s*Get\s*<|\bHecton8\.Core\.GlobalRegistry\s*\.\s*Get\s*<")
GLOBAL_SURFACE_RE = re.compile(r"\b(?:Hecton8\.Core\.)?GlobalRegistry\s*\.\s*[A-Za-z_][A-Za-z0-9_]*")
EVENT_PUBLISH_RE = re.compile(r"\bHectonEventBus\s*\.\s*Publish\s*\(")
COMPLETE_RE = re.compile(r"\.(Complete|CompleteAll)\s*\(")
AUP_FLOAT_DISTANCE_RE = re.compile(r"\bVector3\s*\.\s*Distance\s*\(|\.magnitude\b")
PROPERTY_IN_STRUCT_RE = re.compile(
    r"\b(?:public|private|protected|internal)\s+(?:readonly\s+)?[A-Za-z_][A-Za-z0-9_<>,\.\?\[\]]*\s+"
    r"[A-Za-z_][A-Za-z0-9_]*\s*\{\s*(?:get|set)\b",
    re.S,
)
FIELD_OFFSET_RE = re.compile(
    r"\[FieldOffset\s*\(\s*(?P<offset>\d+)\s*\)\]\s*"
    r"(?:(?:public|private|protected|internal|readonly|static|unsafe|volatile)\s+)*"
    r"(?P<type>[A-Za-z_][A-Za-z0-9_\.<>]*)\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)",
    re.S,
)
STRUCT_FIELD_RE = re.compile(
    r"(?:(?:public|private|protected|internal|readonly|static|unsafe|volatile)\s+)+"
    r"(?P<type>[A-Za-z_][A-Za-z0-9_\.<>]*)\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=|;)",
    re.S,
)
SHADER_LEGACY_RE = re.compile(r"\bGrabPass\b|#pragma\s+surface\b", re.I)


class Violation:
    __slots__ = ("file", "line", "rule", "severity", "message", "owner", "context")

    def __init__(self, file: str, line: int, rule: str, severity: str, message: str, owner: str = "", context: str = ""):
        self.file = file
        self.line = int(line)
        self.rule = rule
        self.severity = severity
        self.message = message
        self.owner = owner
        self.context = context

    def key(self) -> tuple[str, str, int, str, str]:
        return (self.file, self.rule, self.line, self.severity, self.message)

    def to_dict(self) -> dict[str, object]:
        return {
            "File": self.file,
            "Line": self.line,
            "Rule": self.rule,
            "Severity": self.severity,
            "Message": self.message,
            "Owner": self.owner,
            "Context": self.context,
        }


class TypeScope:
    __slots__ = ("name", "kind", "bases", "attributes", "start", "open_brace", "end", "line", "body", "is_job", "is_signal")

    def __init__(self, name: str, kind: str, bases: str, attributes: str, start: int, open_brace: int, end: int, line: int, body: str):
        self.name = name
        self.kind = kind
        self.bases = bases
        self.attributes = attributes
        self.start = start
        self.open_brace = open_brace
        self.end = end
        self.line = line
        self.body = body
        self.is_job = any(token in bases for token in ("IJob", "IJobParallelFor", "IJobFor", "IJobChunk", "IJobEntity"))
        self.is_signal = "ISignal" in bases


class MethodScope:
    __slots__ = ("name", "owner", "start", "open_brace", "end", "line", "body", "is_hot", "loop_spans")

    def __init__(self, name: str, owner: TypeScope, start: int, open_brace: int, end: int, line: int, body: str):
        self.name = name
        self.owner = owner
        self.start = start
        self.open_brace = open_brace
        self.end = end
        self.line = line
        self.body = body
        self.is_hot = name in HOT_METHOD_NAMES or (owner.is_job and name == "Execute")
        self.loop_spans: list[tuple[int, int]] = []


class FileModel:
    __slots__ = ("path", "rel", "text", "scrub", "line_starts", "literal_lines", "types", "methods")

    def __init__(self, path: Path, rel: str, text: str, scrub: str, line_starts: list[int], literal_lines: set[int]):
        self.path = path
        self.rel = rel
        self.text = text
        self.scrub = scrub
        self.line_starts = line_starts
        self.literal_lines = literal_lines
        self.types: list[TypeScope] = []
        self.methods: list[MethodScope] = []


def normalize_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT.resolve()).as_posix()
    except ValueError:
        return path.as_posix().replace("\\", "/")


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.parts)


def line_starts(text: str) -> list[int]:
    starts = [0]
    for match in re.finditer("\n", text):
        starts.append(match.end())
    return starts


def line_of_index(starts: list[int], index: int) -> int:
    lo = 0
    hi = len(starts)
    while lo + 1 < hi:
        mid = (lo + hi) // 2
        if starts[mid] <= index:
            lo = mid
        else:
            hi = mid
    return lo + 1


def find_matching_brace(scrub: str, open_index: int) -> int:
    depth = 0
    for i in range(open_index, len(scrub)):
        c = scrub[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return i
    return len(scrub) - 1


def strip_comments_and_literals(text: str) -> tuple[str, set[int]]:
    chars = list(text)
    literal_lines: set[int] = set()
    starts = line_starts(text)
    i = 0
    n = len(chars)
    while i < n:
        c = chars[i]
        nxt = chars[i + 1] if i + 1 < n else ""
        if c == "/" and nxt == "/":
            j = i
            while j < n and chars[j] != "\n":
                chars[j] = " "
                j += 1
            i = j
            continue
        if c == "/" and nxt == "*":
            chars[i] = chars[i + 1] = " "
            i += 2
            while i + 1 < n and not (chars[i] == "*" and chars[i + 1] == "/"):
                if chars[i] != "\n":
                    chars[i] = " "
                i += 1
            if i + 1 < n:
                chars[i] = chars[i + 1] = " "
                i += 2
            continue
        if c == "'" and i + 1 < n:
            literal_lines.add(line_of_index(starts, i))
            chars[i] = " "
            i += 1
            while i < n:
                old = chars[i]
                if old != "\n":
                    chars[i] = " "
                i += 1
                if old == "\\" and i < n:
                    if chars[i] != "\n":
                        chars[i] = " "
                    i += 1
                    continue
                if old == "'":
                    break
            continue
        if c == '"' or (c == "@" and nxt == '"') or (c == "$" and nxt in ('"', "@")):
            literal_lines.add(line_of_index(starts, i))
            prefix_len = 1
            if c == "@" and nxt == '"':
                prefix_len = 2
            elif c == "$" and nxt == "@":
                prefix_len = 3 if i + 2 < n and chars[i + 2] == '"' else 2
            elif c == "$" and nxt == '"':
                prefix_len = 2
            for k in range(i, min(i + prefix_len, n)):
                if chars[k] != "\n":
                    chars[k] = " "
            i += prefix_len
            raw = prefix_len >= 2 and text[i - prefix_len : i].replace("$", "").startswith("@")
            while i < n:
                old = chars[i]
                if old != "\n":
                    chars[i] = " "
                i += 1
                if old == "\n":
                    literal_lines.add(line_of_index(starts, i))
                if not raw and old == "\\" and i < n:
                    if chars[i] != "\n":
                        chars[i] = " "
                    i += 1
                    continue
                if raw and old == '"' and i < n and chars[i] == '"':
                    chars[i] = " "
                    i += 1
                    continue
                if old == '"':
                    break
            continue
        i += 1
    return "".join(chars), literal_lines


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="ignore")


def read_model(path: Path) -> FileModel:
    text = read_text(path)
    scrub, literal_lines = strip_comments_and_literals(text)
    return FileModel(path, normalize_path(path), text, scrub, line_starts(text), literal_lines)


def iter_project_files(patterns: tuple[str, ...], roots: tuple[Path, ...], changed_only: bool = False) -> list[Path]:
    if changed_only:
        changed = git_changed_files()
        suffixes = tuple(pattern[1:] for pattern in patterns if pattern.startswith("*."))
        return sorted(ROOT / rel for rel in changed if rel.endswith(suffixes) and (ROOT / rel).exists())
    files: list[Path] = []
    for root in roots:
        if not root.exists():
            continue
        for pattern in patterns:
            for path in root.rglob(pattern):
                if path.is_file() and not should_skip(path.relative_to(ROOT)):
                    files.append(path)
    return sorted(set(files))


def git_changed_files() -> list[str]:
    try:
        result = subprocess.run(
            ["git", "diff", "--cached", "--name-only", "--diff-filter=ACMR"],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="ignore",
        )
    except OSError:
        return []
    return [line.strip().replace("\\", "/") for line in result.stdout.splitlines() if line.strip()]


def parse_model(model: FileModel) -> None:
    for match in TYPE_DECL_RE.finditer(model.scrub):
        name = match.group("name")
        kind = match.group("kind")
        bases = (match.group("bases") or "").lstrip(":").strip()
        open_brace = match.start("brace")
        end = find_matching_brace(model.scrub, open_brace)
        if end <= open_brace:
            continue
        body = model.scrub[open_brace + 1 : end]
        t = TypeScope(
            name=name,
            kind=kind,
            bases=bases,
            attributes=match.group("attrs") or "",
            start=match.start(),
            open_brace=open_brace,
            end=end,
            line=line_of_index(model.line_starts, match.start()),
            body=body,
        )
        model.types.append(t)

    for owner in model.types:
        owner_body = model.scrub[owner.open_brace + 1 : owner.end]
        owner_offset = owner.open_brace + 1
        for match in METHOD_RE.finditer(owner_body):
            name = match.group("name")
            if name in CONTROL_NAMES:
                continue
            if name == owner.name or match.group("ret") is not None:
                open_brace = owner_offset + match.start("brace")
                end = find_matching_brace(model.scrub, open_brace)
                if end <= open_brace:
                    continue
                body = model.scrub[open_brace + 1 : end]
                m = MethodScope(
                    name=name,
                    owner=owner,
                    start=owner_offset + match.start(),
                    open_brace=open_brace,
                    end=end,
                    line=line_of_index(model.line_starts, owner_offset + match.start()),
                    body=body,
                )
                m.loop_spans = collect_loop_spans(model.scrub, m.open_brace + 1, m.end)
                model.methods.append(m)


def collect_loop_spans(scrub: str, start: int, end: int) -> list[tuple[int, int]]:
    spans: list[tuple[int, int]] = []
    segment = scrub[start:end]
    for match in LOOP_RE.finditer(segment):
        absolute = start + match.start()
        brace = scrub.find("{", absolute, end)
        semicolon = scrub.find(";", absolute, end)
        if brace != -1 and (semicolon == -1 or brace < semicolon):
            spans.append((absolute, find_matching_brace(scrub, brace)))
        elif semicolon != -1:
            spans.append((absolute, semicolon))
    return spans


def in_loop(method: MethodScope, absolute_index: int) -> bool:
    for start, end in method.loop_spans:
        if start <= absolute_index <= end:
            return True
    return False


def original_line(model: FileModel, line: int) -> str:
    lines = model.text.splitlines()
    if 1 <= line <= len(lines):
        return lines[line - 1].strip()
    return ""


def method_original_text(model: FileModel, method: MethodScope) -> str:
    return model.text[method.open_brace + 1 : method.end]


def has_safe_aup_context(model: FileModel, line: int) -> bool:
    lines = model.text.splitlines()
    start = max(0, line - 7)
    context = "\n".join(lines[start:line])
    safe_tokens = ("double3", "AbsoluteUniversePosition", "AUP", "Aup", "LocalDelta", "DistanceSq", "distancesq", "sqrMagnitude")
    return any(token in context for token in safe_tokens) and "-" in context


def is_first_party_runtime(rel: str) -> bool:
    rel_norm = rel.replace("\\", "/")
    return rel_norm.startswith("Assets/_Project/") and "/Editor/" not in rel_norm and "/Tests/" not in rel_norm


def is_primary_runtime(rel: str) -> bool:
    rel_norm = rel.replace("\\", "/")
    if not rel_norm.startswith("Assets/_Project/Scripts/"):
        return False
    if any(part in rel_norm for part in ("/Editor/", "/Tests/", "/Test/", "/Dev/", "/QA/")):
        return False
    if any(token in Path(rel_norm).name for token in ("SmokeTester", "Benchmark", "Validator", "Verifier", "TunerWindow")):
        return False
    return True


def is_allowed_native_owner(rel: str, owner: TypeScope, method_text: str) -> bool:
    rel_norm = rel.replace("\\", "/")
    if "/Core/Memory/" in rel_norm or rel_norm.endswith("/H8Memory.cs") or rel_norm.endswith("/GlobalDataVault.cs"):
        return True
    if "GlobalDataVault.GetBufferHandle" in method_text or "GetBufferHandle" in method_text and "GlobalDataVault" in method_text:
        return True
    if "H8Memory.Allocate" in method_text or "NativeMemorySentinel" in method_text:
        return True
    if "/Editor/" in rel_norm or "/Tests/" in rel_norm or "Baker" in rel_norm or "Offline" in rel_norm:
        return True
    return False


def add_violation(violations: list[Violation], model: FileModel, index: int, rule: str, severity: str, message: str, owner: str = "", context: str = "") -> None:
    violations.append(Violation(model.rel, line_of_index(model.line_starts, index), rule, severity, message, owner, context))


def scan_cs_model(model: FileModel) -> list[Violation]:
    parse_model(model)
    violations: list[Violation] = []
    scan_struct_layouts(model, violations)
    scan_methods(model, violations)
    return violations


def scan_struct_layouts(model: FileModel, violations: list[Violation]) -> None:
    for t in model.types:
        if "struct" not in t.kind:
            continue
        attr = t.attributes
        body_original = model.text[t.open_brace + 1 : t.end]
        is_runtime_payload = (
            t.is_signal
            or "NativeArray" in body_original
            or "NativeList" in body_original
            or "VaultBuffer" in body_original
            or "GlobalDataVault" in model.text
            or "SignalBus" in model.text
            or is_primary_runtime(model.rel)
        )
        if "Pack" in attr and re.search(r"Pack\s*=\s*1\b", attr):
            severity = "CRITICAL" if is_runtime_payload else "ERROR"
            violations.append(
                Violation(model.rel, t.line, "MemoryAlignment.Pack1", severity, f"{t.name} uses StructLayout Pack=1.", t.name)
            )
        has_struct_layout = "StructLayout" in attr
        explicit = "LayoutKind.Explicit" in attr or "Explicit" in attr and "StructLayout" in attr
        if is_runtime_payload and (not has_struct_layout or not explicit):
            severity = "CRITICAL" if t.is_signal or "NativeArray" in body_original or "NativeList" in body_original else "ERROR"
            violations.append(
                Violation(
                    model.rel,
                    t.line,
                    "MemoryAlignment.MissingExplicitLayout",
                    severity,
                    f"{t.name} lacks [StructLayout(LayoutKind.Explicit)] for first-party runtime struct.",
                    t.name,
                )
            )
        if explicit:
            for fm in FIELD_OFFSET_RE.finditer(body_original):
                field_type = fm.group("type").rsplit(".", 1)[-1]
                offset = int(fm.group("offset"))
                if field_type in EIGHT_BYTE_TYPES and offset % 8 != 0:
                    line = line_of_index(model.line_starts, t.open_brace + 1 + fm.start())
                    violations.append(
                        Violation(
                            model.rel,
                            line,
                            "MemoryAlignment.MisalignedFieldOffset",
                            "CRITICAL",
                            f"{t.name}.{fm.group('name')} type {field_type} FieldOffset({offset}) is not divisible by 8.",
                            t.name,
                        )
                    )
            for field in STRUCT_FIELD_RE.finditer(body_original):
                field_type = field.group("type").rsplit(".", 1)[-1]
                if field_type in EIGHT_BYTE_TYPES:
                    prefix = body_original[max(0, field.start() - 96) : field.start()]
                    if "FieldOffset" not in prefix:
                        line = line_of_index(model.line_starts, t.open_brace + 1 + field.start())
                        violations.append(
                            Violation(
                                model.rel,
                                line,
                                "MemoryAlignment.MissingFieldOffset",
                                "ERROR",
                                f"{t.name}.{field.group('name')} is an 8-byte field without FieldOffset in explicit layout.",
                                t.name,
                            )
                        )
        if is_runtime_payload and PROPERTY_IN_STRUCT_RE.search(body_original):
            violations.append(
                Violation(
                    model.rel,
                    t.line,
                    "CS1612.StructProperty",
                    "ERROR",
                    f"{t.name} contains property accessors inside a DataVault/native/signal struct surface.",
                    t.name,
                )
            )


def scan_methods(model: FileModel, violations: list[Violation]) -> None:
    path_sensitive_aup = any(token in model.rel.replace("\\", "/") for token in ("/Physics/", "/Combat/", "/AI/"))
    for method in model.methods:
        method_text = method_original_text(model, method)
        method_scrub = model.scrub[method.open_brace + 1 : method.end]
        method_abs_start = method.open_brace + 1
        owner_name = method.owner.name
        hot = method.is_hot

        for regex, rule, message in (
            (GLOBAL_GET_RE, "GlobalAuthority.GlobalRegistryGetHotPath", "GlobalRegistry.Get<T>() in hot method or loop."),
            (EVENT_PUBLISH_RE, "GlobalAuthority.HectonEventBusPublishHotPath", "HectonEventBus.Publish() in hot method or loop."),
        ):
            for match in regex.finditer(method_scrub):
                absolute = method_abs_start + match.start()
                if hot or in_loop(method, absolute):
                    sev = "CRITICAL" if is_primary_runtime(model.rel) else "ERROR"
                    add_violation(violations, model, absolute, rule, sev, message, owner_name, method.name)

        for match in GLOBAL_SURFACE_RE.finditer(method_scrub):
            absolute = method_abs_start + match.start()
            if (hot or in_loop(method, absolute)) and not GLOBAL_GET_RE.search(match.group(0)):
                sev = "ERROR" if is_primary_runtime(model.rel) else "WARN"
                add_violation(
                    violations,
                    model,
                    absolute,
                    "GlobalAuthority.GlobalRegistrySurfaceHotPath",
                    sev,
                    "GlobalRegistry service surface read in hot method or loop; cache during injection.",
                    owner_name,
                    method.name,
                )

        for match in COMPLETE_RE.finditer(method_scrub):
            absolute = method_abs_start + match.start()
            if not any(token in method.name for token in SYNC_ALLOWED_METHOD_TOKENS):
                sev = "ERROR" if is_primary_runtime(model.rel) else "WARN"
                add_violation(
                    violations,
                    model,
                    absolute,
                    "JobFence.SynchronousComplete",
                    sev,
                    ".Complete()/.CompleteAll() outside named post-simulation, visual-sync, boot, or teardown window.",
                    owner_name,
                    method.name,
                )

        for match in NATIVE_NEW_RE.finditer(method_scrub):
            absolute = method_abs_start + match.start()
            if not is_allowed_native_owner(model.rel, method.owner, method_text):
                sev = "CRITICAL" if is_primary_runtime(model.rel) else "ERROR"
                add_violation(
                    violations,
                    model,
                    absolute,
                    "DataSovereignty.LocalNativeAllocation",
                    sev,
                    f"Local {match.group('type')} allocation bypasses GlobalDataVault/H8Memory ownership.",
                    owner_name,
                    method.name,
                )

        if hot:
            for match in NEW_RE.finditer(method_scrub):
                new_type = match.group("type").rsplit(".", 1)[-1]
                absolute = method_abs_start + match.start()
                if match.group("kind") == "[" or new_type not in VALUE_NEW_TYPES:
                    add_violation(
                        violations,
                        model,
                        absolute,
                        "ZeroGC.NewReferenceInHotPath",
                        "ERROR",
                        f"new {new_type} in hot method {method.name}.",
                        owner_name,
                        method.name,
                    )
            for match in LINQ_CALL_RE.finditer(method_scrub):
                add_violation(
                    violations,
                    model,
                    method_abs_start + match.start(),
                    "ZeroGC.LinqInHotPath",
                    "ERROR",
                    f"LINQ call .{match.group(1)}() in hot method {method.name}.",
                    owner_name,
                    method.name,
                )
            for match in re.finditer(r"\b(ToString|string\s*\.\s*Format|StringBuilder)\s*(?:\.|\()", method_scrub):
                add_violation(
                    violations,
                    model,
                    method_abs_start + match.start(),
                    "ZeroGC.ManagedStringInHotPath",
                    "ERROR",
                    "Managed string formatting/conversion in hot method.",
                    owner_name,
                    method.name,
                )
            start_line = line_of_index(model.line_starts, method.open_brace)
            end_line = line_of_index(model.line_starts, method.end)
            for line in sorted(line for line in model.literal_lines if start_line <= line <= end_line):
                text_line = original_line(model, line)
                if '$"' in text_line or "$@" in text_line or re.search(r'"[^"]*"\s*\+', text_line) or re.search(r'\+\s*"[^"]*"', text_line):
                    violations.append(
                        Violation(
                            model.rel,
                            line,
                            "ZeroGC.StringAllocationInHotPath",
                            "ERROR",
                            "String interpolation or string concatenation in hot method.",
                            owner_name,
                            method.name,
                        )
                    )

        if path_sensitive_aup:
            for match in AUP_FLOAT_DISTANCE_RE.finditer(method_scrub):
                absolute = method_abs_start + match.start()
                line = line_of_index(model.line_starts, absolute)
                if not has_safe_aup_context(model, line):
                    violations.append(
                        Violation(
                            model.rel,
                            line,
                            "AUP.AbsoluteFloatMath",
                            "ERROR",
                            "Vector3.Distance or .magnitude in Physics/Combat/AI without nearby double3/AUP local delta subtraction.",
                            owner_name,
                            method.name,
                        )
                    )


def scan_shader(path: Path) -> list[Violation]:
    text = read_text(path)
    rel = normalize_path(path)
    violations: list[Violation] = []
    for match in SHADER_LEGACY_RE.finditer(text):
        starts = line_starts(text)
        violations.append(
            Violation(
                rel,
                line_of_index(starts, match.start()),
                "Shader.LegacySurface",
                "WARN",
                "Legacy shader construct found during static scan.",
                "",
                "",
            )
        )
    return violations


def load_asmdef(path: Path) -> dict[str, object]:
    try:
        raw = json.loads(read_text(path))
    except json.JSONDecodeError as exc:
        return {"name": path.stem, "references": [], "_parseError": str(exc)}
    if not isinstance(raw, dict):
        return {"name": path.stem, "references": [], "_parseError": "root is not an object"}
    return raw


def read_guid(path: Path) -> str:
    meta = path.with_suffix(path.suffix + ".meta")
    if not meta.exists():
        return ""
    for line in read_text(meta).splitlines():
        stripped = line.strip()
        if stripped.startswith("guid:"):
            return stripped.split(":", 1)[1].strip()
    return ""


def resolve_asmdefs(paths: list[Path]) -> tuple[dict[str, dict[str, object]], dict[str, str]]:
    raw_by_name: dict[str, dict[str, object]] = {}
    guid_to_name: dict[str, str] = {}
    raw_by_path: dict[Path, dict[str, object]] = {}
    for path in paths:
        raw = load_asmdef(path)
        raw_by_path[path] = raw
        name = str(raw.get("name") or path.stem)
        guid = read_guid(path)
        if guid:
            guid_to_name[guid] = name
    for path, raw in raw_by_path.items():
        name = str(raw.get("name") or path.stem)
        refs = []
        for ref in raw.get("references", []):
            ref_text = str(ref)
            if ref_text.startswith("GUID:"):
                refs.append(guid_to_name.get(ref_text.split(":", 1)[1], ref_text))
            else:
                refs.append(ref_text)
        raw_by_name[name] = {
            "name": name,
            "path": normalize_path(path),
            "references": sorted(set(refs)),
            "includePlatforms": raw.get("includePlatforms", []),
            "autoReferenced": raw.get("autoReferenced", True),
        }
    return raw_by_name, guid_to_name


def find_cycles(graph: dict[str, list[str]]) -> list[list[str]]:
    cycles: list[list[str]] = []
    visiting: set[str] = set()
    visited: set[str] = set()
    stack: list[str] = []

    def dfs(node: str) -> None:
        visiting.add(node)
        stack.append(node)
        for dep in graph.get(node, []):
            if dep not in graph:
                continue
            if dep in visiting:
                start = stack.index(dep)
                cycles.append(stack[start:] + [dep])
            elif dep not in visited:
                dfs(dep)
        stack.pop()
        visiting.remove(node)
        visited.add(node)

    for node in sorted(graph):
        if node not in visited:
            dfs(node)
    unique: dict[str, list[str]] = {}
    for cycle in cycles:
        key = "->".join(cycle)
        unique[key] = cycle
    return [unique[key] for key in sorted(unique)]


def scan_asmdefs(paths: list[Path]) -> tuple[list[Violation], dict[str, object]]:
    asmdefs, _ = resolve_asmdefs(paths)
    graph = {name: list(data["references"]) for name, data in asmdefs.items()}
    cycles = find_cycles(graph)
    violations: list[Violation] = []
    path_by_name = {name: str(data["path"]) for name, data in asmdefs.items()}
    for cycle in cycles:
        first = cycle[0]
        violations.append(
            Violation(
                path_by_name.get(first, ""),
                1,
                "Assembly.CyclicDependency",
                "CRITICAL",
                "Cyclic asmdef dependency: " + " -> ".join(cycle),
                first,
            )
        )
    for name, data in asmdefs.items():
        if not name.startswith("Hecton8.") or name.endswith(".Editor"):
            continue
        for ref in data["references"]:
            if ref.startswith("Hecton8.") and not ref.endswith(".Contracts") and ref != "Hecton8.Core.Contracts":
                source_domain = ".".join(name.split(".")[:2])
                target_domain = ".".join(ref.split(".")[:2])
                if source_domain != target_domain and ref == "Hecton8.Core":
                    violations.append(
                        Violation(
                            str(data["path"]),
                            1,
                            "Assembly.CoreGravityWell",
                            "WARN",
                            f"{name} references concrete Hecton8.Core instead of a contract-first seam.",
                            name,
                            ref,
                        )
                    )
    graph_data = {
        "AssemblyCount": len(asmdefs),
        "FirstPartyAssemblyCount": sum(1 for name in asmdefs if name.startswith("Hecton8.")),
        "Cycles": cycles,
        "Assemblies": asmdefs,
    }
    return violations, graph_data


def load_exclusions(path_candidates: tuple[Path, ...]) -> list[tuple[str, str, str]]:
    rules: list[tuple[str, str, str]] = []
    for path in path_candidates:
        if not path.exists():
            continue
        for line in read_text(path).splitlines():
            stripped = line.strip()
            if not stripped or stripped.startswith("#") or stripped.lower().startswith("path_pattern"):
                continue
            parts = [part.strip() for part in stripped.split(",", 2)]
            if len(parts) >= 2:
                rules.append((parts[0].replace("\\", "/"), parts[1], parts[2] if len(parts) > 2 else ""))
    return rules


def apply_exclusions(violations: list[Violation], exclusions: list[tuple[str, str, str]]) -> list[Violation]:
    if not exclusions:
        return violations
    kept: list[Violation] = []
    for violation in violations:
        suppressed = False
        for path_pattern, rule_pattern, _reason in exclusions:
            if re.search(path_pattern, violation.file) and re.search(rule_pattern, violation.rule):
                suppressed = True
                break
        if not suppressed:
            kept.append(violation)
    return kept


def score_report(violations: list[Violation], graph_data: dict[str, object], file_counts: dict[str, int]) -> dict[str, object]:
    counts: dict[str, int] = {}
    for violation in violations:
        counts[violation.rule] = counts.get(violation.rule, 0) + 1

    def c(prefix: str) -> int:
        return sum(value for key, value in counts.items() if key.startswith(prefix))

    critical = sum(1 for v in violations if v.severity == "CRITICAL")
    errors = sum(1 for v in violations if v.severity == "ERROR")
    warns = sum(1 for v in violations if v.severity == "WARN")

    data_sovereignty = clamp01(1.0 - c("DataSovereignty") * 0.08 - c("CS1612") * 0.015)
    memory_alignment = clamp01(1.0 - c("MemoryAlignment.Pack1") * 0.22 - c("MemoryAlignment.Misaligned") * 0.12 - c("MemoryAlignment.Missing") * 0.018)
    zero_gc = clamp01(1.0 - c("ZeroGC") * 0.025)
    authority = clamp01(1.0 - c("GlobalAuthority") * 0.035)
    aup = clamp01(1.0 - c("AUP") * 0.045)
    job_fence = clamp01(1.0 - c("JobFence") * 0.035)
    dependency = clamp01(1.0 - c("Assembly.Cyclic") * 0.30 - c("Assembly.CoreGravityWell") * 0.006)

    h_phi = (
        data_sovereignty * 0.22
        + memory_alignment * 0.22
        + zero_gc * 0.16
        + authority * 0.16
        + aup * 0.08
        + job_fence * 0.08
        + dependency * 0.08
    )
    if c("MemoryAlignment.Pack1") > 0:
        h_phi = min(h_phi, 0.35)
    if c("DataSovereignty") > 0:
        h_phi = min(h_phi, 0.55)
    if c("Assembly.Cyclic") > 0:
        h_phi = min(h_phi, 0.40)
    if c("GlobalAuthority.GlobalRegistryGetHotPath") > 0:
        h_phi = min(h_phi, 0.60)

    sorted_violations = sorted(violations, key=lambda v: v.key())
    return {
        "Schema": "hecton8.h_phi_static_final.v1",
        "GeneratedUtc": "STATIC_SOURCE_DETERMINISTIC",
        "EvidenceClass": "STATIC_SOURCE",
        "HPhiStatic": round(clamp01(h_phi), 9),
        "DataSovereignty": round(data_sovereignty, 9),
        "MemoryAlignment": round(memory_alignment, 9),
        "ZeroGCPurity": round(zero_gc, 9),
        "GlobalAuthorityPurity": round(authority, 9),
        "AupIntegrity": round(aup, 9),
        "JobFenceDiscipline": round(job_fence, 9),
        "DependencyIntegrity": round(dependency, 9),
        "Threshold": 0.85,
        "ViolationCount": len(sorted_violations),
        "CriticalViolationCount": critical,
        "ErrorViolationCount": errors,
        "WarningViolationCount": warns,
        "CSharpFilesScanned": file_counts.get("cs", 0),
        "AsmdefFilesScanned": file_counts.get("asmdef", 0),
        "ShaderFilesScanned": file_counts.get("shader", 0),
        "ScanElapsedMs": 0.0,
        "ScanTimingEvidence": "CONSOLE_ONLY_DETERMINISTIC_JSON_OMITS_WALL_CLOCK",
        "AssemblyCount": int(graph_data.get("AssemblyCount", 0)),
        "FirstPartyAssemblyCount": int(graph_data.get("FirstPartyAssemblyCount", 0)),
        "RuleCounts": {key: counts[key] for key in sorted(counts)},
        "Violations": [violation.to_dict() for violation in sorted_violations],
    }


def clamp01(value: float) -> float:
    if value < 0.0:
        return 0.0
    if value > 1.0:
        return 1.0
    return value


def write_json_report(report: dict[str, object], path: Path = REPORT_PATH) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(report, ensure_ascii=True, indent=2, sort_keys=True)
    path.write_text(text + "\n", encoding="utf-8")


def mermaid_graph(graph_data: dict[str, object], limit: int = 280) -> str:
    assemblies = graph_data.get("Assemblies", {})
    if not isinstance(assemblies, dict):
        return "graph LR\n"
    lines = ["graph LR"]
    emitted = 0
    for name in sorted(assemblies):
        data = assemblies[name]
        if not isinstance(data, dict):
            continue
        refs = data.get("references", [])
        if not isinstance(refs, list):
            continue
        for ref in sorted(str(item) for item in refs):
            if not (name.startswith("Hecton8.") or ref.startswith("Hecton8.")):
                continue
            safe_name = asm_node(name)
            safe_ref = asm_node(ref)
            lines.append(f"  {safe_name}[\"{name}\"] --> {safe_ref}[\"{ref}\"]")
            emitted += 1
            if emitted >= limit:
                lines.append(f"  Omitted[\"edges omitted after {limit} for readability\"]")
                return "\n".join(lines)
    return "\n".join(lines)


def asm_node(name: str) -> str:
    return re.sub(r"[^A-Za-z0-9_]", "_", name)


def update_atlas(report: dict[str, object], graph_data: dict[str, object]) -> None:
    HPHI_ATLAS_PATH.parent.mkdir(parents=True, exist_ok=True)
    mermaid = mermaid_graph(graph_data)
    top_rules = report.get("RuleCounts", {})
    if not isinstance(top_rules, dict):
        top_rules = {}
    rule_lines = [f"- {key}: {top_rules[key]}" for key in sorted(top_rules)]
    if not rule_lines:
        rule_lines = ["- none"]
    atlas = "\n".join(
        [
            "# PROJECT_ATLAS_HPHI",
            "",
            "Status: AUTOGENERATED STATIC SOURCE / RUNTIME PENDING",
            "Generator: Tools/HectonPhiStaticAudit.py",
            "Owner: SHINOBU_259",
            "",
            "<!-- AUTO_GEN_ATLAS_START:SHINOBU_259 -->",
            "## H-Phi Static Score",
            "",
            f"- HPhiStatic: {report['HPhiStatic']}",
            f"- DataSovereignty: {report['DataSovereignty']}",
            f"- MemoryAlignment: {report['MemoryAlignment']}",
            f"- ZeroGCPurity: {report['ZeroGCPurity']}",
            f"- GlobalAuthorityPurity: {report['GlobalAuthorityPurity']}",
            f"- AupIntegrity: {report['AupIntegrity']}",
            f"- JobFenceDiscipline: {report['JobFenceDiscipline']}",
            f"- DependencyIntegrity: {report['DependencyIntegrity']}",
            f"- ViolationCount: {report['ViolationCount']}",
            "",
            "## Rule Counts",
            "",
            *rule_lines,
            "",
            "## Assembly Dependency Graph",
            "",
            "```mermaid",
            mermaid,
            "```",
            "<!-- AUTO_GEN_ATLAS_END:SHINOBU_259 -->",
            "",
        ]
    )
    HPHI_ATLAS_PATH.write_text(atlas, encoding="utf-8")

    dep_graph = "\n".join(
        [
            "# HECTON-8 Dependency Graph",
            "",
            "Status: AUTOGENERATED STATIC SOURCE / RUNTIME PENDING",
            "Generator: Tools/HectonPhiStaticAudit.py",
            "",
            "<!-- AUTO_GEN_DEPENDENCY_GRAPH_START:SHINOBU_259 -->",
            "```mermaid",
            mermaid,
            "```",
            "<!-- AUTO_GEN_DEPENDENCY_GRAPH_END:SHINOBU_259 -->",
            "",
        ]
    )
    DEPENDENCY_GRAPH_PATH.parent.mkdir(parents=True, exist_ok=True)
    DEPENDENCY_GRAPH_PATH.write_text(dep_graph, encoding="utf-8")
    DEPENDENCY_GRAPH_JSON_PATH.write_text(json.dumps(graph_data, ensure_ascii=True, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def generate_mock_codebase(target: Path, count: int = 500) -> None:
    if target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True, exist_ok=True)
    asmdef = {
        "name": "Hecton8.Mock.BadRuntime",
        "references": ["Hecton8.Core", "Hecton8.Mock.BadRuntime"],
        "includePlatforms": [],
        "excludePlatforms": [],
        "allowUnsafeCode": False,
        "autoReferenced": True,
        "overrideReferences": False,
        "precompiledReferences": [],
        "defineConstraints": [],
        "versionDefines": [],
        "noEngineReferences": False,
    }
    (target / "Hecton8.Mock.BadRuntime.asmdef").write_text(json.dumps(asmdef, indent=2), encoding="utf-8")
    for i in range(count):
        folder = target / ("Physics" if i % 3 == 0 else "AI" if i % 3 == 1 else "Runtime")
        folder.mkdir(parents=True, exist_ok=True)
        code = f"""
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Hecton8.Mock
{{
    [StructLayout(LayoutKind.Explicit, Pack=1)]
    public struct BadSignal{i} : ISignal
    {{
        [FieldOffset(1)] public double BadDouble;
        public int Hidden {{ get; set; }}
    }}

    public sealed class BadRuntime{i} : MonoBehaviour, IJob
    {{
        private NativeArray<int> _buffer;

        private void Update()
        {{
            for (int j = 0; j < 4; j++)
            {{
                GlobalRegistry.Get<object>();
                HectonEventBus.Publish(new object());
                var x = new List<int>();
                Debug.Log($\"bad {{j}}\");
                float d = Vector3.Distance(transform.position, Vector3.zero);
            }}
            _buffer = new NativeArray<int>(16, Allocator.Persistent);
            default(JobHandle).Complete();
        }}

        public void Execute()
        {{
            var y = new int[4];
        }}
    }}
}}
"""
        (folder / f"BadRuntime{i:04d}.cs").write_text(code, encoding="utf-8")


def install_precommit_hook() -> None:
    HOOK_PATH.parent.mkdir(parents=True, exist_ok=True)
    hook = "\n".join(
        [
            "#!/bin/sh",
            "# HECTON-8 SHINOBU_259 static H-Phi gate.",
            "python Tools/HectonPhiStaticAudit.py --changed-only --threshold 0.85",
            "exit $?",
            "",
        ]
    )
    HOOK_PATH.write_text(hook, encoding="utf-8")
    try:
        HOOK_PATH.chmod(0o755)
    except OSError:
        pass


def run_scan(changed_only: bool = False, source_root: Path = SOURCE_ROOT, asm_roots: tuple[Path, ...] | None = None) -> tuple[dict[str, object], dict[str, object], float]:
    start = time.perf_counter()
    if asm_roots is None:
        asm_roots = (ROOT / "Assets", ROOT / "Packages")
    cs_files = iter_project_files(("*.cs",), (source_root,), changed_only=changed_only)
    asmdef_files = iter_project_files(("*.asmdef",), asm_roots, changed_only=changed_only)
    shader_files = iter_project_files(("*.shader",), asm_roots, changed_only=changed_only)

    violations: list[Violation] = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(2, min(32, (os.cpu_count() or 4)))) as executor:
        for file_violations in executor.map(scan_cs_path, cs_files):
            violations.extend(file_violations)
        for shader_violations in executor.map(scan_shader, shader_files):
            violations.extend(shader_violations)

    asm_violations, graph_data = scan_asmdefs(asmdef_files)
    violations.extend(asm_violations)
    exclusions = load_exclusions(DEFAULT_EXCLUSION_PATHS)
    violations = apply_exclusions(violations, exclusions)
    elapsed_ms = (time.perf_counter() - start) * 1000.0
    report = score_report(
        violations,
        graph_data,
        {"cs": len(cs_files), "asmdef": len(asmdef_files), "shader": len(shader_files)},
    )
    return report, graph_data, elapsed_ms


def scan_cs_path(path: Path) -> list[Violation]:
    try:
        return scan_cs_model(read_model(path))
    except Exception as exc:
        rel = normalize_path(path)
        return [Violation(rel, 1, "Scanner.ParseFailure", "ERROR", f"Parser failed: {exc}", "", "")]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--changed-only", action="store_true", help="Scan staged changed files only.")
    parser.add_argument("--threshold", type=float, default=0.85, help="CI H-Phi threshold.")
    parser.add_argument("--no-fail", action="store_true", help="Write reports but always exit 0.")
    parser.add_argument("--no-atlas", action="store_true", help="Do not update Docs/Generated H-Phi atlas or dependency graph artifacts.")
    parser.add_argument("--source-root", type=str, default="", help="Override C# source root for isolated scanner tests.")
    parser.add_argument("--generate-mock-codebase", type=str, default="", help="Create a 500-file mock violating codebase at this path.")
    parser.add_argument("--install-hook", action="store_true", help="Install .git/hooks/pre-commit gate.")
    args = parser.parse_args()

    if args.generate_mock_codebase:
        generate_mock_codebase((ROOT / args.generate_mock_codebase).resolve())
        return 0
    if args.install_hook:
        install_precommit_hook()
        return 0

    source_root = (ROOT / args.source_root).resolve() if args.source_root else SOURCE_ROOT
    asm_roots = ((ROOT / args.source_root).resolve(),) if args.source_root else None
    report, graph_data, elapsed_ms = run_scan(changed_only=args.changed_only, source_root=source_root, asm_roots=asm_roots)
    report["Threshold"] = args.threshold
    write_json_report(report)
    if not args.no_atlas:
        update_atlas(report, graph_data)

    print(
        "H_PHI_STATIC "
        f"score={report['HPhiStatic']} violations={report['ViolationCount']} "
        f"critical={report['CriticalViolationCount']} elapsed_ms={elapsed_ms:.3f}"
    )
    if args.no_fail:
        return 0
    return 0 if float(report["HPhiStatic"]) >= args.threshold else 1


if __name__ == "__main__":
    raise SystemExit(main())
