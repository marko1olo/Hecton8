// ============================================================================
// HECTON-8 - ToolRuntimeSmokeTester.cs
// Dev-only runtime smoke pass for the full held-tool set.
// Temporarily remaps slot 0, equips each tool, invokes primary/secondary,
// then restores the original quick-slot assignments.
// ============================================================================

using System.Collections;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Tool Runtime Smoke Tester")]
    public sealed class ToolRuntimeSmokeTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private PlayerInventory playerInventory;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool restoreOriginalLoadout = true;
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private float equipTimeout = 1.25f;
        [SerializeField] private float settleDelay = 0.2f;
        [SerializeField] private float betweenToolsDelay = 0.1f;
        [SerializeField] private float simulatedDeltaTime = 0.1f;
        [SerializeField] private bool verboseLogging = true;

        [Header("Tool Set")]
        [SerializeField] private GameObject[] heldToolPrefabs = new GameObject[12];

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

            StartCoroutine(RunSmokePass());
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoResolveSceneReferences();
            AutoResolveDefaultAssets();
        }
#endif

        private IEnumerator RunSmokePass()
        {
            if (_isRunning)
                yield break;

            AutoResolveSceneReferences();
            if (toolManager == null || playerInventory == null)
            {
                Debug.LogWarning("[ToolSmoke] Missing PlayerToolManager or PlayerInventory.");
                yield break;
            }

            _isRunning = true;

            if (startupDelay > 0f)
                yield return new WaitForSecondsRealtime(startupDelay);

            GameObject[] originalAssignments = new GameObject[toolManager.SlotCount];
            for (int i = 0; i < originalAssignments.Length; i++)
                originalAssignments[i] = toolManager.GetAssignedToolPrefab(i);

            int originalSlot = toolManager.CurrentSlotIndex;

            int passed = 0;
            int failed = 0;

            Debug.Log("[ToolSmoke] Starting runtime smoke pass.");

            for (int i = 0; i < heldToolPrefabs.Length; i++)
            {
                GameObject prefab = heldToolPrefabs[i];
                if (prefab == null)
                    continue;

                string toolName = prefab.name;
                if (!prefab.TryGetComponent(out PlayerTool prefabTool) || prefabTool.ToolData == null)
                {
                    Debug.LogWarning($"[ToolSmoke] SKIP {toolName}: missing PlayerTool or ToolData.");
                    continue;
                }

                LogVerbose($"BEGIN {toolName}");

                bool setupFailed = false;

                LogVerbose($"HOLSTER {toolName}");
                toolManager.Holster();
                float holsterElapsed = 0f;
                while (holsterElapsed < equipTimeout)
                {
                    if (!toolManager.IsSwapping &&
                        toolManager.CurrentTool == null &&
                        toolManager.CurrentSlotIndex < 0)
                        break;

                    holsterElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (holsterElapsed >= equipTimeout &&
                    (toolManager.IsSwapping || toolManager.CurrentTool != null || toolManager.CurrentSlotIndex >= 0))
                {
                    Debug.LogWarning(
                        $"[ToolSmoke] HOLSTER WAIT TIMEOUT slot={toolManager.CurrentSlotIndex} " +
                        $"tool={(toolManager.CurrentTool != null ? toolManager.CurrentTool.GetType().Name : "null")} " +
                        $"swapping={toolManager.IsSwapping}");
                }

                try
                {
                    if (!playerInventory.ContainsItem(prefabTool.ToolData))
                        playerInventory.TryAddItem(prefabTool.ToolData, 1);

                    LogVerbose($"ASSIGN {toolName}");
                    toolManager.SetAssignedToolPrefab(0, prefab, holsterIfCurrentInvalid: false);
                    LogVerbose($"SWITCH {toolName}");
                    toolManager.SwitchToSlot(0);
                    LogVerbose($"REQUESTED equip {toolName}");
                }
                catch (System.Exception ex)
                {
                    failed++;
                    setupFailed = true;
                    Debug.LogError($"[ToolSmoke] SETUP EXCEPTION {toolName}: {ex}");
                }

                if (setupFailed)
                    continue;

                float elapsed = 0f;
                while (elapsed < equipTimeout)
                {
                    PlayerTool currentTool = toolManager.CurrentTool;
                    if (toolManager.CurrentSlotIndex == 0 &&
                        currentTool != null &&
                        ReferenceEquals(currentTool.ToolData, prefabTool.ToolData) &&
                        !toolManager.IsSwapping)
                        break;

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                PlayerTool liveTool = toolManager.CurrentTool;
                if (liveTool == null || !ReferenceEquals(liveTool.ToolData, prefabTool.ToolData))
                {
                    failed++;
                    Debug.LogWarning(
                        $"[ToolSmoke] FAIL {toolName}: equip timeout/mismatch. " +
                        $"live={(liveTool != null ? liveTool.GetType().Name : "null")}, " +
                        $"slot={toolManager.CurrentSlotIndex}, swapping={toolManager.IsSwapping}");
                    continue;
                }

                LogVerbose(
                    $"EQUIPPED {toolName} -> live={liveTool.GetType().Name}, slot={toolManager.CurrentSlotIndex}, swapping={toolManager.IsSwapping}");

                yield return new WaitForSecondsRealtime(settleDelay);
                LogVerbose($"SETTLED {toolName}");

                bool stepPassed = RunToolInvocation(toolName, liveTool);
                if (stepPassed)
                    passed++;
                else
                    failed++;

                yield return new WaitForSecondsRealtime(betweenToolsDelay);
            }

            if (restoreOriginalLoadout)
            {
                toolManager.Holster();
                float holsterElapsed = 0f;
                while (holsterElapsed < equipTimeout)
                {
                    if (!toolManager.IsSwapping &&
                        toolManager.CurrentTool == null &&
                        toolManager.CurrentSlotIndex < 0)
                        break;

                    holsterElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                for (int i = 0; i < originalAssignments.Length; i++)
                    toolManager.SetAssignedToolPrefab(i, originalAssignments[i], holsterIfCurrentInvalid: false);

                if (originalSlot >= 0 && originalSlot < originalAssignments.Length && originalAssignments[originalSlot] != null)
                {
                    toolManager.SwitchToSlot(originalSlot);
                    yield return null;
                }
            }

            Debug.Log($"[ToolSmoke] COMPLETE pass={passed} fail={failed}");
            _isRunning = false;
        }

        private bool RunToolInvocation(string toolName, PlayerTool liveTool)
        {
            try
            {
                LogVerbose($"PRIMARY {toolName}");
                liveTool.UsePrimary(simulatedDeltaTime);
                LogVerbose($"SECONDARY {toolName}");
                liveTool.UseSecondary(simulatedDeltaTime);
                Debug.Log($"[ToolSmoke] PASS {toolName} -> {liveTool.GetType().Name}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ToolSmoke] EXCEPTION {toolName}: {ex}");
                return false;
            }
        }

        private void AutoResolveSceneReferences()
        {
            if (toolManager == null)
                toolManager = FindFirstObjectByType<PlayerToolManager>();

            if (playerInventory == null)
                playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

#if UNITY_EDITOR
        private void AutoResolveDefaultAssets()
        {
            string[] paths =
            {
                "Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_Builder_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_LaserCutter_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_Propulsion_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_SalvageSampler_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_BeaconDeployer_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_EnvAnalyzer_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_Knife_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_StunPistol_Held.prefab",
                "Assets/_Project/Prefabs/Tools/Held/Tool_HarpoonLauncher_Held.prefab",
            };

            for (int i = 0; i < heldToolPrefabs.Length && i < paths.Length; i++)
            {
                if (heldToolPrefabs[i] != null)
                    continue;

                heldToolPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            }
        }
#endif

        private void LogVerbose(string message)
        {
            if (!verboseLogging)
                return;

            Debug.Log($"[ToolSmoke] {message}");
        }
    }
}
