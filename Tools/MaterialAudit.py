#!/usr/bin/env python3
"""Offline HECTON-8 material audit.

Checks imported texture names, material texture slots, ORM/detail-map usage, and
albedo luminance for PBR energy-conservation violations.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re
from pathlib import Path
from typing import Any

try:
    from PIL import Image
except ImportError as exc:  # pragma: no cover - environment guard
    raise SystemExit("Pillow is required: python -m pip install pillow") from exc


IMAGE_EXTS = {".png", ".tga", ".jpg", ".jpeg", ".tif", ".tiff", ".exr", ".psd"}
MATERIAL_EXTS = {".mat"}
FIRST_PARTY_ASSET_DIRS = {"_Project"}
DEFAULT_EXCLUDED_DIRS = {
    ".git",
    "Adaptive Performance",
    "AmplifyImpostors",
    "AstarPathfindingProject",
    "Bakery",
    "Candice AI for Games",
    "Crest",
    "Dynamic Decals",
    "Eazy Sound Manager",
    "Feel",
    "GPUInstancer",
    "Graphy - Ultimate Stats Monitor",
    "MapMagic",
    "MeshBaker",
    "Packages",
    "Plugins",
    "RealtimeCSG",
    "Shapes",
    "Technie",
    "TextMesh Pro",
    "VolumetricFogBundle",
    "VolumetricLightBeam",
    "_Archive",
    "_Recovery",
    "_ThirdParty",
}
DETAIL_TOKENS = (
    "detail",
    "scratch",
    "scratches",
    "dust",
    "grime",
    "carbon",
    "fiber",
    "fibre",
    "wear",
    "worn",
    "edge",
    "stain",
    "rust",
    "oxid",
    "dent",
    "pitted",
    "noise",
    "micro",
)
ALBEDO_TOKENS = (
    "albedo",
    "basecolor",
    "base_color",
    "base-color",
    "diffuse",
    "_diff",
    "-diff",
    "diff",
    "_d",
    "-d",
    "color",
    "colour",
)
ALBEDO_EXCLUDE_TOKENS = (
    "normal",
    "norm",
    "nrm",
    "bump",
    "height",
    "rough",
    "metal",
    "ao",
    "orm",
    "mask",
    "spec",
    "smooth",
    "emiss",
    "emission",
    "cloud",
    "lut",
    "noise",
    "blue",
)
ORM_TOKENS = ("orm", "mask", "packed", "ao", "occlusion", "rough", "metal", "smooth", "spec")
NORMAL_TOKENS = ("normal", "norm", "nrm", "bump")
NON_SURFACE_PATH_PARTS = ("/sprites/ui/", "/skyboxes/")

TEXTURE_PROPERTY_RE = re.compile(r"^\s*-\s+([A-Za-z0-9_]+):\s*$")
GUID_RE = re.compile(r"guid:\s*([0-9a-fA-F]{32})")


def normalized(path: Path) -> str:
    return path.as_posix()


def contains_any(text: str, tokens: tuple[str, ...]) -> bool:
    return any(token in text for token in tokens)


def tokenize_name(text: str) -> list[str]:
    return re.findall(r"[a-z0-9]+", text.lower())


def is_surface_excluded_path(path: Path) -> bool:
    lowered = "/" + path.as_posix().lower().replace("\\", "/")
    return any(part in lowered for part in NON_SURFACE_PATH_PARTS)


def has_orm_token(terms: list[str]) -> bool:
    for term in terms:
        if term == "orm" or term.startswith("orm"):
            return True
        if term in {"mask", "packed", "ao", "occlusion", "rough", "roughness"}:
            return True
        if term in {"metal", "metallic", "smooth", "smoothness", "spec", "specular"}:
            return True
    return False


def has_detail_token(terms: list[str]) -> bool:
    return any(term in DETAIL_TOKENS for term in terms)


def classify_texture(path: Path) -> dict[str, bool]:
    name = path.stem.lower()
    terms = tokenize_name(name)
    is_surface_excluded = is_surface_excluded_path(path)
    is_normal = contains_any(name, NORMAL_TOKENS)
    is_orm_candidate = has_orm_token(terms) and not is_normal and not is_surface_excluded
    is_detail_candidate = has_detail_token(terms) and not is_surface_excluded
    is_albedo_candidate = contains_any(name, ALBEDO_TOKENS) and not contains_any(name, ALBEDO_EXCLUDE_TOKENS)
    return {
        "is_albedo_candidate": is_albedo_candidate,
        "is_detail_candidate": is_detail_candidate,
        "is_normal": is_normal,
        "is_orm_candidate": is_orm_candidate,
    }


def srgb_to_linear(channel: float) -> float:
    if channel <= 0.04045:
        return channel / 12.92
    return math.pow((channel + 0.055) / 1.055, 2.4)


def read_meta(path: Path) -> dict[str, str]:
    meta_path = Path(str(path) + ".meta")
    result: dict[str, str] = {}
    if not meta_path.exists():
        return result

    wanted = {
        "aniso",
        "enableMipMap",
        "guid",
        "isReadable",
        "maxTextureSize",
        "sRGBTexture",
        "streamingMipmaps",
        "textureCompression",
        "textureFormat",
        "textureType",
    }
    current_platform = ""
    try:
        with meta_path.open("r", encoding="utf-8", errors="ignore") as handle:
            for line in handle:
                stripped = line.strip()
                if ":" not in stripped:
                    continue
                key, value = stripped.split(":", 1)
                key = key.strip()
                value = value.strip()
                if key == "buildTarget":
                    current_platform = value
                    continue
                if key in wanted:
                    if key not in result:
                        result[key] = value
                    if current_platform:
                        result[f"{current_platform}.{key}"] = value
    except OSError:
        return result
    return result


def meta_value(meta: dict[str, str], key: str) -> str:
    standalone_key = f"Standalone.{key}"
    if standalone_key in meta:
        return meta[standalone_key]
    return meta.get(key, "")


def append_texture_import_issues(record: dict[str, Any]) -> None:
    meta = record.get("meta", {})
    issues: list[str] = []
    if not meta:
        record["import_issues"] = ["MISSING_META"]
        return

    srgb = meta.get("sRGBTexture", "")
    enable_mip = meta.get("enableMipMap", "")
    texture_type = meta.get("textureType", "")
    compression = meta_value(meta, "textureCompression")
    is_readable = meta.get("isReadable", "")

    if is_readable == "1":
        issues.append("READ_WRITE_ENABLED")
    if compression == "0":
        issues.append("UNCOMPRESSED_TEXTURE")

    if record.get("is_albedo_candidate"):
        if srgb != "1":
            issues.append("ALBEDO_SRGB_OFF")
        if enable_mip != "1":
            issues.append("ALBEDO_MIPS_OFF")
    if record.get("is_normal"):
        if srgb != "0":
            issues.append("NORMAL_SRGB_ON")
        if texture_type != "1":
            issues.append("NORMAL_NOT_TEXTURETYPE_NORMAL")
        if enable_mip != "1":
            issues.append("NORMAL_MIPS_OFF")
    if record.get("is_orm_candidate") or record.get("is_detail_candidate"):
        if srgb != "0":
            issues.append("DATA_TEXTURE_SRGB_ON")
        if enable_mip != "1":
            issues.append("DATA_TEXTURE_MIPS_OFF")

    record["import_issues"] = issues


def inspect_image(path: Path, root: Path, sample_size: int) -> dict[str, Any]:
    record: dict[str, Any] = {
        "path": normalized(path.relative_to(root)),
        "extension": path.suffix.lower(),
        "meta": read_meta(path),
    }
    record.update(classify_texture(path))
    append_texture_import_issues(record)
    if not record["is_albedo_candidate"]:
        return record

    try:
        with Image.open(path) as image:
            image.draft("RGB", (sample_size, sample_size))
            record["width"] = image.width
            record["height"] = image.height
            record["mode"] = image.mode
            sample = image.convert("RGB")
            sample.thumbnail((sample_size, sample_size))
            pixels = list(sample.getdata())
    except Exception as exc:  # noqa: BLE001 - audit must continue
        record["read_error"] = str(exc)
        return record

    if not pixels:
        record["energy_status"] = "ERROR_EMPTY_IMAGE"
        return record

    srgb_luma: list[float] = []
    linear_luma_sum = 0.0
    bright_pixels = 0
    for red, green, blue in pixels:
        r = red / 255.0
        g = green / 255.0
        b = blue / 255.0
        luma = (0.2126 * r) + (0.7152 * g) + (0.0722 * b)
        srgb_luma.append(luma)
        if luma > 0.90:
            bright_pixels += 1
        linear_luma_sum += (
            (0.2126 * srgb_to_linear(r))
            + (0.7152 * srgb_to_linear(g))
            + (0.0722 * srgb_to_linear(b))
        )

    srgb_luma.sort()
    count = len(srgb_luma)
    p95_index = min(count - 1, int(count * 0.95))
    mean_srgb = sum(srgb_luma) / count
    mean_linear = linear_luma_sum / count
    p95_srgb = srgb_luma[p95_index]
    bright_ratio = bright_pixels / count

    status = "PASS"
    reason = "within albedo luminance budget"
    if mean_srgb > 0.75 or mean_linear > 0.60:
        status = "FAIL"
        reason = "mean albedo too bright for energy conservation"
    elif p95_srgb > 0.92 and bright_ratio > 0.10:
        status = "WARN"
        reason = "large bright albedo area risks baked-light/spec blowout"

    record["energy_status"] = status
    record["energy_reason"] = reason
    record["mean_srgb_luma"] = round(mean_srgb, 5)
    record["mean_linear_luma"] = round(mean_linear, 5)
    record["p95_srgb_luma"] = round(p95_srgb, 5)
    record["bright_pixel_ratio"] = round(bright_ratio, 5)
    return record


def prune_dirs(root: Path, current: Path, dirnames: list[str], include_third_party: bool) -> None:
    if include_third_party:
        dirnames[:] = [name for name in dirnames if name not in {".git", "__pycache__"}]
        return

    if current == root and root.name == "Assets":
        dirnames[:] = [name for name in dirnames if name in FIRST_PARTY_ASSET_DIRS]
        return

    dirnames[:] = [name for name in dirnames if name not in DEFAULT_EXCLUDED_DIRS]


def build_guid_map(root: Path, needed_guids: set[str], include_third_party: bool) -> dict[str, str]:
    guid_map: dict[str, str] = {}
    if not needed_guids:
        return guid_map

    for dirpath, dirnames, filenames in os.walk(root):
        prune_dirs(root, Path(dirpath), dirnames, include_third_party)
        for filename in filenames:
            if not filename.endswith(".meta"):
                continue
            meta_path = Path(dirpath) / filename
            try:
                with meta_path.open("r", encoding="utf-8", errors="ignore") as handle:
                    first_lines = [next(handle, "") for _ in range(8)]
            except OSError:
                continue
            for line in first_lines:
                if line.startswith("guid:"):
                    guid = line.split(":", 1)[1].strip()
                    if guid not in needed_guids:
                        break
                    asset_path = meta_path.with_suffix("")
                    guid_map[guid] = normalized(asset_path.relative_to(root))
                    break
    return guid_map


def parse_material(path: Path, root: Path) -> dict[str, Any]:
    props: dict[str, str] = {}
    current_prop = ""
    try:
        lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
    except OSError as exc:
        return {"path": normalized(path.relative_to(root)), "read_error": str(exc)}

    for line in lines:
        prop_match = TEXTURE_PROPERTY_RE.match(line)
        if prop_match:
            current_prop = prop_match.group(1)
            continue
        if not current_prop:
            continue
        guid_match = GUID_RE.search(line)
        if guid_match:
            guid = guid_match.group(1)
            if guid != "00000000000000000000000000000000":
                props[current_prop] = guid
            current_prop = ""

    return {
        "path": normalized(path.relative_to(root)),
        "texture_properties": props,
    }


def resolve_material(raw: dict[str, Any], guid_map: dict[str, str]) -> dict[str, Any]:
    props = {
        prop: guid_map.get(guid_or_path, guid_or_path)
        for prop, guid_or_path in raw.get("texture_properties", {}).items()
    }
    prop_names = set(props.keys())
    has_base = bool(prop_names.intersection({"_BaseMap", "_MainTex", "_BaseColorMap"}))
    has_packed = bool(prop_names.intersection({"_ORMMap", "_OrmMap", "_MaskMap", "_MetallicGlossMap"}))
    has_separate_occlusion = "_OcclusionMap" in prop_names
    has_detail = bool(prop_names.intersection({"_DetailAlbedoMap", "_DetailNormalMap", "_DetailMask"}))
    has_normal = bool(prop_names.intersection({"_BumpMap", "_NormalMap"}))

    issues: list[str] = []
    if has_base and not has_packed:
        issues.append("NO_PACKED_ORM_OR_MASK_SLOT")
    if has_separate_occlusion and "_MetallicGlossMap" in prop_names:
        issues.append("SEPARATE_OCCLUSION_AND_METALLIC_MAPS")
    if has_base and not has_detail:
        issues.append("NO_DETAIL_MAP_SLOT")

    return {
        "path": raw["path"],
        "texture_properties": props,
        "has_base_map": has_base,
        "has_normal": has_normal,
        "has_packed_mask": has_packed,
        "has_detail": has_detail,
        "issues": issues,
    }


def summarize_textures(textures: list[dict[str, Any]]) -> dict[str, Any]:
    albedo = [item for item in textures if item.get("is_albedo_candidate")]
    detail = [item for item in textures if item.get("is_detail_candidate")]
    orm = [item for item in textures if item.get("is_orm_candidate")]
    normals = [item for item in textures if item.get("is_normal")]
    energy_fail = [item for item in albedo if item.get("energy_status") == "FAIL"]
    energy_warn = [item for item in albedo if item.get("energy_status") == "WARN"]
    import_issue_textures = [item for item in textures if item.get("import_issues")]
    import_issue_counts: dict[str, int] = {}
    for item in import_issue_textures:
        for issue in item.get("import_issues", []):
            import_issue_counts[issue] = import_issue_counts.get(issue, 0) + 1

    detail_sorted = sorted(
        detail,
        key=lambda item: (
            0 if "detail" in Path(item["path"]).stem.lower() else 1,
            Path(item["path"]).stem.lower(),
        ),
    )

    return {
        "texture_count": len(textures),
        "albedo_candidate_count": len(albedo),
        "normal_candidate_count": len(normals),
        "orm_candidate_count": len(orm),
        "detail_candidate_count": len(detail),
        "energy_fail_count": len(energy_fail),
        "energy_warn_count": len(energy_warn),
        "import_issue_count": len(import_issue_textures),
        "import_issue_counts": import_issue_counts,
        "detail_suggestions": detail_sorted[:10],
        "energy_failures": energy_fail[:50],
        "energy_warnings": energy_warn[:50],
        "import_issue_textures": import_issue_textures[:100],
    }


def summarize_materials(materials: list[dict[str, Any]]) -> dict[str, Any]:
    issue_counts: dict[str, int] = {}
    issue_materials: list[dict[str, Any]] = []
    for material in materials:
        for issue in material.get("issues", []):
            issue_counts[issue] = issue_counts.get(issue, 0) + 1
        if material.get("issues"):
            issue_materials.append(material)

    return {
        "material_count": len(materials),
        "materials_with_packed_mask": sum(1 for item in materials if item.get("has_packed_mask")),
        "materials_with_detail": sum(1 for item in materials if item.get("has_detail")),
        "materials_with_issues": len(issue_materials),
        "issue_counts": issue_counts,
        "issue_materials": issue_materials[:100],
    }


def run_audit(root: Path, sample_size: int, include_third_party: bool) -> dict[str, Any]:
    textures: list[dict[str, Any]] = []
    material_paths: list[Path] = []

    for dirpath, dirnames, filenames in os.walk(root):
        prune_dirs(root, Path(dirpath), dirnames, include_third_party)
        for filename in filenames:
            path = Path(dirpath) / filename
            suffix = path.suffix.lower()
            if suffix in IMAGE_EXTS:
                textures.append(inspect_image(path, root, sample_size))
            elif suffix in MATERIAL_EXTS:
                material_paths.append(path)

    raw_materials = [parse_material(path, root) for path in material_paths]
    needed_guids: set[str] = set()
    for material in raw_materials:
        for guid_or_path in material.get("texture_properties", {}).values():
            if isinstance(guid_or_path, str) and re.fullmatch(r"[0-9a-fA-F]{32}", guid_or_path):
                needed_guids.add(guid_or_path)

    guid_map = build_guid_map(root, needed_guids, include_third_party)
    materials = [resolve_material(material, guid_map) for material in raw_materials]

    return {
        "root": normalized(root),
        "sample_size": sample_size,
        "include_third_party": include_third_party,
        "doctrine": {
            "orm_layout": "R=AO, G=Roughness, B=Metallic",
            "albedo_energy_fail": "mean_srgb_luma > 0.75 or mean_linear_luma > 0.60",
            "albedo_energy_warn": "p95_srgb_luma > 0.92 and bright_pixel_ratio > 0.10",
        },
        "texture_summary": summarize_textures(textures),
        "material_summary": summarize_materials(materials),
    }


def markdown_row(values: list[Any]) -> str:
    escaped = [str(value).replace("|", "\\|").replace("\n", " ") for value in values]
    return "| " + " | ".join(escaped) + " |"


def write_markdown_report(report: dict[str, Any], output: Path) -> None:
    texture_summary = report["texture_summary"]
    material_summary = report["material_summary"]
    lines: list[str] = [
        "# Material Audit - TECHNICAL_ARTIST_DATA",
        "",
        f"Root: `{report['root']}`",
        f"Sample size: `{report['sample_size']}`",
        f"Include third-party: `{report['include_third_party']}`",
        "",
        "## Summary",
        "",
        markdown_row(["Metric", "Value"]),
        markdown_row(["---", "---"]),
        markdown_row(["Textures", texture_summary["texture_count"]]),
        markdown_row(["Albedo candidates", texture_summary["albedo_candidate_count"]]),
        markdown_row(["Albedo energy failures", texture_summary["energy_fail_count"]]),
        markdown_row(["Albedo energy warnings", texture_summary["energy_warn_count"]]),
        markdown_row(["Import issue textures", texture_summary["import_issue_count"]]),
        markdown_row(["ORM candidates", texture_summary["orm_candidate_count"]]),
        markdown_row(["Detail candidates", texture_summary["detail_candidate_count"]]),
        markdown_row(["Materials", material_summary["material_count"]]),
        markdown_row(["Materials with packed mask", material_summary["materials_with_packed_mask"]]),
        markdown_row(["Materials with detail", material_summary["materials_with_detail"]]),
        markdown_row(["Materials with issues", material_summary["materials_with_issues"]]),
        "",
        "## Import Issue Counts",
        "",
    ]

    if texture_summary["import_issue_counts"]:
        lines.extend([markdown_row(["Issue", "Count"]), markdown_row(["---", "---"])])
        for issue, count in sorted(texture_summary["import_issue_counts"].items()):
            lines.append(markdown_row([issue, count]))
    else:
        lines.append("No texture import issues detected by this offline pass.")

    lines.extend(["", "## Material Issue Counts", ""])
    if material_summary["issue_counts"]:
        lines.extend([markdown_row(["Issue", "Count"]), markdown_row(["---", "---"])])
        for issue, count in sorted(material_summary["issue_counts"].items()):
            lines.append(markdown_row([issue, count]))
    else:
        lines.append("No material slot issues detected by this offline pass.")

    lines.extend(["", "## Detail Candidates", ""])
    lines.extend([markdown_row(["Path", "Import issues"]), markdown_row(["---", "---"])])
    for item in texture_summary["detail_suggestions"]:
        lines.append(markdown_row([item["path"], ", ".join(item.get("import_issues", []))]))

    lines.extend(["", "## Texture Import Issues", ""])
    if texture_summary["import_issue_textures"]:
        lines.extend([markdown_row(["Path", "Issues"]), markdown_row(["---", "---"])])
        for item in texture_summary["import_issue_textures"]:
            lines.append(markdown_row([item["path"], ", ".join(item.get("import_issues", []))]))
    else:
        lines.append("No import issues detected.")

    lines.extend(["", "## Material Slot Issues", ""])
    if material_summary["issue_materials"]:
        lines.extend([markdown_row(["Material", "Issues"]), markdown_row(["---", "---"])])
        for item in material_summary["issue_materials"]:
            lines.append(markdown_row([item["path"], ", ".join(item.get("issues", []))]))
    else:
        lines.append("No material slot issues detected.")

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit HECTON-8 surface textures/materials.")
    parser.add_argument("--root", default="Assets", help="Asset root to scan.")
    parser.add_argument("--sample-size", type=int, default=512, help="Max image sample dimension.")
    parser.add_argument(
        "--include-third-party",
        action="store_true",
        help="Include third-party/vendor folders. Slow and not default for owned doctrine.",
    )
    parser.add_argument("--json", help="Optional JSON report path.")
    parser.add_argument("--markdown", help="Optional Markdown report path.")
    parser.add_argument(
        "--fail-on-import-issues",
        action="store_true",
        help="Return non-zero when texture import-setting issues are found.",
    )
    parser.add_argument(
        "--fail-on-material-issues",
        action="store_true",
        help="Return non-zero when material slot issues are found.",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if not root.exists():
        raise SystemExit(f"Root not found: {root}")

    report = run_audit(root, max(16, args.sample_size), args.include_third_party)

    if args.json:
        output = Path(args.json)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    if args.markdown:
        write_markdown_report(report, Path(args.markdown))

    texture_summary = report["texture_summary"]
    material_summary = report["material_summary"]
    print("MATERIAL_AUDIT_SUMMARY")
    print(f"root={report['root']}")
    print(f"textures={texture_summary['texture_count']}")
    print(f"albedo_candidates={texture_summary['albedo_candidate_count']}")
    print(f"energy_failures={texture_summary['energy_fail_count']}")
    print(f"energy_warnings={texture_summary['energy_warn_count']}")
    print(f"import_issue_textures={texture_summary['import_issue_count']}")
    print(f"detail_candidates={texture_summary['detail_candidate_count']}")
    print(f"orm_candidates={texture_summary['orm_candidate_count']}")
    print(f"materials={material_summary['material_count']}")
    print(f"materials_with_packed_mask={material_summary['materials_with_packed_mask']}")
    print(f"materials_with_detail={material_summary['materials_with_detail']}")
    print(f"materials_with_issues={material_summary['materials_with_issues']}")
    if args.json:
        print(f"json={args.json}")
    if args.markdown:
        print(f"markdown={args.markdown}")
    if texture_summary["energy_fail_count"]:
        return 1
    if args.fail_on_import_issues and texture_summary["import_issue_count"]:
        return 2
    if args.fail_on_material_issues and material_summary["materials_with_issues"]:
        return 3
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
