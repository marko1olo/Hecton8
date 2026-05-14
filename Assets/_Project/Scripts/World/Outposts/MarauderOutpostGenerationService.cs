using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Signals;
using Hecton8.Gameplay;
using Hecton8.Logistics.Grid.Contracts;
using Hecton8.Narrative;
using Hecton8.Power;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World.Outposts
{
    /// <summary>
    /// Deterministic abandoned marauder outpost generator. Shell pieces are native/GPU data; only gameplay proxies become pooled GameObjects.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/World/Marauder Outpost Generation Service")]
    public sealed class MarauderOutpostGenerationService : MonoBehaviour, IUpdatable, ILateFrameTickable, IRenderable, IOutpostGenerationService
    {
        private const string OwnerName = "MARAUDER_OUTPOST_ARCHITECT";
        private const ulong DefaultFirstBaseHash = 0x4D41524155444552UL; // MARAUDER
        private const uint DefaultWorldSeed = 0x48454338u; // HEC8
        private const float DefaultCellSizeMeters = 4f;
        private const float DefaultFloorHeightMeters = 3f;
        private const float DefaultStiltClearanceMeters = 1.6f;
        private const float DefaultOutpostAge01 = 0.92f;
        private const int GeneratedSignalReplayFrames = 4;
        private const int GeneratedSignalHeartbeatFrames = 60;
        private const ulong TelemetryDumpMagic = 0x00384E4F54434548UL; // HECTON8\0 as little-endian bytes.
        private const uint TelemetryDumpVersion = 1u;
        private const int TelemetryDumpEntryPayloadBytes = 72;
        private const float ShiftEpsilonMeters = 0.0001f;

        private static readonly int OutpostMatricesId = Shader.PropertyToID("_OutpostMatrices");
        private static readonly int OutpostCellTypesId = Shader.PropertyToID("_OutpostCellTypes");
        private static readonly int OutpostAge01Id = Shader.PropertyToID("_OutpostAge01");
        private static readonly int HectonMaterialDecayRuntimeId = Shader.PropertyToID("_HectonMaterialDecayRuntime");

        private enum JobPhase : byte
        {
            None = 0,
            Solving = 1,
            Extracting = 2,
            Shifting = 3
        }

        [Header("Trigger")]
        [SerializeField] private ulong firstBaseHash = DefaultFirstBaseHash;
        [SerializeField] private bool generateOnAnyHydratedSectorForDebug;
        [SerializeField] private uint fallbackWorldSeed = DefaultWorldSeed;
        [SerializeField] private Transform outpostOriginOverride;
        [SerializeField] private Vector3 localOriginOffsetMeters;

        [Header("Shape")]
        [SerializeField, Min(1f)] private float cellSizeMeters = DefaultCellSizeMeters;
        [SerializeField, Min(1f)] private float floorHeightMeters = DefaultFloorHeightMeters;
        [SerializeField, Min(0.25f)] private float stiltClearanceMeters = DefaultStiltClearanceMeters;
        [SerializeField, Range(0f, 1f)] private float outpostAge01 = DefaultOutpostAge01;

        [Header("Rendering")]
        [SerializeField] private Mesh shellMesh;
        [SerializeField] private Material shellMaterial;
        [SerializeField] private int renderLayer;
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;
        [SerializeField] private bool receiveShadows;

        [Header("Interactables")]
        [SerializeField] private GameObject sealedDoorProxyPrefab;
        [SerializeField] private GameObject datapadProxyPrefab;

        [NonSerialized] public NativeArray<byte> WfcGrid;

        private NativeArray<float4x4> _shellMatrices;
        private NativeArray<uint> _shellCellTypes;
        private NativeArray<OutpostInteractableSpawn> _interactableSpawns;
        private NativeArray<byte> _wfcMutableStateGrid;
        private NativeArray<int> _counters;
        private NativeArray<OutpostTelemetryEntry> _telemetryRing;
        private GraphicsBuffer _matrixBuffer;
        private GraphicsBuffer _cellTypeBuffer;
        private GraphicsBuffer _argsBuffer;
        private Mesh _runtimeShellMesh;
        private Material _runtimeShellMaterial;
        private GameObject[] _spawnedInteractables;
        private SealedDoor[] _spawnedDoorControllers;
        private JobHandle _jobHandle;
        private Bounds _drawBounds;
        private OutpostGenerationSnapshot _latestSnapshot;
        private JobPhase _jobPhase;
        private OutpostGenerationQualityTier _qualityTier;
        private OutpostGenerationState _state;
        private float3 _generationOrigin;
        private float3 _pendingShift;
        private int3 _activeDimensions;
        private ulong _activeSectorHash;
        private uint _activeWorldSeed;
        private uint _activeSolveSeed;
        private uint _activeGridHash;
        private uint _publishedPowerGridHandle;
        private uint _generationSequence;
        private uint _pendingShiftFrameId;
        private uint _lastShiftFrameId;
        private int _matrixCount;
        private int _interactableCount;
        private int _solidCellCount;
        private int _supportCount;
        private int _telemetryWriteIndex;
        private int _registeredUpdate;
        private int _registeredLateFrame;
        private int _registeredRenderable;
        private int _generatedSignalReplayFrames;
        private int _generatedSignalHeartbeatFrames;
        private bool _generated;
        private bool _matrixUploadDirty;
        private bool _hasPendingShift;
        private bool _heightmapFallback;

        public bool IsGenerated => _generated;
        public bool IsBusy => _jobPhase != JobPhase.None;
        public ulong FirstBaseHash => firstBaseHash;
        public OutpostGenerationSnapshot LatestSnapshot => _latestSnapshot;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            AllocatePersistentState();
            EnsureGraphicsResources();
            BakeInteractableProxyMeshes();
            GlobalRegistry.RegisterOutpostGenerationService(this);
            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment) ? 1 : 0;
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment) ? 1 : 0;
            _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this) ? 1 : 0;
            SetState(OutpostGenerationState.Idle);
        }

        private void OnDisable()
        {
            Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void OnValidate()
        {
            cellSizeMeters = SanitizeMin(cellSizeMeters, 1f, DefaultCellSizeMeters);
            floorHeightMeters = SanitizeMin(floorHeightMeters, 1f, DefaultFloorHeightMeters);
            stiltClearanceMeters = SanitizeMin(stiltClearanceMeters, 0.25f, DefaultStiltClearanceMeters);
            outpostAge01 = Sanitize01(outpostAge01, DefaultOutpostAge01);
            if (!IsFinite(localOriginOffsetMeters))
                localOriginOffsetMeters = Vector3.zero;
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

            if (ReferenceEquals(GlobalRegistry.OutpostGeneration, this))
                GlobalRegistry.UnregisterOutpostGenerationService(this);

            ReleasePublishedPowerGrid();

            bool deferNativeDispose = _jobPhase != JobPhase.None;
            JobHandle nativeDisposeDependency = deferNativeDispose ? _jobHandle : default;
            _jobPhase = JobPhase.None;

            DespawnInteractables();
            ReleaseGraphicsBuffer(ref _matrixBuffer);
            ReleaseGraphicsBuffer(ref _cellTypeBuffer);
            ReleaseGraphicsBuffer(ref _argsBuffer);
            DisposeNativeArray(ref WfcGrid, ref nativeDisposeDependency, deferNativeDispose);
            DisposeNativeArray(ref _wfcMutableStateGrid, ref nativeDisposeDependency, deferNativeDispose);
            DisposeNativeArray(ref _shellMatrices, ref nativeDisposeDependency, deferNativeDispose);
            DisposeNativeArray(ref _shellCellTypes, ref nativeDisposeDependency, deferNativeDispose);
            DisposeNativeArray(ref _interactableSpawns, ref nativeDisposeDependency, deferNativeDispose);
            DisposeNativeArray(ref _counters, ref nativeDisposeDependency, deferNativeDispose);
            DisposeNativeArray(ref _telemetryRing, ref nativeDisposeDependency, deferNativeDispose);
            _jobHandle = nativeDisposeDependency;

            if (_runtimeShellMesh != null)
            {
                Destroy(_runtimeShellMesh);
                _runtimeShellMesh = null;
            }

            if (_runtimeShellMaterial != null)
            {
                Destroy(_runtimeShellMaterial);
                _runtimeShellMaterial = null;
            }

            _generated = false;
            _matrixCount = 0;
            _interactableCount = 0;
            _matrixUploadDirty = false;
            _hasPendingShift = false;
            _generatedSignalReplayFrames = 0;
            _generatedSignalHeartbeatFrames = 0;
            _state = OutpostGenerationState.Idle;
            _latestSnapshot = default;
        }

        public void Tick(float deltaTime)
        {
            if (!WfcGrid.IsCreated)
                return;

            DrainAupShiftSignals();
            DrainSectorHydratedSignals();
            ReplayGeneratedSignalIfNeeded();
            WriteTelemetry(0u);
        }

        public void LateFrameTick()
        {
            if (_jobPhase == JobPhase.Solving)
            {
                if (!_jobHandle.IsCompleted)
                    return;

                _jobHandle.Complete();
                ScheduleMatrixExtraction();
                return;
            }

            if (_jobPhase == JobPhase.Extracting)
            {
                if (!_jobHandle.IsCompleted)
                    return;

                _jobHandle.Complete();
                CommitCompletedGeneration();
                return;
            }

            if (_jobPhase == JobPhase.Shifting)
            {
                if (!_jobHandle.IsCompleted)
                    return;

                _jobHandle.Complete();
                _jobPhase = JobPhase.None;
                _matrixUploadDirty = true;
                SetState(_generated ? OutpostGenerationState.Ready : OutpostGenerationState.Idle);
            }

            if (_matrixUploadDirty)
                UploadMatricesAndArgs();

            if (_hasPendingShift && _jobPhase == JobPhase.None)
            {
                float3 shift = _pendingShift;
                uint shiftFrame = _pendingShiftFrameId;
                _pendingShift = default;
                _pendingShiftFrameId = 0u;
                _hasPendingShift = false;
                ApplyAupShift(shift, shiftFrame);
            }

            ProcessDoorPowerSignals();
        }

        public void Render(float deltaTime)
        {
            if (!_generated || _matrixCount <= 0 || _matrixBuffer == null || _argsBuffer == null)
                return;

            Material material = ResolveRenderMaterial();
            Mesh mesh = ResolveRenderMesh();
            if (material == null || mesh == null || mesh.subMeshCount <= 0)
                return;

            float age = ResolveOutpostAge01();
            Shader.SetGlobalFloat(OutpostAge01Id, age);
            Shader.SetGlobalVector(HectonMaterialDecayRuntimeId, new Vector4(age, 0.55f, _qualityTier == OutpostGenerationQualityTier.Low ? 1f : 0f, (_activeSolveSeed & 0xFFFFu) * MarauderOutpostConstants.HeightUShortToUnit));
            material.SetBuffer(OutpostMatricesId, _matrixBuffer);
            material.SetBuffer(OutpostCellTypesId, _cellTypeBuffer);

            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = _drawBounds,
                layer = renderLayer,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = receiveShadows,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };
            Graphics.RenderMeshIndirect(renderParams, mesh, _argsBuffer, 1, 0);
        }

        public bool TryRequestGeneration(ulong sectorHash, float3 originMeters, uint worldSeed)
        {
            if (!WfcGrid.IsCreated)
                AllocatePersistentState();

            EnsureGraphicsResources();

            if (_jobPhase != JobPhase.None)
                return false;

            if (_generated && sectorHash == _activeSectorHash && worldSeed == _activeWorldSeed)
            {
                bool published = TryPublishGeneratedSignal();
                SetState(published ? OutpostGenerationState.Ready : OutpostGenerationState.Faulted);
                return published;
            }

            if (!math.all(math.isfinite(originMeters)))
                originMeters = ResolveGenerationOriginMeters();

            DespawnInteractables();
            _generated = false;
            _matrixCount = 0;
            _interactableCount = 0;
            _solidCellCount = 0;
            _supportCount = 0;
            _heightmapFallback = false;
            ReleasePublishedPowerGrid();
            _activeSectorHash = sectorHash;
            _activeWorldSeed = worldSeed;
            _activeSolveSeed = MarauderOutpostHash.LcgHash((ulong)worldSeed + firstBaseHash);
            _activeGridHash = 0u;
            _generationOrigin = originMeters;
            _qualityTier = ResolveQualityTier();
            _activeDimensions = _qualityTier == OutpostGenerationQualityTier.Low
                ? new int3(MarauderOutpostConstants.LowWidth, MarauderOutpostConstants.LowHeight, MarauderOutpostConstants.LowDepth)
                : new int3(MarauderOutpostConstants.FullWidth, MarauderOutpostConstants.FullHeight, MarauderOutpostConstants.FullDepth);

            RestoreWfcMutableState(sectorHash);

            MarauderOutpostSolveJob job = new MarauderOutpostSolveJob
            {
                WfcGrid = WfcGrid,
                Dimensions = _activeDimensions,
                Seed = _activeSolveSeed,
                LowTier = _qualityTier == OutpostGenerationQualityTier.Low ? (byte)1 : (byte)0
            };

            _jobHandle = job.Schedule();
            _jobPhase = JobPhase.Solving;
            SetState(OutpostGenerationState.Solving);
            _generationSequence++;
            UpdateSnapshot();
            return true;
        }

        public bool TryGetWfcGrid(out NativeArray<byte>.ReadOnly cells, out int3 dimensions, out int cellCount, out uint gridHash, out uint generationSequence)
        {
            cells = WfcGrid.IsCreated ? WfcGrid.AsReadOnly() : default;
            dimensions = _activeDimensions;
            cellCount = WfcGrid.IsCreated ? math.min(ResolveActiveCellCount(), WfcGrid.Length) : 0;
            gridHash = _activeGridHash;
            generationSequence = _generationSequence;
            return _generated && WfcGrid.IsCreated && cellCount > 0;
        }

        public bool TryGetShellMatrices(out NativeArray<float4x4>.ReadOnly matrices, out int matrixCount, out uint generationSequence)
        {
            matrices = _shellMatrices.IsCreated ? _shellMatrices.AsReadOnly() : default;
            matrixCount = _matrixCount;
            generationSequence = _generationSequence;
            return _generated && _shellMatrices.IsCreated && _matrixCount > 0;
        }

        public bool TryGetShellGraphicsBuffer(out GraphicsBuffer matrixBuffer, out GraphicsBuffer argsBuffer, out int instanceCount, out uint generationSequence)
        {
            matrixBuffer = _matrixBuffer;
            argsBuffer = _argsBuffer;
            instanceCount = _matrixCount;
            generationSequence = _generationSequence;
            return _generated && _matrixBuffer != null && _argsBuffer != null && _matrixCount > 0;
        }

        public void ApplyAupShift(float3 shiftMeters, uint shiftFrameId)
        {
            if (!math.all(math.isfinite(shiftMeters)))
            {
                WriteTelemetry(MarauderOutpostConstants.FaultFlag | MarauderOutpostConstants.AupShiftFlag);
                DumpBlackBox();
                return;
            }

            if (math.all(math.abs(shiftMeters) < new float3(ShiftEpsilonMeters)))
                return;

            if (_jobPhase == JobPhase.Solving)
            {
                _generationOrigin -= shiftMeters;
                _lastShiftFrameId = shiftFrameId;
                WriteTelemetry(MarauderOutpostConstants.AupShiftFlag);
                UpdateSnapshot();
                return;
            }

            if (_jobPhase != JobPhase.None)
            {
                AccumulatePendingShift(shiftMeters, shiftFrameId);
                return;
            }

            _generationOrigin -= shiftMeters;
            _lastShiftFrameId = shiftFrameId;
            ShiftInteractableProxies(shiftMeters);
            if (!_generated || _matrixCount <= 0 || !_shellMatrices.IsCreated)
            {
                UpdateSnapshot();
                return;
            }

            MarauderOutpostAupShiftJob job = new MarauderOutpostAupShiftJob
            {
                ShellMatrices = _shellMatrices,
                ShiftMeters = shiftMeters
            };
            _jobHandle = job.Schedule(_matrixCount, 64);
            _jobPhase = JobPhase.Shifting;
            _drawBounds.center -= new Vector3(shiftMeters.x, shiftMeters.y, shiftMeters.z);
            WriteTelemetry(MarauderOutpostConstants.AupShiftFlag);
            UpdateSnapshot();
        }

        private void DrainSectorHydratedSignals()
        {
            if (_jobPhase != JobPhase.None || (_generated && _publishedPowerGridHandle != 0u))
                return;

            ReadOnlySpan<Hecton8.Core.Signals.SectorHydratedSignal> signals =
                SignalBus<Hecton8.Core.Signals.SectorHydratedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                Hecton8.Core.Signals.SectorHydratedSignal signal = signals[i];
                if (!generateOnAnyHydratedSectorForDebug && signal.SectorHash != firstBaseHash)
                    continue;

                TryRequestGeneration(signal.SectorHash, ResolveGenerationOriginMeters(), ResolveWorldSeed());
                return;
            }
        }

        private void DrainAupShiftSignals()
        {
            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
                ApplyAupShift(shifts[i].ShiftMeters, shifts[i].ShiftFrameId);
        }

        private void ScheduleMatrixExtraction()
        {
            MapMagicBridge.QuantizedHeightmapPayload payload = ResolveHeightmapPayload();
            MarauderOutpostMatrixExtractionJob job = new MarauderOutpostMatrixExtractionJob
            {
                WfcGrid = WfcGrid,
                MutableGrid = _wfcMutableStateGrid,
                HeightSamples = payload.IsValid ? payload.HeightSamples : default,
                ShellMatrices = _shellMatrices,
                CellTypes = _shellCellTypes,
                InteractableSpawns = _interactableSpawns,
                Counters = _counters,
                Dimensions = _activeDimensions,
                OriginMeters = _generationOrigin,
                TerrainPosition = payload.IsValid ? ToFloat3(payload.TerrainPosition) : _generationOrigin - new float3(16f, ResolveStiltClearanceMeters(), 16f),
                TerrainSize = payload.IsValid ? ToFloat3(payload.TerrainSize) : new float3(32f, 8f, 32f),
                HeightResolution = payload.IsValid ? payload.HeightmapResolution : 0,
                CellSizeMeters = ResolveCellSizeMeters(),
                FloorHeightMeters = ResolveFloorHeightMeters(),
                StiltClearanceMeters = ResolveStiltClearanceMeters(),
                OutpostAge01 = ResolveOutpostAge01(),
                Seed = _activeSolveSeed,
                LowTier = _qualityTier == OutpostGenerationQualityTier.Low ? (byte)1 : (byte)0
            };

            _jobHandle = job.Schedule();
            _jobPhase = JobPhase.Extracting;
            SetState(OutpostGenerationState.ExtractingMatrices);
        }

        private void CommitCompletedGeneration()
        {
            _jobPhase = JobPhase.None;
            _matrixCount = _counters.IsCreated && _counters.Length > 0 ? math.clamp(_counters[0], 0, MarauderOutpostConstants.MaxShellMatrices) : 0;
            _interactableCount = _counters.IsCreated && _counters.Length > 1 ? math.clamp(_counters[1], 0, MarauderOutpostConstants.MaxInteractables) : 0;
            _solidCellCount = _counters.IsCreated && _counters.Length > 2 ? math.max(0, _counters[2]) : 0;
            _supportCount = _counters.IsCreated && _counters.Length > 3 ? math.max(0, _counters[3]) : 0;
            _heightmapFallback = _counters.IsCreated && _counters.Length > 4 && _counters[4] != 0;
            ApplyPendingShiftToExtractedData(_matrixCount, _interactableCount);
            _generated = _matrixCount > 0;
            _activeGridHash = _generated ? ComputeGridHash() : 0u;
            _matrixUploadDirty = _generated;
            UpdateDrawBounds();
            UploadMatricesAndArgs();
            SpawnInteractableProxies();

            if (!_generated || !math.all(math.isfinite(_generationOrigin)))
            {
                SetState(OutpostGenerationState.Faulted);
                WriteTelemetry(MarauderOutpostConstants.FaultFlag);
                DumpBlackBox();
                return;
            }

            bool published = TryPublishGeneratedSignal();
            SetState(published ? OutpostGenerationState.Ready : OutpostGenerationState.Faulted);
            WriteTelemetry((_heightmapFallback ? MarauderOutpostConstants.HeightmapFallbackFlag : 0u) |
                           (published ? 0u : MarauderOutpostConstants.FaultFlag));
        }

        private MapMagicBridge.QuantizedHeightmapPayload ResolveHeightmapPayload()
        {
            MapMagicBridge bridge = GlobalRegistry.MapMagic;
            if (bridge == null)
                return default;

            Vector3 origin = new Vector3(_generationOrigin.x, _generationOrigin.y, _generationOrigin.z);
            if (bridge.TryGetQuantizedHeightmapPayloadAUP(origin, out MapMagicBridge.QuantizedHeightmapPayload payload) && payload.IsValid)
                return payload;

            if (bridge.TryGetActiveQuantizedHeightmapPayload(out payload) && payload.IsValid)
                return payload;

            return default;
        }

        private uint ResolveWorldSeed()
        {
            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;
            if (seedProvider != null && seedProvider.IsInitialized)
                return unchecked((uint)seedProvider.RuntimeWorldSeed);

            return fallbackWorldSeed;
        }

        private float3 ResolveGenerationOriginMeters()
        {
            Transform anchor = outpostOriginOverride != null ? outpostOriginOverride : transform;
            Vector3 position = anchor != null ? anchor.position : Vector3.zero;
            if (!IsFinite(position))
                position = Vector3.zero;
            if (IsFinite(localOriginOffsetMeters))
                position += localOriginOffsetMeters;
            return ToFloat3(position);
        }

        private OutpostGenerationQualityTier ResolveQualityTier()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350 || tier == HectonQualityTier.Unknown)
                return OutpostGenerationQualityTier.Low;
            if (tier == HectonQualityTier.High)
                return OutpostGenerationQualityTier.High;
            if (tier == HectonQualityTier.Ultra)
                return OutpostGenerationQualityTier.Ultra;
            return OutpostGenerationQualityTier.Middle;
        }

        private float ResolveCellSizeMeters()
        {
            return SanitizeMin(cellSizeMeters, 1f, DefaultCellSizeMeters);
        }

        private float ResolveFloorHeightMeters()
        {
            return SanitizeMin(floorHeightMeters, 1f, DefaultFloorHeightMeters);
        }

        private float ResolveStiltClearanceMeters()
        {
            return SanitizeMin(stiltClearanceMeters, 0.25f, DefaultStiltClearanceMeters);
        }

        private float ResolveOutpostAge01()
        {
            return Sanitize01(outpostAge01, DefaultOutpostAge01);
        }

        private void AllocatePersistentState()
        {
            if (WfcGrid.IsCreated)
                return;

            WfcGrid = new NativeArray<byte>(MarauderOutpostConstants.FullCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[10x10x5] - deterministic WFC cell grid - owner: MARAUDER_OUTPOST_ARCHITECT
            _wfcMutableStateGrid = new NativeArray<byte>(MarauderOutpostConstants.FullCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[10x10x5] - restored WFC mutable-state grid - owner: MARAUDER_OUTPOST_ARCHITECT
            _shellMatrices = new NativeArray<float4x4>(MarauderOutpostConstants.MaxShellMatrices, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4x4>[1024] - outpost shell indirect matrices - owner: MARAUDER_OUTPOST_ARCHITECT
            _shellCellTypes = new NativeArray<uint>(MarauderOutpostConstants.MaxShellMatrices, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[1024] - packed shell cell type metadata - owner: MARAUDER_OUTPOST_ARCHITECT
            _interactableSpawns = new NativeArray<OutpostInteractableSpawn>(MarauderOutpostConstants.MaxInteractables, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<OutpostInteractableSpawn>[16] - bounded door/datapad spawn packets - owner: MARAUDER_OUTPOST_ARCHITECT
            _counters = new NativeArray<int>(MarauderOutpostConstants.CounterCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[8] - WFC extraction counters - owner: MARAUDER_OUTPOST_ARCHITECT
            _telemetryRing = new NativeArray<OutpostTelemetryEntry>(MarauderOutpostConstants.TelemetryFrames, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<OutpostTelemetryEntry>[300] - blackbox ring - owner: MARAUDER_OUTPOST_ARCHITECT
            _spawnedInteractables = new GameObject[MarauderOutpostConstants.MaxInteractables]; // COLD ALLOC: GameObject[16] - spawned interactable proxy handles - owner: MARAUDER_OUTPOST_ARCHITECT
            _spawnedDoorControllers = new SealedDoor[MarauderOutpostConstants.MaxInteractables]; // COLD ALLOC: SealedDoor[16] - cached WFC door controllers for power unlocks - owner: MARAUDER_OUTPOST_ARCHITECT

            RegisterNativeArray(WfcGrid, nameof(WfcGrid));
            RegisterNativeArray(_wfcMutableStateGrid, nameof(_wfcMutableStateGrid));
            RegisterNativeArray(_shellMatrices, nameof(_shellMatrices));
            RegisterNativeArray(_shellCellTypes, nameof(_shellCellTypes));
            RegisterNativeArray(_interactableSpawns, nameof(_interactableSpawns));
            RegisterNativeArray(_counters, nameof(_counters));
            RegisterNativeArray(_telemetryRing, nameof(_telemetryRing));
        }

        private void RestoreWfcMutableState(ulong sectorHash)
        {
            if (!_wfcMutableStateGrid.IsCreated)
                return;

            for (int i = 0; i < _wfcMutableStateGrid.Length; i++)
                _wfcMutableStateGrid[i] = 0;

            if (sectorHash == 0UL)
                return;

            IAsyncPersistenceService persistence = GlobalRegistry.AsyncPersistence;
            if (persistence == null)
                return;

            persistence.TryApplyWfcOutpostStateOverride(sectorHash, _wfcMutableStateGrid, out _);
        }

        private void EnsureGraphicsResources()
        {
            if (_matrixBuffer == null)
                _matrixBuffer = CreateStructuredLockBuffer<float4x4>(MarauderOutpostConstants.MaxShellMatrices); // COLD ALLOC: GraphicsBuffer[1024 float4x4] - outpost shell matrices - owner: MARAUDER_OUTPOST_ARCHITECT
            if (_cellTypeBuffer == null)
                _cellTypeBuffer = CreateStructuredLockBuffer<uint>(MarauderOutpostConstants.MaxShellMatrices); // COLD ALLOC: GraphicsBuffer[1024 uint] - outpost shell types - owner: MARAUDER_OUTPOST_ARCHITECT
            if (_argsBuffer == null)
                _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - outpost indirect draw args - owner: MARAUDER_OUTPOST_ARCHITECT

            if (shellMesh == null && _runtimeShellMesh == null)
                _runtimeShellMesh = CreateCubeMesh();

            if (shellMaterial == null && _runtimeShellMaterial == null)
            {
                Shader shader = Shader.Find("Hecton8/Environment/MarauderOutpostIndirect");
                if (shader != null)
                {
                    // COLD ALLOC: Material[1] - fallback indirect shell material when no asset is assigned - owner: MARAUDER_OUTPOST_ARCHITECT
                    _runtimeShellMaterial = new Material(shader)
                    {
                        name = "MarauderOutpostRuntime",
                        hideFlags = HideFlags.DontSave
                    };
                }
            }

            UpdateIndirectArgsBuffer(0u);
        }

        private void UploadMatricesAndArgs()
        {
            if (!_shellMatrices.IsCreated || _matrixBuffer == null || _cellTypeBuffer == null)
                return;

            int count = math.clamp(_matrixCount, 0, MarauderOutpostConstants.MaxShellMatrices);
            if (count > 0)
            {
                UploadNativeArray(_matrixBuffer, _shellMatrices, count);
                UploadNativeArray(_cellTypeBuffer, _shellCellTypes, count);
            }

            UpdateIndirectArgsBuffer((uint)count);
            _matrixUploadDirty = false;
        }

        private void UpdateIndirectArgsBuffer(uint instanceCount)
        {
            if (_argsBuffer == null)
                return;

            Mesh mesh = ResolveRenderMesh();
            uint indexCount = 0u;
            uint startIndex = 0u;
            uint baseVertexIndex = 0u;
            uint safeInstanceCount = 0u;
            if (mesh != null && mesh.subMeshCount > 0)
            {
                indexCount = mesh.GetIndexCount(0);
                startIndex = mesh.GetIndexStart(0);
                baseVertexIndex = (uint)math.max(0, mesh.GetBaseVertex(0));
                safeInstanceCount = indexCount > 0u ? instanceCount : 0u;
            }

            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _argsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = indexCount,
                instanceCount = safeInstanceCount,
                startIndex = startIndex,
                baseVertexIndex = baseVertexIndex,
                startInstance = 0u
            };
            _argsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
        }

        private void SpawnInteractableProxies()
        {
            if (_interactableCount <= 0 || _spawnedInteractables == null)
                return;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null)
                return;

            for (int i = 0; i < _interactableCount; i++)
            {
                OutpostInteractableSpawn spawn = _interactableSpawns[i];
                GameObject prefab = spawn.Kind == MarauderOutpostConstants.SealedDoor ? sealedDoorProxyPrefab : datapadProxyPrefab;
                if (prefab == null)
                    continue;

                Vector3 position = new Vector3(spawn.PositionMeters.x, spawn.PositionMeters.y, spawn.PositionMeters.z);
                Quaternion rotation = Quaternion.Euler(0f, spawn.RotationYRadians * Mathf.Rad2Deg, 0f);
                _spawnedInteractables[i] = pool.Spawn(prefab, position, rotation);
                GameObject instance = _spawnedInteractables[i];
                if (instance == null)
                    continue;

                if (spawn.Kind == MarauderOutpostConstants.SealedDoor &&
                    TryResolveOwnedComponent(instance, out SealedDoor door))
                {
                    door.ConfigureWfcOutpostPersistence(_activeSectorHash, spawn.CellIndex, spawn.Flags);
                    if (_spawnedDoorControllers != null && i < _spawnedDoorControllers.Length)
                        _spawnedDoorControllers[i] = door;
                }
                else if (spawn.Kind == MarauderOutpostConstants.Datapad)
                {
                    ConfigureDatapadPersistence(instance, spawn.CellIndex, spawn.Flags);
                }
            }
        }

        private void ProcessDoorPowerSignals()
        {
            if (!_generated || _publishedPowerGridHandle == 0u || _spawnedDoorControllers == null || _interactableCount <= 0)
                return;

            ReadOnlySpan<WfcOutpostDoorPowerSignal> signals = SignalBus<WfcOutpostDoorPowerSignal>.GetFrameSnapshot();
            for (int signalIndex = 0; signalIndex < signals.Length; signalIndex++)
            {
                WfcOutpostDoorPowerSignal signal = signals[signalIndex];
                if (signal.SectorHash != _activeSectorHash || signal.GridHandle != _publishedPowerGridHandle)
                {
                    continue;
                }

                for (int i = 0; i < _interactableCount && i < _spawnedDoorControllers.Length; i++)
                {
                    if (_interactableSpawns[i].CellIndex != signal.CellIndex)
                        continue;

                    SealedDoor door = _spawnedDoorControllers[i];
                    if (door == null)
                        continue;

                    door.ApplyWfcOutpostPowerState(signal.Unlocked != 0, signal.Frame);
                    break;
                }
            }
        }

        private void ConfigureDatapadPersistence(GameObject instance, ushort cellIndex, byte flags)
        {
            if (instance == null)
                return;

            if (TryResolveOwnedComponent(instance, out MessageTerminal terminal))
            {
                terminal.ConfigureWfcOutpostPersistence(_activeSectorHash, cellIndex, flags);
                return;
            }

            if (TryResolveOwnedComponent(instance, out AudioLogPickup audioLogPickup))
                audioLogPickup.ConfigureWfcOutpostPersistence(_activeSectorHash, cellIndex, flags);
        }

        private static void ClearInteractablePersistence(GameObject instance)
        {
            if (instance == null)
                return;

            if (TryResolveOwnedComponent(instance, out SealedDoor door))
                door.ClearWfcOutpostPersistence();

            if (TryResolveOwnedComponent(instance, out MessageTerminal terminal))
                terminal.ClearWfcOutpostPersistence();

            if (TryResolveOwnedComponent(instance, out AudioLogPickup audioLogPickup))
                audioLogPickup.ClearWfcOutpostPersistence();
        }

        private static bool TryResolveOwnedComponent<T>(GameObject rootObject, out T component) where T : Component
        {
            component = null;
            if (rootObject == null)
                return false;

            return TryResolveOwnedComponent(rootObject.transform, out component);
        }

        private static bool TryResolveOwnedComponent<T>(Transform root, out T component) where T : Component
        {
            component = null;
            if (root == null)
                return false;

            if (root.TryGetComponent(out component))
                return true;

            for (int i = 0; i < root.childCount; i++)
            {
                if (TryResolveOwnedComponent(root.GetChild(i), out component))
                    return true;
            }

            return false;
        }

        private void BakeInteractableProxyMeshes()
        {
            BakeProxyMesh(sealedDoorProxyPrefab);
            BakeProxyMesh(datapadProxyPrefab);
        }

        private static void BakeProxyMesh(GameObject prefab)
        {
            if (prefab == null || !prefab.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                return;

            UnityEngine.Physics.BakeMesh(filter.sharedMesh.GetEntityId(), false);
        }

        private void DespawnInteractables()
        {
            if (_spawnedInteractables == null)
                return;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            for (int i = 0; i < _spawnedInteractables.Length; i++)
            {
                GameObject instance = _spawnedInteractables[i];
                if (instance != null)
                {
                    ClearInteractablePersistence(instance);
                    if (pool != null)
                        pool.Despawn(instance);
                    else
                        instance.SetActive(false);
                }

                _spawnedInteractables[i] = null;
                if (_spawnedDoorControllers != null && i < _spawnedDoorControllers.Length)
                    _spawnedDoorControllers[i] = null;
            }
        }

        private void ShiftInteractableProxies(float3 shiftMeters)
        {
            if (_spawnedInteractables == null)
                return;

            Vector3 shift = new Vector3(shiftMeters.x, shiftMeters.y, shiftMeters.z);
            for (int i = 0; i < _spawnedInteractables.Length; i++)
            {
                GameObject instance = _spawnedInteractables[i];
                if (instance != null)
                    instance.transform.position -= shift;
            }
        }

        private void AccumulatePendingShift(float3 shiftMeters, uint shiftFrameId)
        {
            if (math.any(math.abs(shiftMeters) > new float3(ShiftEpsilonMeters)))
                _pendingShift += shiftMeters;

            _pendingShiftFrameId = shiftFrameId;
            _hasPendingShift = math.any(math.abs(_pendingShift) > new float3(ShiftEpsilonMeters));
        }

        private void ApplyPendingShiftToExtractedData(int matrixCount, int interactableCount)
        {
            if (!_hasPendingShift)
                return;

            if (!math.all(math.isfinite(_pendingShift)))
            {
                _pendingShift = default;
                _pendingShiftFrameId = 0u;
                _hasPendingShift = false;
                WriteTelemetry(MarauderOutpostConstants.FaultFlag | MarauderOutpostConstants.AupShiftFlag);
                DumpBlackBox();
                return;
            }

            float3 shift = _pendingShift;
            uint shiftFrameId = _pendingShiftFrameId;
            _pendingShift = default;
            _pendingShiftFrameId = 0u;
            _hasPendingShift = false;
            if (math.all(math.abs(shift) < new float3(ShiftEpsilonMeters)))
                return;

            _generationOrigin -= shift;
            _lastShiftFrameId = shiftFrameId;

            int safeMatrixCount = _shellMatrices.IsCreated ? math.min(math.max(0, matrixCount), _shellMatrices.Length) : 0;
            for (int i = 0; i < safeMatrixCount; i++)
            {
                float4x4 matrix = _shellMatrices[i];
                matrix.c3.x -= shift.x;
                matrix.c3.y -= shift.y;
                matrix.c3.z -= shift.z;
                _shellMatrices[i] = matrix;
            }

            int safeInteractableCount = _interactableSpawns.IsCreated ? math.min(math.max(0, interactableCount), _interactableSpawns.Length) : 0;
            for (int i = 0; i < safeInteractableCount; i++)
            {
                OutpostInteractableSpawn spawn = _interactableSpawns[i];
                spawn.PositionMeters -= shift;
                _interactableSpawns[i] = spawn;
            }

            WriteTelemetry(MarauderOutpostConstants.AupShiftFlag);
        }

        private void UpdateDrawBounds()
        {
            float width = math.max(_activeDimensions.x, _activeDimensions.z) * ResolveCellSizeMeters() + 12f;
            float height = math.max(4f, _activeDimensions.y * ResolveFloorHeightMeters() + 12f);
            _drawBounds = new Bounds(
                new Vector3(_generationOrigin.x, _generationOrigin.y + height * 0.35f, _generationOrigin.z),
                new Vector3(width, height, width));
        }

        private void SetState(OutpostGenerationState state)
        {
            _state = state;
            UpdateSnapshot();
        }

        private void UpdateSnapshot()
        {
            _latestSnapshot = new OutpostGenerationSnapshot
            {
                SectorHash = _activeSectorHash,
                WorldSeed = _activeWorldSeed,
                GenerationSequence = _generationSequence,
                OriginMeters = _generationOrigin,
                Dimensions = _activeDimensions,
                ShellMatrixCount = _matrixCount,
                InteractableCount = _interactableCount,
                OutpostAge01 = ResolveOutpostAge01(),
                QualityTier = _qualityTier,
                State = _state,
                Flags = ResolveDescriptorFlags()
            };
        }

        private ushort ResolveDescriptorFlags()
        {
            return (ushort)((_heightmapFallback ? MarauderOutpostConstants.HeightmapFallbackFlag : 0u) |
                            (_qualityTier == OutpostGenerationQualityTier.Low ? MarauderOutpostConstants.LowTierFlag : 0u));
        }

        private bool TryPublishGeneratedSignal()
        {
            if (!_generated)
                return false;

            if (_publishedPowerGridHandle != 0u)
            {
                if (WfcOutpostGridRegistry.TryGetGrid(_publishedPowerGridHandle, out _))
                {
                    PublishGeneratedSignalForHandle();
                    _generatedSignalReplayFrames = GeneratedSignalReplayFrames;
                    _generatedSignalHeartbeatFrames = GeneratedSignalHeartbeatFrames;
                    return true;
                }

                _publishedPowerGridHandle = 0u;
            }

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(new Vector3(_generationOrigin.x, _generationOrigin.y, _generationOrigin.z));
            WfcOutpostGridDescriptor descriptor = new WfcOutpostGridDescriptor
            {
                OriginAup = new MacroDatabaseAup
                {
                    GridX = originAup.GridX,
                    GridY = originAup.GridY,
                    GridZ = originAup.GridZ,
                    LocalX = originAup.LocalX,
                    LocalY = originAup.LocalY,
                    LocalZ = originAup.LocalZ
                },
                Dimensions = _activeDimensions,
                CellSizeMeters = ResolveCellSizeMeters(),
                FloorHeightMeters = ResolveFloorHeightMeters(),
                SectorHash = _activeSectorHash,
                WorldSeed = _activeWorldSeed,
                GenerationSequence = _generationSequence,
                GridHash = _activeGridHash,
                CellCount = (ushort)math.min(ResolveActiveCellCount(), ushort.MaxValue),
                Flags = ResolveDescriptorFlags()
            };

            if (!WfcOutpostGridRegistry.RegisterGrid(in descriptor, WfcGrid, out _publishedPowerGridHandle))
            {
                _publishedPowerGridHandle = 0u;
                _generatedSignalReplayFrames = 0;
                _generatedSignalHeartbeatFrames = GeneratedSignalHeartbeatFrames;
                WriteTelemetry(MarauderOutpostConstants.FaultFlag);
                DumpBlackBox();
                return false;
            }

            PublishGeneratedSignalForHandle();
            _generatedSignalReplayFrames = GeneratedSignalReplayFrames;
            _generatedSignalHeartbeatFrames = GeneratedSignalHeartbeatFrames;
            return true;
        }

        private void ReplayGeneratedSignalIfNeeded()
        {
            if (!_generated)
                return;

            if (_generatedSignalReplayFrames <= 0)
            {
                if (_generatedSignalHeartbeatFrames > 0)
                {
                    _generatedSignalHeartbeatFrames--;
                    return;
                }
            }

            if (_publishedPowerGridHandle == 0u)
            {
                TryPublishGeneratedSignal();
                return;
            }

            if (!WfcOutpostGridRegistry.TryGetGrid(_publishedPowerGridHandle, out _))
            {
                _publishedPowerGridHandle = 0u;
                TryPublishGeneratedSignal();
                return;
            }

            PublishGeneratedSignalForHandle();
            if (_generatedSignalReplayFrames > 0)
                _generatedSignalReplayFrames--;
            else
                _generatedSignalHeartbeatFrames = GeneratedSignalHeartbeatFrames;
        }

        private void PublishGeneratedSignalForHandle()
        {
            WfcOutpostGeneratedSignal signal = new WfcOutpostGeneratedSignal
            {
                OriginAup = AbsoluteUniversePosition.FromRuntimePosition(new Vector3(_generationOrigin.x, _generationOrigin.y, _generationOrigin.z)),
                SectorHash = _activeSectorHash,
                GridHandle = _publishedPowerGridHandle,
                GenerationSequence = _generationSequence,
                Dimensions = _activeDimensions,
                CellSizeMeters = ResolveCellSizeMeters(),
                FloorHeightMeters = ResolveFloorHeightMeters(),
                GridHash = _activeGridHash,
                Frame = (uint)Time.frameCount,
                CellCount = (ushort)math.min(ResolveActiveCellCount(), ushort.MaxValue),
                Flags = ResolveDescriptorFlags()
            };
            GlobalSignals.InitializeAllQueues();
            SignalBus<WfcOutpostGeneratedSignal>.Push(in signal);
        }

        private void ReleasePublishedPowerGrid()
        {
            if (_publishedPowerGridHandle == 0u)
            {
                _generatedSignalReplayFrames = 0;
                _generatedSignalHeartbeatFrames = 0;
                return;
            }

            WfcOutpostGridRegistry.ReleaseGrid(_publishedPowerGridHandle);
            _publishedPowerGridHandle = 0u;
            _generatedSignalReplayFrames = 0;
            _generatedSignalHeartbeatFrames = 0;
        }

        private int ResolveActiveCellCount()
        {
            return math.max(0, _activeDimensions.x) *
                   math.max(0, _activeDimensions.y) *
                   math.max(0, _activeDimensions.z);
        }

        private uint ComputeGridHash()
        {
            if (!WfcGrid.IsCreated)
                return 0u;

            int cellCount = math.min(ResolveActiveCellCount(), WfcGrid.Length);
            uint hash = 2166136261u ^ _activeSolveSeed;
            for (int i = 0; i < cellCount; i++)
            {
                hash ^= WfcGrid[i];
                hash *= 16777619u;
            }

            hash ^= (uint)_activeDimensions.x * 0x9E3779B9u;
            hash ^= (uint)_activeDimensions.y * 0x85EBCA6Bu;
            hash ^= (uint)_activeDimensions.z * 0xC2B2AE35u;
            return hash == 0u ? 1u : hash;
        }

        private void WriteTelemetry(uint flags)
        {
            if (!_telemetryRing.IsCreated || _telemetryRing.Length == 0)
                return;

            int length = _telemetryRing.Length;
            int index = _telemetryWriteIndex;
            if ((uint)index >= (uint)length)
                index = 0;

            _telemetryRing[index] = new OutpostTelemetryEntry
            {
                Frame = (uint)Time.frameCount,
                Flags = flags,
                SectorHash = _activeSectorHash,
                Seed = _activeSolveSeed,
                GenerationSequence = _generationSequence,
                OriginMeters = _generationOrigin,
                Dimensions = _activeDimensions,
                MatrixCount = _matrixCount,
                InteractableCount = _interactableCount,
                SolidCellCount = _solidCellCount,
                SupportCount = _supportCount,
                OutpostAge01 = ResolveOutpostAge01(),
                ShiftFrameId = _lastShiftFrameId
            };
            index++;
            _telemetryWriteIndex = index >= length ? 0 : index;
        }

        private void DumpBlackBox()
        {
            if (!_telemetryRing.IsCreated)
                return;

            try
            {
                string path = Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_MARAUDER_OUTPOST_ARCHITECT.bin");
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                int length = _telemetryRing.Length;
                int startIndex = _telemetryWriteIndex;
                if ((uint)startIndex >= (uint)length)
                    startIndex = 0;

                writer.Write(TelemetryDumpMagic);
                writer.Write(TelemetryDumpVersion);
                writer.Write(length);
                writer.Write(TelemetryDumpEntryPayloadBytes);
                writer.Write(startIndex);
                for (int offset = 0; offset < length; offset++)
                {
                    int sourceIndex = startIndex + offset;
                    if (sourceIndex >= length)
                        sourceIndex -= length;

                    OutpostTelemetryEntry entry = _telemetryRing[sourceIndex];
                    writer.Write(entry.Frame);
                    writer.Write(entry.Flags);
                    writer.Write(entry.SectorHash);
                    writer.Write(entry.Seed);
                    writer.Write(entry.GenerationSequence);
                    writer.Write(entry.OriginMeters.x);
                    writer.Write(entry.OriginMeters.y);
                    writer.Write(entry.OriginMeters.z);
                    writer.Write(entry.Dimensions.x);
                    writer.Write(entry.Dimensions.y);
                    writer.Write(entry.Dimensions.z);
                    writer.Write(entry.MatrixCount);
                    writer.Write(entry.InteractableCount);
                    writer.Write(entry.SolidCellCount);
                    writer.Write(entry.SupportCount);
                    writer.Write(entry.OutpostAge01);
                    writer.Write(entry.ShiftFrameId);
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[MARAUDER_OUTPOST_ARCHITECT] Failed to dump outpost blackbox: " + exception.Message, this);
#endif
            }
        }

        private Material ResolveRenderMaterial()
        {
            return shellMaterial != null ? shellMaterial : _runtimeShellMaterial;
        }

        private Mesh ResolveRenderMesh()
        {
            return shellMesh != null ? shellMesh : _runtimeShellMesh;
        }

        private static Mesh CreateCubeMesh()
        {
            // COLD ALLOC: Mesh[1] - fallback unit cube shell mesh when no authored mesh is assigned - owner: MARAUDER_OUTPOST_ARCHITECT
            Mesh mesh = new Mesh
            {
                name = "MarauderOutpostUnitCube",
                hideFlags = HideFlags.DontSave
            };

            // COLD ALLOC: Vector3[8] - fallback cube vertices - owner: MARAUDER_OUTPOST_ARCHITECT
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
            };
            // COLD ALLOC: int[36] - fallback cube triangle indices - owner: MARAUDER_OUTPOST_ARCHITECT
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                2, 3, 7, 2, 7, 6,
                1, 2, 6, 1, 6, 5,
                3, 0, 4, 3, 4, 7
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static float SanitizeMin(float value, float minValue, float fallback)
        {
            float resolvedFallback = IsFinite(fallback) ? fallback : minValue;
            if (!IsFinite(value))
                return math.max(minValue, resolvedFallback);
            return value < minValue ? minValue : value;
        }

        private static float Sanitize01(float value, float fallback)
        {
            if (!IsFinite(value))
                value = fallback;
            if (!IsFinite(value))
                return 0f;
            return Mathf.Clamp01(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        }

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, OwnerName, label, NativeAllocationLifetime.Scene);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, ref JobHandle dependency, bool defer) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (defer)
                dependency = array.Dispose(dependency);
            else
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

        private static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        private static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : struct
        {
            if (destination == null || !source.IsCreated)
                return;

            int safeCount = math.min(math.min(destination.count, source.Length), math.max(0, count));
            if (safeCount <= 0)
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            NativeArray<T>.Copy(source, mapped, safeCount);
            destination.UnlockBufferAfterWrite<T>(safeCount);
        }
    }
}
