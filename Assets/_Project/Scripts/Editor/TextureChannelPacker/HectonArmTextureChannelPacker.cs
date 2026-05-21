#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Editor-only ARM packer configuration. Layout is ABI-stable for Burst and ARM64.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TexturePackerConfigDTO
    {
        [FieldOffset(0)] public float NormalIntensity;
        [FieldOffset(4)] public float RoughnessScale;
        [FieldOffset(8)] public float MetallicScale;
        [FieldOffset(12)] public uint Flags;
    }

    internal static unsafe class HectonArmTextureChannelPacker
    {
        internal const uint FlagInvertRoughness = 1u << 0;
        internal const uint FlagInjectMacroNoise = 1u << 1;
        internal const uint FlagToksvigMipFiltering = 1u << 2;
        internal const uint FlagGenerateNormals = 1u << 3;

        private const string OutputFolder = "Assets/_Project/BakedGeometry/Textures";
        private const string PackingReportPath = "Docs/Reports/TEXTURE_PACKING_REPORT.json";
        private const string LayoutReportPath = "Docs/Reports/TEXTURE_PACKER_LAYOUT_REPORT.json";
        private const string MockReportPath = "Docs/Reports/TEXTURE_PACKER_MOCK_BENCHMARK.json";
        private const int DefaultMaxTextureSize = 2048;
        private const int JobBatchSize = 128;
        private const int MockResolution = 4096;

        [MenuItem("Hecton8/Rendering/Texture Channel Packer/Validate ARM64 DTO Layout", priority = 201)]
        private static void ValidateLayoutFromMenu()
        {
            bool valid = ValidateTexturePackerConfigLayout(out string report);
            WriteText(LayoutReportPath, report);
            if (valid)
                Debug.Log("[HectonArmTextureChannelPacker] TexturePackerConfigDTO layout valid. Report: " + LayoutReportPath);
            else
                Debug.LogError("[HectonArmTextureChannelPacker] TexturePackerConfigDTO layout invalid. Report: " + LayoutReportPath);
        }

        [MenuItem("Hecton8/Rendering/Texture Channel Packer/Run 4K Mock Benchmark", priority = 202)]
        private static void RunMockBenchmarkFromMenu()
        {
            TexturePackerConfigDTO config = DefaultConfig(FlagInvertRoughness | FlagInjectMacroNoise | FlagToksvigMipFiltering);
            MockBenchmarkResult result = RunMockBenchmark(MockResolution, config);
            WriteText(MockReportPath, BuildMockBenchmarkJson(in result));
            Debug.Log("[HectonArmTextureChannelPacker] Mock 4K benchmark wrote " + MockReportPath);
        }

        [MenuItem("Hecton8/Rendering/Texture Channel Packer/Dump Black Box", priority = 203)]
        private static void DumpBlackBoxFromMenu()
        {
            TexturePackerBlackBox.Dump("manual-editor-menu");
            Debug.Log("[HectonArmTextureChannelPacker] Black box dump wrote " + TexturePackerBlackBox.DumpPath);
        }

        internal static TexturePackerConfigDTO DefaultConfig(uint flags)
        {
            TexturePackerConfigDTO config;
            config.NormalIntensity = 1.0f;
            config.RoughnessScale = 1.0f;
            config.MetallicScale = 1.0f;
            config.Flags = flags;
            return config;
        }

        internal static bool ValidateTexturePackerConfigLayout(out string report)
        {
            int size = UnsafeUtility.SizeOf<TexturePackerConfigDTO>();
            int normalOffset = (int)Marshal.OffsetOf<TexturePackerConfigDTO>(nameof(TexturePackerConfigDTO.NormalIntensity));
            int roughnessOffset = (int)Marshal.OffsetOf<TexturePackerConfigDTO>(nameof(TexturePackerConfigDTO.RoughnessScale));
            int metallicOffset = (int)Marshal.OffsetOf<TexturePackerConfigDTO>(nameof(TexturePackerConfigDTO.MetallicScale));
            int flagsOffset = (int)Marshal.OffsetOf<TexturePackerConfigDTO>(nameof(TexturePackerConfigDTO.Flags));
            bool valid = size == 16 &&
                         normalOffset == 0 &&
                         roughnessOffset == 4 &&
                         metallicOffset == 8 &&
                         flagsOffset == 12;

            StringBuilder builder = new StringBuilder(512); // COLD ALLOC: StringBuilder[512] - editor layout report - owner: HectonArmTextureChannelPacker
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.texture_packer_layout.v1", true);
            AppendJson(builder, "structName", "TexturePackerConfigDTO", true);
            AppendJson(builder, "sizeBytes", size, true);
            AppendJson(builder, "normalIntensityOffset", normalOffset, true);
            AppendJson(builder, "roughnessScaleOffset", roughnessOffset, true);
            AppendJson(builder, "metallicScaleOffset", metallicOffset, true);
            AppendJson(builder, "flagsOffset", flagsOffset, true);
            AppendJson(builder, "multipleOfEight", (size & 7) == 0, true);
            AppendJson(builder, "valid", valid, false);
            builder.Append("}\n");
            report = builder.ToString();
            return valid;
        }

        internal static bool TryPackArmAsset(TexturePackerRequest request, out TexturePackerRunMetrics metrics)
        {
            metrics = default;
            if (!ValidateRequest(ref request))
                return false;

            int width = ResolvePackWidth(request);
            int height = ResolvePackHeight(request);
            int pixelCount = width * height;
            metrics.Width = width;
            metrics.Height = height;
            metrics.InputTextureCount = CountSourceTextures(request);
            metrics.OutputFormat = "BC7";

            Texture2D aoSnapshot = null;
            Texture2D roughnessSnapshot = null;
            Texture2D metallicSnapshot = null;
            Texture2D albedoSnapshot = null;
            NativeArray<Color32> aoPixels = default;
            NativeArray<Color32> roughnessPixels = default;
            NativeArray<Color32> metallicPixels = default;
            NativeArray<Color32> albedoPixels = default;
            NativeArray<Color32> armPixels = default;
            NativeArray<Color32> normalPixels = default;
            Texture2D armTexture = null;
            Texture2D normalTexture = null;
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                aoPixels = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[pixelCount] - editor AO source buffer - owner: HectonArmTextureChannelPacker
                roughnessPixels = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[pixelCount] - editor roughness source buffer - owner: HectonArmTextureChannelPacker
                metallicPixels = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[pixelCount] - editor metallic source buffer - owner: HectonArmTextureChannelPacker
                armPixels = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[pixelCount] - editor packed ARM output buffer - owner: HectonArmTextureChannelPacker

                JobHandle aoHandle = PrepareSource(request.AoTexture, width, height, new Color32(255, 255, 255, 255), aoPixels, ref aoSnapshot);
                JobHandle roughnessHandle = PrepareSource(request.RoughnessTexture, width, height, new Color32(166, 166, 166, 255), roughnessPixels, ref roughnessSnapshot);
                JobHandle metallicHandle = PrepareSource(request.MetallicTexture, width, height, new Color32(0, 0, 0, 255), metallicPixels, ref metallicSnapshot);
                JobHandle packDeps = JobHandle.CombineDependencies(aoHandle, roughnessHandle, metallicHandle);

                JobHandle packHandle = new PackArmTextureJob
                {
                    Ao = (Color32*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(aoPixels),
                    Roughness = (Color32*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(roughnessPixels),
                    Metallic = (Color32*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(metallicPixels),
                    Output = (Color32*)NativeArrayUnsafeUtility.GetUnsafePtr(armPixels),
                    Config = request.Config
                }.Schedule(pixelCount, JobBatchSize, packDeps);

                if ((request.Config.Flags & FlagInjectMacroNoise) != 0u && request.MacroNoiseStrength > 0.0001f)
                {
                    packHandle = new InjectMacroNoiseJob
                    {
                        Pixels = armPixels,
                        Width = width,
                        Height = height,
                        MacroStrength = math.saturate(request.MacroNoiseStrength),
                        TileSizeMeters = math.max(0.001f, request.TileSizeMeters),
                        MacroWorldSpanMeters = math.max(1.0f, request.MacroWorldSpanMeters),
                        GlobalQualityWeight = math.saturate(request.GlobalQualityWeight),
                        Seed = request.Seed
                    }.Schedule(pixelCount, JobBatchSize, packHandle);
                }

                if ((request.Config.Flags & FlagGenerateNormals) != 0u && request.AlbedoTexture != null)
                {
                    albedoPixels = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[pixelCount] - editor albedo source buffer - owner: HectonArmTextureChannelPacker
                    normalPixels = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[pixelCount] - editor generated normal buffer - owner: HectonArmTextureChannelPacker
                    JobHandle albedoHandle = PrepareSource(request.AlbedoTexture, width, height, new Color32(128, 128, 128, 255), albedoPixels, ref albedoSnapshot);
                    JobHandle normalHandle = new GenerateSobelNormalsJob
                    {
                        Albedo = albedoPixels,
                        Output = normalPixels,
                        Width = width,
                        Height = height,
                        Intensity = math.max(0.001f, request.Config.NormalIntensity)
                    }.Schedule(pixelCount, JobBatchSize, albedoHandle);
                    packHandle = JobHandle.CombineDependencies(packHandle, normalHandle);
                }

                // Editor serialization boundary: Texture2D.SetPixelData and AssetDatabase need materialized CPU buffers here; runtime is excluded.
                packHandle.Complete();
                metrics.JobMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

                string armPath = CreateUniqueAssetPath(request.OutputFolder, request.OutputName, "_ARM.asset");
                armTexture = BuildArmTextureAsset(width, height, armPixels, normalPixels, normalPixels.IsCreated, request, armPath);
                metrics.OutputPath = armPath;

                if (normalPixels.IsCreated)
                {
                    string normalPath = CreateUniqueAssetPath(request.OutputFolder, request.OutputName, "_N.asset");
                    normalTexture = BuildNormalTextureAsset(width, height, normalPixels, request, normalPath);
                    metrics.NormalOutputPath = normalPath;
                }

                metrics.TotalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                metrics.EstimatedBeforeBytes = EstimateBeforeBytes(width, height, metrics.InputTextureCount);
                metrics.EstimatedAfterBytes = EstimateAfterBytes(width, height, normalPixels.IsCreated);
                long savedBytes = metrics.EstimatedBeforeBytes - metrics.EstimatedAfterBytes;
                metrics.EstimatedSavedBytes = savedBytes > 0L ? savedBytes : 0L;
                metrics.ProcessedTextureCount = 1;
                metrics.CriticalWarning = IsPowerOfTwo(width) && IsPowerOfTwo(height) ? string.Empty : "CRITICAL_WARNING: non-power-of-two packed output.";
                uint outputHash = HashPixelWindow(armPixels, math.max(1, pixelCount / 4096));
                TexturePackerBlackBox.RecordPack(in request, in metrics, pixelCount, outputHash, 0u);
                if (!math.isfinite((float)metrics.JobMilliseconds) || !math.isfinite((float)metrics.TotalMilliseconds))
                {
                    TexturePackerBlackBox.RecordPack(in request, in metrics, pixelCount, outputHash, 0x4E414E31u);
                    TexturePackerBlackBox.Dump("non-finite-texture-pack-metrics");
                }

                WritePackingReport(in metrics);
                return armTexture != null || normalTexture != null;
            }
            catch (Exception exception)
            {
                metrics.TotalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                metrics.CriticalWarning = exception.GetType().Name;
                TexturePackerBlackBox.RecordFault(in request, in metrics, pixelCount, 0x4641494Cu);
                TexturePackerBlackBox.Dump(exception.GetType().Name);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                DestroyImmediateIfNeeded(aoSnapshot);
                DestroyImmediateIfNeeded(roughnessSnapshot);
                DestroyImmediateIfNeeded(metallicSnapshot);
                DestroyImmediateIfNeeded(albedoSnapshot);
                if (aoPixels.IsCreated)
                    aoPixels.Dispose();
                if (roughnessPixels.IsCreated)
                    roughnessPixels.Dispose();
                if (metallicPixels.IsCreated)
                    metallicPixels.Dispose();
                if (albedoPixels.IsCreated)
                    albedoPixels.Dispose();
                if (armPixels.IsCreated)
                    armPixels.Dispose();
                if (normalPixels.IsCreated)
                    normalPixels.Dispose();
            }
        }

        private static Texture2D BuildArmTextureAsset(
            int width,
            int height,
            NativeArray<Color32> basePixels,
            NativeArray<Color32> normalPixels,
            bool hasNormalPixels,
            TexturePackerRequest request,
            string assetPath)
        {
            int mipCount = ResolveMipCount(width, height);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, mipCount > 1, true)
            {
                name = Path.GetFileNameWithoutExtension(assetPath),
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2
            };

            texture.SetPixelData(basePixels, 0);
            if ((request.Config.Flags & FlagToksvigMipFiltering) != 0u && mipCount > 1)
                WriteFilteredArmMips(texture, basePixels, normalPixels, hasNormalPixels, width, height);

            texture.Apply(false, false);
            TryCompressTexture(texture, TextureFormat.BC7);
            EnsureAssetFolder(Path.GetDirectoryName(assetPath));
            AssetDatabase.CreateAsset(texture, assetPath);
            AssetDatabase.SaveAssets();
            return texture;
        }

        private static Texture2D BuildNormalTextureAsset(
            int width,
            int height,
            NativeArray<Color32> basePixels,
            TexturePackerRequest request,
            string assetPath)
        {
            int mipCount = ResolveMipCount(width, height);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, mipCount > 1, true)
            {
                name = Path.GetFileNameWithoutExtension(assetPath),
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };

            texture.SetPixelData(basePixels, 0);
            if (mipCount > 1)
                WriteNormalMips(texture, basePixels, width, height);

            texture.Apply(false, false);
            TryCompressTexture(texture, TextureFormat.BC5);
            EnsureAssetFolder(Path.GetDirectoryName(assetPath));
            AssetDatabase.CreateAsset(texture, assetPath);
            AssetDatabase.SaveAssets();
            return texture;
        }

        private static void WriteFilteredArmMips(
            Texture2D texture,
            NativeArray<Color32> basePixels,
            NativeArray<Color32> normalPixels,
            bool hasNormalPixels,
            int baseWidth,
            int baseHeight)
        {
            NativeArray<Color32> previousArm = basePixels;
            NativeArray<Color32> previousNormal = normalPixels;
            bool ownsPreviousArm = false;
            bool ownsPreviousNormal = false;
            int previousWidth = baseWidth;
            int previousHeight = baseHeight;
            int mip = 1;

            try
            {
                while (previousWidth > 1 || previousHeight > 1)
                {
                    int width = math.max(1, previousWidth >> 1);
                    int height = math.max(1, previousHeight >> 1);
                    NativeArray<Color32> currentArm = new NativeArray<Color32>(width * height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[mip] - editor ARM mip buffer - owner: HectonArmTextureChannelPacker
                    NativeArray<Color32> currentNormal = default;

                    new GenerateArmMipJob
                    {
                        Source = previousArm,
                        NormalSource = hasNormalPixels ? previousNormal : default,
                        Output = currentArm,
                        SourceWidth = previousWidth,
                        SourceHeight = previousHeight,
                        OutputWidth = width,
                        OutputHeight = height,
                        HasNormalSource = hasNormalPixels ? 1 : 0
                    // Editor mip materialization boundary: each mip buffer must be complete before Texture2D consumes it.
                    }.Schedule(currentArm.Length, JobBatchSize).Complete();

                    texture.SetPixelData(currentArm, mip);

                    if (hasNormalPixels)
                    {
                        currentNormal = new NativeArray<Color32>(width * height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[mip] - editor normal mip buffer - owner: HectonArmTextureChannelPacker
                        new GenerateNormalMipJob
                        {
                            Source = previousNormal,
                            Output = currentNormal,
                            SourceWidth = previousWidth,
                            SourceHeight = previousHeight,
                            OutputWidth = width,
                            OutputHeight = height
                        // Editor mip materialization boundary: normal variance must be finalized before SetPixelData.
                        }.Schedule(currentNormal.Length, JobBatchSize).Complete();
                    }

                    if (ownsPreviousArm && previousArm.IsCreated)
                        previousArm.Dispose();
                    if (ownsPreviousNormal && previousNormal.IsCreated)
                        previousNormal.Dispose();

                    previousArm = currentArm;
                    previousNormal = currentNormal;
                    ownsPreviousArm = true;
                    ownsPreviousNormal = hasNormalPixels;
                    previousWidth = width;
                    previousHeight = height;
                    mip++;
                }
            }
            finally
            {
                if (ownsPreviousArm && previousArm.IsCreated)
                    previousArm.Dispose();
                if (ownsPreviousNormal && previousNormal.IsCreated)
                    previousNormal.Dispose();
            }
        }

        private static void WriteNormalMips(Texture2D texture, NativeArray<Color32> basePixels, int baseWidth, int baseHeight)
        {
            NativeArray<Color32> previous = basePixels;
            bool ownsPrevious = false;
            int previousWidth = baseWidth;
            int previousHeight = baseHeight;
            int mip = 1;

            try
            {
                while (previousWidth > 1 || previousHeight > 1)
                {
                    int width = math.max(1, previousWidth >> 1);
                    int height = math.max(1, previousHeight >> 1);
                    NativeArray<Color32> current = new NativeArray<Color32>(width * height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[mip] - editor normal mip buffer - owner: HectonArmTextureChannelPacker
                    new GenerateNormalMipJob
                    {
                        Source = previous,
                        Output = current,
                        SourceWidth = previousWidth,
                        SourceHeight = previousHeight,
                        OutputWidth = width,
                        OutputHeight = height
                    // Editor mip materialization boundary: generated normal mips are immediately serialized into Texture2D.
                    }.Schedule(current.Length, JobBatchSize).Complete();
                    texture.SetPixelData(current, mip);
                    if (ownsPrevious && previous.IsCreated)
                        previous.Dispose();
                    previous = current;
                    ownsPrevious = true;
                    previousWidth = width;
                    previousHeight = height;
                    mip++;
                }
            }
            finally
            {
                if (ownsPrevious && previous.IsCreated)
                    previous.Dispose();
            }
        }

        private static JobHandle PrepareSource(
            Texture2D source,
            int width,
            int height,
            Color32 fallback,
            NativeArray<Color32> output,
            ref Texture2D snapshot)
        {
            if (source == null)
            {
                return new FillConstantTextureJob
                {
                    Output = output,
                    Value = fallback
                }.Schedule(output.Length, JobBatchSize);
            }

            snapshot = CaptureReadableTexture(source, width, height);
            NativeArray<Color32> sourcePixels = snapshot.GetRawTextureData<Color32>();
            return new CopyTextureJob
            {
                Source = sourcePixels,
                Output = output
            }.Schedule(output.Length, JobBatchSize);
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

        private static MockBenchmarkResult RunMockBenchmark(int resolution, TexturePackerConfigDTO config)
        {
            int width = math.max(1, resolution);
            int height = width;
            int pixelCount = width * height;
            NativeArray<Color32> ao = default;
            NativeArray<Color32> roughness = default;
            NativeArray<Color32> metallic = default;
            NativeArray<Color32> albedo = default;
            NativeArray<Color32> output = default;
            NativeArray<Color32> normals = default;
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                ao = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[4K] - editor mock AO - owner: HectonArmTextureChannelPacker
                roughness = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[4K] - editor mock roughness - owner: HectonArmTextureChannelPacker
                metallic = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[4K] - editor mock metallic - owner: HectonArmTextureChannelPacker
                albedo = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[4K] - editor mock albedo - owner: HectonArmTextureChannelPacker
                output = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[4K] - editor mock output - owner: HectonArmTextureChannelPacker
                normals = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Color32>[4K] - editor mock normals - owner: HectonArmTextureChannelPacker

                JobHandle mockHandle = new GenerateMockTexturePackJob
                {
                    Ao = ao,
                    Roughness = roughness,
                    Metallic = metallic,
                    Albedo = albedo,
                    Width = width,
                    Height = height,
                    Seed = 0x53483231u
                }.Schedule(pixelCount, JobBatchSize);

                JobHandle packHandle = new PackArmTextureJob
                {
                    Ao = (Color32*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ao),
                    Roughness = (Color32*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(roughness),
                    Metallic = (Color32*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(metallic),
                    Output = (Color32*)NativeArrayUnsafeUtility.GetUnsafePtr(output),
                    Config = config
                }.Schedule(pixelCount, JobBatchSize, mockHandle);

                JobHandle normalHandle = new GenerateSobelNormalsJob
                {
                    Albedo = albedo,
                    Output = normals,
                    Width = width,
                    Height = height,
                    Intensity = math.max(0.001f, config.NormalIntensity)
                }.Schedule(pixelCount, JobBatchSize, packHandle);

                // Editor benchmark boundary: mock report consumes completed timings and hashes immediately after the stress pass.
                normalHandle.Complete();
                stopwatch.Stop();

                MockBenchmarkResult result;
                result.Resolution = width;
                result.PixelCount = pixelCount;
                result.ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                result.EstimatedPixelsPerMillisecond = pixelCount / math.max(0.001, result.ElapsedMilliseconds);
                result.ConfigSize = UnsafeUtility.SizeOf<TexturePackerConfigDTO>();
                result.OutputHash = HashPixelWindow(output, math.max(1, pixelCount / 4096));
                TexturePackerBlackBox.RecordMock(in result, config.Flags);
                return result;
            }
            finally
            {
                if (ao.IsCreated)
                    ao.Dispose();
                if (roughness.IsCreated)
                    roughness.Dispose();
                if (metallic.IsCreated)
                    metallic.Dispose();
                if (albedo.IsCreated)
                    albedo.Dispose();
                if (output.IsCreated)
                    output.Dispose();
                if (normals.IsCreated)
                    normals.Dispose();
            }
        }

        private static uint HashPixelWindow(NativeArray<Color32> pixels, int stride)
        {
            uint hash = 2166136261u;
            int safeStride = math.max(1, stride);
            for (int i = 0; i < pixels.Length; i += safeStride)
            {
                Color32 p = pixels[i];
                hash ^= p.r;
                hash *= 16777619u;
                hash ^= p.g;
                hash *= 16777619u;
                hash ^= p.b;
                hash *= 16777619u;
            }

            return hash;
        }

        private static int ResolvePackWidth(TexturePackerRequest request)
        {
            int width = 1;
            width = math.max(width, TextureWidth(request.AoTexture));
            width = math.max(width, TextureWidth(request.RoughnessTexture));
            width = math.max(width, TextureWidth(request.MetallicTexture));
            width = math.max(width, TextureWidth(request.AlbedoTexture));
            return ResolveAxisDimension(width, request.MaxSize);
        }

        private static int ResolvePackHeight(TexturePackerRequest request)
        {
            int height = 1;
            height = math.max(height, TextureHeight(request.AoTexture));
            height = math.max(height, TextureHeight(request.RoughnessTexture));
            height = math.max(height, TextureHeight(request.MetallicTexture));
            height = math.max(height, TextureHeight(request.AlbedoTexture));
            return ResolveAxisDimension(height, request.MaxSize);
        }

        private static int ResolveAxisDimension(int sourceAxis, int maxSize)
        {
            int limit = maxSize > 0 ? maxSize : DefaultMaxTextureSize;
            return math.min(limit, Mathf.NextPowerOfTwo(math.max(1, sourceAxis)));
        }

        private static int TextureWidth(Texture2D texture)
        {
            return texture != null ? texture.width : 1;
        }

        private static int TextureHeight(Texture2D texture)
        {
            return texture != null ? texture.height : 1;
        }

        private static int ResolveMipCount(int width, int height)
        {
            int maxDim = math.max(width, height);
            int count = 1;
            while (maxDim > 1)
            {
                maxDim >>= 1;
                count++;
            }

            return count;
        }

        private static bool ValidateRequest(ref TexturePackerRequest request)
        {
            if (request.AoTexture == null && request.RoughnessTexture == null && request.MetallicTexture == null)
            {
                Debug.LogError("[HectonArmTextureChannelPacker] Request has no AO/Roughness/Metallic source texture.");
                return false;
            }

            if (string.IsNullOrEmpty(request.OutputName))
                request.OutputName = BuildOutputName(request);

            if (string.IsNullOrEmpty(request.OutputFolder))
                request.OutputFolder = OutputFolder;

            if (request.MaxSize <= 0)
                request.MaxSize = DefaultMaxTextureSize;

            EnsureAssetFolder(request.OutputFolder);
            return true;
        }

        private static string BuildOutputName(TexturePackerRequest request)
        {
            Texture2D source = request.AoTexture != null ? request.AoTexture : request.RoughnessTexture != null ? request.RoughnessTexture : request.MetallicTexture;
            return source != null ? SanitizeAssetToken(source.name) : "TX_Packed";
        }

        private static string CreateUniqueAssetPath(string folder, string outputName, string suffix)
        {
            EnsureAssetFolder(folder);
            string cleanName = SanitizeAssetToken(outputName);
            if (!cleanName.StartsWith("TX_", StringComparison.Ordinal))
                cleanName = "TX_" + cleanName;

            return AssetDatabase.GenerateUniqueAssetPath(folder + "/" + cleanName + suffix);
        }

        private static string SanitizeAssetToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Texture";

            StringBuilder builder = new StringBuilder(value.Length); // COLD ALLOC: StringBuilder[token] - editor asset-name sanitation - owner: HectonArmTextureChannelPacker
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool valid = (c >= 'a' && c <= 'z') ||
                             (c >= 'A' && c <= 'Z') ||
                             (c >= '0' && c <= '9') ||
                             c == '_' ||
                             c == '-';
                builder.Append(valid ? c : '_');
            }

            return builder.ToString();
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string normalized = folder.Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            if (slash <= 0)
                return;

            string parent = normalized.Substring(0, slash);
            string name = normalized.Substring(slash + 1);
            EnsureAssetFolder(parent);
            if (!AssetDatabase.IsValidFolder(normalized))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void TryCompressTexture(Texture2D texture, TextureFormat format)
        {
            if (texture == null)
                return;

            try
            {
                EditorUtility.CompressTexture(texture, format, TextureCompressionQuality.Best);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[HectonArmTextureChannelPacker] Texture compression failed: " + exception.Message);
            }
        }

        private static int CountSourceTextures(TexturePackerRequest request)
        {
            int count = 0;
            if (request.AoTexture != null)
                count++;
            if (request.RoughnessTexture != null)
                count++;
            if (request.MetallicTexture != null)
                count++;
            if (request.AlbedoTexture != null)
                count++;
            return count;
        }

        private static long EstimateBeforeBytes(int width, int height, int inputCount)
        {
            long texels = (long)width * height;
            return texels * math.max(3, inputCount) * 4L;
        }

        private static long EstimateAfterBytes(int width, int height, bool hasGeneratedNormal)
        {
            long texels = (long)width * height;
            long armBc7Bytes = texels;
            long normalBc5Bytes = hasGeneratedNormal ? texels : 0L;
            return armBc7Bytes + normalBc5Bytes;
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static void DestroyImmediateIfNeeded(Object obj)
        {
            if (obj != null)
                Object.DestroyImmediate(obj);
        }

        private static void WritePackingReport(in TexturePackerRunMetrics metrics)
        {
            WriteText(PackingReportPath, BuildPackingReportJson(in metrics));
        }

        private static void WriteText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, text, Encoding.UTF8);
        }

        private static string BuildPackingReportJson(in TexturePackerRunMetrics metrics)
        {
            StringBuilder builder = new StringBuilder(1024); // COLD ALLOC: StringBuilder[1024] - editor packing JSON report - owner: HectonArmTextureChannelPacker
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.texture_packing_report.v1", true);
            AppendJson(builder, "processedTextures", metrics.ProcessedTextureCount, true);
            AppendJson(builder, "inputTextureCount", metrics.InputTextureCount, true);
            AppendJson(builder, "width", metrics.Width, true);
            AppendJson(builder, "height", metrics.Height, true);
            AppendJson(builder, "compression", metrics.OutputFormat, true);
            AppendJson(builder, "outputPath", metrics.OutputPath ?? string.Empty, true);
            AppendJson(builder, "normalOutputPath", metrics.NormalOutputPath ?? string.Empty, true);
            AppendJson(builder, "estimatedBeforeBytes", metrics.EstimatedBeforeBytes, true);
            AppendJson(builder, "estimatedAfterBytes", metrics.EstimatedAfterBytes, true);
            AppendJson(builder, "estimatedSavedBytes", metrics.EstimatedSavedBytes, true);
            AppendJson(builder, "jobMilliseconds", metrics.JobMilliseconds, true);
            AppendJson(builder, "totalMilliseconds", metrics.TotalMilliseconds, true);
            AppendJson(builder, "blackboxDumpPath", TexturePackerBlackBox.DumpPath, true);
            AppendJson(builder, "blackboxEntryBytes", UnsafeUtility.SizeOf<TexturePackerTelemetryEntry>(), true);
            AppendJson(builder, "blackboxRingLength", TexturePackerBlackBox.RingCapacity, true);
            AppendJson(builder, "criticalWarning", metrics.CriticalWarning ?? string.Empty, false);
            builder.Append("}\n");
            return builder.ToString();
        }

        private static string BuildMockBenchmarkJson(in MockBenchmarkResult result)
        {
            StringBuilder builder = new StringBuilder(512); // COLD ALLOC: StringBuilder[512] - editor mock benchmark report - owner: HectonArmTextureChannelPacker
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.texture_packer_mock_benchmark.v1", true);
            AppendJson(builder, "resolution", result.Resolution, true);
            AppendJson(builder, "pixelCount", result.PixelCount, true);
            AppendJson(builder, "elapsedMilliseconds", result.ElapsedMilliseconds, true);
            AppendJson(builder, "estimatedPixelsPerMillisecond", result.EstimatedPixelsPerMillisecond, true);
            AppendJson(builder, "configSize", result.ConfigSize, true);
            AppendJson(builder, "outputHash", result.OutputHash, false);
            builder.Append("}\n");
            return builder.ToString();
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": \"");
            AppendEscaped(builder, value);
            builder.Append('"');
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendJson(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value ? "true" : "false");
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

        private static void AppendJson(StringBuilder builder, string name, uint value, bool comma)
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

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            if (value == null)
                return;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\')
                    builder.Append('\\');
                builder.Append(c);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ToByte(float value)
        {
            return (byte)math.round(math.saturate(value) * 255f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Luminance(Color32 value)
        {
            return (byte)(((value.r * 54) + (value.g * 183) + (value.b * 19)) >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ScaleByte(byte value, float scale)
        {
            return ToByte((value * (1f / 255f)) * scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackArm32(byte ao, byte roughness, byte metallic)
        {
            v128 packed = default;
            packed.Byte0 = ao;
            packed.Byte1 = roughness;
            packed.Byte2 = metallic;
            packed.Byte3 = 255;
            return packed.UInt0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color32 UnpackColor32(uint packed)
        {
            return new Color32(
                (byte)(packed & 0xFFu),
                (byte)((packed >> 8) & 0xFFu),
                (byte)((packed >> 16) & 0xFFu),
                (byte)((packed >> 24) & 0xFFu));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct FillConstantTextureJob : IJobParallelFor
        {
            [WriteOnly, NoAlias] public NativeArray<Color32> Output;
            public Color32 Value;

            public void Execute(int index)
            {
                Output[index] = Value;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct CopyTextureJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Color32> Source;
            [WriteOnly, NoAlias] public NativeArray<Color32> Output;

            public void Execute(int index)
            {
                Output[index] = Source[index];
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct GenerateMockTexturePackJob : IJobParallelFor
        {
            [WriteOnly, NoAlias] public NativeArray<Color32> Ao;
            [WriteOnly, NoAlias] public NativeArray<Color32> Roughness;
            [WriteOnly, NoAlias] public NativeArray<Color32> Metallic;
            [WriteOnly, NoAlias] public NativeArray<Color32> Albedo;
            public int Width;
            public int Height;
            public uint Seed;

            public void Execute(int index)
            {
                int width = math.max(1, Width);
                int x = index % width;
                int y = index / width;
                uint hash = Hash((uint)x, (uint)y, Seed);
                byte ao = (byte)(170 + (hash & 63u));
                byte rough = (byte)(96 + ((hash >> 8) & 127u));
                byte metal = (byte)(((hash >> 21) & 7u) == 0u ? 220 : 8);
                byte albedo = (byte)(48 + ((hash >> 16) & 159u));
                Ao[index] = new Color32(ao, ao, ao, 255);
                Roughness[index] = new Color32(rough, rough, rough, 255);
                Metallic[index] = new Color32(metal, metal, metal, 255);
                Albedo[index] = new Color32(albedo, (byte)math.min(255, albedo + 18), (byte)math.max(0, albedo - 12), 255);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal unsafe struct PackArmTextureJob : IJobParallelFor
        {
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public Color32* Ao;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public Color32* Roughness;
            [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public Color32* Metallic;
            [WriteOnly, NoAlias, NativeDisableUnsafePtrRestriction] public Color32* Output;
            public TexturePackerConfigDTO Config;

            public void Execute(int index)
            {
                Color32 aoPixel = UnsafeUtility.AsRef<Color32>(Ao + index);
                Color32 roughPixel = UnsafeUtility.AsRef<Color32>(Roughness + index);
                Color32 metallicPixel = UnsafeUtility.AsRef<Color32>(Metallic + index);
                byte ao = Luminance(aoPixel);
                byte roughness = ScaleByte(Luminance(roughPixel), math.max(0f, Config.RoughnessScale));
                byte metallic = ScaleByte(Luminance(metallicPixel), math.max(0f, Config.MetallicScale));
                uint invertMask = (Config.Flags & FlagInvertRoughness) != 0u ? 0xFFu : 0u;
                roughness = (byte)((uint)roughness ^ invertMask);
                UnsafeUtility.AsRef<Color32>(Output + index) = UnpackColor32(PackArm32(ao, roughness, metallic));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct GenerateSobelNormalsJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Color32> Albedo;
            [WriteOnly, NoAlias] public NativeArray<Color32> Output;
            public int Width;
            public int Height;
            public float Intensity;

            public void Execute(int index)
            {
                int width = math.max(1, Width);
                int height = math.max(1, Height);
                int x = index % width;
                int y = index / width;
                float s00 = Gray(SampleClamped(x - 1, y - 1, width, height));
                float s10 = Gray(SampleClamped(x, y - 1, width, height));
                float s20 = Gray(SampleClamped(x + 1, y - 1, width, height));
                float s01 = Gray(SampleClamped(x - 1, y, width, height));
                float s21 = Gray(SampleClamped(x + 1, y, width, height));
                float s02 = Gray(SampleClamped(x - 1, y + 1, width, height));
                float s12 = Gray(SampleClamped(x, y + 1, width, height));
                float s22 = Gray(SampleClamped(x + 1, y + 1, width, height));
                float dx = (s20 + 2f * s21 + s22) - (s00 + 2f * s01 + s02);
                float dy = (s02 + 2f * s12 + s22) - (s00 + 2f * s10 + s20);
                float safeIntensity = math.max(0.001f, Intensity);
                float3 normal = SafeNormalize(new float3(-dx * safeIntensity, -dy * safeIntensity, 1f), new float3(0f, 0f, 1f));
                Output[index] = new Color32(
                    ToByte(normal.x * 0.5f + 0.5f),
                    ToByte(normal.y * 0.5f + 0.5f),
                    ToByte(normal.z * 0.5f + 0.5f),
                    255);
            }

            private Color32 SampleClamped(int x, int y, int width, int height)
            {
                int sx = math.clamp(x, 0, width - 1);
                int sy = math.clamp(y, 0, height - 1);
                return Albedo[(sy * width) + sx];
            }

            private static float Gray(Color32 value)
            {
                return Luminance(value) * (1f / 255f);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct InjectMacroNoiseJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<Color32> Pixels;
            public int Width;
            public int Height;
            public float MacroStrength;
            public float TileSizeMeters;
            public float MacroWorldSpanMeters;
            public float GlobalQualityWeight;
            public uint Seed;

            public void Execute(int index)
            {
                int width = math.max(1, Width);
                int x = index % width;
                int y = index / width;
                float2 uv = new float2(x / (float)math.max(1, Width - 1), y / (float)math.max(1, Height - 1));
                float tileMeters = math.max(0.001f, TileSizeMeters);
                float worldSpan = math.max(tileMeters, MacroWorldSpanMeters);
                float frequency = tileMeters / worldSpan;
                float macro = Fbm(uv * frequency * 1024f, Seed, GlobalQualityWeight);
                float centered = (macro - 0.5f) * 2f;
                float quality = math.saturate(GlobalQualityWeight);
                float qualityCurve = quality * quality * (3f - 2f * quality);
                float strength = math.saturate(MacroStrength) * math.lerp(0.35f, 1.0f, qualityCurve);
                float multiplier = math.saturate(1f + centered * strength);
                Color32 pixel = Pixels[index];
                pixel.r = ToByte((pixel.r * (1f / 255f)) * multiplier);
                pixel.g = ToByte((pixel.g * (1f / 255f)) * math.lerp(1f, multiplier, 0.65f));
                Pixels[index] = pixel;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct GenerateArmMipJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Color32> Source;
            [ReadOnly, NoAlias] public NativeArray<Color32> NormalSource;
            [WriteOnly, NoAlias] public NativeArray<Color32> Output;
            public int SourceWidth;
            public int SourceHeight;
            public int OutputWidth;
            public int OutputHeight;
            public int HasNormalSource;

            public void Execute(int index)
            {
                int width = math.max(1, OutputWidth);
                int x = index % width;
                int y = index / width;
                int sx = x << 1;
                int sy = y << 1;
                Color32 p0 = Sample(Source, sx, sy);
                Color32 p1 = Sample(Source, sx + 1, sy);
                Color32 p2 = Sample(Source, sx, sy + 1);
                Color32 p3 = Sample(Source, sx + 1, sy + 1);
                float ao = (p0.r + p1.r + p2.r + p3.r) * (0.25f / 255f);
                float rough0 = p0.g * (1f / 255f);
                float rough1 = p1.g * (1f / 255f);
                float rough2 = p2.g * (1f / 255f);
                float rough3 = p3.g * (1f / 255f);
                float roughMean = (rough0 + rough1 + rough2 + rough3) * 0.25f;
                float roughVariance = ((rough0 - roughMean) * (rough0 - roughMean) +
                                       (rough1 - roughMean) * (rough1 - roughMean) +
                                       (rough2 - roughMean) * (rough2 - roughMean) +
                                       (rough3 - roughMean) * (rough3 - roughMean)) * 0.25f;
                float normalVariance = HasNormalSource != 0 ? NormalVariance(sx, sy) : 0f;
                float preservedRoughness = math.saturate(math.sqrt(math.saturate((roughMean * roughMean) + roughVariance + normalVariance)));
                float metal = (p0.b + p1.b + p2.b + p3.b) * (0.25f / 255f);
                Output[index] = new Color32(ToByte(ao), ToByte(preservedRoughness), ToByte(metal), 255);
            }

            private Color32 Sample(NativeArray<Color32> source, int x, int y)
            {
                int sx = math.clamp(x, 0, math.max(0, SourceWidth - 1));
                int sy = math.clamp(y, 0, math.max(0, SourceHeight - 1));
                return source[(sy * SourceWidth) + sx];
            }

            private float NormalVariance(int sx, int sy)
            {
                float3 n0 = DecodeNormal(Sample(NormalSource, sx, sy));
                float3 n1 = DecodeNormal(Sample(NormalSource, sx + 1, sy));
                float3 n2 = DecodeNormal(Sample(NormalSource, sx, sy + 1));
                float3 n3 = DecodeNormal(Sample(NormalSource, sx + 1, sy + 1));
                float3 mean = SafeNormalize(n0 + n1 + n2 + n3, new float3(0f, 0f, 1f));
                float lengthMean = math.saturate(math.length((n0 + n1 + n2 + n3) * 0.25f));
                return (1f - lengthMean) * 0.35f + (1f - math.saturate(math.dot(mean, new float3(0f, 0f, 1f)))) * 0.08f;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct GenerateNormalMipJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Color32> Source;
            [WriteOnly, NoAlias] public NativeArray<Color32> Output;
            public int SourceWidth;
            public int SourceHeight;
            public int OutputWidth;
            public int OutputHeight;

            public void Execute(int index)
            {
                int width = math.max(1, OutputWidth);
                int x = index % width;
                int y = index / width;
                int sx = x << 1;
                int sy = y << 1;
                float3 n = DecodeNormal(Sample(sx, sy)) +
                           DecodeNormal(Sample(sx + 1, sy)) +
                           DecodeNormal(Sample(sx, sy + 1)) +
                           DecodeNormal(Sample(sx + 1, sy + 1));
                n = SafeNormalize(n, new float3(0f, 0f, 1f));
                Output[index] = EncodeNormal(n);
            }

            private Color32 Sample(int x, int y)
            {
                int sx = math.clamp(x, 0, math.max(0, SourceWidth - 1));
                int sy = math.clamp(y, 0, math.max(0, SourceHeight - 1));
                return Source[(sy * SourceWidth) + sx];
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct ExtractArmPreviewJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Color32> Source;
            [WriteOnly, NoAlias] public NativeArray<Color32> Ao;
            [WriteOnly, NoAlias] public NativeArray<Color32> Roughness;
            [WriteOnly, NoAlias] public NativeArray<Color32> Metallic;

            public void Execute(int index)
            {
                Color32 p = Source[index];
                Ao[index] = new Color32(p.r, p.r, p.r, 255);
                Roughness[index] = new Color32(p.g, p.g, p.g, 255);
                Metallic[index] = new Color32(p.b, p.b, p.b, 255);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 DecodeNormal(Color32 value)
        {
            float3 normal = new float3(value.r, value.g, value.b) * (1f / 127.5f) - 1f;
            return SafeNormalize(normal, new float3(0f, 0f, 1f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            float finite = math.all(math.isfinite(value)) && math.isfinite(lenSq) ? 1f : 0f;
            float safeLenSq = math.max(lenSq * finite, 0.0001f);
            float3 normalized = value * math.rsqrt(safeLenSq);
            return math.select(fallback, normalized, finite > 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color32 EncodeNormal(float3 normal)
        {
            return new Color32(
                ToByte(normal.x * 0.5f + 0.5f),
                ToByte(normal.y * 0.5f + 0.5f),
                ToByte(normal.z * 0.5f + 0.5f),
                255);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint x, uint y, uint seed)
        {
            uint h = seed ^ (x * 1664525u) ^ (y * 1013904223u);
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            return h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Noise(float2 value, uint seed)
        {
            int2 cell = (int2)math.floor(value);
            float2 local = math.frac(value);
            float2 u = local * local * (3f - 2f * local);
            float a = HashToFloat(cell, seed);
            float b = HashToFloat(cell + new int2(1, 0), seed);
            float c = HashToFloat(cell + new int2(0, 1), seed);
            float d = HashToFloat(cell + new int2(1, 1), seed);
            return math.lerp(math.lerp(a, b, u.x), math.lerp(c, d, u.x), u.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Fbm(float2 value, uint seed, float globalQualityWeight)
        {
            float quality = math.saturate(globalQualityWeight);
            float octave1 = math.smoothstep(0.18f, 0.70f, quality);
            float octave2 = math.smoothstep(0.48f, 1.0f, quality);
            float baseWeight = 0.55f;
            float octave1Weight = 0.30f * octave1;
            float octave2Weight = 0.15f * octave2;
            float weightSum = math.max(0.0001f, baseWeight + octave1Weight + octave2Weight);
            float sum = Noise(value, seed) * baseWeight;
            sum += Noise(value * 2.03f + 19.17f, seed ^ 0xA53Au) * octave1Weight;
            sum += Noise(value * 4.11f - 7.31f, seed ^ 0xC001u) * octave2Weight;
            return math.saturate(sum / weightSum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HashToFloat(int2 cell, uint seed)
        {
            uint h = Hash((uint)cell.x, (uint)cell.y, seed);
            return (h & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct TexturePackerTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public int Width;
        [FieldOffset(12)] public int Height;
        [FieldOffset(16)] public int PixelCount;
        [FieldOffset(20)] public int QueueIndex;
        [FieldOffset(24)] public float JobMilliseconds;
        [FieldOffset(28)] public float TotalMilliseconds;
        [FieldOffset(32)] public uint OutputHash;
        [FieldOffset(36)] public uint FaultCode;
        [FieldOffset(40)] public ulong TimestampTicks;
        [FieldOffset(48)] public ulong PathHash;
        [FieldOffset(56)] public ulong Reserved;
    }

    [InitializeOnLoad]
    internal static unsafe class TexturePackerBlackBox
    {
        internal const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_214.bin";
        internal const int RingCapacity = 300;
        private const int RingLength = RingCapacity;

        private static NativeArray<TexturePackerTelemetryEntry> _ring;
        private static int _cursor;
        private static bool _registered;

        static TexturePackerBlackBox()
        {
            RegisterLifecycle();
        }

        internal static void RecordPack(
            in TexturePackerRequest request,
            in TexturePackerRunMetrics metrics,
            int pixelCount,
            uint outputHash,
            uint faultCode)
        {
            TexturePackerTelemetryEntry entry;
            entry.FrameHash = Mix((uint)metrics.Width, (uint)metrics.Height, (uint)pixelCount, outputHash);
            entry.Flags = request.Config.Flags;
            entry.Width = metrics.Width;
            entry.Height = metrics.Height;
            entry.PixelCount = pixelCount;
            entry.QueueIndex = 0;
            entry.JobMilliseconds = ClampMilliseconds(metrics.JobMilliseconds);
            entry.TotalMilliseconds = ClampMilliseconds(metrics.TotalMilliseconds);
            entry.OutputHash = outputHash;
            entry.FaultCode = faultCode;
            entry.TimestampTicks = (ulong)DateTime.UtcNow.Ticks;
            entry.PathHash = HashString64(metrics.OutputPath);
            entry.Reserved = 0UL;
            Record(in entry);
        }

        internal static void RecordFault(
            in TexturePackerRequest request,
            in TexturePackerRunMetrics metrics,
            int pixelCount,
            uint faultCode)
        {
            TexturePackerTelemetryEntry entry;
            entry.FrameHash = Mix((uint)metrics.Width, (uint)metrics.Height, (uint)math.max(0, pixelCount), faultCode);
            entry.Flags = request.Config.Flags;
            entry.Width = metrics.Width;
            entry.Height = metrics.Height;
            entry.PixelCount = math.max(0, pixelCount);
            entry.QueueIndex = 0;
            entry.JobMilliseconds = ClampMilliseconds(metrics.JobMilliseconds);
            entry.TotalMilliseconds = ClampMilliseconds(metrics.TotalMilliseconds);
            entry.OutputHash = 0u;
            entry.FaultCode = faultCode;
            entry.TimestampTicks = (ulong)DateTime.UtcNow.Ticks;
            entry.PathHash = HashString64(metrics.OutputPath);
            entry.Reserved = HashString64(metrics.CriticalWarning);
            Record(in entry);
        }

        internal static void RecordMock(in MockBenchmarkResult result, uint flags)
        {
            TexturePackerTelemetryEntry entry;
            entry.FrameHash = Mix((uint)result.Resolution, (uint)result.PixelCount, result.OutputHash, flags);
            entry.Flags = flags;
            entry.Width = result.Resolution;
            entry.Height = result.Resolution;
            entry.PixelCount = result.PixelCount;
            entry.QueueIndex = 0;
            entry.JobMilliseconds = ClampMilliseconds(result.ElapsedMilliseconds);
            entry.TotalMilliseconds = ClampMilliseconds(result.ElapsedMilliseconds);
            entry.OutputHash = result.OutputHash;
            entry.FaultCode = 0u;
            entry.TimestampTicks = (ulong)DateTime.UtcNow.Ticks;
            entry.PathHash = 0UL;
            entry.Reserved = (ulong)math.max(0, result.ConfigSize);
            Record(in entry);
        }

        internal static void Dump(string reason)
        {
            EnsureRing();
            string directory = Path.GetDirectoryName(DumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            if (!string.IsNullOrEmpty(reason))
                File.WriteAllText(DumpPath + ".reason.txt", reason, Encoding.UTF8);

            using (FileStream stream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_ring);
                int byteCount = _ring.Length * UnsafeUtility.SizeOf<TexturePackerTelemetryEntry>();
                for (int i = 0; i < byteCount; i++)
                    stream.WriteByte(bytes[i]);
            }
        }

        private static void Record(in TexturePackerTelemetryEntry entry)
        {
            EnsureRing();
            int slot = _cursor % RingLength;
            TexturePackerTelemetryEntry copy = entry;
            copy.QueueIndex = _cursor;
            _ring[slot] = copy;
            _cursor++;
        }

        private static void EnsureRing()
        {
            RegisterLifecycle();
            if (!_ring.IsCreated)
                _ring = new NativeArray<TexturePackerTelemetryEntry>(RingLength, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<Telemetry>[300] - editor forensic ring - owner: TexturePackerBlackBox
        }

        private static void RegisterLifecycle()
        {
            if (_registered)
                return;

            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting -= Dispose;
            EditorApplication.quitting += Dispose;
            _registered = true;
        }

        private static void Dispose()
        {
            if (_ring.IsCreated)
                _ring.Dispose();
            _cursor = 0;
            _registered = false;
        }

        private static float ClampMilliseconds(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return -1f;
            double clamped = Math.Min(16777216.0, Math.Max(0.0, value));
            return (float)clamped;
        }

        private static uint Mix(uint a, uint b, uint c, uint d)
        {
            uint h = 2166136261u;
            h = (h ^ a) * 16777619u;
            h = (h ^ b) * 16777619u;
            h = (h ^ c) * 16777619u;
            h = (h ^ d) * 16777619u;
            h ^= h >> 16;
            return h;
        }

        private static ulong HashString64(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0UL;

            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }

    internal struct TexturePackerRequest
    {
        public Texture2D AoTexture;
        public Texture2D RoughnessTexture;
        public Texture2D MetallicTexture;
        public Texture2D AlbedoTexture;
        public string OutputName;
        public string OutputFolder;
        public TexturePackerConfigDTO Config;
        public int MaxSize;
        public float MacroNoiseStrength;
        public float TileSizeMeters;
        public float MacroWorldSpanMeters;
        public float GlobalQualityWeight;
        public uint Seed;
    }

    internal struct TexturePackerRunMetrics
    {
        public int ProcessedTextureCount;
        public int InputTextureCount;
        public int Width;
        public int Height;
        public long EstimatedBeforeBytes;
        public long EstimatedAfterBytes;
        public long EstimatedSavedBytes;
        public double JobMilliseconds;
        public double TotalMilliseconds;
        public string OutputPath;
        public string NormalOutputPath;
        public string OutputFormat;
        public string CriticalWarning;
    }

    internal struct MockBenchmarkResult
    {
        public int Resolution;
        public int PixelCount;
        public double ElapsedMilliseconds;
        public double EstimatedPixelsPerMillisecond;
        public int ConfigSize;
        public uint OutputHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    internal struct TexturePackingProfile
    {
        [FieldOffset(0)] public FixedString64Bytes Name;
        [FieldOffset(64)] public float NormalIntensity;
        [FieldOffset(68)] public float RoughnessScale;
        [FieldOffset(72)] public float MetallicScale;
        [FieldOffset(76)] public float MacroNoiseStrength;
        [FieldOffset(80)] public float TileSizeMeters;
        [FieldOffset(84)] public float MacroWorldSpanMeters;
        [FieldOffset(88)] public float GlobalQualityWeight;
        [FieldOffset(92)] public uint Flags;
    }
}
#endif
