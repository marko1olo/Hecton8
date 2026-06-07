#!/usr/bin/env python3
import json
import os
import sys
import unittest
from pathlib import Path
from unittest.mock import patch
from io import StringIO

TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

from test_local_temp import project_local_tempdir_factory  # noqa: E402

import AppliedLorePublicationGuard as guard  # noqa: E402

temporary_directory = project_local_tempdir_factory("applied_lore_publication_guard_tests")

class TestAppliedLorePublicationGuard(unittest.TestCase):
    def setUp(self):
        self.temp_dir = temporary_directory()
        self.root = Path(self.temp_dir.name)
        self.wiki_dir = self.root / "Docs" / "Lore" / "AppliedContent" / "in_game_wiki"
        self.site_dir = self.root / "Docs" / "Lore" / "AppliedContent" / "external_site"
        
    def tearDown(self):
        self.temp_dir.cleanup()

    def create_page(self, surface, locale, filename, fm_data, body="Body content"):
        path = (self.wiki_dir if surface == "wiki" else self.site_dir) / locale / filename
        path.parent.mkdir(parents=True, exist_ok=True)
        fm_lines = ["---"]
        for k, v in fm_data.items():
            fm_lines.append(f"{k}: {v}")
        fm_lines.append("---")
        fm_lines.append(body)
        path.write_text("\n".join(fm_lines), encoding="utf-8")
        
    def get_valid_fm(self, packet_id="P123"):
        return {
            "packet_id": packet_id,
            "release_set_id": "R1",
            "article_id": "A1",
            "unlock_id": "U1",
            "localization_status": "native_reviewed",
            "localization_flags": "none",
            "spoiler_tier": "0",
            "source_voice": "narrator",
            "title": "Title"
        }

    def populate_surface_all_locales(self, surface, packet_id="P123", base_body="body", fm_overrides=None):
        for loc in guard.TARGET_LOCALES:
            fm = self.get_valid_fm(packet_id)
            if loc != "en_US":
                fm["localization_status"] = "native_reviewed"
                fm["proof_marker"] = "proof"
            if fm_overrides:
                fm.update(fm_overrides)
            self.create_page(surface, loc, f"{packet_id}.md", fm, base_body + loc)

    def populate_all_locales(self, packet_id="P123", base_body="body"):
        self.populate_surface_all_locales("wiki", packet_id, base_body)

    def test_pass(self):
        self.populate_all_locales()
        ret = guard.run_guard(self.root, "", False)
        self.assertEqual(ret, 0)

    def test_missing_required_key(self):
        fm = self.get_valid_fm()
        del fm["title"]
        self.create_page("wiki", "en_US", "P123.md", fm)
        
        with patch('sys.stdout', new=StringIO()) as out:
            ret = guard.run_guard(self.root, "", False)
        self.assertEqual(ret, 1)
        self.assertIn("Missing or empty required keys: ['title']", out.getvalue())

    def test_locale_gap(self):
        fm = self.get_valid_fm()
        self.create_page("wiki", "en_US", "P123.md", fm)
        
        with patch('sys.stdout', new=StringIO()) as out:
            ret = guard.run_guard(self.root, "", False)
        self.assertEqual(ret, 1)
        self.assertIn("Missing TARGET_LOCALES:", out.getvalue())
        self.assertIn("ru_RU", out.getvalue())

    def test_ready_non_english_without_proof(self):
        self.populate_all_locales()
        fm = self.get_valid_fm()
        fm["localization_status"] = "native_reviewed"
        # no proof_marker
        self.create_page("wiki", "fr_FR", "P123.md", fm)
        
        with patch('sys.stdout', new=StringIO()) as out:
            ret = guard.run_guard(self.root, "", False)
        self.assertEqual(ret, 1)
        self.assertIn("Non-English page claims ready status 'native_reviewed' without proof_marker", out.getvalue())

    def test_anti_ai_phrase_fail(self):
        self.populate_all_locales(base_body="This entry explores something cool.")
        with patch('sys.stdout', new=StringIO()) as out:
            ret = guard.run_guard(self.root, "", False)
        self.assertEqual(ret, 1)
        self.assertIn("Anti-AI prose detected: ['this entry explores']", out.getvalue())

    def test_clone_fail(self):
        self.populate_all_locales(base_body="Identical body")
        self.populate_surface_all_locales("site", base_body="External body")
        fm = self.get_valid_fm()
        self.create_page("site", "en_US", "P123.md", fm, "Identical bodyen_US")
        
        with patch('sys.stdout', new=StringIO()) as out:
            ret = guard.run_guard(self.root, "", False)
        self.assertEqual(ret, 1)
        self.assertIn("external_site and in_game_wiki bodies are exact clones in ready state", out.getvalue())

    def test_draft_clone_warning(self):
        self.populate_all_locales(base_body="Identical body")
        self.populate_surface_all_locales("site", base_body="External body", fm_overrides={"localization_status": "draft"})
        fm = self.get_valid_fm()
        fm["localization_status"] = "draft"
        self.create_page("wiki", "en_US", "P123.md", fm, "Identical bodyen_US")
        self.create_page("site", "en_US", "P123.md", fm, "Identical bodyen_US")
        
        with patch('sys.stdout', new=StringIO()) as out:
            ret = guard.run_guard(self.root, "", False)
        self.assertEqual(ret, 0)
        self.assertIn("external_site and in_game_wiki bodies are exact clones (draft warning)", out.getvalue())

    def test_spoiler_marker_pass(self):
        self.populate_all_locales()
        self.populate_surface_all_locales(
            "site",
            base_body="External page [SPOILER] ",
            fm_overrides={"spoiler_tier": "3"})
        fm = self.get_valid_fm()
        fm["spoiler_tier"] = "3"
        self.create_page("site", "en_US", "P123.md", fm, "Body with [SPOILER]")
        
        with patch('sys.stdout', new=StringIO()) as out:
            ret = guard.run_guard(self.root, "", False)
        self.assertEqual(ret, 0)

    def test_spoiler_marker_fail(self):
        self.populate_all_locales()
        self.populate_surface_all_locales(
            "site",
            base_body="External page [SPOILER] ",
            fm_overrides={"spoiler_tier": "3"})
        fm = self.get_valid_fm()
        fm["spoiler_tier"] = "3"
        self.create_page("site", "en_US", "P123.md", fm, "Body without marker")
        
        with patch('sys.stdout', new=StringIO()) as out:
            ret = guard.run_guard(self.root, "", False)
        self.assertEqual(ret, 1)
        self.assertIn("missing spoiler marker", out.getvalue())

if __name__ == '__main__':
    unittest.main()
