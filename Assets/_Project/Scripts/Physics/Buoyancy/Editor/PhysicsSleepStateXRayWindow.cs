#if UNITY_EDITOR
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physics.Editor
{
    /// <summary>
    /// Editor-only live view for SHINOBU_249 buoyancy sleep telemetry and tuning.
    /// </summary>
    /// <remarks>
    /// Mutates Vault-backed tuning only in editor play mode; no player-build runtime surface is introduced.
    /// </remarks>
    public sealed class PhysicsSleepStateXRayWindow : EditorWindow
    {
        private Slider _baseSleepThreshold;
        private Slider _restingFrameCount;
        private Slider _currentStirThreshold;
        private SleepStackedAreaElement _chart;
        private double _nextReadTime;

        /// <summary>Opens the sleep-state telemetry window.</summary>
        [MenuItem("HECTON-8/Physics/Physics Sleep State X-Ray")]
        public static void Open()
        {
            GetWindow<PhysicsSleepStateXRayWindow>("Sleep X-Ray");
        }

        /// <summary>Builds the UI Toolkit controls for the editor-only window.</summary>
        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _baseSleepThreshold = AddSlider("Base Sleep Threshold", 0.000001f, 0.25f);
            _restingFrameCount = AddSlider("Resting Frame Count", 1f, 255f);
            _currentStirThreshold = AddSlider("Current Stir Threshold", 0.0001f, 4f);

            _chart = new SleepStackedAreaElement();
            _chart.style.height = 144;
            _chart.style.marginTop = 8;
            rootVisualElement.Add(_chart);

            _baseSleepThreshold.RegisterValueChangedCallback(_ => WriteToVault());
            _restingFrameCount.RegisterValueChangedCallback(_ => WriteToVault());
            _currentStirThreshold.RegisterValueChangedCallback(_ => WriteToVault());
            ReadFromVault();
        }

        private Slider AddSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            slider.style.marginBottom = 4;
            rootVisualElement.Add(slider);
            return slider;
        }

        private void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextReadTime)
                return;

            _nextReadTime = now + 0.1d;
            ReadFromVault();
            _chart?.MarkDirtyRepaint();
        }

        private void ReadFromVault()
        {
            if (!BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime) ||
                !runtime.TryOpenSleepTelemetryEditorViews(
                    out NativeArray<BuoyancyTuningDTO> tuning,
                    out _,
                    out _,
                    out NativeArray<BuoyancySleepSdfConfigDTO> sdfConfig))
            {
                return;
            }

            BuoyancyTuningDTO tuningValue = tuning[0];
            BuoyancySleepSdfConfigDTO configValue = sdfConfig[0];
            int restFrames = (int)((configValue.Flags & BuoyancyDisplacementConstants.SleepSdfConfigRestFrameOverrideMask) >>
                                   BuoyancyDisplacementConstants.SleepSdfConfigRestFrameOverrideShift);
            SetSliderWithoutNotify(_baseSleepThreshold, math.max(0.000001f, tuningValue.SleepSpeedSq));
            SetSliderWithoutNotify(_restingFrameCount, restFrames > 0 ? restFrames : 6);
            float stirThresholdSq = math.max(0.0001f, configValue.AmbientStirThresholdSq);
            SetSliderWithoutNotify(_currentStirThreshold, stirThresholdSq * math.rsqrt(math.max(stirThresholdSq, 0.0001f)));
        }

        private void WriteToVault()
        {
            if (!BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime) ||
                !runtime.TryOpenSleepTelemetryEditorViews(
                    out NativeArray<BuoyancyTuningDTO> tuning,
                    out _,
                    out _,
                    out NativeArray<BuoyancySleepSdfConfigDTO> sdfConfig))
            {
                return;
            }

            BuoyancyTuningDTO tuningValue = tuning[0];
            tuningValue.SleepSpeedSq = math.max(0.000001f, _baseSleepThreshold.value);
            tuning[0] = tuningValue;

            BuoyancySleepSdfConfigDTO configValue = sdfConfig[0];
            int restFrames = math.clamp((int)math.round(_restingFrameCount.value), 1, 255);
            uint packedRestFrames = (uint)restFrames << BuoyancyDisplacementConstants.SleepSdfConfigRestFrameOverrideShift;
            configValue.Flags = (configValue.Flags & ~BuoyancyDisplacementConstants.SleepSdfConfigRestFrameOverrideMask) | packedRestFrames | BuoyancyDisplacementConstants.FlagActive;
            float stir = math.max(0.0001f, _currentStirThreshold.value);
            configValue.AmbientStirThresholdSq = stir * stir;
            sdfConfig[0] = configValue;
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private sealed class SleepStackedAreaElement : VisualElement
        {
            public SleepStackedAreaElement()
            {
                generateVisualContent += OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                DrawBackground(painter, rect);

                if (!BuoyancyDisplacementRuntime.TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime) ||
                    !runtime.TryOpenSleepTelemetryEditorViews(
                        out _,
                        out NativeArray<SleepStateTelemetryEntry> telemetry,
                        out NativeArray<int> cursor,
                        out _) ||
                    telemetry.Length <= 0 ||
                    cursor.Length <= 0)
                {
                    DrawBaseline(painter, rect);
                    return;
                }

                int count = telemetry.Length;
                int cursorIndex = math.clamp(cursor[0], 0, count - 1);
                int start = (cursorIndex + 1) % count;
                painter.fillColor = new Color(0.02f, 0.1f, 0.35f, 0.86f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
                for (int i = 0; i < count; i++)
                {
                    int telemetryIndex = (start + i) % count;
                    SleepStateTelemetryEntry entry = telemetry[telemetryIndex];
                    float active = math.max(1f, entry.ActiveObjects);
                    float sleeping01 = math.saturate(entry.SleepingObjects / active);
                    float x = rect.xMin + rect.width * (i / math.max(1f, count - 1f));
                    float y = rect.yMax - rect.height * sleeping01;
                    painter.LineTo(new Vector2(x, y));
                }
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.ClosePath();
                painter.Fill();

                painter.strokeColor = new Color(0.05f, 0.95f, 0.25f, 1f);
                painter.lineWidth = 1.25f;
                painter.BeginPath();
                for (int i = 0; i < count; i++)
                {
                    int telemetryIndex = (start + i) % count;
                    SleepStateTelemetryEntry entry = telemetry[telemetryIndex];
                    float active = math.max(1f, entry.ActiveObjects);
                    float awake01 = math.saturate((entry.ActiveObjects - entry.SleepingObjects) / active);
                    float x = rect.xMin + rect.width * (i / math.max(1f, count - 1f));
                    float y = rect.yMax - rect.height * awake01;
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }
                painter.Stroke();
            }

            private static void DrawBackground(Painter2D painter, Rect rect)
            {
                painter.fillColor = new Color(0.02f, 0.02f, 0.025f, 1f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();
            }

            private static void DrawBaseline(Painter2D painter, Rect rect)
            {
                painter.strokeColor = new Color(0.4f, 0.45f, 0.5f, 1f);
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.Stroke();
            }
        }
    }
}
#endif
