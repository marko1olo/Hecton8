using System;
using Hecton8.Core.Memory;
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

            _statusLabel.text = "Wrote " + result.H8BinPath + " | faults " + result.FaultCount + " | runs " + result.RleRunCount + " | warnings 0x" + result.WarningFlags.ToString("X8");
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
                    out NativeArray<FaultLineParamsDTO> faults,
                    out NativeArray<ThermalVentSpawnDTO> vents,
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
        private const SystemID PreviewMemoryOwner = SystemID.ContentAuthority;
        // H8MEMORY_TRACKED_EDITOR_PREVIEW: static editor preview cache is allocated and released through H8Memory.
        private static NativeArray<FaultLineParamsDTO> s_faults;
        private static NativeArray<ThermalVentSpawnDTO> s_vents;
        private static int s_faultCount;
        private static int s_ventCount;
        private static double3 s_previewOriginAUP;
        private static bool s_hasPreview;
        private static JobHandle s_previewHandle;
        private static bool s_previewPending;
        private static bool s_hasQueuedRebuild;
        private static HadalTrenchBakeConfigDTO s_queuedConfig;

        static HadalTrenchPreviewStore()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        public static bool Rebuild(HadalTrenchBakeConfigDTO config)
        {
            if (s_previewPending)
            {
                if (!s_previewHandle.IsCompleted)
                {
                    s_queuedConfig = config;
                    s_hasQueuedRebuild = true;
                    return false;
                }

                s_previewHandle.Complete();
                s_previewPending = false;
                s_hasPreview = true;
            }

            Dispose();
            config.FaultGridX = math.clamp(config.FaultGridX, 1, 128);
            config.FaultGridZ = math.clamp(config.FaultGridZ, 1, 128);
            config.FaultCount = config.FaultGridX * config.FaultGridZ * 2;
            s_faultCount = math.min(config.FaultCount, HadalTrenchBakeConstants.MaxPreviewFaults);
            s_ventCount = s_faultCount;
            s_faults = H8Memory.Allocate<FaultLineParamsDTO>(s_faultCount, PreviewMemoryOwner, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            s_vents = H8Memory.Allocate<ThermalVentSpawnDTO>(s_ventCount, PreviewMemoryOwner, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (!s_faults.IsCreated || !s_vents.IsCreated)
            {
                Dispose();
                return false;
            }

            config.FaultCount = s_faultCount;
            s_previewOriginAUP = config.SectorOriginAUP;
            JobHandle faultsHandle = new GenerateTectonicNetworkJob { Faults = s_faults, Config = config }.Schedule(math.max(1, s_faultCount >> 1), 32);
            s_previewHandle = new GenerateThermalVentNodesJob { Faults = s_faults, Vents = s_vents, Config = config }.Schedule(s_faultCount, 32, faultsHandle);
            s_previewPending = true;
            s_hasPreview = false;
            EditorApplication.update -= PumpPreview;
            EditorApplication.update += PumpPreview;
            return true;
        }

        private static void PumpPreview()
        {
            if (!s_previewPending)
            {
                EditorApplication.update -= PumpPreview;
                return;
            }

            if (!s_previewHandle.IsCompleted)
                return;

            s_previewHandle.Complete();
            s_previewPending = false;
            if (s_hasQueuedRebuild)
            {
                HadalTrenchBakeConfigDTO config = s_queuedConfig;
                s_hasQueuedRebuild = false;
                Rebuild(config);
                return;
            }

            s_hasPreview = true;
            EditorApplication.update -= PumpPreview;
            SceneView.RepaintAll();
        }

        public static bool TryGetCounts(out int faultCount, out int ventCount)
        {
            faultCount = s_faultCount;
            ventCount = s_ventCount;
            return s_previewPending || s_hasPreview;
        }

        public static bool TryReadPreview(
            out NativeArray<FaultLineParamsDTO> faults,
            out NativeArray<ThermalVentSpawnDTO> vents,
            out int faultCount,
            out int ventCount,
            out double3 previewOriginAUP)
        {
            faults = s_faults;
            vents = s_vents;
            faultCount = s_faultCount;
            ventCount = s_ventCount;
            previewOriginAUP = s_previewOriginAUP;
            return s_hasPreview && s_faults.IsCreated && s_vents.IsCreated;
        }

        public static void Dispose()
        {
            EditorApplication.update -= PumpPreview;
            if (s_previewPending)
            {
                s_previewHandle.Complete();
                s_previewPending = false;
            }

            s_hasQueuedRebuild = false;
            if (s_faults.IsCreated)
                H8Memory.Release(ref s_faults, PreviewMemoryOwner);
            if (s_vents.IsCreated)
                H8Memory.Release(ref s_vents, PreviewMemoryOwner);
            s_faultCount = 0;
            s_ventCount = 0;
            s_previewOriginAUP = double3.zero;
            s_hasPreview = false;
        }
    }
}
