import importlib.util
import re
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "Tools" / "VerifyH8HashCollisions.py"


def load_hash_tool():
    spec = importlib.util.spec_from_file_location("h8_hash_tool", MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class H8HashCollisionToolTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tool = load_hash_tool()

    def test_runtime_hash_variants_match_known_project_values(self):
        self.assertEqual(3511699502, self.tool.fnv1a_utf16("Data_TitaniumScrap"))
        self.assertEqual(618808801, self.tool.fnv1a_ascii_lower("biome.family.abyssal_silt"))
        self.assertEqual(1224539435, self.tool.fnv1a_signal_label("AupShiftSignal"))
        self.assertEqual(4002958475, self.tool.fnv1a_utf16("atlas6_signal_identified"))

    def test_generated_hash_file_is_excluded_from_source_scan(self):
        generated_path = ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Generated" / "H8Hashes.cs"
        self.assertTrue(self.tool.is_generated_hash_output(generated_path, ROOT))
        non_generated_path = ROOT / "Assets" / "_Project" / "Scripts" / "AtlasSignal" / "AtlasSignalEvents.cs"
        self.assertFalse(self.tool.is_generated_hash_output(non_generated_path, ROOT))

    def test_authored_signal_filter_accepts_signal_ids_and_rejects_items_or_logs(self):
        self.assertTrue(self.tool.is_authored_signal_value("atlas6_core_message"))
        self.assertTrue(self.tool.is_authored_signal_value("first_hour_exit_lifepod"))
        self.assertTrue(self.tool.is_authored_signal_constant("AtlasSignalDiscoveryId", "atlas6_signal_identified"))
        self.assertFalse(self.tool.is_authored_signal_value("Data_TitaniumScrap"))
        self.assertFalse(self.tool.is_authored_signal_constant("SignalPulseLog", "[AtlasSignal] Pulse emitted."))

    def test_collision_detection_allows_duplicate_value_and_rejects_distinct_collision(self):
        first = self.tool.HashRecord("Items", "PersistentIds", "First", "same", "loc_utf16", 99, "a")
        duplicate = self.tool.HashRecord("Items", "CodeLiteralIds", "Same", "same", "loc_utf16", 99, "b")
        distinct = self.tool.HashRecord("Signals", "AuthoredSignalIds", "Other", "other", "loc_utf16", 99, "c")
        self.assertEqual({}, self.tool.find_collisions([first, duplicate]))
        collisions = self.tool.find_collisions([first, distinct])
        self.assertIn(99, collisions)

    def test_generated_csharp_is_constants_only(self):
        record = self.tool.HashRecord("Signals", "AuthoredSignalIds", "Atlas", "atlas6_core_message", "loc_utf16", 802807208, "source")
        csharp = self.tool.build_csharp([record])
        self.assertIn("public const string AtlasId", csharp)
        self.assertIn("public const uint AtlasHash = 802807208u;", csharp)
        self.assertNotRegex(csharp, re.compile(r"\b(new|static readonly|Dictionary|List<|LocHash)\b"))
        self.assertNotIn("=>", csharp)

    def test_check_csharp_output_detects_current_missing_and_stale_files(self):
        record = self.tool.HashRecord("Signals", "AuthoredSignalIds", "Atlas", "atlas6_core_message", "loc_utf16", 802807208, "source")
        records = [record]
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir) / "H8Hashes.cs"
            self.assertEqual((False, "missing"), self.tool.check_csharp_output(records, temp_path))
            temp_path.write_text(self.tool.build_csharp(records), encoding="utf-8", newline="\n")
            self.assertEqual((True, "up-to-date"), self.tool.check_csharp_output(records, temp_path))
            temp_path.write_text(self.tool.build_csharp(records).replace("\n", "\r\n"), encoding="utf-8", newline="")
            is_current, status = self.tool.check_csharp_output(records, temp_path)
            self.assertTrue(is_current)
            self.assertIn(status, ("up-to-date", "up-to-date line-ending normalized"))
            temp_path.write_text("// stale\n", encoding="utf-8")
            self.assertEqual((False, "stale"), self.tool.check_csharp_output(records, temp_path))

    def test_markdown_report_contains_counts_modes_and_boundary(self):
        records = [
            self.tool.HashRecord("Items", "PersistentIds", "Scrap", "Data_TitaniumScrap", "loc_utf16", 3511699502, "a"),
            self.tool.HashRecord("Biomes", "FamilyIds", "Biome", "biome.family.abyssal_silt", "ascii_lower", 618808801, "b"),
            self.tool.HashRecord("Signals", "AuthoredSignalIds", "Atlas", "atlas6_core_message", "loc_utf16", 802807208, "c"),
        ]
        report = self.tool.build_report(records, "up-to-date")
        self.assertIn("Status: HASHES SYNCHRONIZED", report)
        self.assertIn("- Total records: 3", report)
        self.assertIn("- Items: 1", report)
        self.assertIn("- `ascii_lower`: 1", report)
        self.assertIn("- `Signals.AuthoredSignalIds`: 1", report)
        self.assertIn("JSON manifest", report)
        self.assertIn("Unity import is not claimed", report)

    def test_json_manifest_contains_full_records_and_runtime_boundary(self):
        records = [
            self.tool.HashRecord("Items", "PersistentIds", "Scrap", "Data_TitaniumScrap", "loc_utf16", 3511699502, "a"),
            self.tool.HashRecord("Signals", "AuthoredSignalIds", "Atlas", "atlas6_core_message", "loc_utf16", 802807208, "b"),
        ]
        manifest = self.tool.build_manifest(records, "up-to-date")
        self.assertEqual("HASHES_SYNCHRONIZED", manifest["status"])
        self.assertEqual(2, manifest["total_records"])
        self.assertEqual(1, manifest["categories"]["Items"])
        self.assertEqual(2, manifest["hash_modes"]["loc_utf16"])
        self.assertEqual(0, manifest["runtime_impact"]["microseconds_per_frame"])
        self.assertEqual("0xD150482E", manifest["records"][0]["hash_hex"])
        self.assertEqual("atlas6_core_message", manifest["records"][1]["value"])


if __name__ == "__main__":
    unittest.main()
