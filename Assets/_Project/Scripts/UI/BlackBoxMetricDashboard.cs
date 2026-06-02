#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Hecton8.Core;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Development-only black-box metric dashboard using caller-owned char buffers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackBoxMetricDashboard : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int BufferCapacity = 128;
        private const int RefreshIntervalFrames = 30;

        [Header("-- Dashboard Bindings ------------------")]
        [Tooltip("TMP text target updated through SetCharArray.")]
        [SerializeField] private TMP_Text metricText;
        [Tooltip("CanvasGroup used for visibility without SetActive.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Whether the dashboard is visible when the component enables.")]
        [SerializeField] private bool visibleByDefault;

        // COLD ALLOC: char[128] - black-box dashboard TMP staging buffer - owner: BlackBoxMetricDashboard
        private readonly char[] _buffer = new char[BufferCapacity];

        private bool _registered;
        private bool _hotSwapListenerRegistered;
        private bool _visible;
        private int _nextRefreshFrame;
        private int _accumulatedFrames;
        private float _accumulatedSeconds;
        private INativeInputManagerRuntime _inputManager;

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
            TrySubscribeInput();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void Start()
        {
            TrySubscribeInput();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            TryUnregisterHotSwapListener();

            if (_registered)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registered = false;
            }
        }

        /// <summary>
        /// Dispatcher tick; toggles visibility and refreshes metrics at low cadence.
        /// </summary>
        /// <param name="deltaTime">Dispatcher delta.</param>
        public void LateFrameTick()
        {
            if (!_visible || metricText == null)
                return;

            float deltaTime = Mathf.Max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            _accumulatedFrames++;
            _accumulatedSeconds += deltaTime > 0f ? deltaTime : 0f;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.NativeInputManagerRuntime)
                return;

            UnsubscribeInput();

            if (!isActiveAndEnabled)
                return;

            TrySubscribeInput(currentService as INativeInputManagerRuntime);
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void TrySubscribeInput()
        {
            if (_inputManager != null)
                return;

            TrySubscribeInput(GlobalRegistry.NativeInputRuntime);
        }

        private void TrySubscribeInput(INativeInputManagerRuntime inputManager)
        {
            if (_inputManager != null || inputManager == null)
                return;

            _inputManager = inputManager;
            _inputManager.OnDebugToggleBlackBoxDashboard += HandleDebugToggleBlackBoxDashboard;
        }

        private void UnsubscribeInput()
        {
            if (_inputManager == null)
                return;

            _inputManager.OnDebugToggleBlackBoxDashboard -= HandleDebugToggleBlackBoxDashboard;
            _inputManager = null;
        }

        private void HandleDebugToggleBlackBoxDashboard()
        {
            SetVisible(!_visible);
        }
    }
}
#endif
