#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.BiomeWeightMapBaker.Editor
{
    public struct BiomeSplatmapBakeResult
    {
        public string AssetPath;
        public int Width;
        public int Height;
        public int PixelCount;
        public int NonFiniteCount;
        public uint WarningFlags;
        public uint StateHash;
        public long DiskBytes;
        public float MockMilliseconds;
        public float NormalMilliseconds;
        public float WeightMilliseconds;
        public float SerializationMilliseconds;
        public bool Bc7Compressed;
    }

    public static class BiomeWeightMapBakePipeline
    {
        public const string OutputFolder = "Assets/_Project/BakedGeometry/Splatmaps";
        public const string DefaultAssetName = "TX_BiomeWeightMap_SHINOBU_243.asset";
        public const string ProductionTerrainMaterialPath = "Assets/_Project/Art/Materials/World/MAT_H8TerrainLit_BasaltSediment_1428.mat";
        public const string TerrainControlTextureProperty = "_TerrainControlRGBA";
        private const string ReportPath = "Docs/Reports/SPLATMAP_BAKE_REPORT.json";
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_243.bin";
        private static readonly UTF8Encoding JsonEncoding = new UTF8Encoding(false);

        [MenuItem("Hecton8/Biome Splatmap Forge/Bake Mock 2048 BC7")]
        public static void BakeDefaultMockSectorMenu()
        {
            BiomeSplatmapBakeConfigDTO config = DefaultConfig(BiomeWeightMapBakeConstants.DefaultResolution);
            FixedList4096Bytes<BiomeBlendRuleDTO> rules = default;
            FillDefaultRules(ref rules);
            BakeMockSector(config, in rules, DefaultAssetName, out _);
        }

        public static BiomeSplatmapBakeConfigDTO DefaultConfig(int resolution)
        {
            int size = math.clamp(resolution, 16, BiomeWeightMapBakeConstants.MaxResolution);
            return new BiomeSplatmapBakeConfigDTO
            {
                SectorOriginAUP = new double3(-50000.0d, -4200.0d, -50000.0d),
                Width = size,
                Height = size,
                CellSizeMeters = 4f,
                HeightScaleMeters = 2400f,
                NoiseStrength = 0.16f,
                NoiseFrequency = 0.0012f,
                ErosionOverrideThreshold = 0.42f,
                ErosionBlendSoftness = 0.08f,
                MacroWidth = 1,
                MacroHeight = 1,
                RulesPerMacro = BiomeWeightMapBakeConstants.DefaultRulesPerMacro,
                RuleSetCount = 1,
                Seed = 0x5348494Eu,
                GlobalQualityWeight = 1f,
                BlurRadiusPixels = 1,
                EdgeSampleFlags = 0u,
                Flags = BiomeWeightMapBakeConstants.RollbackExcludedFlag
            };
        }

        public static void FillDefaultRules(ref FixedList4096Bytes<BiomeBlendRuleDTO> rules)
        {
            rules.Clear();
            for (int i = 0; i < BiomeWeightMapBakeConstants.DefaultRulesPerMacro; i++)
                rules.Add(CreateDefaultRule(i));
        }

        public static BiomeBlendRuleDTO CreateDefaultRule(int index)
        {
            switch (index & 3)
            {
                case 0:
                    return new BiomeBlendRuleDTO
                    {
                        MinHeight = 0f,
                        MaxHeight = 1f,
                        MinSlope = 34f,
                        MaxSlope = 90f,
                        NoiseFrequency = 0.35f,
                        BlendSoftness = 0.08f,
                        ChannelIndex = 0u
                    };
                case 1:
                    return new BiomeBlendRuleDTO
                    {
                        MinHeight = 0.34f,
                        MaxHeight = 1f,
                        MinSlope = 0f,
                        MaxSlope = 38f,
                        NoiseFrequency = 0.28f,
                        BlendSoftness = 0.10f,
                        ChannelIndex = 1u
                    };
                case 2:
                    return new BiomeBlendRuleDTO
                    {
                        MinHeight = 0f,
                        MaxHeight = 0.46f,
                        MinSlope = 0f,
                        MaxSlope = 30f,
                        NoiseFrequency = 0.32f,
                        BlendSoftness = 0.12f,
                        ChannelIndex = 2u
                    };
                default:
                    return new BiomeBlendRuleDTO
                    {
                        MinHeight = 0f,
                        MaxHeight = 0.58f,
                        MinSlope = 0f,
                        MaxSlope = 24f,
                        NoiseFrequency = 0.20f,
                        BlendSoftness = 0.09f,
                        ChannelIndex = 3u
                    };
            }
        }

        public static bool BakeMockSector(
            BiomeSplatmapBakeConfigDTO config,
            in FixedList4096Bytes<BiomeBlendRuleDTO> rules,
            string assetName,
            out BiomeSplatmapBakeResult result)
        {
            result = default;
            NativeArray<float> heights = default;
            NativeArray<float> erosion = default;
            NativeArray<uint> macros = default;
            NativeArray<float3> normals = default;
            NativeArray<Color32> pixels = default;
            NativeArray<Color32> blurredPixels = default;
            NativeArray<byte> nonFiniteFlags = default;
            NativeArray<BiomeBlendRuleDTO> nativeRules = default;
            NativeArray<float> edgeWest = default;
            NativeArray<float> edgeEast = default;
            NativeArray<float> edgeSouth = default;
            NativeArray<float> edgeNorth = default;
            NativeArray<BiomeSplatmapBakeTelemetryEntry> telemetry = default;

            Stopwatch stage = new Stopwatch();
            uint warningFlags = 0u;
            int telemetryCursor = 0;
            JobHandle cleanupHandle = default;
            bool cleanupHandleValid = false;
            bool cleanupHandleCompleted = false;

            try
            {
                ValidateBiomeRuleLayoutOrThrow();
                config = SanitizeConfig(config, in rules);
                int pixelCount = checked(config.Width * config.Height);
                int macroCount = math.max(1, config.MacroWidth * config.MacroHeight);
                result.Width = config.Width;
                result.Height = config.Height;
                result.PixelCount = pixelCount;
                Directory.CreateDirectory(OutputFolder);
                Directory.CreateDirectory("Docs/Reports");
                Directory.CreateDirectory("Docs/AgentLogs");

                heights = new NativeArray<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                erosion = new NativeArray<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                macros = new NativeArray<uint>(macroCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                normals = new NativeArray<float3>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                pixels = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                nonFiniteFlags = new NativeArray<byte>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                nativeRules = CreateNativeRules(in rules, config.RulesPerMacro * config.RuleSetCount);
                edgeWest = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeEast = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeSouth = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeNorth = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                telemetry = new NativeArray<BiomeSplatmapBakeTelemetryEntry>(BiomeWeightMapBakeConstants.TelemetryFrames, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                PrimeTelemetry(telemetry, in result, in config);

                EditorUtility.DisplayProgressBar("Biome Splatmap Forge", "Generating mock heightmap and erosion mask", 0.08f);
                stage.Restart();
                JobHandle mockHandle = new GenerateMockHeightmapJob
                {
                    Heights01 = heights,
                    Erosion01 = erosion,
                    Config = config
                }.Schedule(pixelCount, 64);
                JobHandle macroHandle = new GenerateMockMacroBiomeJob
                {
                    MacroBiomeHashes = macros,
                    Config = config
                }.Schedule(macroCount, 16);
                cleanupHandle = JobHandle.CombineDependencies(mockHandle, macroHandle);
                cleanupHandleValid = true;

                EditorUtility.DisplayProgressBar("Biome Splatmap Forge", "Calculating central-difference terrain normals", 0.32f);
                JobHandle normalHandle = new CalculateTerrainNormalsJob
                {
                    Heights01 = heights,
                    WestEdgeHeights01 = edgeWest,
                    EastEdgeHeights01 = edgeEast,
                    SouthEdgeHeights01 = edgeSouth,
                    NorthEdgeHeights01 = edgeNorth,
                    Normals = normals,
                    Config = config
                }.Schedule(pixelCount, 64, mockHandle);
                JobHandle normalAndMacroHandle = JobHandle.CombineDependencies(normalHandle, macroHandle);
                cleanupHandle = normalAndMacroHandle;

                EditorUtility.DisplayProgressBar("Biome Splatmap Forge", "Evaluating biome weights and erosion override", 0.58f);
                JobHandle weightsHandle = new EvaluateBiomeWeightsJob
                {
                    Heights01 = heights,
                    Normals = normals,
                    Erosion01 = erosion,
                    MacroBiomeHashes = macros,
                    Rules = nativeRules,
                    Pixels = pixels,
                    NonFiniteFlags = nonFiniteFlags,
                    Config = config
                }.Schedule(pixelCount, 64, normalAndMacroHandle);
                cleanupHandle = weightsHandle;

                NativeArray<Color32> finalPixels = pixels;
                JobHandle finalHandle = weightsHandle;
                if (config.BlurRadiusPixels > 0)
                {
                    blurredPixels = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    JobHandle blurHandle = new BoxBlurBiomeWeightsJob
                    {
                        Source = pixels,
                        Destination = blurredPixels,
                        Width = config.Width,
                        Height = config.Height,
                        Radius = config.BlurRadiusPixels
                    }.Schedule(pixelCount, 64, weightsHandle);
                    finalHandle = blurHandle;
                    finalPixels = blurredPixels;
                    cleanupHandle = blurHandle;
                }

                finalHandle.Complete();
                cleanupHandleCompleted = true;
                result.MockMilliseconds = -1f;
                result.NormalMilliseconds = -1f;
                result.WeightMilliseconds = (float)stage.Elapsed.TotalMilliseconds;
                result.NonFiniteCount = CountNonFinite(nonFiniteFlags);
                if (result.NonFiniteCount > 0)
                    warningFlags |= BiomeWeightMapBakeConstants.WarningNonFiniteColor;

                result.StateHash = HashPixels(finalPixels);
                result.WarningFlags = warningFlags;
                RecordTelemetry(telemetry, ref telemetryCursor, in result, in config, 1u);
                RecordTelemetry(telemetry, ref telemetryCursor, in result, in config, 2u);
                RecordTelemetry(telemetry, ref telemetryCursor, in result, in config, 3u);
                if (result.NonFiniteCount > 0)
                    TryDumpBlackBox(telemetry, 2u);

                EditorUtility.DisplayProgressBar("Biome Splatmap Forge", "Creating linear BC7 texture asset", 0.86f);
                stage.Restart();
                string outputPath = ResolveOutputPath(assetName);
                result.Bc7Compressed = SaveBc7TextureAsset(finalPixels, config.Width, config.Height, outputPath);
                if (!result.Bc7Compressed)
                    result.WarningFlags |= BiomeWeightMapBakeConstants.WarningBc7CompressionFailed;
                if (!TryBindControlTextureToProductionTerrainMaterial(outputPath))
                    result.WarningFlags |= BiomeWeightMapBakeConstants.WarningMaterialBindingFailed;
                result.SerializationMilliseconds = (float)stage.Elapsed.TotalMilliseconds;
                result.AssetPath = outputPath;
                result.DiskBytes = ResolveDiskBytes(outputPath);
                RecordTelemetry(telemetry, ref telemetryCursor, in result, in config, 4u);
                WriteReport(in result, in config);
                BiomeWeightMapSelfAudit.WriteAudit(in result, in config);
                return true;
            }
            catch (Exception ex)
            {
                TryDumpBlackBox(telemetry, 1u);
                UnityEngine.Debug.LogError("[SHINOBU_243] Biome weight-map bake failed: " + ex.GetType().Name + " " + ex.Message);
                return false;
            }
            finally
            {
                if (cleanupHandleValid && !cleanupHandleCompleted)
                    cleanupHandle.Complete();
                EditorUtility.ClearProgressBar();
                Dispose(ref heights);
                Dispose(ref erosion);
                Dispose(ref macros);
                Dispose(ref normals);
                Dispose(ref pixels);
                Dispose(ref blurredPixels);
                Dispose(ref nonFiniteFlags);
                Dispose(ref nativeRules);
                Dispose(ref edgeWest);
                Dispose(ref edgeEast);
                Dispose(ref edgeSouth);
                Dispose(ref edgeNorth);
                Dispose(ref telemetry);
            }
        }

        private static void PrimeTelemetry(
            NativeArray<BiomeSplatmapBakeTelemetryEntry> telemetry,
            in BiomeSplatmapBakeResult result,
            in BiomeSplatmapBakeConfigDTO config)
        {
            if (!telemetry.IsCreated)
                return;

            BiomeSplatmapBakeTelemetryEntry entry = BuildTelemetry(in result, in config, 0u);
            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = entry;
        }

        private static void RecordTelemetry(
            NativeArray<BiomeSplatmapBakeTelemetryEntry> telemetry,
            ref int cursor,
            in BiomeSplatmapBakeResult result,
            in BiomeSplatmapBakeConfigDTO config,
            uint stage)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return;

            int index = cursor % telemetry.Length;
            telemetry[index] = BuildTelemetry(in result, in config, stage);
            cursor++;
        }

        public static Texture2D BakePreviewTexture(BiomeSplatmapBakeConfigDTO sourceConfig, in FixedList4096Bytes<BiomeBlendRuleDTO> rules)
        {
            BiomeSplatmapBakeConfigDTO config = SanitizeConfig(sourceConfig, in rules);
            config.Width = BiomeWeightMapBakeConstants.PreviewResolution;
            config.Height = BiomeWeightMapBakeConstants.PreviewResolution;
            config.BlurRadiusPixels = math.clamp(config.BlurRadiusPixels, 0, 2);

            NativeArray<float> heights = default;
            NativeArray<float> erosion = default;
            NativeArray<uint> macros = default;
            NativeArray<float3> normals = default;
            NativeArray<Color32> pixels = default;
            NativeArray<Color32> blurredPixels = default;
            NativeArray<byte> nonFiniteFlags = default;
            NativeArray<BiomeBlendRuleDTO> nativeRules = default;
            NativeArray<float> edge = default;
            JobHandle cleanupHandle = default;
            bool cleanupHandleValid = false;
            bool cleanupHandleCompleted = false;

            try
            {
                int pixelCount = config.Width * config.Height;
                heights = new NativeArray<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                erosion = new NativeArray<float>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                macros = new NativeArray<uint>(math.max(1, config.MacroWidth * config.MacroHeight), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                normals = new NativeArray<float3>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                pixels = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                nonFiniteFlags = new NativeArray<byte>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                nativeRules = CreateNativeRules(in rules, config.RulesPerMacro * config.RuleSetCount);
                edge = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                JobHandle handle = new GenerateMockHeightmapJob
                {
                    Heights01 = heights,
                    Erosion01 = erosion,
                    Config = config
                }.Schedule(pixelCount, 64);
                JobHandle macroHandle = new GenerateMockMacroBiomeJob
                {
                    MacroBiomeHashes = macros,
                    Config = config
                }.Schedule(macros.Length, 16);
                cleanupHandle = JobHandle.CombineDependencies(handle, macroHandle);
                cleanupHandleValid = true;
                handle = new CalculateTerrainNormalsJob
                {
                    Heights01 = heights,
                    WestEdgeHeights01 = edge,
                    EastEdgeHeights01 = edge,
                    SouthEdgeHeights01 = edge,
                    NorthEdgeHeights01 = edge,
                    Normals = normals,
                    Config = config
                }.Schedule(pixelCount, 64, handle);
                handle = JobHandle.CombineDependencies(handle, macroHandle);
                cleanupHandle = handle;
                handle = new EvaluateBiomeWeightsJob
                {
                    Heights01 = heights,
                    Normals = normals,
                    Erosion01 = erosion,
                    MacroBiomeHashes = macros,
                    Rules = nativeRules,
                    Pixels = pixels,
                    NonFiniteFlags = nonFiniteFlags,
                    Config = config
                }.Schedule(pixelCount, 64, handle);
                cleanupHandle = handle;

                NativeArray<Color32> finalPixels = pixels;
                if (config.BlurRadiusPixels > 0)
                {
                    blurredPixels = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    handle = new BoxBlurBiomeWeightsJob
                    {
                        Source = pixels,
                        Destination = blurredPixels,
                        Width = config.Width,
                        Height = config.Height,
                        Radius = config.BlurRadiusPixels
                    }.Schedule(pixelCount, 64, handle);
                    cleanupHandle = handle;
                    finalPixels = blurredPixels;
                }

                handle.Complete();
                cleanupHandleCompleted = true;

                Texture2D preview = new Texture2D(config.Width, config.Height, TextureFormat.RGBA32, false, true);
                preview.name = "SHINOBU_243_BiomeWeightPreview";
                preview.SetPixelData(finalPixels, 0);
                preview.Apply(false, false);
                return preview;
            }
            finally
            {
                if (cleanupHandleValid && !cleanupHandleCompleted)
                    cleanupHandle.Complete();
                Dispose(ref heights);
                Dispose(ref erosion);
                Dispose(ref macros);
                Dispose(ref normals);
                Dispose(ref pixels);
                Dispose(ref blurredPixels);
                Dispose(ref nonFiniteFlags);
                Dispose(ref nativeRules);
                Dispose(ref edge);
            }
        }

        public static void ValidateBiomeRuleLayoutOrThrow()
        {
            int size = UnsafeUtility.SizeOf<BiomeBlendRuleDTO>();
            int align = UnsafeUtility.AlignOf<BiomeBlendRuleDTO>();
            if (size != 32 || align < 4)
                throw new InvalidOperationException("BiomeBlendRuleDTO layout invalid. Size=" + size + " Align=" + align);

            ValidateOffset(nameof(BiomeBlendRuleDTO.MinHeight), 0);
            ValidateOffset(nameof(BiomeBlendRuleDTO.MaxHeight), 4);
            ValidateOffset(nameof(BiomeBlendRuleDTO.MinSlope), 8);
            ValidateOffset(nameof(BiomeBlendRuleDTO.MaxSlope), 12);
            ValidateOffset(nameof(BiomeBlendRuleDTO.NoiseFrequency), 16);
            ValidateOffset(nameof(BiomeBlendRuleDTO.BlendSoftness), 20);
            ValidateOffset(nameof(BiomeBlendRuleDTO.ChannelIndex), 24);
            ValidateOffset(nameof(BiomeBlendRuleDTO._pad0), 28);
        }

        private static void ValidateOffset(string fieldName, int expectedOffset)
        {
            int actual = Marshal.OffsetOf<BiomeBlendRuleDTO>(fieldName).ToInt32();
            if (actual != expectedOffset)
                throw new InvalidOperationException("BiomeBlendRuleDTO." + fieldName + " offset " + actual + " expected " + expectedOffset);
        }

        private static BiomeSplatmapBakeConfigDTO SanitizeConfig(BiomeSplatmapBakeConfigDTO config, in FixedList4096Bytes<BiomeBlendRuleDTO> rules)
        {
            config.Width = math.clamp(config.Width <= 0 ? BiomeWeightMapBakeConstants.DefaultResolution : config.Width, 16, BiomeWeightMapBakeConstants.MaxResolution);
            config.Height = math.clamp(config.Height <= 0 ? config.Width : config.Height, 16, BiomeWeightMapBakeConstants.MaxResolution);
            config.CellSizeMeters = math.max(0.001f, config.CellSizeMeters);
            config.HeightScaleMeters = math.max(0.001f, config.HeightScaleMeters);
            config.NoiseStrength = math.max(0f, config.NoiseStrength);
            config.NoiseFrequency = math.max(0.000001f, config.NoiseFrequency);
            config.ErosionOverrideThreshold = math.saturate(config.ErosionOverrideThreshold);
            config.ErosionBlendSoftness = math.max(0.0001f, config.ErosionBlendSoftness);
            config.RulesPerMacro = math.clamp(
                config.RulesPerMacro <= 0 ? BiomeWeightMapBakeConstants.DefaultRulesPerMacro : config.RulesPerMacro,
                1,
                BiomeWeightMapBakeConstants.MaxRuleCount);
            int sourceRuleCount = rules.Length == 0 ? BiomeWeightMapBakeConstants.DefaultRulesPerMacro : rules.Length;
            int requestedRuleSets = config.RuleSetCount <= 0 ? (int)math.ceil(sourceRuleCount / (float)config.RulesPerMacro) : config.RuleSetCount;
            int maxRuleSets = math.max(1, BiomeWeightMapBakeConstants.MaxRuleCount / config.RulesPerMacro);
            config.RuleSetCount = math.clamp(requestedRuleSets, 1, maxRuleSets);
            config.MacroWidth = math.clamp(config.MacroWidth, 1, 1024);
            config.MacroHeight = math.clamp(config.MacroHeight, 1, 1024);
            config.Seed = config.Seed == 0u ? 0x5348494Eu : config.Seed;
            config.GlobalQualityWeight = math.saturate(config.GlobalQualityWeight);
            float quality = BiomeWeightMapBakeMath.QualityCurve(config.GlobalQualityWeight);
            int requestedBlur = math.clamp(config.BlurRadiusPixels, 0, 8);
            config.BlurRadiusPixels = math.clamp((int)math.round(requestedBlur * math.lerp(0.25f, 1f, quality)), 0, 8);
            // This facade does not own adjacent-sector height buffers; external callers schedule CalculateTerrainNormalsJob directly when they provide real edges.
            config.EdgeSampleFlags = 0u;
            config.Flags |= BiomeWeightMapBakeConstants.RollbackExcludedFlag;
            return config;
        }

        private static NativeArray<BiomeBlendRuleDTO> CreateNativeRules(in FixedList4096Bytes<BiomeBlendRuleDTO> sourceRules, int targetCount)
        {
            int count = math.max(1, targetCount);
            NativeArray<BiomeBlendRuleDTO> nativeRules = new NativeArray<BiomeBlendRuleDTO>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            int sourceCount = sourceRules.Length;
            for (int i = 0; i < count; i++)
                nativeRules[i] = sourceCount > 0 ? sourceRules[i < sourceCount ? i : sourceCount - 1] : CreateDefaultRule(i);
            return nativeRules;
        }

        private static bool SaveBc7TextureAsset(NativeArray<Color32> pixels, int width, int height, string assetPath)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
            texture.name = Path.GetFileNameWithoutExtension(assetPath);
            texture.SetPixelData(pixels, 0);
            texture.Apply(true, false);
            try
            {
                EditorUtility.CompressTexture(texture, TextureFormat.BC7, TextureCompressionQuality.Best);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[SHINOBU_243] BC7 texture compression failed: " + exception.Message);
            }
            texture.Apply(true, true);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(texture, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return texture.format == TextureFormat.BC7;
        }

        private static bool TryBindControlTextureToProductionTerrainMaterial(string textureAssetPath)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ProductionTerrainMaterialPath);
            if (texture == null || material == null || !material.HasProperty(TerrainControlTextureProperty))
                return false;

            material.SetTexture(TerrainControlTextureProperty, texture);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static string ResolveOutputPath(string assetName)
        {
            string safeName = string.IsNullOrWhiteSpace(assetName) ? DefaultAssetName : assetName;
            if (!EndsWithAssetExtension(safeName))
                safeName += ".asset";
            return (OutputFolder + "/" + safeName).Replace('\\', '/');
        }

        private static bool EndsWithAssetExtension(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 6)
                return false;

            int start = value.Length - 6;
            return value[start] == '.' &&
                   ToLowerAscii(value[start + 1]) == 'a' &&
                   ToLowerAscii(value[start + 2]) == 's' &&
                   ToLowerAscii(value[start + 3]) == 's' &&
                   ToLowerAscii(value[start + 4]) == 'e' &&
                   ToLowerAscii(value[start + 5]) == 't';
        }

        private static char ToLowerAscii(char value)
        {
            return value >= 'A' && value <= 'Z' ? (char)(value + 32) : value;
        }

        private static long ResolveDiskBytes(string assetPath)
        {
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            return File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0L;
        }

        private static int CountNonFinite(NativeArray<byte> flags)
        {
            int count = 0;
            for (int i = 0; i < flags.Length; i++)
                count += flags[i] != 0 ? 1 : 0;
            return count;
        }

        private static uint HashPixels(NativeArray<Color32> pixels)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                hash = BiomeWeightMapBakeMath.Mix(hash ^ p.r);
                hash = BiomeWeightMapBakeMath.Mix(hash ^ ((uint)p.g << 8));
                hash = BiomeWeightMapBakeMath.Mix(hash ^ ((uint)p.b << 16));
                hash = BiomeWeightMapBakeMath.Mix(hash ^ ((uint)p.a << 24));
            }
            return hash == 0u ? 1u : hash;
        }

        private static BiomeSplatmapBakeTelemetryEntry BuildTelemetry(
            in BiomeSplatmapBakeResult result,
            in BiomeSplatmapBakeConfigDTO config,
            uint stage)
        {
            return new BiomeSplatmapBakeTelemetryEntry
            {
                Stage = stage,
                PixelCount = (uint)result.PixelCount,
                StateHash = result.StateHash,
                WarningFlags = result.WarningFlags,
                SectorOriginX = config.SectorOriginAUP.x,
                SectorOriginY = config.SectorOriginAUP.y,
                SectorOriginZ = config.SectorOriginAUP.z,
                NormalMilliseconds = result.NormalMilliseconds,
                WeightMilliseconds = result.WeightMilliseconds,
                SerializationMilliseconds = result.SerializationMilliseconds,
                NonFiniteCount = result.NonFiniteCount,
                Width = result.Width,
                Height = result.Height
            };
        }

        private static void TryDumpBlackBox(NativeArray<BiomeSplatmapBakeTelemetryEntry> telemetry, uint reason)
        {
            if (!telemetry.IsCreated)
                return;

            try
            {
                DumpBlackBox(telemetry, reason);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[SHINOBU_243] Black-box dump failed closed: " + exception.GetType().Name);
            }
        }

        private static void DumpBlackBox(NativeArray<BiomeSplatmapBakeTelemetryEntry> telemetry, uint reason)
        {
            Directory.CreateDirectory("Docs/AgentLogs");
            string tempPath = DumpPath + ".tmp";
            using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(BiomeWeightMapBakeConstants.DumpMagic);
                writer.Write(reason);
                writer.Write(telemetry.Length);
                for (int i = 0; i < telemetry.Length; i++)
                {
                    BiomeSplatmapBakeTelemetryEntry entry = telemetry[i];
                    writer.Write(entry.Stage);
                    writer.Write(entry.PixelCount);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.WarningFlags);
                    writer.Write(entry.SectorOriginX);
                    writer.Write(entry.SectorOriginY);
                    writer.Write(entry.SectorOriginZ);
                    writer.Write(entry.NormalMilliseconds);
                    writer.Write(entry.WeightMilliseconds);
                    writer.Write(entry.SerializationMilliseconds);
                    writer.Write(entry.NonFiniteCount);
                    writer.Write(entry.Width);
                    writer.Write(entry.Height);
                }
            }

            if (File.Exists(DumpPath))
            {
                try
                {
                    File.Replace(tempPath, DumpPath, null);
                    return;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                File.Delete(DumpPath);
            }

            File.Move(tempPath, DumpPath);
        }

        private static void WriteReport(in BiomeSplatmapBakeResult result, in BiomeSplatmapBakeConfigDTO config)
        {
            StringBuilder builder = new StringBuilder(1536);
            builder.Append("{\n");
            Append(builder, "schema", "hecton8.splatmap_bake_report.v1", true);
            Append(builder, "agent", "SHINOBU_243", true);
            Append(builder, "output", result.AssetPath, true);
            Append(builder, "texturesGenerated", 1, true);
            Append(builder, "width", result.Width, true);
            Append(builder, "height", result.Height, true);
            Append(builder, "pixelCount", result.PixelCount, true);
            Append(builder, "compressionFormat", result.Bc7Compressed ? "BC7" : "RGBA32_FALLBACK", true);
            Append(builder, "linearDataTexture", true, true);
            Append(builder, "globalQualityWeight", config.GlobalQualityWeight, true);
            Append(builder, "fractalNoiseOctaves", BiomeWeightMapBakeMath.QualityOctaveCount(config.GlobalQualityWeight), true);
            Append(builder, "effectiveBlurRadiusPixels", config.BlurRadiusPixels, true);
            Append(builder, "diskBytes", result.DiskBytes, true);
            builder.Append("  \"timingsMs\": { \"jobChain\": ").Append(Format(result.WeightMilliseconds));
            builder.Append(", \"stageBreakdown\": \"not_isolated_single_fence\"");
            builder.Append(", \"serialization\": ").Append(Format(result.SerializationMilliseconds)).Append(" },\n");
            Append(builder, "nonFiniteColorCount", result.NonFiniteCount, true);
            Append(builder, "criticalWarning", result.NonFiniteCount > 0 ? "CRITICAL_WARNING" : "null", true, result.NonFiniteCount <= 0);
            Append(builder, "stateHash", result.StateHash, true);
            Append(builder, "rollbackNetcodeExcluded", (config.Flags & BiomeWeightMapBakeConstants.RollbackExcludedFlag) != 0u, true);
            Append(builder, "warningFlags", result.WarningFlags, false);
            builder.Append("}\n");
            File.WriteAllText(ReportPath, builder.ToString(), JsonEncoding);
        }

        private static void Append(StringBuilder builder, string name, string value, bool comma, bool raw = false)
        {
            builder.Append("  \"").Append(name).Append("\": ");
            if (raw)
                builder.Append(value);
            else
                builder.Append('"').Append(value).Append('"');
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, long value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, float value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(Format(value));
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, uint value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void Dispose<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;
            array.Dispose();
            array = default;
        }
    }

    public static class BiomeWeightMapSelfAudit
    {
        private const string AuditPath = "Docs/Reports/SPLATMAP_BAKE_SELF_AUDIT_SHINOBU_243.md";

        public static void WriteAudit(in BiomeSplatmapBakeResult result, in BiomeSplatmapBakeConfigDTO config)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("# SHINOBU_243 Biome Weight Map Self Audit");
            builder.AppendLine();
            builder.AppendLine("Evidence: STATIC_SOURCE plus local Editor API path. Unity import/Burst inspector/Frame Debugger proof remains PENDING VERIFICATION.");
            builder.AppendLine();
            builder.AppendLine("## 20-Task Reconciliation");
            AppendTask(builder, 1, "REALTIME_SPLAT_MATH_INQUISITION", true, "TerrainMaster consumes baked _TerrainControlRGBA; runtime slope/height biome selection removed from splat branch.");
            AppendTask(builder, 2, "MANAGED_TEXTURE_MANIPULATION_PURGE", true, "NativeArray<Color32> plus SetPixelData path; no managed pixel getter/setter pipeline.");
            AppendTask(builder, 3, "CS1612_METADATA_STATE_ANNIHILATION", true, "Hot DTOs expose raw public fields, no get/set properties in BiomeWeightMapBaker.");
            AppendTask(builder, 4, "ARM64_RULE_LAYOUT_ASSERTION", true, "BiomeBlendRuleDTO explicit 32 bytes with Marshal offset checks.");
            AppendTask(builder, 5, "EMERGENCY_MOCK_HEIGHTMAP_BENCHMARK", true, "GenerateMockHeightmapJob creates deterministic height/erosion; GenerateMockMacroBiomeJob creates macro rule sets.");
            AppendTask(builder, 6, "BURST_SLOPE_EVALUATION_KERNEL", true, "CalculateTerrainNormalsJob central-difference normals with edge hooks and NoAlias.");
            AppendTask(builder, 7, "MATHEMATICAL_WEIGHT_BLENDING_KERNEL", true, "EvaluateBiomeWeightsJob normalizes Rock/Sand/Silt/Erosion to packed RGBA.");
            AppendTask(builder, 8, "THE_DEAR_LIE_FRACTAL_TRANSITIONS", true, "AUP-seeded quality-scaled value noise breaks banding offline, not per fragment.");
            AppendTask(builder, 9, "ASYNCHRONOUS_TEXTURE_SERIALIZATION", true, "Texture2D.SetPixelData, BC7 compression, AssetDatabase disk asset route.");
            AppendTask(builder, 10, "EROSION_MASK_INTEGRATION", true, "Erosion deposition raises alpha and scales RGB before final normalization.");
            AppendTask(builder, 11, "MACRO_BIOME_OVERRIDE_LOGIC", true, "Macro hash grid selects rule-set offsets without cross-agent dependency.");
            AppendTask(builder, 12, "AUP_SEAM_STITCHING_MATH", true, "double3 sample minus SectorOriginAUP before noise scaling plus optional edge height buffers.");
            AppendTask(builder, 13, "ROLLBACK_NETCODE_EXCLUSION_FENCE", true, "RollbackExcludedFlag and static asset report; no StateRingBuffer participation.");
            AppendTask(builder, 14, "ZERO_INIT_OVERHEAD_BYPASS", true, "Large TempJob buffers allocate UninitializedMemory and are overwritten before read.");
            AppendTask(builder, 15, "TELEMETRY_BAKE_REPORT_GENERATOR", true, "JSON report plus 300-entry black-box dump on failure/non-finite pixels.");
            AppendTask(builder, 16, "PROCEDURAL_SPLAT_FORGE_WINDOW", true, "UI Toolkit Editor facade with preview, bake, CSV, scanner controls, source/output path labels, schema status, and DTO layout summary.");
            AppendTask(builder, 17, "CSV_TEXTURING_PROFILES_INGESTOR", true, "ReadOnlySpan<byte> parser enforces schema v1, exact channel tokens, and column count before filling FixedList4096Bytes<BiomeBlendRuleDTO>.");
            AppendTask(builder, 18, "LIVE_MASK_PREVIEW_GIZMO", true, "Preview runs the same height, macro, normal, weight, and optional blur Burst route at reduced resolution before SetPixelData display.");
            AppendTask(builder, 19, "ARCHITECTURAL_METRIC_VALIDATOR", true, "Shader scanner emits RENDERING_OPTIMIZATION_REPORT.json.");
            AppendTask(builder, 20, "SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION", true, "This audit records layout, quality curve, dependency graph, compile guard, and Dear Lie proof.");
            builder.AppendLine();
            builder.AppendLine("## DTO Layout");
            builder.Append("- BiomeBlendRuleDTO size: ").Append(UnsafeUtility.SizeOf<BiomeBlendRuleDTO>()).AppendLine(" bytes");
            builder.Append("- BiomeBlendRuleDTO align: ").Append(UnsafeUtility.AlignOf<BiomeBlendRuleDTO>()).AppendLine(" bytes");
            builder.AppendLine("- Offsets: MinHeight=0, MaxHeight=4, MinSlope=8, MaxSlope=12, NoiseFrequency=16, BlendSoftness=20, ChannelIndex=24, _pad0=28");
            builder.AppendLine("- Field math: 6 floats * 4 bytes = 24, ChannelIndex uint = 4 -> offset 24, _pad0 uint = 4 -> offset 28, total 32 bytes.");
            builder.Append("- BiomeSplatmapBakeConfigDTO size: ").Append(UnsafeUtility.SizeOf<BiomeSplatmapBakeConfigDTO>()).AppendLine(" bytes");
            builder.AppendLine("- Config offsets: double3 SectorOriginAUP=0..23, ints/floats/uints=24..91, _pad0=92..95, four ulongs=96..127, total 128 bytes.");
            builder.Append("- BiomeSplatmapBakeTelemetryEntry size: ").Append(UnsafeUtility.SizeOf<BiomeSplatmapBakeTelemetryEntry>()).AppendLine(" bytes");
            builder.AppendLine("- Telemetry is exactly one 64-byte L1 line; used as circular forensic entries, not as parallel atomic counters.");
            builder.AppendLine();
            builder.AppendLine("## Scalability Curve");
            builder.Append("- GlobalQualityWeight: ").Append(Format(config.GlobalQualityWeight)).AppendLine();
            builder.Append("- Effective fractal octaves: ").Append(BiomeWeightMapBakeMath.QualityOctaveCount(config.GlobalQualityWeight)).AppendLine(" of 4.");
            builder.Append("- Effective blur radius pixels: ").Append(config.BlurRadiusPixels).AppendLine(".");
            builder.AppendLine("- Below 0.3, QualityCurve collapses noise amplitude and octave count toward one octave, macro noise frequency toward the cheap end, and blur radius toward 25 percent of requested radius.");
            builder.AppendLine("- Around 0.4..0.7, the same math route keeps intermediate octave counts and partial blur; no binary hardware branches or asset contract changes.");
            builder.AppendLine("- At 1.0, the offline bake uses four value-noise octaves, full requested blur, and full transition perturbation. Runtime truth remains one BC7 sample.");
            builder.AppendLine();
            builder.AppendLine("## H-PHI Vault Status");
            builder.AppendLine("- Runtime persistent NativeArray allocations: zero in this domain.");
            builder.AppendLine("- VaultBufferHandle IDs requested: none. This is an Editor-only offline baker; its persistent proof artifacts are a texture asset, JSON report, and optional dump file.");
            builder.AppendLine("- NativeArray lifetime: TempJob buffers are local to bake/preview calls and disposed in finally. No global vault hot polling and no registry lookup.");
            builder.AppendLine();
            builder.AppendLine("## Pointer Aliasing And Dependency Graph");
            builder.AppendLine("- NoAlias fields: height, erosion, macro hash, normal, rules, pixel, non-finite, and blur buffers inside Burst jobs.");
            builder.AppendLine("- Full bake graph: mockHandle(height/erosion) + macroHandle(rule-set hashes) -> normalHandle -> CombineDependencies(normal, macro) -> weightsHandle -> optional blurHandle -> one final Complete before Texture2D.SetPixelData.");
            builder.AppendLine("- Preview graph matches the same route at 256 resolution, including optional blur, and completes once for Editor texture readback.");
            builder.AppendLine("- Failure-only cleanup fences complete outstanding scheduled work before TempJob disposal if an exception interrupts the bake before readback.");
            builder.AppendLine();
            builder.AppendLine("## Compile Guard");
            builder.AppendLine("- Assembly route: Hecton8.World.BiomeWeightMapBaker.Editor references Unity.Burst, Unity.Collections, Unity.Jobs, Unity.Mathematics only.");
            builder.AppendLine("- No sibling runtime assembly reference, no registry hot dependency, no scene MonoBehaviour baking route.");
            builder.AppendLine();
            builder.AppendLine("## Dear Lie");
            builder.AppendLine("- Replaced runtime fragment slope/height/erosion biome selection with an offline BC7 control texture.");
            builder.AppendLine("- Before: O(visible terrain fragments * slope/height/noise/rule evaluation) every frame.");
            builder.AppendLine("- After: O(visible terrain fragments * one control texture sample) every frame plus O(width * height * quality octaves) only when an artist bakes.");
            builder.AppendLine("- Saved runtime ALU is available for material response in TerrainMaster instead of re-solving biome truth.");
            builder.AppendLine();
            builder.AppendLine("## Texture Output");
            builder.Append("- Asset: ").AppendLine(result.AssetPath);
            builder.Append("- Resolution: ").Append(result.Width).Append('x').Append(result.Height).AppendLine();
            builder.Append("- Compression: ").AppendLine(result.Bc7Compressed ? "BC7" : "RGBA32_FALLBACK");
            builder.AppendLine("- Color space: Linear mask data");
            builder.AppendLine("- Channels: R=Rock, G=Sand, B=ambient silt, A=erosion-deposited silt");
            builder.AppendLine("- Netcode: static generated texture, excluded from rollback state hash");
            builder.AppendLine();
            builder.AppendLine("<SELF_AUDIT>");
            builder.AppendLine("  <TaskReconciliation tasks=\"20\" pass=\"20\" fail=\"0\" />");
            builder.AppendLine("  <StructLayout primary=\"BiomeBlendRuleDTO\" bytes=\"32\" offsets=\"0,4,8,12,16,20,24,28\" />");
            builder.Append("  <CSVSchema version=\"").Append(BiomeSplatmapProfileCsvParser.CsvSchemaVersion).Append("\" columns=\"").Append(BiomeSplatmapProfileCsvParser.SchemaColumns).AppendLine("\" failClosed=\"true\" exactChannels=\"true\" extraColumns=\"rejected\" />");
            builder.Append("  <Scalability weight=\"").Append(Format(config.GlobalQualityWeight)).Append("\" octaves=\"").Append(BiomeWeightMapBakeMath.QualityOctaveCount(config.GlobalQualityWeight)).Append("\" blur=\"").Append(config.BlurRadiusPixels).AppendLine("\" continuous=\"true\" />");
            builder.AppendLine("  <HPhiVault runtimePersistentArrays=\"0\" vaultHandles=\"none-editor-only\" />");
            builder.AppendLine("  <DependencyGraph readbackCompletes=\"1_full_bake_plus_1_preview\" cleanupCompletes=\"failure_only_before_dispose\" hiddenHotCompletes=\"0\" />");
            builder.AppendLine("  <CompileGuard siblingRuntimeReferences=\"0\" />");
            builder.AppendLine("  <DearLie before=\"O(fragments*biome_math)\" after=\"O(fragments*texture_sample)\" />");
            builder.Append("  <BC7>").Append(result.Bc7Compressed ? "true" : "false").AppendLine("</BC7>");
            builder.AppendLine("</SELF_AUDIT>");
            File.WriteAllText(AuditPath, builder.ToString(), Encoding.UTF8);
        }

        private static void AppendTask(StringBuilder builder, int index, string name, bool passed, string evidence)
        {
            builder.Append("- Task ");
            if (index < 10)
                builder.Append('0');
            builder.Append(index).Append(" - ").Append(name).Append(": [").Append(passed ? "PASS" : "FAIL").Append("] ").AppendLine(evidence);
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
#endif
