using System.Text;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Builder Status Overlay")]
    public sealed class BuilderStatusOverlay : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.03f, 0.1f, 0.12f, 0.66f);
        private static readonly Color RuleColor = new Color(0.2f, 0.86f, 0.96f, 0.38f);
        private static readonly Color TitleColor = new Color(0.52f, 0.97f, 0.95f, 0.96f);
        private static readonly Color ValueColor = new Color(0.9f, 0.98f, 1f, 0.96f);
        private static readonly Color DimColor = new Color(0.58f, 0.77f, 0.82f, 0.8f);
        private static readonly Color ReadyColor = new Color(0.34f, 0.95f, 0.74f, 0.96f);
        private static readonly Color WarnColor = new Color(1f, 0.75f, 0.28f, 0.96f);
        private static readonly Color BlockedColor = new Color(1f, 0.45f, 0.4f, 0.96f);

        [Header("References")]
        [SerializeField] private PlayerBuilder playerBuilder;
        [SerializeField] private ConstructionManager constructionManager;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Layout")]
        [SerializeField] private Vector2 anchoredOffset = new Vector2(-198f, -168f);
        [SerializeField] private Vector2 panelSize = new Vector2(308f, 198f);
        [SerializeField] private float refreshInterval = 0.1f;

        private RectTransform _self;
        private CanvasGroup _canvasGroup;
        private Image _panel;
        private Image _headerRule;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _moduleName;
        private TextMeshProUGUI _indexLine;
        private TextMeshProUGUI _queueLine;
        private TextMeshProUGUI _placementLine;
        private TextMeshProUGUI _resourceLine;
        private TextMeshProUGUI _powerLine;
        private TextMeshProUGUI _costLine;
        private TextMeshProUGUI _hintLine;
        private float _nextRefreshAt;
        private int _lastStateHash;
        private readonly StringBuilder _sb = new StringBuilder(192);

        private void OnEnable()
        {
            EnsureBuilt();
            ForceRefresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshAt)
                return;

            _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            RefreshState();
        }

        private void ForceRefresh()
        {
            _nextRefreshAt = 0f;
            _lastStateHash = int.MinValue;
            RefreshState();
        }

        private void AutoResolve()
        {
            if (playerBuilder == null)
                playerBuilder = FindFirstObjectByType<PlayerBuilder>();

            if (inventory == null)
                inventory = FindFirstObjectByType<PlayerInventory>();

            if (constructionManager == null)
                constructionManager = FindFirstObjectByType<ConstructionManager>();

            if (labelFont == null || numericFont == null)
            {
                SuitHUDV4CanvasOverlay overlay = GetComponentInParent<SuitHUDV4CanvasOverlay>();
                if (overlay != null)
                {
                    if (labelFont == null)
                    {
                        TMP_FontAsset overlayFont = ReadFontField(overlay, "labelFont");
                        if (overlayFont != null)
                            labelFont = overlayFont;
                    }

                    if (numericFont == null)
                    {
                        TMP_FontAsset overlayFont = ReadFontField(overlay, "numericFont");
                        if (overlayFont != null)
                            numericFont = overlayFont;
                    }
                }
            }

            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;
        }

        private static TMP_FontAsset ReadFontField(SuitHUDV4CanvasOverlay overlay, string fieldName)
        {
            if (overlay == null || string.IsNullOrEmpty(fieldName))
                return null;

            var field = typeof(SuitHUDV4CanvasOverlay).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return field != null ? field.GetValue(overlay) as TMP_FontAsset : null;
        }

        private void EnsureBuilt()
        {
            if (_self != null)
                return;

            _self = transform as RectTransform;
            if (_self == null)
            {
                RectTransform parentRect = transform.parent as RectTransform;
                if (parentRect == null)
                    return;

                Transform existingRoot = transform.Find("BuilderStatusOverlay_Root");
                if (existingRoot != null)
                {
                    _self = existingRoot as RectTransform;
                }
                else
                {
                    _self = CreateRect("BuilderStatusOverlay_Root", parentRect);
                }
            }

            _self.anchorMin = new Vector2(1f, 1f);
            _self.anchorMax = new Vector2(1f, 1f);
            _self.pivot = new Vector2(1f, 1f);
            _self.anchoredPosition = anchoredOffset;
            _self.sizeDelta = panelSize;

            _canvasGroup = _self.gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _self.gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _panel = _self.gameObject.GetComponent<Image>();
            if (_panel == null)
                _panel = _self.gameObject.AddComponent<Image>();
            _panel.color = PanelColor;
            _panel.raycastTarget = false;

            RectTransform headerRule = CreateRect("HeaderRule", _self);
            headerRule.anchorMin = new Vector2(0f, 1f);
            headerRule.anchorMax = new Vector2(1f, 1f);
            headerRule.pivot = new Vector2(0.5f, 1f);
            headerRule.anchoredPosition = new Vector2(0f, -28f);
            headerRule.sizeDelta = new Vector2(-20f, 1f);
            _headerRule = headerRule.gameObject.AddComponent<Image>();
            _headerRule.color = RuleColor;
            _headerRule.raycastTarget = false;

            _title = CreateText("Title", _self, labelFont, 11f, FontStyles.Bold, TitleColor, TextAlignmentOptions.Left);
            Anchor(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -10f), new Vector2(-14f, 18f));
            _title.text = "CONSTRUCTION STATUS";

            _moduleName = CreateText("ModuleName", _self, labelFont, 18f, FontStyles.Bold, ValueColor, TextAlignmentOptions.Left);
            Anchor(_moduleName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -40f), new Vector2(-14f, 24f));

            _indexLine = CreateText("IndexLine", _self, numericFont, 12f, FontStyles.Bold, DimColor, TextAlignmentOptions.Left);
            Anchor(_indexLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -64f), new Vector2(-14f, 18f));

            _queueLine = CreateText("QueueLine", _self, labelFont, 10f, FontStyles.Bold, DimColor, TextAlignmentOptions.Left);
            Anchor(_queueLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -82f), new Vector2(-14f, 16f));

            _placementLine = CreateText("PlacementLine", _self, numericFont, 13f, FontStyles.Bold, ValueColor, TextAlignmentOptions.Left);
            Anchor(_placementLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -104f), new Vector2(-14f, 18f));

            _resourceLine = CreateText("ResourceLine", _self, numericFont, 13f, FontStyles.Bold, ValueColor, TextAlignmentOptions.Left);
            Anchor(_resourceLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -126f), new Vector2(-14f, 18f));

            _powerLine = CreateText("PowerLine", _self, numericFont, 12f, FontStyles.Bold, DimColor, TextAlignmentOptions.Left);
            Anchor(_powerLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -148f), new Vector2(-14f, 18f));

            _costLine = CreateText("CostLine", _self, labelFont, 11f, FontStyles.Normal, DimColor, TextAlignmentOptions.TopLeft);
            _costLine.enableWordWrapping = true;
            Anchor(_costLine.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 36f), new Vector2(-14f, 58f));

            _hintLine = CreateText("HintLine", _self, numericFont, 11f, FontStyles.Bold, TitleColor, TextAlignmentOptions.Left);
            Anchor(_hintLine.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 12f), new Vector2(-14f, 18f));
        }

        private void RefreshState()
        {
            AutoResolve();
            EnsureBuilt();

            if (_self == null || playerBuilder == null)
                return;

            bool shouldShow = playerBuilder.IsEquipped && playerBuilder.ActiveBuildable != null;
            if (_canvasGroup != null)
                _canvasGroup.alpha = shouldShow ? 1f : 0f;
            if (_self.gameObject.activeSelf != shouldShow)
                _self.gameObject.SetActive(shouldShow);

            if (!shouldShow)
                return;

            BuildableData data = playerBuilder.ActiveBuildable;
            bool hasResources = playerBuilder.HasResourcesForActiveBuildable;
            bool canPlace = playerBuilder.CanPlaceActiveBuildable;
            bool snapped = playerBuilder.IsSnapped;
            int activeIndex = playerBuilder.ActiveBuildableIndex;
            int buildCount = playerBuilder.BuildableCount;
            int builtModuleCount = constructionManager != null ? constructionManager.ModuleCount : 0;
            float powerRating = data != null ? data.powerRating : 0f;
            int stateHash = ComputeStateHash(data, hasResources, canPlace, snapped, activeIndex, buildCount, powerRating, builtModuleCount);

            if (stateHash == _lastStateHash)
                return;

            _lastStateHash = stateHash;
            if (data != null)
                _moduleName.text = data.moduleName.ToUpperInvariant() + " [" + data.FamilyShortCode + "]";
            else
                _moduleName.text = "NO MODULE";
            _indexLine.SetText("MODULE {0}/{1}  //  BUILT {2}", activeIndex + 1, Mathf.Max(1, buildCount), builtModuleCount);
            _queueLine.SetText(BuildQueueHint(activeIndex, buildCount));

            if (!hasResources)
            {
                _placementLine.color = BlockedColor;
                _placementLine.SetText("PLACEMENT // HOLD - MISSING COST");
            }
            else if (!canPlace)
            {
                _placementLine.color = WarnColor;
                _placementLine.SetText(snapped ? "PLACEMENT // SOCKET BLOCKED" : "PLACEMENT // BLOCKED");
            }
            else if (snapped)
            {
                _placementLine.color = ReadyColor;
                _placementLine.SetText("PLACEMENT // SNAPPED READY");
            }
            else
            {
                _placementLine.color = TitleColor;
                _placementLine.SetText("PLACEMENT // READY");
            }

            _resourceLine.color = hasResources ? ReadyColor : WarnColor;
            _resourceLine.SetText(hasResources ? "RESOURCES // READY" : "RESOURCES // INSUFFICIENT");

            if (powerRating > 0f)
                _powerLine.text = $"ROLE // {playerBuilder.GetActiveBuildRoleLabel()}  //  +{powerRating:0}W NET";
            else if (powerRating < 0f)
                _powerLine.text = $"ROLE // {playerBuilder.GetActiveBuildRoleLabel()}  //  {powerRating:0}W LOAD";
            else
                _powerLine.text = $"ROLE // {playerBuilder.GetActiveBuildRoleLabel()}";

            BuildCostSummary(data);
            _hintLine.SetText(playerBuilder.GetActiveBuildAdvice().ToUpperInvariant());
        }

        private int ComputeStateHash(BuildableData data, bool hasResources, bool canPlace, bool snapped, int activeIndex, int buildCount, float powerRating, int builtModuleCount)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (data != null ? data.GetInstanceID() : 0);
                hash = hash * 31 + (hasResources ? 1 : 0);
                hash = hash * 31 + (canPlace ? 1 : 0);
                hash = hash * 31 + (snapped ? 1 : 0);
                hash = hash * 31 + activeIndex;
                hash = hash * 31 + buildCount;
                hash = hash * 31 + builtModuleCount;
                hash = hash * 31 + Mathf.RoundToInt(powerRating * 10f);
                if (data != null && data.buildCost != null && inventory != null)
                {
                    for (int i = 0; i < data.buildCost.Count; i++)
                    {
                        var cost = data.buildCost[i];
                        if (cost == null || cost.item == null)
                            continue;
                        hash = hash * 31 + cost.item.GetInstanceID();
                        hash = hash * 31 + cost.amount;
                        hash = hash * 31 + inventory.CountTotal(cost.item);
                    }
                }
                return hash;
            }
        }

        private string BuildQueueHint(int activeIndex, int buildCount)
        {
            if (_queueLine == null || playerBuilder == null || buildCount <= 0)
                return "CATALOG // OFFLINE";

            BuildableData prev = playerBuilder.GetRelativeBuildable(-1);
            BuildableData next = playerBuilder.GetRelativeBuildable(1);

            _sb.Clear();
            _sb.Append("QUEUE // ");
            _sb.Append(prev != null ? prev.moduleName.ToUpperInvariant() : "NONE");
            _sb.Append("  <  ");
            _sb.Append(activeIndex + 1);
            _sb.Append("  >  ");
            _sb.Append(next != null ? next.moduleName.ToUpperInvariant() : "NONE");
            return _sb.ToString();
        }

        private void BuildCostSummary(BuildableData data)
        {
            if (_costLine == null)
                return;

            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
            {
                _costLine.color = DimColor;
                _costLine.SetText("COST // NONE");
                return;
            }

            _sb.Clear();
            _sb.Append("COST // ");

            for (int i = 0; i < data.buildCost.Count; i++)
            {
                var cost = data.buildCost[i];
                if (cost == null || cost.item == null)
                    continue;

                int owned = inventory != null ? inventory.CountTotal(cost.item) : 0;
                if (_sb.Length > 8)
                    _sb.Append("  |  ");

                _sb.Append(cost.item.itemName);
                _sb.Append(' ');
                _sb.Append(owned);
                _sb.Append('/');
                _sb.Append(cost.amount);
            }

            _costLine.color = playerBuilder != null && playerBuilder.HasResourcesForActiveBuildable ? DimColor : WarnColor;
            _costLine.SetText(_sb.ToString());
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(min.x, max.y);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static TextMeshProUGUI CreateText(string name, RectTransform parent, TMP_FontAsset font, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}
