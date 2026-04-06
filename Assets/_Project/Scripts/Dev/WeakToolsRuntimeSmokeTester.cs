// ============================================================================
// HECTON-8 — WeakToolsRuntimeSmokeTester.cs
// Dev-only runtime smoke pass for weaker tools: Beacon, Analyzer, Propulsion, Knife, Stun, Harpoon, Flashlight.
// ============================================================================

using System.Collections;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Hecton8.Dev
{
    /// <summary>Development tool for runtime smoke testing of weak tools (Beacon, Analyzer, Propulsion, Knife, Stun, Harpoon, Flashlight).</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Weak Tools Runtime Smoke Tester")]
    public sealed class WeakToolsRuntimeSmokeTester : MonoBehaviour
    {
        /// <summary>Reference to the player tool manager.</summary>
        [Header("References")]
        [SerializeField] private PlayerToolManager toolManager;

        /// <summary>Reference to the player inventory.</summary>
        [SerializeField] private PlayerInventory playerInventory;

        /// <summary>Whether to run the smoke test on Start.</summary>
        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;

        /// <summary>Whether to restore the original tool loadout after testing.</summary>
        [SerializeField] private bool restoreOriginalLoadout = true;

        /// <summary>Delay before starting the smoke test.</summary>
        [SerializeField] private float startupDelay = 0.75f;

        /// <summary>Timeout for tool equipping operations.</summary>
        [SerializeField] private float equipTimeout = 1.25f;

        /// <summary>Delay after tool operations to let things settle.</summary>
        [SerializeField] private float settleDelay = 0.2f;

        /// <summary>Delay between testing different tools.</summary>
        [SerializeField] private float betweenToolsDelay = 0.1f;

        /// <summary>Simulated delta time for yielding in coroutines.</summary>
        [SerializeField] private float simulatedDeltaTime = 0.1f;

        /// <summary>Whether to enable verbose logging during testing.</summary>
        [SerializeField] private bool verboseLogging = false;

        /// <summary>Beacon tool prefab for testing.</summary>
        [Header("Weak Tool Set")]
        [SerializeField] private GameObject beaconToolPrefab;

        /// <summary>Analyzer tool prefab for testing.</summary>
        [SerializeField] private GameObject analyzerToolPrefab;

        /// <summary>Propulsion tool prefab for testing.</summary>
        [SerializeField] private GameObject propulsionToolPrefab;

        /// <summary>Knife tool prefab for testing.</summary>
        [SerializeField] private GameObject knifeToolPrefab;

        /// <summary>Stun tool prefab for testing.</summary>
        [SerializeField] private GameObject stunToolPrefab;

        /// <summary>Harpoon tool prefab for testing.</summary>
        [SerializeField] private GameObject harpoonToolPrefab;

        /// <summary>Flashlight tool prefab for testing.</summary>
        [SerializeField] private GameObject flashlightToolPrefab;

        /// <summary>Number of tools that passed the smoke test.</summary>
        [Header("Diagnostics")]
        [SerializeField, ReadOnly] private int _debugPassCount;

        /// <summary>Number of tools that failed the smoke test.</summary>
        [SerializeField, ReadOnly] private int _debugFailCount;

        /// <summary>Last issue encountered during testing.</summary>
        [SerializeField, ReadOnly] private string _debugLastIssue = string.Empty;

        /// <summary>Name of the last tool tested.</summary>
        [SerializeField, ReadOnly] private string _debugLastToolName = "None";

        /// <summary>Whether the last tool test passed.</summary>
        [SerializeField, ReadOnly] private bool _debugLastPass;

        /// <summary>Whether the smoke test is currently running.</summary>
        private bool _isRunning;

        /// <summary>Gets whether the smoke test is currently running.</summary>
        public bool IsRunning => _isRunning;

        /// <summary>Gets the number of tools that passed.</summary>
        public int DebugPassCount => _debugPassCount;

        /// <summary>Gets the number of tools that failed.</summary>
        public int DebugFailCount => _debugFailCount;

        /// <summary>Gets the last issue encountered.</summary>
        public string DebugLastIssue => _debugLastIssue;

        /// <summary>Gets the name of the last tool tested.</summary>
        public string DebugLastToolName => _debugLastToolName;

        /// <summary>Gets whether the last tool test passed.</summary>
        public bool DebugLastPass => _debugLastPass;

        /// <summary>Initializes the component and auto-resolves scene references.</summary>
        private void Awake()
        {
            AutoResolveSceneReferences();
        }

        /// <summary>Starts the smoke test if configured to run on start.</summary>
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

        /// <summary>Runs the weak tools smoke test from context menu.</summary>
        [ContextMenu("Run Weak Tools Runtime Smoke Pass")]
        public void RunFromContextMenu()
        {
            if (_isRunning)
                return;

            StartCoroutine(RunSmokePass());
        }

        /// <summary>Automatically resolves scene references for tool manager and inventory.</summary>
        private void AutoResolveSceneReferences()
        {
            if (toolManager == null)
                toolManager = Object.FindAnyObjectByType<PlayerToolManager>(FindObjectsInactive.Include);

            if (playerInventory == null)
                playerInventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        }

        /// <summary>Runs the complete smoke test pass for all weak tools.</summary>
        /// <returns>Coroutine enumerator.</returns>
        private IEnumerator RunSmokePass()
        {
            _isRunning = true;
            _debugPassCount = 0;
            _debugFailCount = 0;
            _debugLastIssue = string.Empty;
            _debugLastToolName = "None";
            _debugLastPass = false;

            if (verboseLogging)
                Debug.Log("[WeakToolsSmoke] Starting weak tools smoke pass...");

            yield return new WaitForSeconds(startupDelay);

            // Backup original loadout
            GameObject[] originalLoadout = null;
            if (restoreOriginalLoadout && toolManager != null)
            {
                originalLoadout = new GameObject[4];
                for (int i = 0; i < 4; i++)
                {
                    originalLoadout[i] = toolManager.GetAssignedToolPrefab(i);
                }
            }

            // Define weak tools to test
            var weakTools = new (string name, GameObject prefab)[]
            {
                ("Beacon", beaconToolPrefab),
                ("Analyzer", analyzerToolPrefab),
                ("Propulsion", propulsionToolPrefab),
                ("Knife", knifeToolPrefab),
                ("Stun", stunToolPrefab),
                ("Harpoon", harpoonToolPrefab),
                ("Flashlight", flashlightToolPrefab)
            };

            foreach (var (toolName, toolPrefab) in weakTools)
            {
                _debugLastToolName = toolName;

                if (toolPrefab == null)
                {
                    _debugLastIssue = $"Tool prefab not assigned: {toolName}";
                    _debugFailCount++;
                    _debugLastPass = false;
                    if (verboseLogging)
                        Debug.LogWarning($"[WeakToolsSmoke] FAIL: {_debugLastIssue}");
                    continue;
                }

                bool pass = false;
                yield return StartCoroutine(TestTool(toolName, toolPrefab, result => pass = result));
                if (pass)
                {
                    _debugPassCount++;
                    _debugLastPass = true;
                    if (verboseLogging)
                        Debug.Log($"[WeakToolsSmoke] PASS: {toolName}");
                }
                else
                {
                    _debugFailCount++;
                    _debugLastPass = false;
                    if (verboseLogging)
                        Debug.LogWarning($"[WeakToolsSmoke] FAIL: {_debugLastIssue}");
                }

                yield return new WaitForSeconds(betweenToolsDelay);
            }

            // Restore original loadout
            if (restoreOriginalLoadout && originalLoadout != null && toolManager != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (originalLoadout[i] != null)
                        toolManager.SetAssignedToolPrefab(i, originalLoadout[i], holsterIfCurrentInvalid: false);
                }
            }

            _isRunning = false;

            if (verboseLogging)
                Debug.Log($"[WeakToolsSmoke] Completed. Passes: {_debugPassCount}, Fails: {_debugFailCount}");

            if (_debugFailCount == 0)
            {
                Debug.Log("[WeakToolsSmoke] ALL WEAK TOOLS PASSED SMOKE TEST");
            }
            else
            {
                Debug.LogWarning($"[WeakToolsSmoke] {_debugFailCount} WEAK TOOLS FAILED SMOKE TEST");
            }
        }

        /// <summary>Tests a single tool by equipping it and invoking its actions.</summary>
        /// <param name="toolName">Name of the tool being tested.</param>
        /// <param name="toolPrefab">Prefab of the tool to test.</param>
        /// <param name="onComplete">Callback invoked with test result (true = pass, false = fail).</param>
        /// <returns>Coroutine enumerator.</returns>
        private IEnumerator TestTool(string toolName, GameObject toolPrefab, System.Action<bool> onComplete)
        {
            if (toolManager == null)
            {
                _debugLastIssue = "ToolManager not found";
                onComplete(false);
                yield break;
            }

            // Temporarily assign to slot 0
            GameObject originalSlot0 = toolManager.GetAssignedToolPrefab(0);
            toolManager.SetAssignedToolPrefab(0, toolPrefab, holsterIfCurrentInvalid: false);

            yield return new WaitForSeconds(settleDelay);

            // Equip the tool
            toolManager.SwitchToSlot(0);
            float equipStartTime = Time.time;

            while ((toolManager.CurrentSlotIndex != 0 || toolManager.CurrentTool == null) && (Time.time - equipStartTime) < equipTimeout)
            {
                yield return new WaitForSeconds(simulatedDeltaTime);
            }

            if (toolManager.CurrentSlotIndex != 0 || toolManager.CurrentTool == null)
            {
                _debugLastIssue = $"Failed to equip {toolName} within {equipTimeout}s";
                toolManager.SetAssignedToolPrefab(0, originalSlot0, holsterIfCurrentInvalid: false);
                onComplete(false);
                yield break;
            }

            yield return new WaitForSeconds(settleDelay);

            // Get the equipped tool
            PlayerTool equippedTool = toolManager.CurrentTool;
            if (equippedTool == null)
            {
                _debugLastIssue = $"No tool equipped after equipping {toolName}";
                toolManager.SetAssignedToolPrefab(0, originalSlot0, holsterIfCurrentInvalid: false);
                onComplete(false);
                yield break;
            }

            // Test primary action through the current PlayerTool contract.
            try
            {
                equippedTool.UsePrimary(simulatedDeltaTime);
                if (verboseLogging)
                    Debug.Log($"[WeakToolsSmoke] {toolName} primary action invoked");
            }
            catch (System.Exception e)
            {
                _debugLastIssue = $"{toolName} primary action threw: {e.Message}";
                toolManager.SetAssignedToolPrefab(0, originalSlot0, holsterIfCurrentInvalid: false);
                onComplete(false);
                yield break;
            }

            yield return new WaitForSeconds(settleDelay);

            // Test secondary action through the current PlayerTool contract.
            try
            {
                equippedTool.UseSecondary(simulatedDeltaTime);
                if (verboseLogging)
                    Debug.Log($"[WeakToolsSmoke] {toolName} secondary action invoked");
            }
            catch (System.Exception e)
            {
                _debugLastIssue = $"{toolName} secondary action threw: {e.Message}";
                toolManager.SetAssignedToolPrefab(0, originalSlot0, holsterIfCurrentInvalid: false);
                onComplete(false);
                yield break;
            }

            yield return new WaitForSeconds(settleDelay);

            // Holster the tool
            toolManager.Holster();

            yield return new WaitForSeconds(settleDelay);

            // Restore original slot
            toolManager.SetAssignedToolPrefab(0, originalSlot0, holsterIfCurrentInvalid: false);

            onComplete(true);
        }
    }
}
