#if UNITY_EDITOR
using Hecton8.Physics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.UIElements;

namespace Hecton8.Editor.Physics
{
    public sealed class HydrodynamicBuoyancyTunerWindow : EditorWindow
    {
        private Slider _qualitySlider;
        private Slider _densitySlider;
        private Slider _gravitySlider;
        private Slider _linearDragSlider;
        private Slider _quadraticDragSlider;
        private Slider _surfaceDampeningSlider;
        private Slider _flowForceSlider;
        private FloatField _seafloorField;
        private IntegerField _activeCountField;
        private Label _telemetryLabel;
        private readonly char[] _readoutBuffer = new char[256];
        private int _lastEvaluated = int.MinValue;
        private int _lastSleeping = int.MinValue;
        private int _lastPackets = int.MinValue;
        private int _lastNonFinite = int.MinValue;
        private int _lastComputeCentis = int.MinValue;
        private int _lastDepthCentis = int.MinValue;
        private int _lastQualityCentis = int.MinValue;

        [MenuItem("Hecton8/Physics/Hydrodynamic Buoyancy Tuner")]
        public static void Open()
        {
            GetWindow<HydrodynamicBuoyancyTunerWindow>("Hydrodynamic Buoyancy");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            Button resolveButton = new Button(ResolveAndPullTuning) { text = "Resolve Vault" };
            Button mockButton = new Button(GenerateMock) { text = "Generate Mock Objects" };
            Button csvButton = new Button(LoadCsv) { text = "Load item_volume_specs.csv" };
            rootVisualElement.Add(resolveButton);
            rootVisualElement.Add(mockButton);
            rootVisualElement.Add(csvButton);

            _qualitySlider = new Slider("Global Quality Weight", 0f, 1f);
            _densitySlider = new Slider("Global Water Density", 900f, 1160f);
            _gravitySlider = new Slider("Gravity Multiplier", 0f, 2f);
            _linearDragSlider = new Slider("Base Linear Drag", 0f, 12f);
            _quadraticDragSlider = new Slider("Base Quadratic Drag", 0f, 4f);
            _surfaceDampeningSlider = new Slider("Surface Dampening", 0f, 1f);
            _flowForceSlider = new Slider("Abyssal Flow Force", 0f, 2f);
            _seafloorField = new FloatField("Seafloor AUP Y");
            _activeCountField = new IntegerField("Active Object Count");

            _qualitySlider.RegisterValueChangedCallback(_ => PushTuning());
            _densitySlider.RegisterValueChangedCallback(_ => PushTuning());
            _gravitySlider.RegisterValueChangedCallback(_ => PushTuning());
            _linearDragSlider.RegisterValueChangedCallback(_ => PushTuning());
            _quadraticDragSlider.RegisterValueChangedCallback(_ => PushTuning());
            _surfaceDampeningSlider.RegisterValueChangedCallback(_ => PushTuning());
            _flowForceSlider.RegisterValueChangedCallback(_ => PushTuning());
            _seafloorField.RegisterValueChangedCallback(_ => PushTuning());
            _activeCountField.RegisterValueChangedCallback(_ => PushTuning());

            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_densitySlider);
            rootVisualElement.Add(_gravitySlider);
            rootVisualElement.Add(_linearDragSlider);
            rootVisualElement.Add(_quadraticDragSlider);
            rootVisualElement.Add(_surfaceDampeningSlider);
            rootVisualElement.Add(_flowForceSlider);
            rootVisualElement.Add(_seafloorField);
            rootVisualElement.Add(_activeCountField);

            _telemetryLabel = new Label("Runtime not resolved.");
            _telemetryLabel.style.marginTop = 8;
            rootVisualElement.Add(_telemetryLabel);

            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
            ResolveAndPullTuning();
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorTick;
        }

        private void EditorTick()
        {
            UpdateTelemetryReadout();
        }

        private void ResolveAndPullTuning()
        {
            if (!TryResolveViews(
                    out NativeArray<BuoyancyTuningDTO>.ReadOnly tuning,
                    out _,
                    out _,
                    out _))
            {
                if (_telemetryLabel != null)
                    _telemetryLabel.text = "Play Mode runtime and GlobalDataVault required.";
                return;
            }

            BuoyancyTuningDTO value = tuning[0];
            _qualitySlider.SetValueWithoutNotify(math.saturate(value.GlobalQualityWeight));
            _densitySlider.SetValueWithoutNotify(math.clamp(value.WaterDensityKgPerM3, 900f, 1160f));
            _gravitySlider.SetValueWithoutNotify(math.clamp(value.GravityMetersPerSecondSq / BuoyancyDisplacementConstants.DefaultGravityMetersPerSecondSq, 0f, 2f));
            _linearDragSlider.SetValueWithoutNotify(math.clamp(value.LinearDragCoefficient, 0f, 12f));
            _quadraticDragSlider.SetValueWithoutNotify(math.clamp(value.QuadraticDragCoefficient, 0f, 4f));
            _surfaceDampeningSlider.SetValueWithoutNotify(math.saturate(value.SurfaceDampening));
            _flowForceSlider.SetValueWithoutNotify(math.clamp(value.FlowForceCoefficient, 0f, 2f));
            _seafloorField.SetValueWithoutNotify(value.SeafloorAUPY);
            _activeCountField.SetValueWithoutNotify(math.clamp(value.ActiveStateCount, 0, BuoyancyDisplacementConstants.StateCapacity));
            UpdateTelemetryReadout();
        }

        private void PushTuning()
        {
            if (!TryResolveViews(
                    out NativeArray<BuoyancyTuningDTO>.ReadOnly tuning,
                    out _,
                    out _,
                    out _))
            {
                return;
            }

            BuoyancyTuningDTO value = tuning[0];
            value.GlobalQualityWeight = math.saturate(_qualitySlider.value);
            value.WaterDensityKgPerM3 = math.clamp(_densitySlider.value, 900f, 1160f);
            value.GravityMetersPerSecondSq = BuoyancyDisplacementConstants.DefaultGravityMetersPerSecondSq * math.clamp(_gravitySlider.value, 0f, 2f);
            value.LinearDragCoefficient = math.max(0f, _linearDragSlider.value);
            value.QuadraticDragCoefficient = math.max(0f, _quadraticDragSlider.value);
            value.SurfaceDampening = math.saturate(_surfaceDampeningSlider.value);
            value.FlowForceCoefficient = math.max(0f, _flowForceSlider.value);
            value.SeafloorAUPY = _seafloorField.value;
            value.ActiveStateCount = math.clamp(_activeCountField.value, 0, BuoyancyDisplacementConstants.StateCapacity);
            if (BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime))
                runtime.TryApplyEditorTuning(value);
        }

        private void GenerateMock()
        {
            if (BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime))
                runtime.GenerateMockBuoyantObjects();
            UpdateTelemetryReadout();
        }

        private void LoadCsv()
        {
            if (BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime))
                runtime.TryLoadMaterialVolumesCsv();
            UpdateTelemetryReadout();
        }

        private void UpdateTelemetryReadout()
        {
            if (_telemetryLabel == null)
                return;

            if (!TryResolveViews(
                    out _,
                    out NativeArray<BuoyancyCounterDTO>.ReadOnly counters,
                    out NativeArray<BuoyancyTelemetryEntry>.ReadOnly telemetry,
                    out NativeArray<int>.ReadOnly cursor))
            {
                _telemetryLabel.text = "Play Mode runtime and GlobalDataVault required.";
                return;
            }

            BuoyancyCounterDTO counter = counters[0];
            int slot = math.max(0, cursor[0] - 1) % telemetry.Length;
            BuoyancyTelemetryEntry entry = telemetry[slot];
            int computeCentis = ToCentis(counter.ComputeMicros);
            int depthCentis = ToCentis(counter.MaxDepthMeters);
            int qualityCentis = ToCentis(entry.GlobalQualityWeight);
            if (counter.EvaluatedObjects == _lastEvaluated &&
                counter.SleepingObjects == _lastSleeping &&
                counter.ForcePackets == _lastPackets &&
                counter.NonFiniteCount == _lastNonFinite &&
                computeCentis == _lastComputeCentis &&
                depthCentis == _lastDepthCentis &&
                qualityCentis == _lastQualityCentis)
            {
                return;
            }

            _lastEvaluated = counter.EvaluatedObjects;
            _lastSleeping = counter.SleepingObjects;
            _lastPackets = counter.ForcePackets;
            _lastNonFinite = counter.NonFiniteCount;
            _lastComputeCentis = computeCentis;
            _lastDepthCentis = depthCentis;
            _lastQualityCentis = qualityCentis;

            int write = 0;
            AppendLiteral(_readoutBuffer, ref write, "Active: ");
            AppendInt(_readoutBuffer, ref write, counter.EvaluatedObjects);
            AppendLiteral(_readoutBuffer, ref write, "  Sleeping: ");
            AppendInt(_readoutBuffer, ref write, counter.SleepingObjects);
            AppendLiteral(_readoutBuffer, ref write, "  Packets: ");
            AppendInt(_readoutBuffer, ref write, counter.ForcePackets);
            AppendLiteral(_readoutBuffer, ref write, "  NonFinite: ");
            AppendInt(_readoutBuffer, ref write, counter.NonFiniteCount);
            AppendChar(_readoutBuffer, ref write, '\n');
            AppendLiteral(_readoutBuffer, ref write, "Compute us: ");
            AppendFixed2(_readoutBuffer, ref write, computeCentis);
            AppendLiteral(_readoutBuffer, ref write, "  Max depth: ");
            AppendFixed2(_readoutBuffer, ref write, depthCentis);
            AppendLiteral(_readoutBuffer, ref write, "  Q: ");
            AppendFixed2(_readoutBuffer, ref write, qualityCentis);
            _telemetryLabel.text = new string(_readoutBuffer, 0, write);
        }

        private static void AppendLiteral(char[] buffer, ref int offset, string value)
        {
            for (int i = 0; i < value.Length && offset < buffer.Length; i++)
                buffer[offset++] = value[i];
        }

        private static void AppendChar(char[] buffer, ref int offset, char value)
        {
            if (offset < buffer.Length)
                buffer[offset++] = value;
        }

        private static void AppendInt(char[] buffer, ref int offset, int value)
        {
            if (offset >= buffer.Length)
                return;

            if (value == 0)
            {
                buffer[offset++] = '0';
                return;
            }

            long remaining = value;
            if (remaining < 0L)
            {
                buffer[offset++] = '-';
                remaining = -remaining;
            }

            int start = offset;
            while (remaining > 0L && offset < buffer.Length)
            {
                buffer[offset++] = (char)('0' + remaining % 10L);
                remaining /= 10L;
            }

            int end = offset - 1;
            while (start < end)
            {
                char swap = buffer[start];
                buffer[start] = buffer[end];
                buffer[end] = swap;
                start++;
                end--;
            }
        }

        private static int ToCentis(float value)
        {
            if (!math.isfinite(value))
                return 0;

            return (int)math.round(value * 100f);
        }

        private static void AppendFixed2(char[] buffer, ref int offset, int centis)
        {
            if (centis == 0)
            {
                AppendLiteral(buffer, ref offset, "0.00");
                return;
            }

            int scaled = math.abs(centis);
            if (centis < 0)
                AppendChar(buffer, ref offset, '-');

            AppendInt(buffer, ref offset, scaled / 100);
            AppendChar(buffer, ref offset, '.');
            int fractional = scaled % 100;
            AppendChar(buffer, ref offset, (char)('0' + fractional / 10));
            AppendChar(buffer, ref offset, (char)('0' + fractional % 10));
        }

        private static bool TryResolveViews(
            out NativeArray<BuoyancyTuningDTO>.ReadOnly tuning,
            out NativeArray<BuoyancyCounterDTO>.ReadOnly counters,
            out NativeArray<BuoyancyTelemetryEntry>.ReadOnly telemetry,
            out NativeArray<int>.ReadOnly cursor)
        {
            tuning = default;
            counters = default;
            telemetry = default;
            cursor = default;
            return BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime) &&
                   runtime.TryOpenEditorViews(out tuning, out counters, out telemetry, out cursor);
        }
    }
}
#endif
