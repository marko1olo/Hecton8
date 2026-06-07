#if UNITY_EDITOR
using Hecton8.VFX;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.VFX.EditorTools
{
    public sealed class CinematicTraumaTunerWindow : EditorWindow
    {
        private const int GraphSampleCapacity = 300;

        private CameraJuiceSystem _runtime;
        private CameraJuiceTelemetryGraphElement _graph;
        private Slider _translationSlider;
        private Slider _rotationSlider;
        private Slider _decaySlider;
        private Slider _frequencySlider;
        private Slider _severitySlider;
        private Toggle _mockToggle;
        private SliderInt _mockCountSlider;
        private Slider _mockRadiusSlider;
        private Label _runtimeLabel;
        private Label _traumaLabel;
        private Label _translationLabel;
        private Label _signalsLabel;
        private Label _burstLabel;

        [MenuItem("Hecton8/VFX/Cinematic Trauma Tuner")]
        public static void Open()
        {
            GetWindow<CinematicTraumaTunerWindow>("Trauma Tuner");
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.backgroundColor = new Color(0.025f, 0.028f, 0.032f, 1f);

            _runtimeLabel = new Label("Runtime: unresolved");
            _runtimeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_runtimeLabel);

            Button refreshButton = new Button(RefreshRuntime) { text = "Refresh Runtime" };
            root.Add(refreshButton);

            _graph = new CameraJuiceTelemetryGraphElement();
            _graph.style.height = 180;
            _graph.style.marginTop = 6;
            root.Add(_graph);

            _translationSlider = BuildSlider("Translation meters", 0.001f, 0.25f, 0.07f, OnTuningChanged);
            _rotationSlider = BuildSlider("Rotation degrees", 0.01f, 12f, 2.4f, OnTuningChanged);
            _decaySlider = BuildSlider("Decay per second", 0.1f, 8f, 1.65f, OnTuningChanged);
            _frequencySlider = BuildSlider("Frequency Hz", 1f, 55f, 18f, OnTuningChanged);
            root.Add(_translationSlider);
            root.Add(_rotationSlider);
            root.Add(_decaySlider);
            root.Add(_frequencySlider);

            _severitySlider = BuildSlider("Test severity", 0f, 1f, 0.65f, default, false);
            root.Add(_severitySlider);
            root.Add(new Button(InjectPulse) { text = "Inject Test Impulse" });

            _mockToggle = new Toggle("Mock AUP spike storm");
            _mockToggle.RegisterValueChangedCallback(OnMockChanged);
            _mockCountSlider = new SliderInt("Mock count", 1, 32) { value = 4, showInputField = true };
            _mockCountSlider.RegisterValueChangedCallback(OnMockChanged);
            _mockRadiusSlider = BuildSlider("Mock radius meters", 1f, 120f, 18f, OnMockChanged);
            root.Add(_mockToggle);
            root.Add(_mockCountSlider);
            root.Add(_mockRadiusSlider);

            _traumaLabel = new Label("Trauma: 0");
            _translationLabel = new Label("Max translation: 0 m");
            _signalsLabel = new Label("Signals: 0");
            _burstLabel = new Label("Burst us: 0");
            root.Add(_traumaLabel);
            root.Add(_translationLabel);
            root.Add(_signalsLabel);
            root.Add(_burstLabel);

            RefreshRuntime();
        }

        private void OnEditorUpdate()
        {
            if (_runtime == null)
            {
                SetRuntimeStatus(false);
                return;
            }

            Vector4 state = _runtime.EditorReadProceduralCameraJuiceState();
            _traumaLabel.text = "Trauma: " + state.x.ToString("0.000");
            _translationLabel.text = "Max translation: " + state.y.ToString("0.0000") + " m";
            _signalsLabel.text = "Signals: " + ((int)state.z).ToString();
            _burstLabel.text = "Burst us: " + state.w.ToString("0.00");
            _graph.SetRuntime(_runtime);
            _graph.MarkDirtyRepaint();
        }

        private void RefreshRuntime()
        {
            _runtime = null;
            CameraJuiceSystem[] runtimes = Resources.FindObjectsOfTypeAll<CameraJuiceSystem>();
            for (int i = 0; i < runtimes.Length; i++)
            {
                CameraJuiceSystem candidate = runtimes[i];
                if (candidate != null && candidate.gameObject.scene.IsValid())
                {
                    _runtime = candidate;
                    break;
                }
            }

            _graph.SetRuntime(_runtime);
            SetRuntimeStatus(_runtime != null);
        }

        private void SetRuntimeStatus(bool resolved)
        {
            if (_runtimeLabel != null)
                _runtimeLabel.text = resolved ? "Runtime: CameraJuiceSystem" : "Runtime: unresolved";
            SetEnabled(_translationSlider, resolved);
            SetEnabled(_rotationSlider, resolved);
            SetEnabled(_decaySlider, resolved);
            SetEnabled(_frequencySlider, resolved);
            SetEnabled(_severitySlider, resolved);
            SetEnabled(_mockToggle, resolved);
            SetEnabled(_mockCountSlider, resolved);
            SetEnabled(_mockRadiusSlider, resolved);
        }

        private static void SetEnabled(VisualElement element, bool enabled)
        {
            if (element != null)
                element.SetEnabled(enabled);
        }

        private static Slider BuildSlider(
            string label,
            float min,
            float max,
            float value,
            EventCallback<ChangeEvent<float>> callback,
            bool registerCallback = true)
        {
            Slider slider = new Slider(label, min, max) { value = value, showInputField = true };
            if (registerCallback)
                slider.RegisterValueChangedCallback(callback);
            return slider;
        }

        private void OnTuningChanged(ChangeEvent<float> evt)
        {
            ApplyTuning();
        }

        private void ApplyTuning()
        {
            if (_runtime == null)
                return;

            _runtime.EditorSetProceduralCameraJuiceTuning(
                _translationSlider.value,
                _rotationSlider.value,
                _decaySlider.value,
                _frequencySlider.value);
        }

        private void InjectPulse()
        {
            if (_runtime != null)
                _runtime.EditorInjectProceduralCameraJuicePulse(math.saturate(_severitySlider.value));
        }

        private void OnMockChanged(ChangeEvent<bool> evt)
        {
            ApplyMock();
        }

        private void OnMockChanged(ChangeEvent<int> evt)
        {
            ApplyMock();
        }

        private void OnMockChanged(ChangeEvent<float> evt)
        {
            ApplyMock();
        }

        private void ApplyMock()
        {
            if (_runtime == null)
                return;

            _runtime.EditorSetProceduralCameraJuiceMockSignals(
                _mockToggle.value,
                _mockCountSlider.value,
                _severitySlider.value,
                _mockRadiusSlider.value);
        }

        private sealed class CameraJuiceTelemetryGraphElement : VisualElement
        {
            private readonly float[] _trauma = new float[GraphSampleCapacity];
            private readonly float[] _signals = new float[GraphSampleCapacity];
            private readonly float[] _burst = new float[GraphSampleCapacity];
            private CameraJuiceSystem _runtime;
            private int _sampleCount;

            public CameraJuiceTelemetryGraphElement()
            {
                generateVisualContent += Draw;
            }

            public void SetRuntime(CameraJuiceSystem runtime)
            {
                _runtime = runtime;
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = context.visualElement.contentRect;
                Painter2D painter = context.painter2D;
                DrawRect(painter, rect, new Color(0.045f, 0.05f, 0.06f, 1f));

                if (_runtime == null)
                    return;

                _sampleCount = _runtime.EditorCopyCameraJuiceTelemetry(_trauma, _signals, _burst, GraphSampleCapacity);
                if (_sampleCount <= 1)
                    return;

                DrawSeries(painter, rect, _trauma, _sampleCount, new Color(1f, 0.22f, 0.18f, 1f), 1.8f);
                DrawSeries(painter, rect, _signals, _sampleCount, new Color(1f, 0.82f, 0.18f, 1f), 1.2f);
                DrawSeries(painter, rect, _burst, _sampleCount, new Color(0.25f, 0.85f, 1f, 1f), 1.2f);
            }

            private static void DrawSeries(Painter2D painter, Rect rect, float[] samples, int count, Color color, float width)
            {
                painter.strokeColor = color;
                painter.lineWidth = width;
                painter.BeginPath();
                for (int i = 0; i < count; i++)
                {
                    float value = Mathf.Clamp01(samples[i]);
                    float x = rect.xMin + (i / (float)(count - 1)) * rect.width;
                    float y = rect.yMax - (value * rect.height);
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }

            private static void DrawRect(Painter2D painter, Rect rect, Color color)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}
#endif
