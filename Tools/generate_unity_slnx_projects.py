#!/usr/bin/env python3
"""Generate missing Unity-style csproj files referenced by Hecton8.slnx.

This is an offline recovery tool for stale Unity IDE project generation. It
uses .asmdef files as source-of-truth and writes deterministic SDK projects
only for projects already referenced by the solution.
"""


from __future__ import annotations
from H8VerifyCore import fromstring_xml_safe

import hashlib
import json
import re
import sys
import defusedxml.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Any


UNITY_EDITOR = r"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Managed"
DEFAULT_DEFINES = (
    "TRACE;"
    "UNITY_6000;"
    "UNITY_6000_4_OR_NEWER;"
    "UNITY_6000_4_1;"
    "UNITY_STANDALONE;"
    "UNITY_STANDALONE_WIN;"
    "ENABLE_UNITY_COLLECTIONS_CHECKS;"
    "UNITY_ADDRESSABLES_EXIST"
)
EDITOR_DEFINES = DEFAULT_DEFINES + ";UNITY_EDITOR;UNITY_EDITOR_WIN"
REPORT_PATH = Path("Docs/Reports/UNITY_SLNX_CSPROJ_RESTORE_1330_RERUN23.json")


@dataclass(frozen=True)
class AsmDef:
    name: str
    safe_name: str
    path: Path
    directory: Path
    references: tuple[str, ...]
    include_platforms: tuple[str, ...]
    exclude_platforms: tuple[str, ...]
    guid: str


def safe_project_name(name: str) -> str:
    return re.sub(r"[^A-Za-z0-9_.-]", "", name)


def rel(path: Path) -> str:
    return str(path).replace("/", "\\")


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_guid(path: Path) -> str:
    meta = path.with_suffix(path.suffix + ".meta")
    if not meta.exists():
        return ""
    for line in meta.read_text(encoding="utf-8", errors="ignore").splitlines():
        stripped = line.strip()
        if stripped.startswith("guid:"):
            return stripped.split(":", 1)[1].strip()
    return ""


def load_asmdefs(root: Path) -> list[AsmDef]:
    result: list[AsmDef] = []
    for path in root.rglob("*.asmdef"):
        relative = path.relative_to(root)
        if relative.parts and relative.parts[0] == ".codexbuild":
            continue
        try:
            data = read_json(path)
        except (OSError, json.JSONDecodeError):
            continue
        name = str(data.get("name", "")).strip()
        if not name:
            continue
        result.append(
            AsmDef(
                name=name,
                safe_name=safe_project_name(name),
                path=relative,
                directory=relative.parent,
                references=tuple(str(x) for x in data.get("references", []) or []),
                include_platforms=tuple(str(x) for x in data.get("includePlatforms", []) or []),
                exclude_platforms=tuple(str(x) for x in data.get("excludePlatforms", []) or []),
                guid=read_guid(path),
            )
        )
    return result


def project_paths_from_slnx(path: Path) -> list[str]:
    xml = fromstring_xml_safe(path.read_text(encoding="utf-8-sig"))
    return [node.attrib["Path"] for node in xml.findall("Project") if "Path" in node.attrib]


def asmdef_rank(asmdef: AsmDef) -> tuple[int, int, str]:
    parts = asmdef.path.parts
    if parts[0] == "Packages":
        source_rank = 0
    elif parts[0] == "Assets":
        source_rank = 1
    elif len(parts) > 1 and parts[0] == "Library" and parts[1] == "PackageCache":
        source_rank = 2
    else:
        source_rank = 3
    return (source_rank, len(parts), rel(asmdef.path))


def choose_asmdef(project_name: str, asmdefs: list[AsmDef]) -> AsmDef | None:
    matches = [a for a in asmdefs if a.name == project_name or a.safe_name == project_name]
    if not matches:
        return None
    return sorted(matches, key=asmdef_rank)[0]


def nested_asmdef_dirs(owner: AsmDef, asmdefs: list[AsmDef]) -> list[Path]:
    nested: list[Path] = []
    owner_parts = owner.directory.parts
    for candidate in asmdefs:
        if candidate.path == owner.path:
            continue
        parts = candidate.directory.parts
        if len(parts) > len(owner_parts) and parts[: len(owner_parts)] == owner_parts:
            nested.append(candidate.directory)
    return sorted(set(nested), key=lambda p: rel(p))


def is_editor_project(project_name: str, asmdef: AsmDef | None) -> bool:
    if project_name.endswith(".Editor") or ".Editor." in project_name or project_name.endswith("Editor"):
        return True
    if project_name.startswith("Assembly-CSharp-Editor"):
        return True
    if asmdef is None:
        return False
    if "Editor" in asmdef.include_platforms:
        return True
    return any(part == "Editor" for part in asmdef.directory.parts)


def compile_items_for_default(project_name: str, asmdefs: list[AsmDef]) -> list[str]:
    items: list[str] = []
    if project_name == "Assembly-CSharp":
        items.append('    <Compile Include="Assets\\**\\*.cs" />')
        items.append('    <Compile Remove="Assets\\**\\Editor\\**\\*.cs" />')
        items.append('    <Compile Remove="Assets\\Plugins\\**\\*.cs" />')
        items.append('    <Compile Remove="Assets\\Standard Assets\\**\\*.cs" />')
        items.append('    <Compile Remove="Assets\\Pro Standard Assets\\**\\*.cs" />')
    elif project_name == "Assembly-CSharp-firstpass":
        items.append('    <Compile Include="Assets\\Plugins\\**\\*.cs" />')
        items.append('    <Compile Include="Assets\\Standard Assets\\**\\*.cs" />')
        items.append('    <Compile Include="Assets\\Pro Standard Assets\\**\\*.cs" />')
        items.append('    <Compile Remove="Assets\\**\\Editor\\**\\*.cs" />')
    elif project_name == "Assembly-CSharp-Editor":
        items.append('    <Compile Include="Assets\\**\\Editor\\**\\*.cs" />')
        items.append('    <Compile Remove="Assets\\Plugins\\**\\Editor\\**\\*.cs" />')
        items.append('    <Compile Remove="Assets\\Standard Assets\\**\\Editor\\**\\*.cs" />')
        items.append('    <Compile Remove="Assets\\Pro Standard Assets\\**\\Editor\\**\\*.cs" />')
    elif project_name == "Assembly-CSharp-Editor-firstpass":
        items.append('    <Compile Include="Assets\\Plugins\\**\\Editor\\**\\*.cs" />')
        items.append('    <Compile Include="Assets\\Standard Assets\\**\\Editor\\**\\*.cs" />')
        items.append('    <Compile Include="Assets\\Pro Standard Assets\\**\\Editor\\**\\*.cs" />')
    for asmdef in sorted(asmdefs, key=lambda a: rel(a.directory)):
        if asmdef.directory.parts and asmdef.directory.parts[0] == "Assets":
            items.append(f'    <Compile Remove="{rel(asmdef.directory)}\\**\\*.cs" />')
    return items


def compile_items_for_asmdef(owner: AsmDef, asmdefs: list[AsmDef]) -> list[str]:
    items = [f'    <Compile Include="{rel(owner.directory)}\\**\\*.cs" />']
    if not is_editor_project(owner.name, owner):
        items.append(f'    <Compile Remove="{rel(owner.directory)}\\**\\Editor\\**\\*.cs" />')
    for directory in nested_asmdef_dirs(owner, asmdefs):
        items.append(f'    <Compile Remove="{rel(directory)}\\**\\*.cs" />')
    return items


def resolve_project_references(owner: AsmDef | None, by_guid: dict[str, AsmDef], by_name: dict[str, AsmDef], slnx_names: set[str]) -> list[str]:
    if owner is None:
        return []
    refs: list[str] = []
    for raw in owner.references:
        target_name = raw
        if raw.startswith("GUID:"):
            target = by_guid.get(raw[5:])
            if target is None:
                continue
            target_name = target.safe_name
        else:
            target = by_name.get(raw) or by_name.get(safe_project_name(raw))
            if target is not None:
                target_name = target.safe_name
        if target_name in slnx_names and target_name != owner.safe_name:
            refs.append(target_name)
    return sorted(set(refs))


def script_assemblies_exclude(project_name: str, asmdef: AsmDef | None, slnx_names: set[str]) -> str:
    names = [project_name] if asmdef is None else sorted(slnx_names)
    return ";".join(f"Library\\ScriptAssemblies\\{name}.dll" for name in names)


def project_xml(project_name: str, asmdef: AsmDef | None, asmdefs: list[AsmDef], refs: list[str], slnx_names: set[str]) -> str:
    editor = is_editor_project(project_name, asmdef)
    defines = EDITOR_DEFINES if editor else DEFAULT_DEFINES
    compile_items = compile_items_for_default(project_name, asmdefs) if asmdef is None else compile_items_for_asmdef(asmdef, asmdefs)
    ref_lines = [
        '    <Reference Include="$(UnityEditorManagedDir)\\*.dll" Private="false" />',
        '    <Reference Include="$(UnityEngineManagedDir)\\*.dll" Private="false" />',
        f'    <Reference Include="Library\\ScriptAssemblies\\*.dll" Exclude="{script_assemblies_exclude(project_name, asmdef, slnx_names)}" Private="false" />',
        '    <Reference Include="Library\\PackageCache\\**\\*.dll" Private="false" />',
        '    <Reference Include="Assets\\Plugins\\**\\*.dll" Private="false" />',
    ]
    project_ref_lines = [f'    <ProjectReference Include="{name}.csproj" Private="false" />' for name in refs]
    lines = [
        '<Project Sdk="Microsoft.NET.Sdk">',
        '  <PropertyGroup>',
        '    <TargetFramework>netstandard2.1</TargetFramework>',
        f'    <AssemblyName>{project_name}</AssemblyName>',
        f'    <RootNamespace>{project_name}</RootNamespace>',
        '    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>',
        '    <LangVersion>latest</LangVersion>',
        '    <Nullable>disable</Nullable>',
        '    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>',
        '    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>',
        '    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>',
        f'    <OutputPath>Temp\\CodexBuild\\{project_name}\\</OutputPath>',
        f'    <DefineConstants>{defines}</DefineConstants>',
        f'    <UnityEditorManagedDir>{UNITY_EDITOR}</UnityEditorManagedDir>',
        '    <UnityEngineManagedDir>$(UnityEditorManagedDir)\\UnityEngine</UnityEngineManagedDir>',
        '  </PropertyGroup>',
        '',
        '  <ItemGroup>',
        *compile_items,
        '  </ItemGroup>',
        '',
        '  <ItemGroup>',
        *ref_lines,
        '  </ItemGroup>',
    ]
    if project_ref_lines:
        lines.extend(['', '  <ItemGroup>', *project_ref_lines, '  </ItemGroup>'])
    lines.append('</Project>')
    return "\n".join(lines) + "\n"


def write_text_verified(target: Path, text: str) -> str:
    target.parent.mkdir(parents=True, exist_ok=True)
    try:
        target.write_text(text, encoding="utf-8", newline="\n")
        if target.read_text(encoding="utf-8") == text:
            return "verified-direct"
    except OSError:
        pass
    return "failed"


def write_report(root: Path, report: dict[str, Any]) -> None:
    text = json.dumps(report, indent=2, sort_keys=True) + "\n"
    if write_text_verified(root / REPORT_PATH, text) == "failed":
        raise OSError(f"Unable to write report: {REPORT_PATH}")


def main() -> int:
    root = Path.cwd()
    slnx = root / "Hecton8.slnx"
    if not slnx.exists():
        print("Hecton8.slnx not found", file=sys.stderr)
        return 2
    project_paths = project_paths_from_slnx(slnx)
    project_names = [Path(path).stem for path in project_paths]
    slnx_names = set(project_names)
    asmdefs = load_asmdefs(root)
    by_guid = {asmdef.guid: asmdef for asmdef in asmdefs if asmdef.guid}
    by_name = {asmdef.name: asmdef for asmdef in asmdefs}
    by_name.update({asmdef.safe_name: asmdef for asmdef in asmdefs})
    created: list[str] = []
    updated: list[str] = []
    unchanged: list[str] = []
    preserved: list[str] = []
    unresolved: list[str] = []
    write_failures: list[str] = []
    verified_direct_writes: list[str] = []
    mapped: list[dict[str, Any]] = []
    hash_input = hashlib.sha256()

    for path_text, project_name in zip(project_paths, project_names):
        target = root / path_text
        if target.exists() and project_name == "Hecton8.Core":
            preserved.append(path_text)
            hash_input.update(target.read_bytes())
            continue
        asmdef = choose_asmdef(project_name, asmdefs)
        if asmdef is None and not project_name.startswith("Assembly-CSharp"):
            unresolved.append(path_text)
            continue
        refs = resolve_project_references(asmdef, by_guid, by_name, slnx_names)
        content = project_xml(project_name, asmdef, asmdefs, refs, slnx_names)
        fromstring_xml_safe(content)
        existed = target.exists()
        current = target.read_text(encoding="utf-8") if existed else None
        if current != content:
            write_mode = write_text_verified(target, content)
            if write_mode == "failed":
                write_failures.append(path_text)
                hash_input.update((current or "").encode("utf-8"))
                continue
            verified_direct_writes.append(path_text)
            if existed:
                updated.append(path_text)
            else:
                created.append(path_text)
        elif existed:
            unchanged.append(path_text)
        hash_input.update(content.encode("utf-8"))
        mapped.append(
            {
                "project": path_text,
                "source": "default-unity-assembly" if asmdef is None else rel(asmdef.path),
                "projectReferences": refs,
                "editor": is_editor_project(project_name, asmdef),
            }
        )

    report = {
        "agentId": "1330",
        "task": "UNITY_SLNX_CSPROJ_RESTORE",
        "status": "PASS" if not unresolved and not write_failures else "PARTIAL",
        "slnxProjectCount": len(project_paths),
        "createdProjectCount": len(created),
        "updatedProjectCount": len(updated),
        "unchangedProjectCount": len(unchanged),
        "preservedProjectCount": len(preserved),
        "unresolvedProjectCount": len(unresolved),
        "writeFailureCount": len(write_failures),
        "verifiedDirectWriteCount": len(verified_direct_writes),
        "createdProjects": created,
        "updatedProjects": updated,
        "unchangedProjects": unchanged,
        "preservedProjects": preserved,
        "unresolvedProjects": unresolved,
        "writeFailures": write_failures,
        "verifiedDirectWrites": verified_direct_writes,
        "mappings": mapped,
        "sourceOfTruth": "Unity .asmdef plus default Assembly-CSharp conventions; generated files are offline recovery artifacts",
        "writeMode": "verified direct writes; this sandbox denies Python os.replace/unlink on generated root artifacts, so the generator verifies readback instead of leaving temp files; Hecton8.Core.csproj preserved",
        "staleScriptAssemblyMasking": "asmdef projects exclude all solution project DLLs from Library/ScriptAssemblies and use project references when the asmdef target is present in Hecton8.slnx",
        "verificationHashSha256": hash_input.hexdigest(),
    }
    write_report(root, report)
    print(
        "unity_slnx_csproj_restore "
        f"status={report['status']} created={len(created)} updated={len(updated)} preserved={len(preserved)} "
        f"unresolved={len(unresolved)} hash={report['verificationHashSha256']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
