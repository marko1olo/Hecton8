#if UNITY_EDITOR
using Hecton8.VFX.Bioluminescence;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.VFX.Bioluminescence.Editor
{
    public sealed class BioluminescenceTunerWindow : EditorWindow
    {
        private const float MinFrequency = 0.0025f;
        private const float MaxFrequency = 8f;
        private const float MinWaveSpeed = 1f;
        private const float MaxWaveSpeed = 180f;
        private const float DefaultWaveSpeed = 48f;

        private VisualElement _playModeRoot;
        private Label _playModeWarning;
        private Slider _ambientSlider;
        private Slider _o2Slider;
        private Slider _healthSlider;
        private Slider _pulseWaveSpeedSlider;
        private ColorField _pulseColorField;
        private Label[] _speciesLabels;
        private ColorField[] _speciesColorFields;
        private Slider[] _speciesFrequencySliders;
        private Slider[] _speciesWaveSpeedSliders;
        private bool _suppressCallbacks;

        [MenuItem("Hecton8/VFX/Bioluminescence Tuner")]
        private static void Open()
        {
            GetWindow<BioluminescenceTunerWindow>("Bioluminescence Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _playModeWarning = new Label("Enter Play Mode to edit DataVault-backed bioluminescence memory.");
            _playModeWarning.style.marginBottom = 8f;
            root.Add(_playModeWarning);

            _playModeRoot = new VisualElement();
            root.Add(_playModeRoot);

            BuildWeatherControls(_playModeRoot);
            BuildPulseControls(_playModeRoot);
            BuildSpeciesControls(_playModeRoot);
            RefreshFromVault();
        }

        private void OnFocus()
        {
            RefreshFromVault();
        }

        private void OnInspectorUpdate()
        {
            RefreshFromVault();
        }

        private void BuildWeatherControls(VisualElement root)
        {
            Label header = CreateHeader("Mock Weather");
            root.Add(header);

            _ambientSlider = CreateSlider("Ambient", 0f, 1f);
            _o2Slider = CreateSlider("O2", 0f, 1f);
            _healthSlider = CreateSlider("Health Index", 0f, 1f);
            _ambientSlider.RegisterValueChangedCallback(_ => WriteWeather());
            _o2Slider.RegisterValueChangedCallback(_ => WriteWeather());
            _healthSlider.RegisterValueChangedCallback(_ => WriteWeather());
            root.Add(_ambientSlider);
            root.Add(_o2Slider);
            root.Add(_healthSlider);
        }

        private void BuildPulseControls(VisualElement root)
        {
            Label header = CreateHeader("Global Pulse");
            header.style.marginTop = 10f;
            root.Add(header);

            _pulseWaveSpeedSlider = CreateSlider("Wave Speed", MinWaveSpeed, MaxWaveSpeed);
            _pulseWaveSpeedSlider.SetValueWithoutNotify(DefaultWaveSpeed);
            _pulseColorField = new ColorField("Pulse Color");
            _pulseColorField.showAlpha = true;
            _pulseColorField.SetValueWithoutNotify(new Color(0.35f, 0.85f, 1f, 1f));

            Button triggerButton = new Button(TriggerGlobalPulse) { text = "Trigger Global Pulse" };
            triggerButton.style.marginTop = 4f;
            root.Add(_pulseWaveSpeedSlider);
            root.Add(_pulseColorField);
            root.Add(triggerButton);
        }

        private void BuildSpeciesControls(VisualElement root)
        {
            Label header = CreateHeader("Species Tuning");
            header.style.marginTop = 10f;
            root.Add(header);

            ScrollView speciesScroll = new ScrollView(ScrollViewMode.Vertical);
            speciesScroll.style.flexGrow = 1f;
            speciesScroll.style.minHeight = 240f;
            root.Add(speciesScroll);

            int count = BiolumPulseSyncRuntime.MaxSpeciesTuningCount;
            _speciesLabels = new Label[count];
            _speciesColorFields = new ColorField[count];
            _speciesFrequencySliders = new Slider[count];
            _speciesWaveSpeedSliders = new Slider[count];

            for (int i = 0; i < count; i++)
            {
                int rowIndex = i;
                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 4f;

                Label hashLabel = new Label("0x00000000");
                hashLabel.style.width = 92f;
                hashLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                ColorField colorField = new ColorField();
                colorField.showAlpha = true;
                colorField.style.width = 94f;
                colorField.RegisterValueChangedCallback(_ => WriteSpecies(rowIndex));

                VisualElement sliders = new VisualElement();
                sliders.style.flexGrow = 1f;
                sliders.style.marginLeft = 6f;

                Slider frequencySlider = CreateSlider("Base Frequency", MinFrequency, MaxFrequency);
                Slider waveSpeedSlider = CreateSlider("Wave Speed", MinWaveSpeed, MaxWaveSpeed);
                frequencySlider.RegisterValueChangedCallback(_ => WriteSpecies(rowIndex));
                waveSpeedSlider.RegisterValueChangedCallback(_ => WriteSpecies(rowIndex));

                sliders.Add(frequencySlider);
                sliders.Add(waveSpeedSlider);
                row.Add(hashLabel);
                row.Add(colorField);
                row.Add(sliders);
                speciesScroll.Add(row);

                _speciesLabels[i] = hashLabel;
                _speciesColorFields[i] = colorField;
                _speciesFrequencySliders[i] = frequencySlider;
                _speciesWaveSpeedSliders[i] = waveSpeedSlider;
            }
        }

        private void RefreshFromVault()
        {
            bool playMode = Application.isPlaying;
            if (_playModeWarning != null)
                _playModeWarning.style.display = playMode ? DisplayStyle.None : DisplayStyle.Flex;
            if (_playModeRoot != null)
                _playModeRoot.SetEnabled(playMode);

            if (!playMode || _ambientSlider == null || _speciesLabels == null)
                return;

            _suppressCallbacks = true;
            try
            {
                if (BiolumPulseSyncRuntime.TryReadEditorMockWeather(out MockWeatherSignal weather))
                {
                    _ambientSlider.SetValueWithoutNotify(weather.AmbientLightLevel);
                    _o2Slider.SetValueWithoutNotify(weather.O2Level01);
                    _healthSlider.SetValueWithoutNotify(weather.SystemHealthIndex01);
                }

                for (int i = 0; i < _speciesLabels.Length; i++)
                {
                    if (!BiolumPulseSyncRuntime.TryReadEditorSpeciesTuning(i, out BiolumSpeciesTuningDTO tuning))
                        continue;

                    _speciesLabels[i].text = FormatSpeciesHash(tuning.SpeciesHash);
                    _speciesColorFields[i].SetValueWithoutNotify(ToColor(tuning.PackedColor));
                    _speciesFrequencySliders[i].SetValueWithoutNotify(math.clamp(tuning.Frequency, MinFrequency, MaxFrequency));
                    _speciesWaveSpeedSliders[i].SetValueWithoutNotify(math.clamp(tuning.WaveSpeed <= 0f ? DefaultWaveSpeed : tuning.WaveSpeed, MinWaveSpeed, MaxWaveSpeed));
                }
            }
            finally
            {
                _suppressCallbacks = false;
            }
        }

        private void WriteWeather()
        {
            if (_suppressCallbacks || !Application.isPlaying)
                return;

            if (!BiolumPulseSyncRuntime.TryReadEditorMockWeather(out MockWeatherSignal weather))
                return;

            weather.AmbientLightLevel = _ambientSlider.value;
            weather.O2Level01 = _o2Slider.value;
            weather.SystemHealthIndex01 = _healthSlider.value;
            BiolumPulseSyncRuntime.TryWriteEditorMockWeather(weather);
        }

        private void WriteSpecies(int index)
        {
            if (_suppressCallbacks || !Application.isPlaying || index < 0)
                return;

            if (!BiolumPulseSyncRuntime.TryReadEditorSpeciesTuning(index, out BiolumSpeciesTuningDTO tuning))
                return;

            Color color = _speciesColorFields[index].value;
            tuning.PackedColor = BiolumPackedColorUtility.PackRgb10A2(new float3(color.r, color.g, color.b), color.a);
            tuning.Frequency = math.clamp(_speciesFrequencySliders[index].value, MinFrequency, MaxFrequency);
            tuning.WaveSpeed = math.clamp(_speciesWaveSpeedSliders[index].value, MinWaveSpeed, MaxWaveSpeed);
            BiolumPulseSyncRuntime.TryWriteEditorSpeciesTuning(index, tuning);
        }

        private void TriggerGlobalPulse()
        {
            if (!Application.isPlaying)
                return;

            Color pulseColor = _pulseColorField.value;
            uint packed = BiolumPackedColorUtility.PackRgb10A2(new float3(pulseColor.r, pulseColor.g, pulseColor.b), pulseColor.a);
            BiolumPulseSyncRuntime.TryTriggerEditorGlobalPulse(ResolveSceneAup(), _pulseWaveSpeedSlider.value, packed);
        }

        private static Slider CreateSlider(string label, float lowValue, float highValue)
        {
            Slider slider = new Slider(label, lowValue, highValue);
            slider.showInputField = true;
            slider.style.marginBottom = 2f;
            return slider;
        }

        private static Label CreateHeader(string text)
        {
            Label label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 4f;
            return label;
        }

        private static Color ToColor(uint packed)
        {
            float3 rgb = BiolumPackedColorUtility.UnpackRgb10A2(packed);
            float alpha = ((packed >> 30) & 3u) * (1f / 3f);
            return new Color(rgb.x, rgb.y, rgb.z, alpha);
        }

        private static string FormatSpeciesHash(uint hash)
        {
            return "0x" + hash.ToString("X8");
        }

        private static double3 ResolveSceneAup()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null && view.camera != null)
            {
                Vector3 p = view.camera.transform.position;
                return new double3(p.x, p.y, p.z);
            }

            return double3.zero;
        }
    }
}
#endif
