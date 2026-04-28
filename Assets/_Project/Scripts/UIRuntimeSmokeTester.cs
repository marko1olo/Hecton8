// ============================================================================
// HECTON-8 - UIRuntimeSmokeTester.cs
// Dev-only runtime smoke coverage for PDA, pause menu and builder handoff UI.
// Verifies shell open/close, tab switching and construction tab -> builder flow.
// ============================================================================

using System.Collections;
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
            StartCoroutine(RunSmokePass());
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

            StartCoroutine(RunSmokePass());
        }

        private IEnumerator RunSmokePass()
        {
            if (_isRunning)
                yield break;

            AutoResolve();
            LogVerbose($"RUN begin refs={DescribeRefs()}");
            if (playerPDA == null || pauseMenu == null)
            {
                Debug.LogWarning("[UISmoke] Missing PlayerPDA or PauseMenuController.");
                yield break;
            }

            _isRunning = true;

            if (startupDelay > 0f)
                yield return new WaitForSecondsRealtime(startupDelay);

            Debug.Log("[UISmoke] Starting UI runtime smoke pass.");

            bool pdaOk = false;
            bool pauseOk = false;
            bool builderOk = false;

            try
            {
                LogVerbose("STEP open PDA inventory");
                pdaOk = false;
                playerPDA.Open(0);
                yield return WaitUntil(() => PlayerPDA.IsOpen && playerPDA.ActiveTab == 0, actionTimeout, "PDA open Inventory");
                pdaOk = PlayerPDA.IsOpen && playerPDA.ActiveTab == 0;

                if (pdaOk)
                {
                    LogVerbose("STEP set PDA loadout");
                    playerPDA.SetActiveTab(1);
                    yield return WaitUntil(() => playerPDA.ActiveTab == 1, actionTimeout, "PDA tab Loadout");
                    pdaOk &= playerPDA.ActiveTab == 1;

                    LogVerbose("STEP set PDA construction");
                    playerPDA.SetActiveTab(2);
                    yield return WaitUntil(() => playerPDA.ActiveTab == 2, actionTimeout, "PDA tab Construction");
                    pdaOk &= playerPDA.ActiveTab == 2;

                    LogVerbose("STEP set PDA barter");
                    playerPDA.SetActiveTab(3);
                    yield return WaitUntil(() => playerPDA.ActiveTab == 3, actionTimeout, "PDA tab Barter");
                    pdaOk &= playerPDA.ActiveTab == 3;

                    LogVerbose("STEP set PDA datalog");
                    playerPDA.SetActiveTab(4);
                    yield return WaitUntil(() => playerPDA.ActiveTab == 4, actionTimeout, "PDA tab DataLog");
                    pdaOk &= playerPDA.ActiveTab == 4;

                    LogVerbose("STEP close PDA");
                    playerPDA.Close();
                    yield return WaitUntil(() => !PlayerPDA.IsOpen, actionTimeout, "PDA close");
                    pdaOk &= !PlayerPDA.IsOpen;
                }

                LogVerbose("STEP open pause");
                pauseMenu.Open();
                yield return WaitUntil(() => pauseMenu.IsOpen, actionTimeout, "Pause open");
                pauseOk = pauseMenu.IsOpen;

                LogVerbose("STEP close pause");
                pauseMenu.Close();
                yield return WaitUntil(() => !pauseMenu.IsOpen, actionTimeout, "Pause close");
                pauseOk &= !pauseMenu.IsOpen;

                builderOk = true;
                if (constructionTab != null && toolManager != null)
                {
                    LogVerbose("STEP holster tools before builder handoff");
                    toolManager.Holster();
                    yield return WaitUntil(
                        () => !toolManager.IsSwapping && toolManager.CurrentTool == null && toolManager.CurrentSlotIndex < 0,
                        actionTimeout,
                        "Tool holster before builder handoff");

                    LogVerbose("STEP open PDA construction");
                    playerPDA.Open(2);
                    yield return WaitUntil(() => PlayerPDA.IsOpen && playerPDA.ActiveTab == 2, actionTimeout, "Open construction tab");

                    LogVerbose("STEP invoke builder action");
                    constructionTab.InvokeBuilderAction();
                    yield return new WaitForSecondsRealtime(settleDelay);

                    int builderSlot = toolManager.FindAssignedSlotForToolType<BuilderTool>();
                    builderOk &= builderSlot >= 0;
                    if (builderSlot >= 0 && toolManager.IsToolAvailableInSlot(builderSlot))
                    {
                        LogVerbose("STEP activate builder via construction tab");
                        constructionTab.InvokeBuilderAction();
                        yield return WaitUntil(
                            () => toolManager.CurrentTool is BuilderTool && toolManager.CurrentSlotIndex == builderSlot,
                            actionTimeout,
                            "Activate builder from construction tab");

                        builderOk &= toolManager.CurrentTool is BuilderTool && toolManager.CurrentSlotIndex == builderSlot;

                        LogVerbose("STEP invoke field action");
                        constructionTab.InvokeFieldAction();
                        yield return new WaitForSecondsRealtime(settleDelay);
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
            }
            finally
            {
                playerPDA?.ForceClose();
                pauseMenu?.Close();
                if (toolManager != null)
                    toolManager.Holster();

                _isRunning = false;
            }

            Debug.Log($"[UISmoke] COMPLETE pda={pdaOk} pause={pauseOk} builder={builderOk}");
        }

        private IEnumerator WaitUntil(System.Func<bool> predicate, float timeout, string label)
        {
            float startedAt = Time.realtimeSinceStartup;
            float deadline = startedAt + Mathf.Max(0.01f, timeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                bool success = false;
                try
                {
                    success = predicate();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[UISmoke] EXCEPTION {label}: {ex}");
                    yield break;
                }

                if (success)
                {
                    LogVerbose($"PASS {label}");
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning($"[UISmoke] TIMEOUT {label} after {timeout:0.00}s");
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
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < all.Length; i++)
            {
                T candidate = all[i];
                if (candidate == null)
                    continue;

                GameObject go = candidate.gameObject;
                if (go == null)
                    continue;

                Scene scene = go.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                return candidate;
            }

            return null;
        }

        private static GameObject FindSceneGameObjectIncludingInactive(string name)
        {
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject candidate = all[i];
                if (candidate == null || !string.Equals(candidate.name, name, System.StringComparison.Ordinal))
                    continue;

                Scene scene = candidate.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                return candidate;
            }

            return null;
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
                Debug.Log("[UISmoke] " + message);
        }
    }
}
