using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    public static partial class CombatDamageRuntime
    {
        private const int CombatTargetLookupCapacity = 4096;
        private const int CombatTargetLookupMask = CombatTargetLookupCapacity - 1;
        private const SystemID CombatDamageMemoryOwner = SystemID.GameplayCombat;
        private const BufferID CombatDamageSignalsBufferId = (BufferID)1417000;
        private const BufferID CombatDamageSignalDetailsBufferId = (BufferID)1417001;
        private const BufferID CombatDamageTargetLookupKeysBufferId = (BufferID)1417002;
        private const BufferID CombatDamageTargetLookupSlotsBufferId = (BufferID)1417003;
        private const BufferID CombatDamageInstanceIdsBufferId = (BufferID)1417004;
        private const BufferID CombatDamageHealthBufferId = (BufferID)1417005;
        private const BufferID CombatDamageMaxHealthBufferId = (BufferID)1417006;
        private const BufferID CombatDamageInvMaxHealthBufferId = (BufferID)1417007;
        private const BufferID CombatDamageArmorValuesBufferId = (BufferID)1417008;
        private const BufferID CombatDamageShieldValuesBufferId = (BufferID)1417009;
        private const BufferID CombatDamageMinorAccumulatorsBufferId = (BufferID)1417010;
        private const BufferID CombatDamageTargetForwardBufferId = (BufferID)1417011;
        private const BufferID CombatDamageTargetHeightsBufferId = (BufferID)1417012;
        private const BufferID CombatDamageTargetFlagsBufferId = (BufferID)1417013;
        private const BufferID CombatDamageStatusMasksBufferId = (BufferID)1417014;
        private const BufferID CombatDamageStatusDurations0123BufferId = (BufferID)1417015;
        private const BufferID CombatDamageLegacyStatusDurations4567BufferId = (BufferID)1417016;
        private const BufferID CombatDamageBrittleDurationsBufferId = (BufferID)1417017;
        private const BufferID CombatDamageArmorLutBufferId = (BufferID)1417018;
        private const BufferID CombatDamageResultsBufferId = (BufferID)1417019;
        private const BufferID CombatDamageStatusResultsBufferId = (BufferID)1417020;
        private const BufferID CombatDamageStatusResultActiveBufferId = (BufferID)1417021;
        private const BufferID CombatDamageCountersBufferId = (BufferID)1417022;
        private const BufferID CombatDamageTelemetryRingBufferId = (BufferID)1417023;
        private const BufferID CombatDamageTelemetryStateBufferId = (BufferID)1417024;
        private const int CombatDamageVaultJobLockCount = 25;

        private static VaultGenerationHandle<CombatDamageRequest> _damageSignalsHandle;
        private static VaultGenerationHandle<CombatDamageSignalDetail> _signalDetailsHandle;
        private static VaultGenerationHandle<int> _targetLookupKeysHandle;
        private static VaultGenerationHandle<int> _targetLookupSlotsHandle;
        private static VaultGenerationHandle<int> _instanceIdsHandle;
        private static VaultGenerationHandle<float> _healthHandle;
        private static VaultGenerationHandle<float> _maxHealthHandle;
        private static VaultGenerationHandle<float> _invMaxHealthHandle;
        private static VaultGenerationHandle<int> _armorValuesHandle;
        private static VaultGenerationHandle<float> _shieldValuesHandle;
        private static VaultGenerationHandle<float> _minorDamageAccumulatorsHandle;
        private static VaultGenerationHandle<float3> _targetForwardVectorsHandle;
        private static VaultGenerationHandle<float> _targetHeightsHandle;
        private static VaultGenerationHandle<uint> _targetFlagsHandle;
        private static VaultGenerationHandle<uint> _statusMasksHandle;
        private static VaultGenerationHandle<float4> _statusDurations0123Handle;
        private static VaultGenerationHandle<float4> _legacyStatusDurations4567Handle;
        private static VaultGenerationHandle<float> _brittleDurationsHandle;
        private static VaultGenerationHandle<float> _damageArmorLutHandle;
        private static VaultGenerationHandle<CombatDamageResult> _resultsHandle;
        private static VaultGenerationHandle<CombatDamageResult> _statusResultsHandle;
        private static VaultGenerationHandle<byte> _statusResultActiveHandle;
        private static VaultGenerationHandle<int> _countersHandle;
        private static VaultGenerationHandle<CombatTelemetryEntry> _telemetryRingHandle;
        private static VaultGenerationHandle<uint> _telemetryStateHandle;

        internal ref struct CombatDamageVaultViews
        {
            public NativeArray<CombatDamageRequest> DamageSignals;
            public NativeArray<CombatDamageSignalDetail> SignalDetails;
            public NativeArray<int> TargetLookupKeys;
            public NativeArray<int> TargetLookupSlots;
            public NativeArray<int> InstanceIds;
            public NativeArray<float> Health;
            public NativeArray<float> MaxHealth;
            public NativeArray<float> InvMaxHealth;
            public NativeArray<int> ArmorValues;
            public NativeArray<float> ShieldValues;
            public NativeArray<float> MinorDamageAccumulators;
            public NativeArray<float3> TargetForwardVectors;
            public NativeArray<float> TargetHeights;
            public NativeArray<uint> TargetFlags;
            public NativeArray<uint> StatusMasks;
            public NativeArray<float4> StatusDurations0123;
            public NativeArray<float4> LegacyStatusDurations4567;
            public NativeArray<float> BrittleDurations;
            public NativeArray<float> DamageArmorLut;
            public NativeArray<CombatDamageResult> Results;
            public NativeArray<CombatDamageResult> StatusResults;
            public NativeArray<byte> StatusResultActive;
            public NativeArray<int> Counters;
            public NativeArray<CombatTelemetryEntry> TelemetryRing;
            public NativeArray<uint> TelemetryState;
        }

        internal ref struct CombatDamageReadOnlyVaultViews
        {
            public NativeArray<int>.ReadOnly TargetLookupKeys;
            public NativeArray<int>.ReadOnly TargetLookupSlots;
            public NativeArray<float>.ReadOnly Health;
            public NativeArray<float>.ReadOnly InvMaxHealth;
            public NativeArray<int>.ReadOnly InstanceIds;
        }

        private static bool IsCombatDamageVaultInitialized()
        {
            return IsCombatDamageVaultHandleCreated(in _damageSignalsHandle, CombatDamageSignalsBufferId) &&
                   IsCombatDamageVaultHandleCreated(in _signalDetailsHandle, CombatDamageSignalDetailsBufferId) &&
                   IsCombatDamageVaultHandleCreated(in _targetLookupKeysHandle, CombatDamageTargetLookupKeysBufferId) &&
                   IsCombatDamageVaultHandleCreated(in _targetLookupSlotsHandle, CombatDamageTargetLookupSlotsBufferId);
        }

        private static bool TryResolveCombatDamageVaultViews(out CombatDamageVaultViews views, bool ensure)
        {
            views = default;
            IDataVault vault = ResolveCombatDataVault(ensure);
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return TryResolveCombatVaultBuffer(vault, ref _damageSignalsHandle, CombatDamageSignalsBufferId, MaxQueuedSignals, NativeArrayOptions.ClearMemory, ensure, out views.DamageSignals) &&
                   TryResolveCombatVaultBuffer(vault, ref _signalDetailsHandle, CombatDamageSignalDetailsBufferId, MaxQueuedSignals, NativeArrayOptions.ClearMemory, ensure, out views.SignalDetails) &&
                   TryResolveCombatVaultBuffer(vault, ref _targetLookupKeysHandle, CombatDamageTargetLookupKeysBufferId, CombatTargetLookupCapacity, NativeArrayOptions.ClearMemory, ensure, out views.TargetLookupKeys) &&
                   TryResolveCombatVaultBuffer(vault, ref _targetLookupSlotsHandle, CombatDamageTargetLookupSlotsBufferId, CombatTargetLookupCapacity, NativeArrayOptions.ClearMemory, ensure, out views.TargetLookupSlots) &&
                   TryResolveCombatVaultBuffer(vault, ref _instanceIdsHandle, CombatDamageInstanceIdsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.InstanceIds) &&
                   TryResolveCombatVaultBuffer(vault, ref _healthHandle, CombatDamageHealthBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.Health) &&
                   TryResolveCombatVaultBuffer(vault, ref _maxHealthHandle, CombatDamageMaxHealthBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.MaxHealth) &&
                   TryResolveCombatVaultBuffer(vault, ref _invMaxHealthHandle, CombatDamageInvMaxHealthBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.InvMaxHealth) &&
                   TryResolveCombatVaultBuffer(vault, ref _armorValuesHandle, CombatDamageArmorValuesBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.ArmorValues) &&
                   TryResolveCombatVaultBuffer(vault, ref _shieldValuesHandle, CombatDamageShieldValuesBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.ShieldValues) &&
                   TryResolveCombatVaultBuffer(vault, ref _minorDamageAccumulatorsHandle, CombatDamageMinorAccumulatorsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.MinorDamageAccumulators) &&
                   TryResolveCombatVaultBuffer(vault, ref _targetForwardVectorsHandle, CombatDamageTargetForwardBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.TargetForwardVectors) &&
                   TryResolveCombatVaultBuffer(vault, ref _targetHeightsHandle, CombatDamageTargetHeightsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.TargetHeights) &&
                   TryResolveCombatVaultBuffer(vault, ref _targetFlagsHandle, CombatDamageTargetFlagsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.TargetFlags) &&
                   TryResolveCombatVaultBuffer(vault, ref _statusMasksHandle, CombatDamageStatusMasksBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.StatusMasks) &&
                   TryResolveCombatVaultBuffer(vault, ref _statusDurations0123Handle, CombatDamageStatusDurations0123BufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.StatusDurations0123) &&
                   TryResolveCombatVaultBuffer(vault, ref _legacyStatusDurations4567Handle, CombatDamageLegacyStatusDurations4567BufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.LegacyStatusDurations4567) &&
                   TryResolveCombatVaultBuffer(vault, ref _brittleDurationsHandle, CombatDamageBrittleDurationsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.BrittleDurations) &&
                   TryResolveCombatVaultBuffer(vault, ref _damageArmorLutHandle, CombatDamageArmorLutBufferId, DamageArmorLutLength, NativeArrayOptions.ClearMemory, ensure, out views.DamageArmorLut) &&
                   TryResolveCombatVaultBuffer(vault, ref _resultsHandle, CombatDamageResultsBufferId, MaxResults, NativeArrayOptions.ClearMemory, ensure, out views.Results) &&
                   TryResolveCombatVaultBuffer(vault, ref _statusResultsHandle, CombatDamageStatusResultsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.StatusResults) &&
                   TryResolveCombatVaultBuffer(vault, ref _statusResultActiveHandle, CombatDamageStatusResultActiveBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.StatusResultActive) &&
                   TryResolveCombatVaultBuffer(vault, ref _countersHandle, CombatDamageCountersBufferId, CounterLength, NativeArrayOptions.ClearMemory, ensure, out views.Counters) &&
                   TryResolveCombatVaultBuffer(vault, ref _telemetryRingHandle, CombatDamageTelemetryRingBufferId, TelemetryFrameCapacity, NativeArrayOptions.ClearMemory, ensure, out views.TelemetryRing) &&
                   TryResolveCombatVaultBuffer(vault, ref _telemetryStateHandle, CombatDamageTelemetryStateBufferId, TelemetryStateLength, NativeArrayOptions.ClearMemory, ensure, out views.TelemetryState);
        }

        private static bool TryResolveCombatDamageReadOnlyViews(out CombatDamageReadOnlyVaultViews views)
        {
            views = default;
            IDataVault vault = ResolveCombatDataVault(allowColdBootstrap: false);
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return TryResolveCombatReadOnlyVaultBuffer(vault, in _targetLookupKeysHandle, CombatDamageTargetLookupKeysBufferId, CombatTargetLookupCapacity, out views.TargetLookupKeys) &&
                   TryResolveCombatReadOnlyVaultBuffer(vault, in _targetLookupSlotsHandle, CombatDamageTargetLookupSlotsBufferId, CombatTargetLookupCapacity, out views.TargetLookupSlots) &&
                   TryResolveCombatReadOnlyVaultBuffer(vault, in _healthHandle, CombatDamageHealthBufferId, MaxTargets, out views.Health) &&
                   TryResolveCombatReadOnlyVaultBuffer(vault, in _invMaxHealthHandle, CombatDamageInvMaxHealthBufferId, MaxTargets, out views.InvMaxHealth) &&
                   TryResolveCombatReadOnlyVaultBuffer(vault, in _instanceIdsHandle, CombatDamageInstanceIdsBufferId, MaxTargets, out views.InstanceIds);
        }

        private static IDataVault ResolveCombatDataVault(bool allowColdBootstrap)
        {
            if (_combatDataVault != null)
                return _combatDataVault;

            if (!allowColdBootstrap)
                return null;

            RegisterCombatRegistryHotSwapBridge();
            return _combatDataVault;
        }

        private static bool TryResolveCombatVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length,
            NativeArrayOptions options,
            bool ensure,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || length <= 0 || vault.IsCompactionFenceActive)
                return false;

            if (IsCombatDamageVaultHandleCreated(in handle, bufferId))
            {
                VaultGenerationHandle<T> readHandle = handle;
                if ((ensure ? vault.TryResolveHandle(in readHandle, out buffer) : vault.TryReadHandle(in readHandle, out buffer)) &&
                    buffer.IsCreated &&
                    (uint)length <= (uint)buffer.Length)
                {
                    return true;
                }
            }

            if (!ensure)
                return false;

            handle = vault.EnsureGenerationHandle<T>(bufferId, length, CombatDamageMemoryOwner, options);
            if (!IsCombatDamageVaultHandleCreated(in handle, bufferId))
                return false;

            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   (uint)length <= (uint)buffer.Length;
        }

        private static bool TryResolveCombatReadOnlyVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                length <= 0 ||
                vault.IsCompactionFenceActive ||
                !IsCombatDamageVaultHandleCreated(in handle, bufferId))
            {
                return false;
            }

            return vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   (uint)length <= (uint)buffer.Length;
        }

        private static bool IsCombatDamageVaultHandleCreated<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)CombatDamageMemoryOwner;
        }

        private static void ReleaseCombatDamageVaultBuffers()
        {
            IDataVault vault = ResolveCombatDataVault(allowColdBootstrap: false);
            ReleaseCombatDamageVaultBuffers(vault);
        }

        private static void ReleaseCombatDamageVaultBuffers(IDataVault vault)
        {
            if (vault == null)
            {
                ResetCombatDamageVaultHandles();
                return;
            }

            ReleaseCombatVaultHandle(vault, ref _damageSignalsHandle);
            ReleaseCombatVaultHandle(vault, ref _signalDetailsHandle);
            ReleaseCombatVaultHandle(vault, ref _targetLookupKeysHandle);
            ReleaseCombatVaultHandle(vault, ref _targetLookupSlotsHandle);
            ReleaseCombatVaultHandle(vault, ref _instanceIdsHandle);
            ReleaseCombatVaultHandle(vault, ref _healthHandle);
            ReleaseCombatVaultHandle(vault, ref _maxHealthHandle);
            ReleaseCombatVaultHandle(vault, ref _invMaxHealthHandle);
            ReleaseCombatVaultHandle(vault, ref _armorValuesHandle);
            ReleaseCombatVaultHandle(vault, ref _shieldValuesHandle);
            ReleaseCombatVaultHandle(vault, ref _minorDamageAccumulatorsHandle);
            ReleaseCombatVaultHandle(vault, ref _targetForwardVectorsHandle);
            ReleaseCombatVaultHandle(vault, ref _targetHeightsHandle);
            ReleaseCombatVaultHandle(vault, ref _targetFlagsHandle);
            ReleaseCombatVaultHandle(vault, ref _statusMasksHandle);
            ReleaseCombatVaultHandle(vault, ref _statusDurations0123Handle);
            ReleaseCombatVaultHandle(vault, ref _legacyStatusDurations4567Handle);
            ReleaseCombatVaultHandle(vault, ref _brittleDurationsHandle);
            ReleaseCombatVaultHandle(vault, ref _damageArmorLutHandle);
            ReleaseCombatVaultHandle(vault, ref _resultsHandle);
            ReleaseCombatVaultHandle(vault, ref _statusResultsHandle);
            ReleaseCombatVaultHandle(vault, ref _statusResultActiveHandle);
            ReleaseCombatVaultHandle(vault, ref _countersHandle);
            ReleaseCombatVaultHandle(vault, ref _telemetryRingHandle);
            ReleaseCombatVaultHandle(vault, ref _telemetryStateHandle);
        }

        private static void ReleaseCombatVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static void ResetCombatDamageVaultHandles()
        {
            _damageSignalsHandle = default;
            _signalDetailsHandle = default;
            _targetLookupKeysHandle = default;
            _targetLookupSlotsHandle = default;
            _instanceIdsHandle = default;
            _healthHandle = default;
            _maxHealthHandle = default;
            _invMaxHealthHandle = default;
            _armorValuesHandle = default;
            _shieldValuesHandle = default;
            _minorDamageAccumulatorsHandle = default;
            _targetForwardVectorsHandle = default;
            _targetHeightsHandle = default;
            _targetFlagsHandle = default;
            _statusMasksHandle = default;
            _statusDurations0123Handle = default;
            _legacyStatusDurations4567Handle = default;
            _brittleDurationsHandle = default;
            _damageArmorLutHandle = default;
            _resultsHandle = default;
            _statusResultsHandle = default;
            _statusResultActiveHandle = default;
            _countersHandle = default;
            _telemetryRingHandle = default;
            _telemetryStateHandle = default;
        }

        private static bool TryLockCombatDamageVaultBuffersForJobs(out int lockedCount)
        {
            lockedCount = 0;
            IDataVault vault = ResolveCombatDataVault(allowColdBootstrap: false);
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryLockBuffer(CombatDamageSignalsBufferId, CombatDamageMemoryOwner)) return false;
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageSignalDetailsBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageTargetLookupKeysBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageTargetLookupSlotsBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageInstanceIdsBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageHealthBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageMaxHealthBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageInvMaxHealthBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageArmorValuesBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageShieldValuesBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageMinorAccumulatorsBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageTargetForwardBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageTargetHeightsBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageTargetFlagsBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageStatusMasksBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageStatusDurations0123BufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageLegacyStatusDurations4567BufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageBrittleDurationsBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageArmorLutBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageResultsBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageStatusResultsBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageStatusResultActiveBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageCountersBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageTelemetryRingBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(CombatDamageTelemetryStateBufferId, CombatDamageMemoryOwner)) { UnlockCombatDamageVaultBuffersForJobs(lockedCount); return false; }
            lockedCount++;
            return true;
        }

        private static void UnlockCombatDamageVaultBuffersForJobs(int lockedCount)
        {
            IDataVault vault = ResolveCombatDataVault(allowColdBootstrap: false);
            if (vault == null)
                return;

            if (lockedCount >= 25) vault.TryUnlockBuffer(CombatDamageTelemetryStateBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 24) vault.TryUnlockBuffer(CombatDamageTelemetryRingBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 23) vault.TryUnlockBuffer(CombatDamageCountersBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 22) vault.TryUnlockBuffer(CombatDamageStatusResultActiveBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 21) vault.TryUnlockBuffer(CombatDamageStatusResultsBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 20) vault.TryUnlockBuffer(CombatDamageResultsBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 19) vault.TryUnlockBuffer(CombatDamageArmorLutBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 18) vault.TryUnlockBuffer(CombatDamageBrittleDurationsBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 17) vault.TryUnlockBuffer(CombatDamageLegacyStatusDurations4567BufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 16) vault.TryUnlockBuffer(CombatDamageStatusDurations0123BufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 15) vault.TryUnlockBuffer(CombatDamageStatusMasksBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 14) vault.TryUnlockBuffer(CombatDamageTargetFlagsBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 13) vault.TryUnlockBuffer(CombatDamageTargetHeightsBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 12) vault.TryUnlockBuffer(CombatDamageTargetForwardBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 11) vault.TryUnlockBuffer(CombatDamageMinorAccumulatorsBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 10) vault.TryUnlockBuffer(CombatDamageShieldValuesBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 9) vault.TryUnlockBuffer(CombatDamageArmorValuesBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 8) vault.TryUnlockBuffer(CombatDamageInvMaxHealthBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 7) vault.TryUnlockBuffer(CombatDamageMaxHealthBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 6) vault.TryUnlockBuffer(CombatDamageHealthBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 5) vault.TryUnlockBuffer(CombatDamageInstanceIdsBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 4) vault.TryUnlockBuffer(CombatDamageTargetLookupSlotsBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 3) vault.TryUnlockBuffer(CombatDamageTargetLookupKeysBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 2) vault.TryUnlockBuffer(CombatDamageSignalDetailsBufferId, CombatDamageMemoryOwner);
            if (lockedCount >= 1) vault.TryUnlockBuffer(CombatDamageSignalsBufferId, CombatDamageMemoryOwner);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputeTargetLookupIndex(int targetId)
        {
            uint hash = math.hash(new uint2(unchecked((uint)targetId), 0x9E3779B9u));
            return (int)(hash & CombatTargetLookupMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryFindTargetSlotInLookup(
            NativeArray<int> keys,
            NativeArray<int> slots,
            int targetId,
            out int slot)
        {
            slot = -1;
            if (targetId == 0 ||
                !keys.IsCreated ||
                !slots.IsCreated ||
                keys.Length < CombatTargetLookupCapacity ||
                slots.Length < CombatTargetLookupCapacity)
            {
                return false;
            }

            int index = ComputeTargetLookupIndex(targetId);
            for (int probe = 0; probe < CombatTargetLookupCapacity; probe++)
            {
                int key = keys[index];
                if (key == targetId)
                {
                    slot = slots[index];
                    return slot >= 0;
                }

                if (key == 0)
                    return false;

                index = (index + 1) & CombatTargetLookupMask;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryFindTargetSlotInLookup(
            NativeArray<int>.ReadOnly keys,
            NativeArray<int>.ReadOnly slots,
            int targetId,
            out int slot)
        {
            slot = -1;
            if (targetId == 0 ||
                !keys.IsCreated ||
                !slots.IsCreated ||
                keys.Length < CombatTargetLookupCapacity ||
                slots.Length < CombatTargetLookupCapacity)
            {
                return false;
            }

            int index = ComputeTargetLookupIndex(targetId);
            for (int probe = 0; probe < CombatTargetLookupCapacity; probe++)
            {
                int key = keys[index];
                if (key == targetId)
                {
                    slot = slots[index];
                    return slot >= 0;
                }

                if (key == 0)
                    return false;

                index = (index + 1) & CombatTargetLookupMask;
            }

            return false;
        }

        private static bool TryAddTargetSlotToLookup(NativeArray<int> keys, NativeArray<int> slots, int targetId, int slot)
        {
            if (targetId == 0 ||
                slot < 0 ||
                !keys.IsCreated ||
                !slots.IsCreated ||
                keys.Length < CombatTargetLookupCapacity ||
                slots.Length < CombatTargetLookupCapacity)
            {
                return false;
            }

            int index = ComputeTargetLookupIndex(targetId);
            for (int probe = 0; probe < CombatTargetLookupCapacity; probe++)
            {
                int key = keys[index];
                if (key == targetId)
                {
                    slots[index] = slot;
                    return true;
                }

                if (key == 0)
                {
                    keys[index] = targetId;
                    slots[index] = slot;
                    return true;
                }

                index = (index + 1) & CombatTargetLookupMask;
            }

            return false;
        }

        private static void ClearTargetLookup(NativeArray<int> keys, NativeArray<int> slots)
        {
            if (!keys.IsCreated || !slots.IsCreated)
                return;

            int count = math.min(keys.Length, slots.Length);
            for (int i = 0; i < count; i++)
            {
                keys[i] = 0;
                slots[i] = -1;
            }
        }

        private static bool RebuildTargetLookup(ref CombatDamageVaultViews views, int targetCount)
        {
            if (!views.TargetLookupKeys.IsCreated ||
                !views.TargetLookupSlots.IsCreated ||
                !views.InstanceIds.IsCreated ||
                targetCount < 0 ||
                (uint)targetCount > (uint)views.InstanceIds.Length)
            {
                return false;
            }

            ClearTargetLookup(views.TargetLookupKeys, views.TargetLookupSlots);
            for (int slot = 0; slot < targetCount; slot++)
            {
                int targetId = views.InstanceIds[slot];
                if (targetId != 0 && !TryAddTargetSlotToLookup(views.TargetLookupKeys, views.TargetLookupSlots, targetId, slot))
                    return false;
            }

            return true;
        }
    }
}
