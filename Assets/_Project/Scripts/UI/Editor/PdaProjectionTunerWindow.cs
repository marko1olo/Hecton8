#if UNITY_EDITOR
using Hecton8.UI;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.UI.Editor
{
    public sealed class PdaProjectionTunerWindow : EditorWindow
    {
        private TelemetryGraphElement _graph;
        private Slider _glassSlider;
        private Slider _curvatureSlider;
        private Slider _qualitySlider;

        [MenuItem("Hecton8/UX/Diegetic UX Tuner")]
        public static void Open()
        {
            GetWindow<PdaProjectionTunerWindow>("Diegetic UX Tuner");
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            _glassSlider = CreateSlider("Glass Refraction Index", 1f, 1.8f, 1.46f);
            _curvatureSlider = CreateSlider("Screen Curvature Scalar", 0f, 1f, 0.28f);
            _qualitySlider = CreateSlider("Global Quality Override", -1f, 1f, -1f);
            _graph = new TelemetryGraphElement();
            _graph.style.height = 128;
            _graph.style.marginTop = 8;

            rootVisualElement.Add(_glassSlider);
            rootVisualElement.Add(_curvatureSlider);
            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_graph);

            _glassSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _curvatureSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _qualitySlider.RegisterValueChangedCallback(_ => ApplyTuning());
        }

        private void OnEnable()
        {
            EditorApplication.update += TickEditor;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickEditor;
        }

        private void TickEditor()
        {
            if (_graph == null)
                return;

            if (WristHologramHudRuntime.TryGetActivePdaProjectionTuning(out PdaProjectionTuningDTO tuning))
            {
                SetSliderWithoutNotify(_glassSlider, tuning.Params1.y);
                SetSliderWithoutNotify(_curvatureSlider, tuning.Params1.z);
                SetSliderWithoutNotify(_qualitySlider, tuning.Params1.x);
            }

            if (WristHologramHudRuntime.TryGetActivePdaProjectionTelemetry(
                    out NativeArray<PdaProjectionTelemetryEntry>.ReadOnly telemetry,
                    out int cursor))
            {
                _graph.Bind(telemetry, cursor);
            }
        }

        private static Slider CreateSlider(string label, float low, float high, float value)
        {
            Slider slider = new Slider(label, low, high)
            {
                value = value,
                showInputField = true
            };
            slider.style.marginBottom = 4;
            return slider;
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private void ApplyTuning()
        {
            if (_glassSlider == null || _curvatureSlider == null || _qualitySlider == null)
                return;

            WristHologramHudRuntime.TrySetActivePdaProjectionTuning(
                _glassSlider.value,
                _curvatureSlider.value,
                _qualitySlider.value);
        }

        private sealed class TelemetryGraphElement : VisualElement
        {
            private const int Capacity = 300;
            private readonly float[] _samples = new float[Capacity]; // COLD ALLOC: editor graph cache - owner: SHINOBU_348
            private int _count;

            public TelemetryGraphElement()
            {
                generateVisualContent += DrawGraph;
            }

            public void Bind(NativeArray<PdaProjectionTelemetryEntry>.ReadOnly telemetry, int cursor)
            {
                if (!telemetry.IsCreated)
                    return;

                int count = Mathf.Min(telemetry.Length, Capacity);
                int start = cursor - count;
                while (start < 0)
                    start += telemetry.Length;

                for (int i = 0; i < count; i++)
                {
                    int source = (start + i) % telemetry.Length;
                    _samples[i] = telemetry[source].JobMicrosecondsQ16 * (1f / 16f);
                }

                _count = count;
                MarkDirtyRepaint();
            }

            private void DrawGraph(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.fillColor = new Color(0.02f, 0.035f, 0.04f, 1f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();

                if (_count <= 1)
                    return;

                painter.strokeColor = new Color(0.1f, 0.95f, 0.58f, 1f);
                painter.lineWidth = 2f;
                painter.BeginPath();
                for (int i = 0; i < _count; i++)
                {
                    float x = Mathf.Lerp(rect.xMin, rect.xMax, i / (float)(_count - 1));
                    float y = Mathf.Lerp(rect.yMax, rect.yMin, Mathf.Clamp01(_samples[i] / 100f));
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }
}
#endif
