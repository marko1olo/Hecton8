using System;
using Hecton8.Audio;
using Hecton8.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    public enum LoadingPipelineStage : byte
    {
        Idle = 0,
        PagingSectors = 1,
        HydratingEntities = 2,
        BuildingNavGrid = 3,
        SafeAupSnap = 4,
        Completed = 5
    }

    /// <summary>
    /// Standardized loading screen system that provides consistent loading feel across all scene transitions.
    /// Prevents broken bootstrap appearance by maintaining visual continuity during async operations.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LoadingScreenController : MonoBehaviour, ITickable, IUpdatable, IServiceHeartbeat, IServiceShutdown
    {
        private enum VisibilityState
        {
            Hidden,
            FadingIn,
            Visible,
            DelayBeforeHide,
            FadingOut
        }

        private static readonly char[] LoadingChars = "Loading...".ToCharArray();
        private static readonly char[] PagingSectorsChars = "Paging Sectors...".ToCharArray();
        private static readonly char[] HydratingEntitiesChars = "Hydrating Entities...".ToCharArray();
        private static readonly char[] BuildingNavGridChars = "Building NavGrid...".ToCharArray();
        private static readonly char[] SafeAupSnapChars = "Securing AUP Position...".ToCharArray();
        private static readonly char[] LoadCompleteChars = "Load Complete.".ToCharArray();

        [Header("UI References")]
        [SerializeField, Tooltip("Main loading panel CanvasGroup")]
        private CanvasGroup _loadingPanel;

        [SerializeField, Tooltip("Progress bar slider (0-1 range)")]
        private Slider _progressBar;

        [SerializeField, Tooltip("Progress percentage text")]
        private TMP_Text _progressText;

        [SerializeField, Tooltip("Loading status message")]
        private TMP_Text _statusText;

        [SerializeField, Tooltip("Loading tip text")]
        private TMP_Text _tipText;

        [Header("Animation Settings")]
        [SerializeField, Tooltip("Fade in/out duration in seconds")]
        private float _fadeDuration = 0.5f;

        [SerializeField, Tooltip("Minimum display time to prevent flicker")]
        private float _minimumDisplayTime = 1.0f;

        [Header("Loading Tips")]
        [SerializeField, Tooltip("Random tips to show during loading")]
        private string[] _loadingTips =
        {
            "Deep sea exploration requires patience...",
            "The ocean holds many secrets...",
            "Check your oxygen levels regularly...",
            "Some ruins are better left unexplored...",
            "The abyss stares back...",
            "Pressure increases with depth...",
            "Bioluminescent life guides the way...",
            "Ancient technology powers the depths..."
        };

        private bool _isShowing;
        private bool _registeredToTickManager;
        private float _showStartTime;
        private float _transitionElapsed;
        private float _fadeStartAlpha;
        private float _delayRemaining;
        private float _lastUnscaledTickTime;
        private bool _runtimeRegistered;
        private bool _serviceShuttingDown;
        private VisibilityState _visibilityState;
        private int _currentProgressPercent = -1;
        private string _currentTipText = "Loading...";
        // COLD ALLOC: char[128] - status text equality cache for zero-GC load-stage updates - owner: LoadingScreenController
        private readonly char[] _currentStatusBuffer = new char[128];
        // COLD ALLOC: char[4] - progress percent fallback when CharBufferPool is exhausted - owner: LoadingScreenController
        private readonly char[] _progressFallbackBuffer = new char[4];
        private int _currentStatusLength = -1;
        private LoadingPipelineStage _currentPipelineStage = LoadingPipelineStage.Idle;
        private uint _tipRandomState;

        private CanvasGroup _canvasGroup;

        public ServiceHeartbeatState HeartbeatState =>
            _serviceShuttingDown
                ? ServiceHeartbeatState.Shutdown
                : _runtimeRegistered
                    ? ServiceHeartbeatState.Ready
                    : ServiceHeartbeatState.NotStarted;

        public bool IsServiceReady => _runtimeRegistered && !_serviceShuttingDown;

        private void Awake()
        {
            _tipRandomState = MixSeed(unchecked((uint)EntityId.ToULong(GetEntityId())));
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[LoadingScreenController] Missing CanvasGroup component!");
#endif
                enabled = false;
                return;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _isShowing = false;
            _visibilityState = VisibilityState.Hidden;

            if (_loadingPanel == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[LoadingScreenController] Loading panel not assigned!");
#endif
                enabled = false;
                return;
            }

            if (_progressBar != null)
            {
                _progressBar.value = 0f;
                _progressBar.interactable = false;
            }

            UpdateProgress(0f);
            UpdateStatus(LoadingChars);
            UpdateTip(GetRandomTip());
        }

        private void OnEnable()
        {
            if (_serviceShuttingDown)
                return;

            TryRegisterRuntime();
            TryRegisterToTickManager();
            _lastUnscaledTickTime = Time.unscaledTime;
        }

        private void Start()
        {
            TryRegisterRuntime();
            TryRegisterToTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            TryUnregisterRuntime();
            _lastUnscaledTickTime = 0f;
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        public void OnServiceShutdown()
        {
            if (_serviceShuttingDown)
                return;

            _serviceShuttingDown = true;
            UnregisterFromTickManager();
            TryUnregisterRuntime();
        }

        /// <summary>
        /// Shows the loading screen with fade animation.
        /// </summary>
        public void Show()
        {
            if (_isShowing &&
                (_visibilityState == VisibilityState.FadingIn ||
                 _visibilityState == VisibilityState.Visible ||
                 _visibilityState == VisibilityState.DelayBeforeHide))
            {
                return;
            }

            _isShowing = true;
            _showStartTime = Time.unscaledTime;
            _delayRemaining = 0f;
            _transitionElapsed = 0f;
            _fadeStartAlpha = _canvasGroup.alpha;
            _visibilityState = VisibilityState.FadingIn;
            _canvasGroup.blocksRaycasts = true;
            TryRegisterToTickManager();

            if (Hecton8.Core.GlobalRegistry.Audio != null)
            {
                // Loading audio hook stays cold-path only.
            }
        }

        /// <summary>
        /// Hides the loading screen with fade animation.
        /// </summary>
        public void Hide()
        {
            if (!_isShowing)
                return;

            float elapsed = Time.unscaledTime - _showStartTime;
            if (elapsed < _minimumDisplayTime)
            {
                _delayRemaining = _minimumDisplayTime - elapsed;
                _visibilityState = VisibilityState.DelayBeforeHide;
                return;
            }

            BeginFadeOut();
        }

        /// <summary>
        /// Updates loading progress (0-1 range).
        /// </summary>
        public void UpdateProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);

            if (_progressBar != null)
                _progressBar.value = progress;

            if (_progressText == null)
                return;

            int percent = Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);
            if (_currentProgressPercent == percent)
                return;

            _currentProgressPercent = percent;
            if (CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
            {
                try
                {
                    int length = WritePercent(percent, lease.Buffer);
                    _progressText.SetCharArray(lease.Buffer, 0, length);
                }
                finally
                {
                    CharBufferPool.Release(in lease);
                }

                return;
            }

            int fallbackLength = WritePercent(percent, _progressFallbackBuffer.AsSpan());
            _progressText.SetCharArray(_progressFallbackBuffer, 0, fallbackLength);
        }

        /// <summary>
        /// Updates the loading status message.
        /// </summary>
        public void UpdateStatus(string status)
        {
            if (string.IsNullOrEmpty(status))
                return;

            UpdateStatus(status.AsSpan());
        }

        /// <summary>
        /// Updates the loading status through a pooled char buffer, avoiding transient string allocations.
        /// </summary>
        public void UpdateStatus(ReadOnlySpan<char> status)
        {
            if (_statusText == null || status.Length <= 0)
                return;

            int safeLength = Mathf.Min(status.Length, _currentStatusBuffer.Length);
            if (IsCurrentStatus(status, safeLength))
                return;

            status.Slice(0, safeLength).CopyTo(_currentStatusBuffer);
            _currentStatusLength = safeLength;

            if (CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
            {
                try
                {
                    status.Slice(0, safeLength).CopyTo(lease.Buffer);
                    _statusText.SetCharArray(lease.Buffer, 0, safeLength);
                }
                finally
                {
                    CharBufferPool.Release(in lease);
                }

                return;
            }

            _statusText.SetCharArray(_currentStatusBuffer, 0, safeLength);
        }

        public void UpdatePipelineStage(LoadingPipelineStage stage)
        {
            if (_currentPipelineStage == stage && _statusText != null)
                return;

            _currentPipelineStage = stage;
            ResolvePipelineStageBuffer(stage, out char[] buffer, out int length);
            UpdateStatus(buffer.AsSpan(0, length));
        }

        /// <summary>
        /// Updates the loading tip.
        /// </summary>
        public void UpdateTip(string tip)
        {
            if (_tipText == null || string.Equals(_currentTipText, tip, StringComparison.Ordinal))
                return;

            _currentTipText = tip;
            _tipText.SetText(tip);
        }

        /// <summary>
        /// Sets a random loading tip.
        /// </summary>
        public void SetRandomTip()
        {
            UpdateTip(GetRandomTip());
        }

        private bool IsCurrentStatus(ReadOnlySpan<char> status, int safeLength)
        {
            if (_currentStatusLength != safeLength)
                return false;

            for (int i = 0; i < safeLength; i++)
            {
                if (_currentStatusBuffer[i] != status[i])
                    return false;
            }

            return true;
        }

        private static void ResolvePipelineStageBuffer(LoadingPipelineStage stage, out char[] buffer, out int length)
        {
            switch (stage)
            {
                case LoadingPipelineStage.PagingSectors:
                    buffer = PagingSectorsChars;
                    break;
                case LoadingPipelineStage.HydratingEntities:
                    buffer = HydratingEntitiesChars;
                    break;
                case LoadingPipelineStage.BuildingNavGrid:
                    buffer = BuildingNavGridChars;
                    break;
                case LoadingPipelineStage.SafeAupSnap:
                    buffer = SafeAupSnapChars;
                    break;
                case LoadingPipelineStage.Completed:
                    buffer = LoadCompleteChars;
                    break;
                default:
                    buffer = LoadingChars;
                    break;
            }

            length = buffer.Length;
        }

        private static int WritePercent(int percent, Span<char> buffer)
        {
            percent = Mathf.Clamp(percent, 0, 100);
            if (!percent.TryFormat(buffer, out int charsWritten))
                return 0;

            if (charsWritten >= buffer.Length)
                return charsWritten;

            buffer[charsWritten++] = '%';
            return charsWritten;
        }

        public void Tick(float deltaTime)
        {
            float unscaledDeltaTime = GetUnscaledDeltaTime();
            if (unscaledDeltaTime <= 0f)
                return;

            switch (_visibilityState)
            {
                case VisibilityState.FadingIn:
                    UpdateFadeIn(unscaledDeltaTime);
                    break;

                case VisibilityState.DelayBeforeHide:
                    _delayRemaining -= unscaledDeltaTime;
                    if (_delayRemaining <= 0f)
                        BeginFadeOut();
                    break;

                case VisibilityState.FadingOut:
                    UpdateFadeOut(unscaledDeltaTime);
                    break;
            }
        }

        private string GetRandomTip()
        {
            if (_loadingTips == null || _loadingTips.Length == 0)
                return "Loading...";

            return _loadingTips[NextTipIndex(_loadingTips.Length)];
        }

        private int NextTipIndex(int length)
        {
            if (length <= 1)
                return 0;

            uint state = _tipRandomState;
            if (state == 0u)
                state = 0xA341316Cu;

            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            _tipRandomState = state != 0u ? state : 0x9E3779B9u;
            return (int)(_tipRandomState % (uint)length);
        }

        private static uint MixSeed(uint seed)
        {
            unchecked
            {
                seed ^= 0x9E3779B9u;
                seed ^= seed >> 16;
                seed *= 0x7FEB352Du;
                seed ^= seed >> 15;
                seed *= 0x846CA68Bu;
                seed ^= seed >> 16;
                return seed != 0u ? seed : 0xA341316Cu;
            }
        }

        private void UpdateFadeIn(float unscaledDeltaTime)
        {
            float duration = Mathf.Max(0.0001f, _fadeDuration);
            _transitionElapsed += unscaledDeltaTime;
            float t = Mathf.Clamp01(_transitionElapsed / duration);
            _canvasGroup.alpha = _fadeStartAlpha + ((1f - _fadeStartAlpha) * t);

            if (t >= 1f)
                _visibilityState = VisibilityState.Visible;
        }

        private void BeginFadeOut()
        {
            _transitionElapsed = 0f;
            _fadeStartAlpha = _canvasGroup.alpha;
            _visibilityState = VisibilityState.FadingOut;
        }

        private void UpdateFadeOut(float unscaledDeltaTime)
        {
            float duration = Mathf.Max(0.0001f, _fadeDuration);
            _transitionElapsed += unscaledDeltaTime;
            float t = Mathf.Clamp01(_transitionElapsed / duration);
            _canvasGroup.alpha = _fadeStartAlpha * (1f - t);

            if (t < 1f)
                return;

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _isShowing = false;
            _visibilityState = VisibilityState.Hidden;
        }

        private float GetUnscaledDeltaTime()
        {
            float currentTime = Time.unscaledTime;
            if (_lastUnscaledTickTime <= 0f)
            {
                _lastUnscaledTickTime = currentTime;
                return 0f;
            }

            float delta = currentTime - _lastUnscaledTickTime;
            _lastUnscaledTickTime = currentTime;
            return delta > 0f ? delta : 0f;
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private bool TryRegisterRuntime()
        {
            if (_runtimeRegistered)
                return true;

            if (!Application.isPlaying || _serviceShuttingDown)
                return false;

            LoadingScreenController current = GlobalRegistry.LoadingScreen;
            if (current != null && current != this)
                return false;

            GlobalRegistry.RegisterLoadingScreenRuntime(this);
            _runtimeRegistered = ReferenceEquals(GlobalRegistry.LoadingScreen, this);
            return _runtimeRegistered;
        }

        private void TryUnregisterRuntime()
        {
            if (!_runtimeRegistered)
                return;

            GlobalRegistry.UnregisterLoadingScreenRuntime(this);
            _runtimeRegistered = false;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_loadingPanel == null)
                _loadingPanel = GetComponent<CanvasGroup>();
        }
#endif
    }
}

