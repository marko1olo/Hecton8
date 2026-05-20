#!/usr/bin/env python3
"""Tests for ArchitectureRiskHotlistAudit."""

from __future__ import annotations

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


if __name__ == "__main__":
    unittest.main()
