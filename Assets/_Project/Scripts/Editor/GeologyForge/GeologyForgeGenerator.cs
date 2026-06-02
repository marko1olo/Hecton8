using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.GeologyForge
{
    internal static unsafe class GeologyForgeGenerator
    {
        private const MeshUpdateFlags MeshFlags =
            MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontValidateIndices |
            MeshUpdateFlags.DontNotifyMeshUsers;
        private const string AsyncBakeProgressTitle = "Geology Forge";
        private const string AsyncBakeProgressMessage = "Baking geology profiles";
        private const MeshColliderCookingOptions CollisionCookingOptions =
            MeshColliderCookingOptions.CookForFasterSimulation |
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices;

        private static readonly Stopwatch _Stopwatch = new Stopwatch();
        private static readonly List<GeologyBakeProfile> _menuProfiles = new List<GeologyBakeProfile>(16);
        private static List<GeologyBakeProfile> _asyncProfiles;
        private static List<GeologyBakeMetrics> _asyncMetrics;
        private static List<GeologyMeshManifestRecord> _asyncManifestRecords;
        private static Action<float> _asyncProgressCallback;
        private static int _asyncProfileIndex;
        private static int _asyncVariationIndex;
        private static int _asyncCompletedBakes;
        private static int _asyncTotalBakes;
        private static bool _asyncSaveAssets;
        private static bool _asyncAssetEditing;

        static GeologyForgeGenerator()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= CancelAsyncBake;
            AssemblyReloadEvents.beforeAssemblyReload += CancelAsyncBake;
        }

        private struct MeshLodSet
        {
            public Mesh Lod0;
            public Mesh Lod1;
            public Mesh Lod2;
        }

        [MenuItem("HECTON-8/Geology Forge/Bake CSV Profiles", false, 180)]
        public static void BakeCsvProfilesMenu()
        {
            if (!TryLoadCsvProfiles(_menuProfiles, "bake request rejected"))
                return;

            if (!BakeProfilesAsync(_menuProfiles, true))
                Debug.LogWarning("Geology Forge async bake request ignored: no profiles loaded or a bake is already running.");
        }

        [MenuItem("HECTON-8/Geology Forge/Bake 1606 Abyssal Validation Set", false, 181)]
        public static void BakeAgent1606ValidationSetMenu()
        {
            _menuProfiles.Clear();
            AddAgent1606ValidationProfiles(_menuProfiles);
            if (!BakeProfilesAsync(_menuProfiles, true))
                Debug.LogWarning("Geology Forge 1606 bake request ignored: no profiles loaded or a bake is already running.");
        }

        internal static void AddAgent1606ValidationProfiles(List<GeologyBakeProfile> profiles)
        {
            if (profiles == null)
                throw new ArgumentNullException(nameof(profiles));

            profiles.Add(CreateAgent1606Profile("Sedimentary_Boulder", 0x53454431u, 36, 1, 2.35f, 0.82f, 1.25f, 0.18f, 0.28f, 0.62f, 5, 32, -0.02f, 0.42f, 11000, 4800, 900, new double3(1606d, -220d, 41000d)));
            profiles.Add(CreateAgent1606Profile("Volcanic_Basalt", 0x42415331u, 44, 1, 2.15f, 2.05f, 1.38f, 0.24f, 0.9f, 0.42f, 6, 40, 0.01f, 0.68f, 17000, 7200, 1200, new double3(1606d, -540d, 42400d)));
            profiles.Add(CreateAgent1606Profile("Thermal_Vent_Spire", 0x56454E54u, 44, 1, 1.65f, 2.7f, 1.55f, 0.22f, 0.78f, 0.58f, 6, 48, 0.03f, 0.75f, 18000, 8000, 1300, new double3(1606d, -810d, 43800d)));
        }

        internal static bool TryLoadCsvProfiles(List<GeologyBakeProfile> profiles, string failureContext)
        {
            if (profiles == null)
                throw new ArgumentNullException(nameof(profiles));

            try
            {
                GeologyProfileCsv.LoadProfiles(profiles);
                return profiles.Count > 0;
            }
            catch (Exception ex)
            {
                profiles.Clear();
                Debug.LogWarning("Geology Forge CSV profile load failed; " + failureContext + ". " + ex.Message);
                return false;
            }
        }

        private static GeologyBakeProfile CreateAgent1606Profile(
            string name,
            uint seed,
            int resolution,
            int variations,
            float radius,
            float height,
            float frequency,
            float amplitude,
            float ridged,
            float voronoi,
            int octaves,
            int aoRays,
            float isoLevel,
            float quality,
            int lod0,
            int lod1,
            int lod2,
            double3 sectorAup)
        {
            GeologyBakeProfile profile = default;
            profile.Name = new FixedString64Bytes(name);
            profile.Seed = seed;
            profile.Resolution = resolution;
            profile.Variations = variations;
            profile.RadiusMeters = radius;
            profile.HeightScale = height;
            profile.Frequency = frequency;
            profile.NoiseAmplitude = amplitude;
            profile.RidgedWeight = ridged;
            profile.VoronoiWeight = voronoi;
            profile.Octaves = octaves;
            profile.AmbientOcclusionRays = aoRays;
            profile.IsoLevel = isoLevel;
            profile.GlobalQualityWeight = quality;
            profile.Lod0Budget = lod0;
            profile.Lod1Budget = lod1;
            profile.Lod2Budget = lod2;
            profile.SectorAup = sectorAup;
            return profile;
        }

        public static bool BakeProfilesAsync(List<GeologyBakeProfile> profiles, bool saveAssets, Action<float> progressCallback = null)
        {
            if (_asyncProfiles != null || profiles == null || profiles.Count == 0)
                return false;

            GeologyVertexLayoutValidator.ValidateStruct();
            EnsureAssetFolder(GeologyForgeConstants.MeshOutputFolder);
            EnsureAssetFolder(GeologyForgeConstants.PrefabOutputFolder);
            List<GeologyBakeProfile> copiedProfiles = new List<GeologyBakeProfile>(profiles.Count);
            for (int i = 0; i < profiles.Count; i++)
                copiedProfiles.Add(SanitizeProfile(profiles[i]));
            int totalBakes = CountTotalBakes(copiedProfiles);
            int resultCapacity = ResolveAsyncResultCapacity(totalBakes);
            List<GeologyBakeMetrics> metrics = new List<GeologyBakeMetrics>(resultCapacity);
            List<GeologyMeshManifestRecord> manifestRecords = saveAssets ? new List<GeologyMeshManifestRecord>(resultCapacity) : null;
            try
            {
                _asyncProfiles = copiedProfiles;
                _asyncSaveAssets = saveAssets;
                _asyncTotalBakes = totalBakes;
                _asyncMetrics = metrics;
                _asyncManifestRecords = manifestRecords;
                _asyncProgressCallback = progressCallback;
                _asyncProfileIndex = 0;
                _asyncVariationIndex = 0;
                _asyncCompletedBakes = 0;
                _asyncAssetEditing = false;
                _asyncProgressCallback?.Invoke(0f);
                EditorApplication.update -= TickAsyncBake;
                EditorApplication.update += TickAsyncBake;
                return true;
            }
            catch
            {
                TryFinishAsyncBake(true);
                throw;
            }
        }

        public static void CancelAsyncBake()
        {
            if (_asyncProfiles == null)
                return;

            FinishAsyncBake(true);
        }

        public static GeologyBakeMetrics BakeSingle(GeologyBakeProfile profile, int variation, bool saveAssets)
        {
            NativeArray<GeologyBakeTelemetryEntry> telemetry = default;
            int telemetryCursor = 0;
            try
            {
                telemetry = new NativeArray<GeologyBakeTelemetryEntry>(GeologyForgeConstants.BlackBoxFrameCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                List<GeologyMeshManifestRecord> manifestRecords = saveAssets ? new List<GeologyMeshManifestRecord>(1) : null;
                GeologyBakeMetrics metric = BakeSingle(profile, variation, saveAssets, telemetry, ref telemetryCursor, manifestRecords);
                bool hasManifestRecords = manifestRecords != null && manifestRecords.Count > 0;
                if (saveAssets && hasManifestRecords)
                {
                    WriteMeshManifest(manifestRecords);
                    AssetDatabase.SaveAssets();
                }
                if ((metric.WarningFlags & GeologyForgeConstants.WarningNonFiniteTelemetry) != 0u)
                    TryDumpBlackBox(telemetry, telemetryCursor, GeologyForgeConstants.DumpReasonNonFinite);
                return metric;
            }
            catch
            {
                if (telemetry.IsCreated)
                    TryDumpBlackBox(telemetry, telemetryCursor, GeologyForgeConstants.DumpReasonException);
                throw;
            }
            finally
            {
                if (telemetry.IsCreated)
                    telemetry.Dispose();
            }
        }

        private static void TickAsyncBake()
        {
            if (_asyncProfiles == null)
                return;

            try
            {
                if (_asyncProfileIndex >= _asyncProfiles.Count)
                {
                    FinishAsyncBake(false);
                    return;
                }

                GeologyBakeProfile profile = SanitizeProfile(_asyncProfiles[_asyncProfileIndex]);
                int variations = profile.Variations;
                if (_asyncVariationIndex >= variations)
                {
                    _asyncProfileIndex++;
                    _asyncVariationIndex = 0;
                    return;
                }

                if (!Application.isBatchMode)
                {
                    float progress = (_asyncCompletedBakes + 0.5f) * math.rcp(_asyncTotalBakes);
                    if (EditorUtility.DisplayCancelableProgressBar(AsyncBakeProgressTitle, AsyncBakeProgressMessage, progress))
                    {
                        FinishAsyncBake(true);
                        return;
                    }
                }

                NativeArray<GeologyBakeTelemetryEntry> telemetry = default;
                int telemetryCursor = 0;
                try
                {
                    telemetry = new NativeArray<GeologyBakeTelemetryEntry>(GeologyForgeConstants.BlackBoxFrameCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                    GeologyBakeMetrics metric = BakeSingle(profile, _asyncVariationIndex, _asyncSaveAssets, telemetry, ref telemetryCursor, _asyncManifestRecords);

                    _asyncMetrics.Add(metric);
                    if ((metric.WarningFlags & GeologyForgeConstants.WarningNonFiniteTelemetry) != 0u)
                        TryDumpBlackBox(telemetry, telemetryCursor, GeologyForgeConstants.DumpReasonNonFinite);
                }
                catch
                {
                    if (telemetry.IsCreated)
                        TryDumpBlackBox(telemetry, telemetryCursor, GeologyForgeConstants.DumpReasonException);
                    throw;
                }
                finally
                {
                    if (telemetry.IsCreated)
                        telemetry.Dispose();
                }

                _asyncCompletedBakes++;
                _asyncVariationIndex++;
                _asyncProgressCallback?.Invoke(_asyncCompletedBakes * math.rcp(_asyncTotalBakes));
                if (_asyncProfileIndex == _asyncProfiles.Count - 1 && _asyncVariationIndex >= variations)
                    FinishAsyncBake(false);
            }
            catch (Exception ex)
            {
                TryFinishAsyncBake(true);
                Debug.LogException(ex);
            }
        }

        private static void TryFinishAsyncBake(bool canceled)
        {
            try
            {
                FinishAsyncBake(canceled);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void FinishAsyncBake(bool canceled)
        {
            EditorApplication.update -= TickAsyncBake;
            try
            {
                if (!Application.isBatchMode)
                    EditorUtility.ClearProgressBar();
                if (_asyncAssetEditing)
                    AssetDatabase.StopAssetEditing();
                bool hasMetrics = _asyncMetrics != null && _asyncMetrics.Count > 0;
                bool hasManifestRecords = _asyncManifestRecords != null && _asyncManifestRecords.Count > 0;
                if (_asyncSaveAssets && hasManifestRecords)
                    WriteMeshManifest(_asyncManifestRecords);
                if (_asyncSaveAssets && hasManifestRecords)
                    AssetDatabase.SaveAssets();
                if (hasMetrics)
                    WriteBakeReport(_asyncMetrics);
                _asyncProgressCallback?.Invoke(canceled ? 0f : 1f);
            }
            finally
            {
                _asyncProfiles = null;
                _asyncMetrics = null;
                _asyncManifestRecords = null;
                _asyncProgressCallback = null;
                _asyncProfileIndex = 0;
                _asyncVariationIndex = 0;
                _asyncCompletedBakes = 0;
                _asyncTotalBakes = 0;
                _asyncSaveAssets = false;
                _asyncAssetEditing = false;
            }
        }

        private static GeologyBakeMetrics BakeSingle(
            GeologyBakeProfile profile,
            int variation,
            bool saveAssets,
            NativeArray<GeologyBakeTelemetryEntry> telemetry,
            ref int telemetryCursor,
            List<GeologyMeshManifestRecord> manifestRecords)
        {
            profile = SanitizeProfile(profile);
            uint seed = ResolveAupSeed(profile.SectorAup, unchecked(profile.Seed + (uint)(variation * 0x9E3779B9u)));
            int points = profile.Resolution;
            int cells = points - 1;
            int pointCount = points * points * points;
            int cellCount = cells * cells * cells;
            float extent = math.max(0.5f, profile.RadiusMeters * 2.25f);
            float voxelStep = extent * math.rcp(cells);
            float3 boundsMin = new float3(-extent * 0.5f);
            float qualityCurve = QualityCurve(profile.GlobalQualityWeight);

            NativeArray<float> density = default;
            NativeArray<int> counts = default;
            NativeArray<int> offsets = default;
            NativeArray<GeologyRawVertex> rawVertices = default;
            NativeParallelMultiHashMap<ulong, int> normalBuckets = default;
            JobHandle pendingDisposeFence = default;
            var metric = new GeologyBakeMetrics
            {
                Name = profile.Name,
                Seed = seed,
                VertexStrideBytes = GeologyForgeConstants.VertexStrideBytes
            };

            try
            {
                // COLD ALLOC: NativeArray<float>[pointCount] — editor SDF density scratch fully overwritten by Burst — owner: GeologyForgeGenerator
                density = new NativeArray<float>(pointCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                _Stopwatch.Restart();
                JobHandle sdfHandle = new GenerateMockFractalNoiseJob
                {
                    Density = density,
                    SectorAup = profile.SectorAup,
                    Seed = seed,
                    Points = points,
                    Octaves = profile.Octaves,
                    VoxelStep = voxelStep,
                    RadiusMeters = profile.RadiusMeters,
                    HeightScale = profile.HeightScale,
                    Frequency = profile.Frequency,
                    NoiseAmplitude = math.min(profile.NoiseAmplitude, voxelStep * 1.8f),
                    RidgedWeight = profile.RidgedWeight,
                    VoronoiWeight = profile.VoronoiWeight,
                    IsoLevel = profile.IsoLevel,
                    GlobalQualityWeight = profile.GlobalQualityWeight
                }.Schedule(pointCount, 64);
                pendingDisposeFence = sdfHandle;
                // BLOCKING_SYNC_POINT: editor-only phase fence for SDF timing and deterministic downstream count input.
                sdfHandle.Complete();
                pendingDisposeFence = default;
                _Stopwatch.Stop();
                metric.SdfMilliseconds = _Stopwatch.Elapsed.TotalMilliseconds;
                metric.WarningFlags = RecordTelemetry(telemetry, ref telemetryCursor, profile, seed, 1u, metric, 0, 0, 0, 0, metric.SdfMilliseconds);

                // COLD ALLOC: NativeArray<int>[cellCount] — editor per-cell emitted vertex counts — owner: GeologyForgeGenerator
                counts = new NativeArray<int>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                _Stopwatch.Restart();
                JobHandle countHandle = new SdfCellVertexCountJob
                {
                    Density = density,
                    CellVertexCounts = counts,
                    Points = points,
                    Cells = cells
                }.Schedule(cellCount, 64);
                pendingDisposeFence = countHandle;
                // BLOCKING_SYNC_POINT: CPU prefix-sum reads every count exactly once before allocating extraction offsets.
                countHandle.Complete();
                pendingDisposeFence = default;

                // COLD ALLOC: NativeArray<int>[cellCount] — editor exact extraction offsets — owner: GeologyForgeGenerator
                offsets = new NativeArray<int>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                int rawCount = 0;
                for (int i = 0; i < cellCount; i++)
                {
                    offsets[i] = rawCount;
                    rawCount += counts[i];
                }

                if (rawCount < 3)
                {
                    metric.WarningFlags |= GeologyForgeConstants.WarningEmptySurface;
                    metric.WarningFlags = RecordTelemetry(telemetry, ref telemetryCursor, profile, seed, 2u, metric, rawCount, 0, 0, 0, _Stopwatch.Elapsed.TotalMilliseconds);
                    return metric;
                }

                // COLD ALLOC: NativeArray<GeologyRawVertex>[rawCount] — exact editor triangle soup output — owner: GeologyForgeGenerator
                rawVertices = new NativeArray<GeologyRawVertex>(rawCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                JobHandle extractHandle = new SdfToMeshExtractionJob
                {
                    Density = density,
                    CellVertexOffsets = offsets,
                    RawVertices = rawVertices,
                    Points = points,
                    Cells = cells,
                    VoxelStep = voxelStep,
                    BoundsMin = boundsMin
                }.Schedule(cellCount, 64);
                pendingDisposeFence = extractHandle;
                // BLOCKING_SYNC_POINT: editor report records extraction timing before normal weld and UV authoring phases.
                extractHandle.Complete();
                pendingDisposeFence = default;
                _Stopwatch.Stop();
                metric.ExtractMilliseconds = _Stopwatch.Elapsed.TotalMilliseconds;
                metric.WarningFlags = RecordTelemetry(telemetry, ref telemetryCursor, profile, seed, 2u, metric, rawCount, 0, 0, 0, metric.ExtractMilliseconds);

                _Stopwatch.Restart();
                float normalWeldTolerance = math.max(voxelStep * 0.03f, 1e-5f);
                float normalBucketSize = math.max(normalWeldTolerance * 2f, 1e-5f);
                // COLD ALLOC: NativeParallelMultiHashMap<ulong,int>[rawCount] — editor normal weld buckets — owner: GeologyForgeGenerator
                normalBuckets = new NativeParallelMultiHashMap<ulong, int>(rawCount, Allocator.TempJob);
                JobHandle bucketHandle = new BuildNormalBucketJob
                {
                    Vertices = rawVertices,
                    Buckets = normalBuckets.AsParallelWriter(),
                    PositionBucketSize = normalBucketSize
                }.Schedule(rawCount, 64);
                pendingDisposeFence = bucketHandle;
                // BLOCKING_SYNC_POINT: bucket storage must have no pending writers before the smoothing job reads it or any exception can dispose it.
                bucketHandle.Complete();
                pendingDisposeFence = default;
                JobHandle normalHandle = new CalculateSmoothNormalsJob
                {
                    Density = density,
                    NormalBuckets = normalBuckets,
                    Vertices = (GeologyRawVertex*)Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafePtr(rawVertices),
                    VertexCount = rawCount,
                    Points = points,
                    VoxelStep = voxelStep,
                    PositionBucketSize = normalBucketSize,
                    PositionToleranceSq = normalWeldTolerance * normalWeldTolerance,
                    BoundsMin = boundsMin
                }.Schedule(rawCount, 64);
                pendingDisposeFence = normalHandle;
                JobHandle uvHandle = new GenerateTriplanarUvsJob
                {
                    Vertices = rawVertices,
                    TextureScale = math.lerp(0.12f, 0.55f, qualityCurve)
                }.Schedule(rawCount, 64, normalHandle);
                pendingDisposeFence = uvHandle;
                // BLOCKING_SYNC_POINT: editor attribute phase owns normal/tangent/UV completion before AO and LOD consumers.
                uvHandle.Complete();
                pendingDisposeFence = default;
                _Stopwatch.Stop();
                metric.AttributeMilliseconds = _Stopwatch.Elapsed.TotalMilliseconds;
                metric.WarningFlags = RecordTelemetry(telemetry, ref telemetryCursor, profile, seed, 3u, metric, rawCount, 0, 0, 0, metric.AttributeMilliseconds);

                _Stopwatch.Restart();
                JobHandle aoHandle = new BakeVertexOcclusionJob
                {
                    Density = density,
                    Vertices = rawVertices,
                    Points = points,
                    RayCount = math.clamp((int)math.round(math.lerp(8f, profile.AmbientOcclusionRays, qualityCurve)), 1, GeologyForgeConstants.MaximumAoRays),
                    StepsPerRay = math.clamp((int)math.round(math.lerp(2f, 9f, qualityCurve)), 2, 9),
                    Seed = seed,
                    VoxelStep = voxelStep,
                    MaxDistance = profile.RadiusMeters * math.lerp(0.24f, 0.9f, qualityCurve),
                    BoundsMin = boundsMin
                }.Schedule(rawCount, 64);
                pendingDisposeFence = aoHandle;
                // BLOCKING_SYNC_POINT: baked AO must be present in vertex color before deterministic LOD packing reads vertices.
                aoHandle.Complete();
                pendingDisposeFence = default;
                _Stopwatch.Stop();
                metric.AoMilliseconds = _Stopwatch.Elapsed.TotalMilliseconds;
                metric.WarningFlags = RecordTelemetry(telemetry, ref telemetryCursor, profile, seed, 4u, metric, rawCount, 0, 0, 0, metric.AoMilliseconds);

                _Stopwatch.Restart();
                MeshLodSet lods = BuildLods(profile, rawVertices, rawCount, voxelStep, out int lod0, out int lod1, out int lod2);
                try
                {
                    metric.Lod0Triangles = lod0;
                    metric.Lod1Triangles = lod1;
                    metric.Lod2Triangles = lod2;
                    metric.CollisionTriangles = GeologyForgeConstants.CollisionProxyTriangleCount;
                    if (lod0 > profile.Lod0Budget)
                        metric.WarningFlags |= GeologyForgeConstants.WarningTriangleBudgetExceeded;
                    if (saveAssets)
                        metric.CollisionTriangles = SaveMeshesAndManifest(profile, seed, variation, lods, lod0, lod1, lod2, manifestRecords);
                    _Stopwatch.Stop();
                    metric.SerializationMilliseconds = _Stopwatch.Elapsed.TotalMilliseconds;
                    metric.WarningFlags = RecordTelemetry(telemetry, ref telemetryCursor, profile, seed, 5u, metric, rawCount, lod0, lod1, lod2, metric.SerializationMilliseconds);
                    return metric;
                }
                finally
                {
                    if (!saveAssets)
                        DestroyTransientLods(lods);
                }
            }
            finally
            {
                pendingDisposeFence.Complete();
                if (normalBuckets.IsCreated) normalBuckets.Dispose();
                if (rawVertices.IsCreated) rawVertices.Dispose();
                if (offsets.IsCreated) offsets.Dispose();
                if (counts.IsCreated) counts.Dispose();
                if (density.IsCreated) density.Dispose();
            }
        }

        private static void DestroyTransientLods(MeshLodSet lods)
        {
            if (lods.Lod0 != null)
                UnityEngine.Object.DestroyImmediate(lods.Lod0);
            if (lods.Lod1 != null)
                UnityEngine.Object.DestroyImmediate(lods.Lod1);
            if (lods.Lod2 != null)
                UnityEngine.Object.DestroyImmediate(lods.Lod2);
        }

        private static void DestroyUnsavedLods(MeshLodSet lods, bool lod0AssetOwned, bool lod1AssetOwned, bool lod2AssetOwned)
        {
            if (!lod0AssetOwned && lods.Lod0 != null)
                UnityEngine.Object.DestroyImmediate(lods.Lod0);
            if (!lod1AssetOwned && lods.Lod1 != null)
                UnityEngine.Object.DestroyImmediate(lods.Lod1);
            if (!lod2AssetOwned && lods.Lod2 != null)
                UnityEngine.Object.DestroyImmediate(lods.Lod2);
        }

        private static uint RecordTelemetry(
            NativeArray<GeologyBakeTelemetryEntry> telemetry,
            ref int cursor,
            GeologyBakeProfile profile,
            uint seed,
            uint stage,
            GeologyBakeMetrics metric,
            int rawVertexCount,
            int lod0Triangles,
            int lod1Triangles,
            int lod2Triangles,
            double stageMilliseconds)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return metric.WarningFlags;

            uint warningFlags = metric.WarningFlags;
            float ms = 0f;
            if (!double.IsNaN(stageMilliseconds) && !double.IsInfinity(stageMilliseconds) && stageMilliseconds >= 0d && stageMilliseconds < float.MaxValue)
                ms = (float)stageMilliseconds;
            else
                warningFlags |= GeologyForgeConstants.WarningNonFiniteTelemetry;

            int index = cursor % telemetry.Length;
            telemetry[index] = new GeologyBakeTelemetryEntry
            {
                SectorAup = profile.SectorAup,
                Seed = seed,
                Stage = stage,
                StageMilliseconds = ms,
                RawVertexCount = math.max(0, rawVertexCount),
                Lod0Triangles = math.max(0, lod0Triangles),
                Lod1Triangles = math.max(0, lod1Triangles),
                Lod2Triangles = math.max(0, lod2Triangles),
                WarningFlags = warningFlags,
                StateHash = MixTelemetryHash(seed ^ ((uint)rawVertexCount * 0x9E3779B9u) ^ (stage * 0x85EBCA6Bu) ^ ((uint)lod0Triangles << 1) ^ ((uint)lod1Triangles << 2) ^ ((uint)lod2Triangles << 3)),
                DumpReason = (warningFlags & GeologyForgeConstants.WarningNonFiniteTelemetry) != 0u ? GeologyForgeConstants.DumpReasonNonFinite : 0u
            };
            cursor++;
            return warningFlags;
        }

        private static uint MixTelemetryHash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        private static void DumpBlackBox(NativeArray<GeologyBakeTelemetryEntry> telemetry, int cursor, uint reason)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return;

            EnsureLittleEndianHost();
            EnsureFileFolder(GeologyForgeConstants.DumpPath);
            string tempPath = GeologyForgeConstants.DumpPath + ".tmp";
            DeleteIfExists(tempPath);
            GeologyBakeDumpHeader header = new GeologyBakeDumpHeader
            {
                Magic = GeologyForgeConstants.DumpMagic,
                EntryCount = (uint)telemetry.Length,
                EntrySize = (uint)UnsafeUtility.SizeOf<GeologyBakeTelemetryEntry>(),
                Cursor = (uint)math.max(0, cursor),
                Reason = reason,
                Reserved0 = 0u,
                Reserved1 = 0UL
            };

            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(new ReadOnlySpan<byte>((byte*)&header, UnsafeUtility.SizeOf<GeologyBakeDumpHeader>()));
                    byte* entries = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetry);
                    stream.Write(new ReadOnlySpan<byte>(entries, UnsafeUtility.SizeOf<GeologyBakeTelemetryEntry>() * telemetry.Length));
                }

                ReplacePayloadFile(tempPath, GeologyForgeConstants.DumpPath, true);
            }
            catch
            {
                DeleteIfExists(tempPath);
                throw;
            }
        }

        private static void TryDumpBlackBox(NativeArray<GeologyBakeTelemetryEntry> telemetry, int cursor, uint reason)
        {
            try
            {
                DumpBlackBox(telemetry, cursor, reason);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void WriteMeshManifest(List<GeologyMeshManifestRecord> records)
        {
            if (records == null || records.Count == 0)
                return;

            GeologyVertexLayoutValidator.ValidateStruct();
            EnsureLittleEndianHost();
            EnsureFileFolder(GeologyForgeConstants.ManifestPath);
            int count = records != null ? records.Count : 0;
            GeologyMeshManifestHeader header = new GeologyMeshManifestHeader
            {
                Magic = GeologyForgeConstants.ManifestMagic,
                Version = GeologyForgeConstants.ManifestVersion,
                RecordCount = (uint)math.max(0, count),
                RecordSize = (uint)UnsafeUtility.SizeOf<GeologyMeshManifestRecord>(),
                HeaderSize = (uint)UnsafeUtility.SizeOf<GeologyMeshManifestHeader>(),
                VertexStrideBytes = GeologyForgeConstants.VertexStrideBytes,
                LodCount = (uint)GeologyForgeConstants.LodCount,
                Flags = GeologyForgeConstants.ManifestFlagBrgReady,
                Reserved0 = 0UL,
                Reserved1 = 0UL,
                Reserved2 = 0UL,
                Reserved3 = 0UL
            };

            string tempPath = GeologyForgeConstants.ManifestPath + ".tmp";
            DeleteIfExists(tempPath);
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(new ReadOnlySpan<byte>((byte*)&header, UnsafeUtility.SizeOf<GeologyMeshManifestHeader>()));
                    for (int i = 0; i < count; i++)
                    {
                        GeologyMeshManifestRecord record = records[i];
                        stream.Write(new ReadOnlySpan<byte>((byte*)&record, UnsafeUtility.SizeOf<GeologyMeshManifestRecord>()));
                    }
                }

                ReplacePayloadFile(tempPath, GeologyForgeConstants.ManifestPath, true);
            }
            catch
            {
                DeleteIfExists(tempPath);
                throw;
            }

            AssetDatabase.ImportAsset(GeologyForgeConstants.ManifestPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static MeshLodSet BuildLods(GeologyBakeProfile profile, NativeArray<GeologyRawVertex> sourceVertices, int sourceVertexCount, float voxelStep, out int lod0, out int lod1, out int lod2)
        {
            int sourceTriangles = sourceVertexCount / 3;
            float qualityCurve = QualityCurve(profile.GlobalQualityWeight);
            int lod0Budget = math.max(32, (int)math.round(math.lerp(profile.Lod0Budget * 0.55f, profile.Lod0Budget, qualityCurve)));
            int lod1Budget = math.max(16, (int)math.round(math.lerp(profile.Lod1Budget * 0.42f, profile.Lod1Budget, qualityCurve)));
            int lod2Budget = math.max(8, (int)math.round(math.lerp(profile.Lod2Budget * 0.32f, profile.Lod2Budget, qualityCurve)));
            lod0 = 0;
            lod1 = 0;
            lod2 = 0;
            MeshLodSet lods = default;
            try
            {
                lods.Lod0 = BuildLodMesh("LOD0", sourceVertices, sourceTriangles, math.min(sourceTriangles, lod0Budget), voxelStep * math.lerp(0.3f, 0.08f, qualityCurve), 0, out lod0);
                lods.Lod1 = BuildLodMesh("LOD1", sourceVertices, sourceTriangles, math.min(sourceTriangles, lod1Budget), voxelStep * math.lerp(1.45f, 0.65f, qualityCurve), 1, out lod1);
                lods.Lod2 = BuildLodMesh("LOD2", sourceVertices, sourceTriangles, math.min(sourceTriangles, lod2Budget), voxelStep * math.lerp(3.6f, 1.8f, qualityCurve), 2, out lod2);
                return lods;
            }
            catch
            {
                DestroyTransientLods(lods);
                throw;
            }
        }

        private static Mesh BuildLodMesh(string lodName, NativeArray<GeologyRawVertex> sourceVertices, int sourceTriangles, int targetTriangles, float collapseCellSize, byte lodMask, out int triangleCount)
        {
            int safeTargetTriangles = math.clamp(targetTriangles, 1, math.max(1, sourceTriangles));
            int outputVertexCount = safeTargetTriangles * 3;
            triangleCount = safeTargetTriangles;
            NativeArray<GeologyRawVertex> lodVertices = default;
            try
            {
                // COLD ALLOC: NativeArray<GeologyRawVertex>[outputVertexCount] — editor LOD decimation output — owner: GeologyForgeGenerator
                lodVertices = new NativeArray<GeologyRawVertex>(outputVertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                JobHandle decimateHandle = new GeologyLodDecimationJob
                {
                    SourceVertices = sourceVertices,
                    OutputVertices = lodVertices,
                    SourceTriangleCount = math.max(1, sourceTriangles),
                    OutputTriangleCount = safeTargetTriangles,
                    CollapseCellSize = collapseCellSize
                }.Schedule(safeTargetTriangles, 64);
                // BLOCKING_SYNC_POINT: Unity Mesh upload consumes the completed editor LOD vertex buffer immediately after this fence.
                decimateHandle.Complete();
                return CreateUnityMesh(lodName, lodVertices, outputVertexCount, lodMask);
            }
            finally
            {
                if (lodVertices.IsCreated) lodVertices.Dispose();
            }
        }

        private static Mesh CreateUnityMesh(string lodName, NativeArray<GeologyRawVertex> rawVertices, int vertexCount, byte lodMask)
        {
            NativeArray<GeologyVertex32> packed = default;
            NativeArray<uint> indices = default;
            Mesh mesh = null;
            try
            {
                // COLD ALLOC: NativeArray<GeologyVertex32>[vertexCount] — editor GPU upload stream — owner: GeologyForgeGenerator
                packed = new NativeArray<GeologyVertex32>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<uint>[vertexCount] — editor linear index stream — owner: GeologyForgeGenerator
                indices = new NativeArray<uint>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                JobHandle packHandle = new GeologyPackVertexJob
                {
                    SourceVertices = rawVertices,
                    PackedVertices = packed,
                    LodMask = lodMask
                }.Schedule(vertexCount, 64);
                JobHandle indexHandle = new GeologyIndexFillJob
                {
                    Indices = indices
                }.Schedule(vertexCount, 64, packHandle);
                // BLOCKING_SYNC_POINT: Unity Mesh API consumes packed vertex/index buffers on the editor thread after this fence.
                indexHandle.Complete();

                mesh = new Mesh
                {
                    name = $"GEN_Geology_{lodName}",
                    indexFormat = IndexFormat.UInt32
                };
                Bounds bounds = CalculateBounds(rawVertices);
                GeologyVertexLayoutValidator.ApplyVertexBufferParams(mesh, vertexCount);
                mesh.SetVertexBufferData(packed, 0, 0, vertexCount, 0, MeshFlags);
                mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
                mesh.SetIndexBufferData(indices, 0, 0, vertexCount, MeshFlags);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount, MeshTopology.Triangles)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                }, MeshFlags);
                mesh.bounds = bounds;
                GeologyVertexLayoutValidator.ValidateMesh(mesh);
                mesh.UploadMeshData(true);
                Mesh result = mesh;
                mesh = null;
                return result;
            }
            finally
            {
                if (mesh != null)
                    UnityEngine.Object.DestroyImmediate(mesh);
                if (indices.IsCreated) indices.Dispose();
                if (packed.IsCreated) packed.Dispose();
            }
        }

        private static int SaveMeshesAndManifest(
            GeologyBakeProfile profile,
            uint seed,
            int variation,
            MeshLodSet lods,
            int lod0Triangles,
            int lod1Triangles,
            int lod2Triangles,
            List<GeologyMeshManifestRecord> manifestRecords)
        {
            string safeName = SanitizeFileName(profile.Name.ToString());
            string stem = $"GEN_Geology_{safeName}_{seed:X8}_{variation.ToString("000", CultureInfo.InvariantCulture)}";
            string path0 = $"{GeologyForgeConstants.MeshOutputFolder}/{stem}_LOD0.asset";
            string path1 = $"{GeologyForgeConstants.MeshOutputFolder}/{stem}_LOD1.asset";
            string path2 = $"{GeologyForgeConstants.MeshOutputFolder}/{stem}_LOD2.asset";
            bool lod0AssetOwned = false;
            bool lod1AssetOwned = false;
            bool lod2AssetOwned = false;
            bool lod0CreatedAsset = false;
            bool lod1CreatedAsset = false;
            bool lod2CreatedAsset = false;
            string backupPath0 = BackupPath(path0);
            string backupPath1 = BackupPath(path1);
            string backupPath2 = BackupPath(path2);
            bool lod0BackupCreated = false;
            bool lod1BackupCreated = false;
            bool lod2BackupCreated = false;
            bool assetEditing = false;
            int manifestStartCount = manifestRecords == null ? -1 : manifestRecords.Count;
            try
            {
                lod0BackupCreated = BackupExistingAsset(path0, backupPath0);
                lod1BackupCreated = BackupExistingAsset(path1, backupPath1);
                lod2BackupCreated = BackupExistingAsset(path2, backupPath2);

                try
                {
                    AssetDatabase.StartAssetEditing();
                    _asyncAssetEditing = true;
                    assetEditing = true;

                    lods.Lod0 = SaveMeshAsset(lods.Lod0, path0, stem, 0, out lod0AssetOwned, out lod0CreatedAsset);
                    lods.Lod1 = SaveMeshAsset(lods.Lod1, path1, stem, 1, out lod1AssetOwned, out lod1CreatedAsset);
                    lods.Lod2 = SaveMeshAsset(lods.Lod2, path2, stem, 2, out lod2AssetOwned, out lod2CreatedAsset);
                }
                finally
                {
                    if (assetEditing)
                    {
                        try
                        {
                            AssetDatabase.StopAssetEditing();
                        }
                        finally
                        {
                            _asyncAssetEditing = false;
                        }
                    }
                }
            }
            catch
            {
                RemoveManifestTail(manifestRecords, manifestStartCount);
                TryCleanupFailedAssetSave(
                    lods,
                    lod0AssetOwned,
                    lod1AssetOwned,
                    lod2AssetOwned,
                    path0,
                    lod0CreatedAsset,
                    path1,
                    lod1CreatedAsset,
                    path2,
                    lod2CreatedAsset,
                    backupPath0,
                    lod0BackupCreated,
                    backupPath1,
                    lod1BackupCreated,
                    backupPath2,
                    lod2BackupCreated);
                throw;
            }

            try
            {
                if (manifestRecords == null)
                {
                    int collisionTriangles = SaveCollisionProxyAndPrefab(stem, lods, path0);
                    DeleteBackupAssets(
                        backupPath0,
                        lod0BackupCreated,
                        backupPath1,
                        lod1BackupCreated,
                        backupPath2,
                        lod2BackupCreated);
                    return collisionTriangles;
                }

                Bounds bounds = CalculateCombinedVisualBounds(lods);
                int savedCollisionTriangles = SaveCollisionProxyAndPrefab(stem, lods, path0);
                ResolveGuid128(path0, out ulong lod0High, out ulong lod0Low);
                ResolveGuid128(path1, out ulong lod1High, out ulong lod1Low);
                ResolveGuid128(path2, out ulong lod2High, out ulong lod2Low);
                manifestRecords.Add(new GeologyMeshManifestRecord
                {
                    SectorAup = profile.SectorAup,
                    Seed = seed,
                    ProfileHash = HashFixedString(profile.Name),
                    Lod0Triangles = lod0Triangles,
                    Lod1Triangles = lod1Triangles,
                    Lod2Triangles = lod2Triangles,
                    VertexStrideBytes = GeologyForgeConstants.VertexStrideBytes,
                    BoundsCenter = new float3(bounds.center.x, bounds.center.y, bounds.center.z),
                    BoundsExtents = new float3(bounds.extents.x, bounds.extents.y, bounds.extents.z),
                    Lod0GuidHigh = lod0High,
                    Lod0GuidLow = lod0Low,
                    Lod1GuidHigh = lod1High,
                    Lod1GuidLow = lod1Low,
                    Lod2GuidHigh = lod2High,
                    Lod2GuidLow = lod2Low,
                    Flags = GeologyForgeConstants.ManifestFlagBrgReady,
                    Variation = (uint)math.max(0, variation)
                });

                DeleteBackupAssets(
                    backupPath0,
                    lod0BackupCreated,
                    backupPath1,
                    lod1BackupCreated,
                    backupPath2,
                    lod2BackupCreated);

                return savedCollisionTriangles;
            }
            catch
            {
                RemoveManifestTail(manifestRecords, manifestStartCount);
                TryCleanupFailedAssetSave(
                    lods,
                    lod0AssetOwned,
                    lod1AssetOwned,
                    lod2AssetOwned,
                    path0,
                    lod0CreatedAsset,
                    path1,
                    lod1CreatedAsset,
                    path2,
                    lod2CreatedAsset,
                    backupPath0,
                    lod0BackupCreated,
                    backupPath1,
                    lod1BackupCreated,
                    backupPath2,
                    lod2BackupCreated);
                throw;
            }
        }

        private static Mesh SaveMeshAsset(Mesh mesh, string path, string stem, int lodIndex, out bool assetOwned, out bool createdAsset)
        {
            assetOwned = false;
            createdAsset = false;
            mesh.name = $"{stem}_LOD{lodIndex}";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                assetOwned = true;
                return existing;
            }

            createdAsset = true;
            AssetDatabase.CreateAsset(mesh, path);
            assetOwned = true;
            return mesh;
        }

        private static int SaveCollisionProxyAndPrefab(string stem, MeshLodSet lods, string lod0Path)
        {
            if (lods.Lod0 == null)
                throw new InvalidOperationException("Cannot create geology prefab without LOD0 mesh for " + lod0Path + ".");
            if (GeologyForgeConstants.CollisionProxyTriangleCount > GeologyForgeConstants.CollisionTriangleBudget)
                throw new InvalidOperationException("Geology collision proxy triangle count exceeds PhysX budget.");

            EnsureAssetFolder(GeologyForgeConstants.MeshOutputFolder);
            EnsureAssetFolder(GeologyForgeConstants.PrefabOutputFolder);
            string collisionPath = $"{GeologyForgeConstants.MeshOutputFolder}/COL_{stem}.asset";
            string prefabPath = $"{GeologyForgeConstants.PrefabOutputFolder}/{stem}.prefab";
            string collisionBackupPath = BackupPath(collisionPath);
            string prefabBackupPath = BackupPath(prefabPath);
            bool collisionBackupCreated = false;
            bool prefabBackupCreated = false;
            bool collisionCreatedAsset = string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(collisionPath));
            bool prefabCreatedAsset = string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(prefabPath));
            try
            {
                collisionBackupCreated = BackupExistingAsset(collisionPath, collisionBackupPath);
                prefabBackupCreated = BackupExistingAsset(prefabPath, prefabBackupPath);
                Mesh collisionMesh = SaveCollisionMeshAsset(CreateCollisionProxyMesh(stem, CalculateCombinedVisualBounds(lods)), collisionPath, stem);
                BakeCollisionMesh(collisionMesh);
                SavePrefabAsset(stem, lods.Lod0, lods.Lod1, lods.Lod2, collisionMesh, prefabPath);
                DeleteBackupAsset(collisionBackupPath, collisionBackupCreated);
                DeleteBackupAsset(prefabBackupPath, prefabBackupCreated);
                return GeologyForgeConstants.CollisionProxyTriangleCount;
            }
            catch
            {
                TryCleanupFailedCollisionAndPrefabSave(
                    collisionPath,
                    collisionCreatedAsset,
                    collisionBackupPath,
                    collisionBackupCreated,
                    prefabPath,
                    prefabCreatedAsset,
                    prefabBackupPath,
                    prefabBackupCreated);
                throw;
            }
        }

        private static Mesh SaveCollisionMeshAsset(Mesh mesh, string path, string stem)
        {
            mesh.name = $"COL_{stem}";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void BakeCollisionMesh(Mesh collisionMesh)
        {
            if (collisionMesh == null)
                throw new InvalidOperationException("Cannot PhysX-bake a null geology collision mesh.");

            if (GeologyForgeConstants.CollisionProxyTriangleCount > GeologyForgeConstants.CollisionTriangleBudget)
                throw new InvalidOperationException("Cannot PhysX-bake geology collision mesh above budget.");

            UnityEngine.Physics.BakeMesh(collisionMesh.GetEntityId(), true, CollisionCookingOptions);
        }

        private static Mesh CreateCollisionProxyMesh(string stem, Bounds visualBounds)
        {
            Vector3 center = IsFiniteVector(visualBounds.center) ? visualBounds.center : Vector3.zero;
            Vector3 extents = IsFiniteVector(visualBounds.extents) ? visualBounds.extents : Vector3.one * 0.5f;
            extents = Vector3.Max(extents + new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.08f, 0.08f, 0.08f));
            Vector3 min = center - extents;
            Vector3 max = center + extents;
            var vertices = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };
            var triangles = new[]
            {
                0, 2, 1, 1, 2, 3,
                4, 5, 6, 5, 7, 6,
                0, 1, 4, 1, 5, 4,
                2, 6, 3, 3, 6, 7,
                0, 4, 2, 2, 4, 6,
                1, 3, 5, 3, 7, 5
            };
            Mesh mesh = new Mesh
            {
                name = $"COL_{stem}",
                indexFormat = IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.bounds = new Bounds(center, extents * 2f);
            mesh.RecalculateNormals();
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static Bounds CalculateCombinedVisualBounds(MeshLodSet lods)
        {
            if (lods.Lod0 == null)
                return new Bounds(Vector3.zero, Vector3.one);

            Bounds bounds = lods.Lod0.bounds;
            if (lods.Lod1 != null)
                bounds.Encapsulate(lods.Lod1.bounds);
            if (lods.Lod2 != null)
                bounds.Encapsulate(lods.Lod2.bounds);
            return bounds;
        }

        private static void SavePrefabAsset(string stem, Mesh lod0, Mesh lod1, Mesh lod2, Mesh collisionMesh, string prefabPath)
        {
            Material material = ResolveGeologyMaterial();
            Bounds visualBounds = CalculateCombinedVisualBounds(new MeshLodSet { Lod0 = lod0, Lod1 = lod1, Lod2 = lod2 });
            StaticEditorFlags rendererFlags = ResolveRendererStaticFlags(visualBounds);
            GameObject root = new GameObject(stem);
            try
            {
                GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                MeshCollider collider = root.AddComponent<MeshCollider>();
                collider.convex = true;
                collider.cookingOptions = CollisionCookingOptions;
                collider.sharedMesh = collisionMesh;

                LODGroup lodGroup = root.AddComponent<LODGroup>();
                Renderer r0 = CreateLodRenderer(root, "VIS_LOD0", lod0, material, rendererFlags);
                Renderer r1 = CreateLodRenderer(root, "VIS_LOD1", lod1 != null ? lod1 : lod0, material, rendererFlags);
                Renderer r2 = CreateLodRenderer(root, "VIS_LOD2", lod2 != null ? lod2 : lod0, material, rendererFlags);
                lodGroup.SetLODs(new[]
                {
                    new LOD(0.55f, new[] { r0 }),
                    new LOD(0.22f, new[] { r1 }),
                    new LOD(0.04f, new[] { r2 })
                });
                lodGroup.RecalculateBounds();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                    throw new InvalidOperationException("Failed to save geology prefab " + prefabPath + ".");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Renderer CreateLodRenderer(GameObject root, string name, Mesh mesh, Material material, StaticEditorFlags rendererFlags)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            GameObjectUtility.SetStaticEditorFlags(child, rendererFlags);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            ConfigureStaticRockRenderer(renderer);
            if (material != null)
                renderer.sharedMaterial = material;
            return renderer;
        }

        private static void ConfigureStaticRockRenderer(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        }

        private static StaticEditorFlags ResolveRendererStaticFlags(Bounds visualBounds)
        {
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            if (CalculateBoundsVolume(visualBounds) >= GeologyForgeConstants.OccluderStaticMinimumVolumeCubicMeters)
                flags |= StaticEditorFlags.OccluderStatic;
            return flags;
        }

        private static float CalculateBoundsVolume(Bounds bounds)
        {
            Vector3 size = bounds.size;
            if (!IsFiniteVector(size))
                return 0f;
            return math.max(0f, size.x) * math.max(0f, size.y) * math.max(0f, size.z);
        }

        private static Material ResolveGeologyMaterial()
        {
            string[] preferredPaths =
            {
                "Assets/_Project/Art/Materials/Mat_TriplanarRock.mat",
                "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_rock_cluster_medium.mat",
                "Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_rock_cluster_medium_Placeholder.mat"
            };
            for (int i = 0; i < preferredPaths.Length; i++)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(preferredPaths[i]);
                if (material != null)
                    return material;
            }

            string[] guids = AssetDatabase.FindAssets("TriplanarRock t:Material", new[] { "Assets/_Project" });
            if (guids != null && guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return null;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static void ResolveGuid128(string assetPath, out ulong high, out ulong low)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid) || guid.Length != 32)
                throw new InvalidOperationException("Invalid geology mesh asset GUID for " + assetPath + ".");

            high = ParseHex64(guid, 0, assetPath);
            low = ParseHex64(guid, 16, assetPath);
        }

        private static ulong ParseHex64(string value, int start, string assetPath)
        {
            ulong result = 0UL;
            for (int i = start; i < start + 16; i++)
            {
                uint nibble = HexNibble(value[i], assetPath);
                result = (result << 4) | nibble;
            }

            return result;
        }

        private static uint HexNibble(char c, string assetPath)
        {
            if (c >= '0' && c <= '9')
                return (uint)(c - '0');
            if (c >= 'a' && c <= 'f')
                return (uint)(10 + c - 'a');
            if (c >= 'A' && c <= 'F')
                return (uint)(10 + c - 'A');
            throw new InvalidOperationException("Invalid geology mesh asset GUID hex digit for " + assetPath + ".");
        }

        private static void TryCleanupFailedAssetSave(
            MeshLodSet lods,
            bool lod0AssetOwned,
            bool lod1AssetOwned,
            bool lod2AssetOwned,
            string path0,
            bool path0Created,
            string path1,
            bool path1Created,
            string path2,
            bool path2Created,
            string backupPath0,
            bool backup0Created,
            string backupPath1,
            bool backup1Created,
            string backupPath2,
            bool backup2Created)
        {
            try
            {
                RestoreBackupAssets(
                    path0,
                    backupPath0,
                    backup0Created,
                    path1,
                    backupPath1,
                    backup1Created,
                    path2,
                    backupPath2,
                    backup2Created);
                DeleteCreatedAssets(path0, path0Created, path1, path1Created, path2, path2Created);
                DeleteBackupAssets(backupPath0, backup0Created, backupPath1, backup1Created, backupPath2, backup2Created);
                DestroyUnsavedLods(lods, lod0AssetOwned, lod1AssetOwned, lod2AssetOwned);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void RemoveManifestTail(List<GeologyMeshManifestRecord> manifestRecords, int manifestStartCount)
        {
            if (manifestRecords == null || manifestStartCount < 0 || manifestRecords.Count <= manifestStartCount)
                return;

            manifestRecords.RemoveRange(manifestStartCount, manifestRecords.Count - manifestStartCount);
        }

        private static string BackupPath(string assetPath)
        {
            int slashIndex = assetPath.LastIndexOf('/');
            string fileName = slashIndex >= 0 ? assetPath.Substring(slashIndex + 1) : assetPath;
            int extensionIndex = fileName.LastIndexOf('.');
            if (extensionIndex < 0)
                return GeologyForgeConstants.MeshOutputFolder + "/_H8Backups/" + fileName + "_H8BACKUP.asset";

            return GeologyForgeConstants.MeshOutputFolder + "/_H8Backups/" + fileName.Substring(0, extensionIndex) + "_H8BACKUP" + fileName.Substring(extensionIndex);
        }

        private static bool BackupExistingAsset(string assetPath, string backupPath)
        {
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
                return false;

            EnsureAssetFolder(GeologyForgeConstants.MeshOutputFolder + "/_H8Backups");
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(backupPath)))
                AssetDatabase.DeleteAsset(backupPath);

            if (!AssetDatabase.CopyAsset(assetPath, backupPath))
                throw new InvalidOperationException("Failed to create geology asset backup for " + assetPath + ".");

            return true;
        }

        private static void RestoreBackupAssets(
            string path0,
            string backupPath0,
            bool backup0Created,
            string path1,
            string backupPath1,
            bool backup1Created,
            string path2,
            string backupPath2,
            bool backup2Created)
        {
            RestoreBackupAsset(path2, backupPath2, backup2Created);
            RestoreBackupAsset(path1, backupPath1, backup1Created);
            RestoreBackupAsset(path0, backupPath0, backup0Created);
        }

        private static void RestoreBackupAsset(string assetPath, string backupPath, bool backupCreated)
        {
            if (!backupCreated)
                return;

            UnityEngine.Object backup = AssetDatabase.LoadMainAssetAtPath(backupPath);
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (backup == null)
                throw new InvalidOperationException("Failed to restore geology asset backup for " + assetPath + ".");

            if (existing == null)
            {
                if (!AssetDatabase.CopyAsset(backupPath, assetPath))
                    throw new InvalidOperationException("Failed to restore missing geology asset backup for " + assetPath + ".");
                return;
            }

            if (backup is Mesh backupMesh && existing is Mesh existingMesh)
            {
                EditorUtility.CopySerialized(backupMesh, existingMesh);
                return;
            }

            FileUtil.ReplaceFile(backupPath, assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
                throw new InvalidOperationException("Failed to restore geology asset backup for " + assetPath + ".");
        }

        private static void DeleteBackupAssets(
            string backupPath0,
            bool backup0Created,
            string backupPath1,
            bool backup1Created,
            string backupPath2,
            bool backup2Created)
        {
            DeleteBackupAsset(backupPath2, backup2Created);
            DeleteBackupAsset(backupPath1, backup1Created);
            DeleteBackupAsset(backupPath0, backup0Created);
        }

        private static void DeleteBackupAsset(string backupPath, bool backupCreated)
        {
            if (backupCreated && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(backupPath)))
                AssetDatabase.DeleteAsset(backupPath);
        }

        private static void DeleteCreatedAssets(
            string path0,
            bool path0Created,
            string path1,
            bool path1Created,
            string path2,
            bool path2Created)
        {
            if (path2Created)
                DeleteCreatedAsset(path2);
            if (path1Created)
                DeleteCreatedAsset(path1);
            if (path0Created)
                DeleteCreatedAsset(path0);
        }

        private static void DeleteCreatedAsset(string path)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                AssetDatabase.DeleteAsset(path);
        }

        private static void TryCleanupFailedCollisionAndPrefabSave(
            string collisionPath,
            bool collisionCreatedAsset,
            string collisionBackupPath,
            bool collisionBackupCreated,
            string prefabPath,
            bool prefabCreatedAsset,
            string prefabBackupPath,
            bool prefabBackupCreated)
        {
            try
            {
                RestoreBackupAsset(collisionPath, collisionBackupPath, collisionBackupCreated);
                RestoreBackupAsset(prefabPath, prefabBackupPath, prefabBackupCreated);
                if (prefabCreatedAsset)
                    DeleteCreatedAsset(prefabPath);
                if (collisionCreatedAsset)
                    DeleteCreatedAsset(collisionPath);
                DeleteBackupAsset(prefabBackupPath, prefabBackupCreated);
                DeleteBackupAsset(collisionBackupPath, collisionBackupCreated);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static uint HashFixedString(FixedString64Bytes value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return hash;
        }

        private static void EnsureLittleEndianHost()
        {
            if (!BitConverter.IsLittleEndian)
                throw new InvalidOperationException("Geology binary payload writer requires explicit little-endian output; big-endian host serialization is unsupported.");
        }

        private static Bounds CalculateBounds(NativeArray<GeologyRawVertex> vertices)
        {
            if (!vertices.IsCreated || vertices.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one);

            float3 min = float3.zero;
            float3 max = float3.zero;
            bool hasFinitePosition = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                float3 p = vertices[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;

                if (!hasFinitePosition)
                {
                    min = p;
                    max = p;
                    hasFinitePosition = true;
                    continue;
                }

                min = math.min(min, p);
                max = math.max(max, p);
            }

            if (!hasFinitePosition)
                return new Bounds(Vector3.zero, Vector3.one);

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(0.01f));
            return new Bounds(new Vector3(center.x, center.y, center.z), new Vector3(size.x, size.y, size.z));
        }

        private static GeologyBakeProfile SanitizeProfile(GeologyBakeProfile profile)
        {
            if (profile.Name.Length == 0)
                profile.Name = new FixedString64Bytes("Unnamed_Geology");

            profile.Resolution = math.clamp(profile.Resolution <= 0 ? GeologyForgeConstants.DefaultResolution : profile.Resolution, GeologyForgeConstants.MinimumResolution, GeologyForgeConstants.MaximumResolution);
            profile.Variations = SanitizeVariationCount(profile.Variations);
            profile.RadiusMeters = math.clamp(FiniteOr(profile.RadiusMeters, 2f), 0.25f, GeologyForgeConstants.MaximumRadiusMeters);
            profile.HeightScale = math.clamp(FiniteOr(profile.HeightScale, 1f), 0.15f, GeologyForgeConstants.MaximumHeightScale);
            profile.Frequency = math.clamp(FiniteOr(profile.Frequency, 1f), 0.001f, GeologyForgeConstants.MaximumFrequency);
            profile.NoiseAmplitude = math.clamp(FiniteOr(profile.NoiseAmplitude, 0f), 0f, GeologyForgeConstants.MaximumNoiseAmplitudeMeters);
            profile.RidgedWeight = math.saturate(FiniteOr(profile.RidgedWeight, 0f));
            profile.VoronoiWeight = math.saturate(FiniteOr(profile.VoronoiWeight, 0f));
            profile.IsoLevel = math.clamp(FiniteOr(profile.IsoLevel, 0f), -0.5f, 0.5f);
            profile.GlobalQualityWeight = math.saturate(FiniteOr(profile.GlobalQualityWeight, 0f));
            profile.SectorAup = new double3(
                CanonicalizeAupLane(profile.SectorAup.x),
                CanonicalizeAupLane(profile.SectorAup.y),
                CanonicalizeAupLane(profile.SectorAup.z));
            profile.Octaves = math.clamp(profile.Octaves <= 0 ? 4 : profile.Octaves, 1, 8);
            profile.AmbientOcclusionRays = math.clamp(profile.AmbientOcclusionRays <= 0 ? GeologyForgeConstants.DefaultAoRays : profile.AmbientOcclusionRays, 1, GeologyForgeConstants.MaximumAoRays);
            profile.Lod0Budget = math.max(32, profile.Lod0Budget <= 0 ? GeologyForgeConstants.Lod0TriangleBudget : profile.Lod0Budget);
            profile.Lod1Budget = math.max(16, profile.Lod1Budget <= 0 ? GeologyForgeConstants.Lod1TriangleBudget : profile.Lod1Budget);
            profile.Lod2Budget = math.max(8, profile.Lod2Budget <= 0 ? GeologyForgeConstants.Lod2TriangleBudget : profile.Lod2Budget);
            return profile;
        }

        internal static GeologyBakeProfile SanitizeForEditor(GeologyBakeProfile profile)
        {
            return SanitizeProfile(profile);
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static double FiniteOr(double value, double fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static double CanonicalizeAupLane(double value)
        {
            double finite = FiniteOr(value, 0d);
            return finite == 0d ? 0d : finite;
        }

        private static float QualityCurve(float globalQualityWeight)
        {
            float q = math.saturate(globalQualityWeight);
            return math.smoothstep(0f, 1f, q);
        }

        private static int CountTotalBakes(List<GeologyBakeProfile> profiles)
        {
            int total = 0;
            for (int i = 0; i < profiles.Count; i++)
            {
                int variations = SanitizeVariationCount(profiles[i].Variations);
                if (total > int.MaxValue - variations)
                    return int.MaxValue;
                total += variations;
            }

            return math.max(1, total);
        }

        private static int ResolveAsyncResultCapacity(int totalBakes)
        {
            return math.clamp(totalBakes <= 0 ? 1 : totalBakes, 1, GeologyForgeConstants.MaximumAsyncResultPreallocation);
        }

        private static int SanitizeVariationCount(int variations)
        {
            return math.clamp(variations <= 0 ? 1 : variations, 1, GeologyForgeConstants.MaximumVariations);
        }

        private static uint ResolveAupSeed(double3 sectorAup, uint seed)
        {
            ulong hash = 1469598103934665603UL;
            hash = Fnva(hash, CanonicalDoubleBits(sectorAup.x));
            hash = Fnva(hash, CanonicalDoubleBits(sectorAup.y));
            hash = Fnva(hash, CanonicalDoubleBits(sectorAup.z));
            hash = Fnva(hash, seed);
            return (uint)(hash ^ (hash >> 32));
        }

        private static ulong CanonicalDoubleBits(double value)
        {
            double finite = CanonicalizeAupLane(value);
            return finite == 0d ? 0UL : (ulong)BitConverter.DoubleToInt64Bits(finite);
        }

        private static ulong Fnva(ulong hash, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                hash ^= (value >> (i * 8)) & 0xFFUL;
                hash *= 1099511628211UL;
            }

            return hash;
        }

        private static void WriteBakeReport(List<GeologyBakeMetrics> metrics)
        {
            EnsureFileFolder(GeologyForgeConstants.BakeReportPath);
            var builder = new StringBuilder(4096);
            builder.Append("{\n  \"agent\": \"1606\",\n  \"status\": \"PENDING_VERIFICATION\",\n  \"generatedMeshCount\": ");
            builder.Append(metrics != null ? metrics.Count : 0);
            builder.Append(",\n  \"meshes\": [\n");
            if (metrics != null)
            {
                for (int i = 0; i < metrics.Count; i++)
                {
                    GeologyBakeMetrics m = metrics[i];
                    if (i > 0)
                        builder.Append(",\n");
                    builder.Append("    { \"name\": \"");
                    builder.Append(EscapeJson(m.Name.ToString()));
                    builder.Append("\", \"seed\": ");
                    builder.Append(m.Seed);
                    builder.Append(", \"lod0Tris\": ");
                    builder.Append(m.Lod0Triangles);
                    builder.Append(", \"lod1Tris\": ");
                    builder.Append(m.Lod1Triangles);
                    builder.Append(", \"lod2Tris\": ");
                    builder.Append(m.Lod2Triangles);
                    builder.Append(", \"collisionTris\": ");
                    builder.Append(m.CollisionTriangles);
                    builder.Append(", \"vertexStrideBytes\": ");
                    builder.Append(m.VertexStrideBytes);
                    builder.Append(", \"sdfMs\": ");
                    AppendFixed(builder, m.SdfMilliseconds);
                    builder.Append(", \"extractMs\": ");
                    AppendFixed(builder, m.ExtractMilliseconds);
                    builder.Append(", \"attributeMs\": ");
                    AppendFixed(builder, m.AttributeMilliseconds);
                    builder.Append(", \"aoMs\": ");
                    AppendFixed(builder, m.AoMilliseconds);
                    builder.Append(", \"serializeMs\": ");
                    AppendFixed(builder, m.SerializationMilliseconds);
                    builder.Append(", \"warning\": \"");
                    builder.Append(m.WarningFlags == 0u ? "NONE" : "CRITICAL_WARNING");
                    builder.Append("\" }");
                }
            }

            builder.Append("\n  ]\n}\n");
            WriteAtomicText(GeologyForgeConstants.BakeReportPath, builder.ToString(), true);
        }

        private static void WriteAtomicText(string path, string contents, bool keepBackup)
        {
            EnsureFileFolder(path);
            string tempPath = path + ".tmp";
            DeleteIfExists(tempPath);
            try
            {
                File.WriteAllText(tempPath, contents);
                ReplacePayloadFile(tempPath, path, keepBackup);
            }
            catch
            {
                DeleteIfExists(tempPath);
                throw;
            }
        }

        private static void ReplacePayloadFile(string tempPath, string finalPath, bool keepBackup)
        {
            if (File.Exists(finalPath))
            {
                string backupPath = keepBackup ? finalPath + ".bak" : null;
                if (backupPath != null)
                    DeleteIfExists(backupPath);
                File.Replace(tempPath, finalPath, backupPath);
                return;
            }

            File.Move(tempPath, finalPath);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void AppendFixed(StringBuilder builder, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                builder.Append("0.000");
                return;
            }

            builder.Append(value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string SanitizeFileName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "Unnamed_Geology";

            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = input;
            for (int i = 0; i < invalid.Length; i++)
                safe = safe.Replace(invalid[i], '_');
            return safe.Replace(' ', '_');
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            int slash = folder.IndexOf('/');
            if (slash <= 0)
                return;

            string current = folder.Substring(0, slash);
            int segmentStart = slash + 1;
            while (segmentStart < folder.Length)
            {
                int nextSlash = folder.IndexOf('/', segmentStart);
                int segmentLength = nextSlash >= 0 ? nextSlash - segmentStart : folder.Length - segmentStart;
                if (segmentLength <= 0)
                    break;

                string segment = folder.Substring(segmentStart, segmentLength);
                string next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segment);
                current = next;
                if (nextSlash < 0)
                    break;
                segmentStart = nextSlash + 1;
            }
        }

        private static void EnsureFileFolder(string relativePath)
        {
            string folder = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
    }
}
