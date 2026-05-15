import sys
import tempfile
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
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "Probe.cs"
            path.write_text(source, encoding="utf-8")

            analysis = atlas_build.analyze_source_file(
                path,
                "Assets/_Project/Scripts/Core/Signals/Probe.cs",
                True,
            )

        self.assertEqual(analysis["line_count"], source.count("\n") + 1)
        self.assertEqual(analysis["signals"][0]["name"], "CacheProbeSignal")
        self.assertIn("CacheProbeSignal", analysis["signal_uses"])
        self.assertIn("Publish", analysis["signal_uses"]["CacheProbeSignal"]["methods"])
        self.assertIn("GetFrameSnapshot", analysis["signal_uses"]["CacheProbeSignal"]["methods"])

    def test_current_atlas_contains_required_machine_sections(self) -> None:
        atlas = PROJECT_ROOT / "Docs" / "DEPENDENCY_GRAPH.md"
        text = atlas.read_text(encoding="utf-8")

        self.assertIn("Status: ATLAS VERIFIED PENDING RUNTIME VERIFICATION", text)
        self.assertIn("## SignalBus<T> Flow Map", text)
        self.assertIn("## Queue-Backed Signal Lanes", text)
        self.assertIn("## SHERST Wall Of Shame", text)
        self.assertIn("## Phi-Resonance Connectivity Model", text)
        self.assertIn("Tools/BuildArchitectureAtlas.py", text)
        self.assertIn("Docs/DEPENDENCY_GRAPH.json", text)


if __name__ == "__main__":
    unittest.main()
