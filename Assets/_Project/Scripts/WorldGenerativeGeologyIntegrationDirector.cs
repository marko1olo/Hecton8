using System.Collections.Generic;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4031)]
    public sealed class WorldGenerativeGeologyIntegrationDirector : MonoBehaviour, ISlowTickable, IOriginShiftListener
    {
        private const int PlanRuntimeKeyCapacity = 256;

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private MapMagicBridge mapMagicBridge;
        [SerializeField] private HectonVoxelEngine voxelEngine;
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;

        [Header("Planning")]
        [SerializeField] private bool includeInactiveBindings;
        [SerializeField] private int maxTrackedPlans = 256;
        [SerializeField, Min(0f)] private float autoResolveRetryInterval = 1f;
        [SerializeField] private float searchRadiusPadding = 24f;
        [SerializeField] private float terrainDeltaBlendWindow = 18f;
        [SerializeField] private float minPlanWeight = 0.12f;
        [SerializeField] private float missingBindingGraceSeconds = 8f;
        [SerializeField] private float planRefreshDistanceThreshold = 6f;
        [SerializeField] private float planForcedRefreshInterval = 0f;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugReady;
        [SerializeField] private bool _debugBridgeReady;
        [SerializeField] private bool _debugVoxelEngineReady;
        [SerializeField] private float _debugSearchRadius;
        [SerializeField] private int _debugBindingsSeen;
        [SerializeField] private int _debugTrackedPlans;
        [SerializeField] private int _debugTerrainBlendPlans;
        [SerializeField] private int _debugVoxelBlendPlans;
        [SerializeField] private int _debugDebrisPlans;
        [SerializeField] private string _debugTopPlanFamilyId = string.Empty;
        [SerializeField] private WorldGenerativeGeologyProfile.ShapeArchetype _debugTopPlanArchetype;
        [SerializeField] private float _debugTopPlanWeight;

        private readonly Dictionary<long, WorldGenerativeGeologySeamPlan> _plansByKey = new Dictionary<long, WorldGenerativeGeologySeamPlan>(256);
        private readonly Dictionary<long, WorldGenerativeGeologyBinding> _bindingsByKey = new Dictionary<long, WorldGenerativeGeologyBinding>(256);
        private readonly Dictionary<long, WorldGenerativeGeologySeamPlan> _retainedPlansByKey = new Dictionary<long, WorldGenerativeGeologySeamPlan>(256);
        private readonly Dictionary<long, WorldGenerativeGeologyBinding> _retainedBindingsByKey = new Dictionary<long, WorldGenerativeGeologyBinding>(256);
        private readonly Dictionary<long, float> _bindingLastSeenTimes = new Dictionary<long, float>(256);
        private readonly List<WorldGenerativeGeologySeamPlan> _orderedPlans = new List<WorldGenerativeGeologySeamPlan>(256);
        private readonly List<long> _retainedRuntimeKeys = new List<long>(256);
        private readonly List<WorldGenerativeGeologySeamPlan> _selectionBuffer = new List<WorldGenerativeGeologySeamPlan>(256);
        private readonly List<WorldGenerativeGeologyBinding> _bindingScanBuffer = new List<WorldGenerativeGeologyBinding>(256);
        private readonly List<long> _dictionaryTrimBuffer = new List<long>(256);
        private readonly HashSet<long> _selectedRuntimeKeys = new HashSet<long>(PlanRuntimeKeyCapacity);
        private bool _registeredToTickManager;
        private bool _hasPlanRefreshSample;
        private Vector3 _lastPlanRefreshPosition;
        private float _lastPlanRefreshTime = float.NegativeInfinity;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;

        internal static WorldGenerativeGeologyIntegrationDirector ActiveRuntimeInstance { get; private set; }

        public IReadOnlyList<WorldGenerativeGeologySeamPlan> ActivePlans => _orderedPlans;
        public int ActivePlanCount => _orderedPlans.Count;
        public bool HasActivePlans => _orderedPlans.Count > 0;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            ResolveReferences();
            RebuildIntegrationPlans();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (Application.isPlaying)
                HectonFloatingOrigin.RegisterListener(this);

            TryRegister();
        }

        private void Start()
        {
            TryRegister();

            RebuildIntegrationPlans();
        }

        private void OnDisable()
        {
            TryUnregister();
            HectonFloatingOrigin.UnregisterListener(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            HectonFloatingOrigin.UnregisterListener(this);

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void TryRegister()
        {
            if (_registeredToTickManager)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
        }

        public void SlowTick()
        {
            if (ShouldSkipPlanRefresh())
                return;

            RebuildIntegrationPlans();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || !_hasPlanRefreshSample || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            _lastPlanRefreshPosition += -shiftData.ShiftOffset;
        }

        public void SetPlayerTransform(Transform target)
        {
            playerTransform = target;
            InvalidatePlanRefreshSample();
        }

        public void SetMapMagicBridge(MapMagicBridge bridge)
        {
            mapMagicBridge = bridge;
            InvalidatePlanRefreshSample();
        }

        public void SetVoxelEngine(HectonVoxelEngine engine)
        {
            voxelEngine = engine;
            InvalidatePlanRefreshSample();
        }

        public void SetChunkStreamingProfile(WorldChunkStreamingProfile profile)
        {
            chunkStreamingProfile = profile;
            InvalidatePlanRefreshSample();
        }

        public bool TryGetPlan(long runtimeKey, out WorldGenerativeGeologySeamPlan plan)
        {
            return _plansByKey.TryGetValue(runtimeKey, out plan);
        }

        public bool TryGetBinding(long runtimeKey, out WorldGenerativeGeologyBinding binding)
        {
            return _bindingsByKey.TryGetValue(runtimeKey, out binding);
        }

        public bool TryGetTopPlan(out WorldGenerativeGeologySeamPlan plan, out WorldGenerativeGeologyBinding binding)
        {
            plan = default;
            binding = null;

            if (_orderedPlans.Count == 0)
                return false;

            plan = _orderedPlans[0];
            if (!_bindingsByKey.TryGetValue(plan.runtimeKey, out binding) || binding == null)
            {
                binding = null;
                return false;
            }

            return true;
        }

        public void CopyPlansTo(List<WorldGenerativeGeologySeamPlan> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            destination.AddRange(_orderedPlans);
        }

        public void RebuildIntegrationPlans()
        {
            ResolveReferences();

            CaptureRetainedRuntimeKeys();
            CaptureRetainedBindings();
            _plansByKey.Clear();
            _bindingsByKey.Clear();
            _orderedPlans.Clear();
            _debugBindingsSeen = 0;
            _debugTrackedPlans = 0;
            _debugTerrainBlendPlans = 0;
            _debugVoxelBlendPlans = 0;
            _debugDebrisPlans = 0;
            _debugTopPlanFamilyId = string.Empty;
            _debugTopPlanArchetype = default;
            _debugTopPlanWeight = 0f;
            _debugReady = false;
            _debugBridgeReady = mapMagicBridge != null && mapMagicBridge.IsAvailable;
            _debugVoxelEngineReady = voxelEngine != null;

            if (playerTransform == null)
                return;

            float searchRadius = ResolveSearchRadius();
            float now = Application.isPlaying ? Time.unscaledTime : 0f;
            _debugSearchRadius = searchRadius;

            if (includeInactiveBindings && !Application.isPlaying)
            {
                WorldGenerativeGeologyBinding.CopyKnownBindingsTo(_bindingScanBuffer, true);
                for (int i = 0; i < _bindingScanBuffer.Count; i++)
                    ConsumeBinding(_bindingScanBuffer[i], searchRadius, now);
            }
            else
            {
                WorldGenerativeGeologyBinding.CopyActiveBindingsTo(_bindingScanBuffer);
                for (int i = 0; i < _bindingScanBuffer.Count; i++)
                    ConsumeBinding(_bindingScanBuffer[i], searchRadius, now);
            }

            RestoreRecentlyMissingPlans(searchRadius, now);

            _orderedPlans.Sort(CompareByWeightDescending);
            StabilizeTrackedPlans();

            for (int i = 0; i < _orderedPlans.Count; i++)
            {
                WorldGenerativeGeologySeamPlan plan = _orderedPlans[i];
                if (plan.RequiresTerrainBlend)
                    _debugTerrainBlendPlans++;
                if (plan.RequiresVoxelBlend)
                    _debugVoxelBlendPlans++;
                if (plan.RequiresDebrisSeam)
                    _debugDebrisPlans++;
            }

            if (_orderedPlans.Count > 0)
            {
                WorldGenerativeGeologySeamPlan topPlan = _orderedPlans[0];
                _debugTopPlanFamilyId = topPlan.familyId ?? string.Empty;
                _debugTopPlanArchetype = topPlan.archetype;
                _debugTopPlanWeight = topPlan.planWeight;
            }

            _debugTrackedPlans = _orderedPlans.Count;
            _debugReady = _orderedPlans.Count > 0;
            RecordPlanRefreshSample();
        }

        private void ConsumeBinding(WorldGenerativeGeologyBinding binding, float searchRadius, float now)
        {
            if (binding == null)
                return;

            _debugBindingsSeen++;
            if (!TryBuildPlan(binding, searchRadius, out WorldGenerativeGeologySeamPlan plan))
                return;

            _plansByKey[plan.runtimeKey] = plan;
            _bindingsByKey[plan.runtimeKey] = binding;
            _bindingLastSeenTimes[plan.runtimeKey] = now;
            _orderedPlans.Add(plan);
        }

        private bool ShouldSkipPlanRefresh()
        {
            if (!_hasPlanRefreshSample || playerTransform == null)
                return false;

            if (planForcedRefreshInterval > 0f)
            {
                float forcedInterval = Mathf.Max(0.5f, planForcedRefreshInterval);
                if (Application.isPlaying && Time.unscaledTime - _lastPlanRefreshTime >= forcedInterval)
                    return false;
            }

            float threshold = Mathf.Max(0.5f, planRefreshDistanceThreshold);
            return (playerTransform.position - _lastPlanRefreshPosition).sqrMagnitude < threshold * threshold;
        }

        private void RecordPlanRefreshSample()
        {
            if (playerTransform == null)
                return;

            _hasPlanRefreshSample = true;
            _lastPlanRefreshPosition = playerTransform.position;
            _lastPlanRefreshTime = Application.isPlaying ? Time.unscaledTime : 0f;
        }

        private void InvalidatePlanRefreshSample()
        {
            _hasPlanRefreshSample = false;
            _lastPlanRefreshTime = float.NegativeInfinity;
        }

        private void CaptureRetainedRuntimeKeys()
        {
            _retainedRuntimeKeys.Clear();
            for (int i = 0; i < _orderedPlans.Count; i++)
                _retainedRuntimeKeys.Add(_orderedPlans[i].runtimeKey);
        }

        private void CaptureRetainedBindings()
        {
            _retainedPlansByKey.Clear();
            _retainedBindingsByKey.Clear();

            for (int i = 0; i < _orderedPlans.Count; i++)
            {
                WorldGenerativeGeologySeamPlan plan = _orderedPlans[i];
                _retainedPlansByKey[plan.runtimeKey] = plan;

                if (_bindingsByKey.TryGetValue(plan.runtimeKey, out WorldGenerativeGeologyBinding binding) &&
                    binding != null)
                {
                    _retainedBindingsByKey[plan.runtimeKey] = binding;
                }
            }
        }

        private void RestoreRecentlyMissingPlans(float searchRadius, float now)
        {
            if (_retainedRuntimeKeys.Count == 0)
                return;

            float graceSeconds = Mathf.Max(0.25f, missingBindingGraceSeconds);
            for (int i = 0; i < _retainedRuntimeKeys.Count; i++)
            {
                long runtimeKey = _retainedRuntimeKeys[i];
                if (runtimeKey == 0L || _plansByKey.ContainsKey(runtimeKey))
                    continue;

                if (!_bindingLastSeenTimes.TryGetValue(runtimeKey, out float lastSeenTime) ||
                    now - lastSeenTime > graceSeconds)
                {
                    continue;
                }

                if (!_retainedBindingsByKey.TryGetValue(runtimeKey, out WorldGenerativeGeologyBinding binding) ||
                    binding == null)
                {
                    continue;
                }

                if (!TryBuildPlan(binding, searchRadius, out WorldGenerativeGeologySeamPlan plan))
                {
                    if (!_retainedPlansByKey.TryGetValue(runtimeKey, out plan))
                        continue;

                    Vector3 runtimeWorldPosition = binding.transform.position;
                    float residencyRadius = searchRadius + Mathf.Max(4f, plan.seamBlendRadius);
                    float residencyRadiusSq = residencyRadius * residencyRadius;
                    Vector3 playerDelta = playerTransform.position - runtimeWorldPosition;
                    if (playerDelta.sqrMagnitude > residencyRadiusSq)
                        continue;

                    float playerDistance = ApproximateDistanceNoSqrt(playerDelta);
                    double3 absoluteUniversePositionDouble = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimeWorldPosition);
                    plan.absoluteUniversePosition = ToVector3(absoluteUniversePositionDouble);
                    plan.absoluteUniverseAup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteUniversePositionDouble);
                    plan.hasAbsoluteUniverseAup = true;
                    plan.worldRotation = binding.transform.rotation;
                    plan.worldScale = binding.transform.lossyScale;
                    plan.playerDistance = playerDistance;
                    float terrainHeight = 0f;
                    bool hasTerrainSample = mapMagicBridge != null && mapMagicBridge.TryGetHeight(runtimeWorldPosition.x, runtimeWorldPosition.z, out terrainHeight);
                    float voxelCenterY = hasTerrainSample
                        ? terrainHeight - plan.suggestedTerrainCut * 0.35f + plan.voxelVolumeSize.y * 0.5f
                        : runtimeWorldPosition.y;
                    Vector3 runtimeTerrainPosition = new Vector3(runtimeWorldPosition.x, hasTerrainSample ? terrainHeight : runtimeWorldPosition.y, runtimeWorldPosition.z);
                    plan.hasTerrainSample = hasTerrainSample;
                    double3 absoluteTerrainPositionDouble = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimeTerrainPosition);
                    plan.absoluteTerrainHeight = (float)absoluteTerrainPositionDouble.y;
                    plan.absoluteTerrainContactAup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteTerrainPositionDouble);
                    plan.hasAbsoluteTerrainContactAup = true;
                    plan.terrainDelta = hasTerrainSample ? runtimeWorldPosition.y - terrainHeight : 0f;
                    Vector3 runtimeVoxelCenter = new Vector3(runtimeWorldPosition.x, voxelCenterY, runtimeWorldPosition.z);
                    double3 absoluteVoxelCenterDouble = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimeVoxelCenter);
                    plan.absoluteVoxelVolumeCenter = ToVector3(absoluteVoxelCenterDouble);
                    plan.absoluteVoxelVolumeCenterAup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteVoxelCenterDouble);
                    plan.hasAbsoluteVoxelVolumeCenterAup = true;
                }

                _plansByKey[runtimeKey] = plan;
                _bindingsByKey[runtimeKey] = binding;
                _orderedPlans.Add(plan);
            }
        }

        private void StabilizeTrackedPlans()
        {
            int capacity = Mathf.Max(1, maxTrackedPlans);
            if (_orderedPlans.Count <= capacity)
                return;

            _selectionBuffer.Clear();
            _selectedRuntimeKeys.Clear();

            for (int i = 0; i < _retainedRuntimeKeys.Count && _selectionBuffer.Count < capacity; i++)
            {
                long runtimeKey = _retainedRuntimeKeys[i];
                if (!_plansByKey.TryGetValue(runtimeKey, out WorldGenerativeGeologySeamPlan plan))
                    continue;

                if (_selectedRuntimeKeys.Add(runtimeKey))
                    _selectionBuffer.Add(plan);
            }

            for (int i = 0; i < _orderedPlans.Count && _selectionBuffer.Count < capacity; i++)
            {
                WorldGenerativeGeologySeamPlan plan = _orderedPlans[i];
                if (_selectedRuntimeKeys.Add(plan.runtimeKey))
                    _selectionBuffer.Add(plan);
            }

            _orderedPlans.Clear();
            _orderedPlans.AddRange(_selectionBuffer);
            TrimPlanDictionaries();
        }

        private void TrimPlanDictionaries()
        {
            _dictionaryTrimBuffer.Clear();
            Dictionary<long, WorldGenerativeGeologySeamPlan>.Enumerator planEnumerator = _plansByKey.GetEnumerator();
            while (planEnumerator.MoveNext())
            {
                long runtimeKey = planEnumerator.Current.Key;
                if (!_selectedRuntimeKeys.Contains(runtimeKey))
                    _dictionaryTrimBuffer.Add(runtimeKey);
            }

            for (int i = 0; i < _dictionaryTrimBuffer.Count; i++)
                _plansByKey.Remove(_dictionaryTrimBuffer[i]);

            _dictionaryTrimBuffer.Clear();
            Dictionary<long, WorldGenerativeGeologyBinding>.Enumerator bindingEnumerator = _bindingsByKey.GetEnumerator();
            while (bindingEnumerator.MoveNext())
            {
                long runtimeKey = bindingEnumerator.Current.Key;
                if (!_selectedRuntimeKeys.Contains(runtimeKey))
                    _dictionaryTrimBuffer.Add(runtimeKey);
            }

            for (int i = 0; i < _dictionaryTrimBuffer.Count; i++)
                _bindingsByKey.Remove(_dictionaryTrimBuffer[i]);
        }

        private bool TryBuildPlan(
            WorldGenerativeGeologyBinding binding,
            float searchRadius,
            out WorldGenerativeGeologySeamPlan plan)
        {
            plan = default;

            if (binding == null || binding.transform == null)
                return false;

            Transform targetTransform = binding.transform;
            Vector3 runtimeWorldPosition = targetTransform.position;
            double3 absoluteUniversePositionDouble = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimeWorldPosition);
            Vector3 absoluteUniversePosition = ToVector3(absoluteUniversePositionDouble);
            float residencyRadius = searchRadius + Mathf.Max(4f, binding.SeamBlendRadius);
            float residencyRadiusSq = residencyRadius * residencyRadius;
            Vector3 playerDelta = playerTransform.position - runtimeWorldPosition;
            if (playerDelta.sqrMagnitude > residencyRadiusSq)
                return false;

            float playerDistance = ApproximateDistanceNoSqrt(playerDelta);
            WorldProceduralProxyInstance metadata = binding.CachedProxyInstance;
            long runtimeKey = binding.RuntimeKey != 0L
                ? binding.RuntimeKey
                : (binding.CachedProxyRuntimeKey != 0L ? binding.CachedProxyRuntimeKey : BuildFallbackRuntimeKey(absoluteUniversePositionDouble));
            if (runtimeKey == 0L)
                return false;

            float terrainHeight = 0f;
            bool hasTerrainSample = mapMagicBridge != null && mapMagicBridge.TryGetHeight(runtimeWorldPosition.x, runtimeWorldPosition.z, out terrainHeight);
            float terrainDelta = hasTerrainSample ? runtimeWorldPosition.y - terrainHeight : 0f;
            float terrainAnchor = hasTerrainSample
                ? 1f - Mathf.Clamp01(Mathf.Abs(terrainDelta) / Mathf.Max(6f, terrainDeltaBlendWindow))
                : 0.45f;

            float terrainWeight = EvaluateTerrainBlendWeight(binding, terrainAnchor);
            float voxelWeight = EvaluateVoxelBlendWeight(binding);
            float debrisWeight = EvaluateDebrisWeight(binding);
            float proximityWeight = 1f - Mathf.Clamp01(playerDistance / Mathf.Max(12f, residencyRadius));
            float planWeight = Mathf.Clamp01(
                terrainWeight * 0.42f +
                voxelWeight * 0.34f +
                debrisWeight * 0.14f +
                proximityWeight * 0.10f);

            if (planWeight < minPlanWeight)
                return false;

            WorldChunkCoordinate chunkCoord = metadata != null
                ? metadata.ChunkCoord
                : WorldChunkCoordinate.FromWorldPosition(absoluteUniversePosition, ResolveChunkSize());
            bool hasMacroZone = metadata != null && metadata.HasMacroZone;
            WorldMacroZoneCoordinate macroZoneCoord = hasMacroZone
                ? metadata.MacroZoneCoord
                : WorldMacroZoneCoordinate.FromWorldPosition(absoluteUniversePosition, ResolveMacroZoneSize());
            float blendRadius = Mathf.Max(2f, binding.SeamBlendRadius);
            float voxelHeight = Mathf.Max(10f, binding.SuggestedTerrainRaise + binding.SuggestedTerrainCut + blendRadius * 0.75f);
            Vector3 voxelSize = new Vector3(blendRadius * 2f, voxelHeight, blendRadius * 2f);
            float voxelCenterY = hasTerrainSample
                ? terrainHeight - binding.SuggestedTerrainCut * 0.35f + voxelHeight * 0.5f
                : runtimeWorldPosition.y;
            Vector3 runtimeVoxelVolumeCenter = new Vector3(runtimeWorldPosition.x, voxelCenterY, runtimeWorldPosition.z);
            Vector3 runtimeTerrainPosition = new Vector3(runtimeWorldPosition.x, hasTerrainSample ? terrainHeight : runtimeWorldPosition.y, runtimeWorldPosition.z);
            double3 absoluteTerrainPositionDouble = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimeTerrainPosition);
            double3 absoluteVoxelVolumeCenterDouble = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimeVoxelVolumeCenter);
            AbsoluteUniversePosition absoluteUniverseAup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteUniversePositionDouble);
            AbsoluteUniversePosition absoluteTerrainContactAup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteTerrainPositionDouble);
            AbsoluteUniversePosition absoluteVoxelVolumeCenterAup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteVoxelVolumeCenterDouble);

            plan = new WorldGenerativeGeologySeamPlan
            {
                runtimeKey = runtimeKey,
                familyId = metadata != null ? metadata.FamilyId : binding.FamilyId,
                geologyProfileId = metadata != null && !string.IsNullOrWhiteSpace(metadata.GeologyProfileId) ? metadata.GeologyProfileId : binding.GeologyProfileId,
                composition = binding.CompositionLabel,
                archetype = binding.Archetype,
                terrainSeamMode = binding.TerrainSeamMode,
                caveBlendMode = binding.CaveBlendMode,
                streamingLayer = metadata != null ? metadata.ActiveStreamingLayer : WorldStreamingLayer.TerrainLod,
                chunkX = chunkCoord.x,
                chunkZ = chunkCoord.z,
                hasMacroZone = hasMacroZone,
                macroZoneX = macroZoneCoord.x,
                macroZoneZ = macroZoneCoord.z,
                absoluteUniversePosition = absoluteUniversePosition,
                absoluteUniverseAup = absoluteUniverseAup,
                hasAbsoluteUniverseAup = true,
                worldRotation = targetTransform.rotation,
                worldScale = targetTransform.lossyScale,
                playerDistance = playerDistance,
                hasTerrainSample = hasTerrainSample,
                absoluteTerrainHeight = (float)absoluteTerrainPositionDouble.y,
                absoluteTerrainContactAup = absoluteTerrainContactAup,
                hasAbsoluteTerrainContactAup = true,
                terrainDelta = terrainDelta,
                seamBlendRadius = blendRadius,
                suggestedTerrainRaise = binding.SuggestedTerrainRaise,
                suggestedTerrainCut = binding.SuggestedTerrainCut,
                suggestedDebrisCount = binding.SuggestedDebrisCount,
                slopeDegrees = binding.SlopeDegrees,
                curvature = binding.Curvature,
                caveProximity = binding.CaveProximity,
                ridgeSignal = binding.RidgeSignal,
                canyonSignal = binding.CanyonSignal,
                compositionPotential = binding.CompositionPotential,
                terrainBlendWeight = terrainWeight,
                caveBlendWeight = voxelWeight,
                debrisWeight = debrisWeight,
                planWeight = planWeight,
                absoluteVoxelVolumeCenter = ToVector3(absoluteVoxelVolumeCenterDouble),
                absoluteVoxelVolumeCenterAup = absoluteVoxelVolumeCenterAup,
                hasAbsoluteVoxelVolumeCenterAup = true,
                voxelVolumeSize = voxelSize
            };

            return true;
        }

        private float ResolveSearchRadius()
        {
            float searchRadius = 128f;
            if (chunkStreamingProfile != null)
            {
                WorldChunkStreamingProfile.LayerProfile terrainLayer =
                    chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.TerrainLod);
                WorldChunkStreamingProfile.LayerProfile constructionLayer =
                    chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.Construction);

                float terrainRadius = chunkStreamingProfile.visualResidencyRadius * Mathf.Max(0.4f, terrainLayer.farRadiusScale);
                float constructionRadius = chunkStreamingProfile.visualResidencyRadius * Mathf.Max(0.4f, constructionLayer.farRadiusScale);
                searchRadius = Mathf.Max(searchRadius, terrainRadius, constructionRadius, chunkStreamingProfile.fullSimulationRadius);
            }

            return searchRadius + Mathf.Max(0f, searchRadiusPadding);
        }

        private float ResolveChunkSize()
        {
            return chunkStreamingProfile != null
                ? Mathf.Max(1f, chunkStreamingProfile.chunkSizeMeters)
                : 192f;
        }

        private float ResolveMacroZoneSize()
        {
            return chunkStreamingProfile != null
                ? Mathf.Max(ResolveChunkSize(), chunkStreamingProfile.macroZoneSizeMeters)
                : 768f;
        }

        private static float EvaluateTerrainBlendWeight(WorldGenerativeGeologyBinding binding, float terrainAnchor)
        {
            if (binding == null || binding.TerrainSeamMode == WorldGenerativeGeologyProfile.TerrainSeamMode.None)
                return 0f;

            float raiseWeight = Mathf.Clamp01(binding.SuggestedTerrainRaise / 6f);
            float cutWeight = Mathf.Clamp01(binding.SuggestedTerrainCut / 6f);
            float shapeWeight = Mathf.Clamp01(
                binding.RidgeSignal * 0.28f +
                binding.CanyonSignal * 0.18f +
                binding.CompositionPotential * 0.26f +
                (1f - Mathf.Clamp01(Mathf.Abs(binding.Curvature))) * 0.12f +
                Mathf.Clamp01(binding.SlopeDegrees / 55f) * 0.16f);

            return Mathf.Clamp01((Mathf.Max(raiseWeight, cutWeight) * 0.55f + shapeWeight * 0.45f) * terrainAnchor);
        }

        private static float EvaluateVoxelBlendWeight(WorldGenerativeGeologyBinding binding)
        {
            if (binding == null || binding.CaveBlendMode == WorldGenerativeGeologyProfile.CaveBlendMode.None)
                return 0f;

            float archetypeBias = binding.Archetype == WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge ? 0.2f : 0f;
            return Mathf.Clamp01(
                binding.CaveProximity * 0.5f +
                binding.CanyonSignal * 0.15f +
                binding.CompositionPotential * 0.15f +
                Mathf.Clamp01(binding.SuggestedTerrainCut / 6f) * 0.12f +
                archetypeBias +
                Mathf.Clamp01(binding.LodCount / 3f) * 0.08f);
        }

        private static float EvaluateDebrisWeight(WorldGenerativeGeologyBinding binding)
        {
            if (binding == null || binding.SuggestedDebrisCount <= 0)
                return 0f;

            return Mathf.Clamp01(
                Mathf.Clamp01(binding.SuggestedDebrisCount / 10f) * 0.38f +
                binding.CompositionPotential * 0.24f +
                binding.CanyonSignal * 0.16f +
                binding.RidgeSignal * 0.12f +
                binding.CaveProximity * 0.10f);
        }

        private static float ApproximateDistanceNoSqrt(Vector3 delta)
        {
            float ax = Mathf.Abs(delta.x);
            float ay = Mathf.Abs(delta.y);
            float az = Mathf.Abs(delta.z);
            float max = Mathf.Max(ax, Mathf.Max(ay, az));
            float min = Mathf.Min(ax, Mathf.Min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + mid * 0.375f + min * 0.125f;
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private static long BuildFallbackRuntimeKey(double3 absoluteUniversePosition)
        {
            ulong hash = 1469598103934665603UL;
            hash = HashLong(hash, FastRoundToLong(absoluteUniversePosition.x * 1000d));
            hash = HashLong(hash, FastRoundToLong(absoluteUniversePosition.y * 1000d));
            hash = HashLong(hash, FastRoundToLong(absoluteUniversePosition.z * 1000d));
            long key = unchecked((long)(hash & 0x7fffffffffffffffUL));
            return key != 0L ? key : 1L;
        }

        private static ulong HashLong(ulong hash, long value)
        {
            ulong data = unchecked((ulong)value);
            hash = unchecked((hash ^ data) * 1099511628211UL);
            return unchecked((hash ^ (data >> 32)) * 1099511628211UL);
        }

        private static long FastRoundToLong(double value)
        {
            if (!math.isfinite(value))
                return 0L;

            const double MaxLongRoundTrip = 9223372036854770000d;
            double clamped = math.clamp(value, -MaxLongRoundTrip, MaxLongRoundTrip);
            return clamped >= 0d
                ? (long)(clamped + 0.5d)
                : (long)(clamped - 0.5d);
        }

        private void ResolveReferences()
        {
            if (playerTransform != null && mapMagicBridge != null && voxelEngine != null)
                return;

            float now = Time.realtimeSinceStartup;
            if (now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);

            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
            WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
            WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);
        }

        private static int CompareByWeightDescending(WorldGenerativeGeologySeamPlan left, WorldGenerativeGeologySeamPlan right)
        {
            return right.planWeight.CompareTo(left.planWeight);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_orderedPlans == null || _orderedPlans.Count == 0)
                return;

            for (int i = 0; i < _orderedPlans.Count; i++)
            {
                WorldGenerativeGeologySeamPlan plan = _orderedPlans[i];
                Color color = plan.RequiresVoxelBlend
                    ? new Color(0.22f, 0.78f, 1f, 0.7f)
                    : plan.RequiresTerrainBlend
                        ? new Color(0.35f, 1f, 0.52f, 0.7f)
                        : new Color(1f, 0.76f, 0.24f, 0.7f);

                Gizmos.color = color;
                Gizmos.DrawWireSphere(plan.TerrainContactPosition, plan.seamBlendRadius);

                if (plan.RequiresVoxelBlend)
                    Gizmos.DrawWireCube(plan.RuntimeVoxelVolumeCenter, plan.voxelVolumeSize);
            }
        }
#endif
    }
}
