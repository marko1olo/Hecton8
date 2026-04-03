using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldGenerativeGeologySeamRuntime : MonoBehaviour
    {
        private static readonly List<WorldGenerativeGeologySeamRuntime> _activeRuntimes = new List<WorldGenerativeGeologySeamRuntime>(128);

        [SerializeField] private long runtimeKey;
        [SerializeField] private int buildSignature;
        [SerializeField] private float planWeight;
        [SerializeField] private bool terrainBlendApplied;
        [SerializeField] private bool voxelBlendPrepared;
        [SerializeField] private bool debrisApplied;

        public long RuntimeKey => runtimeKey;
        public int BuildSignature => buildSignature;

        private void OnEnable()
        {
            if (_activeRuntimes.Contains(this))
                return;

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
            for (int i = 0; i < _activeRuntimes.Count; i++)
            {
                WorldGenerativeGeologySeamRuntime runtime = _activeRuntimes[i];
                if (runtime == null)
                    continue;

                destination.Add(runtime);
            }
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
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4030)]
    public sealed class WorldGenerativeGeologySeamExecutionDirector : MonoBehaviour, ISlowTickable
    {
        private const string SeamRootName = "__GEOLOGY_SEAM";

        [Header("References")]
        [SerializeField] private WorldGenerativeGeologyIntegrationDirector integrationDirector;
        [SerializeField] private Transform playerTransform;

        [Header("Execution")]
        [SerializeField] private int maxExecutedPlans = 48;
        [SerializeField, Min(0f)] private float autoResolveRetryInterval = 1f;
        [SerializeField] private float minExecutionWeight = 0.18f;
        [SerializeField] private int terrainSkirtSegments = 6;
        [SerializeField] private int voxelCollarSegments = 5;
        [SerializeField] private float verticalBlendScale = 0.42f;
        [SerializeField] private float debrisScale = 0.42f;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugReady;
        [SerializeField] private int _debugAppliedSeams;
        [SerializeField] private int _debugTerrainSeams;
        [SerializeField] private int _debugVoxelSeams;
        [SerializeField] private int _debugDebrisSeams;
        [SerializeField] private int _debugVoxelRequests;
        [SerializeField] private string _debugTopExecuted = "None";

        private readonly List<long> _desiredRuntimeKeys = new List<long>(128);
        private readonly List<long> _retainedRuntimeKeys = new List<long>(128);
        private readonly List<WorldGenerativeGeologyVoxelBlendRequest> _voxelRequests = new List<WorldGenerativeGeologyVoxelBlendRequest>(64);
        private readonly List<WorldGenerativeGeologySeamRuntime> _runtimeCleanupBuffer = new List<WorldGenerativeGeologySeamRuntime>(128);
        private readonly List<Transform> _rendererTraversalBuffer = new List<Transform>(64);
        private readonly HashSet<long> _selectedRuntimeKeys = new HashSet<long>();
        private bool _registeredToTickManager;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;

        public IReadOnlyList<WorldGenerativeGeologyVoxelBlendRequest> ActiveVoxelRequests => _voxelRequests;

        private void Awake()
        {
            ResolveReferences();
            ReconcileExecutedSeams();
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

            ReconcileExecutedSeams();
        }

        private void OnDisable()
        {
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        public void SlowTick()
        {
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

        public void CopyVoxelRequestsTo(List<WorldGenerativeGeologyVoxelBlendRequest> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            destination.AddRange(_voxelRequests);
        }

        public void ReconcileExecutedSeams()
        {
            ResolveReferences();

            CaptureRetainedRuntimeKeys();
            _desiredRuntimeKeys.Clear();
            _voxelRequests.Clear();
            _selectedRuntimeKeys.Clear();
            _debugAppliedSeams = 0;
            _debugTerrainSeams = 0;
            _debugVoxelSeams = 0;
            _debugDebrisSeams = 0;
            _debugVoxelRequests = 0;
            _debugTopExecuted = "None";
            _debugReady = false;

            if (integrationDirector == null)
            {
                CleanupStaleSeams();
                return;
            }

            IReadOnlyList<WorldGenerativeGeologySeamPlan> plans = integrationDirector.ActivePlans;
            int appliedCount = 0;
            for (int i = 0; i < _retainedRuntimeKeys.Count; i++)
            {
                if (appliedCount >= Mathf.Max(1, maxExecutedPlans))
                    break;

                if (!TryApplyRuntimeKey(_retainedRuntimeKeys[i], ref appliedCount))
                    continue;
            }

            for (int i = 0; i < plans.Count; i++)
            {
                if (appliedCount >= Mathf.Max(1, maxExecutedPlans))
                    break;

                WorldGenerativeGeologySeamPlan plan = plans[i];
                if (_selectedRuntimeKeys.Contains(plan.runtimeKey))
                    continue;

                if (plan.planWeight < minExecutionWeight)
                    continue;

                if (!integrationDirector.TryGetBinding(plan.runtimeKey, out WorldGenerativeGeologyBinding binding) || binding == null)
                    continue;

                _desiredRuntimeKeys.Add(plan.runtimeKey);
                ApplySeam(binding, plan);
                RegisterVoxelRequest(plan);
                appliedCount++;

                if (_debugTopExecuted == "None")
                    _debugTopExecuted = $"{plan.familyId} [{plan.archetype}]";
            }

            CleanupStaleSeams();

            _debugAppliedSeams = appliedCount;
            _debugVoxelRequests = _voxelRequests.Count;
            _debugReady = appliedCount > 0;
        }

        private void CaptureRetainedRuntimeKeys()
        {
            _retainedRuntimeKeys.Clear();
            _retainedRuntimeKeys.AddRange(_desiredRuntimeKeys);
        }

        private bool TryApplyRuntimeKey(long runtimeKey, ref int appliedCount)
        {
            if (runtimeKey == 0L || integrationDirector == null)
                return false;

            if (!integrationDirector.TryGetPlan(runtimeKey, out WorldGenerativeGeologySeamPlan plan))
                return false;

            if (plan.planWeight < minExecutionWeight)
                return false;

            if (!integrationDirector.TryGetBinding(plan.runtimeKey, out WorldGenerativeGeologyBinding binding) || binding == null)
                return false;

            _desiredRuntimeKeys.Add(plan.runtimeKey);
            _selectedRuntimeKeys.Add(plan.runtimeKey);
            ApplySeam(binding, plan);
            RegisterVoxelRequest(plan);
            appliedCount++;

            if (_debugTopExecuted == "None")
                _debugTopExecuted = $"{plan.familyId} [{plan.archetype}]";

            return true;
        }

        private void ApplySeam(WorldGenerativeGeologyBinding binding, in WorldGenerativeGeologySeamPlan plan)
        {
            Transform seamRoot = GetOrCreateSeamRoot(binding.transform);
            int buildSignature = ComputeBuildSignature(plan);
            WorldGenerativeGeologySeamRuntime runtime = seamRoot.GetComponent<WorldGenerativeGeologySeamRuntime>();
            if (runtime != null && runtime.BuildSignature == buildSignature)
            {
                CountPlan(plan);
                return;
            }

            Material seamMaterial = ResolveSeamMaterial(binding.transform);
            int primitiveIndex = 0;

            if (plan.RequiresTerrainBlend)
                BuildTerrainSkirt(seamRoot, seamMaterial, plan, ref primitiveIndex);

            if (plan.RequiresVoxelBlend)
                BuildVoxelCollar(seamRoot, seamMaterial, plan, ref primitiveIndex);

            if (plan.RequiresDebrisSeam)
                BuildDebrisBand(seamRoot, seamMaterial, plan, ref primitiveIndex);

            DisableUnusedChildren(seamRoot, primitiveIndex);

            if (runtime == null)
                runtime = seamRoot.gameObject.AddComponent<WorldGenerativeGeologySeamRuntime>();

            runtime.Configure(plan.runtimeKey, buildSignature, plan);
            CountPlan(plan);
        }

        private void RegisterVoxelRequest(in WorldGenerativeGeologySeamPlan plan)
        {
            if (!plan.RequiresVoxelBlend)
                return;

            _voxelRequests.Add(new WorldGenerativeGeologyVoxelBlendRequest
            {
                runtimeKey = plan.runtimeKey,
                familyId = plan.familyId,
                geologyProfileId = plan.geologyProfileId,
                archetype = plan.archetype,
                center = plan.voxelVolumeCenter,
                size = plan.voxelVolumeSize,
                rotation = plan.worldRotation,
                weight = plan.caveBlendWeight,
                playerDistance = plan.playerDistance,
                planWeight = plan.planWeight,
                caveBlendMode = plan.caveBlendMode,
                chunkCoord = plan.ChunkCoord,
                hasMacroZone = plan.hasMacroZone,
                macroZoneCoord = plan.MacroZoneCoord
            });
        }

        private void BuildTerrainSkirt(Transform root, Material seamMaterial, in WorldGenerativeGeologySeamPlan plan, ref int primitiveIndex)
        {
            int segments = Mathf.Clamp(terrainSkirtSegments, 4, 12);
            float radius = Mathf.Max(1.8f, plan.seamBlendRadius * 0.72f);
            float height = Mathf.Max(0.8f, (plan.suggestedTerrainRaise + plan.suggestedTerrainCut + 1f) * verticalBlendScale);
            Vector3 contact = root.InverseTransformPoint(plan.TerrainContactPosition);

            for (int i = 0; i < segments; i++)
            {
                float angle = (360f / segments) * i + (plan.runtimeKey % 31);
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                Vector3 localPosition = new Vector3(contact.x + offset.x, contact.y + height * 0.35f, contact.z + offset.z);
                Vector3 localScale = new Vector3(Mathf.Max(0.5f, radius * 0.28f), height, Mathf.Max(0.45f, radius * 0.22f));
                Quaternion localRotation = Quaternion.Euler(0f, angle, Mathf.Lerp(-14f, 14f, i / Mathf.Max(1f, segments - 1f)));
                CreatePrimitive(root, seamMaterial, PrimitiveType.Cube, $"TerrainSkirt_{i}", localPosition, localRotation, localScale, ref primitiveIndex);
            }
        }

        private void BuildVoxelCollar(Transform root, Material seamMaterial, in WorldGenerativeGeologySeamPlan plan, ref int primitiveIndex)
        {
            int segments = Mathf.Clamp(voxelCollarSegments, 3, 10);
            float radius = Mathf.Max(1.4f, plan.seamBlendRadius * 0.48f);
            float height = Mathf.Max(1f, plan.voxelVolumeSize.y * 0.2f);
            Vector3 center = root.InverseTransformPoint(plan.voxelVolumeCenter);

            for (int i = 0; i < segments; i++)
            {
                float angle = (360f / segments) * i + 18f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                Vector3 localPosition = new Vector3(center.x + offset.x, center.y - height * 0.15f, center.z + offset.z);
                Vector3 localScale = new Vector3(Mathf.Max(0.42f, radius * 0.18f), height, Mathf.Max(0.42f, radius * 0.18f));
                CreatePrimitive(root, seamMaterial, PrimitiveType.Cylinder, $"VoxelCollar_{i}", localPosition, Quaternion.identity, localScale, ref primitiveIndex);
            }
        }

        private void BuildDebrisBand(Transform root, Material seamMaterial, in WorldGenerativeGeologySeamPlan plan, ref int primitiveIndex)
        {
            int debrisCount = Mathf.Clamp(plan.suggestedDebrisCount, 1, 14);
            float radius = Mathf.Max(0.8f, plan.seamBlendRadius * 0.68f);
            Vector3 contact = root.InverseTransformPoint(plan.TerrainContactPosition);

            for (int i = 0; i < debrisCount; i++)
            {
                float t = debrisCount <= 1 ? 0f : i / (float)(debrisCount - 1);
                float angle = t * 360f + (plan.runtimeKey % 47);
                float jitter = Mathf.Lerp(-radius * 0.18f, radius * 0.18f, Hash01(plan.runtimeKey, i, 11));
                float localRadius = radius + jitter;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * localRadius;
                float scale = Mathf.Lerp(0.22f, 0.65f, Hash01(plan.runtimeKey, i, 29)) * debrisScale;
                Quaternion rotation = Quaternion.Euler(
                    Hash01(plan.runtimeKey, i, 37) * 18f,
                    angle + Hash01(plan.runtimeKey, i, 43) * 45f,
                    Hash01(plan.runtimeKey, i, 59) * 22f);
                CreatePrimitive(
                    root,
                    seamMaterial,
                    i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Capsule,
                    $"Debris_{i}",
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

        private static Transform GetOrCreateSeamRoot(Transform host)
        {
            Transform seamRoot = host.Find(SeamRootName);
            if (seamRoot != null)
            {
                ActivateTransform(seamRoot);
                return seamRoot;
            }

            seamRoot = new GameObject(SeamRootName).transform;
            seamRoot.SetParent(host, false);
            return seamRoot;
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
                WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisual(
                    existing,
                    primitiveType,
                    name,
                    localPosition,
                    localRotation,
                    localScale,
                    seamMaterial);
            }
            else
            {
                WorldGeneratedPrimitiveFactory.CreatePrimitiveVisual(
                    root,
                    primitiveType,
                    name,
                    localPosition,
                    localRotation,
                    localScale,
                    seamMaterial);
            }

            primitiveIndex++;
        }

        private Material ResolveSeamMaterial(Transform contextRoot)
        {
            if (contextRoot == null)
                return null;

            _rendererTraversalBuffer.Clear();
            _rendererTraversalBuffer.Add(contextRoot);
            for (int i = 0; i < _rendererTraversalBuffer.Count; i++)
            {
                Transform current = _rendererTraversalBuffer[i];
                if (current == null)
                    continue;

                if (current.TryGetComponent(out Renderer renderer))
                {
                    Material material = renderer.sharedMaterial;
                    if (material != null)
                        return material;
                }

                for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                    _rendererTraversalBuffer.Add(current.GetChild(childIndex));
            }

            return null;
        }

        private static int ComputeBuildSignature(in WorldGenerativeGeologySeamPlan plan)
        {
            unchecked
            {
                int hash = (int)plan.runtimeKey;
                hash = (hash * 397) ^ plan.terrainSeamMode.GetHashCode();
                hash = (hash * 397) ^ plan.caveBlendMode.GetHashCode();
                hash = (hash * 397) ^ Mathf.RoundToInt(plan.seamBlendRadius * 100f);
                hash = (hash * 397) ^ Mathf.RoundToInt(plan.suggestedTerrainRaise * 100f);
                hash = (hash * 397) ^ Mathf.RoundToInt(plan.suggestedTerrainCut * 100f);
                hash = (hash * 397) ^ plan.suggestedDebrisCount;
                return hash;
            }
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

                if (runtime.gameObject.activeSelf)
                    runtime.gameObject.SetActive(false);
            }
        }

        private static void DisableUnusedChildren(Transform root, int activeChildCount)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
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

        private void ResolveReferences()
        {
            if (integrationDirector != null && playerTransform != null)
                return;

            float now = Time.realtimeSinceStartup;
            if (now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);

            WorldRuntimeReferenceUtility.TryResolveSceneObject(ref integrationDirector);
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
        }
    }
}
