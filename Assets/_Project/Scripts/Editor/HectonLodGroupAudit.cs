#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Audits high-triangle prefab and model assets for mandatory LODGroup coverage.
    /// </summary>
    internal static class HectonLodGroupAudit
    {
        private const string MenuPath = "Hecton/Validation/Asset Pipeline/Audit LOD Groups";
        private const string ArtRoot = "Assets/_Project/Art";
        private const string ScifiFacilityRoot = "Assets/ScifiFacility";
        private const string PrefabRoot = "Assets/_Project/Prefabs";
        private const int TriangleThreshold = 2000;
        private const int MaxConsoleEntries = 48;
        private static readonly List<MeshFilter> s_MeshFilterScratch = new List<MeshFilter>(64);
        private static readonly List<SkinnedMeshRenderer> s_SkinnedMeshScratch = new List<SkinnedMeshRenderer>(16);

        internal sealed class AuditResult
        {
            internal int ScannedAssetCount;
            internal readonly List<string> Violations = new List<string>(128);
            internal readonly List<string> BrokenAssets = new List<string>(32);
            internal readonly List<string> QuarantineCandidatePaths = new List<string>(16);
        }

        [MenuItem(MenuPath, priority = 193)]
        private static void RunFromMenu()
        {
            AuditResult result = RunAudit();
            Debug.Log(
                $"[HectonLodGroupAudit] ScannedAssets={result.ScannedAssetCount}, " +
                $"Violations={result.Violations.Count}, BrokenAssets={result.BrokenAssets.Count}, " +
                $"QuarantineCandidates={result.QuarantineCandidatePaths.Count}.");
            LogEntries("LOD violations", result.Violations);
            LogEntries("Broken assets", result.BrokenAssets);
        }

        internal static AuditResult RunAudit()
        {
            AuditResult result = new AuditResult();
            ScanAssetSet(result, AssetDatabase.FindAssets("t:Model", new[] { ArtRoot }));
            ScanAssetSet(result, AssetDatabase.FindAssets("t:Prefab", new[] { ArtRoot }));
            ScanAssetSet(result, AssetDatabase.FindAssets("t:Model", new[] { ScifiFacilityRoot }));
            ScanAssetSet(result, AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }));
            return result;
        }

        private static void ScanAssetSet(AuditResult result, string[] assetGuids)
        {
            for (int i = 0; i < assetGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                if (string.IsNullOrWhiteSpace(assetPath))
                    continue;

                result.ScannedAssetCount++;
                GameObject assetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (assetRoot == null)
                {
                    RegisterBrokenAsset(result, assetPath, "asset root failed to load.");
                    continue;
                }

                int triangleCount = CountTriangles(assetRoot, out bool hasBrokenMeshReference);
                if (hasBrokenMeshReference)
                    RegisterBrokenAsset(result, assetPath, "contains renderer components with missing sharedMesh.");

                if (triangleCount > TriangleThreshold && assetRoot.GetComponentInChildren<LODGroup>(true) == null)
                {
                    result.Violations.Add(
                        $"{assetPath}: triangle count {triangleCount} exceeds {TriangleThreshold} and no LODGroup is present.");
                }
            }
        }

        private static int CountTriangles(GameObject assetRoot, out bool hasBrokenMeshReference)
        {
            hasBrokenMeshReference = false;
            int triangles = 0;

            s_MeshFilterScratch.Clear();
            assetRoot.GetComponentsInChildren(true, s_MeshFilterScratch);
            for (int i = 0; i < s_MeshFilterScratch.Count; i++)
            {
                MeshFilter meshFilter = s_MeshFilterScratch[i];
                if (meshFilter == null)
                    continue;

                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    hasBrokenMeshReference = true;
                    continue;
                }

                triangles += CountTriangles(mesh);
            }

            s_SkinnedMeshScratch.Clear();
            assetRoot.GetComponentsInChildren(true, s_SkinnedMeshScratch);
            for (int i = 0; i < s_SkinnedMeshScratch.Count; i++)
            {
                SkinnedMeshRenderer skinnedMesh = s_SkinnedMeshScratch[i];
                if (skinnedMesh == null)
                    continue;

                Mesh mesh = skinnedMesh.sharedMesh;
                if (mesh == null)
                {
                    hasBrokenMeshReference = true;
                    continue;
                }

                triangles += CountTriangles(mesh);
            }

            return triangles;
        }

        private static int CountTriangles(Mesh mesh)
        {
            int triangles = 0;
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                triangles += (int)(mesh.GetIndexCount(subMeshIndex) / 3u);

            return triangles;
        }

        private static void RegisterBrokenAsset(AuditResult result, string assetPath, string message)
        {
            string entry = $"{assetPath}: {message}";
            result.BrokenAssets.Add(entry);

            for (int i = 0; i < result.QuarantineCandidatePaths.Count; i++)
            {
                if (string.Equals(result.QuarantineCandidatePaths[i], assetPath, StringComparison.Ordinal))
                    return;
            }

            result.QuarantineCandidatePaths.Add(assetPath);
        }

        private static void LogEntries(string label, List<string> entries)
        {
            if (entries == null || entries.Count <= 0)
                return;

            int maxCount = Mathf.Min(MaxConsoleEntries, entries.Count);
            for (int i = 0; i < maxCount; i++)
                Debug.LogWarning($"[HectonLodGroupAudit] {label}: {entries[i]}");

            if (entries.Count > maxCount)
                Debug.LogWarning($"[HectonLodGroupAudit] {label}: truncated {entries.Count - maxCount} additional entries.");
        }
    }
}
#endif
