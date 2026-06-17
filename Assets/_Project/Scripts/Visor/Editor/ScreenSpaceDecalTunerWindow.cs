#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
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
        private const string DefaultCsvPath = "Assets/_Project/Data/Decals/visor_trauma_profiles.csv";
        private const string CsvSchemaVersion = "H8_VISOR_TRAUMA_PROFILE_CSV_V1";
        private const string CsvHeader = "source,atlasSlice,lifetimeSeconds,radiusMeters,projectionDepthMeters";
        private const string DataMonolithOutputPath = "Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin";
        private const string RuntimeProfileVaultRoute = "GlobalDataVault BufferID 73195 MaterialProfiles / 73196 CsvScratch / 73197 RequestRing / 73198 RequestState";
        private static readonly uint CsvSchemaHash32 = ComputeFnv1a32(CsvHeader);

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
        private Label _bridgeLabel;
        private Label _layoutLabel;
        private Label _validationLabel;
        private string _lastCsvPath = DefaultCsvPath;
        private int _lastCsvRows;
        private uint _lastCsvHeaderHash32;
        private bool _lastCsvAttempted;
        private bool _lastCsvValid;
        private double _nextRefreshTime;

        [MenuItem("Hecton8/Rendering/Screen-Space Trauma Tuner")]
        public static void Open()
        {
            GetWindow<ScreenSpaceDecalTunerWindow>("Visor Trauma");
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= DrawSceneGizmos;
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
            _bridgeLabel = new Label();
            _layoutLabel = new Label();
            _validationLabel = new Label();
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
            rootVisualElement.Add(new Button(GenerateMockLoad) { text = "Generate Mock Visor Trauma" });
            rootVisualElement.Add(new Button(LoadCsvProfiles) { text = "Load Visor Trauma CSV" });
            rootVisualElement.Add(_bridgeLabel);
            rootVisualElement.Add(_layoutLabel);
            rootVisualElement.Add(_validationLabel);
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
            RefreshBridgeMetadata();
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
            DynamicDecalVaultRuntime.GenerateMockTraumaWounds(DynamicDecalVaultRuntime.MaxCapacity);
            RefreshStats();
        }

        private void LoadCsvProfiles()
        {
            string projectPath = Application.dataPath;
            string defaultPath = Path.GetFullPath(Path.Combine(projectPath, "..", DefaultCsvPath));
            string selected = EditorUtility.OpenFilePanel("Load visor_trauma_profiles.csv", Path.GetDirectoryName(defaultPath), "csv");
            if (string.IsNullOrEmpty(selected))
                return;

            _lastCsvPath = selected;
            _lastCsvAttempted = true;
            _lastCsvHeaderHash32 = ComputeCsvHeaderHash32(selected);
            bool schemaMatches = _lastCsvHeaderHash32 == CsvSchemaHash32;
            int rowCount = 0;
            bool loaded = schemaMatches && DynamicDecalVaultRuntime.TryLoadMaterialProfilesCsv(selected, out rowCount);
            _lastCsvRows = loaded ? rowCount : 0;
            _lastCsvValid = loaded;
            _csvLabel.text = loaded
                ? string.Concat("CSV profiles loaded: ", rowCount.ToString(CultureInfo.InvariantCulture))
                : schemaMatches ? "CSV profiles rejected" : "CSV schema hash mismatch";
            RefreshBridgeMetadata();
        }

        private void RefreshStats()
        {
            if (_statsLabel == null)
                return;

            RefreshBridgeMetadata();
            DynamicDecalVaultRuntime.TryGetRuntimeState(out DecalRuntimeStateDTO state);
            DynamicDecalVaultRuntime.TryGetLatestTelemetry(out TraumaWoundTelemetryEntry telemetry);
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

        private void RefreshBridgeMetadata()
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            if (_bridgeLabel != null)
            {
                _bridgeLabel.text = string.Concat(
                    "Source CSV: ",
                    DefaultCsvPath,
                    "\nSchema: ",
                    CsvSchemaVersion,
                    " 0x",
                    CsvSchemaHash32.ToString("X8", culture),
                    "\nRuntime route: ",
                    RuntimeProfileVaultRoute,
                    "\nBinary output: ",
                    DataMonolithOutputPath,
                    " (DataMonolith bake not claimed by this facade)");
            }

            if (_layoutLabel != null)
            {
                _layoutLabel.text =
                    "ABI: TraumaDecalDTO 80B [LocalToWorld@0:64, DecalTypeHash@64:4, Opacity01@68:4, BirthTime@72:4, Flags@76:4]; " +
                    "DecalRequestSignal 64B [ImpactAup@0:24, Normal@24:12, Radius@36:4, Depth@40:4, Lifetime@44:4, Material/Flags/Seed/Frame@48..60]; " +
                    "DecalRequestQueueStateDTO 64B [Write@0:4, Read@4:4, Pending@8:4, Capacity@12:4, counters@16..28, pad@32:32]; " +
                    "DecalMaterialProfileDTO 32B [SourceHash@0:4, AtlasSlice@4:4, LifetimeSeconds@8:4, RadiusMeters@12:4, ProjectionDepthMeters@16:4, Flags@20:4, pad@24:8].";
            }

            if (_validationLabel != null)
            {
                string state = _lastCsvAttempted
                    ? (_lastCsvValid ? "PASS" : "FAIL")
                    : "PENDING";
                uint headerHash = _lastCsvAttempted ? _lastCsvHeaderHash32 : CsvSchemaHash32;
                _validationLabel.text = string.Concat(
                    "Last validation: ",
                    state,
                    " | Path: ",
                    _lastCsvPath,
                    " | Rows: ",
                    _lastCsvRows.ToString(culture),
                    " | HeaderHash: 0x",
                    headerHash.ToString("X8", culture));
            }
        }

        private static uint ComputeCsvHeaderHash32(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return 0u;

            try
            {
                using FileStream stream = File.OpenRead(path);
                Span<byte> buffer = stackalloc byte[128];
                int read = stream.Read(buffer);
                if (read <= 0)
                    return 0u;

                int length = 0;
                while (length < read && buffer[length] != (byte)'\n' && buffer[length] != (byte)'\r')
                    length++;

                return ComputeFnv1a32(buffer.Slice(0, length));
            }
            catch
            {
                return 0u;
            }
        }

        private static uint ComputeFnv1a32(string text)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offset;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash = (hash ^ (byte)c) * prime;
            }

            return hash != 0u ? hash : 1u;
        }

        private static uint ComputeFnv1a32(ReadOnlySpan<byte> text)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offset;
            for (int i = 0; i < text.Length; i++)
            {
                byte value = text[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                hash = (hash ^ value) * prime;
            }

            return hash != 0u ? hash : 1u;
        }

        private void DrawSceneGizmos(SceneView sceneView)
        {
            if (_drawGizmoToggle == null || !_drawGizmoToggle.value || sceneView == null)
                return;

            if (!DynamicDecalVaultRuntime.TryAcquireDecalBufferRead(out Unity.Collections.NativeArray<TraumaDecalDTO>.ReadOnly decals, out _, out Vector3 cameraWorldPosition))
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
                    TraumaDecalDTO decal = decals[i];
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
