// ============================================================================
// HECTON-8 - UIRuntimeSmokeTester.cs
// Dev-only runtime smoke coverage for PDA, pause menu and builder handoff UI.
// Verifies shell open/close, tab switching and construction tab -> builder flow.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Gameplay;
using Hecton8.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/UI Runtime Smoke Tester")]
    public sealed class UIRuntimeSmokeTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private PauseMenuController pauseMenu;
        [SerializeField] private PDAConstructionTab constructionTab;
        [SerializeField] private PlayerToolManager toolManager;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private float startupDelay = 0.5f;
        [SerializeField] private float actionTimeout = 1.25f;
        [SerializeField] private float settleDelay = 0.1f;
        [SerializeField] private bool verboseLogging = false;

        // COLD ALLOC: List<GameObject>[512] - loaded-scene root traversal scratch for UI smoke reference resolution - owner: UIRuntimeSmokeTester
        private static readonly List<GameObject> _sceneRootScratch = new List<GameObject>(512);

        private bool _isRunning;

        private void Awake()
        {
            AutoResolve();
            LogVerbose($"AWAKE runOnStart={runOnStart} refs={DescribeRefs()}");
        }

        private void OnEnable()
        {
            LogVerbose($"ON_ENABLE runOnStart={runOnStart} isRunning={_isRunning}");
        }

        private void Start()
        {
            LogVerbose($"START runOnStart={runOnStart} isRunning={_isRunning} refs={DescribeRefs()}");
            if (!runOnStart || _isRunning)
                return;

            LogVerbose("START scheduling UI smoke pass.");
            _ = RunSmokePassAsync(destroyCancellationToken);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AutoResolve();
        }
#endif

        [ContextMenu("Run UI Runtime Smoke Pass")]
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

            AutoResolve();
            LogVerbose($"RUN begin refs={DescribeRefs()}");
            if (playerPDA == null || pauseMenu == null)
            {
                Debug.LogWarning("[UISmoke] Missing PlayerPDA or PauseMenuController.");
                return;
            }

            _isRunning = true;
            bool pdaOk = false;
            bool pauseOk = false;
            bool builderOk = false;
            bool completed = false;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (startupDelay > 0f)
                    await DelayRealtimeAsync(startupDelay, cancellationToken);

                Debug.Log("[UISmoke] Starting UI runtime smoke pass.");

                LogVerbose("STEP open PDA inventory");
                pdaOk = false;
                playerPDA.Open(0);
                await WaitUntilAsync(() => PlayerPDA.IsOpen && playerPDA.ActiveTab == 0, actionTimeout, "PDA open Inventory", cancellationToken);
                pdaOk = PlayerPDA.IsOpen && playerPDA.ActiveTab == 0;

                if (pdaOk)
                {
                    LogVerbose("STEP set PDA loadout");
                    playerPDA.SetActiveTab(1);
                    await WaitUntilAsync(() => playerPDA.ActiveTab == 1, actionTimeout, "PDA tab Loadout", cancellationToken);
                    pdaOk &= playerPDA.ActiveTab == 1;

                    LogVerbose("STEP set PDA construction");
                    playerPDA.SetActiveTab(2);
                    await WaitUntilAsync(() => playerPDA.ActiveTab == 2, actionTimeout, "PDA tab Construction", cancellationToken);
                    pdaOk &= playerPDA.ActiveTab == 2;

                    LogVerbose("STEP set PDA barter");
                    playerPDA.SetActiveTab(3);
                    await WaitUntilAsync(() => playerPDA.ActiveTab == 3, actionTimeout, "PDA tab Barter", cancellationToken);
                    pdaOk &= playerPDA.ActiveTab == 3;

                    LogVerbose("STEP set PDA datalog");
                    playerPDA.SetActiveTab(4);
                    await WaitUntilAsync(() => playerPDA.ActiveTab == 4, actionTimeout, "PDA tab DataLog", cancellationToken);
                    pdaOk &= playerPDA.ActiveTab == 4;

                    LogVerbose("STEP close PDA");
                    playerPDA.Close();
                    await WaitUntilAsync(() => !PlayerPDA.IsOpen, actionTimeout, "PDA close", cancellationToken);
                    pdaOk &= !PlayerPDA.IsOpen;
                }

                LogVerbose("STEP open pause");
                pauseMenu.Open();
                await WaitUntilAsync(() => pauseMenu.IsOpen, actionTimeout, "Pause open", cancellationToken);
                pauseOk = pauseMenu.IsOpen;

                LogVerbose("STEP close pause");
                pauseMenu.Close();
                await WaitUntilAsync(() => !pauseMenu.IsOpen, actionTimeout, "Pause close", cancellationToken);
                pauseOk &= !pauseMenu.IsOpen;

                builderOk = true;
                if (constructionTab != null && toolManager != null)
                {
                    LogVerbose("STEP holster tools before builder handoff");
                    toolManager.Holster();
                    await WaitUntilAsync(
                        () => !toolManager.IsSwapping && toolManager.CurrentTool == null && toolManager.CurrentSlotIndex < 0,
                        actionTimeout,
                        "Tool holster before builder handoff",
                        cancellationToken);

                    LogVerbose("STEP open PDA construction");
                    playerPDA.Open(2);
                    await WaitUntilAsync(() => PlayerPDA.IsOpen && playerPDA.ActiveTab == 2, actionTimeout, "Open construction tab", cancellationToken);

                    LogVerbose("STEP invoke builder action");
                    constructionTab.InvokeBuilderAction();
                    if (settleDelay > 0f)
                        await DelayRealtimeAsync(settleDelay, cancellationToken);

                    int builderSlot = toolManager.FindAssignedSlotForToolType<BuilderTool>();
                    builderOk &= builderSlot >= 0;
                    if (builderSlot >= 0 && toolManager.IsToolAvailableInSlot(builderSlot))
                    {
                        LogVerbose("STEP activate builder via construction tab");
                        constructionTab.InvokeBuilderAction();
                        await WaitUntilAsync(
                            () => toolManager.CurrentTool is BuilderTool && toolManager.CurrentSlotIndex == builderSlot,
                            actionTimeout,
                            "Activate builder from construction tab",
                            cancellationToken);

                        builderOk &= toolManager.CurrentTool is BuilderTool && toolManager.CurrentSlotIndex == builderSlot;

                        LogVerbose("STEP invoke field action");
                        constructionTab.InvokeFieldAction();
                        if (settleDelay > 0f)
                            await DelayRealtimeAsync(settleDelay, cancellationToken);
                        builderOk &= !PlayerPDA.IsOpen;
                    }
                    else
                    {
                        LogVerbose("Builder handoff stopped after arm step because builder is not available in cargo.");
                    }

                    playerPDA.ForceClose();
                    pauseMenu.Close();
                    toolManager.Holster();
                }
                else
                {
                    builderOk = false;
                    Debug.LogWarning("[UISmoke] Skipping builder handoff smoke: missing PDAConstructionTab or PlayerToolManager.");
                }

                completed = true;
            }
            catch (OperationCanceledException)
            {
                LogVerbose("UI smoke pass cancelled.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UISmoke] EXCEPTION smoke pass: {exception.Message}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
            finally
            {
                playerPDA?.ForceClose();
                pauseMenu?.Close();
                if (toolManager != null)
                    toolManager.Holster();

                _isRunning = false;
            }

            if (completed)
                Debug.Log($"[UISmoke] COMPLETE pda={pdaOk} pause={pauseOk} builder={builderOk}");
        }

        private async Awaitable<bool> WaitUntilAsync(Func<bool> predicate, float timeout, string label, CancellationToken cancellationToken)
        {
            float startedAt = Time.realtimeSinceStartup;
            float deadline = startedAt + Mathf.Max(0.01f, timeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool success = false;
                try
                {
                    success = predicate();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[UISmoke] EXCEPTION {label}: {ex}");
                    return false;
                }

                if (success)
                {
                    LogVerbose($"PASS {label}");
                    return true;
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }

            Debug.LogWarning($"[UISmoke] TIMEOUT {label} after {timeout:0.00}s");
            return false;
        }

        private static async Awaitable DelayRealtimeAsync(float duration, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, duration);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }
        }

        private void AutoResolve()
        {
            if (playerPDA == null)
                playerPDA = FindSceneObjectIncludingInactive<PlayerPDA>();
            if (pauseMenu == null)
            {
                pauseMenu = PauseMenuController.ActiveRuntimeInstance;
                if (pauseMenu == null)
                    pauseMenu = FindSceneObjectIncludingInactive<PauseMenuController>();
                if (pauseMenu == null)
                {
                    PauseMenuHost host = FindSceneObjectIncludingInactive<PauseMenuHost>();
                    if (host != null)
                        pauseMenu = host.GetComponent<PauseMenuController>();
                }

                if (pauseMenu == null)
                {
                    GameObject pauseRoot = FindSceneGameObjectIncludingInactive("PauseMenu_Root");
                    if (pauseRoot != null)
                        pauseMenu = pauseRoot.GetComponent<PauseMenuController>();
                }
            }
            if (constructionTab == null)
                constructionTab = FindSceneObjectIncludingInactive<PDAConstructionTab>();
            if (toolManager == null)
                toolManager = FindSceneObjectIncludingInactive<PlayerToolManager>();
        }

        private string DescribeRefs()
        {
            return $"pda={(playerPDA != null ? "Y" : "N")} pause={(pauseMenu != null ? "Y" : "N")} ctorTab={(constructionTab != null ? "Y" : "N")} tools={(toolManager != null ? "Y" : "N")}";
        }

        private static T FindSceneObjectIncludingInactive<T>() where T : Component
        {
            _sceneRootScratch.Clear();

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                scene.GetRootGameObjects(_sceneRootScratch);
                for (int rootIndex = 0; rootIndex < _sceneRootScratch.Count; rootIndex++)
                {
                    GameObject root = _sceneRootScratch[rootIndex];
                    if (root == null)
                        continue;

                    T candidate = FindComponentInChildrenIncludingInactive<T>(root.transform);
                    if (candidate != null)
                    {
                        _sceneRootScratch.Clear();
                        return candidate;
                    }
                }

                _sceneRootScratch.Clear();
            }

            return null;
        }

        private static GameObject FindSceneGameObjectIncludingInactive(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            _sceneRootScratch.Clear();

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                scene.GetRootGameObjects(_sceneRootScratch);
                for (int rootIndex = 0; rootIndex < _sceneRootScratch.Count; rootIndex++)
                {
                    GameObject root = _sceneRootScratch[rootIndex];
                    GameObject candidate = FindGameObjectInChildrenIncludingInactive(root != null ? root.transform : null, name);
                    if (candidate != null)
                    {
                        _sceneRootScratch.Clear();
                        return candidate;
                    }
                }

                _sceneRootScratch.Clear();
            }

            return null;
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
                Debug.Log("[UISmoke] " + message);
        }

        private static T FindComponentInChildrenIncludingInactive<T>(Transform root) where T : Component
        {
            if (root == null)
                return null;

            if (root.TryGetComponent(out T candidate))
                return candidate;

            for (int i = 0; i < root.childCount; i++)
            {
                T match = FindComponentInChildrenIncludingInactive<T>(root.GetChild(i));
                if (match != null)
                    return match;
            }

            return null;
        }

        private static GameObject FindGameObjectInChildrenIncludingInactive(Transform root, string name)
        {
            if (root == null)
                return null;

            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root.gameObject;

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject match = FindGameObjectInChildrenIncludingInactive(root.GetChild(i), name);
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
