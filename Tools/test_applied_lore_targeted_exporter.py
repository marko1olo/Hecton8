import csv
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreImporter import TARGET_LOCALES
from AppliedLoreTargetedExporter import (
    AppliedLoreTargetedError,
    find_text_integrity_errors,
    load_packet_sources,
    merge_publication_surface_rows,
    validate_packet_source,
)
from AppliedLorePageExporter import PUBLICATION_INDEX_HEADERS


def localized_row(prefix: str = ""):
    return {
        "title": prefix + "Title",
        "scanner": prefix + "Scanner text",
        "terminal": prefix + "Terminal text",
        "audio": prefix + "Audio text",
        "in_game_wiki": prefix + "Wiki body",
        "external_site": prefix + "Site body",
        "field_note": prefix + "Field note",
    }


def complete_localized():
    rows = {"en_US": localized_row()}
    for locale in TARGET_LOCALES:
        if locale == "en_US":
            continue
        prefix = f"Draft {locale[:2].upper()} localization pending native pass. "
        rows[locale] = localized_row(prefix)
    return rows


class TestAppliedLoreTargetedExporter(unittest.TestCase):
    def test_load_packet_bundle_without_manifest(self):
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp) / "Docs" / "Lore" / "AppliedContent"
            packet_dir = base / "packets"
            packet_dir.mkdir(parents=True)
            packet_path = packet_dir / "RS_TEST.packets.json"
            packet_path.write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_TEST",
                        "packets": [
                            {
                                "packet_id": "P_TEST",
                                "article_id": "test.article",
                                "localized": complete_localized(),
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )

            packets = load_packet_sources(base)

            self.assertEqual(len(packets), 1)
            self.assertEqual(packets[0]["packet_id"], "P_TEST")
            self.assertEqual(packets[0]["release_set_id"], "RS_TEST")
            self.assertEqual(Path(packets[0]["_source_path"]), packet_path.resolve())

    def test_validate_packet_source_catches_missing_draft_marker(self):
        packet = {
            "packet_id": "P_BAD",
            "release_set_id": "RS_TEST",
            "article_id": "test.bad",
            "localized": complete_localized(),
        }
        packet["localized"]["ru_RU"]["scanner"] = "No marker"

        with self.assertRaises(AppliedLoreTargetedError):
            validate_packet_source([packet])

    def test_text_integrity_catches_exact_mojibake(self):
        errors = find_text_integrity_errors("CafÃ©")
        self.assertTrue(any("mojibake" in error for error in errors))

    def test_merge_surface_rows_replaces_selected_only(self):
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp)
            index_path = base / "Publication_Surface_Index.csv"
            index_path.parent.mkdir(parents=True, exist_ok=True)
            buffer = io.StringIO(newline="")
            writer = csv.DictWriter(buffer, fieldnames=PUBLICATION_INDEX_HEADERS, lineterminator="\n")
            writer.writeheader()
            writer.writerow(
                {
                    key: "" for key in PUBLICATION_INDEX_HEADERS
                }
                | {
                    "surface": "in_game_wiki",
                    "locale": "en_US",
                    "direction": "ltr",
                    "packet_id": "P_KEEP",
                    "page_path": "in_game_wiki/en_US/P_KEEP.md",
                    "localization_status": "source_authority",
                    "localization_flags": "0",
                    "status_bucket": "ready",
                }
            )
            writer.writerow(
                {
                    key: "" for key in PUBLICATION_INDEX_HEADERS
                }
                | {
                    "surface": "in_game_wiki",
                    "locale": "en_US",
                    "direction": "ltr",
                    "packet_id": "P_TEST",
                    "page_path": "old.md",
                    "localization_status": "source_authority",
                    "localization_flags": "0",
                    "status_bucket": "ready",
                }
            )
            index_path.write_text(buffer.getvalue(), encoding="utf-8")

            packet = {
                "packet_id": "P_TEST",
                "release_set_id": "RS_TEST",
                "article_id": "test.article",
                "_source_path": str((base / "packets" / "RS_TEST.packets.json").resolve()),
                "localized": complete_localized(),
            }
            count, changed = merge_publication_surface_rows(base, [packet], dry_run=False)

            self.assertTrue(changed)
            self.assertEqual(count, len(TARGET_LOCALES) * 2)
            rows = list(csv.DictReader(index_path.read_text(encoding="utf-8").splitlines()))
            self.assertTrue(any(row["packet_id"] == "P_KEEP" for row in rows))
            self.assertFalse(any(row["packet_id"] == "P_TEST" and row["page_path"] == "old.md" for row in rows))


if __name__ == "__main__":
    unittest.main()
