using System;
using System.Collections.Generic;
using System.Text;
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Inventory;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/PDA Exchange System")]
    public sealed class PDAExchangeSystem : MonoBehaviour, ISaveable
    {
        public readonly struct TransactionSnapshot
        {
            public readonly string OfferId;
            public readonly string OfferName;
            public readonly string ChannelName;
            public readonly string CostSummary;
            public readonly string RewardSummary;

            public TransactionSnapshot(string offerId, string offerName, string channelName, string costSummary, string rewardSummary)
            {
                OfferId = offerId;
                OfferName = offerName;
                ChannelName = channelName;
                CostSummary = costSummary;
                RewardSummary = rewardSummary;
            }
        }

        public readonly struct OfferSnapshot
        {
            public readonly BarterOfferData Offer;
            public readonly int Executions;
            public readonly bool Unlocked;
            public readonly bool CanExecute;
            public readonly string Status;

            public OfferSnapshot(BarterOfferData offer, int executions, bool unlocked, bool canExecute, string status)
            {
                Offer = offer;
                Executions = executions;
                Unlocked = unlocked;
                CanExecute = canExecute;
                Status = status;
            }
        }

        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private ScanLogSystem scanLogSystem;
        [SerializeField] private HUDNotification hudNotification;
        [SerializeField] private BarterOfferCatalog offerCatalog;

        private readonly Dictionary<string, int> _executionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<TransactionSnapshot> _recentTransactions = new List<TransactionSnapshot>(8);
        private readonly StringBuilder _sb = new StringBuilder(256);

        public static PDAExchangeSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        public int SavePriority => 36;
        public int LoadPriority => 36;
        public int OfferCount => offerCatalog != null ? offerCatalog.Count : 0;
        public int RecentTransactionCount => _recentTransactions.Count;

        public event Action ExchangeStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            AutoResolve();
        }

        private void OnEnable()
        {
            AutoResolve();
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Register(this);
            if (playerInventory != null)
                playerInventory.InventoryChanged += HandleInventoryChanged;
            if (scanLogSystem != null)
                scanLogSystem.ScanLogChanged += HandleScanLogChanged;
        }

        private void OnDisable()
        {
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
            if (playerInventory != null)
                playerInventory.InventoryChanged -= HandleInventoryChanged;
            if (scanLogSystem != null)
                scanLogSystem.ScanLogChanged -= HandleScanLogChanged;
            if (Instance == this)
                Instance = null;
        }

        public BarterOfferData GetOfferAt(int index)
        {
            return offerCatalog != null ? offerCatalog.GetAt(index) : null;
        }

        public int CopyRecentTransactions(TransactionSnapshot[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _recentTransactions.Count == 0)
                return 0;

            int count = Mathf.Min(buffer.Length, _recentTransactions.Count);
            for (int i = 0; i < count; i++)
                buffer[i] = _recentTransactions[i];
            return count;
        }

        public int CopySnapshots(OfferSnapshot[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || offerCatalog == null)
                return 0;

            int count = Mathf.Min(buffer.Length, offerCatalog.Count);
            for (int i = 0; i < count; i++)
            {
                BarterOfferData offer = offerCatalog.GetAt(i);
                if (offer == null)
                {
                    buffer[i] = default;
                    continue;
                }

                int executions = GetExecutionCount(offer.offerId);
                bool unlocked = IsUnlocked(offer);
                string status = "SCAN LOCK";
                bool canExecute = unlocked && CanExecute(offer, out status);
                buffer[i] = new OfferSnapshot(offer, executions, unlocked, canExecute, unlocked ? status : "SCAN LOCK");
            }

            return count;
        }

        public bool TryExecuteOffer(int index)
        {
            BarterOfferData offer = GetOfferAt(index);
            if (offer == null)
                return false;

            if (!IsUnlocked(offer))
            {
                NotifyWarning("EXCHANGE RELAY - OFFER LOCKED");
                return false;
            }

            if (!CanExecute(offer, out string status))
            {
                NotifyWarning($"EXCHANGE RELAY - {status}");
                return false;
            }

            if (playerInventory == null)
            {
                NotifyWarning("EXCHANGE RELAY - INVENTORY OFFLINE");
                return false;
            }

            if (!ConsumeBundle(offer.costs))
            {
                NotifyWarning("EXCHANGE RELAY - COST VERIFICATION FAILED");
                return false;
            }

            if (!GrantRewards(offer.rewards))
            {
                RefundBundle(offer.costs);
                NotifyWarning("EXCHANGE RELAY - CARGO CAPACITY INSUFFICIENT");
                return false;
            }

            string key = GetOfferKey(offer.offerId);
            _executionCounts.TryGetValue(key, out int currentCount);
            _executionCounts[key] = currentCount + 1;
            PushRecentTransaction(new TransactionSnapshot(
                offer.offerId,
                offer.offerName,
                offer.channelName,
                BuildBundleSummary(offer.costs, "NONE"),
                BuildBundleSummary(offer.rewards, "NONE")));

            NotifyInfo($"EXCHANGE RELAY - {offer.offerName.ToUpperInvariant()} CONFIRMED");
            ExchangeStateChanged?.Invoke();

            // Уведомляем Atlas6DirectiveSystem — бартер = рост доверия
            Atlas6DirectiveSystem directive = Atlas6DirectiveSystem.Instance;
            if (directive != null)
                directive.RegisterBarterTransaction();

            // Публикуем событие для QuestManager (OnItemCollected уже обрабатывается)
            Atlas6Events.RaiseBarterAccepted(_executionCounts.Count);

            return true;
        }

        public bool CanExecute(BarterOfferData offer, out string status)
        {
            status = "READY";
            if (offer == null)
            {
                status = "NO OFFER";
                return false;
            }

            if (!IsUnlocked(offer))
            {
                status = "SCAN LOCK";
                return false;
            }

            if (HasReachedLimit(offer))
            {
                status = "CONTRACT CLOSED";
                return false;
            }

            if (playerInventory == null)
            {
                status = "INVENTORY OFFLINE";
                return false;
            }

            BarterItemAmount[] costs = offer.costs;
            for (int i = 0; i < costs.Length; i++)
            {
                if (costs[i].item == null)
                {
                    status = "COST DATA INVALID";
                    return false;
                }

                int amount = Mathf.Max(1, costs[i].amount);
                if (playerInventory.CountTotal(Hecton.Localization.LocHash.Compute(costs[i].item.PersistentId)) < amount)
                {
                    status = $"NEED {costs[i].item.itemName.ToUpperInvariant()} X{amount}";
                    return false;
                }
            }

            return true;
        }

        public string BuildBundleSummary(BarterItemAmount[] bundle, string emptyLabel)
        {
            _sb.Length = 0;
            if (bundle == null || bundle.Length == 0)
                return emptyLabel;

            for (int i = 0; i < bundle.Length; i++)
            {
                if (i > 0)
                    _sb.Append("  |  ");

                int amount = Mathf.Max(1, bundle[i].amount);
                _sb.Append(bundle[i].item != null ? bundle[i].item.itemName.ToUpperInvariant() : "UNKNOWN");
                _sb.Append(" X").Append(amount);
            }

            return _sb.ToString();
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.barter.EnsureCapacity();
            int count = 0;
            Dictionary<string, int>.Enumerator enumerator = _executionCounts.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, int> kvp = enumerator.Current;
                if (count >= BarterDTO.MaxOffers)
                    break;

                data.barter.offerStates[count] = new BarterOfferStateDTO
                {
                    offerId = kvp.Key,
                    executionCount = kvp.Value
                };
                count++;
            }

            data.barter.stateCount = count;
            for (int i = count; i < BarterDTO.MaxOffers; i++)
                data.barter.offerStates[i] = default;

            int recentCount = Mathf.Min(_recentTransactions.Count, BarterDTO.MaxRecentTransactions);
            data.barter.recentTransactionCount = recentCount;
            for (int i = 0; i < recentCount; i++)
            {
                TransactionSnapshot tx = _recentTransactions[i];
                data.barter.recentTransactions[i] = new BarterTransactionDTO
                {
                    offerId = tx.OfferId,
                    offerName = tx.OfferName,
                    channelName = tx.ChannelName,
                    costSummary = tx.CostSummary,
                    rewardSummary = tx.RewardSummary
                };
            }

            for (int i = recentCount; i < BarterDTO.MaxRecentTransactions; i++)
                data.barter.recentTransactions[i] = default;
        }

        public void LoadFromSaveData(SaveData data)
        {
            _executionCounts.Clear();
            _recentTransactions.Clear();
            if (data == null)
                return;

            BarterDTO dto = data.barter;
            int count = Mathf.Clamp(dto.stateCount, 0, dto.offerStates != null ? dto.offerStates.Length : 0);
            for (int i = 0; i < count; i++)
            {
                BarterOfferStateDTO state = dto.offerStates[i];
                if (string.IsNullOrWhiteSpace(state.offerId))
                    continue;

                _executionCounts[GetOfferKey(state.offerId)] = Mathf.Max(0, state.executionCount);
            }

            int recentCount = Mathf.Clamp(dto.recentTransactionCount, 0, dto.recentTransactions != null ? dto.recentTransactions.Length : 0);
            for (int i = 0; i < recentCount; i++)
            {
                BarterTransactionDTO tx = dto.recentTransactions[i];
                if (string.IsNullOrWhiteSpace(tx.offerId) && string.IsNullOrWhiteSpace(tx.offerName))
                    continue;

                _recentTransactions.Add(new TransactionSnapshot(
                    tx.offerId ?? string.Empty,
                    tx.offerName ?? string.Empty,
                    tx.channelName ?? string.Empty,
                    tx.costSummary ?? string.Empty,
                    tx.rewardSummary ?? string.Empty));
            }

            ExchangeStateChanged?.Invoke();
        }

        private void AutoResolve()
        {
            if (playerInventory == null)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    playerInventory = playerTransform.GetComponent<PlayerInventory>();
                }
            }
            if (scanLogSystem == null)
                scanLogSystem = ScanLogSystem.Instance;
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
        }

        private void HandleInventoryChanged() => ExchangeStateChanged?.Invoke();
        private void HandleScanLogChanged() => ExchangeStateChanged?.Invoke();

        private bool IsUnlocked(BarterOfferData offer)
        {
            if (offer == null)
                return false;

            if (string.IsNullOrWhiteSpace(offer.requiredScanEntryId))
                return true;

            return scanLogSystem != null && scanLogSystem.ContainsEntry(offer.requiredScanEntryId);
        }

        private bool HasReachedLimit(BarterOfferData offer)
        {
            if (offer == null)
                return true;

            if (offer.repeatLimit <= 0)
                return false;

            return GetExecutionCount(offer.offerId) >= offer.repeatLimit;
        }

        private int GetExecutionCount(string offerId)
        {
            _executionCounts.TryGetValue(GetOfferKey(offerId), out int count);
            return count;
        }

        private static string GetOfferKey(string offerId)
        {
            return string.IsNullOrWhiteSpace(offerId) ? string.Empty : offerId.Trim();
        }

        private void PushRecentTransaction(TransactionSnapshot snapshot)
        {
            if (_recentTransactions.Count >= BarterDTO.MaxRecentTransactions)
                _recentTransactions.RemoveAt(_recentTransactions.Count - 1);
            _recentTransactions.Insert(0, snapshot);
        }

        private bool ConsumeBundle(BarterItemAmount[] bundle)
        {
            if (bundle == null || bundle.Length == 0)
                return true;

            for (int i = 0; i < bundle.Length; i++)
            {
                if (bundle[i].item == null)
                    return false;

                if (!playerInventory.TryRemoveQuantity(Hecton.Localization.LocHash.Compute(bundle[i].item.PersistentId), Mathf.Max(1, bundle[i].amount)))
                {
                    for (int j = 0; j < i; j++)
                        playerInventory.TryAddItem(Hecton.Localization.LocHash.Compute(bundle[j].item.PersistentId), Mathf.Max(1, bundle[j].amount));
                    return false;
                }
            }

            return true;
        }

        private bool GrantRewards(BarterItemAmount[] bundle)
        {
            if (bundle == null || bundle.Length == 0)
                return true;

            for (int i = 0; i < bundle.Length; i++)
            {
                if (bundle[i].item == null)
                    return false;

                if (!playerInventory.TryAddItem(Hecton.Localization.LocHash.Compute(bundle[i].item.PersistentId), Mathf.Max(1, bundle[i].amount)))
                {
                    for (int j = 0; j < i; j++)
                        playerInventory.TryRemoveQuantity(Hecton.Localization.LocHash.Compute(bundle[j].item.PersistentId), Mathf.Max(1, bundle[j].amount));
                    return false;
                }
            }

            return true;
        }

        private void RefundBundle(BarterItemAmount[] bundle)
        {
            if (bundle == null || bundle.Length == 0 || playerInventory == null)
                return;

            for (int i = 0; i < bundle.Length; i++)
            {
                if (bundle[i].item != null)
                    playerInventory.TryAddItem(Hecton.Localization.LocHash.Compute(bundle[i].item.PersistentId), Mathf.Max(1, bundle[i].amount));
            }
        }

        private void NotifyInfo(string message)
        {
            if (hudNotification != null)
                hudNotification.ShowInfo(message);
        }

        private void NotifyWarning(string message)
        {
            if (hudNotification != null)
                hudNotification.ShowWarning(message);
        }
    }
}
