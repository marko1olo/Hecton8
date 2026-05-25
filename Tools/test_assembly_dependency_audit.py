#!/usr/bin/env python3
"""Tests for AssemblyDependencyAudit."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

import AssemblyDependencyAudit as audit

TOOLS_ROOT = Path(__file__).resolve().parent


def write_asmdef(path: Path, name: str, refs: list[str] | None = None, include: list[str] | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "name": name,
        "rootNamespace": name,
        "references": refs or [],
        "includePlatforms": include or [],
        "excludePlatforms": [],
        "allowUnsafeCode": True,
        "overrideReferences": False,
        "precompiledReferences": [],
        "autoReferenced": False,
        "defineConstraints": [],
        "versionDefines": [],
        "noEngineReferences": False,
    }
    path.write_text(json.dumps(payload), encoding="utf-8")


class AssemblyDependencyAuditTests(unittest.TestCase):
    def test_binary_schema_audit_default_report_does_not_clobber_canonical_h8bin_report(self) -> None:
        self.assertNotEqual(audit.DEFAULT_BINARY_REPORT_PATH.name, "BINARY_SCHEMA_AUDIT_REPORT.json")
        self.assertEqual(audit.DEFAULT_BINARY_REPORT_PATH.name, "ASSEMBLY_BINARY_SCHEMA_AUDIT_REPORT_SHINOBU_359.json")
        self.assertNotEqual(audit.DEFAULT_BINARY_CACHE_PATH, audit.DEFAULT_BINARY_FILE_CACHE_PATH)

    def test_detects_core_concrete_sibling_refs(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_asm_audit_", dir=TOOLS_ROOT) as tmp:
            root = Path(tmp)
            write_asmdef(root / "Hecton8.Core.asmdef", "Hecton8.Core", ["Hecton8.AI.Cognition", "Hecton8.World.Contracts"])
            write_asmdef(root / "AI" / "Hecton8.AI.Cognition.asmdef", "Hecton8.AI.Cognition")
            write_asmdef(root / "World" / "Hecton8.World.Contracts.asmdef", "Hecton8.World.Contracts")

            payload = audit.build_payload(root)
            args = audit.build_parser().parse_args(
                [
                    "--source-root",
                    str(root),
                    "--fail-on-core-concrete-sibling-refs",
                ]
            )
            failures = audit.hard_failures(payload, args)

        self.assertEqual(payload["core"]["concreteSiblingReferenceCount"], 1)
        self.assertEqual(payload["runtimeConcreteSiblingReferenceCount"], 1)
        self.assertEqual(len(failures), 1)

    def test_contract_and_editor_refs_are_not_concrete_runtime_debt(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_asm_audit_contract_", dir=TOOLS_ROOT) as tmp:
            root = Path(tmp)
            write_asmdef(root / "Core.asmdef", "Hecton8.Core", ["Hecton8.World.Contracts"])
            write_asmdef(root / "World.Contracts.asmdef", "Hecton8.World.Contracts")
            write_asmdef(
                root / "Editor" / "Hecton8.World.Editor.asmdef",
                "Hecton8.World.Editor",
                ["Hecton8.Core", "Hecton8.World.Runtime"],
                ["Editor"],
            )
            write_asmdef(root / "World" / "Hecton8.World.Runtime.asmdef", "Hecton8.World.Runtime")

            payload = audit.build_payload(root)

        self.assertEqual(payload["core"]["concreteSiblingReferenceCount"], 0)
        self.assertEqual(payload["runtimeConcreteSiblingReferenceCount"], 0)

    def test_detects_first_party_cycles(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_asm_audit_cycle_", dir=TOOLS_ROOT) as tmp:
            root = Path(tmp)
            write_asmdef(root / "A.asmdef", "Hecton8.A", ["Hecton8.B"])
            write_asmdef(root / "B.asmdef", "Hecton8.B", ["Hecton8.A"])

            payload = audit.build_payload(root)

        self.assertEqual(payload["cycleCount"], 1)

    def test_detects_core_contract_boundary_violations(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_asm_audit_contract_gate_", dir=TOOLS_ROOT) as tmp:
            root = Path(tmp)
            write_asmdef(root / "Hecton8.Core.Contracts.asmdef", "Hecton8.Core.Contracts")
            write_asmdef(root / "AI" / "Hecton8.AI.Cognition.asmdef", "Hecton8.AI.Cognition")
            write_asmdef(
                root / "World" / "Hecton8.World.Runtime.asmdef",
                "Hecton8.World.Runtime",
                ["Hecton8.Core.Contracts", "Hecton8.AI.Cognition"],
            )

            payload = audit.build_payload(root)

        boundary = payload["coreContractsBoundary"]
        self.assertEqual(boundary["violationCount"], 1)
        self.assertEqual(boundary["violations"][0]["reference"], "Hecton8.AI.Cognition")

    def test_mock_csharp_layout_parser_detects_alignment_property_and_aup(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_asm_audit_mock_cs_", dir=TOOLS_ROOT) as tmp:
            root = Path(tmp)
            audit.generate_mock_csharp_structs(root)
            payload = audit.build_binary_schema_payload(
                root,
                [root],
                root / "report.json",
                root / "metric.json",
                root / "binary_schema_profiles.csv",
            )

        summary = payload["summary"]
        self.assertGreaterEqual(summary["arm64ViolationCount"], 1)
        self.assertGreaterEqual(summary["cs1612PropertyViolationCount"], 1)
        self.assertGreaterEqual(summary["schemaMismatchCount"], 1)
        self.assertGreaterEqual(summary["aupPrecisionViolationCount"], 1)
        self.assertEqual(summary["schemaProfileCount"], 2)
        self.assertEqual(summary["schemaProfileParseErrorCount"], 0)

    def test_binary_schema_audit_reuses_aggregate_cache_when_inputs_match(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_asm_audit_binary_cache_", dir=TOOLS_ROOT) as tmp:
            root = Path(tmp)
            audit.generate_mock_csharp_structs(root)
            cache_path = root / "audit_cache.json"
            first = audit.build_binary_schema_payload(
                root,
                [root],
                root / "report.json",
                root / "metric.json",
                root / "binary_schema_profiles.csv",
                cache_path=cache_path,
            )
            second = audit.build_binary_schema_payload(
                root,
                [root],
                root / "report.json",
                root / "metric.json",
                root / "binary_schema_profiles.csv",
                cache_path=cache_path,
            )

        first_summary = first["summary"]
        second_summary = second["summary"]
        self.assertFalse(first_summary["cacheHit"])
        self.assertTrue(second_summary["cacheHit"])
        self.assertEqual(second_summary["cacheMisses"], 0)
        self.assertEqual(first_summary["arm64ViolationCount"], second_summary["arm64ViolationCount"])
        self.assertEqual(first_summary["schemaMismatchCount"], second_summary["schemaMismatchCount"])

    def test_binary_schema_file_cache_reuses_csharp_parse_after_schema_profile_change(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_asm_audit_binary_file_cache_", dir=TOOLS_ROOT) as tmp:
            root = Path(tmp)
            audit.generate_mock_csharp_structs(root)
            cache_path = root / "aggregate_cache.json"
            file_cache_path = root / "file_cache.json"
            first = audit.build_binary_schema_payload(
                root,
                [root],
                root / "report.json",
                root / "metric.json",
                root / "binary_schema_profiles.csv",
                cache_path=cache_path,
                file_cache_path=file_cache_path,
            )
            (root / "binary_schema_profiles.csv").write_text(
                "profile,maxStructBytes,alignmentBytes,arm64Strict\n"
                "Meta_Quest_3,64,8,true\n"
                "PC_Ultra,128,8,false\n"
                "Steam_Deck,96,8,true\n",
                encoding="utf-8",
            )
            second = audit.build_binary_schema_payload(
                root,
                [root],
                root / "report.json",
                root / "metric.json",
                root / "binary_schema_profiles.csv",
                cache_path=cache_path,
                file_cache_path=file_cache_path,
            )

        first_summary = first["summary"]
        second_summary = second["summary"]
        self.assertFalse(first_summary["cacheHit"])
        self.assertTrue(second_summary["cacheHit"])
        self.assertEqual(second_summary["cacheMisses"], 0)
        self.assertEqual(second_summary["schemaProfileCount"], 3)
        self.assertEqual(first_summary["structsParsed"], second_summary["structsParsed"])

    def test_schema_profile_csv_parser_uses_fnv_rows(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_asm_audit_csv_", dir=TOOLS_ROOT) as tmp:
            root = Path(tmp)
            csv_path = root / "binary_schema_profiles.csv"
            csv_path.write_text(
                "profile,maxStructBytes,alignmentBytes,arm64Strict\nMeta_Quest_3,64,8,true\n",
                encoding="utf-8",
            )
            payload = audit.build_schema_profile_payload(csv_path)

        self.assertTrue(payload["exists"])
        self.assertEqual(payload["profileCount"], 1)
        self.assertEqual(payload["parseErrorCount"], 0)
        self.assertEqual(payload["profiles"][0]["fnv1a32"], "0xCF3215AF")

    def test_oop_test_audit_detects_world_physics_and_gameobject(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_asm_audit_oop_", dir=TOOLS_ROOT) as tmp:
            root = Path(tmp)
            audit.generate_mock_csharp_structs(root)
            payload = audit.build_oop_test_audit_payload(root, root / "qa.json")

        self.assertEqual(payload["summary"], "OOP Tests Present")
        self.assertGreaterEqual(payload["physicsApiHits"], 1)
        self.assertGreaterEqual(payload["gameObjectInstantiationHits"], 1)

    def test_using_boundary_detects_cross_domain_import(self) -> None:
        with tempfile.TemporaryDirectory(prefix="h8_asm_audit_using_", dir=TOOLS_ROOT) as tmp:
            root = Path(tmp)
            write_asmdef(root / "Core" / "Hecton8.Core.Contracts.asmdef", "Hecton8.Core.Contracts")
            write_asmdef(root / "AI" / "Hecton8.AI.Cognition.asmdef", "Hecton8.AI.Cognition")
            write_asmdef(root / "World" / "Hecton8.World.Runtime.asmdef", "Hecton8.World.Runtime")
            (root / "World" / "Leak.cs").write_text(
                "\n".join(
                    [
                        "using Hecton8.AI.Cognition;",
                        "using Hecton8.Core.Contracts;",
                        "namespace Hecton8.World.Runtime { public static class Leak {} }",
                    ]
                ),
                encoding="utf-8",
            )

            payload = audit.build_using_boundary_payload(root, root / "using.json")

        summary = payload["summary"]
        self.assertEqual(summary["violationCount"], 1)
        self.assertEqual(payload["violations"][0]["using"], "Hecton8.AI.Cognition")


if __name__ == "__main__":
    unittest.main()
