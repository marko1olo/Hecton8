import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from AppliedLorePageExporter import render_page, resolve_source_voice, resolve_spoiler_tier, status_bucket_from_status, PUBLICATION_INDEX_HEADERS, publication_surface_rows

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
