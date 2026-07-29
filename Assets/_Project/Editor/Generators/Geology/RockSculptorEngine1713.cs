using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Editor.ColliderOptimization1716;
using Hecton8.Editor.GeologyForge;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Generators.Geology
{
    public sealed class RockSculptorEngine1713 : EditorWindow
    {
        private const int BlackBoxFrameCount = 300;
        private const int MinimumResolution = 12;
        private const int MaximumResolution = 48;
        private const int DefaultResolution = 32;
        private const int MaxCollisionTriangles = 200;
        private const int Lod1TriangleBudget = 7000;
        private const int Lod2TriangleBudget = 1200;
        private const byte FacePositiveX = 1 << 0;
        private const byte FaceNegativeX = 1 << 1;
        private const byte FacePositiveY = 1 << 2;
        private const byte FaceNegativeY = 1 << 3;
        private const byte FacePositiveZ = 1 << 4;
        private const byte FaceNegativeZ = 1 << 5;
        private const string MeshOutputFolder = GeologyForgeConstants.MeshOutputFolder + "/RockSculptor1713";
        private const string PrefabOutputFolder = GeologyForgeConstants.PrefabOutputFolder + "/RockSculptor1713";
        private const string DumpPath = "Docs/AgentLogs/Dump_1713.bin";
        private const int RockVertexStrideBytes = 40;
        private const int SculptTelemetryEntryStrideBytes = 24;
        private const int QuadricErrorStrideBytes = 40;
        private const int EdgeCollapseCandidateStrideBytes = 32;
        private const int MaxQemCollapsePasses = 8;
        private const float AtlasUvBleedGuard01 = 0.0078125f;
        private const int MaxBatchVariants = 64;
        private const int MaxBatchAssetIdStemLength = 56;
        private const float BatchVariantSilhouetteSpread01 = 0.22f;
        private const float BatchVariantStrataSpread01 = 0.35f;
        private const string AssetIdArgument = "-h8RockAssetId";
        private const string VariantsArgument = "-h8RockVariants";
        private const string SeedArgument = "-h8RockSeed";
        private const string ResolutionArgument = "-h8RockResolution";
        private const string RadiusArgument = "-h8RockRadius";
        private const string HeightArgument = "-h8RockHeight";
        private const string NoiseAmplitudeArgument = "-h8RockNoiseAmplitude";
        private const string StrataFrequencyArgument = "-h8RockStrataFrequency";
        private const string QualityArgument = "-h8RockQuality";
        private const string MaterialArgument = "-h8RockMaterial";
        private const StaticEditorFlags GeneratedRockRootStaticFlags =
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.OccludeeStatic;

        // COLD ALLOC: int[36] - reusable convex box collision index template - owner: RockSculptorEngine1713
        private static readonly int[] s_collisionBoxIndices =
        {
            0, 2, 3, 0, 3, 1,
            4, 5, 7, 4, 7, 6,
            0, 1, 5, 0, 5, 4,
            2, 6, 7, 2, 7, 3,
            0, 4, 6, 0, 6, 2,
            1, 3, 7, 1, 7, 5
        };

        [SerializeField] private string _assetId = "1713";
        [SerializeField] private int _resolution = DefaultResolution;
        [SerializeField] private float _radiusMeters = 8f;
        [SerializeField] private float _heightMeters = 13f;
        [SerializeField] private float _noiseAmplitudeMeters = 1.4f;
        [SerializeField] private float _strataFrequency = 11f;
        [SerializeField] private uint _seed = 1713u;
        [SerializeField] private float _globalQualityWeight = 0.6f;
        [SerializeField] private Material _triplanarMaterial;

        [MenuItem("Hecton8/Geology Forge/Rock Sculptor Engine 1713", false, 179)]
        public static void Open()
        {
            GetWindow<RockSculptorEngine1713>("Rock Sculptor 1713");
        }

        /// <summary>
        /// Batchmode bake entry point. Wrapper, not a second pipeline: it builds one
        /// <see cref="SculptSettings"/> per variant through the same static
        /// <c>BuildSettings</c> overload the window uses, then calls the same
        /// <see cref="Bake"/> body the "Bake Static Rock Prefab" button calls.
        /// <para>
        /// <see cref="Open"/> is itself reachable by <c>-executeMethod</c> - it is public, static and
        /// parameterless - but it bakes nothing, because the only call to <see cref="BakeSelected"/>
        /// is the GUI button inside <see cref="OnGUI"/>, and OnGUI never repaints under
        /// <c>-batchmode</c>. That is why this method exists.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Unity.exe -projectPath &lt;project&gt; -batchmode -quit
        ///   -executeMethod Hecton8.Editor.Generators.Geology.RockSculptorEngine1713.BakeFromCommandLine
        ///   [-h8RockAssetId id] [-h8RockVariants 1..64] [-h8RockSeed uint]
        ///   [-h8RockResolution 12..48] [-h8RockRadius m] [-h8RockHeight m]
        ///   [-h8RockNoiseAmplitude m] [-h8RockStrataFrequency v] [-h8RockQuality 0..1]
        ///   [-h8RockMaterial Assets/.../MAT_Something.mat]
        /// <para>
        /// The sculpt path is Burst/CPU only - no compute shader, no Graphics.Blit, no RenderTexture -
        /// so it does not hit the zero-return trap that makes the MapMagic batchmode protocol ban
        /// <c>-nographics</c>. Prefab save and convex MeshCollider cooking are both CPU work too.
        /// </para>
        /// <para>
        /// A malformed argument throws instead of silently falling back to the default, and any failed
        /// variant makes the whole call throw after the summary, so the run exits non-zero rather than
        /// reporting a silent success with no assets on disk.
        /// </para>
        /// </remarks>
        public static void BakeFromCommandLine()
        {
            string assetIdStem = SanitizeBatchAssetIdStem(ReadStringArgument(AssetIdArgument, "1713"));
            int variants = math.clamp(ReadIntArgument(VariantsArgument, 1), 1, MaxBatchVariants);
            uint seed = ReadSeedArgument(SeedArgument, 1713u);
            int resolution = ReadIntArgument(ResolutionArgument, DefaultResolution);
            float radiusMeters = ReadFloatArgument(RadiusArgument, 8f);
            float heightMeters = ReadFloatArgument(HeightArgument, 13f);
            float noiseAmplitudeMeters = ReadFloatArgument(NoiseAmplitudeArgument, 1.4f);
            float strataFrequency = ReadFloatArgument(StrataFrequencyArgument, 11f);
            float globalQualityWeight = ReadFloatArgument(QualityArgument, 0.6f);
            Material material = ResolveBatchMaterial(ReadStringArgument(MaterialArgument, string.Empty));

            int bakedCount = 0;
            int failedCount = 0;
            for (int variant = 0; variant < variants; variant++)
            {
                // Variation is a named seed, not hidden chance (PROCEDURAL_ASSET_PIPELINE.md
                // "Deterministic Source Contract"). Variant 0 reproduces the requested parameters
                // exactly so a single-variant batch bake equals a single window bake.
                float silhouetteScale = ResolveVariantScale(seed, variant, BatchVariantSilhouetteSpread01);
                float strataScale = ResolveVariantScale(seed ^ 0x85EBCA6Bu, variant, BatchVariantStrataSpread01);
                SculptSettings settings = BuildSettings(
                    variants > 1 ? assetIdStem + "_v" + variant.ToString(CultureInfo.InvariantCulture) : assetIdStem,
                    resolution,
                    radiusMeters * silhouetteScale,
                    heightMeters * silhouetteScale,
                    noiseAmplitudeMeters,
                    strataFrequency * strataScale,
                    unchecked(seed + ((uint)variant * 0x9E3779B9u)),
                    globalQualityWeight,
                    material);

                try
                {
                    Bake(settings);
                    bakedCount++;
                }
                catch (Exception ex)
                {
                    failedCount++;
                    Debug.LogError(string.Format(
                        CultureInfo.InvariantCulture,
                        "[H8_ROCK1713] variant={0} assetId={1} seed={2} failed: {3}",
                        variant,
                        settings.AssetId,
                        settings.Seed,
                        ex.Message));
                }
            }

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_ROCK1713] batch bake finished baked={0} failed={1} requested={2} seed={3} resolution={4} quality={5:F3} meshFolder={6} prefabFolder={7} material={8}",
                bakedCount,
                failedCount,
                variants,
                seed,
                resolution,
                globalQualityWeight,
                MeshOutputFolder,
                PrefabOutputFolder,
                material != null ? material.name : "NONE_DEFAULT_MATERIAL_FALLBACK"));

            if (failedCount > 0)
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "RockSculptorEngine1713 batch bake failed for {0} of {1} variants.",
                    failedCount,
                    variants));
        }

        private static Material ResolveBatchMaterial(string materialAssetPath)
        {
            if (!string.IsNullOrWhiteSpace(materialAssetPath))
            {
                Material requested = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                if (requested == null)
                    throw new InvalidOperationException("Rock material asset not found: " + materialAssetPath);

                return requested;
            }

            Debug.LogWarning(
                "[H8_ROCK1713] no " + MaterialArgument + " argument, so LOD renderers fall back to Unity Default-Material. " +
                "That is a Built-in RP material and is not a valid URP triplanar rock surface under 3DMODEL_TEXTURES_MATERIALS.md. " +
                "The mesh/LOD/collider package is still produced; its visual state stays PENDING VERIFICATION until a triplanar material is bound.");
            return null;
        }

        private static string SanitizeBatchAssetIdStem(string assetId)
        {
            string sanitized = SanitizeAssetId(assetId);
            if (sanitized.Length <= MaxBatchAssetIdStemLength)
                return sanitized;

            // SanitizeAssetId caps ids at 64 chars, so the stem is truncated here to leave room for the
            // "_v<index>" suffix. Without this, two long ids would sanitize to the same name and the
            // second variant would silently overwrite the first.
            return SanitizeAssetId(sanitized.Substring(0, MaxBatchAssetIdStemLength));
        }

        private static float ResolveVariantScale(uint seed, int variant, float spread01)
        {
            if (variant <= 0)
                return 1f;

            uint hash = math.hash(new uint2(seed, (uint)variant));
            float unit = (hash & 0xFFFFu) * (1f / 65535f);
            return 1f + (((unit * 2f) - 1f) * spread01);
        }

        private static bool TryReadArgumentValue(string argumentName, out string value)
        {
            // Fully qualified: this file sits under the Hecton8 namespace root, which contains a
            // Hecton8.Environment namespace that shadows System.Environment during name lookup.
            // A bare `Environment` here is CS0234.
            string[] arguments = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (!string.Equals(arguments[i], argumentName, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = arguments[i + 1];
                return !string.IsNullOrWhiteSpace(value);
            }

            value = string.Empty;
            return false;
        }

        private static string ReadStringArgument(string argumentName, string fallbackValue)
        {
            return TryReadArgumentValue(argumentName, out string value) ? value.Trim() : fallbackValue;
        }

        private static int ReadIntArgument(string argumentName, int fallbackValue)
        {
            if (!TryReadArgumentValue(argumentName, out string value))
                return fallbackValue;

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                return parsed;

            throw new InvalidOperationException("Rock sculptor argument " + argumentName + " is not an integer: " + value);
        }

        private static float ReadFloatArgument(string argumentName, float fallbackValue)
        {
            if (!TryReadArgumentValue(argumentName, out string value))
                return fallbackValue;

            // InvariantCulture is mandatory: this workstation runs a comma-decimal locale, and a
            // culture-sensitive parse would reject "0.6" and silently bake the default quality.
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && float.IsFinite(parsed))
                return parsed;

            throw new InvalidOperationException("Rock sculptor argument " + argumentName + " is not a finite number: " + value);
        }

        private static uint ReadSeedArgument(string argumentName, uint fallbackValue)
        {
            if (!TryReadArgumentValue(argumentName, out string value))
                return fallbackValue;

            if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed))
                return math.max(1u, parsed);

            throw new InvalidOperationException("Rock sculptor argument " + argumentName + " is not an unsigned integer: " + value);
        }

        private void OnGUI()
        {
            _assetId = EditorGUILayout.TextField("Asset ID", _assetId);
            _resolution = EditorGUILayout.IntSlider("SDF Resolution", _resolution, MinimumResolution, MaximumResolution);
            _radiusMeters = EditorGUILayout.Slider("Radius Meters", _radiusMeters, 2f, 32f);
            _heightMeters = EditorGUILayout.Slider("Height Meters", _heightMeters, 2f, 48f);
            _noiseAmplitudeMeters = EditorGUILayout.Slider("Noise Amplitude", _noiseAmplitudeMeters, 0f, 6f);
            _strataFrequency = EditorGUILayout.Slider("Strata Frequency", _strataFrequency, 1f, 32f);
            _seed = (uint)Mathf.Max(1, EditorGUILayout.IntField("Seed", unchecked((int)_seed)));
            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);
            _triplanarMaterial = (Material)EditorGUILayout.ObjectField("Triplanar Material", _triplanarMaterial, typeof(Material), false);

            if (GUILayout.Button("Bake Static Rock Prefab"))
                BakeSelected();
        }

        private void BakeSelected()
        {
            Bake(BuildSettings());
        }

        private static void Bake(SculptSettings settings)
        {
            ValidateUnmanagedLayouts();
            NativeArray<float> sdf = default;
            NativeArray<float> erodedSdf = default;
            NativeArray<RockVertex> vertices = default;
            NativeArray<int> indices = default;
            NativeArray<SculptTelemetryEntry> telemetry = default;
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                telemetry = new NativeArray<SculptTelemetryEntry>(BlackBoxFrameCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                int voxelCount = settings.Resolution * settings.Resolution * settings.Resolution;
                sdf = new NativeArray<float>(voxelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                erodedSdf = new NativeArray<float>(voxelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                var sdfJob = new PopulateSdfJob
                {
                    Sdf = sdf,
                    Resolution = settings.Resolution,
                    RadiusMeters = settings.RadiusMeters,
                    HeightMeters = settings.HeightMeters,
                    NoiseAmplitudeMeters = settings.NoiseAmplitudeMeters,
                    StrataFrequency = settings.StrataFrequency,
                    Seed = settings.Seed
                };
                JobHandle sdfHandle = sdfJob.Schedule(voxelCount, 64);

                var erosionJob = new HydraulicErosionJob
                {
                    SourceSdf = sdf,
                    OutputSdf = erodedSdf,
                    Resolution = settings.Resolution,
                    DropletEquivalentIterations = settings.ErosionDrops,
                    ErosionStrength = math.lerp(0.08f, 0.34f, settings.GlobalQualityWeight),
                    SedimentStrength = math.lerp(0.025f, 0.11f, settings.GlobalQualityWeight),
                    Seed = settings.Seed ^ 0x9E3779B9u
                };
                JobHandle erosionHandle = erosionJob.Schedule(voxelCount, 64, sdfHandle);
                erosionHandle.Complete();
                RecordTelemetry(telemetry, 0, 1u, voxelCount, 0, 0, settings.Seed);

                BuildVoxelSurface(settings, erodedSdf, out vertices, out indices, out int vertexCount, out int indexCount, telemetry);
                Bounds bounds = ValidateAndResolveBounds(vertices, indices, vertexCount, indexCount);
                Mesh lod0 = CreateMesh("GEN_Rock_" + settings.AssetId + "_LOD0", vertices, indices, vertexCount, indexCount, bounds);
                Mesh lod1 = CreateBudgetMesh("GEN_Rock_" + settings.AssetId + "_LOD1", vertices, indices, vertexCount, indexCount, bounds, Lod1TriangleBudget);
                Mesh lod2 = CreateBudgetMesh("GEN_Rock_" + settings.AssetId + "_LOD2", vertices, indices, vertexCount, indexCount, bounds, Lod2TriangleBudget);
                Mesh collision = CreateCollisionProxyMesh("COL_GEN_Rock_" + settings.AssetId, bounds);
                RecordTelemetry(telemetry, 1, 2u, vertexCount, indexCount / 3, (int)(collision.GetIndexCount(0) / 3u), settings.Seed);

                SaveAssets(settings, lod0, lod1, lod2, collision, bounds, stopwatch.Elapsed.TotalMilliseconds, out string prefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("RockSculptorEngine1713 bake complete: " + prefabPath);
            }
            catch (Exception ex)
            {
                DumpBlackBox(telemetry, ex);
                Debug.LogError("RockSculptorEngine1713 bake failed: " + ex.Message);
                throw;
            }
            finally
            {
                if (sdf.IsCreated)
                    sdf.Dispose();
                if (erodedSdf.IsCreated)
                    erodedSdf.Dispose();
                if (vertices.IsCreated)
                    vertices.Dispose();
                if (indices.IsCreated)
                    indices.Dispose();
                if (telemetry.IsCreated)
                    telemetry.Dispose();
            }
        }

        private SculptSettings BuildSettings()
        {
            return BuildSettings(
                _assetId,
                _resolution,
                _radiusMeters,
                _heightMeters,
                _noiseAmplitudeMeters,
                _strataFrequency,
                _seed,
                _globalQualityWeight,
                _triplanarMaterial);
        }

        /// <summary>
        /// Single owner of the clamp/derive rules for a sculpt request. The window's serialized fields
        /// and the batchmode command line both route through here, so neither can drift into a
        /// different budget, seed floor, or erosion-drop curve.
        /// </summary>
        private static SculptSettings BuildSettings(
            string assetId,
            int resolution,
            float radiusMeters,
            float heightMeters,
            float noiseAmplitudeMeters,
            float strataFrequency,
            uint seed,
            float globalQualityWeight,
            Material material)
        {
            float q = math.saturate(float.IsFinite(globalQualityWeight) ? globalQualityWeight : 0f);
            int drops = Mathf.RoundToInt(Mathf.Lerp(500f, 50000f, q * q * (3f - 2f * q)));
            return new SculptSettings
            {
                AssetId = SanitizeAssetId(assetId),
                Resolution = Mathf.Clamp(resolution, MinimumResolution, MaximumResolution),
                RadiusMeters = Mathf.Max(0.5f, radiusMeters),
                HeightMeters = Mathf.Max(0.5f, heightMeters),
                NoiseAmplitudeMeters = Mathf.Max(0f, noiseAmplitudeMeters),
                StrataFrequency = Mathf.Max(0.01f, strataFrequency),
                Seed = math.max(1u, seed),
                GlobalQualityWeight = q,
                ErosionDrops = drops,
                Material = material
            };
        }

        private static string SanitizeAssetId(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId))
                return "1713";

            string trimmed = assetId.Trim();
            int length = math.min(trimmed.Length, 64);
            Span<char> scratch = stackalloc char[64];
            int written = 0;
            for (int i = 0; i < length; i++)
            {
                char c = trimmed[i];
                bool valid =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' ||
                    c == '-';
                scratch[written++] = valid ? c : '_';
            }

            while (written > 0 && scratch[written - 1] == '_')
                written--;

            return written > 0 ? new string(scratch.Slice(0, written)) : "1713";
        }

        private static void ValidateUnmanagedLayouts()
        {
            int rockVertexStride = UnsafeUtility.SizeOf<RockVertex>();
            if (rockVertexStride != RockVertexStrideBytes || (rockVertexStride & 7) != 0)
                throw new InvalidOperationException("RockVertex layout is not ARM64-aligned.");

            int telemetryStride = UnsafeUtility.SizeOf<SculptTelemetryEntry>();
            if (telemetryStride != SculptTelemetryEntryStrideBytes || (telemetryStride & 7) != 0)
                throw new InvalidOperationException("SculptTelemetryEntry layout is not ARM64-aligned.");

            int quadricStride = UnsafeUtility.SizeOf<QuadricError>();
            if (quadricStride != QuadricErrorStrideBytes || (quadricStride & 7) != 0)
                throw new InvalidOperationException("QuadricError layout is not ARM64-aligned.");

            int edgeCandidateStride = UnsafeUtility.SizeOf<EdgeCollapseCandidate>();
            if (edgeCandidateStride != EdgeCollapseCandidateStrideBytes || (edgeCandidateStride & 7) != 0)
                throw new InvalidOperationException("EdgeCollapseCandidate layout is not ARM64-aligned.");
        }

        private static void BuildVoxelSurface(
            SculptSettings settings,
            NativeArray<float> sdf,
            out NativeArray<RockVertex> vertices,
            out NativeArray<int> indices,
            out int vertexCount,
            out int indexCount,
            NativeArray<SculptTelemetryEntry> telemetry)
        {
            int res = settings.Resolution;
            int cellCount = (res - 1) * (res - 1) * (res - 1);
            int maxFaces = math.min(cellCount * 6, 220000);
            vertices = new NativeArray<RockVertex>(maxFaces * 4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            indices = new NativeArray<int>(maxFaces * 6, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> faceMasks = new NativeArray<byte>(res * res * res, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            vertexCount = 0;
            indexCount = 0;

            try
            {
                var classifyJob = new ClassifySurfaceFacesJob
                {
                    Sdf = sdf,
                    FaceMasks = faceMasks,
                    Resolution = res
                };
                classifyJob.Schedule(faceMasks.Length, 64).Complete();

                float invExtentSteps = 1f / math.max(1, res - 1);
                float3 voxelSize = new float3(
                    settings.RadiusMeters * 2f * invExtentSteps,
                    settings.HeightMeters * invExtentSteps,
                    settings.RadiusMeters * 2f * invExtentSteps);
                float3 origin = new float3(-settings.RadiusMeters, -settings.HeightMeters * 0.5f, -settings.RadiusMeters);
                bool capacityExceeded = false;
                for (int z = 1; z < res - 1; z++)
                {
                    for (int y = 1; y < res - 1; y++)
                    {
                        for (int x = 1; x < res - 1; x++)
                        {
                            byte mask = faceMasks[x + (y * res) + (z * res * res)];
                            if (mask == 0)
                                continue;

                            if ((mask & FacePositiveX) != 0)
                                TryAddExposedFace(settings, sdf, vertices, indices, ref vertexCount, ref indexCount, ref capacityExceeded, origin, voxelSize, x, y, z, new int3(1, 0, 0));
                            if ((mask & FaceNegativeX) != 0)
                                TryAddExposedFace(settings, sdf, vertices, indices, ref vertexCount, ref indexCount, ref capacityExceeded, origin, voxelSize, x, y, z, new int3(-1, 0, 0));
                            if ((mask & FacePositiveY) != 0)
                                TryAddExposedFace(settings, sdf, vertices, indices, ref vertexCount, ref indexCount, ref capacityExceeded, origin, voxelSize, x, y, z, new int3(0, 1, 0));
                            if ((mask & FaceNegativeY) != 0)
                                TryAddExposedFace(settings, sdf, vertices, indices, ref vertexCount, ref indexCount, ref capacityExceeded, origin, voxelSize, x, y, z, new int3(0, -1, 0));
                            if ((mask & FacePositiveZ) != 0)
                                TryAddExposedFace(settings, sdf, vertices, indices, ref vertexCount, ref indexCount, ref capacityExceeded, origin, voxelSize, x, y, z, new int3(0, 0, 1));
                            if ((mask & FaceNegativeZ) != 0)
                                TryAddExposedFace(settings, sdf, vertices, indices, ref vertexCount, ref indexCount, ref capacityExceeded, origin, voxelSize, x, y, z, new int3(0, 0, -1));
                        }
                    }
                }

                if (capacityExceeded)
                    throw new InvalidOperationException("SDF surface exceeded rock sculptor mesh budget.");

                if (vertexCount <= 0 || indexCount <= 0)
                    throw new InvalidOperationException("SDF produced no visible rock surface.");

                RecordTelemetry(telemetry, 2, 3u, vertexCount, indexCount / 3, 0, settings.Seed);
            }
            finally
            {
                if (faceMasks.IsCreated)
                    faceMasks.Dispose();
            }
        }

        private static void TryAddExposedFace(
            SculptSettings settings,
            NativeArray<float> sdf,
            NativeArray<RockVertex> vertices,
            NativeArray<int> indices,
            ref int vertexCount,
            ref int indexCount,
            ref bool capacityExceeded,
            float3 origin,
            float3 voxelSize,
            int x,
            int y,
            int z,
            int3 normalInt)
        {
            int res = settings.Resolution;
            int nx = x + normalInt.x;
            int ny = y + normalInt.y;
            int nz = z + normalInt.z;
            if (nx <= 0 || ny <= 0 || nz <= 0 || nx >= res - 1 || ny >= res - 1 || nz >= res - 1)
                return;
            if (IsSolid(sdf, res, nx, ny, nz))
                return;
            if (vertexCount + 4 > vertices.Length || indexCount + 6 > indices.Length)
            {
                capacityExceeded = true;
                return;
            }

            float3 faceNormal = math.normalize(new float3(normalInt.x, normalInt.y, normalInt.z));
            float3 min = origin + new float3(x * voxelSize.x, y * voxelSize.y, z * voxelSize.z);
            float3 max = min + voxelSize;
            ResolveFaceCorners(min, max, normalInt, out float3 p0, out float3 p1, out float3 p2, out float3 p3);
            float solidValue = SampleSdf(sdf, res, x, y, z);
            float neighborValue = SampleSdf(sdf, res, nx, ny, nz);
            float crossing = solidValue / math.select(solidValue - neighborValue, 0.0001f, math.abs(solidValue - neighborValue) < 0.0001f);
            float axisStep =
                math.abs(faceNormal.x) * voxelSize.x +
                math.abs(faceNormal.y) * voxelSize.y +
                math.abs(faceNormal.z) * voxelSize.z;
            float3 zeroCrossingOffset = faceNormal * ((math.saturate(crossing) - 0.5f) * axisStep);
            p0 += zeroCrossingOffset;
            p1 += zeroCrossingOffset;
            p2 += zeroCrossingOffset;
            p3 += zeroCrossingOffset;
            int baseIndex = vertexCount;
            vertices[vertexCount++] = CreateVertex(settings, sdf, p0, faceNormal, x, y, z);
            vertices[vertexCount++] = CreateVertex(settings, sdf, p1, faceNormal, x, y, z);
            vertices[vertexCount++] = CreateVertex(settings, sdf, p2, faceNormal, x, y, z);
            vertices[vertexCount++] = CreateVertex(settings, sdf, p3, faceNormal, x, y, z);

            indices[indexCount++] = baseIndex;
            indices[indexCount++] = baseIndex + 1;
            indices[indexCount++] = baseIndex + 2;
            indices[indexCount++] = baseIndex;
            indices[indexCount++] = baseIndex + 2;
            indices[indexCount++] = baseIndex + 3;
        }

        private static RockVertex CreateVertex(SculptSettings settings, NativeArray<float> sdf, float3 position, float3 faceNormal, int x, int y, int z)
        {
            float3 gradientNormal = ResolveGradientNormal(sdf, settings.Resolution, x, y, z, faceNormal);
            float sediment = math.saturate((gradientNormal.y - 0.72f) * 3.57f);
            float neighborDelta =
                math.abs(SampleSdf(sdf, settings.Resolution, x + 1, y, z) - SampleSdf(sdf, settings.Resolution, x - 1, y, z)) +
                math.abs(SampleSdf(sdf, settings.Resolution, x, y + 1, z) - SampleSdf(sdf, settings.Resolution, x, y - 1, z)) +
                math.abs(SampleSdf(sdf, settings.Resolution, x, y, z + 1) - SampleSdf(sdf, settings.Resolution, x, y, z - 1));
            float convexity = math.saturate(0.18f + math.abs(faceNormal.x) * 0.22f + math.abs(faceNormal.z) * 0.22f + neighborDelta * 0.11f);
            float ao = ResolveCavityAo(sdf, settings.Resolution, x, y, z);
            float2 uv0 = ResolveAtlasSafeUv0(settings, position, gradientNormal);
            return new RockVertex
            {
                Position = position,
                Normal = gradientNormal,
                Color = new Color32(
                    (byte)math.round(convexity * 255f),
                    (byte)math.round(sediment * 255f),
                    (byte)math.round(ao * 255f),
                    255),
                Uv0 = uv0
            };
        }

        private static float2 ResolveAtlasSafeUv0(SculptSettings settings, float3 position, float3 normal)
        {
            float radiusSpan = math.max(0.001f, settings.RadiusMeters * 2f);
            float heightSpan = math.max(0.001f, settings.HeightMeters);
            float3 absNormal = math.abs(normal);
            float2 uv;
            if (absNormal.y >= absNormal.x && absNormal.y >= absNormal.z)
                uv = new float2(position.x / radiusSpan, position.z / radiusSpan) + 0.5f;
            else if (absNormal.x > absNormal.z)
                uv = new float2(position.z / radiusSpan, position.y / heightSpan) + 0.5f;
            else
                uv = new float2(position.x / radiusSpan, position.y / heightSpan) + 0.5f;

            uv = math.saturate(uv);
            return uv * (1f - AtlasUvBleedGuard01 * 2f) + AtlasUvBleedGuard01;
        }

        private static float3 ResolveGradientNormal(NativeArray<float> sdf, int res, int x, int y, int z, float3 fallbackNormal)
        {
            float3 gradient = new float3(
                SampleSdf(sdf, res, x + 1, y, z) - SampleSdf(sdf, res, x - 1, y, z),
                SampleSdf(sdf, res, x, y + 1, z) - SampleSdf(sdf, res, x, y - 1, z),
                SampleSdf(sdf, res, x, y, z + 1) - SampleSdf(sdf, res, x, y, z - 1));
            float lenSq = math.lengthsq(gradient);
            if (!math.isfinite(lenSq) || lenSq < 0.000001f)
                return fallbackNormal;

            float3 normal = gradient * math.rsqrt(lenSq);
            return math.all(math.isfinite(normal)) ? normal : fallbackNormal;
        }

        private static float ResolveCavityAo(NativeArray<float> sdf, int res, int x, int y, int z)
        {
            int occluders = 0;
            occluders += IsSolid(sdf, res, x + 1, y, z) ? 1 : 0;
            occluders += IsSolid(sdf, res, x - 1, y, z) ? 1 : 0;
            occluders += IsSolid(sdf, res, x, y + 1, z) ? 1 : 0;
            occluders += IsSolid(sdf, res, x, y - 1, z) ? 1 : 0;
            occluders += IsSolid(sdf, res, x, y, z + 1) ? 1 : 0;
            occluders += IsSolid(sdf, res, x, y, z - 1) ? 1 : 0;
            return math.saturate(1f - occluders * 0.12f);
        }

        private static void ResolveFaceCorners(float3 min, float3 max, int3 normal, out float3 p0, out float3 p1, out float3 p2, out float3 p3)
        {
            if (normal.x > 0)
            {
                p0 = new float3(max.x, min.y, min.z); p1 = new float3(max.x, min.y, max.z); p2 = new float3(max.x, max.y, max.z); p3 = new float3(max.x, max.y, min.z);
                return;
            }
            if (normal.x < 0)
            {
                p0 = new float3(min.x, min.y, max.z); p1 = new float3(min.x, min.y, min.z); p2 = new float3(min.x, max.y, min.z); p3 = new float3(min.x, max.y, max.z);
                return;
            }
            if (normal.y > 0)
            {
                p0 = new float3(min.x, max.y, min.z); p1 = new float3(max.x, max.y, min.z); p2 = new float3(max.x, max.y, max.z); p3 = new float3(min.x, max.y, max.z);
                return;
            }
            if (normal.y < 0)
            {
                p0 = new float3(min.x, min.y, max.z); p1 = new float3(max.x, min.y, max.z); p2 = new float3(max.x, min.y, min.z); p3 = new float3(min.x, min.y, min.z);
                return;
            }
            if (normal.z > 0)
            {
                p0 = new float3(max.x, min.y, max.z); p1 = new float3(min.x, min.y, max.z); p2 = new float3(min.x, max.y, max.z); p3 = new float3(max.x, max.y, max.z);
                return;
            }

            p0 = new float3(min.x, min.y, min.z); p1 = new float3(max.x, min.y, min.z); p2 = new float3(max.x, max.y, min.z); p3 = new float3(min.x, max.y, min.z);
        }

        private static Mesh CreateMesh(string meshName, NativeArray<RockVertex> vertices, NativeArray<int> indices, int vertexCount, int indexCount, Bounds bounds)
        {
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh mesh = null;
            bool applied = false;
            try
            {
                Mesh.MeshData meshData = meshDataArray[0];
                meshData.SetVertexBufferParams(
                    vertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3));
                meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

                NativeArray<float3> positions = meshData.GetVertexData<float3>(0);
                NativeArray<float3> normals = meshData.GetVertexData<float3>(1);
                NativeArray<Color32> colors = meshData.GetVertexData<Color32>(2);
                NativeArray<float2> uv0 = meshData.GetVertexData<float2>(3);
                for (int i = 0; i < vertexCount; i++)
                {
                    RockVertex vertex = vertices[i];
                    positions[i] = vertex.Position;
                    normals[i] = vertex.Normal;
                    colors[i] = vertex.Color;
                    uv0[i] = vertex.Uv0;
                }

                NativeArray<uint> indexData = meshData.GetIndexData<uint>();
                for (int i = 0; i < indexCount; i++)
                    indexData[i] = (uint)indices[i];

                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

                mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
                Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
                applied = true;
                mesh.bounds = bounds;
                return mesh;
            }
            catch
            {
                if (!applied)
                    meshDataArray.Dispose();
                if (mesh != null)
                    DestroyImmediate(mesh);
                throw;
            }
        }

        private static Mesh CreateBudgetMesh(string meshName, NativeArray<RockVertex> vertices, NativeArray<int> indices, int vertexCount, int indexCount, Bounds bounds, int triangleBudget)
        {
            int sourceTriangles = indexCount / 3;
            int targetTriangles = math.min(sourceTriangles, math.max(1, triangleBudget));
            if (sourceTriangles <= targetTriangles)
                return CreateMesh(meshName, vertices, indices, vertexCount, indexCount, bounds);

            NativeArray<RockVertex> mutableVertices = new NativeArray<RockVertex>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<QuadricError> quadrics = new NativeArray<QuadricError>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<int> vertexParents = new NativeArray<int>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> activeVertices = new NativeArray<byte>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<EdgeCollapseCandidate> candidates = new NativeArray<EdgeCollapseCandidate>(indexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<RockVertex> compactedVertices = new NativeArray<RockVertex>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<int> compactMap = new NativeArray<int>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<int> decimated = new NativeArray<int>(targetTriangles * 3, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                InitializeQemDecimationState(vertices, indices, vertexCount, indexCount, mutableVertices, quadrics, vertexParents, activeVertices);
                int activeTriangleCount = CountActiveTriangles(indices, indexCount, vertexParents);
                for (int pass = 0; pass < MaxQemCollapsePasses && activeTriangleCount > targetTriangles; pass++)
                {
                    int candidateCount = BuildEdgeCollapseCandidates(mutableVertices, indices, indexCount, vertexParents, activeVertices, quadrics, candidates);
                    if (candidateCount <= 0)
                        break;

                    SortEdgeCollapseCandidates(candidates, candidateCount);
                    int collapseBudget = math.max(1, activeTriangleCount - targetTriangles);
                    int collapsed = CollapseEdges(mutableVertices, vertexParents, activeVertices, quadrics, candidates, candidateCount, collapseBudget);
                    if (collapsed <= 0)
                        break;

                    activeTriangleCount = CountActiveTriangles(indices, indexCount, vertexParents);
                }

                int outputIndexCount = EmitCompactedQemMesh(
                    mutableVertices,
                    indices,
                    indexCount,
                    vertexParents,
                    compactMap,
                    compactedVertices,
                    decimated,
                    out int outputVertexCount);
                if (outputVertexCount <= 0 || outputIndexCount < 3)
                    return CreateStrideBudgetMesh(meshName, vertices, indices, vertexCount, indexCount, bounds, targetTriangles);

                Bounds decimatedBounds = ValidateAndResolveBounds(compactedVertices, decimated, outputVertexCount, outputIndexCount);
                return CreateMesh(meshName, compactedVertices, decimated, outputVertexCount, outputIndexCount, decimatedBounds);
            }
            finally
            {
                mutableVertices.Dispose();
                quadrics.Dispose();
                vertexParents.Dispose();
                activeVertices.Dispose();
                candidates.Dispose();
                compactedVertices.Dispose();
                compactMap.Dispose();
                decimated.Dispose();
            }
        }

        private static Mesh CreateStrideBudgetMesh(string meshName, NativeArray<RockVertex> vertices, NativeArray<int> indices, int vertexCount, int indexCount, Bounds bounds, int triangleBudget)
        {
            int sourceTriangles = indexCount / 3;
            int targetTriangles = math.min(sourceTriangles, math.max(1, triangleBudget));
            int stride = math.max(1, (int)math.ceil(sourceTriangles / (float)targetTriangles));
            int outputIndexCount = math.min(indexCount, targetTriangles * 3);
            NativeArray<int> decimated = new NativeArray<int>(outputIndexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                int cursor = 0;
                for (int tri = 0; tri < sourceTriangles && cursor + 3 <= outputIndexCount; tri += stride)
                {
                    int src = tri * 3;
                    decimated[cursor++] = indices[src];
                    decimated[cursor++] = indices[src + 1];
                    decimated[cursor++] = indices[src + 2];
                }

                Bounds decimatedBounds = ValidateAndResolveBounds(vertices, decimated, vertexCount, cursor);
                return CreateMesh(meshName, vertices, decimated, vertexCount, cursor, decimatedBounds);
            }
            finally
            {
                decimated.Dispose();
            }
        }

        private static void InitializeQemDecimationState(
            NativeArray<RockVertex> sourceVertices,
            NativeArray<int> indices,
            int vertexCount,
            int indexCount,
            NativeArray<RockVertex> mutableVertices,
            NativeArray<QuadricError> quadrics,
            NativeArray<int> vertexParents,
            NativeArray<byte> activeVertices)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                mutableVertices[i] = sourceVertices[i];
                quadrics[i] = default;
                vertexParents[i] = i;
                activeVertices[i] = 1;
            }

            for (int i = 0; i < indexCount; i += 3)
            {
                int i0 = indices[i];
                int i1 = indices[i + 1];
                int i2 = indices[i + 2];
                if ((uint)i0 >= vertexCount || (uint)i1 >= vertexCount || (uint)i2 >= vertexCount)
                    continue;

                QuadricError planeQuadric = QuadricError.FromTriangle(
                    mutableVertices[i0].Position,
                    mutableVertices[i1].Position,
                    mutableVertices[i2].Position);
                if (!planeQuadric.IsValid())
                    continue;

                quadrics[i0] = quadrics[i0].Add(planeQuadric);
                quadrics[i1] = quadrics[i1].Add(planeQuadric);
                quadrics[i2] = quadrics[i2].Add(planeQuadric);
            }
        }

        private static int CountActiveTriangles(NativeArray<int> indices, int indexCount, NativeArray<int> vertexParents)
        {
            int count = 0;
            for (int i = 0; i < indexCount; i += 3)
            {
                int i0 = FindRoot(vertexParents, indices[i]);
                int i1 = FindRoot(vertexParents, indices[i + 1]);
                int i2 = FindRoot(vertexParents, indices[i + 2]);
                if (i0 != i1 && i1 != i2 && i2 != i0)
                    count++;
            }

            return count;
        }

        private static int BuildEdgeCollapseCandidates(
            NativeArray<RockVertex> vertices,
            NativeArray<int> indices,
            int indexCount,
            NativeArray<int> vertexParents,
            NativeArray<byte> activeVertices,
            NativeArray<QuadricError> quadrics,
            NativeArray<EdgeCollapseCandidate> candidates)
        {
            int candidateCount = 0;
            for (int i = 0; i < indexCount && candidateCount + 3 <= candidates.Length; i += 3)
            {
                int i0 = FindRoot(vertexParents, indices[i]);
                int i1 = FindRoot(vertexParents, indices[i + 1]);
                int i2 = FindRoot(vertexParents, indices[i + 2]);
                if (i0 == i1 || i1 == i2 || i2 == i0)
                    continue;

                TryWriteEdgeCandidate(vertices, activeVertices, quadrics, i0, i1, candidates, ref candidateCount);
                TryWriteEdgeCandidate(vertices, activeVertices, quadrics, i1, i2, candidates, ref candidateCount);
                TryWriteEdgeCandidate(vertices, activeVertices, quadrics, i2, i0, candidates, ref candidateCount);
            }

            return candidateCount;
        }

        private static void TryWriteEdgeCandidate(
            NativeArray<RockVertex> vertices,
            NativeArray<byte> activeVertices,
            NativeArray<QuadricError> quadrics,
            int v0,
            int v1,
            NativeArray<EdgeCollapseCandidate> candidates,
            ref int candidateCount)
        {
            if ((uint)v0 >= vertices.Length || (uint)v1 >= vertices.Length || v0 == v1)
                return;
            if (activeVertices[v0] == 0 || activeVertices[v1] == 0)
                return;

            int a = math.min(v0, v1);
            int b = math.max(v0, v1);
            QuadricError combined = quadrics[a].Add(quadrics[b]);
            float3 target = ResolveQemTarget(vertices[a], vertices[b], in combined);
            float baseCost = combined.Evaluate(target);
            if (!math.isfinite(baseCost))
                return;

            float3 normalA = SafeNormal(vertices[a].Normal);
            float3 normalB = SafeNormal(vertices[b].Normal);
            float normalPenalty = 1f + math.saturate(1f - math.dot(normalA, normalB)) * 8f;
            int fractureByte = vertices[a].Color.r > vertices[b].Color.r ? vertices[a].Color.r : vertices[b].Color.r;
            float fracturePenalty = 1f + (fractureByte / 255f) * 2f;
            float edgeLength = math.length(vertices[a].Position - vertices[b].Position);
            float cost = (math.max(0f, baseCost) + edgeLength * 0.0005f) * normalPenalty * fracturePenalty;
            if (!math.isfinite(cost))
                return;

            candidates[candidateCount++] = new EdgeCollapseCandidate
            {
                TargetCost = new float4(target, cost),
                V0 = a,
                V1 = b,
                Padding0 = 0u,
                Padding1 = 0u
            };
        }

        private static float3 ResolveQemTarget(RockVertex a, RockVertex b, in QuadricError combined)
        {
            float3 p0 = a.Position;
            float3 p1 = b.Position;
            float3 mid = (p0 + p1) * 0.5f;
            float cost0 = combined.Evaluate(p0);
            float cost1 = combined.Evaluate(p1);
            float costMid = combined.Evaluate(mid);
            float3 target = mid;
            float best = costMid;
            if (math.isfinite(cost0) && cost0 < best)
            {
                best = cost0;
                target = p0;
            }

            if (math.isfinite(cost1) && cost1 < best)
                target = p1;

            return math.all(math.isfinite(target)) ? target : mid;
        }

        private static int CollapseEdges(
            NativeArray<RockVertex> vertices,
            NativeArray<int> vertexParents,
            NativeArray<byte> activeVertices,
            NativeArray<QuadricError> quadrics,
            NativeArray<EdgeCollapseCandidate> candidates,
            int candidateCount,
            int collapseBudget)
        {
            int collapsed = 0;
            for (int i = 0; i < candidateCount && collapsed < collapseBudget; i++)
            {
                EdgeCollapseCandidate candidate = candidates[i];
                int root0 = FindRoot(vertexParents, candidate.V0);
                int root1 = FindRoot(vertexParents, candidate.V1);
                if (root0 == root1 || activeVertices[root0] == 0 || activeVertices[root1] == 0)
                    continue;
                if (RejectSharpCollapse(vertices[root0], vertices[root1]))
                    continue;

                int keep = root0;
                int remove = root1;
                QuadricError combined = quadrics[keep].Add(quadrics[remove]);
                vertices[keep] = MergeCollapsedVertices(vertices[keep], vertices[remove], candidate.TargetCost.xyz);
                quadrics[keep] = combined;
                vertexParents[remove] = keep;
                activeVertices[remove] = 0;
                collapsed++;
            }

            return collapsed;
        }

        private static bool RejectSharpCollapse(RockVertex a, RockVertex b)
        {
            float normalDot = math.dot(SafeNormal(a.Normal), SafeNormal(b.Normal));
            int fracture = a.Color.r > b.Color.r ? a.Color.r : b.Color.r;
            return normalDot < 0.18f && fracture > 192;
        }

        private static RockVertex MergeCollapsedVertices(RockVertex a, RockVertex b, float3 targetPosition)
        {
            float3 normal = SafeNormal(a.Normal + b.Normal);
            return new RockVertex
            {
                Position = math.all(math.isfinite(targetPosition)) ? targetPosition : (a.Position + b.Position) * 0.5f,
                Normal = normal,
                Color = new Color32(
                    (byte)(a.Color.r > b.Color.r ? a.Color.r : b.Color.r),
                    AverageByte(a.Color.g, b.Color.g),
                    AverageByte(a.Color.b, b.Color.b),
                    255),
                Uv0 = (a.Uv0 + b.Uv0) * 0.5f,
                Padding = 0u
            };
        }

        private static int EmitCompactedQemMesh(
            NativeArray<RockVertex> mutableVertices,
            NativeArray<int> indices,
            int indexCount,
            NativeArray<int> vertexParents,
            NativeArray<int> compactMap,
            NativeArray<RockVertex> compactedVertices,
            NativeArray<int> outputIndices,
            out int outputVertexCount)
        {
            for (int i = 0; i < compactMap.Length; i++)
                compactMap[i] = -1;

            int outputIndexCount = 0;
            outputVertexCount = 0;
            for (int i = 0; i < indexCount && outputIndexCount + 3 <= outputIndices.Length; i += 3)
            {
                int root0 = FindRoot(vertexParents, indices[i]);
                int root1 = FindRoot(vertexParents, indices[i + 1]);
                int root2 = FindRoot(vertexParents, indices[i + 2]);
                if (root0 == root1 || root1 == root2 || root2 == root0)
                    continue;

                float3 p0 = mutableVertices[root0].Position;
                float3 p1 = mutableVertices[root1].Position;
                float3 p2 = mutableVertices[root2].Position;
                float area = math.length(math.cross(p1 - p0, p2 - p0)) * 0.5f;
                if (!math.isfinite(area) || area < 0.0001f)
                    continue;

                outputIndices[outputIndexCount++] = ResolveCompactedVertex(root0, mutableVertices, compactMap, compactedVertices, ref outputVertexCount);
                outputIndices[outputIndexCount++] = ResolveCompactedVertex(root1, mutableVertices, compactMap, compactedVertices, ref outputVertexCount);
                outputIndices[outputIndexCount++] = ResolveCompactedVertex(root2, mutableVertices, compactMap, compactedVertices, ref outputVertexCount);
            }

            return outputIndexCount;
        }

        private static int ResolveCompactedVertex(
            int sourceIndex,
            NativeArray<RockVertex> mutableVertices,
            NativeArray<int> compactMap,
            NativeArray<RockVertex> compactedVertices,
            ref int outputVertexCount)
        {
            int mapped = compactMap[sourceIndex];
            if (mapped >= 0)
                return mapped;

            mapped = outputVertexCount++;
            compactMap[sourceIndex] = mapped;
            compactedVertices[mapped] = mutableVertices[sourceIndex];
            return mapped;
        }

        private static int FindRoot(NativeArray<int> vertexParents, int index)
        {
            int root = index;
            int guard = 0;
            while ((uint)root < vertexParents.Length && vertexParents[root] != root && guard++ < vertexParents.Length)
                root = vertexParents[root];

            if ((uint)root >= vertexParents.Length)
                return math.clamp(index, 0, vertexParents.Length - 1);

            int cursor = index;
            guard = 0;
            while ((uint)cursor < vertexParents.Length && vertexParents[cursor] != root && guard++ < vertexParents.Length)
            {
                int next = vertexParents[cursor];
                vertexParents[cursor] = root;
                cursor = next;
            }

            return root;
        }

        private static void SortEdgeCollapseCandidates(NativeArray<EdgeCollapseCandidate> candidates, int count)
        {
            for (int start = (count >> 1) - 1; start >= 0; start--)
                SiftDownCandidates(candidates, start, count);

            for (int end = count - 1; end > 0; end--)
            {
                SwapCandidates(candidates, 0, end);
                SiftDownCandidates(candidates, 0, end);
            }
        }

        private static void SiftDownCandidates(NativeArray<EdgeCollapseCandidate> candidates, int root, int count)
        {
            while (true)
            {
                int child = root * 2 + 1;
                if (child >= count)
                    return;

                int swap = root;
                if (CandidateGreater(candidates[child], candidates[swap]))
                    swap = child;
                int right = child + 1;
                if (right < count && CandidateGreater(candidates[right], candidates[swap]))
                    swap = right;
                if (swap == root)
                    return;

                SwapCandidates(candidates, root, swap);
                root = swap;
            }
        }

        private static bool CandidateGreater(EdgeCollapseCandidate left, EdgeCollapseCandidate right)
        {
            float leftCost = left.TargetCost.w;
            float rightCost = right.TargetCost.w;
            if (leftCost > rightCost)
                return true;
            if (leftCost < rightCost)
                return false;
            if (left.V0 != right.V0)
                return left.V0 > right.V0;
            return left.V1 > right.V1;
        }

        private static void SwapCandidates(NativeArray<EdgeCollapseCandidate> candidates, int left, int right)
        {
            EdgeCollapseCandidate tmp = candidates[left];
            candidates[left] = candidates[right];
            candidates[right] = tmp;
        }

        private static float3 SafeNormal(float3 value)
        {
            float lenSq = math.lengthsq(value);
            if (!math.isfinite(lenSq) || lenSq < 0.000001f)
                return new float3(0f, 1f, 0f);
            return value * math.rsqrt(lenSq);
        }

        private static byte AverageByte(byte a, byte b)
        {
            return (byte)(((int)a + b) >> 1);
        }

        private static Mesh CreateCollisionProxyMesh(string meshName, Bounds bounds)
        {
            NativeArray<RockVertex> vertices = new NativeArray<RockVertex>(8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<int> indices = new NativeArray<int>(s_collisionBoxIndices.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                for (int i = 0; i < 8; i++)
                {
                    float3 p = new float3(
                        (i & 1) == 0 ? min.x : max.x,
                        (i & 2) == 0 ? min.y : max.y,
                        (i & 4) == 0 ? min.z : max.z);
                    vertices[i] = new RockVertex { Position = p, Normal = SafeNormal(p - (float3)bounds.center), Color = new Color32(0, 0, 255, 255), Uv0 = float2.zero };
                }

                for (int i = 0; i < s_collisionBoxIndices.Length; i++)
                    indices[i] = s_collisionBoxIndices[i];

                Mesh mesh = CreateMesh(meshName, vertices, indices, 8, s_collisionBoxIndices.Length, bounds);
                if (s_collisionBoxIndices.Length / 3 > MaxCollisionTriangles)
                    throw new InvalidOperationException("Collision proxy exceeds triangle budget.");
                return mesh;
            }
            finally
            {
                vertices.Dispose();
                indices.Dispose();
            }
        }

        private static Bounds ValidateAndResolveBounds(NativeArray<RockVertex> vertices, NativeArray<int> indices, int vertexCount, int indexCount)
        {
            if (vertexCount <= 0 || indexCount < 3 || indexCount % 3 != 0)
                throw new InvalidOperationException("Invalid topology counts.");

            float3 min = vertices[0].Position;
            float3 max = vertices[0].Position;
            for (int i = 0; i < vertexCount; i++)
            {
                RockVertex vertex = vertices[i];
                if (!math.all(math.isfinite(vertex.Position)) || !math.all(math.isfinite(vertex.Normal)) || math.lengthsq(vertex.Normal) < 0.25f)
                    throw new InvalidOperationException("Non-finite or invalid rock vertex.");

                min = math.min(min, vertex.Position);
                max = math.max(max, vertex.Position);
            }

            for (int i = 0; i < indexCount; i += 3)
            {
                int i0 = indices[i];
                int i1 = indices[i + 1];
                int i2 = indices[i + 2];
                if ((uint)i0 >= vertexCount || (uint)i1 >= vertexCount || (uint)i2 >= vertexCount)
                    throw new InvalidOperationException("Index outside vertex buffer.");

                float3 v0 = vertices[i0].Position;
                float3 v1 = vertices[i1].Position;
                float3 v2 = vertices[i2].Position;
                float area = math.length(math.cross(v1 - v0, v2 - v0)) * 0.5f;
                if (!math.isfinite(area) || area < 0.0001f)
                    throw new InvalidOperationException("Degenerate triangle detected in SDF output.");
            }

            Bounds bounds = new Bounds((Vector3)((min + max) * 0.5f), (Vector3)math.max(max - min, new float3(0.001f)));
            if (!float.IsFinite(bounds.extents.sqrMagnitude) || bounds.extents.sqrMagnitude <= 0f)
                throw new InvalidOperationException("Invalid mesh bounds.");
            return bounds;
        }

        private static void SaveAssets(SculptSettings settings, Mesh lod0, Mesh lod1, Mesh lod2, Mesh collision, Bounds bounds, double elapsedMs, out string prefabPath)
        {
            _ = bounds;
            _ = elapsedMs;
            EnsureAssetFolder(PrefabOutputFolder);
            EnsureAssetFolder(MeshOutputFolder);
            string stem = "GEN_Rock_" + settings.AssetId;
            Mesh savedLod0 = SaveMesh(lod0, MeshOutputFolder + "/" + stem + "_LOD0.asset");
            Mesh savedLod1 = SaveMesh(lod1, MeshOutputFolder + "/" + stem + "_LOD1.asset");
            Mesh savedLod2 = SaveMesh(lod2, MeshOutputFolder + "/" + stem + "_LOD2.asset");
            Mesh savedCollision = SaveMesh(collision, MeshOutputFolder + "/COL_" + stem + ".asset");
            prefabPath = PrefabOutputFolder + "/" + stem + ".prefab";

            GameObject root = new GameObject(stem);
            try
            {
                int staticLayer = ResolveWorldStaticLayer();
                StaticEditorFlags rendererFlags = ResolveGeneratedRockStaticFlags(bounds);
                root.layer = staticLayer;
                GameObjectUtility.SetStaticEditorFlags(root, GeneratedRockRootStaticFlags);
                LODGroup lodGroup = root.AddComponent<LODGroup>();
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = true;
                Renderer[] renderers = new Renderer[3];
                CreateLodChild(root.transform, "VIS_LOD0", savedLod0, settings.Material, staticLayer, rendererFlags, out renderers[0]);
                CreateLodChild(root.transform, "VIS_LOD1", savedLod1, settings.Material, staticLayer, rendererFlags, out renderers[1]);
                CreateLodChild(root.transform, "VIS_LOD2", savedLod2, settings.Material, staticLayer, rendererFlags, out renderers[2]);
                lodGroup.SetLODs(new[]
                {
                    new LOD(0.58f, new[] { renderers[0] }),
                    new LOD(0.28f, new[] { renderers[1] }),
                    new LOD(0.05f, new[] { renderers[2] })
                });
                lodGroup.RecalculateBounds();

                GameObject proxy = new GameObject("COL_RockProxy");
                proxy.layer = staticLayer;
                GameObjectUtility.SetStaticEditorFlags(proxy, GeneratedRockRootStaticFlags);
                proxy.transform.SetParent(root.transform, false);
                MeshCollider collider = proxy.AddComponent<MeshCollider>();
                collider.sharedMesh = savedCollision;
                collider.convex = true;

                if (!ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string colliderFailure))
                    throw new InvalidOperationException("1716 collider validation failed before save: " + colliderFailure);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success)
                    throw new InvalidOperationException("Prefab save failed: " + prefabPath);

                if (!ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath, out colliderFailure))
                    throw new InvalidOperationException("1716 collider validation failed after save: " + colliderFailure);
            }
            finally
            {
                DestroyImmediate(root);
            }
        }

        private static void CreateLodChild(Transform root, string name, Mesh mesh, Material material, int layer, StaticEditorFlags staticFlags, out Renderer renderer)
        {
            GameObject child = new GameObject(name);
            child.layer = layer;
            GameObjectUtility.SetStaticEditorFlags(child, staticFlags);
            child.transform.SetParent(root, false);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material != null ? material : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            meshRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            meshRenderer.allowOcclusionWhenDynamic = true;
            renderer = meshRenderer;
        }

        private static StaticEditorFlags ResolveGeneratedRockStaticFlags(Bounds visualBounds)
        {
            StaticEditorFlags flags = GeneratedRockRootStaticFlags;
            if (CalculateBoundsVolume(visualBounds) >= GeologyForgeConstants.OccluderStaticMinimumVolumeCubicMeters)
                flags |= StaticEditorFlags.OccluderStatic;
            return flags;
        }

        private static float CalculateBoundsVolume(Bounds bounds)
        {
            Vector3 size = bounds.size;
            if (!float.IsFinite(size.x) || !float.IsFinite(size.y) || !float.IsFinite(size.z))
                return 0f;

            return Mathf.Max(0f, size.x) * Mathf.Max(0f, size.y) * Mathf.Max(0f, size.z);
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                existing.RecalculateBounds();
                ValidateSavedBounds(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            mesh.RecalculateBounds();
            ValidateSavedBounds(mesh);
            return mesh;
        }

        private static void ValidateSavedBounds(Mesh mesh)
        {
            if (mesh == null || !float.IsFinite(mesh.bounds.extents.sqrMagnitude) || mesh.bounds.extents.sqrMagnitude <= 0f)
                throw new InvalidOperationException("Saved mesh has invalid bounds.");
        }

        private static void DumpBlackBox(NativeArray<SculptTelemetryEntry> telemetry, Exception ex)
        {
            Directory.CreateDirectory("Docs/AgentLogs");
            using (FileStream stream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x31373133u);
                writer.Write(telemetry.IsCreated ? telemetry.Length : 0);
                writer.Write(ex != null ? ex.GetType().Name : string.Empty);
                if (!telemetry.IsCreated)
                    return;

                for (int i = 0; i < telemetry.Length; i++)
                {
                    SculptTelemetryEntry entry = telemetry[i];
                    writer.Write(entry.Stage);
                    writer.Write(entry.VertexCount);
                    writer.Write(entry.TriangleCount);
                    writer.Write(entry.CollisionTriangles);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.Padding);
                }
            }
        }

        private static void RecordTelemetry(NativeArray<SculptTelemetryEntry> telemetry, int slot, uint stage, int vertices, int triangles, int collisionTriangles, uint seed)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int index = math.clamp(slot, 0, telemetry.Length - 1);
            telemetry[index] = new SculptTelemetryEntry
            {
                Stage = stage,
                VertexCount = vertices,
                TriangleCount = triangles,
                CollisionTriangles = collisionTriangles,
                StateHash = math.hash(new uint4(stage, (uint)math.max(0, vertices), (uint)math.max(0, triangles), seed))
            };
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = folder.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static bool IsSolid(NativeArray<float> sdf, int res, int x, int y, int z)
        {
            return SampleSdf(sdf, res, x, y, z) <= 0f;
        }

        private static float SampleSdf(NativeArray<float> sdf, int res, int x, int y, int z)
        {
            x = math.clamp(x, 0, res - 1);
            y = math.clamp(y, 0, res - 1);
            z = math.clamp(z, 0, res - 1);
            return sdf[x + (y * res) + (z * res * res)];
        }

        private static int ResolveWorldStaticLayer()
        {
            int layer = LayerMask.NameToLayer("World_Static");
            return layer >= 0 ? layer : HectonLayerMasks.Terrain;
        }

        private struct SculptSettings
        {
            public string AssetId;
            public int Resolution;
            public float RadiusMeters;
            public float HeightMeters;
            public float NoiseAmplitudeMeters;
            public float StrataFrequency;
            public uint Seed;
            public float GlobalQualityWeight;
            public int ErosionDrops;
            public Material Material;
        }

        [StructLayout(LayoutKind.Explicit, Size = RockVertexStrideBytes)]
        private struct RockVertex
        {
            [FieldOffset(0)]
            public float3 Position;

            [FieldOffset(12)]
            public float3 Normal;

            [FieldOffset(24)]
            public Color32 Color;

            [FieldOffset(28)]
            public float2 Uv0;

            [FieldOffset(36)]
            public uint Padding;
        }

        [StructLayout(LayoutKind.Explicit, Size = SculptTelemetryEntryStrideBytes)]
        private struct SculptTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Stage;

            [FieldOffset(4)]
            public int VertexCount;

            [FieldOffset(8)]
            public int TriangleCount;

            [FieldOffset(12)]
            public int CollisionTriangles;

            [FieldOffset(16)]
            public uint StateHash;

            [FieldOffset(20)]
            public uint Padding;
        }

        [StructLayout(LayoutKind.Explicit, Size = QuadricErrorStrideBytes)]
        private struct QuadricError
        {
            [FieldOffset(0)]
            public float M00;

            [FieldOffset(4)]
            public float M01;

            [FieldOffset(8)]
            public float M02;

            [FieldOffset(12)]
            public float M03;

            [FieldOffset(16)]
            public float M11;

            [FieldOffset(20)]
            public float M12;

            [FieldOffset(24)]
            public float M13;

            [FieldOffset(28)]
            public float M22;

            [FieldOffset(32)]
            public float M23;

            [FieldOffset(36)]
            public float M33;

            public static QuadricError FromTriangle(float3 p0, float3 p1, float3 p2)
            {
                float3 n = math.cross(p1 - p0, p2 - p0);
                float lenSq = math.lengthsq(n);
                if (!math.isfinite(lenSq) || lenSq < 0.0000001f)
                    return default;

                n *= math.rsqrt(lenSq);
                float d = -math.dot(n, p0);
                return new QuadricError
                {
                    M00 = n.x * n.x,
                    M01 = n.x * n.y,
                    M02 = n.x * n.z,
                    M03 = n.x * d,
                    M11 = n.y * n.y,
                    M12 = n.y * n.z,
                    M13 = n.y * d,
                    M22 = n.z * n.z,
                    M23 = n.z * d,
                    M33 = d * d
                };
            }

            public QuadricError Add(QuadricError other)
            {
                return new QuadricError
                {
                    M00 = M00 + other.M00,
                    M01 = M01 + other.M01,
                    M02 = M02 + other.M02,
                    M03 = M03 + other.M03,
                    M11 = M11 + other.M11,
                    M12 = M12 + other.M12,
                    M13 = M13 + other.M13,
                    M22 = M22 + other.M22,
                    M23 = M23 + other.M23,
                    M33 = M33 + other.M33
                };
            }

            public float Evaluate(float3 p)
            {
                float x = p.x;
                float y = p.y;
                float z = p.z;
                return
                    (M00 * x * x) +
                    (2f * M01 * x * y) +
                    (2f * M02 * x * z) +
                    (2f * M03 * x) +
                    (M11 * y * y) +
                    (2f * M12 * y * z) +
                    (2f * M13 * y) +
                    (M22 * z * z) +
                    (2f * M23 * z) +
                    M33;
            }

            public bool IsValid()
            {
                return
                    math.isfinite(M00) &&
                    math.isfinite(M01) &&
                    math.isfinite(M02) &&
                    math.isfinite(M03) &&
                    math.isfinite(M11) &&
                    math.isfinite(M12) &&
                    math.isfinite(M13) &&
                    math.isfinite(M22) &&
                    math.isfinite(M23) &&
                    math.isfinite(M33);
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = EdgeCollapseCandidateStrideBytes)]
        private struct EdgeCollapseCandidate
        {
            [FieldOffset(0)]
            public float4 TargetCost;

            [FieldOffset(16)]
            public int V0;

            [FieldOffset(20)]
            public int V1;

            [FieldOffset(24)]
            public uint Padding0;

            [FieldOffset(28)]
            public uint Padding1;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private struct ClassifySurfaceFacesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Sdf;
            [WriteOnly] public NativeArray<byte> FaceMasks;
            public int Resolution;

            public void Execute(int index)
            {
                int res = Resolution;
                int z = index / (res * res);
                int rem = index - z * res * res;
                int y = rem / res;
                int x = rem - y * res;
                if (x <= 0 || y <= 0 || z <= 0 || x >= res - 1 || y >= res - 1 || z >= res - 1)
                {
                    FaceMasks[index] = 0;
                    return;
                }

                if (!IsSolidAt(x, y, z))
                {
                    FaceMasks[index] = 0;
                    return;
                }

                byte mask = 0;
                if (!IsSolidAt(x + 1, y, z))
                    mask |= FacePositiveX;
                if (!IsSolidAt(x - 1, y, z))
                    mask |= FaceNegativeX;
                if (!IsSolidAt(x, y + 1, z))
                    mask |= FacePositiveY;
                if (!IsSolidAt(x, y - 1, z))
                    mask |= FaceNegativeY;
                if (!IsSolidAt(x, y, z + 1))
                    mask |= FacePositiveZ;
                if (!IsSolidAt(x, y, z - 1))
                    mask |= FaceNegativeZ;
                FaceMasks[index] = mask;
            }

            private bool IsSolidAt(int x, int y, int z)
            {
                int res = Resolution;
                x = math.clamp(x, 0, res - 1);
                y = math.clamp(y, 0, res - 1);
                z = math.clamp(z, 0, res - 1);
                return Sdf[x + (y * res) + (z * res * res)] <= 0f;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private struct PopulateSdfJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<float> Sdf;
            public int Resolution;
            public float RadiusMeters;
            public float HeightMeters;
            public float NoiseAmplitudeMeters;
            public float StrataFrequency;
            public uint Seed;

            public void Execute(int index)
            {
                int res = Resolution;
                int z = index / (res * res);
                int rem = index - z * res * res;
                int y = rem / res;
                int x = rem - y * res;

                float3 uvw = new float3(x, y, z) / math.max(1f, res - 1f);
                float3 centered = (uvw - 0.5f) * new float3(RadiusMeters * 2f, HeightMeters, RadiusMeters * 2f);
                float ellipsoid = math.length(new float3(centered.x / RadiusMeters, centered.y / (HeightMeters * 0.5f), centered.z / RadiusMeters)) - 1f;
                float strata = math.sin((centered.y + RadiusMeters) * StrataFrequency) * 0.045f;
                float simplex = noise.snoise((centered * 0.24f) + new float3((float)(Seed & 31u), (float)((Seed >> 5) & 31u), (float)((Seed >> 10) & 31u)));
                float worley = Worley(centered * 0.18f, Seed);
                float fractures = (worley - 0.48f) * 0.22f;
                Sdf[index] = ellipsoid + strata - simplex * NoiseAmplitudeMeters * 0.035f + fractures;
            }

            private static float Worley(float3 p, uint seed)
            {
                int3 cell = (int3)math.floor(p);
                float minDist = 10f;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int3 c = cell + new int3(dx, dy, dz);
                            uint h = math.hash(new uint4((uint)c.x, (uint)c.y, (uint)c.z, seed));
                            float3 jitter = new float3(
                                (h & 255u) / 255f,
                                ((h >> 8) & 255u) / 255f,
                                ((h >> 16) & 255u) / 255f);
                            float3 feature = c + jitter;
                            minDist = math.min(minDist, math.length(feature - p));
                        }
                    }
                }

                return math.saturate(minDist);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private struct HydraulicErosionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> SourceSdf;
            [WriteOnly] public NativeArray<float> OutputSdf;
            public int Resolution;
            public int DropletEquivalentIterations;
            public float ErosionStrength;
            public float SedimentStrength;
            public uint Seed;

            public void Execute(int index)
            {
                int res = Resolution;
                int z = index / (res * res);
                int rem = index - z * res * res;
                int y = rem / res;
                int x = rem - y * res;
                if (x <= 0 || y <= 0 || z <= 0 || x >= res - 1 || y >= res - 1 || z >= res - 1)
                {
                    OutputSdf[index] = SourceSdf[index];
                    return;
                }

                float center = SourceSdf[index];
                float gx = SourceSdf[index + 1] - SourceSdf[index - 1];
                float gy = SourceSdf[index + res] - SourceSdf[index - res];
                float gz = SourceSdf[index + res * res] - SourceSdf[index - res * res];
                float3 gradient = new float3(gx, gy, gz);
                float gradientLength = math.sqrt(math.lengthsq(gradient) + 0.000001f);
                float downBias = math.saturate(-gy / gradientLength);
                float ravineNoise = noise.snoise(new float3(x, y, z) * 0.173f + new float3((float)(Seed & 15u)));
                float drops01 = math.saturate(DropletEquivalentIterations / 50000f);
                float erosion = downBias * math.saturate(ravineNoise * 0.5f + 0.5f) * ErosionStrength * drops01;
                float deposition = math.saturate(1f - gradientLength) * SedimentStrength * drops01;
                OutputSdf[index] = center + deposition - erosion;
            }
        }
    }
}
