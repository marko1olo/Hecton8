#if UNITY_EDITOR
using Hecton8.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.EditorTools
{
    public sealed class AbyssalScentTunerWindow : EditorWindow
    {
        private const int GraphBarCount = 32;

        private Slider _diffusionSlider;
        private Slider _advectionSlider;
        private Slider _dissipationSlider;
        private Slider _qualitySlider;
        private IntegerField _frameField;
        private FloatField _maxBloodField;
        private IntegerField _emittersField;
        private IntegerField _mockField;
        private IntegerField _iterationsField;
        private FloatField _solverMicrosField;
        private uint _lastTelemetryFrame = uint.MaxValue;
        private readonly VisualElement[] _bars = new VisualElement[GraphBarCount];

        [MenuItem("Hecton8/World/Abyssal Scent Tuner")]
        private static void Open()
        {
            GetWindow<AbyssalScentTunerWindow>("Abyssal Scent");
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
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10f;
            rootVisualElement.style.paddingRight = 10f;
            rootVisualElement.style.paddingTop = 10f;
            rootVisualElement.style.paddingBottom = 10f;

            _diffusionSlider = BuildSlider("Base Diffusion", 0.001f, 1.25f, 0.18f);
            _advectionSlider = BuildSlider("Advection", 0f, 2.5f, 0.72f);
            _dissipationSlider = BuildSlider("Dissipation", 0f, 0.2f, 0.028f);
            _qualitySlider = BuildSlider("Global Quality", 0f, 1f, 1f);
            rootVisualElement.Add(_diffusionSlider);
            rootVisualElement.Add(_advectionSlider);
            rootVisualElement.Add(_dissipationSlider);
            rootVisualElement.Add(_qualitySlider);

            Button reload = new Button(ReloadProfiles) { text = "Reload chemical_emitter_profiles.csv" };
            rootVisualElement.Add(reload);

            VisualElement telemetry = new VisualElement();
            telemetry.style.marginTop = 8f;
            telemetry.style.flexDirection = FlexDirection.Row;
            telemetry.style.flexWrap = Wrap.Wrap;
            _frameField = BuildIntegerField("Frame");
            _maxBloodField = BuildFloatField("Max Blood");
            _emittersField = BuildIntegerField("Emitters");
            _mockField = BuildIntegerField("Mock");
            _iterationsField = BuildIntegerField("Iter");
            _solverMicrosField = BuildFloatField("Solver us");
            telemetry.Add(_frameField);
            telemetry.Add(_maxBloodField);
            telemetry.Add(_emittersField);
            telemetry.Add(_mockField);
            telemetry.Add(_iterationsField);
            telemetry.Add(_solverMicrosField);
            rootVisualElement.Add(telemetry);

            VisualElement graph = new VisualElement();
            graph.style.flexDirection = FlexDirection.Row;
            graph.style.height = 56f;
            graph.style.marginTop = 8f;
            graph.style.alignItems = Align.FlexEnd;
            for (int i = 0; i < GraphBarCount; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.flexGrow = 1f;
                bar.style.marginRight = 1f;
                bar.style.backgroundColor = new Color(0.65f, 0.05f, 0.02f, 0.85f);
                bar.style.height = 1f;
                _bars[i] = bar;
                graph.Add(bar);
            }

            rootVisualElement.Add(graph);
            PullRuntimeState();
        }

        private Slider BuildSlider(string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max) { value = value };
            slider.RegisterValueChangedCallback(_ => PushTuning());
            return slider;
        }

        private static IntegerField BuildIntegerField(string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            field.style.width = 118f;
            field.style.marginRight = 4f;
            return field;
        }

        private static FloatField BuildFloatField(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            field.style.width = 132f;
            field.style.marginRight = 4f;
            return field;
        }

        private void OnEditorUpdate()
        {
            if (rootVisualElement == null || _frameField == null)
                return;

            PullRuntimeState();
        }

        private void PullRuntimeState()
        {
            if (ChemicalInfluenceGrid.TryGetTuningSnapshot(out ChemicalInfluenceGrid.ChemicalTuningDTO tuning))
            {
                _diffusionSlider.SetValueWithoutNotify(tuning.BaseDiffusionRate);
                _advectionSlider.SetValueWithoutNotify(tuning.AdvectionStrength);
                _dissipationSlider.SetValueWithoutNotify(tuning.DissipationRate);
                _qualitySlider.SetValueWithoutNotify(Mathf.Clamp01(tuning.GlobalQualityWeight));
            }

            if (!ChemicalInfluenceGrid.TryGetLatestTelemetry(out ChemicalInfluenceGrid.ChemicalTelemetryEntry telemetry))
                return;

            if (_lastTelemetryFrame == telemetry.Frame)
                return;

            _lastTelemetryFrame = telemetry.Frame;
            _frameField.SetValueWithoutNotify((int)telemetry.Frame);
            _maxBloodField.SetValueWithoutNotify(telemetry.MaxBlood);
            _emittersField.SetValueWithoutNotify(telemetry.ActiveEmitters);
            _mockField.SetValueWithoutNotify(telemetry.MockEmitters);
            _iterationsField.SetValueWithoutNotify(telemetry.Iterations);
            _solverMicrosField.SetValueWithoutNotify(telemetry.SolverMicros);

            int barIndex = (int)(telemetry.Frame % GraphBarCount);
            if ((uint)barIndex >= (uint)_bars.Length || _bars[barIndex] == null)
                return;

            _bars[barIndex].style.height = Mathf.Lerp(1f, 56f, Mathf.Clamp01(telemetry.MaxBlood));
        }

        private void PushTuning()
        {
            if (_diffusionSlider == null || _advectionSlider == null || _dissipationSlider == null || _qualitySlider == null)
                return;

            ChemicalInfluenceGrid.TrySetTuningFromEditor(
                _diffusionSlider.value,
                _advectionSlider.value,
                _dissipationSlider.value,
                _qualitySlider.value);
        }

        private void ReloadProfiles()
        {
            ChemicalInfluenceGrid.TryReloadEmitterProfilesFromDefaultPath();
        }
    }
}
#endif
