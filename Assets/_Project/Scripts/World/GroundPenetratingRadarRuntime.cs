using System;
using System.IO;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Signals;
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

        [NonSerialized] public NativeArray<float3> GprHits;
        [NonSerialized] public NativeArray<float> GprSignalStrength;

        private NativeArray<float> _gprAgeSeconds;
        private NativeArray<int> _gprOreTypes;
        private NativeArray<float4> _gprPingGpu;
        private NativeArray<int> _gprCounters;
        private NativeArray<float> _maxSignalStrength;
        private NativeArray<GroundRadarTelemetryEntry> _telemetryRing;
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

        public int ActiveGprPings => _activeGprPings;
        public int GprSequence => _gprSequence;
        public int OreFilterType => _oreFilterType;
        public float3 LastProbeOrigin => _lastProbeOrigin;
        public float ScanRadiusMeters => scanRadiusMeters;
        public NativeArray<float3>.ReadOnly GprHitsReadOnly => GprHits.IsCreated ? GprHits.AsReadOnly() : default;
        public NativeArray<float>.ReadOnly GprSignalStrengthReadOnly => GprSignalStrength.IsCreated ? GprSignalStrength.AsReadOnly() : default;

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
            DisposeNativeArray(ref GprHits);
            DisposeNativeArray(ref GprSignalStrength);
            DisposeNativeArray(ref _gprAgeSeconds);
            DisposeNativeArray(ref _gprOreTypes);
            DisposeNativeArray(ref _gprPingGpu);
            DisposeNativeArray(ref _gprCounters);
            DisposeNativeArray(ref _maxSignalStrength);
            DisposeNativeArray(ref _telemetryRing);

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
            if (!GprHits.IsCreated || _scanJobScheduled)
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
            Graphics.RenderMeshIndirect(renderParams, mesh, _gprArgsBuffer, 1, 0);
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
            if (!destination.IsCreated || !_gprPingGpu.IsCreated || _activeGprPings <= 0)
                return false;

            copiedCount = math.min(destination.Length, _activeGprPings);
            for (int i = 0; i < copiedCount; i++)
                destination[i] = _gprPingGpu[i];
            return copiedCount > 0;
        }

        public void SetOreFilterType(int oreType)
        {
            _oreFilterType = math.clamp(oreType, WorldOreTypeIds.None, WorldOreTypeIds.Silver);
        }

        private void AllocatePersistentState()
        {
            if (GprHits.IsCreated)
                return;

            GprHits = new NativeArray<float3>(GroundRadarConstants.MaxPings, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[128] - subsurface GPR hit positions - owner: TERRAIN_GPR_SYSTEM
            GprSignalStrength = new NativeArray<float>(GroundRadarConstants.MaxPings, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[128] - subsurface GPR signal strengths - owner: TERRAIN_GPR_SYSTEM
            _gprAgeSeconds = new NativeArray<float>(GroundRadarConstants.MaxPings, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[128] - subsurface GPR decay age - owner: TERRAIN_GPR_SYSTEM
            _gprOreTypes = new NativeArray<int>(GroundRadarConstants.MaxPings, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[128] - ore type lane for GPR refiltering - owner: TERRAIN_GPR_SYSTEM
            _gprPingGpu = new NativeArray<float4>(GroundRadarConstants.MaxPings, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4>[128] - GPU GPR ping payload - owner: TERRAIN_GPR_SYSTEM
            _gprCounters = new NativeArray<int>(4, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[4] - GPR job counters - owner: TERRAIN_GPR_SYSTEM
            _maxSignalStrength = new NativeArray<float>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[1] - strongest GPR return - owner: TERRAIN_GPR_SYSTEM
            _telemetryRing = new NativeArray<GroundRadarTelemetryEntry>(GroundRadarConstants.TelemetryFrames, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<GroundRadarTelemetryEntry>[300] - blackbox GPR telemetry - owner: TERRAIN_GPR_SYSTEM
            RegisterNativeArray(GprHits, nameof(GprHits));
            RegisterNativeArray(GprSignalStrength, nameof(GprSignalStrength));
            RegisterNativeArray(_gprAgeSeconds, nameof(_gprAgeSeconds));
            RegisterNativeArray(_gprOreTypes, nameof(_gprOreTypes));
            RegisterNativeArray(_gprPingGpu, nameof(_gprPingGpu));
            RegisterNativeArray(_gprCounters, nameof(_gprCounters));
            RegisterNativeArray(_maxSignalStrength, nameof(_maxSignalStrength));
            RegisterNativeArray(_telemetryRing, nameof(_telemetryRing));

            _gprPingBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(GroundRadarConstants.MaxPings); // COLD ALLOC: GraphicsBuffer[128 float4] - shared GPR StructuredBuffer - owner: TERRAIN_GPR_SYSTEM
            _gprArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - GPR indirect draw args - owner: TERRAIN_GPR_SYSTEM
            UpdateIndirectArgsBuffer(0u);
        }

        private void ScheduleRadarJob(float3 probeOrigin, float deltaTime, bool scanDue, bool hasShift, float3 aupShift)
        {
            NativeArray<byte> encodedSdf = default;
            int3 gridDimensions = default;
            float3 volumeOrigin = default;
            float3 cellSize = default;
            float sdfRange = 0f;

            if (scanDue)
                TryResolveNearestSdf(probeOrigin, out encodedSdf, out gridDimensions, out volumeOrigin, out cellSize, out sdfRange);

            TryResolveOreSource(out NativeArray<float3> orePositions, out NativeArray<int> oreTypes, out int oreCount);

            _maxSignalStrength[0] = 0f;
            GroundRadarRaymarchJob job = new GroundRadarRaymarchJob
            {
                EncodedSdf = encodedSdf,
                OrePositions = orePositions,
                OreTypes = oreTypes,
                GprHits = GprHits,
                GprSignalStrength = GprSignalStrength,
                GprAgeSeconds = _gprAgeSeconds,
                GprOreTypes = _gprOreTypes,
                GprPingGpu = _gprPingGpu,
                Counters = _gprCounters,
                MaxSignalStrength = _maxSignalStrength,
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
            int previousCount = _activeGprPings;
            _activeGprPings = _gprCounters.IsCreated && _gprCounters.Length > 0
                ? math.clamp(_gprCounters[0], 0, GroundRadarConstants.MaxPings)
                : 0;
            int addedCount = _gprCounters.IsCreated && _gprCounters.Length > 1 ? math.max(0, _gprCounters[1]) : 0;
            int rayCount = _gprCounters.IsCreated && _gprCounters.Length > 2 ? _gprCounters[2] : 0;
            _highestSignalStrength = _maxSignalStrength.IsCreated && _maxSignalStrength.Length > 0
                ? math.saturate(_maxSignalStrength[0])
                : 0f;

            int macroSwarmAddedCount = AppendMacroSwarmRadarPings();
            if (macroSwarmAddedCount > 0)
            {
                addedCount += macroSwarmAddedCount;
                _highestSignalStrength = math.max(_highestSignalStrength, 0.85f);
            }

            if (_activeGprPings > 0 && _gprPingBuffer != null)
            {
                GraphicsBufferUploadUtility.UploadNativeArray(_gprPingBuffer, _gprPingGpu, _activeGprPings);
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
                !_gprPingGpu.IsCreated ||
                _activeGprPings >= GroundRadarConstants.MaxPings)
            {
                return 0;
            }

            int remaining = GroundRadarConstants.MaxPings - _activeGprPings;
            NativeArray<float4> destination = _gprPingGpu.GetSubArray(_activeGprPings, remaining);
            if (!ecosystem.TryCopyMacroSwarmRadarPings(destination, _lastProbeOrigin, scanRadiusMeters * 4f, out int copiedCount))
                return 0;

            copiedCount = math.clamp(copiedCount, 0, remaining);
            int startIndex = _activeGprPings;
            for (int i = 0; i < copiedCount; i++)
            {
                int pingIndex = startIndex + i;
                float4 ping = _gprPingGpu[pingIndex];
                GprHits[pingIndex] = ping.xyz;
                GprSignalStrength[pingIndex] = math.saturate(ping.w);
                _gprAgeSeconds[pingIndex] = 0f;
                _gprOreTypes[pingIndex] = WorldOreTypeIds.None;
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

            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (ReferenceEquals(components[i], this))
                    continue;
                _worldResourceSpawnerReadModel = components[i] as IWorldResourceSpawnerReadModel;
                if (_worldResourceSpawnerReadModel != null)
                {
                    worldResourceSpawner = components[i];
                    return;
                }
            }
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

            IWorldResourceSpawnerReadModel resourceSpawner = GlobalRegistry.WorldResourceSpawner;
            if (resourceSpawner != null &&
                resourceSpawner.TryGetOrePositions(out orePositions, out oreCount) &&
                resourceSpawner.TryGetOreTypes(out oreTypes, out int typeCountFromRegistry))
            {
                oreCount = math.min(oreCount, typeCountFromRegistry);
                return orePositions.IsCreated && oreTypes.IsCreated && oreCount > 0;
            }

            orePositions = default;
            oreTypes = default;
            oreCount = 0;
            return false;
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
            if (!_telemetryRing.IsCreated || _telemetryRing.Length == 0)
                return;

            int index = _telemetryWriteIndex % _telemetryRing.Length;
            _telemetryRing[index] = new GroundRadarTelemetryEntry
            {
                Frame = (uint)Time.frameCount,
                ActiveGprPings = _activeGprPings,
                AddedGprPings = addedCount,
                RayCount = rayCount,
                HighestSignalStrength = highestStrength,
                ProbeOrigin = _lastProbeOrigin,
                Flags = flags
            };
            _telemetryWriteIndex = (_telemetryWriteIndex + 1) % _telemetryRing.Length;
        }

        private void DumpBlackBox()
        {
            if (!_telemetryRing.IsCreated)
                return;

            try
            {
                string path = Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_TERRAIN_GPR_SYSTEM.bin");
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(_telemetryRing.Length);
                writer.Write(_telemetryWriteIndex);
                for (int i = 0; i < _telemetryRing.Length; i++)
                {
                    GroundRadarTelemetryEntry entry = _telemetryRing[i];
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

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, OwnerName, label, NativeAllocationLifetime.Scene);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
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
