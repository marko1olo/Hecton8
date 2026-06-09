import unittest
import os
import sys
import csv
from pathlib import Path

# Need to make sure the h8_lda_pipeline.py from the root is importable
# Add the project root to sys.path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from h8_lda_pipeline import H8LDAPipeline, LDAException

class TestH8LDAPipeline(unittest.TestCase):
    def setUp(self):
        self.out_dir = Path("test_out_dir")
        self.pipeline = H8LDAPipeline(out_dir=self.out_dir)

    def test_tone_check_pass(self):
        packet = {
            "id": "P_TEST_VALID",
            "title_en": "System Audit",
            "codex_en": "Deep Reach liability doctrine activated.",
            "title_ru": "Системный аудит",
            "codex_ru": "Доктрина ответственности."
        }
        self.pipeline.add_packet(packet)
        self.assertEqual(len(self.pipeline.packets), 1)

    def test_tone_check_fail_en(self):
        packet = {
            "id": "P_TEST_INVALID",
            "title_en": "System Audit",
            "codex_en": "This serves as a reminder of our failure.",
        }
        with self.assertRaises(LDAException):
            self.pipeline.add_packet(packet)

    def test_tone_check_fail_ru(self):
        packet = {
            "id": "P_TEST_INVALID",
            "title_ru": "Системный аудит",
            "codex_ru": "это служит напоминанием о нашей неудаче",
        }
        with self.assertRaises(LDAException):
            self.pipeline.add_packet(packet)

    def test_execute_build_generates_csvs(self):
        packet = {
            "id": "P_TEST_VALID",
            "title_en": "System Audit",
            "codex_en": "Deep Reach liability doctrine activated."
        }
        self.pipeline.add_packet(packet)
        self.pipeline.add_term("Term_EN", "Term_RU")
        self.pipeline.add_crosslink("P1", "P2", "edge", "term", "relation", "reason")

        self.pipeline.execute_build("RS_TEST")

        # Verify output structure
        backlog_dir = self.out_dir / "localization_backlog"
        route_cards_dir = self.out_dir / "route_cards"
        self.assertTrue(backlog_dir.exists())
        self.assertTrue(route_cards_dir.exists())

        # Check for specific files
        self.assertTrue(list(backlog_dir.glob("RS_TEST_15_LOCALE_ROWS_*.csv")))
        self.assertTrue(list(backlog_dir.glob("RS_TEST_TERMINOLOGY_LOCK_TABLE_*.csv")))
        self.assertTrue(list(backlog_dir.glob("RS_TEST_LOCALIZATION_QA_MATRIX_*.csv")))
        self.assertTrue(list(route_cards_dir.glob("RS_TEST_route_cards.csv")))
        self.assertTrue(list(route_cards_dir.glob("RS_TEST_crosslink_graph.csv")))

    def tearDown(self):
        if self.out_dir.exists():
            for root, dirs, files in os.walk(self.out_dir, topdown=False):
                for name in files:
                    os.remove(os.path.join(root, name))
                for name in dirs:
                    os.rmdir(os.path.join(root, name))
            os.rmdir(self.out_dir)

if __name__ == '__main__':
    unittest.main()
