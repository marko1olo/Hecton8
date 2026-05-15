#!/usr/bin/env python3
"""Regression tests for Tools/VerifyLore.py."""

from __future__ import annotations

import contextlib
import io
import os
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import VerifyLore


class VerifyLoreTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repo_root = Path(__file__).resolve().parents[1]

    def setUp(self) -> None:
        self.previous_cwd = Path.cwd()
        os.chdir(self.repo_root)

    def tearDown(self) -> None:
        os.chdir(self.previous_cwd)

    def make_entry(self, canonical_id: str, payload: bytes) -> VerifyLore.SourceEntry:
        return VerifyLore.SourceEntry(
            source_path=Path(canonical_id),
            canonical_id=canonical_id,
            hash_value=VerifyLore.compute_fnv1a32(canonical_id),
            payload=payload,
        )

    def test_empty_string_hash_matches_h8_data_hash_zero_contract(self) -> None:
        self.assertEqual(VerifyLore.compute_fnv1a32(""), 0)

    def test_bake_extract_and_verify_multiple_markdown_files_in_memory(self) -> None:
        entries = [
            self.make_entry("Docs/Lore/alpha.md", b"# Alpha\nPressure note.\n"),
            self.make_entry("Docs/Lore/sub/beta.md", b"# Beta\nAtlas note.\n"),
        ]

        blob = VerifyLore.bake_blob(entries)
        records = VerifyLore.parse_blob(blob)
        self.assertEqual(len(records), 2)
        self.assertEqual(records, sorted(records, key=lambda record: record.hash_value))

        for record in records:
            self.assertEqual(record.offset % VerifyLore.ALIGNMENT, 0)

        VerifyLore.verify_entries_against_blob(blob, records, entries)
        manifest = VerifyLore.build_manifest_data(
            "Data/Lore/Encyclopedia.h8bin",
            blob,
            records,
            entries,
            "Docs/Lore",
        )
        VerifyLore.verify_manifest_data(blob, records, entries, manifest)
        self.assertEqual(manifest["entry_count"], 2)
        self.assertEqual(manifest["alignment_bytes"], VerifyLore.ALIGNMENT)
        self.assertEqual(len(manifest["entries"]), 2)

    def test_absolute_and_relative_paths_hash_to_same_canonical_id(self) -> None:
        source_path = Path("Docs/Lore/Lore_Bible.md")
        self.assertTrue(source_path.exists())
        relative_canonical = VerifyLore.canonicalize_path(source_path)
        absolute_canonical = VerifyLore.canonicalize_path(source_path.resolve())
        self.assertEqual(relative_canonical, "Docs/Lore/Lore_Bible.md")
        self.assertEqual(relative_canonical, absolute_canonical)
        self.assertEqual(
            VerifyLore.compute_fnv1a32(relative_canonical),
            VerifyLore.compute_fnv1a32(absolute_canonical),
        )

    def test_check_command_is_independent_of_process_cwd(self) -> None:
        os.chdir(self.repo_root / "Tools")
        output = io.StringIO()

        with contextlib.redirect_stdout(output):
            self.assertEqual(VerifyLore.main(["--check"]), 0)

        blob, records = VerifyLore.read_blob(Path("Data/Lore/Encyclopedia.h8bin"))
        self.assertEqual(len(records), 1)
        self.assertGreater(len(blob), 0)
        VerifyLore.verify_manifest(
            Path("Data/Lore/Encyclopedia.h8bin"),
            Path("Data/Lore/Encyclopedia.manifest.json"),
            Path("Docs/Lore"),
        )
        self.assertIn("CHECK OK: entries=1", output.getvalue())
        self.assertIn("blob=Data/Lore/Encyclopedia.h8bin", output.getvalue())
        self.assertEqual(
            VerifyLore.canonicalize_path(Path("Docs/Lore/Lore_Bible.md")),
            "Docs/Lore/Lore_Bible.md",
        )

    def test_cli_rejects_hash_and_source_path_together(self) -> None:
        with contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit) as context:
                VerifyLore.main(["0xD1880394", "--source-path", "Docs/Lore/Lore_Bible.md"])

        self.assertNotEqual(context.exception.code, 0)

    def test_cli_rejects_invalid_hash_without_traceback(self) -> None:
        stderr = io.StringIO()

        with contextlib.redirect_stderr(stderr):
            with self.assertRaises(SystemExit) as context:
                VerifyLore.main(["NOT_A_HASH"])

        self.assertNotEqual(context.exception.code, 0)
        self.assertIn("Invalid hash value", stderr.getvalue())
        self.assertNotIn("Traceback", stderr.getvalue())

    def test_cli_rejects_missing_hash_without_traceback(self) -> None:
        stderr = io.StringIO()

        with contextlib.redirect_stderr(stderr):
            with self.assertRaises(SystemExit) as context:
                VerifyLore.main(["0xFFFFFFFF"])

        self.assertNotEqual(context.exception.code, 0)
        self.assertIn("Hash not found", stderr.getvalue())
        self.assertNotIn("Traceback", stderr.getvalue())

    def test_hash_parser_rejects_out_of_uint32_range_values(self) -> None:
        for value in ("-1", "0x100000000", "4294967296"):
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    VerifyLore.parse_hash(value)

    def test_missing_hash_returns_none(self) -> None:
        record = VerifyLore.LoreRecord(0x10, 32, 4)
        self.assertIsNone(VerifyLore.find_record([record], 0x20))

    def test_bad_magic_blob_is_rejected(self) -> None:
        entries = [self.make_entry("Docs/Lore/entry.md", b"Entry\n")]
        blob = bytearray(VerifyLore.bake_blob(entries))
        blob[0:4] = b"BAD!"

        with self.assertRaises(ValueError):
            VerifyLore.parse_blob(bytes(blob))

    def test_unsorted_record_table_is_rejected(self) -> None:
        entries = [
            self.make_entry("Docs/Lore/alpha.md", b"# Alpha\nPressure note.\n"),
            self.make_entry("Docs/Lore/beta.md", b"# Beta\nAtlas note.\n"),
        ]
        blob = bytearray(VerifyLore.bake_blob(entries))
        records = VerifyLore.parse_blob(blob)
        self.assertEqual(len(records), 2)
        VerifyLore.RECORD_STRUCT.pack_into(
            blob,
            VerifyLore.HEADER_SIZE,
            records[1].hash_value,
            records[1].offset,
            records[1].length,
        )
        VerifyLore.RECORD_STRUCT.pack_into(
            blob,
            VerifyLore.HEADER_SIZE + VerifyLore.RECORD_SIZE,
            records[0].hash_value,
            records[0].offset,
            records[0].length,
        )

        with self.assertRaises(ValueError):
            VerifyLore.parse_blob(bytes(blob))

    def test_overlapping_payload_records_are_rejected(self) -> None:
        entries = [
            self.make_entry("Docs/Lore/alpha.md", b"# Alpha\nPressure note.\n"),
            self.make_entry("Docs/Lore/beta.md", b"# Beta\nAtlas note.\n"),
        ]
        blob = bytearray(VerifyLore.bake_blob(entries))
        records = VerifyLore.parse_blob(blob)
        self.assertEqual(len(records), 2)
        second_record_offset = VerifyLore.HEADER_SIZE + VerifyLore.RECORD_SIZE
        VerifyLore.RECORD_STRUCT.pack_into(
            blob,
            second_record_offset,
            records[1].hash_value,
            records[0].offset,
            records[1].length,
        )

        with self.assertRaises(ValueError):
            VerifyLore.parse_blob(bytes(blob))

    def test_nonzero_payload_padding_is_rejected(self) -> None:
        gap_start = 0
        gap_end = 0
        blob = bytearray()
        for suffix in range(32):
            entries = [
                self.make_entry("Docs/Lore/alpha.md", b"# Alpha\nPressure note.\n" + bytes([65 + suffix]) * suffix),
                self.make_entry("Docs/Lore/beta.md", b"# Beta\nAtlas note.\n"),
            ]
            blob = bytearray(VerifyLore.bake_blob(entries))
            records = sorted(VerifyLore.parse_blob(blob), key=lambda record: record.offset)
            gap_start = records[0].offset + records[0].length
            gap_end = records[1].offset
            if gap_end > gap_start:
                break

        self.assertGreater(gap_end, gap_start)
        blob[gap_start] = 0xFF

        with self.assertRaises(ValueError):
            VerifyLore.parse_blob(bytes(blob))

    def test_trailing_blob_bytes_are_rejected(self) -> None:
        entries = [self.make_entry("Docs/Lore/entry.md", b"Entry\n")]
        blob = VerifyLore.bake_blob(entries) + b"\x00"

        with self.assertRaises(ValueError):
            VerifyLore.parse_blob(blob)

    def test_missing_docs_lore_does_not_fallback_to_design_redirect(self) -> None:
        self.assertTrue(Path("Docs/Design/Lore_Bible.md").exists())
        missing_dir = Path(".__verify_lore_missing_docs_lore_sentinel__")
        self.assertFalse(missing_dir.exists())
        self.assertEqual(VerifyLore.discover_markdown_sources(missing_dir), [])
        with self.assertRaises(ValueError):
            VerifyLore.bake_blob(VerifyLore.load_source_entries(missing_dir))

    def test_source_path_hash_matches_numeric_lookup(self) -> None:
        source_path = Path("Docs/Lore/Lore_Bible.md")
        canonical = VerifyLore.canonicalize_path(source_path)
        source_path_hash = VerifyLore.compute_fnv1a32(canonical)
        entries = VerifyLore.load_source_entries(VerifyLore.PRIMARY_SOURCE_DIR)
        matching = [entry for entry in entries if entry.canonical_id == canonical]
        self.assertEqual(len(matching), 1)
        self.assertEqual(source_path_hash, matching[0].hash_value)

    def test_manifest_sha_mismatch_is_rejected(self) -> None:
        entries = [self.make_entry("Docs/Lore/entry.md", b"Entry\n")]
        blob = VerifyLore.bake_blob(entries)
        records = VerifyLore.parse_blob(blob)
        manifest = VerifyLore.build_manifest_data(
            "Data/Lore/Encyclopedia.h8bin",
            blob,
            records,
            entries,
            "Docs/Lore",
        )
        manifest["entries"][0]["sha256"] = "0" * 64

        with self.assertRaises(ValueError):
            VerifyLore.verify_manifest_data(blob, records, entries, manifest)

    def test_manifest_generation_rejects_blob_source_mismatch(self) -> None:
        entries = [self.make_entry("Docs/Lore/entry.md", b"Entry\n")]
        stale_entries = [self.make_entry("Docs/Lore/entry.md", b"Stale entry\n")]
        blob = VerifyLore.bake_blob(stale_entries)
        records = VerifyLore.parse_blob(blob)

        with self.assertRaises(ValueError):
            VerifyLore.build_manifest_data(
                "Data/Lore/Encyclopedia.h8bin",
                blob,
                records,
                entries,
                "Docs/Lore",
            )

    def test_manifest_verification_rejects_blob_source_mismatch_even_when_manifest_matches_blob(self) -> None:
        entries = [self.make_entry("Docs/Lore/entry.md", b"Entry\n")]
        stale_entries = [self.make_entry("Docs/Lore/entry.md", b"Stale entry\n")]
        blob = VerifyLore.bake_blob(stale_entries)
        records = VerifyLore.parse_blob(blob)
        manifest = VerifyLore.build_manifest_data(
            "Data/Lore/Encyclopedia.h8bin",
            blob,
            records,
            stale_entries,
            "Docs/Lore",
        )

        with self.assertRaises(ValueError):
            VerifyLore.verify_manifest_data(blob, records, entries, manifest)

    def test_manifest_blob_length_mismatch_is_rejected(self) -> None:
        entries = [self.make_entry("Docs/Lore/entry.md", b"Entry\n")]
        blob = VerifyLore.bake_blob(entries)
        records = VerifyLore.parse_blob(blob)
        manifest = VerifyLore.build_manifest_data(
            "Data/Lore/Encyclopedia.h8bin",
            blob,
            records,
            entries,
            "Docs/Lore",
        )
        manifest["blob_length"] = len(blob) + 1

        with self.assertRaises(ValueError):
            VerifyLore.verify_manifest_data(blob, records, entries, manifest)

    def test_manifest_metadata_mismatch_is_rejected(self) -> None:
        entries = [self.make_entry("Docs/Lore/entry.md", b"Entry\n")]
        blob = VerifyLore.bake_blob(entries)
        records = VerifyLore.parse_blob(blob)
        manifest = VerifyLore.build_manifest_data(
            "Data/Lore/Encyclopedia.h8bin",
            blob,
            records,
            entries,
            "Docs/Lore",
        )
        manifest["compression"] = "raw"

        with self.assertRaises(ValueError):
            VerifyLore.verify_manifest_data(blob, records, entries, manifest)

    def test_manifest_blob_label_mismatch_is_rejected_when_expected(self) -> None:
        entries = [self.make_entry("Docs/Lore/entry.md", b"Entry\n")]
        blob = VerifyLore.bake_blob(entries)
        records = VerifyLore.parse_blob(blob)
        manifest = VerifyLore.build_manifest_data(
            "Data/Lore/Stale.h8bin",
            blob,
            records,
            entries,
            "Docs/Lore",
        )

        with self.assertRaises(ValueError):
            VerifyLore.verify_manifest_data(
                blob,
                records,
                entries,
                manifest,
                expected_blob_label="Data/Lore/Encyclopedia.h8bin",
                expected_source_dir_label="Docs/Lore",
            )

    def test_manifest_source_dir_label_mismatch_is_rejected_when_expected(self) -> None:
        entries = [self.make_entry("Docs/Lore/entry.md", b"Entry\n")]
        blob = VerifyLore.bake_blob(entries)
        records = VerifyLore.parse_blob(blob)
        manifest = VerifyLore.build_manifest_data(
            "Data/Lore/Encyclopedia.h8bin",
            blob,
            records,
            entries,
            "Docs/Design",
        )

        with self.assertRaises(ValueError):
            VerifyLore.verify_manifest_data(
                blob,
                records,
                entries,
                manifest,
                expected_blob_label="Data/Lore/Encyclopedia.h8bin",
                expected_source_dir_label="Docs/Lore",
            )

    def test_source_path_outside_repo_is_rejected(self) -> None:
        with self.assertRaises(ValueError):
            VerifyLore.canonicalize_path(Path.cwd().parent)

    def test_current_artifact_verifies_against_current_sources(self) -> None:
        VerifyLore.verify_against_sources(VerifyLore.DEFAULT_BLOB, VerifyLore.PRIMARY_SOURCE_DIR)
        VerifyLore.verify_manifest(
            VerifyLore.DEFAULT_BLOB,
            VerifyLore.DEFAULT_MANIFEST,
            VerifyLore.PRIMARY_SOURCE_DIR,
        )


if __name__ == "__main__":
    unittest.main()
