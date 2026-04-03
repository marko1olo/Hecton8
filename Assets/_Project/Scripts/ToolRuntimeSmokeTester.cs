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
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

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
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private bool restoreOriginalLoadout = true;
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private float equipTimeout = 1.25f;
        [SerializeField] private float settleDelay = 0.2f;
        [SerializeField] private float betweenToolsDelay = 0.1f;
        [SerializeField] private float simulatedDeltaTime = 0.1f;
        [SerializeField] private bool verboseLogging = false;

        [Header("Tool Set")]
        [SerializeField] private GameObject[] heldToolPrefabs = new GameObject[12];

        [Header("Diagnostics")]
        [SerializeField] private int _debugPassCount;
        [SerializeField] private int _debugFailCount;
        [SerializeField] private string _debugLastIssue = string.Empty;
        [SerializeField] private string _debugLastToolName = "None";
        [SerializeField] private bool _debugLastPass;

        private bool _isRunning;

        public bool IsRunning => _isRunning;
        public int DebugPassCount => _debugPassCount;
        public int DebugFailCount => _debugFailCount;
        public string DebugLastIssue => _debugLastIssue;
        public string DebugLastToolName => _debugLastToolName;
        public bool DebugLastPass => _debugLastPass;

        private void Awake()
        {
            AutoResolveSceneReferences();
#if UNITY_EDITOR
            AutoResolveDefaultAssets();
#endif
        }

        private void Start()
        {
            if (gameObject.name.StartsWith("__DEV_", System.StringComparison.Ordinal))
            {
                runOnStart = false;
                return;
            }

            if (!runOnStart || _isRunning)
                return;

            StartCoroutine(RunSmokePass());
        }

        [ContextMenu("Run Tool Runtime Smoke Pass")]
        public void RunFromContextMenu()
        {
            if (_isRunning)
                return;

            StartCoroutine(RunSmokePass());
        }

        public void ConfigureForDevRun(
            bool enableVerboseLogging = true,
            bool restoreLoadoutAfterRun = true,
            float startupDelaySeconds = 0.35f,
            float equipTimeoutSeconds = 1.5f,
            float settleDelaySeconds = 0.15f,
            float betweenToolsDelaySeconds = 0.05f,
            float simulatedDeltaSeconds = 0.1f)
        {
            runOnStart = false;
            verboseLogging = enableVerboseLogging;
            restoreOriginalLoadout = restoreLoadoutAfterRun;
            startupDelay = Mathf.Clamp(startupDelaySeconds, 0f, 5f);
            equipTimeout = Mathf.Clamp(equipTimeoutSeconds, 0.25f, 10f);
            settleDelay = Mathf.Clamp(settleDelaySeconds, 0f, 3f);
            betweenToolsDelay = Mathf.Clamp(betweenToolsDelaySeconds, 0f, 3f);
            simulatedDeltaTime = Mathf.Clamp(simulatedDeltaSeconds, 0.01f, 1f);
            AutoResolveSceneReferences();
#if UNITY_EDITOR
            AutoResolveDefaultAssets();
#endif
        }

        public bool TryRunImmediately()
        {
            if (_isRunning)
                return false;

            AutoResolveSceneReferences();
#if UNITY_EDITOR
            AutoResolveDefaultAssets();
#endif
            StartCoroutine(RunSmokePass());
            return true;
        }

        public string DescribeStatus()
        {
            string issue = string.IsNullOrWhiteSpace(_debugLastIssue) ? "none" : _debugLastIssue;
            return
                $"running={_isRunning} pass={_debugPassCount} fail={_debugFailCount} " +
                $"lastTool={_debugLastToolName} lastPass={_debugLastPass} issue={issue}";
        }

        private IEnumerator RunSmokePass()
        {
            if (_isRunning)
                yield break;

            AutoResolveSceneReferences();
            if (toolManager == null || playerInventory == null)
            {
                _debugLastIssue = "Missing PlayerToolManager or PlayerInventory.";
                Debug.LogWarning("[ToolSmoke] Missing PlayerToolManager or PlayerInventory.");
                yield break;
            }

            _isRunning = true;
            _debugPassCount = 0;
            _debugFailCount = 0;
            _debugLastIssue = string.Empty;
            _debugLastToolName = "None";
            _debugLastPass = false;

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
                _debugLastToolName = toolName;
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
                    _debugFailCount++;
                    _debugLastIssue = "Setup exception for " + toolName;
                    _debugLastPass = false;
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
                    _debugFailCount++;
                    _debugLastIssue = "Equip timeout/mismatch for " + toolName;
                    _debugLastPass = false;
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
                {
                    passed++;
                    _debugPassCount++;
                    _debugLastIssue = string.Empty;
                    _debugLastPass = true;
                }
                else
                {
                    failed++;
                    _debugFailCount++;
                    _debugLastIssue = "Invocation failed for " + toolName;
                    _debugLastPass = false;
                }

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
                toolManager = FindAnyObjectByType<PlayerToolManager>();

            if (playerInventory == null)
                playerInventory = FindAnyObjectByType<PlayerInventory>();
        }

        private void LogVerbose(string message)
        {
            if (!verboseLogging)
                return;

            Debug.Log($"[ToolSmoke] {message}");
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
    }
}
