using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.World.GPR;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/World/Ground Penetrating Radar Runtime")]
    public sealed class GroundPenetratingRadarRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IRenderable, IGroundRadarService, IDisposable
    {
        private const string OwnerName = "TERRAIN_GPR_SYSTEM";
        private const byte SubsurfaceAcousticChannel = 3;
        private const byte GprReturnState = 7;
        private const uint GprSourceHash = 0x4750525Fu; // GPR_
        private const uint GprReturnHash = 0x47505252u; // GPRR
        private const uint TelemetryFaultFlag = 1u << 31;
        private static readonly int GroundRadarPingsId = Shader.PropertyToID("_GroundRadarPings");
        private static readonly int GroundRadarPulseId = Shader.PropertyToID("_GroundRadarPulse");
        private static readonly int GroundRadarScaleId = Shader.PropertyToID("_GroundRadarScale");

        [Header("Dependencies")]
        [SerializeField] private MonoBehaviour worldResourceSpawner;
        [SerializeField] private Mesh radarPingMesh;
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

        private VaultBufferHandle<float3> _gprHitsHandle;
        private VaultBufferHandle<float> _gprSignalStrengthHandle;
        private VaultBufferHandle<float> _gprAgeSecondsHandle;
        private VaultBufferHandle<int> _gprOreTypesHandle;
        private VaultBufferHandle<float4> _gprPingGpuHandle;
        private VaultBufferHandle<int> _gprCountersHandle;
        private VaultBufferHandle<float> _maxSignalStrengthHandle;
        private VaultBufferHandle<GroundRadarTelemetryEntry> _telemetryRingHandle;
        private GraphicsBuffer _gprPingBuffer;
        private GraphicsBuffer _gprArgsBuffer;
        private Mesh _runtimeQuadMesh;
        private Material _runtimeMaterial;
        private IEcosystemDirectorService _ecosystemDirector;
        private IWorldResourceSpawnerReadModel _worldResourceSpawnerReadModel;
        private JobHandle _scanJobHandle;
        private Bounds _drawBounds;
        private Transform _cachedPlayerTransform;
        private int _activeGprPings;
        private int _gprSequence;
        private int _telemetryWriteIndex;
        private int _lastScannerSignalSequence;
        private int _oreFilterType;
        private int _registeredUpdate;
        private int _registeredLateFrame;
        private int _registeredRenderable;
        private bool _scanJobScheduled;
        private float _scanTimer;
        private float _highestSignalStrength;
        private float3 _lastProbeOrigin;
        private readonly List<MonoBehaviour> _componentProbe = new List<MonoBehaviour>(16); // COLD ALLOC: List<MonoBehaviour>[16] - configured ore read-model probe scratch - owner: TERRAIN_GPR_SYSTEM

        public int ActiveGprPings => _activeGprPings;
        public int GprSequence => _gprSequence;
        public int OreFilterType => _oreFilterType;
        public float3 LastProbeOrigin => _lastProbeOrigin;
        public float ScanRadiusMeters => scanRadiusMeters;
        public NativeArray<float3>.ReadOnly GprHitsReadOnly => TryResolveGprHits(out NativeArray<float3> hits) ? hits.AsReadOnly() : default;
        public NativeArray<float>.ReadOnly GprSignalStrengthReadOnly => TryResolveGprSignalStrength(out NativeArray<float> signalStrength) ? signalStrength.AsReadOnly() : default;

        private void Awake()
        {
            ResolveConfiguredOreReadModel();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            AllocatePersistentState();
            EnsureRuntimeDrawResources();
            GlobalRegistry.RegisterGroundRadarService(this);
            ResolveConfiguredOreReadModel();
            ResolveOreReadModelFromRegistry();
            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment) ? 1 : 0;
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment) ? 1 : 0;
            _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this) ? 1 : 0;
            _ecosystemDirector = GlobalRegistry.EcosystemDirector;
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
            if (_scanJobScheduled)
            {
                _scanJobHandle.Complete();
                _scanJobScheduled = false;
            }

            ReleaseGraphicsBuffer(ref _gprPingBuffer);
            ReleaseGraphicsBuffer(ref _gprArgsBuffer);
            _gprHitsHandle = default;
            _gprSignalStrengthHandle = default;
            _gprAgeSecondsHandle = default;
            _gprOreTypesHandle = default;
            _gprPingGpuHandle = default;
            _gprCountersHandle = default;
            _maxSignalStrengthHandle = default;
            _telemetryRingHandle = default;
            _activeGprPings = 0;
            _highestSignalStrength = 0f;

            if (_runtimeQuadMesh != null)
            {
                Destroy(_runtimeQuadMesh);
                _runtimeQuadMesh = null;
            }

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_scanJobScheduled || !TryResolveGprHits(out _))
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
            if (!_scanJobScheduled || !_scanJobHandle.IsCompleted)
                return;

            _scanJobHandle.Complete();
            _scanJobScheduled = false;
            CommitCompletedScan();
        }

        public void Render(float deltaTime)
        {
            if (_activeGprPings <= 0 || _gprArgsBuffer == null || _gprPingBuffer == null)
                return;

            Material material = ResolveRenderMaterial();
            Mesh mesh = ResolveRenderMesh();
            if (material == null || mesh == null)
                return;

            material.SetBuffer(GroundRadarPingsId, _gprPingBuffer);
            material.SetFloat(GroundRadarPulseId, Time.time);
            material.SetFloat(GroundRadarScaleId, math.max(0.1f, ringScaleMeters));

            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = _drawBounds,
                layer = renderLayer,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, _gprArgsBuffer, 1, 0);
        }

        public bool TryGetGprPingBuffer(out GraphicsBuffer buffer, out int activeCount, out int sequence)
        {
            buffer = _gprPingBuffer;
            activeCount = _activeGprPings;
            sequence = _gprSequence;
            return buffer != null && activeCount > 0;
        }

        public bool TryCopyGprPings(NativeArray<float4> destination, out int copiedCount)
        {
            copiedCount = 0;
            if (!destination.IsCreated ||
                _activeGprPings <= 0 ||
                !TryResolveGprPingGpu(out NativeArray<float4> pingGpu))
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
            if (_gprHitsHandle.IsCreated && _gprPingBuffer != null && _gprArgsBuffer != null)
                return;

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
            _activeGprPings = 0;
            _highestSignalStrength = 0f;
            _telemetryWriteIndex = 0;

            if (_gprPingBuffer == null)
                _gprPingBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(GroundRadarConstants.MaxPings); // COLD ALLOC: GraphicsBuffer[128 float4] - shared GPR StructuredBuffer - owner: TERRAIN_GPR_SYSTEM
            if (_gprArgsBuffer == null)
                _gprArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - GPR indirect draw args - owner: TERRAIN_GPR_SYSTEM
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

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            _gprHitsHandle = vault.GetBufferHandle<float3>(
                BufferID.GroundRadarHits,
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _gprSignalStrengthHandle = vault.GetBufferHandle<float>(
                BufferID.GroundRadarSignalStrength,
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _gprAgeSecondsHandle = vault.GetBufferHandle<float>(
                BufferID.GroundRadarAgeSeconds,
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _gprOreTypesHandle = vault.GetBufferHandle<int>(
                BufferID.GroundRadarOreTypes,
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _gprPingGpuHandle = vault.GetBufferHandle<float4>(
                BufferID.GroundRadarPingGpu,
                GroundRadarConstants.MaxPings,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _gprCountersHandle = vault.GetBufferHandle<int>(
                BufferID.GroundRadarCounters,
                4,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _maxSignalStrengthHandle = vault.GetBufferHandle<float>(
                BufferID.GroundRadarMaxSignalStrength,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);
            _telemetryRingHandle = vault.GetBufferHandle<GroundRadarTelemetryEntry>(
                BufferID.GroundRadarTelemetryRing,
                GroundRadarConstants.TelemetryFrames,
                SystemID.WorldStreaming,
                NativeArrayOptions.ClearMemory);

            return TryResolveGprState(
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

        private bool TryResolveGprState(
            out NativeArray<float3> hits,
            out NativeArray<float> signalStrength,
            out NativeArray<float> ageSeconds,
            out NativeArray<int> oreTypes,
            out NativeArray<float4> pingGpu,
            out NativeArray<int> counters,
            out NativeArray<float> maxSignalStrength,
            out NativeArray<GroundRadarTelemetryEntry> telemetryRing)
        {
            return TryResolveGprState(
                GlobalRegistry.DataVault,
                out hits,
                out signalStrength,
                out ageSeconds,
                out oreTypes,
                out pingGpu,
                out counters,
                out maxSignalStrength,
                out telemetryRing);
        }

        private bool TryResolveGprState(
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
            bool resolvedHits = TryResolveVaultBuffer(vault, ref _gprHitsHandle, GroundRadarConstants.MaxPings, out hits);
            bool resolvedSignal = TryResolveVaultBuffer(vault, ref _gprSignalStrengthHandle, GroundRadarConstants.MaxPings, out signalStrength);
            bool resolvedAge = TryResolveVaultBuffer(vault, ref _gprAgeSecondsHandle, GroundRadarConstants.MaxPings, out ageSeconds);
            bool resolvedOreTypes = TryResolveVaultBuffer(vault, ref _gprOreTypesHandle, GroundRadarConstants.MaxPings, out oreTypes);
            bool resolvedPingGpu = TryResolveVaultBuffer(vault, ref _gprPingGpuHandle, GroundRadarConstants.MaxPings, out pingGpu);
            bool resolvedCounters = TryResolveVaultBuffer(vault, ref _gprCountersHandle, 4, out counters);
            bool resolvedMaxSignal = TryResolveVaultBuffer(vault, ref _maxSignalStrengthHandle, 1, out maxSignalStrength);
            bool resolvedTelemetry = TryResolveVaultBuffer(vault, ref _telemetryRingHandle, GroundRadarConstants.TelemetryFrames, out telemetryRing);

            return resolvedHits &&
                resolvedSignal &&
                resolvedAge &&
                resolvedOreTypes &&
                resolvedPingGpu &&
                resolvedCounters &&
                resolvedMaxSignal &&
                resolvedTelemetry;
        }

        private bool TryResolveGprHits(out NativeArray<float3> hits)
        {
            return TryResolveVaultBuffer(GlobalRegistry.DataVault, ref _gprHitsHandle, GroundRadarConstants.MaxPings, out hits);
        }

        private bool TryResolveGprSignalStrength(out NativeArray<float> signalStrength)
        {
            return TryResolveVaultBuffer(GlobalRegistry.DataVault, ref _gprSignalStrengthHandle, GroundRadarConstants.MaxPings, out signalStrength);
        }

        private bool TryResolveGprPingGpu(out NativeArray<float4> pingGpu)
        {
            return TryResolveVaultBuffer(GlobalRegistry.DataVault, ref _gprPingGpuHandle, GroundRadarConstants.MaxPings, out pingGpu);
        }

        private bool TryResolveGprTelemetry(out NativeArray<GroundRadarTelemetryEntry> telemetryRing)
        {
            return TryResolveVaultBuffer(GlobalRegistry.DataVault, ref _telemetryRingHandle, GroundRadarConstants.TelemetryFrames, out telemetryRing);
        }

        private static bool TryResolveVaultBuffer<T>(
            IDataVault vault,
            ref VaultBufferHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || !handle.IsCreated)
                return false;

            buffer = handle.Resolve(vault);
            return buffer.IsCreated && buffer.Length >= requiredLength;
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
            if (!TryResolveGprState(
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

            NativeArray<byte> encodedSdf = default;
            int3 gridDimensions = default;
            float3 volumeOrigin = default;
            float3 cellSize = default;
            float sdfRange = 0f;

            if (scanDue)
                TryResolveNearestSdf(probeOrigin, out encodedSdf, out gridDimensions, out volumeOrigin, out cellSize, out sdfRange);

            TryResolveOreSource(out NativeArray<float3> orePositions, out NativeArray<int> oreTypes, out int oreCount);

            maxSignalStrength[0] = 0f;
            GroundRadarRaymarchJob job = new GroundRadarRaymarchJob
            {
                EncodedSdf = encodedSdf.IsCreated ? new NativeSlice<byte>(encodedSdf) : default,
                OrePositions = orePositions.IsCreated ? new NativeSlice<float3>(orePositions) : default,
                OreTypes = oreTypes.IsCreated ? new NativeSlice<int>(oreTypes) : default,
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
                RequestedRayCount = ResolveRayCount(),
                MaxSteps = math.min(maxRaymarchSteps, GroundRadarConstants.MaxRaymarchSteps),
                ProbeOrigin = probeOrigin,
                ScanRadiusMeters = scanRadiusMeters,
                StepMeters = stepMeters,
                DeltaTime = deltaTime,
                RuntimeShift = aupShift,
                Flags = (scanDue ? GroundRadarConstants.ScanFlag : 0u) |
                        (hasShift ? GroundRadarConstants.AupShiftFlag : 0u)
            };

            _scanJobHandle = job.Schedule();
            _scanJobScheduled = true;
        }

        private void CommitCompletedScan()
        {
            if (!TryResolveGprState(
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

            int macroSwarmAddedCount = AppendMacroSwarmRadarPings();
            if (macroSwarmAddedCount > 0)
            {
                addedCount += macroSwarmAddedCount;
                _highestSignalStrength = math.max(_highestSignalStrength, 0.85f);
            }

            if (_activeGprPings > 0 && _gprPingBuffer != null)
            {
                GraphicsBufferUploadUtility.UploadNativeArray(_gprPingBuffer, pingGpu, _activeGprPings);
                _drawBounds = new Bounds(
                    new Vector3(_lastProbeOrigin.x, _lastProbeOrigin.y - stepMeters * maxRaymarchSteps * 0.5f, _lastProbeOrigin.z),
                    Vector3.one * math.max(16f, scanRadiusMeters * 3f));
            }

            if (_activeGprPings != previousCount)
            {
                _gprSequence++;
                UpdateIndirectArgsBuffer((uint)_activeGprPings);
            }

            WriteTelemetry(addedCount, rayCount, _highestSignalStrength, 0u);
            if (!math.all(math.isfinite(_lastProbeOrigin)) || !math.isfinite(_highestSignalStrength))
            {
                WriteTelemetry(addedCount, rayCount, _highestSignalStrength, TelemetryFaultFlag);
                DumpBlackBox();
            }

            if (addedCount > 0)
                PublishGprSignals(_highestSignalStrength);
        }

        private int AppendMacroSwarmRadarPings()
        {
            IEcosystemDirectorService ecosystem = _ecosystemDirector;
            if (ecosystem == null || !ecosystem.IsInitialized)
            {
                ecosystem = GlobalRegistry.EcosystemDirector;
                _ecosystemDirector = ecosystem;
            }

            if (ecosystem == null ||
                !ecosystem.IsInitialized ||
                _activeGprPings >= GroundRadarConstants.MaxPings ||
                !TryResolveGprState(
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

            if (GlobalSignals.TryGetLatestScannerToolActiveSignal(out ScannerToolActiveSignal latest, out int latestSequence))
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
            ISubmarineState state = GlobalRegistry.SubmarineState;
            if (state != null)
            {
                probeOrigin = state.StateSnapshot.RuntimePosition;
                if (math.all(math.isfinite(probeOrigin)))
                    return true;
            }

            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            Rigidbody hull = submarine != null ? submarine.HullRigidbody : null;
            if (hull != null)
            {
                Vector3 position = hull.position;
                probeOrigin = new float3(position.x, position.y, position.z);
                if (math.all(math.isfinite(probeOrigin)))
                    return true;
            }

            if (WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _cachedPlayerTransform) && _cachedPlayerTransform != null)
            {
                Vector3 position = _cachedPlayerTransform.position;
                probeOrigin = new float3(position.x, position.y, position.z);
                return math.all(math.isfinite(probeOrigin));
            }

            probeOrigin = default;
            return false;
        }

        private static bool TryResolveNearestSdf(
            float3 probeOrigin,
            out NativeArray<byte> encodedSdf,
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

            HectonVoxelEngine voxelEngine = GlobalRegistry.VoxelEngine;
            if (voxelEngine == null)
                return false;

            Vector3 origin = new Vector3(probeOrigin.x, probeOrigin.y, probeOrigin.z);
            if (!voxelEngine.TryGetNearestActiveVolume(origin, out HectonVoxelVolume volume) || volume == null)
                return false;

            if (!volume.TryGetPublishedSonarSdfPayload(
                    out NativeArray<byte> payload,
                    out Vector3Int dimensions,
                    out Vector3 payloadOrigin,
                    out Vector3 payloadCellSize,
                    out float payloadRange,
                    out _))
            {
                return false;
            }

            encodedSdf = payload;
            gridDimensions = new int3(dimensions.x, dimensions.y, dimensions.z);
            volumeOrigin = new float3(payloadOrigin.x, payloadOrigin.y, payloadOrigin.z);
            cellSize = new float3(payloadCellSize.x, payloadCellSize.y, payloadCellSize.z);
            sdfRange = payloadRange;
            return true;
        }

        private static int ResolveRayCount()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350 || tier == HectonQualityTier.Unknown
                ? GroundRadarConstants.LowTierRays
                : GroundRadarConstants.MaxRays;
        }

        private void ResolveConfiguredOreReadModel()
        {
            _worldResourceSpawnerReadModel = worldResourceSpawner as IWorldResourceSpawnerReadModel;
            if (_worldResourceSpawnerReadModel != null)
                return;

            _componentProbe.Clear();
            GetComponents(_componentProbe);
            for (int i = 0; i < _componentProbe.Count; i++)
            {
                MonoBehaviour component = _componentProbe[i];
                if (ReferenceEquals(component, this))
                    continue;
                _worldResourceSpawnerReadModel = component as IWorldResourceSpawnerReadModel;
                if (_worldResourceSpawnerReadModel != null)
                {
                    worldResourceSpawner = component;
                    _componentProbe.Clear();
                    return;
                }
            }

            _componentProbe.Clear();
        }

        private bool TryResolveOreSource(out NativeArray<float3> orePositions, out NativeArray<int> oreTypes, out int oreCount)
        {
            IWorldResourceSpawnerReadModel configuredSpawner = _worldResourceSpawnerReadModel;
            if (configuredSpawner != null &&
                configuredSpawner.TryGetOrePositions(out orePositions, out oreCount) &&
                configuredSpawner.TryGetOreTypes(out oreTypes, out int typeCount))
            {
                oreCount = math.min(oreCount, typeCount);
                return orePositions.IsCreated && oreTypes.IsCreated && oreCount > 0;
            }

            orePositions = default;
            oreTypes = default;
            oreCount = 0;
            return false;
        }

        private bool ResolveOreReadModelFromRegistry()
        {
            if (_worldResourceSpawnerReadModel != null)
                return true;

            _worldResourceSpawnerReadModel = GlobalRegistry.WorldResourceSpawner;
            return _worldResourceSpawnerReadModel != null;
        }

        private void PublishGprSignals(float highestStrength)
        {
            float clampedStrength = math.saturate(highestStrength);
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(
                new Vector3(_lastProbeOrigin.x, _lastProbeOrigin.y, _lastProbeOrigin.z));

            GlobalSignals.Publish(new AcousticPingSignal
            {
                PositionAup = positionAup,
                RadiusMeters = scanRadiusMeters,
                Intensity01 = clampedStrength,
                SourceId = GprSourceHash,
                Channel = SubsurfaceAcousticChannel,
                Flags = 1
            });

            GlobalSignals.Publish(new ToolAcousticSignal
            {
                ToolHash = GprSourceHash,
                TargetHash = GprReturnHash,
                Progress01 = clampedStrength,
                PitchScale = 0.85f + clampedStrength * 0.5f,
                Intensity01 = clampedStrength,
                Frame = (uint)Time.frameCount,
                State = GprReturnState,
                Flags = 1
            });
        }

        private void WriteTelemetry(int addedCount, int rayCount, float highestStrength, uint flags)
        {
            if (!TryResolveGprTelemetry(out NativeArray<GroundRadarTelemetryEntry> telemetryRing) || telemetryRing.Length == 0)
                return;

            int index = _telemetryWriteIndex % telemetryRing.Length;
            telemetryRing[index] = new GroundRadarTelemetryEntry
            {
                Frame = (uint)Time.frameCount,
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
            if (!TryResolveGprTelemetry(out NativeArray<GroundRadarTelemetryEntry> telemetryRing))
                return;

            try
            {
                string path = Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_TERRAIN_GPR_SYSTEM.bin");
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
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
                Debug.LogError("[TERRAIN_GPR_SYSTEM] Failed to dump GPR blackbox: " + exception.Message, this);
            }
        }

        private void EnsureRuntimeDrawResources()
        {
            if (radarPingMesh == null && _runtimeQuadMesh == null)
                _runtimeQuadMesh = CreateQuadMesh();

            if (radarPingMaterial == null && _runtimeMaterial == null)
            {
                Shader shader = Shader.Find("Hecton8/World/GroundRadarPingIndirect");
                if (shader != null)
                {
                    _runtimeMaterial = new Material(shader)
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

        private Mesh ResolveRenderMesh()
        {
            return radarPingMesh != null ? radarPingMesh : _runtimeQuadMesh;
        }

        private static Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "GroundRadarPingQuad",
                hideFlags = HideFlags.DontSave
            };
            mesh.vertices = new[]
            {
                new Vector3(-1f, 0f, -1f),
                new Vector3(1f, 0f, -1f),
                new Vector3(1f, 0f, 1f),
                new Vector3(-1f, 0f, 1f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void UpdateIndirectArgsBuffer(uint instanceCount)
        {
            if (_gprArgsBuffer == null)
                return;

            Mesh mesh = ResolveRenderMesh();
            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _gprArgsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = mesh != null ? mesh.GetIndexCount(0) : 0u,
                instanceCount = instanceCount,
                startIndex = mesh != null ? mesh.GetIndexStart(0) : 0u,
                baseVertexIndex = mesh != null ? (uint)math.max(0, mesh.GetBaseVertex(0)) : 0u,
                startInstance = 0u
            };
            _gprArgsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
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
