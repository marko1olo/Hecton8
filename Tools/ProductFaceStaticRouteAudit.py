#!/usr/bin/env python3
"""Static ProductFace route contract audit.

Evidence class: STATIC_SOURCE / STATIC_DOC only. This command does not replace
Unity validators, prefab relink checks, screenshots, profiler, GC, or runtime
proof. Low/Middle/High/Ultra consequence is identical: it prevents stale static
route drift before any hardware lane wastes a Unity slot on bad ProductFace
inputs; it does not change gameplay truth or visual fidelity.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


MAX_TEXT_BYTES = 2 * 1024 * 1024


SOURCE_FILES = [
    "Assets/_Project/Scripts/Editor/ProductFaceToolMeshSourceAuthoring.cs",
    "Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs",
    "Assets/_Project/Scripts/Editor/ProductFaceTransportMeshSourceAuthoring.cs",
    "Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs",
    "Assets/_Project/Scripts/Editor/ProductFaceSkyOceanSourceValidator.cs",
    "Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs",
    "Assets/_Project/Scripts/Editor/ProductFaceMaterialTextureValidator.cs",
]

REPORT_FILES = [
    "Docs/Reports/Batch18/1874_TOOL_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md",
    "Docs/Reports/Batch18/1875_RESOURCE_PICKUP_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md",
    "Docs/Reports/Batch18/1876_TRANSPORT_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md",
    "Docs/Reports/Batch18/1877_PLAYER_SUIT_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md",
    "Docs/Reports/Batch18/1878_SKY_OCEAN_SOURCE_VALIDATOR_IMPLEMENTATION.md",
    "Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md",
    "Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST_AND_SHADER_AUDIT.md",
    "Docs/Reports/Batch18/1889_PRODUCT_FACE_ENVIRONMENT_SOURCE_EXCLUSION_MANIFEST.md",
    "Docs/Reports/Batch18/1890_PRODUCT_FACE_MATERIAL_TEXTURE_VALIDATOR_IMPLEMENTATION.md",
    "Docs/Reports/Batch18/1891_AITEXTURE_PRODUCT_FACE_HARDENING_PACKET.md",
]

CONTRACT_FILES = [
    "Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md",
    "Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_SEQUENCE.csv",
]

CURRENT_ROUTE_ROOTS = [
    "Assets/_Project/Art/Generated/ProductFace/Tools/",
    "Assets/_Project/Art/Generated/ProductFace/Resources/",
    "Assets/_Project/Art/Generated/ProductFace/Transport/",
    "Assets/_Project/Art/Generated/ProductFace/PlayerSuit/",
]

STALE_ROUTE_PATTERNS = [
    "Assets/_Project/Art/Generated/Tools/",
    "Assets/_Project/Art/Generated/Tools",
    "Assets/_Project/Art/Generated/Resources/TitaniumScrap",
    "Generated/Resources/TitaniumScrap",
    "Assets/_Project/Art/Generated/Transport/",
    "Assets/_Project/Art/Generated/Transport",
    "Generated/Transport/",
]

FORBIDDEN_SOURCE_TOKENS = [
    "GameObject.CreatePrimitive",
    "CreatePrimitive",
    "float.IsFinite",
    "double.IsFinite",
]

PENDING_MARKERS = [
    "PENDING UNITY",
    "PENDING UNITY SLOT",
    "PENDING UNITY PROOF",
    "PENDING UNITY/PROFILER VERIFICATION",
    "PENDING VERIFICATION",
    "PENDING RUNTIME",
    "PENDING PROFILER",
    "NOT RUN",
]

RUNTIME_CLAIM_PATTERNS = [
    re.compile(r"\bPLAYMODE VERIFIED\b", re.IGNORECASE),
    re.compile(r"\bPROFILER VERIFIED\b", re.IGNORECASE),
    re.compile(r"\bPLAYER-CAPTURE VERIFIED\b", re.IGNORECASE),
    re.compile(r"\bEDITOR VERIFIED\b", re.IGNORECASE),
    re.compile(r"\bvisual acceptance\s+(?:is\s+)?(?:accepted|verified|complete)\b", re.IGNORECASE),
    re.compile(r"\bruntime acceptance\s+(?:is\s+)?(?:accepted|verified|complete)\b", re.IGNORECASE),
]


@dataclass(frozen=True)
class Finding:
    severity: str
    code: str
    path: str
    message: str
    line: int | None = None

    def to_dict(self) -> dict[str, object]:
        data: dict[str, object] = {
            "severity": self.severity,
            "code": self.code,
            "path": self.path,
            "message": self.message,
        }
        if self.line is not None:
            data["line"] = self.line
        return data


class TextCache:
    def __init__(self, root: Path) -> None:
        self.root = root
        self._cache: dict[str, str | None] = {}

    def exists(self, rel_path: str) -> bool:
        return (self.root / rel_path).is_file()

    def read(self, rel_path: str) -> str | None:
        rel_path = normalize_rel(rel_path)
        if rel_path in self._cache:
            return self._cache[rel_path]

        path = self.root / rel_path
        if not path.is_file():
            self._cache[rel_path] = None
            return None

        try:
            if path.stat().st_size > MAX_TEXT_BYTES:
                self._cache[rel_path] = None
                return None
            self._cache[rel_path] = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            self._cache[rel_path] = None
        return self._cache[rel_path]


def normalize_rel(path: str) -> str:
    return path.replace("\\", "/").strip("/")


def normalize_text(text: str) -> str:
    return text.replace("\\", "/")


def line_number(text: str, start_index: int) -> int:
    return text.count("\n", 0, start_index) + 1


def route_present(text: str, route_root: str) -> bool:
    haystack = normalize_text(text)
    route = normalize_rel(route_root)
    route_with_slash = route + "/"
    return route in haystack or route_with_slash in haystack


def find_pattern_lines(text: str, pattern: str) -> Iterable[int]:
    haystack = normalize_text(text)
    start = 0
    current_line = 1
    last_idx = 0
    while True:
        idx = haystack.find(pattern, start)
        if idx < 0:
            break
        current_line += haystack.count("\n", last_idx, idx)
        last_idx = idx
        yield current_line
        start = idx + max(1, len(pattern))


def add_missing_file_findings(cache: TextCache, findings: list[Finding]) -> None:
    for rel_path in SOURCE_FILES:
        if not cache.exists(rel_path):
            findings.append(
                Finding(
                    "ERROR",
                    "MISSING_SOURCE",
                    rel_path,
                    "Required ProductFace source/validator file is missing.",
                )
            )


def add_route_findings(cache: TextCache, findings: list[Finding]) -> None:
    searchable = SOURCE_FILES + REPORT_FILES + CONTRACT_FILES
    texts: list[tuple[str, str]] = []
    for rel_path in searchable:
        text = cache.read(rel_path)
        if text is not None:
            texts.append((rel_path, text))

    for route_root in CURRENT_ROUTE_ROOTS:
        if not any(route_present(text, route_root) for _, text in texts):
            findings.append(
                Finding(
                    "ERROR",
                    "MISSING_CURRENT_ROUTE_ROOT",
                    route_root,
                    "Current ProductFace generated route root was not found in the audited source/report set.",
                )
            )

    stale_targets = SOURCE_FILES + CONTRACT_FILES
    for rel_path in stale_targets:
        text = cache.read(rel_path)
        if text is None:
            continue
        for pattern in STALE_ROUTE_PATTERNS:
            for line in find_pattern_lines(text, pattern):
                findings.append(
                    Finding(
                        "ERROR",
                        "STALE_ROUTE_ROOT",
                        rel_path,
                        f"Stale generated route root appears: {pattern}",
                        line,
                    )
                )


def add_forbidden_source_findings(cache: TextCache, findings: list[Finding]) -> None:
    for rel_path in SOURCE_FILES:
        text = cache.read(rel_path)
        if text is None:
            continue
        for token in FORBIDDEN_SOURCE_TOKENS:
            for line in find_pattern_lines(text, token):
                findings.append(
                    Finding(
                        "ERROR",
                        "FORBIDDEN_SOURCE_TOKEN",
                        rel_path,
                        f"Forbidden ProductFace source token appears: {token}",
                        line,
                    )
                )


def add_report_proof_findings(cache: TextCache, findings: list[Finding]) -> None:
    for rel_path in REPORT_FILES:
        text = cache.read(rel_path)
        if text is None:
            if rel_path.endswith("1890_PRODUCT_FACE_MATERIAL_TEXTURE_VALIDATOR_IMPLEMENTATION.md"):
                findings.append(
                    Finding(
                        "WARNING",
                        "OPTIONAL_REPORT_MISSING",
                        rel_path,
                        "Optional 1890 report is absent; task allowed absence if not present.",
                    )
                )
            else:
                findings.append(
                    Finding(
                        "ERROR",
                        "MISSING_REPORT",
                        rel_path,
                        "Required ProductFace report is missing.",
                    )
                )
            continue

        upper_text = text.upper()
        if not any(marker in upper_text for marker in PENDING_MARKERS):
            findings.append(
                Finding(
                    "ERROR",
                    "MISSING_PENDING_PROOF_BOUNDARY",
                    rel_path,
                    "Report lacks a pending/NOT RUN proof boundary for runtime, visual, Unity, or profiler claims.",
                )
            )

        for pattern in RUNTIME_CLAIM_PATTERNS:
            for match in pattern.finditer(text):
                findings.append(
                    Finding(
                        "ERROR",
                        "UNSUPPORTED_RUNTIME_ACCEPTANCE_CLAIM",
                        rel_path,
                        f"Potential runtime/visual proof upgrade without accepted evidence boundary: {match.group(0)}",
                        line_number(text, match.start()),
                    )
                )

        for line_index, line in enumerate(text.splitlines(), start=1):
            stripped = line.strip()
            if not stripped.lower().startswith("in-game result:"):
                continue
            value = stripped.split(":", 1)[1].strip().upper()
            if value.startswith("PENDING") or value.startswith("NOT RUN"):
                continue
            findings.append(
                Finding(
                    "ERROR",
                    "UNSUPPORTED_RUNTIME_ACCEPTANCE_CLAIM",
                    rel_path,
                    f"Potential runtime/visual proof upgrade without accepted evidence boundary: {stripped}",
                    line_index,
                )
            )


def add_ai_texture_findings(cache: TextCache, findings: list[Finding]) -> None:
    rel_path = "Docs/Reports/Batch18/1891_AITEXTURE_PRODUCT_FACE_HARDENING_PACKET.md"
    text = cache.read(rel_path)
    if text is None:
        findings.append(
            Finding(
                "ERROR",
                "MISSING_AI_TEXTURE_HARDENING_REPORT",
                rel_path,
                "1891 hardening report is required to guard generic AI texture binding.",
            )
        )
        return

    lowered = text.lower()
    token = "ai_texture_prefab_bindings.csv"
    guard_words = ["not `ai_texture_prefab_bindings.csv`", "reject", "refuse", "forbidden", "must not"]
    if token not in lowered:
        findings.append(
            Finding(
                "ERROR",
                "MISSING_AI_TEXTURE_BINDING_WARNING",
                rel_path,
                "1891 report does not mention ai_texture_prefab_bindings.csv.",
            )
        )
    elif not any(word in lowered for word in guard_words):
        findings.append(
            Finding(
                "ERROR",
                "WEAK_AI_TEXTURE_BINDING_WARNING",
                rel_path,
                "1891 report mentions ai_texture_prefab_bindings.csv but lacks reject/refuse/forbidden/must-not guard language.",
            )
        )

    for rel_source in SOURCE_FILES:
        source_text = cache.read(rel_source)
        if source_text is not None and token in source_text.lower():
            findings.append(
                Finding(
                    "ERROR",
                    "GENERIC_AI_TEXTURE_BINDING_IN_SOURCE",
                    rel_source,
                    "ProductFace source mentions generic ai_texture_prefab_bindings.csv route.",
                )
            )


def add_environment_findings(cache: TextCache, findings: list[Finding]) -> None:
    rel_path = "Docs/Reports/Batch18/1889_PRODUCT_FACE_ENVIRONMENT_SOURCE_EXCLUSION_MANIFEST.md"
    text = cache.read(rel_path)
    if text is None:
        findings.append(
            Finding(
                "ERROR",
                "MISSING_ENVIRONMENT_EXCLUSION_REPORT",
                rel_path,
                "1889 environment exclusion report is missing.",
            )
        )
        return

    required_terms = ["Crest", "terrain", "storm", "noir", "depth"]
    lowered = text.lower()
    for term in required_terms:
        if term.lower() not in lowered:
            findings.append(
                Finding(
                    "ERROR",
                    "MISSING_ENVIRONMENT_EXCLUSION_TERM",
                    rel_path,
                    f"1889 report lacks required exclusion term: {term}",
                )
            )


def add_io_scope_findings(cache: TextCache, findings: list[Finding]) -> None:
    checked_files = SOURCE_FILES + REPORT_FILES + CONTRACT_FILES
    skipped = []
    for rel_path in checked_files:
        path = cache.root / rel_path
        if path.is_file():
            try:
                if path.stat().st_size > MAX_TEXT_BYTES:
                    skipped.append(rel_path)
            except OSError:
                skipped.append(rel_path)
    if skipped:
        findings.append(
            Finding(
                "WARNING",
                "TEXT_FILE_TOO_LARGE",
                "<audit-scope>",
                "Some scoped text files were skipped because they exceeded the static audit size limit: "
                + ", ".join(skipped),
            )
        )


def run_audit(root: Path) -> list[Finding]:
    cache = TextCache(root)
    findings: list[Finding] = []
    add_missing_file_findings(cache, findings)
    add_route_findings(cache, findings)
    add_forbidden_source_findings(cache, findings)
    add_report_proof_findings(cache, findings)
    add_ai_texture_findings(cache, findings)
    add_environment_findings(cache, findings)
    add_io_scope_findings(cache, findings)
    return findings


def severity_counts(findings: Iterable[Finding]) -> dict[str, int]:
    counts = {"ERROR": 0, "WARNING": 0, "INFO": 0}
    for finding in findings:
        counts[finding.severity] = counts.get(finding.severity, 0) + 1
    return counts


def print_text_summary(findings: list[Finding]) -> None:
    counts = severity_counts(findings)
    print("ProductFace static route audit")
    print(f"ERROR: {counts.get('ERROR', 0)}")
    print(f"WARNING: {counts.get('WARNING', 0)}")
    print(f"INFO: {counts.get('INFO', 0)}")

    if not findings:
        print("No findings.")
        return

    print("Findings:")
    for finding in findings:
        location = finding.path
        if finding.line is not None:
            location = f"{location}:{finding.line}"
        print(f"[{finding.severity}] {finding.code} {location} - {finding.message}")


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Static audit for HECTON-8 ProductFace route contracts."
    )
    parser.add_argument("--root", default=".", help="Project root. Default: current directory.")
    parser.add_argument(
        "--fail-on-error",
        action="store_true",
        help="Return non-zero when ERROR findings exist.",
    )
    parser.add_argument("--json", action="store_true", help="Emit JSON only.")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    root = Path(args.root).resolve()
    findings = run_audit(root)
    counts = severity_counts(findings)

    if args.json:
        payload = {
            "tool": "ProductFaceStaticRouteAudit",
            "root": os.fspath(root),
            "counts": counts,
            "findings": [finding.to_dict() for finding in findings],
        }
        print(json.dumps(payload, indent=2, sort_keys=True))
    else:
        print_text_summary(findings)

    if args.fail_on_error and counts.get("ERROR", 0) > 0:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
