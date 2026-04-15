using UnityEngine;
using UnityEngine.EventSystems;
using Hecton.UI.MainMenu;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// Hover preview for save slots — shows enlarged thumbnail + metadata on hover.
    /// EXCEEDS SUBNAUTICA: Subnautica has no hover preview, only click-to-load.
    /// Zero-GC: ITickable state machine, cached delegates, CanvasGroup alpha.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Save Slot Hover Preview")]
    public sealed class SaveSlotHoverPreview : MonoBehaviour, ITickable, IPointerEnterHandler, IPointerExitHandler
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== PREVIEW PANEL ===")]
        [SerializeField] private CanvasGroup previewPanel;
        [SerializeField] private RectTransform previewContainer;
        [SerializeField] private SaveSlotThumbnail previewThumbnail;

        [Header("=== SETTINGS ===")]
        [SerializeField] private float hoverDelay = 0.3f;
        [SerializeField] private float fadeInDuration = 0.15f;
        [SerializeField] private float fadeOutDuration = 0.1f;
        [SerializeField] private Vector2 previewOffset = new Vector2(20f, 0f);

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private enum State { Idle, WaitingForDelay, FadingIn, Visible, FadingOut }

        private State _state;
        private float _timer;
        private float _fadeStartAlpha;
        private string _currentSlotId;
        private bool _registered;
        private SaveSlotUI _slotUI;
        private RectTransform _slotRect;
        private RectTransform _previewParentRect;
        private RectTransform _previewPanelRect;
        private Canvas _rootCanvas;
        private Camera _uiCamera;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _slotUI = GetComponent<SaveSlotUI>();
            _slotRect = transform as RectTransform;
            _previewPanelRect = previewPanel != null ? previewPanel.transform as RectTransform : null;
            _previewParentRect = previewContainer != null
                ? previewContainer.parent as RectTransform
                : (previewPanel != null ? previewPanel.transform.parent as RectTransform : null);
            _rootCanvas = GetComponentInParent<Canvas>();
            _uiCamera = _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _rootCanvas.worldCamera
                : null;
            HideImmediate();
        }

        private void OnEnable()
        {
            TryRegister();
            HideImmediate();
        }

        private void OnDisable()
        {
            Unregister();
            HideImmediate();
        }

        // ══════════════════════════════════════════════════════════
        // ITICKABLE
        // ══════════════════════════════════════════════════════════

        public void Tick(float dt)
        {
            switch (_state)
            {
                case State.WaitingForDelay:
                    _timer += dt;
                    if (_timer >= hoverDelay)
                    {
                        _state = State.FadingIn;
                        _timer = 0f;
                        ShowPreview();
                    }
                    break;

                case State.FadingIn:
                    _timer += dt;
                    float fadeInT = Mathf.Clamp01(_timer / fadeInDuration);
                    if (previewPanel != null)
                        previewPanel.alpha = fadeInT;
                    if (fadeInT >= 1f)
                        _state = State.Visible;
                    break;

                case State.FadingOut:
                    _timer += dt;
                    float fadeOutT = Mathf.Clamp01(_timer / fadeOutDuration);
                    if (previewPanel != null)
                        previewPanel.alpha = Mathf.Lerp(_fadeStartAlpha, 0f, fadeOutT);
                    if (fadeOutT >= 1f)
                    {
                        HideImmediate();
                        _state = State.Idle;
                    }
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        // POINTER EVENTS
        // ══════════════════════════════════════════════════════════

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_state != State.Idle)
                return;

            if (_slotUI == null || !_slotUI.IsInteractable || !_slotUI.HasSaveData)
                return;

            _currentSlotId = _slotUI.SlotId;
            if (string.IsNullOrEmpty(_currentSlotId))
                return;

            _state = State.WaitingForDelay;
            _timer = 0f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_state == State.Idle || _state == State.FadingOut)
                return;

            if (_state == State.WaitingForDelay)
            {
                HideImmediate();
                _state = State.Idle;
                return;
            }

            _state = State.FadingOut;
            _timer = 0f;
            _fadeStartAlpha = previewPanel != null ? previewPanel.alpha : 0f;
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ShowPreview()
        {
            if (previewPanel == null || previewThumbnail == null)
                return;

            // Load thumbnail
            previewThumbnail.LoadThumbnail(_currentSlotId);

            // Position preview panel
            PositionPreview();

            // Show panel
            previewPanel.alpha = 0f;
            previewPanel.interactable = false;
            previewPanel.blocksRaycasts = false;
        }

        private void HideImmediate()
        {
            if (previewPanel == null)
                return;

            previewPanel.alpha = 0f;
            previewPanel.interactable = false;
            previewPanel.blocksRaycasts = false;

            if (previewThumbnail != null)
                previewThumbnail.ClearThumbnail();

            _currentSlotId = string.Empty;
        }

        private void TryRegister()
        {
            if (_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Register(this);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Unregister(this);
            _registered = false;
        }

        private void PositionPreview()
        {
            if (previewContainer == null || _slotRect == null || _previewParentRect == null)
                return;

            Vector3 worldCenter = _slotRect.TransformPoint(_slotRect.rect.center);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _previewParentRect,
                    RectTransformUtility.WorldToScreenPoint(_uiCamera, worldCenter),
                    _uiCamera,
                    out Vector2 localPoint))
            {
                previewContainer.anchoredPosition = previewOffset;
                return;
            }

            Vector2 anchoredPosition = localPoint + previewOffset;
            RectTransform targetRect = _previewPanelRect != null ? _previewPanelRect : previewContainer;
            Rect parentRect = _previewParentRect.rect;
            Vector2 size = targetRect.rect.size;
            Vector2 pivot = targetRect.pivot;

            float minX = parentRect.xMin + size.x * pivot.x;
            float maxX = parentRect.xMax - size.x * (1f - pivot.x);
            float minY = parentRect.yMin + size.y * pivot.y;
            float maxY = parentRect.yMax - size.y * (1f - pivot.y);

            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);
            previewContainer.anchoredPosition = anchoredPosition;
        }
    }
}
