using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World.Terrain;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4029)]
    public sealed class WorldGenerativeGeologyTerrainSeamApplier : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
#pragma warning disable 0649
        private struct SeismicTrenchState
        {
            public long TrenchId;
            public Vector3 AbsoluteStart;
            public Vector3 AbsoluteEnd;
            public float DepthMeters;
            public float Slope;
            public float InfluenceRadius;
        }
#pragma warning restore 0649

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct TerrainSeamTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint TerrainHash;
            [FieldOffset(8)]
            public int PatchSampleCount;
            [FieldOffset(12)]
            public int PlanCount;
            [FieldOffset(16)]
            public float PatchCenterX;
            [FieldOffset(20)]
            public float PatchCenterZ;
            [FieldOffset(24)]
            public float MinHeight01;
            [FieldOffset(28)]
            public float MaxHeight01;
            [FieldOffset(32)]
            public float MaxBlend01;
            [FieldOffset(36)]
            public uint Flags;
            [FieldOffset(40)]
            public uint StateHash;
            [FieldOffset(44)]
            public uint Reserved0;
            [FieldOffset(48)]
            public uint Reserved1;
            [FieldOffset(52)]
            public uint Reserved2;
            [FieldOffset(56)]
            public uint Reserved3;
            [FieldOffset(60)]
            public uint Reserved4;
        }

        private const int TerrainPatchBridgeSampleBudgetMx350 = 131072;
        private const int TerrainChunkSignalSampleDrainBudget = 262144;
        private const int TerrainStateCapacity = 8;
        private const int TerrainChunkSignalDrainBudget = 8;
        private const int TerrainTileSnapshotCapacity = 32;
        private const int TerrainSeamBlackBoxCapacity = 300;
        private const float SeamExpensiveSamplingStartWeight = 0.30f;
        private const string HybridDumpPath = "Docs/AgentLogs/Dump_HYBRID_TERRAIN_BLENDER.bin";
        private const BufferID TerrainSeamBlackBoxBufferId = BufferID.WorldGenerativeGeologyTerrainSeamApplier_TerrainSeamBlackBoxBufferId;
        private const BufferID TerrainSeamNativePlansBufferId = BufferID.WorldGenerativeGeologyTerrainSeamApplier_TerrainSeamNativePlansBufferId;
        private const BufferID TerrainSeamPatchHeightsBufferId = BufferID.WorldGenerativeGeologyTerrainSeamApplier_TerrainSeamPatchHeightsBufferId;
        private const BufferID TerrainSeamBlendMaskBufferId = BufferID.WorldGenerativeGeologyTerrainSeamApplier_TerrainSeamBlendMaskBufferId;
        private const BufferID TerrainSeamNormalsBufferId = BufferID.WorldGenerativeGeologyTerrainSeamApplier_TerrainSeamNormalsBufferId;
        private const int TerrainSeamBaselineBufferIdBase = 0x531000;
        private const int TerrainSeamBaselineBufferIdMask = 0x000FFFFF;
        private const SystemID OwnerSystem = SystemID.TerrainSeams;
        private static readonly int HectonVoxelBlendMaskId = Shader.PropertyToID("_HectonVoxelBlendMask");
        private static readonly int HectonVoxelBlendMaskRectId = Shader.PropertyToID("_HectonVoxelBlendMaskRect");
        private static readonly int HectonVoxelBlendMaskParamsId = Shader.PropertyToID("_HectonVoxelBlendMaskParams");
        private static readonly ProfilerMarker TerrainSignalDrainMarker = new ProfilerMarker("H8.TerrainSeam.SignalDrain");
        private static readonly ProfilerMarker TerrainProjectionFenceMarker = new ProfilerMarker("H8.TerrainSeam.ProjectionFence");
        private static readonly ProfilerMarker TerrainBlendMaskUploadMarker = new ProfilerMarker("H8.TerrainSeam.BlendMaskUpload");
        private static readonly ProfilerMarker TerrainHeightmapWritebackMarker = new ProfilerMarker("H8.TerrainSeam.HeightmapWriteback");

        private static WorldGenerativeGeologyTerrainSeamApplier s_activeRuntimeInstance;

        internal static WorldGenerativeGeologyTerrainSeamApplier ActiveRuntimeInstance => s_activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            s_activeRuntimeInstance = null;
        }

        private sealed class TerrainApplyState
        {
            public UnityEngine.Terrain terrain;
            public UnityEngine.TerrainData terrainData;
            public VaultGenerationHandle<float> baselineHeightsHandle;
            public BufferID baselineHeightsBufferId;
            public int heightmapResolution;
            public RectInt previousRect;
            public bool hasPreviousRect;
            public float[,] patchBuffer;

            public void ReleaseBaseline()
            {
                baselineHeightsHandle = default;
                baselineHeightsBufferId = BufferID.Unknown;
            }
        }

        [Header("References")]
        [SerializeField] private WorldGenerativeGeologyIntegrationDirector integrationDirector;
        [SerializeField] private MapMagicBridge mapMagicBridge;

        [Header("Terrain Blend")]
        [SerializeField] private int maxAppliedPlans = 32;
        [SerializeField] private float minPlanWeight = 0.2f;
        [SerializeField] private float radiusPaddingMeters = 2f;
        [SerializeField] private float raiseStrength = 0.9f;
        [SerializeField] private float cutStrength = 0.8f;
        [SerializeField] private float rimSmoothing = 0.35f;
#pragma warning disable CS0414
        [SerializeField] private int maxActiveTrenches = 8;
#pragma warning restore CS0414
        [SerializeField] private float trenchRadiusPaddingMeters = 3f;
        [SerializeField] private float trenchRimBlendStrength = 0.55f;
        [SerializeField, Range(64, 1024)] private int voxelBlendMaskTextureSide = 512;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugReady;
        [SerializeField] private int _debugAppliedTerrains;
        [SerializeField] private int _debugAppliedPlans;
        [SerializeField] private int _debugRestoredTerrains;
        [SerializeField] private int _debugTopTerrainId;

        private readonly Dictionary<int, TerrainApplyState> _terrainStates = new Dictionary<int, TerrainApplyState>(8);
        private readonly Dictionary<int, List<WorldGenerativeGeologySeamPlan>> _plansByTerrain = new Dictionary<int, List<WorldGenerativeGeologySeamPlan>>(8);
        private readonly Dictionary<int, List<SeismicTrenchState>> _trenchesByTerrain = new Dictionary<int, List<SeismicTrenchState>>(8);
        private readonly HashSet<int> _touchedTerrainIds = new HashSet<int>(TerrainStateCapacity);
        private readonly List<int> _knownTerrainIds = new List<int>(8);
        private readonly List<SeismicTrenchState> _activeTrenches = new List<SeismicTrenchState>(8);
        private readonly List<int> _terrainBucketScratch = new List<int>(8);
        private readonly TerrainApplyState[] _terrainStatePool = new TerrainApplyState[TerrainStateCapacity]; // COLD ALLOC: TerrainApplyState[8] - fixed terrain seam state pool - owner: WorldGenerativeGeologyTerrainSeamApplier
        private readonly List<WorldGenerativeGeologySeamPlan>[] _planBucketPool = new List<WorldGenerativeGeologySeamPlan>[TerrainStateCapacity]; // COLD ALLOC: List<WorldGenerativeGeologySeamPlan>[8] - fixed per-terrain plan bucket refs - owner: WorldGenerativeGeologyTerrainSeamApplier
        private readonly List<SeismicTrenchState>[] _trenchBucketPool = new List<SeismicTrenchState>[TerrainStateCapacity]; // COLD ALLOC: List<SeismicTrenchState>[8] - fixed per-terrain trench bucket refs - owner: WorldGenerativeGeologyTerrainSeamApplier
        private MapMagicTerrainTileSnapshot[] _tileSnapshotScratch;
        private Texture2D _voxelBlendMaskTexture;
        private byte[] _voxelBlendMaskUploadBuffer;
        private IDataVault _dataVault;
        private VaultGenerationHandle<TerrainSeamTelemetryEntry> _terrainSeamBlackBoxHandle;
        private bool _registeredToTickManager;
        private bool _registeredToLateFrameTickManager;
        private bool _hotSwapRegistered;
        private bool _pendingVoxelBlendMaskGlobalClear;
        private UnityEngine.Terrain _pendingVoxelBlendMaskTerrain;
        private RectInt _pendingVoxelBlendMaskRect;
        private int _pendingVoxelBlendMaskSampleCount;
        private float _pendingVoxelBlendMaskExpensiveWeight;
        private bool _hasPendingVoxelBlendMaskUpload;
        private int _nextPatchTelemetryFrame;
        private int _blackBoxWriteIndex;
        private uint _seamFrameCounter;
        private int _debugDrainedTerrainChunkSignals;
        private int _debugDrainedTerrainChunkSamples;
        private int _debugHeightmapVaultSamples;
        private int _debugSkippedVaultHeightmapMismatches;
        private int _debugHybridBlendSamples;
        private int _debugHybridBlendPlans;
        private uint _vaultHeightmapTerrainHash;
        private uint _vaultHeightmapFrame;
        private int _vaultHeightmapResolution;
        private int _vaultHeightmapCacheRevision;
        private bool _voxelBlendMaskGlobalActive;
        private bool _voxelBlendMaskUploadedThisPass;
        private bool _terrainBucketPoolsInitialized;
        private float _debugGlobalQualityWeight = 1f;
        private float _debugSeamExpensiveWeight;
        private float _debugMaskDetailWeight;
        private bool _debugVisualSamplingSuppressed;
        private bool _debugMaskDetailActive;

        private void Awake()
        {
            EnsureHybridTerrainSeamStateCold();
            EnsureVoxelBlendMaskTextureCold();
            PublishActiveRuntimeInstance();
            RefreshDataVaultRoute();
            RefreshColdReferences();
            ReconcileTerrainSeams();
        }

        private void OnEnable()
        {
            EnsureHybridTerrainSeamStateCold();
            EnsureVoxelBlendMaskTextureCold();
            PublishActiveRuntimeInstance();
            RefreshDataVaultRoute();
            RefreshColdReferences();
            TryRegisterHotSwapListener();
            TryRegisterToTickManager();
            TryRegisterToLateFrameTickManager();
        }

        private void Start()
        {
            EnsureVoxelBlendMaskTextureCold();
            RefreshDataVaultRoute();
            RefreshColdReferences();
            TryRegisterToTickManager();
            TryRegisterToLateFrameTickManager();
            ReconcileTerrainSeams();
        }

        private void OnDisable()
        {
            TryUnregisterFromTickManager();
            TryUnregisterFromLateFrameTickManager();
            TryUnregisterHotSwapListener();
            ClearActiveRuntimeInstance();
            RestoreAllTerrains();
            DisposeTerrainStateNativeBuffers();
            DisableVoxelBlendMaskGlobal();
            FlushPendingVoxelBlendMaskGlobalClear();
        }

        private void OnDestroy()
        {
            TryUnregisterFromTickManager();
            TryUnregisterFromLateFrameTickManager();
            TryUnregisterHotSwapListener();
            ClearActiveRuntimeInstance();
            RestoreAllTerrains();
            DisposeTerrainStateNativeBuffers();
            DisposeHybridTerrainSeamState();
        }

        private void PublishActiveRuntimeInstance()
        {
            GlobalRegistry.RegisterGeologyTerrainSeamRuntime(this);
            s_activeRuntimeInstance = this;
        }

        private void ClearActiveRuntimeInstance()
        {
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            if (ReferenceEquals(GlobalRegistry.GeologyTerrainSeam, this))
                GlobalRegistry.UnregisterGeologyTerrainSeamRuntime(this);
        }

        public void SlowTick()
        {
            ProcessTerrainChunkGeneratedSignals();
            ReconcileTerrainSeams();
        }

        public void LateFrameTick()
        {
            FlushPendingVoxelBlendMaskUpload();
            FlushPendingVoxelBlendMaskGlobalClear();
        }

        public async Awaitable ProcessTerrainChunkGeneratedSignalsAsync(CancellationToken cancellationToken = default)
        {
            if (!HasHybridTerrainSeamState())
                return;

            int drained = 0;
            int copiedSamplesSinceYield = 0;
            while (drained < TerrainChunkSignalDrainBudget &&
                   TerrainChunkGeneratedEvents.TryDequeue(out TerrainChunkGeneratedSignal signal))
            {
                if (TryIngestSignalHeightmapToVault(in signal, out int copiedSamples))
                {
                    _debugHeightmapVaultSamples = copiedSamples;
                    _debugDrainedTerrainChunkSamples += copiedSamples;
                    copiedSamplesSinceYield += copiedSamples;
                }

                drained++;
                if ((drained == TerrainChunkSignalDrainBudget >> 1 ||
                     copiedSamplesSinceYield >= TerrainChunkSignalSampleDrainBudget) &&
                    TerrainChunkGeneratedEvents.PendingCount > 0)
                {
                    copiedSamplesSinceYield = 0;
                    await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
                }
            }

            _debugDrainedTerrainChunkSignals += drained;
        }

        public void SetIntegrationDirector(WorldGenerativeGeologyIntegrationDirector director)
        {
            integrationDirector = director;
        }

        public void RegisterSeismicTrench(
            long trenchId,
            Vector3 absoluteStart,
            Vector3 absoluteEnd,
            float trenchDepth,
            float trenchSlope,
            float influenceRadius)
        {
            _ = trenchId;
            _ = absoluteStart;
            _ = absoluteEnd;
            _ = trenchDepth;
            _ = trenchSlope;
            _ = influenceRadius;
            // SHINOBU_241: static trench truth is baked to .h8bin. Keep this compatibility entry inert so runtime
            // seismic payloads cannot write heightmap canyon cuts from SlowTick.
            return;
        }

        public void ReconcileTerrainSeams()
        {
            ClearBuckets();
            _touchedTerrainIds.Clear();
            _debugAppliedTerrains = 0;
            _debugAppliedPlans = 0;
            _debugRestoredTerrains = 0;
            _debugTopTerrainId = 0;
            _debugReady = false;
            _voxelBlendMaskUploadedThisPass = false;

            if (integrationDirector == null)
            {
                RestoreAllTerrains();
                DisableVoxelBlendMaskGlobal();
                return;
            }

            IReadOnlyList<WorldGenerativeGeologySeamPlan> plans = integrationDirector.ActivePlans;
            int acceptedPlans = 0;
            for (int i = 0; i < plans.Count; i++)
            {
                if (acceptedPlans >= Mathf.Max(1, maxAppliedPlans))
                    break;

                WorldGenerativeGeologySeamPlan plan = plans[i];
                if (!plan.RequiresTerrainBlend || !plan.hasTerrainSample || plan.planWeight < minPlanWeight)
                    continue;

                Vector3 runtimeWorldPosition = plan.RuntimeWorldPosition;
                UnityEngine.Terrain terrain = ResolveTerrainAt(runtimeWorldPosition.x, runtimeWorldPosition.z);
                if (terrain == null || terrain.terrainData == null)
                    continue;

                int terrainId = unchecked((int)EntityId.ToULong(terrain.GetEntityId()));
                EnsureTerrainState(terrain, terrainId);
                if (!_plansByTerrain.TryGetValue(terrainId, out List<WorldGenerativeGeologySeamPlan> terrainPlans) ||
                    terrainPlans == null)
                {
                    continue;
                }

                terrainPlans.Add(plan);
                _touchedTerrainIds.Add(terrainId);
                acceptedPlans++;
            }

            BucketActiveTrenches();

            for (int i = 0; i < _knownTerrainIds.Count; i++)
            {
                int terrainId = _knownTerrainIds[i];
                int terrainPlanCount = 0;
                int trenchCount = 0;
                List<WorldGenerativeGeologySeamPlan> terrainPlans = null;
                List<SeismicTrenchState> terrainTrenches = null;
                if (_plansByTerrain.TryGetValue(terrainId, out terrainPlans) && terrainPlans != null)
                    terrainPlanCount = terrainPlans.Count;

                if (_trenchesByTerrain.TryGetValue(terrainId, out terrainTrenches) && terrainTrenches != null)
                    trenchCount = terrainTrenches.Count;

                if (terrainPlanCount <= 0 && trenchCount <= 0)
                {
                    continue;
                }

                if (!_terrainStates.TryGetValue(terrainId, out TerrainApplyState state) || state == null || state.terrain == null)
                    continue;

                ApplyTerrainPlans(state, terrainPlans, terrainTrenches);
                _debugAppliedTerrains++;
                _debugAppliedPlans += terrainPlanCount + trenchCount;
                if (_debugTopTerrainId == 0)
                    _debugTopTerrainId = terrainId;
            }

            RestoreUntouchedTerrains();
            if (!_voxelBlendMaskUploadedThisPass)
                DisableVoxelBlendMaskGlobal();

            _debugReady = _debugAppliedPlans > 0;
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
        }

        private void TryRegisterToLateFrameTickManager()
        {
            if (_registeredToLateFrameTickManager)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            _registeredToLateFrameTickManager = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterFromLateFrameTickManager()
        {
            if (!_registeredToLateFrameTickManager)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredToLateFrameTickManager = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                _dataVault = currentService as IDataVault;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.MapMagicRuntime)
            {
                mapMagicBridge = currentService as MapMagicBridge;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterFromTickManager();
            TryUnregisterFromLateFrameTickManager();
            if (currentService != null && isActiveAndEnabled)
            {
                TryRegisterToTickManager();
                TryRegisterToLateFrameTickManager();
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
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

        private void ApplyTerrainPlans(
            TerrainApplyState state,
            List<WorldGenerativeGeologySeamPlan> plans,
            List<SeismicTrenchState> trenches)
        {
            UnityEngine.Terrain terrain = state.terrain;
            UnityEngine.TerrainData terrainData = terrain.terrainData;
            if (terrainData == null || !TryResolveBaselineHeights(state, out _))
                return;

            RectInt currentRect = default;
            bool hasCurrentRect = false;
            if (plans != null)
            {
                for (int i = 0; i < plans.Count; i++)
                {
                    RectInt planRect = BuildPlanRect(terrain, plans[i]);
                    if (planRect.width <= 0 || planRect.height <= 0)
                        continue;

                    currentRect = hasCurrentRect ? UnionRect(currentRect, planRect) : planRect;
                    hasCurrentRect = true;
                }
            }

            if (trenches != null)
            {
                for (int i = 0; i < trenches.Count; i++)
                {
                    RectInt trenchRect = BuildTrenchRect(terrain, trenches[i]);
                    if (trenchRect.width <= 0 || trenchRect.height <= 0)
                        continue;

                    currentRect = hasCurrentRect ? UnionRect(currentRect, trenchRect) : trenchRect;
                    hasCurrentRect = true;
                }
            }

            if (!hasCurrentRect)
            {
                RestoreTerrainState(state);
                return;
            }

            RectInt activeRect = ClampRect(currentRect, terrainData.heightmapResolution - 1, terrainData.heightmapResolution - 1);
            bool hasActiveRect = activeRect.width > 0 && activeRect.height > 0;
            RectInt applyRect = state.hasPreviousRect
                ? (hasActiveRect ? UnionRect(state.previousRect, activeRect) : state.previousRect)
                : activeRect;
            applyRect = ClampRect(applyRect, terrainData.heightmapResolution - 1, terrainData.heightmapResolution - 1);
            if (applyRect.width <= 0 || applyRect.height <= 0)
                return;

            if (Application.isPlaying)
            {
                state.previousRect = hasActiveRect ? activeRect : default;
                state.hasPreviousRect = hasActiveRect;
                return;
            }

            float[,] patch = PreparePatchBuffer(state, applyRect);
            bool previousHeightmapChanged = state.hasPreviousRect;
            bool currentHeightmapChanged = false;
            bool hybridApplied = TryApplyHybridTerrainProjection(state, applyRect, patch, plans, out bool hybridHeightmapChanged);
            currentHeightmapChanged |= hybridHeightmapChanged;
            if (plans != null)
            {
                for (int i = 0; i < plans.Count; i++)
                {
                    WorldGenerativeGeologySeamPlan plan = plans[i];
                    if (hybridApplied && IsHybridTerrainPlan(in plan))
                        continue;

                    currentHeightmapChanged |= ApplyPlanToPatch(terrain, applyRect, patch, plan);
                }
            }

            if (trenches != null)
            {
                for (int i = 0; i < trenches.Count; i++)
                    currentHeightmapChanged |= ApplyTrenchToPatch(terrain, applyRect, patch, trenches[i]);
            }

            if (previousHeightmapChanged || currentHeightmapChanged)
            {
                using (TerrainHeightmapWritebackMarker.Auto())
                {
                    terrainData.SetHeightsDelayLOD(applyRect.x, applyRect.y, patch);
                    terrainData.SyncHeightmap();
                }
            }

            if (currentHeightmapChanged)
            {
                state.previousRect = hasActiveRect ? activeRect : applyRect;
                state.hasPreviousRect = true;
            }
            else
            {
                state.previousRect = default;
                state.hasPreviousRect = false;
            }
        }

        private void ProcessTerrainChunkGeneratedSignals()
        {
            using (TerrainSignalDrainMarker.Auto())
            {
                if (!HasHybridTerrainSeamState())
                    return;

                // R96 FIX: the vault heightmap copy feeds only the hybrid projection path, which is
                // guarded by `if (Application.isPlaying) return;` further down this class — at
                // runtime the copy of up to a full 513x513 quantized heightmap per signal was pure
                // wasted SlowTick CPU with zero consumers. Keep DRAINING the signal queue at runtime
                // (producers must not saturate), but skip the dead vault ingest. Edit-mode behavior
                // is unchanged. Remove this guard together with the play-mode early-outs if the
                // runtime hybrid projection route is ever enabled.
                bool ingestToVault = !Application.isPlaying;
                int drained = 0;
                int copiedSamples = 0;
                while (drained < TerrainChunkSignalDrainBudget &&
                       TerrainChunkGeneratedEvents.TryDequeue(out TerrainChunkGeneratedSignal signal))
                {
                    if (ingestToVault && TryIngestSignalHeightmapToVault(in signal, out int signalSamples))
                    {
                        _debugHeightmapVaultSamples = signalSamples;
                        _debugDrainedTerrainChunkSamples += signalSamples;
                        copiedSamples += signalSamples;
                    }

                    drained++;
                    if (drained > 0 && copiedSamples >= TerrainChunkSignalSampleDrainBudget)
                        break;
                }

                _debugDrainedTerrainChunkSignals += drained;
            }
        }

        private bool TryIngestSignalHeightmapToVault(in TerrainChunkGeneratedSignal signal, out int copiedSampleCount)
        {
            copiedSampleCount = 0;
            if (!TerrainChunkGeneratedSignal.IsValid(in signal) || mapMagicBridge == null || _tileSnapshotScratch == null)
                return false;

            int snapshotCount = mapMagicBridge.CopyTerrainTileSnapshotsTo(_tileSnapshotScratch);
            bool matchedSnapshot = snapshotCount <= 0;
            for (int i = 0; i < snapshotCount; i++)
            {
                MapMagicTerrainTileSnapshot snapshot = _tileSnapshotScratch[i];
                if (!snapshot.IsValid)
                    continue;

                uint terrainHash = unchecked((uint)EntityId.ToULong(snapshot.Terrain.GetEntityId()));
                if (terrainHash != signal.TerrainEntityHash ||
                    snapshot.TileX != signal.ChunkX ||
                    snapshot.TileZ != signal.ChunkZ)
                {
                    continue;
                }

                matchedSnapshot = true;
                break;
            }

            if (!matchedSnapshot)
                return false;

            float sampleX = signal.TerrainPosition.x + signal.TerrainSize.x * 0.5f;
            float sampleZ = signal.TerrainPosition.z + signal.TerrainSize.z * 0.5f;
            if (!mapMagicBridge.TryGetQuantizedHeightmapPayload(sampleX, sampleZ, out MapMagicBridge.QuantizedHeightmapPayload payload) ||
                !MapMagicBridge.QuantizedHeightmapPayload.IsValid(in payload) ||
                payload.HeightmapResolution != signal.HeightmapResolution)
            {
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int requiredLength = payload.HeightmapResolution * payload.HeightmapResolution;
            if (!TryAcquireTerrainSeamWriteBuffer(
                    vault,
                    BufferID.TerrainSeamHeightmap,
                    requiredLength,
                    NativeArrayOptions.UninitializedMemory,
                    out VaultGenerationHandle<ushort> heightmapHandle,
                    out NativeArray<ushort> vaultHeights))
            {
                return false;
            }

            try
            {
                for (int i = 0; i < requiredLength; i++)
                    vaultHeights[i] = payload.HeightSamples[i];

                _vaultHeightmapTerrainHash = signal.TerrainEntityHash;
                _vaultHeightmapFrame = signal.Frame;
                _vaultHeightmapResolution = payload.HeightmapResolution;
                _vaultHeightmapCacheRevision = payload.CacheRevision;
                copiedSampleCount = requiredLength;
                return true;
            }
            finally
            {
                ReleaseTerrainSeamWriteBuffer(vault, in heightmapHandle);
            }
        }

        private void PopulateNativePlans(
            List<WorldGenerativeGeologySeamPlan> plans,
            int hybridPlanCount,
            in double3 terrainAbsolutePosition,
            Vector3 terrainPosition,
            NativeArray<HybridTerrainSeamPlanNative> nativePlans)
        {
            int writeIndex = 0;
            for (int i = 0; i < plans.Count && writeIndex < hybridPlanCount; i++)
            {
                WorldGenerativeGeologySeamPlan plan = plans[i];
                if (!IsHybridTerrainPlan(in plan))
                    continue;

                Vector3 contact = plan.TerrainContactPosition;
                Vector3 voxelCenter = plan.RuntimeVoxelVolumeCenter;
                Vector3 voxelSize = plan.voxelVolumeSize;
                float3 localContact = ResolveTerrainLocalContactPosition(
                    in plan,
                    in terrainAbsolutePosition,
                    contact,
                    terrainPosition);
                float3 localVoxelCenter = ResolveTerrainLocalVoxelCenter(
                    in plan,
                    in terrainAbsolutePosition,
                    voxelCenter,
                    terrainPosition);
                nativePlans[writeIndex++] = new HybridTerrainSeamPlanNative
                {
                    TerrainLocalContactPosition = localContact,
                    TerrainLocalVoxelCenter = localVoxelCenter,
                    VoxelSize = new float3(
                        Mathf.Max(0.5f, voxelSize.x),
                        Mathf.Max(0.5f, voxelSize.y),
                        Mathf.Max(0.5f, voxelSize.z)),
                    SeamBlendRadius = Mathf.Max(1f, plan.seamBlendRadius),
                    TerrainBlendWeight = Mathf.Clamp01(plan.terrainBlendWeight),
                    CaveBlendWeight = Mathf.Clamp01(plan.caveBlendWeight),
                    SuggestedTerrainRaise = Mathf.Max(0f, plan.suggestedTerrainRaise),
                    SuggestedTerrainCut = Mathf.Max(0f, plan.suggestedTerrainCut),
                    TerrainDelta = plan.terrainDelta,
                    RidgeSignal = Mathf.Clamp01(plan.ridgeSignal),
                    CanyonSignal = Mathf.Clamp01(plan.canyonSignal),
                    CompositionPotential = Mathf.Clamp01(plan.compositionPotential)
                };
            }
        }

        private JobHandle ScheduleHybridTerrainJobs(
            TerrainApplyState state,
            RectInt applyRect,
            int sampleCount,
            bool maskDetailActive,
            bool visualSamplingSuppressed,
            float globalQualityWeight,
            NativeArray<float> baselineHeights,
            NativeArray<ushort> quantizedHeightmap,
            NativeArray<HybridTerrainSeamPlanNative> nativePlans,
            NativeArray<float> patchHeights,
            NativeArray<byte> blendMask,
            NativeArray<float3> normals,
            Vector3 terrainSize)
        {
            HybridSdfHeightmapProjectionJob projectionJob = new HybridSdfHeightmapProjectionJob
            {
                BaselineHeights01 = baselineHeights,
                QuantizedHeightSamples = quantizedHeightmap,
                Plans = nativePlans,
                PatchHeights01 = patchHeights,
                BlendMask = blendMask,
                HeightmapResolution = state.heightmapResolution,
                PatchX = applyRect.x,
                PatchZ = applyRect.y,
                PatchWidth = applyRect.width,
                PatchHeight = applyRect.height,
                HeightmapInvMaxIndex = 1f / Mathf.Max(1, state.heightmapResolution - 1),
                TerrainPosition = float3.zero,
                TerrainSize = (float3)terrainSize,
                VisualSamplingSuppressed = visualSamplingSuppressed ? (byte)1 : (byte)0
            };
            InjectGlobalQualityWeight(ref projectionJob, globalQualityWeight);

            JobHandle projectionHandle = projectionJob.Schedule(sampleCount, 64);
            JobHandle finalHandle = projectionHandle;
            if (maskDetailActive)
            {
                float cellSizeX = terrainSize.x / Mathf.Max(1, state.heightmapResolution - 1);
                float cellSizeZ = terrainSize.z / Mathf.Max(1, state.heightmapResolution - 1);
                HybridTerrainSeamNormalJob normalJob = new HybridTerrainSeamNormalJob
                {
                    PatchHeights01 = patchHeights,
                    Normals = normals,
                    PatchWidth = applyRect.width,
                    PatchHeight = applyRect.height,
                    CellSizeX = cellSizeX,
                    CellSizeZ = cellSizeZ,
                    HeightScale = terrainSize.y
                };
                HybridTerrainSeamMaskDetailJob detailJob = new HybridTerrainSeamMaskDetailJob
                {
                    Normals = normals,
                    BlendMask = blendMask,
                    EnableDetail = 1
                };
                InjectGlobalQualityWeight(ref detailJob, globalQualityWeight);

                JobHandle normalHandle = normalJob.Schedule(sampleCount, 64, projectionHandle);
                finalHandle = detailJob.Schedule(sampleCount, 64, normalHandle);
            }

            return finalHandle;
        }

        private void ApplyHybridTerrainPatchResults(
            RectInt applyRect,
            NativeArray<float> patchHeights,
            NativeArray<byte> blendMask,
            float[,] patch,
            out float minHeight01,
            out float maxHeight01,
            out float maxBlend01,
            out bool faulted,
            out bool changedHeightSample)
        {
            minHeight01 = 1f;
            maxHeight01 = 0f;
            maxBlend01 = 0f;
            faulted = false;
            changedHeightSample = false;
            for (int z = 0; z < applyRect.height; z++)
            {
                int rowOffset = z * applyRect.width;
                for (int x = 0; x < applyRect.width; x++)
                {
                    int index = rowOffset + x;
                    float height01 = patchHeights[index];
                    float sourceHeight01 = patch[z, x];
                    if (float.IsNaN(height01) || float.IsInfinity(height01))
                    {
                        height01 = sourceHeight01;
                        faulted = true;
                    }

                    height01 = Mathf.Clamp01(height01);
                    changedHeightSample |= Mathf.Abs(height01 - sourceHeight01) > 0.00001f;
                    patch[z, x] = height01;
                    minHeight01 = Mathf.Min(minHeight01, height01);
                    maxHeight01 = Mathf.Max(maxHeight01, height01);
                    maxBlend01 = Mathf.Max(maxBlend01, blendMask[index] * (1f / 255f));
                }
            }
        }

        private bool TryApplyHybridTerrainProjection(
            TerrainApplyState state,
            RectInt applyRect,
            float[,] patch,
            List<WorldGenerativeGeologySeamPlan> plans,
            out bool heightmapChanged)
        {
            heightmapChanged = false;
            if (state == null ||
                state.terrain == null ||
                state.terrainData == null ||
                patch == null ||
                plans == null ||
                plans.Count <= 0 ||
                !TryResolveBaselineHeights(state, out NativeArray<float> baselineHeights))
            {
                return false;
            }

            int hybridPlanCount = CountHybridTerrainPlans(plans);
            if (hybridPlanCount <= 0)
                return false;

            int sampleCount = applyRect.width * applyRect.height;
            if (sampleCount <= 0)
                return false;

            UnityEngine.Terrain terrain = state.terrain;
            UnityEngine.TerrainData terrainData = state.terrainData;
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            if (!TryResolveRuntimeAbsoluteDouble(terrainPosition, out double3 terrainAbsolutePosition))
                return false;

            float globalQualityWeight = ResolveGlobalQualityWeight();
            float seamExpensiveWeight = ResolveSeamExpensiveWeight(globalQualityWeight);
            float maskDetailWeight = ResolveMaskDetailWeight(globalQualityWeight);
            bool visualSamplingSuppressed = seamExpensiveWeight <= 0.0001f;
            bool maskDetailActive = maskDetailWeight > 0.0001f;
            NativeArray<ushort> quantizedHeightmap = default;
            bool usedVaultHeightmap = TryResolveVaultHeightmap(state, out quantizedHeightmap);

            NativeArray<HybridTerrainSeamPlanNative> nativePlans = default;
            NativeArray<float> patchHeights = default;
            NativeArray<byte> blendMask = default;
            NativeArray<float3> normals = default;
            ulong terrainSeamGuardMask = 0UL;
            IDataVault terrainSeamGuardVault = null;

            try
            {
                if (!TryResolveHybridTerrainScratchBuffers(
                        hybridPlanCount,
                        sampleCount,
                        maskDetailActive,
                        out nativePlans,
                        out patchHeights,
                        out blendMask,
                        out normals))
                {
                    return false;
                }

                if (!TryAcquireHybridTerrainMutationGuard(
                        state,
                        maskDetailActive,
                        quantizedHeightmap.IsCreated,
                        out terrainSeamGuardMask,
                        out terrainSeamGuardVault))
                {
                    return false;
                }

                PopulateNativePlans(plans, hybridPlanCount, in terrainAbsolutePosition, terrainPosition, nativePlans);

                JobHandle finalHandle = ScheduleHybridTerrainJobs(
                    state,
                    applyRect,
                    sampleCount,
                    maskDetailActive,
                    visualSamplingSuppressed,
                    globalQualityWeight,
                    baselineHeights,
                    quantizedHeightmap,
                    nativePlans,
                    patchHeights,
                    blendMask,
                    normals,
                    terrainSize);

                // COLD SYNC JOB: Unity Terrain SetHeightsDelayLOD requires CPU patch data; this path is bounded to SlowTick/chunk seam work, not frame Tick.
                using (TerrainProjectionFenceMarker.Auto())
                {
                    DispatcherJobFence.BeginLateFrameSwapWindow();
                    try
                    {
                        DispatcherJobFence.TryComplete(ref finalHandle, forceComplete: true);
                    }
                    finally
                    {
                        DispatcherJobFence.EndLateFrameSwapWindow();
                    }
                }

                ApplyHybridTerrainPatchResults(
                    applyRect,
                    patchHeights,
                    blendMask,
                    patch,
                    out float minHeight01,
                    out float maxHeight01,
                    out float maxBlend01,
                    out bool faulted,
                    out bool changedHeightSample);

                QueueVoxelBlendMaskTextureUpload(terrain, applyRect, blendMask, seamExpensiveWeight);
                ReleaseHybridTerrainMutationGuard(terrainSeamGuardVault, terrainSeamGuardMask);
                terrainSeamGuardMask = 0UL;
                terrainSeamGuardVault = null;

                uint terrainHash = unchecked((uint)EntityId.ToULong(terrain.GetEntityId()));
                uint stateHash = HashTerrainSeamState(
                    terrainHash,
                    applyRect,
                    sampleCount,
                    hybridPlanCount,
                    minHeight01,
                    maxHeight01,
                    maxBlend01);
                uint seamFrame = AdvanceTerrainSeamFrame();
                RecordTerrainSeamBlackBox(
                    seamFrame,
                    terrain,
                    terrainHash,
                    applyRect,
                    sampleCount,
                    hybridPlanCount,
                    minHeight01,
                    maxHeight01,
                    maxBlend01,
                    visualSamplingSuppressed,
                    maskDetailActive,
                    usedVaultHeightmap,
                    changedHeightSample,
                    faulted,
                    stateHash);
                if (changedHeightSample)
                {
                    PublishTerrainPatchVoxelModifiedEvent(
                        seamFrame,
                        terrain,
                        applyRect,
                        minHeight01,
                        maxHeight01,
                        stateHash);
                }
                WorldGenerativeGeologyTelemetry.TryPublishTerrainSeamsBlended(
                    sampleCount,
                    hybridPlanCount,
                    seamExpensiveWeight);

                _debugHybridBlendSamples = sampleCount;
                _debugHybridBlendPlans = hybridPlanCount;
                _debugGlobalQualityWeight = globalQualityWeight;
                _debugSeamExpensiveWeight = seamExpensiveWeight;
                _debugMaskDetailWeight = maskDetailWeight;
                _debugVisualSamplingSuppressed = visualSamplingSuppressed;
                _debugMaskDetailActive = maskDetailActive;

                if (faulted)
                    DumpTerrainSeamBlackBox();

                heightmapChanged = changedHeightSample;
                return true;
            }
            finally
            {
                ReleaseHybridTerrainMutationGuard(terrainSeamGuardVault, terrainSeamGuardMask);
                normals = default;
                blendMask = default;
                patchHeights = default;
                nativePlans = default;
            }
        }

        private bool TryResolveVaultHeightmap(TerrainApplyState state, out NativeArray<ushort> quantizedHeightmap)
        {
            quantizedHeightmap = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                state == null ||
                state.terrain == null ||
                state.heightmapResolution <= 1)
                return false;

            uint terrainHash = unchecked((uint)EntityId.ToULong(state.terrain.GetEntityId()));
            if (terrainHash == 0u ||
                terrainHash != _vaultHeightmapTerrainHash ||
                _vaultHeightmapResolution != state.heightmapResolution)
            {
                _debugSkippedVaultHeightmapMismatches++;
                return false;
            }

            int requiredLength = state.heightmapResolution * state.heightmapResolution;
            return TryOpenExistingTerrainSeamBuffer(
                vault,
                BufferID.TerrainSeamHeightmap,
                requiredLength,
                out quantizedHeightmap);
        }

        private bool TryResolveHybridTerrainScratchBuffers(
            int hybridPlanCount,
            int sampleCount,
            bool needsNormals,
            out NativeArray<HybridTerrainSeamPlanNative> nativePlans,
            out NativeArray<float> patchHeights,
            out NativeArray<byte> blendMask,
            out NativeArray<float3> normals)
        {
            nativePlans = default;
            patchHeights = default;
            blendMask = default;
            normals = default;
            if (hybridPlanCount <= 0 || sampleCount <= 0)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryAcquireTerrainSeamBuffer(
                    vault,
                    TerrainSeamNativePlansBufferId,
                    hybridPlanCount,
                    NativeArrayOptions.UninitializedMemory,
                    out nativePlans) ||
                !TryAcquireTerrainSeamBuffer(
                    vault,
                    TerrainSeamPatchHeightsBufferId,
                    sampleCount,
                    NativeArrayOptions.UninitializedMemory,
                    out patchHeights) ||
                !TryAcquireTerrainSeamBuffer(
                    vault,
                    TerrainSeamBlendMaskBufferId,
                    sampleCount,
                    NativeArrayOptions.UninitializedMemory,
                    out blendMask))
            {
                return false;
            }

            if (needsNormals)
            {
                if (!TryAcquireTerrainSeamBuffer(
                        vault,
                        TerrainSeamNormalsBufferId,
                        sampleCount,
                        NativeArrayOptions.UninitializedMemory,
                        out normals))
                {
                    return false;
                }
            }

            return nativePlans.IsCreated &&
                   nativePlans.Length >= hybridPlanCount &&
                   patchHeights.IsCreated &&
                   patchHeights.Length >= sampleCount &&
                   blendMask.IsCreated &&
                   blendMask.Length >= sampleCount &&
                   (!needsNormals || (normals.IsCreated && normals.Length >= sampleCount));
        }

        private bool TryAcquireHybridTerrainMutationGuard(
            TerrainApplyState state,
            bool needsNormals,
            bool hasVaultHeightmap,
            out ulong guardMask,
            out IDataVault guardVault)
        {
            guardMask = 0UL;
            guardVault = null;
            IDataVault vault = _dataVault;
            if (vault == null || state == null || state.baselineHeightsBufferId == BufferID.Unknown)
                return false;

            guardMask =
                TerrainSeamMutationGuardBit(state.baselineHeightsBufferId) |
                TerrainSeamMutationGuardBit(TerrainSeamNativePlansBufferId) |
                TerrainSeamMutationGuardBit(TerrainSeamPatchHeightsBufferId) |
                TerrainSeamMutationGuardBit(TerrainSeamBlendMaskBufferId);

            if (hasVaultHeightmap)
                guardMask |= TerrainSeamMutationGuardBit(BufferID.TerrainSeamHeightmap);
            if (needsNormals)
                guardMask |= TerrainSeamMutationGuardBit(TerrainSeamNormalsBufferId);

            if (guardMask == 0UL || !vault.TryAcquireMutationGuard(guardMask))
                return false;

            bool keepGuard = false;
            try
            {
                guardVault = vault;
                keepGuard = true;
                return true;
            }
            finally
            {
                if (!keepGuard)
                {
                    vault.ReleaseMutationGuard(guardMask);
                    guardMask = 0UL;
                    guardVault = null;
                }
            }
        }

        private static void ReleaseHybridTerrainMutationGuard(IDataVault vault, ulong guardMask)
        {
            if (vault != null && guardMask != 0UL)
                vault.ReleaseMutationGuard(guardMask);
        }

        private static ulong TerrainSeamMutationGuardBit(BufferID bufferId)
        {
            return bufferId == BufferID.Unknown
                ? 0UL
                : 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private bool TryResolveBaselineHeights(TerrainApplyState state, out NativeArray<float> baselineHeights)
        {
            baselineHeights = default;
            if (state == null ||
                state.heightmapResolution <= 1 ||
                state.baselineHeightsHandle.BufferID == 0u)
            {
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int requiredLength = state.heightmapResolution * state.heightmapResolution;
            return TryOpenTerrainSeamBuffer(
                vault,
                in state.baselineHeightsHandle,
                state.baselineHeightsBufferId,
                requiredLength,
                out baselineHeights);
        }

        private static bool TryAcquireTerrainSeamBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            int length = math.max(1, requiredLength);
            if (TryOpenExistingTerrainSeamBuffer(vault, bufferId, length, out buffer))
                return true;

            buffer = default;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                length,
                OwnerSystem,
                options);
            return TryOpenTerrainSeamBuffer(vault, in handle, bufferId, length, out buffer);
        }

        private static bool TryAcquireTerrainSeamWriteBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            handle = default;
            buffer = default;
            int length = math.max(1, requiredLength);
            if (vault == null || bufferId == BufferID.Unknown || length <= 0)
                return false;

            if (vault.TryGetGenerationHandle(bufferId, out handle) &&
                TryOpenTerrainSeamBuffer(vault, in handle, bufferId, length, out _))
            {
                if (TryAcquireTerrainSeamWriteLock(vault, in handle, bufferId, length, out buffer))
                    return true;

                handle = default;
                return false;
            }

            if (vault.IsAllocationLocked)
            {
                handle = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                length,
                OwnerSystem,
                options);
            if (TryAcquireTerrainSeamWriteLock(vault, in handle, bufferId, length, out buffer))
                return true;

            handle = default;
            return false;
        }

        private static bool TryAcquireTerrainSeamWriteLock<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                handle.BufferID != (uint)bufferId ||
                handle.SystemID != (uint)OwnerSystem ||
                handle.Generation == 0u ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystem, out buffer))
            {
                buffer = default;
                return false;
            }

            bool releaseLock = true;
            try
            {
                if (!buffer.IsCreated || buffer.Length < requiredLength)
                {
                    buffer = default;
                    return false;
                }

                releaseLock = false;
                return true;
            }
            finally
            {
                if (releaseLock)
                    vault.ReleaseWriteLock(in handle, OwnerSystem);
            }
        }

        private static void ReleaseTerrainSeamWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseWriteLock(in handle, OwnerSystem);
        }

        private static bool TryOpenExistingTerrainSeamBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle))
            {
                return false;
            }

            return TryOpenTerrainSeamBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenTerrainSeamBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                handle.BufferID != (uint)bufferId ||
                handle.SystemID != (uint)OwnerSystem ||
                handle.Generation == 0u ||
                !vault.TryReadHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static BufferID ResolveTerrainBaselineBufferId(UnityEngine.Terrain terrain)
        {
            uint terrainKey = terrain != null
                ? Hecton8.Core.RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(terrain.GetEntityId()))
                : 0u;
            return (BufferID)(TerrainSeamBaselineBufferIdBase + (int)(terrainKey & (uint)TerrainSeamBaselineBufferIdMask));
        }

        private static int CountHybridTerrainPlans(List<WorldGenerativeGeologySeamPlan> plans)
        {
            int count = 0;
            for (int i = 0; i < plans.Count; i++)
            {
                WorldGenerativeGeologySeamPlan plan = plans[i];
                if (IsHybridTerrainPlan(in plan))
                    count++;
            }

            return count;
        }

        private static bool IsHybridTerrainPlan(in WorldGenerativeGeologySeamPlan plan)
        {
            return plan.RequiresTerrainBlend &&
                   plan.hasTerrainSample &&
                   plan.planWeight > 0.01f &&
                   plan.seamBlendRadius > 0.01f;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(weight) ? math.saturate(weight) : 1f;
        }

        private static float3 ResolveTerrainLocalWorldPosition(
            in WorldGenerativeGeologySeamPlan plan,
            in double3 terrainAbsolutePosition,
            Vector3 runtimeFallback,
            Vector3 terrainRuntimePosition)
        {
            double3 absolutePosition = plan.hasAbsoluteUniverseAup
                ? plan.absoluteUniverseAup.ToAbsoluteDouble3()
                : ResolveTerrainRelativeFallbackAbsolute(
                    in terrainAbsolutePosition,
                    runtimeFallback,
                    terrainRuntimePosition);
            return ToTerrainLocalFloat3(
                absolutePosition,
                terrainAbsolutePosition,
                runtimeFallback,
                terrainRuntimePosition);
        }

        private static float3 ResolveTerrainLocalContactPosition(
            in WorldGenerativeGeologySeamPlan plan,
            in double3 terrainAbsolutePosition,
            Vector3 runtimeFallback,
            Vector3 terrainRuntimePosition)
        {
            double3 absolutePosition = plan.hasAbsoluteTerrainContactAup
                ? plan.absoluteTerrainContactAup.ToAbsoluteDouble3()
                : ResolveTerrainRelativeFallbackAbsolute(
                    in terrainAbsolutePosition,
                    runtimeFallback,
                    terrainRuntimePosition);
            return ToTerrainLocalFloat3(
                absolutePosition,
                terrainAbsolutePosition,
                runtimeFallback,
                terrainRuntimePosition);
        }

        private static float3 ResolveTerrainLocalVoxelCenter(
            in WorldGenerativeGeologySeamPlan plan,
            in double3 terrainAbsolutePosition,
            Vector3 runtimeFallback,
            Vector3 terrainRuntimePosition)
        {
            double3 absolutePosition = plan.hasAbsoluteVoxelVolumeCenterAup
                ? plan.absoluteVoxelVolumeCenterAup.ToAbsoluteDouble3()
                : ResolveTerrainRelativeFallbackAbsolute(
                    in terrainAbsolutePosition,
                    runtimeFallback,
                    terrainRuntimePosition);
            return ToTerrainLocalFloat3(
                absolutePosition,
                terrainAbsolutePosition,
                runtimeFallback,
                terrainRuntimePosition);
        }

        private static float3 ToTerrainLocalFloat3(
            in double3 absolutePosition,
            in double3 terrainAbsolutePosition,
            Vector3 runtimeFallback,
            Vector3 terrainRuntimePosition)
        {
            double3 local = absolutePosition - terrainAbsolutePosition;
            if (IsFiniteTerrainLocal(local.x) &&
                IsFiniteTerrainLocal(local.y) &&
                IsFiniteTerrainLocal(local.z))
            {
                return new float3((float)local.x, (float)local.y, (float)local.z);
            }

            Vector3 fallbackLocal = runtimeFallback - terrainRuntimePosition;
            return new float3(fallbackLocal.x, fallbackLocal.y, fallbackLocal.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveRuntimeAbsoluteDouble(Vector3 runtimePosition, out double3 absolutePosition)
        {
            absolutePosition = default;
            if (!IsFiniteRuntimeVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!IsFiniteAup(in positionAup))
                return false;

            absolutePosition = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absolutePosition));
        }

        private static double3 ResolveTerrainRelativeFallbackAbsolute(
            in double3 terrainAbsolutePosition,
            Vector3 runtimeFallback,
            Vector3 terrainRuntimePosition)
        {
            if (!math.all(math.isfinite(terrainAbsolutePosition)) ||
                !IsFiniteRuntimeVector(runtimeFallback) ||
                !IsFiniteRuntimeVector(terrainRuntimePosition))
            {
                return terrainAbsolutePosition;
            }

            return terrainAbsolutePosition + new double3(
                (double)runtimeFallback.x - terrainRuntimePosition.x,
                (double)runtimeFallback.y - terrainRuntimePosition.y,
                (double)runtimeFallback.z - terrainRuntimePosition.z);
        }

        private static bool IsFiniteRuntimeVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static int FloorToIntSafe(double value)
        {
            if (!math.isfinite(value))
                return 0;

            double clamped = math.clamp(math.floor(value), int.MinValue, int.MaxValue);
            return (int)clamped;
        }

        private static int CeilToIntSafe(double value)
        {
            if (!math.isfinite(value))
                return 0;

            double clamped = math.clamp(math.ceil(value), int.MinValue, int.MaxValue);
            return (int)clamped;
        }

        private static bool IsFiniteTerrainLocal(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value) &&
                   value > -1048576d &&
                   value < 1048576d;
        }

        private static void InjectGlobalQualityWeight(ref HybridSdfHeightmapProjectionJob job, float globalQualityWeight)
        {
            job.GlobalQualityWeight = math.saturate(globalQualityWeight);
            job.GlobalQualityWeightValid = 1;
        }

        private static void InjectGlobalQualityWeight(ref HybridTerrainSeamMaskDetailJob job, float globalQualityWeight)
        {
            job.GlobalQualityWeight = math.saturate(globalQualityWeight);
            job.GlobalQualityWeightValid = 1;
        }

        private static float ResolveSeamExpensiveWeight(float globalQualityWeight)
        {
            float q = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float t = math.saturate((q - SeamExpensiveSamplingStartWeight) *
                                    math.rcp(math.max(1f - SeamExpensiveSamplingStartWeight, 0.0001f)));
            return SmoothStep01(t);
        }

        private static float ResolveMaskDetailWeight(float globalQualityWeight)
        {
            float q = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            return SmoothStep01(math.saturate((q - 0.70f) * math.rcp(0.30f)));
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private void EnsureVoxelBlendMaskTextureCold()
        {
            int side = Mathf.Clamp(voxelBlendMaskTextureSide, 64, 1024);
            int pixelCount = side * side;
            if (_voxelBlendMaskTexture != null &&
                _voxelBlendMaskTexture.width == side &&
                _voxelBlendMaskTexture.height == side &&
                _voxelBlendMaskUploadBuffer != null &&
                _voxelBlendMaskUploadBuffer.Length == pixelCount)
            {
                return;
            }

            DestroyVoxelBlendMaskTexture();
            _voxelBlendMaskUploadBuffer = new byte[pixelCount]; // COLD ALLOC: byte[side*side] - reusable terrain seam blend-mask upload buffer - owner: WorldGenerativeGeologyTerrainSeamApplier
            _voxelBlendMaskTexture = new Texture2D(side, side, TextureFormat.R8, false, true) // COLD ALLOC: Texture2D R8 fixed-size seam blend mask - owner: WorldGenerativeGeologyTerrainSeamApplier
            {
                name = "HectonVoxelBlendMask_Runtime",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        private void CopyVoxelBlendMaskToUploadBuffer(NativeArray<byte> blendMask, int width, int height)
        {
            int textureWidth = _voxelBlendMaskTexture.width;
            int textureHeight = _voxelBlendMaskTexture.height;
            int sourceIndex = 0;
            for (int y = 0; y < height; y++)
            {
                int destinationIndex = y * textureWidth;
                for (int x = 0; x < width; x++)
                    _voxelBlendMaskUploadBuffer[destinationIndex + x] = blendMask[sourceIndex++];

                if (width < textureWidth)
                    _voxelBlendMaskUploadBuffer[destinationIndex + width] = 0;
            }

            if (height < textureHeight)
            {
                int clearStart = height * textureWidth;
                int clearCount = Mathf.Min(width + 1, textureWidth);
                Array.Clear(_voxelBlendMaskUploadBuffer, clearStart, clearCount);
            }
        }

        private void UploadVoxelBlendMaskTexture(
            UnityEngine.Terrain terrain,
            RectInt applyRect,
            NativeArray<byte> blendMask,
            float seamExpensiveWeight)
        {
            if (terrain == null || terrain.terrainData == null || !blendMask.IsCreated)
                return;

            int width = applyRect.width;
            int height = applyRect.height;
            if (width <= 0 ||
                height <= 0 ||
                _voxelBlendMaskTexture == null ||
                _voxelBlendMaskUploadBuffer == null ||
                width > _voxelBlendMaskTexture.width ||
                height > _voxelBlendMaskTexture.height ||
                blendMask.Length < width * height)
                return;

            CopyVoxelBlendMaskToUploadBuffer(blendMask, width, height);

            using (TerrainBlendMaskUploadMarker.Auto())
            {
                _voxelBlendMaskTexture.SetPixelData(_voxelBlendMaskUploadBuffer, 0);
                _voxelBlendMaskTexture.Apply(false, false);
            }

            UnityEngine.TerrainData terrainData = terrain.terrainData;
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            float denominator = Mathf.Max(1f, terrainData.heightmapResolution - 1f);
            float worldMinX = terrainPosition.x + (applyRect.x / denominator) * terrainSize.x;
            float worldMinZ = terrainPosition.z + (applyRect.y / denominator) * terrainSize.z;
            float worldSizeX = Mathf.Max(0.001f, (applyRect.width / denominator) * terrainSize.x);
            float worldSizeZ = Mathf.Max(0.001f, (applyRect.height / denominator) * terrainSize.z);

            Shader.SetGlobalTexture(HectonVoxelBlendMaskId, _voxelBlendMaskTexture);
            Shader.SetGlobalVector(HectonVoxelBlendMaskRectId, new Vector4(worldMinX, worldMinZ, 1f / worldSizeX, 1f / worldSizeZ));
            float expensiveWeight = Mathf.Clamp01(seamExpensiveWeight);
            Shader.SetGlobalVector(
                HectonVoxelBlendMaskParamsId,
                new Vector4(
                    1f,
                    Mathf.Lerp(0.82f, 1f, expensiveWeight),
                    width / (float)_voxelBlendMaskTexture.width,
                    height / (float)_voxelBlendMaskTexture.height));
            _voxelBlendMaskGlobalActive = true;
            _voxelBlendMaskUploadedThisPass = true;
        }

        private void QueueVoxelBlendMaskTextureUpload(
            UnityEngine.Terrain terrain,
            RectInt applyRect,
            NativeArray<byte> blendMask,
            float seamExpensiveWeight)
        {
            _pendingVoxelBlendMaskTerrain = terrain;
            _pendingVoxelBlendMaskRect = applyRect;
            _pendingVoxelBlendMaskSampleCount = blendMask.IsCreated ? math.min(blendMask.Length, applyRect.width * applyRect.height) : 0;
            _pendingVoxelBlendMaskExpensiveWeight = Mathf.Clamp01(seamExpensiveWeight);
            _hasPendingVoxelBlendMaskUpload = true;
            _voxelBlendMaskUploadedThisPass = true;
            TryRegisterToLateFrameTickManager();
        }

        private void FlushPendingVoxelBlendMaskUpload()
        {
            if (!_hasPendingVoxelBlendMaskUpload)
                return;

            _hasPendingVoxelBlendMaskUpload = false;
            if (_pendingVoxelBlendMaskSampleCount > 0 &&
                TryOpenExistingTerrainSeamBuffer(
                    _dataVault,
                    TerrainSeamBlendMaskBufferId,
                    _pendingVoxelBlendMaskSampleCount,
                    out NativeArray<byte> blendMask))
            {
                UploadVoxelBlendMaskTexture(
                    _pendingVoxelBlendMaskTerrain,
                    _pendingVoxelBlendMaskRect,
                    blendMask,
                    _pendingVoxelBlendMaskExpensiveWeight);
            }

            _pendingVoxelBlendMaskTerrain = null;
            _pendingVoxelBlendMaskSampleCount = 0;
            _pendingVoxelBlendMaskExpensiveWeight = 0f;
        }

        private void PublishTerrainPatchVoxelModifiedEvent(
            uint seamFrame,
            UnityEngine.Terrain terrain,
            RectInt applyRect,
            float minHeight01,
            float maxHeight01,
            uint stateHash)
        {
            if (terrain == null || terrain.terrainData == null)
                return;

            UnityEngine.TerrainData terrainData = terrain.terrainData;
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            float denominator = Mathf.Max(1f, terrainData.heightmapResolution - 1f);
            float cellSize = Mathf.Max(0.05f, Mathf.Min(terrainSize.x, terrainSize.z) / denominator);
            if (!TryResolveRuntimeAbsoluteDouble(terrainPosition, out double3 terrainAbsolutePosition))
                return;

            double minWorldX = terrainAbsolutePosition.x + (applyRect.x / denominator) * terrainSize.x;
            double maxWorldX = terrainAbsolutePosition.x + ((applyRect.x + applyRect.width) / denominator) * terrainSize.x;
            double minWorldY = terrainAbsolutePosition.y + minHeight01 * terrainSize.y;
            double maxWorldY = terrainAbsolutePosition.y + maxHeight01 * terrainSize.y;
            double minWorldZ = terrainAbsolutePosition.z + (applyRect.y / denominator) * terrainSize.z;
            double maxWorldZ = terrainAbsolutePosition.z + ((applyRect.y + applyRect.height) / denominator) * terrainSize.z;

            int3 minCell = new int3(
                FloorToIntSafe(minWorldX / cellSize),
                FloorToIntSafe(minWorldY / cellSize),
                FloorToIntSafe(minWorldZ / cellSize));
            int3 maxCell = new int3(
                CeilToIntSafe(maxWorldX / cellSize),
                CeilToIntSafe(maxWorldY / cellSize),
                CeilToIntSafe(maxWorldZ / cellSize));
            ulong volumeInstanceId = EntityId.ToULong(terrain.GetEntityId());
            if (volumeInstanceId == 0ul)
                volumeInstanceId = ((ulong)stateHash << 1) | 1ul;

            VoxelChunkModifiedEvent modifiedEvent = new VoxelChunkModifiedEvent
            {
                VolumeInstanceId = volumeInstanceId,
                MinAbsoluteCell = minCell,
                MaxAbsoluteCell = maxCell,
                VoxelSize = cellSize,
                Frame = seamFrame,
                Operation = (byte)VoxelCarveOperationType.Replace,
                Shape = (byte)VoxelCarveShapeType.Box,
                Flags = 1,
                StateHash = stateHash
            };
            VoxelChunkModifiedEvents.TryPublish(in modifiedEvent);
        }

        private void RecordTerrainSeamBlackBox(
            uint seamFrame,
            UnityEngine.Terrain terrain,
            uint terrainHash,
            RectInt applyRect,
            int sampleCount,
            int planCount,
            float minHeight01,
            float maxHeight01,
            float maxBlend01,
            bool visualSamplingSuppressed,
            bool maskDetailActive,
            bool usedVaultHeightmap,
            bool heightmapChanged,
            bool faulted,
            uint stateHash)
        {
            if (terrain == null || terrain.terrainData == null)
                return;

            IDataVault vault = _dataVault;
            if (!TryAcquireTerrainSeamWriteBuffer(
                    vault,
                    TerrainSeamBlackBoxBufferId,
                    TerrainSeamBlackBoxCapacity,
                    NativeArrayOptions.ClearMemory,
                    out _terrainSeamBlackBoxHandle,
                    out NativeArray<TerrainSeamTelemetryEntry> blackBox))
            {
                return;
            }

            UnityEngine.TerrainData terrainData = terrain.terrainData;
            Vector3 position = terrain.transform.position;
            Vector3 size = terrainData.size;
            float denominator = Mathf.Max(1f, terrainData.heightmapResolution - 1f);
            TerrainSeamTelemetryEntry entry = new TerrainSeamTelemetryEntry
            {
                Frame = seamFrame,
                TerrainHash = terrainHash,
                PatchSampleCount = sampleCount,
                PlanCount = planCount,
                PatchCenterX = position.x + ((applyRect.x + applyRect.width * 0.5f) / denominator) * size.x,
                PatchCenterZ = position.z + ((applyRect.y + applyRect.height * 0.5f) / denominator) * size.z,
                MinHeight01 = minHeight01,
                MaxHeight01 = maxHeight01,
                MaxBlend01 = maxBlend01,
                Flags = (uint)(
                    (visualSamplingSuppressed ? 1 : 0) |
                    (faulted ? 2 : 0) |
                    (maskDetailActive ? 4 : 0) |
                    (usedVaultHeightmap ? 8 : 0) |
                    (heightmapChanged ? 16 : 0)),
                StateHash = stateHash
            };

            try
            {
                blackBox[_blackBoxWriteIndex] = entry;
                _blackBoxWriteIndex = (_blackBoxWriteIndex + 1) % TerrainSeamBlackBoxCapacity;
            }
            finally
            {
                ReleaseTerrainSeamWriteBuffer(vault, in _terrainSeamBlackBoxHandle);
            }
        }

        private uint AdvanceTerrainSeamFrame()
        {
            unchecked
            {
                _seamFrameCounter++;
                if (_seamFrameCounter == 0u)
                    _seamFrameCounter = 1u;

                return _seamFrameCounter;
            }
        }

        private static uint HashTerrainSeamState(
            uint terrainHash,
            RectInt rect,
            int sampleCount,
            int planCount,
            float minHeight01,
            float maxHeight01,
            float maxBlend01)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, terrainHash);
            hash = MixHash(hash, (uint)rect.x);
            hash = MixHash(hash, (uint)rect.y);
            hash = MixHash(hash, (uint)rect.width);
            hash = MixHash(hash, (uint)rect.height);
            hash = MixHash(hash, (uint)sampleCount);
            hash = MixHash(hash, (uint)planCount);
            hash = MixHash(hash, (uint)Mathf.RoundToInt(minHeight01 * 65535f));
            hash = MixHash(hash, (uint)Mathf.RoundToInt(maxHeight01 * 65535f));
            hash = MixHash(hash, (uint)Mathf.RoundToInt(maxBlend01 * 65535f));
            return hash == 0u ? 1u : hash;
        }

        private static uint MixHash(uint hash, uint value)
        {
            unchecked
            {
                return (hash ^ value) * 16777619u;
            }
        }

        private bool ApplyPlanToPatch(
            UnityEngine.Terrain terrain,
            RectInt patchRect,
            float[,] patch,
            in WorldGenerativeGeologySeamPlan plan)
        {
            UnityEngine.TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
                return false;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            if (!TryResolveRuntimeAbsoluteDouble(terrainPosition, out double3 terrainAbsolutePosition))
                return false;

            float invHeight = terrainSize.y > 0.001f ? 1f / terrainSize.y : 0f;
            float invMaxHeightIndex = 1f / Mathf.Max(1, terrainData.heightmapResolution - 1);
            float effectiveRadius = Mathf.Max(2f, plan.seamBlendRadius + radiusPaddingMeters);
            float effectiveRadiusSq = effectiveRadius * effectiveRadius;
            float desiredDelta = ResolveDesiredWorldDelta(plan);
            if (Mathf.Abs(desiredDelta * invHeight) < 0.00001f)
                return false;

            Vector3 planRuntimePosition = plan.RuntimeWorldPosition;
            float3 planLocalPosition = ResolveTerrainLocalWorldPosition(
                in plan,
                in terrainAbsolutePosition,
                planRuntimePosition,
                terrainPosition);
            bool snapVoxelCut = desiredDelta < -0.0001f && plan.RequiresVoxelBlend;
            bool changed = false;

            for (int patchZ = 0; patchZ < patchRect.height; patchZ++)
            {
                int heightmapZ = patchRect.y + patchZ;
                float localZ = heightmapZ * invMaxHeightIndex * terrainSize.z;
                float deltaZ = localZ - planLocalPosition.z;

                for (int patchX = 0; patchX < patchRect.width; patchX++)
                {
                    int heightmapX = patchRect.x + patchX;
                    float localX = heightmapX * invMaxHeightIndex * terrainSize.x;
                    float deltaX = localX - planLocalPosition.x;
                    float distanceSq = deltaX * deltaX + deltaZ * deltaZ;
                    if (distanceSq > effectiveRadiusSq)
                        continue;

                    float radial = 1f - Mathf.Clamp01(distanceSq / effectiveRadiusSq);
                    float falloff = Mathf.SmoothStep(0f, 1f, radial);
                    float rim = Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, radial * radial), rimSmoothing);
                    float shapeBias = Mathf.Clamp01(
                        plan.terrainBlendWeight * 0.45f +
                        plan.ridgeSignal * 0.18f +
                        plan.canyonSignal * 0.12f +
                        plan.compositionPotential * 0.15f +
                        plan.caveProximity * 0.10f);

                    float sourceNormalized = patch[patchZ, patchX];
                    float targetLocalHeight = sourceNormalized * terrainSize.y + desiredDelta * falloff * rim * shapeBias;
                    if (snapVoxelCut &&
                        TryResolveVoxelSnappedTerrainLocalHeight(
                            localX,
                            localZ,
                            targetLocalHeight,
                            in terrainAbsolutePosition,
                            in plan,
                            out float snappedLocalHeight))
                    {
                        targetLocalHeight = snappedLocalHeight;
                    }

                    float targetHeight01 = Mathf.Clamp01(targetLocalHeight * invHeight);
                    changed |= Mathf.Abs(targetHeight01 - sourceNormalized) > 0.00001f;
                    patch[patchZ, patchX] = targetHeight01;
                }
            }

            return changed;
        }

        private static bool TryResolveVoxelSnappedTerrainLocalHeight(
            float terrainLocalX,
            float terrainLocalZ,
            float terrainLocalTargetHeight,
            in double3 terrainAbsolutePosition,
            in WorldGenerativeGeologySeamPlan plan,
            out float snappedTerrainLocalHeight)
        {
            snappedTerrainLocalHeight = terrainLocalTargetHeight;
            if (!plan.hasAbsoluteVoxelVolumeCenterAup && plan.voxelVolumeSize.sqrMagnitude <= 0.0001f)
                return false;

            double3 targetAbsolute = new double3(
                terrainAbsolutePosition.x + terrainLocalX,
                terrainAbsolutePosition.y + terrainLocalTargetHeight,
                terrainAbsolutePosition.z + terrainLocalZ);
            AbsoluteUniversePosition targetAup = AbsoluteUniversePosition.FromAbsolutePosition(targetAbsolute);
            double targetAbsoluteY = targetAup.ToAbsoluteDouble3().y;
            double3 centerAbsolute = plan.hasAbsoluteVoxelVolumeCenterAup
                ? plan.absoluteVoxelVolumeCenterAup.ToAbsoluteDouble3()
                : new double3(
                    plan.absoluteVoxelVolumeCenter.x,
                    plan.absoluteVoxelVolumeCenter.y,
                    plan.absoluteVoxelVolumeCenter.z);
            float snapStepMeters = VoxelSeamDirector.ResolveTerrainVoxelSnapStep(plan.voxelVolumeSize, plan.seamBlendRadius);
            double originY = centerAbsolute.y - plan.voxelVolumeSize.y * 0.5d;
            double snappedAbsoluteY = VoxelSeamDirector.SnapAbsoluteHeightToVoxelLayer(
                targetAbsoluteY,
                originY,
                snapStepMeters);
            float resolvedLocalHeight = (float)(snappedAbsoluteY - terrainAbsolutePosition.y);
            if (float.IsNaN(resolvedLocalHeight) || float.IsInfinity(resolvedLocalHeight))
                return false;

            snappedTerrainLocalHeight = resolvedLocalHeight;
            return true;
        }

        private float ResolveDesiredWorldDelta(in WorldGenerativeGeologySeamPlan plan)
        {
            float upward = Mathf.Max(0f, plan.terrainDelta) + plan.suggestedTerrainRaise;
            float downward = Mathf.Max(0f, -plan.terrainDelta) + plan.suggestedTerrainCut * Mathf.Clamp01(plan.caveBlendWeight + 0.25f);
            float raise = upward * raiseStrength * Mathf.Clamp01(plan.terrainBlendWeight + plan.ridgeSignal * 0.25f);
            float cut = downward * cutStrength * Mathf.Clamp01(plan.terrainBlendWeight + plan.canyonSignal * 0.3f);
            return raise - cut;
        }

        private bool ApplyTrenchToPatch(
            UnityEngine.Terrain terrain,
            RectInt patchRect,
            float[,] patch,
            in SeismicTrenchState trench)
        {
            UnityEngine.TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
                return false;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            if (!TryResolveRuntimeAbsoluteDouble(terrainPosition, out double3 terrainAbsolutePosition))
                return false;

            Vector3 runtimeStart = HectonFloatingOrigin.ToRuntimePosition(trench.AbsoluteStart);
            Vector3 runtimeEnd = HectonFloatingOrigin.ToRuntimePosition(trench.AbsoluteEnd);
            float3 localStart3 = ToTerrainLocalFloat3(
                new double3(trench.AbsoluteStart.x, trench.AbsoluteStart.y, trench.AbsoluteStart.z),
                terrainAbsolutePosition,
                runtimeStart,
                terrainPosition);
            float3 localEnd3 = ToTerrainLocalFloat3(
                new double3(trench.AbsoluteEnd.x, trench.AbsoluteEnd.y, trench.AbsoluteEnd.z),
                terrainAbsolutePosition,
                runtimeEnd,
                terrainPosition);
            Vector2 start = new Vector2(localStart3.x, localStart3.z);
            Vector2 end = new Vector2(localEnd3.x, localEnd3.z);
            Vector2 segment = end - start;
            float segmentLengthSq = segment.sqrMagnitude;
            if (segmentLengthSq <= 0.0001f)
                return false;

            float invHeight = terrainSize.y > 0.001f ? 1f / terrainSize.y : 0f;
            float invMaxHeightIndex = 1f / Mathf.Max(1, terrainData.heightmapResolution - 1);
            float safeSlope = Mathf.Max(0.05f, trench.Slope);
            float influenceRadius = Mathf.Max(trench.InfluenceRadius, trench.DepthMeters / safeSlope) + Mathf.Max(0f, trenchRadiusPaddingMeters);
            float influenceRadiusSq = influenceRadius * influenceRadius;
            float rimBlend = Mathf.Clamp01(trenchRimBlendStrength);
            bool changed = false;

            for (int patchZ = 0; patchZ < patchRect.height; patchZ++)
            {
                int heightmapZ = patchRect.y + patchZ;
                float localZ = heightmapZ * invMaxHeightIndex * terrainSize.z;

                for (int patchX = 0; patchX < patchRect.width; patchX++)
                {
                    int heightmapX = patchRect.x + patchX;
                    float localX = heightmapX * invMaxHeightIndex * terrainSize.x;
                    Vector2 point = new Vector2(localX, localZ);
                    float distanceToLineSq = DistanceSqPointToSegment(point, start, segment, segmentLengthSq);
                    if (distanceToLineSq > influenceRadiusSq)
                        continue;

                    float distanceToLine = LengthFromSqNoSqrt(distanceToLineSq);
                    float cutDepth = Mathf.Max(0f, trench.DepthMeters - distanceToLine * safeSlope);
                    if (cutDepth <= 0.0001f)
                        continue;

                    float radial = 1f - Mathf.Clamp01(distanceToLine / influenceRadius);
                    float rim = Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, radial * radial), rimBlend);
                    float normalizedDelta = -(cutDepth * cutStrength * rim * Mathf.SmoothStep(0f, 1f, radial)) * invHeight;
                    float sourceHeight01 = patch[patchZ, patchX];
                    float targetHeight01 = Mathf.Clamp01(sourceHeight01 + normalizedDelta);
                    changed |= Mathf.Abs(targetHeight01 - sourceHeight01) > 0.00001f;
                    patch[patchZ, patchX] = targetHeight01;
                }
            }

            return changed;
        }

        private void RestoreUntouchedTerrains()
        {
            for (int i = 0; i < _knownTerrainIds.Count; i++)
            {
                int terrainId = _knownTerrainIds[i];
                if (_touchedTerrainIds.Contains(terrainId))
                    continue;

                if (!_terrainStates.TryGetValue(terrainId, out TerrainApplyState state) || state == null || !state.hasPreviousRect)
                    continue;

                RestoreTerrainState(state);
                _debugRestoredTerrains++;
            }
        }

        private void RestoreAllTerrains()
        {
            for (int i = 0; i < _knownTerrainIds.Count; i++)
            {
                int terrainId = _knownTerrainIds[i];
                if (!_terrainStates.TryGetValue(terrainId, out TerrainApplyState state) || state == null || !state.hasPreviousRect)
                    continue;

                RestoreTerrainState(state);
            }
        }

        private void RestoreTerrainState(TerrainApplyState state)
        {
            if (state == null || state.terrain == null || state.terrainData == null || !state.hasPreviousRect)
                return;

            UnityEngine.TerrainData currentTerrainData = state.terrain.terrainData;
            if (currentTerrainData == null)
            {
                state.hasPreviousRect = false;
                state.previousRect = default;
                return;
            }

            int currentResolution = currentTerrainData.heightmapResolution;
            if (currentTerrainData != state.terrainData ||
                currentResolution != state.heightmapResolution ||
                !TryResolveBaselineHeights(state, out _))
            {
                RefreshTerrainBaseline(state, state.terrain, currentTerrainData, currentResolution);
                return;
            }

            RectInt rect = ClampRect(state.previousRect, state.heightmapResolution - 1, state.heightmapResolution - 1);
            if (rect.width <= 0 || rect.height <= 0)
            {
                state.hasPreviousRect = false;
                state.previousRect = default;
                return;
            }

            if (Application.isPlaying)
            {
                state.previousRect = default;
                state.hasPreviousRect = false;
                return;
            }

            float[,] patch = PreparePatchBuffer(state, rect);
            using (TerrainHeightmapWritebackMarker.Auto())
            {
                state.terrainData.SetHeightsDelayLOD(rect.x, rect.y, patch);
                state.terrainData.SyncHeightmap();
            }
            state.previousRect = default;
            state.hasPreviousRect = false;
        }

        private void EnsureTerrainState(UnityEngine.Terrain terrain, int terrainId)
        {
            if (terrain == null || terrain.terrainData == null)
                return;

            UnityEngine.TerrainData terrainData = terrain.terrainData;
            int resolution = terrainData.heightmapResolution;
            if (!_terrainStates.TryGetValue(terrainId, out TerrainApplyState state))
            {
                if (!TryBindTerrainStateBucket(terrainId, out state))
                    return;

                RefreshTerrainBaseline(state, terrain, terrainData, resolution);
            }
            else if (state.terrain != terrain ||
                     state.terrainData != terrainData ||
                     !TryResolveBaselineHeights(state, out _) ||
                     state.heightmapResolution != resolution)
            {
                RefreshTerrainBaseline(state, terrain, terrainData, resolution);
            }
        }

        private bool TryBindTerrainStateBucket(int terrainId, out TerrainApplyState state)
        {
            state = null;
            if (!_terrainBucketPoolsInitialized)
                return false;

            int bucketIndex = _knownTerrainIds.Count;
            if (bucketIndex >= TerrainStateCapacity)
                return false;

            TerrainApplyState candidateState = _terrainStatePool[bucketIndex];
            List<WorldGenerativeGeologySeamPlan> planBucket = _planBucketPool[bucketIndex];
            List<SeismicTrenchState> trenchBucket = _trenchBucketPool[bucketIndex];
            if (candidateState == null || planBucket == null || trenchBucket == null)
                return false;

            candidateState.ReleaseBaseline();
            candidateState.terrain = null;
            candidateState.terrainData = null;
            candidateState.heightmapResolution = 0;
            candidateState.previousRect = default;
            candidateState.hasPreviousRect = false;
            planBucket.Clear();
            trenchBucket.Clear();

            _terrainStates.Add(terrainId, candidateState);
            _plansByTerrain.Add(terrainId, planBucket);
            _trenchesByTerrain.Add(terrainId, trenchBucket);
            _knownTerrainIds.Add(terrainId);
            state = candidateState;
            return true;
        }

        private float[,] PreparePatchBuffer(TerrainApplyState state, RectInt rect)
        {
            if (state.patchBuffer == null ||
                state.patchBuffer.GetLength(0) != rect.height ||
                state.patchBuffer.GetLength(1) != rect.width)
            {
                // COLD ALLOC: Single[rect.height*rect.width] - Unity SetHeightsDelayLOD bridge resized only when seam footprint changes - owner: WorldGenerativeGeologyTerrainSeamApplier
                state.patchBuffer = new float[rect.height, rect.width];
            }

            WorldGenerativeGeologyTelemetry.TryPublishTerrainPatchBridgeWarningIfNeeded(
                rect.width * rect.height,
                TerrainPatchBridgeSampleBudgetMx350,
                ref _nextPatchTelemetryFrame);
            if (TryResolveBaselineHeights(state, out NativeArray<float> baselineHeights))
                CopyBaselinePatch(baselineHeights, state.heightmapResolution, rect, state.patchBuffer);
            return state.patchBuffer;
        }

        private void RefreshTerrainBaseline(
            TerrainApplyState state,
            UnityEngine.Terrain terrain,
            UnityEngine.TerrainData terrainData,
            int resolution)
        {
            if (state == null || terrain == null || terrainData == null)
                return;

            state.ReleaseBaseline();

            state.terrain = terrain;
            state.terrainData = terrainData;
            state.heightmapResolution = resolution;
            int totalHeights = Mathf.Max(0, resolution * resolution);
            if (totalHeights > 0)
            {
                IDataVault vault = _dataVault;
                if (vault != null)
                {
                    state.baselineHeightsBufferId = ResolveTerrainBaselineBufferId(terrain);
                    if (TryAcquireTerrainSeamWriteBuffer(
                            vault,
                            state.baselineHeightsBufferId,
                            totalHeights,
                            NativeArrayOptions.UninitializedMemory,
                            out state.baselineHeightsHandle,
                            out NativeArray<float> baselineHeights))
                    {
                        try
                        {
                            PopulateTerrainBaselineNative(baselineHeights, terrain, terrainData, resolution);
                        }
                        finally
                        {
                            ReleaseTerrainSeamWriteBuffer(vault, in state.baselineHeightsHandle);
                        }
                    }
                }
            }
            state.patchBuffer = null;
            state.previousRect = default;
            state.hasPreviousRect = false;
        }

        private static void PopulateTerrainBaselineNative(
            NativeArray<float> baseline,
            UnityEngine.Terrain terrain,
            UnityEngine.TerrainData terrainData,
            int resolution)
        {
            if (!baseline.IsCreated || terrain == null || terrainData == null || resolution <= 0)
                return;

            Vector3 terrainSize = terrainData.size;
            float invHeight = terrainSize.y > 0.001f ? 1f / terrainSize.y : 0f;
            float denominator = Mathf.Max(1f, resolution - 1f);
            for (int z = 0; z < resolution; z++)
            {
                float normalizedZ = z / denominator;
                int rowOffset = z * resolution;
                for (int x = 0; x < resolution; x++)
                {
                    float normalizedX = x / denominator;
                    float localHeight = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
                    baseline[rowOffset + x] = Mathf.Clamp01(localHeight * invHeight);
                }
            }
        }

        private static void CopyBaselinePatch(NativeArray<float> baseline, int resolution, RectInt rect, float[,] destination)
        {
            for (int z = 0; z < rect.height; z++)
            {
                int sourceZ = rect.y + z;
                int sourceOffset = sourceZ * resolution;
                for (int x = 0; x < rect.width; x++)
                    destination[z, x] = baseline[sourceOffset + rect.x + x];
            }
        }

        private void EnsureHybridTerrainSeamStateCold()
        {
            EnsureTerrainBucketPoolsCold();

            if (_tileSnapshotScratch == null || _tileSnapshotScratch.Length != TerrainTileSnapshotCapacity)
            {
                // COLD ALLOC: MapMagicTerrainTileSnapshot[32] - fixed MapMagic tile snapshot scratch used while draining terrain generated signals - owner: WorldGenerativeGeologyTerrainSeamApplier
                _tileSnapshotScratch = new MapMagicTerrainTileSnapshot[TerrainTileSnapshotCapacity];
            }

            TryEnsureTerrainSeamBlackBox(out _);
        }

        private bool HasHybridTerrainSeamState()
        {
            return _terrainBucketPoolsInitialized && _tileSnapshotScratch != null;
        }

        private void EnsureTerrainBucketPoolsCold()
        {
            if (_terrainBucketPoolsInitialized)
                return;

            for (int i = 0; i < TerrainStateCapacity; i++)
            {
                if (_terrainStatePool[i] == null)
                {
                    // COLD ALLOC: TerrainApplyState[8] - fixed terrain seam state pool - owner: WorldGenerativeGeologyTerrainSeamApplier
                    _terrainStatePool[i] = new TerrainApplyState();
                }

                if (_planBucketPool[i] == null)
                {
                    // COLD ALLOC: List<WorldGenerativeGeologySeamPlan>[8] - fixed per-terrain plan buckets - owner: WorldGenerativeGeologyTerrainSeamApplier
                    _planBucketPool[i] = new List<WorldGenerativeGeologySeamPlan>(8);
                }

                if (_trenchBucketPool[i] == null)
                {
                    // COLD ALLOC: List<SeismicTrenchState>[8] - fixed per-terrain trench buckets - owner: WorldGenerativeGeologyTerrainSeamApplier
                    _trenchBucketPool[i] = new List<SeismicTrenchState>(4);
                }
            }

            _terrainBucketPoolsInitialized = true;
        }

        private void DisposeHybridTerrainSeamState()
        {
            _terrainSeamBlackBoxHandle = default;
            _blackBoxWriteIndex = 0;

            DestroyVoxelBlendMaskTexture();
            ClearVoxelBlendMaskGlobal();
            _voxelBlendMaskGlobalActive = false;
        }

        private bool TryEnsureTerrainSeamBlackBox(out NativeArray<TerrainSeamTelemetryEntry> blackBox)
        {
            blackBox = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (_terrainSeamBlackBoxHandle.BufferID != 0u)
            {
                if (TryOpenTerrainSeamBuffer(
                        vault,
                        in _terrainSeamBlackBoxHandle,
                        TerrainSeamBlackBoxBufferId,
                        TerrainSeamBlackBoxCapacity,
                        out blackBox))
                {
                    return true;
                }
            }

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(
                        TerrainSeamBlackBoxBufferId,
                        out _terrainSeamBlackBoxHandle) ||
                    !TryOpenTerrainSeamBuffer(
                        vault,
                        in _terrainSeamBlackBoxHandle,
                        TerrainSeamBlackBoxBufferId,
                        TerrainSeamBlackBoxCapacity,
                        out blackBox))
                {
                    blackBox = default;
                    return false;
                }
            }
            else
            {
                _terrainSeamBlackBoxHandle = vault.EnsureGenerationHandle<TerrainSeamTelemetryEntry>(
                    TerrainSeamBlackBoxBufferId,
                    TerrainSeamBlackBoxCapacity,
                    OwnerSystem,
                    NativeArrayOptions.ClearMemory);
                if (!TryOpenTerrainSeamBuffer(
                        vault,
                        in _terrainSeamBlackBoxHandle,
                        TerrainSeamBlackBoxBufferId,
                        TerrainSeamBlackBoxCapacity,
                        out blackBox))
                {
                    blackBox = default;
                    return false;
                }
            }

            _blackBoxWriteIndex = 0;
            return blackBox.IsCreated && blackBox.Length >= TerrainSeamBlackBoxCapacity;
        }

        private void DestroyVoxelBlendMaskTexture()
        {
            if (_voxelBlendMaskTexture == null)
                return;

            if (Application.isPlaying)
                Destroy(_voxelBlendMaskTexture);
            else
                DestroyImmediate(_voxelBlendMaskTexture);

            _voxelBlendMaskTexture = null;
            _voxelBlendMaskUploadBuffer = null;
        }

        private void DisableVoxelBlendMaskGlobal()
        {
            if (!_voxelBlendMaskGlobalActive)
                return;

            _pendingVoxelBlendMaskGlobalClear = true;
            TryRegisterToLateFrameTickManager();
            _voxelBlendMaskGlobalActive = false;
        }

        private void FlushPendingVoxelBlendMaskGlobalClear()
        {
            if (!_pendingVoxelBlendMaskGlobalClear)
                return;

            _pendingVoxelBlendMaskGlobalClear = false;
            ClearVoxelBlendMaskGlobal();
        }

        private static void ClearVoxelBlendMaskGlobal()
        {
            Shader.SetGlobalVector(HectonVoxelBlendMaskParamsId, Vector4.zero);
            Shader.SetGlobalVector(HectonVoxelBlendMaskRectId, Vector4.zero);
        }

        private void DumpTerrainSeamBlackBox()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!TryEnsureTerrainSeamBlackBox(out NativeArray<TerrainSeamTelemetryEntry> blackBox))
                return;

            const int HeaderBytes = 8;
            const int RowBytes = 64;
            int totalBytes = HeaderBytes + TerrainSeamBlackBoxCapacity * RowBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                totalBytes,
                nameof(WorldGenerativeGeologyTerrainSeamApplier),
                "TerrainSeamBlackBoxDumpPayload");
            try
            {
                WriteInt32LittleEndian(payload, 0, TerrainSeamBlackBoxCapacity);
                WriteInt32LittleEndian(payload, 4, _blackBoxWriteIndex);
                for (int i = 0; i < TerrainSeamBlackBoxCapacity; i++)
                {
                    TerrainSeamTelemetryEntry entry = blackBox[i];
                    int offset = HeaderBytes + i * RowBytes;
                    WriteUInt32LittleEndian(payload, offset, entry.Frame);
                    WriteUInt32LittleEndian(payload, offset + 4, entry.TerrainHash);
                    WriteInt32LittleEndian(payload, offset + 8, entry.PatchSampleCount);
                    WriteInt32LittleEndian(payload, offset + 12, entry.PlanCount);
                    WriteFloat32LittleEndian(payload, offset + 16, entry.PatchCenterX);
                    WriteFloat32LittleEndian(payload, offset + 20, entry.PatchCenterZ);
                    WriteFloat32LittleEndian(payload, offset + 24, entry.MinHeight01);
                    WriteFloat32LittleEndian(payload, offset + 28, entry.MaxHeight01);
                    WriteFloat32LittleEndian(payload, offset + 32, entry.MaxBlend01);
                    WriteUInt32LittleEndian(payload, offset + 36, entry.Flags);
                    WriteUInt32LittleEndian(payload, offset + 40, entry.StateHash);
                    WriteUInt32LittleEndian(payload, offset + 44, entry.Reserved0);
                    WriteUInt32LittleEndian(payload, offset + 48, entry.Reserved1);
                    WriteUInt32LittleEndian(payload, offset + 52, entry.Reserved2);
                    WriteUInt32LittleEndian(payload, offset + 56, entry.Reserved3);
                    WriteUInt32LittleEndian(payload, offset + 60, entry.Reserved4);
                }

                NativeFaultDumpWriter.TryWriteAll(
                    HybridDumpPath,
                    payload,
                    totalBytes);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(WorldGenerativeGeologyTerrainSeamApplier),
                    "TerrainSeamBlackBoxDumpPayload");
            }
#endif
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> payload, int offset, float value)
        {
            WriteUInt32LittleEndian(payload, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, int offset, int value)
        {
            WriteUInt32LittleEndian(payload, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, int offset, uint value)
        {
            payload[offset] = (byte)value;
            payload[offset + 1] = (byte)(value >> 8);
            payload[offset + 2] = (byte)(value >> 16);
            payload[offset + 3] = (byte)(value >> 24);
        }

        private void DisposeTerrainStateNativeBuffers()
        {
            Dictionary<int, TerrainApplyState>.Enumerator enumerator = _terrainStates.GetEnumerator();
            while (enumerator.MoveNext())
                enumerator.Current.Value?.ReleaseBaseline();
        }

        private void ClearBuckets()
        {
            for (int i = 0; i < _knownTerrainIds.Count; i++)
            {
                int terrainId = _knownTerrainIds[i];
                if (_plansByTerrain.TryGetValue(terrainId, out List<WorldGenerativeGeologySeamPlan> terrainPlans) &&
                    terrainPlans != null &&
                    terrainPlans.Count > 0)
                {
                    terrainPlans.Clear();
                }

                if (_trenchesByTerrain.TryGetValue(terrainId, out List<SeismicTrenchState> terrainTrenches) &&
                    terrainTrenches != null &&
                    terrainTrenches.Count > 0)
                {
                    terrainTrenches.Clear();
                }
            }
        }

        private void BucketActiveTrenches()
        {
            for (int trenchIndex = 0; trenchIndex < _activeTrenches.Count; trenchIndex++)
            {
                SeismicTrenchState trench = _activeTrenches[trenchIndex];
                Vector3 runtimeStart = HectonFloatingOrigin.ToRuntimePosition(trench.AbsoluteStart);
                Vector3 runtimeEnd = HectonFloatingOrigin.ToRuntimePosition(trench.AbsoluteEnd);
                Vector3 lineDelta = runtimeStart - runtimeEnd;
                float lineLength = LengthFromSqNoSqrt(
                    lineDelta.x * lineDelta.x +
                    lineDelta.y * lineDelta.y +
                    lineDelta.z * lineDelta.z);
                int sampleCount = Mathf.Clamp(Mathf.CeilToInt(lineLength / 48f) + 1, 2, 8);
                _terrainBucketScratch.Clear();

                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float sampleT = sampleCount <= 1 ? 0f : sampleIndex / (float)(sampleCount - 1);
                    Vector3 samplePosition = Vector3.Lerp(runtimeStart, runtimeEnd, sampleT);
                    UnityEngine.Terrain terrain = ResolveTerrainAt(samplePosition.x, samplePosition.z);
                    if (terrain == null || terrain.terrainData == null)
                        continue;

                    int terrainId = unchecked((int)EntityId.ToULong(terrain.GetEntityId()));
                    bool duplicate = false;
                    for (int i = 0; i < _terrainBucketScratch.Count; i++)
                    {
                        if (_terrainBucketScratch[i] != terrainId)
                            continue;

                        duplicate = true;
                        break;
                    }

                    if (duplicate)
                        continue;

                    _terrainBucketScratch.Add(terrainId);
                    EnsureTerrainState(terrain, terrainId);
                    if (!_trenchesByTerrain.TryGetValue(terrainId, out List<SeismicTrenchState> terrainTrenches) ||
                        terrainTrenches == null)
                    {
                        continue;
                    }

                    terrainTrenches.Add(trench);
                    _touchedTerrainIds.Add(terrainId);
                }
            }
        }

        private static RectInt BuildPlanRect(UnityEngine.Terrain terrain, in WorldGenerativeGeologySeamPlan plan)
        {
            UnityEngine.TerrainData terrainData = terrain.terrainData;
            if (terrainData == null || terrainData.heightmapResolution < 2)
                return default;

            Vector3 position = terrain.transform.position;
            Vector3 size = terrainData.size;
            if (size.x <= 0.001f || size.z <= 0.001f || plan.seamBlendRadius <= 0f)
                return default;

            int maxIndex = terrainData.heightmapResolution - 1;
            float radius = Mathf.Max(1f, plan.seamBlendRadius);
            Vector3 runtimeWorldPosition = plan.RuntimeWorldPosition;
            if (!TryResolveRuntimeAbsoluteDouble(position, out double3 terrainAbsolutePosition))
                return default;

            float3 localPosition = ResolveTerrainLocalWorldPosition(
                in plan,
                in terrainAbsolutePosition,
                runtimeWorldPosition,
                position);
            float minX01 = Mathf.Clamp01((localPosition.x - radius) / size.x);
            float maxX01 = Mathf.Clamp01((localPosition.x + radius) / size.x);
            float minZ01 = Mathf.Clamp01((localPosition.z - radius) / size.z);
            float maxZ01 = Mathf.Clamp01((localPosition.z + radius) / size.z);

            int minX = Mathf.FloorToInt(minX01 * maxIndex);
            int maxX = Mathf.CeilToInt(maxX01 * maxIndex);
            int minZ = Mathf.FloorToInt(minZ01 * maxIndex);
            int maxZ = Mathf.CeilToInt(maxZ01 * maxIndex);
            return ClampRect(new RectInt(minX, minZ, maxX - minX + 1, maxZ - minZ + 1), maxIndex, maxIndex);
        }

        private static RectInt BuildTrenchRect(UnityEngine.Terrain terrain, in SeismicTrenchState trench)
        {
            UnityEngine.TerrainData terrainData = terrain.terrainData;
            if (terrainData == null || terrainData.heightmapResolution < 2)
                return default;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            if (terrainSize.x <= 0.001f || terrainSize.z <= 0.001f)
                return default;

            Vector3 runtimeStart = HectonFloatingOrigin.ToRuntimePosition(trench.AbsoluteStart);
            Vector3 runtimeEnd = HectonFloatingOrigin.ToRuntimePosition(trench.AbsoluteEnd);
            if (!TryResolveRuntimeAbsoluteDouble(terrainPosition, out double3 terrainAbsolutePosition))
                return default;

            float3 localStart = ToTerrainLocalFloat3(
                new double3(trench.AbsoluteStart.x, trench.AbsoluteStart.y, trench.AbsoluteStart.z),
                terrainAbsolutePosition,
                runtimeStart,
                terrainPosition);
            float3 localEnd = ToTerrainLocalFloat3(
                new double3(trench.AbsoluteEnd.x, trench.AbsoluteEnd.y, trench.AbsoluteEnd.z),
                terrainAbsolutePosition,
                runtimeEnd,
                terrainPosition);
            float radius = Mathf.Max(1f, trench.InfluenceRadius);
            float minLocalX = Mathf.Min(localStart.x, localEnd.x) - radius;
            float maxLocalX = Mathf.Max(localStart.x, localEnd.x) + radius;
            float minLocalZ = Mathf.Min(localStart.z, localEnd.z) - radius;
            float maxLocalZ = Mathf.Max(localStart.z, localEnd.z) + radius;

            int maxIndex = terrainData.heightmapResolution - 1;
            float minX01 = Mathf.Clamp01(minLocalX / terrainSize.x);
            float maxX01 = Mathf.Clamp01(maxLocalX / terrainSize.x);
            float minZ01 = Mathf.Clamp01(minLocalZ / terrainSize.z);
            float maxZ01 = Mathf.Clamp01(maxLocalZ / terrainSize.z);
            int minX = Mathf.FloorToInt(minX01 * maxIndex);
            int maxX = Mathf.CeilToInt(maxX01 * maxIndex);
            int minZ = Mathf.FloorToInt(minZ01 * maxIndex);
            int maxZ = Mathf.CeilToInt(maxZ01 * maxIndex);
            return ClampRect(new RectInt(minX, minZ, maxX - minX + 1, maxZ - minZ + 1), maxIndex, maxIndex);
        }

        private static RectInt UnionRect(RectInt a, RectInt b)
        {
            int minX = Mathf.Min(a.xMin, b.xMin);
            int minY = Mathf.Min(a.yMin, b.yMin);
            int maxX = Mathf.Max(a.xMax, b.xMax);
            int maxY = Mathf.Max(a.yMax, b.yMax);
            return new RectInt(minX, minY, maxX - minX, maxY - minY);
        }

        private static RectInt ClampRect(RectInt rect, int maxX, int maxY)
        {
            int xMin = Mathf.Clamp(rect.xMin, 0, maxX);
            int yMin = Mathf.Clamp(rect.yMin, 0, maxY);
            int xMax = Mathf.Clamp(rect.xMax, 0, maxX + 1);
            int yMax = Mathf.Clamp(rect.yMax, 0, maxY + 1);
            return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
        }

        private static float DistanceSqPointToSegment(
            Vector2 point,
            Vector2 start,
            Vector2 segment,
            float segmentLengthSq)
        {
            if (segmentLengthSq <= 0.0001f)
            {
                Vector2 deltaToStart = point - start;
                return Vector2.Dot(deltaToStart, deltaToStart);
            }

            float projected = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSq);
            Vector2 closest = start + segment * projected;
            Vector2 deltaToClosest = point - closest;
            return Vector2.Dot(deltaToClosest, deltaToClosest);
        }

        private static float LengthFromSqNoSqrt(float lengthSq)
        {
            float safeLengthSq = math.max(lengthSq, 0.000001f);
            return safeLengthSq * math.rsqrt(safeLengthSq);
        }

        private UnityEngine.Terrain ResolveTerrainAt(float x, float z)
        {
            if (mapMagicBridge != null &&
                mapMagicBridge.TryResolveTerrainAt(x, z, out UnityEngine.Terrain bridgeTerrain))
            {
                return bridgeTerrain;
            }

            return null;
        }

        private void RefreshColdReferences()
        {
            WorldRuntimeReferenceUtility.TryResolveWorldGenerativeGeologyIntegrationDirector(ref integrationDirector);
            WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
        }

        private void RefreshDataVaultRoute()
        {
            _dataVault = GlobalRegistry.DataVault;
        }
    }
}
