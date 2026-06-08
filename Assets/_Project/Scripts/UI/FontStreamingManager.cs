using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Input;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Hecton8.UI
{
    /// <summary>
    /// Staged font-swap owner that spreads localized TMP font reassignment over multiple ticks.
    /// Prevents language-switch spikes when the UI has to swap many labels at once.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Font Streaming Manager")]
    public sealed class FontStreamingManager : MonoBehaviour, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001FontStreamingManagerSignalPushDropCount;
        private const string RootName = "FontStreamingStatus";
        private const string DefaultStatusText = "[REBOOTING LANG_MODULE...]";
        private const string BiosFallbackStatusText = "[BIOS FONT FALLBACK ACTIVE]";
        private const float StatusFadeOutSpeed = 6f;
        private const int FontReadinessTimeoutFrames = 2;
        private const ushort UIRescaleReasonLocalizedFontSwap = 1;
        private const ushort UIRescaleReasonAccessibilityTextScale = 2;
        private const uint AccessibilityTextScaleSourceHash = 0x41313332u;
        private static readonly Color StatusTextColor = new Color(0.82f, 0.96f, 0.92f, 0.96f);
        private static readonly Color StatusBackgroundColor = new Color(0.02f, 0.08f, 0.10f, 0.82f);
        private static readonly uint _fontSwapRescaleHash = unchecked((uint)LocHash.Compute("FontStreamingManager.UIRescale"));
        // COLD ALLOC: LabelSwapScheduler[1] — staged font swap queue owner for active localized labels — owner: FontStreamingManager
        private readonly LabelSwapScheduler _swapScheduler = new LabelSwapScheduler();
        // COLD ALLOC: char[96] — status label assembly for staged font streaming — owner: FontStreamingManager
        private char[] _statusBuffer = new char[96];
        private bool _registered;
        private bool _hotSwapListenerRegistered;
        private bool _uiBuilt;
        private bool _streaming;
        private int _queueCount;
        private int _queueIndex;
        private int _lastStatusPercent = int.MinValue;
        private int _fontReadinessStartFrame = -1;
        private bool _awaitingPrimaryFontReadiness;
        private bool _biosFallbackActive;
        private TMP_FontAsset _primaryFont;
        private Material _primaryFontMaterial;
        private TMP_FontAsset _biosFallbackFont;
        private Material _biosFallbackFontMaterial;
        private TMP_FontAsset _targetFont;
        private Material _targetFontMaterial;
        private Canvas _targetCanvas;
        [Header("Authored UI")]
        [SerializeField] private RectTransform _root;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _statusBackground;
        [SerializeField] private TextMeshProUGUI _statusLabel;
        private float _visibleAlpha;

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            LocalizationEvents.RegisterLanguageListener(this);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureRegistryNodes(SceneManager.GetActiveScene());
            EnsureUiBuilt();
            RefreshFontMaterialCachesCold();
            RegisterToTickManager();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            EnsureUiBuilt();
            RefreshFontMaterialCachesCold();
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            ResetSwapState();
            ReleaseTrackedFontData();
        }

        private void OnDestroy()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            ReleaseTrackedFontData();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            float dt = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            bool canShowStatus = _uiBuilt && _group != null && _statusLabel != null;

            if (_awaitingPrimaryFontReadiness)
                EvaluatePendingFontReadiness();

            if (_streaming)
            {
                ProcessSwapBatch();
                if (canShowStatus)
                    ApplyVisibleAlpha(1f);
                return;
            }

            if (_awaitingPrimaryFontReadiness)
            {
                if (canShowStatus)
                    ApplyVisibleAlpha(1f);
                return;
            }

            if (canShowStatus && _visibleAlpha > 0.001f)
                ApplyVisibleAlpha(MoveTowards(_visibleAlpha, 0f, dt * StatusFadeOutSpeed));
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            TMP_FontAsset targetFont = LocalizedFontResolver.ResolveReadableFontForLanguage(null, language);
            if (targetFont == null)
            {
                _primaryFont = null;
                _primaryFontMaterial = null;
                _targetFont = null;
                _targetFontMaterial = null;
                _streaming = false;
                _biosFallbackActive = false;
                _awaitingPrimaryFontReadiness = false;
                _fontReadinessStartFrame = -1;
                _queueCount = 0;
                _queueIndex = 0;
                _lastStatusPercent = int.MinValue;
                _swapScheduler.Clear();
                return;
            }

            _primaryFont = targetFont;
            _primaryFontMaterial = ResolveFontMaterialCold(targetFont);
            RefreshBiosFallbackFontCacheCold();
            _targetFont = null;
            _targetFontMaterial = null;
            _streaming = false;
            _biosFallbackActive = false;
            _awaitingPrimaryFontReadiness = true;
            _fontReadinessStartFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            _queueCount = 0;
            _queueIndex = 0;
            _swapScheduler.Clear();
            _lastStatusPercent = int.MinValue;
        }

        private void CollectSwapQueue(TMP_FontAsset targetFont)
        {
            _swapScheduler.Clear();
            _queueCount = 0;
            int registeredCount = TMP_TextRegistry.Count;
            int prefetchBudget = LocRegistry.ResolveVisibleTextOffsetPrefetchBudget(registeredCount);
            int prefetchedCount = 0;
            for (int i = 0; i < registeredCount; i++)
            {
                TMP_TextEntry entry = TMP_TextRegistry.GetEntryAt(i);
                TMP_Text text = entry.Text;
                if (!IsSwapCandidate(text, targetFont))
                    continue;

                int2 prefetchedSlice = new int2(-1, 0);
                bool hasPrefetchedSlice = false;
                if (!entry.IsUserInput &&
                    entry.HasLocalizationKey &&
                    prefetchedCount < prefetchBudget)
                {
                    uint keyHash = unchecked((uint)entry.LocalizationKeyHash);
                    if (LocRegistry.TryResolveVisibleTextOffsetSlice(keyHash, out prefetchedSlice))
                    {
                        hasPrefetchedSlice = true;
                        prefetchedCount++;
                    }
                }

                if (!_swapScheduler.Enqueue(entry, prefetchedSlice, hasPrefetchedSlice))
                    break;
            }

            _queueCount = _swapScheduler.PendingCount;
        }

        private void ProcessSwapBatch()
        {
            int processed = _swapScheduler.DrainTick(_targetFont, _targetFontMaterial);
            _queueIndex += processed;

            UpdateStatusLabel();
            if (!_swapScheduler.HasPending)
            {
                PublishLocalizedFontSwapRescaleRequest();
                _streaming = false;
                _queueCount = 0;
                _queueIndex = 0;
                if (_biosFallbackActive)
                {
                    _awaitingPrimaryFontReadiness = true;
                    _lastStatusPercent = int.MinValue;
                    UpdateStatusLabel();
                }
            }
        }

        /// <summary>
        /// Queues a sanitized global text scale request for accessibility and settings flows.
        /// </summary>
        public static bool RequestAccessibilityTextScale(float fontScale)
        {
            return PublishRescaleRequest(
                AccessibilityTextScaleSourceHash,
                UIRescaleReasonAccessibilityTextScale,
                0u,
                fontScale);
        }

        private static void PublishLocalizedFontSwapRescaleRequest()
        {
            PublishRescaleRequest(_fontSwapRescaleHash, UIRescaleReasonLocalizedFontSwap, 0u, 1f);
        }

        private static bool PublishRescaleRequest(uint sourceHash, ushort reason, uint flags, float fontScale)
        {
            float safeFontScale = ResolveSafeTextScale(fontScale);
            SignalBus<UIRescaleRequestSignal>.EnsureInitialized();
            UIRescaleRequestSignal signal = new UIRescaleRequestSignal
            {
                SourceHash = sourceHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Reason = reason,
                Language = (ushort)LocRegistry.ActiveLanguage,
                Flags = flags,
                FontScale = safeFontScale
            };

            if (!SignalBus<UIRescaleRequestSignal>.TryPushTracked(in signal, ref s_x001FontStreamingManagerSignalPushDropCount))
                return false;

            DiegeticHudManualLayout.ApplyGlobalRescaleRequest(in signal);
            return true;
        }

        private static float ResolveSafeTextScale(float fontScale)
        {
            if (!math.isfinite(fontScale) || fontScale <= 0f)
                return 1f;

            return math.clamp(fontScale, AccessibilitySettings.MinimumTextScale, AccessibilitySettings.MaximumTextScale);
        }

        private void EvaluatePendingFontReadiness()
        {
            if (_primaryFont == null)
            {
                ResetSwapState();
                return;
            }

            if (IsCachedFontReady(_primaryFont, _primaryFontMaterial))
            {
                _awaitingPrimaryFontReadiness = false;
                BeginSwapQueue(_primaryFont, _primaryFontMaterial, biosFallbackActive: false);
                return;
            }

            if (_biosFallbackActive)
            {
                UpdateStatusLabel();
                return;
            }

            if (Hecton8.Core.SystemDispatcher.CurrentFrameIndex - _fontReadinessStartFrame < FontReadinessTimeoutFrames)
            {
                UpdateStatusLabel();
                return;
            }

            if (!IsCachedFontReady(_biosFallbackFont, _biosFallbackFontMaterial))
            {
                ResetSwapState();
                return;
            }

            _awaitingPrimaryFontReadiness = false;
            BeginSwapQueue(_biosFallbackFont, _biosFallbackFontMaterial, biosFallbackActive: true);
        }

        private void BeginSwapQueue(TMP_FontAsset targetFont, Material targetFontMaterial, bool biosFallbackActive)
        {
            _targetFont = targetFont;
            _targetFontMaterial = targetFontMaterial;
            _biosFallbackActive = biosFallbackActive;
            CollectSwapQueue(targetFont);
            if (_queueCount <= 0)
            {
                if (_biosFallbackActive)
                {
                    _awaitingPrimaryFontReadiness = true;
                    _lastStatusPercent = int.MinValue;
                    UpdateStatusLabel();
                    ApplyVisibleAlpha(1f);
                    return;
                }

                ResetSwapState();
                return;
            }

            _streaming = true;
            _queueIndex = 0;
            _lastStatusPercent = int.MinValue;
            UpdateStatusLabel();
        }

        private bool EnsureUiBuilt()
        {
            if (_uiBuilt)
                return true;

            if (_targetCanvas == null)
                _targetCanvas = ResolveTargetCanvas();

            if (_targetCanvas == null)
                return false;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (canvasRoot == null)
                return false;

            if (_root == null)
                _root = FindExistingChild(canvasRoot, RootName);
            if (_root == null)
                return false;

            if (_group == null &&
                !_root.TryGetComponent(out _group))
            {
                return false;
            }

            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _visibleAlpha = 0f;

            if (_statusBackground == null &&
                !_root.TryGetComponent(out _statusBackground))
            {
                return false;
            }

            _statusBackground.color = StatusBackgroundColor;
            _statusBackground.raycastTarget = false;

            if (_statusLabel == null)
                _statusLabel = FindText(_root, "StatusLabel");

            if (_statusLabel == null)
                return false;

            _statusLabel.font = LocalizedFontResolver.ResolveReadableFont(null);
            _statusLabel.color = StatusTextColor;
            _statusLabel.fontSize = 14f;
            _statusLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _statusLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _statusLabel.raycastTarget = false;
            TMP_TextRegistry.EnsureRegistered(_statusLabel);

            ApplyStatusBuffer(0);
            _uiBuilt = true;
            return true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRegistryNodes(scene);
            EnsureUiBuilt();
            RefreshFontMaterialCachesCold();
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel == null)
                return;

            if (_awaitingPrimaryFontReadiness && !_streaming)
            {
                if (_biosFallbackActive)
                {
                    if (_lastStatusPercent == 1000)
                        return;

                    _lastStatusPercent = 1000;
                    WriteStatusLiteral(BiosFallbackStatusText.AsSpan());
                    return;
                }

                if (_lastStatusPercent == -1000)
                    return;

                _lastStatusPercent = -1000;
                WriteStatusLiteral(DefaultStatusText.AsSpan());
                return;
            }

            int percent = _queueCount > 0
                ? math.clamp((int)math.round((_queueIndex / (float)_queueCount) * 100f), 0, 100)
                : 100;
            if (percent == _lastStatusPercent)
                return;

            _lastStatusPercent = percent;
            WriteStatusWithPercent(percent);
        }

        private void ResetSwapState()
        {
            _streaming = false;
            _awaitingPrimaryFontReadiness = false;
            _biosFallbackActive = false;
            _primaryFont = null;
            _primaryFontMaterial = null;
            _targetFont = null;
            _targetFontMaterial = null;
            _queueCount = 0;
            _queueIndex = 0;
            _fontReadinessStartFrame = -1;
            _lastStatusPercent = int.MinValue;
            _swapScheduler.Clear();
            ApplyVisibleAlpha(0f);
        }

        private void ApplyVisibleAlpha(float alpha)
        {
            alpha = math.saturate(alpha);
            if (_group == null || math.abs(_visibleAlpha - alpha) <= 0.0001f)
                return;

            _visibleAlpha = alpha;
            _group.alpha = alpha;
        }

        private void RegisterToTickManager()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            UnregisterFromTickManager();
            if (currentService != null && isActiveAndEnabled)
                RegisterToTickManager();
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

        private static bool IsSwapCandidate(TMP_Text text, TMP_FontAsset targetFont)
        {
            if (text == null || targetFont == null)
                return false;

            if (text.font == targetFont || LocalizedFontResolver.IsNumericOnlyFont(text.font))
                return false;

            GameObject targetObject = text.gameObject;
            if (!targetObject.scene.IsValid())
                return false;

            return true;
        }

        private void EnsureRegistryNodes(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            Canvas canvas = ResolveTargetCanvas();
            if (canvas == null)
                return;

            EnsureRegistryNodesInHierarchy(canvas.transform);
        }

        private static void EnsureRegistryNodesInHierarchy(Transform root)
        {
            if (root == null)
                return;

            if (root.TryGetComponent(out TMP_Text text))
                TMP_TextRegistry.EnsureRegistered(text);

            for (int i = 0; i < root.childCount; i++)
                EnsureRegistryNodesInHierarchy(root.GetChild(i));
        }

        private static Canvas ResolveTargetCanvas()
        {
            for (int i = 0; i < SuitHUDV4CanvasOverlay.ActiveOverlayCount; i++)
            {
                SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.GetActiveOverlay(i);
                if (overlay != null && overlay.TargetCanvas != null)
                    return overlay.TargetCanvas;
            }

            SuitHUDV4CanvasOverlay activeOverlay = null;
            if (!SuitHUDV4CanvasOverlay.TryResolveActiveRuntime(ref activeOverlay))
                return null;

            activeOverlay.TryGetComponent(out Canvas canvas);
            return canvas;
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child as RectTransform;
            }

            return null;
        }

        private static TextMeshProUGUI FindText(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    child.TryGetComponent(out TextMeshProUGUI text);
                    return text;
                }
            }

            return null;
        }

        private void RefreshFontMaterialCachesCold()
        {
            if (_primaryFont != null)
                _primaryFontMaterial = ResolveFontMaterialCold(_primaryFont);

            RefreshBiosFallbackFontCacheCold();
        }

        private void RefreshBiosFallbackFontCacheCold()
        {
            _biosFallbackFont = LocalizedFontResolver.ResolveBiosFallbackFont();
            _biosFallbackFontMaterial = ResolveFontMaterialCold(_biosFallbackFont);
        }

        private static Material ResolveFontMaterialCold(TMP_FontAsset font)
        {
            return font != null ? font.material : null;
        }

        private static bool IsCachedFontReady(TMP_FontAsset font, Material material)
        {
            return font != null &&
                   material != null &&
                   ReferenceEquals(material, font.material) &&
                   LocalizedFontResolver.IsFontReady(font);
        }

        private void WriteStatusLiteral(ReadOnlySpan<char> source)
        {
            int length = CopyStatusSpan(source);
            ApplyStatusBuffer(length);
        }

        private void WriteStatusWithPercent(int percent)
        {
            ReadOnlySpan<char> prefix = DefaultStatusText.AsSpan();
            int writeIndex = CopyStatusSpan(prefix);
            if (_statusBuffer == null || writeIndex >= _statusBuffer.Length)
            {
                ApplyStatusBuffer(writeIndex);
                return;
            }

            _statusBuffer[writeIndex++] = ' ';
            if (writeIndex >= _statusBuffer.Length)
            {
                ApplyStatusBuffer(writeIndex);
                return;
            }

            Span<char> writableSpan = _statusBuffer.AsSpan(writeIndex, _statusBuffer.Length - writeIndex);
            if (!percent.TryFormat(writableSpan, out int charsWritten))
            {
                ApplyStatusBuffer(0);
                return;
            }

            writeIndex += charsWritten;
            if (writeIndex < _statusBuffer.Length)
                _statusBuffer[writeIndex++] = '%';

            ApplyStatusBuffer(writeIndex);
        }

        private int CopyStatusSpan(ReadOnlySpan<char> source)
        {
            if (_statusBuffer == null || _statusBuffer.Length == 0)
                return 0;

            int length = math.min(source.Length, _statusBuffer.Length);
            for (int i = 0; i < length; i++)
                _statusBuffer[i] = source[i];

            return length;
        }

        private void ApplyStatusBuffer(int length)
        {
            if (_statusLabel == null || _statusBuffer == null)
                return;

            int safeLength = math.clamp(length, 0, _statusBuffer.Length);
            _statusLabel.SetCharArray(_statusBuffer, 0, safeLength);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            float safeDelta = math.max(0f, maxDelta);
            float delta = target - current;
            if (math.abs(delta) <= safeDelta)
                return target;

            return current + math.sign(delta) * safeDelta;
        }

        private void ReleaseTrackedFontData()
        {
            LocalizedFontResolver.ReleaseCachedRuntimeFonts();
        }
    }
}
