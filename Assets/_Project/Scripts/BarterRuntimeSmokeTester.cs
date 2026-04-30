// ============================================================================
// HECTON-8 - BarterRuntimeSmokeTester.cs
// Dev-only runtime smoke coverage for PDA barter / exchange execution.
// Verifies unlock gate, offer execution, inventory delta, and execution count.
// ============================================================================

using System;
using System.Threading;
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

        // Inspector-only smoke diagnostics for manual runtime validation.
#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private int _debugRunCount = 0;
        [SerializeField] private string _debugLastPhase = "Idle";
        [SerializeField] private bool _debugLastPass = false;
        [SerializeField] private string _debugLastIssue = "";
#pragma warning restore CS0414

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

        [ContextMenu("Run Barter Smoke Pass")]
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
            _isRunning = true;
            _debugRunCount++;
            _debugLastPass = false;
            _debugLastIssue = string.Empty;
            _debugLastPhase = "Startup";

            try
            {
                if (startupDelay > 0f)
                    await DelayRealtimeAsync(startupDelay, cancellationToken);

                if (cancellationToken.IsCancellationRequested || this == null)
                    return;

                if (exchangeSystem == null || playerInventory == null || scanLogSystem == null)
                {
                    Fail("Missing exchange/inventory/scan system references.");
                    return;
                }

                if (_snapshotBuffer == null || _snapshotBuffer.Length < Mathf.Max(1, exchangeSystem.OfferCount))
                    _snapshotBuffer = new PDAExchangeSystem.OfferSnapshot[Mathf.Max(1, exchangeSystem.OfferCount)];

                BarterOfferData offer = exchangeSystem.GetOfferAt(offerIndex);
                if (offer == null)
                {
                    Fail($"Offer index {offerIndex} is not available.");
                    return;
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
                    return;
                }

                _debugLastPhase = "Execute";
                bool executed = exchangeSystem.TryExecuteOffer(offerIndex);
                if (!executed)
                {
                    Fail("TryExecuteOffer returned false.");
                    return;
                }

                await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);

                if (cancellationToken.IsCancellationRequested || this == null)
                    return;

                _debugLastPhase = "Validate";
                int afterExecutions = GetExecutionCountForOffer(offer.offerId);
                if (afterExecutions != beforeExecutions + 1)
                {
                    Fail($"Execution count mismatch {beforeExecutions} -> {afterExecutions}.");
                    return;
                }

                if (!ValidateBundleDelta(offer.costs, costBefore, shouldIncrease: false, "cost"))
                    return;

                if (!ValidateBundleDelta(offer.rewards, rewardBefore, shouldIncrease: true, "reward"))
                    return;

                _debugLastPhase = "Complete";
                _debugLastPass = true;
                _debugLastIssue = string.Empty;
                Debug.Log($"[BarterSmoke] COMPLETE pass=True offer={offer.offerId}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                _isRunning = false;
            }
        }

        private static async Awaitable DelayRealtimeAsync(float seconds, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
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
                counts[i] = bundle[i].item != null ? playerInventory.CountTotal(Hecton.Localization.LocHash.Compute(bundle[i].item.PersistentId)) : 0;
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
                int current = playerInventory.CountTotal(Hecton.Localization.LocHash.Compute(entry.item.PersistentId));
                int missing = required - current;
                if (missing > 0)
                    playerInventory.TryAddItem(Hecton.Localization.LocHash.Compute(entry.item.PersistentId), missing);
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
                int after = playerInventory.CountTotal(Hecton.Localization.LocHash.Compute(entry.item.PersistentId));
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
                exchangeSystem = PDAExchangeSystem.Instance;
            if (playerInventory == null)
                playerInventory = (Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.Inventory : null);
            if (scanLogSystem == null)
                scanLogSystem = ScanLogSystem.Instance;
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
