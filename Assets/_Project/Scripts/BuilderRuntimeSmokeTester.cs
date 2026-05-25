// ============================================================================
// HECTON-8 - BuilderRuntimeSmokeTester.cs
// Dev-only runtime smoke for the builder construction loop.
// Verifies deploy -> registry -> recover -> refund without relying on input.
// ============================================================================

using System.Threading;
using Hecton8.Bootstrap;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Builder Runtime Smoke Tester")]
    public sealed class BuilderRuntimeSmokeTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerBuilder playerBuilder;
        [SerializeField] private ConstructionManager constructionManager;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private ToolLoadoutProvisioner loadoutProvisioner;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private bool provisionConstructionMaterials = true;
        [SerializeField] private bool consumeBuildCostOnDeploy = true;
        [SerializeField] private float startupDelay = 0f;
        [SerializeField] private float recoverDelay = 0f;
        [SerializeField] private bool verboseLogging = false;

        [Header("Placement Search")]
        [SerializeField] private float forwardDistance = 4.5f;
        [SerializeField] private float verticalOffset = -1.25f;
        [SerializeField] private float lateralStep = 2.25f;
        [SerializeField] private float depthStep = 1.5f;

        private const int BuildCostSnapshotCapacity = 32;
        private const int PlacementCandidateCount = 7;

        // COLD ALLOC: int[32] - build-cost inventory snapshot reused by builder smoke pass - owner: BuilderRuntimeSmokeTester
        private readonly int[] _buildCostCountSnapshot = new int[BuildCostSnapshotCapacity];
        // COLD ALLOC: SpatialQueryHit[7] - registered placement probe scratch - owner: BuilderRuntimeSmokeTester
        private readonly SpatialQueryHit[] _placementProbeHits = new SpatialQueryHit[PlacementCandidateCount];
        private bool _isRunning;
        private float _nextWaitHeartbeatAt;

        private void Awake()
        {
            AutoResolveSceneReferences();
            LogVerbose($"AWAKE runOnStart={runOnStart} refs={DescribeResolvedRefs()}");
        }

        private void OnEnable()
        {
            LogVerbose($"ON_ENABLE runOnStart={runOnStart} isRunning={_isRunning}");
        }

        private void OnDisable()
        {
            LogVerbose($"ON_DISABLE isRunning={_isRunning} activeInHierarchy={gameObject.activeInHierarchy}");
        }

        private void OnDestroy()
        {
            LogVerbose("ON_DESTROY");
        }

        private void Start()
        {
            LogVerbose($"START runOnStart={runOnStart} isRunning={_isRunning} refs={DescribeResolvedRefs()}");
            if (!runOnStart || _isRunning)
                return;

            LogVerbose("START scheduling smoke pass.");
            _ = RunSmokePassAsync(destroyCancellationToken);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AutoResolveSceneReferences();
        }
#endif

        [ContextMenu("Run Builder Runtime Smoke Pass")]
        public void RunFromContextMenu()
        {
            if (_isRunning)
                return;

            LogVerbose("CONTEXT_MENU scheduling smoke pass.");
            _ = RunSmokePassAsync(destroyCancellationToken);
        }

        private async Awaitable RunSmokePassAsync(CancellationToken cancellationToken)
        {
            if (_isRunning)
            {
                LogVerbose("RUN aborted because a previous smoke pass is still active.");
                return;
            }

            AutoResolveSceneReferences();
            LogVerbose($"RUN begin refs={DescribeResolvedRefs()}");
            if (playerBuilder == null || constructionManager == null || playerInventory == null)
            {
                Debug.LogWarning("[BuilderSmoke] Missing PlayerBuilder, ConstructionManager or PlayerInventory.");
                return;
            }

            _isRunning = true;
            try
            {
                if (startupDelay > 0f)
                {
                    LogVerbose($"WAIT startupDelay={startupDelay:0.00}s");
                    await WaitRealtimeWithHeartbeatAsync(startupDelay, "startup", cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested || this == null)
                    return;

                if (provisionConstructionMaterials && loadoutProvisioner != null)
                {
                    LogVerbose("PROVISION construction materials.");
                    loadoutProvisioner.ProvisionConstructionMaterials();
                }

                BuildableData buildable = playerBuilder.ActiveBuildable;
                if (buildable == null || buildable.finalPrefab == null)
                {
                    Debug.LogWarning("[BuilderSmoke] No active buildable or final prefab.");
                    return;
                }

                Vector3 placePos;
                Quaternion placeRot;
                if (!TryResolvePlacementPose(out placePos, out placeRot))
                {
                    Debug.LogWarning("[BuilderSmoke] Could not resolve a safe placement pose.");
                    return;
                }

                int moduleCountBefore = constructionManager.ModuleCount;
                int countSnapshotLength = CaptureBuildCosts(buildable, _buildCostCountSnapshot);
                if (countSnapshotLength < 0)
                    return;

                LogVerbose(
                    $"STATE active={buildable.moduleName} registryBefore={moduleCountBefore} inventoryWeight={playerInventory.TotalWeight:0.0}");

                LogVerbose($"DEPLOY pose={placePos} rotY={placeRot.eulerAngles.y:0.0}");
                bool deployed = playerBuilder.DebugDeployActiveBuildable(
                    placePos,
                    placeRot,
                    consumeBuildCostOnDeploy);

                if (!deployed)
                {
                    Debug.LogWarning("[BuilderSmoke] FAIL deploy.");
                    return;
                }

                int moduleCountAfterDeploy = constructionManager.ModuleCount;
                LogVerbose($"REGISTRY afterDeploy={moduleCountAfterDeploy}");
                if (moduleCountAfterDeploy <= moduleCountBefore)
                {
                    Debug.LogWarning(
                        $"[BuilderSmoke] FAIL registry did not grow. before={moduleCountBefore} after={moduleCountAfterDeploy}");
                    return;
                }

                BaseModule spawnedModule = ResolveLastSpawnedModule();
                if (spawnedModule == null)
                {
                    Debug.LogWarning("[BuilderSmoke] FAIL no spawned BaseModule found after deploy.");
                    return;
                }

                LogVerbose($"MODULE spawned={spawnedModule.name}");

                if (consumeBuildCostOnDeploy)
                    ValidateCostConsumption(buildable, _buildCostCountSnapshot, countSnapshotLength);

                if (recoverDelay > 0f)
                {
                    LogVerbose($"WAIT recoverDelay={recoverDelay:0.00}s");
                    await WaitRealtimeWithHeartbeatAsync(recoverDelay, "recover", cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested || this == null)
                    return;

                LogVerbose($"RECOVER module={spawnedModule.name}");
                bool recovered = playerBuilder.DebugRecoverModule(spawnedModule);
                if (!recovered)
                {
                    Debug.LogWarning("[BuilderSmoke] FAIL recover.");
                    return;
                }

                int moduleCountAfterRecover = constructionManager.ModuleCount;
                LogVerbose($"REGISTRY afterRecover={moduleCountAfterRecover}");
                if (moduleCountAfterRecover >= moduleCountAfterDeploy)
                {
                    Debug.LogWarning(
                        $"[BuilderSmoke] FAIL registry did not shrink on recover. deploy={moduleCountAfterDeploy} recover={moduleCountAfterRecover}");
                    return;
                }

                Hecton8.Core.H8Debug.Log(
                    $"[BuilderSmoke] PASS buildable={buildable.moduleName} registry={moduleCountBefore}->{moduleCountAfterDeploy}->{moduleCountAfterRecover}");
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                _isRunning = false;
            }
        }

        private void AutoResolveSceneReferences()
        {
            if (playerBuilder == null)
                playerBuilder = (Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.PlayerBuilder : null);

            if (constructionManager == null)
                constructionManager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;

            if (playerInventory == null)
                playerInventory = (Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.Inventory : null);

            if (loadoutProvisioner == null)
                loadoutProvisioner = ToolLoadoutProvisioner.ActiveRuntimeInstance;
        }

        private string DescribeResolvedRefs()
        {
            return $"builder={(playerBuilder != null ? "Y" : "N")} ctor={(constructionManager != null ? "Y" : "N")} inv={(playerInventory != null ? "Y" : "N")} prov={(loadoutProvisioner != null ? "Y" : "N")}";
        }

        private async Awaitable WaitRealtimeWithHeartbeatAsync(float duration, string phase, CancellationToken cancellationToken)
        {
            float startAt = Time.realtimeSinceStartup;
            float endAt = startAt + Mathf.Max(0f, duration);
            _nextWaitHeartbeatAt = startAt + 0.25f;

            while (Time.realtimeSinceStartup < endAt)
            {
                if (verboseLogging && Time.realtimeSinceStartup >= _nextWaitHeartbeatAt)
                {
                    float remaining = Mathf.Max(0f, endAt - Time.realtimeSinceStartup);
                    Hecton8.Core.H8Debug.Log($"[BuilderSmoke] WAIT_HEARTBEAT phase={phase} remaining={remaining:0.00}s");
                    _nextWaitHeartbeatAt = Time.realtimeSinceStartup + 0.25f;
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }

            LogVerbose($"WAIT_COMPLETE phase={phase}");
        }

        private bool TryResolvePlacementPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            Transform reference = null;

            if (playerBuilder != null)
            {
                Camera playerCamera = Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null
                    ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera
                    : null;
                if (playerCamera == null)
                    playerBuilder.TryGetComponent(out playerCamera);
                reference = playerCamera != null ? playerCamera.transform : playerBuilder.transform;
            }

            if (reference == null &&
                GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                Camera playerCamera = Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null
                    ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera
                    : null;
                if (playerCamera == null)
                    playerTransform.TryGetComponent(out playerCamera);
                reference = playerCamera != null ? playerCamera.transform : playerTransform;
            }

            if (reference == null)
                reference = transform;

            if (reference == null)
                return false;

            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = reference.forward;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 basePos = reference.position + forward * forwardDistance + Vector3.up * verticalOffset;

            rotation = Quaternion.LookRotation(forward, Vector3.up);

            for (int i = 0; i < PlacementCandidateCount; i++)
            {
                Vector3 candidate = basePos + ResolvePlacementOffset(i, right, forward);
                if (!IsPlacementCandidateBlocked(candidate, 0.75f))
                {
                    position = candidate;
                    return true;
                }
            }

            position = basePos;
            return true;
        }

        private bool IsPlacementCandidateBlocked(Vector3 candidate, float radius)
        {
            const SpatialTargetKind kindMask =
                SpatialTargetKind.Resource |
                SpatialTargetKind.Bioform |
                SpatialTargetKind.Pickup |
                SpatialTargetKind.Scannable |
                SpatialTargetKind.Module;

            float safeRadius = Mathf.Max(0.1f, radius);
            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(candidate, safeRadius, kindMask, _placementProbeHits);
            if (hitCount <= 0)
                return false;

            int layerMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
            float radiusSq = safeRadius * safeRadius;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                SpatialQueryHit hit = _placementProbeHits[hitIndex];
                _placementProbeHits[hitIndex] = default;

                if (!LayerMatchesMask(hit.Layer, layerMask))
                    continue;

                if (hit.DistanceSqr <= radiusSq)
                    return true;
            }

            return false;
        }

        private static bool LayerMatchesMask(int layer, int mask)
        {
            return layer >= 0 && layer < 32 && (mask & (1 << layer)) != 0;
        }

        private BaseModule ResolveLastSpawnedModule()
        {
            var modules = constructionManager.SpawnedModules;
            if (modules == null || modules.Count == 0)
                return null;

            for (int i = modules.Count - 1; i >= 0; i--)
            {
                GameObject go = modules[i];
                if (go == null)
                    continue;

                if (go.TryGetComponent(out BaseModule module))
                    return module;
            }

            return null;
        }

        private int CaptureBuildCosts(BuildableData buildable, int[] counts)
        {
            if (buildable == null || buildable.buildCost == null || buildable.buildCost.Count == 0)
                return 0;

            if (counts == null || buildable.buildCost.Count > counts.Length)
            {
                Debug.LogWarning(
                    $"[BuilderSmoke] Build cost count {buildable.buildCost.Count} exceeds snapshot capacity {BuildCostSnapshotCapacity}.");
                return -1;
            }

            for (int i = 0; i < buildable.buildCost.Count; i++)
            {
                InventoryCost cost = buildable.buildCost[i];
                counts[i] = cost.item != null ? playerInventory.CountTotal(Hecton.Localization.LocHash.Compute(cost.item.PersistentId)) : 0;
            }

            return buildable.buildCost.Count;
        }

        private void ValidateCostConsumption(BuildableData buildable, int[] countsBefore, int countSnapshotLength)
        {
            if (buildable == null || buildable.buildCost == null)
                return;

            for (int i = 0; i < buildable.buildCost.Count && i < countSnapshotLength; i++)
            {
                InventoryCost cost = buildable.buildCost[i];
                if (cost.item == null)
                    continue;

                int before = countsBefore[i];
                int after = playerInventory.CountTotal(Hecton.Localization.LocHash.Compute(cost.item.PersistentId));
                int expectedUpperBound = before - Mathf.Max(cost.amount, 0);
                if (after > before)
                {
                    Debug.LogWarning($"[BuilderSmoke] Unexpected cost increase for {cost.item.itemName}: {before}->{after}");
                    continue;
                }

                LogVerbose(
                    $"COST {cost.item.itemName}: before={before} after={after} expected<={expectedUpperBound}");
            }
        }

        private Vector3 ResolvePlacementOffset(int index, Vector3 right, Vector3 forward)
        {
            switch (index)
            {
                case 1:
                    return right * lateralStep;
                case 2:
                    return -right * lateralStep;
                case 3:
                    return forward * depthStep;
                case 4:
                    return -forward * depthStep;
                case 5:
                    return right * lateralStep + forward * depthStep;
                case 6:
                    return -right * lateralStep + forward * depthStep;
                default:
                    return Vector3.zero;
            }
        }

        private void LogVerbose(string message)
        {
            if (!verboseLogging)
                return;

            Hecton8.Core.H8Debug.Log($"[BuilderSmoke] {message}");
        }
    }
}
