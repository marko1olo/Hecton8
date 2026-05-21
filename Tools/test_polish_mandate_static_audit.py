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

    def test_ignores_forbidden_tokens_inside_string_literals(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "AuditSmoke.cs"
            src.write_text(
                """
public static class AuditSmoke
{
    private static readonly string[] Forbidden =
    {
        "UnityEngine.Random",
        "Random.Range",
        "Time.deltaTime",
        "IsLowEnd",
        "[BurstCompile]",
    };

    public static void AssertNoRuntimeRandom(string source)
    {
        AssertNotContains(source, "UnityEngine.Random");
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["unityRandom"]["matches"], 0)
            self.assertEqual(cats["unityTimeCritical"]["matches"], 0)
            self.assertEqual(cats["binaryHardwareSwitch"]["matches"], 0)
            self.assertEqual(cats["burstCompile"]["matches"], 0)

    def test_binary_hardware_switch_ignores_plain_dto_fields(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "TierDto.cs"
            src.write_text(
                """
public struct TierDto
{
    public byte QualityTier;
}

public static class TierWriter
{
    public static void Copy(TierDto state, TierDto entry, BinaryWriter writer)
    {
        entry.QualityTier = state.QualityTier;
        writer.Write(entry.QualityTier);
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["binaryHardwareSwitch"]["matches"], 0)

    def test_binary_hardware_switch_ignores_pure_tier_accessors(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "TierAccessor.cs"
            src.write_text(
                """
public static class TierAccessor
{
    public static HectonQualityTier QualityTier => _hasProfile ? _profile.QualityTier : HectonQualityTier.Unknown;

    public static HectonQualityTier ReadTier(Profile profile)
    {
        return profile.QualityTier;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["binaryHardwareSwitch"]["matches"], 0)

    def test_private_native_collection_classification_preserves_raw_total(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "NativeCollections.cs"
            src.write_text(
                """
using Unity.Collections;

public sealed class NativeCollections
{
    private NativeArray<int> _state;
    private NativeArray<float> _voicePool; // Vault alias; GlobalDataVault owns backing memory.
    private static NativeQueue<EventPayload> _pendingEvents;
    private NativeArray<TelemetryEntry> _telemetryRing;
    private NativeList<int> _scratch;
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["privateNativeCollectionField"]["matches"], 5)
            classified_total = (
                cats["privateNativeCollectionVaultAlias"]["matches"]
                + cats["privateNativeCollectionStaticQueueLane"]["matches"]
                + cats["privateNativeCollectionBlackBoxTelemetry"]["matches"]
                + cats["privateNativeCollectionOwnerLocalScratch"]["matches"]
                + cats["privateNativeCollectionUnclassified"]["matches"]
            )
            self.assertEqual(classified_total, cats["privateNativeCollectionField"]["matches"])
            self.assertEqual(cats["privateNativeCollectionVaultAlias"]["matches"], 1)
            self.assertEqual(cats["privateNativeCollectionStaticQueueLane"]["matches"], 1)
            self.assertEqual(cats["privateNativeCollectionBlackBoxTelemetry"]["matches"], 1)
            self.assertEqual(cats["privateNativeCollectionOwnerLocalScratch"]["matches"], 1)
            self.assertEqual(cats["privateNativeCollectionUnclassified"]["matches"], 1)
            self.assertEqual(cats["privateNativeDeclarationField"]["matches"], 5)
            self.assertEqual(cats["privateNativeDeclarationMethodReturn"]["matches"], 0)
            self.assertEqual(cats["privateNativeBuildPlayerRuntime"]["matches"], 5)
            self.assertEqual(cats["privateNativeRiskStaticSignalOrEventBridge"]["matches"], 1)
            self.assertEqual(cats["privateNativeRiskVaultAliasOrVaultResolver"]["matches"], 1)
            self.assertEqual(cats["privateNativeRiskOwnerLocalRuntimeNativeState"]["matches"], 3)

    def test_private_native_collection_classifies_method_returns(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "VaultResolver.cs"
            src.write_text(
                """
using Unity.Collections;

public sealed class VaultResolver
{
    private static NativeArray<int> ResolveVaultBuffer<T>(int capacity)
    {
        return default;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["privateNativeCollectionField"]["matches"], 1)
            self.assertEqual(cats["privateNativeDeclarationMethodReturn"]["matches"], 1)
            self.assertEqual(cats["privateNativeDeclarationField"]["matches"], 0)
            self.assertEqual(cats["privateNativeRiskMethodReturningNativeCollection"]["matches"], 1)

    def test_private_native_collection_classifies_job_struct_view(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "JobView.cs"
            src.write_text(
                """
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct JobView : IJob
{
    private NativeArray<int> _state;

    public void Execute() {}
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["privateNativeCollectionField"]["matches"], 1)
            self.assertEqual(cats["privateNativeRiskJobStructNativeView"]["matches"], 1)

    def test_private_native_collection_classifies_editor_surface(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            editor = root / "Editor"
            editor.mkdir()
            src = editor / "NativeWindow.cs"
            src.write_text(
                """
using Unity.Collections;

public sealed class NativeWindow
{
    private NativeArray<int> _previewRows;
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["privateNativeCollectionField"]["matches"], 1)
            self.assertEqual(cats["privateNativeBuildEditorOnly"]["matches"], 1)
            self.assertEqual(cats["privateNativeRiskEditorOrProofNativeState"]["matches"], 1)

    def test_detects_public_mutable_native_api_exposure(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "NativeApi.cs"
            src.write_text(
                """
using Unity.Collections;

public sealed class NativeApi
{
    public NativeArray<int> MutableRows => _rows;

    public bool TryGetRows(
        out NativeArray<float> rows,
        out int count)
    {
        rows = default;
        count = 0;
        return false;
    }

    public bool TryReadRows(out NativeArray<int>.ReadOnly rows)
    {
        rows = default;
        return false;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["nativeCollectionPublicMutableApiExposure"]["matches"], 2)
            self.assertEqual(cats["nativeApiExposureMutableReturn"]["matches"], 1)
            self.assertEqual(cats["nativeApiExposureOutRefMutable"]["matches"], 1)
            self.assertEqual(cats["nativeApiExposureAmbiguousMutable"]["matches"], 0)
            self.assertEqual(cats["nativeApiExposureBuildPlayerRuntime"]["matches"], 2)
            self.assertEqual(cats["nativeApiRiskRuntimeReturnMutableView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeOutRefMutableView"]["matches"], 1)


if __name__ == "__main__":
    unittest.main()
