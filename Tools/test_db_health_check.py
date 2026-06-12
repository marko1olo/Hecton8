import importlib.util
import math
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "Tools" / "DbHealthCheck.py"


def load_db_health_check():
    spec = importlib.util.spec_from_file_location("db_health_check", MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class DbHealthCheckTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tool = load_db_health_check()

    def test_alignment_constants_match_h8db_v1_layout(self):
        self.assertEqual(4096, self.tool.NODE_ALIGNMENT_BYTES)
        self.assertEqual(16, self.tool.PAYLOAD_ALIGNMENT_BYTES)
        self.assertEqual(4096, self.tool.NODE_SIZE_BYTES)
        self.assertEqual(169, self.tool.NODE_MAX_KEYS)
        self.assertEqual(4080, self.tool.NODE_COMPUTED_BYTES)
        self.assertEqual(16, self.tool.NODE_PADDING_BYTES)


    def test_create_dummy_h8db_writes_structurally_correct_file(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "structural.h8db"
            self.tool.create_dummy_h8db(path)

            self.assertTrue(path.exists())

            data = path.read_bytes()

            # Basic Header Checks
            self.assertEqual(self.tool.FILE_MAGIC, self.tool.read_u32(data, self.tool.HEADER_MAGIC_OFFSET))
            self.assertEqual(self.tool.VERSION, self.tool.read_i32(data, self.tool.HEADER_VERSION_OFFSET))
            self.assertEqual(self.tool.HEADER_SIZE_BYTES, self.tool.read_i32(data, self.tool.HEADER_SIZE_OFFSET))
            self.assertEqual(self.tool.NODE_SIZE_BYTES, self.tool.read_i32(data, self.tool.HEADER_NODE_SIZE_OFFSET))
            self.assertEqual(self.tool.HEADER_SIZE_BYTES, self.tool.read_i64(data, self.tool.HEADER_ROOT_NODE_OFFSET))
            self.assertEqual(512, self.tool.read_i32(data, self.tool.HEADER_SECTOR_SIZE_OFFSET))

            # Check Root Node
            root_offset = self.tool.read_i64(data, self.tool.HEADER_ROOT_NODE_OFFSET)
            self.assertEqual(1, self.tool.read_u16(data, root_offset + self.tool.NODE_KEY_COUNT_OFFSET))
            self.assertEqual(1, data[root_offset + self.tool.NODE_IS_LEAF_OFFSET])

            # Check payloads
            live_hash = self.tool.compute_sector_hash(12, -3, 44)
            old_hash = self.tool.compute_sector_hash(11, -3, 44)

            append = self.tool.HEADER_SIZE_BYTES + self.tool.NODE_SIZE_BYTES
            dead_offset = self.tool.align_up(append, self.tool.PAYLOAD_ALIGNMENT_BYTES)

            self.assertEqual(self.tool.PAYLOAD_MAGIC, self.tool.read_u32(data, dead_offset + self.tool.PAYLOAD_MAGIC_OFFSET))
            self.assertEqual(old_hash, self.tool.read_u64(data, dead_offset + self.tool.PAYLOAD_HASH_OFFSET))
            self.assertEqual(3584, self.tool.read_i32(data, dead_offset + self.tool.PAYLOAD_BYTES_OFFSET))

            dead_record_bytes = self.tool.align_up(self.tool.PAYLOAD_HEADER_SIZE_BYTES + 3584, self.tool.PAYLOAD_ALIGNMENT_BYTES)
            live_offset = dead_offset + dead_record_bytes

            self.assertEqual(self.tool.PAYLOAD_MAGIC, self.tool.read_u32(data, live_offset + self.tool.PAYLOAD_MAGIC_OFFSET))
            self.assertEqual(live_hash, self.tool.read_u64(data, live_offset + self.tool.PAYLOAD_HASH_OFFSET))
            self.assertEqual(1536, self.tool.read_i32(data, live_offset + self.tool.PAYLOAD_BYTES_OFFSET))

    def test_dummy_audit_reports_expected_fragmentation(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "dummy.h8db"
            self.tool.create_dummy_h8db(path)
            result = self.tool.audit_h8db(path)

        self.assertEqual(16384, result["file_bytes"])
        self.assertEqual(13376, result["append_offset"])
        self.assertEqual(3616, result["dead_bytes"])
        self.assertEqual(1, result["nodes"])
        self.assertEqual(1, result["keys"])
        self.assertEqual(1, result["live_payloads"])
        self.assertEqual(16, result["node_padding_bytes"])
        self.assertEqual(0, result["unaligned_payloads"])
        expected_fragmentation = (3616 / 13376) * 100.0
        self.assertTrue(math.isclose(expected_fragmentation, result["fragmentation_percent"], rel_tol=0.0, abs_tol=1e-12))

    def test_corrupt_header_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "corrupt.h8db"
            self.tool.create_dummy_h8db(path)
            data = bytearray(path.read_bytes())
            data[self.tool.HEADER_MAGIC_OFFSET:self.tool.HEADER_MAGIC_OFFSET + 4] = b"BAD!"
            path.write_bytes(data)

            with self.assertRaisesRegex(ValueError, "bad file magic"):
                self.tool.audit_h8db(path)

    def test_payload_record_past_append_offset_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "past_append.h8db"
            self.tool.create_dummy_h8db(path)
            data = bytearray(path.read_bytes())
            self.tool.write_i64(data, self.tool.HEADER_APPEND_OFFSET, 8192)
            path.write_bytes(data)

            with self.assertRaisesRegex(ValueError, "outside append range"):
                self.tool.audit_h8db(path)

    def test_payload_length_crossing_append_offset_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "cross_append.h8db"
            self.tool.create_dummy_h8db(path)
            data = bytearray(path.read_bytes())
            self.tool.write_i64(data, self.tool.HEADER_APPEND_OFFSET, 13360)
            path.write_bytes(data)

            with self.assertRaisesRegex(ValueError, "invalid byte length"):
                self.tool.audit_h8db(path)

    def test_hash_audit_probability_formula_is_stable(self):
        result = self.tool.run_hash_collision_test_python(1024, 0x48384442)
        expected_pairs = (1024 * 1023) / (2.0 * 2.0**64)

        self.assertEqual(1024, result["samples"])
        self.assertEqual(0x48384442, result["seed"])
        self.assertEqual(0, result["observed_collisions"])
        self.assertEqual(0, result["duplicate_coordinates_skipped"])
        self.assertTrue(math.isclose(expected_pairs, result["expected_collision_pairs"], rel_tol=0.0, abs_tol=1e-30))
        self.assertTrue(math.isclose(1.0 - math.exp(-expected_pairs), result["probability_any_collision"], rel_tol=0.0, abs_tol=1e-30))

    def test_sector_hash_changes_with_neighbor_coordinate(self):
        center = self.tool.compute_sector_hash(12, -3, 44)
        neighbor = self.tool.compute_sector_hash(13, -3, 44)

        self.assertNotEqual(0, center)
        self.assertNotEqual(center, neighbor)


if __name__ == "__main__":
    unittest.main()
