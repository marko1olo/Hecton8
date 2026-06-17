using Hecton8.Lighting;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Lighting.Editor
{
    public sealed class DayNightGIRelayTunerWindow : EditorWindow
    {
        private HectonGIRelaySystem _target;
        private Label _status;
        private Label _lighting;
        private Label _telemetry;
        private VisualElement _ambientBlock;
        private VisualElement _fogBlock;
        private VisualElement _directionalBlock;
        private DayNightTelemetryGraphElement _graph;

        [MenuItem("Hecton8/Lighting/Abyssal Lighting Tuner/Day-Night GI Relay")]
        private static void Open()
        {
            GetWindow<DayNightGIRelayTunerWindow>("Day-Night GI Relay");
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshStatus;
            EditorApplication.update += RefreshStatus;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshStatus;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;

            ObjectField targetField = new ObjectField("Runtime")
            {
                objectType = typeof(HectonGIRelaySystem),
                allowSceneObjects = true,
                value = ResolveTarget()
            };
            targetField.RegisterValueChangedCallback(evt =>
            {
                _target = evt.newValue as HectonGIRelaySystem;
                RefreshStatus();
            });
            root.Add(targetField);

            Slider quality = new Slider("GlobalQualityWeight Override", -1f, 1f) { value = -1f };
            quality.RegisterValueChangedCallback(evt => _target?.SetEditorQualityOverride(evt.newValue));
            root.Add(quality);

            Slider extinction = new Slider("Water Extinction Constant", 0f, 0.006f) { value = 0.0017f };
            extinction.RegisterValueChangedCallback(evt => _target?.SetEditorWaterExtinctionConstant(evt.newValue));
            root.Add(extinction);

            Slider eclipse = new Slider("Eclipse Darkening Multiplier", 0f, 1f) { value = 0.72f };
            eclipse.RegisterValueChangedCallback(evt => _target?.SetEditorEclipseDarkeningMultiplier(evt.newValue));
            root.Add(eclipse);

            Toggle debugBlocks = new Toggle("Debug Color Blocks");
            debugBlocks.RegisterValueChangedCallback(evt => _target?.SetEditorDebugColorBlocks(evt.newValue ? 1f : 0f));
            root.Add(debugBlocks);

            Toolbar toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => _target?.GenerateMockLightingEnvironment()) { text = "Mock Environment" });
            toolbar.Add(new ToolbarButton(() => _target?.RequestLightingGradientProfilesReload()) { text = "Reload Lighting CSV" });
            toolbar.Add(new ToolbarButton(() => _target?.DumpDayNightBlackBoxNow()) { text = "Dump Black Box" });
            root.Add(toolbar);

            _status = new Label();
            _lighting = new Label();
            _telemetry = new Label();
            root.Add(_status);
            root.Add(_lighting);
            root.Add(_telemetry);

            VisualElement swatches = new VisualElement();
            swatches.style.flexDirection = FlexDirection.Row;
            swatches.style.height = 28;
            swatches.style.marginTop = 4;
            _ambientBlock = CreateSwatch();
            _fogBlock = CreateSwatch();
            _directionalBlock = CreateSwatch();
            swatches.Add(_ambientBlock);
            swatches.Add(_fogBlock);
            swatches.Add(_directionalBlock);
            root.Add(swatches);

            _graph = new DayNightTelemetryGraphElement();
            _graph.style.height = 90;
            _graph.style.marginTop = 6;
            root.Add(_graph);

            RefreshStatus();
        }

        private HectonGIRelaySystem ResolveTarget()
        {
            if (_target == null)
                _target = Object.FindAnyObjectByType<HectonGIRelaySystem>(FindObjectsInactive.Include);

            return _target;
        }

        private void RefreshStatus()
        {
            if (_status == null || _lighting == null || _telemetry == null)
                return;

            if (_target == null)
                ResolveTarget();

            if (_target == null)
            {
                _status.text = "Runtime: missing";
                _lighting.text = "EnvironmentLightingDTO: unavailable";
                _telemetry.text = "Telemetry: unavailable";
                return;
            }

            _status.text = "Runtime: seq=" + _target.LastAppliedSequence +
                " depth=" + _target.LastAppliedDepthMeters.ToString("0.0") +
                " profiles=" + _target.LightingGradientProfileCount +
                " cbuffer=" + _target.LastEnvironmentLightingValid;

            if (_target.TryGetEnvironmentLightingCopy(out EnvironmentLightingDTO dto))
            {
                _lighting.text = "Ambient " + Format(dto.AmbientColor) +
                    " Fog " + Format(dto.FogColor) +
                    " Sun " + dto.SunIntensity.ToString("0.000") +
                    " Moon " + dto.MoonIntensity.ToString("0.000") +
                    " Gloom " + dto.FogColor.w.ToString("0.000");
                SetSwatch(_ambientBlock, dto.AmbientColor);
                SetSwatch(_fogBlock, dto.FogColor);
                SetSwatch(_directionalBlock, dto.DirectionalLightColor);
            }

            if (_target.TryGetDayNightTelemetryReadback(out NativeArray<LightingRelayTelemetryEntry>.ReadOnly telemetry, out int cursor))
            {
                _graph.LoadTelemetry(telemetry, cursor);
                _telemetry.text = "Telemetry: ring=" + telemetry.Length + " cursor=" + cursor;
            }
        }

        private static VisualElement CreateSwatch()
        {
            VisualElement swatch = new VisualElement();
            swatch.style.flexGrow = 1f;
            swatch.style.marginRight = 4;
            swatch.style.borderTopWidth = 1;
            swatch.style.borderBottomWidth = 1;
            swatch.style.borderLeftWidth = 1;
            swatch.style.borderRightWidth = 1;
            return swatch;
        }

        private static void SetSwatch(VisualElement swatch, float4 value)
        {
            if (swatch == null)
                return;

            swatch.style.backgroundColor = new Color(
                math.saturate(value.x),
                math.saturate(value.y),
                math.saturate(value.z),
                1f);
        }

        private static string Format(float4 value)
        {
            return "(" +
                value.x.ToString("0.000") + ", " +
                value.y.ToString("0.000") + ", " +
                value.z.ToString("0.000") + ")";
        }

        private sealed class DayNightTelemetryGraphElement : VisualElement
        {
            private const int SampleCount = 128;
            private readonly float[] _samples = new float[SampleCount];
            private float _maxUs = 1f;

            public DayNightTelemetryGraphElement()
            {
                generateVisualContent += OnGenerateVisualContent;
            }

            public void LoadTelemetry(NativeArray<LightingRelayTelemetryEntry>.ReadOnly telemetry, int cursor)
            {
                if (telemetry.Length <= 0)
                    return;

                _maxUs = 1f;
                int count = math.min(SampleCount, telemetry.Length);
                for (int i = 0; i < count; i++)
                {
                    int index = cursor - count + i;
                    while (index < 0)
                        index += telemetry.Length;

                    LightingRelayTelemetryEntry entry = telemetry[index];
                    float value = math.max(0f, entry.BurstCpuMicroseconds);
                    _samples[i] = value;
                    _maxUs = math.max(_maxUs, value);
                }

                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                Rect r = contentRect;
                painter.strokeColor = new Color(0.18f, 0.72f, 0.82f, 1f);
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                for (int i = 0; i < SampleCount; i++)
                {
                    float x = r.xMin + (r.width * i / math.max(1, SampleCount - 1));
                    float y = r.yMax - r.height * math.saturate(_samples[i] / _maxUs);
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
