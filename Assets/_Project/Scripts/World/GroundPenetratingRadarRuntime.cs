using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
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
    public sealed class GroundPenetratingRadarRuntime : MonoBehaviour, ISlowTickable, ILateFrameTickable, IRenderable, IGroundRadarService, IDisposable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private static int s_x001GroundPenetratingRadarRuntimeSignalPushDropCount;
        private const string OwnerName = "TERRAIN_GPR_SYSTEM";
        private const byte SubsurfaceAcousticChannel = 3;
        private const byte GprReturnState = 7;
        private const uint GprSourceHash = 0x4750525Fu; // GPR_
        private const uint GprReturnHash = 0x47505252u; // GPRR
        private const uint TelemetryFaultFlag = 1u << 31;
        private const uint TelemetryPublishDropFlag = 1u << 30;
        private const int GprSignalDispatchSlotsPerSweep = 2;
        private const uint GroundRadarProceduralVertexCount = 6u;
        private const int GroundRadarIndirectArgsSizeBytes = 16;
        private const int BlackBoxDumpHeaderBytes = 8;
        private const int BlackBoxDumpEntryBytes = 36;
        private static readonly WaitCallback BlackBoxDumpWorkerCallback = WriteBlackBoxDumpWorker;
        private static readonly int GroundRadarPingsId = Shader.PropertyToID("_GroundRadarPings");
        private static readonly int GroundRadarPulseId = Shader.PropertyToID("_GroundRadarPulse");
        private static readonly int GroundRadarScaleId = Shader.PropertyToID("_GroundRadarScale");
        private static readonly ulong ScanJobMutationGuardMask =
            GroundRadarMutationGuardBit(BufferID.GroundRadarHits) |
            GroundRadarMutationGuardBit(BufferID.GroundRadarSignalStrength) |
            GroundRadarMutationGuardBit(BufferID.GroundRadarAgeSeconds) |
            GroundRadarMutationGuardBit(BufferID.GroundRadarOreTypes) |
            GroundRadarMutationGuardBit(BufferID.GroundRadarPingGpu) |
            GroundRadarMutationGuardBit(BufferID.GroundRadarCounters) |
            GroundRadarMutationGuardBit(BufferID.GroundRadarMaxSignalStrength);
        private static readonly ulong PingGpuReadGuardMask =
            GroundRadarMutationGuardBit(BufferID.GroundRadarPingGpu);

        private struct RadarPendingJob
        {
            public NativeArray<float3> Hits;
            public NativeArray<float> SignalStrength;
            public NativeArray<float> AgeSeconds;
            public NativeArray<int> OreTypes;
            public NativeArray<float4> PingGpu;
            public NativeArray<int> Counters;
            public NativeArray<float> MaxSignalStrength;
            public NativeArray<byte> SdfSnapshot;
            public JobHandle Handle;
            public uint Flags;
        }

        [Header("Dependencies")]
        [SerializeField] private MonoBehaviour worldResourceSpawner;
        [UnityEngine.Serialization.FormerlySerializedAs("radarPingMaterial")]
        [SerializeField] private Material _radarPingAuthoredMaterial;

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
        private IPlayerRuntimeContext _playerContext;
        private ISubmarineState _submarineState;
        private Hecton8.Core.Contracts.IVoxelSonarSdfReadModel _voxelSdfReadModel;
        private Hecton8.Core.Contracts.IVoxelSonarSdfReadLeaseModel _voxelSdfReadLeaseModel;
        private IEcosystemDirectorService _ecosystemDirector;
        private IWorldResourceSpawnerReadModel _worldResourceSpawnerReadModel;
        private IWorldResourceSpawnerReadDependencySink _worldResourceSpawnerReadDependencySink;
        private IWorldResourceSpawnerCommandModel _worldResourceSpawnerCommandModel;
        private readonly GroundRadarTelemetryEntry[] _blackBoxDumpSnapshot = new GroundRadarTelemetryEntry[GroundRadarConstants.TelemetryFrames]; // COLD ALLOC: fixed fault dump snapshot - owner: TERRAIN_GPR_SYSTEM
        private RadarPendingJob _radarJob;
        private JobHandle _radarJobHandle;
        private Bounds _drawBounds;
        private int _activeGprPings;
        private int _gprSequence;
        private uint _fallbackFrameId;
        private int _telemetryWriteIndex;
        private int _lastScannerSignalSequence;
        private int _oreFilterType;
        private int _registeredLateFrame;
        private int _registeredSlowTick;
        private int _registeredRenderable;
        private int _hotSwapRegistered;
        private int _scanJobBufferPinCount;
        private int _radarJobScheduled;
        private int _blackBoxDumpSnapshotCount;
        private int _blackBoxDumpSnapshotCursor;
        private int _blackBoxDumpInFlight;
        private bool _gprReadSnapshotsValid;
        private bool _pendingDataVaultRebind;
        private bool _pendingIndirectArgsClear;
        private bool _missingRadarPingMaterialAnnounced;
        private float _scanTimer;
        private float _pulsePhaseSeconds;
        private float _highestSignalStrength;
        private float3 _lastProbeOrigin;
        private string _blackBoxDumpPath;
        private IDataVault _scanJobGuardVault;
        private IDataVault _pendingDataVault;
        private MaterialPropertyBlock _radarDrawProperties;

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
            AllocatePersistentStateCold();
            EnsureRuntimeDrawResourcesCold();
            EnsureBlackBoxDumpPathCold();
            GlobalRegistry.RegisterGroundRadarService(this);
            CacheConfiguredOreReadModel();
            CacheOreReadModelFromOwnerRoute();
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment) ? 1 : 0;
            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment) ? 1 : 0;
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

            if (_registeredSlowTick != 0)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = 0;
            }

            if (ReferenceEquals(GlobalRegistry.GroundRadar, this))
                GlobalRegistry.UnregisterGroundRadarService(this);

            _ecosystemDirector = null;
            _playerContext = null;
            _submarineState = null;
            ForceCompleteRadarJobForTeardown();
            ReleaseRadarPendingJob(ref _radarJob);
            ReleaseScanJobBufferPins();
            bool gprVaultBuffersReleased = ReleaseGprVaultBuffersInPostSimulationSwapWindow(_dataVault);
            _voxelSdfReadModel = null;
            _voxelSdfReadLeaseModel = null;

            _radarDrawProperties?.Clear();
            ReleaseGraphicsBuffer(ref _gprPingBufferA);
            ReleaseGraphicsBuffer(ref _gprPingBufferB);
            ReleaseGraphicsBuffer(ref _gprArgsBufferA);
            ReleaseGraphicsBuffer(ref _gprArgsBufferB);
            _activeGprPingBuffer = null;
            _activeGprArgsBuffer = null;
            if (gprVaultBuffersReleased)
            {
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
            }
            _blackBoxDumpSnapshotCount = 0;
            _blackBoxDumpSnapshotCursor = 0;
            _gprReadSnapshotsValid = false;
            _activeGprPings = 0;
            _highestSignalStrength = 0f;
            _worldResourceSpawnerReadModel = null;
            _worldResourceSpawnerReadDependencySink = null;
            _worldResourceSpawnerCommandModel = null;
        }

        private void AdvanceRadarFrameState(float deltaTime)
        {
            if (_pendingDataVaultRebind || !_gprReadSnapshotsValid)
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
            FlushPendingIndirectArgsClear();
            CompleteRadarJob(forceComplete: false);
            if (_radarJobScheduled != 0)
                return;

            if ((_pendingDataVaultRebind || !_gprReadSnapshotsValid) &&
                !TryApplyPendingDataVaultRebindCold())
            {
                return;
            }

            AdvanceRadarFrameState(SystemDispatcher.CurrentFrameDeltaTime);
            if (_radarJobScheduled != 0)
                return;
        }

        public void SlowTick()
        {
            if (_dataVault == null || !_gprReadSnapshotsValid || !HasRuntimeGpuBuffersReady())
                QueueIndirectArgsClear();
        }

        public void Render(float deltaTime)
        {
            GraphicsBuffer pingBuffer = _activeGprPingBuffer;
            GraphicsBuffer argsBuffer = _activeGprArgsBuffer;
            if (_activeGprPings <= 0 || !IsValidBuffer(argsBuffer) || !IsValidBuffer(pingBuffer))
                return;

            Material material = ResolveRenderMaterial();
            if (material == null)
                return;

            MaterialPropertyBlock drawProperties = _radarDrawProperties;
            if (drawProperties == null)
                return;

            drawProperties.Clear();
            drawProperties.SetBuffer(GroundRadarPingsId, pingBuffer);
            _pulsePhaseSeconds += math.max(0f, deltaTime);
            if (_pulsePhaseSeconds > 4096f)
                _pulsePhaseSeconds -= 4096f;
            drawProperties.SetFloat(GroundRadarPulseId, _pulsePhaseSeconds);
            drawProperties.SetFloat(GroundRadarScaleId, math.max(0.1f, ringScaleMeters));

            UnityEngine.Graphics.DrawProceduralIndirect(
                material,
                _drawBounds,
                MeshTopology.Triangles,
                argsBuffer,
                0,
                null,
                drawProperties,
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
                _activeGprPings <= 0)
            {
                return false;
            }

            IDataVault vault = _dataVault;
            if (!TryPinPingGpuReadBuffer(vault))
                return false;

            try
            {
                if (!TryReadVaultBuffer(vault, BufferID.GroundRadarPingGpu, in _gprPingGpuHandle, GroundRadarConstants.MaxPings, out NativeArray<float4>.ReadOnly pingGpu))
                    return false;

                copiedCount = math.min(destination.Length, _activeGprPings);
                for (int i = 0; i < copiedCount; i++)
                    destination[i] = pingGpu[i];
                return copiedCount > 0;
            }
            finally
            {
                vault?.ReleaseMutationGuard(PingGpuReadGuardMask);
            }
        }

        public void SetOreFilterType(int oreType)
        {
            _oreFilterType = math.clamp(oreType, WorldOreTypeIds.None, WorldOreTypeIds.Silver);
        }

        private void AllocatePersistentStateCold()
        {
            if (!ValidateGroundRadarRuntimeLayouts(out _, out _))
                return;

            if (AreGprHandlesCreated() &&
                HasRuntimeGpuBuffersReady())
            {
                if (_activeGprPingBuffer == null)
                    _activeGprPingBuffer = _gprPingBufferA;
                if (_activeGprArgsBuffer == null)
                    _activeGprArgsBuffer = _gprArgsBufferA;
                TryEnsureRadarPendingJobCold();
                return;
            }

            if (!TryPrepareGprState() ||
                !TryClearGprStateCold())
            {
                return;
            }

            _gprReadSnapshotsValid = true;
            _activeGprPings = 0;
            _highestSignalStrength = 0f;
            _telemetryWriteIndex = 0;

            if (!IsValidBuffer(_gprPingBufferA))
            {
                ReleaseGraphicsBuffer(ref _gprPingBufferA);
                _gprPingBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(GroundRadarConstants.MaxPings); // COLD ALLOC: GraphicsBuffer[128 float4] A - shared GPR StructuredBuffer - owner: TERRAIN_GPR_SYSTEM
            }
            if (!IsValidBuffer(_gprPingBufferB))
            {
                ReleaseGraphicsBuffer(ref _gprPingBufferB);
                _gprPingBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(GroundRadarConstants.MaxPings); // COLD ALLOC: GraphicsBuffer[128 float4] B - shared GPR StructuredBuffer - owner: TERRAIN_GPR_SYSTEM
            }
            if (!IsValidBuffer(_gprArgsBufferA))
            {
                ReleaseGraphicsBuffer(ref _gprArgsBufferA);
                _gprArgsBufferA = CreateIndirectArgsBuffer(); // COLD ALLOC: GraphicsBuffer[1] A - GPR procedural indirect args - owner: TERRAIN_GPR_SYSTEM
            }
            if (!IsValidBuffer(_gprArgsBufferB))
            {
                ReleaseGraphicsBuffer(ref _gprArgsBufferB);
                _gprArgsBufferB = CreateIndirectArgsBuffer(); // COLD ALLOC: GraphicsBuffer[1] B - GPR procedural indirect args - owner: TERRAIN_GPR_SYSTEM
            }
            if (_activeGprPingBuffer == null)
                _activeGprPingBuffer = _gprPingBufferA;
            QueueIndirectArgsClear();
            TryEnsureRadarPendingJobCold();
        }

        private bool HasRuntimeGpuBuffersReady()
        {
            return IsValidBuffer(_gprPingBufferA) &&
                   IsValidBuffer(_gprPingBufferB) &&
                   IsValidBuffer(_gprArgsBufferA) &&
                   IsValidBuffer(_gprArgsBufferB);
        }

        private bool TryPrepareGprState()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (AreGprHandlesCreated())
                return true;

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

            return AreGprHandlesCreated();
        }

        private bool TryClearGprStateCold()
        {
            IDataVault vault = _dataVault;
            return TryClearGprVaultBufferCold(vault, in _gprHitsHandle, BufferID.GroundRadarHits, GroundRadarConstants.MaxPings) &&
                   TryClearGprVaultBufferCold(vault, in _gprSignalStrengthHandle, BufferID.GroundRadarSignalStrength, GroundRadarConstants.MaxPings) &&
                   TryClearGprVaultBufferCold(vault, in _gprAgeSecondsHandle, BufferID.GroundRadarAgeSeconds, GroundRadarConstants.MaxPings) &&
                   TryClearGprVaultBufferCold(vault, in _gprOreTypesHandle, BufferID.GroundRadarOreTypes, GroundRadarConstants.MaxPings) &&
                   TryClearGprVaultBufferCold(vault, in _gprPingGpuHandle, BufferID.GroundRadarPingGpu, GroundRadarConstants.MaxPings) &&
                   TryClearGprVaultBufferCold(vault, in _gprCountersHandle, BufferID.GroundRadarCounters, 4) &&
                   TryClearGprVaultBufferCold(vault, in _maxSignalStrengthHandle, BufferID.GroundRadarMaxSignalStrength, 1) &&
                   TryClearGprVaultBufferCold(vault, in _telemetryRingHandle, BufferID.GroundRadarTelemetryRing, GroundRadarConstants.TelemetryFrames);
        }

        private bool TryOpenGprStateForRead(
            IDataVault vault,
            out NativeArray<float3>.ReadOnly hits,
            out NativeArray<float>.ReadOnly signalStrength,
            out NativeArray<float>.ReadOnly ageSeconds,
            out NativeArray<int>.ReadOnly oreTypes,
            out NativeArray<float4>.ReadOnly pingGpu,
            out NativeArray<int>.ReadOnly counters,
            out NativeArray<float>.ReadOnly maxSignalStrength)
        {
            bool resolvedHits = TryReadVaultBuffer(vault, BufferID.GroundRadarHits, in _gprHitsHandle, GroundRadarConstants.MaxPings, out hits);
            bool resolvedSignal = TryReadVaultBuffer(vault, BufferID.GroundRadarSignalStrength, in _gprSignalStrengthHandle, GroundRadarConstants.MaxPings, out signalStrength);
            bool resolvedAge = TryReadVaultBuffer(vault, BufferID.GroundRadarAgeSeconds, in _gprAgeSecondsHandle, GroundRadarConstants.MaxPings, out ageSeconds);
            bool resolvedOreTypes = TryReadVaultBuffer(vault, BufferID.GroundRadarOreTypes, in _gprOreTypesHandle, GroundRadarConstants.MaxPings, out oreTypes);
            bool resolvedPingGpu = TryReadVaultBuffer(vault, BufferID.GroundRadarPingGpu, in _gprPingGpuHandle, GroundRadarConstants.MaxPings, out pingGpu);
            bool resolvedCounters = TryReadVaultBuffer(vault, BufferID.GroundRadarCounters, in _gprCountersHandle, 4, out counters);
            bool resolvedMaxSignal = TryReadVaultBuffer(vault, BufferID.GroundRadarMaxSignalStrength, in _maxSignalStrengthHandle, 1, out maxSignalStrength);

            return resolvedHits &&
                resolvedSignal &&
                resolvedAge &&
                resolvedOreTypes &&
                resolvedPingGpu &&
                resolvedCounters &&
                resolvedMaxSignal;
        }

        private bool TryReadGprHits(out NativeArray<float3>.ReadOnly hits)
        {
            return TryReadVaultBuffer(_dataVault, BufferID.GroundRadarHits, in _gprHitsHandle, GroundRadarConstants.MaxPings, out hits);
        }

        private bool TryReadGprSignalStrength(out NativeArray<float>.ReadOnly signalStrength)
        {
            return TryReadVaultBuffer(_dataVault, BufferID.GroundRadarSignalStrength, in _gprSignalStrengthHandle, GroundRadarConstants.MaxPings, out signalStrength);
        }

        private bool TryReadGprPingGpu(out NativeArray<float4>.ReadOnly pingGpu)
        {
            return TryReadVaultBuffer(_dataVault, BufferID.GroundRadarPingGpu, in _gprPingGpuHandle, GroundRadarConstants.MaxPings, out pingGpu);
        }

        private static bool TryClearGprVaultBufferCold<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength) where T : struct
        {
            if (vault == null || requiredLength <= 0 || !IsGroundRadarVaultHandle(in handle, expectedBufferId))
                return false;

            bool locked = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in handle, SystemID.WorldStreaming, out NativeArray<T> buffer))
                    return false;

                locked = true;
                if (!buffer.IsCreated || buffer.Length < requiredLength)
                    return false;

                ClearNativeArray(buffer);
                return true;
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in handle, SystemID.WorldStreaming);
            }
        }

        private static bool TryReadVaultBuffer<T>(
            IDataVault vault,
            BufferID expectedBufferId,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || !IsGroundRadarVaultHandle(in handle, expectedBufferId))
                return false;

            if (!vault.TryReadOnlyHandle(in handle, out buffer))
                return false;
            return buffer.IsCreated && buffer.Length >= requiredLength;
        }

        private static bool IsGroundRadarVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)SystemID.WorldStreaming &&
                   handle.Generation != 0u;
        }

        private bool AreGprHandlesCreated()
        {
            return IsGroundRadarVaultHandle(in _gprHitsHandle, BufferID.GroundRadarHits) &&
                   IsGroundRadarVaultHandle(in _gprSignalStrengthHandle, BufferID.GroundRadarSignalStrength) &&
                   IsGroundRadarVaultHandle(in _gprAgeSecondsHandle, BufferID.GroundRadarAgeSeconds) &&
                   IsGroundRadarVaultHandle(in _gprOreTypesHandle, BufferID.GroundRadarOreTypes) &&
                   IsGroundRadarVaultHandle(in _gprPingGpuHandle, BufferID.GroundRadarPingGpu) &&
                   IsGroundRadarVaultHandle(in _gprCountersHandle, BufferID.GroundRadarCounters) &&
                   IsGroundRadarVaultHandle(in _maxSignalStrengthHandle, BufferID.GroundRadarMaxSignalStrength) &&
                   IsGroundRadarVaultHandle(in _telemetryRingHandle, BufferID.GroundRadarTelemetryRing);
        }

        private static void ClearNativeArray<T>(NativeArray<T> buffer) where T : struct
        {
            if (!buffer.IsCreated)
                return;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = default;
        }

        private static bool IsRadarPendingJobValid(in RadarPendingJob pending)
        {
            return pending.Hits.IsCreated &&
                   pending.Hits.Length >= GroundRadarConstants.MaxPings &&
                   pending.SignalStrength.IsCreated &&
                   pending.SignalStrength.Length >= GroundRadarConstants.MaxPings &&
                   pending.AgeSeconds.IsCreated &&
                   pending.AgeSeconds.Length >= GroundRadarConstants.MaxPings &&
                   pending.OreTypes.IsCreated &&
                   pending.OreTypes.Length >= GroundRadarConstants.MaxPings &&
                   pending.PingGpu.IsCreated &&
                   pending.PingGpu.Length >= GroundRadarConstants.MaxPings &&
                   pending.Counters.IsCreated &&
                   pending.Counters.Length >= 4 &&
                   pending.MaxSignalStrength.IsCreated &&
                   pending.MaxSignalStrength.Length >= 1 &&
                   pending.SdfSnapshot.IsCreated &&
                   pending.SdfSnapshot.Length >= GroundRadarConstants.SdfSnapshotByteCapacity;
        }

        private static void ReleaseRadarPendingJob(ref RadarPendingJob pending)
        {
            H8Memory.Release(ref pending.Hits, SystemID.WorldStreaming);
            H8Memory.Release(ref pending.SignalStrength, SystemID.WorldStreaming);
            H8Memory.Release(ref pending.AgeSeconds, SystemID.WorldStreaming);
            H8Memory.Release(ref pending.OreTypes, SystemID.WorldStreaming);
            H8Memory.Release(ref pending.PingGpu, SystemID.WorldStreaming);
            H8Memory.Release(ref pending.Counters, SystemID.WorldStreaming);
            H8Memory.Release(ref pending.MaxSignalStrength, SystemID.WorldStreaming);
            H8Memory.Release(ref pending.SdfSnapshot, SystemID.WorldStreaming);
            if (pending.Hits.IsCreated ||
                pending.SignalStrength.IsCreated ||
                pending.AgeSeconds.IsCreated ||
                pending.OreTypes.IsCreated ||
                pending.PingGpu.IsCreated ||
                pending.Counters.IsCreated ||
                pending.MaxSignalStrength.IsCreated ||
                pending.SdfSnapshot.IsCreated)
            {
                return;
            }

            pending.Handle = default;
            pending.Flags = 0u;
        }

        private static bool TryCreateRadarPendingJob(out RadarPendingJob pending)
        {
            pending = default;
            pending.Hits = H8Memory.Allocate<float3>(
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            pending.SignalStrength = H8Memory.Allocate<float>(
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            pending.AgeSeconds = H8Memory.Allocate<float>(
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            pending.OreTypes = H8Memory.Allocate<int>(
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            pending.PingGpu = H8Memory.Allocate<float4>(
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            pending.Counters = H8Memory.Allocate<int>(
                4,
                SystemID.WorldStreaming,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            pending.MaxSignalStrength = H8Memory.Allocate<float>(
                1,
                SystemID.WorldStreaming,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            pending.SdfSnapshot = H8Memory.Allocate<byte>(
                GroundRadarConstants.SdfSnapshotByteCapacity,
                SystemID.WorldStreaming,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            if (IsRadarPendingJobValid(in pending))
                return true;

            ReleaseRadarPendingJob(ref pending);
            return false;
        }

        private static void RetireRadarPendingJobForReuse(ref RadarPendingJob pending)
        {
            pending.Handle = default;
            pending.Flags = 0u;
        }

        private bool TryEnsureRadarPendingJobCold()
        {
            if (_radarJobScheduled != 0)
                return IsRadarPendingJobValid(in _radarJob);

            if (IsRadarPendingJobValid(in _radarJob))
            {
                RetireRadarPendingJobForReuse(ref _radarJob);
                return true;
            }

            ReleaseRadarPendingJob(ref _radarJob);
            return TryCreateRadarPendingJob(out _radarJob);
        }

        private bool TryPrepareRadarPendingJobForSchedule()
        {
            if (!IsRadarPendingJobValid(in _radarJob))
                return false;

            RetireRadarPendingJobForReuse(ref _radarJob);
            return true;
        }

        private bool TryCopyCurrentGprStateToPending(ref RadarPendingJob pending)
        {
            IDataVault vault = _dataVault;
            if (!IsRadarPendingJobValid(in pending) || vault == null || !TryPinScanJobBuffers(vault))
                return false;

            try
            {
                if (!TryOpenGprStateForRead(
                    vault,
                    out NativeArray<float3>.ReadOnly hits,
                    out NativeArray<float>.ReadOnly signalStrength,
                    out NativeArray<float>.ReadOnly ageSeconds,
                    out NativeArray<int>.ReadOnly gprOreTypes,
                    out NativeArray<float4>.ReadOnly pingGpu,
                    out NativeArray<int>.ReadOnly counters,
                    out _))
                {
                    return false;
                }

                CopyReadOnlyBufferToPending(hits, pending.Hits, GroundRadarConstants.MaxPings);
                CopyReadOnlyBufferToPending(signalStrength, pending.SignalStrength, GroundRadarConstants.MaxPings);
                CopyReadOnlyBufferToPending(ageSeconds, pending.AgeSeconds, GroundRadarConstants.MaxPings);
                CopyReadOnlyBufferToPending(gprOreTypes, pending.OreTypes, GroundRadarConstants.MaxPings);
                CopyReadOnlyBufferToPending(pingGpu, pending.PingGpu, GroundRadarConstants.MaxPings);
                CopyReadOnlyBufferToPending(counters, pending.Counters, 4);
                pending.MaxSignalStrength[0] = 0f;
                return true;
            }
            finally
            {
                ReleaseScanJobBufferPins();
            }
        }

        private bool TryPublishRadarPendingJob(ref RadarPendingJob pending)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsRadarPendingJobValid(in pending))
            {
                return false;
            }

            if (!TryCopyPendingBufferToVault(vault, in _gprHitsHandle, BufferID.GroundRadarHits, pending.Hits, GroundRadarConstants.MaxPings))
                return false;
            if (!TryCopyPendingBufferToVault(vault, in _gprSignalStrengthHandle, BufferID.GroundRadarSignalStrength, pending.SignalStrength, GroundRadarConstants.MaxPings))
                return false;
            if (!TryCopyPendingBufferToVault(vault, in _gprAgeSecondsHandle, BufferID.GroundRadarAgeSeconds, pending.AgeSeconds, GroundRadarConstants.MaxPings))
                return false;
            if (!TryCopyPendingBufferToVault(vault, in _gprOreTypesHandle, BufferID.GroundRadarOreTypes, pending.OreTypes, GroundRadarConstants.MaxPings))
                return false;
            if (!TryCopyPendingBufferToVault(vault, in _gprPingGpuHandle, BufferID.GroundRadarPingGpu, pending.PingGpu, GroundRadarConstants.MaxPings))
                return false;
            if (!TryCopyPendingBufferToVault(vault, in _maxSignalStrengthHandle, BufferID.GroundRadarMaxSignalStrength, pending.MaxSignalStrength, 1))
                return false;

            // Counters publish last; readers treat counter[0] as the visible ping count.
            return TryCopyPendingBufferToVault(vault, in _gprCountersHandle, BufferID.GroundRadarCounters, pending.Counters, 4);
        }

        private static bool TryCopyPendingBufferToVault<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            NativeArray<T> source,
            int copyLength) where T : struct
        {
            if (vault == null ||
                copyLength <= 0 ||
                !source.IsCreated ||
                source.Length < copyLength ||
                !IsGroundRadarVaultHandle(in handle, expectedBufferId))
            {
                return false;
            }

            bool locked = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in handle, SystemID.WorldStreaming, out NativeArray<T> target))
                    return false;

                locked = true;
                if (!target.IsCreated || target.Length < copyLength)
                    return false;

                NativeArray<T>.Copy(source, target, copyLength);
                return true;
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in handle, SystemID.WorldStreaming);
            }
        }

        private void ScheduleRadarJob(float3 probeOrigin, float deltaTime, bool scanDue, bool hasShift, float3 aupShift)
        {
            if (_radarJobScheduled != 0)
                return;

            if (!TryPrepareRadarPendingJobForSchedule() ||
                !TryCopyCurrentGprStateToPending(ref _radarJob))
            {
                return;
            }

            NativeArray<byte>.ReadOnly encodedSdf = default;
            int3 gridDimensions = default;
            float3 volumeOrigin = default;
            float3 cellSize = default;
            float sdfRange = 0f;

            if (scanDue)
                TryStageNearestSdf(probeOrigin, ref _radarJob, out encodedSdf, out gridDimensions, out volumeOrigin, out cellSize, out sdfRange);

            NativeArray<float3>.ReadOnly orePositions = default;
            NativeArray<int>.ReadOnly oreTypes = default;
            IWorldResourceSpawnerReadDependencySink oreDependencySink = null;
            int oreCount = 0;
            if (scanDue)
                TryResolveOreSource(out orePositions, out oreTypes, out oreCount, out oreDependencySink);
            if (oreCount > 0 && oreDependencySink == null)
            {
                orePositions = default;
                oreTypes = default;
                oreCount = 0;
            }

            float qualityWeight01 = ReadGlobalQualityWeight01();
            _radarJob.MaxSignalStrength[0] = 0f;
            GroundRadarRaymarchJob job = new GroundRadarRaymarchJob
            {
                EncodedSdf = encodedSdf,
                OrePositions = oreCount > 0 ? orePositions : default,
                OreTypes = oreCount > 0 ? oreTypes : default,
                GprHits = new NativeSlice<float3>(_radarJob.Hits),
                GprSignalStrength = new NativeSlice<float>(_radarJob.SignalStrength),
                GprAgeSeconds = new NativeSlice<float>(_radarJob.AgeSeconds),
                GprOreTypes = new NativeSlice<int>(_radarJob.OreTypes),
                GprPingGpu = new NativeSlice<float4>(_radarJob.PingGpu),
                Counters = new NativeSlice<int>(_radarJob.Counters),
                MaxSignalStrength = new NativeSlice<float>(_radarJob.MaxSignalStrength),
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

            JobHandle handle = job.Schedule();
            _radarJob.Handle = handle;
            _radarJob.Flags = job.Flags;
            _radarJobHandle = handle;
            _radarJobScheduled = 1;

            if (oreCount > 0 && oreDependencySink != null)
                oreDependencySink.RegisterOreReadDependency(handle);
        }

        private bool CompleteRadarJob(bool forceComplete)
        {
            if (_radarJobScheduled == 0)
                return false;

            if (!forceComplete && !_radarJobHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryComplete(ref _radarJobHandle, forceComplete))
                return false;

            try
            {
                CommitCompletedScan(ref _radarJob);
                return true;
            }
            finally
            {
                RetireRadarPendingJobForReuse(ref _radarJob);
                _radarJobScheduled = 0;
                _radarJobHandle = default;
            }
        }

        private void ForceCompleteRadarJobForTeardown()
        {
            if (_radarJobScheduled == 0)
                return;

            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                CompleteRadarJob(forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private void CommitCompletedScan(ref RadarPendingJob pending)
        {
            if (!IsRadarPendingJobValid(in pending))
                return;

            int previousCount = _activeGprPings;
            int oreAddedCount = pending.Counters.IsCreated && pending.Counters.Length > 1
                ? math.max(0, pending.Counters[1])
                : 0;

            AppendMacroSwarmRadarPings(ref pending);
            int activeCount = pending.Counters.IsCreated && pending.Counters.Length > 0
                ? math.clamp(pending.Counters[0], 0, GroundRadarConstants.MaxPings)
                : 0;
            int addedCount = pending.Counters.IsCreated && pending.Counters.Length > 1 ? math.max(0, pending.Counters[1]) : 0;
            int rayCount = pending.Counters.IsCreated && pending.Counters.Length > 2 ? pending.Counters[2] : 0;
            float highestSignalStrength = pending.MaxSignalStrength.IsCreated && pending.MaxSignalStrength.Length > 0
                ? math.saturate(pending.MaxSignalStrength[0])
                : 0f;

            if (!TryPublishRadarPendingJob(ref pending))
            {
                WriteTelemetry(AdvanceRadarFrameId(), addedCount, rayCount, highestSignalStrength, TelemetryPublishDropFlag);
                return;
            }

            _activeGprPings = activeCount;
            _highestSignalStrength = highestSignalStrength;
            uint frameId = AdvanceRadarFrameId();

            if (_activeGprPings > 0 && TryAcquireGprPingWriteBuffer(out GraphicsBuffer gprPingWriteBuffer))
            {
                GraphicsBufferUploadUtility.UploadNativeArray(gprPingWriteBuffer, pending.PingGpu, _activeGprPings);
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

            bool hasTelemetryFault = !math.all(math.isfinite(_lastProbeOrigin)) || !math.isfinite(highestSignalStrength);
            uint telemetryFlags = hasTelemetryFault ? TelemetryFaultFlag : 0u;
            if (addedCount > 0 && !TryPublishGprSignals(frameId, highestSignalStrength))
                telemetryFlags |= TelemetryPublishDropFlag;

            WriteTelemetry(frameId, addedCount, rayCount, highestSignalStrength, telemetryFlags);
            ReportOreScannerSweepTelemetry(pending.Flags, oreAddedCount, frameId);
            if (hasTelemetryFault)
            {
                DumpBlackBox();
            }
        }

        private void ReportOreScannerSweepTelemetry(uint pendingFlags, int addedCount, uint frameId)
        {
            if ((pendingFlags & GroundRadarConstants.ScanFlag) == 0u)
                return;

            IWorldResourceSpawnerCommandModel commandModel = _worldResourceSpawnerCommandModel;
            commandModel?.ReportScannerSweepResult(addedCount, scanRadiusMeters, frameId);
        }

        private int AppendMacroSwarmRadarPings(ref RadarPendingJob pending)
        {
            IEcosystemDirectorService ecosystem = _ecosystemDirector;
            if (ecosystem == null ||
                !ecosystem.IsInitialized ||
                !IsRadarPendingJobValid(in pending))
            {
                return 0;
            }

            int activeCount = pending.Counters.IsCreated && pending.Counters.Length > 0
                ? math.clamp(pending.Counters[0], 0, GroundRadarConstants.MaxPings)
                : 0;
            if (activeCount >= GroundRadarConstants.MaxPings)
                return 0;

            int remaining = GroundRadarConstants.MaxPings - activeCount;
            NativeArray<float4> destination = pending.PingGpu.GetSubArray(activeCount, remaining);
            if (!ecosystem.TryCopyMacroSwarmRadarPings(destination, _lastProbeOrigin, scanRadiusMeters * 4f, out int copiedCount))
                return 0;

            copiedCount = math.clamp(copiedCount, 0, remaining);
            int startIndex = activeCount;
            float highestMacroStrength = 0f;
            for (int i = 0; i < copiedCount; i++)
            {
                int pingIndex = startIndex + i;
                float4 ping = pending.PingGpu[pingIndex];
                float signalStrength = math.saturate(ping.w);
                pending.Hits[pingIndex] = ping.xyz;
                pending.SignalStrength[pingIndex] = signalStrength;
                pending.AgeSeconds[pingIndex] = 0f;
                pending.OreTypes[pingIndex] = WorldOreTypeIds.None;
                highestMacroStrength = math.max(highestMacroStrength, signalStrength);
            }

            pending.Counters[0] = activeCount + copiedCount;
            if (pending.Counters.Length > 1)
                pending.Counters[1] = math.max(0, pending.Counters[1]) + copiedCount;
            if (pending.MaxSignalStrength.Length > 0)
                pending.MaxSignalStrength[0] = math.max(math.saturate(pending.MaxSignalStrength[0]), highestMacroStrength);
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

        private bool TryStageNearestSdf(
            float3 probeOrigin,
            ref RadarPendingJob pending,
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
            Hecton8.Core.Contracts.IVoxelSonarSdfReadLeaseModel leaseModel = _voxelSdfReadLeaseModel;
            if (voxelSdfReadModel == null || leaseModel == null)
                return false;

            if (!leaseModel.TryAcquireNearestSonarSdfReadLease(
                    probeOrigin,
                    out NativeArray<byte>.ReadOnly payload,
                    out int3 dimensions,
                    out float3 payloadOrigin,
                    out float3 payloadCellSize,
                    out float payloadRange,
                    out Hecton8.Core.Contracts.VoxelSonarSdfReadLease payloadLease))
            {
                return false;
            }

            bool leaseLocked = true;
            try
            {
                long expectedLong = (long)dimensions.x * dimensions.y * dimensions.z;
                if (expectedLong <= 0L ||
                    expectedLong > int.MaxValue ||
                    !payload.IsCreated ||
                    payload.Length < expectedLong ||
                    !TryStageSdfLeaseToPendingSnapshot(payload, (int)expectedLong, ref pending, out encodedSdf))
                {
                    return false;
                }

                gridDimensions = dimensions;
                volumeOrigin = payloadOrigin;
                cellSize = payloadCellSize;
                sdfRange = payloadRange;
                return true;
            }
            finally
            {
                if (leaseLocked)
                    leaseModel.ReleaseNearestSonarSdfReadLease(in payloadLease);
            }
        }

        private static bool TryStageSdfLeaseToPendingSnapshot(
            NativeArray<byte>.ReadOnly sourceSdf,
            int requiredLength,
            ref RadarPendingJob pending,
            out NativeArray<byte>.ReadOnly snapshotSdf)
        {
            snapshotSdf = default;
            if (!sourceSdf.IsCreated || requiredLength <= 0 || sourceSdf.Length < requiredLength)
                return false;

            if (requiredLength > GroundRadarConstants.SdfSnapshotByteCapacity ||
                !pending.SdfSnapshot.IsCreated ||
                pending.SdfSnapshot.Length < GroundRadarConstants.SdfSnapshotByteCapacity)
            {
                return false;
            }

            for (int i = 0; i < requiredLength; i++)
                pending.SdfSnapshot[i] = sourceSdf[i];

            snapshotSdf = pending.SdfSnapshot.AsReadOnly();
            return true;
        }

        private bool TryPinScanJobBuffers(IDataVault vault)
        {
            if (_scanJobBufferPinCount != 0)
                return false;

            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool acquired = false;
            try
            {
                if (!vault.TryAcquireMutationGuard(ScanJobMutationGuardMask))
                    return false;

                acquired = true;
                if (vault.IsCompactionFenceActive || !TryValidateScanJobBuffers(vault))
                    return false;

                _scanJobGuardVault = vault;
                _scanJobBufferPinCount = 1;
                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseMutationGuard(ScanJobMutationGuardMask);
            }
        }

        private void ReleaseScanJobBufferPins()
        {
            if (_scanJobBufferPinCount == 0)
                return;

            IDataVault vault = _scanJobGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(ScanJobMutationGuardMask);

            _scanJobGuardVault = null;
            _scanJobBufferPinCount = 0;
        }

        private bool TryPinPingGpuReadBuffer(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool acquired = false;
            try
            {
                if (!vault.TryAcquireMutationGuard(PingGpuReadGuardMask))
                    return false;

                acquired = true;
                if (vault.IsCompactionFenceActive || !TryValidatePingGpuBuffer(vault))
                    return false;

                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseMutationGuard(PingGpuReadGuardMask);
            }
        }

        private bool TryValidateScanJobBuffers(IDataVault vault)
        {
            return TryReadVaultBuffer(vault, BufferID.GroundRadarHits, in _gprHitsHandle, GroundRadarConstants.MaxPings, out _) &&
                   TryReadVaultBuffer(vault, BufferID.GroundRadarSignalStrength, in _gprSignalStrengthHandle, GroundRadarConstants.MaxPings, out _) &&
                   TryReadVaultBuffer(vault, BufferID.GroundRadarAgeSeconds, in _gprAgeSecondsHandle, GroundRadarConstants.MaxPings, out _) &&
                   TryReadVaultBuffer(vault, BufferID.GroundRadarOreTypes, in _gprOreTypesHandle, GroundRadarConstants.MaxPings, out _) &&
                   TryReadVaultBuffer(vault, BufferID.GroundRadarPingGpu, in _gprPingGpuHandle, GroundRadarConstants.MaxPings, out _) &&
                   TryReadVaultBuffer(vault, BufferID.GroundRadarCounters, in _gprCountersHandle, 4, out _) &&
                   TryReadVaultBuffer(vault, BufferID.GroundRadarMaxSignalStrength, in _maxSignalStrengthHandle, 1, out _);
        }

        private static void CopyReadOnlyBufferToPending<T>(
            NativeArray<T>.ReadOnly source,
            NativeArray<T> destination,
            int copyLength) where T : struct
        {
            int safeLength = math.min(copyLength, math.min(source.Length, destination.Length));
            for (int i = 0; i < safeLength; i++)
                destination[i] = source[i];
        }

        private bool TryValidatePingGpuBuffer(IDataVault vault)
        {
            return TryReadVaultBuffer(vault, BufferID.GroundRadarPingGpu, in _gprPingGpuHandle, GroundRadarConstants.MaxPings, out _);
        }

        private static ulong GroundRadarMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
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
            _worldResourceSpawnerCommandModel = worldResourceSpawner as IWorldResourceSpawnerCommandModel;
            if (_worldResourceSpawnerReadModel != null)
                return;

            _worldResourceSpawnerReadDependencySink = null;
            _worldResourceSpawnerCommandModel = null;
        }

        private bool TryResolveOreSource(
            out NativeArray<float3>.ReadOnly orePositions,
            out NativeArray<int>.ReadOnly oreTypes,
            out int oreCount,
            out IWorldResourceSpawnerReadDependencySink dependencySink)
        {
            if (!CacheOreReadModelFromOwnerRoute())
            {
                orePositions = default;
                oreTypes = default;
                oreCount = 0;
                dependencySink = null;
                return false;
            }

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

        private bool CacheOreReadModelFromOwnerRoute()
        {
            bool resolved = WorldRuntimeReferenceUtility.TryResolveWorldResourceSpawnerReadModel(
                ref _worldResourceSpawnerReadModel,
                ref _worldResourceSpawnerReadDependencySink);
            if (resolved)
                _worldResourceSpawnerCommandModel = _worldResourceSpawnerReadModel as IWorldResourceSpawnerCommandModel;
            else
                _worldResourceSpawnerCommandModel = null;

            return resolved;
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

        private static bool TryReserveLateFrameEventDispatchSlots(int slotCount)
        {
            if (slotCount <= 0)
                return true;

            if (SystemDispatcher.TryReserveLateFrameEventDispatches(slotCount))
                return true;

            IncrementSignalPushDropCount(slotCount);
            SystemDispatcher.MarkLateFrameEventDispatchDeferred();
            return false;
        }

        private static void IncrementSignalPushDropCount(int dropCount)
        {
            for (int i = 0; i < dropCount; i++)
            {
                int current = Volatile.Read(ref s_x001GroundPenetratingRadarRuntimeSignalPushDropCount);
                while (current < int.MaxValue)
                {
                    int next = current + 1;
                    int observed = Interlocked.CompareExchange(ref s_x001GroundPenetratingRadarRuntimeSignalPushDropCount, next, current);
                    if (observed == current)
                        break;

                    current = observed;
                }
            }
        }

        private bool TryPublishGprSignals(uint frameId, float highestStrength)
        {
            float clampedStrength = math.saturate(highestStrength);
            if (!TryResolveRuntimeAup(_lastProbeOrigin, out AbsoluteUniversePosition positionAup))
                return false;

            if (!TryReserveLateFrameEventDispatchSlots(GprSignalDispatchSlotsPerSweep))
                return false;

            bool acousticPublished = SignalBus<AcousticPingSignal>.TryPushTracked(new AcousticPingSignal
            {
                PositionAup = positionAup,
                RadiusMeters = scanRadiusMeters,
                Intensity01 = clampedStrength,
                SourceId = GprSourceHash,
                Channel = SubsurfaceAcousticChannel,
                Flags = 1
            }, ref s_x001GroundPenetratingRadarRuntimeSignalPushDropCount);

            bool toolPublished = SignalBus<ToolAcousticSignal>.TryPushTracked(new ToolAcousticSignal
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

            return acousticPublished && toolPublished;
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
            HectonVoxelEngine voxelEngine = null;
            if (WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine))
            {
                _voxelSdfReadModel = voxelEngine;
                _voxelSdfReadLeaseModel = voxelEngine as Hecton8.Core.Contracts.IVoxelSonarSdfReadLeaseModel;
            }
            else
            {
                _voxelSdfReadModel = null;
                _voxelSdfReadLeaseModel = null;
            }
            EcosystemDirector ecosystemDirector = _ecosystemDirector as EcosystemDirector;
            if (WorldRuntimeReferenceUtility.TryResolveEcosystemDirector(ref ecosystemDirector))
                _ecosystemDirector = ecosystemDirector;
            else
                _ecosystemDirector = null;
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
                    _voxelSdfReadLeaseModel = currentService as Hecton8.Core.Contracts.IVoxelSonarSdfReadLeaseModel;
                    return;
                case GlobalRegistryServiceSlot.EcosystemDirector:
                    _ecosystemDirector = currentService as IEcosystemDirectorService;
                    return;
                case GlobalRegistryServiceSlot.WorldResourceSpawnerRuntime:
                    _worldResourceSpawnerReadModel = currentService as IWorldResourceSpawnerReadModel;
                    _worldResourceSpawnerReadDependencySink = currentService as IWorldResourceSpawnerReadDependencySink;
                    if (WorldRuntimeReferenceUtility.TryResolveWorldResourceSpawnerReadModel(
                            ref _worldResourceSpawnerReadModel,
                            ref _worldResourceSpawnerReadDependencySink))
                    {
                        _worldResourceSpawnerCommandModel = _worldResourceSpawnerReadModel as IWorldResourceSpawnerCommandModel;
                    }
                    else
                    {
                        _worldResourceSpawnerCommandModel = null;
                    }
                    return;
            }
        }

        private void QueueDataVaultRebind(IDataVault currentVault)
        {
            _pendingDataVault = currentVault;
            _pendingDataVaultRebind = true;
            if (_radarJobScheduled != 0)
                return;

            TryApplyPendingDataVaultRebindCold();
        }

        private bool ReleaseGprVaultBuffersInPostSimulationSwapWindow(IDataVault vault)
        {
            if (vault == null)
                return true;

            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return ReleaseGprVaultBuffers(vault);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private bool ReleaseGprVaultBuffers(IDataVault vault, bool allowCompactionFence = false)
        {
            if (vault == null || (!allowCompactionFence && vault.IsCompactionFenceActive))
                return false;

            bool released = true;
            released &= ReleaseGprVaultBuffer(vault, ref _gprHitsHandle, BufferID.GroundRadarHits);
            released &= ReleaseGprVaultBuffer(vault, ref _gprSignalStrengthHandle, BufferID.GroundRadarSignalStrength);
            released &= ReleaseGprVaultBuffer(vault, ref _gprAgeSecondsHandle, BufferID.GroundRadarAgeSeconds);
            released &= ReleaseGprVaultBuffer(vault, ref _gprOreTypesHandle, BufferID.GroundRadarOreTypes);
            released &= ReleaseGprVaultBuffer(vault, ref _gprPingGpuHandle, BufferID.GroundRadarPingGpu);
            released &= ReleaseGprVaultBuffer(vault, ref _gprCountersHandle, BufferID.GroundRadarCounters);
            released &= ReleaseGprVaultBuffer(vault, ref _maxSignalStrengthHandle, BufferID.GroundRadarMaxSignalStrength);
            released &= ReleaseGprVaultBuffer(vault, ref _telemetryRingHandle, BufferID.GroundRadarTelemetryRing);
            _gprReadSnapshotsValid = false;
            _activeGprPings = 0;
            _highestSignalStrength = 0f;
            _telemetryWriteIndex = 0;
            QueueIndirectArgsClear();
            return released;
        }

        private static bool ReleaseGprVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : struct
        {
            if (vault == null || !IsGroundRadarVaultHandle(in handle, expectedBufferId))
            {
                handle = default;
                return true;
            }

            vault.TryUnlockBuffer(expectedBufferId, SystemID.WorldStreaming);
            if (!vault.ReleaseBuffer(in handle))
                return false;

            handle = default;
            return true;
        }

        private bool TryApplyPendingDataVaultRebindCold()
        {
            if (_radarJobScheduled != 0)
                return false;

            if (!_pendingDataVaultRebind)
                return _dataVault != null;

            IDataVault oldVault = _dataVault;
            IDataVault nextVault = _pendingDataVault;
            if (ReferenceEquals(oldVault, nextVault))
            {
                _pendingDataVault = null;
                _pendingDataVaultRebind = false;
                return _dataVault != null;
            }

            if ((oldVault != null && oldVault.IsCompactionFenceActive) ||
                (nextVault != null && nextVault.IsCompactionFenceActive))
            {
                return false;
            }

            ReleaseScanJobBufferPins();
            if (oldVault != null && !ReleaseGprVaultBuffersInPostSimulationSwapWindow(oldVault))
                return false;

            _dataVault = nextVault;
            _pendingDataVault = null;
            _pendingDataVaultRebind = false;
            ClearGprVaultDescriptors();
            if (_dataVault == null)
            {
                QueueIndirectArgsClear();
                return false;
            }

            AllocatePersistentStateCold();
            return _gprReadSnapshotsValid;
        }

        private void QueueIndirectArgsClear()
        {
            _pendingIndirectArgsClear = true;
        }

        private void FlushPendingIndirectArgsClear()
        {
            if (!_pendingIndirectArgsClear)
                return;

            _pendingIndirectArgsClear = false;
            UpdateIndirectArgsBuffer(0u);
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

        private void EnsureBlackBoxDumpPathCold()
        {
            if (!string.IsNullOrEmpty(_blackBoxDumpPath))
                return;

            _blackBoxDumpPath = "Docs/AgentLogs/Dump_TERRAIN_GPR_SYSTEM.bin"; // COLD ALLOC: string[path] - GPR blackbox dump destination - owner: TERRAIN_GPR_SYSTEM
        }

        private void WriteTelemetry(uint frameId, int addedCount, int rayCount, float highestStrength, uint flags)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsGroundRadarVaultHandle(in _telemetryRingHandle, BufferID.GroundRadarTelemetryRing))
            {
                return;
            }

            bool locked = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in _telemetryRingHandle, SystemID.WorldStreaming, out NativeArray<GroundRadarTelemetryEntry> telemetryRing))
                    return;
                locked = true;

                if (!telemetryRing.IsCreated ||
                    telemetryRing.Length < GroundRadarConstants.TelemetryFrames ||
                    telemetryRing.Length == 0)
                {
                    return;
                }

                int index = _telemetryWriteIndex % telemetryRing.Length;
                GroundRadarTelemetryEntry entry = default;
                entry.Frame = frameId;
                entry.ActiveGprPings = _activeGprPings;
                entry.AddedGprPings = addedCount;
                entry.RayCount = rayCount;
                entry.HighestSignalStrength = highestStrength;
                entry.ProbeOrigin = _lastProbeOrigin;
                entry.Flags = flags;
                telemetryRing[index] = entry;
                _telemetryWriteIndex = (_telemetryWriteIndex + 1) % telemetryRing.Length;
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in _telemetryRingHandle, SystemID.WorldStreaming);
            }
        }

        private void DumpBlackBox()
        {
            if (Interlocked.CompareExchange(ref _blackBoxDumpInFlight, 1, 0) != 0)
                return;

            if (!TryStageBlackBoxDumpSnapshot())
            {
                Interlocked.Exchange(ref _blackBoxDumpInFlight, 0);
                return;
            }

            EnsureBlackBoxDumpPathCold();
            if (!ThreadPool.QueueUserWorkItem(BlackBoxDumpWorkerCallback, this))
                Interlocked.Exchange(ref _blackBoxDumpInFlight, 0);
        }

        private bool TryStageBlackBoxDumpSnapshot()
        {
            IDataVault vault = _dataVault;
            if (!TryReadVaultBuffer(vault, BufferID.GroundRadarTelemetryRing, in _telemetryRingHandle, GroundRadarConstants.TelemetryFrames, out NativeArray<GroundRadarTelemetryEntry>.ReadOnly telemetryRing))
                return false;

            int count = math.min(telemetryRing.Length, _blackBoxDumpSnapshot.Length);
            for (int i = 0; i < count; i++)
                _blackBoxDumpSnapshot[i] = telemetryRing[i];

            _blackBoxDumpSnapshotCount = count;
            _blackBoxDumpSnapshotCursor = _telemetryWriteIndex;
            return count > 0;
        }

        private static void WriteBlackBoxDumpWorker(object state)
        {
            GroundPenetratingRadarRuntime runtime = state as GroundPenetratingRadarRuntime;
            if (runtime == null)
                return;

            try
            {
                runtime.TryWriteBlackBoxSnapshotCold();
            }
            finally
            {
                Interlocked.Exchange(ref runtime._blackBoxDumpInFlight, 0);
            }
        }

        private void TryWriteBlackBoxSnapshotCold()
        {
            string path = _blackBoxDumpPath;
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                int count = math.min(math.max(0, Volatile.Read(ref _blackBoxDumpSnapshotCount)), _blackBoxDumpSnapshot.Length);
                int cursor = Volatile.Read(ref _blackBoxDumpSnapshotCursor);
                int byteCount = BlackBoxDumpHeaderBytes + count * BlackBoxDumpEntryBytes;
                NativeArray<byte> payload = default;
                try
                {
                    payload = NativeFaultDumpWriter.CreateTransientPayload(
                        byteCount,
                        nameof(GroundPenetratingRadarRuntime),
                        "GroundRadarTelemetryDumpPayload");
                    int writeCursor = 0;
                    WriteInt32LittleEndian(payload, ref writeCursor, count);
                    WriteInt32LittleEndian(payload, ref writeCursor, cursor);

                    for (int i = 0; i < count; i++)
                    {
                        GroundRadarTelemetryEntry entry = _blackBoxDumpSnapshot[i];
                        WriteUInt32LittleEndian(payload, ref writeCursor, entry.Frame);
                        WriteInt32LittleEndian(payload, ref writeCursor, entry.ActiveGprPings);
                        WriteInt32LittleEndian(payload, ref writeCursor, entry.AddedGprPings);
                        WriteInt32LittleEndian(payload, ref writeCursor, entry.RayCount);
                        WriteFloatLittleEndian(payload, ref writeCursor, entry.HighestSignalStrength);
                        WriteFloat3LittleEndian(payload, ref writeCursor, entry.ProbeOrigin);
                        WriteUInt32LittleEndian(payload, ref writeCursor, entry.Flags);
                    }

                    if (writeCursor == byteCount)
                        NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(GroundPenetratingRadarRuntime),
                        "GroundRadarTelemetryDumpPayload");
                }
            }
            catch (Exception exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(payload, ref cursor, (uint)value);
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, ref int cursor, uint value)
        {
            payload[cursor++] = (byte)value;
            payload[cursor++] = (byte)(value >> 8);
            payload[cursor++] = (byte)(value >> 16);
            payload[cursor++] = (byte)(value >> 24);
        }

        private static void WriteFloatLittleEndian(NativeArray<byte> payload, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(payload, ref cursor, math.asuint(value));
        }

        private static void WriteFloat3LittleEndian(NativeArray<byte> payload, ref int cursor, float3 value)
        {
            WriteFloatLittleEndian(payload, ref cursor, value.x);
            WriteFloatLittleEndian(payload, ref cursor, value.y);
            WriteFloatLittleEndian(payload, ref cursor, value.z);
        }

        /// <summary>
        /// Allocates the cold GPR draw payload and reports an unassigned ping material without throwing.
        /// </summary>
        /// <remarks>
        /// <c>UnityEngine.Assertions.Assert</c> THROWS in this project - nothing under Assets sets
        /// <c>Assert.raiseExceptions = false</c> - and <see cref="OnEnable"/> calls this method at line 175,
        /// BEFORE every registration the component owns. The old bare assert on
        /// <c>_radarPingAuthoredMaterial</c> therefore unwound OnEnable and deleted its entire tail:
        /// <c>EnsureBlackBoxDumpPathCold</c>, <c>GlobalRegistry.RegisterGroundRadarService</c>,
        /// <c>CacheConfiguredOreReadModel</c>, <c>CacheOreReadModelFromOwnerRoute</c> and all three lane
        /// registrations - <c>TryRegisterLateFrameTickable</c>, <c>TryRegisterSlowTickable</c> and
        /// <c>Renderables.TryRegister</c>. One unassigned inspector slot on a COSMETIC ring material silently
        /// removed subsurface ore scanning entirely, left <c>GlobalRegistry.GroundRadar</c> null for the
        /// cockpit read at UI/VehicleSubOsCockpitRuntime.cs:722, and left the fault-dump path empty so
        /// <c>WriteBlackBoxDumpWorker</c> bailed at its <c>string.IsNullOrEmpty</c> guard.
        ///
        /// The material is optional by construction: <see cref="Render"/> is its only consumer and already
        /// returns early when <c>ResolveRenderMaterial</c> is null, exactly like the cosmetic voxel bake ghost
        /// material fixed in 585401145. So this must NOT latch anything off - a missing material costs the
        /// ping rings and nothing else. The MaterialPropertyBlock is still allocated first so an inspector
        /// assignment during play-mode starts drawing on the next Render instead of being locked out.
        /// </remarks>
        private void EnsureRuntimeDrawResourcesCold()
        {
            if (_radarDrawProperties == null)
                _radarDrawProperties = new MaterialPropertyBlock(); // COLD ALLOC: per-instance GPR draw payload - owner: TERRAIN_GPR_SYSTEM

            if (_radarPingAuthoredMaterial != null || _missingRadarPingMaterialAnnounced)
                return;

            _missingRadarPingMaterialAnnounced = true;
            LogMissingRadarPingMaterial();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingRadarPingMaterial()
        {
            Hecton8.Core.H8Debug.LogError("GroundPenetratingRadarRuntime: serialized field '_radarPingAuthoredMaterial' is unassigned. Render() null-guards it and skips the cosmetic ping rings; subsurface scanning, the GlobalRegistry ground-radar service, telemetry and the black-box dump all stay live. Runtime material generation is forbidden - assign an authored procedural-indirect ping material in the inspector.");
        }

        private Material ResolveRenderMaterial()
        {
            return _radarPingAuthoredMaterial;
        }

        private void UpdateIndirectArgsBuffer(uint instanceCount)
        {
            if (!TryResolveGprArgsWriteBuffer(out GraphicsBuffer argsWriteBuffer))
                return;

            NativeArray<GroundRadarIndirectArgsDTO> argsWrite =
                argsWriteBuffer.LockBufferForWrite<GroundRadarIndirectArgsDTO>(0, 1);
            try
            {
                GroundRadarIndirectArgsDTO args = default;
                args.VertexCountPerInstance = GroundRadarProceduralVertexCount;
                args.InstanceCount = instanceCount;
                args.StartVertex = 0u;
                args.StartInstance = 0u;
                argsWrite[0] = args;
            }
            finally
            {
                argsWriteBuffer.UnlockBufferAfterWrite<GroundRadarIndirectArgsDTO>(1);
            }
            _activeGprArgsBuffer = argsWriteBuffer;
        }

        private bool TryAcquireGprPingWriteBuffer(out GraphicsBuffer buffer)
        {
            buffer = _gprUploadBufferIndex == 0 ? _gprPingBufferA : _gprPingBufferB;
            if (!IsValidBuffer(buffer))
                buffer = ReferenceEquals(_activeGprPingBuffer, _gprPingBufferA) ? _gprPingBufferB : _gprPingBufferA;

            if (!IsValidBuffer(buffer))
                return false;

            _gprUploadBufferIndex ^= 1;
            return true;
        }

        private bool TryResolveGprArgsWriteBuffer(out GraphicsBuffer buffer)
        {
            buffer = _gprUploadBufferIndex == 0 ? _gprArgsBufferA : _gprArgsBufferB;
            if (!IsValidBuffer(buffer))
                buffer = ReferenceEquals(_activeGprArgsBuffer, _gprArgsBufferA) ? _gprArgsBufferB : _gprArgsBufferA;

            return IsValidBuffer(buffer);
        }

        private static GraphicsBuffer CreateIndirectArgsBuffer()
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<GroundRadarIndirectArgsDTO>());
        }

        internal static bool ValidateGroundRadarRuntimeLayouts(out int telemetrySizeBytes, out int indirectArgsSizeBytes)
        {
            telemetrySizeBytes = UnsafeUtility.SizeOf<GroundRadarTelemetryEntry>();
            indirectArgsSizeBytes = UnsafeUtility.SizeOf<GroundRadarIndirectArgsDTO>();
            return telemetrySizeBytes == GroundRadarJobLayout.GroundRadarTelemetryEntryStrideBytes &&
                   (telemetrySizeBytes & 7) == 0 &&
                   indirectArgsSizeBytes == GroundRadarIndirectArgsSizeBytes &&
                   (indirectArgsSizeBytes & 7) == 0;
        }

        [StructLayout(LayoutKind.Explicit, Size = GroundRadarIndirectArgsSizeBytes)]
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

        private static bool IsValidBuffer(GraphicsBuffer buffer)
        {
            return buffer != null && buffer.IsValid();
        }
    }
}
