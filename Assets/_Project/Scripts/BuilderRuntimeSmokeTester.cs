// ============================================================================
// HECTON-8 - BuilderRuntimeSmokeTester.cs
// Dev-only runtime smoke for the builder construction loop.
// Verifies deploy -> registry -> recover -> refund without relying on input.
// ============================================================================

using System.Collections;
using Hecton8.Bootstrap;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Gameplay;
using Hecton8.Inventory;
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
            StartCoroutine(RunSmokePass());
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
            StartCoroutine(RunSmokePass());
        }

        private IEnumerator RunSmokePass()
        {
            if (_isRunning)
            {
                LogVerbose("RUN aborted because a previous smoke pass is still active.");
                yield break;
            }

            AutoResolveSceneReferences();
            LogVerbose($"RUN begin refs={DescribeResolvedRefs()}");
            if (playerBuilder == null || constructionManager == null || playerInventory == null)
            {
                Debug.LogWarning("[BuilderSmoke] Missing PlayerBuilder, ConstructionManager or PlayerInventory.");
                yield break;
            }

            _isRunning = true;

            if (startupDelay > 0f)
            {
                LogVerbose($"WAIT startupDelay={startupDelay:0.00}s");
                yield return WaitRealtimeWithHeartbeat(startupDelay, "startup");
            }

            if (provisionConstructionMaterials && loadoutProvisioner != null)
            {
                LogVerbose("PROVISION construction materials.");
                loadoutProvisioner.ProvisionConstructionMaterials();
            }

            BuildableData buildable = playerBuilder.ActiveBuildable;
            if (buildable == null || buildable.finalPrefab == null)
            {
                Debug.LogWarning("[BuilderSmoke] No active buildable or final prefab.");
                _isRunning = false;
                yield break;
            }

            Vector3 placePos;
            Quaternion placeRot;
            if (!TryResolvePlacementPose(out placePos, out placeRot))
            {
                Debug.LogWarning("[BuilderSmoke] Could not resolve a safe placement pose.");
                _isRunning = false;
                yield break;
            }

            int moduleCountBefore = constructionManager.ModuleCount;
            int[] countsBefore = SnapshotBuildCosts(buildable);
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
                _isRunning = false;
                yield break;
            }

            int moduleCountAfterDeploy = constructionManager.ModuleCount;
            LogVerbose($"REGISTRY afterDeploy={moduleCountAfterDeploy}");
            if (moduleCountAfterDeploy <= moduleCountBefore)
            {
                Debug.LogWarning(
                    $"[BuilderSmoke] FAIL registry did not grow. before={moduleCountBefore} after={moduleCountAfterDeploy}");
                _isRunning = false;
                yield break;
            }

            BaseModule spawnedModule = ResolveLastSpawnedModule();
            if (spawnedModule == null)
            {
                Debug.LogWarning("[BuilderSmoke] FAIL no spawned BaseModule found after deploy.");
                _isRunning = false;
                yield break;
            }

            LogVerbose($"MODULE spawned={spawnedModule.name}");

            if (consumeBuildCostOnDeploy)
                ValidateCostConsumption(buildable, countsBefore);

            if (recoverDelay > 0f)
            {
                LogVerbose($"WAIT recoverDelay={recoverDelay:0.00}s");
                yield return WaitRealtimeWithHeartbeat(recoverDelay, "recover");
            }

            LogVerbose($"RECOVER module={spawnedModule.name}");
            bool recovered = playerBuilder.DebugRecoverModule(spawnedModule);
            if (!recovered)
            {
                Debug.LogWarning("[BuilderSmoke] FAIL recover.");
                _isRunning = false;
                yield break;
            }

            int moduleCountAfterRecover = constructionManager.ModuleCount;
            LogVerbose($"REGISTRY afterRecover={moduleCountAfterRecover}");
            if (moduleCountAfterRecover >= moduleCountAfterDeploy)
            {
                Debug.LogWarning(
                    $"[BuilderSmoke] FAIL registry did not shrink on recover. deploy={moduleCountAfterDeploy} recover={moduleCountAfterRecover}");
                _isRunning = false;
                yield break;
            }

            Debug.Log(
                $"[BuilderSmoke] PASS buildable={buildable.moduleName} registry={moduleCountBefore}->{moduleCountAfterDeploy}->{moduleCountAfterRecover}");
            _isRunning = false;
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

        private IEnumerator WaitRealtimeWithHeartbeat(float duration, string phase)
        {
            float startAt = Time.realtimeSinceStartup;
            float endAt = startAt + Mathf.Max(0f, duration);
            _nextWaitHeartbeatAt = startAt + 0.25f;

            while (Time.realtimeSinceStartup < endAt)
            {
                if (verboseLogging && Time.realtimeSinceStartup >= _nextWaitHeartbeatAt)
                {
                    float remaining = Mathf.Max(0f, endAt - Time.realtimeSinceStartup);
                    Debug.Log($"[BuilderSmoke] WAIT_HEARTBEAT phase={phase} remaining={remaining:0.00}s");
                    _nextWaitHeartbeatAt = Time.realtimeSinceStartup + 0.25f;
                }

                yield return null;
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
                Camera playerCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerBuilder.GetComponent<Camera>());
                reference = playerCamera != null ? playerCamera.transform : playerBuilder.transform;
            }

            if (reference == null &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                Camera playerCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
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

            Vector3[] offsets =
            {
                Vector3.zero,
                right * lateralStep,
                -right * lateralStep,
                forward * depthStep,
                -forward * depthStep,
                right * lateralStep + forward * depthStep,
                -right * lateralStep + forward * depthStep,
            };

            rotation = Quaternion.LookRotation(forward, Vector3.up);

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 candidate = basePos + offsets[i];
                if (!UnityEngine.Physics.CheckSphere(candidate, 0.75f, (1 << 8) | (1 << 9) | (1 << 10), QueryTriggerInteraction.Ignore))
                {
                    position = candidate;
                    return true;
                }
            }

            position = basePos;
            return true;
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

        private int[] SnapshotBuildCosts(BuildableData buildable)
        {
            if (buildable == null || buildable.buildCost == null || buildable.buildCost.Count == 0)
                return System.Array.Empty<int>();

            int[] counts = new int[buildable.buildCost.Count];
            for (int i = 0; i < buildable.buildCost.Count; i++)
            {
                InventoryCost cost = buildable.buildCost[i];
                counts[i] = cost.item != null ? playerInventory.CountTotal(Hecton.Localization.LocHash.Compute(cost.item.PersistentId)) : 0;
            }

            return counts;
        }

        private void ValidateCostConsumption(BuildableData buildable, int[] countsBefore)
        {
            if (buildable == null || buildable.buildCost == null)
                return;

            for (int i = 0; i < buildable.buildCost.Count && i < countsBefore.Length; i++)
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

        private void LogVerbose(string message)
        {
            if (!verboseLogging)
                return;

            Debug.Log($"[BuilderSmoke] {message}");
        }
    }
}
