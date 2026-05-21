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

USING_RE = re.compile(r"^\s*using\s+(?:[A-Za-z_][A-Za-z0-9_]*\s*=\s*)?(Crest|WaveHarmonic\.Crest)(?:\s*;|\.)")
DIRECT_RE = re.compile(
    r"\b(Crest\.OceanRenderer|Crest\.UnderwaterRenderer|WaveHarmonic\.Crest|"
    r"OceanRenderer\.Instance|UnderwaterRenderer\.Instance|WaterRenderer\.Instance)\b"
)
REFLECTION_STRING_RE = re.compile(r'"[^"\n]*(?:Crest\.OceanRenderer|Crest\.UnderwaterRenderer|WaveHarmonic\.Crest)[^"\n]*"')
ACTIVE_ASSET_BREACH_RE = re.compile(
    r"(WaveHarmonic\.Crest::|WaveHarmonic\.Crest|"
    r"382a5d8b1147b4e78a31353c022b8e15|03aa24b56404b45a190a2cfc0c7cc100|"
    r"Crest5KinematicsAdapter|com\.waveharmonic\.crest|Crest::Crest\.UnderwaterRenderer|"
    r"^\s*-\s+Crest\s*$)"
)
CREST_SHADER_INCLUDE_RE = re.compile(r'#include\s+"[^"\n]*Crest/Crest/Shaders/')
CREST_SHADER_NAME_RE = re.compile(r'Shader\s+"Crest/')
RG_ACTIVE_PATTERNS = (
    r"WaveHarmonic\.Crest::|WaveHarmonic\.Crest|382a5d8b1147b4e78a31353c022b8e15|03aa24b56404b45a190a2cfc0c7cc100|Crest5KinematicsAdapter|com\.waveharmonic\.crest|Crest::Crest\.UnderwaterRenderer|^\s*-\s+Crest\s*$",
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


def scan_asmdefs() -> tuple[list[dict], list[dict]]:
    breaches: list[dict] = []
    allowed_hits: list[dict] = []
    asmdef_paths: list[Path] = []
    for root_name in ("Assets", "Packages"):
        root = PROJECT_ROOT / root_name
        if root.exists():
            asmdef_paths.extend(root.rglob("*.asmdef"))

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
            continue
        crest_refs = [reference for reference in references if isinstance(reference, str) and reference in CREST_ASSEMBLIES]
        if not crest_refs:
            continue
        record = {"kind": "asmdef_reference", "path": str(rel(path)), "references": crest_refs}
        if is_prefixed(path, ALLOWED_RELATIVE_PREFIXES):
            allowed_hits.append(record)
        else:
            breaches.append(record)
    return breaches, allowed_hits


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

    asmdef_breaches, asmdef_allowed = scan_asmdefs()
    csharp_breaches, csharp_allowed = scan_csharp()
    reflection_hits = scan_reflection_strings()
    active_asset_breaches = scan_active_assets()
    active_package_breaches = scan_active_package_visibility()
    vocabulary_debt_hits = scan_vocabulary_debt()
    breaches = asmdef_breaches + csharp_breaches + active_asset_breaches + active_package_breaches
    allowed_hits = asmdef_allowed + csharp_allowed

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
        "vocabulary_debt_hit_count": len(vocabulary_debt_hits),
        "breaches": breaches,
        "allowed_hits": allowed_hits,
        "reflection_string_hits": reflection_hits,
        "vocabulary_debt_hits": vocabulary_debt_hits,
    }
    REPORT_PATH.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return 1 if breaches else 0


if __name__ == "__main__":
    raise SystemExit(main())
