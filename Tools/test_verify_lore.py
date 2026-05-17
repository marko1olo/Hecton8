#!/usr/bin/env python3
"""Regression tests for the raw H8LR lore packer/verifier."""

from __future__ import annotations

import contextlib
import io
import json
import os
import shutil
import sys
import unittest
import uuid
from contextlib import contextmanager
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent))
import LorePacker  # noqa: E402
import VerifyLore  # noqa: E402


TEMP_ROOT = Path(r"C:\Users\User\.codex\memories\h8_lore_tests")


@contextmanager
def owned_temp_dir():
    path = TEMP_ROOT / uuid.uuid4().hex
    path.mkdir(parents=True, exist_ok=False)
    try:
        yield path
    finally:
        shutil.rmtree(path, ignore_errors=True)


class LorePackerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repo_root = Path(__file__).resolve().parents[1]

    def setUp(self) -> None:
        self.previous_cwd = Path.cwd()
        os.chdir(self.repo_root)

    def tearDown(self) -> None:
        os.chdir(self.previous_cwd)

    def make_entry(self, lore_id: str, payload: bytes) -> LorePacker.SourceEntry:
        return LorePacker.SourceEntry(
            source_path=Path(f"Docs/Lore/{lore_id}.md"),
            lore_id=lore_id,
            hash_value=LorePacker.compute_fnv1a32(lore_id),
            payload=payload,
        )

    def test_blob_uses_prompt_layout_and_raw_payloads(self) -> None:
        entries = [
            self.make_entry("Lore_Bible", b"# Lore\npressure hull abyss\n"),
            self.make_entry("DeepReach_ColonyFailureArchive", b"# Archive\nrust relay fault\n"),
        ]

        blob, records = LorePacker.bake_blob(sorted(entries, key=lambda row: row.hash_value))

        self.assertEqual(16, LorePacker.HEADER_STRUCT.size)
        self.assertEqual(16, LorePacker.RECORD_STRUCT.size)
        self.assertEqual(b"H8LR", blob[:4])
        self.assertEqual(0, len(blob) % 16)
        self.assertEqual(records, LorePacker.parse_blob(blob))
        for record in records:
            self.assertEqual(0, record.offset % 16)
            expected = next(entry.payload for entry in entries if entry.hash_value == record.hash_value)
            self.assertEqual(expected, blob[record.offset : record.offset + record.length])

    def test_current_artifacts_verify(self) -> None:
        self.assertEqual(0, VerifyLore.main(["--check", "--verify-manifest", "--list"]))

    def test_manifest_requires_no_compression_and_scalability_metadata(self) -> None:
        self.assertEqual(0, LorePacker.main(["--check"]))
        manifest = json.loads(Path("Data/Lore/Encyclopedia.manifest.json").read_text(encoding="utf-8"))

        self.assertEqual("none/raw-utf8", manifest["compression"])
        self.assertEqual("<4sIII", manifest["header_struct_format"])
        self.assertEqual("<IIII", manifest["record_struct_format"])
        self.assertEqual("little", manifest["endianness"])
        self.assertEqual(72, manifest["project_atlas_fit"]["domain_id"])
        self.assertIn("toaster", manifest["scalability_profiles"])
        self.assertIn("rtx_overkill", manifest["scalability_profiles"])
        self.assertFalse(manifest["h_phi_audit"]["private_runtime_state_required"])

    def test_duplicate_filename_hash_fails(self) -> None:
        with owned_temp_dir() as root:
            source_dir = root / "Docs" / "Lore"
            (source_dir / "A").mkdir(parents=True)
            (source_dir / "B").mkdir(parents=True)
            (source_dir / "A" / "Same.md").write_text("# Same\npressure\n", encoding="utf-8")
            (source_dir / "B" / "Same.md").write_text("# Same\nhull\n", encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "Duplicate lore filename-derived ID"):
                LorePacker.load_source_entries(source_dir)

    def test_fnv1a_is_case_folded_and_ascii_guarded(self) -> None:
        self.assertEqual(LorePacker.compute_fnv1a32("Lore_Bible"), LorePacker.compute_fnv1a32("lore_bible"))

        with owned_temp_dir() as root:
            source_dir = root / "Docs" / "Lore"
            source_dir.mkdir(parents=True)
            (source_dir / "Лор.md").write_text("# Bad\npressure\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "ASCII"):
                LorePacker.load_source_entries(source_dir)

    def test_parse_rejects_alignment_and_reserved_corruption(self) -> None:
        entries = [self.make_entry("Lore_Bible", b"# Lore\npressure\n")]
        blob, _records = LorePacker.bake_blob(entries)
        corrupted = bytearray(blob)
        corrupted[16 + 12] = 1
        with self.assertRaisesRegex(ValueError, "reserved pad"):
            LorePacker.parse_blob(bytes(corrupted))

        corrupted = bytearray(blob)
        corrupted[16 + 4] = 13
        with self.assertRaisesRegex(ValueError, "payload offset"):
            LorePacker.parse_blob(bytes(corrupted))

    def test_sterile_sci_fi_terms_fail_bake(self) -> None:
        with owned_temp_dir() as root:
            source_dir = root / "Docs" / "Lore"
            source_dir.mkdir(parents=True)
            (source_dir / "Bad.md").write_text("# Bad\nquantum veil\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "sterile"):
                LorePacker.load_source_entries(source_dir)

    def test_verify_lore_cli_extracts_by_source_path(self) -> None:
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            self.assertEqual(
                0,
                VerifyLore.main(
                    [
                        "--check",
                        "--hash-source",
                        "--source-path",
                        "Docs/Lore/Archives/DeepReach_ColonyFailureArchive.md",
                    ]
                ),
            )
        self.assertIn("0x", output.getvalue())

    def test_verify_lore_cli_check_reports_raw_contract(self) -> None:
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            self.assertEqual(0, VerifyLore.main(["--check", "--list"]))
        text = output.getvalue()
        self.assertIn("LORE BAKED", text)
        self.assertIn("CHECK OK", text)
        self.assertIn("offset=", text)

    def test_manifest_json_on_disk_matches_schema(self) -> None:
        self.assertEqual(0, LorePacker.main(["--check"]))
        blob = Path("Data/Lore/Encyclopedia.h8bin").read_bytes()
        records = LorePacker.parse_blob(blob)
        entries = LorePacker.load_source_entries(Path("Docs/Lore"))
        manifest = json.loads(Path("Data/Lore/Encyclopedia.manifest.json").read_text(encoding="utf-8"))
        LorePacker.verify_manifest(blob, records, entries, manifest)

    def test_no_runtime_string_hashing_needed_in_generated_constants(self) -> None:
        self.assertEqual(0, LorePacker.main(["--check"]))
        generated = Path("Assets/_Project/Scripts/Core/Generated/H8LoreHashes.cs").read_text(encoding="utf-8")
        self.assertIn("public const uint", generated)
        self.assertNotIn("Compute", generated)
        self.assertNotIn("string", generated.lower())

    def test_verify_lore_uses_lore_packer_main(self) -> None:
        with mock.patch.object(LorePacker, "main", return_value=0) as patched:
            self.assertEqual(0, VerifyLore.main(["--check"]))
        patched.assert_called_once()


if __name__ == "__main__":
    unittest.main()
