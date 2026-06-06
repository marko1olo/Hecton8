#!/usr/bin/env python3
"""Classify HECTON-8 JobHandle completion sites.

Evidence class: STATIC_SOURCE. This tool separates editor/test/offline
completion, teardown completion, non-blocking dispatcher polling, and suspicious
frame-path barriers. It does not mutate C# and does not prove frame cost.
"""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_REPORT_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "JobCompletionAudit_HFI_AUDIT.md"
DEFAULT_JSON_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "JobCompletionAudit_HFI_AUDIT.json"
SCHEMA = "hecton8.job_completion_audit.v1"
CANONICAL_DISPATCHER_FENCE_SUFFIXES = (
    "Assets/_Project/Scripts/Core/DispatcherJobFence.cs",
    "Assets/_Project/Scripts/Core/Contracts/CoreLowLevelUtilities.cs",
)
CANONICAL_DISPATCHER_FENCE_METHODS = {"TryComplete", "TryFinalizeCompleted"}
PLUGIN_SYNC_GENERATOR_PATH_TOKENS = (
    "/Plugins/MapMagic/",
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
}

HOT_METHOD_TOKENS = (
    "Tick",
    "Update",
    "Frame",
    "Process",
    "Simulate",
    "Step",
)

TEARDOWN_METHOD_TOKENS = (
    "OnDisable",
    "OnDestroy",
    "Dispose",
    "Shutdown",
    "Teardown",
    "Cleanup",
    "Release",
    "Reset",
    "Deinitialize",
    "Drain",
)

EDITOR_OR_TEST_PATH_TOKENS = (
    "/Editor/",
    "/Tests/",
    "/Test/",
    "/Dev/",
    "SmokeTester",
    "Benchmark",
    "Offline",
    "Baker",
    "Forge",
    "Window",
)

METHOD_RE = re.compile(
    r"^\s*(?:public|private|protected|internal)?\s*"
    r"(?:static\s+)?(?:async\s+)?(?:unsafe\s+)?"
    r"(?:[\w<>\[\],\.\?]+\s+)+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^;]*\)\s*(?:\{|=>)?"
)
RAW_COMPLETE_RE = re.compile(r"\.Complete\s*\(")
DISPATCHER_TRY_COMPLETE_RE = re.compile(r"DispatcherJob(?:Fence|Swap)\.TryComplete\s*\(")
FORCE_TRUE_RE = re.compile(r"\bforceComplete\s*:\s*true\b|,\s*true\s*\)")
FORCE_FALSE_RE = re.compile(r"\bforceComplete\s*:\s*false\b|,\s*false\s*\)")
SCHEDULE_COMPLETE_RE = re.compile(r"\.Schedule\s*\([^;\n]*\)\s*\.Complete\s*\(")


@dataclass
class MethodContext:
    name: str
    brace_depth: int


def normalize_path(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.parts)


def iter_cs_files(source_root: Path) -> list[Path]:
    if source_root.is_file():
        return [source_root]
    if not source_root.exists():
        return []
    return [
        path
        for path in sorted(source_root.rglob("*.cs"))
        if not should_skip(path.relative_to(source_root))
    ]


def strip_line_comment(line: str) -> str:
    return line.split("//", 1)[0]


def likely_editor_or_test(rel: str) -> bool:
    normalized = "/" + rel.replace("\\", "/")
    return any(token in normalized for token in EDITOR_OR_TEST_PATH_TOKENS)


def likely_plugin_sync_generator(rel: str) -> bool:
    normalized = "/" + rel.replace("\\", "/")
    return any(token in normalized for token in PLUGIN_SYNC_GENERATOR_PATH_TOKENS)


def is_canonical_dispatcher_fence(rel: str, method_name: str) -> bool:
    normalized = rel.replace("\\", "/")
    return (
        method_name in CANONICAL_DISPATCHER_FENCE_METHODS
        and any(normalized.endswith(suffix) for suffix in CANONICAL_DISPATCHER_FENCE_SUFFIXES)
    )


def extract_domain(rel: str) -> str:
    parts = rel.replace("\\", "/").split("/")
    if "Scripts" in parts:
        tail = parts[parts.index("Scripts") + 1 :]
        if not tail:
            return "Root"
        first = tail[0]
        return "Root" if first.endswith(".cs") else first
    return parts[0] if parts else "Root"


def update_method_context(context: MethodContext | None, code: str) -> MethodContext | None:
    match = METHOD_RE.search(code)
    if match and (context is None or context.brace_depth <= 0):
        name = match.group("name")
        depth = code.count("{") - code.count("}")
        if "=>" in code and "{" not in code:
            depth = 0
        return MethodContext(name=name, brace_depth=depth)

    if context is not None:
        context.brace_depth += code.count("{") - code.count("}")
        if context.brace_depth <= 0 and "}" in code:
            return None
        return context

    return None


def classify_surface(rel: str, method_name: str) -> str:
    if likely_editor_or_test(rel):
        return "EditorOrTest"
    if any(token in method_name for token in TEARDOWN_METHOD_TOKENS):
        return "Teardown"
    if method_name in HOT_METHOD_NAMES or any(token in method_name for token in HOT_METHOD_TOKENS):
        return "FramePath"
    return "RuntimeOther"


def classify_site(rel: str, method_name: str, code: str) -> str:
    surface = classify_surface(rel, method_name)
    raw_complete = bool(RAW_COMPLETE_RE.search(code))
    dispatcher_complete = bool(DISPATCHER_TRY_COMPLETE_RE.search(code))
    schedule_complete = bool(SCHEDULE_COMPLETE_RE.search(code))
    force_true = bool(FORCE_TRUE_RE.search(code))
    force_false = bool(FORCE_FALSE_RE.search(code))

    if (
        raw_complete
        and is_canonical_dispatcher_fence(rel, method_name)
    ):
        return "DispatcherFenceInternalRawComplete"
    if likely_plugin_sync_generator(rel):
        if schedule_complete:
            return "PluginSynchronousGeneratorScheduleComplete"
        if raw_complete:
            return "PluginSynchronousGeneratorRawComplete"
    if surface == "EditorOrTest":
        return "EditorOrTestComplete"
    if surface == "Teardown":
        if raw_complete:
            return "TeardownRawComplete"
        if force_true:
            return "TeardownForcedDispatcherComplete"
        return "TeardownDispatcherComplete"
    if surface == "FramePath":
        if schedule_complete:
            return "FramePathScheduleCompleteChain"
        if raw_complete:
            return "FramePathRawComplete"
        if dispatcher_complete and force_true:
            return "FramePathForcedDispatcherComplete"
        if dispatcher_complete and force_false:
            return "FramePathPolledDispatcherComplete"
        return "FramePathDispatcherComplete"
    if schedule_complete:
        return "RuntimeScheduleCompleteChain"
    if raw_complete:
        return "RuntimeOtherRawComplete"
    if dispatcher_complete and force_true:
        return "RuntimeOtherForcedDispatcherComplete"
    if dispatcher_complete and force_false:
        return "RuntimeOtherPolledDispatcherComplete"
    return "RuntimeOtherDispatcherComplete"


def scan_file(path: Path) -> list[dict[str, object]]:
    rel = normalize_path(path)
    try:
        source = path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return []
    findings: list[dict[str, object]] = []
    context: MethodContext | None = None
    for line_number, raw_line in enumerate(source.splitlines(), 1):
        code = strip_line_comment(raw_line)
        context = update_method_context(context, code)
        if not RAW_COMPLETE_RE.search(code) and not DISPATCHER_TRY_COMPLETE_RE.search(code):
            continue

        method_name = context.name if context is not None else "<global>"
        classification = classify_site(rel, method_name, code)
        findings.append(
            {
                "path": rel,
                "line": line_number,
                "domain": extract_domain(rel),
                "method": method_name,
                "surface": classify_surface(rel, method_name),
                "classification": classification,
                "text": raw_line.strip(),
            }
        )
    return findings


FRAME_PATH_BLOCKERS = {
    "FramePathRawComplete",
    "FramePathForcedDispatcherComplete",
    "FramePathScheduleCompleteChain",
}

RAW_RUNTIME_BLOCKERS = {
    "FramePathRawComplete",
    "RuntimeOtherRawComplete",
    "RuntimeScheduleCompleteChain",
    "FramePathScheduleCompleteChain",
}

PLUGIN_SYNC_COMPLETES = {
    "PluginSynchronousGeneratorRawComplete",
    "PluginSynchronousGeneratorScheduleComplete",
}


def build_payload(source_root: Path) -> dict[str, object]:
    cs_files = iter_cs_files(source_root)
    findings: list[dict[str, object]] = []
    for path in cs_files:
        findings.extend(scan_file(path))

    by_classification: dict[str, int] = {}
    by_surface: dict[str, int] = {}
    by_domain: dict[str, int] = {}
    for finding in findings:
        classification = str(finding["classification"])
        surface = str(finding["surface"])
        domain = str(finding["domain"])
        by_classification[classification] = by_classification.get(classification, 0) + 1
        by_surface[surface] = by_surface.get(surface, 0) + 1
        by_domain[domain] = by_domain.get(domain, 0) + 1

    frame_path_blockers = [
        finding for finding in findings if finding["classification"] in FRAME_PATH_BLOCKERS
    ]
    raw_runtime_blockers = [
        finding for finding in findings if finding["classification"] in RAW_RUNTIME_BLOCKERS
    ]
    plugin_sync_completes = [
        finding for finding in findings if finding["classification"] in PLUGIN_SYNC_COMPLETES
    ]
    return {
        "schema": SCHEMA,
        "sourceRoot": normalize_path(source_root),
        "csFileCount": len(cs_files),
        "findingCount": len(findings),
        "byClassification": dict(sorted(by_classification.items())),
        "bySurface": dict(sorted(by_surface.items())),
        "byDomain": dict(sorted(by_domain.items(), key=lambda item: (-item[1], item[0]))),
        "framePathBlockerCount": len(frame_path_blockers),
        "rawRuntimeBlockerCount": len(raw_runtime_blockers),
        "pluginSyncCompleteCount": len(plugin_sync_completes),
        "framePathBlockers": frame_path_blockers[:100],
        "rawRuntimeBlockers": raw_runtime_blockers[:100],
        "pluginSyncCompletes": plugin_sync_completes[:100],
        "findings": findings[:500],
    }


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_markdown(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        "# Job Completion Audit",
        "",
        "Evidence class: STATIC_SOURCE. No Unity import, player build, profiler, or runtime frame proof was executed.",
        "",
        f"- Schema: `{payload['schema']}`",
        f"- Source root: `{payload['sourceRoot']}`",
        f"- C# files scanned: `{payload['csFileCount']}`",
        f"- Completion findings: `{payload['findingCount']}`",
        f"- Frame-path blocker findings: `{payload['framePathBlockerCount']}`",
        f"- Raw runtime blocker findings: `{payload['rawRuntimeBlockerCount']}`",
        f"- Plugin synchronous generator findings: `{payload['pluginSyncCompleteCount']}`",
        "",
        "## Totals By Classification",
        "",
        "| Classification | Count |",
        "|---|---:|",
    ]
    for key, value in payload["byClassification"].items():
        lines.append(f"| `{key}` | `{value}` |")
    lines.extend(["", "## Totals By Surface", "", "| Surface | Count |", "|---|---:|"])
    for key, value in payload["bySurface"].items():
        lines.append(f"| `{key}` | `{value}` |")
    lines.extend(["", "## Top Domains", "", "| Domain | Count |", "|---|---:|"])
    for key, value in list(payload["byDomain"].items())[:20]:
        lines.append(f"| `{key}` | `{value}` |")
    lines.extend(["", "## Frame-Path Blockers", ""])
    for item in payload["framePathBlockers"]:
        lines.append(
            f"- `{item['path']}:{item['line']}` `{item['classification']}` "
            f"`{item['method']}`: `{item['text']}`"
        )
    lines.extend(["", "## Plugin Synchronous Generator Completes", ""])
    for item in payload["pluginSyncCompletes"]:
        lines.append(
            f"- `{item['path']}:{item['line']}` `{item['classification']}` "
            f"`{item['method']}`: `{item['text']}`"
        )
    lines.extend(["", "## Interpretation", ""])
    lines.append("- Editor/test/offline and teardown completions are review surfaces, not automatic runtime defects.")
    lines.append("- `DispatcherFenceInternalRawComplete` is the canonical Core fence implementation site; callers are audited separately.")
    lines.append("- `PluginSynchronousGenerator*Complete` means a plugin graph API currently requires concrete products before returning; do not rewrite without caller/lifecycle proof.")
    lines.append("- Frame-path raw/forced completions are blockers until moved to dispatcher/fence windows or justified with profiler proof.")
    lines.append("- Polled dispatcher completions with `forceComplete:false` remain review surfaces because same-frame schedule/readback loops can still hide elsewhere.")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def hard_failures(payload: dict[str, object], args: argparse.Namespace) -> list[str]:
    failures: list[str] = []
    if args.fail_on_frame_path and int(payload["framePathBlockerCount"]) > 0:
        failures.append("frame-path raw/forced JobHandle completion detected")
    if args.fail_on_raw_runtime_complete and int(payload["rawRuntimeBlockerCount"]) > 0:
        failures.append("raw runtime JobHandle.Complete detected outside editor/test/teardown")
    if args.fail_on_plugin_sync_complete and int(payload["pluginSyncCompleteCount"]) > 0:
        failures.append("plugin synchronous generator JobHandle.Complete sites require owner review")
    return failures


def print_text(payload: dict[str, object], failures: list[str]) -> None:
    print("Job completion audit")
    print(f"schema={payload['schema']}")
    print(f"sourceRoot={payload['sourceRoot']}")
    print(f"csFiles={payload['csFileCount']}")
    print(f"findings={payload['findingCount']}")
    print(f"framePathBlockers={payload['framePathBlockerCount']}")
    print(f"rawRuntimeBlockers={payload['rawRuntimeBlockerCount']}")
    print(f"pluginSyncCompletes={payload['pluginSyncCompleteCount']}")
    print(f"byClassification={payload['byClassification']}")
    print(f"bySurface={payload['bySurface']}")
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
        print(json.dumps(payload | {"failures": failures}, indent=2, sort_keys=True))
    else:
        print_text(payload, failures)
    return 1 if failures else 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", default=str(DEFAULT_SOURCE_ROOT))
    parser.add_argument("--report-path", default=str(DEFAULT_REPORT_PATH))
    parser.add_argument("--json-path", default=str(DEFAULT_JSON_PATH))
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--fail-on-frame-path", action="store_true")
    parser.add_argument("--fail-on-raw-runtime-complete", action="store_true")
    parser.add_argument("--fail-on-plugin-sync-complete", action="store_true")
    return parser


def main() -> int:
    return run(build_parser().parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
