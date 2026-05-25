using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Hecton8.Core;
using Hecton.Localization;

namespace Hecton8.UI
{
    /// <summary>
    /// Tooltip system for UI elements (Subnautica-style).
    /// Shows contextual help text on hover.
    /// Zero-GC: late-frame state machine, cached strings, no LINQ.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/UI Tooltip")]
    public sealed class UITooltip : MonoBehaviour, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        // ══════════════════════════════════════════════════════════
        // REGISTRY SERVICE
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== UI REFERENCES ===")]
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private TextMeshProUGUI tooltipText;
        [SerializeField] private CanvasGroup tooltipCanvasGroup;
        [SerializeField] private Image tooltipBackground;

        [Header("=== SETTINGS ===")]
        [SerializeField, Tooltip("Delay before showing tooltip (seconds)")]
        private float showDelay = 0.5f;

        [SerializeField, Tooltip("Fade in duration (seconds)")]
        private float fadeDuration = 0.15f;

        [SerializeField, Tooltip("Offset from cursor (pixels)")]
        private Vector2 cursorOffset = new Vector2(15f, -15f);

        [SerializeField, Tooltip("Max tooltip width (pixels)")]
        private float maxWidth = 400f;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private bool _registered;
        private bool _runtimeRegistered;
        private bool _hotSwapRegistered;
        private bool _isVisible;
        private bool _isFading;
        private float _showTimer;
        private float _fadeTimer;
        private uint _currentTextHash;
        private Vector2 _lastPointerPosition;
        private Vector2 _lastTooltipSize;
        private Vector2 _lastCanvasSize;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private bool _hasPositionCache;
        private bool _pendingPositionRefresh;
        private int _currentTextLength;
        private static UITooltip s_activeRuntime;
        // COLD ALLOC: char[512] - tooltip TMP staging buffer, never resized - owner: UITooltip
        private readonly char[] _textBuffer = new char[TooltipTextCapacity];
        private const int TooltipTextCapacity = 512;
        public ServiceHeartbeatState HeartbeatState => _runtimeRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => _runtimeRegistered;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            UITooltip registered = GlobalRegistry.UITooltip;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            _canvas = ResolveNearestParentCanvas(transform);
            if (_canvas != null)
                _canvas.TryGetComponent(out _canvasRect);

            if (tooltipCanvasGroup != null)
            {
                tooltipCanvasGroup.alpha = 0f;
                tooltipCanvasGroup.interactable = false;
                tooltipCanvasGroup.blocksRaycasts = false;
            }
        }

        private static Canvas ResolveNearestParentCanvas(Transform start)
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out Canvas canvas))
                    return canvas;
            }

            return null;
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            TryRegisterRuntime();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
            HideInternal();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
        }

        public void OnServiceShutdown()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
            HideInternal();
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
                RefreshTickRegistration();
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Show tooltip with text at cursor position.
        /// </summary>
        public static void Show(string text)
        {
            UITooltip instance = s_activeRuntime;
            if (instance == null || string.IsNullOrEmpty(text))
                return;

            instance.ShowInternal(text.AsSpan());
        }

        public static void Show(ReadOnlySpan<char> text)
        {
            UITooltip instance = s_activeRuntime;
            if (instance == null || text.Length <= 0)
                return;

            instance.ShowInternal(text);
        }

        /// <summary>
        /// Hide tooltip immediately.
        /// </summary>
        public static void Hide()
        {
            UITooltip instance = s_activeRuntime;
            if (instance == null)
                return;

            instance.HideInternal();
        }

        // ══════════════════════════════════════════════════════════
        // LATE FRAME
        // ══════════════════════════════════════════════════════════

        public void LateFrameTick()
        {
            float dt = Mathf.Max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            if (_isVisible)
            {
                UpdatePosition();

                if (_isFading)
                {
                    _fadeTimer += dt;
                    float fadeDurationSafe = fadeDuration > 0.0001f ? fadeDuration : 0.0001f;
                    float t = _fadeTimer / fadeDurationSafe;
                    if (t > 1f)
                        t = 1f;
                    else if (t < 0f)
                        t = 0f;

                    if (tooltipCanvasGroup != null)
                        tooltipCanvasGroup.alpha = t;

                    if (t >= 1f)
                        _isFading = false;
                }
            }
            else if (_showTimer > 0f)
            {
                _showTimer -= dt;
                if (_showTimer <= 0f)
                {
                    ShowTooltip();
                }
            }

            RefreshTickRegistration();
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private bool TryRegisterRuntime()
        {
            if (_runtimeRegistered)
                return true;

            if (!Application.isPlaying)
                return false;

            UITooltip registered = GlobalRegistry.UITooltip;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterUITooltipRuntime(this);
            _runtimeRegistered = ReferenceEquals(GlobalRegistry.UITooltip, this);
            if (_runtimeRegistered)
                s_activeRuntime = this;
            return _runtimeRegistered;
        }

        private void TryUnregisterRuntime()
        {
            if (!_runtimeRegistered)
                return;

            GlobalRegistry.UnregisterUITooltipRuntime(this);
            _runtimeRegistered = false;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);

            _registered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void ShowInternal(ReadOnlySpan<char> text)
        {
            uint textHash = unchecked((uint)LocHash.Compute(text));
            if (_currentTextHash == textHash &&
                _currentTextLength == text.Length &&
                BufferMatches(text, _textBuffer, _currentTextLength) &&
                _isVisible)
            {
                return;
            }

            _currentTextHash = textHash;
            _currentTextLength = CopySpanToBuffer(text, _textBuffer);
            _showTimer = showDelay;
            _isVisible = false;
            _pendingPositionRefresh = true;
            TryRegister();
        }

        private void ShowTooltip()
        {
            if (tooltipPanel == null || tooltipText == null || tooltipCanvasGroup == null)
                return;

            tooltipText.SetCharArray(_textBuffer, 0, _currentTextLength);

            // Defer layout rebuild; immediate rebuild spikes the whole tooltip canvas.
            LayoutRebuilder.MarkLayoutForRebuild(tooltipPanel);

            // Clamp width
            Vector2 size = tooltipPanel.sizeDelta;
            if (size.x > maxWidth)
            {
                tooltipPanel.sizeDelta = new Vector2(maxWidth, size.y);
                LayoutRebuilder.MarkLayoutForRebuild(tooltipPanel);
            }

            _pendingPositionRefresh = true;
            UpdatePosition();

            _isVisible = true;
            _isFading = true;
            _fadeTimer = 0f;
            tooltipCanvasGroup.alpha = 0f;
            tooltipCanvasGroup.interactable = false;
            tooltipCanvasGroup.blocksRaycasts = false;
        }

        private void HideInternal()
        {
            _isVisible = false;
            _isFading = false;
            _showTimer = 0f;
            _currentTextHash = 0u;
            _currentTextLength = 0;
            _hasPositionCache = false;
            _pendingPositionRefresh = false;
            TryUnregister();

            if (tooltipCanvasGroup != null)
            {
                tooltipCanvasGroup.alpha = 0f;
                tooltipCanvasGroup.interactable = false;
                tooltipCanvasGroup.blocksRaycasts = false;
            }
        }

        private void UpdatePosition()
        {
            if (tooltipPanel == null || _canvas == null || _canvasRect == null)
                return;

            Pointer pointer = Pointer.current;
            if (pointer == null)
                return;

            Vector2 mousePos = pointer.position.ReadValue();
            Vector2 size = tooltipPanel.sizeDelta;
            Vector2 canvasSize = _canvasRect.sizeDelta;
            if (!_pendingPositionRefresh && _hasPositionCache)
            {
                Vector2 pointerDelta = mousePos - _lastPointerPosition;
                Vector2 sizeDelta = size - _lastTooltipSize;
                Vector2 canvasDelta = canvasSize - _lastCanvasSize;
                if (pointerDelta.sqrMagnitude < 0.01f &&
                    sizeDelta.sqrMagnitude < 0.01f &&
                    canvasDelta.sqrMagnitude < 0.01f)
                {
                    return;
                }
            }

            Vector2 localPoint;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                mousePos,
                _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                out localPoint);

            localPoint += cursorOffset;

            // Clamp to canvas bounds
            float minX = -canvasSize.x * 0.5f;
            float maxX = canvasSize.x * 0.5f - size.x;
            float minY = -canvasSize.y * 0.5f + size.y;
            float maxY = canvasSize.y * 0.5f;

            if (localPoint.x < minX)
                localPoint.x = minX;
            else if (localPoint.x > maxX)
                localPoint.x = maxX;

            if (localPoint.y < minY)
                localPoint.y = minY;
            else if (localPoint.y > maxY)
                localPoint.y = maxY;

            tooltipPanel.anchoredPosition = localPoint;
            _lastPointerPosition = mousePos;
            _lastTooltipSize = size;
            _lastCanvasSize = canvasSize;
            _hasPositionCache = true;
            _pendingPositionRefresh = false;
        }

        private void RefreshTickRegistration()
        {
            if (_isVisible || _isFading || _showTimer > 0f)
                TryRegister();
            else
                TryUnregister();
        }

        private static int CopyStringToBuffer(string value, char[] buffer)
        {
            if (buffer == null || string.IsNullOrEmpty(value))
                return 0;

            return CopySpanToBuffer(value.AsSpan(), buffer);
        }

        private static int CopySpanToBuffer(ReadOnlySpan<char> value, char[] buffer)
        {
            if (buffer == null || value.Length <= 0)
                return 0;

            int length = value.Length;
            if (length <= buffer.Length)
            {
                value.CopyTo(buffer.AsSpan(0, length));
                return length;
            }

            int copyLength = buffer.Length;
            value.Slice(0, copyLength).CopyTo(buffer);
            if (copyLength >= 3)
            {
                buffer[copyLength - 3] = '.';
                buffer[copyLength - 2] = '.';
                buffer[copyLength - 1] = '.';
            }

            return copyLength;
        }

        private static bool BufferMatches(ReadOnlySpan<char> value, char[] buffer, int length)
        {
            return buffer != null &&
                   length == value.Length &&
                   value.SequenceEqual(buffer.AsSpan(0, length));
        }
    }

    /// <summary>
    /// Attach to UI element to show tooltip on hover.
    /// </summary>
    [AddComponentMenu("Hecton8/UI/UI Tooltip Trigger")]
    public sealed class UITooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, TextArea(2, 5)]
        private string tooltipText;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(tooltipText))
                UITooltip.Show(tooltipText.AsSpan());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UITooltip.Hide();
        }
    }
}
