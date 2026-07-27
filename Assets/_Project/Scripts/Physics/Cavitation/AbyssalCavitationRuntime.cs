using System;
#if UNITY_EDITOR
using System.IO;
#endif
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Physics
{
    public static class AbyssalCavitationRuntime
    {
        private static int s_x001DirectSignalPushDropCount_AbyssalCavitationRuntime;

        private const SystemID OwnerSystem = SystemID.VehiclesPhysics;
        private const uint CavitationFaultEventHash = 0x43414654u; // CAFT
        private const uint CavitationFaultDumpHash = 0x43414450u; // CADP
        private static readonly int _shockwavesShaderId = Shader.PropertyToID("_H8CavitationShockwaves");
        private static readonly int _shockwaveCountShaderId = Shader.PropertyToID("_H8CavitationShockwaveCount");
        private static readonly int _shockwaveParamsShaderId = Shader.PropertyToID("_H8CavitationShockwaveParams");
        private static readonly ulong SimulationMutationGuardMask =
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.ShockwaveEvents) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.ShockwaveCounters) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.EntitySnapshots) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.ForcePackets) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.ForceTransportPackets) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.VisualSpheres) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.TelemetryRing) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.Tuning) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.SdfDescriptor) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.SdfVoxels);
        private static readonly ulong CounterMutationGuardMask =
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.ShockwaveCounters);
        private static readonly ulong ColdInitMutationGuardMask =
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.ShockwaveEvents) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.ShockwaveCounters) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.EntitySnapshots) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.ForcePackets) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.ForceTransportPackets) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.VisualSpheres) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.TelemetryRing) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.OrdnanceProfiles) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.Tuning) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.SdfDescriptor) |
            VaultMutationGuardBit(AbyssalCavitationVaultBufferIds.SdfVoxels);
        private static IDataVault _vault;
        private static bool _initialized;
#if UNITY_EDITOR
        private static bool _layoutValidated;
        private static bool _faultHookRegistered;
        private static int _faultDumpInProgress;
#endif
        private static bool _jobScheduled;
        private static bool _csvLoaded;
        private static bool _defaultCsvLoadAttempted;
        private static bool _coreBlackboxWarmed;
        private static JobHandle _scheduledHandle;
        private static long _scheduleTimestamp;
        private static float _lastSolveMicroseconds;
        private static int _droppedSignalCount;
        private static IDataVault _simulationGuardVault;
        private static bool _simulationGuardHeld;
        private static uint _frameIndex;

        private static VaultGenerationHandle<ShockwaveEventDTO> _shockwaveHandle;
        private static VaultGenerationHandle<ShockwaveCounterBlock> _counterHandle;
        private static VaultGenerationHandle<ShockwaveEntitySnapshotDTO> _entityHandle;
        private static VaultGenerationHandle<ShockwaveForcePacketDTO> _forceHandle;
        private static VaultGenerationHandle<ForcePacketDTO> _forceTransportHandle;
        private static VaultGenerationHandle<CavitationVisualSphereDTO> _visualHandle;
        private static VaultGenerationHandle<ShockwaveTelemetryEntry> _telemetryHandle;
        private static VaultGenerationHandle<OrdnanceProfileDTO> _profileHandle;
        private static VaultGenerationHandle<AbyssalCavitationTuningDTO> _tuningHandle;
        private static VaultGenerationHandle<AbyssalCavitationSdfVolumeDTO> _sdfDescriptorHandle;
        private static VaultGenerationHandle<sbyte> _sdfVoxelsHandle;

        private static GraphicsBuffer _visualBufferA;
        private static GraphicsBuffer _visualBufferB;
        private static GraphicsBuffer _emptyVisualBuffer;
        private static int _visualPage;
        private static int _lastUploadedVisualCount = -1;
        private static float _lastUploadedQuality = -1f;
        private static float _lastUploadedVisualIntensity = -1f;
        private static uint _lastUploadedFrameIndex;
        private static GraphicsBuffer _lastUploadedBuffer;

        public static uint FrameIndex => _frameIndex;
        public static int DroppedSignalCount => _droppedSignalCount;
        public static bool HasScheduledWork => _jobScheduled;
        public static bool IsRuntimeReady => _initialized &&
                                            HasRuntimeDescriptorProof(_vault);

        public static bool TryBorrowRuntimeVault(out IDataVault vault)
        {
            vault = IsRuntimeReady ? _vault : null;
            return vault != null;
        }

        public static bool RebindDataVault(IDataVault currentVault)
        {
            if (ReferenceEquals(_vault, currentVault) && _initialized && HasRuntimeDescriptorProof(_vault))
                return true;

            if (_jobScheduled && !CompleteScheduledForTeardown())
                return false;

            ReleaseSimulationGuard();
            ReleaseVaultHandles(_vault);
            _vault = null;
            _initialized = false;
            _csvLoaded = false;
            _defaultCsvLoadAttempted = false;

            return currentVault != null && EnsureInitialized(currentVault);
        }

        public static bool EnsureInitialized(IDataVault explicitVault = null)
        {
            if (_initialized && _vault != null)
            {
                if (explicitVault == null && HasRuntimeDescriptorProof(_vault))
                {
                    WarmCoreBlackboxRoute();
                    RegisterFaultDumpHookCold();
                    return true;
                }
                if (explicitVault != null && ReferenceEquals(_vault, explicitVault) && HasRuntimeDescriptorProof(explicitVault))
                {
                    WarmCoreBlackboxRoute();
                    RegisterFaultDumpHookCold();
                    return true;
                }
            }

            if (_jobScheduled && !CompleteScheduledForTeardown())
                return false;

            ReleaseSimulationGuard();

            IDataVault vault = explicitVault;
            if (vault == null)
                vault = GlobalRegistry.DataVault;

            if (vault == null)
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            ValidateLayoutColdOnce();
            _vault = vault;
            _shockwaveHandle = vault.EnsureGenerationHandle<ShockwaveEventDTO>(
                AbyssalCavitationVaultBufferIds.ShockwaveEvents,
                AbyssalCavitationConstants.MaxShockwaves,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _counterHandle = vault.EnsureGenerationHandle<ShockwaveCounterBlock>(
                AbyssalCavitationVaultBufferIds.ShockwaveCounters,
                AbyssalCavitationConstants.CounterBlockCount,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _entityHandle = vault.EnsureGenerationHandle<ShockwaveEntitySnapshotDTO>(
                AbyssalCavitationVaultBufferIds.EntitySnapshots,
                AbyssalCavitationConstants.MaxEntitySnapshots,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _forceHandle = vault.EnsureGenerationHandle<ShockwaveForcePacketDTO>(
                AbyssalCavitationVaultBufferIds.ForcePackets,
                AbyssalCavitationConstants.MaxForcePackets,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _forceTransportHandle = vault.EnsureGenerationHandle<ForcePacketDTO>(
                AbyssalCavitationVaultBufferIds.ForceTransportPackets,
                AbyssalCavitationConstants.MaxForcePackets,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _visualHandle = vault.EnsureGenerationHandle<CavitationVisualSphereDTO>(
                AbyssalCavitationVaultBufferIds.VisualSpheres,
                AbyssalCavitationConstants.MaxVisualSpheres,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.EnsureGenerationHandle<ShockwaveTelemetryEntry>(
                AbyssalCavitationVaultBufferIds.TelemetryRing,
                AbyssalCavitationConstants.TelemetryCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _profileHandle = vault.EnsureGenerationHandle<OrdnanceProfileDTO>(
                AbyssalCavitationVaultBufferIds.OrdnanceProfiles,
                AbyssalCavitationConstants.OrdnanceProfileCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.EnsureGenerationHandle<AbyssalCavitationTuningDTO>(
                AbyssalCavitationVaultBufferIds.Tuning,
                1,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _sdfDescriptorHandle = vault.EnsureGenerationHandle<AbyssalCavitationSdfVolumeDTO>(
                AbyssalCavitationVaultBufferIds.SdfDescriptor,
                AbyssalCavitationConstants.SdfDescriptorCount,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _sdfVoxelsHandle = vault.EnsureGenerationHandle<sbyte>(
                AbyssalCavitationVaultBufferIds.SdfVoxels,
                AbyssalCavitationConstants.SdfVoxelCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);

            if (!InitializeBuffersCold(vault))
                return false;
            _droppedSignalCount = 0;
            _csvLoaded = false;
            _defaultCsvLoadAttempted = false;
            _initialized = true;
            WarmCoreBlackboxRoute();
            RegisterFaultDumpHookCold();
            return true;
        }

        private static void RegisterFaultDumpHookCold()
        {
#if UNITY_EDITOR
            if (_faultHookRegistered)
                return;

            Application.logMessageReceived += OnUnityLogFault;
            _faultHookRegistered = true;
#endif
        }

        private static void UnregisterFaultDumpHookCold()
        {
#if UNITY_EDITOR
            if (!_faultHookRegistered)
                return;

            Application.logMessageReceived -= OnUnityLogFault;
            _faultHookRegistered = false;
#endif
        }

#if UNITY_EDITOR
        private static void OnUnityLogFault(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
                return;

            if (System.Threading.Interlocked.Exchange(ref _faultDumpInProgress, 1) != 0)
                return;

            try
            {
                TryDumpBlackBox(AbyssalCavitationTelemetryFlags.NonFiniteRecovered | 0x80000000u);
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _faultDumpInProgress, 0);
            }
        }
#endif

        private static void ValidateLayoutColdOnce()
        {
#if UNITY_EDITOR
            if (_layoutValidated)
                return;

            AbyssalCavitationLayout.ValidateOrThrow();
            _layoutValidated = true;
#endif
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static bool HasRuntimeDescriptorProof(IDataVault vault)
        {
            return vault != null &&
                   CanReadVaultDescriptor(vault, in _shockwaveHandle, AbyssalCavitationVaultBufferIds.ShockwaveEvents, AbyssalCavitationConstants.MaxShockwaves) &&
                   CanReadVaultDescriptor(vault, in _counterHandle, AbyssalCavitationVaultBufferIds.ShockwaveCounters, AbyssalCavitationConstants.CounterBlockCount) &&
                   CanReadVaultDescriptor(vault, in _entityHandle, AbyssalCavitationVaultBufferIds.EntitySnapshots, AbyssalCavitationConstants.MaxEntitySnapshots) &&
                   CanReadVaultDescriptor(vault, in _forceHandle, AbyssalCavitationVaultBufferIds.ForcePackets, AbyssalCavitationConstants.MaxForcePackets) &&
                   CanReadVaultDescriptor(vault, in _forceTransportHandle, AbyssalCavitationVaultBufferIds.ForceTransportPackets, AbyssalCavitationConstants.MaxForcePackets) &&
                   CanReadVaultDescriptor(vault, in _visualHandle, AbyssalCavitationVaultBufferIds.VisualSpheres, AbyssalCavitationConstants.MaxVisualSpheres) &&
                   CanReadVaultDescriptor(vault, in _telemetryHandle, AbyssalCavitationVaultBufferIds.TelemetryRing, AbyssalCavitationConstants.TelemetryCapacity) &&
                   CanReadVaultDescriptor(vault, in _profileHandle, AbyssalCavitationVaultBufferIds.OrdnanceProfiles, AbyssalCavitationConstants.OrdnanceProfileCapacity) &&
                   CanReadVaultDescriptor(vault, in _tuningHandle, AbyssalCavitationVaultBufferIds.Tuning, 1) &&
                   CanReadVaultDescriptor(vault, in _sdfDescriptorHandle, AbyssalCavitationVaultBufferIds.SdfDescriptor, AbyssalCavitationConstants.SdfDescriptorCount) &&
                   CanReadVaultDescriptor(vault, in _sdfVoxelsHandle, AbyssalCavitationVaultBufferIds.SdfVoxels, AbyssalCavitationConstants.SdfVoxelCapacity);
        }

        private static bool CanReadVaultDescriptor<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return vault != null &&
                   requiredLength > 0 &&
                   handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static NativeArray<T> OpenVaultView<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return OpenVaultView(_vault, in handle);
        }

        private static NativeArray<T> OpenVaultView<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault == null ||
                !IsVaultHandleCreated(in handle) ||
                handle.SystemID != (uint)OwnerSystem ||
                !vault.TryResolveHandle(in handle, out NativeArray<T> buffer))
            {
                return default;
            }

            return buffer;
        }

        private static bool TryOpenVaultReadOnlyView<T>(in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            return TryOpenVaultReadOnlyView(_vault, in handle, out buffer);
        }

        private static bool TryOpenVaultReadOnlyView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsVaultHandleCreated(in handle) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryAcquireSimulationGuard(IDataVault vault)
        {
            if (_simulationGuardHeld ||
                !TryAcquireCavitationMutationGuard(vault, SimulationMutationGuardMask))
            {
                return false;
            }

            _simulationGuardVault = vault;
            _simulationGuardHeld = true;
            return true;
        }

        private static void ReleaseSimulationGuard()
        {
            if (_simulationGuardHeld && _simulationGuardVault != null)
                _simulationGuardVault.ReleaseMutationGuard(SimulationMutationGuardMask);

            _simulationGuardHeld = false;
            _simulationGuardVault = null;
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _shockwaveHandle);
                ReleaseVaultHandle(vault, ref _counterHandle);
                ReleaseVaultHandle(vault, ref _entityHandle);
                ReleaseVaultHandle(vault, ref _forceHandle);
                ReleaseVaultHandle(vault, ref _forceTransportHandle);
                ReleaseVaultHandle(vault, ref _visualHandle);
                ReleaseVaultHandle(vault, ref _telemetryHandle);
                ReleaseVaultHandle(vault, ref _profileHandle);
                ReleaseVaultHandle(vault, ref _tuningHandle);
                ReleaseVaultHandle(vault, ref _sdfDescriptorHandle);
                ReleaseVaultHandle(vault, ref _sdfVoxelsHandle);
            }

            _shockwaveHandle = default;
            _counterHandle = default;
            _entityHandle = default;
            _forceHandle = default;
            _forceTransportHandle = default;
            _visualHandle = default;
            _telemetryHandle = default;
            _profileHandle = default;
            _tuningHandle = default;
            _sdfDescriptorHandle = default;
            _sdfVoxelsHandle = default;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool TryAcquireCavitationMutationGuard(IDataVault vault, ulong mask)
        {
            return vault != null &&
                   mask != 0UL &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryAcquireMutationGuard(mask);
        }

        private static ulong VaultMutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 63u));
            return 1UL << bitIndex;
        }

        public static bool TryGetTuning(out AbyssalCavitationTuningDTO tuning)
        {
            tuning = default;
            if (!_initialized || _vault == null || !IsVaultHandleCreated(in _tuningHandle))
                return false;

            NativeArray<AbyssalCavitationTuningDTO> tuningArray = OpenVaultView(in _tuningHandle);
            if (!tuningArray.IsCreated || tuningArray.Length == 0)
                return false;

            tuning = AbyssalCavitationSanitizer.SanitizeTuning(tuningArray[0]);
            return true;
        }

        public static bool TryApplyTuning(in AbyssalCavitationTuningDTO tuning)
        {
            if (!IsRuntimeReady || _jobScheduled)
                return false;

            NativeArray<AbyssalCavitationTuningDTO> tuningArray = OpenVaultView(in _tuningHandle);
            if (!tuningArray.IsCreated || tuningArray.Length == 0)
                return false;

            tuningArray[0] = AbyssalCavitationSanitizer.SanitizeTuning(tuning);
            return true;
        }

        public static bool TryWriteEntitySnapshot(
            int slot,
            double3 aup,
            float3 velocity,
            float effectiveArea,
            float inverseMass,
            int rigidbodySlot,
            uint entityHash,
            uint flags)
        {
            if (!IsRuntimeReady || _jobScheduled)
                return false;

            NativeArray<ShockwaveEntitySnapshotDTO> entities = OpenVaultView(in _entityHandle);
            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(in _counterHandle);
            if (!entities.IsCreated || !counters.IsCreated || (uint)slot >= (uint)entities.Length)
                return false;

            uint safeFlags = flags | AbyssalCavitationEntityFlags.Active | AbyssalCavitationEntityFlags.ForceReceiver;
            if (!math.all(math.isfinite(aup)) || !math.all(math.isfinite(velocity)))
                safeFlags |= AbyssalCavitationEntityFlags.NonFinite;

            entities[slot] = new ShockwaveEntitySnapshotDTO
            {
                AUP = math.all(math.isfinite(aup)) ? aup : double3.zero,
                Velocity = math.all(math.isfinite(velocity)) ? velocity : float3.zero,
                EffectiveArea = math.clamp(math.isfinite(effectiveArea) ? effectiveArea : 1f, 0.05f, 64f),
                InverseMass = math.clamp(math.isfinite(inverseMass) ? inverseMass : 1f, 0f, 1000f),
                RigidbodySlot = rigidbodySlot,
                EntityHash = entityHash != 0u ? entityHash : unchecked((uint)(slot + 1) * 2654435761u),
                Flags = safeFlags
            };

            int current = counters[AbyssalCavitationCounterIndex.CandidateCount].Value;
            if (slot + 1 > current)
            {
                ShockwaveCounterBlock block = counters[AbyssalCavitationCounterIndex.CandidateCount];
                block.Value = math.min(slot + 1, entities.Length);
                counters[AbyssalCavitationCounterIndex.CandidateCount] = block;
            }

            return true;
        }

        public static bool TryClearEntitySnapshots()
        {
            if (!IsRuntimeReady || _jobScheduled)
                return false;

            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(in _counterHandle);
            if (!counters.IsCreated || counters.Length <= AbyssalCavitationCounterIndex.CandidateCount)
                return false;

            ShockwaveCounterBlock block = counters[AbyssalCavitationCounterIndex.CandidateCount];
            block.Value = 0;
            counters[AbyssalCavitationCounterIndex.CandidateCount] = block;
            return true;
        }

        public static bool TryWriteSdfVolume(
            double3 originAup,
            int3 dimensions,
            float3 cellSizeMeters,
            float decodeRangeMeters,
            NativeArray<sbyte> signedDistanceBytes,
            uint version)
        {
            if (!IsRuntimeReady || !signedDistanceBytes.IsCreated || _jobScheduled)
                return false;

            int3 maxDimensions = new int3(
                AbyssalCavitationConstants.SdfVolumeDimX,
                AbyssalCavitationConstants.SdfVolumeDimY,
                AbyssalCavitationConstants.SdfVolumeDimZ);
            if (!math.all(dimensions > 0) ||
                !math.all(dimensions <= maxDimensions) ||
                !math.all(math.isfinite(cellSizeMeters)) ||
                !math.all(cellSizeMeters > 0.0001f) ||
                !math.all(math.isfinite(originAup)) ||
                !math.isfinite(decodeRangeMeters))
            {
                return false;
            }

            int voxelCount = dimensions.x * dimensions.y * dimensions.z;
            NativeArray<AbyssalCavitationSdfVolumeDTO> descriptors = OpenVaultView(in _sdfDescriptorHandle);
            NativeArray<sbyte> voxels = OpenVaultView(in _sdfVoxelsHandle);
            if (!descriptors.IsCreated ||
                descriptors.Length == 0 ||
                !voxels.IsCreated ||
                voxelCount <= 0 ||
                voxelCount > voxels.Length ||
                voxelCount > signedDistanceBytes.Length)
            {
                return false;
            }

            for (int i = 0; i < voxelCount; i++)
                voxels[i] = signedDistanceBytes[i];
            for (int i = voxelCount; i < voxels.Length; i++)
                voxels[i] = sbyte.MaxValue;

            descriptors[0] = new AbyssalCavitationSdfVolumeDTO
            {
                OriginAUP = originAup,
                CellSizeMeters = math.max(cellSizeMeters, new float3(0.0001f)),
                Dimensions = dimensions,
                DecodeRangeMeters = math.clamp(decodeRangeMeters, 0.05f, 512f),
                Version = version,
                Flags = AbyssalCavitationSdfFlags.Active | AbyssalCavitationSdfFlags.SignedDistanceBytes
            };
            return true;
        }

        public static bool TryClearSdfVolume()
        {
            if (!IsRuntimeReady || _jobScheduled)
                return false;

            NativeArray<AbyssalCavitationSdfVolumeDTO> descriptors = OpenVaultView(in _sdfDescriptorHandle);
            if (!descriptors.IsCreated || descriptors.Length == 0)
                return false;

            descriptors[0] = default;
            return true;
        }

        public static bool TryQueueRuntimeDetonation(
            Vector3 runtimePosition,
            float maxRadius,
            float peakPressure,
            float expansionSpeed,
            uint sourceHash)
        {
            if (!TryResolveRuntimeAup(runtimePosition, out double3 aup))
                return false;

            return TryQueueDetonationAup(aup, maxRadius, peakPressure, expansionSpeed, sourceHash);
        }

        public static bool TryQueueDetonationAup(
            double3 epicenterAup,
            float maxRadius,
            float peakPressure,
            float expansionSpeed,
            uint sourceHash)
        {
            if (!IsRuntimeReady || _jobScheduled)
                return false;

            NativeArray<ShockwaveEventDTO> shockwaves = OpenVaultView(in _shockwaveHandle);
            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(in _counterHandle);
            if (!shockwaves.IsCreated || !counters.IsCreated)
                return false;

            int activeCount = math.clamp(counters[AbyssalCavitationCounterIndex.ActiveShockwaves].Value, 0, shockwaves.Length);
            if (activeCount >= shockwaves.Length)
                return false;

            ShockwaveEventDTO wave = default;
            wave.EpicenterAUP = math.all(math.isfinite(epicenterAup)) ? epicenterAup : double3.zero;
            wave.CurrentRadius = 0.05f;
            wave.MaxRadius = math.clamp(math.isfinite(maxRadius) ? maxRadius : 24f, 1f, 2048f);
            wave.PeakPressure = math.clamp(math.isfinite(peakPressure) ? peakPressure : 9000f, 1f, 100000000f);
            wave.ExpansionSpeed = math.clamp(math.isfinite(expansionSpeed) ? expansionSpeed : 220f, 1f, 2000f);
            wave.SourceHashID = sourceHash != 0u ? sourceHash : AbyssalCavitationConstants.SourceHash;
            shockwaves[activeCount] = wave;

            ShockwaveCounterBlock countBlock = counters[AbyssalCavitationCounterIndex.ActiveShockwaves];
            countBlock.Value = activeCount + 1;
            counters[AbyssalCavitationCounterIndex.ActiveShockwaves] = countBlock;

            PublishImpulseSignals(in wave);
            return true;
        }

        public static bool TryQueueOrdnanceDetonationAup(double3 epicenterAup, uint profileHash)
        {
            if (!IsRuntimeReady || _jobScheduled)
                return false;

            NativeArray<OrdnanceProfileDTO> profiles = OpenVaultView(in _profileHandle);
            if (!profiles.IsCreated)
                return false;

            if (!AbyssalCavitationOrdnanceLookup.TryFindProfile(profiles, profileHash, out OrdnanceProfileDTO profile))
                return false;

            return TryQueueDetonationAup(
                epicenterAup,
                profile.MaxRadius,
                profile.PeakPressure,
                profile.ExpansionSpeed,
                profile.SourceHash);
        }

        public static bool GenerateMockDetonations(uint sectorHash = 0x5348494Eu)
        {
#if UNITY_EDITOR
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<ShockwaveEventDTO> shockwaves = OpenVaultView(in _shockwaveHandle);
            NativeArray<ShockwaveEntitySnapshotDTO> entities = OpenVaultView(in _entityHandle);
            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(in _counterHandle);
            if (!shockwaves.IsCreated || !entities.IsCreated || !counters.IsCreated)
                return false;

            double3 originAup = ResolveCurrentRuntimeOriginDouble3();
            var job = new GenerateMockDetonationsJob
            {
                Shockwaves = shockwaves,
                Entities = entities,
                Counters = counters,
                OriginAUP = originAup,
                FrameIndex = ++_frameIndex,
                SectorHash = sectorHash != 0u ? sectorHash : 0x5348494Eu,
                GlobalQualityWeight = ResolveGlobalQualityWeight()
            };
            // COLD DIRECT SEED: CI/editor fallback injection, never the live propagation path.
            for (int i = 0; i < 32; i++)
                job.Execute(i);
            return true;
#else
            return false;
#endif
        }

        public static bool GenerateMockSingularityExplosion(uint sectorHash = AbyssalCavitationConstants.SourceHash)
        {
#if UNITY_EDITOR
            if (!EnsureInitialized() || _jobScheduled)
                return false;

            NativeArray<ShockwaveEventDTO> shockwaves = OpenVaultView(in _shockwaveHandle);
            NativeArray<ShockwaveEntitySnapshotDTO> entities = OpenVaultView(in _entityHandle);
            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(in _counterHandle);
            NativeArray<ShockwaveForcePacketDTO> forcePackets = OpenVaultView(in _forceHandle);
            NativeArray<ForcePacketDTO> transportPackets = OpenVaultView(in _forceTransportHandle);
            if (!shockwaves.IsCreated ||
                !entities.IsCreated ||
                !counters.IsCreated ||
                !forcePackets.IsCreated ||
                !transportPackets.IsCreated)
            {
                return false;
            }

            var job = new GenerateMockSingularityExplosionJob
            {
                Shockwaves = shockwaves,
                Entities = entities,
                Counters = counters,
                ForcePackets = forcePackets,
                TransportPackets = transportPackets,
                OriginAUP = ResolveCurrentRuntimeOriginDouble3(),
                FrameIndex = ++_frameIndex,
                SourceHash = sectorHash != 0u ? sectorHash : AbyssalCavitationConstants.SourceHash
            };
            // COLD DIRECT SEED: deterministic proof input for epsilon clamp.
            job.Execute(0);
            return true;
#else
            return false;
#endif
        }

        public static JobHandle ScheduleSimulation(
            float simulationTickDelta,
            JobHandle inputDependency = default)
        {
            if (!IsRuntimeReady || _jobScheduled)
                return inputDependency;

            IDataVault vault = _vault;
            if (!TryAcquireSimulationGuard(vault))
                return inputDependency;

            bool scheduled = false;
            try
            {
            NativeArray<ShockwaveEventDTO> shockwaves = OpenVaultView(in _shockwaveHandle);
            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(in _counterHandle);
            NativeArray<ShockwaveEntitySnapshotDTO> entities = OpenVaultView(in _entityHandle);
            NativeArray<ShockwaveForcePacketDTO> forcePackets = OpenVaultView(in _forceHandle);
            NativeArray<ForcePacketDTO> forceTransportPackets = OpenVaultView(in _forceTransportHandle);
            NativeArray<CavitationVisualSphereDTO> visuals = OpenVaultView(in _visualHandle);
            NativeArray<ShockwaveTelemetryEntry> telemetry = OpenVaultView(in _telemetryHandle);
            NativeArray<AbyssalCavitationTuningDTO> tuningArray = OpenVaultView(in _tuningHandle);
            NativeArray<AbyssalCavitationSdfVolumeDTO> sdfDescriptors = OpenVaultView(in _sdfDescriptorHandle);
            NativeArray<sbyte> sdfVoxels = OpenVaultView(in _sdfVoxelsHandle);
            if (!shockwaves.IsCreated ||
                !counters.IsCreated ||
                !entities.IsCreated ||
                !forcePackets.IsCreated ||
                !forceTransportPackets.IsCreated ||
                !visuals.IsCreated ||
                !telemetry.IsCreated ||
                !tuningArray.IsCreated ||
                !sdfDescriptors.IsCreated ||
                !sdfVoxels.IsCreated ||
                tuningArray.Length == 0)
            {
                return inputDependency;
            }

            AbyssalCavitationTuningDTO tuning = AbyssalCavitationSanitizer.SanitizeTuning(tuningArray[0]);
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning.SimulationTickDelta = math.clamp(math.isfinite(simulationTickDelta) ? simulationTickDelta : tuning.SimulationTickDelta, 0.0001f, 0.1f);
            tuningArray[0] = tuning;

            MockSDFSampler mockSdf = new MockSDFSampler
            {
                SphereCenter = new float3(18f, tuning.MockSeafloorY + 8f, -12f),
                SphereRadius = 16f,
                SecondarySphereCenter = new float3(-22f, tuning.MockSeafloorY + 11f, 15f),
                SecondarySphereRadius = 12f,
                PlaneY = tuning.MockSeafloorY
            };

            uint frame = ++_frameIndex;
            double3 originAup = ResolveCurrentRuntimeOriginDouble3();
            JobHandle propagateHandle = new PropagateShockwavesJob
            {
                Shockwaves = shockwaves,
                SimulationTickDelta = tuning.SimulationTickDelta
            }.Schedule(shockwaves.Length, 32, inputDependency);

            JobHandle compactHandle = new CompactShockwavesJob
            {
                Shockwaves = shockwaves,
                Counters = counters
            }.Schedule(propagateHandle);

            JobHandle pressureHandle = new EvaluateSanitizedShockwaveJob
            {
                Shockwaves = shockwaves,
                Counters = counters,
                Entities = entities,
                ForcePackets = forcePackets,
                TransportPackets = forceTransportPackets,
                SdfVoxels = sdfVoxels,
                Tuning = tuning,
                SdfVolume = sdfDescriptors.Length > 0 ? sdfDescriptors[0] : default,
                MockSdf = mockSdf,
                SdfReferenceAUP = originAup,
                FrameIndex = frame
            }.Schedule(entities.Length, 32, compactHandle);

            JobHandle visualHandle = new UpdateCavityShaderParamsJob
            {
                Shockwaves = shockwaves,
                Counters = counters,
                Visuals = visuals,
                LocalOriginAUP = originAup,
                Tuning = tuning,
                FrameIndex = frame
            }.Schedule(visuals.Length, 32, compactHandle);

            _scheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _scheduledHandle = new RecordShockwaveTelemetryJob
            {
                Shockwaves = shockwaves,
                Counters = counters,
                ForcePackets = forcePackets,
                Telemetry = telemetry,
                Tuning = tuning,
                FrameIndex = frame,
                CpuMicroseconds = _lastSolveMicroseconds
            }.Schedule(JobHandle.CombineDependencies(pressureHandle, visualHandle));
            _jobScheduled = true;
            scheduled = true;
            H8Memory.RegisterActiveJob(OwnerSystem, _scheduledHandle);
            return _scheduledHandle;
            }
            finally
            {
                if (!scheduled)
                    ReleaseSimulationGuard();
            }
        }

        public static bool TryFinalizeScheduledNoWait()
        {
            if (!_jobScheduled)
                return true;

            if (!_scheduledHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledHandle))
                return false;

            return FinishScheduledCompletion();
        }

        public static bool CompleteScheduledForTeardown()
        {
            if (!_jobScheduled)
                return true;

            DispatcherJobFence.BeginPostFixedSwapWindow();
            try
            {
                if (!DispatcherJobFence.TryComplete(ref _scheduledHandle, forceComplete: true))
                    return false;
            }
            finally
            {
                DispatcherJobFence.EndPostFixedSwapWindow();
            }

            return FinishScheduledCompletion();
        }

        private static bool FinishScheduledCompletion()
        {
            _jobScheduled = false;
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - _scheduleTimestamp;
            _lastSolveMicroseconds = (float)math.max(0.0, elapsed * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);
            ReleaseSimulationGuard();
            PatchLatestTelemetryCpu(_lastSolveMicroseconds);

            if (TrySampleLatestTelemetry(out ShockwaveTelemetryEntry entry) &&
                (entry.Flags & AbyssalCavitationTelemetryFlags.NonFiniteRecovered) != 0u)
            {
                TryDumpBlackBox(entry.Flags);
            }

            return true;
        }

        private static void PatchLatestTelemetryCpu(float microseconds)
        {
            IDataVault vault = _vault;
            if (!TryOpenVaultReadOnlyView(in _counterHandle, out NativeArray<ShockwaveCounterBlock>.ReadOnly counters) ||
                counters.Length <= AbyssalCavitationCounterIndex.TelemetryHead)
                return;

            int head = counters[AbyssalCavitationCounterIndex.TelemetryHead].Value;
            if (vault == null ||
                !vault.TryAcquireWriteLock(in _telemetryHandle, OwnerSystem, out NativeArray<ShockwaveTelemetryEntry> ring))
            {
                return;
            }

            try
            {
                if (!ring.IsCreated || ring.Length == 0)
                    return;

                int index = head - 1;
                if (index < 0)
                    index = ring.Length - 1;

                ShockwaveTelemetryEntry entry = ring[index];
                entry.CpuMicroseconds = math.isfinite(microseconds) ? math.max(0f, microseconds) : 0f;
                ring[index] = entry;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryHandle, OwnerSystem);
            }
        }

        public static int FlushForcesToPhysics(double3 localOriginAUP, uint frameIndex = 0u, int maxPackets = 64)
        {
            if (!IsRuntimeReady)
                return 0;

            if (!TryFinalizeScheduledNoWait())
                return 0;

            NativeArray<ShockwaveForcePacketDTO> packets = OpenVaultView(in _forceHandle);
            NativeArray<ForcePacketDTO> transportPackets = OpenVaultView(in _forceTransportHandle);
            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(in _counterHandle);
            NativeArray<AbyssalCavitationTuningDTO> tuningArray = OpenVaultView(in _tuningHandle);
            if (!packets.IsCreated || !transportPackets.IsCreated || !counters.IsCreated || !tuningArray.IsCreated || tuningArray.Length == 0)
                return 0;

            PhysicsApplySystem.DrainCavitationForcePackets(
                packets,
                transportPackets,
                counters,
                AbyssalCavitationSanitizer.SanitizeTuning(tuningArray[0]),
                localOriginAUP,
                frameIndex,
                maxPackets,
                out int accepted,
                out _);

            return accepted;
        }

        public static int FlushForcesToPhysics(Rigidbody[] bodySlots, double3 localOriginAUP, uint frameIndex = 0u, int maxPackets = 64)
        {
            if (bodySlots == null || bodySlots.Length == 0 || !IsRuntimeReady)
                return 0;

            if (!TryFinalizeScheduledNoWait())
                return 0;

            NativeArray<ShockwaveForcePacketDTO> packets = OpenVaultView(in _forceHandle);
            NativeArray<ForcePacketDTO> transportPackets = OpenVaultView(in _forceTransportHandle);
            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(in _counterHandle);
            NativeArray<AbyssalCavitationTuningDTO> tuningArray = OpenVaultView(in _tuningHandle);
            if (!packets.IsCreated || !transportPackets.IsCreated || !counters.IsCreated || !tuningArray.IsCreated || tuningArray.Length == 0)
                return 0;

            AbyssalCavitationTuningDTO tuning = AbyssalCavitationSanitizer.SanitizeTuning(tuningArray[0]);
            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            if (system == null)
                return 0;

            int candidateCount = math.clamp(counters[AbyssalCavitationCounterIndex.CandidateCount].Value, 0, packets.Length);
            int safeLimit = math.min(candidateCount, math.clamp(maxPackets, 0, packets.Length));
            int accepted = 0;
            for (int i = 0; i < safeLimit; i++)
            {
                ShockwaveForcePacketDTO packet = packets[i];
                ForcePacketDTO transport = i < transportPackets.Length ? transportPackets[i] : default;
                uint packetFlags = packet.Flags | transport.ApplicationFlags;
                if ((packetFlags & AbyssalCavitationPacketFlags.Active) == 0u)
                    continue;
                float3 transportForce = transport.TargetEntityHash != 0u ? transport.ForceVector : packet.Force;
                if (frameIndex != 0u && packet.FrameIndex != frameIndex)
                    continue;
                if ((uint)packet.RigidbodySlot >= (uint)bodySlots.Length)
                    continue;

                Rigidbody body = bodySlots[packet.RigidbodySlot];
                if (body == null)
                    continue;

                float3 force = transportForce;
                float forceSq = math.lengthsq(force);
                if (!math.isfinite(forceSq) || forceSq <= 0.000001f)
                    continue;

                float maxForce = math.max(1f, tuning.MaxForceNewton);
                if (forceSq > maxForce * maxForce)
                    force *= maxForce * math.rsqrt(math.max(forceSq, 0.000001f));

                float3 point = LocalAupToFloat3(packet.ApplicationAUP - localOriginAUP);
                if (system.QueueForceAtPosition(
                        body,
                        new Vector3(force.x, force.y, force.z),
                        new Vector3(point.x, point.y, point.z),
                        ForceMode.Impulse,
                        ForcePacketPriority.Critical,
                        wake: true,
                        extraFlags: ForcePacketFlags.None))
                {
                    accepted++;
                }
            }

            return accepted;
        }

        public static void EnsureGraphicsBuffers()
        {
            if (_emptyVisualBuffer == null)
                _emptyVisualBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<CavitationVisualSphereDTO>(1);
            if (_visualBufferA == null)
                _visualBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<CavitationVisualSphereDTO>(AbyssalCavitationConstants.MaxVisualSpheres);
            if (_visualBufferB == null)
                _visualBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<CavitationVisualSphereDTO>(AbyssalCavitationConstants.MaxVisualSpheres);
        }

        public static void ReleaseGraphicsBuffers()
        {
            UnregisterFaultDumpHookCold();
            _visualBufferA?.Dispose();
            _visualBufferB?.Dispose();
            _emptyVisualBuffer?.Dispose();
            _visualBufferA = null;
            _visualBufferB = null;
            _emptyVisualBuffer = null;
            _lastUploadedVisualCount = -1;
            _lastUploadedQuality = -1f;
            _lastUploadedVisualIntensity = -1f;
            _lastUploadedFrameIndex = 0u;
            _lastUploadedBuffer = null;
            _coreBlackboxWarmed = false;
        }

        public static int SyncShaderVisuals(CommandBuffer commandBuffer = null)
        {
            if (!IsRuntimeReady)
                return 0;

            if (!TryFinalizeScheduledNoWait())
                return math.max(0, _lastUploadedVisualCount);
            EnsureGraphicsBuffers();

            NativeArray<CavitationVisualSphereDTO> visuals = OpenVaultView(in _visualHandle);
            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(in _counterHandle);
            NativeArray<AbyssalCavitationTuningDTO> tuningArray = OpenVaultView(in _tuningHandle);
            if (!visuals.IsCreated || !counters.IsCreated || !tuningArray.IsCreated || tuningArray.Length == 0)
                return 0;

            AbyssalCavitationTuningDTO tuning = AbyssalCavitationSanitizer.SanitizeTuning(tuningArray[0]);
            float q = Smooth01(tuning.GlobalQualityWeight);
            int active = math.clamp(counters[AbyssalCavitationCounterIndex.VisualCount].Value, 0, visuals.Length);
            int qualityLimit = math.clamp((int)math.round(math.lerp(2f, AbyssalCavitationConstants.MaxVisualSpheres, q)), 1, AbyssalCavitationConstants.MaxVisualSpheres);
            int uploadCount = math.min(active, qualityLimit);
            float visualIntensity = math.max(0f, tuning.VisualIntensityScale) * q;
            bool reuseUploadedBuffer = _lastUploadedBuffer != null &&
                                       _lastUploadedFrameIndex == _frameIndex &&
                                       _lastUploadedVisualCount == uploadCount &&
                                       math.abs(_lastUploadedQuality - q) <= 0.00001f &&
                                       math.abs(_lastUploadedVisualIntensity - visualIntensity) <= 0.00001f;
            GraphicsBuffer target = reuseUploadedBuffer
                ? _lastUploadedBuffer
                : uploadCount > 0
                    ? ((_visualPage++ & 1) == 0 ? _visualBufferA : _visualBufferB)
                    : _emptyVisualBuffer;

            if (!reuseUploadedBuffer && uploadCount > 0)
            {
                GraphicsBufferUploadUtility.UploadNativeArray(target, visuals, uploadCount);
            }

            if (!reuseUploadedBuffer)
            {
                _lastUploadedBuffer = target;
                _lastUploadedVisualCount = uploadCount;
                _lastUploadedQuality = q;
                _lastUploadedVisualIntensity = visualIntensity;
                _lastUploadedFrameIndex = _frameIndex;
            }

            if (commandBuffer != null)
            {
                commandBuffer.SetGlobalBuffer(_shockwavesShaderId, target);
                commandBuffer.SetGlobalInt(_shockwaveCountShaderId, uploadCount);
                commandBuffer.SetGlobalVector(_shockwaveParamsShaderId, new Vector4(q, visualIntensity, uploadCount, _frameIndex));
            }
            else
            {
                Shader.SetGlobalBuffer(_shockwavesShaderId, target);
                Shader.SetGlobalInt(_shockwaveCountShaderId, uploadCount);
                Shader.SetGlobalVector(_shockwaveParamsShaderId, new Vector4(q, visualIntensity, uploadCount, _frameIndex));
            }

            return uploadCount;
        }

#if UNITY_EDITOR
        public static bool TryLoadDefaultOrdnanceCsv()
        {
            return TryLoadDefaultOrdnanceCsv(false);
        }

        public static bool TryLoadDefaultOrdnanceCsv(bool forceReload)
        {
            if (_csvLoaded && !forceReload)
                return true;

            if (!EnsureInitialized())
                return false;

            if (_jobScheduled)
                return false;

            if (_defaultCsvLoadAttempted && !forceReload)
                return false;

            _defaultCsvLoadAttempted = true;
            string path = Path.Combine(Application.dataPath, "_Project", "Data", "Combat", "ordnance_blast_profiles.csv");
            if (TryLoadOrdnanceCsv(path))
                return true;

            string legacyPath = Path.Combine(Application.dataPath, "_Project", "Data", "Combat", "ordnance_specs.csv");
            return TryLoadOrdnanceCsv(legacyPath);
        }

        public static bool TryLoadOrdnanceCsv(string csvPath)
        {
            if (!EnsureInitialized() || _jobScheduled || string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                return false;

            IDataVault vault = _vault;
            Span<byte> csvScratch = stackalloc byte[AbyssalCavitationConstants.CsvScratchBytes];
            Span<OrdnanceProfileDTO> profileScratch = stackalloc OrdnanceProfileDTO[AbyssalCavitationConstants.OrdnanceProfileCapacity];

            int bytesRead = 0;
            try
            {
                using FileStream stream = File.OpenRead(csvPath);
                if (stream.Length <= 0L || stream.Length > csvScratch.Length)
                    return false;

                int targetLength = (int)stream.Length;
                while (bytesRead < targetLength)
                {
                    int read = stream.Read(csvScratch.Slice(bytesRead, targetLength - bytesRead));
                    if (read <= 0)
                        break;

                    bytesRead += read;
                }

                if (bytesRead != targetLength)
                    return false;
            }
            catch (Exception)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_248] Ordnance CSV load failed.");
#endif
                return false;
            }

            int parsed = AbyssalCavitationOrdnanceCsv.Parse(csvScratch.Slice(0, bytesRead), profileScratch);
            if (parsed <= 0)
                return false;

            if (!TryCommitOrdnanceProfilesCsv(vault, profileScratch, parsed) ||
                !TryCommitOrdnanceCsvCounter(vault, parsed))
            {
                return false;
            }

            _csvLoaded = true;
            return true;
        }

        private static bool TryCommitOrdnanceProfilesCsv(IDataVault vault, ReadOnlySpan<OrdnanceProfileDTO> profileScratch, int count)
        {
            if (vault == null ||
                !vault.TryAcquireWriteLock(in _profileHandle, OwnerSystem, out NativeArray<OrdnanceProfileDTO> profiles))
            {
                return false;
            }

            try
            {
                if (!profiles.IsCreated || profiles.Length == 0)
                    return false;

                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = default;

                int copyLength = math.min(math.min(profiles.Length, profileScratch.Length), count);
                for (int i = 0; i < copyLength; i++)
                    profiles[i] = profileScratch[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _profileHandle, OwnerSystem);
            }
        }

        private static bool TryCommitOrdnanceCsvCounter(IDataVault vault, int parsed)
        {
            if (vault == null ||
                !vault.TryAcquireWriteLock(in _counterHandle, OwnerSystem, out NativeArray<ShockwaveCounterBlock> counters))
            {
                return false;
            }

            try
            {
                if (!counters.IsCreated || counters.Length <= AbyssalCavitationCounterIndex.CsvProfileCount)
                    return false;

                ShockwaveCounterBlock block = counters[AbyssalCavitationCounterIndex.CsvProfileCount];
                block.Value = parsed;
                counters[AbyssalCavitationCounterIndex.CsvProfileCount] = block;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _counterHandle, OwnerSystem);
            }
        }
#endif

        public static bool TrySampleLatestTelemetry(out ShockwaveTelemetryEntry telemetry)
        {
            telemetry = default;
            if (!_initialized || _vault == null || _jobScheduled || !IsVaultHandleCreated(in _telemetryHandle) || !IsVaultHandleCreated(in _counterHandle))
                return false;

            if (!TryOpenVaultReadOnlyView(in _telemetryHandle, out NativeArray<ShockwaveTelemetryEntry>.ReadOnly ring) ||
                !TryOpenVaultReadOnlyView(in _counterHandle, out NativeArray<ShockwaveCounterBlock>.ReadOnly counters) ||
                ring.Length == 0 ||
                counters.Length <= AbyssalCavitationCounterIndex.TelemetryHead)
            {
                return false;
            }

            int head = counters[AbyssalCavitationCounterIndex.TelemetryHead].Value;
            int index = head - 1;
            if (index < 0)
                index = ring.Length - 1;
            telemetry = ring[index];
            return telemetry.FrameIndex != 0u;
        }

        public static bool TrySampleTelemetryEntry(int ageFromLatest, out ShockwaveTelemetryEntry telemetry)
        {
            telemetry = default;
            if (ageFromLatest < 0 ||
                !_initialized ||
                _vault == null ||
                _jobScheduled ||
                !IsVaultHandleCreated(in _telemetryHandle) ||
                !IsVaultHandleCreated(in _counterHandle))
            {
                return false;
            }

            if (!TryOpenVaultReadOnlyView(in _telemetryHandle, out NativeArray<ShockwaveTelemetryEntry>.ReadOnly ring) ||
                !TryOpenVaultReadOnlyView(in _counterHandle, out NativeArray<ShockwaveCounterBlock>.ReadOnly counters) ||
                ring.Length == 0 ||
                ageFromLatest >= ring.Length ||
                counters.Length <= AbyssalCavitationCounterIndex.TelemetryHead)
            {
                return false;
            }

            int head = counters[AbyssalCavitationCounterIndex.TelemetryHead].Value;
            int index = head - 1 - ageFromLatest;
            while (index < 0)
                index += ring.Length;

            telemetry = ring[index];
            return telemetry.FrameIndex != 0u;
        }

        public static bool TryDumpBlackBox(uint reasonFlags)
        {
            if (!IsRuntimeReady || _jobScheduled || !_coreBlackboxWarmed || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return false;

            if (!TryOpenVaultReadOnlyView(in _telemetryHandle, out NativeArray<ShockwaveTelemetryEntry>.ReadOnly ring) ||
                ring.Length <= 0)
            {
                return false;
            }

            int sampleIndex = (int)(_frameIndex % (uint)ring.Length);
            ShockwaveTelemetryEntry sample = ring[sampleIndex];
            float scalar = math.isfinite(sample.PeakPressure) ? sample.PeakPressure : 0f;
            GlobalTelemetryBus.PushEvent(CavitationFaultEventHash, scalar, reasonFlags);
            return GlobalTelemetryBus.TryDumpBlackboxNow(CavitationFaultDumpHash);
        }

        private static void WarmCoreBlackboxRoute()
        {
            if (_coreBlackboxWarmed || !Application.isPlaying)
                return;

            GlobalTelemetryBus.Initialize();
            _coreBlackboxWarmed = GlobalTelemetryBus.BlackboxActiveFrameCount > 0;
        }

#if UNITY_EDITOR
        public static bool IsCsvLoaded()
        {
            return _csvLoaded;
        }
#endif

        private static bool InitializeBuffersCold(IDataVault vault)
        {
            if (!TryAcquireCavitationMutationGuard(vault, ColdInitMutationGuardMask))
                return false;

            try
            {
            NativeArray<ShockwaveEventDTO> shockwaves = OpenVaultView(vault, in _shockwaveHandle);
            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(vault, in _counterHandle);
            NativeArray<ShockwaveEntitySnapshotDTO> entities = OpenVaultView(vault, in _entityHandle);
            NativeArray<ShockwaveForcePacketDTO> forcePackets = OpenVaultView(vault, in _forceHandle);
            NativeArray<ForcePacketDTO> forceTransportPackets = OpenVaultView(vault, in _forceTransportHandle);
            NativeArray<CavitationVisualSphereDTO> visuals = OpenVaultView(vault, in _visualHandle);
            NativeArray<ShockwaveTelemetryEntry> telemetry = OpenVaultView(vault, in _telemetryHandle);
            NativeArray<OrdnanceProfileDTO> profiles = OpenVaultView(vault, in _profileHandle);
            NativeArray<AbyssalCavitationTuningDTO> tuning = OpenVaultView(vault, in _tuningHandle);
            NativeArray<AbyssalCavitationSdfVolumeDTO> sdfDescriptors = OpenVaultView(vault, in _sdfDescriptorHandle);
            NativeArray<sbyte> sdfVoxels = OpenVaultView(vault, in _sdfVoxelsHandle);

            int count = math.max(
                math.max(
                    math.max(AbyssalCavitationConstants.MaxShockwaves, AbyssalCavitationConstants.MaxEntitySnapshots),
                    AbyssalCavitationConstants.SdfVoxelCapacity),
                AbyssalCavitationConstants.TelemetryCapacity);
            var job = new InitializeAbyssalCavitationBuffersJob
            {
                Shockwaves = shockwaves,
                Counters = counters,
                Entities = entities,
                ForcePackets = forcePackets,
                ForceTransportPackets = forceTransportPackets,
                Visuals = visuals,
                Telemetry = telemetry,
                Profiles = profiles,
                Tuning = tuning,
                SdfDescriptors = sdfDescriptors,
                SdfVoxels = sdfVoxels,
                GlobalQualityWeight = ResolveGlobalQualityWeight()
            };
            // COLD DIRECT INIT: required after UninitializedMemory vault acquisition.
            for (int i = 0; i < count; i++)
                job.Execute(i);

            return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(ColdInitMutationGuardMask);
            }
        }

        private static void PublishImpulseSignals(in ShockwaveEventDTO wave)
        {
            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromAbsolutePosition(wave.EpicenterAUP);
            float intensity = math.saturate(wave.PeakPressure * 0.00008f);

            AcousticDeafeningSignal deafening = AcousticDeafeningSignal.FromShockwave(in wave, intensity);
            AcousticPingSignal ping = default;
            ping.PositionAup = position;
            ping.RadiusMeters = wave.MaxRadius;
            ping.Intensity01 = deafening.Intensity01;
            ping.SourceId = wave.SourceHashID;
            ping.Channel = AcousticPingSignal.ChannelMetalStress;
            ping.Flags = AcousticPingSignal.FlagActiveSonar;
            if (!SignalBus<AcousticPingSignal>.TryPushTracked(in ping, ref s_x001DirectSignalPushDropCount_AbyssalCavitationRuntime))
                RecordDroppedSignal();

            WakeRequestSignal wake = default;
            wake.OriginAup = wave.EpicenterAUP;
            wake.RadiusMeters = wave.MaxRadius;
            wake.SourceHash = wave.SourceHashID;
            wake.Frame = _frameIndex;
            wake.Flags = 1;
            if (!SignalBus<WakeRequestSignal>.TryPushTracked(in wake, ref s_x001DirectSignalPushDropCount_AbyssalCavitationRuntime))
                RecordDroppedSignal();
        }

        private static void RecordDroppedSignal()
        {
            if (_droppedSignalCount < 0x3FFFFFFF)
                _droppedSignalCount++;

            IDataVault vault = _vault;
            if (!TryAcquireCavitationMutationGuard(vault, CounterMutationGuardMask))
                return;

            try
            {
            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(vault, in _counterHandle);
            if (!counters.IsCreated || counters.Length <= AbyssalCavitationCounterIndex.FaultFlags)
                return;

            ShockwaveCounterBlock block = counters[AbyssalCavitationCounterIndex.FaultFlags];
            block.Value |= (int)AbyssalCavitationTelemetryFlags.SignalDrop;
            counters[AbyssalCavitationCounterIndex.FaultFlags] = block;
            }
            finally
            {
                vault.ReleaseMutationGuard(CounterMutationGuardMask);
            }
        }

        private static float ResolveGlobalQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float3 LocalAupToFloat3(double3 delta)
        {
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            double span = AbyssalCavitationConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 positionAup)
        {
            positionAup = default;
            float3 local = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(local)))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(local.x, local.y, local.z));
            if (!resolvedAup.IsFinite())
                return false;

            positionAup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(positionAup));
        }

        private static double3 ResolveCurrentRuntimeOriginDouble3()
        {
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return math.all(math.isfinite(origin)) ? origin : double3.zero;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(math.isfinite(value) ? value : 1f);
            return x * x * (3f - 2f * x);
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Abyssal Cavitation Runtime")]
    public sealed class AbyssalCavitationRuntimeHost : MonoBehaviour, IFixedTickable, ILateFrameTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
#if UNITY_EDITOR
        [SerializeField] private bool autoLoadCsv = true;
#endif
        [SerializeField] private bool uploadShaderVisuals = true;
#if UNITY_EDITOR
        [SerializeField] private bool injectMockOnEnable;
#endif
        [SerializeField] private bool drawDebugGizmos = true;

        private bool _registeredFixed;
        private bool _registeredLate;
        private bool _registeredSlow;
        private bool _registeredHotSwap;

        private void Awake()
        {
            AbyssalCavitationRuntime.EnsureInitialized();
            AbyssalCavitationRuntime.EnsureGraphicsBuffers();
#if UNITY_EDITOR
            if (autoLoadCsv)
                AbyssalCavitationRuntime.TryLoadDefaultOrdnanceCsv();
#endif
        }

        private void OnEnable()
        {
            AbyssalCavitationRuntime.EnsureInitialized();
            AbyssalCavitationRuntime.EnsureGraphicsBuffers();
            RegisterRuntimeLanes();
            TryRegisterHotSwapListener();
#if UNITY_EDITOR
            if (injectMockOnEnable)
                AbyssalCavitationRuntime.GenerateMockDetonations();
#endif
        }

        private void OnDisable()
        {
            AbyssalCavitationRuntime.CompleteScheduledForTeardown();
            TryUnregisterHotSwapListener();
            UnregisterRuntimeLanes();
            AbyssalCavitationRuntime.ReleaseGraphicsBuffers();
        }

        private void RegisterRuntimeLanes()
        {
            if (!_registeredFixed)
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredLate)
                _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (!_registeredSlow)
                _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterRuntimeLanes()
        {
            if (_registeredFixed)
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            if (_registeredLate)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredFixed = false;
            _registeredLate = false;
            _registeredSlow = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                if (AbyssalCavitationRuntime.RebindDataVault(currentService as IDataVault))
                {
                    AbyssalCavitationRuntime.EnsureGraphicsBuffers();
#if UNITY_EDITOR
                    if (autoLoadCsv)
                        AbyssalCavitationRuntime.TryLoadDefaultOrdnanceCsv(forceReload: true);
#endif
                }
                else
                {
                    AbyssalCavitationRuntime.ReleaseGraphicsBuffers();
                }

                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
            {
                return;
            }

            UnregisterRuntimeLanes();
            if (currentService == null || !isActiveAndEnabled)
                return;

            RegisterRuntimeLanes();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            AbyssalCavitationRuntime.ScheduleSimulation(fixedDeltaTime);
        }

        public void LateFrameTick()
        {
            AbyssalCavitationRuntime.TryFinalizeScheduledNoWait();
            if (uploadShaderVisuals)
                AbyssalCavitationRuntime.SyncShaderVisuals();
        }

        public void SlowTick()
        {
            if (!AbyssalCavitationRuntime.IsRuntimeReady)
                return;

#if UNITY_EDITOR
            if (autoLoadCsv && !AbyssalCavitationRuntime.IsCsvLoaded())
                AbyssalCavitationRuntime.TryLoadDefaultOrdnanceCsv();
#endif
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugGizmos || !Application.isPlaying)
                return;

            if (!AbyssalCavitationRuntime.TryBorrowRuntimeVault(out IDataVault vault))
                return;
            if (AbyssalCavitationRuntime.HasScheduledWork)
                return;

            if (!vault.TryGetGenerationHandle(AbyssalCavitationVaultBufferIds.ShockwaveEvents, out VaultGenerationHandle<ShockwaveEventDTO> waveHandle) ||
                !vault.TryGetGenerationHandle(AbyssalCavitationVaultBufferIds.ShockwaveCounters, out VaultGenerationHandle<ShockwaveCounterBlock> counterHandle) ||
                !vault.TryGetGenerationHandle(AbyssalCavitationVaultBufferIds.EntitySnapshots, out VaultGenerationHandle<ShockwaveEntitySnapshotDTO> entityHandle) ||
                !vault.TryGetGenerationHandle(AbyssalCavitationVaultBufferIds.ForceTransportPackets, out VaultGenerationHandle<ForcePacketDTO> forceHandle))
            {
                return;
            }

            NativeArray<ShockwaveEventDTO> waves = OpenVaultView(vault, in waveHandle);
            NativeArray<ShockwaveCounterBlock> counters = OpenVaultView(vault, in counterHandle);
            NativeArray<ShockwaveEntitySnapshotDTO> entities = OpenVaultView(vault, in entityHandle);
            NativeArray<ForcePacketDTO> forces = OpenVaultView(vault, in forceHandle);
            if (!waves.IsCreated || !counters.IsCreated || !entities.IsCreated || !forces.IsCreated)
                return;

            int count = math.clamp(counters[AbyssalCavitationCounterIndex.ActiveShockwaves].Value, 0, waves.Length);
            double3 origin = ResolveCurrentRuntimeOriginDouble3();
            for (int i = 0; i < count; i++)
            {
                ShockwaveEventDTO wave = waves[i];
                if (wave.CurrentRadius <= 0f)
                    continue;

                double3 delta = wave.EpicenterAUP - origin;
                float3 local = AupPrecisionMath.DowncastLocalDelta(delta, float3.zero);
                if (!math.all(math.isfinite(local)))
                    continue;

                Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.16f);
                Gizmos.DrawWireSphere(new Vector3(local.x, local.y, local.z), math.max(0.01f, wave.MaxRadius));
                Gizmos.color = new Color(1f, 0.12f, 0.08f, 0.42f);
                Gizmos.DrawWireSphere(new Vector3(local.x, local.y, local.z), math.max(0.01f, wave.CurrentRadius));
            }

            int forceCount = math.min(
                math.clamp(counters[AbyssalCavitationCounterIndex.CandidateCount].Value, 0, forces.Length),
                entities.Length);
            for (int i = 0; i < forceCount; i++)
            {
                ForcePacketDTO force = forces[i];
                if ((force.ApplicationFlags & AbyssalCavitationPacketFlags.Active) == 0u)
                    continue;

                float forceSq = math.lengthsq(force.ForceVector);
                if (!math.isfinite(forceSq) || forceSq <= 0.000001f)
                    continue;

                double3 delta = entities[i].AUP - origin;
                float3 start = AupPrecisionMath.DowncastLocalDelta(delta, float3.zero);
                if (!math.all(math.isfinite(start)))
                    continue;

                float magnitude = AbyssalCavitationSimdMath.LengthFromSq(forceSq);
                float3 direction = force.ForceVector * math.rsqrt(math.max(forceSq, 0.000001f));
                float arrowLength = math.clamp(magnitude * 0.00035f, 0.2f, 18f);
                float3 end = start + direction * arrowLength;
                float3 sideVector = math.cross(direction, new float3(0f, 1f, 0f));
                float sideSq = math.lengthsq(sideVector);
                float3 side = math.select(new float3(1f, 0f, 0f), sideVector * math.rsqrt(math.max(sideSq, 0.000001f)), math.isfinite(sideSq) & sideSq > 0.000001f);
                float3 back = direction * (arrowLength * 0.18f);
                float3 wing = side * (arrowLength * 0.08f);

                Vector3 startV = new Vector3(start.x, start.y, start.z);
                Vector3 endV = new Vector3(end.x, end.y, end.z);
                Gizmos.color = new Color(1f, 0.02f, 0.02f, 0.86f);
                Gizmos.DrawLine(startV, endV);
                float3 left = end - back + wing;
                float3 right = end - back - wing;
                Gizmos.DrawLine(endV, new Vector3(left.x, left.y, left.z));
                Gizmos.DrawLine(endV, new Vector3(right.x, right.y, right.z));
            }
        }

        private static NativeArray<T> OpenVaultView<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault == null ||
                handle.BufferID == 0u ||
                handle.Generation == 0u ||
                handle.SystemID != (uint)SystemID.VehiclesPhysics ||
                !vault.TryResolveHandle(in handle, out NativeArray<T> buffer))
            {
                return default;
            }

            return buffer;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 positionAup)
        {
            positionAup = default;
            float3 local = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(local)))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(local.x, local.y, local.z));
            if (!resolvedAup.IsFinite())
                return false;

            positionAup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(positionAup));
        }

        private static double3 ResolveCurrentRuntimeOriginDouble3()
        {
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return math.all(math.isfinite(origin)) ? origin : double3.zero;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct InitializeAbyssalCavitationBuffersJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;
        [NoAlias] public NativeArray<ShockwaveEntitySnapshotDTO> Entities;
        [NoAlias] public NativeArray<ShockwaveForcePacketDTO> ForcePackets;
        [NoAlias] public NativeArray<ForcePacketDTO> ForceTransportPackets;
        [NoAlias] public NativeArray<CavitationVisualSphereDTO> Visuals;
        [NoAlias] public NativeArray<ShockwaveTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<OrdnanceProfileDTO> Profiles;
        [NoAlias] public NativeArray<AbyssalCavitationTuningDTO> Tuning;
        [NoAlias] public NativeArray<AbyssalCavitationSdfVolumeDTO> SdfDescriptors;
        [NoAlias] public NativeArray<sbyte> SdfVoxels;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if (Shockwaves.IsCreated && index < Shockwaves.Length)
            {
                ShockwaveEventDTO wave = default;
                wave.CurrentRadius = -1f;
                Shockwaves[index] = wave;
            }

            if (Counters.IsCreated && index < Counters.Length)
                Counters[index] = default;

            if (Entities.IsCreated && index < Entities.Length)
            {
                ShockwaveEntitySnapshotDTO entity = default;
                entity.RigidbodySlot = -1;
                entity.InverseMass = 1f;
                Entities[index] = entity;
            }

            if (ForcePackets.IsCreated && index < ForcePackets.Length)
                ForcePackets[index] = default;
            if (ForceTransportPackets.IsCreated && index < ForceTransportPackets.Length)
                ForceTransportPackets[index] = default;
            if (Visuals.IsCreated && index < Visuals.Length)
                Visuals[index] = default;
            if (Telemetry.IsCreated && index < Telemetry.Length)
                Telemetry[index] = default;
            if (Profiles.IsCreated && index < Profiles.Length)
                Profiles[index] = default;
            if (SdfVoxels.IsCreated && index < SdfVoxels.Length)
                SdfVoxels[index] = sbyte.MaxValue;
            if (Tuning.IsCreated && index == 0)
                Tuning[0] = AbyssalCavitationSanitizer.DefaultTuning(GlobalQualityWeight);
            if (SdfDescriptors.IsCreated && index == 0)
                SdfDescriptors[0] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockDetonationsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [NoAlias] public NativeArray<ShockwaveEntitySnapshotDTO> Entities;
        [NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;
        public double3 OriginAUP;
        public uint FrameIndex;
        public uint SectorHash;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            float q = Smooth01(math.saturate(math.select(
                AbyssalCavitationConstants.AuthoritativeQualityWeight,
                GlobalQualityWeight,
                math.isfinite(GlobalQualityWeight))));
            if (index == 0 && Counters.IsCreated && Counters.Length >= AbyssalCavitationConstants.CounterBlockCount)
            {
                SetCounter(AbyssalCavitationCounterIndex.ActiveShockwaves, math.min(10, Shockwaves.Length));
                SetCounter(AbyssalCavitationCounterIndex.CandidateCount, math.min(32, Entities.Length));
                SetCounter(AbyssalCavitationCounterIndex.FaultFlags, (int)AbyssalCavitationTelemetryFlags.MockFallback);
            }

            if (index < 10 && Shockwaves.IsCreated && index < Shockwaves.Length)
            {
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(SafeSeed(SectorHash ^ FrameIndex ^ ((uint)index * 0x9E3779B9u)));
                float angle = rng.NextFloat(0f, 6.2831855f);
                float s = AbyssalCavitationSimdMath.SinPolynomial7(angle);
                float c = AbyssalCavitationSimdMath.CosPolynomial7(angle);
                float ring = math.lerp(4f, 22f, rng.NextFloat());
                Shockwaves[index] = new ShockwaveEventDTO
                {
                    EpicenterAUP = OriginAUP + new double3(c * ring, rng.NextFloat(-12f, -4f), s * ring),
                    CurrentRadius = 0.05f,
                    MaxRadius = math.lerp(16f, 56f, q) + rng.NextFloat(0f, 9f),
                    PeakPressure = math.lerp(5500f, 26000f, q) * (0.75f + rng.NextFloat() * 0.5f),
                    ExpansionSpeed = math.lerp(120f, 340f, q),
                    SourceHashID = SectorHash ^ ((uint)index * 0x85EBCA6Bu)
                };
            }

            if (index < 32 && Entities.IsCreated && index < Entities.Length)
            {
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(SafeSeed(SectorHash ^ FrameIndex ^ 0xC2B2AE35u ^ ((uint)index * 0x27D4EB2Du)));
                float angle = rng.NextFloat(0f, 6.2831855f);
                float s = AbyssalCavitationSimdMath.SinPolynomial7(angle);
                float c = AbyssalCavitationSimdMath.CosPolynomial7(angle);
                float ring = math.lerp(3f, 72f, rng.NextFloat());
                uint flags = AbyssalCavitationEntityFlags.Active | AbyssalCavitationEntityFlags.ForceReceiver;
                if ((index & 7) == 0)
                    flags |= AbyssalCavitationEntityFlags.Critical;

                Entities[index] = new ShockwaveEntitySnapshotDTO
                {
                    AUP = OriginAUP + new double3(c * ring, rng.NextFloat(-16f, 2f), s * ring),
                    Velocity = float3.zero,
                    EffectiveArea = math.lerp(0.35f, 3.5f, rng.NextFloat()),
                    InverseMass = math.lerp(0.1f, 1.0f, rng.NextFloat()),
                    RigidbodySlot = index,
                    EntityHash = SectorHash ^ (uint)(index + 1) * 2654435761u,
                    Flags = flags
                };
            }
        }

        private void SetCounter(int index, int value)
        {
            ShockwaveCounterBlock block = Counters[index];
            block.Value = value;
            Counters[index] = block;
        }

        private static uint SafeSeed(uint seed)
        {
            return seed != 0u ? seed : 1u;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(math.isfinite(value) ? value : 1f);
            return x * x * (3f - 2f * x);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockSingularityExplosionJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [NoAlias] public NativeArray<ShockwaveEntitySnapshotDTO> Entities;
        [NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;
        [NoAlias] public NativeArray<ShockwaveForcePacketDTO> ForcePackets;
        [NoAlias] public NativeArray<ForcePacketDTO> TransportPackets;
        public double3 OriginAUP;
        public uint FrameIndex;
        public uint SourceHash;

        public void Execute(int index)
        {
            if (index != 0)
                return;

            if (Shockwaves.IsCreated && Shockwaves.Length > 0)
            {
                Shockwaves[0] = new ShockwaveEventDTO
                {
                    EpicenterAUP = OriginAUP,
                    CurrentRadius = 0f,
                    MaxRadius = 12f,
                    PeakPressure = 50000f,
                    ExpansionSpeed = 0f,
                    SourceHashID = SourceHash
                };
            }

            if (Entities.IsCreated && Entities.Length > 0)
            {
                Entities[0] = new ShockwaveEntitySnapshotDTO
                {
                    AUP = OriginAUP,
                    Velocity = float3.zero,
                    EffectiveArea = 1f,
                    InverseMass = 1f,
                    RigidbodySlot = 0,
                    EntityHash = SourceHash ^ 0x9E3779B9u,
                    Flags = AbyssalCavitationEntityFlags.Active |
                            AbyssalCavitationEntityFlags.Critical |
                            AbyssalCavitationEntityFlags.ForceReceiver
                };
            }

            if (ForcePackets.IsCreated && ForcePackets.Length > 0)
                ForcePackets[0] = default;
            if (TransportPackets.IsCreated && TransportPackets.Length > 0)
                TransportPackets[0] = default;

            if (Counters.IsCreated && Counters.Length >= AbyssalCavitationConstants.CounterBlockCount)
            {
                WriteCounter(AbyssalCavitationCounterIndex.ActiveShockwaves, 1);
                WriteCounter(AbyssalCavitationCounterIndex.CandidateCount, 1);
                WriteCounter(AbyssalCavitationCounterIndex.FaultFlags, (int)AbyssalCavitationTelemetryFlags.MockFallback);
                WriteCounter(AbyssalCavitationCounterIndex.LastFrame, unchecked((int)FrameIndex));
            }
        }

        private void WriteCounter(int counterIndex, int value)
        {
            ShockwaveCounterBlock block = Counters[counterIndex];
            block.Value = value;
            Counters[counterIndex] = block;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct PropagateShockwavesJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        public float SimulationTickDelta;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Shockwaves.Length)
                return;

            ShockwaveEventDTO wave = Shockwaves[index];
            if (!IsActive(in wave))
                return;

            float dt = math.clamp(math.isfinite(SimulationTickDelta) ? SimulationTickDelta : 0.02f, 0.0001f, 0.1f);
            wave.CurrentRadius += math.max(0f, wave.ExpansionSpeed) * dt;
            if (!math.isfinite(wave.CurrentRadius) || wave.CurrentRadius > wave.MaxRadius)
            {
                wave = default;
                wave.CurrentRadius = -1f;
            }

            Shockwaves[index] = wave;
        }

        private static bool IsActive(in ShockwaveEventDTO wave)
        {
            return wave.CurrentRadius >= 0f &&
                   wave.CurrentRadius <= wave.MaxRadius &&
                   wave.MaxRadius > 0f &&
                   wave.PeakPressure > 0f &&
                   math.isfinite(wave.CurrentRadius) &&
                   math.isfinite(wave.MaxRadius) &&
                   math.isfinite(wave.PeakPressure) &&
                   math.isfinite(wave.ExpansionSpeed) &&
                   math.all(math.isfinite(wave.EpicenterAUP));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct CompactShockwavesJob : IJob
    {
        [NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;

        public void Execute()
        {
            if (!Shockwaves.IsCreated || !Counters.IsCreated || Counters.Length <= AbyssalCavitationCounterIndex.ActiveShockwaves)
                return;

            int count = math.clamp(Counters[AbyssalCavitationCounterIndex.ActiveShockwaves].Value, 0, Shockwaves.Length);
            int i = 0;
            while (i < count)
            {
                ShockwaveEventDTO wave = Shockwaves[i];
                if (IsActive(in wave))
                {
                    i++;
                    continue;
                }

                int last = count - 1;
                Shockwaves[i] = Shockwaves[last];
                ShockwaveEventDTO inactive = default;
                inactive.CurrentRadius = -1f;
                Shockwaves[last] = inactive;
                count--;
            }

            ShockwaveCounterBlock block = Counters[AbyssalCavitationCounterIndex.ActiveShockwaves];
            block.Value = count;
            Counters[AbyssalCavitationCounterIndex.ActiveShockwaves] = block;
        }

        private static bool IsActive(in ShockwaveEventDTO wave)
        {
            return wave.CurrentRadius >= 0f &&
                   wave.CurrentRadius <= wave.MaxRadius &&
                   wave.MaxRadius > 0f &&
                   wave.PeakPressure > 0f &&
                   math.isfinite(wave.CurrentRadius) &&
                   math.isfinite(wave.MaxRadius) &&
                   math.isfinite(wave.PeakPressure) &&
                   math.isfinite(wave.ExpansionSpeed) &&
                   math.all(math.isfinite(wave.EpicenterAUP));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct EvaluateSanitizedShockwaveJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [ReadOnly, NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;
        [ReadOnly, NoAlias] public NativeArray<ShockwaveEntitySnapshotDTO> Entities;
        [NoAlias] public NativeArray<ShockwaveForcePacketDTO> ForcePackets;
        [NoAlias] public NativeArray<ForcePacketDTO> TransportPackets;
        [ReadOnly, NoAlias] public NativeArray<sbyte> SdfVoxels;
        public AbyssalCavitationTuningDTO Tuning;
        public AbyssalCavitationSdfVolumeDTO SdfVolume;
        public MockSDFSampler MockSdf;
        public double3 SdfReferenceAUP;
        public uint FrameIndex;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ForcePackets.Length)
                return;

            ForcePackets[index] = default;
            if (TransportPackets.IsCreated && (uint)index < (uint)TransportPackets.Length)
                TransportPackets[index] = default;
            int candidateCount = ReadCounter(AbyssalCavitationCounterIndex.CandidateCount, Entities.Length);
            if ((uint)index >= (uint)candidateCount)
                return;

            ShockwaveEntitySnapshotDTO entity = Entities[index];
            if ((entity.Flags & AbyssalCavitationEntityFlags.Active) == 0u ||
                (entity.Flags & AbyssalCavitationEntityFlags.ForceReceiver) == 0u ||
                (entity.Flags & AbyssalCavitationEntityFlags.NonFinite) != 0u)
            {
                return;
            }

            float q = Smooth01(Tuning.GlobalQualityWeight);
            bool critical = (entity.Flags & AbyssalCavitationEntityFlags.Critical) != 0u;
            float acceptance = math.lerp(0.08f, 1.0f, q);
            if (!critical && Hash01(entity.EntityHash ^ FrameIndex * 747796405u) > acceptance)
                return;

            int waveCount = ReadCounter(AbyssalCavitationCounterIndex.ActiveShockwaves, Shockwaves.Length);
            float3 accumulatedForce = float3.zero;
            double3 applicationAup = entity.AUP;
            float peakPressure = 0f;
            float minSdfDamp = 1f;
            uint flags = 0u;
            float epsilon = math.max(Tuning.EpsilonClampValue, 0.000001f);
            float nonCriticalRadiusScale = math.lerp(0.5f, 1.0f, q);
            float inverseSquareMultiplier = math.max(0.0001f, Tuning.InverseSquareMultiplier);

            for (int i = 0; i < waveCount; i++)
            {
                ShockwaveEventDTO wave = Shockwaves[i];
                if (!IsActive(in wave))
                    continue;

                double3 deltaD = entity.AUP - wave.EpicenterAUP;
                float3 delta = LocalDeltaToFloat3(deltaD, ref flags);
                float rawDistanceSq = math.lengthsq(delta);
                bool epsilonClamped = !math.isfinite(rawDistanceSq) || rawDistanceSq <= epsilon;
                float distanceSq = math.max(math.select(0f, rawDistanceSq, math.isfinite(rawDistanceSq)), epsilon);
                if (epsilonClamped)
                    flags |= AbyssalCavitationPacketFlags.EpsilonClamped;
                float distance = AbyssalCavitationSimdMath.LengthFromSq(distanceSq);
                float effectiveMaxRadius = math.select(wave.MaxRadius * nonCriticalRadiusScale, wave.MaxRadius, critical);
                if (distance > math.max(0.01f, effectiveMaxRadius))
                    continue;

                float shellWidth = math.max(
                    Tuning.CavitationShellMeters,
                    math.max(0.05f, wave.ExpansionSpeed * Tuning.SimulationTickDelta) * math.lerp(0.35f, 1.1f, q));
                float shell = math.saturate(1f - math.abs(distance - wave.CurrentRadius) * math.rcp(math.max(shellWidth, 0.0001f)));
                if (shell <= 0f)
                    continue;

                float sdfDamp = ResolveSdfRayDampening(wave.EpicenterAUP, entity.AUP, Tuning.SdfSoftnessMeters, Tuning.SdfOcclusionDampening, q);
                if (sdfDamp < 0.999f)
                    flags |= AbyssalCavitationPacketFlags.SdfDampened;

                minSdfDamp = math.min(minSdfDamp, sdfDamp);
                float inverseSquare = inverseSquareMultiplier * math.rcp(distanceSq);
                float pressure = wave.PeakPressure * inverseSquare * shell * sdfDamp;
                peakPressure = math.max(peakPressure, pressure);
                if (pressure <= Tuning.MinPressure)
                    continue;

                float3 direction = ResolveShockDirection(
                    delta,
                    rawDistanceSq,
                    distanceSq,
                    epsilon,
                    entity.EntityHash,
                    wave.SourceHashID,
                    FrameIndex);
                float area = math.clamp(math.isfinite(entity.EffectiveArea) ? entity.EffectiveArea : 1f, 0.05f, 64f);
                float inverseMass = math.clamp(math.isfinite(entity.InverseMass) ? entity.InverseMass : 1f, 0f, 1000f);
                accumulatedForce += direction * (pressure * area * inverseMass * math.max(0.00001f, Tuning.ForceScale));
            }

            float forceSq = math.lengthsq(accumulatedForce);
            if (!math.isfinite(forceSq))
            {
                accumulatedForce = float3.zero;
                forceSq = 0f;
                flags |= AbyssalCavitationPacketFlags.NonFiniteRecovered;
            }

            if (forceSq <= 0.000001f || peakPressure <= Tuning.MinPressure)
                return;

            float maxForce = math.max(1f, Tuning.MaxForceNewton);
            if (forceSq > maxForce * maxForce)
            {
                accumulatedForce *= maxForce * math.rsqrt(math.max(forceSq, 0.000001f));
                flags |= AbyssalCavitationPacketFlags.ForceSaturated;
            }

            if (critical)
                flags |= AbyssalCavitationPacketFlags.CriticalTarget;

            ForcePackets[index] = new ShockwaveForcePacketDTO
            {
                ApplicationAUP = applicationAup,
                Force = accumulatedForce,
                Pressure = peakPressure,
                RigidbodySlot = entity.RigidbodySlot,
                TargetEntityHash = entity.EntityHash,
                SourceHashID = AbyssalCavitationConstants.SourceHash,
                FrameIndex = FrameIndex,
                Flags = flags | AbyssalCavitationPacketFlags.Active,
                SdfDampening = minSdfDamp
            };

            if (TransportPackets.IsCreated && (uint)index < (uint)TransportPackets.Length)
            {
                TransportPackets[index] = new ForcePacketDTO
                {
                    ForceVector = accumulatedForce,
                    TorqueScalar = 0f,
                    TargetEntityHash = entity.EntityHash,
                    ApplicationFlags = flags | AbyssalCavitationPacketFlags.Active
                };
            }
        }

        private int ReadCounter(int index, int maxValue)
        {
            if (!Counters.IsCreated || (uint)index >= (uint)Counters.Length)
                return 0;

            return math.clamp(Counters[index].Value, 0, maxValue);
        }

        private float ResolveSdfRayDampening(double3 epicenterAup, double3 targetAup, float softnessMeters, float hardDampening, float quality)
        {
            double3 ray = targetAup - epicenterAup;
            double3 p25 = epicenterAup + ray * 0.25;
            double3 p50 = epicenterAup + ray * 0.5;
            double3 p75 = epicenterAup + ray * 0.75;

            float midDamp = ResolveSdfDampening(SampleSdfDistance(p50, quality), softnessMeters, hardDampening);
            float multiTapWeight = SmoothRange(0.35f, 0.85f, quality);
            if (multiTapWeight <= 0f)
                return midDamp;

            float rayDamp = math.min(
                ResolveSdfDampening(SampleSdfDistance(p25, quality), softnessMeters, hardDampening),
                ResolveSdfDampening(SampleSdfDistance(p75, quality), softnessMeters, hardDampening));
            rayDamp = math.min(rayDamp, midDamp);

            return math.lerp(midDamp, rayDamp, multiTapWeight);
        }

        private float SampleSdfDistance(double3 midpointAup, float quality)
        {
            if ((SdfVolume.Flags & AbyssalCavitationSdfFlags.Active) != 0u &&
                SdfVoxels.IsCreated)
                return SampleSdfVolume(midpointAup, quality);

            float3 local = LocalDeltaToFloat3NoFlags(midpointAup - SdfReferenceAUP);
            return MockSdf.SampleDistance(local);
        }

        private float SampleSdfVolume(double3 midpointAup, float quality)
        {
            int3 dimensions = SdfVolume.Dimensions;
            if (!math.all(dimensions > 0))
                return 1f;

            int voxelCount = dimensions.x * dimensions.y * dimensions.z;
            if (voxelCount <= 0 || voxelCount > SdfVoxels.Length)
                return 1f;

            float3 cellSize = math.max(SdfVolume.CellSizeMeters, new float3(0.0001f));
            float3 local = LocalDeltaToFloat3NoFlags(midpointAup - SdfVolume.OriginAUP);
            float3 grid = local * math.rcp(math.max(cellSize, new float3(0.0001f)));
            float3 maxGrid = new float3(dimensions - 1);
            if (math.any(grid < 0f) || math.any(grid > maxGrid))
                return 1f;

            int3 nearestCoord = (int3)math.floor(grid + 0.5f);
            nearestCoord = math.clamp(nearestCoord, int3.zero, dimensions - 1);
            float nearest = DecodeSdfByte(SdfVoxels[FlatIndex(nearestCoord, dimensions)]);
            float interpolationWeight = Smooth01(math.saturate((Smooth01(quality) - 0.18f) * math.rcp(0.52f)));
            if (interpolationWeight <= 0f)
                return nearest;

            int3 baseCoord = (int3)math.floor(grid);
            baseCoord = math.clamp(baseCoord, int3.zero, dimensions - 1);
            int3 nextCoord = math.min(baseCoord + 1, dimensions - 1);
            float3 t = math.saturate(grid - (float3)baseCoord);

            float c000 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(baseCoord.x, baseCoord.y, baseCoord.z), dimensions)]);
            float c100 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(nextCoord.x, baseCoord.y, baseCoord.z), dimensions)]);
            float c010 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(baseCoord.x, nextCoord.y, baseCoord.z), dimensions)]);
            float c110 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(nextCoord.x, nextCoord.y, baseCoord.z), dimensions)]);
            float c001 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(baseCoord.x, baseCoord.y, nextCoord.z), dimensions)]);
            float c101 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(nextCoord.x, baseCoord.y, nextCoord.z), dimensions)]);
            float c011 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(baseCoord.x, nextCoord.y, nextCoord.z), dimensions)]);
            float c111 = DecodeSdfByte(SdfVoxels[FlatIndex(new int3(nextCoord.x, nextCoord.y, nextCoord.z), dimensions)]);

            float c00 = math.lerp(c000, c100, t.x);
            float c10 = math.lerp(c010, c110, t.x);
            float c01 = math.lerp(c001, c101, t.x);
            float c11 = math.lerp(c011, c111, t.x);
            float c0 = math.lerp(c00, c10, t.y);
            float c1 = math.lerp(c01, c11, t.y);
            float trilinear = math.lerp(c0, c1, t.z);
            return math.lerp(nearest, trilinear, interpolationWeight);
        }

        private float DecodeSdfByte(sbyte encoded)
        {
            float range = math.max(0.05f, math.isfinite(SdfVolume.DecodeRangeMeters) ? SdfVolume.DecodeRangeMeters : 32f);
            return math.clamp(encoded * (1f / 127f), -1f, 1f) * range;
        }

        private static int FlatIndex(int3 coord, int3 dimensions)
        {
            return coord.x + coord.y * dimensions.x + coord.z * dimensions.x * dimensions.y;
        }

        private static float ResolveSdfDampening(float sdfDistance, float softnessMeters, float hardDampening)
        {
            float softness = math.max(0.05f, math.isfinite(softnessMeters) ? softnessMeters : 4f);
            float open = SmoothRange(-softness, softness, math.isfinite(sdfDistance) ? sdfDistance : softness);
            return math.lerp(math.saturate(hardDampening), 1f, open);
        }

        private static float3 ResolveShockDirection(
            float3 delta,
            float rawDistanceSq,
            float distanceSq,
            float epsilon,
            uint entityHash,
            uint sourceHash,
            uint frameIndex)
        {
            bool hasDirection = math.isfinite(rawDistanceSq) && rawDistanceSq > epsilon;
            float3 radial = delta * math.rsqrt(math.max(distanceSq, epsilon));
            if (hasDirection)
                return radial;

            uint seed = unchecked(entityHash ^ (sourceHash * 747796405u) ^ (frameIndex * 2891336453u) ^ 0x53323438u);
            return HashUnitDirection(seed);
        }

        private static float3 HashUnitDirection(uint seed)
        {
            uint hx = MixHash(seed ^ 0xA511E9B3u);
            uint hy = MixHash(seed ^ 0x63D83595u);
            uint hz = MixHash(seed ^ 0x9E3779B9u);
            float3 value = new float3(HashToSigned(hx), HashToSigned(hy), HashToSigned(hz));
            float lengthSq = math.lengthsq(value);
            return math.select(
                new float3(0f, 1f, 0f),
                value * math.rsqrt(math.max(lengthSq, 0.000001f)),
                math.isfinite(lengthSq) & lengthSq > 0.000001f);
        }

        private static uint MixHash(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        private static float HashToSigned(uint value)
        {
            return ((value & 0x00FFFFFFu) * (1f / 8388607.5f)) - 1f;
        }

        private static float3 LocalDeltaToFloat3(double3 delta, ref uint flags)
        {
            if (!math.all(math.isfinite(delta)))
            {
                flags |= AbyssalCavitationPacketFlags.NonFiniteRecovered;
                return float3.zero;
            }

            double span = AbyssalCavitationConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            if (math.all(math.isfinite(local)))
                return local;

            flags |= AbyssalCavitationPacketFlags.NonFiniteRecovered;
            return float3.zero;
        }

        private static float3 LocalDeltaToFloat3NoFlags(double3 delta)
        {
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            double span = AbyssalCavitationConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }

        private static bool IsActive(in ShockwaveEventDTO wave)
        {
            return wave.CurrentRadius >= 0f &&
                   wave.CurrentRadius <= wave.MaxRadius &&
                   wave.MaxRadius > 0f &&
                   wave.PeakPressure > 0f &&
                   math.isfinite(wave.CurrentRadius) &&
                   math.isfinite(wave.MaxRadius) &&
                   math.isfinite(wave.PeakPressure) &&
                   math.isfinite(wave.ExpansionSpeed) &&
                   math.all(math.isfinite(wave.EpicenterAUP));
        }

        private static float Hash01(uint hash)
        {
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(math.isfinite(value) ? value : 1f);
            return x * x * (3f - 2f * x);
        }

        private static float SmoothRange(float edge0, float edge1, float value)
        {
            float t = math.saturate((value - edge0) * math.rcp(math.max(edge1 - edge0, 0.0001f)));
            return t * t * (3f - 2f * t);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct UpdateCavityShaderParamsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [ReadOnly, NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;
        [NoAlias] public NativeArray<CavitationVisualSphereDTO> Visuals;
        public double3 LocalOriginAUP;
        public AbyssalCavitationTuningDTO Tuning;
        public uint FrameIndex;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Visuals.Length)
                return;

            Visuals[index] = default;
            int active = Counters.IsCreated && Counters.Length > AbyssalCavitationCounterIndex.ActiveShockwaves
                ? math.clamp(Counters[AbyssalCavitationCounterIndex.ActiveShockwaves].Value, 0, Shockwaves.Length)
                : 0;
            if ((uint)index >= (uint)active)
                return;

            ShockwaveEventDTO wave = Shockwaves[index];
            if (wave.CurrentRadius < 0f || wave.MaxRadius <= 0f)
                return;

            uint flags = 0u;
            float3 center = LocalDeltaToFloat3(wave.EpicenterAUP - LocalOriginAUP, ref flags);
            float age01 = math.saturate(wave.CurrentRadius * math.rcp(math.max(wave.MaxRadius, 0.0001f)));
            float q = Smooth01(Tuning.GlobalQualityWeight);
            float intensity = math.saturate(wave.PeakPressure * 0.00008f) * math.max(0f, Tuning.VisualIntensityScale);
            float phase = Hash01(wave.SourceHashID ^ FrameIndex * 747796405u);
            float phaseRadians = phase * 6.2831855f;
            Visuals[index] = new CavitationVisualSphereDTO
            {
                CenterRadius = new float4(center, wave.CurrentRadius),
                IntensityAgeQualityFlags = new float4(intensity, age01, q, flags),
                CurlPhase = new float4(
                    AbyssalCavitationSimdMath.SinPolynomial7(phaseRadians),
                    AbyssalCavitationSimdMath.CosPolynomial7(phaseRadians),
                    phase,
                    wave.MaxRadius),
                Reserved = new float4(wave.ExpansionSpeed, wave.PeakPressure, wave.SourceHashID & 0xFFFFu, 0f)
            };
        }

        private static float3 LocalDeltaToFloat3(double3 delta, ref uint flags)
        {
            if (!math.all(math.isfinite(delta)))
            {
                flags |= AbyssalCavitationTelemetryFlags.NonFiniteRecovered;
                return float3.zero;
            }

            double span = AbyssalCavitationConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }

        private static float Hash01(uint hash)
        {
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(math.isfinite(value) ? value : 1f);
            return x * x * (3f - 2f * x);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct RecordShockwaveTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ShockwaveEventDTO> Shockwaves;
        [NoAlias] public NativeArray<ShockwaveCounterBlock> Counters;
        [ReadOnly, NoAlias] public NativeArray<ShockwaveForcePacketDTO> ForcePackets;
        [NoAlias] public NativeArray<ShockwaveTelemetryEntry> Telemetry;
        public AbyssalCavitationTuningDTO Tuning;
        public uint FrameIndex;
        public float CpuMicroseconds;

        public void Execute()
        {
            if (!Counters.IsCreated || !Telemetry.IsCreated || Telemetry.Length == 0)
                return;

            int active = ReadCounter(AbyssalCavitationCounterIndex.ActiveShockwaves, Shockwaves.IsCreated ? Shockwaves.Length : 0);
            int candidates = ReadCounter(AbyssalCavitationCounterIndex.CandidateCount, ForcePackets.IsCreated ? ForcePackets.Length : 0);
            int forcePackets = 0;
            int epsilonClampCount = 0;
            float peakForce = 0f;
            float peakPressure = 0f;
            uint flags = (uint)math.max(0, ReadCounter(AbyssalCavitationCounterIndex.FaultFlags, int.MaxValue));
            uint hash = 2166136261u;
            double3 epicenter = double3.zero;
            float radius = 0f;

            for (int i = 0; i < active; i++)
            {
                ShockwaveEventDTO wave = Shockwaves[i];
                epicenter = wave.EpicenterAUP;
                radius = math.max(radius, wave.CurrentRadius);
                peakPressure = math.max(peakPressure, wave.PeakPressure);
                hash = Hash(hash, math.asuint(wave.CurrentRadius));
                hash = Hash(hash, math.asuint(wave.PeakPressure));
                if (!math.all(math.isfinite(wave.EpicenterAUP)) || !math.isfinite(wave.CurrentRadius))
                    flags |= AbyssalCavitationTelemetryFlags.NonFiniteRecovered;
            }

            for (int i = 0; i < candidates; i++)
            {
                ShockwaveForcePacketDTO packet = ForcePackets[i];
                if ((packet.Flags & AbyssalCavitationPacketFlags.Active) == 0u)
                    continue;

                forcePackets++;
                float forceSq = math.lengthsq(packet.Force);
                if (!math.isfinite(forceSq))
                {
                    flags |= AbyssalCavitationTelemetryFlags.NonFiniteRecovered;
                    continue;
                }

                peakForce = math.max(peakForce, AbyssalCavitationSimdMath.LengthFromSq(forceSq));
                if ((packet.Flags & AbyssalCavitationPacketFlags.SdfDampened) != 0u)
                    flags |= AbyssalCavitationTelemetryFlags.SdfDampened;
                if ((packet.Flags & AbyssalCavitationPacketFlags.ForceSaturated) != 0u)
                    flags |= AbyssalCavitationTelemetryFlags.ForceSaturated;
                if ((packet.Flags & AbyssalCavitationPacketFlags.EpsilonClamped) != 0u)
                {
                    flags |= AbyssalCavitationTelemetryFlags.EpsilonClamped;
                    epsilonClampCount++;
                }
                if ((packet.Flags & AbyssalCavitationPacketFlags.NonFiniteRecovered) != 0u)
                    flags |= AbyssalCavitationTelemetryFlags.NonFiniteRecovered;
            }

            WriteCounter(AbyssalCavitationCounterIndex.ForcePacketCount, forcePackets);
            WriteCounter(AbyssalCavitationCounterIndex.VisualCount, active);
            WriteCounter(AbyssalCavitationCounterIndex.LastFrame, unchecked((int)FrameIndex));

            int head = ReadCounter(AbyssalCavitationCounterIndex.TelemetryHead, Telemetry.Length);
            Telemetry[head] = new ShockwaveTelemetryEntry
            {
                EpicenterAUP = epicenter,
                CurrentRadius = radius,
                PeakPressure = peakPressure,
                PeakForce = peakForce,
                GlobalQualityWeight = math.saturate(math.select(1f, Tuning.GlobalQualityWeight, math.isfinite(Tuning.GlobalQualityWeight))),
                FrameIndex = FrameIndex,
                StateHash = hash,
                ActiveShockwaves = active,
                CandidateCount = candidates,
                AffectedEntities = forcePackets,
                EpsilonClampCount = epsilonClampCount,
                CpuMicroseconds = math.isfinite(CpuMicroseconds) ? math.max(0f, CpuMicroseconds) : 0f,
                Flags = flags
            };

            head++;
            if (head >= Telemetry.Length)
                head = 0;
            WriteCounter(AbyssalCavitationCounterIndex.TelemetryHead, head);
            WriteCounter(AbyssalCavitationCounterIndex.FaultFlags, (int)flags);
        }

        private int ReadCounter(int index, int maxValue)
        {
            if (!Counters.IsCreated || (uint)index >= (uint)Counters.Length)
                return 0;

            return math.clamp(Counters[index].Value, 0, maxValue);
        }

        private void WriteCounter(int index, int value)
        {
            if (!Counters.IsCreated || (uint)index >= (uint)Counters.Length)
                return;

            ShockwaveCounterBlock block = Counters[index];
            block.Value = value;
            Counters[index] = block;
        }

        private static uint Hash(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }
    }

    public sealed partial class PhysicsApplySystem
    {
        internal static void DrainCavitationForcePackets(
            NativeArray<ShockwaveForcePacketDTO> packets,
            NativeArray<ForcePacketDTO> transportPackets,
            NativeArray<ShockwaveCounterBlock> counters,
            AbyssalCavitationTuningDTO tuning,
            double3 localOriginAUP,
            uint frameIndex,
            int maxPackets,
            out int accepted,
            out int unresolved)
        {
            accepted = 0;
            unresolved = 0;
            if (!packets.IsCreated ||
                !transportPackets.IsCreated ||
                packets.Length <= 0 ||
                !counters.IsCreated ||
                counters.Length <= AbyssalCavitationCounterIndex.CandidateCount)
            {
                return;
            }

            PhysicsApplySystem system = EnsureRuntimeInstance();
            GlobalPhysicsStateManager.TryGetBuoyancyBodyResolver(out GlobalPhysicsStateManager bodyResolver);
            int candidateCount = math.clamp(counters[AbyssalCavitationCounterIndex.CandidateCount].Value, 0, packets.Length);
            int budget = math.min(candidateCount, math.clamp(maxPackets, 0, packets.Length));
            float maxForce = math.max(1f, AbyssalCavitationSanitizer.SanitizeTuning(tuning).MaxForceNewton);
            for (int i = 0; i < budget; i++)
            {
                ShockwaveForcePacketDTO packet = packets[i];
                ForcePacketDTO transport = i < transportPackets.Length ? transportPackets[i] : default;
                uint packetFlags = packet.Flags | transport.ApplicationFlags;
                if ((packetFlags & AbyssalCavitationPacketFlags.Active) == 0u)
                    continue;
                if (frameIndex != 0u && packet.FrameIndex != frameIndex)
                    continue;
                uint targetHash = transport.TargetEntityHash != 0u ? transport.TargetEntityHash : packet.TargetEntityHash;
                if (system == null || targetHash == 0u)
                {
                    unresolved++;
                    continue;
                }

                Rigidbody body;
                bool resolved = false;
                if (packet.RigidbodySlot >= 0)
                {
                    resolved = GlobalPhysicsStateManager.TryResolveTrackedBodyByIndex(
                        bodyResolver,
                        packet.RigidbodySlot,
                        targetHash,
                        out body);
                }
                else
                {
                    body = null;
                }

                if (!resolved)
                {
                    resolved = GlobalPhysicsStateManager.TryFindTrackedBodyByFoldedEntityHash(
                        bodyResolver,
                        targetHash,
                        out body,
                        out _);
                }

                if (!resolved)
                {
                    unresolved++;
                    continue;
                }

                float3 force = transport.TargetEntityHash != 0u ? transport.ForceVector : packet.Force;
                float forceSq = math.lengthsq(force);
                if (!math.isfinite(forceSq) || forceSq <= 0.000001f)
                {
                    unresolved++;
                    continue;
                }

                if (forceSq > maxForce * maxForce)
                    force *= maxForce * math.rsqrt(math.max(forceSq, 0.000001f));

                float3 point = LocalAupToFloat3(packet.ApplicationAUP - localOriginAUP);
                if (!math.all(math.isfinite(point)))
                {
                    unresolved++;
                    continue;
                }

                if (system.QueueForceAtPosition(
                        body,
                        new Vector3(force.x, force.y, force.z),
                        new Vector3(point.x, point.y, point.z),
                        ForceMode.Impulse,
                        ForcePacketPriority.Critical,
                        wake: true,
                        extraFlags: ForcePacketFlags.None))
                {
                    accepted++;
                }
                else
                {
                    unresolved++;
                }
            }
        }

        private static float3 LocalAupToFloat3(double3 delta)
        {
            double span = AbyssalCavitationConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            return new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
        }
    }
}
