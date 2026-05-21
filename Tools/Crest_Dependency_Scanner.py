#!/usr/bin/env python3
"""Static Crest dependency wall scanner for SHINOBU_260."""

from __future__ import annotations

import json
import re
import subprocess
import sys
import time
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
REPORT_PATH = PROJECT_ROOT / "Docs" / "Reports" / "ARCHITECTURE_OPTIMIZATION_REPORT.json"

ALLOWED_RELATIVE_PREFIXES = (
    Path("Assets/_Project/Scripts/Plugins/Crest"),
)
THIRD_PARTY_PREFIXES = (
    Path("Assets/Crest"),
    Path("Docs/Archive"),
)
CREST_ASSEMBLIES = {
    "Crest",
    "Crest.Helpers.Editor",
    "WaveHarmonic.Crest",
    "WaveHarmonic.Crest.Shared",
    "WaveHarmonic.Crest.Editor",
    "WaveHarmonic.Crest.Shared.Editor",
    "WaveHarmonic.Crest.Scripting",
    "WaveHarmonic.Crest.Samples",
}
CREST_ASMDEF_GUID_REFERENCES = {
    "GUID:5b35af79ebbe89647a157055d52c59d3",  # Assets/Crest/Crest/Scripts/Crest.asmdef
    "GUID:59cd48da98d9e4a80917b613abe9416e",  # Assets/Crest/Crest/Scripts/Editor/Crest.Editor.asmdef.meta
}
CREST_ASSEMBLY_REFERENCES = CREST_ASSEMBLIES | CREST_ASMDEF_GUID_REFERENCES
CREST_SCRIPTING_DEFINE_SYMBOLS = (
    "CREST_OCEAN",
    "CREST_URP",
)
COMPLIANCE_DENYLIST_PATH = Path("Assets/_Project/Scripts/Editor/HectonComplianceValidator.cs")
GENERATED_REPORT_PATHS = (
    Path("Assets/profilermarkers.csv"),
)
GENERATED_PROJECT_EXTENSIONS = {
    ".csproj",
    ".sln",
    ".slnx",
    ".props",
    ".targets",
    ".rsp",
}
GENERATED_PROJECT_DONOR_FILES = {
    "Crest.csproj",
    "Crest.Helpers.Editor.csproj",
}
CREST_DONOR_OPTIONAL_REFERENCE_PACKAGES = {
    "Unity.RenderPipelines.HighDefinition.Runtime": "com.unity.render-pipelines.high-definition",
    "Unity.Postprocessing.Runtime": "com.unity.postprocessing",
}
QUARANTINED_ASSET_GUIDS = {
    "ed12880d16f3f2f4e80ceee64594101d",  # archived Crest5_WaveSpectrum.asset
    "149ebcba5c729ad49911b1ea4b8456fd",  # archived Crest5_FoamSettings.asset
    "0ef7bde4d259c9d4abcc93f41b0903a0",  # archived 03_HECTON_WORLD_CREST5.unity
    "a73ab923bdc811242bdca5f288eb3877",  # archived Assets/_Recovery folder
}
QUARANTINED_ASSET_GUID_RE = "|".join(sorted(QUARANTINED_ASSET_GUIDS))
CREST_DONOR_ASMDEF_PATHS = (
    Path("Assets/Crest/Crest/Scripts/Crest.asmdef"),
    Path("Assets/Crest/Crest/Scripts/Editor/Crest.Editor.asmdef"),
)

USING_RE = re.compile(r"^\s*using\s+(?:[A-Za-z_][A-Za-z0-9_]*\s*=\s*)?(Crest|WaveHarmonic\.Crest)(?:\s*;|\.)")
DIRECT_RE = re.compile(
    r"\b(Crest\.OceanRenderer|Crest\.UnderwaterRenderer|WaveHarmonic\.Crest|"
    r"OceanRenderer\.Instance|UnderwaterRenderer\.Instance|WaterRenderer\.Instance)\b"
)
CREST_DEFINE_RE = re.compile(r"\b(?:" + "|".join(CREST_SCRIPTING_DEFINE_SYMBOLS) + r")\b")
CREST_PREPROCESSOR_DEFINE_RE = re.compile(r"^\s*#\s*(?:if|elif)\b.*\b(?:" + "|".join(CREST_SCRIPTING_DEFINE_SYMBOLS) + r")\b")
REFLECTION_STRING_RE = re.compile(r'"[^"\n]*(?:Crest\.OceanRenderer|Crest\.UnderwaterRenderer|WaveHarmonic\.Crest)[^"\n]*"')
ACTIVE_ASSET_BREACH_RE = re.compile(
    r"(WaveHarmonic\.Crest::|WaveHarmonic\.Crest|"
    r"382a5d8b1147b4e78a31353c022b8e15|03aa24b56404b45a190a2cfc0c7cc100|"
    + QUARANTINED_ASSET_GUID_RE
    + r"|"
    r"Crest5KinematicsAdapter|com\.waveharmonic\.crest|Crest::Crest\.UnderwaterRenderer|"
    r"^\s*-\s+Crest\s*$)"
)
CREST_SHADER_INCLUDE_RE = re.compile(r'#include\s+"[^"\n]*Crest/Crest/Shaders/')
CREST_SHADER_NAME_RE = re.compile(r'Shader\s+"Crest/')
GENERATED_PROJECT_HARD_ROUTE_RE = re.compile(
    r'(<ProjectReference\s+Include="(?:Crest|Crest\.Helpers\.Editor|WaveHarmonic\.Crest[^"]*)\.csproj"\s*/>|'
    r'<Project\s+Path="WaveHarmonic\.Crest[^"]*\.csproj"\s*/>|'
    r'<Reference\s+Include="(?:Crest|WaveHarmonic\.Crest[^"]*)"|'
    r'<(?:Compile|None|Content)\s+Include="Packages[\\/]+com\.waveharmonic\.crest[^"]*"\s*/>)'
)
RG_ACTIVE_PATTERNS = (
    rf"WaveHarmonic\.Crest::|WaveHarmonic\.Crest|382a5d8b1147b4e78a31353c022b8e15|03aa24b56404b45a190a2cfc0c7cc100|{QUARANTINED_ASSET_GUID_RE}|Crest5KinematicsAdapter|com\.waveharmonic\.crest|Crest::Crest\.UnderwaterRenderer|^\s*-\s+Crest\s*$",
    r'#include\s+"[^"\n]*Crest/Crest/Shaders/',
    r'Shader\s+"Crest/',
)
ASSET_SCAN_EXTENSIONS = {".prefab", ".unity", ".asset", ".mat"}
SHADER_SCAN_EXTENSIONS = {".shader", ".hlsl", ".compute"}
SERIALIZED_TEXT_SCAN_EXTENSIONS = ASSET_SCAN_EXTENSIONS | {".meta", ".json"}
MAX_ACTIVE_ASSET_SCAN_BYTES = 2 * 1024 * 1024
GENERIC_CREST_TERMS = (
    "WaveCrest",
    "CrestReach",
    "CrestSharpness",
)


def rel(path: Path) -> Path:
    return path.resolve().relative_to(PROJECT_ROOT)


def is_prefixed(path: Path, prefixes: tuple[Path, ...]) -> bool:
    relative = rel(path)
    return any(relative == prefix or prefix in relative.parents for prefix in prefixes)


def strip_strings(line: str) -> str:
    result = []
    in_string = False
    escaped = False
    for char in line:
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
            result.append(" ")
            continue
        if char == '"':
            in_string = True
            result.append(" ")
            continue
        result.append(char)
    return "".join(result)


def collect_assembly_definition_paths() -> tuple[list[Path], list[Path]]:
    asmdef_paths: list[Path] = []
    asmref_paths: list[Path] = []
    for root_name in ("Assets", "Packages"):
        root = PROJECT_ROOT / root_name
        if not root.exists():
            continue
        asmdef_paths.extend(root.rglob("*.asmdef"))
        asmref_paths.extend(root.rglob("*.asmref"))
    return asmdef_paths, asmref_paths


def classify_assembly_reference_record(path: Path, record: dict, breaches: list[dict], allowed_hits: list[dict]) -> None:
    if is_prefixed(path, ALLOWED_RELATIVE_PREFIXES):
        allowed_hits.append(record)
    else:
        breaches.append(record)


def scan_assembly_definitions() -> tuple[list[dict], list[dict]]:
    breaches: list[dict] = []
    allowed_hits: list[dict] = []
    asmdef_paths, asmref_paths = collect_assembly_definition_paths()

    for path in sorted(asmdef_paths):
        if is_prefixed(path, THIRD_PARTY_PREFIXES):
            continue
        try:
            data = json.loads(path.read_text(encoding="utf-8-sig"))
        except Exception as exc:  # noqa: BLE001
            breaches.append({"kind": "asmdef_parse_error", "path": str(rel(path)), "detail": str(exc)})
            continue

        references = data.get("references", [])
        if not isinstance(references, list):
            references = []

        define_hits: list[str] = []
        define_constraints = data.get("defineConstraints", [])
        if isinstance(define_constraints, list):
            define_hits.extend(
                constraint
                for constraint in define_constraints
                if isinstance(constraint, str) and CREST_DEFINE_RE.search(constraint)
            )
        version_defines = data.get("versionDefines", [])
        if isinstance(version_defines, list):
            for version_define in version_defines:
                if not isinstance(version_define, dict):
                    continue
                define = version_define.get("define")
                expression = version_define.get("expression")
                if isinstance(define, str) and CREST_DEFINE_RE.search(define):
                    define_hits.append(define)
                if isinstance(expression, str) and CREST_DEFINE_RE.search(expression):
                    define_hits.append(expression)

        if define_hits:
            record = {
                "kind": "asmdef_crest_define_constraint",
                "path": str(rel(path)),
                "defines": sorted(set(define_hits)),
            }
            classify_assembly_reference_record(path, record, breaches, allowed_hits)

        crest_refs = [reference for reference in references if isinstance(reference, str) and reference in CREST_ASSEMBLY_REFERENCES]
        if not crest_refs:
            continue
        record = {"kind": "asmdef_reference", "path": str(rel(path)), "references": crest_refs}
        if is_prefixed(path, ALLOWED_RELATIVE_PREFIXES) and data.get("autoReferenced") is not False:
            breaches.append(
                {
                    "kind": "bridge_crest_asmdef_auto_referenced",
                    "path": str(rel(path)),
                    "references": crest_refs,
                    "detail": "Crest bridge assemblies must be opt-in; autoReferenced must stay false.",
                }
            )
            continue
        classify_assembly_reference_record(path, record, breaches, allowed_hits)

    for path in sorted(asmref_paths):
        if is_prefixed(path, THIRD_PARTY_PREFIXES):
            continue
        try:
            data = json.loads(path.read_text(encoding="utf-8-sig"))
        except Exception as exc:  # noqa: BLE001
            breaches.append({"kind": "asmref_parse_error", "path": str(rel(path)), "detail": str(exc)})
            continue

        reference = data.get("reference")
        if not isinstance(reference, str) or reference not in CREST_ASSEMBLY_REFERENCES:
            continue
        record = {"kind": "asmref_reference", "path": str(rel(path)), "references": [reference]}
        classify_assembly_reference_record(path, record, breaches, allowed_hits)
    return breaches, allowed_hits


def scan_crest_donor_autoreference() -> list[dict]:
    breaches: list[dict] = []
    for relative_path in CREST_DONOR_ASMDEF_PATHS:
        path = PROJECT_ROOT / relative_path
        if not path.exists():
            continue
        try:
            data = json.loads(path.read_text(encoding="utf-8-sig"))
        except Exception as exc:  # noqa: BLE001
            breaches.append({"kind": "crest_donor_asmdef_parse_error", "path": relative_path.as_posix(), "detail": str(exc)})
            continue
        if data.get("autoReferenced") is False:
            continue
        breaches.append(
            {
                "kind": "crest_donor_asmdef_auto_referenced",
                "path": relative_path.as_posix(),
                "detail": "Active Crest donor assemblies must remain autoReferenced=false to preserve the compile wall.",
            }
        )
    return breaches


def load_package_ids() -> set[str]:
    package_ids: set[str] = set()
    manifest_path = PROJECT_ROOT / "Packages" / "manifest.json"
    lock_path = PROJECT_ROOT / "Packages" / "packages-lock.json"

    for path in (manifest_path, lock_path):
        if not path.exists():
            continue
        try:
            data = json.loads(path.read_text(encoding="utf-8-sig"))
        except Exception:
            continue
        dependencies = data.get("dependencies", {})
        if isinstance(dependencies, dict):
            package_ids.update(key for key in dependencies if isinstance(key, str))
    return package_ids


def scan_crest_donor_missing_optional_references() -> list[dict]:
    """Fail selected Crest donor references to optional Unity assemblies when the backing package is absent."""
    breaches: list[dict] = []
    package_ids = load_package_ids()
    for relative_path in CREST_DONOR_ASMDEF_PATHS:
        path = PROJECT_ROOT / relative_path
        if not path.exists():
            continue
        try:
            data = json.loads(path.read_text(encoding="utf-8-sig"))
        except Exception as exc:  # noqa: BLE001
            breaches.append({"kind": "crest_donor_asmdef_parse_error", "path": relative_path.as_posix(), "detail": str(exc)})
            continue
        references = data.get("references", [])
        if not isinstance(references, list):
            continue
        for reference in references:
            package_id = CREST_DONOR_OPTIONAL_REFERENCE_PACKAGES.get(reference)
            if package_id is None or package_id in package_ids:
                continue
            breaches.append(
                {
                    "kind": "crest_donor_missing_optional_package_reference",
                    "path": relative_path.as_posix(),
                    "reference": reference,
                    "missing_package": package_id,
                    "detail": "Selected Crest donor asmdef references an assembly whose Unity package is not present in manifest or packages-lock.",
                }
            )
    return breaches


def scan_csharp() -> tuple[list[dict], list[dict]]:
    breaches: list[dict] = []
    allowed_hits: list[dict] = []
    for path in sorted((PROJECT_ROOT / "Assets" / "_Project").rglob("*.cs")):
        if is_prefixed(path, THIRD_PARTY_PREFIXES):
            continue
        try:
            lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
        except Exception as exc:  # noqa: BLE001
            breaches.append({"kind": "cs_read_error", "path": str(rel(path)), "detail": str(exc)})
            continue

        for line_number, line in enumerate(lines, start=1):
            stripped = strip_strings(line)
            if not (USING_RE.search(stripped) or DIRECT_RE.search(stripped)):
                continue
            record = {"kind": "csharp_direct_reference", "path": str(rel(path)), "line": line_number, "text": stripped.strip()}
            if is_prefixed(path, ALLOWED_RELATIVE_PREFIXES):
                allowed_hits.append(record)
            else:
                breaches.append(record)
    return breaches, allowed_hits


def scan_first_party_scripting_define_usage() -> tuple[list[dict], list[dict]]:
    """Fail on first-party Crest scripting-symbol branches outside the bridge."""
    breaches: list[dict] = []
    allowed_hits: list[dict] = []
    for path in sorted((PROJECT_ROOT / "Assets" / "_Project").rglob("*.cs")):
        if is_prefixed(path, THIRD_PARTY_PREFIXES):
            continue
        try:
            lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
        except Exception as exc:  # noqa: BLE001
            breaches.append({"kind": "cs_read_error", "path": str(rel(path)), "detail": str(exc)})
            continue

        for line_number, line in enumerate(lines, start=1):
            if not CREST_PREPROCESSOR_DEFINE_RE.search(line):
                continue
            record = {
                "kind": "crest_scripting_define_usage",
                "path": str(rel(path)),
                "line": line_number,
                "text": line.strip(),
            }
            if is_prefixed(path, ALLOWED_RELATIVE_PREFIXES):
                allowed_hits.append(record)
            else:
                breaches.append(record)
    return breaches, allowed_hits


def scan_reflection_strings() -> list[dict]:
    """Report non-compiling Crest reflection strings outside the bridge without failing the compile-wall gate."""
    hits: list[dict] = []
    for path in sorted((PROJECT_ROOT / "Assets" / "_Project").rglob("*.cs")):
        if is_prefixed(path, THIRD_PARTY_PREFIXES) or is_prefixed(path, ALLOWED_RELATIVE_PREFIXES):
            continue
        if "/Editor/" in rel(path).as_posix():
            continue
        try:
            lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
        except Exception:
            continue

        for line_number, line in enumerate(lines, start=1):
            if REFLECTION_STRING_RE.search(line):
                hits.append(
                    {
                        "kind": "crest_reflection_string",
                        "path": str(rel(path)),
                        "line": line_number,
                        "text": line.strip(),
                    }
                )
    return hits


def scan_active_assets() -> list[dict]:
    """Fail on serialized Crest5 refs, third-party underwater components, and Crest shader imports outside the bridge."""
    rg_result = scan_active_assets_with_rg()
    if rg_result is not None:
        return rg_result

    return scan_active_assets_with_python()


def scan_active_assets_with_rg() -> list[dict] | None:
    breaches: list[dict] = []
    args = [
        "rg",
        "--json",
        "-n",
        "-a",
        "--glob",
        "*.asset",
        "--glob",
        "*.prefab",
        "--glob",
        "*.unity",
        "--glob",
        "*.mat",
        "--glob",
        "*.meta",
        "--glob",
        "*.json",
        "--glob",
        "*.shader",
        "--glob",
        "*.hlsl",
        "--glob",
        "*.compute",
        "--glob",
        "!Assets/Crest/**",
    ]
    for pattern in RG_ACTIVE_PATTERNS:
        args.extend(("-e", pattern))
    args.extend(("ProjectSettings", "Packages", "Assets"))

    try:
        completed = subprocess.run(
            args,
            cwd=PROJECT_ROOT,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
    except FileNotFoundError:
        return None

    if completed.returncode not in (0, 1):
        breaches.append(
            {
                "kind": "active_asset_scan_error",
                "path": ".",
                "detail": completed.stderr.strip() or f"rg exited {completed.returncode}",
            }
        )
        return breaches

    for raw_line in completed.stdout.splitlines():
        try:
            event = json.loads(raw_line)
        except json.JSONDecodeError:
            continue
        if event.get("type") != "match":
            continue
        data = event.get("data", {})
        path_text = data.get("path", {}).get("text")
        line_text = data.get("lines", {}).get("text", "").rstrip("\r\n")
        line_number = data.get("line_number", 0)
        if not path_text:
            continue
        path = PROJECT_ROOT / path_text
        if is_prefixed(path, THIRD_PARTY_PREFIXES):
            continue

        suffix = path.suffix.lower()
        if suffix in SERIALIZED_TEXT_SCAN_EXTENSIONS and ACTIVE_ASSET_BREACH_RE.search(line_text):
            breaches.append(
                {
                    "kind": "active_serialized_crest_reference",
                    "path": str(rel(path)),
                    "line": line_number,
                    "text": line_text.strip(),
                }
            )
            continue
        if suffix in SHADER_SCAN_EXTENSIONS and not is_prefixed(path, ALLOWED_RELATIVE_PREFIXES):
            if CREST_SHADER_INCLUDE_RE.search(line_text) or CREST_SHADER_NAME_RE.search(line_text):
                breaches.append(
                    {
                        "kind": "active_shader_crest_reference",
                        "path": str(rel(path)),
                        "line": line_number,
                        "text": line_text.strip(),
                    }
                )
    return breaches


def scan_active_assets_with_python() -> list[dict]:
    breaches: list[dict] = []
    paths: set[Path] = set()
    scan_roots = (
        PROJECT_ROOT / "Assets",
        PROJECT_ROOT / "ProjectSettings",
        PROJECT_ROOT / "Packages",
    )
    active_extensions = SERIALIZED_TEXT_SCAN_EXTENSIONS | SHADER_SCAN_EXTENSIONS
    for root in scan_roots:
        if not root.exists():
            continue
        if root.is_file():
            paths.add(root)
            continue
        for path in root.rglob("*"):
            if not path.is_file():
                continue
            if path.suffix.lower() in active_extensions:
                paths.add(path)

    for path in sorted(paths):
        if is_prefixed(path, THIRD_PARTY_PREFIXES):
            continue

        suffix = path.suffix.lower()
        if suffix not in active_extensions:
            continue

        try:
            with path.open("rb") as handle:
                raw = handle.read(MAX_ACTIVE_ASSET_SCAN_BYTES)
        except Exception as exc:  # noqa: BLE001
            breaches.append({"kind": "asset_read_error", "path": str(rel(path)), "detail": str(exc)})
            continue

        text = raw.decode("utf-8-sig", errors="replace")
        lines = text.splitlines()
        for line_number, line in enumerate(lines, start=1):
            if suffix in SERIALIZED_TEXT_SCAN_EXTENSIONS and ACTIVE_ASSET_BREACH_RE.search(line):
                breaches.append(
                    {
                        "kind": "active_serialized_crest_reference",
                        "path": str(rel(path)),
                        "line": line_number,
                        "text": line.strip(),
                    }
                )
                continue

            if suffix in SHADER_SCAN_EXTENSIONS and not is_prefixed(path, ALLOWED_RELATIVE_PREFIXES):
                if CREST_SHADER_INCLUDE_RE.search(line) or CREST_SHADER_NAME_RE.search(line):
                    breaches.append(
                        {
                            "kind": "active_shader_crest_reference",
                            "path": str(rel(path)),
                            "line": line_number,
                            "text": line.strip(),
                        }
                    )
    return breaches


def scan_active_package_visibility() -> list[dict]:
    """Crest 5 package must stay outside Unity-visible Packages entirely."""
    package_path = PROJECT_ROOT / "Packages" / "com.waveharmonic.crest"
    if not package_path.exists():
        return []
    return [
        {
            "kind": "active_crest5_package_visible",
            "path": str(rel(package_path)),
            "detail": "Packages/com.waveharmonic.crest is active; Crest 5 must remain under Docs/Archive/Crest_Version_Quarantine.",
        }
    ]


def scan_generated_report_crest_rows() -> list[dict]:
    """Fail Unity-visible generated reports that still contain stale donor assembly/profile rows."""
    breaches: list[dict] = []
    for relative_path in GENERATED_REPORT_PATHS:
        path = PROJECT_ROOT / relative_path
        if not path.exists():
            continue
        try:
            lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
        except Exception as exc:  # noqa: BLE001
            breaches.append({"kind": "generated_report_read_error", "path": relative_path.as_posix(), "detail": str(exc)})
            continue
        for line_number, line in enumerate(lines, start=1):
            if "Crest" not in line and "WaveHarmonic" not in line:
                continue
            breaches.append(
                {
                    "kind": "generated_report_crest_reference",
                    "path": relative_path.as_posix(),
                    "line": line_number,
                    "text": line.strip(),
                }
            )
    return breaches


def collect_generated_project_paths() -> list[Path]:
    paths: list[Path] = []
    for path in PROJECT_ROOT.iterdir():
        if path.is_file() and path.suffix.lower() in GENERATED_PROJECT_EXTENSIONS:
            paths.append(path)
    return sorted(paths)


def scan_generated_project_crest_routes() -> tuple[list[dict], list[dict], list[dict]]:
    """Fail generated IDE/MSBuild surfaces that keep first-party routes into Crest."""
    breaches: list[dict] = []
    define_hits: list[dict] = []
    prune_rule_hits: list[dict] = []

    for path in collect_generated_project_paths():
        relative = rel(path)
        if path.name.startswith("WaveHarmonic.Crest") and path.suffix.lower() == ".csproj":
            breaches.append(
                {
                    "kind": "active_waveharmonic_generated_project_file",
                    "path": str(relative),
                    "detail": "Root generated WaveHarmonic Crest project is active while Packages/com.waveharmonic.crest is quarantined.",
                }
            )

        try:
            lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
        except Exception as exc:  # noqa: BLE001
            breaches.append({"kind": "generated_project_read_error", "path": str(relative), "detail": str(exc)})
            continue

        for line_number, line in enumerate(lines, start=1):
            if "DefineConstants" in line:
                symbols = [symbol for symbol in CREST_SCRIPTING_DEFINE_SYMBOLS if symbol in line]
                if symbols:
                    define_hits.append(
                        {
                            "kind": "generated_project_crest_scripting_define",
                            "path": str(relative),
                            "line": line_number,
                            "symbols": symbols,
                        }
                    )

            if path.name == "Directory.Build.targets" and (
                "HectonPruneMissingWaveHarmonicCrestPackageItems" in line
                or "com.waveharmonic.crest" in line
                or "WaveHarmonic.Crest" in line
            ):
                prune_rule_hits.append(
                    {
                        "kind": "generated_project_waveharmonic_prune_rule",
                        "path": str(relative),
                        "line": line_number,
                        "text": line.strip(),
                    }
                )
                continue

            if not GENERATED_PROJECT_HARD_ROUTE_RE.search(line):
                continue

            if path.name == "Crest.Helpers.Editor.csproj" and 'ProjectReference Include="Crest.csproj"' in line:
                continue

            breaches.append(
                {
                    "kind": "generated_project_crest_route",
                    "path": str(relative),
                    "line": line_number,
                    "text": line.strip(),
                }
            )

    return breaches, define_hits, prune_rule_hits


def scan_global_scripting_defines() -> list[dict]:
    """Report global Crest scripting symbols; they are donor state, not a first-party route by themselves."""
    path = PROJECT_ROOT / "ProjectSettings" / "ProjectSettings.asset"
    if not path.exists():
        return []
    hits: list[dict] = []
    try:
        lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    except Exception as exc:  # noqa: BLE001
        return [
            {
                "kind": "project_settings_read_error",
                "path": "ProjectSettings/ProjectSettings.asset",
                "detail": str(exc),
            }
        ]

    for line_number, line in enumerate(lines, start=1):
        symbols = [symbol for symbol in CREST_SCRIPTING_DEFINE_SYMBOLS if symbol in line]
        if not symbols:
            continue
        platform = line.split(":", 1)[0].strip()
        hits.append(
            {
                "kind": "global_crest_scripting_define",
                "path": str(rel(path)),
                "line": line_number,
                "platform": platform,
                "symbols": symbols,
                "text": line.strip(),
            }
        )
    return hits


def scan_compliance_denylist_strings() -> list[dict]:
    """Report editor compliance denylist strings so policy-only Crest mentions are visible evidence."""
    path = PROJECT_ROOT / COMPLIANCE_DENYLIST_PATH
    if not path.exists():
        return []

    try:
        lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
    except Exception as exc:  # noqa: BLE001
        return [
            {
                "kind": "compliance_denylist_read_error",
                "path": COMPLIANCE_DENYLIST_PATH.as_posix(),
                "detail": str(exc),
            }
        ]

    hits: list[dict] = []
    denylist_tokens = tuple(sorted(CREST_ASSEMBLIES | {"using Crest", "global::Crest", "Crest."}))
    for line_number, line in enumerate(lines, start=1):
        tokens = [token for token in denylist_tokens if token in line]
        if not tokens:
            continue
        hits.append(
            {
                "kind": "crest_compliance_denylist_string",
                "path": str(rel(path)),
                "line": line_number,
                "tokens": tokens,
                "text": line.strip(),
            }
        )
    return hits


def scan_vocabulary_debt() -> list[dict]:
    """Report non-failing donor vocabulary outside the bridge so serialized ABI debt stays visible."""
    hits: list[dict] = []
    for path in sorted((PROJECT_ROOT / "Assets" / "_Project").rglob("*.cs")):
        if is_prefixed(path, THIRD_PARTY_PREFIXES) or is_prefixed(path, ALLOWED_RELATIVE_PREFIXES):
            continue
        if "/Editor/" in rel(path).as_posix():
            continue
        try:
            lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
        except Exception:
            continue

        for line_number, line in enumerate(lines, start=1):
            if "Crest" not in line:
                continue
            if any(term in line for term in GENERIC_CREST_TERMS):
                continue
            hits.append(
                {
                    "kind": "crest_vocabulary_debt",
                    "path": str(rel(path)),
                    "line": line_number,
                    "text": line.strip(),
                }
            )
    return hits


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    asmdef_breaches, asmdef_allowed = scan_assembly_definitions()
    csharp_breaches, csharp_allowed = scan_csharp()
    define_breaches, define_allowed = scan_first_party_scripting_define_usage()
    reflection_hits = scan_reflection_strings()
    active_asset_breaches = scan_active_assets()
    active_package_breaches = scan_active_package_visibility()
    donor_autoreference_breaches = scan_crest_donor_autoreference()
    donor_missing_reference_breaches = scan_crest_donor_missing_optional_references()
    generated_report_breaches = scan_generated_report_crest_rows()
    generated_project_breaches, generated_project_define_hits, generated_project_prune_rule_hits = scan_generated_project_crest_routes()
    global_define_hits = scan_global_scripting_defines()
    compliance_denylist_hits = scan_compliance_denylist_strings()
    vocabulary_debt_hits = scan_vocabulary_debt()
    breaches = (
        asmdef_breaches
        + csharp_breaches
        + define_breaches
        + active_asset_breaches
        + active_package_breaches
        + donor_autoreference_breaches
        + donor_missing_reference_breaches
        + generated_report_breaches
        + generated_project_breaches
    )
    allowed_hits = asmdef_allowed + csharp_allowed + define_allowed

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    report = {
        "agent": "SHINOBU_260",
        "role": "CREST_VERSION_QUARANTINE_DIRECTOR",
        "timestamp_utc": time.strftime("%Y%m%d_%H%M%S", time.gmtime()),
        "allowed_bridge_prefixes": [prefix.as_posix() for prefix in ALLOWED_RELATIVE_PREFIXES],
        "breach_count": len(breaches),
        "allowed_hit_count": len(allowed_hits),
        "quarantine_breaches_prevented": len(allowed_hits),
        "reflection_string_hit_count": len(reflection_hits),
        "global_scripting_define_hit_count": len(global_define_hits),
        "generated_project_scripting_define_hit_count": len(generated_project_define_hits),
        "generated_project_prune_rule_hit_count": len(generated_project_prune_rule_hits),
        "compliance_denylist_hit_count": len(compliance_denylist_hits),
        "vocabulary_debt_hit_count": len(vocabulary_debt_hits),
        "breaches": breaches,
        "allowed_hits": allowed_hits,
        "reflection_string_hits": reflection_hits,
        "global_scripting_define_hits": global_define_hits,
        "generated_project_scripting_define_hits": generated_project_define_hits,
        "generated_project_prune_rule_hits": generated_project_prune_rule_hits,
        "compliance_denylist_hits": compliance_denylist_hits,
        "vocabulary_debt_hits": vocabulary_debt_hits,
    }
    REPORT_PATH.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return 1 if breaches else 0


if __name__ == "__main__":
    raise SystemExit(main())
