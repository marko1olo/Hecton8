#!/usr/bin/env python3
"""Static import-intent audit for Batch31 local PBR source bakes.

This is offline/source evidence only. It does not import Unity assets, create
meta files, edit materials, or claim visual acceptance. The output is a
machine-readable bridge between generated source packages and the later Unity
texture/material owner.
"""

from __future__ import annotations

import argparse
import csv
import datetime as _dt
import hashlib
import json
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

from PIL import Image, ImageChops, ImageStat


EVIDENCE_CLASS = "STATIC_SOURCE"
EVIDENCE_SCOPE = "STATIC_IMAGE_IMPORT_INTENT"
DEFAULT_INDEX = "Docs/GeneratedAssets/Batch31_LocalPBR/Batch31_LocalPBR_INDEX.json"
DEFAULT_OUT_PREFIX = "Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605"
EXPECTED_SOURCE_ROOT = Path("Docs/GeneratedAssets/Batch31_LocalPBR")

TARGET_MASK_CONTRACT = "_MaskMap ARM R=AO G=Roughness B=Metallic A=Emission/default1"
SOURCE_MRAO_CONTRACT = "Source package calls map MRAO; playbook allows R=Metallic G=Roughness/Smoothness B=AO A=Emission/Wetness"


@dataclass(frozen=True)
class ImportIntent:
    import_role: str
    runtime_import: int
    srgb: int
    color_space: str
    texture_type: str
    standalone_format: str
    android_format: str
    max_size_low: int
    max_size_middle: int
    max_size_high: int
    max_size_ultra: int
    mipmaps: int
    read_write: int
    channel_contract: str


ROLE_INTENTS: dict[str, ImportIntent] = {
    "albedo": ImportIntent(
        import_role="Albedo",
        runtime_import=1,
        srgb=1,
        color_space="sRGB",
        texture_type="Default",
        standalone_format="BC7",
        android_format="ASTC_6x6",
        max_size_low=1024,
        max_size_middle=2048,
        max_size_high=2048,
        max_size_ultra=2048,
        mipmaps=1,
        read_write=0,
        channel_contract="Base color only; no baked lighting; no direct highlights.",
    ),
    "normal": ImportIntent(
        import_role="Normal",
        runtime_import=1,
        srgb=0,
        color_space="Linear",
        texture_type="NormalMap",
        standalone_format="BC5",
        android_format="ASTC_6x6",
        max_size_low=1024,
        max_size_middle=2048,
        max_size_high=2048,
        max_size_ultra=2048,
        mipmaps=1,
        read_write=0,
        channel_contract="Tangent normal, linear, BC5 where supported.",
    ),
    "mrao": ImportIntent(
        import_role="PackedMask",
        runtime_import=0,
        srgb=0,
        color_space="Linear",
        texture_type="Default",
        standalone_format="BC7",
        android_format="ASTC_6x6",
        max_size_low=1024,
        max_size_middle=2048,
        max_size_high=2048,
        max_size_ultra=2048,
        mipmaps=1,
        read_write=0,
        channel_contract=f"BLOCKED_CHANNEL_SEMANTICS: {SOURCE_MRAO_CONTRACT}; target route says {TARGET_MASK_CONTRACT}.",
    ),
    "height": ImportIntent(
        import_role="HeightSource",
        runtime_import=0,
        srgb=0,
        color_space="Linear",
        texture_type="Default",
        standalone_format="BC4_or_BC7_after_shader_owner",
        android_format="ASTC_6x6_after_shader_owner",
        max_size_low=1024,
        max_size_middle=2048,
        max_size_high=2048,
        max_size_ultra=2048,
        mipmaps=1,
        read_write=0,
        channel_contract="Offline source for normal/parallax/wear only until shader route explicitly consumes height.",
    ),
    "source_crop": ImportIntent(
        import_role="SourceReference",
        runtime_import=0,
        srgb=1,
        color_space="sRGB",
        texture_type="ReferenceOnly",
        standalone_format="DO_NOT_IMPORT_AS_RUNTIME_TEXTURE",
        android_format="DO_NOT_IMPORT_AS_RUNTIME_TEXTURE",
        max_size_low=0,
        max_size_middle=0,
        max_size_high=0,
        max_size_ultra=0,
        mipmaps=0,
        read_write=0,
        channel_contract="Reference crop only.",
    ),
    "tile2x2": ImportIntent(
        import_role="TilePreview",
        runtime_import=0,
        srgb=1,
        color_space="sRGB",
        texture_type="ReferenceOnly",
        standalone_format="DO_NOT_IMPORT_AS_RUNTIME_TEXTURE",
        android_format="DO_NOT_IMPORT_AS_RUNTIME_TEXTURE",
        max_size_low=0,
        max_size_middle=0,
        max_size_high=0,
        max_size_ultra=0,
        mipmaps=0,
        read_write=0,
        channel_contract="2x2 seam preview only.",
    ),
    "normal_tile2x2": ImportIntent(
        import_role="NormalTilePreview",
        runtime_import=0,
        srgb=0,
        color_space="Linear",
        texture_type="ReferenceOnly",
        standalone_format="DO_NOT_IMPORT_AS_RUNTIME_TEXTURE",
        android_format="DO_NOT_IMPORT_AS_RUNTIME_TEXTURE",
        max_size_low=0,
        max_size_middle=0,
        max_size_high=0,
        max_size_ultra=0,
        mipmaps=0,
        read_write=0,
        channel_contract="2x2 normal seam preview only.",
    ),
}


@dataclass
class TextureRow:
    package_id: str
    role_key: str
    import_role: str
    path: str
    exists: int
    sha256_expected: str
    sha256_actual: str
    sha256_match: int
    width: int
    height: int
    mode: str
    bands: str
    runtime_import: int
    srgb: int
    color_space: str
    texture_type: str
    standalone_format: str
    android_format: str
    max_size_low: int
    max_size_middle: int
    max_size_high: int
    max_size_ultra: int
    mipmaps: int
    read_write: int
    channel_contract: str
    channel_min: str
    channel_max: str
    channel_mean: str
    channel_range: str
    channel_pair_delta: str
    luminance_mean: float
    luminance_min: int
    luminance_max: int
    luminance_black_pct: float
    luminance_white_pct: float
    verdict: str
    issues: str
    warnings: str


class ImportIntentError(ValueError):
    pass


def rel(path: Path, root: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def resolve_index_output_path(root: Path, rel_path: str) -> Path:
    raw = Path(rel_path)
    if raw.is_absolute():
        raise ImportIntentError(f"absolute output path rejected: {rel_path}")
    candidate = (root / raw).resolve()
    expected_root = (root / EXPECTED_SOURCE_ROOT).resolve()
    try:
        candidate.relative_to(expected_root)
    except ValueError as exc:
        raise ImportIntentError(f"output path outside Batch31 source root rejected: {rel_path}") from exc
    return candidate


def compact_numbers(values: list[float], decimals: int = 3) -> str:
    return ";".join(f"{value:.{decimals}f}" for value in values)


def luminance_clip_stats(image: Image.Image) -> tuple[float, int, int, float, float]:
    gray = image.convert("L")
    stat = ImageStat.Stat(gray)
    extrema = gray.getextrema()
    values = list(gray.getdata())
    count = max(1, len(values))
    black = sum(1 for value in values if value <= 1) * 100.0 / count
    white = sum(1 for value in values if value >= 254) * 100.0 / count
    return round(float(stat.mean[0]), 3), int(extrema[0]), int(extrema[1]), round(black, 3), round(white, 3)


def channel_pair_delta(image: Image.Image) -> str:
    rgba = image.convert("RGBA")
    channels = rgba.split()
    names = ("R", "G", "B", "A")
    deltas: list[str] = []
    for left_index in range(len(channels)):
        for right_index in range(left_index + 1, len(channels)):
            diff = ImageChops.difference(channels[left_index], channels[right_index])
            mean = float(ImageStat.Stat(diff).mean[0])
            deltas.append(f"{names[left_index]}{names[right_index]}={mean:.3f}")
    return ";".join(deltas)


def image_metrics(path: Path) -> dict[str, Any]:
    with Image.open(path) as image:
        image.load()
        rgba = image.convert("RGBA")
        stat = ImageStat.Stat(rgba)
        minima = [float(item[0]) for item in stat.extrema]
        maxima = [float(item[1]) for item in stat.extrema]
        means = [float(item) for item in stat.mean]
        ranges = [maxima[index] - minima[index] for index in range(len(minima))]
        lum_mean, lum_min, lum_max, black_pct, white_pct = luminance_clip_stats(rgba)
        return {
            "width": int(image.width),
            "height": int(image.height),
            "mode": image.mode,
            "bands": "".join(image.getbands()),
            "min": compact_numbers(minima, 1),
            "max": compact_numbers(maxima, 1),
            "mean": compact_numbers(means, 3),
            "range": compact_numbers(ranges, 1),
            "pair_delta": channel_pair_delta(rgba),
            "luminance_mean": lum_mean,
            "luminance_min": lum_min,
            "luminance_max": lum_max,
            "luminance_black_pct": black_pct,
            "luminance_white_pct": white_pct,
            "ranges": ranges,
            "means": means,
        }


def add_role_warnings(role_key: str, metrics: dict[str, Any], issues: list[str], warnings: list[str]) -> None:
    if role_key == "albedo":
        if metrics["luminance_mean"] < 45.0:
            warnings.append("surface_or_shallow_albedo_dark_review")
        if metrics["luminance_black_pct"] > 1.0:
            warnings.append("albedo_black_clip_review")
        if metrics["luminance_white_pct"] > 0.25:
            warnings.append("albedo_white_clip_review")
    elif role_key == "normal":
        means = metrics["means"]
        ranges = metrics["ranges"]
        if len(means) >= 3 and means[2] < 110.0:
            warnings.append("normal_z_channel_low_review")
        if len(ranges) >= 2 and max(ranges[0], ranges[1]) < 8.0:
            warnings.append("normal_xy_nearly_flat_review")
    elif role_key == "mrao":
        issues.append("blocked_channel_semantics_mrao_vs_arm")
        ranges = metrics["ranges"]
        for index, name in enumerate(("R", "G", "B")):
            if index < len(ranges) and ranges[index] <= 2.0:
                warnings.append(f"mrao_{name}_channel_flat_review")
        pair_tokens = dict(
            token.split("=") for token in str(metrics["pair_delta"]).split(";") if "=" in token
        )
        for pair_name in ("RG", "RB", "GB"):
            value = float(pair_tokens.get(pair_name, "999"))
            if value < 1.0:
                warnings.append(f"mrao_{pair_name}_channels_nearly_identical_review")
    elif role_key == "height":
        ranges = metrics["ranges"]
        if ranges and ranges[0] <= 4.0:
            warnings.append("height_source_flat_review")


def row_for_output(root: Path, package: dict[str, Any], role_key: str, rel_path: str) -> TextureRow:
    if role_key not in ROLE_INTENTS:
        raise ImportIntentError(f"unknown output role rejected: {role_key}")
    intent = ROLE_INTENTS[role_key]
    path = resolve_index_output_path(root, rel_path)
    expected = str(package.get("sha256", {}).get(role_key, ""))
    issues: list[str] = []
    warnings: list[str] = []
    actual = ""
    metrics = {
        "width": 0,
        "height": 0,
        "mode": "",
        "bands": "",
        "min": "",
        "max": "",
        "mean": "",
        "range": "",
        "pair_delta": "",
        "luminance_mean": 0.0,
        "luminance_min": 0,
        "luminance_max": 0,
        "luminance_black_pct": 0.0,
        "luminance_white_pct": 0.0,
    }

    exists = path.exists()
    if not exists:
        issues.append("missing_file")
    else:
        actual = sha256(path)
        if expected and actual.lower() != expected.lower():
            issues.append("sha256_mismatch")
        if not expected:
            warnings.append("missing_expected_sha256")
        try:
            metrics = image_metrics(path)
            if metrics["width"] <= 0 or metrics["height"] <= 0:
                issues.append("invalid_image_dimensions")
            add_role_warnings(role_key, metrics, issues, warnings)
        except OSError as exc:
            issues.append(f"image_open_failed:{exc.__class__.__name__}")

    verdict = "BLOCKED" if any(issue.startswith("blocked_channel_semantics") for issue in issues) else ("ERROR" if issues else ("REVIEW" if warnings else "PASS_STATIC"))
    return TextureRow(
        package_id=str(package.get("id", "")),
        role_key=role_key,
        import_role=intent.import_role,
        path=rel_path,
        exists=1 if exists else 0,
        sha256_expected=expected,
        sha256_actual=actual,
        sha256_match=1 if expected and actual and actual.lower() == expected.lower() else 0,
        width=int(metrics["width"]),
        height=int(metrics["height"]),
        mode=str(metrics["mode"]),
        bands=str(metrics["bands"]),
        runtime_import=intent.runtime_import,
        srgb=intent.srgb,
        color_space=intent.color_space,
        texture_type=intent.texture_type,
        standalone_format=intent.standalone_format,
        android_format=intent.android_format,
        max_size_low=intent.max_size_low,
        max_size_middle=intent.max_size_middle,
        max_size_high=intent.max_size_high,
        max_size_ultra=intent.max_size_ultra,
        mipmaps=intent.mipmaps,
        read_write=intent.read_write,
        channel_contract=intent.channel_contract,
        channel_min=str(metrics["min"]),
        channel_max=str(metrics["max"]),
        channel_mean=str(metrics["mean"]),
        channel_range=str(metrics["range"]),
        channel_pair_delta=str(metrics["pair_delta"]),
        luminance_mean=float(metrics["luminance_mean"]),
        luminance_min=int(metrics["luminance_min"]),
        luminance_max=int(metrics["luminance_max"]),
        luminance_black_pct=float(metrics["luminance_black_pct"]),
        luminance_white_pct=float(metrics["luminance_white_pct"]),
        verdict=verdict,
        issues=";".join(issues),
        warnings=";".join(warnings),
    )


def package_dimension_issues(rows: list[TextureRow]) -> list[str]:
    runtime_rows = [row for row in rows if row.runtime_import and row.exists]
    dimensions = {(row.width, row.height) for row in runtime_rows if row.width > 0 and row.height > 0}
    if len(dimensions) <= 1:
        return []
    return [f"runtime_texture_dimension_mismatch:{sorted(dimensions)}"]


def build_report(root: Path, index_path: Path) -> dict[str, Any]:
    index = json.loads(index_path.read_text(encoding="utf-8"))
    rows: list[TextureRow] = []
    package_summaries: list[dict[str, Any]] = []

    for package in index.get("packages", []):
        package_rows = [
            row_for_output(root, package, role_key, rel_path)
            for role_key, rel_path in sorted(package.get("outputs", {}).items())
        ]
        rows.extend(package_rows)

        package_issues: list[str] = []
        package_warnings: list[str] = []
        role_keys = {row.role_key for row in package_rows}
        for required_role in ("albedo", "normal", "mrao"):
            if required_role not in role_keys:
                package_issues.append(f"missing_required_role:{required_role}")
        package_issues.extend(package_dimension_issues(package_rows))
        if any(row.role_key == "mrao" for row in package_rows):
            package_warnings.append("blocked_channel_contract_mrao_vs_arm")
        if bool(package.get("not_unity_imported", False)):
            package_warnings.append("not_unity_imported")
        if bool(package.get("not_visual_acceptance", False)):
            package_warnings.append("not_visual_acceptance")

        blocked_rows = [row for row in package_rows if row.verdict == "BLOCKED"]
        error_rows = [row for row in package_rows if row.verdict == "ERROR"]
        review_rows = [row for row in package_rows if row.verdict == "REVIEW"]
        verdict = "BLOCKED" if blocked_rows else ("ERROR" if package_issues or error_rows else ("REVIEW" if package_warnings or review_rows else "PASS_STATIC"))
        package_summaries.append(
            {
                "id": package.get("id", ""),
                "source": package.get("source", ""),
                "verdict": verdict,
                "rows": len(package_rows),
                "runtimeRows": sum(1 for row in package_rows if row.runtime_import),
                "blockedRows": len(blocked_rows),
                "errorRows": len(error_rows),
                "reviewRows": len(review_rows),
                "issues": package_issues,
                "warnings": package_warnings,
                "requires": package.get("requires", []),
                "note": package.get("note", ""),
            }
        )

    summary = {
        "packages": len(package_summaries),
        "rows": len(rows),
        "runtimeRows": sum(1 for row in rows if row.runtime_import),
        "blockedRows": sum(1 for row in rows if row.verdict == "BLOCKED"),
        "errorRows": sum(1 for row in rows if row.verdict == "ERROR"),
        "reviewRows": sum(1 for row in rows if row.verdict == "REVIEW"),
        "passStaticRows": sum(1 for row in rows if row.verdict == "PASS_STATIC"),
        "channelContractBlockedPackages": sum(
            1 for package in package_summaries if package["verdict"] == "BLOCKED"
        ),
    }
    return {
        "evidenceClass": EVIDENCE_CLASS,
        "evidenceScope": EVIDENCE_SCOPE,
        "generatedAtUtc": _dt.datetime.now(_dt.timezone.utc).isoformat(),
        "sourceIndex": rel(index_path, root),
        "notUnityImported": True,
        "notVisualAcceptance": True,
        "targetMaskContract": TARGET_MASK_CONTRACT,
        "sourceMraoContract": SOURCE_MRAO_CONTRACT,
        "summary": summary,
        "packages": package_summaries,
        "rows": [asdict(row) for row in rows],
    }


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = list(TextureRow.__dataclass_fields__.keys())
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow(row)


def write_json(path: Path, report: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")


def write_markdown(path: Path, report: dict[str, Any]) -> None:
    summary = report["summary"]
    lines = [
        "# Batch31 Local PBR Import Intent",
        "",
        f"Evidence class: `{report['evidenceClass']}`.",
        f"Evidence scope: `{report['evidenceScope']}`.",
        "",
        "Unity was not run. No `Assets` files, `.meta` files, materials, prefabs, scenes, Addressables groups, or project settings were edited.",
        "This artifact is an importer-facing static contract, not visual acceptance and not runtime readiness.",
        "",
        "## Summary",
        "",
        f"- Source index: `{report['sourceIndex']}`",
        f"- Packages: {summary['packages']}",
        f"- Texture rows: {summary['rows']}",
        f"- Runtime import candidate rows: {summary['runtimeRows']}",
        f"- Blocked rows: {summary['blockedRows']}",
        f"- Error rows: {summary['errorRows']}",
        f"- Review rows: {summary['reviewRows']}",
        f"- Static-pass rows: {summary['passStaticRows']}",
        f"- Channel-semantics blocked packages: {summary['channelContractBlockedPackages']}",
        "",
        "## Channel Contract Block",
        "",
        f"- Source package contract: `{report['sourceMraoContract']}`",
        f"- Target route contract: `{report['targetMaskContract']}`",
        "- Required owner decision before Unity promotion: choose shader target and repack or relabel packed masks. Do not import Batch31 `MRAOSource` as `_MaskMap` by name alone.",
        "",
        "## Package Verdicts",
        "",
        "| Verdict | Package | Runtime rows | Blocked rows | Error rows | Review rows | Issues | Warnings |",
        "|---|---|---:|---:|---:|---:|---|---|",
    ]
    for package in report["packages"]:
        issues = ";".join(package["issues"]) if package["issues"] else ""
        warnings = ";".join(package["warnings"]) if package["warnings"] else ""
        lines.append(
            f"| {package['verdict']} | `{package['id']}` | {package['runtimeRows']} | "
            f"{package['blockedRows']} | {package['errorRows']} | {package['reviewRows']} | `{issues}` | `{warnings}` |"
        )

    lines.extend(
        [
            "",
            "## Import Intent",
            "",
            "| Verdict | Package | Role | Size | sRGB | Type | Standalone | Android | Low/Middle/High/Ultra | Path | Warnings | Issues |",
            "|---|---|---|---:|---:|---|---|---|---|---|---|---|",
        ]
    )
    for row in report["rows"]:
        scale = f"{row['max_size_low']}/{row['max_size_middle']}/{row['max_size_high']}/{row['max_size_ultra']}"
        lines.append(
            f"| {row['verdict']} | `{row['package_id']}` | {row['import_role']} | "
            f"{row['width']}x{row['height']} | {row['srgb']} | {row['texture_type']} | "
            f"{row['standalone_format']} | {row['android_format']} | {scale} | "
            f"`{row['path']}` | `{row['warnings']}` | `{row['issues']}` |"
        )

    lines.extend(
        [
            "",
            "## Scalability Consequences",
            "",
            "- Low: runtime candidates are capped at 1024, one packed mask sampler, mipmaps on, read/write off.",
            "- Middle: runtime candidates may use 2048 where route importance justifies memory.",
            "- High: same sampler count; saved time should buy stronger normal/detail/material response, not extra gameplay truth.",
            "- Ultra: richer near-field shader/detail can be layered later, but Batch31 source identity and channel contract must stay stable.",
            "",
            "## Residual Risk",
            "",
            "- Static checksum and image-channel inspection do not prove Unity import settings, material binding, compression quality, route screenshots, memory residency, frame time, or GC.",
            "- MRAO/ARM convention is unresolved in current docs; this is intentionally left as a blocker, not guessed.",
            "",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Build static import-intent artifacts for Batch31 local PBR packages.")
    parser.add_argument("--project-root", default=".", help="Project root.")
    parser.add_argument("--index", default=DEFAULT_INDEX, help="Batch31 index JSON path, relative to project root.")
    parser.add_argument("--out-prefix", default=DEFAULT_OUT_PREFIX, help="Output path prefix without extension.")
    parser.add_argument("--fail-on-error", action="store_true", help="Return non-zero if any error row or package issue exists.")
    args = parser.parse_args()

    root = Path(args.project_root).resolve()
    index_path = (root / args.index).resolve()
    out_prefix = root / args.out_prefix

    report = build_report(root, index_path)
    write_csv(out_prefix.with_suffix(".csv"), report["rows"])
    write_json(out_prefix.with_suffix(".json"), report)
    write_markdown(out_prefix.with_suffix(".md"), report)

    summary = report["summary"]
    print(
        "BATCH31_LOCAL_PBR_IMPORT_INTENT "
        f"packages={summary['packages']} rows={summary['rows']} "
        f"blocked={summary['blockedRows']} errors={summary['errorRows']} review={summary['reviewRows']} "
        f"channel_blocked={summary['channelContractBlockedPackages']} "
        f"out={rel(out_prefix, root)}"
    )
    has_package_issue = any(package["issues"] for package in report["packages"])
    if args.fail_on_error and (summary["blockedRows"] > 0 or summary["errorRows"] > 0 or has_package_issue):
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
