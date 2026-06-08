#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Hecton8.EditorTools
{
    public sealed class TextureChannelPackerWindow : EditorWindow
    {
        private const string WindowMenuPath = "Hecton8/Rendering/Texture Channel Packer";
        private const string ProfileCsvPath = "Assets/_Project/Data/TechArt/texture_packing_profiles.csv";
        private const string DefaultOutputFolder = "Assets/_Project/BakedGeometry/Textures";
        private const int PreviewSize = 256;
        private const string NativeMemoryOwner = nameof(TextureChannelPackerWindow);

        private readonly List<TexturePackingProfile> _profiles = new List<TexturePackingProfile>(8); // COLD ALLOC: List<TexturePackingProfile>[8] - editor profile cache - owner: TextureChannelPackerWindow
        private readonly List<string> _profileNames = new List<string>(8); // COLD ALLOC: List<string>[8] - editor popup labels - owner: TextureChannelPackerWindow
        private ObjectField _sourceFolderField;
        private ObjectField _previewTextureField;
        private Toggle _generateNormalsToggle;
        private Toggle _toksvigToggle;
        private Toggle _invertRoughnessToggle;
        private Slider _macroStrengthSlider;
        private FloatField _tileMetersField;
        private FloatField _macroSpanField;
        private Slider _qualityWeightSlider;
        private PopupField<string> _profilePopup;
        private ProgressBar _progressBar;
        private Label _statusLabel;
        private Image _aoPreview;
        private Image _roughnessPreview;
        private Image _metallicPreview;
        private Texture2D _aoPreviewTexture;
        private Texture2D _roughnessPreviewTexture;
        private Texture2D _metallicPreviewTexture;
        private SourceTextureSet[] _pendingSets;
        private int _pendingIndex;
        private bool _isPacking;
        private BatchAccumulator _batch;

        [MenuItem(WindowMenuPath, priority = 200)]
        private static void Open()
        {
            TextureChannelPackerWindow window = GetWindow<TextureChannelPackerWindow>("Texture Channel Packer");
            window.minSize = new Vector2(560f, 520f);
        }

        public void CreateGUI()
        {
            LoadProfiles();
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _sourceFolderField = new ObjectField("Source Folder")
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false
            };
            root.Add(_sourceFolderField);

            _profilePopup = new PopupField<string>("Profile", _profileNames, 0);
            _profilePopup.RegisterValueChangedCallback(_ => ApplySelectedProfile());
            root.Add(_profilePopup);

            _generateNormalsToggle = new Toggle("Generate Sobel Normals") { value = true };
            _toksvigToggle = new Toggle("Toksvig / VSM Mips") { value = true };
            _invertRoughnessToggle = new Toggle("Invert Roughness") { value = false };
            root.Add(_generateNormalsToggle);
            root.Add(_toksvigToggle);
            root.Add(_invertRoughnessToggle);

            _macroStrengthSlider = new Slider("Macro Noise Strength", 0f, 0.35f) { value = 0.12f };
            _tileMetersField = new FloatField("Material Tile Meters") { value = 10f };
            _macroSpanField = new FloatField("Macro Span Meters") { value = 240f };
            _qualityWeightSlider = new Slider("Global Quality Weight", 0f, 1f) { value = 0.55f };
            root.Add(_macroStrengthSlider);
            root.Add(_tileMetersField);
            root.Add(_macroSpanField);
            root.Add(_qualityWeightSlider);

            Button packButton = new Button(StartFolderPack) { text = "PACK ALL TEXTURES" };
            packButton.style.height = 42f;
            packButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(packButton);

            _progressBar = new ProgressBar { title = "Idle", lowValue = 0f, highValue = 1f, value = 0f };
            _statusLabel = new Label("No batch running.");
            root.Add(_progressBar);
            root.Add(_statusLabel);

            _previewTextureField = new ObjectField("Packed ARM Preview")
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false
            };
            root.Add(_previewTextureField);
            root.Add(new Button(RebuildPreview) { text = "Extract ARM Channels" });

            VisualElement previewRow = new VisualElement();
            previewRow.style.flexDirection = FlexDirection.Row;
            previewRow.style.marginTop = 8;
            _aoPreview = CreatePreviewImage("AO");
            _roughnessPreview = CreatePreviewImage("Roughness");
            _metallicPreview = CreatePreviewImage("Metallic");
            previewRow.Add(_aoPreview);
            previewRow.Add(_roughnessPreview);
            previewRow.Add(_metallicPreview);
            root.Add(previewRow);

            ApplySelectedProfile();
        }

        private void OnDisable()
        {
            StopPacking();
            DestroyPreviewTextures();
        }

        private static Image CreatePreviewImage(string tooltip)
        {
            Image image = new Image { tooltip = tooltip, scaleMode = ScaleMode.ScaleToFit };
            image.style.width = 170;
            image.style.height = 170;
            image.style.marginRight = 8;
            image.style.backgroundColor = new Color(0.06f, 0.06f, 0.06f, 1f);
            return image;
        }

        private void LoadProfiles()
        {
            _profiles.Clear();
            _profileNames.Clear();
            TexturePackingProfileCsv.Load(ProfileCsvPath, _profiles);
            for (int i = 0; i < _profiles.Count; i++)
                _profileNames.Add(_profiles[i].Name.ToString());
        }

        private void ApplySelectedProfile()
        {
            int index = _profilePopup != null ? math.clamp(_profilePopup.index, 0, math.max(0, _profiles.Count - 1)) : 0;
            TexturePackingProfile profile = _profiles.Count > 0 ? _profiles[index] : TexturePackingProfileCsv.DefaultProfile();
            _macroStrengthSlider.value = math.saturate(profile.MacroNoiseStrength);
            _tileMetersField.value = math.max(0.001f, profile.TileSizeMeters);
            _macroSpanField.value = math.max(1f, profile.MacroWorldSpanMeters);
            _qualityWeightSlider.value = math.saturate(profile.GlobalQualityWeight);
            _generateNormalsToggle.value = (profile.Flags & HectonArmTextureChannelPacker.FlagGenerateNormals) != 0u;
            _toksvigToggle.value = (profile.Flags & HectonArmTextureChannelPacker.FlagToksvigMipFiltering) != 0u;
            _invertRoughnessToggle.value = (profile.Flags & HectonArmTextureChannelPacker.FlagInvertRoughness) != 0u;
        }

        private void StartFolderPack()
        {
            if (_isPacking)
                return;

            DefaultAsset folder = _sourceFolderField.value as DefaultAsset;
            string folderPath = folder != null ? AssetDatabase.GetAssetPath(folder) : string.Empty;
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                _statusLabel.text = "Select a valid source folder.";
                return;
            }

            _pendingSets = DiscoverSourceSets(folderPath);
            _pendingIndex = 0;
            _batch = default;
            if (_pendingSets.Length == 0)
            {
                _statusLabel.text = "No AO/Roughness/Metallic source sets found.";
                return;
            }

            _isPacking = true;
            _progressBar.value = 0f;
            _progressBar.title = "Packing 0 / " + _pendingSets.Length.ToString(CultureInfo.InvariantCulture);
            _statusLabel.text = "Batch queued.";
            EditorApplication.update += TickPackingQueue;
        }

        private void StopPacking()
        {
            if (!_isPacking)
                return;

            EditorApplication.update -= TickPackingQueue;
            _isPacking = false;
            EditorUtility.ClearProgressBar();
        }

        private void TickPackingQueue()
        {
            if (!_isPacking || _pendingSets == null)
                return;

            if (_pendingIndex >= _pendingSets.Length)
            {
                WriteBatchReport();
                _statusLabel.text = "Batch complete. Processed=" + _batch.Processed.ToString(CultureInfo.InvariantCulture);
                _progressBar.value = 1f;
                _progressBar.title = "Complete";
                StopPacking();
                return;
            }

            SourceTextureSet set = _pendingSets[_pendingIndex];
            float progress = _pendingSets.Length > 0 ? _pendingIndex / (float)_pendingSets.Length : 1f;
            _progressBar.value = progress;
            _progressBar.title = "Packing " + _pendingIndex.ToString(CultureInfo.InvariantCulture) + " / " + _pendingSets.Length.ToString(CultureInfo.InvariantCulture);
            EditorUtility.DisplayProgressBar("Texture Channel Packer", set.Key, progress);

            TexturePackerRequest request = BuildRequest(set);
            try
            {
                if (HectonArmTextureChannelPacker.TryPackArmAsset(request, out TexturePackerRunMetrics metrics))
                {
                    _batch.Processed++;
                    _batch.EstimatedBeforeBytes += metrics.EstimatedBeforeBytes;
                    _batch.EstimatedAfterBytes += metrics.EstimatedAfterBytes;
                    _batch.EstimatedSavedBytes += metrics.EstimatedSavedBytes;
                    _batch.TotalMilliseconds += metrics.TotalMilliseconds;
                    if (!string.IsNullOrEmpty(metrics.CriticalWarning))
                        _batch.CriticalWarnings++;
                }
                else
                {
                    _batch.Failed++;
                }
            }
            catch (Exception exception)
            {
                _batch.Failed++;
                Debug.LogError("[TextureChannelPackerWindow] Pack failed for " + set.Key + ": " + exception.GetType().Name);
            }

            _pendingIndex++;
            Repaint();
        }

        private TexturePackerRequest BuildRequest(SourceTextureSet set)
        {
            TexturePackingProfile profile = _profiles.Count > 0 ? _profiles[math.clamp(_profilePopup.index, 0, _profiles.Count - 1)] : TexturePackingProfileCsv.DefaultProfile();
            uint flags = profile.Flags;
            flags = SetFlag(flags, HectonArmTextureChannelPacker.FlagInvertRoughness, _invertRoughnessToggle.value);
            flags = SetFlag(flags, HectonArmTextureChannelPacker.FlagToksvigMipFiltering, _toksvigToggle.value);
            flags = SetFlag(flags, HectonArmTextureChannelPacker.FlagGenerateNormals, _generateNormalsToggle.value);

            TexturePackerConfigDTO config;
            config.NormalIntensity = math.max(0.001f, profile.NormalIntensity);
            config.RoughnessScale = math.max(0f, profile.RoughnessScale);
            config.MetallicScale = math.max(0f, profile.MetallicScale);
            config.Flags = flags;

            return new TexturePackerRequest
            {
                AoTexture = set.Ao,
                RoughnessTexture = set.Roughness,
                MetallicTexture = set.Metallic,
                AlbedoTexture = set.Albedo,
                OutputName = set.Key,
                OutputFolder = DefaultOutputFolder,
                Config = config,
                MaxSize = 2048,
                MacroNoiseStrength = _macroStrengthSlider.value,
                TileSizeMeters = math.max(0.001f, _tileMetersField.value),
                MacroWorldSpanMeters = math.max(1f, _macroSpanField.value),
                GlobalQualityWeight = math.saturate(_qualityWeightSlider.value),
                Seed = HashName(set.Key)
            };
        }

        private static uint SetFlag(uint flags, uint flag, bool enabled)
        {
            return enabled ? flags | flag : flags & ~flag;
        }

        private static SourceTextureSet[] DiscoverSourceSets(string folderPath)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            Dictionary<string, SourceTextureSet> sets = new Dictionary<string, SourceTextureSet>(guids.Length); // COLD ALLOC: Dictionary<string, SourceTextureSet>[textureGuidCount] - editor source grouping - owner: TextureChannelPackerWindow
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                    continue;

                string key = BuildSetKey(Path.GetFileNameWithoutExtension(path));
                if (!sets.TryGetValue(key, out SourceTextureSet set))
                {
                    set.Key = key;
                    sets.Add(key, set);
                }

                AssignTextureByToken(path, texture, ref set);
                sets[key] = set;
            }

            SourceTextureSet[] output = new SourceTextureSet[sets.Count];
            int count = 0;
            Dictionary<string, SourceTextureSet>.Enumerator e = sets.GetEnumerator();
            while (e.MoveNext())
            {
                SourceTextureSet set = e.Current.Value;
                if (set.Ao != null || set.Roughness != null || set.Metallic != null)
                    output[count++] = set;
            }

            if (count == output.Length)
                return output;

            SourceTextureSet[] compact = new SourceTextureSet[count];
            for (int i = 0; i < count; i++)
                compact[i] = output[i];
            return compact;
        }

        private static void AssignTextureByToken(string path, Texture2D texture, ref SourceTextureSet set)
        {
            string lower = path.Replace('\\', '/').ToLowerInvariant();
            if (ContainsOrdinal(lower, "_ao") || ContainsOrdinal(lower, "ambient") || ContainsOrdinal(lower, "occlusion"))
                set.Ao = texture;
            else if (ContainsOrdinal(lower, "rough"))
                set.Roughness = texture;
            else if (ContainsOrdinal(lower, "metal"))
                set.Metallic = texture;
            else if (ContainsOrdinal(lower, "albedo") ||
                     ContainsOrdinal(lower, "basecolor") ||
                     ContainsOrdinal(lower, "base_color") ||
                     ContainsOrdinal(lower, "diffuse"))
                set.Albedo = texture;
        }

        private static bool ContainsOrdinal(string source, string token)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(token, StringComparison.Ordinal) >= 0;
        }

        private static string BuildSetKey(string name)
        {
            string value = name;
            value = RemoveToken(value, "_AmbientOcclusion");
            value = RemoveToken(value, "_Occlusion");
            value = RemoveToken(value, "_Roughness");
            value = RemoveToken(value, "_Metallic");
            value = RemoveToken(value, "_Albedo");
            value = RemoveToken(value, "_BaseColor");
            value = RemoveToken(value, "_Base_Color");
            value = RemoveToken(value, "_Diffuse");
            value = RemoveToken(value, "_AO");
            return string.IsNullOrEmpty(value) ? name : value;
        }

        private static string RemoveToken(string value, string token)
        {
            int index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? value.Remove(index, token.Length) : value;
        }

        private static uint HashName(string value)
        {
            uint hash = 2166136261u;
            if (value == null)
                return hash;

            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return hash;
        }

        private void RebuildPreview()
        {
            Texture2D source = _previewTextureField.value as Texture2D;
            if (source == null)
            {
                _statusLabel.text = "Select a packed ARM texture for preview.";
                return;
            }

            DestroyPreviewTextures();
            Texture2D snapshot = null;
            NativeArray<Color32> ao = default;
            NativeArray<Color32> roughness = default;
            NativeArray<Color32> metallic = default;

            try
            {
                snapshot = CaptureReadableTexture(source, PreviewSize, PreviewSize);
                NativeArray<Color32> sourcePixels = snapshot.GetRawTextureData<Color32>();
                ao = TexturePackerEditorNativeMemory.AllocateArray<Color32>(sourcePixels.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, NativeMemoryOwner, nameof(ao)); // COLD ALLOC: NativeArray<Color32>[preview] - editor AO preview - owner: TextureChannelPackerWindow
                roughness = TexturePackerEditorNativeMemory.AllocateArray<Color32>(sourcePixels.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, NativeMemoryOwner, nameof(roughness)); // COLD ALLOC: NativeArray<Color32>[preview] - editor roughness preview - owner: TextureChannelPackerWindow
                metallic = TexturePackerEditorNativeMemory.AllocateArray<Color32>(sourcePixels.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, NativeMemoryOwner, nameof(metallic)); // COLD ALLOC: NativeArray<Color32>[preview] - editor metallic preview - owner: TextureChannelPackerWindow

                new HectonArmTextureChannelPacker.ExtractArmPreviewJob
                {
                    Source = sourcePixels,
                    Ao = ao,
                    Roughness = roughness,
                    Metallic = metallic
                // Editor preview boundary: UI Toolkit Image consumes the extracted channel textures immediately.
                }.Schedule(sourcePixels.Length, 128).Complete();

                _aoPreviewTexture = BuildPreviewTexture("TX_PREVIEW_ARM_AO", ao);
                _roughnessPreviewTexture = BuildPreviewTexture("TX_PREVIEW_ARM_ROUGHNESS", roughness);
                _metallicPreviewTexture = BuildPreviewTexture("TX_PREVIEW_ARM_METALLIC", metallic);
                _aoPreview.image = _aoPreviewTexture;
                _roughnessPreview.image = _roughnessPreviewTexture;
                _metallicPreview.image = _metallicPreviewTexture;
                _statusLabel.text = "Preview channels extracted: R=AO, G=Roughness, B=Metallic.";
            }
            finally
            {
                if (snapshot != null)
                    Object.DestroyImmediate(snapshot);
                TexturePackerEditorNativeMemory.DisposeArray(ref ao);
                TexturePackerEditorNativeMemory.DisposeArray(ref roughness);
                TexturePackerEditorNativeMemory.DisposeArray(ref metallic);
            }
        }

        private static Texture2D BuildPreviewTexture(string name, NativeArray<Color32> pixels)
        {
            Texture2D texture = new Texture2D(PreviewSize, PreviewSize, TextureFormat.RGBA32, false, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CaptureReadableTexture(Texture texture, int width, int height)
        {
            RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            bool returned = false;

            try
            {
                UnityEngine.Graphics.Blit(texture, temp);
                RenderTexture.active = temp;
                readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readable.Apply(false, false);
                returned = true;
                return readable;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temp);
                if (!returned)
                    Object.DestroyImmediate(readable);
            }
        }

        private void DestroyPreviewTextures()
        {
            DestroyPreview(ref _aoPreviewTexture);
            DestroyPreview(ref _roughnessPreviewTexture);
            DestroyPreview(ref _metallicPreviewTexture);
        }

        private static void DestroyPreview(ref Texture2D texture)
        {
            if (texture == null)
                return;

            Object.DestroyImmediate(texture);
            texture = null;
        }

        private void WriteBatchReport()
        {
            Directory.CreateDirectory("Docs/Reports");
            StringBuilder builder = new StringBuilder(1024); // COLD ALLOC: StringBuilder[1024] - editor batch packing report - owner: TextureChannelPackerWindow
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.texture_packing_batch_report.v1", true);
            AppendJson(builder, "processedTextures", _batch.Processed, true);
            AppendJson(builder, "failedTextures", _batch.Failed, true);
            AppendJson(builder, "estimatedBeforeBytes", _batch.EstimatedBeforeBytes, true);
            AppendJson(builder, "estimatedAfterBytes", _batch.EstimatedAfterBytes, true);
            AppendJson(builder, "estimatedSavedBytes", _batch.EstimatedSavedBytes, true);
            AppendJson(builder, "compression", "BC7", true);
            AppendJson(builder, "totalMilliseconds", _batch.TotalMilliseconds, true);
            AppendJson(builder, "criticalWarnings", _batch.CriticalWarnings, true);
            AppendJson(builder, "blackboxDumpPath", TexturePackerBlackBox.DumpPath, true);
            AppendJson(builder, "blackboxEntryBytes", UnsafeUtility.SizeOf<TexturePackerTelemetryEntry>(), true);
            AppendJson(builder, "blackboxRingLength", TexturePackerBlackBox.RingCapacity, false);
            builder.Append("}\n");
            File.WriteAllText("Docs/Reports/TEXTURE_PACKING_REPORT.json", builder.ToString(), Encoding.UTF8);
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": \"");
            builder.Append(value);
            builder.Append('"');
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendJson(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendJson(StringBuilder builder, string name, long value, bool comma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendJson(StringBuilder builder, string name, double value, bool comma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private struct SourceTextureSet
        {
            public string Key;
            public Texture2D Ao;
            public Texture2D Roughness;
            public Texture2D Metallic;
            public Texture2D Albedo;
        }

        private struct BatchAccumulator
        {
            public int Processed;
            public int Failed;
            public int CriticalWarnings;
            public long EstimatedBeforeBytes;
            public long EstimatedAfterBytes;
            public long EstimatedSavedBytes;
            public double TotalMilliseconds;
        }
    }

    internal static class TexturePackerEditorNativeMemory
    {
        internal static NativeArray<T> AllocateArray<T>(int length, Allocator allocator, NativeArrayOptions options, string owner, string label) where T : struct
        {
            if (length <= 0)
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[TexturePackerEditorNativeMemory] NativeArray allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, owner, label, ResolveNativeAllocationLifetime(allocator));
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[TexturePackerEditorNativeMemory] NativeMemorySentinel rejected NativeArray registration for " + owner + "." + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        internal static unsafe void DisposeArray<T>(ref NativeArray<T> array) where T : struct
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
    }

    internal static unsafe class TexturePackingProfileCsv
    {
        private const uint HashNone = 0xADA7AFDBu;
        private const uint HashOff = 0xAB3A8A0Au;
        private const uint HashFalse = 0x0B069958u;
        private const uint HashZero = 0x350CA8AFu;
        private const uint HashMacro = 0x36A3CAD3u;
        private const uint HashNoise = 0x904416D1u;
        private const uint HashM = 0xE80C2F78u;
        private const uint HashToksvig = 0x04F6BFE0u;
        private const uint HashMip = 0xCF8F4EC9u;
        private const uint HashT = 0xF10C3DA3u;
        private const uint HashNormal = 0xE68B9C52u;
        private const uint HashNormals = 0x0EC6C7F3u;
        private const uint HashSobel = 0x1FBA0C56u;
        private const uint HashN = 0xEB0C3431u;
        private const uint HashInvert = 0x316C9FA1u;
        private const uint HashSmoothness = 0xA29A2330u;
        private const uint HashI = 0xEC0C35C4u;
        private const string NativeMemoryOwner = nameof(TexturePackingProfileCsv);

        internal static void Load(string assetPath, List<TexturePackingProfile> profiles)
        {
            profiles.Clear();
            string root = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
            string fullPath = Path.Combine(root, assetPath);
            if (!File.Exists(fullPath))
            {
                profiles.Add(DefaultProfile());
                profiles.Add(TerrainProfile());
                return;
            }

            NativeArray<byte> bytes = default;
            try
            {
                using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long length64 = stream.Length;
                    if (length64 <= 0L || length64 > int.MaxValue)
                    {
                        profiles.Add(DefaultProfile());
                        return;
                    }

                    int length = (int)length64;
                    bytes = TexturePackerEditorNativeMemory.AllocateArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory, NativeMemoryOwner, nameof(bytes)); // COLD ALLOC: NativeArray<byte>[csv bytes] - editor profile CSV buffer - owner: TexturePackingProfileCsv
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
                    int read = 0;
                    while (read < length)
                    {
                        int value = stream.ReadByte();
                        if (value < 0)
                            break;
                        ptr[read++] = (byte)value;
                    }

                    int cursor = 0;
                    SkipLine(ptr, read, ref cursor);
                    while (cursor < read)
                    {
                        if (TryParseProfile(ptr, read, ref cursor, out TexturePackingProfile profile))
                            profiles.Add(profile);
                    }
                }
            }
            finally
            {
                TexturePackerEditorNativeMemory.DisposeArray(ref bytes);
            }

            if (profiles.Count == 0)
                profiles.Add(DefaultProfile());
        }

        internal static TexturePackingProfile DefaultProfile()
        {
            TexturePackingProfile profile = default;
            profile.Name = new FixedString64Bytes("HardSurface_Default");
            profile.NormalIntensity = 1.0f;
            profile.RoughnessScale = 1.0f;
            profile.MetallicScale = 1.0f;
            profile.MacroNoiseStrength = 0.08f;
            profile.TileSizeMeters = 4f;
            profile.MacroWorldSpanMeters = 120f;
            profile.GlobalQualityWeight = 0.55f;
            profile.Flags = HectonArmTextureChannelPacker.FlagInjectMacroNoise |
                            HectonArmTextureChannelPacker.FlagToksvigMipFiltering |
                            HectonArmTextureChannelPacker.FlagGenerateNormals;
            return profile;
        }

        private static TexturePackingProfile TerrainProfile()
        {
            TexturePackingProfile profile = DefaultProfile();
            profile.Name = new FixedString64Bytes("Terrain_100km_Macro");
            profile.MacroNoiseStrength = 0.18f;
            profile.TileSizeMeters = 10f;
            profile.MacroWorldSpanMeters = 320f;
            profile.GlobalQualityWeight = 0.7f;
            return profile;
        }

        private static bool TryParseProfile(byte* bytes, int length, ref int cursor, out TexturePackingProfile profile)
        {
            profile = default;
            SkipBlank(bytes, length, ref cursor);
            if (cursor >= length)
                return false;

            profile.Name = ParseFixedStringColumn(bytes, length, ref cursor);
            profile.NormalIntensity = SafePositive(ParseFloatColumn(bytes, length, ref cursor, 1f), 1f);
            profile.RoughnessScale = math.max(0f, ParseFloatColumn(bytes, length, ref cursor, 1f));
            profile.MetallicScale = math.max(0f, ParseFloatColumn(bytes, length, ref cursor, 1f));
            profile.MacroNoiseStrength = math.saturate(ParseFloatColumn(bytes, length, ref cursor, 0.08f));
            profile.TileSizeMeters = SafePositive(ParseFloatColumn(bytes, length, ref cursor, 4f), 4f);
            profile.MacroWorldSpanMeters = SafePositive(ParseFloatColumn(bytes, length, ref cursor, 120f), 120f);
            profile.GlobalQualityWeight = math.saturate(ParseFloatColumn(bytes, length, ref cursor, 0.55f));
            profile.Flags = ParseFlagsColumn(bytes, length, ref cursor);
            SkipLine(bytes, length, ref cursor);
            if (profile.Name.Length == 0)
                profile.Name = new FixedString64Bytes("Unnamed_Texture_Profile");
            return true;
        }

        private static FixedString64Bytes ParseFixedStringColumn(byte* bytes, int length, ref int cursor)
        {
            FixedString64Bytes value = default;
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == ',' || b == '\n' || b == '\r')
                    break;
                if (value.Length < FixedString64Bytes.UTF8MaxLengthInBytes)
                    value.Add(b);
            }

            ConsumeLineBreakRemainder(bytes, length, ref cursor);
            return value;
        }

        private static float ParseFloatColumn(byte* bytes, int length, ref int cursor, float fallback)
        {
            SkipColumnWhitespace(bytes, length, ref cursor);
            bool negative = false;
            if (cursor < length && bytes[cursor] == '-')
            {
                negative = true;
                cursor++;
            }

            double value = 0d;
            bool hasDigit = false;
            while (cursor < length)
            {
                byte b = bytes[cursor];
                if (b < '0' || b > '9')
                    break;
                hasDigit = true;
                value = value * 10d + (b - '0');
                cursor++;
            }

            if (cursor < length && bytes[cursor] == '.')
            {
                cursor++;
                double scale = 0.1d;
                while (cursor < length)
                {
                    byte b = bytes[cursor];
                    if (b < '0' || b > '9')
                        break;
                    hasDigit = true;
                    value += (b - '0') * scale;
                    scale *= 0.1d;
                    cursor++;
                }
            }

            SkipToNextColumn(bytes, length, ref cursor);
            if (!hasDigit)
                return fallback;

            float result = (float)(negative ? -value : value);
            return math.isfinite(result) ? result : fallback;
        }

        private static uint ParseFlagsColumn(byte* bytes, int length, ref int cursor)
        {
            uint flags = 0u;
            uint tokenHash = 2166136261u;
            bool hasToken = false;
            bool hasExplicitToken = false;
            bool explicitOff = false;
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == ',' || b == '\n' || b == '\r')
                    break;
                if (b >= 'A' && b <= 'Z')
                    b = (byte)(b + 32);
                if ((b >= 'a' && b <= 'z') || (b >= '0' && b <= '9'))
                {
                    hasToken = true;
                    tokenHash ^= b;
                    tokenHash *= 16777619u;
                    continue;
                }

                CommitFlagToken(tokenHash, hasToken, ref flags, ref hasExplicitToken, ref explicitOff);
                tokenHash = 2166136261u;
                hasToken = false;
            }

            CommitFlagToken(tokenHash, hasToken, ref flags, ref hasExplicitToken, ref explicitOff);
            ConsumeLineBreakRemainder(bytes, length, ref cursor);
            if (explicitOff)
                return 0u;
            if (!hasExplicitToken)
                return HectonArmTextureChannelPacker.FlagInjectMacroNoise |
                       HectonArmTextureChannelPacker.FlagToksvigMipFiltering |
                       HectonArmTextureChannelPacker.FlagGenerateNormals;
            return flags;
        }

        private static void CommitFlagToken(uint tokenHash, bool hasToken, ref uint flags, ref bool hasExplicitToken, ref bool explicitOff)
        {
            if (!hasToken)
                return;

            hasExplicitToken = true;
            if (tokenHash == HashNone || tokenHash == HashOff || tokenHash == HashFalse || tokenHash == HashZero)
            {
                explicitOff = true;
                flags = 0u;
                return;
            }

            if (tokenHash == HashMacro || tokenHash == HashNoise || tokenHash == HashM)
                flags |= HectonArmTextureChannelPacker.FlagInjectMacroNoise;
            else if (tokenHash == HashToksvig || tokenHash == HashMip || tokenHash == HashT)
                flags |= HectonArmTextureChannelPacker.FlagToksvigMipFiltering;
            else if (tokenHash == HashNormal || tokenHash == HashNormals || tokenHash == HashSobel || tokenHash == HashN)
                flags |= HectonArmTextureChannelPacker.FlagGenerateNormals;
            else if (tokenHash == HashInvert || tokenHash == HashSmoothness || tokenHash == HashI)
                flags |= HectonArmTextureChannelPacker.FlagInvertRoughness;
        }

        private static float SafePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static void SkipColumnWhitespace(byte* bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == ' ' || bytes[cursor] == '\t'))
                cursor++;
        }

        private static void SkipToNextColumn(byte* bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == ',' || b == '\n')
                    return;
                if (b == '\r')
                {
                    ConsumeLineBreakRemainder(bytes, length, ref cursor);
                    return;
                }
            }
        }

        private static void SkipLine(byte* bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == '\n')
                    break;
            }
        }

        private static void SkipBlank(byte* bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == '\n' || bytes[cursor] == '\r'))
                cursor++;
        }

        private static void ConsumeLineBreakRemainder(byte* bytes, int length, ref int cursor)
        {
            if (cursor > 0 && cursor < length && bytes[cursor - 1] == '\r' && bytes[cursor] == '\n')
                cursor++;
        }
    }
}
#endif
