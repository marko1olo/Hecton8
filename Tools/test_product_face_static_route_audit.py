#!/usr/bin/env python3
"""Tests for ProductFaceStaticRouteAudit."""

import os
import sys
import unittest
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import ProductFaceStaticRouteAudit as audit


class TestUtilityFunctions(unittest.TestCase):
    def test_normalize_rel(self):
        self.assertEqual(audit.normalize_rel("foo\\bar"), "foo/bar")
        self.assertEqual(audit.normalize_rel("/foo/bar/"), "foo/bar")
        self.assertEqual(audit.normalize_rel("\\foo\\bar\\"), "foo/bar")

    def test_normalize_text(self):
        self.assertEqual(audit.normalize_text("foo\\bar"), "foo/bar")
        self.assertEqual(audit.normalize_text("foo/bar"), "foo/bar")

    def test_line_number(self):
        text = "line 1\nline 2\nline 3"
        self.assertEqual(audit.line_number(text, 0), 1)
        self.assertEqual(audit.line_number(text, 6), 1)  # Just before \n
        self.assertEqual(audit.line_number(text, 7), 2)  # At \n
        self.assertEqual(audit.line_number(text, 14), 3)

    def test_route_present(self):
        text = "some text\nAssets/_Project/Art/Generated/ProductFace/Tools/some_file.asset\nmore text"
        self.assertTrue(audit.route_present(text, "Assets/_Project/Art/Generated/ProductFace/Tools/"))
        self.assertTrue(audit.route_present(text, "Assets/_Project/Art/Generated/ProductFace/Tools"))

        text2 = "Assets/_Project/Art/Generated/ProductFace/Tools"
        self.assertTrue(audit.route_present(text2, "Assets/_Project/Art/Generated/ProductFace/Tools"))

        text3 = "No match here"
        self.assertFalse(audit.route_present(text3, "Assets/_Project/Art/Generated/ProductFace/Tools/"))

    def test_find_pattern_lines(self):
        text = "foo\nbar\nbaz foo\nqux"
        lines = list(audit.find_pattern_lines(text, "foo"))
        self.assertEqual(lines, [1, 3])

        lines2 = list(audit.find_pattern_lines(text, "notfound"))
        self.assertEqual(lines2, [])


if __name__ == "__main__":
    unittest.main()

class TestTextCache(unittest.TestCase):
    def setUp(self):
        import tempfile
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.cache = audit.TextCache(self.root)

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_exists(self):
        (self.root / "existing.txt").write_text("hello")
        self.assertTrue(self.cache.exists("existing.txt"))
        self.assertFalse(self.cache.exists("missing.txt"))

    def test_read_success_and_cache(self):
        file_path = self.root / "file.txt"
        file_path.write_text("content")

        # Read first time
        self.assertEqual(self.cache.read("file.txt"), "content")

        # Modify file directly
        file_path.write_text("new content")

        # Read again should return cached version
        self.assertEqual(self.cache.read("file.txt"), "content")

    def test_read_missing_file(self):
        self.assertIsNone(self.cache.read("missing.txt"))
        # Should be cached as None
        self.assertIsNone(self.cache._cache["missing.txt"])

    def test_read_file_too_large(self):
        file_path = self.root / "large.txt"
        # Instead of actually creating a large file, we can mock the size
        # Or create a dummy large file if it's not too big, but let's just
        # monkeypatch MAX_TEXT_BYTES for the test
        original_max = audit.MAX_TEXT_BYTES
        audit.MAX_TEXT_BYTES = 10
        try:
            file_path.write_text("this is a file larger than 10 bytes")
            self.assertIsNone(self.cache.read("large.txt"))
        finally:
            audit.MAX_TEXT_BYTES = original_max

    def test_read_os_error(self):
        # We can simulate an OSError by creating a directory with the same name
        file_path = self.root / "dir.txt"
        file_path.mkdir()
        self.assertIsNone(self.cache.read("dir.txt"))


class TestValidationMethods(unittest.TestCase):
    def setUp(self):
        import tempfile
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)
        self.cache = audit.TextCache(self.root)
        self.findings = []

    def tearDown(self):
        self.temp_dir.cleanup()

    def create_file(self, rel_path, content=""):
        path = self.root / rel_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)

    def test_add_missing_file_findings(self):
        # Without any files, all SOURCE_FILES should be missing
        audit.add_missing_file_findings(self.cache, self.findings)
        self.assertEqual(len(self.findings), len(audit.SOURCE_FILES))
        for finding in self.findings:
            self.assertEqual(finding.severity, "ERROR")
            self.assertEqual(finding.code, "MISSING_SOURCE")

        self.findings.clear()

        # Create one file
        self.create_file(audit.SOURCE_FILES[0])
        audit.add_missing_file_findings(self.cache, self.findings)
        self.assertEqual(len(self.findings), len(audit.SOURCE_FILES) - 1)
        self.assertNotIn(audit.SOURCE_FILES[0], [f.path for f in self.findings])

    def test_add_route_findings_missing_current(self):
        # Create a file but without current routes
        self.create_file(audit.SOURCE_FILES[0], "some other text")
        audit.add_route_findings(self.cache, self.findings)

        missing_roots = [f.path for f in self.findings if f.code == "MISSING_CURRENT_ROUTE_ROOT"]
        self.assertEqual(len(missing_roots), len(audit.CURRENT_ROUTE_ROOTS))

    def test_add_route_findings_stale_route(self):
        self.create_file(audit.SOURCE_FILES[0], f"text with {audit.STALE_ROUTE_PATTERNS[0]}")
        audit.add_route_findings(self.cache, self.findings)

        stale_findings = [f for f in self.findings if f.code == "STALE_ROUTE_ROOT"]
        self.assertTrue(len(stale_findings) > 0)
        self.assertEqual(stale_findings[0].path, audit.SOURCE_FILES[0])

    def test_add_forbidden_source_findings(self):
        self.create_file(audit.SOURCE_FILES[0], f"using {audit.FORBIDDEN_SOURCE_TOKENS[0]} here")
        audit.add_forbidden_source_findings(self.cache, self.findings)

        self.assertEqual(len(self.findings), 2)
        self.assertEqual(self.findings[0].code, "FORBIDDEN_SOURCE_TOKEN")
        self.assertEqual(self.findings[0].path, audit.SOURCE_FILES[0])

    def test_add_report_proof_findings_missing(self):
        audit.add_report_proof_findings(self.cache, self.findings)

        missing_reports = [f for f in self.findings if f.code == "MISSING_REPORT"]
        self.assertTrue(len(missing_reports) > 0)

        missing_optional = [f for f in self.findings if f.code == "OPTIONAL_REPORT_MISSING"]
        self.assertEqual(len(missing_optional), 1)

    def test_add_report_proof_findings_missing_boundary(self):
        report_path = audit.REPORT_FILES[0]
        self.create_file(report_path, "no boundary here")
        audit.add_report_proof_findings(self.cache, self.findings)

        boundary_findings = [f for f in self.findings if f.code == "MISSING_PENDING_PROOF_BOUNDARY"]
        self.assertTrue(len(boundary_findings) > 0)

    def test_add_report_proof_findings_unsupported_claim(self):
        report_path = audit.REPORT_FILES[0]
        self.create_file(report_path, f"{audit.PENDING_MARKERS[0]}\nPLAYMODE VERIFIED\nIn-game result: SUCCESS")
        audit.add_report_proof_findings(self.cache, self.findings)

        claims = [f for f in self.findings if f.code == "UNSUPPORTED_RUNTIME_ACCEPTANCE_CLAIM"]
        self.assertEqual(len(claims), 2)  # One for pattern, one for in-game result

    def test_add_ai_texture_findings_missing(self):
        audit.add_ai_texture_findings(self.cache, self.findings)
        self.assertEqual(len(self.findings), 1)
        self.assertEqual(self.findings[0].code, "MISSING_AI_TEXTURE_HARDENING_REPORT")

    def test_add_ai_texture_findings_missing_binding(self):
        path = "Docs/Reports/Batch18/1891_AITEXTURE_PRODUCT_FACE_HARDENING_PACKET.md"
        self.create_file(path, "no binding here")
        audit.add_ai_texture_findings(self.cache, self.findings)
        self.assertEqual(self.findings[0].code, "MISSING_AI_TEXTURE_BINDING_WARNING")

    def test_add_ai_texture_findings_weak_binding(self):
        path = "Docs/Reports/Batch18/1891_AITEXTURE_PRODUCT_FACE_HARDENING_PACKET.md"
        self.create_file(path, "ai_texture_prefab_bindings.csv is ok")
        audit.add_ai_texture_findings(self.cache, self.findings)
        self.assertEqual(self.findings[0].code, "WEAK_AI_TEXTURE_BINDING_WARNING")

    def test_add_ai_texture_findings_generic_binding_in_source(self):
        path = "Docs/Reports/Batch18/1891_AITEXTURE_PRODUCT_FACE_HARDENING_PACKET.md"
        self.create_file(path, "reject ai_texture_prefab_bindings.csv")
        self.create_file(audit.SOURCE_FILES[0], "using ai_texture_prefab_bindings.csv")
        audit.add_ai_texture_findings(self.cache, self.findings)
        self.assertEqual(self.findings[0].code, "GENERIC_AI_TEXTURE_BINDING_IN_SOURCE")

    def test_add_environment_findings_missing(self):
        audit.add_environment_findings(self.cache, self.findings)
        self.assertEqual(len(self.findings), 1)
        self.assertEqual(self.findings[0].code, "MISSING_ENVIRONMENT_EXCLUSION_REPORT")

    def test_add_environment_findings_missing_term(self):
        path = "Docs/Reports/Batch18/1889_PRODUCT_FACE_ENVIRONMENT_SOURCE_EXCLUSION_MANIFEST.md"
        self.create_file(path, "Crest terrain")
        audit.add_environment_findings(self.cache, self.findings)
        missing_terms = [f for f in self.findings if f.code == "MISSING_ENVIRONMENT_EXCLUSION_TERM"]
        self.assertTrue(len(missing_terms) > 0)


class TestCLILogic(unittest.TestCase):
    def test_severity_counts(self):
        findings = [
            audit.Finding("ERROR", "CODE1", "path1", "msg"),
            audit.Finding("ERROR", "CODE2", "path2", "msg"),
            audit.Finding("WARNING", "CODE3", "path3", "msg"),
        ]
        counts = audit.severity_counts(findings)
        self.assertEqual(counts["ERROR"], 2)
        self.assertEqual(counts["WARNING"], 1)
        self.assertEqual(counts["INFO"], 0)

    def test_run_audit(self):
        import tempfile
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            findings = audit.run_audit(root)
            # Just test it runs and returns findings (like missing files)
            self.assertTrue(len(findings) > 0)

    def test_main_cli(self):
        import tempfile
        import io
        from unittest.mock import patch

        with tempfile.TemporaryDirectory() as temp_dir:
            # We expect ERRORs because the temp dir is empty

            # Test default run
            with patch('sys.stdout', new_callable=io.StringIO) as mock_stdout:
                exit_code = audit.main(["--root", temp_dir])
                self.assertEqual(exit_code, 0) # doesn't fail unless --fail-on-error
                output = mock_stdout.getvalue()
                self.assertIn("ERROR:", output)

            # Test fail on error
            with patch('sys.stdout', new_callable=io.StringIO):
                exit_code = audit.main(["--root", temp_dir, "--fail-on-error"])
                self.assertEqual(exit_code, 1)

            # Test JSON output
            with patch('sys.stdout', new_callable=io.StringIO) as mock_stdout:
                exit_code = audit.main(["--root", temp_dir, "--json"])
                import json
                output = mock_stdout.getvalue()
                parsed = json.loads(output)
                self.assertIn("findings", parsed)
                self.assertIn("counts", parsed)
