import json
import sys
from test_temp_root import temporary_directory
import unittest
from pathlib import Path
from unittest.mock import patch
from io import StringIO

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreImporter import TARGET_LOCALES
from AppliedLoreProductionSourceGuard import GuardError, main, next_release_set_id, normalize_release_slug, run_guard


def packet_markdown(packet_id: str, statuses: dict[str, str] | None = None, body_suffix: str = "") -> str:
    statuses = statuses or {}
    rows = []
    for locale in TARGET_LOCALES:
        status = statuses.get(locale)
        if status is None:
            status = "source_authority" if locale == "en_US" else "draft_machine_or_llm"
        rows.append(f"| {locale} | {status} | Scanner text. |")

    return "\n".join(
        [
            f"# {packet_id}",
            "",
            "## Header Metadata",
            "",
            "| Field | Value |",
            "|---|---|",
            f"| Packet ID | {packet_id} |",
            "| Runtime layer | future_import_candidate |",
            "| Content status | source_complete_unimported |",
            "",
            "## Source Brief",
            "",
            "Cold source only. No runtime readiness.",
            "",
            "## Surface Texts",
            "",
            "### Scanner",
            "",
            "Scanner text.",
            "",
            "## Future Integration Notes",
            "",
            "Importer/publication readiness remains false.",
            "",
            "## Locale Rows",
            "",
            "| Locale | Status | Text |",
            "|---|---|---|",
            *rows,
            body_suffix,
        ]
    )


def write_release(root: Path, release_set_id: str, packet_ids: list[str]) -> None:
    base = root / "Docs" / "Lore" / "AppliedContent"
    packet_dir = base / "production_packets"
    release_dir = base / "release_sets"
    packet_dir.mkdir(parents=True, exist_ok=True)
    release_dir.mkdir(parents=True, exist_ok=True)

    sources = []
    for packet_id in packet_ids:
        path = packet_dir / f"{packet_id}.production.md"
        path.write_text(packet_markdown(packet_id), encoding="utf-8")
        sources.append(f"Docs/Lore/AppliedContent/production_packets/{packet_id}.production.md")

    manifest = {
        "schema": "H8.APPLIED_LORE_RELEASE_SET.V0",
        "release_set_id": release_set_id,
        "packets": list(packet_ids),
        "packet_sources": sources,
        "canonical_importer_ready": False,
        "runtime_ready": False,
        "native_localization_ready": False,
        "data_monolith_ready": False,
        "h8bin_ready": False,
        "unity_placement_ready": False,
        "generated_page_ready": False,
        "publication_ready": False,
    }
    (release_dir / f"{release_set_id}_manifest.json").write_text(json.dumps(manifest), encoding="utf-8")


class TestAppliedLoreProductionSourceGuard(unittest.TestCase):
    def test_valid_release_passes(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS900_TEST_PRODUCTION_SOURCE", ["P9000_TEST_PACKET"])

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS900_*", "", False)

            self.assertEqual(ret, 0)
            self.assertIn("FINAL: PASS", out.getvalue())

    def test_locale_gap_fails(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS901_TEST_PRODUCTION_SOURCE", ["P9001_TEST_PACKET"])
            packet_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "production_packets"
                / "P9001_TEST_PACKET.production.md"
            )
            packet_path.write_text(
                packet_markdown("P9001_TEST_PACKET").replace("| ru_RU | draft_machine_or_llm | Scanner text. |\n", ""),
                encoding="utf-8",
            )

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS901_*", "", False)

            self.assertEqual(ret, 1)
            self.assertIn("missing locale rows: ru_RU", out.getvalue())

    def test_ready_manifest_flag_fails(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS902_TEST_PRODUCTION_SOURCE", ["P9002_TEST_PACKET"])
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS902_TEST_PRODUCTION_SOURCE_manifest.json"
            )
            data = json.loads(manifest_path.read_text(encoding="utf-8"))
            data["runtime_ready"] = True
            manifest_path.write_text(json.dumps(data), encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS902_*", "", False)

            self.assertEqual(ret, 1)
            self.assertIn("runtime_ready must be false", out.getvalue())

    def test_manifest_status_ready_claim_fails(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS902_TEST_PRODUCTION_SOURCE", ["P9002_TEST_PACKET"])
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS902_TEST_PRODUCTION_SOURCE_manifest.json"
            )
            data = json.loads(manifest_path.read_text(encoding="utf-8"))
            data["status"] = "canonical_importer_ready_route_card_exported_pages_generated_binding_targets_pending_h8bin"
            manifest_path.write_text(json.dumps(data), encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS902_*", "", False)

            self.assertEqual(ret, 1)
            self.assertIn("status must not claim ready/exported runtime state", out.getvalue())

    def test_release_number_conflict_fails_selected_release(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS903_TEST_ONE", ["P9003_TEST_PACKET"])
            write_release(root, "RS903_TEST_TWO", ["P9004_TEST_PACKET"])

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS903_TEST_ONE", "", False)

            self.assertEqual(ret, 1)
            self.assertIn("release number prefix RS903 is reused", out.getvalue())

    def test_unknown_connected_packet_fails(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS904_TEST_PRODUCTION_SOURCE", ["P9005_TEST_PACKET"])
            packet_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "production_packets"
                / "P9005_TEST_PACKET.production.md"
            )
            packet_path.write_text(
                packet_markdown("P9005_TEST_PACKET").replace(
                    "| Packet ID | P9005_TEST_PACKET |\n",
                    "| Packet ID | P9005_TEST_PACKET |\n"
                    "| Connected packets | P9999_MISSING_PACKET |\n",
                ),
                encoding="utf-8",
            )

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS904_*", "", False)

            self.assertEqual(ret, 1)
            self.assertIn("connected packet id is not present", out.getvalue())

    def test_connected_packet_can_exist_in_packet_bundle(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS904_TEST_PRODUCTION_SOURCE", ["P9005_TEST_PACKET"])
            packet_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "production_packets"
                / "P9005_TEST_PACKET.production.md"
            )
            packet_path.write_text(
                packet_markdown("P9005_TEST_PACKET").replace(
                    "| Packet ID | P9005_TEST_PACKET |\n",
                    "| Packet ID | P9005_TEST_PACKET |\n"
                    "| Connected packets | P9999_BUNDLED_PACKET |\n",
                ),
                encoding="utf-8",
            )
            bundle_path = root / "Docs" / "Lore" / "AppliedContent" / "packets" / "RS904_TEST.packets.json"
            bundle_path.parent.mkdir(parents=True, exist_ok=True)
            bundle_path.write_text(
                json.dumps({"release_set_id": "RS904_TEST", "packets": [{"packet_id": "P9999_BUNDLED_PACKET"}]}),
                encoding="utf-8",
            )

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS904_*", "", False)

            self.assertEqual(ret, 0)
            self.assertIn("FINAL: PASS", out.getvalue())

    def test_connected_packet_can_exist_in_applied_markdown(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS904_TEST_PRODUCTION_SOURCE", ["P9005_TEST_PACKET"])
            packet_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "production_packets"
                / "P9005_TEST_PACKET.production.md"
            )
            packet_path.write_text(
                packet_markdown("P9005_TEST_PACKET").replace(
                    "| Packet ID | P9005_TEST_PACKET |\n",
                    "| Packet ID | P9005_TEST_PACKET |\n"
                    "| Connected packets | P9998_GENERATED_PACKET |\n",
                ),
                encoding="utf-8",
            )
            applied_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "in_game_wiki"
                / "en_US"
                / "P9998_GENERATED_PACKET.md"
            )
            applied_path.parent.mkdir(parents=True, exist_ok=True)
            applied_path.write_text("# Generated packet\n", encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS904_*", "", False)

            self.assertEqual(ret, 0)
            self.assertIn("FINAL: PASS", out.getvalue())

    def test_manifest_packets_must_include_selected_production_source(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS905_TEST_PRODUCTION_SOURCE", ["P9006_TEST_PACKET"])
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS905_TEST_PRODUCTION_SOURCE_manifest.json"
            )
            data = json.loads(manifest_path.read_text(encoding="utf-8"))
            data["packets"] = ["P9999_OTHER_PACKET"]
            manifest_path.write_text(json.dumps(data), encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS905_*", "", False)

            self.assertEqual(ret, 1)
            self.assertIn("production source P9006_TEST_PACKET is not listed in manifest packets", out.getvalue())

    def test_manifest_packets_missing_full_source_fails(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS906_TEST_PRODUCTION_SOURCE", ["P9007_TEST_PACKET"])
            manifest_path = (
                root
                / "Docs"
                / "Lore"
                / "AppliedContent"
                / "release_sets"
                / "RS906_TEST_PRODUCTION_SOURCE_manifest.json"
            )
            data = json.loads(manifest_path.read_text(encoding="utf-8"))
            data["packets"].append("P9008_MISSING_SOURCE")
            manifest_path.write_text(json.dumps(data), encoding="utf-8")

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS906_*", "", False)

            self.assertEqual(ret, 1)
            self.assertIn("manifest packet ids missing production sources: P9008_MISSING_SOURCE", out.getvalue())

    def test_manifest_packets_allow_scoped_packet_glob(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(
                root,
                "RS907_TEST_PRODUCTION_SOURCE",
                ["P9009_TEST_PACKET", "P9011_TEST_PACKET"],
            )

            with patch("sys.stdout", new=StringIO()) as out:
                ret = run_guard(root, "RS907_*", "P9009_*", False)

            self.assertEqual(ret, 0)
            self.assertIn("FINAL: PASS", out.getvalue())

    def test_next_release_id_uses_high_water_not_gap(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS010_TEST_ONE", ["P9010_TEST_PACKET"])
            write_release(root, "RS012_TEST_TWO", ["P9012_TEST_PACKET"])

            release_set_id, number, high_water = next_release_set_id(root, "new test pack", 10)

            self.assertEqual(release_set_id, "RS013_NEW_TEST_PACK")
            self.assertEqual(number, 13)
            self.assertEqual(high_water, 12)

    def test_next_release_id_uses_filename_prefix_for_bad_manifest_id(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            release_dir = root / "Docs" / "Lore" / "AppliedContent" / "release_sets"
            release_dir.mkdir(parents=True)
            manifest = {
                "schema": "H8.APPLIED_LORE_RELEASE_SET.V0",
                "release_set_id": "BAD",
                "packet_sources": ["Docs/Lore/AppliedContent/production_packets/P9014_BAD.production.md"],
            }
            (release_dir / "RS014_BAD_manifest.json").write_text(json.dumps(manifest), encoding="utf-8")

            release_set_id, number, high_water = next_release_set_id(root, "rs014 weird slug", 10)

            self.assertEqual(release_set_id, "RS015_WEIRD_SLUG")
            self.assertEqual(number, 15)
            self.assertEqual(high_water, 14)

    def test_normalize_release_slug_falls_back_to_untitled(self):
        self.assertEqual(normalize_release_slug(""), "UNTITLED_RELEASE")
        self.assertEqual(normalize_release_slug("RS042"), "UNTITLED_RELEASE")

    def test_next_release_id_rejects_invalid_start_number(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            (root / "Docs" / "Lore" / "AppliedContent" / "release_sets").mkdir(parents=True)

            with self.assertRaises(GuardError):
                next_release_set_id(root, "bad", -1)

            with self.assertRaises(GuardError):
                next_release_set_id(root, "bad", 1000)

    def test_next_release_id_cli_prints_json(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_release(root, "RS018_TEST_SOURCE", ["P9018_TEST_PACKET"])

            with patch(
                "sys.argv",
                [
                    "AppliedLoreProductionSourceGuard.py",
                    "--root",
                    str(root),
                    "--next-release-id",
                    "cli source",
                    "--start-release-number",
                    "18",
                    "--json",
                ],
            ), patch("sys.stdout", new=StringIO()) as out:
                ret = main()

            self.assertEqual(ret, 0)
            data = json.loads(out.getvalue())
            self.assertEqual(data["release_set_id"], "RS019_CLI_SOURCE")
            self.assertEqual(data["release_number"], 19)
            self.assertEqual(data["high_water_release_number"], 18)


if __name__ == "__main__":
    unittest.main()
