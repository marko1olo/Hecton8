using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hecton8.Physics;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Editor.ColliderOptimization1609
{
    public enum ColliderOptimizationStrategy1609
    {
        AggressivePrimitives = 0,
        ConvexHullWrapper = 1,
        PurgeAll = 2
    }

    public struct ColliderOptimizationReport1609
    {
        public int PrefabsVisited;
        public int PrefabsModified;
        public int PrefabsFailed;
        public int MeshCollidersFound;
        public int HighPolyMeshColliders;
        public int MeshCollidersDeleted;
        public int PrimitiveCollidersGenerated;
        public int ProxyMeshesGenerated;
        public int ProxyMeshesDeleted;
        public int FloraCollidersDeleted;
        public int RigidbodiesTuned;
        public int CcdStripped;
        public int LayerAssignments;
        public int MaterialAssignments;
        public int VisualTrianglesRemovedFromPhysics;
        public long ExecutionMilliseconds;
        public float GlobalQualityWeight;
        public bool TechnieAvailable;
    }

    public struct ColliderOptimizationSettings1609
    {
        public float GlobalQualityWeight;
        public int MaxPrimitiveCollidersPerPrefab;
        public float ProxyPaddingMeters;

        public static ColliderOptimizationSettings1609 FromGlobalQualityWeight(float globalQualityWeight)
        {
            float quality = float.IsNaN(globalQualityWeight) || float.IsInfinity(globalQualityWeight)
                ? ColliderOptimizationEngine1609.DefaultGlobalQualityWeight
                : Mathf.Clamp01(globalQualityWeight);
            return new ColliderOptimizationSettings1609
            {
                GlobalQualityWeight = quality,
                MaxPrimitiveCollidersPerPrefab = Mathf.RoundToInt(Mathf.Lerp(
                    ColliderOptimizationEngine1609.MinPrimitiveCollidersPerPrefab,
                    ColliderOptimizationEngine1609.MaxPrimitiveCollidersPerPrefab,
                    quality)),
                ProxyPaddingMeters = Mathf.Lerp(
                    ColliderOptimizationEngine1609.MaxProxyPaddingMeters,
                    ColliderOptimizationEngine1609.MinProxyPaddingMeters,
                    quality)
            };
        }
    }

    internal struct ColliderPrimitiveFit1609
    {
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector3 Size;
        public float SphereRadius;
        public float CapsuleRadius;
        public float CapsuleHeight;
        public int CapsuleDirection;
        public byte PrimitiveKind;
    }

    internal struct SymmetricCovariance1609
    {
        public float XX;
        public float XY;
        public float XZ;
        public float YY;
        public float YZ;
        public float ZZ;

        public Vector3 Multiply(Vector3 value)
        {
            return new Vector3(
                XX * value.x + XY * value.y + XZ * value.z,
                XY * value.x + YY * value.y + YZ * value.z,
                XZ * value.x + YZ * value.y + ZZ * value.z);
        }
    }

    public static class ColliderOptimizationEngine1609
    {
        public const int MeshColliderFatalTriangleLimit = 500;
        public const int ProxyMeshTriangleLimit = 200;
        public const string PrefabRoot = "Assets/_Project/Prefabs";
        public const string FloraRoot = "Assets/_Project/Prefabs/Nature/Flora";
        public const string FloraPurgeScopeLabel = "Assets/_Project/Prefabs (flora-like only)";
        public const string GeneratedAssetRoot = "Assets/_Project/Data/Generated/ColliderOptimization1609";
        public const float DefaultGlobalQualityWeight = 0.5f;
        public const int MinPrimitiveCollidersPerPrefab = 8;
        public const int MaxPrimitiveCollidersPerPrefab = 96;
        public const float MinProxyPaddingMeters = 0.015f;
        public const float MaxProxyPaddingMeters = 0.08f;

        private const string LegacyCompoundRootName = "__CompoundCollider_1609";
        private const string GeneratedCompoundRootName = "COL_CompoundProxy_1716";
        private const string GeneratedConvexRootName = "COL_ConvexProxy_1716";
        private const float MinimumColliderAxisMeters = 0.025f;
        private const float SphereAxisTolerance = 0.18f;
        private const float CapsuleAspectThreshold = 2.25f;
        private const float CapsuleCircularityTolerance = 0.35f;
        private const int PowerIterationCount = 12;
        private const string TechnieTypeName = "Technie.PhysicsCreator.HullPainter";
        private const int MeshColliderScratchCapacity = 256;
        private const int ColliderScratchCapacity = 512;
        private const int MeshFilterScratchCapacity = 512;
        private const int RigidbodyScratchCapacity = 256;
        private const int VertexScratchCapacity = 65536;
        private const int IndexScratchCapacity = 131072;

        // COLD ALLOC: List<MeshCollider>[256] - editor prefab collider scan scratch - owner: ColliderOptimizationEngine1609
        private static readonly List<MeshCollider> s_MeshColliderScratch = new List<MeshCollider>(MeshColliderScratchCapacity);
        // COLD ALLOC: List<Collider>[512] - editor prefab collider mutation scratch - owner: ColliderOptimizationEngine1609
        private static readonly List<Collider> s_ColliderScratch = new List<Collider>(ColliderScratchCapacity);
        // COLD ALLOC: List<MeshFilter>[512] - editor mesh source scratch - owner: ColliderOptimizationEngine1609
        private static readonly List<MeshFilter> s_MeshFilterScratch = new List<MeshFilter>(MeshFilterScratchCapacity);
        // COLD ALLOC: List<Rigidbody>[256] - editor rigidbody tuning scratch - owner: ColliderOptimizationEngine1609
        private static readonly List<Rigidbody> s_RigidbodyScratch = new List<Rigidbody>(RigidbodyScratchCapacity);
        // COLD ALLOC: List<Vector3>[65536] - editor mesh vertex extraction scratch - owner: ColliderOptimizationEngine1609
        private static readonly List<Vector3> s_VertexScratch = new List<Vector3>(VertexScratchCapacity);
        // COLD ALLOC: List<int>[131072] - editor mesh triangle index scratch - owner: ColliderOptimizationEngine1609
        private static readonly List<int> s_IndexScratch = new List<int>(IndexScratchCapacity);

        private static readonly int[] s_BoxHullTriangles =
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            1, 2, 6, 1, 6, 5,
            2, 3, 7, 2, 7, 6,
            3, 0, 4, 3, 4, 7
        };

        public static ColliderOptimizationReport1609 AuditPrefabs(string folder, bool emitErrors)
        {
            ColliderOptimizationReport1609 report = default;
            report.TechnieAvailable = IsTechnieAvailable();
            report.GlobalQualityWeight = DefaultGlobalQualityWeight;
            string[] prefabPaths = FindPrefabPaths(folder);
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < prefabPaths.Length; i++)
                AuditPrefab(prefabPaths[i], emitErrors, ref report);
            stopwatch.Stop();
            report.ExecutionMilliseconds = stopwatch.ElapsedMilliseconds;
            return report;
        }

        public static ColliderOptimizationReport1609 OptimizeFolder(string folder, ColliderOptimizationStrategy1609 strategy)
        {
            return OptimizeFolder(folder, strategy, ColliderOptimizationSettings1609.FromGlobalQualityWeight(DefaultGlobalQualityWeight));
        }

        public static ColliderOptimizationReport1609 OptimizeFolder(string folder, ColliderOptimizationStrategy1609 strategy, ColliderOptimizationSettings1609 settings)
        {
            settings = NormalizeSettings(settings);
            ColliderOptimizationReport1609 report = default;
            report.TechnieAvailable = IsTechnieAvailable();
            report.GlobalQualityWeight = settings.GlobalQualityWeight;
            string[] prefabPaths = FindPrefabPaths(folder);
            Stopwatch stopwatch = Stopwatch.StartNew();
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < prefabPaths.Length; i++)
                {
                    if (strategy == ColliderOptimizationStrategy1609.PurgeAll && !IsFloraPath(prefabPaths[i]))
                        continue;

                    TryOptimizePrefabAsset(prefabPaths[i], strategy, settings, ref report);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            stopwatch.Stop();
            report.ExecutionMilliseconds = stopwatch.ElapsedMilliseconds;
            return report;
        }

        public static ColliderOptimizationReport1609 PurgeFloraColliders()
        {
            ColliderOptimizationSettings1609 settings = ColliderOptimizationSettings1609.FromGlobalQualityWeight(DefaultGlobalQualityWeight);
            ColliderOptimizationReport1609 report = default;
            report.TechnieAvailable = IsTechnieAvailable();
            report.GlobalQualityWeight = settings.GlobalQualityWeight;
            string[] prefabPaths = FindPrefabPaths(PrefabRoot);
            Stopwatch stopwatch = Stopwatch.StartNew();
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < prefabPaths.Length; i++)
                {
                    if (!IsFloraPath(prefabPaths[i]))
                        continue;

                    TryOptimizePrefabAsset(prefabPaths[i], ColliderOptimizationStrategy1609.PurgeAll, settings, ref report);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            stopwatch.Stop();
            report.ExecutionMilliseconds = stopwatch.ElapsedMilliseconds;
            return report;
        }

        public static bool ValidateLayerMatrix(out string failure)
        {
            failure = string.Empty;
            int debris = ResolveLayerIndex("Debris", -1);
            int flora = ResolveLayerIndex("Flora", -1);
            if (debris >= 0 && !UnityEngine.Physics.GetIgnoreLayerCollision(debris, debris))
            {
                failure = "Debris layer still collides with itself.";
                return false;
            }

            if (flora >= 0 && !UnityEngine.Physics.GetIgnoreLayerCollision(flora, flora))
            {
                failure = "Flora layer still collides with itself.";
                return false;
            }

            return true;
        }

        public static bool ValidatePrefabMeshColliderBudget(GameObject prefab, out string failure)
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

                    int triangles = CountMeshColliderTriangles(meshCollider);
                    if (triangles > MeshColliderFatalTriangleLimit)
                    {
                        failure = prefab.name + " MeshCollider exceeds 500 triangles: " + triangles;
                        return false;
                    }
                }
            }
            finally
            {
                ClearMeshColliderScratch();
            }

            return true;
        }

        public static bool ValidateProxyEncapsulation(GameObject prefab, Mesh proxyMesh, out string failure)
        {
            failure = string.Empty;
            if (prefab == null || proxyMesh == null)
            {
                failure = "Null prefab or proxy mesh.";
                return false;
            }

            Bounds proxyBounds = proxyMesh.bounds;
            if (!IsFinite(proxyBounds))
            {
                failure = "Proxy mesh has non-finite bounds.";
                return false;
            }

            Transform root = prefab.transform;
            prefab.GetComponentsInChildren(true, s_MeshFilterScratch);
            try
            {
                for (int f = 0; f < s_MeshFilterScratch.Count; f++)
                {
                    MeshFilter filter = s_MeshFilterScratch[f];
                    if (filter == null ||
                        filter.sharedMesh == null ||
                        filter.sharedMesh == proxyMesh ||
                        !IsPrimaryCollisionVisual(filter))
                    {
                        continue;
                    }

                    Mesh mesh = filter.sharedMesh;
                    Matrix4x4 sourceToRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    s_VertexScratch.Clear();
                    mesh.GetVertices(s_VertexScratch);
                    for (int i = 0; i < s_VertexScratch.Count; i++)
                    {
                        Vector3 point = sourceToRoot.MultiplyPoint3x4(s_VertexScratch[i]);
                        if (!ContainsWithEpsilon(proxyBounds, point, 0.002f))
                        {
                            failure = prefab.name + " visual vertex escapes proxy bounds.";
                            return false;
                        }
                    }
                }
            }
            finally
            {
                ClearMeshFilterScratch();
                ClearVertexScratch();
            }

            return true;
        }

        private static void AuditPrefab(string prefabPath, bool emitErrors, ref ColliderOptimizationReport1609 report)
        {
            report.PrefabsVisited++;
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
                    int triangles = CountMeshColliderTriangles(meshCollider);
                    if (triangles > MeshColliderFatalTriangleLimit)
                    {
                        report.HighPolyMeshColliders++;
                        if (emitErrors)
                            Debug.LogError("[ColliderOptimization1609] High-poly MeshCollider: " + prefabPath + " triangles=" + triangles, prefab);
                    }
                }
            }
            finally
            {
                ClearMeshColliderScratch();
            }
        }

        private static void TryOptimizePrefabAsset(string prefabPath, ColliderOptimizationStrategy1609 strategy, ColliderOptimizationSettings1609 settings, ref ColliderOptimizationReport1609 report)
        {
            try
            {
                OptimizePrefabAsset(prefabPath, strategy, settings, ref report);
            }
            catch (Exception exception)
            {
                report.PrefabsFailed++;
                Debug.LogError("[ColliderOptimization1609] Failed prefab optimization: " + prefabPath + " " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void OptimizePrefabAsset(string prefabPath, ColliderOptimizationStrategy1609 strategy, ColliderOptimizationSettings1609 settings, ref ColliderOptimizationReport1609 report)
        {
            report.PrefabsVisited++;
            GameObject root = null;
            bool changed = false;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    report.PrefabsFailed++;
                    Debug.LogError("[ColliderOptimization1609] Failed prefab optimization: " + prefabPath + " returned null root.");
                    return;
                }

                if (strategy == ColliderOptimizationStrategy1609.PurgeAll || IsFloraPath(prefabPath))
                {
                    changed |= PurgeAllColliders(root, IsFloraPath(prefabPath), ref report);
                }
                else if (strategy == ColliderOptimizationStrategy1609.ConvexHullWrapper || IsOrganicPath(prefabPath))
                {
                    changed |= PrepareProxyBake(root, prefabPath, settings, ref report);
                }
                else
                {
                    changed |= GenerateCompoundColliders(root, prefabPath, settings, ref report);
                }

                changed |= EnforceLayerAndMaterial(root, prefabPath, ref report);
                changed |= TuneRigidbodies(root, prefabPath, ref report);
                if (changed)
                {
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

        private static bool GenerateCompoundColliders(GameObject root, string prefabPath, ColliderOptimizationSettings1609 settings, ref ColliderOptimizationReport1609 report)
        {
            bool changed = false;
            bool hadMeshColliders = false;
            bool hasFallbackBounds = false;
            Vector3 fallbackMin = default;
            Vector3 fallbackMax = default;
            Transform rootTransform = root.transform;
            root.GetComponentsInChildren(true, s_MeshColliderScratch);
            try
            {
                for (int i = 0; i < s_MeshColliderScratch.Count; i++)
                {
                    MeshCollider meshCollider = s_MeshColliderScratch[i];
                    if (meshCollider == null)
                        continue;

                    hadMeshColliders = true;
                    report.VisualTrianglesRemovedFromPhysics += CountMeshColliderTriangles(meshCollider);
                    ExpandMeshColliderRootBounds(meshCollider, rootTransform, ref fallbackMin, ref fallbackMax, ref hasFallbackBounds);
                    Object.DestroyImmediate(meshCollider, true);
                    report.MeshCollidersDeleted++;
                    changed = true;
                }
            }
            finally
            {
                ClearMeshColliderScratch();
            }

            if (!hadMeshColliders)
            {
                Transform legacyRoot = rootTransform.Find(LegacyCompoundRootName);
                Transform existingGeneratedRoot = rootTransform.Find(GeneratedCompoundRootName);
                if (legacyRoot != null && existingGeneratedRoot == null)
                {
                    legacyRoot.name = GeneratedCompoundRootName;
                    return true;
                }

                if (legacyRoot != null)
                {
                    Object.DestroyImmediate(legacyRoot.gameObject, true);
                    return true;
                }

                return false;
            }

            changed |= RemoveGeneratedChildRoot(rootTransform, LegacyCompoundRootName);
            changed |= RemoveGeneratedChildRoot(rootTransform, GeneratedCompoundRootName);

            GameObject generatedRoot = new GameObject(GeneratedCompoundRootName);
            generatedRoot.layer = ResolvePhysicsLayer(prefabPath, root.name, root.layer);
            Transform generatedTransform = generatedRoot.transform;
            generatedTransform.SetParent(rootTransform, false);
            generatedTransform.localPosition = Vector3.zero;
            generatedTransform.localRotation = Quaternion.identity;
            generatedTransform.localScale = Vector3.one;

            int generatedCount = 0;
            int primitiveBudget = Mathf.Max(1, settings.MaxPrimitiveCollidersPerPrefab);
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

                    Matrix4x4 sourceToRoot = rootTransform.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
                    int subMeshCount = Math.Max(1, mesh.subMeshCount);
                    for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                    {
                        if (generatedCount >= primitiveBudget)
                            break;

                        if (!TryFitSubMesh(mesh, subMeshIndex, sourceToRoot, out ColliderPrimitiveFit1609 fit))
                            continue;

                        CreatePrimitiveCollider(generatedTransform, meshFilter.name, subMeshIndex, fit, generatedRoot.layer);
                        generatedCount++;
                    }
                }
            }
            finally
            {
                ClearMeshFilterScratch();
            }

            if (generatedCount <= 0)
            {
                Object.DestroyImmediate(generatedRoot, true);
                if (hasFallbackBounds)
                {
                    BoxCollider fallback = root.AddComponent<BoxCollider>();
                    fallback.center = (fallbackMin + fallbackMax) * 0.5f;
                    fallback.size = ClampColliderSize(fallbackMax - fallbackMin);
                    fallback.isTrigger = false;
                    report.PrimitiveCollidersGenerated++;
                    return true;
                }

                return changed;
            }

            report.PrimitiveCollidersGenerated += generatedCount;
            return true;
        }

        private static bool PrepareProxyBake(GameObject root, string prefabPath, ColliderOptimizationSettings1609 settings, ref ColliderOptimizationReport1609 report)
        {
            RuntimePhysicsBaker1609 baker = root.GetComponent<RuntimePhysicsBaker1609>();
            bool cleanupChanged = RemoveExistingProxyBakeArtifacts(baker, ref report);

            Mesh proxy = BuildRootAabbProxyMesh(root, root.name, settings.ProxyPaddingMeters);
            if (proxy == null)
            {
                RemoveProxyBakerComponent(baker);
                return cleanupChanged | GenerateCompoundColliders(root, prefabPath, settings, ref report);
            }

            if (CountMeshTrianglesNoAlloc(proxy) > ProxyMeshTriangleLimit)
            {
                Object.DestroyImmediate(proxy);
                RemoveProxyBakerComponent(baker);
                return cleanupChanged | GenerateCompoundColliders(root, prefabPath, settings, ref report);
            }

            EnsureGeneratedFolders();
            string meshPath = AssetDatabase.GenerateUniqueAssetPath(GeneratedAssetRoot + "/COL_" + SanitizeAssetStem(root.name) + ".asset");
            AssetDatabase.CreateAsset(proxy, meshPath);
            report.ProxyMeshesGenerated++;

            root.GetComponentsInChildren(true, s_MeshColliderScratch);
            try
            {
                for (int i = 0; i < s_MeshColliderScratch.Count; i++)
                {
                    MeshCollider existing = s_MeshColliderScratch[i];
                    if (existing == null)
                        continue;

                    report.VisualTrianglesRemovedFromPhysics += CountMeshColliderTriangles(existing);
                    Object.DestroyImmediate(existing, true);
                    report.MeshCollidersDeleted++;
                }
            }
            finally
            {
                ClearMeshColliderScratch();
            }

            Transform rootTransform = root.transform;
            RemoveGeneratedChildRoot(rootTransform, GeneratedConvexRootName);
            GameObject colliderRoot = new GameObject(GeneratedConvexRootName);
            colliderRoot.layer = ResolvePhysicsLayer(prefabPath, root.name, root.layer);
            Transform colliderTransform = colliderRoot.transform;
            colliderTransform.SetParent(rootTransform, false);
            colliderTransform.localPosition = Vector3.zero;
            colliderTransform.localRotation = Quaternion.identity;
            colliderTransform.localScale = Vector3.one;

            MeshCollider collider = colliderRoot.AddComponent<MeshCollider>();
            collider.sharedMesh = proxy;
            collider.convex = true;
            collider.enabled = false;
            collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
                                      MeshColliderCookingOptions.EnableMeshCleaning |
                                      MeshColliderCookingOptions.WeldColocatedVertices;

            BoxCollider bootstrap = root.AddComponent<BoxCollider>();
            bootstrap.center = proxy.bounds.center;
            bootstrap.size = ClampColliderSize(proxy.bounds.size);
            bootstrap.isTrigger = false;

            if (baker == null)
                baker = root.AddComponent<RuntimePhysicsBaker1609>();
            baker.ConfigureAuthoring(proxy, collider, bootstrap, true);
            return true;
        }

        private static bool RemoveExistingProxyBakeArtifacts(RuntimePhysicsBaker1609 baker, ref ColliderOptimizationReport1609 report)
        {
            if (baker == null)
                return false;

            bool changed = false;
            MeshCollider target = baker.TargetCollider;
            if (target != null && IsOwnedByBakerRoot(target, baker))
            {
                report.VisualTrianglesRemovedFromPhysics += CountMeshColliderTriangles(target);
                Object.DestroyImmediate(target, true);
                report.MeshCollidersDeleted++;
                changed = true;
            }

            BoxCollider bootstrap = baker.BootstrapProxyCollider;
            if (bootstrap != null && IsOwnedByBakerRoot(bootstrap, baker))
            {
                Object.DestroyImmediate(bootstrap, true);
                changed = true;
            }

            Mesh oldProxy = baker.CollisionProxyMesh;
            string oldProxyPath = oldProxy != null ? AssetDatabase.GetAssetPath(oldProxy) : string.Empty;
            if (IsGeneratedProxyMeshAssetPath(oldProxyPath) && AssetDatabase.DeleteAsset(oldProxyPath))
            {
                report.ProxyMeshesDeleted++;
                changed = true;
            }

            return changed;
        }

        private static void RemoveProxyBakerComponent(RuntimePhysicsBaker1609 baker)
        {
            if (baker != null)
                Object.DestroyImmediate(baker, true);
        }

        private static bool IsOwnedByBakerRoot(Component component, RuntimePhysicsBaker1609 baker)
        {
            return component != null &&
                   baker != null &&
                   component.transform.root == baker.transform.root;
        }

        private static bool RemoveGeneratedChildRoot(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
                return false;

            Transform existing = parent.Find(childName);
            if (existing == null)
                return false;

            Object.DestroyImmediate(existing.gameObject, true);
            return true;
        }

        private static bool PurgeAllColliders(GameObject root, bool flora, ref ColliderOptimizationReport1609 report)
        {
            root.GetComponentsInChildren(true, s_ColliderScratch);
            try
            {
                if (s_ColliderScratch.Count <= 0)
                    return false;

                for (int i = 0; i < s_ColliderScratch.Count; i++)
                {
                    Collider collider = s_ColliderScratch[i];
                    if (collider == null)
                        continue;

                    if (collider is MeshCollider meshCollider)
                    {
                        report.VisualTrianglesRemovedFromPhysics += CountMeshColliderTriangles(meshCollider);
                        report.MeshCollidersDeleted++;
                    }

                    Object.DestroyImmediate(collider, true);
                    if (flora)
                        report.FloraCollidersDeleted++;
                }

                return true;
            }
            finally
            {
                ClearColliderScratch();
            }
        }

        private static bool EnforceLayerAndMaterial(GameObject root, string prefabPath, ref ColliderOptimizationReport1609 report)
        {
            bool changed = false;
            int layer = ResolvePhysicsLayer(prefabPath, root.name, root.layer);
            UnityEngine.PhysicsMaterial material = null;
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

                    if (material == null)
                        material = ResolvePhysicsMaterial(prefabPath, root.name);

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
                ClearColliderScratch();
            }

            return changed;
        }

        private static bool TuneRigidbodies(GameObject root, string prefabPath, ref ColliderOptimizationReport1609 report)
        {
            bool changed = false;
            bool critical = IsCriticalCcdPath(prefabPath, root.name);
            root.GetComponentsInChildren(true, s_RigidbodyScratch);
            try
            {
                for (int i = 0; i < s_RigidbodyScratch.Count; i++)
                {
                    Rigidbody body = s_RigidbodyScratch[i];
                    if (body == null)
                        continue;

                    float targetSleep = ResolveSleepThreshold(prefabPath, root.name);
                    if (body.sleepThreshold < targetSleep)
                    {
                        body.sleepThreshold = targetSleep;
                        report.RigidbodiesTuned++;
                        changed = true;
                    }

                    if (!critical &&
                        (body.collisionDetectionMode == CollisionDetectionMode.Continuous ||
                         body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic ||
                         body.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative))
                    {
                        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                        report.CcdStripped++;
                        changed = true;
                    }
                }
            }
            finally
            {
                ClearRigidbodyScratch();
            }

            return changed;
        }

        private static Mesh BuildRootAabbProxyMesh(GameObject root, string sourceName, float paddingMeters)
        {
            if (root == null)
                return null;

            Transform rootTransform = root.transform;
            Vector3 min = default;
            Vector3 max = default;
            bool hasPoint = false;
            root.GetComponentsInChildren(true, s_MeshFilterScratch);
            try
            {
                for (int f = 0; f < s_MeshFilterScratch.Count; f++)
                {
                    MeshFilter filter = s_MeshFilterScratch[f];
                    Mesh sourceMesh = filter != null ? filter.sharedMesh : null;
                    if (sourceMesh == null || sourceMesh.vertexCount <= 0)
                        continue;

                    if (!IsPrimaryCollisionVisual(filter))
                        continue;

                    Matrix4x4 sourceToRoot = rootTransform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    s_VertexScratch.Clear();
                    sourceMesh.GetVertices(s_VertexScratch);
                    for (int i = 0; i < s_VertexScratch.Count; i++)
                    {
                        Vector3 point = sourceToRoot.MultiplyPoint3x4(s_VertexScratch[i]);
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
                }

                if (!hasPoint)
                    return null;

                float safePadding = Mathf.Clamp(paddingMeters, MinProxyPaddingMeters, MaxProxyPaddingMeters);
                Vector3 padding = new Vector3(safePadding, safePadding, safePadding);
                min -= padding;
                max += padding;

                Vector3[] outputVertices =
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

                Mesh proxyMesh = new Mesh
                {
                    name = "COL_" + SanitizeAssetStem(sourceName)
                };
                proxyMesh.SetVertices(outputVertices);
                proxyMesh.SetTriangles(s_BoxHullTriangles, 0, false);
                proxyMesh.RecalculateNormals();
                proxyMesh.RecalculateBounds();
                return proxyMesh;
            }
            finally
            {
                ClearMeshFilterScratch();
                ClearVertexScratch();
            }
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

        private static bool IsGeneratedCollisionName(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   (value.StartsWith("COL_", StringComparison.OrdinalIgnoreCase) ||
                    value.IndexOf("_COL_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("CollisionProxy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("PhysicsProxy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf("Impostor", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool TryFitSubMesh(Mesh mesh, int subMeshIndex, Matrix4x4 sourceToRoot, out ColliderPrimitiveFit1609 fit)
        {
            fit = default;
            s_VertexScratch.Clear();
            s_IndexScratch.Clear();
            mesh.GetVertices(s_VertexScratch);
            mesh.GetTriangles(s_IndexScratch, subMeshIndex, true);
            if (s_VertexScratch.Count <= 0 || s_IndexScratch.Count <= 0)
            {
                ClearMeshFitScratch();
                return false;
            }

            Vector3 centroid = Vector3.zero;
            Vector3 aabbMin = default;
            Vector3 aabbMax = default;
            bool hasPoint = false;
            int pointCount = 0;
            for (int i = 0; i < s_IndexScratch.Count; i++)
            {
                int vertexIndex = s_IndexScratch[i];
                if ((uint)vertexIndex >= (uint)s_VertexScratch.Count)
                    continue;

                Vector3 point = sourceToRoot.MultiplyPoint3x4(s_VertexScratch[vertexIndex]);
                if (!IsFinite(point))
                    continue;

                centroid += point;
                pointCount++;
                if (!hasPoint)
                {
                    aabbMin = point;
                    aabbMax = point;
                    hasPoint = true;
                    continue;
                }

                aabbMin = Vector3.Min(aabbMin, point);
                aabbMax = Vector3.Max(aabbMax, point);
            }

            if (pointCount <= 2)
            {
                ClearMeshFitScratch();
                return false;
            }

            centroid /= pointCount;
            SymmetricCovariance1609 covariance = default;
            for (int i = 0; i < s_IndexScratch.Count; i++)
            {
                int vertexIndex = s_IndexScratch[i];
                if ((uint)vertexIndex >= (uint)s_VertexScratch.Count)
                    continue;

                Vector3 point = sourceToRoot.MultiplyPoint3x4(s_VertexScratch[vertexIndex]);
                if (!IsFinite(point))
                    continue;

                Vector3 d = point - centroid;
                covariance.XX += d.x * d.x;
                covariance.XY += d.x * d.y;
                covariance.XZ += d.x * d.z;
                covariance.YY += d.y * d.y;
                covariance.YZ += d.y * d.z;
                covariance.ZZ += d.z * d.z;
            }

            float invCount = 1f / pointCount;
            covariance.XX *= invCount;
            covariance.XY *= invCount;
            covariance.XZ *= invCount;
            covariance.YY *= invCount;
            covariance.YZ *= invCount;
            covariance.ZZ *= invCount;

            Vector3 seed = ResolveLargestAabbAxis(aabbMax - aabbMin);
            Vector3 axis0 = ResolvePrincipalAxis(covariance, seed, Vector3.zero);
            Vector3 axis1 = ResolvePrincipalAxis(covariance, ResolveFallbackAxis(axis0), axis0);
            Vector3 axis2 = Vector3.Cross(axis0, axis1);
            if (axis2.sqrMagnitude <= 0.000001f)
            {
                ClearMeshFitScratch();
                return false;
            }

            axis2.Normalize();
            axis1 = Vector3.Cross(axis2, axis0).normalized;
            if (!IsFinite(axis0) || !IsFinite(axis1) || !IsFinite(axis2))
            {
                ClearMeshFitScratch();
                return false;
            }

            float min0 = float.PositiveInfinity;
            float min1 = float.PositiveInfinity;
            float min2 = float.PositiveInfinity;
            float max0 = float.NegativeInfinity;
            float max1 = float.NegativeInfinity;
            float max2 = float.NegativeInfinity;
            for (int i = 0; i < s_IndexScratch.Count; i++)
            {
                int vertexIndex = s_IndexScratch[i];
                if ((uint)vertexIndex >= (uint)s_VertexScratch.Count)
                    continue;

                Vector3 point = sourceToRoot.MultiplyPoint3x4(s_VertexScratch[vertexIndex]);
                if (!IsFinite(point))
                    continue;

                float d0 = Vector3.Dot(point, axis0);
                float d1 = Vector3.Dot(point, axis1);
                float d2 = Vector3.Dot(point, axis2);
                min0 = Mathf.Min(min0, d0);
                min1 = Mathf.Min(min1, d1);
                min2 = Mathf.Min(min2, d2);
                max0 = Mathf.Max(max0, d0);
                max1 = Mathf.Max(max1, d1);
                max2 = Mathf.Max(max2, d2);
            }

            Vector3 size = ClampColliderSize(new Vector3(max0 - min0, max1 - min1, max2 - min2));
            Vector3 center =
                axis0 * ((min0 + max0) * 0.5f) +
                axis1 * ((min1 + max1) * 0.5f) +
                axis2 * ((min2 + max2) * 0.5f);
            Quaternion rotation = Quaternion.LookRotation(axis2, axis1);
            if (!IsFinite(rotation))
                rotation = Quaternion.identity;

            fit.Center = center;
            fit.Rotation = rotation;
            fit.Size = size;
            ResolvePrimitiveKind(size, out fit.PrimitiveKind, out fit.CapsuleDirection, out fit.SphereRadius, out fit.CapsuleRadius, out fit.CapsuleHeight);
            ClearMeshFitScratch();
            return true;
        }

        private static void ClearMeshFitScratch()
        {
            ClearVertexScratch();
            ClearIndexScratch();
        }

        private static void ClearMeshColliderScratch()
        {
            ClearScratch(s_MeshColliderScratch, MeshColliderScratchCapacity);
        }

        private static void ClearColliderScratch()
        {
            ClearScratch(s_ColliderScratch, ColliderScratchCapacity);
        }

        private static void ClearMeshFilterScratch()
        {
            ClearScratch(s_MeshFilterScratch, MeshFilterScratchCapacity);
        }

        private static void ClearRigidbodyScratch()
        {
            ClearScratch(s_RigidbodyScratch, RigidbodyScratchCapacity);
        }

        private static void ClearVertexScratch()
        {
            ClearScratch(s_VertexScratch, VertexScratchCapacity);
        }

        private static void ClearIndexScratch()
        {
            ClearScratch(s_IndexScratch, IndexScratchCapacity);
        }

        private static void ClearScratch<T>(List<T> scratch, int maxCapacity)
        {
            scratch.Clear();
            if (scratch.Capacity > maxCapacity)
                scratch.Capacity = maxCapacity;
        }

        private static void CreatePrimitiveCollider(Transform generatedRoot, string sourceName, int subMeshIndex, ColliderPrimitiveFit1609 fit, int layer)
        {
            GameObject child = new GameObject(SanitizeAssetStem(sourceName) + "_SM" + subMeshIndex.ToString("00") + "_Collider");
            child.layer = layer;
            Transform transform = child.transform;
            transform.SetParent(generatedRoot, false);
            transform.localPosition = fit.Center;
            transform.localRotation = fit.Rotation;
            transform.localScale = Vector3.one;

            if (fit.PrimitiveKind == 2)
            {
                CapsuleCollider capsule = child.AddComponent<CapsuleCollider>();
                capsule.center = Vector3.zero;
                capsule.direction = fit.CapsuleDirection;
                capsule.radius = fit.CapsuleRadius;
                capsule.height = fit.CapsuleHeight;
                capsule.isTrigger = false;
                return;
            }

            if (fit.PrimitiveKind == 1)
            {
                SphereCollider sphere = child.AddComponent<SphereCollider>();
                sphere.center = Vector3.zero;
                sphere.radius = fit.SphereRadius;
                sphere.isTrigger = false;
                return;
            }

            BoxCollider box = child.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = fit.Size;
            box.isTrigger = false;
        }

        private static void ResolvePrimitiveKind(Vector3 size, out byte kind, out int capsuleDirection, out float sphereRadius, out float capsuleRadius, out float capsuleHeight)
        {
            float maxAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float minAxis = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            sphereRadius = maxAxis * 0.5f;
            capsuleDirection = ResolveDominantAxis(size);
            int axisA = (capsuleDirection + 1) % 3;
            int axisB = (capsuleDirection + 2) % 3;
            float dominant = GetAxis(size, capsuleDirection);
            float secondaryA = GetAxis(size, axisA);
            float secondaryB = GetAxis(size, axisB);
            float secondaryMax = Mathf.Max(secondaryA, secondaryB);
            float circularity = Mathf.Abs(secondaryA - secondaryB) / Mathf.Max(secondaryMax, MinimumColliderAxisMeters);
            capsuleRadius = Mathf.Max(MinimumColliderAxisMeters, secondaryMax * 0.5f);
            capsuleHeight = Mathf.Max(capsuleRadius * 2f, dominant);

            if ((maxAxis - minAxis) / Mathf.Max(maxAxis, MinimumColliderAxisMeters) <= SphereAxisTolerance)
            {
                kind = 1;
                return;
            }

            kind = dominant >= secondaryMax * CapsuleAspectThreshold && circularity <= CapsuleCircularityTolerance ? (byte)2 : (byte)0;
        }

        private static Vector3 ResolvePrincipalAxis(SymmetricCovariance1609 covariance, Vector3 seed, Vector3 rejectAxis)
        {
            Vector3 axis = Orthogonalize(seed, rejectAxis);
            if (axis.sqrMagnitude <= 0.000001f)
                axis = Orthogonalize(Vector3.right, rejectAxis);
            if (axis.sqrMagnitude <= 0.000001f)
                axis = Orthogonalize(Vector3.up, rejectAxis);
            if (axis.sqrMagnitude <= 0.000001f)
                axis = Vector3.forward;

            axis.Normalize();
            for (int i = 0; i < PowerIterationCount; i++)
            {
                Vector3 next = Orthogonalize(covariance.Multiply(axis), rejectAxis);
                if (next.sqrMagnitude <= 0.000001f)
                    break;

                axis = next.normalized;
            }

            return axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector3.right;
        }

        private static Vector3 Orthogonalize(Vector3 value, Vector3 rejectAxis)
        {
            if (rejectAxis.sqrMagnitude <= 0.000001f)
                return value;

            Vector3 normalizedReject = rejectAxis.normalized;
            return value - normalizedReject * Vector3.Dot(value, normalizedReject);
        }

        private static Vector3 ResolveLargestAabbAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
                return Vector3.right;
            if (size.y >= size.z)
                return Vector3.up;
            return Vector3.forward;
        }

        private static Vector3 ResolveFallbackAxis(Vector3 axis)
        {
            float x = Mathf.Abs(Vector3.Dot(axis, Vector3.right));
            float y = Mathf.Abs(Vector3.Dot(axis, Vector3.up));
            float z = Mathf.Abs(Vector3.Dot(axis, Vector3.forward));
            if (x <= y && x <= z)
                return Vector3.right;
            if (y <= z)
                return Vector3.up;
            return Vector3.forward;
        }

        private static int ResolveDominantAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
                return 0;
            if (size.y >= size.z)
                return 1;
            return 2;
        }

        private static float GetAxis(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : (axis == 1 ? value.y : value.z);
        }

        private static int CountMeshColliderTriangles(MeshCollider meshCollider)
        {
            Mesh mesh = meshCollider != null ? meshCollider.sharedMesh : null;
            if (mesh == null)
            {
                MeshFilter filter = meshCollider != null ? meshCollider.GetComponent<MeshFilter>() : null;
                mesh = filter != null ? filter.sharedMesh : null;
            }

            return CountMeshTrianglesNoAlloc(mesh);
        }

        private static void ExpandMeshColliderRootBounds(
            MeshCollider meshCollider,
            Transform rootTransform,
            ref Vector3 min,
            ref Vector3 max,
            ref bool hasPoint)
        {
            Mesh mesh = meshCollider != null ? meshCollider.sharedMesh : null;
            if (mesh == null)
            {
                MeshFilter filter = meshCollider != null ? meshCollider.GetComponent<MeshFilter>() : null;
                mesh = filter != null ? filter.sharedMesh : null;
            }

            if (mesh == null || rootTransform == null || meshCollider == null)
                return;

            Bounds bounds = mesh.bounds;
            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                return;

            Matrix4x4 sourceToRoot = rootTransform.worldToLocalMatrix * meshCollider.transform.localToWorldMatrix;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    center.x + (((corner & 1) == 0) ? -extents.x : extents.x),
                    center.y + (((corner & 2) == 0) ? -extents.y : extents.y),
                    center.z + (((corner & 4) == 0) ? -extents.z : extents.z));
                point = sourceToRoot.MultiplyPoint3x4(point);
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

        private static ColliderOptimizationSettings1609 NormalizeSettings(ColliderOptimizationSettings1609 settings)
        {
            ColliderOptimizationSettings1609 defaults = ColliderOptimizationSettings1609.FromGlobalQualityWeight(settings.GlobalQualityWeight);
            float quality = defaults.GlobalQualityWeight;

            if (settings.MaxPrimitiveCollidersPerPrefab <= 0)
                settings.MaxPrimitiveCollidersPerPrefab = defaults.MaxPrimitiveCollidersPerPrefab;
            else
                settings.MaxPrimitiveCollidersPerPrefab = Mathf.Clamp(settings.MaxPrimitiveCollidersPerPrefab, MinPrimitiveCollidersPerPrefab, MaxPrimitiveCollidersPerPrefab);

            if (!IsFinite(settings.ProxyPaddingMeters) || settings.ProxyPaddingMeters <= 0f)
                settings.ProxyPaddingMeters = defaults.ProxyPaddingMeters;
            else
                settings.ProxyPaddingMeters = Mathf.Clamp(settings.ProxyPaddingMeters, MinProxyPaddingMeters, MaxProxyPaddingMeters);

            settings.GlobalQualityWeight = quality;
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

        private static bool IsTechnieAvailable()
        {
            Type type = Type.GetType(TechnieTypeName + ", Technie.PhysicsCreator");
            return type != null;
        }

        private static bool IsFloraPath(string path)
        {
            if (IsNonFloraInteractablePath(path) || IsNonFloraPhysicalEnvironmentPath(path))
                return false;

            return ContainsOrdinalIgnoreCase(path, "/Flora/") ||
                   ContainsOrdinalIgnoreCase(path, "_Flora_") ||
                   ContainsOrdinalIgnoreCase(path, "family_coral_low") ||
                   ContainsOrdinalIgnoreCase(path, "family_coral_brittle") ||
                   ContainsOrdinalIgnoreCase(path, "family_coral_branching") ||
                   ContainsOrdinalIgnoreCase(path, "kelp") ||
                   ContainsOrdinalIgnoreCase(path, "grass");
        }

        private static bool IsNonFloraInteractablePath(string path)
        {
            return ContainsOrdinalIgnoreCase(path, "/Resources/Pickups/") ||
                   ContainsOrdinalIgnoreCase(path, "/Items/") ||
                   ContainsOrdinalIgnoreCase(path, "/Tools/") ||
                   ContainsOrdinalIgnoreCase(path, "Pickup");
        }

        private static bool IsNonFloraPhysicalEnvironmentPath(string path)
        {
            return ContainsOrdinalIgnoreCase(path, "/PorousRock/") ||
                   ContainsOrdinalIgnoreCase(path, "_Rock_") ||
                   ContainsOrdinalIgnoreCase(path, "/Rocks/") ||
                   ContainsOrdinalIgnoreCase(path, "/Geology/") ||
                   ContainsOrdinalIgnoreCase(path, "GOTOVYE_PREFABY_KAMNEY") ||
                   ContainsOrdinalIgnoreCase(path, "PFB_Geo");
        }

        private static bool IsOrganicPath(string path)
        {
            return ContainsOrdinalIgnoreCase(path, "Rock") ||
                   ContainsOrdinalIgnoreCase(path, "Geology") ||
                   ContainsOrdinalIgnoreCase(path, "Coral") ||
                   ContainsOrdinalIgnoreCase(path, "Nature");
        }

        private static bool IsCriticalCcdPath(string path, string name)
        {
            return ContainsOrdinalIgnoreCase(path, "Player") ||
                   ContainsOrdinalIgnoreCase(path, "Submarine") ||
                   ContainsOrdinalIgnoreCase(path, "Projectile") ||
                   ContainsOrdinalIgnoreCase(path, "Harpoon") ||
                   ContainsOrdinalIgnoreCase(name, "Player") ||
                   ContainsOrdinalIgnoreCase(name, "Submarine");
        }

        private static float ResolveSleepThreshold(string path, string name)
        {
            if (ContainsOrdinalIgnoreCase(path, "Debris") || ContainsOrdinalIgnoreCase(name, "Debris"))
                return 0.05f;
            if (ContainsOrdinalIgnoreCase(path, "Cargo") || ContainsOrdinalIgnoreCase(name, "Cargo"))
                return 0.04f;
            return 0.02f;
        }

        private static int ResolvePhysicsLayer(string path, string name, int fallback)
        {
            if (IsFloraPath(path))
                return ResolveLayerIndex("Flora", fallback);
            if (ContainsOrdinalIgnoreCase(path, "Debris") || ContainsOrdinalIgnoreCase(name, "Debris"))
                return ResolveLayerIndex("Debris", fallback);
            if (ContainsOrdinalIgnoreCase(path, "Module") || ContainsOrdinalIgnoreCase(name, "Module") || ContainsOrdinalIgnoreCase(path, "Structure"))
                return ResolveLayerIndex("BaseModule", fallback);
            if (ContainsOrdinalIgnoreCase(path, "Vehicle") || ContainsOrdinalIgnoreCase(name, "Submarine"))
                return ResolveLayerIndex("Vehicle", fallback);
            if (ContainsOrdinalIgnoreCase(path, "Item") || ContainsOrdinalIgnoreCase(name, "Item"))
                return ResolveLayerIndex("DroppedItem", fallback);
            if (ContainsOrdinalIgnoreCase(path, "Voxel") || ContainsOrdinalIgnoreCase(path, "Cave"))
                return ResolveLayerIndex("VoxelCave", fallback);
            return ResolveLayerIndex("Terrain", fallback);
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
            if (ContainsOrdinalIgnoreCase(path, "Debris") || ContainsOrdinalIgnoreCase(name, "Debris"))
                return EnsurePhysicsMaterial("MAT_Physics_Debris_1609", 0.55f, 0.65f, 0.02f);
            if (ContainsOrdinalIgnoreCase(path, "Metal") || ContainsOrdinalIgnoreCase(path, "Module") || ContainsOrdinalIgnoreCase(name, "Module"))
                return EnsurePhysicsMaterial("MAT_Physics_HighFrictionFloor_1609", 0.85f, 0.95f, 0f);
            if (IsOrganicPath(path))
                return EnsurePhysicsMaterial("MAT_Physics_SlipperyOrganic_1609", 0.15f, 0.2f, 0f);
            return EnsurePhysicsMaterial("MAT_Physics_WorldStatic_1609", 0.45f, 0.55f, 0f);
        }

        private static UnityEngine.PhysicsMaterial EnsurePhysicsMaterial(string name, float dynamicFriction, float staticFriction, float bounciness)
        {
            string path = GeneratedAssetRoot + "/Materials/" + name + ".asset";
            UnityEngine.PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<UnityEngine.PhysicsMaterial>(path);
            if (material == null)
            {
                material = new UnityEngine.PhysicsMaterial(name);
                AssetDatabase.CreateAsset(material, path);
            }

            material.dynamicFriction = dynamicFriction;
            material.staticFriction = staticFriction;
            material.bounciness = bounciness;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureGeneratedFolders()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/Generated");
            EnsureFolder(GeneratedAssetRoot);
            EnsureFolder(GeneratedAssetRoot + "/Materials");
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

        private static bool ContainsWithEpsilon(Bounds bounds, Vector3 point, float epsilon)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return point.x >= min.x - epsilon && point.x <= max.x + epsilon &&
                   point.y >= min.y - epsilon && point.y <= max.y + epsilon &&
                   point.z >= min.z - epsilon && point.z <= max.z + epsilon;
        }

        private static bool ContainsOrdinalIgnoreCase(string source, string token)
        {
            return !string.IsNullOrEmpty(source) &&
                   !string.IsNullOrEmpty(token) &&
                   source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsGeneratedProxyMeshAssetPath(string path)
        {
            string normalized = !string.IsNullOrEmpty(path) ? path.Replace('\\', '/') : string.Empty;
            return normalized.StartsWith(GeneratedAssetRoot + "/COL_", StringComparison.OrdinalIgnoreCase) &&
                   normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
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
                bool isAsciiLetterOrDigit =
                    (c >= '0' && c <= '9') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= 'a' && c <= 'z');
                if (isAsciiLetterOrDigit)
                {
                    hasStableCharacter = true;
                    continue;
                }

                if (c == '_' || c == '-')
                    continue;

                chars[i] = '_';
            }

            return hasStableCharacter ? new string(chars) : "ColliderProxy";
        }

        private static bool IsFinite(Bounds bounds)
        {
            return IsFinite(bounds.center) &&
                   IsFinite(bounds.extents) &&
                   bounds.extents.x >= 0f &&
                   bounds.extents.y >= 0f &&
                   bounds.extents.z >= 0f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
