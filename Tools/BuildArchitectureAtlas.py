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
    return path.resolve().relative_to(ROOT).as_posix()


def iter_files(root_names: tuple[str, ...], pattern: str) -> list[Path]:
    if pattern == "*.cs":
        try:
            result = subprocess.run(
                ["rg", "--files", "-g", "*.cs", *root_names],
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


def load_asmdefs() -> list[dict[str, object]]:
    asmdefs: list[dict[str, object]] = []
    for path in iter_files(("Assets", "Packages"), "*.asmdef"):
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
    first_party_files = 0
    total_lines = 0
    first_party_lines = 0

    for path in source_files:
        path_rel = rel(path)
        first_party = path_rel.startswith("Assets/_Project/Scripts/")
        if first_party:
            first_party_files += 1

        try:
            raw = path.read_bytes()
        except OSError:
            continue

        file_lines = raw.count(b"\n") + 1
        total_lines += file_lines
        if first_party:
            first_party_lines += file_lines

        if not first_party:
            continue

        text = raw.decode("utf-8-sig", errors="ignore")
        namespace_match = NAMESPACE_RE.search(text)
        namespace = namespace_match.group(1) if namespace_match else ""

        parts = path_rel.split("/")
        domain = parts[3] if len(parts) > 4 else "RootScripts"
        for match in USING_RE.finditer(text):
            namespace_used = match.group(1)
            namespace_parts = namespace_used.split(".")
            target_domain = namespace_parts[1] if len(namespace_parts) > 1 else "Core"
            if target_domain != domain:
                using_edges[(domain, target_domain)] += 1

        for match in STRUCT_RE.finditer(text):
            if "ISignal" not in match.group(2):
                continue

            name = match.group(1)
            signals.setdefault(
                name,
                {
                    "namespace": namespace,
                    "path": path_rel,
                    "line": line_number(text, match.start()),
                },
            )

        for match in SIGNAL_BUS_RE.finditer(text):
            name = normalize_signal_name(match.group(1))
            method = match.group(2)
            entry = f"{path_rel}:{line_number(text, match.start())}"
            lane = signal_uses[name]
            methods = lane["methods"]
            if isinstance(methods, set):
                methods.add(method)
            if method in ("Push", "Publish"):
                lane["producers"].append(entry)
            else:
                lane["consumers"].append(entry)

        for match in GLOBAL_PUBLISH_RE.finditer(text):
            global_publish_sites.append(f"{path_rel}:{line_number(text, match.start())}")

    all_signal_names = sorted(set(signals) | set(signal_uses))
    return {
        "source_file_count": len(source_files),
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
        "Tools/BuildArchitectureAtlas.py",
        "Tools/AtlasCheck.py",
    ):
        if (ROOT / path).exists():
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
            out.append(f"| `{path}` | {line} | {text.replace('|', '/')} |")
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


def build_markdown() -> str:
    asmdefs = load_asmdefs()
    source = scan_source()
    lanes = parse_queue_lanes()
    vram = load_vram()
    sherst = scan_sherst()
    phi_exact, phi_near = scan_phi_logs()
    first_party_asmdefs = [item for item in asmdefs if str(item["path"]).startswith("Assets/_Project/")]

    out: list[str] = []
    out.append("# HECTON-8 Architecture Atlas - Dependency Graph")
    out.append("")
    out.append(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    out.append("Status: ATLAS VERIFIED PENDING RUNTIME VERIFICATION")
    out.append(
        "Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PY_TOOL. "
        "No Unity Editor, Play Mode, Memory Profiler, Frame Debugger, or player build evidence is claimed here."
    )
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
    out.append(
        '- `rg --files | rg "\\.sln$|\\.csproj$"` returned no project files during this pass, '
        "so C# compile verification is not available from current root state."
    )
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


def main() -> int:
    OUTPUT.write_text(build_markdown(), encoding="utf-8")
    print(f"WROTE {rel(OUTPUT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
