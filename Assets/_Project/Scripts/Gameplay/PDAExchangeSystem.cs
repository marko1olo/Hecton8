using System;
using System.Runtime.InteropServices;
using System.Text;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Inventory;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/PDA Exchange System")]
    public sealed class PDAExchangeSystem : MonoBehaviour, ISaveable
    {
        [StructLayout(LayoutKind.Sequential)]
        public readonly struct TransactionSnapshot
        {
            public readonly BarterOfferData Offer;
            public readonly int OfferHash;
            public readonly string LegacyOfferId;
            public readonly string LegacyOfferName;
            public readonly string LegacyChannelName;
            public readonly string LegacyCostSummary;
            public readonly string LegacyRewardSummary;

            public TransactionSnapshot(BarterOfferData offer, int offerHash)
            {
                Offer = offer;
                OfferHash = offerHash;
                LegacyOfferId = string.Empty;
                LegacyOfferName = string.Empty;
                LegacyChannelName = string.Empty;
                LegacyCostSummary = string.Empty;
                LegacyRewardSummary = string.Empty;
            }

            private TransactionSnapshot(
                BarterOfferData offer,
                int offerHash,
                string legacyOfferId,
                string legacyOfferName,
                string legacyChannelName,
                string legacyCostSummary,
                string legacyRewardSummary)
            {
                Offer = offer;
                OfferHash = offerHash;
                LegacyOfferId = legacyOfferId;
                LegacyOfferName = legacyOfferName;
                LegacyChannelName = legacyChannelName;
                LegacyCostSummary = legacyCostSummary;
                LegacyRewardSummary = legacyRewardSummary;
            }

            public static TransactionSnapshot FromLegacy(
                int offerHash,
                string offerId,
                string offerName,
                string channelName,
                string costSummary,
                string rewardSummary)
            {
                return new TransactionSnapshot(
                    null,
                    offerHash,
                    offerId ?? string.Empty,
                    offerName ?? string.Empty,
                    channelName ?? string.Empty,
                    costSummary ?? string.Empty,
                    rewardSummary ?? string.Empty);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct OfferSnapshot
        {
            public readonly BarterOfferData Offer;
            public readonly int OfferHash;
            public readonly uint RequiredScanEntryHash;
            public readonly int Executions;
            public readonly ExchangeStatus Status;
            private readonly byte _unlocked;
            private readonly byte _canExecute;

            public OfferSnapshot(
                BarterOfferData offer,
                int offerHash,
                uint requiredScanEntryHash,
                int executions,
                bool unlocked,
                bool canExecute,
                ExchangeStatus status)
            {
                Offer = offer;
                OfferHash = offerHash;
                RequiredScanEntryHash = requiredScanEntryHash;
                Executions = executions;
                Status = status;
                _unlocked = unlocked ? (byte)1 : (byte)0;
                _canExecute = canExecute ? (byte)1 : (byte)0;
            }

            public bool Unlocked => _unlocked != 0;
            public bool CanExecute => _canExecute != 0;
            public bool HasRequiredScanEntry => RequiredScanEntryHash != 0u;
        }

        public enum ExchangeStatus : byte
        {
            Ready = 0,
            NoOffer = 1,
            ScanLock = 2,
            ContractClosed = 3,
            InventoryOffline = 4,
            CostDataInvalid = 5,
            InsufficientMaterials = 6
        }

        private const string StatusReady = "READY";
        private const string StatusNoOffer = "NO OFFER";
        private const string StatusScanLock = "SCAN LOCK";
        private const string StatusContractClosed = "CONTRACT CLOSED";
        private const string StatusInventoryOffline = "INVENTORY OFFLINE";
        private const string StatusCostDataInvalid = "COST DATA INVALID";
        private const string StatusInsufficientMaterials = "INSUFFICIENT MATERIALS";
        private const string RelayOfferLockedMessage = "EXCHANGE RELAY - OFFER LOCKED";
        private const string RelayReadyMessage = "EXCHANGE RELAY - READY";
        private const string RelayNoOfferMessage = "EXCHANGE RELAY - NO OFFER";
        private const string RelayScanLockMessage = "EXCHANGE RELAY - SCAN LOCK";
        private const string RelayContractClosedMessage = "EXCHANGE RELAY - CONTRACT CLOSED";
        private const string RelayInventoryOfflineMessage = "EXCHANGE RELAY - INVENTORY OFFLINE";
        private const string RelayCostDataInvalidMessage = "EXCHANGE RELAY - COST DATA INVALID";
        private const string RelayInsufficientMaterialsMessage = "EXCHANGE RELAY - INSUFFICIENT MATERIALS";
        private const string RelayCostFailedMessage = "EXCHANGE RELAY - COST VERIFICATION FAILED";
        private const string RelayCargoFullMessage = "EXCHANGE RELAY - CARGO CAPACITY INSUFFICIENT";
        private const string RelayConfirmedMessage = "EXCHANGE RELAY - CONFIRMED";

        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private ScanLogSystem scanLogSystem;
        [SerializeField] private HUDNotification hudNotification;
        [SerializeField] private BarterOfferCatalog offerCatalog;

        // COLD ALLOC: int[MaxOffers] - offer execution hash slots without Dictionary churn - owner: PDAExchangeSystem
        private readonly int[] _executionOfferHashes = new int[BarterDTO.MaxOffers];
        // COLD ALLOC: int[MaxOffers] - offer execution counts parallel to _executionOfferHashes - owner: PDAExchangeSystem
        private readonly int[] _executionCounts = new int[BarterDTO.MaxOffers];
        // COLD ALLOC: int[MaxOffers] - boot-time catalog offer hashes; ScriptableObjects stay immutable at runtime.
        private readonly int[] _catalogOfferHashes = new int[BarterDTO.MaxOffers];
        // COLD ALLOC: uint[MaxOffers] - boot-time scan-entry gates parallel to _catalogOfferHashes.
        private readonly uint[] _catalogRequiredScanEntryHashes = new uint[BarterDTO.MaxOffers];
        // COLD ALLOC: TransactionSnapshot[8] - fixed recent exchange history ring - owner: PDAExchangeSystem
        private readonly TransactionSnapshot[] _recentTransactions = new TransactionSnapshot[BarterDTO.MaxRecentTransactions];
        private readonly StringBuilder _sb = new StringBuilder(256);
        private int _executionStateCount;
        private int _catalogRuntimeHashCount;
        private int _recentTransactionCount;
        private bool _serviceRegistered;

        public int SavePriority => 36;
        public int LoadPriority => 36;
        public int OfferCount => offerCatalog != null ? offerCatalog.Count : 0;
        public int RecentTransactionCount => _recentTransactionCount;

        private void Awake()
        {
            PDAExchangeSystem registered = GlobalRegistry.PDAExchange;
            if (Application.isPlaying && registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            AutoResolve();
            CacheCatalogRuntimeHashes();
        }

        private void OnEnable()
        {
            TryRegisterService();
            AutoResolve();
            CacheCatalogRuntimeHashes();
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
            TryUnregisterService();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            PDAExchangeSystem registered = GlobalRegistry.PDAExchange;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterPDAExchangeRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PDAExchange, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPDAExchangeRuntime(this);
            _serviceRegistered = false;
        }

        public BarterOfferData GetOfferAt(int index)
        {
            return offerCatalog != null ? offerCatalog.GetAt(index) : null;
        }

        public int GetOfferHash(BarterOfferData offer)
        {
            return ResolveOfferHash(offer);
        }

        public uint GetRequiredScanEntryHash(BarterOfferData offer)
        {
            int index = ResolveOfferIndex(offer);
            return GetRequiredScanEntryHashAt(index);
        }

        public bool HasRequiredScanEntry(BarterOfferData offer)
        {
            return GetRequiredScanEntryHash(offer) != 0u;
        }

        public int CopyRecentTransactions(TransactionSnapshot[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _recentTransactionCount == 0)
                return 0;

            int count = math.min(buffer.Length, _recentTransactionCount);
            for (int i = 0; i < count; i++)
                buffer[i] = _recentTransactions[i];
            return count;
        }

        public int CopySnapshots(OfferSnapshot[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || offerCatalog == null)
                return 0;

            int count = math.min(math.min(buffer.Length, offerCatalog.Count), BarterDTO.MaxOffers);
            for (int i = 0; i < count; i++)
            {
                BarterOfferData offer = offerCatalog.GetAt(i);
                if (offer == null)
                {
                    buffer[i] = default;
                    continue;
                }

                int offerHash = GetOfferHashAt(i);
                uint requiredScanEntryHash = GetRequiredScanEntryHashAt(i);
                int executions = GetExecutionCount(offerHash);
                bool unlocked = IsUnlocked(requiredScanEntryHash);
                ExchangeStatus status = ExchangeStatus.ScanLock;
                bool canExecute = unlocked && CanExecute(offer, offerHash, requiredScanEntryHash, out status);
                buffer[i] = new OfferSnapshot(
                    offer,
                    offerHash,
                    requiredScanEntryHash,
                    executions,
                    unlocked,
                    canExecute,
                    unlocked ? status : ExchangeStatus.ScanLock);
            }

            return count;
        }

        public bool TryExecuteOffer(int index)
        {
            BarterOfferData offer = GetOfferAt(index);
            if (offer == null)
                return false;

            int offerHash = ResolveOfferHash(offer);
            if (offerHash == 0)
                return false;

            uint requiredScanEntryHash = GetRequiredScanEntryHash(offer);
            if (!IsUnlocked(requiredScanEntryHash))
            {
                NotifyWarning(RelayOfferLockedMessage);
                return false;
            }

            if (!CanExecute(offer, offerHash, requiredScanEntryHash, out ExchangeStatus status))
            {
                NotifyWarning(ResolveRelayStatusMessage(status));
                return false;
            }

            if (playerInventory == null)
            {
                NotifyWarning(RelayInventoryOfflineMessage);
                return false;
            }

            if (!ConsumeBundle(offer.costs))
            {
                NotifyWarning(RelayCostFailedMessage);
                return false;
            }

            if (!GrantRewards(offer.rewards))
            {
                RefundBundle(offer.costs);
                NotifyWarning(RelayCargoFullMessage);
                return false;
            }

            IncrementExecutionCount(offerHash);
            PushRecentTransaction(new TransactionSnapshot(offer, offerHash));

            NotifyInfo(RelayConfirmedMessage);
            PublishExchangeStateChanged(PdaExchangeStateChangedSignal.ReasonExecuted);

            // Notify Atlas6DirectiveSystem: barter raises trust.
            Atlas6DirectiveSystem directive = Hecton8.Core.GlobalRegistry.Atlas6Directive;
            if (directive != null)
                directive.RegisterBarterTransaction();

            // Publish the QuestManager barter event; item collection is handled elsewhere.
            Atlas6Events.RaiseBarterAccepted(_executionStateCount);

            return true;
        }

        public bool CanExecute(BarterOfferData offer, out ExchangeStatus status)
        {
            int index = ResolveOfferIndex(offer);
            return CanExecute(offer, GetOfferHashAt(index), GetRequiredScanEntryHashAt(index), out status);
        }

        private bool CanExecute(
            BarterOfferData offer,
            int offerHash,
            uint requiredScanEntryHash,
            out ExchangeStatus status)
        {
            status = ExchangeStatus.Ready;
            if (offer == null || offerHash == 0)
            {
                status = ExchangeStatus.NoOffer;
                return false;
            }

            if (!IsUnlocked(requiredScanEntryHash))
            {
                status = ExchangeStatus.ScanLock;
                return false;
            }

            if (HasReachedLimit(offerHash, offer.repeatLimit))
            {
                status = ExchangeStatus.ContractClosed;
                return false;
            }

            if (playerInventory == null)
            {
                status = ExchangeStatus.InventoryOffline;
                return false;
            }

            BarterItemAmount[] costs = offer.costs;
            if (costs != null)
            {
                for (int i = 0; i < costs.Length; i++)
                {
                    if (costs[i].item == null)
                    {
                        status = ExchangeStatus.CostDataInvalid;
                        return false;
                    }

                    int amount = math.max(1, costs[i].amount);
                    int itemHash = costs[i].item.PersistentHashId;
                    if (itemHash == 0 || playerInventory.CountTotal(itemHash) < amount)
                    {
                        status = ExchangeStatus.InsufficientMaterials;
                        return false;
                    }
                }
            }

            return true;
        }

        public void AppendBundleSummary(StringBuilder builder, BarterItemAmount[] bundle, string emptyLabel)
        {
            if (builder == null)
                return;

            if (bundle == null || bundle.Length == 0)
            {
                builder.Append(emptyLabel);
                return;
            }

            for (int i = 0; i < bundle.Length; i++)
            {
                if (i > 0)
                    builder.Append("  |  ");

                int amount = math.max(1, bundle[i].amount);
                builder.Append(bundle[i].item != null ? bundle[i].item.itemName : "UNKNOWN");
                builder.Append(" X").Append(amount);
            }
        }

        public bool TryAppendBundleSummary(Span<char> buffer, ref int cursor, BarterItemAmount[] bundle, ReadOnlySpan<char> emptyLabel)
        {
            if (bundle == null || bundle.Length == 0)
                return TryAppend(buffer, ref cursor, emptyLabel);

            for (int i = 0; i < bundle.Length; i++)
            {
                if (i > 0 && !TryAppend(buffer, ref cursor, "  |  ".AsSpan()))
                    return false;

                int amount = math.max(1, bundle[i].amount);
                ReadOnlySpan<char> itemName = bundle[i].item != null && !string.IsNullOrWhiteSpace(bundle[i].item.itemName)
                    ? bundle[i].item.itemName.AsSpan()
                    : "UNKNOWN".AsSpan();

                if (!TryAppend(buffer, ref cursor, itemName) ||
                    !TryAppend(buffer, ref cursor, " X".AsSpan()) ||
                    !amount.TryFormat(buffer.Slice(cursor), out int written))
                {
                    return false;
                }

                cursor += written;
            }

            return true;
        }

        private string BuildBundleSummaryForSave(BarterItemAmount[] bundle, string emptyLabel)
        {
            _sb.Length = 0;
            AppendBundleSummary(_sb, bundle, emptyLabel);

            return _sb.ToString();
        }

        private static bool TryAppend(Span<char> buffer, ref int cursor, ReadOnlySpan<char> value)
        {
            if (cursor < 0 || cursor + value.Length > buffer.Length)
                return false;

            value.CopyTo(buffer.Slice(cursor));
            cursor += value.Length;
            return true;
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.barter.EnsureCapacity();
            int count = 0;
            if (offerCatalog != null)
            {
                for (int i = 0; i < offerCatalog.Count && count < BarterDTO.MaxOffers; i++)
                {
                    BarterOfferData offer = offerCatalog.GetAt(i);
                    int offerHash = ResolveOfferHash(offer);
                    int executionCount = GetExecutionCount(offerHash);
                    if (offerHash == 0 || executionCount <= 0)
                    {
                        continue;
                    }

                    data.barter.offerStates[count] = new BarterOfferStateDTO
                    {
                        offerId = offer.offerId,
                        executionCount = executionCount
                    };
                    count++;
                }
            }

            data.barter.stateCount = count;
            for (int i = count; i < BarterDTO.MaxOffers; i++)
                data.barter.offerStates[i] = default;

            int recentCount = math.min(_recentTransactionCount, BarterDTO.MaxRecentTransactions);
            data.barter.recentTransactionCount = recentCount;
            for (int i = 0; i < recentCount; i++)
            {
                TransactionSnapshot tx = _recentTransactions[i];
                BarterOfferData txOffer = tx.Offer;
                data.barter.recentTransactions[i] = new BarterTransactionDTO
                {
                    offerId = txOffer != null ? txOffer.offerId : tx.LegacyOfferId,
                    offerName = txOffer != null ? txOffer.offerName : tx.LegacyOfferName,
                    channelName = txOffer != null ? txOffer.channelName : tx.LegacyChannelName,
                    costSummary = txOffer != null ? BuildBundleSummaryForSave(txOffer.costs, "NONE") : tx.LegacyCostSummary,
                    rewardSummary = txOffer != null ? BuildBundleSummaryForSave(txOffer.rewards, "NONE") : tx.LegacyRewardSummary
                };
            }

            for (int i = recentCount; i < BarterDTO.MaxRecentTransactions; i++)
                data.barter.recentTransactions[i] = default;
        }

        public void LoadFromSaveData(SaveData data)
        {
            ClearExecutionCounts();
            ClearRecentTransactions();
            if (data == null)
                return;

            BarterDTO dto = data.barter;
            CacheCatalogRuntimeHashes();
            int count = math.clamp(dto.stateCount, 0, dto.offerStates != null ? dto.offerStates.Length : 0);
            for (int i = 0; i < count; i++)
            {
                BarterOfferStateDTO state = dto.offerStates[i];
                if (string.IsNullOrWhiteSpace(state.offerId))
                    continue;

                int offerHash = ComputeOfferHash(state.offerId);
                if (offerHash != 0)
                    SetExecutionCount(offerHash, math.max(0, state.executionCount));
            }

            int recentCount = math.clamp(dto.recentTransactionCount, 0, dto.recentTransactions != null ? dto.recentTransactions.Length : 0);
            for (int i = 0; i < recentCount; i++)
            {
                BarterTransactionDTO tx = dto.recentTransactions[i];
                if (string.IsNullOrWhiteSpace(tx.offerId) && string.IsNullOrWhiteSpace(tx.offerName))
                    continue;

                int offerHash = ComputeOfferHash(tx.offerId);
                BarterOfferData offer = FindOfferByHash(offerHash);
                AppendLoadedTransaction(offer != null
                    ? new TransactionSnapshot(offer, offerHash)
                    : TransactionSnapshot.FromLegacy(
                        offerHash,
                        tx.offerId,
                        tx.offerName,
                        tx.channelName,
                        tx.costSummary,
                        tx.rewardSummary));
            }

            PublishExchangeStateChanged(PdaExchangeStateChangedSignal.ReasonLoaded);
        }

        private void AutoResolve()
        {
            if (playerInventory == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null &&
                    playerContext.PlayerObject != null)
                {
                    playerContext.PlayerObject.TryGetComponent(out playerInventory);
                }
            }
            if (scanLogSystem == null)
                scanLogSystem = Hecton8.Core.GlobalRegistry.ScanLog;
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
        }

        private void HandleInventoryChanged() => PublishExchangeStateChanged(PdaExchangeStateChangedSignal.ReasonInventoryChanged);
        private void HandleScanLogChanged() => PublishExchangeStateChanged(PdaExchangeStateChangedSignal.ReasonScanLogChanged);

        private void PublishExchangeStateChanged(byte reason)
        {
            PdaExchangeStateChangedSignal signal = new PdaExchangeStateChangedSignal
            {
                SourceId = unchecked((uint)GetInstanceID()),
                Frame = unchecked((uint)Time.frameCount),
                OfferCount = OfferCount,
                RecentTransactionCount = _recentTransactionCount,
                ExecutionStateCount = _executionStateCount,
                Reason = reason,
                Flags = 0
            };

            GlobalSignals.Publish(in signal);
        }

        private bool IsUnlocked(BarterOfferData offer)
        {
            if (offer == null)
                return false;

            return IsUnlocked(GetRequiredScanEntryHash(offer));
        }

        private bool IsUnlocked(uint requiredScanEntryHash)
        {
            if (requiredScanEntryHash == 0u)
                return true;

            return scanLogSystem != null && scanLogSystem.ContainsEntry(requiredScanEntryHash);
        }

        private bool HasReachedLimit(BarterOfferData offer)
        {
            if (offer == null)
                return true;

            return HasReachedLimit(ResolveOfferHash(offer), offer.repeatLimit);
        }

        private bool HasReachedLimit(int offerHash, int repeatLimit)
        {
            if (repeatLimit <= 0)
                return false;

            return offerHash == 0 || GetExecutionCount(offerHash) >= repeatLimit;
        }

        private int GetExecutionCount(BarterOfferData offer)
        {
            int offerHash = ResolveOfferHash(offer);
            if (offerHash == 0)
                return 0;

            return GetExecutionCount(offerHash);
        }

        private int GetExecutionCount(int offerHash)
        {
            int slot = FindExecutionSlot(offerHash);
            return slot >= 0 ? _executionCounts[slot] : 0;
        }

        private void IncrementExecutionCount(int offerHash)
        {
            int slot = FindOrCreateExecutionSlot(offerHash);
            if (slot >= 0)
                _executionCounts[slot] = math.max(0, _executionCounts[slot]) + 1;
        }

        private void SetExecutionCount(int offerHash, int count)
        {
            if (count <= 0)
                return;

            int slot = FindOrCreateExecutionSlot(offerHash);
            if (slot >= 0)
                _executionCounts[slot] = count;
        }

        private int FindExecutionSlot(int offerHash)
        {
            if (offerHash == 0)
                return -1;

            for (int i = 0; i < _executionStateCount; i++)
            {
                if (_executionOfferHashes[i] == offerHash)
                    return i;
            }

            return -1;
        }

        private int FindOrCreateExecutionSlot(int offerHash)
        {
            int existingSlot = FindExecutionSlot(offerHash);
            if (existingSlot >= 0)
                return existingSlot;

            if (offerHash == 0 || _executionStateCount >= BarterDTO.MaxOffers)
                return -1;

            int slot = _executionStateCount++;
            _executionOfferHashes[slot] = offerHash;
            _executionCounts[slot] = 0;
            return slot;
        }

        private void ClearExecutionCounts()
        {
            for (int i = 0; i < _executionStateCount; i++)
            {
                _executionOfferHashes[i] = 0;
                _executionCounts[i] = 0;
            }

            _executionStateCount = 0;
        }

        private int ResolveOfferHash(BarterOfferData offer)
        {
            int index = ResolveOfferIndex(offer);
            return GetOfferHashAt(index);
        }

        private int ResolveOfferIndex(BarterOfferData offer)
        {
            if (offer == null || offerCatalog == null)
                return -1;

            int count = math.min(_catalogRuntimeHashCount, offerCatalog.Count);
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(offerCatalog.GetAt(i), offer))
                    return i;
            }

            return -1;
        }

        private int GetOfferHashAt(int index)
        {
            return index >= 0 && index < _catalogRuntimeHashCount ? _catalogOfferHashes[index] : 0;
        }

        private uint GetRequiredScanEntryHashAt(int index)
        {
            return index >= 0 && index < _catalogRuntimeHashCount ? _catalogRequiredScanEntryHashes[index] : 0u;
        }

        private static int ComputeOfferHash(string offerId)
        {
            return string.IsNullOrWhiteSpace(offerId) ? 0 : LocHash.Compute(offerId);
        }

        public static string ResolveStatusLabel(ExchangeStatus status)
        {
            switch (status)
            {
                case ExchangeStatus.Ready:
                    return StatusReady;
                case ExchangeStatus.NoOffer:
                    return StatusNoOffer;
                case ExchangeStatus.ScanLock:
                    return StatusScanLock;
                case ExchangeStatus.ContractClosed:
                    return StatusContractClosed;
                case ExchangeStatus.InventoryOffline:
                    return StatusInventoryOffline;
                case ExchangeStatus.CostDataInvalid:
                    return StatusCostDataInvalid;
                case ExchangeStatus.InsufficientMaterials:
                    return StatusInsufficientMaterials;
                default:
                    return StatusNoOffer;
            }
        }

        private static string ResolveRelayStatusMessage(ExchangeStatus status)
        {
            switch (status)
            {
                case ExchangeStatus.Ready:
                    return RelayReadyMessage;
                case ExchangeStatus.NoOffer:
                    return RelayNoOfferMessage;
                case ExchangeStatus.ScanLock:
                    return RelayScanLockMessage;
                case ExchangeStatus.ContractClosed:
                    return RelayContractClosedMessage;
                case ExchangeStatus.InventoryOffline:
                    return RelayInventoryOfflineMessage;
                case ExchangeStatus.CostDataInvalid:
                    return RelayCostDataInvalidMessage;
                case ExchangeStatus.InsufficientMaterials:
                    return RelayInsufficientMaterialsMessage;
                default:
                    return RelayNoOfferMessage;
            }
        }

        private void PushRecentTransaction(TransactionSnapshot snapshot)
        {
            int last = math.min(_recentTransactionCount, BarterDTO.MaxRecentTransactions - 1);
            for (int i = last; i > 0; i--)
                _recentTransactions[i] = _recentTransactions[i - 1];

            _recentTransactions[0] = snapshot;
            _recentTransactionCount = math.min(_recentTransactionCount + 1, BarterDTO.MaxRecentTransactions);
        }

        private void AppendLoadedTransaction(TransactionSnapshot snapshot)
        {
            if (_recentTransactionCount >= BarterDTO.MaxRecentTransactions)
                return;

            _recentTransactions[_recentTransactionCount++] = snapshot;
        }

        private void ClearRecentTransactions()
        {
            for (int i = 0; i < _recentTransactionCount; i++)
                _recentTransactions[i] = default;

            _recentTransactionCount = 0;
        }

        private BarterOfferData FindOfferByHash(int offerHash)
        {
            if (offerHash == 0 || offerCatalog == null)
                return null;

            int count = math.min(_catalogRuntimeHashCount, offerCatalog.Count);
            for (int i = 0; i < count; i++)
            {
                BarterOfferData offer = offerCatalog.GetAt(i);
                if (GetOfferHashAt(i) == offerHash)
                    return offer;
            }

            return null;
        }

        private void CacheCatalogRuntimeHashes()
        {
            if (offerCatalog == null)
            {
                ClearCatalogRuntimeHashes();
                return;
            }

            ClearCatalogRuntimeHashes();
            int count = math.min(offerCatalog.Count, BarterDTO.MaxOffers);
            for (int i = 0; i < count; i++)
            {
                BarterOfferData offer = offerCatalog.GetAt(i);
                if (offer != null)
                {
                    _catalogOfferHashes[i] = ComputeOfferHash(offer.offerId);
                    _catalogRequiredScanEntryHashes[i] = ScanEvents.ComputeEntryHash(offer.requiredScanEntryId);
                }
            }

            _catalogRuntimeHashCount = count;
        }

        private void ClearCatalogRuntimeHashes()
        {
            for (int i = 0; i < _catalogRuntimeHashCount; i++)
            {
                _catalogOfferHashes[i] = 0;
                _catalogRequiredScanEntryHashes[i] = 0u;
            }

            _catalogRuntimeHashCount = 0;
        }

        private bool ConsumeBundle(BarterItemAmount[] bundle)
        {
            if (bundle == null || bundle.Length == 0)
                return true;

            for (int i = 0; i < bundle.Length; i++)
            {
                if (bundle[i].item == null)
                    return false;

                int itemHash = bundle[i].item.PersistentHashId;
                if (itemHash == 0 || !playerInventory.TryRemoveQuantity(itemHash, math.max(1, bundle[i].amount)))
                {
                    for (int j = 0; j < i; j++)
                        playerInventory.TryAddItem(bundle[j].item.PersistentHashId, math.max(1, bundle[j].amount));
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

                int itemHash = bundle[i].item.PersistentHashId;
                if (itemHash == 0 || !playerInventory.TryAddItem(itemHash, math.max(1, bundle[i].amount)))
                {
                    for (int j = 0; j < i; j++)
                        playerInventory.TryRemoveQuantity(bundle[j].item.PersistentHashId, math.max(1, bundle[j].amount));
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
                if (bundle[i].item == null)
                    continue;

                int itemHash = bundle[i].item.PersistentHashId;
                if (itemHash != 0)
                    playerInventory.TryAddItem(itemHash, math.max(1, bundle[i].amount));
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
