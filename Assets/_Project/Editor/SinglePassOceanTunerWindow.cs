#if UNITY_EDITOR
using Hecton8.Rendering.OceanSinglePass;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class SinglePassOceanTunerWindow : EditorWindow
    {
        private const string FoamThresholdKey = "H8.SinglePassOcean.FoamThreshold";
        private const string WakeLifespanKey = "H8.SinglePassOcean.WakeLifespan";
        private const string ShorelineFadeKey = "H8.SinglePassOcean.ShorelineFade";

        private Slider _foamThresholdSlider;
        private Slider _wakeLifespanSlider;
        private Slider _shorelineFadeSlider;
        private Toggle _wakePreviewToggle;
        private Image _wakePreviewImage;
        private Label _statusLabel;
        private OceanTelemetryGraphElement _telemetryGraph;

        [MenuItem("HECTON-8/Rendering/Single-Pass Ocean Tuner")]
        public static void Open()
        {
            GetWindow<SinglePassOceanTunerWindow>("Single-Pass Ocean");
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _foamThresholdSlider = CreateSlider("Jacobian Foam Threshold", 0.02f, 1.5f, EditorPrefs.GetFloat(FoamThresholdKey, 0.68f));
            _wakeLifespanSlider = CreateSlider("Wake Ripple Lifespan", 0.05f, 24f, EditorPrefs.GetFloat(WakeLifespanKey, 3.6f));
            _shorelineFadeSlider = CreateSlider("Shoreline Depth Fade", 0.1f, 128f, EditorPrefs.GetFloat(ShorelineFadeKey, 8f));
            _foamThresholdSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _wakeLifespanSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _shorelineFadeSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            root.Add(_foamThresholdSlider);
            root.Add(_wakeLifespanSlider);
            root.Add(_shorelineFadeSlider);

            Button applyButton = new Button(ApplyTuning) { text = "Apply Vault Tuning" };
            root.Add(applyButton);
            Button mockButton = new Button(GenerateMockState) { text = "Generate Mock Ocean State" };
            root.Add(mockButton);

            _telemetryGraph = new OceanTelemetryGraphElement();
            root.Add(_telemetryGraph);

            _wakePreviewToggle = new Toggle("Live Wake Texture") { value = false };
            root.Add(_wakePreviewToggle);

            _wakePreviewImage = new Image();
            _wakePreviewImage.scaleMode = ScaleMode.ScaleToFit;
            _wakePreviewImage.style.height = 220f;
            root.Add(_wakePreviewImage);

            _statusLabel = new Label();
            root.Add(_statusLabel);

            ApplyTuning();
            RefreshView();
            root.schedule.Execute(RefreshView).Every(250);
        }

        private static Slider CreateSlider(string label, float min, float max, float value)
        {
            return new Slider(label, min, max)
            {
                value = value,
                showInputField = true
            };
        }

        private void ApplyTuning()
        {
            if (_foamThresholdSlider == null || _wakeLifespanSlider == null || _shorelineFadeSlider == null)
                return;

            EditorPrefs.SetFloat(FoamThresholdKey, _foamThresholdSlider.value);
            EditorPrefs.SetFloat(WakeLifespanKey, _wakeLifespanSlider.value);
            EditorPrefs.SetFloat(ShorelineFadeKey, _shorelineFadeSlider.value);

            bool applied = OceanSinglePassRuntime.TrySetEditorTuning(
                _foamThresholdSlider.value,
                _wakeLifespanSlider.value,
                _shorelineFadeSlider.value);
            SetStatus(applied ? "Vault tuning updated in VisualSync route." : "Enter Play Mode with OceanSinglePassRuntime to update Vault tuning.");
        }

        private void RefreshView()
        {
            if (_telemetryGraph != null)
                _telemetryGraph.MarkDirtyRepaint();

            if (_wakePreviewImage == null)
                return;

            bool preview = _wakePreviewToggle != null && _wakePreviewToggle.value;
            if (!preview)
            {
                _wakePreviewImage.image = null;
                return;
            }

            if (OceanSinglePassRuntime.TryGetActiveWakeTexture(out Texture runtimeTexture, out _))
            {
                _wakePreviewImage.image = runtimeTexture;
                return;
            }

            _wakePreviewImage.image = Shader.GetGlobalTexture(H8OceanSinglePassShaderIds.WakeTextureId);
        }

        private void GenerateMockState()
        {
            bool generated = OceanSinglePassRuntime.GenerateMockOceanRenderState();
            SetStatus(generated ? "Mock ocean render state generated." : "Mock generation requires GlobalDataVault or active runtime.");
            RefreshView();
        }

        private void SetStatus(string value)
        {
            if (_statusLabel != null)
                _statusLabel.text = value;
        }

        private sealed class OceanTelemetryGraphElement : VisualElement
        {
            private const float GraphHeight = 112f;
            private const float MaxMicroseconds = 2000f;

            public OceanTelemetryGraphElement()
            {
                style.height = GraphHeight;
                style.marginTop = 8f;
                style.marginBottom = 8f;
                generateVisualContent += OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1f;
                painter.strokeColor = new Color(0.08f, 0.10f, 0.11f, 1f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Stroke();

                if (!OceanSinglePassRuntime.TryReadTelemetry(out NativeArray<OceanRenderTelemetryEntry> telemetry, out int cursor) ||
                    !telemetry.IsCreated ||
                    telemetry.Length <= 1)
                {
                    return;
                }

                int count = telemetry.Length;
                float step = rect.width / (count - 1);
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(0.14f, 0.78f, 0.92f, 1f);
                painter.BeginPath();
                for (int i = 0; i < count; i++)
                {
                    int sampleIndex = WrapIndex(cursor + i, count);
                    OceanRenderTelemetryEntry entry = telemetry[sampleIndex];
                    float sample = Mathf.Clamp01(entry.WakeComputeMicroseconds / MaxMicroseconds);
                    float x = rect.xMin + step * i;
                    float y = rect.yMax - sample * rect.height;
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();

                painter.lineWidth = 1f;
                painter.strokeColor = new Color(0.82f, 0.88f, 0.38f, 1f);
                painter.BeginPath();
                for (int i = 0; i < count; i++)
                {
                    int sampleIndex = WrapIndex(cursor + i, count);
                    OceanRenderTelemetryEntry entry = telemetry[sampleIndex];
                    float sample = Mathf.Clamp01(entry.DepthPassMicroseconds / MaxMicroseconds);
                    float x = rect.xMin + step * i;
                    float y = rect.yMax - sample * rect.height;
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }

            private static int WrapIndex(int value, int capacity)
            {
                int wrapped = value % capacity;
                return wrapped < 0 ? wrapped + capacity : wrapped;
            }
        }
    }
}
#endif
