using System;
#if UNITY_EDITOR
using System.IO;
#endif
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    public unsafe sealed class AsyncBuoyancyReadbackRuntime : MonoBehaviour, IGlobalRegistryHotSwapListener, IOriginShiftListener
    {
        private const float TwoPi = 6.2831853071795864769f;
        private const string WaveHeightSamplerKernelName = "SampleBuoyancyReadbackRequests";
        private const string WaveHeightSamplerComputeGuid = "60f3dfa702904496933e12041a3e1764";
        private const int PortableMaxComputeThreadsPerGroup = 256;
        private const int MaxDispatchGroupsPerDimension = 65535;
        private const uint SystemHashPre = 0x53323650u;  // S26P
        private const uint SystemHashSim = 0x53323653u;  // S26S
        private const uint SystemHashPost = 0x5332364Fu; // S26O
        private const uint SystemHashVisual = 0x53323656u; // S26V
        private const uint AsyncReadbackFaultEventHash = 0x41524654u; // ARFT
        private const uint AsyncReadbackFaultDumpHash = 0x41524450u; // ARDP

        private enum ReadbackDispatchStatus : byte
        {
            NoWork = 0,
            Dispatched = 1,
            Unavailable = 2,
            RingBacklog = 3
        }

        private static readonly int OceanWaveBufferId = Shader.PropertyToID("_H8OceanWaveParameters");
        private static readonly int BuoyancyReadbackRequestsId = Shader.PropertyToID("_H8BuoyancyReadbackRequests");
        private static readonly int WaveSampleCountId = Shader.PropertyToID("_H8WaveSampleCount");
        private static readonly int WaveSampleSeaLevelId = Shader.PropertyToID("_H8WaveSeaLevel");
        private static readonly int OceanTimeId = Shader.PropertyToID("_H8OceanSurfaceTime");
        private static readonly int OceanQualityId = Shader.PropertyToID("_H8OceanGlobalQualityWeight");
        private static readonly int OceanWaveCountId = Shader.PropertyToID("_H8OceanActiveWaveCount");
        private static readonly int OceanLocalProjectionId = Shader.PropertyToID("_H8OceanCameraAupLocalProjection");
        private static readonly int OceanWavePhaseBase0Id = Shader.PropertyToID("_H8OceanWavePhaseBase0");
        private static readonly int OceanWavePhaseBase1Id = Shader.PropertyToID("_H8OceanWavePhaseBase1");
        private static readonly int WaveSampleLodId = Shader.PropertyToID("_H8WaveSampleLod");
        private static readonly int OceanWakeDisplacementId = Shader.PropertyToID("_H8OceanWakeDisplacement");
        private static readonly int OceanShorelineDepthParamsId = Shader.PropertyToID("_H8OceanShorelineDepthParams");

        [Header("GPU Readback")]
        [SerializeField]
        private ComputeShader waveHeightSamplerCompute;

        [SerializeField]
        private bool enableGpuReadback = true;

        [SerializeField]
        private bool enableMockWhenGpuUnavailable = true;

        [SerializeField]
        private bool seedEmergencyLargeVesselSamples = true;

#if UNITY_EDITOR
        [SerializeField]
        private Transform cameraAupAnchor;
#endif

        [SerializeField]
        private float seaLevel = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;

        [Header("Sampling")]
        [SerializeField, Range(1, 64)]
        private int minimumSampleCount = 4;

        [SerializeField, Range(4, AsyncBuoyancyReadbackConstants.RequestCapacity)]
        private int maximumSampleCount = 128;

        [SerializeField]
        private float fallbackLargeVesselLengthMeters = 42f;

        [SerializeField]
        private float fallbackLargeVesselBeamMeters = 12f;

        [SerializeField]
        private float fallbackLargeVesselInsetMeters = 1.2f;

#if UNITY_EDITOR
        [Header("Cold Data")]
        [SerializeField]
        private bool loadVehicleSamplingProfilesOnEnable = true;

        [SerializeField]
        private string vehicleSamplingProfilesCsvRelativePath = AsyncBuoyancyReadbackConstants.CsvRelativePath;
#endif

        private IDataVault _dataVault;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private VaultGenerationHandle<ReadbackRequestDTO> _requestsHandle;
        private VaultGenerationHandle<ReadbackRequestDTO> _completedRequestsHandle;
        private VaultGenerationHandle<ReadbackResolvedHeightDTO> _resolvedHeightsHandle;
        private VaultGenerationHandle<ReadbackResultStateDTO> _resultStatesHandle;
        private VaultGenerationHandle<ReadbackTuningDTO> _tuningHandle;
        private VaultGenerationHandle<ReadbackTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<ReadbackRequestDTO> _mockRingHandle;
        private VaultGenerationHandle<AsyncBuoyancyWaveParametersDTO> _fallbackWavesHandle;
        private VaultGenerationHandle<VehicleSamplingProfileDTO> _vehicleProfilesHandle;
        private VaultGenerationHandle<AsyncReadbackCounterDTO> _counterHandle;

        private GraphicsBuffer _requestBuffer0;
        private GraphicsBuffer _requestBuffer1;
        private GraphicsBuffer _requestBuffer2;
        private GraphicsBuffer _requestUploadBuffer0;
        private GraphicsBuffer _requestUploadBuffer1;
        private GraphicsBuffer _requestUploadBuffer2;
        private AsyncGPUReadbackRequest _readbackRequest0;
        private AsyncGPUReadbackRequest _readbackRequest1;
        private AsyncGPUReadbackRequest _readbackRequest2;
        private ReadbackDataOwner _readbackData;
        private int _readbackCount0;
        private int _readbackCount1;
        private int _readbackCount2;

        private struct ReadbackDataOwner
        {
            public NativeArray<ReadbackRequestDTO> Data0;
            public NativeArray<ReadbackRequestDTO> Data1;
            public NativeArray<ReadbackRequestDTO> Data2;
        }
        private uint _readbackFrame0;
        private uint _readbackFrame1;
        private uint _readbackFrame2;
        private byte _readbackActive0;
        private byte _readbackActive1;
        private byte _readbackActive2;

        private GraphicsBuffer _waveParametersBuffer0;
        private GraphicsBuffer _waveParametersBuffer1;
        private GraphicsBuffer _waveParametersBuffer2;
        private uint _waveUploadHash0;
        private uint _waveUploadHash1;
        private uint _waveUploadHash2;
        private int _waveUploadCount0;
        private int _waveUploadCount1;
        private int _waveUploadCount2;
        private PreSimulationPhaseSystem _preSimulationSystem;
        private SimulationPhaseSystem _simulationSystem;
        private PostSimulationPhaseSystem _postSimulationSystem;
        private VisualSyncPhaseSystem _visualSyncSystem;
        private uint _frameIndex;
        private float _globalQualityWeight = 1f;
        private float _timeSeconds;
        private float _lastDispatchMicros;
        private float _lastApplyMicros;
        private int _queuedRequestCount;
        private int _dispatchRequestCount;
        private int _completedRequestCount;
        private int _readbackWriteSlot;
        private int _mockWriteSlot;
        private int _lastLatencyFrames;
        private int _droppedRequests;
        private int _failedRequests;
        private bool _coldSupportsComputeShaders;
        private bool _coldBootCompleted;
        private bool _registeredDispatcher;
        private bool _mockPathThisFrame;
        private bool _gpuDispatchQueuedForVisualSync;
        private bool _gpuUnavailableForNextSimulation;
        private bool _dumpRequested;
        private bool _dumpedFault;
        private bool _coreBlackboxWarmed;
        private bool _kernelResolved;
        private bool _hotSwapRegistered;
        private bool _registeredOriginShiftListener;
        private int _kernelIndex = -1;
        private int _threadGroupSize;
        private float _lastFixedDelta = 0.016666667f;
        private double3 _cameraAup;
        private double3 _cachedOriginAup;
        private uint _cachedOriginShiftSequence;
        private uint _cachedOriginShiftFlags;
        private double3 _publishedCameraAup;
        private uint _publishedCameraShiftSequence;
        private uint _publishedCameraFrame;
        private byte _hasPublishedCameraAup;
        private float _deadReckoningDecayOverride = -1f;
#if UNITY_EDITOR
        private static AsyncBuoyancyReadbackRuntime _activeRuntimeInstance;
        private bool _editorQualityOverrideActive;
        private float _editorQualityOverride = 1f;

        public static bool TryGetActiveRuntimeInstance(out AsyncBuoyancyReadbackRuntime runtime)
        {
            runtime = _activeRuntimeInstance;
            return runtime != null;
        }
#endif

        public bool TryQueueSample(double3 sampleAup, double3 cameraAup, uint entityHash)
        {
            return TryQueueSample(sampleAup, cameraAup, _cachedOriginShiftSequence, entityHash);
        }

        public bool TryQueueSample(double3 sampleAup, double3 cameraAup, uint cameraShiftSequence, uint entityHash)
        {
            if (!IsRuntimeReady())
                return false;

            NativeArray<ReadbackRequestDTO> requests = default;
            bool requestsLocked = false;
            IDataVault requestsWriteVault = null;
            try
            {
                requests = AcquireVaultWriteBuffer(_dataVault, in _requestsHandle, out requestsWriteVault);
                requestsLocked = requests.IsCreated;
                if (!requestsLocked || _queuedRequestCount >= math.min(requests.Length, AsyncBuoyancyReadbackConstants.RequestCapacity))
                    return false;

                if (cameraShiftSequence != _cachedOriginShiftSequence)
                    return false;

                double3 delta = sampleAup - cameraAup;
                if (!math.all(math.isfinite(delta)))
                    return false;

                ReadbackRequestDTO request = default;
                request.LocalXZ = new float2((float)delta.x, (float)delta.z);
                request.ResultHeight = 0f;
                request.EntityHash = entityHash != 0u ? entityHash : 1u;
                requests[_queuedRequestCount++] = request;
                _cameraAup = cameraAup;
                _publishedCameraAup = cameraAup;
                _publishedCameraShiftSequence = cameraShiftSequence;
                _publishedCameraFrame = _frameIndex;
                _hasPublishedCameraAup = 1;
                return true;
            }
            finally
            {
                if (requestsLocked)
                    ReleaseVaultWriteBuffer(requestsWriteVault, in _requestsHandle);
            }
        }

        public bool TryPublishCameraAupSnapshot(double3 cameraAup, uint shiftSequence, uint frameIndex)
        {
            if (!math.all(math.isfinite(cameraAup)) || shiftSequence != _cachedOriginShiftSequence)
                return false;

            _publishedCameraAup = cameraAup;
            _publishedCameraShiftSequence = shiftSequence;
            _publishedCameraFrame = frameIndex;
            _hasPublishedCameraAup = 1;
            return true;
        }

        public bool TryReadResolvedHeight(int sampleIndex, out ReadbackResolvedHeightDTO resolvedHeight)
        {
            resolvedHeight = default;
            if (!IsRuntimeReady())
                return false;

            NativeArray<ReadbackResolvedHeightDTO> resolved = ReadVaultBuffer(_dataVault, in _resolvedHeightsHandle);
            if (!resolved.IsCreated || (uint)sampleIndex >= (uint)resolved.Length)
                return false;

            resolvedHeight = resolved[sampleIndex];
            return resolvedHeight.EntityHash != 0u;
        }

#if UNITY_EDITOR
        public void ApplyEditorTuning(int maxSamplePoints, float deadReckoningDecayRate, bool qualityOverrideActive, float qualityOverride)
        {
            maximumSampleCount = math.clamp(maxSamplePoints, minimumSampleCount, AsyncBuoyancyReadbackConstants.RequestCapacity);
            _deadReckoningDecayOverride = math.saturate(deadReckoningDecayRate);
            _editorQualityOverrideActive = qualityOverrideActive;
            _editorQualityOverride = math.saturate(qualityOverride);
            WriteTuningSnapshot(_lastFixedDelta);
        }

        public bool TryOpenEditorViews(
            out NativeArray<ReadbackTuningDTO>.ReadOnly tuning,
            out NativeArray<ReadbackTelemetryEntry>.ReadOnly telemetry,
            out NativeArray<int>.ReadOnly cursor,
            out NativeArray<AsyncReadbackCounterDTO>.ReadOnly counters)
        {
            tuning = default;
            telemetry = default;
            cursor = default;
            counters = default;
            if (!IsRuntimeReady())
                return false;

            if (_dataVault == null ||
                !_dataVault.TryReadOnlyHandle(in _tuningHandle, out tuning) ||
                !_dataVault.TryReadOnlyHandle(in _telemetryRingHandle, out telemetry) ||
                !_dataVault.TryReadOnlyHandle(in _telemetryCursorHandle, out cursor) ||
                !_dataVault.TryReadOnlyHandle(in _counterHandle, out counters))
            {
                return false;
            }

            if (!tuning.IsCreated || !telemetry.IsCreated || !cursor.IsCreated || !counters.IsCreated)
                return false;

            return true;
        }
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR
            _activeRuntimeInstance = this;
            TryAutoAssignComputeShaderInEditor();
#endif
            CacheGraphicsCapabilitySnapshotCold();
            _dataVault = GlobalRegistry.DataVault;
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            TryRegisterHotSwapListener();
            TryRegisterOriginShiftListener();
            if (EnsureRuntimeReady())
            {
                EnsureGpuBuffers();
                WarmCoreBlackboxRoute();
#if UNITY_EDITOR
                if (loadVehicleSamplingProfilesOnEnable)
                    LoadVehicleSamplingProfiles();
#endif
            }
            TryRegisterDispatcherSystems();
        }

        private void CacheGraphicsCapabilitySnapshotCold()
        {
            _coldSupportsComputeShaders = SystemInfo.supportsComputeShaders;
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterOriginShiftListener();
            TryUnregisterDispatcherSystems();
            ReleaseGpuBuffers();
            _coreBlackboxWarmed = false;
            _oceanKinematicsService = null;
#if UNITY_EDITOR
            if (ReferenceEquals(_activeRuntimeInstance, this))
                _activeRuntimeInstance = null;
#endif
        }

        private void OnValidate()
        {
            minimumSampleCount = math.clamp(minimumSampleCount, 1, AsyncBuoyancyReadbackConstants.RequestCapacity);
            maximumSampleCount = math.clamp(maximumSampleCount, minimumSampleCount, AsyncBuoyancyReadbackConstants.RequestCapacity);
            fallbackLargeVesselLengthMeters = math.max(1f, fallbackLargeVesselLengthMeters);
            fallbackLargeVesselBeamMeters = math.max(1f, fallbackLargeVesselBeamMeters);
            fallbackLargeVesselInsetMeters = math.max(0f, fallbackLargeVesselInsetMeters);
            seaLevel = SanitizeFallbackSeaLevelY(seaLevel);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)
            {
                _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault nextVault = currentService as IDataVault;
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            _dataVault = nextVault;
            _coldBootCompleted = false;
            _coreBlackboxWarmed = false;
            if (EnsureRuntimeReady())
            {
                EnsureGpuBuffers();
                WarmCoreBlackboxRoute();
            }
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);
            float shiftSqrMagnitude = math.lengthsq(shiftOffset);
            if (!math.all(math.isfinite(shiftOffset)) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f ||
                !math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))
            {
                return;
            }

            ApplyOriginSnapshot(in shiftData);
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            _frameIndex = timing.FrameId;
            float fixedDelta = ResolveSimulationFixedDelta(in timing);
            _timeSeconds += fixedDelta;
            _globalQualityWeight = ResolveGlobalQualityWeight();
            if (_queuedRequestCount <= 0 || !math.all(math.isfinite(_cameraAup)))
                _cameraAup = ResolveCameraAup();
            if (!IsRuntimeReady())
            {
                _gpuDispatchQueuedForVisualSync = false;
                _mockPathThisFrame = false;
                return;
            }

            PrepareFrameRequests(fixedDelta);
            _gpuDispatchQueuedForVisualSync = _dispatchRequestCount > 0;
            _mockPathThisFrame = _dispatchRequestCount > 0 &&
                                 enableMockWhenGpuUnavailable &&
                                 (!enableGpuReadback || _gpuUnavailableForNextSimulation);
            UpdateCounterPreSimulation();
            _queuedRequestCount = 0;
        }

        private JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            return dependsOn;
        }

        private void ProcessReadbackSimulation(float fixedDelta)
        {
            if (!IsRuntimeReady())
                return;

            if (_mockPathThisFrame && enableMockWhenGpuUnavailable)
            {
                if (RunMockReadbackSimulation())
                {
                    _mockWriteSlot = (_mockWriteSlot + 1) % AsyncBuoyancyReadbackConstants.ReadbackRingSize;
                    _lastLatencyFrames = AsyncBuoyancyReadbackConstants.MockLatencyFrames;
                }
                else
                {
                    _mockPathThisFrame = false;
                    _completedRequestCount = 0;
                }
            }

            int activeStateCount = math.max(_dispatchRequestCount, _completedRequestCount);
            if (activeStateCount <= 0)
            {
                _lastApplyMicros = 0u;
                return;
            }

            int stateCount = math.min(activeStateCount, AsyncBuoyancyReadbackConstants.RequestCapacity);
            long applyStart = System.Diagnostics.Stopwatch.GetTimestamp();
            _lastApplyMicros = ApplyDelayedReadbackResults(stateCount, fixedDelta)
                ? ElapsedMicroseconds(applyStart)
                : 0u;
        }

        private bool RunMockReadbackSimulation()
        {
            NativeArray<ReadbackRequestDTO> requests = ReadVaultBuffer(_dataVault, in _requestsHandle);
            NativeArray<ReadbackRequestDTO> completedRead = ReadVaultBuffer(_dataVault, in _completedRequestsHandle);
            if (!requests.IsCreated || !completedRead.IsCreated)
                return false;

            int safeCapacity = math.min(
                AsyncBuoyancyReadbackConstants.RequestCapacity,
                math.min(requests.Length, completedRead.Length));
            int ringSize = AsyncBuoyancyReadbackConstants.ReadbackRingSize;
            if (safeCapacity <= 0 || ringSize <= 0)
                return false;

            int count = math.clamp(_dispatchRequestCount, 0, safeCapacity);
            int safeWriteSlot = math.clamp(_mockWriteSlot, 0, ringSize - 1);
            int latency = math.max(1, AsyncBuoyancyReadbackConstants.MockLatencyFrames);
            NativeArray<ReadbackRequestDTO> mockRing = default;
            bool mockLocked = false;
            IDataVault mockWriteVault = null;
            try
            {
                mockRing = AcquireVaultWriteBuffer(_dataVault, in _mockRingHandle, out mockWriteVault);
                mockLocked = mockRing.IsCreated;
                if (!mockLocked)
                    return false;

                int ringCapacity = math.min(mockRing.Length, safeCapacity * ringSize);
                int writeBase = safeWriteSlot * safeCapacity;
                int writable = math.max(0, math.min(count, ringCapacity - writeBase));
                ReadbackRequestDTO* ringPtr = (ReadbackRequestDTO*)mockRing.GetUnsafePtr();
                for (int i = 0; i < writable; i++)
                {
                    ReadbackRequestDTO request = requests[i];
                    request.ResultHeight = AsyncBuoyancyReadbackMath.ResolveMockLocalHeight(
                        request.LocalXZ,
                        _frameIndex,
                        _timeSeconds);
                    ref ReadbackRequestDTO ringRef = ref UnsafeUtility.AsRef<ReadbackRequestDTO>(ringPtr + writeBase + i);
                    ringRef = request;
                }
            }
            finally
            {
                if (mockLocked)
                    ReleaseVaultWriteBuffer(mockWriteVault, in _mockRingHandle);
            }

            int completed = 0;
            if (_frameIndex >= (uint)latency)
            {
                NativeArray<ReadbackRequestDTO> mockRingRead = ReadVaultBuffer(_dataVault, in _mockRingHandle);
                NativeArray<ReadbackRequestDTO> completedWrite = default;
                bool completedLocked = false;
                IDataVault completedWriteVault = null;
                try
                {
                    completedWrite = AcquireVaultWriteBuffer(_dataVault, in _completedRequestsHandle, out completedWriteVault);
                    completedLocked = completedWrite.IsCreated;
                    if (!mockRingRead.IsCreated || !completedLocked)
                        return false;

                    int ringCapacity = math.min(mockRingRead.Length, safeCapacity * ringSize);
                    int readSlot = safeWriteSlot - latency;
                    while (readSlot < 0)
                        readSlot += ringSize;
                    readSlot %= ringSize;
                    int readBase = readSlot * safeCapacity;
                    int readable = math.max(0, math.min(count, math.min(ringCapacity - readBase, completedWrite.Length)));
                    ReadbackRequestDTO* ringPtr = (ReadbackRequestDTO*)mockRingRead.GetUnsafeReadOnlyPtr();
                    ReadbackRequestDTO* completedPtr = (ReadbackRequestDTO*)completedWrite.GetUnsafePtr();
                    for (int i = 0; i < readable; i++)
                    {
                        ReadbackRequestDTO delayed = UnsafeUtility.AsRef<ReadbackRequestDTO>(ringPtr + readBase + i);
                        ref ReadbackRequestDTO completedRef = ref UnsafeUtility.AsRef<ReadbackRequestDTO>(completedPtr + i);
                        completedRef = delayed;
                    }

                    completed = readable;
                }
                finally
                {
                    if (completedLocked)
                        ReleaseVaultWriteBuffer(completedWriteVault, in _completedRequestsHandle);
                }
            }

            NativeArray<AsyncReadbackCounterDTO> counters = default;
            bool counterLocked = false;
            IDataVault counterWriteVault = null;
            try
            {
                counters = AcquireVaultWriteBuffer(_dataVault, in _counterHandle, out counterWriteVault);
                counterLocked = counters.IsCreated;
                if (!counterLocked || counters.Length <= 0)
                    return false;

                AsyncReadbackCounterDTO* counterPtr = (AsyncReadbackCounterDTO*)counters.GetUnsafePtr();
                ref AsyncReadbackCounterDTO counter = ref UnsafeUtility.AsRef<AsyncReadbackCounterDTO>(counterPtr);
                counter.DispatchCount = count;
                counter.CompletedCount = completed;
                counter.LastLatencyFrames = latency;
                counter.FrameIndex = _frameIndex;
                counter.Flags |= AsyncBuoyancyReadbackConstants.FlagMockPath;
                _completedRequestCount = completed;
                return true;
            }
            finally
            {
                if (counterLocked)
                    ReleaseVaultWriteBuffer(counterWriteVault, in _counterHandle);
            }
        }

        private bool ApplyDelayedReadbackResults(int stateCount, float fixedDelta)
        {
            if (stateCount <= 0)
                return true;

            float smoothingAlpha = AsyncBuoyancyReadbackMath.ResolveSmoothingAlpha();
            float deadReckoningDecay = ResolveDeadReckoningDecayRate();
            if (!WriteResolvedHeightsPass(stateCount, fixedDelta, smoothingAlpha, deadReckoningDecay))
                return false;

            if (!WriteResultStatesPass(
                    stateCount,
                    fixedDelta,
                    smoothingAlpha,
                    deadReckoningDecay,
                    out int maxStaleFrames,
                    out uint lastEntityHash,
                    out float lastLocalHeight,
                    out uint flags))
            {
                return false;
            }

            return WriteApplyCounter(maxStaleFrames, lastEntityHash, lastLocalHeight, flags);
        }

        private bool WriteResolvedHeightsPass(
            int stateCount,
            float fixedDelta,
            float smoothingAlpha,
            float deadReckoningDecay)
        {
            NativeArray<ReadbackRequestDTO> completed = ReadVaultBuffer(_dataVault, in _completedRequestsHandle);
            NativeArray<ReadbackResultStateDTO> states = ReadVaultBuffer(_dataVault, in _resultStatesHandle);
            if (!completed.IsCreated || !states.IsCreated)
                return false;

            NativeArray<ReadbackResolvedHeightDTO> resolved = default;
            bool resolvedLocked = false;
            IDataVault resolvedWriteVault = null;
            try
            {
                resolved = AcquireVaultWriteBuffer(_dataVault, in _resolvedHeightsHandle, out resolvedWriteVault);
                resolvedLocked = resolved.IsCreated;
                if (!resolvedLocked)
                    return false;

                int count = math.min(stateCount, math.min(resolved.Length, states.Length));
                int freshCount = math.min(math.max(0, _completedRequestCount), completed.Length);
                ReadbackResolvedHeightDTO* resolvedPtr = (ReadbackResolvedHeightDTO*)resolved.GetUnsafePtr();
                for (int i = 0; i < count; i++)
                {
                    bool hasFresh = i < freshCount;
                    ReadbackRequestDTO request = hasFresh ? completed[i] : default;
                    ResolveAppliedReadbackState(
                        states[i],
                        request,
                        hasFresh,
                        _cameraAup.y,
                        fixedDelta,
                        smoothingAlpha,
                        deadReckoningDecay,
                        AsyncBuoyancyReadbackConstants.MaxFreshAgeFrames,
                        _frameIndex,
                        out _,
                        out ReadbackResolvedHeightDTO resolvedValue);
                    ref ReadbackResolvedHeightDTO resolvedRef = ref UnsafeUtility.AsRef<ReadbackResolvedHeightDTO>(resolvedPtr + i);
                    resolvedRef = resolvedValue;
                }

                return true;
            }
            finally
            {
                if (resolvedLocked)
                    ReleaseVaultWriteBuffer(resolvedWriteVault, in _resolvedHeightsHandle);
            }
        }

        private bool WriteResultStatesPass(
            int stateCount,
            float fixedDelta,
            float smoothingAlpha,
            float deadReckoningDecay,
            out int maxStaleFrames,
            out uint lastEntityHash,
            out float lastLocalHeight,
            out uint flags)
        {
            maxStaleFrames = 0;
            lastEntityHash = 0u;
            lastLocalHeight = 0f;
            flags = 0u;
            NativeArray<ReadbackRequestDTO> completed = ReadVaultBuffer(_dataVault, in _completedRequestsHandle);
            if (!completed.IsCreated)
                return false;

            NativeArray<ReadbackResultStateDTO> states = default;
            bool statesLocked = false;
            IDataVault statesWriteVault = null;
            try
            {
                states = AcquireVaultWriteBuffer(_dataVault, in _resultStatesHandle, out statesWriteVault);
                statesLocked = states.IsCreated;
                if (!statesLocked)
                    return false;

                int count = math.min(stateCount, states.Length);
                int freshCount = math.min(math.max(0, _completedRequestCount), completed.Length);
                ReadbackResultStateDTO* statePtr = (ReadbackResultStateDTO*)states.GetUnsafePtr();
                for (int i = 0; i < count; i++)
                {
                    bool hasFresh = i < freshCount;
                    ReadbackRequestDTO request = hasFresh ? completed[i] : default;
                    ReadbackResultStateDTO previousState = UnsafeUtility.AsRef<ReadbackResultStateDTO>(statePtr + i);
                    ResolveAppliedReadbackState(
                        previousState,
                        request,
                        hasFresh,
                        _cameraAup.y,
                        fixedDelta,
                        smoothingAlpha,
                        deadReckoningDecay,
                        AsyncBuoyancyReadbackConstants.MaxFreshAgeFrames,
                        _frameIndex,
                        out ReadbackResultStateDTO stateValue,
                        out ReadbackResolvedHeightDTO resolvedValue);
                    ref ReadbackResultStateDTO stateRef = ref UnsafeUtility.AsRef<ReadbackResultStateDTO>(statePtr + i);
                    stateRef = stateValue;
                    if (i == 0)
                    {
                        maxStaleFrames = stateValue.StaleFrames;
                        lastEntityHash = resolvedValue.EntityHash;
                        lastLocalHeight = resolvedValue.LocalHeight;
                        flags = resolvedValue.Flags;
                    }
                }

                return true;
            }
            finally
            {
                if (statesLocked)
                    ReleaseVaultWriteBuffer(statesWriteVault, in _resultStatesHandle);
            }
        }

        private bool WriteApplyCounter(int maxStaleFrames, uint lastEntityHash, float lastLocalHeight, uint flags)
        {
            NativeArray<AsyncReadbackCounterDTO> counters = default;
            bool counterLocked = false;
            IDataVault counterWriteVault = null;
            try
            {
                counters = AcquireVaultWriteBuffer(_dataVault, in _counterHandle, out counterWriteVault);
                counterLocked = counters.IsCreated;
                if (!counterLocked || counters.Length <= 0)
                    return false;

                AsyncReadbackCounterDTO* counterPtr = (AsyncReadbackCounterDTO*)counters.GetUnsafePtr();
                ref AsyncReadbackCounterDTO counter = ref UnsafeUtility.AsRef<AsyncReadbackCounterDTO>(counterPtr);
                counter.MaxStaleFrames = math.max(counter.MaxStaleFrames, maxStaleFrames);
                counter.LastEntityHash = lastEntityHash;
                counter.LastLocalHeight = lastLocalHeight;
                counter.Flags |= flags;
                return true;
            }
            finally
            {
                if (counterLocked)
                    ReleaseVaultWriteBuffer(counterWriteVault, in _counterHandle);
            }
        }

        private static void ResolveAppliedReadbackState(
            in ReadbackResultStateDTO previousState,
            in ReadbackRequestDTO request,
            bool hasFresh,
            double cameraAupY,
            float fixedDeltaTime,
            float smoothingAlpha,
            float deadReckoningDecayRate,
            int maxFreshAgeFrames,
            uint frameIndex,
            out ReadbackResultStateDTO state,
            out ReadbackResolvedHeightDTO resolved)
        {
            state = previousState;
            resolved = default;
            float dt = math.max(0.0001f, fixedDeltaTime);
            float invDt = math.rcp(math.max(dt, 0.0001f));
            float alpha = math.saturate(smoothingAlpha);
            float decay = math.saturate(deadReckoningDecayRate);
            uint flags = AsyncBuoyancyReadbackConstants.FlagActive;
            float localHeight;
            uint entityHash = state.EntityHash;
            if (hasFresh)
            {
                entityHash = request.EntityHash;
                float observed = math.isfinite(request.ResultHeight) ? request.ResultHeight : state.LastLocalHeight;
                float previous = math.isfinite(state.SmoothedLocalHeight) ? state.SmoothedLocalHeight : observed;
                float predicted = previous + (state.VelocityY * dt);
                localHeight = math.lerp(predicted, observed, alpha);
                float velocity = (localHeight - previous) * invDt;

                state.PreviousLocalHeight = previous;
                state.LastLocalHeight = observed;
                state.SmoothedLocalHeight = localHeight;
                state.DeadReckonedLocalHeight = localHeight;
                state.VelocityY = math.isfinite(velocity) ? velocity : 0f;
                state.LastLocalX = request.LocalXZ.x;
                state.LastLocalZ = request.LocalXZ.y;
                state.EntityHash = entityHash;
                state.LastFrameIndex = frameIndex;
                state.StaleFrames = 0;
                state.CameraAupY = cameraAupY;
                state.Flags = flags;
            }
            else
            {
                int stale = math.max(0, state.StaleFrames + 1);
                state.StaleFrames = stale;
                float predicted = state.SmoothedLocalHeight + (state.VelocityY * dt * math.min(stale, math.max(1, maxFreshAgeFrames)));
                float staleFactor = math.saturate((float)math.max(0, stale - maxFreshAgeFrames) * math.rcp(math.max(1f, maxFreshAgeFrames)));
                localHeight = math.lerp(predicted, state.SmoothedLocalHeight, staleFactor * decay);
                state.DeadReckonedLocalHeight = localHeight;
                state.VelocityY *= math.lerp(1f, 0.65f, staleFactor);
                flags |= AsyncBuoyancyReadbackConstants.FlagStale;
                if (stale > maxFreshAgeFrames)
                    flags |= AsyncBuoyancyReadbackConstants.FlagDeadReckoned;
                state.Flags = flags;
            }

            double heightAupY = cameraAupY + localHeight;
            bool finite = math.isfinite(localHeight) && math.isfinite(heightAupY);
            if (!finite)
            {
                localHeight = 0f;
                heightAupY = cameraAupY;
                flags |= AsyncBuoyancyReadbackConstants.FlagNonFinite;
            }

            state.LastHeightAupY = heightAupY;
            resolved.HeightAupY = heightAupY;
            resolved.LocalHeight = localHeight;
            resolved.VelocityY = state.VelocityY;
            resolved.EntityHash = entityHash;
            resolved.FrameIndex = frameIndex;
            resolved.Flags = flags;
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            if (!IsRuntimeReady())
                return;

            float fixedDelta = ResolveSimulationFixedDelta(in timing);
            ProcessReadbackSimulation(fixedDelta);
            WriteTuningSnapshot(fixedDelta);
            UpdateCounterPostSimulation();
            WriteTelemetryDirect();
            if (_dumpRequested || _lastLatencyFrames > 4)
                _dumpRequested = true;
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            ConsumeGpuReadbacksNoWait();
            FlushQueuedGpuReadbackDispatch();
            if (_dumpRequested || _lastLatencyFrames > 4)
                DumpTelemetryOnce();
        }

        private void FlushQueuedGpuReadbackDispatch()
        {
            if (!_gpuDispatchQueuedForVisualSync)
                return;

            _gpuDispatchQueuedForVisualSync = false;
            if (!IsRuntimeReady())
            {
                _gpuUnavailableForNextSimulation = true;
                return;
            }

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            ReadbackDispatchStatus dispatchStatus = DispatchGpuReadback();
            _lastDispatchMicros = ElapsedMicroseconds(start);
            _gpuUnavailableForNextSimulation = dispatchStatus == ReadbackDispatchStatus.Unavailable;
        }

        private ReadbackDispatchStatus DispatchGpuReadback()
        {
            if (!enableGpuReadback || !TryResolveKernel() || _dispatchRequestCount <= 0)
                return _dispatchRequestCount <= 0 ? ReadbackDispatchStatus.NoWork : ReadbackDispatchStatus.Unavailable;

            if (!HasGpuBuffers())
                return ReadbackDispatchStatus.Unavailable;

            if (!TryResolveWaveBuffer(out NativeArray<AsyncBuoyancyWaveParametersDTO> waves) || !waves.IsCreated || waves.Length < AsyncBuoyancyReadbackConstants.WaveCapacity)
                return ReadbackDispatchStatus.Unavailable;

            int slot = _readbackWriteSlot;
            ref byte activeRef = ref ResolveReadbackActiveRef(slot);
            if (activeRef != 0)
            {
                _droppedRequests += _dispatchRequestCount;
                return ReadbackDispatchStatus.RingBacklog;
            }

            NativeArray<ReadbackRequestDTO> requests = ReadVaultBuffer(_dataVault, in _requestsHandle);
            if (!requests.IsCreated)
                return ReadbackDispatchStatus.Unavailable;

            GraphicsBuffer requestBuffer = ResolveRequestBuffer(slot);
            if (requestBuffer == null)
                return ReadbackDispatchStatus.Unavailable;

            GraphicsBuffer requestUploadBuffer = ResolveRequestUploadBuffer(slot);
            if (requestUploadBuffer == null)
                return ReadbackDispatchStatus.Unavailable;

            GraphicsBuffer waveBuffer = ResolveWaveParametersBuffer(slot);
            if (waveBuffer == null)
                return ReadbackDispatchStatus.Unavailable;

            GraphicsBufferUploadUtility.UploadNativeArrayAndCopyWholeBuffer(
                requestUploadBuffer,
                requestBuffer,
                requests,
                _dispatchRequestCount);
            int waveUploadCount = math.min(waves.Length, AsyncBuoyancyReadbackConstants.WaveCapacity);
            uint waveUploadHash = ComputeWaveParametersHash(waves, waveUploadCount);
            ref uint waveHashRef = ref ResolveWaveUploadHashRef(slot);
            ref int waveCountRef = ref ResolveWaveUploadCountRef(slot);
            if (waveHashRef != waveUploadHash || waveCountRef != waveUploadCount)
            {
                UploadNativeArrayToGraphicsBuffer(waveBuffer, waves, waveUploadCount);
                waveHashRef = waveUploadHash;
                waveCountRef = waveUploadCount;
            }

            float shaderQuality = math.saturate(math.select(
                AsyncBuoyancyReadbackConstants.AuthoritativeQualityWeight,
                _globalQualityWeight,
                math.isfinite(_globalQualityWeight)));
            int activeWaveCount = ResolveShaderActiveWaveIndex(AsyncBuoyancyReadbackConstants.WaveCapacity * 3, shaderQuality);
            float maxWavelength = ResolveMaxWavelength(waves);
            ResolveWavePhaseBases(_cameraAup, waves, activeWaveCount + 1, out float4 phaseBase0, out float4 phaseBase1);
            Vector4 shorelineParams = Shader.GetGlobalVector(OceanShorelineDepthParamsId);
            float wakeWorldSize = ResolveWakeWorldSize(shorelineParams);
            float2 cameraProjection = ResolveCameraLocalProjection(_cameraAup, wakeWorldSize);

            waveHeightSamplerCompute.SetBuffer(_kernelIndex, OceanWaveBufferId, waveBuffer);
            waveHeightSamplerCompute.SetBuffer(_kernelIndex, BuoyancyReadbackRequestsId, requestBuffer);
            waveHeightSamplerCompute.SetInt(WaveSampleCountId, _dispatchRequestCount);
            waveHeightSamplerCompute.SetFloat(WaveSampleSeaLevelId, ResolveRuntimeSeaLevelY());
            waveHeightSamplerCompute.SetFloat(OceanTimeId, _timeSeconds);
            waveHeightSamplerCompute.SetFloat(OceanQualityId, shaderQuality);
            waveHeightSamplerCompute.SetInt(OceanWaveCountId, activeWaveCount);
            waveHeightSamplerCompute.SetVector(OceanLocalProjectionId, ToVector4(new float4(cameraProjection.x, cameraProjection.y, maxWavelength, wakeWorldSize)));
            waveHeightSamplerCompute.SetVector(OceanWavePhaseBase0Id, ToVector4(phaseBase0));
            waveHeightSamplerCompute.SetVector(OceanWavePhaseBase1Id, ToVector4(phaseBase1));
            Vector4 waveSampleLod = default;
            waveSampleLod.x = maxWavelength;
            waveSampleLod.y = activeWaveCount;
            waveSampleLod.z = shaderQuality;
            waveSampleLod.w = _timeSeconds;
            waveHeightSamplerCompute.SetVector(WaveSampleLodId, waveSampleLod);
            Texture wakeDisplacement = Shader.GetGlobalTexture(OceanWakeDisplacementId);
            waveHeightSamplerCompute.SetTexture(_kernelIndex, OceanWakeDisplacementId, wakeDisplacement != null ? wakeDisplacement : Texture2D.blackTexture);
            waveHeightSamplerCompute.SetVector(OceanShorelineDepthParamsId, shorelineParams);

            int groups = ResolveDispatchGroups(_dispatchRequestCount, _threadGroupSize);
            if (groups <= 0)
                return ReadbackDispatchStatus.Unavailable;

            waveHeightSamplerCompute.Dispatch(_kernelIndex, groups, 1, 1);
            int readbackBytes = ResolveReadbackByteCount(requestBuffer, _dispatchRequestCount);
            if (readbackBytes <= 0)
                return ReadbackDispatchStatus.Unavailable;

            ref AsyncGPUReadbackRequest requestRef = ref ResolveReadbackRequestRef(slot);
            ref int countRef = ref ResolveReadbackCountRef(slot);
            ref uint frameRef = ref ResolveReadbackFrameRef(slot);
            if (!EnsureReadbackData(slot))
                return ReadbackDispatchStatus.Unavailable;

            ref NativeArray<ReadbackRequestDTO> readbackDataRef = ref ResolveReadbackDataRef(slot);
            requestRef = AsyncGPUReadback.RequestIntoNativeArray(ref readbackDataRef, requestBuffer, readbackBytes, 0, null);
            if (requestRef.hasError)
                return ReadbackDispatchStatus.Unavailable;

            countRef = _dispatchRequestCount;
            frameRef = _frameIndex;
            activeRef = 1;
            _readbackWriteSlot = (_readbackWriteSlot + 1) % AsyncBuoyancyReadbackConstants.ReadbackRingSize;
            return ReadbackDispatchStatus.Dispatched;
        }

        private float ResolveRuntimeSeaLevelY()
        {
            return TryResolveOceanSeaLevelY(out float resolvedSeaLevelY)
                ? resolvedSeaLevelY
                : SanitizeFallbackSeaLevelY(seaLevel);
        }

        private bool TryResolveOceanSeaLevelY(out float resolvedSeaLevelY)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TrySanitizeOceanRuntimeSeaLevelY(oceanKinematics.SeaLevel, out resolvedSeaLevelY))
            {
                return true;
            }

            resolvedSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
            return false;
        }

        private static float SanitizeFallbackSeaLevelY(float value)
        {
            return TrySanitizeFallbackSeaLevelY(value, out float resolvedSeaLevelY)
                ? resolvedSeaLevelY
                : WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        }

        private static bool TrySanitizeOceanRuntimeSeaLevelY(float value, out float resolvedSeaLevelY)
        {
            if (math.isfinite(value) &&
                math.abs(value) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                resolvedSeaLevelY = value;
                return true;
            }

            resolvedSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
            return false;
        }

        private static bool TrySanitizeFallbackSeaLevelY(float value, out float resolvedSeaLevelY)
        {
            if (math.isfinite(value) &&
                math.abs(value) > 0.0001f &&
                math.abs(value) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                resolvedSeaLevelY = value;
                return true;
            }

            resolvedSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
            return false;
        }

        private void ConsumeGpuReadbacksNoWait()
        {
            _completedRequestCount = 0;
            for (int slot = 0; slot < AsyncBuoyancyReadbackConstants.ReadbackRingSize; slot++)
            {
                ref byte activeRef = ref ResolveReadbackActiveRef(slot);
                if (activeRef == 0)
                    continue;

                ref AsyncGPUReadbackRequest requestRef = ref ResolveReadbackRequestRef(slot);
                AsyncGPUReadbackRequest request = requestRef;
                if (!SystemDispatcher.IsAsyncReadbackReadyNoWait(request, out byte statusFlags))
                {
                    if ((statusFlags & 2) != 0)
                    {
                        _failedRequests++;
                        activeRef = 0;
                    }
                    continue;
                }

                int count = math.min(ResolveReadbackCountRef(slot), AsyncBuoyancyReadbackConstants.RequestCapacity);
                if (count <= 0)
                {
                    activeRef = 0;
                    continue;
                }

                NativeArray<ReadbackRequestDTO> completed = default;
                bool completedLocked = false;
                IDataVault completedWriteVault = null;
                try
                {
                    completed = AcquireVaultWriteBuffer(_dataVault, in _completedRequestsHandle, out completedWriteVault);
                    completedLocked = completed.IsCreated;
                    if (!completedLocked)
                        continue;

                    NativeArray<ReadbackRequestDTO> readbackData = ResolveReadbackDataRef(slot);
                    int copyCount = math.min(count, math.min(completed.Length, readbackData.Length));
                    for (int i = 0; i < copyCount; i++)
                        completed[i] = readbackData[i];

                    activeRef = 0;
                    _completedRequestCount = copyCount;
                    _lastLatencyFrames = math.max(0, unchecked((int)_frameIndex - (int)ResolveReadbackFrameRef(slot)));
                    if (_lastLatencyFrames > 4)
                        _dumpRequested = true;
                    return;
                }
                finally
                {
                    if (completedLocked)
                        ReleaseVaultWriteBuffer(completedWriteVault, in _completedRequestsHandle);
                }
            }
        }

        private void PrepareFrameRequests(float fixedDeltaTime)
        {
            if (_queuedRequestCount <= 0 && seedEmergencyLargeVesselSamples)
                SeedEmergencySamples();

            _dispatchRequestCount = math.clamp(_queuedRequestCount, 0, AsyncBuoyancyReadbackConstants.RequestCapacity);
            WriteTuningSnapshot(fixedDeltaTime);
        }

        private void SeedEmergencySamples()
        {
            NativeArray<ReadbackRequestDTO> requests = default;
            bool requestsLocked = false;
            IDataVault requestsWriteVault = null;
            try
            {
                requests = AcquireVaultWriteBuffer(_dataVault, in _requestsHandle, out requestsWriteVault);
                requestsLocked = requests.IsCreated;
                if (!requestsLocked)
                    return;

                VehicleSamplingProfileDTO profile = ResolvePrimaryVehicleProfile();
                int count = AsyncBuoyancyReadbackMath.ResolveSampleBudget(
                    math.max(1, profile.MinSamples),
                    math.max(profile.MinSamples, profile.MaxSamples),
                    _globalQualityWeight);
                count = math.min(count, math.min(requests.Length, AsyncBuoyancyReadbackConstants.RequestCapacity));
                float length = math.max(1f, profile.LengthMeters);
                float beam = math.max(1f, profile.BeamMeters);
                float inset = math.max(0f, profile.InsetMeters);
                float usableLength = math.max(0.5f, length - (inset * 2f));
                float usableBeam = math.max(0.5f, beam - (inset * 2f));
                float columnEstimateSq = math.max(1f, count * (usableLength / math.max(0.25f, usableBeam)));
                float columnEstimate = columnEstimateSq * math.rsqrt(math.max(columnEstimateSq, 0.0001f));
                int columns = math.max(1, (int)math.ceil(columnEstimate));
                int rows = math.max(1, (int)math.ceil((float)count / columns));
                uint baseHash = profile.VehicleHash != 0u ? profile.VehicleHash : 0x53483236u;

                for (int i = 0; i < count; i++)
                {
                    int column = i % columns;
                    int row = i / columns;
                    float x01 = columns <= 1 ? 0.5f : (float)column / (columns - 1);
                    float z01 = rows <= 1 ? 0.5f : (float)row / (rows - 1);
                    ReadbackRequestDTO request = default;
                    request.LocalXZ = new float2((x01 - 0.5f) * usableLength, (z01 - 0.5f) * usableBeam);
                    request.ResultHeight = 0f;
                    request.EntityHash = baseHash ^ ((uint)(i + 1) * 0x9E3779B9u);
                    requests[i] = request;
                }

                _queuedRequestCount = count;
            }
            finally
            {
                if (requestsLocked)
                    ReleaseVaultWriteBuffer(requestsWriteVault, in _requestsHandle);
            }
        }

        private VehicleSamplingProfileDTO ResolvePrimaryVehicleProfile()
        {
            if (_dataVault == null ||
                !_dataVault.TryReadOnlyHandle(in _vehicleProfilesHandle, out NativeArray<VehicleSamplingProfileDTO>.ReadOnly profiles))
            {
                return BuildFallbackVehicleProfile();
            }

            if (profiles.IsCreated && profiles.Length > 0 && profiles[0].VehicleHash != 0u)
                return profiles[0];

            return BuildFallbackVehicleProfile();
        }

        private VehicleSamplingProfileDTO BuildFallbackVehicleProfile()
        {
            VehicleSamplingProfileDTO profile = default;
            profile.VehicleHash = 0x53483236u;
            profile.LengthMeters = fallbackLargeVesselLengthMeters;
            profile.BeamMeters = fallbackLargeVesselBeamMeters;
            profile.DraftMeters = 4f;
            profile.MinSamples = minimumSampleCount;
            profile.MaxSamples = maximumSampleCount;
            profile.InsetMeters = fallbackLargeVesselInsetMeters;
            profile.Flags = AsyncBuoyancyReadbackConstants.FlagActive;
            return profile;
        }

        private bool EnsureRuntimeReady()
        {
            if (_coldBootCompleted && HandlesReady())
                return true;

            IDataVault vault = _dataVault;
            if (vault == null || !AsyncBuoyancyReadbackLayout.Validate())
                return false;

            if (!EnsureVaultDescriptor(vault, ref _requestsHandle, AsyncBuoyancyReadbackBufferIds.Requests, AsyncBuoyancyReadbackConstants.RequestCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultDescriptor(vault, ref _completedRequestsHandle, AsyncBuoyancyReadbackBufferIds.CompletedRequests, AsyncBuoyancyReadbackConstants.RequestCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultDescriptor(vault, ref _resolvedHeightsHandle, AsyncBuoyancyReadbackBufferIds.ResolvedHeights, AsyncBuoyancyReadbackConstants.RequestCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultDescriptor(vault, ref _resultStatesHandle, AsyncBuoyancyReadbackBufferIds.ResultStates, AsyncBuoyancyReadbackConstants.RequestCapacity, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultDescriptor(vault, ref _tuningHandle, AsyncBuoyancyReadbackBufferIds.Tuning, 1, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultDescriptor(vault, ref _telemetryRingHandle, AsyncBuoyancyReadbackBufferIds.TelemetryRing, AsyncBuoyancyReadbackConstants.TelemetryCapacity, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultDescriptor(vault, ref _telemetryCursorHandle, AsyncBuoyancyReadbackBufferIds.TelemetryCursor, 1, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultDescriptor(vault, ref _mockRingHandle, AsyncBuoyancyReadbackBufferIds.MockRing, AsyncBuoyancyReadbackConstants.RequestCapacity * AsyncBuoyancyReadbackConstants.ReadbackRingSize, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultDescriptor(vault, ref _fallbackWavesHandle, AsyncBuoyancyReadbackBufferIds.FallbackWaves, AsyncBuoyancyReadbackConstants.WaveCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultDescriptor(vault, ref _vehicleProfilesHandle, AsyncBuoyancyReadbackBufferIds.VehicleSamplingProfiles, AsyncBuoyancyReadbackConstants.VehicleProfileCapacity, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultDescriptor(vault, ref _counterHandle, AsyncBuoyancyReadbackBufferIds.Counter, 1, NativeArrayOptions.ClearMemory))
            {
                return false;
            }

            SeedFallbackWavesIfNeeded();
            SeedDefaultVehicleProfileIfNeeded();
            _coldBootCompleted = true;
            return true;
        }

        private bool HandlesReady()
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   HasHandle(in _requestsHandle) &&
                   HasHandle(in _completedRequestsHandle) &&
                   HasHandle(in _resolvedHeightsHandle) &&
                   HasHandle(in _resultStatesHandle) &&
                   HasHandle(in _tuningHandle) &&
                   HasHandle(in _telemetryRingHandle) &&
                   HasHandle(in _telemetryCursorHandle) &&
                   HasHandle(in _mockRingHandle) &&
                   HasHandle(in _fallbackWavesHandle) &&
                   HasHandle(in _vehicleProfilesHandle) &&
                   HasHandle(in _counterHandle);
        }

        private bool IsRuntimeReady()
        {
            return _coldBootCompleted && HandlesReady();
        }

        private bool TryResolveWaveBuffer(out NativeArray<AsyncBuoyancyWaveParametersDTO> waves)
        {
            waves = ReadVaultBuffer(_dataVault, in _fallbackWavesHandle);
            return waves.IsCreated && waves.Length >= AsyncBuoyancyReadbackConstants.WaveCapacity;
        }

        private void SeedFallbackWavesIfNeeded()
        {
            NativeArray<AsyncBuoyancyWaveParametersDTO> waves = default;
            bool wavesLocked = false;
            IDataVault wavesWriteVault = null;
            try
            {
                waves = AcquireVaultWriteBuffer(_dataVault, in _fallbackWavesHandle, out wavesWriteVault);
                wavesLocked = waves.IsCreated;
                if (!wavesLocked || waves.Length < AsyncBuoyancyReadbackConstants.WaveCapacity)
                    return;
                if (math.any(waves[0].Wave1 != float4.zero))
                    return;

                AsyncBuoyancyWaveParametersDTO primary = default;
                primary.Wave1 = new float4(0.12f, 0.42f, 28f, 0.28f);
                primary.Wave2 = new float4(1.64f, 0.32f, 16f, -0.18f);
                primary.Wave3 = new float4(2.71f, 0.18f, 9f, 0.11f);
                primary.GlobalWindAndStorm = new float4(0.8f, 0.25f, 0.15f, 0f);
                AsyncBuoyancyWaveParametersDTO secondary = default;
                secondary.Wave1 = new float4(0.76f, 0.12f, 6f, 0.21f);
                secondary.Wave2 = new float4(2.32f, 0.10f, 4f, -0.17f);
                secondary.Wave3 = new float4(3.04f, 0.08f, 2.6f, 0.09f);
                secondary.GlobalWindAndStorm = new float4(0.8f, 0.25f, 0.15f, 0f);
                waves[0] = primary;
                waves[1] = secondary;
            }
            finally
            {
                if (wavesLocked)
                    ReleaseVaultWriteBuffer(wavesWriteVault, in _fallbackWavesHandle);
            }
        }

        private void SeedDefaultVehicleProfileIfNeeded()
        {
            NativeArray<VehicleSamplingProfileDTO> profiles = default;
            bool profilesLocked = false;
            IDataVault profilesWriteVault = null;
            try
            {
                profiles = AcquireVaultWriteBuffer(_dataVault, in _vehicleProfilesHandle, out profilesWriteVault);
                profilesLocked = profiles.IsCreated;
                if (!profilesLocked || profiles.Length <= 0 || profiles[0].VehicleHash != 0u)
                    return;

                profiles[0] = BuildFallbackVehicleProfile();
            }
            finally
            {
                if (profilesLocked)
                    ReleaseVaultWriteBuffer(profilesWriteVault, in _vehicleProfilesHandle);
            }
        }

        private bool EnsureGpuBuffers()
        {
            if (_requestBuffer0 == null)
                _requestBuffer0 = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<ReadbackRequestDTO>(AsyncBuoyancyReadbackConstants.RequestCapacity);
            if (_requestBuffer1 == null)
                _requestBuffer1 = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<ReadbackRequestDTO>(AsyncBuoyancyReadbackConstants.RequestCapacity);
            if (_requestBuffer2 == null)
                _requestBuffer2 = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<ReadbackRequestDTO>(AsyncBuoyancyReadbackConstants.RequestCapacity);
            if (_requestUploadBuffer0 == null)
                _requestUploadBuffer0 = GraphicsBufferUploadUtility.CreateStructuredUploadStagingBuffer<ReadbackRequestDTO>(AsyncBuoyancyReadbackConstants.RequestCapacity);
            if (_requestUploadBuffer1 == null)
                _requestUploadBuffer1 = GraphicsBufferUploadUtility.CreateStructuredUploadStagingBuffer<ReadbackRequestDTO>(AsyncBuoyancyReadbackConstants.RequestCapacity);
            if (_requestUploadBuffer2 == null)
                _requestUploadBuffer2 = GraphicsBufferUploadUtility.CreateStructuredUploadStagingBuffer<ReadbackRequestDTO>(AsyncBuoyancyReadbackConstants.RequestCapacity);

            if (_waveParametersBuffer0 == null)
                _waveParametersBuffer0 = CreateWaveParametersBuffer();
            if (_waveParametersBuffer1 == null)
                _waveParametersBuffer1 = CreateWaveParametersBuffer();
            if (_waveParametersBuffer2 == null)
                _waveParametersBuffer2 = CreateWaveParametersBuffer();

            return HasGpuBuffers();
        }

        private static GraphicsBuffer CreateWaveParametersBuffer()
        {
            return CreateStructuredLockBuffer<AsyncBuoyancyWaveParametersDTO>(AsyncBuoyancyReadbackConstants.WaveCapacity);
        }

        private static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                math.max(1, count),
                UnsafeUtility.SizeOf<T>());
        }

        private static void UploadNativeArrayToGraphicsBuffer<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : struct
        {
            GraphicsBufferUploadUtility.UploadNativeArray(destination, source, count);
        }

        private bool HasGpuBuffers()
        {
            return _requestBuffer0 != null &&
                   _requestBuffer1 != null &&
                   _requestBuffer2 != null &&
                   _requestUploadBuffer0 != null &&
                   _requestUploadBuffer1 != null &&
                   _requestUploadBuffer2 != null &&
                   _waveParametersBuffer0 != null &&
                   _waveParametersBuffer1 != null &&
                   _waveParametersBuffer2 != null;
        }

        private bool TryResolveKernel()
        {
            if (_kernelResolved)
                return _kernelIndex >= 0 && waveHeightSamplerCompute != null;

            _kernelResolved = true;
            _kernelIndex = -1;
            _threadGroupSize = 0;
            if (waveHeightSamplerCompute == null || !_coldSupportsComputeShaders)
                return false;

            try
            {
                if (!waveHeightSamplerCompute.HasKernel(WaveHeightSamplerKernelName))
                    return false;

                _kernelIndex = waveHeightSamplerCompute.FindKernel(WaveHeightSamplerKernelName);
            }
            catch (System.ObjectDisposedException)
            {
                _kernelIndex = -1;
                return false;
            }
            catch (System.InvalidOperationException)
            {
                _kernelIndex = -1;
                return false;
            }
            catch (System.ArgumentException)
            {
                _kernelIndex = -1;
                return false;
            }
            catch (MissingReferenceException)
            {
                _kernelIndex = -1;
                return false;
            }
            catch (UnityException)
            {
                _kernelIndex = -1;
                return false;
            }

            if (!TryResolveKernelThreadGroupSize1D(waveHeightSamplerCompute, _kernelIndex, out _threadGroupSize))
            {
                _kernelIndex = -1;
                _threadGroupSize = 0;
                return false;
            }

            return true;
        }

        private static bool TryResolveKernelThreadGroupSize1D(ComputeShader compute, int kernelIndex, out int threadGroupSize)
        {
            threadGroupSize = 0;
            if (compute == null || kernelIndex < 0)
                return false;

            uint sizeX;
            uint sizeY;
            uint sizeZ;
            try
            {
                if (!compute.IsSupported(kernelIndex))
                    return false;

                compute.GetKernelThreadGroupSizes(kernelIndex, out sizeX, out sizeY, out sizeZ);
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            catch (UnityEngine.MissingReferenceException)
            {
                return false;
            }
            catch (UnityEngine.UnityException)
            {
                return false;
            }
            if (sizeX == 0u || sizeY != 1u || sizeZ != 1u)
                return false;

            if (sizeX > (uint)PortableMaxComputeThreadsPerGroup)
                return false;

            threadGroupSize = (int)sizeX;
            return true;
        }

        private static int ResolveDispatchGroups(int value, int divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            if (groups <= 0L || groups > MaxDispatchGroupsPerDimension)
                return 0;

            return (int)groups;
        }

        private static int ResolveReadbackByteCount(GraphicsBuffer buffer, int requestCount)
        {
            if (buffer == null || requestCount <= 0)
                return 0;

            int stride = UnsafeUtility.SizeOf<ReadbackRequestDTO>();
            int safeCount = math.min(requestCount, math.max(0, buffer.count));
            long byteCount = (long)safeCount * stride;
            long maxBytes = (long)math.max(0, buffer.count) * math.max(1, buffer.stride);
            return byteCount > 0L && byteCount <= maxBytes ? (int)byteCount : 0;
        }

        private void ReleaseGpuBuffers()
        {
            CompletePendingReadbacksForRelease();
            DisposeReadbackData();
            DisposeRequestBuffer(ref _requestBuffer0);
            DisposeRequestBuffer(ref _requestBuffer1);
            DisposeRequestBuffer(ref _requestBuffer2);
            DisposeRequestBuffer(ref _requestUploadBuffer0);
            DisposeRequestBuffer(ref _requestUploadBuffer1);
            DisposeRequestBuffer(ref _requestUploadBuffer2);
            DisposeRequestBuffer(ref _waveParametersBuffer0);
            DisposeRequestBuffer(ref _waveParametersBuffer1);
            DisposeRequestBuffer(ref _waveParametersBuffer2);
            _waveUploadHash0 = 0u;
            _waveUploadHash1 = 0u;
            _waveUploadHash2 = 0u;
            _waveUploadCount0 = 0;
            _waveUploadCount1 = 0;
            _waveUploadCount2 = 0;
            ResetReadbackRingState();
        }

        private void ResetReadbackRingState()
        {
            _readbackRequest0 = default;
            _readbackRequest1 = default;
            _readbackRequest2 = default;
            _readbackCount0 = 0;
            _readbackCount1 = 0;
            _readbackCount2 = 0;
            _readbackFrame0 = 0u;
            _readbackFrame1 = 0u;
            _readbackFrame2 = 0u;
            _readbackActive0 = 0;
            _readbackActive1 = 0;
            _readbackActive2 = 0;
            _readbackWriteSlot = 0;
            _mockWriteSlot = 0;
            _queuedRequestCount = 0;
            _dispatchRequestCount = 0;
            _completedRequestCount = 0;
            _mockPathThisFrame = false;
            _gpuDispatchQueuedForVisualSync = false;
            _gpuUnavailableForNextSimulation = false;
        }

        private void CompletePendingReadbacksForRelease()
        {
            if (_readbackActive0 == 0 && _readbackActive1 == 0 && _readbackActive2 == 0)
                return;

            // BLOCKING_SYNC_POINT: teardown only; readback source buffers must outlive in-flight AsyncGPUReadback.
            AsyncGPUReadback.WaitAllRequests();
            _readbackActive0 = 0;
            _readbackActive1 = 0;
            _readbackActive2 = 0;
        }

        private void DisposeReadbackData()
        {
            DisposeReadbackData(ref _readbackData.Data0);
            DisposeReadbackData(ref _readbackData.Data1);
            DisposeReadbackData(ref _readbackData.Data2);
        }

        private static void DisposeReadbackData(ref NativeArray<ReadbackRequestDTO> data)
        {
            H8Memory.Release(ref data, SystemID.Physics);
        }

        private static void DisposeRequestBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Dispose();
            buffer = null;
        }

        private GraphicsBuffer ResolveRequestBuffer(int slot)
        {
            if (slot == 0)
                return _requestBuffer0;
            if (slot == 1)
                return _requestBuffer1;
            return _requestBuffer2;
        }

        private GraphicsBuffer ResolveRequestUploadBuffer(int slot)
        {
            if (slot == 0)
                return _requestUploadBuffer0;
            if (slot == 1)
                return _requestUploadBuffer1;
            return _requestUploadBuffer2;
        }

        private GraphicsBuffer ResolveWaveParametersBuffer(int slot)
        {
            if (slot == 0)
                return _waveParametersBuffer0;
            if (slot == 1)
                return _waveParametersBuffer1;
            return _waveParametersBuffer2;
        }

        private ref uint ResolveWaveUploadHashRef(int slot)
        {
            if (slot == 0)
                return ref _waveUploadHash0;
            if (slot == 1)
                return ref _waveUploadHash1;
            return ref _waveUploadHash2;
        }

        private ref int ResolveWaveUploadCountRef(int slot)
        {
            if (slot == 0)
                return ref _waveUploadCount0;
            if (slot == 1)
                return ref _waveUploadCount1;
            return ref _waveUploadCount2;
        }

        private ref AsyncGPUReadbackRequest ResolveReadbackRequestRef(int slot)
        {
            if (slot == 0)
                return ref _readbackRequest0;
            if (slot == 1)
                return ref _readbackRequest1;
            return ref _readbackRequest2;
        }

        private ref NativeArray<ReadbackRequestDTO> ResolveReadbackDataRef(int slot)
        {
            if (slot == 0)
                return ref _readbackData.Data0;
            if (slot == 1)
                return ref _readbackData.Data1;
            return ref _readbackData.Data2;
        }

        private bool EnsureReadbackData(int slot)
        {
            if (slot == 0)
            {
                if (_readbackData.Data0.IsCreated && _readbackData.Data0.Length >= AsyncBuoyancyReadbackConstants.RequestCapacity)
                    return true;

                H8Memory.Release(ref _readbackData.Data0, SystemID.Physics);
                if (_readbackData.Data0.IsCreated)
                    return false;

                _readbackData.Data0 = H8Memory.Allocate<ReadbackRequestDTO>(
                    AsyncBuoyancyReadbackConstants.RequestCapacity,
                    SystemID.Physics,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                return _readbackData.Data0.IsCreated && _readbackData.Data0.Length >= AsyncBuoyancyReadbackConstants.RequestCapacity;
            }

            if (slot == 1)
            {
                if (_readbackData.Data1.IsCreated && _readbackData.Data1.Length >= AsyncBuoyancyReadbackConstants.RequestCapacity)
                    return true;

                H8Memory.Release(ref _readbackData.Data1, SystemID.Physics);
                if (_readbackData.Data1.IsCreated)
                    return false;

                _readbackData.Data1 = H8Memory.Allocate<ReadbackRequestDTO>(
                    AsyncBuoyancyReadbackConstants.RequestCapacity,
                    SystemID.Physics,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                return _readbackData.Data1.IsCreated && _readbackData.Data1.Length >= AsyncBuoyancyReadbackConstants.RequestCapacity;
            }

            if (_readbackData.Data2.IsCreated && _readbackData.Data2.Length >= AsyncBuoyancyReadbackConstants.RequestCapacity)
                return true;

            H8Memory.Release(ref _readbackData.Data2, SystemID.Physics);
            if (_readbackData.Data2.IsCreated)
                return false;

            _readbackData.Data2 = H8Memory.Allocate<ReadbackRequestDTO>(
                AsyncBuoyancyReadbackConstants.RequestCapacity,
                SystemID.Physics,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            return _readbackData.Data2.IsCreated && _readbackData.Data2.Length >= AsyncBuoyancyReadbackConstants.RequestCapacity;
        }

        private ref int ResolveReadbackCountRef(int slot)
        {
            if (slot == 0)
                return ref _readbackCount0;
            if (slot == 1)
                return ref _readbackCount1;
            return ref _readbackCount2;
        }

        private ref uint ResolveReadbackFrameRef(int slot)
        {
            if (slot == 0)
                return ref _readbackFrame0;
            if (slot == 1)
                return ref _readbackFrame1;
            return ref _readbackFrame2;
        }

        private ref byte ResolveReadbackActiveRef(int slot)
        {
            if (slot == 0)
                return ref _readbackActive0;
            if (slot == 1)
                return ref _readbackActive1;
            return ref _readbackActive2;
        }

        private void WriteTuningSnapshot(float fixedDelta)
        {
            NativeArray<ReadbackTuningDTO> tuning = default;
            bool tuningLocked = false;
            IDataVault tuningWriteVault = null;
            try
            {
                tuning = AcquireVaultWriteBuffer(_dataVault, in _tuningHandle, out tuningWriteVault);
                tuningLocked = tuning.IsCreated;
                if (!tuningLocked || tuning.Length <= 0)
                    return;

                ReadbackTuningDTO value = default;
                value.CameraAup = _cameraAup;
                value.GlobalQualityWeight = _globalQualityWeight;
                value.FixedDeltaTime = fixedDelta;
                value.ActiveRequestCount = _dispatchRequestCount;
                value.ActiveCompletedCount = _completedRequestCount;
                value.MinSampleCount = minimumSampleCount;
                value.MaxSampleCount = maximumSampleCount;
                value.FrameIndex = _frameIndex;
                value.Flags = _mockPathThisFrame ? AsyncBuoyancyReadbackConstants.FlagMockPath : AsyncBuoyancyReadbackConstants.FlagGpuPath;
                value.SmoothingAlpha = AsyncBuoyancyReadbackMath.ResolveSmoothingAlpha();
                value.DeadReckoningDecayRate = ResolveDeadReckoningDecayRate();
                tuning[0] = value;
            }
            finally
            {
                if (tuningLocked)
                    ReleaseVaultWriteBuffer(tuningWriteVault, in _tuningHandle);
            }
        }

        private void UpdateCounterPreSimulation()
        {
            NativeArray<AsyncReadbackCounterDTO> counters = default;
            bool countersLocked = false;
            IDataVault countersWriteVault = null;
            try
            {
                counters = AcquireVaultWriteBuffer(_dataVault, in _counterHandle, out countersWriteVault);
                countersLocked = counters.IsCreated;
                if (!countersLocked || counters.Length <= 0)
                    return;

                AsyncReadbackCounterDTO counter = counters[0];
                counter.QueuedCount = _queuedRequestCount;
                counter.DispatchCount = _dispatchRequestCount;
                counter.ActiveRingSlots = CountActiveReadbackSlots();
                counter.DroppedRequests = _droppedRequests;
                counter.FailedRequests = _failedRequests;
                counter.FrameIndex = _frameIndex;
                counter.DispatchMicros = _lastDispatchMicros;
                counter.Flags = _mockPathThisFrame ? AsyncBuoyancyReadbackConstants.FlagMockPath : AsyncBuoyancyReadbackConstants.FlagGpuPath;
                counters[0] = counter;
            }
            finally
            {
                if (countersLocked)
                    ReleaseVaultWriteBuffer(countersWriteVault, in _counterHandle);
            }
        }

        private void UpdateCounterPostSimulation()
        {
            NativeArray<AsyncReadbackCounterDTO> counters = default;
            bool countersLocked = false;
            IDataVault countersWriteVault = null;
            try
            {
                counters = AcquireVaultWriteBuffer(_dataVault, in _counterHandle, out countersWriteVault);
                countersLocked = counters.IsCreated;
                if (!countersLocked || counters.Length <= 0)
                    return;

                AsyncReadbackCounterDTO counter = counters[0];
                counter.CompletedCount = _completedRequestCount;
                counter.ActiveRingSlots = CountActiveReadbackSlots();
                counter.LastLatencyFrames = _lastLatencyFrames;
                counter.DroppedRequests = _droppedRequests;
                counter.FailedRequests = _failedRequests;
                counter.ApplyMicros = _lastApplyMicros;
                counter.DispatchMicros = _lastDispatchMicros;
                if (_lastLatencyFrames > 4)
                    counter.Flags |= AsyncBuoyancyReadbackConstants.FlagDumpedLatency;
                counters[0] = counter;
            }
            finally
            {
                if (countersLocked)
                    ReleaseVaultWriteBuffer(countersWriteVault, in _counterHandle);
            }
        }

        private void WriteTelemetryDirect()
        {
            NativeArray<AsyncReadbackCounterDTO> counters = ReadVaultBuffer(_dataVault, in _counterHandle);
            NativeArray<ReadbackTelemetryEntry> telemetryRead = ReadVaultBuffer(_dataVault, in _telemetryRingHandle);
            if (!telemetryRead.IsCreated || telemetryRead.Length <= 0)
                return;

            AsyncReadbackCounterDTO counter = counters.IsCreated && counters.Length > 0 ? counters[0] : default;
            int cursorValue;
            NativeArray<int> cursor = default;
            bool cursorLocked = false;
            IDataVault cursorWriteVault = null;
            try
            {
                cursor = AcquireVaultWriteBuffer(_dataVault, in _telemetryCursorHandle, out cursorWriteVault);
                cursorLocked = cursor.IsCreated;
                if (!cursorLocked || cursor.Length <= 0)
                    return;

                cursorValue = cursor[0];
                cursor[0] = cursorValue + 1;
            }
            finally
            {
                if (cursorLocked)
                    ReleaseVaultWriteBuffer(cursorWriteVault, in _telemetryCursorHandle);
            }

            int write = math.max(0, cursorValue) % telemetryRead.Length;
            ReadbackTelemetryEntry entry = default;
            entry.FrameIndex = _frameIndex;
            entry.RequestedSamples = counter.DispatchCount;
            entry.CompletedSamples = counter.CompletedCount;
            entry.ActiveRingSlots = counter.ActiveRingSlots;
            entry.ReadbackLatencyFrames = counter.LastLatencyFrames;
            entry.DroppedRequests = counter.DroppedRequests;
            entry.FailedRequests = counter.FailedRequests;
            entry.MaxStaleFrames = counter.MaxStaleFrames;
            entry.GlobalQualityWeight = _globalQualityWeight;
            entry.ApplyMicros = counter.ApplyMicros;
            entry.DispatchMicros = counter.DispatchMicros;
            entry.SmoothedAlpha = AsyncBuoyancyReadbackMath.ResolveSmoothingAlpha();
            entry.Flags = counter.Flags;
            entry.LastEntityHash = counter.LastEntityHash;
            entry.LastLocalHeight = counter.LastLocalHeight;

            NativeArray<ReadbackTelemetryEntry> telemetry = default;
            bool telemetryLocked = false;
            IDataVault telemetryWriteVault = null;
            try
            {
                telemetry = AcquireVaultWriteBuffer(_dataVault, in _telemetryRingHandle, out telemetryWriteVault);
                telemetryLocked = telemetry.IsCreated;
                if (!telemetryLocked || telemetry.Length <= 0)
                    return;

                telemetry[math.min(write, telemetry.Length - 1)] = entry;
            }
            finally
            {
                if (telemetryLocked)
                    ReleaseVaultWriteBuffer(telemetryWriteVault, in _telemetryRingHandle);
            }
        }

        private int CountActiveReadbackSlots()
        {
            return (_readbackActive0 != 0 ? 1 : 0) +
                   (_readbackActive1 != 0 ? 1 : 0) +
                   (_readbackActive2 != 0 ? 1 : 0);
        }

        private double3 ResolveCameraAup()
        {
            if (TryResolvePublishedCameraAup(out double3 cameraAup))
                return cameraAup;

            double3 origin = ResolveCachedOriginAup();
#if UNITY_EDITOR
            if (cameraAupAnchor == null)
                return origin;

            Vector3 p = cameraAupAnchor.position;
            return origin + new double3(p.x, p.y, p.z);
#else
            return origin;
#endif
        }

        private bool TryResolvePublishedCameraAup(out double3 cameraAup)
        {
            cameraAup = _publishedCameraAup;
            return _hasPublishedCameraAup != 0 &&
                   _publishedCameraShiftSequence == _cachedOriginShiftSequence &&
                   math.all(math.isfinite(cameraAup));
        }

        private double3 ResolveCachedOriginAup()
        {
            double3 origin = _cachedOriginAup;
            return math.select(double3.zero, origin, math.isfinite(origin));
        }

        private void RefreshCachedOriginSnapshot()
        {
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
            ApplyOriginSnapshot(in shiftEvent);
        }

        private void ApplyOriginSnapshot(in OriginShiftEventData shiftData)
        {
            double3 origin = shiftData.NewTotalOffsetDouble;
            _cachedOriginAup = math.select(double3.zero, origin, math.isfinite(origin));
            _cachedOriginShiftSequence = shiftData.Sequence;
            _cachedOriginShiftFlags = shiftData.IsSafeTeleport != 0 ? 1u : 0u;
        }

#if UNITY_EDITOR
        private void LoadVehicleSamplingProfiles()
        {
            if (!IsRuntimeReady())
                return;

            string path = Path.Combine(Application.dataPath, "..", vehicleSamplingProfilesCsvRelativePath);
            path = Path.GetFullPath(path);
            if (!File.Exists(path))
                return;

            Span<byte> csvScratch = stackalloc byte[AsyncBuoyancyReadbackConstants.CsvImportByteCapacity];
            int bytesRead = ReadCsvFileIntoColdScratch(path, csvScratch);

            if (bytesRead <= 0)
                return;

            Span<VehicleSamplingProfileDTO> profileScratch = stackalloc VehicleSamplingProfileDTO[AsyncBuoyancyReadbackConstants.VehicleProfileCapacity];
            int profileCount = ParseVehicleSamplingProfilesCsv(csvScratch.Slice(0, bytesRead), profileScratch);
            if (profileCount <= 0)
                return;

            CommitVehicleSamplingProfiles(profileScratch.Slice(0, profileCount));
        }

        private int ParseVehicleSamplingProfilesCsv(ReadOnlySpan<byte> bytes, Span<VehicleSamplingProfileDTO> profiles)
        {
            int write = 0;
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length && write < profiles.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = bytes.Slice(lineStart, i - lineStart);
                if (TryParseVehicleProfileLine(line, out VehicleSamplingProfileDTO profile))
                    profiles[write++] = profile;
                lineStart = i + 1;
            }

            return write;
        }

        private void CommitVehicleSamplingProfiles(ReadOnlySpan<VehicleSamplingProfileDTO> stagedProfiles)
        {
            if (stagedProfiles.Length <= 0)
                return;

            NativeArray<VehicleSamplingProfileDTO> profiles = default;
            bool profilesLocked = false;
            IDataVault profilesWriteVault = null;
            try
            {
                profiles = AcquireVaultWriteBuffer(_dataVault, in _vehicleProfilesHandle, out profilesWriteVault);
                profilesLocked = profiles.IsCreated;
                if (!profilesLocked || profiles.Length <= 0)
                    return;

                int write = math.min(stagedProfiles.Length, profiles.Length);
                for (int i = 0; i < write; i++)
                    profiles[i] = stagedProfiles[i];

                for (int i = write; i < profiles.Length; i++)
                    profiles[i] = default;
            }
            finally
            {
                if (profilesLocked)
                    ReleaseVaultWriteBuffer(profilesWriteVault, in _vehicleProfilesHandle);
            }
        }

        private static int ReadCsvFileIntoColdScratch(string path, Span<byte> scratch)
        {
            if (string.IsNullOrEmpty(path) || scratch.Length <= 0)
                return 0;

            FileInfo fileInfo = new FileInfo(path);
            if (!fileInfo.Exists || fileInfo.Length <= 0L || fileInfo.Length > scratch.Length)
                return 0;

            int bytesRead = 0;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, scratch.Length, FileOptions.SequentialScan))
            {
                while (bytesRead < fileInfo.Length)
                {
                    int read = stream.Read(scratch.Slice(bytesRead, (int)fileInfo.Length - bytesRead));
                    if (read <= 0)
                        return 0;

                    bytesRead += read;
                }
            }

            return bytesRead == fileInfo.Length ? bytesRead : 0;
        }

        private bool TryParseVehicleProfileLine(ReadOnlySpan<byte> line, out VehicleSamplingProfileDTO profile)
        {
            profile = default;
            line = Trim(line);
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;
            if (ContainsAsciiIgnoreCase(line, "vehicle") && ContainsAsciiIgnoreCase(line, "length"))
                return false;

            Span<int> starts = stackalloc int[8];
            Span<int> lengths = stackalloc int[8];
            int count = SplitCsvLine(line, starts, lengths);
            if (count < 6)
                return false;

            uint hash = HashText(line.Slice(starts[0], lengths[0]));
            if (!TryParseFloat(line.Slice(starts[1], lengths[1]), out float length) ||
                !TryParseFloat(line.Slice(starts[2], lengths[2]), out float beam) ||
                !TryParseFloat(line.Slice(starts[3], lengths[3]), out float draft) ||
                !TryParseInt(line.Slice(starts[4], lengths[4]), out int minSamples) ||
                !TryParseInt(line.Slice(starts[5], lengths[5]), out int maxSamples))
            {
                return false;
            }

            float inset = fallbackLargeVesselInsetMeters;
            if (count > 6)
                TryParseFloat(line.Slice(starts[6], lengths[6]), out inset);

            profile.VehicleHash = hash != 0u ? hash : 1u;
            profile.LengthMeters = math.max(1f, length);
            profile.BeamMeters = math.max(1f, beam);
            profile.DraftMeters = math.max(0f, draft);
            profile.MinSamples = math.clamp(minSamples, 1, AsyncBuoyancyReadbackConstants.RequestCapacity);
            profile.MaxSamples = math.clamp(maxSamples, profile.MinSamples, AsyncBuoyancyReadbackConstants.RequestCapacity);
            profile.InsetMeters = math.max(0f, inset);
            profile.Flags = AsyncBuoyancyReadbackConstants.FlagActive;
            return true;
        }

        private static int SplitCsvLine(ReadOnlySpan<byte> line, Span<int> starts, Span<int> lengths)
        {
            int start = 0;
            int count = 0;
            for (int i = 0; i <= line.Length && count < starts.Length && count < lengths.Length; i++)
            {
                if (i < line.Length && line[i] != (byte)',')
                    continue;
                starts[count] = start;
                lengths[count] = i - start;
                count++;
                start = i + 1;
            }

            return count;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> span, out float value)
        {
            span = Trim(span);
            value = 0f;
            if (span.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            if (span[index] == (byte)'-' || span[index] == (byte)'+')
            {
                negative = span[index] == (byte)'-';
                index++;
            }

            double whole = 0.0;
            double fraction = 0.0;
            double divisor = 1.0;
            bool hasDigits = false;
            for (; index < span.Length; index++)
            {
                byte c = span[index];
                if (c == (byte)'.')
                {
                    index++;
                    break;
                }
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                hasDigits = true;
                whole = (whole * 10.0) + (c - (byte)'0');
            }

            for (; index < span.Length; index++)
            {
                byte c = span[index];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                hasDigits = true;
                divisor *= 10.0;
                fraction += (c - (byte)'0') / divisor;
            }

            if (!hasDigits)
                return false;

            double parsed = whole + fraction;
            value = (float)(negative ? -parsed : parsed);
            return math.isfinite(value);
        }

        private static bool TryParseInt(ReadOnlySpan<byte> span, out int value)
        {
            span = Trim(span);
            value = 0;
            if (span.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            if (span[index] == (byte)'-' || span[index] == (byte)'+')
            {
                negative = span[index] == (byte)'-';
                index++;
            }

            int parsed = 0;
            bool hasDigits = false;
            for (; index < span.Length; index++)
            {
                byte c = span[index];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                hasDigits = true;
                parsed = (parsed * 10) + (c - (byte)'0');
            }

            if (!hasDigits)
                return false;

            value = negative ? -parsed : parsed;
            return true;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && IsAsciiWhitespace(span[start]))
                start++;
            while (end >= start && IsAsciiWhitespace(span[end]))
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static uint HashText(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte c = ToUpperAscii(value[i]);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool ContainsAsciiIgnoreCase(ReadOnlySpan<byte> value, string token)
        {
            if (token.Length == 0 || value.Length < token.Length)
                return false;

            for (int i = 0; i <= value.Length - token.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < token.Length; j++)
                {
                    if (ToUpperAscii(value[i + j]) != ToUpperAscii((byte)token[j]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return true;
            }

            return false;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' ||
                   value == (byte)'\t' ||
                   value == (byte)'\r' ||
                   value == (byte)'\n';
        }

        private static byte ToUpperAscii(byte value)
        {
            return value >= (byte)'a' && value <= (byte)'z' ? (byte)(value - 32) : value;
        }
#endif

        private void DumpTelemetryOnce()
        {
            if (_dumpedFault)
                return;

            NativeArray<ReadbackTelemetryEntry> telemetry = ReadVaultBuffer(_dataVault, in _telemetryRingHandle);
            NativeArray<int> cursor = ReadVaultBuffer(_dataVault, in _telemetryCursorHandle);
            if (!telemetry.IsCreated || telemetry.Length <= 0 || !_coreBlackboxWarmed || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return;

            int cursorValue = cursor.IsCreated && cursor.Length > 0 ? math.max(0, cursor[0]) : 0;
            int latestIndex = cursorValue > 0 ? (cursorValue - 1) % telemetry.Length : 0;
            ReadbackTelemetryEntry latest = telemetry[latestIndex];
            float scalar = math.max(latest.ApplyMicros, latest.DispatchMicros);
            GlobalTelemetryBus.PushEvent(AsyncReadbackFaultEventHash, scalar, latest.LastEntityHash);
            _ = GlobalTelemetryBus.TryDumpBlackboxNow(AsyncReadbackFaultDumpHash);
            _dumpedFault = true;
        }

        private void WarmCoreBlackboxRoute()
        {
            if (_coreBlackboxWarmed)
                return;

            GlobalTelemetryBus.Initialize();
            _coreBlackboxWarmed = GlobalTelemetryBus.BlackboxActiveFrameCount > 0;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            RefreshCachedOriginSnapshot();
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            if (_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryRegisterDispatcherSystems()
        {
            if (_registeredDispatcher || !Application.isPlaying)
                return;

            _preSimulationSystem = new PreSimulationPhaseSystem(this);
            _simulationSystem = new SimulationPhaseSystem(this);
            _postSimulationSystem = new PostSimulationPhaseSystem(this);
            _visualSyncSystem = new VisualSyncPhaseSystem(this);
            bool pre = SystemDispatcher.Register(_preSimulationSystem);
            bool sim = SystemDispatcher.Register(_simulationSystem);
            bool post = SystemDispatcher.Register(_postSimulationSystem);
            bool visual = SystemDispatcher.Register(_visualSyncSystem);
            _registeredDispatcher = pre && sim && post && visual;
            if (!_registeredDispatcher)
            {
                SystemDispatcher.Unregister(_preSimulationSystem);
                SystemDispatcher.Unregister(_simulationSystem);
                SystemDispatcher.Unregister(_postSimulationSystem);
                SystemDispatcher.Unregister(_visualSyncSystem);
                _preSimulationSystem = null;
                _simulationSystem = null;
                _postSimulationSystem = null;
                _visualSyncSystem = null;
            }
        }

        private void TryUnregisterDispatcherSystems()
        {
            if (!_registeredDispatcher)
                return;

            SystemDispatcher.Unregister(_preSimulationSystem);
            SystemDispatcher.Unregister(_simulationSystem);
            SystemDispatcher.Unregister(_postSimulationSystem);
            SystemDispatcher.Unregister(_visualSyncSystem);
            _preSimulationSystem = null;
            _simulationSystem = null;
            _postSimulationSystem = null;
            _visualSyncSystem = null;
            _registeredDispatcher = false;
        }

        private static bool EnsureVaultDescriptor<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return false;

            if (HasHandle(in handle) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle) &&
                HasHandle(in existingHandle) &&
                vault.TryReadOnlyHandle(in existingHandle, out NativeArray<T>.ReadOnly existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= requiredLength)
            {
                handle = existingHandle;
                return true;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.Physics, options);
            return HasHandle(in handle) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredLength;
        }

        private static NativeArray<T> ReadVaultBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return vault != null &&
                   HasHandle(in handle) &&
                   vault.TryReadHandle(in handle, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private static NativeArray<T> AcquireVaultWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            out IDataVault writeVault)
            where T : struct
        {
            writeVault = null;
            if (vault == null ||
                !HasHandle(in handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.Physics, out NativeArray<T> buffer))
            {
                return default;
            }

            bool releaseOnFailure = true;
            try
            {
                if (buffer.IsCreated)
                {
                    writeVault = vault;
                    releaseOnFailure = false;
                    return buffer;
                }

                return default;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.ReleaseWriteLock(in handle, SystemID.Physics);
            }
        }

        private static void ReleaseVaultWriteBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && HasHandle(in handle))
                vault.ReleaseWriteLock(in handle, SystemID.Physics);
        }

        private static bool HasHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private float ResolveGlobalQualityWeight()
        {
#if UNITY_EDITOR
            if (_editorQualityOverrideActive)
                return math.saturate(_editorQualityOverride);
#endif
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private float ResolveDeadReckoningDecayRate()
        {
            if (_deadReckoningDecayOverride >= 0f)
                return math.saturate(_deadReckoningDecayOverride);

            return 0.96f;
        }

        private float ResolveSimulationFixedDelta(in DispatcherTimingDTO timing)
        {
            float fixedDelta = timing.FixedDelta;
            if (!math.isfinite(fixedDelta) || fixedDelta <= 0f)
                fixedDelta = _lastFixedDelta;
            if (!math.isfinite(fixedDelta) || fixedDelta <= 0f)
                fixedDelta = 0.016666667f;

            _lastFixedDelta = fixedDelta;
            return fixedDelta;
        }

        private static int ResolveShaderActiveWaveIndex(int maxWaveCount, float globalQualityWeight)
        {
            int safeMax = math.max(1, maxWaveCount);
            float quality = math.saturate(math.select(
                AsyncBuoyancyReadbackConstants.AuthoritativeQualityWeight,
                globalQualityWeight,
                math.isfinite(globalQualityWeight)));
            float activeWaveCount = math.lerp(2f, (float)safeMax, quality);
            return math.clamp((int)math.ceil(activeWaveCount) - 1, 0, safeMax - 1);
        }

        private static float ResolveMaxWavelength(NativeArray<AsyncBuoyancyWaveParametersDTO> waves)
        {
            float maxWavelength = 0.25f;
            int maxWaveCount = math.min(waves.Length * 3, AsyncBuoyancyReadbackConstants.WaveCapacity * 3);
            for (int i = 0; i < maxWaveCount; i++)
            {
                float4 lane = GetWaveLane(waves[i / 3], i - ((i / 3) * 3));
                maxWavelength = math.max(maxWavelength, WaveLaneWavelength(lane));
            }

            return maxWavelength;
        }

        private static uint ComputeWaveParametersHash(NativeArray<AsyncBuoyancyWaveParametersDTO> waves, int count)
        {
            uint hash = 2166136261u;
            if (!waves.IsCreated || count <= 0)
                return hash;

            int safeCount = math.min(count, waves.Length);
            for (int i = 0; i < safeCount; i++)
            {
                AsyncBuoyancyWaveParametersDTO dto = waves[i];
                hash = HashFloat4(hash, dto.Wave1);
                hash = HashFloat4(hash, dto.Wave2);
                hash = HashFloat4(hash, dto.Wave3);
                hash = HashFloat4(hash, dto.GlobalWindAndStorm);
            }

            hash = HashUInt(hash, (uint)safeCount);
            return hash != 0u ? hash : 1u;
        }

        private static uint HashFloat4(uint hash, float4 value)
        {
            hash = HashUInt(hash, math.asuint(value.x));
            hash = HashUInt(hash, math.asuint(value.y));
            hash = HashUInt(hash, math.asuint(value.z));
            return HashUInt(hash, math.asuint(value.w));
        }

        private static uint HashUInt(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }

        private static void ResolveWavePhaseBases(
            double3 cameraAup,
            NativeArray<AsyncBuoyancyWaveParametersDTO> waves,
            int activeWaveCount,
            out float4 phaseBase0,
            out float4 phaseBase1)
        {
            phaseBase0 = float4.zero;
            phaseBase1 = float4.zero;
            int waveCount = math.min(math.min(activeWaveCount, waves.Length * 3), AsyncBuoyancyReadbackConstants.WaveCapacity * 3);
            for (int i = 0; i < waveCount; i++)
            {
                float4 lane = GetWaveLane(waves[i / 3], i - ((i / 3) * 3));
                float2 direction = WaveLaneDirection(lane);
                float wavelength = WaveLaneWavelength(lane);
                double projected = (cameraAup.x * direction.x) + (cameraAup.z * direction.y);
                double safeWavelength = math.max(0.25f, wavelength);
                double wrapped = projected - (math.floor(projected / safeWavelength) * safeWavelength);
                float phase = WrapPhase((float)(wrapped * (TwoPi / wavelength)));
                if (i == 0)
                    phaseBase0.x = phase;
                else if (i == 1)
                    phaseBase0.y = phase;
                else if (i == 2)
                    phaseBase0.z = phase;
                else if (i == 3)
                    phaseBase0.w = phase;
                else if (i == 4)
                    phaseBase1.x = phase;
                else
                    phaseBase1.y = phase;
            }
        }

        private static float2 ResolveCameraLocalProjection(double3 cameraAup, float maxWavelength)
        {
            double wavelength = math.max(0.25f, maxWavelength);
            double wrappedX = cameraAup.x - (math.floor(cameraAup.x / wavelength) * wavelength);
            double wrappedZ = cameraAup.z - (math.floor(cameraAup.z / wavelength) * wavelength);
            return new float2((float)wrappedX, (float)wrappedZ);
        }

        private static float ResolveWakeWorldSize(Vector4 shorelineParams)
        {
            float worldSize = shorelineParams.z;
            if (!math.isfinite(worldSize) || worldSize < 1f)
                return 512f;

            return worldSize;
        }

        private static float4 GetWaveLane(AsyncBuoyancyWaveParametersDTO dto, int laneIndex)
        {
            if (laneIndex == 0)
                return dto.Wave1;
            if (laneIndex == 1)
                return dto.Wave2;
            return dto.Wave3;
        }

        private static float2 WaveLaneDirection(float4 lane)
        {
            float angle = math.isfinite(lane.x) ? lane.x : 0f;
            return new float2(
                SimdTranscendentalApproximator.CosPolynomial(angle, 1f, 7),
                SimdTranscendentalApproximator.SinPolynomial(angle, 1f, 7));
        }

        private static float WaveLaneWavelength(float4 lane)
        {
            float wavelength = math.abs(math.isfinite(lane.z) ? lane.z : 0.25f);
            return math.max(0.25f, wavelength);
        }

        private static float WrapPhase(float phase)
        {
            float safePhase = math.isfinite(phase) ? phase : 0f;
            return safePhase - (math.floor(safePhase / TwoPi) * TwoPi);
        }

        private static float ElapsedMicroseconds(long startTimestamp)
        {
            if (startTimestamp <= 0)
                return 0f;

            long delta = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            return (float)((double)delta * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);
        }

        private static Vector4 ToVector4(float4 value)
        {
            return new Vector4(value.x, value.y, value.z, value.w);
        }

#if UNITY_EDITOR
        private void TryAutoAssignComputeShaderInEditor()
        {
            if (waveHeightSamplerCompute != null)
                return;

            string computePath = UnityEditor.AssetDatabase.GUIDToAssetPath(WaveHeightSamplerComputeGuid);
            if (string.IsNullOrEmpty(computePath))
                return;

            waveHeightSamplerCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(computePath);
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryOpenEditorViews(out _, out _, out _, out _))
                return;

            NativeArray<ReadbackResultStateDTO> states = ReadVaultBuffer(_dataVault, in _resultStatesHandle);
            if (!states.IsCreated)
                return;

            double3 origin = ResolveCachedOriginAup();
            int count = math.min(_dispatchRequestCount > 0 ? _dispatchRequestCount : minimumSampleCount, states.Length);
            Gizmos.color = _lastLatencyFrames > 4 ? Color.red : Color.cyan;
            for (int i = 0; i < count; i++)
            {
                ReadbackResultStateDTO state = states[i];
                if (state.EntityHash == 0u)
                    continue;

                Vector3 local = new Vector3(state.LastLocalX, (float)(state.LastHeightAupY - origin.y), state.LastLocalZ);
                Gizmos.DrawWireSphere(local, 0.18f + math.min(0.8f, state.StaleFrames * 0.05f));
                Gizmos.DrawLine(local, local + Vector3.up * math.max(0.1f, _lastLatencyFrames * 0.08f));
            }
        }
#endif

        private sealed class PreSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly AsyncBuoyancyReadbackRuntime _owner;
            public PreSimulationPhaseSystem(AsyncBuoyancyReadbackRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return SystemHashPre; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PreSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { _owner.PreSimulationTick(in timing); }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem
        {
            private readonly AsyncBuoyancyReadbackRuntime _owner;
            public SimulationPhaseSystem(AsyncBuoyancyReadbackRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return SystemHashSim; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.Simulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return _owner.ScheduleSimulation(in timing, in context, dependsOn); }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly AsyncBuoyancyReadbackRuntime _owner;
            public PostSimulationPhaseSystem(AsyncBuoyancyReadbackRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return SystemHashPost; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PostSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { _owner.PostSimulationTick(in timing); }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private readonly AsyncBuoyancyReadbackRuntime _owner;
            public VisualSyncPhaseSystem(AsyncBuoyancyReadbackRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return SystemHashVisual; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.VisualSync; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { _owner.VisualSyncTick(in timing); }
        }
    }
}
