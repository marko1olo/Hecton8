// ============================================================================
// HECTON-8 â€” HUDNotification.cs
// ÐšÑ€Ð°Ñ‚ÐºÐ¾Ð²Ñ€ÐµÐ¼ÐµÐ½Ð½Ñ‹Ðµ ÑƒÐ²ÐµÐ´Ð¾Ð¼Ð»ÐµÐ½Ð¸Ñ Ð½Ð° HUD (Ð¸Ð½Ð²ÐµÐ½Ñ‚Ð°Ñ€ÑŒ Ð¿Ð¾Ð»Ð¾Ð½, Ð¸ Ñ‚.Ð´.)
// Sibling Ðº HUD_V4_CanvasRoot Ð½Ð° Suit_HUD_Canvas.
// ============================================================================

using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton.Localization;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/HUD Notification")]
    public sealed class HUDNotification : MonoBehaviour, ITickable, IUpdatable, INotificationEventListener
    {
        private static HUDNotification _ActiveInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _ActiveInstance = null;
        }

        private enum NotificationSeverity
        {
            Info = 0,
            Warning = 1,
            Critical = 2
        }

        private const int InventoryFullMessageCacheSize = 16;
        private const int MaxNotificationQueueCapacity = 8;
        private const string InventoryFullMessagePrefix = "INVENTORY FULL \u2014 CANNOT STORE ";
        private const string FallbackInventoryItemName = "ITEM";

        private struct NotificationRequest
        {
            public uint MessageHash;
            public byte Severity;
        }

        [Header("â”€â”€ Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
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
        private static readonly string[] _inventoryFullItemNameCache = new string[InventoryFullMessageCacheSize];
        private static readonly string[] _inventoryFullMessageCache = new string[InventoryFullMessageCacheSize];

        private RectTransform _notifRoot;
        private Image _notifBg;
        private TextMeshProUGUI _notifText;
        private CanvasGroup _canvasGroup;
        private float _timer;
        private float _currentAlpha;
        private bool _built;
        private bool _isShowing;
        private NativeArray<NotificationRequest> _queue;
        private int _queueCount;
        private uint _currentMessageHash;
        private NotificationSeverity _currentSeverity;
        private uint _lastEnqueuedMessageHash;
        private NotificationSeverity _lastEnqueuedSeverity;
        private float _lastEnqueueTime = -999f;
        private bool _registeredToTickManager;
        private int _lastStressCorruptionBucket = int.MinValue;

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
            NotificationEvents.Register(this);

            EnsureBuilt();
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(_ActiveInstance, this))
                _ActiveInstance = null;

            UnregisterFromTickManager();
            InventoryEvents.OnInventoryFull -= OnInventoryFull;
            NotificationEvents.Unregister(this);
            _queueCount = 0;
            _currentMessageHash = 0u;
        }

        private void OnDestroy()
        {
            if (_queue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_queue);
                _queue.Dispose();
                _queue = default;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_notifRoot == null) return;

            if (_timer > 0f)
            {
                _timer -= deltaTime;
                _currentAlpha = Mathf.Lerp(_currentAlpha, 1f,
                    1f - Mathf.Exp(-fadeSpeed * deltaTime));
            }
            else
            {
                _currentAlpha = Mathf.Lerp(_currentAlpha, 0f,
                    1f - Mathf.Exp(-fadeSpeed * deltaTime));

                if (_currentAlpha < 0.01f)
                {
                    _currentAlpha = 0f;
                    _isShowing = false;

                    if (_queueCount > 0)
                    {
                        NotificationRequest next = PopQueueFront();
                        ShowImmediate(next.MessageHash, (NotificationSeverity)next.Severity);
                    }
                    else
                    {
                        UnregisterFromTickManager();
                    }
                }
            }

            if (_isShowing)
                RefreshStressCorruptionIfNeeded();

            if (_canvasGroup != null)
                _canvasGroup.alpha = _currentAlpha;
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = false;
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

            uint messageHash = NotificationEvents.RegisterMessage(message);
            if (messageHash == 0u)
                return;

            Enqueue(messageHash, severity);
        }

        private void Enqueue(uint messageHash, NotificationSeverity severity)
        {
            EnsureBuilt();
            if (messageHash == 0u)
                return;

            float now = Time.unscaledTime;

            if (messageHash == _currentMessageHash && severity == _currentSeverity && _timer > 0f)
            {
                _timer = displayDuration;
                return;
            }

            if (messageHash == _lastEnqueuedMessageHash &&
                severity == _lastEnqueuedSeverity &&
                now - _lastEnqueueTime < repeatSuppressWindow)
            {
                return;
            }

            _lastEnqueuedMessageHash = messageHash;
            _lastEnqueuedSeverity = severity;
            _lastEnqueueTime = now;

            if (_timer <= 0f && _queueCount == 0 && !_isShowing && _currentAlpha <= 0.01f)
            {
                ShowImmediate(messageHash, severity);
                return;
            }

            if (severity == NotificationSeverity.Critical && _currentSeverity != NotificationSeverity.Critical)
            {
                if (_currentMessageHash != 0u && _queueCount < ResolveQueueCapacity())
                {
                    InsertQueueFront(new NotificationRequest
                    {
                        MessageHash = _currentMessageHash,
                        Severity = (byte)_currentSeverity
                    });
                }

                ShowImmediate(messageHash, severity);
                return;
            }

            if (_queueCount >= ResolveQueueCapacity())
            {
                if (severity <= NotificationSeverity.Info)
                    return;

                RemoveQueueFront();
            }

            PushQueueBack(new NotificationRequest
            {
                MessageHash = messageHash,
                Severity = (byte)severity
            });
        }

        private void ShowImmediate(uint messageHash, NotificationSeverity severity)
        {
            RegisterToTickManager();
            ApplyVisuals(messageHash, severity);
            _timer = displayDuration;
            _currentAlpha = 0f;
            _isShowing = true;
        }

        private void ApplyVisuals(uint messageHash, NotificationSeverity severity)
        {
            _currentMessageHash = messageHash;
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

            _lastStressCorruptionBucket = int.MinValue;
            ApplyNotificationText(messageHash);
        }

        public void OnNotificationEvent(in NotificationEventPayload payload)
        {
            Enqueue(payload.MessageHash, (NotificationSeverity)payload.Severity);
        }

        private void OnInventoryFull(ItemData item)
        {
            ShowWarning(GetInventoryFullMessage(item));
        }

        private static string GetInventoryFullMessage(ItemData item)
        {
            string itemName = item != null ? item.itemName : null;
            if (string.IsNullOrWhiteSpace(itemName))
                itemName = FallbackInventoryItemName;

            int cacheIndex = (itemName.GetHashCode() & int.MaxValue) % InventoryFullMessageCacheSize;
            string cachedItemName = _inventoryFullItemNameCache[cacheIndex];
            if (!string.IsNullOrEmpty(cachedItemName) && string.Equals(cachedItemName, itemName, System.StringComparison.Ordinal))
                return _inventoryFullMessageCache[cacheIndex];

            string message = InventoryFullMessagePrefix + ZeroGCStringCache.CachedToUpperInvariant(itemName);
            _inventoryFullItemNameCache[cacheIndex] = itemName;
            _inventoryFullMessageCache[cacheIndex] = message;
            return message;
        }

        private void RefreshStressCorruptionIfNeeded()
        {
            LocalizationManager manager = LocalizationManager.Instance;
            int stressBucket = manager != null ? manager.GetHullStressCorruptionBucket() : 0;
            if (stressBucket == _lastStressCorruptionBucket)
                return;

            _lastStressCorruptionBucket = stressBucket;
            ApplyNotificationText(_currentMessageHash);
        }

        private void ApplyNotificationText(uint messageHash)
        {
            if (_notifText == null || messageHash == 0u)
                return;

            if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
                return;

            try
            {
                if (!TryWriteDisplayMessage(messageHash, lease.Buffer.AsSpan(), out int length))
                    return;

                _notifText.SetCharArray(lease.Buffer, 0, length);
            }
            finally
            {
                CharBufferPool.Release(lease);
            }
        }

        private static bool TryWriteDisplayMessage(uint messageHash, Span<char> target, out int length)
        {
            length = 0;
            if (messageHash == 0u || !NotificationEvents.TryResolveMessage(messageHash, out string message) || string.IsNullOrEmpty(message))
                return false;

            LocalizationManager manager = LocalizationManager.Instance;
            string displayMessage = manager != null
                ? manager.ApplyHullStressCorruptionIfNeeded(message)
                : message;
            ReadOnlySpan<char> source = displayMessage.AsSpan();
            length = Mathf.Min(source.Length, target.Length);
            if (length <= 0)
                return false;

            source.Slice(0, length).CopyTo(target);
            return true;
        }

        private int ResolveQueueCapacity()
        {
            EnsureQueue();
            return Mathf.Clamp(maxQueuedNotifications, 1, _queue.Length);
        }

        private void EnsureQueue()
        {
            if (_queue.IsCreated)
                return;

            int capacity = Mathf.Clamp(maxQueuedNotifications, 1, MaxNotificationQueueCapacity);
            _queue = new NativeArray<NotificationRequest>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<NotificationRequest>[capacity] - fixed HUD notification hash queue - owner: HUDNotification
            NativeMemorySentinel.RegisterNativeArray(_queue, nameof(HUDNotification), nameof(_queue), NativeAllocationLifetime.Scene);
            _queueCount = 0;
        }

        private void PushQueueBack(in NotificationRequest request)
        {
            EnsureQueue();
            int capacity = ResolveQueueCapacity();
            if (_queueCount >= capacity)
                return;

            _queue[_queueCount] = request;
            _queueCount++;
        }

        private void InsertQueueFront(in NotificationRequest request)
        {
            EnsureQueue();
            int capacity = ResolveQueueCapacity();
            if (_queueCount >= capacity)
                _queueCount = capacity - 1;

            for (int i = _queueCount; i > 0; i--)
                _queue[i] = _queue[i - 1];

            _queue[0] = request;
            _queueCount++;
        }

        private NotificationRequest PopQueueFront()
        {
            EnsureQueue();
            NotificationRequest request = _queueCount > 0 ? _queue[0] : default;
            RemoveQueueFront();
            return request;
        }

        private void RemoveQueueFront()
        {
            if (!_queue.IsCreated || _queueCount <= 0)
                return;

            for (int i = 1; i < _queueCount; i++)
                _queue[i - 1] = _queue[i];

            _queueCount--;
            _queue[_queueCount] = default;
        }

        private void EnsureBuilt()
        {
            if (_built) return;

            EnsureQueue();

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
            _currentAlpha = 0f;
            _isShowing = false;

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
