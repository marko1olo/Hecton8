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


if __name__ == "__main__":
    unittest.main()
