#!/usr/bin/env python3
"""Verify the HECTON-8 Sabine acoustic LUT and SHINOBU ingest contract."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from collections import Counter
from pathlib import Path
from typing import Iterable

import numpy as np

import SabineBaker


ROOT_DIR = Path(__file__).resolve().parents[1]
DEFAULT_BINARY_PATH = ROOT_DIR / "Data" / "Audio" / "Acoustic_LUT.bin"
DEFAULT_MANIFEST_PATH = ROOT_DIR / "Data" / "Audio" / "Acoustic_LUT.manifest.json"
DEFAULT_ATLAS_PATH = ROOT_DIR / "Docs" / "PROJECT_ATLAS.md"
STATUS_OK = "SABINE_LUT_VERIFIED"


def load_manifest(path: Path) -> dict[str, object]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise ValueError("manifest root must be an object")
    return payload


def verify_binary(binary_path: Path) -> list[str]:
    file_bytes = binary_path.read_bytes()
    pairs = SabineBaker.read_lut_pairs(binary_path)
    rt60 = pairs[:, :, 0]
    damping = pairs[:, :, 1]
    if len(file_bytes) != SabineBaker.EXPECTED_FILE_BYTES:
        raise ValueError("binary byte size drift")
    if len(file_bytes) % SabineBaker.SIMD_GROUP_BYTES != 0:
        raise ValueError("binary blob is not 16-byte aligned")
    if SabineBaker.RECORD_STRUCT.size != SabineBaker.RECORD_BYTES:
        raise ValueError("<ff record size drift")
    if SabineBaker.SIMD_GROUP_STRUCT.size != SabineBaker.SIMD_GROUP_BYTES:
        raise ValueError("<ffff SIMD group size drift")
    if not np.isfinite(pairs).all():
        raise ValueError("non-finite LUT value")
    if float(rt60.max()) > SabineBaker.RT60_MAX_SECONDS:
        raise ValueError("RT60 clamp exceeded")
    if float(damping.min()) < 0.0 or float(damping.max()) > 1.0:
        raise ValueError("damping outside 0..1")

    generated = SabineBaker.build_lut_pairs()
    first_expected = struct.pack(
        SabineBaker.PACK_FORMAT,
        float(generated[0, 0, 0]),
        float(generated[0, 0, 1]),
    )
    if file_bytes[: SabineBaker.RECORD_BYTES] != first_expected:
        raise ValueError("little-endian <ff sentinel mismatch")
    if file_bytes[: SabineBaker.RECORD_BYTES] == struct.pack(
        ">ff",
        float(generated[0, 0, 0]),
        float(generated[0, 0, 1]),
    ):
        raise ValueError("big-endian sentinel collision")

    for volume_index, absorption_index in SabineBaker.VERIFY_SAMPLES:
        record_index = (volume_index * SabineBaker.ABSORPTION_COUNT) + absorption_index
        byte_offset = record_index * SabineBaker.RECORD_BYTES
        rt60_value, damping_value = SabineBaker.RECORD_STRUCT.unpack_from(file_bytes, byte_offset)
        if rt60_value != float(generated[volume_index, absorption_index, 0]):
            raise ValueError(f"RT60 sample mismatch at {volume_index},{absorption_index}")
        if damping_value != float(generated[volume_index, absorption_index, 1]):
            raise ValueError(f"damping sample mismatch at {volume_index},{absorption_index}")

    for byte_offset in (0, 16, len(file_bytes) - SabineBaker.SIMD_GROUP_BYTES):
        grouped = SabineBaker.SIMD_GROUP_STRUCT.unpack_from(file_bytes, byte_offset)
        first_pair = SabineBaker.RECORD_STRUCT.unpack_from(file_bytes, byte_offset)
        second_pair = SabineBaker.RECORD_STRUCT.unpack_from(file_bytes, byte_offset + SabineBaker.RECORD_BYTES)
        if grouped != (first_pair[0], first_pair[1], second_pair[0], second_pair[1]):
            raise ValueError(f"SIMD pair group mismatch at byte {byte_offset}")

    return [
        f"binaryBytes={len(file_bytes)}",
        f"recordFormat={SabineBaker.PACK_FORMAT}",
        f"simdGroupFormat={SabineBaker.SIMD_GROUP_FORMAT}",
        f"rt60Range={float(rt60.min()):.8f}..{float(rt60.max()):.8f}",
        f"dampingRange={float(damping.min()):.8f}..{float(damping.max()):.8f}",
    ]


def verify_material_presets(manifest: dict[str, object]) -> list[str]:
    if manifest["materialProfileSource"] != SabineBaker.DEFAULT_MATERIAL_PROFILE_PATH.relative_to(SabineBaker.ROOT_DIR).as_posix():
        raise ValueError("manifest material profile source mismatch")
    material_presets = manifest["materialPresets"]
    if not isinstance(material_presets, list) or len(material_presets) != len(SabineBaker.MATERIAL_PRESETS):
        raise ValueError("manifest material preset count mismatch")
    expected_presets = {preset.name: preset for preset in SabineBaker.MATERIAL_PRESETS}
    for row in material_presets:
        if not isinstance(row, dict):
            raise ValueError("manifest material preset rows must be objects")
        name = str(row.get("name", ""))
        preset = expected_presets.get(name)
        if preset is None:
            raise ValueError(f"unexpected material preset: {name}")
        if abs(float(row.get("alpha", -1.0)) - preset.alpha) > 0.000001:
            raise ValueError(f"material alpha mismatch for {name}")
        for key in ("profileId", "sourceFile", "sourceKey", "provenance"):
            if not str(row.get(key, "")):
                raise ValueError(f"material preset missing {key}: {name}")
        if row["profileId"] != preset.profile_id or row["sourceFile"] != preset.source_file:
            raise ValueError(f"material preset provenance mismatch for {name}")
    return [f"materialPresetSource={manifest['materialProfileSource']}"]


def verify_manifest(manifest_path: Path, binary_path: Path) -> list[str]:
    manifest = load_manifest(manifest_path)
    required = {
        "status",
        "endianness",
        "recordFormat",
        "simdGroupFormat",
        "fileBytes",
        "fileAlignmentBytes",
        "physics",
        "constantProvenance",
        "materialProfileSource",
        "materialPresets",
        "mockRoomContract",
        "qualityTiers",
        "hashes",
        "runtimeContract",
    }
    missing = required - set(manifest)
    if missing:
        raise ValueError(f"manifest missing keys: {sorted(missing)}")
    if manifest["status"] != SabineBaker.STATUS_OK:
        raise ValueError("manifest status mismatch")
    if manifest["endianness"] != "little" or manifest["recordFormat"] != SabineBaker.PACK_FORMAT:
        raise ValueError("manifest endian/record contract mismatch")
    if int(manifest["fileBytes"]) != SabineBaker.EXPECTED_FILE_BYTES:
        raise ValueError("manifest file byte count mismatch")
    if int(manifest["fileAlignmentBytes"]) != SabineBaker.SIMD_GROUP_BYTES:
        raise ValueError("manifest alignment mismatch")
    if manifest["sha256"] != hashlib.sha256(binary_path.read_bytes()).hexdigest().upper():
        raise ValueError("manifest SHA256 mismatch")

    physics = manifest["physics"]
    if not isinstance(physics, dict):
        raise ValueError("manifest physics must be object")
    for key in (
        "waterAbsorption",
        "beerLambert",
        "pressure",
        "pressureCorrection",
        "thorpAbsorptionDbPerKm",
        "thorpEquation",
        "thorpCoefficients",
    ):
        if key not in physics:
            raise ValueError(f"manifest physics missing {key}")

    provenance = manifest["constantProvenance"]
    if not isinstance(provenance, dict):
        raise ValueError("manifest constantProvenance must be object")
    for key in (
        "sabineCoefficient",
        "volumeAxis",
        "absorptionAxis",
        "equivalentAbsorptionEpsilon",
        "hydrostaticPressure",
        "seawaterConstants",
        "thorpAbsorption",
        "mockRoom",
    ):
        row = provenance.get(key)
        if not isinstance(row, dict) or not str(row.get("source", "")) or not str(row.get("formula", "")):
            raise ValueError(f"manifest constant provenance incomplete: {key}")

    material_report = verify_material_presets(manifest)

    mock_room = manifest["mockRoomContract"]
    if not isinstance(mock_room, dict):
        raise ValueError("manifest mockRoomContract must be object")
    if abs(float(mock_room.get("pressureBar", -1.0)) - SabineBaker.MOCK_PRESSURE_BAR) > 0.000001:
        raise ValueError("manifest mock pressure is not derived from baker constants")

    tiers = manifest["qualityTiers"]
    if not isinstance(tiers, list) or len(tiers) != 4:
        raise ValueError("manifest quality tier count mismatch")
    tier_ids = {str(row.get("id", "")) for row in tiers if isinstance(row, dict)}
    if tier_ids != {"toaster_i3", "middle", "high", "rtx_overkill"}:
        raise ValueError(f"manifest tier ids mismatch: {sorted(tier_ids)}")
    for row in tiers:
        if not isinstance(row, dict):
            raise ValueError("manifest quality tier rows must be objects")
        extra_data = row.get("extraData")
        if not isinstance(extra_data, dict):
            raise ValueError(f"quality tier missing extraData: {row.get('id')}")
        for key in ("gradientBands", "harmonicNoiseOctaves", "convolutionTail", "dirtyResonanceLayers"):
            if key not in extra_data:
                raise ValueError(f"quality tier extraData missing {key}: {row.get('id')}")
    rtx_tier = next(row for row in tiers if isinstance(row, dict) and row.get("id") == "rtx_overkill")
    rtx_extra = rtx_tier["extraData"]
    if int(rtx_extra["gradientBands"]) < 16 or int(rtx_extra["harmonicNoiseOctaves"]) < 6:
        raise ValueError("rtx_overkill extraData is under-specified")

    hashes = manifest["hashes"]
    if not isinstance(hashes, list):
        raise ValueError("manifest hashes must be a list")
    hash_values: list[int] = []
    ids: list[str] = []
    for row in hashes:
        if not isinstance(row, dict):
            raise ValueError("manifest hash rows must be objects")
        semantic_id = str(row.get("id", ""))
        hash_value = int(row.get("fnv1a32", -1))
        if SabineBaker.fnv1a_ascii_lower(semantic_id) != hash_value:
            raise ValueError(f"FNV mismatch for {semantic_id}")
        ids.append(semantic_id)
        hash_values.append(hash_value)
    duplicate_ids = [value for value, count in Counter(ids).items() if count > 1]
    duplicate_hashes = [value for value, count in Counter(hash_values).items() if count > 1]
    if duplicate_ids or duplicate_hashes:
        raise ValueError(f"FNV collision or duplicate id: ids={duplicate_ids} hashes={duplicate_hashes}")

    return [
        f"manifest={manifest_path.relative_to(ROOT_DIR).as_posix()}",
        f"fnvIds={len(ids)}",
        "fnvCollisions=0",
        *material_report,
        f"constantProvenance={len(provenance)}",
        f"tiers={','.join(sorted(tier_ids))}",
    ]


def verify_atlas(atlas_path: Path) -> list[str]:
    text = atlas_path.read_text(encoding="utf-8", errors="replace")
    required_tokens = (
        "Audio",
        "Hecton8.Audio.Propagation",
        "Hecton8.Audio.Synthesis",
        "Hecton8.Audio.Echolocation",
        "DSP/presentation consumers; no gameplay authority",
    )
    missing = [token for token in required_tokens if token not in text]
    if missing:
        raise ValueError(f"PROJECT_ATLAS audio boundary mismatch: {missing}")
    return ["atlasFamily=Audio", "dataSovereignty=stateless_binary_lookup"]


def verify_no_old_magic(source_path: Path) -> list[str]:
    source = source_path.read_text(encoding="utf-8", errors="replace")
    forbidden = (
        "pressure01 " + "* 0.62",
        "absorption01 " + "* 0.28",
        "151" + ".0",
        "MOCK_PRESSURE_BAR = 51" + ".0",
        'MaterialPreset("Rock", ' + "0.22)",
        'MaterialPreset("Metal", ' + "0.10)",
    )
    hits = [needle for needle in forbidden if needle in source]
    if hits:
        raise ValueError(f"old authored damping constants remain: {hits}")
    required = ("thorp_absorption_db_per_km", "beer_lambert_retention", "hydrostatic_pressure_pa")
    missing = [needle for needle in required if needle not in source]
    if missing:
        raise ValueError(f"derived damping functions missing: {missing}")
    return ["mathAudit=Sabine+Thorp+BeerLambert+HydrostaticPressure"]


def parse_args(argv: Iterable[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Verify HECTON-8 Sabine acoustic LUT data.")
    parser.add_argument("--binary", type=Path, default=DEFAULT_BINARY_PATH)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST_PATH)
    parser.add_argument("--atlas", type=Path, default=DEFAULT_ATLAS_PATH)
    return parser.parse_args(list(argv))


def main(argv: Iterable[str]) -> int:
    args = parse_args(argv)
    report: list[str] = []
    report.extend(verify_binary(args.binary))
    report.extend(verify_manifest(args.manifest, args.binary))
    report.extend(verify_atlas(args.atlas))
    report.extend(verify_no_old_magic(ROOT_DIR / "Tools" / "SabineBaker.py"))
    for line in report:
        print(f"VERIFY_SABINE: {line}")
    print(f"STATUS: {STATUS_OK}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main(sys.argv[1:]))
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
