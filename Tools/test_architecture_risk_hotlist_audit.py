#!/usr/bin/env python3
"""Tests for ArchitectureRiskHotlistAudit."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

import ArchitectureRiskHotlistAudit as audit


class ArchitectureRiskHotlistAuditTests(unittest.TestCase):
    def test_scores_overlapping_architecture_pressure(self) -> None:
        row = audit.scan_source(
            "Risky.cs",
            """
using Unity.Collections;
using UnityEngine;

public class Risky
{
    private NativeArray<int> _state;
    void Update() {}
    void Tick()
    {
        GlobalRegistry.Audio.Play();
        GlobalSignals.Publish(default);
        HectonEventBus.Publish(default);
        var id = (BufferID)71337;
        var scratch = new NativeArray<int>(4, Allocator.Persistent);
        _handle.Complete();
        var r = Random.Range(0, 2);
        var dt = Time.deltaTime;
        if (IsLowEnd) {}
    }
}
""",
        )
        payload = audit.aggregate_payload(Path("."), 1, [row])
        top = payload["topFiles"][0]
        domain = payload["domainTotals"][0]

        self.assertTrue(str(top["path"]).endswith("Risky.cs"))
        self.assertEqual(top["domain"], "Root")
        self.assertEqual(domain["domain"], "Root")
        self.assertGreater(top["score"], 80)
        self.assertEqual(payload["categoryTotals"]["localNumericBufferCast"], 1)
        self.assertEqual(payload["categoryTotals"]["hectonEventBusPubSub"], 1)

    def test_ignores_line_comments(self) -> None:
        row = audit.scan_source(
            "Comments.cs",
            """
// GlobalSignals.Publish(default);
// var id = (BufferID)70000;
public class Comments {}
""",
        )
        payload = audit.aggregate_payload(Path("."), 1, [row])

        self.assertEqual(payload["scoredFileCount"], 0)

    def test_extracts_project_script_domain(self) -> None:
        self.assertEqual(
            audit.extract_domain("Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs"),
            "World",
        )
        self.assertEqual(audit.extract_domain("Assets/_Project/Scripts/PlayerInventory.cs"), "Root")

    def test_write_markdown_success(self) -> None:
        payload = {
            "schema": "v1",
            "sourceRoot": "Assets",
            "csFileCount": 100,
            "scoredFileCount": 10,
            "categoryTotals": {"hectonEventBusPubSub": 5},
            "familyTotals": {"Coupling": 5},
            "domainTotals": [
                {
                    "domain": "Root",
                    "score": 50,
                    "files": 2,
                    "familyCounts": {"Coupling": 5},
                    "topFiles": [{"path": "Test.cs", "score": 25}],
                }
            ],
            "topFiles": [
                {
                    "path": "Test.cs",
                    "score": 25,
                    "categoryCounts": {"hectonEventBusPubSub": 5},
                    "familyCounts": {"Coupling": 5},
                    "examples": [{"line": 10, "category": "hectonEventBusPubSub", "text": "Publish()"}],
                }
            ],
        }
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "report.md"
            audit.write_markdown(p, payload)
            self.assertTrue(p.exists())
            content = p.read_text(encoding="utf-8")
            self.assertIn("# Architecture Risk Hotlist", content)
            self.assertIn("Test.cs", content)
            self.assertIn("hectonEventBusPubSub", content)
            self.assertIn("Publish()", content)

    def test_write_markdown_malformed_payload(self) -> None:
        payload = {
            "categoryTotals": "not a dict",
            "familyTotals": {},
            "domainTotals": [],
            "topFiles": [],
        }
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "report.md"
            with self.assertRaises(TypeError):
                audit.write_markdown(p, payload)

if __name__ == "__main__":
    unittest.main()
