#!/usr/bin/env python3
"""X_003 static compile-wall archaeology for HECTON-8.

Evidence class: STATIC_SOURCE. This tool does not prove Unity import, C# compile,
runtime wiring, GC, profiler cost, or player-build behavior.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter, defaultdict, deque
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


REPO_ROOT = Path(__file__).resolve().parent.parent
SOURCE_ROOT = REPO_ROOT / "Assets" / "_Project"
SCHEMA = "hecton8.compile_wall_x003.static.v1"
CONTRACT_ROUTE = "Hecton8.Core.Contracts"

SKIP_DIRS = {".git", ".vs", "Library", "Temp", "Obj", "obj", "bin", "__pycache__"}
HOT_METHODS = {
    "Update",
    "LateUpdate",
    "FixedUpdate",
    "Tick",
    "FixedTick",
    "SlowTick",
    "FastTick",
    "LateFrameTick",
    "PostFixedTick",
    "PreSimulationTick",
    "SimulationTick",
    "VisualSyncTick",
    "OnUpdate",
}
HOT_LOOKUP_PATTERNS = (
    ("GlobalRegistry", re.compile(r"\bGlobalRegistry\.")),
    ("GetComponent", re.compile(r"\b(?:Try)?GetComponent(?:InChildren|InParent)?\s*<")),
    ("FindObject", re.compile(r"\b(?:FindObjectOfType|FindFirstObjectByType|FindAnyObjectByType|Resources\.FindObjectsOfTypeAll)\s*<")),
    ("GameObjectFind", re.compile(r"\bGameObject\.(?:Find|FindWithTag)\s*\(")),
    ("CameraMain", re.compile(r"\bCamera\.main\b")),
)
REGISTRY_MUTATION_PATTERN = re.compile(r"\bGlobalRegistry\.(?:Try)?(?:Register|Unregister)[A-Za-z0-9_]*\s*\(")
TYPE_PATTERN = re.compile(
    r"\bpublic\s+"
    r"(?:(?:readonly|partial|sealed|static|unsafe|ref|abstract|record)\s+)*"
    r"(struct|class|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)"
)
METHOD_PATTERN = re.compile(
    r"^\s*(?:public|private|protected|internal|static|sealed|override|virtual|unsafe|new|partial|extern|\s)+"
    r"[\w<>,\[\]\.?]+\s+(?:(?:[A-Za-z_][A-Za-z0-9_]*)\.)?"
    r"(?P<name>Update|LateUpdate|FixedUpdate|Tick|FixedTick|SlowTick|FastTick|LateFrameTick|PostFixedTick|PreSimulationTick|SimulationTick|VisualSyncTick|OnUpdate)\s*\("
)
USING_PATTERN = re.compile(r"^\s*using\s+(Hecton8\.[A-Za-z0-9_.]+)\s*;", re.MULTILINE)
NAMESPACE_PATTERN = re.compile(r"^\s*namespace\s+(Hecton8\.[A-Za-z0-9_.]+)\b", re.MULTILINE)
STRING_LITERAL_PATTERN = re.compile(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')
FULLY_QUALIFIED_NAMESPACE_PATTERN = re.compile(r"\b(?:global::)?(Hecton8\.[A-Za-z0-9_.]+)\b")
AS_CAST_PATTERN = re.compile(r"\bas\s+([A-Z][A-Za-z0-9_.]*)")
IS_CAST_PATTERN = re.compile(r"\bis\s+([A-Z][A-Za-z0-9_.]*)")
GET_COMPONENT_PATTERN = re.compile(r"\b(?:Try)?GetComponent(?:InChildren|InParent)?\s*<\s*([A-Z][A-Za-z0-9_.]*)\s*>")
EXPLICIT_CAST_PATTERN = re.compile(r"(?<!\w)\(\s*([A-Z][A-Za-z0-9_.]*(?:\s*<[^()\n]+>)?)\s*\)\s*(?=[A-Za-z_(])")
CONCRETE_IGNORE_TYPES = {
    "Application",
    "Array",
    "BoxCollider",
    "Bounds",
    "CapsuleCollider",
    "CharacterController",
    "Collider",
    "Color",
    "Debug",
    "ForceMode",
    "GameObject",
    "Guid",
    "Hash128",
    "Math",
    "Mathf",
    "Matrix4x4",
    "MeshCollider",
    "NativeArray",
    "Object",
    "ParticleSystemRenderer",
    "ProfilerMarker",
    "Quaternion",
    "Rect",
    "ScriptableObject",
    "SphereCollider",
    "Span",
    "StringComparison",
    "Time",
    "TimeSpan",
    "Transform",
    "Vector2",
    "Vector3",
    "Vector4",
}
VALUE_CAST_SUFFIXES = (
    "Flag",
    "Flags",
    "Kind",
    "Mask",
    "Mode",
    "Role",
    "State",
    "States",
    "Status",
    "Type",
)


@dataclass(frozen=True)
class Assembly:
    name: str
    path: Path
    references: tuple[str, ...]
    auto_referenced: bool
    include_platforms: tuple[str, ...]
    optional_unity_references: tuple[str, ...]
    define_constraints: tuple[str, ...]


@dataclass(frozen=True)
class SourceFile:
    path: Path
    assembly: str
    domain: str
    editor: bool
    text: str


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(REPO_ROOT.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return ""


def iter_files(root: Path, suffix: str) -> list[Path]:
    files: list[Path] = []
    for path in root.rglob(f"*{suffix}"):
        if any(part in SKIP_DIRS for part in path.parts):
            continue
        if path.is_file():
            files.append(path)
    return sorted(files)


def read_guid(path: Path) -> str | None:
    meta = path.with_suffix(path.suffix + ".meta")
    if not meta.exists():
        return None
    for line in read_text(meta).splitlines():
        stripped = line.strip()
        if stripped.startswith("guid:"):
            return stripped.split(":", 1)[1].strip()
    return None


def load_assemblies(root: Path) -> list[Assembly]:
    asmdef_paths = iter_files(root, ".asmdef")
    raw_by_path: dict[Path, dict[str, object]] = {}
    guid_to_name: dict[str, str] = {}
    for path in asmdef_paths:
        try:
            raw = json.loads(read_text(path))
        except json.JSONDecodeError:
            raw = {"name": path.stem, "references": []}
        if not isinstance(raw, dict):
            raw = {"name": path.stem, "references": []}
        raw_by_path[path] = raw
        guid = read_guid(path)
        if guid:
            guid_to_name[guid] = str(raw.get("name") or path.stem)

    assemblies: list[Assembly] = []
    for path in asmdef_paths:
        raw = raw_by_path[path]
        refs: list[str] = []
        for item in raw.get("references") if isinstance(raw.get("references"), list) else []:
            text = str(item)
            if text.startswith("GUID:"):
                text = guid_to_name.get(text.split(":", 1)[1], text)
            refs.append(text)
        include_raw = raw.get("includePlatforms")
        optional_raw = raw.get("optionalUnityReferences")
        defines_raw = raw.get("defineConstraints")
        include = tuple(str(item) for item in include_raw) if isinstance(include_raw, list) else ()
        optional = tuple(str(item) for item in optional_raw) if isinstance(optional_raw, list) else ()
        defines = tuple(str(item) for item in defines_raw) if isinstance(defines_raw, list) else ()
        assemblies.append(
            Assembly(
                name=str(raw.get("name") or path.stem),
                path=path,
                references=tuple(refs),
                auto_referenced=bool(raw.get("autoReferenced", True)),
                include_platforms=include,
                optional_unity_references=optional,
                define_constraints=defines,
            )
        )
    return assemblies


def domain_key(name: str) -> str:
    if not name.startswith("Hecton8."):
        return name
    parts = name.split(".")
    if len(parts) >= 3 and parts[1] == "Core":
        return "Hecton8.Core"
    return ".".join(parts[:2])


def namespace_domain(namespace: str) -> str:
    if not namespace.startswith("Hecton8."):
        return namespace
    parts = namespace.split(".")
    if len(parts) >= 3 and parts[1] == "Core":
        return "Hecton8.Core"
    if len(parts) >= 2:
        return ".".join(parts[:2])
    return namespace


def source_path_domain(path: Path, fallback_assembly: str) -> str:
    parts = list(path.parts)
    try:
        scripts_index = parts.index("Scripts")
    except ValueError:
        return domain_key(fallback_assembly)

    next_index = scripts_index + 1
    if next_index >= len(parts):
        return domain_key(fallback_assembly)

    root = parts[next_index]
    if root.endswith(".cs") or root.endswith(".asmdef"):
        return domain_key(fallback_assembly)
    if root == "Core":
        return "Hecton8.Core"
    return f"Hecton8.{root}"


def declared_namespace_domain(text: str, path_domain: str) -> str:
    namespaces = [match.group(1) for match in NAMESPACE_PATTERN.finditer(text)]
    if not namespaces:
        return path_domain

    domains = [namespace_domain(namespace) for namespace in namespaces]
    if path_domain in domains:
        return path_domain

    counts = Counter(domains)
    if len(counts) == 1:
        return domains[0]

    top_domain, top_count = counts.most_common(1)[0]
    if top_count > 1:
        return top_domain

    return path_domain


def is_first_party(name: str) -> bool:
    return name.startswith("Hecton8.")


def is_contract(name: str) -> bool:
    return name == CONTRACT_ROUTE or name.endswith(".Contracts") or ".Contracts." in name


def is_editor_assembly(assembly: Assembly | str, include_platforms: Iterable[str] = ()) -> bool:
    if isinstance(assembly, Assembly):
        return (
            assembly.name.endswith(".Editor")
            or assembly.include_platforms == ("Editor",)
            or "TestAssemblies" in assembly.optional_unity_references
            or "UNITY_INCLUDE_TESTS" in assembly.define_constraints
        )
    return assembly.endswith(".Editor") or tuple(include_platforms) == ("Editor",)


def build_scope(assemblies: list[Assembly]) -> list[tuple[Path, Assembly]]:
    return sorted(((assembly.path.parent.resolve(), assembly) for assembly in assemblies), key=lambda item: len(item[0].parts), reverse=True)


def owning_assembly(path: Path, scope: list[tuple[Path, Assembly]]) -> Assembly | None:
    resolved = path.resolve()
    for root, assembly in scope:
        try:
            resolved.relative_to(root)
            return assembly
        except ValueError:
            continue
    return None


def load_sources(root: Path, assemblies: list[Assembly]) -> list[SourceFile]:
    scope = build_scope(assemblies)
    result: list[SourceFile] = []
    for path in iter_files(root, ".cs"):
        assembly = owning_assembly(path, scope)
        if assembly is None:
            continue
        text = read_text(path)
        path_domain = source_path_domain(path, assembly.name)
        result.append(
            SourceFile(
                path=path,
                assembly=assembly.name,
                domain=declared_namespace_domain(text, path_domain),
                editor=is_editor_assembly(assembly) or "Editor" in path.parts,
                text=text,
            )
        )
    return result


def graph_metrics(assemblies: list[Assembly]) -> dict[str, object]:
    names = {assembly.name for assembly in assemblies}
    edges: list[dict[str, str]] = []
    inbound: dict[str, set[str]] = defaultdict(set)
    outbound: dict[str, set[str]] = defaultdict(set)
    unresolved: list[dict[str, str]] = []
    sibling_edges: list[dict[str, str]] = []
    auto_ref_true: list[dict[str, object]] = []

    for assembly in assemblies:
        if is_first_party(assembly.name) and assembly.auto_referenced:
            auto_ref_true.append({"assembly": assembly.name, "path": rel(assembly.path), "editor": is_editor_assembly(assembly)})
        for reference in assembly.references:
            if reference not in names:
                if is_first_party(reference) or reference.startswith("GUID:"):
                    unresolved.append({"assembly": assembly.name, "reference": reference, "path": rel(assembly.path)})
                continue
            edges.append({"from": assembly.name, "to": reference})
            outbound[assembly.name].add(reference)
            inbound[reference].add(assembly.name)
            if (
                is_first_party(assembly.name)
                and is_first_party(reference)
                and not is_editor_assembly(assembly)
                and not is_editor_assembly(reference)
                and domain_key(assembly.name) != domain_key(reference)
                and not is_contract(reference)
            ):
                sibling_edges.append(
                    {
                        "assembly": assembly.name,
                        "reference": reference,
                        "path": rel(assembly.path),
                        "sourceDomain": domain_key(assembly.name),
                        "targetDomain": domain_key(reference),
                    }
                )

    def reverse_closure(start: str) -> set[str]:
        seen: set[str] = set()
        queue: deque[str] = deque(sorted(inbound.get(start, ())))
        while queue:
            item = queue.popleft()
            if item in seen:
                continue
            seen.add(item)
            queue.extend(sorted(inbound.get(item, ())))
        return seen

    blast = []
    for assembly in assemblies:
        if not is_first_party(assembly.name):
            continue
        affected = reverse_closure(assembly.name)
        blast.append(
            {
                "assembly": assembly.name,
                "path": rel(assembly.path),
                "directInbound": len(inbound.get(assembly.name, ())),
                "blastRadiusAssemblies": len(affected) + 1,
                "dependentAssemblies": sorted(affected),
                "outboundReferences": len(outbound.get(assembly.name, ())),
                "firstPartyOutboundReferences": sum(1 for r in outbound.get(assembly.name, ()) if is_first_party(r)),
            }
        )
    blast.sort(key=lambda item: (int(item["blastRadiusAssemblies"]), int(item["directInbound"])), reverse=True)

    core = next((item for item in blast if item["assembly"] == "Hecton8.Core"), None)
    return {
        "assemblyCount": len(assemblies),
        "runtimeFirstPartyAssemblyCount": sum(1 for a in assemblies if is_first_party(a.name) and not is_editor_assembly(a)),
        "edgeCount": len(edges),
        "edges": edges,
        "inboundGravityWells": blast[:25],
        "allBlastRadii": blast,
        "coreBlastRadius": core,
        "runtimeConcreteSiblingReferences": sibling_edges,
        "runtimeConcreteSiblingReferenceCount": len(sibling_edges),
        "autoReferencedTrue": auto_ref_true,
        "autoReferencedTrueCount": len(auto_ref_true),
        "unresolvedFirstPartyReferences": unresolved,
        "unresolvedFirstPartyReferenceCount": len(unresolved),
    }


def detect_cycles(edges: list[dict[str, str]]) -> list[list[str]]:
    graph: dict[str, set[str]] = defaultdict(set)
    for edge in edges:
        graph[edge["from"]].add(edge["to"])
    visited: set[str] = set()
    stack: set[str] = set()
    path: list[str] = []
    cycles: list[list[str]] = []

    def dfs(node: str) -> None:
        visited.add(node)
        stack.add(node)
        path.append(node)
        for nxt in sorted(graph.get(node, ())):
            if nxt not in visited:
                dfs(nxt)
            elif nxt in stack:
                try:
                    index = path.index(nxt)
                except ValueError:
                    index = 0
                cycle = path[index:] + [nxt]
                if cycle not in cycles:
                    cycles.append(cycle)
        stack.remove(node)
        path.pop()

    for node in sorted(graph):
        if node not in visited:
            dfs(node)
    return cycles


def find_public_types(sources: list[SourceFile]) -> list[dict[str, object]]:
    occurrences_by_name: dict[str, list[SourceFile]] = defaultdict(list)
    for source in sources:
        for match in re.finditer(r"\b[A-Za-z_][A-Za-z0-9_]*\b", source.text):
            occurrences_by_name[match.group(0)].append(source)

    candidates: list[dict[str, object]] = []
    for source in sources:
        if source.editor or is_contract(source.assembly):
            continue
        for line_no, line in enumerate(source.text.splitlines(), 1):
            match = TYPE_PATTERN.search(line)
            if not match:
                continue
            kind, name = match.groups()
            if not looks_like_contract_candidate(kind, name):
                continue
            external_files: set[str] = set()
            external_assemblies: set[str] = set()
            external_domains: set[str] = set()
            for other in occurrences_by_name.get(name, ()):
                if other.path == source.path:
                    continue
                if other.assembly == source.assembly:
                    continue
                external_files.add(rel(other.path))
                external_assemblies.add(other.assembly)
                external_domains.add(other.domain)
            if not external_assemblies:
                continue
            candidates.append(
                {
                    "name": name,
                    "kind": kind,
                    "path": rel(source.path),
                    "line": line_no,
                    "assembly": source.assembly,
                    "domain": source.domain,
                    "externalAssemblyCount": len(external_assemblies),
                    "externalAssemblies": sorted(external_assemblies)[:20],
                    "externalDomainCount": len(external_domains),
                    "externalDomains": sorted(external_domains),
                    "externalFileCount": len(external_files),
                    "recommendation": "MOVE_TO_CONTRACTS_OR_CREATE_LEGACY_WRAPPER",
                }
            )
    candidates.sort(
        key=lambda item: (
            int(item["externalDomainCount"]),
            int(item["externalAssemblyCount"]),
            int(item["externalFileCount"]),
        ),
        reverse=True,
    )
    return candidates


def looks_like_contract_candidate(kind: str, name: str) -> bool:
    if kind == "interface":
        return True
    if kind not in {"struct", "enum"}:
        return False
    markers = (
        "DTO",
        "Signal",
        "Config",
        "Tuning",
        "State",
        "Snapshot",
        "Packet",
        "Request",
        "Result",
        "Profile",
        "Telemetry",
        "Contract",
        "Handle",
        "Buffer",
        "Sample",
        "Entry",
    )
    return any(marker in name for marker in markers)


def hot_path_lookup_scan(sources: list[SourceFile]) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    findings: list[dict[str, object]] = []
    mutations: list[dict[str, object]] = []
    for source in sources:
        if source.editor:
            continue
        lines = source.text.splitlines()
        index = 0
        while index < len(lines):
            match = METHOD_PATTERN.search(lines[index])
            if not match:
                index += 1
                continue
            method = match.group("name")
            start = index
            brace_depth = 0
            body_started = False
            end = index
            j = index
            while j < len(lines):
                raw = lines[j]
                brace_depth += raw.count("{")
                if raw.count("{"):
                    body_started = True
                if body_started:
                    for kind, pattern in HOT_LOOKUP_PATTERNS:
                        if pattern.search(raw):
                            item = {
                                "kind": kind,
                                "method": method,
                                "path": rel(source.path),
                                "line": j + 1,
                                "assembly": source.assembly,
                                "domain": source.domain,
                                "text": raw.strip()[:220],
                            }
                            if kind == "GlobalRegistry" and REGISTRY_MUTATION_PATTERN.search(raw):
                                item["recommendation"] = "VERIFY_RARE_SELF_REGISTRATION_ROUTE_OR_DEFER_OUT_OF_HOT_PHASE"
                                mutations.append(item)
                            else:
                                item["recommendation"] = "CACHE_IN_BOOTSTRAP_OR_DEPENDENCY_INJECTION"
                                findings.append(item)
                brace_depth -= raw.count("}")
                end = j
                if body_started and brace_depth <= 0:
                    break
                if j - start > 3000:
                    break
                j += 1
            index = max(end + 1, index + 1)
    findings.sort(key=lambda item: (item["assembly"], item["path"], int(item["line"])))
    mutations.sort(key=lambda item: (item["assembly"], item["path"], int(item["line"])))
    return findings, mutations


def concrete_cast_scan(sources: list[SourceFile]) -> list[dict[str, object]]:
    findings: list[dict[str, object]] = []
    for source in sources:
        if source.editor:
            continue
        for line_no, line in enumerate(source.text.splitlines(), 1):
            stripped = line.strip()
            if not stripped or stripped.startswith("//"):
                continue
            scan_line = STRING_LITERAL_PATTERN.sub('""', line)
            matches: list[tuple[str, str]] = []
            matches.extend(("as", match.group(1).split(".")[-1]) for match in AS_CAST_PATTERN.finditer(scan_line))
            matches.extend(("is", match.group(1).split(".")[-1]) for match in IS_CAST_PATTERN.finditer(scan_line))
            matches.extend(("GetComponent", match.group(1).split(".")[-1]) for match in GET_COMPONENT_PATTERN.finditer(scan_line))
            matches.extend(("explicit", match.group(1).split(".")[-1].split("<", 1)[0].strip()) for match in EXPLICIT_CAST_PATTERN.finditer(scan_line))
            for kind, type_name in matches:
                if type_name.startswith("I") and len(type_name) > 1 and type_name[1].isupper():
                    continue
                if type_name in CONCRETE_IGNORE_TYPES:
                    continue
                if kind == "explicit" and type_name.endswith(VALUE_CAST_SUFFIXES):
                    continue
                findings.append(
                    {
                        "path": rel(source.path),
                        "line": line_no,
                        "assembly": source.assembly,
                        "domain": source.domain,
                        "kind": kind,
                        "type": type_name,
                        "directPlayerCoupling": "Player" in type_name or type_name == "InputManager",
                        "text": stripped[:220],
                    }
                )
    return findings


def domain_filtered(items: list[dict[str, object]], domains: set[str]) -> list[dict[str, object]]:
    return [item for item in items if str(item.get("domain")) in domains]


def source_using_domain_scan(sources: list[SourceFile]) -> dict[str, object]:
    edge_counts: Counter[tuple[str, str]] = Counter()
    findings: list[dict[str, object]] = []
    critical_findings: list[dict[str, object]] = []
    for source in sources:
        if source.editor:
            continue
        for match in USING_PATTERN.finditer(source.text):
            namespace = match.group(1)
            target_domain = namespace_domain(namespace)
            if target_domain == source.domain:
                continue
            edge_counts[(source.domain, target_domain)] += 1
            line_no = source.text.count("\n", 0, match.start()) + 1
            row = {
                "path": rel(source.path),
                "line": line_no,
                "assembly": source.assembly,
                "sourceDomain": source.domain,
                "targetDomain": target_domain,
                "namespace": namespace,
            }
            findings.append(row)
            if (
                (source.domain == "Hecton8.AI" and target_domain == "Hecton8.Physics")
                or (source.domain == "Hecton8.Physics" and target_domain == "Hecton8.AI")
                or (source.domain in {"Hecton8.AI", "Hecton8.Physics", "Hecton8.Physiology"} and target_domain in {"Hecton8.UI", "Hecton8.Audio"})
            ):
                critical_findings.append(row)
    return {
        "edgeCount": len(edge_counts),
        "usingCount": sum(edge_counts.values()),
        "topEdges": [
            {"sourceDomain": source, "targetDomain": target, "count": count}
            for (source, target), count in edge_counts.most_common(80)
        ],
        "criticalFindingCount": len(critical_findings),
        "criticalFindings": critical_findings[:200],
        "topFindings": findings[:300],
    }


def source_reference_domain_scan(sources: list[SourceFile]) -> dict[str, object]:
    edge_counts: Counter[tuple[str, str]] = Counter()
    findings: list[dict[str, object]] = []
    critical_findings: list[dict[str, object]] = []
    for source in sources:
        if source.editor:
            continue
        for line_no, line in enumerate(source.text.splitlines(), 1):
            stripped = line.strip()
            if not stripped or stripped.startswith("//") or stripped.startswith("using ") or stripped.startswith("namespace "):
                continue
            scan_line = STRING_LITERAL_PATTERN.sub('""', line)
            seen_on_line: set[tuple[str, str]] = set()
            for match in FULLY_QUALIFIED_NAMESPACE_PATTERN.finditer(scan_line):
                namespace = match.group(1)
                target_domain = namespace_domain(namespace)
                if target_domain == source.domain:
                    continue
                key = (namespace, target_domain)
                if key in seen_on_line:
                    continue
                seen_on_line.add(key)
                edge_counts[(source.domain, target_domain)] += 1
                row = {
                    "path": rel(source.path),
                    "line": line_no,
                    "assembly": source.assembly,
                    "sourceDomain": source.domain,
                    "targetDomain": target_domain,
                    "namespace": namespace,
                    "text": stripped[:220],
                }
                findings.append(row)
                if (
                    (source.domain == "Hecton8.AI" and target_domain == "Hecton8.Physics")
                    or (source.domain == "Hecton8.Physics" and target_domain == "Hecton8.AI")
                    or (source.domain in {"Hecton8.AI", "Hecton8.Physics", "Hecton8.Physiology"} and target_domain in {"Hecton8.UI", "Hecton8.Audio"})
                ):
                    critical_findings.append(row)
    return {
        "edgeCount": len(edge_counts),
        "referenceCount": sum(edge_counts.values()),
        "topEdges": [
            {"sourceDomain": source, "targetDomain": target, "count": count}
            for (source, target), count in edge_counts.most_common(80)
        ],
        "criticalFindingCount": len(critical_findings),
        "criticalFindings": critical_findings[:200],
        "topFindings": findings[:300],
    }


def selected_file_blast_radius(sources: list[SourceFile], graph: dict[str, object]) -> list[dict[str, object]]:
    selected_names = {
        "CablePhysicsSolver132.cs",
        "HectonPlayerMovement.cs",
        "CombatDamageRuntime.cs",
        "HarpoonTensionSolver328.cs",
        "HectonPlayerHealth.cs",
        "HectonPlayerState.cs",
        "HectonSubmarineOS.cs",
        "SeaglideHydrodynamicsRuntime.cs",
        "ShinobuApexBrainVault.cs",
        "ShinobuMetabolismRuntime.cs",
        "UtilityAICognitionVault.cs",
    }
    blast_by_assembly = {item["assembly"]: item for item in graph["allBlastRadii"]}
    rows: list[dict[str, object]] = []
    for source in sources:
        if source.path.name not in selected_names:
            continue
        blast = blast_by_assembly.get(source.assembly)
        if blast is None:
            continue
        rows.append(
            {
                "file": rel(source.path),
                "assembly": source.assembly,
                "blastRadiusAssembliesBefore": blast["blastRadiusAssemblies"],
                "directInboundBefore": blast["directInbound"],
                "reachesAudio": any(str(item).startswith("Hecton8.Audio") for item in blast.get("dependentAssemblies", [])),
                "reachesUI": any(str(item).startswith("Hecton8.UI") for item in blast.get("dependentAssemblies", [])),
                "evidence": "STATIC_ASMDEF_REVERSE_CLOSURE",
            }
        )
    rows.sort(key=lambda item: item["file"])
    return rows


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_markdown(path: Path, payload: dict[str, object]) -> None:
    graph = payload["graph"]
    dto = payload["dtoAndInterfaceCensus"]
    hot = payload["hotPathLookupDetection"]
    mutations = payload["hotPathRegistryMutationDetection"]
    cast = payload["concreteCastDetection"]
    blast = payload["compileWallBlastRadius"]
    using_audit = payload["sourceUsingDomainAudit"]
    reference_audit = payload["sourceReferenceDomainAudit"]
    lines = [
        "# Compile Wall X_003 Static Archaeology",
        "",
        "Evidence class: STATIC_SOURCE. No Unity import, C# compile, runtime wiring, GC, profiler, or player build proof.",
        "",
        "## Assembly Graph",
        "",
        f"- Asmdefs: `{graph['assemblyCount']}`",
        f"- Runtime first-party asmdefs: `{graph['runtimeFirstPartyAssemblyCount']}`",
        f"- Edges: `{graph['edgeCount']}`",
        f"- Runtime concrete sibling refs: `{graph['runtimeConcreteSiblingReferenceCount']}`",
        f"- `autoReferenced=true` first-party asmdefs: `{graph['autoReferencedTrueCount']}`",
        f"- Unresolved first-party refs: `{graph['unresolvedFirstPartyReferenceCount']}`",
        f"- Cycles: `{payload['cycleCount']}`",
        "",
        "## Gravity Wells",
        "",
        "| Assembly | Blast Radius | Direct Inbound | Outbound | First-Party Outbound |",
        "|---|---:|---:|---:|---:|",
    ]
    for item in graph["inboundGravityWells"][:12]:
        lines.append(
            f"| `{item['assembly']}` | {item['blastRadiusAssemblies']} | {item['directInbound']} | {item['outboundReferences']} | {item['firstPartyOutboundReferences']} |"
        )
    lines.extend(["", "## DTO / Interface Extraction Candidates", "", "| Type | Kind | Assembly | External Assemblies | External Domains | Path |", "|---|---|---|---:|---:|---|"])
    for item in dto["topCandidates"][:20]:
        lines.append(
            f"| `{item['name']}` | `{item['kind']}` | `{item['assembly']}` | {item['externalAssemblyCount']} | {item['externalDomainCount']} | `{item['path']}:{item['line']}` |"
        )
    lines.extend(["", "## Hot-Path Lookup Findings", "", f"- Polling/search findings: `{hot['findingCount']}`", f"- Registry mutation findings: `{mutations['findingCount']}`", "", "| Kind | Method | Assembly | Path |", "|---|---|---|---|"])
    for item in hot["topFindings"][:40]:
        lines.append(f"| `{item['kind']}` | `{item['method']}` | `{item['assembly']}` | `{item['path']}:{item['line']}` |")
    if mutations["topFindings"]:
        lines.extend(["", "### Registry Mutation Notes", "", "| Kind | Method | Assembly | Path |", "|---|---|---|---|"])
        for item in mutations["topFindings"][:20]:
            lines.append(f"| `{item['kind']}` | `{item['method']}` | `{item['assembly']}` | `{item['path']}:{item['line']}` |")
    lines.extend(
        [
            "",
            "## Concrete Cast Findings",
            "",
            f"- Findings: `{cast['findingCount']}`",
            f"- Direct player concrete coupling findings: `{cast['directPlayerCouplingCount']}`",
            f"- AI/Physics/Physiology concrete cast findings: `{cast['aiPhysicsPhysiologyConcreteCastCount']}`",
            f"- AI/Physics/Physiology direct player concrete coupling findings: `{cast['aiPhysicsPhysiologyDirectPlayerCouplingCount']}`",
            "",
            "| Domain | Count |",
            "|---|---:|",
        ]
    )
    for item in cast["byDomain"][:12]:
        lines.append(f"| `{item['domain']}` | {item['count']} |")
    if cast["topFindings"]:
        lines.extend(["", "| Kind | Type | Assembly | Path |", "|---|---|---|---|"])
        for item in cast["topFindings"][:60]:
            lines.append(f"| `{item['kind']}` | `{item['type']}` | `{item['assembly']}` | `{item['path']}:{item['line']}` |")
    if cast.get("directPlayerFindings"):
        lines.extend(["", "### Direct Player Concrete Findings", "", "| Domain | Kind | Type | Path |", "|---|---|---|---|"])
        for item in cast["directPlayerFindings"][:80]:
            lines.append(f"| `{item['domain']}` | `{item['kind']}` | `{item['type']}` | `{item['path']}:{item['line']}` |")
    if cast["aiPhysicsPhysiologyConcreteFindings"]:
        lines.extend(["", "### AI/Physics/Physiology Concrete Cast Findings", "", "| Domain | Kind | Type | Path |", "|---|---|---|---|"])
        for item in cast["aiPhysicsPhysiologyConcreteFindings"][:80]:
            lines.append(f"| `{item['domain']}` | `{item['kind']}` | `{item['type']}` | `{item['path']}:{item['line']}` |")
    lines.extend(
        [
            "",
            "## Source Using Domain Audit",
            "",
            f"- Cross-domain using edges: `{using_audit['edgeCount']}`",
            f"- Cross-domain using directives: `{using_audit['usingCount']}`",
            f"- Critical AI/Physics/UI/Audio findings: `{using_audit['criticalFindingCount']}`",
            "",
            "| Source Domain | Target Domain | Count |",
            "|---|---|---:|",
        ]
    )
    for item in using_audit["topEdges"][:30]:
        lines.append(f"| `{item['sourceDomain']}` | `{item['targetDomain']}` | {item['count']} |")
    if using_audit["criticalFindings"]:
        lines.extend(["", "### Critical Using Findings", "", "| Assembly | Namespace | Path |", "|---|---|---|"])
        for item in using_audit["criticalFindings"][:40]:
            lines.append(f"| `{item['assembly']}` | `{item['namespace']}` | `{item['path']}:{item['line']}` |")
    lines.extend(
        [
            "",
            "## Source Fully-Qualified Reference Audit",
            "",
            f"- Cross-domain reference edges: `{reference_audit['edgeCount']}`",
            f"- Cross-domain references: `{reference_audit['referenceCount']}`",
            f"- Critical AI/Physics/UI/Audio findings: `{reference_audit['criticalFindingCount']}`",
            "",
            "| Source Domain | Target Domain | Count |",
            "|---|---|---:|",
        ]
    )
    for item in reference_audit["topEdges"][:30]:
        lines.append(f"| `{item['sourceDomain']}` | `{item['targetDomain']}` | {item['count']} |")
    if reference_audit["criticalFindings"]:
        lines.extend(["", "### Critical Fully-Qualified Reference Findings", "", "| Assembly | Namespace | Path |", "|---|---|---|"])
        for item in reference_audit["criticalFindings"][:40]:
            lines.append(f"| `{item['assembly']}` | `{item['namespace']}` | `{item['path']}:{item['line']}` |")
    lines.extend(["", "## Selected Blast Radius Baseline", "", "| File | Assembly | Before Radius | Direct Inbound | Reaches UI | Reaches Audio |", "|---|---|---:|---:|---|---|"])
    for item in blast["selectedFiles"]:
        lines.append(f"| `{item['file']}` | `{item['assembly']}` | {item['blastRadiusAssembliesBefore']} | {item['directInboundBefore']} | `{item['reachesUI']}` | `{item['reachesAudio']}` |")
    lines.append("")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def build_payload() -> dict[str, object]:
    assemblies = load_assemblies(SOURCE_ROOT)
    sources = load_sources(SOURCE_ROOT, assemblies)
    graph = graph_metrics(assemblies)
    cycles = detect_cycles(graph["edges"])
    dto_candidates = find_public_types(sources)
    hot_findings, registry_mutations = hot_path_lookup_scan(sources)
    cast_findings = concrete_cast_scan(sources)
    ai_physics_physiology_casts = domain_filtered(cast_findings, {"Hecton8.AI", "Hecton8.Physics", "Hecton8.Physiology"})
    direct_player_findings = [item for item in cast_findings if item.get("directPlayerCoupling")]
    using_domain_audit = source_using_domain_scan(sources)
    reference_domain_audit = source_reference_domain_scan(sources)
    blast = selected_file_blast_radius(sources, graph)
    return {
        "schema": SCHEMA,
        "agent": "X_003",
        "evidenceClass": "STATIC_SOURCE",
        "sourceRoot": rel(SOURCE_ROOT),
        "requiredRoute": CONTRACT_ROUTE,
        "graph": graph,
        "cycleCount": len(cycles),
        "cycles": cycles,
        "dtoAndInterfaceCensus": {
            "candidateCount": len(dto_candidates),
            "topCandidates": dto_candidates[:200],
            "residualRisk": "Regex type census; nested/generated/partial type semantics require compile/API review before moves.",
        },
        "hotPathLookupDetection": {
            "findingCount": len(hot_findings),
            "byAssembly": [{"assembly": k, "count": v} for k, v in Counter(item["assembly"] for item in hot_findings).most_common(50)],
            "topFindings": hot_findings[:300],
            "residualRisk": "Method-scope static scan; each hit must be classified as cold registration, cached access, or true hot poll before edit.",
        },
        "hotPathRegistryMutationDetection": {
            "findingCount": len(registry_mutations),
            "topFindings": registry_mutations[:100],
            "residualRisk": "Self-registration/unregistration from a hot method is not polling, but it still mutates registry state during a dispatcher phase and needs owner review.",
        },
        "concreteCastDetection": {
            "findingCount": len(cast_findings),
            "byDomain": [{"domain": k, "count": v} for k, v in Counter(item["domain"] for item in cast_findings).most_common(30)],
            "directPlayerCouplingCount": len(direct_player_findings),
            "aiPhysicsPhysiologyConcreteCastCount": len(ai_physics_physiology_casts),
            "aiPhysicsPhysiologyDirectPlayerCouplingCount": sum(
                1
                for item in cast_findings
                if item.get("directPlayerCoupling") and item.get("domain") in {"Hecton8.AI", "Hecton8.Physics", "Hecton8.Physiology"}
            ),
            "directPlayerFindings": direct_player_findings[:200],
            "aiPhysicsPhysiologyConcreteFindings": ai_physics_physiology_casts[:200],
            "topFindings": cast_findings[:300],
            "residualRisk": "Static pattern scan of runtime code; interface casts are excluded, concrete/service casts and GetComponent concrete lookups require owner review.",
        },
        "sourceUsingDomainAudit": using_domain_audit,
        "sourceReferenceDomainAudit": reference_domain_audit,
        "compileWallBlastRadius": {
            "selectedFiles": blast,
            "metric": "reverse asmdef transitive closure; includes owning assembly",
            "after": "PENDING_AFTER_EDITS",
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json-path", default=str(REPO_ROOT / "Docs" / "AgentLogs" / "CompileWall_X_003_Archaeology.json"))
    parser.add_argument("--report-path", default=str(REPO_ROOT / "Docs" / "AgentLogs" / "CompileWall_X_003_Archaeology.md"))
    parser.add_argument("--fail-on-cycles", action="store_true")
    parser.add_argument("--fail-on-runtime-sibling-refs", action="store_true")
    args = parser.parse_args()

    payload = build_payload()
    write_json(Path(args.json_path), payload)
    write_markdown(Path(args.report_path), payload)

    graph = payload["graph"]
    print("Compile wall X_003 archaeology")
    print(f"schema={payload['schema']}")
    print(f"asmdefs={graph['assemblyCount']}")
    print(f"runtimeConcreteSiblingReferences={graph['runtimeConcreteSiblingReferenceCount']}")
    print(f"cycles={payload['cycleCount']}")
    print(f"dtoCandidates={payload['dtoAndInterfaceCensus']['candidateCount']}")
    print(f"hotPathLookupFindings={payload['hotPathLookupDetection']['findingCount']}")
    print(f"hotPathRegistryMutationFindings={payload['hotPathRegistryMutationDetection']['findingCount']}")
    print(f"concreteCastFindings={payload['concreteCastDetection']['findingCount']}")
    print(f"criticalSourceUsingFindings={payload['sourceUsingDomainAudit']['criticalFindingCount']}")
    print(f"criticalFullyQualifiedReferenceFindings={payload['sourceReferenceDomainAudit']['criticalFindingCount']}")
    print(f"json={args.json_path}")
    print(f"report={args.report_path}")

    if args.fail_on_cycles and int(payload["cycleCount"]) > 0:
        return 1
    if args.fail_on_runtime_sibling_refs and int(graph["runtimeConcreteSiblingReferenceCount"]) > 0:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
