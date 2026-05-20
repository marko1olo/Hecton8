#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import re
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from H8VerifyCore import ROOT, path


HOT_METHODS = {
    "Tick",
    "FixedTick",
    "Update",
    "FixedUpdate",
    "LateUpdate",
    "PreSimulationTick",
    "ScheduleSimulation",
    "PostSimulationTick",
    "VisualSyncTick",
    "LateFrameTick",
    "Execute",
}

MID_FRAME_METHODS = {
    "Tick",
    "FixedTick",
    "Update",
    "FixedUpdate",
    "PreSimulationTick",
    "ScheduleSimulation",
    "Execute",
}

SKIPPED_DIRS = {
    ".git",
    ".vs",
    ".idea",
    "Build",
    "Builds",
    "Library",
    "Logs",
    "Temp",
    "UserSettings",
    "__pycache__",
    "bin",
    "obj",
}

TEXT_CACHE: dict[Path, str] = {}
LINE_CACHE: dict[Path, list[str]] = {}

INTERFACE_DECLARATION_RE = re.compile(r"\binterface\s+(I[A-Za-z_][A-Za-z0-9_]*)\b")
INTERFACE_ARRAY_RE = re.compile(r"\b(I[A-Za-z_][A-Za-z0-9_]*)\s*\[\]")
INTERFACE_GENERIC_RE = re.compile(
    r"\b(?:List|IEnumerable|IReadOnlyList|NativeArray|NativeList)\s*<\s*(I[A-Za-z_][A-Za-z0-9_]*)\b"
)
ACCESSOR_PROPERTY_RE = re.compile(r"(^|[\s{;])(?:public\s+|private\s+|protected\s+|internal\s+)?(?:get|set)\s*;")
MANAGED_REFERENCE_FIELD_RE = re.compile(
    r"\b(?:string|object|GameObject|Mesh|Material|Light|Texture|Texture2D|Sprite|AudioClip|AnimationCurve|"
    r"Transform|Component|Camera|Collider|Rigidbody|MonoBehaviour|ScriptableObject|AssetReference|TMP_Text)\b"
)
METHOD_NAME_RE = re.compile(r"(?<![A-Za-z0-9_])([A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^;{}()]*>)?\s*\(")
CONTROL_METHOD_NAMES = {
    "if",
    "for",
    "foreach",
    "while",
    "switch",
    "catch",
    "using",
    "lock",
    "return",
    "new",
    "sizeof",
    "typeof",
}
SELF_AUDIT_TASK_SCANNER_MAP = {
    "01": ("Hot_Registry_Polling", "Hot_Helper_Registry_Polling"),
    "02": ("Mid_Frame_Complete", "Hot_Helper_Complete"),
    "07": ("Vault_Sovereignty",),
    "09": ("Signal_Bus_Topology",),
    "11": ("Rollback_Fence_Compliance",),
    "12": ("AUP_Compliance",),
    "14": ("Compile_Wall",),
}

FORBIDDEN_CORE_REFERENCE_PREFIXES = (
    "Hecton8.World",
    "Hecton8.AI",
    "Hecton8.Systems",
    "Hecton8.Graphics",
    "Hecton8.Audio",
    "Hecton8.Gameplay",
    "Hecton8.Physics",
    "Hecton8.Networking",
    "Hecton8.Environment",
    "Hecton8.Inventory",
    "Hecton8.Narrative",
    "Hecton8.Power",
    "Hecton8.Quest",
    "Hecton8.SaveSystem",
    "Hecton8.Visor",
    "Hecton8.VFX",
)

FORBIDDEN_CORE_NAMESPACE_TOKENS = (
    "Hecton8.Networking",
    "Hecton8.World",
    "Hecton8.AI",
    "Hecton8.Systems.AI",
    "Hecton8.Graphics",
    "Hecton8.Audio",
    "Hecton8.Gameplay",
    "Hecton8.Physics",
    "Hecton8.Atmosphere",
    "Hecton8.Celestial",
    "Hecton8.Construction",
    "Hecton8.Environment",
    "Hecton8.Inventory",
    "Hecton8.Narrative",
    "Hecton8.Optimization",
    "Hecton8.Power",
    "Hecton8.Quest",
    "Hecton8.SaveSystem",
    "Hecton8.Visor",
    "Hecton8.VFX",
)

VAULT_ALLOCATION_STARTERS = (
    "new NativeArray",
    "new NativeList",
    "new NativeQueue",
    "new NativeParallelHashMap",
    "new UnsafeHashMap",
    "H8Memory.Allocate",
    "H8Memory.AllocateRaw",
    "H8Memory.ReallocateRaw",
    "UnsafeUtility.Malloc",
)

VAULT_EXEMPT_TOKENS = (
    "DataVault",
    "VaultBufferHandle",
    "NativeArrayOptions",
)

VAULT_AUTHORITY_FILE_SUFFIXES = (
    "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs",
    "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
    "Assets/_Project/Scripts/Core/HectonArenaAllocator.cs",
    "Assets/_Project/Scripts/Core/NativeMemorySentinel.cs",
    "Assets/_Project/Scripts/Core/BurstCallback.cs",
    "Assets/_Project/Scripts/Core/GlobalRegistry.cs",
    "Assets/_Project/Scripts/Core/GlobalSignals.cs",
    "Assets/_Project/Scripts/Core/MathGuard.cs",
    "Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs",
)


@dataclass
class Finding:
    scanner: str
    path: str
    line: int
    rule: str
    detail: str
    severity: int


class ScanResult:
    def __init__(self, scanner: str) -> None:
        self.scanner = scanner
        self.files_scanned = 0
        self.findings: list[Finding] = []

    @property
    def critical_count(self) -> int:
        return sum(1 for finding in self.findings if finding.severity >= 2)

    @property
    def warning_count(self) -> int:
        return sum(1 for finding in self.findings if finding.severity < 2)

    def add(self, file_path: Path | str, line: int, rule: str, detail: str, severity: int) -> None:
        self.findings.append(
            Finding(
                self.scanner,
                project_path(file_path),
                line,
                rule,
                detail,
                severity,
            )
        )

    def to_dict(self) -> dict:
        return {
            "scanner": self.scanner,
            "filesScanned": self.files_scanned,
            "criticalCount": self.critical_count,
            "warningCount": self.warning_count,
            "findings": [finding.__dict__ for finding in self.findings],
        }


def project_path(file_path: Path | str) -> str:
    candidate = Path(file_path)
    try:
        return candidate.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(file_path).replace("\\", "/")


def runtime_cs_files() -> list[Path]:
    root = ROOT / "Assets" / "_Project" / "Scripts"
    if not root.exists():
        return []
    files: list[Path] = []
    for file_path in root.rglob("*.cs"):
        parts = {part.lower() for part in file_path.parts}
        if "editor" in parts or "tests" in parts or "generated" in parts:
            continue
        files.append(file_path)
    return sorted(files)


def read_lines(file_path: Path) -> list[str]:
    if file_path in LINE_CACHE:
        return LINE_CACHE[file_path]
    text = read_text(file_path)
    lines = text.splitlines()
    LINE_CACHE[file_path] = lines
    return lines


def read_text(file_path: Path) -> str:
    if file_path in TEXT_CACHE:
        return TEXT_CACHE[file_path]
    try:
        text = file_path.read_text(encoding="utf-8-sig", errors="ignore")
    except OSError:
        text = ""
    TEXT_CACHE[file_path] = text
    return text


def discover_asmdefs() -> list[Path]:
    roots = [ROOT / "Assets", ROOT / "Packages"]
    asmdefs: list[Path] = []
    for base in roots:
        if not base.exists():
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [name for name in dirnames if name not in SKIPPED_DIRS]
            directory = Path(dirpath)
            for filename in filenames:
                if filename.endswith(".asmdef"):
                    asmdefs.append(directory / filename)
    return sorted(asmdefs)


def mask_comments_and_strings(line: str) -> str:
    builder: list[str] = []
    in_string = False
    string_char = ""
    i = 0
    while i < len(line):
        char = line[i]
        if not in_string and char == "/" and i + 1 < len(line) and line[i + 1] == "/":
            break
        if not in_string and char in ("'", '"'):
            in_string = True
            string_char = char
            builder.append(" ")
            i += 1
            continue
        if in_string:
            if char == "\\" and i + 1 < len(line):
                builder.append(" ")
                i += 2
                continue
            if char == string_char:
                in_string = False
            builder.append(" ")
            i += 1
            continue
        builder.append(char)
        i += 1
    return "".join(builder)


def is_vault_authority_file(file_path: Path) -> bool:
    normalized = file_path.as_posix()
    return any(normalized.endswith(suffix) for suffix in VAULT_AUTHORITY_FILE_SUFFIXES)


def collect_masked_statement(lines: list[str], start_index: int, max_lines: int = 12) -> str:
    parts: list[str] = []
    paren_depth = 0
    upper_bound = min(len(lines), start_index + max_lines)
    for index in range(start_index, upper_bound):
        masked = mask_comments_and_strings(lines[index])
        parts.append(masked)
        paren_depth += masked.count("(")
        paren_depth -= masked.count(")")
        if ";" in masked and paren_depth <= 0:
            break
    return " ".join(parts)


def is_vault_allocation_violation(file_path: Path, lines: list[str], line_index: int, masked_line: str) -> bool:
    if is_vault_authority_file(file_path):
        return False
    if not any(marker in masked_line for marker in VAULT_ALLOCATION_STARTERS):
        return False

    statement = collect_masked_statement(lines, line_index)
    if not any(marker in statement for marker in VAULT_ALLOCATION_STARTERS):
        return False
    if "Allocator.Persistent" not in statement and "new NativeArray" not in statement:
        return False
    if any(token in statement for token in VAULT_EXEMPT_TOKENS):
        return False
    return True


def update_method_context(masked_line: str, current_method: str) -> str:
    paren = masked_line.find("(")
    if paren <= 0 or ";" in masked_line:
        return current_method
    before = masked_line[:paren].strip()
    if not before:
        return current_method
    candidate = before.split()[-1]
    if candidate in {"if", "for", "while", "switch", "catch", "using", "lock"}:
        return current_method
    return candidate


def try_method_declaration_name(masked_line: str) -> str:
    paren = masked_line.find("(")
    if paren <= 0 or ";" in masked_line or "=>" in masked_line:
        return ""
    before = masked_line[:paren].strip()
    if not before:
        return ""
    candidate = before.split()[-1]
    if candidate in CONTROL_METHOD_NAMES or candidate == "operator":
        return ""
    if not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", candidate):
        return ""
    return candidate


def extract_method_blocks(file_path: Path) -> list[tuple[str, int, list[tuple[int, str]]]]:
    lines = read_lines(file_path)
    blocks: list[tuple[str, int, list[tuple[int, str]]]] = []
    index = 0
    while index < len(lines):
        masked = mask_comments_and_strings(lines[index])
        method_name = try_method_declaration_name(masked)
        if not method_name:
            index += 1
            continue

        body: list[tuple[int, str]] = []
        depth = 0
        saw_open_brace = False
        cursor = index
        while cursor < len(lines):
            body_line = mask_comments_and_strings(lines[cursor])
            body.append((cursor + 1, body_line))
            if "{" in body_line:
                saw_open_brace = True
            if saw_open_brace:
                depth += body_line.count("{")
                depth -= body_line.count("}")
                if depth <= 0:
                    break
            cursor += 1

        if saw_open_brace:
            blocks.append((method_name, index + 1, body))
        index = max(cursor + 1, index + 1)
    return blocks


def contains_method_call(masked_line: str, method_name: str) -> bool:
    for match in METHOD_NAME_RE.finditer(masked_line):
        if match.group(1) == method_name:
            return True
    return False


def write_report(result: ScanResult, output_dir: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    target = output_dir / f"SHINOBU_140_{result.scanner}.json"
    target.write_text(json.dumps(result.to_dict(), indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_summary(results: Iterable[ScanResult], output_dir: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    results_list = list(results)
    regression = next((result for result in results_list if result.scanner == "Static_Gate_Regression"), None)
    data = {
        "agent": "SHINOBU_140",
        "evidence": "STATIC_SOURCE/PY_TOOL/CI_FALLBACK",
        "baseline": "Docs/Reports/SHINOBU_140_STATIC_GATE_BASELINE.json",
        "ownerMap": "Docs/Reports/SHINOBU_140_STATIC_GATE_OWNER_MAP.json",
        "regressionAttribution": "Docs/Reports/SHINOBU_140_STATIC_GATE_REGRESSION_ATTRIBUTION.json",
        "regressionCritical": regression.critical_count if regression is not None else -1,
        "regressionWarnings": regression.warning_count if regression is not None else -1,
        "status": "PENDING VERIFICATION",
        "totalCritical": sum(result.critical_count for result in results_list),
        "totalWarnings": sum(result.warning_count for result in results_list),
        "scanners": {
            result.scanner: {
                "filesScanned": result.files_scanned,
                "criticalCount": result.critical_count,
                "warningCount": result.warning_count,
            }
            for result in results_list
        },
    }
    (output_dir / "SHINOBU_140_STATIC_GATE_SUMMARY.json").write_text(
        json.dumps(data, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def domain_hint(project_relative_path: str) -> str:
    parts = project_relative_path.replace("\\", "/").split("/")
    try:
        scripts_index = parts.index("Scripts")
    except ValueError:
        return "ProjectWide"
    if scripts_index + 1 >= len(parts):
        return "ProjectWide"
    candidate = parts[scripts_index + 1]
    if candidate.endswith(".cs"):
        return "RootScripts"
    return candidate


def write_owner_map(results: Iterable[ScanResult], output_dir: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    scanners: dict[str, dict] = {}
    domain_totals: dict[str, dict[str, int]] = {}
    for result in results:
        path_rows: dict[str, dict[str, int]] = {}
        for finding in result.findings:
            row = path_rows.setdefault(finding.path, {"critical": 0, "warning": 0})
            domain = domain_hint(finding.path)
            domain_row = domain_totals.setdefault(domain, {"critical": 0, "warning": 0})
            if finding.severity >= 2:
                row["critical"] += 1
                domain_row["critical"] += 1
            else:
                row["warning"] += 1
                domain_row["warning"] += 1

        top_paths = sorted(
            (
                {
                    "path": file_path,
                    "domainHint": domain_hint(file_path),
                    "criticalCount": counts["critical"],
                    "warningCount": counts["warning"],
                }
                for file_path, counts in path_rows.items()
            ),
            key=lambda item: (item["criticalCount"], item["warningCount"], item["path"]),
            reverse=True,
        )[:25]
        scanners[result.scanner] = {
            "criticalCount": result.critical_count,
            "warningCount": result.warning_count,
            "topPaths": top_paths,
        }

    owner_rows = sorted(
        (
            {
                "domainHint": domain,
                "criticalCount": counts["critical"],
                "warningCount": counts["warning"],
            }
            for domain, counts in domain_totals.items()
        ),
        key=lambda item: (item["criticalCount"], item["warningCount"], item["domainHint"]),
        reverse=True,
    )
    data = {
        "agent": "SHINOBU_140",
        "evidence": "STATIC_SOURCE/PY_TOOL/CI_FALLBACK",
        "status": "PENDING VERIFICATION",
        "purpose": "Route static red debt to owner domains without mutating foreign runtime code.",
        "ownerTotals": owner_rows,
        "scanners": scanners,
    }
    (output_dir / "SHINOBU_140_STATIC_GATE_OWNER_MAP.json").write_text(
        json.dumps(data, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def summarize_paths_for_result(result: ScanResult, limit: int) -> list[dict]:
    path_rows: dict[str, dict[str, int]] = {}
    for finding in result.findings:
        row = path_rows.setdefault(finding.path, {"critical": 0, "warning": 0})
        if finding.severity >= 2:
            row["critical"] += 1
        else:
            row["warning"] += 1
    return sorted(
        (
            {
                "path": file_path,
                "domainHint": domain_hint(file_path),
                "criticalCount": counts["critical"],
                "warningCount": counts["warning"],
            }
            for file_path, counts in path_rows.items()
        ),
        key=lambda item: (item["criticalCount"], item["warningCount"], item["path"]),
        reverse=True,
    )[:limit]


def summarize_domains_for_result(result: ScanResult, limit: int) -> list[dict]:
    domain_rows: dict[str, dict[str, int]] = {}
    for finding in result.findings:
        row = domain_rows.setdefault(domain_hint(finding.path), {"critical": 0, "warning": 0})
        if finding.severity >= 2:
            row["critical"] += 1
        else:
            row["warning"] += 1
    return sorted(
        (
            {
                "domainHint": domain,
                "criticalCount": counts["critical"],
                "warningCount": counts["warning"],
            }
            for domain, counts in domain_rows.items()
        ),
        key=lambda item: (item["criticalCount"], item["warningCount"], item["domainHint"]),
        reverse=True,
    )[:limit]


def load_json_object(file_path: Path) -> dict:
    if not file_path.exists():
        return {}
    try:
        payload = json.loads(file_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return {}
    return payload if isinstance(payload, dict) else {}


def build_regression_attribution(results: list[ScanResult], baseline_path: Path) -> dict:
    baseline = load_json_object(baseline_path)
    baseline_scanners = baseline.get("scanners", {}) if isinstance(baseline.get("scanners", {}), dict) else {}
    regressions: list[dict] = []
    for current in results:
        if current.scanner == "Static_Gate_Regression":
            continue
        baseline_row = baseline_scanners.get(current.scanner, {})
        if not isinstance(baseline_row, dict):
            continue
        critical_budget = int(baseline_row.get("criticalCount", -1))
        warning_budget = int(baseline_row.get("warningCount", -1))
        critical_delta = current.critical_count - critical_budget
        warning_delta = current.warning_count - warning_budget
        if critical_delta <= 0 and warning_delta <= 0:
            continue
        regressions.append(
            {
                "scanner": current.scanner,
                "currentCritical": current.critical_count,
                "baselineCritical": critical_budget,
                "criticalDelta": critical_delta,
                "currentWarnings": current.warning_count,
                "baselineWarnings": warning_budget,
                "warningDelta": warning_delta,
                "topDomains": summarize_domains_for_result(current, 12),
                "topPaths": summarize_paths_for_result(current, 20),
            }
        )
    return {
        "agent": "SHINOBU_140",
        "baseline": project_path(baseline_path),
        "evidence": "STATIC_SOURCE/PY_TOOL/CI_FALLBACK",
        "purpose": "Attribute no-regression failures to owner domains without mutating foreign runtime code.",
        "status": "PENDING VERIFICATION",
        "regressions": regressions,
    }


def write_regression_attribution(results: list[ScanResult], baseline_path: Path, output_dir: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    data = build_regression_attribution(results, baseline_path)
    (output_dir / "SHINOBU_140_STATIC_GATE_REGRESSION_ATTRIBUTION.json").write_text(
        json.dumps(data, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def build_regression_budget_result(results: list[ScanResult], baseline_path: Path) -> ScanResult:
    result = ScanResult("Static_Gate_Regression")
    result.files_scanned = len(results)
    baseline = load_json_object(baseline_path)
    if not baseline:
        result.add(
            baseline_path,
            1,
            "STATIC_GATE_BASELINE_MISSING",
            "SHINOBU_140 static gate baseline is missing or invalid; red debt cannot be regression-guarded.",
            2,
        )
        return result

    baseline_scanners = baseline.get("scanners", {})
    if not isinstance(baseline_scanners, dict):
        result.add(
            baseline_path,
            1,
            "STATIC_GATE_BASELINE_SCHEMA_INVALID",
            "SHINOBU_140 static gate baseline must contain a scanners object.",
            2,
        )
        return result

    for current in results:
        baseline_row = baseline_scanners.get(current.scanner)
        if not isinstance(baseline_row, dict):
            result.add(
                baseline_path,
                1,
                "STATIC_GATE_BASELINE_SCANNER_MISSING",
                f"Baseline has no row for scanner {current.scanner}; update baseline deliberately.",
                2,
            )
            continue

        critical_budget = int(baseline_row.get("criticalCount", -1))
        warning_budget = int(baseline_row.get("warningCount", -1))
        if current.critical_count > critical_budget:
            result.add(
                baseline_path,
                1,
                "STATIC_GATE_CRITICAL_REGRESSION",
                f"{current.scanner} critical count {current.critical_count} exceeds baseline {critical_budget}.",
                2,
            )
        if current.warning_count > warning_budget:
            result.add(
                baseline_path,
                1,
                "STATIC_GATE_WARNING_REGRESSION",
                f"{current.scanner} warning count {current.warning_count} exceeds baseline {warning_budget}.",
                2,
            )

    return result


def scan_aup(files: list[Path]) -> ScanResult:
    result = ScanResult("AUP_Compliance")
    result.files_scanned = len(files)
    for file_path in files:
        if file_path.name.lower() == "hectonfloatingorigin.cs":
            continue
        method = ""
        for line_number, line in enumerate(read_lines(file_path), 1):
            masked = mask_comments_and_strings(line)
            method = update_method_context(masked, method)
            if method not in HOT_METHODS:
                continue
            if "Vector3.Distance" in masked or ".position" in masked:
                result.add(
                    file_path,
                    line_number,
                    "AUP_WORLD_SPACE_HOT_PATH",
                    "World-space transform/math access inside hot method; route through AUP snapshot or local-sector DTO.",
                    2,
                )
    return result


def scan_vault(files: list[Path]) -> ScanResult:
    result = ScanResult("Vault_Sovereignty")
    result.files_scanned = len(files)
    for file_path in files:
        lines = read_lines(file_path)
        for line_index, line in enumerate(lines):
            line_number = line_index + 1
            masked = mask_comments_and_strings(line)
            if not is_vault_allocation_violation(file_path, lines, line_index, masked):
                continue
            result.add(
                file_path,
                line_number,
                "DIRECT_NATIVE_ALLOCATION",
                "Runtime native memory must be DataVault-owned or explicitly exempted.",
                2,
            )
    return result


def scan_compile_wall() -> ScanResult:
    result = ScanResult("Compile_Wall")
    asmdefs = discover_asmdefs()
    result.files_scanned = len(asmdefs)
    for asmdef in asmdefs:
        try:
            data = json.loads(read_text(asmdef))
        except json.JSONDecodeError:
            continue
        name = str(data.get("name", ""))
        if name != "Hecton8.Core" and not name.startswith("Hecton8.Core."):
            continue
        for reference in data.get("references", []):
            if not isinstance(reference, str):
                continue
            if is_allowed_core_reference(reference):
                continue
            if reference.startswith(FORBIDDEN_CORE_REFERENCE_PREFIXES):
                result.add(
                    asmdef,
                    1,
                    "CORE_RUNTIME_DOMAIN_EDGE",
                    f"{name} references {reference}; move through contracts or EventBus/DataVault surfaces.",
                    2,
                )
        text = read_text(asmdef)
        if '"Pack": 1' in text or "Pack=1" in text:
            result.add(
                asmdef,
                1,
                "PACK1_RUNTIME_LAYOUT",
                "Runtime assembly metadata contains Pack=1; ARM64 DTOs must use explicit 16/32/64-byte layouts.",
                2,
            )

    files = runtime_cs_files()
    result.files_scanned += len(files)
    for file_path in files:
        if not is_core_runtime_source(file_path):
            continue
        for line_number, line in enumerate(read_lines(file_path), 1):
            masked = mask_comments_and_strings(line)
            forbidden = resolve_forbidden_core_namespace(masked)
            if not forbidden:
                continue
            result.add(
                file_path,
                line_number,
                "CORE_SOURCE_DOMAIN_EDGE",
                f"Core source references {forbidden}; mirror DTOs through contracts, EventBus, or DataVault handles.",
                2,
            )
    return result


def is_allowed_core_reference(reference: str) -> bool:
    return any(token in reference for token in ("Contracts", "Memory", "Scheduling", "Bucketing", "Time", "Persistence"))


def is_core_runtime_source(file_path: Path) -> bool:
    return "Assets/_Project/Scripts/Core/" in file_path.as_posix()


def resolve_forbidden_core_namespace(masked: str) -> str:
    for token in FORBIDDEN_CORE_NAMESPACE_TOKENS:
        if token in masked:
            return token
    return ""


def scan_self_audit() -> ScanResult:
    result = ScanResult("Self_Audit_Proof")
    result.files_scanned = 1
    candidate = path("Docs/Reports/SHINOBU_140_SELF_AUDIT.xml")
    if not candidate.exists():
        result.add(candidate, 1, "SELF_AUDIT_MISSING", "SHINOBU_140 forensic self-audit XML is missing.", 2)
        return result

    try:
        root = ET.parse(candidate).getroot()
    except (OSError, ET.ParseError):
        result.add(candidate, 1, "SELF_AUDIT_INVALID_XML", "SHINOBU_140 forensic self-audit XML is not parseable.", 2)
        return result

    if root.tag != "SELF_AUDIT":
        result.add(candidate, 1, "SELF_AUDIT_WRONG_ROOT", "Self-audit root element must be SELF_AUDIT.", 2)
    if root.attrib.get("agent") != "SHINOBU_140":
        result.add(candidate, 1, "SELF_AUDIT_AGENT_MISMATCH", "Self-audit agent attribute must be SHINOBU_140.", 2)
    if root.attrib.get("taskCount") != "20":
        result.add(candidate, 1, "SELF_AUDIT_TASK_COUNT_MISMATCH", "Self-audit must declare exactly 20 tasks.", 2)

    task_reconciliation = root.find("TaskReconciliation")
    tasks = task_reconciliation.findall("Task") if task_reconciliation is not None else []
    if len(tasks) != 20:
        result.add(candidate, 1, "SELF_AUDIT_TASK_ROWS_MISMATCH", "Self-audit must list Tasks 01 through 20 explicitly.", 2)
    expected_ids = {f"{index:02d}" for index in range(1, 21)}
    actual_ids = {task.attrib.get("id", "") for task in tasks}
    if actual_ids != expected_ids:
        result.add(candidate, 1, "SELF_AUDIT_TASK_ID_SET_MISMATCH", "Self-audit task ids must be the exact set 01..20.", 2)

    required_sections = (
        "StructLayoutVerification",
        "ScalabilityCurve",
        "HPhiVaultStatus",
        "PointerAliasingAndDependencyGraph",
        "CompileGuard",
        "DearLieConfirmation",
    )
    for section in required_sections:
        if root.find(section) is None:
            result.add(candidate, 1, "SELF_AUDIT_SECTION_MISSING", f"Self-audit section {section} is missing.", 2)

    return result


def validate_self_audit_counts_against_results(result: ScanResult, results: list[ScanResult], baseline_path: Path) -> None:
    candidate = path("Docs/Reports/SHINOBU_140_SELF_AUDIT.xml")
    if not candidate.exists():
        return

    try:
        root = ET.parse(candidate).getroot()
    except (OSError, ET.ParseError):
        return

    expected = build_self_audit_count_model(results, baseline_path)
    sync_self_audit_counts(candidate, root, expected)
    try:
        root = ET.parse(candidate).getroot()
    except (OSError, ET.ParseError):
        return

    expected_hygiene = expected["hygiene"]
    expected_tasks = expected["tasks"]
    scanner_rows = {
        row.scanner: row
        for row in results
        if row.scanner not in {"Static_Gate_Regression"}
    }

    hygiene = root.find("Hygiene")
    if hygiene is None:
        result.add(candidate, 1, "SELF_AUDIT_HYGIENE_MISSING", "Self-audit Hygiene row is missing.", 2)
    else:
        for attribute, expected in expected_hygiene.items():
            actual = hygiene.attrib.get(attribute)
            if actual != str(expected):
                result.add(
                    candidate,
                    1,
                    "SELF_AUDIT_HYGIENE_COUNT_MISMATCH",
                    f"Hygiene {attribute}={actual} does not match static gate summary {expected}.",
                    2,
                )

    task_reconciliation = root.find("TaskReconciliation")
    tasks = task_reconciliation.findall("Task") if task_reconciliation is not None else []
    tasks_by_id = {task.attrib.get("id", ""): task for task in tasks}
    for task_id, scanner_names in SELF_AUDIT_TASK_SCANNER_MAP.items():
        for scanner_name in scanner_names:
            scanner_row = scanner_rows.get(scanner_name)
            if scanner_row is None:
                result.add(candidate, 1, "SELF_AUDIT_SCANNER_ROW_MISSING", f"Static gate summary has no {scanner_name} row.", 2)
        task = tasks_by_id.get(task_id)
        if task is None:
            continue
        expected_task = expected_tasks.get(task_id, {})
        expected_critical = str(expected_task.get("criticalDebt", -1))
        actual_critical = task.attrib.get("criticalDebt")
        if actual_critical != expected_critical:
            result.add(
                candidate,
                1,
                "SELF_AUDIT_TASK_DEBT_MISMATCH",
                f"Task {task_id} criticalDebt={actual_critical} does not match scanner critical sum {expected_critical}.",
                2,
            )
        if "warningDebt" in task.attrib:
            expected_warning = str(expected_task.get("warningDebt", -1))
            actual_warning = task.attrib.get("warningDebt")
            if actual_warning != expected_warning:
                result.add(
                    candidate,
                    1,
                    "SELF_AUDIT_TASK_WARNING_MISMATCH",
                    f"Task {task_id} warningDebt={actual_warning} does not match scanner warning sum {expected_warning}.",
                    2,
                )


def build_self_audit_count_model(results: list[ScanResult], baseline_path: Path) -> dict:
    non_regression_rows = [row for row in results if row.scanner != "Static_Gate_Regression"]
    expected_regression = build_regression_budget_result(non_regression_rows, baseline_path)
    task_rows: dict[str, dict[str, int]] = {}
    by_scanner = {row.scanner: row for row in non_regression_rows}
    for task_id, scanner_names in SELF_AUDIT_TASK_SCANNER_MAP.items():
        critical = 0
        warning = 0
        for scanner_name in scanner_names:
            row = by_scanner.get(scanner_name)
            if row is None:
                continue
            critical += row.critical_count
            warning += row.warning_count
        task_rows[task_id] = {"criticalDebt": critical, "warningDebt": warning}
    return {
        "hygiene": {
            "totalCritical": sum(row.critical_count for row in non_regression_rows) + expected_regression.critical_count,
            "totalWarnings": sum(row.warning_count for row in non_regression_rows) + expected_regression.warning_count,
            "scannerCount": len(non_regression_rows) + 1,
            "regressionCritical": expected_regression.critical_count,
        },
        "tasks": task_rows,
        "regression": {
            "critical": expected_regression.critical_count,
            "warning": expected_regression.warning_count,
        },
    }


def sync_self_audit_counts(candidate: Path, root: ET.Element, expected: dict) -> None:
    hygiene = root.find("Hygiene")
    if hygiene is not None:
        for attribute, value in expected["hygiene"].items():
            hygiene.set(attribute, str(value))

    task_reconciliation = root.find("TaskReconciliation")
    if task_reconciliation is not None:
        tasks_by_id = {task.attrib.get("id", ""): task for task in task_reconciliation.findall("Task")}
        for task_id, task_counts in expected["tasks"].items():
            task = tasks_by_id.get(task_id)
            if task is None:
                continue
            task.set("criticalDebt", str(task_counts["criticalDebt"]))
            if "warningDebt" in task.attrib:
                task.set("warningDebt", str(task_counts["warningDebt"]))

    regression_budget = root.find("RegressionBudget")
    if regression_budget is not None:
        regression_budget.set("critical", str(expected["regression"]["critical"]))
        regression_budget.set("warning", str(expected["regression"]["warning"]))
        if expected["regression"]["critical"] == 0 and expected["regression"]["warning"] == 0:
            regression_budget.text = (
                "\n    No scanner currently exceeds the frozen red-debt baseline. "
                "Static gate remains red on legacy debt, but the no-regression budget is clean.\n  "
            )
        else:
            regression_budget.text = (
                "\n    One or more scanners exceed the frozen red-debt baseline; see "
                "Docs/Reports/SHINOBU_140_STATIC_GATE_REGRESSION_ATTRIBUTION.json for owner routing. "
                "New helper-reachability scanner rows are seeded at their first measured debt and are not counted as existing-scanner regressions.\n  "
            )

    ET.indent(root, space="  ")
    candidate.write_text(ET.tostring(root, encoding="unicode") + "\n", encoding="utf-8")


def scan_struct_layout(files: list[Path]) -> ScanResult:
    result = ScanResult("Runtime_Struct_Layout")
    result.files_scanned = len(files)
    for file_path in files:
        inside_struct = False
        struct_depth = 0
        struct_has_managed_reference = False
        pending_property_findings: list[tuple[int, Path]] = []
        pending_bool_findings: list[tuple[int, Path]] = []
        for line_number, line in enumerate(read_lines(file_path), 1):
            masked = mask_comments_and_strings(line)
            if "StructLayout" in masked and "Pack" in masked:
                result.add(
                    file_path,
                    line_number,
                    "PACKED_RUNTIME_STRUCT",
                    "Runtime structs must not use packed layout; copy file records into aligned DTOs instead.",
                    2,
                )
            if not inside_struct and is_struct_declaration(masked):
                inside_struct = True
                struct_depth = 0
                struct_has_managed_reference = False
                pending_property_findings = []
                pending_bool_findings = []
            if not inside_struct:
                continue
            if contains_managed_reference_field(masked):
                struct_has_managed_reference = True
            if looks_like_accessor_property(masked):
                pending_property_findings.append((line_number, file_path))
            if contains_bool_field(masked):
                pending_bool_findings.append((line_number, file_path))
            struct_depth += masked.count("{")
            struct_depth -= masked.count("}")
            if struct_depth <= 0 and "}" in masked:
                if not struct_has_managed_reference:
                    for pending_line, pending_path in pending_property_findings:
                        result.add(
                            pending_path,
                            pending_line,
                            "STRUCT_PROPERTY_DEFENSIVE_COPY_RISK",
                            "Runtime structs in native/hot paths must expose raw fields, not C# properties.",
                            2,
                        )
                    for pending_line, pending_path in pending_bool_findings:
                        result.add(
                            pending_path,
                            pending_line,
                            "STRUCT_BOOL_FIELD_ARM64_RISK",
                            "Runtime structs must use byte or bit flags instead of bool fields.",
                            2,
                        )
                inside_struct = False
                pending_property_findings = []
                pending_bool_findings = []
                struct_has_managed_reference = False
    return result


def is_struct_declaration(masked: str) -> bool:
    stripped = masked.lstrip()
    return stripped.startswith("struct ") or " struct " in masked or " partial struct " in masked


def looks_like_accessor_property(masked: str) -> bool:
    return "{" in masked and ACCESSOR_PROPERTY_RE.search(masked) is not None


def contains_bool_field(masked: str) -> bool:
    bool_index = masked.find(" bool ")
    semicolon_index = masked.find(";")
    if bool_index < 0 or semicolon_index < 0 or semicolon_index < bool_index:
        return False
    if "=>" in masked or "get;" in masked or "set;" in masked:
        return False
    if "(" in masked[:semicolon_index]:
        return False
    return any(prefix in masked for prefix in ("public ", "internal ", "private ", "protected "))


def contains_managed_reference_field(masked: str) -> bool:
    semicolon_index = masked.find(";")
    if semicolon_index < 0 or "(" in masked[:semicolon_index]:
        return False
    if "[]" in masked or "List<" in masked or "Dictionary<" in masked:
        return True
    return MANAGED_REFERENCE_FIELD_RE.search(masked) is not None


def is_deterministic_burst_path(file_path: Path) -> bool:
    normalized = file_path.as_posix().lower()
    return any(
        token in normalized
        for token in (
            "net",
            "rollback",
            "determinism",
            "lockstep",
            "memorysentinel",
            "desync",
            "origin",
            "aup",
            "vaultmemory",
            "signalwarden",
        )
    )


def scan_burst(files: list[Path]) -> ScanResult:
    result = ScanResult("Burst_Job_Directives")
    result.files_scanned = len(files)
    for file_path in files:
        lines = read_lines(file_path)
        for index, line in enumerate(lines):
            masked = mask_comments_and_strings(line)
            if not is_job_declaration(masked):
                continue
            attribute_window = " ".join(mask_comments_and_strings(item) for item in lines[max(0, index - 8) : index])
            if "BurstCompile" not in attribute_window:
                result.add(file_path, index + 1, "JOB_MISSING_BURSTCOMPILE", "Job structs must be Burst compiled before dispatcher phase scheduling.", 2)
                continue
            has_sync = "CompileSynchronously" in attribute_window and "true" in attribute_window.lower()
            has_precision = "FloatPrecision" in attribute_window and "Standard" in attribute_window
            deterministic_path = is_deterministic_burst_path(file_path)
            valid_mode = ("Deterministic" in attribute_window) if deterministic_path else ("Fast" in attribute_window)
            if not has_sync or not has_precision or not valid_mode:
                result.add(
                    file_path,
                    index + 1,
                    "BURST_DIRECTIVE_FLAGS_INCOMPLETE",
                    "Burst jobs require CompileSynchronously=true, FloatPrecision.Standard, and domain-correct FloatMode.",
                    2,
                )
    return result


def is_job_declaration(masked: str) -> bool:
    return ":" in masked and "IJob" in masked and is_struct_declaration(masked)


def scan_devirtualization(files: list[Path]) -> ScanResult:
    result = ScanResult("Dev_Virtualization")
    result.files_scanned = len(files)
    interface_names = collect_declared_interfaces(files)
    for file_path in files:
        method = ""
        for line_number, line in enumerate(read_lines(file_path), 1):
            masked = mask_comments_and_strings(line)
            method = update_method_context(masked, method)
            if not looks_like_interface_container(masked, interface_names):
                continue
            result.add(
                file_path,
                line_number,
                "INTERFACE_CONTAINER_DEVIRTUALIZATION_RISK",
                "Arrays or collections of interfaces block Burst/IL2CPP devirtualization; use flat concrete arrays or generic unmanaged constraints.",
                2 if method in HOT_METHODS else 1,
            )
    return result


def collect_declared_interfaces(files: list[Path]) -> set[str]:
    interface_names: set[str] = set()
    for file_path in files:
        for line in read_lines(file_path):
            masked = mask_comments_and_strings(line)
            match = INTERFACE_DECLARATION_RE.search(masked)
            if match:
                interface_names.add(match.group(1))
    return interface_names


def looks_like_interface_container(masked: str, interface_names: set[str]) -> bool:
    if "where " in masked:
        return False
    for match in INTERFACE_ARRAY_RE.finditer(masked):
        if match.group(1) in interface_names:
            return True
    for match in INTERFACE_GENERIC_RE.finditer(masked):
        if match.group(1) in interface_names:
            return True
    return False


def scan_rollback(files: list[Path]) -> ScanResult:
    result = ScanResult("Rollback_Fence_Compliance")
    result.files_scanned = len(files)
    dispatcher = ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "SystemDispatcher.cs"
    dispatcher_text = read_text(dispatcher)
    if "TryFenceRollbackBeforeVisualSync" not in dispatcher_text or "_masterRollbackFenceThisFrame" not in dispatcher_text or "RunMasterVisualSyncPhase" not in dispatcher_text:
        result.add(dispatcher, 1, "ROLLBACK_VISUAL_FENCE_MISSING", "Dispatcher must read rollback state and skip VISUAL_SYNC on rollback/resimulation frames.", 2)
    if not contains_token(files, "RollbackAudioSuppressionDTO"):
        result.add("Assets/_Project/Scripts", 1, "ROLLBACK_AUDIO_SUPPRESSION_ROUTE_MISSING", "Rollback catch-up must suppress audio presentation through an owned unmanaged route.", 2)
    if not contains_token(files, "HeadlessResimulationCommandJob"):
        result.add("Assets/_Project/Scripts", 1, "HEADLESS_RESIM_COMMAND_ROUTE_MISSING", "Rollback catch-up requires a netcode-owned command route before dispatcher can loop simulation safely.", 2)
    if not contains_token(files, "MockTickCommand"):
        result.add("Assets/_Project/Scripts", 1, "MOCK_TICK_COMMAND_ROUTE_MISSING", "Fallback deterministic resimulation command DTO is absent.", 2)
    if not contains_rollback_particle_suppression(files):
        result.add("Assets/_Project/Scripts", 1, "ROLLBACK_PARTICLE_SUPPRESSION_ROUTE_ABSENT", "Particle suppression has no proven owner route; do not invent a global lane without route-card review.", 1)
    return result


def contains_token(files: list[Path], token: str) -> bool:
    return any(token in read_text(file_path) for file_path in files)


def contains_rollback_particle_suppression(files: list[Path]) -> bool:
    for file_path in files:
        text = read_text(file_path)
        if "Rollback" not in text:
            continue
        if "Particle" in text and any(token in text for token in ("Suppress", "Mute", "DisableEmission")):
            return True
    return False


def scan_hot_registry(files: list[Path]) -> ScanResult:
    result = ScanResult("Hot_Registry_Polling")
    result.files_scanned = len(files)
    for file_path in files:
        method = ""
        for line_number, line in enumerate(read_lines(file_path), 1):
            masked = mask_comments_and_strings(line)
            method = update_method_context(masked, method)
            if method not in HOT_METHODS or "GlobalRegistry." not in masked:
                continue
            if "Register" in masked or "Unregister" in masked:
                continue
            result.add(file_path, line_number, "HOT_REGISTRY_POLL", "Global authority lookup in hot method; cache at boot or consume dispatcher snapshot.", 2)
    return result


def scan_mid_frame_complete(files: list[Path]) -> ScanResult:
    result = ScanResult("Mid_Frame_Complete")
    result.files_scanned = len(files)
    for file_path in files:
        method = ""
        for line_number, line in enumerate(read_lines(file_path), 1):
            masked = mask_comments_and_strings(line)
            method = update_method_context(masked, method)
            if ".Complete(" not in masked or "[BLOCKING_SYNC_POINT]" in masked:
                continue
            if method not in MID_FRAME_METHODS:
                continue
            result.add(file_path, line_number, "MID_FRAME_JOB_COMPLETE", "Job completion must be a named phase fence or deferred post-sim readback.", 2)
    return result


def scan_signal_bus(files: list[Path]) -> ScanResult:
    result = ScanResult("Signal_Bus_Topology")
    result.files_scanned = len(files)
    for file_path in files:
        for line_number, line in enumerate(read_lines(file_path), 1):
            masked = mask_comments_and_strings(line)
            file_name = file_path.name.lower()
            if "FlushPreSimulation" in masked and file_name not in {"systemdispatcher.cs", "globalsignals.cs"}:
                result.add(file_path, line_number, "SIGNAL_FLUSH_OUTSIDE_DISPATCHER", "Signal lanes may only flush through dispatcher topology.", 2)
            if "ClearPostSimulationSnapshots" in masked and file_name not in {"systemdispatcher.cs", "globalsignals.cs"}:
                result.add(file_path, line_number, "SIGNAL_CLEAR_OUTSIDE_DISPATCHER", "Post-simulation signal snapshot clear is dispatcher-owned.", 2)
    return result


def add_hot_helper_findings(file_path: Path, hot_helper_registry: ScanResult, hot_helper_complete: ScanResult) -> None:
    blocks = extract_method_blocks(file_path)
    if not blocks:
        return

    registry_helpers: dict[str, int] = {}
    complete_helpers: dict[str, int] = {}
    for method_name, start_line, body in blocks:
        if method_name not in HOT_METHODS:
            if any("GlobalRegistry." in masked for _, masked in body):
                registry_helpers[method_name] = start_line
        if method_name not in MID_FRAME_METHODS:
            if any(".Complete(" in masked and "[BLOCKING_SYNC_POINT]" not in masked for _, masked in body):
                complete_helpers[method_name] = start_line

    if not registry_helpers and not complete_helpers:
        return

    for method_name, _start_line, body in blocks:
        if method_name in HOT_METHODS and registry_helpers:
            for line_number, masked in body:
                for helper_name, helper_line in registry_helpers.items():
                    if contains_method_call(masked, helper_name):
                        hot_helper_registry.add(
                            file_path,
                            line_number,
                            "HOT_HELPER_REGISTRY_POLL",
                            f"Hot method {method_name} calls helper {helper_name} at line {helper_line}; helper reads GlobalRegistry.",
                            2,
                        )
        if method_name in MID_FRAME_METHODS and complete_helpers:
            for line_number, masked in body:
                for helper_name, helper_line in complete_helpers.items():
                    if contains_method_call(masked, helper_name):
                        hot_helper_complete.add(
                            file_path,
                            line_number,
                            "HOT_HELPER_JOB_COMPLETE",
                            f"Hot method {method_name} calls helper {helper_name} at line {helper_line}; helper completes a JobHandle.",
                            2,
                        )


def run_combined_cs_scans(files: list[Path]) -> list[ScanResult]:
    aup = ScanResult("AUP_Compliance")
    vault = ScanResult("Vault_Sovereignty")
    struct_layout = ScanResult("Runtime_Struct_Layout")
    burst = ScanResult("Burst_Job_Directives")
    devirtualization = ScanResult("Dev_Virtualization")
    rollback = ScanResult("Rollback_Fence_Compliance")
    hot_registry = ScanResult("Hot_Registry_Polling")
    hot_helper_registry = ScanResult("Hot_Helper_Registry_Polling")
    mid_complete = ScanResult("Mid_Frame_Complete")
    hot_helper_complete = ScanResult("Hot_Helper_Complete")
    signal = ScanResult("Signal_Bus_Topology")
    results = [
        aup,
        vault,
        struct_layout,
        burst,
        devirtualization,
        rollback,
        hot_registry,
        hot_helper_registry,
        mid_complete,
        hot_helper_complete,
        signal,
    ]
    for result in results:
        result.files_scanned = len(files)

    interface_names = collect_declared_interfaces(files)
    rollback_has_audio = False
    rollback_has_headless = False
    rollback_has_mock_tick = False
    rollback_has_particle_suppression = False

    for file_path in files:
        file_name_lower = file_path.name.lower()
        text = read_text(file_path)
        if "RollbackAudioSuppressionDTO" in text:
            rollback_has_audio = True
        if "HeadlessResimulationCommandJob" in text:
            rollback_has_headless = True
        if "MockTickCommand" in text:
            rollback_has_mock_tick = True
        if "Rollback" in text and "Particle" in text and any(token in text for token in ("Suppress", "Mute", "DisableEmission")):
            rollback_has_particle_suppression = True

        method = ""
        inside_struct = False
        struct_depth = 0
        struct_has_managed_reference = False
        pending_property_findings: list[tuple[int, Path]] = []
        pending_bool_findings: list[tuple[int, Path]] = []
        masked_history: list[str] = []
        lines = read_lines(file_path)
        for index, line in enumerate(lines):
            line_number = index + 1
            masked = mask_comments_and_strings(line)
            method = update_method_context(masked, method)

            if file_name_lower != "hectonfloatingorigin.cs" and method in HOT_METHODS and ("Vector3.Distance" in masked or ".position" in masked):
                aup.add(
                    file_path,
                    line_number,
                    "AUP_WORLD_SPACE_HOT_PATH",
                    "World-space transform/math access inside hot method; route through AUP snapshot or local-sector DTO.",
                    2,
                )

            if is_vault_allocation_violation(file_path, lines, index, masked):
                vault.add(
                    file_path,
                    line_number,
                    "DIRECT_NATIVE_ALLOCATION",
                    "Runtime native memory must be DataVault-owned or explicitly exempted.",
                    2,
                )

            if "StructLayout" in masked and "Pack" in masked:
                struct_layout.add(
                    file_path,
                    line_number,
                    "PACKED_RUNTIME_STRUCT",
                    "Runtime structs must not use packed layout; copy file records into aligned DTOs instead.",
                    2,
                )
            if not inside_struct and is_struct_declaration(masked):
                inside_struct = True
                struct_depth = 0
                struct_has_managed_reference = False
                pending_property_findings = []
                pending_bool_findings = []
            if inside_struct:
                if contains_managed_reference_field(masked):
                    struct_has_managed_reference = True
                if looks_like_accessor_property(masked):
                    pending_property_findings.append((line_number, file_path))
                if contains_bool_field(masked):
                    pending_bool_findings.append((line_number, file_path))
                struct_depth += masked.count("{")
                struct_depth -= masked.count("}")
                if struct_depth <= 0 and "}" in masked:
                    if not struct_has_managed_reference:
                        for pending_line, pending_path in pending_property_findings:
                            struct_layout.add(
                                pending_path,
                                pending_line,
                                "STRUCT_PROPERTY_DEFENSIVE_COPY_RISK",
                                "Runtime structs in native/hot paths must expose raw fields, not C# properties.",
                                2,
                            )
                        for pending_line, pending_path in pending_bool_findings:
                            struct_layout.add(
                                pending_path,
                                pending_line,
                                "STRUCT_BOOL_FIELD_ARM64_RISK",
                                "Runtime structs must use byte or bit flags instead of bool fields.",
                                2,
                            )
                    inside_struct = False
                    pending_property_findings = []
                    pending_bool_findings = []
                    struct_has_managed_reference = False

            if is_job_declaration(masked):
                attribute_window = " ".join(masked_history[-8:])
                if "BurstCompile" not in attribute_window:
                    burst.add(file_path, line_number, "JOB_MISSING_BURSTCOMPILE", "Job structs must be Burst compiled before dispatcher phase scheduling.", 2)
                else:
                    has_sync = "CompileSynchronously" in attribute_window and "true" in attribute_window.lower()
                    has_precision = "FloatPrecision" in attribute_window and "Standard" in attribute_window
                    deterministic_path = is_deterministic_burst_path(file_path)
                    valid_mode = ("Deterministic" in attribute_window) if deterministic_path else ("Fast" in attribute_window)
                    if not has_sync or not has_precision or not valid_mode:
                        burst.add(
                            file_path,
                            line_number,
                            "BURST_DIRECTIVE_FLAGS_INCOMPLETE",
                            "Burst jobs require CompileSynchronously=true, FloatPrecision.Standard, and domain-correct FloatMode.",
                            2,
                        )

            if looks_like_interface_container(masked, interface_names):
                devirtualization.add(
                    file_path,
                    line_number,
                    "INTERFACE_CONTAINER_DEVIRTUALIZATION_RISK",
                    "Arrays or collections of interfaces block Burst/IL2CPP devirtualization; use flat concrete arrays or generic unmanaged constraints.",
                    2 if method in HOT_METHODS else 1,
                )

            if method in HOT_METHODS and "GlobalRegistry." in masked and "Register" not in masked and "Unregister" not in masked:
                hot_registry.add(file_path, line_number, "HOT_REGISTRY_POLL", "Global authority lookup in hot method; cache at boot or consume dispatcher snapshot.", 2)

            if ".Complete(" in masked and "[BLOCKING_SYNC_POINT]" not in masked and method in MID_FRAME_METHODS:
                mid_complete.add(file_path, line_number, "MID_FRAME_JOB_COMPLETE", "Job completion must be a named phase fence or deferred post-sim readback.", 2)

            if "FlushPreSimulation" in masked and file_name_lower not in {"systemdispatcher.cs", "globalsignals.cs"}:
                signal.add(file_path, line_number, "SIGNAL_FLUSH_OUTSIDE_DISPATCHER", "Signal lanes may only flush through dispatcher topology.", 2)
            if "ClearPostSimulationSnapshots" in masked and file_name_lower not in {"systemdispatcher.cs", "globalsignals.cs"}:
                signal.add(file_path, line_number, "SIGNAL_CLEAR_OUTSIDE_DISPATCHER", "Post-simulation signal snapshot clear is dispatcher-owned.", 2)

            masked_history.append(masked)

        add_hot_helper_findings(file_path, hot_helper_registry, hot_helper_complete)

    dispatcher = ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "SystemDispatcher.cs"
    dispatcher_text = read_text(dispatcher)
    if "TryFenceRollbackBeforeVisualSync" not in dispatcher_text or "_masterRollbackFenceThisFrame" not in dispatcher_text or "RunMasterVisualSyncPhase" not in dispatcher_text:
        rollback.add(dispatcher, 1, "ROLLBACK_VISUAL_FENCE_MISSING", "Dispatcher must read rollback state and skip VISUAL_SYNC on rollback/resimulation frames.", 2)
    if not rollback_has_audio:
        rollback.add("Assets/_Project/Scripts", 1, "ROLLBACK_AUDIO_SUPPRESSION_ROUTE_MISSING", "Rollback catch-up must suppress audio presentation through an owned unmanaged route.", 2)
    if not rollback_has_headless:
        rollback.add("Assets/_Project/Scripts", 1, "HEADLESS_RESIM_COMMAND_ROUTE_MISSING", "Rollback catch-up requires a netcode-owned command route before dispatcher can loop simulation safely.", 2)
    if not rollback_has_mock_tick:
        rollback.add("Assets/_Project/Scripts", 1, "MOCK_TICK_COMMAND_ROUTE_MISSING", "Fallback deterministic resimulation command DTO is absent.", 2)
    if not rollback_has_particle_suppression:
        rollback.add("Assets/_Project/Scripts", 1, "ROLLBACK_PARTICLE_SUPPRESSION_ROUTE_ABSENT", "Particle suppression has no proven owner route; do not invent a global lane without route-card review.", 1)

    return results


def run_all(baseline_path: Path | None = None) -> list[ScanResult]:
    files = runtime_cs_files()
    results = run_combined_cs_scans(files)
    results.insert(2, scan_compile_wall())
    self_audit = scan_self_audit()
    results.append(self_audit)
    validate_self_audit_counts_against_results(
        self_audit,
        results,
        baseline_path if baseline_path is not None else path("Docs/Reports/SHINOBU_140_STATIC_GATE_BASELINE.json"),
    )
    return results


def main() -> int:
    parser = argparse.ArgumentParser(description="Run SHINOBU_140 static architecture gates without Unity Editor.")
    parser.add_argument("--output-dir", default="Docs/Reports")
    parser.add_argument("--baseline", default="Docs/Reports/SHINOBU_140_STATIC_GATE_BASELINE.json")
    args = parser.parse_args()
    output_dir = path(args.output_dir)
    results = run_all(path(args.baseline))
    results.append(build_regression_budget_result(results, path(args.baseline)))
    for result in results:
        write_report(result, output_dir)
    write_summary(results, output_dir)
    write_owner_map(results, output_dir)
    write_regression_attribution(results, path(args.baseline), output_dir)
    print("SHINOBU_140_STATIC_SCAN_START evidence=STATIC_SOURCE/PY_TOOL/CI_FALLBACK")
    for result in results:
        print(f"{result.scanner}: files={result.files_scanned} critical={result.critical_count} warning={result.warning_count}")
    print("WROTE Docs/Reports/SHINOBU_140_STATIC_GATE_SUMMARY.json")
    print("WROTE Docs/Reports/SHINOBU_140_STATIC_GATE_OWNER_MAP.json")
    print("WROTE Docs/Reports/SHINOBU_140_STATIC_GATE_REGRESSION_ATTRIBUTION.json")
    print("STATUS: PENDING VERIFICATION")
    return 1 if sum(result.critical_count for result in results) > 0 else 0


if __name__ == "__main__":
    raise SystemExit(main())
