#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Rendering.Editor
{
    public sealed class AbyssalCausticsTunerWindow : EditorWindow
    {
        private const string DefaultProfilesPath = "Assets/_Project/Data/Rendering/caustic_lighting_profiles.csv";
        private const string RuntimeActiveLabel = "Runtime: active";
        private const string RuntimeOfflineLabel = "Runtime: offline";
        private const string DepthUnavailableLabel = "Max depth: n/a";
        private const string QualityUnavailableLabel = "Quality: n/a";
        private const string ProfilesIdleLabel = "Profiles: idle";
        private const string ProfilesLoadedLabel = "Profiles: loaded";
        private const string ProfilesFailedLabel = "Profiles: failed";
        private const string DepthPrefix = "Max depth: ";
        private const string DepthSuffix = " m";
        private const string QualityPrefix = "Quality: ";
        private const int DepthTenthsCacheCount = 1801;
        private const int QualityMillisCacheCount = 1001;
        private const int UnsetReadout = -1;

        private static readonly string[] s_depthLabels = BuildDepthLabels();
        private static readonly string[] s_qualityLabels = BuildQualityLabels();

        private Slider _chromaticSlider;
        private Slider _noiseScaleSlider;
        private Slider _flowSpeedSlider;
        private Slider _maxDepthSlider;
        private TextField _csvPathField;
        private Label _statusLabel;
        private Label _depthLabel;
        private Label _qualityLabel;
        private Label _csvStatusLabel;
        private bool _readoutInitialized;
        private bool _lastHasParameters;
        private int _lastDepthTenths = UnsetReadout;
        private int _lastQualityMillis = UnsetReadout;

        [MenuItem("HECTON-8/Rendering/Abyssal Caustics Tuner")]
        private static void Open()
        {
            AbyssalCausticsTunerWindow window = GetWindow<AbyssalCausticsTunerWindow>();
            window.titleContent = new GUIContent("Abyssal Caustics");
            window.minSize = new Vector2(320f, 220f);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _statusLabel = new Label(RuntimeOfflineLabel);
            _depthLabel = new Label(DepthUnavailableLabel);
            _qualityLabel = new Label(QualityUnavailableLabel);
            root.Add(_statusLabel);
            root.Add(_depthLabel);
            root.Add(_qualityLabel);

            _chromaticSlider = CreateSlider("Chromatic Dispersion", 0f, 1f);
            _noiseScaleSlider = CreateSlider("Noise Scale", 0.005f, 0.25f);
            _flowSpeedSlider = CreateSlider("Flow Speed Multiplier", 0f, 4f);
            _maxDepthSlider = CreateSlider("Maximum Depth", 1f, 180f);
            root.Add(_chromaticSlider);
            root.Add(_noiseScaleSlider);
            root.Add(_flowSpeedSlider);
            root.Add(_maxDepthSlider);

            _csvPathField = new TextField("Profiles CSV");
            _csvPathField.value = DefaultProfilesPath;
            Button loadProfilesButton = new Button(OnLoadProfilesClicked) { text = "Load Profiles CSV" };
            _csvStatusLabel = new Label(ProfilesIdleLabel);
            root.Add(_csvPathField);
            root.Add(loadProfilesButton);
            root.Add(_csvStatusLabel);

            _chromaticSlider.RegisterValueChangedCallback(OnSliderChanged);
            _noiseScaleSlider.RegisterValueChangedCallback(OnSliderChanged);
            _flowSpeedSlider.RegisterValueChangedCallback(OnSliderChanged);
            _maxDepthSlider.RegisterValueChangedCallback(OnSliderChanged);
            RefreshFromRuntime();
        }

        private void OnInspectorUpdate()
        {
            RefreshReadout();
        }

        private static Slider CreateSlider(string label, float lowValue, float highValue)
        {
            Slider slider = new Slider(label, lowValue, highValue);
            slider.showInputField = true;
            return slider;
        }

        private void RefreshFromRuntime()
        {
            if (!AbyssalDeferredCausticsRuntime.TryGetTuning(out CausticsTuningDTO tuning))
                return;

            _noiseScaleSlider.SetValueWithoutNotify(tuning.ScaleFlowDepthIntensity.x);
            _flowSpeedSlider.SetValueWithoutNotify(tuning.ScaleFlowDepthIntensity.y);
            _maxDepthSlider.SetValueWithoutNotify(tuning.ScaleFlowDepthIntensity.z);
            _chromaticSlider.SetValueWithoutNotify(tuning.DispersionSdfTileProfile.x);
            RefreshReadout();
        }

        private void RefreshReadout()
        {
            bool hasParameters = AbyssalDeferredCausticsRuntime.TryGetActiveParameters(out CausticsParametersDTO parameters);
            if (!_readoutInitialized || _lastHasParameters != hasParameters)
            {
                _statusLabel.text = hasParameters ? RuntimeActiveLabel : RuntimeOfflineLabel;
                _lastHasParameters = hasParameters;
                _readoutInitialized = true;

                if (!hasParameters)
                {
                    _depthLabel.text = DepthUnavailableLabel;
                    _qualityLabel.text = QualityUnavailableLabel;
                    _lastDepthTenths = UnsetReadout;
                    _lastQualityMillis = UnsetReadout;
                    return;
                }
            }

            if (!hasParameters)
                return;

            int depthTenths = Mathf.Clamp(
                Mathf.RoundToInt(parameters.IntensityAndDepthFalloff.z * 10f),
                0,
                DepthTenthsCacheCount - 1);
            if (_lastDepthTenths != depthTenths)
            {
                _depthLabel.text = s_depthLabels[depthTenths];
                _lastDepthTenths = depthTenths;
            }

            int qualityMillis = Mathf.Clamp(
                Mathf.RoundToInt(parameters.QualityAndColor.x * 1000f),
                0,
                QualityMillisCacheCount - 1);
            if (_lastQualityMillis != qualityMillis)
            {
                _qualityLabel.text = s_qualityLabels[qualityMillis];
                _lastQualityMillis = qualityMillis;
            }
        }

        private void OnSliderChanged(ChangeEvent<float> evt)
        {
            AbyssalDeferredCausticsRuntime.TrySetEditorTuning(
                _chromaticSlider.value,
                _noiseScaleSlider.value,
                _flowSpeedSlider.value,
                _maxDepthSlider.value);
            RefreshReadout();
        }

        private void OnLoadProfilesClicked()
        {
            string projectRelativePath = string.IsNullOrEmpty(_csvPathField.value) ? DefaultProfilesPath : _csvPathField.value;
            bool loaded = AbyssalDeferredCausticsRuntime.TryLoadLightingProfilesCsv(projectRelativePath);
            _csvStatusLabel.text = loaded ? ProfilesLoadedLabel : ProfilesFailedLabel;
            RefreshReadout();
        }

        private static string[] BuildDepthLabels()
        {
            string[] labels = new string[DepthTenthsCacheCount];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = CreateDepthLabel(i);
            return labels;
        }

        private static string[] BuildQualityLabels()
        {
            string[] labels = new string[QualityMillisCacheCount];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = CreateQualityLabel(i);
            return labels;
        }

        private static string CreateDepthLabel(int tenths)
        {
            int whole = tenths / 10;
            int fraction = tenths - whole * 10;
            int wholeDigits = CountDigits(whole);
            char[] characters = new char[DepthPrefix.Length + wholeDigits + 2 + DepthSuffix.Length];
            int offset = CopyLiteral(DepthPrefix, characters, 0);
            offset = WriteUnsigned(whole, characters, offset, wholeDigits);
            characters[offset++] = '.';
            characters[offset++] = (char)('0' + fraction);
            CopyLiteral(DepthSuffix, characters, offset);
            return new string(characters);
        }

        private static string CreateQualityLabel(int millis)
        {
            int whole = millis / 1000;
            int fraction = millis - whole * 1000;
            char[] characters = new char[QualityPrefix.Length + 5];
            int offset = CopyLiteral(QualityPrefix, characters, 0);
            characters[offset++] = (char)('0' + whole);
            characters[offset++] = '.';
            characters[offset++] = (char)('0' + fraction / 100);
            characters[offset++] = (char)('0' + fraction / 10 % 10);
            characters[offset] = (char)('0' + fraction % 10);
            return new string(characters);
        }

        private static int CountDigits(int value)
        {
            int digits = 1;
            while (value >= 10)
            {
                value /= 10;
                digits++;
            }

            return digits;
        }

        private static int WriteUnsigned(int value, char[] characters, int offset, int digits)
        {
            for (int i = digits - 1; i >= 0; i--)
            {
                characters[offset + i] = (char)('0' + value % 10);
                value /= 10;
            }

            return offset + digits;
        }

        private static int CopyLiteral(string value, char[] characters, int offset)
        {
            for (int i = 0; i < value.Length; i++)
                characters[offset + i] = value[i];
            return offset + value.Length;
        }
    }
}
#endif
