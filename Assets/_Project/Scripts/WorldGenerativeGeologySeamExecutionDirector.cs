using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldGenerativeGeologySeamRuntime : MonoBehaviour
    {
        private static readonly List<WorldGenerativeGeologySeamRuntime> _activeRuntimes = new List<WorldGenerativeGeologySeamRuntime>(128);
        private static readonly List<int> _staleRuntimeIndexBuffer = new List<int>(32);

        [SerializeField] private long runtimeKey;
        [SerializeField] private int buildSignature;
        [SerializeField] private float planWeight;
        [SerializeField] private bool terrainBlendApplied;
        [SerializeField] private bool voxelBlendPrepared;
        [SerializeField] private bool debrisApplied;

        private ParticleSystem _gapDitherSystem;
        private ParticleSystemRenderer _gapDitherRenderer;
        private Transform _primitiveRoot;

        public long RuntimeKey => runtimeKey;
        public int BuildSignature => buildSignature;
        internal Transform PrimitiveRoot => _primitiveRoot;
        internal ParticleSystem GapDitherSystem => _gapDitherSystem;
        internal ParticleSystemRenderer GapDitherRenderer => _gapDitherRenderer;

        private void OnEnable()
        {
            if (_activeRuntimes.Contains(this))
                return;

            if (_activeRuntimes.Count < _activeRuntimes.Capacity)
                _activeRuntimes.Add(this);
        }

        private void OnDisable()
        {
            _activeRuntimes.Remove(this);
        }

        public static void CopyActiveRuntimesTo(List<WorldGenerativeGeologySeamRuntime> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            _staleRuntimeIndexBuffer.Clear();
            for (int i = 0; i < _activeRuntimes.Count; i++)
            {
                WorldGenerativeGeologySeamRuntime runtime = _activeRuntimes[i];
                if (runtime == null || !runtime.isActiveAndEnabled)
                {
                    if (_staleRuntimeIndexBuffer.Count < _staleRuntimeIndexBuffer.Capacity)
                        _staleRuntimeIndexBuffer.Add(i);
                    continue;
                }

                if (destination.Count < destination.Capacity)
                    destination.Add(runtime);
            }

            TrimStaleActiveRuntimes();
        }

        public static bool TryResolveActiveRuntime(long runtimeKey, Transform host, out WorldGenerativeGeologySeamRuntime runtime)
        {
            runtime = null;
            if (runtimeKey == 0L || host == null)
                return false;

            _staleRuntimeIndexBuffer.Clear();
            for (int i = 0; i < _activeRuntimes.Count; i++)
            {
                WorldGenerativeGeologySeamRuntime candidate = _activeRuntimes[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    if (_staleRuntimeIndexBuffer.Count < _staleRuntimeIndexBuffer.Capacity)
                        _staleRuntimeIndexBuffer.Add(i);
                    continue;
                }

                if (candidate.RuntimeKey == runtimeKey && candidate.transform.parent == host)
                {
                    runtime = candidate;
                    TrimStaleActiveRuntimes();
                    return true;
                }
            }

            TrimStaleActiveRuntimes();
            return false;
        }

        public void CacheGapDither(ParticleSystem system, ParticleSystemRenderer renderer)
        {
            _gapDitherSystem = system;
            _gapDitherRenderer = renderer;
        }

        public void CachePrimitiveRoot(Transform primitiveRoot)
        {
            _primitiveRoot = primitiveRoot;
        }

        public void ReleaseToPool(Transform poolRoot)
        {
            runtimeKey = 0L;
            buildSignature = 0;
            planWeight = 0f;
            terrainBlendApplied = false;
            voxelBlendPrepared = false;
            debrisApplied = false;

            if (_gapDitherSystem != null && _gapDitherSystem.gameObject.activeSelf)
                _gapDitherSystem.gameObject.SetActive(false);

            if (_primitiveRoot != null)
            {
                for (int i = 0; i < _primitiveRoot.childCount; i++)
                {
                    Transform child = _primitiveRoot.GetChild(i);
                    if (child != null && child.gameObject.activeSelf)
                        child.gameObject.SetActive(false);
                }
            }

            if (poolRoot != null)
                transform.SetParent(poolRoot, false);

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public void Configure(long configuredRuntimeKey, int configuredBuildSignature, in WorldGenerativeGeologySeamPlan plan)
        {
            runtimeKey = configuredRuntimeKey;
            buildSignature = configuredBuildSignature;
            planWeight = plan.planWeight;
            terrainBlendApplied = plan.RequiresTerrainBlend;
            voxelBlendPrepared = plan.RequiresVoxelBlend;
            debrisApplied = plan.RequiresDebrisSeam;
        }

        public void Claim(long configuredRuntimeKey)
        {
            runtimeKey = configuredRuntimeKey;
            buildSignature = int.MinValue;
            planWeight = 0f;
            terrainBlendApplied = false;
            voxelBlendPrepared = false;
            debrisApplied = false;
        }

        private static void TrimStaleActiveRuntimes()
        {
            for (int i = _staleRuntimeIndexBuffer.Count - 1; i >= 0; i--)
            {
                int index = _staleRuntimeIndexBuffer[i];
                if (index < 0 || index >= _activeRuntimes.Count)
                    continue;

                _activeRuntimes.RemoveAt(index);
            }

            _staleRuntimeIndexBuffer.Clear();
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4030)]
    public sealed class WorldGenerativeGeologySeamExecutionDirector : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const string SeamRootName = "__GEOLOGY_SEAM";
        private const string PoolRootName = "__GEOLOGY_SEAM_POOL";
        private const string PrimitiveRootName = "__SEAM_PRIMITIVES";
        private const string GapDitherName = "__SEAM_DITHER";
#if UNITY_EDITOR
        private const string EditorDefaultGapDitherMaterialPath = "Assets/_Project/Art/Materials/VFX/Mat_LeakPlume.mat";
#endif
        private const int RuntimeKeySelectionCapacity = 128;
        private const int HybridTerrainSeamBuildVersion = 2;
        private const int DebrisPrimitiveCapacity = 14;
        private static readonly string[] _VoxelCollarNames = CreateIndexedNames("VoxelCollar_", 10);
        private static readonly string[] _DebrisNames = CreateIndexedNames("Debris_", 14);

        [Header("References")]
        [SerializeField] private WorldGenerativeGeologyIntegrationDirector integrationDirector;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private SeamRegistry seamRegistry;

        [Header("Execution")]
        [SerializeField] private int maxExecutedPlans = 48;
        [SerializeField] private float minExecutionWeight = 0.18f;
        [SerializeField] private int voxelCollarSegments = 5;
        [SerializeField] private float debrisScale = 0.42f;
        [SerializeField] private float seamDitherVerticalOffset = 0.14f;
        [SerializeField] private int seamDitherMaxParticles = 36;
        [SerializeField] private float seamDitherSize = 0.16f;
        [SerializeField] private Material gapDitherMaterial;
        [SerializeField, Min(1)] private int prewarmedRuntimeCapacity = 24;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugReady;
        [SerializeField] private int _debugAppliedSeams;
        [SerializeField] private int _debugTerrainSeams;
        [SerializeField] private int _debugVoxelSeams;
        [SerializeField] private int _debugDebrisSeams;
        [SerializeField] private int _debugVoxelRequests;
        [SerializeField] private string _debugTopExecutedFamilyId = string.Empty;
        [SerializeField] private WorldGenerativeGeologyProfile.ShapeArchetype _debugTopExecutedArchetype;
        [SerializeField] private float _debugVisualQualityWeight = 1f;
        [SerializeField] private int _debugExecutedPlanBudget;
        [SerializeField] private int _debugCollarSegmentBudget;
        [SerializeField] private int _debugDebrisBudget;

        private readonly List<long> _desiredRuntimeKeys = new List<long>(128);
        private readonly List<long> _retainedRuntimeKeys = new List<long>(128);
        private readonly List<WorldGenerativeGeologyVoxelBlendRequest> _voxelRequests = new List<WorldGenerativeGeologyVoxelBlendRequest>(RuntimeKeySelectionCapacity);
        private readonly List<WorldGenerativeGeologySeamRuntime> _runtimePool = new List<WorldGenerativeGeologySeamRuntime>(64);
        private readonly List<WorldGenerativeGeologySeamRuntime> _runtimeCleanupBuffer = new List<WorldGenerativeGeologySeamRuntime>(128);
        private readonly List<long> _runtimeCacheTrimBuffer = new List<long>(128);
        private readonly Dictionary<long, WorldGenerativeGeologySeamRuntime> _runtimeCacheByKey = new Dictionary<long, WorldGenerativeGeologySeamRuntime>(128);
        private readonly HashSet<long> _selectedRuntimeKeys = new HashSet<long>(RuntimeKeySelectionCapacity);
        private Transform _poolRoot;
        private bool _registeredToTickManager;
        private bool _registeredToLateFrame;
        private bool _pendingReconcileVisualSync;
        private bool _loggedMissingGapDitherMaterial;

        internal static WorldGenerativeGeologySeamExecutionDirector ActiveRuntimeInstance { get; private set; }

        public IReadOnlyList<WorldGenerativeGeologyVoxelBlendRequest> ActiveVoxelRequests => _voxelRequests;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            RefreshColdReferences();
            EnsureRuntimePoolCold();
            ReconcileExecutedSeams();
        }

        private void OnEnable()
        {
            RefreshColdReferences();
            if (Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

            TryRegisterToTickManager();
        }

        private void Start()
        {
            RefreshColdReferences();
            EnsureRuntimePoolCold();
            TryRegisterToTickManager();
            ReconcileExecutedSeams();
        }

        private void OnDisable()
        {
            TryUnregisterFromTickManager();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
        }

        private void OnDestroy()
        {
            TryUnregisterFromTickManager();
            GlobalRegistry.TryUnregisterHotSwapListener(this);

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void SlowTick()
        {
            _pendingReconcileVisualSync = true;
        }

        public void LateFrameTick()
        {
            if (!_pendingReconcileVisualSync)
                return;

            _pendingReconcileVisualSync = false;
            ReconcileExecutedSeams();
        }

        public void SetIntegrationDirector(WorldGenerativeGeologyIntegrationDirector director)
        {
            integrationDirector = director;
        }

        public void SetPlayerTransform(Transform target)
        {
            playerTransform = target;
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager && _registeredToLateFrame)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredToTickManager)
            {
                _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredToLateFrame)
                _registeredToLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterFromTickManager()
        {
            if (!_registeredToTickManager && !_registeredToLateFrame)
                return;

            if (_registeredToTickManager)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (_registeredToLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
            _registeredToLateFrame = false;
            _pendingReconcileVisualSync = false;
        }

        private void TryUnregisterDispatcherTicks()
        {
            if (!_registeredToTickManager && !_registeredToLateFrame)
                return;

            if (_registeredToTickManager)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (_registeredToLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
            _registeredToLateFrame = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterDispatcherTicks();
            if (isActiveAndEnabled)
            {
                if (currentService != null)
                    TryRegisterToTickManager();
            }
        }

        public void CopyVoxelRequestsTo(List<WorldGenerativeGeologyVoxelBlendRequest> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            int copyCount = Mathf.Min(_voxelRequests.Count, destination.Capacity);
            for (int i = 0; i < copyCount; i++)
                destination.Add(_voxelRequests[i]);
        }

        public void ReconcileExecutedSeams()
        {
            CaptureRetainedRuntimeKeys();
            _desiredRuntimeKeys.Clear();
            _voxelRequests.Clear();
            _selectedRuntimeKeys.Clear();
            _debugAppliedSeams = 0;
            _debugTerrainSeams = 0;
            _debugVoxelSeams = 0;
            _debugDebrisSeams = 0;
            _debugVoxelRequests = 0;
            _debugTopExecutedFamilyId = string.Empty;
            _debugTopExecutedArchetype = default;
            _debugReady = false;
            _debugDebrisBudget = 0;

            if (integrationDirector == null)
            {
                CleanupStaleSeams();
                return;
            }

            IReadOnlyList<WorldGenerativeGeologySeamPlan> plans = integrationDirector.ActivePlans;
            float visualQualityWeight = ResolveGlobalQualityWeight();
            int executedPlanBudget = ResolveExecutedPlanBudget(visualQualityWeight);
            _debugVisualQualityWeight = visualQualityWeight;
            _debugExecutedPlanBudget = executedPlanBudget;
            _debugCollarSegmentBudget = ResolveVoxelCollarSegments(visualQualityWeight);
            int appliedCount = 0;
            for (int i = 0; i < _retainedRuntimeKeys.Count; i++)
            {
                if (appliedCount >= executedPlanBudget)
                    break;

                if (!TryApplyRuntimeKey(_retainedRuntimeKeys[i], visualQualityWeight, ref appliedCount))
                    continue;
            }

            for (int i = 0; i < plans.Count; i++)
            {
                if (appliedCount >= executedPlanBudget)
                    break;

                WorldGenerativeGeologySeamPlan plan = plans[i];
                if (_selectedRuntimeKeys.Contains(plan.runtimeKey))
                    continue;

                if (plan.planWeight < minExecutionWeight)
                    continue;

                if (!integrationDirector.TryGetBinding(plan.runtimeKey, out WorldGenerativeGeologyBinding binding) || binding == null)
                    continue;

                if (!TryAddDesiredRuntimeKey(plan.runtimeKey))
                    continue;
                ApplySeam(binding, plan, visualQualityWeight);
                TryRegisterVoxelRequest(plan);
                appliedCount++;

                if (string.IsNullOrEmpty(_debugTopExecutedFamilyId))
                {
                    _debugTopExecutedFamilyId = plan.familyId ?? string.Empty;
                    _debugTopExecutedArchetype = plan.archetype;
                }
            }

            CleanupStaleSeams();

            _debugAppliedSeams = appliedCount;
            _debugVoxelRequests = _voxelRequests.Count;
            _debugReady = appliedCount > 0;
        }

        private void CaptureRetainedRuntimeKeys()
        {
            _retainedRuntimeKeys.Clear();
            for (int i = 0; i < _desiredRuntimeKeys.Count && _retainedRuntimeKeys.Count < _retainedRuntimeKeys.Capacity; i++)
                _retainedRuntimeKeys.Add(_desiredRuntimeKeys[i]);
        }

        private bool TryApplyRuntimeKey(long runtimeKey, float visualQualityWeight, ref int appliedCount)
        {
            if (runtimeKey == 0L || integrationDirector == null)
                return false;

            if (!integrationDirector.TryGetPlan(runtimeKey, out WorldGenerativeGeologySeamPlan plan))
                return false;

            if (plan.planWeight < minExecutionWeight)
                return false;

            if (!integrationDirector.TryGetBinding(plan.runtimeKey, out WorldGenerativeGeologyBinding binding) || binding == null)
                return false;

            if (!TryAddDesiredRuntimeKey(plan.runtimeKey))
                return false;
            if (_selectedRuntimeKeys.Count < RuntimeKeySelectionCapacity)
                _selectedRuntimeKeys.Add(plan.runtimeKey);
            ApplySeam(binding, plan, visualQualityWeight);
            TryRegisterVoxelRequest(plan);
            appliedCount++;

            if (string.IsNullOrEmpty(_debugTopExecutedFamilyId))
            {
                _debugTopExecutedFamilyId = plan.familyId ?? string.Empty;
                _debugTopExecutedArchetype = plan.archetype;
            }

            return true;
        }

        private void ApplySeam(WorldGenerativeGeologyBinding binding, in WorldGenerativeGeologySeamPlan plan, float visualQualityWeight)
        {
            if (binding == null || binding.transform == null)
                return;

            WorldGenerativeGeologySeamRuntime runtime = GetOrCreateSeamRuntime(binding.transform, plan.runtimeKey);
            if (runtime == null)
                return;

            Transform seamRoot = runtime.transform;
            int buildSignature = ComputeBuildSignature(plan, visualQualityWeight);
            if (runtime.BuildSignature == buildSignature)
            {
                ConfigureGapDitherVfx(runtime, plan, visualQualityWeight);
                SeamRegistry registry = seamRegistry;
                if (registry != null)
                    registry.Upsert(plan);
                CountPlan(plan);
                return;
            }

            Material seamMaterial = ResolveSeamMaterial(binding);
            Transform primitiveRoot = runtime.PrimitiveRoot;
            if (primitiveRoot == null)
                return;

            int primitiveIndex = 0;

            // Terrain contact is now owned by the SDF-to-heightmap projection and global shader mask.

            if (plan.RequiresVoxelBlend)
                BuildVoxelCollar(primitiveRoot, seamMaterial, plan, visualQualityWeight, ref primitiveIndex);

            if (plan.RequiresDebrisSeam)
                BuildDebrisBand(primitiveRoot, seamMaterial, plan, visualQualityWeight, ref primitiveIndex);

            ConfigureGapDitherVfx(runtime, plan, visualQualityWeight);
            DisableUnusedChildren(primitiveRoot, primitiveIndex);
            runtime.Configure(plan.runtimeKey, buildSignature, plan);
            SeamRegistry activeRegistry = seamRegistry;
            if (activeRegistry != null)
                activeRegistry.Upsert(plan);
            CountPlan(plan);
        }

        private bool TryAddDesiredRuntimeKey(long runtimeKey)
        {
            if (runtimeKey == 0L || _desiredRuntimeKeys.Count >= _desiredRuntimeKeys.Capacity)
                return false;

            _desiredRuntimeKeys.Add(runtimeKey);
            return true;
        }

        private bool TryRegisterVoxelRequest(in WorldGenerativeGeologySeamPlan plan)
        {
            if (!plan.RequiresVoxelBlend)
                return true;

            if (_voxelRequests.Count >= _voxelRequests.Capacity)
                return false;

            AbsoluteUniversePosition voxelCenterAup = plan.absoluteVoxelVolumeCenterAup;
            bool hasVoxelCenterAup = plan.hasAbsoluteVoxelVolumeCenterAup && voxelCenterAup.IsFinite();
            if (!hasVoxelCenterAup)
                hasVoxelCenterAup = TryResolveAupFromRuntimeOrigin(plan.RuntimeVoxelVolumeCenter, out voxelCenterAup);

            AbsoluteUniversePosition terrainContactAup = plan.absoluteTerrainContactAup;
            bool hasTerrainContactAup = plan.hasAbsoluteTerrainContactAup && terrainContactAup.IsFinite();
            if (!hasTerrainContactAup)
                hasTerrainContactAup = TryResolveAupFromRuntimeOrigin(plan.TerrainContactPosition, out terrainContactAup);

            if (!hasVoxelCenterAup || !hasTerrainContactAup)
                return false;

            _voxelRequests.Add(new WorldGenerativeGeologyVoxelBlendRequest
            {
                runtimeKey = plan.runtimeKey,
                familyId = plan.familyId,
                geologyProfileId = plan.geologyProfileId,
                archetype = plan.archetype,
                absoluteUniverseCenter = plan.absoluteVoxelVolumeCenter,
                size = plan.voxelVolumeSize,
                rotation = plan.worldRotation,
                weight = plan.caveBlendWeight,
                playerDistance = plan.playerDistance,
                planWeight = plan.planWeight,
                hasTerrainSample = plan.hasTerrainSample,
                absoluteTerrainContactPosition = new Vector3(plan.absoluteUniversePosition.x, plan.absoluteTerrainHeight, plan.absoluteUniversePosition.z),
                absoluteUniverseCenterAup = voxelCenterAup,
                absoluteTerrainContactAup = terrainContactAup,
                hasAbsoluteUniverseCenterAup = true,
                hasAbsoluteTerrainContactAup = true,
                slopeDegrees = plan.slopeDegrees,
                seamBlendRadius = plan.seamBlendRadius,
                suggestedTerrainCut = plan.suggestedTerrainCut,
                caveBlendMode = plan.caveBlendMode,
                chunkCoord = plan.ChunkCoord,
                hasMacroZone = plan.hasMacroZone,
                macroZoneCoord = plan.MacroZoneCoord
            });
            return true;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return absoluteAup.IsFinite();
        }

        private void ConfigureGapDitherVfx(WorldGenerativeGeologySeamRuntime runtime, in WorldGenerativeGeologySeamPlan plan, float visualQualityWeight)
        {
            Transform root = runtime != null ? runtime.transform : null;
            if (root == null)
                return;

            ParticleSystem system = runtime.GapDitherSystem;
            bool shouldRender = plan.RequiresTerrainBlend || plan.RequiresVoxelBlend;
            if (!shouldRender)
            {
                if (system != null && system.gameObject.activeSelf)
                    system.gameObject.SetActive(false);

                return;
            }

            if (system == null)
                return;

            Transform systemTransform = system.transform;
            Vector3 contact = root.InverseTransformPoint(plan.TerrainContactPosition);
            systemTransform.localPosition = new Vector3(contact.x, contact.y + seamDitherVerticalOffset, contact.z);
            systemTransform.localRotation = Quaternion.identity;
            systemTransform.localScale = Vector3.one;

            GameObject vfxRoot = system.gameObject;
            if (!vfxRoot.activeSelf)
                vfxRoot.SetActive(true);

            system.useAutoRandomSeed = false;
            system.randomSeed = unchecked((uint)plan.runtimeKey);

            int maxParticles = ResolveDitherParticleBudget(plan, visualQualityWeight);
            float ditherSize = seamDitherSize * ResolveDitherSizeMultiplier(visualQualityWeight);
            var main = system.main;
            main.maxParticles = maxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                Mathf.Lerp(0.05f, 0.08f, visualQualityWeight),
                Mathf.Lerp(0.16f, 0.24f, visualQualityWeight));
            main.startSize = new ParticleSystem.MinMaxCurve(ditherSize * 0.72f, ditherSize * 1.35f);
            main.startColor = new Color(0.32f, 0.92f, 1f, 0.9f);

            var emission = system.emission;
            emission.rateOverTime = ResolveDitherEmissionRate(plan, visualQualityWeight);

            var shape = system.shape;
            shape.radius = Mathf.Max(0.9f, plan.seamBlendRadius * Mathf.Lerp(0.64f, 0.92f, visualQualityWeight));

            if (!system.isPlaying)
                system.Play(true);
        }

        private ParticleSystem CreateGapDitherSystem(WorldGenerativeGeologySeamRuntime runtime, Transform root)
        {
            GameObject vfxRoot = new GameObject(GapDitherName);
            vfxRoot.transform.SetParent(root, false);

            ParticleSystemRenderer renderer = vfxRoot.AddComponent<ParticleSystemRenderer>();
            ParticleSystem system = vfxRoot.AddComponent<ParticleSystem>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = ResolveGapDitherMaterial();
                renderer.sortMode = ParticleSystemSortMode.Distance;
            }

            runtime.CacheGapDither(system, renderer);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = seamDitherMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startSize = new ParticleSystem.MinMaxCurve(seamDitherSize * 0.72f, seamDitherSize * 1.35f);
            main.startColor = new Color(0.32f, 0.92f, 1f, 0.9f);

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 12f;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.8f;
            shape.radiusThickness = 0.12f;

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.2f;
            noise.scrollSpeed = 0.08f;

            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = new ParticleSystem.MinMaxCurve(-0.04f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);

            return system;
        }

        private void EnsureRuntimePoolCold()
        {
            WorldGeneratedPrimitiveFactory.PrewarmPrimitiveResources();
            if (_poolRoot == null)
            {
                GameObject poolObject = new GameObject(PoolRootName);
                _poolRoot = poolObject.transform;
                _poolRoot.SetParent(transform, false);
            }

            int targetCapacity = Mathf.Max(1, prewarmedRuntimeCapacity);
            int qualityCapacity = ResolveExecutedPlanBudget(ResolveGlobalQualityWeight());
            targetCapacity = Mathf.Max(targetCapacity, qualityCapacity);
            int primitiveCapacity = ResolvePooledPrimitiveCapacity();
            while (_runtimePool.Count < targetCapacity)
                CreatePooledRuntimeCold(_runtimePool.Count, primitiveCapacity);
        }

        private int ResolvePooledPrimitiveCapacity()
        {
            int collarCapacity = Mathf.Clamp(voxelCollarSegments, 3, _VoxelCollarNames.Length);
            return collarCapacity + DebrisPrimitiveCapacity;
        }

        private void CreatePooledRuntimeCold(int poolIndex, int primitiveCapacity)
        {
            GameObject runtimeObject = new GameObject(SeamRootName);
            runtimeObject.SetActive(false);
            Transform runtimeTransform = runtimeObject.transform;
            runtimeTransform.SetParent(_poolRoot, false);

            WorldGenerativeGeologySeamRuntime runtime = runtimeObject.AddComponent<WorldGenerativeGeologySeamRuntime>();

            GameObject primitiveRootObject = new GameObject(PrimitiveRootName);
            Transform primitiveRoot = primitiveRootObject.transform;
            primitiveRoot.SetParent(runtimeTransform, false);
            runtime.CachePrimitiveRoot(primitiveRoot);

            for (int i = 0; i < primitiveCapacity; i++)
                WorldGeneratedPrimitiveFactory.CreateCachedPrimitiveShell(primitiveRoot, "SeamPrimitive_" + i);

            ParticleSystem ditherSystem = CreateGapDitherSystem(runtime, runtimeTransform);
            if (ditherSystem != null && ditherSystem.gameObject.activeSelf)
                ditherSystem.gameObject.SetActive(false);

            runtimeObject.name = SeamRootName + "_" + poolIndex;
            _runtimePool.Add(runtime);
        }

        private Material ResolveGapDitherMaterial()
        {
            if (gapDitherMaterial != null)
                return gapDitherMaterial;

#if UNITY_EDITOR
            gapDitherMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(EditorDefaultGapDitherMaterialPath);
            if (gapDitherMaterial != null)
                return gapDitherMaterial;
#endif

            if (!_loggedMissingGapDitherMaterial)
            {
                _loggedMissingGapDitherMaterial = true;
                Hecton8.Core.H8Debug.LogError("[WorldGenerativeGeologySeamExecutionDirector] Missing gapDitherMaterial asset. Runtime material creation is forbidden for seam dither particles.", this);
            }

            return null;
        }

        private void BuildVoxelCollar(Transform root, Material seamMaterial, in WorldGenerativeGeologySeamPlan plan, float visualQualityWeight, ref int primitiveIndex)
        {
            int segments = ResolveVoxelCollarSegments(visualQualityWeight);
            float radius = Mathf.Max(1.4f, plan.seamBlendRadius * 0.48f);
            float height = Mathf.Max(1f, plan.voxelVolumeSize.y * 0.2f);
            Vector3 center = root.InverseTransformPoint(plan.RuntimeVoxelVolumeCenter);

            for (int i = 0; i < segments; i++)
            {
                float angle = (360f / segments) * i + 18f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                Vector3 localPosition = new Vector3(center.x + offset.x, center.y - height * 0.15f, center.z + offset.z);
                Vector3 localScale = new Vector3(Mathf.Max(0.42f, radius * 0.18f), height, Mathf.Max(0.42f, radius * 0.18f));
                CreatePrimitive(root, seamMaterial, PrimitiveType.Cylinder, _VoxelCollarNames[i], localPosition, Quaternion.identity, localScale, ref primitiveIndex);
            }
        }

        private void BuildDebrisBand(Transform root, Material seamMaterial, in WorldGenerativeGeologySeamPlan plan, float visualQualityWeight, ref int primitiveIndex)
        {
            int debrisCount = ResolveDebrisCount(plan, visualQualityWeight);
            _debugDebrisBudget = Mathf.Max(_debugDebrisBudget, debrisCount);
            float radius = Mathf.Max(0.8f, plan.seamBlendRadius * 0.68f);
            float scaleMultiplier = ResolveDebrisScaleMultiplier(visualQualityWeight);
            Vector3 contact = root.InverseTransformPoint(plan.TerrainContactPosition);

            for (int i = 0; i < debrisCount; i++)
            {
                float t = debrisCount <= 1 ? 0f : i / (float)(debrisCount - 1);
                float angle = t * 360f + (plan.runtimeKey % 47);
                float jitter = Mathf.Lerp(-radius * 0.18f, radius * 0.18f, Hash01(plan.runtimeKey, i, 11));
                float localRadius = radius + jitter;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * localRadius;
                float scale = Mathf.Lerp(0.22f, 0.65f, Hash01(plan.runtimeKey, i, 29)) * debrisScale * scaleMultiplier;
                Quaternion rotation = Quaternion.Euler(
                    Hash01(plan.runtimeKey, i, 37) * 18f,
                    angle + Hash01(plan.runtimeKey, i, 43) * 45f,
                    Hash01(plan.runtimeKey, i, 59) * 22f);
                CreatePrimitive(
                    root,
                    seamMaterial,
                    i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Capsule,
                    _DebrisNames[i],
                    new Vector3(contact.x + offset.x, contact.y + scale * 0.3f, contact.z + offset.z),
                    rotation,
                    Vector3.one * scale,
                    ref primitiveIndex);
            }
        }

        private static float Hash01(long runtimeKey, int index, int salt)
        {
            int seed = unchecked((int)(runtimeKey * 486187739L + index * 92821L + salt * 15485863L));
            uint value = (uint)seed;
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private WorldGenerativeGeologySeamRuntime GetOrCreateSeamRuntime(Transform host, long runtimeKey)
        {
            if (host == null)
                return null;

            if (_runtimeCacheByKey.TryGetValue(runtimeKey, out WorldGenerativeGeologySeamRuntime cachedRuntime) &&
                cachedRuntime != null &&
                cachedRuntime.transform.parent == host &&
                (cachedRuntime.RuntimeKey == 0L || cachedRuntime.RuntimeKey == runtimeKey))
            {
                ActivateTransform(cachedRuntime.transform);
                return cachedRuntime;
            }

            if (WorldGenerativeGeologySeamRuntime.TryResolveActiveRuntime(runtimeKey, host, out WorldGenerativeGeologySeamRuntime activeRuntime))
            {
                _runtimeCacheByKey[runtimeKey] = activeRuntime;
                ActivateTransform(activeRuntime.transform);
                return activeRuntime;
            }

            if (!TryClaimPooledRuntime(host, runtimeKey, out WorldGenerativeGeologySeamRuntime runtime))
                return null;

            _runtimeCacheByKey[runtimeKey] = runtime;
            return runtime;
        }

        private bool TryClaimPooledRuntime(Transform host, long runtimeKey, out WorldGenerativeGeologySeamRuntime runtime)
        {
            runtime = null;
            for (int i = 0; i < _runtimePool.Count; i++)
            {
                WorldGenerativeGeologySeamRuntime candidate = _runtimePool[i];
                if (candidate == null || candidate.gameObject.activeSelf)
                    continue;

                Transform candidateTransform = candidate.transform;
                candidateTransform.SetParent(host, false);
                candidateTransform.localPosition = Vector3.zero;
                candidateTransform.localRotation = Quaternion.identity;
                candidateTransform.localScale = Vector3.one;
                candidate.gameObject.SetActive(true);
                candidate.Claim(runtimeKey);
                runtime = candidate;
                return true;
            }

            return false;
        }

        private static void CreatePrimitive(
            Transform root,
            Material seamMaterial,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            ref int primitiveIndex)
        {
            if (primitiveIndex < root.childCount)
            {
                GameObject existing = root.GetChild(primitiveIndex).gameObject;
                ActivateTransform(existing.transform);
                WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisualHot(
                    existing,
                    primitiveType,
                    name,
                    localPosition,
                    localRotation,
                    localScale,
                    seamMaterial);
            }

            primitiveIndex++;
        }

        private static Material ResolveSeamMaterial(WorldGenerativeGeologyBinding binding)
        {
            return binding != null ? binding.CachedSeamMaterial : null;
        }

        private int ComputeBuildSignature(in WorldGenerativeGeologySeamPlan plan, float visualQualityWeight)
        {
            unchecked
            {
                int hash = HybridTerrainSeamBuildVersion;
                hash = (hash * 397) ^ (int)plan.runtimeKey;
                hash = (hash * 397) ^ (int)plan.terrainSeamMode;
                hash = (hash * 397) ^ (int)plan.caveBlendMode;
                hash = (hash * 397) ^ Mathf.RoundToInt(plan.seamBlendRadius * 100f);
                hash = (hash * 397) ^ Mathf.RoundToInt(plan.suggestedTerrainRaise * 100f);
                hash = (hash * 397) ^ Mathf.RoundToInt(plan.suggestedTerrainCut * 100f);
                hash = (hash * 397) ^ plan.suggestedDebrisCount;
                hash = (hash * 397) ^ ResolveVoxelCollarSegments(visualQualityWeight);
                hash = (hash * 397) ^ ResolveDebrisCount(plan, visualQualityWeight);
                return hash;
            }
        }

        private int ResolveExecutedPlanBudget(float visualQualityWeight)
        {
            int configuredBudget = Mathf.Clamp(maxExecutedPlans, 1, RuntimeKeySelectionCapacity);
            int survivalBudget = Mathf.Clamp(Mathf.CeilToInt(configuredBudget * 0.25f), 1, configuredBudget);
            float curvedWeight = SmoothQualityWeight(visualQualityWeight);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(survivalBudget, configuredBudget, curvedWeight)), 1, configuredBudget);
        }

        private int ResolveVoxelCollarSegments(float visualQualityWeight)
        {
            int configuredSegments = Mathf.Clamp(voxelCollarSegments, 3, 10);
            float curvedWeight = SmoothQualityWeight(visualQualityWeight);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(3f, configuredSegments, curvedWeight)), 3, configuredSegments);
        }

        private int ResolveDebrisCount(in WorldGenerativeGeologySeamPlan plan, float visualQualityWeight)
        {
            int requestedCount = Mathf.Clamp(plan.suggestedDebrisCount, 1, 14);
            float curvedWeight = SmoothQualityWeight(visualQualityWeight);
            float weightedMax = Mathf.Lerp(1f, requestedCount, curvedWeight);
            float contextualWeight = Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(plan.planWeight));
            return Mathf.Clamp(Mathf.RoundToInt(weightedMax * contextualWeight), 1, requestedCount);
        }

        private int ResolveDitherParticleBudget(in WorldGenerativeGeologySeamPlan plan, float visualQualityWeight)
        {
            int configuredMax = Mathf.Max(12, seamDitherMaxParticles);
            float curvedWeight = SmoothQualityWeight(visualQualityWeight);
            float radiusBudget = plan.seamBlendRadius * Mathf.Lerp(3.5f, 7.5f, curvedWeight);
            return Mathf.Clamp(Mathf.RoundToInt(radiusBudget), 12, configuredMax);
        }

        private float ResolveDitherEmissionRate(in WorldGenerativeGeologySeamPlan plan, float visualQualityWeight)
        {
            float curvedWeight = SmoothQualityWeight(visualQualityWeight);
            float rateScale = Mathf.Lerp(3.5f, 6.5f, curvedWeight);
            return Mathf.Clamp(plan.seamBlendRadius * rateScale, 6f, Mathf.Lerp(16f, 32f, curvedWeight));
        }

        private static float ResolveDebrisScaleMultiplier(float visualQualityWeight)
        {
            return Mathf.Lerp(0.82f, 1.12f, SmoothQualityWeight(visualQualityWeight));
        }

        private static float ResolveDitherSizeMultiplier(float visualQualityWeight)
        {
            return Mathf.Lerp(0.82f, 1.18f, SmoothQualityWeight(visualQualityWeight));
        }

        private static float SmoothQualityWeight(float visualQualityWeight)
        {
            float weight = Mathf.Clamp01(visualQualityWeight);
            return weight * weight * (3f - 2f * weight);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(weight) ? math.saturate(weight) : 1f;
        }

        private void CountPlan(in WorldGenerativeGeologySeamPlan plan)
        {
            if (plan.RequiresTerrainBlend)
                _debugTerrainSeams++;
            if (plan.RequiresVoxelBlend)
                _debugVoxelSeams++;
            if (plan.RequiresDebrisSeam)
                _debugDebrisSeams++;
        }

        private void CleanupStaleSeams()
        {
            WorldGenerativeGeologySeamRuntime.CopyActiveRuntimesTo(_runtimeCleanupBuffer);
            for (int i = 0; i < _runtimeCleanupBuffer.Count; i++)
            {
                WorldGenerativeGeologySeamRuntime runtime = _runtimeCleanupBuffer[i];
                if (runtime == null)
                    continue;

                if (_desiredRuntimeKeys.Contains(runtime.RuntimeKey))
                    continue;

                SeamRegistry registry = seamRegistry;
                if (registry != null)
                    registry.Remove(runtime.RuntimeKey);
                runtime.ReleaseToPool(_poolRoot);
            }

            _runtimeCacheTrimBuffer.Clear();
            Dictionary<long, WorldGenerativeGeologySeamRuntime>.Enumerator runtimeCacheEnumerator = _runtimeCacheByKey.GetEnumerator();
            while (runtimeCacheEnumerator.MoveNext())
            {
                long runtimeKey = runtimeCacheEnumerator.Current.Key;
                WorldGenerativeGeologySeamRuntime runtime = runtimeCacheEnumerator.Current.Value;
                if (runtime != null &&
                    (runtime.RuntimeKey == 0L || runtime.RuntimeKey == runtimeKey))
                    continue;

                if (_runtimeCacheTrimBuffer.Count < _runtimeCacheTrimBuffer.Capacity)
                    _runtimeCacheTrimBuffer.Add(runtimeKey);
            }

            for (int i = 0; i < _runtimeCacheTrimBuffer.Count; i++)
                _runtimeCacheByKey.Remove(_runtimeCacheTrimBuffer[i]);
        }

        private static void DisableUnusedChildren(Transform root, int activeChildCount)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                    continue;

                if (child.name == GapDitherName)
                    continue;

                bool keepActive = i < activeChildCount;
                if (child.gameObject.activeSelf != keepActive)
                    child.gameObject.SetActive(keepActive);
            }
        }

        private static void ActivateTransform(Transform target)
        {
            if (target != null && !target.gameObject.activeSelf)
                target.gameObject.SetActive(true);
        }

        private static string[] CreateIndexedNames(string prefix, int count)
        {
            string[] result = new string[count];
            for (int i = 0; i < count; i++)
                result[i] = prefix + i;

            return result;
        }

        private void RefreshColdReferences()
        {
            if (integrationDirector != null && playerTransform != null)
            {
                if (seamRegistry == null)
                    seamRegistry = SeamRegistry.ActiveRuntimeInstance;
                return;
            }

            WorldRuntimeReferenceUtility.TryResolveWorldGenerativeGeologyIntegrationDirector(ref integrationDirector);
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
            if (seamRegistry == null)
                seamRegistry = SeamRegistry.ActiveRuntimeInstance;
        }
    }
}
