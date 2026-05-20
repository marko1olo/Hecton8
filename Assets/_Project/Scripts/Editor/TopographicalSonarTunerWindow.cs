using Hecton8.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class TopographicalSonarTunerWindow : EditorWindow
    {
        private Slider _radiusSlider;
        private Slider _stepSlider;
        private Slider _pointSizeSlider;
        private Slider _fadeSlider;
        private Slider _qualitySlider;
        private Label _statusLabel;

        [MenuItem("Hecton8/Tools/Topographical Sonar Tuner")]
        public static void Open()
        {
            TopographicalSonarTunerWindow window = GetWindow<TopographicalSonarTunerWindow>();
            window.titleContent = new GUIContent("Sonar Tuner");
            window.minSize = new Vector2(420f, 260f);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            _statusLabel = new Label("No active TopographicalSonarSynthesizer");
            root.Add(_statusLabel);

            _radiusSlider = CreateSlider("Max Ping Radius", 4f, 400f);
            _stepSlider = CreateSlider("Ray Step Size", TopographicalSonarConstants.MinimumStepMeters, 8f);
            _pointSizeSlider = CreateSlider("Point Size", 0.5f, 18f);
            _fadeSlider = CreateSlider("Point Fade Speed", 0.1f, 60f);
            _qualitySlider = CreateSlider("Quality Override", -1f, 1f);

            root.Add(_radiusSlider);
            root.Add(_stepSlider);
            root.Add(_pointSizeSlider);
            root.Add(_fadeSlider);
            root.Add(_qualitySlider);

            Button apply = new Button(ApplyTuning) { text = "Apply" };
            Button ping = new Button(FirePing) { text = "Manual Ping" };
            Button csv = new Button(LoadCsv) { text = "Load sonar_material_colors.csv" };
            root.Add(apply);
            root.Add(ping);
            root.Add(csv);

            EditorApplication.update -= RefreshStatus;
            EditorApplication.update += RefreshStatus;
            RefreshStatus();
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshStatus;
        }

        private static Slider CreateSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high)
            {
                showInputField = true
            };
            slider.style.marginTop = 4f;
            return slider;
        }

        private void RefreshStatus()
        {
            TopographicalSonarSynthesizer runtime = ResolveRuntime();
            if (runtime == null)
            {
                _statusLabel.text = "No active TopographicalSonarSynthesizer";
                return;
            }

            _radiusSlider.SetValueWithoutNotify(runtime.GetMaxDistanceMeters());
            _stepSlider.SetValueWithoutNotify(runtime.GetStepMeters());
            _pointSizeSlider.SetValueWithoutNotify(runtime.GetPointSizePixels());
            _fadeSlider.SetValueWithoutNotify(runtime.GetEchoFadeSeconds());
            _qualitySlider.SetValueWithoutNotify(runtime.GetQualityOverride());
            _statusLabel.text =
                "points=" + runtime.GetActivePointCount() +
                " hits=" + runtime.GetLastHitCount() +
                " ms=" + runtime.GetLastScanWallMilliseconds().ToString("0.000") +
                " seq=" + runtime.GetSequence();
        }

        private void ApplyTuning()
        {
            TopographicalSonarSynthesizer runtime = ResolveRuntime();
            if (runtime == null)
                return;

            runtime.SetTuningFromEditor(
                _radiusSlider.value,
                _stepSlider.value,
                _pointSizeSlider.value,
                _fadeSlider.value,
                _qualitySlider.value);
            EditorUtility.SetDirty(runtime);
        }

        private void FirePing()
        {
            TopographicalSonarSynthesizer runtime = ResolveRuntime();
            if (runtime != null)
                runtime.TriggerManualPing(1f);
        }

        private void LoadCsv()
        {
            TopographicalSonarSynthesizer runtime = ResolveRuntime();
            if (runtime == null)
                return;

            string path = EditorUtility.OpenFilePanel("sonar_material_colors.csv", "Assets", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            runtime.TryApplyMaterialColorCsvFileForEditor(path, out _);
        }

        private static TopographicalSonarSynthesizer ResolveRuntime()
        {
            if (TopographicalSonarSynthesizer.ActiveRuntime != null)
                return TopographicalSonarSynthesizer.ActiveRuntime;

#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<TopographicalSonarSynthesizer>();
#else
            return UnityEngine.Object.FindObjectOfType<TopographicalSonarSynthesizer>();
#endif
        }
    }
}
