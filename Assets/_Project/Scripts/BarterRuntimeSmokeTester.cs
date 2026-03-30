// ============================================================================
// HECTON-8 - BarterRuntimeSmokeTester.cs
// Dev-only runtime smoke coverage for PDA barter / exchange execution.
// Verifies unlock gate, offer execution, inventory delta, and execution count.
// ============================================================================

using System.Collections;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Barter Runtime Smoke Tester")]
    public sealed class BarterRuntimeSmokeTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PDAExchangeSystem exchangeSystem;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private ScanLogSystem scanLogSystem;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private int offerIndex = 0;
        [SerializeField] private bool verboseLogging = false;

        [Header("Debug")]
        [SerializeField] private int _debugRunCount = 0;
        [SerializeField] private string _debugLastPhase = "Idle";
        [SerializeField] private bool _debugLastPass = false;
        [SerializeField] private string _debugLastIssue = "";

        private bool _isRunning;
        private PDAExchangeSystem.OfferSnapshot[] _snapshotBuffer;

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
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AutoResolve();
        }
#endif

        [ContextMenu("Run Barter Smoke Pass")]
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
            _isRunning = true;
            _debugRunCount++;
            _debugLastPass = false;
            _debugLastIssue = string.Empty;
            _debugLastPhase = "Startup";

            if (startupDelay > 0f)
                yield return new WaitForSecondsRealtime(startupDelay);

            if (exchangeSystem == null || playerInventory == null || scanLogSystem == null)
            {
                Fail("Missing exchange/inventory/scan system references.");
                _isRunning = false;
                yield break;
            }

            if (_snapshotBuffer == null || _snapshotBuffer.Length < Mathf.Max(1, exchangeSystem.OfferCount))
                _snapshotBuffer = new PDAExchangeSystem.OfferSnapshot[Mathf.Max(1, exchangeSystem.OfferCount)];

            BarterOfferData offer = exchangeSystem.GetOfferAt(offerIndex);
            if (offer == null)
            {
                Fail($"Offer index {offerIndex} is not available.");
                _isRunning = false;
                yield break;
            }

            _debugLastPhase = "Unlock";
            if (!string.IsNullOrWhiteSpace(offer.requiredScanEntryId) && !scanLogSystem.ContainsEntry(offer.requiredScanEntryId))
            {
                LogVerbose($"Archiving unlock entry {offer.requiredScanEntryId}");
                scanLogSystem.ArchiveEntry(
                    offer.requiredScanEntryId,
                    "SMOKE UNLOCK",
                    "Debug",
                    "Synthetic unlock for barter runtime smoke.",
                    markRecent: false);
            }

            _debugLastPhase = "ProvisionCosts";
            EnsureBundleAvailable(offer.costs);

            _debugLastPhase = "SnapshotBefore";
            int beforeExecutions = GetExecutionCountForOffer(offer.offerId);
            int[] costBefore = CaptureBundleCounts(offer.costs);
            int[] rewardBefore = CaptureBundleCounts(offer.rewards);

            if (!exchangeSystem.CanExecute(offer, out string beforeStatus))
            {
                Fail($"Offer not executable before smoke: {beforeStatus}");
                _isRunning = false;
                yield break;
            }

            _debugLastPhase = "Execute";
            bool executed = exchangeSystem.TryExecuteOffer(offerIndex);
            if (!executed)
            {
                Fail("TryExecuteOffer returned false.");
                _isRunning = false;
                yield break;
            }

            yield return null;

            _debugLastPhase = "Validate";
            int afterExecutions = GetExecutionCountForOffer(offer.offerId);
            if (afterExecutions != beforeExecutions + 1)
            {
                Fail($"Execution count mismatch {beforeExecutions} -> {afterExecutions}.");
                _isRunning = false;
                yield break;
            }

            if (!ValidateBundleDelta(offer.costs, costBefore, shouldIncrease: false, "cost"))
            {
                _isRunning = false;
                yield break;
            }

            if (!ValidateBundleDelta(offer.rewards, rewardBefore, shouldIncrease: true, "reward"))
            {
                _isRunning = false;
                yield break;
            }

            _debugLastPhase = "Complete";
            _debugLastPass = true;
            _debugLastIssue = string.Empty;
            Debug.Log($"[BarterSmoke] COMPLETE pass=True offer={offer.offerId}");
            _isRunning = false;
        }

        private int GetExecutionCountForOffer(string offerId)
        {
            int count = exchangeSystem != null ? exchangeSystem.CopySnapshots(_snapshotBuffer) : 0;
            for (int i = 0; i < count; i++)
            {
                PDAExchangeSystem.OfferSnapshot snapshot = _snapshotBuffer[i];
                if (snapshot.Offer != null && string.Equals(snapshot.Offer.offerId, offerId, System.StringComparison.Ordinal))
                    return snapshot.Executions;
            }

            return 0;
        }

        private int[] CaptureBundleCounts(BarterItemAmount[] bundle)
        {
            if (bundle == null || bundle.Length == 0)
                return System.Array.Empty<int>();

            int[] counts = new int[bundle.Length];
            for (int i = 0; i < bundle.Length; i++)
            {
                counts[i] = bundle[i].item != null ? playerInventory.CountTotal(bundle[i].item) : 0;
            }

            return counts;
        }

        private void EnsureBundleAvailable(BarterItemAmount[] bundle)
        {
            if (bundle == null || bundle.Length == 0)
                return;

            for (int i = 0; i < bundle.Length; i++)
            {
                BarterItemAmount entry = bundle[i];
                if (entry.item == null)
                    continue;

                int required = Mathf.Max(1, entry.amount);
                int current = playerInventory.CountTotal(entry.item);
                int missing = required - current;
                if (missing > 0)
                    playerInventory.TryAddItem(entry.item, missing);
            }
        }

        private bool ValidateBundleDelta(BarterItemAmount[] bundle, int[] beforeCounts, bool shouldIncrease, string label)
        {
            if (bundle == null || bundle.Length == 0)
                return true;

            for (int i = 0; i < bundle.Length; i++)
            {
                BarterItemAmount entry = bundle[i];
                if (entry.item == null)
                    continue;

                int expectedDelta = Mathf.Max(1, entry.amount);
                int before = i < beforeCounts.Length ? beforeCounts[i] : 0;
                int after = playerInventory.CountTotal(entry.item);
                int actualDelta = after - before;

                if (!shouldIncrease)
                    actualDelta = -actualDelta;

                if (actualDelta < expectedDelta)
                {
                    Fail($"{label} delta mismatch for {entry.item.itemName}: expected {expectedDelta}, got {actualDelta}.");
                    return false;
                }
            }

            return true;
        }

        private void AutoResolve()
        {
            if (exchangeSystem == null)
                exchangeSystem = FindFirstObjectByType<PDAExchangeSystem>();
            if (playerInventory == null)
                playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (scanLogSystem == null)
                scanLogSystem = FindFirstObjectByType<ScanLogSystem>();
        }

        private void Fail(string issue)
        {
            _debugLastPass = false;
            _debugLastIssue = issue;
            _debugLastPhase = "Failed";
            Debug.LogWarning($"[BarterSmoke] FAIL {issue}");
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
                Debug.Log($"[BarterSmoke] {message}");
        }
    }
}
