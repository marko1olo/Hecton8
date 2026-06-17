#if UNITY_EDITOR
using System.Globalization;
using Hecton8.Power;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Power.Editor
{
    public sealed class BatteryLogisticsXRayWindow : EditorWindow
    {
        private const int HistogramBars = 64;
        private readonly VisualElement[] _bars = new VisualElement[HistogramBars];
        private Label _stateLabel;
        private Label _telemetryLabel;
        private Slider _maxRateSlider;
        private Slider _exponentSlider;
        private Slider _qualityOverrideSlider;

        [MenuItem("Hecton8/Power/Battery Logistics X-Ray")]
        public static void Open()
        {
            BatteryLogisticsXRayWindow window = GetWindow<BatteryLogisticsXRayWindow>();
            window.titleContent = new GUIContent("Battery Logistics X-Ray");
            window.minSize = new Vector2(420f, 260f);
        }

        private void OnEnable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _stateLabel = new Label("runtime: offline");
            root.Add(_stateLabel);

            _telemetryLabel = new Label("telemetry: none");
            root.Add(_telemetryLabel);

            _maxRateSlider = new Slider("GlobalMaxChargeRate", 0f, 0.5f) { value = BatteryChargerLogisticsConstants.DefaultMaxChargeRate01PerSecond };
            _exponentSlider = new Slider("EfficiencyCurveExponent", 0.05f, 2f) { value = BatteryChargerLogisticsConstants.DefaultEfficiencyExponent };
            _qualityOverrideSlider = new Slider("GlobalQualityWeight Override", -1f, 1f) { value = -1f };
            _maxRateSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _exponentSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _qualityOverrideSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            root.Add(_maxRateSlider);
            root.Add(_exponentSlider);
            root.Add(_qualityOverrideSlider);

            VisualElement histogram = new VisualElement();
            histogram.style.height = 96;
            histogram.style.flexDirection = FlexDirection.Row;
            histogram.style.alignItems = Align.FlexEnd;
            histogram.style.marginTop = 8;
            histogram.style.backgroundColor = new Color(0.06f, 0.06f, 0.06f, 1f);
            root.Add(histogram);

            for (int i = 0; i < HistogramBars; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.width = 5;
                bar.style.marginRight = 1;
                bar.style.height = 1;
                bar.style.backgroundColor = new Color(0.2f, 0.85f, 0.65f, 1f);
                histogram.Add(bar);
                _bars[i] = bar;
            }

            Button dumpButton = new Button(Tick) { text = "Refresh" };
            root.Add(dumpButton);
        }

        private void ApplyTuning()
        {
            if (_maxRateSlider == null || _exponentSlider == null || _qualityOverrideSlider == null)
                return;

            BatteryChargerLogisticsRuntime.TryApplyEditorTuning(
                _maxRateSlider.value,
                _exponentSlider.value,
                _qualityOverrideSlider.value);
        }

        private void Tick()
        {
            if (_stateLabel == null || _telemetryLabel == null)
                return;

            if (!BatteryChargerLogisticsRuntime.TryReadEditorState(out int activeCount, out float quality, out float cadenceHz, out float lastFenceElapsedUs))
            {
                _stateLabel.text = "runtime: offline";
                _telemetryLabel.text = "telemetry: none";
                ClearBars();
                return;
            }

            _stateLabel.text = "links: " + activeCount +
                " | q: " + quality.ToString("0.000", CultureInfo.InvariantCulture) +
                " | cadence: " + cadenceHz.ToString("0.0", CultureInfo.InvariantCulture) +
                " Hz | fence: " + lastFenceElapsedUs.ToString("0.0", CultureInfo.InvariantCulture) + " us";
            if (!BatteryChargerLogisticsRuntime.TryGetTelemetryReadOnly(out NativeArray<ChargerTelemetryEntry>.ReadOnly telemetry, out int cursor) ||
                telemetry.Length == 0)
            {
                _telemetryLabel.text = "telemetry: empty";
                ClearBars();
                return;
            }

            int latest = cursor <= 0 ? 0 : (cursor - 1) % telemetry.Length;
            ChargerTelemetryEntry entry = telemetry[latest];
            _telemetryLabel.text = "draw: " + entry.TotalEnergyDrawn.ToString("0.000", CultureInfo.InvariantCulture) +
                " | atomic failures: " + entry.AtomicLockFailures +
                " | skipped: " + entry.SkippedCadenceFrames +
                " | full: " + entry.FullLinks +
                " | unpowered: " + entry.UnpoweredLinks;
            DrawHistogram(telemetry, cursor);
        }

        private void DrawHistogram(NativeArray<ChargerTelemetryEntry>.ReadOnly telemetry, int cursor)
        {
            float maxDraw = 0.001f;
            for (int i = 0; i < HistogramBars; i++)
            {
                int index = ResolveTelemetryIndex(cursor, telemetry.Length, i);
                maxDraw = math.max(maxDraw, telemetry[index].TotalEnergyDrawn);
            }

            for (int i = 0; i < HistogramBars; i++)
            {
                int index = ResolveTelemetryIndex(cursor, telemetry.Length, i);
                ChargerTelemetryEntry entry = telemetry[index];
                float height = math.saturate(entry.TotalEnergyDrawn / maxDraw) * 92f;
                VisualElement bar = _bars[i];
                if (bar == null)
                    continue;

                Color barColor = new Color(0.2f, 0.85f, 0.65f, 1f);
                if ((entry.Flags & BatteryChargerLogisticsConstants.TelemetryFlagSkippedCadence) != 0u)
                    barColor = new Color(0.2f, 0.55f, 1f, 1f);
                if (entry.AtomicLockFailures > 0)
                    barColor = new Color(1f, 0.35f, 0.18f, 1f);

                bar.style.height = math.max(1f, height);
                bar.style.backgroundColor = barColor;
            }
        }

        private static int ResolveTelemetryIndex(int cursor, int length, int barIndex)
        {
            int start = math.max(0, cursor - HistogramBars);
            int value = start + barIndex;
            int index = value % length;
            return index < 0 ? index + length : index;
        }

        private void ClearBars()
        {
            for (int i = 0; i < _bars.Length; i++)
            {
                if (_bars[i] != null)
                    _bars[i].style.height = 1;
            }
        }
    }
}
#endif
