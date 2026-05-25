// ============================================================================
// HECTON-8 - BarterRuntimeSmokeTester.cs
// Dev-only runtime smoke coverage for PDA barter / exchange execution.
// Verifies unlock gate, offer execution, inventory delta, and execution count.
// ============================================================================

using System;
using System.Threading;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Unity.Mathematics;
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

        private const int BundleCountSnapshotCapacity = 32;

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
        // COLD ALLOC: int[32] - barter cost count snapshot reused by smoke validation - owner: BarterRuntimeSmokeTester
        private readonly int[] _costCountSnapshot = new int[BundleCountSnapshotCapacity];
        // COLD ALLOC: int[32] - barter reward count snapshot reused by smoke validation - owner: BarterRuntimeSmokeTester
        private readonly int[] _rewardCountSnapshot = new int[BundleCountSnapshotCapacity];

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

                int snapshotCapacity = math.max(1, exchangeSystem.OfferCount);
                if (_snapshotBuffer == null || _snapshotBuffer.Length < snapshotCapacity)
                    _snapshotBuffer = new PDAExchangeSystem.OfferSnapshot[snapshotCapacity];

                BarterOfferData offer = exchangeSystem.GetOfferAt(offerIndex);
                if (offer == null)
                {
                    Fail($"Offer index {offerIndex} is not available.");
                    return;
                }

                _debugLastPhase = "Unlock";
                uint requiredScanEntryHash = exchangeSystem.GetRequiredScanEntryHash(offer);
                if (requiredScanEntryHash != 0u && !scanLogSystem.ContainsEntry(requiredScanEntryHash))
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
                int offerHash = exchangeSystem.GetOfferHash(offer);
                int beforeExecutions = GetExecutionCountForOffer(offerHash);
                int costSnapshotLength = CaptureBundleCounts(offer.costs, _costCountSnapshot, "cost");
                if (costSnapshotLength < 0)
                    return;

                int rewardSnapshotLength = CaptureBundleCounts(offer.rewards, _rewardCountSnapshot, "reward");
                if (rewardSnapshotLength < 0)
                    return;

                if (!exchangeSystem.CanExecute(offer, out PDAExchangeSystem.ExchangeStatus beforeStatus))
                {
                    Fail($"Offer not executable before smoke: {PDAExchangeSystem.ResolveStatusLabel(beforeStatus)}");
                    return;
                }

                _debugLastPhase = "Execute";
                bool executed = exchangeSystem.TryExecuteOffer(offerIndex);
                if (!executed)
                {
                    Fail("TryExecuteOffer returned false.");
                    return;
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);

                if (cancellationToken.IsCancellationRequested || this == null)
                    return;

                _debugLastPhase = "Validate";
                int afterExecutions = GetExecutionCountForOffer(offerHash);
                if (afterExecutions != beforeExecutions + 1)
                {
                    Fail($"Execution count mismatch {beforeExecutions} -> {afterExecutions}.");
                    return;
                }

                if (!ValidateBundleDelta(offer.costs, _costCountSnapshot, costSnapshotLength, shouldIncrease: false, "cost"))
                    return;

                if (!ValidateBundleDelta(offer.rewards, _rewardCountSnapshot, rewardSnapshotLength, shouldIncrease: true, "reward"))
                    return;

                _debugLastPhase = "Complete";
                _debugLastPass = true;
                _debugLastIssue = string.Empty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.Log($"[BarterSmoke] COMPLETE pass=True offer={offer.offerId}");
#endif
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
            finally
            {
                _isRunning = false;
            }
        }

        private static async Awaitable DelayRealtimeAsync(float seconds, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + math.max(0f, seconds);
            while (Time.realtimeSinceStartup < deadline)
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
        }

        private int GetExecutionCountForOffer(int offerHash)
        {
            if (offerHash == 0)
                return 0;

            int count = exchangeSystem != null ? exchangeSystem.CopySnapshots(_snapshotBuffer) : 0;
            for (int i = 0; i < count; i++)
            {
                PDAExchangeSystem.OfferSnapshot snapshot = _snapshotBuffer[i];
                if (snapshot.Offer != null && snapshot.OfferHash == offerHash)
                    return snapshot.Executions;
            }

            return 0;
        }

        private int CaptureBundleCounts(BarterItemAmount[] bundle, int[] counts, string label)
        {
            if (bundle == null || bundle.Length == 0)
                return 0;

            if (counts == null || bundle.Length > counts.Length)
            {
                Fail($"{label} bundle count {bundle.Length} exceeds smoke snapshot capacity {BundleCountSnapshotCapacity}.");
                return -1;
            }

            for (int i = 0; i < bundle.Length; i++)
            {
                counts[i] = bundle[i].item != null ? playerInventory.CountTotal(bundle[i].item.PersistentHashId) : 0;
            }

            return bundle.Length;
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

                int required = math.max(1, entry.amount);
                int itemHash = entry.item.PersistentHashId;
                int current = playerInventory.CountTotal(itemHash);
                int missing = required - current;
                if (missing > 0)
                    playerInventory.TryAddItem(itemHash, missing);
            }
        }

        private bool ValidateBundleDelta(BarterItemAmount[] bundle, int[] beforeCounts, int beforeCountLength, bool shouldIncrease, string label)
        {
            if (bundle == null || bundle.Length == 0)
                return true;

            for (int i = 0; i < bundle.Length && i < beforeCountLength; i++)
            {
                BarterItemAmount entry = bundle[i];
                if (entry.item == null)
                    continue;

                int expectedDelta = math.max(1, entry.amount);
                int before = beforeCounts[i];
                int after = playerInventory.CountTotal(entry.item.PersistentHashId);
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
                exchangeSystem = Hecton8.Core.GlobalRegistry.PDAExchange;
            if (playerInventory == null)
                playerInventory = (Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext != null ? Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext.Inventory : null);
            if (scanLogSystem == null)
                scanLogSystem = Hecton8.Core.GlobalRegistry.ScanLog;
        }

        private void Fail(string issue)
        {
            _debugLastPass = false;
            _debugLastIssue = issue;
            _debugLastPhase = "Failed";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[BarterSmoke] FAIL {issue}");
#endif
        }

        private void LogVerbose(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogging)
                Hecton8.Core.H8Debug.Log($"[BarterSmoke] {message}");
#endif
        }

    }
}
