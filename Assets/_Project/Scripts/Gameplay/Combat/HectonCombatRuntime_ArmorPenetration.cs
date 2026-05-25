using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    internal static class ArmorPenetrationVaultBufferIds
    {
        public const BufferID SignalImpactAups = (BufferID)73580;
        public const BufferID TargetRootAups = (BufferID)73581;
        public const BufferID TargetRotations = (BufferID)73582;
        public const BufferID TargetHalfExtents = (BufferID)73583;
        public const BufferID TargetArmorProfiles = (BufferID)73584;
        public const BufferID TelemetryRing = (BufferID)73585;
        public const BufferID DebugHits = (BufferID)73586;
        public const BufferID Tuning = (BufferID)73587;
        public const BufferID MockRequests = (BufferID)73588;
        public const BufferID MockDetails = (BufferID)73589;
        public const BufferID MockAups = (BufferID)73590;
        public const BufferID MockTargetSlots = (BufferID)73591;
        public const BufferID TortureRequests = (BufferID)73592;
        public const BufferID TortureDetails = (BufferID)73593;
        public const BufferID TortureAups = (BufferID)73594;
        public const BufferID TortureTargetSlots = (BufferID)73595;
        public const BufferID TortureResolvedHits = (BufferID)73596;
        public const BufferID CasTortureHealth = (BufferID)73597;
        public const BufferID CasTortureSuccesses = (BufferID)73598;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public unsafe struct ShinobuArmorPenetrationTable
    {
        public const int MaterialRows = 8;
        public const int AngleSteps = 6;
        public const int CellCount = MaterialRows * AngleSteps;

        [FieldOffset(0)] public fixed byte Cells[CellCount];
        [FieldOffset(48)] public uint Revision;
        [FieldOffset(52)] public uint AuthoringHash;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public unsafe struct ArmorProfileDTO
    {
        [FieldOffset(0)] public uint SpeciesHashID;
        [FieldOffset(4)] public float BaseHealth;
        [FieldOffset(8)] public float BaseArmor;
        [FieldOffset(12)] public uint _pad0;
        [FieldOffset(16)] public fixed byte ArmorGridLUT[ShinobuArmorPenetrationTable.CellCount];
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ArmorPenetrationTuningDTO
    {
        [FieldOffset(0)] public float GlobalArmorMultiplier;
        [FieldOffset(4)] public float WeakPointDamageScalar;
        [FieldOffset(8)] public float ChitinDeflectStrength;
        [FieldOffset(12)] public float SteelDeflectStrength;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float MockGridSpacingMeters;
        [FieldOffset(24)] public uint Revision;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public ulong _pad0;
        [FieldOffset(40)] public ulong _pad1;
        [FieldOffset(48)] public ulong _pad2;
        [FieldOffset(56)] public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ArmorPenetrationTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ImpactCount;
        [FieldOffset(8)] public uint WeakPointHits;
        [FieldOffset(12)] public uint DeflectCount;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public float AvgMitigatedDamage;
        [FieldOffset(24)] public float SolveMicroseconds;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public uint StateHash;
        [FieldOffset(36)] public uint LastMaterialHash;
        [FieldOffset(40)] public uint LastTargetHash;
        [FieldOffset(44)] public uint Reserved;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct ArmorPenetrationDebugHitDTO
    {
        [FieldOffset(0)] public double3 ImpactAup;
        [FieldOffset(24)] public float3 LocalPoint;
        [FieldOffset(36)] public float3 SurfaceNormal;
        [FieldOffset(48)] public uint TargetHash;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public uint MaterialHash;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public float EffectiveArmor;
        [FieldOffset(68)] public float DamageScalar;
        [FieldOffset(72)] public byte LutStrength;
        [FieldOffset(73)] public byte MaterialId;
        [FieldOffset(74)] public ushort Flags;
        [FieldOffset(76)] public uint Reserved0;
        [FieldOffset(80)] public ulong _pad0;
        [FieldOffset(88)] public ulong _pad1;
    }

    internal struct ArmorPenetrationSample
    {
        public double3 ImpactAup;
        public float3 LocalPoint;
        public float3 SurfaceNormal;
        public float EffectiveArmor;
        public float DamageScalar;
        public uint MaterialHash;
        public byte MaterialId;
        public byte MaterialRow;
        public byte AngleStep;
        public byte LutByte;
        public byte RawLutByte;
        public uint Deflected;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct ArmorPenetrationResolvedHitDTO
    {
        [FieldOffset(0)] public int TargetId;
        [FieldOffset(4)] public int SourceId;
        [FieldOffset(8)] public int TargetSlot;
        [FieldOffset(12)] public int DetailIndex;
        [FieldOffset(16)] public float BaseAmount;
        [FieldOffset(20)] public float EffectiveArmor;
        [FieldOffset(24)] public float DamageScalar;
        [FieldOffset(28)] public float DamageBeforeArmor;
        [FieldOffset(32)] public float FinalDamage;
        [FieldOffset(36)] public float ArmorMitigated;
        [FieldOffset(40)] public float3 LocalPoint;
        [FieldOffset(52)] public float3 SurfaceNormal;
        [FieldOffset(64)] public double3 ImpactAup;
        [FieldOffset(88)] public uint MaterialHash;
        [FieldOffset(92)] public uint Flags;
        [FieldOffset(96)] public byte MaterialId;
        [FieldOffset(97)] public byte MaterialRow;
        [FieldOffset(98)] public byte AngleStep;
        [FieldOffset(99)] public byte LutByte;
        [FieldOffset(100)] public uint Reserved0;
        [FieldOffset(104)] public ulong _pad0;
        [FieldOffset(112)] public ulong _pad1;
        [FieldOffset(120)] public ulong _pad2;
    }

    internal struct ArmorPenetrationVaultViews
    {
        public NativeArray<double3> SignalImpactAups;
        public NativeArray<double3> TargetRootAups;
        public NativeArray<quaternion> TargetRotations;
        public NativeArray<float3> TargetHalfExtents;
        public NativeArray<ArmorProfileDTO> TargetArmorProfiles;
        public NativeArray<ArmorPenetrationTelemetryEntry> TelemetryRing;
        public NativeArray<ArmorPenetrationDebugHitDTO> DebugHits;
        public NativeArray<ArmorPenetrationTuningDTO> Tuning;
        public NativeArray<CombatDamageRequest> MockRequests;
        public NativeArray<CombatDamageSignalDetail> MockDetails;
        public NativeArray<double3> MockAups;
        public NativeArray<int> MockTargetSlots;
        public NativeArray<CombatDamageRequest> TortureRequests;
        public NativeArray<CombatDamageSignalDetail> TortureDetails;
        public NativeArray<double3> TortureAups;
        public NativeArray<int> TortureTargetSlots;
        public NativeArray<ArmorPenetrationResolvedHitDTO> TortureResolvedHits;
        public NativeArray<float> CasTortureHealth;
        public NativeArray<int> CasTortureSuccesses;

        public bool HasCoreRuntimeBuffers()
        {
            return SignalImpactAups.IsCreated &&
                   TargetRootAups.IsCreated &&
                   TargetRotations.IsCreated &&
                   TargetHalfExtents.IsCreated &&
                   TargetArmorProfiles.IsCreated &&
                   TelemetryRing.IsCreated &&
                   DebugHits.IsCreated &&
                   Tuning.IsCreated;
        }
    }

    public static partial class CombatDamageRuntime
    {
        internal const byte ArmorWeakPointLutThreshold = 10;
        internal const float ArmorDeflectDamageFloor = 0.25f;

        private const int ArmorMaterialRows = ShinobuArmorPenetrationTable.MaterialRows;
        private const int ArmorAngleSteps = ShinobuArmorPenetrationTable.AngleSteps;
        private const int ArmorGridLutLength = ShinobuArmorPenetrationTable.CellCount;
        private const int ArmorMockSpatialColumns = 8;
        private const int ArmorMockSpatialRows = 6;
        private const int ArmorTelemetryFlagsOverBudget = 1 << 0;
        private const int ArmorTelemetryFlagsNanGuard = 1 << 1;
        private const int ArmorTelemetryFlagsDumped = 1 << 2;
        private const int ArmorMaterialStrengthMask = 0x3F;
        private const int ArmorMaterialShift = 6;
        private const byte ArmorMaterialChitin = 1;
        private const byte ArmorMaterialSteel = 2;
        private const double ArmorTelemetryDumpThresholdMicroseconds = 500.0d;
        private const int ArmorTortureMaxImpacts = 10000;
        private const double ArmorTortureBudgetMicroseconds = 10.0d;
        private const uint ArmorTelemetryMagic = 0x41333138u; // A318
        private const uint ArmorSourceHash = 0x53483318u; // SH318
        private const uint ArmorResolvedFlagWeakPoint = 1u << 0;
        private const uint ArmorResolvedFlagDeflected = 1u << 1;
        private const uint ArmorResolvedFlagNonFinite = 1u << 2;
        private const byte ArmorImpactSignalFlagDeflect = 1 << 0;
        private const byte ArmorImpactSignalFlagDirectionalDeflect = 1 << 1;

        private const SystemID ArmorMemoryOwner = SystemID.GameplayCombat;

        private static IDataVault _armorDataVault;
        private static VaultGenerationHandle<double3> _signalImpactAupsHandle;
        private static VaultGenerationHandle<double3> _targetRootAupsHandle;
        private static VaultGenerationHandle<quaternion> _targetRotationsHandle;
        private static VaultGenerationHandle<float3> _targetHalfExtentsHandle;
        private static VaultGenerationHandle<ArmorProfileDTO> _targetArmorProfilesHandle;
        private static VaultGenerationHandle<ArmorPenetrationTelemetryEntry> _armorTelemetryRingHandle;
        private static VaultGenerationHandle<ArmorPenetrationDebugHitDTO> _armorDebugHitsHandle;
        private static VaultGenerationHandle<ArmorPenetrationTuningDTO> _armorTuningHandle;
        private static VaultGenerationHandle<CombatDamageRequest> _armorMockRequestsHandle;
        private static VaultGenerationHandle<CombatDamageSignalDetail> _armorMockDetailsHandle;
        private static VaultGenerationHandle<double3> _armorMockAupsHandle;
        private static VaultGenerationHandle<int> _armorMockTargetSlotsHandle;
        private static VaultGenerationHandle<CombatDamageRequest> _armorTortureRequestsHandle;
        private static VaultGenerationHandle<CombatDamageSignalDetail> _armorTortureDetailsHandle;
        private static VaultGenerationHandle<double3> _armorTortureAupsHandle;
        private static VaultGenerationHandle<int> _armorTortureTargetSlotsHandle;
        private static VaultGenerationHandle<ArmorPenetrationResolvedHitDTO> _armorTortureResolvedHitsHandle;
        private static VaultGenerationHandle<float> _armorCasTortureHealthHandle;
        private static VaultGenerationHandle<int> _armorCasTortureSuccessesHandle;
        private static int _armorActiveTelemetryIndex;
        private static uint _armorTelemetryCursor;
        private static long _armorScheduleTicks;
        private static bool _armorTelemetryDumped;
        private static bool _armorTelemetryDumpRequested;
        private static bool _armorVaultBuffersLocked;
        private static bool _armorVaultRebindPending;
        private static bool _armorHotSwapRegistered;
        private static IDataVault _armorPendingDataVault;
        private static readonly ArmorRegistryHotSwapBridge _armorHotSwapBridge = new ArmorRegistryHotSwapBridge();
        private static ArmorPenetrationTelemetryEntry _lastArmorTelemetry;

        private static void EnsureArmorPenetrationNativeState()
        {
            RegisterArmorRegistryHotSwapBridge();
            TryApplyPendingArmorVaultRebind();
            TryResolveArmorPenetrationVaultViews(out _, ensure: true);
        }

        private static void DisposeArmorPenetrationNativeState()
        {
            UnregisterArmorRegistryHotSwapBridge();
            IDataVault vault = _armorDataVault;
            ReleaseArmorVaultHandle(vault, ref _signalImpactAupsHandle);
            ReleaseArmorVaultHandle(vault, ref _targetRootAupsHandle);
            ReleaseArmorVaultHandle(vault, ref _targetRotationsHandle);
            ReleaseArmorVaultHandle(vault, ref _targetHalfExtentsHandle);
            ReleaseArmorVaultHandle(vault, ref _targetArmorProfilesHandle);
            ReleaseArmorVaultHandle(vault, ref _armorTelemetryRingHandle);
            ReleaseArmorVaultHandle(vault, ref _armorDebugHitsHandle);
            ReleaseArmorVaultHandle(vault, ref _armorTuningHandle);
            ReleaseArmorVaultHandle(vault, ref _armorMockRequestsHandle);
            ReleaseArmorVaultHandle(vault, ref _armorMockDetailsHandle);
            ReleaseArmorVaultHandle(vault, ref _armorMockAupsHandle);
            ReleaseArmorVaultHandle(vault, ref _armorMockTargetSlotsHandle);
            ReleaseArmorVaultHandle(vault, ref _armorTortureRequestsHandle);
            ReleaseArmorVaultHandle(vault, ref _armorTortureDetailsHandle);
            ReleaseArmorVaultHandle(vault, ref _armorTortureAupsHandle);
            ReleaseArmorVaultHandle(vault, ref _armorTortureTargetSlotsHandle);
            ReleaseArmorVaultHandle(vault, ref _armorTortureResolvedHitsHandle);
            ReleaseArmorVaultHandle(vault, ref _armorCasTortureHealthHandle);
            ReleaseArmorVaultHandle(vault, ref _armorCasTortureSuccessesHandle);
            _armorDataVault = null;
            _armorPendingDataVault = null;
            _armorVaultRebindPending = false;
        }

        private static void RegisterArmorRegistryHotSwapBridge()
        {
            if (!_armorHotSwapRegistered)
                _armorHotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(_armorHotSwapBridge);
        }

        private static void UnregisterArmorRegistryHotSwapBridge()
        {
            if (!_armorHotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(_armorHotSwapBridge);
            _armorHotSwapRegistered = false;
        }

        private static void RequestArmorVaultRebind(IDataVault previousVault, IDataVault currentVault)
        {
            if (_damageJobScheduled)
            {
                if (!_damageJobHandle.IsCompleted)
                {
                    _armorPendingDataVault = currentVault;
                    _armorVaultRebindPending = true;
                    return;
                }

                DispatcherJobSwap.TryFinalizeCompleted(ref _damageJobHandle);
                _damageJobScheduled = false;
                FinishArmorPenetrationScheduledCompletion();
            }

            ApplyArmorVaultRebind(previousVault, currentVault);
        }

        private static void TryApplyPendingArmorVaultRebind()
        {
            if (!_armorVaultRebindPending || (_damageJobScheduled && !_damageJobHandle.IsCompleted))
                return;

            if (_damageJobScheduled)
            {
                DispatcherJobSwap.TryFinalizeCompleted(ref _damageJobHandle);
                _damageJobScheduled = false;
                FinishArmorPenetrationScheduledCompletion();
                return;
            }

            IDataVault previousVault = _armorDataVault;
            IDataVault currentVault = _armorPendingDataVault;
            _armorPendingDataVault = null;
            _armorVaultRebindPending = false;
            ApplyArmorVaultRebind(previousVault, currentVault);
        }

        private static void ApplyArmorVaultRebind(IDataVault previousVault, IDataVault currentVault)
        {
            if (ReferenceEquals(previousVault, currentVault))
            {
                _armorDataVault = currentVault;
                return;
            }

            ReleaseArmorVaultHandle(previousVault, ref _signalImpactAupsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _targetRootAupsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _targetRotationsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _targetHalfExtentsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _targetArmorProfilesHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorTelemetryRingHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorDebugHitsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorTuningHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorMockRequestsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorMockDetailsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorMockAupsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorMockTargetSlotsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorTortureRequestsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorTortureDetailsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorTortureAupsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorTortureTargetSlotsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorTortureResolvedHitsHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorCasTortureHealthHandle);
            ReleaseArmorVaultHandle(previousVault, ref _armorCasTortureSuccessesHandle);
            _armorDataVault = currentVault;

            if (currentVault != null && !currentVault.IsAllocationLocked && !currentVault.IsCompactionFenceActive)
                TryResolveArmorPenetrationVaultViews(out _, ensure: true);
        }

        private sealed class ArmorRegistryHotSwapBridge : IGlobalRegistryHotSwapListener
        {
            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                    return;

                RequestArmorVaultRebind(previousService as IDataVault ?? _armorDataVault, currentService as IDataVault);
            }
        }

        private static bool TryResolveArmorPenetrationVaultViews(
            out ArmorPenetrationVaultViews views,
            bool ensure,
            bool includeMock = false,
            bool includeEvaluatorTorture = false,
            bool includeCasTorture = false)
        {
            views = default;
            IDataVault vault = _armorDataVault;
            if (vault == null && ensure)
            {
                vault = _combatDataVault;
                _armorDataVault = vault;
            }

            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool ok =
                TryResolveArmorVaultBuffer(
                    vault,
                    ref _signalImpactAupsHandle,
                    ArmorPenetrationVaultBufferIds.SignalImpactAups,
                    MaxQueuedSignals,
                    NativeArrayOptions.UninitializedMemory,
                    ensure,
                    out views.SignalImpactAups) &&
                TryResolveArmorVaultBuffer(
                    vault,
                    ref _targetRootAupsHandle,
                    ArmorPenetrationVaultBufferIds.TargetRootAups,
                    MaxTargets,
                    NativeArrayOptions.UninitializedMemory,
                    ensure,
                    out views.TargetRootAups) &&
                TryResolveArmorVaultBuffer(
                    vault,
                    ref _targetRotationsHandle,
                    ArmorPenetrationVaultBufferIds.TargetRotations,
                    MaxTargets,
                    NativeArrayOptions.UninitializedMemory,
                    ensure,
                    out views.TargetRotations) &&
                TryResolveArmorVaultBuffer(
                    vault,
                    ref _targetHalfExtentsHandle,
                    ArmorPenetrationVaultBufferIds.TargetHalfExtents,
                    MaxTargets,
                    NativeArrayOptions.UninitializedMemory,
                    ensure,
                    out views.TargetHalfExtents) &&
                TryResolveArmorVaultBuffer(
                    vault,
                    ref _targetArmorProfilesHandle,
                    ArmorPenetrationVaultBufferIds.TargetArmorProfiles,
                    MaxTargets,
                    NativeArrayOptions.UninitializedMemory,
                    ensure,
                    out views.TargetArmorProfiles) &&
                TryResolveArmorVaultBuffer(
                    vault,
                    ref _armorTelemetryRingHandle,
                    ArmorPenetrationVaultBufferIds.TelemetryRing,
                    TelemetryFrameCapacity,
                    NativeArrayOptions.ClearMemory,
                    ensure,
                    out views.TelemetryRing) &&
                TryResolveArmorVaultBuffer(
                    vault,
                    ref _armorDebugHitsHandle,
                    ArmorPenetrationVaultBufferIds.DebugHits,
                    MaxQueuedSignals,
                    NativeArrayOptions.UninitializedMemory,
                    ensure,
                    out views.DebugHits) &&
                TryResolveArmorVaultBuffer(
                    vault,
                    ref _armorTuningHandle,
                    ArmorPenetrationVaultBufferIds.Tuning,
                    1,
                    NativeArrayOptions.ClearMemory,
                    ensure,
                    out views.Tuning);

            if (includeMock)
            {
                ok =
                    ok &&
                    TryResolveArmorVaultBuffer(
                        vault,
                        ref _armorMockRequestsHandle,
                        ArmorPenetrationVaultBufferIds.MockRequests,
                        MaxQueuedSignals,
                        NativeArrayOptions.UninitializedMemory,
                        ensure,
                        out views.MockRequests) &&
                    TryResolveArmorVaultBuffer(
                        vault,
                        ref _armorMockDetailsHandle,
                        ArmorPenetrationVaultBufferIds.MockDetails,
                        MaxQueuedSignals,
                        NativeArrayOptions.UninitializedMemory,
                        ensure,
                        out views.MockDetails) &&
                    TryResolveArmorVaultBuffer(
                        vault,
                        ref _armorMockAupsHandle,
                        ArmorPenetrationVaultBufferIds.MockAups,
                        MaxQueuedSignals,
                        NativeArrayOptions.UninitializedMemory,
                        ensure,
                        out views.MockAups) &&
                    TryResolveArmorVaultBuffer(
                        vault,
                        ref _armorMockTargetSlotsHandle,
                        ArmorPenetrationVaultBufferIds.MockTargetSlots,
                        MaxQueuedSignals,
                        NativeArrayOptions.UninitializedMemory,
                        ensure,
                        out views.MockTargetSlots);
            }

            if (includeEvaluatorTorture)
            {
                ok =
                    ok &&
                    TryResolveArmorVaultBuffer(
                        vault,
                        ref _armorTortureRequestsHandle,
                        ArmorPenetrationVaultBufferIds.TortureRequests,
                        ArmorTortureMaxImpacts,
                        NativeArrayOptions.UninitializedMemory,
                        ensure,
                        out views.TortureRequests) &&
                    TryResolveArmorVaultBuffer(
                        vault,
                        ref _armorTortureDetailsHandle,
                        ArmorPenetrationVaultBufferIds.TortureDetails,
                        ArmorTortureMaxImpacts,
                        NativeArrayOptions.UninitializedMemory,
                        ensure,
                        out views.TortureDetails) &&
                    TryResolveArmorVaultBuffer(
                        vault,
                        ref _armorTortureAupsHandle,
                        ArmorPenetrationVaultBufferIds.TortureAups,
                        ArmorTortureMaxImpacts,
                        NativeArrayOptions.UninitializedMemory,
                        ensure,
                        out views.TortureAups) &&
                    TryResolveArmorVaultBuffer(
                        vault,
                        ref _armorTortureTargetSlotsHandle,
                        ArmorPenetrationVaultBufferIds.TortureTargetSlots,
                        ArmorTortureMaxImpacts,
                        NativeArrayOptions.UninitializedMemory,
                        ensure,
                        out views.TortureTargetSlots) &&
                    TryResolveArmorVaultBuffer(
                        vault,
                        ref _armorTortureResolvedHitsHandle,
                        ArmorPenetrationVaultBufferIds.TortureResolvedHits,
                        ArmorTortureMaxImpacts,
                        NativeArrayOptions.UninitializedMemory,
                        ensure,
                        out views.TortureResolvedHits);
            }

            if (includeCasTorture)
            {
                ok =
                    ok &&
                    TryResolveArmorVaultBuffer(
                        vault,
                        ref _armorCasTortureHealthHandle,
                        ArmorPenetrationVaultBufferIds.CasTortureHealth,
                        1,
                        NativeArrayOptions.UninitializedMemory,
                        ensure,
                        out views.CasTortureHealth) &&
                    TryResolveArmorVaultBuffer(
                        vault,
                        ref _armorCasTortureSuccessesHandle,
                        ArmorPenetrationVaultBufferIds.CasTortureSuccesses,
                        AtomicHealthCasRetryLimit,
                        NativeArrayOptions.ClearMemory,
                        ensure,
                        out views.CasTortureSuccesses);
            }

            if (!ok)
                return false;

            if (ensure && views.Tuning.IsCreated && views.Tuning.Length > 0 && views.Tuning[0].Revision == 0u &&
                !TryWriteDefaultArmorTuning(vault, ref views))
            {
                return false;
            }

            return views.HasCoreRuntimeBuffers();
        }

        private static bool TryWriteDefaultArmorTuning(IDataVault vault, ref ArmorPenetrationVaultViews views)
        {
            if (vault == null ||
                vault.IsAllocationLocked ||
                !IsArmorVaultHandleCreated(in _armorTuningHandle, ArmorPenetrationVaultBufferIds.Tuning) ||
                !vault.TryAcquireWriteLock(in _armorTuningHandle, ArmorMemoryOwner, out NativeArray<ArmorPenetrationTuningDTO> tuningBuffer))
            {
                return false;
            }

            bool valid = false;
            try
            {
                if (tuningBuffer.IsCreated && tuningBuffer.Length > 0 && tuningBuffer[0].Revision == 0u)
                    tuningBuffer[0] = ResolveDefaultArmorTuning();

                views.Tuning = tuningBuffer;
                valid = tuningBuffer.IsCreated && tuningBuffer.Length > 0 && tuningBuffer[0].Revision != 0u;
            }
            finally
            {
                valid &= vault.ReleaseWriteLock(in _armorTuningHandle, ArmorMemoryOwner);
            }

            return valid;
        }

        private static bool TryResolveArmorVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            bool ensure,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsArmorVaultHandleCreated(in handle, bufferId) &&
                (ensure ? vault.TryResolveHandle(in handle, out buffer) : vault.TryReadHandle(in handle, out buffer)) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!ensure)
            {
                VaultGenerationHandle<T> readHandle = handle;
                if (!vault.TryGetGenerationHandle<T>(bufferId, out readHandle) ||
                    !IsArmorVaultHandleCreated(in readHandle, bufferId))
                {
                    buffer = default;
                    return false;
                }

                return vault.TryReadHandle(in readHandle, out buffer) &&
                       buffer.IsCreated &&
                       buffer.Length >= requiredLength;
            }

            if (vault.IsAllocationLocked)
            {
                VaultGenerationHandle<T> lockedHandle = handle;
                if (!IsArmorVaultHandleCreated(in lockedHandle, bufferId) &&
                    (!vault.TryGetGenerationHandle<T>(bufferId, out lockedHandle) ||
                     !IsArmorVaultHandleCreated(in lockedHandle, bufferId)))
                {
                    buffer = default;
                    return false;
                }

                if (!vault.TryResolveHandle(in lockedHandle, out buffer) ||
                    !buffer.IsCreated ||
                    buffer.Length < requiredLength)
                {
                    buffer = default;
                    return false;
                }

                handle = lockedHandle;
                return true;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, ArmorMemoryOwner, options);
            if (!IsArmorVaultHandleCreated(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsArmorVaultHandleCreated<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)ArmorMemoryOwner &&
                   handle.Generation != 0u;
        }

        private static void ReleaseArmorVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool TryLockArmorVaultBuffersForJobs()
        {
            if (_armorVaultBuffersLocked)
                return true;

            IDataVault vault = _armorDataVault;
            if (vault == null)
                return false;

            int locked = 0;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.SignalImpactAups, ArmorMemoryOwner)) return false;
            locked++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.TargetRootAups, ArmorMemoryOwner)) { UnlockArmorVaultBuffersForJobs(locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.TargetRotations, ArmorMemoryOwner)) { UnlockArmorVaultBuffersForJobs(locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.TargetHalfExtents, ArmorMemoryOwner)) { UnlockArmorVaultBuffersForJobs(locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.TargetArmorProfiles, ArmorMemoryOwner)) { UnlockArmorVaultBuffersForJobs(locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.TelemetryRing, ArmorMemoryOwner)) { UnlockArmorVaultBuffersForJobs(locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.DebugHits, ArmorMemoryOwner)) { UnlockArmorVaultBuffersForJobs(locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.Tuning, ArmorMemoryOwner)) { UnlockArmorVaultBuffersForJobs(locked); return false; }

            _armorVaultBuffersLocked = true;
            return true;
        }

        private static void UnlockArmorVaultBuffersForJobs()
        {
            UnlockArmorVaultBuffersForJobs(8);
        }

        private static void UnlockArmorVaultBuffersForJobs(int lockedCount)
        {
            IDataVault vault = _armorDataVault;
            if (vault == null)
                return;

            if (lockedCount >= 8) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.Tuning, ArmorMemoryOwner);
            if (lockedCount >= 7) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.DebugHits, ArmorMemoryOwner);
            if (lockedCount >= 6) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.TelemetryRing, ArmorMemoryOwner);
            if (lockedCount >= 5) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.TargetArmorProfiles, ArmorMemoryOwner);
            if (lockedCount >= 4) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.TargetHalfExtents, ArmorMemoryOwner);
            if (lockedCount >= 3) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.TargetRotations, ArmorMemoryOwner);
            if (lockedCount >= 2) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.TargetRootAups, ArmorMemoryOwner);
            if (lockedCount >= 1) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.SignalImpactAups, ArmorMemoryOwner);
            _armorVaultBuffersLocked = false;
        }

        private static bool TryLockArmorMockBuffersForJobs(out int lockedCount)
        {
            lockedCount = 0;
            IDataVault vault = _armorDataVault;
            if (vault == null)
                return false;

            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.MockRequests, ArmorMemoryOwner)) return false;
            lockedCount++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.MockDetails, ArmorMemoryOwner)) { UnlockArmorMockBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.MockAups, ArmorMemoryOwner)) { UnlockArmorMockBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.MockTargetSlots, ArmorMemoryOwner)) { UnlockArmorMockBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            return true;
        }

        private static void UnlockArmorMockBuffersForJobs(int lockedCount)
        {
            IDataVault vault = _armorDataVault;
            if (vault == null)
                return;

            if (lockedCount >= 4) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.MockTargetSlots, ArmorMemoryOwner);
            if (lockedCount >= 3) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.MockAups, ArmorMemoryOwner);
            if (lockedCount >= 2) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.MockDetails, ArmorMemoryOwner);
            if (lockedCount >= 1) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.MockRequests, ArmorMemoryOwner);
        }

        private static bool TryLockArmorEvaluatorTortureBuffersForJobs(out int lockedCount)
        {
            lockedCount = 0;
            IDataVault vault = _armorDataVault;
            if (vault == null)
                return false;

            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.TortureRequests, ArmorMemoryOwner)) return false;
            lockedCount++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.TortureDetails, ArmorMemoryOwner)) { UnlockArmorEvaluatorTortureBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.TortureAups, ArmorMemoryOwner)) { UnlockArmorEvaluatorTortureBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.TortureTargetSlots, ArmorMemoryOwner)) { UnlockArmorEvaluatorTortureBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.TortureResolvedHits, ArmorMemoryOwner)) { UnlockArmorEvaluatorTortureBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            return true;
        }

        private static void UnlockArmorEvaluatorTortureBuffersForJobs(int lockedCount)
        {
            IDataVault vault = _armorDataVault;
            if (vault == null)
                return;

            if (lockedCount >= 5) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.TortureResolvedHits, ArmorMemoryOwner);
            if (lockedCount >= 4) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.TortureTargetSlots, ArmorMemoryOwner);
            if (lockedCount >= 3) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.TortureAups, ArmorMemoryOwner);
            if (lockedCount >= 2) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.TortureDetails, ArmorMemoryOwner);
            if (lockedCount >= 1) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.TortureRequests, ArmorMemoryOwner);
        }

        private static bool TryLockArmorCasTortureBuffersForJobs(out int lockedCount)
        {
            lockedCount = 0;
            IDataVault vault = _armorDataVault;
            if (vault == null)
                return false;

            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.CasTortureHealth, ArmorMemoryOwner)) return false;
            lockedCount++;
            if (!vault.TryLockBuffer(ArmorPenetrationVaultBufferIds.CasTortureSuccesses, ArmorMemoryOwner)) { UnlockArmorCasTortureBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            return true;
        }

        private static void UnlockArmorCasTortureBuffersForJobs(int lockedCount)
        {
            IDataVault vault = _armorDataVault;
            if (vault == null)
                return;

            if (lockedCount >= 2) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.CasTortureSuccesses, ArmorMemoryOwner);
            if (lockedCount >= 1) vault.TryUnlockBuffer(ArmorPenetrationVaultBufferIds.CasTortureHealth, ArmorMemoryOwner);
        }

        private static void ResetArmorPenetrationTransientState()
        {
            _armorActiveTelemetryIndex = 0;
            _armorTelemetryCursor = 0u;
            _armorScheduleTicks = 0L;
            _armorTelemetryDumped = false;
            _armorTelemetryDumpRequested = false;
            _lastArmorTelemetry = default;
        }

        private static ArmorPenetrationTuningDTO ResolveDefaultArmorTuning()
        {
            return new ArmorPenetrationTuningDTO
            {
                GlobalArmorMultiplier = 1f,
                WeakPointDamageScalar = 1.18f,
                ChitinDeflectStrength = 54f,
                SteelDeflectStrength = 58f,
                GlobalQualityWeight = 1f,
                MockGridSpacingMeters = 0.45f,
                Revision = 1u,
                Flags = 0u
            };
        }

        private static ArmorPenetrationTuningDTO PrepareArmorTuningForJob(ref ArmorPenetrationVaultViews views)
        {
            ArmorPenetrationTuningDTO tuning = views.Tuning.IsCreated && views.Tuning.Length > 0
                ? views.Tuning[0]
                : ResolveDefaultArmorTuning();
            tuning.GlobalArmorMultiplier = math.clamp(math.select(1f, tuning.GlobalArmorMultiplier, math.isfinite(tuning.GlobalArmorMultiplier)), 0f, 8f);
            tuning.WeakPointDamageScalar = math.clamp(math.select(1.18f, tuning.WeakPointDamageScalar, math.isfinite(tuning.WeakPointDamageScalar)), 0.1f, 8f);
            tuning.ChitinDeflectStrength = math.clamp(math.select(54f, tuning.ChitinDeflectStrength, math.isfinite(tuning.ChitinDeflectStrength)), 0f, 63f);
            tuning.SteelDeflectStrength = math.clamp(math.select(58f, tuning.SteelDeflectStrength, math.isfinite(tuning.SteelDeflectStrength)), 0f, 63f);
            tuning.MockGridSpacingMeters = math.clamp(math.select(0.45f, tuning.MockGridSpacingMeters, math.isfinite(tuning.MockGridSpacingMeters)), 0.05f, 4f);
            tuning.GlobalQualityWeight = ResolveArmorQualityWeight();
            return tuning;
        }

        private static float ResolveArmorQualityWeight()
        {
            float homeostasis = HomeostasisBrain.GlobalQualityWeight;
            float fallback = math.saturate(_visualQualityWeight01);
            return math.saturate(math.select(fallback, homeostasis, math.isfinite(homeostasis)));
        }

        private static int BeginArmorPenetrationSchedule()
        {
            int telemetryLength = TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: false) && views.TelemetryRing.IsCreated
                ? views.TelemetryRing.Length
                : 0;
            _armorActiveTelemetryIndex = telemetryLength > 0
                ? (int)(_armorTelemetryCursor % (uint)telemetryLength)
                : 0;
            _armorTelemetryCursor++;
            _armorScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            return _armorActiveTelemetryIndex;
        }

        private static void FinishArmorPenetrationScheduledCompletion()
        {
            if (!TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: false) ||
                !views.TelemetryRing.IsCreated ||
                views.TelemetryRing.Length == 0)
            {
                UnlockArmorVaultBuffersForJobs();
                return;
            }

            long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - _armorScheduleTicks;
            double microseconds = ticks > 0L
                ? ticks * 1000000.0d / System.Diagnostics.Stopwatch.Frequency
                : 0.0d;
            int index = math.clamp(_armorActiveTelemetryIndex, 0, views.TelemetryRing.Length - 1);
            ArmorPenetrationTelemetryEntry entry = views.TelemetryRing[index];
            entry.SolveMicroseconds = (float)math.min(microseconds, float.MaxValue);
            if (microseconds > ArmorTelemetryDumpThresholdMicroseconds)
                entry.Flags |= ArmorTelemetryFlagsOverBudget;
            if (!math.isfinite(entry.AvgMitigatedDamage) || !math.isfinite(entry.GlobalQualityWeight))
                entry.Flags |= ArmorTelemetryFlagsNanGuard;

            views.TelemetryRing[index] = entry;
            _lastArmorTelemetry = entry;
            if ((entry.Flags & (ArmorTelemetryFlagsNanGuard | ArmorTelemetryFlagsOverBudget)) != 0)
                DumpArmorTelemetryIfNeeded(views.TelemetryRing, entry);

            UnlockArmorVaultBuffersForJobs();
            TryApplyPendingArmorVaultRebind();
        }

        private static void DumpArmorTelemetryIfNeeded(NativeArray<ArmorPenetrationTelemetryEntry> telemetryRing, in ArmorPenetrationTelemetryEntry cause)
        {
            if (_armorTelemetryDumped || !telemetryRing.IsCreated)
                return;

            _armorTelemetryDumpRequested = true;
#if UNITY_EDITOR
            try
            {
                string root = Application.dataPath;
                string projectRoot = Directory.GetParent(root)?.FullName ?? root;
                string logDir = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDir);
                string path = Path.Combine(logDir, "Dump_SHINOBU_318.bin");
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(ArmorTelemetryMagic);
                    writer.Write((uint)UnsafeUtility.SizeOf<ArmorPenetrationTelemetryEntry>());
                    writer.Write((uint)telemetryRing.Length);
                    writer.Write(cause.Frame);
                    writer.Write(cause.Flags | ArmorTelemetryFlagsDumped);
                    int count = math.min(telemetryRing.Length, ArmorTelemetryCapacity);
                    uint cursor = _armorTelemetryCursor;
                    int start = cursor >= (uint)count && count > 0
                        ? (int)(cursor % (uint)count)
                        : 0;
                    for (int i = 0; i < count; i++)
                    {
                        int index = (start + i) % count;
                        WriteArmorTelemetryEntry(writer, telemetryRing[index]);
                    }
                }

                _armorTelemetryDumped = true;
            }
            catch (Exception)
            {
            }
#else
            _armorTelemetryDumped = true;
#endif
        }

        private static void WriteArmorTelemetryEntry(BinaryWriter writer, in ArmorPenetrationTelemetryEntry entry)
        {
            writer.Write(entry.Frame);
            writer.Write(entry.ImpactCount);
            writer.Write(entry.WeakPointHits);
            writer.Write(entry.DeflectCount);
            writer.Write(entry.Flags);
            writer.Write(entry.AvgMitigatedDamage);
            writer.Write(entry.SolveMicroseconds);
            writer.Write(entry.GlobalQualityWeight);
            writer.Write(entry.StateHash);
            writer.Write(entry.LastMaterialHash);
            writer.Write(entry.LastTargetHash);
            writer.Write(entry.Reserved);
            writer.Write(entry._pad0);
            writer.Write(entry._pad1);
        }

        private static void WriteSignalImpactAup(int detailIndex, double3 impactAup)
        {
            if (!TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: false) ||
                !views.SignalImpactAups.IsCreated ||
                (uint)detailIndex >= (uint)views.SignalImpactAups.Length)
                return;

            views.SignalImpactAups[detailIndex] = math.select(double3.zero, impactAup, new bool3(IsFinite(impactAup)));
        }

        private static void SeedTargetArmorProfile(
            int slot,
            int targetId,
            CombatEntityKind kind,
            CombatArmorClass armorClass,
            float safeMaxHealth,
            float armorValue)
        {
            if (!TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: true) ||
                !CanUseArmorTargetSlot(in views, slot))
                return;

            ArmorProfileDTO profile = default;
            profile.SpeciesHashID = unchecked((uint)targetId);
            profile.BaseHealth = math.max(0.0001f, safeMaxHealth);
            profile.BaseArmor = math.max(0f, armorValue);
            unsafe
            {
                for (int materialRow = 0; materialRow < ArmorMaterialRows; materialRow++)
                {
                    for (int angleStep = 0; angleStep < ArmorAngleSteps; angleStep++)
                    {
                        int index = (materialRow * ArmorAngleSteps) + angleStep;
                        profile.ArmorGridLUT[index] = ResolveDefaultArmorCell(kind, armorClass, materialRow, angleStep);
                    }
                }
            }

            views.TargetArmorProfiles[slot] = profile;
            views.TargetRotations[slot] = quaternion.identity;
            views.TargetHalfExtents[slot] = ResolveArmorHalfExtents(
                _targetHeights.IsCreated && (uint)slot < (uint)_targetHeights.Length ? _targetHeights[slot] : 1f);
            views.TargetRootAups[slot] = double3.zero;
        }

        private static void MoveTargetArmorState(int sourceSlot, int destinationSlot)
        {
            if (!TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: false) ||
                !CanUseArmorTargetSlot(in views, sourceSlot) ||
                !CanUseArmorTargetSlot(in views, destinationSlot))
            {
                return;
            }

            views.TargetArmorProfiles[destinationSlot] = views.TargetArmorProfiles[sourceSlot];
            views.TargetRootAups[destinationSlot] = views.TargetRootAups[sourceSlot];
            views.TargetRotations[destinationSlot] = views.TargetRotations[sourceSlot];
            views.TargetHalfExtents[destinationSlot] = views.TargetHalfExtents[sourceSlot];
        }

        private static void RefreshTargetArmorBase(int slot, float armorValue)
        {
            if (!TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: false) ||
                !views.TargetArmorProfiles.IsCreated ||
                (uint)slot >= (uint)views.TargetArmorProfiles.Length)
                return;

            ArmorProfileDTO profile = views.TargetArmorProfiles[slot];
            profile.BaseArmor = math.max(0f, armorValue);
            views.TargetArmorProfiles[slot] = profile;
        }

        private static void ClearTargetArmorState(int slot)
        {
            if (!TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: false) ||
                !CanUseArmorTargetSlot(in views, slot))
                return;

            views.TargetArmorProfiles[slot] = default;
            views.TargetRootAups[slot] = double3.zero;
            views.TargetRotations[slot] = quaternion.identity;
            views.TargetHalfExtents[slot] = float3.zero;
        }

        private static bool CanUseArmorTargetSlot(in ArmorPenetrationVaultViews views, int slot)
        {
            return views.TargetArmorProfiles.IsCreated &&
                   (uint)slot < (uint)views.TargetArmorProfiles.Length &&
                   views.TargetRootAups.IsCreated &&
                   (uint)slot < (uint)views.TargetRootAups.Length &&
                   views.TargetRotations.IsCreated &&
                   (uint)slot < (uint)views.TargetRotations.Length &&
                   views.TargetHalfExtents.IsCreated &&
                   (uint)slot < (uint)views.TargetHalfExtents.Length;
        }

        private static bool CanUseArmorEvaluatorTargetBuffers(in ArmorPenetrationVaultViews views, int targetCount)
        {
            return targetCount > 0 &&
                   _instanceIds.IsCreated &&
                   (uint)targetCount <= (uint)_instanceIds.Length &&
                   _targetFlags.IsCreated &&
                   (uint)targetCount <= (uint)_targetFlags.Length &&
                   _targetHeights.IsCreated &&
                   (uint)targetCount <= (uint)_targetHeights.Length &&
                   _damageArmorLut.IsCreated &&
                   _damageArmorLut.Length >= DamageArmorLutLength &&
                   views.TargetRootAups.IsCreated &&
                   (uint)targetCount <= (uint)views.TargetRootAups.Length &&
                   views.TargetRotations.IsCreated &&
                   (uint)targetCount <= (uint)views.TargetRotations.Length &&
                   views.TargetHalfExtents.IsCreated &&
                   (uint)targetCount <= (uint)views.TargetHalfExtents.Length &&
                   views.TargetArmorProfiles.IsCreated &&
                   (uint)targetCount <= (uint)views.TargetArmorProfiles.Length;
        }

        private static bool CanUseArmorMockSignalBuffers(in ArmorPenetrationVaultViews views, int count)
        {
            return count > 0 &&
                   views.MockRequests.IsCreated &&
                   (uint)count <= (uint)views.MockRequests.Length &&
                   views.MockDetails.IsCreated &&
                   (uint)count <= (uint)views.MockDetails.Length &&
                   views.MockAups.IsCreated &&
                   (uint)count <= (uint)views.MockAups.Length &&
                   views.MockTargetSlots.IsCreated &&
                   (uint)count <= (uint)views.MockTargetSlots.Length;
        }

        private static void RefreshArmorTargetSnapshots()
        {
            if (!TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: true))
                return;

            RefreshArmorTargetSnapshots(ref views);
        }

        private static void RefreshArmorTargetSnapshots(ref ArmorPenetrationVaultViews views)
        {
            if (!views.TargetRootAups.IsCreated ||
                !views.TargetRotations.IsCreated ||
                !views.TargetHalfExtents.IsCreated ||
                _receiverTransforms == null ||
                !_targetHeights.IsCreated)
            {
                return;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            double3 origin = originAup.IsFinite() ? originAup.ToAbsoluteDouble3() : HectonFloatingOrigin.CurrentTotalOffsetDouble;
            int count = math.min(
                math.max(0, _targetCount),
                math.min(
                    _receiverTransforms.Length,
                    math.min(
                        _targetHeights.Length,
                        math.min(views.TargetRootAups.Length, math.min(views.TargetRotations.Length, views.TargetHalfExtents.Length)))));
            for (int i = 0; i < count; i++)
            {
                Transform transform = _receiverTransforms[i];
                if (transform == null)
                {
                    views.TargetRootAups[i] = double3.zero;
                    views.TargetRotations[i] = quaternion.identity;
                    views.TargetHalfExtents[i] = ResolveArmorHalfExtents(_targetHeights[i]);
                    continue;
                }

                Vector3 position = transform.position;
                double3 runtime = new double3(position.x, position.y, position.z);
                views.TargetRootAups[i] = IsFinite(runtime) ? origin + runtime : origin;
                Quaternion rotation = transform.rotation;
                quaternion q = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
                float lengthSq = math.lengthsq(q.value);
                bool validRotation = (lengthSq > 0.0001f) & math.all(math.isfinite(q.value));
                views.TargetRotations[i] = new quaternion(math.select(
                    quaternion.identity.value,
                    q.value * math.rsqrt(math.max(lengthSq, 0.0001f)),
                    new bool4(validRotation)));
                views.TargetHalfExtents[i] = ResolveArmorHalfExtents(_targetHeights[i]);
            }
        }

        private static float3 ResolveArmorHalfExtents(float targetHeight)
        {
            float height = math.max(0.25f, math.select(1f, targetHeight, math.isfinite(targetHeight)));
            float radius = math.max(0.125f, height * 0.35f);
            return new float3(radius, height * 0.5f, radius);
        }

        private static byte ResolveDefaultArmorCell(CombatEntityKind kind, CombatArmorClass armorClass, int materialRow, int angleStep)
        {
            byte material = armorClass == CombatArmorClass.Structure || armorClass == CombatArmorClass.Shielded || armorClass == CombatArmorClass.Suit
                ? ArmorMaterialSteel
                : ArmorMaterialChitin;
            byte strength = ResolveDefaultArmorStrength(kind, armorClass, materialRow, angleStep);
            return ComposeArmorByte(material, strength);
        }

        private static byte ResolveDefaultArmorStrength(CombatEntityKind kind, CombatArmorClass armorClass, int materialRow, int angleStep)
        {
            int baseStrength = 32;
            switch (armorClass)
            {
                case CombatArmorClass.None:
                    baseStrength = 4;
                    break;
                case CombatArmorClass.Suit:
                    baseStrength = 36;
                    break;
                case CombatArmorClass.Shell:
                    baseStrength = 50;
                    break;
                case CombatArmorClass.Structure:
                    baseStrength = 56;
                    break;
                case CombatArmorClass.OrganicHeavy:
                    baseStrength = 48;
                    break;
                case CombatArmorClass.Brittle:
                    baseStrength = 26;
                    break;
                case CombatArmorClass.Shielded:
                    baseStrength = 60;
                    break;
            }

            int grazingBonus = math.clamp(angleStep, 0, ArmorAngleSteps - 1) * 5;
            int materialAdjustment = ResolveProjectileMaterialArmorAdjustment(kind, armorClass, math.clamp(materialRow, 0, ArmorMaterialRows - 1));
            return (byte)math.clamp(baseStrength + grazingBonus + materialAdjustment, 0, ArmorMaterialStrengthMask);
        }

        private static int ResolveProjectileMaterialArmorAdjustment(CombatEntityKind kind, CombatArmorClass armorClass, int materialRow)
        {
            switch (materialRow)
            {
                case 1:
                    return armorClass == CombatArmorClass.Structure || armorClass == CombatArmorClass.Shielded ? 4 : -4;
                case 2:
                    return kind == CombatEntityKind.Fauna ? -8 : 8;
                case 3:
                    return armorClass == CombatArmorClass.Shielded ? -12 : 6;
                case 4:
                    return -2;
                case 5:
                    return kind == CombatEntityKind.Fauna ? -5 : 6;
                case 6:
                    return -3;
                case 7:
                    return -10;
                default:
                    return 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ComposeArmorByte(byte material, byte strength)
        {
            return (byte)(((material & 0x3) << ArmorMaterialShift) | (strength & ArmorMaterialStrengthMask));
        }

        internal static unsafe ArmorPenetrationSample EvaluateArmorPenetrationForSignal(
            int slot,
            int detailIndex,
            in CombatDamageRequest signal,
            in CombatDamageSignalDetail detail,
            in NativeArray<double3> signalImpactAups,
            in NativeArray<double3> targetRootAups,
            in NativeArray<quaternion> targetRotations,
            in NativeArray<float3> targetHalfExtents,
            in NativeArray<ArmorProfileDTO> targetArmorProfiles,
            in ArmorPenetrationTuningDTO tuning)
        {
            ArmorProfileDTO defaultProfile = default;
            ArmorProfileDTO* profilePtr = &defaultProfile;
            if (targetArmorProfiles.IsCreated && (uint)slot < (uint)targetArmorProfiles.Length)
            {
                profilePtr = ((ArmorProfileDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(targetArmorProfiles)) + slot;
            }

            double3 impactAup = signalImpactAups.IsCreated && (uint)detailIndex < (uint)signalImpactAups.Length
                ? signalImpactAups[detailIndex]
                : double3.zero;
            double3 rootAup = targetRootAups.IsCreated && (uint)slot < (uint)targetRootAups.Length
                ? targetRootAups[slot]
                : double3.zero;
            quaternion rotation = targetRotations.IsCreated && (uint)slot < (uint)targetRotations.Length
                ? targetRotations[slot]
                : quaternion.identity;
            float3 extents = targetHalfExtents.IsCreated && (uint)slot < (uint)targetHalfExtents.Length
                ? math.max(targetHalfExtents[slot], new float3(0.125f))
                : new float3(0.35f, 0.5f, 0.35f);

            return EvaluateArmorPenetrationCore(
                in signal,
                in detail,
                impactAup,
                rootAup,
                rotation,
                extents,
                profilePtr,
                in tuning);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ArmorPenetrationSample EvaluateArmorPenetrationCore(
            in CombatDamageRequest signal,
            in CombatDamageSignalDetail detail,
            double3 impactAup,
            double3 rootAup,
            quaternion rotation,
            float3 extents,
            ArmorProfileDTO* profilePtr,
            in ArmorPenetrationTuningDTO tuning)
        {
            double3 delta = impactAup - rootAup;
            float3 localFromAup = math.mul(math.conjugate(rotation), new float3((float)delta.x, (float)delta.y, (float)delta.z));
            bool aupValid =
                math.isfinite(impactAup.x) &
                math.isfinite(impactAup.y) &
                math.isfinite(impactAup.z) &
                math.isfinite(rootAup.x) &
                math.isfinite(rootAup.y) &
                math.isfinite(rootAup.z) &
                math.isfinite(delta.x) &
                math.isfinite(delta.y) &
                math.isfinite(delta.z) &
                math.any(impactAup != double3.zero);
            float3 localPoint = math.select(detail.LocalPoint, localFromAup, new bool3(aupValid));
            localPoint = SanitizeFinite(localPoint, SanitizeFinite(detail.LocalPoint, float3.zero));
            float3 normal = ResolveArmorSurfaceNormal(localPoint, math.max(extents, new float3(0.125f)), detail.ArmorNormal);
            int materialRow = ResolveArmorMaterialRow(signal.PackedMeta);
            int angleStep = ResolveArmorAngleStep(signal.Direction, normal);
            int lutIndex = (materialRow * ArmorAngleSteps) + angleStep;
            byte raw = profilePtr->ArmorGridLUT[lutIndex];
            byte materialBits = (byte)((raw >> ArmorMaterialShift) & 0x3);
            byte strengthByte = (byte)(raw & ArmorMaterialStrengthMask);
            float strength01 = strengthByte * (1f / ArmorMaterialStrengthMask);
            float preciseWeight = Smooth01(tuning.GlobalQualityWeight);
            float baseArmor = math.max(0f, profilePtr->BaseArmor);
            float lutArmor = baseArmor * strength01 * math.max(0f, tuning.GlobalArmorMultiplier);
            float effectiveArmor = math.lerp(baseArmor * 0.65f, lutArmor, preciseWeight);
            float weakWeight = math.saturate(1f - strengthByte * (1f / math.max(1f, ArmorWeakPointLutThreshold)));
            float damageScalar = math.lerp(1f, math.max(1f, tuning.WeakPointDamageScalar), weakWeight * preciseWeight);
            bool steelMaterial = materialBits == ArmorMaterialSteel;
            byte materialId = (byte)math.select(
                (int)HighSpeedImpactSignal.MaterialOrganic,
                (int)HighSpeedImpactSignal.MaterialMetal,
                steelMaterial);
            float deflectStrength = math.select(tuning.ChitinDeflectStrength, tuning.SteelDeflectStrength, steelMaterial);
            uint deflected = math.select(0u, 1u, strengthByte >= (byte)math.clamp(deflectStrength, 0f, 63f));
            uint materialHash = HighSpeedImpactSignal.ComposeMaterialHash(unchecked((uint)signal.TargetId), materialId, strengthByte);
            return new ArmorPenetrationSample
            {
                ImpactAup = impactAup,
                LocalPoint = localPoint,
                SurfaceNormal = normal,
                EffectiveArmor = effectiveArmor,
                DamageScalar = damageScalar,
                MaterialHash = materialHash,
                MaterialId = materialId,
                MaterialRow = (byte)materialRow,
                AngleStep = (byte)angleStep,
                LutByte = strengthByte,
                RawLutByte = raw,
                Deflected = deflected
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ArmorPenetrationResolvedHitDTO BuildArmorPenetrationResolvedHit(
            int slot,
            int detailIndex,
            in CombatDamageRequest signal,
            in CombatDamageSignalDetail detail,
            in ArmorPenetrationSample sample,
            uint targetFlags,
            float targetHeight,
            in NativeArray<float> damageArmorLut)
        {
            byte kind = (byte)((targetFlags >> TargetFlagKindShift) & 0xFu);
            int armorClass = math.clamp((int)(targetFlags & TargetFlagArmorMask), 0, ArmorClassCount - 1);
            int damageClass = math.clamp((int)ReadDamageClass(signal.PackedMeta), 0, DamageClassCount - 1);
            float armorMultiplier = damageArmorLut[(damageClass * ArmorClassCount) + armorClass];
            float3 projectileDirection = NormalizeArmorLookup(signal.Direction, float3.zero);
            float3 armorNormal = NormalizeArmorLookup(sample.SurfaceNormal, float3.zero);
            float directionalArmorMultiplier = math.saturate(math.dot(projectileDirection, armorNormal) + 0.2f);
            bool hasDirectionalArmorProof = (math.lengthsq(projectileDirection) > 0.0001f) &
                                            (math.lengthsq(armorNormal) > 0.0001f);
            armorMultiplier *= math.select(1f, directionalArmorMultiplier, hasDirectionalArmorProof);

            int weakspotTier = ReadWeakspotTier(signal.PackedMeta);
            float weakspotMultiplier = math.select(1f, 3f, weakspotTier == (int)CombatWeakspotTier.Weakspot);
            bool headshot = math.all(math.isfinite(detail.LocalPoint)) &
                            (targetHeight > 0.0001f) &
                            (detail.LocalPoint.y > targetHeight * HeadshotHeightFraction);
            weakspotMultiplier = math.select(
                weakspotMultiplier,
                math.max(weakspotMultiplier, HeadshotDamageMultiplier),
                headshot);

            float baseAmount = ResolveBranchlessBaseDamage(signal.Amount, signal.Direction, signal.ImpulseMagnitude, kind);
            float momentumMultiplier = ResolveBranchlessMomentumMultiplier(signal.Amount, signal.Direction);
            float damageBeforeArmor = math.max(0f, baseAmount * momentumMultiplier * weakspotMultiplier * armorMultiplier * sample.DamageScalar);
            float finalDamage = math.max(0f, damageBeforeArmor - sample.EffectiveArmor);
            float armorMitigated = math.max(0f, damageBeforeArmor - finalDamage);
            uint weakFlag = math.select(0u, ArmorResolvedFlagWeakPoint, sample.LutByte <= ArmorWeakPointLutThreshold);
            uint deflectFlag = math.select(
                0u,
                ArmorResolvedFlagDeflected,
                (sample.Deflected != 0u) & (finalDamage <= ArmorDeflectDamageFloor));
            uint finiteFlag = math.select(
                ArmorResolvedFlagNonFinite,
                0u,
                math.isfinite(baseAmount) &
                math.isfinite(momentumMultiplier) &
                math.isfinite(damageBeforeArmor) &
                math.isfinite(finalDamage));

            return new ArmorPenetrationResolvedHitDTO
            {
                TargetId = signal.TargetId,
                SourceId = signal.SourceId,
                TargetSlot = slot,
                DetailIndex = detailIndex,
                BaseAmount = baseAmount,
                EffectiveArmor = sample.EffectiveArmor,
                DamageScalar = sample.DamageScalar,
                DamageBeforeArmor = damageBeforeArmor,
                FinalDamage = finalDamage,
                ArmorMitigated = armorMitigated,
                LocalPoint = sample.LocalPoint,
                SurfaceNormal = sample.SurfaceNormal,
                ImpactAup = sample.ImpactAup,
                MaterialHash = sample.MaterialHash,
                Flags = weakFlag | deflectFlag | finiteFlag,
                MaterialId = sample.MaterialId,
                MaterialRow = sample.MaterialRow,
                AngleStep = sample.AngleStep,
                LutByte = sample.LutByte,
                Reserved0 = unchecked((uint)armorClass | ((uint)damageClass << 8))
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveBranchlessBaseDamage(float amount, float3 impulseVector, float impulseMagnitude, byte kind)
        {
            bool amountValid = math.isfinite(amount) & (amount > 0f);
            float safeAmount = math.select(0f, amount, amountValid);
            bool impulseValid = math.isfinite(impulseMagnitude) & (impulseMagnitude > 0f);
            float safeImpulse = math.select(0f, impulseMagnitude, impulseValid);
            float lengthSq = math.lengthsq(impulseVector);
            bool directionValid = math.all(math.isfinite(impulseVector)) & (lengthSq > 0.0001f);
            float nonPlayerKinetic = lengthSq * math.rsqrt(math.max(lengthSq, 0.0001f));
            float playerKinetic = lengthSq;
            float kinetic = math.select(nonPlayerKinetic, playerKinetic, kind == (byte)CombatEntityKind.Player);
            kinetic = math.select(0f, kinetic, directionValid);
            float fallbackDamage = math.select(kinetic, safeImpulse, impulseValid);
            return math.select(fallbackDamage, safeAmount, amountValid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveBranchlessMomentumMultiplier(float amount, float3 attackerVelocity)
        {
            float lengthSq = math.lengthsq(attackerVelocity);
            bool validVelocity = math.all(math.isfinite(attackerVelocity)) & (lengthSq > 0.0001f);
            bool amountDriven = math.isfinite(amount) & (amount > 0f);
            float momentum = math.clamp(lengthSq, 1f, MaxMomentumDamageMultiplier);
            return math.select(1f, momentum, amountDriven & validVelocity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveArmorMaterialRow(uint packedMeta)
        {
            return (int)(ReadDamageClass(packedMeta) & (uint)(ArmorMaterialRows - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveArmorAngleStep(float3 projectileDirection, float3 armorNormal)
        {
            float3 direction = NormalizeArmorLookup(projectileDirection, new float3(0f, 0f, 1f));
            float3 normal = NormalizeArmorLookup(armorNormal, new float3(0f, 0f, 1f));
            float attackDot = math.saturate(math.abs(math.dot(direction, normal)));
            return math.clamp((int)math.floor((1f - attackDot) * ArmorAngleSteps), 0, ArmorAngleSteps - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeArmorLookup(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            bool valid = math.all(math.isfinite(value)) & (lengthSq > 0.0001f);
            float3 selected = math.select(fallback, value, new bool3(valid));
            return selected * math.rsqrt(math.max(math.lengthsq(selected), 0.0001f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveArmorSurfaceNormal(float3 localPoint, float3 extents, float3 fallback)
        {
            float3 scaled = localPoint * math.rcp(math.max(extents, new float3(0.0001f)));
            float3 absScaled = math.abs(scaled);
            bool xMajor = (absScaled.x >= absScaled.y) & (absScaled.x >= absScaled.z);
            bool yMajor = (!xMajor) & (absScaled.y >= absScaled.z);
            float3 xNormal = new float3(math.select(-1f, 1f, scaled.x >= 0f), 0f, 0f);
            float3 yNormal = new float3(0f, math.select(-1f, 1f, scaled.y >= 0f), 0f);
            float3 zNormal = new float3(0f, 0f, math.select(-1f, 1f, scaled.z >= 0f));
            float3 normal = math.select(zNormal, yNormal, new bool3(yMajor));
            normal = math.select(normal, xNormal, new bool3(xMajor));
            return NormalizeArmorLookup(normal, NormalizeArmorLookup(fallback, new float3(0f, 0f, 1f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float x = math.saturate(math.select(0f, value, math.isfinite(value)));
            return x * x * (3f - (2f * x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.select(fallback, value, new bool3(math.all(math.isfinite(value))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double3 value)
        {
            return CombatDamageSignalCodec.IsFiniteAup(value);
        }

        internal static void EmitArmorDeflectFeedback(
            global::Hecton8.Core.MpscSignalRingBuffer<DeflectSignal>.ParallelWriter deflectWriter,
            NativeArray<int> deflectWriterBudget,
            global::Hecton8.Core.MpscSignalRingBuffer<ImpactSignal>.ParallelWriter impactWriter,
            NativeArray<int> impactWriterBudget,
            in CombatDamageRequest signal,
            in CombatDamageSignalDetail detail,
            in ArmorPenetrationSample sample,
            int armorClass,
            float preMitigationDamage,
            float visualQualityWeight01)
        {
            SignalBus<DeflectSignal>.TryEnqueueBounded(deflectWriter, deflectWriterBudget, new DeflectSignal
            {
                LocalPoint = sample.LocalPoint,
                FrontDot = math.dot(NormalizeArmorLookup(signal.Direction, float3.zero), sample.SurfaceNormal),
                TargetHash = unchecked((uint)signal.TargetId),
                SourceHash = unchecked((uint)signal.SourceId),
                DamageScalar = math.saturate(preMitigationDamage * 0.01f),
                Flags = sample.RawLutByte,
                ArmorClass = (byte)armorClass,
                Reserved = 0
            });

            EmitArmorImpactFeedback(
                impactWriter,
                impactWriterBudget,
                in sample,
                preMitigationDamage,
                visualQualityWeight01,
                ArmorImpactSignalFlagDeflect);
        }

        internal static void EmitArmorImpactFeedback(
            global::Hecton8.Core.MpscSignalRingBuffer<ImpactSignal>.ParallelWriter impactWriter,
            NativeArray<int> impactWriterBudget,
            in ArmorPenetrationSample sample,
            float preMitigationDamage,
            float visualQualityWeight01,
            byte flags)
        {
            float q = math.saturate(math.select(0f, visualQualityWeight01, math.isfinite(visualQualityWeight01)));
            if (q <= 0.02f || !IsFinite(sample.ImpactAup))
                return;

            SignalBus<ImpactSignal>.TryEnqueueBounded(impactWriter, impactWriterBudget, new ImpactSignal
            {
                PointAup = AbsoluteUniversePosition.FromAbsolutePosition(sample.ImpactAup),
                Intensity = math.saturate((preMitigationDamage * 0.02f) * math.lerp(0.35f, 1f, q)),
                MaterialHash = sample.MaterialHash,
                WeightClass = (byte)math.clamp((int)math.round(preMitigationDamage), 0, byte.MaxValue),
                PrimaryMaterialId = sample.MaterialId,
                SecondaryMaterialId = sample.LutByte,
                Flags = flags
            });
        }

        internal static unsafe bool TryAtomicSubtractHealth(
            NativeArray<float> health,
            int slot,
            float damage,
            out float previousHealth,
            out float nextHealth)
        {
            previousHealth = 0f;
            nextHealth = 0f;
            if (!health.IsCreated || (uint)slot >= (uint)health.Length)
                return false;

            float safeDamage = math.max(0f, math.select(0f, damage, math.isfinite(damage)));
            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(health);
            int* bits = (int*)ptr + slot;
            ref int location = ref UnsafeUtility.AsRef<int>((void*)bits);
            for (int i = 0; i < AtomicHealthCasRetryLimit; i++)
            {
                int observed = Interlocked.CompareExchange(ref location, 0, 0);
                previousHealth = math.asfloat(unchecked((uint)observed));
                if (!math.isfinite(previousHealth))
                {
                    nextHealth = 0f;
                    return false;
                }

                nextHealth = math.max(0f, previousHealth - safeDamage);
                int desired = unchecked((int)math.asuint(nextHealth));
                if (Interlocked.CompareExchange(ref location, desired, observed) == observed)
                    return true;
            }

            return false;
        }

        public static bool RunAtomicHealthCasTortureProof(int pelletCount, out int successCount, out float finalHealth)
        {
            successCount = 0;
            finalHealth = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureInitialized();
            if (_damageJobScheduled ||
                !TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: true, includeCasTorture: true) ||
                !views.CasTortureHealth.IsCreated ||
                !views.CasTortureSuccesses.IsCreated)
            {
                return false;
            }

            int count = math.clamp(math.select(pelletCount, 100, pelletCount <= 0), 1, AtomicHealthCasRetryLimit);
            if (views.CasTortureHealth.Length < 1 || views.CasTortureSuccesses.Length < count)
                return false;

            int lockedCasBuffers = 0;
            try
            {
                if (!TryLockArmorCasTortureBuffersForJobs(out lockedCasBuffers))
                {
                    lockedCasBuffers = 0;
                    return false;
                }

                views.CasTortureHealth[0] = count;
                for (int i = 0; i < count; i++)
                    views.CasTortureSuccesses[i] = 0;

                AtomicHealthCasTortureJob job = new AtomicHealthCasTortureJob
                {
                    Health = views.CasTortureHealth,
                    Successes = views.CasTortureSuccesses
                };
                JobHandle handle = job.Schedule(count, 64);
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true); // COLD EDITOR/QA ONLY: same-slot CAS storm proof, never part of FrameTick.

                for (int i = 0; i < count; i++)
                    successCount += views.CasTortureSuccesses[i];
                finalHealth = views.CasTortureHealth[0];
                return successCount == count && finalHealth <= 0.0001f;
            }
            finally
            {
                if (lockedCasBuffers > 0)
                    UnlockArmorCasTortureBuffersForJobs(lockedCasBuffers);
            }
#else
            return false;
#endif
        }

        internal static void WriteArmorDebugHit(
            NativeArray<ArmorPenetrationDebugHitDTO> debugHits,
            int index,
            in CombatDamageRequest signal,
            in ArmorPenetrationSample sample,
            uint frame)
        {
            if (!debugHits.IsCreated || (uint)index >= (uint)debugHits.Length)
                return;

            debugHits[index] = new ArmorPenetrationDebugHitDTO
            {
                ImpactAup = sample.ImpactAup,
                LocalPoint = sample.LocalPoint,
                SurfaceNormal = sample.SurfaceNormal,
                TargetHash = unchecked((uint)signal.TargetId),
                SourceHash = unchecked((uint)signal.SourceId),
                MaterialHash = sample.MaterialHash,
                Frame = frame,
                EffectiveArmor = sample.EffectiveArmor,
                DamageScalar = sample.DamageScalar,
                LutStrength = sample.LutByte,
                MaterialId = sample.MaterialId,
                Flags = (ushort)sample.Deflected,
                Reserved0 = (uint)(sample.MaterialRow | (sample.AngleStep << 8))
            };
        }

        internal static void WriteArmorTelemetry(
            NativeArray<ArmorPenetrationTelemetryEntry> telemetryRing,
            int telemetryIndex,
            uint frame,
            uint impactCount,
            uint weakPointHits,
            uint deflectCount,
            float mitigatedDamageSum,
            float globalQualityWeight)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length == 0)
                return;

            int index = math.clamp(telemetryIndex, 0, telemetryRing.Length - 1);
            float avgMitigated = math.select(0f, mitigatedDamageSum * math.rcp((float)math.max(1u, impactCount)), impactCount > 0u);
            uint flags = math.select(
                0u,
                (uint)ArmorTelemetryFlagsNanGuard,
                !math.isfinite(avgMitigated) || !math.isfinite(globalQualityWeight));
            telemetryRing[index] = new ArmorPenetrationTelemetryEntry
            {
                Frame = frame,
                ImpactCount = impactCount,
                WeakPointHits = weakPointHits,
                DeflectCount = deflectCount,
                Flags = flags,
                AvgMitigatedDamage = math.select(0f, avgMitigated, math.isfinite(avgMitigated)),
                SolveMicroseconds = 0f,
                GlobalQualityWeight = math.saturate(math.select(0f, globalQualityWeight, math.isfinite(globalQualityWeight))),
                StateHash = math.hash(new uint4(frame, impactCount, weakPointHits, deflectCount)),
                LastMaterialHash = 0u,
                LastTargetHash = 0u,
                Reserved = 0u
            };
        }

        public static bool TryGetLastArmorTelemetry(out ArmorPenetrationTelemetryEntry telemetry)
        {
            telemetry = _lastArmorTelemetry;
            return _armorTelemetryRingHandle.BufferID != 0u && _armorTelemetryRingHandle.Generation != 0u;
        }

        public static bool ReadArmorTelemetryDumpRequested()
        {
            return _armorTelemetryDumpRequested;
        }

        public static bool TryGetArmorTuning(out ArmorPenetrationTuningDTO tuning)
        {
            tuning = default;
            if (!TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: false) ||
                !views.Tuning.IsCreated ||
                views.Tuning.Length == 0)
            {
                return false;
            }

            tuning = views.Tuning[0];
            return true;
        }

        public static bool WriteArmorTuning(in ArmorPenetrationTuningDTO tuning)
        {
            if (!TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: true) ||
                !views.Tuning.IsCreated ||
                views.Tuning.Length == 0 ||
                _damageJobScheduled)
            {
                return false;
            }

            IDataVault vault = _armorDataVault;
            if (vault == null ||
                !IsArmorVaultHandleCreated(in _armorTuningHandle, ArmorPenetrationVaultBufferIds.Tuning) ||
                !vault.TryAcquireWriteLock(in _armorTuningHandle, ArmorMemoryOwner, out NativeArray<ArmorPenetrationTuningDTO> tuningBuffer))
            {
                return false;
            }

            bool written = false;
            try
            {
                written = tuningBuffer.IsCreated && tuningBuffer.Length > 0;
                if (written)
                    tuningBuffer[0] = tuning;
            }
            finally
            {
                vault.ReleaseWriteLock(in _armorTuningHandle, ArmorMemoryOwner);
            }

            return written;
        }

        public static bool TryGetArmorDebugBuffers(
            out NativeArray<ArmorProfileDTO>.ReadOnly profiles,
            out NativeArray<double3>.ReadOnly targetAups,
            out NativeArray<float3>.ReadOnly halfExtents,
            out NativeArray<ArmorPenetrationDebugHitDTO>.ReadOnly hits,
            out int targetCount)
        {
            profiles = default;
            targetAups = default;
            halfExtents = default;
            hits = default;
            targetCount = 0;
            if (_damageJobScheduled ||
                !TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: false) ||
                !views.TargetArmorProfiles.IsCreated ||
                !views.TargetRootAups.IsCreated ||
                !views.TargetHalfExtents.IsCreated ||
                !views.DebugHits.IsCreated)
            {
                return false;
            }

            int availableTargets = math.min(
                views.TargetArmorProfiles.Length,
                math.min(views.TargetRootAups.Length, views.TargetHalfExtents.Length));
            profiles = views.TargetArmorProfiles.AsReadOnly();
            targetAups = views.TargetRootAups.AsReadOnly();
            halfExtents = views.TargetHalfExtents.AsReadOnly();
            hits = views.DebugHits.AsReadOnly();
            targetCount = math.min(math.max(0, _targetCount), availableTargets);
            return true;
        }

#if UNITY_EDITOR
        public static bool TryLoadArmorProfilesCsv(string path)
        {
#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            byte[] bytes = File.ReadAllBytes(path);
            return ApplyArmorProfilesCsvBytes(bytes);
#else
            return false;
#endif
        }

        public static unsafe bool ApplyArmorProfilesCsvBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0 ||
                _damageJobScheduled ||
                !TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: true))
            {
                return false;
            }

            IDataVault vault = _armorDataVault;
            if (vault == null ||
                !IsArmorVaultHandleCreated(in _targetArmorProfilesHandle, ArmorPenetrationVaultBufferIds.TargetArmorProfiles) ||
                !vault.TryAcquireWriteLock(in _targetArmorProfilesHandle, ArmorMemoryOwner, out NativeArray<ArmorProfileDTO> profileBuffer))
            {
                return false;
            }

            bool parsedAny = false;
            try
            {
                views.TargetArmorProfiles = profileBuffer;

                int cursor = 0;
                while (TryReadLine(bytes, ref cursor, out ReadOnlySpan<byte> line))
                {
                    line = Trim(line);
                    if (line.Length == 0 || IsCsvHeader(line))
                        continue;

                    ArmorProfileDTO profile = default;
                    int lineCursor = 0;
                    int column = 0;
                    int lutIndex = 0;
                    while (TryReadToken(line, ref lineCursor, out ReadOnlySpan<byte> token))
                    {
                        token = Trim(token);
                        if (token.Length == 0)
                        {
                            column++;
                            continue;
                        }

                        if (column == 0)
                            profile.SpeciesHashID = ParseUIntOrHash(token);
                        else if (column == 1)
                            profile.BaseHealth = ParseFloat(token, 1f);
                        else if (column == 2)
                            profile.BaseArmor = ParseFloat(token, 0f);
                        else if (lutIndex < ArmorGridLutLength)
                        {
                            profile.ArmorGridLUT[lutIndex] = (byte)math.clamp((int)ParseUIntOrHash(token), 0, byte.MaxValue);
                            lutIndex++;
                        }

                        column++;
                    }

                    if (profile.SpeciesHashID == 0u || lutIndex != ArmorGridLutLength)
                        continue;

                    parsedAny |= ApplyCsvProfileToTargets(ref views, in profile);
                }

                return parsedAny;
            }
            finally
            {
                vault.ReleaseWriteLock(in _targetArmorProfilesHandle, ArmorMemoryOwner);
            }
        }

        private static unsafe bool ApplyCsvProfileToTargets(ref ArmorPenetrationVaultViews views, in ArmorProfileDTO profile)
        {
            if (!views.TargetArmorProfiles.IsCreated)
                return false;

            bool applied = false;
            int count = math.min(math.max(0, _targetCount), views.TargetArmorProfiles.Length);
            for (int i = 0; i < count; i++)
            {
                if (views.TargetArmorProfiles[i].SpeciesHashID != profile.SpeciesHashID)
                    continue;

                ArmorProfileDTO merged = profile;
                if (!(merged.BaseHealth > 0f))
                    merged.BaseHealth = _maxHealth.IsCreated && (uint)i < (uint)_maxHealth.Length ? _maxHealth[i] : 1f;
                if (!(merged.BaseArmor >= 0f))
                    merged.BaseArmor = _armorValues.IsCreated && (uint)i < (uint)_armorValues.Length ? _armorValues[i] : 0f;
                views.TargetArmorProfiles[i] = merged;
                applied = true;
            }

            return applied;
        }
#endif

        public static bool GenerateMockArmorImpacts(int maxSignals = 32)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureInitialized();
            if (_damageJobScheduled ||
                _targetCount <= 0 ||
                !TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: true, includeMock: true) ||
                !views.MockRequests.IsCreated)
            {
                return false;
            }

            bool coreLocked = false;
            int lockedMockBuffers = 0;
            try
            {
                RefreshArmorTargetSnapshots(ref views);
                int targetCount = math.max(0, _targetCount);
                if (!CanUseArmorEvaluatorTargetBuffers(in views, targetCount))
                    return false;

                if (!TryLockArmorVaultBuffersForJobs())
                    return false;
                coreLocked = true;
                if (!TryLockArmorMockBuffersForJobs(out lockedMockBuffers))
                {
                    lockedMockBuffers = 0;
                    return false;
                }

                int count = math.min(math.max(1, maxSignals), math.min(targetCount, MaxQueuedSignals));
                if (!CanUseArmorMockSignalBuffers(in views, count))
                    return false;

                GenerateMockArmorImpactSignalsJob job = new GenerateMockArmorImpactSignalsJob
                {
                    Count = count,
                    TargetCount = targetCount,
                    SourceHash = ArmorSourceHash,
                    InstanceIds = _instanceIds,
                    TargetRootAups = views.TargetRootAups,
                    TargetHalfExtents = views.TargetHalfExtents,
                    Requests = views.MockRequests,
                    Details = views.MockDetails,
                    ImpactAups = views.MockAups,
                    TargetSlots = views.MockTargetSlots
                };

                JobHandle handle = job.Schedule(count, 32);
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true); // COLD EDITOR/QA ONLY: deterministic mock data generation, never part of FrameTick.
                for (int i = 0; i < count; i++)
                {
                    CombatDamageRequest request = views.MockRequests[i];
                    CombatDamageSignalDetail detail = views.MockDetails[i];
                    double3 aup = views.MockAups[i];
                    if (!TryQueueDamage(in request, in detail, aup))
                        break;
                }

                return _queuedSignalCount > 0;
            }
            finally
            {
                if (lockedMockBuffers > 0)
                    UnlockArmorMockBuffersForJobs(lockedMockBuffers);
                if (coreLocked)
                    UnlockArmorVaultBuffersForJobs();
            }
#else
            return false;
#endif
        }

        public static bool RunArmorPenetrationTortureProof(int maxImpacts, out ArmorPenetrationTelemetryEntry proof)
        {
            proof = default;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureInitialized();
            if (_damageJobScheduled ||
                _targetCount <= 0 ||
                !_damageArmorLut.IsCreated ||
                !TryResolveArmorPenetrationVaultViews(out ArmorPenetrationVaultViews views, ensure: true, includeEvaluatorTorture: true) ||
                !views.TargetArmorProfiles.IsCreated ||
                !views.TortureRequests.IsCreated ||
                !views.TortureDetails.IsCreated ||
                !views.TortureAups.IsCreated ||
                !views.TortureTargetSlots.IsCreated ||
                !views.TortureResolvedHits.IsCreated)
            {
                return false;
            }

            bool coreLocked = false;
            int lockedTortureBuffers = 0;
            try
            {
                RefreshArmorTargetSnapshots(ref views);
                int targetCount = math.max(0, _targetCount);
                if (!CanUseArmorEvaluatorTargetBuffers(in views, targetCount))
                    return false;

                if (!TryLockArmorVaultBuffersForJobs())
                    return false;
                coreLocked = true;

                int count = math.clamp(maxImpacts <= 0 ? ArmorTortureMaxImpacts : maxImpacts, 1, ArmorTortureMaxImpacts);
                if (views.TortureRequests.Length < count ||
                    views.TortureDetails.Length < count ||
                    views.TortureAups.Length < count ||
                    views.TortureTargetSlots.Length < count ||
                    views.TortureResolvedHits.Length < count)
                {
                    return false;
                }

                if (!TryLockArmorEvaluatorTortureBuffersForJobs(out lockedTortureBuffers))
                {
                    lockedTortureBuffers = 0;
                    return false;
                }

                ArmorPenetrationTuningDTO tuning = PrepareArmorTuningForJob(ref views);
                CombatDamageTortureJob mockJob = new CombatDamageTortureJob
                {
                    Count = count,
                    TargetCount = targetCount,
                    SourceHash = ArmorSourceHash,
                    InstanceIds = _instanceIds,
                    TargetRootAups = views.TargetRootAups,
                    TargetHalfExtents = views.TargetHalfExtents,
                    Requests = views.TortureRequests,
                    Details = views.TortureDetails,
                    ImpactAups = views.TortureAups,
                    TargetSlots = views.TortureTargetSlots
                };
                JobHandle mockHandle = mockJob.Schedule(count, 64);
                DispatcherJobFence.TryComplete(ref mockHandle, forceComplete: true); // COLD EDITOR/QA ONLY: synthetic pellet storm fill, not part of FrameTick.

                long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                EvaluateArmorPenetrationJob job = new EvaluateArmorPenetrationJob
                {
                    Count = count,
                    TargetCount = targetCount,
                    TargetFlags = _targetFlags,
                    TargetHeights = _targetHeights,
                    TargetRootAups = views.TargetRootAups,
                    TargetRotations = views.TargetRotations,
                    TargetHalfExtents = views.TargetHalfExtents,
                    TargetArmorProfiles = views.TargetArmorProfiles,
                    DamageArmorLut = _damageArmorLut,
                    ArmorTuning = tuning,
                    Requests = views.TortureRequests,
                    Details = views.TortureDetails,
                    ImpactAups = views.TortureAups,
                    TargetSlots = views.TortureTargetSlots,
                    ResolvedHits = views.TortureResolvedHits
                };
                JobHandle handle = job.Schedule(count, 64);
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true); // COLD EDITOR/QA ONLY: measured LUT evaluator, never part of FrameTick.
                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
                double elapsedMicroseconds = elapsedTicks > 0L
                    ? elapsedTicks * 1000000.0d / System.Diagnostics.Stopwatch.Frequency
                    : 0.0d;

                uint weakPointHits = 0u;
                uint deflectCount = 0u;
                uint flags = elapsedMicroseconds > ArmorTortureBudgetMicroseconds
                    ? (uint)ArmorTelemetryFlagsOverBudget
                    : 0u;
                float mitigatedSum = 0f;
                uint lastTargetHash = 0u;
                uint lastMaterialHash = 0u;
                for (int i = 0; i < count; i++)
                {
                    ArmorPenetrationResolvedHitDTO hit = views.TortureResolvedHits[i];
                    weakPointHits += math.select(0u, 1u, (hit.Flags & ArmorResolvedFlagWeakPoint) != 0u);
                    deflectCount += math.select(0u, 1u, (hit.Flags & ArmorResolvedFlagDeflected) != 0u);
                    flags |= math.select(0u, (uint)ArmorTelemetryFlagsNanGuard, (hit.Flags & ArmorResolvedFlagNonFinite) != 0u);
                    mitigatedSum += math.max(0f, math.select(0f, hit.ArmorMitigated, math.isfinite(hit.ArmorMitigated)));
                    lastTargetHash = unchecked((uint)hit.TargetId);
                    lastMaterialHash = hit.MaterialHash;
                }

                int telemetryIndex = views.TelemetryRing.IsCreated && views.TelemetryRing.Length > 0
                    ? (int)(_armorTelemetryCursor % (uint)views.TelemetryRing.Length)
                    : 0;
                _armorTelemetryCursor++;
                proof = new ArmorPenetrationTelemetryEntry
                {
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    ImpactCount = unchecked((uint)count),
                    WeakPointHits = weakPointHits,
                    DeflectCount = deflectCount,
                    Flags = flags,
                    AvgMitigatedDamage = count > 0 ? mitigatedSum * math.rcp((float)count) : 0f,
                    SolveMicroseconds = (float)math.min(elapsedMicroseconds, float.MaxValue),
                    GlobalQualityWeight = tuning.GlobalQualityWeight,
                    StateHash = math.hash(new uint4(unchecked((uint)count), weakPointHits, deflectCount, flags)),
                    LastMaterialHash = lastMaterialHash,
                    LastTargetHash = lastTargetHash,
                    Reserved = 0u
                };

                if (views.TelemetryRing.IsCreated && views.TelemetryRing.Length > 0)
                    views.TelemetryRing[math.clamp(telemetryIndex, 0, views.TelemetryRing.Length - 1)] = proof;
                _lastArmorTelemetry = proof;
                if ((proof.Flags & (uint)(ArmorTelemetryFlagsNanGuard | ArmorTelemetryFlagsOverBudget)) != 0u &&
                    views.TelemetryRing.IsCreated)
                {
                    DumpArmorTelemetryIfNeeded(views.TelemetryRing, proof);
                }

                return true;
            }
            finally
            {
                if (lockedTortureBuffers > 0)
                    UnlockArmorEvaluatorTortureBuffersForJobs(lockedTortureBuffers);
                if (coreLocked)
                    UnlockArmorVaultBuffersForJobs();
            }
#else
            return false;
#endif
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct EvaluateArmorPenetrationJob : IJobParallelFor
        {
            public int Count;
            public int TargetCount;
            [ReadOnly, NoAlias] public NativeArray<uint> TargetFlags;
            [ReadOnly, NoAlias] public NativeArray<float> TargetHeights;
            [ReadOnly, NoAlias] public NativeArray<double3> TargetRootAups;
            [ReadOnly, NoAlias] public NativeArray<quaternion> TargetRotations;
            [ReadOnly, NoAlias] public NativeArray<float3> TargetHalfExtents;
            [ReadOnly, NoAlias] public NativeArray<ArmorProfileDTO> TargetArmorProfiles;
            [ReadOnly, NoAlias] public NativeArray<float> DamageArmorLut;
            public ArmorPenetrationTuningDTO ArmorTuning;
            [ReadOnly, NoAlias] public NativeArray<CombatDamageRequest> Requests;
            [ReadOnly, NoAlias] public NativeArray<CombatDamageSignalDetail> Details;
            [ReadOnly, NoAlias] public NativeArray<double3> ImpactAups;
            [ReadOnly, NoAlias] public NativeArray<int> TargetSlots;
            [WriteOnly, NoAlias] public NativeArray<ArmorPenetrationResolvedHitDTO> ResolvedHits;

            public void Execute(int index)
            {
                int slot = math.clamp(TargetSlots[index], 0, math.max(0, TargetCount - 1));
                CombatDamageRequest request = Requests[index];
                CombatDamageSignalDetail detail = Details[index];
                ArmorProfileDTO* profilePtr = ((ArmorProfileDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(TargetArmorProfiles)) + slot;
                ArmorPenetrationSample sample = EvaluateArmorPenetrationCore(
                    in request,
                    in detail,
                    ImpactAups[index],
                    TargetRootAups[slot],
                    TargetRotations[slot],
                    math.max(TargetHalfExtents[slot], new float3(0.125f)),
                    profilePtr,
                    in ArmorTuning);

                ResolvedHits[index] = BuildArmorPenetrationResolvedHit(
                    slot,
                    index,
                    in request,
                    in detail,
                    in sample,
                    TargetFlags[slot],
                    TargetHeights[slot],
                    in DamageArmorLut);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct CombatDamageTortureJob : IJobParallelFor
        {
            public int Count;
            public int TargetCount;
            public uint SourceHash;
            [ReadOnly, NoAlias] public NativeArray<int> InstanceIds;
            [ReadOnly, NoAlias] public NativeArray<double3> TargetRootAups;
            [ReadOnly, NoAlias] public NativeArray<float3> TargetHalfExtents;
            [WriteOnly, NoAlias] public NativeArray<CombatDamageRequest> Requests;
            [WriteOnly, NoAlias] public NativeArray<CombatDamageSignalDetail> Details;
            [WriteOnly, NoAlias] public NativeArray<double3> ImpactAups;
            [WriteOnly, NoAlias] public NativeArray<int> TargetSlots;

            public void Execute(int index)
            {
                int slot = index % TargetCount;
                float3 normalizedLocal = new float3(
                    (((index * 37) % ArmorMockSpatialColumns) + 0.5f) * (2f / ArmorMockSpatialColumns) - 1f,
                    (((index * 19) % ArmorMockSpatialRows) + 0.5f) * (2f / ArmorMockSpatialRows) - 1f,
                    -1f);
                float3 extents = math.max(TargetHalfExtents[slot], new float3(0.125f));
                float3 localMeters = normalizedLocal * extents;
                double3 aup = TargetRootAups[slot] + new double3(localMeters.x, localMeters.y, localMeters.z);
                Requests[index] = new CombatDamageRequest
                {
                    TargetId = InstanceIds[slot],
                    SourceId = unchecked((int)SourceHash),
                    Amount = 12f,
                    ImpulseMagnitude = 12f,
                    Direction = new float3(0f, 0f, 1f),
                    PackedMeta = PackSignalMeta(CombatDamageTypes.Impact, 0u, CombatWeakspotTier.None)
                };
                Details[index] = new CombatDamageSignalDetail
                {
                    LocalPoint = localMeters,
                    ArmorNormal = new float3(0f, 0f, -1f),
                    LocalTemperatureCelsius = 0f,
                    StatusDurationSeconds = 0f
                };
                ImpactAups[index] = aup;
                TargetSlots[index] = slot;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct AtomicHealthCasTortureJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> Health;
            [WriteOnly, NoAlias] public NativeArray<int> Successes;

            public void Execute(int index)
            {
                float previousHealth;
                float nextHealth;
                bool success = TryAtomicSubtractHealth(Health, 0, 1f, out previousHealth, out nextHealth);
                Successes[index] = math.select(0, 1, success);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockArmorImpactSignalsJob : IJobParallelFor
        {
            public int Count;
            public int TargetCount;
            public uint SourceHash;
            [ReadOnly, NoAlias] public NativeArray<int> InstanceIds;
            [ReadOnly, NoAlias] public NativeArray<double3> TargetRootAups;
            [ReadOnly, NoAlias] public NativeArray<float3> TargetHalfExtents;
            [WriteOnly, NoAlias] public NativeArray<CombatDamageRequest> Requests;
            [WriteOnly, NoAlias] public NativeArray<CombatDamageSignalDetail> Details;
            [WriteOnly, NoAlias] public NativeArray<double3> ImpactAups;
            [WriteOnly, NoAlias] public NativeArray<int> TargetSlots;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Count || TargetCount <= 0)
                    return;

                int slot = index % TargetCount;
                float3 normalizedLocal = new float3(
                    (((index * 37) % ArmorMockSpatialColumns) + 0.5f) * (2f / ArmorMockSpatialColumns) - 1f,
                    (((index * 19) % ArmorMockSpatialRows) + 0.5f) * (2f / ArmorMockSpatialRows) - 1f,
                    -1f);
                float3 extents = TargetHalfExtents.IsCreated && (uint)slot < (uint)TargetHalfExtents.Length
                    ? math.max(TargetHalfExtents[slot], new float3(0.125f))
                    : new float3(0.35f, 0.5f, 0.35f);
                float3 localMeters = normalizedLocal * extents;
                double3 root = TargetRootAups.IsCreated && (uint)slot < (uint)TargetRootAups.Length
                    ? TargetRootAups[slot]
                    : double3.zero;
                double3 aup = root + new double3(localMeters.x, localMeters.y, localMeters.z);
                Requests[index] = new CombatDamageRequest
                {
                    TargetId = InstanceIds[slot],
                    SourceId = unchecked((int)SourceHash),
                    Amount = 12f,
                    ImpulseMagnitude = 12f,
                    Direction = new float3(0f, 0f, 1f),
                    PackedMeta = PackSignalMeta(CombatDamageTypes.Impact, 0u, CombatWeakspotTier.None)
                };
                Details[index] = new CombatDamageSignalDetail
                {
                    LocalPoint = localMeters,
                    ArmorNormal = new float3(0f, 0f, -1f),
                    LocalTemperatureCelsius = 0f,
                    StatusDurationSeconds = 0f
                };
                ImpactAups[index] = aup;
                TargetSlots[index] = slot;
            }
        }

        public static bool ValidateArmorLayout(out string failure)
        {
            failure = string.Empty;
            if (UnsafeUtility.SizeOf<ArmorProfileDTO>() != 64)
            {
                failure = "ArmorProfileDTO size mismatch.";
                return false;
            }

            if (UnsafeUtility.SizeOf<ShinobuArmorPenetrationTable>() != 64)
            {
                failure = "ShinobuArmorPenetrationTable size mismatch.";
                return false;
            }

            if ((int)Marshal.OffsetOf(typeof(ShinobuArmorPenetrationTable), nameof(ShinobuArmorPenetrationTable.Cells)) != 0 ||
                (int)Marshal.OffsetOf(typeof(ShinobuArmorPenetrationTable), nameof(ShinobuArmorPenetrationTable.Revision)) != 48 ||
                (int)Marshal.OffsetOf(typeof(ShinobuArmorPenetrationTable), nameof(ShinobuArmorPenetrationTable.AuthoringHash)) != 52 ||
                (int)Marshal.OffsetOf(typeof(ShinobuArmorPenetrationTable), nameof(ShinobuArmorPenetrationTable._pad0)) != 56)
            {
                failure = "ShinobuArmorPenetrationTable offset mismatch.";
                return false;
            }

            if ((int)Marshal.OffsetOf(typeof(ArmorProfileDTO), nameof(ArmorProfileDTO.SpeciesHashID)) != 0 ||
                (int)Marshal.OffsetOf(typeof(ArmorProfileDTO), nameof(ArmorProfileDTO.BaseHealth)) != 4 ||
                (int)Marshal.OffsetOf(typeof(ArmorProfileDTO), nameof(ArmorProfileDTO.BaseArmor)) != 8 ||
                (int)Marshal.OffsetOf(typeof(ArmorProfileDTO), nameof(ArmorProfileDTO._pad0)) != 12 ||
                (int)Marshal.OffsetOf(typeof(ArmorProfileDTO), nameof(ArmorProfileDTO.ArmorGridLUT)) != 16)
            {
                failure = "ArmorProfileDTO offset mismatch.";
                return false;
            }

            if (UnsafeUtility.SizeOf<ArmorPenetrationTelemetryEntry>() != 64)
            {
                failure = "ArmorPenetrationTelemetryEntry size mismatch.";
                return false;
            }

            if (UnsafeUtility.SizeOf<ArmorPenetrationResolvedHitDTO>() != 128)
            {
                failure = "ArmorPenetrationResolvedHitDTO size mismatch.";
                return false;
            }

            return true;
        }

#if UNITY_EDITOR
        private static bool TryReadLine(ReadOnlySpan<byte> bytes, ref int cursor, out ReadOnlySpan<byte> line)
        {
            line = default;
            if (cursor >= bytes.Length)
                return false;

            int start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != '\n')
                cursor++;

            int end = cursor;
            if (cursor < bytes.Length && bytes[cursor] == '\n')
                cursor++;
            if (end > start && bytes[end - 1] == '\r')
                end--;

            line = bytes.Slice(start, end - start);
            return true;
        }

        private static bool TryReadToken(ReadOnlySpan<byte> line, ref int cursor, out ReadOnlySpan<byte> token)
        {
            token = default;
            if (cursor > line.Length)
                return false;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != ',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == ',')
                cursor++;
            else if (cursor == line.Length)
                cursor++;

            token = line.Slice(start, end - start);
            return true;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length;
            while (start < end && value[start] <= 32)
                start++;
            while (end > start && value[end - 1] <= 32)
                end--;
            return value.Slice(start, end - start);
        }

        private static bool IsCsvHeader(ReadOnlySpan<byte> line)
        {
            if (line.Length == 0)
                return false;

            byte c = line[0];
            return (c < (byte)'0' || c > (byte)'9') && c != (byte)'-';
        }

        private static uint ParseUIntOrHash(ReadOnlySpan<byte> token)
        {
            token = Trim(token);
            if (token.Length == 0)
                return 0u;

            int cursor = 0;
            bool hex = token.Length > 2 && token[0] == '0' && (token[1] == 'x' || token[1] == 'X');
            if (hex)
                cursor = 2;

            uint value = 0u;
            bool numeric = cursor < token.Length;
            for (; cursor < token.Length; cursor++)
            {
                byte b = token[cursor];
                uint digit;
                if (b >= '0' && b <= '9')
                    digit = (uint)(b - '0');
                else if (hex && b >= 'a' && b <= 'f')
                    digit = (uint)(10 + b - 'a');
                else if (hex && b >= 'A' && b <= 'F')
                    digit = (uint)(10 + b - 'A');
                else
                {
                    numeric = false;
                    break;
                }

                value = hex ? (value << 4) | digit : (value * 10u) + digit;
            }

            if (numeric)
                return value;

            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                byte c = token[i];
                if (c >= 'A' && c <= 'Z')
                    c = (byte)(c + 32);
                hash = (hash ^ c) * 16777619u;
            }

            return hash != 0u ? hash : 1u;
        }

        private static float ParseFloat(ReadOnlySpan<byte> token, float fallback)
        {
            token = Trim(token);
            if (token.Length == 0)
                return fallback;

            int cursor = 0;
            float sign = 1f;
            if (token[0] == '-')
            {
                sign = -1f;
                cursor = 1;
            }

            float value = 0f;
            bool any = false;
            while (cursor < token.Length && token[cursor] >= '0' && token[cursor] <= '9')
            {
                value = (value * 10f) + (token[cursor] - '0');
                cursor++;
                any = true;
            }

            if (cursor < token.Length && token[cursor] == '.')
            {
                cursor++;
                float scale = 0.1f;
                while (cursor < token.Length && token[cursor] >= '0' && token[cursor] <= '9')
                {
                    value += (token[cursor] - '0') * scale;
                    scale *= 0.1f;
                    cursor++;
                    any = true;
                }
            }

            float parsed = any ? value * sign : fallback;
            return math.select(fallback, parsed, math.isfinite(parsed));
        }
#endif
    }
}
