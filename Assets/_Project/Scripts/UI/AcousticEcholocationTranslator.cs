using System;
using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Visor;
using Hecton8.World;
using System.Text;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    internal static class UiChildSpanUtility
    {
        private static readonly Transform[] s_childSnapshotBuffer = new Transform[128]; // COLD ALLOC: Transform[128] — shared UI child snapshot buffer — owner: UiChildSpanUtility

        public static RectTransform FindExistingChild(Transform parent, string childName)
        {
            ReadOnlySpan<Transform> children = SnapshotChildren(parent);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && child.name == childName)
                    return child as RectTransform;
            }

            return null;
        }

        public static void DestroyChildren(Transform parent)
        {
            ReadOnlySpan<Transform> children = SnapshotChildren(parent);
            for (int i = children.Length - 1; i >= 0; i--)
            {
                Transform child = children[i];
                if (child == null)
                    continue;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static ReadOnlySpan<Transform> SnapshotChildren(Transform parent)
        {
            if (parent == null)
                return ReadOnlySpan<Transform>.Empty;

            int childCount = Mathf.Min(parent.childCount, s_childSnapshotBuffer.Length);
            for (int i = 0; i < childCount; i++)
                s_childSnapshotBuffer[i] = parent.GetChild(i);

            return new ReadOnlySpan<Transform>(s_childSnapshotBuffer, 0, childCount);
        }
    }

    /// <summary>
    /// Player-owned diegetic sonar translator that converts active sonar contacts into terse PDA classification overlays.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Acoustic Echolocation Translator")]
    public sealed class AcousticEcholocationTranslator : MonoBehaviour, ITickable, IUpdatable
    {
        private enum ContactClassification : byte
        {
            None = 0,
            Leviathan = 1,
            Wreckage = 2
        }

        private const float OverlayWidth = 412f;
        private const float OverlayHeight = 92f;
        private const float VisibleDuration = 2.25f;
        private const float FadeDuration = 0.42f;
        private const float PulseDecaySharpness = 3.6f;
        private const float AnchorClassificationRadius = 112f;
        private const int MaxBioformContacts = 24;
        private const string OverlayName = "AcousticEcholocationTranslatorOverlay";
        private const string DefaultContactHeader = "[SONAR CONTACT]";
        private const string DefaultClassificationPrefix = "CLASSIFICATION";
        private const string DefaultLeviathanClass = "UNKNOWN BIOMASS // LEVIATHAN";
        private const string DefaultWreckageClass = "WRECKAGE // ANCHOR RETURN";

        private static readonly Color FrameColor = new Color(0.08f, 0.14f, 0.16f, 0.78f);
        private static readonly Color HeaderColor = new Color(0.72f, 0.96f, 0.88f, 0.96f);
        private static readonly Color ValueColor = new Color(0.86f, 0.98f, 0.92f, 0.96f);
        private static readonly Color AccentColor = new Color(0.38f, 0.92f, 0.88f, 0.18f);

        // COLD ALLOC: SpatialQueryHit[24] — active-sonar leviathan classification buffer — owner: AcousticEcholocationTranslator
        private readonly SpatialQueryHit[] _bioformContacts = new SpatialQueryHit[MaxBioformContacts];
        // COLD ALLOC: StringBuilder[128] — sonar contact line assembly buffer — owner: AcousticEcholocationTranslator
        private readonly StringBuilder _lineBuilder = new StringBuilder(128);

        [Header("── Font ──────────────────")]
        [Tooltip("Optional readable font override for the acoustic translator overlay.")]
        [SerializeField] private TMP_FontAsset labelFont;
        [Tooltip("Optional numeric font override for distance readouts.")]
        [SerializeField] private TMP_FontAsset numericFont;

        private bool _uiBuilt;
        private bool _tickRegistered;
        private bool _pendingPing;
        private float _visibleTimer;
        private float _fadeTimer;
        private float _pulse01;
        private Canvas _targetCanvas;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private RectTransform _root;
        private CanvasGroup _group;
        private Image _background;
        private TextMeshProUGUI _headerLabel;
        private TextMeshProUGUI _classificationLabel;
        private string _lastHeaderText = string.Empty;
        private string _lastClassificationText = string.Empty;
        private string _localizedContactHeader = DefaultContactHeader;
        private string _localizedClassificationPrefix = DefaultClassificationPrefix;
        private string _localizedLeviathanClass = DefaultLeviathanClass;
        private string _localizedWreckageClass = DefaultWreckageClass;

        private void OnEnable()
        {
            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            numericFont = LocalizedFontResolver.ResolveNumericFont(numericFont, labelFont);
            ResolveOwners();
            EnsureUiBuilt();
            RefreshLocalizedCache();
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            SpectrumEvents.OnSonarPingSent += HandleSonarPingSent;
            SpectrumEvents.OnSonarSnapshotUpdated += HandleSonarSnapshotUpdated;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            SpectrumEvents.OnSonarSnapshotUpdated -= HandleSonarSnapshotUpdated;
            UnregisterFromTickManager();
            ApplyRootAlpha(0f);
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            SpectrumEvents.OnSonarSnapshotUpdated -= HandleSonarSnapshotUpdated;
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_group == null)
            {
                UnregisterFromTickManager();
                return;
            }

            if (_pulse01 > 0f)
                _pulse01 = Mathf.Max(0f, _pulse01 - (dt * PulseDecaySharpness));

            if (_visibleTimer > 0f)
            {
                _visibleTimer -= dt;
                ApplyVisualState(1f);
                return;
            }

            if (_fadeTimer > 0f)
            {
                _fadeTimer = Mathf.Max(0f, _fadeTimer - dt);
                float alpha = FadeDuration > 0.0001f
                    ? Mathf.Clamp01(_fadeTimer / FadeDuration)
                    : 0f;
                ApplyVisualState(alpha);
                if (_fadeTimer > 0f)
                    return;
            }

            ApplyRootAlpha(0f);
            _lastHeaderText = string.Empty;
            _lastClassificationText = string.Empty;
            UnregisterFromTickManager();
        }

        private void HandleSonarPingSent(float intensity)
        {
            _pendingPing = true;
            _pulse01 = Mathf.Max(_pulse01, Mathf.Clamp01(intensity));
        }

        private void HandleSonarSnapshotUpdated(SpatialSonarSnapshot snapshot)
        {
            if (!_pendingPing)
                return;

            _pendingPing = false;
            ResolveOwners();
            EnsureUiBuilt();
            if (_classificationLabel == null || _headerLabel == null)
                return;

            if (!TryResolveContact(snapshot, out ContactClassification classification, out int distanceMeters))
                return;

            ShowClassification(classification, distanceMeters);
        }

        private void HandleLanguageChanged(GameLanguage _)
        {
            RefreshLocalizedCache();
            _lastHeaderText = string.Empty;
            _lastClassificationText = string.Empty;
        }

        private bool TryResolveContact(SpatialSonarSnapshot snapshot, out ContactClassification classification, out int distanceMeters)
        {
            classification = ContactClassification.None;
            distanceMeters = 0;

            if (TryResolveNearestLeviathan(snapshot, out distanceMeters))
            {
                classification = ContactClassification.Leviathan;
                return true;
            }

            if (TryResolveNearestAbyssalAnchor(out distanceMeters))
            {
                classification = ContactClassification.Wreckage;
                return true;
            }

            return false;
        }

        private bool TryResolveNearestLeviathan(SpatialSonarSnapshot snapshot, out int distanceMeters)
        {
            distanceMeters = 0;
            if (!snapshot.HasNearestBioform)
                return false;

            float searchRadius = Mathf.Clamp(snapshot.NearestBioformDistanceMeters + 12f, 18f, 180f);
            int contactCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                transform.position,
                searchRadius,
                SpatialTargetKind.Bioform,
                _bioformContacts);

            float nearestDistanceSqr = float.MaxValue;
            for (int i = 0; i < contactCount; i++)
            {
                FaunaBrain brain = _bioformContacts[i].Owner as FaunaBrain;
                if (brain == null || brain.IsDead)
                    continue;

                FaunaSpeciesProfile speciesProfile = brain.SpeciesProfile;
                if (speciesProfile == null || !speciesProfile.isLeviathan)
                    continue;

                float candidateDistanceSqr = _bioformContacts[i].DistanceSqr;
                if (candidateDistanceSqr >= nearestDistanceSqr)
                    continue;

                nearestDistanceSqr = candidateDistanceSqr;
            }

            if (nearestDistanceSqr == float.MaxValue)
                return false;

            distanceMeters = Mathf.RoundToInt(Mathf.Sqrt(nearestDistanceSqr));
            return true;
        }

        private bool TryResolveNearestAbyssalAnchor(out int distanceMeters)
        {
            distanceMeters = 0;
            if (_vegetationBridge == null ||
                !_vegetationBridge.TryGetActiveAbyssalAnchorPayload(out NativeArray<Vector3> anchors, out int count) ||
                !anchors.IsCreated ||
                count <= 0)
            {
                return false;
            }

            float maxDistanceSqr = AnchorClassificationRadius * AnchorClassificationRadius;
            float nearestDistanceSqr = float.MaxValue;
            Vector3 origin = transform.position;
            int limit = Mathf.Min(count, anchors.Length);
            for (int i = 0; i < limit; i++)
            {
                float candidateDistanceSqr = (anchors[i] - origin).sqrMagnitude;
                if (candidateDistanceSqr > maxDistanceSqr || candidateDistanceSqr >= nearestDistanceSqr)
                    continue;

                nearestDistanceSqr = candidateDistanceSqr;
            }

            if (nearestDistanceSqr == float.MaxValue)
                return false;

            distanceMeters = Mathf.RoundToInt(Mathf.Sqrt(nearestDistanceSqr));
            return true;
        }

        private void ShowClassification(ContactClassification classification, int distanceMeters)
        {
            string headerText = ResolveStressReactiveText(_localizedContactHeader);
            string classText = classification == ContactClassification.Leviathan
                ? _localizedLeviathanClass
                : _localizedWreckageClass;

            _lineBuilder.Clear();
            _lineBuilder.Append(_localizedClassificationPrefix);
            _lineBuilder.Append(": ");
            _lineBuilder.Append(classText);
            _lineBuilder.Append(" // ");
            _lineBuilder.Append(distanceMeters);
            _lineBuilder.Append('M');
            string classificationText = ResolveStressReactiveText(_lineBuilder.ToString());

            if (!string.Equals(_lastHeaderText, headerText, System.StringComparison.Ordinal))
            {
                _headerLabel.text = headerText;
                _lastHeaderText = headerText;
            }

            if (!string.Equals(_lastClassificationText, classificationText, System.StringComparison.Ordinal))
            {
                _classificationLabel.text = classificationText;
                _lastClassificationText = classificationText;
            }

            _visibleTimer = VisibleDuration;
            _fadeTimer = FadeDuration;
            _pulse01 = Mathf.Max(_pulse01, 1f);
            ApplyVisualState(1f);
            RegisterToTickManager();
        }

        private void ApplyVisualState(float alpha)
        {
            ApplyRootAlpha(alpha);
            if (_background != null)
                _background.color = new Color(FrameColor.r, FrameColor.g, FrameColor.b, Mathf.Lerp(0f, FrameColor.a, alpha));

            if (_classificationLabel != null)
                _classificationLabel.color = Color.Lerp(ValueColor, HeaderColor, _pulse01 * 0.45f);
        }

        private void ResolveOwners()
        {
            if (_vegetationBridge == null)
                _vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            if (_targetCanvas == null)
                _targetCanvas = ResolveTargetCanvas();
        }

        private void RefreshLocalizedCache()
        {
            _localizedContactHeader = ResolveLocalized(LocalizationKeys.SONAR_CONTACT_HEADER, DefaultContactHeader);
            _localizedClassificationPrefix = ResolveLocalized(LocalizationKeys.SONAR_CLASSIFICATION_PREFIX, DefaultClassificationPrefix);
            _localizedLeviathanClass = ResolveLocalized(LocalizationKeys.SONAR_CLASS_LEVIATHAN, DefaultLeviathanClass);
            _localizedWreckageClass = ResolveLocalized(LocalizationKeys.SONAR_CLASS_WRECKAGE, DefaultWreckageClass);
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt || _targetCanvas == null)
                return;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (canvasRoot == null)
                return;

            _root = FindExistingChild(canvasRoot, OverlayName);
            if (_root == null)
            {
                // COLD ALLOC: GameObject[1] — sonar translator HUD panel host — owner: AcousticEcholocationTranslator
                GameObject rootObject = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                rootObject.layer = canvasRoot.gameObject.layer;
                _root = rootObject.GetComponent<RectTransform>();
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = new Vector2(1f, 1f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(1f, 1f);
            _root.anchoredPosition = new Vector2(-42f, -86f);
            _root.sizeDelta = new Vector2(OverlayWidth, OverlayHeight);
            _root.localScale = Vector3.one;
            _root.SetAsLastSibling();

            _group = _root.GetComponent<CanvasGroup>();
            if (_group == null)
                _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _background = _root.GetComponent<Image>();
            _background.color = FrameColor;
            _background.raycastTarget = false;

            ClearChildren(_root);
            CreateRule(new Vector2(18f, -18f), new Vector2(-18f, -18f));
            CreateRule(new Vector2(18f, -74f), new Vector2(-18f, -74f));

            _headerLabel = CreateText("Header", labelFont, 12f, FontStyles.Bold, HeaderColor, TextAlignmentOptions.Left);
            Anchor(_headerLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -12f), new Vector2(-20f, -34f));

            _classificationLabel = CreateText("Classification", numericFont, 13f, FontStyles.Bold, ValueColor, TextAlignmentOptions.Left);
            Anchor(_classificationLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -34f), new Vector2(-20f, -72f));

            _uiBuilt = true;
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private void ApplyRootAlpha(float alpha)
        {
            if (_group != null && !Mathf.Approximately(_group.alpha, alpha))
                _group.alpha = alpha;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>() : null);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static string ResolveStressReactiveText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.ApplyHullStressCorruptionIfNeeded(text)
                : text;
        }

        private void CreateRule(Vector2 leftOffset, Vector2 rightOffset)
        {
            RectTransform rule = CreateRect(_root, "Rule");
            Image image = rule.gameObject.AddComponent<Image>();
            image.color = AccentColor;
            image.raycastTarget = false;
            Anchor(rule, new Vector2(0f, 1f), new Vector2(1f, 1f), leftOffset, rightOffset);
            rule.sizeDelta = new Vector2(0f, 1f);
        }

        private TextMeshProUGUI CreateText(string name, TMP_FontAsset fontAsset, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(_root, name);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = fontAsset;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            LocalizedTMPAutoSizer.Configure(text, size * 0.7f, size, TextOverflowModes.Truncate, TextWrappingModes.NoWrap);
            TMP_TextRegistry.EnsureRegistered(text);
            return text;
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            return UiChildSpanUtility.FindExistingChild(parent, childName);
        }

        private static void ClearChildren(Transform parent)
        {
            UiChildSpanUtility.DestroyChildren(parent);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }

    /// <summary>
    /// Fast sonar-driver boot log shown on active sonar pings.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Terminal Boot Sequence")]
    public sealed class TerminalBootSequence : MonoBehaviour, ITickable, IUpdatable
    {
        private enum SequenceState : byte
        {
            Hidden = 0,
            Typing = 1,
            Hold = 2,
            Fade = 3
        }

        private const float CharacterRevealRate = 210f;
        private const float HoldDuration = 0.22f;
        private const float FadeSharpness = 7.5f;
        private const float HiddenAlphaCutoff = 0.01f;
        private const float OverlayWidth = 436f;
        private const float OverlayHeight = 148f;
        private const string OverlayName = "TerminalBootSequenceOverlay";
        private const string DefaultStatusOk = "[OK]";
        private const string DefaultStatusDegraded = "[DEGRADED]";
        private const string DefaultStatusFailed = "[FAILED]";

        [Header("── Font ──────────────────")]
        [Tooltip("Optional readable font override for the sonar terminal boot feed.")]
        [SerializeField] private TMP_FontAsset font;

        private RectTransform _overlayRoot;
        private CanvasGroup _overlayGroup;
        private TextMeshProUGUI _consoleLabel;
        private bool _uiBuilt;
        private bool _tickRegistered;
        private SequenceState _state;
        private float _stateTimer;
        private float _visibleCharacterProgress;
        private int _visibleCharacterTarget;
        private HectonSurvivalSystem _survivalSystem;

        private void OnEnable()
        {
            font = LocalizedFontResolver.ResolveReadableFont(font);
            ResolveOwners();
            EnsureUiBuilt();
            SpectrumEvents.OnSonarPingSent += HandleSonarPingSent;
        }

        private void OnDisable()
        {
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            UnregisterFromTickManager();
            HideOverlay();
        }

        private void OnDestroy()
        {
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_consoleLabel == null || _overlayGroup == null || _state == SequenceState.Hidden)
                return;

            switch (_state)
            {
                case SequenceState.Typing:
                    _visibleCharacterProgress += dt * CharacterRevealRate;
                    int visibleCharacters = Mathf.Min(_visibleCharacterTarget, Mathf.FloorToInt(_visibleCharacterProgress));
                    if (_consoleLabel.maxVisibleCharacters != visibleCharacters)
                        _consoleLabel.maxVisibleCharacters = visibleCharacters;

                    if (visibleCharacters >= _visibleCharacterTarget)
                    {
                        _state = SequenceState.Hold;
                        _stateTimer = HoldDuration;
                    }
                    break;

                case SequenceState.Hold:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                        _state = SequenceState.Fade;
                    break;

                case SequenceState.Fade:
                    _overlayGroup.alpha = Mathf.Lerp(_overlayGroup.alpha, 0f, 1f - Mathf.Exp(-FadeSharpness * dt));
                    if (_overlayGroup.alpha <= HiddenAlphaCutoff)
                    {
                        HideOverlay();
                        UnregisterFromTickManager();
                    }
                    break;
            }
        }

        private void HandleSonarPingSent(float intensity)
        {
            if (intensity <= 0.001f)
                return;

            ResolveOwners();
            EnsureUiBuilt();
            if (_consoleLabel == null || _overlayGroup == null)
                return;

            _consoleLabel.text = BuildSequenceText();
            _consoleLabel.ForceMeshUpdate();
            _visibleCharacterTarget = _consoleLabel.textInfo.characterCount;
            _visibleCharacterProgress = 0f;
            _consoleLabel.maxVisibleCharacters = 0;
            _overlayGroup.alpha = 1f;
            _overlayGroup.blocksRaycasts = false;
            _overlayGroup.interactable = false;
            _state = SequenceState.Typing;
            _stateTimer = 0f;
            RegisterToTickManager();
        }

        private void ResolveOwners()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);
        }

        private string BuildSequenceText()
        {
            float integrity01 = _survivalSystem != null ? _survivalSystem.IntegrityNormalized : 0f;
            float energy01 = _survivalSystem != null ? _survivalSystem.EnergyNormalized : 0f;
            float hullStress01 = _survivalSystem != null ? Mathf.Clamp01(1f - integrity01) : 1f;
            string hullStatus = ResolveIntegrityStatus(integrity01);
            string powerStatus = energy01 >= 0.25f ? DefaultStatusOk : DefaultStatusDegraded;
            string linkStatus = hullStress01 <= 0.18f ? DefaultStatusOk : DefaultStatusDegraded;

            System.Text.StringBuilder builder = StringBuilderPool.Get();
            builder.Append(DefaultStatusOk).AppendLine(" MOUNTING SONAR_DRIVER...");
            builder.Append(DefaultStatusOk).AppendLine(" CALIBRATING LIDAR ARRAY...");
            builder.Append(linkStatus).Append(" ACOUSTIC BUS LINK... HULL ")
                .Append(_survivalSystem != null ? Mathf.RoundToInt(integrity01 * 100f) : 0)
                .Append('%')
                .AppendLine();
            builder.Append(powerStatus).Append(" POWER FEED... ")
                .Append(_survivalSystem != null ? Mathf.RoundToInt(energy01 * 100f) : 0)
                .Append('%')
                .AppendLine();
            builder.Append(hullStatus).Append(" NOISE FILTER... STRESS ")
                .Append(_survivalSystem != null ? Mathf.RoundToInt(hullStress01 * 100f) : 100)
                .Append('%');

            string text = builder.ToString();
            StringBuilderPool.Return(builder);
            return text;
        }

        private static string ResolveIntegrityStatus(float integrity01)
        {
            if (integrity01 < 0.55f)
                return DefaultStatusFailed;

            if (integrity01 < 0.82f)
                return DefaultStatusDegraded;

            return DefaultStatusOk;
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt)
                return;

            Canvas targetCanvas = ResolveTargetCanvas();
            if (targetCanvas == null)
                return;

            RectTransform contentRoot = HectonUIScaler.ResolveContentRoot(targetCanvas);
            if (contentRoot == null)
                return;

            _overlayRoot = FindExistingChild(contentRoot, OverlayName);
            if (_overlayRoot == null)
            {
                // COLD ALLOC: GameObject[1] — sonar terminal boot overlay host — owner: TerminalBootSequence
                GameObject overlayObject = new GameObject(
                    OverlayName,
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(Image));
                overlayObject.layer = contentRoot.gameObject.layer;
                _overlayRoot = overlayObject.GetComponent<RectTransform>();
                _overlayRoot.SetParent(contentRoot, false);
            }

            _overlayRoot.anchorMin = new Vector2(0f, 1f);
            _overlayRoot.anchorMax = new Vector2(0f, 1f);
            _overlayRoot.pivot = new Vector2(0f, 1f);
            _overlayRoot.anchoredPosition = new Vector2(34f, -188f);
            _overlayRoot.sizeDelta = new Vector2(OverlayWidth, OverlayHeight);
            _overlayRoot.localScale = Vector3.one;
            _overlayRoot.SetAsLastSibling();

            _overlayGroup = _overlayRoot.GetComponent<CanvasGroup>();
            if (_overlayGroup == null)
                _overlayGroup = _overlayRoot.gameObject.AddComponent<CanvasGroup>();
            _overlayGroup.alpha = 0f;
            _overlayGroup.blocksRaycasts = false;
            _overlayGroup.interactable = false;

            Image background = _overlayRoot.GetComponent<Image>();
            if (background == null)
                background = _overlayRoot.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.07f, 0.08f, 0.72f);
            background.raycastTarget = false;

            ClearChildren(_overlayRoot);

            GameObject textObject = new GameObject("ConsoleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.layer = _overlayRoot.gameObject.layer;
            RectTransform textRoot = textObject.GetComponent<RectTransform>();
            textRoot.SetParent(_overlayRoot, false);
            textRoot.anchorMin = Vector2.zero;
            textRoot.anchorMax = Vector2.one;
            textRoot.offsetMin = new Vector2(16f, 12f);
            textRoot.offsetMax = new Vector2(-16f, -12f);

            _consoleLabel = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null)
                _consoleLabel.font = font;

            _consoleLabel.fontSize = 16f;
            _consoleLabel.color = new Color(0.78f, 0.96f, 0.88f, 1f);
            _consoleLabel.alignment = TextAlignmentOptions.TopLeft;
            _consoleLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _consoleLabel.overflowMode = TextOverflowModes.Overflow;
            _consoleLabel.maxVisibleCharacters = int.MaxValue;

            _uiBuilt = true;
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private void HideOverlay()
        {
            _state = SequenceState.Hidden;
            _stateTimer = 0f;
            _visibleCharacterProgress = 0f;
            _visibleCharacterTarget = 0;

            if (_overlayGroup != null)
            {
                _overlayGroup.alpha = 0f;
                _overlayGroup.blocksRaycasts = false;
                _overlayGroup.interactable = false;
            }

            if (_consoleLabel != null)
                _consoleLabel.maxVisibleCharacters = int.MaxValue;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>() : null);
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            return UiChildSpanUtility.FindExistingChild(parent, childName);
        }

        private static void ClearChildren(Transform parent)
        {
            UiChildSpanUtility.DestroyChildren(parent);
        }
    }

    /// <summary>
    /// Player-owned overlay that renders pooled spatial-audio captions around the HUD center.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Audio Caption Overlay")]
    public sealed class AudioCaptionOverlay : MonoBehaviour, ITickable, IUpdatable
    {
        private const int SlotCount = 8;
        private const float DefaultDuration = 1.65f;
        private const float MinDuration = 0.35f;
        private const float RadiusMin = 112f;
        private const float RadiusMax = 188f;
        private const float VerticalBias = -14f;
        private const float BehindFlipBias = 0.18f;
        private const string OverlayName = "AudioCaptionOverlay";

        private static readonly Color CaptionColor = new Color(0.86f, 0.97f, 0.92f, 0.94f);
        private static readonly Color CaptionShadowColor = new Color(0.06f, 0.11f, 0.12f, 0.84f);
        private static readonly Vector2 CaptionSize = new Vector2(240f, 44f);

        private struct CaptionSlot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public TextMeshProUGUI Label;
            public bool Active;
            public float Age;
            public float Duration;
            public float Intensity;
            public Vector3 WorldPosition;
        }

        [Header("── Font ──────────────────")]
        [Tooltip("Readable font override for spatial audio captions.")]
        [SerializeField] private TMP_FontAsset labelFont;

        private Canvas _targetCanvas;
        private Camera _viewCamera;
        private RectTransform _overlayRoot;
        private bool _tickRegistered;
        private bool _uiBuilt;
        // COLD ALLOC: CaptionSlot[8] — pooled spatial audio caption slots — owner: AudioCaptionOverlay
        private readonly CaptionSlot[] _slots = new CaptionSlot[SlotCount];

        private void OnEnable()
        {
            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            EnsureUiBuilt();
            AudioCaptionEvents.OnCaptionRequested += HandleCaptionRequested;
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            AudioCaptionEvents.OnCaptionRequested -= HandleCaptionRequested;
            UnregisterFromTickManager();
            HideAllSlots();
        }

        private void OnDestroy()
        {
            AudioCaptionEvents.OnCaptionRequested -= HandleCaptionRequested;
            UnregisterFromTickManager();
        }

        public void Tick(float dt)
        {
            if (!_uiBuilt)
            {
                EnsureUiBuilt();
                if (!_uiBuilt)
                    return;
            }

            if (_viewCamera == null)
                ResolveViewCamera();

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Active)
                    continue;

                CaptionSlot slot = _slots[i];
                slot.Age += dt;
                if (slot.Age >= slot.Duration)
                {
                    slot.Active = false;
                    ApplySlotHidden(ref slot);
                    _slots[i] = slot;
                    continue;
                }

                UpdateSlotPose(ref slot);
                _slots[i] = slot;
            }
        }

        private void HandleCaptionRequested(AudioCaptionRequest request)
        {
            EnsureUiBuilt();
            if (!_uiBuilt)
                return;

            int slotIndex = AcquireSlotIndex();
            ref CaptionSlot slot = ref _slots[slotIndex];
            slot.Active = true;
            slot.Age = 0f;
            slot.Duration = Mathf.Max(MinDuration, request.DurationSeconds > 0f ? request.DurationSeconds : DefaultDuration);
            slot.Intensity = Mathf.Clamp01(request.Intensity);
            slot.WorldPosition = request.WorldPosition;
            if (!string.Equals(slot.Label.text, request.CaptionText, System.StringComparison.Ordinal))
                slot.Label.text = request.CaptionText;

            slot.Group.alpha = 1f;
            slot.Group.blocksRaycasts = false;
            slot.Group.interactable = false;
            UpdateSlotPose(ref slot);
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt)
                return;

            _targetCanvas = ResolveTargetCanvas();
            if (_targetCanvas == null)
                return;

            RectTransform contentRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (contentRoot == null)
                return;

            _overlayRoot = FindExistingChild(contentRoot, OverlayName);
            if (_overlayRoot == null)
            {
                // COLD ALLOC: GameObject[1] — spatial audio caption host — owner: AudioCaptionOverlay
                GameObject overlayObject = new GameObject(OverlayName, typeof(RectTransform));
                overlayObject.layer = contentRoot.gameObject.layer;
                _overlayRoot = overlayObject.GetComponent<RectTransform>();
                _overlayRoot.SetParent(contentRoot, false);
            }

            _overlayRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _overlayRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _overlayRoot.pivot = new Vector2(0.5f, 0.5f);
            _overlayRoot.anchoredPosition = Vector2.zero;
            _overlayRoot.sizeDelta = Vector2.zero;
            _overlayRoot.localScale = Vector3.one;
            _overlayRoot.SetAsLastSibling();

            ClearChildren(_overlayRoot);

            for (int i = 0; i < _slots.Length; i++)
                BuildSlot(i);

            ResolveViewCamera();
            _uiBuilt = true;
        }

        private void BuildSlot(int index)
        {
            // COLD ALLOC: GameObject[1] — pooled caption slot root — owner: AudioCaptionOverlay
            GameObject slotObject = new GameObject(
                "CaptionSlot_" + index,
                typeof(RectTransform),
                typeof(CanvasGroup));
            slotObject.layer = _overlayRoot.gameObject.layer;
            RectTransform slotRoot = slotObject.GetComponent<RectTransform>();
            slotRoot.SetParent(_overlayRoot, false);
            slotRoot.anchorMin = new Vector2(0.5f, 0.5f);
            slotRoot.anchorMax = new Vector2(0.5f, 0.5f);
            slotRoot.pivot = new Vector2(0.5f, 0.5f);
            slotRoot.sizeDelta = CaptionSize;
            slotRoot.anchoredPosition = new Vector2(0f, VerticalBias);

            CanvasGroup group = slotObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // COLD ALLOC: GameObject[1] — pooled caption text owner — owner: AudioCaptionOverlay
            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.layer = slotObject.layer;
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(slotRoot, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = labelFont != null ? labelFont : TMP_Settings.defaultFontAsset;
            text.fontSize = 13f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            text.color = CaptionColor;
            text.outlineColor = CaptionShadowColor;
            text.outlineWidth = 0.18f;
            text.text = string.Empty;
            text.raycastTarget = false;

            _slots[index] = new CaptionSlot
            {
                Root = slotRoot,
                Group = group,
                Label = text,
                Active = false,
                Age = 0f,
                Duration = DefaultDuration,
                Intensity = 0f,
                WorldPosition = Vector3.zero
            };
        }

        private void ResolveViewCamera()
        {
            if (_targetCanvas != null && _targetCanvas.worldCamera != null)
            {
                _viewCamera = _targetCanvas.worldCamera;
                return;
            }

            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null && overlay.TargetCanvas.worldCamera != null)
            {
                _viewCamera = overlay.TargetCanvas.worldCamera;
                return;
            }

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
                _viewCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
        }

        private int AcquireSlotIndex()
        {
            int oldestIndex = 0;
            float oldestAge = float.MinValue;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Active)
                    return i;

                if (_slots[i].Age > oldestAge)
                {
                    oldestAge = _slots[i].Age;
                    oldestIndex = i;
                }
            }

            return oldestIndex;
        }

        private void UpdateSlotPose(ref CaptionSlot slot)
        {
            if (slot.Root == null || slot.Group == null)
                return;

            Vector2 direction = ResolveScreenDirection(slot.WorldPosition);
            float radius = Mathf.Lerp(RadiusMin, RadiusMax, slot.Intensity);
            slot.Root.anchoredPosition = direction * radius + new Vector2(0f, VerticalBias);

            float remaining01 = 1f - Mathf.Clamp01(slot.Age / Mathf.Max(MinDuration, slot.Duration));
            slot.Group.alpha = Mathf.Sin(remaining01 * Mathf.PI * 0.5f);
        }

        private Vector2 ResolveScreenDirection(Vector3 worldPosition)
        {
            if (_viewCamera == null)
                return Vector2.up;

            Transform cameraTransform = _viewCamera.transform;
            Vector3 local = cameraTransform.InverseTransformPoint(worldPosition);
            Vector2 planar = new Vector2(local.x, local.y);
            if (planar.sqrMagnitude < 0.0001f)
                planar = new Vector2(local.x >= 0f ? BehindFlipBias : -BehindFlipBias, 1f);

            if (local.z < 0f)
                planar = -planar;

            float magnitude = planar.magnitude;
            if (magnitude <= 0.0001f)
                return Vector2.up;

            return planar / magnitude;
        }

        private void HideAllSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Active)
                    continue;

                CaptionSlot slot = _slots[i];
                slot.Active = false;
                ApplySlotHidden(ref slot);
                _slots[i] = slot;
            }
        }

        private static void ApplySlotHidden(ref CaptionSlot slot)
        {
            if (slot.Group != null)
            {
                slot.Group.alpha = 0f;
                slot.Group.blocksRaycasts = false;
                slot.Group.interactable = false;
            }
        }

        private void RegisterToTickManager()
        {
            if (_tickRegistered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return (SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.GetComponent<Canvas>() : null);
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            return UiChildSpanUtility.FindExistingChild(parent, childName);
        }

        private static void ClearChildren(Transform parent)
        {
            UiChildSpanUtility.DestroyChildren(parent);
        }
    }
}
