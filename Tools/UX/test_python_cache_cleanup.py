#!/usr/bin/env python3
"""Unit tests for UX Python cache cleanup tooling."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from Tools.UX.clean_python_cache import find_pycache_dirs, remove_pycache_dirs


class PythonCacheCleanupTests(unittest.TestCase):
    def test_find_and_remove_pycache_dirs_under_root(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root:
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
