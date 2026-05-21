#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor.GeographySanity
{
    public sealed class WorldSanityCheckerWindow : EditorWindow
    {
        private Toggle _floating;
        private Toggle _buried;
        private Toggle _crushDepth;
        private Toggle _connectivity;
        private Toggle _mockFallback;
        private Slider _qualityWeight;
        private IntegerField _sectorCountX;
        private IntegerField _sectorCountZ;
        private IntegerField _heightResolution;
        private IntegerField _sdfResolution;
        private IntegerField _entitiesPerSector;
        private IntegerField _navigationRequestsPerSector;
        private IntegerField _connectivityResolution;
        private IntegerField _verticalProbeSteps;
        private FloatField _verticalProbeStepMeters;
        private FloatField _maxFloatingDistance;
        private ProgressBar _progress;
        private Label _status;

        [MenuItem("Tools/Hecton8/World Sanity Checker/World Sanity Checker", false, 247)]
        public static void Open()
        {
            WorldSanityCheckerWindow window = GetWindow<WorldSanityCheckerWindow>("World Sanity Checker");
            window.minSize = new Vector2(480f, 420f);
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            GeographySanitySettings defaults = GeographySanityPipeline.DefaultSettings();
            _floating = new Toggle("Check Floating") { value = defaults.CheckFloating };
            _buried = new Toggle("Check Buried") { value = defaults.CheckBuried };
            _crushDepth = new Toggle("Check Crush Depth") { value = defaults.CheckCrushDepth };
            _connectivity = new Toggle("Check Connectivity") { value = defaults.CheckConnectivity };
            _mockFallback = new Toggle("Use Mock Data When Sector Files Are Missing") { value = defaults.UseMockDataWhenSectorFilesMissing };
            _qualityWeight = new Slider("Global Quality Weight", 0f, 1f) { value = defaults.GlobalQualityWeight };
            _sectorCountX = new IntegerField("Sector Count X") { value = defaults.SectorCountX };
            _sectorCountZ = new IntegerField("Sector Count Z") { value = defaults.SectorCountZ };
            _heightResolution = new IntegerField("Height Resolution") { value = defaults.HeightResolution };
            _sdfResolution = new IntegerField("SDF Resolution") { value = defaults.SdfResolution };
            _entitiesPerSector = new IntegerField("Entities Per Sector") { value = defaults.EntitiesPerSector };
            _navigationRequestsPerSector = new IntegerField("Navigation Requests Per Sector") { value = defaults.NavigationRequestsPerSector };
            _connectivityResolution = new IntegerField("Connectivity Grid") { value = defaults.ConnectivityResolution };
            _verticalProbeSteps = new IntegerField("Vertical Probe Steps") { value = defaults.VerticalProbeSteps };
            _verticalProbeStepMeters = new FloatField("Vertical Probe Step Meters") { value = defaults.VerticalProbeStepMeters };
            _maxFloatingDistance = new FloatField("Max Floating Distance") { value = defaults.MaxFloatingDistance };
            _progress = new ProgressBar { title = "Sector Progress", lowValue = 0f, highValue = 1f, value = 0f };
            _status = new Label("Idle. Reports are STATIC_SOURCE until Unity validation runs.");

            rootVisualElement.Add(_floating);
            rootVisualElement.Add(_buried);
            rootVisualElement.Add(_crushDepth);
            rootVisualElement.Add(_connectivity);
            rootVisualElement.Add(_mockFallback);
            rootVisualElement.Add(_qualityWeight);
            rootVisualElement.Add(_sectorCountX);
            rootVisualElement.Add(_sectorCountZ);
            rootVisualElement.Add(_heightResolution);
            rootVisualElement.Add(_sdfResolution);
            rootVisualElement.Add(_entitiesPerSector);
            rootVisualElement.Add(_navigationRequestsPerSector);
            rootVisualElement.Add(_connectivityResolution);
            rootVisualElement.Add(_verticalProbeSteps);
            rootVisualElement.Add(_verticalProbeStepMeters);
            rootVisualElement.Add(_maxFloatingDistance);
            rootVisualElement.Add(_progress);
            rootVisualElement.Add(_status);

            rootVisualElement.Add(new Button(RunMockBenchmark) { text = "RUN MOCK BENCHMARK" });
            rootVisualElement.Add(new Button(ValidateEntireWorld) { text = "VALIDATE ENTIRE WORLD" });
            rootVisualElement.Add(new Button(GeographySanityPipeline.Cancel) { text = "Cancel Validation" });
            rootVisualElement.Add(new Button(RunProfileLoad) { text = "Load CSV Profiles" });
            rootVisualElement.Add(new Button(RunRuntimeScanner) { text = "Run Runtime Spatial Query Scanner" });
            rootVisualElement.Add(new Button(GeographySanityLayoutAssertion.AssertMenu) { text = "Assert DTO Layouts" });
            rootVisualElement.Add(new Button(GeographySanitySelfAudit.RunAndWriteReport) { text = "Run Self Audit" });
        }

        private void RunMockBenchmark()
        {
            GeographySanityMetricsDTO metrics = GeographySanityPipeline.RunMockBenchmark();
            SetStatusMockBenchmark(metrics);
        }

        private void ValidateEntireWorld()
        {
            GeographySanitySettings settings = ResolveSettings();
            if (!GeographySanityPipeline.ValidateEntireWorldAsync(settings, SetProgress))
            {
                SetStatus("Validation already running.");
                return;
            }

            SetStatus("Validation started. STATUS remains PENDING VERIFICATION until report and compile/runtime proof exist.");
        }

        private void RunProfileLoad()
        {
            NativeList<SanityProfileDTO> profiles = default;
            try
            {
                profiles = GeographySanityProfileCsv.LoadProfiles(Allocator.TempJob, out int rows, out int errors);
                SetStatusProfileLoad(rows, errors, profiles.Length);
            }
            finally
            {
                if (profiles.IsCreated)
                    profiles.Dispose();
            }
        }

        private void RunRuntimeScanner()
        {
            int findings = Runtime_Spatial_Query_Scanner.RunAndWriteReport();
            SetStatusRuntimeScanner(findings);
        }

        private GeographySanitySettings ResolveSettings()
        {
            GeographySanitySettings settings = GeographySanityPipeline.DefaultSettings();
            settings.CheckFloating = _floating == null || _floating.value;
            settings.CheckBuried = _buried == null || _buried.value;
            settings.CheckCrushDepth = _crushDepth == null || _crushDepth.value;
            settings.CheckConnectivity = _connectivity == null || _connectivity.value;
            settings.UseMockDataWhenSectorFilesMissing = _mockFallback == null || _mockFallback.value;
            settings.ForceMockData = false;
            settings.GlobalQualityWeight = _qualityWeight != null ? math.saturate(_qualityWeight.value) : 1f;
            settings.SectorCountX = _sectorCountX != null ? math.clamp(_sectorCountX.value, 1, GeographySanityConstants.MaximumSectorCountAxis) : settings.SectorCountX;
            settings.SectorCountZ = _sectorCountZ != null ? math.clamp(_sectorCountZ.value, 1, GeographySanityConstants.MaximumSectorCountAxis) : settings.SectorCountZ;
            settings.HeightResolution = _heightResolution != null ? math.clamp(_heightResolution.value, 2, GeographySanityConstants.MaximumHeightResolution) : settings.HeightResolution;
            settings.SdfResolution = _sdfResolution != null ? math.clamp(_sdfResolution.value, 4, GeographySanityConstants.MaximumSdfResolution) : settings.SdfResolution;
            settings.EntitiesPerSector = _entitiesPerSector != null ? math.clamp(_entitiesPerSector.value, 1, GeographySanityConstants.MaximumEntitiesPerSector) : settings.EntitiesPerSector;
            settings.NavigationRequestsPerSector = _navigationRequestsPerSector != null ? math.clamp(_navigationRequestsPerSector.value, 0, GeographySanityConstants.MaximumNavigationRequestsPerSector) : settings.NavigationRequestsPerSector;
            settings.ConnectivityResolution = _connectivityResolution != null ? math.clamp(_connectivityResolution.value, 4, GeographySanityConstants.MaximumConnectivityResolution) : settings.ConnectivityResolution;
            settings.VerticalProbeSteps = _verticalProbeSteps != null ? math.clamp(_verticalProbeSteps.value, 1, GeographySanityConstants.MaximumVerticalProbeSteps) : settings.VerticalProbeSteps;
            settings.VerticalProbeStepMeters = _verticalProbeStepMeters != null ? math.max(0.05f, _verticalProbeStepMeters.value) : settings.VerticalProbeStepMeters;
            settings.MaxFloatingDistance = _maxFloatingDistance != null ? math.max(0.01f, _maxFloatingDistance.value) : settings.MaxFloatingDistance;
            return settings;
        }

        private void SetProgress(float value)
        {
            if (_progress == null)
                return;

            _progress.value = math.saturate(value);
            _progress.MarkDirtyRepaint();
            Repaint();
        }

        private void SetStatus(string value)
        {
            if (_status != null)
                _status.text = value;
            Repaint();
        }

        private void SetStatusMockBenchmark(GeographySanityMetricsDTO metrics)
        {
            Span<char> buffer = stackalloc char[160];
            int cursor = 0;
            AppendText(buffer, ref cursor, "Mock benchmark wrote report. Floating=".AsSpan());
            AppendInt(buffer, ref cursor, metrics.FloatingCount);
            AppendText(buffer, ref cursor, ", Buried=".AsSpan());
            AppendInt(buffer, ref cursor, metrics.BuriedCount);
            AppendText(buffer, ref cursor, ", Crush=".AsSpan());
            AppendInt(buffer, ref cursor, metrics.CrushDepthCount);
            AppendText(buffer, ref cursor, ", NavTrap=".AsSpan());
            AppendInt(buffer, ref cursor, metrics.NavigationTrapCount);
            AppendText(buffer, ref cursor, ".".AsSpan());
            SetStatus(new string(buffer.Slice(0, cursor)));
        }

        private void SetStatusProfileLoad(int rows, int errors, int nativeRows)
        {
            Span<char> buffer = stackalloc char[96];
            int cursor = 0;
            AppendText(buffer, ref cursor, "CSV profiles loaded: rows=".AsSpan());
            AppendInt(buffer, ref cursor, rows);
            AppendText(buffer, ref cursor, ", errors=".AsSpan());
            AppendInt(buffer, ref cursor, errors);
            AppendText(buffer, ref cursor, ", nativeRows=".AsSpan());
            AppendInt(buffer, ref cursor, nativeRows);
            AppendText(buffer, ref cursor, ".".AsSpan());
            SetStatus(new string(buffer.Slice(0, cursor)));
        }

        private void SetStatusRuntimeScanner(int findings)
        {
            Span<char> buffer = stackalloc char[96];
            int cursor = 0;
            AppendText(buffer, ref cursor, "Runtime spatial query scanner findings=".AsSpan());
            AppendInt(buffer, ref cursor, findings);
            AppendText(buffer, ref cursor, ". Agent report written.".AsSpan());
            SetStatus(new string(buffer.Slice(0, cursor)));
        }

        private static void AppendText(Span<char> destination, ref int cursor, ReadOnlySpan<char> value)
        {
            value.CopyTo(destination.Slice(cursor));
            cursor += value.Length;
        }

        private static void AppendInt(Span<char> destination, ref int cursor, int value)
        {
            if (!value.TryFormat(destination.Slice(cursor), out int written))
                return;

            cursor += written;
        }
    }
}
#endif
