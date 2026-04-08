using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4029)]
    public sealed class WorldGenerativeGeologyTerrainSeamApplier : MonoBehaviour, ISlowTickable
    {
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

        [Header("Diagnostics")]
        [SerializeField] private bool _debugReady;
        [SerializeField] private int _debugAppliedTerrains;
        [SerializeField] private int _debugAppliedPlans;
        [SerializeField] private int _debugRestoredTerrains;
        [SerializeField] private int _debugTopTerrainId;

        private readonly Dictionary<int, TerrainApplyState> _terrainStates = new Dictionary<int, TerrainApplyState>(8);
        private readonly Dictionary<int, List<WorldGenerativeGeologySeamPlan>> _plansByTerrain = new Dictionary<int, List<WorldGenerativeGeologySeamPlan>>(8);
        private readonly HashSet<int> _touchedTerrainIds = new HashSet<int>();
        private readonly List<int> _knownTerrainIds = new List<int>(8);
        private bool _registeredToTickManager;

        private void Awake()
        {
            ResolveReferences();
            ReconcileTerrainSeams();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (GameTickManager.Instance != null && !_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }

            ReconcileTerrainSeams();
        }

        private void OnDisable()
        {
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }

            RestoreAllTerrains();
        }

        public void SlowTick()
        {
            ReconcileTerrainSeams();
        }

        public void SetIntegrationDirector(WorldGenerativeGeologyIntegrationDirector director)
        {
            integrationDirector = director;
        }

        public void ReconcileTerrainSeams()
        {
            ResolveReferences();

            ClearPlanBuckets();
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

                Terrain terrain = ResolveTerrainAt(plan.worldPosition.x, plan.worldPosition.z);
                if (terrain == null || terrain.terrainData == null)
                    continue;

                #pragma warning disable CS0618
                int terrainId = terrain.GetInstanceID();
                #pragma warning restore CS0618
                EnsureTerrainState(terrain, terrainId);
                List<WorldGenerativeGeologySeamPlan> terrainPlans = _plansByTerrain[terrainId];

                terrainPlans.Add(plan);
                _touchedTerrainIds.Add(terrainId);
                acceptedPlans++;
            }

            for (int i = 0; i < _knownTerrainIds.Count; i++)
            {
                int terrainId = _knownTerrainIds[i];
                if (!_plansByTerrain.TryGetValue(terrainId, out List<WorldGenerativeGeologySeamPlan> terrainPlans) ||
                    terrainPlans == null ||
                    terrainPlans.Count == 0)
                {
                    continue;
                }

                if (!_terrainStates.TryGetValue(terrainId, out TerrainApplyState state) || state == null || state.terrain == null)
                    continue;

                ApplyTerrainPlans(state, terrainPlans);
                _debugAppliedTerrains++;
                _debugAppliedPlans += terrainPlans.Count;
                if (_debugTopTerrainId == 0)
                    _debugTopTerrainId = terrainId;
            }

            RestoreUntouchedTerrains();
            _debugReady = _debugAppliedPlans > 0;
        }

        private void ApplyTerrainPlans(TerrainApplyState state, List<WorldGenerativeGeologySeamPlan> plans)
        {
            Terrain terrain = state.terrain;
            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null || state.baselineHeights == null)
                return;

            RectInt currentRect = default;
            bool hasCurrentRect = false;
            for (int i = 0; i < plans.Count; i++)
            {
                RectInt planRect = BuildPlanRect(terrain, plans[i]);
                if (planRect.width <= 0 || planRect.height <= 0)
                    continue;

                currentRect = hasCurrentRect ? UnionRect(currentRect, planRect) : planRect;
                hasCurrentRect = true;
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
            for (int i = 0; i < plans.Count; i++)
                ApplyPlanToPatch(terrain, applyRect, patch, plans[i]);

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
                        new Vector2(plan.worldPosition.x, plan.worldPosition.z));
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

        private void ClearPlanBuckets()
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
            }
        }

        private static RectInt BuildPlanRect(Terrain terrain, in WorldGenerativeGeologySeamPlan plan)
        {
            TerrainData terrainData = terrain.terrainData;
            Vector3 position = terrain.transform.position;
            Vector3 size = terrainData.size;
            int maxIndex = terrainData.heightmapResolution - 1;
            float radius = Mathf.Max(1f, plan.seamBlendRadius);
            float minX01 = Mathf.Clamp01((plan.worldPosition.x - radius - position.x) / size.x);
            float maxX01 = Mathf.Clamp01((plan.worldPosition.x + radius - position.x) / size.x);
            float minZ01 = Mathf.Clamp01((plan.worldPosition.z - radius - position.z) / size.z);
            float maxZ01 = Mathf.Clamp01((plan.worldPosition.z + radius - position.z) / size.z);

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
