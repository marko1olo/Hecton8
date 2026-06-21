import sys
import unittest
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent

import importlib.util

def load_tool():
    spec = importlib.util.spec_from_file_location("UpgradeStaticBTreePayloads", ROOT / "UpgradeStaticBTreePayloads.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

tool = load_tool()

class TestUpgradeStaticBTreePayloads(unittest.TestCase):
    def test_align_up(self):
        self.assertEqual(tool.align_up(10, 16), 16)
        self.assertEqual(tool.align_up(16, 16), 16)
        self.assertEqual(tool.align_up(0, 16), 0)
        self.assertEqual(tool.align_up(63, 64), 64)
        self.assertEqual(tool.align_up(64, 64), 64)

    def test_crc32_hecton(self):
        # Known values
        self.assertEqual(tool.crc32_hecton(b"hecton"), 917731327)
        self.assertEqual(tool.crc32_hecton(b"test data"), 3540561586)
        self.assertEqual(tool.crc32_hecton(b""), 0)

    def test_build_and_find_btree(self):
        records = [
            (100, 0),
            (200, 1),
            (300, 2),
            (400, 3),
            (500, 4),
            (600, 5),
            (700, 6),
            (800, 7), # This should force an internal node creation as capacity is BTREE_KEY_CAPACITY = 7
        ]

        # Test offset
        tree_offset = 64
        tree_bytes = tool.build_btree(records, tree_offset)

        # The bytes we pass to find_btree_value simulate the entire blob.
        # tree_bytes needs to be prepended by tree_offset zeros so that slicing inside find_btree_value works correctly.
        blob = bytearray(tree_offset)
        blob.extend(tree_bytes)

        tree_end = tree_offset + len(tree_bytes)
        for hash_val, expected_idx in records:
            resolved_idx = tool.find_btree_value(blob, tree_offset, tree_end, hash_val)
            self.assertEqual(resolved_idx, expected_idx, f"Failed to find {hash_val}")

        # Search for non-existent hash values
        self.assertIsNone(tool.find_btree_value(blob, tree_offset, tree_end, 50))
        self.assertIsNone(tool.find_btree_value(blob, tree_offset, tree_end, 450))
        self.assertIsNone(tool.find_btree_value(blob, tree_offset, tree_end, 900))

    def test_validate_lookup_sorted(self):
        valid = [(1, 0), (5, 1), (10, 2)]
        tool.validate_lookup_sorted(valid) # Should not raise

        invalid_unsorted = [(1, 0), (10, 1), (5, 2)]
        with self.assertRaises(ValueError):
            tool.validate_lookup_sorted(invalid_unsorted)

        invalid_duplicate = [(1, 0), (5, 1), (5, 2)]
        with self.assertRaises(ValueError):
            tool.validate_lookup_sorted(invalid_duplicate)

    def _create_mock_babel_blob(self, entry_count=2, flags=0) -> bytes:
        # BABEL_HEADER = struct.Struct("<IHH6I") -> 4 + 2 + 2 + 6*4 = 32 bytes
        # [0] magic, [1] version, [2] pad, [3] entry_count, [4] index_offset, [5] data_offset, [6] file_len, [7] crc32, [8] flags
        index_offset = 32
        data_offset = index_offset + (entry_count * tool.BABEL_INDEX.size)
        data_offset = tool.align_up(data_offset, 16) # usually aligned

        blob = bytearray(32)

        index_data = bytearray()
        for i in range(entry_count):
            # BABEL_INDEX = struct.Struct("<IIII")
            index_data.extend(tool.BABEL_INDEX.pack(100 + i, data_offset + i * 4, 4, 0))

        blob.extend(index_data)

        # pad to data_offset
        if len(blob) < data_offset:
            blob.extend(b'\0' * (data_offset - len(blob)))

        data = b"DATA" * entry_count
        blob.extend(data)

        file_len = len(blob)
        crc = tool.crc32_hecton(bytes(blob[32:]))

        tool.BABEL_HEADER.pack_into(blob, 0, tool.BABEL_MAGIC, 1, 0, entry_count, index_offset, data_offset, file_len, crc, flags)

        return bytes(blob)

    def _create_mock_static_blob(self, lookup_count=2, flags=0, babel_crc=0) -> bytes:
        # STATIC_HEADER = struct.Struct("<IHHHH13I") -> 4 + 4*2 + 13*4 = 64 bytes
        # [0] magic, [1,2,3,4] h, [5] total_len?, [6] crc32, [7] lookup_count, [8] _, [9] lookup_offset, [10] records_offset, [11] record_bytes, [12] babel_crc32, [13] flags ...
        lookup_offset = 64
        records_offset = lookup_offset + (lookup_count * tool.STATIC_LOOKUP.size)
        records_offset = tool.align_up(records_offset, tool.STATIC_RECORD_ALIGNMENT)

        blob = bytearray(64)

        lookup_data = bytearray()
        record_data = bytearray()
        for i in range(lookup_count):
            # STATIC_LOOKUP = struct.Struct("<IHHq") -> 4 + 2 + 2 + 8 = 16 bytes
            # hash, type, size, offset
            offset = records_offset + len(record_data)
            lookup_data.extend(tool.STATIC_LOOKUP.pack(200 + i, 1, 4, offset))
            record_data.extend(b"REC" + bytes([i]))
            # Align records
            next_offset = tool.align_up(records_offset + len(record_data), tool.STATIC_RECORD_ALIGNMENT)
            record_data.extend(b'\0' * (next_offset - (records_offset + len(record_data))))

        blob.extend(lookup_data)
        if len(blob) < records_offset:
            blob.extend(b'\0' * (records_offset - len(blob)))

        blob.extend(record_data)

        file_len = len(blob)
        record_bytes = len(record_data)
        crc = tool.crc32_hecton(bytes(blob[64:]))

        # packing header
        header = [tool.STATIC_MAGIC, 1, 1, 1, 1, file_len, crc, lookup_count, 0, lookup_offset, records_offset, record_bytes, babel_crc, flags, 0, 0, 0, 0]
        tool.STATIC_HEADER.pack_into(blob, 0, *header)

        return bytes(blob)

    def test_patch_babel(self):
        import tempfile
        import os

        blob = self._create_mock_babel_blob(entry_count=3)
        with tempfile.TemporaryDirectory() as tmpdir:
            tmp_path = Path(tmpdir) / "Babel_Dictionary.h8bin"
            tmp_path.write_bytes(blob)

            # Create a mock manifest
            manifest_path = Path(tmpdir) / "Babel_Dictionary.manifest.json"
            manifest_path.write_text(json.dumps({"hash": "dummy"}), encoding="utf-8")

            # Temporarily monkeypatch BABEL_MANIFEST_PATH in module
            old_manifest = tool.BABEL_MANIFEST_PATH
            tool.BABEL_MANIFEST_PATH = manifest_path

            try:
                result_crc = tool.patch_babel(tmp_path)

                # Check that it patched correctly
                patched_blob = tmp_path.read_bytes()
                self.assertTrue(len(patched_blob) > len(blob))
                header = list(tool.BABEL_HEADER.unpack_from(patched_blob, 0))
                self.assertTrue(header[8] & tool.CACHE_BTREE_FLAG)

                # Call validate
                tool.validate_babel_btree(patched_blob, tuple(header))

                # Double patch should be no-op (just returns crc)
                result_crc2 = tool.patch_babel(tmp_path)
                self.assertEqual(result_crc, result_crc2)
                self.assertEqual(patched_blob, tmp_path.read_bytes())

            finally:
                tool.BABEL_MANIFEST_PATH = old_manifest

    def test_patch_static(self):
        import tempfile
        import os

        blob = self._create_mock_static_blob(lookup_count=3)
        with tempfile.TemporaryDirectory() as tmpdir:
            tmp_path = Path(tmpdir) / "H8StaticData.bin"
            tmp_path.write_bytes(blob)

            manifest_path = Path(tmpdir) / "H8StaticData.manifest.json"
            manifest_path.write_text(json.dumps({"hash": "dummy"}), encoding="utf-8")

            old_manifest = tool.STATIC_MANIFEST_PATH
            tool.STATIC_MANIFEST_PATH = manifest_path

            try:
                babel_crc = 0x12345678
                did_patch = tool.patch_static(tmp_path, babel_crc)
                self.assertTrue(did_patch)

                patched_blob = tmp_path.read_bytes()
                header = list(tool.STATIC_HEADER.unpack_from(patched_blob, 0))
                self.assertTrue(header[13] & tool.CACHE_BTREE_FLAG)
                self.assertEqual(header[12], babel_crc)

                # Call validate
                tool.validate_static_btree(patched_blob, tuple(header))

                # Double patch without crc change should return False (no patch)
                did_patch2 = tool.patch_static(tmp_path, babel_crc)
                self.assertFalse(did_patch2)

                # Double patch with new crc should return True (update crc only)
                new_babel_crc = 0x87654321
                did_patch3 = tool.patch_static(tmp_path, new_babel_crc)
                self.assertTrue(did_patch3)
                patched_blob3 = tmp_path.read_bytes()
                header3 = list(tool.STATIC_HEADER.unpack_from(patched_blob3, 0))
                self.assertEqual(header3[12], new_babel_crc)

            finally:
                tool.STATIC_MANIFEST_PATH = old_manifest

if __name__ == "__main__":
    unittest.main()
