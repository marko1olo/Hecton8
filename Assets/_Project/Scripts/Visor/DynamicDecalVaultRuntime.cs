using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Visor
{
    internal static class DynamicDecalVaultBufferIds
    {
        public const BufferID Instances = (BufferID)73190;
        public const BufferID UploadScratch = (BufferID)73191;
        public const BufferID RuntimeState = (BufferID)73192;
        public const BufferID TelemetryRing = (BufferID)73193;
        public const BufferID Tuning = (BufferID)73194;
        public const BufferID MaterialProfiles = (BufferID)73195;
        public const BufferID CsvScratch = (BufferID)73196;
        public const BufferID RequestRing = (BufferID)73197;
        public const BufferID RequestState = (BufferID)73198;
    }

    public static class DynamicDecalMaterialHashes
    {
        public const uint Scorch = 0u;
        public const uint Blood = 1u;
        public const uint Acid = 2u;
        public const uint HullDent = 3u;
        public const uint GlassCrack = 4u;
        public const uint Burn = 5u;
    }

    public static class DynamicDecalFlags
    {
        public const uint None = 0u;
        public const uint Active = 1u << 0;
        public const uint Ballistic = 1u << 1;
        public const uint HullImpact = 1u << 2;
        public const uint Mock = 1u << 3;
        public const uint PersistentGlass = 1u << 4;
        public const uint NonFinite = 1u << 31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct TraumaDecalDTO
    {
        [FieldOffset(0)] public float4x4 LocalToWorld;
        [FieldOffset(64)] public uint DecalTypeHash;
        [FieldOffset(68)] public float Opacity01;
        [FieldOffset(72)] public float BirthTime;
        [FieldOffset(76)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DecalRequestSignal
    {
        [FieldOffset(0)] public double3 ImpactAup;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float RadiusMeters;
        [FieldOffset(40)] public float ProjectionDepthMeters;
        [FieldOffset(44)] public float LifetimeSeconds;
        [FieldOffset(48)] public uint MaterialHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint StableSeed;
        [FieldOffset(60)] public uint SourceFrame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DecalRequestQueueStateDTO
    {
        [FieldOffset(0)] public int WriteIndex;
        [FieldOffset(4)] public int ReadIndex;
        [FieldOffset(8)] public int PendingCount;
        [FieldOffset(12)] public int Capacity;
        [FieldOffset(16)] public uint EnqueuedTotal;
        [FieldOffset(20)] public uint DrainedTotal;
        [FieldOffset(24)] public uint DroppedTotal;
        [FieldOffset(28)] public uint LastFrame;
        [FieldOffset(32)] private byte _pad32;
        [FieldOffset(33)] private byte _pad33;
        [FieldOffset(34)] private byte _pad34;
        [FieldOffset(35)] private byte _pad35;
        [FieldOffset(36)] private byte _pad36;
        [FieldOffset(37)] private byte _pad37;
        [FieldOffset(38)] private byte _pad38;
        [FieldOffset(39)] private byte _pad39;
        [FieldOffset(40)] private byte _pad40;
        [FieldOffset(41)] private byte _pad41;
        [FieldOffset(42)] private byte _pad42;
        [FieldOffset(43)] private byte _pad43;
        [FieldOffset(44)] private byte _pad44;
        [FieldOffset(45)] private byte _pad45;
        [FieldOffset(46)] private byte _pad46;
        [FieldOffset(47)] private byte _pad47;
        [FieldOffset(48)] private byte _pad48;
        [FieldOffset(49)] private byte _pad49;
        [FieldOffset(50)] private byte _pad50;
        [FieldOffset(51)] private byte _pad51;
        [FieldOffset(52)] private byte _pad52;
        [FieldOffset(53)] private byte _pad53;
        [FieldOffset(54)] private byte _pad54;
        [FieldOffset(55)] private byte _pad55;
        [FieldOffset(56)] private byte _pad56;
        [FieldOffset(57)] private byte _pad57;
        [FieldOffset(58)] private byte _pad58;
        [FieldOffset(59)] private byte _pad59;
        [FieldOffset(60)] private byte _pad60;
        [FieldOffset(61)] private byte _pad61;
        [FieldOffset(62)] private byte _pad62;
        [FieldOffset(63)] private byte _pad63;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DecalRuntimeStateDTO
    {
        [FieldOffset(0)] public int CurrentWriteIndex;
        [FieldOffset(4)] public int ActiveCount;
        [FieldOffset(8)] public uint TotalWritten;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public int LastUploadCount;
        [FieldOffset(20)] public int NewThisFrame;
        [FieldOffset(24)] public int DroppedThisFrame;
        [FieldOffset(28)] public int MaxActiveThisFrame;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public float DecayRate;
        [FieldOffset(40)] public float ThermalPressure01;
        [FieldOffset(44)] public float CpuMicroseconds;
        [FieldOffset(48)] public float UploadMicroseconds;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint LastBallisticFrame;
        [FieldOffset(60)] public float NormalRefractionIntensity;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DecalTuningDTO
    {
        [FieldOffset(0)] public float BaseFadeTimeSeconds;
        [FieldOffset(4)] public float MaximumOverkillCapacity;
        [FieldOffset(8)] public float NormalRefractionIntensity;
        [FieldOffset(12)] public float ProjectionDepthMeters;
        [FieldOffset(16)] public float LowTierCapacity;
        [FieldOffset(20)] public float BaseRadiusMeters;
        [FieldOffset(24)] public uint Revision;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TraumaWoundTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveDecals;
        [FieldOffset(8)] public uint NewDecals;
        [FieldOffset(12)] public uint UploadCount;
        [FieldOffset(16)] public float GpuUploadMicroseconds;
        [FieldOffset(20)] public float CpuMicroseconds;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float ThermalPressure01;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint StateHash;
        [FieldOffset(40)] public uint DroppedThisFrame;
        [FieldOffset(44)] public uint TotalWritten;
        [FieldOffset(48)] public uint MaxActiveThisFrame;
        [FieldOffset(52)] public uint LastBallisticFrame;
        [FieldOffset(56)] private byte _pad56;
        [FieldOffset(57)] private byte _pad57;
        [FieldOffset(58)] private byte _pad58;
        [FieldOffset(59)] private byte _pad59;
        [FieldOffset(60)] private byte _pad60;
        [FieldOffset(61)] private byte _pad61;
        [FieldOffset(62)] private byte _pad62;
        [FieldOffset(63)] private byte _pad63;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DecalMaterialProfileDTO
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint AtlasSlice;
        [FieldOffset(8)] public float LifetimeSeconds;
        [FieldOffset(12)] public float RadiusMeters;
        [FieldOffset(16)] public float ProjectionDepthMeters;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private byte _pad24;
        [FieldOffset(25)] private byte _pad25;
        [FieldOffset(26)] private byte _pad26;
        [FieldOffset(27)] private byte _pad27;
        [FieldOffset(28)] private byte _pad28;
        [FieldOffset(29)] private byte _pad29;
        [FieldOffset(30)] private byte _pad30;
        [FieldOffset(31)] private byte _pad31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct DynamicDecalFrameStats
    {
        [FieldOffset(0)]
        public VaultGenerationHandle<TraumaDecalDTO> UploadHandle;
        [FieldOffset(16)]
        public int UploadCapacity;
        [FieldOffset(20)]
        public int UploadCount;
        [FieldOffset(24)]
        public int ActiveCount;
        [FieldOffset(28)]
        public int NewCount;
        [FieldOffset(32)]
        public int MaxActiveCount;
        [FieldOffset(36)]
        public float CpuMicroseconds;
        [FieldOffset(40)]
        public float UploadMicroseconds;
        [FieldOffset(44)]
        public float GlobalQualityWeight;
        [FieldOffset(48)]
        public float ThermalPressure01;
        [FieldOffset(52)]
        public float NormalRefractionIntensity;
    }

    public static unsafe class DynamicDecalVaultRuntime
    {
        public const int MaxCapacity = 128;
        public const int LowCapacity = 8;
        public const int TelemetryCapacity = 300;
        public const int RequestRingCapacity = 1024;
        public const int AtlasSliceCount = 16;
        public const uint DecalTypePayloadMask = 0xFFu;
        public const uint DecalTypePackedMask = 0x0Fu;
        public const int DecalAtlasPackedShift = 4;
        public const uint DecalAtlasPackedMask = 0x0Fu;
        public const uint DecalMaterialPayloadPackedFlag = 1u << 31;
        public const int DecalLifetimePackedShift = 8;
        public const uint DecalLifetimePackedMask = 0xFFFFu;
        public const float DecalLifetimePackedScale = 100f;
        public const int MaxMaterialProfiles = 256;
        public const int CsvScratchBytes = 16384;

        private const uint RuntimeInitializedFlag = 1u << 0;
        private const uint RuntimeLayoutFaultFlag = 1u << 1;
        private const uint RuntimeNonFiniteFaultFlag = 1u << 2;
        private const uint RuntimeUploadStallFlag = 1u << 3;
        private const uint DumpMagic = 0x4445434Cu; // DECL
        private const string DumpFileName = "Dump_1335_DynamicDecal.bin";
        private const string LogOwner = "AGENT_1335";
        private const SystemID OwnerSystem = SystemID.Vfx;

        private static readonly ProfilerMarker _visualSyncMarker = new ProfilerMarker("H8.VisorTrauma.VisualSync");
        private static readonly ProfilerMarker _enqueueMarker = new ProfilerMarker("H8.VisorTrauma.Enqueue");

        private static IDataVault _vault;
        private static IPlayerRuntimeContext _cachedPlayerContext;
        private static bool _coldRoutesCached;
        private static VaultGenerationHandle<TraumaDecalDTO> _instancesHandle;
        private static VaultGenerationHandle<TraumaDecalDTO> _uploadHandle;
        private static VaultGenerationHandle<DecalRuntimeStateDTO> _stateHandle;
        private static VaultGenerationHandle<TraumaWoundTelemetryEntry> _telemetryHandle;
        private static VaultGenerationHandle<DecalTuningDTO> _tuningHandle;
        private static VaultGenerationHandle<DecalMaterialProfileDTO> _materialProfileHandle;
        private static VaultGenerationHandle<byte> _csvScratchHandle;
        private static VaultGenerationHandle<DecalRequestSignal> _requestRingHandle;
        private static VaultGenerationHandle<DecalRequestQueueStateDTO> _requestStateHandle;
        private static uint _lastIngestedBallisticFrame;
        private static uint _lastIngestedHighSpeedFrame;
        private static uint _lastIngestedCombatDamageFrame;
        private static bool _hasIngestedHighSpeedFrame;
        private static bool _hasIngestedCombatDamageFrame;
        private static int _telemetryCursor;
        private static int _materialProfileCount;
        private static uint _lastSignalSnapshotFrameId;
        private static uint _fallbackVisualFrameId;
        private static int _droppedIngressThisFrame;
        private static bool _dumpedFault;
        private static bool _layoutValidated;
        private static bool _layoutValid;
        private static Vector3 _lastCameraWorldPosition;
        private static JobHandle _pendingVisualSyncHandle;
        private static bool _pendingVisualSyncActive;
        private static long _pendingVisualSyncStartTicks;
        private static float _pendingVisualSyncQuality;
        private static float _pendingVisualSyncThermalPressure;
        private static int _pendingVisualSyncMaxActive;
        private static DynamicDecalFrameStats _lastCompletedStats;
#pragma warning disable CS0414
        private static bool _hasLastCompletedStats;
#pragma warning restore CS0414
        private static DecalTuningDTO _lastTuningSnapshot;
        private static DecalRuntimeStateDTO _lastRuntimeStateSnapshot;
        private static TraumaWoundTelemetryEntry _lastTelemetrySnapshot;
        private static bool _hasTuningSnapshot;
        private static bool _hasRuntimeStateSnapshot;
        private static bool _hasTelemetrySnapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingVisualSyncActive)
            {
                DispatcherJobFence.TryComplete(ref _pendingVisualSyncHandle, forceComplete: true);
                ReleaseAllDynamicDecalWriteLocks();
            }

            ReleaseDynamicDecalVaultHandles(_vault);
            _vault = null;
            _cachedPlayerContext = null;
            _coldRoutesCached = false;
            _instancesHandle = default;
            _uploadHandle = default;
            _stateHandle = default;
            _telemetryHandle = default;
            _tuningHandle = default;
            _materialProfileHandle = default;
            _csvScratchHandle = default;
            _requestRingHandle = default;
            _requestStateHandle = default;
            _lastIngestedBallisticFrame = 0u;
            _lastIngestedHighSpeedFrame = 0u;
            _lastIngestedCombatDamageFrame = 0u;
            _hasIngestedHighSpeedFrame = false;
            _hasIngestedCombatDamageFrame = false;
            _telemetryCursor = 0;
            _materialProfileCount = 0;
            _lastSignalSnapshotFrameId = 0u;
            _fallbackVisualFrameId = 0u;
            _droppedIngressThisFrame = 0;
            _dumpedFault = false;
            _layoutValidated = false;
            _layoutValid = false;
            _lastCameraWorldPosition = Vector3.zero;
            _pendingVisualSyncHandle = default;
            _pendingVisualSyncActive = false;
            _pendingVisualSyncStartTicks = 0L;
            _pendingVisualSyncQuality = 0f;
            _pendingVisualSyncThermalPressure = 0f;
            _pendingVisualSyncMaxActive = 0;
            _lastCompletedStats = default;
            _hasLastCompletedStats = false;
            _lastTuningSnapshot = default;
            _lastRuntimeStateSnapshot = default;
            _lastTelemetrySnapshot = default;
            _hasTuningSnapshot = false;
            _hasRuntimeStateSnapshot = false;
            _hasTelemetrySnapshot = false;
        }

        public static bool WarmupColdGlobalRoutes()
        {
            if (_coldRoutesCached && _vault != null)
                return true;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            _vault = vault;
            _cachedPlayerContext = GlobalRegistry.Player;
            _coldRoutesCached = true;
            return true;
        }

        public static bool TryInitializeColdStorage()
        {
            return EnsureInitialized();
        }

        public static bool IsColdStorageReady()
        {
            return IsInitializedForRead();
        }

        public static void RefreshColdPlayerContext()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
        }

        public static void ResetColdStorageForRebind()
        {
            ForceCompletePendingVisualSync(out _);
            ReleaseDynamicDecalVaultHandles(_vault);
            _vault = null;
            _cachedPlayerContext = null;
            _coldRoutesCached = false;
            _instancesHandle = default;
            _uploadHandle = default;
            _stateHandle = default;
            _telemetryHandle = default;
            _tuningHandle = default;
            _materialProfileHandle = default;
            _csvScratchHandle = default;
            _requestRingHandle = default;
            _requestStateHandle = default;
            _telemetryCursor = 0;
            _materialProfileCount = 0;
            _lastSignalSnapshotFrameId = 0u;
            _fallbackVisualFrameId = 0u;
            _droppedIngressThisFrame = 0;
            _lastCameraWorldPosition = Vector3.zero;
            _pendingVisualSyncHandle = default;
            _pendingVisualSyncActive = false;
            _pendingVisualSyncStartTicks = 0L;
            _pendingVisualSyncQuality = 0f;
            _pendingVisualSyncThermalPressure = 0f;
            _pendingVisualSyncMaxActive = 0;
            _lastCompletedStats = default;
            _hasLastCompletedStats = false;
            _lastTuningSnapshot = default;
            _lastRuntimeStateSnapshot = default;
            _lastTelemetrySnapshot = default;
            _hasTuningSnapshot = false;
            _hasRuntimeStateSnapshot = false;
            _hasTelemetrySnapshot = false;
        }

        public static bool ValidateDecalInstanceLayout()
        {
            if (_layoutValidated)
                return _layoutValid;

            _layoutValid = UnsafeUtility.SizeOf<TraumaDecalDTO>() == 80 &&
                           UnsafeUtility.SizeOf<DecalRequestSignal>() == 64 &&
                           UnsafeUtility.SizeOf<DecalRequestQueueStateDTO>() == 64 &&
                           UnsafeUtility.SizeOf<DynamicDecalFrameStats>() == 56;
#if UNITY_EDITOR
            _layoutValid = _layoutValid &&
                           OffsetOf<TraumaDecalDTO>(nameof(TraumaDecalDTO.LocalToWorld)) == 0 &&
                           OffsetOf<TraumaDecalDTO>(nameof(TraumaDecalDTO.DecalTypeHash)) == 64 &&
                           OffsetOf<TraumaDecalDTO>(nameof(TraumaDecalDTO.Opacity01)) == 68 &&
                           OffsetOf<TraumaDecalDTO>(nameof(TraumaDecalDTO.BirthTime)) == 72 &&
                           OffsetOf<TraumaDecalDTO>(nameof(TraumaDecalDTO.Flags)) == 76 &&
                           OffsetOf<DecalRequestSignal>(nameof(DecalRequestSignal.ImpactAup)) == 0 &&
                           OffsetOf<DecalRequestSignal>(nameof(DecalRequestSignal.Normal)) == 24 &&
                           OffsetOf<DecalRequestSignal>(nameof(DecalRequestSignal.SourceFrame)) == 60 &&
                           OffsetOf<DecalRequestQueueStateDTO>(nameof(DecalRequestQueueStateDTO.WriteIndex)) == 0 &&
                           OffsetOf<DecalRequestQueueStateDTO>(nameof(DecalRequestQueueStateDTO.PendingCount)) == 8 &&
                           OffsetOf<DecalRequestQueueStateDTO>("_pad56") == 56 &&
                           OffsetOf<DecalRequestQueueStateDTO>("_pad60") == 60 &&
                           OffsetOf<DynamicDecalFrameStats>(nameof(DynamicDecalFrameStats.UploadHandle)) == 0 &&
                           OffsetOf<DynamicDecalFrameStats>(nameof(DynamicDecalFrameStats.UploadCapacity)) == 16 &&
                           OffsetOf<DynamicDecalFrameStats>(nameof(DynamicDecalFrameStats.NormalRefractionIntensity)) == 52;
#endif
            _layoutValidated = true;
            return _layoutValid;
        }

        public static bool ValidateTraumaDecalLayout()
        {
            return ValidateDecalInstanceLayout();
        }

        private static bool IsInitializedForRead()
        {
            return _coldRoutesCached &&
                   _vault != null &&
                   HasDynamicDecalVaultBuffer(_vault, in _instancesHandle, DynamicDecalVaultBufferIds.Instances, MaxCapacity) &&
                   HasDynamicDecalVaultBuffer(_vault, in _uploadHandle, DynamicDecalVaultBufferIds.UploadScratch, MaxCapacity) &&
                   HasDynamicDecalVaultBuffer(_vault, in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState, 1) &&
                   HasDynamicDecalVaultBuffer(_vault, in _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing, TelemetryCapacity) &&
                   HasDynamicDecalVaultBuffer(_vault, in _tuningHandle, DynamicDecalVaultBufferIds.Tuning, 1) &&
                   HasDynamicDecalVaultBuffer(_vault, in _materialProfileHandle, DynamicDecalVaultBufferIds.MaterialProfiles, MaxMaterialProfiles) &&
                   HasDynamicDecalVaultBuffer(_vault, in _csvScratchHandle, DynamicDecalVaultBufferIds.CsvScratch, CsvScratchBytes) &&
                   HasDynamicDecalVaultBuffer(_vault, in _requestRingHandle, DynamicDecalVaultBufferIds.RequestRing, RequestRingCapacity) &&
                   HasDynamicDecalVaultBuffer(_vault, in _requestStateHandle, DynamicDecalVaultBufferIds.RequestState, 1);
        }

        public static bool TryEnqueueRuntimeImpact(
            Vector3 runtimePosition,
            Vector3 surfaceNormal,
            uint materialHash,
            float radiusMeters,
            float lifetimeSeconds,
            uint flags)
        {
            using (_enqueueMarker.Auto())
            {
                if (!IsInitializedForRead())
                    return false;

                if (!IsFinite(runtimePosition))
                    return false;

                if (!TryResolveRuntimeAup(runtimePosition, out double3 aup))
                    return false;

                DecalTuningDTO tuning = ResolveLiveTuning();
                float3 normal = MakeFloat3(surfaceNormal.x, surfaceNormal.y, surfaceNormal.z);
                uint seed = Mix(HashFloat3(runtimePosition) ^ materialHash);
                return TryEnqueueAupImpact(
                    aup,
                    normal,
                    materialHash,
                    radiusMeters,
                    tuning.ProjectionDepthMeters,
                    lifetimeSeconds,
                    flags,
                    seed,
                    ResolveVisualFrameId());
            }
        }

        public static bool TryEnqueueAupImpact(
            double3 impactAup,
            float3 surfaceNormal,
            uint materialHash,
            float radiusMeters,
            float projectionDepthMeters,
            float lifetimeSeconds,
            uint flags,
            uint stableSeed,
            uint sourceFrame)
        {
            using (_enqueueMarker.Auto())
            {
                if (!IsInitializedForRead())
                    return false;

                if (!math.all(math.isfinite(impactAup)))
                    return false;

                DecalTuningDTO defaults = ResolveLiveTuning();
                DecalRequestSignal request = default;
                request.ImpactAup = impactAup;
                request.Normal = SanitizeNormal(surfaceNormal, MakeFloat3(0f, 1f, 0f));
                request.RadiusMeters = math.clamp(math.isfinite(radiusMeters) ? radiusMeters : defaults.BaseRadiusMeters, 0.025f, 8f);
                request.ProjectionDepthMeters = math.clamp(math.isfinite(projectionDepthMeters) ? projectionDepthMeters : defaults.ProjectionDepthMeters, 0.025f, 2f);
                request.LifetimeSeconds = math.clamp(math.isfinite(lifetimeSeconds) ? lifetimeSeconds : defaults.BaseFadeTimeSeconds, 0.1f, 60f);
                request.MaterialHash = PackRequestMaterialPayload(ResolveDecalTypeFromMaterial(materialHash), ResolveAtlasSliceFromMaterial(materialHash));
                request.Flags = flags | DynamicDecalFlags.Active;
                request.StableSeed = stableSeed != 0u
                    ? stableSeed
                    : Mix(math.asuint((float)impactAup.x) ^ RotateLeft(math.asuint((float)impactAup.y), 11) ^ RotateLeft(math.asuint((float)impactAup.z), 19) ^ materialHash);
                request.SourceFrame = sourceFrame;
                return TryEnqueueRequest(in request);
            }
        }

        public static bool GenerateMockDecals(int count)
        {
            return GenerateMockTraumaWounds(count);
        }

        public static bool GenerateMockTraumaWounds(int count)
        {
            if (!EnsureInitialized() || count <= 0)
                return false;

            if (_pendingVisualSyncActive)
            {
                AccumulateDroppedIngress(count);
                return false;
            }

            bool requestRingLocked = false;
            bool requestStateLocked = false;
            try
            {
                if (!TryAcquireDynamicDecalVaultBuffer(in _requestRingHandle, DynamicDecalVaultBufferIds.RequestRing, RequestRingCapacity, out NativeArray<DecalRequestSignal> requestRing))
                {
                    AccumulateDroppedIngress(count);
                    return false;
                }

                requestRingLocked = true;
                if (!TryAcquireDynamicDecalVaultBuffer(in _requestStateHandle, DynamicDecalVaultBufferIds.RequestState, 1, out NativeArray<DecalRequestQueueStateDTO> requestStateArray))
                {
                    AccumulateDroppedIngress(count);
                    return false;
                }

                requestStateLocked = true;
                DecalRequestQueueStateDTO queueState = SanitizeRequestQueueState(requestStateArray[0], requestRing.Length);
                int headroom = math.min(requestRing.Length, RequestRingCapacity) - queueState.PendingCount;
                if (headroom <= 0)
                {
                    AccumulateDroppedIngress(count);
                    queueState.DroppedTotal += (uint)math.max(0, count);
                    requestStateArray[0] = queueState;
                    return false;
                }

                int safeCount = math.clamp(count, 1, math.min(MaxCapacity, headroom));
                AccumulateDroppedIngress(count - safeCount);
                int startIndex = queueState.WriteIndex;
                int capacity = math.min(requestRing.Length, RequestRingCapacity);
                uint frame = ResolveVisualFrameId();
                double3 originAup = ResolveCurrentRuntimeOriginAup();
                for (int i = 0; i < safeCount; i++)
                {
                    int targetIndex = startIndex + i;
                    if (targetIndex >= capacity)
                        targetIndex -= capacity;

                    requestRing[targetIndex] = BuildMockTraumaWoundRequest(i, frame, originAup);
                }

                queueState.WriteIndex = WrapRequestIndex(startIndex + safeCount, queueState.Capacity);
                queueState.PendingCount += safeCount;
                queueState.EnqueuedTotal += (uint)safeCount;
                queueState.DroppedTotal += (uint)math.max(0, count - safeCount);
                queueState.LastFrame = frame;
                requestStateArray[0] = queueState;
                return true;
            }
            finally
            {
                if (requestStateLocked)
                    ReleaseDynamicDecalVaultBuffer(in _requestStateHandle, DynamicDecalVaultBufferIds.RequestState);
                if (requestRingLocked)
                    ReleaseDynamicDecalVaultBuffer(in _requestRingHandle, DynamicDecalVaultBufferIds.RequestRing);
            }
        }

        private static DecalRequestSignal BuildMockTraumaWoundRequest(
            int index,
            uint frame,
            double3 originAup)
        {
            uint seed = Mix((uint)(index + 1) * 0x9E3779B9u);
            float phase = (seed & 1023u) * (1f / 1024f);
            float radius = 2.0f + ((seed >> 10) & 31u) * 0.22f;
            float xAxis = TriangleWaveSigned(phase);
            float zAxis = TriangleWaveSigned(phase + 0.25f);
            float3 rawNormal = MakeFloat3(xAxis * 0.35f, 1f, zAxis * 0.35f);
            float normalLengthSq = math.max(math.lengthsq(rawNormal), 0.0001f);
            float3 normal = math.all(math.isfinite(rawNormal))
                ? rawNormal * math.rsqrt(normalLengthSq)
                : MakeFloat3(0f, 1f, 0f);
            DecalRequestSignal request = default;
            request.ImpactAup = originAup + MakeDouble3(zAxis * radius, ((index & 15) - 8) * 0.12f, xAxis * radius);
            request.Normal = normal;
            request.RadiusMeters = 0.22f + ((seed >> 16) & 15u) * 0.035f;
            request.ProjectionDepthMeters = 0.18f;
            request.LifetimeSeconds = 4.0f + ((seed >> 21) & 7u) * 0.5f;
            uint pattern = (uint)(index % 5);
            request.MaterialHash = pattern == 0u ? DynamicDecalMaterialHashes.Blood :
                pattern == 1u ? DynamicDecalMaterialHashes.GlassCrack :
                pattern == 2u ? DynamicDecalMaterialHashes.Burn :
                pattern == 3u ? DynamicDecalMaterialHashes.Acid :
                DynamicDecalMaterialHashes.Scorch;
            request.Flags = DynamicDecalFlags.Active | DynamicDecalFlags.Mock;
            if (request.MaterialHash == DynamicDecalMaterialHashes.GlassCrack)
                request.Flags |= DynamicDecalFlags.PersistentGlass;
            request.StableSeed = seed;
            request.SourceFrame = frame;
            return request;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleWaveSigned(float phase)
        {
            return math.abs(math.frac(phase) * 2f - 1f) * 2f - 1f;
        }

        public static bool ExecuteVisualSync(
            Camera camera,
            float deltaTime,
            int requestedCapacity,
            float baseFadeSeconds,
            out DynamicDecalFrameStats stats)
        {
            stats = default;
            using (_visualSyncMarker.Auto())
            {
                if (!IsInitializedForRead())
                    return false;

                if (_pendingVisualSyncActive)
                    return TryFinalizePendingVisualSync(out stats) && stats.UploadCount > 0;

                if (!ValidateDecalInstanceLayout())
                {
                    MarkFault(RuntimeLayoutFaultFlag);
                    DumpBlackBox(RuntimeLayoutFaultFlag);
                    return false;
                }

                long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                uint faultFlags = 0u;
                bool requestRingLocked = false;
                bool requestStateLocked = false;
                bool instancesLocked = false;
                bool uploadLocked = false;
                bool stateLocked = false;
                bool telemetryLocked = false;
                bool tuningLocked = false;
                try
                {
                    TryIngestGlobalImpactSignals();
                    int ingressDroppedBeforeJob = math.max(0, _droppedIngressThisFrame);
                    _droppedIngressThisFrame = 0;

                    if (!TryAcquireDynamicDecalVaultBuffer(in _requestRingHandle, DynamicDecalVaultBufferIds.RequestRing, RequestRingCapacity, out NativeArray<DecalRequestSignal> requests))
                        return false;
                    requestRingLocked = true;
                    if (!TryAcquireDynamicDecalVaultBuffer(in _requestStateHandle, DynamicDecalVaultBufferIds.RequestState, 1, out NativeArray<DecalRequestQueueStateDTO> requestStateArray))
                        return false;
                    requestStateLocked = true;
                    if (!TryAcquireDynamicDecalVaultBuffer(in _instancesHandle, DynamicDecalVaultBufferIds.Instances, MaxCapacity, out NativeArray<TraumaDecalDTO> instances))
                        return false;
                    instancesLocked = true;
                    if (!TryAcquireDynamicDecalVaultBuffer(in _uploadHandle, DynamicDecalVaultBufferIds.UploadScratch, MaxCapacity, out NativeArray<TraumaDecalDTO> upload))
                        return false;
                    uploadLocked = true;
                    if (!TryAcquireDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState, 1, out NativeArray<DecalRuntimeStateDTO> stateArray))
                        return false;
                    stateLocked = true;
                    if (!TryAcquireDynamicDecalVaultBuffer(in _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing, TelemetryCapacity, out NativeArray<TraumaWoundTelemetryEntry> telemetry))
                        return false;
                    telemetryLocked = true;
                    if (!TryAcquireDynamicDecalVaultBuffer(in _tuningHandle, DynamicDecalVaultBufferIds.Tuning, 1, out NativeArray<DecalTuningDTO> tuningArray))
                        return false;
                    tuningLocked = true;

                    DecalTuningDTO tuning = SanitizeTuning(tuningArray[0], baseFadeSeconds, requestedCapacity);
                    tuningArray[0] = tuning;
                    _lastTuningSnapshot = tuning;
                    _hasTuningSnapshot = true;
                    if (!TryGetDynamicDecalElementPtr(stateArray, 0, out void* statePtr))
                    {
                        faultFlags |= RuntimeLayoutFaultFlag;
                        MarkFault(RuntimeLayoutFaultFlag);
                        return false;
                    }

                    ref DecalRuntimeStateDTO state = ref UnsafeUtility.AsRef<DecalRuntimeStateDTO>(statePtr);
                    state.NormalRefractionIntensity = tuning.NormalRefractionIntensity;
                    float targetQuality = ResolveGlobalQualityWeight();
                    float thermalPressure = ResolveThermalPressure01();
                    bool hadRuntimeState = (state.Flags & RuntimeInitializedFlag) != 0u;
                    if (!hadRuntimeState)
                    {
                        ClearNativeBuffer(instances, MaxCapacity);
                        ClearNativeBuffer(upload, MaxCapacity);
                        state = CreateInitializedRuntimeState(targetQuality, thermalPressure, tuning);
                    }

                    float quality = ResolveEffectiveQualityWeight(targetQuality, state.GlobalQualityWeight, deltaTime, thermalPressure, hadRuntimeState);
                    int maxActive = ResolveMaxActiveDecals(quality, tuning);
                    float decayRate = ResolveDecayRate(deltaTime, quality, thermalPressure, tuning);
                    double3 cameraAup = ResolveCameraAup(camera);
                    _lastCameraWorldPosition = camera != null ? camera.transform.position : Vector3.zero;

                    GenerateTraumaDecalMatricesJob generateJob = new GenerateTraumaDecalMatricesJob
                    {
                        Requests = (DecalRequestSignal*)NativeArrayUnsafeUtility.GetUnsafePtr(requests),
                        RequestState = (DecalRequestQueueStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(requestStateArray),
                        Decals = (TraumaDecalDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(instances),
                        State = (DecalRuntimeStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(stateArray),
                        CameraAup = cameraAup,
                        Capacity = math.min(instances.Length, MaxCapacity),
                        MaxRequestsPerFrame = math.max(1, maxActive),
                        DefaultRadiusMeters = math.max(0.025f, tuning.BaseRadiusMeters),
                        DefaultProjectionDepthMeters = math.max(0.01f, tuning.ProjectionDepthMeters),
                        DefaultLifetimeSeconds = math.max(0.1f, tuning.BaseFadeTimeSeconds),
                        IngressDroppedBeforeJob = ingressDroppedBeforeJob
                    };
                    JobHandle handle = generateJob.Schedule();
                    DecayTraumaDecalOpacityJob decayJob = new DecayTraumaDecalOpacityJob
                    {
                        Decals = (TraumaDecalDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(instances),
                        State = (DecalRuntimeStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(stateArray),
                        DeltaTime = math.max(0f, math.isfinite(deltaTime) ? deltaTime : 0f),
                        DecayRate = decayRate,
                        DefaultLifetimeSeconds = math.max(0.1f, tuning.BaseFadeTimeSeconds),
                        Capacity = math.min(instances.Length, MaxCapacity)
                    };
                    handle = decayJob.Schedule(handle);
                    BuildDecalUploadBufferJob uploadJob = new BuildDecalUploadBufferJob
                    {
                        Decals = (TraumaDecalDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(instances),
                        Upload = (TraumaDecalDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(upload),
                        State = (DecalRuntimeStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(stateArray),
                        Capacity = math.min(instances.Length, MaxCapacity),
                        UploadCapacity = math.min(upload.Length, MaxCapacity),
                        MaxActiveDecals = maxActive
                    };
                    handle = uploadJob.Schedule(handle);
                    H8Memory.RegisterActiveJob(OwnerSystem, handle);

                    if (!DispatcherJobFence.TryFinalizeCompleted(ref handle))
                    {
                        if (!DispatcherJobFence.TryComplete(ref handle, forceComplete: true))
                        {
                            stats = _lastCompletedStats;
                            return false;
                        }
                    }

                    FinalizeCompletedVisualSync(
                        stateArray,
                        upload,
                        telemetry,
                        startTicks,
                        quality,
                        thermalPressure,
                        maxActive,
                        out stats,
                        ref faultFlags);
                }
                finally
                {
                    if (tuningLocked)
                        ReleaseDynamicDecalVaultBuffer(in _tuningHandle, DynamicDecalVaultBufferIds.Tuning);
                    if (telemetryLocked)
                        ReleaseDynamicDecalVaultBuffer(in _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing);
                    if (stateLocked)
                        ReleaseDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState);
                    if (uploadLocked)
                        ReleaseDynamicDecalVaultBuffer(in _uploadHandle, DynamicDecalVaultBufferIds.UploadScratch);
                    if (instancesLocked)
                        ReleaseDynamicDecalVaultBuffer(in _instancesHandle, DynamicDecalVaultBufferIds.Instances);
                    if (requestStateLocked)
                        ReleaseDynamicDecalVaultBuffer(in _requestStateHandle, DynamicDecalVaultBufferIds.RequestState);
                    if (requestRingLocked)
                        ReleaseDynamicDecalVaultBuffer(in _requestRingHandle, DynamicDecalVaultBufferIds.RequestRing);
                }

                if (faultFlags != 0u)
                    DumpBlackBox(faultFlags);

                return stats.UploadCount > 0;
            }
        }

        public static bool TryDrainPendingVisualSync(out DynamicDecalFrameStats stats)
        {
            stats = default;
            return _pendingVisualSyncActive && TryFinalizePendingVisualSync(out stats);
        }

        public static bool ForceCompletePendingVisualSync(out DynamicDecalFrameStats stats)
        {
            stats = default;
            if (!_pendingVisualSyncActive)
                return false;

            DispatcherJobFence.TryComplete(ref _pendingVisualSyncHandle, forceComplete: true);
            return TryFinalizePendingVisualSync(out stats);
        }

        private static bool TryFinalizePendingVisualSync(out DynamicDecalFrameStats stats)
        {
            stats = default;
            if (!_pendingVisualSyncActive)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingVisualSyncHandle))
            {
                stats = _lastCompletedStats;
                return false;
            }

            uint faultFlags = 0u;
            bool stateLocked = false;
            bool uploadLocked = false;
            bool telemetryLocked = false;
            try
            {
                if (!TryAcquireDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState, 1, out NativeArray<DecalRuntimeStateDTO> stateArray))
                    return false;
                stateLocked = true;
                if (!TryAcquireDynamicDecalVaultBuffer(in _uploadHandle, DynamicDecalVaultBufferIds.UploadScratch, MaxCapacity, out NativeArray<TraumaDecalDTO> upload))
                    return false;
                uploadLocked = true;
                if (!TryAcquireDynamicDecalVaultBuffer(in _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing, TelemetryCapacity, out NativeArray<TraumaWoundTelemetryEntry> telemetry))
                    return false;
                telemetryLocked = true;

                FinalizeCompletedVisualSync(
                    stateArray,
                    upload,
                    telemetry,
                    _pendingVisualSyncStartTicks,
                    _pendingVisualSyncQuality,
                    _pendingVisualSyncThermalPressure,
                    _pendingVisualSyncMaxActive,
                    out stats,
                    ref faultFlags);
            }
            finally
            {
                _pendingVisualSyncActive = false;
                _pendingVisualSyncStartTicks = 0L;
                _pendingVisualSyncQuality = 0f;
                _pendingVisualSyncThermalPressure = 0f;
                _pendingVisualSyncMaxActive = 0;
                if (telemetryLocked)
                    ReleaseDynamicDecalVaultBuffer(in _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing);
                if (uploadLocked)
                    ReleaseDynamicDecalVaultBuffer(in _uploadHandle, DynamicDecalVaultBufferIds.UploadScratch);
                if (stateLocked)
                    ReleaseDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState);
            }

            if (faultFlags != 0u)
                DumpBlackBox(faultFlags);

            return true;
        }

        private static void FinalizeCompletedVisualSync(
            NativeArray<DecalRuntimeStateDTO> stateArray,
            NativeArray<TraumaDecalDTO> upload,
            NativeArray<TraumaWoundTelemetryEntry> telemetry,
            long startTicks,
            float quality,
            float thermalPressure,
            int maxActive,
            out DynamicDecalFrameStats stats,
            ref uint faultFlags)
        {
            DecalRuntimeStateDTO state = stateArray[0];
            state.Frame = ResolveVisualFrameId();
            state.GlobalQualityWeight = quality;
            state.ThermalPressure01 = thermalPressure;
            state.MaxActiveThisFrame = maxActive;
            state.LastBallisticFrame = _lastIngestedBallisticFrame;
            if ((state.Flags & DynamicDecalFlags.NonFinite) != 0u)
                faultFlags |= RuntimeNonFiniteFaultFlag;

            double cpuUs = (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) *
                           1000000.0d /
                           System.Diagnostics.Stopwatch.Frequency;
            state.CpuMicroseconds = (float)math.max(0.0d, cpuUs);
            stateArray[0] = state;
            _lastRuntimeStateSnapshot = state;
            _hasRuntimeStateSnapshot = true;

            PushTelemetry(telemetry, in state, quality, thermalPressure);
            stats.UploadHandle = _uploadHandle;
            stats.UploadCapacity = math.min(upload.Length, MaxCapacity);
            stats.UploadCount = math.clamp(state.LastUploadCount, 0, math.min(upload.Length, MaxCapacity));
            stats.ActiveCount = math.max(0, state.ActiveCount);
            stats.NewCount = math.max(0, state.NewThisFrame);
            stats.MaxActiveCount = maxActive;
            stats.CpuMicroseconds = state.CpuMicroseconds;
            stats.UploadMicroseconds = state.UploadMicroseconds;
            stats.GlobalQualityWeight = quality;
            stats.ThermalPressure01 = thermalPressure;
            stats.NormalRefractionIntensity = math.max(0f, math.isfinite(state.NormalRefractionIntensity) ? state.NormalRefractionIntensity : 1f);
            _lastCompletedStats = stats;
            _hasLastCompletedStats = true;
        }

        public static void RecordGpuUploadMicroseconds(float uploadMicroseconds)
        {
            if (!IsInitializedForRead())
                return;

            bool uploadStall = false;
            bool stateLocked = false;
            bool telemetryLocked = false;
            try
            {
                if (!TryAcquireDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState, 1, out NativeArray<DecalRuntimeStateDTO> stateArray))
                    return;
                stateLocked = true;

                float safe = math.max(0f, math.isfinite(uploadMicroseconds) ? uploadMicroseconds : 0f);
                DecalRuntimeStateDTO state = stateArray[0];
                state.UploadMicroseconds = safe;
                uploadStall = safe > 300f;
                if (uploadStall)
                    state.Flags |= RuntimeUploadStallFlag;
                stateArray[0] = state;
                _lastRuntimeStateSnapshot = state;
                _hasRuntimeStateSnapshot = true;

                TryAcquireDynamicDecalVaultBuffer(in _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing, TelemetryCapacity, out NativeArray<TraumaWoundTelemetryEntry> telemetry);
                telemetryLocked = telemetry.IsCreated;
                int count = telemetry.IsCreated ? math.min(telemetry.Length, TelemetryCapacity) : 0;
                if (count > 0)
                {
                    int index = _telemetryCursor - 1;
                    if (index < 0)
                        index = count - 1;

                    TraumaWoundTelemetryEntry entry = telemetry[index];
                    entry.GpuUploadMicroseconds = safe;
                    entry.Flags = state.Flags;
                    telemetry[index] = entry;
                    _lastTelemetrySnapshot = entry;
                    _hasTelemetrySnapshot = true;
                }
            }
            finally
            {
                if (telemetryLocked)
                    ReleaseDynamicDecalVaultBuffer(in _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing);
                if (stateLocked)
                    ReleaseDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState);
            }

            if (uploadStall)
                DumpBlackBox(RuntimeUploadStallFlag);
        }

        public static bool TryGetTuning(out DecalTuningDTO tuning)
        {
            tuning = _lastTuningSnapshot;
            return _hasTuningSnapshot;
        }

        public static bool WriteTuning(in DecalTuningDTO tuning)
        {
            if (!EnsureInitialized())
                return false;

            bool tuningLocked = false;
            try
            {
                if (!TryAcquireDynamicDecalVaultBuffer(in _tuningHandle, DynamicDecalVaultBufferIds.Tuning, 1, out NativeArray<DecalTuningDTO> tuningArray))
                    return false;
                tuningLocked = true;

                float fallbackCapacityFloat = math.isfinite(tuning.MaximumOverkillCapacity)
                    ? math.clamp(tuning.MaximumOverkillCapacity, LowCapacity, MaxCapacity)
                    : MaxCapacity;
                DecalTuningDTO sanitized = SanitizeTuning(tuning, tuning.BaseFadeTimeSeconds, (int)math.round(fallbackCapacityFloat));
                sanitized.Revision = tuning.Revision == uint.MaxValue ? 1u : tuning.Revision + 1u;
                tuningArray[0] = sanitized;
                _lastTuningSnapshot = sanitized;
                _hasTuningSnapshot = true;
                return true;
            }
            finally
            {
                if (tuningLocked)
                    ReleaseDynamicDecalVaultBuffer(in _tuningHandle, DynamicDecalVaultBufferIds.Tuning);
            }
        }

        public static bool TryGetRuntimeState(out DecalRuntimeStateDTO state)
        {
            state = _lastRuntimeStateSnapshot;
            return _hasRuntimeStateSnapshot;
        }

        public static bool TryGetLatestTelemetry(out TraumaWoundTelemetryEntry entry)
        {
            entry = _lastTelemetrySnapshot;
            return _hasTelemetrySnapshot;
        }

#if UNITY_EDITOR
        public static bool TryAcquireDecalBufferRead(
            out NativeArray<TraumaDecalDTO>.ReadOnly decals,
            out int activeCount,
            out Vector3 cameraWorldPosition)
        {
            decals = default;
            activeCount = 0;
            cameraWorldPosition = _lastCameraWorldPosition;
            if (!IsInitializedForRead())
                return false;

            if (!TryReadDynamicDecalVaultBuffer(in _instancesHandle, DynamicDecalVaultBufferIds.Instances, MaxCapacity, out decals) ||
                !TryReadDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState, 1, out NativeArray<DecalRuntimeStateDTO>.ReadOnly stateArray))
            {
                decals = default;
                return false;
            }

            DecalRuntimeStateDTO state = stateArray[0];
            activeCount = math.clamp(state.ActiveCount, 0, math.min(decals.Length, MaxCapacity));
            return true;
        }

        public static void ReleaseDecalBufferRead()
        {
        }
#endif

#if UNITY_EDITOR
        public static unsafe bool TryLoadMaterialProfilesCsv(string csvPath, out int profilesWritten)
        {
            profilesWritten = 0;
            if (!EnsureInitialized() || string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                return false;

            try
            {
                int bytesRead = 0;
                Span<byte> scratch = stackalloc byte[CsvScratchBytes];
                using FileStream stream = File.OpenRead(csvPath);
                long streamLength = stream.Length;
                if (streamLength <= 0L || streamLength > scratch.Length)
                    return false;

                int expectedBytes = (int)streamLength;
                while (bytesRead < expectedBytes)
                {
                    Span<byte> readSpan = scratch.Slice(bytesRead, expectedBytes - bytesRead);
                    int read = stream.Read(readSpan);
                    if (read <= 0)
                        return false;

                    bytesRead += read;
                }

                if (!TryAcquireDynamicDecalVaultBuffer(in _materialProfileHandle, DynamicDecalVaultBufferIds.MaterialProfiles, MaxMaterialProfiles, out NativeArray<DecalMaterialProfileDTO> profiles))
                    return false;

                try
                {
                    profilesWritten = ParseMaterialProfilesCsv(scratch.Slice(0, bytesRead), profiles);
                    _materialProfileCount = profilesWritten;
                    return profilesWritten > 0;
                }
                finally
                {
                    ReleaseDynamicDecalVaultBuffer(in _materialProfileHandle, DynamicDecalVaultBufferIds.MaterialProfiles);
                }
            }
            catch (IOException)
            {
                profilesWritten = 0;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                profilesWritten = 0;
                return false;
            }
            catch (ObjectDisposedException)
            {
                profilesWritten = 0;
                return false;
            }
            catch (InvalidOperationException)
            {
                profilesWritten = 0;
                return false;
            }
            catch (ArgumentException)
            {
                profilesWritten = 0;
                return false;
            }
            catch (NotSupportedException)
            {
                profilesWritten = 0;
                return false;
            }
        }
#endif

        public static int GetLoadedMaterialProfileCount()
        {
            return math.max(0, _materialProfileCount);
        }

#if UNITY_EDITOR
        public static int ParseMaterialProfilesCsv(ReadOnlySpan<byte> csv, NativeArray<DecalMaterialProfileDTO> profiles)
        {
            if (csv.Length <= 0 || !profiles.IsCreated || profiles.Length <= 0)
                return 0;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            int cursor = 0;
            int count = 0;
            while (TryReadLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = TrimAscii(line);
                if (line.Length <= 0 || line[0] == (byte)'#')
                    continue;

                if (!TryReadField(line, 0, out ReadOnlySpan<byte> sourceToken) || IsHeaderToken(sourceToken))
                    continue;

                DecalMaterialProfileDTO profile = default;
                profile.SourceHash = TryParseUInt(sourceToken, out uint sourceHash)
                    ? sourceHash
                    : HashLowerAscii(sourceToken);
                if (profile.SourceHash == 0u)
                    profile.SourceHash = HashLowerAscii(sourceToken);
                profile.AtlasSlice = TryReadField(line, 1, out ReadOnlySpan<byte> sliceToken) &&
                                     TryParseUInt(sliceToken, out uint slice)
                    ? slice % AtlasSliceCount
                    : DynamicDecalMaterialHashes.Scorch;
                profile.LifetimeSeconds = TryReadField(line, 2, out ReadOnlySpan<byte> lifetimeToken) &&
                                          TryParseFloat(lifetimeToken, out float lifetime)
                    ? math.clamp(lifetime, 0.1f, 60f)
                    : 7.5f;
                profile.RadiusMeters = TryReadField(line, 3, out ReadOnlySpan<byte> radiusToken) &&
                                       TryParseFloat(radiusToken, out float radius)
                    ? math.clamp(radius, 0.025f, 8f)
                    : 0.55f;
                profile.ProjectionDepthMeters = TryReadField(line, 4, out ReadOnlySpan<byte> depthToken) &&
                                                TryParseFloat(depthToken, out float depth)
                    ? math.clamp(depth, 0.025f, 2f)
                    : 0.18f;
                profile.Flags = DynamicDecalFlags.Active;
                if (TryInsertMaterialProfile(profiles, in profile))
                    count++;
            }

            return count;
        }
#endif

        private static bool TryInsertMaterialProfile(NativeArray<DecalMaterialProfileDTO> profiles, in DecalMaterialProfileDTO profile)
        {
            int capacity = profiles.IsCreated ? profiles.Length : 0;
            if (capacity <= 0 || profile.SourceHash == 0u)
                return false;

            uint mask = (uint)(capacity - 1);
            int slot = capacity > 0 && (capacity & (capacity - 1)) == 0
                ? (int)(profile.SourceHash & mask)
                : (int)(profile.SourceHash % (uint)capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = slot + probe;
                if (index >= capacity)
                    index -= capacity;

                DecalMaterialProfileDTO existing = profiles[index];
                if (existing.SourceHash != 0u && existing.SourceHash != profile.SourceHash)
                    continue;

                profiles[index] = profile;
                return existing.SourceHash == 0u;
            }

            return false;
        }

        private static bool EnsureInitialized()
        {
            if (!_coldRoutesCached && !WarmupColdGlobalRoutes())
                return false;

            IDataVault resolvedVault = _vault;
            if (resolvedVault == null)
                return false;
            if (resolvedVault.IsCompactionFenceActive)
                return false;

            if (ReferenceEquals(_vault, resolvedVault) &&
                HasDynamicDecalVaultBuffer(_vault, in _instancesHandle, DynamicDecalVaultBufferIds.Instances, MaxCapacity) &&
                HasDynamicDecalVaultBuffer(_vault, in _uploadHandle, DynamicDecalVaultBufferIds.UploadScratch, MaxCapacity) &&
                HasDynamicDecalVaultBuffer(_vault, in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState, 1) &&
                HasDynamicDecalVaultBuffer(_vault, in _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing, TelemetryCapacity) &&
                HasDynamicDecalVaultBuffer(_vault, in _tuningHandle, DynamicDecalVaultBufferIds.Tuning, 1) &&
                HasDynamicDecalVaultBuffer(_vault, in _materialProfileHandle, DynamicDecalVaultBufferIds.MaterialProfiles, MaxMaterialProfiles) &&
                HasDynamicDecalVaultBuffer(_vault, in _csvScratchHandle, DynamicDecalVaultBufferIds.CsvScratch, CsvScratchBytes) &&
                HasDynamicDecalVaultBuffer(_vault, in _requestRingHandle, DynamicDecalVaultBufferIds.RequestRing, RequestRingCapacity) &&
                HasDynamicDecalVaultBuffer(_vault, in _requestStateHandle, DynamicDecalVaultBufferIds.RequestState, 1))
            {
                SeedDefaultTuning();
                SeedRequestQueueState();
                SeedColdRuntimeState();
                return true;
            }

            ReleaseDynamicDecalVaultHandles(_vault);
            _vault = resolvedVault;
            bool ready =
                EnsureDynamicDecalVaultBuffer(
                    ref _instancesHandle,
                    DynamicDecalVaultBufferIds.Instances,
                    MaxCapacity,
                    NativeArrayOptions.ClearMemory,
                    out _) &&
                EnsureDynamicDecalVaultBuffer(
                    ref _uploadHandle,
                    DynamicDecalVaultBufferIds.UploadScratch,
                    MaxCapacity,
                    NativeArrayOptions.ClearMemory,
                    out _) &&
                EnsureDynamicDecalVaultBuffer(
                    ref _stateHandle,
                    DynamicDecalVaultBufferIds.RuntimeState,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out _) &&
                EnsureDynamicDecalVaultBuffer(
                    ref _telemetryHandle,
                    DynamicDecalVaultBufferIds.TelemetryRing,
                    TelemetryCapacity,
                    NativeArrayOptions.ClearMemory,
                    out _) &&
                EnsureDynamicDecalVaultBuffer(
                    ref _tuningHandle,
                    DynamicDecalVaultBufferIds.Tuning,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out _) &&
                EnsureDynamicDecalVaultBuffer(
                    ref _materialProfileHandle,
                    DynamicDecalVaultBufferIds.MaterialProfiles,
                    MaxMaterialProfiles,
                    NativeArrayOptions.ClearMemory,
                    out _) &&
                EnsureDynamicDecalVaultBuffer(
                    ref _csvScratchHandle,
                    DynamicDecalVaultBufferIds.CsvScratch,
                    CsvScratchBytes,
                    NativeArrayOptions.UninitializedMemory,
                    out _) &&
                EnsureDynamicDecalVaultBuffer(
                    ref _requestRingHandle,
                    DynamicDecalVaultBufferIds.RequestRing,
                    RequestRingCapacity,
                    NativeArrayOptions.ClearMemory,
                    out _) &&
                EnsureDynamicDecalVaultBuffer(
                    ref _requestStateHandle,
                    DynamicDecalVaultBufferIds.RequestState,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out _);

            if (!ready)
            {
                ReleaseDynamicDecalVaultHandles(_vault);
                return false;
            }

            SeedDefaultTuning();
            SeedRequestQueueState();
            SeedColdRuntimeState();
            return true;
        }

        private static bool EnsureDynamicDecalVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T>.ReadOnly buffer) where T : unmanaged
        {
            buffer = default;
            if (_vault == null || _vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (IsDynamicDecalVaultHandle(in handle, bufferId) &&
                TryReadDynamicDecalVaultBuffer(in handle, bufferId, requiredLength, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (_vault.IsAllocationLocked)
                return false;

            handle = _vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystem,
                options);
            if (TryReadDynamicDecalVaultBuffer(in handle, bufferId, requiredLength, out buffer))
                return true;

            ReleaseDynamicDecalVaultHandle(_vault, ref handle, bufferId);
            buffer = default;
            return false;
        }

        private static bool TryReadDynamicDecalVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : unmanaged
        {
            buffer = default;
            if (_vault == null || _vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            return IsDynamicDecalVaultHandle(in handle, bufferId) &&
                   _vault.TryReadOnlyHandle(in handle, out buffer) &&
                   !_vault.IsCompactionFenceActive &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryAcquireDynamicDecalVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : unmanaged
        {
            buffer = default;
            if (_vault == null || _vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (!IsDynamicDecalVaultHandle(in handle, bufferId) ||
                !_vault.TryAcquireWriteLock(in handle, OwnerSystem, out buffer) ||
                _vault.IsCompactionFenceActive ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                ReleaseDynamicDecalVaultBuffer(in handle, bufferId);
                buffer = default;
                return false;
            }

            return true;
        }

        private static void ReleaseDynamicDecalVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : unmanaged
        {
            if (_vault != null && IsDynamicDecalVaultHandle(in handle, bufferId))
                _vault.ReleaseWriteLock(in handle, OwnerSystem);
        }

        private static void ReleaseAllDynamicDecalWriteLocks()
        {
            ReleaseDynamicDecalVaultBuffer(in _materialProfileHandle, DynamicDecalVaultBufferIds.MaterialProfiles);
            ReleaseDynamicDecalVaultBuffer(in _tuningHandle, DynamicDecalVaultBufferIds.Tuning);
            ReleaseDynamicDecalVaultBuffer(in _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing);
            ReleaseDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState);
            ReleaseDynamicDecalVaultBuffer(in _uploadHandle, DynamicDecalVaultBufferIds.UploadScratch);
            ReleaseDynamicDecalVaultBuffer(in _instancesHandle, DynamicDecalVaultBufferIds.Instances);
            ReleaseDynamicDecalVaultBuffer(in _requestStateHandle, DynamicDecalVaultBufferIds.RequestState);
            ReleaseDynamicDecalVaultBuffer(in _requestRingHandle, DynamicDecalVaultBufferIds.RequestRing);
        }

        private static bool HasDynamicDecalVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : unmanaged
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsDynamicDecalVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   !vault.IsCompactionFenceActive &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsDynamicDecalVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private static bool TryGetDynamicDecalElementPtr<T>(NativeArray<T> buffer, int index, out void* elementPtr) where T : unmanaged
        {
            elementPtr = null;
            if (!buffer.IsCreated || (uint)index >= (uint)buffer.Length)
                return false;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
            elementPtr = (byte*)ptr + (UnsafeUtility.SizeOf<T>() * index);
            return true;
        }

        private static void ReleaseDynamicDecalVaultHandles(IDataVault vault)
        {
            ReleaseDynamicDecalVaultHandle(vault, ref _instancesHandle, DynamicDecalVaultBufferIds.Instances);
            ReleaseDynamicDecalVaultHandle(vault, ref _uploadHandle, DynamicDecalVaultBufferIds.UploadScratch);
            ReleaseDynamicDecalVaultHandle(vault, ref _stateHandle, DynamicDecalVaultBufferIds.RuntimeState);
            ReleaseDynamicDecalVaultHandle(vault, ref _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing);
            ReleaseDynamicDecalVaultHandle(vault, ref _tuningHandle, DynamicDecalVaultBufferIds.Tuning);
            ReleaseDynamicDecalVaultHandle(vault, ref _materialProfileHandle, DynamicDecalVaultBufferIds.MaterialProfiles);
            ReleaseDynamicDecalVaultHandle(vault, ref _csvScratchHandle, DynamicDecalVaultBufferIds.CsvScratch);
            ReleaseDynamicDecalVaultHandle(vault, ref _requestRingHandle, DynamicDecalVaultBufferIds.RequestRing);
            ReleaseDynamicDecalVaultHandle(vault, ref _requestStateHandle, DynamicDecalVaultBufferIds.RequestState);
        }

        private static void ReleaseDynamicDecalVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : unmanaged
        {
            if (vault != null && IsDynamicDecalVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static void SeedDefaultTuning()
        {
            if (!TryAcquireDynamicDecalVaultBuffer(in _tuningHandle, DynamicDecalVaultBufferIds.Tuning, 1, out NativeArray<DecalTuningDTO> tuningArray))
                return;

            try
            {
                DecalTuningDTO tuning = tuningArray[0];
                if (tuning.Revision != 0u)
                {
                    _lastTuningSnapshot = SanitizeTuning(tuning, ResolveDefaultTuning().BaseFadeTimeSeconds, MaxCapacity);
                    _hasTuningSnapshot = true;
                    return;
                }

                tuning.BaseFadeTimeSeconds = 7.5f;
                tuning.MaximumOverkillCapacity = MaxCapacity;
                tuning.NormalRefractionIntensity = 1f;
                tuning.ProjectionDepthMeters = 0.18f;
                tuning.LowTierCapacity = LowCapacity;
                tuning.BaseRadiusMeters = 0.55f;
                tuning.Revision = 1u;
                tuning.Flags = 0u;
                tuningArray[0] = tuning;
                _lastTuningSnapshot = tuning;
                _hasTuningSnapshot = true;
            }
            finally
            {
                ReleaseDynamicDecalVaultBuffer(in _tuningHandle, DynamicDecalVaultBufferIds.Tuning);
            }
        }

        private static void SeedRequestQueueState()
        {
            if (!TryAcquireDynamicDecalVaultBuffer(in _requestStateHandle, DynamicDecalVaultBufferIds.RequestState, 1, out NativeArray<DecalRequestQueueStateDTO> stateArray))
                return;

            try
            {
                DecalRequestQueueStateDTO state = SanitizeRequestQueueState(stateArray[0], RequestRingCapacity);
                state.Capacity = RequestRingCapacity;
                stateArray[0] = state;
            }
            finally
            {
                ReleaseDynamicDecalVaultBuffer(in _requestStateHandle, DynamicDecalVaultBufferIds.RequestState);
            }
        }

        private static void SeedColdRuntimeState()
        {
            if (!TryAcquireDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState, 1, out NativeArray<DecalRuntimeStateDTO> stateArray))
                return;

            try
            {
                DecalRuntimeStateDTO state = stateArray[0];
                if ((state.Flags & RuntimeInitializedFlag) != 0u)
                {
                    _lastRuntimeStateSnapshot = state;
                    _hasRuntimeStateSnapshot = true;
                    return;
                }

                ClearColdVisualBuffers();
                state = CreateInitializedRuntimeState(
                    ResolveGlobalQualityWeight(),
                    ResolveThermalPressure01(),
                    ResolveLiveTuning());
                stateArray[0] = state;
                _lastRuntimeStateSnapshot = state;
                _hasRuntimeStateSnapshot = true;
            }
            finally
            {
                ReleaseDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState);
            }
        }

        private static DecalRuntimeStateDTO CreateInitializedRuntimeState(
            float quality,
            float thermalPressure,
            DecalTuningDTO tuning)
        {
            float safeQuality = math.saturate(math.isfinite(quality) ? quality : 0f);
            float safeThermal = math.saturate(math.isfinite(thermalPressure) ? thermalPressure : 0f);
            DecalRuntimeStateDTO state = default;
            state.Flags = RuntimeInitializedFlag;
            state.GlobalQualityWeight = safeQuality;
            state.ThermalPressure01 = safeThermal;
            state.MaxActiveThisFrame = ResolveMaxActiveDecals(safeQuality, tuning);
            state.NormalRefractionIntensity = math.clamp(
                math.isfinite(tuning.NormalRefractionIntensity) ? tuning.NormalRefractionIntensity : ResolveDefaultTuning().NormalRefractionIntensity,
                0f,
                2.5f);
            return state;
        }

        private static void ClearColdVisualBuffers()
        {
            ClearColdVaultBuffer(ref _instancesHandle, DynamicDecalVaultBufferIds.Instances, MaxCapacity);
            ClearColdVaultBuffer(ref _uploadHandle, DynamicDecalVaultBufferIds.UploadScratch, MaxCapacity);
        }

        private static void ClearColdVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : unmanaged
        {
            if (!TryAcquireDynamicDecalVaultBuffer(in handle, bufferId, requiredLength, out NativeArray<T> buffer))
                return;

            try
            {
                ClearNativeBuffer(buffer, requiredLength);
            }
            finally
            {
                ReleaseDynamicDecalVaultBuffer(in handle, bufferId);
            }
        }

        private static void ClearNativeBuffer<T>(
            NativeArray<T> buffer,
            int requiredLength) where T : unmanaged
        {
            int safeLength = math.min(buffer.IsCreated ? buffer.Length : 0, requiredLength);
            if (safeLength <= 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
            UnsafeUtility.MemClear(ptr, (long)UnsafeUtility.SizeOf<T>() * safeLength);
        }

        private static DecalTuningDTO ResolveDefaultTuning()
        {
            DecalTuningDTO tuning = default;
            tuning.BaseFadeTimeSeconds = 7.5f;
            tuning.MaximumOverkillCapacity = MaxCapacity;
            tuning.NormalRefractionIntensity = 1f;
            tuning.ProjectionDepthMeters = 0.18f;
            tuning.LowTierCapacity = LowCapacity;
            tuning.BaseRadiusMeters = 0.55f;
            return tuning;
        }

        private static DecalTuningDTO ResolveLiveTuning()
        {
            return _hasTuningSnapshot ? _lastTuningSnapshot : ResolveDefaultTuning();
        }

        private static DecalTuningDTO SanitizeTuning(DecalTuningDTO tuning, float fallbackFadeSeconds, int fallbackCapacity)
        {
            DecalTuningDTO defaults = ResolveDefaultTuning();
            tuning.BaseFadeTimeSeconds = math.clamp(
                math.isfinite(tuning.BaseFadeTimeSeconds) && tuning.BaseFadeTimeSeconds > 0f ? tuning.BaseFadeTimeSeconds : fallbackFadeSeconds,
                0.25f,
                60f);
            if (!math.isfinite(tuning.BaseFadeTimeSeconds) || tuning.BaseFadeTimeSeconds <= 0f)
                tuning.BaseFadeTimeSeconds = defaults.BaseFadeTimeSeconds;
            float requestedMaxCapacity = math.clamp(math.max(fallbackCapacity, LowCapacity), LowCapacity, MaxCapacity);
            float desiredMaxCapacity = math.isfinite(tuning.MaximumOverkillCapacity) && tuning.MaximumOverkillCapacity > 0f
                ? tuning.MaximumOverkillCapacity
                : requestedMaxCapacity;
            tuning.MaximumOverkillCapacity = math.clamp(
                desiredMaxCapacity,
                LowCapacity,
                requestedMaxCapacity);
            tuning.NormalRefractionIntensity = math.clamp(
                math.isfinite(tuning.NormalRefractionIntensity) ? tuning.NormalRefractionIntensity : defaults.NormalRefractionIntensity,
                0f,
                2.5f);
            tuning.ProjectionDepthMeters = math.clamp(
                math.isfinite(tuning.ProjectionDepthMeters) && tuning.ProjectionDepthMeters > 0f ? tuning.ProjectionDepthMeters : defaults.ProjectionDepthMeters,
                0.025f,
                2.0f);
            tuning.LowTierCapacity = math.clamp(
                math.isfinite(tuning.LowTierCapacity) && tuning.LowTierCapacity > 0f ? tuning.LowTierCapacity : LowCapacity,
                LowCapacity,
                tuning.MaximumOverkillCapacity);
            tuning.BaseRadiusMeters = math.clamp(
                math.isfinite(tuning.BaseRadiusMeters) && tuning.BaseRadiusMeters > 0f ? tuning.BaseRadiusMeters : defaults.BaseRadiusMeters,
                0.025f,
                8f);
            return tuning;
        }

        public static bool TryResolveUploadBuffer(
            in DynamicDecalFrameStats stats,
            out NativeArray<TraumaDecalDTO>.ReadOnly upload)
        {
            upload = default;
            int requiredLength = math.clamp(stats.UploadCount, 0, math.min(stats.UploadCapacity, MaxCapacity));
            return requiredLength > 0 &&
                   _vault != null &&
                   !_vault.IsCompactionFenceActive &&
                   IsDynamicDecalVaultHandle(in stats.UploadHandle, DynamicDecalVaultBufferIds.UploadScratch) &&
                   _vault.TryReadOnlyHandle(in stats.UploadHandle, out upload) &&
                   upload.IsCreated &&
                   upload.Length >= requiredLength;
        }

        private static void TryIngestGlobalImpactSignals()
        {
            uint frameId = ResolveVisualFrameId();
            if (_lastSignalSnapshotFrameId == frameId)
                return;

            _lastSignalSnapshotFrameId = frameId;
            uint maxHighSpeedFrame = _lastIngestedHighSpeedFrame;
            uint maxCombatDamageFrame = _lastIngestedCombatDamageFrame;
            bool highSpeedAccepted = false;
            bool combatDamageAccepted = false;
            DecalTuningDTO tuning = ResolveLiveTuning();
            NativeArray<DecalMaterialProfileDTO>.ReadOnly materialProfiles = default;
            int materialProfileCapacity = 0;
            if (_materialProfileCount > 0 &&
                TryReadDynamicDecalVaultBuffer(in _materialProfileHandle, DynamicDecalVaultBufferIds.MaterialProfiles, MaxMaterialProfiles, out materialProfiles))
            {
                materialProfileCapacity = materialProfiles.IsCreated ? materialProfiles.Length : 0;
            }

            ReadOnlySpan<HighSpeedImpactSignal> highSpeed = SignalBus<HighSpeedImpactSignal>.GetFrameSnapshot();
            for (int i = 0; i < highSpeed.Length; i++)
            {
                ref readonly HighSpeedImpactSignal signal = ref highSpeed[i];
                uint frame = signal.Frame;
                double3 impactAup = ToDouble3(
                    signal.PointAup.GridX,
                    signal.PointAup.GridY,
                    signal.PointAup.GridZ,
                    signal.PointAup.LocalX,
                    signal.PointAup.LocalY,
                    signal.PointAup.LocalZ);
                if ((_hasIngestedHighSpeedFrame && frame <= _lastIngestedHighSpeedFrame) || !math.all(math.isfinite(impactAup)))
                    continue;

                uint materialHash = signal.MaterialHash != 0u
                    ? signal.MaterialHash
                    : HighSpeedImpactSignal.ComposeMaterialHash(signal.TargetHash, signal.PrimaryMaterialId, signal.SecondaryMaterialId);
                float speed = math.max(0f, math.isfinite(signal.ImpactSpeed) ? signal.ImpactSpeed : 0f);
                float energy = math.max(0f, math.isfinite(signal.KineticEnergy) ? signal.KineticEnergy : signal.LostKineticEnergy);
                EnqueueSignalImpact(
                    impactAup,
                    signal.Normal,
                    materialHash,
                    signal.SourceHash ^ signal.TargetHash,
                    tuning,
                    materialProfiles,
                    materialProfileCapacity,
                    energy * 0.0125f,
                    speed,
                    frame,
                    DynamicDecalFlags.Ballistic);
                maxHighSpeedFrame = math.max(maxHighSpeedFrame, frame);
                highSpeedAccepted = true;
            }

            ReadOnlySpan<CombatDamageSignal> damage = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            for (int i = 0; i < damage.Length; i++)
            {
                ref readonly CombatDamageSignal signal = ref damage[i];
                uint frame = signal.Frame;
                if ((_hasIngestedCombatDamageFrame && frame <= _lastIngestedCombatDamageFrame) || !math.all(math.isfinite(signal.ImpactAup)))
                    continue;

                float3 normal = -SanitizeNormal(signal.Direction, MakeFloat3(0f, 1f, 0f));
                uint materialHash = signal.DamageType != 0u ? signal.DamageType : signal.SourceHash;
                uint flags = materialHash == DynamicDecalMaterialHashes.HullDent
                    ? DynamicDecalFlags.HullImpact
                    : DynamicDecalFlags.Ballistic;
                if (materialHash == DynamicDecalMaterialHashes.GlassCrack)
                    flags |= DynamicDecalFlags.PersistentGlass;
                EnqueueSignalImpact(
                    signal.ImpactAup,
                    normal,
                    materialHash,
                    signal.SourceHash,
                    tuning,
                    materialProfiles,
                    materialProfileCapacity,
                    signal.Magnitude,
                    signal.Magnitude,
                    frame,
                    flags);
                maxCombatDamageFrame = math.max(maxCombatDamageFrame, frame);
                combatDamageAccepted = true;
            }

            if (highSpeedAccepted)
            {
                _lastIngestedHighSpeedFrame = maxHighSpeedFrame;
                _hasIngestedHighSpeedFrame = true;
            }

            if (combatDamageAccepted)
            {
                _lastIngestedCombatDamageFrame = maxCombatDamageFrame;
                _hasIngestedCombatDamageFrame = true;
            }

            _lastIngestedBallisticFrame = math.max(maxHighSpeedFrame, maxCombatDamageFrame);
        }

        private static void EnqueueSignalImpact(
            double3 impactAup,
            float3 normal,
            uint materialHash,
            uint profileHash,
            DecalTuningDTO tuning,
            NativeArray<DecalMaterialProfileDTO>.ReadOnly materialProfiles,
            int materialProfileCapacity,
            float damage,
            float velocity,
            uint frame,
            uint flags)
        {
            DecalRequestSignal request = default;
            request.ImpactAup = impactAup;
            request.Normal = SanitizeNormal(normal, MakeFloat3(0f, 1f, 0f));
            if (TryResolveMaterialProfile(
                    profileHash != 0u ? profileHash : materialHash,
                    materialProfiles,
                    materialProfileCapacity,
                    out DecalMaterialProfileDTO profile))
            {
                request.RadiusMeters = profile.RadiusMeters;
                request.ProjectionDepthMeters = profile.ProjectionDepthMeters;
                request.LifetimeSeconds = profile.LifetimeSeconds;
                request.MaterialHash = PackRequestMaterialPayload(ResolveDecalTypeFromMaterial(materialHash), profile.AtlasSlice);
            }
            else
            {
                request.RadiusMeters = ResolveBallisticRadius(damage, velocity, tuning);
                request.ProjectionDepthMeters = tuning.ProjectionDepthMeters;
                request.LifetimeSeconds = ResolveBallisticLifetime(materialHash, damage, tuning);
                request.MaterialHash = PackRequestMaterialPayload(ResolveDecalTypeFromMaterial(materialHash), ResolveAtlasSliceFromMaterial(materialHash));
            }

            request.Flags = DynamicDecalFlags.Active | flags;
            if (ResolveDecalTypeFromMaterial(materialHash) == DynamicDecalMaterialHashes.GlassCrack)
                request.Flags |= DynamicDecalFlags.PersistentGlass;
            request.StableSeed = Mix(materialHash ^ profileHash ^ frame);
            request.SourceFrame = frame;
            TryEnqueueRequest(in request);
        }

        private static bool TryEnqueueRequest(in DecalRequestSignal request)
        {
            if (!IsInitializedForRead())
                return false;

            if (_pendingVisualSyncActive)
            {
                AccumulateDroppedIngress(1);
                return false;
            }

            bool requestRingLocked = false;
            bool requestStateLocked = false;
            try
            {
                if (!TryAcquireDynamicDecalVaultBuffer(in _requestRingHandle, DynamicDecalVaultBufferIds.RequestRing, RequestRingCapacity, out NativeArray<DecalRequestSignal> requestRing))
                {
                    AccumulateDroppedIngress(1);
                    return false;
                }

                requestRingLocked = true;
                if (!TryAcquireDynamicDecalVaultBuffer(in _requestStateHandle, DynamicDecalVaultBufferIds.RequestState, 1, out NativeArray<DecalRequestQueueStateDTO> requestStateArray))
                {
                    AccumulateDroppedIngress(1);
                    return false;
                }

                requestStateLocked = true;
                return TryEnqueueRequestToBuffers(requestRing, requestStateArray, in request);
            }
            finally
            {
                if (requestStateLocked)
                    ReleaseDynamicDecalVaultBuffer(in _requestStateHandle, DynamicDecalVaultBufferIds.RequestState);
                if (requestRingLocked)
                    ReleaseDynamicDecalVaultBuffer(in _requestRingHandle, DynamicDecalVaultBufferIds.RequestRing);
            }
        }

        private static bool TryEnqueueRequestToBuffers(
            NativeArray<DecalRequestSignal> requestRing,
            NativeArray<DecalRequestQueueStateDTO> requestStateArray,
            in DecalRequestSignal request)
        {
            if (!requestRing.IsCreated || !requestStateArray.IsCreated || requestStateArray.Length <= 0)
            {
                AccumulateDroppedIngress(1);
                return false;
            }

            DecalRequestQueueStateDTO queueState = SanitizeRequestQueueState(requestStateArray[0], requestRing.Length);
            if (queueState.PendingCount >= queueState.Capacity)
            {
                AccumulateDroppedIngress(1);
                queueState.DroppedTotal++;
                requestStateArray[0] = queueState;
                return false;
            }

            int writeIndex = queueState.WriteIndex;
            requestRing[writeIndex] = request;
            queueState.WriteIndex = WrapRequestIndex(writeIndex + 1, queueState.Capacity);
            queueState.PendingCount++;
            queueState.EnqueuedTotal++;
            queueState.LastFrame = request.SourceFrame;
            requestStateArray[0] = queueState;
            return true;
        }

        private static void AccumulateDroppedIngress(int dropped)
        {
            if (dropped <= 0)
                return;

            int current = math.max(0, _droppedIngressThisFrame);
            int remaining = int.MaxValue - current;
            _droppedIngressThisFrame = current + math.min(dropped, remaining);
        }

        private static double3 ToDouble3(
            long gridX,
            long gridY,
            long gridZ,
            float localX,
            float localY,
            float localZ)
        {
            const double cellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersDouble;
            return MakeDouble3(
                (gridX * cellSizeMeters) + localX,
                (gridY * cellSizeMeters) + localY,
                (gridZ * cellSizeMeters) + localZ);
        }

        private static DecalRequestQueueStateDTO SanitizeRequestQueueState(DecalRequestQueueStateDTO state, int capacity)
        {
            int safeCapacity = math.clamp(capacity, 1, RequestRingCapacity);
            state.Capacity = safeCapacity;
            state.WriteIndex = WrapRequestIndex(state.WriteIndex, safeCapacity);
            state.ReadIndex = WrapRequestIndex(state.ReadIndex, safeCapacity);
            state.PendingCount = math.clamp(state.PendingCount, 0, safeCapacity);
            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WrapRequestIndex(int index, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            while (index >= safeCapacity)
                index -= safeCapacity;
            while (index < 0)
                index += safeCapacity;
            return index;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 0f);
        }

        private static float ResolveEffectiveQualityWeight(
            float targetQuality,
            float previousQuality,
            float deltaTime,
            float thermalPressure,
            bool hasPrevious)
        {
            float target = math.saturate(math.isfinite(targetQuality) ? targetQuality : 0f);
            if (!hasPrevious || !math.isfinite(previousQuality))
                return target;

            float previous = math.saturate(previousQuality);
            float safeDelta = math.clamp(math.isfinite(deltaTime) ? deltaTime : 0.016f, 0f, 0.1f);
            float response = math.lerp(2.5f, 9.0f, Smooth01(math.saturate(thermalPressure)));
            float blend = math.saturate(safeDelta * response);
            return math.saturate(math.lerp(previous, target, blend));
        }

        private static float ResolveThermalPressure01()
        {
            if (HomeostasisBrain.TryGetHardwareDictatorSnapshot(out SystemHealthDTO health, out ScalabilityStateDTO state))
                return math.saturate(math.max(health.ThermalIndex, math.max(health.VramPressure, state.ThermalIndex)));

            return math.saturate(HomeostasisBrain.SystemHealthIndex01);
        }

        private static int ResolveMaxActiveDecals(float quality, DecalTuningDTO tuning)
        {
            float q = Smooth01(math.saturate(quality));
            float low = math.clamp(tuning.LowTierCapacity, LowCapacity, MaxCapacity);
            float high = math.clamp(tuning.MaximumOverkillCapacity, low, MaxCapacity);
            return math.clamp((int)math.round(math.lerp(low, high, q)), 1, MaxCapacity);
        }

        private static float ResolveDecayRate(float deltaTime, float quality, float thermalPressure, DecalTuningDTO tuning)
        {
            float baseLifetime = math.max(0.1f, tuning.BaseFadeTimeSeconds);
            float pressureBoost = math.lerp(2.75f, 0.65f, Smooth01(quality)) + (math.saturate(thermalPressure) * 3.5f);
            return math.max(0.01f, pressureBoost * math.rcp(baseLifetime)) * math.max(0f, math.isfinite(deltaTime) ? 1f : 2f);
        }

        private static double3 ResolveCameraAup(Camera camera)
        {
            if (camera != null)
            {
                Vector3 position = camera.transform.position;
                if (IsFinite(position))
                {
                    var originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
                    if (originAup.IsFinite())
                    {
                        var cameraAup = originAup;
                        if (RuntimeOriginRoute.TryRuntimePositionToAup(position, ref cameraAup) && cameraAup.IsFinite())
                        {
                            double3 absolute = cameraAup.ToAbsoluteDouble3();
                            if (math.all(math.isfinite(absolute)))
                                return absolute;
                        }
                    }
                }
            }

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    snapshot.Aup.IsFinite())
                {
                    return snapshot.Aup.ToAbsoluteDouble3();
                }

                var playerMovement = playerContext.PlayerMovement;
                if (playerMovement != null)
                {
                    var currentAup = playerMovement.CurrentAup;
                    if (currentAup.IsFinite())
                        return currentAup.ToAbsoluteDouble3();
                }
            }

            return ResolveCurrentRuntimeOriginAup();
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 absolutePosition)
        {
            absolutePosition = default;
            if (!IsFinite(runtimePosition))
                return false;

            var positionAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!RuntimeOriginRoute.TryRuntimePositionToAup(runtimePosition, ref positionAup) || !positionAup.IsFinite())
                return false;

            absolutePosition = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absolutePosition));
        }

        private static double3 ResolveCurrentRuntimeOriginAup()
        {
            var originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return double3.zero;

            double3 absoluteOrigin = originAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteOrigin)) ? absoluteOrigin : double3.zero;
        }

        private static float ResolveBallisticRadius(float damage, float velocity, DecalTuningDTO tuning)
        {
            float safeDamage = math.max(0f, math.isfinite(damage) ? damage : 0f);
            float safeVelocity = math.max(0f, math.isfinite(velocity) ? velocity : 0f);
            float severity = math.saturate((safeDamage * 0.035f) + (safeVelocity * 0.0025f));
            float baseRadius = math.max(0.025f, math.isfinite(tuning.BaseRadiusMeters) ? tuning.BaseRadiusMeters : ResolveDefaultTuning().BaseRadiusMeters);
            return math.clamp(math.lerp(baseRadius * 0.29f, baseRadius * 1.67f, Smooth01(severity)), 0.025f, 8f);
        }

        private static float ResolveBallisticLifetime(uint materialHash, float damage, DecalTuningDTO tuning)
        {
            float severity = math.saturate(math.max(0f, math.isfinite(damage) ? damage : 0f) * 0.04f);
            float materialBoost = ((Mix(materialHash) & 7u) * 0.18f);
            float baseLifetime = math.max(0.1f, math.isfinite(tuning.BaseFadeTimeSeconds) ? tuning.BaseFadeTimeSeconds : ResolveDefaultTuning().BaseFadeTimeSeconds);
            return math.clamp(math.lerp(baseLifetime * 0.366f, (baseLifetime * 1.333f) + materialBoost, Smooth01(severity)), 0.1f, 60f);
        }

        private static bool TryResolveMaterialProfile(
            uint sourceHash,
            NativeArray<DecalMaterialProfileDTO>.ReadOnly profiles,
            int profileCapacity,
            out DecalMaterialProfileDTO profile)
        {
            profile = default;
            if (sourceHash == 0u || _materialProfileCount <= 0 || !profiles.IsCreated)
                return false;

            int capacity = math.min(math.max(0, profileCapacity), profiles.Length);
            if (capacity <= 0)
                return false;

            uint mask = (uint)(capacity - 1);
            int slot = capacity > 0 && (capacity & (capacity - 1)) == 0
                ? (int)(sourceHash & mask)
                : (int)(sourceHash % (uint)capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = slot + probe;
                if (index >= capacity)
                    index -= capacity;

                DecalMaterialProfileDTO candidate = profiles[index];
                if (candidate.SourceHash == 0u)
                    return false;
                if (candidate.SourceHash != sourceHash)
                    continue;

                profile = candidate;
                return true;
            }

            return false;
        }

        private static void PushTelemetry(
            NativeArray<TraumaWoundTelemetryEntry> telemetry,
            in DecalRuntimeStateDTO state,
            float quality,
            float thermalPressure)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int index = _telemetryCursor;
            _telemetryCursor++;
            if (_telemetryCursor >= math.min(telemetry.Length, TelemetryCapacity))
                _telemetryCursor = 0;

            TraumaWoundTelemetryEntry entry = default;
            entry.Frame = state.Frame;
            entry.ActiveDecals = (uint)math.max(0, state.ActiveCount);
            entry.NewDecals = (uint)math.max(0, state.NewThisFrame);
            entry.UploadCount = (uint)math.max(0, state.LastUploadCount);
            entry.GpuUploadMicroseconds = state.UploadMicroseconds;
            entry.CpuMicroseconds = state.CpuMicroseconds;
            entry.GlobalQualityWeight = quality;
            entry.ThermalPressure01 = thermalPressure;
            entry.Flags = state.Flags;
            entry.StateHash = Mix((uint)state.ActiveCount ^ ((uint)state.LastUploadCount << 8) ^ state.TotalWritten);
            entry.DroppedThisFrame = (uint)math.max(0, state.DroppedThisFrame);
            entry.TotalWritten = state.TotalWritten;
            entry.MaxActiveThisFrame = (uint)math.max(0, state.MaxActiveThisFrame);
            entry.LastBallisticFrame = state.LastBallisticFrame;
            telemetry[index] = entry;
            _lastTelemetrySnapshot = entry;
            _hasTelemetrySnapshot = true;
        }

        private static void MarkFault(uint flags)
        {
            if (!EnsureInitialized())
                return;

            bool stateLocked = false;
            try
            {
                if (!TryAcquireDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState, 1, out NativeArray<DecalRuntimeStateDTO> stateArray))
                    return;
                stateLocked = true;

                DecalRuntimeStateDTO state = stateArray[0];
                state.Flags |= flags;
                stateArray[0] = state;
                _lastRuntimeStateSnapshot = state;
                _hasRuntimeStateSnapshot = true;
            }
            finally
            {
                if (stateLocked)
                    ReleaseDynamicDecalVaultBuffer(in _stateHandle, DynamicDecalVaultBufferIds.RuntimeState);
            }
        }

        private static void DumpBlackBox(uint reasonFlags)
        {
            if (_dumpedFault || !EnsureInitialized())
                return;

            if (!TryResolveTelemetryDumpWindow(out int telemetryCursor, out int count))
                return;

            _dumpedFault = true;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string directory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string dumpPath = Path.Combine(directory, DumpFileName);
                using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                WriteBlackBoxDumpHeader(stream, reasonFlags, telemetryCursor);
                for (int i = 0; i < count; i++)
                {
                    int index = telemetryCursor + i;
                    if (index >= count)
                        index -= count;

                    if (!TryReadTelemetryDumpEntry(index, out TraumaWoundTelemetryEntry entry))
                        entry = default;

                    WriteBlackBoxTelemetryRow(stream, in entry);
                }
            }
            catch (IOException)
            {
                _dumpedFault = false;
            }
            catch (UnauthorizedAccessException)
            {
                _dumpedFault = false;
            }
            catch (ObjectDisposedException)
            {
                _dumpedFault = false;
            }
            catch (InvalidOperationException)
            {
                _dumpedFault = false;
            }
            catch (ArgumentException)
            {
                _dumpedFault = false;
            }
            catch (NotSupportedException)
            {
                _dumpedFault = false;
            }
        }

        private static bool TryResolveTelemetryDumpWindow(out int telemetryCursor, out int count)
        {
            telemetryCursor = 0;
            count = 0;
            if (!TryReadDynamicDecalVaultBuffer(in _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing, TelemetryCapacity, out NativeArray<TraumaWoundTelemetryEntry>.ReadOnly telemetry))
                return false;

            count = math.min(telemetry.Length, TelemetryCapacity);
            telemetryCursor = count > 0 ? PositiveModulo(_telemetryCursor, count) : 0;
            return count > 0;
        }

        private static bool TryReadTelemetryDumpEntry(int index, out TraumaWoundTelemetryEntry entry)
        {
            entry = default;
            if (index < 0 ||
                !TryReadDynamicDecalVaultBuffer(in _telemetryHandle, DynamicDecalVaultBufferIds.TelemetryRing, TelemetryCapacity, out NativeArray<TraumaWoundTelemetryEntry>.ReadOnly telemetry) ||
                index >= telemetry.Length)
                return false;

            entry = telemetry[index];
            return true;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            if (modulus <= 0)
                return 0;

            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static void WriteBlackBoxDumpHeader(FileStream stream, uint reasonFlags, int telemetryCursor)
        {
            Span<byte> header = stackalloc byte[16];
            WriteUInt32LittleEndian(header, 0, DumpMagic);
            WriteUInt32LittleEndian(header, 4, reasonFlags);
            WriteUInt32LittleEndian(header, 8, (uint)TelemetryCapacity);
            WriteUInt32LittleEndian(header, 12, (uint)math.max(0, telemetryCursor));
            stream.Write(header);
        }

        private static void WriteBlackBoxTelemetryRow(FileStream stream, in TraumaWoundTelemetryEntry entry)
        {
            Span<byte> row = stackalloc byte[64];
            WriteUInt32LittleEndian(row, 0, entry.Frame);
            WriteUInt32LittleEndian(row, 4, entry.ActiveDecals);
            WriteUInt32LittleEndian(row, 8, entry.NewDecals);
            WriteUInt32LittleEndian(row, 12, entry.UploadCount);
            WriteSingleLittleEndian(row, 16, entry.GpuUploadMicroseconds);
            WriteSingleLittleEndian(row, 20, entry.CpuMicroseconds);
            WriteSingleLittleEndian(row, 24, entry.GlobalQualityWeight);
            WriteSingleLittleEndian(row, 28, entry.ThermalPressure01);
            WriteUInt32LittleEndian(row, 32, entry.Flags);
            WriteUInt32LittleEndian(row, 36, entry.StateHash);
            WriteUInt32LittleEndian(row, 40, entry.DroppedThisFrame);
            WriteUInt32LittleEndian(row, 44, entry.TotalWritten);
            WriteUInt32LittleEndian(row, 48, entry.MaxActiveThisFrame);
            WriteUInt32LittleEndian(row, 52, entry.LastBallisticFrame);
            stream.Write(row);
        }

        private static void WriteSingleLittleEndian(Span<byte> bytes, int offset, float value)
        {
            WriteUInt32LittleEndian(bytes, offset, math.asuint(value));
        }

        private static void WriteUInt32LittleEndian(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static bool TryReadLine(ReadOnlySpan<byte> text, ref int cursor, out ReadOnlySpan<byte> line)
        {
            line = ReadOnlySpan<byte>.Empty;
            if ((uint)cursor >= (uint)text.Length)
                return false;

            int start = cursor;
            while (cursor < text.Length)
            {
                byte value = text[cursor++];
                if (value == (byte)'\n')
                {
                    int length = cursor - start - 1;
                    if (length > 0 && text[start + length - 1] == (byte)'\r')
                        length--;
                    line = length > 0 ? text.Slice(start, length) : ReadOnlySpan<byte>.Empty;
                    return true;
                }
            }

            line = text.Slice(start, text.Length - start);
            return true;
        }

        private static bool TryReadField(ReadOnlySpan<byte> line, int fieldIndex, out ReadOnlySpan<byte> token)
        {
            token = ReadOnlySpan<byte>.Empty;
            if (fieldIndex < 0)
                return false;

            int field = 0;
            int start = 0;
            for (int i = 0; i <= line.Length; i++)
            {
                bool delimiter = i == line.Length || line[i] == (byte)',' || line[i] == (byte)';' || line[i] == (byte)'\t';
                if (!delimiter)
                    continue;

                if (field == fieldIndex)
                {
                    token = TrimAscii(line.Slice(start, i - start));
                    return token.Length > 0;
                }

                field++;
                start = i + 1;
            }

            return false;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsAsciiWhitespace(value[start]))
                start++;
            while (end >= start && IsAsciiWhitespace(value[end]))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool IsHeaderToken(ReadOnlySpan<byte> token)
        {
            return EqualsLowerAscii(token, "source") ||
                   EqualsLowerAscii(token, "weapon") ||
                   EqualsLowerAscii(token, "material") ||
                   EqualsLowerAscii(token, "hash") ||
                   EqualsLowerAscii(token, "name");
        }

        private static bool EqualsLowerAscii(ReadOnlySpan<byte> token, string ascii)
        {
            if (token.Length != ascii.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                byte value = token[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                if (value != (byte)ascii[i])
                    return false;
            }

            return true;
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> token)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                byte value = token[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                hash ^= value;
                hash *= 16777619u;
            }

            return hash != 0u ? hash : 1u;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            token = TrimAscii(token);
            if (token.Length <= 0)
                return false;

            int index = 0;
            bool hex = token.Length > 2 && token[0] == (byte)'0' && (token[1] == (byte)'x' || token[1] == (byte)'X');
            if (hex)
                index = 2;

            bool any = false;
            for (; index < token.Length; index++)
            {
                byte c = token[index];
                uint digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                    digit = (uint)(c - (byte)'0');
                else if (hex && c >= (byte)'a' && c <= (byte)'f')
                    digit = (uint)(c - (byte)'a' + 10);
                else if (hex && c >= (byte)'A' && c <= (byte)'F')
                    digit = (uint)(c - (byte)'A' + 10);
                else
                    return false;

                value = hex ? (value * 16u) + digit : (value * 10u) + digit;
                any = true;
            }

            return any;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            token = TrimAscii(token);
            if (token.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (token[index] == (byte)'-' || token[index] == (byte)'+')
            {
                sign = token[index] == (byte)'-' ? -1f : 1f;
                index++;
            }

            float result = 0f;
            bool any = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                result = (result * 10f) + (token[index] - (byte)'0');
                index++;
                any = true;
            }

            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    result += (token[index] - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                    any = true;
                }
            }

            if (!any || index != token.Length)
                return false;

            value = result * sign;
            return math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveVisualFrameId()
        {
            uint dispatcherFrame = TimeSliceScheduler.CurrentFrameId;
            if (dispatcherFrame != 0u)
                return dispatcherFrame;

            uint next = _fallbackVisualFrameId + 1u;
            if (next == 0u)
                next = 1u;

            _fallbackVisualFrameId = next;
            return next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 MakeFloat3(float x, float y, float z)
        {
            float3 result = default;
            result.x = x;
            result.y = y;
            result.z = z;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double3 MakeDouble3(double x, double y, double z)
        {
            double3 result = default;
            result.x = x;
            result.y = y;
            result.z = z;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float4 MakeFloat4(float x, float y, float z, float w)
        {
            float4 result = default;
            result.x = x;
            result.y = y;
            result.z = z;
            result.w = w;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float4 MakeFloat4(float3 xyz, float w)
        {
            float4 result = default;
            result.x = xyz.x;
            result.y = xyz.y;
            result.z = xyz.z;
            result.w = w;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeNormal(float3 normal, float3 fallback)
        {
            if (!math.all(math.isfinite(normal)))
                return fallback;

            float lengthSq = math.lengthsq(normal);
            return lengthSq > 0.0001f ? normal * math.rsqrt(lengthSq) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float t = math.saturate(math.isfinite(value) ? value : 0f);
            return t * t * (3f - (2f * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashFloat3(Vector3 value)
        {
            uint x = math.asuint(value.x);
            uint y = math.asuint(value.y);
            uint z = math.asuint(value.z);
            return Mix(x ^ RotateLeft(y, 11) ^ RotateLeft(z, 19));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ResolveDecalTypeFromMaterial(uint materialHash)
        {
            uint materialType = materialHash & DecalTypePackedMask;
            return materialHash < AtlasSliceCount && materialType <= DynamicDecalMaterialHashes.Burn
                ? materialType
                : DynamicDecalMaterialHashes.Scorch;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ResolveAtlasSliceFromMaterial(uint materialHash)
        {
            return materialHash < AtlasSliceCount
                ? materialHash & DecalAtlasPackedMask
                : Mix(materialHash) & DecalAtlasPackedMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint PackDecalPayload(uint decalType, uint atlasSlice)
        {
            return (decalType & DecalTypePackedMask) |
                   ((atlasSlice & DecalAtlasPackedMask) << DecalAtlasPackedShift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint PackRequestMaterialPayload(uint decalType, uint atlasSlice)
        {
            return DecalMaterialPayloadPackedFlag | PackDecalPayload(decalType, atlasSlice);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ResolveRequestDecalType(uint materialPayload)
        {
            return (materialPayload & DecalMaterialPayloadPackedFlag) != 0u
                ? materialPayload & DecalTypePackedMask
                : ResolveDecalTypeFromMaterial(materialPayload);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ResolveRequestAtlasSlice(uint materialPayload)
        {
            return (materialPayload & DecalMaterialPayloadPackedFlag) != 0u
                ? (materialPayload >> DecalAtlasPackedShift) & DecalAtlasPackedMask
                : ResolveAtlasSliceFromMaterial(materialPayload);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ResolveRequestDecalPayload(uint materialPayload)
        {
            return PackDecalPayload(ResolveRequestDecalType(materialPayload), ResolveRequestAtlasSlice(materialPayload));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint PackDecalTypeAndLifetime(uint decalPayload, float lifetimeSeconds)
        {
            float safeLifetime = math.clamp(math.isfinite(lifetimeSeconds) ? lifetimeSeconds : 0.1f, 0.1f, 60f);
            uint lifetimeCentiseconds = (uint)math.clamp(
                math.round(safeLifetime * DecalLifetimePackedScale),
                10f,
                (float)DecalLifetimePackedMask);
            return (decalPayload & DecalTypePayloadMask) |
                   ((lifetimeCentiseconds & DecalLifetimePackedMask) << DecalLifetimePackedShift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint UnpackDecalType(uint packedDecalTypeHash)
        {
            return packedDecalTypeHash & DecalTypePackedMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint UnpackDecalAtlasSlice(uint packedDecalTypeHash)
        {
            return (packedDecalTypeHash >> DecalAtlasPackedShift) & DecalAtlasPackedMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float UnpackDecalLifetimeSeconds(uint packedDecalTypeHash, float fallbackSeconds)
        {
            uint lifetimeCentiseconds = (packedDecalTypeHash >> DecalLifetimePackedShift) & DecalLifetimePackedMask;
            float fallback = math.max(0.1f, math.isfinite(fallbackSeconds) ? fallbackSeconds : 7.5f);
            return lifetimeCentiseconds > 0u
                ? math.max(0.1f, lifetimeCentiseconds / DecalLifetimePackedScale)
                : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft(uint value, int shift)
        {
            return (value << shift) | (value >> (32 - shift));
        }

        public static void CopyDecalsToMappedUploadBuffer(
            NativeArray<TraumaDecalDTO>.ReadOnly source,
            TraumaDecalDTO* destination,
            int count)
        {
            int safeCount = math.min(math.max(0, count), source.IsCreated ? source.Length : 0);
            if (safeCount <= 0 || destination == null)
                return;

            for (int i = 0; i < safeCount; i++)
                UnsafeUtility.AsRef<TraumaDecalDTO>(destination + i) = source[i];
        }

#if UNITY_EDITOR
        private static int OffsetOf<T>(string fieldName)
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
#endif
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateTraumaDecalMatricesJob : IJob
    {
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public DecalRequestSignal* Requests;
        [NoAlias, NativeDisableUnsafePtrRestriction] public DecalRequestQueueStateDTO* RequestState;
        [NoAlias, NativeDisableUnsafePtrRestriction] public TraumaDecalDTO* Decals;
        [NoAlias, NativeDisableUnsafePtrRestriction] public DecalRuntimeStateDTO* State;
        public double3 CameraAup;
        public int Capacity;
        public int MaxRequestsPerFrame;
        public float DefaultRadiusMeters;
        public float DefaultProjectionDepthMeters;
        public float DefaultLifetimeSeconds;
        public int IngressDroppedBeforeJob;

        public void Execute()
        {
            ref DecalRuntimeStateDTO state = ref UnsafeUtility.AsRef<DecalRuntimeStateDTO>(State);
            state.NewThisFrame = 0;
            state.DroppedThisFrame = math.max(0, IngressDroppedBeforeJob);
            int processed = 0;
            int capacity = math.max(1, Capacity);
            int maxRequests = math.max(1, MaxRequestsPerFrame);
            ref DecalRequestQueueStateDTO queueState = ref UnsafeUtility.AsRef<DecalRequestQueueStateDTO>(RequestState);
            int requestCapacity = math.max(1, math.min(queueState.Capacity, DynamicDecalVaultRuntime.RequestRingCapacity));
            queueState.ReadIndex = WrapRequestIndex(queueState.ReadIndex, requestCapacity);
            queueState.WriteIndex = WrapRequestIndex(queueState.WriteIndex, requestCapacity);
            queueState.PendingCount = math.clamp(queueState.PendingCount, 0, requestCapacity);
            while (processed < maxRequests && queueState.PendingCount > 0)
            {
                int requestIndex = queueState.ReadIndex;
                DecalRequestSignal request = UnsafeUtility.AsRef<DecalRequestSignal>(Requests + requestIndex);
                queueState.ReadIndex = WrapRequestIndex(requestIndex + 1, requestCapacity);
                queueState.PendingCount--;
                queueState.DrainedTotal++;
                processed++;
                if (!TryBuildMatrix(in request, out float4x4 matrix, out uint decalPayload))
                {
                    state.Flags |= DynamicDecalFlags.NonFinite;
                    state.DroppedThisFrame++;
                    continue;
                }

                int index = (int)(state.TotalWritten % (uint)capacity);

                ref TraumaDecalDTO decal = ref UnsafeUtility.AsRef<TraumaDecalDTO>(Decals + index);
                float lifetime = math.max(0.1f, math.isfinite(request.LifetimeSeconds) && request.LifetimeSeconds > 0f ? request.LifetimeSeconds : DefaultLifetimeSeconds);
                decal.LocalToWorld = matrix;
                decal.DecalTypeHash = DynamicDecalVaultRuntime.PackDecalTypeAndLifetime(decalPayload, lifetime);
                decal.Opacity01 = 1f;
                decal.BirthTime = (float)(request.SourceFrame != 0u ? request.SourceFrame : state.Frame);
                decal.Flags = (request.Flags | DynamicDecalFlags.Active) & ~DynamicDecalFlags.NonFinite;

                index++;
                if (index >= capacity)
                    index = 0;
                state.CurrentWriteIndex = index;
                if (state.ActiveCount < capacity)
                    state.ActiveCount++;
                state.TotalWritten++;
                state.NewThisFrame++;
            }
        }

        private bool TryBuildMatrix(in DecalRequestSignal request, out float4x4 matrix, out uint decalPayload)
        {
            matrix = float4x4.identity;
            decalPayload = DynamicDecalVaultRuntime.ResolveRequestDecalPayload(request.MaterialHash);
            double3 local = request.ImpactAup - CameraAup;
            if (!math.all(math.isfinite(local)))
                return false;

            const double MaxLocalDecalMeters = 1000000.0d;
            local = math.clamp(local, new double3(-MaxLocalDecalMeters), new double3(MaxLocalDecalMeters));
            float3 position = (float3)local;
            if (!math.all(math.isfinite(position)))
                return false;

            float3 surfaceNormal = NormalizeOrDefault(request.Normal, DynamicDecalVaultRuntime.MakeFloat3(0f, 1f, 0f));
            float3 zAxis = -surfaceNormal;
            float3 basis = math.abs(surfaceNormal.y) < 0.92f
                ? DynamicDecalVaultRuntime.MakeFloat3(0f, 1f, 0f)
                : DynamicDecalVaultRuntime.MakeFloat3(1f, 0f, 0f);
            float3 xAxis = NormalizeOrDefault(math.cross(basis, zAxis), DynamicDecalVaultRuntime.MakeFloat3(1f, 0f, 0f));
            float3 yAxis = NormalizeOrDefault(math.cross(zAxis, xAxis), DynamicDecalVaultRuntime.MakeFloat3(0f, 1f, 0f));

            uint seed = request.StableSeed != 0u
                ? request.StableSeed
                : DynamicDecalVaultRuntime.Mix(math.asuint(position.x) ^ math.asuint(position.y) ^ math.asuint(position.z) ^ decalPayload);
            float phase = (seed & 65535u) * (1f / 65535f);
            float3 rolledX = NormalizeOrDefault(
                (xAxis * TriangleWaveSigned(phase + 0.25f)) + (yAxis * TriangleWaveSigned(phase)),
                xAxis);
            float3 rolledY = NormalizeOrDefault(math.cross(zAxis, rolledX), yAxis);
            float radius = math.max(0.025f, math.isfinite(request.RadiusMeters) && request.RadiusMeters > 0f ? request.RadiusMeters : DefaultRadiusMeters);
            float depth = math.max(0.01f, math.isfinite(request.ProjectionDepthMeters) && request.ProjectionDepthMeters > 0f ? request.ProjectionDepthMeters : DefaultProjectionDepthMeters);
            matrix = default;
            matrix.c0 = DynamicDecalVaultRuntime.MakeFloat4(rolledX * radius, 0f);
            matrix.c1 = DynamicDecalVaultRuntime.MakeFloat4(rolledY * radius, 0f);
            matrix.c2 = DynamicDecalVaultRuntime.MakeFloat4(zAxis * depth, 0f);
            matrix.c3 = DynamicDecalVaultRuntime.MakeFloat4(position, 1f);
            return math.all(math.isfinite(matrix.c0)) &&
                   math.all(math.isfinite(matrix.c1)) &&
                   math.all(math.isfinite(matrix.c2)) &&
                   math.all(math.isfinite(matrix.c3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleWaveSigned(float phase)
        {
            return math.abs(math.frac(phase) * 2f - 1f) * 2f - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeOrDefault(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && lengthSq > 0.0001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WrapRequestIndex(int index, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            while (index >= safeCapacity)
                index -= safeCapacity;
            while (index < 0)
                index += safeCapacity;
            return index;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct DecayTraumaDecalOpacityJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public TraumaDecalDTO* Decals;
        [NoAlias, NativeDisableUnsafePtrRestriction] public DecalRuntimeStateDTO* State;
        public float DeltaTime;
        public float DecayRate;
        public float DefaultLifetimeSeconds;
        public int Capacity;

        public void Execute()
        {
            int activeCount = 0;
            int capacity = math.max(0, Capacity);
            float baseLifetime = math.max(0.1f, math.isfinite(DefaultLifetimeSeconds) ? DefaultLifetimeSeconds : 7.5f);
            float decay = math.max(0f, DecayRate) * math.max(0f, DeltaTime);
            for (int i = 0; i < capacity; i++)
            {
                ref TraumaDecalDTO decal = ref UnsafeUtility.AsRef<TraumaDecalDTO>(Decals + i);
                if ((decal.Flags & DynamicDecalFlags.Active) == 0u)
                    continue;

                float opacity = decal.Opacity01;
                if (!math.isfinite(opacity))
                {
                    decal.Opacity01 = 0f;
                    decal.Flags = 0u;
                    ref DecalRuntimeStateDTO faultState = ref UnsafeUtility.AsRef<DecalRuntimeStateDTO>(State);
                    faultState.Flags |= DynamicDecalFlags.NonFinite;
                    continue;
                }

                uint decalType = DynamicDecalVaultRuntime.UnpackDecalType(decal.DecalTypeHash);
                bool persistentGlass = decalType == DynamicDecalMaterialHashes.GlassCrack ||
                                       (decal.Flags & DynamicDecalFlags.PersistentGlass) != 0u;
                float materialScale = persistentGlass ? 0.035f :
                    decalType == DynamicDecalMaterialHashes.Blood ? 1.15f :
                    decalType == DynamicDecalMaterialHashes.Burn ? 0.85f :
                    decalType == DynamicDecalMaterialHashes.Acid ? 1.35f :
                    1.0f;
                float lifetime = DynamicDecalVaultRuntime.UnpackDecalLifetimeSeconds(decal.DecalTypeHash, baseLifetime);
                float lifetimeScale = materialScale * baseLifetime * math.rcp(math.max(lifetime, 0.1f));
                opacity = persistentGlass
                    ? math.max(0.08f, opacity - (decay * lifetimeScale))
                    : math.max(0f, opacity - (decay * lifetimeScale));
                decal.Opacity01 = opacity;
                if (opacity <= 0.0001f)
                {
                    decal.Flags = 0u;
                    continue;
                }

                activeCount++;
            }

            ref DecalRuntimeStateDTO state = ref UnsafeUtility.AsRef<DecalRuntimeStateDTO>(State);
            state.ActiveCount = activeCount;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct BuildDecalUploadBufferJob : IJob
    {
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public TraumaDecalDTO* Decals;
        [NoAlias, NativeDisableUnsafePtrRestriction] public TraumaDecalDTO* Upload;
        [NoAlias, NativeDisableUnsafePtrRestriction] public DecalRuntimeStateDTO* State;
        public int Capacity;
        public int UploadCapacity;
        public int MaxActiveDecals;

        public void Execute()
        {
            ref DecalRuntimeStateDTO state = ref UnsafeUtility.AsRef<DecalRuntimeStateDTO>(State);
            int capacity = math.max(1, Capacity);
            int limit = math.min(math.max(0, MaxActiveDecals), math.min(capacity, UploadCapacity));
            int write = 0;
            int cursor = state.CurrentWriteIndex - 1;
            if (cursor < 0)
                cursor += capacity;

            for (int visited = 0; visited < capacity && write < limit; visited++)
            {
                ref readonly TraumaDecalDTO decal = ref UnsafeUtility.AsRef<TraumaDecalDTO>(Decals + cursor);
                if ((decal.Flags & DynamicDecalFlags.Active) != 0u && decal.Opacity01 > 0.0001f)
                {
                    ref TraumaDecalDTO destination = ref UnsafeUtility.AsRef<TraumaDecalDTO>(Upload + write);
                    destination = decal;
                    write++;
                }

                cursor--;
                if (cursor < 0)
                    cursor += capacity;
            }

            state.LastUploadCount = write;
        }
    }

#if UNITY_EDITOR
    internal static class DynamicDecalLayoutEditorValidator
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void ValidateOnLoad()
        {
            if (!DynamicDecalVaultRuntime.ValidateDecalInstanceLayout())
                Hecton8.Core.H8Debug.LogError("AGENT_1335 visor trauma ABI layout mismatch: expected TraumaDecalDTO=80B and request ingress DTOs=64B with explicit shader/Vault offsets.");
        }

        [UnityEditor.MenuItem("HECTON-8/Rendering/Validate Visor Trauma Layout")]
        private static void ValidateMenu()
        {
            if (DynamicDecalVaultRuntime.ValidateDecalInstanceLayout())
                Hecton8.Core.H8Debug.Log("AGENT_1335 visor trauma ABI layout valid: TraumaDecalDTO=80B, DecalRequestSignal=64B, DecalRequestQueueStateDTO=64B.");
            else
                Hecton8.Core.H8Debug.LogError("AGENT_1335 visor trauma ABI layout mismatch: shader/Vault ABI is unsafe.");
        }
    }
#endif
}
