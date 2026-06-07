#!/usr/bin/env python3
"""Static texture-sample audit for owned HECTON-8 UI shaders."""

from __future__ import annotations

import argparse
import glob
import hashlib
import json
import re
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve()
ROOT = SCRIPT_PATH.parents[2]
SPEC_PATH = ROOT / "Docs" / "Design" / "HardwareAdaptiveUIScaler.json"
DEFAULT_REPORT = ROOT / "Docs" / "AgentLogs" / "UI_ShaderSampleAudit_UX_ENGINEER.json"
MATERIAL_SAMPLE_RE = re.compile(r"\b(?:SAMPLE_TEXTURE2D(?:_X|_LOD|_X_LOD)?|tex2D)\s*\(")
DEPTH_SAMPLE_RE = re.compile(r"\bSampleSceneDepth\s*\(")


def normalize(path: Path) -> str:
    return str(path.relative_to(ROOT)).replace("\\", "/")


def iter_shader_paths(patterns: list[str]) -> list[Path]:
    paths: set[Path] = set()
    for pattern in patterns:
        for raw in glob.glob(str(ROOT / pattern)):
            path = Path(raw).resolve()
            if path.is_file():
                paths.add(path)
    return sorted(paths)


def audit_shader(path: Path, max_samples: int) -> dict:
    source = path.read_text(encoding="utf-8", errors="ignore")
    material_samples = len(MATERIAL_SAMPLE_RE.findall(source))
    depth_samples = len(DEPTH_SAMPLE_RE.findall(source))
    total_samples = material_samples + depth_samples
    return {
        "path": normalize(path),
        "materialTextureSamples": material_samples,
        "depthTextureSamples": depth_samples,
        "totalTextureSamples": total_samples,
        "status": "PASS" if total_samples <= max_samples else "FAIL",
    }


def build_report(spec_path: Path) -> dict:
    spec = json.loads(spec_path.read_text(encoding="utf-8-sig"))
    budget = spec["textureSampleBudget"]
    max_samples = int(budget["maxSamplesPerUiElement"])
    records = [audit_shader(path, max_samples) for path in iter_shader_paths(list(budget["auditGlobs"]))]
    errors = [
        f"{record['path']} has {record['totalTextureSamples']} texture samples"
        for record in records
        if record["status"] != "PASS"
    ]
    return {
        "schema": "hecton8.ui_shader_sample_audit.v1",
        "owner": "UX_ENGINEER",
        "promptId": "HARDWARE_ADAPTIVE_UI_BAKER",
        "status": "PASS" if not errors else "FAIL",
        "maxSamplesPerUiElement": max_samples,
        "shaderCount": len(records),
        "sourceHashes": build_source_hashes(spec_path, records),
        "records": records,
        "errors": errors,
    }


def build_source_hashes(spec_path: Path, records: list[dict]) -> dict[str, str | dict[str, str]]:
    shader_hashes: dict[str, str] = {}
    for record in records:
        shader_path = ROOT / str(record["path"])
        shader_hashes[str(record["path"])] = sha256_file(shader_path)
    return {
        "specSha256": sha256_file(spec_path),
        "scriptSha256": sha256_file(SCRIPT_PATH),
        "shaderSha256": shader_hashes,
    }


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(65536)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit HECTON-8 UI shader texture sample counts.")
    parser.add_argument("--spec", default=str(SPEC_PATH), help="Hardware adaptive UI scaler JSON.")
    parser.add_argument("--write-report", nargs="?", const=str(DEFAULT_REPORT), default="", help="Optional report path.")
    args = parser.parse_args()

    report = build_report(Path(args.spec).resolve())
    print("UI_SHADER_SAMPLE_AUDIT")
    for record in report["records"]:
        print(
            "{path}: material={materialTextureSamples} depth={depthTextureSamples} total={totalTextureSamples} status={status}".format(
                **record
            )
        )

    if args.write_report:
        report_path = Path(args.write_report).resolve()
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(f"report={report_path}")

    if report["errors"]:
        print("STATUS: FAIL")
        for error in report["errors"]:
            print(f"ERROR: {error}")
        return 1

    print("STATUS: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
