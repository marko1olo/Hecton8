using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton8.World.StaticCaveSdfBaker;
using Hecton8.World.StaticCaveSdfBaker.Editor;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Bakers
{
    /// <summary>
    /// Editor-only Texture3D baker for abyssal fog density/flow. Static SDF work is delegated to StaticCaveSdfBakePipeline.
    /// </summary>
    public sealed class VolumetricTextureBaker : EditorWindow
    {
        public const string DefaultOutputFolder = "Assets/_Project/Art/Textures/Volumes";

        private const string MenuRoot = "Hecton8/Bakers/1720/";
        private const int MinimumResolution = 32;
        private const int MaximumResolution = 128;
        private const int ResolutionStep = 16;
        private const int JobBatchSize = 64;
        private const int FogDefaultOctaves = 4;
        private const uint WarningValidationRangeWeak = 1u << 0;
        private const uint WarningTextureFormatRejected = 1u << 1;

        private string _assetName = "abyss_default";
        private string _outputFolder = DefaultOutputFolder;
        private MeshFilter _sdfTarget;
        private float _globalQualityWeight = 0.75f;
        private int _requestedResolution = 96;
        private int _fogOctaves = FogDefaultOctaves;
        private int _sdfSubMeshIndex = -1;
        private float _fogDensityScale = 1.15f;
        private float _flowStrength = 0.82f;
        private float _sdfMaxDistanceMeters = 12f;
        private string _lastStatus = "Idle.";

        [MenuItem(MenuRoot + "Open Volumetric Texture Baker", false, 1720)]
        private static void Open()
        {
            VolumetricTextureBaker window = GetWindow<VolumetricTextureBaker>();
            window.titleContent = new GUIContent("Volume Baker 1720");
            window.minSize = new Vector2(500f, 390f);
        }

        [MenuItem(MenuRoot + "Bake Default Fog Density Flow Volume", false, 1721)]
        private static void BakeDefaultFogMenu()
        {
            BakeSettings settings = BakeSettings.DefaultFog();
            if (TryBakeFogDensityFlow(settings, out BakeResult result))
                Debug.Log("[VolumetricTextureBaker] Fog/flow volume baked: " + result.AssetPath);
        }

        [MenuItem(MenuRoot + "Bake Selected Mesh SDF With Static Forge", false, 1722)]
        private static void BakeSelectedMeshSdfMenu()
        {
            MeshFilter meshFilter = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<MeshFilter>()
                : null;
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError("[VolumetricTextureBaker] Select a GameObject with a MeshFilter before baking SDF.");
                return;
            }

            BakeSettings settings = BakeSettings.DefaultSdf(meshFilter);
            if (TryBakeSdfWithStaticForge(settings, out StaticCaveSdfBakeResult result))
                Debug.Log("[VolumetricTextureBaker] Static Forge encoded SDF Texture3D: " + result.TextureAssetPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Offline 3D Volume Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _outputFolder = EditorGUILayout.TextField("Fog Output Folder", _outputFolder);
            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);
            _requestedResolution = EditorGUILayout.IntSlider("Resolution", _requestedResolution, MinimumResolution, MaximumResolution);
            _fogOctaves = EditorGUILayout.IntSlider("Fog Octaves", _fogOctaves, 1, 5);
            _fogDensityScale = EditorGUILayout.Slider("Fog Density Scale", _fogDensityScale, 0.1f, 3f);
            _flowStrength = EditorGUILayout.Slider("Flow Strength", _flowStrength, 0f, 2f);

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Bake Fog Density + Flow Texture3D"))
                BakeFogFromWindow();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("SDF uses Static Cave SDF Forge", EditorStyles.boldLabel);
            _sdfTarget = (MeshFilter)EditorGUILayout.ObjectField("SDF MeshFilter", _sdfTarget, typeof(MeshFilter), true);
            _sdfSubMeshIndex = EditorGUILayout.IntField("SubMesh Index (-1 = all)", _sdfSubMeshIndex);
            _sdfMaxDistanceMeters = EditorGUILayout.Slider("SDF Max Distance", _sdfMaxDistanceMeters, 0.25f, 80f);
            if (GUILayout.Button("Bake Compatible Cave SDF Texture3D"))
                BakeSdfFromWindow();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
        }

        private void BakeFogFromWindow()
        {
            BakeSettings settings = new BakeSettings(
                _assetName,
                _outputFolder,
                null,
                _globalQualityWeight,
                _requestedResolution,
                _fogOctaves,
                _sdfSubMeshIndex,
                _fogDensityScale,
                _flowStrength,
                _sdfMaxDistanceMeters);
            _lastStatus = TryBakeFogDensityFlow(settings, out BakeResult result)
                ? result.AssetPath
                : "Fog bake failed; see Console.";
        }

        private void BakeSdfFromWindow()
        {
            BakeSettings settings = new BakeSettings(
                _assetName,
                _outputFolder,
                _sdfTarget,
                _globalQualityWeight,
                _requestedResolution,
                _fogOctaves,
                _sdfSubMeshIndex,
                _fogDensityScale,
                _flowStrength,
                _sdfMaxDistanceMeters);
            _lastStatus = TryBakeSdfWithStaticForge(settings, out StaticCaveSdfBakeResult result)
                ? result.TextureAssetPath
                : "SDF bake failed; see Console.";
        }

        public static bool TryBakeFogDensityFlow(BakeSettings settings, out BakeResult result)
        {
            Stopwatch total = Stopwatch.StartNew();
            result = BakeResult.Empty(0u);
            NativeArray<Color32> voxels = default;

            try
            {
                ValidateStructLayoutsOrThrow();
                BakeSettings sanitized = settings.Sanitize();
                int3 resolution = new int3(sanitized.Resolution);
                int voxelCount = ResolveVoxelCountOrThrow(resolution);
                if (!ProceduralTextureBaker.TryEnsureAssetFolder(sanitized.OutputFolder, out string outputFolder, out string folderFailure))
                {
                    Debug.LogError("[VolumetricTextureBaker] " + folderFailure);
                    return false;
                }

                voxels = new NativeArray<Color32>(voxelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                EditorUtility.DisplayProgressBar("Volume Baker 1720", "Baking periodic fog density and flow", 0.42f);
                Stopwatch generation = Stopwatch.StartNew();
                JobHandle handle = new FogVolumeBakeJob
                {
                    Voxels = voxels,
                    Config = new FogBakeConfig1720
                    {
                        Resolution = resolution,
                        Octaves = sanitized.FogOctaves,
                        GlobalQualityWeight = sanitized.GlobalQualityWeight,
                        DensityScale = sanitized.FogDensityScale,
                        FlowStrength = sanitized.FlowStrength,
                        Seed = StableHash32(sanitized.AssetName)
                    }
                }.Schedule(voxelCount, JobBatchSize);
                // [EDITOR_BLOCKING_SYNC_POINT] Offline bake must own the immutable Texture3D payload before AssetDatabase serialization.
                handle.Complete();
                generation.Stop();

                VolumeValidation validation = ValidateColorVolume(voxels, resolution);
                uint warnings = validation.WarningFlags;
                string safeName = ProceduralTextureBaker.SanitizeAssetNameForPath(sanitized.AssetName);
                if (string.IsNullOrEmpty(safeName))
                    safeName = "VolumetricFog";
                string assetPath = CreateTexture3DAsset(safeName, outputFolder, voxels, resolution, ref warnings, out string actualFormat, out string compressionNote);
                if (!ProceduralTextureBaker.TryFinalizeAssetDatabase("1720 volumetric fog texture bake", out string finalizeFailure))
                {
                    Debug.LogError("[VolumetricTextureBaker] " + finalizeFailure);
                    return false;
                }

                total.Stop();
                result = new BakeResult(
                    sanitized.AssetName,
                    assetPath,
                    resolution,
                    voxelCount,
                    sanitized.GlobalQualityWeight,
                    validation.MinR,
                    validation.MaxR,
                    actualFormat,
                    compressionNote,
                    generation.Elapsed.TotalMilliseconds,
                    total.Elapsed.TotalMilliseconds,
                    warnings);
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is UnityException || ex is UnauthorizedAccessException)
            {
                Debug.LogError("[VolumetricTextureBaker] Fog bake failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
            finally
            {
                if (voxels.IsCreated)
                    voxels.Dispose();
                EditorUtility.ClearProgressBar();
            }
        }

        public static bool TryBakeSdfWithStaticForge(BakeSettings settings, out StaticCaveSdfBakeResult result)
        {
            result = default;
            try
            {
                BakeSettings sanitized = settings.Sanitize();
                if (sanitized.SdfMeshFilter == null || sanitized.SdfMeshFilter.sharedMesh == null)
                {
                    Debug.LogError("[VolumetricTextureBaker] SDF bake requires MeshFilter.sharedMesh.");
                    return false;
                }

                int resolution = sanitized.Resolution;
                long voxelCount = (long)resolution * resolution * resolution;
                if (voxelCount <= 0L || voxelCount > int.MaxValue)
                    throw new InvalidOperationException("SDF voxel count exceeds Int32 budget.");

                StaticCaveSdfBakeConfigDTO config = default;
                config.Resolution = new int3(resolution);
                config.MaxSdfDistance = math.max(0.05f, sanitized.SdfMaxDistanceMeters);
                config.GlobalQualityWeight = sanitized.GlobalQualityWeight;
                config.SubMeshIndex = sanitized.SdfSubMeshIndex;
                config.VoxelCount = (int)voxelCount;
                config.Flags = StaticCaveSdfConstants.RollbackExcludedFlag;

                result = StaticCaveSdfBakePipeline.BakeMesh(
                    sanitized.SdfMeshFilter.sharedMesh,
                    sanitized.AssetName,
                    config,
                    StaticCaveSdfTexture3DExportMode.EncodedUnorm,
                    false);
                return !string.IsNullOrEmpty(result.TextureAssetPath);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is UnityException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                Debug.LogError("[VolumetricTextureBaker] Static Forge SDF bake failed: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private static string CreateTexture3DAsset(
            string safeName,
            string outputFolder,
            NativeArray<Color32> voxels,
            int3 resolution,
            ref uint warnings,
            out string actualFormat,
            out string compressionNote)
        {
            Texture3D texture;
            NativeArray<ushort> packedVoxels = default;
            if (!SystemInfo.supports3DTextures)
                throw new InvalidOperationException("Texture3D is not supported by the active editor graphics device.");
            if (!SystemInfo.SupportsTextureFormat(TextureFormat.RGB565))
                throw new InvalidOperationException("RGB565 Texture3D is not supported by the active editor graphics device.");

            try
            {
                packedVoxels = new NativeArray<ushort>(voxels.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                new PackRgb565FogVolumeJob
                {
                    Source = voxels,
                    Packed = packedVoxels
                // [EDITOR_BLOCKING_SYNC_POINT] Serialization consumes the packed Texture3D payload immediately.
                }.Schedule(voxels.Length, JobBatchSize).Complete();

                texture = BuildTextureRgb565(resolution, packedVoxels);
                actualFormat = "RGB565";
                compressionNote = "Packed 16-bit RGB565 Texture3D; R=density, G/B=flow, no RGBA32 fallback.";
            }
            catch (Exception ex) when (ex is UnityException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                warnings |= WarningTextureFormatRejected;
                actualFormat = "NONE";
                compressionNote = "RGB565 Texture3D payload rejected by editor runtime; bake aborted to prevent RGBA32 fallback emission.";
                throw new InvalidOperationException(compressionNote, ex);
            }
            finally
            {
                if (packedVoxels.IsCreated)
                    packedVoxels.Dispose();
            }

            texture.name = "TX_" + safeName + "_FogFlow";
            string path = AssetDatabase.GenerateUniqueAssetPath(outputFolder + "/TX_" + safeName + "_FogFlow.asset");
            // [EDITOR_BLOCKING_SYNC_POINT] Texture asset creation is a cold bake handoff.
            AssetDatabase.CreateAsset(texture, path);
            return path;
        }

        private static Texture3D BuildTextureRgb565(int3 resolution, NativeArray<ushort> packedVoxels)
        {
            Texture3D texture = new Texture3D(resolution.x, resolution.y, resolution.z, TextureFormat.RGB565, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 0
            };

            try
            {
                texture.SetPixelData(packedVoxels, 0);
                texture.Apply(true, true);
                return texture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        private static VolumeValidation ValidateColorVolume(NativeArray<Color32> voxels, int3 resolution)
        {
            if (!voxels.IsCreated || voxels.Length != ResolveVoxelCountOrThrow(resolution))
                throw new InvalidOperationException("Texture3D voxel count mismatch.");

            byte minR = byte.MaxValue;
            byte maxR = byte.MinValue;
            for (int i = 0; i < voxels.Length; i++)
            {
                byte r = voxels[i].r;
                minR = r < minR ? r : minR;
                maxR = r > maxR ? r : maxR;
            }

            uint warnings = 0u;
            if (maxR - minR < 80)
                warnings |= WarningValidationRangeWeak;
            return new VolumeValidation(minR, maxR, warnings);
        }

        private static int ResolveBakeResolution(float qualityWeight, int requestedResolution)
        {
            float q = Smooth01(qualityWeight);
            int continuous = Mathf.RoundToInt(Mathf.Lerp(MinimumResolution, MaximumResolution, q));
            int resolved = requestedResolution > 0 ? requestedResolution : continuous;
            resolved = Mathf.RoundToInt(Mathf.Lerp(resolved, continuous, 0.5f));
            return Mathf.Clamp(RoundToStep(resolved, ResolutionStep), MinimumResolution, MaximumResolution);
        }

        private static int RoundToStep(int value, int step)
        {
            int safeStep = Mathf.Max(1, step);
            return Mathf.Max(safeStep, ((value + safeStep / 2) / safeStep) * safeStep);
        }

        private static float Smooth01(float value)
        {
            float q = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
            return q * q * (3f - 2f * q);
        }

        private static int ResolveVoxelCountOrThrow(int3 resolution)
        {
            if (resolution.x < 2 || resolution.y < 2 || resolution.z < 2)
                throw new InvalidOperationException("Texture3D resolution must be at least 2 on every axis.");

            long count = (long)resolution.x * resolution.y * resolution.z;
            if (count <= 0L || count > int.MaxValue)
                throw new InvalidOperationException("Texture3D voxel count exceeds Int32 index budget.");
            return (int)count;
        }

        private static void ValidateStructLayoutsOrThrow()
        {
            int bytes = UnsafeUtility.SizeOf<FogBakeConfig1720>();
            if (bytes != 32 || (bytes & 7) != 0)
                throw new InvalidOperationException("FogBakeConfig1720 layout invalid: " + bytes + " bytes.");
        }

        private static uint StableHash32(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (!string.IsNullOrEmpty(value))
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619u;
                    }
                }

                return Mix(hash);
            }
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct FogBakeConfig1720
        {
            [FieldOffset(0)] public int3 Resolution;
            [FieldOffset(12)] public int Octaves;
            [FieldOffset(16)] public float GlobalQualityWeight;
            [FieldOffset(20)] public float DensityScale;
            [FieldOffset(24)] public float FlowStrength;
            [FieldOffset(28)] public uint Seed;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct PackRgb565FogVolumeJob : IJobParallelFor
        {
            [ReadOnly]
            [NoAlias]
            public NativeArray<Color32> Source;

            [WriteOnly]
            [NoAlias]
            public NativeArray<ushort> Packed;

            public void Execute(int index)
            {
                if (!Source.IsCreated || !Packed.IsCreated || (uint)index >= (uint)Source.Length || (uint)index >= (uint)Packed.Length)
                    return;

                Color32 value = Source[index];
                uint r = (uint)(value.r >> 3);
                uint g = (uint)(value.g >> 2);
                uint b = (uint)(value.b >> 3);
                Packed[index] = (ushort)((r << 11) | (g << 5) | b);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct FogVolumeBakeJob : IJobParallelFor
        {
            [WriteOnly]
            [NoAlias]
            public NativeArray<Color32> Voxels;

            public FogBakeConfig1720 Config;

            public void Execute(int index)
            {
                int3 resolution = math.max(Config.Resolution, new int3(2));
                int layer = resolution.x * resolution.y;
                int z = index / layer;
                int rem = index - z * layer;
                int y = rem / resolution.x;
                int x = rem - y * resolution.x;
                float3 uv = new float3(
                    x * math.rcp(math.max(1, resolution.x - 1)),
                    y * math.rcp(math.max(1, resolution.y - 1)),
                    z * math.rcp(math.max(1, resolution.z - 1)));

                float densityNoise = PeriodicFbm(uv, Config.Octaves, Config.Seed);
                float quality = math.saturate(Config.GlobalQualityWeight);
                float verticalStrata = math.lerp(1.18f, 0.54f, uv.y);
                float density = math.saturate((densityNoise * 0.82f + 0.18f) * Config.DensityScale * verticalStrata);
                density = math.saturate(math.lerp(density * 0.78f, density, quality));

                float dxStep = math.rcp(math.max(2, resolution.x - 1));
                float dzStep = math.rcp(math.max(2, resolution.z - 1));
                float dx = PeriodicFbm(uv + new float3(dxStep, 0f, 0f), Config.Octaves, Config.Seed ^ 0xA53C9E2Du) -
                           PeriodicFbm(uv - new float3(dxStep, 0f, 0f), Config.Octaves, Config.Seed ^ 0xA53C9E2Du);
                float dz = PeriodicFbm(uv + new float3(0f, 0f, dzStep), Config.Octaves, Config.Seed ^ 0xC2B2AE35u) -
                           PeriodicFbm(uv - new float3(0f, 0f, dzStep), Config.Octaves, Config.Seed ^ 0xC2B2AE35u);
                float2 flow = math.normalizesafe(new float2(dx, dz), new float2(0f, 1f)) * math.saturate(Config.FlowStrength);
                float2 packedFlow = math.saturate(flow * 0.5f + 0.5f);

                Voxels[index] = new Color32(
                    ToByte(density),
                    ToByte(packedFlow.x),
                    ToByte(packedFlow.y),
                    byte.MaxValue);
            }

            private static float PeriodicFbm(float3 uv, int octaves, uint seed)
            {
                int count = math.clamp(octaves, 1, 5);
                float sum = 0f;
                float amplitude = 0.58f;
                float amplitudeSum = 0f;
                for (int octave = 0; octave < count; octave++)
                {
                    int period = 4 << octave;
                    sum += PeriodicValueNoise(uv, period, seed + (uint)(octave * 747796405)) * amplitude;
                    amplitudeSum += amplitude;
                    amplitude *= 0.53f;
                }

                return amplitudeSum > 0f ? sum * math.rcp(amplitudeSum) : 0f;
            }

            private static float PeriodicValueNoise(float3 uv, int period, uint seed)
            {
                float3 p = uv * period;
                int3 i0 = (int3)math.floor(p);
                float3 f = p - math.floor(p);
                f = f * f * (3f - 2f * f);
                int3 i1 = i0 + 1;

                float c000 = Hash01(Wrap(i0.x, period), Wrap(i0.y, period), Wrap(i0.z, period), seed);
                float c100 = Hash01(Wrap(i1.x, period), Wrap(i0.y, period), Wrap(i0.z, period), seed);
                float c010 = Hash01(Wrap(i0.x, period), Wrap(i1.y, period), Wrap(i0.z, period), seed);
                float c110 = Hash01(Wrap(i1.x, period), Wrap(i1.y, period), Wrap(i0.z, period), seed);
                float c001 = Hash01(Wrap(i0.x, period), Wrap(i0.y, period), Wrap(i1.z, period), seed);
                float c101 = Hash01(Wrap(i1.x, period), Wrap(i0.y, period), Wrap(i1.z, period), seed);
                float c011 = Hash01(Wrap(i0.x, period), Wrap(i1.y, period), Wrap(i1.z, period), seed);
                float c111 = Hash01(Wrap(i1.x, period), Wrap(i1.y, period), Wrap(i1.z, period), seed);

                float c00 = math.lerp(c000, c100, f.x);
                float c10 = math.lerp(c010, c110, f.x);
                float c01 = math.lerp(c001, c101, f.x);
                float c11 = math.lerp(c011, c111, f.x);
                float c0 = math.lerp(c00, c10, f.y);
                float c1 = math.lerp(c01, c11, f.y);
                return math.lerp(c0, c1, f.z);
            }

            private static int Wrap(int value, int period)
            {
                int result = value % period;
                return result < 0 ? result + period : result;
            }

            private static float Hash01(int x, int y, int z, uint seed)
            {
                uint h = seed;
                h ^= (uint)x * 0x9E3779B9u;
                h ^= (uint)y * 0x85EBCA6Bu;
                h ^= (uint)z * 0xC2B2AE35u;
                h = MixForJob(h);
                return (h & 0x00FFFFFFu) * (1f / 16777216f);
            }

            private static uint MixForJob(uint value)
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value == 0u ? 1u : value;
            }

            private static byte ToByte(float value)
            {
                return (byte)math.clamp((int)math.round(math.saturate(value) * 255f), 0, 255);
            }
        }

        private readonly struct VolumeValidation
        {
            public readonly byte MinR;
            public readonly byte MaxR;
            public readonly uint WarningFlags;

            public VolumeValidation(byte minR, byte maxR, uint warningFlags)
            {
                MinR = minR;
                MaxR = maxR;
                WarningFlags = warningFlags;
            }
        }

        public readonly struct BakeSettings
        {
            public readonly string AssetName;
            public readonly string OutputFolder;
            public readonly MeshFilter SdfMeshFilter;
            public readonly float GlobalQualityWeight;
            public readonly int Resolution;
            public readonly int FogOctaves;
            public readonly int SdfSubMeshIndex;
            public readonly float FogDensityScale;
            public readonly float FlowStrength;
            public readonly float SdfMaxDistanceMeters;

            public BakeSettings(
                string assetName,
                string outputFolder,
                MeshFilter sdfMeshFilter,
                float globalQualityWeight,
                int resolution,
                int fogOctaves,
                int sdfSubMeshIndex,
                float fogDensityScale,
                float flowStrength,
                float sdfMaxDistanceMeters)
            {
                AssetName = assetName;
                OutputFolder = outputFolder;
                SdfMeshFilter = sdfMeshFilter;
                GlobalQualityWeight = globalQualityWeight;
                Resolution = resolution;
                FogOctaves = fogOctaves;
                SdfSubMeshIndex = sdfSubMeshIndex;
                FogDensityScale = fogDensityScale;
                FlowStrength = flowStrength;
                SdfMaxDistanceMeters = sdfMaxDistanceMeters;
            }

            public static BakeSettings DefaultFog()
            {
                return new BakeSettings("abyss_default", DefaultOutputFolder, null, 0.75f, 96, FogDefaultOctaves, -1, 1.15f, 0.82f, 12f);
            }

            public static BakeSettings DefaultSdf(MeshFilter meshFilter)
            {
                string meshName = meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : "Selected_Cave";
                return new BakeSettings(meshName, DefaultOutputFolder, meshFilter, 0.75f, 96, 1, -1, 1f, 0f, 12f);
            }

            public BakeSettings Sanitize()
            {
                float q = float.IsNaN(GlobalQualityWeight) || float.IsInfinity(GlobalQualityWeight) ? 0f : Mathf.Clamp01(GlobalQualityWeight);
                return new BakeSettings(
                    string.IsNullOrWhiteSpace(AssetName) ? "VolumetricTexture" : AssetName,
                    string.IsNullOrWhiteSpace(OutputFolder) ? DefaultOutputFolder : OutputFolder,
                    SdfMeshFilter,
                    q,
                    ResolveBakeResolution(q, Resolution),
                    Mathf.Clamp(FogOctaves, 1, 5),
                    Mathf.Max(-1, SdfSubMeshIndex),
                    Mathf.Clamp(float.IsNaN(FogDensityScale) || float.IsInfinity(FogDensityScale) ? 1f : FogDensityScale, 0.1f, 3f),
                    Mathf.Clamp(float.IsNaN(FlowStrength) || float.IsInfinity(FlowStrength) ? 0f : FlowStrength, 0f, 2f),
                    Mathf.Clamp(float.IsNaN(SdfMaxDistanceMeters) || float.IsInfinity(SdfMaxDistanceMeters) ? 12f : SdfMaxDistanceMeters, 0.05f, 1024f));
            }
        }

        public readonly struct BakeResult
        {
            public readonly string AssetName;
            public readonly string AssetPath;
            public readonly int3 Resolution;
            public readonly int VoxelCount;
            public readonly float GlobalQualityWeight;
            public readonly byte MinR;
            public readonly byte MaxR;
            public readonly string TextureFormatLabel;
            public readonly string CompressionNote;
            public readonly double GenerationMilliseconds;
            public readonly double TotalMilliseconds;
            public readonly uint WarningFlags;

            public BakeResult(
                string assetName,
                string assetPath,
                int3 resolution,
                int voxelCount,
                float globalQualityWeight,
                byte minR,
                byte maxR,
                string textureFormatLabel,
                string compressionNote,
                double generationMilliseconds,
                double totalMilliseconds,
                uint warningFlags)
            {
                AssetName = assetName;
                AssetPath = assetPath;
                Resolution = resolution;
                VoxelCount = voxelCount;
                GlobalQualityWeight = globalQualityWeight;
                MinR = minR;
                MaxR = maxR;
                TextureFormatLabel = textureFormatLabel;
                CompressionNote = compressionNote;
                GenerationMilliseconds = generationMilliseconds;
                TotalMilliseconds = totalMilliseconds;
                WarningFlags = warningFlags;
            }

            public static BakeResult Empty(uint warningFlags)
            {
                return new BakeResult(string.Empty, string.Empty, default, 0, 0f, 0, 0, string.Empty, string.Empty, 0.0, 0.0, warningFlags);
            }
        }
    }
}
