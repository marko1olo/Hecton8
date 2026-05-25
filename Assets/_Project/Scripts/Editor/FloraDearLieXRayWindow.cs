using System.Globalization;
using Hecton8.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    internal sealed class FloraDearLieXRayWindow : EditorWindow
    {
        private const int TelemetryGraphCapacity = 300;

        private readonly int[] _frameScratch = new int[TelemetryGraphCapacity];
        private readonly int[] _destroyedScratch = new int[TelemetryGraphCapacity];
        private readonly int[] _vfxScratch = new int[TelemetryGraphCapacity];
        private readonly int[] _regenScratch = new int[TelemetryGraphCapacity];

        private Label _status;
        private Label _counts;
        private Label _quality;
        private Slider _radiusSlider;
        private Slider _regenSlider;
        private Slider _qualityOverrideSlider;
        private Button _mockButton;
        private TelemetryGraphElement _graph;
        private bool _updatingSliders;

        [MenuItem("Hecton8/Diagnostics/Flora Dear Lie X-Ray")]
        private static void Open()
        {
            FloraDearLieXRayWindow window = GetWindow<FloraDearLieXRayWindow>();
            window.titleContent = new GUIContent("Flora Dear Lie");
            window.minSize = new Vector2(320f, 160f);
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            _status = new Label();
            _counts = new Label();
            _quality = new Label();
            _radiusSlider = new Slider("Damage Radius", 0.25f, 8f);
            _regenSlider = new Slider("Regeneration Seconds", 5f, 900f);
            _qualityOverrideSlider = new Slider("Quality Override", -1f, 1f);
            _mockButton = new Button(RequestMockBurst) { text = "Inject Mock Damage" };
            _graph = new TelemetryGraphElement();
            _radiusSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _regenSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _qualityOverrideSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            root.Add(_status);
            root.Add(_counts);
            root.Add(_quality);
            root.Add(_graph);
            root.Add(_radiusSlider);
            root.Add(_regenSlider);
            root.Add(_qualityOverrideSlider);
            root.Add(_mockButton);
        }

        private void Update()
        {
            DestructibleOrganicManager runtime = ResolveRuntime();
            if (runtime == null)
            {
                SetText("runtime: missing", "surface=0 underwater=0 regen=0", "quality=0");
                _graph?.SetSamples(_destroyedScratch, _vfxScratch, _regenScratch, 0);
                return;
            }

            SetText(
                "lastFrame=" + runtime.DearLieLastDamageFrame + " destroyed=" + runtime.DearLieLastDestroyedCount + " vfx=" + runtime.DearLieLastVfxCount,
                "surface=" + runtime.DearLieSurfaceInstanceCount + " underwater=" + runtime.DearLieUnderwaterInstanceCount + " regen=" + runtime.DearLieRegenQueueCount,
                "quality=" + runtime.DearLieQualityWeight.ToString("0.000", CultureInfo.InvariantCulture));
            SyncSliders(runtime);
            int sampleCount = runtime.EditorCopyDearLieTelemetry(_frameScratch, _destroyedScratch, _vfxScratch, _regenScratch);
            _graph?.SetSamples(_destroyedScratch, _vfxScratch, _regenScratch, sampleCount);
        }

        private void SetText(string status, string counts, string quality)
        {
            if (_status == null || _counts == null || _quality == null)
                return;

            _status.text = status;
            _counts.text = counts;
            _quality.text = quality;
        }

        private void SyncSliders(DestructibleOrganicManager runtime)
        {
            if (_radiusSlider == null || _regenSlider == null || _qualityOverrideSlider == null)
                return;

            _updatingSliders = true;
            _radiusSlider.SetValueWithoutNotify(runtime.DearLieDamageRadiusEpsilon);
            _regenSlider.SetValueWithoutNotify(runtime.DearLieRegenerationDelayTuningSeconds);
            _qualityOverrideSlider.SetValueWithoutNotify(runtime.DearLieQualityOverride);
            _updatingSliders = false;
        }

        private void ApplyTuning()
        {
            if (_updatingSliders)
                return;

            DestructibleOrganicManager runtime = ResolveRuntime();
            if (runtime == null || _radiusSlider == null || _regenSlider == null || _qualityOverrideSlider == null)
                return;

            runtime.EditorSetDearLieTuning(_radiusSlider.value, _regenSlider.value, _qualityOverrideSlider.value);
            EditorUtility.SetDirty(runtime);
        }

        private void RequestMockBurst()
        {
            DestructibleOrganicManager runtime = ResolveRuntime();
            if (runtime == null)
                return;

            runtime.EditorRequestDearLieMockBurst();
            EditorUtility.SetDirty(runtime);
        }

        private static DestructibleOrganicManager ResolveRuntime()
        {
            return DestructibleOrganicManager.ActiveRuntimeInstance;
        }

        private sealed class TelemetryGraphElement : VisualElement
        {
            private int[] _destroyed;
            private int[] _vfx;
            private int[] _regen;
            private int _count;

            public TelemetryGraphElement()
            {
                style.height = 72f;
                style.marginTop = 6f;
                style.marginBottom = 6f;
                style.backgroundColor = new Color(0.04f, 0.05f, 0.055f, 1f);
                generateVisualContent += DrawGraph;
            }

            public void SetSamples(int[] destroyed, int[] vfx, int[] regen, int count)
            {
                _destroyed = destroyed;
                _vfx = vfx;
                _regen = regen;
                _count = Mathf.Clamp(count, 0, TelemetryGraphCapacity);
                MarkDirtyRepaint();
            }

            private void DrawGraph(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (_count <= 1 || rect.width <= 1f || rect.height <= 1f)
                    return;

                int maxValue = 1;
                for (int i = 0; i < _count; i++)
                {
                    maxValue = Mathf.Max(maxValue, _destroyed[i]);
                    maxValue = Mathf.Max(maxValue, _vfx[i]);
                    maxValue = Mathf.Max(maxValue, _regen[i]);
                }

                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.4f;
                DrawSeries(painter, rect, _regen, maxValue, new Color(0.2f, 0.55f, 1f, 0.9f));
                DrawSeries(painter, rect, _vfx, maxValue, new Color(0.15f, 0.95f, 0.55f, 0.9f));
                DrawSeries(painter, rect, _destroyed, maxValue, new Color(1f, 0.15f, 0.08f, 0.95f));
            }

            private void DrawSeries(Painter2D painter, Rect rect, int[] samples, int maxValue, Color color)
            {
                if (samples == null || maxValue <= 0)
                    return;

                float step = rect.width / Mathf.Max(1, _count - 1);
                painter.strokeColor = color;
                painter.BeginPath();
                for (int i = 0; i < _count; i++)
                {
                    float x = rect.xMin + (i * step);
                    float y = rect.yMax - ((Mathf.Clamp(samples[i], 0, maxValue) / (float)maxValue) * rect.height);
                    Vector2 point = new Vector2(x, y);
                    if (i == 0)
                        painter.MoveTo(point);
                    else
                        painter.LineTo(point);
                }

                painter.Stroke();
            }
        }
    }
}
