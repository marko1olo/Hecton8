#if UNITY_EDITOR
using Hecton8.Core.Memory;
using Unity.Collections;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Core.Editor
{
    public sealed class TactileSynthesisTunerWindow : EditorWindow
    {
        private HapticTelemetryGraphElement _graph;
        private Slider _distanceSlider;
        private Slider _rumbleSlider;
        private Slider _maxAmplitudeSlider;

        [MenuItem("Hecton8/Tactile Synthesis Tuner")]
        private static void Open()
        {
            GetWindow<TactileSynthesisTunerWindow>("Tactile Synthesis");
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

            _graph = new HapticTelemetryGraphElement();
            _graph.style.height = 180;
            root.Add(_graph);

            _distanceSlider = BuildSlider("Distance Attenuation Curve", 0.25f, 4f, OnDistanceChanged);
            _rumbleSlider = BuildSlider("Global Rumble Multiplier", 0f, 2f, OnRumbleChanged);
            _maxAmplitudeSlider = BuildSlider("Max Motor Amplitude", 0f, 1f, OnMaxAmplitudeChanged);
            root.Add(_distanceSlider);
            root.Add(_rumbleSlider);
            root.Add(_maxAmplitudeSlider);
        }

        private void OnEditorUpdate()
        {
            if (_graph != null)
                _graph.MarkDirtyRepaint();

            if (!Application.isPlaying || !TryReadTuning(out NativeArray<HapticTuningDTO>.ReadOnly tuning) || tuning.Length <= 0)
                return;

            HapticTuningDTO dto = tuning[0];
            SetSliderWithoutNotify(_distanceSlider, dto.DistanceAttenuationCurve);
            SetSliderWithoutNotify(_rumbleSlider, dto.GlobalRumbleMultiplier);
            SetSliderWithoutNotify(_maxAmplitudeSlider, dto.MaxMotorAmplitude);
        }

        private static Slider BuildSlider(string label, float low, float high, EventCallback<ChangeEvent<float>> callback)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(callback);
            return slider;
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null && math.isfinite(value))
                slider.SetValueWithoutNotify(value);
        }

        private static void OnDistanceChanged(ChangeEvent<float> evt)
        {
            MutateTuning(evt.newValue, 0);
        }

        private static void OnRumbleChanged(ChangeEvent<float> evt)
        {
            MutateTuning(evt.newValue, 1);
        }

        private static void OnMaxAmplitudeChanged(ChangeEvent<float> evt)
        {
            MutateTuning(evt.newValue, 2);
        }

        private static void MutateTuning(float value, int field)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuHapticSynthesisTuning, out VaultGenerationHandle<HapticTuningDTO> handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.CoreDeterminism, out NativeArray<HapticTuningDTO> tuning))
            {
                return;
            }

            try
            {
                if (!tuning.IsCreated || tuning.Length <= 0)
                    return;

                HapticTuningDTO dto = tuning[0];
                switch (field)
                {
                    case 0:
                        dto.DistanceAttenuationCurve = Mathf.Max(0.0001f, value);
                        break;
                    case 1:
                        dto.GlobalRumbleMultiplier = Mathf.Max(0f, value);
                        break;
                    default:
                        dto.MaxMotorAmplitude = Mathf.Clamp01(value);
                        break;
                }

                tuning[0] = dto;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDeterminism);
            }
        }

        private static bool TryReadTuning(out NativeArray<HapticTuningDTO>.ReadOnly tuning)
        {
            tuning = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuHapticSynthesisTuning, out VaultGenerationHandle<HapticTuningDTO> handle) ||
                !vault.TryReadOnlyHandle(in handle, out tuning) ||
                !tuning.IsCreated)
            {
                tuning = default;
                return false;
            }

            return true;
        }

        private sealed class HapticTelemetryGraphElement : VisualElement
        {
            public HapticTelemetryGraphElement()
            {
                generateVisualContent += Draw;
            }

            private static void Draw(MeshGenerationContext context)
            {
                Rect rect = context.visualElement.contentRect;
                Painter2D painter = context.painter2D;
                painter.fillColor = new Color(0.03f, 0.035f, 0.04f, 1f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();

                IDataVault vault = GlobalRegistry.DataVault;
                if (vault == null ||
                    !vault.TryGetGenerationHandle(BufferID.ShinobuHapticSynthesisTelemetryRing, out VaultGenerationHandle<HapticTelemetryEntry> handle) ||
                    !vault.TryReadOnlyHandle(in handle, out NativeArray<HapticTelemetryEntry>.ReadOnly telemetry) ||
                    !telemetry.IsCreated ||
                    telemetry.Length <= 1)
                {
                    return;
                }

                DrawSeries(painter, rect, telemetry, 0, new Color(0.35f, 0.75f, 1f, 1f));
                DrawSeries(painter, rect, telemetry, 1, new Color(1f, 0.58f, 0.22f, 1f));
                DrawSeries(painter, rect, telemetry, 2, new Color(0.65f, 1f, 0.35f, 1f));
            }

            private static void DrawSeries(Painter2D painter, Rect rect, NativeArray<HapticTelemetryEntry>.ReadOnly telemetry, int mode, Color color)
            {
                painter.strokeColor = color;
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                bool started = false;
                int count = telemetry.Length;
                for (int i = 0; i < count; i++)
                {
                    HapticTelemetryEntry entry = telemetry[i];
                    float value = mode == 0
                        ? Mathf.Clamp01(entry.RawSignalCount * (1f / 32f))
                        : mode == 1
                            ? Mathf.Clamp01(entry.FinalLowFrequency01)
                            : Mathf.Clamp01(entry.FinalHighFrequency01);
                    float x = rect.xMin + (i / (float)(count - 1)) * rect.width;
                    float y = rect.yMax - value * rect.height;
                    if (!started)
                    {
                        painter.MoveTo(new Vector2(x, y));
                        started = true;
                    }
                    else
                    {
                        painter.LineTo(new Vector2(x, y));
                    }
                }

                painter.Stroke();
            }
        }
    }
}
#endif
