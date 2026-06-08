import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from io import StringIO

sys.path.insert(0, str(Path(__file__).parent))

from LoreTextBoundsVerifier import TARGET_LOCALES, collect_packets, main, rewrite_draft_prefixes


def localized_rows() -> dict[str, dict[str, str]]:
    return {
        locale: {
            "title": "Title",
            "scanner": "Scanner text",
            "terminal": "Terminal text",
            "audio": "Audio text",
            "in_game_wiki": "Wiki body",
            "external_site": "Site body",
            "field_note": "Field note",
        }
        for locale in TARGET_LOCALES
    }


class TestLoreTextBoundsVerifier(unittest.TestCase):
    def write_mixed_fixture(self, root: Path) -> None:
        base = root / "Docs" / "Lore" / "AppliedContent"
        release_dir = base / "release_sets"
        packet_dir = base / "packets"
        production_dir = base / "production_packets"
        release_dir.mkdir(parents=True)
        packet_dir.mkdir(parents=True)
        production_dir.mkdir(parents=True)

        json_source = packet_dir / "RS_TEST.packets.json"
        json_source.write_text(
            json.dumps(
                {
                    "release_set_id": "RS_TEST",
                    "packets": [
                        {
                            "packet_id": "P_TEST_BOUNDS",
                            "article_id": "test.bounds",
                            "localized": localized_rows(),
                        }
                    ],
                }
            ),
            encoding="utf-8",
        )
        production_source = production_dir / "P_TEST_PRODUCTION_ONLY.production.md"
        production_source.write_text("# P_TEST_PRODUCTION_ONLY\n", encoding="utf-8")
        (release_dir / "RS_TEST_manifest.json").write_text(
            json.dumps(
                {
                    "release_set_id": "RS_TEST",
                    "packet_sources": [
                        "Docs/Lore/AppliedContent/packets/RS_TEST.packets.json",
                        "Docs/Lore/AppliedContent/production_packets/P_TEST_PRODUCTION_ONLY.production.md",
                    ],
                }
            ),
            encoding="utf-8",
        )

    def test_collect_packets_skips_production_markdown_sources(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.write_mixed_fixture(root)

            packets, warnings = collect_packets(root, None)

            self.assertEqual([packet.packet_id for packet in packets], ["P_TEST_BOUNDS"])
            self.assertEqual(len(warnings), 1)
            self.assertIn("manifest-non-json-packet-source", warnings[0])

    def test_collect_packets_skips_noncanonical_manifest_in_all_mode(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.write_mixed_fixture(root)
            base = root / "Docs" / "Lore" / "AppliedContent"
            draft_source = base / "packets" / "RS_DRAFT.packets.json"
            draft_source.write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_DRAFT",
                        "packets": [
                            {
                                "packet_id": "P_TEST_BOUNDS",
                                "article_id": "test.bounds.draft",
                                "localized": localized_rows(),
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )
            (base / "release_sets" / "RS_DRAFT_manifest.json").write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_DRAFT",
                        "canonical_importer_ready": False,
                        "packet_sources": [
                            "Docs/Lore/AppliedContent/packets/RS_DRAFT.packets.json",
                        ],
                    }
                ),
                encoding="utf-8",
            )

            packets, warnings = collect_packets(root, None)

            self.assertEqual([packet.release_set_id for packet in packets], ["RS_TEST"])
            self.assertTrue(any("manifest-noncanonical-skipped" in warning for warning in warnings))

    def test_rewrite_draft_prefixes_removes_player_visible_process_text(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.write_mixed_fixture(root)
            source = root / "Docs" / "Lore" / "AppliedContent" / "packets" / "RS_TEST.packets.json"
            text = source.read_text(encoding="utf-8")
            source.write_text(
                text.replace('"Scanner text"', '"Draft RU localization pending native pass. Scanner text"', 1),
                encoding="utf-8",
            )
            packets, _warnings = collect_packets(root, None)

            touched = rewrite_draft_prefixes(packets)

            self.assertEqual(len(touched), 1)
            updated = source.read_text(encoding="utf-8")
            self.assertIn('"Scanner text"', updated)
            self.assertNotIn("localization pending native pass", updated)
            self.assertNotIn("LOC HOLD", updated)

    def test_no_write_reports_cli_skips_report_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.write_mixed_fixture(root)

            with patch(
                "sys.argv",
                [
                    "LoreTextBoundsVerifier.py",
                    "--root",
                    str(root),
                    "--json-report",
                    "Docs/Reports/test_bounds.json",
                    "--csv-report",
                    "Docs/Reports/test_bounds.csv",
                    "--no-write-reports",
                ],
            ), patch("sys.stdout", new=StringIO()) as out:
                ret = main()

            self.assertEqual(ret, 0)
            self.assertIn("warnings=1", out.getvalue())
            self.assertIn("reports=skipped", out.getvalue())
            self.assertFalse((root / "Docs" / "Reports" / "test_bounds.json").exists())
            self.assertFalse((root / "Docs" / "Reports" / "test_bounds.csv").exists())


if __name__ == "__main__":
    unittest.main()
