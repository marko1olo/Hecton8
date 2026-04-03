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
        private static HUDNotification _ActiveInstance;

        private enum NotificationSeverity
        {
            Info = 0,
            Warning = 1,
            Critical = 2
        }

        private struct NotificationRequest
        {
            public string Message;
            public NotificationSeverity Severity;
        }

        [Header("── Settings ──────────────────────────────────")]
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeSpeed = 4f;
        [SerializeField] private int maxQueuedNotifications = 6;
        [SerializeField] private float repeatSuppressWindow = 0.85f;
        [SerializeField] private TMP_FontAsset font;

        private static readonly Color WarningBg = new Color(0.12f, 0.06f, 0.02f, 0.7f);
        private static readonly Color WarningText = new Color(1f, 0.74f, 0.22f, 0.95f);
        private static readonly Color CriticalBg = new Color(0.18f, 0.03f, 0.03f, 0.78f);
        private static readonly Color CriticalText = new Color(1f, 0.52f, 0.42f, 0.98f);
        private static readonly Color InfoBg = new Color(0.02f, 0.08f, 0.1f, 0.7f);
        private static readonly Color InfoText = new Color(0.46f, 0.98f, 0.94f, 0.9f);

        private RectTransform _notifRoot;
        private Image _notifBg;
        private TextMeshProUGUI _notifText;
        private CanvasGroup _canvasGroup;
        private float _timer;
        private float _currentAlpha;
        private bool _built;
        private readonly System.Collections.Generic.List<NotificationRequest> _queue =
            new System.Collections.Generic.List<NotificationRequest>(8);
        private string _currentMessage;
        private NotificationSeverity _currentSeverity;
        private string _lastEnqueuedMessage;
        private NotificationSeverity _lastEnqueuedSeverity;
        private float _lastEnqueueTime = -999f;

        public static bool TryGetActive(out HUDNotification notification)
        {
            notification = _ActiveInstance;
            return notification != null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ApplyPreviewSafeState();
        }
#endif

        private void OnEnable()
        {
            _ActiveInstance = this;

            if (font == null) font = TMP_Settings.defaultFontAsset;

            InventoryEvents.OnInventoryFull += OnInventoryFull;
            NotificationEvents.OnPushNotification += OnPushNotification;

            EnsureBuilt();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(_ActiveInstance, this))
                _ActiveInstance = null;

            InventoryEvents.OnInventoryFull -= OnInventoryFull;
            NotificationEvents.OnPushNotification -= OnPushNotification;
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

                    if (_queue.Count > 0)
                    {
                        NotificationRequest next = _queue[0];
                        _queue.RemoveAt(0);
                        ShowImmediate(next.Message, next.Severity);
                    }
                }
            }

            if (_canvasGroup != null)
                _canvasGroup.alpha = _currentAlpha;
        }

        public void ShowWarning(string message)
        {
            Enqueue(message, NotificationSeverity.Warning);
        }

        public void ShowCritical(string message)
        {
            Enqueue(message, NotificationSeverity.Critical);
        }

        public void ShowInfo(string message)
        {
            Enqueue(message, NotificationSeverity.Info);
        }

        private void Enqueue(string message, NotificationSeverity severity)
        {
            EnsureBuilt();

            if (string.IsNullOrWhiteSpace(message))
                return;

            string normalized = message.Trim();
            float now = Time.unscaledTime;

            if (normalized == _currentMessage && severity == _currentSeverity && _timer > 0f)
            {
                _timer = displayDuration;
                return;
            }

            if (normalized == _lastEnqueuedMessage &&
                severity == _lastEnqueuedSeverity &&
                now - _lastEnqueueTime < repeatSuppressWindow)
            {
                return;
            }

            _lastEnqueuedMessage = normalized;
            _lastEnqueuedSeverity = severity;
            _lastEnqueueTime = now;

            if (_timer <= 0f && _queue.Count == 0 && !_notifRoot.gameObject.activeSelf)
            {
                ShowImmediate(normalized, severity);
                return;
            }

            if (severity == NotificationSeverity.Critical && _currentSeverity != NotificationSeverity.Critical)
            {
                if (!string.IsNullOrWhiteSpace(_currentMessage) && _queue.Count < maxQueuedNotifications)
                {
                    _queue.Insert(0, new NotificationRequest
                    {
                        Message = _currentMessage,
                        Severity = _currentSeverity
                    });
                }

                ShowImmediate(normalized, severity);
                return;
            }

            if (_queue.Count >= Mathf.Max(1, maxQueuedNotifications))
            {
                if (severity <= NotificationSeverity.Info)
                    return;

                _queue.RemoveAt(0);
            }

            _queue.Add(new NotificationRequest
            {
                Message = normalized,
                Severity = severity
            });
        }

        private void ShowImmediate(string message, NotificationSeverity severity)
        {
            ApplyVisuals(message, severity);
            _timer = displayDuration;
            _currentAlpha = 0f;
            _notifRoot.gameObject.SetActive(true);
        }

        private void ApplyVisuals(string message, NotificationSeverity severity)
        {
            _currentMessage = message;
            _currentSeverity = severity;

            switch (severity)
            {
                case NotificationSeverity.Critical:
                    _notifBg.color = CriticalBg;
                    _notifText.color = CriticalText;
                    break;
                case NotificationSeverity.Warning:
                    _notifBg.color = WarningBg;
                    _notifText.color = WarningText;
                    break;
                default:
                    _notifBg.color = InfoBg;
                    _notifText.color = InfoText;
                    break;
            }

            _notifText.text = message;
        }

        private void OnPushNotification(string message, int severity)
        {
            Enqueue(message, (NotificationSeverity)severity);
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
