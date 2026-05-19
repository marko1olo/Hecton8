#if UNITY_EDITOR
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
        private const string DefaultCsvPath = "Assets/_Project/Data/Decals/decal_material_profiles.csv";

        private readonly VisualElement[] _histogramBars = new VisualElement[16];
        private Slider _qualitySlider;
        private Slider _fadeSlider;
        private Slider _capacitySlider;
        private Slider _mipBiasSlider;
        private Slider _radiusSlider;
        private Slider _depthSlider;
        private Label _statsLabel;
        private Label _csvLabel;
        private double _nextRefreshTime;

        [MenuItem("HECTON-8/Rendering/Screen-Space Decal Tuner")]
        public static void Open()
        {
            GetWindow<ScreenSpaceDecalTunerWindow>("Dynamic Decals");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            _qualitySlider = BuildSlider("Global Quality", 0f, 1f, HomeostasisBrain.GlobalQualityWeight);
            _fadeSlider = BuildSlider("Base Fade Seconds", 0.25f, 60f, 7.5f);
            _capacitySlider = BuildSlider("Maximum Overkill Capacity", DynamicDecalVaultRuntime.LowCapacity, DynamicDecalVaultRuntime.MaxCapacity, DynamicDecalVaultRuntime.MaxCapacity);
            _mipBiasSlider = BuildSlider("Atlas Mipmap Bias", -2f, 4f, 0f);
            _radiusSlider = BuildSlider("Base Radius", 0.025f, 8f, 0.55f);
            _depthSlider = BuildSlider("Projection Depth", 0.025f, 2f, 0.18f);
            _statsLabel = new Label();
            _csvLabel = new Label();

            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_fadeSlider);
            rootVisualElement.Add(_capacitySlider);
            rootVisualElement.Add(_mipBiasSlider);
            rootVisualElement.Add(_radiusSlider);
            rootVisualElement.Add(_depthSlider);
            rootVisualElement.Add(new Button(GenerateMockLoad) { text = "Generate 1024 Mock Decals" });
            rootVisualElement.Add(new Button(LoadCsvProfiles) { text = "Load Material CSV" });
            rootVisualElement.Add(BuildHistogram());
            rootVisualElement.Add(_statsLabel);
            rootVisualElement.Add(_csvLabel);

            _qualitySlider.RegisterValueChangedCallback(evt => HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(evt.newValue, true));
            _fadeSlider.RegisterValueChangedCallback(_ => PublishTuning());
            _capacitySlider.RegisterValueChangedCallback(_ => PublishTuning());
            _mipBiasSlider.RegisterValueChangedCallback(_ => PublishTuning());
            _radiusSlider.RegisterValueChangedCallback(_ => PublishTuning());
            _depthSlider.RegisterValueChangedCallback(_ => PublishTuning());

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
            _mipBiasSlider?.SetValueWithoutNotify(tuning.AtlasMipmapBias);
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
            tuning.AtlasMipmapBias = _mipBiasSlider != null ? _mipBiasSlider.value : 0f;
            tuning.ProjectionDepthMeters = _depthSlider != null ? _depthSlider.value : 0.18f;
            tuning.LowTierCapacity = DynamicDecalVaultRuntime.LowCapacity;
            tuning.BaseRadiusMeters = _radiusSlider != null ? _radiusSlider.value : 0.55f;
            DynamicDecalVaultRuntime.WriteTuning(in tuning);
        }

        private void GenerateMockLoad()
        {
            DynamicDecalVaultRuntime.GenerateMockDecals(DynamicDecalVaultRuntime.MaxCapacity);
            RefreshStats();
        }

        private void LoadCsvProfiles()
        {
            string projectPath = Application.dataPath;
            string defaultPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectPath, "..", DefaultCsvPath));
            string selected = EditorUtility.OpenFilePanel("Load decal_material_profiles.csv", System.IO.Path.GetDirectoryName(defaultPath), "csv");
            if (string.IsNullOrEmpty(selected))
                return;

            bool loaded = DynamicDecalVaultRuntime.TryLoadMaterialProfilesCsv(selected, out int rowCount);
            _csvLabel.text = loaded
                ? $"CSV profiles loaded: {rowCount}"
                : "CSV profiles rejected";
        }

        private void RefreshStats()
        {
            if (_statsLabel == null)
                return;

            DynamicDecalVaultRuntime.TryGetRuntimeState(out DecalRuntimeStateDTO state);
            DynamicDecalVaultRuntime.TryGetLatestTelemetry(out DecalTelemetryEntry telemetry);
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

            _statsLabel.text =
                $"Active {state.ActiveCount}/{state.MaxActiveThisFrame} | New {state.NewThisFrame} | Upload {state.LastUploadCount} | CPU {state.CpuMicroseconds:0.00} us | GPU Upload {state.UploadMicroseconds:0.00} us | Q {state.GlobalQualityWeight:0.000} | Thermal {state.ThermalPressure01:0.000} | CSV {DynamicDecalVaultRuntime.GetLoadedMaterialProfileCount()} | Last Hash 0x{telemetry.StateHash:X8}";
        }
    }
}
#endif
