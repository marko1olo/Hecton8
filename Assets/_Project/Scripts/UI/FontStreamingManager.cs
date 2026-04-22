using System.Text;
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
    public sealed class FontStreamingManager : MonoBehaviour, ITickable
    {
        private const string RootName = "FontStreamingStatus";
        private const string DefaultStatusText = "[REBOOTING LANG_MODULE...]";
        private const int MaxTrackedTexts = 512;
        private const int SwapBatchPerTick = 18;
        private const float StatusFadeOutSpeed = 6f;

        private static readonly Color StatusTextColor = new Color(0.82f, 0.96f, 0.92f, 0.96f);
        private static readonly Color StatusBackgroundColor = new Color(0.02f, 0.08f, 0.10f, 0.82f);
        private static readonly System.Collections.Generic.List<SuitHUDV4CanvasOverlay> s_overlayResolveBuffer =
            new System.Collections.Generic.List<SuitHUDV4CanvasOverlay>(2);

        // COLD ALLOC: TMP_Text[512] — staged font swap queue for active localized labels — owner: FontStreamingManager
        private readonly TMP_Text[] _swapQueue = new TMP_Text[MaxTrackedTexts];
        // COLD ALLOC: StringBuilder[64] — status label assembly for staged font streaming — owner: FontStreamingManager
        private readonly StringBuilder _statusBuilder = new StringBuilder(64);
        // COLD ALLOC: List[64] — active scene root cache for TMP registry bootstrap — owner: FontStreamingManager
        private readonly System.Collections.Generic.List<GameObject> _sceneRootBuffer = new System.Collections.Generic.List<GameObject>(64);
        // COLD ALLOC: List[512] — temporary TMP text scan buffer for registry bootstrap — owner: FontStreamingManager
        private readonly System.Collections.Generic.List<TMP_Text> _textScanBuffer = new System.Collections.Generic.List<TMP_Text>(512);

        private bool _registered;
        private bool _uiBuilt;
        private bool _streaming;
        private int _queueCount;
        private int _queueIndex;
        private int _lastStatusPercent = int.MinValue;
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
            _streaming = false;
            _queueCount = 0;
            _queueIndex = 0;
            ApplyVisibleAlpha(0f);
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            EnsureUiBuilt();

            if (_streaming)
            {
                ProcessSwapBatch();
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
                return;

            _targetFont = targetFont;
            CollectSwapQueue(targetFont);
            if (_queueCount <= 0)
            {
                _streaming = false;
                _lastStatusPercent = int.MinValue;
                ApplyVisibleAlpha(0f);
                return;
            }

            _streaming = true;
            _queueIndex = 0;
            _lastStatusPercent = int.MinValue;
            UpdateStatusLabel();
        }

        private void CollectSwapQueue(TMP_FontAsset targetFont)
        {
            _queueCount = 0;
            int registeredCount = TMP_TextRegistry.Count;
            for (int i = 0; i < registeredCount && _queueCount < _swapQueue.Length; i++)
            {
                HectonTextNode node = TMP_TextRegistry.GetNodeAt(i);
                TMP_Text text = node != null ? node.TextComponent : null;
                if (!IsSwapCandidate(text, targetFont))
                    continue;

                _swapQueue[_queueCount] = text;
                _queueCount++;
            }

            for (int i = _queueCount; i < _swapQueue.Length; i++)
                _swapQueue[i] = null;
        }

        private void ProcessSwapBatch()
        {
            int processed = 0;
            while (_queueIndex < _queueCount && processed < SwapBatchPerTick)
            {
                TMP_Text text = _swapQueue[_queueIndex];
                _swapQueue[_queueIndex] = null;
                _queueIndex++;
                processed++;

                if (text == null || _targetFont == null)
                    continue;

                text.font = _targetFont;
                if (_targetFont.material != null)
                    text.fontSharedMaterial = _targetFont.material;
                text.SetMaterialDirty();
                text.SetVerticesDirty();
            }

            UpdateStatusLabel();
            if (_queueIndex >= _queueCount)
            {
                _streaming = false;
                _queueCount = 0;
                _queueIndex = 0;
            }
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

                _statusLabel = labelObject.AddComponent<TextMeshProUGUI>(); // COLD ALLOC: TextMeshProUGUI[1] — localized font streaming status label — owner: FontStreamingManager
                _statusLabel.font = LocalizedFontResolver.ResolveReadableFont(null);
                _statusLabel.color = StatusTextColor;
                _statusLabel.fontSize = 14f;
                _statusLabel.textWrappingMode = TextWrappingModes.NoWrap;
                _statusLabel.alignment = TextAlignmentOptions.MidlineLeft;
                _statusLabel.raycastTarget = false;
                TMP_TextRegistry.EnsureRegistered(_statusLabel);
            }

            _statusLabel.text = string.Empty;
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

            int percent = _queueCount > 0
                ? Mathf.Clamp(Mathf.RoundToInt((_queueIndex / (float)_queueCount) * 100f), 0, 100)
                : 100;
            if (percent == _lastStatusPercent)
                return;

            _lastStatusPercent = percent;
            _statusBuilder.Clear();
            _statusBuilder.Append(DefaultStatusText);
            _statusBuilder.Append(' ');
            _statusBuilder.Append(percent);
            _statusBuilder.Append('%');
            _statusLabel.SetText(_statusBuilder);
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

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _registered = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

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
            return Object.FindAnyObjectByType<Canvas>();
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
    }
}
