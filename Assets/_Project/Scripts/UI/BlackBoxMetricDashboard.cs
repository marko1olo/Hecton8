#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Hecton8.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hecton8.UI
{
    /// <summary>
    /// Development-only black-box metric dashboard using caller-owned char buffers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackBoxMetricDashboard : MonoBehaviour, IUpdatable
    {
        private const int BufferCapacity = 128;
        private const int RefreshIntervalFrames = 30;

        [Header("── Dashboard Bindings ──────────────────")]
        [Tooltip("TMP text target updated through SetCharArray.")]
        [SerializeField] private TMP_Text metricText;
        [Tooltip("CanvasGroup used for visibility without SetActive.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Whether the dashboard is visible when the component enables.")]
        [SerializeField] private bool visibleByDefault;

        // COLD ALLOC: char[128] - black-box dashboard TMP staging buffer - owner: BlackBoxMetricDashboard
        private readonly char[] _buffer = new char[BufferCapacity];

        private bool _registered;
        private bool _visible;
        private int _nextRefreshFrame;
        private int _accumulatedFrames;
        private float _accumulatedSeconds;

        private void Awake()
        {
            if (metricText == null)
                TryGetComponent(out metricText);
            if (canvasGroup == null)
                TryGetComponent(out canvasGroup);
        }

        private void OnEnable()
        {
            SetVisible(visibleByDefault);
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registered = false;
            }
        }

        /// <summary>
        /// Dispatcher tick; toggles visibility and refreshes metrics at low cadence.
        /// </summary>
        /// <param name="deltaTime">Dispatcher delta.</param>
        public void Tick(float deltaTime)
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
                SetVisible(!_visible);

            if (!_visible || metricText == null)
                return;

            _accumulatedFrames++;
            _accumulatedSeconds += deltaTime > 0f ? deltaTime : 0f;

            int frame = Time.frameCount;
            if (frame < _nextRefreshFrame)
                return;

            _nextRefreshFrame = frame + RefreshIntervalFrames;
            RefreshMetrics();
        }

        /// <summary>
        /// Applies dashboard visibility without activating or deactivating the GameObject.
        /// </summary>
        /// <param name="visible">Requested visible state.</param>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void RefreshMetrics()
        {
            int fps = 0;
            if (_accumulatedSeconds > 0f)
                fps = Mathf.RoundToInt(_accumulatedFrames / _accumulatedSeconds);

            _accumulatedFrames = 0;
            _accumulatedSeconds = 0f;

            int cursor = 0;
            System.Span<char> span = _buffer.AsSpan();
            if (!ZeroGCFormatter.AppendToSpan("FPS ".AsSpan(), span, ref cursor) ||
                !ZeroGCFormatter.AppendInt(fps, span, ref cursor) ||
                !ZeroGCFormatter.AppendToSpan(" | AUP ".AsSpan(), span, ref cursor) ||
                !ZeroGCFormatter.AppendInt(unchecked((int)HectonFloatingOrigin.LastShiftEvent.Sequence), span, ref cursor) ||
                !ZeroGCFormatter.AppendToSpan(" | VRAM ".AsSpan(), span, ref cursor) ||
                !ZeroGCFormatter.AppendInt(VRAMBudgetTracker.EstimatedVRAMMegabytes, span, ref cursor) ||
                !ZeroGCFormatter.AppendToSpan("MB".AsSpan(), span, ref cursor))
            {
                return;
            }

            metricText.SetCharArray(_buffer, 0, cursor);
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }
    }
}
#endif
