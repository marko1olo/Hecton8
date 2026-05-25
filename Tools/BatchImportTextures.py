#!/usr/bin/env python3
"""Generate or apply HECTON-8 texture import settings for generated textures.

Default mode is dry-run. Use --write-meta only after Unity has created .meta files.
The script refuses to invent Unity GUIDs.
"""

from __future__ import annotations

import argparse
import csv
import re
from dataclasses import asdict, dataclass
from pathlib import Path


IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr", ".hdr", ".webp"}


@dataclass
class ImportPlan:
    path: str
    role: str
    srgb: int
    texture_type: int
    mipmaps: int
    read_write: int
    compression: int
    compression_quality: int
    max_size: int
    standalone_format: str
    standalone_texture_format: int
    android_format: str
    android_texture_format: int
    action: str


STANDALONE_BC7 = 25
STANDALONE_BC5 = 27
ANDROID_ASTC_6X6 = 50
TEXTURE_COMPRESSION_COMPRESSED = 1
COMPRESSION_QUALITY_HIGH = 100


def classify_role(path: Path) -> str:
    name = path.stem.lower()
    if name.endswith("_normal") or "_normal_" in name or name.endswith("_nrm"):
        return "Normal"
    if name.endswith("_orm") or "_orm_" in name or name.endswith("_mask") or name.endswith("_roughness") or name.endswith("_metallic"):
        return "ORM"
    if name.endswith("_emissive") or "_emissive_" in name or name.endswith("_emission"):
        return "Emissive"
    return "Albedo"


def settings_for(role: str) -> tuple[int, int, int, int, int, str, int, str, int]:
    if role == "Normal":
        return (
            0,
            1,
            1,
            TEXTURE_COMPRESSION_COMPRESSED,
            2048,
            "BC5",
            STANDALONE_BC5,
            "ASTC_6x6",
            ANDROID_ASTC_6X6,
        )
    if role in {"ORM", "Emissive"}:
        return (
            0,
            0,
            1,
            TEXTURE_COMPRESSION_COMPRESSED,
            2048,
            "BC7",
            STANDALONE_BC7,
            "ASTC_6x6",
            ANDROID_ASTC_6X6,
        )
    return (
        1,
        0,
        1,
        TEXTURE_COMPRESSION_COMPRESSED,
        2048,
        "BC7",
        STANDALONE_BC7,
        "ASTC_6x6",
        ANDROID_ASTC_6X6,
    )


def replace_or_append(text: str, key: str, value: int) -> str:
    pattern = re.compile(rf"(\b{re.escape(key)}:\s*)[-0-9]+")
    if pattern.search(text):
        return pattern.sub(rf"\g<1>{value}", text)
    return text.rstrip() + f"\n  {key}: {value}\n"


def replace_or_append_platform_key(block: str, key: str, value: int) -> str:
    pattern = re.compile(rf"(^    {re.escape(key)}:\s*)[-0-9]+", re.MULTILINE)
    if pattern.search(block):
        return pattern.sub(rf"\g<1>{value}", block)
    return block.rstrip() + f"\n    {key}: {value}\n"


def platform_block(build_target: str, max_size: int, texture_format: int) -> str:
    return (
        "  - serializedVersion: 4\n"
        f"    buildTarget: {build_target}\n"
        f"    maxTextureSize: {max_size}\n"
        "    resizeAlgorithm: 0\n"
        f"    textureFormat: {texture_format}\n"
        f"    textureCompression: {TEXTURE_COMPRESSION_COMPRESSED}\n"
        f"    compressionQuality: {COMPRESSION_QUALITY_HIGH}\n"
        "    crunchedCompression: 0\n"
        "    allowsAlphaSplitting: 0\n"
        "    overridden: 1\n"
        "    ignorePlatformSupport: 0\n"
        "    androidETC2FallbackOverride: 0\n"
        "    forceMaximumCompressionQuality_BC6H_BC7: 1"
    )


def update_platform_block(text: str, build_target: str, max_size: int, texture_format: int) -> str:
    block_pattern = re.compile(
        rf"(  - serializedVersion: 4\n    buildTarget: {re.escape(build_target)}\n)"
        r"(?:(?!\n  - serializedVersion: 4\n    buildTarget:|\n  spriteSheet:|\n  mipmapLimitGroupName:).)*",
        re.DOTALL,
    )
    match = block_pattern.search(text)
    if match:
        block = match.group(0)
        block = replace_or_append_platform_key(block, "maxTextureSize", max_size)
        block = replace_or_append_platform_key(block, "textureFormat", texture_format)
        block = replace_or_append_platform_key(block, "textureCompression", TEXTURE_COMPRESSION_COMPRESSED)
        block = replace_or_append_platform_key(block, "compressionQuality", COMPRESSION_QUALITY_HIGH)
        block = replace_or_append_platform_key(block, "crunchedCompression", 0)
        block = replace_or_append_platform_key(block, "overridden", 1)
        block = replace_or_append_platform_key(block, "forceMaximumCompressionQuality_BC6H_BC7", 1)
        return text[: match.start()] + block + text[match.end() :]

    insertion = platform_block(build_target, max_size, texture_format)
    if "  platformSettings:\n" in text:
        return text.replace("  platformSettings:\n", "  platformSettings:\n" + insertion + "\n", 1)
    return text.rstrip() + "\n  platformSettings:\n" + insertion + "\n"


def update_meta(
    meta_path: Path,
    role: str,
    srgb: int,
    texture_type: int,
    mipmaps: int,
    read_write: int,
    compression: int,
    compression_quality: int,
    max_size: int,
    standalone_format: str,
    standalone_texture_format: int,
    android_format: str,
    android_texture_format: int,
) -> str:
    if not meta_path.exists():
        return "PENDING_UNITY_META"
    text = meta_path.read_text(encoding="utf-8", errors="ignore")
    if "guid:" not in text:
        return "REFUSED_META_WITHOUT_GUID"
    text = replace_or_append(text, "sRGBTexture", srgb)
    text = replace_or_append(text, "textureType", texture_type)
    text = replace_or_append(text, "enableMipMap", mipmaps)
    text = replace_or_append(text, "isReadable", read_write)
    text = replace_or_append(text, "textureCompression", compression)
    text = replace_or_append(text, "compressionQuality", compression_quality)
    text = replace_or_append(text, "maxTextureSize", max_size)
    text = update_platform_block(text, "Standalone", max_size, standalone_texture_format)
    text = update_platform_block(text, "Android", max_size, android_texture_format)
    meta_path.write_text(text, encoding="utf-8")
    return f"UPDATED_{role.upper()}_STANDALONE_{standalone_format}_ANDROID_{android_format}"


def build_plan(import_root: Path, project_root: Path, write_meta: bool) -> list[ImportPlan]:
    plans: list[ImportPlan] = []
    for path in sorted(import_root.rglob("*")):
        if path.suffix.lower() not in IMAGE_EXTS:
            continue
        role = classify_role(path)
        (
            srgb,
            texture_type,
            mipmaps,
            compression,
            max_size,
            standalone_format,
            standalone_texture_format,
            android_format,
            android_texture_format,
        ) = settings_for(role)
        read_write = 0
        meta_path = path.with_name(path.name + ".meta")
        if write_meta:
            action = update_meta(
                meta_path,
                role,
                srgb,
                texture_type,
                mipmaps,
                read_write,
                compression,
                COMPRESSION_QUALITY_HIGH,
                max_size,
                standalone_format,
                standalone_texture_format,
                android_format,
                android_texture_format,
            )
        else:
            action = "DRY_RUN"
            if not meta_path.exists():
                action = "PENDING_UNITY_META"
        plans.append(
            ImportPlan(
                path=path.relative_to(project_root).as_posix(),
                role=role,
                srgb=srgb,
                texture_type=texture_type,
                mipmaps=mipmaps,
                read_write=read_write,
                compression=compression,
                compression_quality=COMPRESSION_QUALITY_HIGH,
                max_size=max_size,
                standalone_format=standalone_format,
                standalone_texture_format=standalone_texture_format,
                android_format=android_format,
                android_texture_format=android_texture_format,
                action=action,
            )
        )
    return plans


def main() -> int:
    parser = argparse.ArgumentParser(description="Prepare generated texture import settings.")
    parser.add_argument("--project-root", default=".", help="Unity project root.")
    parser.add_argument("--import-root", default="Assets/_Project/Art/Textures/Generated/SHINOBU_361", help="Generated texture folder.")
    parser.add_argument("--out", default="Docs/Reports/BatchImportTextures_SHINOBU_361_import_plan.csv", help="CSV import plan output.")
    parser.add_argument("--write-meta", action="store_true", help="Apply targeted changes to existing Unity .meta files. Missing meta files are not created.")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    import_root = (project_root / args.import_root).resolve()
    output_path = (project_root / args.out).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    plans = build_plan(import_root, project_root, args.write_meta) if import_root.exists() else []
    with output_path.open("w", encoding="utf-8", newline="") as handle:
        fieldnames = list(ImportPlan.__dataclass_fields__.keys())
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for plan in plans:
            writer.writerow(asdict(plan))
    print("BATCH_IMPORT_TEXTURES_SHINOBU_361")
    print(f"import_root={import_root.relative_to(project_root).as_posix() if import_root.exists() else args.import_root}")
    print(f"textures={len(plans)}")
    print(f"write_meta={'true' if args.write_meta else 'false'}")
    print(f"plan={output_path.relative_to(project_root).as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
