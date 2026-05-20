#!/usr/bin/env python3
"""Build the HECTON-8 architecture atlas from current disk state.

Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM.
This tool does not claim Unity import, Play Mode, profiler, GC, or player-build proof.
"""

from __future__ import annotations

import json
import re
import subprocess
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path


ROOT = Path(".").resolve()
OUTPUT = ROOT / "Docs" / "DEPENDENCY_GRAPH.md"
JSON_OUTPUT = ROOT / "Docs" / "DEPENDENCY_GRAPH.json"
SOURCE_CACHE_OUTPUT = ROOT / "Docs" / "DEPENDENCY_GRAPH.cache.json"
SOURCE_CACHE_SCHEMA_VERSION = 1
_FILE_LIST_CACHE: dict[tuple[str, ...], list[str]] = {}

SKIP_DIRS = {
    ".git",
    ".codex-artifacts",
    ".codex-build",
    "Build",
    "Builds",
    "Library",
    "Logs",
    "Temp",
    "UserSettings",
    "obj",
}

USING_RE = re.compile(r"^\s*using\s+(Hecton8(?:\.[A-Za-z0-9_]+)+)\s*;", re.M)
STRUCT_RE = re.compile(
    r"\b(?:public|internal|private)?\s*(?:readonly\s+)?(?:partial\s+)?struct\s+(\w+)\s*:\s*([^\{\n]+)"
)
SIGNAL_BUS_RE = re.compile(r"SignalBus\s*<\s*([^>]+?)\s*>\s*\.\s*(Push|Publish|GetFrameSnapshot)\s*\(")
NAMESPACE_RE = re.compile(r"\bnamespace\s+([A-Za-z0-9_.]+)")
GLOBAL_PUBLISH_RE = re.compile(r"GlobalSignals\.Publish\s*\(")
SHERST_RE = re.compile(r"TODO|HACK|FIX LATER", re.IGNORECASE)


def is_skipped(path: Path) -> bool:
    return any(part in SKIP_DIRS for part in path.parts)


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.resolve().relative_to(ROOT).as_posix()


def git_file_list(root_names: tuple[str, ...]) -> list[str] | None:
    cached = _FILE_LIST_CACHE.get(root_names)
    if cached is not None:
        return cached

    try:
        result = subprocess.run(
            ["git", "ls-files", "--cached", "--others", "--exclude-standard", "--", *root_names],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="ignore",
        )
    except (OSError, subprocess.CalledProcessError):
        return None

    files = [line.strip().replace("\\", "/") for line in result.stdout.splitlines() if line.strip()]
    _FILE_LIST_CACHE[root_names] = files
    return files


def git_changed_paths(root_names: tuple[str, ...]) -> set[str] | None:
    try:
        result = subprocess.run(
            ["git", "status", "--porcelain", "-z", "--", *root_names],
            cwd=ROOT,
            check=True,
            capture_output=True,
        )
    except (OSError, subprocess.CalledProcessError):
        return None

    changed: set[str] = set()
    for raw in result.stdout.split(b"\0"):
        if not raw:
            continue
        text = raw.decode("utf-8", errors="ignore").replace("\\", "/")
        path = text[3:] if len(text) > 3 and text[2] == " " else text
        if any(path == root or path.startswith(f"{root}/") for root in root_names):
            changed.add(path)
    return changed


def iter_files(root_names: tuple[str, ...], pattern: str) -> list[Path]:
    if pattern.startswith("*.") and pattern.count("*") == 1:
        suffix = pattern[1:]
        git_files = git_file_list(root_names)
        if git_files is not None:
            return sorted(ROOT / line for line in git_files if line.endswith(suffix))

        try:
            result = subprocess.run(
                ["rg", "--files", "-g", pattern, *root_names],
                cwd=ROOT,
                check=True,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="ignore",
            )
            return sorted(ROOT / line.strip() for line in result.stdout.splitlines() if line.strip())
        except (OSError, subprocess.CalledProcessError):
            pass

    files: list[Path] = []
    for name in root_names:
        root = ROOT / name
        if root.exists():
            files.extend(path for path in root.rglob(pattern) if path.is_file() and not is_skipped(path))
    return sorted(files)


def line_number(text: str, index: int) -> int:
    return text.count("\n", 0, index) + 1


def normalize_signal_name(raw: str) -> str:
    value = raw.strip().replace("global::", "")
    value = value.split(",", 1)[0].strip()
    return value.rsplit(".", 1)[-1]


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="ignore")


def load_source_cache() -> dict[str, object]:
    if not SOURCE_CACHE_OUTPUT.exists():
        return {"files": {}}

    try:
        payload = json.loads(SOURCE_CACHE_OUTPUT.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {"files": {}}

    if payload.get("schema_version") != SOURCE_CACHE_SCHEMA_VERSION:
        return {"files": {}}

    files = payload.get("files")
    if not isinstance(files, dict):
        return {"files": {}}

    return payload


def write_source_cache(files: dict[str, object]) -> None:
    payload = {
        "schema_version": SOURCE_CACHE_SCHEMA_VERSION,
        "files": files,
    }
    SOURCE_CACHE_OUTPUT.write_text(json.dumps(payload, separators=(",", ":")), encoding="utf-8")


def analyze_source_bytes(raw: bytes, path_rel: str, first_party: bool) -> dict[str, object]:
    line_count = raw.count(b"\n") + (0 if not raw or raw.endswith(b"\n") else 1)

    entry: dict[str, object] = {
        "line_count": line_count,
        "first_party": first_party,
        "using_edges": [],
        "signals": [],
        "signal_uses": {},
        "global_publish_sites": [],
    }

    if not first_party:
        return entry

    text = raw.decode("utf-8-sig", errors="ignore")
    namespace_match = NAMESPACE_RE.search(text)
    namespace = namespace_match.group(1) if namespace_match else ""

    parts = path_rel.split("/")
    domain = parts[3] if len(parts) > 4 else "RootScripts"
    edge_counts: Counter[tuple[str, str]] = Counter()
    for match in USING_RE.finditer(text):
        namespace_used = match.group(1)
        namespace_parts = namespace_used.split(".")
        target_domain = namespace_parts[1] if len(namespace_parts) > 1 else "Core"
        if target_domain != domain:
            edge_counts[(domain, target_domain)] += 1

    signals = []
    for match in STRUCT_RE.finditer(text):
        if "ISignal" not in match.group(2):
            continue

        signals.append(
            {
                "name": match.group(1),
                "namespace": namespace,
                "path": path_rel,
                "line": line_number(text, match.start()),
            }
        )

    signal_uses: dict[str, dict[str, object]] = {}
    for match in SIGNAL_BUS_RE.finditer(text):
        name = normalize_signal_name(match.group(1))
        method = match.group(2)
        entry_text = f"{path_rel}:{line_number(text, match.start())}"
        lane = signal_uses.setdefault(name, {"producers": [], "consumers": [], "methods": []})
        methods = lane["methods"]
        if isinstance(methods, list) and method not in methods:
            methods.append(method)
        if method in ("Push", "Publish"):
            producers = lane["producers"]
            if isinstance(producers, list):
                producers.append(entry_text)
        else:
            consumers = lane["consumers"]
            if isinstance(consumers, list):
                consumers.append(entry_text)

    global_publish_sites = [
        f"{path_rel}:{line_number(text, match.start())}" for match in GLOBAL_PUBLISH_RE.finditer(text)
    ]

    entry["using_edges"] = [[src, dst, count] for (src, dst), count in edge_counts.items()]
    entry["signals"] = signals
    entry["signal_uses"] = signal_uses
    entry["global_publish_sites"] = global_publish_sites
    return entry


def analyze_source_file(path: Path, path_rel: str, first_party: bool) -> dict[str, object]:
    return analyze_source_bytes(path.read_bytes(), path_rel, first_party)


def load_asmdefs() -> list[dict[str, object]]:
    asmdefs: list[dict[str, object]] = []
    for path in iter_files(("Assets", "Packages"), "*.asmdef"):
        if not path.is_file():
            continue
        try:
            data = json.loads(path.read_text(encoding="utf-8-sig"))
        except Exception as exc:  # noqa: BLE001 - report parse failure in atlas.
            data = {"name": path.stem, "references": [], "parse_error": str(exc)}

        refs = data.get("references") or []
        normalized_refs = [ref.get("name", "") if isinstance(ref, dict) else str(ref) for ref in refs]
        asmdefs.append(
            {
                "name": str(data.get("name", path.stem)),
                "path": rel(path),
                "refs": normalized_refs,
            }
        )
    return sorted(asmdefs, key=lambda item: str(item["name"]))


def scan_source() -> dict[str, object]:
    using_edges: Counter[tuple[str, str]] = Counter()
    signals: dict[str, dict[str, object]] = {}
    signal_uses: dict[str, dict[str, object]] = defaultdict(
        lambda: {"producers": [], "consumers": [], "methods": set()}
    )
    global_publish_sites: list[str] = []

    source_files = iter_files(("Assets", "Packages"), "*.cs")
    cache = load_source_cache()
    cached_files = cache.get("files", {})
    if not isinstance(cached_files, dict):
        cached_files = {}
    changed_paths = git_changed_paths(("Assets", "Packages"))
    new_cache_files: dict[str, object] = {}
    existing_source_files = 0
    first_party_files = 0
    total_lines = 0
    first_party_lines = 0

    for path in source_files:
        path_rel = rel(path)
        first_party = path_rel.startswith("Assets/_Project/Scripts/")

        cached = cached_files.get(path_rel)
        cache_entry_valid = isinstance(cached, dict) and cached.get("first_party") == first_party
        try:
            stat = path.stat()
        except OSError:
            continue

        existing_source_files += 1
        if first_party:
            first_party_files += 1

        cache_metadata_valid = (
            cache_entry_valid
            and cached.get("size") == stat.st_size
            and cached.get("mtime_ns") == stat.st_mtime_ns
        )
        if cache_metadata_valid and (changed_paths is None or path_rel not in changed_paths):
            analysis = cached.get("analysis")
        else:
            try:
                analysis = analyze_source_file(path, path_rel, first_party)
            except OSError:
                continue

        new_cache_files[path_rel] = {
            "size": stat.st_size,
            "mtime_ns": stat.st_mtime_ns,
            "first_party": first_party,
            "analysis": analysis,
        }

        if not isinstance(analysis, dict):
            continue

        file_lines = int(analysis.get("line_count", 0))
        total_lines += file_lines
        if first_party:
            first_party_lines += file_lines

        if not first_party:
            continue

        for edge in analysis.get("using_edges", []):
            if not isinstance(edge, list) or len(edge) != 3:
                continue
            using_edges[(str(edge[0]), str(edge[1]))] += int(edge[2])

        for signal in analysis.get("signals", []):
            if not isinstance(signal, dict):
                continue
            name = str(signal.get("name", ""))
            if not name:
                continue
            signals.setdefault(
                name,
                {
                    "namespace": str(signal.get("namespace", "")),
                    "path": str(signal.get("path", path_rel)),
                    "line": int(signal.get("line", 0)),
                },
            )

        cached_signal_uses = analysis.get("signal_uses", {})
        if isinstance(cached_signal_uses, dict):
            for name, cached_lane in cached_signal_uses.items():
                if not isinstance(cached_lane, dict):
                    continue
                lane = signal_uses[str(name)]
                methods = lane["methods"]
                if isinstance(methods, set):
                    for method in cached_lane.get("methods", []):
                        methods.add(str(method))
                for entry in cached_lane.get("producers", []):
                    lane["producers"].append(str(entry))
                for entry in cached_lane.get("consumers", []):
                    lane["consumers"].append(str(entry))

        for entry in analysis.get("global_publish_sites", []):
            global_publish_sites.append(str(entry))

    write_source_cache(new_cache_files)

    all_signal_names = sorted(set(signals) | set(signal_uses))
    return {
        "source_file_count": existing_source_files,
        "first_party_file_count": first_party_files,
        "total_lines": total_lines,
        "first_party_lines": first_party_lines,
        "using_edges": using_edges,
        "signals": signals,
        "signal_uses": signal_uses,
        "all_signal_names": all_signal_names,
        "global_publish_sites": global_publish_sites,
    }

def parse_queue_lanes() -> list[list[str]]:
    path = ROOT / "Docs" / "ARCHITECTURE" / "SYSTEM_INTERCONNECT_MATRIX.md"
    lanes: list[list[str]] = []
    if not path.exists():
        return lanes

    for line in read_text(path).splitlines():
        if not line.startswith("| `"):
            continue
        cells = [cell.strip() for cell in line.strip("|").split("|")]
        if len(cells) >= 5 and cells[0] != "`Lane Owner`":
            lanes.append(cells[:6])
    return lanes


def load_vram() -> dict[str, object]:
    path = ROOT / "Docs" / "Reports" / "VRAM_Budget_Audit.json"
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8-sig"))


def scan_sherst() -> list[tuple[str, int, str]]:
    root = ROOT / "Docs" / "AgentLogs"
    if not root.exists():
        return []

    hits: list[tuple[str, int, str]] = []
    for path in sorted(root.glob("*")):
        if not path.is_file() or path.suffix.lower() not in (".md", ".txt"):
            continue
        try:
            lines = read_text(path).splitlines()
        except OSError:
            continue
        for index, line in enumerate(lines, start=1):
            if not SHERST_RE.search(line):
                continue
            text = line.strip()
            if len(text) > 180:
                text = text[:177] + "..."
            hits.append((rel(path), index, text))
    return hits


def scan_phi_logs() -> tuple[bool, list[str]]:
    root = ROOT / "Docs" / "AgentLogs"
    if not root.exists():
        return False, []

    exact_exists = (root / "Rationale_PHI_SYN.md").exists()
    near = []
    for path in sorted(root.glob("*")):
        name = path.name.lower()
        if path.is_file() and ("phi" in name or "syn" in name):
            near.append(rel(path))
    return exact_exists, near


def collect_atlas_data() -> dict[str, object]:
    return {
        "asmdefs": load_asmdefs(),
        "source": scan_source(),
        "queue_lanes": parse_queue_lanes(),
        "vram": load_vram(),
        "sherst": scan_sherst(),
        "phi": scan_phi_logs(),
    }


def append_source_authority(out: list[str]) -> None:
    out.append("## Source Of Authority")
    for path in (
        "AGENTS.md",
        "Docs/Tasks/CURRENT_BATCH.md",
        "Docs/Actual Domains of Project.txt",
        "Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md",
        "Docs/Reports/VRAM_Budget_Audit.json",
        "Docs/Reports/VRAM_Budget_Audit_Summary.md",
        "Docs/Reports/VRAM_Remediation_Plan.md",
        "Docs/AgentLogs/LOG_VRAM_ASSET_SCOUT.md",
        "Docs/AgentLogs/Rationale_VRAM_ASSET_SCOUT.md",
        "Docs/DEPENDENCY_GRAPH.json",
        "Docs/DEPENDENCY_GRAPH.cache.json",
        "Tools/BuildArchitectureAtlas.py",
        "Tools/AtlasCheck.py",
    ):
        if path in ("Docs/DEPENDENCY_GRAPH.json", "Docs/DEPENDENCY_GRAPH.cache.json") or (ROOT / path).exists():
            out.append(f"- `{path}`")
    out.append("")


def append_mandates(out: list[str]) -> None:
    out.append("## Loaded Mandates")
    for path in (
        ".agents-skills/ARCH_Execution_Phases.txt",
        ".agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt",
        ".agents-skills/ARCH_Signal_Lane_Segregation.txt",
        ".agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt",
        ".agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt",
        ".agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt",
        ".agents-skills/QA_Evidence_Text_Filter_Audit.txt",
        ".agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt",
    ):
        if (ROOT / path).exists():
            out.append(f"- `{path}`")
    out.append("")


def append_assembly_graph(out: list[str], asmdefs: list[dict[str, object]]) -> None:
    exact_core = [
        item for item in asmdefs if item["name"] != "Hecton8.Core" and "Hecton8.Core" in item["refs"]
    ]
    core_family = [
        item
        for item in asmdefs
        if item["name"] != "Hecton8.Core" and any(str(ref).startswith("Hecton8.Core") for ref in item["refs"])
    ]
    core = next((item for item in asmdefs if item["name"] == "Hecton8.Core"), None)
    contracts = next((item for item in asmdefs if item["name"] == "Hecton8.Core.Contracts"), None)

    out.append("## Assembly Dependency Graph")
    out.append("")
    if core is not None:
        out.append(f"Core assembly: `{core['path']}`")
        out.append("")
        out.append("`Hecton8.Core` direct references currently recorded in its asmdef:")
        for ref in core["refs"]:
            out.append(f"- `{ref}`")
        out.append("")
    if contracts is not None:
        refs = ", ".join(f"`{ref}`" for ref in contracts["refs"])
        out.append(f"Core contracts assembly: `{contracts['path']}` references {refs}.")
        out.append("")

    out.append(f"Assemblies directly depending on exact `Hecton8.Core`: {len(exact_core)}")
    out.append("")
    out.append("| Assembly | Path |")
    out.append("|---|---|")
    if exact_core:
        for item in exact_core:
            out.append(f"| `{item['name']}` | `{item['path']}` |")
    else:
        out.append("| None found | STATIC_SOURCE result |")
    out.append("")

    out.append(f"Assemblies depending on any `Hecton8.Core*` assembly: {len(core_family)}")
    out.append("")
    out.append("| Assembly | Core-family references | Path |")
    out.append("|---|---|---|")
    if core_family:
        for item in core_family:
            refs = ", ".join(f"`{ref}`" for ref in item["refs"] if str(ref).startswith("Hecton8.Core"))
            out.append(f"| `{item['name']}` | {refs} | `{item['path']}` |")
    else:
        out.append("| None found |  | STATIC_SOURCE result |")
    out.append("")


def append_signal_map(out: list[str], source: dict[str, object]) -> None:
    signals = source["signals"]
    signal_uses = source["signal_uses"]
    all_signal_names = source["all_signal_names"]
    global_publish_sites = source["global_publish_sites"]

    out.append("## SignalBus<T> Flow Map")
    out.append("")
    out.append(
        f"`ISignal` structs declared: {len(signals)}. "
        f"`SignalBus<T>` lanes observed in producer/consumer calls: {len(signal_uses)}. "
        f"Union listed below: {len(all_signal_names)} signals."
    )
    out.append(
        f"Legacy `GlobalSignals.Publish(...)` call sites found: {len(global_publish_sites)}. "
        "Many use local variables and are not type-resolved by this static pass; they remain a migration/integrator risk."
    )
    out.append("")
    out.append("| Signal | Declared at | Producers (`SignalBus<T>.Push/Publish`) | Consumers (`GetFrameSnapshot`) |")
    out.append("|---|---|---|---|")
    for name in all_signal_names:
        decl = signals.get(name)
        declared = f"`{decl['path']}:{decl['line']}`" if decl else "not found as local `ISignal` declaration"
        uses = signal_uses.get(name, {"producers": [], "consumers": []})
        producers_list = uses.get("producers", [])
        consumers_list = uses.get("consumers", [])
        producers = "<br>".join(f"`{entry}`" for entry in producers_list[:12]) or "none found"
        consumers = "<br>".join(f"`{entry}`" for entry in consumers_list[:12]) or "none found"
        if len(producers_list) > 12:
            producers += f"<br>... +{len(producers_list) - 12} more"
        if len(consumers_list) > 12:
            consumers += f"<br>... +{len(consumers_list) - 12} more"
        out.append(f"| `{name}` | {declared} | {producers} | {consumers} |")
    out.append("")


def append_queue_lanes(out: list[str], lanes: list[list[str]]) -> None:
    out.append("## Queue-Backed Signal Lanes")
    out.append("")
    out.append(f"Queue-backed lanes parsed from `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`: {len(lanes)}.")
    out.append("")
    out.append("| Lane Owner | Backing Queue | Listener Contract | Raise Surface | Flush Owner |")
    out.append("|---|---|---|---|---|")
    for cells in lanes:
        lane, backing, listener, raise_surface, flush = cells[:5]
        out.append(f"| {lane} | {backing} | {listener} | {raise_surface} | {flush} |")
    out.append("")


def append_vram(out: list[str], vram: dict[str, object]) -> None:
    out.append("## VRAM Map")
    out.append("")
    out.append(
        "Mandate target for MX350 from performance budget: total VRAM ceiling 1800 MiB; "
        "texture budget 900 MiB; render targets and depth 320 MiB; shadow maps 128 MiB; "
        "geometry buffers 200 MiB; compute/UAV 128 MiB; shader constant pools 64 MiB; "
        "post-process chain 96 MiB; driver reserve 164 MiB. Guard: used/total > 0.90 triggers mip downgrade."
    )
    out.append("")
    if not vram:
        out.append("No `Docs/Reports/VRAM_Budget_Audit.json` was found.")
        out.append("")
        return

    out.append("| Metric | Value | Evidence |")
    out.append("|---|---:|---|")
    for key, label in (
        ("texture_count", "Texture files scanned"),
        ("mesh_count", "Mesh files scanned"),
        ("bc7_full_mip_total_mib", "All scanned full-mip BC7 MiB"),
        ("bc7_full_mip_runtime_candidate_mib", "Runtime-candidate full-mip BC7 MiB"),
        ("bc7_full_mip_first_party_production_mib", "First-party production full-mip BC7 MiB"),
        ("mx350_texture_budget_mib", "MX350 texture budget MiB"),
        ("critical_texture_pool_mib", "Critical texture pool MiB"),
        ("texture_vram_crime_rows", "Texture VRAM crime rows"),
        ("mesh_redline_rows", "Mesh redline rows"),
        ("first_party_large_streaming_mips_off", "First-party large streaming mips off"),
        ("all_large_streaming_mips_off", "All large streaming mips off"),
        ("ci_expected_exit_code", "Expected VRAM CI exit code"),
    ):
        if key in vram:
            out.append(f"| {label} | `{vram[key]}` | `Docs/Reports/VRAM_Budget_Audit.json` |")
    out.append("")

    out.append("### Top Non-First-Party Runtime Payload Pressure")
    out.append("")
    out.append("| Directory | Count | Full-mip BC7 MiB | VRAM crime rows |")
    out.append("|---|---:|---:|---:|")
    for item in vram.get("top_non_first_party_runtime_directories", [])[:12]:
        out.append(
            f"| `{item['directory']}` | {item['count']} | "
            f"{item['bc7_full_mip_mib']} | {item['vram_crime_rows']} |"
        )
    out.append("")

    out.append("### Atlas Candidates")
    out.append("")
    out.append("| Group | Count | Combined BC7 MiB |")
    out.append("|---|---:|---:|")
    for item in vram.get("atlas_suggestions", []):
        out.append(f"| `{item['group']}` | {item['count']} | {item['combined_bc7_mib']} |")
    out.append("")

    if vram.get("mesh_redlines"):
        out.append("### Mesh Redlines")
        out.append("")
        out.append("| Path | Triangles | Flags |")
        out.append("|---|---:|---|")
        for item in vram["mesh_redlines"]:
            flags = ", ".join(item["flags"])
            out.append(f"| `{item['path']}` | {item['triangles']} | `{flags}` |")
        out.append("")


def append_sherst(out: list[str], hits: list[tuple[str, int, str]]) -> None:
    def sanitize_text_cell(text: str) -> str:
        safe = text.replace("|", "/")
        for prefix in ("Assets/", "Docs/", "Packages/", "ProjectSettings/", "Tools/", ".agents-skills/"):
            safe = safe.replace(prefix, prefix.replace("/", "&#47;"))
        return safe

    out.append("## SHERST Wall Of Shame")
    out.append("")
    out.append(
        "Pattern scan: active `Docs/AgentLogs/` only; terms: `TODO`, `HACK`, `FIX LATER`. "
        "These are text hits, not proof of executable debt."
    )
    out.append("")
    out.append("| File | Line | Text |")
    out.append("|---|---:|---|")
    if hits:
        for path, line, text in hits:
            out.append(f"| `{path}` | {line} | {sanitize_text_cell(text)} |")
    else:
        out.append("| none | 0 | no active matches |")
    out.append("")


def append_phi(out: list[str], source: dict[str, object], exact_exists: bool, near: list[str]) -> None:
    signals = source["signals"]
    signal_uses = source["signal_uses"]
    hphi_signals = (
        "PlayerActionProgressSignal",
        "PlayerActionCompletedSignal",
        "PlayerActionCancelledSignal",
        "PdaExchangeStateChangedSignal",
        "VehicleUpgradesChangedSignal",
        "PlayerStateSignal",
        "InventoryChangedSignal",
        "InputStateSignal",
        "SystemHealthSignal",
    )

    out.append("## PHI Self-Audit")
    out.append("")
    if exact_exists:
        out.append("Exact PHI_SYN rationale file is present in active logs.")
    else:
        out.append(
            "Exact PHI_SYN rationale file is absent from active logs. Near-match H-Phi rationale files "
            "were scanned as supporting evidence, but this is not treated as the exact requested artifact."
        )
    out.append("")
    out.append("Near-match active logs:")
    for path in near:
        out.append(f"- `{path}`")
    out.append("")

    out.append("| H-Phi / UX signal | Declared at | Producers | Consumers |")
    out.append("|---|---|---|---|")
    for name in hphi_signals:
        decl = signals.get(name)
        declared = f"`{decl['path']}:{decl['line']}`" if decl else "not found"
        uses = signal_uses.get(name, {"producers": [], "consumers": []})
        producers = "<br>".join(f"`{entry}`" for entry in uses.get("producers", [])[:8]) or "none found"
        consumers = "<br>".join(f"`{entry}`" for entry in uses.get("consumers", [])[:8]) or "none found"
        out.append(f"| `{name}` | {declared} | {producers} | {consumers} |")
    out.append("")


def build_markdown(data: dict[str, object] | None = None, generated_at: datetime | None = None) -> str:
    if data is None:
        data = collect_atlas_data()
    if generated_at is None:
        generated_at = datetime.now()

    asmdefs = data["asmdefs"]
    source = data["source"]
    lanes = data["queue_lanes"]
    vram = data["vram"]
    sherst = data["sherst"]
    phi_exact, phi_near = data["phi"]
    first_party_asmdefs = [item for item in asmdefs if str(item["path"]).startswith("Assets/_Project/")]

    out: list[str] = []
    out.append("# HECTON-8 Architecture Atlas - Dependency Graph")
    out.append("")
    out.append(f"Generated: {generated_at.strftime('%Y-%m-%d %H:%M:%S')}")
    out.append(f"Date: {generated_at.strftime('%Y-%m-%d')}")
    out.append("Status: ATLAS GENERATED STATIC SOURCE / ATLASCHECK SEPARATE GATE REQUIRED / RUNTIME PENDING")
    out.append(
        "Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PY_TOOL. "
        "No Unity Editor, Play Mode, Memory Profiler, Frame Debugger, or player build evidence is claimed here."
    )
    out.append("")
    out.append("<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->")
    out.append("## 2026-05-20 R47 Root/Architecture Actuality Boundary")
    out.append("")
    out.append("This document is active only where it agrees with:")
    out.append("")
    out.append("- `Docs/README.md`")
    out.append("- `Docs/DOC_GOVERNANCE.md`")
    out.append("- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`")
    out.append("- current source files")
    out.append("- fresh verification logs and artifacts")
    out.append("")
    out.append(
        "No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, "
        "Frame Debugger, player build, save/load route, or visual-route proof is implied unless "
        "this document links a fresh evidence artifact. Historical counters and older version claims "
        "inside this file are subordinate to the current authority spine above."
    )
    out.append("")
    out.append(
        "Current DOC_GLOBAL boundary (2026-05-20 R47): "
        "`Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` "
        "is the latest local static root/architecture authority-spine, runtime-wording, and counter-drift correction. "
        "R46 remains the prior interior-authority/route-field/proof-language correction; "
        "R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; "
        "R44 remains the prior internal-residue/exact-route-field/proof-wording correction; "
        "R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; AtlasCheck remains red and runtime proof is absent."
    )
    out.append("<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->")
    out.append("")

    append_source_authority(out)
    append_mandates(out)

    out.append("## Repository Scale")
    out.append("")
    out.append(f"- C# source files scanned under `Assets/` and `Packages/`: {source['source_file_count']}")
    out.append(f"- C# line count scanned under `Assets/` and `Packages/`: {source['total_lines']:,}")
    out.append(f"- First-party C# source files under `Assets/_Project/Scripts/`: {source['first_party_file_count']}")
    out.append(f"- First-party C# line count under `Assets/_Project/Scripts/`: {source['first_party_lines']:,}")
    out.append(f"- Assembly definitions scanned: {len(asmdefs)}")
    out.append(f"- First-party assembly definitions under `Assets/_Project/`: {len(first_party_asmdefs)}")
    docs_count = len([path for path in (ROOT / "Docs").rglob("*.md") if path.is_file() and not is_skipped(path)])
    out.append(f"- Markdown docs under `Docs/`: {docs_count}")
    out.append("")

    append_assembly_graph(out, asmdefs)

    out.append("### Domain Namespace Edges")
    out.append("")
    out.append(
        "Static `using Hecton8.*` edges from first-party source. "
        "This exposes compile-time namespace pressure, not runtime coupling proof."
    )
    out.append("")
    out.append("| From domain | To domain | Using count |")
    out.append("|---|---|---:|")
    using_edges = source["using_edges"]
    for (src, dst), count in using_edges.most_common(80):
        out.append(f"| `{src}` | `{dst}` | {count} |")
    out.append("")

    append_signal_map(out, source)
    append_queue_lanes(out, lanes)
    append_vram(out, vram)
    append_sherst(out, sherst)
    append_phi(out, source, phi_exact, phi_near)

    out.append("## Phi-Resonance Connectivity Model")
    out.append("")
    out.append(
        "The engine connectivity model is not mystical. It is a three-layer resonance model: "
        "contracts define stable frequency, typed signal lanes carry bounded pulses, and dispatcher phases "
        "control when state can move. `GlobalRegistry` is the cold authority spine for stable interfaces. "
        "`SignalBus<T>` and queue-backed event lanes are the nervous system for broadcast state. "
        "DataVault/native buffers are the mass: owned memory that simulation mutates and presentation samples."
    )
    out.append("")
    out.append(
        "Low/MX350 interpretation: coalesce lanes, consume bounded snapshots, keep optional visual consumers "
        "in `VISUAL_SYNC`, and shed mips/LOD before simulation truth. High/Ultra interpretation: do not "
        "increase gameplay broadcast cost; attach richer presentation consumers to the same snapshots and spend "
        "the saved budget on light, fog, HUD, audio, and material overkill."
    )
    out.append("")
    out.append(
        "Connectivity risk: `Hecton8.Core` currently references many domain assemblies directly. "
        "That may be intentional current architecture, but it is not a clean inward-only core. "
        "The integrator should treat this as a dependency inversion watchpoint, especially while many agents "
        "expand contracts in parallel."
    )
    out.append("")

    out.append("## Verification Commands")
    out.append("")
    out.append("- `python Tools/BuildArchitectureAtlas.py`")
    out.append("- `python Tools/AtlasCheck.py`")
    out.append("- `python -m py_compile Tools/BuildArchitectureAtlas.py Tools/AtlasCheck.py`")
    out.append("- C# compile verification is outside this atlas; run Unity import/Console and serial CLI builds as separate evidence.")
    out.append("- Current DOC_GLOBAL R47 blocker: `python Tools/AtlasCheck.py` still exits `1` with `ATLAS_CHECK_FAIL references=6781 missing=61`; missing refs include one Dynamic Decals vendor asset reference, RealtimeCSG vendor icon/readme image references, `Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs`, `Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs`, and `Assets/_Project/Scripts/Habitat/Deformation/Editor/HabitatDamageBakePipeline.cs` until the references are restored or the atlas check excludes that evidence class deliberately.")
    out.append("- This generated atlas is not `VERIFIED` unless `Tools/AtlasCheck.py` exits `0` after generation.")
    out.append("")

    out.append("## Residual Risk")
    out.append("")
    out.append(
        "- STATIC_SOURCE only: Unity import, runtime wiring, actual VRAM residency, profiler frame time, GC, "
        "build, and Play Mode remain PENDING VERIFICATION."
    )
    out.append(
        "- The signal producer map only resolves explicit `SignalBus<T>` calls. Legacy "
        "`GlobalSignals.Publish(...)` variable publishes require Roslyn-level dataflow to type-resolve fully."
    )
    out.append("- Active logs can change while this atlas is being written because the workspace is multi-agent.")
    out.append("")

    return "\n".join(out)


def build_json_payload(data: dict[str, object], generated_at: datetime | None = None) -> dict[str, object]:
    if generated_at is None:
        generated_at = datetime.now()
    asmdefs = data["asmdefs"]
    source = data["source"]
    lanes = data["queue_lanes"]
    vram = data["vram"]
    sherst = data["sherst"]
    phi_exact, phi_near = data["phi"]
    signals = source["signals"]
    signal_uses = source["signal_uses"]

    exact_core = [
        item for item in asmdefs if item["name"] != "Hecton8.Core" and "Hecton8.Core" in item["refs"]
    ]
    core_family = [
        item
        for item in asmdefs
        if item["name"] != "Hecton8.Core" and any(str(ref).startswith("Hecton8.Core") for ref in item["refs"])
    ]

    signal_payloads = []
    for name in source["all_signal_names"]:
        declaration = signals.get(name)
        uses = signal_uses.get(name, {"producers": [], "consumers": [], "methods": set()})
        signal_payloads.append(
            {
                "name": name,
                "declaration": declaration,
                "methods": sorted(uses.get("methods", [])),
                "producers": list(uses.get("producers", [])),
                "consumers": list(uses.get("consumers", [])),
            }
        )

    return {
        "schema_version": 1,
        "status": "ATLAS GENERATED STATIC SOURCE / ATLASCHECK SEPARATE GATE REQUIRED / RUNTIME PENDING",
        "evidence_class": "STATIC_SOURCE/STATIC_DOC/FILESYSTEM/PY_TOOL",
        "generated": generated_at.strftime("%Y-%m-%d %H:%M:%S"),
        "summary": {
            "source_file_count": source["source_file_count"],
            "source_line_count": source["total_lines"],
            "first_party_source_file_count": source["first_party_file_count"],
            "first_party_source_line_count": source["first_party_lines"],
            "asmdef_count": len(asmdefs),
            "first_party_asmdef_count": len(
                [item for item in asmdefs if str(item["path"]).startswith("Assets/_Project/")]
            ),
            "exact_core_dependent_count": len(exact_core),
            "core_family_dependent_count": len(core_family),
            "signal_count": len(source["all_signal_names"]),
            "queue_lane_count": len(lanes),
            "sherst_hit_count": len(sherst),
            "phi_exact_rationale_present": phi_exact,
        },
        "assemblies": {
            "exact_core_dependents": exact_core,
            "core_family_dependents": core_family,
            "all_asmdefs": asmdefs,
        },
        "signals": signal_payloads,
        "queue_lanes": [
            {
                "lane_owner": cells[0],
                "backing_queue": cells[1],
                "listener_contract": cells[2],
                "raise_surface": cells[3],
                "flush_owner": cells[4],
                "notes": cells[5] if len(cells) > 5 else "",
            }
            for cells in lanes
        ],
        "vram": vram,
        "sherst": [{"path": path, "line": line, "text": text} for path, line, text in sherst],
        "phi": {
            "exact_rationale_present": phi_exact,
            "near_match_logs": phi_near,
        },
        "artifacts": {
            "markdown": "Docs/DEPENDENCY_GRAPH.md",
            "json": "Docs/DEPENDENCY_GRAPH.json",
            "source_cache": "Docs/DEPENDENCY_GRAPH.cache.json",
            "generator": "Tools/BuildArchitectureAtlas.py",
            "validator": "Tools/AtlasCheck.py",
            "tests": "Tools/test_architecture_atlas.py",
            "atlas_check_status": "RED: Tools/AtlasCheck.py exits 1 with ATLAS_CHECK_FAIL references=6781 missing=61; missing refs include one Dynamic Decals vendor asset reference, RealtimeCSG vendor icon/readme image references, Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs, Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs, and Assets/_Project/Scripts/Habitat/Deformation/Editor/HabitatDamageBakePipeline.cs. Generated atlas is STATIC_SOURCE only until AtlasCheck exits 0.",
        },
        "residual_risk": [
            "Unity import, runtime wiring, actual VRAM residency, profiler frame time, GC, build, and Play Mode remain PENDING VERIFICATION.",
            "Legacy GlobalSignals.Publish(...) variable publishes require Roslyn-level dataflow to type-resolve fully.",
        ],
    }


def main() -> int:
    data = collect_atlas_data()
    generated_at = datetime.now()
    OUTPUT.write_text(build_markdown(data, generated_at), encoding="utf-8")
    JSON_OUTPUT.write_text(json.dumps(build_json_payload(data, generated_at), indent=2), encoding="utf-8")
    print(f"WROTE {rel(OUTPUT)}")
    print(f"WROTE {rel(JSON_OUTPUT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
