#!/usr/bin/env python3
"""Static asmdef dependency audit for HECTON-8 compile-wall pressure.

Evidence class: STATIC_SOURCE. This tool reads Unity asmdef JSON and reports
first-party dependency graph risks. It does not prove Unity import, generated
project correctness, or compile health.
"""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project" / "Scripts"
DEFAULT_REPORT_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "AssemblyDependencyAudit_HFI_AUDIT.md"
DEFAULT_JSON_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "AssemblyDependencyAudit_HFI_AUDIT.json"
SCHEMA = "hecton8.assembly_dependency_audit.v1"

SKIP_DIR_NAMES = {
    ".git",
    ".vs",
    "__pycache__",
    "bin",
    "obj",
    "Library",
    "Temp",
}


@dataclass(frozen=True)
class AssemblyDef:
    name: str
    path: Path
    references: tuple[str, ...]
    include_platforms: tuple[str, ...]
    exclude_platforms: tuple[str, ...]
    auto_referenced: bool
    no_engine_references: bool


def normalize_path(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.parts)


def iter_asmdefs(source_root: Path) -> list[Path]:
    return [
        path
        for path in sorted(source_root.rglob("*.asmdef"))
        if not should_skip(path.relative_to(source_root))
    ]


def read_guid(path: Path) -> str | None:
    meta_path = path.with_suffix(path.suffix + ".meta")
    if not meta_path.exists():
        return None
    for line in meta_path.read_text(encoding="utf-8", errors="ignore").splitlines():
        stripped = line.strip()
        if stripped.startswith("guid:"):
            return stripped.split(":", 1)[1].strip()
    return None


def load_raw_asmdefs(paths: list[Path]) -> tuple[dict[Path, dict[str, object]], dict[str, str]]:
    raw_by_path: dict[Path, dict[str, object]] = {}
    guid_to_name: dict[str, str] = {}
    for path in paths:
        try:
            raw = json.loads(path.read_text(encoding="utf-8", errors="ignore"))
        except json.JSONDecodeError as exc:
            raw_by_path[path] = {
                "name": path.stem,
                "references": [],
                "_parseError": str(exc),
            }
            continue
        if not isinstance(raw, dict):
            raw = {"name": path.stem, "references": [], "_parseError": "root is not object"}
        raw_by_path[path] = raw
        name = str(raw.get("name") or path.stem)
        guid = read_guid(path)
        if guid:
            guid_to_name[guid] = name
    return raw_by_path, guid_to_name


def resolve_reference(reference: object, guid_to_name: dict[str, str]) -> str:
    text = str(reference)
    if text.startswith("GUID:"):
        guid = text.split(":", 1)[1]
        return guid_to_name.get(guid, text)
    return text


def tuple_of_strings(value: object) -> tuple[str, ...]:
    if not isinstance(value, list):
        return ()
    return tuple(str(item) for item in value)


def load_asmdefs(source_root: Path) -> list[AssemblyDef]:
    paths = iter_asmdefs(source_root)
    raw_by_path, guid_to_name = load_raw_asmdefs(paths)
    assemblies: list[AssemblyDef] = []
    for path in paths:
        raw = raw_by_path[path]
        refs = tuple(resolve_reference(item, guid_to_name) for item in tuple_of_strings(raw.get("references")))
        assemblies.append(
            AssemblyDef(
                name=str(raw.get("name") or path.stem),
                path=path,
                references=refs,
                include_platforms=tuple_of_strings(raw.get("includePlatforms")),
                exclude_platforms=tuple_of_strings(raw.get("excludePlatforms")),
                auto_referenced=bool(raw.get("autoReferenced", True)),
                no_engine_references=bool(raw.get("noEngineReferences", False)),
            )
        )
    return assemblies


def is_first_party(name: str) -> bool:
    return name.startswith("Hecton8.")


def is_unity_reference(name: str) -> bool:
    return name.startswith("Unity.") or name.startswith("UnityEngine") or name in {"GPUInstancer"}


def is_contract_reference(name: str) -> bool:
    return name.endswith(".Contracts") or ".Contracts." in name or name == "Hecton8.Core.Contracts"


def is_editor_assembly(assembly: AssemblyDef) -> bool:
    return assembly.name.endswith(".Editor") or assembly.include_platforms == ("Editor",)


def domain_key(name: str) -> str:
    parts = name.split(".")
    if len(parts) < 2:
        return name
    if parts[0] != "Hecton8":
        return name
    if len(parts) >= 3 and parts[1] == "Core":
        return "Hecton8.Core"
    return ".".join(parts[:2])


def allowed_core_reference(reference: str) -> bool:
    if not is_first_party(reference):
        return True
    if reference.startswith("Hecton8.Core"):
        return True
    if is_contract_reference(reference):
        return True
    return False


def classify_edges(assemblies: list[AssemblyDef]) -> tuple[list[dict[str, str]], list[dict[str, str]]]:
    core_concrete: list[dict[str, str]] = []
    runtime_concrete: list[dict[str, str]] = []
    by_name = {assembly.name: assembly for assembly in assemblies}
    for assembly in assemblies:
        if not is_first_party(assembly.name) or is_editor_assembly(assembly):
            continue
        source_domain = domain_key(assembly.name)
        for reference in assembly.references:
            if not is_first_party(reference):
                continue
            target_domain = domain_key(reference)
            if assembly.name == "Hecton8.Core" and not allowed_core_reference(reference):
                core_concrete.append(
                    {
                        "assembly": assembly.name,
                        "reference": reference,
                        "path": normalize_path(assembly.path),
                    }
                )
            if (
                target_domain != source_domain
                and not is_contract_reference(reference)
                and reference in by_name
                and not is_editor_assembly(by_name[reference])
            ):
                runtime_concrete.append(
                    {
                        "assembly": assembly.name,
                        "reference": reference,
                        "path": normalize_path(assembly.path),
                    }
                )
    return core_concrete, runtime_concrete


def build_graph(assemblies: list[AssemblyDef]) -> dict[str, list[str]]:
    names = {assembly.name for assembly in assemblies}
    graph: dict[str, list[str]] = {}
    for assembly in assemblies:
        if not is_first_party(assembly.name):
            continue
        graph[assembly.name] = sorted(ref for ref in assembly.references if ref in names and is_first_party(ref))
    return graph


def detect_cycles(graph: dict[str, list[str]]) -> list[list[str]]:
    visited: set[str] = set()
    stack: set[str] = set()
    path: list[str] = []
    cycles: list[list[str]] = []
    seen: set[tuple[str, ...]] = set()

    def canonical(cycle: list[str]) -> tuple[str, ...]:
        body = cycle[:-1]
        rotations = [tuple(body[index:] + body[:index]) for index in range(len(body))]
        return min(rotations)

    def visit(node: str) -> None:
        visited.add(node)
        stack.add(node)
        path.append(node)
        for target in graph.get(node, []):
            if target not in visited:
                visit(target)
            elif target in stack:
                start = path.index(target)
                cycle = path[start:] + [target]
                key = canonical(cycle)
                if key not in seen:
                    seen.add(key)
                    cycles.append(cycle)
        stack.remove(node)
        path.pop()

    for node in sorted(graph):
        if node not in visited:
            visit(node)
    return cycles


def summarize_assemblies(assemblies: list[AssemblyDef]) -> dict[str, object]:
    first_party = [assembly for assembly in assemblies if is_first_party(assembly.name)]
    editor = [assembly for assembly in first_party if is_editor_assembly(assembly)]
    runtime = [assembly for assembly in first_party if not is_editor_assembly(assembly)]
    no_engine = [assembly for assembly in first_party if assembly.no_engine_references]
    auto_false = [assembly for assembly in first_party if not assembly.auto_referenced]
    reference_counts = sorted(
        (
            {
                "assembly": assembly.name,
                "path": normalize_path(assembly.path),
                "references": len(assembly.references),
                "firstPartyReferences": sum(1 for ref in assembly.references if is_first_party(ref)),
            }
            for assembly in first_party
        ),
        key=lambda item: (-int(item["references"]), str(item["assembly"])),
    )
    return {
        "assemblyCount": len(assemblies),
        "firstPartyAssemblyCount": len(first_party),
        "runtimeFirstPartyAssemblyCount": len(runtime),
        "editorFirstPartyAssemblyCount": len(editor),
        "noEngineReferencesCount": len(no_engine),
        "autoReferencedFalseCount": len(auto_false),
        "topReferenceCounts": reference_counts[:20],
    }


def build_payload(source_root: Path) -> dict[str, object]:
    assemblies = load_asmdefs(source_root)
    core_concrete, runtime_concrete = classify_edges(assemblies)
    graph = build_graph(assemblies)
    cycles = detect_cycles(graph)
    core = next((assembly for assembly in assemblies if assembly.name == "Hecton8.Core"), None)
    return {
        "schema": SCHEMA,
        "sourceRoot": normalize_path(source_root),
        "summary": summarize_assemblies(assemblies),
        "core": {
            "present": core is not None,
            "path": normalize_path(core.path) if core else "",
            "referenceCount": len(core.references) if core else 0,
            "firstPartyReferenceCount": sum(1 for ref in core.references if is_first_party(ref)) if core else 0,
            "concreteSiblingReferenceCount": len(core_concrete),
            "concreteSiblingReferences": core_concrete,
        },
        "runtimeConcreteSiblingReferenceCount": len(runtime_concrete),
        "runtimeConcreteSiblingReferences": runtime_concrete[:200],
        "cycleCount": len(cycles),
        "cycles": cycles[:50],
    }


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_markdown(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    summary = payload["summary"]
    core = payload["core"]
    if not isinstance(summary, dict) or not isinstance(core, dict):
        raise TypeError("payload malformed")
    lines = [
        "# Assembly Dependency Audit",
        "",
        "Evidence class: STATIC_SOURCE. No Unity import, compile, player build, or runtime proof was executed.",
        "",
        f"- Schema: `{payload['schema']}`",
        f"- Source root: `{payload['sourceRoot']}`",
        f"- Asmdefs: `{summary['assemblyCount']}`",
        f"- First-party asmdefs: `{summary['firstPartyAssemblyCount']}`",
        f"- Runtime first-party asmdefs: `{summary['runtimeFirstPartyAssemblyCount']}`",
        f"- Editor first-party asmdefs: `{summary['editorFirstPartyAssemblyCount']}`",
        f"- First-party `noEngineReferences=true`: `{summary['noEngineReferencesCount']}`",
        f"- First-party `autoReferenced=false`: `{summary['autoReferencedFalseCount']}`",
        "",
        "## Core Compile-Wall Pressure",
        "",
        f"- Core present: `{core['present']}`",
        f"- Core references: `{core['referenceCount']}`",
        f"- Core first-party references: `{core['firstPartyReferenceCount']}`",
        f"- Core concrete sibling references: `{core['concreteSiblingReferenceCount']}`",
        "",
    ]
    concrete = core.get("concreteSiblingReferences") or []
    if concrete:
        lines.extend(["| Reference | Source asmdef |", "|---|---|"])
        for item in concrete:
            if isinstance(item, dict):
                lines.append(f"| `{item.get('reference')}` | `{item.get('path')}` |")
        lines.append("")

    lines.extend(
        [
            "## Runtime Concrete Cross-Domain References",
            "",
            f"- Count: `{payload['runtimeConcreteSiblingReferenceCount']}`",
            "",
        ]
    )
    runtime_refs = payload.get("runtimeConcreteSiblingReferences") or []
    if runtime_refs:
        lines.extend(["| Assembly | Reference | Path |", "|---|---|---|"])
        for item in runtime_refs[:50]:
            if isinstance(item, dict):
                lines.append(
                    f"| `{item.get('assembly')}` | `{item.get('reference')}` | `{item.get('path')}` |"
                )
        lines.append("")

    lines.extend(["## Cycles", "", f"- First-party asmdef cycles: `{payload['cycleCount']}`", ""])
    cycles = payload.get("cycles") or []
    for cycle in cycles[:20]:
        if isinstance(cycle, list):
            lines.append("- `" + " -> ".join(str(part) for part in cycle) + "`")
    if cycles:
        lines.append("")

    lines.extend(
        [
            "## Interpretation",
            "",
            "- Core concrete sibling references are compile-wall pressure. They are not automatically removable; each one needs a contract/facade migration plan plus Unity import proof.",
            "- Runtime concrete cross-domain references are review surfaces. Same-domain and `.Contracts` references are not counted in this bucket.",
            "- Cycles in first-party asmdefs are hard architectural defects if Unity import confirms them. This tool only reports the serialized asmdef graph.",
            "- This audit does not mutate asmdefs and does not claim compile health.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def hard_failures(payload: dict[str, object], args: argparse.Namespace) -> list[str]:
    failures: list[str] = []
    core = payload["core"]
    if not isinstance(core, dict):
        raise TypeError("core payload malformed")
    core_concrete = int(core["concreteSiblingReferenceCount"])
    cycles = int(payload["cycleCount"])
    runtime_concrete = int(payload["runtimeConcreteSiblingReferenceCount"])
    if args.fail_on_core_concrete_sibling_refs and core_concrete > args.max_core_concrete_sibling_refs:
        failures.append(
            "Core concrete sibling asmdef refs "
            f"{core_concrete} > {args.max_core_concrete_sibling_refs}"
        )
    if args.fail_on_runtime_concrete_sibling_refs and runtime_concrete > args.max_runtime_concrete_sibling_refs:
        failures.append(
            "Runtime concrete cross-domain asmdef refs "
            f"{runtime_concrete} > {args.max_runtime_concrete_sibling_refs}"
        )
    if args.fail_on_cycles and cycles > 0:
        failures.append(f"First-party asmdef cycles: {cycles}")
    return failures


def print_text(payload: dict[str, object], failures: list[str]) -> None:
    summary = payload["summary"]
    core = payload["core"]
    print("Assembly dependency audit")
    print(f"schema={payload['schema']}")
    print(f"sourceRoot={payload['sourceRoot']}")
    if isinstance(summary, dict):
        print(f"asmdefs={summary['assemblyCount']}")
        print(f"firstPartyAsmdefs={summary['firstPartyAssemblyCount']}")
        print(f"runtimeFirstPartyAsmdefs={summary['runtimeFirstPartyAssemblyCount']}")
        print(f"editorFirstPartyAsmdefs={summary['editorFirstPartyAssemblyCount']}")
        print(f"noEngineReferences={summary['noEngineReferencesCount']}")
        print(f"autoReferencedFalse={summary['autoReferencedFalseCount']}")
    if isinstance(core, dict):
        print(f"coreReferences={core['referenceCount']}")
        print(f"coreFirstPartyReferences={core['firstPartyReferenceCount']}")
        print(f"coreConcreteSiblingReferences={core['concreteSiblingReferenceCount']}")
        concrete = core.get("concreteSiblingReferences") or []
        if concrete:
            print(
                "coreConcreteSiblingList="
                + "; ".join(str(item.get("reference")) for item in concrete if isinstance(item, dict))
            )
    print(f"runtimeConcreteSiblingReferences={payload['runtimeConcreteSiblingReferenceCount']}")
    print(f"cycles={payload['cycleCount']}")
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
    parser.add_argument("--json", action="store_true", help="Print JSON payload to stdout.")
    parser.add_argument("--fail-on-core-concrete-sibling-refs", action="store_true")
    parser.add_argument("--max-core-concrete-sibling-refs", type=int, default=0)
    parser.add_argument("--fail-on-runtime-concrete-sibling-refs", action="store_true")
    parser.add_argument("--max-runtime-concrete-sibling-refs", type=int, default=0)
    parser.add_argument("--fail-on-cycles", action="store_true")
    return parser


def main() -> int:
    return run(build_parser().parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
