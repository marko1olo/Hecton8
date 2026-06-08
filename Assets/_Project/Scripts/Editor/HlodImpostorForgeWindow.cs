#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed unsafe class HlodImpostorForgeWindow : EditorWindow
    {
        private const string DefaultProfilePath = "Assets/_Project/Data/impostor_generation_profiles.csv";
        private const int MaxProfiles = 32;
        private const int MaxPreviewViews = 64;
        private const string NativeMemoryOwner = nameof(HlodImpostorForgeWindow);

        private ObjectField _folderField;
        private ObjectField _singlePrefabField;
        private PopupField<string> _profilePopup;
        private SliderInt _viewSlider;
        private SliderInt _atlasSlider;
        private SliderInt _dilationSlider;
        private Slider _paddingSlider;
        private Slider _swapDistanceSlider;
        private Toggle _hemisphereToggle;
        private ProgressBar _progressBar;
        private Label _statusLabel;

        private HlodImpostorProfileRecord[] _profiles;
        private HlodImpostorCaptureAngleRecord[] _previewRecords;
        private readonly List<string> _profileNames = new List<string>(MaxProfiles);
        private int _profileCount;
        private GameObject _previewTarget;
        private int _previewCount;

        [MenuItem("HECTON-8/Rendering/HLOD Impostor Forge", false, 2499)]
        public static void Open()
        {
            GetWindow<HlodImpostorForgeWindow>("HLOD Impostor Forge");
        }

        private void OnEnable()
        {
            _profiles = new HlodImpostorProfileRecord[MaxProfiles]; // COLD EDITOR ALLOC: profile cache for UI Toolkit choices - owner: HlodImpostorForgeWindow
            _previewRecords = new HlodImpostorCaptureAngleRecord[MaxPreviewViews]; // COLD EDITOR ALLOC: SceneView preview cache - owner: HlodImpostorForgeWindow
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            LoadProfiles(DefaultProfilePath);
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            _profiles = null;
            _previewRecords = null;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _singlePrefabField = new ObjectField("Preview / Single Prefab") { objectType = typeof(GameObject), allowSceneObjects = true };
            _folderField = new ObjectField("Prefab Folder") { objectType = typeof(DefaultAsset), allowSceneObjects = false };
            _profilePopup = new PopupField<string>("Profile", _profileNames, _profileNames.Count > 0 ? 0 : -1);
            _viewSlider = new SliderInt("Hemisphere Angles", 8, 32) { value = HectonOctahedralImpostorData.ViewCount, showInputField = true };
            _atlasSlider = new SliderInt("Atlas Resolution", 1024, 8192) { value = HectonOctahedralImpostorData.DefaultAtlasSize, showInputField = true };
            _dilationSlider = new SliderInt("Dilation Radius", 0, 16) { value = 4, showInputField = true };
            _paddingSlider = new Slider("Capture Padding", 0f, 8f) { value = 0.75f, showInputField = true };
            _swapDistanceSlider = new Slider("Swap Distance", 100f, 5000f) { value = HectonChunkImpostorResidency.DefaultImpostorEnterDistanceMeters, showInputField = true };
            _hemisphereToggle = new Toggle("Hemisphere Only");
            _progressBar = new ProgressBar { title = "Idle", lowValue = 0f, highValue = 1f, value = 0f };
            _statusLabel = new Label("SHINOBU_212 forge ready.");

            Button loadCsvButton = new Button(() => LoadProfilesFromDialog()) { text = "Load impostor_generation_profiles.csv" };
            Button previewButton = new Button(RebuildPreview) { text = "Preview Capture Rig" };
            Button bakeButton = new Button(Bake) { text = "BAKE IMPOSTORS" };
            Button scanButton = new Button(() => HlodImpostorStaticValidators.ScanLodDistances(true)) { text = "Scan Unoptimized Horizons" };

            root.Add(_singlePrefabField);
            root.Add(_folderField);
            root.Add(_profilePopup);
            root.Add(_viewSlider);
            root.Add(_atlasSlider);
            root.Add(_dilationSlider);
            root.Add(_paddingSlider);
            root.Add(_swapDistanceSlider);
            root.Add(_hemisphereToggle);
            root.Add(loadCsvButton);
            root.Add(previewButton);
            root.Add(bakeButton);
            root.Add(scanButton);
            root.Add(_progressBar);
            root.Add(_statusLabel);

            _singlePrefabField.RegisterValueChangedCallback(evt =>
            {
                _previewTarget = evt.newValue as GameObject;
                RebuildPreview();
            });
            _profilePopup.RegisterValueChangedCallback(_ => ApplySelectedProfile());
            _viewSlider.RegisterValueChangedCallback(_ => RebuildPreview());
            _paddingSlider.RegisterValueChangedCallback(_ => RebuildPreview());
            _hemisphereToggle.RegisterValueChangedCallback(_ => RebuildPreview());
            ApplySelectedProfile();
        }

        private void LoadProfilesFromDialog()
        {
            string fullDefault = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), DefaultProfilePath));
            string selected = EditorUtility.OpenFilePanel("Load impostor_generation_profiles.csv", Path.GetDirectoryName(fullDefault), "csv");
            if (!string.IsNullOrEmpty(selected))
                LoadProfiles(FullPathToAssetOrProjectPath(selected));
        }

        private void LoadProfiles(string assetOrProjectPath)
        {
            _profileCount = 0;
            _profileNames.Clear();
            string fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetOrProjectPath));
            if (File.Exists(fullPath))
            {
                using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
                {
                    int length = (int)math.min(stream.Length, 64 * 1024);
                    if (length > 0)
                    {
                        NativeArray<byte> bytes = AllocateTrackedNativeArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory, nameof(bytes));
                        try
                        {
                            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
                            int read = stream.Read(new Span<byte>(ptr, length));
                            _profileCount = HlodImpostorProfileCsvParser.Parse(new ReadOnlySpan<byte>(ptr, read), _profiles);
                        }
                        finally
                        {
                            DisposeTrackedNativeArray(ref bytes);
                        }
                    }
                }
            }

            if (_profileCount == 0)
            {
                _profiles[0] = HlodImpostorProfileRecord.CreateDefault();
                _profileCount = 1;
            }

            for (int i = 0; i < _profileCount; i++)
                _profileNames.Add(_profiles[i].Name.ToString());

            if (_profilePopup != null)
            {
                _profilePopup.choices = _profileNames;
                _profilePopup.index = _profileNames.Count > 0 ? 0 : -1;
                ApplySelectedProfile();
            }
        }

        private void ApplySelectedProfile()
        {
            int index = _profilePopup != null ? math.clamp(_profilePopup.index, 0, math.max(0, _profileCount - 1)) : 0;
            if (_profileCount <= 0 || index >= _profileCount)
                return;

            HlodImpostorProfileRecord profile = _profiles[index];
            _viewSlider?.SetValueWithoutNotify(math.clamp(profile.ViewCount, 8, 32));
            _atlasSlider?.SetValueWithoutNotify(math.clamp(profile.AtlasResolution, 1024, 8192));
            _dilationSlider?.SetValueWithoutNotify(math.clamp(profile.DilationRadiusPixels, 0, 16));
            _paddingSlider?.SetValueWithoutNotify(math.clamp(profile.ExtraPaddingMeters, 0f, 8f));
            _swapDistanceSlider?.SetValueWithoutNotify(math.clamp(profile.RealGeometryDistanceMeters, 100f, 5000f));
            if (_hemisphereToggle != null)
                _hemisphereToggle.SetValueWithoutNotify(profile.HemisphereOnly != 0);
            RebuildPreview();
        }

        private void RebuildPreview()
        {
            _previewCount = 0;
            if (_previewTarget == null || _previewRecords == null)
                return;

            int viewCount = math.min(_viewSlider != null ? _viewSlider.value : HectonOctahedralImpostorData.ViewCount, _previewRecords.Length);
            NativeArray<HlodImpostorCaptureAngleRecord> records =
                AllocateTrackedNativeArray<HlodImpostorCaptureAngleRecord>(viewCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(records));
            try
            {
                if (HectonOctahedralImpostorBaker.TryBuildPreviewAngles(
                        _previewTarget,
                        viewCount,
                        (byte)(_hemisphereToggle != null && _hemisphereToggle.value ? 1 : 0),
                        records))
                {
                    for (int i = 0; i < viewCount; i++)
                        _previewRecords[i] = records[i];
                    _previewCount = viewCount;
                    SceneView.RepaintAll();
                }
            }
            finally
            {
                DisposeTrackedNativeArray(ref records);
            }
        }

        private static NativeArray<T> AllocateTrackedNativeArray<T>(int length, Allocator allocator, NativeArrayOptions options, string label) where T : struct
        {
            if (length <= 0)
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[HlodImpostorForgeWindow] NativeArray allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, ResolveNativeAllocationLifetime(allocator));
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[HlodImpostorForgeWindow] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static unsafe void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
        }

        private static NativeAllocationLifetime ResolveNativeAllocationLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return NativeAllocationLifetime.Temp;
                case Allocator.TempJob:
                    return NativeAllocationLifetime.TempJob;
                case Allocator.Persistent:
                    return NativeAllocationLifetime.Session;
                default:
                    return NativeAllocationLifetime.Session;
            }
        }

        private void Bake()
        {
            HlodImpostorBakeSettings settings = BuildSettings();
            _progressBar.value = 0f;
            _progressBar.title = "Queued";
            _statusLabel.text = "Bake queued.";

            DefaultAsset folder = _folderField != null ? _folderField.value as DefaultAsset : null;
            string folderPath = folder != null ? AssetDatabase.GetAssetPath(folder) : string.Empty;
            if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
            {
                int launched = HectonOctahedralImpostorBaker.BakePrefabFolder(folderPath, settings, UpdateProgress);
                _statusLabel.text = "Folder bake launched: " + launched.ToString(CultureInfo.InvariantCulture);
                return;
            }

            GameObject target = _singlePrefabField != null ? _singlePrefabField.value as GameObject : Selection.activeGameObject;
            if (target == null)
            {
                _statusLabel.text = "No prefab or folder selected.";
                return;
            }

            HectonOctahedralImpostorBaker.BakeGameObject(target, settings, UpdateProgress);
        }

        private HlodImpostorBakeSettings BuildSettings()
        {
            int profileIndex = _profilePopup != null ? math.clamp(_profilePopup.index, 0, math.max(0, _profileCount - 1)) : 0;
            FixedString64Bytes name = _profileCount > 0 ? _profiles[profileIndex].Name : new FixedString64Bytes("Massive_Wreck");
            return new HlodImpostorBakeSettings
            {
                ViewCount = _viewSlider != null ? _viewSlider.value : HectonOctahedralImpostorData.ViewCount,
                AtlasResolution = _atlasSlider != null ? _atlasSlider.value : HectonOctahedralImpostorData.DefaultAtlasSize,
                DilationRadiusPixels = _dilationSlider != null ? _dilationSlider.value : 4,
                ExtraPaddingMeters = _paddingSlider != null ? _paddingSlider.value : 0.75f,
                RealGeometryDistanceMeters = _swapDistanceSlider != null ? _swapDistanceSlider.value : HectonChunkImpostorResidency.DefaultImpostorEnterDistanceMeters,
                HemisphereOnly = (byte)(_hemisphereToggle != null && _hemisphereToggle.value ? 1 : 0),
                ProfileName = name
            };
        }

        private void UpdateProgress(string phase, float value)
        {
            if (_progressBar == null || _statusLabel == null)
                return;

            _progressBar.value = math.saturate(value);
            _progressBar.title = phase;
            _statusLabel.text = phase;
            Repaint();
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (_previewTarget == null || _previewCount <= 0 || _previewRecords == null)
                return;

            Handles.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            Vector3 center = _previewTarget.transform.position;
            for (int i = 0; i < _previewCount; i++)
            {
                HlodImpostorCaptureAngleRecord record = _previewRecords[i];
                Vector3 cameraPosition = new Vector3(record.CameraPosition.x, record.CameraPosition.y, record.CameraPosition.z);
                Handles.DrawLine(center, cameraPosition);
                Handles.SphereHandleCap(0, cameraPosition, Quaternion.identity, Mathf.Max(0.25f, record.OrthoSize * 0.025f), EventType.Repaint);
            }
        }

        private static string FullPathToAssetOrProjectPath(string fullPath)
        {
            string normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
            string project = Directory.GetCurrentDirectory().Replace('\\', '/');
            if (normalized.StartsWith(project, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(project.Length + 1);
            return normalized;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct HlodImpostorProfileRecord
    {
        [FieldOffset(0)]
        public FixedString64Bytes Name;

        [FieldOffset(64)]
        public int ViewCount;

        [FieldOffset(68)]
        public int AtlasResolution;

        [FieldOffset(72)]
        public int DilationRadiusPixels;

        [FieldOffset(76)]
        public float ExtraPaddingMeters;

        [FieldOffset(80)]
        public float RealGeometryDistanceMeters;

        [FieldOffset(84)]
        public byte HemisphereOnly;

        [FieldOffset(85)]
        private byte _pad0;

        [FieldOffset(86)]
        private ushort _pad1;

        [FieldOffset(88)]
        private ulong _pad2;

        public static HlodImpostorProfileRecord CreateDefault()
        {
            return new HlodImpostorProfileRecord
            {
                Name = new FixedString64Bytes("Massive_Wreck"),
                ViewCount = 16,
                AtlasResolution = 4096,
                DilationRadiusPixels = 4,
                ExtraPaddingMeters = 0.75f,
                RealGeometryDistanceMeters = 500f,
                HemisphereOnly = 0
            };
        }
    }

    public static class HlodImpostorProfileCsvParser
    {
        public static unsafe int Parse(ReadOnlySpan<byte> csv, NativeArray<HlodImpostorProfileRecord> output)
        {
            if (!output.IsCreated || output.Length == 0)
                return 0;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(output);
            return Parse(csv, new Span<HlodImpostorProfileRecord>(ptr, output.Length));
        }

        public static int Parse(ReadOnlySpan<byte> csv, Span<HlodImpostorProfileRecord> output)
        {
            if (output.Length == 0)
                return 0;

            int count = 0;
            int cursor = 0;
            while (cursor < csv.Length && count < output.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != '\n' && csv[cursor] != '\r')
                    cursor++;

                ReadOnlySpan<byte> line = Trim(csv.Slice(lineStart, cursor - lineStart));
                while (cursor < csv.Length && (csv[cursor] == '\n' || csv[cursor] == '\r'))
                    cursor++;

                if (line.Length == 0 || line[0] == (byte)'#' || IsHeader(line))
                    continue;

                if (TryParseLine(line, out HlodImpostorProfileRecord record))
                    output[count++] = record;
            }

            return count;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out HlodImpostorProfileRecord record)
        {
            record = HlodImpostorProfileRecord.CreateDefault();
            int cursor = 0;
            if (!TryReadCell(line, ref cursor, out ReadOnlySpan<byte> name) ||
                !TryReadCell(line, ref cursor, out ReadOnlySpan<byte> views) ||
                !TryReadCell(line, ref cursor, out ReadOnlySpan<byte> atlas) ||
                !TryReadCell(line, ref cursor, out ReadOnlySpan<byte> dilation) ||
                !TryReadCell(line, ref cursor, out ReadOnlySpan<byte> padding) ||
                !TryReadCell(line, ref cursor, out ReadOnlySpan<byte> distance))
            {
                return false;
            }

            ReadOnlySpan<byte> hemisphere = cursor <= line.Length
                ? Trim(line.Slice(cursor))
                : ReadOnlySpan<byte>.Empty;
            record.Name = ToFixedString(name);
            record.ViewCount = TryParseInt(views, out int viewCount) ? math.clamp(viewCount, 1, 64) : 16;
            record.AtlasResolution = TryParseInt(atlas, out int atlasResolution) ? math.clamp(atlasResolution, 512, 8192) : 4096;
            record.DilationRadiusPixels = TryParseInt(dilation, out int dilationRadius) ? math.clamp(dilationRadius, 0, 32) : 4;
            record.ExtraPaddingMeters = TryParseFloat(padding, out float pad) ? math.clamp(pad, 0f, 16f) : 0.75f;
            record.RealGeometryDistanceMeters = TryParseFloat(distance, out float swap) ? math.max(1f, swap) : 500f;
            record.HemisphereOnly = IsTrue(hemisphere) ? (byte)1 : (byte)0;
            return record.Name.Length > 0;
        }

        private static bool TryReadCell(ReadOnlySpan<byte> line, ref int cursor, out ReadOnlySpan<byte> cell)
        {
            if (cursor > line.Length)
            {
                cell = ReadOnlySpan<byte>.Empty;
                return false;
            }

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            cell = Trim(line.Slice(start, cursor - start));
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;
            return true;
        }

        private static bool TryParseInt(ReadOnlySpan<byte> bytes, out int value)
        {
            value = 0;
            bool any = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b < (byte)'0' || b > (byte)'9')
                    return false;
                any = true;
                value = value * 10 + (b - (byte)'0');
            }

            return any;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            float sign = 1f;
            float scale = 1f;
            bool fractional = false;
            bool any = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (i == 0 && b == (byte)'-')
                {
                    sign = -1f;
                    continue;
                }

                if (b == (byte)'.')
                {
                    fractional = true;
                    continue;
                }

                if (b < (byte)'0' || b > (byte)'9')
                    return false;

                any = true;
                int digit = b - (byte)'0';
                if (fractional)
                {
                    scale *= 0.1f;
                    value += digit * scale;
                }
                else
                {
                    value = value * 10f + digit;
                }
            }

            value *= sign;
            return any && math.isfinite(value);
        }

        private static FixedString64Bytes ToFixedString(ReadOnlySpan<byte> bytes)
        {
            FixedString64Bytes fixedString = default;
            int count = math.min(bytes.Length, FixedString64Bytes.UTF8MaxLengthInBytes);
            for (int i = 0; i < count; i++)
                fixedString.Add(bytes[i]);
            return fixedString;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length - 1;
            while (start <= end && bytes[start] <= 32)
                start++;
            while (end >= start && bytes[end] <= 32)
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : bytes.Slice(start, end - start + 1);
        }

        private static bool IsHeader(ReadOnlySpan<byte> line)
        {
            ReadOnlySpan<byte> trimmed = Trim(line);
            return StartsWith(trimmed, "name") || StartsWith(trimmed, "profile");
        }

        private static bool IsTrue(ReadOnlySpan<byte> bytes)
        {
            return StartsWith(bytes, "true") || StartsWith(bytes, "1") || StartsWith(bytes, "yes");
        }

        private static bool StartsWith(ReadOnlySpan<byte> bytes, string text)
        {
            if (bytes.Length < text.Length)
                return false;

            for (int i = 0; i < text.Length; i++)
            {
                byte a = bytes[i];
                byte b = (byte)text[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a = (byte)(a + 32);
                if (a != b)
                    return false;
            }

            return true;
        }
    }
}
#endif
