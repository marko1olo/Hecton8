#!/usr/bin/env python3
"""Regression tests for Gemini inventory prompt generator."""

import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import sys
sys.path.insert(0, str(Path(__file__).parent))
import InventoryGeminiPromptFromGapAudit as gemini_prompt


class TestInventoryGeminiPromptFromGapAudit(unittest.TestCase):
    def test_display_path(self):
        with tempfile.TemporaryDirectory() as tmp_str:
            tmp = Path(tmp_str)
            with patch.object(gemini_prompt, "ROOT", tmp):
                inside_path = tmp / "test_dir" / "file.txt"
                self.assertEqual(gemini_prompt.display_path(inside_path), "test_dir/file.txt")

                # Test fallback when path is not relative to ROOT
                # Use a path definitely outside tmp
                outside_path = Path("/tmp") / "outside_file.txt"
                self.assertEqual(gemini_prompt.display_path(outside_path), str(outside_path))

    def test_number_word(self):
        self.assertEqual(gemini_prompt.number_word(1), "one")
        self.assertEqual(gemini_prompt.number_word(12), "twelve")
        self.assertEqual(gemini_prompt.number_word(99), "99")

    def test_position_label(self):
        # 3x4 grid mapping tests
        # index 1 -> row 0, col 0 -> top-left
        self.assertEqual(gemini_prompt.position_label(1, 4, 3), "top-left position")
        # index 4 -> row 0, col 3 -> top-right
        self.assertEqual(gemini_prompt.position_label(4, 4, 3), "top-right position")
        # index 8 -> row 1, col 3 -> middle-right
        self.assertEqual(gemini_prompt.position_label(8, 4, 3), "middle-right position")

        # Non-3x4 grid mapping test
        self.assertEqual(gemini_prompt.position_label(1, 2, 2), "reading-order position one")

    def test_sanitize_prompt_phrase(self):
        self.assertEqual(gemini_prompt.sanitize_prompt_phrase("  oxygen tank , no text  "), "oxygen tank")
        self.assertEqual(gemini_prompt.sanitize_prompt_phrase("battery with no screen text"), "battery")
        self.assertEqual(gemini_prompt.sanitize_prompt_phrase("  plain phrase  "), "plain phrase")

    def test_parse_force_ids(self):
        self.assertEqual(
            gemini_prompt.parse_force_ids(["Item_1, Item_2", "Item_1, ,Item_3"]),
            ("Item_1", "Item_2", "Item_3")
        )
        self.assertEqual(gemini_prompt.parse_force_ids([]), ())
        self.assertEqual(gemini_prompt.parse_force_ids([" , "]), ())


    def test_render_prompt(self):
        spec = {"items": [{"index": 1, "promptPhrase": "oxygen tank"}]}
        result = gemini_prompt.render_prompt(spec, "dummy.png")
        self.assertIn("`dummy.png`", result)
        self.assertIn("reading-order position one: oxygen tank", result)
        self.assertIn("Generate one distinct AA-quality physical objects", result)

    def test_lint_prompt(self):
        # Should not raise exception
        gemini_prompt.lint_prompt("A valid prompt with oxygen tank.")

    def test_lint_prompt_banned_pattern(self):
        with self.assertRaisesRegex(RuntimeError, "Prompt lint failed: visible cell number: Cell "):
            gemini_prompt.lint_prompt("A prompt mentioning Cell 1.")

    def test_lint_prompt_negative_item_line(self):
        with self.assertRaisesRegex(RuntimeError, "Prompt lint failed: negative phrase inside item line: position: oxygen no text"):
            gemini_prompt.lint_prompt("position: oxygen no text")

if __name__ == '__main__':
    unittest.main()
