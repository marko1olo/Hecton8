#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPORT_PATH = ROOT / "Docs" / "Reports" / "RENDERING_OPTIMIZATION_REPORT.json"
SCAN_ROOTS = (ROOT / "Assets" / "_Project",)
MAX_SCAN_BYTES = 2_000_000
TEXT_SUFFIXES = {
    ".asset",
    ".cs",
    ".hlsl",
    ".mat",
    ".prefab",
    ".shader",
    ".shadergraph",
    ".unity",
    ".uss",
    ".uxml",
}

ACTIVE_DECAL_PATTERNS = (
    re.compile(r"AddComponent\s*<\s*DecalProjector\s*>"),
    re.compile(r"GetComponent\s*<\s*DecalProjector\s*>"),
    re.compile(r"\bDecalProjector\s+[A-Za-z_][A-Za-z0-9_]*"),
    re.compile(r"new\s+GameObject\s*\([^)]*(?:decal|wound|blood|crack)", re.IGNORECASE),
    re.compile(r"Instantiate\s*\([^;]*(?:decal|wound|blood|crack)", re.IGNORECASE),
)


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return path.read_text(encoding="utf-8", errors="ignore")


def iter_text_assets() -> tuple[list[Path], int]:
    files: list[Path] = []
    skipped_large = 0
    for root in SCAN_ROOTS:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if not path.is_file() or path.suffix.lower() not in TEXT_SUFFIXES:
                continue
            if path.stat().st_size > MAX_SCAN_BYTES:
                skipped_large += 1
                continue
            files.append(path)
    return files, skipped_large


def relative(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def collect_candidate_paths(files: list[Path]) -> list[Path]:
    globs: list[str] = []
    for suffix in sorted(TEXT_SUFFIXES):
        globs.extend(["--glob", f"*{suffix}"])

    try:
        completed = subprocess.run(
            [
                "rg",
                "--files-with-matches",
                "--no-messages",
                *globs,
                "DecalProjector|UnityEngine.Rendering.Universal.DecalRendererFeature|new\\s+GameObject|Instantiate\\s*\\(|AddComponent\\s*<|GetComponent\\s*<",
                str(SCAN_ROOTS[0]),
            ],
            cwd=ROOT,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            check=False,
        )
    except OSError:
        return files

    if completed.returncode not in (0, 1):
        return files

    candidates: list[Path] = []
    seen: set[Path] = set()
    for line in completed.stdout.splitlines():
        path = (ROOT / line.strip()).resolve()
        if path in seen or not path.exists() or path.stat().st_size > MAX_SCAN_BYTES:
            continue
        seen.add(path)
        candidates.append(path)
    return candidates


def scan_active_renderer_features(path: Path, text: str) -> tuple[list[dict[str, object]], int]:
    active: list[dict[str, object]] = []
    inactive = 0
    lines = text.splitlines()
    for index, line in enumerate(lines):
        if "UnityEngine.Rendering.Universal.DecalRendererFeature" not in line:
            continue

        block = lines[max(0, index - 8) : min(len(lines), index + 24)]
        active_line = next((candidate for candidate in block if "m_Active:" in candidate), "")
        is_active = active_line.strip().endswith("1")
        if is_active:
            active.append({"path": relative(path), "line": index + 1, "activeLine": active_line.strip()})
        else:
            inactive += 1
    return active, inactive


def main() -> int:
    files, skipped_large = iter_text_assets()
    candidate_files = collect_candidate_paths(files)
    token_hits: list[dict[str, object]] = []
    active_violations: list[dict[str, object]] = []
    active_renderer_features: list[dict[str, object]] = []
    inactive_renderer_feature_count = 0

    for path in candidate_files:
        text = read_text(path)
        active, inactive = scan_active_renderer_features(path, text)
        active_renderer_features.extend(active)
        inactive_renderer_feature_count += inactive

        for line_index, line in enumerate(text.splitlines(), start=1):
            if "DecalProjector" in line:
                token_hits.append({"path": relative(path), "line": line_index, "text": line.strip()[:180]})

            if any(pattern.search(line) for pattern in ACTIVE_DECAL_PATTERNS):
                active_violations.append({"path": relative(path), "line": line_index, "text": line.strip()[:180]})

    entry = {
        "agentId": "SHINOBU_275",
        "scanner": "Decal_Projector_Inquisition",
        "timestampUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "evidenceClass": "STATIC_SOURCE_TARGETED",
        "scannedAssetCount": len(files),
        "candidateAssetCount": len(candidate_files),
        "skippedLargeAssetCount": skipped_large,
        "decalProjectorTokenHits": len(token_hits),
        "activeGameObjectDecalViolations": len(active_violations),
        "activeDecalRendererFeatureViolations": len(active_renderer_features),
        "inactiveUrpDecalRendererFeatures": inactive_renderer_feature_count,
        "oopDecalsPurged": len(active_violations) == 0 and len(active_renderer_features) == 0,
        "status": "PASS" if len(active_violations) == 0 and len(active_renderer_features) == 0 else "FAIL",
        "tokenHits": token_hits,
        "activeViolations": active_violations,
        "activeRendererFeatures": active_renderer_features,
        "notes": "Inactive URP DecalRendererFeature assets are reported but not counted as active GameObject decal routes. Runtime visor wounds use _GlobalVisorWounds via RenderGraph fullscreen pass.",
    }

    if REPORT_PATH.exists():
        try:
            report = json.loads(REPORT_PATH.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            report = {}
    else:
        report = {}

    report["shinobu_275_screen_space_wound_decal_compressor"] = entry
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=False) + "\n", encoding="utf-8")
    print(json.dumps(entry, indent=2, sort_keys=True))
    return 0 if entry["status"] == "PASS" else 2


if __name__ == "__main__":
    raise SystemExit(main())
