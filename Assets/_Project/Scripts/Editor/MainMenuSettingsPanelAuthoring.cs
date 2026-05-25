using System.Collections.Generic;
using System.IO;
using Hecton.UI.MainMenu;
using Hecton8.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.Editor
{
    /// <summary>
    /// Rebuilds the 01_MAIN_MENU settings panel and assigns all required scene references.
    /// Editor-only authoring utility; does not run in player builds.
    /// </summary>
    internal static class MainMenuSettingsPanelAuthoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/01_MAIN_MENU.unity";
        private const string MixerPath = "Assets/_Project/MasterMixer.mixer";

        private static readonly Color PanelColor = new Color(0.04f, 0.08f, 0.10f, 0.94f);
        private static readonly Color SectionColor = new Color(0.08f, 0.13f, 0.16f, 0.92f);
        private static readonly Color LabelColor = new Color(0.78f, 0.89f, 0.94f, 1f);
        private static readonly Color ValueColor = new Color(0.92f, 0.98f, 1f, 1f);
        private static readonly Color AccentColor = new Color(0.24f, 0.77f, 0.86f, 1f);
        private static readonly Color ToggleOnColor = new Color(0.18f, 0.78f, 0.64f, 1f);
        private static readonly List<GameObject> SceneRootScratch = new List<GameObject>(16);
        private static readonly List<Component> ComponentScratch = new List<Component>(8);

        [MenuItem("Hecton/UI/Rebuild Main Menu Settings Panel", priority = 231)]
        private static void RebuildMainMenuSettingsPanel()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            if (!scene.IsValid())
            {
                throw new IOException("Failed to open 01_MAIN_MENU scene.");
            }

            Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                throw new MissingReferenceException("Canvas root not found in 01_MAIN_MENU.");
            }

            GameObject panelObject = FindRequired(canvas.transform, "Panel_Settings");
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            CanvasGroup panelGroup = panelObject.GetComponent<CanvasGroup>() ?? panelObject.AddComponent<CanvasGroup>();
            Image panelImage = panelObject.GetComponent<Image>() ?? panelObject.AddComponent<Image>();

            ConfigurePanelRoot(panelRect, panelGroup, panelImage);

            RectTransform container = EnsureChildRect(panelRect, "Container");
            RebuildChildren(container);
            ConfigureContainer(container);

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;

            CreateHeader(container, font);

            RectTransform presetsSection = CreateSection(container, "Section_Presets");
            CreateSectionLabel(presetsSection, "Label_Presets", "PRESETS", font);
            RectTransform presetsRow = CreateRow(presetsSection, "Row_Presets", 12f, 68f);
            Button btnPresetLow = CreateButton(presetsRow, "Btn_PresetLow", "LOW", new Color(0.22f, 0.26f, 0.29f, 1f), font);
            Button btnPresetMedium = CreateButton(presetsRow, "Btn_PresetMedium", "MEDIUM", new Color(0.30f, 0.35f, 0.39f, 1f), font);
            Button btnPresetHigh = CreateButton(presetsRow, "Btn_PresetHigh", "HIGH", new Color(0.38f, 0.43f, 0.48f, 1f), font);
            Button btnPresetUltra = CreateButton(presetsRow, "Btn_PresetUltra", "ULTRA", new Color(0.50f, 0.56f, 0.60f, 1f), font);

            RectTransform graphicsSection = CreateSection(container, "Section_Graphics");
            CreateSectionLabel(graphicsSection, "Label_Graphics", "GRAPHICS", font);

            TMP_Text txtQualityLevel;
            Button btnQualityDecrease;
            Button btnQualityIncrease;
            CreateStepValueRow(graphicsSection, "Row_QualityLevel", "Quality Level", font, out btnQualityDecrease, out txtQualityLevel, out btnQualityIncrease);

            Slider sliderFieldOfView;
            TMP_Text txtFieldOfView;
            CreateSliderRow(graphicsSection, "Row_FOV", "Field Of View", font, 60f, 110f, 75f, out sliderFieldOfView, out txtFieldOfView, " deg");

            TMP_Text txtShadowQuality;
            Button btnShadowQualityDecrease;
            Button btnShadowQualityIncrease;
            CreateStepValueRow(graphicsSection, "Row_ShadowQuality", "Shadow Quality", font, out btnShadowQualityDecrease, out txtShadowQuality, out btnShadowQualityIncrease);

            Slider sliderShadowDistance;
            TMP_Text txtShadowDistance;
            CreateSliderRow(graphicsSection, "Row_ShadowDistance", "Shadow Distance", font, 50f, 300f, 200f, out sliderShadowDistance, out txtShadowDistance, "m");

            TMP_Text txtAntiAliasing;
            Button btnAntiAliasingDecrease;
            Button btnAntiAliasingIncrease;
            CreateStepValueRow(graphicsSection, "Row_AntiAliasing", "Anti-Aliasing", font, out btnAntiAliasingDecrease, out txtAntiAliasing, out btnAntiAliasingIncrease);

            RectTransform togglesRow = CreateRow(graphicsSection, "Row_Toggles", 16f, 42f);
            Toggle toggleVsync = CreateToggle(togglesRow, "Toggle_Vsync", "V-Sync", font);
            Toggle toggleFullscreen = CreateToggle(togglesRow, "Toggle_Fullscreen", "Fullscreen", font);
            Toggle toggleAmbientOcclusion = CreateToggle(togglesRow, "Toggle_AO", "Ambient Occlusion", font);
            Toggle toggleBloom = CreateToggle(togglesRow, "Toggle_Bloom", "Bloom", font);
            Toggle toggleMotionBlur = CreateToggle(togglesRow, "Toggle_MotionBlur", "Motion Blur", font);

            TMP_Text txtTextureQuality;
            Button btnTextureQualityDecrease;
            Button btnTextureQualityIncrease;
            CreateStepValueRow(graphicsSection, "Row_TextureQuality", "Texture Quality", font, out btnTextureQualityDecrease, out txtTextureQuality, out btnTextureQualityIncrease);

            RectTransform audioSection = CreateSection(container, "Section_Audio");
            CreateSectionLabel(audioSection, "Label_Audio", "AUDIO", font);

            Slider sliderMasterVolume;
            TMP_Text txtMasterVolume;
            CreateSliderRow(audioSection, "Row_MasterVolume", "Master", font, 0f, 1f, 0.8f, out sliderMasterVolume, out txtMasterVolume, "%", true);

            Slider sliderMusicVolume;
            TMP_Text txtMusicVolume;
            CreateSliderRow(audioSection, "Row_MusicVolume", "Music", font, 0f, 1f, 0.8f, out sliderMusicVolume, out txtMusicVolume, "%", true);

            Slider sliderSfxVolume;
            TMP_Text txtSfxVolume;
            CreateSliderRow(audioSection, "Row_SfxVolume", "SFX", font, 0f, 1f, 0.8f, out sliderSfxVolume, out txtSfxVolume, "%", true);

            Slider sliderAmbientVolume;
            TMP_Text txtAmbientVolume;
            CreateSliderRow(audioSection, "Row_AmbientVolume", "Ambient", font, 0f, 1f, 0.8f, out sliderAmbientVolume, out txtAmbientVolume, "%", true);

            RectTransform actionsRow = CreateRow(container, "Row_Actions", 12f, 62f);
            Button btnResetDefaults = CreateButton(actionsRow, "Btn_ResetDefaults", "RESET", new Color(0.35f, 0.38f, 0.42f, 1f), font);
            Button btnApply = CreateButton(actionsRow, "Btn_Apply", "APPLY", new Color(0.18f, 0.60f, 0.26f, 1f), font);
            Button btnCancel = CreateButton(actionsRow, "Btn_Cancel", "CANCEL", new Color(0.30f, 0.30f, 0.34f, 1f), font);
            Button btnBackFromSettings = CreateButton(actionsRow, "Btn_BackFromSettings", "RETURN", new Color(0.16f, 0.28f, 0.40f, 1f), font);

            SettingsPanel settingsPanel = panelObject.GetComponent<SettingsPanel>() ?? panelObject.AddComponent<SettingsPanel>();
            WireSettingsPanel(
                settingsPanel,
                btnPresetLow, btnPresetMedium, btnPresetHigh, btnPresetUltra,
                btnQualityDecrease, btnQualityIncrease, txtQualityLevel,
                toggleVsync, toggleFullscreen,
                sliderFieldOfView, txtFieldOfView,
                btnShadowQualityDecrease, btnShadowQualityIncrease, txtShadowQuality,
                sliderShadowDistance, txtShadowDistance,
                btnAntiAliasingDecrease, btnAntiAliasingIncrease, txtAntiAliasing,
                toggleAmbientOcclusion, toggleBloom, toggleMotionBlur,
                btnTextureQualityDecrease, btnTextureQualityIncrease, txtTextureQuality,
                sliderMasterVolume, sliderMusicVolume, sliderSfxVolume, sliderAmbientVolume,
                txtMasterVolume, txtMusicVolume, txtSfxVolume, txtAmbientVolume,
                btnResetDefaults, btnApply, btnCancel,
                null);

            SettingsManager settingsManager = Object.FindAnyObjectByType<SettingsManager>(FindObjectsInactive.Include);
            if (settingsManager == null)
            {
                throw new MissingReferenceException("[SettingsManager] root is missing.");
            }

            Camera mainCamera = Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
            Object volume = FindComponentByTypeName("[SETTINGS_VOLUME]", "UnityEngine.Rendering.Volume");
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            WireSettingsManager(settingsManager, mainCamera, volume, mixer);

            SettingsLivePreview livePreview = settingsManager.GetComponent<SettingsLivePreview>();
            if (livePreview == null)
            {
                livePreview = settingsManager.gameObject.AddComponent<SettingsLivePreview>();
            }

            WireSettingsLivePreview(livePreview, mainCamera, volume);
            WireSettingsPanel(
                settingsPanel,
                btnPresetLow, btnPresetMedium, btnPresetHigh, btnPresetUltra,
                btnQualityDecrease, btnQualityIncrease, txtQualityLevel,
                toggleVsync, toggleFullscreen,
                sliderFieldOfView, txtFieldOfView,
                btnShadowQualityDecrease, btnShadowQualityIncrease, txtShadowQuality,
                sliderShadowDistance, txtShadowDistance,
                btnAntiAliasingDecrease, btnAntiAliasingIncrease, txtAntiAliasing,
                toggleAmbientOcclusion, toggleBloom, toggleMotionBlur,
                btnTextureQualityDecrease, btnTextureQualityIncrease, txtTextureQuality,
                sliderMasterVolume, sliderMusicVolume, sliderSfxVolume, sliderAmbientVolume,
                txtMasterVolume, txtMusicVolume, txtSfxVolume, txtAmbientVolume,
                btnResetDefaults, btnApply, btnCancel,
                livePreview);

            EnsureLoadingScreenUI(canvas.transform, font);
            Button btnBackFromSaveLoad = EnsureSaveLoadBackButton(canvas.transform, font);

            MainMenuController mainMenuController = canvas.GetComponent<MainMenuController>();
            if (mainMenuController != null)
            {
                WireMainMenuController(mainMenuController, canvas.transform, btnBackFromSaveLoad, btnBackFromSettings);
            }

            EditorUtility.SetDirty(panelObject);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigurePanelRoot(RectTransform panelRect, CanvasGroup group, Image image)
        {
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1280f, 920f);

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            image.color = PanelColor;
            image.raycastTarget = false;
        }

        private static void ConfigureContainer(RectTransform container)
        {
            container.anchorMin = new Vector2(0f, 0f);
            container.anchorMax = new Vector2(1f, 1f);
            container.pivot = new Vector2(0.5f, 0.5f);
            container.offsetMin = new Vector2(36f, 36f);
            container.offsetMax = new Vector2(-36f, -36f);

            VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>() ?? container.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void RebuildChildren(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void CreateHeader(RectTransform container, TMP_FontAsset font)
        {
            GameObject header = new GameObject("Header_Graphics", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            header.transform.SetParent(container, false);

            LayoutElement layout = header.GetComponent<LayoutElement>();
            layout.preferredHeight = 54f;

            TextMeshProUGUI text = header.GetComponent<TextMeshProUGUI>();
            ConfigureText(text, font, 34f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, ValueColor);
            text.SetText("SETTINGS");
        }

        private static RectTransform CreateSection(RectTransform parent, string name)
        {
            RectTransform section = EnsureChildRect(parent, name);
            Image image = section.GetComponent<Image>() ?? section.gameObject.AddComponent<Image>();
            image.color = SectionColor;
            image.raycastTarget = false;

            VerticalLayoutGroup layout = section.GetComponent<VerticalLayoutGroup>() ?? section.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LayoutElement element = section.GetComponent<LayoutElement>() ?? section.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = -1f;

            return section;
        }

        private static void CreateSectionLabel(RectTransform parent, string name, string text, TMP_FontAsset font)
        {
            GameObject label = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            label.transform.SetParent(parent, false);

            LayoutElement layout = label.GetComponent<LayoutElement>();
            layout.preferredHeight = 30f;

            TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
            ConfigureText(tmp, font, 22f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, AccentColor);
            tmp.SetText(text);
        }

        private static RectTransform CreateRow(RectTransform parent, string name, float spacing, float preferredHeight)
        {
            RectTransform row = EnsureChildRect(parent, name);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>() ?? row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            LayoutElement element = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = 1f;

            return row;
        }

        private static void CreateStepValueRow(
            RectTransform parent,
            string rowName,
            string labelText,
            TMP_FontAsset font,
            out Button decreaseButton,
            out TMP_Text valueText,
            out Button increaseButton)
        {
            RectTransform row = CreateRow(parent, rowName, 12f, 44f);
            CreateFixedLabel(row, "Label_" + rowName, labelText, font, 260f);
            decreaseButton = CreateButton(row, "Btn_Decrease", "-", new Color(0.20f, 0.28f, 0.34f, 1f), font, 46f);
            valueText = CreateValueText(row, "Txt_Value", "--", font, 240f);
            increaseButton = CreateButton(row, "Btn_Increase", "+", new Color(0.20f, 0.28f, 0.34f, 1f), font, 46f);
        }

        private static void CreateSliderRow(
            RectTransform parent,
            string rowName,
            string labelText,
            TMP_FontAsset font,
            float minValue,
            float maxValue,
            float initialValue,
            out Slider slider,
            out TMP_Text valueText,
            string suffix,
            bool valueAsPercent = false)
        {
            RectTransform row = CreateRow(parent, rowName, 12f, 48f);
            CreateFixedLabel(row, "Label_" + rowName, labelText, font, 260f);
            slider = CreateSlider(row, "Slider_" + rowName.Replace("Row_", string.Empty), minValue, maxValue, initialValue);

            string formatted = valueAsPercent
                ? Mathf.RoundToInt(initialValue * 100f) + suffix
                : Mathf.RoundToInt(initialValue) + suffix;

            valueText = CreateValueText(row, "Txt_" + rowName.Replace("Row_", string.Empty), formatted, font, 110f);
        }

        private static TMP_Text CreateFixedLabel(RectTransform parent, string name, string text, TMP_FontAsset font, float width)
        {
            GameObject label = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            label.transform.SetParent(parent, false);

            LayoutElement layout = label.GetComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 40f;

            TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
            ConfigureText(tmp, font, 20f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, LabelColor);
            tmp.SetText(text);
            return tmp;
        }

        private static TMP_Text CreateValueText(RectTransform parent, string name, string text, TMP_FontAsset font, float width)
        {
            GameObject value = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            value.transform.SetParent(parent, false);

            LayoutElement layout = value.GetComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 40f;

            TextMeshProUGUI tmp = value.GetComponent<TextMeshProUGUI>();
            ConfigureText(tmp, font, 20f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, ValueColor);
            tmp.SetText(text);
            return tmp;
        }

        private static Button CreateButton(RectTransform parent, string name, string text, Color color, TMP_FontAsset font, float preferredWidth = 0f)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = color;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 52f;
            layout.flexibleWidth = preferredWidth <= 0f ? 1f : 0f;
            if (preferredWidth > 0f)
            {
                layout.preferredWidth = preferredWidth;
            }

            RectTransform textRect = EnsureChildRect(buttonObject.transform, "Text");
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 6f);
            textRect.offsetMax = new Vector2(-10f, -6f);

            TextMeshProUGUI tmp = textRect.GetComponent<TextMeshProUGUI>() ?? textRect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(tmp, font, 18f, FontStyles.Bold, TextAlignmentOptions.Midline, ValueColor);
            tmp.SetText(text);
            tmp.raycastTarget = false;
            return button;
        }

        private static Toggle CreateToggle(RectTransform parent, string name, string labelText, TMP_FontAsset font)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup), typeof(Toggle));
            root.transform.SetParent(parent, false);

            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = 210f;
            layout.preferredHeight = 34f;

            HorizontalLayoutGroup group = root.GetComponent<HorizontalLayoutGroup>();
            group.spacing = 10f;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = false;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            RectTransform backgroundRect = EnsureChildRect(root.transform, "Background");
            LayoutElement backgroundLayout = backgroundRect.GetComponent<LayoutElement>() ?? backgroundRect.gameObject.AddComponent<LayoutElement>();
            backgroundLayout.preferredWidth = 24f;
            backgroundLayout.preferredHeight = 24f;

            Image backgroundImage = backgroundRect.GetComponent<Image>() ?? backgroundRect.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.15f, 0.22f, 0.28f, 1f);

            RectTransform checkmarkRect = EnsureChildRect(backgroundRect, "Checkmark");
            checkmarkRect.anchorMin = new Vector2(0.15f, 0.15f);
            checkmarkRect.anchorMax = new Vector2(0.85f, 0.85f);
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;

            Image checkmarkImage = checkmarkRect.GetComponent<Image>() ?? checkmarkRect.gameObject.AddComponent<Image>();
            checkmarkImage.color = ToggleOnColor;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(root.transform, false);

            LayoutElement labelLayout = labelObject.GetComponent<LayoutElement>();
            labelLayout.preferredWidth = 172f;
            labelLayout.preferredHeight = 30f;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            ConfigureText(label, font, 16f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, LabelColor);
            label.SetText(labelText);

            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            toggle.isOn = false;
            return toggle;
        }

        private static Slider CreateSlider(RectTransform parent, string name, float minValue, float maxValue, float initialValue)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Slider));
            root.transform.SetParent(parent, false);

            LayoutElement rootLayout = root.GetComponent<LayoutElement>();
            rootLayout.preferredHeight = 28f;
            rootLayout.flexibleWidth = 1f;
            rootLayout.minWidth = 260f;

            Slider slider = root.GetComponent<Slider>();
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.value = initialValue;
            slider.wholeNumbers = false;

            RectTransform backgroundRect = EnsureChildRect(root.transform, "Background");
            backgroundRect.anchorMin = new Vector2(0f, 0.25f);
            backgroundRect.anchorMax = new Vector2(1f, 0.75f);
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            Image backgroundImage = backgroundRect.GetComponent<Image>() ?? backgroundRect.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.16f, 0.20f, 0.24f, 1f);

            RectTransform fillArea = EnsureChildRect(root.transform, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0f);
            fillArea.anchorMax = new Vector2(1f, 1f);
            fillArea.offsetMin = new Vector2(8f, 4f);
            fillArea.offsetMax = new Vector2(-16f, -4f);

            RectTransform fillRect = EnsureChildRect(fillArea, "Fill");
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fillRect.GetComponent<Image>() ?? fillRect.gameObject.AddComponent<Image>();
            fillImage.color = AccentColor;

            RectTransform handleArea = EnsureChildRect(root.transform, "Handle Slide Area");
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(8f, 0f);
            handleArea.offsetMax = new Vector2(-8f, 0f);

            RectTransform handleRect = EnsureChildRect(handleArea, "Handle");
            handleRect.sizeDelta = new Vector2(20f, 28f);

            Image handleImage = handleRect.GetComponent<Image>() ?? handleRect.gameObject.AddComponent<Image>();
            handleImage.color = ValueColor;

            slider.targetGraphic = handleImage;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static void ConfigureText(
            TextMeshProUGUI text,
            TMP_FontAsset font,
            float size,
            FontStyles style,
            TextAlignmentOptions alignment,
            Color color)
        {
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
        }

        private static RectTransform EnsureChildRect(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing as RectTransform;
            }

            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static GameObject FindRequired(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                throw new MissingReferenceException("Required child not found: " + name);
            }

            return child.gameObject;
        }

        private static Object FindComponentByTypeName(string gameObjectName, string fullTypeName)
        {
            GameObject target = FindLoadedSceneGameObject(gameObjectName);
            if (target == null)
            {
                throw new MissingReferenceException("Required GameObject not found: " + gameObjectName);
            }

            ComponentScratch.Clear();
            target.GetComponents(ComponentScratch);
            Object result = null;
            for (int i = 0; i < ComponentScratch.Count; i++)
            {
                Component component = ComponentScratch[i];
                if (component != null && component.GetType().FullName == fullTypeName)
                {
                    result = component;
                    break;
                }
            }

            ComponentScratch.Clear();
            if (result != null)
                return result;

            throw new MissingReferenceException("Required component not found: " + fullTypeName);
        }

        private static GameObject FindLoadedSceneGameObject(string gameObjectName)
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                SceneRootScratch.Clear();
                scene.GetRootGameObjects(SceneRootScratch);
                for (int rootIndex = 0; rootIndex < SceneRootScratch.Count; rootIndex++)
                {
                    GameObject root = SceneRootScratch[rootIndex];
                    if (root == null)
                        continue;

                    Transform found = FindDeepChild(root.transform, gameObjectName);
                    if (found != null)
                    {
                        SceneRootScratch.Clear();
                        return found.gameObject;
                    }
                }
            }

            SceneRootScratch.Clear();
            return null;
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == childName)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindDeepChild(parent.GetChild(i), childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static Button FindButtonByLabelText(Transform scope, string labelText)
        {
            if (scope == null || string.IsNullOrEmpty(labelText))
            {
                return null;
            }

            Button[] buttons = scope.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null && text.text == labelText)
                {
                    return button;
                }
            }

            return null;
        }

        private static Button EnsureSaveLoadBackButton(Transform root, TMP_FontAsset font)
        {
            Transform saveLoadPanel = FindDeepChild(root, "Panel_Sideload Popup");
            Transform windowFrame = FindDeepChild(root, "Window_Frame");
            if (saveLoadPanel == null || windowFrame == null)
            {
                return null;
            }

            Transform buttonTransform = FindDeepChild(saveLoadPanel, "BTN_Back (\"RETURN\")");
            RectTransform rectTransform;
            if (buttonTransform == null)
            {
                GameObject buttonObject = new GameObject("BTN_Back (\"RETURN\")", typeof(RectTransform));
                buttonObject.transform.SetParent(windowFrame, false);
                rectTransform = buttonObject.GetComponent<RectTransform>();
            }
            else
            {
                rectTransform = buttonTransform.GetComponent<RectTransform>();
            }

            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(1f, 0f);
            rectTransform.anchoredPosition = new Vector2(-72f, 64f);
            rectTransform.sizeDelta = new Vector2(240f, 64f);

            Image image = rectTransform.GetComponent<Image>() ?? rectTransform.gameObject.AddComponent<Image>();
            Button button = rectTransform.GetComponent<Button>() ?? rectTransform.gameObject.AddComponent<Button>();
            image.color = new Color(0.16f, 0.28f, 0.40f, 1f);
            image.raycastTarget = true;
            button.targetGraphic = image;

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;

            Transform textTransform = rectTransform.Find("Text (TMP)");
            if (textTransform == null)
            {
                GameObject textObject = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(rectTransform, false);
                textTransform = textObject.transform;
            }

            RectTransform textRect = textTransform.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 10f);
            textRect.offsetMax = new Vector2(-16f, -10f);

            TextMeshProUGUI text = textTransform.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 20f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = ValueColor;
            text.raycastTarget = false;
            text.text = "RETURN";

            EditorUtility.SetDirty(rectTransform.gameObject);
            return button;
        }

        private static void EnsureLoadingScreenUI(Transform root, TMP_FontAsset font)
        {
            RectTransform loadingPanel = FindDeepChild(root, "Panel_LoadingScreen") as RectTransform;
            if (loadingPanel == null)
            {
                return;
            }

            Image panelImage = loadingPanel.GetComponent<Image>() ?? loadingPanel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 1f);
            panelImage.raycastTarget = true;

            RectTransform titleRect = EnsureChildRect(loadingPanel, "Text (TMP)");
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 96f);
            titleRect.sizeDelta = new Vector2(1080f, 60f);

            TextMeshProUGUI titleText = titleRect.GetComponent<TextMeshProUGUI>() ?? titleRect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(titleText, font, 28f, FontStyles.Bold, TextAlignmentOptions.Center, ValueColor);
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            titleText.SetText("SYNCHRONIZING ENVIRONMENT...");

            Slider progressSlider = EnsureSlider(loadingPanel, "Slider_LoadingProgress", 0f, 1f, 0f);
            RectTransform progressRect = progressSlider.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.5f, 0.5f);
            progressRect.anchorMax = new Vector2(0.5f, 0.5f);
            progressRect.pivot = new Vector2(0.5f, 0.5f);
            progressRect.anchoredPosition = new Vector2(0f, 18f);
            progressRect.sizeDelta = new Vector2(880f, 30f);

            RectTransform percentRect = EnsureChildRect(loadingPanel, "Text_LoadingPercent");
            percentRect.anchorMin = new Vector2(0.5f, 0.5f);
            percentRect.anchorMax = new Vector2(0.5f, 0.5f);
            percentRect.pivot = new Vector2(0.5f, 0.5f);
            percentRect.anchoredPosition = new Vector2(0f, -34f);
            percentRect.sizeDelta = new Vector2(240f, 44f);

            TextMeshProUGUI percentText = percentRect.GetComponent<TextMeshProUGUI>() ?? percentRect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(percentText, font, 24f, FontStyles.Bold, TextAlignmentOptions.Center, AccentColor);
            percentText.textWrappingMode = TextWrappingModes.NoWrap;
            percentText.SetText("0%");

            RectTransform tipsGroupRect = EnsureChildRect(loadingPanel, "Group_LoadingTips");
            tipsGroupRect.anchorMin = new Vector2(0.5f, 0.5f);
            tipsGroupRect.anchorMax = new Vector2(0.5f, 0.5f);
            tipsGroupRect.pivot = new Vector2(0.5f, 0.5f);
            tipsGroupRect.anchoredPosition = new Vector2(0f, -162f);
            tipsGroupRect.sizeDelta = new Vector2(1080f, 120f);

            CanvasGroup tipsCanvasGroup = tipsGroupRect.GetComponent<CanvasGroup>();
            if (tipsCanvasGroup == null)
            {
                tipsCanvasGroup = tipsGroupRect.gameObject.AddComponent<CanvasGroup>();
            }

            if (tipsCanvasGroup == null)
            {
                throw new MissingComponentException("Failed to add CanvasGroup to Group_LoadingTips.");
            }

            tipsCanvasGroup.alpha = 0f;
            tipsCanvasGroup.interactable = false;
            tipsCanvasGroup.blocksRaycasts = false;

            RectTransform tipTextRect = EnsureChildRect(tipsGroupRect, "Text_Tip");
            tipTextRect.anchorMin = new Vector2(0f, 0f);
            tipTextRect.anchorMax = new Vector2(1f, 1f);
            tipTextRect.offsetMin = new Vector2(48f, 0f);
            tipTextRect.offsetMax = new Vector2(-48f, 0f);

            TextMeshProUGUI tipText = tipTextRect.GetComponent<TextMeshProUGUI>() ?? tipTextRect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(tipText, font, 19f, FontStyles.Normal, TextAlignmentOptions.Center, LabelColor);
            tipText.textWrappingMode = TextWrappingModes.Normal;
            tipText.SetText("Scan unknown objects to unlock blueprints and research data.");

            LoadingTipsDisplay loadingTips = tipsGroupRect.GetComponent<LoadingTipsDisplay>() ?? tipsGroupRect.gameObject.AddComponent<LoadingTipsDisplay>();
            SerializedObject loadingTipsObject = new SerializedObject(loadingTips);
            loadingTipsObject.FindProperty("tipText").objectReferenceValue = tipText;
            loadingTipsObject.FindProperty("tipCanvasGroup").objectReferenceValue = tipsCanvasGroup;
            loadingTipsObject.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(loadingPanel.gameObject);
            EditorUtility.SetDirty(tipsGroupRect.gameObject);
        }

        private static Slider EnsureSlider(RectTransform parent, string name, float minValue, float maxValue, float initialValue)
        {
            RectTransform root = EnsureChildRect(parent, name);
            Slider slider = root.GetComponent<Slider>() ?? root.gameObject.AddComponent<Slider>();

            LayoutElement rootLayout = root.GetComponent<LayoutElement>() ?? root.gameObject.AddComponent<LayoutElement>();
            rootLayout.preferredHeight = 28f;
            rootLayout.flexibleWidth = 1f;
            rootLayout.minWidth = 260f;

            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.value = initialValue;
            slider.wholeNumbers = false;

            RectTransform backgroundRect = EnsureChildRect(root, "Background");
            backgroundRect.anchorMin = new Vector2(0f, 0.25f);
            backgroundRect.anchorMax = new Vector2(1f, 0.75f);
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            Image backgroundImage = backgroundRect.GetComponent<Image>() ?? backgroundRect.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.16f, 0.20f, 0.24f, 1f);

            RectTransform fillArea = EnsureChildRect(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0f);
            fillArea.anchorMax = new Vector2(1f, 1f);
            fillArea.offsetMin = new Vector2(8f, 4f);
            fillArea.offsetMax = new Vector2(-16f, -4f);

            RectTransform fillRect = EnsureChildRect(fillArea, "Fill");
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fillRect.GetComponent<Image>() ?? fillRect.gameObject.AddComponent<Image>();
            fillImage.color = AccentColor;

            RectTransform handleArea = EnsureChildRect(root, "Handle Slide Area");
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(8f, 0f);
            handleArea.offsetMax = new Vector2(-8f, 0f);

            RectTransform handleRect = EnsureChildRect(handleArea, "Handle");
            handleRect.sizeDelta = new Vector2(20f, 28f);

            Image handleImage = handleRect.GetComponent<Image>() ?? handleRect.gameObject.AddComponent<Image>();
            handleImage.color = ValueColor;

            slider.targetGraphic = handleImage;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static void WireSettingsManager(SettingsManager manager, Camera camera, Object volume, AudioMixer mixer)
        {
            SerializedObject serializedObject = new SerializedObject(manager);
            serializedObject.FindProperty("audioMixer").objectReferenceValue = mixer;
            serializedObject.FindProperty("mainCamera").objectReferenceValue = camera;
            serializedObject.FindProperty("urpVolume").objectReferenceValue = volume;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        private static void WireSettingsLivePreview(SettingsLivePreview livePreview, Camera camera, Object volume)
        {
            SerializedObject serializedObject = new SerializedObject(livePreview);
            serializedObject.FindProperty("mainCamera").objectReferenceValue = camera;
            serializedObject.FindProperty("urpVolume").objectReferenceValue = volume;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(livePreview);
        }

        private static void WireMainMenuController(
            MainMenuController controller,
            Transform root,
            Button btnBackFromSaveLoad,
            Button btnBackFromSettings)
        {
            CanvasGroup mainMenuGroup = FindDeepChild(root, "Panel_MainMenu")?.GetComponent<CanvasGroup>();
            CanvasGroup saveLoadGroup = FindDeepChild(root, "Panel_Sideload Popup")?.GetComponent<CanvasGroup>();
            CanvasGroup settingsGroup = FindDeepChild(root, "Panel_Settings")?.GetComponent<CanvasGroup>();
            CanvasGroup loadingGroup = FindDeepChild(root, "Panel_LoadingScreen")?.GetComponent<CanvasGroup>();
            Transform slotsContainer = FindDeepChild(root, "ScrollView_Slots");
            Button btnNewGame = FindDeepChild(root, "BTN_Start")?.GetComponent<Button>();
            Button btnLoadGame = FindDeepChild(root, "BTN_ResumeLog")?.GetComponent<Button>();
            Button btnSettings = FindDeepChild(root, "BTN_Settings")?.GetComponent<Button>();
            Button btnQuit = FindDeepChild(root, "BTN_Abort")?.GetComponent<Button>();
            if (btnBackFromSaveLoad == null && saveLoadGroup != null)
            {
                btnBackFromSaveLoad = FindButtonByLabelText(saveLoadGroup.transform, "RETURN");
            }
            Slider loadingProgressBar = FindDeepChild(root, "Slider_LoadingProgress")?.GetComponent<Slider>();
            TMP_Text loadingPercentText = FindDeepChild(root, "Text_LoadingPercent")?.GetComponent<TMP_Text>();
            LoadingTipsDisplay loadingTips = FindDeepChild(root, "Group_LoadingTips")?.GetComponent<LoadingTipsDisplay>();

            SerializedObject serializedObject = new SerializedObject(controller);
            serializedObject.FindProperty("mainMenuGroup").objectReferenceValue = mainMenuGroup;
            serializedObject.FindProperty("saveLoadGroup").objectReferenceValue = saveLoadGroup;
            serializedObject.FindProperty("settingsGroup").objectReferenceValue = settingsGroup;
            serializedObject.FindProperty("loadingGroup").objectReferenceValue = loadingGroup;
            serializedObject.FindProperty("slotsContainer").objectReferenceValue = slotsContainer;
            serializedObject.FindProperty("btnNewGame").objectReferenceValue = btnNewGame;
            serializedObject.FindProperty("btnLoadGame").objectReferenceValue = btnLoadGame;
            serializedObject.FindProperty("btnSettings").objectReferenceValue = btnSettings;
            serializedObject.FindProperty("btnQuit").objectReferenceValue = btnQuit;
            serializedObject.FindProperty("labelNewGame").objectReferenceValue = btnNewGame != null ? btnNewGame.GetComponentInChildren<TMP_Text>(true) : null;
            serializedObject.FindProperty("labelLoadGame").objectReferenceValue = btnLoadGame != null ? btnLoadGame.GetComponentInChildren<TMP_Text>(true) : null;
            serializedObject.FindProperty("labelSettings").objectReferenceValue = btnSettings != null ? btnSettings.GetComponentInChildren<TMP_Text>(true) : null;
            serializedObject.FindProperty("labelQuit").objectReferenceValue = btnQuit != null ? btnQuit.GetComponentInChildren<TMP_Text>(true) : null;
            serializedObject.FindProperty("btnBackFromSaveLoad").objectReferenceValue = btnBackFromSaveLoad;
            serializedObject.FindProperty("btnBackFromSettings").objectReferenceValue = btnBackFromSettings;
            serializedObject.FindProperty("loadingProgressBar").objectReferenceValue = loadingProgressBar;
            serializedObject.FindProperty("loadingPercentText").objectReferenceValue = loadingPercentText;
            serializedObject.FindProperty("loadingTips").objectReferenceValue = loadingTips;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void WireSettingsPanel(
            SettingsPanel settingsPanel,
            Button btnPresetLow,
            Button btnPresetMedium,
            Button btnPresetHigh,
            Button btnPresetUltra,
            Button btnQualityDecrease,
            Button btnQualityIncrease,
            TMP_Text txtQualityLevel,
            Toggle toggleVsync,
            Toggle toggleFullscreen,
            Slider sliderFieldOfView,
            TMP_Text txtFieldOfView,
            Button btnShadowQualityDecrease,
            Button btnShadowQualityIncrease,
            TMP_Text txtShadowQuality,
            Slider sliderShadowDistance,
            TMP_Text txtShadowDistance,
            Button btnAntiAliasingDecrease,
            Button btnAntiAliasingIncrease,
            TMP_Text txtAntiAliasing,
            Toggle toggleAmbientOcclusion,
            Toggle toggleBloom,
            Toggle toggleMotionBlur,
            Button btnTextureQualityDecrease,
            Button btnTextureQualityIncrease,
            TMP_Text txtTextureQuality,
            Slider sliderMasterVolume,
            Slider sliderMusicVolume,
            Slider sliderSfxVolume,
            Slider sliderAmbientVolume,
            TMP_Text txtMasterVolume,
            TMP_Text txtMusicVolume,
            TMP_Text txtSfxVolume,
            TMP_Text txtAmbientVolume,
            Button btnResetDefaults,
            Button btnApply,
            Button btnCancel,
            SettingsLivePreview livePreview)
        {
            SerializedObject serializedObject = new SerializedObject(settingsPanel);
            serializedObject.FindProperty("btnPresetLow").objectReferenceValue = btnPresetLow;
            serializedObject.FindProperty("btnPresetMedium").objectReferenceValue = btnPresetMedium;
            serializedObject.FindProperty("btnPresetHigh").objectReferenceValue = btnPresetHigh;
            serializedObject.FindProperty("btnPresetUltra").objectReferenceValue = btnPresetUltra;
            serializedObject.FindProperty("btnQualityDecrease").objectReferenceValue = btnQualityDecrease;
            serializedObject.FindProperty("btnQualityIncrease").objectReferenceValue = btnQualityIncrease;
            serializedObject.FindProperty("txtQualityLevel").objectReferenceValue = txtQualityLevel;
            serializedObject.FindProperty("toggleVsync").objectReferenceValue = toggleVsync;
            serializedObject.FindProperty("toggleFullscreen").objectReferenceValue = toggleFullscreen;
            serializedObject.FindProperty("sliderFieldOfView").objectReferenceValue = sliderFieldOfView;
            serializedObject.FindProperty("txtFieldOfView").objectReferenceValue = txtFieldOfView;
            serializedObject.FindProperty("btnShadowQualityDecrease").objectReferenceValue = btnShadowQualityDecrease;
            serializedObject.FindProperty("btnShadowQualityIncrease").objectReferenceValue = btnShadowQualityIncrease;
            serializedObject.FindProperty("txtShadowQuality").objectReferenceValue = txtShadowQuality;
            serializedObject.FindProperty("sliderShadowDistance").objectReferenceValue = sliderShadowDistance;
            serializedObject.FindProperty("txtShadowDistance").objectReferenceValue = txtShadowDistance;
            serializedObject.FindProperty("btnAntiAliasingDecrease").objectReferenceValue = btnAntiAliasingDecrease;
            serializedObject.FindProperty("btnAntiAliasingIncrease").objectReferenceValue = btnAntiAliasingIncrease;
            serializedObject.FindProperty("txtAntiAliasing").objectReferenceValue = txtAntiAliasing;
            serializedObject.FindProperty("toggleAmbientOcclusion").objectReferenceValue = toggleAmbientOcclusion;
            serializedObject.FindProperty("toggleBloom").objectReferenceValue = toggleBloom;
            serializedObject.FindProperty("toggleMotionBlur").objectReferenceValue = toggleMotionBlur;
            serializedObject.FindProperty("btnTextureQualityDecrease").objectReferenceValue = btnTextureQualityDecrease;
            serializedObject.FindProperty("btnTextureQualityIncrease").objectReferenceValue = btnTextureQualityIncrease;
            serializedObject.FindProperty("txtTextureQuality").objectReferenceValue = txtTextureQuality;
            serializedObject.FindProperty("sliderMasterVolume").objectReferenceValue = sliderMasterVolume;
            serializedObject.FindProperty("sliderMusicVolume").objectReferenceValue = sliderMusicVolume;
            serializedObject.FindProperty("sliderSfxVolume").objectReferenceValue = sliderSfxVolume;
            serializedObject.FindProperty("sliderAmbientVolume").objectReferenceValue = sliderAmbientVolume;
            serializedObject.FindProperty("txtMasterVolume").objectReferenceValue = txtMasterVolume;
            serializedObject.FindProperty("txtMusicVolume").objectReferenceValue = txtMusicVolume;
            serializedObject.FindProperty("txtSfxVolume").objectReferenceValue = txtSfxVolume;
            serializedObject.FindProperty("txtAmbientVolume").objectReferenceValue = txtAmbientVolume;
            serializedObject.FindProperty("btnResetDefaults").objectReferenceValue = btnResetDefaults;
            serializedObject.FindProperty("btnApply").objectReferenceValue = btnApply;
            serializedObject.FindProperty("btnCancel").objectReferenceValue = btnCancel;
            serializedObject.FindProperty("livePreview").objectReferenceValue = livePreview;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settingsPanel);
        }
    }
}
