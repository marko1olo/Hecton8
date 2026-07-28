import csv
import io
import json
import sys
from test_temp_root import temporary_directory
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from AppliedLoreImporter import CSV_HEADERS, TARGET_LOCALES, packet_rows
from AppliedLoreTargetedExporter import (
    AppliedLoreTargetedError,
    export_targeted,
    find_text_integrity_errors,
    load_packet_sources,
    merge_publication_surface_rows,
    read_baked_lore_packet_rows,
    validate_packet_source,
    validate_selected_pages,
)
from AppliedLorePageExporter import (
    NAVIGATION_CLUSTER_GRAPH_HEADERS,
    NAVIGATION_CLUSTER_GRAPH_PATH,
    PUBLICATION_INDEX_HEADERS,
    check_publication_freshness,
    export_pages,
    render_page,
)


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


def complete_localized_without_visible_draft_prefix():
    return {locale: localized_row() for locale in TARGET_LOCALES}


def write_baked_lore_csv(root: Path, rows: list[dict[str, str]]) -> None:
    path = root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"
    path.parent.mkdir(parents=True, exist_ok=True)
    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=CSV_HEADERS, lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    path.write_text(buffer.getvalue(), encoding="utf-8")


class TestAppliedLoreTargetedExporter(unittest.TestCase):
    def test_load_packet_bundle_without_manifest(self):
        with temporary_directory() as tmp:
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

    def test_load_single_packet_inherits_release_set_from_manifest(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            base = root / "Docs" / "Lore" / "AppliedContent"
            packet_dir = base / "packets"
            release_dir = base / "release_sets"
            packet_dir.mkdir(parents=True)
            release_dir.mkdir(parents=True)
            packet_path = packet_dir / "P_TEST_DIRECT.json"
            packet_path.write_text(
                json.dumps(
                    {
                        "packet_id": "P_TEST_DIRECT",
                        "article_id": "test.direct",
                        "localized": complete_localized(),
                    }
                ),
                encoding="utf-8",
            )
            (release_dir / "RS_DIRECT_manifest.json").write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_DIRECT",
                        "packet_sources": ["Docs/Lore/AppliedContent/packets/P_TEST_DIRECT.json"],
                    }
                ),
                encoding="utf-8",
            )

            packets = load_packet_sources(base)

            self.assertEqual(len(packets), 1)
            self.assertEqual(packets[0]["packet_id"], "P_TEST_DIRECT")
            self.assertEqual(packets[0]["release_set_id"], "RS_DIRECT")

    def test_validate_packet_source_catches_missing_draft_status(self):
        packet = {
            "packet_id": "P_BAD",
            "release_set_id": "RS_TEST",
            "article_id": "test.bad",
            "localized": complete_localized_without_visible_draft_prefix(),
        }

        with self.assertRaisesRegex(AppliedLoreTargetedError, "missing draft localization status"):
            validate_packet_source([packet])

    def test_validate_packet_source_accepts_metadata_draft_status_without_visible_prefix(self):
        packet = {
            "packet_id": "P_METADATA_DRAFT",
            "release_set_id": "RS_TEST",
            "article_id": "test.metadata_draft",
            "_manifest_status": "production_facing_draft_pending_native_localization",
            "localized": complete_localized_without_visible_draft_prefix(),
        }

        validate_packet_source([packet])

    def test_text_integrity_catches_exact_mojibake(self):
        errors = find_text_integrity_errors("CafÃ©")
        self.assertTrue(any("mojibake" in error for error in errors))

    def test_validate_packet_source_catches_external_site_article_replacement_question_marks(self):
        packet = {
            "packet_id": "P_BAD_ARTICLE",
            "release_set_id": "RS_TEST",
            "article_id": "test.bad_article",
            "localized": complete_localized(),
        }
        packet["localized"]["fr_FR"]["external_site_article"] = (
            "Draft fr_FR localization pending native pass. L??conomie de route sans FTL garde "
            "HECTON-8 honn?te."
        )

        with self.assertRaisesRegex(AppliedLoreTargetedError, "external_site_article: suspicious_question_mark"):
            validate_packet_source([packet])

    def test_validate_packet_source_allows_sentence_question_marks(self):
        packet = {
            "packet_id": "P_GOOD_QUESTION",
            "release_set_id": "RS_TEST",
            "article_id": "test.good_question",
            "localized": complete_localized(),
        }
        packet["localized"]["fr_FR"]["external_site_article"] = (
            "Draft fr_FR localization pending native pass. Qui paie le freinage? La réponse reste dans le registre."
        )

        validate_packet_source([packet])

    def test_source_only_validates_packet_without_generated_pages(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            base = root / "Docs" / "Lore" / "AppliedContent"
            packet_dir = base / "packets"
            packet_dir.mkdir(parents=True)
            (packet_dir / "RS_TEST.packets.json").write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_TEST",
                        "packets": [
                            {
                                "packet_id": "P_SOURCE_ONLY",
                                "article_id": "test.source_only",
                                "localized": complete_localized(),
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )

            stats = export_targeted(
                root,
                ("P_SOURCE_ONLY",),
                include_all=False,
                explicit_packet_sources=(),
                dry_run=False,
                validate_only=False,
                source_only=True,
                refresh_indexes=False,
            )

            self.assertEqual(stats.source_packets, 1)
            self.assertEqual(stats.target_packets, 1)
            self.assertEqual(stats.pages_written, 0)
            self.assertEqual(stats.surface_rows_targeted, 0)

    def test_merge_surface_rows_replaces_selected_only(self):
        with temporary_directory() as tmp:
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
                    "title": "x" * 150_000,
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

    def test_source_only_does_not_require_generated_pages(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            packet_dir = root / "Docs" / "Lore" / "AppliedContent" / "packets"
            packet_dir.mkdir(parents=True)
            packet_dir.joinpath("RS_TEST.packets.json").write_text(
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

            stats = export_targeted(
                root,
                ("P_TEST",),
                include_all=False,
                explicit_packet_sources=(),
                dry_run=False,
                validate_only=False,
                source_only=True,
                refresh_indexes=False,
            )

            self.assertEqual(stats.target_packets, 1)
            self.assertEqual(stats.pages_written, 0)

    def test_refresh_indexes_with_explicit_packet_source_preserves_default_packets(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            base = root / "Docs" / "Lore" / "AppliedContent"
            packet_dir = base / "packets"
            packet_dir.mkdir(parents=True)
            keep = {
                "packet_id": "P_KEEP_INDEX",
                "release_set_id": "RS_KEEP",
                "article_id": "test.keep_index",
                "localized": complete_localized(),
            }
            selected = {
                "packet_id": "P_SELECTED_INDEX",
                "release_set_id": "RS_SELECTED",
                "article_id": "test.selected_index",
                "localized": complete_localized(),
            }
            packet_dir.joinpath("RS_KEEP.packets.json").write_text(
                json.dumps({"release_set_id": "RS_KEEP", "packets": [keep]}),
                encoding="utf-8",
            )
            selected_source = packet_dir / "RS_SELECTED.packets.json"
            selected_source.write_text(
                json.dumps({"release_set_id": "RS_SELECTED", "packets": [selected]}),
                encoding="utf-8",
            )

            export_targeted(
                root,
                (),
                include_all=True,
                explicit_packet_sources=(),
                dry_run=False,
                validate_only=False,
                source_only=False,
                refresh_indexes=True,
            )

            export_targeted(
                root,
                ("P_SELECTED_INDEX",),
                include_all=False,
                explicit_packet_sources=(selected_source.resolve(),),
                dry_run=False,
                validate_only=False,
                source_only=False,
                refresh_indexes=True,
            )

            index_text = (base / "in_game_wiki" / "en_US" / "INDEX.md").read_text(encoding="utf-8")
            self.assertIn("localized_pages: 2", index_text)
            self.assertIn("(P_KEEP_INDEX.md) `P_KEEP_INDEX`", index_text)
            self.assertIn("(P_SELECTED_INDEX.md) `P_SELECTED_INDEX`", index_text)

    def test_source_only_can_validate_selected_baked_csv_bridge(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            packet_dir = root / "Docs" / "Lore" / "AppliedContent" / "packets"
            packet_dir.mkdir(parents=True)
            packet = {
                "packet_id": "P_BRIDGE",
                "release_set_id": "RS_TEST",
                "article_id": "test.bridge",
                "localized": complete_localized(),
            }
            packet_dir.joinpath("RS_TEST.packets.json").write_text(
                json.dumps({"release_set_id": "RS_TEST", "packets": [packet]}),
                encoding="utf-8",
            )
            write_baked_lore_csv(root, packet_rows([packet]))

            stats = export_targeted(
                root,
                ("P_BRIDGE",),
                include_all=False,
                explicit_packet_sources=(),
                dry_run=False,
                validate_only=False,
                source_only=True,
                refresh_indexes=False,
                validate_baked_csv=True,
            )

            self.assertEqual(stats.target_packets, 1)
            self.assertEqual(stats.baked_csv_rows_targeted, len(TARGET_LOCALES))

    def test_validate_selected_baked_csv_bridge_catches_stale_row(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            packet_dir = root / "Docs" / "Lore" / "AppliedContent" / "packets"
            packet_dir.mkdir(parents=True)
            packet = {
                "packet_id": "P_STALE",
                "release_set_id": "RS_TEST",
                "article_id": "test.stale",
                "localized": complete_localized(),
            }
            packet_dir.joinpath("RS_TEST.packets.json").write_text(
                json.dumps({"release_set_id": "RS_TEST", "packets": [packet]}),
                encoding="utf-8",
            )
            stale_rows = packet_rows([packet])
            stale_rows[0] = dict(stale_rows[0])
            stale_rows[0]["title"] = "Old title still in runtime table"
            write_baked_lore_csv(root, stale_rows)

            with self.assertRaisesRegex(AppliedLoreTargetedError, "baked CSV mismatch"):
                export_targeted(
                    root,
                    ("P_STALE",),
                    include_all=False,
                    explicit_packet_sources=(),
                    dry_run=False,
                    validate_only=False,
                    source_only=True,
                    refresh_indexes=False,
                    validate_baked_csv=True,
                )

    def test_validate_selected_baked_csv_bridge_catches_missing_and_duplicate_rows(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            packet_dir = root / "Docs" / "Lore" / "AppliedContent" / "packets"
            packet_dir.mkdir(parents=True)
            packet = {
                "packet_id": "P_DUPLICATE",
                "release_set_id": "RS_TEST",
                "article_id": "test.duplicate",
                "localized": complete_localized(),
            }
            packet_dir.joinpath("RS_TEST.packets.json").write_text(
                json.dumps({"release_set_id": "RS_TEST", "packets": [packet]}),
                encoding="utf-8",
            )
            rows = packet_rows([packet])
            rows = rows[:-1] + [dict(rows[0])]
            write_baked_lore_csv(root, rows)

            with self.assertRaisesRegex(AppliedLoreTargetedError, "duplicate row"):
                export_targeted(
                    root,
                    ("P_DUPLICATE",),
                    include_all=False,
                    explicit_packet_sources=(),
                    dry_run=False,
                    validate_only=False,
                    source_only=True,
                    refresh_indexes=False,
                    validate_baked_csv=True,
                )

    def test_update_baked_csv_replaces_selected_rows_and_preserves_unrelated_rows(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            packet_dir = root / "Docs" / "Lore" / "AppliedContent" / "packets"
            packet_dir.mkdir(parents=True)
            selected = {
                "packet_id": "P_UPDATE",
                "release_set_id": "RS_TEST",
                "article_id": "test.update",
                "localized": complete_localized(),
            }
            keep = {
                "packet_id": "P_KEEP",
                "release_set_id": "RS_KEEP",
                "article_id": "test.keep",
                "localized": complete_localized(),
            }
            packet_dir.joinpath("RS_TEST.packets.json").write_text(
                json.dumps({"release_set_id": "RS_TEST", "packets": [selected]}),
                encoding="utf-8",
            )
            rows = packet_rows([keep, selected])
            stale_rows = []
            for row in rows:
                row = dict(row)
                if row["packet_id"] == "P_UPDATE":
                    row["title"] = "Old selected title"
                stale_rows.append(row)
            write_baked_lore_csv(root, stale_rows)

            stats = export_targeted(
                root,
                ("P_UPDATE",),
                include_all=False,
                explicit_packet_sources=(),
                dry_run=False,
                validate_only=False,
                source_only=True,
                refresh_indexes=False,
                update_baked_csv=True,
            )

            self.assertEqual(stats.baked_csv_rows_targeted, len(TARGET_LOCALES))
            self.assertTrue(stats.baked_csv_written)
            actual_rows = read_baked_lore_packet_rows(
                root / "Assets" / "_SourceData" / "DataMonolith" / "Narrative" / "applied_lore_packets.csv"
            )
            selected_titles = {row["title"] for row in actual_rows if row["packet_id"] == "P_UPDATE"}
            keep_titles = {row["title"] for row in actual_rows if row["packet_id"] == "P_KEEP"}
            self.assertEqual(selected_titles, {"Title"})
            self.assertEqual(keep_titles, {"Title"})
            self.assertEqual(len(actual_rows), len(TARGET_LOCALES) * 2)

    def test_validate_selected_pages_uses_packet_metadata_draft_flags(self):
        with temporary_directory() as tmp:
            base = Path(tmp) / "Docs" / "Lore" / "AppliedContent"
            packet = {
                "packet_id": "P_TEST_PAGE",
                "release_set_id": "RS_TEST",
                "article_id": "test.page",
                "_manifest_status": "production_facing_draft_pending_native_localization",
                "surface_mask": 16,
                "localized": complete_localized(),
            }

            for locale in TARGET_LOCALES:
                path = base / "in_game_wiki" / locale / "P_TEST_PAGE.md"
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(
                    render_page(base, packet, locale, "in_game_wiki", "In-Game Wiki", {}),
                    encoding="utf-8",
                )

            validate_selected_pages(base, [packet])

    def test_page_freshness_check_catches_fresh_corrupt_rendered_text(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            base = root / "Docs" / "Lore" / "AppliedContent"
            packet_dir = base / "packets"
            release_dir = base / "release_sets"
            packet_dir.mkdir(parents=True)
            release_dir.mkdir(parents=True)
            packet = {
                "packet_id": "P_FRESH_CORRUPT",
                "release_set_id": "RS_TEST",
                "article_id": "test.fresh_corrupt",
                # Default-deny publish gate: without a positive in-world declaration no page is written, so
                # there would be nothing on disk for the corruption check to find.
                "content_class": "in_world_artifact",
                "localized": complete_localized(),
            }
            packet["localized"]["fr_FR"]["external_site_article"] = (
                "Draft fr_FR localization pending native pass. L??conomie de route sans FTL garde "
                "HECTON-8 honn?te."
            )
            packet_dir.joinpath("RS_TEST.packets.json").write_text(
                json.dumps({"release_set_id": "RS_TEST", "packets": [packet]}),
                encoding="utf-8",
            )
            release_dir.joinpath("RS_TEST_manifest.json").write_text(
                json.dumps(
                    {
                        "release_set_id": "RS_TEST",
                        "status": "production_facing_draft_pending_native_localization",
                        "canonical_importer_ready": True,
                        "packets": ["P_FRESH_CORRUPT"],
                        "packet_sources": ["Docs/Lore/AppliedContent/packets/RS_TEST.packets.json"],
                    }
                ),
                encoding="utf-8",
            )
            graph_path = base / NAVIGATION_CLUSTER_GRAPH_PATH
            graph_path.parent.mkdir(parents=True, exist_ok=True)
            with graph_path.open("w", encoding="utf-8", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=NAVIGATION_CLUSTER_GRAPH_HEADERS, lineterminator="\n")
                writer.writeheader()
                writer.writerow(
                    {
                        "packet_id": "P_FRESH_CORRUPT",
                        "arc_id": "test",
                        "depth_band": "test",
                        "route_moment": "first_test",
                        "prereq_packet_ids": "",
                        "next_packet_ids": "",
                        "evidence_type": "test",
                        "truth_claim": "test",
                        "player_decision": "test",
                        "spoiler_tier": "public",
                        "primary_surface": "external_site",
                    }
                )
            export_pages(root, overwrite=True, packet_glob="P_FRESH_CORRUPT")

            stats = check_publication_freshness(root, "P_FRESH_CORRUPT")

            self.assertGreater(stats.integrity_issues, 0)


if __name__ == "__main__":
    unittest.main()
