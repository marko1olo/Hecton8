using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// Tooltip system for UI elements (Subnautica-style).
    /// Shows contextual help text on hover.
    /// Zero-GC: late-frame state machine, cached strings, no LINQ.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/UI Tooltip")]
    public sealed class UITooltip : MonoBehaviour, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown
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
        private bool _isVisible;
        private bool _isFading;
        private float _showTimer;
        private float _fadeTimer;
        private string _currentText;
        private Vector2 _lastPointerPosition;
        private Vector2 _lastTooltipSize;
        private Vector2 _lastCanvasSize;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private bool _hasPositionCache;
        private bool _pendingPositionRefresh;
        private int _currentTextLength;
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

            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null)
                _canvasRect = _canvas.GetComponent<RectTransform>();

            if (tooltipCanvasGroup != null)
            {
                tooltipCanvasGroup.alpha = 0f;
                tooltipCanvasGroup.interactable = false;
                tooltipCanvasGroup.blocksRaycasts = false;
            }
        }

        private void OnEnable()
        {
            TryRegisterRuntime();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterRuntime();
            HideInternal();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterRuntime();
        }

        public void OnServiceShutdown()
        {
            TryUnregister();
            TryUnregisterRuntime();
            HideInternal();
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Show tooltip with text at cursor position.
        /// </summary>
        public static void Show(string text)
        {
            UITooltip instance = GlobalRegistry.UITooltip;
            if (instance == null || string.IsNullOrEmpty(text))
                return;

            instance.ShowInternal(text);
        }

        /// <summary>
        /// Hide tooltip immediately.
        /// </summary>
        public static void Hide()
        {
            UITooltip instance = GlobalRegistry.UITooltip;
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
            return _runtimeRegistered;
        }

        private void TryUnregisterRuntime()
        {
            if (!_runtimeRegistered)
                return;

            GlobalRegistry.UnregisterUITooltipRuntime(this);
            _runtimeRegistered = false;
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

        private void ShowInternal(string text)
        {
            if (_currentText == text && _isVisible)
                return;

            _currentText = text;
            _currentTextLength = CopyStringToBuffer(text, _textBuffer);
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
            _currentText = null;
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

            int length = value.Length;
            if (length <= buffer.Length)
            {
                value.CopyTo(0, buffer, 0, length);
                return length;
            }

            int copyLength = buffer.Length;
            value.CopyTo(0, buffer, 0, copyLength);
            if (copyLength >= 3)
            {
                buffer[copyLength - 3] = '.';
                buffer[copyLength - 2] = '.';
                buffer[copyLength - 1] = '.';
            }

            return copyLength;
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
                UITooltip.Show(tooltipText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UITooltip.Hide();
        }
    }
}
