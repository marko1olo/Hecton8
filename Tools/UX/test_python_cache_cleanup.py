#!/usr/bin/env python3
"""Unit tests for UX Python cache cleanup tooling."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from Tools.UX.clean_python_cache import find_pycache_dirs, remove_pycache_dirs
from Tools.test_local_temp import project_local_tempdir_factory


TEMP_DIR = project_local_tempdir_factory("ux_python_cache_cleanup")


class PythonCacheCleanupTests(unittest.TestCase):
    def test_find_and_remove_pycache_dirs_under_root(self) -> None:
        with TEMP_DIR() as temp_root:
            root = Path(temp_root)
            cache_a = root / "Tools" / "UX" / "__pycache__"
            cache_b = root / "Tools" / "__pycache__"
            normal_dir = root / "Tools" / "UX" / "not_cache"
            cache_a.mkdir(parents=True)
            cache_b.mkdir(parents=True)
            normal_dir.mkdir(parents=True)
            (cache_a / "a.pyc").write_bytes(b"cache")
            (cache_b / "b.pyc").write_bytes(b"cache")

            found = find_pycache_dirs(root)
            self.assertEqual([cache_b, cache_a], sorted(found))

            removed = remove_pycache_dirs(root)

            self.assertEqual(2, len(removed))
            self.assertFalse(cache_a.exists())
            self.assertFalse(cache_b.exists())
            self.assertTrue(normal_dir.exists())


if __name__ == "__main__":
    unittest.main()
