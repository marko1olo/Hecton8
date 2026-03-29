using System.Collections;
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

        private bool _isRunning;

        private void Awake()
        {
            AutoResolve();
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
            AutoResolve();
        }
#endif

        [ContextMenu("Run Scan Runtime Smoke Pass")]
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
            if (toolManager == null || scanLogSystem == null || probeTarget == null || playerRoot == null)
            {
                Debug.LogWarning($"[ScanSmoke] Missing references refs={DescribeRefs()}");
                yield break;
            }

            _isRunning = true;
            if (startupDelay > 0f)
                yield return new WaitForSecondsRealtime(startupDelay);

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
                    yield break;
                }

                LogVerbose($"ARM slot={scannerSlot}");
                toolManager.SwitchToSlot(scannerSlot);
                yield return WaitUntil(
                    () => !toolManager.IsSwapping && toolManager.CurrentTool is ScannerTool,
                    actionTimeout,
                    "Equip scanner");

                if (!(toolManager.CurrentTool is ScannerTool scanner))
                {
                    Debug.LogWarning("[ScanSmoke] ScannerTool did not become active.");
                    yield break;
                }

                int entriesBefore = scanLogSystem.EntryCount;
                LogVerbose($"SCAN before={entriesBefore} probeId={probeId}");
                scanner.UsePrimary(0f);
                yield return new WaitForSecondsRealtime(settleDelay);
                yield return WaitUntil(
                    () => scanLogSystem.ContainsEntry(probeId) && scanLogSystem.EntryCount >= entriesBefore + 1,
                    actionTimeout,
                    "Archive scan probe");

                bool archived = scanLogSystem.ContainsEntry(probeId);
                int entriesAfter = scanLogSystem.EntryCount;
                Debug.Log($"[ScanSmoke] COMPLETE archived={archived} entries={entriesBefore}->{entriesAfter}");
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

        private IEnumerator WaitUntil(System.Func<bool> predicate, float timeout, string label)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.01f, timeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                bool success = false;
                try
                {
                    success = predicate();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ScanSmoke] EXCEPTION {label}: {ex}");
                    yield break;
                }

                if (success)
                    yield break;

                yield return null;
            }

            Debug.LogWarning($"[ScanSmoke] TIMEOUT {label} after {timeout:0.00}s");
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

        private static T FindSceneObjectIncludingInactive<T>() where T : Object
        {
            T[] candidates = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate == null)
                    continue;

                if (candidate is Component component)
                {
                    if (!component.gameObject.scene.IsValid())
                        continue;

                    return candidate;
                }

                if (candidate is GameObject gameObject)
                {
                    if (!gameObject.scene.IsValid())
                        continue;

                    return candidate;
                }
            }

            return null;
        }
    }
}
