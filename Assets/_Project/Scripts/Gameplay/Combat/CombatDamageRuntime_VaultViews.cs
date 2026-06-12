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
        private const BufferID CombatDamageSignalsBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageSignalsBufferId;
        private const BufferID CombatDamageSignalDetailsBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageSignalDetailsBufferId;
        private const BufferID CombatDamageTargetLookupKeysBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageTargetLookupKeysBufferId;
        private const BufferID CombatDamageTargetLookupSlotsBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageTargetLookupSlotsBufferId;
        private const BufferID CombatDamageInstanceIdsBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageInstanceIdsBufferId;
        private const BufferID CombatDamageHealthBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageHealthBufferId;
        private const BufferID CombatDamageMaxHealthBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageMaxHealthBufferId;
        private const BufferID CombatDamageInvMaxHealthBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageInvMaxHealthBufferId;
        private const BufferID CombatDamageArmorValuesBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageArmorValuesBufferId;
        private const BufferID CombatDamageShieldValuesBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageShieldValuesBufferId;
        private const BufferID CombatDamageMinorAccumulatorsBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageMinorAccumulatorsBufferId;
        private const BufferID CombatDamageTargetForwardBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageTargetForwardBufferId;
        private const BufferID CombatDamageTargetHeightsBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageTargetHeightsBufferId;
        private const BufferID CombatDamageTargetFlagsBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageTargetFlagsBufferId;
        private const BufferID CombatDamageStatusMasksBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageStatusMasksBufferId;
        private const BufferID CombatDamageStatusDurations0123BufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageStatusDurations0123BufferId;
        private const BufferID CombatDamageLegacyStatusDurations4567BufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageLegacyStatusDurations4567BufferId;
        private const BufferID CombatDamageBrittleDurationsBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageBrittleDurationsBufferId;
        private const BufferID CombatDamageArmorLutBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageArmorLutBufferId;
        private const BufferID CombatDamageResultsBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageResultsBufferId;
        private const BufferID CombatDamageStatusResultsBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageStatusResultsBufferId;
        private const BufferID CombatDamageStatusResultActiveBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageStatusResultActiveBufferId;
        private const BufferID CombatDamageCountersBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageCountersBufferId;
        private const BufferID CombatDamageTelemetryRingBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageTelemetryRingBufferId;
        private const BufferID CombatDamageTelemetryStateBufferId = BufferID.CombatDamageRuntime_VaultViews_CombatDamageTelemetryStateBufferId;
        private static readonly ulong CombatDamageJobMutationGuardMask =
            CombatVaultMutationGuardBit(CombatDamageSignalsBufferId) |
            CombatVaultMutationGuardBit(CombatDamageSignalDetailsBufferId) |
            CombatVaultMutationGuardBit(CombatDamageTargetLookupKeysBufferId) |
            CombatVaultMutationGuardBit(CombatDamageTargetLookupSlotsBufferId) |
            CombatVaultMutationGuardBit(CombatDamageInstanceIdsBufferId) |
            CombatVaultMutationGuardBit(CombatDamageHealthBufferId) |
            CombatVaultMutationGuardBit(CombatDamageMaxHealthBufferId) |
            CombatVaultMutationGuardBit(CombatDamageInvMaxHealthBufferId) |
            CombatVaultMutationGuardBit(CombatDamageArmorValuesBufferId) |
            CombatVaultMutationGuardBit(CombatDamageShieldValuesBufferId) |
            CombatVaultMutationGuardBit(CombatDamageMinorAccumulatorsBufferId) |
            CombatVaultMutationGuardBit(CombatDamageTargetForwardBufferId) |
            CombatVaultMutationGuardBit(CombatDamageTargetHeightsBufferId) |
            CombatVaultMutationGuardBit(CombatDamageTargetFlagsBufferId) |
            CombatVaultMutationGuardBit(CombatDamageStatusMasksBufferId) |
            CombatVaultMutationGuardBit(CombatDamageStatusDurations0123BufferId) |
            CombatVaultMutationGuardBit(CombatDamageLegacyStatusDurations4567BufferId) |
            CombatVaultMutationGuardBit(CombatDamageBrittleDurationsBufferId) |
            CombatVaultMutationGuardBit(CombatDamageArmorLutBufferId) |
            CombatVaultMutationGuardBit(CombatDamageResultsBufferId) |
            CombatVaultMutationGuardBit(CombatDamageStatusResultsBufferId) |
            CombatVaultMutationGuardBit(CombatDamageStatusResultActiveBufferId) |
            CombatVaultMutationGuardBit(CombatDamageCountersBufferId) |
            CombatVaultMutationGuardBit(CombatDamageTelemetryRingBufferId) |
            CombatVaultMutationGuardBit(CombatDamageTelemetryStateBufferId);
        private static readonly ulong CombatDamageCounterMutationGuardMask =
            CombatVaultMutationGuardBit(CombatDamageCountersBufferId);

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

        private struct CombatVaultMutationGuardLease
        {
            private IDataVault _vault;
            private ulong _mask;
            private bool _acquired;

            public bool Add(IDataVault vault, ulong mask)
            {
                if (vault == null || mask == 0UL || vault.IsCompactionFenceActive)
                    return false;

                if (_vault == null)
                {
                    _vault = vault;
                    _mask = mask;
                    return true;
                }

                if (object.ReferenceEquals(_vault, vault))
                {
                    _mask |= mask;
                    return true;
                }

                return false;
            }

            public bool TryAcquire()
            {
                IDataVault vault = _vault;
                if (vault == null || _mask == 0UL)
                    return false;

                if (!vault.TryAcquireMutationGuard(_mask))
                    return false;

                _acquired = true;
                return true;
            }

            public void Release()
            {
                IDataVault vault = _vault;
                ulong mask = _mask;
                bool acquired = _acquired;

                _vault = null;
                _mask = 0UL;
                _acquired = false;

                if (acquired)
                    vault.ReleaseMutationGuard(mask);
            }
        }

        private static ulong CombatVaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private static bool TryAcquireDamageJobMutationGuardLease(out CombatVaultMutationGuardLease lease)
        {
            lease = default;
            if (!lease.Add(OpenCombatDataVault(allowColdBootstrap: false), CombatDamageJobMutationGuardMask) ||
                !lease.Add(_statusEffectVault, StatusEffectJobMutationGuardMask) ||
                !lease.Add(_armorDataVault, ArmorPenetrationJobMutationGuardMask))
            {
                lease.Release();
                return false;
            }

            if (lease.TryAcquire())
                return true;

            lease.Release();
            return false;
        }

        private static bool TryAcquireCombatDispatchMutationGuardLease(out CombatVaultMutationGuardLease lease)
        {
            lease = default;
            if (!lease.Add(OpenCombatDataVault(allowColdBootstrap: false), CombatDamageJobMutationGuardMask))
            {
                lease.Release();
                return false;
            }

            if (lease.TryAcquire())
                return true;

            lease.Release();
            return false;
        }

        private static bool TryAcquireCombatCounterMutationGuardLease(out CombatVaultMutationGuardLease lease)
        {
            lease = default;
            if (!lease.Add(OpenCombatDataVault(allowColdBootstrap: false), CombatDamageCounterMutationGuardMask))
            {
                lease.Release();
                return false;
            }

            if (lease.TryAcquire())
                return true;

            lease.Release();
            return false;
        }

        private static bool TryAcquireStatusEffectJobMutationGuardLease(
            bool includeSimulationBuffers,
            out CombatVaultMutationGuardLease lease)
        {
            lease = default;
            ulong statusMask = includeSimulationBuffers
                ? StatusEffectJobMutationGuardMask
                : StatusEffectRequestJobMutationGuardMask;

            if (!lease.Add(OpenCombatDataVault(allowColdBootstrap: false), CombatDamageJobMutationGuardMask) ||
                !lease.Add(_statusEffectVault, statusMask))
            {
                lease.Release();
                return false;
            }

            if (includeSimulationBuffers && !lease.Add(_armorDataVault, ArmorPenetrationJobMutationGuardMask))
            {
                lease.Release();
                return false;
            }

            if (lease.TryAcquire())
                return true;

            lease.Release();
            return false;
        }

        private static bool IsCombatDamageVaultInitialized()
        {
            return IsCombatDamageVaultHandleCreated(in _damageSignalsHandle, CombatDamageSignalsBufferId) &&
                   IsCombatDamageVaultHandleCreated(in _signalDetailsHandle, CombatDamageSignalDetailsBufferId) &&
                   IsCombatDamageVaultHandleCreated(in _targetLookupKeysHandle, CombatDamageTargetLookupKeysBufferId) &&
                   IsCombatDamageVaultHandleCreated(in _targetLookupSlotsHandle, CombatDamageTargetLookupSlotsBufferId);
        }

        private static bool TryOpenOrEnsureCombatDamageVaultViews(out CombatDamageVaultViews views, bool ensure)
        {
            views = default;
            IDataVault vault = OpenCombatDataVault(ensure);
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return TryOpenOrEnsureCombatVaultBuffer(vault, ref _damageSignalsHandle, CombatDamageSignalsBufferId, MaxQueuedSignals, NativeArrayOptions.ClearMemory, ensure, out views.DamageSignals) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _signalDetailsHandle, CombatDamageSignalDetailsBufferId, MaxQueuedSignals, NativeArrayOptions.ClearMemory, ensure, out views.SignalDetails) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _targetLookupKeysHandle, CombatDamageTargetLookupKeysBufferId, CombatTargetLookupCapacity, NativeArrayOptions.ClearMemory, ensure, out views.TargetLookupKeys) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _targetLookupSlotsHandle, CombatDamageTargetLookupSlotsBufferId, CombatTargetLookupCapacity, NativeArrayOptions.ClearMemory, ensure, out views.TargetLookupSlots) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _instanceIdsHandle, CombatDamageInstanceIdsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.InstanceIds) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _healthHandle, CombatDamageHealthBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.Health) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _maxHealthHandle, CombatDamageMaxHealthBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.MaxHealth) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _invMaxHealthHandle, CombatDamageInvMaxHealthBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.InvMaxHealth) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _armorValuesHandle, CombatDamageArmorValuesBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.ArmorValues) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _shieldValuesHandle, CombatDamageShieldValuesBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.ShieldValues) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _minorDamageAccumulatorsHandle, CombatDamageMinorAccumulatorsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.MinorDamageAccumulators) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _targetForwardVectorsHandle, CombatDamageTargetForwardBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.TargetForwardVectors) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _targetHeightsHandle, CombatDamageTargetHeightsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.TargetHeights) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _targetFlagsHandle, CombatDamageTargetFlagsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.TargetFlags) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _statusMasksHandle, CombatDamageStatusMasksBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.StatusMasks) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _statusDurations0123Handle, CombatDamageStatusDurations0123BufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.StatusDurations0123) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _legacyStatusDurations4567Handle, CombatDamageLegacyStatusDurations4567BufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.LegacyStatusDurations4567) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _brittleDurationsHandle, CombatDamageBrittleDurationsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.BrittleDurations) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _damageArmorLutHandle, CombatDamageArmorLutBufferId, DamageArmorLutLength, NativeArrayOptions.ClearMemory, ensure, out views.DamageArmorLut) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _resultsHandle, CombatDamageResultsBufferId, MaxResults, NativeArrayOptions.ClearMemory, ensure, out views.Results) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _statusResultsHandle, CombatDamageStatusResultsBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.StatusResults) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _statusResultActiveHandle, CombatDamageStatusResultActiveBufferId, MaxTargets, NativeArrayOptions.ClearMemory, ensure, out views.StatusResultActive) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _countersHandle, CombatDamageCountersBufferId, CounterLength, NativeArrayOptions.ClearMemory, ensure, out views.Counters) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _telemetryRingHandle, CombatDamageTelemetryRingBufferId, TelemetryFrameCapacity, NativeArrayOptions.ClearMemory, ensure, out views.TelemetryRing) &&
                   TryOpenOrEnsureCombatVaultBuffer(vault, ref _telemetryStateHandle, CombatDamageTelemetryStateBufferId, TelemetryStateLength, NativeArrayOptions.ClearMemory, ensure, out views.TelemetryState);
        }

        private static bool TryResolveCombatDamageReadOnlyViews(out CombatDamageReadOnlyVaultViews views)
        {
            views = default;
            IDataVault vault = OpenCombatDataVault(allowColdBootstrap: false);
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return TryResolveCombatReadOnlyVaultBuffer(vault, in _targetLookupKeysHandle, CombatDamageTargetLookupKeysBufferId, CombatTargetLookupCapacity, out views.TargetLookupKeys) &&
                   TryResolveCombatReadOnlyVaultBuffer(vault, in _targetLookupSlotsHandle, CombatDamageTargetLookupSlotsBufferId, CombatTargetLookupCapacity, out views.TargetLookupSlots) &&
                   TryResolveCombatReadOnlyVaultBuffer(vault, in _healthHandle, CombatDamageHealthBufferId, MaxTargets, out views.Health) &&
                   TryResolveCombatReadOnlyVaultBuffer(vault, in _invMaxHealthHandle, CombatDamageInvMaxHealthBufferId, MaxTargets, out views.InvMaxHealth) &&
                   TryResolveCombatReadOnlyVaultBuffer(vault, in _instanceIdsHandle, CombatDamageInstanceIdsBufferId, MaxTargets, out views.InstanceIds);
        }

        private static IDataVault OpenCombatDataVault(bool allowColdBootstrap)
        {
            if (_combatDataVault != null)
                return _combatDataVault;

            if (!allowColdBootstrap)
                return null;

            RegisterCombatRegistryHotSwapBridge();
            return _combatDataVault;
        }

        private static bool TryOpenOrEnsureCombatVaultBuffer<T>(
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
            IDataVault vault = OpenCombatDataVault(allowColdBootstrap: false);
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

        private static bool TryResolveCombatTargetOwnerViews(out CombatDamageVaultViews views)
        {
            views = default;
            IDataVault vault = OpenCombatDataVault(allowColdBootstrap: false);
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return vault.TryResolveHandle(in _targetLookupKeysHandle, out views.TargetLookupKeys) &&
                   vault.TryResolveHandle(in _targetLookupSlotsHandle, out views.TargetLookupSlots) &&
                   vault.TryResolveHandle(in _instanceIdsHandle, out views.InstanceIds) &&
                   vault.TryResolveHandle(in _healthHandle, out views.Health) &&
                   vault.TryResolveHandle(in _maxHealthHandle, out views.MaxHealth) &&
                   vault.TryResolveHandle(in _invMaxHealthHandle, out views.InvMaxHealth) &&
                   vault.TryResolveHandle(in _armorValuesHandle, out views.ArmorValues) &&
                   vault.TryResolveHandle(in _shieldValuesHandle, out views.ShieldValues) &&
                   vault.TryResolveHandle(in _minorDamageAccumulatorsHandle, out views.MinorDamageAccumulators) &&
                   vault.TryResolveHandle(in _targetForwardVectorsHandle, out views.TargetForwardVectors) &&
                   vault.TryResolveHandle(in _targetHeightsHandle, out views.TargetHeights) &&
                   vault.TryResolveHandle(in _targetFlagsHandle, out views.TargetFlags) &&
                   vault.TryResolveHandle(in _statusMasksHandle, out views.StatusMasks) &&
                   vault.TryResolveHandle(in _statusDurations0123Handle, out views.StatusDurations0123) &&
                   vault.TryResolveHandle(in _legacyStatusDurations4567Handle, out views.LegacyStatusDurations4567) &&
                   vault.TryResolveHandle(in _brittleDurationsHandle, out views.BrittleDurations) &&
                   vault.TryResolveHandle(in _statusResultsHandle, out views.StatusResults) &&
                   vault.TryResolveHandle(in _statusResultActiveHandle, out views.StatusResultActive) &&
                   views.TargetLookupKeys.IsCreated &&
                   views.TargetLookupSlots.IsCreated &&
                   views.InstanceIds.IsCreated &&
                   views.Health.IsCreated &&
                   views.MaxHealth.IsCreated &&
                   views.InvMaxHealth.IsCreated &&
                   views.ArmorValues.IsCreated &&
                   views.ShieldValues.IsCreated &&
                   views.MinorDamageAccumulators.IsCreated &&
                   views.TargetForwardVectors.IsCreated &&
                   views.TargetHeights.IsCreated &&
                   views.TargetFlags.IsCreated &&
                   views.StatusMasks.IsCreated &&
                   views.StatusDurations0123.IsCreated &&
                   views.LegacyStatusDurations4567.IsCreated &&
                   views.BrittleDurations.IsCreated &&
                   views.StatusResults.IsCreated &&
                   views.StatusResultActive.IsCreated;
        }

        private static bool TryResolveCombatTargetSlotReadOnly(int targetId, out int slot)
        {
            slot = -1;
            IDataVault vault = OpenCombatDataVault(allowColdBootstrap: false);
            if (targetId == 0 ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in _targetLookupKeysHandle, out NativeArray<int>.ReadOnly keys) ||
                !vault.TryReadOnlyHandle(in _targetLookupSlotsHandle, out NativeArray<int>.ReadOnly slots) ||
                !vault.TryReadOnlyHandle(in _instanceIdsHandle, out NativeArray<int>.ReadOnly instanceIds) ||
                !TryFindTargetSlotInLookup(keys, slots, targetId, out slot) ||
                !instanceIds.IsCreated ||
                (uint)slot >= (uint)instanceIds.Length ||
                instanceIds[slot] != targetId)
            {
                return false;
            }

            return IsManagedMirrorSlotReadable(slot);
        }

        private static bool TryResolveCombatTargetHealthOwnerViews(
            out NativeArray<float> health,
            out NativeArray<float> maxHealth,
            out NativeArray<float> invMaxHealth)
        {
            health = default;
            maxHealth = default;
            invMaxHealth = default;
            IDataVault vault = OpenCombatDataVault(allowColdBootstrap: false);
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryResolveHandle(in _healthHandle, out health) &&
                   vault.TryResolveHandle(in _maxHealthHandle, out maxHealth) &&
                   vault.TryResolveHandle(in _invMaxHealthHandle, out invMaxHealth) &&
                   health.IsCreated &&
                   maxHealth.IsCreated &&
                   invMaxHealth.IsCreated;
        }

        private static bool TryResolveCombatTargetProtectionOwnerViews(
            out NativeArray<int> armorValues,
            out NativeArray<float> shieldValues)
        {
            armorValues = default;
            shieldValues = default;
            IDataVault vault = OpenCombatDataVault(allowColdBootstrap: false);
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryResolveHandle(in _armorValuesHandle, out armorValues) &&
                   vault.TryResolveHandle(in _shieldValuesHandle, out shieldValues) &&
                   armorValues.IsCreated &&
                   shieldValues.IsCreated;
        }

        private static bool TryResolveCombatTargetHitProfileOwnerViews(
            out NativeArray<float3> targetForwardVectors,
            out NativeArray<float> targetHeights)
        {
            targetForwardVectors = default;
            targetHeights = default;
            IDataVault vault = OpenCombatDataVault(allowColdBootstrap: false);
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryResolveHandle(in _targetForwardVectorsHandle, out targetForwardVectors) &&
                   vault.TryResolveHandle(in _targetHeightsHandle, out targetHeights) &&
                   targetForwardVectors.IsCreated &&
                   targetHeights.IsCreated;
        }

        private static bool TryClearCombatTargetLookupOwnerView()
        {
            IDataVault vault = OpenCombatDataVault(allowColdBootstrap: false);
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryResolveHandle(in _targetLookupKeysHandle, out NativeArray<int> keys) ||
                !vault.TryResolveHandle(in _targetLookupSlotsHandle, out NativeArray<int> slots))
            {
                return false;
            }

            ClearTargetLookup(keys, slots);
            return true;
        }

        private static bool TryResolveCombatTelemetryOwnerViews(
            out NativeArray<CombatTelemetryEntry> telemetryRing,
            out NativeArray<uint> telemetryState)
        {
            telemetryRing = default;
            telemetryState = default;
            IDataVault vault = OpenCombatDataVault(allowColdBootstrap: false);
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryResolveHandle(in _telemetryRingHandle, out telemetryRing) &&
                   vault.TryResolveHandle(in _telemetryStateHandle, out telemetryState) &&
                   telemetryRing.IsCreated &&
                   telemetryState.IsCreated;
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
