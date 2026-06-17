using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Bakers
{
    /// <summary>
    /// Editor-only particulate flipbook extension. No generated pixel math is compiled into player runtime paths.
    /// </summary>
    public static partial class ProceduralTextureBaker
    {
        private const string DefaultParticleFlipbookOutputFolder1718 = "Assets/_Project/Art/Textures/VFX/ParticleFlipbooks1718";

        private const int MinimumAtlasSize = 1024;
        private const int MaximumAtlasSize = 4096;
        private const int RequiredFrameGridSize = 8;
        private const int RequiredPaddingPixels = 8;
        private const int MaxParticleFlipbookEncodedPngBytes = 192 * 1024 * 1024;
        private const int JobBatchSize = 128;
        private const float Tau = 6.28318530718f;
        private const float DensitySampleStep = 1.5f;
        private const string MarineSnowShaderName1718 = "Hecton8/VFX/MarineSnow";
        private const string SiltConeShaderName1718 = "Hecton8/VFX/FlashlightConeSilt";

        private static readonly int s_marineSnowMaskAtlasId1718 = Shader.PropertyToID("_MarineSnowMaskAtlas");
        private static readonly int s_marineSnowNormalAtlasId1718 = Shader.PropertyToID("_MarineSnowNormalAtlas");
        private static readonly int s_marineSnowAtlasParamsId1718 = Shader.PropertyToID("_MarineSnowAtlasParams");
        private static readonly int s_marineSnowFlipbookParamsId1718 = Shader.PropertyToID("_MarineSnowFlipbookParams");
        private static readonly int s_marineSnowRenderParamsId1718 = Shader.PropertyToID("_MarineSnowRenderParams");
        private static readonly int s_marineSnowTintId1718 = Shader.PropertyToID("_MarineSnowTint");
        private static readonly int s_siltMaskAtlasId1718 = Shader.PropertyToID("_SiltMaskAtlas");
        private static readonly int s_siltNormalAtlasId1718 = Shader.PropertyToID("_SiltNormalAtlas");
        private static readonly int s_siltAtlasParamsId1718 = Shader.PropertyToID("_SiltAtlasParams");
        private static readonly int s_siltFlipbookParamsId1718 = Shader.PropertyToID("_SiltFlipbookParams");
        private static readonly int s_siltBeamParamsId1718 = Shader.PropertyToID("_BeamParams");

        private enum ParticleBakeKind
        {
            SiltCloud = 0,
            MarineSnow = 1,
            CavitationBubble = 2
        }

        private readonly struct ParticleBakeProfile
        {
            public readonly string AssetName;
            public readonly ParticleBakeKind Kind;
            public readonly uint Seed;
            public readonly float GlobalQualityWeight;
            public readonly float DensityScale;
            public readonly float WorleyScale;
            public readonly float NormalStrength;
            public readonly float BiolumThreshold;
            public readonly float FlowScale;
            public readonly float ShellThickness;

            public ParticleBakeProfile(
                string assetName,
                ParticleBakeKind kind,
                uint seed,
                float globalQualityWeight,
                float densityScale,
                float worleyScale,
                float normalStrength,
                float biolumThreshold,
                float flowScale,
                float shellThickness = 0.05f)
            {
                AssetName = assetName;
                Kind = kind;
                Seed = seed;
                GlobalQualityWeight = math.saturate(globalQualityWeight);
                DensityScale = math.max(0.01f, densityScale);
                WorleyScale = math.max(0.01f, worleyScale);
                NormalStrength = math.max(0.01f, normalStrength);
                BiolumThreshold = math.saturate(biolumThreshold);
                FlowScale = math.max(0.01f, flowScale);
                ShellThickness = math.max(0.005f, shellThickness);
            }
        }

        private readonly struct ResolvedBakeSettings
        {
            public readonly int AtlasSize;
            public readonly int FrameGridSize;
            public readonly int FrameCount;
            public readonly int FrameSize;
            public readonly int PaddingPixels;
            public readonly float GlobalQualityWeight;

            public ResolvedBakeSettings(
                int atlasSize,
                int frameGridSize,
                int paddingPixels,
                float globalQualityWeight)
            {
                AtlasSize = atlasSize;
                FrameGridSize = frameGridSize;
                FrameCount = frameGridSize * frameGridSize;
                FrameSize = atlasSize / frameGridSize;
                PaddingPixels = paddingPixels;
                GlobalQualityWeight = math.saturate(globalQualityWeight);
            }
        }

        private readonly struct ParticleBakeAssetPaths
        {
            public readonly string MaskPath;
            public readonly string NormalPath;
            public readonly string MaterialPath;

            public ParticleBakeAssetPaths(string maskPath, string normalPath, string materialPath)
            {
                MaskPath = maskPath;
                NormalPath = normalPath;
                MaterialPath = materialPath;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private struct ParticleFlipbookBakeJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<Color32> PackedMask;
            [WriteOnly] public NativeArray<Color32> NormalMap;
            public int AtlasSize;
            public int FrameGridSize;
            public int FrameSize;
            public int FrameCount;
            public int PaddingPixels;
            public uint Seed;
            public int Kind;
            public float DensityScale;
            public float WorleyScale;
            public float NormalStrength;
            public float BiolumThreshold;
            public float FlowScale;
            public float ShellThickness;
            public float GlobalQualityWeight;
            public float Padding0;

            public void Execute(int index)
            {
                int atlasX = index % AtlasSize;
                int atlasY = index / AtlasSize;
                int frameX = atlasX / FrameSize;
                int frameY = atlasY / FrameSize;
                int localX = atlasX - frameX * FrameSize;
                int localY = atlasY - frameY * FrameSize;
                int frameIndex = frameY * FrameGridSize + frameX;

                bool inPadding =
                    localX < PaddingPixels ||
                    localY < PaddingPixels ||
                    localX >= FrameSize - PaddingPixels ||
                    localY >= FrameSize - PaddingPixels ||
                    frameIndex >= FrameCount;

                if (inPadding)
                {
                    PackedMask[index] = default;
                    NormalMap[index] = new Color32(128, 128, 255, 0);
                    return;
                }

                float2 uv = new float2(
                    ((localX + 0.5f) / FrameSize) * 2f - 1f,
                    ((localY + 0.5f) / FrameSize) * 2f - 1f);

                float time01 = FrameCount > 1 ? frameIndex / (float)FrameCount : 0f;
                float density = EvaluateDensity(uv, time01);
                density = math.saturate(FiniteOrZero(density));

                float2 sampleStep = new float2(DensitySampleStep / FrameSize, DensitySampleStep / FrameSize);
                float dx = FiniteOrZero(EvaluateDensity(uv + new float2(sampleStep.x, 0f), time01) -
                                        EvaluateDensity(uv - new float2(sampleStep.x, 0f), time01));
                float dy = FiniteOrZero(EvaluateDensity(uv + new float2(0f, sampleStep.y), time01) -
                                        EvaluateDensity(uv - new float2(0f, sampleStep.y), time01));
                float normalZ = Kind == (int)ParticleBakeKind.CavitationBubble ? 0.74f + density * 0.42f : 0.55f + density * 0.65f;
                float3 normal = math.normalizesafe(
                    new float3(-dx * NormalStrength, -dy * NormalStrength, math.max(normalZ, 0.18f)),
                    new float3(0f, 0f, 1f));
                normal.z = math.max(FiniteOrZero(normal.z), 0.12f);
                normal = math.normalizesafe(normal, new float3(0f, 0f, 1f));

                float highFreq = FiniteOrZero(PeriodicSimplex(
                    uv * (DensityScale * 11.0f + 17.0f),
                    time01,
                    Seed ^ 0xA3B19535u));
                float biolum = math.saturate((highFreq - BiolumThreshold) * 8.0f) * math.smoothstep(0.18f, 0.92f, density);
                if (Kind == (int)ParticleBakeKind.CavitationBubble)
                    biolum = math.max(biolum * 0.25f, math.saturate((density - 0.42f) * 1.35f));

                float flow = math.saturate(FiniteOrZero(PeriodicSimplex(
                    uv * FlowScale + new float2(11.7f, -3.9f),
                    time01,
                    Seed ^ 0xC2B2AE35u)) * 0.5f + 0.5f);
                float ao = math.saturate(math.sqrt(density) * (0.45f + density * 0.55f));

                PackedMask[index] = new Color32(
                    Pack01(density),
                    Pack01(biolum),
                    Pack01(flow),
                    Pack01(ao));

                NormalMap[index] = new Color32(
                    Pack01(normal.x * 0.5f + 0.5f),
                    Pack01(normal.y * 0.5f + 0.5f),
                    Pack01(normal.z * 0.5f + 0.5f),
                    255);
            }

            private float EvaluateDensity(float2 uv, float time01)
            {
                float radius = math.length(uv);
                float edge = 1f - math.smoothstep(0.58f, 0.96f, radius);
                if (Kind == (int)ParticleBakeKind.CavitationBubble)
                    return FiniteOrZero(EvaluateCavitationDensity(uv, time01) * edge);

                float contentRadius = Kind == (int)ParticleBakeKind.MarineSnow ? 0.76f : 0.92f;
                float silhouette = 1f - math.smoothstep(contentRadius * 0.72f, contentRadius, radius);

                float baseField = PeriodicFbm(uv * DensityScale, time01, Seed, 4);
                float stringWarp = PeriodicFbm(
                    new float2(uv.x * 0.55f + uv.y * 1.85f, uv.y * 0.33f - uv.x * 1.35f) * DensityScale,
                    time01,
                    Seed ^ 0x7FEB352Du,
                    3);
                float worley = EvaluateWorley(uv * WorleyScale, time01, Seed ^ 0x9E3779B9u);

                float density;
                if (Kind == (int)ParticleBakeKind.MarineSnow)
                {
                    float filament = math.saturate((stringWarp - 0.32f) * 1.55f);
                    float clump = math.saturate((worley * 1.2f + baseField * 0.65f - 0.58f) * 2.15f);
                    density = math.saturate((filament * 0.56f + clump * 0.88f) * silhouette * edge);
                    density = math.pow(density, math.lerp(1.35f, 0.82f, GlobalQualityWeight));
                }
                else
                {
                    float haze = math.saturate((baseField * 0.74f + (1f - worley) * 0.36f + stringWarp * 0.22f - 0.36f) * 1.85f);
                    density = haze * edge;
                    density *= math.saturate(1f - radius * 0.18f);
                    density = math.pow(math.saturate(density), math.lerp(1.18f, 0.72f, GlobalQualityWeight));
                }

                return FiniteOrZero(density);
            }

            private float EvaluateCavitationDensity(float2 uv, float time01)
            {
                float phase = time01 * Tau;
                float2 orbit = new float2(math.cos(phase + 1.7f), math.sin(phase + 1.7f)) * 0.055f;
                float2 p = uv - orbit;
                float r = math.length(p);
                float pulse = math.sin(phase);
                float pulseSquared = pulse * pulse;
                float shellRadius = math.lerp(0.22f, 0.66f, pulseSquared);
                float thickness = math.max(ShellThickness, 0.012f) * math.lerp(1.45f, 0.72f, pulseSquared);
                float shell = 1f - math.smoothstep(thickness * 0.45f, thickness, math.abs(r - shellRadius));
                float innerMist = (1f - math.smoothstep(0.0f, shellRadius, r)) * 0.18f * (1f - pulseSquared);
                float breakup = PeriodicFbm(p * (DensityScale + 3.0f), time01, Seed ^ 0x27D4EB2Fu, 3);
                float pits = EvaluateWorley(p * (WorleyScale + 4.0f), time01, Seed ^ 0x165667B1u);
                float glint = math.saturate((breakup * 0.7f + pits * 0.55f - 0.42f) * 1.7f);
                return math.saturate((shell * (0.74f + glint * 0.34f) + innerMist) * (1f - math.smoothstep(0.78f, 0.96f, r)));
            }

            private float PeriodicFbm(float2 p, float time01, uint seed, int octaves)
            {
                float amplitude = 0.5f;
                float frequency = 1f;
                float total = 0f;
                float norm = 0f;
                for (int i = 0; i < octaves; i++)
                {
                    total += (PeriodicSimplex(p * frequency, time01, seed + (uint)i * 0x85EBCA6Bu) * 0.5f + 0.5f) * amplitude;
                    norm += amplitude;
                    frequency *= 2.03125f;
                    amplitude *= 0.5f;
                }

                return norm > 0f ? total / norm : 0f;
            }

            private static float PeriodicSimplex(float2 p, float time01, uint seed)
            {
                float phase = time01 * Tau;
                float2 loop = new float2(math.cos(phase), math.sin(phase));
                float seedOffset = (seed & 0xFFFFu) * 0.0009765625f;
                return noise.snoise(new float4(
                    p.x + seedOffset,
                    p.y - seedOffset * 0.37f,
                    loop.x * 1.37f + seedOffset * 0.11f,
                    loop.y * 1.37f - seedOffset * 0.07f));
            }

            private static float EvaluateWorley(float2 p, float time01, uint seed)
            {
                float2 cellFloat = math.floor(p);
                int2 cell = (int2)cellFloat;
                float2 local = p - cellFloat;
                float best = 10f;
                float phase = time01 * Tau;
                float2 loop = new float2(math.cos(phase), math.sin(phase));

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = new float2(x, y);
                        uint h = Hash(((uint)(cell.x + x) * 0x8DA6B343u) ^ ((uint)(cell.y + y) * 0xD8163841u) ^ seed);
                        float2 basePoint = new float2(
                            ((h & 0xFFu) + 0.5f) * (1f / 256f),
                            (((h >> 8) & 0xFFu) + 0.5f) * (1f / 256f));
                        float2 orbit = new float2(
                            (((h >> 16) & 0xFFu) * (1f / 255f) - 0.5f) * loop.x,
                            (((h >> 24) & 0xFFu) * (1f / 255f) - 0.5f) * loop.y) * 0.18f;
                        float2 diff = neighbor + math.saturate(basePoint + orbit) - local;
                        best = math.min(best, math.lengthsq(diff));
                    }
                }

                return 1f - math.saturate(math.sqrt(best));
            }

            private static uint Hash(uint value)
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }

            private static float FiniteOrZero(float value)
            {
                return math.isfinite(value) ? value : 0f;
            }

            private static byte Pack01(float value)
            {
                return (byte)math.clamp(math.round(math.saturate(value) * 255f), 0f, 255f);
            }
        }

        [MenuItem("Hecton8/Bakers/1718/Bake Default Silt And Marine Snow Flipbooks", false, 207)]
        public static void BakeDefaultSiltAndMarineSnowFlipbooks()
        {
            if (!ValidateUnmanagedLayouts1718(out string layoutFailure))
            {
                UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] " + layoutFailure);
                return;
            }

            ParticleBakeProfile silt = new ParticleBakeProfile(
                "abyssal_silt_cloud",
                ParticleBakeKind.SiltCloud,
                17180001u,
                0.72f,
                2.85f,
                4.25f,
                5.5f,
                0.76f,
                1.35f);

            ParticleBakeProfile snow = new ParticleBakeProfile(
                "asymmetric_marine_snow",
                ParticleBakeKind.MarineSnow,
                17180002u,
                0.86f,
                3.65f,
                7.0f,
                7.25f,
                0.81f,
                1.9f);

            if (!TryResolveParticleBakeAssetPaths1718(in silt, DefaultParticleFlipbookOutputFolder1718, out ResolvedBakeSettings siltSettings, out ParticleBakeAssetPaths siltPaths, out string siltPathFailure, true))
            {
                UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] " + siltPathFailure);
                return;
            }

            if (!TryResolveParticleBakeAssetPaths1718(in snow, DefaultParticleFlipbookOutputFolder1718, out ResolvedBakeSettings snowSettings, out ParticleBakeAssetPaths snowPaths, out string snowPathFailure, true))
            {
                UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] " + snowPathFailure);
                return;
            }

            string[] transactionalPaths =
            {
                siltPaths.MaskPath,
                siltPaths.NormalPath,
                siltPaths.MaterialPath,
                snowPaths.MaskPath,
                snowPaths.NormalPath,
                snowPaths.MaterialPath
            };

            if (!ProceduralTextureBaker.TryCaptureAssetFileRollbackSnapshots(transactionalPaths, out ProceduralTextureBaker.AssetFileRollbackSnapshot[] outputRollback, out string rollbackFailure))
            {
                UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] Output rollback capture failed: " + rollbackFailure);
                return;
            }

            if (!TryBakeParticleFlipbookProfile1718(in silt, in siltSettings, in siltPaths))
            {
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(outputRollback);
                return;
            }

            if (!TryBakeParticleFlipbookProfile1718(in snow, in snowSettings, in snowPaths))
            {
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(outputRollback);
                return;
            }

            if (!ProceduralTextureBaker.TryFinalizeAssetDatabase("1718 particulate flipbook bake", out string finalizeFailure))
            {
                UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] " + finalizeFailure);
                ProceduralTextureBaker.TryRestoreAssetFileRollbackSnapshots(outputRollback);
                return;
            }

            UnityEngine.Debug.Log("[ParticleFlipbookBaker1718] Baked 2 particulate flipbook profile sets.");
        }

        private static bool TryResolveParticleBakeAssetPaths1718(
            in ParticleBakeProfile profile,
            string outputFolder,
            out ResolvedBakeSettings settings,
            out ParticleBakeAssetPaths paths,
            out string failure,
            bool forceRequiredFrameGrid = false)
        {
            settings = ResolveSettings(profile.GlobalQualityWeight, forceRequiredFrameGrid);
            paths = default;
            failure = string.Empty;

            int pixelCount = settings.AtlasSize * settings.AtlasSize;
            if (pixelCount <= 0)
            {
                failure = "invalid pixel count";
                return false;
            }

            if (!ProceduralTextureBaker.TryEnsureAssetFolder(outputFolder, out string normalizedOutputFolder, out string folderFailure))
            {
                failure = "output folder rejected: " + folderFailure;
                return false;
            }

            string safeAssetName = ProceduralTextureBaker.SanitizeAssetNameForPath(profile.AssetName);
            if (string.IsNullOrEmpty(safeAssetName))
                safeAssetName = "particle_flipbook_fallback";

            string maskPath = normalizedOutputFolder + "/TX_Flipbook_" + safeAssetName + "_MaskPacked.png";
            string normalPath = normalizedOutputFolder + "/TX_Flipbook_" + safeAssetName + "_Normal.png";
            string materialPath = normalizedOutputFolder + "/MAT_Flipbook_" + safeAssetName + ".mat";
            paths = new ParticleBakeAssetPaths(maskPath, normalPath, materialPath);
            return true;
        }

        private static bool TryBakeParticleFlipbookProfile1718(
            in ParticleBakeProfile profile,
            in ResolvedBakeSettings settings,
            in ParticleBakeAssetPaths paths)
        {
            int pixelCount = settings.AtlasSize * settings.AtlasSize;
            NativeArray<Color32> packedMask = default;
            NativeArray<Color32> normalMap = default;
            try
            {
                packedMask = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                normalMap = new NativeArray<Color32>(pixelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                ParticleFlipbookBakeJob job = new ParticleFlipbookBakeJob
                {
                    PackedMask = packedMask,
                    NormalMap = normalMap,
                    AtlasSize = settings.AtlasSize,
                    FrameGridSize = settings.FrameGridSize,
                    FrameSize = settings.FrameSize,
                    FrameCount = settings.FrameCount,
                    PaddingPixels = settings.PaddingPixels,
                    Seed = profile.Seed,
                    Kind = (int)profile.Kind,
                    DensityScale = profile.DensityScale,
                    WorleyScale = profile.WorleyScale,
                    NormalStrength = profile.NormalStrength,
                    BiolumThreshold = profile.BiolumThreshold,
                    FlowScale = profile.FlowScale,
                    ShellThickness = profile.ShellThickness,
                    GlobalQualityWeight = settings.GlobalQualityWeight,
                    Padding0 = 0f
                };
                JobHandle handle = job.Schedule(pixelCount, JobBatchSize);
                handle.Complete();

                if (!ValidatePixelCount(packedMask, settings.AtlasSize, out string pixelFailure))
                {
                    UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] " + pixelFailure);
                    return false;
                }

                if (!ValidatePadding(in settings, packedMask, normalMap, out string paddingFailure))
                {
                    UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] Flipbook padding violation detected! Particles will show hard edges. " + paddingFailure);
                    return false;
                }

                if (!TryWriteTexture(paths.MaskPath, packedMask, settings.AtlasSize, false))
                    return false;

                if (!TryWriteTexture(paths.NormalPath, normalMap, settings.AtlasSize, true))
                    return false;

                if (!ProceduralTextureBaker.TryEnforceTextureImportSettings(paths.MaskPath, ProceduralTextureBaker.TextureRole.Mask, settings.AtlasSize, out string maskImportFailure))
                {
                    UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] " + maskImportFailure);
                    return false;
                }

                if (!ProceduralTextureBaker.TryEnforceTextureImportSettings(paths.NormalPath, ProceduralTextureBaker.TextureRole.Normal, settings.AtlasSize, out string normalImportFailure))
                {
                    UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] " + normalImportFailure);
                    return false;
                }

                if (!TryCreateOrUpdateParticleMaterial1718(in profile, in settings, paths.MaterialPath, paths.MaskPath, paths.NormalPath, out string materialFailure))
                {
                    UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] " + materialFailure);
                    return false;
                }

                return true;
            }
            finally
            {
                if (packedMask.IsCreated)
                    packedMask.Dispose();
                if (normalMap.IsCreated)
                    normalMap.Dispose();
            }
        }

        private static bool TryCreateOrUpdateParticleMaterial1718(
            in ParticleBakeProfile profile,
            in ResolvedBakeSettings settings,
            string materialPath,
            string maskPath,
            string normalPath,
            out string failure)
        {
            failure = string.Empty;
            try
            {
                string shaderName = profile.Kind == ParticleBakeKind.SiltCloud ? SiltConeShaderName1718 : MarineSnowShaderName1718;
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    failure = "missing required shader " + shaderName;
                    return false;
                }

                Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (mask == null || normal == null)
                {
                    failure = "particle material textures missing after import: mask=" + (mask != null) + " normal=" + (normal != null);
                    return false;
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                else if (material.shader != shader)
                {
                    material.shader = shader;
                }

                float normalWeight = math.saturate(profile.NormalStrength * 0.18f);
                if (profile.Kind != ParticleBakeKind.SiltCloud)
                {
                    material.SetTexture(s_marineSnowMaskAtlasId1718, mask);
                    material.SetTexture(s_marineSnowNormalAtlasId1718, normal);
                    material.SetVector(s_marineSnowAtlasParamsId1718, new Vector4(settings.FrameGridSize, settings.FrameGridSize, normalWeight, 1f));
                    if (profile.Kind == ParticleBakeKind.CavitationBubble)
                    {
                        material.SetVector(s_marineSnowFlipbookParamsId1718, new Vector4(0.31f, 0.15f, 0.24f, 0.18f));
                        material.SetVector(s_marineSnowRenderParamsId1718, new Vector4(0.48f, math.lerp(1.6f, 2.8f, settings.GlobalQualityWeight), 18f, 0f));
                        material.SetColor(s_marineSnowTintId1718, new Color(0.62f, 0.86f, 0.94f, 0.46f));
                    }
                    else
                    {
                        material.SetVector(s_marineSnowFlipbookParamsId1718, new Vector4(0.18f, 0.15f, math.lerp(0.16f, 0.32f, settings.GlobalQualityWeight), math.lerp(0.22f, 0.48f, settings.GlobalQualityWeight)));
                        material.SetVector(s_marineSnowRenderParamsId1718, new Vector4(0.55f, math.lerp(2.4f, 4.2f, settings.GlobalQualityWeight), 18f, 0f));
                        material.SetColor(s_marineSnowTintId1718, new Color(0.50f, 0.58f, 0.53f, 0.58f));
                    }
                }
                else
                {
                    material.SetTexture(s_siltMaskAtlasId1718, mask);
                    material.SetTexture(s_siltNormalAtlasId1718, normal);
                    material.SetVector(s_siltAtlasParamsId1718, new Vector4(settings.FrameGridSize, settings.FrameGridSize, normalWeight, 1f));
                    material.SetVector(s_siltFlipbookParamsId1718, new Vector4(0.16f, 0.11f, math.lerp(0.28f, 0.62f, settings.GlobalQualityWeight), 0f));
                    material.SetVector(s_siltBeamParamsId1718, new Vector4(math.lerp(0.12f, 0.24f, settings.GlobalQualityWeight), math.lerp(2.1f, 3.2f, settings.GlobalQualityWeight), 0.42f, 2.8f));
                }

                material.name = Path.GetFileNameWithoutExtension(materialPath);
                EditorUtility.SetDirty(material);
                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                failure = "particle material creation failed for " + materialPath + ": " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static ResolvedBakeSettings ResolveSettings(float globalQualityWeight, bool forceRequiredFrameGrid)
        {
            float q = math.saturate(globalQualityWeight);
            int atlasSize = math.max(MinimumAtlasSize, ProceduralTextureBaker.ResolveSafeTextureSize(MaximumAtlasSize, q));
            atlasSize = math.clamp(atlasSize, MinimumAtlasSize, MaximumAtlasSize);
            int frameGridSize = forceRequiredFrameGrid ? RequiredFrameGridSize : ResolveFrameGridSize(q);
            return new ResolvedBakeSettings(atlasSize, frameGridSize, RequiredPaddingPixels, q);
        }

        private static int ResolveFrameGridSize(float globalQualityWeight)
        {
            return globalQualityWeight >= 0.5f ? RequiredFrameGridSize : 4;
        }

        private static bool ValidateUnmanagedLayouts1718(out string failure)
        {
            failure = string.Empty;
            int settingsBytes = UnsafeUtility.SizeOf<ResolvedBakeSettings>();
            int jobBytes = UnsafeUtility.SizeOf<ParticleFlipbookBakeJob>();
            if ((settingsBytes & 7) != 0)
            {
                failure = "ResolvedBakeSettings size is not 8-byte aligned: " + settingsBytes;
                return false;
            }

            if ((jobBytes & 7) != 0)
            {
                failure = "ParticleFlipbookBakeJob size is not 8-byte aligned: " + jobBytes;
                return false;
            }

            return true;
        }

        private static bool TryWriteTexture(string assetPath, NativeArray<Color32> pixels, int size, bool normalMap)
        {
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
                {
                    name = Path.GetFileNameWithoutExtension(assetPath),
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = normalMap ? 2 : 1
                };
                texture.SetPixelData(pixels, 0);
                texture.Apply(true, false);
                byte[] png = ImageConversion.EncodeToPNG(texture);
                if (png == null || png.Length == 0)
                {
                    UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] PNG encoding returned no bytes for " + assetPath);
                    return false;
                }

                if (png.Length > MaxParticleFlipbookEncodedPngBytes)
                {
                    UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] PNG exceeds byte ceiling for " + assetPath);
                    return false;
                }

                if (!ProceduralTextureBaker.TryWriteBytesAtomic(assetPath, png, out string writeFailure))
                {
                    UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] PNG write failed for " + assetPath + ": " + writeFailure);
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is UnityException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException)
            {
                UnityEngine.Debug.LogError("[ParticleFlipbookBaker1718] Texture write failed for " + assetPath + ": " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static bool ValidatePixelCount(NativeArray<Color32> pixels, int atlasSize, out string failure)
        {
            int expected = atlasSize * atlasSize;
            if (pixels.IsCreated && pixels.Length == expected)
            {
                failure = string.Empty;
                return true;
            }

            failure = "pixel count mismatch. actual=" + (pixels.IsCreated ? pixels.Length : 0) + " expected=" + expected;
            return false;
        }

        private static bool ValidatePadding(
            in ResolvedBakeSettings settings,
            NativeArray<Color32> packedMask,
            NativeArray<Color32> normalMap,
            out string failure)
        {
            int violations = 0;
            for (int frame = 0; frame < settings.FrameCount; frame++)
            {
                int frameX = frame % settings.FrameGridSize;
                int frameY = frame / settings.FrameGridSize;
                int originX = frameX * settings.FrameSize;
                int originY = frameY * settings.FrameSize;
                for (int y = 0; y < settings.FrameSize; y++)
                {
                    for (int x = 0; x < settings.FrameSize; x++)
                    {
                        bool padding =
                            x < settings.PaddingPixels ||
                            y < settings.PaddingPixels ||
                            x >= settings.FrameSize - settings.PaddingPixels ||
                            y >= settings.FrameSize - settings.PaddingPixels;
                        if (!padding)
                            continue;

                        int index = (originY + y) * settings.AtlasSize + originX + x;
                        Color32 p = packedMask[index];
                        Color32 n = normalMap[index];
                        if (p.r != 0 || p.g != 0 || p.b != 0 || p.a != 0)
                            violations++;
                        if (n.r != 128 || n.g != 128 || n.b != 255 || n.a != 0)
                            violations++;
                    }
                }
            }

            if (violations == 0)
            {
                failure = string.Empty;
                return true;
            }

            failure = "paddingViolations=" + violations;
            return false;
        }
    }
}
