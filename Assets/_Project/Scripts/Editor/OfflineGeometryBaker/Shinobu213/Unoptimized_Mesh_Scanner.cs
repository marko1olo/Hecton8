#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.OfflineGeometry
{
    internal struct UnoptimizedMeshFinding
    {
        public string AssetPath;
        public string Issue;
        public string Detail;
        public int TriangleCount;
    }

    internal static class Unoptimized_Mesh_Scanner
    {
        private static readonly string[] _ScanRoots =
        {
            "Assets/_Project/Prefabs",
            "Assets/_Project/Prefabs/Environment",
            "Assets/_Project/Prefabs/Interactables"
        };

        [MenuItem("HECTON-8/LOD Collider Forge/Write Physics Optimization Report", false, 253)]
        public static void ScanAndWriteReportMenu()
        {
            List<UnoptimizedMeshFinding> findings = ScanProject();
            WriteReport(findings);
            OfflineGeometrySelfAudit.WriteSelfAuditReport();
            Debug.Log("[SHINOBU_213] PHYSICS_OPTIMIZATION_REPORT findings=" + findings.Count + ".");
        }

        [MenuItem("HECTON-8/LOD Collider Forge/Repair High Poly Concave MeshColliders", false, 254)]
        public static void RepairHighPolyConcaveMeshCollidersMenu()
        {
            int repaired = RepairHighPolyConcaveMeshColliders();
            List<UnoptimizedMeshFinding> findings = ScanProject();
            WriteReport(findings);
            OfflineGeometrySelfAudit.WriteSelfAuditReport();
            Debug.Log("[SHINOBU_213] Concave MeshCollider repair pass replaced=" + repaired + ".");
        }

        internal static List<UnoptimizedMeshFinding> ScanProject()
        {
            var findings = new List<UnoptimizedMeshFinding>(128);
            var seen = new HashSet<string>(128, StringComparer.Ordinal);
            for (int rootIndex = 0; rootIndex < _ScanRoots.Length; rootIndex++)
            {
                string root = _ScanRoots[rootIndex];
                if (!AssetDatabase.IsValidFolder(root))
                    continue;

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrWhiteSpace(path) || !seen.Add(path))
                        continue;

                    ScanPrefab(path, findings);
                }
            }

            return findings;
        }

        internal static int RepairHighPolyConcaveMeshColliders()
        {
            int repaired = 0;
            var seen = new HashSet<string>(128, StringComparer.Ordinal);
            for (int rootIndex = 0; rootIndex < _ScanRoots.Length; rootIndex++)
            {
                string root = _ScanRoots[rootIndex];
                if (!AssetDatabase.IsValidFolder(root))
                    continue;

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrWhiteSpace(path) || !seen.Add(path))
                        continue;

                    repaired += RepairPrefab(path);
                }
            }

            if (repaired > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return repaired;
        }

        internal static void WriteReport(List<UnoptimizedMeshFinding> findings)
        {
            OfflineGeometryBaker.EnsureFileFolder(OfflineGeometryBakerConstants.PhysicsReportPath);
            int count = findings != null ? findings.Count : 0;
            var builder = new StringBuilder(4096);
            builder.Append("{\n  \"agent\": \"SHINOBU_213\",\n  \"status\": \"PENDING_VERIFICATION\",\n  \"scanRoots\": [");
            for (int i = 0; i < _ScanRoots.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append("\"");
                builder.Append(OfflineGeometryBaker.Escape(_ScanRoots[i]));
                builder.Append("\"");
            }

            builder.Append("],\n  \"findingCount\": ");
            builder.Append(count);
            builder.Append(",\n  \"findings\": [\n");
            if (findings != null)
            {
                for (int i = 0; i < findings.Count; i++)
                {
                    UnoptimizedMeshFinding finding = findings[i];
                    if (i > 0)
                        builder.Append(",\n");
                    builder.Append("    { \"asset\": \"");
                    builder.Append(OfflineGeometryBaker.Escape(finding.AssetPath));
                    builder.Append("\", \"issue\": \"");
                    builder.Append(OfflineGeometryBaker.Escape(finding.Issue));
                    builder.Append("\", \"triangles\": ");
                    builder.Append(finding.TriangleCount);
                    builder.Append(", \"detail\": \"");
                    builder.Append(OfflineGeometryBaker.Escape(finding.Detail));
                    builder.Append("\" }");
                }
            }

            builder.Append("\n  ],\n  \"policy\": \"Concave MeshCollider on high-poly visual meshes is forbidden. Generated output must use BoxCollider, SphereCollider, or convex MeshCollider only.\"\n}\n");
            OfflineGeometryBaker.WriteTextFileAtomic(OfflineGeometryBakerConstants.PhysicsReportPath, builder.ToString());
        }

        private static void ScanPrefab(string path, List<UnoptimizedMeshFinding> findings)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return;

            int totalTriangles = CountPrefabTriangles(prefab);
            bool environmentOrInteractable = IsEnvironmentOrInteractable(path);
            if (environmentOrInteractable && totalTriangles > OfflineGeometryBakerConstants.HighPolyColliderTriangles && prefab.GetComponentInChildren<LODGroup>(true) == null)
            {
                findings.Add(new UnoptimizedMeshFinding
                {
                    AssetPath = path,
                    Issue = "MISSING_LODGROUP",
                    Detail = "Environment/interactable prefab exceeds triangle threshold without LODGroup.",
                    TriangleCount = totalTriangles
                });
            }

            MeshCollider[] colliders = prefab.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                MeshCollider collider = colliders[i];
                if (collider == null || collider.convex)
                    continue;

                int colliderTriangles = OfflineGeometryBaker.CountTriangles(collider.sharedMesh);
                if (colliderTriangles > OfflineGeometryBakerConstants.HighPolyColliderTriangles)
                {
                    findings.Add(new UnoptimizedMeshFinding
                    {
                        AssetPath = path,
                        Issue = "CONCAVE_HIGH_POLY_MESHCOLLIDER",
                        Detail = collider.name + " uses non-convex mesh collision.",
                        TriangleCount = colliderTriangles
                    });
                }
            }

            ScanManualLodDrift(path, prefab, findings);
        }

        private static int RepairPrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
                return 0;

            int repaired = 0;
            try
            {
                MeshCollider[] colliders = root.GetComponentsInChildren<MeshCollider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    MeshCollider collider = colliders[i];
                    if (collider == null || collider.convex)
                        continue;

                    Mesh mesh = collider.sharedMesh;
                    int triangles = OfflineGeometryBaker.CountTriangles(mesh);
                    if (triangles <= OfflineGeometryBakerConstants.HighPolyColliderTriangles || mesh == null)
                        continue;

                    Bounds bounds = mesh.bounds;
                    GameObject owner = collider.gameObject;
                    UnityEngine.Object.DestroyImmediate(collider, true);
                    BoxCollider box = owner.AddComponent<BoxCollider>();
                    box.center = bounds.center;
                    box.size = new Vector3(
                        Mathf.Max(0.01f, bounds.size.x),
                        Mathf.Max(0.01f, bounds.size.y),
                        Mathf.Max(0.01f, bounds.size.z));
                    repaired++;
                }

                if (repaired > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    if (!saved)
                    {
                        Debug.LogWarning("[SHINOBU_213] Concave collider repair failed to save prefab: " + path);
                        repaired = 0;
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return repaired;
        }

        private static void ScanManualLodDrift(string path, GameObject prefab, List<UnoptimizedMeshFinding> findings)
        {
            LODGroup lodGroup = prefab.GetComponentInChildren<LODGroup>(true);
            if (lodGroup == null)
                return;

            LOD[] lods = lodGroup.GetLODs();
            if (lods.Length < 3)
            {
                findings.Add(new UnoptimizedMeshFinding
                {
                    AssetPath = path,
                    Issue = "MANUAL_LOD_INSUFFICIENT_LEVELS",
                    Detail = "LODGroup must expose LOD0, LOD1, and LOD2.",
                    TriangleCount = 0
                });
                return;
            }

            int lod0Triangles = CountRenderers(lods[0].renderers);
            int lod1Triangles = CountRenderers(lods[1].renderers);
            int lod2Triangles = CountRenderers(lods[2].renderers);
            if (lod0Triangles <= 0)
                return;

            if (lod1Triangles > mathCeilRatio(lod0Triangles, 0.52f))
            {
                findings.Add(new UnoptimizedMeshFinding
                {
                    AssetPath = path,
                    Issue = "MANUAL_LOD1_BUDGET_DRIFT",
                    Detail = "LOD1 exceeds deterministic 50 percent triangle budget.",
                    TriangleCount = lod1Triangles
                });
            }

            if (lod2Triangles > mathCeilRatio(lod0Triangles, 0.12f))
            {
                findings.Add(new UnoptimizedMeshFinding
                {
                    AssetPath = path,
                    Issue = "MANUAL_LOD2_BUDGET_DRIFT",
                    Detail = "LOD2 exceeds deterministic 10 percent triangle budget.",
                    TriangleCount = lod2Triangles
                });
            }

            if (MaterialsMismatch(lods))
            {
                findings.Add(new UnoptimizedMeshFinding
                {
                    AssetPath = path,
                    Issue = "MANUAL_LOD_MATERIAL_MISMATCH",
                    Detail = "LOD renderers do not preserve LOD0 material slot count.",
                    TriangleCount = lod0Triangles
                });
            }
        }

        private static int CountPrefabTriangles(GameObject prefab)
        {
            int total = 0;
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter != null)
                    total += OfflineGeometryBaker.CountTriangles(filter.sharedMesh);
            }

            SkinnedMeshRenderer[] skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinned[i];
                if (renderer != null)
                    total += OfflineGeometryBaker.CountTriangles(renderer.sharedMesh);
            }

            return total;
        }

        private static int CountRenderers(Renderer[] renderers)
        {
            int total = 0;
            if (renderers == null)
                return total;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                MeshFilter filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                if (filter != null)
                    total += OfflineGeometryBaker.CountTriangles(filter.sharedMesh);
            }

            return total;
        }

        private static bool MaterialsMismatch(LOD[] lods)
        {
            int baseline = MaterialSlotCount(lods[0].renderers);
            if (baseline <= 0)
                return false;

            for (int i = 1; i < lods.Length; i++)
            {
                int count = MaterialSlotCount(lods[i].renderers);
                if (count > 0 && count != baseline)
                    return true;
            }

            return false;
        }

        private static int MaterialSlotCount(Renderer[] renderers)
        {
            if (renderers == null)
                return 0;

            int total = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.sharedMaterials != null)
                    total += renderer.sharedMaterials.Length;
            }

            return total;
        }

        private static int mathCeilRatio(int value, float ratio)
        {
            return Mathf.CeilToInt(value * ratio);
        }

        private static bool IsEnvironmentOrInteractable(string path)
        {
            string lower = path.Replace('\\', '/').ToLowerInvariant();
            return lower.Contains("/environment/") || lower.Contains("/interactable") || lower.Contains("/interactables/");
        }
    }
}
#endif
