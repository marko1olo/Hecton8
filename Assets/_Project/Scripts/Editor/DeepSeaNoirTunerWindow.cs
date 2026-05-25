#if UNITY_EDITOR
using System.Globalization;
using Hecton8.Core.Contracts;
using Hecton8.Visor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    /// <summary>
    /// UI Toolkit facade for the single-pass Deep Sea Noir constants and A/B split.
    /// </summary>
    public sealed class DeepSeaNoirTunerWindow : EditorWindow
    {
        private Toggle _overrideToggle;
        private Toggle _abSplitToggle;
        private Toggle _mockToggle;
        private Slider _grain;
        private Slider _glitch;
        private Slider _chroma;
        private Slider _vignette;
        private Slider _contrast;
        private Slider _saturation;
        private Slider _temperature;
        private Slider _depthTone;
        private Slider _mockStress;
        private Slider _mockDepth;
        private Slider _mockToxicity;
        private Label _constantsLabel;
        private VisualElement _stressGraph;
        private readonly float[] _stressSamples = new float[128];
        private int _stressCursor;
        private uint _lastReadoutHash;
        private bool _hasReadoutHash;
        private bool _pendingReadoutShown;

        [MenuItem("Hecton8/Rendering/Deep Sea Noir Tuner")]
        public static void Open()
        {
            GetWindow<DeepSeaNoirTunerWindow>("Deep Sea Noir");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            _overrideToggle = new Toggle("Editor Override") { value = true };
            _overrideToggle.RegisterValueChangedCallback(_ => ApplyOverride());
            root.Add(_overrideToggle);

            _grain = CreateSlider("Grain", 0f, 0.16f, 0.035f);
            _glitch = CreateSlider("Glitch", 0f, 1f, 0.18f);
            _chroma = CreateSlider("Chroma", 0f, 0.012f, 0.0025f);
            _vignette = CreateSlider("Vignette", 0f, 1f, 0.24f);
            _contrast = CreateSlider("Contrast", 0.5f, 1.8f, 1.08f);
            _saturation = CreateSlider("Saturation", 0f, 1.4f, 0.72f);
            _temperature = CreateSlider("Temperature", -1f, 1f, -0.12f);
            _depthTone = CreateSlider("Depth Tone", 0f, 1f, 0.42f);
            root.Add(_grain);
            root.Add(_glitch);
            root.Add(_chroma);
            root.Add(_vignette);
            root.Add(_contrast);
            root.Add(_saturation);
            root.Add(_temperature);
            root.Add(_depthTone);

            _abSplitToggle = new Toggle("A/B Split");
            _abSplitToggle.RegisterValueChangedCallback(_ => ApplyOverride());
            root.Add(_abSplitToggle);

            _mockToggle = new Toggle("Mock Stress/Depth");
            _mockToggle.RegisterValueChangedCallback(_ => ApplyOverride());
            root.Add(_mockToggle);

            _mockStress = CreateSlider("Mock Stress", 0f, 1f, 0.65f);
            _mockDepth = CreateSlider("Mock Depth", 0f, 1200f, 420f);
            _mockToxicity = CreateSlider("Mock Toxicity", 0f, 1f, 0.35f);
            root.Add(_mockStress);
            root.Add(_mockDepth);
            root.Add(_mockToxicity);

            _stressGraph = new VisualElement();
            _stressGraph.style.height = 84;
            _stressGraph.generateVisualContent += DrawStressGraph;
            root.Add(_stressGraph);

            _constantsLabel = new Label();
            root.Add(_constantsLabel);

            ApplyOverride();
            RefreshReadout();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            RefreshReadout();
        }

        private Slider CreateSlider(string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max);
            slider.value = value;
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(_ => ApplyOverride());
            return slider;
        }

        private void ApplyOverride()
        {
            if (_grain == null)
                return;

            HectonVisorUberPostFeature.SetEditorNoirOverride(
                _overrideToggle != null && _overrideToggle.value,
                _grain.value,
                _glitch.value,
                _chroma.value,
                _vignette.value,
                _contrast.value,
                _saturation.value,
                _temperature.value,
                _depthTone.value,
                _abSplitToggle != null && _abSplitToggle.value,
                _mockToggle != null && _mockToggle.value,
                _mockStress.value,
                _mockDepth.value,
                _mockToxicity.value);

            NoirPostProcessTuningDTO tuning = default;
            tuning.BaseParams = new float4(_grain.value, _glitch.value, _chroma.value, _vignette.value);
            tuning.GradeParams = new float4(_contrast.value, _saturation.value, _temperature.value, _depthTone.value);
            tuning.StressResponse = new float4(0.72f, 0.82f, 0.94f, 0.22f);
            tuning.ProfileParams = new float4(_abSplitToggle != null && _abSplitToggle.value ? 1f : 0f, 1f, 0f, 0f);
            HectonVisorUberPostFeature.TryWriteEditorNoirTuning(in tuning);
        }

        private void RefreshReadout()
        {
            if (_constantsLabel == null)
                return;

            if (HectonVisorUberPostFeature.TryFetchEditorNoirConstants(out NoirPostProcessDTO constants))
            {
                PushStressSample(constants.QualityAndLimits.y);
                uint readoutHash = BuildReadoutHash(in constants);
                if (!_hasReadoutHash || readoutHash != _lastReadoutHash)
                {
                    _constantsLabel.text =
                        "CBuffer: q " + constants.QualityAndLimits.x.ToString("0.000", CultureInfo.InvariantCulture) +
                        ", stress " + constants.QualityAndLimits.y.ToString("0.000", CultureInfo.InvariantCulture) +
                        ", grain " + constants.GrainParams.x.ToString("0.000", CultureInfo.InvariantCulture) +
                        ", glitch " + constants.AberrationParams.y.ToString("0.000", CultureInfo.InvariantCulture);
                    _lastReadoutHash = readoutHash;
                    _hasReadoutHash = true;
                }

                _pendingReadoutShown = false;
            }
            else
            {
                if (!_pendingReadoutShown)
                    _constantsLabel.text = "CBuffer: pending first render pass";

                _pendingReadoutShown = true;
                _hasReadoutHash = false;
            }
        }

        private static uint BuildReadoutHash(in NoirPostProcessDTO constants)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, QuantizeReadout(constants.QualityAndLimits.x));
            hash = MixHash(hash, QuantizeReadout(constants.QualityAndLimits.y));
            hash = MixHash(hash, QuantizeReadout(constants.GrainParams.x));
            hash = MixHash(hash, QuantizeReadout(constants.AberrationParams.y));
            return hash;
        }

        private static uint MixHash(uint hash, int value)
        {
            unchecked
            {
                return (hash ^ (uint)value) * 16777619u;
            }
        }

        private static int QuantizeReadout(float value)
        {
            float finite = math.isfinite(value) ? value : 0f;
            return (int)math.round(finite * 1000f);
        }

        private void PushStressSample(float stress01)
        {
            _stressSamples[_stressCursor] = math.saturate(math.isfinite(stress01) ? stress01 : 0f);
            _stressCursor++;
            if (_stressCursor >= _stressSamples.Length)
                _stressCursor = 0;
            _stressGraph?.MarkDirtyRepaint();
        }

        private void DrawStressGraph(MeshGenerationContext context)
        {
            if (_stressGraph == null)
                return;

            Rect rect = _stressGraph.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            painter.fillColor = new Color(0.015f, 0.022f, 0.026f, 1f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();

            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.18f, 0.82f, 0.92f, 1f);
            painter.BeginPath();
            for (int i = 0; i < _stressSamples.Length; i++)
            {
                int index = (_stressCursor + i) % _stressSamples.Length;
                float x = rect.xMin + rect.width * (i / (float)(_stressSamples.Length - 1));
                float y = rect.yMax - _stressSamples[index] * rect.height;
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();
        }
    }
}
#endif
