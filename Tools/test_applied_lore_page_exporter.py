import sys
from test_temp_root import temporary_directory
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parent))
from AppliedLoreImporter import TARGET_LOCALES
from AppliedLorePageExporter import (
    PUBLICATION_INDEX_HEADERS,
    check_publication_freshness,
    export_pages,
    publication_cluster_rows,
    publication_surface_rows,
    render_index,
    render_page,
    resolve_source_voice,
    resolve_spoiler_tier,
    status_bucket_from_status,
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


def page_export_packet() -> dict:
    return {
        "packet_id": "P_TEST_PAGE",
        "release_set_id": "RS_TEST",
        "article_id": "test.page",
        "_source_path": str(Path("Docs") / "Lore" / "AppliedContent" / "packets" / "RS_TEST.packets.json"),
        "surface_mask": 16,
        # The publish gate is default-deny: a packet must positively declare itself in-world before it may
        # reach in_game_wiki or external_site. Real in-world packets carry this, so the fixture carries it.
        "content_class": "in_world_artifact",
        "unlock": {
            "primary": "unlock.test.page",
            "poi_tags": ["poi.correct", "poi.second"],
            "biome_tags": ["biome.correct", "biome.second"],
        },
        "localized": complete_localized(),
    }

class TestAppliedLorePageExporter(unittest.TestCase):
    def test_render_page_frontmatter_and_spoiler(self):
        base = Path(".")
        packet = {
            "packet_id": "P123",
            "release_set_id": "RS001",
            "article_id": "A1",
            "localized": {
                "en_US": {
                    "title": "Test Title",
                    "external_site": "External body"
                }
            },
            "metadata": {
                "source_voice": "Custom Voice"
            }
        }
        cluster_spoiler_tiers = {"P123": "3"}

        rendered = render_page(base, packet, "en_US", "external_site", "External Site", cluster_spoiler_tiers)

        # rendered frontmatter contains title, source_voice, spoiler_tier
        self.assertIn('title: "Test Title"', rendered)
        self.assertIn('source_voice: Custom Voice', rendered)
        self.assertIn('spoiler_tier: 3', rendered)

        # explicit packet metadata wins over fallback
        self.assertIn('source_voice: Custom Voice', rendered)

        # external site page with tier >= 3 emits spoiler marker
        self.assertIn('spoiler_warning: archive_spoilers', rendered)

    def test_packet_draft_status_sets_non_english_page_and_index_flags(self):
        base = Path(".")
        packet = page_export_packet()
        packet["status"] = "production_facing_draft_pending_native_localization"

        english = render_page(base, packet, "en_US", "in_game_wiki", "In-Game Wiki", {})
        russian = render_page(base, packet, "ru_RU", "in_game_wiki", "In-Game Wiki", {})
        index = render_index([packet], "ru_RU", "in_game_wiki", "in_game_wiki")
        rows = publication_surface_rows(base, [packet])
        russian_row = next(
            row for row in rows
            if row["packet_id"] == "P_TEST_PAGE" and row["locale"] == "ru_RU" and row["surface"] == "in_game_wiki"
        )

        self.assertIn("localization_flags: 0", english)
        self.assertIn("localization_status: draft_machine_or_llm", russian)
        self.assertIn("localization_flags: 1", russian)
        self.assertIn("draft_marker_pages: 1", index)
        self.assertEqual(russian_row["localization_flags"], "1")

    def test_export_pages_rewrites_stale_frontmatter_even_when_localization_matches(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            base = root / "Docs" / "Lore" / "AppliedContent"
            path = base / "in_game_wiki" / "en_US" / "P_TEST_PAGE.md"
            path.parent.mkdir(parents=True)
            path.write_text(
                "---\n"
                "packet_id: P_TEST_PAGE\n"
                "poi_tags: poi.stale\n"
                "locale: en_US\n"
                "surface: in_game_wiki\n"
                "localization_status: source_authority\n"
                "localization_flags: 0\n"
                "---\n"
                "# Test Title\n",
                encoding="utf-8",
            )

            with patch("AppliedLorePageExporter.collect_packets", return_value=[page_export_packet()]), patch(
                "AppliedLorePageExporter.navigation_cluster_graph_rows",
                return_value=[],
            ):
                written, _skipped, _removed_disabled, _indexes_written = export_pages(root, overwrite=False)

            self.assertGreaterEqual(written, 1)
            self.assertIn("poi_tags: poi.correct;poi.second", path.read_text(encoding="utf-8"))

    def test_publication_check_reports_stale_file_without_writing(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            base = root / "Docs" / "Lore" / "AppliedContent"
            path = base / "in_game_wiki" / "en_US" / "P_TEST_PAGE.md"
            path.parent.mkdir(parents=True)
            stale_text = (
                "---\n"
                "packet_id: P_TEST_PAGE\n"
                "poi_tags: poi.stale\n"
                "locale: en_US\n"
                "surface: in_game_wiki\n"
                "localization_status: source_authority\n"
                "localization_flags: 0\n"
                "---\n"
                "# Test Title\n"
            )
            path.write_text(stale_text, encoding="utf-8")

            with patch("AppliedLorePageExporter.collect_packets", return_value=[page_export_packet()]), patch(
                "AppliedLorePageExporter.navigation_cluster_graph_rows",
                return_value=[],
            ):
                stats = check_publication_freshness(root)

            self.assertGreaterEqual(stats.stale_files, 1)
            self.assertIn("stale: Docs/Lore/AppliedContent/in_game_wiki/en_US/P_TEST_PAGE.md", stats.sample_issues)
            self.assertEqual(path.read_text(encoding="utf-8"), stale_text)

    def test_publication_check_and_export_reject_orphan_generated_pages(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            base = root / "Docs" / "Lore" / "AppliedContent"
            orphan = base / "in_game_wiki" / "en_US" / "P_ORPHAN.md"
            orphan.parent.mkdir(parents=True)
            orphan.write_text(
                "---\n"
                "packet_id: P_ORPHAN\n"
                "locale: en_US\n"
                "surface: in_game_wiki\n"
                "source: AppliedContent packet JSON\n"
                "runtime_reads_markdown: false\n"
                "---\n"
                "# Orphan\n",
                encoding="utf-8",
            )

            unlinked: list[Path] = []

            def capture_unlink(path: Path) -> None:
                unlinked.append(path)

            with patch("AppliedLorePageExporter.collect_packets", return_value=[page_export_packet()]), patch(
                "AppliedLorePageExporter.navigation_cluster_graph_rows",
                return_value=[],
            ), patch(
                "pathlib.Path.unlink",
                capture_unlink,
            ):
                stats = check_publication_freshness(root)
                _written, _skipped, removed_disabled, _indexes_written = export_pages(root, overwrite=True)

            self.assertEqual(stats.disabled_generated_pages, 1)
            self.assertIn("orphan-generated: Docs/Lore/AppliedContent/in_game_wiki/en_US/P_ORPHAN.md", stats.sample_issues)
            self.assertEqual(removed_disabled, 1)
            self.assertEqual(unlinked, [orphan])

    def test_publish_gate_denies_production_metadata_and_allows_in_world(self):
        """The gate that stops the project publishing its own design docs to the player.

        P217_IN_GAME_WIKI_UNLOCK_TIER_RULES - the in-game wiki's own unlock rules, published in the wiki, in
        15 locales, as a thing the player unlocks - is why this exists. Without a permanent test the gate is
        one refactor away from silently reopening.
        """
        for content_class, expect_pages in (
            ("in_world_artifact", True),
            ("production_metadata", False),
            ("in_world", False),  # unrecognized value is not a positive declaration
        ):
            with self.subTest(content_class=content_class):
                with temporary_directory() as tmp:
                    root = Path(tmp)
                    packet = page_export_packet()
                    packet["content_class"] = content_class
                    with patch("AppliedLorePageExporter.collect_packets", return_value=[packet]), patch(
                        "AppliedLorePageExporter.navigation_cluster_graph_rows",
                        return_value=[],
                    ):
                        written, _skipped, removed_disabled, _indexes = export_pages(root, overwrite=True)

                    # Nothing is ever deleted by the gate; denial only withholds creation.
                    self.assertEqual(removed_disabled, 0)
                    if expect_pages:
                        self.assertGreater(written, 0)
                    else:
                        self.assertEqual(written, 0)

    def test_publish_gate_reads_declaration_from_metadata_block(self):
        """resolve_source_voice looks in metadata before the packet root; the gate must match that order."""
        with temporary_directory() as tmp:
            root = Path(tmp)
            packet = page_export_packet()
            del packet["content_class"]
            packet["metadata"] = {"content_class": "in_world_artifact"}
            with patch("AppliedLorePageExporter.collect_packets", return_value=[packet]), patch(
                "AppliedLorePageExporter.navigation_cluster_graph_rows",
                return_value=[],
            ):
                written, _skipped, _removed, _indexes = export_pages(root, overwrite=True)

            self.assertGreater(written, 0)

    def test_export_pages_counts_only_changed_indexes(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            with patch("AppliedLorePageExporter.collect_packets", return_value=[page_export_packet()]), patch(
                "AppliedLorePageExporter.navigation_cluster_graph_rows",
                return_value=[],
            ):
                first = export_pages(root, overwrite=False)
                second = export_pages(root, overwrite=False)

            self.assertEqual(first[3], len(TARGET_LOCALES) * 2)
            self.assertEqual(second[0], 0)
            self.assertEqual(second[3], 0)

    def test_targeted_export_does_not_rewrite_global_indexes(self):
        with temporary_directory() as tmp:
            root = Path(tmp)
            base = root / "Docs" / "Lore" / "AppliedContent"
            index_path = base / "Publication_Surface_Index.csv"
            index_path.parent.mkdir(parents=True)
            index_text = "sentinel global index\n"
            index_path.write_text(index_text, encoding="utf-8")

            with patch("AppliedLorePageExporter.collect_packets", return_value=[page_export_packet()]), patch(
                "AppliedLorePageExporter.navigation_cluster_graph_rows",
                return_value=[],
            ):
                written, _skipped, _removed_disabled, indexes_written = export_pages(
                    root,
                    overwrite=True,
                    packet_glob="P_TEST_PAGE",
                )

            self.assertGreater(written, 0)
            self.assertEqual(indexes_written, 0)
            self.assertEqual(index_path.read_text(encoding="utf-8"), index_text)

    def test_surface_source_voice_and_packet_tier_metadata_win(self):
        packet = {
            "packet_id": "P459",
            "metadata": {
                "spoiler_tier": "3",
                "source_voice_by_surface": {
                    "external_site": "Website Archive",
                    "in_game_wiki": "Neutral Reference"
                }
            }
        }

        self.assertEqual(resolve_source_voice(packet, "external_site"), "Website Archive")
        self.assertEqual(resolve_source_voice(packet, "in_game_wiki"), "Neutral Reference")
        self.assertEqual(resolve_spoiler_tier(packet, {"P459": "0"}), "3")


    def test_status_bucket_mapping(self):
        self.assertEqual(status_bucket_from_status("source_authority"), "ready")
        self.assertEqual(status_bucket_from_status("native_reviewed"), "ready")
        self.assertEqual(status_bucket_from_status("draft_machine_or_llm"), "draft")
        self.assertEqual(status_bucket_from_status("blocked_on_lore"), "blocked")
        self.assertEqual(status_bucket_from_status(""), "missing")

    def test_publication_index_headers(self):
        self.assertIn("source_voice", PUBLICATION_INDEX_HEADERS)
        self.assertIn("spoiler_tier", PUBLICATION_INDEX_HEADERS)
        self.assertIn("spoiler_warning", PUBLICATION_INDEX_HEADERS)
        self.assertIn("packet_json_path", PUBLICATION_INDEX_HEADERS)
        self.assertIn("status_bucket", PUBLICATION_INDEX_HEADERS)

    def test_publication_cluster_rows_skip_noncanonical_staged_graph_packet(self):
        staged_cluster = {
            "packet_id": "P_STAGED_NAV_CLUSTER",
            "route_moment": "first_staged_nav_cluster",
            "spoiler_tier": "1",
            "primary_surface": "external_site",
            "prereq_packet_ids": "",
            "next_packet_ids": "",
            "truth_claim": "Staged cluster is not active until the packet is canonical.",
            "player_decision": "Should staged source publish?",
        }

        with patch("AppliedLorePageExporter.navigation_cluster_graph_rows", return_value=[staged_cluster]):
            rows = publication_cluster_rows(Path("."), [page_export_packet()])

        self.assertEqual(rows, [])

    def test_publication_surface_rows_resolution(self):
        base = Path(".")
        packets = [
            {
                "packet_id": "P_TEST",
                "release_set_id": "RS001",
                "_source_path": str(base / "packets" / "RS001_bundle.packets.json"),
                "surface_mask": 32, # external_site = 1 << 5
                "localized": {
                    "en_US": {
                        "title": "Test Title"
                    }
                },
                "metadata": {
                    "source_voice": "Site Output",
                    "spoiler_tier": "4"
                }
            }
        ]

        # Patch SURFACES directly or rely on the filter logic
        rows = publication_surface_rows(base, packets)
        en_row = next(r for r in rows if r["packet_id"] == "P_TEST" and r["locale"] == "en_US")

        self.assertEqual(en_row["source_voice"], "Site Output")
        self.assertEqual(en_row["spoiler_tier"], "4")
        self.assertEqual(en_row["spoiler_warning"], "archive_spoilers")
        self.assertEqual(en_row["status_bucket"], "ready") # en_US is source_authority
        self.assertEqual(Path(en_row["packet_json_path"]).as_posix(), "packets/RS001_bundle.packets.json")

if __name__ == '__main__':
    unittest.main()
