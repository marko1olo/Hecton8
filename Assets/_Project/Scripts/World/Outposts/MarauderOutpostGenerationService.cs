using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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
    public sealed class MarauderOutpostGenerationService : MonoBehaviour, IUpdatable, ILateFrameTickable, IRenderable, IOutpostGenerationService, IGlobalRegistryHotSwapListener
    {
        private static int s_x001MarauderOutpostGenerationServiceSignalPushDropCount;
        private const ulong DefaultFirstBaseHash = 0x4D41524155444552UL; // MARAUDER
        private const uint DefaultWorldSeed = 0x48454338u; // HEC8
        private const float DefaultCellSizeMeters = 4f;
        private const float DefaultFloorHeightMeters = 3f;
        private const float DefaultStiltClearanceMeters = 1.6f;
        private const float DefaultOutpostAge01 = 0.92f;
        private const int GeneratedSignalReplayFrames = 4;
        private const int GeneratedSignalHeartbeatFrames = 60;
        private const string TelemetryDumpPath = "Docs/AgentLogs/Dump_MARAUDER_OUTPOST_ARCHITECT.bin";
        private const ulong TelemetryDumpMagic = 0x00384E4F54434548UL; // HECTON8\0 as little-endian bytes.
        private const uint TelemetryDumpVersion = 1u;
        private const int TelemetryDumpHeaderBytes = 24;
        private const int TelemetryDumpEntryPayloadBytes = 72;
        private const string TelemetryDumpPayloadLabel = "marauderOutpostTelemetryDumpPayload";
        private const float ShiftEpsilonMeters = 0.0001f;
        private const float MaxAupShiftMeters = 10000f;
        private const SystemID VaultOwnerSystemId = SystemID.WorldOutposts;
        private const BufferID WfcGridBufferId = BufferID.MarauderOutpostWfcGrid;
        private const BufferID ShellMatricesBufferId = BufferID.MarauderOutpostShellMatrices;
        private const BufferID ShellCellTypesBufferId = BufferID.MarauderOutpostShellCellTypes;
        private const BufferID InteractableSpawnsBufferId = BufferID.MarauderOutpostInteractableSpawns;
        private const BufferID WfcMutableStateGridBufferId = BufferID.MarauderOutpostMutableStateGrid;
        private const BufferID CountersBufferId = BufferID.MarauderOutpostCounters;
        private const BufferID TelemetryRingBufferId = BufferID.MarauderOutpostTelemetryRing;

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

        private struct OutpostScratchBuffers : IDisposable
        {
            public NativeArray<byte> SolveWfcGrid;
            public NativeArray<byte> ExtractionMutableGrid;
            public NativeArray<float4x4> ExtractionShellMatrices;
            public NativeArray<uint> ExtractionShellCellTypes;
            public NativeArray<OutpostInteractableSpawn> ExtractionInteractableSpawns;
            public NativeArray<int> ExtractionCounters;
            public NativeArray<float4x4> ShiftShellMatrices;

            public void Dispose()
            {
                DisposeNativeScratch(ref ShiftShellMatrices);
                DisposeNativeScratch(ref ExtractionCounters);
                DisposeNativeScratch(ref ExtractionInteractableSpawns);
                DisposeNativeScratch(ref ExtractionShellCellTypes);
                DisposeNativeScratch(ref ExtractionShellMatrices);
                DisposeNativeScratch(ref ExtractionMutableGrid);
                DisposeNativeScratch(ref SolveWfcGrid);
            }

            public void DisposeSolve()
            {
                DisposeNativeScratch(ref SolveWfcGrid);
            }

            public void DisposeExtraction()
            {
                DisposeNativeScratch(ref ExtractionCounters);
                DisposeNativeScratch(ref ExtractionInteractableSpawns);
                DisposeNativeScratch(ref ExtractionShellCellTypes);
                DisposeNativeScratch(ref ExtractionShellMatrices);
                DisposeNativeScratch(ref ExtractionMutableGrid);
            }

            public void DisposeShift()
            {
                DisposeNativeScratch(ref ShiftShellMatrices);
            }

            public bool EnsureSolve(int length, NativeArrayOptions options)
            {
                return EnsureNativeScratch(ref SolveWfcGrid, length, options);
            }

            public bool IsSolveReady(int length)
            {
                return SolveWfcGrid.IsCreated && SolveWfcGrid.Length == length;
            }

            public bool EnsureExtraction()
            {
                return EnsureNativeScratch(ref ExtractionMutableGrid, MarauderOutpostConstants.FullCellCount, NativeArrayOptions.ClearMemory) &&
                       EnsureNativeScratch(ref ExtractionShellMatrices, MarauderOutpostConstants.MaxShellMatrices, NativeArrayOptions.ClearMemory) &&
                       EnsureNativeScratch(ref ExtractionShellCellTypes, MarauderOutpostConstants.MaxShellMatrices, NativeArrayOptions.ClearMemory) &&
                       EnsureNativeScratch(ref ExtractionInteractableSpawns, MarauderOutpostConstants.MaxInteractables, NativeArrayOptions.ClearMemory) &&
                       EnsureNativeScratch(ref ExtractionCounters, MarauderOutpostConstants.CounterCount, NativeArrayOptions.ClearMemory);
            }

            public bool IsExtractionReady()
            {
                return ExtractionMutableGrid.IsCreated &&
                       ExtractionShellMatrices.IsCreated &&
                       ExtractionShellCellTypes.IsCreated &&
                       ExtractionInteractableSpawns.IsCreated &&
                       ExtractionCounters.IsCreated &&
                       ExtractionMutableGrid.Length == MarauderOutpostConstants.FullCellCount &&
                       ExtractionShellMatrices.Length == MarauderOutpostConstants.MaxShellMatrices &&
                       ExtractionShellCellTypes.Length == MarauderOutpostConstants.MaxShellMatrices &&
                       ExtractionInteractableSpawns.Length == MarauderOutpostConstants.MaxInteractables &&
                       ExtractionCounters.Length == MarauderOutpostConstants.CounterCount;
            }

            public bool EnsureShift(int length, NativeArrayOptions options)
            {
                return EnsureNativeScratch(ref ShiftShellMatrices, length, options);
            }

            public bool IsShiftReady(int length)
            {
                return ShiftShellMatrices.IsCreated && ShiftShellMatrices.Length == length;
            }

            public bool EnsureAll()
            {
                try
                {
                    if (EnsureSolve(MarauderOutpostConstants.FullCellCount, NativeArrayOptions.ClearMemory) &&
                        EnsureExtraction() &&
                        EnsureShift(MarauderOutpostConstants.MaxShellMatrices, NativeArrayOptions.ClearMemory))
                    {
                        return true;
                    }

                    Dispose();
                    return false;
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public bool IsReady()
            {
                return IsSolveReady(MarauderOutpostConstants.FullCellCount) &&
                       IsExtractionReady() &&
                       IsShiftReady(MarauderOutpostConstants.MaxShellMatrices);
            }

            private static bool EnsureNativeScratch<T>(
                ref NativeArray<T> scratch,
                int length,
                NativeArrayOptions options) where T : struct
            {
                if (scratch.IsCreated && scratch.Length == length)
                    return true;

                DisposeNativeScratch(ref scratch);
                try
                {
                    scratch = H8Memory.Allocate<T>(length, VaultOwnerSystemId, Allocator.Persistent, options);
                    return scratch.IsCreated;
                }
                catch
                {
                    DisposeNativeScratch(ref scratch);
                    throw;
                }
            }

            private static void DisposeNativeScratch<T>(ref NativeArray<T> scratch) where T : struct
            {
                if (!scratch.IsCreated)
                    return;

                H8Memory.Release(ref scratch, VaultOwnerSystemId);
            }
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

        private IDataVault _dataVault;
        private VaultGenerationHandle<byte> _wfcGridHandle;
        private VaultGenerationHandle<float4x4> _shellMatricesHandle;
        private VaultGenerationHandle<uint> _shellCellTypesHandle;
        private VaultGenerationHandle<OutpostInteractableSpawn> _interactableSpawnsHandle;
        private VaultGenerationHandle<byte> _wfcMutableStateGridHandle;
        private VaultGenerationHandle<int> _countersHandle;
        private VaultGenerationHandle<OutpostTelemetryEntry> _telemetryRingHandle;
        private IDataVault _wfcGridWriteVault;
        private IDataVault _shellMatricesWriteVault;
        private IDataVault _shellCellTypesWriteVault;
        private IDataVault _interactableSpawnsWriteVault;
        private IDataVault _wfcMutableStateWriteVault;
        private IDataVault _countersWriteVault;
        private IDataVault _telemetryRingWriteVault;
        private GraphicsBuffer _matrixBufferA;
        private GraphicsBuffer _matrixBufferB;
        private GraphicsBuffer _activeMatrixBuffer;
        private GraphicsBuffer _cellTypeBufferA;
        private GraphicsBuffer _cellTypeBufferB;
        private GraphicsBuffer _activeCellTypeBuffer;
        private GraphicsBuffer _argsBufferA;
        private GraphicsBuffer _argsBufferB;
        private GraphicsBuffer _activeArgsBuffer;
        private MaterialPropertyBlock _renderPropertyBlock;
        private GraphicsBuffer _renderPropertyMatrixBuffer;
        private GraphicsBuffer _renderPropertyCellTypeBuffer;
        private Vector4 _renderPropertyDecayRuntime;
        private float _renderPropertyAge01;
        private int _shellUploadBufferIndex;
        private GameObject[] _spawnedInteractables;
        private IObjectPoolService[] _spawnedInteractableOwners;
        private SealedDoor[] _spawnedDoorControllers;
        private RegistryBucket<IRenderable> _registeredRenderables;
        private MapMagicBridge _cachedMapMagicBridge;
        private IWorldSeedProvider _cachedWorldSeedProvider;
        private IAsyncPersistenceService _cachedPersistence;
        private IObjectPoolService _cachedObjectPool;
        private JobHandle _jobHandle;
        private Bounds _drawBounds;
        private OutpostGenerationSnapshot _latestSnapshot;
        private JobPhase _jobPhase;
        private OutpostGenerationQualityTier _compatibilityQualityTier;
        private OutpostGenerationState _state;
        private float _generationQualityWeight01 = 1f;
        private float3 _generationOrigin;
        private float3 _pendingShift;
        private float3 _pendingInteractableProxyShift;
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
        private int _registeredOutpostGeneration;
        private int _registeredUpdate;
        private int _registeredLateFrame;
        private int _registeredRenderable;
        private int _registeredHotSwapListener;
        private int _generatedSignalReplayFrames;
        private int _generatedSignalHeartbeatFrames;
        private bool _generated;
        private bool _matrixUploadDirty;
        private bool _hasPendingShift;
        private bool _interactableProxyShiftDirty;
        private bool _missingHeightmap;
        private bool _renderPropertiesDirty = true;
        private bool _authoredRenderResourceFaultLogged;
        private OutpostScratchBuffers _scratchBuffers;

        public bool IsGenerated => _generated;
        public bool IsBusy => _jobPhase != JobPhase.None;
        public ulong FirstBaseHash => ResolveFirstBaseHash();
        public OutpostGenerationSnapshot LatestSnapshot => _latestSnapshot;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            SignalCorridorRuntime.EnsureInitialized();
            EnsurePersistentState();
            EnsureGraphicsResources();
            BakeInteractableProxyMeshes();
            CacheRegistryDependenciesCold();
            _registeredRenderables = GlobalRegistry.Renderables;
            GlobalRegistry.RegisterOutpostGenerationService(this);
            _registeredOutpostGeneration = 1;
            TryRegisterHotSwapListener();
            TryRegisterUpdate();
            TryRegisterLateFrame();
            _registeredRenderable = _registeredRenderables != null && _registeredRenderables.TryRegister(this) ? 1 : 0;
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
            if (firstBaseHash == 0UL)
                firstBaseHash = DefaultFirstBaseHash;
            if (!IsFinite(localOriginOffsetMeters))
                localOriginOffsetMeters = Vector3.zero;
        }

        public void Dispose()
        {
            TryUnregisterHotSwapListener();

            if (_registeredRenderable != 0)
            {
                _registeredRenderables?.Unregister(this);
                _registeredRenderable = 0;
            }

            TryUnregisterLateFrame();
            TryUnregisterUpdate();

            if (_registeredOutpostGeneration != 0)
            {
                GlobalRegistry.UnregisterOutpostGenerationService(this);
                _registeredOutpostGeneration = 0;
            }

            ReleasePublishedPowerGrid();

            CompleteCurrentOutpostJobForTeardown();

            DespawnInteractables();
            ReleaseGraphicsBuffer(ref _matrixBufferA);
            ReleaseGraphicsBuffer(ref _matrixBufferB);
            ReleaseGraphicsBuffer(ref _cellTypeBufferA);
            ReleaseGraphicsBuffer(ref _cellTypeBufferB);
            ReleaseGraphicsBuffer(ref _argsBufferA);
            ReleaseGraphicsBuffer(ref _argsBufferB);
            _activeMatrixBuffer = null;
            _activeCellTypeBuffer = null;
            _activeArgsBuffer = null;
            _renderPropertyMatrixBuffer = null;
            _renderPropertyCellTypeBuffer = null;
            _shellUploadBufferIndex = 0;
            ReleaseVaultBuffers();
            _scratchBuffers.Dispose();

            _generated = false;
            _matrixCount = 0;
            _interactableCount = 0;
            _matrixUploadDirty = false;
            _hasPendingShift = false;
            _generatedSignalReplayFrames = 0;
            _generatedSignalHeartbeatFrames = 0;
            _state = OutpostGenerationState.Idle;
            _latestSnapshot = default;
            ClearRenderPropertyCache();
            ClearCachedRegistryDependencies();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterLateFrame();
                    TryUnregisterUpdate();
                    if (currentService == null || !isActiveAndEnabled)
                        return;

                    TryRegisterUpdate();
                    TryRegisterLateFrame();
                    break;
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                    _cachedMapMagicBridge = currentService as MapMagicBridge;
                    break;
                case GlobalRegistryServiceSlot.WorldSeedProvider:
                    _cachedWorldSeedProvider = currentService as IWorldSeedProvider;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    IAsyncPersistenceService persistence = currentService as IAsyncPersistenceService;
                    _cachedPersistence = IsAsyncPersistenceUsable(persistence) ? persistence : null;
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    OnDataVaultReplaced(currentService as IDataVault);
                    break;
            }
        }

        public void Tick(float deltaTime)
        {
            if (!IsExactVaultHandle(in _wfcGridHandle, WfcGridBufferId))
                return;

            DrainAupShiftSignals();
            DrainSectorHydratedSignals();
            ReplayGeneratedSignalIfNeeded();
            WriteTelemetry(0u);
        }

        public void LateFrameTick()
        {
            if (!AreGraphicsResourcesReady())
                return;

            if (_jobPhase == JobPhase.Solving)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _jobHandle))
                    return;

                if (!FlushSolveScratchToVault())
                {
                    _jobPhase = JobPhase.None;
                    SetState(OutpostGenerationState.Faulted);
                    WriteTelemetry(MarauderOutpostConstants.FaultFlag);
                    DumpBlackBox();
                    return;
                }

                ScheduleMatrixExtraction();
                return;
            }

            if (_jobPhase == JobPhase.Extracting)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _jobHandle))
                    return;

                if (!FlushExtractionScratchToVault())
                {
                    _jobPhase = JobPhase.None;
                    SetState(OutpostGenerationState.Faulted);
                    WriteTelemetry(MarauderOutpostConstants.FaultFlag);
                    DumpBlackBox();
                    return;
                }

                CommitCompletedGeneration();
                return;
            }

            if (_jobPhase == JobPhase.Shifting)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _jobHandle))
                    return;

                if (!FlushShiftScratchToVault())
                {
                    _jobPhase = JobPhase.None;
                    SetState(OutpostGenerationState.Faulted);
                    WriteTelemetry(MarauderOutpostConstants.FaultFlag | MarauderOutpostConstants.AupShiftFlag);
                    DumpBlackBox();
                    return;
                }

                _jobPhase = JobPhase.None;
                _matrixUploadDirty = true;
                SetState(_generated ? OutpostGenerationState.Ready : OutpostGenerationState.Idle);
            }

            if (_matrixUploadDirty)
                UploadMatricesAndArgs();

            FlushInteractableProxyShift();

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
            if (!_generated || _jobPhase == JobPhase.Shifting || _matrixUploadDirty || _matrixCount <= 0 || _activeMatrixBuffer == null || _activeCellTypeBuffer == null || _activeArgsBuffer == null)
                return;

            Material material = ResolveRenderMaterial();
            Mesh mesh = ResolveRenderMesh();
            if (material == null || mesh == null || mesh.subMeshCount <= 0)
                return;

            float age = ResolveOutpostAge01();
            MaterialPropertyBlock renderProperties = ResolveRenderProperties(age);

            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = _drawBounds,
                layer = renderLayer,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = receiveShadows,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                matProps = renderProperties
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, _activeArgsBuffer, 1, 0);
        }

        public bool TryRequestGeneration(ulong sectorHash, float3 originMeters, uint worldSeed)
        {
            if (sectorHash == 0UL)
            {
                WriteTelemetry(MarauderOutpostConstants.FaultFlag, sectorHash);
                return false;
            }

            if (!IsPersistentStateReady())
            {
                SetState(OutpostGenerationState.Faulted);
                return false;
            }

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

            if (!math.all(math.isfinite(originMeters)))
            {
                WriteTelemetry(MarauderOutpostConstants.FaultFlag, sectorHash);
                DumpBlackBox();
                SetState(OutpostGenerationState.Faulted);
                return false;
            }

            DespawnInteractables();
            _generated = false;
            _matrixCount = 0;
            _interactableCount = 0;
            _solidCellCount = 0;
            _supportCount = 0;
            _missingHeightmap = false;
            ReleasePublishedPowerGrid();
            _activeSectorHash = sectorHash;
            _activeWorldSeed = worldSeed;
            _activeSolveSeed = MarauderOutpostHash.LcgHash((ulong)worldSeed + ResolveFirstBaseHash());
            _activeGridHash = 0u;
            _generationOrigin = originMeters;
            _generationQualityWeight01 = ResolveOutpostQualityWeight01();
            _compatibilityQualityTier = ResolveCompatibilityQualityTier(_generationQualityWeight01);
            _activeDimensions = ResolveContinuousActiveDimensions(_generationQualityWeight01);

            RestoreWfcMutableState(sectorHash);

            if (!TryPrepareSolveScratch(out NativeArray<byte> wfcGrid))
            {
                WriteTelemetry(MarauderOutpostConstants.FaultFlag, sectorHash);
                SetState(OutpostGenerationState.Faulted);
                return false;
            }

            try
            {
                MarauderOutpostSolveJob job = new MarauderOutpostSolveJob
                {
                    WfcGrid = wfcGrid,
                    Dimensions = ResolveActiveDimensions(),
                    Seed = _activeSolveSeed,
                    GlobalQualityWeight = _generationQualityWeight01
                };

                _jobHandle = job.Schedule();
                _jobPhase = JobPhase.Solving;
                SetState(OutpostGenerationState.Solving);
                _generationSequence++;
                UpdateSnapshot();
                return true;
            }
            catch
            {
                _jobHandle = default;
                _jobPhase = JobPhase.None;
                throw;
            }
        }

        public bool TryGetWfcGrid(out NativeArray<byte>.ReadOnly cells, out int3 dimensions, out int cellCount, out uint gridHash, out uint generationSequence)
        {
            generationSequence = _generationSequence;
            if (!_generated || !TryReadWfcGrid(out NativeArray<byte>.ReadOnly wfcGrid))
            {
                cells = default;
                dimensions = default;
                cellCount = 0;
                gridHash = 0u;
                return false;
            }

            cellCount = math.min(ResolveActiveCellCount(), wfcGrid.Length);
            if (cellCount <= 0)
            {
                cells = default;
                dimensions = default;
                gridHash = 0u;
                return false;
            }

            cells = wfcGrid;
            dimensions = ResolveActiveDimensions();
            gridHash = _activeGridHash;
            return true;
        }

        public bool TryGetShellMatrices(out NativeArray<float4x4>.ReadOnly matrices, out int matrixCount, out uint generationSequence)
        {
            generationSequence = _generationSequence;
            if (!_generated || _jobPhase == JobPhase.Shifting || !TryReadShellMatrices(out NativeArray<float4x4>.ReadOnly shellMatrices))
            {
                matrices = default;
                matrixCount = 0;
                return false;
            }

            matrixCount = math.min(math.max(0, _matrixCount), shellMatrices.Length);
            if (matrixCount <= 0)
            {
                matrices = default;
                return false;
            }

            matrices = shellMatrices;
            return true;
        }

        public bool TryGetShellGraphicsBuffer(out GraphicsBuffer matrixBuffer, out GraphicsBuffer argsBuffer, out int instanceCount, out uint generationSequence)
        {
            generationSequence = _generationSequence;
            if (!_generated || _jobPhase == JobPhase.Shifting || _matrixUploadDirty || _activeMatrixBuffer == null || _activeArgsBuffer == null)
            {
                matrixBuffer = null;
                argsBuffer = null;
                instanceCount = 0;
                return false;
            }

            instanceCount = math.min(math.max(0, _matrixCount), _activeMatrixBuffer.count);
            if (instanceCount <= 0)
            {
                matrixBuffer = null;
                argsBuffer = null;
                return false;
            }

            matrixBuffer = _activeMatrixBuffer;
            argsBuffer = _activeArgsBuffer;
            return true;
        }

        public void ApplyAupShift(float3 shiftMeters, uint shiftFrameId)
        {
            if (!math.all(math.isfinite(shiftMeters)))
            {
                WriteTelemetry(MarauderOutpostConstants.FaultFlag | MarauderOutpostConstants.AupShiftFlag);
                DumpBlackBox();
                return;
            }

            if (!IsWithinAupShiftLimit(shiftMeters))
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
            if (!_generated || _matrixCount <= 0)
            {
                UpdateSnapshot();
                return;
            }

            if (!TryPrepareShiftScratch(out NativeArray<float4x4> shellMatrices))
            {
                AccumulatePendingShift(shiftMeters, shiftFrameId);
                UpdateSnapshot();
                return;
            }

            MarauderOutpostAupShiftJob job = new MarauderOutpostAupShiftJob
            {
                ShellMatrices = shellMatrices,
                ShiftMeters = shiftMeters
            };
            try
            {
                _jobHandle = job.Schedule(_matrixCount, 64);
                _jobPhase = JobPhase.Shifting;
                _drawBounds.center -= new Vector3(shiftMeters.x, shiftMeters.y, shiftMeters.z);
                WriteTelemetry(MarauderOutpostConstants.AupShiftFlag);
                UpdateSnapshot();
            }
            catch
            {
                _jobHandle = default;
                _jobPhase = JobPhase.None;
                throw;
            }
        }

        private void DrainSectorHydratedSignals()
        {
            if (_jobPhase != JobPhase.None || (_generated && _publishedPowerGridHandle != 0u))
                return;

            ReadOnlySpan<Hecton8.Core.Contracts.Signals.MacroDatabaseSectorHydrationSignal> signals =
                SignalBus<Hecton8.Core.Contracts.Signals.MacroDatabaseSectorHydrationSignal>.GetFrameSnapshot();
            ulong targetBaseHash = ResolveFirstBaseHash();
            for (int i = 0; i < signals.Length; i++)
            {
                Hecton8.Core.Contracts.Signals.MacroDatabaseSectorHydrationSignal signal = signals[i];
                if (!generateOnAnyHydratedSectorForDebug && signal.SectorHash != targetBaseHash)
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
            if (!TryReadFullWfcGrid(out NativeArray<byte>.ReadOnly wfcGrid))
            {
                _jobHandle = default;
                _jobPhase = JobPhase.None;
                WriteTelemetry(MarauderOutpostConstants.FaultFlag);
                SetState(OutpostGenerationState.Faulted);
                return;
            }

            MapMagicBridge.QuantizedHeightmapPayload payload = ResolveHeightmapPayload();
            bool hasHeightmapPayload = IsValidHeightmapPayload(in payload, _generationOrigin);
            if (!hasHeightmapPayload)
            {
                _jobHandle = default;
                _jobPhase = JobPhase.None;
                _missingHeightmap = true;
                _generated = false;
                _matrixCount = 0;
                _interactableCount = 0;
                _solidCellCount = 0;
                _supportCount = 0;
                _matrixUploadDirty = false;
                WriteTelemetry(MarauderOutpostConstants.FaultFlag | MarauderOutpostConstants.MissingHeightmapFlag);
                DumpBlackBox();
                SetState(OutpostGenerationState.Faulted);
                return;
            }

            if (!TryPrepareExtractionScratch(
                    out NativeArray<byte> mutableGrid,
                    out NativeArray<float4x4> shellMatrices,
                    out NativeArray<uint> shellCellTypes,
                    out NativeArray<OutpostInteractableSpawn> interactableSpawns,
                    out NativeArray<int> counters))
            {
                _jobHandle = default;
                _jobPhase = JobPhase.None;
                WriteTelemetry(MarauderOutpostConstants.FaultFlag);
                SetState(OutpostGenerationState.Faulted);
                return;
            }

            MarauderOutpostMatrixExtractionJob job = new MarauderOutpostMatrixExtractionJob
            {
                WfcGrid = wfcGrid,
                MutableGrid = mutableGrid,
                HeightSamples = payload.HeightSamples,
                ShellMatrices = shellMatrices,
                CellTypes = shellCellTypes,
                InteractableSpawns = interactableSpawns,
                Counters = counters,
                Dimensions = ResolveActiveDimensions(),
                OriginMeters = _generationOrigin,
                TerrainPosition = ToFloat3(payload.TerrainPosition),
                TerrainSize = ToFloat3(payload.TerrainSize),
                HeightResolution = payload.HeightmapResolution,
                CellSizeMeters = ResolveCellSizeMeters(),
                FloorHeightMeters = ResolveFloorHeightMeters(),
                StiltClearanceMeters = ResolveStiltClearanceMeters(),
                OutpostAge01 = ResolveOutpostAge01(),
                Seed = _activeSolveSeed,
                GlobalQualityWeight = _generationQualityWeight01
            };

            try
            {
                _jobHandle = job.Schedule();
                _jobPhase = JobPhase.Extracting;
                SetState(OutpostGenerationState.ExtractingMatrices);
            }
            catch
            {
                _jobHandle = default;
                _jobPhase = JobPhase.None;
                throw;
            }
        }

        private void CommitCompletedGeneration()
        {
            _jobPhase = JobPhase.None;
            if (!TryReadCounters(out NativeArray<int>.ReadOnly counters))
            {
                SetState(OutpostGenerationState.Faulted);
                WriteTelemetry(MarauderOutpostConstants.FaultFlag);
                DumpBlackBox();
                return;
            }

            _matrixCount = counters.Length > 0 ? math.clamp(counters[0], 0, MarauderOutpostConstants.MaxShellMatrices) : 0;
            _interactableCount = counters.Length > 1 ? math.clamp(counters[1], 0, MarauderOutpostConstants.MaxInteractables) : 0;
            _solidCellCount = counters.Length > 2 ? math.max(0, counters[2]) : 0;
            _supportCount = counters.Length > 3 ? math.max(0, counters[3]) : 0;
            _missingHeightmap = counters.Length > 4 && counters[4] != 0;
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
            WriteTelemetry((_missingHeightmap ? MarauderOutpostConstants.MissingHeightmapFlag : 0u) |
                           (published ? 0u : MarauderOutpostConstants.FaultFlag));
        }

        private MapMagicBridge.QuantizedHeightmapPayload ResolveHeightmapPayload()
        {
            MapMagicBridge bridge = ResolveMapMagicBridge();
            if (bridge == null)
                return default;

            Vector3 origin = new Vector3(_generationOrigin.x, _generationOrigin.y, _generationOrigin.z);
            if (bridge.TryGetQuantizedHeightmapPayloadAUP(origin, out MapMagicBridge.QuantizedHeightmapPayload payload) && IsValidHeightmapPayload(in payload, _generationOrigin))
                return payload;

            if (bridge.TryGetActiveQuantizedHeightmapPayload(out payload) && IsValidHeightmapPayload(in payload, _generationOrigin))
                return payload;

            return default;
        }

        private static bool IsValidHeightmapPayload(in MapMagicBridge.QuantizedHeightmapPayload payload, float3 originMeters)
        {
            int resolution = payload.HeightmapResolution;
            if (!payload.HeightSamples.IsCreated || resolution <= 1 || resolution > 46340)
                return false;

            int requiredLength = resolution * resolution;
            float3 terrainPosition = ToFloat3(payload.TerrainPosition);
            float3 terrainSize = ToFloat3(payload.TerrainSize);
            return payload.HeightSamples.Length >= requiredLength &&
                   math.all(math.isfinite(originMeters)) &&
                   IsFinite(payload.TerrainPosition) &&
                   IsFinite(payload.TerrainSize) &&
                   payload.TerrainSize.x > 0.001f &&
                   payload.TerrainSize.y > 0.001f &&
                   payload.TerrainSize.z > 0.001f &&
                   math.all(math.isfinite(originMeters - terrainPosition)) &&
                   math.isfinite(terrainPosition.y + terrainSize.y);
        }

        private uint ResolveWorldSeed()
        {
            if (global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int runtimeWorldSeed))
                return unchecked((uint)runtimeWorldSeed);

            IWorldSeedProvider seedProvider = _cachedWorldSeedProvider;
            if (seedProvider != null &&
                !IsDestroyedUnityObject(seedProvider) &&
                seedProvider.IsInitialized)
            {
                return unchecked((uint)seedProvider.RuntimeWorldSeed);
            }

            return fallbackWorldSeed;
        }

        private ulong ResolveFirstBaseHash()
        {
            return firstBaseHash != 0UL ? firstBaseHash : DefaultFirstBaseHash;
        }

        private float3 ResolveGenerationOriginMeters()
        {
            Transform anchor = outpostOriginOverride != null ? outpostOriginOverride : transform;
            Vector3 position = anchor != null ? anchor.position : Vector3.zero;
            if (!IsFinite(position))
                position = Vector3.zero;
            if (IsFinite(localOriginOffsetMeters))
            {
                position += localOriginOffsetMeters;
                if (!IsFinite(position))
                    position = Vector3.zero;
            }
            return ToFloat3(position);
        }

        private static OutpostGenerationQualityTier ResolveCompatibilityQualityTier(float qualityWeight01)
        {
            int tierOrdinal = (int)math.round(ResolveCompatibilityQualityTierOrdinal(qualityWeight01));
            tierOrdinal = math.clamp(
                tierOrdinal,
                (int)OutpostGenerationQualityTier.Low,
                (int)OutpostGenerationQualityTier.Ultra);
            return (OutpostGenerationQualityTier)tierOrdinal;
        }

        private static float ResolveCompatibilityQualityTierOrdinal(float qualityWeight01)
        {
            float q = MathLodApproximation.SmoothStep01(MathLodApproximation.SaturateFinite(qualityWeight01, 1f));
            return math.lerp(
                (float)OutpostGenerationQualityTier.Low,
                (float)OutpostGenerationQualityTier.Ultra,
                q);
        }

        private static byte EncodeQualityWeightQ8(float qualityWeight01)
        {
            return (byte)math.clamp((int)math.round(MathLodApproximation.SaturateFinite(qualityWeight01, 1f) * 255f), 0, 255);
        }

        private static byte EncodeSurvivalBandWeightQ8(float qualityWeight01)
        {
            return (byte)math.clamp((int)math.round(ResolveSurvivalBandWeight01(qualityWeight01) * 255f), 0, 255);
        }

        private static float ResolveSurvivalBandWeight01(float qualityWeight01)
        {
            return 1f - MathLodApproximation.SmoothRange01(0.18f, 0.42f, qualityWeight01);
        }

        private static int3 ResolveContinuousActiveDimensions(float qualityWeight01)
        {
            float q = MathLodApproximation.SmoothStep01(MathLodApproximation.SaturateFinite(qualityWeight01, 1f));
            return new int3(
                math.clamp((int)math.round(math.lerp(MarauderOutpostConstants.LowWidth, MarauderOutpostConstants.FullWidth, q)), MarauderOutpostConstants.LowWidth, MarauderOutpostConstants.FullWidth),
                math.clamp((int)math.round(math.lerp(MarauderOutpostConstants.LowHeight, MarauderOutpostConstants.FullHeight, q)), MarauderOutpostConstants.LowHeight, MarauderOutpostConstants.FullHeight),
                math.clamp((int)math.round(math.lerp(MarauderOutpostConstants.LowDepth, MarauderOutpostConstants.FullDepth, q)), MarauderOutpostConstants.LowDepth, MarauderOutpostConstants.FullDepth));
        }

        private static float ResolveOutpostQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 1f;
        }

        private MapMagicBridge ResolveMapMagicBridge()
        {
            return _cachedMapMagicBridge;
        }

        private IAsyncPersistenceService ResolveAsyncPersistence()
        {
            IAsyncPersistenceService persistence = _cachedPersistence;
            if (IsAsyncPersistenceUsable(persistence))
                return persistence;

            persistence = GlobalRegistry.AsyncPersistence;
            _cachedPersistence = IsAsyncPersistenceUsable(persistence) ? persistence : null;
            return _cachedPersistence;
        }

        private IObjectPoolService ResolveObjectPool()
        {
            return TryResolveCachedObjectPool(out IObjectPoolService pool) ? pool : null;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            if (!ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(candidate))
            {
                _cachedObjectPool = null;
                return;
            }

            _cachedObjectPool = candidate;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _cachedObjectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _cachedObjectPool = resolved;
                pool = resolved;
                return true;
            }

            _cachedObjectPool = null;
            pool = null;
            return false;
        }

        private static bool CanDespawnWithPool(IObjectPoolService pool, GameObject instance)
        {
            return instance != null &&
                   pool is ObjectPoolManager manager &&
                   ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(manager) &&
                   manager.CanDespawnWithoutDestroy(instance);
        }

        private static bool TryResolvePoolForInstance(
            GameObject instance,
            IObjectPoolService preferredPool,
            out IObjectPoolService pool)
        {
            if (CanDespawnWithPool(preferredPool, instance))
            {
                pool = preferredPool;
                return true;
            }

            if (instance != null &&
                instance.TryGetComponent(out ObjectPoolManager.PoolItemMarker marker) &&
                marker.Owner != null &&
                ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(marker.Owner) &&
                marker.Owner.CanDespawnWithoutDestroy(instance))
            {
                pool = marker.Owner;
                return true;
            }

            ObjectPoolManager active = null;
            if (instance != null &&
                ObjectPoolManager.TryResolveActiveRuntime(ref active) &&
                active.CanDespawnWithoutDestroy(instance))
            {
                pool = active;
                return true;
            }

            pool = null;
            return false;
        }

        private static void DespawnInteractableProxyOrDeactivate(IObjectPoolService pool, GameObject instance)
        {
            if (TryResolvePoolForInstance(instance, pool, out IObjectPoolService ownerPool))
            {
                ownerPool.Despawn(instance);
                return;
            }

            if (instance != null)
                instance.SetActive(false);
        }

        private void CacheRegistryDependenciesCold()
        {
            if (_cachedMapMagicBridge == null)
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _cachedMapMagicBridge);

            if (!IsAsyncPersistenceUsable(_cachedPersistence))
            {
                IAsyncPersistenceService persistence = GlobalRegistry.AsyncPersistence;
                _cachedPersistence = IsAsyncPersistenceUsable(persistence) ? persistence : null;
            }

            if (_cachedObjectPool == null)
                CacheObjectPoolService(null);
        }

        private void ClearCachedRegistryDependencies()
        {
            _registeredRenderables = null;
            _cachedMapMagicBridge = null;
            _cachedWorldSeedProvider = null;
            _cachedPersistence = null;
            _cachedObjectPool = null;
        }

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate != 0 || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment) ? 1 : 0;
        }

        private void TryUnregisterUpdate()
        {
            if (_registeredUpdate == 0)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredUpdate = 0;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame != 0 || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment) ? 1 : 0;
        }

        private void TryUnregisterLateFrame()
        {
            if (_registeredLateFrame == 0)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = 0;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener != 0 || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this) ? 1 : 0;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (_registeredHotSwapListener == 0)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = 0;
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

        private bool EnsurePersistentState()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return false;

            bool buffersReady =
                EnsureVaultBuffer(ref _wfcGridHandle, WfcGridBufferId, MarauderOutpostConstants.FullCellCount, NativeArrayOptions.ClearMemory) &&
                EnsureVaultBuffer(ref _wfcMutableStateGridHandle, WfcMutableStateGridBufferId, MarauderOutpostConstants.FullCellCount, NativeArrayOptions.ClearMemory) &&
                EnsureVaultBuffer(ref _shellMatricesHandle, ShellMatricesBufferId, MarauderOutpostConstants.MaxShellMatrices, NativeArrayOptions.ClearMemory) &&
                EnsureVaultBuffer(ref _shellCellTypesHandle, ShellCellTypesBufferId, MarauderOutpostConstants.MaxShellMatrices, NativeArrayOptions.ClearMemory) &&
                EnsureVaultBuffer(ref _interactableSpawnsHandle, InteractableSpawnsBufferId, MarauderOutpostConstants.MaxInteractables, NativeArrayOptions.ClearMemory) &&
                EnsureVaultBuffer(ref _countersHandle, CountersBufferId, MarauderOutpostConstants.CounterCount, NativeArrayOptions.ClearMemory) &&
                EnsureVaultBuffer(ref _telemetryRingHandle, TelemetryRingBufferId, MarauderOutpostConstants.TelemetryFrames, NativeArrayOptions.ClearMemory);

            bool scratchReady = _scratchBuffers.EnsureAll();

            if (_spawnedInteractables == null || _spawnedInteractables.Length != MarauderOutpostConstants.MaxInteractables)
                _spawnedInteractables = new GameObject[MarauderOutpostConstants.MaxInteractables]; // COLD ALLOC: GameObject[16] - spawned interactable proxy handles - owner: MARAUDER_OUTPOST_ARCHITECT

            if (_spawnedInteractableOwners == null || _spawnedInteractableOwners.Length != MarauderOutpostConstants.MaxInteractables)
                _spawnedInteractableOwners = new IObjectPoolService[MarauderOutpostConstants.MaxInteractables]; // COLD ALLOC: IObjectPoolService[16] - spawned interactable proxy pool owners - owner: MARAUDER_OUTPOST_ARCHITECT

            if (_spawnedDoorControllers == null || _spawnedDoorControllers.Length != MarauderOutpostConstants.MaxInteractables)
                _spawnedDoorControllers = new SealedDoor[MarauderOutpostConstants.MaxInteractables]; // COLD ALLOC: SealedDoor[16] - cached WFC door controllers for power unlocks - owner: MARAUDER_OUTPOST_ARCHITECT

            return buffersReady && scratchReady;
        }

        private bool IsPersistentStateReady()
        {
            return IsExactVaultHandle(in _wfcGridHandle, WfcGridBufferId) &&
                   IsExactVaultHandle(in _wfcMutableStateGridHandle, WfcMutableStateGridBufferId) &&
                   IsExactVaultHandle(in _shellMatricesHandle, ShellMatricesBufferId) &&
                   IsExactVaultHandle(in _shellCellTypesHandle, ShellCellTypesBufferId) &&
                   IsExactVaultHandle(in _interactableSpawnsHandle, InteractableSpawnsBufferId) &&
                   IsExactVaultHandle(in _countersHandle, CountersBufferId) &&
                   IsExactVaultHandle(in _telemetryRingHandle, TelemetryRingBufferId) &&
                   _scratchBuffers.IsReady() &&
                   _spawnedInteractables != null &&
                   _spawnedInteractableOwners != null &&
                   _spawnedDoorControllers != null &&
                   _spawnedInteractables.Length == MarauderOutpostConstants.MaxInteractables &&
                   _spawnedInteractableOwners.Length == MarauderOutpostConstants.MaxInteractables &&
                   _spawnedDoorControllers.Length == MarauderOutpostConstants.MaxInteractables;
        }

        private void RestoreWfcMutableState(ulong sectorHash)
        {
            if (!TryAcquireMutableStateWriteBuffer(out NativeArray<byte> mutableStateGrid))
                return;

            try
            {
                for (int i = 0; i < mutableStateGrid.Length; i++)
                    mutableStateGrid[i] = 0;

                if (sectorHash == 0UL)
                    return;

                IAsyncPersistenceService persistence = ResolveAsyncPersistence();
                if (persistence == null)
                    return;

                persistence.TryApplyWfcOutpostStateOverride(sectorHash, mutableStateGrid, out _);
            }
            finally
            {
                ReleaseMutableStateWriteBuffer();
            }
        }

        private void EnsureGraphicsResources()
        {
            bool matrixBufferCreated = false;
            bool cellTypeBufferCreated = false;
            bool argsBufferCreated = false;

            if (_matrixBufferA == null || _matrixBufferB == null)
            {
                ReleaseGraphicsBuffer(ref _matrixBufferA);
                ReleaseGraphicsBuffer(ref _matrixBufferB);
                _matrixBufferA = CreateStructuredLockBuffer<float4x4>(MarauderOutpostConstants.MaxShellMatrices); // COLD ALLOC: GraphicsBuffer[1024 float4x4] - outpost shell matrices A - owner: MARAUDER_OUTPOST_ARCHITECT
                _matrixBufferB = CreateStructuredLockBuffer<float4x4>(MarauderOutpostConstants.MaxShellMatrices); // COLD ALLOC: GraphicsBuffer[1024 float4x4] - outpost shell matrices B - owner: MARAUDER_OUTPOST_ARCHITECT
                _activeMatrixBuffer = _matrixBufferA;
                matrixBufferCreated = true;
            }

            if (_cellTypeBufferA == null || _cellTypeBufferB == null)
            {
                ReleaseGraphicsBuffer(ref _cellTypeBufferA);
                ReleaseGraphicsBuffer(ref _cellTypeBufferB);
                _cellTypeBufferA = CreateStructuredLockBuffer<uint>(MarauderOutpostConstants.MaxShellMatrices); // COLD ALLOC: GraphicsBuffer[1024 uint] - outpost shell types A - owner: MARAUDER_OUTPOST_ARCHITECT
                _cellTypeBufferB = CreateStructuredLockBuffer<uint>(MarauderOutpostConstants.MaxShellMatrices); // COLD ALLOC: GraphicsBuffer[1024 uint] - outpost shell types B - owner: MARAUDER_OUTPOST_ARCHITECT
                _activeCellTypeBuffer = _cellTypeBufferA;
                cellTypeBufferCreated = true;
            }

            if (_argsBufferA == null || _argsBufferB == null)
            {
                ReleaseGraphicsBuffer(ref _argsBufferA);
                ReleaseGraphicsBuffer(ref _argsBufferB);
                _argsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - outpost indirect draw args A - owner: MARAUDER_OUTPOST_ARCHITECT
                _argsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - outpost indirect draw args B - owner: MARAUDER_OUTPOST_ARCHITECT
                _activeArgsBuffer = _argsBufferA;
                argsBufferCreated = true;
            }

            if (_renderPropertyBlock == null)
            {
                _renderPropertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - per-outpost indirect draw payload - owner: MARAUDER_OUTPOST_ARCHITECT
                _renderPropertiesDirty = true;
            }

            ValidateAuthoredRenderResources();

            if (matrixBufferCreated || cellTypeBufferCreated || argsBufferCreated)
                _renderPropertiesDirty = true;

            if (_generated && _matrixCount > 0 && (matrixBufferCreated || cellTypeBufferCreated || argsBufferCreated))
                UploadMatricesAndArgs();
            else if (argsBufferCreated)
                UpdateIndirectArgsBuffer(0u);
        }

        private void UploadMatricesAndArgs()
        {
            if (!TryReadShellMatrices(out NativeArray<float4x4>.ReadOnly shellMatrices) ||
                !TryReadShellCellTypes(out NativeArray<uint>.ReadOnly shellCellTypes) ||
                _activeMatrixBuffer == null ||
                _activeCellTypeBuffer == null ||
                _activeArgsBuffer == null)
            {
                return;
            }

            int count = math.clamp(_matrixCount, 0, MarauderOutpostConstants.MaxShellMatrices);
            count = math.min(count, shellMatrices.Length);
            count = math.min(count, shellCellTypes.Length);
            GraphicsBuffer matrixWriteBuffer = ResolveShellMatrixWriteBuffer();
            GraphicsBuffer cellTypeWriteBuffer = ResolveShellCellTypeWriteBuffer();
            GraphicsBuffer argsWriteBuffer = ResolveShellArgsWriteBuffer();
            if (matrixWriteBuffer == null || cellTypeWriteBuffer == null || argsWriteBuffer == null)
                return;

            _activeArgsBuffer = argsWriteBuffer;
            count = math.min(count, matrixWriteBuffer.count);
            count = math.min(count, cellTypeWriteBuffer.count);
            if (count > 0)
            {
                UploadNativeArray(matrixWriteBuffer, shellMatrices, count);
                UploadNativeArray(cellTypeWriteBuffer, shellCellTypes, count);
                _activeMatrixBuffer = matrixWriteBuffer;
                _activeCellTypeBuffer = cellTypeWriteBuffer;
                _activeArgsBuffer = argsWriteBuffer;
                _shellUploadBufferIndex ^= 1;
            }

            UpdateIndirectArgsBuffer((uint)count);
            _matrixUploadDirty = false;
        }

        private GraphicsBuffer ResolveShellMatrixWriteBuffer()
        {
            GraphicsBuffer preferred = (_shellUploadBufferIndex & 1) == 0 ? _matrixBufferB : _matrixBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _matrixBufferA != null && _matrixBufferA.IsValid() ? _matrixBufferA : _matrixBufferB;
        }

        private GraphicsBuffer ResolveShellCellTypeWriteBuffer()
        {
            GraphicsBuffer preferred = (_shellUploadBufferIndex & 1) == 0 ? _cellTypeBufferB : _cellTypeBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _cellTypeBufferA != null && _cellTypeBufferA.IsValid() ? _cellTypeBufferA : _cellTypeBufferB;
        }

        private GraphicsBuffer ResolveShellArgsWriteBuffer()
        {
            GraphicsBuffer preferred = (_shellUploadBufferIndex & 1) == 0 ? _argsBufferB : _argsBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _argsBufferA != null && _argsBufferA.IsValid() ? _argsBufferA : _argsBufferB;
        }

        private void UpdateIndirectArgsBuffer(uint instanceCount)
        {
            if (_activeArgsBuffer == null)
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
                _activeArgsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            try
            {
                argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = indexCount,
                    instanceCount = safeInstanceCount,
                    startIndex = startIndex,
                    baseVertexIndex = baseVertexIndex,
                    startInstance = 0u
                };
            }
            finally
            {
                _activeArgsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            }
        }

        private void SpawnInteractableProxies()
        {
            if (_interactableCount <= 0 || _spawnedInteractables == null)
                return;

            IObjectPoolService pool = ResolveObjectPool();
            if (pool == null)
                return;

            if (!TryReadInteractableSpawns(out NativeArray<OutpostInteractableSpawn>.ReadOnly interactableSpawns))
                return;

            int count = math.min(_interactableCount, interactableSpawns.Length);
            for (int i = 0; i < count; i++)
            {
                OutpostInteractableSpawn spawn = interactableSpawns[i];
                GameObject prefab = spawn.Kind == MarauderOutpostConstants.SealedDoor ? sealedDoorProxyPrefab : datapadProxyPrefab;
                if (prefab == null)
                    continue;

                Vector3 position = new Vector3(spawn.PositionMeters.x, spawn.PositionMeters.y, spawn.PositionMeters.z);
                Quaternion rotation = Quaternion.Euler(0f, spawn.RotationYRadians * Mathf.Rad2Deg, 0f);
                _spawnedInteractables[i] = pool.Spawn(prefab, position, rotation);
                GameObject instance = _spawnedInteractables[i];
                if (_spawnedInteractableOwners != null && i < _spawnedInteractableOwners.Length)
                    _spawnedInteractableOwners[i] = instance != null ? pool : null;

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

            if (!TryReadInteractableSpawns(out NativeArray<OutpostInteractableSpawn>.ReadOnly interactableSpawns))
                return;

            ReadOnlySpan<WfcOutpostDoorPowerSignal> signals = SignalBus<WfcOutpostDoorPowerSignal>.GetFrameSnapshot();
            for (int signalIndex = 0; signalIndex < signals.Length; signalIndex++)
            {
                WfcOutpostDoorPowerSignal signal = signals[signalIndex];
                if (signal.SectorHash != _activeSectorHash || signal.GridHandle != _publishedPowerGridHandle)
                {
                    continue;
                }

                int count = math.min(_interactableCount, math.min(_spawnedDoorControllers.Length, interactableSpawns.Length));
                for (int i = 0; i < count; i++)
                {
                    if (interactableSpawns[i].CellIndex != signal.CellIndex)
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
        }

        private void DespawnInteractables()
        {
            if (_spawnedInteractables == null)
                return;

            IObjectPoolService pool = ResolveObjectPool();
            for (int i = 0; i < _spawnedInteractables.Length; i++)
            {
                GameObject instance = _spawnedInteractables[i];
                if (instance != null)
                {
                    ClearInteractablePersistence(instance);
                    IObjectPoolService ownerPool = _spawnedInteractableOwners != null && i < _spawnedInteractableOwners.Length
                        ? _spawnedInteractableOwners[i]
                        : pool;
                    DespawnInteractableProxyOrDeactivate(ownerPool, instance);
                }

                _spawnedInteractables[i] = null;
                if (_spawnedInteractableOwners != null && i < _spawnedInteractableOwners.Length)
                    _spawnedInteractableOwners[i] = null;

                if (_spawnedDoorControllers != null && i < _spawnedDoorControllers.Length)
                    _spawnedDoorControllers[i] = null;
            }
        }

        private void ShiftInteractableProxies(float3 shiftMeters)
        {
            if (_spawnedInteractables == null)
                return;

            _pendingInteractableProxyShift += shiftMeters;
            _interactableProxyShiftDirty = math.any(math.abs(_pendingInteractableProxyShift) > new float3(ShiftEpsilonMeters));
        }

        private void FlushInteractableProxyShift()
        {
            if (!_interactableProxyShiftDirty || _spawnedInteractables == null)
                return;

            float3 pendingShift = _pendingInteractableProxyShift;
            _pendingInteractableProxyShift = default;
            _interactableProxyShiftDirty = false;
            Vector3 shift = new Vector3(pendingShift.x, pendingShift.y, pendingShift.z);
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
            {
                float3 accumulatedShift = _pendingShift + shiftMeters;
                if (!math.all(math.isfinite(accumulatedShift)) || !IsWithinAupShiftLimit(accumulatedShift))
                {
                    _pendingShift = default;
                    _pendingShiftFrameId = shiftFrameId;
                    _hasPendingShift = false;
                    WriteTelemetry(MarauderOutpostConstants.FaultFlag | MarauderOutpostConstants.AupShiftFlag);
                    DumpBlackBox();
                    return;
                }

                _pendingShift = accumulatedShift;
            }

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

            if (!IsWithinAupShiftLimit(_pendingShift))
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

            if (TryAcquireShellMatricesWriteBuffer(out NativeArray<float4x4> shellMatrices))
            {
                try
                {
                    int safeMatrixCount = math.min(math.max(0, matrixCount), shellMatrices.Length);
                    for (int i = 0; i < safeMatrixCount; i++)
                    {
                        float4x4 matrix = shellMatrices[i];
                        matrix.c3.x -= shift.x;
                        matrix.c3.y -= shift.y;
                        matrix.c3.z -= shift.z;
                        shellMatrices[i] = matrix;
                    }
                }
                finally
                {
                    ReleaseShellMatricesWriteBuffer();
                }
            }

            if (TryAcquireInteractableSpawnsWriteBuffer(out NativeArray<OutpostInteractableSpawn> interactableSpawns))
            {
                try
                {
                    int safeInteractableCount = math.min(math.max(0, interactableCount), interactableSpawns.Length);
                    for (int i = 0; i < safeInteractableCount; i++)
                    {
                        OutpostInteractableSpawn spawn = interactableSpawns[i];
                        spawn.PositionMeters -= shift;
                        interactableSpawns[i] = spawn;
                    }
                }
                finally
                {
                    ReleaseInteractableSpawnsWriteBuffer();
                }
            }

            WriteTelemetry(MarauderOutpostConstants.AupShiftFlag);
        }

        private void UpdateDrawBounds()
        {
            int3 dimensions = ResolveActiveDimensions();
            float width = math.max(dimensions.x, dimensions.z) * ResolveCellSizeMeters() + 12f;
            float height = math.max(4f, dimensions.y * ResolveFloorHeightMeters() + 12f);
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
                Dimensions = ResolveActiveDimensions(),
                ShellMatrixCount = _matrixCount,
                InteractableCount = _interactableCount,
                OutpostAge01 = ResolveOutpostAge01(),
                QualityTier = _compatibilityQualityTier,
                State = _state,
                Flags = ResolveDescriptorFlags(),
                QualityWeightQ8 = EncodeQualityWeightQ8(_generationQualityWeight01),
                SurvivalBandWeightQ8 = EncodeSurvivalBandWeightQ8(_generationQualityWeight01)
            };
        }

        private ushort ResolveDescriptorFlags()
        {
            return (ushort)(_missingHeightmap ? MarauderOutpostConstants.MissingHeightmapFlag : 0u);
        }

        private bool TryPublishGeneratedSignal()
        {
            if (!_generated)
                return false;

            if (_publishedPowerGridHandle != 0u)
            {
                if (WfcOutpostGridRegistry.TryAcquireGridLease(_publishedPowerGridHandle, out WfcOutpostGridLease lease))
                {
                    try
                    {
                        PublishGeneratedSignalForHandle();
                        _generatedSignalReplayFrames = GeneratedSignalReplayFrames;
                        _generatedSignalHeartbeatFrames = GeneratedSignalHeartbeatFrames;
                        return true;
                    }
                    finally
                    {
                        WfcOutpostGridRegistry.ReleaseGridLease(in lease);
                    }
                }

                _publishedPowerGridHandle = 0u;
            }

            if (!TryResolveGenerationOriginAup(out AbsoluteUniversePosition originAup))
            {
                _publishedPowerGridHandle = 0u;
                _generatedSignalReplayFrames = 0;
                _generatedSignalHeartbeatFrames = GeneratedSignalHeartbeatFrames;
                WriteTelemetry(MarauderOutpostConstants.FaultFlag);
                DumpBlackBox();
                return false;
            }

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
                Dimensions = ResolveActiveDimensions(),
                CellSizeMeters = ResolveCellSizeMeters(),
                FloorHeightMeters = ResolveFloorHeightMeters(),
                SectorHash = _activeSectorHash,
                WorldSeed = _activeWorldSeed,
                GenerationSequence = _generationSequence,
                GridHash = _activeGridHash,
                CellCount = (ushort)math.min(ResolveActiveCellCount(), ushort.MaxValue),
                Flags = ResolveDescriptorFlags()
            };

            if (!TryAcquireWriteBuffer(in _wfcGridHandle, WfcGridBufferId, MarauderOutpostConstants.FullCellCount, out NativeArray<byte> wfcGrid))
            {
                _publishedPowerGridHandle = 0u;
                _generatedSignalReplayFrames = 0;
                _generatedSignalHeartbeatFrames = GeneratedSignalHeartbeatFrames;
                WriteTelemetry(MarauderOutpostConstants.FaultFlag);
                DumpBlackBox();
                return false;
            }

            bool registered;
            try
            {
                registered = WfcOutpostGridRegistry.RegisterGrid(in descriptor, wfcGrid, out _publishedPowerGridHandle);
            }
            finally
            {
                ReleaseWriteBuffer(in _wfcGridHandle, WfcGridBufferId);
            }

            if (!registered)
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

            if (!WfcOutpostGridRegistry.TryAcquireGridLease(_publishedPowerGridHandle, out WfcOutpostGridLease lease))
            {
                _publishedPowerGridHandle = 0u;
                TryPublishGeneratedSignal();
                return;
            }

            try
            {
                PublishGeneratedSignalForHandle();
                if (_generatedSignalReplayFrames > 0)
                    _generatedSignalReplayFrames--;
                else
                    _generatedSignalHeartbeatFrames = GeneratedSignalHeartbeatFrames;
            }
            finally
            {
                WfcOutpostGridRegistry.ReleaseGridLease(in lease);
            }
        }

        private bool AreGraphicsResourcesReady()
        {
            return _matrixBufferA != null &&
                   _matrixBufferB != null &&
                   _cellTypeBufferA != null &&
                   _cellTypeBufferB != null &&
                   _argsBufferA != null &&
                   _argsBufferB != null &&
                   _activeMatrixBuffer != null &&
                   _activeCellTypeBuffer != null &&
                   _activeArgsBuffer != null &&
                   _renderPropertyBlock != null;
        }

        private void PublishGeneratedSignalForHandle()
        {
            if (!TryResolveGenerationOriginAup(out AbsoluteUniversePosition originAup))
                return;

            WfcOutpostGeneratedSignal signal = new WfcOutpostGeneratedSignal
            {
                OriginAup = originAup,
                SectorHash = _activeSectorHash,
                GridHandle = _publishedPowerGridHandle,
                GenerationSequence = _generationSequence,
                Dimensions = ResolveActiveDimensions(),
                CellSizeMeters = ResolveCellSizeMeters(),
                FloorHeightMeters = ResolveFloorHeightMeters(),
                GridHash = _activeGridHash,
                Frame = CurrentFrameU32(),
                CellCount = (ushort)math.min(ResolveActiveCellCount(), ushort.MaxValue),
                Flags = ResolveDescriptorFlags()
            };
            SignalBus<WfcOutpostGeneratedSignal>.TryPushTracked(in signal, ref s_x001MarauderOutpostGenerationServiceSignalPushDropCount);
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
            int3 dimensions = ResolveActiveDimensions();
            return dimensions.x * dimensions.y * dimensions.z;
        }

        private int3 ResolveActiveDimensions()
        {
            return new int3(
                math.clamp(_activeDimensions.x, 0, MarauderOutpostConstants.FullWidth),
                math.clamp(_activeDimensions.y, 0, MarauderOutpostConstants.FullHeight),
                math.clamp(_activeDimensions.z, 0, MarauderOutpostConstants.FullDepth));
        }

        private static uint CurrentFrameU32()
        {
            return Hecton8.Core.SystemDispatcher.CurrentFrameId;
        }

        private uint ComputeGridHash()
        {
            if (!TryReadWfcGrid(out NativeArray<byte>.ReadOnly wfcGrid))
                return 0u;

            int cellCount = math.min(ResolveActiveCellCount(), wfcGrid.Length);
            uint hash = 2166136261u ^ _activeSolveSeed;
            for (int i = 0; i < cellCount; i++)
            {
                hash ^= wfcGrid[i];
                hash *= 16777619u;
            }

            int3 dimensions = ResolveActiveDimensions();
            hash ^= (uint)dimensions.x * 0x9E3779B9u;
            hash ^= (uint)dimensions.y * 0x85EBCA6Bu;
            hash ^= (uint)dimensions.z * 0xC2B2AE35u;
            return hash == 0u ? 1u : hash;
        }

        private void WriteTelemetry(uint flags)
        {
            WriteTelemetry(flags, _activeSectorHash);
        }

        private void WriteTelemetry(uint flags, ulong sectorHash)
        {
            if (!TryAcquireTelemetryWriteBuffer(out NativeArray<OutpostTelemetryEntry> telemetryRing))
                return;

            try
            {
                int length = telemetryRing.Length;
                if (length <= 0)
                    return;

                int index = _telemetryWriteIndex;
                if ((uint)index >= (uint)length)
                    index = 0;

                telemetryRing[index] = new OutpostTelemetryEntry
                {
                    Frame = CurrentFrameU32(),
                    Flags = flags,
                    SectorHash = sectorHash,
                    Seed = _activeSolveSeed,
                    GenerationSequence = _generationSequence,
                    OriginMeters = _generationOrigin,
                    Dimensions = ResolveActiveDimensions(),
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
            finally
            {
                ReleaseTelemetryWriteBuffer();
            }
        }

        private void DumpBlackBox()
        {
            if (!TryReadTelemetryRing(out NativeArray<OutpostTelemetryEntry>.ReadOnly telemetryRing))
                return;

            int length = telemetryRing.Length;
            if (length <= 0)
                return;

            int startIndex = _telemetryWriteIndex;
            if ((uint)startIndex >= (uint)length)
                startIndex = 0;
            int payloadBytes = TelemetryDumpHeaderBytes + length * TelemetryDumpEntryPayloadBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    payloadBytes,
                    nameof(MarauderOutpostGenerationService),
                    TelemetryDumpPayloadLabel,
                    NativeArrayOptions.ClearMemory);
                int writeCursor = 0;
                WriteUInt64LittleEndian(payload, ref writeCursor, TelemetryDumpMagic);
                WriteUInt32LittleEndian(payload, ref writeCursor, TelemetryDumpVersion);
                WriteUInt32LittleEndian(payload, ref writeCursor, unchecked((uint)length));
                WriteUInt32LittleEndian(payload, ref writeCursor, unchecked((uint)TelemetryDumpEntryPayloadBytes));
                WriteUInt32LittleEndian(payload, ref writeCursor, unchecked((uint)startIndex));

                for (int offset = 0; offset < length; offset++)
                {
                    int sourceIndex = startIndex + offset;
                    if (sourceIndex >= length)
                        sourceIndex -= length;

                    int rowEnd = writeCursor + TelemetryDumpEntryPayloadBytes;
                    WriteTelemetryDumpEntry(payload, ref writeCursor, telemetryRing[sourceIndex]);
                    if (writeCursor > rowEnd)
                        return;

                    writeCursor = rowEnd;
                }

                NativeFaultDumpWriter.TryWriteAll(TelemetryDumpPath, payload, writeCursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(MarauderOutpostGenerationService),
                    TelemetryDumpPayloadLabel);
            }
        }

        private static void WriteTelemetryDumpEntry(NativeArray<byte> destination, ref int cursor, OutpostTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, ref cursor, entry.Frame);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Flags);
            WriteUInt64LittleEndian(destination, ref cursor, entry.SectorHash);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Seed);
            WriteUInt32LittleEndian(destination, ref cursor, entry.GenerationSequence);
            WriteFloat3LittleEndian(destination, ref cursor, entry.OriginMeters);
            WriteInt32LittleEndian(destination, ref cursor, entry.Dimensions.x);
            WriteInt32LittleEndian(destination, ref cursor, entry.Dimensions.y);
            WriteInt32LittleEndian(destination, ref cursor, entry.Dimensions.z);
            WriteInt32LittleEndian(destination, ref cursor, entry.MatrixCount);
            WriteInt32LittleEndian(destination, ref cursor, entry.InteractableCount);
            WriteInt32LittleEndian(destination, ref cursor, entry.SolidCellCount);
            WriteInt32LittleEndian(destination, ref cursor, entry.SupportCount);
            WriteFloatLittleEndian(destination, ref cursor, entry.OutpostAge01);
            WriteUInt32LittleEndian(destination, ref cursor, entry.ShiftFrameId);
        }

        private static void WriteFloat3LittleEndian(NativeArray<byte> destination, ref int cursor, float3 value)
        {
            WriteFloatLittleEndian(destination, ref cursor, value.x);
            WriteFloatLittleEndian(destination, ref cursor, value.y);
            WriteFloatLittleEndian(destination, ref cursor, value.z);
        }

        private static void WriteFloatLittleEndian(NativeArray<byte> destination, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> destination, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, ref int cursor, uint value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            cursor += sizeof(uint);
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> destination, ref int cursor, ulong value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            destination[cursor + 4] = (byte)(value >> 32);
            destination[cursor + 5] = (byte)(value >> 40);
            destination[cursor + 6] = (byte)(value >> 48);
            destination[cursor + 7] = (byte)(value >> 56);
            cursor += sizeof(ulong);
        }

        private Material ResolveRenderMaterial()
        {
            return shellMaterial;
        }

        private Mesh ResolveRenderMesh()
        {
            return shellMesh;
        }

        private bool ValidateAuthoredRenderResources()
        {
            if (shellMesh != null && shellMaterial != null)
            {
                _authoredRenderResourceFaultLogged = false;
                return true;
            }

            if (!_authoredRenderResourceFaultLogged)
            {
                _authoredRenderResourceFaultLogged = true;
                WriteTelemetry(MarauderOutpostConstants.FaultFlag);
                SetState(OutpostGenerationState.Faulted);
                H8Debug.LogError("[MarauderOutpostGenerationService] Missing authored shell mesh/material. Authored render resources are required.", this);
            }

            return false;
        }

        private MaterialPropertyBlock ResolveRenderProperties(float age)
        {
            if (_renderPropertyBlock == null)
            {
                _renderPropertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - late-created indirect draw payload - owner: MARAUDER_OUTPOST_ARCHITECT
                _renderPropertiesDirty = true;
            }

            Vector4 decayRuntime = new Vector4(
                age,
                0.55f,
                1f - MathLodApproximation.SmoothRange01(0.18f, 0.42f, _generationQualityWeight01),
                (_activeSolveSeed & 0xFFFFu) * MarauderOutpostConstants.HeightUShortToUnit);

            if (_renderPropertiesDirty || _renderPropertyMatrixBuffer != _activeMatrixBuffer)
            {
                _renderPropertyBlock.SetBuffer(OutpostMatricesId, _activeMatrixBuffer);
                _renderPropertyMatrixBuffer = _activeMatrixBuffer;
            }

            if (_renderPropertiesDirty || _renderPropertyCellTypeBuffer != _activeCellTypeBuffer)
            {
                _renderPropertyBlock.SetBuffer(OutpostCellTypesId, _activeCellTypeBuffer);
                _renderPropertyCellTypeBuffer = _activeCellTypeBuffer;
            }

            if (_renderPropertiesDirty || _renderPropertyAge01 != age)
            {
                _renderPropertyBlock.SetFloat(OutpostAge01Id, age);
                _renderPropertyAge01 = age;
            }

            if (_renderPropertiesDirty || _renderPropertyDecayRuntime != decayRuntime)
            {
                _renderPropertyBlock.SetVector(HectonMaterialDecayRuntimeId, decayRuntime);
                _renderPropertyDecayRuntime = decayRuntime;
            }

            _renderPropertiesDirty = false;
            return _renderPropertyBlock;
        }

        private void ClearRenderPropertyCache()
        {
            if (_renderPropertyBlock != null)
                _renderPropertyBlock.Clear();
            _renderPropertyMatrixBuffer = null;
            _renderPropertyCellTypeBuffer = null;
            _renderPropertyDecayRuntime = default;
            _renderPropertyAge01 = 0f;
            _renderPropertiesDirty = true;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static bool IsDestroyedUnityObject(object instance)
        {
            return instance is UnityEngine.Object unityObject && unityObject == null;
        }

        private static bool IsAsyncPersistenceUsable(IAsyncPersistenceService persistence)
        {
            return persistence != null && !IsDestroyedUnityObject(persistence) && persistence.IsInitialized;
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

        private bool TryResolveGenerationOriginAup(out AbsoluteUniversePosition originAup)
        {
            return TryResolveAupFromRuntimeOrigin(
                new Vector3(_generationOrigin.x, _generationOrigin.y, _generationOrigin.z),
                out originAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return aup.IsFinite();
        }

        private static bool IsWithinAupShiftLimit(float3 shiftMeters)
        {
            return math.all(math.abs(shiftMeters) <= new float3(MaxAupShiftMeters));
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private bool EnsureVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return false;

            int safeLength = math.max(1, requiredLength);
            if (IsExactVaultHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= safeLength)
            {
                return true;
            }

            ReleaseVaultHandle(ref handle);
            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                safeLength,
                VaultOwnerSystemId,
                options);

            return IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= safeLength;
        }

        private bool TryReadVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= math.max(1, requiredLength);
        }

        private bool TryAcquireWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (GetActiveWriteVault(bufferId) != null)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in handle, bufferId) ||
                !vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out buffer))
            {
                return false;
            }

            bool releaseOnFailure = true;
            try
            {
                if (buffer.IsCreated && buffer.Length >= math.max(1, requiredLength))
                {
                    SetActiveWriteVault(bufferId, vault);
                    releaseOnFailure = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
            }
        }

        private void ReleaseWriteBuffer<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            IDataVault vault = TakeActiveWriteVault(bufferId);
            if (vault != null && IsExactVaultHandle(in handle, bufferId))
                vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
        }

        private IDataVault GetActiveWriteVault(BufferID bufferId)
        {
            switch (bufferId)
            {
                case WfcGridBufferId:
                    return _wfcGridWriteVault;
                case ShellMatricesBufferId:
                    return _shellMatricesWriteVault;
                case ShellCellTypesBufferId:
                    return _shellCellTypesWriteVault;
                case InteractableSpawnsBufferId:
                    return _interactableSpawnsWriteVault;
                case WfcMutableStateGridBufferId:
                    return _wfcMutableStateWriteVault;
                case CountersBufferId:
                    return _countersWriteVault;
                case TelemetryRingBufferId:
                    return _telemetryRingWriteVault;
                default:
                    return null;
            }
        }

        private void SetActiveWriteVault(BufferID bufferId, IDataVault vault)
        {
            switch (bufferId)
            {
                case WfcGridBufferId:
                    _wfcGridWriteVault = vault;
                    break;
                case ShellMatricesBufferId:
                    _shellMatricesWriteVault = vault;
                    break;
                case ShellCellTypesBufferId:
                    _shellCellTypesWriteVault = vault;
                    break;
                case InteractableSpawnsBufferId:
                    _interactableSpawnsWriteVault = vault;
                    break;
                case WfcMutableStateGridBufferId:
                    _wfcMutableStateWriteVault = vault;
                    break;
                case CountersBufferId:
                    _countersWriteVault = vault;
                    break;
                case TelemetryRingBufferId:
                    _telemetryRingWriteVault = vault;
                    break;
            }
        }

        private IDataVault TakeActiveWriteVault(BufferID bufferId)
        {
            IDataVault vault = GetActiveWriteVault(bufferId);
            ClearActiveWriteVault(bufferId);
            return vault;
        }

        private void ClearActiveWriteVault(BufferID bufferId)
        {
            switch (bufferId)
            {
                case WfcGridBufferId:
                    _wfcGridWriteVault = null;
                    break;
                case ShellMatricesBufferId:
                    _shellMatricesWriteVault = null;
                    break;
                case ShellCellTypesBufferId:
                    _shellCellTypesWriteVault = null;
                    break;
                case InteractableSpawnsBufferId:
                    _interactableSpawnsWriteVault = null;
                    break;
                case WfcMutableStateGridBufferId:
                    _wfcMutableStateWriteVault = null;
                    break;
                case CountersBufferId:
                    _countersWriteVault = null;
                    break;
                case TelemetryRingBufferId:
                    _telemetryRingWriteVault = null;
                    break;
            }
        }

        private bool TryReadWfcGrid(out NativeArray<byte>.ReadOnly wfcGrid)
        {
            return TryReadVaultBuffer(in _wfcGridHandle, WfcGridBufferId, 1, out wfcGrid);
        }

        private bool TryReadFullWfcGrid(out NativeArray<byte>.ReadOnly wfcGrid)
        {
            return TryReadVaultBuffer(in _wfcGridHandle, WfcGridBufferId, MarauderOutpostConstants.FullCellCount, out wfcGrid);
        }

        private bool TryReadMutableStateGrid(out NativeArray<byte>.ReadOnly mutableStateGrid)
        {
            return TryReadVaultBuffer(in _wfcMutableStateGridHandle, WfcMutableStateGridBufferId, MarauderOutpostConstants.FullCellCount, out mutableStateGrid);
        }

        private bool TryReadShellMatrices(out NativeArray<float4x4>.ReadOnly shellMatrices)
        {
            return TryReadVaultBuffer(in _shellMatricesHandle, ShellMatricesBufferId, 1, out shellMatrices);
        }

        private bool TryReadFullShellMatrices(out NativeArray<float4x4>.ReadOnly shellMatrices)
        {
            return TryReadVaultBuffer(in _shellMatricesHandle, ShellMatricesBufferId, MarauderOutpostConstants.MaxShellMatrices, out shellMatrices);
        }

        private bool TryReadShellCellTypes(out NativeArray<uint>.ReadOnly shellCellTypes)
        {
            return TryReadVaultBuffer(in _shellCellTypesHandle, ShellCellTypesBufferId, 1, out shellCellTypes);
        }

        private bool TryReadInteractableSpawns(out NativeArray<OutpostInteractableSpawn>.ReadOnly interactableSpawns)
        {
            return TryReadVaultBuffer(in _interactableSpawnsHandle, InteractableSpawnsBufferId, 1, out interactableSpawns);
        }

        private bool TryReadCounters(out NativeArray<int>.ReadOnly counters)
        {
            return TryReadVaultBuffer(in _countersHandle, CountersBufferId, MarauderOutpostConstants.CounterCount, out counters);
        }

        private bool TryReadTelemetryRing(out NativeArray<OutpostTelemetryEntry>.ReadOnly telemetryRing)
        {
            return TryReadVaultBuffer(in _telemetryRingHandle, TelemetryRingBufferId, 1, out telemetryRing);
        }

        private bool TryPrepareSolveScratch(out NativeArray<byte> wfcGrid)
        {
            wfcGrid = default;
            if (!_scratchBuffers.IsSolveReady(MarauderOutpostConstants.FullCellCount))
                return false;

            wfcGrid = _scratchBuffers.SolveWfcGrid;
            return true;
        }

        private bool FlushSolveScratchToVault()
        {
            return TryFlushScratchBuffer(in _wfcGridHandle, WfcGridBufferId, _scratchBuffers.SolveWfcGrid, MarauderOutpostConstants.FullCellCount);
        }

        private void ReleaseSolveScratchBuffer()
        {
            _scratchBuffers.DisposeSolve();
        }

        private bool TryPrepareExtractionScratch(
            out NativeArray<byte> mutableGrid,
            out NativeArray<float4x4> shellMatrices,
            out NativeArray<uint> shellCellTypes,
            out NativeArray<OutpostInteractableSpawn> interactableSpawns,
            out NativeArray<int> counters)
        {
            mutableGrid = default;
            shellMatrices = default;
            shellCellTypes = default;
            interactableSpawns = default;
            counters = default;
            if (!EnsureExtractionScratchBuffers() ||
                !TryReadMutableStateGrid(out NativeArray<byte>.ReadOnly mutableSnapshot))
            {
                return false;
            }

            CopyReadOnlyToNative(mutableSnapshot, _scratchBuffers.ExtractionMutableGrid, MarauderOutpostConstants.FullCellCount);
            mutableGrid = _scratchBuffers.ExtractionMutableGrid;
            shellMatrices = _scratchBuffers.ExtractionShellMatrices;
            shellCellTypes = _scratchBuffers.ExtractionShellCellTypes;
            interactableSpawns = _scratchBuffers.ExtractionInteractableSpawns;
            counters = _scratchBuffers.ExtractionCounters;
            return true;
        }

        private bool FlushExtractionScratchToVault()
        {
            if (!_scratchBuffers.ExtractionMutableGrid.IsCreated ||
                !_scratchBuffers.ExtractionShellMatrices.IsCreated ||
                !_scratchBuffers.ExtractionShellCellTypes.IsCreated ||
                !_scratchBuffers.ExtractionInteractableSpawns.IsCreated ||
                !_scratchBuffers.ExtractionCounters.IsCreated)
            {
                return false;
            }

            return TryFlushScratchBuffer(in _wfcMutableStateGridHandle, WfcMutableStateGridBufferId, _scratchBuffers.ExtractionMutableGrid, MarauderOutpostConstants.FullCellCount) &&
                   TryFlushScratchBuffer(in _shellMatricesHandle, ShellMatricesBufferId, _scratchBuffers.ExtractionShellMatrices, MarauderOutpostConstants.MaxShellMatrices) &&
                   TryFlushScratchBuffer(in _shellCellTypesHandle, ShellCellTypesBufferId, _scratchBuffers.ExtractionShellCellTypes, MarauderOutpostConstants.MaxShellMatrices) &&
                   TryFlushScratchBuffer(in _interactableSpawnsHandle, InteractableSpawnsBufferId, _scratchBuffers.ExtractionInteractableSpawns, MarauderOutpostConstants.MaxInteractables) &&
                   TryFlushScratchBuffer(in _countersHandle, CountersBufferId, _scratchBuffers.ExtractionCounters, MarauderOutpostConstants.CounterCount);
        }

        private bool EnsureExtractionScratchBuffers()
        {
            return _scratchBuffers.IsExtractionReady();
        }

        private void ReleaseExtractionScratchBuffers()
        {
            _scratchBuffers.DisposeExtraction();
        }

        private bool TryFlushScratchBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            NativeArray<T> scratch,
            int requiredLength) where T : struct
        {
            if (!scratch.IsCreated || scratch.Length < requiredLength)
                return false;

            if (!TryAcquireWriteBuffer(in handle, bufferId, requiredLength, out NativeArray<T> target))
                return false;

            try
            {
                CopyNativeToNative(scratch, target, requiredLength);
                return true;
            }
            finally
            {
                ReleaseWriteBuffer(in handle, bufferId);
            }
        }

        private static void CopyReadOnlyToNative<T>(NativeArray<T>.ReadOnly source, NativeArray<T> destination, int count) where T : struct
        {
            int safeCount = math.min(count, math.min(source.Length, destination.Length));
            for (int i = 0; i < safeCount; i++)
                destination[i] = source[i];
        }

        private static void CopyNativeToNative<T>(NativeArray<T> source, NativeArray<T> destination, int count) where T : struct
        {
            int safeCount = math.min(count, math.min(source.Length, destination.Length));
            for (int i = 0; i < safeCount; i++)
                destination[i] = source[i];
        }

        private bool TryPrepareShiftScratch(out NativeArray<float4x4> shellMatrices)
        {
            shellMatrices = default;
            if (!_scratchBuffers.IsShiftReady(MarauderOutpostConstants.MaxShellMatrices) ||
                !TryReadFullShellMatrices(out NativeArray<float4x4>.ReadOnly sourceMatrices))
            {
                return false;
            }

            CopyReadOnlyToNative(sourceMatrices, _scratchBuffers.ShiftShellMatrices, MarauderOutpostConstants.MaxShellMatrices);
            shellMatrices = _scratchBuffers.ShiftShellMatrices;
            return true;
        }

        private bool FlushShiftScratchToVault()
        {
            return TryFlushScratchBuffer(in _shellMatricesHandle, ShellMatricesBufferId, _scratchBuffers.ShiftShellMatrices, MarauderOutpostConstants.MaxShellMatrices);
        }

        private void ReleaseShiftScratchBuffer()
        {
            _scratchBuffers.DisposeShift();
        }

        private bool TryAcquireMutableStateWriteBuffer(out NativeArray<byte> mutableStateGrid)
        {
            return TryAcquireWriteBuffer(in _wfcMutableStateGridHandle, WfcMutableStateGridBufferId, MarauderOutpostConstants.FullCellCount, out mutableStateGrid);
        }

        private void ReleaseMutableStateWriteBuffer()
        {
            ReleaseWriteBuffer(in _wfcMutableStateGridHandle, WfcMutableStateGridBufferId);
        }

        private bool TryAcquireShellMatricesWriteBuffer(out NativeArray<float4x4> shellMatrices)
        {
            return TryAcquireWriteBuffer(in _shellMatricesHandle, ShellMatricesBufferId, MarauderOutpostConstants.MaxShellMatrices, out shellMatrices);
        }

        private void ReleaseShellMatricesWriteBuffer()
        {
            ReleaseWriteBuffer(in _shellMatricesHandle, ShellMatricesBufferId);
        }

        private bool TryAcquireInteractableSpawnsWriteBuffer(out NativeArray<OutpostInteractableSpawn> interactableSpawns)
        {
            return TryAcquireWriteBuffer(in _interactableSpawnsHandle, InteractableSpawnsBufferId, MarauderOutpostConstants.MaxInteractables, out interactableSpawns);
        }

        private void ReleaseInteractableSpawnsWriteBuffer()
        {
            ReleaseWriteBuffer(in _interactableSpawnsHandle, InteractableSpawnsBufferId);
        }

        private bool TryAcquireTelemetryWriteBuffer(out NativeArray<OutpostTelemetryEntry> telemetryRing)
        {
            return TryAcquireWriteBuffer(in _telemetryRingHandle, TelemetryRingBufferId, MarauderOutpostConstants.TelemetryFrames, out telemetryRing);
        }

        private void ReleaseTelemetryWriteBuffer()
        {
            ReleaseWriteBuffer(in _telemetryRingHandle, TelemetryRingBufferId);
        }

        private void CompleteCurrentOutpostJobForTeardown()
        {
            if (_jobPhase != JobPhase.None)
            {
                DispatcherJobFence.BeginPostSimulationSwapWindow();
                try
                {
                    DispatcherJobFence.TryComplete(ref _jobHandle, forceComplete: true);
                }
                finally
                {
                    DispatcherJobFence.EndPostSimulationSwapWindow();
                }
            }

            _jobHandle = default;
            _jobPhase = JobPhase.None;
        }

        private void OnDataVaultReplaced(IDataVault nextVault)
        {
            CompleteCurrentOutpostJobForTeardown();
            ReleaseVaultBuffers();
            _dataVault = nextVault;
            ReleasePublishedPowerGrid();
            _generated = false;
            _matrixCount = 0;
            _interactableCount = 0;
            _solidCellCount = 0;
            _supportCount = 0;
            _matrixUploadDirty = false;
            SetState(OutpostGenerationState.Idle);
            if (isActiveAndEnabled && nextVault != null)
                EnsurePersistentState();
        }

        private void ReleaseVaultBuffers()
        {
            ReleaseWriteBuffer(in _telemetryRingHandle, TelemetryRingBufferId);
            ReleaseWriteBuffer(in _countersHandle, CountersBufferId);
            ReleaseWriteBuffer(in _wfcMutableStateGridHandle, WfcMutableStateGridBufferId);
            ReleaseWriteBuffer(in _interactableSpawnsHandle, InteractableSpawnsBufferId);
            ReleaseWriteBuffer(in _shellCellTypesHandle, ShellCellTypesBufferId);
            ReleaseWriteBuffer(in _shellMatricesHandle, ShellMatricesBufferId);
            ReleaseWriteBuffer(in _wfcGridHandle, WfcGridBufferId);
            ReleaseVaultHandle(ref _telemetryRingHandle);
            ReleaseVaultHandle(ref _countersHandle);
            ReleaseVaultHandle(ref _wfcMutableStateGridHandle);
            ReleaseVaultHandle(ref _interactableSpawnsHandle);
            ReleaseVaultHandle(ref _shellCellTypesHandle);
            ReleaseVaultHandle(ref _shellMatricesHandle);
            ReleaseVaultHandle(ref _wfcGridHandle);
        }

        private void ReleaseVaultHandle<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) && handle.Generation != 0u;
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

        private static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T>.ReadOnly source, int count) where T : struct
        {
            if (destination == null || !source.IsCreated)
                return;

            int safeCount = math.min(math.min(destination.count, source.Length), math.max(0, count));
            if (safeCount <= 0)
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            try
            {
                for (int i = 0; i < safeCount; i++)
                    mapped[i] = source[i];
            }
            finally
            {
                destination.UnlockBufferAfterWrite<T>(safeCount);
            }
        }
    }
}
