#if UNITY_EDITOR
using Hecton8.Core;
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
    /// UI Toolkit control surface for vault-backed ocean kinematics telemetry and tuning.
    /// </summary>
    public sealed class OceanPhysicsTunerWindow : EditorWindow
    {
        private const int HistogramBars = 64;
        private readonly VisualElement[] _bars = new VisualElement[HistogramBars];
        private Label _statusLabel;
        private Label _qualityLabel;
        private Slider _depthSlider;
        private SliderInt _octaveSlider;
        private Slider _amplitudeSlider;
        private VisualElement _histogram;

        [MenuItem("Hecton8/Physics/Ocean Physics Tuner")]
        public static void Open()
        {
            GetWindow<OceanPhysicsTunerWindow>("Ocean Physics Tuner");
        }

        private void OnEnable()
        {
            BuildUi();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
        }

        private void BuildUi()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            _statusLabel = new Label("Vault not resolved.");
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_statusLabel);

            _qualityLabel = new Label("GlobalQualityWeight: n/a");
            root.Add(_qualityLabel);

            _depthSlider = new Slider("Depth Cull Threshold", 0f, 200f);
            _depthSlider.RegisterValueChangedCallback(evt => MutateTuning(depth: evt.newValue, octaves: -1, amplitude: float.NaN));
            root.Add(_depthSlider);

            _octaveSlider = new SliderInt("Max Octaves", 1, OceanKinematicsConstants.WaveCapacity);
            _octaveSlider.RegisterValueChangedCallback(evt => MutateTuning(depth: float.NaN, octaves: evt.newValue, amplitude: float.NaN));
            root.Add(_octaveSlider);

            _amplitudeSlider = new Slider("Amplitude Multiplier", 0f, 4f);
            _amplitudeSlider.RegisterValueChangedCallback(evt => MutateTuning(depth: float.NaN, octaves: -1, amplitude: evt.newValue));
            root.Add(_amplitudeSlider);

            _histogram = new VisualElement();
            _histogram.style.height = 120;
            _histogram.style.flexDirection = FlexDirection.Row;
            _histogram.style.alignItems = Align.FlexEnd;
            _histogram.style.marginTop = 10;
            _histogram.style.borderTopWidth = 1;
            _histogram.style.borderBottomWidth = 1;
            _histogram.style.borderLeftWidth = 1;
            _histogram.style.borderRightWidth = 1;
            _histogram.style.borderTopColor = Color.gray;
            _histogram.style.borderBottomColor = Color.gray;
            _histogram.style.borderLeftColor = Color.gray;
            _histogram.style.borderRightColor = Color.gray;
            root.Add(_histogram);

            for (int i = 0; i < HistogramBars; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.width = 4;
                bar.style.height = 1;
                bar.style.marginLeft = 1;
                bar.style.backgroundColor = new Color(0.1f, 0.55f, 0.95f, 1f);
                _histogram.Add(bar);
                _bars[i] = bar;
            }
        }

        private void Tick()
        {
            RefreshFromVault();
        }

        private void RefreshFromVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !OceanKinematicsVaultRuntime.EnsureBuffers(vault, out OceanKinematicsVaultRuntime.Views views))
            {
                _statusLabel.text = "Vault not resolved. Enter Play Mode or bind GlobalDataVault.";
                return;
            }

            OceanKinematicsTuningDTO tuning = views.Tuning.IsCreated && views.Tuning.Length > 0 ? views.Tuning[0] : default;
            _qualityLabel.text = "GlobalQualityWeight: " + tuning.GlobalQualityWeight.ToString("0.000");
            _statusLabel.text = "Telemetry frames: " + views.TelemetryRing.Length.ToString() + " | Cursor: " +
                                (views.TelemetryCursor.IsCreated && views.TelemetryCursor.Length > 0 ? views.TelemetryCursor[0].ToString() : "0");

            SetSliderWithoutNotify(_depthSlider, tuning.DepthCullingThresholdMeters);
            _octaveSlider.SetValueWithoutNotify(math.clamp(tuning.MaxOctaveLimit, 1, OceanKinematicsConstants.WaveCapacity));
            SetSliderWithoutNotify(_amplitudeSlider, tuning.WaveAmplitudeMultiplier);
            RefreshHistogram(views.TelemetryRing);
        }

        private void RefreshHistogram(NativeArray<OceanKinematicsTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return;

            int count = math.min(HistogramBars, telemetry.Length);
            float maxMicros = 1f;
            for (int i = 0; i < count; i++)
                maxMicros = math.max(maxMicros, telemetry[i].BurstExecutionMicros);

            for (int i = 0; i < HistogramBars; i++)
            {
                float height = 1f;
                Color color = new Color(0.1f, 0.55f, 0.95f, 1f);
                if (i < count)
                {
                    OceanKinematicsTelemetryEntry entry = telemetry[i];
                    height = math.clamp((entry.BurstExecutionMicros / maxMicros) * 110f, 1f, 110f);
                    if (entry.BurstExecutionMicros > 1000f || (entry.Flags & (1u << 30)) != 0u)
                        color = new Color(0.9f, 0.15f, 0.1f, 1f);
                }

                _bars[i].style.height = height;
                _bars[i].style.backgroundColor = color;
            }
        }

        private void MutateTuning(float depth, int octaves, float amplitude)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !OceanKinematicsVaultRuntime.EnsureBuffers(vault, out OceanKinematicsVaultRuntime.Views views) ||
                !views.Tuning.IsCreated ||
                views.Tuning.Length == 0)
            {
                return;
            }

            OceanKinematicsTuningDTO tuning = views.Tuning[0];
            if (math.isfinite(depth))
                tuning.DepthCullingThresholdMeters = math.max(0f, depth);

            if (octaves > 0)
                tuning.MaxOctaveLimit = math.clamp(octaves, 1, OceanKinematicsConstants.WaveCapacity);

            if (math.isfinite(amplitude))
                tuning.WaveAmplitudeMultiplier = math.max(0f, amplitude);

            views.Tuning[0] = tuning;
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null && math.isfinite(value))
                slider.SetValueWithoutNotify(value);
        }
    }
}
#endif
