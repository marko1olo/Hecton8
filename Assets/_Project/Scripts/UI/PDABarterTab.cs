using System;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Gameplay;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Barter Tab")]
    public sealed class PDABarterTab : MonoBehaviour, IPDAEventListener, IUpdatable
    {
        private static readonly Color PanelBg = new Color(0.03f, 0.08f, 0.1f, 0.84f);
        private static readonly Color BoxBg = new Color(0.05f, 0.12f, 0.14f, 0.72f);
        private static readonly Color BoxActive = new Color(0.08f, 0.2f, 0.22f, 0.86f);
        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color Dim = new Color(0.78f, 0.96f, 0.93f, 0.84f);
        private static readonly Color DimLow = new Color(0.55f, 0.74f, 0.71f, 0.72f);
        private static readonly Color Warn = new Color(1f, 0.75f, 0.28f, 0.94f);
        private static readonly Color Rule = new Color(0.46f, 0.98f, 0.94f, 0.18f);
        private static readonly Color ButtonUnavailable = new Color(0.14f, 0.12f, 0.08f, 0.72f);
        // COLD ALLOC: string[16] - prefab child names for barter card construction without interpolation - owner: PDABarterTab
        private static readonly string[] OfferCardNames =
        {
            "OfferCard_0",
            "OfferCard_1",
            "OfferCard_2",
            "OfferCard_3",
            "OfferCard_4",
            "OfferCard_5",
            "OfferCard_6",
            "OfferCard_7",
            "OfferCard_8",
            "OfferCard_9",
            "OfferCard_10",
            "OfferCard_11",
            "OfferCard_12",
            "OfferCard_13",
            "OfferCard_14",
            "OfferCard_15"
        };

        [Header("References")]
        [SerializeField] private PDAExchangeSystem exchangeSystem;
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Settings")]
        [SerializeField] private int barterTabIndex = 3;
        [SerializeField, Min(1)] private int maxVisibleOffers = 5;
        private bool _built;
        private TextMeshProUGUI _summaryText;
        private TextMeshProUGUI _directiveText;
        private TextMeshProUGUI _hintText;
        private RectTransform[] _cardRoots;
        private CanvasGroup[] _cardCanvasGroups;
        private Image[] _cardBgs;
        private TextMeshProUGUI[] _cardTitles;
        private TextMeshProUGUI[] _cardBodies;
        private Image[] _cardButtonBgs;
        private TextMeshProUGUI[] _cardButtonLabels;
        private PDABarterActionButton[] _cardButtons;
        private PDAExchangeSystem.OfferSnapshot[] _snapshotBuffer;
        private PDAExchangeSystem.TransactionSnapshot[] _transactionBuffer;
        private bool _registered;
        private PDAExchangeSystem _boundExchangeSystem;
        private uint _exchangeSourceId;

        private bool IsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPDA != null &&
            playerPDA.ActiveTab == barterTabIndex;

        // ════════════════════════════════════════════════════════════════════════════════
        //  POOLED CHAR BUFFER TEXT OPERATIONS
        // ════════════════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            AutoResolve();
        }

        private void OnEnable()
        {
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            TryRegister();
            RefreshAll(true);
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            Unsubscribe();
            TryUnregister();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            TryUnregister();
            PDAEvents.AssertUnregistered(this, nameof(PDABarterTab));
        }

        private void AutoResolve()
        {
            if (exchangeSystem == null)
                exchangeSystem = GlobalRegistry.PDAExchange;
            if (playerPDA == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    playerPDA = playerContext.PlayerPDA;

                if (playerPDA == null)
                {
                    for (Transform current = transform; current != null; current = current.parent)
                    {
                        if (current.TryGetComponent(out playerPDA))
                            break;
                    }
                }
            }

            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            numericFont = LocalizedFontResolver.ResolveNumericFont(numericFont, labelFont);
            int visibleOfferCapacity = ResolveVisibleOfferCapacity();
            // COLD ALLOC: OfferSnapshot[visibleOfferCapacity] - PDA barter snapshot staging - owner: PDABarterTab
            if (_snapshotBuffer == null || _snapshotBuffer.Length != visibleOfferCapacity)
                _snapshotBuffer = new PDAExchangeSystem.OfferSnapshot[visibleOfferCapacity];
            // COLD ALLOC: TransactionSnapshot[3] - latest barter transaction staging - owner: PDABarterTab
            if (_transactionBuffer == null || _transactionBuffer.Length < 3)
                _transactionBuffer = new PDAExchangeSystem.TransactionSnapshot[3];
        }

        private void Subscribe()
        {
            RefreshExchangeBinding();
            PDAEvents.Register(this);
        }

        private void Unsubscribe()
        {
            _boundExchangeSystem = null;
            _exchangeSourceId = 0u;
            PDAEvents.Unregister(this);
        }

        private void RefreshExchangeBinding()
        {
            PDAExchangeSystem current = exchangeSystem != null ? exchangeSystem : GlobalRegistry.PDAExchange;
            if (current == null)
            {
                exchangeSystem = null;
                _boundExchangeSystem = null;
                _exchangeSourceId = 0u;
                return;
            }

            if (ReferenceEquals(_boundExchangeSystem, current) && _exchangeSourceId != 0u)
                return;

            exchangeSystem = current;
            _boundExchangeSystem = current;
            _exchangeSourceId = GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(current.GetEntityId()));
        }

        public void Tick(float deltaTime)
        {
            ProcessExchangeSignals();
        }

        private void ProcessExchangeSignals()
        {
            if (!IsTabActive)
                return;

            RefreshExchangeBinding();
            if (exchangeSystem == null)
                return;

            uint sourceId = _exchangeSourceId;
            if (sourceId == 0u)
                return;

            ReadOnlySpan<PdaExchangeStateChangedSignal> signals = SignalBus<PdaExchangeStateChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                if (signals[i].SourceId != sourceId)
                    continue;

                RefreshAll(true);
                break;
            }
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            switch ((PDAEventType)payload.EventType)
            {
                case PDAEventType.Opened:
                    HandlePdaOpened(payload.CurrentTab);
                    break;
                case PDAEventType.Closed:
                    HandlePdaClosed(payload.DurationSeconds);
                    break;
                case PDAEventType.TabChanged:
                    HandlePdaTabChanged(payload.PreviousTab, payload.CurrentTab);
                    break;
            }
        }

        private void HandlePdaOpened(int tab)
        {
            if (tab == barterTabIndex)
            {
                RefreshExchangeBinding();
                RefreshAll(true);
            }
        }

        private void HandlePdaClosed(float _) { }

        private void HandlePdaTabChanged(int _, int newTab)
        {
            if (newTab == barterTabIndex)
            {
                RefreshExchangeBinding();
                RefreshAll(true);
            }
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            RectTransform self = transform as RectTransform;
            if (self == null)
                return;

            ClearChildren(self);

            Image bg = EnsureImage(self.gameObject);
            bg.color = PanelBg;
            bg.raycastTarget = false;

            TextMeshProUGUI title = CreateText(self, "Title", labelFont, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, 24f));
            title.color = Primary;
            ApplyStaticText(title, "EXCHANGE RELAY".AsSpan());

            TextMeshProUGUI sub = CreateText(self, "Subtitle", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, 24f));
            sub.color = DimLow;
            ApplyStaticText(sub, "field contracts, relay fabrication, and remote requisition routing".AsSpan());

            CreateRule(self, -52f);

            RectTransform summaryPanel = CreatePanel(self, "SummaryPanel", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -62f), new Vector2(-18f, -150f));
            _summaryText = CreateBody(summaryPanel, "SummaryText", numericFont);
            Stretch(_summaryText.rectTransform, 14f, 14f, 14f, 14f);

            RectTransform directivePanel = CreatePanel(self, "DirectivePanel", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -160f), new Vector2(-18f, -216f));
            _directiveText = CreateBody(directivePanel, "DirectiveText", numericFont);
            Stretch(_directiveText.rectTransform, 14f, 10f, 14f, 10f);
            _directiveText.fontSize = 11.5f;
            _directiveText.color = Primary;

            int visibleOfferCapacity = ResolveVisibleOfferCapacity();
            // COLD ALLOC: UI card cache arrays[visibleOfferCapacity] - one-time PDA barter card cache - owner: PDABarterTab
            _cardRoots = new RectTransform[visibleOfferCapacity];
            _cardCanvasGroups = new CanvasGroup[visibleOfferCapacity];
            _cardBgs = new Image[visibleOfferCapacity];
            _cardTitles = new TextMeshProUGUI[visibleOfferCapacity];
            _cardBodies = new TextMeshProUGUI[visibleOfferCapacity];
            _cardButtonBgs = new Image[visibleOfferCapacity];
            _cardButtonLabels = new TextMeshProUGUI[visibleOfferCapacity];
            _cardButtons = new PDABarterActionButton[visibleOfferCapacity];

            float top = -232f;
            float cardHeight = 90f;
            float gap = 10f;

            for (int i = 0; i < visibleOfferCapacity; i++)
            {
                RectTransform card = CreatePanel(self, ResolveOfferCardName(i), new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(18f, top - i * (cardHeight + gap)), new Vector2(-18f, top - i * (cardHeight + gap) - cardHeight));
                _cardRoots[i] = card;
                _cardCanvasGroups[i] = EnsureCanvasGroup(card.gameObject);
                _cardBgs[i] = EnsureImage(card.gameObject);
                _cardBgs[i].color = BoxBg;

                _cardTitles[i] = CreateText(card, "Title", labelFont, 13f, FontStyles.Bold, TextAlignmentOptions.Left);
                Anchor(_cardTitles[i].rectTransform, new Vector2(0f, 1f), new Vector2(0.72f, 1f), new Vector2(14f, -10f), new Vector2(-8f, 18f));
                _cardTitles[i].color = Primary;

                _cardBodies[i] = CreateBody(card, "Body", numericFont);
                Anchor(_cardBodies[i].rectTransform, new Vector2(0f, 0f), new Vector2(0.72f, 1f), new Vector2(14f, 12f), new Vector2(-8f, -30f));

                RectTransform buttonRoot = CreateRect(card, "Action");
                Anchor(buttonRoot, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-76f, -16f), new Vector2(44f, 16f));
                _cardButtonBgs[i] = EnsureImage(buttonRoot.gameObject);
                _cardButtonBgs[i].color = BoxActive;
                _cardButtonLabels[i] = CreateText(buttonRoot, "ActionLabel", numericFont, 11f, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(_cardButtonLabels[i].rectTransform, 0f, 0f, 0f, 0f);
                _cardButtonLabels[i].color = Dim;

                PDABarterActionButton button = buttonRoot.gameObject.AddComponent<PDABarterActionButton>();
                button.Init(this, i, _cardButtonBgs[i]);
                _cardButtons[i] = button;
            }

            _hintText = CreateText(self, "Hint", labelFont, 10.5f, FontStyles.Italic, TextAlignmentOptions.Center);
            Anchor(_hintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 4f), new Vector2(-24f, 14f));
            _hintText.color = DimLow;
            ApplyStaticText(_hintText, "Relay contracts consume real cargo and route rewards back into your field inventory.".AsSpan());

            _built = true;
        }

        private static string ResolveOfferCardName(int index)
        {
            return (uint)index < (uint)OfferCardNames.Length ? OfferCardNames[index] : "OfferCard";
        }

        private void RefreshAll(bool immediate)
        {
            if (!_built)
                return;

            RefreshSummary();
            RefreshCards();
        }

        private void RefreshSummary()
        {
            if (_summaryText == null)
                return;

            int offerCount = exchangeSystem != null ? exchangeSystem.OfferCount : 0;
            int snapshotCount = exchangeSystem != null ? exchangeSystem.CopySnapshots(_snapshotBuffer) : 0;
            int ready = 0;
            int locked = 0;
            int closed = 0;

            for (int i = 0; i < snapshotCount; i++)
            {
                PDAExchangeSystem.OfferSnapshot snapshot = _snapshotBuffer[i];
                if (snapshot.Offer == null)
                    continue;

                if (!snapshot.Unlocked) locked++;
                else if (snapshot.Status == PDAExchangeSystem.ExchangeStatus.ContractClosed) closed++;
                else if (snapshot.CanExecute) ready++;
            }

            int txCount = exchangeSystem != null ? exchangeSystem.CopyRecentTransactions(_transactionBuffer) : 0;
            ApplySummaryText(offerCount, ready, locked, closed, txCount);

            if (_directiveText != null)
                ApplyStaticText(_directiveText, ResolveDirectiveText(ready, locked, closed));

            if (_hintText != null)
                RefreshHintText(txCount > 0 ? _transactionBuffer[0] : default, txCount > 0);
        }

        private void RefreshCards()
        {
            int count = exchangeSystem != null ? exchangeSystem.CopySnapshots(_snapshotBuffer) : 0;
            for (int i = 0; i < _cardRoots.Length; i++)
            {
                bool visible = i < count && _snapshotBuffer[i].Offer != null;
                SetCanvasGroupVisible(_cardCanvasGroups[i], visible);
                if (!visible)
                    continue;

                PDAExchangeSystem.OfferSnapshot snapshot = _snapshotBuffer[i];
                BarterOfferData offer = snapshot.Offer;

                ApplyOfferTitle(_cardTitles[i], offer);
                ApplyOfferBody(_cardBodies[i], snapshot, offer);

                _cardBgs[i].color = snapshot.CanExecute ? BoxActive : BoxBg;
                _cardButtonBgs[i].color = snapshot.CanExecute ? BoxActive : ButtonUnavailable;
                _cardButtonLabels[i].color = snapshot.CanExecute ? Primary : Warn;
                ApplyStaticText(_cardButtonLabels[i], ResolveActionLabel(snapshot));

                PDABarterActionButton button = _cardButtons[i];
                if (button != null)
                    button.SetVisualState(_cardButtonBgs[i].color);
            }
        }

        private static void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        internal void InvokeOffer(int index)
        {
            if (exchangeSystem == null)
                return;

            exchangeSystem.TryExecuteOffer(index);
            RefreshAll(true);
        }

        private int ResolveVisibleOfferCapacity()
        {
            return math.max(1, maxVisibleOffers);
        }

        private static ReadOnlySpan<char> ResolveActionLabel(PDAExchangeSystem.OfferSnapshot snapshot)
        {
            if (!snapshot.Unlocked)
                return "LOCKED".AsSpan();
            if (snapshot.Status == PDAExchangeSystem.ExchangeStatus.ContractClosed)
                return "CLOSED".AsSpan();
            return snapshot.CanExecute ? "EXECUTE".AsSpan() : "UNAVAILABLE".AsSpan();
        }

        private static ReadOnlySpan<char> ResolveDirectiveText(int ready, int locked, int closed)
        {
            if (ready > 0)
                return "Exchange relay has executable requisitions. Convert surplus field cargo into mission-ready support packages before the next sortie.".AsSpan();
            if (locked > 0)
                return "Relay is waiting on additional scan intel before higher-value exchange contracts can be routed into the field stack.".AsSpan();
            if (closed > 0)
                return "Current relay queue is partially exhausted. Archive more intel or recover more cargo to refresh contract value.".AsSpan();
            return "Exchange relay is online but no active requisitions are currently actionable.".AsSpan();
        }

        private void RefreshHintText(PDAExchangeSystem.TransactionSnapshot latest, bool hasLatest)
        {
            if (_hintText == null)
                return;

            if (hasLatest)
            {
                string offerName = ResolveTransactionOfferName(latest);
                if (!string.IsNullOrWhiteSpace(offerName))
                {
                    ApplyTransactionHintText(latest, offerName);
                    return;
                }
            }

            ApplyStaticText(_hintText, "Relay contracts consume real cargo and route rewards back into your field inventory.".AsSpan());
        }

        private static string ResolveTransactionOfferName(PDAExchangeSystem.TransactionSnapshot transaction)
        {
            if (transaction.Offer != null && !string.IsNullOrWhiteSpace(transaction.Offer.offerName))
                return transaction.Offer.offerName;

            return transaction.LegacyOfferName;
        }

        private void ApplySummaryText(int offerCount, int ready, int locked, int closed, int transactionCount)
        {
            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                Span<char> buffer = lease.Buffer.AsSpan();
                int cursor = 0;
                bool written =
                    TryAppendLine(buffer, ref cursor, "EXCHANGE BACKBONE".AsSpan()) &&
                    TryAppend(buffer, ref cursor, "CATALOG      ".AsSpan()) &&
                    TryAppendInt(buffer, ref cursor, offerCount) &&
                    TryAppendNewLine(buffer, ref cursor) &&
                    TryAppend(buffer, ref cursor, "READY        ".AsSpan()) &&
                    TryAppendInt(buffer, ref cursor, ready) &&
                    TryAppendNewLine(buffer, ref cursor) &&
                    TryAppend(buffer, ref cursor, "LOCKED       ".AsSpan()) &&
                    TryAppendInt(buffer, ref cursor, locked) &&
                    TryAppendNewLine(buffer, ref cursor) &&
                    TryAppend(buffer, ref cursor, "FULFILLED    ".AsSpan()) &&
                    TryAppendInt(buffer, ref cursor, closed) &&
                    TryAppendNewLine(buffer, ref cursor);

                if (written && transactionCount > 0)
                {
                    PDAExchangeSystem.TransactionSnapshot tx = _transactionBuffer[0];
                    written =
                        TryAppend(buffer, ref cursor, "LATEST       ".AsSpan()) &&
                        TryAppendUpperInvariant(buffer, ref cursor, ResolveTransactionOfferName(tx), "UNKNOWN".AsSpan()) &&
                        TryAppendNewLine(buffer, ref cursor) &&
                        TryAppend(buffer, ref cursor, "OUTPUT       ".AsSpan()) &&
                        TryAppendTransactionRewardSummary(buffer, ref cursor, tx, "NONE".AsSpan()) &&
                        TryAppendNewLine(buffer, ref cursor);
                }

                if (written)
                    _summaryText.SetCharArray(lease.Buffer, 0, cursor);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private void ApplyOfferTitle(TextMeshProUGUI label, BarterOfferData offer)
        {
            if (label == null || offer == null || !CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                Span<char> buffer = lease.Buffer.AsSpan();
                int cursor = 0;
                if (TryAppendUpperInvariant(buffer, ref cursor, offer.offerName, "UNKNOWN".AsSpan()) &&
                    TryAppend(buffer, ref cursor, "  //  ".AsSpan()) &&
                    TryAppendUpperInvariant(buffer, ref cursor, offer.channelName, "FIELD".AsSpan()))
                {
                    label.SetCharArray(lease.Buffer, 0, cursor);
                }
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private void ApplyOfferBody(TextMeshProUGUI label, PDAExchangeSystem.OfferSnapshot snapshot, BarterOfferData offer)
        {
            if (label == null || exchangeSystem == null || offer == null || !CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                Span<char> buffer = lease.Buffer.AsSpan();
                int cursor = 0;
                bool written =
                    TryAppend(buffer, ref cursor, "REQ  ".AsSpan()) &&
                    exchangeSystem.TryAppendBundleSummary(buffer, ref cursor, offer.costs, "NONE".AsSpan()) &&
                    TryAppendNewLine(buffer, ref cursor) &&
                    TryAppend(buffer, ref cursor, "OUT  ".AsSpan()) &&
                    exchangeSystem.TryAppendBundleSummary(buffer, ref cursor, offer.rewards, "NO PAYOUT".AsSpan()) &&
                    TryAppendNewLine(buffer, ref cursor);

                if (written && snapshot.HasRequiredScanEntry)
                {
                    written =
                        TryAppend(buffer, ref cursor, "GATE #".AsSpan()) &&
                        TryAppendHex8(buffer, ref cursor, snapshot.RequiredScanEntryHash) &&
                        TryAppendNewLine(buffer, ref cursor);
                }

                written = written &&
                    TryAppend(buffer, ref cursor, "STAT ".AsSpan()) &&
                    TryAppendStatusLabel(buffer, ref cursor, snapshot.Status);

                if (written)
                    label.SetCharArray(lease.Buffer, 0, cursor);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private void ApplyTransactionHintText(PDAExchangeSystem.TransactionSnapshot latest, string offerName)
        {
            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                Span<char> buffer = lease.Buffer.AsSpan();
                int cursor = 0;
                if (TryAppend(buffer, ref cursor, "Last confirmed contract: ".AsSpan()) &&
                    TryAppendUpperInvariant(buffer, ref cursor, offerName, "UNKNOWN".AsSpan()) &&
                    TryAppend(buffer, ref cursor, "  //  ".AsSpan()) &&
                    TryAppendTransactionRewardSummary(buffer, ref cursor, latest, "NO OUTPUT".AsSpan()))
                {
                    _hintText.SetCharArray(lease.Buffer, 0, cursor);
                }
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static void ApplyStaticText(TextMeshProUGUI label, ReadOnlySpan<char> text)
        {
            if (label == null || !CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                Span<char> buffer = lease.Buffer.AsSpan();
                int cursor = 0;
                if (TryAppend(buffer, ref cursor, text))
                    label.SetCharArray(lease.Buffer, 0, cursor);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private bool TryAppendTransactionRewardSummary(
            Span<char> buffer,
            ref int cursor,
            PDAExchangeSystem.TransactionSnapshot transaction,
            ReadOnlySpan<char> emptyLabel)
        {
            if (transaction.Offer != null && exchangeSystem != null)
                return exchangeSystem.TryAppendBundleSummary(buffer, ref cursor, transaction.Offer.rewards, emptyLabel);

            ReadOnlySpan<char> reward = string.IsNullOrWhiteSpace(transaction.LegacyRewardSummary)
                ? emptyLabel
                : transaction.LegacyRewardSummary.AsSpan();
            return TryAppend(buffer, ref cursor, reward);
        }

        private static bool TryAppendStatusLabel(Span<char> buffer, ref int cursor, PDAExchangeSystem.ExchangeStatus status)
        {
            switch (status)
            {
                case PDAExchangeSystem.ExchangeStatus.Ready:
                    return TryAppend(buffer, ref cursor, "READY".AsSpan());
                case PDAExchangeSystem.ExchangeStatus.NoOffer:
                    return TryAppend(buffer, ref cursor, "NO OFFER".AsSpan());
                case PDAExchangeSystem.ExchangeStatus.ScanLock:
                    return TryAppend(buffer, ref cursor, "SCAN LOCK".AsSpan());
                case PDAExchangeSystem.ExchangeStatus.ContractClosed:
                    return TryAppend(buffer, ref cursor, "CONTRACT CLOSED".AsSpan());
                case PDAExchangeSystem.ExchangeStatus.InventoryOffline:
                    return TryAppend(buffer, ref cursor, "INVENTORY OFFLINE".AsSpan());
                case PDAExchangeSystem.ExchangeStatus.CostDataInvalid:
                    return TryAppend(buffer, ref cursor, "COST DATA INVALID".AsSpan());
                case PDAExchangeSystem.ExchangeStatus.InsufficientMaterials:
                    return TryAppend(buffer, ref cursor, "INSUFFICIENT MATERIALS".AsSpan());
                default:
                    return TryAppend(buffer, ref cursor, "NO OFFER".AsSpan());
            }
        }

        private static bool TryAppendUpperInvariant(Span<char> buffer, ref int cursor, string value, ReadOnlySpan<char> fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return TryAppend(buffer, ref cursor, fallback);

            for (int i = 0; i < value.Length; i++)
            {
                if (cursor >= buffer.Length)
                    return false;

                char c = value[i];
                buffer[cursor++] = c >= 'a' && c <= 'z' ? (char)(c - 32) : c;
            }

            return true;
        }

        private static bool TryAppendHex8(Span<char> buffer, ref int cursor, uint value)
        {
            const string Hex = "0123456789ABCDEF";
            for (int shift = 28; shift >= 0; shift -= 4)
            {
                if (cursor >= buffer.Length)
                    return false;

                buffer[cursor++] = Hex[(int)((value >> shift) & 0xFu)];
            }

            return true;
        }

        private static bool TryAppendLine(Span<char> buffer, ref int cursor, ReadOnlySpan<char> value)
        {
            return TryAppend(buffer, ref cursor, value) && TryAppendNewLine(buffer, ref cursor);
        }

        private static bool TryAppendNewLine(Span<char> buffer, ref int cursor)
        {
            if (cursor >= buffer.Length)
                return false;

            buffer[cursor++] = '\n';
            return true;
        }

        private static bool TryAppendInt(Span<char> buffer, ref int cursor, int value)
        {
            if ((uint)cursor > (uint)buffer.Length ||
                !value.TryFormat(buffer.Slice(cursor), out int written))
            {
                return false;
            }

            cursor += written;
            return true;
        }

        private static bool TryAppend(Span<char> buffer, ref int cursor, ReadOnlySpan<char> value)
        {
            if (cursor < 0 || cursor + value.Length > buffer.Length)
                return false;

            value.CopyTo(buffer.Slice(cursor));
            cursor += value.Length;
            return true;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image bg = EnsureImage(rect.gameObject);
            bg.color = BoxBg;
            bg.raycastTarget = false;
            return rect;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.TryGetComponent(out RectTransform rect);
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image EnsureImage(GameObject go)
        {
            if (!go.TryGetComponent(out Image image))
                image = go.AddComponent<Image>();
            return image;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            if (!go.TryGetComponent(out CanvasGroup canvasGroup))
                canvasGroup = go.AddComponent<CanvasGroup>();
            return canvasGroup;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.TryGetComponent(out RectTransform rect);
            rect.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            LocalizedTMPAutoSizer.Configure(text, size * 0.72f, size, TextOverflowModes.Ellipsis, TextWrappingModes.Normal);
            return text;
        }

        private static TextMeshProUGUI CreateBody(Transform parent, string name, TMP_FontAsset font)
        {
            TextMeshProUGUI text = CreateText(parent, name, font, 11f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            text.color = Dim;
            return text;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void CreateRule(Transform parent, float topOffset)
        {
            RectTransform line = CreateRect(parent, "Rule");
            line.anchorMin = new Vector2(0.03f, 1f);
            line.anchorMax = new Vector2(0.97f, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.anchoredPosition = new Vector2(0f, topOffset);
            line.sizeDelta = new Vector2(0f, 1f);
            Image img = EnsureImage(line.gameObject);
            img.color = Rule;
            img.raycastTarget = false;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    internal sealed class PDABarterActionButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PDABarterTab _tab;
        private int _offerIndex;
        private Image _bg;
        private Color _normalColor;
        private Color _hoverColor;

        public void Init(PDABarterTab tab, int offerIndex, Image bg)
        {
            _tab = tab;
            _offerIndex = offerIndex;
            _bg = bg;
            SetVisualState(bg != null ? bg.color : Color.white);
        }

        public void SetVisualState(Color normal)
        {
            _normalColor = normal;
            _hoverColor = normal * 1.15f;
            _hoverColor.a = normal.a;

            if (_bg != null)
                _bg.color = normal;
        }

        public void OnPointerClick(PointerEventData eventData) => _tab?.InvokeOffer(_offerIndex);
        public void OnPointerEnter(PointerEventData eventData) { if (_bg != null) _bg.color = _hoverColor; }
        public void OnPointerExit(PointerEventData eventData) { if (_bg != null) _bg.color = _normalColor; }
    }
}
