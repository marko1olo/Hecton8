using System;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.SaveSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Pause Menu Controller")]
    public sealed class PauseMenuController : MonoBehaviour, ITickable
    {
        private enum PauseSection
        {
            Main = 0,
            Saves = 1,
            Help = 2,
            Settings = 3
        }

        private static readonly Color ShellBg = new Color(0.03f, 0.07f, 0.09f, 0.92f);
        private static readonly Color HeaderBg = new Color(0.07f, 0.16f, 0.18f, 0.86f);
        private static readonly Color PanelBg = new Color(0.05f, 0.11f, 0.13f, 0.82f);
        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color Dim = new Color(0.78f, 0.96f, 0.93f, 0.84f);
        private static readonly Color DimLow = new Color(0.56f, 0.74f, 0.71f, 0.72f);
        private static readonly Color ButtonBg = new Color(0.08f, 0.16f, 0.18f, 0.84f);
        private static readonly Color ButtonHover = new Color(0.12f, 0.24f, 0.28f, 0.94f);
        private static readonly Color Rule = new Color(0.46f, 0.98f, 0.94f, 0.18f);

        [Header("References")]
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Settings")]
        [SerializeField] private string mainMenuSceneName = "01_MAIN_MENU";
        [SerializeField] private string[] saveSlots = { "slot_1", "slot_2", "slot_3" };
        [SerializeField] private bool pauseTimeScale = true;

        private static int _openMenuCount;

        private bool _registered;
        private bool _built;
        private bool _isOpen;
        private PauseSection _activeSection;
        private float _cachedTimeScale = 1f;

        private RectTransform _root;
        private CanvasGroup _canvasGroup;
        private Image _background;
        private TextMeshProUGUI _headerTitle;
        private TextMeshProUGUI _headerSub;
        private TextMeshProUGUI _footerHint;
        private RectTransform _mainPanel;
        private RectTransform _savesPanel;
        private RectTransform _helpPanel;
        private RectTransform _settingsPanel;
        private CanvasGroup _mainPanelCanvasGroup;
        private CanvasGroup _savesPanelCanvasGroup;
        private CanvasGroup _helpPanelCanvasGroup;
        private CanvasGroup _settingsPanelCanvasGroup;
        private TextMeshProUGUI _saveStatus;
        private PauseControlsPanel _controlsPanel;

        public bool IsOpen => _isOpen;
        public bool IsSettingsOpen => _isOpen && _activeSection == PauseSection.Settings;
        public static bool IsAnyOpen => _openMenuCount > 0;

        // Простой кэш для ToUpperInvariant, чтобы уменьшить аллокации в UI-строках
        private static readonly string[] _cachedUpperStrings = new string[16];

        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            int hash = input.GetHashCode() & 0xF;
            string cached = _cachedUpperStrings[hash];
            if (cached != null && string.Equals(cached, input, StringComparison.OrdinalIgnoreCase))
                return cached;

            string upper = input.ToUpperInvariant();
            _cachedUpperStrings[hash] = upper;
            return upper;
        }

        private void Awake()
        {
            AutoResolve();
            EnsureBuilt();
            ApplyClosedState(restorePlayerInput: false);
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

            bool restorePlayerInput = _isOpen && ShouldRestorePlayerInputOnDisable();
            ApplyClosedState(restorePlayerInput: restorePlayerInput);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif

            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;
        }

        public void Tick(float deltaTime)
        {
            if (!Application.isPlaying)
                return;

            bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool startPressed = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;

            if (!escapePressed && !startPressed)
                return;

            if (!_isOpen)
            {
                if (PlayerPDA.IsOpen || HectonFabricatorUI.IsMenuOpen)
                    return;

                Open();
                return;
            }

            if (_activeSection == PauseSection.Main)
            {
                Close();
                return;
            }

            ShowSection(PauseSection.Main);
        }

        public void Open()
        {
            if (_isOpen)
                return;

            EnsureBuilt();

            _isOpen = true;
            RegisterOpenMenu();
            _activeSection = PauseSection.Main;

            if (pauseTimeScale)
            {
                _cachedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            if (InputManager.Instance != null)
                InputManager.Instance.SwitchToUIInput();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            ShowSection(PauseSection.Main);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }

        public void Close()
        {
            if (!_isOpen)
                return;

            ApplyClosedState(restorePlayerInput: true);
        }

        internal void RefreshSettingsPanel()
        {
            if (_controlsPanel != null)
                _controlsPanel.RefreshAllBindingsNow();
        }

        private void ApplyClosedState(bool restorePlayerInput)
        {
            bool wasOpen = _isOpen;
            _isOpen = false;
            if (wasOpen)
                UnregisterOpenMenu();
            _activeSection = PauseSection.Main;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (pauseTimeScale)
                Time.timeScale = Mathf.Approximately(Time.timeScale, 0f) ? _cachedTimeScale : Time.timeScale;

            if (restorePlayerInput &&
                InputManager.Instance != null &&
                InputManager.Instance.CanSwitchActionMaps)
            {
                InputManager.Instance.SwitchToPlayerInput();
            }

            ApplyCursorState(restorePlayerInput);

        }

        private static void ApplyCursorState(bool restorePlayerInput)
        {
            if (restorePlayerInput)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
        }

        private static void RegisterOpenMenu()
        {
            if (_openMenuCount < int.MaxValue)
                _openMenuCount++;
        }

        private static void UnregisterOpenMenu()
        {
            if (_openMenuCount > 0)
                _openMenuCount--;
        }

        private static bool ShouldRestorePlayerInputOnDisable()
        {
            if (!Application.isPlaying)
                return false;

            InputManager inputManager = InputManager.Instance;
            return inputManager != null && inputManager.CanSwitchActionMaps;
        }

        private void AutoResolve()
        {
            if (playerPDA == null)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    playerPDA = playerTransform.GetComponentInChildren<PlayerPDA>(true);
                }
            }
            labelFont = ResolveReadableFont(labelFont);
            if (numericFont == null)
                numericFont = labelFont;
            else if (IsNumericOnlyFont(labelFont) && !IsNumericOnlyFont(numericFont))
                labelFont = numericFont;
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            RectTransform self = transform as RectTransform;
            if (self == null)
                return;

            _root = self;
            Stretch(_root, 0f, 0f, 0f, 0f);

            // Ensure Canvas for UI rendering and cursor
            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000; // High order to appear on top
            }

            // Ensure CanvasScaler for proper scaling
            UnityEngine.UI.CanvasScaler scaler = gameObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            // Ensure GraphicRaycaster for input
            UnityEngine.UI.GraphicRaycaster raycaster = gameObject.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
                raycaster = gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _background = gameObject.GetComponent<Image>();
            if (_background == null)
                _background = gameObject.AddComponent<Image>();
            _background.color = ShellBg;
            _background.raycastTarget = true;

            ClearChildren(_root);

            RectTransform shell = CreateRect(_root, "PauseShell");
            shell.anchorMin = new Vector2(0.5f, 0.5f);
            shell.anchorMax = new Vector2(0.5f, 0.5f);
            shell.pivot = new Vector2(0.5f, 0.5f);
            shell.anchoredPosition = Vector2.zero;
            shell.sizeDelta = new Vector2(1240f, 720f);
            Image shellBg = EnsureImage(shell.gameObject);
            shellBg.color = new Color(0.02f, 0.05f, 0.07f, 0.96f);
            shellBg.raycastTarget = false;

            RectTransform header = CreateRect(shell, "Header");
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -12f), new Vector2(-14f, 58f));
            Image headerBg = EnsureImage(header.gameObject);
            headerBg.color = HeaderBg;
            headerBg.raycastTarget = false;

            _headerTitle = CreateText(header, "HeaderTitle", labelFont, 20f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(_headerTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), new Vector2(18f, 0f), new Vector2(-8f, 0f));
            _headerTitle.color = Primary;
            _headerTitle.SetText("MISSION PAUSE");

            _headerSub = CreateText(header, "HeaderSub", labelFont, 11f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(_headerSub.rectTransform, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-18f, 0f));
            _headerSub.color = DimLow;

            CreateRule(shell, new Vector2(0.04f, 1f), new Vector2(0.96f, 1f), -76f);
            CreateRule(shell, new Vector2(0.04f, 0f), new Vector2(0.96f, 0f), 58f);

            RectTransform content = CreateRect(shell, "Content");
            Stretch(content, 22f, 22f, 92f, 74f);

            _mainPanel = CreatePanel(content, "MainPanel");
            _savesPanel = CreatePanel(content, "SavesPanel");
            _helpPanel = CreatePanel(content, "HelpPanel");
            _settingsPanel = CreatePanel(content, "SettingsPanel");
            _mainPanelCanvasGroup = EnsurePanelCanvasGroup(_mainPanel);
            _savesPanelCanvasGroup = EnsurePanelCanvasGroup(_savesPanel);
            _helpPanelCanvasGroup = EnsurePanelCanvasGroup(_helpPanel);
            _settingsPanelCanvasGroup = EnsurePanelCanvasGroup(_settingsPanel);

            BuildMainPanel(_mainPanel);
            BuildSavesPanel(_savesPanel);
            BuildHelpPanel(_helpPanel);
            BuildSettingsPanel(_settingsPanel);

            RectTransform footer = CreateRect(shell, "Footer");
            Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 12f), new Vector2(-14f, 34f));
            Image footerBg = EnsureImage(footer.gameObject);
            footerBg.color = new Color(0.06f, 0.11f, 0.13f, 0.86f);
            footerBg.raycastTarget = false;

            _footerHint = CreateText(footer, "FooterHint", labelFont, 10.5f, FontStyles.Italic, TextAlignmentOptions.Center);
            Stretch(_footerHint.rectTransform, 12f, 12f, 0f, 0f);
            _footerHint.color = DimLow;
            _footerHint.SetText("ESC = back / resume  |  SETTINGS hosts controls and rebinds");

            _built = true;
        }

        private void BuildMainPanel(RectTransform panel)
        {
            TextMeshProUGUI title = CreateSectionTitle(panel, "MISSION CONTROL");
            title.SetText("MISSION CONTROL");

            string[] labels =
            {
                "RESUME EXPEDITION",
                "SAVE STATION",
                "FIELD GUIDE",
                "SETTINGS",
                "EXIT TO MAIN MENU",
                "QUIT APPLICATION"
            };

            Action[] actions =
            {
                Close,
                () => ShowSection(PauseSection.Saves),
                () => ShowSection(PauseSection.Help),
                () => ShowSection(PauseSection.Settings),
                ExitToMainMenu,
                QuitApplication
            };

            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform btn = CreateButton(panel, $"MainButton_{i}", labels[i], new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -88f - i * 58f), new Vector2(420f, 42f), actions[i]);

                if (i == 0)
                    GetText(btn, "Label")?.SetText("RESUME EXPEDITION");
            }
        }

        private void BuildSavesPanel(RectTransform panel)
        {
            CreateSectionTitle(panel, "SAVE STATION").SetText("SAVE STATION");
            CreateSectionSub(panel, "Manual save points. Use these before risky dives or major construction changes.")
                .rectTransform.anchoredPosition = new Vector2(0f, -42f);

            for (int i = 0; i < saveSlots.Length; i++)
            {
                string slotName = saveSlots[i];
                RectTransform btn = CreateButton(panel, $"SaveSlot_{i}", $"WRITE {CachedToUpperInvariant(slotName)}",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -108f - i * 56f), new Vector2(420f, 40f),
                    () => SaveSlot(slotName));

                TextMeshProUGUI label = GetText(btn, "Label");
                if (label != null)
                    label.alignment = TextAlignmentOptions.Center;
            }

            _saveStatus = CreateText(panel, "SaveStatus", numericFont, 11f, FontStyles.Normal, TextAlignmentOptions.Center);
            Anchor(_saveStatus.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(22f, 66f), new Vector2(-22f, 22f));
            _saveStatus.color = Dim;
            _saveStatus.SetText("Awaiting save command.");

            CreateBackButton(panel, () => ShowSection(PauseSection.Main));
        }

        private void BuildHelpPanel(RectTransform panel)
        {
            CreateSectionTitle(panel, "FIELD GUIDE").SetText("FIELD GUIDE");

            TextMeshProUGUI body = CreateText(panel, "HelpBody", numericFont, 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Anchor(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 68f), new Vector2(-28f, -74f));
            body.color = Dim;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.SetText(
                "CORE INPUTS\n" +
                "TAB  // PDA shell\n" +
                "I    // inventory direct open\n" +
                "1-4  // quick slot arm/swap\n" +
                "LMB/RMB // primary / secondary tool action\n\n" +
                "MISSION RHYTHM\n" +
                "1. Scan and classify unknowns.\n" +
                "2. Repair and stabilize critical infrastructure.\n" +
                "3. Keep loadout aligned with cargo before committing to depth.\n" +
                "4. Save before hazardous traversal, fauna contact, or base edits.");

            CreateBackButton(panel, () => ShowSection(PauseSection.Main));
        }

        private void BuildSettingsPanel(RectTransform panel)
        {
            CreateSectionTitle(panel, "SETTINGS").SetText("SETTINGS");
            CreateSectionSub(panel, "Controls were moved out of the PDA. Rebind them here.")
                .rectTransform.anchoredPosition = new Vector2(0f, -42f);

            RectTransform controlsRoot = CreateRect(panel, "ControlsPanel");
            Anchor(controlsRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(22f, 62f), new Vector2(-22f, -80f));
            PauseControlsPanel controls = controlsRoot.gameObject.AddComponent<PauseControlsPanel>();
            controls.Configure(this, labelFont, labelFont);
            _controlsPanel = controls;

            CreateBackButton(panel, () => ShowSection(PauseSection.Main));
        }

        private void ShowSection(PauseSection section)
        {
            _activeSection = section;

            SetPanelVisible(_mainPanelCanvasGroup, section == PauseSection.Main);
            SetPanelVisible(_savesPanelCanvasGroup, section == PauseSection.Saves);
            SetPanelVisible(_helpPanelCanvasGroup, section == PauseSection.Help);
            SetPanelVisible(_settingsPanelCanvasGroup, section == PauseSection.Settings);

            if (_headerSub == null)
                return;

            switch (section)
            {
                case PauseSection.Main:
                    _headerSub.SetText("resume, save, inspect field guidance, or move into settings");
                    break;
                case PauseSection.Saves:
                    _headerSub.SetText("manual persistence via SaveManager");
                    break;
                case PauseSection.Help:
                    _headerSub.SetText("compact operational reference for current tool and inventory loop");
                    break;
                case PauseSection.Settings:
                    _headerSub.SetText("controls and interaction tuning are managed here now");
                    RefreshSettingsPanel();
                    break;
            }
        }

        private async void SaveSlot(string slotName)
        {
            if (_saveStatus != null)
                _saveStatus.text = $"WRITING {CachedToUpperInvariant(slotName)}...";

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
            {
                if (_saveStatus != null)
                    _saveStatus.SetText("SAVE MANAGER UNAVAILABLE.");
                return;
            }

            try
            {
                if (saveManager.IsBusy)
                {
                    if (_saveStatus != null)
                        _saveStatus.text = "SAVE ALREADY IN PROGRESS.";
                    return;
                }

                await saveManager.SaveGameAsync(slotName);
                if (_saveStatus != null)
                {
                    _saveStatus.text = saveManager.LastOperationSucceeded
                        ? $"{CachedToUpperInvariant(slotName)} WRITTEN."
                        : $"{CachedToUpperInvariant(slotName)} FAILED. {CachedToUpperInvariant(saveManager.LastOperationError ?? string.Empty)}";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PauseMenuController] Save failed for '{slotName}': {ex.Message}");
                if (_saveStatus != null)
                    _saveStatus.text = $"{CachedToUpperInvariant(slotName)} FAILED. CHECK CONSOLE.";
            }
        }

        private void ExitToMainMenu()
        {
            if (pauseTimeScale)
                Time.timeScale = 1f;

            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void QuitApplication()
        {
            if (pauseTimeScale)
                Time.timeScale = 1f;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static CanvasGroup EnsurePanelCanvasGroup(RectTransform panel)
        {
            if (panel == null)
                return null;

            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null)
                group = panel.gameObject.AddComponent<CanvasGroup>();

            return group;
        }

        private static void SetPanelVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private static RectTransform CreatePanel(Transform parent, string name)
        {
            RectTransform rect = CreateRect(parent, name);
            Stretch(rect, 0f, 0f, 0f, 0f);
            Image bg = EnsureImage(rect.gameObject);
            bg.color = PanelBg;
            bg.raycastTarget = false;
            return rect;
        }

        private RectTransform CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Action onClick)
        {
            RectTransform rect = CreateRect(parent, name);
            Anchor(rect, anchorMin, anchorMax, anchoredPosition, size);
            Image bg = EnsureImage(rect.gameObject);
            bg.color = ButtonBg;
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonBg;
            colors.highlightedColor = ButtonHover;
            colors.selectedColor = ButtonHover;
            colors.pressedColor = new Color(0.16f, 0.32f, 0.36f, 1f);
            colors.disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.5f);
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() => onClick?.Invoke());

            TextMeshProUGUI text = CreateText(rect, "Label", labelFont, 12f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
            text.color = Primary;
            text.SetText(label);

            return rect;
        }

        private void CreateBackButton(Transform parent, Action onClick)
        {
            CreateButton(parent, "BackButton", "BACK", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-108f, 28f), new Vector2(180f, 34f), onClick);
        }

        private TextMeshProUGUI CreateSectionTitle(Transform parent, string value)
        {
            TextMeshProUGUI text = CreateText(parent, $"{value}_Title", labelFont, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -18f), new Vector2(-24f, 24f));
            text.color = Primary;
            text.SetText(value);
            return text;
        }

        private TextMeshProUGUI CreateSectionSub(Transform parent, string value)
        {
            TextMeshProUGUI text = CreateText(parent, "Subtitle", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Left);
            Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -42f), new Vector2(-24f, 18f));
            text.color = DimLow;
            text.SetText(value);
            return text;
        }

        private static TextMeshProUGUI GetText(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private static void CreateRule(Transform parent, Vector2 anchorMin, Vector2 anchorMax, float y)
        {
            RectTransform rect = CreateRect(parent, "Rule");
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, anchorMin.y < 0.5f ? 0f : 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, 1f);
            Image img = EnsureImage(rect.gameObject);
            img.color = Rule;
            img.raycastTarget = false;
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

        private static Image EnsureImage(GameObject target)
        {
            Image image = target.GetComponent<Image>();
            if (image == null)
                image = target.AddComponent<Image>();
            return image;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font,
            float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.color = Dim;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2((anchorMin.x + anchorMax.x) * 0.5f, (anchorMin.y + anchorMax.y) * 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static TMP_FontAsset ResolveReadableFont(TMP_FontAsset preferred)
        {
            if (preferred != null && !IsNumericOnlyFont(preferred))
                return preferred;

            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < fonts.Length; i++)
            {
                TMP_FontAsset candidate = fonts[i];
                if (candidate == null)
                    continue;

                string name = candidate.name;
                if (name.IndexOf("текст", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static bool IsNumericOnlyFont(TMP_FontAsset font)
        {
            if (font == null)
                return false;

            string name = font.name;
            return name.IndexOf("циф", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("digit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("number", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
