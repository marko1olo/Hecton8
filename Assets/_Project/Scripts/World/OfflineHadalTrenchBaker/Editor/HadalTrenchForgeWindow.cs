using System;
using System.Globalization;
using Hecton8.World.OfflineHadalTrenchBaker;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.OfflineHadalTrenchBaker.Editor
{
    public sealed class HadalTrenchForgeWindow : EditorWindow
    {
        private Slider _qualitySlider;
        private FloatField _cellSizeField;
        private FloatField _widthField;
        private FloatField _depthField;
        private FloatField _noiseField;
        private FloatField _frequencyField;
        private IntegerField _resolutionField;
        private IntegerField _faultGridField;
        private Label _statusLabel;
        private TectonicRiftProfileDTO _activeProfile;
        private bool _bakeInFlight;
        private bool _hasActiveProfile;

        [MenuItem("HECTON-8/Hadal Trench Forge/Open Forge")]
        public static void Open()
        {
            GetWindow<HadalTrenchForgeWindow>("Hadal Trench Forge");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _resolutionField = new IntegerField("Voxel Resolution") { value = 128 };
            _faultGridField = new IntegerField("Voronoi Grid") { value = HadalTrenchBakeConstants.DefaultFaultGridX };
            _cellSizeField = new FloatField("Voronoi Cell Size") { value = 3200f };
            _widthField = new FloatField("Trench Width") { value = 420f };
            _depthField = new FloatField("Depth Falloff") { value = 5000f };
            _noiseField = new FloatField("Noise Displacement") { value = 96f };
            _frequencyField = new FloatField("Noise Frequency") { value = 0.0025f };
            _qualitySlider = new Slider("Global Quality Weight", 0f, 1f) { value = 0.7f };
            rootVisualElement.Add(_resolutionField);
            rootVisualElement.Add(_faultGridField);
            rootVisualElement.Add(_cellSizeField);
            rootVisualElement.Add(_widthField);
            rootVisualElement.Add(_depthField);
            rootVisualElement.Add(_noiseField);
            rootVisualElement.Add(_frequencyField);
            rootVisualElement.Add(_qualitySlider);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.Add(new Button(LoadCsvProfile) { text = "Load CSV" });
            buttons.Add(new Button(RefreshPreview) { text = "Preview Faults" });
            buttons.Add(new Button(CarveTrenches) { text = "CARVE TRENCHES" });
            buttons.Add(new Button(Manual_Trench_Scanner.ScanAndReportMenu) { text = "Scan Manual Geometry" });
            rootVisualElement.Add(buttons);

            _statusLabel = new Label("No trench bake run in this editor session.");
            _statusLabel.style.marginTop = 8;
            rootVisualElement.Add(_statusLabel);
            LoadCsvProfile();
            RefreshPreview();
        }

        private void OnDisable()
        {
            HadalTrenchPreviewStore.Dispose();
        }

        private void LoadCsvProfile()
        {
            NativeList<TectonicRiftProfileDTO> profiles = new NativeList<TectonicRiftProfileDTO>(8, Allocator.Temp);
            try
            {
                TectonicRiftProfileCsvParser.LoadProfiles(profiles);
                if (profiles.Length <= 0)
                    return;

                TectonicRiftProfileDTO profile = profiles[0];
                _activeProfile = profile;
                _hasActiveProfile = true;
                _cellSizeField.value = profile.VoronoiCellSizeMeters;
                _widthField.value = profile.TrenchWidthMeters;
                _depthField.value = profile.TrenchDepthMeters;
                _noiseField.value = profile.NoiseIntensity;
                _frequencyField.value = profile.NoiseFrequency;
                _qualitySlider.value = profile.GlobalQualityWeight;
                _statusLabel.text = "Loaded tectonic profile: " + profile.Name.ToString();
            }
            finally
            {
                if (profiles.IsCreated)
                    profiles.Dispose();
            }
        }

        private void RefreshPreview()
        {
            HadalTrenchBakeConfigDTO config = BuildConfig();
            bool scheduled = HadalTrenchPreviewStore.Rebuild(config);
            SceneView.RepaintAll();
            HadalTrenchPreviewStore.TryGetCounts(out int faultCount, out int ventCount);
            if (_statusLabel != null)
                _statusLabel.text = scheduled
                    ? "Preview scheduled: " + faultCount + " segments, vents: " + ventCount + "."
                    : "Preview queued behind active fault job.";
        }

        private void CarveTrenches()
        {
            if (_bakeInFlight)
            {
                _statusLabel.text = "Bake already running.";
                return;
            }

            HadalTrenchBakeConfigDTO config = BuildConfig();
            _bakeInFlight = HadalTrenchBakePipeline.BakeAsync(config, OnBakeCompleted, OnBakeFailed);
            _statusLabel.text = _bakeInFlight ? "Trench bake scheduled." : "Bake rejected; another trench bake is active.";
        }

        private HadalTrenchBakeConfigDTO BuildConfig()
        {
            HadalTrenchBakeConfigDTO config = HadalTrenchBakePipeline.DefaultConfig();
            if (_hasActiveProfile)
                TectonicRiftProfileCsvParser.ApplyToConfig(in _activeProfile, ref config);

            config.Resolution = new int3(math.clamp(_resolutionField.value, 32, 256));
            config.FaultGridX = math.clamp(_faultGridField.value, 1, 128);
            config.FaultGridZ = config.FaultGridX;
            config.VoronoiCellSizeMeters = ClampFinite(_cellSizeField.value, 1f, 20000f, 3200f);
            config.DefaultWidthMeters = ClampFinite(_widthField.value, 1f, 5000f, 420f);
            config.DefaultDepthMeters = ClampFinite(_depthField.value, 1f, 10000f, 5000f);
            config.NoiseIntensity = ClampFinite(_noiseField.value, 0f, 512f, 96f);
            config.NoiseFrequency = ClampFinite(_frequencyField.value, 0.00001f, 0.05f, 0.0025f);
            config.GlobalQualityWeight = ClampFinite(_qualitySlider.value, 0f, 1f, 0.7f);
            config.FaultGridX = math.clamp(_faultGridField.value, 1, 128);
            config.FaultGridZ = config.FaultGridX;
            config.FaultCount = config.FaultGridX * config.FaultGridZ * 2;
            config.MaxVentCount = config.FaultCount;
            return config;
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (!math.isfinite(value))
                return fallback;
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private void OnBakeCompleted(HadalTrenchBakeResult result)
        {
            _bakeInFlight = false;
            if (_statusLabel == null)
                return;

                _statusLabel.text = "Wrote " + result.H8BinPath + " | faults " + result.FaultCount + " | runs " + result.RleRunCount + " | warnings 0x" + result.WarningFlags.ToString("X8", CultureInfo.InvariantCulture);
        }

        private void OnBakeFailed(Exception exception)
        {
            _bakeInFlight = false;
            if (_statusLabel == null)
                return;

            _statusLabel.text = "Trench bake failed: " + exception.GetType().Name;
        }
    }

    [DisallowMultipleComponent]
    public sealed class HadalTrenchPreviewGizmo : MonoBehaviour
    {
        [SerializeField, Tooltip("Draws Hadal Trench Forge Voronoi faults and thermal vent anchors in Scene View.")]
        private bool drawPreview = true;

        private void OnDrawGizmos()
        {
            if (drawPreview)
                HadalTrenchPreviewDrawer.Draw();
        }
    }

    [InitializeOnLoad]
    internal static class HadalTrenchPreviewSceneOverlay
    {
        static HadalTrenchPreviewSceneOverlay()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        private static void Dispose()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private static void OnSceneGui(SceneView _)
        {
            HadalTrenchPreviewDrawer.Draw();
        }
    }

    internal static class HadalTrenchPreviewDrawer
    {
        private static readonly Vector3[] LineScratch = new Vector3[2];

        public static void Draw()
        {
            if (!HadalTrenchPreviewStore.TryReadPreview(
                    out FaultLineParamsDTO[] faults,
                    out ThermalVentSpawnDTO[] vents,
                    out int faultCount,
                    out int ventCount,
                    out double3 previewOrigin))
            {
                return;
            }

            Handles.color = new Color(1f, 0.05f, 0.02f, 0.9f);
            faultCount = math.min(faultCount, faults.Length);
            for (int i = 0; i < faultCount; i++)
            {
                FaultLineParamsDTO fault = faults[i];
                float3 localStart = HadalTrenchBakeMath.LocalizeAup(fault.StartAUP, previewOrigin);
                float3 localEnd = HadalTrenchBakeMath.LocalizeAup(fault.EndAUP, previewOrigin);
                LineScratch[0] = new Vector3(localStart.x, localStart.y, localStart.z);
                LineScratch[1] = new Vector3(localEnd.x, localEnd.y, localEnd.z);
                Handles.DrawAAPolyLine(4f, LineScratch);
            }

            Handles.color = new Color(0.1f, 0.35f, 1f, 0.85f);
            ventCount = math.min(ventCount, vents.Length);
            for (int i = 0; i < ventCount; i++)
            {
                float3 localVent = HadalTrenchBakeMath.LocalizeAup(vents[i].PositionAUP, previewOrigin);
                Vector3 center = new Vector3(localVent.x, localVent.y, localVent.z);
                float radius = math.max(8f, vents[i].RadiusMeters);
                Handles.SphereHandleCap(0, center, Quaternion.identity, radius * 2f, EventType.Repaint);
            }
        }
    }

    internal static class HadalTrenchPreviewStore
    {
        private static FaultLineParamsDTO[] s_faults;
        private static ThermalVentSpawnDTO[] s_vents;
        private static int s_faultCount;
        private static int s_ventCount;
        private static double3 s_previewOriginAUP;
        private static bool s_hasPreview;

        static HadalTrenchPreviewStore()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        public static bool Rebuild(HadalTrenchBakeConfigDTO config)
        {
            Dispose();
            config.FaultGridX = math.clamp(config.FaultGridX, 1, 128);
            config.FaultGridZ = math.clamp(config.FaultGridZ, 1, 128);
            config.FaultCount = config.FaultGridX * config.FaultGridZ * 2;
            s_faultCount = math.min(config.FaultCount, HadalTrenchBakeConstants.MaxPreviewFaults);
            s_ventCount = s_faultCount;
            NativeArray<FaultLineParamsDTO> faults = new NativeArray<FaultLineParamsDTO>(s_faultCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<ThermalVentSpawnDTO> vents = new NativeArray<ThermalVentSpawnDTO>(s_ventCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            try
            {
                config.FaultCount = s_faultCount;
                s_previewOriginAUP = config.SectorOriginAUP;
                GenerateTectonicNetworkJob faultsJob = new GenerateTectonicNetworkJob { Faults = faults, Config = config };
                GenerateThermalVentNodesJob ventsJob = new GenerateThermalVentNodesJob { Faults = faults, Vents = vents, Config = config };
                ventsJob.Schedule(s_faultCount, 32, faultsJob.Schedule(math.max(1, s_faultCount >> 1), 32)).Complete();

                s_faults = new FaultLineParamsDTO[s_faultCount];
                s_vents = new ThermalVentSpawnDTO[s_ventCount];
                for (int i = 0; i < s_faultCount; i++)
                    s_faults[i] = faults[i];
                for (int i = 0; i < s_ventCount; i++)
                    s_vents[i] = vents[i];

                s_hasPreview = true;
                SceneView.RepaintAll();
                return true;
            }
            finally
            {
                if (faults.IsCreated)
                    faults.Dispose();
                if (vents.IsCreated)
                    vents.Dispose();
            }
        }

        public static bool TryGetCounts(out int faultCount, out int ventCount)
        {
            faultCount = s_faultCount;
            ventCount = s_ventCount;
            return s_hasPreview;
        }

        public static bool TryReadPreview(
            out FaultLineParamsDTO[] faults,
            out ThermalVentSpawnDTO[] vents,
            out int faultCount,
            out int ventCount,
            out double3 previewOriginAUP)
        {
            faults = s_faults;
            vents = s_vents;
            faultCount = s_faultCount;
            ventCount = s_ventCount;
            previewOriginAUP = s_previewOriginAUP;
            return s_hasPreview && s_faults != null && s_vents != null;
        }

        public static void Dispose()
        {
            s_faults = null;
            s_vents = null;
            s_faultCount = 0;
            s_ventCount = 0;
            s_previewOriginAUP = double3.zero;
            s_hasPreview = false;
        }
    }
}
