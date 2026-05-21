#!/usr/bin/env python3
"""Static risk audit for HECTON-8 polish/portability mandates.

This tool is intentionally conservative: it reports broad source pressure
without pretending to prove runtime cost. Default exit is zero unless explicit
fail flags are supplied.
"""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_REPORT_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "PolishMandateStaticAudit_HFI_AUDIT.md"
DEFAULT_JSON_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "PolishMandateStaticAudit_HFI_AUDIT.json"
SCHEMA = "hecton8.polish_mandate_static_audit.v1"

SKIP_DIR_NAMES = {
    ".git",
    ".vs",
    "__pycache__",
    "bin",
    "obj",
    "Library",
    "Temp",
}

LINE_PATTERNS: dict[str, re.Pattern[str]] = {
    "packOne": re.compile(r"\[StructLayout[^\]]*\bPack\s*=\s*1\b"),
    "privateNativeCollectionField": re.compile(
        r"^\s*private\s+(?:static\s+|readonly\s+|volatile\s+|unsafe\s+)*"
        r"Native(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<"
    ),
    "jobHandleComplete": re.compile(r"\.Complete\s*\("),
    "unityUpdateMethod": re.compile(
        r"^\s*(?:private|protected|public|internal)?\s*"
        r"(?:void|async\s+void)\s+(?:Update|FixedUpdate|LateUpdate)\s*\("
    ),
    "unityRandom": re.compile(r"\bUnityEngine\.Random\b|\bRandom\.(?:Range|value|insideUnit)"),
    "unityTimeCritical": re.compile(r"\bTime\.(?:deltaTime|fixedDeltaTime|frameCount|time)\b"),
    "linqSurface": re.compile(r"\busing\s+System\.Linq\b|\.(?:Where|Select|Any|First|FirstOrDefault|ToList)\s*\("),
    "binaryHardwareSwitch": re.compile(
        r"\b(?:isLowEnd|IsLowEnd|LowEnd|HighEnd|UltraTier|QualityTier|HardwareTier|DeviceTier|"
        r"StandaloneQuest|QuestOnly|PcOnly)\b"
    ),
    "globalQualityWeight": re.compile(r"\bGlobalQualityWeight\b"),
    "noAlias": re.compile(r"\[NoAlias\]"),
}

BURST_ATTR_RE = re.compile(r"\[BurstCompile(?P<body>[^\]]*)\]", re.MULTILINE | re.DOTALL)
STRUCT_DECL_RE = re.compile(r"\b(?:public|private|internal|protected)?\s*(?:readonly\s+)?(?:partial\s+)?struct\s+\w+")
AUTO_PROPERTY_RE = re.compile(r"\{\s*get\s*;\s*(?:private\s+)?set\s*;")
BINARY_HARDWARE_CONTROL_RE = re.compile(r"^\s*(?:if|else\s+if|switch|case|return|while|for)\b|[?:]")
BINARY_HARDWARE_EXPLICIT_TOKEN_RE = re.compile(
    r"\b(?:isLowEnd|IsLowEnd|LowEnd|HighEnd|UltraTier|DeviceTier|StandaloneQuest|QuestOnly|PcOnly)\b"
)
BINARY_HARDWARE_TIER_TOKEN_RE = re.compile(r"\b(?:QualityTier|HardwareTier)\b")
BINARY_HARDWARE_TIER_COMPARISON_RE = re.compile(
    r"\b(?:QualityTier|HardwareTier)\b\s*(?<![=>])(?:==|!=|<=|>=|<|>)(?![=>])|"
    r"(?<![=>])(?:==|!=|<=|>=|<|>)(?![=>])\s*(?:\w+\.)*\b(?:QualityTier|HardwareTier)\b"
)
BINARY_HARDWARE_SWITCH_RE = re.compile(r"^\s*(?:switch|case)\b")
PRIVATE_NATIVE_OWNER_LOCAL_SCRATCH_RE = re.compile(
    r"(?:scratch|openSet|closedSet|queue|commands|hits|slots|sortRecords|toLoad|toUnload)",
    re.IGNORECASE,
)
PRIVATE_NATIVE_BLACKBOX_RE = re.compile(r"(?:blackBox|black_box|telemetry|Telemetry|Ring|ring)")
PRIVATE_NATIVE_VAULT_ALIAS_RE = re.compile(
    r"\b(?:Vault alias|GlobalDataVault owns backing memory|owner owns backing memory)\b",
    re.IGNORECASE,
)
PRIVATE_NATIVE_STATIC_QUEUE_RE = re.compile(r"^\s*private\s+static\s+NativeQueue\s*<")
PRIVATE_NATIVE_DECL_RE = re.compile(
    r"^\s*private\s+(?P<modifiers>(?:static\s+|readonly\s+|volatile\s+|unsafe\s+)*)"
    r"Native(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<[^>]+>\s*(?P<tail>.*)"
)
PRIVATE_NATIVE_METHOD_RETURN_RE = re.compile(r"^\w+\s*(?:<[^>]+>)?\s*\(")
PRIVATE_NATIVE_QA_DEV_RE = re.compile(
    r"(?:/QA/|/Dev/|SmokeTester|Fuzzer|Harness|Baker|Validator|XRay|TunerWindow)",
    re.IGNORECASE,
)
PRIVATE_NATIVE_SIGNAL_BRIDGE_RE = re.compile(
    r"(?:Signal|Signals|Event|Events|Bus|GlobalSignals|_pending|_nextFrame|_signals|_events)",
    re.IGNORECASE,
)
PRIVATE_NATIVE_VAULT_RESOLVER_RE = re.compile(
    r"(?:Vault alias|GlobalDataVault owns backing memory|owner owns backing memory|ResolveVault|VaultGenerationHandle|IDataVault|BufferID|SystemID|TryResolveHandle)",
    re.IGNORECASE,
)
PRIVATE_NATIVE_CLASSIFICATION_KEYS = (
    "privateNativeCollectionVaultAlias",
    "privateNativeCollectionStaticQueueLane",
    "privateNativeCollectionBlackBoxTelemetry",
    "privateNativeCollectionOwnerLocalScratch",
    "privateNativeCollectionUnclassified",
)
PRIVATE_NATIVE_DECLARATION_KIND_KEYS = (
    "privateNativeDeclarationField",
    "privateNativeDeclarationMethodReturn",
    "privateNativeDeclarationAmbiguous",
)
PRIVATE_NATIVE_BUILD_SURFACE_KEYS = (
    "privateNativeBuildPlayerRuntime",
    "privateNativeBuildEditorOnly",
    "privateNativeBuildQaDevProof",
)
PRIVATE_NATIVE_PRIMARY_RISK_BUCKET_KEYS = (
    "privateNativeRiskMethodReturningNativeCollection",
    "privateNativeRiskJobStructNativeView",
    "privateNativeRiskStaticSignalOrEventBridge",
    "privateNativeRiskStaticGlobalNativeState",
    "privateNativeRiskVaultAliasOrVaultResolver",
    "privateNativeRiskOwnerLocalRuntimeNativeState",
    "privateNativeRiskEditorOrProofNativeState",
    "privateNativeRiskUnclassifiedNativeCollection",
)
PUBLIC_API_NATIVE_COLLECTION_RE = re.compile(
    r"^\s*(?:public|internal|protected)\s+"
    r"(?:static\s+|readonly\s+|virtual\s+|override\s+|sealed\s+|unsafe\s+)*"
)
NATIVE_COLLECTION_TOKEN_RE = re.compile(r"\bNative(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<")
NATIVE_COLLECTION_READONLY_RE = re.compile(r"\bNativeArray\s*<[^>]+>\s*\.ReadOnly\b")
PUBLIC_NATIVE_RETURN_RE = re.compile(
    r"^\s*(?:public|internal|protected)\s+"
    r"(?:static\s+|readonly\s+|virtual\s+|override\s+|sealed\s+|unsafe\s+)*"
    r"Native(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<"
)
PUBLIC_NATIVE_OUT_REF_RE = re.compile(r"\b(?:out|ref)\s+Native(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<")
NATIVE_API_EXPOSURE_KIND_KEYS = (
    "nativeApiExposureMutableReturn",
    "nativeApiExposureOutRefMutable",
    "nativeApiExposureAmbiguousMutable",
)
NATIVE_API_EXPOSURE_BUILD_SURFACE_KEYS = (
    "nativeApiExposureBuildPlayerRuntime",
    "nativeApiExposureBuildEditorOnly",
    "nativeApiExposureBuildQaDevProof",
)
NATIVE_API_EXPOSURE_RISK_BUCKET_KEYS = (
    "nativeApiRiskCoreVaultOrAllocatorSurface",
    "nativeApiRiskEditorOrProofSurface",
    "nativeApiRiskRuntimeDiagnosticNamedMutableView",
    "nativeApiRiskRuntimeOutRefMutableView",
    "nativeApiRiskRuntimeReturnMutableView",
    "nativeApiRiskRuntimeAmbiguousMutableView",
)
NATIVE_API_DIAGNOSTIC_NAME_RE = re.compile(
    r"(?:ForEditor|Debug|Diagnostic|Readback|Tuner|Snapshot|Telemetry|Inspector|Gizmo)",
    re.IGNORECASE,
)
UNITY_TIME_KIND_KEYS = (
    "unityTimeFrameCount",
    "unityTimeDelta",
    "unityTimeWallClock",
)
UNITY_TIME_BUILD_SURFACE_KEYS = (
    "unityTimeBuildPlayerRuntime",
    "unityTimeBuildEditorOnly",
    "unityTimeBuildQaDevProof",
)
UNITY_TIME_RISK_BUCKET_KEYS = (
    "unityTimeRiskEditorOrProof",
    "unityTimeRiskFrameStampOrTelemetry",
    "unityTimeRiskCooldownOrPerfLog",
    "unityTimeRiskGameplayDelta",
    "unityTimeRiskGameplayWallClock",
)
UNITY_TIME_FRAME_RE = re.compile(r"\bTime\.frameCount\b")
UNITY_TIME_DELTA_RE = re.compile(r"\bTime\.(?:deltaTime|fixedDeltaTime)\b")
UNITY_TIME_WALL_RE = re.compile(r"\bTime\.time\b")
UNITY_TIME_DIAGNOSTIC_RE = re.compile(
    r"(?:warning|warn|log|debug|perf|profile|watchdog|cooldown|next|telemetry|dump|diagnostic)",
    re.IGNORECASE,
)
STRING_LITERAL_RE = re.compile(
    r"""
    (?:
        (?:\$?@|@\$)"(?:""|[^"])*"
        |
        \$?"(?:\\.|[^"\\])*"
    )
    """,
    re.VERBOSE,
)


@dataclass(frozen=True)
class Finding:
    path: str
    line: int
    text: str


def normalize_path(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.parts)


def iter_cs_files(source_root: Path) -> list[Path]:
    return [
        path
        for path in sorted(source_root.rglob("*.cs"))
        if not should_skip(path.relative_to(source_root))
    ]


def strip_line_comment(line: str) -> str:
    return line.split("//", 1)[0]


def strip_string_literals(line: str) -> str:
    return STRING_LITERAL_RE.sub('""', line)


def is_binary_hardware_switch_line(scan_code: str) -> bool:
    if LINE_PATTERNS["binaryHardwareSwitch"].search(scan_code) is None:
        return False
    if BINARY_HARDWARE_SWITCH_RE.search(scan_code) is not None:
        return True
    if BINARY_HARDWARE_EXPLICIT_TOKEN_RE.search(scan_code) is not None:
        return BINARY_HARDWARE_CONTROL_RE.search(scan_code) is not None
    if BINARY_HARDWARE_TIER_TOKEN_RE.search(scan_code) is not None:
        return (
            BINARY_HARDWARE_CONTROL_RE.search(scan_code) is not None
            and BINARY_HARDWARE_TIER_COMPARISON_RE.search(scan_code) is not None
        )
    return False


def classify_private_native_collection(raw_line: str) -> str:
    if PRIVATE_NATIVE_VAULT_ALIAS_RE.search(raw_line):
        return "privateNativeCollectionVaultAlias"
    if PRIVATE_NATIVE_STATIC_QUEUE_RE.search(raw_line):
        return "privateNativeCollectionStaticQueueLane"
    if PRIVATE_NATIVE_BLACKBOX_RE.search(raw_line):
        return "privateNativeCollectionBlackBoxTelemetry"
    if PRIVATE_NATIVE_OWNER_LOCAL_SCRATCH_RE.search(raw_line):
        return "privateNativeCollectionOwnerLocalScratch"
    return "privateNativeCollectionUnclassified"


def classify_private_native_declaration_kind(scan_code: str) -> str:
    match = PRIVATE_NATIVE_DECL_RE.search(scan_code)
    if match is None:
        return "privateNativeDeclarationAmbiguous"

    tail = match.group("tail").strip()
    paren_index = tail.find("(")
    semicolon_index = tail.find(";")
    if semicolon_index >= 0 and (paren_index < 0 or semicolon_index < paren_index):
        return "privateNativeDeclarationField"
    if PRIVATE_NATIVE_METHOD_RETURN_RE.search(tail):
        return "privateNativeDeclarationMethodReturn"
    return "privateNativeDeclarationAmbiguous"


def classify_private_native_build_surface(rel: str) -> str:
    normalized = rel.replace("\\", "/")
    if "/Editor/" in normalized or normalized.endswith(".Editor.cs"):
        return "privateNativeBuildEditorOnly"
    if PRIVATE_NATIVE_QA_DEV_RE.search(normalized):
        return "privateNativeBuildQaDevProof"
    return "privateNativeBuildPlayerRuntime"


def private_native_context(lines: list[str] | None, line_number: int, before: int, after: int) -> str:
    if lines is None:
        return ""
    start = max(0, line_number - 1 - before)
    end = min(len(lines), line_number + after)
    return "\n".join(lines[start:end])


def private_native_preceding_comments(lines: list[str] | None, line_number: int, limit: int = 3) -> str:
    if lines is None:
        return ""
    comments: list[str] = []
    cursor = line_number - 2
    while cursor >= 0 and len(comments) < limit:
        stripped = lines[cursor].strip()
        if stripped == "":
            cursor -= 1
            continue
        if stripped.startswith("//"):
            comments.append(stripped)
            cursor -= 1
            continue
        break
    comments.reverse()
    return "\n".join(comments)


def is_private_native_job_struct_view(lines: list[str] | None, line_number: int) -> bool:
    context = private_native_context(lines, line_number, before=20, after=4)
    return "[BurstCompile" in context or re.search(r"\bIJob(?:For|ParallelFor|Chunk)?\b", context) is not None


def classify_private_native_primary_risk(
    rel: str,
    raw_line: str,
    scan_code: str,
    lines: list[str] | None,
    line_number: int,
    declaration_kind: str,
    build_surface: str,
) -> str:
    if declaration_kind == "privateNativeDeclarationMethodReturn":
        return "privateNativeRiskMethodReturningNativeCollection"
    if build_surface != "privateNativeBuildPlayerRuntime":
        return "privateNativeRiskEditorOrProofNativeState"
    if is_private_native_job_struct_view(lines, line_number):
        return "privateNativeRiskJobStructNativeView"
    if "static" in (PRIVATE_NATIVE_DECL_RE.search(scan_code).group("modifiers") if PRIVATE_NATIVE_DECL_RE.search(scan_code) else ""):
        if "NativeQueue" in scan_code or PRIVATE_NATIVE_SIGNAL_BRIDGE_RE.search(rel) or PRIVATE_NATIVE_SIGNAL_BRIDGE_RE.search(raw_line):
            return "privateNativeRiskStaticSignalOrEventBridge"
        return "privateNativeRiskStaticGlobalNativeState"
    vault_context = raw_line + "\n" + private_native_preceding_comments(lines, line_number)
    if PRIVATE_NATIVE_VAULT_RESOLVER_RE.search(vault_context) or "ResolveVault" in scan_code:
        return "privateNativeRiskVaultAliasOrVaultResolver"
    if declaration_kind == "privateNativeDeclarationField":
        return "privateNativeRiskOwnerLocalRuntimeNativeState"
    return "privateNativeRiskUnclassifiedNativeCollection"


def record_private_native_classification(
    results: dict[str, list[Finding]],
    finding: Finding,
    rel: str,
    line_number: int,
    raw_line: str,
    scan_code: str,
    lines: list[str] | None,
) -> None:
    results[classify_private_native_collection(raw_line)].append(finding)
    declaration_kind = classify_private_native_declaration_kind(scan_code)
    build_surface = classify_private_native_build_surface(rel)
    primary_risk = classify_private_native_primary_risk(
        rel,
        raw_line,
        scan_code,
        lines,
        line_number,
        declaration_kind,
        build_surface,
    )
    results[declaration_kind].append(finding)
    results[build_surface].append(finding)
    results[primary_risk].append(finding)


def collect_signature(lines: list[str], line_number: int, max_lines: int = 12) -> str:
    parts: list[str] = []
    balance = 0
    for cursor in range(line_number - 1, min(len(lines), line_number - 1 + max_lines)):
        code = strip_string_literals(strip_line_comment(lines[cursor])).strip()
        if not code:
            continue
        parts.append(code)
        balance += code.count("(") - code.count(")")
        if "=>" in code or "{" in code or ";" in code or (balance <= 0 and ")" in code):
            break
    return " ".join(parts)


def is_public_mutable_native_api_exposure(lines: list[str], line_number: int, scan_code: str) -> bool:
    if PUBLIC_API_NATIVE_COLLECTION_RE.search(scan_code) is None:
        return False

    signature = collect_signature(lines, line_number)
    if NATIVE_COLLECTION_TOKEN_RE.search(signature) is None:
        return False
    if NATIVE_COLLECTION_READONLY_RE.search(signature) is not None:
        return False

    returns_native_view = PUBLIC_NATIVE_RETURN_RE.search(signature) is not None and (
        "(" in signature or "=>" in signature
    )
    if "=>" in signature and not returns_native_view:
        return False
    return returns_native_view or PUBLIC_NATIVE_OUT_REF_RE.search(signature) is not None


def classify_native_api_exposure_kind(signature: str) -> str:
    returns_native_view = PUBLIC_NATIVE_RETURN_RE.search(signature) is not None and (
        "(" in signature or "=>" in signature
    )
    if returns_native_view:
        return "nativeApiExposureMutableReturn"
    if PUBLIC_NATIVE_OUT_REF_RE.search(signature) is not None:
        return "nativeApiExposureOutRefMutable"
    return "nativeApiExposureAmbiguousMutable"


def classify_native_api_build_surface(rel: str) -> str:
    private_surface = classify_private_native_build_surface(rel)
    if private_surface == "privateNativeBuildEditorOnly":
        return "nativeApiExposureBuildEditorOnly"
    if private_surface == "privateNativeBuildQaDevProof":
        return "nativeApiExposureBuildQaDevProof"
    return "nativeApiExposureBuildPlayerRuntime"


def classify_native_api_primary_risk(rel: str, signature: str, kind: str, build_surface: str) -> str:
    if build_surface != "nativeApiExposureBuildPlayerRuntime":
        return "nativeApiRiskEditorOrProofSurface"

    normalized = rel.replace("\\", "/")
    if (
        "/Core/Memory/" in normalized
        or normalized.endswith("/Core/HectonArenaAllocator.cs")
        or normalized.endswith("/Core/Memory/H8Memory.cs")
        or normalized.endswith("/Core/Memory/GlobalDataVault.cs")
    ):
        return "nativeApiRiskCoreVaultOrAllocatorSurface"

    if NATIVE_API_DIAGNOSTIC_NAME_RE.search(signature) is not None:
        return "nativeApiRiskRuntimeDiagnosticNamedMutableView"

    if kind == "nativeApiExposureOutRefMutable":
        return "nativeApiRiskRuntimeOutRefMutableView"
    if kind == "nativeApiExposureMutableReturn":
        return "nativeApiRiskRuntimeReturnMutableView"
    return "nativeApiRiskRuntimeAmbiguousMutableView"


def record_native_api_exposure_classification(
    results: dict[str, list[Finding]],
    finding: Finding,
    rel: str,
    signature: str,
) -> None:
    kind = classify_native_api_exposure_kind(signature)
    build_surface = classify_native_api_build_surface(rel)
    risk = classify_native_api_primary_risk(rel, signature, kind, build_surface)
    results[kind].append(finding)
    results[build_surface].append(finding)
    results[risk].append(finding)


def classify_unity_time_kind(scan_code: str) -> str:
    if UNITY_TIME_FRAME_RE.search(scan_code) is not None:
        return "unityTimeFrameCount"
    if UNITY_TIME_DELTA_RE.search(scan_code) is not None:
        return "unityTimeDelta"
    return "unityTimeWallClock"


def classify_unity_time_build_surface(rel: str) -> str:
    private_surface = classify_private_native_build_surface(rel)
    if private_surface == "privateNativeBuildEditorOnly":
        return "unityTimeBuildEditorOnly"
    if private_surface == "privateNativeBuildQaDevProof":
        return "unityTimeBuildQaDevProof"
    return "unityTimeBuildPlayerRuntime"


def classify_unity_time_primary_risk(raw_line: str, scan_code: str, kind: str, build_surface: str) -> str:
    if build_surface != "unityTimeBuildPlayerRuntime":
        return "unityTimeRiskEditorOrProof"
    if kind == "unityTimeFrameCount":
        return "unityTimeRiskFrameStampOrTelemetry"
    if UNITY_TIME_DIAGNOSTIC_RE.search(raw_line) is not None:
        return "unityTimeRiskCooldownOrPerfLog"
    if kind == "unityTimeDelta":
        return "unityTimeRiskGameplayDelta"
    return "unityTimeRiskGameplayWallClock"


def record_unity_time_classification(
    results: dict[str, list[Finding]],
    finding: Finding,
    rel: str,
    raw_line: str,
    scan_code: str,
) -> None:
    kind = classify_unity_time_kind(scan_code)
    build_surface = classify_unity_time_build_surface(rel)
    risk = classify_unity_time_primary_risk(raw_line, scan_code, kind, build_surface)
    results[kind].append(finding)
    results[build_surface].append(finding)
    results[risk].append(finding)


def empty_results() -> dict[str, list[Finding]]:
    results: dict[str, list[Finding]] = {key: [] for key in LINE_PATTERNS}
    results.update(
        {
            "structAutoProperties": [],
            "burstCompile": [],
            "burstMissingCompileSynchronously": [],
            "burstMissingFloatMode": [],
            "burstMissingFloatPrecision": [],
            "nativeCollectionPublicMutableApiExposure": [],
        }
    )
    for key in (
        *PRIVATE_NATIVE_CLASSIFICATION_KEYS,
        *PRIVATE_NATIVE_DECLARATION_KIND_KEYS,
        *PRIVATE_NATIVE_BUILD_SURFACE_KEYS,
        *PRIVATE_NATIVE_PRIMARY_RISK_BUCKET_KEYS,
        *NATIVE_API_EXPOSURE_KIND_KEYS,
        *NATIVE_API_EXPOSURE_BUILD_SURFACE_KEYS,
        *NATIVE_API_EXPOSURE_RISK_BUCKET_KEYS,
        *UNITY_TIME_KIND_KEYS,
        *UNITY_TIME_BUILD_SURFACE_KEYS,
        *UNITY_TIME_RISK_BUCKET_KEYS,
    ):
        results[key] = []
    return results


def record_burst_attribute(
    results: dict[str, list[Finding]],
    rel: str,
    line: int,
    attribute_text: str,
) -> None:
    finding = Finding(rel, line, attribute_text.replace("\n", " ").strip())
    results["burstCompile"].append(finding)
    if "CompileSynchronously" not in attribute_text:
        results["burstMissingCompileSynchronously"].append(finding)
    if "FloatMode" not in attribute_text:
        results["burstMissingFloatMode"].append(finding)
    if "FloatPrecision" not in attribute_text:
        results["burstMissingFloatPrecision"].append(finding)


def record_line_patterns(
    results: dict[str, list[Finding]],
    rel: str,
    line_number: int,
    raw_line: str,
    code: str,
    lines: list[str] | None = None,
) -> None:
    scan_code = strip_string_literals(code)
    checks = (
        ("packOne", "Pack"),
        ("privateNativeCollectionField", "Native"),
        ("jobHandleComplete", ".Complete"),
        ("unityUpdateMethod", "Update"),
        ("unityRandom", "Random"),
        ("unityTimeCritical", "Time."),
        ("linqSurface", "Linq" if "Linq" in code else "."),
        ("globalQualityWeight", "GlobalQualityWeight"),
        ("noAlias", "[NoAlias]"),
    )
    for key, token in checks:
        if token in scan_code and LINE_PATTERNS[key].search(scan_code):
            finding = Finding(rel, line_number, raw_line.strip())
            results[key].append(finding)
            if key == "privateNativeCollectionField":
                record_private_native_classification(
                    results,
                    finding,
                    rel,
                    line_number,
                    raw_line,
                    scan_code,
                    lines,
                )
            if key == "unityTimeCritical":
                record_unity_time_classification(results, finding, rel, raw_line, scan_code)

    if is_binary_hardware_switch_line(scan_code):
        results["binaryHardwareSwitch"].append(Finding(rel, line_number, raw_line.strip()))


def scan_all(files: Iterable[Path]) -> dict[str, list[Finding]]:
    results = empty_results()
    for path in files:
        rel = normalize_path(path)
        lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
        in_struct = False
        depth = 0
        line_count = len(lines)
        line_number = 1
        while line_number <= line_count:
            raw_line = lines[line_number - 1]
            code = strip_line_comment(raw_line)
            scan_code = strip_string_literals(code)

            record_line_patterns(results, rel, line_number, raw_line, code, lines)
            if is_public_mutable_native_api_exposure(lines, line_number, scan_code):
                signature = collect_signature(lines, line_number)
                finding = Finding(rel, line_number, signature)
                results["nativeCollectionPublicMutableApiExposure"].append(
                    finding
                )
                record_native_api_exposure_classification(results, finding, rel, signature)

            if "[BurstCompile" in scan_code:
                attr_parts = [scan_code.strip()]
                cursor = line_number
                while "]" not in attr_parts[-1] and cursor < line_count and cursor < line_number + 8:
                    cursor += 1
                    attr_parts.append(strip_string_literals(strip_line_comment(lines[cursor - 1])).strip())
                record_burst_attribute(results, rel, line_number, " ".join(attr_parts))

            if not in_struct and STRUCT_DECL_RE.search(scan_code):
                in_struct = True
                depth = 0

            if in_struct and AUTO_PROPERTY_RE.search(scan_code):
                results["structAutoProperties"].append(Finding(rel, line_number, raw_line.strip()))

            if in_struct:
                depth += scan_code.count("{") - scan_code.count("}")
                if depth <= 0 and "}" in scan_code:
                    in_struct = False
                    depth = 0

            line_number += 1

    return results


def scan_lines(files: Iterable[Path]) -> dict[str, list[Finding]]:
    return {key: value for key, value in scan_all(files).items() if key in LINE_PATTERNS}


def scan_burst(files: Iterable[Path]) -> dict[str, list[Finding]]:
    results = scan_all(files)
    # Kept for compatibility with older tests; build_payload uses scan_all.
    return {
        key: results[key]
        for key in (
            "burstCompile",
            "burstMissingCompileSynchronously",
            "burstMissingFloatMode",
            "burstMissingFloatPrecision",
        )
    }


def scan_struct_properties(files: Iterable[Path]) -> list[Finding]:
    findings: list[Finding] = []
    for path in files:
        rel = normalize_path(path)
        lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
        in_struct = False
        depth = 0
        for line_number, raw_line in enumerate(lines, 1):
            code = strip_line_comment(raw_line)
            if not in_struct and STRUCT_DECL_RE.search(code):
                in_struct = True
                depth = 0

            if in_struct and AUTO_PROPERTY_RE.search(code):
                findings.append(Finding(rel, line_number, raw_line.strip()))

            if in_struct:
                depth += code.count("{") - code.count("}")
                if depth <= 0 and "}" in code:
                    in_struct = False
                    depth = 0
    return findings


def summarize_findings(findings: list[Finding]) -> dict[str, object]:
    by_file: dict[str, int] = {}
    for finding in findings:
        by_file[finding.path] = by_file.get(finding.path, 0) + 1
    top_files = [
        {"path": path, "count": count}
        for path, count in sorted(by_file.items(), key=lambda item: (-item[1], item[0]))[:10]
    ]
    examples = [
        {"path": f.path, "line": f.line, "text": f.text}
        for f in findings[:20]
    ]
    return {
        "matches": len(findings),
        "files": len(by_file),
        "topFiles": top_files,
        "examples": examples,
    }


def build_payload(source_root: Path) -> dict[str, object]:
    files = iter_cs_files(source_root)
    all_findings = scan_all(files)
    categories: dict[str, dict[str, object]] = {}
    for key, findings in all_findings.items():
        categories[key] = summarize_findings(findings)

    return {
        "schema": SCHEMA,
        "sourceRoot": normalize_path(source_root),
        "csFileCount": len(files),
        "categories": categories,
    }


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_markdown(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    categories = payload["categories"]
    if not isinstance(categories, dict):
        raise TypeError("categories payload malformed")

    lines = [
        "# Polish Mandate Static Audit",
        "",
        "Evidence class: STATIC_SOURCE. No Unity import, compile, Play Mode, profiler, GC, memory, player build, or device proof was executed.",
        "",
        f"- Schema: `{payload['schema']}`",
        f"- Source root: `{payload['sourceRoot']}`",
        f"- C# files: `{payload['csFileCount']}`",
        "",
        "## Counts",
        "",
        "| Category | Matches | Files |",
        "|---|---:|---:|",
    ]

    for key in sorted(categories):
        item = categories[key]
        if not isinstance(item, dict):
            continue
        lines.append(f"| `{key}` | {item.get('matches', 0)} | {item.get('files', 0)} |")

    lines.extend(["", "## Top Files", ""])
    for key in sorted(categories):
        item = categories[key]
        if not isinstance(item, dict):
            continue
        top_files = item.get("topFiles") or []
        if not top_files:
            continue
        lines.append(f"### {key}")
        lines.append("")
        lines.append("| Path | Count |")
        lines.append("|---|---:|")
        for top in top_files[:5]:
            if isinstance(top, dict):
                lines.append(f"| `{top.get('path')}` | {top.get('count')} |")
        lines.append("")

    lines.extend(
        [
            "## Interpretation",
            "",
            "- `Pack=1`, private persistent native collections, and Burst attribute drift are platform-portability risks until each hit is classified as cold file-format, owner-local scratch, or hot runtime.",
            "- `jobHandleComplete`, Unity `Update` methods, `Time.*`, and `UnityEngine.Random` are not automatically defects, but they are mandatory review surfaces for gameplay/runtime code.",
            "- Binary hardware switches are suspect unless they are presentation-only or build-time/platform setup. Runtime scalability should flow through continuous `GlobalQualityWeight` curves.",
            "- This audit is a pressure map. It does not mutate code and does not prove frame cost.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def hard_failures(payload: dict[str, object], args: argparse.Namespace) -> list[str]:
    categories = payload["categories"]
    if not isinstance(categories, dict):
        raise TypeError("categories payload malformed")

    failures: list[str] = []
    if args.fail_on_pack_one and int(categories["packOne"]["matches"]) > 0:
        failures.append(f"Pack=1 hits: {categories['packOne']['matches']}")
    if args.fail_on_missing_burst_flags:
        for key in ("burstMissingCompileSynchronously", "burstMissingFloatMode", "burstMissingFloatPrecision"):
            if int(categories[key]["matches"]) > 0:
                failures.append(f"{key}: {categories[key]['matches']}")
    return failures


def print_text(payload: dict[str, object], failures: list[str]) -> None:
    categories = payload["categories"]
    print("Polish mandate static audit")
    print(f"schema={payload['schema']}")
    print(f"sourceRoot={payload['sourceRoot']}")
    print(f"csFiles={payload['csFileCount']}")
    if isinstance(categories, dict):
        for key in sorted(categories):
            item = categories[key]
            if isinstance(item, dict):
                print(f"{key}={item['matches']} files={item['files']}")
    if failures:
        print("status=FAIL")
        for failure in failures:
            print(f"failure={failure}")
    else:
        print("status=PASS_WITH_WARNINGS")


def run(args: argparse.Namespace) -> int:
    payload = build_payload(Path(args.source_root))
    write_json(Path(args.json_path), payload)
    write_markdown(Path(args.report_path), payload)
    failures = hard_failures(payload, args)
    if args.json:
        print(json.dumps(payload, indent=2, sort_keys=True))
    else:
        print_text(payload, failures)
    return 1 if failures else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", default=str(DEFAULT_SOURCE_ROOT))
    parser.add_argument("--report-path", default=str(DEFAULT_REPORT_PATH))
    parser.add_argument("--json-path", default=str(DEFAULT_JSON_PATH))
    parser.add_argument("--json", action="store_true", help="Print JSON payload to stdout.")
    parser.add_argument("--fail-on-pack-one", action="store_true")
    parser.add_argument("--fail-on-missing-burst-flags", action="store_true")
    return run(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
