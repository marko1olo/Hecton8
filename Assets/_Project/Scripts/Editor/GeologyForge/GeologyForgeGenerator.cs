using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            GeologyProfileCsv.LoadProfiles(_menuProfiles);
            if (!BakeProfilesAsync(_menuProfiles, true))
                Debug.LogWarning("Geology Forge async bake request ignored: no profiles loaded or a bake is already running.");
        }

        public static bool BakeProfilesAsync(List<GeologyBakeProfile> profiles, bool saveAssets, Action<float> progressCallback = null)
        {
            if (_asyncProfiles != null || profiles == null || profiles.Count == 0)
                return false;

            GeologyVertexLayoutValidator.ValidateStruct();
            EnsureAssetFolder(GeologyForgeConstants.MeshOutputFolder);
            _asyncProfiles = new List<GeologyBakeProfile>(profiles.Count);
            for (int i = 0; i < profiles.Count; i++)
                _asyncProfiles.Add(profiles[i]);
            _asyncSaveAssets = saveAssets;
            _asyncTotalBakes = CountTotalBakes(_asyncProfiles);
            int resultCapacity = ResolveAsyncResultCapacity(_asyncTotalBakes);
            _asyncMetrics = new List<GeologyBakeMetrics>(resultCapacity);
            _asyncManifestRecords = saveAssets ? new List<GeologyMeshManifestRecord>(resultCapacity) : null;
            _asyncProgressCallback = progressCallback;
            _asyncProfileIndex = 0;
            _asyncVariationIndex = 0;
            _asyncCompletedBakes = 0;
            _asyncAssetEditing = false;
            try
            {
                _asyncProgressCallback?.Invoke(0f);
                EditorApplication.update -= TickAsyncBake;
                EditorApplication.update += TickAsyncBake;
                return true;
            }
            catch
            {
                FinishAsyncBake(true);
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
                if (saveAssets)
                {
                    WriteMeshManifest(manifestRecords);
                    AssetDatabase.SaveAssets();
                }
                if ((metric.WarningFlags & GeologyForgeConstants.WarningNonFiniteTelemetry) != 0u)
                    DumpBlackBox(telemetry, telemetryCursor, GeologyForgeConstants.DumpReasonNonFinite);
                return metric;
            }
            catch
            {
                if (telemetry.IsCreated)
                    DumpBlackBox(telemetry, telemetryCursor, GeologyForgeConstants.DumpReasonException);
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
                bool assetEditing = false;
                try
                {
                    telemetry = new NativeArray<GeologyBakeTelemetryEntry>(GeologyForgeConstants.BlackBoxFrameCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                    if (_asyncSaveAssets)
                    {
                        AssetDatabase.StartAssetEditing();
                        _asyncAssetEditing = true;
                        assetEditing = true;
                    }

                    GeologyBakeMetrics metric = BakeSingle(profile, _asyncVariationIndex, _asyncSaveAssets, telemetry, ref telemetryCursor, _asyncManifestRecords);
                    if (assetEditing)
                    {
                        AssetDatabase.StopAssetEditing();
                        _asyncAssetEditing = false;
                        assetEditing = false;
                    }

                    _asyncMetrics.Add(metric);
                    if ((metric.WarningFlags & GeologyForgeConstants.WarningNonFiniteTelemetry) != 0u)
                        DumpBlackBox(telemetry, telemetryCursor, GeologyForgeConstants.DumpReasonNonFinite);
                }
                catch
                {
                    if (assetEditing)
                    {
                        AssetDatabase.StopAssetEditing();
                        _asyncAssetEditing = false;
                        assetEditing = false;
                    }

                    if (telemetry.IsCreated)
                        DumpBlackBox(telemetry, telemetryCursor, GeologyForgeConstants.DumpReasonException);
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
                FinishAsyncBake(true);
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
                bool shouldWriteArtifacts = !canceled || hasMetrics || hasManifestRecords;
                if (_asyncSaveAssets && shouldWriteArtifacts)
                    WriteMeshManifest(_asyncManifestRecords);
                if (_asyncSaveAssets && shouldWriteArtifacts)
                    AssetDatabase.SaveAssets();
                if (_asyncMetrics != null && shouldWriteArtifacts)
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
                sdfHandle.Complete();
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
                countHandle.Complete();

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
                extractHandle.Complete();
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
                }.Schedule(rawCount, 64, bucketHandle);
                JobHandle uvHandle = new GenerateTriplanarUvsJob
                {
                    Vertices = rawVertices,
                    TextureScale = math.lerp(0.12f, 0.55f, qualityCurve)
                }.Schedule(rawCount, 64, normalHandle);
                uvHandle.Complete();
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
                aoHandle.Complete();
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
                    if (lod0 > profile.Lod0Budget)
                        metric.WarningFlags |= GeologyForgeConstants.WarningTriangleBudgetExceeded;
                    if (saveAssets)
                        SaveMeshesAndManifest(profile, seed, variation, lods, lod0, lod1, lod2, manifestRecords);
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
                StateHash = Mix(seed ^ ((uint)rawVertexCount * 0x9E3779B9u) ^ (stage * 0x85EBCA6Bu) ^ ((uint)lod0Triangles << 1) ^ ((uint)lod1Triangles << 2) ^ ((uint)lod2Triangles << 3)),
                DumpReason = (warningFlags & GeologyForgeConstants.WarningNonFiniteTelemetry) != 0u ? GeologyForgeConstants.DumpReasonNonFinite : 0u
            };
            cursor++;
            return warningFlags;
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

        private static void WriteMeshManifest(List<GeologyMeshManifestRecord> records)
        {
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
            return new MeshLodSet
            {
                Lod0 = BuildLodMesh("LOD0", sourceVertices, sourceTriangles, math.min(sourceTriangles, lod0Budget), voxelStep * math.lerp(0.3f, 0.08f, qualityCurve), 0, out lod0),
                Lod1 = BuildLodMesh("LOD1", sourceVertices, sourceTriangles, math.min(sourceTriangles, lod1Budget), voxelStep * math.lerp(1.45f, 0.65f, qualityCurve), 1, out lod1),
                Lod2 = BuildLodMesh("LOD2", sourceVertices, sourceTriangles, math.min(sourceTriangles, lod2Budget), voxelStep * math.lerp(3.6f, 1.8f, qualityCurve), 2, out lod2)
            };
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
                indexHandle.Complete();

                mesh = new Mesh
                {
                    name = $"GEN_Geology_{lodName}",
                    indexFormat = IndexFormat.UInt32
                };
                Bounds bounds = CalculateBounds(rawVertices);
                mesh.SetVertexBufferParams(vertexCount, GeologyVertexLayoutValidator.GetLayout());
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

        private static void SaveMeshesAndManifest(
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
            string stem = $"GEN_Geology_{safeName}_{seed:X8}_{variation:000}";
            string path0 = $"{GeologyForgeConstants.MeshOutputFolder}/{stem}_LOD0.asset";
            string path1 = $"{GeologyForgeConstants.MeshOutputFolder}/{stem}_LOD1.asset";
            string path2 = $"{GeologyForgeConstants.MeshOutputFolder}/{stem}_LOD2.asset";
            lods.Lod0 = SaveMeshAsset(lods.Lod0, path0, stem, 0);
            lods.Lod1 = SaveMeshAsset(lods.Lod1, path1, stem, 1);
            lods.Lod2 = SaveMeshAsset(lods.Lod2, path2, stem, 2);

            if (manifestRecords == null)
                return;

            Bounds bounds = lods.Lod0.bounds;
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
        }

        private static Mesh SaveMeshAsset(Mesh mesh, string path, string stem, int lodIndex)
        {
            mesh.name = $"{stem}_LOD{lodIndex}";
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

        private static void ResolveGuid128(string assetPath, out ulong high, out ulong low)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid) || guid.Length < 32)
            {
                high = 0UL;
                low = 0UL;
                return;
            }

            high = ParseHex64(guid, 0);
            low = ParseHex64(guid, 16);
        }

        private static ulong ParseHex64(string value, int start)
        {
            ulong result = 0UL;
            int end = math.min(value.Length, start + 16);
            for (int i = start; i < end; i++)
            {
                uint nibble = HexNibble(value[i]);
                result = (result << 4) | nibble;
            }

            return result;
        }

        private static uint HexNibble(char c)
        {
            if (c >= '0' && c <= '9')
                return (uint)(c - '0');
            if (c >= 'a' && c <= 'f')
                return (uint)(10 + c - 'a');
            if (c >= 'A' && c <= 'F')
                return (uint)(10 + c - 'A');
            return 0u;
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
            profile.RadiusMeters = math.max(0.25f, profile.RadiusMeters);
            profile.HeightScale = math.max(0.15f, profile.HeightScale);
            profile.Frequency = math.max(0.001f, profile.Frequency);
            profile.NoiseAmplitude = math.max(0f, profile.NoiseAmplitude);
            profile.RidgedWeight = math.saturate(profile.RidgedWeight);
            profile.VoronoiWeight = math.saturate(profile.VoronoiWeight);
            profile.GlobalQualityWeight = math.saturate(profile.GlobalQualityWeight);
            profile.Octaves = math.clamp(profile.Octaves <= 0 ? 4 : profile.Octaves, 1, 8);
            profile.AmbientOcclusionRays = math.clamp(profile.AmbientOcclusionRays <= 0 ? GeologyForgeConstants.DefaultAoRays : profile.AmbientOcclusionRays, 1, GeologyForgeConstants.MaximumAoRays);
            profile.Lod0Budget = math.max(32, profile.Lod0Budget <= 0 ? GeologyForgeConstants.Lod0TriangleBudget : profile.Lod0Budget);
            profile.Lod1Budget = math.max(16, profile.Lod1Budget <= 0 ? GeologyForgeConstants.Lod1TriangleBudget : profile.Lod1Budget);
            profile.Lod2Budget = math.max(8, profile.Lod2Budget <= 0 ? GeologyForgeConstants.Lod2TriangleBudget : profile.Lod2Budget);
            return profile;
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
            hash = Fnva(hash, (ulong)BitConverter.DoubleToInt64Bits(sectorAup.x));
            hash = Fnva(hash, (ulong)BitConverter.DoubleToInt64Bits(sectorAup.y));
            hash = Fnva(hash, (ulong)BitConverter.DoubleToInt64Bits(sectorAup.z));
            hash = Fnva(hash, seed);
            return (uint)(hash ^ (hash >> 32));
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
            builder.Append("{\n  \"agent\": \"SHINOBU_208\",\n  \"status\": \"PENDING_VERIFICATION\",\n  \"generatedMeshCount\": ");
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
