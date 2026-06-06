#!/usr/bin/env python3
"""Tests for PolishMandateStaticAudit."""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_ROOT = Path(__file__).resolve().parent
if str(TOOLS_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOLS_ROOT))

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
            self.assertEqual(cats["unityTimeDelta"]["matches"], 1)
            self.assertEqual(cats["unityTimeBuildPlayerRuntime"]["matches"], 1)
            self.assertEqual(cats["unityTimeRiskGameplayDelta"]["matches"], 1)
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

    def test_unity_update_method_ignores_editor_surfaces(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            runtime = root / "RuntimeThing.cs"
            runtime.write_text(
                """
public sealed class RuntimeThing
{
    private void Update() {}
}
""",
                encoding="utf-8",
            )
            editor_suffix = root / "WaveTuner.Editor.cs"
            editor_suffix.write_text(
                """
public sealed class WaveTuner
{
    private void Update() {}
}
""",
                encoding="utf-8",
            )
            editor_dir = root / "Editor"
            editor_dir.mkdir()
            editor_file = editor_dir / "BakeWindow.cs"
            editor_file.write_text(
                """
public sealed class BakeWindow
{
    private void Update() {}
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["unityUpdateMethod"]["matches"], 1)
            self.assertTrue(cats["unityUpdateMethod"]["examples"][0]["path"].endswith("RuntimeThing.cs"))

    def test_linq_surface_ignores_editor_surfaces(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            runtime = root / "RuntimeQuery.cs"
            runtime.write_text(
                """
public sealed class RuntimeQuery
{
    public bool HasRows(Row[] rows)
    {
        return rows.Any(row => row.Enabled);
    }
}
""",
                encoding="utf-8",
            )
            editor_dir = root / "Editor"
            editor_dir.mkdir()
            editor_file = editor_dir / "Verifier.cs"
            editor_file.write_text(
                """
using System.Linq;

public sealed class Verifier
{
    public object FindSyntax(Node node)
    {
        return node.Ancestors().OfType<object>().FirstOrDefault();
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["linqSurface"]["matches"], 1)
            self.assertEqual(cats["linqSurface"]["files"], 1)
            self.assertTrue(cats["linqSurface"]["examples"][0]["path"].endswith("RuntimeQuery.cs"))

    def test_empty_runtime_tick_or_update_method_ignores_non_runtime_surfaces(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            runtime = root / "RuntimeThing.cs"
            runtime.write_text(
                """
public sealed class RuntimeThing
{
    public void Tick()
    {
    }

    private void LateFrameTick() { }

    private void Helper()
    {
    }
}
""",
                encoding="utf-8",
            )
            editor_dir = root / "Editor"
            editor_dir.mkdir()
            editor_file = editor_dir / "BakeWindow.cs"
            editor_file.write_text(
                """
public sealed class BakeWindow
{
    private void Update() {}
}
""",
                encoding="utf-8",
            )
            qa_file = root / "SmokeTester.cs"
            qa_file.write_text(
                """
public sealed class SmokeTester
{
    public void Tick()
    {
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["emptyRuntimeTickOrUpdateMethod"]["matches"], 2)
            self.assertEqual(cats["emptyRuntimeTickOrUpdateMethod"]["files"], 1)
            self.assertTrue(cats["emptyRuntimeTickOrUpdateMethod"]["examples"][0]["path"].endswith("RuntimeThing.cs"))

    def test_empty_runtime_tick_or_update_method_ignores_dispatcher_system_stubs(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "DispatcherPhaseOwner.cs"
            src.write_text(
                """
public sealed class DispatcherPhaseOwner
{
    private sealed class PhaseSystem : IDispatcherSystem
    {
        public void PreSimulationTick(in DispatcherTimingDTO timing)
        {
        }

        public void VisualSyncTick(in DispatcherTimingDTO timing)
        {
        }
    }

    public virtual void SlowTick()
    {
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["emptyRuntimeTickOrUpdateMethod"]["matches"], 0)

    def test_empty_runtime_tick_or_update_method_ignores_explicit_legacy_stubs(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "LegacyEntrypoints.cs"
            src.write_text(
                """
public sealed class LegacyEntrypoints
{
    /// Legacy interface method retained for compatibility.
    public void Tick(float deltaTime)
    {
    }

    /// Legacy entrypoint retained for serialized call-sites.
    public void SlowTick()
    {
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["emptyRuntimeTickOrUpdateMethod"]["matches"], 0)

    def test_empty_compile_unit_marker_flags_only_explicit_empty_files(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            empty_marker = root / "AddressablesCompatibility.cs"
            empty_marker.write_text(
                """
// Intentionally empty.
// Addressables compatibility shims were removed because the package is present in the project.
""",
                encoding="utf-8",
            )
            live_file = root / "LiveCompatibility.cs"
            live_file.write_text(
                """
// Intentionally empty comments are harmless when the file still declares a type.
public sealed class LiveCompatibility
{
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["emptyCompileUnitMarker"]["matches"], 1)
            self.assertTrue(cats["emptyCompileUnitMarker"]["examples"][0]["path"].endswith("AddressablesCompatibility.cs"))

    def test_empty_compatibility_noop_method_flags_only_explicit_runtime_debt(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            runtime = root / "RuntimeCompatibility.cs"
            runtime.write_text(
                """
public sealed class RuntimeCompatibility
{
    /// Compatibility no-op. Persistent native snapshots were retired.
    public void WarmupNativeMap()
    {
    }

    /// Legacy no-op retained for external callers.
    public void CancelLegacyExternalPlayback()
    {
    }

    /// Compatibility no-op phrase with actual bridge work.
    public void Bridge()
    {
        EnsureInitialized();
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["emptyCompatibilityNoopMethod"]["matches"], 1)
            self.assertTrue(cats["emptyCompatibilityNoopMethod"]["examples"][0]["path"].endswith("RuntimeCompatibility.cs"))

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

    public bool TryResolveTuningForEditor(out NativeArray<int> tuning)
    {
        tuning = default;
        return false;
    }

    public bool TryGetRows(
        out NativeArray<float> rows,
        out int count)
    {
        rows = default;
        count = 0;
        return false;
    }

    public bool TryReadRows(out NativeArray<long> rows)
    {
        rows = default;
        return false;
    }

    public bool TryReadRows(out NativeArray<int>.ReadOnly rows)
    {
        rows = default;
        return false;
    }

    public NativeHashMap<int, int>.ReadOnly ReadOnlyMap()
    {
        return default;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["nativeCollectionPublicMutableApiExposure"]["matches"], 4)
            self.assertEqual(cats["nativeApiExposureMutableReturn"]["matches"], 1)
            self.assertEqual(cats["nativeApiExposureOutRefMutable"]["matches"], 3)
            self.assertEqual(cats["nativeApiExposureAmbiguousMutable"]["matches"], 0)
            self.assertEqual(cats["nativeApiExposureBuildPlayerRuntime"]["matches"], 4)
            self.assertEqual(cats["nativeApiRiskRuntimeReturnMutableView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeOutRefMutableView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeReadNamedMutableView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeDiagnosticNamedMutableView"]["matches"], 1)

    def test_suppresses_public_native_api_inside_private_nested_type(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "PrivateNestedNativeApi.cs"
            src.write_text(
                """
using Unity.Collections;

public sealed class Owner
{
    private struct VaultNativeArray<T> where T : struct
    {
        public NativeArray<T> Resolve()
        {
            return default;
        }
    }

    struct ImplicitPrivateLease
    {
        public NativeArray<byte> Buffer => default;
    }

    public NativeArray<int> ExposedRows()
    {
        return default;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["nativeCollectionPublicMutableApiExposure"]["matches"], 1)
            self.assertEqual(cats["nativeApiExposurePrivateNestedSuppressed"]["matches"], 2)
            self.assertEqual(cats["nativeApiRiskRuntimeReturnMutableView"]["matches"], 1)

    def test_classifies_private_readonly_native_views_separately_from_mutable_returns(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "PrivateReadOnlyViews.cs"
            src.write_text(
                """
using Unity.Collections;

public sealed class PrivateReadOnlyViews
{
    private NativeArray<int>.ReadOnly ReadRows()
    {
        return default;
    }

    private NativeArray<float> ResolveMutableRows()
    {
        return default;
    }

    private NativeArray<byte>[] _banks;
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["privateNativeCollectionField"]["matches"], 3)
            self.assertEqual(cats["privateNativeCollectionReadOnlyView"]["matches"], 1)
            self.assertEqual(cats["privateNativeDeclarationReadOnlyView"]["matches"], 1)
            self.assertEqual(cats["privateNativeDeclarationMethodReturn"]["matches"], 1)
            self.assertEqual(cats["privateNativeDeclarationField"]["matches"], 1)
            self.assertEqual(cats["privateNativeRiskReadOnlyNativeView"]["matches"], 1)
            self.assertEqual(cats["privateNativeRiskMethodReturningNativeCollection"]["matches"], 1)

    def test_classifies_private_vault_resolver_properties_and_prewarmed_banks(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "PrivateNativeRoutes.cs"
            src.write_text(
                """
using Unity.Collections;

public sealed class PrivateNativeRoutes
{
    private NativeArray<int> _rows => ResolveBuffer(in _rowsHandle);
    private NativeArray<byte>[] _recordBanks = Array.Empty<NativeArray<byte>>();
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["privateNativeCollectionField"]["matches"], 2)
            self.assertEqual(cats["privateNativeCollectionPrewarmedNativeBank"]["matches"], 1)
            self.assertEqual(cats["privateNativeDeclarationField"]["matches"], 1)
            self.assertEqual(cats["privateNativeDeclarationExpressionProperty"]["matches"], 1)
            self.assertEqual(cats["privateNativeDeclarationAmbiguous"]["matches"], 0)
            self.assertEqual(cats["privateNativeRiskVaultAliasOrVaultResolver"]["matches"], 1)
            self.assertEqual(cats["privateNativeRiskOwnerLocalRuntimeNativeState"]["matches"], 1)
            self.assertEqual(cats["privateNativeRiskUnclassifiedNativeCollection"]["matches"], 0)

    def test_classifies_core_native_allocator_surfaces(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            contracts = root / "Core" / "Contracts"
            contracts.mkdir(parents=True)
            utilities = contracts / "CoreLowLevelUtilities.cs"
            utilities.write_text(
                """
using Unity.Collections;

public static class NativeFaultDumpWriter
{
    public static NativeArray<byte> CreateTransientPayload(int byteCount)
    {
        return default;
    }

    public static void DisposeTransientPayload(ref NativeArray<byte> payload)
    {
    }
}
""",
                encoding="utf-8",
            )
            arena = root / "Core" / "NativeArenaArray.cs"
            arena.write_text(
                """
using Unity.Collections;

public struct NativeArenaArray<T> where T : unmanaged
{
    public NativeArray<T> AsNativeArray()
    {
        return default;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["nativeCollectionPublicMutableApiExposure"]["matches"], 3)
            self.assertEqual(cats["nativeApiRiskCoreVaultOrAllocatorSurface"]["matches"], 3)
            self.assertEqual(cats["nativeApiRiskRuntimeReturnMutableView"]["matches"], 0)
            self.assertEqual(cats["nativeApiRiskRuntimeOutRefMutableView"]["matches"], 0)

    def test_scan_tolerates_files_deleted_after_listing(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            missing = root / "DeletedByParallelAgent.cs"

            results = audit.scan_all([missing])

            self.assertEqual(results["packOne"], [])
            self.assertEqual(results["nativeCollectionPublicMutableApiExposure"], [])

    def test_classifies_owner_alias_mutable_native_surfaces(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "OwnerAlias.cs"
            src.write_text(
                """
using Unity.Collections;

public sealed class OwnerAlias
{
    internal NativeArray<int> _rows => ResolveAlias(in _rowsHandle);

    public static bool TryResolveStatesOwnerView(out NativeArray<int> states)
    {
        states = default;
        return false;
    }

    public NativeArray<int> LeakRows()
    {
        return default;
    }

    public NativeArray<uint> AsUIntQuantityView(NativeArray<int> quantities)
    {
        return default;
    }
}

internal ref struct EquipmentVaultView<T> where T : unmanaged
{
    public NativeArray<T> AsNativeArray()
    {
        return default;
    }
}

internal struct InventoryVaultLane<T> where T : struct
{
    public NativeArray<T> Resolve()
    {
        return default;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["nativeCollectionPublicMutableApiExposure"]["matches"], 6)
            self.assertEqual(cats["nativeApiRiskRuntimeOwnerAliasMutableView"]["matches"], 4)
            self.assertEqual(cats["nativeApiRiskRuntimeReinterpretMutableView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeReturnMutableView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeOutRefMutableView"]["matches"], 0)

    def test_classifies_obsolete_mutable_return_wrappers_as_compatibility(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "ObsoleteMutableReturn.cs"
            src.write_text(
                """
using Unity.Collections;

public sealed class ObsoleteMutableReturn
{
    [System.Obsolete("Use the owner alias API; legacy mutable wrapper retained for compatibility.", false)]
    public NativeArray<uint> AsUIntQuantityView(NativeArray<int> quantities)
    {
        return default;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["nativeCollectionPublicMutableApiExposure"]["matches"], 1)
            self.assertEqual(cats["nativeApiExposureMutableReturn"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeObsoleteMutableCompatibilityView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeReinterpretMutableView"]["matches"], 0)
            self.assertEqual(cats["nativeApiRiskRuntimeReturnMutableView"]["matches"], 0)

    def test_classifies_out_ref_mutable_native_routes_by_intent(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "NativeOutRefRoutes.cs"
            src.write_text(
                """
using Unity.Collections;

public sealed class NativeOutRefRoutes
{
    public static bool TryAcquireWriteBuffers(out NativeArray<int> rows)
    {
        rows = default;
        return false;
    }

    public static bool TryGetDeconstructionCsrLanes(out NativeArray<int> rows)
    {
        rows = default;
        return false;
    }

    public static void ExtractJobAliases(out NativeArray<int> rows)
    {
        rows = default;
    }

    public static bool TryGetActiveSurfaceNativePayload(out NativeArray<int> rows)
    {
        rows = default;
        return false;
    }

    public static void DisposeTracked(ref NativeArray<int> rows)
    {
        rows = default;
    }

    public static bool TryResolveDragArrays(out NativeArray<int> velocities)
    {
        velocities = default;
        return false;
    }

    public static bool TryGetRows(out NativeArray<int> rows)
    {
        rows = default;
        return false;
    }

    public static bool TryReadRows(out NativeArray<int> rows)
    {
        rows = default;
        return false;
    }

    [System.Obsolete("Use the NativeArray<T>.ReadOnly overload; legacy mutable wrapper retained for compatibility.", false)]
    public static bool TryReadLegacyRows(out NativeArray<int> rows)
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
            self.assertEqual(cats["nativeCollectionPublicMutableApiExposure"]["matches"], 9)
            self.assertEqual(cats["nativeApiExposureOutRefMutable"]["matches"], 9)
            self.assertEqual(cats["nativeApiRiskRuntimeWriteLeaseMutableView"]["matches"], 2)
            self.assertEqual(cats["nativeApiRiskRuntimeJobAliasMutableView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeNativePayloadMutableView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeDisposeMutableRef"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeOwnerAliasMutableView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeOutRefMutableView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeReadNamedMutableView"]["matches"], 1)
            self.assertEqual(cats["nativeApiRiskRuntimeObsoleteMutableCompatibilityView"]["matches"], 1)

    def test_classifies_unity_time_risk_buckets(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            src = root / "TimeRoutes.cs"
            src.write_text(
                """
using UnityEngine;

public sealed class TimeRoutes
{
    public void Tick()
    {
        int frame = Time.frameCount;
        if (Time.time < _nextWarningLogTime) return;
        float dt = Time.fixedDeltaTime;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["unityTimeCritical"]["matches"], 3)
            self.assertEqual(cats["unityTimeFrameCount"]["matches"], 1)
            self.assertEqual(cats["unityTimeWallClock"]["matches"], 1)
            self.assertEqual(cats["unityTimeDelta"]["matches"], 1)
            self.assertEqual(cats["unityTimeRiskFrameStampOrTelemetry"]["matches"], 1)
            self.assertEqual(cats["unityTimeRiskCooldownOrPerfLog"]["matches"], 1)
            self.assertEqual(cats["unityTimeRiskGameplayDelta"]["matches"], 1)

    def test_classifies_visual_wall_clock_separately_from_gameplay_time(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            visual = root / "WorldShellVisualDriver.cs"
            visual.write_text(
                """
using UnityEngine;

public sealed class WorldShellVisualDriver
{
    public void LateFrameTick()
    {
        float time = Time.time;
    }
}
""",
                encoding="utf-8",
            )
            gameplay = root / "GameplayClockOwner.cs"
            gameplay.write_text(
                """
using UnityEngine;

public sealed class GameplayClockOwner
{
    public void Tick()
    {
        float time = Time.time;
    }
}
""",
                encoding="utf-8",
            )

            payload = audit.build_payload(root)
            cats = payload["categories"]
            self.assertEqual(cats["unityTimeWallClock"]["matches"], 2)
            self.assertEqual(cats["unityTimeRiskVisualPresentationClock"]["matches"], 1)
            self.assertEqual(cats["unityTimeRiskGameplayWallClock"]["matches"], 1)


if __name__ == "__main__":
    unittest.main()
