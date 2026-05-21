#if UNITY_EDITOR
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.AITextureControlMaps
{
    public sealed class AITextureForgeWindow : EditorWindow
    {
        private ObjectField _meshFolderField;
        private ObjectField _previewTargetField;
        private Toggle _normalToggle;
        private Toggle _depthToggle;
        private Toggle _colorIdToggle;
        private Toggle _curvatureToggle;
        private Toggle _previewToggle;
        private IntegerField _resolutionField;
        private Slider _qualitySlider;
        private EnumField _previewPassField;
        private ProgressBar _progressBar;
        private Label _statusLabel;

        [MenuItem("HECTON-8/AI Texture Control Maps/AI Control Map Forge", false, 2670)]
        private static void Open()
        {
            GetWindow<AITextureForgeWindow>("AI Control Map Forge");
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _statusLabel = new Label("PENDING VERIFICATION");
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(_statusLabel);

            _meshFolderField = new ObjectField("Mesh Folder")
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false
            };
            rootVisualElement.Add(_meshFolderField);

            VisualElement passRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            _normalToggle = CreatePassToggle("Normal", true);
            _depthToggle = CreatePassToggle("Depth", true);
            _colorIdToggle = CreatePassToggle("ColorID", true);
            _curvatureToggle = CreatePassToggle("Curvature", true);
            passRow.Add(_normalToggle);
            passRow.Add(_depthToggle);
            passRow.Add(_colorIdToggle);
            passRow.Add(_curvatureToggle);
            rootVisualElement.Add(passRow);

            _resolutionField = new IntegerField("Resolution");
            _resolutionField.value = AITextureControlMapConstants.DefaultBakeResolution;
            rootVisualElement.Add(_resolutionField);

            _qualitySlider = new Slider("GlobalQualityWeight", 0.0f, 1.0f);
            _qualitySlider.value = 1.0f;
            rootVisualElement.Add(_qualitySlider);

            _progressBar = new ProgressBar
            {
                title = "GPU readback idle",
                lowValue = 0.0f,
                highValue = 1.0f,
                value = 0.0f
            };
            _progressBar.style.height = 22;
            _progressBar.style.marginTop = 8;
            rootVisualElement.Add(_progressBar);

            Button csvButton = new Button(LoadCsvProfile) { text = "LOAD CSV PROFILE" };
            Button bakeButton = new Button(RunBake) { text = "GENERATE AI TEMPLATES" };
            Button inboxButton = new Button(AITextureIngestionWatcher.ProcessInboxNow) { text = "PROCESS AI INBOX" };
            Button scanButton = new Button(Material_Setup_Scanner.RunScan) { text = "SCAN MATERIAL SETUP" };
            Button auditButton = new Button(AITextureControlMapSelfAudit.RunSelfAuditMenu) { text = "RUN SELF AUDIT" };
            rootVisualElement.Add(csvButton);
            rootVisualElement.Add(bakeButton);
            rootVisualElement.Add(inboxButton);
            rootVisualElement.Add(scanButton);
            rootVisualElement.Add(auditButton);

            _previewTargetField = new ObjectField("Preview Target")
            {
                objectType = typeof(Object),
                allowSceneObjects = true
            };
            _previewPassField = new EnumField("Preview Pass", AITextureControlPass.Curvature);
            _previewToggle = new Toggle("Scene Preview Enabled");
            _previewTargetField.RegisterValueChangedCallback(_ => RefreshPreview());
            _previewPassField.RegisterValueChangedCallback(_ => RefreshPreview());
            _previewToggle.RegisterValueChangedCallback(_ => RefreshPreview());
            _qualitySlider.RegisterValueChangedCallback(_ => RefreshPreview());
            rootVisualElement.Add(_previewTargetField);
            rootVisualElement.Add(_previewPassField);
            rootVisualElement.Add(_previewToggle);
        }

        private void OnDisable()
        {
            AITextureLiveMapPreview.SetPreview(null, AITextureControlPass.Curvature, false);
        }

        private static Toggle CreatePassToggle(string label, bool value)
        {
            Toggle toggle = new Toggle(label);
            toggle.value = value;
            toggle.style.marginRight = 12;
            return toggle;
        }

        private void LoadCsvProfile()
        {
            AITextureBakeSettings settings = AITextureProfileCsv.LoadFirstSettingsOrDefault();
            _resolutionField.value = settings.Resolution;
            _qualitySlider.value = math.saturate(settings.GlobalQualityWeight);
            _normalToggle.value = (settings.PassMask & AITexturePassMask.Normal) != (AITexturePassMask)0;
            _depthToggle.value = (settings.PassMask & AITexturePassMask.Depth) != (AITexturePassMask)0;
            _colorIdToggle.value = (settings.PassMask & AITexturePassMask.ColorId) != (AITexturePassMask)0;
            _curvatureToggle.value = (settings.PassMask & AITexturePassMask.Curvature) != (AITexturePassMask)0;
            _statusLabel.text = "Loaded " + settings.ProfileName.ToString();
        }

        private void RunBake()
        {
            Object folder = _meshFolderField.value;
            string folderPath = folder != null ? AssetDatabase.GetAssetPath(folder) : string.Empty;
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                _statusLabel.text = "Invalid mesh folder";
                return;
            }

            AITextureBakeSettings settings = BuildSettings();
            _progressBar.value = 0.0f;
            _progressBar.title = "Queued";
            _statusLabel.text = "Baking " + folderPath;
            AITextureControlMapBaker.BakeFolder(folderPath, settings, OnBakeProgress);
        }

        private AITextureBakeSettings BuildSettings()
        {
            AITextureBakeSettings settings = AITextureProfileCsv.LoadFirstSettingsOrDefault();
            settings.ProfileName = new FixedString64Bytes("Forge_Custom");
            settings.PassMask = (AITexturePassMask)0;
            if (_normalToggle.value)
                settings.PassMask |= AITexturePassMask.Normal;
            if (_depthToggle.value)
                settings.PassMask |= AITexturePassMask.Depth;
            if (_colorIdToggle.value)
                settings.PassMask |= AITexturePassMask.ColorId;
            if (_curvatureToggle.value)
                settings.PassMask |= AITexturePassMask.Curvature;
            settings.Resolution = math.clamp(_resolutionField.value, 64, AITextureControlMapConstants.HeroBakeResolution);
            settings.GlobalQualityWeight = math.saturate(_qualitySlider.value);
            return settings;
        }

        private void OnBakeProgress(string label, float value)
        {
            _progressBar.value = math.saturate(value);
            _progressBar.title = label;
            _statusLabel.text = value >= 1.0f ? "Batch report written" : "GPU readback " + ((int)(value * 100.0f)).ToString() + "%";
        }

        private void RefreshPreview()
        {
            AITextureControlPass pass = (AITextureControlPass)(object)_previewPassField.value;
            AITextureLiveMapPreview.SetPreview(_previewTargetField.value, pass, _previewToggle.value, math.saturate(_qualitySlider.value));
        }
    }
}
#endif
