using System;
using Hecton.Localization;
using Hecton8.Core;
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
    public sealed class FontStreamingManager : MonoBehaviour, ITickable, IUpdatable, ILocalizationLanguageChangedListener
    {
        private const string RootName = "FontStreamingStatus";
        private const string DefaultStatusText = "[REBOOTING LANG_MODULE...]";
        private const string BiosFallbackStatusText = "[BIOS FONT FALLBACK ACTIVE]";
        private const float StatusFadeOutSpeed = 6f;
        private const int FontReadinessTimeoutFrames = 2;

        private static readonly Color StatusTextColor = new Color(0.82f, 0.96f, 0.92f, 0.96f);
        private static readonly Color StatusBackgroundColor = new Color(0.02f, 0.08f, 0.10f, 0.82f);
        private static readonly System.Collections.Generic.List<SuitHUDV4CanvasOverlay> s_overlayResolveBuffer =
            new System.Collections.Generic.List<SuitHUDV4CanvasOverlay>(2);

        // COLD ALLOC: LabelSwapScheduler[1] â€” staged font swap queue owner for active localized labels â€” owner: FontStreamingManager
        private readonly LabelSwapScheduler _swapScheduler = new LabelSwapScheduler();
        // COLD ALLOC: char[96] â€” status label assembly for staged font streaming â€” owner: FontStreamingManager
        private char[] _statusBuffer = new char[96];
        // COLD ALLOC: List[64] â€” active scene root cache for TMP registry bootstrap â€” owner: FontStreamingManager
        private readonly System.Collections.Generic.List<GameObject> _sceneRootBuffer = new System.Collections.Generic.List<GameObject>(64);
        // COLD ALLOC: List[512] â€” temporary TMP text scan buffer for registry bootstrap â€” owner: FontStreamingManager
        private readonly System.Collections.Generic.List<TMP_Text> _textScanBuffer = new System.Collections.Generic.List<TMP_Text>(512);

        private bool _registered;
        private bool _uiBuilt;
        private bool _streaming;
        private int _queueCount;
        private int _queueIndex;
        private int _lastStatusPercent = int.MinValue;
        private int _fontReadinessStartFrame = -1;
        private bool _awaitingPrimaryFontReadiness;
        private bool _biosFallbackActive;
        private TMP_FontAsset _primaryFont;
        private TMP_FontAsset _targetFont;
        private Canvas _targetCanvas;
        private RectTransform _root;
        private CanvasGroup _group;
        private TextMeshProUGUI _statusLabel;
        private float _visibleAlpha;

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureRegistryNodes(SceneManager.GetActiveScene());
            EnsureUiBuilt(allowCreate: true);
            RegisterToTickManager();
        }

        private void Start()
        {
            EnsureUiBuilt(allowCreate: true);
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnregisterFromTickManager();
            ResetSwapState();
            ReleaseTrackedFontData();
        }

        private void OnDestroy()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnregisterFromTickManager();
            ReleaseTrackedFontData();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (!EnsureUiBuilt(allowCreate: false))
                return;

            if (_awaitingPrimaryFontReadiness)
                EvaluatePendingFontReadiness();

            if (_streaming)
            {
                ProcessSwapBatch();
                ApplyVisibleAlpha(1f);
                return;
            }

            if (_awaitingPrimaryFontReadiness)
            {
                ApplyVisibleAlpha(1f);
                return;
            }

            if (_visibleAlpha > 0.001f)
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
                ResetSwapState();
                return;
            }

            _primaryFont = targetFont;
            _targetFont = null;
            _streaming = false;
            _biosFallbackActive = false;
            _awaitingPrimaryFontReadiness = true;
            _fontReadinessStartFrame = Time.frameCount;
            _queueCount = 0;
            _queueIndex = 0;
            _swapScheduler.Clear();
            _lastStatusPercent = int.MinValue;
            UpdateStatusLabel();
            ApplyVisibleAlpha(1f);
        }

        private void CollectSwapQueue(TMP_FontAsset targetFont)
        {
            _swapScheduler.Clear();
            _queueCount = 0;
            int registeredCount = TMP_TextRegistry.Count;
            for (int i = 0; i < registeredCount; i++)
            {
                TMP_TextEntry entry = TMP_TextRegistry.GetEntryAt(i);
                TMP_Text text = entry.Text;
                if (!IsSwapCandidate(text, targetFont))
                    continue;

                if (!_swapScheduler.Enqueue(entry))
                    break;
            }

            _queueCount = _swapScheduler.PendingCount;
        }

        private void ProcessSwapBatch()
        {
            Material targetMaterial = _targetFont != null ? _targetFont.material : null;
            int processed = _swapScheduler.DrainTick(_targetFont, targetMaterial);
            _queueIndex += processed;

            UpdateStatusLabel();
            if (!_swapScheduler.HasPending)
            {
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

        private void EvaluatePendingFontReadiness()
        {
            if (_primaryFont == null)
            {
                ResetSwapState();
                return;
            }

            if (LocalizedFontResolver.IsFontReady(_primaryFont))
            {
                _awaitingPrimaryFontReadiness = false;
                BeginSwapQueue(_primaryFont, biosFallbackActive: false);
                return;
            }

            if (_biosFallbackActive)
            {
                UpdateStatusLabel();
                return;
            }

            if (Time.frameCount - _fontReadinessStartFrame < FontReadinessTimeoutFrames)
            {
                UpdateStatusLabel();
                return;
            }

            TMP_FontAsset biosFallback = LocalizedFontResolver.ResolveBiosFallbackFont();
            if (biosFallback == null)
            {
                ResetSwapState();
                return;
            }

            _awaitingPrimaryFontReadiness = false;
            BeginSwapQueue(biosFallback, biosFallbackActive: true);
        }

        private void BeginSwapQueue(TMP_FontAsset targetFont, bool biosFallbackActive)
        {
            _targetFont = targetFont;
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

        private bool EnsureUiBuilt(bool allowCreate)
        {
            if (_uiBuilt)
                return true;

            if (!allowCreate)
                return false;

            if (_targetCanvas == null)
                _targetCanvas = ResolveTargetCanvas();

            if (_targetCanvas == null)
                return false;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (canvasRoot == null)
                return false;

            _root = FindExistingChild(canvasRoot, RootName);
            if (_root == null)
            {
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                rootObject.layer = canvasRoot.gameObject.layer;
                rootObject.TryGetComponent(out _root);
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = new Vector2(0.5f, 1f);
            _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, -94f);
            _root.sizeDelta = new Vector2(348f, 34f);
            _root.SetAsLastSibling();

            if (!_root.TryGetComponent(out _group))
                _group = _root.gameObject.AddComponent<CanvasGroup>(); // COLD ALLOC: CanvasGroup[1] - repairs missing font streaming root component - owner: FontStreamingManager

            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _visibleAlpha = 0f;

            if (!_root.TryGetComponent(out Image background))
                background = _root.gameObject.AddComponent<Image>(); // COLD ALLOC: Image[1] - repairs missing font streaming root component - owner: FontStreamingManager

            background.color = StatusBackgroundColor;
            background.raycastTarget = false;

            if (_statusLabel == null)
                _statusLabel = FindText(_root, "StatusLabel");

            if (_statusLabel == null)
            {
                GameObject labelObject = new GameObject("StatusLabel", typeof(RectTransform));
                labelObject.layer = _root.gameObject.layer;
                labelObject.TryGetComponent(out RectTransform labelRect);
                labelRect.SetParent(_root, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 4f);
                labelRect.offsetMax = new Vector2(-12f, -4f);

                _statusLabel = labelObject.AddComponent<TextMeshProUGUI>(); // COLD ALLOC: TextMeshProUGUI[1] â€” localized font streaming status label â€” owner: FontStreamingManager
                _statusLabel.font = LocalizedFontResolver.ResolveReadableFont(null);
                _statusLabel.color = StatusTextColor;
                _statusLabel.fontSize = 14f;
                _statusLabel.textWrappingMode = TextWrappingModes.NoWrap;
                _statusLabel.alignment = TextAlignmentOptions.MidlineLeft;
                _statusLabel.raycastTarget = false;
                TMP_TextRegistry.EnsureRegistered(_statusLabel);
            }

            ApplyStatusBuffer(0);
            _uiBuilt = true;
            return true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRegistryNodes(scene);
            EnsureUiBuilt(allowCreate: true);
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
            _targetFont = null;
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

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
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

            _sceneRootBuffer.Clear();
            scene.GetRootGameObjects(_sceneRootBuffer);
            for (int rootIndex = 0; rootIndex < _sceneRootBuffer.Count; rootIndex++)
            {
                GameObject root = _sceneRootBuffer[rootIndex];
                if (root == null)
                    continue;

                _textScanBuffer.Clear();
                root.GetComponentsInChildren(true, _textScanBuffer);
                for (int textIndex = 0; textIndex < _textScanBuffer.Count; textIndex++)
                {
                    TMP_Text text = _textScanBuffer[textIndex];
                    if (text == null)
                        continue;

                    TMP_TextRegistry.EnsureRegistered(text);
                }
            }
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay.CopyActiveOverlaysTo(s_overlayResolveBuffer);
            for (int i = 0; i < s_overlayResolveBuffer.Count; i++)
            {
                SuitHUDV4CanvasOverlay overlay = s_overlayResolveBuffer[i];
                if (overlay != null && overlay.TargetCanvas != null)
                {
                    s_overlayResolveBuffer.Clear();
                    return overlay.TargetCanvas;
                }
            }

            s_overlayResolveBuffer.Clear();
            if (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance == null)
                return null;

            SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.TryGetComponent(out Canvas canvas);
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
            LocalizedFontResolver.TryClearDynamicFontData(_primaryFont);

            if (!ReferenceEquals(_targetFont, _primaryFont))
                LocalizedFontResolver.TryClearDynamicFontData(_targetFont);

            if (_statusLabel != null)
                LocalizedFontResolver.TryClearDynamicFontData(_statusLabel.font);

            LocalizedFontResolver.ReleaseCachedRuntimeFonts();
        }
    }
}
