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
        public const BufferID Instances = (BufferID)71490;
        public const BufferID UploadScratch = (BufferID)71491;
        public const BufferID RuntimeState = (BufferID)71492;
        public const BufferID TelemetryRing = (BufferID)71493;
        public const BufferID Tuning = (BufferID)71494;
        public const BufferID MaterialProfiles = (BufferID)71495;
        public const BufferID CsvScratch = (BufferID)71496;
    }

    public static class DynamicDecalMaterialHashes
    {
        public const uint Scorch = 0u;
        public const uint Blood = 1u;
        public const uint Acid = 2u;
        public const uint HullDent = 3u;
    }

    public static class DynamicDecalFlags
    {
        public const uint None = 0u;
        public const uint Active = 1u << 0;
        public const uint Ballistic = 1u << 1;
        public const uint HullImpact = 1u << 2;
        public const uint Mock = 1u << 3;
        public const uint NonFinite = 1u << 31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct DecalInstanceDTO
    {
        [FieldOffset(0)] public float4x4 LocalToWorld;
        [FieldOffset(64)] public uint MaterialHash;
        [FieldOffset(68)] public float Opacity01;
        [FieldOffset(72)] public float LifetimeSeconds;
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
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DecalTuningDTO
    {
        [FieldOffset(0)] public float BaseFadeTimeSeconds;
        [FieldOffset(4)] public float MaximumOverkillCapacity;
        [FieldOffset(8)] public float AtlasMipmapBias;
        [FieldOffset(12)] public float ProjectionDepthMeters;
        [FieldOffset(16)] public float LowTierCapacity;
        [FieldOffset(20)] public float BaseRadiusMeters;
        [FieldOffset(24)] public uint Revision;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DecalTelemetryEntry
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
        [FieldOffset(56)] public ulong _pad0;
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
        [FieldOffset(24)] public ulong _pad0;
    }

    public struct DynamicDecalFrameStats
    {
        public NativeArray<DecalInstanceDTO> UploadBuffer;
        public int UploadCount;
        public int ActiveCount;
        public int NewCount;
        public int MaxActiveCount;
        public float CpuMicroseconds;
        public float UploadMicroseconds;
        public float GlobalQualityWeight;
        public float ThermalPressure01;
    }

    public static unsafe class DynamicDecalVaultRuntime
    {
        public const int MaxCapacity = 1024;
        public const int LowCapacity = 128;
        public const int TelemetryCapacity = 300;
        public const int RequestQueuePrewarmCapacity = 1024;
        public const int AtlasSliceCount = 16;
        public const int MaxMaterialProfiles = 256;
        public const int CsvScratchBytes = 16384;

        private const uint RuntimeInitializedFlag = 1u << 0;
        private const uint RuntimeLayoutFaultFlag = 1u << 1;
        private const uint RuntimeNonFiniteFaultFlag = 1u << 2;
        private const uint RuntimeUploadStallFlag = 1u << 3;
        private const uint DumpMagic = 0x4445434Cu; // DECL
        private const string DumpFileName = "Dump_DECAL_PROJECTOR.bin";
        private const string LogOwner = nameof(DynamicDecalVaultRuntime);
        private const SystemID OwnerSystem = SystemID.Vfx;

        private static readonly ProfilerMarker _visualSyncMarker = new ProfilerMarker("H8.Decal.VisualSync");
        private static readonly ProfilerMarker _enqueueMarker = new ProfilerMarker("H8.Decal.Enqueue");

        private static IDataVault _vault;
        private static VaultBufferHandle<DecalInstanceDTO> _instancesHandle;
        private static VaultBufferHandle<DecalInstanceDTO> _uploadHandle;
        private static VaultBufferHandle<DecalRuntimeStateDTO> _stateHandle;
        private static VaultBufferHandle<DecalTelemetryEntry> _telemetryHandle;
        private static VaultBufferHandle<DecalTuningDTO> _tuningHandle;
        private static VaultBufferHandle<DecalMaterialProfileDTO> _materialProfileHandle;
        private static VaultBufferHandle<byte> _csvScratchHandle;
        private static NativeQueue<DecalRequestSignal> _requests;
        private static uint _lastIngestedBallisticFrame;
        private static uint _lastIngestedHighSpeedFrame;
        private static uint _lastIngestedCombatDamageFrame;
        private static bool _hasIngestedHighSpeedFrame;
        private static bool _hasIngestedCombatDamageFrame;
        private static int _telemetryCursor;
        private static int _materialProfileCount;
        private static int _lastSignalSnapshotUnityFrame;
        private static int _droppedIngressThisFrame;
        private static bool _queueRegistered;
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
        private static bool _hasLastCompletedStats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_requests.IsCreated)
            {
                if (_queueRegistered)
                    NativeMemorySentinel.UnregisterNativeQueue(LogOwner, nameof(_requests));
                _requests.Dispose();
            }

            if (_pendingVisualSyncActive)
            {
                DispatcherJobFence.TryComplete(ref _pendingVisualSyncHandle, forceComplete: true);
                UnlockRuntimeBuffers();
            }

            _vault = null;
            _instancesHandle = default;
            _uploadHandle = default;
            _stateHandle = default;
            _telemetryHandle = default;
            _tuningHandle = default;
            _materialProfileHandle = default;
            _csvScratchHandle = default;
            _lastIngestedBallisticFrame = 0u;
            _lastIngestedHighSpeedFrame = 0u;
            _lastIngestedCombatDamageFrame = 0u;
            _hasIngestedHighSpeedFrame = false;
            _hasIngestedCombatDamageFrame = false;
            _telemetryCursor = 0;
            _materialProfileCount = 0;
            _lastSignalSnapshotUnityFrame = -1;
            _droppedIngressThisFrame = 0;
            _queueRegistered = false;
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
        }

        public static bool ValidateDecalInstanceLayout()
        {
            if (_layoutValidated)
                return _layoutValid;

            _layoutValid = UnsafeUtility.SizeOf<DecalInstanceDTO>() == 80;
#if UNITY_EDITOR
            _layoutValid = _layoutValid &&
                           OffsetOf<DecalInstanceDTO>(nameof(DecalInstanceDTO.LocalToWorld)) == 0 &&
                           OffsetOf<DecalInstanceDTO>(nameof(DecalInstanceDTO.MaterialHash)) == 64 &&
                           OffsetOf<DecalInstanceDTO>(nameof(DecalInstanceDTO.Opacity01)) == 68 &&
                           OffsetOf<DecalInstanceDTO>(nameof(DecalInstanceDTO.LifetimeSeconds)) == 72 &&
                           OffsetOf<DecalInstanceDTO>(nameof(DecalInstanceDTO.Flags)) == 76;
#endif
            _layoutValidated = true;
            return _layoutValid;
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
                if (!EnsureInitialized())
                    return false;

                if (!IsFinite(runtimePosition))
                    return false;

                DecalTuningDTO tuning = ResolveLiveTuning();
                double3 aup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimePosition);
                float3 normal = new float3(surfaceNormal.x, surfaceNormal.y, surfaceNormal.z);
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
                    (uint)math.max(0, Time.frameCount));
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
                if (!EnsureInitialized())
                    return false;

                if (!math.all(math.isfinite(impactAup)))
                    return false;

                DecalTuningDTO defaults = ResolveLiveTuning();
                DecalRequestSignal request = default;
                request.ImpactAup = impactAup;
                request.Normal = SanitizeNormal(surfaceNormal, new float3(0f, 1f, 0f));
                request.RadiusMeters = math.clamp(math.isfinite(radiusMeters) ? radiusMeters : defaults.BaseRadiusMeters, 0.025f, 8f);
                request.ProjectionDepthMeters = math.clamp(math.isfinite(projectionDepthMeters) ? projectionDepthMeters : defaults.ProjectionDepthMeters, 0.025f, 2f);
                request.LifetimeSeconds = math.clamp(math.isfinite(lifetimeSeconds) ? lifetimeSeconds : defaults.BaseFadeTimeSeconds, 0.1f, 60f);
                request.MaterialHash = materialHash;
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
            if (!EnsureInitialized() || count <= 0)
                return false;

            int headroom = RequestQueuePrewarmCapacity - _requests.Count;
            if (headroom <= 0)
            {
                AccumulateDroppedIngress(count);
                return false;
            }

            int safeCount = math.clamp(count, 1, math.min(MaxCapacity, headroom));
            AccumulateDroppedIngress(count - safeCount);
            GenerateMockDecalRequestsJob job = new GenerateMockDecalRequestsJob
            {
                Requests = _requests.AsParallelWriter(),
                Count = safeCount,
                Frame = (uint)math.max(0, Time.frameCount),
                OriginAup = HectonFloatingOrigin.CurrentTotalOffsetDouble
            };

            JobHandle handle = job.Schedule(safeCount, 64);
            H8Memory.RegisterActiveJob(OwnerSystem, handle);
            // [BLOCKING_SYNC_POINT] Cold editor/test path: caller explicitly requested generated mock packets before profiling this frame.
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true); // COLD PROFILE PATH: deterministic mock injection requested by tools/tests.
            return true;
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
                if (!EnsureInitialized())
                    return false;

                if (!TryFinalizePendingVisualSync(out stats))
                    return _hasLastCompletedStats && stats.UploadCount > 0;

                if (!ValidateDecalInstanceLayout())
                {
                    MarkFault(RuntimeLayoutFaultFlag);
                    DumpBlackBox(RuntimeLayoutFaultFlag);
                    return false;
                }

                if (!TryLockRuntimeBuffers())
                    return false;

                long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                uint faultFlags = 0u;
                bool releaseRuntimeLocks = true;
                try
                {
                    TryIngestGlobalImpactSignals();
                    int ingressDroppedBeforeJob = math.max(0, _droppedIngressThisFrame);
                    _droppedIngressThisFrame = 0;

                    NativeArray<DecalInstanceDTO> instances = _instancesHandle.Resolve(_vault);
                    NativeArray<DecalInstanceDTO> upload = _uploadHandle.Resolve(_vault);
                    NativeArray<DecalRuntimeStateDTO> stateArray = _stateHandle.Resolve(_vault);
                    NativeArray<DecalTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
                    NativeArray<DecalTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
                    if (!instances.IsCreated || !upload.IsCreated || !stateArray.IsCreated || stateArray.Length <= 0 || !telemetry.IsCreated || !tuningArray.IsCreated)
                        return false;

                    DecalTuningDTO tuning = SanitizeTuning(tuningArray[0], baseFadeSeconds, requestedCapacity);
                    tuningArray[0] = tuning;
                    ref DecalRuntimeStateDTO state = ref _stateHandle.GetElementAsRef(_vault, 0);
                    bool hadRuntimeState = (state.Flags & RuntimeInitializedFlag) != 0u;
                    if (!hadRuntimeState)
                    {
                        ClearDecalsJob clearJob = new ClearDecalsJob
                        {
                            Decals = (DecalInstanceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(instances),
                            Capacity = math.min(instances.Length, MaxCapacity)
                        };
                        int clearCount = math.min(instances.Length, MaxCapacity);
                        for (int i = 0; i < clearCount; i++)
                            clearJob.Execute(i);

                        state = default;
                        state.Flags = RuntimeInitializedFlag;
                    }

                    float targetQuality = ResolveGlobalQualityWeight();
                    float thermalPressure = ResolveThermalPressure01();
                    float quality = ResolveEffectiveQualityWeight(targetQuality, state.GlobalQualityWeight, deltaTime, thermalPressure, hadRuntimeState);
                    int maxActive = ResolveMaxActiveDecals(quality, tuning);
                    float decayRate = ResolveDecayRate(deltaTime, quality, thermalPressure, tuning);
                    double3 cameraAup = ResolveCameraAup(camera);
                    _lastCameraWorldPosition = camera != null ? camera.transform.position : Vector3.zero;

                    GenerateDecalMatricesJob generateJob = new GenerateDecalMatricesJob
                    {
                        Requests = _requests,
                        Decals = (DecalInstanceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(instances),
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
                    DecayDecalOpacityJob decayJob = new DecayDecalOpacityJob
                    {
                        Decals = (DecalInstanceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(instances),
                        State = (DecalRuntimeStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(stateArray),
                        DeltaTime = math.max(0f, math.isfinite(deltaTime) ? deltaTime : 0f),
                        DecayRate = decayRate,
                        DefaultLifetimeSeconds = math.max(0.1f, tuning.BaseFadeTimeSeconds),
                        Capacity = math.min(instances.Length, MaxCapacity)
                    };
                    handle = decayJob.Schedule(handle);
                    BuildDecalUploadBufferJob uploadJob = new BuildDecalUploadBufferJob
                    {
                        Decals = (DecalInstanceDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(instances),
                        Upload = (DecalInstanceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(upload),
                        State = (DecalRuntimeStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(stateArray),
                        Capacity = math.min(instances.Length, MaxCapacity),
                        UploadCapacity = math.min(upload.Length, MaxCapacity),
                        MaxActiveDecals = maxActive
                    };
                    handle = uploadJob.Schedule(handle);
                    H8Memory.RegisterActiveJob(OwnerSystem, handle);

                    if (!DispatcherJobFence.TryFinalizeCompleted(ref handle))
                    {
                        _pendingVisualSyncHandle = handle;
                        _pendingVisualSyncActive = true;
                        _pendingVisualSyncStartTicks = startTicks;
                        _pendingVisualSyncQuality = quality;
                        _pendingVisualSyncThermalPressure = thermalPressure;
                        _pendingVisualSyncMaxActive = maxActive;
                        releaseRuntimeLocks = false;
                        stats = _lastCompletedStats;
                        return _hasLastCompletedStats && stats.UploadCount > 0;
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
                    if (releaseRuntimeLocks)
                        UnlockRuntimeBuffers();
                }

                if (faultFlags != 0u)
                    DumpBlackBox(faultFlags);

                return stats.UploadCount > 0;
            }
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
            try
            {
                NativeArray<DecalRuntimeStateDTO> stateArray = _stateHandle.Resolve(_vault);
                NativeArray<DecalInstanceDTO> upload = _uploadHandle.Resolve(_vault);
                NativeArray<DecalTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
                if (!stateArray.IsCreated || stateArray.Length <= 0 || !upload.IsCreated || !telemetry.IsCreated)
                    return false;

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
                UnlockRuntimeBuffers();
            }

            if (faultFlags != 0u)
                DumpBlackBox(faultFlags);

            return true;
        }

        private static void FinalizeCompletedVisualSync(
            NativeArray<DecalRuntimeStateDTO> stateArray,
            NativeArray<DecalInstanceDTO> upload,
            NativeArray<DecalTelemetryEntry> telemetry,
            long startTicks,
            float quality,
            float thermalPressure,
            int maxActive,
            out DynamicDecalFrameStats stats,
            ref uint faultFlags)
        {
            DecalRuntimeStateDTO state = stateArray[0];
            state.Frame = (uint)math.max(0, Time.frameCount);
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

            PushTelemetry(telemetry, in state, quality, thermalPressure);
            stats.UploadBuffer = upload;
            stats.UploadCount = math.clamp(state.LastUploadCount, 0, math.min(upload.Length, MaxCapacity));
            stats.ActiveCount = math.max(0, state.ActiveCount);
            stats.NewCount = math.max(0, state.NewThisFrame);
            stats.MaxActiveCount = maxActive;
            stats.CpuMicroseconds = state.CpuMicroseconds;
            stats.UploadMicroseconds = state.UploadMicroseconds;
            stats.GlobalQualityWeight = quality;
            stats.ThermalPressure01 = thermalPressure;
            _lastCompletedStats = stats;
            _hasLastCompletedStats = true;
        }

        public static void RecordGpuUploadMicroseconds(float uploadMicroseconds)
        {
            if (!EnsureInitialized())
                return;

            if (!TryLockUploadTelemetryBuffers())
                return;

            bool uploadStall = false;
            try
            {
                NativeArray<DecalRuntimeStateDTO> stateArray = _stateHandle.Resolve(_vault);
                if (!stateArray.IsCreated || stateArray.Length <= 0)
                    return;

                float safe = math.max(0f, math.isfinite(uploadMicroseconds) ? uploadMicroseconds : 0f);
                DecalRuntimeStateDTO state = stateArray[0];
                state.UploadMicroseconds = safe;
                uploadStall = safe > 300f;
                if (uploadStall)
                    state.Flags |= RuntimeUploadStallFlag;
                stateArray[0] = state;

                NativeArray<DecalTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
                int count = telemetry.IsCreated ? math.min(telemetry.Length, TelemetryCapacity) : 0;
                if (count > 0)
                {
                    int index = _telemetryCursor - 1;
                    if (index < 0)
                        index = count - 1;

                    DecalTelemetryEntry entry = telemetry[index];
                    entry.GpuUploadMicroseconds = safe;
                    entry.Flags = state.Flags;
                    telemetry[index] = entry;
                }
            }
            finally
            {
                UnlockUploadTelemetryBuffers();
            }

            if (uploadStall)
                DumpBlackBox(RuntimeUploadStallFlag);
        }

        public static bool TryGetTuning(out DecalTuningDTO tuning)
        {
            tuning = default;
            if (!EnsureInitialized())
                return false;

            if (!TryLockTuningBuffer())
                return false;

            try
            {
                NativeArray<DecalTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
                if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                    return false;

                tuning = tuningArray[0];
                return true;
            }
            finally
            {
                UnlockTuningBuffer();
            }
        }

        public static bool WriteTuning(in DecalTuningDTO tuning)
        {
            if (!EnsureInitialized())
                return false;

            if (!TryLockTuningBuffer())
                return false;

            try
            {
                NativeArray<DecalTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
                if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                    return false;

                float fallbackCapacityFloat = math.isfinite(tuning.MaximumOverkillCapacity)
                    ? math.clamp(tuning.MaximumOverkillCapacity, LowCapacity, MaxCapacity)
                    : MaxCapacity;
                DecalTuningDTO sanitized = SanitizeTuning(tuning, tuning.BaseFadeTimeSeconds, (int)math.round(fallbackCapacityFloat));
                sanitized.Revision = tuning.Revision + 1u;
                tuningArray[0] = sanitized;
                return true;
            }
            finally
            {
                UnlockTuningBuffer();
            }
        }

        public static bool TryGetRuntimeState(out DecalRuntimeStateDTO state)
        {
            state = default;
            if (!EnsureInitialized())
                return false;

            if (!TryLockStateBuffer())
                return false;

            try
            {
                NativeArray<DecalRuntimeStateDTO> stateArray = _stateHandle.Resolve(_vault);
                if (!stateArray.IsCreated || stateArray.Length <= 0)
                    return false;

                state = stateArray[0];
                return true;
            }
            finally
            {
                UnlockStateBuffer();
            }
        }

        public static bool TryGetLatestTelemetry(out DecalTelemetryEntry entry)
        {
            entry = default;
            if (!EnsureInitialized())
                return false;

            if (!TryLockTelemetryBuffer())
                return false;

            try
            {
                NativeArray<DecalTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
                int count = telemetry.IsCreated ? math.min(telemetry.Length, TelemetryCapacity) : 0;
                if (count <= 0)
                    return false;

                int index = _telemetryCursor - 1;
                if (index < 0)
                    index = count - 1;

                entry = telemetry[index];
                return entry.Frame != 0u || entry.TotalWritten != 0u || entry.ActiveDecals != 0u;
            }
            finally
            {
                UnlockTelemetryBuffer();
            }
        }

        public static bool TryAcquireDecalBufferRead(
            out NativeArray<DecalInstanceDTO> decals,
            out int activeCount,
            out Vector3 cameraWorldPosition)
        {
            decals = default;
            activeCount = 0;
            cameraWorldPosition = _lastCameraWorldPosition;
            if (!EnsureInitialized())
                return false;

            if (!TryLockDecalReadBuffers())
                return false;

            bool success = false;
            try
            {
                decals = _instancesHandle.Resolve(_vault);
                NativeArray<DecalRuntimeStateDTO> stateArray = _stateHandle.Resolve(_vault);
                if (!decals.IsCreated || !stateArray.IsCreated || stateArray.Length <= 0)
                    return false;

                DecalRuntimeStateDTO state = stateArray[0];
                activeCount = math.clamp(state.ActiveCount, 0, math.min(decals.Length, MaxCapacity));
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                    ReleaseDecalBufferRead();
            }
        }

        public static void ReleaseDecalBufferRead()
        {
            UnlockDecalReadBuffers();
        }

        public static unsafe bool TryLoadMaterialProfilesCsv(string csvPath, out int profilesWritten)
        {
            profilesWritten = 0;
            if (!EnsureInitialized() || string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                return false;

            if (!TryLockProfileCsvBuffers())
                return false;

            try
            {
                NativeArray<byte> scratch = _csvScratchHandle.Resolve(_vault);
                NativeArray<DecalMaterialProfileDTO> profiles = _materialProfileHandle.Resolve(_vault);
                if (!scratch.IsCreated || !profiles.IsCreated || scratch.Length <= 0 || profiles.Length <= 0)
                    return false;

                int bytesRead = 0;
                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                using FileStream stream = File.OpenRead(csvPath);
                long streamLength = stream.Length;
                if (streamLength <= 0L || streamLength > scratch.Length)
                    return false;

                int expectedBytes = (int)streamLength;
                while (bytesRead < expectedBytes)
                {
                    int read = stream.Read(new Span<byte>(scratchPtr + bytesRead, expectedBytes - bytesRead));
                    if (read <= 0)
                        return false;

                    bytesRead += read;
                }

                profilesWritten = ParseMaterialProfilesCsv(
                    new ReadOnlySpan<byte>(scratchPtr, bytesRead),
                    profiles);
                _materialProfileCount = profilesWritten;
                return profilesWritten > 0;
            }
            catch (Exception)
            {
                profilesWritten = 0;
                return false;
            }
            finally
            {
                UnlockProfileCsvBuffers();
            }
        }

        public static int GetLoadedMaterialProfileCount()
        {
            return math.max(0, _materialProfileCount);
        }

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
            IDataVault resolvedVault = _vault ?? GlobalRegistry.DataVault;
            if (resolvedVault == null)
                return false;

            if (!_requests.IsCreated)
            {
                _requests = new NativeQueue<DecalRequestSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<DecalRequestSignal>[1024 prewarm] - presentation-only decal request lane - owner: SHINOBU_149
                NativeMemorySentinel.RegisterNativeQueue(
                    _requests,
                    RequestQueuePrewarmCapacity,
                    LogOwner,
                    nameof(_requests),
                    NativeAllocationLifetime.Session);
                _queueRegistered = true;
                PrewarmQueue(RequestQueuePrewarmCapacity);
            }

            if (ReferenceEquals(_vault, resolvedVault) &&
                _instancesHandle.IsCreated &&
                _uploadHandle.IsCreated &&
                _stateHandle.IsCreated &&
                _telemetryHandle.IsCreated &&
                _tuningHandle.IsCreated &&
                _materialProfileHandle.IsCreated &&
                _csvScratchHandle.IsCreated)
            {
                return true;
            }

            _vault = resolvedVault;
            _instancesHandle = _vault.GetBufferHandle<DecalInstanceDTO>(
                DynamicDecalVaultBufferIds.Instances,
                MaxCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _uploadHandle = _vault.GetBufferHandle<DecalInstanceDTO>(
                DynamicDecalVaultBufferIds.UploadScratch,
                MaxCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _stateHandle = _vault.GetBufferHandle<DecalRuntimeStateDTO>(
                DynamicDecalVaultBufferIds.RuntimeState,
                1,
                OwnerSystem,
                NativeArrayOptions.ClearMemory);
            _telemetryHandle = _vault.GetBufferHandle<DecalTelemetryEntry>(
                DynamicDecalVaultBufferIds.TelemetryRing,
                TelemetryCapacity,
                OwnerSystem,
                NativeArrayOptions.ClearMemory);
            _tuningHandle = _vault.GetBufferHandle<DecalTuningDTO>(
                DynamicDecalVaultBufferIds.Tuning,
                1,
                OwnerSystem,
                NativeArrayOptions.ClearMemory);
            _materialProfileHandle = _vault.GetBufferHandle<DecalMaterialProfileDTO>(
                DynamicDecalVaultBufferIds.MaterialProfiles,
                MaxMaterialProfiles,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = _vault.GetBufferHandle<byte>(
                DynamicDecalVaultBufferIds.CsvScratch,
                CsvScratchBytes,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            SeedDefaultTuning();
            return _instancesHandle.IsCreated &&
                   _uploadHandle.IsCreated &&
                   _stateHandle.IsCreated &&
                   _telemetryHandle.IsCreated &&
                   _tuningHandle.IsCreated &&
                   _materialProfileHandle.IsCreated &&
                   _csvScratchHandle.IsCreated;
        }

        private static void SeedDefaultTuning()
        {
            NativeArray<DecalTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return;

            DecalTuningDTO tuning = tuningArray[0];
            if (tuning.Revision != 0u)
                return;

            tuning.BaseFadeTimeSeconds = 7.5f;
            tuning.MaximumOverkillCapacity = MaxCapacity;
            tuning.AtlasMipmapBias = 0f;
            tuning.ProjectionDepthMeters = 0.18f;
            tuning.LowTierCapacity = LowCapacity;
            tuning.BaseRadiusMeters = 0.55f;
            tuning.Revision = 1u;
            tuning.Flags = 0u;
            tuningArray[0] = tuning;
        }

        private static DecalTuningDTO ResolveDefaultTuning()
        {
            DecalTuningDTO tuning = default;
            tuning.BaseFadeTimeSeconds = 7.5f;
            tuning.MaximumOverkillCapacity = MaxCapacity;
            tuning.ProjectionDepthMeters = 0.18f;
            tuning.LowTierCapacity = LowCapacity;
            tuning.BaseRadiusMeters = 0.55f;
            return tuning;
        }

        private static DecalTuningDTO ResolveLiveTuning()
        {
            DecalTuningDTO defaults = ResolveDefaultTuning();
            if (_vault == null || !_tuningHandle.IsCreated || !TryLockTuningBuffer())
                return defaults;

            try
            {
                NativeArray<DecalTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
                if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                    return defaults;

                return SanitizeTuning(tuningArray[0], defaults.BaseFadeTimeSeconds, MaxCapacity);
            }
            finally
            {
                UnlockTuningBuffer();
            }
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
            tuning.AtlasMipmapBias = math.clamp(math.isfinite(tuning.AtlasMipmapBias) ? tuning.AtlasMipmapBias : 0f, -2f, 4f);
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

        private static bool TryLockRuntimeBuffers()
        {
            if (_vault == null)
                return false;

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.Instances, OwnerSystem))
                return false;

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.UploadScratch, OwnerSystem))
            {
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.Instances, OwnerSystem);
                return false;
            }

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem))
            {
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.UploadScratch, OwnerSystem);
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.Instances, OwnerSystem);
                return false;
            }

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.TelemetryRing, OwnerSystem))
            {
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem);
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.UploadScratch, OwnerSystem);
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.Instances, OwnerSystem);
                return false;
            }

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.Tuning, OwnerSystem))
            {
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.TelemetryRing, OwnerSystem);
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem);
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.UploadScratch, OwnerSystem);
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.Instances, OwnerSystem);
                return false;
            }

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.MaterialProfiles, OwnerSystem))
            {
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.Tuning, OwnerSystem);
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.TelemetryRing, OwnerSystem);
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem);
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.UploadScratch, OwnerSystem);
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.Instances, OwnerSystem);
                return false;
            }

            return true;
        }

        private static void UnlockRuntimeBuffers()
        {
            if (_vault == null)
                return;

            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.MaterialProfiles, OwnerSystem);
            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.Tuning, OwnerSystem);
            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.TelemetryRing, OwnerSystem);
            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem);
            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.UploadScratch, OwnerSystem);
            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.Instances, OwnerSystem);
        }

        private static bool TryLockUploadTelemetryBuffers()
        {
            if (_vault == null)
                return false;

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem))
                return false;

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.TelemetryRing, OwnerSystem))
            {
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem);
                return false;
            }

            return true;
        }

        private static void UnlockUploadTelemetryBuffers()
        {
            if (_vault == null)
                return;

            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.TelemetryRing, OwnerSystem);
            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem);
        }

        private static bool TryLockStateBuffer()
        {
            return _vault != null && _vault.TryLockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem);
        }

        private static void UnlockStateBuffer()
        {
            if (_vault != null)
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem);
        }

        private static bool TryLockTelemetryBuffer()
        {
            return _vault != null && _vault.TryLockBuffer(DynamicDecalVaultBufferIds.TelemetryRing, OwnerSystem);
        }

        private static void UnlockTelemetryBuffer()
        {
            if (_vault != null)
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.TelemetryRing, OwnerSystem);
        }

        private static bool TryLockDecalReadBuffers()
        {
            if (_vault == null)
                return false;

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.Instances, OwnerSystem))
                return false;

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem))
            {
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.Instances, OwnerSystem);
                return false;
            }

            return true;
        }

        private static void UnlockDecalReadBuffers()
        {
            if (_vault == null)
                return;

            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.RuntimeState, OwnerSystem);
            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.Instances, OwnerSystem);
        }

        private static bool TryLockTuningBuffer()
        {
            return _vault != null && _vault.TryLockBuffer(DynamicDecalVaultBufferIds.Tuning, OwnerSystem);
        }

        private static void UnlockTuningBuffer()
        {
            if (_vault != null)
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.Tuning, OwnerSystem);
        }

        private static bool TryLockProfileCsvBuffers()
        {
            if (_vault == null)
                return false;

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.CsvScratch, OwnerSystem))
                return false;

            if (!_vault.TryLockBuffer(DynamicDecalVaultBufferIds.MaterialProfiles, OwnerSystem))
            {
                _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.CsvScratch, OwnerSystem);
                return false;
            }

            return true;
        }

        private static void UnlockProfileCsvBuffers()
        {
            if (_vault == null)
                return;

            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.MaterialProfiles, OwnerSystem);
            _vault.TryUnlockBuffer(DynamicDecalVaultBufferIds.CsvScratch, OwnerSystem);
        }

        private static void TryIngestGlobalImpactSignals()
        {
            int unityFrame = Time.frameCount;
            if (_lastSignalSnapshotUnityFrame == unityFrame)
                return;

            _lastSignalSnapshotUnityFrame = unityFrame;
            uint maxHighSpeedFrame = _lastIngestedHighSpeedFrame;
            uint maxCombatDamageFrame = _lastIngestedCombatDamageFrame;
            bool highSpeedAccepted = false;
            bool combatDamageAccepted = false;
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

                float3 normal = -SanitizeNormal(signal.Direction, new float3(0f, 1f, 0f));
                uint materialHash = signal.DamageType != 0u ? signal.DamageType : signal.SourceHash;
                uint flags = materialHash == DynamicDecalMaterialHashes.HullDent
                    ? DynamicDecalFlags.HullImpact
                    : DynamicDecalFlags.Ballistic;
                EnqueueSignalImpact(
                    signal.ImpactAup,
                    normal,
                    materialHash,
                    signal.SourceHash,
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
            float damage,
            float velocity,
            uint frame,
            uint flags)
        {
            DecalTuningDTO tuning = ResolveLiveTuning();
            DecalRequestSignal request = default;
            request.ImpactAup = impactAup;
            request.Normal = SanitizeNormal(normal, new float3(0f, 1f, 0f));
            if (TryResolveMaterialProfile(profileHash != 0u ? profileHash : materialHash, out DecalMaterialProfileDTO profile))
            {
                request.RadiusMeters = profile.RadiusMeters;
                request.ProjectionDepthMeters = profile.ProjectionDepthMeters;
                request.LifetimeSeconds = profile.LifetimeSeconds;
                request.MaterialHash = profile.AtlasSlice;
            }
            else
            {
                request.RadiusMeters = ResolveBallisticRadius(damage, velocity, tuning);
                request.ProjectionDepthMeters = tuning.ProjectionDepthMeters;
                request.LifetimeSeconds = ResolveBallisticLifetime(materialHash, damage, tuning);
                request.MaterialHash = materialHash;
            }

            request.Flags = DynamicDecalFlags.Active | flags;
            request.StableSeed = Mix(materialHash ^ profileHash ^ frame);
            request.SourceFrame = frame;
            TryEnqueueRequest(in request);
        }

        private static bool TryEnqueueRequest(in DecalRequestSignal request)
        {
            if (!_requests.IsCreated)
                return false;

            if (_requests.Count >= RequestQueuePrewarmCapacity)
            {
                AccumulateDroppedIngress(1);
                return false;
            }

            _requests.Enqueue(request);
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
            return new double3(
                (gridX * cellSizeMeters) + localX,
                (gridY * cellSizeMeters) + localY,
                (gridZ * cellSizeMeters) + localZ);
        }

        private static void PrewarmQueue(int count)
        {
            DecalRequestSignal value = default;
            int safeCount = math.max(0, count);
            for (int i = 0; i < safeCount; i++)
                _requests.Enqueue(value);
            for (int i = 0; i < safeCount; i++)
                _requests.TryDequeue(out _);
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
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    MathGuard.IsFinite(in snapshot.Aup))
                {
                    return snapshot.Aup.ToAbsoluteDouble3();
                }

                var playerMovement = playerContext.PlayerMovement;
                if (playerMovement != null)
                {
                    AbsoluteUniversePosition currentAup = playerMovement.CurrentAup;
                    if (MathGuard.IsFinite(in currentAup))
                        return currentAup.ToAbsoluteDouble3();
                }
            }

            return HectonFloatingOrigin.CurrentTotalOffsetDouble;
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

        private static bool TryResolveMaterialProfile(uint sourceHash, out DecalMaterialProfileDTO profile)
        {
            profile = default;
            if (sourceHash == 0u || _materialProfileCount <= 0)
                return false;

            NativeArray<DecalMaterialProfileDTO> profiles = _materialProfileHandle.Resolve(_vault);
            int capacity = profiles.IsCreated ? profiles.Length : 0;
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
            NativeArray<DecalTelemetryEntry> telemetry,
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

            DecalTelemetryEntry entry = default;
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
        }

        private static void MarkFault(uint flags)
        {
            if (!EnsureInitialized())
                return;

            if (!TryLockStateBuffer())
                return;

            try
            {
                NativeArray<DecalRuntimeStateDTO> stateArray = _stateHandle.Resolve(_vault);
                if (!stateArray.IsCreated || stateArray.Length <= 0)
                    return;

                DecalRuntimeStateDTO state = stateArray[0];
                state.Flags |= flags;
                stateArray[0] = state;
            }
            finally
            {
                UnlockStateBuffer();
            }
        }

        private static void DumpBlackBox(uint reasonFlags)
        {
            if (_dumpedFault || !EnsureInitialized())
                return;

            if (!TryLockTelemetryBuffer())
                return;

            try
            {
                NativeArray<DecalTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
                if (!telemetry.IsCreated)
                    return;

                _dumpedFault = true;
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string directory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string dumpPath = Path.Combine(directory, DumpFileName);
                using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(DumpMagic);
                writer.Write(reasonFlags);
                writer.Write(TelemetryCapacity);
                writer.Write(_telemetryCursor);
                int count = math.min(telemetry.Length, TelemetryCapacity);
                for (int i = 0; i < count; i++)
                {
                    int index = _telemetryCursor + i;
                    if (index >= count)
                        index -= count;

                    DecalTelemetryEntry entry = telemetry[index];
                    writer.Write(entry.Frame);
                    writer.Write(entry.ActiveDecals);
                    writer.Write(entry.NewDecals);
                    writer.Write(entry.UploadCount);
                    writer.Write(entry.GpuUploadMicroseconds);
                    writer.Write(entry.CpuMicroseconds);
                    writer.Write(entry.GlobalQualityWeight);
                    writer.Write(entry.ThermalPressure01);
                    writer.Write(entry.Flags);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.DroppedThisFrame);
                    writer.Write(entry.TotalWritten);
                    writer.Write(entry.MaxActiveThisFrame);
                    writer.Write(entry.LastBallisticFrame);
                }
            }
            catch (Exception)
            {
                _dumpedFault = false;
            }
            finally
            {
                UnlockTelemetryBuffer();
            }
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
        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
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
        private static uint RotateLeft(uint value, int shift)
        {
            return (value << shift) | (value >> (32 - shift));
        }

#if UNITY_EDITOR
        private static int OffsetOf<T>(string fieldName)
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
#endif
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ClearDecalsJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public DecalInstanceDTO* Decals;
        public int Capacity;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Capacity)
                return;

            ref DecalInstanceDTO decal = ref UnsafeUtility.AsRef<DecalInstanceDTO>(Decals + index);
            decal.Opacity01 = 0f;
            decal.Flags = 0u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockDecalRequestsJob : IJobParallelFor
    {
        public NativeQueue<DecalRequestSignal>.ParallelWriter Requests;
        public int Count;
        public uint Frame;
        public double3 OriginAup;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count)
                return;

            uint seed = DynamicDecalVaultRuntime.Mix((uint)(index + 1) * 0x9E3779B9u);
            float angle = (seed & 1023u) * (6.28318530718f / 1024f);
            float radius = 2.0f + ((seed >> 10) & 31u) * 0.22f;
            float3 normal = math.normalize(new float3(math.sin(angle) * 0.35f, 1f, math.cos(angle) * 0.35f));
            DecalRequestSignal request = default;
            request.ImpactAup = OriginAup + new double3(math.cos(angle) * radius, ((index & 15) - 8) * 0.12f, math.sin(angle) * radius);
            request.Normal = normal;
            request.RadiusMeters = 0.22f + ((seed >> 16) & 15u) * 0.035f;
            request.ProjectionDepthMeters = 0.18f;
            request.LifetimeSeconds = 4.0f + ((seed >> 21) & 7u) * 0.5f;
            request.MaterialHash = seed % DynamicDecalVaultRuntime.AtlasSliceCount;
            request.Flags = DynamicDecalFlags.Active | DynamicDecalFlags.Mock;
            request.StableSeed = seed;
            request.SourceFrame = Frame;
            Requests.Enqueue(request);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateDecalMatricesJob : IJob
    {
        public NativeQueue<DecalRequestSignal> Requests;
        [NoAlias, NativeDisableUnsafePtrRestriction] public DecalInstanceDTO* Decals;
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
            while (processed < maxRequests && Requests.TryDequeue(out DecalRequestSignal request))
            {
                processed++;
                if (!TryBuildMatrix(in request, out float4x4 matrix, out uint materialIndex))
                {
                    state.Flags |= DynamicDecalFlags.NonFinite;
                    state.DroppedThisFrame++;
                    continue;
                }

                int index = state.CurrentWriteIndex;
                if ((uint)index >= (uint)capacity)
                    index = 0;

                ref DecalInstanceDTO decal = ref UnsafeUtility.AsRef<DecalInstanceDTO>(Decals + index);
                decal.LocalToWorld = matrix;
                decal.MaterialHash = materialIndex;
                decal.Opacity01 = 1f;
                decal.LifetimeSeconds = math.max(0.1f, math.isfinite(request.LifetimeSeconds) && request.LifetimeSeconds > 0f ? request.LifetimeSeconds : DefaultLifetimeSeconds);
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

        private bool TryBuildMatrix(in DecalRequestSignal request, out float4x4 matrix, out uint materialIndex)
        {
            matrix = float4x4.identity;
            materialIndex = ResolveAtlasIndex(request.MaterialHash);
            double3 local = request.ImpactAup - CameraAup;
            if (!math.all(math.isfinite(local)))
                return false;

            float3 position = (float3)local;
            if (!math.all(math.isfinite(position)))
                return false;

            float3 surfaceNormal = NormalizeOrDefault(request.Normal, new float3(0f, 1f, 0f));
            float3 zAxis = -surfaceNormal;
            float3 basis = math.abs(surfaceNormal.y) < 0.92f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            float3 xAxis = NormalizeOrDefault(math.cross(basis, zAxis), new float3(1f, 0f, 0f));
            float3 yAxis = NormalizeOrDefault(math.cross(zAxis, xAxis), new float3(0f, 1f, 0f));

            uint seed = request.StableSeed != 0u
                ? request.StableSeed
                : DynamicDecalVaultRuntime.Mix(math.asuint(position.x) ^ math.asuint(position.y) ^ math.asuint(position.z) ^ request.MaterialHash);
            float roll = (seed & 65535u) * (6.28318530718f / 65535f);
            math.sincos(roll, out float sinRoll, out float cosRoll);
            float3 rolledX = (xAxis * cosRoll) + (yAxis * sinRoll);
            float3 rolledY = (yAxis * cosRoll) - (xAxis * sinRoll);
            float radius = math.max(0.025f, math.isfinite(request.RadiusMeters) && request.RadiusMeters > 0f ? request.RadiusMeters : DefaultRadiusMeters);
            float depth = math.max(0.01f, math.isfinite(request.ProjectionDepthMeters) && request.ProjectionDepthMeters > 0f ? request.ProjectionDepthMeters : DefaultProjectionDepthMeters);
            matrix = new float4x4(
                new float4(rolledX * radius, 0f),
                new float4(rolledY * radius, 0f),
                new float4(zAxis * depth, 0f),
                new float4(position, 1f));
            return math.all(math.isfinite(matrix.c0)) &&
                   math.all(math.isfinite(matrix.c1)) &&
                   math.all(math.isfinite(matrix.c2)) &&
                   math.all(math.isfinite(matrix.c3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveAtlasIndex(uint materialHash)
        {
            if (materialHash < DynamicDecalVaultRuntime.AtlasSliceCount)
                return materialHash;

            return DynamicDecalVaultRuntime.Mix(materialHash) & (DynamicDecalVaultRuntime.AtlasSliceCount - 1u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeOrDefault(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && lengthSq > 0.0001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct DecayDecalOpacityJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public DecalInstanceDTO* Decals;
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
                ref DecalInstanceDTO decal = ref UnsafeUtility.AsRef<DecalInstanceDTO>(Decals + i);
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

                float lifetime = math.max(0.1f, math.isfinite(decal.LifetimeSeconds) && decal.LifetimeSeconds > 0f ? decal.LifetimeSeconds : baseLifetime);
                float lifetimeScale = baseLifetime * math.rcp(math.max(lifetime, 0.1f));
                opacity = math.max(0f, opacity - (decay * lifetimeScale));
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
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public DecalInstanceDTO* Decals;
        [NoAlias, NativeDisableUnsafePtrRestriction] public DecalInstanceDTO* Upload;
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
                ref readonly DecalInstanceDTO decal = ref UnsafeUtility.AsRef<DecalInstanceDTO>(Decals + cursor);
                if ((decal.Flags & DynamicDecalFlags.Active) != 0u && decal.Opacity01 > 0.0001f)
                {
                    ref DecalInstanceDTO destination = ref UnsafeUtility.AsRef<DecalInstanceDTO>(Upload + write);
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct DynamicDecalMappedUploadJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<DecalInstanceDTO> Source;
        [NoAlias, NativeDisableUnsafePtrRestriction] public DecalInstanceDTO* Destination;
        public int Count;

        public void Execute()
        {
            int safeCount = math.min(math.max(0, Count), Source.IsCreated ? Source.Length : 0);
            if (safeCount <= 0 || Destination == null)
                return;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Source);
            UnsafeUtility.MemCpy(Destination, sourcePtr, (long)UnsafeUtility.SizeOf<DecalInstanceDTO>() * safeCount);
        }
    }

#if UNITY_EDITOR
    internal static class DynamicDecalLayoutEditorValidator
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void ValidateOnLoad()
        {
            if (!DynamicDecalVaultRuntime.ValidateDecalInstanceLayout())
                Debug.LogError("SHINOBU_149 decal DTO layout mismatch: expected 80B with matrix[0], material[64], opacity[68], lifetime[72], flags[76].");
        }

        [UnityEditor.MenuItem("HECTON-8/Rendering/Validate Dynamic Decal Layout")]
        private static void ValidateMenu()
        {
            if (DynamicDecalVaultRuntime.ValidateDecalInstanceLayout())
                Debug.Log("SHINOBU_149 decal DTO layout valid: 80B explicit struct matches shader ABI.");
            else
                Debug.LogError("SHINOBU_149 decal DTO layout mismatch: shader ABI is unsafe.");
        }
    }
#endif
}
