"""Regression tests for the offline ore LCG baker."""

from __future__ import annotations

import csv
import json
import shutil
import struct
import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_ROOT.parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import OreLcgBaker as baker  # noqa: E402


class OreLcgBakerTests(unittest.TestCase):
    def test_lcg_known_vectors_match_existing_contract(self) -> None:
        self.assertEqual(1013904223, baker.next_lcg(0))
        self.assertEqual(1015568748, baker.next_lcg(1))
        self.assertEqual(288599351, baker.next_lcg(0x48454338))
        self.assertEqual(1012239698, baker.next_lcg(0xFFFFFFFF))

    def test_bake_outputs_integer_tables_and_exact_safe_shallows_titanium(self) -> None:
        temp_root = REPO_ROOT / ".codex_tmp" / "OreLcgBakerTests" / "current"
        if temp_root.exists():
            shutil.rmtree(temp_root)
        try:
            target_dir = temp_root / "Data" / "Economy"
            target_dir.mkdir(parents=True)
            shutil.copy2(REPO_ROOT / baker.MATRIX_INPUT, target_dir / "Resource_Distribution_Matrix.csv")

            payload = baker.bake(temp_root, 100_000, baker.DEFAULT_WORLD_SEED)

            validation = payload["validation"]
            self.assertEqual("LCG BAKED", validation["status"])
            self.assertTrue(validation["safe_shallows_titanium_exact_50"])
            self.assertEqual(5000, validation["safe_shallows_titanium_basis_points"])
            self.assertTrue(validation["table_version_hash_matches_schema_id"])
            self.assertEqual(0, validation["fnv_collision_count"])
            self.assertEqual(10, len(payload["density_map_u8"]))
            self.assertEqual(10, len(payload["clumping_factors_u8"]))
            self.assertEqual(150, len(payload["weight_matrix_u8_flat"]))
            self.assertTrue(all(isinstance(value, int) and 0 <= value <= 255 for value in payload["weight_matrix_u8_flat"]))
            self.assertTrue(payload["binary_cache"]["aligned_16_bytes"])
            self.assertEqual("<", payload["binary_cache"]["endian"])
            self.assertEqual(0, payload["binary_cache"]["file_size_bytes"] % 16)
            self.assertEqual(150, payload["binary_cache"]["minimal_weight_matrix_bytes"])
            self.assertEqual(190, payload["binary_cache"]["minimal_lod_payload_bytes"])
            self.assertEqual(192, payload["binary_cache"]["minimal_lod_bytes"])

            json_path = temp_root / baker.JSON_OUTPUT
            json_text = json_path.read_text(encoding="utf-8")
            self.assertNotIn("\n ", json_text)
            self.assertEqual(payload, json.loads(json_text))

            with (temp_root / baker.HISTOGRAM_OUTPUT).open("r", encoding="utf-8", newline="") as handle:
                rows = list(csv.DictReader(handle))
            self.assertEqual(150, len(rows))

            blob = (temp_root / baker.BINARY_OUTPUT).read_bytes()
            self.assertEqual(0, len(blob) % 16)
            header = struct.unpack(baker.BINARY_HEADER_FORMAT, blob[: baker.BINARY_HEADER_SIZE])
            self.assertEqual(baker.BINARY_MAGIC, header[0])
            self.assertEqual(baker.BINARY_VERSION, header[1])
            self.assertEqual(baker.BINARY_HEADER_SIZE, header[2])
            self.assertEqual(0x01020304, header[3])
            self.assertEqual(baker.TABLE_VERSION_HASH32, header[4])
            self.assertEqual(baker.LCG_MULTIPLIER, header[5])
            self.assertEqual(baker.LCG_INCREMENT, header[6])
            self.assertEqual(baker.LCG_MODULUS_BITS, header[7])
            self.assertEqual(10, header[8])
            self.assertEqual(150, header[9])
            self.assertEqual(len(blob), header[14])
            self.assertEqual(0, header[16])

            minimal_offset = header[12]
            ultra_offset = header[13]
            self.assertEqual(payload["binary_cache"]["minimal_lod_bytes"], ultra_offset - minimal_offset)
            density = list(blob[minimal_offset : minimal_offset + header[8]])
            clump_offset = minimal_offset + header[8]
            clump = list(blob[clump_offset : clump_offset + header[8]])
            total_offset = clump_offset + header[8]
            totals = [
                struct.unpack("<H", blob[total_offset + index * 2 : total_offset + index * 2 + 2])[0]
                for index in range(header[8])
            ]
            weight_offset = total_offset + header[8] * 2
            weights = list(blob[weight_offset : weight_offset + header[9]])
            self.assertEqual(payload["density_map_u8"], density)
            self.assertEqual(payload["clumping_factors_u8"], clump)
            self.assertEqual([biome["total_weight_u16"] for biome in payload["biomes"]], totals)
            self.assertEqual(payload["weight_matrix_u8_flat"], weights)
        finally:
            shutil.rmtree(temp_root, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
