using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.World.GPR;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/World/Ground Penetrating Radar Runtime")]
    public sealed class GroundPenetratingRadarRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IRenderable, IGroundRadarService, IDisposable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private static int s_x001GroundPenetratingRadarRuntimeSignalPushDropCount;
        private const string OwnerName = "TERRAIN_GPR_SYSTEM";
        private const byte SubsurfaceAcousticChannel = 3;
        private const byte GprReturnState = 7;
        private const uint GprSourceHash = 0x4750525Fu; // GPR_
        private const uint GprReturnHash = 0x47505252u; // GPRR
        private const uint TelemetryFaultFlag = 1u << 31;
        private const uint GroundRadarProceduralVertexCount = 6u;
        private static readonly int GroundRadarPingsId = Shader.PropertyToID("_GroundRadarPings");
        private static readonly int GroundRadarPulseId = Shader.PropertyToID("_GroundRadarPulse");
        private static readonly int GroundRadarScaleId = Shader.PropertyToID("_GroundRadarScale");

        [Header("Dependencies")]
        [SerializeField] private MonoBehaviour worldResourceSpawner;
        [SerializeField] private Material radarPingMaterial;

        [Header("Scan")]
        [SerializeField] private float scanIntervalSeconds = 0.35f;
        [SerializeField] private float scanRadiusMeters = 24f;
        [SerializeField] private float stepMeters = 6f;
        [SerializeField, Range(1, GroundRadarConstants.MaxRaymarchSteps)] private int maxRaymarchSteps = GroundRadarConstants.MaxRaymarchSteps;

        [Header("Draw")]
        [SerializeField] private int renderLayer;
        [SerializeField] private float ringScaleMeters = 1.4f;
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;

        private VaultGenerationHandle<float3> _gprHitsHandle;
        private VaultGenerationHandle<float> _gprSignalStrengthHandle;
        private VaultGenerationHandle<float> _gprAgeSecondsHandle;
        private VaultGenerationHandle<int> _gprOreTypesHandle;
        private VaultGenerationHandle<float4> _gprPingGpuHandle;
        private VaultGenerationHandle<int> _gprCountersHandle;
        private VaultGenerationHandle<float> _maxSignalStrengthHandle;
        private VaultGenerationHandle<GroundRadarTelemetryEntry> _telemetryRingHandle;
        private IDataVault _dataVault;
        private GraphicsBuffer _gprPingBufferA;
        private GraphicsBuffer _gprPingBufferB;
        private GraphicsBuffer _activeGprPingBuffer;
        private GraphicsBuffer _gprArgsBufferA;
        private GraphicsBuffer _gprArgsBufferB;
        private GraphicsBuffer _activeGprArgsBuffer;
        private int _gprUploadBufferIndex;
        private Material _runtimeMaterial;
        private IPlayerRuntimeContext _playerContext;
        private ISubmarineState _submarineState;
        private Hecton8.Core.Contracts.IVoxelSonarSdfReadModel _voxelSdfReadModel;
        private IEcosystemDirectorService _ecosystemDirector;
        private IWorldResourceSpawnerReadModel _worldResourceSpawnerReadModel;
        private IWorldResourceSpawnerReadDependencySink _worldResourceSpawnerReadDependencySink;
        private JobHandle _scanJobHandle;
        private Bounds _drawBounds;
        private int _activeGprPings;
        private int _gprSequence;
        private uint _fallbackFrameId;
        private int _telemetryWriteIndex;
        private int _lastScannerSignalSequence;
        private int _oreFilterType;
        private int _registeredUpdate;
        private int _registeredLateFrame;
        private int _registeredRenderable;
        private int _hotSwapRegistered;
        private bool _scanJobScheduled;
        private bool _gprReadSnapshotsValid;
        private bool _pendingDataVaultRebind;
        private float _scanTimer;
        private float _pulsePhaseSeconds;
        private float _highestSignalStrength;
        private float3 _lastProbeOrigin;
        private IDataVault _pendingDataVault;

        public int ActiveGprPings => _activeGprPings;
        public int GprSequence => _gprSequence;
        public int OreFilterType => _oreFilterType;
        public float3 LastProbeOrigin => _lastProbeOrigin;
        public float ScanRadiusMeters => scanRadiusMeters;
        public NativeArray<float3>.ReadOnly GprHitsReadOnly
        {
            get
            {
                return _gprReadSnapshotsValid && TryReadGprHits(out NativeArray<float3>.ReadOnly hits)
                    ? hits
                    : default;
            }
        }

        public NativeArray<float>.ReadOnly GprSignalStrengthReadOnly
        {
            get
            {
                return _gprReadSnapshotsValid && TryReadGprSignalStrength(out NativeArray<float>.ReadOnly signalStrength)
                    ? signalStrength
                    : default;
            }
        }

        private void Awake()
        {
            CacheConfiguredOreReadModel();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            TryRegisterHotSwapDependency();
            CacheRuntimeServices();
            AllocatePersistentState();
            EnsureRuntimeDrawResources();
            GlobalRegistry.RegisterGroundRadarService(this);
            CacheConfiguredOreReadModel();
            CacheOreReadModelFromRegistry();
            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment) ? 1 : 0;
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment) ? 1 : 0;
            _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this) ? 1 : 0;
        }

        private void OnDisable()
        {
            Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            UnregisterHotSwapDependency();
            if (_registeredRenderable != 0)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = 0;
            }

            if (_registeredLateFrame != 0)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = 0;
            }

            if (_registeredUpdate != 0)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdate = 0;
            }

            if (ReferenceEquals(GlobalRegistry.GroundRadar, this))
                GlobalRegistry.UnregisterGroundRadarService(this);

            _ecosystemDirector = null;
            _playerContext = null;
            _submarineState = null;
            _voxelSdfReadModel = null;
            if (_scanJobScheduled)
            {
                if (DispatcherJobFence.TryComplete(ref _scanJobHandle, forceComplete: true))
                    _scanJobScheduled = false;
            }

            ReleaseGraphicsBuffer(ref _gprPingBufferA);
            ReleaseGraphicsBuffer(ref _gprPingBufferB);
            ReleaseGraphicsBuffer(ref _gprArgsBufferA);
            ReleaseGraphicsBuffer(ref _gprArgsBufferB);
            _activeGprPingBuffer = null;
            _activeGprArgsBuffer = null;
            _gprHitsHandle = default;
            _gprSignalStrengthHandle = default;
            _gprAgeSecondsHandle = default;
            _gprOreTypesHandle = default;
            _gprPingGpuHandle = default;
            _gprCountersHandle = default;
            _maxSignalStrengthHandle = default;
            _telemetryRingHandle = default;
            _dataVault = null;
            _pendingDataVault = null;
            _pendingDataVaultRebind = false;
            _gprReadSnapshotsValid = false;
            _activeGprPings = 0;
            _highestSignalStrength = 0f;
            _worldResourceSpawnerReadModel = null;
            _worldResourceSpawnerReadDependencySink = null;

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_scanJobScheduled || _pendingDataVaultRebind || !_gprReadSnapshotsValid)
                return;

            bool scannerActive = TryResolveScannerActive(out int scannerSequence);
            if (!scannerActive && _activeGprPings <= 0)
                return;

            float safeDelta = math.max(0f, deltaTime);
            _scanTimer += safeDelta;
            bool scanDue = scannerActive &&
                           (_scanTimer >= math.max(0.05f, scanIntervalSeconds) ||
                            scannerSequence != _lastScannerSignalSequence);

            float3 aupShift = DrainAupShiftSignals();
            bool hasShift = math.any(math.abs(aupShift) > new float3(0.0001f));
            if (!scanDue && !hasShift && _activeGprPings <= 0)
                return;

            if (!TryResolveProbeOrigin(out float3 probeOrigin))
                return;

            _lastProbeOrigin = probeOrigin;
            _lastScannerSignalSequence = scannerSequence;
            if (scanDue)
                _scanTimer = 0f;

            ScheduleRadarJob(probeOrigin, safeDelta, scanDue, hasShift, aupShift);
        }

        public void LateFrameTick()
        {
            if (!_scanJobScheduled)
                TryApplyPendingDataVaultRebind();

            if (!_scanJobScheduled)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _scanJobHandle))
                return;

            _scanJobScheduled = false;
            CommitCompletedScan();
        }

        public void Render(float deltaTime)
        {
            GraphicsBuffer pingBuffer = _activeGprPingBuffer;
            GraphicsBuffer argsBuffer = _activeGprArgsBuffer;
            if (_activeGprPings <= 0 || argsBuffer == null || pingBuffer == null)
                return;

            Material material = ResolveRenderMaterial();
            if (material == null)
                return;

            material.SetBuffer(GroundRadarPingsId, pingBuffer);
            _pulsePhaseSeconds += math.max(0f, deltaTime);
            if (_pulsePhaseSeconds > 4096f)
                _pulsePhaseSeconds -= 4096f;
            material.SetFloat(GroundRadarPulseId, _pulsePhaseSeconds);
            material.SetFloat(GroundRadarScaleId, math.max(0.1f, ringScaleMeters));

            UnityEngine.Graphics.DrawProceduralIndirect(
                material,
                _drawBounds,
                MeshTopology.Triangles,
                argsBuffer,
                0,
                null,
                null,
                shadowCastingMode,
                false,
                renderLayer);
        }

        public bool TryGetGprPingBuffer(out GraphicsBuffer buffer, out int activeCount, out int sequence)
        {
            buffer = _activeGprPingBuffer;
            activeCount = _activeGprPings;
            sequence = _gprSequence;
            return buffer != null && activeCount > 0;
        }

        public bool TryCopyGprPings(NativeArray<float4> destination, out int copiedCount)
        {
            copiedCount = 0;
            if (!destination.IsCreated ||
                _activeGprPings <= 0 ||
                !TryReadGprPingGpu(out NativeArray<float4>.ReadOnly pingGpu))
            {
                return false;
            }

            copiedCount = math.min(destination.Length, _activeGprPings);
            for (int i = 0; i < copiedCount; i++)
                destination[i] = pingGpu[i];
            return copiedCount > 0;
        }

        public void SetOreFilterType(int oreType)
        {
            _oreFilterType = math.clamp(oreType, WorldOreTypeIds.None, WorldOreTypeIds.Silver);
        }

        private void AllocatePersistentState()
        {
            if (AreGprHandlesCreated() &&
                _gprPingBufferA != null &&
                _gprPingBufferB != null &&
                _gprArgsBufferA != null &&
                _gprArgsBufferB != null)
            {
                if (_activeGprPingBuffer == null)
                    _activeGprPingBuffer = _gprPingBufferA;
                if (_activeGprArgsBuffer == null)
                    _activeGprArgsBuffer = _gprArgsBufferA;
                return;
            }

            if (!TryPrepareGprState(
                out NativeArray<float3> hits,
                out NativeArray<float> signalStrength,
                out NativeArray<float> ageSeconds,
                out NativeArray<int> oreTypes,
                out NativeArray<float4> pingGpu,
                out NativeArray<int> counters,
                out NativeArray<float> maxSignalStrength,
                out NativeArray<GroundRadarTelemetryEntry> telemetryRing))
            {
                return;
            }

            ClearNativeArray(hits);
            ClearNativeArray(signalStrength);
            ClearNativeArray(ageSeconds);
            ClearNativeArray(oreTypes);
            ClearNativeArray(pingGpu);
            ClearNativeArray(counters);
            ClearNativeArray(maxSignalStrength);
            ClearNativeArray(telemetryRing);
            _gprReadSnapshotsValid = true;
            _activeGprPings = 0;
            _highestSignalStrength = 0f;
            _telemetryWriteIndex = 0;

            if (_gprPingBufferA == null)
                _gprPingBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(GroundRadarConstants.MaxPings); // COLD ALLOC: GraphicsBuffer[128 float4] A - shared GPR StructuredBuffer - owner: TERRAIN_GPR_SYSTEM
            if (_gprPingBufferB == null)
                _gprPingBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(GroundRadarConstants.MaxPings); // COLD ALLOC: GraphicsBuffer[128 float4] B - shared GPR StructuredBuffer - owner: TERRAIN_GPR_SYSTEM
            if (_gprArgsBufferA == null)
                _gprArgsBufferA = CreateIndirectArgsBuffer(); // COLD ALLOC: GraphicsBuffer[1] A - GPR procedural indirect args - owner: TERRAIN_GPR_SYSTEM
            if (_gprArgsBufferB == null)
                _gprArgsBufferB = CreateIndirectArgsBuffer(); // COLD ALLOC: GraphicsBuffer[1] B - GPR procedural indirect args - owner: TERRAIN_GPR_SYSTEM
            if (_activeGprPingBuffer == null)
                _activeGprPingBuffer = _gprPingBufferA;
            UpdateIndirectArgsBuffer(0u);
        }

        private bool TryPrepareGprState(
            out NativeArray<float3> hits,
            out NativeArray<float> signalStrength,
            out NativeArray<float> ageSeconds,
            out NativeArray<int> oreTypes,
            out NativeArray<float4> pingGpu,
            out NativeArray<int> counters,
            out NativeArray<float> maxSignalStrength,
            out NativeArray<GroundRadarTelemetryEntry> telemetryRing)
        {
            hits = default;
            signalStrength = default;
            ageSeconds = default;
            oreTypes = default;
            pingGpu = default;
            counters = default;
            maxSignalStrength = default;
            telemetryRing = default;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            _gprHitsHandle = vault.EnsureGenerationHandle<float3>(
                BufferID.GroundRadarHits,
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _gprSignalStrengthHandle = vault.EnsureGenerationHandle<float>(
                BufferID.GroundRadarSignalStrength,
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _gprAgeSecondsHandle = vault.EnsureGenerationHandle<float>(
                BufferID.GroundRadarAgeSeconds,
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _gprOreTypesHandle = vault.EnsureGenerationHandle<int>(
                BufferID.GroundRadarOreTypes,
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _gprPingGpuHandle = vault.EnsureGenerationHandle<float4>(
                BufferID.GroundRadarPingGpu,
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _gprCountersHandle = vault.EnsureGenerationHandle<int>(
                BufferID.GroundRadarCounters,
                4,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _maxSignalStrengthHandle = vault.EnsureGenerationHandle<float>(
                BufferID.GroundRadarMaxSignalStrength,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _telemetryRingHandle = vault.EnsureGenerationHandle<GroundRadarTelemetryEntry>(
                BufferID.GroundRadarTelemetryRing,
                GroundRadarConstants.TelemetryFrames,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);

            return TryOpenGprStateForOwnerWrite(
                vault,
                out hits,
                out signalStrength,
                out ageSeconds,
                out oreTypes,
                out pingGpu,
                out counters,
                out maxSignalStrength,
                out telemetryRing);
        }

        private bool TryOpenGprStateForOwnerWrite(
            out NativeArray<float3> hits,
            out NativeArray<float> signalStrength,
            out NativeArray<float> ageSeconds,
            out NativeArray<int> oreTypes,
            out NativeArray<float4> pingGpu,
            out NativeArray<int> counters,
            out NativeArray<float> maxSignalStrength,
            out NativeArray<GroundRadarTelemetryEntry> telemetryRing)
        {
            return TryOpenGprStateForOwnerWrite(
                _dataVault,
                out hits,
                out signalStrength,
                out ageSeconds,
                out oreTypes,
                out pingGpu,
                out counters,
                out maxSignalStrength,
                out telemetryRing);
        }

        private bool TryOpenGprStateForOwnerWrite(
            IDataVault vault,
            out NativeArray<float3> hits,
            out NativeArray<float> signalStrength,
            out NativeArray<float> ageSeconds,
            out NativeArray<int> oreTypes,
            out NativeArray<float4> pingGpu,
            out NativeArray<int> counters,
            out NativeArray<float> maxSignalStrength,
            out NativeArray<GroundRadarTelemetryEntry> telemetryRing)
        {
            bool resolvedHits = TryOpenVaultBufferForOwnerWrite(vault, in _gprHitsHandle, GroundRadarConstants.MaxPings, out hits);
            bool resolvedSignal = TryOpenVaultBufferForOwnerWrite(vault, in _gprSignalStrengthHandle, GroundRadarConstants.MaxPings, out signalStrength);
            bool resolvedAge = TryOpenVaultBufferForOwnerWrite(vault, in _gprAgeSecondsHandle, GroundRadarConstants.MaxPings, out ageSeconds);
            bool resolvedOreTypes = TryOpenVaultBufferForOwnerWrite(vault, in _gprOreTypesHandle, GroundRadarConstants.MaxPings, out oreTypes);
            bool resolvedPingGpu = TryOpenVaultBufferForOwnerWrite(vault, in _gprPingGpuHandle, GroundRadarConstants.MaxPings, out pingGpu);
            bool resolvedCounters = TryOpenVaultBufferForOwnerWrite(vault, in _gprCountersHandle, 4, out counters);
            bool resolvedMaxSignal = TryOpenVaultBufferForOwnerWrite(vault, in _maxSignalStrengthHandle, 1, out maxSignalStrength);
            bool resolvedTelemetry = TryOpenVaultBufferForOwnerWrite(vault, in _telemetryRingHandle, GroundRadarConstants.TelemetryFrames, out telemetryRing);

            return resolvedHits &&
                resolvedSignal &&
                resolvedAge &&
                resolvedOreTypes &&
                resolvedPingGpu &&
                resolvedCounters &&
                resolvedMaxSignal &&
                resolvedTelemetry;
        }

        private bool TryReadGprHits(out NativeArray<float3>.ReadOnly hits)
        {
            return TryReadVaultBuffer(_dataVault, in _gprHitsHandle, GroundRadarConstants.MaxPings, out hits);
        }

        private bool TryReadGprSignalStrength(out NativeArray<float>.ReadOnly signalStrength)
        {
            return TryReadVaultBuffer(_dataVault, in _gprSignalStrengthHandle, GroundRadarConstants.MaxPings, out signalStrength);
        }

        private bool TryReadGprPingGpu(out NativeArray<float4>.ReadOnly pingGpu)
        {
            return TryReadVaultBuffer(_dataVault, in _gprPingGpuHandle, GroundRadarConstants.MaxPings, out pingGpu);
        }

        private bool TryOpenGprTelemetryForOwnerWrite(out NativeArray<GroundRadarTelemetryEntry> telemetryRing)
        {
            return TryOpenVaultBufferForOwnerWrite(_dataVault, in _telemetryRingHandle, GroundRadarConstants.TelemetryFrames, out telemetryRing);
        }

        private static bool TryOpenVaultBufferForOwnerWrite<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || !IsVaultHandleCreated(in handle))
                return false;

            if (!vault.TryResolveHandle(in handle, out buffer))
                return false;
            return buffer.IsCreated && buffer.Length >= requiredLength;
        }

        private static bool TryReadVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || !IsVaultHandleCreated(in handle))
                return false;

            if (!vault.TryReadOnlyHandle(in handle, out buffer))
                return false;
            return buffer.IsCreated && buffer.Length >= requiredLength;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private bool AreGprHandlesCreated()
        {
            return IsVaultHandleCreated(in _gprHitsHandle) &&
                   IsVaultHandleCreated(in _gprSignalStrengthHandle) &&
                   IsVaultHandleCreated(in _gprAgeSecondsHandle) &&
                   IsVaultHandleCreated(in _gprOreTypesHandle) &&
                   IsVaultHandleCreated(in _gprPingGpuHandle) &&
                   IsVaultHandleCreated(in _gprCountersHandle) &&
                   IsVaultHandleCreated(in _maxSignalStrengthHandle) &&
                   IsVaultHandleCreated(in _telemetryRingHandle);
        }

        private static void ClearNativeArray<T>(NativeArray<T> buffer) where T : struct
        {
            if (!buffer.IsCreated)
                return;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = default;
        }

        private void ScheduleRadarJob(float3 probeOrigin, float deltaTime, bool scanDue, bool hasShift, float3 aupShift)
        {
            if (!TryOpenGprStateForOwnerWrite(
                out NativeArray<float3> hits,
                out NativeArray<float> signalStrength,
                out NativeArray<float> ageSeconds,
                out NativeArray<int> gprOreTypes,
                out NativeArray<float4> pingGpu,
                out NativeArray<int> counters,
                out NativeArray<float> maxSignalStrength,
                out _))
            {
                return;
            }

            NativeArray<byte>.ReadOnly encodedSdf = default;
            int3 gridDimensions = default;
            float3 volumeOrigin = default;
            float3 cellSize = default;
            float sdfRange = 0f;

            if (scanDue)
                TryReadNearestSdf(probeOrigin, out encodedSdf, out gridDimensions, out volumeOrigin, out cellSize, out sdfRange);

            NativeArray<float3>.ReadOnly orePositions = default;
            NativeArray<int>.ReadOnly oreTypes = default;
            int oreCount = 0;
            IWorldResourceSpawnerReadDependencySink oreReadDependencySink = null;
            if (scanDue)
                TryResolveOreSource(out orePositions, out oreTypes, out oreCount, out oreReadDependencySink);

            float qualityWeight01 = ReadGlobalQualityWeight01();
            maxSignalStrength[0] = 0f;
            GroundRadarRaymarchJob job = new GroundRadarRaymarchJob
            {
                EncodedSdf = encodedSdf,
                OrePositions = oreCount > 0 ? orePositions : default,
                OreTypes = oreCount > 0 ? oreTypes : default,
                GprHits = new NativeSlice<float3>(hits),
                GprSignalStrength = new NativeSlice<float>(signalStrength),
                GprAgeSeconds = new NativeSlice<float>(ageSeconds),
                GprOreTypes = new NativeSlice<int>(gprOreTypes),
                GprPingGpu = new NativeSlice<float4>(pingGpu),
                Counters = new NativeSlice<int>(counters),
                MaxSignalStrength = new NativeSlice<float>(maxSignalStrength),
                GridDimensions = gridDimensions,
                VolumeOrigin = volumeOrigin,
                CellSize = cellSize,
                SdfRange = sdfRange,
                OreScanCount = oreCount,
                OreFilterType = _oreFilterType,
                PreviousActiveCount = _activeGprPings,
                RequestedRayCount = SelectRayCount(qualityWeight01),
                MaxSteps = SelectRaymarchStepCount(maxRaymarchSteps, qualityWeight01),
                ProbeOrigin = probeOrigin,
                ScanRadiusMeters = scanRadiusMeters,
                StepMeters = stepMeters,
                DeltaTime = deltaTime,
                RuntimeShift = aupShift,
                Flags = (scanDue ? GroundRadarConstants.ScanFlag : 0u) |
                        (hasShift ? GroundRadarConstants.AupShiftFlag : 0u)
            };

            _scanJobHandle = job.Schedule();
            if (scanDue && oreCount > 0 && oreReadDependencySink != null)
                oreReadDependencySink.RegisterOreReadDependency(_scanJobHandle);
            _scanJobScheduled = true;
        }

        private void CommitCompletedScan()
        {
            if (!TryOpenGprStateForOwnerWrite(
                out _,
                out _,
                out _,
                out _,
                out NativeArray<float4> pingGpu,
                out NativeArray<int> counters,
                out NativeArray<float> maxSignalStrength,
                out _))
            {
                return;
            }

            int previousCount = _activeGprPings;
            _activeGprPings = counters.IsCreated && counters.Length > 0
                ? math.clamp(counters[0], 0, GroundRadarConstants.MaxPings)
                : 0;
            int addedCount = counters.IsCreated && counters.Length > 1 ? math.max(0, counters[1]) : 0;
            int rayCount = counters.IsCreated && counters.Length > 2 ? counters[2] : 0;
            _highestSignalStrength = maxSignalStrength.IsCreated && maxSignalStrength.Length > 0
                ? math.saturate(maxSignalStrength[0])
                : 0f;
            uint frameId = AdvanceRadarFrameId();

            int macroSwarmAddedCount = AppendMacroSwarmRadarPings();
            if (macroSwarmAddedCount > 0)
            {
                addedCount += macroSwarmAddedCount;
                _highestSignalStrength = math.max(_highestSignalStrength, 0.85f);
            }

            if (_activeGprPings > 0 && TryResolveGprPingWriteBuffer(out GraphicsBuffer gprPingWriteBuffer))
            {
                GraphicsBufferUploadUtility.UploadNativeArray(gprPingWriteBuffer, pingGpu, _activeGprPings);
                _activeGprPingBuffer = gprPingWriteBuffer;
                _drawBounds = new Bounds(
                    new Vector3(_lastProbeOrigin.x, _lastProbeOrigin.y - stepMeters * maxRaymarchSteps * 0.5f, _lastProbeOrigin.z),
                    Vector3.one * math.max(16f, scanRadiusMeters * 3f));
            }

            if (_activeGprPings != previousCount)
            {
                _gprSequence++;
                UpdateIndirectArgsBuffer((uint)_activeGprPings);
            }

            WriteTelemetry(frameId, addedCount, rayCount, _highestSignalStrength, 0u);
            if (!math.all(math.isfinite(_lastProbeOrigin)) || !math.isfinite(_highestSignalStrength))
            {
                WriteTelemetry(frameId, addedCount, rayCount, _highestSignalStrength, TelemetryFaultFlag);
                DumpBlackBox();
            }

            if (addedCount > 0)
                PublishGprSignals(frameId, _highestSignalStrength);
        }

        private int AppendMacroSwarmRadarPings()
        {
            IEcosystemDirectorService ecosystem = _ecosystemDirector;
            if (ecosystem == null ||
                !ecosystem.IsInitialized ||
                _activeGprPings >= GroundRadarConstants.MaxPings ||
                !TryOpenGprStateForOwnerWrite(
                    out NativeArray<float3> hits,
                    out NativeArray<float> signalStrength,
                    out NativeArray<float> ageSeconds,
                    out NativeArray<int> gprOreTypes,
                    out NativeArray<float4> pingGpu,
                    out _,
                    out _,
                    out _))
            {
                return 0;
            }

            int remaining = GroundRadarConstants.MaxPings - _activeGprPings;
            NativeArray<float4> destination = pingGpu.GetSubArray(_activeGprPings, remaining);
            if (!ecosystem.TryCopyMacroSwarmRadarPings(destination, _lastProbeOrigin, scanRadiusMeters * 4f, out int copiedCount))
                return 0;

            copiedCount = math.clamp(copiedCount, 0, remaining);
            int startIndex = _activeGprPings;
            for (int i = 0; i < copiedCount; i++)
            {
                int pingIndex = startIndex + i;
                float4 ping = pingGpu[pingIndex];
                hits[pingIndex] = ping.xyz;
                signalStrength[pingIndex] = math.saturate(ping.w);
                ageSeconds[pingIndex] = 0f;
                gprOreTypes[pingIndex] = WorldOreTypeIds.None;
            }

            _activeGprPings += copiedCount;
            return copiedCount;
        }

        private bool TryResolveScannerActive(out int sequence)
        {
            sequence = 0;
            ReadOnlySpan<ScannerToolActiveSignal> frameSignals = SignalBus<ScannerToolActiveSignal>.GetFrameSnapshot();
            for (int i = frameSignals.Length - 1; i >= 0; i--)
            {
                ScannerToolActiveSignal signal = frameSignals[i];
                if (signal.Active != 0 && signal.Battery01 > 0.001f)
                {
                    sequence = (int)signal.Frame;
                    return true;
                }
            }

            if (ScannerSignalRoute.TryGetLatestActive(out ScannerToolActiveSignal latest, out int latestSequence))
            {
                sequence = latestSequence;
                return latest.Active != 0 && latest.Battery01 > 0.001f;
            }

            return false;
        }

        private static float3 DrainAupShiftSignals()
        {
            float3 shift = default;
            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                float3 delta = shifts[i].ShiftMeters;
                if (math.all(math.isfinite(delta)))
                    shift += delta;
            }

            return shift;
        }

        private bool TryResolveProbeOrigin(out float3 probeOrigin)
        {
            ISubmarineState state = _submarineState;
            if (state != null)
            {
                probeOrigin = state.StateSnapshot.RuntimePosition;
                if (math.all(math.isfinite(probeOrigin)))
                    return true;
            }

            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext != null && playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot playerPose))
            {
                probeOrigin = playerPose.RuntimePosition;
                if (math.all(math.isfinite(probeOrigin)))
                    return true;
            }

            probeOrigin = default;
            return false;
        }

        private bool TryReadNearestSdf(
            float3 probeOrigin,
            out NativeArray<byte>.ReadOnly encodedSdf,
            out int3 gridDimensions,
            out float3 volumeOrigin,
            out float3 cellSize,
            out float sdfRange)
        {
            encodedSdf = default;
            gridDimensions = default;
            volumeOrigin = default;
            cellSize = default;
            sdfRange = 0f;

            Hecton8.Core.Contracts.IVoxelSonarSdfReadModel voxelSdfReadModel = _voxelSdfReadModel;
            if (voxelSdfReadModel == null)
                return false;

            if (!voxelSdfReadModel.TryReadNearestSonarSdf(
                    probeOrigin,
                    out NativeArray<byte>.ReadOnly payload,
                    out int3 dimensions,
                    out float3 payloadOrigin,
                    out float3 payloadCellSize,
                    out float payloadRange))
            {
                return false;
            }

            encodedSdf = payload;
            gridDimensions = dimensions;
            volumeOrigin = payloadOrigin;
            cellSize = payloadCellSize;
            sdfRange = payloadRange;
            return true;
        }

        private static float ReadGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(0f, quality, math.isfinite(quality)));
        }

        private static int SelectRayCount(float qualityWeight01)
        {
            float q = math.saturate(math.select(0f, qualityWeight01, math.isfinite(qualityWeight01)));
            float eased = math.smoothstep(0f, 1f, q);
            return math.clamp((int)math.round(math.lerp(4f, GroundRadarConstants.MaxRays, eased)), 1, GroundRadarConstants.MaxRays);
        }

        private static int SelectRaymarchStepCount(int configuredMaxSteps, float qualityWeight01)
        {
            int maxSteps = math.clamp(configuredMaxSteps, 1, GroundRadarConstants.MaxRaymarchSteps);
            float q = math.saturate(math.select(0f, qualityWeight01, math.isfinite(qualityWeight01)));
            float eased = math.smoothstep(0f, 1f, q);
            return math.clamp((int)math.round(math.lerp(1f, maxSteps, eased)), 1, GroundRadarConstants.MaxRaymarchSteps);
        }

        private void CacheConfiguredOreReadModel()
        {
            _worldResourceSpawnerReadModel = worldResourceSpawner as IWorldResourceSpawnerReadModel;
            _worldResourceSpawnerReadDependencySink = worldResourceSpawner as IWorldResourceSpawnerReadDependencySink;
            if (_worldResourceSpawnerReadModel != null)
                return;

            _worldResourceSpawnerReadDependencySink = null;
        }

        private bool TryResolveOreSource(
            out NativeArray<float3>.ReadOnly orePositions,
            out NativeArray<int>.ReadOnly oreTypes,
            out int oreCount,
            out IWorldResourceSpawnerReadDependencySink dependencySink)
        {
            IWorldResourceSpawnerReadModel configuredSpawner = _worldResourceSpawnerReadModel;
            if (configuredSpawner != null &&
                configuredSpawner.TryGetOrePositionsReadOnly(out orePositions, out oreCount) &&
                configuredSpawner.TryGetOreTypesReadOnly(out oreTypes, out int typeCount))
            {
                oreCount = math.min(math.min(oreCount, typeCount), math.min(orePositions.Length, oreTypes.Length));
                dependencySink = _worldResourceSpawnerReadDependencySink;
                return oreCount > 0;
            }

            orePositions = default;
            oreTypes = default;
            oreCount = 0;
            dependencySink = null;
            return false;
        }

        private bool CacheOreReadModelFromRegistry()
        {
            if (_worldResourceSpawnerReadModel != null)
                return true;

            _worldResourceSpawnerReadModel = GlobalRegistry.WorldResourceSpawner;
            _worldResourceSpawnerReadDependencySink = _worldResourceSpawnerReadModel as IWorldResourceSpawnerReadDependencySink;
            return _worldResourceSpawnerReadModel != null;
        }

        private uint AdvanceRadarFrameId()
        {
            uint frameId = TimeSliceScheduler.CurrentFrameId;
            if (frameId != 0u)
                return frameId;

            unchecked
            {
                _fallbackFrameId++;
            }

            if (_fallbackFrameId == 0u)
                _fallbackFrameId = 1u;
            return _fallbackFrameId;
        }

        private void PublishGprSignals(uint frameId, float highestStrength)
        {
            float clampedStrength = math.saturate(highestStrength);
            if (!TryResolveRuntimeAup(_lastProbeOrigin, out AbsoluteUniversePosition positionAup))
                return;

            SignalBus<AcousticPingSignal>.TryPushTracked(new AcousticPingSignal
            {
                PositionAup = positionAup,
                RadiusMeters = scanRadiusMeters,
                Intensity01 = clampedStrength,
                SourceId = GprSourceHash,
                Channel = SubsurfaceAcousticChannel,
                Flags = 1
            }, ref s_x001GroundPenetratingRadarRuntimeSignalPushDropCount);

            SignalBus<ToolAcousticSignal>.TryPushTracked(new ToolAcousticSignal
            {
                ToolHash = GprSourceHash,
                TargetHash = GprReturnHash,
                Progress01 = clampedStrength,
                PitchScale = 0.85f + clampedStrength * 0.5f,
                Intensity01 = clampedStrength,
                Frame = frameId,
                State = GprReturnState,
                Flags = 1
            }, ref s_x001GroundPenetratingRadarRuntimeSignalPushDropCount);
        }

        private static bool TryResolveRuntimeAup(float3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.all(math.isfinite(runtimePosition)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            RebindCachedService(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            // Ref-forwarding hook above owns this cache update; avoid duplicate Vault rebinding.
        }

        private void TryRegisterHotSwapDependency()
        {
            if (_hotSwapRegistered == 0 && GlobalRegistry.TryRegisterHotSwapListener(this))
                _hotSwapRegistered = 1;
        }

        private void UnregisterHotSwapDependency()
        {
            if (_hotSwapRegistered == 0)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = 0;
        }

        private void CacheRuntimeServices()
        {
            _dataVault = GlobalRegistry.DataVault;
            _playerContext = GlobalRegistry.Player;
            _submarineState = GlobalRegistry.SubmarineState;
            _voxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;
            _ecosystemDirector = GlobalRegistry.EcosystemDirector;
        }

        private void RebindCachedService(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    QueueDataVaultRebind(currentService as IDataVault);
                    return;
                case GlobalRegistryServiceSlot.Player:
                    _playerContext = currentService as IPlayerRuntimeContext;
                    return;
                case GlobalRegistryServiceSlot.SubmarineState:
                    _submarineState = currentService as ISubmarineState;
                    return;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    _voxelSdfReadModel = currentService as Hecton8.Core.Contracts.IVoxelSonarSdfReadModel;
                    return;
                case GlobalRegistryServiceSlot.EcosystemDirector:
                    _ecosystemDirector = currentService as IEcosystemDirectorService;
                    return;
                case GlobalRegistryServiceSlot.WorldResourceSpawnerRuntime:
                    _worldResourceSpawnerReadModel = currentService as IWorldResourceSpawnerReadModel;
                    _worldResourceSpawnerReadDependencySink = currentService as IWorldResourceSpawnerReadDependencySink;
                    return;
            }
        }

        private void QueueDataVaultRebind(IDataVault currentVault)
        {
            _pendingDataVault = currentVault;
            _pendingDataVaultRebind = true;
        }

        private bool TryApplyPendingDataVaultRebind()
        {
            if (!_pendingDataVaultRebind)
                return _dataVault != null;

            if (_scanJobScheduled)
                return false;

            _dataVault = _pendingDataVault;
            _pendingDataVault = null;
            _pendingDataVaultRebind = false;
            ClearGprVaultDescriptors();
            if (_dataVault == null)
            {
                UpdateIndirectArgsBuffer(0u);
                return false;
            }

            AllocatePersistentState();
            return _gprReadSnapshotsValid;
        }

        private void ClearGprVaultDescriptors()
        {
            _gprHitsHandle = default;
            _gprSignalStrengthHandle = default;
            _gprAgeSecondsHandle = default;
            _gprOreTypesHandle = default;
            _gprPingGpuHandle = default;
            _gprCountersHandle = default;
            _maxSignalStrengthHandle = default;
            _telemetryRingHandle = default;
            _gprReadSnapshotsValid = false;
            _activeGprPings = 0;
            _highestSignalStrength = 0f;
            _telemetryWriteIndex = 0;
        }

        private void WriteTelemetry(uint frameId, int addedCount, int rayCount, float highestStrength, uint flags)
        {
            if (!TryOpenGprTelemetryForOwnerWrite(out NativeArray<GroundRadarTelemetryEntry> telemetryRing) || telemetryRing.Length == 0)
                return;

            int index = _telemetryWriteIndex % telemetryRing.Length;
            telemetryRing[index] = new GroundRadarTelemetryEntry
            {
                Frame = frameId,
                ActiveGprPings = _activeGprPings,
                AddedGprPings = addedCount,
                RayCount = rayCount,
                HighestSignalStrength = highestStrength,
                ProbeOrigin = _lastProbeOrigin,
                Flags = flags
            };
            _telemetryWriteIndex = (_telemetryWriteIndex + 1) % telemetryRing.Length;
        }

        private void DumpBlackBox()
        {
            if (!TryOpenGprTelemetryForOwnerWrite(out NativeArray<GroundRadarTelemetryEntry> telemetryRing))
                return;

            try
            {
                string path = Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_TERRAIN_GPR_SYSTEM.bin");
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read); // COLD ALLOC: FileStream[GPR telemetry dump] — blackbox dump file writer — owner: TERRAIN_GPR_SYSTEM
                using BinaryWriter writer = new BinaryWriter(stream); // COLD ALLOC: BinaryWriter[GPR telemetry dump] — blackbox binary row serializer — owner: TERRAIN_GPR_SYSTEM
                writer.Write(telemetryRing.Length);
                writer.Write(_telemetryWriteIndex);
                for (int i = 0; i < telemetryRing.Length; i++)
                {
                    GroundRadarTelemetryEntry entry = telemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.ActiveGprPings);
                    writer.Write(entry.AddedGprPings);
                    writer.Write(entry.RayCount);
                    writer.Write(entry.HighestSignalStrength);
                    writer.Write(entry.ProbeOrigin.x);
                    writer.Write(entry.ProbeOrigin.y);
                    writer.Write(entry.ProbeOrigin.z);
                    writer.Write(entry.Flags);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void EnsureRuntimeDrawResources()
        {
            if (radarPingMaterial == null && _runtimeMaterial == null)
            {
                Shader shader = Shader.Find("Hecton8/World/GroundRadarPingIndirect");
                if (shader != null)
                {
                    _runtimeMaterial = new Material(shader) // COLD ALLOC: Material[GroundRadarPingRuntime] — fallback GPR render material — owner: TERRAIN_GPR_SYSTEM
                    {
                        name = "GroundRadarPingRuntime",
                        hideFlags = HideFlags.DontSave
                    };
                }
            }
        }

        private Material ResolveRenderMaterial()
        {
            return radarPingMaterial != null ? radarPingMaterial : _runtimeMaterial;
        }

        private void UpdateIndirectArgsBuffer(uint instanceCount)
        {
            if (!TryResolveGprArgsWriteBuffer(out GraphicsBuffer argsWriteBuffer))
                return;

            NativeArray<GroundRadarIndirectArgsDTO> argsWrite =
                argsWriteBuffer.LockBufferForWrite<GroundRadarIndirectArgsDTO>(0, 1);
            GroundRadarIndirectArgsDTO args = default;
            args.VertexCountPerInstance = GroundRadarProceduralVertexCount;
            args.InstanceCount = instanceCount;
            args.StartVertex = 0u;
            args.StartInstance = 0u;
            argsWrite[0] = args;
            argsWriteBuffer.UnlockBufferAfterWrite<GroundRadarIndirectArgsDTO>(1);
            _activeGprArgsBuffer = argsWriteBuffer;
        }

        private bool TryResolveGprPingWriteBuffer(out GraphicsBuffer buffer)
        {
            buffer = _gprUploadBufferIndex == 0 ? _gprPingBufferA : _gprPingBufferB;
            if (buffer == null)
                buffer = ReferenceEquals(_activeGprPingBuffer, _gprPingBufferA) ? _gprPingBufferB : _gprPingBufferA;

            if (buffer == null)
                return false;

            _gprUploadBufferIndex ^= 1;
            return true;
        }

        private bool TryResolveGprArgsWriteBuffer(out GraphicsBuffer buffer)
        {
            buffer = _gprUploadBufferIndex == 0 ? _gprArgsBufferA : _gprArgsBufferB;
            if (buffer == null)
                buffer = ReferenceEquals(_activeGprArgsBuffer, _gprArgsBufferA) ? _gprArgsBufferB : _gprArgsBufferA;

            return buffer != null;
        }

        private static GraphicsBuffer CreateIndirectArgsBuffer()
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<GroundRadarIndirectArgsDTO>());
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct GroundRadarIndirectArgsDTO
        {
            [FieldOffset(0)] public uint VertexCountPerInstance;
            [FieldOffset(4)] public uint InstanceCount;
            [FieldOffset(8)] public uint StartVertex;
            [FieldOffset(12)] public uint StartInstance;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }
    }
}
