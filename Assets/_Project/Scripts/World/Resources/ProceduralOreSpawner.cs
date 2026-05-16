using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
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
    /// Deterministic sector ore generator with SoA authority, indirect dormant rendering, and bounded collider hydration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralOreSpawner : MonoBehaviour, ISlowTickable, ILateFrameTickable, IDisposable, IWorldResourceSpawnerReadModel
    {
        private const string OwnerName = nameof(ProceduralOreSpawner);
        private const int DefaultOreCapacity = 2048;
        private const int MinimumOreCapacity = 64;
        private const int MaximumOreCapacity = 16384;
        private const int DefaultIterationsPerSector = 1024;
        private const int BiomeHeatmapResolution = 16;
        private const int DefaultProxyCapacity = 24;
        private const int TelemetryCapacity = 300;
        private const int CopperBiomeId = 4;
        private const float SlopeRejectNormalY = 0.5f;
        private const float ProxyHydrateDistanceSq = 9f;
        private const float ProxyDehydrateDistanceSq = 16f;
        private const int OreTypeBasaltIron = WorldOreTypeIds.BasaltIron;
        private const int OreTypeCopper = WorldOreTypeIds.Copper;
        private const int OreTypeTitanium = WorldOreTypeIds.Titanium;
        private const int OreTypeSilver = WorldOreTypeIds.Silver;
        private const float NearDropPodDistanceSq = 2500f;
        private const float FarDropPodDistanceSq = 10000f;
        private const float DropPodBandInvDistanceSq = 1f / (FarDropPodDistanceSq - NearDropPodDistanceSq);
        private const float CopperClumpDistanceSq = 4f;
        private const int CopperClumpBiasPercent = 85;
        private static readonly int _OreMatricesId = Shader.PropertyToID("_OreMatrices");

        [Header("Generation")]
        [SerializeField, Tooltip("Maximum deterministic ore slots retained for the active sector."), Min(MinimumOreCapacity)] private int maxOreCapacity = DefaultOreCapacity;
        [SerializeField, Tooltip("LCG candidate budget before quality-tier scaling."), Min(1)] private int iterationsPerSector = DefaultIterationsPerSector;
        [SerializeField, Tooltip("AUP sector width used for stable ore hashing."), Min(16f)] private float sectorSizeMeters = 128f;
        [SerializeField, Tooltip("Project seed mixed into every sector hash.")] private uint worldSeed = 0x48454338u;

        [Header("Runtime References")]
        [SerializeField, Tooltip("Optional cached player transform; auto-resolved through the world runtime helper if empty.")] private Transform playerTransform;
        [SerializeField, Tooltip("Optional cached MapMagic bridge for terrain height and biome sampling.")] private MapMagicBridge mapMagicBridge;

        [Header("Rendering")]
        [SerializeField, Tooltip("Shared dormant ore mesh for indirect draw and hydrated collider proxies.")] private Mesh oreMesh;
        [SerializeField, Tooltip("Indirect ore material with a StructuredBuffer named _OreMatrices.")] private Material oreMaterial;
        [SerializeField, Tooltip("Shadow mode used by dormant ore indirect draws.")] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;
        [SerializeField, Tooltip("Whether dormant ore indirect draws receive shadows.")] private bool receiveShadows = true;
        [SerializeField, Tooltip("Disables visual-only dormant ore rendering without changing authoritative ore state.")] private bool renderDormantOres = true;

        [Header("Yield Hashes")]
        [SerializeField, Tooltip("Inventory item hash emitted for basalt iron ore yields.")] private int basaltIronItemHash;
        [SerializeField, Tooltip("Inventory item hash emitted for copper ore yields.")] private int copperItemHash;
        [SerializeField, Tooltip("Inventory item hash emitted for titanium ore yields.")] private int titaniumItemHash;
        [SerializeField, Tooltip("Inventory item hash emitted for silver ore yields.")] private int silverItemHash;

        /// <summary>Authoritative runtime ore positions for the current deterministic sector.</summary>
        [NonSerialized] public NativeArray<float3> OrePositions;

        /// <summary>Authoritative ore type ids aligned by index with <see cref="OrePositions"/>.</summary>
        [NonSerialized] public NativeArray<int> OreTypes;

        /// <summary>Bit-packed live/depleted state for deterministic ore slots.</summary>
        [NonSerialized] public NativeArray<ulong> DepletionMasks;

        private NativeArray<float4x4> _oreMatrices;
        private NativeArray<byte> _biomeHeatmap;
        private NativeArray<int> _spawnCounts;
        private NativeArray<ProceduralOreTelemetryEntry> _telemetryRing;
        private NativeParallelHashMap<ulong, ulong> _sectorDepletionWords;

        private GraphicsBuffer _matrixBufferA;
        private GraphicsBuffer _matrixBufferB;
        private GraphicsBuffer _argsBuffer;
        private GraphicsBuffer _activeMatrixBuffer;

        private GameObject[] _proxyObjects;
        private MeshCollider[] _proxyColliders;
        private ProceduralOreProxy[] _proxyComponents;
        private int[] _proxyOreIndices;
        private int[] _oreProxySlots;

        private JobHandle _spawnJob;
        private bool _spawnJobScheduled;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;
        private bool _renderUploadDirty;
        private bool _depletionLoaded;
        private bool _discardSpawnJobOutput;
        private int _oreCapacity;
        private int _depletionWordCount;
        private int _renderInstanceCount;
        private int _activeOreCount;
        private int _activeProxyCount;
        private int _telemetryWriteIndex;
        private int2 _currentSector;
        private long _currentSectorHash;
        private int _currentBiomeId;
        private Bounds _drawBounds;
        private MapMagicBridge.QuantizedHeightmapPayload _heightPayload;
        private float3 _pendingRuntimeShift;
        private bool _hasPendingRuntimeShift;
        private uint _lastAppliedAupShiftFrameId;
        private AbsoluteUniversePosition _dropPodAup;
        private float3 _dropPodRuntimePosition;
        private uint _lastDropPodSignalFrame;
        private bool _hasDropPodAnchor;
        private bool _dropPodAnchorFromSignal;
        private bool _dropPodAnchorRequiresGenerationRefresh;
        private int _localTitaniumCount;

        /// <summary>Number of non-depleted ore slots currently alive in the active sector.</summary>
        public int ActiveOreCount => _activeOreCount;
        public int LocalTitaniumCount => _localTitaniumCount;
        /// <summary>Number of hydrated collider proxies currently active near the player.</summary>
        public int ActiveProxyCount => _activeProxyCount;
        /// <summary>Stable hash for the currently loaded AUP sector.</summary>
        public long CurrentSectorHash => _currentSectorHash;

        public bool TryGetOrePositions(out NativeArray<float3> orePositions, out int scanCount)
        {
            orePositions = OrePositions;
            scanCount = _renderInstanceCount;
            return OrePositions.IsCreated && _renderInstanceCount > 0 && _activeOreCount > 0;
        }

        public bool TryGetOreTypes(out NativeArray<int> oreTypes, out int scanCount)
        {
            oreTypes = OreTypes;
            scanCount = _renderInstanceCount;
            return OreTypes.IsCreated && _renderInstanceCount > 0 && _activeOreCount > 0;
        }

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            AllocateNativeState();
            EnsureRenderBuffers();
            EnsureProxyPool();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!OrePositions.IsCreated)
            {
                AllocateNativeState();
                EnsureRenderBuffers();
                EnsureProxyPool();
            }

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
            UnregisterDispatchers();
            DisableAllProxies();
            if (_spawnJobScheduled)
                _discardSpawnJobOutput = true;
            ClearPresentationState(true);
        }

        private void UnregisterDispatchers()
        {
            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }
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
        }

        /// <summary>Slow-tick sector scan, terrain projection refresh, AUP drift drain, and collider hydration.</summary>
        public void SlowTick()
        {
            if (!OrePositions.IsCreated)
                return;

            if (!DrainAupShiftSignals())
                return;

            DrainDropPodLandingSignals();

            if (!WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform) || playerTransform == null)
                return;

            WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
            RefreshSectorAndTerrain();
            if (_spawnJobScheduled)
                return;

            RefreshHydrationProxies(playerTransform.position);
            WriteTelemetrySample(0u);
        }

        /// <summary>Late-frame job retirement, matrix upload, and dormant ore indirect draw.</summary>
        public void LateFrameTick()
        {
            if (!OrePositions.IsCreated)
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
        }

        /// <summary>Releases native state and GPU buffers without forcing a scheduled spawn job to complete.</summary>
        public void Dispose()
        {
            UnregisterDispatchers();
            JobHandle disposeHandle = _spawnJobScheduled ? _spawnJob : default;
            _spawnJobScheduled = false;
            DisableAllProxies();

            disposeHandle = DisposeNativeArray(ref OrePositions, disposeHandle);
            disposeHandle = DisposeNativeArray(ref OreTypes, disposeHandle);
            disposeHandle = DisposeNativeArray(ref DepletionMasks, disposeHandle);
            disposeHandle = DisposeNativeArray(ref _oreMatrices, disposeHandle);
            disposeHandle = DisposeNativeArray(ref _biomeHeatmap, disposeHandle);
            disposeHandle = DisposeNativeArray(ref _spawnCounts, disposeHandle);
            disposeHandle = DisposeNativeArray(ref _telemetryRing, disposeHandle);

            if (_sectorDepletionWords.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(OwnerName, nameof(_sectorDepletionWords));
                disposeHandle = _sectorDepletionWords.Dispose(disposeHandle);
                _sectorDepletionWords = default;
            }

            ReleaseBuffer(ref _matrixBufferA);
            ReleaseBuffer(ref _matrixBufferB);
            ReleaseBuffer(ref _argsBuffer);
            _activeMatrixBuffer = null;
            ReleaseProxyPool();
            _oreProxySlots = null;
            _pendingRuntimeShift = default;
            _hasPendingRuntimeShift = false;
            _lastAppliedAupShiftFrameId = 0u;
            _dropPodAup = default;
            _dropPodRuntimePosition = default;
            _lastDropPodSignalFrame = 0u;
            _hasDropPodAnchor = false;
            _dropPodAnchorFromSignal = false;
            _dropPodAnchorRequiresGenerationRefresh = false;
            _localTitaniumCount = 0;
            _discardSpawnJobOutput = false;
        }

        private void AllocateNativeState()
        {
            _oreCapacity = Mathf.Clamp(maxOreCapacity, MinimumOreCapacity, MaximumOreCapacity);
            _depletionWordCount = Mathf.Max(1, (_oreCapacity + 63) >> 6);

            OrePositions = new NativeArray<float3>(_oreCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[oreCapacity] - SoA ore runtime positions - owner: ProceduralOreSpawner
            OreTypes = new NativeArray<int>(_oreCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[oreCapacity] - SoA ore type ids - owner: ProceduralOreSpawner
            DepletionMasks = new NativeArray<ulong>(_depletionWordCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<ulong>[wordCount] - ore depletion bitmasks - owner: ProceduralOreSpawner
            _oreMatrices = new NativeArray<float4x4>(_oreCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4x4>[oreCapacity] - indirect render matrices - owner: ProceduralOreSpawner
            _biomeHeatmap = new NativeArray<byte>(BiomeHeatmapResolution * BiomeHeatmapResolution, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[16x16] - ore biome heatmap lane - owner: ProceduralOreSpawner
            _spawnCounts = new NativeArray<int>(3, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[3] - Burst spawn counters incl. titanium telemetry - owner: ProceduralOreSpawner
            _telemetryRing = new NativeArray<ProceduralOreTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ProceduralOreTelemetryEntry>[300] - blackbox ring - owner: ProceduralOreSpawner
            _sectorDepletionWords = new NativeParallelHashMap<ulong, ulong>(_depletionWordCount * 16, Allocator.Persistent); // COLD ALLOC: NativeParallelHashMap<ulong,ulong> - session sector depletion cache - owner: ProceduralOreSpawner
            _oreProxySlots = new int[_oreCapacity]; // COLD ALLOC: int[oreCapacity] - ore index to hydrated proxy slot lookup - owner: ProceduralOreSpawner
            ResetOreProxySlots();

            NativeMemorySentinel.RegisterNativeArray(OrePositions, OwnerName, nameof(OrePositions), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(OreTypes, OwnerName, nameof(OreTypes), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(DepletionMasks, OwnerName, nameof(DepletionMasks), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_oreMatrices, OwnerName, nameof(_oreMatrices), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_biomeHeatmap, OwnerName, nameof(_biomeHeatmap), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_spawnCounts, OwnerName, nameof(_spawnCounts), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_telemetryRing, OwnerName, nameof(_telemetryRing), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeParallelHashMap(_sectorDepletionWords, OwnerName, nameof(_sectorDepletionWords), NativeAllocationLifetime.Scene);

            for (int i = 0; i < _depletionWordCount; i++)
                DepletionMasks[i] = ulong.MaxValue;
        }

        private void EnsureRenderBuffers()
        {
            if (_oreCapacity <= 0)
                return;

            if (_matrixBufferA == null)
                _matrixBufferA = CreateStructuredLockBuffer<float4x4>(_oreCapacity); // COLD ALLOC: GraphicsBuffer[oreCapacity] - ore matrix upload buffer A - owner: ProceduralOreSpawner
            if (_matrixBufferB == null)
                _matrixBufferB = CreateStructuredLockBuffer<float4x4>(_oreCapacity); // COLD ALLOC: GraphicsBuffer[oreCapacity] - ore matrix upload buffer B - owner: ProceduralOreSpawner
            if (_argsBuffer == null)
                _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - ore indirect draw args - owner: ProceduralOreSpawner

            _activeMatrixBuffer = _matrixBufferA;
            UpdateIndirectArgsBuffer(0u);
        }

        private void EnsureProxyPool()
        {
            if (_proxyObjects != null)
                return;

            _proxyObjects = new GameObject[DefaultProxyCapacity]; // COLD ALLOC: GameObject proxy slots - hydrated ore collider pool - owner: ProceduralOreSpawner
            _proxyColliders = new MeshCollider[DefaultProxyCapacity]; // COLD ALLOC: MeshCollider proxy slots - hydrated ore collider pool - owner: ProceduralOreSpawner
            _proxyComponents = new ProceduralOreProxy[DefaultProxyCapacity]; // COLD ALLOC: ProceduralOreProxy slots - hydrated ore collider pool - owner: ProceduralOreSpawner
            _proxyOreIndices = new int[DefaultProxyCapacity]; // COLD ALLOC: int proxy mapping - hydrated ore collider pool - owner: ProceduralOreSpawner

            if (oreMesh != null)
                UnityEngine.Physics.BakeMesh(oreMesh.GetEntityId(), false);

            for (int i = 0; i < DefaultProxyCapacity; i++)
            {
                GameObject proxy = new GameObject("ProceduralOreProxy");
                proxy.layer = gameObject.layer;
                proxy.isStatic = true;
                proxy.transform.SetParent(transform, false);
                MeshCollider collider = proxy.AddComponent<MeshCollider>();
                collider.sharedMesh = oreMesh;
                collider.convex = false;
                ProceduralOreProxy component = proxy.AddComponent<ProceduralOreProxy>();
                component.Bind(this, -1);
                proxy.SetActive(false);

                _proxyObjects[i] = proxy;
                _proxyColliders[i] = collider;
                _proxyComponents[i] = component;
                _proxyOreIndices[i] = -1;
            }
        }

        private void RefreshSectorAndTerrain()
        {
            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerTransform.position);
            double3 playerAbsolute = playerAup.ToAbsoluteDouble3();
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
                    LoadDepletionMasksForCurrentSector();
                }

                DisableAllProxies();
            }

            RefreshMapMagicPayload(playerAbsolute);

            if ((sectorChanged || anchorRefresh) && !_spawnJobScheduled)
            {
                _dropPodAnchorRequiresGenerationRefresh = false;
                ScheduleSpawnJob(playerAbsolute);
            }
        }

        private void RefreshMapMagicPayload(double3 playerAbsolute)
        {
            _heightPayload = default;
            _currentBiomeId = 0;

            if (mapMagicBridge == null)
            {
                FillBiomeHeatmap(0);
                return;
            }

            Vector3 absoluteProbe = new Vector3((float)playerAbsolute.x, (float)playerAbsolute.y, (float)playerAbsolute.z);
            if (mapMagicBridge.TryGetQuantizedHeightmapPayloadAUP(absoluteProbe, out MapMagicBridge.QuantizedHeightmapPayload payload) && payload.IsValid)
                _heightPayload = payload;

            if (mapMagicBridge.TryGetMatrixBiomeId(playerTransform.position.x, playerTransform.position.z, out int biomeId))
                _currentBiomeId = biomeId;
            else
                _currentBiomeId = mapMagicBridge.CurrentBiomeID;

            FillBiomeHeatmap(_currentBiomeId);
        }

        private void FillBiomeHeatmap(int biomeId)
        {
            if (!_biomeHeatmap.IsCreated)
                return;

            byte packed = (byte)math.clamp(biomeId, 0, byte.MaxValue);
            for (int i = 0; i < _biomeHeatmap.Length; i++)
                _biomeHeatmap[i] = packed;
        }

        private void ScheduleSpawnJob(double3 playerAbsolute)
        {
            EnsureDropPodAnchor(playerAbsolute);
            int scanCount = ResolveIterationBudget();
            float safeSectorSize = math.max(16f, sectorSizeMeters);
            float2 sectorOrigin = new float2(_currentSector.x * safeSectorSize, _currentSector.y * safeSectorSize);
            MapMagicBridge.QuantizedHeightmapPayload payload = _heightPayload;
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            ClearPresentationState(false);
            _drawBounds = new Bounds(transform.position, Vector3.one * safeSectorSize);
            _discardSpawnJobOutput = false;

            ProceduralOreSpawnJob job = new ProceduralOreSpawnJob
            {
                OrePositions = OrePositions,
                OreTypes = OreTypes,
                DepletionMasks = DepletionMasks,
                OreMatrices = _oreMatrices,
                SpawnCounts = _spawnCounts,
                HeightSamples = payload.IsValid ? payload.HeightSamples : default,
                BiomeHeatmap = _biomeHeatmap,
                Capacity = _oreCapacity,
                ScanCount = scanCount,
                SectorOrigin = sectorOrigin,
                SectorSize = safeSectorSize,
                TerrainPosition = payload.IsValid ? ToFloat3(payload.TerrainPosition) : new float3(sectorOrigin.x, (float)playerAbsolute.y, sectorOrigin.y),
                TerrainSize = payload.IsValid ? ToFloat3(payload.TerrainSize) : new float3(safeSectorSize, 64f, safeSectorSize),
                HeightResolution = payload.IsValid ? payload.HeightmapResolution : 0,
                BiomeHeatmapResolution = BiomeHeatmapResolution,
                Seed = unchecked((uint)_currentSectorHash ^ (uint)(_currentSectorHash >> 32) ^ worldSeed),
                DominantBiomeId = _currentBiomeId,
                CopperBiomeId = CopperBiomeId,
                SlopeRejectNormalY = SlopeRejectNormalY,
                DropPodAbsolutePosition = _hasDropPodAnchor ? _dropPodAup.ToAbsoluteDouble3() : playerAbsolute,
                HasDropPodAnchor = _hasDropPodAnchor ? 1 : 0,
                LowTierClumpMode = tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350 || tier == HectonQualityTier.Unknown ? 1 : 0
            };

            _spawnJob = job.Schedule();
            _spawnJobScheduled = true;
        }

        private int ResolveIterationBudget()
        {
            int clamped = Mathf.Clamp(iterationsPerSector, 1, _oreCapacity);
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350 || tier == HectonQualityTier.Unknown)
                return math.max(1, clamped >> 1);
            if (tier == HectonQualityTier.High)
                return math.min(_oreCapacity, clamped + (clamped >> 2));
            if (tier == HectonQualityTier.Ultra)
                return math.min(_oreCapacity, clamped + (clamped >> 1));

            return clamped;
        }

        private void EnsureDropPodAnchor(double3 playerAbsolute)
        {
            if (_hasDropPodAnchor)
                return;

            _dropPodAup = AbsoluteUniversePosition.FromAbsolutePosition(playerAbsolute);
            _dropPodRuntimePosition = playerTransform != null
                ? new float3(playerTransform.position.x, playerTransform.position.y, playerTransform.position.z)
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

            _spawnJob.Complete();
            _spawnJobScheduled = false;
            return true;
        }

        private void CommitSpawnJobOutput()
        {
            _activeOreCount = math.max(0, _spawnCounts[0]);
            _renderInstanceCount = math.clamp(_spawnCounts[1], 0, _oreCapacity);
            _localTitaniumCount = _spawnCounts.Length > 2 ? math.max(0, _spawnCounts[2]) : 0;
            if (_hasPendingRuntimeShift)
            {
                ApplyRuntimeShift(_pendingRuntimeShift, false);
                _pendingRuntimeShift = default;
                _hasPendingRuntimeShift = false;
            }

            _drawBounds = ResolveDrawBounds();
            _renderUploadDirty = true;
            UpdateIndirectArgsBuffer((uint)_renderInstanceCount);
            if (!ValidateOreState())
                DumpTelemetry();
        }

        private void DiscardSpawnJobOutput()
        {
            _discardSpawnJobOutput = false;
            _pendingRuntimeShift = default;
            _hasPendingRuntimeShift = false;
            ClearPresentationState(false);
        }

        private void ClearPresentationState(bool forgetLoadedSector)
        {
            _activeOreCount = 0;
            _renderInstanceCount = 0;
            _localTitaniumCount = 0;
            _drawBounds = new Bounds(transform.position, Vector3.one);
            _renderUploadDirty = false;
            if (forgetLoadedSector)
                _depletionLoaded = false;
            UpdateIndirectArgsBuffer(0u);
            ResetOreProxySlots();
        }

        private void LoadDepletionMasksForCurrentSector()
        {
            for (int word = 0; word < _depletionWordCount; word++)
            {
                ulong key = ComputeDepletionWordKey(_currentSectorHash, word);
                DepletionMasks[word] = _sectorDepletionWords.TryGetValue(key, out ulong mask) ? mask : ulong.MaxValue;
            }

            _depletionLoaded = true;
        }

        private void StoreDepletionWord(int wordIndex, ulong mask)
        {
            ulong key = ComputeDepletionWordKey(_currentSectorHash, wordIndex);
            _sectorDepletionWords.Remove(key);
            _sectorDepletionWords.TryAdd(key, mask);
        }

        private void RefreshHydrationProxies(Vector3 playerPosition)
        {
            if (_renderInstanceCount <= 0)
                return;

            float3 player = new float3(playerPosition.x, playerPosition.y, playerPosition.z);
            for (int i = 0; i < _renderInstanceCount; i++)
            {
                if (OreTypes[i] == 0)
                    continue;

                float distanceSq = math.distancesq(OrePositions[i], player);
                int existingSlot = ResolveProxySlot(i);
                if (existingSlot >= 0)
                {
                    if (distanceSq > ProxyDehydrateDistanceSq)
                        DisableProxy(existingSlot);
                    else
                        SuppressOreMatrix(i);
                    continue;
                }

                if (distanceSq < ProxyHydrateDistanceSq)
                    HydrateProxy(i);
            }
        }

        private void HydrateProxy(int oreIndex)
        {
            int slot = FindFreeProxySlot();
            if (slot < 0)
                return;

            GameObject proxy = _proxyObjects[slot];
            if (proxy == null)
                return;

            float3 orePosition = OrePositions[oreIndex];
            proxy.transform.position = new Vector3(orePosition.x, orePosition.y, orePosition.z);
            if (_proxyColliders[slot] != null)
                _proxyColliders[slot].sharedMesh = oreMesh;
            _proxyComponents[slot].Bind(this, oreIndex);
            _proxyOreIndices[slot] = oreIndex;
            if (_oreProxySlots != null && (uint)oreIndex < (uint)_oreProxySlots.Length)
                _oreProxySlots[oreIndex] = slot;
            proxy.SetActive(true);
            _activeProxyCount++;
            SuppressOreMatrix(oreIndex);
        }

        private void SuppressOreMatrix(int oreIndex)
        {
            if ((uint)oreIndex >= (uint)_oreMatrices.Length)
                return;

            _oreMatrices[oreIndex] = default;
            _renderUploadDirty = true;
        }

        private void DisableProxy(int slot)
        {
            if ((uint)slot >= (uint)_proxyObjects.Length)
                return;

            int oreIndex = _proxyOreIndices[slot];
            if ((uint)oreIndex < (uint)_renderInstanceCount && OreTypes[oreIndex] != 0)
                _oreMatrices[oreIndex] = BuildMatrix(OrePositions[oreIndex], ResolveOreScale(oreIndex));
            if (_oreProxySlots != null && (uint)oreIndex < (uint)_oreProxySlots.Length)
                _oreProxySlots[oreIndex] = -1;

            if (_proxyObjects[slot] != null)
                _proxyObjects[slot].SetActive(false);

            _proxyComponents[slot].Bind(this, -1);
            _proxyOreIndices[slot] = -1;
            _activeProxyCount = math.max(0, _activeProxyCount - 1);
            _renderUploadDirty = true;
        }

        private void DisableAllProxies()
        {
            if (_proxyObjects == null)
                return;

            for (int i = 0; i < _proxyObjects.Length; i++)
            {
                int oreIndex = _proxyOreIndices[i];
                if (_oreProxySlots != null && (uint)oreIndex < (uint)_oreProxySlots.Length)
                    _oreProxySlots[oreIndex] = -1;
                if (_proxyObjects[i] != null)
                    _proxyObjects[i].SetActive(false);
                if (_proxyComponents[i] != null)
                    _proxyComponents[i].Bind(this, -1);
                _proxyOreIndices[i] = -1;
            }

            _activeProxyCount = 0;
        }

        private int ResolveProxySlot(int oreIndex)
        {
            if (_oreProxySlots == null || (uint)oreIndex >= (uint)_oreProxySlots.Length)
                return -1;

            int slot = _oreProxySlots[oreIndex];
            if (_proxyOreIndices == null || (uint)slot >= (uint)_proxyOreIndices.Length)
            {
                _oreProxySlots[oreIndex] = -1;
                return -1;
            }

            if (_proxyOreIndices[slot] == oreIndex)
                return slot;

            _oreProxySlots[oreIndex] = -1;
            return -1;
        }

        private int FindFreeProxySlot()
        {
            if (_proxyOreIndices == null)
                return -1;

            for (int i = 0; i < _proxyOreIndices.Length; i++)
            {
                if (_proxyOreIndices[i] < 0)
                    return i;
            }

            return -1;
        }

        private void ResetOreProxySlots()
        {
            if (_oreProxySlots == null)
                return;

            for (int i = 0; i < _oreProxySlots.Length; i++)
                _oreProxySlots[i] = -1;
        }

        private void MarkDepleted(int oreIndex)
        {
            if ((uint)oreIndex >= (uint)_renderInstanceCount || OreTypes[oreIndex] == 0)
                return;

            int wordIndex = oreIndex >> 6;
            int bitIndex = oreIndex & 63;
            ulong mask = DepletionMasks[wordIndex] & ~(1UL << bitIndex);
            DepletionMasks[wordIndex] = mask;
            StoreDepletionWord(wordIndex, mask);

            uint oreHash = ComputeOreHash(_currentSectorHash, oreIndex);
            float3 position = OrePositions[oreIndex];
            int depletedOreType = OreTypes[oreIndex];
            ItemAcquiredSignal acquiredSignal = new ItemAcquiredSignal
            {
                PositionAup = AbsoluteUniversePosition.FromRuntimePosition(new Vector3(position.x, position.y, position.z)),
                ItemHash = unchecked((uint)ResolveItemHash(depletedOreType)),
                OreHash = oreHash,
                Quantity = 1,
                SourceKind = 2,
                Flags = 0,
                Frame = unchecked((uint)Time.frameCount)
            };
            GlobalSignals.Push(in acquiredSignal);

            ResourceDepletionDeltaSignal depletionSignal = new ResourceDepletionDeltaSignal
            {
                SectorHash = _currentSectorHash,
                DepletionMask = mask,
                OreHash = oreHash,
                Frame = unchecked((uint)Time.frameCount),
                WordIndex = (ushort)wordIndex,
                Operation = 1,
                Flags = 0
            };
            GlobalSignals.Push(in depletionSignal);

            OreTypes[oreIndex] = 0;
            _oreMatrices[oreIndex] = default;
            _activeOreCount = math.max(0, _activeOreCount - 1);
            if (depletedOreType == OreTypeTitanium)
                _localTitaniumCount = math.max(0, _localTitaniumCount - 1);
            int proxySlot = ResolveProxySlot(oreIndex);
            if (proxySlot >= 0)
                DisableProxy(proxySlot);
            _renderUploadDirty = true;
            WriteTelemetrySample(1u);
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
                return true;

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

            ApplyRuntimeShift(totalShift, true);
            _lastAppliedAupShiftFrameId = newestShiftFrameId;
            return true;
        }

        private static bool IsNewAupShift(uint shiftFrameId, uint lastAppliedFrameId)
        {
            return shiftFrameId != lastAppliedFrameId && unchecked(shiftFrameId - lastAppliedFrameId) < 0x80000000u;
        }

        private void ApplyRuntimeShift(float3 totalShift, bool writeTelemetry)
        {
            if (!math.any(totalShift != new float3(0f)))
                return;

            for (int i = 0; i < _renderInstanceCount; i++)
            {
                if (OreTypes[i] == 0)
                    continue;

                OrePositions[i] -= totalShift;
                if (_oreMatrices[i].c3.w != 0f)
                    _oreMatrices[i] = BuildMatrix(OrePositions[i], ResolveOreScale(i));
            }

            if (_hasDropPodAnchor)
                _dropPodRuntimePosition -= totalShift;

            if (_proxyObjects != null)
            {
                Vector3 shift = new Vector3(totalShift.x, totalShift.y, totalShift.z);
                for (int i = 0; i < _proxyObjects.Length; i++)
                {
                    if (_proxyOreIndices[i] >= 0 && _proxyObjects[i] != null)
                        _proxyObjects[i].transform.position -= shift;
                }
            }

            _renderUploadDirty = true;
            if (writeTelemetry)
                WriteTelemetrySample(2u);
        }

        private void UploadRenderMatrices()
        {
            if (_activeMatrixBuffer == null || !_oreMatrices.IsCreated || _renderInstanceCount <= 0)
            {
                _renderUploadDirty = false;
                return;
            }

            GraphicsBuffer writeBuffer = ReferenceEquals(_activeMatrixBuffer, _matrixBufferA) ? _matrixBufferB : _matrixBufferA;
            UploadNativeArray(writeBuffer, _oreMatrices, _renderInstanceCount);
            _activeMatrixBuffer = writeBuffer;
            if (oreMaterial != null)
                oreMaterial.SetBuffer(_OreMatricesId, _activeMatrixBuffer);
            _renderUploadDirty = false;
        }

        private void RenderDormantOres()
        {
            if (!renderDormantOres || _renderInstanceCount <= 0 || oreMesh == null || oreMaterial == null || _argsBuffer == null)
                return;

            RenderParams renderParams = new RenderParams(oreMaterial)
            {
                worldBounds = _drawBounds,
                layer = gameObject.layer,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = receiveShadows
            };
            Graphics.RenderMeshIndirect(renderParams, oreMesh, _argsBuffer, 1, 0);
        }

        private void UpdateIndirectArgsBuffer(uint instanceCount)
        {
            if (_argsBuffer == null)
                return;

            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _argsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = oreMesh != null ? oreMesh.GetIndexCount(0) : 0u,
                instanceCount = instanceCount,
                startIndex = oreMesh != null ? oreMesh.GetIndexStart(0) : 0u,
                baseVertexIndex = oreMesh != null ? (uint)math.max(0, oreMesh.GetBaseVertex(0)) : 0u,
                startInstance = 0u
            };
            _argsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
        }

        private Bounds ResolveDrawBounds()
        {
            if (_renderInstanceCount <= 0)
                return new Bounds(transform.position, Vector3.one);

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            for (int i = 0; i < _renderInstanceCount; i++)
            {
                if (OreTypes[i] == 0)
                    continue;

                float3 p = OrePositions[i];
                min = math.min(min, p);
                max = math.max(max, p);
            }

            if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max)) || math.any(max < min))
                return new Bounds(transform.position, Vector3.one * sectorSizeMeters);

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(4f));
            return new Bounds(new Vector3(center.x, center.y, center.z), new Vector3(size.x, size.y, size.z));
        }

        private bool ValidateOreState()
        {
            for (int i = 0; i < _renderInstanceCount; i++)
            {
                if (OreTypes[i] == 0)
                    continue;

                if (!math.all(math.isfinite(OrePositions[i])))
                    return false;
            }

            return true;
        }

        private void WriteTelemetrySample(uint flags)
        {
            if (!_telemetryRing.IsCreated)
                return;

            int index = _telemetryWriteIndex;
            _telemetryWriteIndex = (_telemetryWriteIndex + 1) % TelemetryCapacity;
            float3 player = playerTransform != null
                ? new float3(playerTransform.position.x, playerTransform.position.y, playerTransform.position.z)
                : default;
            float3 firstOre = default;
            if (_renderInstanceCount > 0)
            {
                for (int i = 0; i < _renderInstanceCount; i++)
                {
                    if (OreTypes[i] == 0)
                        continue;
                    firstOre = OrePositions[i];
                    break;
                }
            }

            _telemetryRing[index] = new ProceduralOreTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                SpawnedOres = _activeOreCount,
                ActiveProxies = _activeProxyCount,
                RenderInstanceCount = _renderInstanceCount,
                SectorHash = _currentSectorHash,
                PlayerPosition = player,
                FirstOrePosition = firstOre,
                Flags = flags,
                DepletionWord0 = _depletionWordCount > 0 ? (uint)DepletionMasks[0] : 0u,
                DepletionWord1 = _depletionWordCount > 0 ? (uint)(DepletionMasks[0] >> 32) : 0u,
                LocalTitaniumCount = _localTitaniumCount
            };
        }

        private void DumpTelemetry()
        {
            if (!_telemetryRing.IsCreated)
                return;

            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_WORLD_RESOURCE_SPAWNER.bin"));
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                for (int i = 0; i < _telemetryRing.Length; i++)
                {
                    ProceduralOreTelemetryEntry entry = _telemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.SpawnedOres);
                    writer.Write(entry.ActiveProxies);
                    writer.Write(entry.RenderInstanceCount);
                    writer.Write(entry.SectorHash);
                    writer.Write(entry.PlayerPosition.x);
                    writer.Write(entry.PlayerPosition.y);
                    writer.Write(entry.PlayerPosition.z);
                    writer.Write(entry.FirstOrePosition.x);
                    writer.Write(entry.FirstOrePosition.y);
                    writer.Write(entry.FirstOrePosition.z);
                    writer.Write(entry.Flags);
                    writer.Write(entry.DepletionWord0);
                    writer.Write(entry.DepletionWord1);
                    writer.Write(entry.LocalTitaniumCount);
                    writer.Write(entry.Reserved);
                }
            }
            catch (Exception)
            {
                // Crash-path dump failure must not cascade into gameplay exception spam.
            }
        }

        private static float ResolveOreScale(int oreIndex)
        {
            uint hash = LcgHash((uint)oreIndex ^ 0xA53C9E31u);
            return 0.72f + ((hash & 1023u) * (0.42f / 1023f));
        }

        private static float4x4 BuildMatrix(float3 position, float scale)
        {
            return float4x4.TRS(position, quaternion.identity, new float3(scale));
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
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

        private void ReleaseProxyPool()
        {
            if (_proxyObjects != null)
            {
                for (int i = 0; i < _proxyObjects.Length; i++)
                {
                    GameObject proxy = _proxyObjects[i];
                    if (proxy == null)
                        continue;

                    if (Application.isPlaying)
                        Destroy(proxy);
                    else
                        DestroyImmediate(proxy);
                }
            }

            _proxyObjects = null;
            _proxyColliders = null;
            _proxyComponents = null;
            _proxyOreIndices = null;
            _activeProxyCount = 0;
        }

        private static JobHandle DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return dependency;

            NativeMemorySentinel.UnregisterNativeArray(array);
            JobHandle disposeHandle = array.Dispose(dependency);
            array = default;
            return disposeHandle;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
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

        [StructLayout(LayoutKind.Sequential)]
        private struct ProceduralOreTelemetryEntry
        {
            public uint Frame;
            public int SpawnedOres;
            public int ActiveProxies;
            public int RenderInstanceCount;
            public long SectorHash;
            public float3 PlayerPosition;
            public float3 FirstOrePosition;
            public uint Flags;
            public uint DepletionWord0;
            public uint DepletionWord1;
            public int LocalTitaniumCount;
            public uint Reserved;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ProceduralOreSpawnJob : IJob
        {
            public NativeArray<float3> OrePositions;
            public NativeArray<int> OreTypes;
            [ReadOnly] public NativeArray<ulong> DepletionMasks;
            public NativeArray<float4x4> OreMatrices;
            public NativeArray<int> SpawnCounts;
            [ReadOnly] public NativeArray<ushort> HeightSamples;
            [ReadOnly] public NativeArray<byte> BiomeHeatmap;

            public int Capacity;
            public int ScanCount;
            public float2 SectorOrigin;
            public float SectorSize;
            public float3 TerrainPosition;
            public float3 TerrainSize;
            public int HeightResolution;
            public int BiomeHeatmapResolution;
            public uint Seed;
            public int DominantBiomeId;
            public int CopperBiomeId;
            public float SlopeRejectNormalY;
            public double3 DropPodAbsolutePosition;
            public int HasDropPodAnchor;
            public int LowTierClumpMode;

            public void Execute()
            {
                int safeCapacity = math.max(0, math.min(Capacity, OrePositions.Length));
                int safeScanCount = math.clamp(ScanCount, 0, safeCapacity);
                int activeCount = 0;
                int localTitaniumCount = 0;
                int previousOreType = OreTypeBasaltIron;
                float3 previousOrePosition = default;
                bool hasPreviousOre = false;

                for (int slot = 0; slot < safeCapacity; slot++)
                {
                    OreTypes[slot] = 0;
                    OrePositions[slot] = default;
                    OreMatrices[slot] = default;

                    if (slot >= safeScanCount || !IsBitSet(slot))
                        continue;

                    uint state = Seed ^ unchecked((uint)slot * 747796405u);
                    float2 uv = new float2(Next01(ref state), Next01(ref state));
                    float x = SectorOrigin.x + uv.x * SectorSize;
                    float z = SectorOrigin.y + uv.y * SectorSize;
                    float y = SampleHeight(x, z);
                    float normalY = SampleNormalY(x, z, y);
                    if (normalY < SlopeRejectNormalY)
                        continue;

                    float3 position = new float3(x, y + 0.08f, z);
                    double3 oreAbsolute = new double3(x, y, z);
                    float dropPodDistanceSq = ResolveDropPodDistanceSq(oreAbsolute);
                    int oreType = ResolveOreType(ref state, SampleBiomeId(uv), dropPodDistanceSq, position, previousOrePosition, previousOreType, hasPreviousOre, slot);
                    OrePositions[slot] = position;
                    OreTypes[slot] = oreType;
                    OreMatrices[slot] = BuildMatrix(position, ResolveSlotScale(slot, ref state));
                    if (oreType == OreTypeTitanium)
                        localTitaniumCount++;
                    previousOreType = oreType;
                    previousOrePosition = position;
                    hasPreviousOre = true;
                    activeCount++;
                }

                if (SpawnCounts.IsCreated && SpawnCounts.Length > 0)
                    SpawnCounts[0] = activeCount;
                if (SpawnCounts.IsCreated && SpawnCounts.Length > 1)
                    SpawnCounts[1] = safeScanCount;
                if (SpawnCounts.IsCreated && SpawnCounts.Length > 2)
                    SpawnCounts[2] = localTitaniumCount;
            }

            private bool IsBitSet(int slot)
            {
                int word = slot >> 6;
                if ((uint)word >= (uint)DepletionMasks.Length)
                    return false;

                ulong bit = 1UL << (slot & 63);
                return (DepletionMasks[word] & bit) != 0UL;
            }

            private float ResolveDropPodDistanceSq(double3 oreAbsolute)
            {
                if (HasDropPodAnchor == 0 || !math.all(math.isfinite(DropPodAbsolutePosition)) || !math.all(math.isfinite(oreAbsolute)))
                    return FarDropPodDistanceSq;

                double distanceSq = math.distancesq(oreAbsolute, DropPodAbsolutePosition);
                if (!math.isfinite(distanceSq) || distanceSq <= 0.0)
                    return 0f;

                return (float)math.min(distanceSq, (double)float.MaxValue);
            }

            private int ResolveOreType(
                ref uint state,
                int dominantBiomeId,
                float dropPodDistanceSq,
                float3 position,
                float3 previousPosition,
                int previousOreType,
                bool hasPreviousOre,
                int slot)
            {
                if (hasPreviousOre &&
                    previousOreType == OreTypeCopper &&
                    ShouldBiasCopperClump(ref state, position, previousPosition, slot))
                {
                    return OreTypeCopper;
                }

                if (HasDropPodAnchor == 0)
                    return ResolveLegacyOreType(ref state, dominantBiomeId);

                int titaniumWeight;
                int copperWeight;
                int silverWeight;
                ResolveOreWeights(dropPodDistanceSq, out titaniumWeight, out copperWeight, out silverWeight);

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

            private bool ShouldBiasCopperClump(
                ref uint state,
                float3 position,
                float3 previousPosition,
                int slot)
            {
                bool inClumpRange;
                if (LowTierClumpMode != 0)
                {
                    uint sectorHashModulus = (Seed ^ unchecked((uint)slot * 2654435761u)) & 3u;
                    inClumpRange = sectorHashModulus == 0u;
                }
                else
                {
                    inClumpRange = math.distancesq(position, previousPosition) <= CopperClumpDistanceSq;
                }

                return inClumpRange && MapToPercent(Next(ref state)) < CopperClumpBiasPercent;
            }

            private static void ResolveOreWeights(float dropPodDistanceSq, out int titaniumWeight, out int copperWeight, out int silverWeight)
            {
                if (dropPodDistanceSq < NearDropPodDistanceSq)
                {
                    titaniumWeight = 70;
                    copperWeight = 30;
                    silverWeight = 0;
                    return;
                }

                if (dropPodDistanceSq > FarDropPodDistanceSq)
                {
                    titaniumWeight = 40;
                    copperWeight = 40;
                    silverWeight = 20;
                    return;
                }

                float gradient01 = math.saturate((dropPodDistanceSq - NearDropPodDistanceSq) * DropPodBandInvDistanceSq);
                titaniumWeight = 70 - (int)math.round(30f * gradient01);
                copperWeight = 30 + (int)math.round(10f * gradient01);
                silverWeight = 100 - titaniumWeight - copperWeight;
            }

            private int ResolveLegacyOreType(ref uint state, int dominantBiomeId)
            {
                uint roll = Next(ref state);
                if (dominantBiomeId == CopperBiomeId && (roll & 3u) == 0u)
                    return OreTypeCopper;
                if ((roll & 7u) == 0u)
                    return OreTypeTitanium;
                return OreTypeBasaltIron;
            }

            private static int MapToPercent(uint value)
            {
                return (int)(((ulong)value * 100UL) >> 32);
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

            private float SampleNormalY(float x, float z, float centerHeight)
            {
                const float step = 2f;
                const float invStep = 0.5f;
                float hx = SampleHeight(x + step, z);
                float hz = SampleHeight(x, z + step);
                float dx = (hx - centerHeight) * invStep;
                float dz = (hz - centerHeight) * invStep;
                return math.rsqrt(1f + dx * dx + dz * dz);
            }

            private float SampleHeight(float x, float z)
            {
                if (HeightResolution > 1 && HeightSamples.IsCreated && HeightSamples.Length >= HeightResolution * HeightResolution)
                {
                    float invSizeX = math.rcp(math.max(0.001f, TerrainSize.x));
                    float invSizeZ = math.rcp(math.max(0.001f, TerrainSize.z));
                    float u = math.saturate((x - TerrainPosition.x) * invSizeX);
                    float v = math.saturate((z - TerrainPosition.z) * invSizeZ);
                    int sx = math.clamp((int)math.round(u * (HeightResolution - 1)), 0, HeightResolution - 1);
                    int sz = math.clamp((int)math.round(v * (HeightResolution - 1)), 0, HeightResolution - 1);
                    ushort sample = HeightSamples[sz * HeightResolution + sx];
                    return TerrainPosition.y + (sample * (TerrainSize.y * (1f / 65535f)));
                }

                float waveA = TriangleSigned((x * 0.037f) + (z * 0.011f) + Seed * 0.0001f);
                float waveB = TriangleSigned((z * 0.023f) - (x * 0.017f));
                return TerrainPosition.y + (waveA * 3.5f) + (waveB * 1.75f);
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

            private static float ResolveSlotScale(int slot, ref uint state)
            {
                return 0.72f + ((Next(ref state) ^ (uint)slot) & 1023u) * (0.42f / 1023f);
            }

            private static float4x4 BuildMatrix(float3 position, float scale)
            {
                return float4x4.TRS(position, quaternion.identity, new float3(scale));
            }
        }

        private sealed class ProceduralOreProxy : MonoBehaviour, ICuttable
        {
            private ProceduralOreSpawner _owner;
            private int _oreIndex;

            public void Bind(ProceduralOreSpawner owner, int oreIndex)
            {
                _owner = owner;
                _oreIndex = oreIndex;
            }

            public void ApplyCutDamage(float damage, Vector3 hitPoint)
            {
                if (damage <= 0f || _owner == null || _oreIndex < 0)
                    return;

                _owner.MarkDepleted(_oreIndex);
            }
        }
    }
}
