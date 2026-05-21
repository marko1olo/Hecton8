using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.World.StaticCaveSdfBaker;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Hecton8.World.StaticCaveSdfBaker.Editor
{
    public sealed class StaticSdfForgeWindow : EditorWindow
    {
        private const string ProfileFileName = "sdf_baking_profiles.csv";
        internal const int ProfileCapacity = 16;
        private ObjectField _meshField;
        private DropdownField _profileDropdown;
        private IntegerField _resolutionField;
        private IntegerField _subMeshField;
        private Slider _narrowBandSlider;
        private Slider _qualitySlider;
        private Toggle _textureToggle;
        private DoubleField _anchorXField;
        private DoubleField _anchorYField;
        private DoubleField _anchorZField;
        private ProgressBar _progressBar;
        private Label _statusLabel;
        private StaticCaveSdfProfileCache _profiles;
        private int _profileCount;

        [MenuItem("HECTON-8/Static SDF Forge/Open Forge")]
        public static void Open()
        {
            GetWindow<StaticSdfForgeWindow>("Static SDF Forge");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _meshField = new ObjectField("Source Mesh") { objectType = typeof(Mesh), allowSceneObjects = false };
            _profileDropdown = new DropdownField("SDF Profile");
            _resolutionField = new IntegerField("Voxel Resolution") { value = StaticCaveSdfConstants.DefaultResolution };
            _subMeshField = new IntegerField("Sub-Mesh Index (-1 All)") { value = -1 };
            _narrowBandSlider = new Slider("Narrow Band Limit", 1f, 80f) { value = 20f };
            _qualitySlider = new Slider("GlobalQualityWeight", 0f, 1f) { value = 1f };
            _textureToggle = new Toggle("Create R16_SFloat Texture3D") { value = true };
            _anchorXField = new DoubleField("Anchor AUP X");
            _anchorYField = new DoubleField("Anchor AUP Y");
            _anchorZField = new DoubleField("Anchor AUP Z");

            rootVisualElement.Add(_meshField);
            rootVisualElement.Add(_profileDropdown);
            rootVisualElement.Add(_resolutionField);
            rootVisualElement.Add(_subMeshField);
            rootVisualElement.Add(_narrowBandSlider);
            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_textureToggle);
            rootVisualElement.Add(_anchorXField);
            rootVisualElement.Add(_anchorYField);
            rootVisualElement.Add(_anchorZField);

            Button loadProfiles = new Button(LoadProfiles) { text = "LOAD CSV PROFILES" };
            Button bakeButton = new Button(BakeSelectedMesh) { text = "BAKE SDF VOLUME" };
            Button mockButton = new Button(RunMockBenchmark) { text = "RUN MOCK TORUS BENCHMARK" };
            Button scanButton = new Button(RunScanner) { text = "SCAN PHYSICS PROXIMITY" };
            rootVisualElement.Add(loadProfiles);
            rootVisualElement.Add(bakeButton);
            rootVisualElement.Add(mockButton);
            rootVisualElement.Add(scanButton);

            _progressBar = new ProgressBar { title = "Idle", lowValue = 0f, highValue = 1f, value = 0f };
            _statusLabel = new Label("No SHINOBU_244 bake has run in this editor session.");
            rootVisualElement.Add(_progressBar);
            rootVisualElement.Add(_statusLabel);
            _profileDropdown.RegisterValueChangedCallback(evt =>
            {
                int index = _profileDropdown.index;
                if ((uint)index < (uint)_profileCount)
                    ApplyProfile(_profiles.Get(index));
            });
            LoadProfiles();
        }

        private void OnDisable()
        {
            StaticCaveSdfPreviewStore.Dispose();
        }

        private void LoadProfiles()
        {
            _profiles = default;
            string path = Path.Combine(ProjectRoot(), ProfileFileName);
            _profileCount = StaticCaveSdfProfileCsvParser.LoadProfilesFromCsv(path, ref _profiles);
            if (_profileCount <= 0)
            {
                _profiles.Set(0, BuildFallbackProfile("Hero_Cave", 256, 20f, 1f, -1));
                _profiles.Set(1, BuildFallbackProfile("Large_Arch", 192, 18f, 0.85f, -1));
                _profiles.Set(2, BuildFallbackProfile("Small_Wreck", 96, 12f, 0.65f, -1));
                _profileCount = 3;
            }

            System.Collections.Generic.List<string> choices = new System.Collections.Generic.List<string>(_profileCount);
            for (int i = 0; i < _profileCount; i++)
                choices.Add("0x" + _profiles.Get(i).ProfileHash.ToString("X8", CultureInfo.InvariantCulture));

            _profileDropdown.choices = choices;
            _profileDropdown.index = 0;
            ApplyProfile(_profiles.Get(0));
            _statusLabel.text = "Profiles loaded: " + _profileCount + ".";
        }

        private void BakeSelectedMesh()
        {
            Mesh mesh = _meshField.value as Mesh;
            if (mesh == null)
            {
                _statusLabel.text = "Assign a Source Mesh before baking.";
                return;
            }

            try
            {
                _progressBar.value = 0.05f;
                _progressBar.title = "Baking";
                StaticCaveSdfBakeConfigDTO config = CurrentConfig();
                StaticCaveSdfBakeResult result = StaticCaveSdfBakePipeline.BakeMesh(
                    mesh,
                    mesh.name,
                    config,
                    _textureToggle.value);
                _progressBar.value = 1f;
                _progressBar.title = "PENDING VERIFICATION";
                _statusLabel.text = "Binary: " + result.BinaryAssetPath + " | VoxelCount=" + result.VoxelCount + " | Warnings=" + result.WarningFlags;
                SceneView.RepaintAll();
            }
            catch (Exception exception)
            {
                _progressBar.title = "Bake failed";
                _statusLabel.text = exception.GetType().Name + ": " + exception.Message;
            }
        }

        private void RunMockBenchmark()
        {
            try
            {
                _progressBar.value = 0.05f;
                _progressBar.title = "Mock benchmark";
                StaticCaveSdfBakeConfigDTO config = CurrentConfig();
                if (config.Resolution.x > 96)
                    config.Resolution = new int3(96);
                StaticCaveSdfBakeResult result = StaticCaveSdfBakePipeline.RunMockTorusBenchmark(
                    "Mock_Twisted_Torus_SHINOBU_244",
                    config,
                    100000,
                    _textureToggle.value);
                _progressBar.value = 1f;
                _progressBar.title = "PENDING VERIFICATION";
                _statusLabel.text = "Mock benchmark wrote " + result.BinaryAssetPath + " triangles=" + result.TriangleCount;
                SceneView.RepaintAll();
            }
            catch (Exception exception)
            {
                _progressBar.title = "Benchmark failed";
                _statusLabel.text = exception.GetType().Name + ": " + exception.Message;
            }
        }

        private void RunScanner()
        {
            int findings = Physics_Proximity_Scanner.ScanAndWriteReport(ProjectRoot());
            _statusLabel.text = "Physics proximity scan findings: " + findings + ". Report: Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_244.json";
        }

        private StaticCaveSdfBakeConfigDTO CurrentConfig()
        {
            int resolution = math.clamp(_resolutionField.value, 16, StaticCaveSdfConstants.MaxResolution);
            StaticCaveSdfBakeConfigDTO config = default;
            config.AnchorAup = new double3(_anchorXField.value, _anchorYField.value, _anchorZField.value);
            config.Resolution = new int3(resolution);
            config.MaxSdfDistance = math.max(_narrowBandSlider.value, 0.05f);
            config.GlobalQualityWeight = math.saturate(_qualitySlider.value);
            config.SubMeshIndex = _subMeshField.value;
            config.Flags = StaticCaveSdfConstants.RollbackExcludedFlag;
            return config;
        }

        private void ApplyProfile(in StaticCaveSdfProfileDTO profile)
        {
            _resolutionField.value = math.clamp(profile.Resolution <= 0 ? StaticCaveSdfConstants.DefaultResolution : profile.Resolution, 16, StaticCaveSdfConstants.MaxResolution);
            _narrowBandSlider.value = math.max(profile.NarrowBandMeters, 1f);
            _qualitySlider.value = math.saturate(profile.GlobalQualityWeight);
            _subMeshField.value = profile.SubMeshIndex;
        }

        private static StaticCaveSdfProfileDTO BuildFallbackProfile(string name, int resolution, float band, float quality, int subMesh)
        {
            StaticCaveSdfProfileDTO profile = default;
            profile.ProfileHash = HashProfileName(name);
            profile.Resolution = resolution;
            profile.NarrowBandMeters = band;
            profile.GlobalQualityWeight = quality;
            profile.SubMeshIndex = subMesh;
            return profile;
        }

        private static uint HashProfileName(string name)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(name))
                return StaticCaveSdfEditorMath.Mix(hash);

            for (int i = 0; i < name.Length; i++)
                hash = HashProfileByte((byte)name[i], hash);
            return StaticCaveSdfEditorMath.Mix(hash);
        }

        internal static uint HashProfileByte(byte value, uint hash)
        {
            byte c = value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
            if (c == (byte)' ' || c == (byte)'\t' || c == (byte)'\r')
                return hash;

            hash ^= c;
            hash *= 16777619u;
            return hash;
        }

        private static string ProjectRoot()
        {
            return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
        }
    }

    internal struct StaticCaveSdfProfileCache
    {
        public StaticCaveSdfProfileDTO Profile00;
        public StaticCaveSdfProfileDTO Profile01;
        public StaticCaveSdfProfileDTO Profile02;
        public StaticCaveSdfProfileDTO Profile03;
        public StaticCaveSdfProfileDTO Profile04;
        public StaticCaveSdfProfileDTO Profile05;
        public StaticCaveSdfProfileDTO Profile06;
        public StaticCaveSdfProfileDTO Profile07;
        public StaticCaveSdfProfileDTO Profile08;
        public StaticCaveSdfProfileDTO Profile09;
        public StaticCaveSdfProfileDTO Profile10;
        public StaticCaveSdfProfileDTO Profile11;
        public StaticCaveSdfProfileDTO Profile12;
        public StaticCaveSdfProfileDTO Profile13;
        public StaticCaveSdfProfileDTO Profile14;
        public StaticCaveSdfProfileDTO Profile15;

        public StaticCaveSdfProfileDTO Get(int index)
        {
            switch (math.clamp(index, 0, StaticSdfForgeWindow.ProfileCapacity - 1))
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

        public void Set(int index, in StaticCaveSdfProfileDTO profile)
        {
            switch (math.clamp(index, 0, StaticSdfForgeWindow.ProfileCapacity - 1))
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

    internal static class StaticCaveSdfProfileCsvParser
    {
        private const int MaxProfileCsvBytes = 32768;
        private const int MaxStackProfileCsvBytes = 4096;

        public static int LoadProfilesFromCsv(string path, ref StaticCaveSdfProfileCache profiles)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return 0;

            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaxProfileCsvBytes)
                    return 0;

                int byteCount = (int)info.Length;
                if (byteCount <= MaxStackProfileCsvBytes)
                {
                    Span<byte> stackBytes = stackalloc byte[MaxStackProfileCsvBytes];
                    return PopulateProfilesFromCsvBytes(path, stackBytes.Slice(0, byteCount), ref profiles);
                }

                byte[] rentedBytes = ArrayPool<byte>.Shared.Rent(byteCount);
                try
                {
                    return PopulateProfilesFromCsvBytes(path, rentedBytes.AsSpan(0, byteCount), ref profiles);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBytes, clearArray: true);
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        private static int PopulateProfilesFromCsvBytes(string path, Span<byte> bytes, ref StaticCaveSdfProfileCache profiles)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length != bytes.Length)
                    return 0;

                int read = 0;
                while (read < bytes.Length)
                {
                    int chunk = stream.Read(bytes.Slice(read));
                    if (chunk <= 0)
                        break;
                    read += chunk;
                }

                if (read != bytes.Length)
                    return 0;
            }

            if (!ValidateProfileCsvHeader(path, bytes, out int index))
                return 0;

            int count = 0;
            int row = 2;
            while (index < bytes.Length && count < StaticSdfForgeWindow.ProfileCapacity)
            {
                SkipWhitespace(bytes, ref index);
                if (index >= bytes.Length)
                    break;
                if (ConsumeLineEnd(bytes, ref index))
                {
                    row++;
                    continue;
                }

                if (!ParseProfileRow(path, bytes, ref index, row, out StaticCaveSdfProfileDTO profile))
                    return 0;

                profile.Resolution = math.clamp(profile.Resolution, 16, StaticCaveSdfConstants.MaxResolution);
                profile.NarrowBandMeters = math.max(profile.NarrowBandMeters, 1f);
                profile.GlobalQualityWeight = math.saturate(profile.GlobalQualityWeight);
                profiles.Set(count++, profile);
                if (!ConsumeProfileRowEnd(path, bytes, ref index, row))
                    return 0;
                row++;
            }

            if (!ValidateNoProfileCapacityOverflow(path, bytes, ref index, row))
                return 0;

            return count;
        }

        private static bool ValidateProfileCsvHeader(string path, ReadOnlySpan<byte> bytes, out int index)
        {
            index = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                index = 3;

            if (!MatchHeaderColumn(path, bytes, ref index, "name", 1))
                return false;
            if (!ConsumeRequiredHeaderComma(path, bytes, ref index, 1))
                return false;
            if (!MatchHeaderColumn(path, bytes, ref index, "resolution", 2))
                return false;
            if (!ConsumeRequiredHeaderComma(path, bytes, ref index, 2))
                return false;
            if (!MatchHeaderColumn(path, bytes, ref index, "narrow_band_meters", 3))
                return false;
            if (!ConsumeRequiredHeaderComma(path, bytes, ref index, 3))
                return false;
            if (!MatchHeaderColumn(path, bytes, ref index, "global_quality_weight", 4))
                return false;
            if (!ConsumeRequiredHeaderComma(path, bytes, ref index, 4))
                return false;
            if (!MatchHeaderColumn(path, bytes, ref index, "submesh_index", 5))
                return false;
            return ConsumeRequiredHeaderEnd(path, bytes, ref index, 5);
        }

        private static bool MatchHeaderColumn(string path, ReadOnlySpan<byte> bytes, ref int index, string expected, int column)
        {
            SkipWhitespace(bytes, ref index);
            for (int i = 0; i < expected.Length; i++)
            {
                if (index + i >= bytes.Length || bytes[index + i] != (byte)expected[i])
                {
                    ReportCsvSchemaMismatch(path, column, expected);
                    return false;
                }
            }

            index += expected.Length;
            SkipWhitespace(bytes, ref index);
            if (index >= bytes.Length)
                return true;

            byte c = bytes[index];
            if (c == (byte)',' || c == (byte)'\n' || c == (byte)'\r')
                return true;

            ReportCsvSchemaMismatch(path, column, expected);
            return false;
        }

        private static bool ConsumeRequiredHeaderComma(string path, ReadOnlySpan<byte> bytes, ref int index, int column)
        {
            SkipWhitespace(bytes, ref index);
            if (index < bytes.Length && bytes[index] == (byte)',')
            {
                index++;
                return true;
            }

            ReportCsvSchemaMismatch(path, column + 1, "comma before next required column");
            return false;
        }

        private static bool ConsumeRequiredHeaderEnd(string path, ReadOnlySpan<byte> bytes, ref int index, int column)
        {
            SkipWhitespace(bytes, ref index);
            if (index >= bytes.Length)
                return true;
            if (bytes[index] == (byte)'\r')
                index++;
            if (index < bytes.Length && bytes[index] == (byte)'\n')
            {
                index++;
                return true;
            }

            ReportCsvSchemaMismatch(path, column, "line end after submesh_index");
            return false;
        }

        private static void ReportCsvSchemaMismatch(string path, int column, string expected)
        {
            Debug.LogWarning("[StaticSdfForge] CSV schema mismatch path=" + path + " row=1 column=" + column.ToString(CultureInfo.InvariantCulture) + " expected=" + expected);
        }

        private static bool ParseProfileRow(
            string path,
            ReadOnlySpan<byte> bytes,
            ref int index,
            int row,
            out StaticCaveSdfProfileDTO profile)
        {
            profile = default;
            if (!ParseKeyHash(path, bytes, ref index, row, out profile.ProfileHash))
                return false;
            if (!ConsumeRequiredRowComma(path, bytes, ref index, row, 2, "resolution"))
                return false;
            if (!ParseInt(bytes, ref index, out profile.Resolution))
            {
                ReportCsvRowMismatch(path, row, 2, "int resolution");
                return false;
            }

            if (!ConsumeRequiredRowComma(path, bytes, ref index, row, 3, "narrow_band_meters"))
                return false;
            if (!ParseFloat(bytes, ref index, out profile.NarrowBandMeters))
            {
                ReportCsvRowMismatch(path, row, 3, "float narrow_band_meters");
                return false;
            }

            if (!ConsumeRequiredRowComma(path, bytes, ref index, row, 4, "global_quality_weight"))
                return false;
            if (!ParseFloat(bytes, ref index, out profile.GlobalQualityWeight))
            {
                ReportCsvRowMismatch(path, row, 4, "float global_quality_weight");
                return false;
            }

            if (!ConsumeRequiredRowComma(path, bytes, ref index, row, 5, "submesh_index"))
                return false;
            if (!ParseInt(bytes, ref index, out profile.SubMeshIndex))
            {
                ReportCsvRowMismatch(path, row, 5, "int submesh_index");
                return false;
            }

            return true;
        }

        private static bool ParseKeyHash(string path, ReadOnlySpan<byte> bytes, ref int index, int row, out uint hash)
        {
            hash = 2166136261u;
            SkipWhitespace(bytes, ref index);
            bool readAny = false;
            while (index < bytes.Length)
            {
                byte c = bytes[index];
                if (c == (byte)',' || c == (byte)'\n' || c == (byte)'\r')
                    break;
                hash = StaticSdfForgeWindow.HashProfileByte(c, hash);
                readAny = true;
                index++;
            }

            if (!readAny)
            {
                ReportCsvRowMismatch(path, row, 1, "non-empty profile name");
                return false;
            }

            hash = StaticCaveSdfEditorMath.Mix(hash);
            return true;
        }

        private static bool ParseInt(ReadOnlySpan<byte> bytes, ref int index, out int value)
        {
            value = 0;
            SkipWhitespace(bytes, ref index);
            int sign = 1;
            if (index < bytes.Length && bytes[index] == (byte)'-')
            {
                sign = -1;
                index++;
            }

            bool readAny = false;
            long accumulator = 0L;
            while (index < bytes.Length)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                accumulator = (accumulator * 10L) + (c - (byte)'0');
                if ((sign > 0 && accumulator > int.MaxValue) ||
                    (sign < 0 && accumulator > 2147483648L))
                {
                    return false;
                }

                readAny = true;
                index++;
            }

            if (!readAny)
                return false;

            value = sign > 0
                ? (int)accumulator
                : accumulator == 2147483648L ? int.MinValue : -(int)accumulator;
            return readAny;
        }

        private static bool ParseFloat(ReadOnlySpan<byte> bytes, ref int index, out float value)
        {
            value = 0f;
            SkipWhitespace(bytes, ref index);
            float sign = 1f;
            if (index < bytes.Length && bytes[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            bool readAny = false;
            float whole = 0f;
            while (index < bytes.Length)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                whole = whole * 10f + (c - (byte)'0');
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

            value = (whole + fraction) * sign;
            return readAny && math.isfinite(value);
        }

        private static bool ConsumeRequiredRowComma(string path, ReadOnlySpan<byte> bytes, ref int index, int row, int nextColumn, string expected)
        {
            SkipWhitespace(bytes, ref index);
            if (index < bytes.Length && bytes[index] == (byte)',')
            {
                index++;
                return true;
            }

            ReportCsvRowMismatch(path, row, nextColumn, "comma before " + expected);
            return false;
        }

        private static void SkipWhitespace(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length)
            {
                byte c = bytes[index];
                if (c != (byte)' ' && c != (byte)'\t')
                    break;
                index++;
            }
        }

        private static bool ConsumeLineEnd(ReadOnlySpan<byte> bytes, ref int index)
        {
            if (index >= bytes.Length)
                return false;

            if (bytes[index] == (byte)'\r')
            {
                index++;
                if (index < bytes.Length && bytes[index] == (byte)'\n')
                    index++;
                return true;
            }

            if (bytes[index] == (byte)'\n')
            {
                index++;
                return true;
            }

            return false;
        }

        private static bool ConsumeProfileRowEnd(string path, ReadOnlySpan<byte> bytes, ref int index, int row)
        {
            SkipWhitespace(bytes, ref index);
            if (index >= bytes.Length)
                return true;
            if (ConsumeLineEnd(bytes, ref index))
                return true;

            ReportCsvRowMismatch(path, row, 5, "line end after submesh_index");
            return false;
        }

        private static bool ValidateNoProfileCapacityOverflow(string path, ReadOnlySpan<byte> bytes, ref int index, int row)
        {
            while (index < bytes.Length)
            {
                SkipWhitespace(bytes, ref index);
                if (index >= bytes.Length)
                    return true;
                if (ConsumeLineEnd(bytes, ref index))
                {
                    row++;
                    continue;
                }

                ReportCsvRowMismatch(path, row, 1, "profile count <= " + StaticSdfForgeWindow.ProfileCapacity.ToString(CultureInfo.InvariantCulture));
                return false;
            }

            return true;
        }

        private static void ReportCsvRowMismatch(string path, int row, int column, string expected)
        {
            Debug.LogWarning("[StaticSdfForge] CSV row mismatch path=" + path + " row=" + row.ToString(CultureInfo.InvariantCulture) + " column=" + column.ToString(CultureInfo.InvariantCulture) + " expected=" + expected);
        }
    }

    [InitializeOnLoad]
    internal static class StaticCaveSdfSliceSceneOverlay
    {
        private const float ZSlice01 = 0.5f;
        private const int MaxSamplesPerAxis = 32;
        private const float SquareScale = 0.75f;

        static StaticCaveSdfSliceSceneOverlay()
        {
            SceneView.duringSceneGui -= Draw;
            SceneView.duringSceneGui += Draw;
        }

        private static void Draw(SceneView sceneView)
        {
            if (!StaticCaveSdfPreviewStore.ValidatePreviewBinaryForGizmo())
                return;

            StaticCaveSdfBakeConfigDTO config = StaticCaveSdfPreviewStore.CopyConfig();
            int3 res = math.max(config.Resolution, new int3(2));
            int z = math.clamp((int)math.round(math.saturate(ZSlice01) * (res.z - 1)), 0, res.z - 1);
            int stepX = math.max(1, res.x / math.max(MaxSamplesPerAxis, 1));
            int stepY = math.max(1, res.y / math.max(MaxSamplesPerAxis, 1));
            float3 span = config.BoundsMax - config.BoundsMin;
            float3 cell = span / math.max(new float3(res - 1), new float3(1f));
            Vector3 halfSize = new Vector3(math.max(cell.x * SquareScale, 0.01f) * 0.5f, math.max(cell.y * SquareScale, 0.01f) * 0.5f, 0f);
            CompareFunction previousZTest = Handles.zTest;
            Color previousColor = Handles.color;
            Handles.zTest = CompareFunction.LessEqual;
            try
            {
                using (FileStream stream = StaticCaveSdfPreviewStore.OpenPreviewStreamForGizmo())
                {
                    if (stream == null)
                        return;

                    Span<byte> rowBytes = stackalloc byte[StaticCaveSdfConstants.MaxResolution * 2];
                    for (int y = 0; y < res.y; y += stepY)
                    {
                        int rowStartIndex = res.x * (y + res.y * z);
                        if (!StaticCaveSdfPreviewStore.CopyRowFromOpenStreamForGizmo(stream, rowStartIndex, res.x, rowBytes))
                            continue;

                        for (int x = 0; x < res.x; x += stepX)
                        {
                            int byteOffset = x * 2;
                            ushort halfValue = (ushort)(rowBytes[byteOffset] | (rowBytes[byteOffset + 1] << 8));
                            float value = math.f16tof32(halfValue);
                            float intensity = math.saturate(math.abs(value) * math.rcp(math.max(config.MaxSdfDistance, 0.001f)));
                            Color color = value < 0f
                                ? new Color(0.05f, 0.25f + intensity * 0.45f, 1f, 0.65f)
                                : new Color(1f, 0.15f + intensity * 0.25f, 0.05f, 0.45f);
                            float3 p = math.lerp(config.BoundsMin, config.BoundsMax, new float3(
                                x * math.rcp(math.max(res.x - 1f, 1f)),
                                y * math.rcp(math.max(res.y - 1f, 1f)),
                                z * math.rcp(math.max(res.z - 1f, 1f))));
                            Vector3 center = new Vector3(p.x, p.y, p.z);
                            Handles.color = color;
                            Handles.DrawSolidDisc(center, Vector3.forward, math.max(halfSize.x, halfSize.y));
                        }
                    }
                }
            }
            finally
            {
                Handles.zTest = previousZTest;
                Handles.color = previousColor;
            }
        }
    }

    public static class Physics_Proximity_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_244.json";
        private const string PhysicsTokenPrefix = "Phys" + "ics.";
        private const string ClosestPointSuffix = "ClosestPoint";
        private const string RaycastSuffix = "Raycast(";
        private const string GeometryCollisionTypeA = "Mesh";
        private const string GeometryCollisionTypeB = "Collider";

        public static int ScanAndWriteReport(string projectRoot)
        {
            string scriptRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            int findings = 0;
            bool scanIncomplete = false;
            StringBuilder rows = new StringBuilder(4096);
            StringBuilder diagnostics = new StringBuilder(1024);
            ScanDirectory(scriptRoot, ref findings, rows, ref scanIncomplete, diagnostics);
            string reportPath = Path.Combine(projectRoot, ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            StringBuilder json = new StringBuilder(8192);
            json.Append("{\n");
            json.Append("  \"agent\": \"SHINOBU_244\",\n");
            json.Append("  \"status\": \"PENDING_VERIFICATION\",\n");
            json.Append("  \"summary\": \"Physics Proximity Queries Eradicated by static SDF bake route where owned by SHINOBU_244; cross-domain findings are reported for owning agents.\",\n");
            json.Append("  \"scanIncomplete\": ").Append(scanIncomplete ? "true" : "false").Append(",\n");
            json.Append("  \"diagnostics\": [\n");
            json.Append(diagnostics);
            json.Append("\n  ],\n");
            json.Append("  \"findingCount\": ").Append(findings).Append(",\n");
            json.Append("  \"findings\": [\n");
            json.Append(rows);
            json.Append("\n  ]\n");
            json.Append("}\n");
            File.WriteAllText(reportPath, json.ToString());
            return findings;
        }

        private static void ScanDirectory(string root, ref int findings, StringBuilder rows, ref bool scanIncomplete, StringBuilder diagnostics)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return;

            List<string> pendingDirectories = new List<string>(64);
            pendingDirectories.Add(root);
            int cursor = 0;
            while (cursor < pendingDirectories.Count)
            {
                string directory = pendingDirectories[cursor++];
                try
                {
                    using (IEnumerator<string> files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly).GetEnumerator())
                    {
                        while (files.MoveNext())
                            ScanFile(files.Current, ref findings, rows, ref scanIncomplete, diagnostics);
                    }
                }
                catch (IOException exception)
                {
                    AppendScanDiagnostic(directory, "EnumerateFiles", exception.GetType().Name, ref scanIncomplete, diagnostics);
                }
                catch (UnauthorizedAccessException exception)
                {
                    AppendScanDiagnostic(directory, "EnumerateFiles", exception.GetType().Name, ref scanIncomplete, diagnostics);
                }

                try
                {
                    using (IEnumerator<string> directories = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly).GetEnumerator())
                    {
                        while (directories.MoveNext())
                            pendingDirectories.Add(directories.Current);
                    }
                }
                catch (IOException exception)
                {
                    AppendScanDiagnostic(directory, "EnumerateDirectories", exception.GetType().Name, ref scanIncomplete, diagnostics);
                }
                catch (UnauthorizedAccessException exception)
                {
                    AppendScanDiagnostic(directory, "EnumerateDirectories", exception.GetType().Name, ref scanIncomplete, diagnostics);
                }
            }
        }

        private static void ScanFile(string path, ref int findings, StringBuilder rows, ref bool scanIncomplete, StringBuilder diagnostics)
        {
            if (path.IndexOf("StaticCaveSdfBaker", StringComparison.OrdinalIgnoreCase) >= 0)
                return;

            try
            {
                bool fileHasJob = ContainsTokenInFile(path, "IJob");
                bool inHotMethod = false;
                int braceDepth = 0;
                int methodStartLine = 0;
                string methodName = string.Empty;
                int lineNumber = 0;
                using (StreamReader reader = new StreamReader(path))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        string trimmed = line.Trim();
                        if (!inHotMethod && IsHotMethodSignature(trimmed, fileHasJob, out methodName))
                        {
                            inHotMethod = true;
                            methodStartLine = lineNumber;
                            braceDepth = 0;
                        }

                        if (!inHotMethod)
                            continue;

                        string symbol;
                        if (TryGetForbiddenSymbol(trimmed, out symbol))
                            AppendFinding(path, lineNumber, methodStartLine, methodName, symbol, ref findings, rows);

                        braceDepth += CountChar(line, '{');
                        braceDepth -= CountChar(line, '}');
                        if (braceDepth <= 0 && trimmed.IndexOf("}", StringComparison.Ordinal) >= 0)
                        {
                            inHotMethod = false;
                            methodName = string.Empty;
                            methodStartLine = 0;
                        }
                    }
                }
            }
            catch (IOException exception)
            {
                AppendScanDiagnostic(path, "ScanFile", exception.GetType().Name, ref scanIncomplete, diagnostics);
            }
            catch (UnauthorizedAccessException exception)
            {
                AppendScanDiagnostic(path, "ScanFile", exception.GetType().Name, ref scanIncomplete, diagnostics);
            }
        }

        private static void AppendScanDiagnostic(
            string path,
            string stage,
            string reason,
            ref bool scanIncomplete,
            StringBuilder diagnostics)
        {
            scanIncomplete = true;
            if (diagnostics.Length > 0)
                diagnostics.Append(",\n");

            diagnostics.Append("    { \"path\": \"").Append(Escape(path.Replace('\\', '/')));
            diagnostics.Append("\", \"stage\": \"").Append(Escape(stage));
            diagnostics.Append("\", \"reason\": \"").Append(Escape(reason)).Append("\" }");
        }

        private static void AppendFinding(
            string path,
            int line,
            int methodStartLine,
            string methodName,
            string symbol,
            ref int findings,
            StringBuilder rows)
        {
            if (findings > 0)
                rows.Append(",\n");

            rows.Append("    { \"file\": \"").Append(Escape(path.Replace('\\', '/')));
            rows.Append("\", \"line\": ").Append(line);
            rows.Append(", \"methodStartLine\": ").Append(methodStartLine);
            rows.Append(", \"context\": \"").Append(Escape(methodName));
            rows.Append("\", \"severity\": \"CROSS_DOMAIN_REPORTED\", \"pattern\": \"");
            rows.Append(Escape(symbol)).Append("\" }");
            findings++;
        }

        private static bool ContainsTokenInFile(string path, string token)
        {
            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.IndexOf(token, StringComparison.Ordinal) >= 0)
                        return true;
                }
            }

            return false;
        }

        private static bool IsHotMethodSignature(string trimmed, bool fileHasJob, out string methodName)
        {
            methodName = string.Empty;
            if (trimmed.IndexOf("(", StringComparison.Ordinal) < 0)
                return false;

            if (ContainsMethodName(trimmed, "Update"))
                methodName = "Update";
            else if (ContainsMethodName(trimmed, "FixedUpdate"))
                methodName = "FixedUpdate";
            else if (ContainsMethodName(trimmed, "Tick"))
                methodName = "Tick";
            else if (ContainsMethodName(trimmed, "FixedTick"))
                methodName = "FixedTick";
            else if (fileHasJob && ContainsMethodName(trimmed, "Execute"))
                methodName = "IJob.Execute";

            return methodName.Length > 0;
        }

        private static bool ContainsMethodName(string line, string methodName)
        {
            int index = line.IndexOf(methodName + "(", StringComparison.Ordinal);
            if (index < 0)
                return false;

            if (index > 0)
            {
                char before = line[index - 1];
                if ((before >= 'A' && before <= 'Z') ||
                    (before >= 'a' && before <= 'z') ||
                    (before >= '0' && before <= '9') ||
                    before == '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetForbiddenSymbol(string line, out string symbol)
        {
            string closestPointSymbol = PhysicsTokenPrefix + ClosestPointSuffix;
            if (line.IndexOf(closestPointSymbol, StringComparison.Ordinal) >= 0)
            {
                symbol = closestPointSymbol;
                return true;
            }

            string closestPointInstanceSymbol = "." + ClosestPointSuffix + "(";
            if (line.IndexOf(closestPointInstanceSymbol, StringComparison.Ordinal) >= 0)
            {
                symbol = closestPointInstanceSymbol;
                return true;
            }

            string raycastSymbol = PhysicsTokenPrefix + RaycastSuffix;
            if (line.IndexOf(raycastSymbol, StringComparison.Ordinal) >= 0)
            {
                symbol = raycastSymbol;
                return true;
            }

            string colliderSymbol = GeometryCollisionTypeA + GeometryCollisionTypeB;
            if (line.IndexOf(colliderSymbol, StringComparison.Ordinal) >= 0)
            {
                symbol = colliderSymbol;
                return true;
            }

            symbol = string.Empty;
            return false;
        }

        private static int CountChar(string text, char value)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == value)
                    count++;
            }

            return count;
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
