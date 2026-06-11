#!/usr/bin/env python3
"""Regression tests for the Grand Library Localization Injector."""

from __future__ import annotations

import json
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

# Add the parent directory so we can import Grand_Library_Loc_Injector
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import Grand_Library_Loc_Injector


class TestGrandLibraryLocInjector(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.mkdtemp()
        self.grand_library_dir = os.path.join(self.temp_dir, "Grand_Library")
        self.loc_dir = os.path.join(self.temp_dir, "Localization")

        os.makedirs(self.grand_library_dir)
        os.makedirs(self.loc_dir)

        # Patch the directory constants used by the module
        self.patcher_gl = mock.patch("Grand_Library_Loc_Injector.GRAND_LIBRARY_DIR", self.grand_library_dir)
        self.patcher_loc = mock.patch("Grand_Library_Loc_Injector.LOC_DIR", self.loc_dir)

        self.patcher_gl.start()
        self.patcher_loc.start()

    def tearDown(self) -> None:
        self.patcher_gl.stop()
        self.patcher_loc.stop()
        shutil.rmtree(self.temp_dir)

    def test_inject_library_happy_path(self) -> None:
        loc_file = os.path.join(self.loc_dir, "en_US.json")
        with open(loc_file, "w", encoding="utf-8") as f:
            json.dump({"strings": {"existing.key": "Existing value"}}, f)

        md_file = os.path.join(self.grand_library_dir, "01_ASTRONOMY_AND_HISTORY_en_US.md")
        with open(md_file, "w", encoding="utf-8") as f:
            f.write("# Astronomy\nSpace is big.")

        with mock.patch("sys.stdout"):
            Grand_Library_Loc_Injector.inject_library()

        with open(loc_file, "r", encoding="utf-8") as f:
            data = json.load(f)

        self.assertIn("strings", data)
        self.assertIn("existing.key", data["strings"])
        self.assertIn("ns.lore.grand_library.01_astronomy_and_history", data["strings"])
        self.assertEqual(data["strings"]["ns.lore.grand_library.01_astronomy_and_history"], "# Astronomy\nSpace is big.")

    def test_inject_library_creates_strings_dict(self) -> None:
        loc_file = os.path.join(self.loc_dir, "fr_FR.json")
        with open(loc_file, "w", encoding="utf-8") as f:
            json.dump({"other_key": "value"}, f)

        md_file = os.path.join(self.grand_library_dir, "02_BIOLOGY_fr_FR.md")
        with open(md_file, "w", encoding="utf-8") as f:
            f.write("# Biology\nLife is complex.")

        with mock.patch("sys.stdout"):
            Grand_Library_Loc_Injector.inject_library()

        with open(loc_file, "r", encoding="utf-8") as f:
            data = json.load(f)

        self.assertIn("strings", data)
        self.assertEqual(data["strings"]["ns.lore.grand_library.02_biology"], "# Biology\nLife is complex.")

    def test_inject_library_skips_missing_loc_file(self) -> None:
        md_file = os.path.join(self.grand_library_dir, "03_PHYSICS_de_DE.md")
        with open(md_file, "w", encoding="utf-8") as f:
            f.write("Physics")

        with mock.patch("sys.stdout"):
            Grand_Library_Loc_Injector.inject_library()

        self.assertFalse(os.path.exists(os.path.join(self.loc_dir, "de_DE.json")))

    def test_inject_library_handles_empty_grand_library(self) -> None:
        loc_file = os.path.join(self.loc_dir, "en_US.json")
        with open(loc_file, "w", encoding="utf-8") as f:
            json.dump({"strings": {}}, f)

        with mock.patch("sys.stdout"):
            Grand_Library_Loc_Injector.inject_library()

        with open(loc_file, "r", encoding="utf-8") as f:
            data = json.load(f)
        self.assertEqual(data, {"strings": {}})


    def test_inject_library_missing_grand_library_dir(self) -> None:
        shutil.rmtree(self.grand_library_dir)
        with mock.patch("sys.stdout"):
            Grand_Library_Loc_Injector.inject_library()
        self.assertFalse(os.path.exists(self.grand_library_dir))

    def test_inject_library_invalid_language_format(self) -> None:
        # Create a localization file that shouldn't be touched because the language format is invalid
        loc_file = os.path.join(self.loc_dir, "en_US.json")
        with open(loc_file, "w", encoding="utf-8") as f:
            json.dump({"strings": {}}, f)

        # Invalid formats
        invalid_mds = [
            "no_underscore.md",
            "just_one_part.md",
            "wrong_lang_format_en.md",  # length not 5
            "wrong_lang_format_en-US.md", # lang[2] is not '_'
        ]

        for invalid_md in invalid_mds:
            with open(os.path.join(self.grand_library_dir, invalid_md), "w", encoding="utf-8") as f:
                f.write("# Content")

        with mock.patch("sys.stdout"):
            Grand_Library_Loc_Injector.inject_library()

        # Check that no keys were added to the en_US loc file, since the language format was not detected
        with open(loc_file, "r", encoding="utf-8") as f:
            data = json.load(f)

        self.assertEqual(data["strings"], {})

if __name__ == "__main__":
    unittest.main()
