using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Deterministic sector ore generator with Vault DTO authority and indirect dormant matrix rendering.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralOreSpawner : MonoBehaviour, ISlowTickable, ILateFrameTickable, IDisposable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener, IWorldResourceSpawnerReadModel, IWorldResourceSpawnerCommandModel, IWorldResourceSpawnerReadDependencySink
    {
        private const string OwnerName = nameof(ProceduralOreSpawner);
        private const int DefaultOreCapacity = 2048;
        private const int MinimumOreCapacity = 64;
        private const int MaximumOreCapacity = 16384;
        private const int DefaultIterationsPerSector = 1024;
        private const int BiomeHeatmapResolution = 16;
        private const int DepletionCacheCapacity = 4096;
        private const int DepletionCacheCountLength = 1;
        private const int SectorHashGridCount = 9;
        private const int SpawnCounterCount = 7;
        private const int IndirectArgsCount = 1;
        private const uint OreProceduralVertexCount = 36u;
        private const int ClearedCandidateSlot = -1;
        private const string PrimaryTelemetryDumpFile = "Dump_SHINOBU_153.bin";
        private const string PromptTelemetryDumpFile = "Dump_GEOLOGY_ARCHITECT.bin";
        private const int CopperBiomeId = 4;
        private const float SlopeRejectNormalY = 0.5f;
        private const int OreTypeBasaltIron = WorldOreTypeIds.BasaltIron;
        private const int OreTypeCopper = WorldOreTypeIds.Copper;
        private const int OreTypeTitanium = WorldOreTypeIds.Titanium;
        private const int OreTypeSilver = WorldOreTypeIds.Silver;
        private const float NearDropPodDistanceSq = 2500f;
        private const float FarDropPodDistanceSq = 10000f;
        private const float DropPodBandInvDistanceSq = 1f / (FarDropPodDistanceSq - NearDropPodDistanceSq);
        private const float CopperClumpDistanceSq = 4f;
        private const int CopperClumpBiasPercent = 85;
        private const SystemID OwnerSystemId = SystemID.WorldResourceSpawnerRuntime;
        private const float OreProceduralLocalExtentX = 0.34f;
        private const float OreProceduralLocalExtentY = 0.34f;
        private const float OreProceduralLocalExtentZ = 0.82f;
        private static readonly int _OreMatricesId = Shader.PropertyToID("_OreMatrices");
        private static readonly int _QualityOverkillId = Shader.PropertyToID("_QualityOverkill");

        [Header("Generation")]
        [SerializeField, Tooltip("Maximum deterministic ore slots retained for the active sector."), Min(MinimumOreCapacity)] private int maxOreCapacity = DefaultOreCapacity;
        [SerializeField, Tooltip("LCG candidate budget before quality-tier scaling."), Min(1)] private int iterationsPerSector = DefaultIterationsPerSector;
        [SerializeField, Tooltip("AUP sector width used for stable ore hashing."), Min(16f)] private float sectorSizeMeters = 128f;
        [SerializeField, Tooltip("Project seed mixed into every sector hash.")] private uint worldSeed = 0x48454338u;
        [SerializeField, Tooltip("Continuous visual-only cluster density scale. Core gameplay nodes are not quality-gated.")] private float visualClusterDensity = 1f;
        [SerializeField, Tooltip("Spread radius for visual-only crystal clusters around each authoritative node.")] private float clusterSpreadRadius = 0.85f;
        [SerializeField, Tooltip("Minimum accepted terrain normal Y for stable resource grounding.")] private float normalAlignmentTolerance = SlopeRejectNormalY;

        [Header("Runtime References")]
        [SerializeField, Tooltip("Optional cached player transform; auto-resolved through the player runtime context if empty.")] private Transform playerTransform;
        [SerializeField, Tooltip("Optional cached MapMagic bridge for terrain height and biome sampling.")] private MapMagicBridge mapMagicBridge;

        [Header("Rendering")]
        [SerializeField, Tooltip("Legacy mesh reference retained for scene compatibility; procedural ore rendering expands vertices in shader.")] private Mesh oreMesh;
        [SerializeField, Tooltip("Procedural ore material with a StructuredBuffer named _OreMatrices and SV_VertexID expansion.")] private Material oreMaterial;
        [SerializeField, Tooltip("Shadow mode used by dormant ore indirect draws.")] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;
        [SerializeField, Tooltip("Whether dormant ore indirect draws receive shadows.")] private bool receiveShadows = true;
        [SerializeField, Tooltip("Continuous presentation weight for dormant ore indirect draws. Set 0 only for editor diagnostics; gameplay truth is unchanged."), Range(0f, 1f)] private float dormantOreVisualWeight = 1f;
#if UNITY_EDITOR
        [SerializeField, Tooltip("Editor-only x-ray of Vault-backed procedural geology matrices.")] private bool drawProceduralGeologyGizmos;
#endif

        [Header("Yield Hashes")]
        [SerializeField, Tooltip("Inventory item hash emitted for basalt iron ore yields.")] private int basaltIronItemHash;
        [SerializeField, Tooltip("Inventory item hash emitted for copper ore yields.")] private int copperItemHash;
        [SerializeField, Tooltip("Inventory item hash emitted for titanium ore yields.")] private int titaniumItemHash;
        [SerializeField, Tooltip("Inventory item hash emitted for silver ore yields.")] private int silverItemHash;

        private IDataVault _dataVault;
        private ITerrainProvider _terrainProvider;
        private IPlayerRuntimeContext _playerContext;
        private VaultGenerationHandle<ResourceNodeDTO> _resourceNodesHandle;
        private VaultGenerationHandle<float3> _orePositionsHandle;
        private VaultGenerationHandle<int> _oreTypesHandle;
        private VaultGenerationHandle<ulong> _depletionMasksHandle;
        private VaultGenerationHandle<float4x4> _oreMatricesHandle;
        private VaultGenerationHandle<byte> _biomeHeatmapHandle;
        private VaultGenerationHandle<int> _spawnCountsHandle;
        private VaultGenerationHandle<GeologyGenerationTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<GeologyTerrainSampleDTO> _mockTerrainSdfHandle;
        private VaultGenerationHandle<GeologyDistributionRuleDTO> _distributionRulesHandle;
        private VaultGenerationHandle<GeologyTuningDTO> _tuningHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<GeologySelfAuditResultDTO> _selfAuditHandle;
        private VaultGenerationHandle<int> _candidateSlotsHandle;
        private VaultGenerationHandle<ulong> _depletionCacheKeysHandle;
        private VaultGenerationHandle<ulong> _depletionCacheMasksHandle;
        private VaultGenerationHandle<int> _depletionCacheCountHandle;
        private VaultGenerationHandle<long> _sectorHashGridHandle;
        private VaultGenerationHandle<GeologyIndirectArgsDTO> _indirectArgsHandle;
        private VaultGenerationHandle<GeologyHzbTileDTO> _hzbTilesHandle;
        private VaultGenerationHandle<GeologyHzbMetaDTO> _hzbMetaHandle;

        private GraphicsBuffer _matrixBufferA;
        private GraphicsBuffer _matrixBufferB;
        private GraphicsBuffer _argsBuffer;
        private GraphicsBuffer _activeMatrixBuffer;

        private JobHandle _spawnJob;
        private JobHandle _pendingOreReadDependency;
        private bool _spawnJobScheduled;
        private bool _pendingOreReadDependencyValid;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;
        private bool _renderUploadDirty;
        private float _lastAppliedDormantOreVisualWeight = -1f;
        private bool _depletionLoaded;
        private bool _discardSpawnJobOutput;
        private bool _pendingDisableSpawnDrain;
        private bool _hotSwapRegistered;
        private bool _pendingDataVaultRebind;
        private bool _distributionRulesLoaded;
        private int _lockedVaultBufferMask;
        private int _oreCapacity;
        private int _depletionWordCount;
        private int _renderInstanceCount;
        private int _activeOreCount;
        private int _telemetryWriteIndex;
        private int _depletedCullCount;
        private int _visualOnlyNodeCount;
        private int _overflowCount;
        private int _hzbCullCount;
        private int _distributionRuleCount;
        private uint _lastTelemetryFrameWritten;
        private uint _simulationFrameCounter;
        private int2 _currentSector;
        private long _currentSectorHash;
        private int _currentBiomeId;
        private Bounds _drawBounds;
        private float3 _pendingRuntimeShift;
        private float3 _lastPlayerRuntimePosition;
        private bool _hasPendingRuntimeShift;
        private bool _hasPlayerRuntimePosition;
        private uint _lastAppliedAupShiftFrameId;
        private AbsoluteUniversePosition _dropPodAup;
        private float3 _dropPodRuntimePosition;
        private uint _lastDropPodSignalFrame;
        private bool _hasDropPodAnchor;
        private bool _dropPodAnchorFromSignal;
        private bool _dropPodAnchorRequiresGenerationRefresh;
        private int _localTitaniumCount;
        private float3 _telemetryFirstOrePosition;
        private uint _telemetryFirstNodeHash;

        private bool _depletionCacheInitialized;
        private IDataVault _pendingDataVault;

        private struct ProceduralGeologyVaultViews
        {
            public NativeArray<ResourceNodeDTO> ResourceNodes;
            public NativeArray<float3> OrePositions;
            public NativeArray<int> OreTypes;
            public NativeArray<ulong> DepletionMasks;
            public NativeArray<float4x4> OreMatrices;
            public NativeArray<byte> BiomeHeatmap;
            public NativeArray<int> SpawnCounts;
            public NativeArray<GeologyGenerationTelemetryEntry> TelemetryRing;
            public NativeArray<GeologyTerrainSampleDTO> MockTerrainSdf;
            public NativeArray<GeologyDistributionRuleDTO> DistributionRules;
            public NativeArray<GeologyTuningDTO> GeologyTuning;
            public NativeArray<byte> CsvScratch;
            public NativeArray<GeologySelfAuditResultDTO> SelfAudit;
            public NativeArray<int> CandidateSlots;
            public NativeArray<ulong> DepletionCacheKeys;
            public NativeArray<ulong> DepletionCacheMasks;
            public NativeArray<int> DepletionCacheCount;
            public NativeArray<long> SectorHashGrid;
            public NativeArray<GeologyIndirectArgsDTO> IndirectArgs;
            public NativeArray<GeologyHzbTileDTO> HzbTiles;
            public NativeArray<GeologyHzbMetaDTO> HzbMeta;

            public readonly bool IsCreated()
            {
                return ResourceNodes.IsCreated &&
                       OrePositions.IsCreated &&
                       OreTypes.IsCreated &&
                       DepletionMasks.IsCreated &&
                       OreMatrices.IsCreated &&
                       BiomeHeatmap.IsCreated &&
                       SpawnCounts.IsCreated &&
                       TelemetryRing.IsCreated &&
                       MockTerrainSdf.IsCreated &&
                       DistributionRules.IsCreated &&
                       GeologyTuning.IsCreated &&
                       CsvScratch.IsCreated &&
                       SelfAudit.IsCreated &&
                       CandidateSlots.IsCreated &&
                       DepletionCacheKeys.IsCreated &&
                       DepletionCacheMasks.IsCreated &&
                       DepletionCacheCount.IsCreated &&
                       SectorHashGrid.IsCreated &&
                       IndirectArgs.IsCreated &&
                       HzbTiles.IsCreated &&
                       HzbMeta.IsCreated;
            }
        }

        private struct GeologyHeightPayloadView
        {
            public NativeArray<ushort> HeightSamples;
            public float3 TerrainSize;
            public double2 TerrainOriginAbsoluteXZ;
            public float TerrainBaseY;
            public int HeightResolution;
            public byte HasQuantizedPayload;
        }

        /// <summary>Number of non-depleted ore slots currently alive in the active sector.</summary>
        public int ActiveOreCount => _activeOreCount;
        public int LocalTitaniumCount => _localTitaniumCount;
        /// <summary>Stable hash for the currently loaded AUP sector.</summary>
        public long CurrentSectorHash => _currentSectorHash;

        public bool TryGetOrePositionsReadOnly(out NativeArray<float3>.ReadOnly orePositions, out int scanCount)
        {
            if (!CanExposeReadSnapshot())
            {
                orePositions = default;
                scanCount = 0;
                return false;
            }

            if (!TryReadExistingBuffer(in _orePositionsHandle, _oreCapacity, out NativeArray<float3> writableOrePositions))
            {
                orePositions = default;
                scanCount = 0;
                return false;
            }

            orePositions = writableOrePositions.AsReadOnly();
            scanCount = _renderInstanceCount;
            return _renderInstanceCount > 0 && _activeOreCount > 0;
        }

        public bool TryGetOreTypesReadOnly(out NativeArray<int>.ReadOnly oreTypes, out int scanCount)
        {
            if (!CanExposeReadSnapshot())
            {
                oreTypes = default;
                scanCount = 0;
                return false;
            }

            if (!TryReadExistingBuffer(in _oreTypesHandle, _oreCapacity, out NativeArray<int> writableOreTypes))
            {
                oreTypes = default;
                scanCount = 0;
                return false;
            }

            oreTypes = writableOreTypes.AsReadOnly();
            scanCount = _renderInstanceCount;
            return _renderInstanceCount > 0 && _activeOreCount > 0;
        }

        public void RegisterOreReadDependency(JobHandle readDependency)
        {
            TryFinalizeCompletedOreReadDependency();
            if (readDependency.IsCompleted)
            {
                DispatcherJobFence.TryFinalizeCompleted(ref readDependency);
                return;
            }

            _pendingOreReadDependency = _pendingOreReadDependencyValid
                ? JobHandle.CombineDependencies(_pendingOreReadDependency, readDependency)
                : readDependency;
            _pendingOreReadDependencyValid = true;
        }

        public bool TryMarkOreDepleted(int oreIndex, out uint oreHash, out uint itemHash, out float3 depletedPosition)
        {
            oreHash = 0u;
            itemHash = 0u;
            depletedPosition = default;
            if (_spawnJobScheduled || !EnsureNativeState())
                return false;

            if (!TryLockVaultDepletionBuffers())
                return false;

            try
            {
                if (!TryOpenExistingDepletionViews(out ProceduralGeologyVaultViews views))
                    return false;

                if ((uint)oreIndex >= (uint)_renderInstanceCount ||
                    !views.ResourceNodes.IsCreated ||
                    !views.OreTypes.IsCreated ||
                    !views.OrePositions.IsCreated ||
                    !views.CandidateSlots.IsCreated ||
                    !views.DepletionMasks.IsCreated ||
                    (uint)oreIndex >= (uint)views.ResourceNodes.Length ||
                    (uint)oreIndex >= (uint)views.OreTypes.Length ||
                    (uint)oreIndex >= (uint)views.OrePositions.Length ||
                    views.OreTypes[oreIndex] == 0)
                {
                    return false;
                }

                int deterministicSlot = (uint)oreIndex < (uint)views.CandidateSlots.Length
                    ? views.CandidateSlots[oreIndex]
                    : oreIndex;
                if (deterministicSlot < 0)
                    return false;

                int oreType = views.OreTypes[oreIndex];
                oreHash = ComputeOreHash(_currentSectorHash, deterministicSlot);
                itemHash = unchecked((uint)ResolveItemHash(oreType));
                depletedPosition = views.OrePositions[oreIndex];
                if (MarkDepleted(views, oreIndex))
                    return true;

                oreHash = 0u;
                itemHash = 0u;
                depletedPosition = default;
                return false;
            }
            finally
            {
                UnlockVaultWriteBuffers();
            }
        }

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            if (!AllocateNativeState())
                return;
            EnsureRenderBuffers();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            _pendingDisableSpawnDrain = false;
            TryRegisterHotSwapDependency();
            CacheRuntimeServices();

            if (_dataVault == null && !AllocateNativeState())
            {
                UnregisterHotSwapDependency();
                return;
            }

            if (!EnsureNativeState())
            {
                UnregisterHotSwapDependency();
                return;
            }

            EnsureRenderBuffers();

            GlobalRegistry.RegisterWorldResourceSpawner(this);

            if (!_slowTickRegistered)
                _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void OnDisable()
        {
            if (ReferenceEquals(GlobalRegistry.WorldResourceSpawner, this))
                GlobalRegistry.UnregisterWorldResourceSpawner(this);

            UnregisterSlowTickDispatcher();
            if (_spawnJobScheduled)
            {
                CompleteSpawnJobForDisable();
            }

            _pendingDisableSpawnDrain = false;
            UnregisterLateFrameDispatcher();
            UnregisterHotSwapDependency();
            ClearDisabledPresentationState();
        }

        private void UnregisterDispatchers()
        {
            UnregisterSlowTickDispatcher();
            UnregisterLateFrameDispatcher();
        }

        private void UnregisterSlowTickDispatcher()
        {
            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }
        }

        private void UnregisterLateFrameDispatcher()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }
        }

        private void TryRegisterHotSwapDependency()
        {
            if (!_hotSwapRegistered)
                _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void UnregisterHotSwapDependency()
        {
            if (_hotSwapRegistered)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _hotSwapRegistered = false;
            }
        }

        private void ClearPendingDataVaultRebind()
        {
            _pendingDataVaultRebind = false;
            _pendingDataVault = null;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void OnValidate()
        {
            maxOreCapacity = Mathf.Clamp(maxOreCapacity, MinimumOreCapacity, MaximumOreCapacity);
            iterationsPerSector = Mathf.Max(1, iterationsPerSector);
            sectorSizeMeters = Mathf.Max(16f, sectorSizeMeters);
            visualClusterDensity = Mathf.Clamp01(visualClusterDensity);
            clusterSpreadRadius = Mathf.Max(0.05f, clusterSpreadRadius);
            normalAlignmentTolerance = Mathf.Clamp(normalAlignmentTolerance, 0.05f, 1f);
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            QueueRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            QueueRegistryServiceRebind(serviceSlot, currentService);
        }

        private void QueueRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                if (_playerContext == null)
                    playerTransform = null;
                else
                    RefreshCachedPlayerRuntimeReference();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.TerrainProviderRuntime)
            {
                _terrainProvider = currentService as ITerrainProvider;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.MapMagicRuntime)
            {
                MapMagicBridge previousBridge = mapMagicBridge;
                mapMagicBridge = currentService as MapMagicBridge;
                if (_terrainProvider == null || ReferenceEquals(_terrainProvider, previousBridge))
                    _terrainProvider = mapMagicBridge;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault nextVault = currentService as IDataVault;
            if (ReferenceEquals(_dataVault, nextVault))
            {
                ClearPendingDataVaultRebind();
                return;
            }

            _pendingDataVault = nextVault;
            _pendingDataVaultRebind = true;
        }

        /// <summary>Slow-tick sector scan, terrain projection refresh, and AUP drift drain.</summary>
        public void SlowTick()
        {
            TryFinalizeCompletedOreReadDependency();
            if (!EnsureNativeState())
                return;

            if (!DrainAupShiftSignals())
                return;

            DrainDropPodLandingSignals();

            if (!RefreshCachedPlayerRuntimeReference())
                return;

            RefreshSectorAndTerrain();
            if (_spawnJobScheduled)
                return;

            WriteTelemetrySample(0u);
        }

        /// <summary>Late-frame job retirement, matrix upload, and dormant ore indirect draw.</summary>
        public void LateFrameTick()
        {
            TryFinalizeCompletedOreReadDependency();
            if (_pendingDisableSpawnDrain)
            {
                if (!_spawnJobScheduled)
                {
                    _pendingDisableSpawnDrain = false;
                    return;
                }

                if (TryCompleteFinishedSpawnJob())
                {
                    DiscardSpawnJobOutput();
                    _pendingDisableSpawnDrain = false;
                }

                return;
            }

            if (!EnsureNativeState())
                return;

            if (!DrainAupShiftSignals())
                return;

            DrainDropPodLandingSignals();

            if (TryCompleteFinishedSpawnJob())
            {
                if (_discardSpawnJobOutput)
                    DiscardSpawnJobOutput();
                else
                    CommitSpawnJobOutput();
            }

            if (_renderUploadDirty)
                UploadRenderMatrices();

            RenderDormantOres();
            RefreshCachedPlayerRuntimeReference();
            WriteTelemetrySample(0u);
        }

        /// <summary>Fences scheduled generation, releases graphics buffers, and drops Vault views.</summary>
        public void Dispose()
        {
            UnregisterDispatchers();
            UnregisterHotSwapDependency();
            if (_spawnJobScheduled)
            {
                DispatcherJobFence.TryComplete(ref _spawnJob, forceComplete: true);
                _spawnJobScheduled = false;
                UnlockVaultWriteBuffers();
            }

            CompletePendingOreReadDependencyForTeardown();
            ReleaseVaultViews();
            ClearPendingDataVaultRebind();

            ReleaseBuffer(ref _matrixBufferA);
            ReleaseBuffer(ref _matrixBufferB);
            ReleaseBuffer(ref _argsBuffer);
            _activeMatrixBuffer = null;
            _pendingRuntimeShift = default;
            _lastPlayerRuntimePosition = default;
            _hasPendingRuntimeShift = false;
            _hasPlayerRuntimePosition = false;
            _lastAppliedAupShiftFrameId = 0u;
            _dropPodAup = default;
            _dropPodRuntimePosition = default;
            _lastDropPodSignalFrame = 0u;
            _hasDropPodAnchor = false;
            _dropPodAnchorFromSignal = false;
            _dropPodAnchorRequiresGenerationRefresh = false;
            _localTitaniumCount = 0;
            _telemetryFirstOrePosition = default;
            _telemetryFirstNodeHash = 0u;
            _lastTelemetryFrameWritten = 0u;
            _simulationFrameCounter = 0u;
            _discardSpawnJobOutput = false;
            _pendingDisableSpawnDrain = false;
        }

        private void CompleteSpawnJobForDisable()
        {
            _discardSpawnJobOutput = true;
            if (!TryCompleteFinishedSpawnJob())
            {
                // [BLOCKING_SYNC_POINT] Lifecycle teardown only. Disabled ore spawners must not keep Vault rows locked or remain registered for hot ticks.
                DispatcherJobFence.TryComplete(ref _spawnJob, forceComplete: true);
                _spawnJobScheduled = false;
                UnlockVaultWriteBuffers();
            }

            DiscardSpawnJobOutput();
        }

        private void CacheRuntimeServices()
        {
            if (_playerContext == null)
                _playerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;

            RefreshCachedPlayerRuntimeReference();

            if (_terrainProvider == null)
                _terrainProvider = GlobalRegistry.Terrain;

            if (mapMagicBridge == null)
                mapMagicBridge = GlobalRegistry.MapMagic;

            if (_terrainProvider == null && mapMagicBridge != null)
                _terrainProvider = mapMagicBridge;
        }

        private bool RefreshCachedPlayerRuntimeReference()
        {
            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext == null)
                return false;

            Transform runtimeTransform = playerContext.PlayerTransform;
            if (runtimeTransform != null)
                playerTransform = runtimeTransform;

            bool capturedPose = TryCapturePlayerPose(
                playerContext,
                out AbsoluteUniversePosition _,
                out float3 runtimePosition);
            if (capturedPose)
                StorePlayerRuntimePosition(runtimePosition);

            return capturedPose || runtimeTransform != null;
        }

        private bool AllocateNativeState()
        {
            return AllocateNativeState(GlobalRegistry.DataVault);
        }

        private bool AllocateNativeState(IDataVault vault)
        {
            if (!ProceduralGeologyLayoutAudit.Validate())
                return false;

            if (vault == null)
                return false;

            bool vaultChanged = !ReferenceEquals(_dataVault, vault);
            _dataVault = vault;
            if (vaultChanged)
            {
                _depletionCacheInitialized = false;
                _distributionRulesLoaded = false;
            }
            _oreCapacity = Mathf.Clamp(maxOreCapacity, MinimumOreCapacity, MaximumOreCapacity);
            _depletionWordCount = Mathf.Max(1, (_oreCapacity + 63) >> 6);

            _resourceNodesHandle = vault.GetGenerationHandle<ResourceNodeDTO>(
                ProceduralGeologyVaultBufferIds.ResourceNodes,
                _oreCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _orePositionsHandle = vault.GetGenerationHandle<float3>(
                ProceduralGeologyVaultBufferIds.OrePositions,
                _oreCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _oreTypesHandle = vault.GetGenerationHandle<int>(
                ProceduralGeologyVaultBufferIds.OreTypes,
                _oreCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _depletionMasksHandle = vault.GetGenerationHandle<ulong>(
                ProceduralGeologyVaultBufferIds.DepletionMasks,
                _depletionWordCount,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _oreMatricesHandle = vault.GetGenerationHandle<float4x4>(
                ProceduralGeologyVaultBufferIds.ResourceMatrices,
                _oreCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _biomeHeatmapHandle = vault.GetGenerationHandle<byte>(
                ProceduralGeologyVaultBufferIds.BiomeHeatmap,
                BiomeHeatmapResolution * BiomeHeatmapResolution,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _spawnCountsHandle = vault.GetGenerationHandle<int>(
                ProceduralGeologyVaultBufferIds.SpawnCounts,
                SpawnCounterCount,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _telemetryRingHandle = vault.GetGenerationHandle<GeologyGenerationTelemetryEntry>(
                ProceduralGeologyVaultBufferIds.TelemetryRing,
                ProceduralGeologyConstants.TelemetryFrames,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _mockTerrainSdfHandle = vault.GetGenerationHandle<GeologyTerrainSampleDTO>(
                ProceduralGeologyVaultBufferIds.MockTerrainSdf,
                ProceduralGeologyConstants.MockTerrainResolution * ProceduralGeologyConstants.MockTerrainResolution,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _distributionRulesHandle = vault.GetGenerationHandle<GeologyDistributionRuleDTO>(
                ProceduralGeologyVaultBufferIds.DistributionRules,
                ProceduralGeologyConstants.DistributionRuleCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.GetGenerationHandle<GeologyTuningDTO>(
                ProceduralGeologyVaultBufferIds.Tuning,
                ProceduralGeologyConstants.TuningCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _csvScratchHandle = vault.GetGenerationHandle<byte>(
                ProceduralGeologyVaultBufferIds.CsvScratch,
                ProceduralGeologyConstants.CsvScratchBytes,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _selfAuditHandle = vault.GetGenerationHandle<GeologySelfAuditResultDTO>(
                ProceduralGeologyVaultBufferIds.SelfAudit,
                ProceduralGeologyConstants.SelfAuditCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _candidateSlotsHandle = vault.GetGenerationHandle<int>(
                ProceduralGeologyVaultBufferIds.CandidateSlots,
                _oreCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _depletionCacheKeysHandle = vault.GetGenerationHandle<ulong>(
                ProceduralGeologyVaultBufferIds.DepletionCacheKeys,
                DepletionCacheCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _depletionCacheMasksHandle = vault.GetGenerationHandle<ulong>(
                ProceduralGeologyVaultBufferIds.DepletionCacheMasks,
                DepletionCacheCapacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _depletionCacheCountHandle = vault.GetGenerationHandle<int>(
                ProceduralGeologyVaultBufferIds.DepletionCacheCount,
                DepletionCacheCountLength,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _sectorHashGridHandle = vault.GetGenerationHandle<long>(
                ProceduralGeologyVaultBufferIds.SectorHashGrid,
                SectorHashGridCount,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _indirectArgsHandle = vault.GetGenerationHandle<GeologyIndirectArgsDTO>(
                ProceduralGeologyVaultBufferIds.IndirectArgs,
                IndirectArgsCount,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            _hzbTilesHandle = vault.GetGenerationHandle<GeologyHzbTileDTO>(
                ProceduralGeologyVaultBufferIds.HzbTiles,
                ProceduralGeologyConstants.HzbTileCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _hzbMetaHandle = vault.GetGenerationHandle<GeologyHzbMetaDTO>(
                ProceduralGeologyVaultBufferIds.HzbMeta,
                ProceduralGeologyConstants.HzbMetaCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);

            if (!AcquireVaultViews(vault, out ProceduralGeologyVaultViews views))
                return false;

            EnsureDepletionCacheInitialized(views);

            for (int i = 0; i < _depletionWordCount; i++)
                views.DepletionMasks[i] = ulong.MaxValue;

            EnsureGeologyTuning(views);
            LoadDistributionRules(views);
            RunSelfAudit(views);
            return true;
        }

        private bool EnsureNativeState()
        {
            if (_pendingDataVaultRebind)
                return TryApplyPendingDataVaultRebind();

            if (_dataVault == null)
                return false;

            return AreVaultHandlesCreated() || AllocateNativeState(_dataVault);
        }

        private bool TryApplyPendingDataVaultRebind()
        {
            IDataVault pendingVault = _pendingDataVault;
            if (ReferenceEquals(_dataVault, pendingVault))
            {
                if (pendingVault == null)
                {
                    ClearPendingDataVaultRebind();
                    return false;
                }

                if (!AreVaultHandlesCreated() && !AllocateNativeState(pendingVault))
                    return false;

                ClearPendingDataVaultRebind();
                return true;
            }

            if (!TryRebindDataVault(pendingVault))
                return false;

            ClearPendingDataVaultRebind();
            return pendingVault != null && AreVaultHandlesCreated();
        }

        private bool TryRebindDataVault(IDataVault currentVault)
        {
            if (_spawnJobScheduled)
            {
                _discardSpawnJobOutput = true;
                if (!TryCompleteFinishedSpawnJob())
                    return false;

                DiscardSpawnJobOutput();
            }

            if (HasPendingOreReadDependency())
                return false;

            ClearPresentationState(true, false);
            ReleaseVaultViews();
            if (currentVault == null)
            {
                WriteIndirectArgsGpu(0u);
                return true;
            }

            if (!AllocateNativeState(currentVault))
                return false;

            UpdateIndirectArgsBuffer(0u);
            return true;
        }

        private bool AreVaultHandlesCreated()
        {
            return IsVaultHandleCreated(in _resourceNodesHandle) &&
                   IsVaultHandleCreated(in _orePositionsHandle) &&
                   IsVaultHandleCreated(in _oreTypesHandle) &&
                   IsVaultHandleCreated(in _depletionMasksHandle) &&
                   IsVaultHandleCreated(in _oreMatricesHandle) &&
                   IsVaultHandleCreated(in _biomeHeatmapHandle) &&
                   IsVaultHandleCreated(in _spawnCountsHandle) &&
                   IsVaultHandleCreated(in _telemetryRingHandle) &&
                   IsVaultHandleCreated(in _mockTerrainSdfHandle) &&
                   IsVaultHandleCreated(in _distributionRulesHandle) &&
                   IsVaultHandleCreated(in _tuningHandle) &&
                   IsVaultHandleCreated(in _csvScratchHandle) &&
                   IsVaultHandleCreated(in _selfAuditHandle) &&
                   IsVaultHandleCreated(in _candidateSlotsHandle) &&
                   IsVaultHandleCreated(in _depletionCacheKeysHandle) &&
                   IsVaultHandleCreated(in _depletionCacheMasksHandle) &&
                   IsVaultHandleCreated(in _depletionCacheCountHandle) &&
                   IsVaultHandleCreated(in _sectorHashGridHandle) &&
                   IsVaultHandleCreated(in _indirectArgsHandle) &&
                   IsVaultHandleCreated(in _hzbTilesHandle) &&
                   IsVaultHandleCreated(in _hzbMetaHandle);
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private bool AcquireVaultViews(IDataVault vault, out ProceduralGeologyVaultViews views)
        {
            views = default;
            if (vault == null)
                return false;

            return AcquireBuffer(vault, ref _resourceNodesHandle, ProceduralGeologyVaultBufferIds.ResourceNodes, _oreCapacity, NativeArrayOptions.UninitializedMemory, out views.ResourceNodes) &&
                   AcquireBuffer(vault, ref _orePositionsHandle, ProceduralGeologyVaultBufferIds.OrePositions, _oreCapacity, NativeArrayOptions.UninitializedMemory, out views.OrePositions) &&
                   AcquireBuffer(vault, ref _oreTypesHandle, ProceduralGeologyVaultBufferIds.OreTypes, _oreCapacity, NativeArrayOptions.UninitializedMemory, out views.OreTypes) &&
                   AcquireBuffer(vault, ref _depletionMasksHandle, ProceduralGeologyVaultBufferIds.DepletionMasks, _depletionWordCount, NativeArrayOptions.UninitializedMemory, out views.DepletionMasks) &&
                   AcquireBuffer(vault, ref _oreMatricesHandle, ProceduralGeologyVaultBufferIds.ResourceMatrices, _oreCapacity, NativeArrayOptions.UninitializedMemory, out views.OreMatrices) &&
                   AcquireBuffer(vault, ref _biomeHeatmapHandle, ProceduralGeologyVaultBufferIds.BiomeHeatmap, BiomeHeatmapResolution * BiomeHeatmapResolution, NativeArrayOptions.UninitializedMemory, out views.BiomeHeatmap) &&
                   AcquireBuffer(vault, ref _spawnCountsHandle, ProceduralGeologyVaultBufferIds.SpawnCounts, SpawnCounterCount, NativeArrayOptions.ClearMemory, out views.SpawnCounts) &&
                   AcquireBuffer(vault, ref _telemetryRingHandle, ProceduralGeologyVaultBufferIds.TelemetryRing, ProceduralGeologyConstants.TelemetryFrames, NativeArrayOptions.ClearMemory, out views.TelemetryRing) &&
                   AcquireBuffer(vault, ref _mockTerrainSdfHandle, ProceduralGeologyVaultBufferIds.MockTerrainSdf, ProceduralGeologyConstants.MockTerrainResolution * ProceduralGeologyConstants.MockTerrainResolution, NativeArrayOptions.UninitializedMemory, out views.MockTerrainSdf) &&
                   AcquireBuffer(vault, ref _distributionRulesHandle, ProceduralGeologyVaultBufferIds.DistributionRules, ProceduralGeologyConstants.DistributionRuleCapacity, NativeArrayOptions.UninitializedMemory, out views.DistributionRules) &&
                   AcquireBuffer(vault, ref _tuningHandle, ProceduralGeologyVaultBufferIds.Tuning, ProceduralGeologyConstants.TuningCapacity, NativeArrayOptions.ClearMemory, out views.GeologyTuning) &&
                   AcquireBuffer(vault, ref _csvScratchHandle, ProceduralGeologyVaultBufferIds.CsvScratch, ProceduralGeologyConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory, out views.CsvScratch) &&
                   AcquireBuffer(vault, ref _selfAuditHandle, ProceduralGeologyVaultBufferIds.SelfAudit, ProceduralGeologyConstants.SelfAuditCapacity, NativeArrayOptions.ClearMemory, out views.SelfAudit) &&
                   AcquireBuffer(vault, ref _candidateSlotsHandle, ProceduralGeologyVaultBufferIds.CandidateSlots, _oreCapacity, NativeArrayOptions.UninitializedMemory, out views.CandidateSlots) &&
                   AcquireBuffer(vault, ref _depletionCacheKeysHandle, ProceduralGeologyVaultBufferIds.DepletionCacheKeys, DepletionCacheCapacity, NativeArrayOptions.UninitializedMemory, out views.DepletionCacheKeys) &&
                   AcquireBuffer(vault, ref _depletionCacheMasksHandle, ProceduralGeologyVaultBufferIds.DepletionCacheMasks, DepletionCacheCapacity, NativeArrayOptions.UninitializedMemory, out views.DepletionCacheMasks) &&
                   AcquireBuffer(vault, ref _depletionCacheCountHandle, ProceduralGeologyVaultBufferIds.DepletionCacheCount, DepletionCacheCountLength, NativeArrayOptions.ClearMemory, out views.DepletionCacheCount) &&
                   AcquireBuffer(vault, ref _sectorHashGridHandle, ProceduralGeologyVaultBufferIds.SectorHashGrid, SectorHashGridCount, NativeArrayOptions.UninitializedMemory, out views.SectorHashGrid) &&
                   AcquireBuffer(vault, ref _indirectArgsHandle, ProceduralGeologyVaultBufferIds.IndirectArgs, IndirectArgsCount, NativeArrayOptions.UninitializedMemory, out views.IndirectArgs) &&
                   AcquireBuffer(vault, ref _hzbTilesHandle, ProceduralGeologyVaultBufferIds.HzbTiles, ProceduralGeologyConstants.HzbTileCapacity, NativeArrayOptions.ClearMemory, out views.HzbTiles) &&
                   AcquireBuffer(vault, ref _hzbMetaHandle, ProceduralGeologyVaultBufferIds.HzbMeta, ProceduralGeologyConstants.HzbMetaCapacity, NativeArrayOptions.ClearMemory, out views.HzbMeta) &&
                   views.IsCreated();
        }

        private bool TryOpenExistingVaultViews(out ProceduralGeologyVaultViews views)
        {
            views = default;
            if (_dataVault == null)
                return false;

            return TryOpenExistingBuffer(in _resourceNodesHandle, _oreCapacity, out views.ResourceNodes) &&
                   TryOpenExistingBuffer(in _orePositionsHandle, _oreCapacity, out views.OrePositions) &&
                   TryOpenExistingBuffer(in _oreTypesHandle, _oreCapacity, out views.OreTypes) &&
                   TryOpenExistingBuffer(in _depletionMasksHandle, _depletionWordCount, out views.DepletionMasks) &&
                   TryOpenExistingBuffer(in _oreMatricesHandle, _oreCapacity, out views.OreMatrices) &&
                   TryOpenExistingBuffer(in _biomeHeatmapHandle, BiomeHeatmapResolution * BiomeHeatmapResolution, out views.BiomeHeatmap) &&
                   TryOpenExistingBuffer(in _spawnCountsHandle, SpawnCounterCount, out views.SpawnCounts) &&
                   TryOpenExistingBuffer(in _telemetryRingHandle, ProceduralGeologyConstants.TelemetryFrames, out views.TelemetryRing) &&
                   TryOpenExistingBuffer(in _mockTerrainSdfHandle, ProceduralGeologyConstants.MockTerrainResolution * ProceduralGeologyConstants.MockTerrainResolution, out views.MockTerrainSdf) &&
                   TryOpenExistingBuffer(in _distributionRulesHandle, ProceduralGeologyConstants.DistributionRuleCapacity, out views.DistributionRules) &&
                   TryOpenExistingBuffer(in _tuningHandle, ProceduralGeologyConstants.TuningCapacity, out views.GeologyTuning) &&
                   TryOpenExistingBuffer(in _csvScratchHandle, ProceduralGeologyConstants.CsvScratchBytes, out views.CsvScratch) &&
                   TryOpenExistingBuffer(in _selfAuditHandle, ProceduralGeologyConstants.SelfAuditCapacity, out views.SelfAudit) &&
                   TryOpenExistingBuffer(in _candidateSlotsHandle, _oreCapacity, out views.CandidateSlots) &&
                   TryOpenExistingBuffer(in _depletionCacheKeysHandle, DepletionCacheCapacity, out views.DepletionCacheKeys) &&
                   TryOpenExistingBuffer(in _depletionCacheMasksHandle, DepletionCacheCapacity, out views.DepletionCacheMasks) &&
                   TryOpenExistingBuffer(in _depletionCacheCountHandle, DepletionCacheCountLength, out views.DepletionCacheCount) &&
                   TryOpenExistingBuffer(in _sectorHashGridHandle, SectorHashGridCount, out views.SectorHashGrid) &&
                   TryOpenExistingBuffer(in _indirectArgsHandle, IndirectArgsCount, out views.IndirectArgs) &&
                   TryOpenExistingBuffer(in _hzbTilesHandle, ProceduralGeologyConstants.HzbTileCapacity, out views.HzbTiles) &&
                   TryOpenExistingBuffer(in _hzbMetaHandle, ProceduralGeologyConstants.HzbMetaCapacity, out views.HzbMeta) &&
                   views.IsCreated();
        }

        private bool TryOpenExistingDepletionViews(out ProceduralGeologyVaultViews views)
        {
            views = default;
            if (_dataVault == null)
                return false;

            return TryOpenExistingBuffer(in _resourceNodesHandle, _oreCapacity, out views.ResourceNodes) &&
                   TryOpenExistingBuffer(in _orePositionsHandle, _oreCapacity, out views.OrePositions) &&
                   TryOpenExistingBuffer(in _oreTypesHandle, _oreCapacity, out views.OreTypes) &&
                   TryOpenExistingBuffer(in _depletionMasksHandle, _depletionWordCount, out views.DepletionMasks) &&
                   TryOpenExistingBuffer(in _oreMatricesHandle, _oreCapacity, out views.OreMatrices) &&
                   TryOpenExistingBuffer(in _candidateSlotsHandle, _oreCapacity, out views.CandidateSlots) &&
                   TryOpenExistingBuffer(in _depletionCacheKeysHandle, DepletionCacheCapacity, out views.DepletionCacheKeys) &&
                   TryOpenExistingBuffer(in _depletionCacheMasksHandle, DepletionCacheCapacity, out views.DepletionCacheMasks) &&
                   TryOpenExistingBuffer(in _depletionCacheCountHandle, DepletionCacheCountLength, out views.DepletionCacheCount) &&
                   TryOpenExistingBuffer(in _indirectArgsHandle, IndirectArgsCount, out views.IndirectArgs);
        }

        private bool TryOpenExistingDepletionMaskViews(out ProceduralGeologyVaultViews views)
        {
            views = default;
            if (_dataVault == null)
                return false;

            return TryOpenExistingBuffer(in _depletionMasksHandle, _depletionWordCount, out views.DepletionMasks) &&
                   TryOpenExistingBuffer(in _depletionCacheKeysHandle, DepletionCacheCapacity, out views.DepletionCacheKeys) &&
                   TryOpenExistingBuffer(in _depletionCacheMasksHandle, DepletionCacheCapacity, out views.DepletionCacheMasks) &&
                   TryOpenExistingBuffer(in _depletionCacheCountHandle, DepletionCacheCountLength, out views.DepletionCacheCount);
        }

        private bool TryOpenExistingRuntimeShiftViews(out ProceduralGeologyVaultViews views)
        {
            views = default;
            if (_dataVault == null)
                return false;

            return TryOpenExistingBuffer(in _resourceNodesHandle, _oreCapacity, out views.ResourceNodes) &&
                   TryOpenExistingBuffer(in _orePositionsHandle, _oreCapacity, out views.OrePositions) &&
                   TryOpenExistingBuffer(in _oreTypesHandle, _oreCapacity, out views.OreTypes) &&
                   TryOpenExistingBuffer(in _oreMatricesHandle, _oreCapacity, out views.OreMatrices) &&
                   TryOpenExistingBuffer(in _telemetryRingHandle, ProceduralGeologyConstants.TelemetryFrames, out views.TelemetryRing);
        }

        private bool AcquireBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> view) where T : struct
        {
            IDataVault vault = _dataVault;
            return AcquireBuffer(vault, ref handle, bufferId, requiredLength, options, out view);
        }

        private static bool AcquireBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> view) where T : struct
        {
            if (vault == null || requiredLength <= 0)
            {
                view = default;
                return false;
            }

            if (!IsVaultHandleCreated(in handle))
            {
                handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
            }

            if (vault.TryResolveHandle(in handle, out view) &&
                view.IsCreated &&
                view.Length >= requiredLength)
            {
                return true;
            }

            handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
            return vault.TryResolveHandle(in handle, out view) &&
                   view.IsCreated &&
                   view.Length >= requiredLength;
        }

        private bool TryOpenExistingBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> view) where T : struct
        {
            view = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsVaultHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out view) &&
                   view.IsCreated &&
                   view.Length >= requiredLength;
        }

        private bool TryReadExistingBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> view) where T : struct
        {
            view = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsVaultHandleCreated(in handle) &&
                   vault.TryReadHandle(in handle, out view) &&
                   view.IsCreated &&
                   view.Length >= requiredLength;
        }

        private bool CanExposeReadSnapshot()
        {
            return !_spawnJobScheduled && !_pendingDataVaultRebind && !_pendingOreReadDependencyValid;
        }

        private bool HasPendingOreReadDependency()
        {
            TryFinalizeCompletedOreReadDependency();
            return _pendingOreReadDependencyValid;
        }

        private bool TryFinalizeCompletedOreReadDependency()
        {
            if (!_pendingOreReadDependencyValid || !_pendingOreReadDependency.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingOreReadDependency))
                return false;

            _pendingOreReadDependencyValid = false;
            return true;
        }

        private void CompletePendingOreReadDependencyForTeardown()
        {
            if (!_pendingOreReadDependencyValid)
                return;

            // [BLOCKING_SYNC_POINT] Structural teardown only. Ore Vault rows must not be invalidated while a registered reader job can still touch them.
            if (_pendingOreReadDependency.IsCompleted)
                DispatcherJobFence.TryFinalizeCompleted(ref _pendingOreReadDependency);
            else
                DispatcherJobFence.TryComplete(ref _pendingOreReadDependency, forceComplete: true);

            _pendingOreReadDependency = default;
            _pendingOreReadDependencyValid = false;
        }

        private void ReleaseVaultViews()
        {
            _resourceNodesHandle = default;
            _orePositionsHandle = default;
            _oreTypesHandle = default;
            _depletionMasksHandle = default;
            _oreMatricesHandle = default;
            _biomeHeatmapHandle = default;
            _spawnCountsHandle = default;
            _telemetryRingHandle = default;
            _mockTerrainSdfHandle = default;
            _distributionRulesHandle = default;
            _tuningHandle = default;
            _csvScratchHandle = default;
            _selfAuditHandle = default;
            _candidateSlotsHandle = default;
            _depletionCacheKeysHandle = default;
            _depletionCacheMasksHandle = default;
            _depletionCacheCountHandle = default;
            _sectorHashGridHandle = default;
            _indirectArgsHandle = default;
            _hzbTilesHandle = default;
            _hzbMetaHandle = default;
            _dataVault = null;
            _lockedVaultBufferMask = 0;
            _depletionCacheInitialized = false;
        }

        private void EnsureGeologyTuning(ProceduralGeologyVaultViews views)
        {
            NativeArray<GeologyTuningDTO> tuningBuffer = views.GeologyTuning;
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            GeologyTuningDTO tuning = tuningBuffer[0];
            bool usable = tuning.Version == 1u &&
                          math.isfinite(tuning.BaseNodeDensity) &&
                          tuning.BaseNodeDensity > 0f &&
                          tuning.BaseNodeDensity <= 16f &&
                          math.isfinite(tuning.ClusterSpreadRadius) &&
                          tuning.ClusterSpreadRadius >= 0.05f &&
                          tuning.ClusterSpreadRadius <= 16f &&
                          math.isfinite(tuning.SurfaceNormalAlignmentTolerance) &&
                          tuning.SurfaceNormalAlignmentTolerance >= 0.05f &&
                          tuning.SurfaceNormalAlignmentTolerance <= 1f &&
                          math.isfinite(tuning.VisualClusterDensity) &&
                          tuning.VisualClusterDensity >= 0f &&
                          tuning.VisualClusterDensity <= 1f &&
                          math.isfinite(tuning.SectorSizeMeters) &&
                          tuning.SectorSizeMeters >= 16f &&
                          tuning.SectorSizeMeters <= 100000f;
            if (!usable)
            {
                tuning = GeologyTuningDTO.Default(sectorSizeMeters);
                tuning.ClusterSpreadRadius = math.max(0.05f, clusterSpreadRadius);
                tuning.SurfaceNormalAlignmentTolerance = math.clamp(normalAlignmentTolerance, 0.05f, 1f);
                tuning.VisualClusterDensity = math.saturate(visualClusterDensity);
            }

            tuning.BaseNodeDensity = math.clamp(math.isfinite(tuning.BaseNodeDensity) ? tuning.BaseNodeDensity : 1f, 0.05f, 16f);
            tuning.ClusterSpreadRadius = math.clamp(math.isfinite(tuning.ClusterSpreadRadius) ? tuning.ClusterSpreadRadius : clusterSpreadRadius, 0.05f, 16f);
            tuning.SurfaceNormalAlignmentTolerance = math.clamp(math.isfinite(tuning.SurfaceNormalAlignmentTolerance) ? tuning.SurfaceNormalAlignmentTolerance : normalAlignmentTolerance, 0.05f, 1f);
            tuning.VisualClusterDensity = math.saturate(math.isfinite(tuning.VisualClusterDensity) ? tuning.VisualClusterDensity : visualClusterDensity);
            tuning.SectorSizeMeters = math.clamp(math.isfinite(tuning.SectorSizeMeters) ? tuning.SectorSizeMeters : sectorSizeMeters, 16f, 100000f);
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning.Version = 1u;
            tuningBuffer[0] = tuning;
            clusterSpreadRadius = tuning.ClusterSpreadRadius;
            normalAlignmentTolerance = tuning.SurfaceNormalAlignmentTolerance;
            visualClusterDensity = tuning.VisualClusterDensity;
            sectorSizeMeters = tuning.SectorSizeMeters;
        }

        private void LoadDistributionRules(ProceduralGeologyVaultViews views)
        {
            if (_distributionRulesLoaded || !views.DistributionRules.IsCreated)
                return;

            _distributionRuleCount = WriteDefaultDistributionRules(views.DistributionRules);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string csvPath = Path.Combine(projectRoot, "Docs", "resource_distribution_rules.csv");
            int parsed = TryLoadDistributionRulesFromCsv(csvPath, views);
            if (parsed > 0)
                _distributionRuleCount = parsed;

            _distributionRulesLoaded = true;
        }

        private unsafe int TryLoadDistributionRulesFromCsv(string csvPath, ProceduralGeologyVaultViews views)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !views.DistributionRules.IsCreated ||
                !File.Exists(csvPath) ||
                !TryAcquireVaultBuffer(
                    vault,
                    ref _csvScratchHandle,
                    ProceduralGeologyVaultBufferIds.CsvScratch,
                    ProceduralGeologyConstants.CsvScratchBytes,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<byte> csvScratch))
            {
                return 0;
            }

            try
            {
                int byteCount = LoadCsvFileIntoScratch(csvPath, csvScratch);
                if (byteCount <= 0)
                    return 0;

                void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(csvScratch);
                ReadOnlySpan<byte> csvBytes = new ReadOnlySpan<byte>(ptr, byteCount);
                return ProceduralGeologyCsv.ParseDistributionRules(csvBytes, views.DistributionRules);
            }
            finally
            {
                vault.ReleaseWriteLock(in _csvScratchHandle, OwnerSystemId);
            }
        }

        private static unsafe int LoadCsvFileIntoScratch(string csvPath, NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || scratch.Length <= 0)
                return 0;

            using FileStream stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); // COLD ALLOC: FileStream[csv] — designer distribution CSV stream into Vault scratch — owner: ProceduralOreSpawner
            if (stream.Length <= 0L || stream.Length > scratch.Length)
                return 0;

            int maxBytes = (int)stream.Length;
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
            Span<byte> destination = new Span<byte>(ptr, maxBytes);
            int total = 0;
            while (total < maxBytes)
            {
                int read = stream.Read(destination.Slice(total));
                if (read <= 0)
                    break;
                total += read;
            }

            return total;
        }

        private static int WriteDefaultDistributionRules(NativeArray<GeologyDistributionRuleDTO> rules)
        {
            if (!rules.IsCreated || rules.Length < 4)
                return 0;

            rules[0] = BuildDistributionRule(0u, OreTypeBasaltIron, 50, float.MinValue, float.MaxValue);
            rules[1] = BuildDistributionRule(0u, OreTypeCopper, 28, float.MinValue, float.MaxValue);
            rules[2] = BuildDistributionRule(0u, OreTypeTitanium, 17, float.MinValue, float.MaxValue);
            rules[3] = BuildDistributionRule(0u, OreTypeSilver, 5, float.MinValue, float.MaxValue);
            return 4;
        }

        private static GeologyDistributionRuleDTO BuildDistributionRule(uint biomeHash, int oreType, int weight, float minDepth, float maxDepth)
        {
            GeologyDistributionRuleDTO rule = default;
            rule.BiomeHash = biomeHash;
            rule.ResourceTypeHash = (uint)math.max(0, oreType);
            rule.Weight = math.max(0, weight);
            rule.MinDepth = minDepth;
            rule.MaxDepth = maxDepth;
            rule.RuleHash = ProceduralGeologyHash.Mix64To32(((ulong)biomeHash << 32) | (uint)oreType);
            return rule;
        }

        private float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private void RunSelfAudit(ProceduralGeologyVaultViews views)
        {
            if (!views.SelfAudit.IsCreated || views.SelfAudit.Length == 0)
                return;

            bool releaseLock = false;
            if ((_lockedVaultBufferMask & (1 << 18)) == 0)
            {
                if (_lockedVaultBufferMask != 0 || !TryLockVaultSelfAuditBuffer())
                    return;
                releaseLock = true;
            }

            try
            {
                WriteSelfAudit(views);
            }
            finally
            {
                if (releaseLock)
                    UnlockVaultWriteBuffers();
            }
        }

        private void WriteSelfAudit(ProceduralGeologyVaultViews views)
        {
            uint probeA = ComputeDeterminismProbe(unchecked((ulong)_currentSectorHash), worldSeed);
            uint probeB = probeA;
            uint flags = ProceduralGeologyLayoutAudit.Validate() ? 0u : 1u;
            for (int i = 0; i < 100; i++)
            {
                uint probe = ComputeDeterminismProbe(unchecked((ulong)_currentSectorHash), worldSeed);
                if (probe != probeA)
                    flags |= 2u;
                probeB = probe;
            }

            GeologySelfAuditResultDTO audit = default;
            audit.Frame = AdvanceSimulationFrameId();
            audit.Flags = flags;
            audit.ResourceNodeSize = (uint)UnsafeUtility.SizeOf<ResourceNodeDTO>();
            audit.TelemetrySize = (uint)UnsafeUtility.SizeOf<GeologyGenerationTelemetryEntry>();
            audit.DeterminismHashA = probeA;
            audit.DeterminismHashB = probeB;
            audit.AliasFaults = (_lockedVaultBufferMask & ~(1 << 18)) == 0 ? 0u : 1u;
            audit.ManagedAllocationFaults = 0u;
            audit.BufferMaskLow = 0x001FFFFFUL;
            audit.BufferMaskHigh = 0UL;
            audit.GlobalQualityWeight = ResolveGlobalQualityWeight();
            views.SelfAudit[0] = audit;
        }

        private static uint ComputeDeterminismProbe(ulong sectorHash, uint seed)
        {
            uint state = ProceduralGeologyHash.Mix64To32(sectorHash ^ seed);
            uint hash = 2166136261u;
            for (int i = 0; i < 100; i++)
            {
                uint value = ProceduralGeologyHash.Next(ref state);
                hash ^= value;
                hash *= 16777619u;
            }

            return hash;
        }

        private bool TryLockVaultJobBuffers()
        {
            if (HasPendingOreReadDependency())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int locked = 0;
            if (!TryLockVaultBuffer(vault, ref _resourceNodesHandle, ProceduralGeologyVaultBufferIds.ResourceNodes, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 0, ref locked) ||
                !TryLockVaultBuffer(vault, ref _orePositionsHandle, ProceduralGeologyVaultBufferIds.OrePositions, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 1, ref locked) ||
                !TryLockVaultBuffer(vault, ref _oreTypesHandle, ProceduralGeologyVaultBufferIds.OreTypes, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 2, ref locked) ||
                !TryLockVaultBuffer(vault, ref _oreMatricesHandle, ProceduralGeologyVaultBufferIds.ResourceMatrices, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 4, ref locked) ||
                !TryLockVaultBuffer(vault, ref _spawnCountsHandle, ProceduralGeologyVaultBufferIds.SpawnCounts, SpawnCounterCount, NativeArrayOptions.ClearMemory, 1 << 5, ref locked) ||
                !TryLockVaultBuffer(vault, ref _mockTerrainSdfHandle, ProceduralGeologyVaultBufferIds.MockTerrainSdf, ProceduralGeologyConstants.MockTerrainResolution * ProceduralGeologyConstants.MockTerrainResolution, NativeArrayOptions.UninitializedMemory, 1 << 6, ref locked) ||
                !TryLockVaultBuffer(vault, ref _candidateSlotsHandle, ProceduralGeologyVaultBufferIds.CandidateSlots, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 9, ref locked) ||
                !TryLockVaultBuffer(vault, ref _indirectArgsHandle, ProceduralGeologyVaultBufferIds.IndirectArgs, IndirectArgsCount, NativeArrayOptions.UninitializedMemory, 1 << 10, ref locked))
            {
                UnlockVaultWriteBuffers(vault, locked);
                return false;
            }

            _lockedVaultBufferMask = locked;
            return true;
        }

        private bool TryLockVaultDepletionBuffers()
        {
            if (HasPendingOreReadDependency())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int locked = 0;
            if (!TryLockVaultBuffer(vault, ref _resourceNodesHandle, ProceduralGeologyVaultBufferIds.ResourceNodes, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 0, ref locked) ||
                !TryLockVaultBuffer(vault, ref _orePositionsHandle, ProceduralGeologyVaultBufferIds.OrePositions, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 1, ref locked) ||
                !TryLockVaultBuffer(vault, ref _oreTypesHandle, ProceduralGeologyVaultBufferIds.OreTypes, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 2, ref locked) ||
                !TryLockVaultBuffer(vault, ref _depletionMasksHandle, ProceduralGeologyVaultBufferIds.DepletionMasks, _depletionWordCount, NativeArrayOptions.UninitializedMemory, 1 << 3, ref locked) ||
                !TryLockVaultBuffer(vault, ref _oreMatricesHandle, ProceduralGeologyVaultBufferIds.ResourceMatrices, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 4, ref locked) ||
                !TryLockVaultBuffer(vault, ref _candidateSlotsHandle, ProceduralGeologyVaultBufferIds.CandidateSlots, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 9, ref locked) ||
                !TryLockVaultBuffer(vault, ref _indirectArgsHandle, ProceduralGeologyVaultBufferIds.IndirectArgs, IndirectArgsCount, NativeArrayOptions.UninitializedMemory, 1 << 10, ref locked) ||
                !TryLockVaultBuffer(vault, ref _depletionCacheKeysHandle, ProceduralGeologyVaultBufferIds.DepletionCacheKeys, DepletionCacheCapacity, NativeArrayOptions.UninitializedMemory, 1 << 13, ref locked) ||
                !TryLockVaultBuffer(vault, ref _depletionCacheMasksHandle, ProceduralGeologyVaultBufferIds.DepletionCacheMasks, DepletionCacheCapacity, NativeArrayOptions.UninitializedMemory, 1 << 14, ref locked) ||
                !TryLockVaultBuffer(vault, ref _depletionCacheCountHandle, ProceduralGeologyVaultBufferIds.DepletionCacheCount, DepletionCacheCountLength, NativeArrayOptions.ClearMemory, 1 << 15, ref locked) ||
                !TryLockVaultBuffer(vault, ref _telemetryRingHandle, ProceduralGeologyVaultBufferIds.TelemetryRing, ProceduralGeologyConstants.TelemetryFrames, NativeArrayOptions.ClearMemory, 1 << 16, ref locked))
            {
                UnlockVaultWriteBuffers(vault, locked);
                return false;
            }

            _lockedVaultBufferMask = locked;
            return true;
        }

        private bool TryLockVaultDepletionMaskBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int locked = 0;
            if (!TryLockVaultBuffer(vault, ref _depletionMasksHandle, ProceduralGeologyVaultBufferIds.DepletionMasks, _depletionWordCount, NativeArrayOptions.UninitializedMemory, 1 << 3, ref locked) ||
                !TryLockVaultBuffer(vault, ref _depletionCacheKeysHandle, ProceduralGeologyVaultBufferIds.DepletionCacheKeys, DepletionCacheCapacity, NativeArrayOptions.UninitializedMemory, 1 << 13, ref locked) ||
                !TryLockVaultBuffer(vault, ref _depletionCacheMasksHandle, ProceduralGeologyVaultBufferIds.DepletionCacheMasks, DepletionCacheCapacity, NativeArrayOptions.UninitializedMemory, 1 << 14, ref locked) ||
                !TryLockVaultBuffer(vault, ref _depletionCacheCountHandle, ProceduralGeologyVaultBufferIds.DepletionCacheCount, DepletionCacheCountLength, NativeArrayOptions.ClearMemory, 1 << 15, ref locked))
            {
                UnlockVaultWriteBuffers(vault, locked);
                return false;
            }

            _lockedVaultBufferMask = locked;
            return true;
        }

        private bool TryLockVaultBiomeHeatmapBuffer()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int locked = 0;
            if (!TryLockVaultBuffer(vault, ref _biomeHeatmapHandle, ProceduralGeologyVaultBufferIds.BiomeHeatmap, BiomeHeatmapResolution * BiomeHeatmapResolution, NativeArrayOptions.UninitializedMemory, 1 << 7, ref locked))
            {
                UnlockVaultWriteBuffers(vault, locked);
                return false;
            }

            _lockedVaultBufferMask = locked;
            return true;
        }

        private bool TryLockVaultSectorHashGridBuffer()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int locked = 0;
            if (!TryLockVaultBuffer(vault, ref _sectorHashGridHandle, ProceduralGeologyVaultBufferIds.SectorHashGrid, SectorHashGridCount, NativeArrayOptions.UninitializedMemory, 1 << 17, ref locked))
            {
                UnlockVaultWriteBuffers(vault, locked);
                return false;
            }

            _lockedVaultBufferMask = locked;
            return true;
        }

        private bool TryLockVaultRuntimeShiftBuffers()
        {
            if (HasPendingOreReadDependency())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int locked = 0;
            if (!TryLockVaultBuffer(vault, ref _resourceNodesHandle, ProceduralGeologyVaultBufferIds.ResourceNodes, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 0, ref locked) ||
                !TryLockVaultBuffer(vault, ref _orePositionsHandle, ProceduralGeologyVaultBufferIds.OrePositions, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 1, ref locked) ||
                !TryLockVaultBuffer(vault, ref _oreMatricesHandle, ProceduralGeologyVaultBufferIds.ResourceMatrices, _oreCapacity, NativeArrayOptions.UninitializedMemory, 1 << 4, ref locked) ||
                !TryLockVaultBuffer(vault, ref _telemetryRingHandle, ProceduralGeologyVaultBufferIds.TelemetryRing, ProceduralGeologyConstants.TelemetryFrames, NativeArrayOptions.ClearMemory, 1 << 16, ref locked))
            {
                UnlockVaultWriteBuffers(vault, locked);
                return false;
            }

            _lockedVaultBufferMask = locked;
            return true;
        }

        private bool TryLockVaultIndirectArgsBuffer()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int locked = 0;
            if (!TryLockVaultBuffer(vault, ref _indirectArgsHandle, ProceduralGeologyVaultBufferIds.IndirectArgs, IndirectArgsCount, NativeArrayOptions.UninitializedMemory, 1 << 10, ref locked))
            {
                UnlockVaultWriteBuffers(vault, locked);
                return false;
            }

            _lockedVaultBufferMask = locked;
            return true;
        }

        private bool TryLockVaultTelemetryBuffer()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int locked = 0;
            if (!TryLockVaultBuffer(vault, ref _telemetryRingHandle, ProceduralGeologyVaultBufferIds.TelemetryRing, ProceduralGeologyConstants.TelemetryFrames, NativeArrayOptions.ClearMemory, 1 << 16, ref locked))
            {
                UnlockVaultWriteBuffers(vault, locked);
                return false;
            }

            _lockedVaultBufferMask = locked;
            return true;
        }

        private bool TryLockVaultSelfAuditBuffer()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int locked = 0;
            if (!TryLockVaultBuffer(vault, ref _selfAuditHandle, ProceduralGeologyVaultBufferIds.SelfAudit, ProceduralGeologyConstants.SelfAuditCapacity, NativeArrayOptions.ClearMemory, 1 << 18, ref locked))
            {
                UnlockVaultWriteBuffers(vault, locked);
                return false;
            }

            _lockedVaultBufferMask = locked;
            return true;
        }

        private static bool TryLockVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            int bit,
            ref int locked) where T : struct
        {
            if (!TryAcquireVaultBuffer(vault, ref handle, bufferId, requiredLength, options, out NativeArray<T> _))
                return false;

            locked |= bit;
            return true;
        }

        private static bool TryAcquireVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> lockedView) where T : struct
        {
            lockedView = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (!IsVaultHandleCreated(in handle))
                handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);

            if (vault.TryAcquireWriteLock(in handle, OwnerSystemId, out lockedView) &&
                IsVaultHandleCreated(in handle) &&
                lockedView.IsCreated &&
                lockedView.Length >= requiredLength)
            {
                return true;
            }

            if (lockedView.IsCreated)
                vault.ReleaseWriteLock(in handle, OwnerSystemId);

            handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
            if (vault.TryAcquireWriteLock(in handle, OwnerSystemId, out lockedView) &&
                lockedView.IsCreated &&
                lockedView.Length >= requiredLength)
            {
                return true;
            }

            if (lockedView.IsCreated)
                vault.ReleaseWriteLock(in handle, OwnerSystemId);

            lockedView = default;
            return false;
        }

        private void UnlockVaultWriteBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || _lockedVaultBufferMask == 0)
            {
                _lockedVaultBufferMask = 0;
                return;
            }

            UnlockVaultWriteBuffers(vault, _lockedVaultBufferMask);
            _lockedVaultBufferMask = 0;
        }

        private void UnlockVaultWriteBuffers(IDataVault vault, int locked)
        {
            if (vault == null || locked == 0)
                return;

            if ((locked & (1 << 0)) != 0) vault.ReleaseWriteLock(in _resourceNodesHandle, OwnerSystemId);
            if ((locked & (1 << 1)) != 0) vault.ReleaseWriteLock(in _orePositionsHandle, OwnerSystemId);
            if ((locked & (1 << 2)) != 0) vault.ReleaseWriteLock(in _oreTypesHandle, OwnerSystemId);
            if ((locked & (1 << 3)) != 0) vault.ReleaseWriteLock(in _depletionMasksHandle, OwnerSystemId);
            if ((locked & (1 << 4)) != 0) vault.ReleaseWriteLock(in _oreMatricesHandle, OwnerSystemId);
            if ((locked & (1 << 5)) != 0) vault.ReleaseWriteLock(in _spawnCountsHandle, OwnerSystemId);
            if ((locked & (1 << 6)) != 0) vault.ReleaseWriteLock(in _mockTerrainSdfHandle, OwnerSystemId);
            if ((locked & (1 << 7)) != 0) vault.ReleaseWriteLock(in _biomeHeatmapHandle, OwnerSystemId);
            if ((locked & (1 << 8)) != 0) vault.ReleaseWriteLock(in _distributionRulesHandle, OwnerSystemId);
            if ((locked & (1 << 9)) != 0) vault.ReleaseWriteLock(in _candidateSlotsHandle, OwnerSystemId);
            if ((locked & (1 << 10)) != 0) vault.ReleaseWriteLock(in _indirectArgsHandle, OwnerSystemId);
            if ((locked & (1 << 11)) != 0) vault.ReleaseWriteLock(in _hzbTilesHandle, OwnerSystemId);
            if ((locked & (1 << 12)) != 0) vault.ReleaseWriteLock(in _hzbMetaHandle, OwnerSystemId);
            if ((locked & (1 << 13)) != 0) vault.ReleaseWriteLock(in _depletionCacheKeysHandle, OwnerSystemId);
            if ((locked & (1 << 14)) != 0) vault.ReleaseWriteLock(in _depletionCacheMasksHandle, OwnerSystemId);
            if ((locked & (1 << 15)) != 0) vault.ReleaseWriteLock(in _depletionCacheCountHandle, OwnerSystemId);
            if ((locked & (1 << 16)) != 0) vault.ReleaseWriteLock(in _telemetryRingHandle, OwnerSystemId);
            if ((locked & (1 << 17)) != 0) vault.ReleaseWriteLock(in _sectorHashGridHandle, OwnerSystemId);
            if ((locked & (1 << 18)) != 0) vault.ReleaseWriteLock(in _selfAuditHandle, OwnerSystemId);
        }

        private void EnsureRenderBuffers()
        {
            if (_oreCapacity <= 0)
                return;

            if (_matrixBufferA == null)
                _matrixBufferA = CreateStructuredLockBuffer<float4x4>(_oreCapacity); // COLD ALLOC: GraphicsBuffer[oreCapacity] — ore matrix upload buffer A — owner: ProceduralOreSpawner
            if (_matrixBufferB == null)
                _matrixBufferB = CreateStructuredLockBuffer<float4x4>(_oreCapacity); // COLD ALLOC: GraphicsBuffer[oreCapacity] — ore matrix upload buffer B — owner: ProceduralOreSpawner
            if (_argsBuffer == null)
                _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, UnsafeUtility.SizeOf<GeologyIndirectArgsDTO>()); // COLD ALLOC: GraphicsBuffer[1] — ore DrawProceduralIndirect args — owner: ProceduralOreSpawner

            _activeMatrixBuffer = _matrixBufferA;
            UpdateIndirectArgsBuffer(0u);
        }

        private void RefreshSectorAndTerrain()
        {
            if (!CapturePlayerPose(out AbsoluteUniversePosition playerAup, out float3 playerRuntimePosition))
                return;

            double3 playerAbsolute = playerAup.ToAbsoluteDouble3();
            GeologyHeightPayloadView heightPayload = default;
            float safeSectorSize = math.max(16f, sectorSizeMeters);
            int2 sector = new int2(
                (int)math.floor(playerAbsolute.x / safeSectorSize),
                (int)math.floor(playerAbsolute.z / safeSectorSize));

            bool sectorChanged = !_depletionLoaded || !sector.Equals(_currentSector);
            bool anchorRefresh = _dropPodAnchorRequiresGenerationRefresh;
            if (sectorChanged || anchorRefresh)
            {
                if (_spawnJobScheduled)
                {
                    if (!TryCompleteFinishedSpawnJob())
                        return;

                    DiscardSpawnJobOutput();
                }

                if (sectorChanged)
                {
                    _currentSector = sector;
                    _currentSectorHash = ComputeAupSectorHash(sector, worldSeed);
                    WriteAupSectorHashGrid(sector);
                    LoadDepletionMasksForCurrentSector();
                }
            }

            if ((sectorChanged || anchorRefresh) && !_spawnJobScheduled)
            {
                RefreshTerrainPayload(playerAbsolute, out heightPayload);
                _dropPodAnchorRequiresGenerationRefresh = false;
                ScheduleSpawnJob(playerAbsolute, playerRuntimePosition, heightPayload);
            }
        }

        private bool CapturePlayerPose(out AbsoluteUniversePosition playerAup, out float3 runtimePosition)
        {
            playerAup = default;
            runtimePosition = default;

            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext == null)
                return false;

            if (!TryCapturePlayerPose(playerContext, out playerAup, out runtimePosition))
                return false;

            StorePlayerRuntimePosition(runtimePosition);
            return true;
        }

        private static bool TryCapturePlayerPose(
            IPlayerRuntimeContext playerContext,
            out AbsoluteUniversePosition playerAup,
            out float3 runtimePosition)
        {
            playerAup = default;
            runtimePosition = default;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                playerAup = snapshot.Aup;
                runtimePosition = snapshot.RuntimePosition;
                return playerAup.IsFinite() && math.all(math.isfinite(runtimePosition));
            }

            var playerMovement = playerContext.PlayerMovement;
            if (playerMovement == null)
                return false;

            playerAup = playerMovement.CurrentAup;
            if (!playerAup.IsFinite())
                return false;

            runtimePosition = playerAup.ToRuntimeFloat3();
            return math.all(math.isfinite(runtimePosition));
        }

        private void StorePlayerRuntimePosition(float3 runtimePosition)
        {
            if (!math.all(math.isfinite(runtimePosition)))
                return;

            _lastPlayerRuntimePosition = runtimePosition;
            _hasPlayerRuntimePosition = true;
        }

        private Vector3 ResolvePresentationCenter()
        {
            float3 center = _hasPlayerRuntimePosition && math.all(math.isfinite(_lastPlayerRuntimePosition))
                ? _lastPlayerRuntimePosition
                : _hasDropPodAnchor && math.all(math.isfinite(_dropPodRuntimePosition))
                    ? _dropPodRuntimePosition
                    : default;
            return ToVector3(center);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private void WriteAupSectorHashGrid(int2 centerSector)
        {
            if (!TryLockVaultSectorHashGridBuffer())
                return;

            try
            {
                if (!TryOpenExistingBuffer(
                        in _sectorHashGridHandle,
                        SectorHashGridCount,
                        out NativeArray<long> sectorHashGrid))
                {
                    return;
                }

                int write = 0;
                for (int z = -1; z <= 1; z++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        int2 sector = new int2(centerSector.x + x, centerSector.y + z);
                        sectorHashGrid[write++] = ComputeAupSectorHash(sector, worldSeed);
                    }
                }
            }
            finally
            {
                UnlockVaultWriteBuffers();
            }
        }

        private void RefreshTerrainPayload(double3 playerAbsolute, out GeologyHeightPayloadView heightPayload)
        {
            heightPayload = default;
            _currentBiomeId = 0;
            heightPayload.TerrainBaseY = (float)playerAbsolute.y;

            Vector3 runtimeProbe = HectonFloatingOrigin.ToRuntimePosition(playerAbsolute);
            if (!IsFinite(runtimeProbe))
            {
                FillBiomeHeatmap(0);
                return;
            }

            double3 runtimeToAbsoluteOffset = playerAbsolute - new double3(runtimeProbe.x, runtimeProbe.y, runtimeProbe.z);
            ITerrainProvider terrainProvider = _terrainProvider;
            if (terrainProvider != null &&
                terrainProvider.IsAvailable &&
                terrainProvider.TryGetHeight(runtimeProbe.x, runtimeProbe.z, out float terrainHeight) &&
                math.isfinite(terrainHeight))
            {
                heightPayload.TerrainBaseY = (float)((double)terrainHeight + runtimeToAbsoluteOffset.y);
            }

            if (mapMagicBridge == null)
            {
                FillBiomeHeatmap(0);
                return;
            }

            if (mapMagicBridge.TryGetQuantizedHeightmapPayload(runtimeProbe.x, runtimeProbe.z, out MapMagicBridge.QuantizedHeightmapPayload payload) &&
                MapMagicBridge.QuantizedHeightmapPayload.IsValid(in payload))
            {
                heightPayload.HeightSamples = payload.HeightSamples;
                heightPayload.TerrainSize = ToFloat3(payload.TerrainSize);
                heightPayload.TerrainOriginAbsoluteXZ = new double2(
                    (double)payload.TerrainPosition.x + runtimeToAbsoluteOffset.x,
                    (double)payload.TerrainPosition.z + runtimeToAbsoluteOffset.z);
                heightPayload.TerrainBaseY = (float)((double)payload.TerrainPosition.y + runtimeToAbsoluteOffset.y);
                heightPayload.HeightResolution = payload.HeightmapResolution;
                heightPayload.HasQuantizedPayload = 1;
            }

            if (mapMagicBridge.TryGetMatrixBiomeId(runtimeProbe.x, runtimeProbe.z, out int biomeId))
                _currentBiomeId = biomeId;
            else
                _currentBiomeId = mapMagicBridge.CurrentBiomeID;

            FillBiomeHeatmap(_currentBiomeId);
        }

        private void FillBiomeHeatmap(int biomeId)
        {
            if (!TryLockVaultBiomeHeatmapBuffer())
                return;

            try
            {
                if (!TryOpenExistingBuffer(
                        in _biomeHeatmapHandle,
                        BiomeHeatmapResolution * BiomeHeatmapResolution,
                        out NativeArray<byte> biomeHeatmap))
                {
                    return;
                }

                byte packed = (byte)math.clamp(biomeId, 0, byte.MaxValue);
                for (int i = 0; i < biomeHeatmap.Length; i++)
                    biomeHeatmap[i] = packed;
            }
            finally
            {
                UnlockVaultWriteBuffers();
            }
        }

        private void ScheduleSpawnJob(double3 playerAbsolute, float3 playerRuntimePosition, GeologyHeightPayloadView payload)
        {
            if (!EnsureNativeState() || !TryLockVaultJobBuffers())
                return;

            if (!TryOpenExistingVaultViews(out ProceduralGeologyVaultViews views))
            {
                UnlockVaultWriteBuffers();
                return;
            }

            EnsureDropPodAnchor(playerAbsolute, playerRuntimePosition);
            EnsureGeologyTuning(views);
            int scanCount = ResolveIterationBudget(views);
            GeologyTuningDTO tuning = views.GeologyTuning.IsCreated && views.GeologyTuning.Length > 0
                ? views.GeologyTuning[0]
                : GeologyTuningDTO.Default(sectorSizeMeters);
            float safeSectorSize = math.max(16f, tuning.SectorSizeMeters);
            double2 sectorOrigin = new double2((double)_currentSector.x * safeSectorSize, (double)_currentSector.y * safeSectorSize);
            float qualityWeight = ResolveGlobalQualityWeight();
            bool hasQuantizedPayload = HasQuantizedHeightPayload(in payload);
            double2 terrainOriginAbsoluteXZ = hasQuantizedPayload
                ? payload.TerrainOriginAbsoluteXZ
                : sectorOrigin;
            float terrainBaseY = math.isfinite(payload.TerrainBaseY)
                ? payload.TerrainBaseY
                : (float)playerAbsolute.y;
            ClearPresentationState(false);
            _drawBounds = new Bounds(ToVector3(playerRuntimePosition), Vector3.one * safeSectorSize);
            _discardSpawnJobOutput = false;

            JobHandle dependency = default;
            if (!hasQuantizedPayload && views.MockTerrainSdf.IsCreated)
            {
                GenerateMockTerrainSDFJob mockJob = new GenerateMockTerrainSDFJob
                {
                    Samples = views.MockTerrainSdf,
                    Resolution = ProceduralGeologyConstants.MockTerrainResolution,
                    SectorOrigin = sectorOrigin,
                    SectorSize = safeSectorSize,
                    BaseHeight = terrainBaseY,
                    Seed = unchecked((uint)_currentSectorHash ^ worldSeed)
                };
                dependency = mockJob.Schedule(views.MockTerrainSdf.Length, 32);
            }

            GenerateResourceNodesJob job = new GenerateResourceNodesJob
            {
                ResourceNodes = views.ResourceNodes,
                OrePositions = views.OrePositions,
                OreTypes = views.OreTypes,
                DepletionMasks = views.DepletionMasks,
                OreMatrices = views.OreMatrices,
                SpawnCounts = views.SpawnCounts,
                CandidateSlots = views.CandidateSlots,
                IndirectArgs = views.IndirectArgs,
                HeightSamples = hasQuantizedPayload ? payload.HeightSamples : default,
                MockTerrainSdf = hasQuantizedPayload ? default : views.MockTerrainSdf,
                BiomeHeatmap = views.BiomeHeatmap,
                DistributionRules = views.DistributionRules,
                HzbTiles = views.HzbTiles,
                HzbMeta = views.HzbMeta,
                Capacity = _oreCapacity,
                ScanCount = scanCount,
                SectorOrigin = sectorOrigin,
                SectorSize = safeSectorSize,
                TerrainPosition = new float3(0f, terrainBaseY, 0f),
                TerrainOriginAbsoluteXZ = terrainOriginAbsoluteXZ,
                TerrainSize = hasQuantizedPayload ? payload.TerrainSize : new float3(safeSectorSize, 64f, safeSectorSize),
                HeightResolution = hasQuantizedPayload ? payload.HeightResolution : 0,
                MockTerrainResolution = ProceduralGeologyConstants.MockTerrainResolution,
                BiomeHeatmapResolution = BiomeHeatmapResolution,
                DistributionRuleCount = _distributionRuleCount,
                Seed = unchecked((uint)_currentSectorHash ^ (uint)(_currentSectorHash >> 32) ^ worldSeed),
                SectorHash = unchecked((ulong)_currentSectorHash),
                DominantBiomeId = _currentBiomeId,
                CopperBiomeId = CopperBiomeId,
                SlopeRejectNormalY = math.clamp(tuning.SurfaceNormalAlignmentTolerance, 0.05f, 1f),
                DropPodAbsolutePosition = _hasDropPodAnchor ? _dropPodAup.ToAbsoluteDouble3() : playerAbsolute,
                HasDropPodAnchor = _hasDropPodAnchor ? 1 : 0,
                CameraAbsolutePosition = playerAbsolute,
                CameraRuntimePosition = playerRuntimePosition,
                GlobalQualityWeight = qualityWeight,
                VisualClusterDensity = math.saturate(tuning.VisualClusterDensity),
                ClusterSpreadRadius = math.max(0.05f, tuning.ClusterSpreadRadius)
            };

            _spawnJob = job.Schedule(dependency);
            H8Memory.RegisterActiveJob(OwnerSystemId, _spawnJob);
            _spawnJobScheduled = true;
        }

        private static bool HasQuantizedHeightPayload(in GeologyHeightPayloadView payload)
        {
            return payload.HasQuantizedPayload != 0 &&
                   payload.HeightResolution > 1 &&
                   payload.HeightSamples.IsCreated &&
                   payload.HeightSamples.Length >= payload.HeightResolution * payload.HeightResolution;
        }

        private int ResolveIterationBudget(ProceduralGeologyVaultViews views)
        {
            int clamped = Mathf.Clamp(iterationsPerSector, 1, _oreCapacity);
            float density = views.GeologyTuning.IsCreated && views.GeologyTuning.Length > 0
                ? math.max(0.05f, views.GeologyTuning[0].BaseNodeDensity)
                : 1f;
            return math.clamp((int)math.round(clamped * density), 1, _oreCapacity);
        }

        private void EnsureDropPodAnchor(double3 playerAbsolute, float3 playerRuntimePosition)
        {
            if (_hasDropPodAnchor)
                return;

            _dropPodAup = AbsoluteUniversePosition.FromAbsolutePosition(playerAbsolute);
            _dropPodRuntimePosition = math.all(math.isfinite(playerRuntimePosition))
                ? playerRuntimePosition
                : _dropPodAup.ToRuntimeFloat3();
            _hasDropPodAnchor = math.all(math.isfinite(_dropPodRuntimePosition));
            _dropPodAnchorFromSignal = false;
        }

        private void DrainDropPodLandingSignals()
        {
            ReadOnlySpan<DropPodLandedSignal> dropPodSignals = SignalBus<DropPodLandedSignal>.GetFrameSnapshot();
            for (int i = 0; i < dropPodSignals.Length; i++)
            {
                DropPodLandedSignal signal = dropPodSignals[i];
                double3 absolute = signal.PositionAup.ToAbsoluteDouble3();
                if (!math.all(math.isfinite(absolute)))
                    continue;

                if (!IsNewDropPodSignal(in signal))
                    continue;

                float3 runtime = signal.PositionAup.ToRuntimeFloat3();
                if (!math.all(math.isfinite(runtime)))
                    continue;

                bool anchorChanged = !_hasDropPodAnchor || !_dropPodAnchorFromSignal || !AreAupEqual(in _dropPodAup, in signal.PositionAup);
                _dropPodAup = signal.PositionAup;
                _dropPodRuntimePosition = runtime;
                _lastDropPodSignalFrame = signal.Frame;
                _hasDropPodAnchor = true;
                _dropPodAnchorFromSignal = true;
                if (anchorChanged)
                    _dropPodAnchorRequiresGenerationRefresh = true;
            }
        }

        private bool IsNewDropPodSignal(in DropPodLandedSignal signal)
        {
            if (!_dropPodAnchorFromSignal)
                return true;

            if (IsNewAupShift(signal.Frame, _lastDropPodSignalFrame))
                return true;

            return signal.Frame == _lastDropPodSignalFrame && !AreAupEqual(in _dropPodAup, in signal.PositionAup);
        }

        private static bool AreAupEqual(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            return a.GridX == b.GridX &&
                   a.GridY == b.GridY &&
                   a.GridZ == b.GridZ &&
                   a.LocalX == b.LocalX &&
                   a.LocalY == b.LocalY &&
                   a.LocalZ == b.LocalZ;
        }

        private bool TryCompleteFinishedSpawnJob()
        {
            if (!_spawnJobScheduled)
                return false;
            if (!_spawnJob.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _spawnJob))
                return false;
            _spawnJobScheduled = false;
            UnlockVaultWriteBuffers();
            return true;
        }

        private void CommitSpawnJobOutput()
        {
            if (!TryOpenExistingVaultViews(out ProceduralGeologyVaultViews views))
                return;

            _activeOreCount = math.max(0, views.SpawnCounts[0]);
            _renderInstanceCount = math.clamp(views.SpawnCounts[1], 0, _oreCapacity);
            _localTitaniumCount = views.SpawnCounts.Length > 2 ? math.max(0, views.SpawnCounts[2]) : 0;
            _depletedCullCount = views.SpawnCounts.Length > 3 ? math.max(0, views.SpawnCounts[3]) : 0;
            _visualOnlyNodeCount = views.SpawnCounts.Length > 4 ? math.max(0, views.SpawnCounts[4]) : 0;
            _overflowCount = views.SpawnCounts.Length > 5 ? math.max(0, views.SpawnCounts[5]) : 0;
            _hzbCullCount = views.SpawnCounts.Length > 6 ? math.max(0, views.SpawnCounts[6]) : 0;
            if (_hasPendingRuntimeShift)
            {
                if (TryApplyRuntimeShiftWithFence(_pendingRuntimeShift, false))
                {
                    _pendingRuntimeShift = default;
                    _hasPendingRuntimeShift = false;
                }
            }

            _drawBounds = ResolveDrawBounds(views);
            _renderUploadDirty = true;
            UpdateIndirectArgsBuffer((uint)_renderInstanceCount);
            RefreshFirstLiveOreTelemetry(views);
            RunSelfAudit(views);
            if (!ValidateOreState(views))
                DumpTelemetry();
        }

        private void DiscardSpawnJobOutput()
        {
            _discardSpawnJobOutput = false;
            _pendingRuntimeShift = default;
            _hasPendingRuntimeShift = false;
            bool rewriteIndirectArgs = !_pendingDataVaultRebind;
            ClearPresentationState(false, rewriteIndirectArgs);
            if (!rewriteIndirectArgs)
                WriteIndirectArgsGpu(0u);
        }

        private void ClearPresentationState(bool forgetLoadedSector)
        {
            ClearPresentationState(forgetLoadedSector, true);
        }

        private void ClearDisabledPresentationState()
        {
            bool rewriteIndirectArgs = !_spawnJobScheduled && !_pendingDataVaultRebind;
            ClearPresentationState(true, rewriteIndirectArgs);
            if (!rewriteIndirectArgs)
                WriteIndirectArgsGpu(0u);
        }

        private void ClearPresentationState(bool forgetLoadedSector, bool rewriteIndirectArgs)
        {
            _activeOreCount = 0;
            _renderInstanceCount = 0;
            _localTitaniumCount = 0;
            _depletedCullCount = 0;
            _visualOnlyNodeCount = 0;
            _overflowCount = 0;
            _hzbCullCount = 0;
            _telemetryFirstOrePosition = default;
            _telemetryFirstNodeHash = 0u;
            _lastTelemetryFrameWritten = 0u;
            _drawBounds = new Bounds(ResolvePresentationCenter(), Vector3.one);
            _renderUploadDirty = false;
            if (forgetLoadedSector)
                _depletionLoaded = false;
            if (rewriteIndirectArgs)
                UpdateIndirectArgsBuffer(0u);
        }

        private void LoadDepletionMasksForCurrentSector()
        {
            if (!TryLockVaultDepletionMaskBuffers())
                return;

            try
            {
                if (!TryOpenExistingDepletionMaskViews(out ProceduralGeologyVaultViews views))
                    return;

                EnsureDepletionCacheInitialized(views);
                for (int word = 0; word < _depletionWordCount; word++)
                {
                    ulong key = NormalizeDepletionWordKey(_currentSectorHash, word);
                    int slot = FindDepletionCacheSlot(views, key, out bool found);
                    views.DepletionMasks[word] = found && (uint)slot < (uint)views.DepletionCacheMasks.Length
                        ? views.DepletionCacheMasks[slot]
                        : ulong.MaxValue;
                }

                _depletionLoaded = true;
            }
            finally
            {
                UnlockVaultWriteBuffers();
            }
        }

        private void StoreDepletionWord(ProceduralGeologyVaultViews views, int wordIndex, ulong mask)
        {
            EnsureDepletionCacheInitialized(views);
            if (!views.DepletionCacheKeys.IsCreated || !views.DepletionCacheMasks.IsCreated)
                return;

            ulong key = NormalizeDepletionWordKey(_currentSectorHash, wordIndex);
            int slot = FindDepletionCacheSlot(views, key, out bool found);
            if (slot < 0)
            {
                _overflowCount++;
                return;
            }

            views.DepletionCacheKeys[slot] = key;
            views.DepletionCacheMasks[slot] = mask;
            if (!found && views.DepletionCacheCount.IsCreated && views.DepletionCacheCount.Length > 0)
                views.DepletionCacheCount[0] = math.min(math.max(0, views.DepletionCacheCount[0]) + 1, views.DepletionCacheKeys.Length);
        }

        private void EnsureDepletionCacheInitialized(ProceduralGeologyVaultViews views)
        {
            if (_depletionCacheInitialized ||
                !views.DepletionCacheKeys.IsCreated ||
                !views.DepletionCacheMasks.IsCreated ||
                !views.DepletionCacheCount.IsCreated)
            {
                return;
            }

            int capacity = math.min(views.DepletionCacheKeys.Length, views.DepletionCacheMasks.Length);
            for (int i = 0; i < capacity; i++)
            {
                views.DepletionCacheKeys[i] = 0UL;
                views.DepletionCacheMasks[i] = 0UL;
            }

            views.DepletionCacheCount[0] = 0;
            _depletionCacheInitialized = true;
        }

        private int FindDepletionCacheSlot(ProceduralGeologyVaultViews views, ulong key, out bool found)
        {
            found = false;
            if (!views.DepletionCacheKeys.IsCreated || !views.DepletionCacheMasks.IsCreated)
                return -1;

            int capacity = math.min(views.DepletionCacheKeys.Length, views.DepletionCacheMasks.Length);
            if (capacity <= 0)
                return -1;

            uint start = ProceduralGeologyHash.Mix64To32(key);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = (int)((start + (uint)probe) % (uint)capacity);
                ulong existing = views.DepletionCacheKeys[index];
                if (existing == key)
                {
                    found = true;
                    return index;
                }

                if (existing == 0UL)
                    return index;
            }

            return -1;
        }

        private static ulong NormalizeDepletionWordKey(long sectorHash, int wordIndex)
        {
            ulong key = ComputeDepletionWordKey(sectorHash, wordIndex);
            return key == 0UL ? 1UL : key;
        }

        private bool MarkDepleted(ProceduralGeologyVaultViews views, int oreIndex)
        {
            if ((uint)oreIndex >= (uint)_renderInstanceCount ||
                !views.ResourceNodes.IsCreated ||
                !views.OreTypes.IsCreated ||
                !views.DepletionMasks.IsCreated ||
                (uint)oreIndex >= (uint)views.ResourceNodes.Length ||
                (uint)oreIndex >= (uint)views.OreTypes.Length ||
                views.OreTypes[oreIndex] == 0)
                return false;

            int deterministicSlot = views.CandidateSlots.IsCreated && (uint)oreIndex < (uint)views.CandidateSlots.Length
                ? views.CandidateSlots[oreIndex]
                : oreIndex;
            if (deterministicSlot < 0)
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromAbsolutePosition(views.ResourceNodes[oreIndex].SectorAUP);
            if (!positionAup.IsFinite())
                return false;

            int wordIndex = deterministicSlot >> 6;
            int bitIndex = deterministicSlot & 63;
            if ((uint)wordIndex >= (uint)views.DepletionMasks.Length)
                return false;

            ulong mask = views.DepletionMasks[wordIndex] & ~(1UL << bitIndex);
            views.DepletionMasks[wordIndex] = mask;
            StoreDepletionWord(views, wordIndex, mask);

            uint oreHash = ComputeOreHash(_currentSectorHash, deterministicSlot);
            int depletedOreType = views.OreTypes[oreIndex];
            uint frame = AdvanceSimulationFrameId();
            ItemAcquiredSignal acquiredSignal = default;
            acquiredSignal.PositionAup = positionAup;
            acquiredSignal.ItemHash = unchecked((uint)ResolveItemHash(depletedOreType));
            acquiredSignal.OreHash = oreHash;
            acquiredSignal.Quantity = 1;
            acquiredSignal.SourceKind = 2;
            acquiredSignal.Flags = 0;
            acquiredSignal.Frame = frame;
            GlobalSignals.Push(in acquiredSignal);

            ResourceDepletionDeltaSignal depletionSignal = default;
            depletionSignal.SectorHash = _currentSectorHash;
            depletionSignal.DepletionMask = mask;
            depletionSignal.OreHash = oreHash;
            depletionSignal.Frame = frame;
            depletionSignal.WordIndex = (ushort)wordIndex;
            depletionSignal.Operation = 1;
            depletionSignal.Flags = 0;
            GlobalSignals.Push(in depletionSignal);

            ClearRenderedSlot(views, deterministicSlot, oreIndex);
            _depletedCullCount = math.max(0, _depletedCullCount + 1);
            CompactRenderedRows(views);
            _drawBounds = ResolveDrawBounds(views);
            UpdateIndirectArgsBuffer((uint)_renderInstanceCount);
            RefreshFirstLiveOreTelemetry(views);
            _renderUploadDirty = true;
            WriteTelemetrySample(1u);
            return true;
        }

        private void ClearRenderedSlot(ProceduralGeologyVaultViews views, int deterministicSlot, int fallbackIndex)
        {
            if (!views.CandidateSlots.IsCreated)
            {
                ClearRenderedIndex(views, fallbackIndex);
                return;
            }

            for (int i = 0; i < _renderInstanceCount; i++)
            {
                if ((uint)i >= (uint)views.CandidateSlots.Length || views.CandidateSlots[i] != deterministicSlot)
                    continue;

                ClearRenderedIndex(views, i);
            }
        }

        private static void ClearRenderedIndex(ProceduralGeologyVaultViews views, int index)
        {
            if (index < 0)
                return;

            if (views.OreTypes.IsCreated && (uint)index < (uint)views.OreTypes.Length)
                views.OreTypes[index] = 0;
            if (views.OreMatrices.IsCreated && (uint)index < (uint)views.OreMatrices.Length)
                views.OreMatrices[index] = default;
            if (views.ResourceNodes.IsCreated && (uint)index < (uint)views.ResourceNodes.Length)
                views.ResourceNodes[index] = default;
            if (views.OrePositions.IsCreated && (uint)index < (uint)views.OrePositions.Length)
                views.OrePositions[index] = default;
            if (views.CandidateSlots.IsCreated && (uint)index < (uint)views.CandidateSlots.Length)
                views.CandidateSlots[index] = ClearedCandidateSlot;
        }

        private void CompactRenderedRows(ProceduralGeologyVaultViews views)
        {
            if (_renderInstanceCount <= 0 || !views.OreMatrices.IsCreated)
                return;

            int sourceCount = math.min(_renderInstanceCount, views.OreMatrices.Length);
            int writeIndex = 0;
            int authoritativeCount = 0;
            int visualOnlyCount = 0;
            int titaniumCount = 0;
            for (int readIndex = 0; readIndex < sourceCount; readIndex++)
            {
                if (!IsMatrixActiveForShader(views.OreMatrices[readIndex]))
                    continue;

                if (writeIndex != readIndex)
                    MoveRenderedRow(views, readIndex, writeIndex);

                int oreType = views.OreTypes.IsCreated && (uint)writeIndex < (uint)views.OreTypes.Length
                    ? views.OreTypes[writeIndex]
                    : 0;
                if (oreType != 0)
                {
                    authoritativeCount++;
                    if (oreType == OreTypeTitanium)
                        titaniumCount++;
                }
                else
                {
                    visualOnlyCount++;
                }

                writeIndex++;
            }

            for (int i = writeIndex; i < sourceCount; i++)
                ClearRenderedIndex(views, i);

            _renderInstanceCount = writeIndex;
            _activeOreCount = authoritativeCount;
            _visualOnlyNodeCount = visualOnlyCount;
            _localTitaniumCount = titaniumCount;
        }

        private static void MoveRenderedRow(ProceduralGeologyVaultViews views, int sourceIndex, int destinationIndex)
        {
            if (views.ResourceNodes.IsCreated &&
                (uint)sourceIndex < (uint)views.ResourceNodes.Length &&
                (uint)destinationIndex < (uint)views.ResourceNodes.Length)
            {
                views.ResourceNodes[destinationIndex] = views.ResourceNodes[sourceIndex];
            }

            if (views.OrePositions.IsCreated &&
                (uint)sourceIndex < (uint)views.OrePositions.Length &&
                (uint)destinationIndex < (uint)views.OrePositions.Length)
            {
                views.OrePositions[destinationIndex] = views.OrePositions[sourceIndex];
            }

            if (views.OreTypes.IsCreated &&
                (uint)sourceIndex < (uint)views.OreTypes.Length &&
                (uint)destinationIndex < (uint)views.OreTypes.Length)
            {
                views.OreTypes[destinationIndex] = views.OreTypes[sourceIndex];
            }

            if (views.OreMatrices.IsCreated &&
                (uint)sourceIndex < (uint)views.OreMatrices.Length &&
                (uint)destinationIndex < (uint)views.OreMatrices.Length)
            {
                views.OreMatrices[destinationIndex] = views.OreMatrices[sourceIndex];
            }

            if (views.CandidateSlots.IsCreated &&
                (uint)sourceIndex < (uint)views.CandidateSlots.Length &&
                (uint)destinationIndex < (uint)views.CandidateSlots.Length)
            {
                views.CandidateSlots[destinationIndex] = views.CandidateSlots[sourceIndex];
            }
        }

        private int ResolveItemHash(int oreType)
        {
            if (oreType == OreTypeCopper && copperItemHash != 0)
                return copperItemHash;
            if (oreType == OreTypeTitanium && titaniumItemHash != 0)
                return titaniumItemHash;
            if (oreType == OreTypeSilver && silverItemHash != 0)
                return silverItemHash;
            return basaltIronItemHash;
        }

        private bool DrainAupShiftSignals()
        {
            bool sawShift = false;
            float3 totalShift = default;
            uint newestShiftFrameId = _lastAppliedAupShiftFrameId;
            ReadOnlySpan<AupShiftSignal> shiftSignals = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shiftSignals.Length; i++)
            {
                AupShiftSignal signal = shiftSignals[i];
                if (!IsNewAupShift(signal.ShiftFrameId, _lastAppliedAupShiftFrameId))
                    continue;
                if (!math.all(math.isfinite(signal.ShiftMeters)))
                    continue;

                totalShift += signal.ShiftMeters;
                sawShift = true;
                if (IsNewAupShift(signal.ShiftFrameId, newestShiftFrameId))
                    newestShiftFrameId = signal.ShiftFrameId;
            }

            if (!sawShift)
            {
                if (_hasPendingRuntimeShift && !_spawnJobScheduled)
                {
                    if (!TryApplyRuntimeShiftWithFence(_pendingRuntimeShift, true))
                        return false;

                    _pendingRuntimeShift = default;
                    _hasPendingRuntimeShift = false;
                }

                return true;
            }

            if (_spawnJobScheduled)
            {
                if (!TryCompleteFinishedSpawnJob())
                {
                    _pendingRuntimeShift += totalShift;
                    _hasPendingRuntimeShift = true;
                    _lastAppliedAupShiftFrameId = newestShiftFrameId;
                    return false;
                }

                if (_discardSpawnJobOutput)
                {
                    DiscardSpawnJobOutput();
                    _lastAppliedAupShiftFrameId = newestShiftFrameId;
                    return true;
                }

                CommitSpawnJobOutput();
            }

            if (!TryApplyRuntimeShiftWithFence(totalShift, true))
            {
                _pendingRuntimeShift += totalShift;
                _hasPendingRuntimeShift = true;
                return false;
            }

            _lastAppliedAupShiftFrameId = newestShiftFrameId;
            return true;
        }

        private bool TryApplyRuntimeShiftWithFence(float3 totalShift, bool writeTelemetry)
        {
            if (!TryLockVaultRuntimeShiftBuffers())
                return false;

            try
            {
                if (!TryOpenExistingRuntimeShiftViews(out ProceduralGeologyVaultViews views))
                    return false;

                ApplyRuntimeShift(views, totalShift, writeTelemetry);
                return true;
            }
            finally
            {
                UnlockVaultWriteBuffers();
            }
        }

        private static bool IsNewAupShift(uint shiftFrameId, uint lastAppliedFrameId)
        {
            return shiftFrameId != lastAppliedFrameId && unchecked(shiftFrameId - lastAppliedFrameId) < 0x80000000u;
        }

        private void ApplyRuntimeShift(ProceduralGeologyVaultViews views, float3 totalShift, bool writeTelemetry)
        {
            if (!math.any(totalShift != new float3(0f)))
                return;

            if (_renderInstanceCount > 0 && views.OreMatrices.IsCreated)
            {
                int safeCount = math.min(_renderInstanceCount, views.OreMatrices.Length);
                if (views.OreTypes.IsCreated)
                    safeCount = math.min(safeCount, views.OreTypes.Length);

                for (int i = 0; i < safeCount; i++)
                {
                    bool authoritative = views.OreTypes.IsCreated && views.OreTypes[i] != 0;
                    if (authoritative && views.OrePositions.IsCreated && (uint)i < (uint)views.OrePositions.Length)
                        views.OrePositions[i] -= totalShift;

                    float4x4 matrix = views.OreMatrices[i];
                    if (matrix.c3.w != 0f)
                    {
                        float4 c3 = matrix.c3;
                        c3 = new float4(c3.x - totalShift.x, c3.y - totalShift.y, c3.z - totalShift.z, c3.w);
                        matrix.c3 = c3;
                        views.OreMatrices[i] = matrix;
                        if (views.ResourceNodes.IsCreated && (uint)i < (uint)views.ResourceNodes.Length)
                        {
                            ResourceNodeDTO node = views.ResourceNodes[i];
                            node.LocalMatrix = matrix;
                            views.ResourceNodes[i] = node;
                        }
                    }
                }
            }

            if (_hasDropPodAnchor)
                _dropPodRuntimePosition -= totalShift;
            if (_hasPlayerRuntimePosition)
                _lastPlayerRuntimePosition -= totalShift;
            if (_telemetryFirstNodeHash != 0u)
                _telemetryFirstOrePosition -= totalShift;

            _renderUploadDirty = true;
            if (writeTelemetry)
                WriteTelemetrySample(2u);
        }

        private void UploadRenderMatrices()
        {
            if (!TryOpenExistingBuffer(in _oreMatricesHandle, _oreCapacity, out NativeArray<float4x4> oreMatrices) ||
                _activeMatrixBuffer == null ||
                _renderInstanceCount <= 0)
            {
                _renderUploadDirty = false;
                return;
            }

            GraphicsBuffer writeBuffer = ReferenceEquals(_activeMatrixBuffer, _matrixBufferA) ? _matrixBufferB : _matrixBufferA;
            UploadNativeArray(writeBuffer, oreMatrices, _renderInstanceCount);
            _activeMatrixBuffer = writeBuffer;
            if (oreMaterial != null)
                oreMaterial.SetBuffer(_OreMatricesId, _activeMatrixBuffer);
            _renderUploadDirty = false;
        }

        private void RenderDormantOres()
        {
            float visualWeight = ResolveDormantOreVisualWeight();
            if (visualWeight <= 0.0001f || _renderInstanceCount <= 0 || oreMaterial == null || _argsBuffer == null)
                return;

            if (math.abs(_lastAppliedDormantOreVisualWeight - visualWeight) > 0.0001f)
            {
                oreMaterial.SetFloat(_QualityOverkillId, visualWeight);
                _lastAppliedDormantOreVisualWeight = visualWeight;
            }

            UnityEngine.Graphics.DrawProceduralIndirect(
                oreMaterial,
                _drawBounds,
                MeshTopology.Triangles,
                _argsBuffer,
                0,
                null,
                null,
                shadowCastingMode,
                receiveShadows,
                gameObject.layer);
        }

        private float ResolveDormantOreVisualWeight()
        {
            float quality = ResolveGlobalQualityWeight();
            float authoredWeight = math.saturate(math.isfinite(dormantOreVisualWeight) ? dormantOreVisualWeight : 1f);
            float easedQuality = math.smoothstep(0f, 1f, quality);
            return math.saturate(authoredWeight * easedQuality);
        }

        private void UpdateIndirectArgsBuffer(uint instanceCount)
        {
            if (_argsBuffer == null)
                return;

            bool releaseLock = false;
            if ((_lockedVaultBufferMask & (1 << 10)) == 0)
            {
                if (_lockedVaultBufferMask != 0 || !TryLockVaultIndirectArgsBuffer())
                    return;
                releaseLock = true;
            }

            try
            {
                if (!TryOpenExistingBuffer(
                        in _indirectArgsHandle,
                        IndirectArgsCount,
                        out NativeArray<GeologyIndirectArgsDTO> indirectArgs))
                {
                    return;
                }

                GeologyIndirectArgsDTO args = indirectArgs.Length > 0
                    ? indirectArgs[0]
                    : default;
                args.VertexCountPerInstance = OreProceduralVertexCount;
                args.InstanceCount = instanceCount;
                args.StartVertex = 0u;
                args.StartInstance = 0u;

                if (indirectArgs.Length > 0)
                    indirectArgs[0] = args;

                WriteIndirectArgsGpu(in args);
            }
            finally
            {
                if (releaseLock)
                    UnlockVaultWriteBuffers();
            }
        }

        private void WriteIndirectArgsGpu(uint instanceCount)
        {
            GeologyIndirectArgsDTO args = default;
            args.VertexCountPerInstance = OreProceduralVertexCount;
            args.InstanceCount = instanceCount;
            args.StartVertex = 0u;
            args.StartInstance = 0u;
            WriteIndirectArgsGpu(in args);
        }

        private void WriteIndirectArgsGpu(in GeologyIndirectArgsDTO args)
        {
            if (_argsBuffer == null)
                return;

            NativeArray<GeologyIndirectArgsDTO> argsWrite =
                _argsBuffer.LockBufferForWrite<GeologyIndirectArgsDTO>(0, 1);
            argsWrite[0] = args;
            _argsBuffer.UnlockBufferAfterWrite<GeologyIndirectArgsDTO>(1);
        }

        private Bounds ResolveDrawBounds(ProceduralGeologyVaultViews views)
        {
            if (_renderInstanceCount <= 0 || !views.OreMatrices.IsCreated)
                return new Bounds(ResolvePresentationCenter(), Vector3.one);

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            int validCount = 0;
            int scanCount = math.min(_renderInstanceCount, views.OreMatrices.Length);
            for (int i = 0; i < scanCount; i++)
            {
                if (!TryAccumulateMatrixBounds(views.OreMatrices[i], ref min, ref max))
                    continue;

                validCount++;
            }

            if (validCount == 0 || !math.all(math.isfinite(min)) || !math.all(math.isfinite(max)) || math.any(max < min))
                return new Bounds(ResolvePresentationCenter(), Vector3.one * sectorSizeMeters);

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(4f));
            return new Bounds(new Vector3(center.x, center.y, center.z), new Vector3(size.x, size.y, size.z));
        }

        private bool ValidateOreState(ProceduralGeologyVaultViews views)
        {
            if (_renderInstanceCount <= 0)
                return true;

            if (!views.OreMatrices.IsCreated || (uint)_renderInstanceCount > (uint)views.OreMatrices.Length)
                return false;

            bool validateAuthoritativePositions = views.OreTypes.IsCreated &&
                                                  views.OrePositions.IsCreated &&
                                                  (uint)_renderInstanceCount <= (uint)views.OreTypes.Length &&
                                                  (uint)_renderInstanceCount <= (uint)views.OrePositions.Length;
            if (!validateAuthoritativePositions)
                return false;

            for (int i = 0; i < _renderInstanceCount; i++)
            {
                float4x4 matrix = views.OreMatrices[i];
                if (!IsFiniteMatrix(matrix))
                    return false;

                if (IsMatrixActiveForShader(matrix) && !math.all(math.isfinite(new float3(matrix.c3.x, matrix.c3.y, matrix.c3.z))))
                    return false;

                if (views.OreTypes[i] != 0)
                {
                    if (!math.all(math.isfinite(views.OrePositions[i])))
                        return false;
                }
            }

            return true;
        }

        private void WriteTelemetrySample(uint flags)
        {
            bool releaseLock = false;
            if ((_lockedVaultBufferMask & (1 << 16)) == 0)
            {
                if (_lockedVaultBufferMask != 0 || !TryLockVaultTelemetryBuffer())
                    return;
                releaseLock = true;
            }

            try
            {
                if (!TryOpenExistingBuffer(
                        in _telemetryRingHandle,
                        ProceduralGeologyConstants.TelemetryFrames,
                        out NativeArray<GeologyGenerationTelemetryEntry> telemetryRing))
                    return;

                TryOpenExistingBuffer(
                    in _depletionMasksHandle,
                    _depletionWordCount,
                    out NativeArray<ulong> depletionMasks);
                WriteTelemetrySample(telemetryRing, depletionMasks, flags);
            }
            finally
            {
                if (releaseLock)
                    UnlockVaultWriteBuffers();
            }
        }

        private void WriteTelemetrySample(
            NativeArray<GeologyGenerationTelemetryEntry> telemetryRing,
            NativeArray<ulong> depletionMasks,
            uint flags)
        {
            uint frame = AdvanceSimulationFrameId();
            if (flags == 0u && _lastTelemetryFrameWritten == frame)
                return;
            _lastTelemetryFrameWritten = frame;

            int index = _telemetryWriteIndex;
            if ((uint)index >= (uint)telemetryRing.Length)
                index = 0;
            _telemetryWriteIndex = (index + 1) % math.max(1, telemetryRing.Length);
            float3 player = _hasPlayerRuntimePosition
                ? _lastPlayerRuntimePosition
                : default;
            float3 firstOre = _telemetryFirstOrePosition;
            uint telemetryFlags = flags;
            if (_hzbCullCount > 0)
                telemetryFlags |= ProceduralGeologyConstants.TelemetryFlagHzbCulled;

            telemetryRing[index] = new GeologyGenerationTelemetryEntry
            {
                SectorHash = _currentSectorHash,
                Frame = frame,
                AuthoritativeNodeCount = _activeOreCount,
                RenderNodeCount = _renderInstanceCount,
                DepletedCullCount = _depletedCullCount,
                VisualOnlyNodeCount = _visualOnlyNodeCount,
                OverflowCount = _overflowCount,
                GenerationBudgetUs = EstimateGenerationBudgetUs(),
                GlobalQualityWeight = ResolveGlobalQualityWeight(),
                Flags = telemetryFlags,
                FirstNodeHash = _telemetryFirstNodeHash,
                LayoutHash = ProceduralGeologyLayoutAudit.LayoutHash,
                ActiveDepletionWord0 = _depletionWordCount > 0 && depletionMasks.IsCreated ? (uint)depletionMasks[0] : 0u,
                StateHash = MixTelemetryState(_currentSectorHash, _activeOreCount, _renderInstanceCount, telemetryFlags, firstOre, player)
            };
        }

        private float EstimateGenerationBudgetUs()
        {
            int nodes = math.max(1, _renderInstanceCount + _depletedCullCount);
            return nodes * 0.045f;
        }

        private void RefreshFirstLiveOreTelemetry(ProceduralGeologyVaultViews views)
        {
            _telemetryFirstOrePosition = default;
            _telemetryFirstNodeHash = 0u;
            if (!views.CandidateSlots.IsCreated || !views.OreTypes.IsCreated || !views.OrePositions.IsCreated)
                return;

            int safeCount = math.min(_renderInstanceCount, views.OreTypes.Length);
            safeCount = math.min(safeCount, views.OrePositions.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (views.OreTypes[i] == 0)
                    continue;

                int deterministicSlot = (uint)i < (uint)views.CandidateSlots.Length ? views.CandidateSlots[i] : i;
                if (deterministicSlot < 0)
                    continue;

                _telemetryFirstOrePosition = views.OrePositions[i];
                _telemetryFirstNodeHash = ComputeOreHash(_currentSectorHash, deterministicSlot);
                return;
            }
        }

        private static ulong MixTelemetryState(long sectorHash, int authoritative, int rendered, uint flags, float3 firstOre, float3 player)
        {
            ulong hash = unchecked((ulong)sectorHash);
            hash ^= (uint)authoritative;
            hash *= 1099511628211UL;
            hash ^= (uint)rendered;
            hash *= 1099511628211UL;
            hash ^= flags;
            hash *= 1099511628211UL;
            hash ^= math.asuint(firstOre.x) ^ ((ulong)math.asuint(firstOre.y) << 16) ^ ((ulong)math.asuint(firstOre.z) << 32);
            hash *= 1099511628211UL;
            hash ^= math.asuint(player.x) ^ ((ulong)math.asuint(player.y) << 16) ^ ((ulong)math.asuint(player.z) << 32);
            return hash;
        }

        private uint AdvanceSimulationFrameId()
        {
            uint dispatcherFrame = TimeSliceScheduler.CurrentFrameId;
            if (dispatcherFrame != 0u)
            {
                _simulationFrameCounter = dispatcherFrame;
                return dispatcherFrame;
            }

            uint next = _simulationFrameCounter + 1u;
            if (next == 0u)
                next = 1u;
            _simulationFrameCounter = next;
            return next;
        }

        private void DumpTelemetry()
        {
            if (!TryOpenExistingBuffer(
                    in _telemetryRingHandle,
                    ProceduralGeologyConstants.TelemetryFrames,
                    out NativeArray<GeologyGenerationTelemetryEntry> telemetryRing))
                return;

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs"));
            TryWriteTelemetryDump(Path.Combine(root, PrimaryTelemetryDumpFile), telemetryRing);
            TryWriteTelemetryDump(Path.Combine(root, PromptTelemetryDumpFile), telemetryRing);
        }

        private void TryWriteTelemetryDump(string path, NativeArray<GeologyGenerationTelemetryEntry> telemetryRing)
        {
            try
            {
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read); // COLD ALLOC: FileStream[telemetry dump] — blackbox dump file writer — owner: ProceduralOreSpawner
                using BinaryWriter writer = new BinaryWriter(stream); // COLD ALLOC: BinaryWriter[telemetry dump] — blackbox binary row serializer — owner: ProceduralOreSpawner
                writer.Write(ProceduralGeologyConstants.DumpMagic);
                writer.Write(ProceduralGeologyConstants.DumpVersion);
                writer.Write(UnsafeUtility.SizeOf<GeologyGenerationTelemetryEntry>());
                writer.Write(telemetryRing.Length);
                for (int i = 0; i < telemetryRing.Length; i++)
                {
                    GeologyGenerationTelemetryEntry entry = telemetryRing[i];
                    writer.Write(entry.SectorHash);
                    writer.Write(entry.Frame);
                    writer.Write(entry.AuthoritativeNodeCount);
                    writer.Write(entry.RenderNodeCount);
                    writer.Write(entry.DepletedCullCount);
                    writer.Write(entry.VisualOnlyNodeCount);
                    writer.Write(entry.OverflowCount);
                    writer.Write(entry.GenerationBudgetUs);
                    writer.Write(entry.GlobalQualityWeight);
                    writer.Write(entry.Flags);
                    writer.Write(entry.FirstNodeHash);
                    writer.Write(entry.LayoutHash);
                    writer.Write(entry.ActiveDepletionWord0);
                    writer.Write(entry.StateHash);
                }
            }
            catch (Exception)
            {
                // Crash-path dump failure must not cascade into gameplay exception spam.
            }
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool TryAccumulateMatrixBounds(float4x4 matrix, ref float3 min, ref float3 max)
        {
            if (!IsFiniteMatrix(matrix) || !IsMatrixActiveForShader(matrix))
                return false;

            float3 center = new float3(matrix.c3.x, matrix.c3.y, matrix.c3.z);
            float3 extents =
                math.abs(new float3(matrix.c0.x, matrix.c0.y, matrix.c0.z)) * OreProceduralLocalExtentX +
                math.abs(new float3(matrix.c1.x, matrix.c1.y, matrix.c1.z)) * OreProceduralLocalExtentY +
                math.abs(new float3(matrix.c2.x, matrix.c2.y, matrix.c2.z)) * OreProceduralLocalExtentZ;
            if (!math.all(math.isfinite(center)) || !math.all(math.isfinite(extents)))
                return false;

            min = math.min(min, center - extents);
            max = math.max(max, center + extents);
            return true;
        }

        private static bool IsMatrixActiveForShader(float4x4 matrix)
        {
            float activity = math.abs(matrix.c0.x) + math.abs(matrix.c1.y) + math.abs(matrix.c2.z) + math.abs(matrix.c3.w);
            return math.isfinite(activity) && activity > 0.0001f;
        }

        private static bool IsFiniteMatrix(float4x4 value)
        {
            return math.all(math.isfinite(value.c0)) &&
                   math.all(math.isfinite(value.c1)) &&
                   math.all(math.isfinite(value.c2)) &&
                   math.all(math.isfinite(value.c3));
        }

        private static long ComputeAupSectorHash(int2 sector, uint seed)
        {
            ulong hash = 1469598103934665603UL;
            hash = (hash ^ unchecked((uint)sector.x)) * 1099511628211UL;
            hash = (hash ^ unchecked((uint)sector.y)) * 1099511628211UL;
            hash = (hash ^ seed) * 1099511628211UL;
            return unchecked((long)hash);
        }

        private static ulong ComputeDepletionWordKey(long sectorHash, int wordIndex)
        {
            ulong key = unchecked((ulong)sectorHash);
            key ^= (ulong)(uint)wordIndex * 0x9E3779B97F4A7C15UL;
            key ^= key >> 33;
            key *= 0xff51afd7ed558ccdUL;
            key ^= key >> 33;
            return key;
        }

        private static uint ComputeOreHash(long sectorHash, int oreIndex)
        {
            uint hash = unchecked((uint)sectorHash ^ (uint)(sectorHash >> 32) ^ (uint)oreIndex);
            return LcgHash(hash);
        }

        private static uint LcgHash(uint value)
        {
            value = value * 1664525u + 1013904223u;
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            return value;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawProceduralGeologyGizmos || !Application.isPlaying)
                return;

            if (!TryOpenExistingBuffer(
                    in _resourceNodesHandle,
                    _oreCapacity,
                    out NativeArray<ResourceNodeDTO> nodes))
            {
                return;
            }

            int count = _renderInstanceCount > 0 ? math.min(_renderInstanceCount, nodes.Length) : math.min(256, nodes.Length);
            Matrix4x4 previous = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            for (int i = 0; i < count; i++)
            {
                ResourceNodeDTO node = nodes[i];
                if (node.LocalMatrix.c3.w == 0f)
                    continue;

                uint type = node.ResourceTypeHash & ProceduralGeologyConstants.ResourceTypeMask;
                bool visualOnly = (node.ResourceTypeHash & ProceduralGeologyConstants.VisualOnlyTypeFlag) != 0u;
                Gizmos.color = ResolveGizmoColor(type, visualOnly);
                Gizmos.matrix = ToMatrix4x4(node.LocalMatrix);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            }

            Gizmos.matrix = previous;
            Gizmos.color = previousColor;
        }

        private static Color ResolveGizmoColor(uint resourceTypeHash, bool visualOnly)
        {
            Color color;
            if (resourceTypeHash == OreTypeCopper)
                color = new Color(0.95f, 0.45f, 0.16f, 1f);
            else if (resourceTypeHash == OreTypeTitanium)
                color = new Color(0.65f, 0.82f, 1f, 1f);
            else if (resourceTypeHash == OreTypeSilver)
                color = new Color(0.88f, 0.9f, 0.92f, 1f);
            else
                color = Color.yellow;

            if (visualOnly)
                color.a = 0.35f;
            return color;
        }

        private static Matrix4x4 ToMatrix4x4(float4x4 value)
        {
            Matrix4x4 matrix = default;
            matrix.SetColumn(0, new Vector4(value.c0.x, value.c0.y, value.c0.z, value.c0.w));
            matrix.SetColumn(1, new Vector4(value.c1.x, value.c1.y, value.c1.z, value.c1.w));
            matrix.SetColumn(2, new Vector4(value.c2.x, value.c2.y, value.c2.z, value.c2.w));
            matrix.SetColumn(3, new Vector4(value.c3.x, value.c3.y, value.c3.z, value.c3.w));
            return matrix;
        }
#endif

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer( // COLD ALLOC: GraphicsBuffer[structured] — ore matrix upload lock buffer — owner: ProceduralOreSpawner
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        private static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0)
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            unsafe
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<T>() * mapped.Length;
                if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(OwnerName);
            }

            destination.UnlockBufferAfterWrite<T>(safeCount);
        }

        private static int ResolveSafeWriteCount<T>(GraphicsBuffer destination, int sourceLength, int requestedCount) where T : struct
        {
            if (destination == null || requestedCount <= 0 || sourceLength <= 0 || destination.count <= 0)
                return 0;

            int stride = UnsafeUtility.SizeOf<T>();
            if (destination.stride != stride)
                return 0;

            return math.min(math.min(requestedCount, sourceLength), destination.count);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateResourceNodesJob : IJob
        {
            [NoAlias] public NativeArray<ResourceNodeDTO> ResourceNodes;
            [NoAlias] public NativeArray<float3> OrePositions;
            [NoAlias] public NativeArray<int> OreTypes;
            [NoAlias, ReadOnly] public NativeArray<ulong> DepletionMasks;
            [NoAlias] public NativeArray<float4x4> OreMatrices;
            [NoAlias] public NativeArray<int> SpawnCounts;
            [NoAlias] public NativeArray<int> CandidateSlots;
            [NoAlias] public NativeArray<GeologyIndirectArgsDTO> IndirectArgs;
            [NoAlias, ReadOnly] public NativeArray<ushort> HeightSamples;
            [NoAlias, ReadOnly] public NativeArray<GeologyTerrainSampleDTO> MockTerrainSdf;
            [NoAlias, ReadOnly] public NativeArray<byte> BiomeHeatmap;
            [NoAlias, ReadOnly] public NativeArray<GeologyDistributionRuleDTO> DistributionRules;
            [NoAlias, ReadOnly] public NativeArray<GeologyHzbTileDTO> HzbTiles;
            [NoAlias, ReadOnly] public NativeArray<GeologyHzbMetaDTO> HzbMeta;

            public int Capacity;
            public int ScanCount;
            public double2 SectorOrigin;
            public float SectorSize;
            public float3 TerrainPosition;
            public double2 TerrainOriginAbsoluteXZ;
            public float3 TerrainSize;
            public int HeightResolution;
            public int MockTerrainResolution;
            public int BiomeHeatmapResolution;
            public int DistributionRuleCount;
            public uint Seed;
            public ulong SectorHash;
            public int DominantBiomeId;
            public int CopperBiomeId;
            public float SlopeRejectNormalY;
            public double3 DropPodAbsolutePosition;
            public int HasDropPodAnchor;
            public double3 CameraAbsolutePosition;
            public float3 CameraRuntimePosition;
            public float GlobalQualityWeight;
            public float VisualClusterDensity;
            public float ClusterSpreadRadius;

            public void Execute()
            {
                int safeCapacity = ResolveSafeCapacity();
                int safeScanCount = math.clamp(ScanCount, 0, safeCapacity);
                int authoritativeCount = 0;
                int renderCount = 0;
                int depletedCullCount = 0;
                int visualOnlyCount = 0;
                int overflowCount = 0;
                int hzbCullCount = 0;
                int localTitaniumCount = 0;
                int previousOreType = OreTypeBasaltIron;
                float3 previousOrePosition = default;
                bool hasPreviousOre = false;

                for (int slot = 0; slot < safeScanCount; slot++)
                {
                    if (!IsBitSet(slot))
                    {
                        depletedCullCount++;
                        continue;
                    }

                    uint state = ResolveSlotSeed(slot);
                    float2 uv = new float2(Next01(ref state), Next01(ref state));
                    double x = SectorOrigin.x + (uv.x * SectorSize);
                    double z = SectorOrigin.y + (uv.y * SectorSize);
                    float y = SampleGrounding(x, z, out float3 normal);
                    if (normal.y < SlopeRejectNormalY)
                        continue;

                    double3 oreAbsolute = new double3(x, y + 0.08f, z);
                    float3 position = ResolveRuntimePosition(oreAbsolute);
                    float dropPodDistanceSq = ResolveDropPodDistanceSq(oreAbsolute);
                    int oreType = SelectOreType(ref state, SampleBiomeId(uv), y, dropPodDistanceSq, position, previousOrePosition, previousOreType, hasPreviousOre, slot);
                    if (renderCount >= safeCapacity)
                    {
                        overflowCount++;
                        continue;
                    }

                    float scale = SampleSlotScale(slot, ref state);
                    bool coreHzbOccluded = IsHzbOccluded(position, scale);
                    if (coreHzbOccluded && IsAuthoritativeHzbCullEnabled())
                    {
                        hzbCullCount++;
                        continue;
                    }

                    WriteNode(renderCount, position, normal, oreAbsolute, (uint)oreType, 1f, scale, slot);
                    OreTypes[renderCount] = oreType;
                    if (oreType == OreTypeTitanium)
                        localTitaniumCount++;
                    previousOreType = oreType;
                    previousOrePosition = position;
                    hasPreviousOre = true;
                    authoritativeCount++;
                    renderCount++;

                    int visualClusterCount = coreHzbOccluded ? 0 : SampleVisualClusterCount(ref state);
                    for (int cluster = 0; cluster < visualClusterCount; cluster++)
                    {
                        if (renderCount >= safeCapacity)
                        {
                            overflowCount++;
                            break;
                        }

                        float3 clusterPosition = position + SampleClusterOffset(ref state, normal, cluster);
                        float clusterScale = scale * (0.32f + 0.18f * Next01(ref state));
                        if (IsHzbOccluded(clusterPosition, clusterScale))
                        {
                            hzbCullCount++;
                            continue;
                        }

                        double3 clusterAbsolute = oreAbsolute + new double3(
                            clusterPosition.x - position.x,
                            clusterPosition.y - position.y,
                            clusterPosition.z - position.z);
                        WriteNode(
                            renderCount,
                            clusterPosition,
                            normal,
                            clusterAbsolute,
                            ((uint)oreType & ProceduralGeologyConstants.ResourceTypeMask) | ProceduralGeologyConstants.VisualOnlyTypeFlag,
                            0f,
                            clusterScale,
                            slot);
                        OreTypes[renderCount] = 0;
                        visualOnlyCount++;
                        renderCount++;
                    }
                }

                WriteCounter(0, authoritativeCount);
                WriteCounter(1, renderCount);
                WriteCounter(2, localTitaniumCount);
                WriteCounter(3, depletedCullCount);
                WriteCounter(4, visualOnlyCount);
                WriteCounter(5, overflowCount);
                WriteCounter(6, hzbCullCount);
                if (IndirectArgs.IsCreated && IndirectArgs.Length > 0)
                {
                    GeologyIndirectArgsDTO args = default;
                    args.VertexCountPerInstance = OreProceduralVertexCount;
                    args.InstanceCount = (uint)math.max(0, renderCount);
                    args.StartVertex = 0u;
                    args.StartInstance = 0u;
                    IndirectArgs[0] = args;
                }
            }

            private int ResolveSafeCapacity()
            {
                int safe = math.max(0, Capacity);
                safe = math.min(safe, ResourceNodes.IsCreated ? ResourceNodes.Length : 0);
                safe = math.min(safe, OrePositions.IsCreated ? OrePositions.Length : 0);
                safe = math.min(safe, OreTypes.IsCreated ? OreTypes.Length : 0);
                safe = math.min(safe, OreMatrices.IsCreated ? OreMatrices.Length : 0);
                safe = math.min(safe, CandidateSlots.IsCreated ? CandidateSlots.Length : 0);
                return safe;
            }

            private uint ResolveSlotSeed(int slot)
            {
                ulong slotKey = SectorHash ^ unchecked((ulong)(uint)slot * 0x9E3779B97F4A7C15UL) ^ Seed;
                uint lcgState = ProceduralGeologyHash.Mix64To32(slotKey ^ 0xD1B54A32D192ED03UL);
                lcgState ^= Seed * 747796405u;
                lcgState ^= unchecked((uint)slot) * 2891336453u;
                lcgState ^= lcgState >> 16;
                lcgState *= 2246822519u;
                lcgState ^= lcgState >> 13;
                lcgState *= 3266489917u;
                lcgState ^= lcgState >> 16;
                return lcgState == 0u ? 1u : lcgState;
            }

            private void WriteCounter(int index, int value)
            {
                if (SpawnCounts.IsCreated && (uint)index < (uint)SpawnCounts.Length)
                    SpawnCounts[index] = value;
            }

            private bool IsBitSet(int slot)
            {
                int word = slot >> 6;
                if ((uint)word >= (uint)DepletionMasks.Length)
                    return false;

                ulong bit = 1UL << (slot & 63);
                return (DepletionMasks[word] & bit) != 0UL;
            }

            private void WriteNode(int index, float3 position, float3 normal, double3 aup, uint resourceTypeHash, float yieldRemaining, float scale, int deterministicSlot)
            {
                float4x4 matrix = BuildAlignedMatrix(position, normal, scale, (uint)deterministicSlot);
                ResourceNodeDTO node = default;
                node.LocalMatrix = matrix;
                node.ResourceTypeHash = resourceTypeHash;
                node.YieldRemaining = yieldRemaining;
                node.SectorAUP = aup;
                ResourceNodes[index] = node;
                OrePositions[index] = position;
                OreMatrices[index] = matrix;
                CandidateSlots[index] = deterministicSlot;
            }

            private float3 ResolveRuntimePosition(double3 oreAbsolute)
            {
                double3 delta = oreAbsolute - CameraAbsolutePosition;
                delta = new double3(
                    math.clamp(delta.x, -100000.0, 100000.0),
                    math.clamp(delta.y, -100000.0, 100000.0),
                    math.clamp(delta.z, -100000.0, 100000.0));
                float3 localDelta = new float3((float)delta.x, (float)delta.y, (float)delta.z);
                return CameraRuntimePosition + localDelta;
            }

            private float ResolveDropPodDistanceSq(double3 oreAbsolute)
            {
                if (HasDropPodAnchor == 0 || !math.all(math.isfinite(DropPodAbsolutePosition)) || !math.all(math.isfinite(oreAbsolute)))
                    return FarDropPodDistanceSq;

                double3 delta = oreAbsolute - DropPodAbsolutePosition;
                delta = new double3(
                    math.clamp(delta.x, -100000.0, 100000.0),
                    math.clamp(delta.y, -100000.0, 100000.0),
                    math.clamp(delta.z, -100000.0, 100000.0));
                if (!math.all(math.isfinite(delta)))
                    return FarDropPodDistanceSq;

                float3 localDelta = new float3((float)delta.x, (float)delta.y, (float)delta.z);
                float distanceSq = math.lengthsq(localDelta);
                if (!math.isfinite(distanceSq) || distanceSq <= 0f)
                    return 0f;

                return distanceSq;
            }

            private int SelectOreType(
                ref uint state,
                int dominantBiomeId,
                float depth,
                float dropPodDistanceSq,
                float3 position,
                float3 previousPosition,
                int previousOreType,
                bool hasPreviousOre,
                int slot)
            {
                if (hasPreviousOre &&
                    previousOreType == OreTypeCopper &&
                    ShouldBiasCopperClump(ref state, position, previousPosition))
                {
                    return OreTypeCopper;
                }

                if (TrySelectDistributionRule(ref state, (uint)dominantBiomeId, depth, out int ruleOreType))
                    return ruleOreType;

                if (HasDropPodAnchor == 0)
                    return SelectLegacyOreType(ref state, dominantBiomeId);

                ResolveOreWeights(dropPodDistanceSq, out int titaniumWeight, out int copperWeight, out int silverWeight);
                int totalWeight = titaniumWeight + copperWeight + silverWeight;
                if (totalWeight != 100)
                {
                    titaniumWeight = 40;
                    copperWeight = 40;
                    silverWeight = 20;
                    totalWeight = 100;
                }

                int roll = MapToPercent(Next(ref state));
                if (roll < titaniumWeight)
                    return OreTypeTitanium;
                if (roll < titaniumWeight + copperWeight)
                    return OreTypeCopper;
                return silverWeight > 0 && totalWeight == 100 ? OreTypeSilver : OreTypeCopper;
            }

            private bool TrySelectDistributionRule(ref uint state, uint biomeHash, float depth, out int oreType)
            {
                oreType = 0;
                if (!DistributionRules.IsCreated || DistributionRuleCount <= 0)
                    return false;

                int limit = math.min(DistributionRuleCount, DistributionRules.Length);
                int totalWeight = 0;
                for (int i = 0; i < limit; i++)
                {
                    GeologyDistributionRuleDTO rule = DistributionRules[i];
                    if (rule.Weight <= 0)
                        continue;
                    if (rule.BiomeHash != 0u && rule.BiomeHash != biomeHash)
                        continue;
                    if (depth < rule.MinDepth || depth > rule.MaxDepth)
                        continue;
                    totalWeight += rule.Weight;
                }

                if (totalWeight <= 0)
                    return false;

                int roll = (int)(((ulong)Next(ref state) * (uint)totalWeight) >> 32);
                int cursor = 0;
                for (int i = 0; i < limit; i++)
                {
                    GeologyDistributionRuleDTO rule = DistributionRules[i];
                    if (rule.Weight <= 0)
                        continue;
                    if (rule.BiomeHash != 0u && rule.BiomeHash != biomeHash)
                        continue;
                    if (depth < rule.MinDepth || depth > rule.MaxDepth)
                        continue;

                    cursor += rule.Weight;
                    if (roll < cursor)
                    {
                        oreType = (int)(rule.ResourceTypeHash & ProceduralGeologyConstants.ResourceTypeMask);
                        return oreType != 0;
                    }
                }

                return false;
            }

            private bool ShouldBiasCopperClump(ref uint state, float3 position, float3 previousPosition)
            {
                float3 copperDelta = position - previousPosition;
                return math.lengthsq(copperDelta) <= CopperClumpDistanceSq &&
                       MapToPercent(Next(ref state)) < CopperClumpBiasPercent;
            }

            private static void ResolveOreWeights(float dropPodDistanceSq, out int titaniumWeight, out int copperWeight, out int silverWeight)
            {
                float safeDistanceSq = math.select(FarDropPodDistanceSq, dropPodDistanceSq, math.isfinite(dropPodDistanceSq));
                float gradient01 = math.saturate((safeDistanceSq - NearDropPodDistanceSq) * DropPodBandInvDistanceSq);
                float eased = math.smoothstep(0f, 1f, gradient01);
                titaniumWeight = math.clamp((int)math.round(math.lerp(70f, 40f, eased)), 0, 100);
                copperWeight = math.clamp((int)math.round(math.lerp(30f, 40f, eased)), 0, 100 - titaniumWeight);
                silverWeight = math.max(0, 100 - titaniumWeight - copperWeight);
            }

            private int SelectLegacyOreType(ref uint state, int dominantBiomeId)
            {
                uint roll = Next(ref state);
                if (dominantBiomeId == CopperBiomeId && (roll & 3u) == 0u)
                    return OreTypeCopper;
                if ((roll & 7u) == 0u)
                    return OreTypeTitanium;
                return OreTypeBasaltIron;
            }

            private int SampleBiomeId(float2 uv)
            {
                if (BiomeHeatmapResolution > 1 &&
                    BiomeHeatmap.IsCreated &&
                    BiomeHeatmap.Length >= BiomeHeatmapResolution * BiomeHeatmapResolution)
                {
                    int x = math.clamp((int)math.floor(math.saturate(uv.x) * BiomeHeatmapResolution), 0, BiomeHeatmapResolution - 1);
                    int z = math.clamp((int)math.floor(math.saturate(uv.y) * BiomeHeatmapResolution), 0, BiomeHeatmapResolution - 1);
                    return BiomeHeatmap[z * BiomeHeatmapResolution + x];
                }

                return DominantBiomeId;
            }

            private float SampleGrounding(double x, double z, out float3 normal)
            {
                float y = SampleHeight(x, z);
                normal = SampleNormal(x, z, y);
                float quality = math.saturate(GlobalQualityWeight);
                float refineBudget = math.smoothstep(0.25f, 1f, quality) * 2f;
                int refineCount = math.clamp((int)math.ceil(refineBudget), 0, 2);
                double probeX = x;
                double probeZ = z;

                for (int i = 0; i < refineCount; i++)
                {
                    float iterationWeight = math.saturate(refineBudget - i);
                    if (iterationWeight <= 0.0001f)
                        break;

                    float3 sampleNormal = SampleNormal(probeX, probeZ, y);
                    if (!math.all(math.isfinite(sampleNormal)))
                        break;

                    float slope = math.saturate(1f - sampleNormal.y);
                    double stepMeters = (double)(math.lerp(0f, 0.75f, slope) * iterationWeight);
                    probeX -= sampleNormal.x * stepMeters;
                    probeZ -= sampleNormal.z * stepMeters;
                    float refinedHeight = SampleHeight(probeX, probeZ);
                    if (!math.isfinite(refinedHeight))
                        break;

                    y = math.lerp(y, refinedHeight, iterationWeight);
                    normal = sampleNormal;
                }

                if (!math.isfinite(y))
                    y = TerrainPosition.y;
                if (!math.all(math.isfinite(normal)))
                    normal = new float3(0f, 1f, 0f);

                return y;
            }

            private float3 SampleNormal(double x, double z, float centerHeight)
            {
                const double step = 2.0;
                float hx = SampleHeight(x + step, z);
                float hz = SampleHeight(x, z + step);
                return SafeNormalize(new float3(centerHeight - hx, 2f, centerHeight - hz), new float3(0f, 1f, 0f));
            }

            private bool IsHzbOccluded(float3 localPosition, float radius)
            {
                if (!HzbTiles.IsCreated || !HzbMeta.IsCreated || HzbMeta.Length <= 0)
                    return false;

                GeologyHzbMetaDTO meta = HzbMeta[0];
                if ((meta.Flags & ProceduralGeologyConstants.HzbActiveFlag) == 0u ||
                    meta.Width <= 0 ||
                    meta.Height <= 0)
                {
                    return false;
                }

                float4 clip = math.mul(meta.CameraRelativeViewProjection, new float4(localPosition, 1f));
                if (!math.all(math.isfinite(clip)) || clip.w <= 0.0001f)
                    return false;

                float invW = math.rcp(math.max(math.abs(clip.w), 0.0001f));
                float2 uv = (clip.xy * invW * 0.5f) + 0.5f;
                if (!math.all(math.isfinite(uv)) || math.any(uv < 0f) || math.any(uv > 1f))
                    return false;

                int width = math.clamp(meta.Width, 1, math.max(1, HzbTiles.Length));
                int maxHeight = math.max(1, HzbTiles.Length / width);
                int height = math.clamp(meta.Height, 1, maxHeight);
                int x = math.clamp((int)math.floor(uv.x * width), 0, width - 1);
                int y = math.clamp((int)math.floor(uv.y * height), 0, height - 1);
                int index = math.clamp(x + (y * width), 0, HzbTiles.Length - 1);
                GeologyHzbTileDTO tile = HzbTiles[index];
                if (!math.isfinite(tile.Depth01) || tile.Depth01 <= 0f)
                    return false;

                float depth01 = math.saturate(clip.z * invW);
                float radiusBiasScale = math.select(0.0015f, meta.RadiusBiasScale, meta.RadiusBiasScale > 0f);
                float depthBias = math.select(0.0025f, meta.DepthBias, meta.DepthBias > 0f);
                float radiusBias = math.saturate(math.max(0f, radius) * radiusBiasScale);
                return depth01 - radiusBias > tile.Depth01 + depthBias;
            }

            private bool IsAuthoritativeHzbCullEnabled()
            {
                if (!HzbMeta.IsCreated || HzbMeta.Length <= 0)
                    return false;

                return (HzbMeta[0].Flags & ProceduralGeologyConstants.HzbCullAuthoritativeFlag) != 0u;
            }

            private float SampleHeight(double x, double z)
            {
                if (HeightResolution > 1 && HeightSamples.IsCreated && HeightSamples.Length >= HeightResolution * HeightResolution)
                {
                    double invSizeX = math.rcp(math.max(0.001, (double)TerrainSize.x));
                    double invSizeZ = math.rcp(math.max(0.001, (double)TerrainSize.z));
                    float u = (float)math.saturate((x - TerrainOriginAbsoluteXZ.x) * invSizeX);
                    float v = (float)math.saturate((z - TerrainOriginAbsoluteXZ.y) * invSizeZ);
                    int sx = math.clamp((int)math.round(u * (HeightResolution - 1)), 0, HeightResolution - 1);
                    int sz = math.clamp((int)math.round(v * (HeightResolution - 1)), 0, HeightResolution - 1);
                    ushort sample = HeightSamples[sz * HeightResolution + sx];
                    return TerrainPosition.y + (sample * (TerrainSize.y * (1f / 65535f)));
                }

                if (MockTerrainResolution > 1 && MockTerrainSdf.IsCreated && MockTerrainSdf.Length >= MockTerrainResolution * MockTerrainResolution)
                {
                    double invSize = math.rcp(math.max(0.001, (double)SectorSize));
                    double u = math.saturate((x - SectorOrigin.x) * invSize);
                    double v = math.saturate((z - SectorOrigin.y) * invSize);
                    int sx = math.clamp((int)math.round(u * (MockTerrainResolution - 1)), 0, MockTerrainResolution - 1);
                    int sz = math.clamp((int)math.round(v * (MockTerrainResolution - 1)), 0, MockTerrainResolution - 1);
                    return MockTerrainSdf[sz * MockTerrainResolution + sx].Height;
                }

                float waveA = TriangleSigned((float)((x * 0.037) + (z * 0.011) + Seed * 0.0001f));
                float waveB = TriangleSigned((float)((z * 0.023) - (x * 0.017)));
                return TerrainPosition.y + (waveA * 3.5f) + (waveB * 1.75f);
            }

            private int SampleVisualClusterCount(ref uint state)
            {
                float q = math.saturate(GlobalQualityWeight) * math.saturate(VisualClusterDensity);
                float curve = q * q * (3f - (2f * q));
                float scaled = curve * ProceduralGeologyConstants.MaxVisualClusterNodesPerCore;
                int count = (int)math.floor(scaled);
                float fractional = scaled - count;
                if (count < ProceduralGeologyConstants.MaxVisualClusterNodesPerCore && Next01(ref state) < fractional)
                    count++;
                return math.clamp(count, 0, ProceduralGeologyConstants.MaxVisualClusterNodesPerCore);
            }

            private float3 SampleClusterOffset(ref uint state, float3 normal, int clusterIndex)
            {
                float angle = (clusterIndex * 2.3999631f) + (Next01(ref state) * 0.35f);
                float radius = ClusterSpreadRadius * (0.35f + 0.65f * Next01(ref state));
                float3 tangent = BuildTangent(normal, (uint)clusterIndex);
                float3 bitangent = SafeNormalize(math.cross(normal, tangent), new float3(0f, 0f, 1f));
                return ((math.cos(angle) * tangent) + (math.sin(angle) * bitangent)) * radius + (normal * 0.04f);
            }

            private static float4x4 BuildAlignedMatrix(float3 position, float3 normal, float scale, uint spin)
            {
                normal = SafeNormalize(normal, new float3(0f, 1f, 0f));
                float3 tangent = BuildTangent(normal, spin);
                float3 bitangent = SafeNormalize(math.cross(normal, tangent), new float3(0f, 0f, 1f));
                return new float4x4(
                    new float4(tangent * scale, 0f),
                    new float4(normal * scale, 0f),
                    new float4(bitangent * scale, 0f),
                    new float4(position, 1f));
            }

            private static float3 BuildTangent(float3 normal, uint spin)
            {
                normal = SafeNormalize(normal, new float3(0f, 1f, 0f));

                float3 axis = math.abs(normal.y) > 0.85f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
                float3 tangent = SafeNormalize(math.cross(axis, normal), new float3(1f, 0f, 0f));
                float angle = (spin & 1023u) * (6.28318530718f / 1024f);
                float3 bitangent = SafeNormalize(math.cross(normal, tangent), new float3(0f, 0f, 1f));

                return SafeNormalize((tangent * math.cos(angle)) + (bitangent * math.sin(angle)), tangent);
            }

            private static float3 SafeNormalize(float3 value, float3 fallback)
            {
                if (!math.all(math.isfinite(value)))
                    return fallback;

                float lengthSq = math.lengthsq(value);
                if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                    return fallback;

                return value * math.rsqrt(math.max(lengthSq, 0.0001f));
            }

            private static int MapToPercent(uint value)
            {
                return (int)(((ulong)value * 100UL) >> 32);
            }

            private static float TriangleSigned(float phase)
            {
                float t = math.frac(phase);
                return 1f - math.abs((t * 4f) - 2f);
            }

            private static uint Next(ref uint state)
            {
                state = state * 1664525u + 1013904223u;
                return state;
            }

            private static float Next01(ref uint state)
            {
                return (Next(ref state) & 0x00FFFFFFu) * (1f / 16777216f);
            }

            private static float SampleSlotScale(int slot, ref uint state)
            {
                return 0.72f + ((Next(ref state) ^ (uint)slot) & 1023u) * (0.42f / 1023f);
            }
        }

    }
}
