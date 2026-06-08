#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Hecton8.World.OfflineGeometry;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.OfflineGeometry
{
    internal static class OfflineGeometryBaker
    {
        private const uint LodManifestMagic = 0x444C3848u;
        private const uint LittleEndianTag = 0x01020304u;
        private const uint AgentStableHash = 0x53483231u;
        private const uint WarningBakeAttemptFailed = 16u;
        private const uint WarningSourceAssetMissing = 32u;
        private const uint WarningPrefabSaveFailed = 64u;
        private const uint WarningLodAssetBindFailed = 128u;
        private const string NativeMemoryOwner = "SHINOBU_213";
        private const string NativeMemorySentinelTypeName = "Hecton8.Core.NativeMemorySentinel";
        private const string NativeAllocationLifetimeTypeName = "Hecton8.Core.NativeAllocationLifetime";

        private const MeshUpdateFlags MeshFlags =
            MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontValidateIndices |
            MeshUpdateFlags.DontNotifyMeshUsers;

        private static readonly Stopwatch _Stopwatch = new Stopwatch();
        private static readonly UTF8Encoding _Utf8NoBom = new UTF8Encoding(false);
        private static readonly ProfilerMarker _MockHighPolyJobMarker = new ProfilerMarker("SHINOBU_213.MockHighPolyJobFence");
        private static readonly ProfilerMarker _PreviewPrimitiveFitJobMarker = new ProfilerMarker("SHINOBU_213.PreviewPrimitiveFitJobFence");
        private static readonly ProfilerMarker _PreviewHullJobMarker = new ProfilerMarker("SHINOBU_213.PreviewHullJobFence");
        private static readonly ProfilerMarker _DecimateJobMarker = new ProfilerMarker("SHINOBU_213.DecimateJobFence");
        private static readonly ProfilerMarker _PackMeshJobMarker = new ProfilerMarker("SHINOBU_213.PackMeshJobFence");
        private static readonly ProfilerMarker _ColliderPrimitiveFitJobMarker = new ProfilerMarker("SHINOBU_213.ColliderPrimitiveFitJobFence");
        private static readonly ProfilerMarker _ColliderHullJobMarker = new ProfilerMarker("SHINOBU_213.ColliderHullJobFence");

        [MenuItem("HECTON-8/LOD Collider Forge/Bake Selected Optimized Prefabs", false, 250)]
        private static void BakeSelectedMenu()
        {
            List<OfflineBakeSettings> profiles = OfflineOptimizationProfileCsv.LoadProfiles();
            OfflineBakeSettings settings = profiles.Count > 0 ? profiles[0] : OfflineOptimizationProfileCsv.DefaultSettings();
            List<OfflineBakeMetrics> metrics = BakeSelection(settings);
            WriteOptimizationReport(metrics);
            Debug.Log("[SHINOBU_213] Offline geometry bake finished. Assets=" + metrics.Count + ".");
        }

        [MenuItem("HECTON-8/LOD Collider Forge/Run Concave MeshCollider Inquisition", false, 251)]
        private static void InquisitionMenu()
        {
            List<UnoptimizedMeshFinding> findings = Unoptimized_Mesh_Scanner.ScanProject();
            Unoptimized_Mesh_Scanner.WriteReport(findings);
            Debug.Log("[SHINOBU_213] Physics optimization scan wrote " + OfflineGeometryBakerConstants.PhysicsReportPath + " findings=" + findings.Count + ".");
        }

        [MenuItem("HECTON-8/LOD Collider Forge/Generate Mock High Poly Benchmark Mesh", false, 252)]
        private static void GenerateMockBenchmarkMenu()
        {
            GenerateMockBenchmarkMesh(96, 192, OfflineOptimizationProfileCsv.DefaultSettings());
        }

        internal static List<OfflineBakeMetrics> BakeSelection(OfflineBakeSettings settings)
        {
            var metrics = new List<OfflineBakeMetrics>(32);
            UnityEngine.Object[] selected = Selection.objects;
            OfflineGeometryVertexLayoutValidator.ValidateStructs();
            EnsureAssetFolder(OfflineGeometryBakerConstants.MeshOutputFolder);
            EnsureAssetFolder(OfflineGeometryBakerConstants.ColliderOutputFolder);
            EnsureAssetFolder(OfflineGeometryBakerConstants.PrefabOutputFolder);

            bool editing = false;
            try
            {
                AssetDatabase.StartAssetEditing();
                editing = true;
                for (int i = 0; i < selected.Length; i++)
                {
                    string path = AssetDatabase.GetAssetPath(selected[i]);
                    ShowBakeProgress("Selection", path, i, selected.Length);
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    if (AssetDatabase.IsValidFolder(path))
                    {
                        BakeFolder(path, settings, metrics);
                        continue;
                    }

                    BakeAsset(path, settings, metrics);
                }
            }
            finally
            {
                if (editing)
                    AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return metrics;
        }

        internal static void BakeFolder(string folder, OfflineBakeSettings settings, List<OfflineBakeMetrics> metrics)
        {
            if (metrics == null || !AssetDatabase.IsValidFolder(folder))
                return;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            int total = prefabGuids.Length + modelGuids.Length;
            int cursor = 0;
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                ShowBakeProgress(folder, path, cursor++, total);
                BakeAsset(path, settings, metrics);
            }

            for (int i = 0; i < modelGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                ShowBakeProgress(folder, path, cursor++, total);
                BakeAsset(path, settings, metrics);
            }
        }

        internal static List<OfflineBakeMetrics> BakeFolderBatch(string folder, OfflineBakeSettings settings)
        {
            var metrics = new List<OfflineBakeMetrics>(64);
            OfflineGeometryVertexLayoutValidator.ValidateStructs();
            EnsureAssetFolder(OfflineGeometryBakerConstants.MeshOutputFolder);
            EnsureAssetFolder(OfflineGeometryBakerConstants.ColliderOutputFolder);
            EnsureAssetFolder(OfflineGeometryBakerConstants.PrefabOutputFolder);
            bool editing = false;
            try
            {
                AssetDatabase.StartAssetEditing();
                editing = true;
                BakeFolder(folder, settings, metrics);
            }
            finally
            {
                if (editing)
                    AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return metrics;
        }

        internal static bool BakeAsset(string sourcePath, OfflineBakeSettings settings, List<OfflineBakeMetrics> metrics)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || metrics == null)
                return false;

            OfflineBakeMetrics metric = CreateBaseMetric(sourcePath, settings);

            GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourceRoot == null)
            {
                metric.WarningFlags |= WarningSourceAssetMissing | WarningBakeAttemptFailed;
                OfflineGeometryBakeBlackBox.Record(metric);
                return false;
            }

            string sourceToken = SanitizeToken(Path.GetFileNameWithoutExtension(sourcePath));
            string prefabName = "GEN_" + sourceToken + "_Optimized";
            string prefabPath = OfflineGeometryBakerConstants.PrefabOutputFolder + "/" + prefabName + ".prefab";
            GameObject outputRoot = new GameObject(prefabName);
            GameObject lod0Root = new GameObject("LOD0");
            GameObject lod1Root = new GameObject("LOD1");
            GameObject lod2Root = new GameObject("LOD2");
            GameObject colliderRoot = new GameObject("COLLIDERS");
            lod0Root.transform.SetParent(outputRoot.transform, false);
            lod1Root.transform.SetParent(outputRoot.transform, false);
            lod2Root.transform.SetParent(outputRoot.transform, false);
            colliderRoot.transform.SetParent(outputRoot.transform, false);

            var lod0Renderers = new List<Renderer>(16);
            var lod1Renderers = new List<Renderer>(16);
            var lod2Renderers = new List<Renderer>(16);

            bool success = false;
            bool recorded = false;
            try
            {
                MeshFilter[] filters = sourceRoot.GetComponentsInChildren<MeshFilter>(true);
                for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
                {
                    MeshFilter sourceFilter = filters[filterIndex];
                    MeshRenderer sourceRenderer = null;
                    if (sourceFilter != null)
                        sourceRenderer = sourceFilter.TryGetComponent(out MeshRenderer resolvedSourceRenderer) ? resolvedSourceRenderer : null;
                    Mesh sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
                    if (sourceFilter == null || sourceRenderer == null || sourceMesh == null)
                        continue;

                    int originalTriangles = CountTriangles(sourceMesh);
                    if (originalTriangles <= 0)
                        continue;

                    float lod1Ratio = ResolveLod1Ratio(settings);
                    float lod2Ratio = ResolveLod2Ratio(settings);
                    float primitiveTolerance = ResolvePrimitiveTolerance(settings);
                    int decimationWindow = ResolveDecimationWindow(settings);
                    int lod1HardBudget = ResolveDerivedLodBudget(settings.Lod0HardBudget, lod1Ratio);
                    int lod2HardBudget = math.min(lod1HardBudget, ResolveDerivedLodBudget(settings.Lod0HardBudget, lod2Ratio));
                    metric.OriginalTriangles += originalTriangles;
                    metric.Lod1Ratio = lod1Ratio;
                    metric.Lod2Ratio = lod2Ratio;
                    metric.PrimitiveTolerance = primitiveTolerance;
                    metric.DecimationWindow = decimationWindow;
                    string meshToken = sourceToken + "_" + filterIndex.ToString("000", CultureInfo.InvariantCulture) + "_" + SanitizeToken(sourceMesh.name);
                    Mesh lod0Mesh = null;
                    Mesh lod1Mesh = null;
                    Mesh lod2Mesh = null;
                    bool lod0OwnedByCaller = false;
                    bool lod1OwnedByCaller = false;
                    bool lod2OwnedByCaller = false;
                    NativeArray<OfflineGeometryRawVertex> lod0Raw = default;
                    NativeArray<OfflineGeometryRawVertex> lod1Raw = default;
                    NativeArray<OfflineGeometryRawVertex> lod2Raw = default;
                    _Stopwatch.Restart();
                    lod0Mesh = BuildLodMesh(sourceMesh, meshToken + "_LOD0", 1f, settings.Lod0HardBudget, 1, out int lod0Triangles, out lod0Raw);
                    lod0OwnedByCaller = lod0Mesh != null;
                    _Stopwatch.Stop();
                    metric.ExtractionMilliseconds += _Stopwatch.Elapsed.TotalMilliseconds;
                    if (lod0Mesh == null || !lod0Raw.IsCreated)
                    {
                        if (lod0OwnedByCaller && lod0Mesh != null)
                            UnityEngine.Object.DestroyImmediate(lod0Mesh);
                        DisposeTrackedNativeArray(ref lod0Raw);
                        continue;
                    }

                    try
                    {
                        _Stopwatch.Restart();
                        lod1Mesh = BuildLodMesh(sourceMesh, meshToken + "_LOD1", lod1Ratio, lod1HardBudget, decimationWindow, out int lod1Triangles, out lod1Raw);
                        lod1OwnedByCaller = lod1Mesh != null;
                        _Stopwatch.Stop();
                        metric.ExtractionMilliseconds += _Stopwatch.Elapsed.TotalMilliseconds;
                        if (lod1Raw.IsCreated)
                        {
                            DisposeTrackedNativeArray(ref lod1Raw);
                        }

                        _Stopwatch.Restart();
                        lod2Mesh = BuildLodMesh(sourceMesh, meshToken + "_LOD2", lod2Ratio, lod2HardBudget, decimationWindow, out int lod2Triangles, out lod2Raw);
                        lod2OwnedByCaller = lod2Mesh != null;
                        _Stopwatch.Stop();
                        metric.ExtractionMilliseconds += _Stopwatch.Elapsed.TotalMilliseconds;
                        if (lod2Raw.IsCreated)
                        {
                            DisposeTrackedNativeArray(ref lod2Raw);
                        }
                        if (lod1Mesh == null || lod2Mesh == null)
                            continue;

                        _Stopwatch.Restart();
                        string lod0Path = SaveOrReplaceMesh(lod0Mesh, OfflineGeometryBakerConstants.MeshOutputFolder + "/" + meshToken + "_LOD0.asset", true);
                        lod0OwnedByCaller = false;
                        lod0Mesh = null;
                        string lod1Path = SaveOrReplaceMesh(lod1Mesh, OfflineGeometryBakerConstants.MeshOutputFolder + "/" + meshToken + "_LOD1.asset", true);
                        lod1OwnedByCaller = false;
                        lod1Mesh = null;
                        string lod2Path = SaveOrReplaceMesh(lod2Mesh, OfflineGeometryBakerConstants.MeshOutputFolder + "/" + meshToken + "_LOD2.asset", true);
                        lod2OwnedByCaller = false;
                        lod2Mesh = null;
                        if (string.IsNullOrEmpty(lod0Path) || string.IsNullOrEmpty(lod1Path) || string.IsNullOrEmpty(lod2Path))
                        {
                            metric.WarningFlags |= WarningLodAssetBindFailed;
                            continue;
                        }

                        Mesh lod0Asset = AssetDatabase.LoadAssetAtPath<Mesh>(lod0Path);
                        Mesh lod1Asset = AssetDatabase.LoadAssetAtPath<Mesh>(lod1Path);
                        Mesh lod2Asset = AssetDatabase.LoadAssetAtPath<Mesh>(lod2Path);
                        _Stopwatch.Stop();
                        if (lod0Asset == null || lod1Asset == null || lod2Asset == null)
                        {
                            metric.WarningFlags |= WarningLodAssetBindFailed;
                            continue;
                        }

                        metric.SerializationMilliseconds += _Stopwatch.Elapsed.TotalMilliseconds;
                        metric.Lod0Triangles += lod0Triangles;
                        metric.Lod1Triangles += lod1Triangles;
                        metric.Lod2Triangles += lod2Triangles;
                        if (originalTriangles > settings.Lod0HardBudget || originalTriangles > OfflineGeometryBakerConstants.HardLod0WarningTriangles)
                            metric.WarningFlags |= 1u;

                        CopyRenderer(sourceRoot.transform, sourceFilter.transform, lod0Root.transform, sourceRenderer, lod0Asset, lod0Renderers, "LOD0_" + filterIndex.ToString("000", CultureInfo.InvariantCulture));
                        CopyRenderer(sourceRoot.transform, sourceFilter.transform, lod1Root.transform, sourceRenderer, lod1Asset, lod1Renderers, "LOD1_" + filterIndex.ToString("000", CultureInfo.InvariantCulture));
                        CopyRenderer(sourceRoot.transform, sourceFilter.transform, lod2Root.transform, sourceRenderer, lod2Asset, lod2Renderers, "LOD2_" + filterIndex.ToString("000", CultureInfo.InvariantCulture));
                        CreateCollider(sourceRoot.transform, sourceFilter.transform, colliderRoot.transform, lod0Raw, sourceToken, filterIndex, primitiveTolerance, settings.ConvexHullVertexLimit, ref metric);

                        LodConfigurationDTO dto = new LodConfigurationDTO
                        {
                            Lod1Threshold = ResolveLod1Threshold(settings),
                            Lod2Threshold = ResolveLod2Threshold(settings),
                            Lod1MeshHash = StableHash(lod1Path),
                            Lod2MeshHash = StableHash(lod2Path)
                        };
                        metric.Lod1Threshold = dto.Lod1Threshold;
                        metric.Lod2Threshold = dto.Lod2Threshold;
                        metric.GlobalQualityWeight = math.saturate(settings.GlobalQualityWeight);
                        metric.DepthMeters = math.max(0f, settings.DepthMeters);
                        metric.Lod1MeshHash = FoldHash(metric.Lod1MeshHash, dto.Lod1MeshHash);
                        metric.Lod2MeshHash = FoldHash(metric.Lod2MeshHash, dto.Lod2MeshHash);
                        if (dto.Lod1Threshold <= dto.Lod2Threshold)
                            metric.WarningFlags |= 2u;

                        success = true;
                    }
                    finally
                    {
                        if (lod2OwnedByCaller && lod2Mesh != null)
                            UnityEngine.Object.DestroyImmediate(lod2Mesh);
                        if (lod1OwnedByCaller && lod1Mesh != null)
                            UnityEngine.Object.DestroyImmediate(lod1Mesh);
                        if (lod0OwnedByCaller && lod0Mesh != null)
                            UnityEngine.Object.DestroyImmediate(lod0Mesh);
                        DisposeTrackedNativeArray(ref lod2Raw);
                        DisposeTrackedNativeArray(ref lod1Raw);
                        DisposeTrackedNativeArray(ref lod0Raw);
                    }
                }

                if (!success || lod0Renderers.Count == 0)
                    return false;

                LODGroup lodGroup = outputRoot.AddComponent<LODGroup>();
                LOD[] levels =
                {
                    new LOD(ResolveLod0Threshold(settings), CopyRenderers(lod0Renderers)),
                    new LOD(ResolveLod1Threshold(settings), CopyRenderers(lod1Renderers)),
                    new LOD(ResolveLod2Threshold(settings), CopyRenderers(lod2Renderers))
                };
                levels[0].fadeTransitionWidth = ResolveFadeTransitionWidth(settings, 0.04f, 0.12f);
                levels[1].fadeTransitionWidth = ResolveFadeTransitionWidth(settings, 0.035f, 0.1f);
                levels[2].fadeTransitionWidth = ResolveFadeTransitionWidth(settings, 0.015f, 0.055f);
                lodGroup.SetLODs(levels);
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = true;
                lodGroup.RecalculateBounds();
                GameObjectUtility.SetStaticEditorFlags(outputRoot, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
                PrefabUtility.SaveAsPrefabAsset(outputRoot, prefabPath, out bool prefabSaved);
                if (!prefabSaved)
                {
                    metric.WarningFlags |= WarningPrefabSaveFailed;
                    return false;
                }

                metric.OutputPath = ToFixed128(prefabPath);
                OfflineGeometryBakeBlackBox.Record(metric);
                recorded = true;
                metrics.Add(metric);
                return true;
            }
            finally
            {
                if (!recorded)
                {
                    metric.WarningFlags |= WarningBakeAttemptFailed;
                    OfflineGeometryBakeBlackBox.Record(metric);
                }

                UnityEngine.Object.DestroyImmediate(outputRoot);
            }
        }

        internal static Mesh GenerateMockBenchmarkMesh(int latitudeSegments, int longitudeSegments, OfflineBakeSettings settings)
        {
            int lat = math.clamp(latitudeSegments, 4, 512);
            int lon = math.clamp(longitudeSegments, 4, 1024);
            int quadCount = lat * lon;
            int vertexCount = quadCount * 6;
            NativeArray<OfflineGeometryRawVertex> raw = default;
            NativeArray<OfflineSubMeshRange> ranges = default;
            try
            {
                // COLD ALLOC: NativeArray<OfflineGeometryRawVertex>[vertexCount] - editor mock high-poly mesh benchmark - owner: OfflineGeometryBaker
                raw = AllocateTrackedNativeArray<OfflineGeometryRawVertex>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(raw));
                _Stopwatch.Restart();
                using (_MockHighPolyJobMarker.Auto())
                {
                    new GenerateMockHighPolyMeshJob
                    {
                        Vertices = raw,
                        LatitudeSegments = lat,
                        LongitudeSegments = lon,
                        Radius = 3f,
                        FractalAmplitude = math.lerp(0.08f, 0.45f, math.saturate(settings.GlobalQualityWeight)),
                        Seed = 0x53483231u
                    }.Schedule(quadCount, 64).Complete();
                }
                _Stopwatch.Stop();
                ranges = BuildSingleSubMeshRange(vertexCount / 3);
                Mesh mesh = CreateUnityMesh("GEN_SHINOBU_213_MockHighPoly", raw, ranges);
                string path = SaveOrReplaceMesh(mesh, OfflineGeometryBakerConstants.MeshOutputFolder + "/GEN_SHINOBU_213_MockHighPoly.asset", true);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning("[SHINOBU_213] Mock high poly mesh generation failed before AssetDatabase bind.");
                    return null;
                }

                Mesh assetMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Debug.Log("[SHINOBU_213] Mock high poly mesh generated vertices=" + vertexCount + " ms=" + _Stopwatch.Elapsed.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture) + ".");
                return assetMesh;
            }
            finally
            {
                DisposeTrackedNativeArray(ref ranges);
                DisposeTrackedNativeArray(ref raw);
            }
        }

        internal static bool TryBuildPreviewHull(Mesh source, OfflineBakeSettings settings, out OfflinePrimitiveFitResult fitResult, out FixedList512Bytes<float3> hullPoints, out FixedList4096Bytes<ushort> hullIndices)
        {
            fitResult = default;
            hullPoints = default;
            hullIndices = default;
            if (source == null)
                return false;

            Mesh previewMesh = BuildLodMesh(source, source.name + "_PreviewRaw", 1f, settings.Lod0HardBudget, 1, out _, out NativeArray<OfflineGeometryRawVertex> raw);
            if (previewMesh != null)
                UnityEngine.Object.DestroyImmediate(previewMesh);

            if (!raw.IsCreated)
                return false;

            NativeArray<OfflinePrimitiveFitResult> fit = default;
            NativeArray<float3> hull = default;
            NativeArray<ushort> hullIndexBuffer = default;
            NativeArray<int> hullCount = default;
            NativeArray<int> hullIndexCount = default;
            try
            {
                // COLD ALLOC: NativeArray<OfflinePrimitiveFitResult>[1] - editor preview primitive fit - owner: OfflineGeometryBaker
                fit = AllocateTrackedNativeArray<OfflinePrimitiveFitResult>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(fit));
                using (_PreviewPrimitiveFitJobMarker.Auto())
                {
                    new FitGeometricPrimitivesJob
                    {
                        Vertices = raw,
                        Result = fit,
                        PrimitiveTolerance = ResolvePrimitiveTolerance(settings)
                    }.Schedule().Complete();
                }
                fitResult = fit[0];

                int hullVertexCapacity = ResolveHullVertexCapacity(settings.ConvexHullVertexLimit);
                // COLD ALLOC: NativeArray<float3>[hullVertexCapacity] - editor preview support hull - owner: OfflineGeometryBaker
                hull = AllocateTrackedNativeArray<float3>(hullVertexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(hull));
                // COLD ALLOC: NativeArray<ushort>[2048] - editor preview support hull indices - owner: OfflineGeometryBaker
                hullIndexBuffer = AllocateTrackedNativeArray<ushort>(OfflineGeometryBakerConstants.MaxHullIndexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(hullIndexBuffer));
                // COLD ALLOC: NativeArray<int>[1] - editor preview hull count - owner: OfflineGeometryBaker
                hullCount = AllocateTrackedNativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(hullCount));
                // COLD ALLOC: NativeArray<int>[1] - editor preview hull index count - owner: OfflineGeometryBaker
                hullIndexCount = AllocateTrackedNativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(hullIndexCount));
                using (_PreviewHullJobMarker.Auto())
                {
                    new GenerateConvexHullJob
                    {
                        Vertices = raw,
                        HullVertices = hull,
                        HullIndices = hullIndexBuffer,
                        HullVertexCount = hullCount,
                        HullIndexCount = hullIndexCount,
                        HullVertexLimit = settings.ConvexHullVertexLimit
                    }.Schedule().Complete();
                }

                int count = math.clamp(hullCount[0], 0, hull.Length);
                for (int i = 0; i < count; i++)
                    hullPoints.Add(hull[i]);

                int indexCount = math.clamp(hullIndexCount[0], 0, hullIndexBuffer.Length);
                int previewIndexCount = math.min(indexCount, 2000);
                for (int i = 0; i < previewIndexCount; i++)
                    hullIndices.Add(hullIndexBuffer[i]);
                return count >= OfflineGeometryBakerConstants.MinHullVertexCount && previewIndexCount >= 12;
            }
            finally
            {
                DisposeTrackedNativeArray(ref hullIndexCount);
                DisposeTrackedNativeArray(ref hullCount);
                DisposeTrackedNativeArray(ref hullIndexBuffer);
                DisposeTrackedNativeArray(ref hull);
                DisposeTrackedNativeArray(ref fit);
                DisposeTrackedNativeArray(ref raw);
            }
        }

        internal static int CountTriangles(Mesh mesh)
        {
            if (mesh == null)
                return 0;

            int triangles = 0;
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                triangles += (int)(mesh.GetIndexCount(subMeshIndex) / 3u);
            return triangles;
        }

        private static NativeArray<T> AllocateTrackedNativeArray<T>(int length, Allocator allocator, NativeArrayOptions options, string label) where T : struct
        {
            if (length <= 0)
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[SHINOBU_213] NativeArray allocation failed for " + label + ".");

            try
            {
                RegisterTrackedNativeArray(array, label, ResolveNativeAllocationLifetimeName(allocator));
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

            IntPtr trackedPointer = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            array.Dispose();
            array = default;
            UnregisterTrackedNativeArray(trackedPointer);
        }

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label, string lifetimeName) where T : struct
        {
            Type sentinelType = FindType(NativeMemorySentinelTypeName);
            Type lifetimeType = FindType(NativeAllocationLifetimeTypeName);
            if (sentinelType == null || lifetimeType == null)
                throw new InvalidOperationException("[SHINOBU_213] NativeMemorySentinel bridge unavailable for " + label + ".");

            MethodInfo method = sentinelType.GetMethod("RegisterNativeArray", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                throw new InvalidOperationException("[SHINOBU_213] NativeMemorySentinel.RegisterNativeArray unavailable for " + label + ".");

            object lifetime = Enum.Parse(lifetimeType, lifetimeName);
            object id = method.MakeGenericMethod(typeof(T)).Invoke(
                null,
                new object[] { array, NativeMemoryOwner, label, lifetime });
            if (!(id is int sentinelId) || sentinelId <= 0)
                throw new InvalidOperationException("[SHINOBU_213] NativeMemorySentinel rejected native allocation registration for " + label + ".");
        }

        private static void UnregisterTrackedNativeArray(IntPtr trackedPointer)
        {
            if (trackedPointer == IntPtr.Zero)
                return;

            Type sentinelType = FindType(NativeMemorySentinelTypeName);
            MethodInfo method = sentinelType != null
                ? sentinelType.GetMethod("UnregisterPointer", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(IntPtr) }, null)
                : null;
            if (method == null)
                throw new InvalidOperationException("[SHINOBU_213] NativeMemorySentinel.UnregisterPointer unavailable.");

            method.Invoke(null, new object[] { trackedPointer });
        }

        private static string ResolveNativeAllocationLifetimeName(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return "Temp";
                case Allocator.TempJob:
                    return "TempJob";
                case Allocator.Persistent:
                    return "Session";
                default:
                    return "Session";
            }
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static unsafe Mesh BuildLodMesh(Mesh source, string meshName, float ratio, int maxTriangles, int selectionWindow, out int triangleCount, out NativeArray<OfflineGeometryRawVertex> rawVertices)
        {
            triangleCount = 0;
            rawVertices = default;
            if (source == null)
                return null;

            using (Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(source))
            {
                Mesh.MeshData meshData = meshDataArray[0];
                if (!TryResolveVertexLayout(meshData, out SourceVertexLayout layout))
                    return null;

                int sourceTriangles = CountMeshDataTriangles(meshData);
                if (sourceTriangles <= 0)
                    return null;

                int targetTriangles = math.max(1, (int)math.round(sourceTriangles * math.saturate(ratio)));
                if (maxTriangles > 0)
                    targetTriangles = math.min(targetTriangles, maxTriangles);

                NativeArray<OfflineSubMeshRange> ranges = default;
                try
                {
                    ranges = BuildSubMeshRanges(meshData, sourceTriangles, targetTriangles, out triangleCount);
                    if (!ranges.IsCreated || triangleCount <= 0)
                        return null;

                    int vertexCount = triangleCount * 3;
                    // COLD ALLOC: NativeArray<OfflineGeometryRawVertex>[vertexCount] - editor LOD triangle-soup output - owner: OfflineGeometryBaker
                    rawVertices = AllocateTrackedNativeArray<OfflineGeometryRawVertex>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(rawVertices));
                    NativeArray<byte> positionData = meshData.GetVertexData<byte>(layout.PositionStream);
                    NativeArray<byte> normalData = layout.HasNormals != 0 ? meshData.GetVertexData<byte>(layout.NormalStream) : default;
                    NativeArray<byte> uvData = layout.HasUv0 != 0 ? meshData.GetVertexData<byte>(layout.Uv0Stream) : default;

                    void* positionPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(positionData);
                    void* normalPtr = layout.HasNormals != 0 ? NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(normalData) : null;
                    void* uvPtr = layout.HasUv0 != 0 ? NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(uvData) : null;

                    JobHandle handle;
                    if (meshData.indexFormat == IndexFormat.UInt16)
                    {
                        NativeArray<ushort> indices = meshData.GetIndexData<ushort>();
                        handle = new OfflineDecimateUInt16Job
                        {
                            Indices = indices,
                            Ranges = ranges,
                            OutputVertices = rawVertices,
                            PositionPtr = positionPtr,
                            NormalPtr = normalPtr,
                            Uv0Ptr = uvPtr,
                            PositionStride = layout.PositionStride,
                            PositionOffset = layout.PositionOffset,
                            NormalStride = layout.NormalStride,
                            NormalOffset = layout.NormalOffset,
                            Uv0Stride = layout.Uv0Stride,
                            Uv0Offset = layout.Uv0Offset,
                            SourceVertexCount = meshData.vertexCount,
                            HasNormals = layout.HasNormals,
                            HasUv0 = layout.HasUv0,
                            SelectionWindow = selectionWindow
                        }.Schedule(triangleCount, 64);
                    }
                    else
                    {
                        NativeArray<uint> indices = meshData.GetIndexData<uint>();
                        handle = new OfflineDecimateUInt32Job
                        {
                            Indices = indices,
                            Ranges = ranges,
                            OutputVertices = rawVertices,
                            PositionPtr = positionPtr,
                            NormalPtr = normalPtr,
                            Uv0Ptr = uvPtr,
                            PositionStride = layout.PositionStride,
                            PositionOffset = layout.PositionOffset,
                            NormalStride = layout.NormalStride,
                            NormalOffset = layout.NormalOffset,
                            Uv0Stride = layout.Uv0Stride,
                            Uv0Offset = layout.Uv0Offset,
                            SourceVertexCount = meshData.vertexCount,
                            HasNormals = layout.HasNormals,
                            HasUv0 = layout.HasUv0,
                            SelectionWindow = selectionWindow
                        }.Schedule(triangleCount, 64);
                    }

                    using (_DecimateJobMarker.Auto())
                    {
                        handle.Complete();
                    }
                    Mesh mesh = CreateUnityMesh(meshName, rawVertices, ranges);
                    if (mesh == null)
                    {
                        if (rawVertices.IsCreated)
                        {
                            DisposeTrackedNativeArray(ref rawVertices);
                        }

                        triangleCount = 0;
                    }

                    return mesh;
                }
                catch
                {
                    if (rawVertices.IsCreated)
                    {
                        DisposeTrackedNativeArray(ref rawVertices);
                    }
                    throw;
                }
                finally
                {
                    DisposeTrackedNativeArray(ref ranges);
                }
            }
        }

        private static void ShowBakeProgress(string scope, string path, int index, int total)
        {
            float progress = total > 0 ? math.saturate(index * math.rcp(total)) : 0f;
            EditorUtility.DisplayProgressBar("HECTON-8 LOD Collider Forge", scope + " :: " + path, progress);
        }

        private static Mesh CreateUnityMesh(string meshName, NativeArray<OfflineGeometryRawVertex> rawVertices, NativeArray<OfflineSubMeshRange> ranges)
        {
            if (!rawVertices.IsCreated || rawVertices.Length < 3 || !ranges.IsCreated || ranges.Length <= 0)
                return null;

            int subMeshCount = CountPositiveRanges(ranges, rawVertices.Length);
            if (subMeshCount <= 0)
                return null;

            NativeArray<OfflineGeometryVertex32> packed = default;
            NativeArray<uint> indices = default;
            try
            {
                int vertexCount = rawVertices.Length;
                // COLD ALLOC: NativeArray<OfflineGeometryVertex32>[vertexCount] - editor interleaved GPU vertex stream - owner: OfflineGeometryBaker
                packed = AllocateTrackedNativeArray<OfflineGeometryVertex32>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(packed));
                // COLD ALLOC: NativeArray<uint>[vertexCount] - editor linear index stream - owner: OfflineGeometryBaker
                indices = AllocateTrackedNativeArray<uint>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(indices));
                using (_PackMeshJobMarker.Auto())
                {
                    JobHandle packHandle = new OfflinePackVertexJob
                    {
                        SourceVertices = rawVertices,
                        PackedVertices = packed
                    }.Schedule(vertexCount, 64);
                    JobHandle indexHandle = new OfflineIndexFillJob
                    {
                        Indices = indices
                    }.Schedule(vertexCount, 64, packHandle);
                    indexHandle.Complete();
                }

                Bounds bounds = CalculateBounds(rawVertices);
                Mesh mesh = null;
                bool transferred = false;
                try
                {
                    mesh = new Mesh
                    {
                        name = meshName,
                        indexFormat = IndexFormat.UInt32
                    };
                    mesh.SetVertexBufferParams(vertexCount, OfflineGeometryVertexLayoutValidator.Layout);
                    mesh.SetVertexBufferData(packed, 0, 0, vertexCount, 0, MeshFlags);
                    mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
                    mesh.SetIndexBufferData(indices, 0, 0, vertexCount, MeshFlags);
                    mesh.subMeshCount = subMeshCount;
                    int outputSubMeshIndex = 0;
                    for (int i = 0; i < ranges.Length; i++)
                    {
                        OfflineSubMeshRange range = ranges[i];
                        if (!IsSubMeshRangeValid(range, vertexCount))
                            continue;

                        int indexStart = range.TargetTriangleStart * 3;
                        int indexCount = range.TargetTriangleCount * 3;
                        mesh.SetSubMesh(outputSubMeshIndex, new SubMeshDescriptor(indexStart, indexCount, MeshTopology.Triangles)
                        {
                            bounds = bounds,
                            vertexCount = vertexCount
                        }, MeshFlags);
                        outputSubMeshIndex++;
                    }

                    mesh.bounds = bounds;
                    OfflineGeometryVertexLayoutValidator.ValidateMesh(mesh);
                    transferred = true;
                    return mesh;
                }
                finally
                {
                    if (!transferred && mesh != null)
                        UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            finally
            {
                DisposeTrackedNativeArray(ref indices);
                DisposeTrackedNativeArray(ref packed);
            }
        }

        private static NativeArray<OfflineSubMeshRange> BuildSingleSubMeshRange(int triangleCount)
        {
            // COLD ALLOC: NativeArray<OfflineSubMeshRange>[1] - editor mock mesh submesh range - owner: OfflineGeometryBaker
            NativeArray<OfflineSubMeshRange> ranges = AllocateTrackedNativeArray<OfflineSubMeshRange>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory, nameof(ranges));
            ranges[0] = new OfflineSubMeshRange
            {
                SourceIndexStart = 0,
                SourceTriangleCount = triangleCount,
                TargetTriangleStart = 0,
                TargetTriangleCount = triangleCount
            };
            return ranges;
        }

        private static int CountPositiveRanges(NativeArray<OfflineSubMeshRange> ranges, int vertexCount)
        {
            int count = 0;
            for (int i = 0; i < ranges.Length; i++)
            {
                if (IsSubMeshRangeValid(ranges[i], vertexCount))
                    count++;
            }

            return count;
        }

        private static bool IsSubMeshRangeValid(OfflineSubMeshRange range, int vertexCount)
        {
            if (range.TargetTriangleStart < 0 || range.TargetTriangleCount <= 0)
                return false;

            long indexStart = (long)range.TargetTriangleStart * 3L;
            long indexCount = (long)range.TargetTriangleCount * 3L;
            return indexStart >= 0L && indexCount > 0L && indexStart + indexCount <= vertexCount;
        }

        private static string SaveOrReplaceMesh(Mesh mesh, string path, bool uploadMeshData)
        {
            if (mesh == null)
                return null;

            if (string.IsNullOrWhiteSpace(path))
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                return null;
            }

            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!TryEnsureAssetFolder(folder))
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                return null;
            }

            bool assetOwned = false;
            try
            {
                mesh.name = Path.GetFileNameWithoutExtension(path);
                if (uploadMeshData)
                    mesh.UploadMeshData(true);

                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    return path;
                }

                AssetDatabase.CreateAsset(mesh, path);
                assetOwned = true;
                return path;
            }
            finally
            {
                if (!assetOwned)
                    UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static void CopyRenderer(Transform sourceRoot, Transform sourceTransform, Transform parent, MeshRenderer sourceRenderer, Mesh mesh, List<Renderer> renderers, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            ApplyRelativeTransform(sourceRoot, sourceTransform, child.transform);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterials = sourceRenderer.sharedMaterials;
            renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
            renderer.receiveShadows = sourceRenderer.receiveShadows;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
            renderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
            renderers.Add(renderer);
            GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
        }

        private static Renderer[] CopyRenderers(List<Renderer> renderers)
        {
            int count = renderers != null ? renderers.Count : 0;
            Renderer[] copied = new Renderer[count];
            for (int i = 0; i < count; i++)
                copied[i] = renderers[i];

            return copied;
        }

        private static void CreateCollider(Transform sourceRoot, Transform sourceTransform, Transform parent, NativeArray<OfflineGeometryRawVertex> rawVertices, string sourceToken, int filterIndex, float primitiveTolerance, int convexHullVertexLimit, ref OfflineBakeMetrics metric)
        {
            NativeArray<OfflinePrimitiveFitResult> fit = default;
            try
            {
                // COLD ALLOC: NativeArray<OfflinePrimitiveFitResult>[1] - editor primitive collider fit result - owner: OfflineGeometryBaker
                fit = AllocateTrackedNativeArray<OfflinePrimitiveFitResult>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(fit));
                using (_ColliderPrimitiveFitJobMarker.Auto())
                {
                    new FitGeometricPrimitivesJob
                    {
                        Vertices = rawVertices,
                        Result = fit,
                        PrimitiveTolerance = primitiveTolerance
                    }.Schedule().Complete();
                }

                OfflinePrimitiveFitResult result = fit[0];
                string name = "COL_" + filterIndex.ToString("000", CultureInfo.InvariantCulture);
                GameObject colliderObject = new GameObject(name);
                colliderObject.transform.SetParent(parent, false);
                ApplyRelativeTransform(sourceRoot, sourceTransform, colliderObject.transform);

                if (result.ColliderType == (byte)OfflineColliderKind.Sphere)
                {
                    SphereCollider sphere = colliderObject.AddComponent<SphereCollider>();
                    sphere.center = ToVector3(result.Center);
                    sphere.radius = math.max(0.01f, result.Radius);
                    metric.PrimitiveColliderCount++;
                    return;
                }

                if (result.ColliderType == (byte)OfflineColliderKind.Box)
                {
                    BoxCollider box = colliderObject.AddComponent<BoxCollider>();
                    box.center = ToVector3(result.Center);
                    box.size = ToVector3(math.max(result.Size, new float3(0.01f)));
                    metric.PrimitiveColliderCount++;
                    return;
                }

                Mesh hull = BuildConvexHullMesh(rawVertices, "GEN_" + sourceToken + "_COL_" + filterIndex.ToString("000", CultureInfo.InvariantCulture), convexHullVertexLimit);
                if (hull == null)
                {
                    AddFallbackBoxCollider(colliderObject, result, ref metric, 4u);
                    return;
                }

                string hullPath = SaveOrReplaceMesh(hull, OfflineGeometryBakerConstants.ColliderOutputFolder + "/" + hull.name + ".asset", false);
                Mesh hullAsset = string.IsNullOrEmpty(hullPath) ? null : AssetDatabase.LoadAssetAtPath<Mesh>(hullPath);
                if (hullAsset == null)
                {
                    AddFallbackBoxCollider(colliderObject, result, ref metric, 8u);
                    return;
                }

                MeshCollider meshCollider = colliderObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = hullAsset;
                meshCollider.convex = true;
                meshCollider.cookingOptions =
                    MeshColliderCookingOptions.CookForFasterSimulation |
                    MeshColliderCookingOptions.EnableMeshCleaning |
                    MeshColliderCookingOptions.WeldColocatedVertices;
                metric.ConvexColliderCount++;
            }
            finally
            {
                DisposeTrackedNativeArray(ref fit);
            }
        }

        private static void AddFallbackBoxCollider(GameObject colliderObject, OfflinePrimitiveFitResult result, ref OfflineBakeMetrics metric, uint warningFlag)
        {
            BoxCollider fallbackBox = colliderObject.AddComponent<BoxCollider>();
            fallbackBox.center = ToVector3(result.Center);
            fallbackBox.size = ToVector3(math.max(result.Size, new float3(0.01f)));
            metric.PrimitiveColliderCount++;
            metric.WarningFlags |= warningFlag;
        }

        private static Mesh BuildConvexHullMesh(NativeArray<OfflineGeometryRawVertex> rawVertices, string name, int hullVertexLimit)
        {
            NativeArray<float3> hullVertices = default;
            NativeArray<ushort> hullIndices = default;
            NativeArray<int> hullCount = default;
            NativeArray<int> hullIndexCount = default;
            NativeArray<OfflineGeometryVertex32> packed = default;
            Mesh mesh = null;
            bool transferred = false;
            try
            {
                int hullVertexCapacity = ResolveHullVertexCapacity(hullVertexLimit);
                // COLD ALLOC: NativeArray<float3>[hullVertexCapacity] - editor bounded convex support hull vertices - owner: OfflineGeometryBaker
                hullVertices = AllocateTrackedNativeArray<float3>(hullVertexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(hullVertices));
                // COLD ALLOC: NativeArray<ushort>[2048] - editor bounded convex support hull indices - owner: OfflineGeometryBaker
                hullIndices = AllocateTrackedNativeArray<ushort>(OfflineGeometryBakerConstants.MaxHullIndexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(hullIndices));
                // COLD ALLOC: NativeArray<int>[1] - editor fallback hull vertex count - owner: OfflineGeometryBaker
                hullCount = AllocateTrackedNativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(hullCount));
                // COLD ALLOC: NativeArray<int>[1] - editor fallback hull index count - owner: OfflineGeometryBaker
                hullIndexCount = AllocateTrackedNativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(hullIndexCount));
                using (_ColliderHullJobMarker.Auto())
                {
                    new GenerateConvexHullJob
                    {
                        Vertices = rawVertices,
                        HullVertices = hullVertices,
                        HullIndices = hullIndices,
                        HullVertexCount = hullCount,
                        HullIndexCount = hullIndexCount,
                        HullVertexLimit = hullVertexLimit
                    }.Schedule().Complete();
                }

                int vertexCount = math.clamp(hullCount[0], 0, hullVertices.Length);
                int indexCount = math.clamp(hullIndexCount[0], 0, hullIndices.Length);
                indexCount -= indexCount % 3;
                if (vertexCount < OfflineGeometryBakerConstants.MinHullVertexCount || indexCount < 12)
                    return null;

                // COLD ALLOC: NativeArray<OfflineGeometryVertex32>[vertexCount] - editor convex collider vertex stream - owner: OfflineGeometryBaker
                packed = AllocateTrackedNativeArray<OfflineGeometryVertex32>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(packed));
                for (int i = 0; i < vertexCount; i++)
                {
                    packed[i] = new OfflineGeometryVertex32
                    {
                        Position = hullVertices[i],
                        Normal = new float3(0f, 1f, 0f),
                        Uv0 = float2.zero
                    };
                }

                float3 min = hullVertices[0];
                float3 max = hullVertices[0];
                for (int i = 1; i < vertexCount; i++)
                {
                    min = math.min(min, hullVertices[i]);
                    max = math.max(max, hullVertices[i]);
                }

                Bounds bounds = new Bounds(ToVector3((min + max) * 0.5f), ToVector3(max - min));
                mesh = new Mesh
                {
                    name = name,
                    indexFormat = IndexFormat.UInt16
                };
                mesh.SetVertexBufferParams(vertexCount, OfflineGeometryVertexLayoutValidator.Layout);
                mesh.SetVertexBufferData(packed, 0, 0, vertexCount, 0, MeshFlags);
                mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
                mesh.SetIndexBufferData(hullIndices, 0, 0, indexCount, MeshFlags);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                }, MeshFlags);
                mesh.bounds = bounds;
                transferred = true;
                return mesh;
            }
            finally
            {
                if (!transferred && mesh != null)
                    UnityEngine.Object.DestroyImmediate(mesh);
                DisposeTrackedNativeArray(ref packed);
                DisposeTrackedNativeArray(ref hullIndexCount);
                DisposeTrackedNativeArray(ref hullCount);
                DisposeTrackedNativeArray(ref hullIndices);
                DisposeTrackedNativeArray(ref hullVertices);
            }
        }

        private static int ResolveHullVertexCapacity(int hullVertexLimit)
        {
            return math.clamp(hullVertexLimit, OfflineGeometryBakerConstants.MinHullVertexCount, OfflineGeometryBakerConstants.MaxHullVertexCount);
        }

        private static NativeArray<OfflineSubMeshRange> BuildSubMeshRanges(Mesh.MeshData meshData, int sourceTriangles, int targetTriangles, out int resolvedTargetTriangles)
        {
            int subMeshCount = math.max(0, meshData.subMeshCount);
            // COLD ALLOC: NativeArray<OfflineSubMeshRange>[subMeshCount] - editor mesh submesh budget ranges - owner: OfflineGeometryBaker
            NativeArray<OfflineSubMeshRange> ranges = AllocateTrackedNativeArray<OfflineSubMeshRange>(subMeshCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(ranges));
            int total = 0;
            for (int i = 0; i < subMeshCount; i++)
            {
                SubMeshDescriptor descriptor = meshData.GetSubMesh(i);
                int sourceSubmeshTriangles = descriptor.topology == MeshTopology.Triangles ? descriptor.indexCount / 3 : 0;
                int targetSubmeshTriangles = sourceSubmeshTriangles > 0
                    ? (int)math.floor((double)sourceSubmeshTriangles * targetTriangles / math.max(1, sourceTriangles))
                    : 0;
                ranges[i] = new OfflineSubMeshRange
                {
                    SourceIndexStart = descriptor.indexStart,
                    SourceTriangleCount = sourceSubmeshTriangles,
                    TargetTriangleStart = total,
                    TargetTriangleCount = targetSubmeshTriangles
                };
                total += targetSubmeshTriangles;
            }

            while (total < targetTriangles)
            {
                bool added = false;
                for (int i = 0; i < ranges.Length && total < targetTriangles; i++)
                {
                    OfflineSubMeshRange range = ranges[i];
                    if (range.SourceTriangleCount <= 0 || range.TargetTriangleCount >= range.SourceTriangleCount)
                        continue;

                    range.TargetTriangleCount++;
                    ranges[i] = range;
                    total++;
                    added = true;
                }

                if (!added)
                    break;
            }

            while (total > targetTriangles)
            {
                bool reduced = false;
                for (int i = ranges.Length - 1; i >= 0 && total > targetTriangles; i--)
                {
                    OfflineSubMeshRange range = ranges[i];
                    if (range.TargetTriangleCount <= 0)
                        continue;

                    range.TargetTriangleCount--;
                    ranges[i] = range;
                    total--;
                    reduced = true;
                }

                if (!reduced)
                    break;
            }

            int cursor = 0;
            for (int i = 0; i < ranges.Length; i++)
            {
                OfflineSubMeshRange range = ranges[i];
                range.TargetTriangleStart = cursor;
                ranges[i] = range;
                cursor += range.TargetTriangleCount;
            }

            resolvedTargetTriangles = cursor;
            return ranges;
        }

        private static int CountMeshDataTriangles(Mesh.MeshData meshData)
        {
            int count = 0;
            for (int i = 0; i < meshData.subMeshCount; i++)
            {
                SubMeshDescriptor descriptor = meshData.GetSubMesh(i);
                if (descriptor.topology == MeshTopology.Triangles)
                    count += descriptor.indexCount / 3;
            }

            return count;
        }

        private static bool TryResolveVertexLayout(Mesh.MeshData meshData, out SourceVertexLayout layout)
        {
            layout = default;
            if (!meshData.HasVertexAttribute(VertexAttribute.Position))
                return false;

            if (meshData.GetVertexAttributeFormat(VertexAttribute.Position) != VertexAttributeFormat.Float32 ||
                meshData.GetVertexAttributeDimension(VertexAttribute.Position) < 3)
                return false;

            layout.PositionStream = meshData.GetVertexAttributeStream(VertexAttribute.Position);
            layout.PositionStride = meshData.GetVertexBufferStride(layout.PositionStream);
            layout.PositionOffset = meshData.GetVertexAttributeOffset(VertexAttribute.Position);
            if (!IsStreamLaneValid(layout.PositionStride, layout.PositionOffset, 12))
                return false;

            if (meshData.HasVertexAttribute(VertexAttribute.Normal))
            {
                if (meshData.GetVertexAttributeFormat(VertexAttribute.Normal) == VertexAttributeFormat.Float32 &&
                    meshData.GetVertexAttributeDimension(VertexAttribute.Normal) >= 3)
                {
                    layout.HasNormals = 1;
                    layout.NormalStream = meshData.GetVertexAttributeStream(VertexAttribute.Normal);
                    layout.NormalStride = meshData.GetVertexBufferStride(layout.NormalStream);
                    layout.NormalOffset = meshData.GetVertexAttributeOffset(VertexAttribute.Normal);
                    if (!IsStreamLaneValid(layout.NormalStride, layout.NormalOffset, 12))
                        layout.HasNormals = 0;
                }
            }

            if (meshData.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                if (meshData.GetVertexAttributeFormat(VertexAttribute.TexCoord0) == VertexAttributeFormat.Float32 &&
                    meshData.GetVertexAttributeDimension(VertexAttribute.TexCoord0) >= 2)
                {
                    layout.HasUv0 = 1;
                    layout.Uv0Stream = meshData.GetVertexAttributeStream(VertexAttribute.TexCoord0);
                    layout.Uv0Stride = meshData.GetVertexBufferStride(layout.Uv0Stream);
                    layout.Uv0Offset = meshData.GetVertexAttributeOffset(VertexAttribute.TexCoord0);
                    if (!IsStreamLaneValid(layout.Uv0Stride, layout.Uv0Offset, 8))
                        layout.HasUv0 = 0;
                }
            }

            return true;
        }

        private static bool IsStreamLaneValid(int stride, int offset, int laneBytes)
        {
            return stride > 0 && offset >= 0 && laneBytes > 0 && offset <= stride - laneBytes;
        }

        private static Bounds CalculateBounds(NativeArray<OfflineGeometryRawVertex> vertices)
        {
            if (!vertices.IsCreated || vertices.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one);

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            int valid = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                float3 p = vertices[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;

                min = math.min(min, p);
                max = math.max(max, p);
                valid++;
            }

            if (valid <= 0)
                return new Bounds(Vector3.zero, Vector3.one);

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(0.01f));
            return new Bounds(ToVector3(center), ToVector3(size));
        }

        private static void ApplyRelativeTransform(Transform sourceRoot, Transform sourceTransform, Transform target)
        {
            Matrix4x4 matrix = sourceRoot.worldToLocalMatrix * sourceTransform.localToWorldMatrix;
            Vector3 right = matrix.GetColumn(0);
            Vector3 up = matrix.GetColumn(1);
            Vector3 forward = matrix.GetColumn(2);
            Vector3 scale = new Vector3(SafeMagnitude(right), SafeMagnitude(up), SafeMagnitude(forward));
            target.localPosition = SanitizeVector3(matrix.GetColumn(3), Vector3.zero);
            target.localRotation = SafeLookRotation(forward, up);
            target.localScale = scale;
        }

        private static Quaternion SafeLookRotation(Vector3 forward, Vector3 up)
        {
            Vector3 safeForward = NormalizeOrDefault(forward, Vector3.forward);
            Vector3 safeUp = NormalizeOrDefault(up, Vector3.up);
            safeUp -= safeForward * Vector3.Dot(safeUp, safeForward);
            if (!IsFinite(safeUp) || safeUp.sqrMagnitude <= 1e-8f)
            {
                Vector3 seed = Mathf.Abs(safeForward.y) < 0.75f ? Vector3.up : Vector3.right;
                safeUp = seed - safeForward * Vector3.Dot(seed, safeForward);
            }

            safeUp = NormalizeOrDefault(safeUp, Mathf.Abs(safeForward.y) < 0.75f ? Vector3.up : Vector3.right);
            return Quaternion.LookRotation(safeForward, safeUp);
        }

        private static Vector3 NormalizeOrDefault(Vector3 value, Vector3 fallback)
        {
            if (!IsFinite(value))
                return fallback;

            float lenSq = value.sqrMagnitude;
            if (!IsFinite(lenSq) || lenSq <= 1e-8f)
                return fallback;

            return value / Mathf.Sqrt(Mathf.Max(lenSq, 1e-8f));
        }

        private static float SafeMagnitude(Vector3 value)
        {
            if (!IsFinite(value))
                return 0.0001f;

            float magnitude = value.magnitude;
            return IsFinite(magnitude) ? Mathf.Max(0.0001f, magnitude) : 0.0001f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static Vector3 SanitizeVector3(Vector3 value, Vector3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float ResolveLod0Threshold(OfflineBakeSettings settings)
        {
            float quality = math.saturate(settings.GlobalQualityWeight);
            float depth = math.saturate((settings.DepthMeters - 400f) * math.rcp(1600f));
            return math.clamp(OfflineGeometryBakerConstants.DefaultLod0Threshold * math.lerp(1.18f, 0.92f, quality) + depth * 0.1f, 0.35f, 0.85f);
        }

        private static float ResolveLod1Ratio(OfflineBakeSettings settings)
        {
            float quality = math.saturate(settings.GlobalQualityWeight);
            float curve = math.smoothstep(0f, 1f, quality);
            float depth = math.saturate((settings.DepthMeters - 300f) * math.rcp(1700f));
            float densityScale = math.lerp(0.36f, 1.08f, curve) * math.lerp(1f, 0.78f, depth);
            return math.clamp(settings.Lod1Ratio * densityScale, 0.04f, 1f);
        }

        private static float ResolveLod2Ratio(OfflineBakeSettings settings)
        {
            float quality = math.saturate(settings.GlobalQualityWeight);
            float curve = math.smoothstep(0f, 1f, quality);
            float depth = math.saturate((settings.DepthMeters - 300f) * math.rcp(1700f));
            float densityScale = math.lerp(0.22f, 1.15f, curve) * math.lerp(1f, 0.68f, depth);
            return math.clamp(settings.Lod2Ratio * densityScale, 0.01f, 0.8f);
        }

        private static float ResolvePrimitiveTolerance(OfflineBakeSettings settings)
        {
            float quality = math.saturate(settings.GlobalQualityWeight);
            float curve = math.smoothstep(0f, 1f, quality);
            float depth = math.saturate((settings.DepthMeters - 300f) * math.rcp(1700f));
            float lieScale = math.lerp(1.85f, 0.82f, curve) + depth * 0.35f;
            return math.max(0.001f, settings.PrimitiveTolerance * lieScale);
        }

        private static int ResolveDerivedLodBudget(int lod0HardBudget, float lodRatio)
        {
            int baseBudget = math.max(1, lod0HardBudget);
            float ratio = math.saturate(lodRatio);
            return math.max(1, (int)math.floor(baseBudget * ratio));
        }

        private static int ResolveDecimationWindow(OfflineBakeSettings settings)
        {
            float quality = math.saturate(settings.GlobalQualityWeight);
            float depth = math.saturate((settings.DepthMeters - 300f) * math.rcp(1700f));
            float curve = math.smoothstep(0f, 1f, quality) * math.lerp(1f, 0.65f, depth);
            int radius = (int)math.floor(math.lerp(0f, 3f, curve));
            return 1 + radius * 2;
        }

        private static float ResolveLod1Threshold(OfflineBakeSettings settings)
        {
            float quality = math.saturate(settings.GlobalQualityWeight);
            float depth = math.saturate((settings.DepthMeters - 400f) * math.rcp(1600f));
            return math.clamp(OfflineGeometryBakerConstants.DefaultLod1Threshold * math.lerp(1.35f, 0.78f, quality) + depth * 0.06f, 0.05f, 0.45f);
        }

        private static float ResolveLod2Threshold(OfflineBakeSettings settings)
        {
            float quality = math.saturate(settings.GlobalQualityWeight);
            float depth = math.saturate((settings.DepthMeters - 400f) * math.rcp(1600f));
            return math.clamp(OfflineGeometryBakerConstants.DefaultLod2Threshold * math.lerp(1.6f, 0.75f, quality) + depth * 0.025f, 0.015f, 0.2f);
        }

        private static float ResolveFadeTransitionWidth(OfflineBakeSettings settings, float lowQualityWidth, float highQualityWidth)
        {
            float quality = math.smoothstep(0f, 1f, math.saturate(settings.GlobalQualityWeight));
            float depth = math.saturate((settings.DepthMeters - 400f) * math.rcp(1600f));
            float width = math.lerp(lowQualityWidth, highQualityWidth, quality) * math.lerp(1f, 0.65f, depth);
            return math.clamp(width, 0.005f, 0.18f);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static uint StableHash(string value)
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

                return hash;
            }
        }

        private static uint StableHash(in FixedString128Bytes value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static uint FoldHash(uint current, uint value)
        {
            unchecked
            {
                uint hash = current == 0u ? 2166136261u : current;
                hash ^= value;
                hash *= 16777619u;
                return hash;
            }
        }

        private static uint FoldManifestRecord(OfflineLodManifestRecord record)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = FoldHash(hash, record.SourceHash);
                hash = FoldHash(hash, record.OutputHash);
                hash = FoldHash(hash, record.Lod1MeshHash);
                hash = FoldHash(hash, record.Lod2MeshHash);
                hash = FoldHash(hash, (uint)record.OriginalTriangles);
                hash = FoldHash(hash, (uint)record.Lod0Triangles);
                hash = FoldHash(hash, (uint)record.Lod1Triangles);
                hash = FoldHash(hash, (uint)record.Lod2Triangles);
                hash = FoldHash(hash, math.asuint(record.Lod1Threshold));
                hash = FoldHash(hash, math.asuint(record.Lod2Threshold));
                hash = FoldHash(hash, math.asuint(record.Lod1Ratio));
                hash = FoldHash(hash, math.asuint(record.Lod2Ratio));
                hash = FoldHash(hash, math.asuint(record.PrimitiveTolerance));
                hash = FoldHash(hash, math.asuint(record.GlobalQualityWeight));
                hash = FoldHash(hash, math.asuint(record.DepthMeters));
                hash = FoldHash(hash, (uint)record.DecimationWindow);
                hash = FoldHash(hash, record.WarningFlags);
                return hash;
            }
        }

        private static FixedString128Bytes ToFixed128(string value)
        {
            FixedString128Bytes fixedValue = default;
            if (!string.IsNullOrEmpty(value))
                fixedValue.CopyFromTruncated(value);
            return fixedValue;
        }

        private static OfflineBakeMetrics CreateBaseMetric(string sourcePath, OfflineBakeSettings settings)
        {
            string sourceToken = SanitizeToken(Path.GetFileNameWithoutExtension(sourcePath));
            string prefabName = "GEN_" + sourceToken + "_Optimized";
            string prefabPath = OfflineGeometryBakerConstants.PrefabOutputFolder + "/" + prefabName + ".prefab";
            return new OfflineBakeMetrics
            {
                SourcePath = ToFixed128(sourcePath),
                OutputPath = ToFixed128(prefabPath),
                Lod1Ratio = ResolveLod1Ratio(settings),
                Lod2Ratio = ResolveLod2Ratio(settings),
                PrimitiveTolerance = ResolvePrimitiveTolerance(settings),
                Lod1Threshold = ResolveLod1Threshold(settings),
                Lod2Threshold = ResolveLod2Threshold(settings),
                GlobalQualityWeight = math.saturate(settings.GlobalQualityWeight),
                DepthMeters = math.max(0f, settings.DepthMeters),
                DecimationWindow = ResolveDecimationWindow(settings)
            };
        }

        private static string SanitizeToken(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Unnamed";

            char[] chars = input.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool valid = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                if (!valid)
                    chars[i] = '_';
            }

            return new string(chars);
        }

        internal static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;

            string normalized = folder.Replace('\\', '/');
            if (!IsProjectAssetFolder(normalized) || AssetDatabase.IsValidFolder(normalized))
                return;

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

        private static bool TryEnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return false;

            string normalized = folder.Replace('\\', '/');
            if (!IsProjectAssetFolder(normalized))
                return false;

            EnsureAssetFolder(normalized);
            return AssetDatabase.IsValidFolder(normalized);
        }

        private static bool IsProjectAssetFolder(string folder)
        {
            return string.Equals(folder, "Assets", StringComparison.Ordinal) ||
                   folder.StartsWith("Assets/", StringComparison.Ordinal);
        }

        internal static void WriteOptimizationReport(List<OfflineBakeMetrics> metrics)
        {
            EnsureFileFolder(OfflineGeometryBakerConstants.OptimizationReportPath);
            WriteLodManifest(metrics);
            int processed = metrics != null ? metrics.Count : 0;
            long original = 0;
            long lodTotal = 0;
            int primitives = 0;
            int convex = 0;
            var builder = new StringBuilder(4096);
            builder.Append("{\n  \"agent\": \"SHINOBU_213\",\n  \"status\": \"PENDING_VERIFICATION\",\n  \"prefabsProcessed\": ");
            builder.Append(processed);
            builder.Append(",\n  \"items\": [\n");
            if (metrics != null)
            {
                for (int i = 0; i < metrics.Count; i++)
                {
                    OfflineBakeMetrics m = metrics[i];
                    original += m.OriginalTriangles;
                    lodTotal += m.Lod0Triangles + m.Lod1Triangles + m.Lod2Triangles;
                    primitives += m.PrimitiveColliderCount;
                    convex += m.ConvexColliderCount;
                    if (i > 0)
                        builder.Append(",\n");
                    builder.Append("    { \"source\": \"");
                    AppendEscapedFixedString(builder, in m.SourcePath);
                    builder.Append("\", \"output\": \"");
                    AppendEscapedFixedString(builder, in m.OutputPath);
                    builder.Append("\", \"originalTris\": ");
                    builder.Append(m.OriginalTriangles);
                    builder.Append(", \"lod0Tris\": ");
                    builder.Append(m.Lod0Triangles);
                    builder.Append(", \"lod1Tris\": ");
                    builder.Append(m.Lod1Triangles);
                    builder.Append(", \"lod2Tris\": ");
                    builder.Append(m.Lod2Triangles);
                    builder.Append(", \"primitiveColliders\": ");
                    builder.Append(m.PrimitiveColliderCount);
                    builder.Append(", \"convexColliders\": ");
                    builder.Append(m.ConvexColliderCount);
                    builder.Append(", \"lod1Ratio\": ");
                    AppendFixed(builder, m.Lod1Ratio);
                    builder.Append(", \"lod2Ratio\": ");
                    AppendFixed(builder, m.Lod2Ratio);
                    builder.Append(", \"primitiveTolerance\": ");
                    AppendFixed(builder, m.PrimitiveTolerance);
                    builder.Append(", \"decimationWindow\": ");
                    builder.Append(m.DecimationWindow);
                    builder.Append(", \"lod1Threshold\": ");
                    AppendFixed(builder, m.Lod1Threshold);
                    builder.Append(", \"lod2Threshold\": ");
                    AppendFixed(builder, m.Lod2Threshold);
                    builder.Append(", \"globalQualityWeight\": ");
                    AppendFixed(builder, m.GlobalQualityWeight);
                    builder.Append(", \"depthMeters\": ");
                    AppendFixed(builder, m.DepthMeters);
                    builder.Append(", \"lod1MeshHash\": ");
                    builder.Append(m.Lod1MeshHash);
                    builder.Append(", \"lod2MeshHash\": ");
                    builder.Append(m.Lod2MeshHash);
                    builder.Append(", \"burstExtractionMs\": ");
                    AppendFixed(builder, m.ExtractionMilliseconds);
                    builder.Append(", \"serializationMs\": ");
                    AppendFixed(builder, m.SerializationMilliseconds);
                    builder.Append(", \"warning\": \"");
                    builder.Append(m.WarningFlags == 0u ? "NONE" : "CRITICAL_WARNING");
                    builder.Append("\" }");
                }
            }

            builder.Append("\n  ],\n  \"totalOriginalTris\": ");
            builder.Append(original);
            builder.Append(",\n  \"totalGeneratedLodTris\": ");
            builder.Append(lodTotal);
            builder.Append(",\n  \"primitiveColliderCount\": ");
            builder.Append(primitives);
            builder.Append(",\n  \"convexColliderCount\": ");
            builder.Append(convex);
            builder.Append(",\n  \"binaryManifest\": \"");
            builder.Append(Escape(OfflineGeometryBakerConstants.LodManifestPath));
            builder.Append("\",\n  \"netcodeExclusion\": \"Generated mesh, LODGroup, and collider data are immutable presentation/environment data. Do not add LOD selected state or screen thresholds to StateRingBuffer/Merkle hashing. Synchronize existence and AUP pose only.\"\n}\n");
            WriteTextFileAtomic(OfflineGeometryBakerConstants.OptimizationReportPath, builder.ToString());
            OfflineGeometrySelfAudit.WriteSelfAuditReport();
        }

        private static void WriteLodManifest(List<OfflineBakeMetrics> metrics)
        {
            OfflineGeometryVertexLayoutValidator.ValidateStructs();
            string manifestPath = OfflineGeometryBakerConstants.LodManifestPath;
            string manifestFolder = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
            if (!TryEnsureAssetFolder(manifestFolder))
                throw new IOException("[SHINOBU_213] Invalid .h8lod manifest folder: " + manifestFolder);
            EnsureFileFolder(manifestPath);
            int count = metrics != null ? metrics.Count : 0;
            uint sourceAggregate = 2166136261u;
            uint outputAggregate = 2166136261u;
            NativeArray<OfflineLodManifestRecord> records = default;
            string tempPath = manifestPath + ".tmp";
            try
            {
                if (count > 0)
                {
                    // COLD ALLOC: NativeArray<OfflineLodManifestRecord>[count] - editor binary LOD manifest staging - owner: OfflineGeometryBaker
                    records = AllocateTrackedNativeArray<OfflineLodManifestRecord>(count, Allocator.Temp, NativeArrayOptions.UninitializedMemory, nameof(records));
                    for (int i = 0; i < count; i++)
                    {
                        OfflineBakeMetrics metric = metrics[i];
                        uint sourceHash = StableHash(in metric.SourcePath);
                        uint outputHash = StableHash(in metric.OutputPath);
                        sourceAggregate = FoldHash(sourceAggregate, sourceHash);
                        outputAggregate = FoldHash(outputAggregate, outputHash);
                        OfflineLodManifestRecord record = new OfflineLodManifestRecord
                        {
                            SourceHash = sourceHash,
                            OutputHash = outputHash,
                            Lod1MeshHash = metric.Lod1MeshHash,
                            Lod2MeshHash = metric.Lod2MeshHash,
                            OriginalTriangles = metric.OriginalTriangles,
                            Lod0Triangles = metric.Lod0Triangles,
                            Lod1Triangles = metric.Lod1Triangles,
                            Lod2Triangles = metric.Lod2Triangles,
                            PrimitiveColliderCount = metric.PrimitiveColliderCount,
                            ConvexColliderCount = metric.ConvexColliderCount,
                            Lod1Threshold = SanitizeFinite(metric.Lod1Threshold),
                            Lod2Threshold = SanitizeFinite(metric.Lod2Threshold),
                            Lod1Ratio = SanitizeFinite(metric.Lod1Ratio),
                            Lod2Ratio = SanitizeFinite(metric.Lod2Ratio),
                            PrimitiveTolerance = SanitizeFinite(metric.PrimitiveTolerance),
                            GlobalQualityWeight = SanitizeFinite(metric.GlobalQualityWeight),
                            DepthMeters = SanitizeFinite(metric.DepthMeters),
                            DecimationWindow = metric.DecimationWindow,
                            WarningFlags = metric.WarningFlags
                        };
                        record.StateHash = FoldManifestRecord(record);
                        records[i] = record;
                    }
                }

                OfflineLodManifestHeader header = new OfflineLodManifestHeader
                {
                    Magic = LodManifestMagic,
                    Version = 1,
                    HeaderBytes = UnsafeUtility.SizeOf<OfflineLodManifestHeader>(),
                    RecordCount = count,
                    RecordBytes = UnsafeUtility.SizeOf<OfflineLodManifestRecord>(),
                    EndianTag = LittleEndianTag,
                    AgentHash = AgentStableHash,
                    SourceAggregateHash = sourceAggregate,
                    OutputAggregateHash = outputAggregate
                };

                long expectedBytes = 64L + ((long)count * 128L);
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Span<byte> headerBytes = stackalloc byte[64];
                    WriteManifestHeaderLittleEndian(headerBytes, in header);
                    stream.Write(headerBytes);
                    if (records.IsCreated && records.Length > 0)
                    {
                        Span<byte> recordBytes = stackalloc byte[128];
                        for (int i = 0; i < records.Length; i++)
                        {
                            OfflineLodManifestRecord record = records[i];
                            WriteManifestRecordLittleEndian(recordBytes, in record);
                            stream.Write(recordBytes);
                        }
                    }

                    stream.Flush(true);
                }

                long actualBytes = new FileInfo(tempPath).Length;
                if (actualBytes != expectedBytes)
                    throw new IOException("[SHINOBU_213] Torn .h8lod manifest write. expected=" + expectedBytes.ToString(CultureInfo.InvariantCulture) + " actual=" + actualBytes.ToString(CultureInfo.InvariantCulture));

                ReplaceTempFile(tempPath, manifestPath);
                AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                DisposeTrackedNativeArray(ref records);
            }
        }

        private static void WriteManifestHeaderLittleEndian(Span<byte> bytes, in OfflineLodManifestHeader header)
        {
            WriteUInt32Little(bytes, 0, header.Magic);
            WriteUInt32Little(bytes, 4, header.Version);
            WriteInt32Little(bytes, 8, header.HeaderBytes);
            WriteInt32Little(bytes, 12, header.RecordCount);
            WriteInt32Little(bytes, 16, header.RecordBytes);
            WriteUInt32Little(bytes, 20, header.EndianTag);
            WriteUInt32Little(bytes, 24, header.AgentHash);
            WriteUInt32Little(bytes, 28, header.SourceAggregateHash);
            WriteUInt32Little(bytes, 32, header.OutputAggregateHash);
            WriteUInt32Little(bytes, 36, header.Reserved0);
            WriteUInt32Little(bytes, 40, header.Reserved1);
            WriteUInt32Little(bytes, 44, header.Reserved2);
            WriteUInt32Little(bytes, 48, header.Reserved3);
            WriteUInt32Little(bytes, 52, header.Reserved4);
            WriteUInt32Little(bytes, 56, header.Reserved5);
            WriteUInt32Little(bytes, 60, header.Reserved6);
        }

        private static void WriteManifestRecordLittleEndian(Span<byte> bytes, in OfflineLodManifestRecord record)
        {
            WriteUInt32Little(bytes, 0, record.SourceHash);
            WriteUInt32Little(bytes, 4, record.OutputHash);
            WriteUInt32Little(bytes, 8, record.Lod1MeshHash);
            WriteUInt32Little(bytes, 12, record.Lod2MeshHash);
            WriteInt32Little(bytes, 16, record.OriginalTriangles);
            WriteInt32Little(bytes, 20, record.Lod0Triangles);
            WriteInt32Little(bytes, 24, record.Lod1Triangles);
            WriteInt32Little(bytes, 28, record.Lod2Triangles);
            WriteInt32Little(bytes, 32, record.PrimitiveColliderCount);
            WriteInt32Little(bytes, 36, record.ConvexColliderCount);
            WriteFloatLittle(bytes, 40, record.Lod1Threshold);
            WriteFloatLittle(bytes, 44, record.Lod2Threshold);
            WriteFloatLittle(bytes, 48, record.Lod1Ratio);
            WriteFloatLittle(bytes, 52, record.Lod2Ratio);
            WriteFloatLittle(bytes, 56, record.PrimitiveTolerance);
            WriteFloatLittle(bytes, 60, record.GlobalQualityWeight);
            WriteFloatLittle(bytes, 64, record.DepthMeters);
            WriteInt32Little(bytes, 68, record.DecimationWindow);
            WriteUInt32Little(bytes, 72, record.WarningFlags);
            WriteUInt32Little(bytes, 76, record.StateHash);
            WriteUInt32Little(bytes, 80, record.Reserved0);
            WriteUInt32Little(bytes, 84, record.Reserved1);
            WriteUInt32Little(bytes, 88, record.Reserved2);
            WriteUInt32Little(bytes, 92, record.Reserved3);
            WriteUInt32Little(bytes, 96, record.Reserved4);
            WriteUInt32Little(bytes, 100, record.Reserved5);
            WriteUInt32Little(bytes, 104, record.Reserved6);
            WriteUInt32Little(bytes, 108, record.Reserved7);
            WriteUInt32Little(bytes, 112, record.Reserved8);
            WriteUInt32Little(bytes, 116, record.Reserved9);
            WriteUInt32Little(bytes, 120, record.Reserved10);
            WriteUInt32Little(bytes, 124, record.Reserved11);
        }

        private static void WriteFloatLittle(Span<byte> bytes, int offset, float value)
        {
            WriteUInt32Little(bytes, offset, math.asuint(value));
        }

        private static void WriteInt32Little(Span<byte> bytes, int offset, int value)
        {
            WriteUInt32Little(bytes, offset, unchecked((uint)value));
        }

        private static void WriteUInt32Little(Span<byte> bytes, int offset, uint value)
        {
            uint endianSafe = BitConverter.IsLittleEndian ? value : ReverseBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                bytes[offset] = (byte)endianSafe;
                bytes[offset + 1] = (byte)(endianSafe >> 8);
                bytes[offset + 2] = (byte)(endianSafe >> 16);
                bytes[offset + 3] = (byte)(endianSafe >> 24);
            }
            else
            {
                bytes[offset] = (byte)(endianSafe >> 24);
                bytes[offset + 1] = (byte)(endianSafe >> 16);
                bytes[offset + 2] = (byte)(endianSafe >> 8);
                bytes[offset + 3] = (byte)endianSafe;
            }
        }

        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        internal static void EnsureFileFolder(string relativePath)
        {
            string folder = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        internal static void WriteTextFileAtomic(string relativePath, string contents)
        {
            EnsureFileFolder(relativePath);
            string tempPath = relativePath + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (StreamWriter writer = new StreamWriter(stream, _Utf8NoBom, 1024, true))
                    {
                        writer.Write(contents ?? string.Empty);
                        writer.Flush();
                    }

                    stream.Flush(true);
                }

                ReplaceTempFile(tempPath, relativePath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        internal static void ReplaceTempFile(string tempPath, string finalPath)
        {
            EnsureFileFolder(finalPath);
            if (File.Exists(finalPath))
            {
                string backupPath = finalPath + ".bak";
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                File.Replace(tempPath, finalPath, backupPath, true);
                return;
            }

            File.Move(tempPath, finalPath);
        }

        internal static void AppendFixed(StringBuilder builder, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                builder.Append("0.000");
                return;
            }

            builder.Append(value.ToString("0.000", CultureInfo.InvariantCulture));
        }

        private static float SanitizeFinite(float value)
        {
            return math.isfinite(value) ? value : 0f;
        }

        internal static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            StringBuilder builder = null;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                string replacement = null;
                switch (c)
                {
                    case '\\':
                        replacement = "\\\\";
                        break;
                    case '"':
                        replacement = "\\\"";
                        break;
                    case '\n':
                        replacement = "\\n";
                        break;
                    case '\r':
                        replacement = "\\r";
                        break;
                    case '\t':
                        replacement = "\\t";
                        break;
                    case '\b':
                        replacement = "\\b";
                        break;
                    case '\f':
                        replacement = "\\f";
                        break;
                }

                if (replacement == null && c >= 0x20)
                {
                    if (builder != null)
                        builder.Append(c);
                    continue;
                }

                if (builder == null)
                {
                    builder = new StringBuilder(value.Length + 8);
                    for (int prefix = 0; prefix < i; prefix++)
                        builder.Append(value[prefix]);
                }

                if (replacement != null)
                    builder.Append(replacement);
                else
                    AppendControlCharacterEscape(builder, c);
            }

            return builder != null ? builder.ToString() : value;
        }

        private static void AppendEscapedFixedString(StringBuilder builder, in FixedString128Bytes value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] >= 0x80)
                {
                    builder.Append(Escape(value.ToString()));
                    return;
                }
            }

            for (int i = 0; i < value.Length; i++)
                AppendEscapedJsonChar(builder, (char)value[i]);
        }

        private static void AppendEscapedJsonChar(StringBuilder builder, char value)
        {
            switch (value)
            {
                case '\\':
                    builder.Append("\\\\");
                    return;
                case '"':
                    builder.Append("\\\"");
                    return;
                case '\n':
                    builder.Append("\\n");
                    return;
                case '\r':
                    builder.Append("\\r");
                    return;
                case '\t':
                    builder.Append("\\t");
                    return;
                case '\b':
                    builder.Append("\\b");
                    return;
                case '\f':
                    builder.Append("\\f");
                    return;
            }

            if (value < 0x20)
            {
                AppendControlCharacterEscape(builder, value);
                return;
            }

            builder.Append(value);
        }

        private static void AppendControlCharacterEscape(StringBuilder builder, char value)
        {
            builder.Append("\\u00");
            AppendHexNibble(builder, (value >> 4) & 0xF);
            AppendHexNibble(builder, value & 0xF);
        }

        private static void AppendHexNibble(StringBuilder builder, int value)
        {
            builder.Append((char)(value < 10 ? '0' + value : 'A' + (value - 10)));
        }

        private struct SourceVertexLayout
        {
            public int PositionStream;
            public int PositionStride;
            public int PositionOffset;
            public int NormalStream;
            public int NormalStride;
            public int NormalOffset;
            public int Uv0Stream;
            public int Uv0Stride;
            public int Uv0Offset;
            public byte HasNormals;
            public byte HasUv0;
        }
    }
}
#endif
