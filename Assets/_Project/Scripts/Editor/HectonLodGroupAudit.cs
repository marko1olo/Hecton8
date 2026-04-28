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
        private const string ScifiFacilityRoot = "Assets/ScifiFacility";
        private const string PrefabRoot = "Assets/_Project/Prefabs";
        private const int TriangleThreshold = 10000;
        private const int MaxConsoleEntries = 48;

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

            MeshFilter[] meshFilters = assetRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
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

            SkinnedMeshRenderer[] skinnedMeshes = assetRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedMeshes.Length; i++)
            {
                SkinnedMeshRenderer skinnedMesh = skinnedMeshes[i];
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
