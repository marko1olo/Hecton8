#if UNITY_EDITOR
using System.Globalization;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Visor.Editor
{
    public sealed class ScreenSpaceDecalTunerWindow : EditorWindow
    {
        private const double RefreshSeconds = 0.2d;
        private const string DefaultCsvPath = "Assets/_Project/Data/Decals/visor_decal_profiles.csv";

        private readonly VisualElement[] _histogramBars = new VisualElement[16];
        private Slider _qualitySlider;
        private Slider _fadeSlider;
        private Slider _capacitySlider;
        private Slider _refractionSlider;
        private Slider _radiusSlider;
        private Slider _depthSlider;
        private Toggle _drawGizmoToggle;
        private SliderInt _gizmoLimitSlider;
        private Label _statsLabel;
        private Label _csvLabel;
        private double _nextRefreshTime;

        [MenuItem("HECTON-8/Rendering/Screen-Space Visor Wound Tuner")]
        public static void Open()
        {
            GetWindow<ScreenSpaceDecalTunerWindow>("Visor Wounds");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui -= OnDrawGizmos;
            SceneView.duringSceneGui += OnDrawGizmos;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= OnDrawGizmos;
        }

        public void CreateGUI()
        {
            DynamicDecalVaultRuntime.WarmupColdGlobalRoutes();

            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            _qualitySlider = BuildSlider("Global Quality", 0f, 1f, HomeostasisBrain.GlobalQualityWeight);
            _fadeSlider = BuildSlider("Base Fade Seconds", 0.25f, 60f, 7.5f);
            _capacitySlider = BuildSlider("Maximum Overkill Capacity", DynamicDecalVaultRuntime.LowCapacity, DynamicDecalVaultRuntime.MaxCapacity, DynamicDecalVaultRuntime.MaxCapacity);
            _refractionSlider = BuildSlider("Normal Refraction Intensity", 0f, 2.5f, 1f);
            _radiusSlider = BuildSlider("Base Radius", 0.025f, 8f, 0.55f);
            _depthSlider = BuildSlider("Projection Depth", 0.025f, 2f, 0.18f);
            _drawGizmoToggle = new Toggle("Draw Live Matrix Gizmo") { value = false };
            _gizmoLimitSlider = new SliderInt("Gizmo Volume Limit", 1, DynamicDecalVaultRuntime.MaxCapacity)
            {
                value = 64,
                showInputField = true
            };
            _statsLabel = new Label();
            _csvLabel = new Label();

            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_fadeSlider);
            rootVisualElement.Add(_capacitySlider);
            rootVisualElement.Add(_refractionSlider);
            rootVisualElement.Add(_radiusSlider);
            rootVisualElement.Add(_depthSlider);
            rootVisualElement.Add(_drawGizmoToggle);
            rootVisualElement.Add(_gizmoLimitSlider);
            rootVisualElement.Add(new Button(GenerateMockLoad) { text = "Generate Mock Visor Wounds" });
            rootVisualElement.Add(new Button(LoadCsvProfiles) { text = "Load Visor Wound CSV" });
            rootVisualElement.Add(BuildHistogram());
            rootVisualElement.Add(_statsLabel);
            rootVisualElement.Add(_csvLabel);

            _qualitySlider.RegisterValueChangedCallback(evt => HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(evt.newValue, true));
            _fadeSlider.RegisterValueChangedCallback(_ => PublishTuning());
            _capacitySlider.RegisterValueChangedCallback(_ => PublishTuning());
            _refractionSlider.RegisterValueChangedCallback(_ => PublishTuning());
            _radiusSlider.RegisterValueChangedCallback(_ => PublishTuning());
            _depthSlider.RegisterValueChangedCallback(_ => PublishTuning());
            _drawGizmoToggle.RegisterValueChangedCallback(_ => SceneView.RepaintAll());
            _gizmoLimitSlider.RegisterValueChangedCallback(_ => SceneView.RepaintAll());

            PullTuning();
            RefreshStats();
        }

        private static Slider BuildSlider(string label, float min, float max, float value)
        {
            return new Slider(label, min, max)
            {
                value = math.clamp(value, min, max),
                showInputField = true
            };
        }

        private VisualElement BuildHistogram()
        {
            VisualElement root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.height = 44f;
            root.style.marginTop = 8f;
            root.style.marginBottom = 8f;

            for (int i = 0; i < _histogramBars.Length; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.flexGrow = 1f;
                bar.style.marginLeft = 1f;
                bar.style.marginRight = 1f;
                bar.style.alignSelf = Align.FlexEnd;
                bar.style.height = 2f;
                bar.style.backgroundColor = new StyleColor(new Color(0.78f, 0.34f, 0.08f, 0.9f));
                _histogramBars[i] = bar;
                root.Add(bar);
            }

            return root;
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshSeconds;
            PullTuning();
            RefreshStats();
        }

        private void PullTuning()
        {
            if (!DynamicDecalVaultRuntime.TryGetTuning(out DecalTuningDTO tuning))
                return;

            _fadeSlider?.SetValueWithoutNotify(tuning.BaseFadeTimeSeconds);
            _capacitySlider?.SetValueWithoutNotify(tuning.MaximumOverkillCapacity);
            _refractionSlider?.SetValueWithoutNotify(tuning.NormalRefractionIntensity);
            _radiusSlider?.SetValueWithoutNotify(tuning.BaseRadiusMeters);
            _depthSlider?.SetValueWithoutNotify(tuning.ProjectionDepthMeters);
            _qualitySlider?.SetValueWithoutNotify(HomeostasisBrain.GlobalQualityWeight);
        }

        private void PublishTuning()
        {
            DecalTuningDTO tuning = default;
            if (DynamicDecalVaultRuntime.TryGetTuning(out DecalTuningDTO current))
                tuning = current;

            tuning.BaseFadeTimeSeconds = _fadeSlider != null ? _fadeSlider.value : 7.5f;
            tuning.MaximumOverkillCapacity = _capacitySlider != null ? _capacitySlider.value : DynamicDecalVaultRuntime.MaxCapacity;
            tuning.NormalRefractionIntensity = _refractionSlider != null ? _refractionSlider.value : 1f;
            tuning.ProjectionDepthMeters = _depthSlider != null ? _depthSlider.value : 0.18f;
            tuning.LowTierCapacity = DynamicDecalVaultRuntime.LowCapacity;
            tuning.BaseRadiusMeters = _radiusSlider != null ? _radiusSlider.value : 0.55f;
            DynamicDecalVaultRuntime.WriteTuning(in tuning);
        }

        private void GenerateMockLoad()
        {
            DynamicDecalVaultRuntime.GenerateMockVisorWounds(DynamicDecalVaultRuntime.MaxCapacity);
            RefreshStats();
        }

        private void LoadCsvProfiles()
        {
            string projectPath = Application.dataPath;
            string defaultPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectPath, "..", DefaultCsvPath));
            string selected = EditorUtility.OpenFilePanel("Load visor_decal_profiles.csv", System.IO.Path.GetDirectoryName(defaultPath), "csv");
            if (string.IsNullOrEmpty(selected))
                return;

            bool loaded = DynamicDecalVaultRuntime.TryLoadMaterialProfilesCsv(selected, out int rowCount);
            _csvLabel.text = loaded
                ? string.Concat("CSV profiles loaded: ", rowCount.ToString(CultureInfo.InvariantCulture))
                : "CSV profiles rejected";
        }

        private void RefreshStats()
        {
            if (_statsLabel == null)
                return;

            DynamicDecalVaultRuntime.TryGetRuntimeState(out DecalRuntimeStateDTO state);
            DynamicDecalVaultRuntime.TryGetLatestTelemetry(out VisorWoundTelemetryEntry telemetry);
            float capacity = math.max(1f, state.MaxActiveThisFrame);
            float fill = math.saturate(state.ActiveCount / capacity);
            for (int i = 0; i < _histogramBars.Length; i++)
            {
                if (_histogramBars[i] == null)
                    continue;

                float threshold = (i + 1f) / _histogramBars.Length;
                float height = math.lerp(2f, 42f, math.saturate(fill - threshold + (1f / _histogramBars.Length)) * _histogramBars.Length);
                _histogramBars[i].style.height = height;
            }

            CultureInfo culture = CultureInfo.InvariantCulture;
            _statsLabel.text = string.Concat(
                "Active ",
                state.ActiveCount.ToString(culture),
                "/",
                state.MaxActiveThisFrame.ToString(culture),
                " | New ",
                state.NewThisFrame.ToString(culture),
                " | Upload ",
                state.LastUploadCount.ToString(culture),
                " | CPU ",
                state.CpuMicroseconds.ToString("0.00", culture),
                " us | GPU Upload ",
                state.UploadMicroseconds.ToString("0.00", culture),
                " us | Q ",
                state.GlobalQualityWeight.ToString("0.000", culture),
                " | Thermal ",
                state.ThermalPressure01.ToString("0.000", culture),
                " | CSV ",
                DynamicDecalVaultRuntime.GetLoadedMaterialProfileCount().ToString(culture),
                " | Last Hash 0x",
                telemetry.StateHash.ToString("X8", culture));
        }

        private void OnDrawGizmos(SceneView sceneView)
        {
            if (_drawGizmoToggle == null || !_drawGizmoToggle.value || sceneView == null)
                return;

            if (!DynamicDecalVaultRuntime.TryAcquireDecalBufferRead(out Unity.Collections.NativeArray<VisorDecalDTO> decals, out _, out Vector3 cameraWorldPosition))
                return;

            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            try
            {
                int limit = _gizmoLimitSlider != null
                    ? Mathf.Clamp(_gizmoLimitSlider.value, 1, DynamicDecalVaultRuntime.MaxCapacity)
                    : 64;
                int drawn = 0;
                for (int i = 0; i < decals.Length && drawn < limit; i++)
                {
                    VisorDecalDTO decal = decals[i];
                    if ((decal.Flags & DynamicDecalFlags.Active) == 0u || decal.Opacity01 <= 0.0001f)
                        continue;

                    Unity.Mathematics.float4x4 source = decal.LocalToWorld;
                    Matrix4x4 matrix = default;
                    matrix.SetColumn(0, new Vector4(source.c0.x, source.c0.y, source.c0.z, source.c0.w));
                    matrix.SetColumn(1, new Vector4(source.c1.x, source.c1.y, source.c1.z, source.c1.w));
                    matrix.SetColumn(2, new Vector4(source.c2.x, source.c2.y, source.c2.z, source.c2.w));
                    matrix.SetColumn(
                        3,
                        new Vector4(
                            source.c3.x + cameraWorldPosition.x,
                            source.c3.y + cameraWorldPosition.y,
                            source.c3.z + cameraWorldPosition.z,
                            1f));

                    float opacity = Mathf.Clamp01(decal.Opacity01);
                    Handles.matrix = matrix;
                    Handles.color = new Color(1f, 0.35f, 0.08f, 0.2f + opacity * 0.55f);
                    Handles.DrawWireCube(Vector3.zero, Vector3.one);
                    drawn++;
                }
            }
            finally
            {
                DynamicDecalVaultRuntime.ReleaseDecalBufferRead();
                Handles.matrix = previousMatrix;
                Handles.color = previousColor;
            }
        }
    }
}
#endif
