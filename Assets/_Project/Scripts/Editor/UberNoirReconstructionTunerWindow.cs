#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Visor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    /// <summary>
    /// UI Toolkit facade for DRS reconstruction constants and split-screen proof.
    /// </summary>
    public sealed class UberNoirReconstructionTunerWindow : EditorWindow
    {
        private Slider _bilateralRadius;
        private Slider _temporalHistory;
        private Slider _sharpeningClamp;
        private Slider _grainIntensity;
        private Slider _overkillThreshold;
        private Slider _mockScale;
        private Slider _mockQuality;
        private Toggle _overrideToggle;
        private Toggle _mockToggle;
        private Toggle _abSplitToggle;
        private Label _scaleLabel;
        private Label _qualityLabel;
        private Label _constantsLabel;

        [MenuItem("Hecton8/Rendering/Uber Noir Tuner")]
        public static void Open()
        {
            GetWindow<UberNoirReconstructionTunerWindow>("Uber Noir Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            _overrideToggle = new Toggle("Editor Override");
            _overrideToggle.value = true;
            _overrideToggle.RegisterValueChangedCallback(_ => ApplyOverride());
            root.Add(_overrideToggle);

            _bilateralRadius = CreateSlider("Bilateral Radius", 0.25f, 3f, 1.15f);
            _temporalHistory = CreateSlider("Temporal History", 0f, 0.96f, 0.62f);
            _sharpeningClamp = CreateSlider("Sharpening Clamp", 0f, 1f, 0.68f);
            _grainIntensity = CreateSlider("Grain Intensity", 0f, 0.16f, 0.035f);
            _overkillThreshold = CreateSlider("Overkill Threshold", 0f, 1f, 0.84f);
            root.Add(_bilateralRadius);
            root.Add(_temporalHistory);
            root.Add(_sharpeningClamp);
            root.Add(_grainIntensity);
            root.Add(_overkillThreshold);

            _abSplitToggle = new Toggle("A/B Split");
            _abSplitToggle.RegisterValueChangedCallback(_ => ApplyOverride());
            root.Add(_abSplitToggle);

            _mockToggle = new Toggle("Mock Input");
            _mockToggle.RegisterValueChangedCallback(_ => ApplyOverride());
            root.Add(_mockToggle);

            _mockScale = CreateSlider("Mock Render Scale", 0.3f, 1f, 0.5f);
            _mockQuality = CreateSlider("Mock Quality", 0f, 1f, 0.35f);
            root.Add(_mockScale);
            root.Add(_mockQuality);

            Button forceButton = new Button(ForceExtremeMock) { text = "Force 0.3x Proof" };
            root.Add(forceButton);

            _scaleLabel = new Label();
            _qualityLabel = new Label();
            _constantsLabel = new Label();
            root.Add(_scaleLabel);
            root.Add(_qualityLabel);
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
            if (_bilateralRadius == null)
                return;

            HectonVisorUberPostFeature.SetEditorReconstructionOverride(
                _overrideToggle != null && _overrideToggle.value,
                _bilateralRadius.value,
                _temporalHistory.value,
                _sharpeningClamp.value,
                _grainIntensity.value,
                _overkillThreshold.value,
                _abSplitToggle != null && _abSplitToggle.value,
                _mockToggle != null && _mockToggle.value,
                _mockScale.value,
                _mockQuality.value);

            PushMockSignal();
        }

        private void ForceExtremeMock()
        {
            if (_mockToggle != null)
                _mockToggle.value = true;
            if (_mockScale != null)
                _mockScale.value = 0.3f;
            if (_mockQuality != null)
                _mockQuality.value = 0.1f;
            ApplyOverride();
        }

        private void PushMockSignal()
        {
            MockReconstructionInputSignal signal = default;
            bool mockActive = _mockToggle != null && _mockToggle.value;
            signal.RenderScale01 = mockActive ? math.clamp(_mockScale.value, 0.3f, 1f) : 1f;
            signal.GlobalQualityWeight01 = mockActive ? math.saturate(_mockQuality.value) : 1f;
            signal.JitterPixels = mockActive ? 2.0f : 0f;
            signal.FrameTimeMs = mockActive ? 22.0f : 0f;
            signal.TemporalStress01 = mockActive ? 1f : 0f;
            signal.Flags = mockActive ? 1u : 0u;
            signal._pad0 = 0u;
            signal._pad1 = 0u;
            HectonVisorUberPostFeature.TryWriteEditorMockReconstructionSignal(in signal);
        }

        private void RefreshReadout()
        {
            if (_scaleLabel == null)
                return;

            if (GlobalRegistry.ResolutionScaler != null &&
                GlobalRegistry.ResolutionScaler.TryGetScaleState(out ResolutionScaleState state))
            {
                _scaleLabel.text = $"Effective Render Scale: {state.CurrentRenderScale01:0.000}";
                _qualityLabel.text = $"Global Quality Weight: {state.GlobalQualityWeight01:0.000}";
            }
            else
            {
                _scaleLabel.text = "Effective Render Scale: runtime unavailable";
                _qualityLabel.text = "Global Quality Weight: runtime unavailable";
            }

            if (HectonVisorUberPostFeature.TryReadEditorReconstructionConstants(out UberNoirReconstructionConstantsDTO constants))
            {
                _constantsLabel.text =
                    $"CBuffer: scale {constants.RenderScaleParams.x:0.000}, sharp {constants.RenderScaleParams.w:0.000}, radius {constants.TemporalParams.w:0.000}, grain {constants.OverkillParams.x:0.000}";
            }
            else
            {
                _constantsLabel.text = "CBuffer: pending first render pass";
            }
        }
    }
}
#endif
