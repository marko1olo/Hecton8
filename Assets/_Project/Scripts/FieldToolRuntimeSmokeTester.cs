// ============================================================================
// HECTON-8 - FieldToolRuntimeSmokeTester.cs
// Dev-only smoke pass for salvage recovery and cutter deconstruction loops.
// Verifies real field-facing tool interactions without relying on live input.
// ============================================================================

using System;
using System.Threading;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Dev;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Field Tool Runtime Smoke Tester")]
    public sealed class FieldToolRuntimeSmokeTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerBuilder playerBuilder;
        [SerializeField] private ConstructionManager constructionManager;
        [SerializeField] private ToolLoadoutProvisioner loadoutProvisioner;
        [SerializeField] private ItemData salvageProbeItem;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private float equipTimeout = 1.25f;
        [SerializeField] private float settleDelay = 0.2f;
        [SerializeField] private float salvageForwardDistance = 2.5f;
        [SerializeField] private float cutterForwardDistance = 4.5f;
        [SerializeField] private float verticalOffset = -1.1f;
        [SerializeField] private bool verboseLogging = false;

        // Inspector-only smoke diagnostics for manual field tool validation.
#pragma warning disable CS0414
        [Header("Diagnostics")]
        [SerializeField] private int _debugRunCount;
        [SerializeField] private string _debugLastPhase = "Idle";
        [SerializeField] private string _debugLastStep = string.Empty;
        [SerializeField] private bool _debugLastSalvagePass;
        [SerializeField] private bool _debugLastCutterPass;
        [SerializeField] private string _debugLastIssue = string.Empty;
#pragma warning restore CS0414

        private bool _isRunning;

        private void Awake()
        {
            AutoResolveSceneReferences();
#if UNITY_EDITOR
            AutoResolveDefaultAssets();
#endif
        }

        private void Start()
        {
            if (!runOnStart || _isRunning)
                return;

            _ = RunSmokePassAsync(destroyCancellationToken);
        }

        [ContextMenu("Run Field Tool Runtime Smoke Pass")]
        public void RunFromContextMenu()
        {
            if (_isRunning)
                return;

            _ = RunSmokePassAsync(destroyCancellationToken);
        }

        private async Awaitable RunSmokePassAsync(CancellationToken cancellationToken)
        {
            if (_isRunning)
                return;

            AutoResolveSceneReferences();
            if (toolManager == null || playerInventory == null || playerBuilder == null || constructionManager == null)
            {
                Debug.LogWarning($"[FieldToolSmoke] Missing references refs={DescribeRefs()}");
                return;
            }

            if (salvageProbeItem == null)
            {
                Debug.LogWarning("[FieldToolSmoke] Missing salvage probe item.");
                return;
            }

            _isRunning = true;
            try
            {
                _debugRunCount++;
                _debugLastPhase = "Start";
                _debugLastStep = "Begin";
                _debugLastIssue = string.Empty;
                if (startupDelay > 0f)
                    await DelayRealtimeAsync(startupDelay, cancellationToken);

                GameObject[] originalAssignments = SnapshotAssignments();
                int originalSlot = toolManager.CurrentSlotIndex;

                if (loadoutProvisioner != null)
                {
                    loadoutProvisioner.ProvisionFullToolKit();
                    loadoutProvisioner.ProvisionConstructionMaterials();
                }

                _debugLastStep = "WaitInitialToolIdle";
                await WaitUntilAsync(
                    () => toolManager != null && !toolManager.IsSwapping,
                    equipTimeout * 2f,
                    "Initial tool-manager settle",
                    cancellationToken);

                _debugLastPhase = "Salvage";
                _debugLastStep = "RunSalvagePass";
                bool salvagePass = await RunSalvagePassAsync(cancellationToken);
                _debugLastSalvagePass = salvagePass;

                _debugLastPhase = "Cutter";
                _debugLastStep = "RunCutterPass";
                bool cutterPass = await RunCutterPassAsync(cancellationToken);
                _debugLastCutterPass = cutterPass;

                _debugLastPhase = "Restore";
                _debugLastStep = "RestoreLoadout";
                await RestoreLoadoutAsync(originalAssignments, originalSlot, cancellationToken);

                if (salvagePass && cutterPass)
                {
                    _debugLastPhase = "Complete";
                    Debug.Log("[FieldToolSmoke] PASS salvage=True cutter=True");
                }
                else
                {
                    _debugLastPhase = "Failed";
                    Debug.LogWarning($"[FieldToolSmoke] FAIL salvage={salvagePass} cutter={cutterPass}");
                }
            }
            catch (OperationCanceledException)
            {
                _debugLastPhase = "Cancelled";
                _debugLastIssue = "Cancelled";
                LogVerbose("Cancelled.");
            }
            catch (Exception ex)
            {
                _debugLastPhase = "Exception";
                _debugLastIssue = "Unhandled exception";
                Debug.LogError($"[FieldToolSmoke] UNHANDLED EXCEPTION: {ex}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        private async Awaitable<bool> RunSalvagePassAsync(CancellationToken cancellationToken)
        {
            _debugLastStep = "ResolveSalvagePrefab";

            GameObject salvagePrefab = toolManager.GetKnownToolPrefabForToolType<SalvageSamplerTool>();
            if (salvagePrefab == null)
            {
                _debugLastIssue = "Missing SalvageSamplerTool prefab registration";
                Debug.LogWarning("[FieldToolSmoke] Missing SalvageSamplerTool prefab registration.");
                return false;
            }

            if (!salvagePrefab.TryGetComponent(out PlayerTool salvagePrefabTool) || salvagePrefabTool.ToolData == null)
            {
                _debugLastIssue = "Salvage sampler prefab missing ToolData";
                Debug.LogWarning("[FieldToolSmoke] Salvage sampler prefab is missing ToolData.");
                return false;
            }

            if (!playerInventory.ContainsItem(Hecton.Localization.LocHash.Compute(salvagePrefabTool.ToolData.PersistentId)))
                playerInventory.TryAddItem(Hecton.Localization.LocHash.Compute(salvagePrefabTool.ToolData.PersistentId), 1);

            _debugLastStep = "CreateSalvageProbe";
            int beforeCount = playerInventory.CountTotal(Hecton.Localization.LocHash.Compute(salvageProbeItem.PersistentId));
            GameObject probe = CreateSalvageProbe();
            if (probe == null)
            {
                _debugLastIssue = "Failed to create salvage probe";
                Debug.LogWarning("[FieldToolSmoke] Could not create salvage probe.");
                return false;
            }

            try
            {
                _debugLastStep = "HolsterForSalvage";
                if (!IsToolManagerHolstered())
                {
                    toolManager.Holster();
                    await WaitUntilAsync(
                        () => IsToolManagerHolstered(),
                        equipTimeout,
                        "Holster before salvage",
                        cancellationToken);
                }

                _debugLastStep = "AssignSalvageSlot";
                toolManager.SetAssignedToolPrefab(0, salvagePrefab, holsterIfCurrentInvalid: false);
                _debugLastStep = "SwitchToSalvageSlot";
                toolManager.SwitchToSlot(0);
                _debugLastStep = "WaitEquipSalvage";
                await WaitUntilAsync(
                    () => !toolManager.IsSwapping && toolManager.CurrentTool is SalvageSamplerTool,
                    equipTimeout,
                    "Equip salvage sampler",
                    cancellationToken);

                if (!(toolManager.CurrentTool is SalvageSamplerTool sampler))
                {
                    _debugLastIssue = "Salvage sampler did not become active";
                    Debug.LogWarning("[FieldToolSmoke] Salvage sampler did not become active.");
                    return false;
                }

                _debugLastStep = "UseSalvageSecondary";
                sampler.UseSecondary(0f);
                _debugLastStep = "SettleAfterSalvage";
                await DelayRealtimeAsync(settleDelay, cancellationToken);

                _debugLastStep = "VerifySalvage";
                int afterCount = playerInventory.CountTotal(Hecton.Localization.LocHash.Compute(salvageProbeItem.PersistentId));
                bool recovered = afterCount > beforeCount && !probe.activeSelf;
                if (!recovered)
                {
                    _debugLastIssue = $"Salvage failed inventory={beforeCount}->{afterCount} probeActive={probe.activeSelf}";
                    Debug.LogWarning($"[FieldToolSmoke] Salvage failed inventory={beforeCount}->{afterCount} probeActive={probe.activeSelf}");
                    return false;
                }

                Debug.Log($"[FieldToolSmoke] PASS salvage item={salvageProbeItem.itemName} inventory={beforeCount}->{afterCount}");
                return true;
            }
            finally
            {
                if (probe != null)
                    Destroy(probe);
            }
        }

        private async Awaitable<bool> RunCutterPassAsync(CancellationToken cancellationToken)
        {
            _debugLastStep = "ResolveCutterPrefab";

            GameObject cutterPrefab = toolManager.GetKnownToolPrefabForToolType<LaserCutter>();
            if (cutterPrefab == null)
            {
                _debugLastIssue = "Missing LaserCutter prefab registration";
                Debug.LogWarning("[FieldToolSmoke] Missing LaserCutter prefab registration.");
                return false;
            }

            if (!cutterPrefab.TryGetComponent(out PlayerTool cutterPrefabTool) || cutterPrefabTool.ToolData == null)
            {
                _debugLastIssue = "Laser cutter prefab missing ToolData";
                Debug.LogWarning("[FieldToolSmoke] Laser cutter prefab is missing ToolData.");
                return false;
            }

            if (!playerInventory.ContainsItem(Hecton.Localization.LocHash.Compute(cutterPrefabTool.ToolData.PersistentId)))
                playerInventory.TryAddItem(Hecton.Localization.LocHash.Compute(cutterPrefabTool.ToolData.PersistentId), 1);

            if (playerBuilder.ActiveBuildable == null || playerBuilder.ActiveBuildable.finalPrefab == null)
            {
                _debugLastIssue = "Builder has no active buildable";
                Debug.LogWarning("[FieldToolSmoke] Builder has no active buildable.");
                return false;
            }

            Vector3 placePos = ResolvePlacementPose(cutterForwardDistance);
            Quaternion placeRot = Quaternion.LookRotation(GetForwardReference(), Vector3.up);
            int beforeModuleCount = constructionManager.ModuleCount;
            _debugLastStep = "DeployModuleForCutter";
            bool deployed = playerBuilder.DebugDeployActiveBuildable(placePos, placeRot, consumeCost: false);
            if (!deployed)
            {
                _debugLastIssue = "Could not deploy module for cutter pass";
                Debug.LogWarning("[FieldToolSmoke] Could not deploy module for cutter pass.");
                return false;
            }

            BaseModule module = ResolveLastSpawnedModule();
            if (module == null)
            {
                _debugLastIssue = "No spawned module found after deploy";
                Debug.LogWarning("[FieldToolSmoke] No spawned module found after deploy.");
                return false;
            }

            _debugLastStep = "HolsterForCutter";
            if (!IsToolManagerHolstered())
            {
                toolManager.Holster();
                await WaitUntilAsync(
                    () => IsToolManagerHolstered(),
                    equipTimeout,
                    "Holster before cutter",
                    cancellationToken);
            }

            _debugLastStep = "AssignCutterSlot";
            toolManager.SetAssignedToolPrefab(1, cutterPrefab, holsterIfCurrentInvalid: false);
            _debugLastStep = "SwitchToCutterSlot";
            toolManager.SwitchToSlot(1);
            _debugLastStep = "WaitEquipCutter";
            await WaitUntilAsync(
                () => !toolManager.IsSwapping && toolManager.CurrentTool is LaserCutter,
                equipTimeout,
                "Equip laser cutter",
                cancellationToken);

            if (!(toolManager.CurrentTool is LaserCutter cutter))
            {
                _debugLastIssue = "Laser cutter did not become active";
                Debug.LogWarning("[FieldToolSmoke] Laser cutter did not become active.");
                return false;
            }

            _debugLastStep = "RecoverModuleWithCutter";
            bool recovered = cutter.DebugRecoverModule(module);
            _debugLastStep = "SettleAfterCutter";
            await DelayRealtimeAsync(settleDelay, cancellationToken);

            _debugLastStep = "VerifyCutter";
            int afterModuleCount = constructionManager.ModuleCount;
            bool moduleGone = module == null || !module.gameObject.activeInHierarchy;

            if (!recovered || afterModuleCount >= beforeModuleCount + 1 || !moduleGone)
            {
                _debugLastIssue = $"Cutter failed recovered={recovered} registry={beforeModuleCount}->{afterModuleCount} moduleGone={moduleGone}";
                Debug.LogWarning(
                    $"[FieldToolSmoke] Cutter failed recovered={recovered} registry={beforeModuleCount}->{afterModuleCount} moduleGone={moduleGone}");
                return false;
            }

            Debug.Log($"[FieldToolSmoke] PASS cutter registry={beforeModuleCount}->{afterModuleCount}");
            return true;
        }

        private GameObject CreateSalvageProbe()
        {
            Vector3 position = transform.position + GetForwardReference() * salvageForwardDistance + Vector3.up * verticalOffset;
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            probe.name = "SMOKE_SALVAGE_PICKUP";
            probe.transform.SetPositionAndRotation(position, Quaternion.identity);
            probe.transform.localScale = new Vector3(0.35f, 0.2f, 0.35f);

            InteractionHighlighter highlighter = probe.AddComponent<InteractionHighlighter>();
            highlighter.SetHighlight(false);

            PickupItem pickup = probe.AddComponent<PickupItem>();
            pickup.Configure(salvageProbeItem, 1);

            Renderer renderer = probe.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                renderer.sharedMaterial.color = new Color(0.56f, 0.72f, 0.84f, 1f);

            return probe;
        }

        private async Awaitable RestoreLoadoutAsync(
            GameObject[] originalAssignments,
            int originalSlot,
            CancellationToken cancellationToken)
        {
            if (!IsToolManagerHolstered())
            {
                toolManager.Holster();
                await WaitUntilAsync(
                    () => IsToolManagerHolstered(),
                    equipTimeout,
                    "Holster for restore",
                    cancellationToken);
            }

            if (originalAssignments != null)
            {
                for (int i = 0; i < originalAssignments.Length; i++)
                    toolManager.SetAssignedToolPrefab(i, originalAssignments[i], holsterIfCurrentInvalid: false);
            }

            if (originalAssignments != null &&
                originalSlot >= 0 &&
                originalSlot < originalAssignments.Length &&
                originalAssignments[originalSlot] != null)
            {
                toolManager.SwitchToSlot(originalSlot);
                await WaitUntilAsync(
                    () => !toolManager.IsSwapping && toolManager.CurrentSlotIndex == originalSlot,
                    equipTimeout,
                    "Restore original slot",
                    cancellationToken);
            }
        }

        private GameObject[] SnapshotAssignments()
        {
            GameObject[] result = new GameObject[toolManager.SlotCount];
            for (int i = 0; i < result.Length; i++)
                result[i] = toolManager.GetAssignedToolPrefab(i);
            return result;
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

        private Vector3 ResolvePlacementPose(float distance)
        {
            return transform.position + GetForwardReference() * distance + Vector3.up * verticalOffset;
        }

        private Vector3 GetForwardReference()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            return forward.normalized;
        }

        private bool IsToolManagerHolstered()
        {
            return toolManager != null &&
                   !toolManager.IsSwapping &&
                   toolManager.CurrentTool == null &&
                   toolManager.CurrentSlotIndex < 0;
        }

        private static async Awaitable DelayRealtimeAsync(float seconds, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
            }
        }

        private async Awaitable<bool> WaitUntilAsync(
            Func<bool> predicate,
            float timeout,
            string label,
            CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.01f, timeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool success = false;
                try
                {
                    success = predicate();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FieldToolSmoke] EXCEPTION {label}: {ex}");
                    return false;
                }

                if (success)
                    return true;

                await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
            }

            _debugLastIssue = $"TIMEOUT {label}";
            Debug.LogWarning($"[FieldToolSmoke] TIMEOUT {label} after {timeout:0.00}s");
            return false;
        }

        private void AutoResolveSceneReferences()
        {
            if (toolManager == null)
                toolManager = (Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.ToolManager : null);
            if (playerInventory == null)
                playerInventory = (Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.Inventory : null);
            if (playerBuilder == null)
                playerBuilder = (Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.PlayerBuilder : null);
            if (constructionManager == null)
                constructionManager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;
            if (loadoutProvisioner == null)
                loadoutProvisioner = ToolLoadoutProvisioner.ActiveRuntimeInstance;
            if (salvageProbeItem == null)
            {
                PickupItem pickup = PickupItem.ActiveRuntimeInstance;
                if (pickup != null)
                    salvageProbeItem = pickup.ItemData;
            }
        }

        private string DescribeRefs()
        {
            return $"tools={(toolManager != null ? "Y" : "N")} inv={(playerInventory != null ? "Y" : "N")} builder={(playerBuilder != null ? "Y" : "N")} ctor={(constructionManager != null ? "Y" : "N")} prov={(loadoutProvisioner != null ? "Y" : "N")} item={(salvageProbeItem != null ? salvageProbeItem.name : "N")}";
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
                Debug.Log($"[FieldToolSmoke] {message}");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AutoResolveSceneReferences();
            AutoResolveDefaultAssets();
        }

        private void AutoResolveDefaultAssets()
        {
            if (salvageProbeItem == null)
                salvageProbeItem = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Project/Data/Items/Data_Titanium.asset");
        }
#endif
    }
}
