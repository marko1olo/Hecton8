using System;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay.Atlas6Liability;
using Hecton8.Inventory;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/PDA Exchange System")]
    public sealed class PDAExchangeSystem : MonoBehaviour, ISaveable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001PDAExchangeSystemSignalPushDropCount;
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
        [SerializeField] private Atlas6CorporateLiabilityManager liabilityManager;

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
        private readonly char[] _saveSummaryBuffer = new char[256]; // COLD ALLOC: char[256] - exchange save summary staging buffer - owner: PDAExchangeSystem
        private FixedCharBuffer _notificationBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] - exchange notification staging buffer - owner: PDAExchangeSystem
        private int _executionStateCount;
        private int _catalogRuntimeHashCount;
        private int _recentTransactionCount;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _registeredHotSwapListener;
        private bool _saveRegistered;
        private bool _liabilityEventsRegistered;
        private bool _canTransmit = true;
        private uint _signalSourceId;
        private PlayerInventory _boundInventory;
        private IScanLogService _boundScanLog;
        private IPlayerRuntimeContext _playerRuntime;
        private IScanLogService _scanLogRuntime;
        private ISaveService _saveService;
        private IAtlas6DirectiveCommandSink _atlas6DirectiveCommandSink;
        private ExtractionGatingSystem _subscribedLiabilityExtractionGating;
        private uint _inventorySignalHash;
        private uint _scanLogSourceId;

        public int SavePriority => 36;
        public int LoadPriority => 36;
        public bool CanTransmit => _canTransmit;
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

            _signalSourceId = RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
            RefreshColdRegistryReferences();
            AutoResolve(true);
            CacheCatalogRuntimeHashes();
        }

        private void OnEnable()
        {
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            TryRegisterService();
            AutoResolve(true);
            RefreshSignalFilters();
            CacheCatalogRuntimeHashes();
            TryRegisterSaveParticipant();
            TryRegister();
            TryRegisterLiabilityEvents();
        }

        private void Start()
        {
            TryRegister();
            TryRegisterLiabilityEvents();
        }

        private void OnDisable()
        {
            TryUnregisterLiabilityEvents();
            TryUnregisterSaveParticipant();
            TryUnregister();
            TryUnregisterService();
            TryUnregisterHotSwapListener();
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

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registered = false;
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

        public void AttemptDataTransmission(string dataPayload)
        {
            if (!_canTransmit)
            {
                NotifyWarning("PDA EXCHANGE - ACOUSTIC TETHER SEVERED");
                return;
            }

            if (ContainsAtlas6LiabilityPayload(dataPayload))
            {
                TryBindAtlas6LiabilityManager();
                if (liabilityManager != null)
                    liabilityManager.ReportGhostPDADataUploaded((dataPayload ?? string.Empty).Length * 0.01f);

                NotifyWarning("PDA EXCHANGE - ACTUARIAL LIABILITY FLAGGED");
                return;
            }

            NotifyInfo("PDA EXCHANGE - TRANSMISSION READY");
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

            if (!ConsumeOfferCosts(offer))
            {
                NotifyWarning(RelayCostFailedMessage);
                return false;
            }

            if (!GrantOfferRewards(offer))
            {
                RefundOfferCosts(offer);
                NotifyWarning(RelayCargoFullMessage);
                return false;
            }

            IncrementExecutionCount(offerHash);
            PushRecentTransaction(new TransactionSnapshot(offer, offerHash));

            NotifyInfo(RelayConfirmedMessage);
            PublishExchangeStateChanged(PdaExchangeStateChangedSignal.ReasonExecuted);

            // Notify Atlas-6 directive command sink: barter raises trust.
            IAtlas6DirectiveCommandSink directive = _atlas6DirectiveCommandSink;
            if (directive != null)
                directive.RegisterBarterTransaction();

            // Publish the QuestManager barter event; item collection is handled elsewhere.
            Atlas6Events.TryRaiseBarterAccepted(_executionStateCount);

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

            if (HasReachedLimit(offerHash, offer.RuntimeRepeatLimit))
            {
                status = ExchangeStatus.ContractClosed;
                return false;
            }

            if (offer.HasBlockingRuntimeErrors)
            {
                status = ExchangeStatus.CostDataInvalid;
                return false;
            }

            if (playerInventory == null)
            {
                status = ExchangeStatus.InventoryOffline;
                return false;
            }

            int costCount = offer.RuntimeCostCount;
            for (int i = 0; i < costCount; i++)
            {
                if (!offer.TryGetCost(i, out BarterItemAmount cost))
                {
                    status = ExchangeStatus.CostDataInvalid;
                    return false;
                }

                int itemHash = cost.RuntimeItemHash;
                if (itemHash == 0 || playerInventory.CountTotal(itemHash) < cost.RuntimeAmount)
                {
                    status = ExchangeStatus.InsufficientMaterials;
                    return false;
                }
            }

            return true;
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

        public bool TryAppendOfferCostSummary(Span<char> buffer, ref int cursor, BarterOfferData offer, ReadOnlySpan<char> emptyLabel)
        {
            return TryAppendOfferBundleSummary(buffer, ref cursor, offer, false, emptyLabel);
        }

        public bool TryAppendOfferRewardSummary(Span<char> buffer, ref int cursor, BarterOfferData offer, ReadOnlySpan<char> emptyLabel)
        {
            return TryAppendOfferBundleSummary(buffer, ref cursor, offer, true, emptyLabel);
        }

        private bool TryAppendOfferBundleSummary(
            Span<char> buffer,
            ref int cursor,
            BarterOfferData offer,
            bool rewards,
            ReadOnlySpan<char> emptyLabel)
        {
            int count = offer != null ? (rewards ? offer.RuntimeRewardCount : offer.RuntimeCostCount) : 0;
            if (count <= 0)
                return TryAppend(buffer, ref cursor, emptyLabel);

            for (int i = 0; i < count; i++)
            {
                if (i > 0 && !TryAppend(buffer, ref cursor, "  |  ".AsSpan()))
                    return false;

                bool hasEntry = rewards
                    ? offer.TryGetReward(i, out BarterItemAmount entry)
                    : offer.TryGetCost(i, out entry);
                if (!hasEntry)
                    return false;

                ReadOnlySpan<char> itemName = entry.item != null && !string.IsNullOrWhiteSpace(entry.item.itemName)
                    ? entry.item.itemName.AsSpan()
                    : "UNKNOWN".AsSpan();

                if (!TryAppend(buffer, ref cursor, itemName) ||
                    !TryAppend(buffer, ref cursor, " X".AsSpan()) ||
                    !entry.RuntimeAmount.TryFormat(buffer.Slice(cursor), out int written))
                {
                    return false;
                }

                cursor += written;
            }

            return true;
        }

        private string BuildBundleSummaryForSave(BarterItemAmount[] bundle, string emptyLabel)
        {
            int cursor = 0;
            if (!TryAppendBundleSummary(_saveSummaryBuffer.AsSpan(), ref cursor, bundle, emptyLabel.AsSpan()))
                cursor = math.min(cursor, _saveSummaryBuffer.Length);

            return new string(_saveSummaryBuffer, 0, cursor);
        }

        private string BuildOfferCostSummaryForSave(BarterOfferData offer, string emptyLabel)
        {
            int cursor = 0;
            if (!TryAppendOfferCostSummary(_saveSummaryBuffer.AsSpan(), ref cursor, offer, emptyLabel.AsSpan()))
                cursor = math.min(cursor, _saveSummaryBuffer.Length);

            return new string(_saveSummaryBuffer, 0, cursor);
        }

        private string BuildOfferRewardSummaryForSave(BarterOfferData offer, string emptyLabel)
        {
            int cursor = 0;
            if (!TryAppendOfferRewardSummary(_saveSummaryBuffer.AsSpan(), ref cursor, offer, emptyLabel.AsSpan()))
                cursor = math.min(cursor, _saveSummaryBuffer.Length);

            return new string(_saveSummaryBuffer, 0, cursor);
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
                        offerId = offer.RuntimeOfferId,
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
                    offerId = txOffer != null ? txOffer.RuntimeOfferId : tx.LegacyOfferId,
                    offerName = txOffer != null ? txOffer.RuntimeOfferName : tx.LegacyOfferName,
                    channelName = txOffer != null ? txOffer.RuntimeChannelName : tx.LegacyChannelName,
                    costSummary = txOffer != null ? BuildOfferCostSummaryForSave(txOffer, "NONE") : tx.LegacyCostSummary,
                    rewardSummary = txOffer != null ? BuildOfferRewardSummaryForSave(txOffer, "NONE") : tx.LegacyRewardSummary
                };
            }

            for (int i = recentCount; i < BarterDTO.MaxRecentTransactions; i++)
                data.barter.recentTransactions[i] = default;
        }

        public void LoadFromSaveData(SaveData data)
        {
            ClearExecutionCounts();
            ClearRecentTransactions();
            _canTransmit = true;
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

            TryResolveAtlas6LiabilityManager();
            SyncTransmissionStateFromLiability(false);
            PublishExchangeStateChanged(PdaExchangeStateChangedSignal.ReasonLoaded);
        }

        private void RefreshColdRegistryReferences()
        {
            _playerRuntime = GlobalRegistry.Player;
            _scanLogRuntime = GlobalRegistry.ScanLogService;
            _saveService = GlobalRegistry.Save;
            _atlas6DirectiveCommandSink = GlobalRegistry.Atlas6DirectiveCommandSink;
        }

        private void AutoResolve(bool resolveHud)
        {
            TryBindAtlas6LiabilityManager();

            if (playerInventory == null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntime;
                if (playerContext != null &&
                    playerContext.PlayerObject != null)
                {
                    playerContext.PlayerObject.TryGetComponent(out playerInventory);
                }
            }
            if (resolveHud && hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
        }

        private void TryBindAtlas6LiabilityManager()
        {
            TryResolveAtlas6LiabilityManager();
            SyncTransmissionStateFromLiability(true);
        }

        private bool TryResolveAtlas6LiabilityManager()
        {
            if (liabilityManager == null)
                liabilityManager = Atlas6CorporateLiabilityManager.ActiveRuntimeInstance;

            return liabilityManager != null;
        }

        private void TryRegisterLiabilityEvents()
        {
            if (_liabilityEventsRegistered)
                return;

            TryBindAtlas6LiabilityManager();
            ExtractionGatingSystem extractionGating = liabilityManager != null ? liabilityManager.ExtractionGating : null;
            if (extractionGating == null)
                return;

            extractionGating.OnTetherSeveredSatoRen += HandleSatoRenSilenceProtocol;
            _subscribedLiabilityExtractionGating = extractionGating;
            _liabilityEventsRegistered = true;
            SyncTransmissionStateFromLiability(true);
        }

        private void TryUnregisterLiabilityEvents()
        {
            if (!_liabilityEventsRegistered && _subscribedLiabilityExtractionGating == null)
                return;

            ExtractionGatingSystem extractionGating = _subscribedLiabilityExtractionGating;
            if (extractionGating != null)
                extractionGating.OnTetherSeveredSatoRen -= HandleSatoRenSilenceProtocol;

            _subscribedLiabilityExtractionGating = null;
            _liabilityEventsRegistered = false;
        }

        private void HandleSatoRenSilenceProtocol()
        {
            ApplySatoRenSilenceProtocol(true);
        }

        private void SyncTransmissionStateFromLiability(bool publishStateChanged)
        {
            ExtractionGatingSystem extractionGating = liabilityManager != null ? liabilityManager.ExtractionGating : null;
            if (extractionGating == null ||
                extractionGating.CarrierState != ExtractionCarrierState.TetherSevered)
            {
                return;
            }

            ApplySatoRenSilenceProtocol(publishStateChanged);
        }

        private void ApplySatoRenSilenceProtocol(bool publishStateChanged)
        {
            if (!_canTransmit)
                return;

            _canTransmit = false;
            if (publishStateChanged)
                PublishExchangeStateChanged(PdaExchangeStateChangedSignal.ReasonInventoryChanged, PdaExchangeStateChangedSignal.FlagInventoryDirty);
        }

        private static bool ContainsAtlas6LiabilityPayload(string dataPayload)
        {
            if (string.IsNullOrEmpty(dataPayload))
                return false;

            return dataPayload.IndexOf("Liability", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   dataPayload.IndexOf("Arendt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   dataPayload.IndexOf("Varnek", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   dataPayload.IndexOf("Haldane", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   dataPayload.IndexOf("Sato-Ren", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntime = currentService as IPlayerRuntimeContext;
                    if (playerInventory == null)
                        AutoResolve(false);
                    break;
                case GlobalRegistryServiceSlot.ScanLogRuntime:
                    _scanLogRuntime = currentService as IScanLogService;
                    RefreshSignalFilters();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
                    TryRegisterSaveParticipant();
                    break;
                case GlobalRegistryServiceSlot.Atlas6DirectiveRuntime:
                    _atlas6DirectiveCommandSink = currentService as IAtlas6DirectiveCommandSink;
                    break;
            }
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            if (_saveService == null)
                _saveService = GlobalRegistry.Save;

            if (_saveService == null)
                return;

            _saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _saveRegistered = false;
        }

        public void Tick(float deltaTime)
        {
            if (_liabilityEventsRegistered)
            {
                ExtractionGatingSystem currentExtractionGating = liabilityManager != null
                    ? liabilityManager.ExtractionGating
                    : null;
                if (!ReferenceEquals(_subscribedLiabilityExtractionGating, currentExtractionGating))
                    TryUnregisterLiabilityEvents();
            }

            if (!_liabilityEventsRegistered)
                TryRegisterLiabilityEvents();

            RefreshSignalFilters();
            byte dirtyFlags = 0;
            if (ConsumeInventoryChangedSignals())
                dirtyFlags |= PdaExchangeStateChangedSignal.FlagInventoryDirty;
            if (ConsumeScanLogChangedSignals())
                dirtyFlags |= PdaExchangeStateChangedSignal.FlagScanLogDirty;

            if (dirtyFlags == 0)
                return;

            PublishExchangeStateChanged(
                (dirtyFlags & PdaExchangeStateChangedSignal.FlagScanLogDirty) != 0
                    ? PdaExchangeStateChangedSignal.ReasonScanLogChanged
                    : PdaExchangeStateChangedSignal.ReasonInventoryChanged,
                dirtyFlags);
        }

        private void RefreshSignalFilters()
        {
            PlayerInventory currentInventory = playerInventory != null ? playerInventory : null;
            if (!ReferenceEquals(_boundInventory, currentInventory))
            {
                _boundInventory = currentInventory;
                _inventorySignalHash = currentInventory != null ? unchecked((uint)EntityId.ToULong(currentInventory.GetEntityId())) : 0u;
            }

            IScanLogService currentScanLog = ActiveScanLogService;
            if (!ReferenceEquals(_boundScanLog, currentScanLog))
            {
                _boundScanLog = currentScanLog;
                _scanLogSourceId = currentScanLog != null ? currentScanLog.SourceId : 0u;
            }
        }

        private bool ConsumeInventoryChangedSignals()
        {
            uint inventoryHash = _inventorySignalHash;
            if (inventoryHash == 0u)
                return false;

            ReadOnlySpan<InventoryChangedSignal> signals = SignalBus<InventoryChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                if (signals[i].InventoryHash != inventoryHash)
                    continue;

                return true;
            }

            return false;
        }

        private bool ConsumeScanLogChangedSignals()
        {
            uint sourceId = _scanLogSourceId;
            if (sourceId == 0u)
                return false;

            ReadOnlySpan<ScanLogChangedSignal> signals = SignalBus<ScanLogChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                if (signals[i].SourceId != sourceId)
                    continue;

                return true;
            }

            return false;
        }

        private void PublishExchangeStateChanged(byte reason, byte flags = 0)
        {
            if (_signalSourceId == 0u)
                _signalSourceId = RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));

            PdaExchangeStateChangedSignal signal = new PdaExchangeStateChangedSignal
            {
                SourceId = _signalSourceId,
                Frame = SystemDispatcher.CurrentFrameId,
                OfferCount = OfferCount,
                RecentTransactionCount = _recentTransactionCount,
                ExecutionStateCount = _executionStateCount,
                Reason = reason,
                Flags = flags
            };

            SignalBus<PdaExchangeStateChangedSignal>.TryPushTracked(in signal, ref s_x001PDAExchangeSystemSignalPushDropCount);
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

            IScanLogService scanLog = ActiveScanLogService;
            return scanLog != null && scanLog.ContainsEntry(requiredScanEntryHash);
        }

        private IScanLogService ActiveScanLogService => scanLogSystem != null ? scanLogSystem : _scanLogRuntime;

        private bool HasReachedLimit(BarterOfferData offer)
        {
            if (offer == null)
                return true;

            return HasReachedLimit(ResolveOfferHash(offer), offer.RuntimeRepeatLimit);
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
            {
                int safeCount = math.max(0, _executionCounts[slot]);
                _executionCounts[slot] = safeCount < int.MaxValue
                    ? safeCount + 1
                    : int.MaxValue;
            }
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
            offerCatalog.RefreshValidationState();
            int count = math.min(offerCatalog.Count, BarterDTO.MaxOffers);
            for (int i = 0; i < count; i++)
            {
                BarterOfferData offer = offerCatalog.GetAt(i);
                if (offer == null || !offerCatalog.IsRuntimeOfferSlotValid(i))
                    continue;

                _catalogOfferHashes[i] = ComputeOfferHash(offer.RuntimeOfferId);
                _catalogRequiredScanEntryHashes[i] = ScanEvents.ComputeEntryHash(offer.RuntimeRequiredScanEntryId);
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

        private bool ConsumeOfferCosts(BarterOfferData offer)
        {
            if (offer == null || offer.RuntimeCostCount <= 0 || playerInventory == null)
                return false;

            int count = offer.RuntimeCostCount;
            for (int i = 0; i < count; i++)
            {
                if (!offer.TryGetCost(i, out BarterItemAmount cost))
                    return false;

                int itemHash = cost.RuntimeItemHash;
                if (itemHash == 0 || !playerInventory.TryRemoveQuantity(itemHash, cost.RuntimeAmount))
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (offer.TryGetCost(j, out BarterItemAmount refund))
                            playerInventory.TryAddItem(refund.RuntimeItemHash, refund.RuntimeAmount);
                    }

                    return false;
                }
            }

            return true;
        }

        private bool GrantOfferRewards(BarterOfferData offer)
        {
            if (offer == null || offer.RuntimeRewardCount <= 0 || playerInventory == null)
                return false;

            int count = offer.RuntimeRewardCount;
            for (int i = 0; i < count; i++)
            {
                if (!offer.TryGetReward(i, out BarterItemAmount reward))
                    return false;

                int itemHash = reward.RuntimeItemHash;
                if (itemHash == 0 || !playerInventory.TryAddItem(itemHash, reward.RuntimeAmount))
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (offer.TryGetReward(j, out BarterItemAmount rollback))
                            playerInventory.TryRemoveQuantity(rollback.RuntimeItemHash, rollback.RuntimeAmount);
                    }

                    return false;
                }
            }

            return true;
        }

        private void RefundOfferCosts(BarterOfferData offer)
        {
            if (offer == null || playerInventory == null)
                return;

            int count = offer.RuntimeCostCount;
            for (int i = 0; i < count; i++)
            {
                if (!offer.TryGetCost(i, out BarterItemAmount cost))
                    continue;

                int itemHash = cost.RuntimeItemHash;
                if (itemHash != 0)
                    playerInventory.TryAddItem(itemHash, cost.RuntimeAmount);
            }
        }

        private void NotifyInfo(string message)
        {
            _notificationBuffer.Clear();
            if (hudNotification != null && _notificationBuffer.Append(message.AsSpan()))
                hudNotification.ShowInfo(in _notificationBuffer);
        }

        private void NotifyWarning(string message)
        {
            _notificationBuffer.Clear();
            if (hudNotification != null && _notificationBuffer.Append(message.AsSpan()))
                hudNotification.ShowWarning(in _notificationBuffer);
        }
    }
}
