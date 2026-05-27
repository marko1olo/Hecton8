import sys
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import AtlasCheck as atlas_check  # noqa: E402
import BuildArchitectureAtlas as atlas_build  # noqa: E402


PROJECT_ROOT = TOOLS_ROOT.parent


class AtlasCheckTests(unittest.TestCase):
    def test_normalize_reference_keeps_repo_paths_and_strips_line_suffix(self) -> None:
        self.assertEqual(
            atlas_check.normalize_reference("Docs/DEPENDENCY_GRAPH.md:12"),
            "Docs/DEPENDENCY_GRAPH.md",
        )
        self.assertEqual(
            atlas_check.normalize_reference("<Tools/AtlasCheck.py>"),
            "Tools/AtlasCheck.py",
        )

    def test_normalize_reference_rejects_non_repo_or_unstable_patterns(self) -> None:
        self.assertIsNone(atlas_check.normalize_reference("https://example.invalid/file.md"))
        self.assertIsNone(atlas_check.normalize_reference("*.cs"))
        self.assertIsNone(atlas_check.normalize_reference("not/a/repo/path.md"))

    def test_collect_references_finds_markdown_inline_and_plain_paths(self) -> None:
        text = (
            "[Atlas](Docs/DEPENDENCY_GRAPH.md)\n"
            "`Tools/AtlasCheck.py:10`\n"
            "Plain path: .agents-skills/ARCH_Signal_Lane_Segregation.txt\n"
        )

        refs = atlas_check.collect_references(text)

        self.assertIn("Docs/DEPENDENCY_GRAPH.md", refs)
        self.assertIn("Tools/AtlasCheck.py", refs)
        self.assertIn(".agents-skills/ARCH_Signal_Lane_Segregation.txt", refs)

    def test_collect_json_references_walks_nested_payloads(self) -> None:
        refs = {}
        atlas_check.collect_json_references(
            {
                "artifact": "Docs/DEPENDENCY_GRAPH.md",
                "nested": ["Tools/BuildArchitectureAtlas.py:44"],
                "external": "https://example.invalid/nope",
            },
            refs,
        )

        self.assertIn("Docs/DEPENDENCY_GRAPH.md", refs)
        self.assertIn("Tools/BuildArchitectureAtlas.py", refs)
        self.assertNotIn("https://example.invalid/nope", refs)

    def test_collect_source_cache_references_validates_cache_keys(self) -> None:
        refs = {}
        invalid = atlas_check.collect_source_cache_references(
            {
                "schema_version": 1,
                "files": {
                    "Assets/_Project/Scripts/Core/GlobalSignals.cs": {},
                    "not/a/repo/path.cs": {},
                },
            },
            refs,
        )

        self.assertIn("Assets/_Project/Scripts/Core/GlobalSignals.cs", refs)
        self.assertEqual(invalid, ["not/a/repo/path.cs"])


class BuildArchitectureAtlasTests(unittest.TestCase):
    def test_normalize_signal_name_strips_namespace_and_generic_tail(self) -> None:
        self.assertEqual(
            atlas_build.normalize_signal_name("Hecton8.Core.Signals.CombatDamageSignal"),
            "CombatDamageSignal",
        )
        self.assertEqual(
            atlas_build.normalize_signal_name("global::Hecton8.Core.Signals.WeatherChangedSignal"),
            "WeatherChangedSignal",
        )

    def test_line_number_counts_one_based_lines(self) -> None:
        text = "alpha\nbeta\ngamma"
        self.assertEqual(atlas_build.line_number(text, 0), 1)
        self.assertEqual(atlas_build.line_number(text, text.index("beta")), 2)
        self.assertEqual(atlas_build.line_number(text, text.index("gamma")), 3)

    def test_analyze_source_file_extracts_cacheable_signal_data(self) -> None:
        source = (
            "namespace Hecton8.Core.Signals\n"
            "{\n"
            "    public struct CacheProbeSignal : ISignal {}\n"
            "    public static class Probe\n"
            "    {\n"
            "        public static void Run()\n"
            "        {\n"
            "            SignalBus<CacheProbeSignal>.Publish(default);\n"
            "            SignalBus<CacheProbeSignal>.GetFrameSnapshot();\n"
            "        }\n"
            "    }\n"
            "}\n"
        )
        analysis = atlas_build.analyze_source_bytes(
            source.encode("utf-8"),
            "Assets/_Project/Scripts/Core/Signals/Probe.cs",
            True,
        )

        expected_lines = source.count("\n") + (0 if not source or source.endswith("\n") else 1)
        self.assertEqual(analysis["line_count"], expected_lines)
        self.assertEqual(analysis["signals"][0]["name"], "CacheProbeSignal")
        self.assertIn("CacheProbeSignal", analysis["signal_uses"])
        self.assertIn("Publish", analysis["signal_uses"]["CacheProbeSignal"]["methods"])
        self.assertIn("GetFrameSnapshot", analysis["signal_uses"]["CacheProbeSignal"]["methods"])

    def test_sanitized_text_cells_do_not_emit_path_references(self) -> None:
        rendered = atlas_build.sanitize_text_cell(
            "Solution: moved code through Assets/_Project/Scripts/Core/Signals/Foo.cs"
        )
        refs = atlas_check.collect_references(rendered)

        self.assertNotIn("Assets/_Project/Scripts/Core/Signals/Foo.cs", refs)
        self.assertIn("Assets&#47;_Project/Scripts/Core/Signals/Foo.cs", rendered)

    def test_current_atlas_contains_required_machine_sections(self) -> None:
        atlas = PROJECT_ROOT / "Docs" / "DEPENDENCY_GRAPH.md"
        text = atlas.read_text(encoding="utf-8")

        self.assertIn("Status: GENERATED ARTIFACT STUB", text)
        self.assertIn("Tools/BuildArchitectureAtlas.py", text)
        self.assertIn("Docs/Generated/DEPENDENCY_GRAPH.json", text)
        self.assertIn("python Tools/AtlasCheck.py", text)
        self.assertNotIn("DOC" + "_GLOBAL", text)
        self.assertNotIn("R" + "51", text)


if __name__ == "__main__":
    unittest.main()
