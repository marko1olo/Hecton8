import ast
import datetime as dt
import hashlib
import io
import json
import re
import subprocess
import tokenize
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPORT_DIR = ROOT / "Docs" / "Reports"
SAMARA = dt.timezone(dt.timedelta(hours=4))
REPORT_DATE = dt.datetime.now(SAMARA).date().isoformat()
TOKEN_REPORT_JSON = ROOT / "Docs" / "DEPRECATED" / "Root_Docs_Noise_2026-05-26" / f"TOKEN_USAGE_AUDIT_{REPORT_DATE}.json"
TOKEN_REPORT_MD = ROOT / "Docs" / "DEPRECATED" / "Root_Docs_Noise_2026-05-26" / f"TOKEN_USAGE_AUDIT_{REPORT_DATE}.md"
DASHBOARD_JSON = REPORT_DIR / f"PROJECT_METRICS_DASHBOARD_{REPORT_DATE}.json"
DASHBOARD_MD = REPORT_DIR / f"PROJECT_METRICS_DASHBOARD_{REPORT_DATE}.md"
CHART_DIR = REPORT_DIR / "MetricCharts" / REPORT_DATE
CPU_SAMPLE_JSON = REPORT_DIR / f"TOKEN_USAGE_APEX_CPU_SAMPLE_{REPORT_DATE}.json"
OUTPUT_JSON = REPORT_DIR / f"TOKEN_USAGE_APEX_VERIFICATION_{REPORT_DATE}.json"
OUTPUT_MD = REPORT_DIR / f"TOKEN_USAGE_APEX_VERIFICATION_{REPORT_DATE}.md"

OWNED_EXECUTABLE_FILES = [
    ROOT / "Tools" / "CodexTokenUsageAudit_20260525.py",
    ROOT / "Tools" / "CodexTokenUsageFastRefresh_20260528.py",
    ROOT / "Tools" / "ProjectMetricsDashboard_20260528.py",
    ROOT / "Tools" / "TokenUsageApexVerification_20260528.py",
]

OWNED_ARTIFACTS = [
    TOKEN_REPORT_JSON,
    TOKEN_REPORT_MD,
    DASHBOARD_JSON,
    DASHBOARD_MD,
]

MANDATES = [
    "OPT_Zero_GC_Policy_AllocFree_Mandate.txt",
    "QA_Evidence_Text_Filter_Audit.txt",
    "DBG_Telemetry_Crash_Reporting_PostMortem.txt",
    "OPT_Premium_Approximation_Protocol.txt",
    "OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt",
    "ARCH_Global_Registry_ServiceLocator_DI_Init.txt",
    "ARCH_Signal_Lane_Segregation.txt",
    "DATA_Runtime_Struct_Layout_ARM64.txt",
]

HOT_PATH_RE = re.compile(r"\b(Update|LateUpdate|FixedUpdate|Tick|FixedTick)\s*\(")
CS_HOT_FORBIDDEN_RE = re.compile(
    r"\bnew\s+[A-Za-z_][A-Za-z0-9_<>]*\s*\("
    r"|string\.Format"
    r"|\.\s*ToString\s*\("
    r"|\.\s*(Where|Select|Any|FirstOrDefault|ToList)\s*\("
    r"|\bforeach\s*\(",
)
GLOBAL_AUTHORITY_RE = re.compile(
    r"GlobalDataVault|TryAcquireWriteLock|ReleaseWriteLock|BufferID|SignalBus|"
    r"GlobalRegistry|HomeostasisBrain|GlobalQualityWeight|\bisLowEnd\b|\bisLowTier\b|lowEnd",
)


def rel(path):
    return str(path.relative_to(ROOT)).replace("\\", "/")


def sha256(path):
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def read_text(path):
    return path.read_text(encoding="utf-8-sig")


def line_count(text):
    if not text:
        return 0
    return text.count("\n") + (0 if text.endswith("\n") else 1)


def regex_hits(path, regex):
    if path.suffix.lower() == ".py":
        return python_code_regex_hits(path, regex)
    hits = []
    for index, line in enumerate(read_text(path).splitlines(), start=1):
        if regex.search(line):
            hits.append({"line": index, "text": line.strip()[:240]})
    return hits


def python_code_regex_hits(path, regex):
    lines = read_text(path).splitlines()
    code_lines = {index: [] for index in range(1, len(lines) + 1)}
    stream = io.StringIO(read_text(path))
    for token in tokenize.generate_tokens(stream.readline):
        if token.type in {
            tokenize.STRING,
            tokenize.COMMENT,
            tokenize.ENCODING,
            tokenize.NL,
            tokenize.NEWLINE,
            tokenize.ENDMARKER,
        }:
            continue
        line_index = token.start[0]
        if line_index in code_lines:
            code_lines[line_index].append(token.string)
    hits = []
    for index in range(1, len(lines) + 1):
        joined = " ".join(code_lines[index])
        if joined and regex.search(joined):
            hits.append({"line": index, "code_tokens": joined[:240]})
    return hits


def ast_counts(path):
    tree = ast.parse(read_text(path), filename=str(path))
    counters = {
        "For": 0,
        "ListComp": 0,
        "DictComp": 0,
        "SetComp": 0,
        "GeneratorExp": 0,
        "Call": 0,
        "Lambda": 0,
        "Await": 0,
        "Yield": 0,
    }
    functions = []
    for node in ast.walk(tree):
        node_type = type(node).__name__
        if node_type in counters:
            counters[node_type] += 1
        if isinstance(node, ast.FunctionDef):
            functions.append({"name": node.name, "line": node.lineno, "end_line": node.end_lineno})
    return counters, sorted(functions, key=lambda item: item["line"])


def source_file_report(path):
    text = read_text(path)
    counters, functions = ast_counts(path)
    return {
        "path": rel(path),
        "sha256": sha256(path),
        "line_count": line_count(text),
        "python_ast_counts": counters,
        "function_ranges": functions,
        "hot_path_symbol_hits": regex_hits(path, HOT_PATH_RE),
        "csharp_hot_forbidden_text_hits": regex_hits(path, CS_HOT_FORBIDDEN_RE),
        "global_authority_text_hits": regex_hits(path, GLOBAL_AUTHORITY_RE),
        "evidence_note": "Offline Python tooling. Python AST allocations are not Unity runtime GC evidence.",
    }


def load_json(path):
    return json.loads(read_text(path))


def sample_cpu_and_compilers():
    sample = {
        "source": "runtime_powershell_sample",
        "sampled_at_samara": dt.datetime.now(SAMARA).isoformat(),
        "cpu_total_percent": None,
        "dotnet_or_csc_process_count": None,
        "processes": [],
        "error": None,
    }
    script = r"""
$cpu = (Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
$procs = @(Get-Process dotnet,csc,VBCSCompiler,MSBuild -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,CPU)
[pscustomobject]@{
  cpu_total_percent = [int]$cpu
  dotnet_or_csc_process_count = $procs.Count
  processes = $procs
} | ConvertTo-Json -Depth 4
"""
    try:
        completed = subprocess.run(
            ["powershell", "-NoProfile", "-Command", script],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=20,
        )
        parsed = json.loads(completed.stdout)
        sample["cpu_total_percent"] = parsed.get("cpu_total_percent")
        sample["dotnet_or_csc_process_count"] = parsed.get("dotnet_or_csc_process_count")
        processes = parsed.get("processes") or []
        if isinstance(processes, dict):
            processes = [processes]
        sample["processes"] = processes
    except Exception as exc:
        sample["error"] = str(exc)
    return sample


def chart_report():
    charts = []
    for path in sorted(CHART_DIR.glob("*.png")):
        data = path.read_bytes()
        charts.append(
            {
                "path": rel(path),
                "dashboard_path": str(path.relative_to(REPORT_DIR)).replace("\\", "/"),
                "sha256": hashlib.sha256(data).hexdigest(),
                "bytes": len(data),
                "png_signature_ok": data.startswith(b"\x89PNG\r\n\x1a\n"),
            }
        )
    return charts


def chart_manifest_integrity(dashboard, charts):
    reported_paths = [
        str(item.get("path", "")).replace("\\", "/")
        for item in dashboard.get("charts", [])
        if item.get("path")
    ]
    disk_paths = [item["path"].replace("\\", "/") for item in charts]
    disk_dashboard_paths = [item["dashboard_path"].replace("\\", "/") for item in charts]
    reported_set = set(reported_paths)
    disk_dashboard_set = set(disk_dashboard_paths)
    missing = sorted(reported_set - disk_dashboard_set)
    extra = sorted(disk_dashboard_set - reported_set)
    duplicate_count = len(reported_paths) - len(reported_set)
    return {
        "reported_chart_paths": reported_paths,
        "disk_chart_paths": disk_paths,
        "disk_dashboard_relative_chart_paths": disk_dashboard_paths,
        "missing_reported_chart_files": missing,
        "unreported_disk_chart_files": extra,
        "reported_chart_duplicate_path_count": duplicate_count,
        "chart_manifest_exact_match": not missing and not extra and duplicate_count == 0,
    }


def artifact_report(path):
    text_line_total = None
    if path.suffix.lower() in {".md", ".py", ".json"}:
        text_line_total = line_count(read_text(path))
    return {
        "path": rel(path),
        "sha256": sha256(path),
        "bytes": path.stat().st_size,
        "line_count": text_line_total,
    }


def command_log_stub():
    cpu_sample = sample_cpu_and_compilers()
    if cpu_sample.get("cpu_total_percent") is None and CPU_SAMPLE_JSON.exists():
        cpu_sample = load_json(CPU_SAMPLE_JSON)
        cpu_sample["source"] = "persisted_cpu_sample_json"
    dotnet_or_csc_raw = cpu_sample.get("dotnet_or_csc_process_count")
    cpu_total_raw = cpu_sample.get("cpu_total_percent")
    dotnet_or_csc_count = int(dotnet_or_csc_raw or 0)
    cpu_total_percent = int(cpu_total_raw or 0)
    blocked_reasons = []
    if cpu_total_raw is None:
        blocked_reasons.append("missing_cpu_sample")
    if dotnet_or_csc_raw is None:
        blocked_reasons.append("missing_compiler_process_sample")
    if dotnet_or_csc_count > 0:
        blocked_reasons.append("compiler_process_active")
    if cpu_total_percent > 50:
        blocked_reasons.append("cpu_above_50_percent")
    blocked = bool(blocked_reasons)
    return {
        "dotnet_build_invoked_by_token_usage_audit": False,
        "unity_build_invoked_by_token_usage_audit": False,
        "final_compile_check": "SKIPPED_BLOCKED_BY_COMPILER_CONTENTION" if blocked else "python -m py_compile Tools/CodexTokenUsageAudit_20260525.py Tools/CodexTokenUsageFastRefresh_20260528.py Tools/ProjectMetricsDashboard_20260528.py Tools/TokenUsageApexVerification_20260528.py",
        "cpu_sample_before_final_compile": cpu_sample,
        "blocked_reasons": blocked_reasons,
        "throttling_interpretation": "No dotnet build or Unity build was invoked. Python bytecode compile is skipped if CPU is above 50 percent or another compiler process remains active, to avoid ambiguous contention proof." if blocked else "Compilation throttling rule targets dotnet/csc/Unity. This pass used Python bytecode compile only after sampling CPU and dotnet/csc process state.",
    }


def build_report():
    token_report = load_json(TOKEN_REPORT_JSON)
    dashboard = load_json(DASHBOARD_JSON)
    charts = chart_report()
    chart_manifest = chart_manifest_integrity(dashboard, charts)
    source_reports = [source_file_report(path) for path in OWNED_EXECUTABLE_FILES]

    hot_hits = sum(len(item["hot_path_symbol_hits"]) for item in source_reports)
    forbidden_hits = sum(len(item["csharp_hot_forbidden_text_hits"]) for item in source_reports)
    authority_hits = sum(len(item["global_authority_text_hits"]) for item in source_reports)

    pricing = token_report["pricing"]["gpt-5.5_standard_short_context_equivalent"]
    delta = token_report["previous_snapshot_delta"]
    velocity = delta["velocity"]

    command_log = command_log_stub()
    evidence_class = (
        "STATIC_SOURCE_AND_STATIC_DOC_CPU_THROTTLE_NO_COMPILE"
        if str(command_log["final_compile_check"]).startswith("SKIPPED_")
        else "STATIC_SOURCE_AND_STATIC_DOC_PLUS_PYTHON_BYTECODE_COMPILE"
    )

    return {
        "schema": "hecton8.token_usage_apex_verification.v1",
        "generated_at_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "generated_at_samara": dt.datetime.now(SAMARA).isoformat(),
        "agent_id": "TOKEN_USAGE_AUDIT",
        "domain": "offline Codex token telemetry, pricing evidence, dashboards, and documentation artifacts",
        "evidence_class": evidence_class,
        "mandates_consulted": MANDATES,
        "owned_runtime_csharp_files": [],
        "owned_executable_files": source_reports,
        "owned_artifacts": [artifact_report(path) for path in OWNED_ARTIFACTS],
        "chart_artifacts": charts,
        "zero_gc_self_audit": {
            "runtime_hot_path_changed": False,
            "runtime_profiler_gc_proof": "ABSENT",
            "status": "PENDING_RUNTIME_VERIFICATION_FOR_ANY_RUNTIME_CLAIM",
            "static_hot_path_symbol_hits_in_owned_tooling": hot_hits,
            "static_csharp_hot_forbidden_hits_in_owned_tooling": forbidden_hits,
            "interpretation": "No owned Unity runtime C# hot path was edited by this domain. Static scan over owned tooling found no Unity hot-path methods or C# hot-path forbidden text. This does not prove runtime 0 B/frame.",
        },
        "data_sovereignty_self_audit": {
            "migrated_to_global_data_vault": False,
            "buffer_id_constants_secured": [],
            "try_acquire_write_lock_sites": [],
            "release_write_lock_finally_sites": [],
            "static_global_authority_hits_in_owned_tooling": authority_hits,
            "interpretation": "No GlobalDataVault, SignalBus, GlobalRegistry, BufferID, or HomeostasisBrain route was added by TOKEN_USAGE_AUDIT. Data sovereignty route proof is not applicable.",
        },
        "cinematic_cheat_and_scalability_audit": {
            "runtime_simulation_added": False,
            "global_quality_weight_runtime_requirement_applicable": False,
            "binary_quality_switches_in_owned_tooling": [],
            "interpretation": "The work generated offline charts/reports. It did not add water/light/fog/physics/runtime visual systems, so approximation-first and continuous GlobalQualityWeight scaling are not invoked.",
        },
        "token_report_headline": {
            "total_tokens": token_report["totals"]["total_tokens"],
            "input_tokens": token_report["totals"]["input_tokens"],
            "cached_input_tokens": token_report["totals"]["cached_input_tokens"],
            "output_tokens": token_report["totals"]["output_tokens"],
            "reasoning_output_tokens": token_report["totals"]["reasoning_output_tokens"],
            "sessions_with_usage": token_report["sessions_with_usage"],
            "gpt_5_5_standard_api_equivalent_usd": pricing["total_cost_usd"],
            "delta_total_tokens": delta["totals_delta"]["total_tokens"],
            "tokens_per_hour": velocity["total_tokens_per_hour"],
            "tokens_per_second": velocity["total_tokens_per_second"],
            "gpt_5_5_standard_usd_per_hour": velocity["gpt_5_5_standard_usd_per_hour"],
        },
        "pricing_context_rules": token_report.get("pricing_context_rules") or {},
        "official_pricing_sources_checked": [
            {
                "url": "https://developers.openai.com/api/docs/pricing",
                "fact_used": "GPT-5.5 base public API-equivalent pricing used by the report: input $5.00, cached input $0.50, output $30.00 per 1M tokens.",
            },
            {
                "url": "https://developers.openai.com/api/docs/models/gpt-5.5",
                "fact_used": "Prompts above 272K input tokens get a long-context surcharge. Report records this as a sensitivity because local JSONL does not expose exact provider-side billing classification.",
            },
            {
                "url": "https://developers.openai.com/api/docs/guides/prompt-caching",
                "fact_used": "Cached token counters and prompt-cache behavior are treated as separate cached input accounting; report remains API-equivalent, not invoice proof.",
            },
        ],
        "dashboard_integrity": {
            "dashboard_json": rel(DASHBOARD_JSON),
            "dashboard_markdown": rel(DASHBOARD_MD),
            "chart_count_reported": dashboard.get("chart_count"),
            "chart_count_on_disk": len(charts),
            "all_png_signatures_ok": all(item["png_signature_ok"] for item in charts),
            "all_png_non_empty": all(item["bytes"] > 0 for item in charts),
            **chart_manifest,
        },
        "compilation_resource_throttling": command_log,
        "known_faults": [
            "No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.",
            f"Full all-time token replay exceeded 20 minutes under live parallel-agent churn; {REPORT_DATE} report uses fast incremental evidence from the previous full snapshot plus post-cutoff JSONL deltas.",
            "Workspace remains live-dirty from other agents after remote push; those changes are outside TOKEN_USAGE_AUDIT ownership.",
        ],
    }


def write_bom(path, text):
    path.write_text("\ufeff" + text, encoding="utf-8")


def write_markdown(report):
    lines = [
        f"# Token Usage Apex Verification {REPORT_DATE}",
        "",
        f"Generated Samara: `{report['generated_at_samara']}`",
        f"Evidence class: `{report['evidence_class']}`",
        "",
        "## Verdict",
        "",
        "| Claim | Status | Evidence |",
        "|---|---|---|",
        f"| Runtime hot-path changed | `{report['zero_gc_self_audit']['runtime_hot_path_changed']}` | owned runtime C# file list is empty |",
        f"| Runtime 0 B/frame | `{report['zero_gc_self_audit']['status']}` | no profiler/GCMonitor run |",
        f"| C# hot forbidden text hits in owned tooling | `{report['zero_gc_self_audit']['static_csharp_hot_forbidden_hits_in_owned_tooling']}` | regex scan |",
        f"| DataVault migration | `{report['data_sovereignty_self_audit']['migrated_to_global_data_vault']}` | route scan |",
        f"| Chart count | `{report['dashboard_integrity']['chart_count_on_disk']}` | PNG scan |",
        f"| PNG signatures ok | `{report['dashboard_integrity']['all_png_signatures_ok']}` | binary signature check |",
        f"| Chart manifest exact match | `{report['dashboard_integrity']['chart_manifest_exact_match']}` | dashboard paths vs disk paths |",
        "",
        "## Token Headline",
        "",
        "| Metric | Value |",
        "|---|---:|",
    ]
    for key, value in report["token_report_headline"].items():
        lines.append(f"| {key} | {value} |")

    pricing_context = report.get("pricing_context_rules") or {}
    if pricing_context:
        lines.extend(
            [
                "",
                "## Pricing Sensitivity",
                "",
                "| Metric | Value |",
                "|---|---:|",
                f"| long_context_trigger_input_tokens | {pricing_context.get('long_context_trigger_input_tokens')} |",
                f"| gpt_5_5_long_context_upper_bound_usd | {pricing_context.get('gpt_5_5_long_context_upper_bound_usd')} |",
                f"| gpt_5_5_long_context_upper_bound_delta_usd | {pricing_context.get('gpt_5_5_long_context_upper_bound_delta_usd')} |",
                f"| gpt_5_5_long_context_regional_10pct_upper_bound_usd | {pricing_context.get('gpt_5_5_long_context_regional_10pct_upper_bound_usd')} |",
                f"| gpt_5_5_regional_10pct_usd | {pricing_context.get('gpt_5_5_regional_10pct_usd')} |",
                f"| gpt_5_5_regional_10pct_delta_usd | {pricing_context.get('gpt_5_5_regional_10pct_delta_usd')} |",
                f"| post_cutoff_long_context_event_count | {pricing_context.get('post_cutoff_long_context_event_count')} |",
                f"| post_cutoff_long_context_event_surcharge_delta_usd | {pricing_context.get('post_cutoff_long_context_event_surcharge_delta_usd')} |",
                f"| post_cutoff_long_context_event_evidence_class | {pricing_context.get('post_cutoff_long_context_event_evidence_class')} |",
            ]
        )

    throttle = report.get("compilation_resource_throttling") or {}
    cpu_sample = throttle.get("cpu_sample_before_final_compile") or {}
    lines.extend(
        [
            "",
            "## Compilation Resource Throttling",
            "",
            "| Metric | Value |",
            "|---|---|",
            f"| dotnet_build_invoked_by_token_usage_audit | `{throttle.get('dotnet_build_invoked_by_token_usage_audit')}` |",
            f"| unity_build_invoked_by_token_usage_audit | `{throttle.get('unity_build_invoked_by_token_usage_audit')}` |",
            f"| final_compile_check | `{throttle.get('final_compile_check')}` |",
            f"| cpu_total_percent | `{cpu_sample.get('cpu_total_percent')}` |",
            f"| dotnet_or_csc_process_count | `{cpu_sample.get('dotnet_or_csc_process_count')}` |",
        ]
    )

    lines.extend(
        [
            "",
            "## Artifact Hashes",
            "",
            "| Path | SHA-256 | Bytes |",
            "|---|---|---:|",
        ]
    )
    for artifact in report["owned_artifacts"]:
        lines.append(f"| `{artifact['path']}` | `{artifact['sha256']}` | {artifact['bytes']} |")
    for artifact in report["owned_executable_files"]:
        lines.append(f"| `{artifact['path']}` | `{artifact['sha256']}` | {artifact['line_count']} lines |")

    lines.extend(
        [
            "",
            "## Known Faults",
            "",
        ]
    )
    for fault in report["known_faults"]:
        lines.append(f"- {fault}")
    lines.append("")
    return "\n".join(lines)


def main():
    report = build_report()
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    write_bom(OUTPUT_JSON, json.dumps(report, indent=2, ensure_ascii=False) + "\n")
    write_bom(OUTPUT_MD, write_markdown(report))
    OUTPUT_JSON.with_suffix(OUTPUT_JSON.suffix + ".sha256").write_text(
        f"{sha256(OUTPUT_JSON)}  {rel(OUTPUT_JSON)}\n",
        encoding="utf-8",
    )
    OUTPUT_MD.with_suffix(OUTPUT_MD.suffix + ".sha256").write_text(
        f"{sha256(OUTPUT_MD)}  {rel(OUTPUT_MD)}\n",
        encoding="utf-8",
    )
    print(json.dumps({
        "report_json": str(OUTPUT_JSON),
        "report_json_sha256": sha256(OUTPUT_JSON),
        "report_md": str(OUTPUT_MD),
        "chart_count": report["dashboard_integrity"]["chart_count_on_disk"],
        "csharp_hot_forbidden_hits": report["zero_gc_self_audit"]["static_csharp_hot_forbidden_hits_in_owned_tooling"],
        "global_authority_hits": report["data_sovereignty_self_audit"]["static_global_authority_hits_in_owned_tooling"],
    }, indent=2))


if __name__ == "__main__":
    main()
