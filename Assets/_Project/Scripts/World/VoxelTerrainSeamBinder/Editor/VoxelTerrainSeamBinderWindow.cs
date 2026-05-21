#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.World.VoxelTerrainSeamBinder;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.VoxelTerrainSeamBinder.Editor
{
    public sealed class VoxelTerrainSeamBinderWindow : EditorWindow
    {
        private const string ProfilePath = "Assets/_Project/Data/seam_binding_profiles.csv";
        private ObjectField _terrainLod0Field;
        private ObjectField _terrainLod1Field;
        private ObjectField _terrainLod2Field;
        private ObjectField _voxelLod0Field;
        private ObjectField _voxelLod1Field;
        private ObjectField _voxelLod2Field;
        private TextField _assetNameField;
        private IntegerField _profileIndexField;
        private Label _profileHashLabel;
        private Slider _qualitySlider;
        private Slider _snapRadiusSlider;
        private Slider _normalBlendSlider;
        private Slider _textureFalloffSlider;
        private Slider _spatialCellSlider;
        private Slider _lodBiasSlider;
        private DoubleField _terrainAupX;
        private DoubleField _terrainAupY;
        private DoubleField _terrainAupZ;
        private DoubleField _voxelAupX;
        private DoubleField _voxelAupY;
        private DoubleField _voxelAupZ;
        private Toggle _previewToggle;
        private ProgressBar _progressBar;
        private Label _statusLabel;
        private SeamBindingProfileCache _profiles;
        private int _profileCount;
        private bool _previewRefreshQueued;

        [MenuItem("HECTON-8/Voxel Terrain Seam Binder/Open Forge")]
        public static void Open()
        {
            GetWindow<VoxelTerrainSeamBinderWindow>("Voxel-Terrain Seam Binder");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _assetNameField = new TextField("Output Asset Name") { value = "VoxelTerrainSeam" };
            rootVisualElement.Add(_assetNameField);

            _terrainLod0Field = MeshField("Terrain LOD0");
            _terrainLod1Field = MeshField("Terrain LOD1");
            _terrainLod2Field = MeshField("Terrain LOD2");
            _voxelLod0Field = MeshField("Voxel LOD0");
            _voxelLod1Field = MeshField("Voxel LOD1");
            _voxelLod2Field = MeshField("Voxel LOD2");
            rootVisualElement.Add(_terrainLod0Field);
            rootVisualElement.Add(_terrainLod1Field);
            rootVisualElement.Add(_terrainLod2Field);
            rootVisualElement.Add(_voxelLod0Field);
            rootVisualElement.Add(_voxelLod1Field);
            rootVisualElement.Add(_voxelLod2Field);

            _profileIndexField = new IntegerField("Binding Profile Index") { value = 0 };
            _profileIndexField.RegisterValueChangedCallback(OnProfileIndexChanged);
            _profileHashLabel = new Label("Profile: none");
            rootVisualElement.Add(_profileIndexField);
            rootVisualElement.Add(_profileHashLabel);

            _qualitySlider = new Slider("GlobalQualityWeight", 0f, 1f) { value = 0.65f };
            _snapRadiusSlider = new Slider("Snap Radius", 0.02f, 20f) { value = 2f };
            _normalBlendSlider = new Slider("Normal Blend Distance", 0.02f, 40f) { value = 3.5f };
            _textureFalloffSlider = new Slider("Texture Gradient Falloff", 0.02f, 64f) { value = 4f };
            _spatialCellSlider = new Slider("Spatial Hash Cell Size", 0.02f, 64f) { value = 2f };
            _lodBiasSlider = new Slider("LOD Continuity Bias", 0f, 1f) { value = 0.5f };
            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_snapRadiusSlider);
            rootVisualElement.Add(_normalBlendSlider);
            rootVisualElement.Add(_textureFalloffSlider);
            rootVisualElement.Add(_spatialCellSlider);
            rootVisualElement.Add(_lodBiasSlider);

            _terrainAupX = new DoubleField("Terrain AUP X");
            _terrainAupY = new DoubleField("Terrain AUP Y");
            _terrainAupZ = new DoubleField("Terrain AUP Z");
            _voxelAupX = new DoubleField("Voxel AUP X");
            _voxelAupY = new DoubleField("Voxel AUP Y");
            _voxelAupZ = new DoubleField("Voxel AUP Z");
            rootVisualElement.Add(_terrainAupX);
            rootVisualElement.Add(_terrainAupY);
            rootVisualElement.Add(_terrainAupZ);
            rootVisualElement.Add(_voxelAupX);
            rootVisualElement.Add(_voxelAupY);
            rootVisualElement.Add(_voxelAupZ);

            _previewToggle = new Toggle("Publish Scene Preview") { value = true };
            rootVisualElement.Add(_previewToggle);

            Button loadProfiles = new Button(LoadProfiles) { text = "LOAD CSV PROFILES" };
            Button previewButton = new Button(PreviewSeamPulls) { text = "PREVIEW SEAM PULLS" };
            Button stitchButton = new Button(StitchSeams) { text = "STITCH SEAMS" };
            Button mockButton = new Button(RunMockBenchmark) { text = "RUN MOCK SEAM BENCHMARK" };
            Button clearPreviewButton = new Button(VoxelTerrainSeamPreviewStore.Clear) { text = "CLEAR PREVIEW" };
            Button scanButton = new Button(RunDynamicVertexScanner) { text = "SCAN RUNTIME SEAM MUTATION" };
            rootVisualElement.Add(loadProfiles);
            rootVisualElement.Add(previewButton);
            rootVisualElement.Add(stitchButton);
            rootVisualElement.Add(mockButton);
            rootVisualElement.Add(clearPreviewButton);
            rootVisualElement.Add(scanButton);

            _progressBar = new ProgressBar { title = "Idle", lowValue = 0f, highValue = 1f, value = 0f };
            _statusLabel = new Label("No seam stitch has run in this editor session.");
            rootVisualElement.Add(_progressBar);
            rootVisualElement.Add(_statusLabel);
            RegisterPreviewCallbacks();
            LoadProfiles();
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= ExecuteQueuedPreviewRefresh;
            _previewRefreshQueued = false;
        }

        private static ObjectField MeshField(string label)
        {
            return new ObjectField(label) { objectType = typeof(Mesh), allowSceneObjects = false };
        }

        private void LoadProfiles()
        {
            _profiles = default;
            string fullPath = Path.Combine(ProjectRoot(), ProfilePath.Replace('/', Path.DirectorySeparatorChar));
            _profileCount = SeamBindingProfileCsvParser.TryLoad(fullPath, ref _profiles);
            if (_profileCount <= 0)
            {
                _profiles.Set(0, FallbackProfile("dear_lie_default", 0.65f, 2f, 3.5f, 4f, 2f, 0.5f));
                _profiles.Set(1, FallbackProfile("sharp_basalt_cut", 0.55f, 1.2f, 0.35f, 1.5f, 1.2f, 0.35f));
                _profiles.Set(2, FallbackProfile("soft_sand_dune", 0.8f, 2.8f, 6f, 8f, 2.8f, 0.85f));
                _profileCount = 3;
            }

            if (_profileIndexField != null)
                _profileIndexField.SetValueWithoutNotify(0);
            RefreshProfileHashLabel(0);
            ApplyProfile(_profiles.Get(0));
        }

        private void StitchSeams()
        {
            _progressBar.value = 0.05f;
            _progressBar.title = "Preparing native buffers";
            SeamBindingProfileDTO profile = CurrentProfile();
            VoxelTerrainSeamBindRequest request = BuildRequest();

            try
            {
                _progressBar.value = 0.35f;
                _progressBar.title = "Burst spatial hash and snap";
                VoxelTerrainSeamBindResult result = VoxelTerrainSeamBinderPipeline.Stitch(in request, profile);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _progressBar.value = 1f;
                _progressBar.title = "PENDING VERIFICATION";
                SetStitchStatus(in result);
            }
            catch (Exception ex)
            {
                _progressBar.value = 1f;
                _progressBar.title = "FAILED";
                SetExceptionStatus(ex);
                Debug.LogException(ex);
            }
        }

        private void PreviewSeamPulls()
        {
            _progressBar.value = 0.15f;
            _progressBar.title = "Preview native buffers";
            SeamBindingProfileDTO profile = CurrentProfile();
            VoxelTerrainSeamBindRequest request = BuildRequest();
            try
            {
                bool success = VoxelTerrainSeamBinderPipeline.PreviewLod0(in request, profile, out SeamBindCounters64 counters);
                _progressBar.value = 1f;
                _progressBar.title = success ? "Preview PENDING VERIFICATION" : "Preview source missing";
                SetPreviewStatus(success, in counters);
            }
            catch (Exception ex)
            {
                _progressBar.value = 1f;
                _progressBar.title = "PREVIEW FAILED";
                SetExceptionStatus(ex);
                Debug.LogException(ex);
            }
        }

        private void RunMockBenchmark()
        {
            SeamBindCounters64 counters = VoxelTerrainSeamBinderPipeline.RunMockBenchmark(CurrentProfile());
            SetMockStatus(in counters);
            _progressBar.value = 1f;
            _progressBar.title = "Mock benchmark PENDING VERIFICATION";
        }

        private void RunDynamicVertexScanner()
        {
            int findings = Dynamic_Vertex_Scanner.ScanAndWriteReport(ProjectRoot());
            SetScannerStatus(findings);
        }

        private SeamBindingProfileDTO CurrentProfile()
        {
            int index = math.clamp(_profileIndexField == null ? 0 : _profileIndexField.value, 0, math.max(_profileCount - 1, 0));
            SeamBindingProfileDTO profile = _profileCount > 0 ? _profiles.Get(index) : VoxelTerrainSeamBinderPipeline.BuildDefaultProfile();
            RefreshProfileHashLabel(index);
            profile.GlobalQualityWeight = _qualitySlider == null ? profile.GlobalQualityWeight : _qualitySlider.value;
            profile.SnapRadiusMeters = _snapRadiusSlider == null ? profile.SnapRadiusMeters : _snapRadiusSlider.value;
            profile.NormalBlendDistanceMeters = _normalBlendSlider == null ? profile.NormalBlendDistanceMeters : _normalBlendSlider.value;
            profile.TextureGradientFalloffMeters = _textureFalloffSlider == null ? profile.TextureGradientFalloffMeters : _textureFalloffSlider.value;
            profile.SpatialCellSizeMeters = _spatialCellSlider == null ? profile.SpatialCellSizeMeters : _spatialCellSlider.value;
            profile.LodContinuityBias = _lodBiasSlider == null ? profile.LodContinuityBias : _lodBiasSlider.value;
            return profile;
        }

        private VoxelTerrainSeamBindRequest BuildRequest()
        {
            VoxelTerrainSeamBindRequest request = default;
            request.AssetName = _assetNameField == null ? "VoxelTerrainSeam" : _assetNameField.value;
            request.TerrainLod0 = _terrainLod0Field == null ? null : _terrainLod0Field.value as Mesh;
            request.TerrainLod1 = _terrainLod1Field == null ? null : _terrainLod1Field.value as Mesh;
            request.TerrainLod2 = _terrainLod2Field == null ? null : _terrainLod2Field.value as Mesh;
            request.VoxelLod0 = _voxelLod0Field == null ? null : _voxelLod0Field.value as Mesh;
            request.VoxelLod1 = _voxelLod1Field == null ? null : _voxelLod1Field.value as Mesh;
            request.VoxelLod2 = _voxelLod2Field == null ? null : _voxelLod2Field.value as Mesh;
            request.TerrainRootAup = TerrainAup();
            request.VoxelRootAup = VoxelAup();
            request.PublishPreview = _previewToggle == null || _previewToggle.value;
            return request;
        }

        private void OnProfileIndexChanged(ChangeEvent<int> evt)
        {
            int index = math.clamp(evt.newValue, 0, math.max(_profileCount - 1, 0));
            if (_profileIndexField != null && _profileIndexField.value != index)
                _profileIndexField.SetValueWithoutNotify(index);
            if (_profileCount > 0)
                ApplyProfile(_profiles.Get(index));
            RefreshProfileHashLabel(index);
            RequestPreviewRefresh();
        }

        private void RefreshProfileHashLabel(int index)
        {
            if (_profileHashLabel == null)
                return;

            uint hash = _profileCount > 0 ? _profiles.Get(math.clamp(index, 0, math.max(_profileCount - 1, 0))).ProfileHash : 0u;
            _profileHashLabel.text = ProfileHashLabel(hash, _profileCount);
        }

        private static string ProfileHashLabel(uint hash, int count)
        {
            Span<char> buffer = stackalloc char[48];
            const string prefix = "Profile: 0x";
            int cursor = 0;
            for (int i = 0; i < prefix.Length; i++)
                buffer[cursor++] = prefix[i];
            WriteHex8(buffer, ref cursor, hash);
            const string suffix = " / count ";
            for (int i = 0; i < suffix.Length; i++)
                buffer[cursor++] = suffix[i];
            WritePositiveInt(buffer, ref cursor, count);
            return new string(buffer.Slice(0, cursor));
        }

        private static void WriteHex8(Span<char> buffer, ref int cursor, uint value)
        {
            const string digits = "0123456789ABCDEF";
            for (int shift = 28; shift >= 0; shift -= 4)
                buffer[cursor++] = digits[(int)((value >> shift) & 0xFu)];
        }

        private static void WritePositiveInt(Span<char> buffer, ref int cursor, int value)
        {
            value = math.max(value, 0);
            if (value == 0)
            {
                buffer[cursor++] = '0';
                return;
            }

            Span<char> reversed = stackalloc char[10];
            int count = 0;
            while (value > 0 && count < reversed.Length)
            {
                int digit = value % 10;
                reversed[count++] = (char)('0' + digit);
                value /= 10;
            }

            for (int i = count - 1; i >= 0; i--)
                buffer[cursor++] = reversed[i];
        }

        private void ApplyProfile(SeamBindingProfileDTO profile)
        {
            _qualitySlider.SetValueWithoutNotify(math.saturate(profile.GlobalQualityWeight));
            _snapRadiusSlider.SetValueWithoutNotify(math.max(0.02f, profile.SnapRadiusMeters));
            _normalBlendSlider.SetValueWithoutNotify(math.max(0.02f, profile.NormalBlendDistanceMeters));
            _textureFalloffSlider.SetValueWithoutNotify(math.max(0.02f, profile.TextureGradientFalloffMeters));
            _spatialCellSlider.SetValueWithoutNotify(math.max(0.02f, profile.SpatialCellSizeMeters));
            _lodBiasSlider.SetValueWithoutNotify(math.saturate(profile.LodContinuityBias));
        }

        private void RegisterPreviewCallbacks()
        {
            _terrainLod0Field.RegisterValueChangedCallback(OnPreviewObjectChanged);
            _voxelLod0Field.RegisterValueChangedCallback(OnPreviewObjectChanged);
            _qualitySlider.RegisterValueChangedCallback(OnPreviewFloatChanged);
            _snapRadiusSlider.RegisterValueChangedCallback(OnPreviewFloatChanged);
            _normalBlendSlider.RegisterValueChangedCallback(OnPreviewFloatChanged);
            _textureFalloffSlider.RegisterValueChangedCallback(OnPreviewFloatChanged);
            _spatialCellSlider.RegisterValueChangedCallback(OnPreviewFloatChanged);
            _lodBiasSlider.RegisterValueChangedCallback(OnPreviewFloatChanged);
            _terrainAupX.RegisterValueChangedCallback(OnPreviewDoubleChanged);
            _terrainAupY.RegisterValueChangedCallback(OnPreviewDoubleChanged);
            _terrainAupZ.RegisterValueChangedCallback(OnPreviewDoubleChanged);
            _voxelAupX.RegisterValueChangedCallback(OnPreviewDoubleChanged);
            _voxelAupY.RegisterValueChangedCallback(OnPreviewDoubleChanged);
            _voxelAupZ.RegisterValueChangedCallback(OnPreviewDoubleChanged);
        }

        private void OnPreviewObjectChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            RequestPreviewRefresh();
        }

        private void OnPreviewFloatChanged(ChangeEvent<float> evt)
        {
            RequestPreviewRefresh();
        }

        private void OnPreviewDoubleChanged(ChangeEvent<double> evt)
        {
            RequestPreviewRefresh();
        }

        private void RequestPreviewRefresh()
        {
            if (_previewToggle == null || !_previewToggle.value || _previewRefreshQueued)
                return;

            _previewRefreshQueued = true;
            EditorApplication.delayCall -= ExecuteQueuedPreviewRefresh;
            EditorApplication.delayCall += ExecuteQueuedPreviewRefresh;
        }

        private void ExecuteQueuedPreviewRefresh()
        {
            _previewRefreshQueued = false;
            if (this == null || _previewToggle == null || !_previewToggle.value)
                return;
            if (_terrainLod0Field == null || _voxelLod0Field == null || _terrainLod0Field.value == null || _voxelLod0Field.value == null)
                return;

            PreviewSeamPulls();
        }

        private void SetStitchStatus(in VoxelTerrainSeamBindResult result)
        {
            StringBuilder builder = new StringBuilder(128);
            builder.Append("Processed LODs: ");
            builder.Append(result.ProcessedLods);
            builder.Append(" snapped: ");
            builder.Append(result.SnappedVertices);
            builder.Append(" report: ");
            builder.Append(result.ReportPath);
            _statusLabel.text = builder.ToString();
        }

        private void SetPreviewStatus(bool success, in SeamBindCounters64 counters)
        {
            if (!success)
            {
                _statusLabel.text = "Preview requires Terrain LOD0 and Voxel LOD0.";
                return;
            }

            StringBuilder builder = new StringBuilder(96);
            builder.Append("Preview LOD0 snapped: ");
            builder.Append(counters.SnappedVertexCount);
            builder.Append(" lines: ");
            builder.Append(VoxelTerrainSeamPreviewStore.Count);
            _statusLabel.text = builder.ToString();
        }

        private void SetMockStatus(in SeamBindCounters64 counters)
        {
            StringBuilder builder = new StringBuilder(128);
            builder.Append("Mock 500x500 seam snapped ");
            builder.Append(counters.SnappedVertexCount);
            builder.Append(" vertices in ");
            builder.Append(counters.BurstMicroseconds.ToString("0.0", CultureInfo.InvariantCulture));
            builder.Append(" us.");
            _statusLabel.text = builder.ToString();
        }

        private void SetScannerStatus(int findings)
        {
            StringBuilder builder = new StringBuilder(128);
            builder.Append("Runtime seam mutation findings: ");
            builder.Append(findings);
            builder.Append(". Report: Docs/Reports/WORLD_OPTIMIZATION_REPORT.json");
            _statusLabel.text = builder.ToString();
        }

        private void SetExceptionStatus(Exception exception)
        {
            StringBuilder builder = new StringBuilder(256);
            builder.Append(exception.GetType().Name);
            builder.Append(": ");
            builder.Append(exception.Message);
            _statusLabel.text = builder.ToString();
        }

        private double3 TerrainAup()
        {
            return new double3(_terrainAupX.value, _terrainAupY.value, _terrainAupZ.value);
        }

        private double3 VoxelAup()
        {
            return new double3(_voxelAupX.value, _voxelAupY.value, _voxelAupZ.value);
        }

        private static SeamBindingProfileDTO FallbackProfile(string name, float q, float snap, float normal, float texture, float cell, float lodBias)
        {
            SeamBindingProfileDTO profile = default;
            profile.ProfileHash = VoxelTerrainSeamMath.HashAscii(name);
            profile.GlobalQualityWeight = q;
            profile.SnapRadiusMeters = snap;
            profile.NormalBlendDistanceMeters = normal;
            profile.TextureGradientFalloffMeters = texture;
            profile.SpatialCellSizeMeters = cell;
            profile.LodContinuityBias = lodBias;
            profile.PreviewLineColor = new float3(1f, 0.08f, 0.04f);
            return profile;
        }

        private static string ProjectRoot()
        {
            return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
        }
    }

    internal struct SeamBindingProfileCache
    {
        private SeamBindingProfileDTO Profile00;
        private SeamBindingProfileDTO Profile01;
        private SeamBindingProfileDTO Profile02;
        private SeamBindingProfileDTO Profile03;
        private SeamBindingProfileDTO Profile04;
        private SeamBindingProfileDTO Profile05;
        private SeamBindingProfileDTO Profile06;
        private SeamBindingProfileDTO Profile07;
        private SeamBindingProfileDTO Profile08;
        private SeamBindingProfileDTO Profile09;
        private SeamBindingProfileDTO Profile10;
        private SeamBindingProfileDTO Profile11;
        private SeamBindingProfileDTO Profile12;
        private SeamBindingProfileDTO Profile13;
        private SeamBindingProfileDTO Profile14;
        private SeamBindingProfileDTO Profile15;

        public SeamBindingProfileDTO Get(int index)
        {
            switch (math.clamp(index, 0, VoxelTerrainSeamConstants.SeamProfileCapacity - 1))
            {
                case 0: return Profile00;
                case 1: return Profile01;
                case 2: return Profile02;
                case 3: return Profile03;
                case 4: return Profile04;
                case 5: return Profile05;
                case 6: return Profile06;
                case 7: return Profile07;
                case 8: return Profile08;
                case 9: return Profile09;
                case 10: return Profile10;
                case 11: return Profile11;
                case 12: return Profile12;
                case 13: return Profile13;
                case 14: return Profile14;
                default: return Profile15;
            }
        }

        public void Set(int index, in SeamBindingProfileDTO profile)
        {
            switch (math.clamp(index, 0, VoxelTerrainSeamConstants.SeamProfileCapacity - 1))
            {
                case 0: Profile00 = profile; break;
                case 1: Profile01 = profile; break;
                case 2: Profile02 = profile; break;
                case 3: Profile03 = profile; break;
                case 4: Profile04 = profile; break;
                case 5: Profile05 = profile; break;
                case 6: Profile06 = profile; break;
                case 7: Profile07 = profile; break;
                case 8: Profile08 = profile; break;
                case 9: Profile09 = profile; break;
                case 10: Profile10 = profile; break;
                case 11: Profile11 = profile; break;
                case 12: Profile12 = profile; break;
                case 13: Profile13 = profile; break;
                case 14: Profile14 = profile; break;
                default: Profile15 = profile; break;
            }
        }
    }

    internal static class SeamBindingProfileCsvParser
    {
        private const int MaxProfileCsvBytes = 32768;

        public static int TryLoad(string path, ref SeamBindingProfileCache profiles)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return 0;

            FileInfo info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > MaxProfileCsvBytes)
                return 0;

            NativeArray<byte> bytes = default;
            try
            {
                int byteCount = (int)info.Length;
                bytes = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                unsafe
                {
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(bytes);
                    Span<byte> target = new Span<byte>(ptr, byteCount);
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        int read = 0;
                        while (read < byteCount)
                        {
                            int chunk = stream.Read(target.Slice(read));
                            if (chunk <= 0)
                                break;
                            read += chunk;
                        }

                        if (read != byteCount)
                            return 0;
                    }

                    return ParseProfiles(target, ref profiles);
                }
            }
            finally
            {
                if (bytes.IsCreated)
                    bytes.Dispose();
            }
        }

        private static int ParseProfiles(ReadOnlySpan<byte> bytes, ref SeamBindingProfileCache profiles)
        {
            int index = 0;
            int count = 0;
            SkipLine(bytes, ref index);
            while (index < bytes.Length && count < VoxelTerrainSeamConstants.SeamProfileCapacity)
            {
                SeamBindingProfileDTO profile = default;
                profile.ProfileHash = ReadKeyHash(bytes, ref index);
                if (profile.ProfileHash == VoxelTerrainSeamMath.HashAscii(string.Empty))
                {
                    SkipLine(bytes, ref index);
                    continue;
                }

                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.GlobalQualityWeight);
                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.SnapRadiusMeters);
                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.NormalBlendDistanceMeters);
                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.TextureGradientFalloffMeters);
                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.SpatialCellSizeMeters);
                ConsumeComma(bytes, ref index);
                TryReadFloat(bytes, ref index, out profile.LodContinuityBias);
                profile.GlobalQualityWeight = math.saturate(profile.GlobalQualityWeight);
                profile.SnapRadiusMeters = math.max(profile.SnapRadiusMeters, 0.02f);
                profile.NormalBlendDistanceMeters = math.max(profile.NormalBlendDistanceMeters, 0.02f);
                profile.TextureGradientFalloffMeters = math.max(profile.TextureGradientFalloffMeters, 0.02f);
                profile.SpatialCellSizeMeters = math.max(profile.SpatialCellSizeMeters, 0.02f);
                profile.LodContinuityBias = math.saturate(profile.LodContinuityBias);
                profile.PreviewLineColor = new float3(1f, 0.08f, 0.04f);
                profiles.Set(count++, profile);
                SkipLine(bytes, ref index);
            }

            return count;
        }

        private static uint ReadKeyHash(ReadOnlySpan<byte> bytes, ref int index)
        {
            uint hash = 2166136261u;
            SkipValueWhitespace(bytes, ref index);
            while (index < bytes.Length)
            {
                byte c = bytes[index];
                if (c == (byte)',' || c == (byte)'\n' || c == (byte)'\r')
                    break;

                hash = VoxelTerrainSeamMath.HashBytes(c, hash);
                index++;
            }

            return VoxelTerrainSeamMath.Hash(hash);
        }

        private static bool TryReadFloat(ReadOnlySpan<byte> bytes, ref int index, out float value)
        {
            value = 0f;
            SkipValueWhitespace(bytes, ref index);
            if (index >= bytes.Length)
                return false;

            float sign = 1f;
            if (bytes[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (bytes[index] == (byte)'+')
            {
                index++;
            }

            bool readAny = false;
            float integer = 0f;
            while (index < bytes.Length)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                integer = (integer * 10f) + (c - (byte)'0');
                readAny = true;
                index++;
            }

            float fraction = 0f;
            if (index < bytes.Length && bytes[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                while (index < bytes.Length)
                {
                    byte c = bytes[index];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;

                    fraction += (c - (byte)'0') * place;
                    place *= 0.1f;
                    readAny = true;
                    index++;
                }
            }

            value = (integer + fraction) * sign;
            return readAny && math.isfinite(value);
        }

        private static void ConsumeComma(ReadOnlySpan<byte> bytes, ref int index)
        {
            SkipValueWhitespace(bytes, ref index);
            if (index < bytes.Length && bytes[index] == (byte)',')
                index++;
        }

        private static void SkipValueWhitespace(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length)
            {
                byte c = bytes[index];
                if (c != (byte)' ' && c != (byte)'\t')
                    break;
                index++;
            }
        }

        private static void SkipLine(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length && bytes[index] != (byte)'\n')
                index++;
            if (index < bytes.Length)
                index++;
        }
    }
}
#endif
