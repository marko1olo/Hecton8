// ============================================================================
// HECTON-8 — HUDNotification.cs
// Кратковременные уведомления на HUD (инвентарь полон, и т.д.)
// Sibling к HUD_V4_CanvasRoot на Suit_HUD_Canvas.
// ============================================================================

using Hecton8.Inventory;
using Hecton8.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/HUD Notification")]
    public sealed class HUDNotification : MonoBehaviour
    {
        [Header("── Settings ──────────────────────────────────")]
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeSpeed = 4f;
        [SerializeField] private TMP_FontAsset font;

        private static readonly Color WarningBg = new Color(0.12f, 0.06f, 0.02f, 0.7f);
        private static readonly Color WarningText = new Color(1f, 0.74f, 0.22f, 0.95f);
        private static readonly Color InfoBg = new Color(0.02f, 0.08f, 0.1f, 0.7f);
        private static readonly Color InfoText = new Color(0.46f, 0.98f, 0.94f, 0.9f);

        private RectTransform _notifRoot;
        private Image _notifBg;
        private TextMeshProUGUI _notifText;
        private CanvasGroup _canvasGroup;
        private float _timer;
        private float _currentAlpha;
        private bool _built;
        private PlayerInventory _inventory;

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyPreviewSafeState();
        }
#endif

        private void OnEnable()
        {
            if (font == null) font = TMP_Settings.defaultFontAsset;

            _inventory = FindFirstObjectByType<PlayerInventory>();
            if (_inventory != null)
                _inventory.InventoryFull += OnInventoryFull;

            EnsureBuilt();
        }

        private void OnDisable()
        {
            if (_inventory != null)
                _inventory.InventoryFull -= OnInventoryFull;
        }

        private void LateUpdate()
        {
            if (_notifRoot == null) return;

            if (_timer > 0f)
            {
                _timer -= Time.deltaTime;
                _currentAlpha = Mathf.Lerp(_currentAlpha, 1f,
                    1f - Mathf.Exp(-fadeSpeed * Time.deltaTime));
            }
            else
            {
                _currentAlpha = Mathf.Lerp(_currentAlpha, 0f,
                    1f - Mathf.Exp(-fadeSpeed * Time.deltaTime));

                if (_currentAlpha < 0.01f)
                {
                    _currentAlpha = 0f;
                    _notifRoot.gameObject.SetActive(false);
                }
            }

            if (_canvasGroup != null)
                _canvasGroup.alpha = _currentAlpha;
        }

        public void ShowWarning(string message)
        {
            EnsureBuilt();
            _notifBg.color = WarningBg;
            _notifText.text = message;
            _notifText.color = WarningText;
            _timer = displayDuration;
            _currentAlpha = 0f;
            _notifRoot.gameObject.SetActive(true);
        }

        public void ShowInfo(string message)
        {
            EnsureBuilt();
            _notifBg.color = InfoBg;
            _notifText.text = message;
            _notifText.color = InfoText;
            _timer = displayDuration;
            _currentAlpha = 0f;
            _notifRoot.gameObject.SetActive(true);
        }

        private void OnInventoryFull(ItemData item)
        {
            string name = item != null ? item.itemName : "ITEM";
            ShowWarning($"INVENTORY FULL — CANNOT STORE {name.ToUpperInvariant()}");
        }

        private void EnsureBuilt()
        {
            if (_built) return;

            RectTransform self = transform as RectTransform;
            if (self == null) return;

            self.anchorMin = new Vector2(0.5f, 1f);
            self.anchorMax = new Vector2(0.5f, 1f);
            self.pivot = new Vector2(0.5f, 1f);
            self.anchoredPosition = new Vector2(0f, -110f);
            self.sizeDelta = new Vector2(420f, 36f);

            _notifRoot = self;
            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _notifBg = gameObject.GetComponent<Image>();
            if (_notifBg == null)
                _notifBg = gameObject.AddComponent<Image>();
            _notifBg.color = WarningBg;
            _notifBg.raycastTarget = false;

            GameObject txtGo = new GameObject("NotifText", typeof(RectTransform));
            RectTransform txtR = txtGo.GetComponent<RectTransform>();
            txtR.SetParent(self, false);
            txtR.anchorMin = Vector2.zero;
            txtR.anchorMax = Vector2.one;
            txtR.offsetMin = new Vector2(12f, 0f);
            txtR.offsetMax = new Vector2(-12f, 0f);
            txtGo.layer = gameObject.layer;

            _notifText = txtGo.AddComponent<TextMeshProUGUI>();
            _notifText.font = font;
            _notifText.fontSize = 13f;
            _notifText.fontStyle = FontStyles.Bold;
            _notifText.alignment = TextAlignmentOptions.Center;
            _notifText.textWrappingMode = TextWrappingModes.NoWrap;
            _notifText.raycastTarget = false;
            _notifText.color = WarningText;

            gameObject.SetActive(false);
            _built = true;
        }

        private void ApplyPreviewSafeState()
        {
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }

            Image image = GetComponent<Image>();
            if (image != null)
            {
                Color c = image.color;
                c.a = 0f;
                image.color = c;
                image.raycastTarget = false;
            }
        }
    }
}
