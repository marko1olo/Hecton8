using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Dev;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Scan Runtime Smoke Tester")]
    public sealed class ScanRuntimeSmokeTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerToolManager toolManager;
        [SerializeField] private ScanLogSystem scanLogSystem;
        [SerializeField] private ScannableTarget probeTarget;
        [SerializeField] private Transform playerRoot;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private float actionTimeout = 1.5f;
        [SerializeField] private float settleDelay = 0.15f;
        [SerializeField] private bool verboseLogging = false;

        // COLD ALLOC: List<GameObject>[512] - loaded-scene root traversal scratch for scan smoke reference resolution - owner: ScanRuntimeSmokeTester
        private static readonly List<GameObject> _sceneRootScratch = new List<GameObject>(512);

        private bool _isRunning;

        private void Awake()
        {
            AutoResolve();
        }

        private void Start()
        {
            if (!runOnStart || _isRunning)
                return;

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

        [ContextMenu("Run Scan Runtime Smoke Pass")]
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
            if (toolManager == null || scanLogSystem == null || probeTarget == null || playerRoot == null)
            {
                Debug.LogWarning($"[ScanSmoke] Missing references refs={DescribeRefs()}");
                return;
            }

            _isRunning = true;
            if (startupDelay > 0f)
                await DelayRealtimeAsync(startupDelay, cancellationToken);

            if (cancellationToken.IsCancellationRequested || this == null)
            {
                _isRunning = false;
                return;
            }

            Debug.Log("[ScanSmoke] Starting scan runtime smoke pass.");

            string originalId = probeTarget.EntryId;
            string originalTitle = probeTarget.EntryTitle;
            string originalCategory = probeTarget.EntryCategory;
            string originalSummary = probeTarget.EntrySummary;
            Vector3 originalPosition = probeTarget.transform.position;
            Quaternion originalRotation = probeTarget.transform.rotation;
            bool originalActive = probeTarget.gameObject.activeSelf;

            try
            {
                string probeId = $"scan.smoke.{SceneManager.GetActiveScene().name.ToLowerInvariant()}.{Time.frameCount}";
                probeTarget.Configure(
                    probeId,
                    "SCAN SMOKE PROBE",
                    "Diagnostics",
                    "Temporary authored scan probe used by runtime smoke validation.");
                if (!probeTarget.gameObject.activeSelf)
                    probeTarget.gameObject.SetActive(true);

                Vector3 targetPosition = playerRoot.position + playerRoot.forward * 3.5f;
                probeTarget.transform.SetPositionAndRotation(targetPosition, Quaternion.identity);

                int scannerSlot = toolManager.FindAssignedSlotForToolType<ScannerTool>();
                if (scannerSlot < 0)
                {
                    Debug.LogWarning("[ScanSmoke] ScannerTool is not assigned to any quick slot.");
                    return;
                }

                LogVerbose($"ARM slot={scannerSlot}");
                toolManager.SwitchToSlot(scannerSlot);
                bool equipped = await WaitUntilAsync(
                    () => !toolManager.IsSwapping && toolManager.CurrentTool is ScannerTool,
                    actionTimeout,
                    "Equip scanner",
                    cancellationToken);
                if (!equipped)
                    return;

                if (!(toolManager.CurrentTool is ScannerTool scanner))
                {
                    Debug.LogWarning("[ScanSmoke] ScannerTool did not become active.");
                    return;
                }

                int entriesBefore = scanLogSystem.EntryCount;
                LogVerbose($"SCAN before={entriesBefore} probeId={probeId}");
                scanner.UsePrimary(0f);
                await DelayRealtimeAsync(settleDelay, cancellationToken);
                bool archivedInTime = await WaitUntilAsync(
                    () => scanLogSystem.ContainsEntry(probeId) && scanLogSystem.EntryCount >= entriesBefore + 1,
                    actionTimeout,
                    "Archive scan probe",
                    cancellationToken);
                if (!archivedInTime)
                    return;

                bool archived = scanLogSystem.ContainsEntry(probeId);
                int entriesAfter = scanLogSystem.EntryCount;
                Debug.Log($"[ScanSmoke] COMPLETE archived={archived} entries={entriesBefore}->{entriesAfter}");
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                probeTarget.Configure(originalId, originalTitle, originalCategory, originalSummary);
                probeTarget.transform.SetPositionAndRotation(originalPosition, originalRotation);
                probeTarget.gameObject.SetActive(originalActive);
                toolManager.Holster();
                _isRunning = false;
            }
        }

        private static async Awaitable<bool> WaitUntilAsync(Func<bool> predicate, float timeout, string label, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.01f, timeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                bool success = false;
                try
                {
                    success = predicate();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ScanSmoke] EXCEPTION {label}: {ex}");
                    return false;
                }

                if (success)
                    return true;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
            }

            Debug.LogWarning($"[ScanSmoke] TIMEOUT {label} after {timeout:0.00}s");
            return false;
        }

        private static async Awaitable DelayRealtimeAsync(float seconds, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < deadline)
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
        }

        private void AutoResolve()
        {
            if (toolManager == null)
                toolManager = FindSceneObjectIncludingInactive<PlayerToolManager>();
            if (scanLogSystem == null)
                scanLogSystem = FindSceneObjectIncludingInactive<ScanLogSystem>();
            if (probeTarget == null)
                probeTarget = FindSceneObjectIncludingInactive<ScannableTarget>();
            if (playerRoot == null)
            {
                PlayerToolManager resolvedManager = toolManager != null ? toolManager : FindSceneObjectIncludingInactive<PlayerToolManager>();
                if (resolvedManager != null)
                    playerRoot = resolvedManager.transform;
            }
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
                Debug.Log($"[ScanSmoke] {message}");
        }

        private string DescribeRefs()
        {
            return $"tools={(toolManager != null ? "Y" : "N")} scanLog={(scanLogSystem != null ? "Y" : "N")} probe={(probeTarget != null ? "Y" : "N")} player={(playerRoot != null ? "Y" : "N")}";
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
    }
}
