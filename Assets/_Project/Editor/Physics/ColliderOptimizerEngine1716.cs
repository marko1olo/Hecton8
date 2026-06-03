using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.ColliderOptimization1716
{
    public enum ColliderOptimizerMode1716
    {
        Auto = 0,
        CompoundPrimitives = 1,
        ConvexProxy = 2,
        StripOnly = 3
    }

    public struct ColliderOptimizerSettings1716
    {
        public float GlobalQualityWeight;
        public int MaxPrimitiveCollidersPerPrefab;
        public int HullSupportDirectionCount;
        public float ProxyPaddingMeters;
        public float LowTierCullDistanceMeters;
        public float UltraTierCullDistanceMeters;

        public static ColliderOptimizerSettings1716 FromGlobalQualityWeight(float globalQualityWeight)
        {
            float quality = ColliderOptimizerEngine1716.SanitizeQuality(globalQualityWeight);
            return new ColliderOptimizerSettings1716
            {
                GlobalQualityWeight = quality,
                MaxPrimitiveCollidersPerPrefab = Mathf.RoundToInt(Mathf.Lerp(
                    ColliderOptimizerEngine1716.MinPrimitiveCollidersPerPrefab,
                    ColliderOptimizerEngine1716.MaxPrimitiveCollidersPerPrefab,
                    quality)),
                HullSupportDirectionCount = Mathf.RoundToInt(Mathf.Lerp(
                    ColliderOptimizerEngine1716.MinHullSupportDirections,
                    ColliderOptimizerEngine1716.MaxHullSupportDirections,
                    quality)),
                ProxyPaddingMeters = Mathf.Lerp(
                    ColliderOptimizerEngine1716.MaxProxyPaddingMeters,
                    ColliderOptimizerEngine1716.MinProxyPaddingMeters,
                    quality),
                LowTierCullDistanceMeters = Mathf.Lerp(42f, 64f, quality),
                UltraTierCullDistanceMeters = Mathf.Lerp(96f, 160f, quality)
            };
        }
    }

    public struct ColliderOptimizerReport1716
    {
        public int PrefabsVisited;
        public int PrefabsModified;
        public int PrefabsFailed;
        public int MeshCollidersFound;
        public int HighPolyMeshColliders;
        public int VisualLod0MeshCollidersRemoved;
        public int MeshCollidersDeleted;
        public int PrimitiveCollidersGenerated;
        public int CapsuleCollidersGenerated;
        public int BoxCollidersGenerated;
        public int SphereCollidersGenerated;
        public int ProxyMeshesGenerated;
        public int ProxyMeshesDeleted;
        public int NavMeshObstaclesInjected;
        public int LayerAssignments;
        public int MaterialAssignments;
        public int VisualTrianglesRemovedFromPhysics;
        public int ValidatorFailures;
        public int RuntimeBakeCallsitesFound;
        public int RuntimeSharedMeshAssignmentsFound;
        public long ExecutionMicroseconds;
        public float GlobalQualityWeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ColliderPrimitiveFit1716
    {
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector3 Size;
        public float SphereRadius;
        public float CapsuleRadius;
        public float CapsuleHeight;
        public int CapsuleDirection;
        public byte PrimitiveKind;
        public byte Pad0;
        public byte Pad1;
        public byte Pad2;
        public int Pad3;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OptimizerTelemetryEntry1716
    {
        public int Stage;
        public int PathHash;
        public int A;
        public int B;
        public float C;
        public int Pad;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal struct HullSupportPointJob1716 : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Vertices;
        [ReadOnly] public NativeArray<float3> Directions;
        [WriteOnly] public NativeArray<float3> SupportPoints;

        public void Execute(int index)
        {
            float3 direction = Directions[index];
            float bestDistance = float.NegativeInfinity;
            float3 bestPoint = default;
            for (int i = 0; i < Vertices.Length; i++)
            {
                float distance = math.dot(Vertices[i], direction);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestPoint = Vertices[i];
                }
            }

            SupportPoints[index] = bestPoint;
        }
    }

    public static class ColliderOptimizerEngine1716
    {
        public const int MeshColliderFatalTriangleLimit = 500;
        public const int ProxyMeshTriangleLimit = 200;
        public const string PrefabRoot = "Assets/_Project/Prefabs";
        public const string ProxyAssetRoot = "Assets/_Project/Prefabs/PhysicsProxies";
        public const string MaterialAssetRoot = "Assets/_Project/Data/Generated/ColliderOptimizer1716/Materials";
        public const float DefaultGlobalQualityWeight = 0.5f;
        public const int MinPrimitiveCollidersPerPrefab = 2;
        public const int MaxPrimitiveCollidersPerPrefab = 10;
        public const int MinHullSupportDirections = 10;
        public const int MaxHullSupportDirections = 42;
        public const float MinProxyPaddingMeters = 0.015f;
        public const float MaxProxyPaddingMeters = 0.08f;

        public const string GeneratedCompoundRootName = "COL_CompoundProxy_1716";
        public const string GeneratedConvexRootName = "COL_ConvexProxy_1716";
        private const float MinimumColliderAxisMeters = 0.025f;
        private const float CapsuleAspectThreshold = 2.35f;
        private const float CapsuleCircularityTolerance = 0.45f;
        private const int TelemetryCapacity = 300;
        private const int MeshColliderScratchCapacity = 384;
        private const int ColliderScratchCapacity = 512;
        private const int MeshFilterScratchCapacity = 768;
        private const int VertexScratchCapacity = 65536;
        private const int IndexScratchCapacity = 131072;

        // COLD ALLOC: editor prefab scan scratch; not used in play-mode hot paths.
        private static readonly List<MeshCollider> s_MeshColliderScratch = new List<MeshCollider>(MeshColliderScratchCapacity);
        // COLD ALLOC: editor collider mutation scratch; not used in play-mode hot paths.
        private static readonly List<Collider> s_ColliderScratch = new List<Collider>(ColliderScratchCapacity);
        // COLD ALLOC: editor mesh source scratch; not used in play-mode hot paths.
        private static readonly List<MeshFilter> s_MeshFilterScratch = new List<MeshFilter>(MeshFilterScratchCapacity);
        // COLD ALLOC: editor mesh vertex extraction scratch.
        private static readonly List<Vector3> s_VertexScratch = new List<Vector3>(VertexScratchCapacity);
        // COLD ALLOC: editor per-mesh vertex read scratch; preserves multi-mesh hull aggregation.
        private static readonly List<Vector3> s_SourceVertexScratch = new List<Vector3>(VertexScratchCapacity);
        // COLD ALLOC: editor mesh triangle index scratch.
        private static readonly List<int> s_IndexScratch = new List<int>(IndexScratchCapacity);
        // COLD ALLOC: editor hull point scratch.
        private static readonly List<Vector3> s_HullPointScratch = new List<Vector3>(MaxHullSupportDirections + 8);
        // COLD ALLOC: editor hull triangle scratch.
        private static readonly List<int> s_HullTriangleScratch = new List<int>(ProxyMeshTriangleLimit * 3);

        private static NativeArray<OptimizerTelemetryEntry1716> s_TelemetryRing;
        private static int s_TelemetryCursor;
        private static bool s_TelemetryAllocated;

        private static readonly int[] s_BoxHullTriangles =
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            1, 2, 6, 1, 6, 5,
            2, 3, 7, 2, 7, 6,
            3, 0, 4, 3, 4, 7
        };

        static ColliderOptimizerEngine1716()
        {
            ValidateEditorStructLayouts();
            AssemblyReloadEvents.beforeAssemblyReload += DisposeTelemetry;
            EditorApplication.quitting += DisposeTelemetry;
        }

        private static void ValidateEditorStructLayouts()
        {
            ValidateStructAligned<ColliderPrimitiveFit1716>(nameof(ColliderPrimitiveFit1716));
            ValidateStructAligned<OptimizerTelemetryEntry1716>(nameof(OptimizerTelemetryEntry1716));
        }

        private static void ValidateStructAligned<T>(string label) where T : struct
        {
            int size = UnsafeUtility.SizeOf<T>();
            if ((size & 7) != 0)
                throw new InvalidOperationException("[ColliderOptimizer1716] " + label + " size " + size + " is not 8-byte aligned.");
        }

        [MenuItem("HECTON-8/Physics/1716 Audit Collider Topology", false, 1716)]
        public static void AuditDefaultFolderMenu()
        {
            ColliderOptimizerReport1716 report = AuditPrefabs(PrefabRoot, true);
            Debug.Log("[ColliderOptimizer1716] Audit complete. Prefabs=" + report.PrefabsVisited +
                      " MeshColliders=" + report.MeshCollidersFound +
                      " HighPoly=" + report.HighPolyMeshColliders +
                      " RuntimeBakeCalls=" + report.RuntimeBakeCallsitesFound);
        }

        [MenuItem("HECTON-8/Physics/1716 Optimize Collider Proxies", false, 1717)]
        public static void OptimizeDefaultFolderMenu()
        {
            ColliderOptimizerReport1716 report = OptimizeFolder(PrefabRoot, ColliderOptimizerMode1716.Auto, ColliderOptimizerSettings1716.FromGlobalQualityWeight(DefaultGlobalQualityWeight), false);
            Debug.Log("[ColliderOptimizer1716] Optimize complete. PrefabsModified=" + report.PrefabsModified +
                      " MeshDeleted=" + report.MeshCollidersDeleted +
                      " Primitive=" + report.PrimitiveCollidersGenerated +
                      " ProxyMeshes=" + report.ProxyMeshesGenerated +
                      " Failures=" + report.PrefabsFailed);
        }

        public static ColliderOptimizerReport1716 AuditPrefabs(string folder, bool emitErrors)
        {
            EnsureTelemetry();
            ColliderOptimizerReport1716 report = default;
            report.GlobalQualityWeight = DefaultGlobalQualityWeight;
            Stopwatch stopwatch = Stopwatch.StartNew();
            string[] prefabPaths = FindPrefabPaths(folder);
            for (int i = 0; i < prefabPaths.Length; i++)
                AuditPrefab(prefabPaths[i], emitErrors, ref report);

            AuditRuntimeCookingCallsites(ref report);
            stopwatch.Stop();
            report.ExecutionMicroseconds = StopwatchTicksToMicroseconds(stopwatch.ElapsedTicks);
            return report;
        }

        public static ColliderOptimizerReport1716 OptimizeFolder(string folder, ColliderOptimizerMode1716 mode)
        {
            return OptimizeFolder(folder, mode, ColliderOptimizerSettings1716.FromGlobalQualityWeight(DefaultGlobalQualityWeight), false);
        }

        public static ColliderOptimizerReport1716 OptimizeFolder(string folder, ColliderOptimizerMode1716 mode, ColliderOptimizerSettings1716 settings, bool dryRun)
        {
            EnsureTelemetry();
            settings = NormalizeSettings(settings);
            ColliderOptimizerReport1716 report = default;
            report.GlobalQualityWeight = settings.GlobalQualityWeight;
            Stopwatch stopwatch = Stopwatch.StartNew();
            string[] prefabPaths = FindPrefabPaths(folder);

            if (!dryRun)
                AssetDatabase.StartAssetEditing();

            try
            {
                for (int i = 0; i < prefabPaths.Length; i++)
                    TryOptimizePrefabAsset(prefabPaths[i], mode, settings, dryRun, ref report);
            }
            finally
            {
                if (!dryRun)
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            AuditRuntimeCookingCallsites(ref report);
            stopwatch.Stop();
            report.ExecutionMicroseconds = StopwatchTicksToMicroseconds(stopwatch.ElapsedTicks);
            return report;
        }

        public static bool ValidatePrefabAssetTopology(string prefabPath, out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrEmpty(prefabPath))
            {
                failure = "Empty prefab path.";
                return false;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                failure = "Prefab asset not found: " + prefabPath;
                return false;
            }

            return ValidatePrefabColliderBudget(prefab, out failure);
        }

        public static bool ValidatePrefabColliderBudget(GameObject prefab, out string failure)
        {
            failure = string.Empty;
            if (prefab == null)
            {
                failure = "Null prefab.";
                return false;
            }

            prefab.GetComponentsInChildren(true, s_MeshColliderScratch);
            try
            {
                for (int i = 0; i < s_MeshColliderScratch.Count; i++)
                {
                    MeshCollider meshCollider = s_MeshColliderScratch[i];
                    if (meshCollider == null)
                        continue;

                    int triangles = CountMeshTrianglesNoAlloc(meshCollider.sharedMesh);
                    if (triangles > ProxyMeshTriangleLimit)
                    {
                        failure = prefab.name + " owns MeshCollider over 200 triangles: " + triangles;
                        return false;
                    }

                    if (IsPrimaryVisualMeshReference(prefab, meshCollider))
                    {
                        failure = prefab.name + " still has MeshCollider on a primary visual LOD0 mesh.";
                        return false;
                    }
                }
            }
            finally
            {
                s_MeshColliderScratch.Clear();
            }

            if (!ValidateGeneratedColliderRoots(prefab, out failure))
                return false;

            return ValidateColliderCount(prefab, out failure);
        }

        public static bool ValidateGeneratedColliderRoots(GameObject prefab, out string failure)
        {
            failure = string.Empty;
            if (prefab == null)
            {
                failure = "Null prefab.";
                return false;
            }

            Transform compound = prefab.transform.Find(GeneratedCompoundRootName);
            if (compound != null && !ValidateGeneratedRootHasCollider(compound, prefab.name, out failure))
                return false;

            Transform convex = prefab.transform.Find(GeneratedConvexRootName);
            if (convex == null)
                return true;

            if (!ValidateGeneratedRootHasCollider(convex, prefab.name, out failure))
                return false;

            convex.GetComponentsInChildren(true, s_MeshColliderScratch);
            try
            {
                if (s_MeshColliderScratch.Count != 1)
                {
                    failure = prefab.name + " convex proxy must own exactly one MeshCollider.";
                    return false;
                }

                MeshCollider collider = s_MeshColliderScratch[0];
                if (collider == null || !collider.convex)
                {
                    failure = prefab.name + " convex proxy MeshCollider is not convex.";
                    return false;
                }

                return ValidateProxyMesh(collider.sharedMesh, out failure);
            }
            finally
            {
                s_MeshColliderScratch.Clear();
            }
        }

        public static bool ValidateColliderCount(GameObject prefab, out string failure)
        {
            failure = string.Empty;
            if (prefab == null)
            {
                failure = "Null prefab.";
                return false;
            }

            Transform compound = prefab.transform.Find(GeneratedCompoundRootName);
            if (compound == null)
                return true;

            compound.GetComponentsInChildren(true, s_ColliderScratch);
            try
            {
                if (s_ColliderScratch.Count > MaxPrimitiveCollidersPerPrefab)
                {
                    failure = prefab.name + " compound collider count exceeds 10: " + s_ColliderScratch.Count;
                    return false;
                }
            }
            finally
            {
                s_ColliderScratch.Clear();
            }

            return true;
        }

        private static bool ValidateGeneratedRootHasCollider(Transform root, string prefabName, out string failure)
        {
            failure = string.Empty;
            root.GetComponentsInChildren(true, s_ColliderScratch);
            try
            {
                if (s_ColliderScratch.Count > 0)
                    return true;

                failure = prefabName + " generated collider root has no Collider components.";
                return false;
            }
            finally
            {
                s_ColliderScratch.Clear();
            }
        }

        public static bool ValidateProxyMesh(Mesh proxyMesh, out string failure)
        {
            failure = string.Empty;
            if (proxyMesh == null)
            {
                failure = "Null proxy mesh.";
                return false;
            }

            if (proxyMesh.vertexCount < 4)
            {
                failure = proxyMesh.name + " has fewer than 4 proxy vertices.";
                return false;
            }

            int triangles = CountMeshTrianglesNoAlloc(proxyMesh);
            if (triangles <= 0)
            {
                failure = proxyMesh.name + " has no proxy triangles.";
                return false;
            }

            if (triangles > ProxyMeshTriangleLimit)
            {
                failure = proxyMesh.name + " exceeds 200 proxy triangles: " + triangles;
                return false;
            }

            Bounds bounds = proxyMesh.bounds;
            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
            {
                failure = proxyMesh.name + " has non-finite bounds.";
                return false;
            }

            Vector3 size = bounds.size;
            if (size.x < MinimumColliderAxisMeters ||
                size.y < MinimumColliderAxisMeters ||
                size.z < MinimumColliderAxisMeters)
            {
                failure = proxyMesh.name + " has non-volumetric proxy bounds.";
                return false;
            }

            return true;
        }

        internal static float SanitizeQuality(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? DefaultGlobalQualityWeight : Mathf.Clamp01(value);
        }

        private static void AuditPrefab(string prefabPath, bool emitErrors, ref ColliderOptimizerReport1716 report)
        {
            report.PrefabsVisited++;
            RecordTrace(1, prefabPath, 0, 0, 0f);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return;

            prefab.GetComponentsInChildren(true, s_MeshColliderScratch);
            try
            {
                for (int i = 0; i < s_MeshColliderScratch.Count; i++)
                {
                    MeshCollider meshCollider = s_MeshColliderScratch[i];
                    if (meshCollider == null)
                        continue;

                    report.MeshCollidersFound++;
                    int triangles = CountMeshTrianglesNoAlloc(meshCollider.sharedMesh);
                    if (triangles > MeshColliderFatalTriangleLimit || IsPrimaryVisualMeshReference(prefab, meshCollider))
                    {
                        report.HighPolyMeshColliders++;
                        if (emitErrors)
                            Debug.LogError("[ColliderOptimizer1716] Illegal MeshCollider: " + prefabPath + " triangles=" + triangles, prefab);
                    }
                }
            }
            finally
            {
                s_MeshColliderScratch.Clear();
            }
        }

        private static void TryOptimizePrefabAsset(string prefabPath, ColliderOptimizerMode1716 requestedMode, ColliderOptimizerSettings1716 settings, bool dryRun, ref ColliderOptimizerReport1716 report)
        {
            try
            {
                OptimizePrefabAsset(prefabPath, requestedMode, settings, dryRun, ref report);
            }
            catch (Exception exception)
            {
                report.PrefabsFailed++;
                RecordTrace(99, prefabPath, report.PrefabsFailed, 0, 0f);
                Debug.LogError("[ColliderOptimizer1716] Failed prefab optimization: " + prefabPath + " " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void OptimizePrefabAsset(string prefabPath, ColliderOptimizerMode1716 requestedMode, ColliderOptimizerSettings1716 settings, bool dryRun, ref ColliderOptimizerReport1716 report)
        {
            report.PrefabsVisited++;
            RecordTrace(2, prefabPath, report.PrefabsVisited, dryRun ? 1 : 0, settings.GlobalQualityWeight);
            GameObject root = null;
            bool changed = false;

            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    report.PrefabsFailed++;
                    return;
                }

                ColliderOptimizerMode1716 mode = ResolveMode(root, prefabPath, requestedMode);
                if (dryRun)
                {
                    DryRunPrefab(root, prefabPath, mode, ref report);
                    return;
                }

                RemoveGeneratedRoots(root.transform, ref report);
                if (mode == ColliderOptimizerMode1716.StripOnly)
                {
                    changed |= StripAllColliders(root, ref report);
                }
                else if (mode == ColliderOptimizerMode1716.ConvexProxy)
                {
                    changed |= GenerateConvexProxy(root, prefabPath, settings, ref report);
                }
                else
                {
                    changed |= GenerateCompoundPrimitives(root, prefabPath, settings, ref report);
                }

                changed |= EnforceLayersAndMaterials(root, prefabPath, ref report);
                if (changed && !ValidatePrefabColliderBudget(root, out string failure))
                {
                    report.ValidatorFailures++;
                    Debug.LogError("[ColliderOptimizer1716] Validator failure before save: " + prefabPath + " :: " + failure, root);
                    return;
                }

                if (changed)
                {
                    if (!SerializeGeneratedProxyMeshes(root, prefabPath, settings, ref report, out failure))
                    {
                        report.ValidatorFailures++;
                        Debug.LogError("[ColliderOptimizer1716] Proxy serialization failure before save: " + prefabPath + " :: " + failure, root);
                        return;
                    }

                    if (!ValidatePrefabColliderBudget(root, out failure))
                    {
                        report.ValidatorFailures++;
                        Debug.LogError("[ColliderOptimizer1716] Validator failure after proxy serialization: " + prefabPath + " :: " + failure, root);
                        return;
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    report.PrefabsModified++;
                }
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void DryRunPrefab(GameObject root, string prefabPath, ColliderOptimizerMode1716 mode, ref ColliderOptimizerReport1716 report)
        {
            root.GetComponentsInChildren(true, s_MeshColliderScratch);
            try
            {
                report.MeshCollidersFound += s_MeshColliderScratch.Count;
                for (int i = 0; i < s_MeshColliderScratch.Count; i++)
                {
                    MeshCollider meshCollider = s_MeshColliderScratch[i];
                    if (meshCollider == null)
                        continue;

                    int triangles = CountMeshTrianglesNoAlloc(meshCollider.sharedMesh);
                    if (triangles > MeshColliderFatalTriangleLimit || IsPrimaryVisualMeshReference(root, meshCollider))
                        report.HighPolyMeshColliders++;
                }
            }
            finally
            {
                s_MeshColliderScratch.Clear();
            }

            RecordTrace(3, prefabPath, (int)mode, report.HighPolyMeshColliders, 0f);
        }

        private static bool GenerateCompoundPrimitives(GameObject root, string prefabPath, ColliderOptimizerSettings1716 settings, ref ColliderOptimizerReport1716 report)
        {
            GameObject generatedRoot = CreateGeneratedRoot(root.transform, GeneratedCompoundRootName, prefabPath, root.name);
            Transform generatedTransform = generatedRoot.transform;

            int primitiveBudget = Mathf.Clamp(settings.MaxPrimitiveCollidersPerPrefab, MinPrimitiveCollidersPerPrefab, MaxPrimitiveCollidersPerPrefab);
            int generatedCount = 0;
            root.GetComponentsInChildren(true, s_MeshFilterScratch);
            try
            {
                for (int filterIndex = 0; filterIndex < s_MeshFilterScratch.Count; filterIndex++)
                {
                    if (generatedCount >= primitiveBudget)
                        break;

                    MeshFilter meshFilter = s_MeshFilterScratch[filterIndex];
                    if (meshFilter == null || meshFilter.transform == generatedTransform || meshFilter.transform.IsChildOf(generatedTransform))
                        continue;

                    if (!IsPrimaryCollisionVisual(meshFilter))
                        continue;

                    Mesh mesh = meshFilter.sharedMesh;
                    if (mesh == null || mesh.vertexCount <= 0)
                        continue;

                    Matrix4x4 sourceToRoot = root.transform.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
                    int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
                    for (int subMesh = 0; subMesh < subMeshCount && generatedCount < primitiveBudget; subMesh++)
                    {
                        if (!TryFitPrimitive(mesh, subMesh, sourceToRoot, meshFilter.name, out ColliderPrimitiveFit1716 fit))
                            continue;

                        Collider collider = CreatePrimitiveCollider(generatedTransform, meshFilter.name, subMesh, fit, generatedRoot.layer, ref report);
                        if (collider != null)
                        {
                            generatedCount++;
                        }
                    }
                }
            }
            finally
            {
                s_MeshFilterScratch.Clear();
            }

            if (generatedCount <= 0)
            {
                Bounds fallbackBounds;
                if (!TryCollectRootVisualBounds(root, settings.ProxyPaddingMeters, out fallbackBounds))
                {
                    Object.DestroyImmediate(generatedRoot, true);
                    report.ValidatorFailures++;
                    Debug.LogError("[ColliderOptimizer1716] Compound proxy could not be generated for " + prefabPath + ": no primary visual mesh vertices.", root);
                    return false;
                }

                BoxCollider fallback = generatedRoot.AddComponent<BoxCollider>();
                fallback.center = fallbackBounds.center;
                fallback.size = ClampColliderSize(fallbackBounds.size);
                report.BoxCollidersGenerated++;
                generatedCount = 1;
            }

            StripMeshColliders(root, false, ref report);
            InjectNavMeshObstacle(generatedRoot, root, prefabPath, ref report);
            report.PrimitiveCollidersGenerated += generatedCount;
            return true;
        }

        private static bool GenerateConvexProxy(GameObject root, string prefabPath, ColliderOptimizerSettings1716 settings, ref ColliderOptimizerReport1716 report)
        {
            Mesh proxyMesh = BuildConvexProxyMesh(root, settings, out _);
            string failure = "proxy mesh allocation failed";
            if (proxyMesh == null || !ValidateProxyMesh(proxyMesh, out failure))
            {
                if (proxyMesh != null)
                    Object.DestroyImmediate(proxyMesh);

                report.ValidatorFailures++;
                Debug.LogError("[ColliderOptimizer1716] Convex proxy rejected for " + prefabPath + ": " + failure, root);
                return GenerateCompoundPrimitives(root, prefabPath, settings, ref report);
            }

            StripMeshColliders(root, false, ref report);

            GameObject generatedRoot = CreateGeneratedRoot(root.transform, GeneratedConvexRootName, prefabPath, root.name);
            MeshCollider collider = generatedRoot.AddComponent<MeshCollider>();
            collider.sharedMesh = proxyMesh;
            collider.convex = true;
            collider.enabled = true;
            collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
                                      MeshColliderCookingOptions.EnableMeshCleaning |
                                      MeshColliderCookingOptions.WeldColocatedVertices;

            InjectNavMeshObstacle(generatedRoot, root, prefabPath, ref report);
            return true;
        }

        private static bool SerializeGeneratedProxyMeshes(
            GameObject root,
            string prefabPath,
            ColliderOptimizerSettings1716 settings,
            ref ColliderOptimizerReport1716 report,
            out string failure)
        {
            failure = string.Empty;
            Transform convex = root.transform.Find(GeneratedConvexRootName);
            if (convex == null)
                return true;

            convex.GetComponentsInChildren(true, s_MeshColliderScratch);
            try
            {
                if (s_MeshColliderScratch.Count != 1)
                {
                    failure = root.name + " convex proxy serialization requires exactly one MeshCollider.";
                    return false;
                }

                MeshCollider collider = s_MeshColliderScratch[0];
                if (collider == null || !collider.convex)
                {
                    failure = root.name + " convex proxy serialization target is not convex.";
                    return false;
                }

                Mesh mesh = collider.sharedMesh;
                if (!ValidateProxyMesh(mesh, out failure))
                    return false;

                string existingPath = AssetDatabase.GetAssetPath(mesh);
                if (!string.IsNullOrEmpty(existingPath))
                {
                    if (existingPath.StartsWith(ProxyAssetRoot + "/", StringComparison.Ordinal))
                        return true;

                    failure = root.name + " convex proxy references non-generated mesh asset: " + existingPath;
                    return false;
                }

                EnsureGeneratedFolders();
                string meshPath = AssetDatabase.GenerateUniqueAssetPath(ProxyAssetRoot + "/COL_" + SanitizeAssetStem(root.name) + "_1716.asset");
                AssetDatabase.CreateAsset(mesh, meshPath);
                EditorUtility.SetDirty(mesh);
                report.ProxyMeshesGenerated++;
                RecordTrace(4, prefabPath, CountMeshTrianglesNoAlloc(mesh), ContainsOrdinalIgnoreCase(mesh.name, "_Aabb1716") ? 1 : 0, settings.GlobalQualityWeight);
                return true;
            }
            finally
            {
                s_MeshColliderScratch.Clear();
            }
        }

        private static bool StripMeshColliders(GameObject root, bool onlyIllegal, ref ColliderOptimizerReport1716 report)
        {
            bool changed = false;
            root.GetComponentsInChildren(true, s_MeshColliderScratch);
            try
            {
                for (int i = s_MeshColliderScratch.Count - 1; i >= 0; i--)
                {
                    MeshCollider meshCollider = s_MeshColliderScratch[i];
                    if (meshCollider == null)
                        continue;

                    report.MeshCollidersFound++;
                    int triangles = CountMeshTrianglesNoAlloc(meshCollider.sharedMesh);
                    bool primaryVisual = IsPrimaryVisualMeshReference(root, meshCollider);
                    bool illegal = triangles > MeshColliderFatalTriangleLimit || primaryVisual || !IsGeneratedCollisionName(SafeName(meshCollider.sharedMesh));
                    if (triangles > MeshColliderFatalTriangleLimit)
                        report.HighPolyMeshColliders++;

                    if (onlyIllegal && !illegal)
                        continue;

                    report.VisualTrianglesRemovedFromPhysics += triangles;
                    if (primaryVisual)
                        report.VisualLod0MeshCollidersRemoved++;

                    Object.DestroyImmediate(meshCollider, true);
                    report.MeshCollidersDeleted++;
                    changed = true;
                }
            }
            finally
            {
                s_MeshColliderScratch.Clear();
            }

            return changed;
        }

        private static bool StripAllColliders(GameObject root, ref ColliderOptimizerReport1716 report)
        {
            bool changed = false;
            root.GetComponentsInChildren(true, s_ColliderScratch);
            try
            {
                for (int i = s_ColliderScratch.Count - 1; i >= 0; i--)
                {
                    Collider collider = s_ColliderScratch[i];
                    if (collider == null)
                        continue;

                    if (collider is MeshCollider meshCollider)
                    {
                        report.VisualTrianglesRemovedFromPhysics += CountMeshTrianglesNoAlloc(meshCollider.sharedMesh);
                        report.MeshCollidersDeleted++;
                    }

                    Object.DestroyImmediate(collider, true);
                    changed = true;
                }
            }
            finally
            {
                s_ColliderScratch.Clear();
            }

            return changed;
        }

        private static Collider CreatePrimitiveCollider(Transform parent, string sourceName, int subMesh, ColliderPrimitiveFit1716 fit, int layer, ref ColliderOptimizerReport1716 report)
        {
            GameObject child = new GameObject(BuildPrimitiveColliderName(sourceName, subMesh, fit.PrimitiveKind));
            child.layer = layer;
            Transform childTransform = child.transform;
            childTransform.SetParent(parent, false);
            childTransform.localPosition = fit.Center;
            childTransform.localRotation = fit.Rotation;
            childTransform.localScale = Vector3.one;

            if (fit.PrimitiveKind == 2)
            {
                CapsuleCollider capsule = child.AddComponent<CapsuleCollider>();
                capsule.center = Vector3.zero;
                capsule.radius = Mathf.Max(MinimumColliderAxisMeters, fit.CapsuleRadius);
                capsule.height = Mathf.Max(capsule.radius * 2f, fit.CapsuleHeight);
                capsule.direction = fit.CapsuleDirection;
                report.CapsuleCollidersGenerated++;
                return capsule;
            }

            if (fit.PrimitiveKind == 3)
            {
                SphereCollider sphere = child.AddComponent<SphereCollider>();
                sphere.center = Vector3.zero;
                sphere.radius = Mathf.Max(MinimumColliderAxisMeters, fit.SphereRadius);
                report.SphereCollidersGenerated++;
                return sphere;
            }

            BoxCollider box = child.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = ClampColliderSize(fit.Size);
            report.BoxCollidersGenerated++;
            return box;
        }

        private static string BuildPrimitiveColliderName(string sourceName, int subMesh, byte primitiveKind)
        {
            string prefix = primitiveKind == 2
                ? "COL_Capsule_"
                : primitiveKind == 3
                    ? "COL_Sphere_"
                    : "COL_Box_";
            return prefix + SanitizeAssetStem(sourceName) + "_" + subMesh.ToString("00");
        }

        private static bool TryFitPrimitive(Mesh mesh, int subMeshIndex, Matrix4x4 sourceToRoot, string sourceName, out ColliderPrimitiveFit1716 fit)
        {
            fit = default;
            if (mesh == null ||
                subMeshIndex < 0 ||
                subMeshIndex >= mesh.subMeshCount ||
                mesh.GetTopology(subMeshIndex) != MeshTopology.Triangles)
            {
                return false;
            }

            s_VertexScratch.Clear();
            s_IndexScratch.Clear();
            mesh.GetVertices(s_VertexScratch);
            mesh.GetTriangles(s_IndexScratch, subMeshIndex, true);
            if (s_VertexScratch.Count <= 0 || s_IndexScratch.Count <= 0)
                return false;

            Vector3 min = default;
            Vector3 max = default;
            bool hasPoint = false;
            for (int i = 0; i < s_IndexScratch.Count; i++)
            {
                int vertexIndex = s_IndexScratch[i];
                if ((uint)vertexIndex >= (uint)s_VertexScratch.Count)
                    continue;

                Vector3 point = sourceToRoot.MultiplyPoint3x4(s_VertexScratch[vertexIndex]);
                if (!IsFinite(point))
                    continue;

                if (!hasPoint)
                {
                    min = point;
                    max = point;
                    hasPoint = true;
                    continue;
                }

                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }

            if (!hasPoint)
                return false;

            Vector3 size = ClampColliderSize(max - min);
            fit.Center = (min + max) * 0.5f;
            fit.Rotation = Quaternion.identity;
            fit.Size = size;
            fit.PrimitiveKind = 1;

            if (ShouldUseSphere(sourceName, size, out float sphereRadius))
            {
                fit.PrimitiveKind = 3;
                fit.SphereRadius = sphereRadius;
            }
            else if (ShouldUseCapsule(sourceName, size, out int capsuleDirection, out float radius, out float height))
            {
                fit.PrimitiveKind = 2;
                fit.CapsuleDirection = capsuleDirection;
                fit.CapsuleRadius = radius;
                fit.CapsuleHeight = height;
            }

            return true;
        }

        private static bool ShouldUseSphere(string sourceName, Vector3 size, out float radius)
        {
            radius = 0f;
            float x = Mathf.Max(size.x, MinimumColliderAxisMeters);
            float y = Mathf.Max(size.y, MinimumColliderAxisMeters);
            float z = Mathf.Max(size.z, MinimumColliderAxisMeters);
            float longest = Mathf.Max(x, Mathf.Max(y, z));
            float shortest = Mathf.Min(x, Mathf.Min(y, z));
            float isotropicError = (longest - shortest) / Mathf.Max(MinimumColliderAxisMeters, longest);
            bool namedRoundOrOrganic = ContainsOrdinalIgnoreCase(sourceName, "sphere") ||
                                       ContainsOrdinalIgnoreCase(sourceName, "ball") ||
                                       ContainsOrdinalIgnoreCase(sourceName, "boulder") ||
                                       ContainsOrdinalIgnoreCase(sourceName, "bold") ||
                                       ContainsOrdinalIgnoreCase(sourceName, "rock") ||
                                       ContainsOrdinalIgnoreCase(sourceName, "stone") ||
                                       ContainsOrdinalIgnoreCase(sourceName, "kamen") ||
                                       ContainsOrdinalIgnoreCase(sourceName, "ore") ||
                                       ContainsOrdinalIgnoreCase(sourceName, "node") ||
                                       ContainsOrdinalIgnoreCase(sourceName, "coral");
            if (!namedRoundOrOrganic || isotropicError > 0.22f)
                return false;

            radius = Mathf.Max(MinimumColliderAxisMeters, longest * 0.5f);
            return true;
        }

        private static bool ShouldUseCapsule(string sourceName, Vector3 size, out int direction, out float radius, out float height)
        {
            direction = 1;
            radius = 0f;
            height = 0f;
            float x = Mathf.Max(size.x, MinimumColliderAxisMeters);
            float y = Mathf.Max(size.y, MinimumColliderAxisMeters);
            float z = Mathf.Max(size.z, MinimumColliderAxisMeters);
            float longest = x;
            direction = 0;
            if (y > longest)
            {
                longest = y;
                direction = 1;
            }

            if (z > longest)
            {
                longest = z;
                direction = 2;
            }

            float a = direction == 0 ? y : x;
            float b = direction == 2 ? y : z;
            float crossMax = Mathf.Max(a, b);
            float crossMin = Mathf.Min(a, b);
            bool namedTube = ContainsOrdinalIgnoreCase(sourceName, "pipe") ||
                             ContainsOrdinalIgnoreCase(sourceName, "tube") ||
                             ContainsOrdinalIgnoreCase(sourceName, "hose") ||
                             ContainsOrdinalIgnoreCase(sourceName, "cable") ||
                             ContainsOrdinalIgnoreCase(sourceName, "kelp") ||
                             ContainsOrdinalIgnoreCase(sourceName, "vine") ||
                             ContainsOrdinalIgnoreCase(sourceName, "frond") ||
                             ContainsOrdinalIgnoreCase(sourceName, "stem") ||
                             ContainsOrdinalIgnoreCase(sourceName, "tendril");
            bool cylindrical = longest / Mathf.Max(MinimumColliderAxisMeters, crossMax) >= CapsuleAspectThreshold &&
                               (crossMax - crossMin) / Mathf.Max(MinimumColliderAxisMeters, crossMax) <= CapsuleCircularityTolerance;
            if (!namedTube && !cylindrical)
                return false;

            radius = Mathf.Max(MinimumColliderAxisMeters, crossMax * 0.5f);
            height = Mathf.Max(radius * 2f, longest);
            return true;
        }

        private static Mesh BuildConvexProxyMesh(GameObject root, ColliderOptimizerSettings1716 settings, out bool usedAabbFallback)
        {
            usedAabbFallback = false;
            if (!TryCollectRootVertices(root, out Bounds rootBounds))
                return null;

            int sourceCount = s_VertexScratch.Count;
            int directionCount = Mathf.Clamp(settings.HullSupportDirectionCount, MinHullSupportDirections, MaxHullSupportDirections);
            NativeArray<float3> vertices = new NativeArray<float3>(sourceCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<float3> directions = new NativeArray<float3>(directionCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<float3> supportPoints = new NativeArray<float3>(directionCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (int i = 0; i < sourceCount; i++)
                {
                    Vector3 v = s_VertexScratch[i];
                    vertices[i] = new float3(v.x, v.y, v.z);
                }

                FillSupportDirections(directions);
                HullSupportPointJob1716 job = new HullSupportPointJob1716
                {
                    Vertices = vertices,
                    Directions = directions,
                    SupportPoints = supportPoints
                };
                job.Schedule(directionCount, 4).Complete();

                s_HullPointScratch.Clear();
                Vector3 center = rootBounds.center;
                float padding = Mathf.Clamp(settings.ProxyPaddingMeters, MinProxyPaddingMeters, MaxProxyPaddingMeters);
                for (int i = 0; i < supportPoints.Length; i++)
                {
                    float3 p = supportPoints[i];
                    Vector3 point = new Vector3(p.x, p.y, p.z);
                    Vector3 outward = point - center;
                    if (outward.sqrMagnitude > 0.000001f)
                        point += outward.normalized * padding;

                    AddUniquePoint(s_HullPointScratch, point);
                }

                if (s_HullPointScratch.Count >= 8 &&
                    TryBuildSupportHullTriangles(s_HullPointScratch, s_HullTriangleScratch) &&
                    HullContainsSourceVertices(s_HullPointScratch, s_HullTriangleScratch, s_VertexScratch, padding + 0.004f))
                {
                    Mesh hull = new Mesh { name = "COL_" + SanitizeAssetStem(root.name) + "_Hull1716" };
                    hull.SetVertices(s_HullPointScratch);
                    hull.SetTriangles(s_HullTriangleScratch, 0, false);
                    hull.RecalculateNormals();
                    hull.RecalculateBounds();
                    if (CountMeshTrianglesNoAlloc(hull) <= ProxyMeshTriangleLimit && BoundsContains(rootBounds, hull.bounds, padding + 0.002f))
                        return hull;

                    Object.DestroyImmediate(hull);
                }
            }
            finally
            {
                if (supportPoints.IsCreated)
                    supportPoints.Dispose();
                if (directions.IsCreated)
                    directions.Dispose();
                if (vertices.IsCreated)
                    vertices.Dispose();
                s_VertexScratch.Clear();
            }

            usedAabbFallback = true;
            return BuildAabbProxyMesh(root.name, ExpandBounds(rootBounds, Mathf.Clamp(settings.ProxyPaddingMeters, MinProxyPaddingMeters, MaxProxyPaddingMeters)));
        }

        private static bool TryBuildSupportHullTriangles(List<Vector3> points, List<int> triangles)
        {
            triangles.Clear();
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
                centroid += points[i];
            centroid /= points.Count;

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    for (int k = j + 1; k < points.Count; k++)
                    {
                        Vector3 a = points[i];
                        Vector3 b = points[j];
                        Vector3 c = points[k];
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        float magnitude = normal.magnitude;
                        if (magnitude <= 0.00001f)
                            continue;

                        normal /= magnitude;
                        bool positive = false;
                        bool negative = false;
                        for (int p = 0; p < points.Count; p++)
                        {
                            if (p == i || p == j || p == k)
                                continue;

                            float side = Vector3.Dot(normal, points[p] - a);
                            if (side > 0.002f)
                                positive = true;
                            else if (side < -0.002f)
                                negative = true;

                            if (positive && negative)
                                break;
                        }

                        if (positive && negative)
                            continue;

                        if (triangles.Count / 3 >= ProxyMeshTriangleLimit)
                            return false;

                        if (Vector3.Dot(normal, centroid - a) > 0f)
                        {
                            triangles.Add(i);
                            triangles.Add(k);
                            triangles.Add(j);
                        }
                        else
                        {
                            triangles.Add(i);
                            triangles.Add(j);
                            triangles.Add(k);
                        }
                    }
                }
            }

            return triangles.Count >= 12;
        }

        private static bool HullContainsSourceVertices(List<Vector3> hullPoints, List<int> hullTriangles, List<Vector3> sourceVertices, float epsilon)
        {
            if (hullPoints == null || hullTriangles == null || sourceVertices == null)
                return false;

            if (hullPoints.Count < 4 || hullTriangles.Count < 12)
                return false;

            for (int t = 0; t + 2 < hullTriangles.Count; t += 3)
            {
                int ia = hullTriangles[t];
                int ib = hullTriangles[t + 1];
                int ic = hullTriangles[t + 2];
                if ((uint)ia >= (uint)hullPoints.Count ||
                    (uint)ib >= (uint)hullPoints.Count ||
                    (uint)ic >= (uint)hullPoints.Count)
                {
                    return false;
                }

                Vector3 a = hullPoints[ia];
                Vector3 b = hullPoints[ib];
                Vector3 c = hullPoints[ic];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                float magnitude = normal.magnitude;
                if (magnitude <= 0.00001f)
                    return false;

                normal /= magnitude;
                for (int p = 0; p < sourceVertices.Count; p++)
                {
                    Vector3 source = sourceVertices[p];
                    if (!IsFinite(source))
                        continue;

                    if (Vector3.Dot(normal, source - a) > epsilon)
                        return false;
                }
            }

            return true;
        }

        private static void FillSupportDirections(NativeArray<float3> directions)
        {
            int index = 0;
            AddDirection(directions, ref index, 1f, 0f, 0f);
            AddDirection(directions, ref index, -1f, 0f, 0f);
            AddDirection(directions, ref index, 0f, 1f, 0f);
            AddDirection(directions, ref index, 0f, -1f, 0f);
            AddDirection(directions, ref index, 0f, 0f, 1f);
            AddDirection(directions, ref index, 0f, 0f, -1f);

            for (int sx = -1; sx <= 1 && index < directions.Length; sx += 2)
            {
                for (int sy = -1; sy <= 1 && index < directions.Length; sy += 2)
                {
                    for (int sz = -1; sz <= 1 && index < directions.Length; sz += 2)
                        AddDirection(directions, ref index, sx, sy, sz);
                }
            }

            for (int axis = 0; axis < 3 && index < directions.Length; axis++)
            {
                for (int sx = -1; sx <= 1 && index < directions.Length; sx += 2)
                {
                    for (int sy = -1; sy <= 1 && index < directions.Length; sy += 2)
                    {
                        if (axis == 0)
                            AddDirection(directions, ref index, 0f, sx, sy);
                        else if (axis == 1)
                            AddDirection(directions, ref index, sx, 0f, sy);
                        else
                            AddDirection(directions, ref index, sx, sy, 0f);
                    }
                }
            }

            while (index < directions.Length)
            {
                float t = (index + 1) * 2.3999632f;
                float z = 1f - 2f * ((index + 0.5f) / directions.Length);
                float radius = math.sqrt(math.max(0f, 1f - z * z));
                AddDirection(directions, ref index, math.cos(t) * radius, z, math.sin(t) * radius);
            }
        }

        private static void AddDirection(NativeArray<float3> directions, ref int index, float x, float y, float z)
        {
            if (index >= directions.Length)
                return;

            float3 value = new float3(x, y, z);
            float length = math.length(value);
            directions[index++] = length > 0.00001f ? value / length : new float3(0f, 1f, 0f);
        }

        private static bool TryCollectRootVertices(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool hasPoint = false;
            s_VertexScratch.Clear();
            root.GetComponentsInChildren(true, s_MeshFilterScratch);
            try
            {
                for (int f = 0; f < s_MeshFilterScratch.Count; f++)
                {
                    MeshFilter filter = s_MeshFilterScratch[f];
                    if (filter == null || !IsPrimaryCollisionVisual(filter))
                        continue;

                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null || mesh.vertexCount <= 0)
                        continue;

                    Matrix4x4 sourceToRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    s_SourceVertexScratch.Clear();
                    mesh.GetVertices(s_SourceVertexScratch);
                    for (int i = 0; i < s_SourceVertexScratch.Count; i++)
                    {
                        Vector3 point = sourceToRoot.MultiplyPoint3x4(s_SourceVertexScratch[i]);
                        if (!IsFinite(point))
                            continue;

                        s_VertexScratch.Add(point);
                        if (!hasPoint)
                        {
                            bounds = new Bounds(point, Vector3.zero);
                            hasPoint = true;
                        }
                        else
                        {
                            bounds.Encapsulate(point);
                        }
                    }
                }
            }
            finally
            {
                s_MeshFilterScratch.Clear();
                s_SourceVertexScratch.Clear();
            }

            return hasPoint;
        }

        private static bool TryCollectRootVisualBounds(GameObject root, float paddingMeters, out Bounds bounds)
        {
            if (!TryCollectRootVertices(root, out bounds))
                return false;

            bounds = ExpandBounds(bounds, paddingMeters);
            s_VertexScratch.Clear();
            return true;
        }

        private static Mesh BuildAabbProxyMesh(string sourceName, Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] vertices =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z)
            };

            Mesh mesh = new Mesh { name = "COL_" + SanitizeAssetStem(sourceName) + "_Aabb1716" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(s_BoxHullTriangles, 0, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void InjectNavMeshObstacle(GameObject generatedRoot, GameObject root, string prefabPath, ref ColliderOptimizerReport1716 report)
        {
            if (IsFloraPath(prefabPath) || IsDebrisPath(prefabPath, root.name))
                return;

            if (!TryCollectGeneratedColliderBounds(generatedRoot, out Bounds bounds))
                return;

            Vector3 size = bounds.size;
            if (size.x < 1f && size.z < 1f)
                return;

            NavMeshObstacle obstacle = generatedRoot.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = generatedRoot.AddComponent<NavMeshObstacle>();
                report.NavMeshObstaclesInjected++;
            }

            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = bounds.center - generatedRoot.transform.localPosition;
            obstacle.size = ClampColliderSize(size);
            obstacle.carving = true;
        }

        private static bool TryCollectGeneratedColliderBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            root.GetComponentsInChildren(true, s_ColliderScratch);
            try
            {
                for (int i = 0; i < s_ColliderScratch.Count; i++)
                {
                    Collider collider = s_ColliderScratch[i];
                    if (collider == null)
                        continue;

                    Bounds local = new Bounds(root.transform.InverseTransformPoint(collider.bounds.center), collider.bounds.size);
                    if (!hasBounds)
                    {
                        bounds = local;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }
            finally
            {
                s_ColliderScratch.Clear();
            }

            return hasBounds;
        }

        private static bool EnforceLayersAndMaterials(GameObject root, string prefabPath, ref ColliderOptimizerReport1716 report)
        {
            bool changed = false;
            int layer = ResolvePhysicsLayer(prefabPath, root.name, root.layer);
            UnityEngine.PhysicsMaterial material = ResolvePhysicsMaterial(prefabPath, root.name);
            bool useTriggerContact = IsFloraAsset(prefabPath, root.name);
            root.GetComponentsInChildren(true, s_ColliderScratch);
            try
            {
                for (int i = 0; i < s_ColliderScratch.Count; i++)
                {
                    Collider collider = s_ColliderScratch[i];
                    if (collider == null)
                        continue;

                    if (collider.gameObject.layer != layer)
                    {
                        collider.gameObject.layer = layer;
                        report.LayerAssignments++;
                        changed = true;
                    }

                    if (useTriggerContact && !collider.isTrigger)
                    {
                        collider.isTrigger = true;
                        changed = true;
                    }

                    if (material != null && collider.sharedMaterial != material)
                    {
                        collider.sharedMaterial = material;
                        report.MaterialAssignments++;
                        changed = true;
                    }
                }
            }
            finally
            {
                s_ColliderScratch.Clear();
            }

            return changed;
        }

        private static GameObject CreateGeneratedRoot(Transform rootTransform, string rootName, string prefabPath, string sourceName)
        {
            Transform existing = rootTransform.Find(rootName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject, true);

            GameObject generatedRoot = new GameObject(rootName);
            generatedRoot.layer = ResolvePhysicsLayer(prefabPath, sourceName, rootTransform.gameObject.layer);
            Transform generatedTransform = generatedRoot.transform;
            generatedTransform.SetParent(rootTransform, false);
            generatedTransform.localPosition = Vector3.zero;
            generatedTransform.localRotation = Quaternion.identity;
            generatedTransform.localScale = Vector3.one;
            return generatedRoot;
        }

        private static void RemoveGeneratedRoots(Transform rootTransform, ref ColliderOptimizerReport1716 report)
        {
            RemoveGeneratedRoot(rootTransform, GeneratedCompoundRootName);
            RemoveGeneratedRoot(rootTransform, GeneratedConvexRootName);
            DeleteGeneratedProxyAssetsForRoot(rootTransform.name, ref report);
        }

        private static void RemoveGeneratedRoot(Transform rootTransform, string rootName)
        {
            Transform existing = rootTransform.Find(rootName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject, true);
        }

        private static void DeleteGeneratedProxyAssetsForRoot(string rootName, ref ColliderOptimizerReport1716 report)
        {
            if (!AssetDatabase.IsValidFolder(ProxyAssetRoot))
                return;

            string stem = SanitizeAssetStem(rootName);
            string[] guids = AssetDatabase.FindAssets("COL_" + stem + "_1716 t:Mesh", new[] { ProxyAssetRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetDatabase.DeleteAsset(assetPath))
                    report.ProxyMeshesDeleted++;
            }
        }

        private static ColliderOptimizerMode1716 ResolveMode(GameObject root, string prefabPath, ColliderOptimizerMode1716 requestedMode)
        {
            if (requestedMode != ColliderOptimizerMode1716.Auto)
                return requestedMode;

            if (IsFloraAsset(prefabPath, root.name))
                return ColliderOptimizerMode1716.CompoundPrimitives;

            if (IsOrganicPath(prefabPath) || ContainsOrdinalIgnoreCase(root.name, "Rock") || ContainsOrdinalIgnoreCase(root.name, "Skala") || ContainsOrdinalIgnoreCase(root.name, "Kamen"))
                return ColliderOptimizerMode1716.ConvexProxy;

            return ColliderOptimizerMode1716.CompoundPrimitives;
        }

        private static bool IsPrimaryCollisionVisual(MeshFilter meshFilter)
        {
            if (meshFilter == null)
                return false;

            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null || IsGeneratedCollisionName(mesh.name) || IsGeneratedCollisionName(meshFilter.name))
                return false;

            LODGroup lodGroup = meshFilter.GetComponentInParent<LODGroup>();
            if (lodGroup == null)
                return true;

            Renderer renderer = meshFilter.GetComponent<Renderer>();
            if (renderer == null)
                return false;

            LOD[] lods = lodGroup.GetLODs();
            if (lods == null || lods.Length <= 0)
                return true;

            Renderer[] renderers = lods[0].renderers;
            if (renderers == null || renderers.Length <= 0)
                return true;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == renderer)
                    return true;
            }

            return false;
        }

        private static bool IsPrimaryVisualMeshCollider(MeshCollider collider)
        {
            if (collider == null)
                return false;

            MeshFilter filter = collider.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh == collider.sharedMesh && IsPrimaryCollisionVisual(filter);
        }

        private static bool IsPrimaryVisualMeshReference(GameObject root, MeshCollider collider)
        {
            if (collider == null || collider.sharedMesh == null)
                return false;

            if (IsPrimaryVisualMeshCollider(collider))
                return true;

            if (root == null)
                return false;

            Mesh colliderMesh = collider.sharedMesh;
            root.GetComponentsInChildren(true, s_MeshFilterScratch);
            try
            {
                for (int i = 0; i < s_MeshFilterScratch.Count; i++)
                {
                    MeshFilter filter = s_MeshFilterScratch[i];
                    if (filter == null || filter.sharedMesh != colliderMesh)
                        continue;

                    if (IsPrimaryCollisionVisual(filter))
                        return true;
                }
            }
            finally
            {
                s_MeshFilterScratch.Clear();
            }

            return false;
        }

        private static int CountMeshTrianglesNoAlloc(Mesh mesh)
        {
            if (mesh == null)
                return 0;

            long triangles = 0L;
            int subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < subMeshCount; i++)
                triangles += (long)mesh.GetIndexCount(i) / 3L;

            return triangles > int.MaxValue ? int.MaxValue : (int)triangles;
        }

        private static ColliderOptimizerSettings1716 NormalizeSettings(ColliderOptimizerSettings1716 settings)
        {
            ColliderOptimizerSettings1716 defaults = ColliderOptimizerSettings1716.FromGlobalQualityWeight(settings.GlobalQualityWeight);
            settings.GlobalQualityWeight = defaults.GlobalQualityWeight;
            if (settings.MaxPrimitiveCollidersPerPrefab <= 0)
                settings.MaxPrimitiveCollidersPerPrefab = defaults.MaxPrimitiveCollidersPerPrefab;
            settings.MaxPrimitiveCollidersPerPrefab = Mathf.Clamp(settings.MaxPrimitiveCollidersPerPrefab, MinPrimitiveCollidersPerPrefab, MaxPrimitiveCollidersPerPrefab);

            if (settings.HullSupportDirectionCount <= 0)
                settings.HullSupportDirectionCount = defaults.HullSupportDirectionCount;
            settings.HullSupportDirectionCount = Mathf.Clamp(settings.HullSupportDirectionCount, MinHullSupportDirections, MaxHullSupportDirections);

            if (!IsFinite(settings.ProxyPaddingMeters) || settings.ProxyPaddingMeters <= 0f)
                settings.ProxyPaddingMeters = defaults.ProxyPaddingMeters;
            settings.ProxyPaddingMeters = Mathf.Clamp(settings.ProxyPaddingMeters, MinProxyPaddingMeters, MaxProxyPaddingMeters);

            if (!IsFinite(settings.LowTierCullDistanceMeters) || settings.LowTierCullDistanceMeters <= 0f)
                settings.LowTierCullDistanceMeters = defaults.LowTierCullDistanceMeters;
            if (!IsFinite(settings.UltraTierCullDistanceMeters) || settings.UltraTierCullDistanceMeters <= settings.LowTierCullDistanceMeters)
                settings.UltraTierCullDistanceMeters = defaults.UltraTierCullDistanceMeters;
            return settings;
        }

        private static string[] FindPrefabPaths(string folder)
        {
            string safeFolder = string.IsNullOrEmpty(folder) ? PrefabRoot : folder;
            if (!AssetDatabase.IsValidFolder(safeFolder))
                return Array.Empty<string>();

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { safeFolder });
            string[] paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            return paths;
        }

        private static void AuditRuntimeCookingCallsites(ref ColliderOptimizerReport1716 report)
        {
            string projectRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Scripts");
            if (!Directory.Exists(projectRoot))
                return;

            string[] files = Directory.GetFiles(projectRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string normalized = files[i].Replace('\\', '/');
                if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string[] lines = File.ReadAllLines(files[i]);
                for (int line = 0; line < lines.Length; line++)
                {
                    string sourceLine = lines[line];
                    if (sourceLine.IndexOf("Physics." + "BakeMesh", StringComparison.Ordinal) >= 0 ||
                        sourceLine.IndexOf("UnityEngine.Physics." + "BakeMesh", StringComparison.Ordinal) >= 0)
                    {
                        report.RuntimeBakeCallsitesFound++;
                    }

                    if (IsRuntimeMeshColliderCommitLine(sourceLine))
                        report.RuntimeSharedMeshAssignmentsFound++;
                }
            }
        }

        private static bool IsRuntimeMeshColliderCommitLine(string sourceLine)
        {
            if (string.IsNullOrEmpty(sourceLine))
                return false;

            int commentIndex = sourceLine.IndexOf("//", StringComparison.Ordinal);
            if (commentIndex >= 0)
                sourceLine = sourceLine.Substring(0, commentIndex);

            int sharedMeshIndex = sourceLine.IndexOf(".sharedMesh", StringComparison.Ordinal);
            int assignmentIndex = FindAssignmentOperatorIndex(sourceLine);
            if (sharedMeshIndex < 0 || assignmentIndex <= sharedMeshIndex)
                return false;

            string rhs = sourceLine.Substring(assignmentIndex + 1).Trim();
            if (rhs.StartsWith("null", StringComparison.Ordinal))
                return false;

            string lhs = sourceLine.Substring(0, assignmentIndex).Trim();
            bool visualFilterTarget =
                lhs.IndexOf("filter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                lhs.IndexOf("renderer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                lhs.EndsWith("mf.sharedMesh", StringComparison.OrdinalIgnoreCase);
            if (visualFilterTarget)
                return false;

            bool colliderTarget =
                lhs.IndexOf("collider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                lhs.EndsWith("col.sharedMesh", StringComparison.OrdinalIgnoreCase) ||
                lhs.EndsWith("mc.sharedMesh", StringComparison.OrdinalIgnoreCase) ||
                lhs.EndsWith("meshCol.sharedMesh", StringComparison.OrdinalIgnoreCase);
            return colliderTarget && !visualFilterTarget;
        }

        private static int FindAssignmentOperatorIndex(string sourceLine)
        {
            if (string.IsNullOrEmpty(sourceLine))
                return -1;

            for (int i = 0; i < sourceLine.Length; i++)
            {
                if (sourceLine[i] != '=')
                    continue;

                char previous = i > 0 ? sourceLine[i - 1] : '\0';
                char next = i + 1 < sourceLine.Length ? sourceLine[i + 1] : '\0';
                if (previous == '=' ||
                    previous == '!' ||
                    previous == '<' ||
                    previous == '>' ||
                    next == '=' ||
                    next == '>')
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        private static int ResolvePhysicsLayer(string path, string name, int fallback)
        {
            if (IsFloraAsset(path, name))
                return ResolveLayerIndex("Flora_NonColliding", ResolveLayerIndex("Flora", fallback));
            if (IsDebrisPath(path, name))
                return ResolveLayerIndex("Debris", fallback);
            if (ContainsOrdinalIgnoreCase(path, "Module") || ContainsOrdinalIgnoreCase(name, "Module") || ContainsOrdinalIgnoreCase(path, "Base"))
                return ResolveLayerIndex("World_Static", ResolveLayerIndex("BaseModule", fallback));
            if (IsOrganicPath(path))
                return ResolveLayerIndex("World_Static", ResolveLayerIndex("Terrain", fallback));
            return ResolveLayerIndex("World_Static", fallback);
        }

        private static int ResolveLayerIndex(string logicalName, int fallback)
        {
            string[] layers = InternalEditorUtility.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                string layer = layers[i];
                if (string.Equals(layer, logicalName, StringComparison.Ordinal) ||
                    string.Equals(layer != null ? layer.Trim() : string.Empty, logicalName, StringComparison.Ordinal))
                {
                    int index = LayerMask.NameToLayer(layer);
                    return index >= 0 ? index : fallback;
                }
            }

            return fallback;
        }

        private static UnityEngine.PhysicsMaterial ResolvePhysicsMaterial(string path, string name)
        {
            EnsureGeneratedFolders();
            if (IsDebrisPath(path, name))
                return EnsurePhysicsMaterial("MAT_Physics_Debris_1716", 0.55f, 0.65f, 0.02f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Minimum);
            if (ContainsOrdinalIgnoreCase(path, "Kelp") || ContainsOrdinalIgnoreCase(name, "Kelp"))
                return EnsurePhysicsMaterial("MAT_Physics_Slippery_Kelp_1716", 0.08f, 0.12f, 0f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Minimum);
            if (IsFloraAsset(path, name))
                return EnsurePhysicsMaterial("MAT_Physics_Flora_Trigger_1716", 0.18f, 0.24f, 0f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Minimum);
            if (ContainsOrdinalIgnoreCase(path, "Metal") || ContainsOrdinalIgnoreCase(path, "Module") || ContainsOrdinalIgnoreCase(name, "Module") || ContainsOrdinalIgnoreCase(path, "Base"))
                return EnsurePhysicsMaterial("MAT_Physics_Steel_Heavy_Friction_1716", 0.82f, 0.95f, 0f, PhysicsMaterialCombine.Maximum, PhysicsMaterialCombine.Minimum);
            if (IsOrganicPath(path))
                return EnsurePhysicsMaterial("MAT_Physics_Sediment_Rock_1716", 0.48f, 0.62f, 0f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Minimum);
            return EnsurePhysicsMaterial("MAT_Physics_World_Static_1716", 0.45f, 0.55f, 0f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Minimum);
        }

        private static UnityEngine.PhysicsMaterial EnsurePhysicsMaterial(
            string name,
            float dynamicFriction,
            float staticFriction,
            float bounciness,
            PhysicsMaterialCombine frictionCombine,
            PhysicsMaterialCombine bounceCombine)
        {
            string path = MaterialAssetRoot + "/" + name + ".asset";
            UnityEngine.PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<UnityEngine.PhysicsMaterial>(path);
            if (material == null)
            {
                material = new UnityEngine.PhysicsMaterial(name);
                AssetDatabase.CreateAsset(material, path);
            }

            material.dynamicFriction = dynamicFriction;
            material.staticFriction = staticFriction;
            material.bounciness = bounciness;
            material.frictionCombine = frictionCombine;
            material.bounceCombine = bounceCombine;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static bool IsFloraAsset(string path, string name)
        {
            if (ContainsOrdinalIgnoreCase(path, "Coral") ||
                ContainsOrdinalIgnoreCase(name, "Coral") ||
                ContainsOrdinalIgnoreCase(path, "family_coral") ||
                ContainsOrdinalIgnoreCase(name, "family_coral") ||
                ContainsOrdinalIgnoreCase(path, "/PorousRock/") ||
                ContainsOrdinalIgnoreCase(path, "_Rock_") ||
                ContainsOrdinalIgnoreCase(name, "Rock") ||
                ContainsOrdinalIgnoreCase(name, "Skala") ||
                ContainsOrdinalIgnoreCase(name, "Kamen"))
            {
                return false;
            }

            return IsFloraPath(path) || IsFloraToken(name);
        }

        private static bool IsFloraPath(string path)
        {
            if (ContainsOrdinalIgnoreCase(path, "/PorousRock/") ||
                ContainsOrdinalIgnoreCase(path, "_Rock_") ||
                ContainsOrdinalIgnoreCase(path, "Coral") ||
                ContainsOrdinalIgnoreCase(path, "family_coral"))
            {
                return false;
            }

            return ContainsOrdinalIgnoreCase(path, "/Flora/") ||
                   IsFloraToken(path);
        }

        private static bool IsFloraToken(string value)
        {
            return ContainsOrdinalIgnoreCase(value, "kelp") ||
                   ContainsOrdinalIgnoreCase(value, "grass") ||
                   ContainsOrdinalIgnoreCase(value, "seaweed") ||
                   ContainsOrdinalIgnoreCase(value, "sargassum") ||
                   ContainsOrdinalIgnoreCase(value, "algae");
        }

        private static bool IsOrganicPath(string path)
        {
            return ContainsOrdinalIgnoreCase(path, "Rock") ||
                   ContainsOrdinalIgnoreCase(path, "Coral") ||
                   ContainsOrdinalIgnoreCase(path, "family_coral") ||
                   ContainsOrdinalIgnoreCase(path, "Geo") ||
                   ContainsOrdinalIgnoreCase(path, "Geology") ||
                   ContainsOrdinalIgnoreCase(path, "GOTOVYE_PREFABY_KAMNEY") ||
                   ContainsOrdinalIgnoreCase(path, "Kamen") ||
                   ContainsOrdinalIgnoreCase(path, "Skala") ||
                   ContainsOrdinalIgnoreCase(path, "Cave");
        }

        private static bool IsDebrisPath(string path, string name)
        {
            return ContainsOrdinalIgnoreCase(path, "Debris") || ContainsOrdinalIgnoreCase(name, "Debris");
        }

        private static bool IsGeneratedCollisionName(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   (value.StartsWith("COL_", StringComparison.OrdinalIgnoreCase) ||
                    value.IndexOf("_COL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("CollisionProxy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("PhysicsProxy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("ColliderProxy", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void EnsureGeneratedFolders()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder(ProxyAssetRoot);
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/Generated");
            EnsureFolder("Assets/_Project/Data/Generated/ColliderOptimizer1716");
            EnsureFolder(MaterialAssetRoot);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            if (slash <= 0)
                return;

            string parent = path.Substring(0, slash);
            string child = path.Substring(slash + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static Vector3 ClampColliderSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(MinimumColliderAxisMeters, Mathf.Abs(size.x)),
                Mathf.Max(MinimumColliderAxisMeters, Mathf.Abs(size.y)),
                Mathf.Max(MinimumColliderAxisMeters, Mathf.Abs(size.z)));
        }

        private static Bounds ExpandBounds(Bounds bounds, float paddingMeters)
        {
            float padding = Mathf.Clamp(paddingMeters, MinProxyPaddingMeters, MaxProxyPaddingMeters);
            bounds.Expand(new Vector3(padding * 2f, padding * 2f, padding * 2f));
            return bounds;
        }

        private static bool BoundsContains(Bounds inner, Bounds outer, float epsilon)
        {
            Vector3 innerMin = inner.min;
            Vector3 innerMax = inner.max;
            Vector3 outerMin = outer.min;
            Vector3 outerMax = outer.max;
            return innerMin.x >= outerMin.x - epsilon && innerMax.x <= outerMax.x + epsilon &&
                   innerMin.y >= outerMin.y - epsilon && innerMax.y <= outerMax.y + epsilon &&
                   innerMin.z >= outerMin.z - epsilon && innerMax.z <= outerMax.z + epsilon;
        }

        private static void AddUniquePoint(List<Vector3> points, Vector3 point)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if ((points[i] - point).sqrMagnitude <= 0.00001f)
                    return;
            }

            points.Add(point);
        }

        private static string SanitizeAssetStem(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "ColliderProxy";

            char[] chars = value.ToCharArray();
            bool hasStableCharacter = false;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool valid = (c >= 'a' && c <= 'z') ||
                             (c >= 'A' && c <= 'Z') ||
                             (c >= '0' && c <= '9') ||
                             c == '_' ||
                             c == '-';
                if (!valid)
                    chars[i] = '_';
                else
                    hasStableCharacter = true;
            }

            return hasStableCharacter ? new string(chars) : "ColliderProxy";
        }

        private static string SafeName(Object asset)
        {
            return asset != null ? asset.name : string.Empty;
        }

        private static bool ContainsOrdinalIgnoreCase(string haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack) &&
                   !string.IsNullOrEmpty(needle) &&
                   haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void EnsureTelemetry()
        {
            if (s_TelemetryAllocated)
                return;

            s_TelemetryRing = new NativeArray<OptimizerTelemetryEntry1716>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            s_TelemetryCursor = 0;
            s_TelemetryAllocated = true;
        }

        private static void RecordTrace(int stage, string path, int a, int b, float c)
        {
            EnsureTelemetry();
            int index = s_TelemetryCursor % TelemetryCapacity;
            s_TelemetryRing[index] = new OptimizerTelemetryEntry1716
            {
                Stage = stage,
                PathHash = StableHash(path),
                A = a,
                B = b,
                C = c,
                Pad = 0
            };
            s_TelemetryCursor++;
        }

        private static int StableHash(string value)
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

                return (int)hash;
            }
        }

        private static void DisposeTelemetry()
        {
            if (!s_TelemetryAllocated)
                return;

            if (s_TelemetryRing.IsCreated)
                s_TelemetryRing.Dispose();
            s_TelemetryAllocated = false;
            s_TelemetryCursor = 0;
        }

        private static long StopwatchTicksToMicroseconds(long ticks)
        {
            return (long)(ticks * (1000000.0 / Stopwatch.Frequency));
        }
    }
}
