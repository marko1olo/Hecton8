using System;
using Hecton.Localization;
using Hecton8.Core;
using TMPro;
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
    public sealed class FontStreamingManager : MonoBehaviour, ITickable, IUpdatable
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
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureRegistryNodes(SceneManager.GetActiveScene());
            RegisterToTickManager();
        }

        private void Start()
        {
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnregisterFromTickManager();
            ResetSwapState();
            ReleaseTrackedFontData();
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnregisterFromTickManager();
            ReleaseTrackedFontData();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            EnsureUiBuilt();

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
                ApplyVisibleAlpha(Mathf.MoveTowards(_visibleAlpha, 0f, dt * StatusFadeOutSpeed));
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

        private void EnsureUiBuilt()
        {
            if (_uiBuilt)
                return;

            if (_targetCanvas == null)
                _targetCanvas = ResolveTargetCanvas();

            if (_targetCanvas == null)
                return;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (canvasRoot == null)
                return;

            _root = FindExistingChild(canvasRoot, RootName);
            if (_root == null)
            {
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                rootObject.layer = canvasRoot.gameObject.layer;
                _root = rootObject.GetComponent<RectTransform>();
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = new Vector2(0.5f, 1f);
            _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, -94f);
            _root.sizeDelta = new Vector2(348f, 34f);
            _root.SetAsLastSibling();

            _group = _root.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _visibleAlpha = 0f;

            Image background = _root.GetComponent<Image>();
            background.color = StatusBackgroundColor;
            background.raycastTarget = false;

            if (_statusLabel == null)
                _statusLabel = FindText(_root, "StatusLabel");

            if (_statusLabel == null)
            {
                GameObject labelObject = new GameObject("StatusLabel", typeof(RectTransform));
                labelObject.layer = _root.gameObject.layer;
                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
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
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRegistryNodes(scene);
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
                ? Mathf.Clamp(Mathf.RoundToInt((_queueIndex / (float)_queueCount) * 100f), 0, 100)
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
            if (_group == null || Mathf.Approximately(_visibleAlpha, alpha))
                return;

            _visibleAlpha = alpha;
            _group.alpha = alpha;
        }

        private void RegisterToTickManager()
        {
            if (_registered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
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
            return (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>() : null);
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
                    return child.GetComponent<TextMeshProUGUI>();
            }

            return null;
        }

        private void WriteStatusLiteral(ReadOnlySpan<char> source)
        {
            EnsureStatusCapacity(source.Length);
            source.CopyTo(_statusBuffer);
            ApplyStatusBuffer(source.Length);
        }

        private void WriteStatusWithPercent(int percent)
        {
            ReadOnlySpan<char> prefix = DefaultStatusText.AsSpan();
            int requiredLength = prefix.Length + 5;
            EnsureStatusCapacity(requiredLength);
            prefix.CopyTo(_statusBuffer);

            int writeIndex = prefix.Length;
            _statusBuffer[writeIndex++] = ' ';
            if (!percent.TryFormat(_statusBuffer.AsSpan(writeIndex), out int charsWritten))
            {
                ApplyStatusBuffer(0);
                return;
            }

            writeIndex += charsWritten;
            _statusBuffer[writeIndex++] = '%';
            ApplyStatusBuffer(writeIndex);
        }

        private void EnsureStatusCapacity(int requiredLength)
        {
            if (_statusBuffer != null && _statusBuffer.Length >= requiredLength)
                return;

            int capacity = _statusBuffer == null ? 32 : _statusBuffer.Length;
            while (capacity < requiredLength)
                capacity <<= 1;

            _statusBuffer = new char[capacity]; // COLD ALLOC: char[capacity] â€” expanded font streaming status buffer â€” owner: FontStreamingManager
        }

        private void ApplyStatusBuffer(int length)
        {
            if (_statusLabel == null || _statusBuffer == null)
                return;

            int safeLength = Mathf.Clamp(length, 0, _statusBuffer.Length);
            _statusLabel.SetCharArray(_statusBuffer, 0, safeLength);
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
