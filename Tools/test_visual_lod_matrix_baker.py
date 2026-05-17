#!/usr/bin/env python3
from __future__ import annotations

import json
import hashlib
import shutil
import struct
import tempfile
import unittest
from pathlib import Path

from Tools import VisualLodMatrixBaker as baker

HEADER_FORMAT = "<4sHHIIIIIIIIII16s"
TIER_RECORD_FORMAT = "<32I"
EXTRA_RECORD_FORMAT = "<16I"
HASH_RECORD_FORMAT = "<4I"

HEADER_BYTES = struct.calcsize(HEADER_FORMAT)
TIER_RECORD_BYTES = struct.calcsize(TIER_RECORD_FORMAT)
EXTRA_RECORD_BYTES = struct.calcsize(EXTRA_RECORD_FORMAT)
HASH_RECORD_BYTES = struct.calcsize(HASH_RECORD_FORMAT)


def _fnv1a32(label: str) -> int:
    value = 0x811C9DC5
    for byte in label.lower().encode("ascii"):
        value ^= byte
        value = (value * 0x01000193) & 0xFFFFFFFF
    return value


class VisualLodMatrixBakerTests(unittest.TestCase):
    def test_current_artifacts_are_deterministic(self) -> None:
        manifest = baker.verify_existing(baker.SOURCE_JSON, baker.BINARY_PATH, baker.MANIFEST_PATH)
        self.assertEqual("little", manifest["endianness"])
        self.assertTrue(manifest["fileAligned16"])
        self.assertEqual(0, manifest["fnv1a32"]["collisionCount"])
        self.assertEqual(4, manifest["sections"]["tierRecords"]["count"])
        self.assertEqual(4, manifest["sections"]["extraRecords"]["count"])

    def test_binary_layout_unpacks_from_raw_bytes(self) -> None:
        binary = baker.BINARY_PATH.read_bytes()
        manifest = json.loads(baker.MANIFEST_PATH.read_text(encoding="utf-8"))
        source = baker.load_json(baker.SOURCE_JSON)
        source_sha16 = hashlib.sha256(
            json.dumps(source, sort_keys=True, separators=(",", ":")).encode("utf-8")
        ).digest()[:16]

        self.assertEqual(2048, len(binary))
        self.assertEqual(0, len(binary) % 16)
        self.assertEqual(hashlib.sha256(binary).hexdigest().upper(), manifest["sha256"])

        header = struct.unpack(HEADER_FORMAT, binary[:HEADER_BYTES])
        self.assertEqual(b"H8VG", header[0])
        self.assertEqual((1, 0), header[1:3])
        self.assertEqual(0x01020304, header[3])
        self.assertEqual(4, header[4])
        self.assertEqual(HEADER_BYTES, header[5])
        self.assertEqual(TIER_RECORD_BYTES, header[6])
        self.assertEqual(4, header[7])
        self.assertEqual(HEADER_BYTES + 4 * TIER_RECORD_BYTES, header[8])
        self.assertEqual(EXTRA_RECORD_BYTES, header[9])
        self.assertEqual(manifest["fnv1a32"]["rowCount"], header[10])
        self.assertEqual(HEADER_BYTES + 4 * TIER_RECORD_BYTES + 4 * EXTRA_RECORD_BYTES, header[11])
        self.assertEqual(HASH_RECORD_BYTES, header[12])
        self.assertEqual(source_sha16, header[13])

        first_tier = struct.unpack(TIER_RECORD_FORMAT, binary[header[5] : header[5] + TIER_RECORD_BYTES])
        self.assertEqual(_fnv1a32("TOASTER"), first_tier[0])
        self.assertEqual(60, first_tier[1])
        self.assertEqual(16667, first_tier[2])

        first_extra = struct.unpack(EXTRA_RECORD_FORMAT, binary[header[8] : header[8] + EXTRA_RECORD_BYTES])
        self.assertEqual(_fnv1a32("TOASTER"), first_extra[0])

        hash_rows = manifest["hashRows"]
        self.assertEqual(76, len(hash_rows))
        seen_hash_labels: dict[int, str] = {}
        for ordinal, row in enumerate(hash_rows):
            offset = header[11] + ordinal * HASH_RECORD_BYTES
            hash32, category_id, packed_ordinal, reserved = struct.unpack(
                HASH_RECORD_FORMAT, binary[offset : offset + HASH_RECORD_BYTES]
            )
            normalized_label = row["label"].lower()
            self.assertEqual(_fnv1a32(row["label"]), hash32)
            self.assertEqual(row["categoryId"], category_id)
            self.assertEqual(ordinal, packed_ordinal)
            self.assertEqual(0, reserved)
            self.assertEqual(normalized_label, seen_hash_labels.setdefault(hash32, normalized_label))

    def test_verify_existing_rejects_binary_byte_drift(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            binary_path = Path(tmp_dir) / "Visual_Scalability_Matrix.bin"
            manifest_path = Path(tmp_dir) / "Visual_Scalability_Matrix.manifest.json"
            shutil.copyfile(baker.BINARY_PATH, binary_path)
            shutil.copyfile(baker.MANIFEST_PATH, manifest_path)

            payload = bytearray(binary_path.read_bytes())
            payload[HEADER_BYTES + 4] ^= 0x01
            binary_path.write_bytes(payload)

            with self.assertRaisesRegex(baker.MatrixError, "binary does not match deterministic bake"):
                baker.verify_existing(baker.SOURCE_JSON, binary_path, manifest_path)

    def test_verify_existing_rejects_manifest_drift(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            binary_path = Path(tmp_dir) / "Visual_Scalability_Matrix.bin"
            manifest_path = Path(tmp_dir) / "Visual_Scalability_Matrix.manifest.json"
            shutil.copyfile(baker.BINARY_PATH, binary_path)
            shutil.copyfile(baker.MANIFEST_PATH, manifest_path)

            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["fileBytes"] += 16
            manifest_path.write_text(json.dumps(manifest, indent=2, sort_keys=True), encoding="utf-8")

            with self.assertRaisesRegex(baker.MatrixError, "manifest does not match deterministic bake"):
                baker.verify_existing(baker.SOURCE_JSON, binary_path, manifest_path)

    def test_baker_rejects_non_derived_beer_lambert_values(self) -> None:
        data = baker.load_json(baker.SOURCE_JSON)
        data["physicsDerivation"]["waterOptics"]["tenMeterTransmittanceRgb"][0] = 0.5
        with tempfile.TemporaryDirectory() as tmp_dir:
            source = Path(tmp_dir) / "Visual_Scalability_Matrix.json"
            source.write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaises(baker.MatrixError):
                baker.build_artifacts(source)

    def test_baker_rejects_toaster_expensive_feature_leak(self) -> None:
        data = baker.load_json(baker.SOURCE_JSON)
        toaster = next(tier for tier in data["tiers"] if tier["tier"] == "TOASTER")
        toaster["shaderFeatures"]["bloom"] = True
        with tempfile.TemporaryDirectory() as tmp_dir:
            source = Path(tmp_dir) / "Visual_Scalability_Matrix.json"
            source.write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaises(baker.MatrixError):
                baker.build_artifacts(source)

    def test_baker_rejects_god_mode_underfed_density(self) -> None:
        data = baker.load_json(baker.SOURCE_JSON)
        god = next(tier for tier in data["tiers"] if tier["tier"] == "GOD_MODE")
        god["particles"]["totalBudget"] = 40000
        with tempfile.TemporaryDirectory() as tmp_dir:
            source = Path(tmp_dir) / "Visual_Scalability_Matrix.json"
            source.write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaises(baker.MatrixError):
                baker.build_artifacts(source)


if __name__ == "__main__":
    unittest.main()
