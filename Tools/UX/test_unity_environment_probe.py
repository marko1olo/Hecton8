#!/usr/bin/env python3
"""Unit tests for Unity environment probing."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from Tools.UX.probe_unity_environment import (
    build_candidate_details,
    find_unity_candidates,
    infer_unity_version_from_path,
    read_required_unity_version,
    resolve_probe_status,
)


class UnityEnvironmentProbeTests(unittest.TestCase):
    def test_reads_project_version(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root:
            version_path = Path(temp_root) / "ProjectVersion.txt"
            version_path.write_text("m_EditorVersion: 6000.4.1f1\n", encoding="utf-8")

            self.assertEqual("6000.4.1f1", read_required_unity_version(version_path))

    def test_missing_project_version_returns_empty_string(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root:
            self.assertEqual("", read_required_unity_version(Path(temp_root) / "missing.txt"))

    def test_finds_unity_candidate_in_supplied_root(self) -> None:
        with tempfile.TemporaryDirectory() as temp_root:
            root = Path(temp_root)
            unity_exe = root / "6000.4.1f1" / "Editor" / "Unity.exe"
            unity_exe.parent.mkdir(parents=True)
            unity_exe.write_bytes(b"")

            candidates = find_unity_candidates((root,))

            self.assertIn(str(unity_exe.resolve()), candidates)

    def test_infers_unity_version_from_candidate_path(self) -> None:
        path = Path("C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe")

        self.assertEqual("6000.4.1f1", infer_unity_version_from_path(path))

    def test_candidate_details_mark_required_version_match(self) -> None:
        candidate = str(Path("C:/Unity/6000.4.1f1/Editor/Unity.exe"))
        details = build_candidate_details([candidate], "6000.4.1f1")

        self.assertTrue(details[0]["matchesRequiredVersion"])

    def test_probe_status_reports_version_mismatch(self) -> None:
        candidate = str(Path("C:/Unity/6000.4.0f1/Editor/Unity.exe"))
        details = build_candidate_details([candidate], "6000.4.1f1")

        self.assertEqual("UNITY_VERSION_MISMATCH", resolve_probe_status([candidate], details, "6000.4.1f1"))


if __name__ == "__main__":
    unittest.main()
