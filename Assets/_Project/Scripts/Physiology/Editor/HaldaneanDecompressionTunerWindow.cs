using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physiology.Editor
{
    public sealed class HaldaneanDecompressionTunerWindow : EditorWindow
    {
        private const int TissueCount = ShinobuPhysiologyConstants.TissueCompartmentCount;
        private readonly VisualElement[] _bars = new VisualElement[TissueCount]; // COLD ALLOC: VisualElement[3] - editor histogram bars - owner: HaldaneanDecompressionTunerWindow
        private readonly VisualElement[] _ambientMarkers = new VisualElement[TissueCount]; // COLD ALLOC: VisualElement[3] - editor ambient pressure markers
        private readonly Label[] _labels = new Label[TissueCount]; // COLD ALLOC: Label[3] - editor histogram labels - owner: HaldaneanDecompressionTunerWindow
        private Slider _gradientLow;
        private Slider _gradientHigh;
        private Slider _gasNitrogen;
        private Label _status;
        private int _statusCode = int.MinValue;
        private ShinobuPhysiologyRuntime _runtime;
        private static ShinobuPhysiologyRuntime s_cachedRuntime;
        private static double s_nextRuntimeResolveTime;

        [MenuItem("Hecton8/Physiology/Haldanean Decompression Tuner")]
        public static void Open()
        {
            GetWindow<HaldaneanDecompressionTunerWindow>("Haldanean Decompression Tuner");
        }

        private void OnEnable()
        {
            BuildUi();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            _runtime = ResolveRuntime();
            RefreshHistogram();
        }

        private void BuildUi()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;

            _status = new Label("No runtime row");
            root.Add(_status);

            _gradientLow = new Slider("Gradient Factor Low", 0.2f, 1.0f) { value = 0.85f };
            _gradientHigh = new Slider("Gradient Factor High", 0.2f, 1.0f) { value = 1.0f };
            _gasNitrogen = new Slider("Gas Mix Nitrogen Fraction", 0f, 0.95f) { value = ShinobuPhysiologyConstants.NitrogenFraction };
            _gradientLow.RegisterValueChangedCallback(OnGradientChanged);
            _gradientHigh.RegisterValueChangedCallback(OnGradientChanged);
            _gasNitrogen.RegisterValueChangedCallback(OnGasNitrogenChanged);
            root.Add(_gradientLow);
            root.Add(_gradientHigh);
            root.Add(_gasNitrogen);

            for (int i = 0; i < TissueCount; i++)
            {
                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.height = 20;
                row.style.marginTop = 2;
                Label label = new Label(ResolveTissueLabel(i));
                label.style.width = 28;
                VisualElement track = new VisualElement();
                track.style.flexGrow = 1f;
                track.style.height = 16;
                track.style.position = Position.Relative;
                track.style.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 1f);
                VisualElement bar = new VisualElement();
                bar.style.position = Position.Absolute;
                bar.style.left = new Length(0f, LengthUnit.Pixel);
                bar.style.top = new Length(0f, LengthUnit.Pixel);
                bar.style.height = 16;
                bar.style.width = new Length(1f, LengthUnit.Percent);
                bar.style.backgroundColor = new Color(0.10f, 0.65f, 0.30f, 1f);
                VisualElement marker = new VisualElement();
                marker.style.position = Position.Absolute;
                marker.style.left = new Length(0f, LengthUnit.Percent);
                marker.style.top = new Length(0f, LengthUnit.Pixel);
                marker.style.width = new Length(2f, LengthUnit.Pixel);
                marker.style.height = 16;
                marker.style.backgroundColor = Color.white;
                row.Add(label);
                track.Add(bar);
                track.Add(marker);
                row.Add(track);
                root.Add(row);
                _labels[i] = label;
                _bars[i] = bar;
                _ambientMarkers[i] = marker;
            }
        }

        private void RefreshHistogram()
        {
            if (_runtime == null ||
                !_runtime.TryGetDecompressionState(0, out DecompressionStateDTO state))
            {
                _status.text = "No readable decompression state";
                return;
            }

            bool hasDecompressionTelemetry = _runtime.TryGetLatestDecompressionTelemetry(out DecompressionTelemetryEntry decompressionTelemetry);
            bool hasTelemetry = _runtime.TryGetLatestTelemetry(out PhysiologyTelemetryEntry telemetry);
            float ambientLine = hasDecompressionTelemetry && decompressionTelemetry.AmbientPressureAtm > 0f
                ? math.max(0f, decompressionTelemetry.AmbientPressureAtm)
                : hasTelemetry && telemetry.AmbientPressureAtm > 0f
                ? math.max(0f, telemetry.AmbientPressureAtm)
                : math.max(0f, state.CurrentAmbientPressure);
            float max = math.max(1f, math.max(state.CurrentAmbientPressure, ambientLine));
            for (int i = 0; i < TissueCount; i++)
                max = math.max(max, math.max(0f, state.GetTissueTensionN2(i)));

            float ambientMarkerLeft = math.saturate(ambientLine * math.rcp(math.max(0.0001f, max))) * 100f;
            for (int i = 0; i < TissueCount; i++)
            {
                float tension = math.max(0f, state.GetTissueTensionN2(i));
                float width = math.saturate(tension * math.rcp(max)) * 100f;
                _bars[i].style.width = new Length(width, LengthUnit.Percent);
                _bars[i].style.backgroundColor = ResolveGradientColor(state.GradientAdvantage);
                _ambientMarkers[i].style.left = new Length(ambientMarkerLeft, LengthUnit.Percent);
            }

            bool decompressionFault = hasDecompressionTelemetry &&
                                      (decompressionTelemetry.FatalFlags != 0u ||
                                       !math.isfinite(decompressionTelemetry.ExecutionMicroseconds) ||
                                       !math.isfinite(decompressionTelemetry.LeadingTissueTensionAtm) ||
                                       !math.isfinite(decompressionTelemetry.MValueGradientAtm) ||
                                       decompressionTelemetry.ExecutionMicroseconds >= ShinobuPhysiologyConstants.TelemetryDumpBudgetMicroseconds);
            bool telemetryFault = decompressionFault ||
                                  (hasTelemetry &&
                                   (telemetry.FatalFlags != 0u ||
                                    !math.isfinite(telemetry.ExecutionMicroseconds) ||
                                    telemetry.ExecutionMicroseconds >= ShinobuPhysiologyConstants.TelemetryDumpBudgetMicroseconds));
            int statusCode = telemetryFault ? 3 : state.BubbleFlags != 0u ? 2 : state.GradientAdvantage < 0.25f ? 1 : 0;
            if (statusCode != _statusCode)
            {
                _statusCode = statusCode;
                _status.text = statusCode == 3
                    ? "Telemetry fault - black box dump armed"
                    : statusCode == 2
                    ? "Bubbling - decompression ceiling broken"
                    : statusCode == 1
                        ? "Ceiling margin critical"
                        : "Ceiling margin stable";
            }
        }

        private void WriteTuning()
        {
            _runtime = ResolveRuntime();
            if (_runtime == null || !_runtime.TryGetTuning(out PhysiologyTuningDTO tuning))
                return;

            float low = math.clamp(_gradientLow.value, 0.2f, 1f);
            float high = math.clamp(_gradientHigh.value, low, 1f);
            tuning.BendsRiskScale = math.rcp(math.max(0.2f, low));
            tuning.HaldaneTimeScale = math.lerp(0.75f, 1.25f, high);
            _runtime.SetEditorTuning(tuning);
        }

        private static Color ResolveGradientColor(float gradient)
        {
            if (gradient < 0f)
                return new Color(0.90f, 0.08f, 0.05f, 1f);
            if (gradient < 0.25f)
                return new Color(0.95f, 0.74f, 0.10f, 1f);
            return new Color(0.10f, 0.65f, 0.30f, 1f);
        }

        private void OnGradientChanged(ChangeEvent<float> evt)
        {
            WriteTuning();
        }

        private void OnGasNitrogenChanged(ChangeEvent<float> evt)
        {
            _runtime = ResolveRuntime();
            if (_runtime != null)
                _runtime.SetEditorBreathingGasNitrogenFraction(evt.newValue);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            _runtime = null;
            s_cachedRuntime = null;
            _statusCode = int.MinValue;
        }

        internal static ShinobuPhysiologyRuntime ResolveRuntime()
        {
            if (s_cachedRuntime != null)
                return s_cachedRuntime;

            double now = EditorApplication.timeSinceStartup;
            if (now < s_nextRuntimeResolveTime)
                return null;

            s_nextRuntimeResolveTime = now + 1.0;
            s_cachedRuntime = UnityEngine.Object.FindAnyObjectByType<ShinobuPhysiologyRuntime>();
            return s_cachedRuntime;
        }

        private static string ResolveTissueLabel(int index)
        {
            switch (index)
            {
                case 0: return "00";
                case 1: return "01";
                case 2: return "02";
                case 3: return "03";
                case 4: return "04";
                case 5: return "05";
                case 6: return "06";
                case 7: return "07";
                case 8: return "08";
                case 9: return "09";
                case 10: return "10";
                case 11: return "11";
                case 12: return "12";
                case 13: return "13";
                case 14: return "14";
                default: return "15";
            }
        }
    }

    [InitializeOnLoad]
    internal static class HaldaneanDecompressionSceneGizmo
    {
        static HaldaneanDecompressionSceneGizmo()
        {
            SceneView.duringSceneGui += Draw;
        }

        private static void Draw(SceneView view)
        {
            if (!Application.isPlaying)
                return;

            ShinobuPhysiologyRuntime runtime = HaldaneanDecompressionTunerWindow.ResolveRuntime();
            if (runtime == null || !runtime.TryGetDecompressionState(0, out DecompressionStateDTO state))
                return;

            Vector3 basePosition = runtime.transform.position + Vector3.up * 2f;
            float height = math.clamp(math.abs(state.GradientAdvantage), 0.1f, 2f);
            Handles.color = state.GradientAdvantage < 0f
                ? new Color(0.90f, 0.08f, 0.05f, 1f)
                : state.GradientAdvantage < 0.25f
                    ? new Color(0.95f, 0.74f, 0.10f, 1f)
                    : new Color(0.10f, 0.65f, 0.30f, 1f);
            Handles.DrawAAPolyLine(8f, basePosition, basePosition + Vector3.up * height);
        }
    }
}
