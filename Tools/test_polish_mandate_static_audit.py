#!/usr/bin/env python3
"""Tests for PolishMandateStaticAudit."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

import PolishMandateStaticAudit as audit


class PolishMandateStaticAuditTests(unittest.TestCase):
    def test_detects_broad_polish_risks(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "Risk.cs"
            src.write_text(
                """
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RiskDto
{
    public int Value { get; set; }
}

[BurstCompile]
public struct RiskJob
{
    private NativeArray<int> _state;
    public void Run()
    {
        Random.Range(0, 4);
        float dt = Time.deltaTime;
        if (IsLowEnd) { }
        _handle.Complete();
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["packOne"]["matches"], 1)
            self.assertEqual(cats["structAutoProperties"]["matches"], 1)
            self.assertEqual(cats["burstMissingCompileSynchronously"]["matches"], 1)
            self.assertEqual(cats["privateNativeCollectionField"]["matches"], 1)
            self.assertEqual(cats["unityRandom"]["matches"], 1)
            self.assertEqual(cats["unityTimeCritical"]["matches"], 1)
            self.assertEqual(cats["binaryHardwareSwitch"]["matches"], 1)
            self.assertEqual(cats["jobHandleComplete"]["matches"], 1)

    def test_accepts_explicit_burst_flags(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "CleanBurst.cs"
            src.write_text(
                """
using Unity.Burst;

[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
public struct CleanJob {}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["burstCompile"]["matches"], 1)
            self.assertEqual(cats["burstMissingCompileSynchronously"]["matches"], 0)
            self.assertEqual(cats["burstMissingFloatMode"]["matches"], 0)
            self.assertEqual(cats["burstMissingFloatPrecision"]["matches"], 0)


if __name__ == "__main__":
    unittest.main()
