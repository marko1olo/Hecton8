import unittest
import sys
import struct
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODULE_DIR = ROOT / "Data" / "Localization" / "Radio"
sys.path.insert(0, str(MODULE_DIR))

import VerifyMarauderRadio

class TestVerifyMarauderRadio(unittest.TestCase):
    def setUp(self):
        self.raw = []
        self.clean = []
        self.dictionary = {"Characters": [], "Slang": []}
        self.layout = {
            "Header": {
                "Struct": "<4sHH14I",
                "SizeBytes": 64,
            },
            "Record": {
                "Struct": "<32I",
                "SizeBytes": 128,
            }
        }

    def build_valid_header(self, **kwargs):
        defaults = {
            "magic": VerifyMarauderRadio.BINARY_MAGIC,
            "version": 1,
            "header_size": VerifyMarauderRadio.BINARY_HEADER_SIZE,
            "record_count": len(self.raw),
            "record_size": VerifyMarauderRadio.BINARY_RECORD_SIZE,
            "table_offset": VerifyMarauderRadio.BINARY_HEADER_SIZE,
            "payload_offset": VerifyMarauderRadio.BINARY_HEADER_SIZE + (len(self.raw) * VerifyMarauderRadio.BINARY_RECORD_SIZE),
            "payload_length": 0,
            "flags": VerifyMarauderRadio.BINARY_FLAGS,
            "raw_hash": VerifyMarauderRadio.canonical_hash(self.raw),
            "clean_hash": VerifyMarauderRadio.canonical_hash(self.clean),
            "dictionary_hash": VerifyMarauderRadio.canonical_hash(self.dictionary),
            "layout_hash": VerifyMarauderRadio.canonical_hash(self.layout),
            "reserved0": 0,
            "reserved1": 0,
            "reserved2": 0,
            "reserved3": 0,
        }
        defaults.update(kwargs)

        return VerifyMarauderRadio.BINARY_HEADER_STRUCT.pack(
            defaults["magic"], defaults["version"], defaults["header_size"],
            defaults["record_count"], defaults["record_size"],
            defaults["table_offset"], defaults["payload_offset"], defaults["payload_length"],
            defaults["flags"], defaults["raw_hash"], defaults["clean_hash"],
            defaults["dictionary_hash"], defaults["layout_hash"],
            defaults["reserved0"], defaults["reserved1"], defaults["reserved2"], defaults["reserved3"]
        )

    def test_verify_binary_blob_smaller_than_header(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            tmp_path.write_bytes(b"small")

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn("blob smaller than header", result["Errors"])

    def test_verify_binary_magic_mismatch(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header(magic=b"BAD!")
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn(f"magic mismatch {b'BAD!'!r}", result["Errors"])

    def test_verify_binary_version_mismatch(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header(version=2)
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn(f"version mismatch {2}", result["Errors"])

    def test_verify_binary_header_size_mismatch(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header(header_size=99)
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn(f"header size mismatch {99}", result["Errors"])

    def test_verify_binary_record_size_mismatch(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header(record_size=99)
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn(f"record size mismatch {99}", result["Errors"])

    def test_verify_binary_table_offset_mismatch(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header(table_offset=99)
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn(f"table offset mismatch {99}", result["Errors"])

    def test_verify_binary_payload_offset_not_at_end(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header(payload_offset=VerifyMarauderRadio.BINARY_HEADER_SIZE + 16)
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn("payload offset not at record-table end", result["Errors"])

    def test_verify_binary_payload_offset_unaligned(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header(payload_offset=65)
            blob += b'\0' * 16
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn("payload offset not 16-byte aligned", result["Errors"])

    def test_verify_binary_file_length_unaligned(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header()
            blob += b"1"
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn("file length not 16-byte aligned", result["Errors"])

    def test_verify_binary_payload_overflows_file(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header(payload_length=16)
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn("payload range overflows file", result["Errors"])

    def test_verify_binary_flags_mismatch(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header(flags=0)
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn(f"binary flags mismatch {0}", result["Errors"])

    def test_verify_binary_hashes_mismatch(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header(raw_hash=0, clean_hash=0, dictionary_hash=0, layout_hash=0)
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn("raw JSON canonical hash mismatch in header", result["Errors"])
            self.assertIn("clean JSON canonical hash mismatch in header", result["Errors"])
            self.assertIn("dictionary canonical hash mismatch in header", result["Errors"])
            self.assertIn("layout canonical hash mismatch in header", result["Errors"])

    def test_verify_binary_record_count_mismatch(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            blob = self.build_valid_header(record_count=1)
            blob += b'\0' * 128
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, self.layout)
            self.assertIn(f"record count {1} != raw entry count {0}", result["Errors"])

    def test_verify_binary_layout_mismatches(self):
        with tempfile.NamedTemporaryFile() as tmp:
            tmp_path = Path(tmp.name)
            bad_layout = {
                "Header": {"Struct": "<BAD"},
                "Record": {"Struct": "<BAD", "SizeBytes": 0}
            }
            blob = self.build_valid_header(layout_hash=VerifyMarauderRadio.canonical_hash(bad_layout))
            tmp_path.write_bytes(blob)

            result = VerifyMarauderRadio.verify_binary(tmp_path, self.raw, self.clean, self.dictionary, bad_layout)
            self.assertIn("layout header struct is not explicit Little-endian <4sHH14I", result["Errors"])
            self.assertIn("layout record struct is not explicit Little-endian <32I", result["Errors"])
            self.assertIn("layout record size does not match verifier struct", result["Errors"])

if __name__ == '__main__':
    unittest.main()
