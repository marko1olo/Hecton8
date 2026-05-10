#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hecton8.Editor;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Audits model UV2/lightmap readiness for Bakery and enables importer auto-unwrap when Unity can repair it.
    /// </summary>
    internal static class HectonBakeryUvAudit
    {
        private const string MenuPath = "Hecton/Validation/Asset Pipeline/Audit Bakery UVs";
        private const int MaxTrianglesPerMeshCheck = 4096;
        private const float DegenerateAreaThreshold = 0.000001f;
        private const float MaxUvEmptySpaceRatio = 0.30f;
        private const float UvAreaCoverageFudge = 1.18f;
        private const float UvOverlapAreaFudge = 1.08f;
        private const int UvScratchInitialCapacity = 8192;
        private const int IndexScratchInitialCapacity = 12288;

        // COLD ALLOC: editor audit scratch buffers reused across the single-threaded UV pass.
        private static readonly List<Vector2> s_Uv2Scratch = new List<Vector2>(UvScratchInitialCapacity);
        private static readonly List<Vector2> s_Uv0Scratch = new List<Vector2>(UvScratchInitialCapacity);
        private static readonly List<int> s_IndexScratch = new List<int>(IndexScratchInitialCapacity);

        internal sealed class AuditResult
        {
            internal int ScannedModelCount;
            internal readonly List<string> AutoFixedModels = new List<string>(32);
            internal readonly List<string> ManualReviewModels = new List<string>(64);
        }

        private sealed class ModelInspection
        {
            internal bool HasIssue;
            internal bool CanAttemptAutoFix;
            internal readonly List<string> Issues = new List<string>(8);
        }

        [MenuItem(MenuPath, priority = 192)]
        private static void RunFromMenu()
        {
            AuditResult result = RunAudit();
            Debug.Log(
                $"[HectonBakeryUvAudit] ScannedModels={result.ScannedModelCount}, AutoFixed={result.AutoFixedModels.Count}, " +
                $"ManualReview={result.ManualReviewModels.Count}.");
        }

        internal static AuditResult RunAudit()
        {
            AuditResult result = new AuditResult();
            List<string> fbxPaths = HectonFBXPostprocessor.CollectFbxPaths(HectonFBXPostprocessor.ManagedFbxRoots);

            try
            {
                for (int i = 0; i < fbxPaths.Count; i++)
                {
                    string modelPath = fbxPaths[i];
                    EditorUtility.DisplayProgressBar(
                        "HECTON-8 Bakery UV Audit",
                        modelPath,
                        fbxPaths.Count > 0 ? (i + 1f) / fbxPaths.Count : 1f);
                    result.ScannedModelCount++;

                    ModelInspection inspection = InspectModel(modelPath);
                    if (!inspection.HasIssue)
                        continue;

                    ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                    if (inspection.CanAttemptAutoFix && importer != null && !importer.generateSecondaryUV)
                    {
                        importer.generateSecondaryUV = true;
                        importer.SaveAndReimport();

                        ModelInspection postFixInspection = InspectModel(modelPath);
                        if (!postFixInspection.HasIssue)
                        {
                            result.AutoFixedModels.Add($"{modelPath}: enabled ModelImporter.generateSecondaryUV.");
                            continue;
                        }

                        AppendManualReview(result, modelPath, postFixInspection.Issues);
                        continue;
                    }

                    AppendManualReview(result, modelPath, inspection.Issues);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return result;
        }

        private static void AppendManualReview(AuditResult result, string modelPath, List<string> issues)
        {
            for (int i = 0; i < issues.Count; i++)
                result.ManualReviewModels.Add($"{modelPath}: {issues[i]}");
        }

        private static ModelInspection InspectModel(string modelPath)
        {
            ModelInspection inspection = new ModelInspection();
            UnityEngine.Object[] assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(modelPath);

            for (int i = 0; i < assetsAtPath.Length; i++)
            {
                Mesh mesh = assetsAtPath[i] as Mesh;
                if (mesh == null || mesh.vertexCount <= 0)
                    continue;

                List<Vector2> uv2 = s_Uv2Scratch;
                EnsureScratchCapacity(uv2, mesh.vertexCount);
                uv2.Clear();
                mesh.GetUVs(1, uv2);
                if (uv2.Count != mesh.vertexCount)
                {
                    inspection.HasIssue = true;
                    inspection.CanAttemptAutoFix = true;
                    inspection.Issues.Add($"{mesh.name}: missing or incomplete UV2/lightmap channel.");
                }
                else if (TryDetectUvOverlap(mesh, uv2, out string overlapReason))
                {
                    inspection.HasIssue = true;
                    inspection.CanAttemptAutoFix = true;
                    inspection.Issues.Add($"{mesh.name}: {overlapReason}");
                }

                List<Vector2> uv0 = s_Uv0Scratch;
                EnsureScratchCapacity(uv0, mesh.vertexCount);
                uv0.Clear();
                mesh.GetUVs(0, uv0);
                if (uv0.Count != mesh.vertexCount)
                {
                    inspection.HasIssue = true;
                    inspection.Issues.Add($"{mesh.name}: missing or incomplete UV0; UV island utilization cannot be measured.");
                }
                else if (TryMeasureUvEmptySpace(mesh, uv0, out float emptySpaceRatio, out string utilizationReason)
                    && emptySpaceRatio > MaxUvEmptySpaceRatio)
                {
                    inspection.HasIssue = true;
                    inspection.Issues.Add(utilizationReason);
                }
            }

            return inspection;
        }

        private static bool TryDetectUvOverlap(Mesh mesh, List<Vector2> uv2, out string reason)
        {
            int testedTriangles = 0;
            List<int> indices = s_IndexScratch;
            EnsureScratchCapacity(indices, ResolveIndexScratchCapacity(mesh));
            float occupiedArea = 0f;

            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                indices.Clear();
                mesh.GetTriangles(indices, subMeshIndex, true);
                int triangleCount = indices.Count / 3;
                for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
                {
                    if (testedTriangles >= MaxTrianglesPerMeshCheck)
                    {
                        reason = "partial UV2 overlap scan reached safety limit; manual review required.";
                        return true;
                    }

                    int i0 = indices[triangleIndex * 3];
                    int i1 = indices[triangleIndex * 3 + 1];
                    int i2 = indices[triangleIndex * 3 + 2];
                    float triangleArea = Mathf.Abs(SignedArea(uv2[i0], uv2[i1], uv2[i2])) * 0.5f;

                    if (triangleArea <= DegenerateAreaThreshold)
                    {
                        reason = $"{mesh.name}: UV2 triangle is degenerate/self-intersecting.";
                        return true;
                    }

                    occupiedArea += triangleArea;
                    testedTriangles++;
                }
            }

            if (occupiedArea * UvOverlapAreaFudge > 1f)
            {
                reason = $"{mesh.name}: UV2 estimated occupied area exceeds atlas bounds; probable lightmap overlap.";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        private static bool TryMeasureUvEmptySpace(
            Mesh mesh,
            List<Vector2> uv0,
            out float emptySpaceRatio,
            out string reason)
        {
            List<int> indices = s_IndexScratch;
            EnsureScratchCapacity(indices, ResolveIndexScratchCapacity(mesh));
            int triangleCount = 0;
            float occupiedArea = 0f;

            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                indices.Clear();
                mesh.GetTriangles(indices, subMeshIndex, true);
                int submeshTriangleCount = indices.Count / 3;
                for (int triangleIndex = 0; triangleIndex < submeshTriangleCount; triangleIndex++)
                {
                    int i0 = indices[triangleIndex * 3];
                    int i1 = indices[triangleIndex * 3 + 1];
                    int i2 = indices[triangleIndex * 3 + 2];
                    float signedArea = SignedArea(uv0[i0], uv0[i1], uv0[i2]);
                    float triangleArea = Mathf.Abs(signedArea) * 0.5f;
                    if (triangleArea <= DegenerateAreaThreshold)
                        continue;

                    occupiedArea += triangleArea;
                    triangleCount++;
                }
            }

            if (triangleCount <= 0)
            {
                emptySpaceRatio = 1f;
                reason = $"{mesh.name}: UV0 has no measurable triangles; asset is Bloated.";
                return true;
            }

            float estimatedOccupied = Mathf.Clamp01(occupiedArea * UvAreaCoverageFudge);
            emptySpaceRatio = 1f - estimatedOccupied;
            reason = $"{mesh.name}: UV0 empty space {emptySpaceRatio:P0} exceeds 30%; asset is Bloated.";
            return true;
        }

        private static float SignedArea(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private static void EnsureScratchCapacity<T>(List<T> scratch, int capacity)
        {
            if (scratch.Capacity < capacity)
                scratch.Capacity = capacity;
        }

        private static int ResolveIndexScratchCapacity(Mesh mesh)
        {
            long indexCount = ResolveIndexCount(mesh);
            return indexCount > int.MaxValue ? int.MaxValue : (int)indexCount;
        }

        private static long ResolveIndexCount(Mesh mesh)
        {
            if (mesh == null)
                return 0L;

            long indexCount = 0L;
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                indexCount += mesh.GetIndexCount(subMeshIndex);

            return indexCount;
        }
    }
}
#endif
