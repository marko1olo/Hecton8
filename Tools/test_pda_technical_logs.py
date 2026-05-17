#!/usr/bin/env python3
"""Regression tests for PDA technical-log data and H8PT verification."""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS_DIR = Path(__file__).resolve().parent
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import LoreTechValidator
import PackPdaTechnicalLogs
import VerifyPdaTechnicalLogs
from PdaTechSchema import build_extra_data, title_from_text


class PdaTechnicalLogTests(unittest.TestCase):
    def load_entries(self) -> list[dict[str, object]]:
        return LoreTechValidator.load_jsonl(LoreTechValidator.DEFAULT_SOURCE)

    def load_localization(self) -> list[dict[str, object]]:
        return LoreTechValidator.load_localization_entries(LoreTechValidator.DEFAULT_LOCALIZATION)

    def test_current_source_validates(self) -> None:
        LoreTechValidator.validate(self.load_entries(), self.load_localization())

    def test_extra_data_is_formula_derived(self) -> None:
        for entry in self.load_entries():
            title = title_from_text(str(entry["Text"]))
            expected = build_extra_data(str(entry["LocID"]), str(entry["Category"]), title)
            self.assertEqual(expected, entry["ExtraData"])

    def test_binary_pack_check_is_fresh(self) -> None:
        source_bytes = LoreTechValidator.DEFAULT_SOURCE.read_bytes()
        entries = self.load_entries()
        full_blob, _records = PackPdaTechnicalLogs.build_blob(entries, source_bytes)
        toaster_blob, _toaster_records = PackPdaTechnicalLogs.build_toaster_blob(entries, source_bytes)
        self.assertEqual(full_blob, PackPdaTechnicalLogs.DEFAULT_OUTPUT.read_bytes())
        self.assertEqual(toaster_blob, PackPdaTechnicalLogs.DEFAULT_TOASTER_OUTPUT.read_bytes())

    def test_independent_verifier_accepts_current_artifacts(self) -> None:
        entries, binary_bytes, toaster_bytes = VerifyPdaTechnicalLogs.verify()
        self.assertEqual(100, entries)
        self.assertEqual(59120, binary_bytes)
        self.assertEqual(19120, toaster_bytes)

    def test_toaster_contract_is_compact_only(self) -> None:
        toaster_blob = PackPdaTechnicalLogs.DEFAULT_TOASTER_OUTPUT.read_bytes()
        header = VerifyPdaTechnicalLogs.read_header(toaster_blob)
        self.assertEqual(VerifyPdaTechnicalLogs.TOASTER_FLAGS, int(header["Flags"]))
        self.assertEqual(0, int(header["CompactLength"]))
        self.assertEqual(0, int(header["ExtraLength"]))
        self.assertLess(len(toaster_blob), len(PackPdaTechnicalLogs.DEFAULT_OUTPUT.read_bytes()))
        manifest = json.loads(PackPdaTechnicalLogs.DEFAULT_MANIFEST.read_text(encoding="utf-8"))
        lookup = manifest["ToasterBinary"]["LookupContract"]
        self.assertEqual("CompactText", lookup["PayloadSemantic"])
        self.assertEqual(["TextOffset", "TextLength"], lookup["PayloadSliceFields"])

    def test_manifest_crc_drift_is_rejected(self) -> None:
        manifest = json.loads(PackPdaTechnicalLogs.DEFAULT_MANIFEST.read_text(encoding="utf-8"))
        manifest["Integrity"]["BinaryCrc32"] ^= 1
        with tempfile.TemporaryDirectory() as temp:
            manifest_path = Path(temp) / "PdaTechnicalLogs.manifest.json"
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaises(ValueError):
                VerifyPdaTechnicalLogs.verify(
                    LoreTechValidator.DEFAULT_SOURCE,
                    LoreTechValidator.DEFAULT_LOCALIZATION,
                    PackPdaTechnicalLogs.DEFAULT_OUTPUT,
                    manifest_path,
                    PackPdaTechnicalLogs.DEFAULT_TOASTER_OUTPUT,
                )

    def test_broken_link_is_rejected(self) -> None:
        entries = json.loads(json.dumps(self.load_entries()))
        entries[0]["LinkHash"] = "0xFFFFFFFF"
        with self.assertRaises(ValueError):
            LoreTechValidator.validate(entries, self.load_localization())

    def test_stale_extra_data_is_rejected(self) -> None:
        entries = json.loads(json.dumps(self.load_entries()))
        entries[0]["ExtraData"]["NoiseSeed"] ^= 1
        with self.assertRaises(ValueError):
            LoreTechValidator.validate(entries, self.load_localization())


if __name__ == "__main__":
    unittest.main()
