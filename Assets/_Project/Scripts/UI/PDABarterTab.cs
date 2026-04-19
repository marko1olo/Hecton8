using System.Text;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Barter Tab")]
    public sealed class PDABarterTab : MonoBehaviour
    {
        private static readonly Color PanelBg = new Color(0.03f, 0.08f, 0.1f, 0.84f);
        private static readonly Color BoxBg = new Color(0.05f, 0.12f, 0.14f, 0.72f);
        private static readonly Color BoxActive = new Color(0.08f, 0.2f, 0.22f, 0.86f);
        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color Dim = new Color(0.78f, 0.96f, 0.93f, 0.84f);
        private static readonly Color DimLow = new Color(0.55f, 0.74f, 0.71f, 0.72f);
        private static readonly Color Warn = new Color(1f, 0.75f, 0.28f, 0.94f);
        private static readonly Color Rule = new Color(0.46f, 0.98f, 0.94f, 0.18f);

        [Header("References")]
        [SerializeField] private PDAExchangeSystem exchangeSystem;
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Settings")]
        [SerializeField] private int barterTabIndex = 3;
        [SerializeField] private int maxVisibleOffers = 5;
        private bool _built;
        private TextMeshProUGUI _summaryText;
        private TextMeshProUGUI _directiveText;
        private TextMeshProUGUI _hintText;
        private RectTransform[] _cardRoots;
        private Image[] _cardBgs;
        private TextMeshProUGUI[] _cardTitles;
        private TextMeshProUGUI[] _cardBodies;
        private Image[] _cardButtonBgs;
        private TextMeshProUGUI[] _cardButtonLabels;
        private PDAExchangeSystem.OfferSnapshot[] _snapshotBuffer;
        private PDAExchangeSystem.TransactionSnapshot[] _transactionBuffer;
        private readonly StringBuilder _sb = new StringBuilder(512);
        private PDAExchangeSystem _subscribedExchangeSystem;

        private bool IsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPDA != null &&
            playerPDA.ActiveTab == barterTabIndex;

        // ════════════════════════════════════════════════════════════════════════════════
        //  CACHED STRING OPERATIONS — ZERO GC
        // ════════════════════════════════════════════════════════════════════════════════

        private static readonly string[] _cachedUpperStrings = new string[16];

        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Простой hash для кэширования (не криптографический)
            int hash = input.GetHashCode() & 0xF; // Маска для индекса 0-15

            string cached = _cachedUpperStrings[hash];
            if (cached != null && string.Equals(cached, input, System.StringComparison.OrdinalIgnoreCase))
                return cached;

            // Создаем новую строку и кэшируем
            string upper = input.ToUpperInvariant();
            _cachedUpperStrings[hash] = upper;
            return upper;
        }

        private void Awake()
        {
            AutoResolve();
        }

        private void OnEnable()
        {
            AutoResolve();
            EnsureBuilt();
            Subscribe();
            RefreshAll(true);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void AutoResolve()
        {
            if (exchangeSystem == null)
                exchangeSystem = PDAExchangeSystem.Instance;
            if (playerPDA == null)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    playerPDA = playerTransform.GetComponentInChildren<PlayerPDA>(true);
                }

                if (playerPDA == null)
                    playerPDA = GetComponentInParent<PlayerPDA>();
            }
            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;
            if (_snapshotBuffer == null || _snapshotBuffer.Length != Mathf.Max(1, maxVisibleOffers))
                _snapshotBuffer = new PDAExchangeSystem.OfferSnapshot[Mathf.Max(1, maxVisibleOffers)];
            if (_transactionBuffer == null || _transactionBuffer.Length < 3)
                _transactionBuffer = new PDAExchangeSystem.TransactionSnapshot[3];
        }

        private void Subscribe()
        {
            RefreshExchangeBinding();
            PDAEvents.OnOpened += HandlePdaOpened;
            PDAEvents.OnClosed += HandlePdaClosed;
            PDAEvents.OnTabChanged += HandlePdaTabChanged;
        }

        private void Unsubscribe()
        {
            UnsubscribeExchangeSystem();
            PDAEvents.OnOpened -= HandlePdaOpened;
            PDAEvents.OnClosed -= HandlePdaClosed;
            PDAEvents.OnTabChanged -= HandlePdaTabChanged;
        }

        private void RefreshExchangeBinding()
        {
            if (exchangeSystem == null)
                exchangeSystem = PDAExchangeSystem.Instance;

            if (_subscribedExchangeSystem == exchangeSystem)
                return;

            UnsubscribeExchangeSystem();
            if (exchangeSystem == null)
                return;

            exchangeSystem.ExchangeStateChanged += HandleExchangeStateChanged;
            _subscribedExchangeSystem = exchangeSystem;
        }

        private void UnsubscribeExchangeSystem()
        {
            if (_subscribedExchangeSystem == null)
                return;

            _subscribedExchangeSystem.ExchangeStateChanged -= HandleExchangeStateChanged;
            _subscribedExchangeSystem = null;
        }

        private void HandleExchangeStateChanged()
        {
            if (IsTabActive)
                RefreshAll(true);
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
            title.SetText("EXCHANGE RELAY");

            TextMeshProUGUI sub = CreateText(self, "Subtitle", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, 24f));
            sub.color = DimLow;
            sub.SetText("field contracts, relay fabrication, and remote requisition routing");

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

            _cardRoots = new RectTransform[maxVisibleOffers];
            _cardBgs = new Image[maxVisibleOffers];
            _cardTitles = new TextMeshProUGUI[maxVisibleOffers];
            _cardBodies = new TextMeshProUGUI[maxVisibleOffers];
            _cardButtonBgs = new Image[maxVisibleOffers];
            _cardButtonLabels = new TextMeshProUGUI[maxVisibleOffers];

            float top = -232f;
            float cardHeight = 90f;
            float gap = 10f;

            for (int i = 0; i < maxVisibleOffers; i++)
            {
                RectTransform card = CreatePanel(self, $"OfferCard_{i}", new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(18f, top - i * (cardHeight + gap)), new Vector2(-18f, top - i * (cardHeight + gap) - cardHeight));
                _cardRoots[i] = card;
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
            }

            _hintText = CreateText(self, "Hint", labelFont, 10.5f, FontStyles.Italic, TextAlignmentOptions.Center);
            Anchor(_hintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 4f), new Vector2(-24f, 14f));
            _hintText.color = DimLow;
            _hintText.SetText("Relay contracts consume real cargo and route rewards back into your field inventory.");

            _built = true;
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
                else if (snapshot.Status == "CONTRACT CLOSED") closed++;
                else if (snapshot.CanExecute) ready++;
            }

            _sb.Length = 0;
            _sb.AppendLine("EXCHANGE BACKBONE");
            _sb.Append("CATALOG      ").Append(offerCount).AppendLine();
            _sb.Append("READY        ").Append(ready).AppendLine();
            _sb.Append("LOCKED       ").Append(locked).AppendLine();
            _sb.Append("FULFILLED    ").Append(closed).AppendLine();
            int txCount = exchangeSystem != null ? exchangeSystem.CopyRecentTransactions(_transactionBuffer) : 0;
            if (txCount > 0)
            {
                PDAExchangeSystem.TransactionSnapshot tx = _transactionBuffer[0];
                _sb.Append("LATEST       ").Append(string.IsNullOrWhiteSpace(tx.OfferName) ? "UNKNOWN" : CachedToUpperInvariant(tx.OfferName)).AppendLine();
                _sb.Append("OUTPUT       ").Append(string.IsNullOrWhiteSpace(tx.RewardSummary) ? "NONE" : tx.RewardSummary).AppendLine();
            }
            _summaryText.SetText(_sb);

            if (_directiveText != null)
                _directiveText.SetText(GetDirectiveText(ready, locked, closed));

            if (_hintText != null)
                _hintText.SetText(GetHintText(txCount > 0 ? _transactionBuffer[0] : default));
        }

        private void RefreshCards()
        {
            int count = exchangeSystem != null ? exchangeSystem.CopySnapshots(_snapshotBuffer) : 0;
            for (int i = 0; i < _cardRoots.Length; i++)
            {
                bool visible = i < count && _snapshotBuffer[i].Offer != null;
                _cardRoots[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;

                PDAExchangeSystem.OfferSnapshot snapshot = _snapshotBuffer[i];
                BarterOfferData offer = snapshot.Offer;

                // ZERO-GC: Use StringBuilder to avoid string concatenation allocation
                _sb.Clear();
                _sb.Append(CachedToUpperInvariant(offer.offerName)).Append("  //  ").Append(CachedToUpperInvariant(offer.channelName));
                _cardTitles[i].text = _sb.ToString();
                
                _sb.Clear();
                _sb.Append("REQ  ").Append(exchangeSystem.BuildBundleSummary(offer.costs, "NONE")).AppendLine();
                _sb.Append("OUT  ").Append(exchangeSystem.BuildBundleSummary(offer.rewards, "NO PAYOUT")).AppendLine();
                if (!string.IsNullOrWhiteSpace(offer.requiredScanEntryId))
                    _sb.Append("GATE ").Append(CachedToUpperInvariant(offer.requiredScanEntryId)).AppendLine();
                _sb.Append("STAT ").Append(snapshot.Status);
                _cardBodies[i].SetText(_sb);

                _cardBgs[i].color = snapshot.CanExecute ? BoxActive : BoxBg;
                _cardButtonBgs[i].color = snapshot.CanExecute ? BoxActive : new Color(0.14f, 0.12f, 0.08f, 0.72f);
                _cardButtonLabels[i].color = snapshot.CanExecute ? Primary : Warn;
                _cardButtonLabels[i].SetText(GetActionLabel(snapshot));

                PDABarterActionButton button = _cardButtonBgs[i].GetComponent<PDABarterActionButton>();
                if (button != null)
                    button.SetVisualState(_cardButtonBgs[i].color);
            }
        }

        internal void InvokeOffer(int index)
        {
            if (exchangeSystem == null)
                return;

            exchangeSystem.TryExecuteOffer(index);
            RefreshAll(true);
        }

        private static string GetActionLabel(PDAExchangeSystem.OfferSnapshot snapshot)
        {
            if (!snapshot.Unlocked)
                return "LOCKED";
            if (snapshot.Status == "CONTRACT CLOSED")
                return "CLOSED";
            return snapshot.CanExecute ? "EXECUTE" : "UNAVAILABLE";
        }

        private static string GetDirectiveText(int ready, int locked, int closed)
        {
            if (ready > 0)
                return "Exchange relay has executable requisitions. Convert surplus field cargo into mission-ready support packages before the next sortie.";
            if (locked > 0)
                return "Relay is waiting on additional scan intel before higher-value exchange contracts can be routed into the field stack.";
            if (closed > 0)
                return "Current relay queue is partially exhausted. Archive more intel or recover more cargo to refresh contract value.";
            return "Exchange relay is online but no active requisitions are currently actionable.";
        }

        private string GetHintText(PDAExchangeSystem.TransactionSnapshot latest)
        {
            if (!string.IsNullOrWhiteSpace(latest.OfferName))
            {
                return "Last confirmed contract: " +
                       CachedToUpperInvariant(latest.OfferName) +
                       "  //  " +
                       (string.IsNullOrWhiteSpace(latest.RewardSummary) ? "NO OUTPUT" : latest.RewardSummary);
            }

            return "Relay contracts consume real cargo and route rewards back into your field inventory.";
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
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image EnsureImage(GameObject go)
        {
            Image image = go.GetComponent<Image>();
            if (image == null)
                image = go.AddComponent<Image>();
            return image;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
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
                    Object.Destroy(child.gameObject);
                else
                    Object.DestroyImmediate(child.gameObject);
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
