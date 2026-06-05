import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from AppliedLorePageExporter import render_page, resolve_source_voice, resolve_spoiler_tier

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

if __name__ == '__main__':
    unittest.main()
