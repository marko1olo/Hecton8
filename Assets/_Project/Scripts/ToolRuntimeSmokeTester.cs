// ============================================================================
// HECTON-8 - ToolRuntimeSmokeTester.cs
// Dev-only runtime smoke pass for the full held-tool set.
// Temporarily remaps slot 0, equips each tool, invokes primary/secondary,
// then restores the original quick-slot assignments.
// ============================================================================

using System;
using System.Threading;
using Hecton8.Core;
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
        [SerializeField] private GameObject[] heldToolPrefabs = new GameObject[13];

        [Header("Diagnostics")]
        [SerializeField] private int _debugPassCount;
        [SerializeField] private int _debugFailCount;
        [SerializeField] private string _debugLastIssue = string.Empty;
        [SerializeField] private string _debugLastToolName = "None";
        [SerializeField] private bool _debugLastPass;

        private PlayerTool[] _cachedTools;
        private bool _isRunning;
        private const float DefaultSmokeFrameDeltaSeconds = 1f / 60f;

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

            _ = RunSmokePassAsync(destroyCancellationToken);
        }

        [ContextMenu("Run Tool Runtime Smoke Pass")]
        public void RunFromContextMenu()
        {
            if (_isRunning)
                return;

            _ = RunSmokePassAsync(destroyCancellationToken);
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
            _ = RunSmokePassAsync(destroyCancellationToken);
            return true;
        }

        public string DescribeStatus()
        {
            string issue = string.IsNullOrWhiteSpace(_debugLastIssue) ? "none" : _debugLastIssue;
            return
                $"running={_isRunning} pass={_debugPassCount} fail={_debugFailCount} " +
                $"lastTool={_debugLastToolName} lastPass={_debugLastPass} issue={issue}";
        }

        private async Awaitable RunSmokePassAsync(CancellationToken cancellationToken)
        {
            if (_isRunning)
                return;

            AutoResolveSceneReferences();
            if (toolManager == null || playerInventory == null)
            {
                _debugLastIssue = "Missing PlayerToolManager or PlayerInventory.";
                Hecton8.Core.H8Debug.LogWarning("[ToolSmoke] Missing PlayerToolManager or PlayerInventory.");
                return;
            }

            if (_cachedTools == null || _cachedTools.Length != heldToolPrefabs.Length)
            {
                _cachedTools = new PlayerTool[heldToolPrefabs.Length];
                for (int i = 0; i < heldToolPrefabs.Length; i++)
                {
                    if (heldToolPrefabs[i] != null)
                        heldToolPrefabs[i].TryGetComponent(out _cachedTools[i]);
                }
            }

            _isRunning = true;
            try
            {
                _debugPassCount = 0;
                _debugFailCount = 0;
                _debugLastIssue = string.Empty;
                _debugLastToolName = "None";
                _debugLastPass = false;

                if (startupDelay > 0f)
                    await DelayRealtimeAsync(startupDelay, cancellationToken);

                GameObject[] originalAssignments = new GameObject[toolManager.SlotCount];
                for (int i = 0; i < originalAssignments.Length; i++)
                    originalAssignments[i] = toolManager.GetAssignedToolPrefab(i);

                int originalSlot = toolManager.CurrentSlotIndex;

                int passed = 0;
                int failed = 0;

                Hecton8.Core.H8Debug.Log("[ToolSmoke] Starting runtime smoke pass.");

                for (int i = 0; i < heldToolPrefabs.Length; i++)
                {
                    GameObject prefab = heldToolPrefabs[i];
                    if (prefab == null)
                        continue;

                    bool? result = await TestSingleToolAsync(prefab, _cachedTools[i], cancellationToken);
                    if (result == true)
                    {
                        passed++;
                    }
                    else if (result == false)
                    {
                        failed++;
                    }
                }

                if (restoreOriginalLoadout)
                {
                    await RestoreOriginalLoadoutAsync(originalAssignments, originalSlot, cancellationToken);
                }

                Hecton8.Core.H8Debug.Log($"[ToolSmoke] COMPLETE pass={passed} fail={failed}");
            }
            catch (OperationCanceledException)
            {
                _debugLastIssue = "Cancelled";
                LogVerbose("Cancelled.");
            }
            catch (Exception ex)
            {
                _debugFailCount++;
                _debugLastIssue = "Unhandled exception";
                _debugLastPass = false;
                Hecton8.Core.H8Debug.LogError($"[ToolSmoke] UNHANDLED EXCEPTION: {ex}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        private async Awaitable<bool?> TestSingleToolAsync(GameObject prefab, PlayerTool prefabTool, CancellationToken cancellationToken)
        {
            string toolName = prefab.name;
            _debugLastToolName = toolName;

            if (prefabTool == null || prefabTool.ToolData == null)
            {
                Hecton8.Core.H8Debug.LogWarning($"[ToolSmoke] SKIP {toolName}: missing PlayerTool or ToolData.");
                return null;
            }

            LogVerbose($"BEGIN {toolName}");
            LogVerbose($"HOLSTER {toolName}");

            await WaitForHolsterAsync(cancellationToken, warnOnTimeout: true);

            try
            {
                int toolHashId = ItemData.ResolvePersistentHashId(prefabTool.ToolData);
                if (toolHashId == 0)
                    throw new InvalidOperationException($"{toolName} ToolData has no valid persistent hash.");

                if (!playerInventory.ContainsItem(toolHashId))
                    playerInventory.TryAddItem(toolHashId, 1);

                LogVerbose($"ASSIGN {toolName}");
                toolManager.SetAssignedToolPrefab(0, prefab, holsterIfCurrentInvalid: false);
                LogVerbose($"SWITCH {toolName}");
                toolManager.SwitchToSlot(0);
                LogVerbose($"REQUESTED equip {toolName}");
            }
            catch (Exception ex)
            {
                _debugFailCount++;
                _debugLastIssue = "Setup exception for " + toolName;
                _debugLastPass = false;
                Hecton8.Core.H8Debug.LogError($"[ToolSmoke] SETUP EXCEPTION {toolName}: {ex}");
                return false;
            }

            await WaitForEquipAsync(prefabTool, cancellationToken);

            PlayerTool liveTool = toolManager.CurrentTool;
            if (liveTool == null || !ReferenceEquals(liveTool.ToolData, prefabTool.ToolData))
            {
                _debugFailCount++;
                _debugLastIssue = "Equip timeout/mismatch for " + toolName;
                _debugLastPass = false;
                Hecton8.Core.H8Debug.LogWarning(
                    $"[ToolSmoke] FAIL {toolName}: equip timeout/mismatch. " +
                    $"live={(liveTool != null ? liveTool.GetType().Name : "null")}, " +
                    $"slot={toolManager.CurrentSlotIndex}, swapping={toolManager.IsSwapping}");
                return false;
            }

            LogVerbose($"EQUIPPED {toolName} -> live={liveTool.GetType().Name}, slot={toolManager.CurrentSlotIndex}, swapping={toolManager.IsSwapping}");

            await DelayRealtimeAsync(settleDelay, cancellationToken);
            LogVerbose($"SETTLED {toolName}");

            bool stepPassed = RunToolInvocation(toolName, liveTool);
            if (stepPassed)
            {
                _debugPassCount++;
                _debugLastIssue = string.Empty;
                _debugLastPass = true;
            }
            else
            {
                _debugFailCount++;
                _debugLastIssue = "Invocation failed for " + toolName;
                _debugLastPass = false;
            }

            await DelayRealtimeAsync(betweenToolsDelay, cancellationToken);
            return stepPassed;
        }

        private async Awaitable WaitForHolsterAsync(CancellationToken cancellationToken, bool warnOnTimeout)
        {
            toolManager.Holster();
            float holsterElapsed = 0f;
            while (holsterElapsed < equipTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!toolManager.IsSwapping &&
                    toolManager.CurrentTool == null &&
                    toolManager.CurrentSlotIndex < 0)
                    break;

                holsterElapsed += ResolveSmokeFrameDeltaSeconds();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }

            if (warnOnTimeout && holsterElapsed >= equipTimeout &&
                (toolManager.IsSwapping || toolManager.CurrentTool != null || toolManager.CurrentSlotIndex >= 0))
            {
                Hecton8.Core.H8Debug.LogWarning(
                    $"[ToolSmoke] HOLSTER WAIT TIMEOUT slot={toolManager.CurrentSlotIndex} " +
                    $"tool={(toolManager.CurrentTool != null ? toolManager.CurrentTool.GetType().Name : "null")} " +
                    $"swapping={toolManager.IsSwapping}");
            }
        }

        private async Awaitable WaitForEquipAsync(PlayerTool prefabTool, CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            while (elapsed < equipTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlayerTool currentTool = toolManager.CurrentTool;
                if (toolManager.CurrentSlotIndex == 0 &&
                    currentTool != null &&
                    ReferenceEquals(currentTool.ToolData, prefabTool.ToolData) &&
                    !toolManager.IsSwapping)
                    break;

                elapsed += ResolveSmokeFrameDeltaSeconds();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }
        }

        private async Awaitable RestoreOriginalLoadoutAsync(GameObject[] originalAssignments, int originalSlot, CancellationToken cancellationToken)
        {
            await WaitForHolsterAsync(cancellationToken, warnOnTimeout: false);

            for (int i = 0; i < originalAssignments.Length; i++)
                toolManager.SetAssignedToolPrefab(i, originalAssignments[i], holsterIfCurrentInvalid: false);

            if (originalSlot >= 0 && originalSlot < originalAssignments.Length && originalAssignments[originalSlot] != null)
            {
                toolManager.SwitchToSlot(originalSlot);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }
        }

        private static async Awaitable DelayRealtimeAsync(float seconds, CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0f, seconds);
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += ResolveSmokeFrameDeltaSeconds();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }
        }

        private static float ResolveSmokeFrameDeltaSeconds()
        {
            float deltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            return float.IsFinite(deltaTime) && deltaTime > 0f ? deltaTime : DefaultSmokeFrameDeltaSeconds;
        }

        private bool RunToolInvocation(string toolName, PlayerTool liveTool)
        {
            if (liveTool == null)
            {
                Hecton8.Core.H8Debug.LogError($"[ToolSmoke] EXCEPTION {toolName}: liveTool is null");
                return false;
            }

            try
            {
                LogVerbose($"PRIMARY {toolName}");
                liveTool.UsePrimary(simulatedDeltaTime);
                LogVerbose($"SECONDARY {toolName}");
                liveTool.UseSecondary(simulatedDeltaTime);
                Hecton8.Core.H8Debug.Log($"[ToolSmoke] PASS {toolName} -> {liveTool.GetType().Name}");
                return true;
            }
            catch (System.Exception ex)
            {
                Hecton8.Core.H8Debug.LogError($"[ToolSmoke] EXCEPTION {toolName}: {ex}");
                return false;
            }
        }

        private void AutoResolveSceneReferences()
        {
            if (toolManager == null)
                toolManager = (Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.ToolManager : null);

            if (playerInventory == null)
                playerInventory = (Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.Inventory : null);
        }

        private void LogVerbose(string message)
        {
            if (!verboseLogging)
                return;

            Hecton8.Core.H8Debug.Log($"[ToolSmoke] {message}");
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
                "Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab",
            };

            if (heldToolPrefabs == null || heldToolPrefabs.Length != paths.Length)
                Array.Resize(ref heldToolPrefabs, paths.Length);

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
