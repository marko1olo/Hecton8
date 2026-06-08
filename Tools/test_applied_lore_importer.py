import json
import sys
from test_temp_root import temporary_directory
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreImporter import (
    ROW_FLAG_DRAFT_LOCALIZATION,
    TARGET_LOCALES,
    check_import_outputs,
    collect_packets,
    data_monolith_csv_path,
    import_applied_lore,
    localized_row_flags,
    packet_rows,
    safe_console_line,
    sanitize_localized_text,
)


def complete_localized(title: str = "Test Title") -> dict[str, dict[str, str]]:
    return {
        locale: {
            "title": title,
            "scanner": "Scanner text",
            "terminal": "Terminal text",
            "audio": "Audio text",
            "in_game_wiki": "Wiki body",
            "external_site": "Site body",
            "field_note": "Field note",
        }
        for locale in TARGET_LOCALES
    }


def importer_packet() -> dict:
    return {
        "packet_id": "P_TEST_IMPORT_CHECK",
        "release_set_id": "RS_TEST",
        "article_id": "test.import.check",
        "unlock": {
            "primary": "unlock.test.import",
            "poi_tags": ["poi.test"],
            "biome_tags": ["biome.test"],
        },
        "localized": complete_localized(),
    }


def write_canonical_release(
    root: Path,
    release_set_id: str,
    manifest_packet_ids: list[str],
    source_packets: list[dict],
    *,
    bundle_status: str = "",
) -> None:
    base = root / "Docs" / "Lore" / "AppliedContent"
    packet_dir = base / "packets"
    release_dir = base / "release_sets"
    packet_dir.mkdir(parents=True, exist_ok=True)
    release_dir.mkdir(parents=True, exist_ok=True)

    normalized_packets = []
    for packet in source_packets:
        normalized_packet = dict(packet)
        normalized_packet.pop("release_set_id", None)
        normalized_packets.append(normalized_packet)

    source_name = f"{release_set_id}.packets.json"
    (packet_dir / source_name).write_text(
        json_text({"release_set_id": release_set_id, "status": bundle_status, "packets": normalized_packets}),
        encoding="utf-8",
    )
    (release_dir / f"{release_set_id}_manifest.json").write_text(
        json_text(
            {
                "schema": "H8.APPLIED_LORE_RELEASE_SET.V0",
                "release_set_id": release_set_id,
                "packets": manifest_packet_ids,
                "packet_sources": [f"Docs/Lore/AppliedContent/packets/{source_name}"],
                "canonical_importer_ready": True,
            }
        ),
        encoding="utf-8",
    )


def packet_with_id(packet_id: str) -> dict:
    packet = importer_packet()
    packet["packet_id"] = packet_id
    return packet


def json_text(data: dict) -> str:
    return json.dumps(data) + "\n"


class TestAppliedLoreImporter(unittest.TestCase):
    def test_safe_console_line_escapes_non_ascii_for_ascii_stdout(self):
        line = safe_console_line("first_diff_field: expected=質量 current=old", encoding="ascii")

        self.assertEqual(line, "first_diff_field: expected=\\u8cea\\u91cf current=old")

    def test_sanitize_strips_underscore_locale_draft_prefix(self):
        text = "Draft ru_RU localization pending native pass. Shallow Annex P-63 Pump Room"

        self.assertEqual(sanitize_localized_text(text), "Shallow Annex P-63 Pump Room")
        self.assertEqual(localized_row_flags({"title": text}), ROW_FLAG_DRAFT_LOCALIZATION)

    def test_sanitize_strips_hyphen_locale_draft_prefix(self):
        text = "Draft PT-BR localization pending native pass. Livro de Frenagem"

        self.assertEqual(sanitize_localized_text(text), "Livro de Frenagem")
        self.assertEqual(localized_row_flags({"title": text}), ROW_FLAG_DRAFT_LOCALIZATION)

    def test_bundle_pending_native_status_marks_non_english_rows_draft_without_visible_prefix(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_canonical_release(
                root,
                "RS_TEST_IMPORTER_DRAFT_STATUS",
                ["P_IMPORTER_DRAFT_STATUS"],
                [packet_with_id("P_IMPORTER_DRAFT_STATUS")],
                bundle_status="production_facing_draft_pending_native_localization",
            )

            rows = packet_rows(collect_packets(root))
            by_locale = {row["locale"]: int(row["flags"]) for row in rows}

            self.assertEqual(by_locale["en_US"], 0)
            self.assertEqual(by_locale["ru_RU"] & ROW_FLAG_DRAFT_LOCALIZATION, ROW_FLAG_DRAFT_LOCALIZATION)

    def test_import_check_is_clean_after_import(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            with patch("AppliedLoreImporter.collect_packets", return_value=[importer_packet()]):
                import_applied_lore(root)
                stats = check_import_outputs(root)

            self.assertEqual(stats.checked_files, 2)
            self.assertEqual(stats.stale_files, 0)
            self.assertEqual(stats.missing_files, 0)

    def test_import_check_reports_stale_csv_without_writing(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            packet = importer_packet()
            with patch("AppliedLoreImporter.collect_packets", return_value=[packet]):
                import_applied_lore(root)
                csv_path = data_monolith_csv_path(root)
                stale_text = "stale\n"
                csv_path.write_text(stale_text, encoding="utf-8")
                stats = check_import_outputs(root)

            self.assertEqual(stats.stale_files, 1)
            self.assertIn(
                "stale: Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv",
                stats.sample_issues,
            )
            self.assertEqual(csv_path.read_text(encoding="utf-8"), stale_text)

    def test_import_check_reports_first_stale_csv_packet_locale_and_field(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            packet = importer_packet()
            with patch("AppliedLoreImporter.collect_packets", return_value=[packet]):
                import_applied_lore(root)
                csv_path = data_monolith_csv_path(root)
                stale_text = csv_path.read_text(encoding="utf-8").replace(
                    "Scanner text",
                    "Old scanner text",
                    1,
                )
                csv_path.write_text(stale_text, encoding="utf-8")
                stats = check_import_outputs(root)

            self.assertEqual(stats.stale_files, 1)
            self.assertIn(
                (
                    "first_diff: Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv: "
                    "row=2 expected_packet=P_TEST_IMPORT_CHECK expected_locale=en_US "
                    "current_packet=P_TEST_IMPORT_CHECK current_locale=en_US"
                ),
                stats.sample_issues,
            )
            self.assertIn(
                (
                    "first_diff_field: Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv: "
                    "field=scanner expected=Scanner text current=Old scanner text"
                ),
                stats.sample_issues,
            )
            self.assertEqual(csv_path.read_text(encoding="utf-8"), stale_text)

    def test_collect_packets_accepts_matching_manifest_and_sources(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_canonical_release(
                root,
                "RS_TEST_IMPORTER_MATCH",
                ["P_IMPORTER_MATCH"],
                [packet_with_id("P_IMPORTER_MATCH")],
            )

            packets = collect_packets(root)

            self.assertEqual([packet["packet_id"] for packet in packets], ["P_IMPORTER_MATCH"])

    def test_collect_packets_scoped_glob_ignores_unrelated_duplicate_wip(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_canonical_release(
                root,
                "RS_TEST_IMPORTER_TARGET",
                ["P_IMPORTER_TARGET"],
                [packet_with_id("P_IMPORTER_TARGET")],
            )
            write_canonical_release(
                root,
                "RS_TEST_IMPORTER_OTHER_A",
                ["P_IMPORTER_OTHER_DUPLICATE"],
                [packet_with_id("P_IMPORTER_OTHER_DUPLICATE")],
            )
            write_canonical_release(
                root,
                "RS_TEST_IMPORTER_OTHER_B",
                ["P_IMPORTER_OTHER_DUPLICATE"],
                [packet_with_id("P_IMPORTER_OTHER_DUPLICATE")],
            )

            packets = collect_packets(root, "P_IMPORTER_TARGET")

            self.assertEqual([packet["packet_id"] for packet in packets], ["P_IMPORTER_TARGET"])
            with self.assertRaisesRegex(ValueError, "Duplicate packet_id P_IMPORTER_OTHER_DUPLICATE"):
                collect_packets(root)

    def test_collect_packets_rejects_manifest_id_missing_source(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_canonical_release(
                root,
                "RS_TEST_IMPORTER_MISSING",
                ["P_IMPORTER_PRESENT", "P_IMPORTER_MISSING"],
                [packet_with_id("P_IMPORTER_PRESENT")],
            )

            with self.assertRaisesRegex(ValueError, "Manifest packet ids missing from sources: P_IMPORTER_MISSING"):
                collect_packets(root)

    def test_collect_packets_rejects_source_id_missing_manifest_id(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_canonical_release(
                root,
                "RS_TEST_IMPORTER_EXTRA",
                ["P_IMPORTER_LISTED"],
                [packet_with_id("P_IMPORTER_LISTED"), packet_with_id("P_IMPORTER_EXTRA")],
            )

            with self.assertRaisesRegex(ValueError, "Source packet ids not listed in manifest packets: P_IMPORTER_EXTRA"):
                collect_packets(root)

    def test_collect_packets_rejects_duplicate_manifest_packet_ids(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_canonical_release(
                root,
                "RS_TEST_IMPORTER_DUPLICATE_MANIFEST",
                ["P_IMPORTER_DUPLICATE", "P_IMPORTER_DUPLICATE"],
                [packet_with_id("P_IMPORTER_DUPLICATE")],
            )

            with self.assertRaisesRegex(ValueError, "Duplicate manifest packet ids: P_IMPORTER_DUPLICATE"):
                collect_packets(root)

    def test_collect_packets_rejects_ready_manifest_without_packet_list(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            write_canonical_release(
                root,
                "RS_TEST_IMPORTER_EMPTY",
                [],
                [packet_with_id("P_IMPORTER_EMPTY")],
            )

            with self.assertRaisesRegex(ValueError, "Manifest packets must list source packet ids"):
                collect_packets(root)


if __name__ == "__main__":
    unittest.main()
