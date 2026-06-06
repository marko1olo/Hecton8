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
REPO_ROOT_RESOLVED = REPO_ROOT.resolve()
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
TYPE_DECL_RE = re.compile(
    r"^\s*(?P<access>public|private|internal|protected)?\s*"
    r"(?:(?:static|sealed|abstract|partial|readonly|unsafe|ref)\s+)*"
    r"(?:(?:class|struct|interface)\s+(?P<name>\w+)|record\s+(?:class\s+|struct\s+)?(?P<record_name>\w+))"
)
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
PRIVATE_NATIVE_PREWARMED_BANK_RE = re.compile(
    r"(?:RecordBanks|NativeArray<[^>]+>\[\]|prewarmed|borrowed by .*jobs)",
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
    r"Native(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<[^>]+>\s*(?P<readonly>\.ReadOnly)?\s*(?P<tail>.*)"
)
PRIVATE_NATIVE_READONLY_RE = re.compile(
    r"Native(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<[^>]+>\s*\.ReadOnly\b"
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
    r"(?:Vault alias|GlobalDataVault owns backing memory|owner owns backing memory|ResolveVault|ResolveBuffer|VaultGenerationHandle|IDataVault|BufferID|SystemID|TryResolveHandle)",
    re.IGNORECASE,
)
PRIVATE_NATIVE_CLASSIFICATION_KEYS = (
    "privateNativeCollectionVaultAlias",
    "privateNativeCollectionStaticQueueLane",
    "privateNativeCollectionBlackBoxTelemetry",
    "privateNativeCollectionOwnerLocalScratch",
    "privateNativeCollectionPrewarmedNativeBank",
    "privateNativeCollectionReadOnlyView",
    "privateNativeCollectionUnclassified",
)
PRIVATE_NATIVE_DECLARATION_KIND_KEYS = (
    "privateNativeDeclarationField",
    "privateNativeDeclarationMethodReturn",
    "privateNativeDeclarationReadOnlyView",
    "privateNativeDeclarationExpressionProperty",
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
    "privateNativeRiskReadOnlyNativeView",
    "privateNativeRiskUnclassifiedNativeCollection",
)
PUBLIC_API_NATIVE_COLLECTION_RE = re.compile(
    r"^\s*(?:public|internal|protected)\s+"
    r"(?:static\s+|readonly\s+|virtual\s+|override\s+|sealed\s+|unsafe\s+)*"
)
NATIVE_COLLECTION_TOKEN_RE = re.compile(r"\bNative(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<")
NATIVE_COLLECTION_READONLY_RE = re.compile(
    r"\bNative(?:Array|List|HashMap|ParallelHashMap|Queue)\s*<[^>]+>\s*\.ReadOnly\b"
)
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
    "nativeApiRiskRuntimeReadNamedMutableView",
    "nativeApiRiskRuntimeObsoleteMutableCompatibilityView",
    "nativeApiRiskRuntimeDiagnosticNamedMutableView",
    "nativeApiRiskRuntimeOwnerAliasMutableView",
    "nativeApiRiskRuntimeReinterpretMutableView",
    "nativeApiRiskRuntimeWriteLeaseMutableView",
    "nativeApiRiskRuntimeJobAliasMutableView",
    "nativeApiRiskRuntimeNativePayloadMutableView",
    "nativeApiRiskRuntimeDisposeMutableRef",
    "nativeApiRiskRuntimeOutRefMutableView",
    "nativeApiRiskRuntimeReturnMutableView",
    "nativeApiRiskRuntimeAmbiguousMutableView",
)
NATIVE_API_READ_NAMED_RE = re.compile(
    r"\b(?:TryRead|Read|GetRead|TryGetRead|ResolveRead|TryResolveRead)\w*\s*\(",
    re.IGNORECASE,
)
NATIVE_API_OBSOLETE_COMPATIBILITY_RE = re.compile(
    r"(?:\[System\.Obsolete|\[Obsolete|legacy compatibility|legacy mutable|retained for legacy)",
    re.IGNORECASE,
)
NATIVE_API_DIAGNOSTIC_NAME_RE = re.compile(
    r"(?:ForEditor|Debug|Diagnostic|Readback|Tuner|Snapshot|Telemetry|Inspector|Gizmo)",
    re.IGNORECASE,
)
NATIVE_API_OWNER_ALIAS_RE = re.compile(
    r"(?:OwnerAlias|OwnerView|ResolveAlias|ResolveView|TryResolveDragArrays)",
    re.IGNORECASE,
)
NATIVE_API_OWNER_TYPE_CONTEXT_RE = re.compile(
    r"(?:VaultLane|VaultView)",
    re.IGNORECASE,
)
NATIVE_API_REINTERPRET_VIEW_RE = re.compile(
    r"(?:Reinterpret|AsUIntQuantityView)",
    re.IGNORECASE,
)
NATIVE_API_WRITE_LEASE_RE = re.compile(
    r"(?:TryAcquire|Acquire|OpenOrAcquire|WriteLock|WriteBuffer|WriteBuffers|PanelStateWrite|QuestDagWriteBuffer|CsrLanes)",
    re.IGNORECASE,
)
NATIVE_API_JOB_ALIAS_RE = re.compile(
    r"(?:ExtractJobAliases|JobAliases)",
    re.IGNORECASE,
)
NATIVE_API_NATIVE_PAYLOAD_RE = re.compile(
    r"(?:NativePayload|GpuUploadSourceLease|GpuUpload|GraphicsBuffer)",
    re.IGNORECASE,
)
NATIVE_API_DISPOSE_RE = re.compile(
    r"(?:DisposeTracked|DisposeTransientPayload|Dispose.*Native|Release.*Native)",
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
    "unityTimeRiskVisualPresentationClock",
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
UNITY_TIME_VISUAL_PRESENTATION_RE = re.compile(
    r"(?:Visual|Vfx|VFX|Rendering|Render|Camera|Hud|HUD|UI|Light|Lighting|Haze|Caustic|Particle|Fx|FX)",
    re.IGNORECASE,
)
EMPTY_RUNTIME_TICK_METHOD_RE = re.compile(
    r"^\s*(?:public|private|protected|internal)?\s*"
    r"(?P<modifiers>(?:(?:static|virtual|override|sealed|new|unsafe)\s+)*)"
    r"(?:void|async\s+void)\s+"
    r"(?P<name>Update|FixedUpdate|LateUpdate|Tick|FixedTick|SlowTick|ColdTick|FrostTick|"
    r"LateFrameTick|PostFixedTick|PreSimulationTick|SimulationTick|VisualSyncTick|OnUpdate)"
    r"\s*\((?P<params>[^)]*)"
)
EMPTY_COMPILE_UNIT_MARKER_RE = re.compile(
    r"(?:intentionally empty|empty compile unit|retired legacy stubs|shims were removed)",
    re.IGNORECASE,
)
COMPATIBILITY_NOOP_CONTEXT_RE = re.compile(
    r"\bcompatibility\b.{0,80}\bno-?op\b|\bno-?op\b.{0,80}\bcompatibility\b",
    re.IGNORECASE,
)
METHOD_SIGNATURE_RE = re.compile(
    r"^\s*(?:public|private|protected|internal)?\s*"
    r"(?:(?:static|virtual|override|sealed|new|unsafe|async)\s+)*"
    r"[\w.<>,\[\]?]+\s+"
    r"(?P<name>[A-Za-z_]\w*)\s*\("
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
    if not path.is_absolute():
        return path.as_posix()
    try:
        return path.relative_to(repo_root).as_posix()
    except ValueError:
        try:
            root = REPO_ROOT_RESOLVED if repo_root == REPO_ROOT else repo_root.resolve()
            return path.resolve().relative_to(root).as_posix()
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
    marker = line.find("//")
    if marker < 0:
        return line
    return line[:marker]


def strip_string_literals(line: str) -> str:
    if '"' not in line:
        return line
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
    if PRIVATE_NATIVE_READONLY_RE.search(raw_line):
        return "privateNativeCollectionReadOnlyView"
    if PRIVATE_NATIVE_PREWARMED_BANK_RE.search(raw_line):
        return "privateNativeCollectionPrewarmedNativeBank"
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
    if match.group("readonly") is not None:
        return "privateNativeDeclarationReadOnlyView"
    if tail.startswith("[]"):
        return "privateNativeDeclarationField"
    if PRIVATE_NATIVE_METHOD_RETURN_RE.search(tail):
        return "privateNativeDeclarationMethodReturn"
    if "=>" in tail:
        return "privateNativeDeclarationExpressionProperty"
    paren_index = tail.find("(")
    semicolon_index = tail.find(";")
    if semicolon_index >= 0 and (paren_index < 0 or semicolon_index < paren_index):
        return "privateNativeDeclarationField"
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
    if declaration_kind == "privateNativeDeclarationReadOnlyView":
        return "privateNativeRiskReadOnlyNativeView"
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
    build_surface: str,
    lines: list[str] | None,
) -> None:
    results[classify_private_native_collection(raw_line)].append(finding)
    declaration_kind = classify_private_native_declaration_kind(scan_code)
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


def classify_native_api_primary_risk(
    rel: str,
    signature: str,
    kind: str,
    build_surface: str,
    type_context: str = "",
    declaration_context: str = "",
) -> str:
    if build_surface != "nativeApiExposureBuildPlayerRuntime":
        return "nativeApiRiskEditorOrProofSurface"

    normalized = rel.replace("\\", "/")
    if (
        "/Core/Memory/" in normalized
        or normalized.endswith("/Core/Contracts/CoreLowLevelUtilities.cs")
        or normalized.endswith("/Core/HectonArenaAllocator.cs")
        or normalized.endswith("/Core/NativeArenaArray.cs")
        or normalized.endswith("/Core/Memory/H8Memory.cs")
        or normalized.endswith("/Core/Memory/GlobalDataVault.cs")
    ):
        return "nativeApiRiskCoreVaultOrAllocatorSurface"

    if NATIVE_API_OBSOLETE_COMPATIBILITY_RE.search(declaration_context) is not None:
        return "nativeApiRiskRuntimeObsoleteMutableCompatibilityView"

    if (
        NATIVE_API_OWNER_ALIAS_RE.search(signature) is not None
        or NATIVE_API_OWNER_TYPE_CONTEXT_RE.search(type_context) is not None
    ):
        return "nativeApiRiskRuntimeOwnerAliasMutableView"

    if NATIVE_API_REINTERPRET_VIEW_RE.search(signature) is not None:
        return "nativeApiRiskRuntimeReinterpretMutableView"

    if kind == "nativeApiExposureOutRefMutable":
        if NATIVE_API_READ_NAMED_RE.search(signature) is not None:
            return "nativeApiRiskRuntimeReadNamedMutableView"
        if NATIVE_API_WRITE_LEASE_RE.search(signature) is not None:
            return "nativeApiRiskRuntimeWriteLeaseMutableView"
        if NATIVE_API_JOB_ALIAS_RE.search(signature) is not None:
            return "nativeApiRiskRuntimeJobAliasMutableView"
        if NATIVE_API_NATIVE_PAYLOAD_RE.search(signature) is not None:
            return "nativeApiRiskRuntimeNativePayloadMutableView"
        if NATIVE_API_DISPOSE_RE.search(signature) is not None:
            return "nativeApiRiskRuntimeDisposeMutableRef"
        if NATIVE_API_DIAGNOSTIC_NAME_RE.search(signature) is not None:
            return "nativeApiRiskRuntimeDiagnosticNamedMutableView"
        return "nativeApiRiskRuntimeOutRefMutableView"
    if kind == "nativeApiExposureMutableReturn":
        if NATIVE_API_DIAGNOSTIC_NAME_RE.search(signature) is not None:
            return "nativeApiRiskRuntimeDiagnosticNamedMutableView"
        return "nativeApiRiskRuntimeReturnMutableView"
    return "nativeApiRiskRuntimeAmbiguousMutableView"


def record_native_api_exposure_classification(
    results: dict[str, list[Finding]],
    finding: Finding,
    rel: str,
    signature: str,
    type_context: str = "",
    declaration_context: str = "",
) -> None:
    kind = classify_native_api_exposure_kind(signature)
    build_surface = classify_native_api_build_surface(rel)
    risk = classify_native_api_primary_risk(rel, signature, kind, build_surface, type_context, declaration_context)
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


def classify_unity_time_primary_risk(rel: str, raw_line: str, scan_code: str, kind: str, build_surface: str) -> str:
    if build_surface != "unityTimeBuildPlayerRuntime":
        return "unityTimeRiskEditorOrProof"
    if kind == "unityTimeFrameCount":
        return "unityTimeRiskFrameStampOrTelemetry"
    if UNITY_TIME_DIAGNOSTIC_RE.search(raw_line) is not None:
        return "unityTimeRiskCooldownOrPerfLog"
    if kind == "unityTimeWallClock" and UNITY_TIME_VISUAL_PRESENTATION_RE.search(rel) is not None:
        return "unityTimeRiskVisualPresentationClock"
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
    risk = classify_unity_time_primary_risk(rel, raw_line, scan_code, kind, build_surface)
    results[kind].append(finding)
    results[build_surface].append(finding)
    results[risk].append(finding)


def method_preceding_context(lines: list[str], line_number: int, limit: int = 8) -> str:
    start = max(0, line_number - 1 - limit)
    return "\n".join(lines[start : line_number - 1])


def first_nonempty_text(lines: list[str]) -> str:
    for line in lines:
        text = line.strip()
        if text:
            return text
    return "<empty file>"


def is_explicit_empty_csharp_compile_unit(lines: list[str]) -> bool:
    if EMPTY_COMPILE_UNIT_MARKER_RE.search("\n".join(lines[:16])) is None:
        return False
    for raw_line in lines:
        code = strip_string_literals(strip_line_comment(raw_line)).strip()
        if code:
            return False
    return True


def has_empty_method_body(lines: list[str], line_number: int) -> bool:
    cursor = line_number - 1
    brace_line = -1
    brace_tail = ""
    while cursor < len(lines) and cursor < line_number + 8:
        code = strip_string_literals(strip_line_comment(lines[cursor])).strip()
        if code:
            if "=>" in code or ";" in code:
                return False
            brace_index = code.find("{")
            if brace_index >= 0:
                brace_line = cursor
                brace_tail = code[brace_index + 1 :]
                break
        cursor += 1

    if brace_line < 0:
        return False

    closing_index = brace_tail.find("}")
    if closing_index >= 0:
        return brace_tail[:closing_index].strip() == ""
    if brace_tail.strip():
        return False

    cursor = brace_line + 1
    while cursor < len(lines):
        code = strip_string_literals(strip_line_comment(lines[cursor])).strip()
        if not code:
            cursor += 1
            continue
        return code.startswith("}")

    return False


def is_empty_compatibility_noop_method(lines: list[str], line_number: int) -> bool:
    first_code = strip_string_literals(strip_line_comment(lines[line_number - 1]))
    if METHOD_SIGNATURE_RE.search(first_code) is None:
        return False
    context = method_preceding_context(lines, line_number).lower()
    if "legacy no-op retained for external callers" in context:
        return False
    if COMPATIBILITY_NOOP_CONTEXT_RE.search(context) is None:
        return False
    return has_empty_method_body(lines, line_number)


def is_empty_runtime_tick_or_update_method(lines: list[str], line_number: int) -> bool:
    first_code = strip_string_literals(strip_line_comment(lines[line_number - 1]))
    method_match = EMPTY_RUNTIME_TICK_METHOD_RE.search(first_code)
    if method_match is None:
        return False
    modifiers = method_match.group("modifiers") or ""
    if "virtual" in modifiers or "override" in modifiers:
        return False
    if "DispatcherTimingDTO" in (method_match.group("params") or ""):
        return False
    legacy_context = method_preceding_context(lines, line_number).lower()
    if "legacy" in legacy_context and ("serialized" in legacy_context or "compatibility" in legacy_context):
        return False
    return has_empty_method_body(lines, line_number)


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
            "nativeApiExposurePrivateNestedSuppressed": [],
            "emptyCompatibilityNoopMethod": [],
            "emptyCompileUnitMarker": [],
            "emptyRuntimeTickOrUpdateMethod": [],
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
    scan_code: str,
    build_surface: str,
    lines: list[str] | None = None,
) -> None:
    checks = (
        ("packOne", "Pack"),
        ("privateNativeCollectionField", "Native"),
        ("jobHandleComplete", ".Complete"),
        ("unityUpdateMethod", "Update"),
        ("unityRandom", "Random"),
        ("unityTimeCritical", "Time."),
        ("globalQualityWeight", "GlobalQualityWeight"),
        ("noAlias", "[NoAlias]"),
    )
    for key, token in checks:
        if token in scan_code and LINE_PATTERNS[key].search(scan_code):
            if key == "unityUpdateMethod" and build_surface == "privateNativeBuildEditorOnly":
                continue

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
                    build_surface,
                    lines,
                )
            if key == "unityTimeCritical":
                record_unity_time_classification(results, finding, rel, raw_line, scan_code)

    if (
        (
            "System.Linq" in scan_code
            or ".Where" in scan_code
            or ".Select" in scan_code
            or ".Any" in scan_code
            or ".First" in scan_code
            or ".ToList" in scan_code
        )
        and LINE_PATTERNS["linqSurface"].search(scan_code)
    ):
        if build_surface != "privateNativeBuildEditorOnly":
            results["linqSurface"].append(Finding(rel, line_number, raw_line.strip()))

    if (
        (
            "Tier" in scan_code
            or "LowEnd" in scan_code
            or "HighEnd" in scan_code
            or "Quest" in scan_code
            or "PcOnly" in scan_code
        )
        and is_binary_hardware_switch_line(scan_code)
    ):
        results["binaryHardwareSwitch"].append(Finding(rel, line_number, raw_line.strip()))


def scan_all(files: Iterable[Path]) -> dict[str, list[Finding]]:
    results = empty_results()
    for path in files:
        rel = normalize_path(path)
        try:
            source_text = path.read_text(encoding="utf-8", errors="ignore")
        except FileNotFoundError:
            continue
        lines = source_text.splitlines()
        build_surface = classify_private_native_build_surface(rel)
        is_player_runtime = build_surface == "privateNativeBuildPlayerRuntime"
        source_text_lower = source_text.lower() if is_player_runtime else ""
        may_have_compatibility_noop = (
            "compatibility" in source_text_lower
            and ("no-op" in source_text_lower or "noop" in source_text_lower)
        )
        if path.suffix.lower() == ".cs" and is_explicit_empty_csharp_compile_unit(lines):
            results["emptyCompileUnitMarker"].append(Finding(rel, 1, first_nonempty_text(lines)))
        in_struct = False
        depth = 0
        brace_depth = 0
        pending_private_type_count = 0
        pending_type_entries: list[tuple[bool, str]] = []
        type_stack: list[tuple[int, str]] = []
        private_type_stack: list[int] = []
        private_type_depth = 0
        type_depth = 0
        line_count = len(lines)
        line_number = 1
        while line_number <= line_count:
            raw_line = lines[line_number - 1]
            code = strip_line_comment(raw_line)
            scan_code = strip_string_literals(code)

            while type_stack and brace_depth < type_stack[-1][0]:
                type_stack.pop()
                type_depth -= 1
            while private_type_stack and brace_depth < private_type_stack[-1]:
                private_type_stack.pop()
                private_type_depth -= 1
            inside_type = type_depth > 0
            inside_private_type = private_type_depth > 0
            type_context = ".".join(entry[1] for entry in type_stack)

            record_line_patterns(results, rel, line_number, raw_line, scan_code, build_surface, lines)
            if (
                is_player_runtime
                and may_have_compatibility_noop
                and "(" in raw_line
                and is_empty_compatibility_noop_method(lines, line_number)
            ):
                results["emptyCompatibilityNoopMethod"].append(Finding(rel, line_number, raw_line.strip()))
            if (
                is_player_runtime
                and "(" in raw_line
                and ("Tick" in raw_line or "Update" in raw_line)
                and is_empty_runtime_tick_or_update_method(lines, line_number)
            ):
                results["emptyRuntimeTickOrUpdateMethod"].append(Finding(rel, line_number, raw_line.strip()))
            if (
                ("Native" in scan_code or ("(" in scan_code and PUBLIC_API_NATIVE_COLLECTION_RE.search(scan_code)))
                and is_public_mutable_native_api_exposure(lines, line_number, scan_code)
            ):
                signature = collect_signature(lines, line_number)
                finding = Finding(rel, line_number, signature)
                if inside_private_type:
                    results["nativeApiExposurePrivateNestedSuppressed"].append(finding)
                else:
                    results["nativeCollectionPublicMutableApiExposure"].append(finding)
                    declaration_context = method_preceding_context(lines, line_number, limit=4) + "\n" + signature
                    record_native_api_exposure_classification(
                        results,
                        finding,
                        rel,
                        signature,
                        type_context,
                        declaration_context,
                    )

            if "[BurstCompile" in scan_code:
                attr_parts = [scan_code.strip()]
                cursor = line_number
                while "]" not in attr_parts[-1] and cursor < line_count and cursor < line_number + 8:
                    cursor += 1
                    attr_parts.append(strip_string_literals(strip_line_comment(lines[cursor - 1])).strip())
                record_burst_attribute(results, rel, line_number, " ".join(attr_parts))

            if not in_struct and "struct" in scan_code and STRUCT_DECL_RE.search(scan_code):
                in_struct = True
                depth = 0

            if in_struct and "get;" in scan_code and "set;" in scan_code and AUTO_PROPERTY_RE.search(scan_code):
                results["structAutoProperties"].append(Finding(rel, line_number, raw_line.strip()))

            if in_struct:
                depth += scan_code.count("{") - scan_code.count("}")
                if depth <= 0 and "}" in scan_code:
                    in_struct = False
                    depth = 0

            if "class" in scan_code or "struct" in scan_code or "interface" in scan_code or "record" in scan_code:
                type_match = TYPE_DECL_RE.search(scan_code)
                if type_match is not None:
                    access = type_match.group("access") or ""
                    type_name = type_match.group("name") or type_match.group("record_name") or ""
                    is_private_type = access == "private" or (inside_type and access == "") or inside_private_type
                    pending_type_entries.append((is_private_type, type_name))
                    if is_private_type:
                        pending_private_type_count += 1

            open_count = scan_code.count("{")
            if open_count > 0 and pending_type_entries:
                for _ in range(min(open_count, len(pending_type_entries))):
                    _, type_name = pending_type_entries.pop(0)
                    type_stack.append((brace_depth + 1, type_name))
                    type_depth += 1
            if open_count > 0 and pending_private_type_count:
                for _ in range(min(open_count, pending_private_type_count)):
                    private_type_stack.append(brace_depth + 1)
                    private_type_depth += 1
                    pending_private_type_count -= 1

            brace_depth += open_count - scan_code.count("}")

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
        try:
            lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
        except FileNotFoundError:
            continue
        in_struct = False
        depth = 0
        for line_number, raw_line in enumerate(lines, 1):
            code = strip_line_comment(raw_line)
            if not in_struct and "struct" in code and STRUCT_DECL_RE.search(code):
                in_struct = True
                depth = 0

            if in_struct and "get;" in code and "set;" in code and AUTO_PROPERTY_RE.search(code):
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
            "- `emptyCompatibilityNoopMethod` flags explicit compatibility no-op methods in player runtime code; delete them or replace them with a real compatibility bridge.",
            "- `emptyCompileUnitMarker` flags explicit empty C# files that preserve no type/API and should usually be deleted instead of kept as source markers.",
            "- `emptyRuntimeTickOrUpdateMethod` marks runtime tick/update surfaces that perform no work and should usually be deleted instead of registered.",
            "- `nativeApiRiskRuntimeReadNamedMutableView` flags runtime APIs whose names promise read access while exposing mutable native buffers.",
            "- `nativeApiRiskRuntimeObsoleteMutableCompatibilityView` tracks deprecated mutable compatibility wrappers separately from active read-named API debt.",
            "- `nativeApiRiskRuntimeReinterpretMutableView` flags writable native views created through reinterpret helpers; these need owner/alias review, not blind read-only conversion.",
            "- `nativeApiRiskRuntimeWriteLeaseMutableView`, `nativeApiRiskRuntimeJobAliasMutableView`, `nativeApiRiskRuntimeNativePayloadMutableView`, and `nativeApiRiskRuntimeDisposeMutableRef` separate intentional write/lease/job/payload/disposal surfaces from generic mutable API debt.",
            "- `unityTimeRiskVisualPresentationClock` separates wall-clock presentation animation from gameplay clock debt.",
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
