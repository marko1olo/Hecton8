using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton.UI.MainMenu
{
    /// <summary>
    /// Cold-created screen-space readability layer for the main menu.
    /// The authored diegetic menu remains the state owner; this layer only mirrors visible panels
    /// and forwards button commands to existing menu/settings routes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReadableMainMenuOverlay1428 : MonoBehaviour
    {
        private const int MainMode = 0;
        private const int SettingsMode = 1;
        private const int LoadMode = 2;
        private const int LoadingMode = 3;
        private const int SlotCount = SaveEvents.ManualSlotCount;
        private const string RootName = "H8_MENU_READABLE_RUNTIME_1428";
        private const int OverlaySortingOrder = 32760;

        private static readonly Color Backdrop = new Color(0.004f, 0.014f, 0.016f, 1f);
        private static readonly Color Panel = new Color(0.010f, 0.036f, 0.040f, 1f);
        private static readonly Color Button = new Color(0.020f, 0.150f, 0.165f, 0.92f);
        private static readonly Color ButtonHover = new Color(0.035f, 0.245f, 0.265f, 0.96f);
        private static readonly Color ButtonDisabled = new Color(0.030f, 0.045f, 0.048f, 0.62f);
        private static readonly Color Cyan = new Color(0.18f, 0.96f, 0.96f, 1f);
        private static readonly Color White = new Color(0.86f, 0.98f, 0.98f, 1f);
        private static readonly Color Muted = new Color(0.48f, 0.70f, 0.70f, 1f);
        private static readonly Color Amber = new Color(1f, 0.66f, 0.20f, 1f);

        private readonly Button[] _slotButtons = new Button[SlotCount]; // COLD ALLOC: fixed load-slot button cache - owner: ReadableMainMenuOverlay1428.
        private readonly Text[] _slotLabels = new Text[SlotCount]; // COLD ALLOC: fixed load-slot text cache - owner: ReadableMainMenuOverlay1428.

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
        private Text _settingsQualityLabel;
        private Text _settingsPresetLabel;
        private Text _settingsStyleLabel;
        private Text _settingsConceptLabel;
        private Text _settingsTextScaleLabel;
        private Text _settingsMotionLabel;
        private Font _resolvedFont;
        private int _lastMode = -1;
        private int _lastQuality = -999;
        private int _lastPreset = -999;
        private int _lastStyle = -999;
        private int _lastConcept = -999;
        private int _lastTextScale = -999;
        private int _lastMotionScale = -999;
        private int _slotRefreshFrame = -1;

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
            _lastMode = -1;
            _slotRefreshFrame = -1;
            InvalidateSettingsLabels();
            RebindRootControlsCold();
            SyncLateFrame();
        }

        private void OnDestroy()
        {
            if (_rootObject == null)
                return;

            if (Application.isPlaying)
                Destroy(_rootObject);
            else
                DestroyImmediate(_rootObject);

            _rootObject = null;
        }

        public void SyncLateFrame()
        {
            if (_rootObject == null)
                return;

            RebindRootControlsCold();

            if (_rootGroup != null && _rootGroup.alpha < 0.999f)
            {
                _rootGroup.alpha = 1f;
                _rootGroup.interactable = true;
                _rootGroup.blocksRaycasts = true;
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
            _rootObject = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup)); // COLD ALLOC: overlay root.
            Scene scene = gameObject.scene;
            if (scene.IsValid())
                SceneManager.MoveGameObjectToScene(_rootObject, scene);

            RectTransform rootRect = (RectTransform)_rootObject.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;

            Canvas canvas = _rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = OverlaySortingOrder;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = _rootObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            _rootGroup = _rootObject.GetComponent<CanvasGroup>();
            _rootGroup.alpha = 1f;
            _rootGroup.interactable = true;
            _rootGroup.blocksRaycasts = true;

            Image background = _rootObject.AddComponent<Image>();
            background.color = Backdrop;
            background.raycastTarget = false;

            _mainPanel = CreatePanel("Main", new Vector2(0.50f, 0.50f), new Vector2(610f, 515f), new Vector2(0f, -4f), true);
            BuildMainPanel(_mainPanel.transform);
            _settingsPanelGroup = CreatePanel("Settings", new Vector2(0.50f, 0.50f), new Vector2(990f, 585f), new Vector2(0f, -4f), false);
            BuildSettingsPanel(_settingsPanelGroup.transform);
            _loadPanel = CreatePanel("LoadArchive", new Vector2(0.50f, 0.50f), new Vector2(820f, 520f), new Vector2(0f, -4f), false);
            BuildLoadPanel(_loadPanel.transform);
            _loadingPanel = CreatePanel("Loading", new Vector2(0.50f, 0.50f), new Vector2(620f, 320f), new Vector2(0f, -4f), false);
            BuildLoadingPanel(_loadingPanel.transform);
        }

        private CanvasGroup CreatePanel(string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, bool visible)
        {
            RectTransform rect = CreateRect("H8_READABLE_" + name, _rootObject.transform, anchor, size, anchoredPosition);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Panel;
            image.raycastTarget = true;

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

            CreateButton(parent, "BtnNewDive", "NEW DIVE", new Vector2(0.5f, 1f), new Vector2(340f, 46f), new Vector2(0f, -252f), OnNewDiveClicked);
            CreateButton(parent, "BtnLoad", "LOAD ARCHIVE", new Vector2(0.5f, 1f), new Vector2(340f, 46f), new Vector2(0f, -306f), OnLoadClicked);
            CreateButton(parent, "BtnSettings", "SETTINGS", new Vector2(0.5f, 1f), new Vector2(340f, 46f), new Vector2(0f, -360f), OnSettingsClicked);
            CreateButton(parent, "BtnOrbit", "ORBIT DROP TEST", new Vector2(0.5f, 1f), new Vector2(340f, 42f), new Vector2(0f, -414f), OnOrbitClicked);
            CreateButton(parent, "BtnQuit", "QUIT", new Vector2(0.5f, 1f), new Vector2(220f, 38f), new Vector2(0f, -468f), OnQuitClicked);
        }

        private void BuildSettingsPanel(Transform parent)
        {
            CreateText(parent, "Title", "SETTINGS / ADAPTIVE RUNTIME", new Vector2(0.5f, 1f), new Vector2(820f, 52f), new Vector2(0f, -58f), 30, TextAnchor.MiddleCenter, White);
            CreateText(parent, "Subtitle", "CONTINUOUS QUALITY WEIGHT / PRESENTATION VARIANTS", new Vector2(0.5f, 1f), new Vector2(820f, 28f), new Vector2(0f, -98f), 14, TextAnchor.MiddleCenter, Muted);

            _settingsQualityLabel = CreateText(parent, "QualityValue", "QUALITY --", new Vector2(0.5f, 1f), new Vector2(700f, 36f), new Vector2(0f, -146f), 20, TextAnchor.MiddleCenter, Amber);
            _settingsPresetLabel = CreateText(parent, "PresetValue", "PRESET --", new Vector2(0.5f, 1f), new Vector2(700f, 28f), new Vector2(0f, -178f), 14, TextAnchor.MiddleCenter, Muted);

            CreateButton(parent, "QualityMinus", "QUALITY -", new Vector2(0.5f, 1f), new Vector2(150f, 38f), new Vector2(-88f, -230f), OnQualityMinusClicked);
            CreateButton(parent, "QualityPlus", "QUALITY +", new Vector2(0.5f, 1f), new Vector2(150f, 38f), new Vector2(88f, -230f), OnQualityPlusClicked);
            CreateButton(parent, "PresetLow", "LOW", new Vector2(0.5f, 1f), new Vector2(118f, 34f), new Vector2(-220f, -282f), OnPresetLowClicked);
            CreateButton(parent, "PresetMedium", "MEDIUM", new Vector2(0.5f, 1f), new Vector2(118f, 34f), new Vector2(-74f, -282f), OnPresetMediumClicked);
            CreateButton(parent, "PresetHigh", "HIGH", new Vector2(0.5f, 1f), new Vector2(118f, 34f), new Vector2(74f, -282f), OnPresetHighClicked);
            CreateButton(parent, "PresetUltra", "ULTRA", new Vector2(0.5f, 1f), new Vector2(118f, 34f), new Vector2(220f, -282f), OnPresetUltraClicked);

            _settingsStyleLabel = CreateText(parent, "StyleValue", "STYLE --", new Vector2(0.5f, 1f), new Vector2(720f, 30f), new Vector2(0f, -338f), 15, TextAnchor.MiddleCenter, White);
            CreateButton(parent, "StyleMinus", "STYLE -", new Vector2(0.5f, 1f), new Vector2(120f, 34f), new Vector2(-330f, -338f), OnStyleMinusClicked);
            CreateButton(parent, "StylePlus", "STYLE +", new Vector2(0.5f, 1f), new Vector2(120f, 34f), new Vector2(330f, -338f), OnStylePlusClicked);

            _settingsConceptLabel = CreateText(parent, "ConceptValue", "CONCEPT --", new Vector2(0.5f, 1f), new Vector2(720f, 30f), new Vector2(0f, -386f), 15, TextAnchor.MiddleCenter, White);
            CreateButton(parent, "ConceptMinus", "CONCEPT -", new Vector2(0.5f, 1f), new Vector2(132f, 34f), new Vector2(-330f, -386f), OnConceptMinusClicked);
            CreateButton(parent, "ConceptPlus", "CONCEPT +", new Vector2(0.5f, 1f), new Vector2(132f, 34f), new Vector2(330f, -386f), OnConceptPlusClicked);

            _settingsTextScaleLabel = CreateText(parent, "TextScaleValue", "TEXT SCALE --", new Vector2(0.5f, 1f), new Vector2(360f, 28f), new Vector2(-210f, -436f), 14, TextAnchor.MiddleCenter, Muted);
            _settingsMotionLabel = CreateText(parent, "MotionValue", "UI MOTION --", new Vector2(0.5f, 1f), new Vector2(360f, 28f), new Vector2(210f, -436f), 14, TextAnchor.MiddleCenter, Muted);

            CreateButton(parent, "Reset", "RESET", new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(-250f, 62f), OnResetClicked);
            CreateButton(parent, "Apply", "APPLY", new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(0f, 62f), OnApplyClicked);
            CreateButton(parent, "Back", "BACK", new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(250f, 62f), OnBackClicked);
        }

        private void BuildLoadPanel(Transform parent)
        {
            CreateText(parent, "Title", "LOAD ARCHIVE", new Vector2(0.5f, 1f), new Vector2(650f, 56f), new Vector2(0f, -62f), 34, TextAnchor.MiddleCenter, White);
            CreateText(parent, "Subtitle", "VALIDATED SAVE SLOTS / FAIL-CLOSED LOAD", new Vector2(0.5f, 1f), new Vector2(650f, 28f), new Vector2(0f, -106f), 14, TextAnchor.MiddleCenter, Muted);

            _slotButtons[0] = CreateButton(parent, "Slot0", "SLOT 1\nSCANNING", new Vector2(0.5f, 1f), new Vector2(610f, 70f), new Vector2(0f, -176f), OnSlot0Clicked);
            _slotLabels[0] = ResolveChildTextCold(_slotButtons[0]);
            _slotButtons[1] = CreateButton(parent, "Slot1", "SLOT 2\nSCANNING", new Vector2(0.5f, 1f), new Vector2(610f, 70f), new Vector2(0f, -260f), OnSlot1Clicked);
            _slotLabels[1] = ResolveChildTextCold(_slotButtons[1]);
            _slotButtons[2] = CreateButton(parent, "Slot2", "SLOT 3\nSCANNING", new Vector2(0.5f, 1f), new Vector2(610f, 70f), new Vector2(0f, -344f), OnSlot2Clicked);
            _slotLabels[2] = ResolveChildTextCold(_slotButtons[2]);

            CreateButton(parent, "Back", "BACK", new Vector2(0.5f, 0f), new Vector2(170f, 40f), new Vector2(0f, 62f), OnBackClicked);
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
            group.interactable = visible;
            group.blocksRaycasts = visible;
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
                _settingsQualityLabel.text = "QUALITY WEIGHT " + quality + " / " + SettingsManager.MaxContinuousQualityLevel + "  (" + ResolveQualityName(quality) + ")";
            }

            if (_lastPreset != preset && _settingsPresetLabel != null)
            {
                _lastPreset = preset;
                _settingsPresetLabel.text = "GRAPHICS PRESET " + ResolvePresetName(preset);
            }

            if (_lastStyle != style && _settingsStyleLabel != null)
            {
                _lastStyle = style;
                _settingsStyleLabel.text = "STYLE " + (style + 1) + "/" + MenuVisualStyleCatalog.StyleCount + "  " + MenuVisualStyleCatalog.GetDisplayName(MenuVisualStyleCatalog.FromIndex(style)).ToString();
            }

            if (_lastConcept != concept && _settingsConceptLabel != null)
            {
                _lastConcept = concept;
                _settingsConceptLabel.text = "CONCEPT " + (concept + 1) + "/" + MenuVisualConceptCatalog.ConceptCount + "  " + MenuVisualConceptCatalog.GetDisplayName(MenuVisualConceptCatalog.FromIndex(concept)).ToString();
            }

            if (_lastTextScale != textScale && _settingsTextScaleLabel != null)
            {
                _lastTextScale = textScale;
                _settingsTextScaleLabel.text = "TEXT SCALE " + textScale + "%";
            }

            if (_lastMotionScale != motionScale && _settingsMotionLabel != null)
            {
                _lastMotionScale = motionScale;
                _settingsMotionLabel.text = "UI MOTION " + motionScale + "%";
            }
        }

        private void RefreshSlotLabelsIfNeeded()
        {
            if (_slotRefreshFrame >= 0)
                return;

            RebindLoadSlotControlsCold();
            _slotRefreshFrame = Time.frameCount;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_controller == null || !_controller.TryGetReadableSaveSlotState(i, out string title, out string detail, out bool canLoad))
                {
                    title = "SLOT " + (i + 1);
                    detail = "SAVE SYSTEM OFFLINE";
                    canLoad = false;
                }

                if (_slotLabels[i] != null)
                    _slotLabels[i].text = title + "\n" + detail;

                if (_slotButtons[i] != null)
                    _slotButtons[i].interactable = canLoad;
            }
        }

        private void RebindRootControlsCold()
        {
            if (_rootObject == null)
                return;

            Canvas canvas;
            if (_rootObject.TryGetComponent(out canvas))
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = OverlaySortingOrder;
            }

            if (_rootGroup == null)
                _rootObject.TryGetComponent(out _rootGroup);

            Image background;
            if (_rootObject.TryGetComponent(out background))
                background.color = Backdrop;

            Transform root = _rootObject.transform;
            RebindPanelCold(root, "H8_READABLE_Main", ref _mainPanel);
            RebindPanelCold(root, "H8_READABLE_Settings", ref _settingsPanelGroup);
            RebindPanelCold(root, "H8_READABLE_LoadArchive", ref _loadPanel);
            RebindPanelCold(root, "H8_READABLE_Loading", ref _loadingPanel);
            ApplyPanelColorCold(_mainPanel);
            ApplyPanelColorCold(_settingsPanelGroup);
            ApplyPanelColorCold(_loadPanel);
            ApplyPanelColorCold(_loadingPanel);
            RebindSettingsLabelsCold();
            RebindLoadSlotControlsCold();
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

        private static void RebindTextCold(Transform root, string name, ref Text text)
        {
            if (text != null || root == null)
                return;

            Transform textTransform = root.Find(name);
            if (textTransform != null)
                textTransform.TryGetComponent(out text);
        }

        private void RebindLoadSlotControlsCold()
        {
            if (_loadPanel == null)
                return;

            Transform root = _loadPanel.transform;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slotButtons[i] == null)
                {
                    Transform slot = root.Find("Slot" + i);
                    if (slot != null)
                        slot.TryGetComponent(out _slotButtons[i]);
                }

                if (_slotLabels[i] == null)
                    _slotLabels[i] = ResolveChildTextCold(_slotButtons[i]);
            }
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

        private void OnNewDiveClicked()
        {
            _controller?.ReadableStartNewGame();
        }

        private void OnLoadClicked()
        {
            _controller?.ReadableOpenLoadPanel();
        }

        private void OnSettingsClicked()
        {
            _controller?.ReadableOpenSettingsPanel();
        }

        private void OnOrbitClicked()
        {
            _controller?.ReadableStartOrbitPrologue();
        }

        private void OnQuitClicked()
        {
            _controller?.ReadableQuit();
        }

        private void OnBackClicked()
        {
            _controller?.ReadableBackToMainMenu();
        }

        private void OnSlot0Clicked()
        {
            _controller?.ReadableLoadSlot(0);
        }

        private void OnSlot1Clicked()
        {
            _controller?.ReadableLoadSlot(1);
        }

        private void OnSlot2Clicked()
        {
            _controller?.ReadableLoadSlot(2);
        }

        private void OnQualityMinusClicked()
        {
            _settingsPanel?.ReadableQualityDecrease();
            InvalidateSettingsLabels();
        }

        private void OnQualityPlusClicked()
        {
            _settingsPanel?.ReadableQualityIncrease();
            InvalidateSettingsLabels();
        }

        private void OnPresetLowClicked()
        {
            _settingsPanel?.ReadableSelectGraphicsPreset(0);
            InvalidateSettingsLabels();
        }

        private void OnPresetMediumClicked()
        {
            _settingsPanel?.ReadableSelectGraphicsPreset(1);
            InvalidateSettingsLabels();
        }

        private void OnPresetHighClicked()
        {
            _settingsPanel?.ReadableSelectGraphicsPreset(2);
            InvalidateSettingsLabels();
        }

        private void OnPresetUltraClicked()
        {
            _settingsPanel?.ReadableSelectGraphicsPreset(3);
            InvalidateSettingsLabels();
        }

        private void OnStyleMinusClicked()
        {
            _settingsPanel?.ReadableMenuStyleDecrease();
            InvalidateSettingsLabels();
        }

        private void OnStylePlusClicked()
        {
            _settingsPanel?.ReadableMenuStyleIncrease();
            InvalidateSettingsLabels();
        }

        private void OnConceptMinusClicked()
        {
            _settingsPanel?.ReadableMenuConceptDecrease();
            InvalidateSettingsLabels();
        }

        private void OnConceptPlusClicked()
        {
            _settingsPanel?.ReadableMenuConceptIncrease();
            InvalidateSettingsLabels();
        }

        private void OnResetClicked()
        {
            _settingsPanel?.ReadableResetDefaults();
            InvalidateSettingsLabels();
        }

        private void OnApplyClicked()
        {
            _settingsPanel?.ReadableApply();
            InvalidateSettingsLabels();
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

        private Text CreateText(
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
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = _resolvedFont;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(9, fontSize - 8);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchor,
            Vector2 size,
            Vector2 anchoredPosition,
            UnityAction action)
        {
            RectTransform rect = CreateRect(name, parent, anchor, size, anchoredPosition);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Button;
            image.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Button;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = Amber;
            colors.selectedColor = ButtonHover;
            colors.disabledColor = ButtonDisabled;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            if (action != null)
                button.onClick.AddListener(action);

            Text text = CreateText(rect, "Label", label, new Vector2(0.5f, 0.5f), new Vector2(size.x - 20f, size.y - 8f), Vector2.zero, 17, TextAnchor.MiddleCenter, White);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return button;
        }

        private void CreateLine(RectTransform parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchor, size, anchoredPosition);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static Font ResolveFontCold()
        {
            Text[] existingTexts = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include); // COLD ALLOC: font discovery from existing menu labels.
            for (int i = 0; i < existingTexts.Length; i++)
            {
                Text text = existingTexts[i];
                if (text != null && text.font != null)
                    return text.font;
            }

            Font legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacy != null)
                return legacy;

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Text ResolveChildTextCold(Button button)
        {
            if (button == null)
                return null;

            return ResolveChildTextCold(button.transform);
        }

        private static Text ResolveChildTextCold(Transform transform)
        {
            if (transform == null)
                return null;

            if (transform.TryGetComponent(out Text text))
                return text;

            for (int i = 0; i < transform.childCount; i++)
            {
                Text childText = ResolveChildTextCold(transform.GetChild(i));
                if (childText != null)
                    return childText;
            }

            return null;
        }
    }
}
