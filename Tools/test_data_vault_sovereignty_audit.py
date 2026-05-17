import json
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

import DataVaultSovereigntyAudit as audit  # noqa: E402


class DataVaultSovereigntyAuditTests(unittest.TestCase):
    def test_scan_separates_h8memory_allowed_constructors_from_system_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_audit_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            h8memory = source / "Core" / "Memory" / "H8Memory.cs"
            gameplay = source / "Gameplay" / "BadSystem.cs"
            h8memory.parent.mkdir(parents=True)
            gameplay.parent.mkdir(parents=True)
            h8memory.write_text(
                "new NativeArray<int>(4, Allocator.Persistent);\n"
                "new NativeArray<float>(4, Allocator.Persistent);\n",
                encoding="utf-8",
            )
            gameplay.write_text(
                "new NativeArray<int>(4, Allocator.Persistent);\n"
                "new    NativeArray<float>(4, Allocator.Persistent);\n",
                encoding="utf-8",
            )

            findings = audit.scan_source_tree(source, root)
            payload = audit.build_audit_payload(findings, source, root)

            self.assertEqual(payload["totalDirectConstructors"], 4)
            self.assertEqual(payload["allowedDirectConstructors"], 2)
            self.assertEqual(payload["forbiddenDirectConstructors"], 2)
            self.assertEqual(payload["forbiddenFileCount"], 1)

    def test_scan_tracks_nativearray_field_declaration_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_declaration_audit_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            h8memory = source / "Core" / "Memory" / "H8Memory.cs"
            gameplay = source / "Gameplay" / "StatefulSystem.cs"
            h8memory.parent.mkdir(parents=True)
            gameplay.parent.mkdir(parents=True)
            h8memory.write_text(
                "private NativeArray<int> _allocatorScratch;\n",
                encoding="utf-8",
            )
            gameplay.write_text(
                "private NativeArray<int> _localState;\n"
                "[ReadOnly] public NativeArray<float> JobView;\n"
                "public NativeArray<int> View => _localState;\n"
                "NativeArray<int> localOnly = default;\n"
                "// private NativeArray<byte> _commented;\n",
                encoding="utf-8",
            )

            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            payload = audit.build_audit_payload(
                [],
                source,
                root,
                declaration_findings=declaration_findings,
            )

            self.assertEqual(payload["totalNativeArrayDeclarations"], 3)
            self.assertEqual(payload["allowedNativeArrayDeclarations"], 1)
            self.assertEqual(payload["forbiddenNativeArrayDeclarations"], 2)
            self.assertEqual(payload["declarationFileCount"], 1)

    def test_combined_scan_matches_individual_nativearray_scans(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_vault_combined_audit_") as temp_dir:
            root = Path(temp_dir)
            source = root / "Assets" / "_Project" / "Scripts"
            gameplay = source / "Gameplay" / "StatefulSystem.cs"
            gameplay.parent.mkdir(parents=True)
            gameplay.write_text(
                "private NativeArray<int> _localState;\n"
                "public void Allocate() { _localState = new NativeArray<int>(4, Allocator.Persistent); }\n",
                encoding="utf-8",
            )

            constructor_findings = audit.scan_source_tree(source, root)
            declaration_findings = audit.scan_native_array_declaration_tree(source, root)
            combined_constructors, combined_declarations = audit.scan_source_tree_with_declarations(source, root)

            self.assertEqual(combined_constructors, constructor_findings)
            self.assertEqual(combined_declarations, declaration_findings)

    def test_no_regression_gate_fails_when_file_count_increases(self) -> None:
        payload = {
            "findings": [
                {
                    "path": "Assets/_Project/Scripts/Gameplay/BadSystem.cs",
                    "count": 2,
                    "lines": [1, 2],
                    "allowed": False,
                }
            ],
            "forbiddenDirectConstructors": 2,
        }
        baseline = {
            "schema": audit.BASELINE_SCHEMA,
            "forbiddenDirectConstructors": 2,
            "forbiddenByFile": {
                "Assets/_Project/Scripts/Gameplay/BadSystem.cs": 1,
            },
        }

        errors = audit.detect_regressions(payload, baseline)

        self.assertEqual(len(errors), 1)
        self.assertIn("BadSystem.cs", errors[0])

    def test_no_regression_gate_fails_when_declaration_count_increases(self) -> None:
        payload = {
            "findings": [],
            "forbiddenDirectConstructors": 0,
            "declarationFindings": [
                {
                    "path": "Assets/_Project/Scripts/Gameplay/StatefulSystem.cs",
                    "count": 2,
                    "lines": [4, 5],
                    "allowed": False,
                }
            ],
            "forbiddenNativeArrayDeclarations": 2,
        }
        baseline = {
            "schema": audit.BASELINE_SCHEMA,
            "forbiddenDirectConstructors": 0,
            "forbiddenByFile": {},
            "forbiddenNativeArrayDeclarations": 1,
            "forbiddenDeclarationsByFile": {
                "Assets/_Project/Scripts/Gameplay/StatefulSystem.cs": 1,
            },
        }

        errors = audit.detect_regressions(payload, baseline)

        self.assertGreaterEqual(len(errors), 1)
        self.assertTrue(any("StatefulSystem.cs" in error for error in errors))

    def test_baseline_round_trip_preserves_forbidden_counts(self) -> None:
        payload = {
            "sourceRoot": "Assets/_Project/Scripts",
            "pattern": audit.NATIVE_ARRAY_CONSTRUCTOR_RE.pattern,
            "declarationPattern": audit.NATIVE_ARRAY_DECLARATION_RE.pattern,
            "totalDirectConstructors": 3,
            "allowedDirectConstructors": 1,
            "forbiddenDirectConstructors": 2,
            "forbiddenFileCount": 1,
            "totalNativeArrayDeclarations": 2,
            "allowedNativeArrayDeclarations": 1,
            "forbiddenNativeArrayDeclarations": 1,
            "declarationFileCount": 1,
            "allowedPathSuffixes": list(audit.DEFAULT_ALLOWED_PATH_SUFFIXES),
            "declarationAllowedPathSuffixes": list(audit.DEFAULT_ALLOWED_DECLARATION_PATH_SUFFIXES),
            "findings": [
                {
                    "path": "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
                    "count": 1,
                    "lines": [10],
                    "allowed": True,
                },
                {
                    "path": "Assets/_Project/Scripts/World/BadWorld.cs",
                    "count": 2,
                    "lines": [20, 21],
                    "allowed": False,
                },
            ],
            "declarationFindings": [
                {
                    "path": "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs",
                    "count": 1,
                    "lines": [30],
                    "allowed": True,
                },
                {
                    "path": "Assets/_Project/Scripts/World/BadWorld.cs",
                    "count": 1,
                    "lines": [40],
                    "allowed": False,
                },
            ],
        }

        with tempfile.TemporaryDirectory(prefix="h8_vault_baseline_") as temp_dir:
            path = Path(temp_dir) / "baseline.json"
            baseline = audit.build_baseline(payload)
            audit.write_json(path, baseline)

            loaded = json.loads(path.read_text(encoding="utf-8"))

        self.assertEqual(loaded["schema"], audit.BASELINE_SCHEMA)
        self.assertEqual(loaded["forbiddenByFile"]["Assets/_Project/Scripts/World/BadWorld.cs"], 2)
        self.assertEqual(loaded["forbiddenDeclarationsByFile"]["Assets/_Project/Scripts/World/BadWorld.cs"], 1)


if __name__ == "__main__":
    unittest.main()
