#if UNITY_EDITOR
using Hecton8.Crafting;
using Hecton8.World;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class FabricationAssemblerTunerWindow : EditorWindow
    {
        private const float SliderSpeedMin = 0.05f;
        private const float SliderSpeedMax = 16f;
        private const float SliderPowerMin = 0f;
        private const float SliderPowerMax = 8f;
        private const float SliderEdgeMin = 0f;
        private const float SliderEdgeMax = 8f;

        private readonly Vector3[] _planeVertices = new Vector3[4]; // COLD ALLOC: Vector3[4] - SceneView clipping plane corners - owner: FabricationAssemblerTunerWindow

        private IntegerField _activeJobsField;
        private IntegerField _completedJobsField;
        private FloatField _averageProgressField;
        private FloatField _qualityWeightField;
        private Slider _baseBuildSpeedSlider;
        private Slider _powerDrawSlider;
        private Slider _edgeGlowSlider;
        private Toggle _drawGizmosToggle;
        private IntegerField _csvRowsField;
        private bool _suppressSliderCallbacks;

        [MenuItem("Hecton8/Fabrication/Zero-GC Fabrication Tuner")]
        public static void Open()
        {
            GetWindow<FabricationAssemblerTunerWindow>("Fabrication Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _activeJobsField = CreateReadOnlyInteger("Active Jobs");
            _completedJobsField = CreateReadOnlyInteger("Completed Jobs");
            _averageProgressField = CreateReadOnlyFloat("Average Progress");
            _qualityWeightField = CreateReadOnlyFloat("Global Quality Weight");
            _csvRowsField = CreateReadOnlyInteger("CSV Rows");
            _drawGizmosToggle = new Toggle("Draw Clipping Gizmo") { value = true };

            _baseBuildSpeedSlider = new Slider("Base Build Speed", SliderSpeedMin, SliderSpeedMax) { value = 1f };
            _powerDrawSlider = new Slider("Power Draw Multiplier", SliderPowerMin, SliderPowerMax) { value = 1f };
            _edgeGlowSlider = new Slider("Shader Edge Glow", SliderEdgeMin, SliderEdgeMax) { value = 1f };

            _baseBuildSpeedSlider.RegisterValueChangedCallback(OnTuningSliderChanged);
            _powerDrawSlider.RegisterValueChangedCallback(OnTuningSliderChanged);
            _edgeGlowSlider.RegisterValueChangedCallback(OnTuningSliderChanged);

            Button mockButton = new Button(OnGenerateMockClicked) { text = "Generate 50 Mock Jobs" };
            Button csvButton = new Button(OnLoadCsvClicked) { text = "Load fabrication_timings.csv" };

            root.Add(_activeJobsField);
            root.Add(_completedJobsField);
            root.Add(_averageProgressField);
            root.Add(_qualityWeightField);
            root.Add(_baseBuildSpeedSlider);
            root.Add(_powerDrawSlider);
            root.Add(_edgeGlowSlider);
            root.Add(mockButton);
            root.Add(csvButton);
            root.Add(_csvRowsField);
            root.Add(_drawGizmosToggle);
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshReadout;
            EditorApplication.update += RefreshReadout;
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshReadout;
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        private void RefreshReadout()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (FabricationAssemblerRuntime.TryGetEditorStats(out FabricationEditorStats stats))
            {
                _activeJobsField?.SetValueWithoutNotify(stats.ActiveJobs);
                _completedJobsField?.SetValueWithoutNotify(stats.CompletedJobs);
                _averageProgressField?.SetValueWithoutNotify(stats.AverageProgress01);
                _qualityWeightField?.SetValueWithoutNotify(stats.GlobalQualityWeight);
            }

            if (FabricationAssemblerRuntime.TryGetTuning(out float buildSpeed, out float powerDraw, out float edgeGlow))
            {
                _suppressSliderCallbacks = true;
                _baseBuildSpeedSlider?.SetValueWithoutNotify(buildSpeed);
                _powerDrawSlider?.SetValueWithoutNotify(powerDraw);
                _edgeGlowSlider?.SetValueWithoutNotify(edgeGlow);
                _suppressSliderCallbacks = false;
            }
        }

        private void OnTuningSliderChanged(ChangeEvent<float> evt)
        {
            if (_suppressSliderCallbacks || !EditorApplication.isPlaying)
                return;

            FabricationAssemblerRuntime.TrySetTuning(
                _baseBuildSpeedSlider != null ? _baseBuildSpeedSlider.value : 1f,
                _powerDrawSlider != null ? _powerDrawSlider.value : 1f,
                _edgeGlowSlider != null ? _edgeGlowSlider.value : 1f);
        }

        private void OnGenerateMockClicked()
        {
            if (EditorApplication.isPlaying)
                FabricationAssemblerRuntime.GenerateMockFabricationJobs();
        }

        private void OnLoadCsvClicked()
        {
            if (!EditorApplication.isPlaying)
                return;

            string path = EditorUtility.OpenFilePanel("fabrication_timings.csv", Application.dataPath, "csv");
            if (string.IsNullOrEmpty(path))
                return;

            if (FabricationAssemblerRuntime.TryIngestFabricationTimingsCsv(path, out int parsedRows))
                _csvRowsField?.SetValueWithoutNotify(parsedRows);
        }

        private void DrawSceneGizmos(SceneView sceneView)
        {
            if (!EditorApplication.isPlaying || _drawGizmosToggle == null || !_drawGizmosToggle.value)
                return;

            Color previousColor = Handles.color;
            for (int slot = 0; slot < FabricationAssemblerRuntime.MaxFabricationJobs; slot++)
            {
                if (!FabricationAssemblerRuntime.TryGetEditorJobDebug(slot, out FabricationEditorJobDebug debug))
                    continue;

                AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromAbsolutePosition(debug.TargetAUP);
                Vector3 center = (Vector3)aup.ToRuntimeFloat3();
                float minY = math.isfinite(debug.BoundsMinY) ? debug.BoundsMinY : -0.5f;
                float maxY = math.max(minY + 0.001f, math.isfinite(debug.BoundsMaxY) ? debug.BoundsMaxY : minY + 1f);
                float height = maxY - minY;
                center.y += (minY + maxY) * 0.5f;
                Vector3 size = new Vector3(1.2f, height, 1.2f);

                Handles.color = new Color(0.1f, 0.95f, 1f, 0.72f);
                Handles.DrawWireCube(center, size);

                float planeY = center.y - height * 0.5f + height * math.saturate(debug.Progress01);
                FillPlaneVertices(center, planeY);
                Handles.DrawSolidRectangleWithOutline(_planeVertices, new Color(0.1f, 0.8f, 1f, 0.18f), new Color(0.85f, 1f, 1f, 0.9f));
            }

            Handles.color = previousColor;
        }

        private void FillPlaneVertices(Vector3 center, float y)
        {
            _planeVertices[0] = new Vector3(center.x - 0.6f, y, center.z - 0.6f);
            _planeVertices[1] = new Vector3(center.x - 0.6f, y, center.z + 0.6f);
            _planeVertices[2] = new Vector3(center.x + 0.6f, y, center.z + 0.6f);
            _planeVertices[3] = new Vector3(center.x + 0.6f, y, center.z - 0.6f);
        }

        private static IntegerField CreateReadOnlyInteger(string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            return field;
        }

        private static FloatField CreateReadOnlyFloat(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            return field;
        }
    }
}
#endif
