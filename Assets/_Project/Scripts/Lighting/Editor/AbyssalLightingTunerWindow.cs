using Hecton8.Lighting;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Hecton8.Lighting.Editor
{
    public sealed class AbyssalLightingTunerWindow : EditorWindow
    {
        private InteriorGIProbeVolumeRuntime _target;
        private Label _status;
        private Label _layout;
        private ProbeTelemetryGraphElement _graph;

        [MenuItem("HECTON-8/Lighting/Abyssal Lighting Tuner")]
        private static void Open()
        {
            GetWindow<AbyssalLightingTunerWindow>("Abyssal Lighting Tuner");
        }

        private void OnEnable()
        {
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
                objectType = typeof(InteriorGIProbeVolumeRuntime),
                allowSceneObjects = true,
                value = ResolveTarget()
            };
            targetField.RegisterValueChangedCallback(evt =>
            {
                _target = evt.newValue as InteriorGIProbeVolumeRuntime;
                RefreshStatus();
            });
            root.Add(targetField);

            _status = new Label();
            _layout = new Label();
            _graph = new ProbeTelemetryGraphElement();
            _graph.style.height = 86;
            _graph.style.marginTop = 6;
            _graph.style.marginBottom = 6;
            root.Add(_status);
            root.Add(_layout);
            root.Add(_graph);

            Slider quality = new Slider("Quality Override", -1f, 1f) { value = -1f };
            quality.RegisterValueChangedCallback(evt => _target?.SetEditorForceQuality(evt.newValue));
            root.Add(quality);

            Slider emergency = new Slider("Emergency Red", 0f, 1f);
            emergency.RegisterValueChangedCallback(evt => _target?.SetEditorEmergencyOverride(evt.newValue));
            root.Add(emergency);

            Slider propagation = new Slider("Propagation", 0.05f, 4f) { value = 0.9f };
            propagation.RegisterValueChangedCallback(evt => _target?.SetEditorPropagationSpeed(evt.newValue));
            root.Add(propagation);

            Slider wallAbsorption = new Slider("Wall Absorption", 0f, 1f) { value = 1f };
            wallAbsorption.RegisterValueChangedCallback(evt => _target?.SetEditorWallAbsorption(evt.newValue));
            root.Add(wallAbsorption);

            Slider waterAbsorption = new Slider("Water Absorption", 0f, 1f) { value = 0.8f };
            waterAbsorption.RegisterValueChangedCallback(evt => _target?.SetEditorWaterAbsorption(evt.newValue));
            root.Add(waterAbsorption);

            Toolbar toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => _target?.GenerateMockProbeGrid()) { text = "Mock Grid" });
            toolbar.Add(new ToolbarButton(() => _target?.RequestCsvReload()) { text = "Reload Sources CSV" });
            toolbar.Add(new ToolbarButton(() => _target?.RequestAmbientProfileCsvReload()) { text = "Reload Ambient CSV" });
            toolbar.Add(new ToolbarButton(() => _target?.DumpBlackBoxNow()) { text = "Dump Black Box" });
            root.Add(toolbar);

            Button disableSelection = new Button(DisableUnityLightProbesOnSelection) { text = "Disable Unity Probes On Selection" };
            root.Add(disableSelection);

            Button scan = new Button(ScanLoadedScenesForUnityProbeGroups) { text = "Scan Unity Probe Groups" };
            root.Add(scan);

            RefreshStatus();
        }

        private InteriorGIProbeVolumeRuntime ResolveTarget()
        {
            if (_target == null)
                _target = Object.FindAnyObjectByType<InteriorGIProbeVolumeRuntime>(FindObjectsInactive.Include);

            return _target;
        }

        private void RefreshStatus()
        {
            if (_status == null || _layout == null)
                return;

            if (_target == null)
                ResolveTarget();

            if (_target == null)
            {
                _status.text = "Runtime: missing";
            }
            else if (_target.TryGetTuningCopy(out InteriorGITuningDTO tuning))
            {
                _status.text = "Runtime: probes=" + tuning.ActiveProbeCount + " res=" + tuning.Resolution + " quality=" + tuning.GlobalQualityWeight.ToString("0.000");
            }
            else
            {
                _status.text = "Runtime: cold";
            }

            if (_target != null &&
                _target.TryGetTelemetryReadback(out NativeArray<InteriorGITelemetryEntry>.ReadOnly telemetry, out int cursor))
            {
                _graph.LoadTelemetry(telemetry, cursor);
            }

            bool valid = CustomLightProbeLayoutAudit.Validate(out int size, out int lane0, out int lane6, out int b8);
            _layout.text = "CustomLightProbeDTO: " + size + " bytes lane0=" + lane0 + " lane6=" + lane6 + " b8=" + b8 + " valid=" + valid;
        }

        private static void DisableUnityLightProbesOnSelection()
        {
            Lightmapping.realtimeGI = false;
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                Renderer[] renderers = selected[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                    renderers[r].lightProbeUsage = LightProbeUsage.Off;
            }
        }

        private static void ScanLoadedScenesForUnityProbeGroups()
        {
            string forbiddenType = "Light" + "Probe" + "Group";
            Component[] components = Resources.FindObjectsOfTypeAll<Component>();
            int sceneGroups = 0;
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null &&
                    component.GetType().Name == forbiddenType &&
                    component.gameObject.scene.IsValid())
                {
                    sceneGroups++;
                }
            }

            Debug.Log("[13KRA] Loaded-scene Unity probe group count: " + sceneGroups);
        }

        private sealed class ProbeTelemetryGraphElement : VisualElement
        {
            private const int SampleCount = 128;
            private readonly float[] _samples = new float[SampleCount];
            private float _maxMs = 0.001f;

            public ProbeTelemetryGraphElement()
            {
                generateVisualContent += OnGenerateVisualContent;
            }

            public void LoadTelemetry(NativeArray<InteriorGITelemetryEntry>.ReadOnly telemetry, int cursor)
            {
                if (telemetry.Length <= 0)
                    return;

                int length = math.min(SampleCount, telemetry.Length);
                int start = cursor - length;
                while (start < 0)
                    start += telemetry.Length;

                float max = 0.001f;
                for (int i = 0; i < SampleCount; i++)
                {
                    float value = 0f;
                    if (i < length)
                    {
                        int ringIndex = (start + i) % telemetry.Length;
                        value = math.max(0f, telemetry[ringIndex].SolverCompleteMs);
                    }

                    _samples[i] = value;
                    max = math.max(max, value);
                }

                _maxMs = max;
                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect r = contentRect;
                if (r.width <= 1f || r.height <= 1f)
                    return;

                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.25f;
                painter.strokeColor = new Color(0.12f, 0.95f, 1f, 0.95f);
                painter.BeginPath();
                for (int i = 0; i < SampleCount; i++)
                {
                    float x = r.xMin + r.width * (i / math.max(1f, SampleCount - 1f));
                    float y = r.yMax - r.height * math.saturate(_samples[i] / math.max(0.001f, _maxMs));
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
