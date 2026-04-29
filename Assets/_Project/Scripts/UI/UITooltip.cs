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
    /// Zero-GC: ITickable, cached strings, no LINQ.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/UI Tooltip")]
    public sealed class UITooltip : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        // SINGLETON
        // ══════════════════════════════════════════════════════════

        private static UITooltip _instance;

        public static UITooltip Instance => _instance;

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
        private bool _isVisible;
        private bool _isFading;
        private float _showTimer;
        private float _fadeTimer;
        private string _currentText;
        private Canvas _canvas;
        private RectTransform _canvasRect;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

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
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            Hide();
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (_instance == this)
                _instance = null;
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Show tooltip with text at cursor position.
        /// </summary>
        public static void Show(string text)
        {
            if (_instance == null || string.IsNullOrEmpty(text))
                return;

            _instance.ShowInternal(text);
        }

        /// <summary>
        /// Hide tooltip immediately.
        /// </summary>
        public static void Hide()
        {
            if (_instance == null)
                return;

            _instance.HideInternal();
        }

        // ══════════════════════════════════════════════════════════
        // ITICKABLE
        // ══════════════════════════════════════════════════════════

        public void Tick(float dt)
        {
            if (_isVisible)
            {
                UpdatePosition();

                if (_isFading)
                {
                    _fadeTimer += dt;
                    float t = Mathf.Clamp01(_fadeTimer / fadeDuration);
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
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private void TryRegister()
        {
            if (_registered)
                return;


            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);

            _registered = false;
        }

        private void ShowInternal(string text)
        {
            if (_currentText == text && _isVisible)
                return;

            _currentText = text;
            _showTimer = showDelay;
            _isVisible = false;
        }

        private void ShowTooltip()
        {
            if (tooltipPanel == null || tooltipText == null || tooltipCanvasGroup == null)
                return;

            tooltipText.SetText(_currentText);

            // Force layout rebuild to get correct size
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);

            // Clamp width
            Vector2 size = tooltipPanel.sizeDelta;
            if (size.x > maxWidth)
            {
                tooltipPanel.sizeDelta = new Vector2(maxWidth, size.y);
                LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);
            }

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
            Vector2 localPoint;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                mousePos,
                _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                out localPoint);

            localPoint += cursorOffset;

            // Clamp to canvas bounds
            Vector2 size = tooltipPanel.sizeDelta;
            Vector2 canvasSize = _canvasRect.sizeDelta;

            float minX = -canvasSize.x * 0.5f;
            float maxX = canvasSize.x * 0.5f - size.x;
            float minY = -canvasSize.y * 0.5f + size.y;
            float maxY = canvasSize.y * 0.5f;

            localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
            localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

            tooltipPanel.anchoredPosition = localPoint;
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
