using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4029)]
    public sealed class WorldGenerativeGeologyTerrainSeamApplier : MonoBehaviour, ISlowTickable
    {
        private struct SeismicTrenchState
        {
            public long TrenchId;
            public Vector3 AbsoluteStart;
            public Vector3 AbsoluteEnd;
            public float DepthMeters;
            public float Slope;
            public float InfluenceRadius;
        }

        internal static WorldGenerativeGeologyTerrainSeamApplier ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        private sealed class TerrainApplyState
        {
            public Terrain terrain;
            public TerrainData terrainData;
            public float[,] baselineHeights;
            public int heightmapResolution;
            public RectInt previousRect;
            public bool hasPreviousRect;
            public float[,] patchBuffer;
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
        [SerializeField] private int maxActiveTrenches = 8;
        [SerializeField] private float trenchRadiusPaddingMeters = 3f;
        [SerializeField] private float trenchRimBlendStrength = 0.55f;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugReady;
        [SerializeField] private int _debugAppliedTerrains;
        [SerializeField] private int _debugAppliedPlans;
        [SerializeField] private int _debugRestoredTerrains;
        [SerializeField] private int _debugTopTerrainId;

        private readonly Dictionary<int, TerrainApplyState> _terrainStates = new Dictionary<int, TerrainApplyState>(8);
        private readonly Dictionary<int, List<WorldGenerativeGeologySeamPlan>> _plansByTerrain = new Dictionary<int, List<WorldGenerativeGeologySeamPlan>>(8);
        private readonly Dictionary<int, List<SeismicTrenchState>> _trenchesByTerrain = new Dictionary<int, List<SeismicTrenchState>>(8);
        private readonly HashSet<int> _touchedTerrainIds = new HashSet<int>();
        private readonly List<int> _knownTerrainIds = new List<int>(8);
        private readonly List<SeismicTrenchState> _activeTrenches = new List<SeismicTrenchState>(8);
        private readonly List<int> _terrainBucketScratch = new List<int>(8);
        private bool _registeredToTickManager;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            ResolveReferences();
            ReconcileTerrainSeams();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            ResolveReferences();
            TryRegisterToTickManager();
        }

        private void Start()
        {
            TryRegisterToTickManager();
            ReconcileTerrainSeams();
        }

        private void OnDisable()
        {
            TryUnregisterFromTickManager();
            RestoreAllTerrains();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            TryUnregisterFromTickManager();
            RestoreAllTerrains();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void SlowTick()
        {
            ReconcileTerrainSeams();
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
            if (trenchId == 0L)
                return;

            SeismicTrenchState trench = new SeismicTrenchState
            {
                TrenchId = trenchId,
                AbsoluteStart = absoluteStart,
                AbsoluteEnd = absoluteEnd,
                DepthMeters = Mathf.Max(1f, trenchDepth),
                Slope = Mathf.Max(0.05f, trenchSlope),
                InfluenceRadius = Mathf.Max(2f, influenceRadius)
            };

            for (int i = 0; i < _activeTrenches.Count; i++)
            {
                if (_activeTrenches[i].TrenchId != trenchId)
                    continue;

                _activeTrenches[i] = trench;
                return;
            }

            if (_activeTrenches.Count >= Mathf.Max(1, maxActiveTrenches))
                _activeTrenches.RemoveAt(0);

            _activeTrenches.Add(trench);
        }

        public void ReconcileTerrainSeams()
        {
            ResolveReferences();

            ClearBuckets();
            _touchedTerrainIds.Clear();
            _debugAppliedTerrains = 0;
            _debugAppliedPlans = 0;
            _debugRestoredTerrains = 0;
            _debugTopTerrainId = 0;
            _debugReady = false;

            if (integrationDirector == null)
            {
                RestoreAllTerrains();
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
                Terrain terrain = ResolveTerrainAt(runtimeWorldPosition.x, runtimeWorldPosition.z);
                if (terrain == null || terrain.terrainData == null)
                    continue;

                int terrainId = unchecked((int)EntityId.ToULong(terrain.GetEntityId()));
                EnsureTerrainState(terrain, terrainId);
                List<WorldGenerativeGeologySeamPlan> terrainPlans = _plansByTerrain[terrainId];

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
            _debugReady = _debugAppliedPlans > 0;
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager)
                return;


            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = true;
        }

        private void TryUnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
        }

        private void ApplyTerrainPlans(
            TerrainApplyState state,
            List<WorldGenerativeGeologySeamPlan> plans,
            List<SeismicTrenchState> trenches)
        {
            Terrain terrain = state.terrain;
            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null || state.baselineHeights == null)
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

            RectInt applyRect = state.hasPreviousRect ? UnionRect(state.previousRect, currentRect) : currentRect;
            applyRect = ClampRect(applyRect, terrainData.heightmapResolution - 1, terrainData.heightmapResolution - 1);
            if (applyRect.width <= 0 || applyRect.height <= 0)
                return;

            float[,] patch = PreparePatchBuffer(state, applyRect);
            if (plans != null)
            {
                for (int i = 0; i < plans.Count; i++)
                    ApplyPlanToPatch(terrain, applyRect, patch, plans[i]);
            }

            if (trenches != null)
            {
                for (int i = 0; i < trenches.Count; i++)
                    ApplyTrenchToPatch(terrain, applyRect, patch, trenches[i]);
            }

            terrainData.SetHeightsDelayLOD(applyRect.x, applyRect.y, patch);
            terrainData.SyncHeightmap();
            state.previousRect = applyRect;
            state.hasPreviousRect = true;
        }

        private void ApplyPlanToPatch(
            Terrain terrain,
            RectInt patchRect,
            float[,] patch,
            in WorldGenerativeGeologySeamPlan plan)
        {
            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
                return;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            float invHeight = terrainSize.y > 0.001f ? 1f / terrainSize.y : 0f;
            float effectiveRadius = Mathf.Max(2f, plan.seamBlendRadius + radiusPaddingMeters);
            float desiredDelta = ResolveDesiredWorldDelta(plan);
            float desiredNormalizedDelta = desiredDelta * invHeight;
            if (Mathf.Abs(desiredNormalizedDelta) < 0.00001f)
                return;

            for (int patchZ = 0; patchZ < patchRect.height; patchZ++)
            {
                int heightmapZ = patchRect.y + patchZ;
                float worldZ = terrainPosition.z + (heightmapZ / (float)(terrainData.heightmapResolution - 1)) * terrainSize.z;

                for (int patchX = 0; patchX < patchRect.width; patchX++)
                {
                    int heightmapX = patchRect.x + patchX;
                    float worldX = terrainPosition.x + (heightmapX / (float)(terrainData.heightmapResolution - 1)) * terrainSize.x;
                    float distance = Vector2.Distance(
                        new Vector2(worldX, worldZ),
                        new Vector2(plan.RuntimeWorldPosition.x, plan.RuntimeWorldPosition.z));
                    if (distance > effectiveRadius)
                        continue;

                    float radial = 1f - Mathf.Clamp01(distance / effectiveRadius);
                    float falloff = Mathf.SmoothStep(0f, 1f, radial);
                    float rim = Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, radial * radial), rimSmoothing);
                    float shapeBias = Mathf.Clamp01(
                        plan.terrainBlendWeight * 0.45f +
                        plan.ridgeSignal * 0.18f +
                        plan.canyonSignal * 0.12f +
                        plan.compositionPotential * 0.15f +
                        plan.caveProximity * 0.10f);

                    float normalizedDelta = desiredNormalizedDelta * falloff * rim * shapeBias;
                    patch[patchZ, patchX] = Mathf.Clamp01(patch[patchZ, patchX] + normalizedDelta);
                }
            }
        }

        private float ResolveDesiredWorldDelta(in WorldGenerativeGeologySeamPlan plan)
        {
            float upward = Mathf.Max(0f, plan.terrainDelta) + plan.suggestedTerrainRaise;
            float downward = Mathf.Max(0f, -plan.terrainDelta) + plan.suggestedTerrainCut * Mathf.Clamp01(plan.caveBlendWeight + 0.25f);
            float raise = upward * raiseStrength * Mathf.Clamp01(plan.terrainBlendWeight + plan.ridgeSignal * 0.25f);
            float cut = downward * cutStrength * Mathf.Clamp01(plan.terrainBlendWeight + plan.canyonSignal * 0.3f);
            return raise - cut;
        }

        private void ApplyTrenchToPatch(
            Terrain terrain,
            RectInt patchRect,
            float[,] patch,
            in SeismicTrenchState trench)
        {
            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
                return;

            Vector3 runtimeStart = HectonFloatingOrigin.ToRuntimePosition(trench.AbsoluteStart);
            Vector3 runtimeEnd = HectonFloatingOrigin.ToRuntimePosition(trench.AbsoluteEnd);
            Vector2 start = new Vector2(runtimeStart.x, runtimeStart.z);
            Vector2 end = new Vector2(runtimeEnd.x, runtimeEnd.z);
            Vector2 segment = end - start;
            float segmentLengthSq = segment.sqrMagnitude;
            if (segmentLengthSq <= 0.0001f)
                return;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            float invHeight = terrainSize.y > 0.001f ? 1f / terrainSize.y : 0f;
            float safeSlope = Mathf.Max(0.05f, trench.Slope);
            float influenceRadius = Mathf.Max(trench.InfluenceRadius, trench.DepthMeters / safeSlope) + Mathf.Max(0f, trenchRadiusPaddingMeters);
            float rimBlend = Mathf.Clamp01(trenchRimBlendStrength);

            for (int patchZ = 0; patchZ < patchRect.height; patchZ++)
            {
                int heightmapZ = patchRect.y + patchZ;
                float worldZ = terrainPosition.z + (heightmapZ / (float)(terrainData.heightmapResolution - 1)) * terrainSize.z;

                for (int patchX = 0; patchX < patchRect.width; patchX++)
                {
                    int heightmapX = patchRect.x + patchX;
                    float worldX = terrainPosition.x + (heightmapX / (float)(terrainData.heightmapResolution - 1)) * terrainSize.x;
                    Vector2 point = new Vector2(worldX, worldZ);
                    float distanceToLine = DistancePointToSegment(point, start, end, segment, segmentLengthSq);
                    if (distanceToLine > influenceRadius)
                        continue;

                    float cutDepth = Mathf.Max(0f, trench.DepthMeters - distanceToLine * safeSlope);
                    if (cutDepth <= 0.0001f)
                        continue;

                    float radial = 1f - Mathf.Clamp01(distanceToLine / influenceRadius);
                    float rim = Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, radial * radial), rimBlend);
                    float normalizedDelta = -(cutDepth * cutStrength * rim * Mathf.SmoothStep(0f, 1f, radial)) * invHeight;
                    patch[patchZ, patchX] = Mathf.Clamp01(patch[patchZ, patchX] + normalizedDelta);
                }
            }
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

            TerrainData currentTerrainData = state.terrain.terrainData;
            if (currentTerrainData == null)
            {
                state.hasPreviousRect = false;
                state.previousRect = default;
                return;
            }

            int currentResolution = currentTerrainData.heightmapResolution;
            if (currentTerrainData != state.terrainData ||
                currentResolution != state.heightmapResolution ||
                state.baselineHeights == null)
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

            float[,] patch = PreparePatchBuffer(state, rect);
            state.terrainData.SetHeightsDelayLOD(rect.x, rect.y, patch);
            state.terrainData.SyncHeightmap();
            state.previousRect = default;
            state.hasPreviousRect = false;
        }

        private void EnsureTerrainState(Terrain terrain, int terrainId)
        {
            if (terrain == null || terrain.terrainData == null)
                return;

            TerrainData terrainData = terrain.terrainData;
            int resolution = terrainData.heightmapResolution;
            if (!_terrainStates.TryGetValue(terrainId, out TerrainApplyState state))
            {
                state = new TerrainApplyState();
                RefreshTerrainBaseline(state, terrain, terrainData, resolution);
                _terrainStates.Add(terrainId, state);
                _knownTerrainIds.Add(terrainId);
            }
            else if (state.terrain != terrain ||
                     state.terrainData != terrainData ||
                     state.baselineHeights == null ||
                     state.heightmapResolution != resolution)
            {
                RefreshTerrainBaseline(state, terrain, terrainData, resolution);
            }

            if (!_plansByTerrain.ContainsKey(terrainId))
            {
                // COLD ALLOC: one reusable plan bucket per touched terrain.
                _plansByTerrain.Add(terrainId, new List<WorldGenerativeGeologySeamPlan>(8));
            }

            if (!_trenchesByTerrain.ContainsKey(terrainId))
            {
                // COLD ALLOC: one reusable trench bucket per touched terrain.
                _trenchesByTerrain.Add(terrainId, new List<SeismicTrenchState>(4));
            }
        }

        private float[,] PreparePatchBuffer(TerrainApplyState state, RectInt rect)
        {
            if (state.patchBuffer == null ||
                state.patchBuffer.GetLength(0) != rect.height ||
                state.patchBuffer.GetLength(1) != rect.width)
            {
                // COLD ALLOC: resized only when seam footprint dimensions change.
                state.patchBuffer = new float[rect.height, rect.width];
            }

            CopyBaselinePatch(state.baselineHeights, rect, state.patchBuffer);
            return state.patchBuffer;
        }

        private static void RefreshTerrainBaseline(
            TerrainApplyState state,
            Terrain terrain,
            TerrainData terrainData,
            int resolution)
        {
            if (state == null || terrain == null || terrainData == null)
                return;

            // COLD ALLOC: full baseline snapshot is refreshed only when the
            // bound Terrain/TerrainData owner changes or heightmap resolution changes.
            state.terrain = terrain;
            state.terrainData = terrainData;
            state.heightmapResolution = resolution;
            state.baselineHeights = terrainData.GetHeights(0, 0, resolution, resolution);
            state.patchBuffer = null;
            state.previousRect = default;
            state.hasPreviousRect = false;
        }

        private static void CopyBaselinePatch(float[,] baseline, RectInt rect, float[,] destination)
        {
            for (int z = 0; z < rect.height; z++)
            {
                int sourceZ = rect.y + z;
                for (int x = 0; x < rect.width; x++)
                    destination[z, x] = baseline[sourceZ, rect.x + x];
            }
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
                float lineLength = Vector3.Distance(runtimeStart, runtimeEnd);
                int sampleCount = Mathf.Clamp(Mathf.CeilToInt(lineLength / 48f) + 1, 2, 8);
                _terrainBucketScratch.Clear();

                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float sampleT = sampleCount <= 1 ? 0f : sampleIndex / (float)(sampleCount - 1);
                    Vector3 samplePosition = Vector3.Lerp(runtimeStart, runtimeEnd, sampleT);
                    Terrain terrain = ResolveTerrainAt(samplePosition.x, samplePosition.z);
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
                    _trenchesByTerrain[terrainId].Add(trench);
                    _touchedTerrainIds.Add(terrainId);
                }
            }
        }

        private static RectInt BuildPlanRect(Terrain terrain, in WorldGenerativeGeologySeamPlan plan)
        {
            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null || terrainData.heightmapResolution < 2)
                return default;

            Vector3 position = terrain.transform.position;
            Vector3 size = terrainData.size;
            if (size.x <= 0.001f || size.z <= 0.001f || plan.seamBlendRadius <= 0f)
                return default;

            int maxIndex = terrainData.heightmapResolution - 1;
            float radius = Mathf.Max(1f, plan.seamBlendRadius);
            Vector3 runtimeWorldPosition = plan.RuntimeWorldPosition;
            float minX01 = Mathf.Clamp01((runtimeWorldPosition.x - radius - position.x) / size.x);
            float maxX01 = Mathf.Clamp01((runtimeWorldPosition.x + radius - position.x) / size.x);
            float minZ01 = Mathf.Clamp01((runtimeWorldPosition.z - radius - position.z) / size.z);
            float maxZ01 = Mathf.Clamp01((runtimeWorldPosition.z + radius - position.z) / size.z);

            int minX = Mathf.FloorToInt(minX01 * maxIndex);
            int maxX = Mathf.CeilToInt(maxX01 * maxIndex);
            int minZ = Mathf.FloorToInt(minZ01 * maxIndex);
            int maxZ = Mathf.CeilToInt(maxZ01 * maxIndex);
            return ClampRect(new RectInt(minX, minZ, maxX - minX + 1, maxZ - minZ + 1), maxIndex, maxIndex);
        }

        private static RectInt BuildTrenchRect(Terrain terrain, in SeismicTrenchState trench)
        {
            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null || terrainData.heightmapResolution < 2)
                return default;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            if (terrainSize.x <= 0.001f || terrainSize.z <= 0.001f)
                return default;

            Vector3 runtimeStart = HectonFloatingOrigin.ToRuntimePosition(trench.AbsoluteStart);
            Vector3 runtimeEnd = HectonFloatingOrigin.ToRuntimePosition(trench.AbsoluteEnd);
            float radius = Mathf.Max(1f, trench.InfluenceRadius);
            float minWorldX = Mathf.Min(runtimeStart.x, runtimeEnd.x) - radius;
            float maxWorldX = Mathf.Max(runtimeStart.x, runtimeEnd.x) + radius;
            float minWorldZ = Mathf.Min(runtimeStart.z, runtimeEnd.z) - radius;
            float maxWorldZ = Mathf.Max(runtimeStart.z, runtimeEnd.z) + radius;

            int maxIndex = terrainData.heightmapResolution - 1;
            float minX01 = Mathf.Clamp01((minWorldX - terrainPosition.x) / terrainSize.x);
            float maxX01 = Mathf.Clamp01((maxWorldX - terrainPosition.x) / terrainSize.x);
            float minZ01 = Mathf.Clamp01((minWorldZ - terrainPosition.z) / terrainSize.z);
            float maxZ01 = Mathf.Clamp01((maxWorldZ - terrainPosition.z) / terrainSize.z);
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

        private static float DistancePointToSegment(
            Vector2 point,
            Vector2 start,
            Vector2 end,
            Vector2 segment,
            float segmentLengthSq)
        {
            if (segmentLengthSq <= 0.0001f)
                return Vector2.Distance(point, start);

            float projected = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSq);
            Vector2 closest = start + segment * projected;
            return Vector2.Distance(point, closest);
        }

        private Terrain ResolveTerrainAt(float x, float z)
        {
            if (mapMagicBridge != null &&
                mapMagicBridge.TryResolveTerrainAt(x, z, out Terrain bridgeTerrain))
            {
                return bridgeTerrain;
            }

            return FindTerrainAtFallback(x, z);
        }

        private static Terrain FindTerrainAtFallback(float x, float z)
        {
            Terrain active = Terrain.activeTerrain;
            if (active != null && IsPointInTerrain(active, x, z))
                return active;

            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains == null)
                return null;

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain != null && IsPointInTerrain(terrain, x, z))
                    return terrain;
            }

            return null;
        }

        private static bool IsPointInTerrain(Terrain terrain, float x, float z)
        {
            if (terrain == null || terrain.terrainData == null)
                return false;

            Vector3 pos = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return x >= pos.x && x <= pos.x + size.x &&
                   z >= pos.z && z <= pos.z + size.z;
        }

        private void ResolveReferences()
        {
            WorldRuntimeReferenceUtility.TryResolveWorldGenerativeGeologyIntegrationDirector(ref integrationDirector);
            WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
        }
    }
}
