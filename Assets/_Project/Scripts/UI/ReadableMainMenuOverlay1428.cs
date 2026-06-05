using System;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton.UI.MainMenu
{
    /// <summary>
    /// Explicitly authored world-space readability layer for the main menu.
    /// The authored diegetic menu remains the state owner; this layer only mirrors visible panels
    /// and never owns pointer or command input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReadableMainMenuOverlay1428 : MonoBehaviour
    {
        private const int MainMode = 0;
        private const int SettingsMode = 1;
        private const int LoadMode = 2;
        private const int LoadingMode = 3;
        private const int SlotCount = SaveEvents.ManualSlotCount;
        private const int ReferenceWidthPixels = 1280;
        private const int ReferenceHeightPixels = 720;
        private const int OverlayLabelBufferLength = 192;
        private const int OverlaySlotTitleBufferLength = 32;
        private const int OverlaySlotDetailBufferLength = 192;
        private const int OverlaySlotCombinedBufferLength = OverlaySlotTitleBufferLength + OverlaySlotDetailBufferLength + 1;
        private const float OverlayDistanceMeters = 0.55f;
        private const string RootName = "H8_MENU_READABLE_RUNTIME_1428";
        private const string AuthoredRootName = "H8_MENU_READABLE_OVERLAY_1428";
        private const string MainPanelObjectName = "H8_READABLE_Main";
        private const string SettingsPanelObjectName = "H8_READABLE_Settings";
        private const string LoadPanelObjectName = "H8_READABLE_LoadArchive";
        private const string LoadingPanelObjectName = "H8_READABLE_Loading";
        private const string Slot0ObjectName = "Slot0";
        private const string Slot1ObjectName = "Slot1";
        private const string Slot2ObjectName = "Slot2";
        private const int OverlaySortingOrder = 32760;

        private static readonly Color Backdrop = new Color(0.004f, 0.014f, 0.016f, 1f);
        private static readonly Color Panel = new Color(0.010f, 0.036f, 0.040f, 1f);
        private static readonly Color CommandPlate = new Color(0.020f, 0.150f, 0.165f, 0.92f);
        private static readonly Color CommandPlateDisabled = new Color(0.030f, 0.045f, 0.048f, 0.62f);
        private static readonly Color Cyan = new Color(0.18f, 0.96f, 0.96f, 1f);
        private static readonly Color White = new Color(0.86f, 0.98f, 0.98f, 1f);
        private static readonly Color Muted = new Color(0.48f, 0.70f, 0.70f, 1f);
        private static readonly Color Amber = new Color(1f, 0.66f, 0.20f, 1f);

        private readonly Image[] _slotImages = new Image[SlotCount]; // COLD ALLOC: fixed load-slot visual cache - owner: ReadableMainMenuOverlay1428.
        private readonly TMP_Text[] _slotLabels = new TMP_Text[SlotCount]; // COLD ALLOC: fixed load-slot text cache - owner: ReadableMainMenuOverlay1428.
        private readonly char[] _labelBuffer = new char[OverlayLabelBufferLength]; // COLD ALLOC: changed-label staging buffer.
        private readonly char[] _slotTitleBuffer = new char[OverlaySlotTitleBufferLength]; // COLD ALLOC: changed-slot title staging buffer.
        private readonly char[] _slotDetailBuffer = new char[OverlaySlotDetailBufferLength]; // COLD ALLOC: changed-slot detail staging buffer.
        private readonly char[] _slotCombinedBuffer = new char[OverlaySlotCombinedBufferLength]; // COLD ALLOC: changed-slot combined staging buffer.

        private MainMenuController _controller;
        private SettingsPanel _settingsPanel;
        private CanvasGroup _mainSourceGroup;
        private CanvasGroup _settingsSourceGroup;
        private CanvasGroup _loadSourceGroup;
        private CanvasGroup _loadingSourceGroup;
        private GameObject _rootObject;
        private CanvasGroup _rootGroup;
        private CanvasGroup _mainPanel;
        private CanvasGroup _settingsPanelGroup;
        private CanvasGroup _loadPanel;
        private CanvasGroup _loadingPanel;
        private TMP_Text _settingsQualityLabel;
        private TMP_Text _settingsPresetLabel;
        private TMP_Text _settingsStyleLabel;
        private TMP_Text _settingsConceptLabel;
        private TMP_Text _settingsTextScaleLabel;
        private TMP_Text _settingsMotionLabel;
        private TMP_FontAsset _resolvedFont;
        private Camera _cachedOverlayCamera;
        private int _lastMode = -1;
        private int _lastQuality = -999;
        private int _lastPreset = -999;
        private int _lastStyle = -999;
        private int _lastConcept = -999;
        private int _lastTextScale = -999;
        private int _lastMotionScale = -999;
        private int _slotRefreshFrame = -1;
        private bool _controlsBound;

        public void Configure(
            MainMenuController controller,
            CanvasGroup mainGroup,
            CanvasGroup settingsGroup,
            CanvasGroup loadGroup,
            CanvasGroup loadingGroup,
            SettingsPanel panel)
        {
            _controller = controller;
            _mainSourceGroup = mainGroup;
            _settingsSourceGroup = settingsGroup;
            _loadSourceGroup = loadGroup;
            _loadingSourceGroup = loadingGroup;
            _settingsPanel = panel;
            _lastMode = -1;
            _slotRefreshFrame = -1;
            InvalidateSettingsLabels();

            if (_rootObject == null)
                BuildCold();
            else
                RebindRootControlsCold();

            SyncLateFrame();
        }

        public void ForceRefresh()
        {
            InvalidateContent();
            RebindRootControlsCold();
            SyncLateFrame();
        }

        public void InvalidateContent()
        {
            _lastMode = -1;
            _slotRefreshFrame = -1;
            InvalidateSettingsLabels();
        }

        private void OnDestroy()
        {
            if (_rootObject == null)
                return;

            if (_rootObject != gameObject)
            {
                if (Application.isPlaying)
                    Destroy(_rootObject);
                else
                    DestroyImmediate(_rootObject);
            }

            _rootObject = null;
            _controlsBound = false;
        }

        public void SyncLateFrame()
        {
            if (_rootObject == null)
                return;

            if (!_controlsBound)
                RebindRootControlsCold();

            if (_rootGroup != null && _rootGroup.alpha < 0.999f)
            {
                _rootGroup.alpha = 1f;
                _rootGroup.interactable = false;
                _rootGroup.blocksRaycasts = false;
            }

            int mode = ResolveMode();
            if (_lastMode != mode)
            {
                _lastMode = mode;
                SetPanelVisible(_mainPanel, mode == MainMode);
                SetPanelVisible(_settingsPanelGroup, mode == SettingsMode);
                SetPanelVisible(_loadPanel, mode == LoadMode);
                SetPanelVisible(_loadingPanel, mode == LoadingMode);
                _slotRefreshFrame = -1;
            }

            if (mode == SettingsMode)
                RefreshSettingsLabelsIfNeeded();
            else if (mode == LoadMode)
                RefreshSlotLabelsIfNeeded();
        }

        private void BuildCold()
        {
            _resolvedFont = ResolveFontCold();
            bool usesAuthoredRoot = TryBindAuthoredRootCold(out Canvas canvas, out CanvasScaler scaler);
            if (!usesAuthoredRoot)
            {
                _rootObject = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup)); // COLD ALLOC: overlay root fallback.
                Scene scene = gameObject.scene;
                if (scene.IsValid())
                    SceneManager.MoveGameObjectToScene(_rootObject, scene);

                _rootObject.TryGetComponent(out canvas);
                _rootObject.TryGetComponent(out scaler);
            }

            RectTransform rootRect = (RectTransform)_rootObject.transform;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(ReferenceWidthPixels, ReferenceHeightPixels);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.localScale = Vector3.one;

            ConfigureWorldSpaceCanvas(canvas);
            ConfigureCanvasScaler(scaler);

            if (_rootGroup == null && !_rootObject.TryGetComponent(out _rootGroup))
                _rootGroup = _rootObject.AddComponent<CanvasGroup>(); // COLD ALLOC: missing authored overlay group repair.
            _rootGroup.alpha = 1f;
            _rootGroup.interactable = false;
            _rootGroup.blocksRaycasts = false;

            if (usesAuthoredRoot)
                DisableLegacyAuthoredChildrenCold(_rootObject.transform);

            if (!_rootObject.TryGetComponent(out Image background))
                background = _rootObject.AddComponent<Image>(); // COLD ALLOC: readable overlay backing plate.
            background.color = Backdrop;
            background.raycastTarget = false;

            _mainPanel = CreatePanel(MainPanelObjectName, new Vector2(0.50f, 0.50f), new Vector2(610f, 515f), new Vector2(0f, -4f), true);
            BuildMainPanel(_mainPanel.transform);
            _settingsPanelGroup = CreatePanel(SettingsPanelObjectName, new Vector2(0.50f, 0.50f), new Vector2(990f, 585f), new Vector2(0f, -4f), false);
            BuildSettingsPanel(_settingsPanelGroup.transform);
            _loadPanel = CreatePanel(LoadPanelObjectName, new Vector2(0.50f, 0.50f), new Vector2(820f, 520f), new Vector2(0f, -4f), false);
            BuildLoadPanel(_loadPanel.transform);
            _loadingPanel = CreatePanel(LoadingPanelObjectName, new Vector2(0.50f, 0.50f), new Vector2(620f, 320f), new Vector2(0f, -4f), false);
            BuildLoadingPanel(_loadingPanel.transform);
            _controlsBound = true;
        }

        private bool TryBindAuthoredRootCold(out Canvas canvas, out CanvasScaler scaler)
        {
            canvas = null;
            scaler = null;
            if (!TryGetComponent(out canvas) || !(transform is RectTransform))
                return false;

            _rootObject = gameObject;
            if (!_rootObject.activeSelf)
                _rootObject.SetActive(true);

            _rootObject.name = AuthoredRootName;
            TryGetComponent(out scaler);
            TryGetComponent(out _rootGroup);
            return true;
        }

        private static void ConfigureCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null)
                return;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referenceResolution = new Vector2(ReferenceWidthPixels, ReferenceHeightPixels);
            scaler.dynamicPixelsPerUnit = 1f;
        }

        private static void DisableLegacyAuthoredChildrenCold(Transform root)
        {
            if (root == null)
                return;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null)
                    child.gameObject.SetActive(false);
            }
        }

        private CanvasGroup CreatePanel(string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, bool visible)
        {
            RectTransform rect = CreateRect(name, _rootObject.transform, anchor, size, anchoredPosition);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Panel;
            image.raycastTarget = false;

            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            SetPanelVisible(group, visible);

            CreateLine(rect, "TopLine", new Vector2(0.5f, 1f), new Vector2(size.x - 72f, 2f), new Vector2(0f, -28f), Cyan);
            CreateLine(rect, "BottomLine", new Vector2(0.5f, 0f), new Vector2(size.x - 72f, 2f), new Vector2(0f, 28f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f));
            return group;
        }

        private void BuildMainPanel(Transform parent)
        {
            CreateText(parent, "Title", "HECTON-8", new Vector2(0.5f, 1f), new Vector2(520f, 78f), new Vector2(0f, -82f), 52, TextAnchor.MiddleCenter, White);
            CreateText(parent, "Subtitle", "DEEP SEA NOIR / RUNTIME ENTRY", new Vector2(0.5f, 1f), new Vector2(520f, 34f), new Vector2(0f, -142f), 18, TextAnchor.MiddleCenter, Muted);
            CreateText(parent, "Status", "BOOTSTRAP VERIFIED / MENU ROUTES ACTIVE", new Vector2(0.5f, 1f), new Vector2(520f, 30f), new Vector2(0f, -188f), 15, TextAnchor.MiddleCenter, Amber);

            CreateCommandPlate(parent, "BtnNewDive", "NEW DIVE", new Vector2(0.5f, 1f), new Vector2(340f, 46f), new Vector2(0f, -252f));
            CreateCommandPlate(parent, "BtnLoad", "LOAD ARCHIVE", new Vector2(0.5f, 1f), new Vector2(340f, 46f), new Vector2(0f, -306f));
            CreateCommandPlate(parent, "BtnSettings", "SETTINGS", new Vector2(0.5f, 1f), new Vector2(340f, 46f), new Vector2(0f, -360f));
            CreateCommandPlate(parent, "BtnOrbit", "ORBIT DROP TEST", new Vector2(0.5f, 1f), new Vector2(340f, 42f), new Vector2(0f, -414f));
            CreateCommandPlate(parent, "BtnQuit", "QUIT", new Vector2(0.5f, 1f), new Vector2(220f, 38f), new Vector2(0f, -468f));
        }

        private void BuildSettingsPanel(Transform parent)
        {
            CreateText(parent, "Title", "SETTINGS / ADAPTIVE RUNTIME", new Vector2(0.5f, 1f), new Vector2(820f, 52f), new Vector2(0f, -58f), 30, TextAnchor.MiddleCenter, White);
            CreateText(parent, "Subtitle", "CONTINUOUS QUALITY WEIGHT / PRESENTATION VARIANTS", new Vector2(0.5f, 1f), new Vector2(820f, 28f), new Vector2(0f, -98f), 14, TextAnchor.MiddleCenter, Muted);

            _settingsQualityLabel = CreateText(parent, "QualityValue", "QUALITY --", new Vector2(0.5f, 1f), new Vector2(700f, 36f), new Vector2(0f, -146f), 20, TextAnchor.MiddleCenter, Amber);
            _settingsPresetLabel = CreateText(parent, "PresetValue", "PRESET --", new Vector2(0.5f, 1f), new Vector2(700f, 28f), new Vector2(0f, -178f), 14, TextAnchor.MiddleCenter, Muted);

            CreateCommandPlate(parent, "QualityMinus", "QUALITY -", new Vector2(0.5f, 1f), new Vector2(150f, 38f), new Vector2(-88f, -230f));
            CreateCommandPlate(parent, "QualityPlus", "QUALITY +", new Vector2(0.5f, 1f), new Vector2(150f, 38f), new Vector2(88f, -230f));
            CreateCommandPlate(parent, "PresetLow", "LOW", new Vector2(0.5f, 1f), new Vector2(118f, 34f), new Vector2(-220f, -282f));
            CreateCommandPlate(parent, "PresetMedium", "MEDIUM", new Vector2(0.5f, 1f), new Vector2(118f, 34f), new Vector2(-74f, -282f));
            CreateCommandPlate(parent, "PresetHigh", "HIGH", new Vector2(0.5f, 1f), new Vector2(118f, 34f), new Vector2(74f, -282f));
            CreateCommandPlate(parent, "PresetUltra", "ULTRA", new Vector2(0.5f, 1f), new Vector2(118f, 34f), new Vector2(220f, -282f));

            _settingsStyleLabel = CreateText(parent, "StyleValue", "STYLE --", new Vector2(0.5f, 1f), new Vector2(720f, 30f), new Vector2(0f, -338f), 15, TextAnchor.MiddleCenter, White);
            CreateCommandPlate(parent, "StyleMinus", "STYLE -", new Vector2(0.5f, 1f), new Vector2(120f, 34f), new Vector2(-330f, -338f));
            CreateCommandPlate(parent, "StylePlus", "STYLE +", new Vector2(0.5f, 1f), new Vector2(120f, 34f), new Vector2(330f, -338f));

            _settingsConceptLabel = CreateText(parent, "ConceptValue", "CONCEPT --", new Vector2(0.5f, 1f), new Vector2(720f, 30f), new Vector2(0f, -386f), 15, TextAnchor.MiddleCenter, White);
            CreateCommandPlate(parent, "ConceptMinus", "CONCEPT -", new Vector2(0.5f, 1f), new Vector2(132f, 34f), new Vector2(-330f, -386f));
            CreateCommandPlate(parent, "ConceptPlus", "CONCEPT +", new Vector2(0.5f, 1f), new Vector2(132f, 34f), new Vector2(330f, -386f));

            _settingsTextScaleLabel = CreateText(parent, "TextScaleValue", "TEXT SCALE --", new Vector2(0.5f, 1f), new Vector2(360f, 28f), new Vector2(-210f, -436f), 14, TextAnchor.MiddleCenter, Muted);
            _settingsMotionLabel = CreateText(parent, "MotionValue", "UI MOTION --", new Vector2(0.5f, 1f), new Vector2(360f, 28f), new Vector2(210f, -436f), 14, TextAnchor.MiddleCenter, Muted);

            CreateCommandPlate(parent, "Reset", "RESET", new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(-250f, 62f));
            CreateCommandPlate(parent, "Apply", "APPLY", new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(0f, 62f));
            CreateCommandPlate(parent, "Back", "BACK", new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(250f, 62f));
        }

        private void BuildLoadPanel(Transform parent)
        {
            CreateText(parent, "Title", "LOAD ARCHIVE", new Vector2(0.5f, 1f), new Vector2(650f, 56f), new Vector2(0f, -62f), 34, TextAnchor.MiddleCenter, White);
            CreateText(parent, "Subtitle", "VALIDATED SAVE SLOTS / FAIL-CLOSED LOAD", new Vector2(0.5f, 1f), new Vector2(650f, 28f), new Vector2(0f, -106f), 14, TextAnchor.MiddleCenter, Muted);

            _slotImages[0] = CreateCommandPlate(parent, Slot0ObjectName, "SLOT 1\nSCANNING", new Vector2(0.5f, 1f), new Vector2(610f, 70f), new Vector2(0f, -176f));
            _slotLabels[0] = ResolveChildTextCold(_slotImages[0] != null ? _slotImages[0].transform : null);
            _slotImages[1] = CreateCommandPlate(parent, Slot1ObjectName, "SLOT 2\nSCANNING", new Vector2(0.5f, 1f), new Vector2(610f, 70f), new Vector2(0f, -260f));
            _slotLabels[1] = ResolveChildTextCold(_slotImages[1] != null ? _slotImages[1].transform : null);
            _slotImages[2] = CreateCommandPlate(parent, Slot2ObjectName, "SLOT 3\nSCANNING", new Vector2(0.5f, 1f), new Vector2(610f, 70f), new Vector2(0f, -344f));
            _slotLabels[2] = ResolveChildTextCold(_slotImages[2] != null ? _slotImages[2].transform : null);

            CreateCommandPlate(parent, "Back", "BACK", new Vector2(0.5f, 0f), new Vector2(170f, 40f), new Vector2(0f, 62f));
        }

        private void BuildLoadingPanel(Transform parent)
        {
            CreateText(parent, "Title", "LOADING", new Vector2(0.5f, 1f), new Vector2(520f, 60f), new Vector2(0f, -86f), 34, TextAnchor.MiddleCenter, White);
            CreateText(parent, "Subtitle", "SCENE TRANSFER IN PROGRESS", new Vector2(0.5f, 1f), new Vector2(520f, 38f), new Vector2(0f, -142f), 16, TextAnchor.MiddleCenter, Amber);
            CreateText(parent, "Body", "NO INPUT REQUIRED", new Vector2(0.5f, 1f), new Vector2(520f, 34f), new Vector2(0f, -204f), 14, TextAnchor.MiddleCenter, Muted);
        }

        private int ResolveMode()
        {
            if (IsVisible(_loadingSourceGroup))
                return LoadingMode;
            if (IsVisible(_settingsSourceGroup))
                return SettingsMode;
            if (IsVisible(_loadSourceGroup))
                return LoadMode;
            return MainMode;
        }

        private static bool IsVisible(CanvasGroup group)
        {
            return group != null && group.gameObject.activeInHierarchy && group.alpha > 0.001f;
        }

        private static void SetPanelVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private void RefreshSettingsLabelsIfNeeded()
        {
            SettingsPanel panel = _settingsPanel;
            if (panel == null)
                return;

            int quality = panel.ReadableCachedQualityLevel;
            int preset = panel.ReadableCachedGraphicsPreset;
            int style = panel.ReadableCachedMenuVisualStyleIndex;
            int concept = panel.ReadableCachedMenuVisualConceptIndex;
            int textScale = Mathf.RoundToInt(panel.ReadableCachedTextScale * 100f);
            int motionScale = Mathf.RoundToInt(panel.ReadableCachedUiMotionScale * 100f);

            if (_lastQuality != quality && _settingsQualityLabel != null)
            {
                _lastQuality = quality;
                int length = FormatQualityLabel(quality);
                SetTextFromBuffer(_settingsQualityLabel, _labelBuffer, length);
            }

            if (_lastPreset != preset && _settingsPresetLabel != null)
            {
                _lastPreset = preset;
                int length = FormatPresetLabel(preset);
                SetTextFromBuffer(_settingsPresetLabel, _labelBuffer, length);
            }

            if (_lastStyle != style && _settingsStyleLabel != null)
            {
                _lastStyle = style;
                int length = FormatStyleLabel(style);
                SetTextFromBuffer(_settingsStyleLabel, _labelBuffer, length);
            }

            if (_lastConcept != concept && _settingsConceptLabel != null)
            {
                _lastConcept = concept;
                int length = FormatConceptLabel(concept);
                SetTextFromBuffer(_settingsConceptLabel, _labelBuffer, length);
            }

            if (_lastTextScale != textScale && _settingsTextScaleLabel != null)
            {
                _lastTextScale = textScale;
                int length = FormatPercentLabel("TEXT SCALE ".AsSpan(), textScale);
                SetTextFromBuffer(_settingsTextScaleLabel, _labelBuffer, length);
            }

            if (_lastMotionScale != motionScale && _settingsMotionLabel != null)
            {
                _lastMotionScale = motionScale;
                int length = FormatPercentLabel("UI MOTION ".AsSpan(), motionScale);
                SetTextFromBuffer(_settingsMotionLabel, _labelBuffer, length);
            }
        }

        private void RefreshSlotLabelsIfNeeded()
        {
            if (_slotRefreshFrame >= 0)
                return;

            if (NeedsLoadSlotControlRebind())
                RebindLoadSlotControlsCold();
            _slotRefreshFrame = Time.frameCount;
            for (int i = 0; i < SlotCount; i++)
            {
                Span<char> titleBuffer = _slotTitleBuffer.AsSpan();
                Span<char> detailBuffer = _slotDetailBuffer.AsSpan();
                if (_controller == null ||
                    !_controller.TryGetReadableSaveSlotState(
                        i,
                        titleBuffer,
                        out int titleLength,
                        detailBuffer,
                        out int detailLength,
                        out bool canLoad))
                {
                    titleLength = FormatFallbackSlotTitle(i, titleBuffer);
                    detailLength = FormatLiteral("SAVE SYSTEM OFFLINE".AsSpan(), detailBuffer);
                    canLoad = false;
                }

                if (_slotLabels[i] != null)
                {
                    int combinedLength = FormatSlotLabel(
                        titleBuffer.Slice(0, titleLength),
                        detailBuffer.Slice(0, detailLength));
                    SetTextFromBuffer(_slotLabels[i], _slotCombinedBuffer, combinedLength);
                }

                if (_slotImages[i] != null)
                    _slotImages[i].color = canLoad ? CommandPlate : CommandPlateDisabled;
            }
        }

        private void RebindRootControlsCold()
        {
            if (_rootObject == null)
                return;

            Canvas canvas;
            if (_rootObject.TryGetComponent(out canvas))
                ConfigureWorldSpaceCanvas(canvas);

            if (_rootGroup == null)
                _rootObject.TryGetComponent(out _rootGroup);

            Image background;
            if (_rootObject.TryGetComponent(out background))
                background.color = Backdrop;

            Transform root = _rootObject.transform;
            RebindPanelCold(root, MainPanelObjectName, ref _mainPanel);
            RebindPanelCold(root, SettingsPanelObjectName, ref _settingsPanelGroup);
            RebindPanelCold(root, LoadPanelObjectName, ref _loadPanel);
            RebindPanelCold(root, LoadingPanelObjectName, ref _loadingPanel);
            ApplyPanelColorCold(_mainPanel);
            ApplyPanelColorCold(_settingsPanelGroup);
            ApplyPanelColorCold(_loadPanel);
            ApplyPanelColorCold(_loadingPanel);
            RebindSettingsLabelsCold();
            RebindLoadSlotControlsCold();
            _controlsBound = true;
        }

        private static void ApplyPanelColorCold(CanvasGroup group)
        {
            if (group == null)
                return;

            Image image;
            if (group.TryGetComponent(out image))
                image.color = Panel;
        }

        private static void RebindPanelCold(Transform root, string name, ref CanvasGroup group)
        {
            if (group != null || root == null)
                return;

            Transform panel = root.Find(name);
            if (panel != null)
                panel.TryGetComponent(out group);
        }

        private void RebindSettingsLabelsCold()
        {
            if (_settingsPanelGroup == null)
                return;

            Transform root = _settingsPanelGroup.transform;
            RebindTextCold(root, "QualityValue", ref _settingsQualityLabel);
            RebindTextCold(root, "PresetValue", ref _settingsPresetLabel);
            RebindTextCold(root, "StyleValue", ref _settingsStyleLabel);
            RebindTextCold(root, "ConceptValue", ref _settingsConceptLabel);
            RebindTextCold(root, "TextScaleValue", ref _settingsTextScaleLabel);
            RebindTextCold(root, "MotionValue", ref _settingsMotionLabel);
        }

        private static void RebindTextCold(Transform root, string name, ref TMP_Text text)
        {
            if (text != null || root == null)
                return;

            Transform textTransform = root.Find(name);
            if (textTransform != null)
                textTransform.TryGetComponent(out text);
        }

        private void ConfigureWorldSpaceCanvas(Canvas canvas)
        {
            if (canvas == null)
                return;

            Camera camera = ResolveOverlayCameraCold();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = OverlaySortingOrder;
            canvas.pixelPerfect = false;

            if (!(canvas.transform is RectTransform rootRect))
                return;

            rootRect.sizeDelta = new Vector2(ReferenceWidthPixels, ReferenceHeightPixels);
            if (camera == null)
            {
                rootRect.localScale = Vector3.one * 0.001f;
                return;
            }

            Transform cameraTransform = camera.transform;
            if (rootRect.parent != cameraTransform)
                rootRect.SetParent(cameraTransform, false);

            float distance = Mathf.Max(0.05f, OverlayDistanceMeters);
            float heightMeters = camera.orthographic
                ? Mathf.Max(0.01f, camera.orthographicSize * 2f)
                : 2f * distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float scale = Mathf.Max(0.0001f, heightMeters / ReferenceHeightPixels * 0.92f);
            rootRect.localPosition = new Vector3(0f, 0f, distance);
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = new Vector3(scale, scale, scale);
        }

        private Camera ResolveOverlayCameraCold()
        {
            if (_controller != null &&
                _controller.TryGetReadableOverlayCamera(out Camera camera) &&
                camera != null &&
                camera.isActiveAndEnabled)
            {
                _cachedOverlayCamera = camera;
                return camera;
            }

            return _cachedOverlayCamera != null && _cachedOverlayCamera.isActiveAndEnabled
                ? _cachedOverlayCamera
                : null;
        }

        private void RebindLoadSlotControlsCold()
        {
            if (_loadPanel == null)
                return;

            Transform root = _loadPanel.transform;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slotImages[i] == null)
                {
                    Transform slot = root.Find(ResolveSlotObjectName(i));
                    if (slot != null)
                        slot.TryGetComponent(out _slotImages[i]);
                }

                if (_slotLabels[i] == null)
                    _slotLabels[i] = ResolveChildTextCold(_slotImages[i] != null ? _slotImages[i].transform : null);
            }
        }

        private bool NeedsLoadSlotControlRebind()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slotImages[i] == null || _slotLabels[i] == null)
                    return true;
            }

            return false;
        }

        private static string ResolveSlotObjectName(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0: return Slot0ObjectName;
                case 1: return Slot1ObjectName;
                case 2: return Slot2ObjectName;
                default: return string.Empty;
            }
        }

        private int FormatQualityLabel(int quality)
        {
            Span<char> buffer = _labelBuffer.AsSpan();
            int length = 0;
            ZeroGCFormatter.AppendToSpan("QUALITY WEIGHT ".AsSpan(), buffer, ref length);
            ZeroGCFormatter.AppendInt(quality, buffer, ref length);
            ZeroGCFormatter.AppendToSpan(" / ".AsSpan(), buffer, ref length);
            ZeroGCFormatter.AppendInt(SettingsManager.MaxContinuousQualityLevel, buffer, ref length);
            ZeroGCFormatter.AppendToSpan("  (".AsSpan(), buffer, ref length);
            ZeroGCFormatter.AppendToSpan(ResolveQualityName(quality).AsSpan(), buffer, ref length);
            ZeroGCFormatter.AppendChar(')', buffer, ref length);
            return length;
        }

        private int FormatPresetLabel(int preset)
        {
            Span<char> buffer = _labelBuffer.AsSpan();
            int length = 0;
            ZeroGCFormatter.AppendToSpan("GRAPHICS PRESET ".AsSpan(), buffer, ref length);
            ZeroGCFormatter.AppendToSpan(ResolvePresetName(preset).AsSpan(), buffer, ref length);
            return length;
        }

        private int FormatStyleLabel(int style)
        {
            Span<char> buffer = _labelBuffer.AsSpan();
            int length = 0;
            ZeroGCFormatter.AppendToSpan("STYLE ".AsSpan(), buffer, ref length);
            ZeroGCFormatter.AppendInt(style + 1, buffer, ref length);
            ZeroGCFormatter.AppendChar('/', buffer, ref length);
            ZeroGCFormatter.AppendInt(MenuVisualStyleCatalog.StyleCount, buffer, ref length);
            ZeroGCFormatter.AppendToSpan("  ".AsSpan(), buffer, ref length);
            ZeroGCFormatter.AppendToSpan(MenuVisualStyleCatalog.GetDisplayName(MenuVisualStyleCatalog.FromIndex(style)), buffer, ref length);
            return length;
        }

        private int FormatConceptLabel(int concept)
        {
            Span<char> buffer = _labelBuffer.AsSpan();
            int length = 0;
            ZeroGCFormatter.AppendToSpan("CONCEPT ".AsSpan(), buffer, ref length);
            ZeroGCFormatter.AppendInt(concept + 1, buffer, ref length);
            ZeroGCFormatter.AppendChar('/', buffer, ref length);
            ZeroGCFormatter.AppendInt(MenuVisualConceptCatalog.ConceptCount, buffer, ref length);
            ZeroGCFormatter.AppendToSpan("  ".AsSpan(), buffer, ref length);
            ZeroGCFormatter.AppendToSpan(MenuVisualConceptCatalog.GetDisplayName(MenuVisualConceptCatalog.FromIndex(concept)), buffer, ref length);
            return length;
        }

        private int FormatPercentLabel(ReadOnlySpan<char> prefix, int percent)
        {
            Span<char> buffer = _labelBuffer.AsSpan();
            int length = 0;
            ZeroGCFormatter.AppendToSpan(prefix, buffer, ref length);
            ZeroGCFormatter.AppendInt(percent, buffer, ref length);
            ZeroGCFormatter.AppendChar('%', buffer, ref length);
            return length;
        }

        private static int FormatFallbackSlotTitle(int slotIndex, Span<char> destination)
        {
            int length = 0;
            ZeroGCFormatter.AppendToSpan("SLOT ".AsSpan(), destination, ref length);
            ZeroGCFormatter.AppendInt(slotIndex + 1, destination, ref length);
            return length;
        }

        private static int FormatLiteral(ReadOnlySpan<char> value, Span<char> destination)
        {
            int length = 0;
            ZeroGCFormatter.AppendToSpanTruncated(value, destination, ref length, out _);
            return length;
        }

        private int FormatSlotLabel(ReadOnlySpan<char> title, ReadOnlySpan<char> detail)
        {
            Span<char> buffer = _slotCombinedBuffer.AsSpan();
            int length = 0;
            ZeroGCFormatter.AppendToSpanTruncated(title, buffer, ref length, out _);
            ZeroGCFormatter.AppendChar('\n', buffer, ref length);
            ZeroGCFormatter.AppendToSpanTruncated(detail, buffer, ref length, out _);
            return length;
        }

        private static void SetTextFromBuffer(TMP_Text target, char[] buffer, int length)
        {
            if (target == null || buffer == null)
                return;

            int safeLength = Mathf.Clamp(length, 0, buffer.Length);
            target.SetCharArray(buffer, 0, safeLength);
        }

        private static string ResolveQualityName(int quality)
        {
            switch (quality)
            {
                case 0: return "SURVIVAL";
                case 1: return "LOW";
                case 2: return "LEAN";
                case 3: return "MEDIUM";
                case 4: return "HIGH";
                case 5: return "ULTRA";
                case 6: return "OVERKILL";
                default: return "--";
            }
        }

        private static string ResolvePresetName(int preset)
        {
            switch (preset)
            {
                case 0: return "LOW";
                case 1: return "MEDIUM";
                case 2: return "HIGH";
                case 3: return "ULTRA";
                default: return "--";
            }
        }

        private void InvalidateSettingsLabels()
        {
            _lastQuality = -999;
            _lastPreset = -999;
            _lastStyle = -999;
            _lastConcept = -999;
            _lastTextScale = -999;
            _lastMotionScale = -999;
        }

        private RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 anchoredPosition)
        {
            GameObject go = new GameObject(name, typeof(RectTransform)); // COLD ALLOC: UI rect.
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            rect.localScale = Vector3.one;
            return rect;
        }

        private TMP_Text CreateText(
            Transform parent,
            string name,
            string value,
            Vector2 anchor,
            Vector2 size,
            Vector2 anchoredPosition,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchor, size, anchoredPosition);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_resolvedFont != null)
                text.font = _resolvedFont;

            text.text = value;
            text.fontSize = fontSize;
            text.alignment = ResolveTextAlignment(alignment);
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(9, fontSize - 8);
            text.fontSizeMax = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private Image CreateCommandPlate(
            Transform parent,
            string name,
            string label,
            Vector2 anchor,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            RectTransform rect = CreateRect(name, parent, anchor, size, anchoredPosition);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = CommandPlate;
            image.raycastTarget = false;

            CreateText(rect, "Label", label, new Vector2(0.5f, 0.5f), new Vector2(size.x - 20f, size.y - 8f), Vector2.zero, 17, TextAnchor.MiddleCenter, White);
            return image;
        }

        private void CreateLine(RectTransform parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchor, size, anchoredPosition);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static TMP_FontAsset ResolveFontCold()
        {
            TMP_Text[] existingTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include); // COLD ALLOC: font discovery from existing menu labels.
            for (int i = 0; i < existingTexts.Length; i++)
            {
                TMP_Text text = existingTexts[i];
                if (text != null && text.font != null)
                    return text.font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static TMP_Text ResolveChildTextCold(Transform transform)
        {
            if (transform == null)
                return null;

            if (transform.TryGetComponent(out TMP_Text text))
                return text;

            for (int i = 0; i < transform.childCount; i++)
            {
                TMP_Text childText = ResolveChildTextCold(transform.GetChild(i));
                if (childText != null)
                    return childText;
            }

            return null;
        }

        private static TextAlignmentOptions ResolveTextAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }
    }
}
